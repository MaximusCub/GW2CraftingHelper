using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class RequiredRecipe
    {
        public int RecipeId { get; set; }
        public int OutputItemId { get; set; }
        public bool IsAutoLearned { get; set; }
        public int MinRating { get; set; }
        public List<string> Disciplines { get; set; } = new List<string>();
        public bool? IsMissing { get; set; }
    }
}
