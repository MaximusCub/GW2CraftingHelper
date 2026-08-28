using System;
using System.Globalization;
using System.Text;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Splits one <see cref="ModuleLogEntry"/> into the two columns the Log
    /// tab renders - a dim "[LEVEL] timestamp [tag]" prefix and the message
    /// itself - and re-composes the single flat line that the search filter,
    /// the Copy button and the truncation tooltip all still work in.
    /// <see cref="Compose"/> is what keeps those two views of an entry from
    /// diverging: <see cref="Line"/> reproduces the exact string
    /// LogTabContent.FormatLine used to build before the row was split, so
    /// copy output and search matches are unchanged by the split - with the
    /// one deliberate exception <see cref="Message"/> documents, a message
    /// carrying CR/LF/TAB, which is now flattened to a single line in every
    /// one of those views at once.
    /// Blish-free by design so it can be unit-tested directly - the same
    /// reason <see cref="LogViewFloor"/> lives here rather than beside
    /// LogTabContent.
    /// </summary>
    internal static class LogLineFormat
    {
        // Timestamp culture policy (applies to every user-facing timestamp
        // in this module, not just the line below): formatted with
        // CultureInfo.InvariantCulture rather than the ambient
        // CurrentCulture, because the module's UI strings are English-only.
        // Invariant keeps month abbreviations and the AM/PM designator
        // stable - under de-DE, "h:mm tt" yields an EMPTY AM/PM designator,
        // so "2:14" would be ambiguous with "14:14" - and stops ':' from
        // being culture-substituted inside "HH:mm:ss".
        public static string Prefix(ModuleLogEntry entry)
        {
            string tagPart = string.IsNullOrEmpty(entry?.Tag) ? string.Empty : $" [{entry.Tag}]";
            return Time(entry) + tagPart;
        }

        /// <summary>
        /// The prefix's LEVEL-and-stamp half, without the tag - the Log
        /// tab's own Time column, which is banded separately from the tag
        /// beside it (see LogGutterLayout). <see cref="Prefix"/> still
        /// composes the two, so the copied line and the search corpus are
        /// unchanged by the split.
        /// </summary>
        public static string Time(ModuleLogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            string levelText = entry.Level.ToString().ToUpperInvariant();
            string timestampText = entry.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            return $"[{levelText}] {timestampText}";
        }

        /// <summary>
        /// The entry's message as ONE line: every run of CR/LF/TAB becomes a
        /// single space, a leading run is dropped, and a flattened message
        /// keeps no trailing whitespace.
        /// A log row is a fixed-height Panel that clips what it cannot draw,
        /// and BitmapFont.MeasureString reports a multi-line string's WIDEST
        /// LINE rather than its full extent - so an un-flattened message
        /// whose first line happens to fit would render as that line alone,
        /// with no ellipsis and no tooltip to say the rest was dropped.
        /// Exception messages (HTTP, serialization) embed newlines routinely.
        /// Flattening here rather than at the label is deliberate: it also
        /// keeps Copy's Environment.NewLine join at one line per entry.
        /// </summary>
        public static string Message(ModuleLogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return Flatten(entry.Message);
        }

        /// <summary>
        /// The single separating space between the two rendered columns -
        /// the one character that is NOT drawn on screen (the message
        /// label's own x offset supplies that gap instead), so it lives
        /// here rather than in either column's own text.
        /// </summary>
        public static string Compose(string prefix, string message)
        {
            return (prefix ?? string.Empty) + " " + (message ?? string.Empty);
        }

        public static string Line(ModuleLogEntry entry)
        {
            return Compose(Prefix(entry), Message(entry));
        }

        private static readonly char[] BreakChars = { '\r', '\n', '\t' };

        private static string Flatten(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            // Overwhelmingly the common case, and it keeps the caller's own
            // string instance rather than allocating a copy per rendered row.
            if (message.IndexOfAny(BreakChars) < 0)
            {
                return message;
            }

            var builder = new StringBuilder(message.Length);
            foreach (char c in message)
            {
                if (c == '\r' || c == '\n' || c == '\t')
                {
                    if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
                    {
                        builder.Append(' ');
                    }

                    continue;
                }

                builder.Append(c);
            }

            while (builder.Length > 0 && builder[builder.Length - 1] == ' ')
            {
                builder.Length--;
            }

            return builder.ToString();
        }
    }
}
