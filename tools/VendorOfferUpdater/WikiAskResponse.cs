using System;
using System.Net.Http;
using System.Text.Json;

namespace VendorOfferUpdater
{
    /// <summary>
    /// Which of the three deterministic action=ask response shapes a body
    /// carries, plus the fourth case of a body that is none of them.
    /// <para>
    /// All of these arrive as HTTP 200. "results" is a JSON object when rows
    /// exist and a JSON array when they do not, and a query the API refuses
    /// returns neither: it returns a top-level "error" object with the same
    /// 200 status. A caller that tests only for the object shape therefore
    /// reads a refusal as "this branch of the wiki holds no rows".
    /// </para>
    /// </summary>
    internal enum WikiAskShape
    {
        Rows,
        NoRows,
        ApiError,
        Unrecognized,
    }

    /// <summary>
    /// A MediaWiki API error carried inside an HTTP 200 body.
    /// </summary>
    public class WikiApiError
    {
        public WikiApiError(string code, string info)
        {
            Code = code;
            Info = info;
        }

        public string Code { get; }

        public string Info { get; }

        public override string ToString()
        {
            return $"{Code}: {Info}";
        }
    }

    /// <summary>
    /// Thrown when the wiki refused a query on every attempt it was given.
    /// Derives from <see cref="HttpRequestException"/> so existing per-request
    /// handlers keep treating it as a failed request; call sites that record
    /// the refused section catch this type first.
    /// </summary>
    public class WikiApiErrorException : HttpRequestException
    {
        public WikiApiErrorException(WikiApiError error, string section, int attempts)
            : base($"Wiki API refused section [{section}] after {attempts} attempt(s): {error}")
        {
            Error = error;
            Section = section;
            Attempts = attempts;
        }

        public WikiApiError Error { get; }

        public string Section { get; }

        public int Attempts { get; }
    }

    /// <summary>
    /// One reading of an action=ask response: its shape, its rows when it has
    /// them, and its error when the API refused the query.
    /// </summary>
    internal readonly struct WikiAskReading
    {
        private WikiAskReading(WikiAskShape shape, JsonElement results, WikiApiError? error)
        {
            Shape = shape;
            Results = results;
            Error = error;
        }

        internal WikiAskShape Shape { get; }

        /// <summary>The "results" object. Only meaningful for Rows.</summary>
        internal JsonElement Results { get; }

        /// <summary>The refusal. Only set for ApiError.</summary>
        internal WikiApiError? Error { get; }

        internal static WikiAskReading Rows(JsonElement results) =>
            new WikiAskReading(WikiAskShape.Rows, results, null);

        internal static WikiAskReading NoRows() =>
            new WikiAskReading(WikiAskShape.NoRows, default, null);

        internal static WikiAskReading Refused(WikiApiError error) =>
            new WikiAskReading(WikiAskShape.ApiError, default, error);

        internal static WikiAskReading Unrecognized() =>
            new WikiAskReading(WikiAskShape.Unrecognized, default, null);
    }

    /// <summary>
    /// The single reader for action=ask responses, used by every call site
    /// that asks the wiki for rows. It exists so that "the API returned no
    /// rows" and "the API refused the query" can never again produce the
    /// same answer at one call site and a different one at another.
    /// </summary>
    internal static class WikiAskResponse
    {
        /// <summary>
        /// Reads a body's error, or null when the body carries none.
        /// A body that is not JSON at all is reported as an error rather
        /// than thrown from: the wiki serves its rate-limit block page as
        /// HTML with an HTTP 200 status, and that is a refusal, not data.
        /// </summary>
        internal static WikiApiError? ReadApiError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return new WikiApiError("empty-response", "The response body was empty.");
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                return ReadApiError(doc.RootElement);
            }
            catch (JsonException ex)
            {
                return new WikiApiError(
                    "unparseable-response",
                    $"The response body was not JSON ({ex.Message}). First 200 characters: " +
                    Summarize(body));
            }
        }

        internal static WikiApiError? ReadApiError(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("error", out var error))
            {
                return null;
            }

            // MediaWiki writes {"error":{"code":..,"info":..}}; SMW's own
            // query errors come back under the same key with a different
            // body, so the whole element is kept as the info text when the
            // documented fields are absent.
            string code = ReadString(error, "code") ?? "(no code)";
            string info = ReadString(error, "info")
                ?? ReadString(error, "*")
                ?? Summarize(error.GetRawText());

            return new WikiApiError(code, info);
        }

        /// <summary>
        /// Classifies a parsed action=ask response. The caller owns the
        /// <see cref="JsonDocument"/> and must keep it alive while it reads
        /// <see cref="WikiAskReading.Results"/>.
        /// </summary>
        internal static WikiAskReading Read(JsonElement root)
        {
            var error = ReadApiError(root);
            if (error != null)
            {
                return WikiAskReading.Refused(error);
            }

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("query", out var query) ||
                query.ValueKind != JsonValueKind.Object ||
                !query.TryGetProperty("results", out var results))
            {
                return WikiAskReading.Unrecognized();
            }

            if (results.ValueKind == JsonValueKind.Array)
            {
                return results.GetArrayLength() == 0
                    ? WikiAskReading.NoRows()
                    : WikiAskReading.Unrecognized();
            }

            if (results.ValueKind != JsonValueKind.Object)
            {
                return WikiAskReading.Unrecognized();
            }

            using var members = results.EnumerateObject();
            return members.MoveNext()
                ? WikiAskReading.Rows(results)
                : WikiAskReading.NoRows();
        }

        private static string? ReadString(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }

        private static string Summarize(string text)
        {
            string flat = text.Replace('\n', ' ').Replace('\r', ' ');
            return flat.Length <= 200 ? flat : flat.Substring(0, 200) + "...";
        }
    }
}
