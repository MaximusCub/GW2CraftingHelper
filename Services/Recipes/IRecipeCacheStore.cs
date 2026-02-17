using System.Collections.Generic;

namespace GW2CraftingHelper.Services.Recipes
{
    public class RecipeCacheStats
    {
        public int SearchHits;
        public int SearchMisses;
        public int RecipeHits;
        public int RecipeMisses;
    }

    public interface IRecipeCacheStore
    {
        IReadOnlyList<int> TryGetSearch(int outputItemId);
        RawRecipe TryGetRecipe(int recipeId);
        void PutSearch(int outputItemId, IReadOnlyList<int> recipeIds);
        void PutRecipe(int recipeId, RawRecipe recipe);
        void Flush();
        RecipeCacheStats Stats { get; }
    }
}
