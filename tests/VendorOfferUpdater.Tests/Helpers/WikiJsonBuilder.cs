using System.Collections.Generic;
using System.Text.Json;

namespace VendorOfferUpdater.Tests.Helpers
{
    /// <summary>
    /// Builds SMW action=ask JSON response payloads for testing WikiSmwClient.
    /// Only constructs JSON strings — contains no parsing or production logic.
    /// </summary>
    public class WikiJsonBuilder
    {
        private readonly List<object> _results = new();
        private int? _continueOffset;

        public WikiJsonBuilder AddResult(
            string pageName,
            int? gameId = null,
            string itemName = null,
            int? quantity = null,
            List<(int value, string currency)> costs = null,
            string vendor = null,
            List<string> locations = null)
        {
            _results.Add(new ResultEntry
            {
                PageName = pageName,
                GameId = gameId,
                ItemName = itemName,
                Quantity = quantity,
                Costs = costs,
                Vendor = vendor,
                Locations = locations
            });
            return this;
        }

        public WikiJsonBuilder WithContinueOffset(int offset)
        {
            _continueOffset = offset;
            return this;
        }

        public string Build()
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject(); // root

            writer.WriteStartObject("query");
            writer.WriteStartObject("results");

            foreach (ResultEntry entry in _results)
            {
                writer.WriteStartObject(entry.PageName);
                writer.WriteStartObject("printouts");

                // Has game id
                writer.WriteStartArray("Has game id");
                if (entry.GameId.HasValue)
                {
                    writer.WriteNumberValue(entry.GameId.Value);
                }
                writer.WriteEndArray();

                // Sells item
                writer.WriteStartArray("Sells item");
                if (entry.ItemName != null)
                {
                    writer.WriteStartObject();
                    writer.WriteString("fulltext", entry.ItemName);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                // Has item quantity
                writer.WriteStartArray("Has item quantity");
                if (entry.Quantity.HasValue)
                {
                    writer.WriteNumberValue(entry.Quantity.Value);
                }
                writer.WriteEndArray();

                // Has item cost
                writer.WriteStartArray("Has item cost");
                if (entry.Costs != null)
                {
                    foreach (var (value, currency) in entry.Costs)
                    {
                        writer.WriteStartObject();

                        writer.WriteStartObject("Has item value");
                        writer.WriteStartArray("item");
                        writer.WriteStringValue(value.ToString());
                        writer.WriteEndArray();
                        writer.WriteEndObject();

                        writer.WriteStartObject("Has item currency");
                        writer.WriteStartArray("item");
                        writer.WriteStringValue(currency ?? "");
                        writer.WriteEndArray();
                        writer.WriteEndObject();

                        writer.WriteEndObject();
                    }
                }
                writer.WriteEndArray();

                // Has vendor
                writer.WriteStartArray("Has vendor");
                if (entry.Vendor != null)
                {
                    writer.WriteStartObject();
                    writer.WriteString("fulltext", entry.Vendor);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                // Located in
                writer.WriteStartArray("Located in");
                if (entry.Locations != null)
                {
                    foreach (var loc in entry.Locations)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("fulltext", loc);
                        writer.WriteEndObject();
                    }
                }
                writer.WriteEndArray();

                writer.WriteEndObject(); // printouts
                writer.WriteEndObject(); // pageName
            }

            writer.WriteEndObject(); // results
            writer.WriteEndObject(); // query

            if (_continueOffset.HasValue)
            {
                writer.WriteNumber("query-continue-offset", _continueOffset.Value);
            }

            writer.WriteEndObject(); // root

            writer.Flush();
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Builds an empty SMW response (results as empty array instead of object).
        /// </summary>
        public static string BuildEmpty()
        {
            return "{\"query\":{\"results\":[]}}";
        }

        private class ResultEntry
        {
            public string PageName { get; set; }
            public int? GameId { get; set; }
            public string ItemName { get; set; }
            public int? Quantity { get; set; }
            public List<(int value, string currency)> Costs { get; set; }
            public string Vendor { get; set; }
            public List<string> Locations { get; set; }
        }
    }
}
