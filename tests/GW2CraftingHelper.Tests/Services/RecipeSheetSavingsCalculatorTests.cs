using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// opportunity-notes (RECIPE-SHEET SAVINGS) - direct unit tests on
    /// RecipeSheetSavingsCalculator's pure tree-walk, using plain
    /// CraftingTreeNode fixtures (no Blish reference, no solver/pipeline
    /// round-trip needed) plus a REAL VendorOfferStore backed by a
    /// temp-directory baseline (repo invariant: use real stores with
    /// temporary directories, never a fake/mirrored store).
    /// </summary>
    public class RecipeSheetSavingsCalculatorTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly VendorOfferLoader _loader;

        public RecipeSheetSavingsCalculatorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GW2CraftingHelper_Tests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
            _loader = new VendorOfferLoader();
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        private VendorOfferStore MakeStore(params VendorOffer[] offers)
        {
            var store = new VendorOfferStore(_tempDir, _loader);
            var dataset = new VendorOfferDataset
            {
                SchemaVersion = 1,
                GeneratedAt = "2026-01-01T00:00:00Z",
                Source = "test",
                Offers = new List<VendorOffer>(offers)
            };
            string json = _loader.Serialize(dataset);
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                store.LoadBaseline(stream);
            }
            return store;
        }

        private static VendorOffer CoinSheetOffer(int sheetItemId, int coinCost)
        {
            return new VendorOffer
            {
                OfferId = "sheet-" + sheetItemId,
                OutputItemId = sheetItemId,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = coinCost }
                },
                MerchantName = "TestMerchant",
                Locations = new List<string>()
            };
        }

        /// <summary>
        /// Bought item 100 (chosen unit cost 50), with a reference branch
        /// (recipe 999, "Chef" 400, LearnedFromItem) whose one ingredient
        /// costs 300 total for the 10 units needed (30/unit craft cost) -
        /// 20/unit savings. Sheet (item 500) costs 200 coin. Recipe 999 is
        /// missing (not in learnedRecipeIds).
        /// </summary>
        private static CraftingTreeNode BoughtNodeWithReferenceBranch(
            long unitCost = 50, int quantity = 10, long ingredientSubtreeCost = 300,
            int recipeId = 999, bool learnedFromItem = true,
            List<string> disciplines = null, int minRating = 400,
            List<CostLine> vendorCurrencyCosts = null)
        {
            return new CraftingTreeNode
            {
                ItemId = 100,
                Quantity = quantity,
                Decision = CraftingDecision.BuyFromTp,
                UnitCost = unitCost,
                IsReferenceBranch = true,
                ReferenceRecipeId = recipeId,
                ReferenceRecipeDisciplines = disciplines ?? new List<string> { "Chef" },
                ReferenceRecipeMinRating = minRating,
                ReferenceRecipeIsLearnedFromItem = learnedFromItem,
                VendorCurrencyCosts = vendorCurrencyCosts,
                Children = new[]
                {
                    new CraftingTreeNode
                    {
                        ItemId = 200,
                        Quantity = quantity * 2,
                        Decision = CraftingDecision.BuyFromTp,
                        SubtreeCost = ingredientSubtreeCost
                    }
                }
            };
        }

        [Fact]
        public void PositiveSavings_SheetAvailable_MissingLearnedFromItemRecipe_EmitsOpportunity()
        {
            var node = BoughtNodeWithReferenceBranch();
            var result = new CraftingPlanResult { CraftingTree = node };
            var store = MakeStore(CoinSheetOffer(500, 200));
            var sheetMap = new Dictionary<int, int> { { 999, 500 } };

            RecipeSheetSavingsCalculator.Apply(
                result, learnedRecipeIds: new HashSet<int>(), prices: new Dictionary<int, ItemPrice>(),
                priceBasis: PriceBasis.BuyOrder, vendorOfferStore: store,
                recipeSheetItemIdByRecipeId: sheetMap, characterDisciplines: null);

            Assert.Single(result.RecipeSheetSavingsOpportunities);
            var opp = result.RecipeSheetSavingsOpportunities[0];
            Assert.Equal(100, opp.ItemId);
            Assert.Equal(999, opp.RecipeId);
            Assert.Equal(500, opp.SheetItemId);
            Assert.Equal(200, opp.SheetCost);
            Assert.Equal(20, opp.SavingsPerUnit); // 50 - 30
            Assert.False(opp.DisciplineBlocked); // no snapshot -> never claim blocked
        }

        [Fact]
        public void RecipeAlreadyLearned_NoOpportunity()
        {
            var node = BoughtNodeWithReferenceBranch();
            var result = new CraftingPlanResult { CraftingTree = node };
            var store = MakeStore(CoinSheetOffer(500, 200));
            var sheetMap = new Dictionary<int, int> { { 999, 500 } };

            RecipeSheetSavingsCalculator.Apply(
                result, learnedRecipeIds: new HashSet<int> { 999 }, prices: new Dictionary<int, ItemPrice>(),
                priceBasis: PriceBasis.BuyOrder, vendorOfferStore: store,
                recipeSheetItemIdByRecipeId: sheetMap, characterDisciplines: null);

            Assert.Empty(result.RecipeSheetSavingsOpportunities);
        }

        [Fact]
        public void NotLearnedFromItem_NoOpportunity()
        {
            var node = BoughtNodeWithReferenceBranch(learnedFromItem: false);
            var result = new CraftingPlanResult { CraftingTree = node };
            var store = MakeStore(CoinSheetOffer(500, 200));
            var sheetMap = new Dictionary<int, int> { { 999, 500 } };

            RecipeSheetSavingsCalculator.Apply(
                result, learnedRecipeIds: new HashSet<int>(), prices: new Dictionary<int, ItemPrice>(),
                priceBasis: PriceBasis.BuyOrder, vendorOfferStore: store,
                recipeSheetItemIdByRecipeId: sheetMap, characterDisciplines: null);

            Assert.Empty(result.RecipeSheetSavingsOpportunities);
        }

        [Fact]
        public void SheetNotInCuratedMap_EmitsNothing()
        {
            var node = BoughtNodeWithReferenceBranch();
            var result = new CraftingPlanResult { CraftingTree = node };
            var store = MakeStore(CoinSheetOffer(500, 200));

            RecipeSheetSavingsCalculator.Apply(
                result, learnedRecipeIds: new HashSet<int>(), prices: new Dictionary<int, ItemPrice>(),
                priceBasis: PriceBasis.BuyOrder, vendorOfferStore: store,
                recipeSheetItemIdByRecipeId: new Dictionary<int, int>(), characterDisciplines: null);

            Assert.Empty(result.RecipeSheetSavingsOpportunities);
        }

        [Fact]
        public void SheetInMapButNoVendorOffer_EmitsNothing()
        {
            var node = BoughtNodeWithReferenceBranch();
            var result = new CraftingPlanResult { CraftingTree = node };
            var store = MakeStore(); // no offers at all
            var sheetMap = new Dictionary<int, int> { { 999, 500 } };

            RecipeSheetSavingsCalculator.Apply(
                result, learnedRecipeIds: new HashSet<int>(), prices: new Dictionary<int, ItemPrice>(),
                priceBasis: PriceBasis.BuyOrder, vendorOfferStore: store,
                recipeSheetItemIdByRecipeId: sheetMap, characterDisciplines: null);

            Assert.Empty(result.RecipeSheetSavingsOpportunities);
        }

        [Fact]
        public void NonPositiveSavings_NoOpportunity()
        {
            // Craft cost per unit (300/10=30) now exceeds chosen cost (25/unit).
            var node = BoughtNodeWithReferenceBranch(unitCost: 25);
            var result = new CraftingPlanResult { CraftingTree = node };
            var store = MakeStore(CoinSheetOffer(500, 200));
            var sheetMap = new Dictionary<int, int> { { 999, 500 } };

            RecipeSheetSavingsCalculator.Apply(
                result, learnedRecipeIds: new HashSet<int>(), prices: new Dictionary<int, ItemPrice>(),
                priceBasis: PriceBasis.BuyOrder, vendorOfferStore: store,
                recipeSheetItemIdByRecipeId: sheetMap, characterDisciplines: null);

            Assert.Empty(result.RecipeSheetSavingsOpportunities);
        }

        [Fact]
        public void VendorCurrencyCostsPresent_NotComparable_NoOpportunity()
        {
            var node = BoughtNodeWithReferenceBranch(
                vendorCurrencyCosts: new List<CostLine> { new CostLine { Type = "Currency", Id = 2, Count = 10 } });
            var result = new CraftingPlanResult { CraftingTree = node };
            var store = MakeStore(CoinSheetOffer(500, 200));
            var sheetMap = new Dictionary<int, int> { { 999, 500 } };

            RecipeSheetSavingsCalculator.Apply(
                result, learnedRecipeIds: new HashSet<int>(), prices: new Dictionary<int, ItemPrice>(),
                priceBasis: PriceBasis.BuyOrder, vendorOfferStore: store,
                recipeSheetItemIdByRecipeId: sheetMap, characterDisciplines: null);

            Assert.Empty(result.RecipeSheetSavingsOpportunities);
        }

        [Fact]
        public void DisciplineBlocked_WhenAccountLacksIt()
        {
            var node = BoughtNodeWithReferenceBranch();
            var result = new CraftingPlanResult { CraftingTree = node };
            var store = MakeStore(CoinSheetOffer(500, 200));
            var sheetMap = new Dictionary<int, int> { { 999, 500 } };
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Alice", Discipline = "Chef", Rating = 200 }
            };

            RecipeSheetSavingsCalculator.Apply(
                result, learnedRecipeIds: new HashSet<int>(), prices: new Dictionary<int, ItemPrice>(),
                priceBasis: PriceBasis.BuyOrder, vendorOfferStore: store,
                recipeSheetItemIdByRecipeId: sheetMap, characterDisciplines: characterDisciplines);

            var opp = Assert.Single(result.RecipeSheetSavingsOpportunities);
            Assert.True(opp.DisciplineBlocked);
            Assert.Equal("Chef", opp.Discipline);
            Assert.Equal(400, opp.RequiredRating);
        }

        [Fact]
        public void DisciplineSatisfied_NotBlocked()
        {
            var node = BoughtNodeWithReferenceBranch();
            var result = new CraftingPlanResult { CraftingTree = node };
            var store = MakeStore(CoinSheetOffer(500, 200));
            var sheetMap = new Dictionary<int, int> { { 999, 500 } };
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Alice", Discipline = "Chef", Rating = 400 }
            };

            RecipeSheetSavingsCalculator.Apply(
                result, learnedRecipeIds: new HashSet<int>(), prices: new Dictionary<int, ItemPrice>(),
                priceBasis: PriceBasis.BuyOrder, vendorOfferStore: store,
                recipeSheetItemIdByRecipeId: sheetMap, characterDisciplines: characterDisciplines);

            var opp = Assert.Single(result.RecipeSheetSavingsOpportunities);
            Assert.False(opp.DisciplineBlocked);
        }

        [Fact]
        public void MultiItemRoots_WalksEveryRoot()
        {
            var nodeA = BoughtNodeWithReferenceBranch();
            nodeA.ItemId = 100;
            var nodeB = BoughtNodeWithReferenceBranch(recipeId: 998);
            nodeB.ItemId = 101;
            var result = new CraftingPlanResult
            {
                MultiItemRoots = new List<CraftingTreeNode> { nodeA, nodeB }
            };
            var store = MakeStore(CoinSheetOffer(500, 200), CoinSheetOffer(600, 200));
            var sheetMap = new Dictionary<int, int> { { 999, 500 }, { 998, 600 } };

            RecipeSheetSavingsCalculator.Apply(
                result, learnedRecipeIds: new HashSet<int>(), prices: new Dictionary<int, ItemPrice>(),
                priceBasis: PriceBasis.BuyOrder, vendorOfferStore: store,
                recipeSheetItemIdByRecipeId: sheetMap, characterDisciplines: null);

            Assert.Equal(2, result.RecipeSheetSavingsOpportunities.Count);
        }

        [Fact]
        public void NullResult_NoOp()
        {
            RecipeSheetSavingsCalculator.Apply(
                null, null, null, PriceBasis.BuyOrder, null, null, null);
        }

        [Fact]
        public void NoTreeAtAll_EmptyOutputsListNotNull()
        {
            var result = new CraftingPlanResult();

            RecipeSheetSavingsCalculator.Apply(
                result, new HashSet<int>(), new Dictionary<int, ItemPrice>(), PriceBasis.BuyOrder,
                MakeStore(), new Dictionary<int, int> { { 1, 2 } }, null);

            Assert.NotNull(result.RecipeSheetSavingsOpportunities);
            Assert.Empty(result.RecipeSheetSavingsOpportunities);
        }

        [Fact]
        public void PicksCheapestOfMultipleSheetOffers()
        {
            var node = BoughtNodeWithReferenceBranch();
            var result = new CraftingPlanResult { CraftingTree = node };
            var store = MakeStore(
                new VendorOffer
                {
                    OfferId = "expensive", OutputItemId = 500, OutputCount = 1,
                    CostLines = new List<CostLine> { new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 999 } },
                    MerchantName = "Expensive", Locations = new List<string>()
                },
                CoinSheetOffer(500, 200));
            var sheetMap = new Dictionary<int, int> { { 999, 500 } };

            RecipeSheetSavingsCalculator.Apply(
                result, learnedRecipeIds: new HashSet<int>(), prices: new Dictionary<int, ItemPrice>(),
                priceBasis: PriceBasis.BuyOrder, vendorOfferStore: store,
                recipeSheetItemIdByRecipeId: sheetMap, characterDisciplines: null);

            Assert.Equal(200, result.RecipeSheetSavingsOpportunities[0].SheetCost);
        }
    }
}
