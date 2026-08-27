using System.Collections.Generic;
using System.Globalization;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pure string shaping for Plan History rows - shared by the tab (row
    /// label, detail lines) and Module's Re-solve path (the request label
    /// handed to the Generate delegate), so the two can never drift into
    /// different spellings of the same entry.
    /// </summary>
    internal static class PlanHistoryLabels
    {
        /// <summary>
        /// Shown for an item summary whose name was never captured.
        /// Neutral on purpose: item ids are internal-only, so an unnamed
        /// entry must never fall back to displaying its id.
        /// </summary>
        public const string UnnamedItem = "Unknown item";

        /// <summary>
        /// One "Name xN" line per item summary, in capture order. The
        /// quantity suffix is omitted at 1, matching the Ranker's own row
        /// naming. Returns an empty list (never null) for a null entry or
        /// summary list.
        /// </summary>
        public static IReadOnlyList<string> ItemLineTexts(PlanHistoryEntry entry)
        {
            var lines = new List<string>();
            if (entry?.ItemSummaries == null)
            {
                return lines;
            }

            foreach (var summary in entry.ItemSummaries)
            {
                if (summary == null)
                {
                    continue;
                }

                string name = string.IsNullOrEmpty(summary.Name) ? UnnamedItem : summary.Name;
                lines.Add(summary.Quantity > 1
                    ? name + " x" + summary.Quantity.ToString(CultureInfo.InvariantCulture)
                    : name);
            }

            return lines;
        }

        /// <summary>
        /// The row's one-line label: the item lines through
        /// RequestLabelFormatter's "+N more" cap.
        /// </summary>
        public static string RowLabel(PlanHistoryEntry entry)
        {
            return RequestLabelFormatter.Format(ItemLineTexts(entry));
        }

        /// <summary>
        /// The row hover's full, uncapped item list - one line per item,
        /// no "+N more" truncation.
        /// </summary>
        public static string FullItemList(PlanHistoryEntry entry)
        {
            return string.Join("\n", ItemLineTexts(entry));
        }

        /// <summary>
        /// The detail panel's settings line: the three request flags in
        /// one caption, joined by three spaces.
        /// </summary>
        public static string SettingsLine(bool useOwnMaterials, PriceBasis priceBasis, bool valueOwnMaterials)
        {
            string basis = priceBasis == PriceBasis.BuyOrder ? "buy orders" : "instant buy";
            return "Own materials: " + (useOwnMaterials ? "on" : "off")
                + "   Prices: " + basis
                + "   Value own materials: " + (valueOwnMaterials ? "on" : "off");
        }
    }
}
