using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class MysticForgeIntegrationTests
    {
        // Verified GW2 API item IDs
        private const int GiftOfMagicId = 19673;
        private const int GiftOfMightId = 19672;
        private const int GiftOfFortuneId = 19626;
        private const int MysticSalvageKitId = 23045;

        // Gift of Magic ingredients (T6 fine mats)
        private const int VialOfPowerfulBloodId = 24295;
        private const int PowerfulVenomSacId = 24283;
        private const int ElaborateTotemId = 24300;
        private const int PileOfCrystallineDustId = 24277;

        // Gift of Might ingredients (T6 trophy mats)
        private const int ViciousFangId = 24357;
        private const int ArmoredScaleId = 24289;
        private const int ViciousClawId = 24351;
        private const int AncientBoneId = 24358;

        // Gift of Fortune ingredients
        private const int MysticCloverId = 19675;
        private const int GlobOfEctoplasmId = 19721;

        // Mystic Salvage Kit ingredients
        private const int FineSalvageKitId = 23041;
        private const int JourneymansSalvageKitId = 23042;
        private const int MastersSalvageKitId = 23043;
        private const int MysticForgeStoneId = 19983;

        private static readonly string SeedJson = @"{
            ""schemaVersion"": 1,
            ""recipes"": [
                {
                    ""id"": -1,
                    ""outputItemId"": 19673,
                    ""outputItemCount"": 1,
                    ""ingredients"": [
                        { ""type"": ""Item"", ""id"": 24295, ""count"": 250 },
                        { ""type"": ""Item"", ""id"": 24283, ""count"": 250 },
                        { ""type"": ""Item"", ""id"": 24300, ""count"": 250 },
                        { ""type"": ""Item"", ""id"": 24277, ""count"": 250 }
                    ]
                },
                {
                    ""id"": -2,
                    ""outputItemId"": 19672,
                    ""outputItemCount"": 1,
                    ""ingredients"": [
                        { ""type"": ""Item"", ""id"": 24357, ""count"": 250 },
                        { ""type"": ""Item"", ""id"": 24289, ""count"": 250 },
                        { ""type"": ""Item"", ""id"": 24351, ""count"": 250 },
                        { ""type"": ""Item"", ""id"": 24358, ""count"": 250 }
                    ]
                },
                {
                    ""id"": -3,
                    ""outputItemId"": 19626,
                    ""outputItemCount"": 1,
                    ""ingredients"": [
                        { ""type"": ""Item"", ""id"": 19675, ""count"": 77 },
                        { ""type"": ""Item"", ""id"": 19721, ""count"": 250 },
                        { ""type"": ""Item"", ""id"": 19673, ""count"": 1 },
                        { ""type"": ""Item"", ""id"": 19672, ""count"": 1 }
                    ]
                },
                {
                    ""id"": -4,
                    ""outputItemId"": 23045,
                    ""outputItemCount"": 1,
                    ""ingredients"": [
                        { ""type"": ""Item"", ""id"": 23041, ""count"": 1 },
                        { ""type"": ""Item"", ""id"": 23042, ""count"": 1 },
                        { ""type"": ""Item"", ""id"": 23043, ""count"": 1 },
                        { ""type"": ""Item"", ""id"": 19983, ""count"": 250 }
                    ]
                }
            ]
        }";

        private static MysticForgeRecipeData LoadMfData()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(SeedJson)))
            {
                return MysticForgeRecipeData.Load(stream);
            }
        }

        [Fact]
        public async Task SingleMfRecipe_SolverChoosesCraftWhenCheaper()
        {
            // Gift of Magic: 250 each of 4 T6 mats
            // Set TP buy-instantly price (sellUnitPrice → BuyInstant) very high
            // so crafting is cheaper
            var api = new InMemoryRecipeApiClient();
            var mfData = LoadMfData();
            var composite = new CompositeRecipeApiClient(api, mfData);

            // Note: BuyInstant = sellUnitPrice (GW2 API: instant-buy = sell listing)
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(GiftOfMagicId, buyUnitPrice: 100, sellUnitPrice: 2000000);
            priceApi.AddPrice(VialOfPowerfulBloodId, buyUnitPrice: 100, sellUnitPrice: 300);
            priceApi.AddPrice(PowerfulVenomSacId, buyUnitPrice: 100, sellUnitPrice: 200);
            priceApi.AddPrice(ElaborateTotemId, buyUnitPrice: 100, sellUnitPrice: 250);
            priceApi.AddPrice(PileOfCrystallineDustId, buyUnitPrice: 100, sellUnitPrice: 350);

            var recipeService = new RecipeService(composite);
            var tree = await recipeService.BuildTreeAsync(GiftOfMagicId, 1, CancellationToken.None);

            // Tree should have one MF recipe option
            Assert.False(tree.IsLeaf);
            Assert.Single(tree.Recipes);
            Assert.Equal(-1, tree.Recipes[0].RecipeId);
            Assert.Contains("MysticForge", tree.Recipes[0].Disciplines);
            Assert.Equal(4, tree.Recipes[0].Ingredients.Count);

            // Craft cost: 250*(300+200+250+350) = 275000, cheaper than buying (2000000)
            var prices = await new TradingPostService(priceApi)
                .GetPricesAsync(new HashSet<int>
                {
                    GiftOfMagicId, VialOfPowerfulBloodId,
                    PowerfulVenomSacId, ElaborateTotemId, PileOfCrystallineDustId
                }, CancellationToken.None);

            var solver = new PlanSolver();
            var plan = solver.Solve(tree, prices);

            // Solver should choose Craft
            var craftStep = plan.Steps.FirstOrDefault(s => s.ItemId == GiftOfMagicId);
            Assert.NotNull(craftStep);
            Assert.Equal(AcquisitionSource.Craft, craftStep.Source);
            Assert.Equal(-1, craftStep.RecipeId);

            // Verify total = sum of ingredient BuyInstant costs
            long expectedCost = 250L * 300 + 250L * 200 + 250L * 250 + 250L * 350;
            Assert.Equal(expectedCost, plan.TotalCoinCost);
        }

        [Fact]
        public async Task SingleMfRecipe_SolverChoosesBuyWhenCheaper()
        {
            // Gift of Magic BuyInstant (sellUnitPrice) is cheap,
            // ingredient BuyInstants are expensive → buy is cheaper than craft
            var api = new InMemoryRecipeApiClient();
            var mfData = LoadMfData();
            var composite = new CompositeRecipeApiClient(api, mfData);

            // BuyInstant = sellUnitPrice
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(GiftOfMagicId, buyUnitPrice: 50, sellUnitPrice: 100);
            priceApi.AddPrice(VialOfPowerfulBloodId, buyUnitPrice: 100, sellUnitPrice: 500);
            priceApi.AddPrice(PowerfulVenomSacId, buyUnitPrice: 100, sellUnitPrice: 400);
            priceApi.AddPrice(ElaborateTotemId, buyUnitPrice: 100, sellUnitPrice: 450);
            priceApi.AddPrice(PileOfCrystallineDustId, buyUnitPrice: 100, sellUnitPrice: 600);

            var recipeService = new RecipeService(composite);
            var tree = await recipeService.BuildTreeAsync(GiftOfMagicId, 1, CancellationToken.None);

            var prices = await new TradingPostService(priceApi)
                .GetPricesAsync(new HashSet<int>
                {
                    GiftOfMagicId, VialOfPowerfulBloodId,
                    PowerfulVenomSacId, ElaborateTotemId, PileOfCrystallineDustId
                }, CancellationToken.None);

            var solver = new PlanSolver();
            var plan = solver.Solve(tree, prices);

            // BuyInstant for Gift of Magic = 100
            // Craft cost = 250*(500+400+450+600) = 487500
            // 100 < 487500 → choose BuyFromTp
            var buyStep = plan.Steps.FirstOrDefault(s => s.ItemId == GiftOfMagicId);
            Assert.NotNull(buyStep);
            Assert.Equal(AcquisitionSource.BuyFromTp, buyStep.Source);
            Assert.Equal(100, plan.TotalCoinCost);
        }

        [Fact]
        public async Task ChainedMfRecipes_GiftOfFortune_ExpandsFullTree()
        {
            // Gift of Fortune (-3) references:
            //   77 Mystic Clover (leaf)
            //   250 Glob of Ectoplasm (leaf)
            //   1 Gift of Magic (-1) -> 4 T6 fine mats
            //   1 Gift of Might (-2) -> 4 T6 trophy mats
            var api = new InMemoryRecipeApiClient();
            var mfData = LoadMfData();
            var composite = new CompositeRecipeApiClient(api, mfData);

            var recipeService = new RecipeService(composite);
            var tree = await recipeService.BuildTreeAsync(GiftOfFortuneId, 1, CancellationToken.None);

            // Root: Gift of Fortune
            Assert.False(tree.IsLeaf);
            Assert.Single(tree.Recipes);

            var fortuneRecipe = tree.Recipes[0];
            Assert.Equal(-3, fortuneRecipe.RecipeId);
            Assert.Contains("MysticForge", fortuneRecipe.Disciplines);
            Assert.Equal(4, fortuneRecipe.Ingredients.Count);

            // Ingredient 0: Mystic Clover (77, leaf — no MF recipe for it in seed)
            var cloverNode = fortuneRecipe.Ingredients[0];
            Assert.Equal(MysticCloverId, cloverNode.Id);
            Assert.Equal(77, cloverNode.Quantity);
            Assert.True(cloverNode.IsLeaf);

            // Ingredient 1: Glob of Ectoplasm (250, leaf)
            var ectoNode = fortuneRecipe.Ingredients[1];
            Assert.Equal(GlobOfEctoplasmId, ectoNode.Id);
            Assert.Equal(250, ectoNode.Quantity);
            Assert.True(ectoNode.IsLeaf);

            // Ingredient 2: Gift of Magic (1, has sub-recipe -1)
            var magicNode = fortuneRecipe.Ingredients[2];
            Assert.Equal(GiftOfMagicId, magicNode.Id);
            Assert.Equal(1, magicNode.Quantity);
            Assert.False(magicNode.IsLeaf);
            Assert.Single(magicNode.Recipes);
            Assert.Equal(-1, magicNode.Recipes[0].RecipeId);
            Assert.Equal(4, magicNode.Recipes[0].Ingredients.Count);

            // Verify T6 fine mat leaves under Gift of Magic
            var magicIngredients = magicNode.Recipes[0].Ingredients;
            Assert.Equal(VialOfPowerfulBloodId, magicIngredients[0].Id);
            Assert.Equal(250, magicIngredients[0].Quantity);
            Assert.True(magicIngredients[0].IsLeaf);

            // Ingredient 3: Gift of Might (1, has sub-recipe -2)
            var mightNode = fortuneRecipe.Ingredients[3];
            Assert.Equal(GiftOfMightId, mightNode.Id);
            Assert.Equal(1, mightNode.Quantity);
            Assert.False(mightNode.IsLeaf);
            Assert.Single(mightNode.Recipes);
            Assert.Equal(-2, mightNode.Recipes[0].RecipeId);
            Assert.Equal(4, mightNode.Recipes[0].Ingredients.Count);

            // Verify T6 trophy mat leaves under Gift of Might
            var mightIngredients = mightNode.Recipes[0].Ingredients;
            Assert.Equal(ViciousFangId, mightIngredients[0].Id);
            Assert.Equal(250, mightIngredients[0].Quantity);
            Assert.True(mightIngredients[0].IsLeaf);
        }

        [Fact]
        public async Task ChainedMfRecipes_SolverComputesCorrectTotal()
        {
            // Gift of Fortune → Gift of Magic + Gift of Might + Ecto + Clover
            // All leaf ingredients priced, gifts are expensive to buy outright
            var api = new InMemoryRecipeApiClient();
            var mfData = LoadMfData();
            var composite = new CompositeRecipeApiClient(api, mfData);

            // BuyInstant = sellUnitPrice (GW2 API convention)
            var priceApi = new InMemoryPriceApiClient();

            // Gift of Fortune: very expensive to buy (force craft)
            priceApi.AddPrice(GiftOfFortuneId, buyUnitPrice: 100, sellUnitPrice: 9999999);

            // Gift of Magic/Might: very expensive to buy (force craft)
            priceApi.AddPrice(GiftOfMagicId, buyUnitPrice: 100, sellUnitPrice: 9999999);
            priceApi.AddPrice(GiftOfMightId, buyUnitPrice: 100, sellUnitPrice: 9999999);

            // Leaf ingredients: BuyInstant (sellUnitPrice) is the cost to buy
            priceApi.AddPrice(MysticCloverId, buyUnitPrice: 100, sellUnitPrice: 500);
            priceApi.AddPrice(GlobOfEctoplasmId, buyUnitPrice: 100, sellUnitPrice: 200);

            // T6 fine mats (Gift of Magic ingredients)
            priceApi.AddPrice(VialOfPowerfulBloodId, buyUnitPrice: 100, sellUnitPrice: 300);
            priceApi.AddPrice(PowerfulVenomSacId, buyUnitPrice: 100, sellUnitPrice: 200);
            priceApi.AddPrice(ElaborateTotemId, buyUnitPrice: 100, sellUnitPrice: 250);
            priceApi.AddPrice(PileOfCrystallineDustId, buyUnitPrice: 100, sellUnitPrice: 350);

            // T6 trophy mats (Gift of Might ingredients)
            priceApi.AddPrice(ViciousFangId, buyUnitPrice: 100, sellUnitPrice: 400);
            priceApi.AddPrice(ArmoredScaleId, buyUnitPrice: 100, sellUnitPrice: 300);
            priceApi.AddPrice(ViciousClawId, buyUnitPrice: 100, sellUnitPrice: 350);
            priceApi.AddPrice(AncientBoneId, buyUnitPrice: 100, sellUnitPrice: 250);

            var allItemIds = new HashSet<int>
            {
                GiftOfFortuneId, GiftOfMagicId, GiftOfMightId,
                MysticCloverId, GlobOfEctoplasmId,
                VialOfPowerfulBloodId, PowerfulVenomSacId, ElaborateTotemId, PileOfCrystallineDustId,
                ViciousFangId, ArmoredScaleId, ViciousClawId, AncientBoneId
            };

            var recipeService = new RecipeService(composite);
            var tree = await recipeService.BuildTreeAsync(GiftOfFortuneId, 1, CancellationToken.None);

            var prices = await new TradingPostService(priceApi)
                .GetPricesAsync(allItemIds, CancellationToken.None);

            var solver = new PlanSolver();
            var plan = solver.Solve(tree, prices);

            // Gift of Fortune should be crafted (not bought)
            var fortuneStep = plan.Steps.FirstOrDefault(s =>
                s.ItemId == GiftOfFortuneId && s.Source == AcquisitionSource.Craft);
            Assert.NotNull(fortuneStep);
            Assert.Equal(-3, fortuneStep.RecipeId);

            // Gift of Magic should be crafted
            var magicStep = plan.Steps.FirstOrDefault(s =>
                s.ItemId == GiftOfMagicId && s.Source == AcquisitionSource.Craft);
            Assert.NotNull(magicStep);
            Assert.Equal(-1, magicStep.RecipeId);

            // Gift of Might should be crafted
            var mightStep = plan.Steps.FirstOrDefault(s =>
                s.ItemId == GiftOfMightId && s.Source == AcquisitionSource.Craft);
            Assert.NotNull(mightStep);
            Assert.Equal(-2, mightStep.RecipeId);

            // Calculate expected total from leaf BuyInstant (sellUnitPrice) costs
            long cloverCost = 77L * 500;          // 38500
            long ectoCost = 250L * 200;            // 50000
            long magicCost = 250L * (300 + 200 + 250 + 350);  // 275000
            long mightCost = 250L * (400 + 300 + 350 + 250);  // 325000
            long expectedTotal = cloverCost + ectoCost + magicCost + mightCost;  // 688500

            Assert.Equal(expectedTotal, plan.TotalCoinCost);
        }

        [Fact]
        public async Task MysticForgeDiscipline_AppearsInRequiredDisciplines()
        {
            // Build a plan that crafts a MF recipe, then check RequiredDisciplines
            var api = new InMemoryRecipeApiClient();
            var mfData = LoadMfData();
            var composite = new CompositeRecipeApiClient(api, mfData);

            // BuyInstant = sellUnitPrice; force craft by making kit expensive to buy
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(MysticSalvageKitId, buyUnitPrice: 100, sellUnitPrice: 9999999);
            priceApi.AddPrice(FineSalvageKitId, buyUnitPrice: 100, sellUnitPrice: 200);
            priceApi.AddPrice(JourneymansSalvageKitId, buyUnitPrice: 100, sellUnitPrice: 300);
            priceApi.AddPrice(MastersSalvageKitId, buyUnitPrice: 100, sellUnitPrice: 400);
            priceApi.AddPrice(MysticForgeStoneId, buyUnitPrice: 10, sellUnitPrice: 20);

            var allItemIds = new HashSet<int>
            {
                MysticSalvageKitId, FineSalvageKitId, JourneymansSalvageKitId,
                MastersSalvageKitId, MysticForgeStoneId
            };

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(MysticSalvageKitId, "Mystic Salvage Kit", "icon.png");
            itemApi.AddItem(FineSalvageKitId, "Fine Salvage Kit", "icon.png");
            itemApi.AddItem(JourneymansSalvageKitId, "Journeyman's Salvage Kit", "icon.png");
            itemApi.AddItem(MastersSalvageKitId, "Master's Salvage Kit", "icon.png");
            itemApi.AddItem(MysticForgeStoneId, "Mystic Forge Stone", "icon.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(composite),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            var result = await pipeline.GenerateStructuredAsync(
                MysticSalvageKitId, 1, null, CancellationToken.None);

            // MysticForge discipline should be required
            Assert.Contains(result.RequiredDisciplines,
                d => d.Discipline == "MysticForge");

            // The MF discipline should have MinRating = 0
            var mfDisc = result.RequiredDisciplines.First(d => d.Discipline == "MysticForge");
            Assert.Equal(0, mfDisc.MinRating);
        }

        [Fact]
        public async Task MixedTree_ApiAndMfRecipes_BothAvailable()
        {
            // Item 100 has both an API recipe (10) and an MF recipe (-1)
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(100, 10);
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

            // MF data also has a recipe for item 100
            var mfJson = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 100,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 300, ""count"": 3 },
                            { ""type"": ""Item"", ""id"": 301, ""count"": 3 },
                            { ""type"": ""Item"", ""id"": 302, ""count"": 3 },
                            { ""type"": ""Item"", ""id"": 303, ""count"": 3 }
                        ]
                    }
                ]
            }";

            MysticForgeRecipeData mfData;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(mfJson)))
            {
                mfData = MysticForgeRecipeData.Load(stream);
            }

            var composite = new CompositeRecipeApiClient(api, mfData);
            var recipeService = new RecipeService(composite);
            var tree = await recipeService.BuildTreeAsync(100, 1, CancellationToken.None);

            // Both recipes should be present
            Assert.Equal(2, tree.Recipes.Count);

            // API recipe first (by merge order)
            Assert.Equal(10, tree.Recipes[0].RecipeId);
            Assert.Contains("Weaponsmith", tree.Recipes[0].Disciplines);

            // MF recipe second
            Assert.Equal(-1, tree.Recipes[1].RecipeId);
            Assert.Contains("MysticForge", tree.Recipes[1].Disciplines);
        }

        [Fact]
        public async Task MysticSalvageKit_FullPipeline_ProducesValidResult()
        {
            var api = new InMemoryRecipeApiClient();
            var mfData = LoadMfData();
            var composite = new CompositeRecipeApiClient(api, mfData);

            // BuyInstant = sellUnitPrice
            var priceApi = new InMemoryPriceApiClient();
            // Mystic Salvage Kit: very expensive to buy (force craft)
            priceApi.AddPrice(MysticSalvageKitId, buyUnitPrice: 100, sellUnitPrice: 9999999);
            // Ingredients: BuyInstant is sellUnitPrice
            priceApi.AddPrice(FineSalvageKitId, buyUnitPrice: 50, sellUnitPrice: 88);
            priceApi.AddPrice(JourneymansSalvageKitId, buyUnitPrice: 100, sellUnitPrice: 296);
            priceApi.AddPrice(MastersSalvageKitId, buyUnitPrice: 200, sellUnitPrice: 1536);
            priceApi.AddPrice(MysticForgeStoneId, buyUnitPrice: 50, sellUnitPrice: 130);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(MysticSalvageKitId, "Mystic Salvage Kit", "icon.png");
            itemApi.AddItem(FineSalvageKitId, "Fine Salvage Kit", "icon.png");
            itemApi.AddItem(JourneymansSalvageKitId, "Journeyman's Salvage Kit", "icon.png");
            itemApi.AddItem(MastersSalvageKitId, "Master's Salvage Kit", "icon.png");
            itemApi.AddItem(MysticForgeStoneId, "Mystic Forge Stone", "icon.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(composite),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateAsync(
                MysticSalvageKitId, 1, CancellationToken.None);

            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);

            // Mystic Salvage Kit should be crafted via MF recipe
            var craftStep = result.Plan.Steps.FirstOrDefault(s =>
                s.ItemId == MysticSalvageKitId && s.Source == AcquisitionSource.Craft);
            Assert.NotNull(craftStep);
            Assert.Equal(-4, craftStep.RecipeId);

            // Expected cost (BuyInstant = sellUnitPrice): 88 + 296 + 1536 + 250*130 = 34420
            long expectedTotal = 88L + 296 + 1536 + 250L * 130;
            Assert.Equal(expectedTotal, result.Plan.TotalCoinCost);
        }
    }
}
