using System.Collections.Generic;
using System.Threading;

namespace TaimisToolbench.Services.Recipes
{
    internal class RecipeCacheStats
    {
        private int _searchHits;
        private int _searchMisses;
        private int _recipeHits;
        private int _recipeMisses;

        public int SearchHits => Volatile.Read(ref _searchHits);

        public int SearchMisses => Volatile.Read(ref _searchMisses);

        public int RecipeHits => Volatile.Read(ref _recipeHits);

        public int RecipeMisses => Volatile.Read(ref _recipeMisses);

        public void IncrementSearchHit() => Interlocked.Increment(ref _searchHits);

        public void IncrementSearchMiss() => Interlocked.Increment(ref _searchMisses);

        public void IncrementRecipeHit() => Interlocked.Increment(ref _recipeHits);

        public void IncrementRecipeMiss() => Interlocked.Increment(ref _recipeMisses);
    }

    internal interface IRecipeCacheStore
    {
        IReadOnlyList<int> TryGetSearch(int outputItemId);

        RawRecipe TryGetRecipe(int recipeId);

        void PutSearch(int outputItemId, IReadOnlyList<int> recipeIds);

        void PutRecipe(int recipeId, RawRecipe recipe);

        void Flush(bool force = false);

        RecipeCacheStats Stats { get; }
    }
}
