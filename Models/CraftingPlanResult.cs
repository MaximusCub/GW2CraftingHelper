using System.Collections.Generic;
using GW2CraftingHelper.Contracts;

namespace GW2CraftingHelper.Models
{
    public class CraftingPlanResult
    {
        public CraftingPlan Plan { get; set; }
        public IReadOnlyDictionary<int, ItemMetadata> ItemMetadata { get; set; }
        public List<UsedMaterial> UsedMaterials { get; set; }
        public List<RequiredDiscipline> RequiredDisciplines { get; set; }
        public List<RequiredRecipe> RequiredRecipes { get; set; }
        public CraftingTreeNode CraftingTree { get; set; }
        public List<string> DebugLog { get; set; }

        /// <summary>Price basis used for material costs in this plan.</summary>
        public PriceBasis PriceBasis { get; set; }

        /// <summary>
        /// Instant-sell unit price of the target item (buys.unit_price),
        /// null when the item has no buy orders / is untradable.
        /// </summary>
        public long? TargetUnitSellPrice { get; set; }

        /// <summary>
        /// Net coin from instant-selling the crafted quantity after the 15%
        /// Trading Post fees; null when no sell price exists.
        /// </summary>
        public long? NetSaleValue { get; set; }

        /// <summary>
        /// NetSaleValue minus the plan's total COIN cost. Non-coin currency
        /// costs are not valued and are excluded; null when no sell price.
        /// </summary>
        public long? CraftingProfit { get; set; }
    }
}
