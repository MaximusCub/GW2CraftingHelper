using System.IO;
using System.Text;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // Quality-audit fix (B3): MysticForgeRecipeData.LoadWarnings used to be
    // collected by MysticForgeRecipeData.Load and then discarded - the sole
    // consumer, RecipeClientFactory.Create, never read them, and its own
    // load-failure catch swallowed the exception wholesale too. Both are
    // now wired to ModuleLog (Warn, "startup"), matching the idiom
    // Module.cs's own startup-diagnostics try/catches already use. Only a
    // count is logged, never the individual warning strings - one of them
    // (MysticForgeRecipeData's "invalid ingredient" warning) embeds a raw
    // item id, and a Warn-level ModuleLog line is a Log-tab-visible
    // surface the item/currency/vendor-id-internal-only invariant covers
    // (see RecipeClientFactory.Create's own doc comment). These tests
    // exercise the real RecipeClientFactory.Create production path (a
    // real MysticForgeRecipeData.Load over real JSON, a real ModuleLog
    // instance) rather than asserting on internals directly.
    public class RecipeClientFactoryTests
    {
        private class StreamMysticForgeRecipeSource : IMysticForgeRecipeSource
        {
            private readonly string _json;

            public StreamMysticForgeRecipeSource(string json)
            {
                _json = json;
            }

            public Stream Open()
            {
                return new MemoryStream(Encoding.UTF8.GetBytes(_json));
            }
        }

        private class ThrowingMysticForgeRecipeSource : IMysticForgeRecipeSource
        {
            public Stream Open()
            {
                throw new IOException("disk read failed");
            }
        }

        [Fact]
        public void Create_LoadWarningsPresent_WritesOneWarnLineWithCountAndText()
        {
            // Same "id must be negative" shape as MysticForgeRecipeDataTests'
            // own Load_PositiveId_SkipsWithWarning - one skipped recipe, one
            // LoadWarnings entry.
            string json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": 5,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 }
                        ]
                    }
                ]
            }";
            var source = new StreamMysticForgeRecipeSource(json);
            var log = new ModuleLog();

            RecipeClientFactory.Create(new InMemoryRecipeApiClient(), source, log);

            var entry = Assert.Single(log.Snapshot());
            Assert.Equal(ModuleLogLevel.Warn, entry.Level);
            Assert.Equal("startup", entry.Tag);
            Assert.Contains("1 warning(s)", entry.Message);
            // Deliberately NOT asserting the raw warning text ("must be
            // negative") appears - see this file's own class doc comment
            // on why only the count is logged.
            Assert.DoesNotContain("must be negative", entry.Message);
        }

        [Fact]
        public void Create_NoLoadWarnings_WritesNothing()
        {
            string json = @"{ ""schemaVersion"": 1, ""recipes"": [] }";
            var source = new StreamMysticForgeRecipeSource(json);
            var log = new ModuleLog();

            RecipeClientFactory.Create(new InMemoryRecipeApiClient(), source, log);

            Assert.Empty(log.Snapshot());
        }

        [Fact]
        public void Create_SourceOpenThrows_LogsWarnAndStillReturnsWorkingClient()
        {
            var source = new ThrowingMysticForgeRecipeSource();
            var log = new ModuleLog();

            var client = RecipeClientFactory.Create(new InMemoryRecipeApiClient(), source, log);

            Assert.NotNull(client);
            var entry = Assert.Single(log.Snapshot());
            Assert.Equal(ModuleLogLevel.Warn, entry.Level);
            Assert.Equal("startup", entry.Tag);
            Assert.Contains("IOException", entry.Message);
            Assert.Contains("disk read failed", entry.Message);
        }

        [Fact]
        public void Create_InvalidIngredientWarning_LoggedMessageOmitsRawItemId()
        {
            // Repo invariant (IDs internal-only): MysticForgeRecipeData's
            // "invalid ingredient" LoadWarnings entry embeds the raw
            // ingredient item id (id=24295 below) directly in its text -
            // the one LoadWarnings category that genuinely names an item
            // id rather than a synthetic internal MF recipe id. Proves
            // that id never reaches the logged Warn line even though it
            // is present, verbatim, in the underlying warning string.
            string json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 0 }
                        ]
                    }
                ]
            }";
            var source = new StreamMysticForgeRecipeSource(json);
            var log = new ModuleLog();

            RecipeClientFactory.Create(new InMemoryRecipeApiClient(), source, log);

            var entry = Assert.Single(log.Snapshot());
            Assert.Contains("1 warning(s)", entry.Message);
            Assert.DoesNotContain("24295", entry.Message);
        }
    }
}
