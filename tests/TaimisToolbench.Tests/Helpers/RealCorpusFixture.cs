using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Services.Recipes;

namespace TaimisToolbench.Tests.Helpers
{
    /// <summary>
    /// The SHIPPED corpus - ref/recipes_seed.json, ref/recipe_search_seed.json,
    /// ref/mystic_forge_recipes.json and ref/vendor_offers.json - loaded through
    /// the same production types Module.cs loads them with, so a test can drive
    /// the real solver over the real data rather than a hand-built miniature.
    /// <para>
    /// The recipe API client is <see cref="OfflineRecipeApiClient"/>: every
    /// lookup misses, so the corpus is the only source of recipes and a test
    /// result can never depend on a live endpoint.
    /// </para>
    /// </summary>
    internal sealed class RealCorpusFixture
    {
        private RealCorpusFixture(SeededRecipeCacheStore recipeSeed, VendorOfferDataset offers)
        {
            RecipeSeed = recipeSeed;
            OffersByOutputItem = IndexOffers(offers);
        }

        public SeededRecipeCacheStore RecipeSeed { get; }

        /// <summary>Every shipped offer, keyed by the item it produces.</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> OffersByOutputItem { get; }

        public static RealCorpusFixture Load()
        {
            var recipeSeed = new SeededRecipeCacheStore();
            using (var searchStream = OpenRepoFile("ref/recipe_search_seed.json"))
            using (var recipesStream = OpenRepoFile("ref/recipes_seed.json"))
            {
                recipeSeed.Load(searchStream, recipesStream);
            }

            using (var mfStream = OpenRepoFile("ref/mystic_forge_recipes.json"))
            {
                recipeSeed.MergeMysticForgeRecipes(MysticForgeRecipeData.Load(mfStream));
            }

            recipeSeed.FinalizeIndex();

            VendorOfferDataset offers;
            using (var offerStream = OpenRepoFile("ref/vendor_offers.json"))
            {
                offers = new VendorOfferLoader().Load(offerStream);
            }

            return new RealCorpusFixture(recipeSeed, offers);
        }

        public RecipeService NewRecipeService()
        {
            return new RecipeService(new OfflineRecipeApiClient(), 4, RecipeSeed);
        }

        public Task<RecipeNode> BuildTreeAsync(int itemId, int quantity)
        {
            return NewRecipeService().BuildTreeAsync(itemId, quantity, CancellationToken.None);
        }

        private static Stream OpenRepoFile(string relativePath)
        {
            string path = RepoFileLocator.FindRepoFile(relativePath);
            if (string.IsNullOrEmpty(path))
            {
                throw new FileNotFoundException(
                    "Could not locate " + relativePath + " by walking up from the test assembly's directory.");
            }

            return File.OpenRead(path);
        }

        private static Dictionary<int, IReadOnlyList<VendorOffer>> IndexOffers(VendorOfferDataset dataset)
        {
            var byOutput = new Dictionary<int, List<VendorOffer>>();
            foreach (var offer in dataset.Offers)
            {
                if (!byOutput.TryGetValue(offer.OutputItemId, out var list))
                {
                    list = new List<VendorOffer>();
                    byOutput[offer.OutputItemId] = list;
                }

                list.Add(offer);
            }

            var result = new Dictionary<int, IReadOnlyList<VendorOffer>>(byOutput.Count);
            foreach (var kvp in byOutput)
            {
                result[kvp.Key] = kvp.Value;
            }

            return result;
        }

        /// <summary>
        /// Answers every recipe lookup as a miss with absence UNPROVEN, so a
        /// corpus gap is never persisted as "this item has no recipe" and no
        /// test can reach the network.
        /// </summary>
        private sealed class OfflineRecipeApiClient : IRecipeApiClient
        {
            public Task<RecipeSearchResult> SearchByOutputAsync(int itemId, CancellationToken ct)
            {
                return Task.FromResult(new RecipeSearchResult(null, absenceProven: false));
            }

            public Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)
            {
                return Task.FromResult<RawRecipe>(null);
            }
        }
    }
}
