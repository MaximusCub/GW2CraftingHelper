using System.Globalization;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Parses user-entered currency valuation text from the Settings tab
    /// into a positive copper-per-unit value. Kept separate from
    /// SettingsTabContent (Blish-coupled, untestable per repo invariant) so
    /// the actual validation logic is covered by a real, Blish-free test.
    /// Mirrors the constraint CurrencyValuation's constructor enforces
    /// (positive value, no coin-keyed entries handled by the caller) so a
    /// value that parses here is always safe to hand to that constructor.
    /// </summary>
    public static class SettingsInputParser
    {
        /// <summary>
        /// Attempts to parse <paramref name="text"/> as a positive integer
        /// copper-per-unit valuation. Digits only - no sign, decimal point,
        /// or thousands separators. Returns false (with
        /// <paramref name="copperPerUnit"/> set to 0) for null, blank,
        /// non-numeric, zero, negative, decimal, or overflowing input.
        /// An empty/blank string is treated as "unset" by callers, not as
        /// an error - this method only judges whether non-blank text is a
        /// valid positive integer.
        /// </summary>
        public static bool TryParseCopperValue(string text, out long copperPerUnit)
        {
            copperPerUnit = 0;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();

            if (!long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed))
            {
                return false;
            }

            if (parsed <= 0)
            {
                return false;
            }

            copperPerUnit = parsed;
            return true;
        }
    }
}
