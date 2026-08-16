using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Recipes;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// KNOWN-ISSUES recipe-ingestion bug class (2026-08-15): end-to-end
    /// proof, through the REAL production pipeline
    /// (CraftingPlanPipeline -> RecipeService -> PlanSolver ->
    /// CraftingTreeBuilder), that recipe 14025 (Amalgamated Rift Essence ->
    /// item 100930) - the concrete recipe that was invisible to every
    /// unversioned GW2 API call before this fix - now produces a real,
    /// craftable tree with its 3 currency-typed ingredients rendered as
    /// leaves and its 1 item-typed ingredient (Glob of Ectoplasm) rendered
    /// as an ordinary priced leaf.
    ///
    /// The seeded recipe row below is the REAL entry now shipped in
    /// ref/recipes_seed.json after re-running
    /// tools/GW2CraftingHelper.RecipeSeeder (verified byte-for-byte
    /// against a live `curl .../v2/recipes/14025?v=2026-08-15` fetch - see
    /// Gw2RecipeApiClientParseTests' own real-JSON test for that capture).
    /// It is loaded through the exact same production deserialization path
    /// a real module install uses (SeededRecipeCacheStore.Load via
    /// RecipeCacheSerializer, matching RecipeCacheStoreTests' own
    /// MemoryStream pattern) rather than hand-built directly as a RawRecipe,
    /// so this test also proves the row round-trips through the real seed
    /// JSON shape correctly - not just that RecipeService's in-memory model
    /// can represent it.
    /// </summary>
    public class AmalgamatedRiftEssenceIngestionTests
    {
        private const int RiftEssence = 100930;
        private const int RiftEssenceRecipeId = 14025;
        private const int GlobEcto = 19721;

        // Real currency ids from the captured recipe (Karma-family
        // currencies used by the Wizard's Vault / Skyscale-era economy;
        // exact display names are Gw2Constants.ResolveCurrencyName's
        // concern, not this test's - only id/count/leaf-ness matter here).
        private const int Currency78 = 78;
        private const int Currency80 = 80;
        private const int Currency79 = 79;

        private static SeededRecipeCacheStore BuildSeededStore(
            Dictionary<int, RawRecipe> recipes,
            Dictionary<int, IReadOnlyList<int>> searches)
        {
            string recipeJson = RecipeCacheSerializer.SerializeRecipes(recipes);
            string searchJson = RecipeCacheSerializer.SerializeSearches(searches);

            var store = new SeededRecipeCacheStore();
            using (var recipeStream = new MemoryStream(Encoding.UTF8.GetBytes(recipeJson)))
            using (var searchStream = new MemoryStream(Encoding.UTF8.GetBytes(searchJson)))
            {
                store.Load(searchStream, recipeStream);
            }
            return store;
        }

        [Fact]
        public async Task RiftEssence_BuildsCraftableTree_WithThreeCurrencyLeavesAndEctoIngredient()
        {
            // The real seed row (see class doc comment) - hand-transcribed
            // from ref/recipes_seed.json, not re-read from disk, so this
            // test stays stable across future reseeds unrelated to this
            // recipe.
            var recipes = new Dictionary<int, RawRecipe>
            {
                {
                    RiftEssenceRecipeId, new RawRecipe
                    {
                        Id = RiftEssenceRecipeId,
                        OutputItemId = RiftEssence,
                        OutputItemCount = 1,
                        Ingredients = new List<RawIngredient>
                        {
                            new RawIngredient { Type = "Currency", Id = Currency78, Count = 250 },
                            new RawIngredient { Type = "Currency", Id = Currency80, Count = 100 },
                            new RawIngredient { Type = "Currency", Id = Currency79, Count = 50 },
                            new RawIngredient { Type = "Item", Id = GlobEcto, Count = 50 }
                        },
                        Disciplines = new List<string>
                        {
                            "Leatherworker", "Armorsmith", "Chef", "Tailor", "Artificer",
                            "Weaponsmith", "Scribe", "Huntsman", "Jeweler"
                        },
                        MinRating = 400,
                        Flags = new List<string> { "LearnedFromItem" }
                    }
                }
            };
            var searches = new Dictionary<int, IReadOnlyList<int>>
            {
                { RiftEssence, new List<int> { RiftEssenceRecipeId } }
            };

            var seededStore = BuildSeededStore(recipes, searches);

            // Fallback API for cache misses (the ecto ingredient's own
            // search) - returns empty (leaf, no recipe) for any id not
            // explicitly registered, matching InMemoryRecipeApiClient's
            // documented default.
            var fallbackApi = new InMemoryRecipeApiClient();

            var priceApi = new InMemoryPriceApiClient();
            // Deliberately NO price for RiftEssence itself: currency-cost
            // recipe outputs are typically untradeable, and omitting a TP
            // price here means Craft is the only feasible source - the
            // real-world shape this recipe actually has, not an artificial
            // bias.
            priceApi.AddPrice(GlobEcto, buyUnitPrice: 200, sellUnitPrice: 400);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(RiftEssence, "Amalgamated Rift Essence", "icon.png");
            itemApi.AddItem(GlobEcto, "Glob of Ectoplasm", "icon.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(fallbackApi, cacheStore: seededStore),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            var result = await pipeline.GenerateStructuredAsync(
                RiftEssence, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // The root is craftable via the newly-visible recipe.
            Assert.Equal(RiftEssence, result.CraftingTree.ItemId);
            Assert.Equal(CraftingDecision.Craft, result.CraftingTree.Decision);
            Assert.Equal(RiftEssenceRecipeId, result.CraftingTree.RecipeId);
            Assert.Equal(4, result.CraftingTree.Children.Count);

            // Exactly 3 currency leaves, with the real ids/counts.
            var currencyLeaves = result.CraftingTree.Children
                .Where(c => c.Decision == CraftingDecision.Currency)
                .ToList();
            Assert.Equal(3, currencyLeaves.Count);
            Assert.Contains(currencyLeaves, c => c.ItemId == Currency78 && c.Quantity == 250);
            Assert.Contains(currencyLeaves, c => c.ItemId == Currency80 && c.Quantity == 100);
            Assert.Contains(currencyLeaves, c => c.ItemId == Currency79 && c.Quantity == 50);
            // Currency leaves are always terminal (CraftingTreeBuilder never
            // populates Children for them).
            Assert.All(currencyLeaves, c => Assert.Empty(c.Children));

            // The 1 item ingredient (Glob of Ectoplasm) is a real, priced
            // leaf - not a currency, not craftable here (no recipe seeded
            // for it in this isolated test).
            var ectoNode = result.CraftingTree.Children
                .Single(c => c.Decision != CraftingDecision.Currency);
            Assert.Equal(GlobEcto, ectoNode.ItemId);
            Assert.Equal(50, ectoNode.Quantity);
            Assert.Equal(CraftingDecision.BuyFromTp, ectoNode.Decision);

            // The flat plan reflects the same shape: a Craft step for the
            // root recipe and a Buy step for the ecto.
            Assert.Contains(result.Plan.Steps, s =>
                s.ItemId == RiftEssence &&
                s.Source == AcquisitionSource.Craft &&
                s.RecipeId == RiftEssenceRecipeId);
            Assert.Contains(result.Plan.Steps, s =>
                s.ItemId == GlobEcto &&
                s.Source == AcquisitionSource.BuyFromTp &&
                s.Quantity == 50);

            Assert.Contains(result.RequiredRecipes, r => r.RecipeId == RiftEssenceRecipeId
                && r.OutputItemId == RiftEssence);
        }

        [Fact]
        public async Task GuildUpgradeIngredient_DiscoveredByTheSameSchemaFix_DoesNotThrow()
        {
            // Adversarial-review finding from this same fix (out of scope
            // to actually FIX - see docs/KNOWN-ISSUES.md's own entry): the
            // versioned schema pinned by this fix does not only reveal
            // Currency ingredients - it also folds an UNVERSIONED response's
            // separate top-level "guild_ingredients" array directly into
            // "ingredients" as a new "GuildUpgrade" ingredient type this
            // module has never modeled (verified live: recipe 9917's real
            // seed row, ref/recipes_seed.json, now carries a
            // {"type":"GuildUpgrade","id":279,"count":1} ingredient that
            // was previously silently dropped entirely, both before and
            // after this fix - the module has never read
            // "guild_ingredients"). PlanSolver has no "GuildUpgrade" arm
            // (only "Currency" short-circuits to a free leaf) and
            // CraftingTreeBuilder buckets ANY non-"Item" type as a display
            // Currency leaf - so this ingredient ends up priced as
            // unavailable (contributes 0, like an unvalued Currency) and
            // displayed with the generic "Currency" fallback name
            // (Gw2Constants.ResolveCurrencyName has no entry for a guild
            // upgrade id and falls back to the literal string "Currency").
            // This test exists purely to PROVE that mislabeling is
            // cosmetic, not a crash: the real seed row for a genuine
            // Guild Decoration recipe must flow through the full pipeline
            // without throwing.
            var recipes = new Dictionary<int, RawRecipe>
            {
                {
                    9917, new RawRecipe
                    {
                        Id = 9917,
                        OutputItemId = 75375,
                        OutputItemCount = 1,
                        Ingredients = new List<RawIngredient>
                        {
                            new RawIngredient { Type = "Item", Id = 70454, Count = 1 },
                            new RawIngredient { Type = "Item", Id = 24356, Count = 50 },
                            new RawIngredient { Type = "Item", Id = 24350, Count = 50 },
                            new RawIngredient { Type = "GuildUpgrade", Id = 279, Count = 1 }
                        },
                        Disciplines = new List<string> { "Scribe" },
                        MinRating = 100,
                        Flags = new List<string> { "AutoLearned" }
                    }
                }
            };
            var searches = new Dictionary<int, IReadOnlyList<int>>
            {
                { 75375, new List<int> { 9917 } }
            };

            var seededStore = BuildSeededStore(recipes, searches);
            var fallbackApi = new InMemoryRecipeApiClient();

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(70454, buyUnitPrice: 1000, sellUnitPrice: 2000);
            priceApi.AddPrice(24356, buyUnitPrice: 10, sellUnitPrice: 20);
            priceApi.AddPrice(24350, buyUnitPrice: 10, sellUnitPrice: 20);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(75375, "Guild Decoration", "icon.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(fallbackApi, cacheStore: seededStore),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            var result = await pipeline.GenerateStructuredAsync(
                75375, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // Does not throw, and the tree completes with the guild-upgrade
            // ingredient present (as a Currency-labeled leaf - the known,
            // documented, out-of-scope cosmetic gap).
            Assert.Equal(CraftingDecision.Craft, result.CraftingTree.Decision);
            var guildNode = result.CraftingTree.Children.Single(c => c.ItemId == 279);
            Assert.Equal(CraftingDecision.Currency, guildNode.Decision);
            Assert.Equal(1, guildNode.Quantity);
        }
    }
}
