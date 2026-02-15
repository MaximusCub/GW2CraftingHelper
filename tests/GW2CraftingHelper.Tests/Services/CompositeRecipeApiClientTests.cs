using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Records whether GetRecipeAsync was called on the primary client.
    /// </summary>
    internal class RecordingRecipeApiClient : IRecipeApiClient
    {
        public bool GetRecipeCalled { get; private set; }

        public Task<IReadOnlyList<int>> SearchByOutputAsync(int itemId, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<int>>(new List<int>());
        }

        public Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)
        {
            GetRecipeCalled = true;
            return Task.FromResult<RawRecipe>(null);
        }
    }

    public class CompositeRecipeApiClientTests
    {
        private static MysticForgeRecipeData LoadMfData(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return MysticForgeRecipeData.Load(stream);
            }
        }

        private static readonly string TwoMfRecipesJson = @"{
            ""schemaVersion"": 1,
            ""recipes"": [
                {
                    ""id"": -1,
                    ""outputItemId"": 100,
                    ""outputItemCount"": 1,
                    ""ingredients"": [
                        { ""type"": ""Item"", ""id"": 200, ""count"": 4 },
                        { ""type"": ""Item"", ""id"": 201, ""count"": 4 },
                        { ""type"": ""Item"", ""id"": 202, ""count"": 4 },
                        { ""type"": ""Item"", ""id"": 203, ""count"": 4 }
                    ]
                },
                {
                    ""id"": -2,
                    ""outputItemId"": 300,
                    ""outputItemCount"": 1,
                    ""ingredients"": [
                        { ""type"": ""Item"", ""id"": 400, ""count"": 10 }
                    ]
                }
            ]
        }";

        [Fact]
        public async Task SearchByOutput_OnlyApiResults_ReturnsThem()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(100, 10, 11);

            var composite = new CompositeRecipeApiClient(api, MysticForgeRecipeData.Empty);
            var result = await composite.SearchByOutputAsync(100, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(10, result[0]);
            Assert.Equal(11, result[1]);
        }

        [Fact]
        public async Task SearchByOutput_OnlyMfResults_ReturnsThem()
        {
            var api = new InMemoryRecipeApiClient();
            var mfData = LoadMfData(TwoMfRecipesJson);

            var composite = new CompositeRecipeApiClient(api, mfData);
            var result = await composite.SearchByOutputAsync(100, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(-1, result[0]);
        }

        [Fact]
        public async Task SearchByOutput_BothSources_MergesApiFirst()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(100, 10);

            var mfData = LoadMfData(TwoMfRecipesJson);

            var composite = new CompositeRecipeApiClient(api, mfData);
            var result = await composite.SearchByOutputAsync(100, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(10, result[0]);   // API first
            Assert.Equal(-1, result[1]);   // MF second
        }

        [Fact]
        public async Task SearchByOutput_Deduplicates()
        {
            // Edge case: same ID in both sources (shouldn't happen in practice,
            // but we guarantee dedup)
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(100, -1);  // unusual but tests dedup

            var mfData = LoadMfData(TwoMfRecipesJson);

            var composite = new CompositeRecipeApiClient(api, mfData);
            var result = await composite.SearchByOutputAsync(100, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(-1, result[0]);
        }

        [Fact]
        public async Task SearchByOutput_NoResults_ReturnsEmpty()
        {
            var api = new InMemoryRecipeApiClient();
            var mfData = LoadMfData(TwoMfRecipesJson);

            var composite = new CompositeRecipeApiClient(api, mfData);
            var result = await composite.SearchByOutputAsync(99999, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetRecipe_PositiveId_DelegatesToPrimary()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 100,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 200, Count = 5 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400
            });

            var mfData = LoadMfData(TwoMfRecipesJson);
            var composite = new CompositeRecipeApiClient(api, mfData);

            var recipe = await composite.GetRecipeAsync(10, CancellationToken.None);

            Assert.Equal(10, recipe.Id);
            Assert.Equal(100, recipe.OutputItemId);
            Assert.Contains("Weaponsmith", recipe.Disciplines);
            Assert.Equal(400, recipe.MinRating);
        }

        [Fact]
        public async Task GetRecipe_NegativeId_ReturnsFromMfData()
        {
            var api = new InMemoryRecipeApiClient();
            var mfData = LoadMfData(TwoMfRecipesJson);

            var composite = new CompositeRecipeApiClient(api, mfData);
            var recipe = await composite.GetRecipeAsync(-1, CancellationToken.None);

            Assert.Equal(-1, recipe.Id);
            Assert.Equal(100, recipe.OutputItemId);
            Assert.Single(recipe.Disciplines);
            Assert.Contains("MysticForge", recipe.Disciplines);
            Assert.Equal(0, recipe.MinRating);
            Assert.Empty(recipe.Flags);
        }

        [Fact]
        public async Task GetRecipe_NegativeId_HasCorrectIngredients()
        {
            var api = new InMemoryRecipeApiClient();
            var mfData = LoadMfData(TwoMfRecipesJson);

            var composite = new CompositeRecipeApiClient(api, mfData);
            var recipe = await composite.GetRecipeAsync(-1, CancellationToken.None);

            Assert.Equal(4, recipe.Ingredients.Count);
            Assert.Equal("Item", recipe.Ingredients[0].Type);
            Assert.Equal(200, recipe.Ingredients[0].Id);
            Assert.Equal(4, recipe.Ingredients[0].Count);
        }

        [Fact]
        public async Task GetRecipe_NegativeIdNotInMf_ReturnsNullWithoutCallingPrimary()
        {
            var recorder = new RecordingRecipeApiClient();
            var mfData = LoadMfData(TwoMfRecipesJson);

            var composite = new CompositeRecipeApiClient(recorder, mfData);
            var recipe = await composite.GetRecipeAsync(-999, CancellationToken.None);

            Assert.Null(recipe);
            Assert.False(recorder.GetRecipeCalled, "Primary should not be called for negative IDs");
        }
    }
}
