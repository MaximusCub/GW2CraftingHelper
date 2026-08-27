using System.IO;
using System.Text;
using TaimisToolbench.Services;
using Xunit;
// FindRepoFile comes from Helpers/RepoFileLocator.cs.
using static TaimisToolbench.Tests.Helpers.RepoFileLocator;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// RECIPE-SHEET SAVINGS -
    /// RecipeSheetItemSeedService.Load, same test shape as
    /// DailyCooldownItemServiceTests (see that file's own doc comment).
    /// </summary>
    public class RecipeSheetItemSeedServiceTests
    {
        private const string ValidEnvelopeJson = @"{
            ""schemaVersion"": 1,
            ""generatedAt"": ""2026-08-16T00:00:00Z"",
            ""source"": ""api.guildwars2.com (manual research)"",
            ""items"": [
                { ""recipeId"": 13853, ""sheetItemId"": 96274, ""sourceUrl"": ""https://api.guildwars2.com/v2/recipes/13853"", ""lastVerified"": ""2026-08-16"" },
                { ""recipeId"": 11924, ""sheetItemId"": 80124, ""sourceUrl"": ""https://api.guildwars2.com/v2/recipes/11924"", ""lastVerified"": ""2026-08-16"" }
            ]
        }";

        [Fact]
        public void Load_ValidPayload_ParsesBothEntries()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(ValidEnvelopeJson)))
            {
                var map = RecipeSheetItemSeedService.Load(stream);

                Assert.Equal(2, map.Count);
                Assert.Equal(96274, map[13853]);
                Assert.Equal(80124, map[11924]);
            }
        }

        [Fact]
        public void Load_NullStream_ReturnsEmpty()
        {
            var map = RecipeSheetItemSeedService.Load(null);

            Assert.NotNull(map);
            Assert.Empty(map);
        }

        [Fact]
        public void Load_EmptyStream_ReturnsEmpty()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("")))
            {
                var map = RecipeSheetItemSeedService.Load(stream);

                Assert.NotNull(map);
                Assert.Empty(map);
            }
        }

        [Fact]
        public void Load_MalformedJson_ReturnsEmpty_NoThrow()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("{ not valid json ")))
            {
                var map = RecipeSheetItemSeedService.Load(stream);

                Assert.NotNull(map);
                Assert.Empty(map);
            }
        }

        [Fact]
        public void Load_DuplicateRecipeId_LastWriteWins_NoThrow()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""generatedAt"": ""2026-08-16T00:00:00Z"",
                ""source"": ""test"",
                ""items"": [
                    { ""recipeId"": 100, ""sheetItemId"": 1, ""sourceUrl"": ""https://example.com/a"", ""lastVerified"": ""2026-01-01"" },
                    { ""recipeId"": 100, ""sheetItemId"": 2, ""sourceUrl"": ""https://example.com/b"", ""lastVerified"": ""2026-02-02"" }
                ]
            }";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var map = RecipeSheetItemSeedService.Load(stream);

                Assert.Single(map);
                Assert.Equal(2, map[100]);
            }
        }

        [Fact]
        public void Load_ZeroOrNegativeRecipeIdOrSheetItemId_EntrySkipped_NoThrow()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""generatedAt"": ""2026-08-16T00:00:00Z"",
                ""source"": ""test"",
                ""items"": [
                    { ""recipeId"": 0, ""sheetItemId"": 1, ""sourceUrl"": ""https://example.com/a"", ""lastVerified"": ""2026-01-01"" },
                    { ""recipeId"": -5, ""sheetItemId"": 1, ""sourceUrl"": ""https://example.com/b"", ""lastVerified"": ""2026-01-01"" },
                    { ""recipeId"": 102, ""sheetItemId"": 0, ""sourceUrl"": ""https://example.com/c"", ""lastVerified"": ""2026-01-01"" },
                    { ""recipeId"": 103, ""sheetItemId"": 200, ""sourceUrl"": ""https://example.com/d"", ""lastVerified"": ""2026-01-01"" }
                ]
            }";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var map = RecipeSheetItemSeedService.Load(stream);

                Assert.Single(map);
                Assert.True(map.ContainsKey(103));
                Assert.False(map.ContainsKey(0));
                Assert.False(map.ContainsKey(-5));
                Assert.False(map.ContainsKey(102));
            }
        }

        // --- Shipped seed file (pins the real file against silent drift) ---
        [Fact]
        public void Load_ShippedSeedFile_ParsesEntryWithCitation()
        {
            string path = FindRepoFile(Path.Combine("ref", "recipe_sheet_items.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/recipe_sheet_items.json by walking up from the test assembly's directory.");

            using (var stream = File.OpenRead(path))
            {
                var map = RecipeSheetItemSeedService.Load(stream);

                // At
                // least one real, API-verified entry must ship so the
                // calculator's own "empty map -> nothing" gate is no
                // longer permanently closed - see this seed's own "note"
                // field for the GET /v2/recipes and /v2/items verification.
                Assert.NotEmpty(map);
                Assert.True(map.ContainsKey(13853));
                Assert.Equal(96274, map[13853]);
            }
        }
    }
}
