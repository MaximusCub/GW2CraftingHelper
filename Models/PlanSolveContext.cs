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
    }
}
