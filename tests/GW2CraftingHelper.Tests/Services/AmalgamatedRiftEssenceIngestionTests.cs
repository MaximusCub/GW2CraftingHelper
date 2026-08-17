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
    /// KNOWN-ISSUES recipe-ingestion bug class: end-to-end
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
    /// against a live `curl .../v2/recipes/14025?v=` fetch - see
    /// Gw2RecipeApiClientParseTests' own real-JSON test for that capture).
    /// It is loaded through the exact same production deserialization path
    /// a real module install uses (SeededRecipeCacheStore.Load via
    /// RecipeCacheSerializer, matching RecipeCacheStoreTests' own
    /// MemoryStream pattern) rather than hand-built directly as a RawRecipe,
    /// so this test also proves the row round-trips through the real seed
    /// JSON shape correctly - not just that RecipeService's in-memory model
    /// can represent it.
    ///
    /// Also covers the guildupgrade-ingredients fix (docs/KNOWN-ISSUES.md):
    /// this same schema-versioning fix incidentally revealed a second,
    /// previously-unmodeled ingredient type ("GuildUpgrade", a Guild
    /// Decoration recipe's claimed-guild-hall-upgrade requirement) through
    /// the exact same production pipeline - see the tests below.
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
        public async Task GuildUpgradeIngredient_NeverPricedAsItemOrCurrency_DisplaysAsUnresolvedGuildUpgrade()
        {
            // guildupgrade-ingredients fix (docs/KNOWN-ISSUES.md): the
            // versioned schema pinned by the recipe-ingestion fix does not
            // only reveal Currency ingredients - it also folds an
            // UNVERSIONED response's separate top-level "guild_ingredients"
            // array directly into "ingredients" as a "GuildUpgrade"
            // ingredient type (verified live: recipe 9917's real seed row,
            // ref/recipes_seed.json, carries a
            // {"type":"GuildUpgrade","id":279,"count":1} ingredient).
            // Before this fix, PlanSolver.Evaluate's ingredient loop only
            // special-cased "Currency", so a GuildUpgrade ingredient fell
            // through to the item-pricing path and was priced as whatever
            // TP item happened to share its numeric id - a real mis-costing
            // bug, not just a cosmetic display gap. A TP price is
            // deliberately seeded here for id 279 (the exact "shares that
            // numeric id" collision) to prove it is no longer consulted.
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

            // buyUnitPrice/sellUnitPrice set equal on each line (rather
            // than distinct values) so the effective per-unit cost under
            // PriceBasis.InstantBuy is unambiguous regardless of which of
            // the two the solver's GetUnitPrice reads.
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(70454, buyUnitPrice: 1000, sellUnitPrice: 1000);
            priceApi.AddPrice(24356, buyUnitPrice: 10, sellUnitPrice: 10);
            priceApi.AddPrice(24350, buyUnitPrice: 10, sellUnitPrice: 10);
            // Deliberately mispriced "collision" - id 279 is a guild
            // upgrade id, not the id of this absurdly expensive TP item.
            // The fix must never look this up.
            priceApi.AddPrice(279, buyUnitPrice: 999999, sellUnitPrice: 999999);

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

            // The recipe is still fully craftable (the "hasComponents"
            // guarantee, same as an all-unvalued-currency recipe) - a
            // GuildUpgrade ingredient does not disqualify the recipe.
            Assert.Equal(CraftingDecision.Craft, result.CraftingTree.Decision);
            var guildNode = result.CraftingTree.Children.Single(c => c.ItemId == 279);

            // Distinct decision from Currency (see CraftingDecision.
            // GuildUpgrade's own doc comment), a generic ID-free display
            // name (repo invariant: IDs are never displayed), and no
            // priced cost cell at all - never Currency's literal "Currency"
            // fallback name.
            Assert.Equal(CraftingDecision.GuildUpgrade, guildNode.Decision);
            Assert.Equal("Guild upgrade (unresolved)", guildNode.Name);
            Assert.Equal(1, guildNode.Quantity);
            Assert.Null(guildNode.SubtreeCost);

            // The root's REAL cost is exactly the 3 item ingredients' TP
            // price sum (1000 + 500 + 500 = 2000) - the bogus 999999-copper
            // "price" for id 279 must never leak in.
            Assert.Equal(2000, result.CraftingTree.SubtreeCost);
            Assert.Contains(result.Plan.Steps, s =>
                s.ItemId == 75375 && s.Source == AcquisitionSource.Craft && s.TotalCost == 2000);
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 279);
            Assert.Empty(result.Plan.CurrencyCosts);
        }

        [Fact]
        public async Task GuildUpgradeIngredient_RealRecipe12002Shape_CraftsAtItemIngredientCostOnly()
        {
            // The exact real seed row (ref/recipes_seed.json, verified live
            // via api.guildwars2.com/v2/recipes/12002?v=): a Guild
            // Decoration recipe needing 1x a real crafting item plus 5x
            // guild upgrade id 829 - the audit's own reference shape for
            // this fix.
            var recipes = new Dictionary<int, RawRecipe>
            {
                {
                    12002, new RawRecipe
                    {
                        Id = 12002,
                        OutputItemId = 80471,
                        OutputItemCount = 1,
                        Ingredients = new List<RawIngredient>
                        {
                            new RawIngredient { Type = "Item", Id = 70489, Count = 1 },
                            new RawIngredient { Type = "GuildUpgrade", Id = 829, Count = 5 }
                        },
                        Disciplines = new List<string> { "Scribe" },
                        MinRating = 350,
                        Flags = new List<string> { "AutoLearned" }
                    }
                }
            };
            var searches = new Dictionary<int, IReadOnlyList<int>>
            {
                { 80471, new List<int> { 12002 } }
            };

            var seededStore = BuildSeededStore(recipes, searches);
            var fallbackApi = new InMemoryRecipeApiClient();

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(70489, buyUnitPrice: 250, sellUnitPrice: 250);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(80471, "Guild Decoration", "icon.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(fallbackApi, cacheStore: seededStore),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            var result = await pipeline.GenerateStructuredAsync(
                80471, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(CraftingDecision.Craft, result.CraftingTree.Decision);
            Assert.Equal(12002, result.CraftingTree.RecipeId);
            Assert.Equal(2, result.CraftingTree.Children.Count);

            var itemChild = result.CraftingTree.Children.Single(c => c.ItemId == 70489);
            Assert.Equal(CraftingDecision.BuyFromTp, itemChild.Decision);
            Assert.Equal(1, itemChild.Quantity);

            var guildChild = result.CraftingTree.Children.Single(c => c.ItemId == 829);
            Assert.Equal(CraftingDecision.GuildUpgrade, guildChild.Decision);
            Assert.Equal(5, guildChild.Quantity);
            Assert.Equal("Guild upgrade (unresolved)", guildChild.Name);
            Assert.Null(guildChild.SubtreeCost);
            Assert.Empty(guildChild.Children);

            // Root cost is the item ingredient's price alone.
            Assert.Equal(250, result.CraftingTree.SubtreeCost);
            Assert.Empty(result.Plan.CurrencyCosts);
        }
    }
}
