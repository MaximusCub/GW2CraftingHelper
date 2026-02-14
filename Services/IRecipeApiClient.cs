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
