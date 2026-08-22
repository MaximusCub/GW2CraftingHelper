using System;
using System.Globalization;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Splits one <see cref="ModuleLogEntry"/> into the two columns the Log
    /// tab renders - a dim "[LEVEL] timestamp [tag]" prefix and the message
    /// itself - and re-composes the single flat line that the search filter,
    /// the Copy button and the truncation tooltip all still work in.
    /// <see cref="Compose"/> is what keeps those two views of an entry from
    /// diverging: <see cref="Line"/> reproduces the exact string
    /// LogTabContent.FormatLine used to build before the row was split, so
    /// copy output and search matches are unchanged by the split.
    /// Blish-free by design so it can be unit-tested directly - the same
    /// reason <see cref="LogViewFloor"/> lives here rather than beside
    /// LogTabContent.
    /// </summary>
    public static class LogLineFormat
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
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            string levelText = entry.Level.ToString().ToUpperInvariant();
            string timestampText = entry.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string tagPart = string.IsNullOrEmpty(entry.Tag) ? string.Empty : $" [{entry.Tag}]";
            return $"[{levelText}] {timestampText}{tagPart}";
        }

        public static string Message(ModuleLogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return entry.Message ?? string.Empty;
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
    }
}
