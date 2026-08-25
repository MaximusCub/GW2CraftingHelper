using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    public class RawIngredient
    {
        public string Type { get; set; }

        public int Id { get; set; }

        public int Count { get; set; }

        // Achievement-bit ingredient dedup (KNOWN-ISSUES #26, gw2e
        // parity): mirrors gw2efficiency's own achievement_id/achievement_bit
        // ingredient fields exactly (docs/research/m37-r3-achievement-dedup.md
        // Section 1.0/4.1). Null for every existing seed row (JSON-absent =
        // ordinary ingredient, fully backward compatible). Only
        // AchievementBit drives the dedup mechanism (AchievementBitDedupPrePass) -
        // AchievementId is carried alongside purely because it is present on
        // the same upstream ingredient objects and costs nothing extra to
        // keep; it is never read by the dedup logic itself.
        public int? AchievementId { get; set; }

        public int? AchievementBit { get; set; }
    }

    public class RawRecipe
    {
        public int Id { get; set; }

        public int OutputItemId { get; set; }

        public int OutputItemCount { get; set; }

        // Optional fractional expected-output count (Mystic Clover-style
        // recipes, gw2e's output_item_count=0.31 - see r2 report). Null
        // means "no EV override": RecipeService defaults this to
        // OutputItemCount, making it a no-op for every ordinary recipe.
        // Kept separate from OutputItemCount (which stays an integer used
        // for ceil-based crafts-needed/quantity propagation) so tree
        // shape/quantity math is unaffected; only PlanSolver's craft-cost
        // pricing reads this field.
        public double? ExpectedOutputCount { get; set; }

        public List<RawIngredient> Ingredients { get; set; } = new List<RawIngredient>();

        public List<string> Disciplines { get; set; } = new List<string>();

        public int MinRating { get; set; }

        public List<string> Flags { get; set; } = new List<string>();

        // Recipe-level achievement_id, mirroring
        // gw2efficiency's own custom-recipes field (marks the RECIPE itself
        // as achievement-gated - e.g. a collection reward). Informational
        // only: NOT read by AchievementBitDedupPrePass, which keys purely on
        // ingredient-level RawIngredient.AchievementBit. Populated
        // for the achievement-recipe seed additions, so a future task
        // surfacing "this recipe is achievement-gated" does not need a
        // second schema migration (m37-r3-achievement-dedup.md Section 6,
        // open question 5).
        public int? AchievementId { get; set; }
    }

    public interface IRecipeApiClient
    {
        Task<IReadOnlyList<int>> SearchByOutputAsync(int itemId, CancellationToken ct);

        Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct);
    }
}
