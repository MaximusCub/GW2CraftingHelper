using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Request-identity key for Plan History dedup: a repeat Generate of
    /// the same request bumps the existing row instead of creating a
    /// second one. Covers exactly Module's PersistedPlanMetadata four
    /// fields plus the item-id-keyed ignore set - Homestead tiers and
    /// currency valuation are deliberately excluded (they are solve
    /// context, not request identity; see Models/PlanHistoryEntry.cs).
    /// <para>
    /// Deliberately strict: a PriceBasis/UseOwnMaterials/
    /// ValueOwnMaterials change makes a DIFFERENT row, because loose
    /// dedup would make one row's stored cost jump between incompatible
    /// bases with no visible cause.
    /// </para>
    /// </summary>
    internal static class PlanHistoryDedupKey
    {
        /// <summary>
        /// Order-insensitive over requestItems and ignoredItemIds;
        /// sensitive to the three flags. Deterministic across sessions
        /// (no GetHashCode). The exact format is pinned by
        /// PlanHistoryDedupKeyTests: "i:" + items sorted by
        /// (ItemId, Quantity) rendered "id*qty" joined "," + "|o:" +
        /// (useOwn ? 1 : 0) + "|b:" + (int)priceBasis + "|v:" +
        /// (valueOwnMaterials ? 1 : 0) + "|x:" + ignored ids sorted
        /// ascending joined ",". Null/empty collections render as the
        /// empty string, not a missing segment.
        /// </summary>
        public static string Compute(
            IReadOnlyList<PlanRequestItem> requestItems,
            bool useOwnMaterials,
            PriceBasis priceBasis,
            bool valueOwnMaterials,
            IReadOnlyCollection<int> ignoredItemIds)
        {
            var sb = new StringBuilder("i:");

            if (requestItems != null)
            {
                var sorted = requestItems
                    .Where(item => item != null)
                    .OrderBy(item => item.ItemId)
                    .ThenBy(item => item.Quantity)
                    .ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(sorted[i].ItemId.ToString(CultureInfo.InvariantCulture));
                    sb.Append('*');
                    sb.Append(sorted[i].Quantity.ToString(CultureInfo.InvariantCulture));
                }
            }

            sb.Append("|o:").Append(useOwnMaterials ? 1 : 0);
            sb.Append("|b:").Append(((int)priceBasis).ToString(CultureInfo.InvariantCulture));
            sb.Append("|v:").Append(valueOwnMaterials ? 1 : 0);
            sb.Append("|x:");

            if (ignoredItemIds != null)
            {
                var sortedIds = ignoredItemIds.OrderBy(id => id).ToList();
                for (int i = 0; i < sortedIds.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(sortedIds[i].ToString(CultureInfo.InvariantCulture));
                }
            }

            return sb.ToString();
        }

        /// <summary>The key an index row's own persisted identity computes to.</summary>
        public static string ForEntry(PlanHistoryEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return Compute(
                entry.RequestItems,
                entry.UseOwnMaterials,
                entry.PriceBasis,
                entry.ValueOwnMaterials,
                entry.IgnoredItemIds);
        }
    }
}
