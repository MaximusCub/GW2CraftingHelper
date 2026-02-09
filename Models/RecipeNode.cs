using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class RecipeNode
    {
        public int Id { get; set; }
        public string IngredientType { get; set; }
        public int Quantity { get; set; }
        public List<RecipeOption> Recipes { get; set; } = new List<RecipeOption>();
        public bool IsLeaf => Recipes.Count == 0;
    }
}
