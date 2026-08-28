using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Which items a Plan History view can draw an icon for - the input to
    /// its background stat top-up (Views/Rendering/ItemStatWarmer.cs).
    ///
    /// <para>
    /// EVERY summary of every entry, not the first of each. A row draws the
    /// first summary's icon, but any row can be expanded into one detail
    /// line per item, and each of those lines draws its own icon with its
    /// own hover. Warming only the row icons would leave the detail lines
    /// showing the identity-only fallback, which is the defect this whole
    /// path exists to close.
    /// </para>
    /// <para>
    /// Blish-free (repo invariant), so the rule above is directly testable.
    /// </para>
    /// </summary>
    internal static class PlanHistoryItemIds
    {
        /// <summary>
        /// Distinct, positive item ids across <paramref name="entries"/>,
        /// in first-seen order. Never null. A summary with no id is a row
        /// written before ids were captured; it renders from its own name
        /// and icon and has nothing to warm.
        /// </summary>
        public static IReadOnlyList<int> ForEntries(IReadOnlyList<PlanHistoryEntry> entries)
        {
            var ids = new List<int>();
            if (entries == null)
            {
                return ids;
            }

            var seen = new HashSet<int>();
            foreach (var entry in entries)
            {
                if (entry?.ItemSummaries == null)
                {
                    continue;
                }

                foreach (var summary in entry.ItemSummaries)
                {
                    if (summary != null && summary.ItemId > 0 && seen.Add(summary.ItemId))
                    {
                        ids.Add(summary.ItemId);
                    }
                }
            }

            return ids;
        }
    }
}
