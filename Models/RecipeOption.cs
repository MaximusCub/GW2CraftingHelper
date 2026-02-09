using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class RecipeOption
    {
        public int RecipeId { get; set; }
        public int OutputCount { get; set; }
        public int CraftsNeeded { get; set; }
        public List<RecipeNode> Ingredients { get; set; } = new List<RecipeNode>();
    }
}
