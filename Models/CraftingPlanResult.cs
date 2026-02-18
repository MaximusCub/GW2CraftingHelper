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
    }
}
