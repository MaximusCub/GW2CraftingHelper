using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class RequiredRecipe
    {
        public int RecipeId { get; set; }

        public int OutputItemId { get; set; }

        public bool IsAutoLearned { get; set; }

        // True when this
        // recipe's unlock method is a consumable recipe sheet
        // (RecipeOption.Flags contains "LearnedFromItem" - see
        // PlanResultBuilder). Drives which wiki page the Required Recipes
        // Missing! row links to (WikiLinkBuilder.BuildRequiredRecipeUrl):
        // the recipe's own "Recipe: <name>" sheet page when true, the
        // output item's page + "#Acquisition" anchor otherwise.
        public bool IsLearnedFromItem { get; set; }

        public int MinRating { get; set; }

        public List<string> Disciplines { get; set; } = new List<string>();

        public bool? IsMissing { get; set; }
    }
}
