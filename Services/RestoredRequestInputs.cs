using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Maps a restored plan's persisted request back into what the input
    /// strip's rows should show (Blish-free, unit-testable) - the reverse
    /// of <see cref="ItemRowRequestBuilder.Build"/>. Restoring the plan
    /// without its request left Generate Plan answering "Add at least one
    /// item" after every module restart until the user retyped their own
    /// items; these seeds are what <c>ItemInputRowStrip.RestoreRows</c>
    /// reseeds the strip from so a restored request re-solves with zero
    /// retyping.
    /// </summary>
    internal static class RestoredRequestInputs
    {
        /// <summary>
        /// Shown as the search box's placeholder for a row whose item name
        /// is absent from the restored metadata. Neutral on purpose: item
        /// ids are internal-only, so an unnamed row must never fall back to
        /// displaying its id - it keeps the id off screen and still solves.
        /// </summary>
        public const string UnnamedRowPlaceholder = "Unnamed item";

        /// <summary>
        /// One restored input row: the resolved item id, the display name
        /// (null when the restored metadata has none - see
        /// <see cref="UnnamedRowPlaceholder"/>) and the quantity box text.
        /// </summary>
        internal sealed class RowSeed
        {
            public RowSeed(int itemId, string itemName, string quantityText)
            {
                ItemId = itemId;
                ItemName = itemName;
                QuantityText = quantityText;
            }

            public int ItemId { get; }

            public string ItemName { get; }

            public string QuantityText { get; }
        }

        /// <summary>
        /// Builds one seed per persisted request item, in request order.
        /// Names come from the restored result's own ItemMetadata - never
        /// an API call; redrawing input rows is not worth a network round
        /// trip. Returns an empty list (never null) when the persisted
        /// request is null or empty, so a caller keeps its default row
        /// instead of restoring an empty strip. Quantities below 1 seed as
        /// "1", mirroring <see cref="ItemRowRequestBuilder.Build"/>'s own
        /// clamp.
        /// </summary>
        public static IReadOnlyList<RowSeed> BuildRowSeeds(
            IReadOnlyList<PlanRequestItem> requestItems,
            IReadOnlyDictionary<int, ItemMetadata> itemMetadata)
        {
            var seeds = new List<RowSeed>();
            if (requestItems == null)
            {
                return seeds;
            }

            foreach (var item in requestItems)
            {
                if (item == null)
                {
                    continue;
                }

                string name = null;
                if (itemMetadata != null
                    && itemMetadata.TryGetValue(item.ItemId, out var metadata)
                    && !string.IsNullOrWhiteSpace(metadata?.Name))
                {
                    name = metadata.Name;
                }

                int quantity = item.Quantity < 1 ? 1 : item.Quantity;
                seeds.Add(new RowSeed(item.ItemId, name, quantity.ToString()));
            }

            return seeds;
        }
    }
}
