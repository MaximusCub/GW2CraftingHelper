using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Everything needed to re-solve a generated plan locally (no network):
    /// the reduced tree plus the fetched prices, offers, and metadata from
    /// the originating generation. Enables instant per-node override
    /// recomputes in the UI.
    /// </summary>
    public class PlanSolveContext
    {
        public int TargetItemId { get; set; }
        public int Quantity { get; set; }
        public RecipeNode Tree { get; set; }
        public IReadOnlyDictionary<int, ItemPrice> Prices { get; set; }
        public IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> VendorOffers { get; set; }
        public IReadOnlyDictionary<int, ItemMetadata> Metadata { get; set; }
        public ISet<int> LearnedRecipeIds { get; set; }
        public List<UsedMaterial> UsedMaterials { get; set; }
        public PriceBasis PriceBasis { get; set; }

        /// <summary>
        /// Currency name/icon metadata snapshotted at GENERATION time, so
        /// that ResolveWithOverrides' local re-solve can reuse it on
        /// CurrencyCost rows without any network call (same reasoning as
        /// Prices/VendorOffers/Metadata above).
        /// </summary>
        public IReadOnlyDictionary<int, CurrencyMetadata> CurrencyMetadata { get; set; }

        /// <summary>
        /// The currency valuation in effect at GENERATION time, snapshotted
        /// here alongside Prices/VendorOffers/Metadata. This is intentional:
        /// ResolveWithOverrides re-solves locally and, like prices and
        /// vendor data, deliberately reuses the generation-time valuation
        /// rather than re-reading live settings - a local override toggle
        /// must not silently re-price the plan out from under the user with
        /// whatever the settings say right now. Freshly edited rates apply
        /// starting with the next full Generate.
        /// </summary>
        public CurrencyValuation CurrencyValuation { get; set; }

        /// <summary>
        /// The own-materials valuation mode in effect at GENERATION time,
        /// snapshotted here for the same reason as CurrencyValuation: a
        /// local override re-solve must keep pricing owned materials the
        /// way the original Generate did, not whatever the setting reads
        /// right now. A freshly toggled setting applies starting with the
        /// next full Generate.
        /// </summary>
        public OwnMaterialsMode OwnMaterialsMode { get; set; }

        /// <summary>
        /// Wiki-derived acquisition hints snapshotted at GENERATION time, so
        /// that ResolveWithOverrides' local re-solve can keep hint text on
        /// unpriceable nodes without any refetch (same reasoning as
        /// CurrencyMetadata above - this is a static local seed, not a live
        /// fetch, but the snapshot keeps the two code paths symmetric).
        /// </summary>
        public IReadOnlyDictionary<int, AcquisitionHint> AcquisitionHints { get; set; }

        /// <summary>
        /// Per-node owned-quantity attribution snapshotted at GENERATION
        /// time (M34-B2a #1, see ReducedTreeResult.OwnedQuantityUsedByNode
        /// and CraftingPlanPipeline.BuildOwnedQuantityUsedByNodeId) - NodeId
        /// is stable across repeat Solve() calls on the same Tree object, so
        /// ResolveWithOverrides' local re-solve reuses this as-is rather
        /// than recomputing it (reduction itself never re-runs locally -
        /// see Tree's own doc comment).
        /// </summary>
        public IReadOnlyDictionary<int, int> OwnedQuantityUsedByNodeId { get; set; }

        /// <summary>
        /// Owned amount per currency id referenced by the plan's
        /// CurrencyCosts, snapshotted at GENERATION time (M34-B2a #4 - see
        /// AccountCurrencyIndex). Cosmetic display data only; null when no
        /// wallet snapshot was available or the plan needed no currency.
        /// </summary>
        public IReadOnlyDictionary<int, int> OwnedCurrencyAmounts { get; set; }

        /// <summary>
        /// NodeIds gw2e's "Value Own Materials" force-buy pre-pass excluded
        /// from crafting at GENERATION time (M34-B2a #3 - see
        /// OwnedMaterialsForceBuyPrePass), snapshotted here so
        /// ResolveWithOverrides' local re-solve keeps applying it to every
        /// node the user hasn't manually overridden, rather than forgetting
        /// it the moment any single pill is clicked. Null in
        /// OwnMaterialsMode.Free (the pre-pass never ran).
        /// </summary>
        public ISet<int> ForceBuyOnlyNodeIds { get; set; }
    }
}
