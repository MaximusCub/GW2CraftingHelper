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
    }

    public interface IRecipeApiClient
    {
        Task<IReadOnlyList<int>> SearchByOutputAsync(int itemId, CancellationToken ct);
        Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct);
    }
}
