using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// What one slot of the Crafting Ranker's priority list is allowed to
    /// see: the account state left over after every higher-priority slot has
    /// taken its claim. Produced by Services.RankerPriorityCascade.
    ///
    /// Slot 0 sees the untouched account. Slot n sees the account minus the
    /// union of what slots 0..n-1 consume, which is not the same as their
    /// shopping lists - a plan that buys an intermediate never consumes that
    /// intermediate's ingredients, so the consumption record is the solver's
    /// own UsedMaterials, taken after the solve.
    /// </summary>
    internal class RankerSlotAvailability
    {
        /// <summary>
        /// The snapshot to hand the pipeline for this slot's owned solve.
        /// Null when the account has no snapshot at all, in which case the
        /// cascade is inert and every slot solves unreduced.
        /// </summary>
        public AccountSnapshot Snapshot { get; set; }

        /// <summary>
        /// Coin left after higher slots have paid their own coin costs. Null
        /// when there is no snapshot, which suppresses the affordability
        /// chip rather than reading as zero coin. Coin is never a solver
        /// input; this changes only what "can I afford this" means, and it
        /// means "after paying for everything above it".
        /// </summary>
        public int? CoinCopper { get; set; }

        /// <summary>
        /// Wallet amounts left after higher slots, by currency id. Empty
        /// (never null) when there is no snapshot. The solver never nets the
        /// wallet against a plan's currency costs (see AccountCurrencyIndex),
        /// so this ledger is the cascade's own arithmetic.
        /// </summary>
        public IReadOnlyDictionary<int, int> Currency { get; set; }

        /// <summary>
        /// Output units of each daily-cooldown-gated recipe that higher slots
        /// have already claimed, by item id. A daily cap is per account, so
        /// this slot's crafts queue behind them. Empty, never null.
        /// </summary>
        public IReadOnlyDictionary<int, int> ClaimedGatedUnits { get; set; }

        /// <summary>
        /// Item ids a higher slot drew from the account. Intersected against
        /// this slot's own plan steps to tell the user that a cost is caused
        /// by their own ordering rather than by the item. Empty, never null.
        /// </summary>
        public IReadOnlyCollection<int> ClaimedItemIds { get; set; }

        /// <summary>Currency ids a higher slot spent. Empty, never null.</summary>
        public IReadOnlyCollection<int> ClaimedCurrencyIds { get; set; }
    }
}
