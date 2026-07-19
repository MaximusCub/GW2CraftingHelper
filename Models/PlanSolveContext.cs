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
    }
}
