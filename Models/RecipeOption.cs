using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    internal class RecipeOption
    {
        public int RecipeId { get; set; }

        public int OutputCount { get; set; }

        public int CraftsNeeded { get; set; }

        // Expected output per craft attempt, used by RecipeService (and
        // kept in sync by InventoryReducer) to compute CraftsNeeded and
        // scale every ingredient quantity - see RawRecipe.ExpectedOutputCount.
        // The C# default for an un-set property is 0.0, NOT OutputCount -
        // every construction site (RecipeService, InventoryReducer's
        // CloneOption) MUST explicitly assign it, falling back to
        // OutputCount itself when the source recipe has no fractional EV
        // (a no-op ratio of 1.0). Only Mystic Clover-style Mystic Forge
        // recipes set this below OutputCount.
        public double ExpectedOutputCount { get; set; }

        public List<RecipeNode> Ingredients { get; set; } = new List<RecipeNode>();

        public List<string> Disciplines { get; set; } = new List<string>();

        public int MinRating { get; set; }

        public List<string> Flags { get; set; } = new List<string>();
    }
}
