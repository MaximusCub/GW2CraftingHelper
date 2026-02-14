using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class ReducedTreeResult
    {
        public RecipeNode ReducedTree { get; set; }
        public List<UsedMaterial> UsedMaterials { get; set; } = new List<UsedMaterial>();
    }
}
