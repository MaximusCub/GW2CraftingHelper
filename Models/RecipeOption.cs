using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class RecipeOption
    {
        public int RecipeId { get; set; }
        public int OutputCount { get; set; }
        public int CraftsNeeded { get; set; }

        // Expected output per craft for pricing (see RawRecipe.ExpectedOutputCount).
        // Defaults to OutputCount (a no-op) for every recipe without a
        // seeded fractional EV; only Mystic Clover-style Mystic Forge
        // recipes set this below OutputCount.
        public double ExpectedOutputCount { get; set; }
        public List<RecipeNode> Ingredients { get; set; } = new List<RecipeNode>();
        public List<string> Disciplines { get; set; } = new List<string>();
        public int MinRating { get; set; }
        public List<string> Flags { get; set; } = new List<string>();
    }
}
