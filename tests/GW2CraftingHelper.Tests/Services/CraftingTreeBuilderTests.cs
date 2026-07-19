using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftingTreeBuilderTests
    {
        private static RecipeNode Leaf(int id, int quantity, string type = "Item")
        {
            return new RecipeNode
            {
                Id = id,
                IngredientType = type,
                Quantity = quantity,
                Recipes = new List<RecipeOption>()
            };
        }

        private static RecipeNode Craftable(int id, int quantity, params RecipeOption[] recipes)
        {
            var node = new RecipeNode
            {
                Id = id,
                IngredientType = "Item",
                Quantity = quantity,
                Recipes = new List<RecipeOption>()
            };
            if (recipes != null)
            {
                node.Recipes.AddRange(recipes);
            }
            return node;
        }

        private static RecipeOption Option(int recipeId, int outputCount, int craftsNeeded, params RecipeNode[] ingredients)
        {
            var opt = new RecipeOption
            {
                RecipeId = recipeId,
                OutputCount = outputCount,
                CraftsNeeded = craftsNeeded,
                Ingredients = new List<RecipeNode>()
            };
            if (ingredients != null)
            {
                opt.Ingredients.AddRange(ingredients);
            }
            return opt;
        }

        private static Dictionary<int, ItemMetadata> Meta(params (int id, string name, string icon)[] items)
        {
            var dict = new Dictionary<int, ItemMetadata>();
            foreach (var (id, name, icon) in items)
            {
                dict[id] = new ItemMetadata { ItemId = id, Name = name, IconUrl = icon };
            }
            return dict;
        }

        /// <summary>
        /// Solve the tree and return the builder result along with solver decisions.
        /// This exercises real PlanSolver code paths.
        /// </summary>
        private static CraftingTreeNode BuildViaRealSolver(
            RecipeNode tree,
            Dictionary<int, ItemPrice> prices,
            Dictionary<int, ItemMetadata> metadata,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers = null)
        {
            var solver = new PlanSolver();
            var solveResult = solver.Solve(tree, prices, vendorOffers);

            var builder = new CraftingTreeBuilder();
            return builder.BuildTree(tree, solveResult.Decisions, metadata);
        }

        [Fact]
        public void LeafBuyNode_HasNoChildren()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var metadata = Meta((1, "Copper Ore", "copper.png"));

            var node = BuildViaRealSolver(tree, prices, metadata);

            Assert.Equal(1, node.ItemId);
            Assert.Equal("Copper Ore", node.Name);
            Assert.Equal("copper.png", node.IconUrl);
            Assert.Equal(5, node.Quantity);
            Assert.Equal(CraftingDecision.BuyFromTp, node.Decision);
            Assert.Empty(node.Children);
            Assert.Equal(100, node.UnitCost);
            Assert.Equal(500, node.SubtreeCost);
        }

        [Fact]
        public void Rarity_PopulatedFromMetadata_NullWhenAbsent()
        {
            // Item 1 crafts from item 2; only item 2 has rarity metadata.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } }
            };
            var metadata = new Dictionary<int, ItemMetadata>
            {
                { 1, new ItemMetadata { ItemId = 1, Name = "Sword", IconUrl = "s.png" } },
                { 2, new ItemMetadata { ItemId = 2, Name = "Ingot", IconUrl = "i.png", Rarity = "Fine" } }
            };

            var node = BuildViaRealSolver(tree, prices, metadata);

            Assert.Null(node.Rarity);
            Assert.Equal("Fine", node.Children[0].Rarity);
        }

        [Fact]
        public void CraftNode_ChildrenAreIngredients()
        {
            // Item 1 crafts from 2x item 2. Craft is cheaper.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var metadata = Meta(
                (1, "Sword", "sword.png"),
                (2, "Ingot", "ingot.png"));

            var node = BuildViaRealSolver(tree, prices, metadata);

            Assert.Equal(CraftingDecision.Craft, node.Decision);
            Assert.Equal(10, node.RecipeId);
            Assert.Null(node.UnitCost); // Craft nodes have no unit cost
            Assert.Equal(200, node.SubtreeCost);
            Assert.Single(node.Children);

            var child = node.Children[0];
            Assert.Equal(2, child.ItemId);
            Assert.Equal("Ingot", child.Name);
            Assert.Equal(2, child.Quantity);
            Assert.Equal(CraftingDecision.BuyFromTp, child.Decision);
            Assert.Equal(100, child.UnitCost);
            Assert.Equal(200, child.SubtreeCost);
            Assert.Empty(child.Children);
        }

        [Fact]
        public void OwnedItem_DecisionIsHave()
        {
            // A node with quantity 0 means owned
            var node = Leaf(1, 0);
            node.NodeId = 0;
            var decisions = new Dictionary<int, SolverDecision>();
            var metadata = Meta((1, "Owned Item", "owned.png"));

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata);

            Assert.Equal(CraftingDecision.Have, treeNode.Decision);
            Assert.Equal(0, treeNode.Quantity);
            Assert.Empty(treeNode.Children);
        }

        [Fact]
        public void CurrencyNode_DecisionIsCurrency()
        {
            var node = Leaf(23, 100, "Currency");
            node.NodeId = 0;
            var decisions = new Dictionary<int, SolverDecision>();
            var metadata = Meta();

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata);

            Assert.Equal(CraftingDecision.Currency, treeNode.Decision);
            Assert.Equal(23, treeNode.ItemId);
            Assert.Equal(100, treeNode.Quantity);
            Assert.Empty(treeNode.Children);
        }

        [Fact]
        public void MultiLevel_Tree_CorrectStructure()
        {
            // Root -> Craft(recipe 10) -> Intermediate -> Craft(recipe 20) -> Leaf(Buy)
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Craftable(2, 1,
                        Option(20, 1, 1,
                            Leaf(3, 2)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 10000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 5000 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 10 } }
            };
            var metadata = Meta(
                (1, "Final", "final.png"),
                (2, "Intermediate", "inter.png"),
                (3, "Raw", "raw.png"));

            var root = BuildViaRealSolver(tree, prices, metadata);

            // Root = Craft
            Assert.Equal(CraftingDecision.Craft, root.Decision);
            Assert.Equal(10, root.RecipeId);
            Assert.Single(root.Children);

            // Level 2 = Craft
            var mid = root.Children[0];
            Assert.Equal(2, mid.ItemId);
            Assert.Equal(CraftingDecision.Craft, mid.Decision);
            Assert.Equal(20, mid.RecipeId);
            Assert.Single(mid.Children);

            // Level 3 = Buy
            var leaf = mid.Children[0];
            Assert.Equal(3, leaf.ItemId);
            Assert.Equal(CraftingDecision.BuyFromTp, leaf.Decision);
            Assert.Empty(leaf.Children);
            Assert.Equal(10, leaf.UnitCost);
        }

        [Fact]
        public void MissingDecision_DecisionIsUnknown()
        {
            var node = Leaf(99, 5);
            node.NodeId = 42; // NodeId that won't be in decisions
            var decisions = new Dictionary<int, SolverDecision>();
            var metadata = Meta((99, "Mystery", "mystery.png"));

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata);

            Assert.Equal(CraftingDecision.Unknown, treeNode.Decision);
            Assert.Empty(treeNode.Children);
        }

        [Fact]
        public void CostAnnotations_Propagated()
        {
            var tree = Leaf(1, 10);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 50 } }
            };
            var metadata = Meta((1, "Item", "item.png"));

            var node = BuildViaRealSolver(tree, prices, metadata);

            Assert.Equal(CraftingDecision.BuyFromTp, node.Decision);
            Assert.Equal(500, node.SubtreeCost); // 10 * 50
            Assert.Equal(50, node.UnitCost);     // 500 / 10

            // Vendor test
            var tree2 = Leaf(2, 3);
            var prices2 = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    2, new List<VendorOffer>
                    {
                        new VendorOffer
                        {
                            OfferId = "v1",
                            OutputItemId = 2,
                            OutputCount = 1,
                            CostLines = new List<CostLine>
                            {
                                new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 200 }
                            },
                            MerchantName = "Test",
                            Locations = new List<string>()
                        }
                    }
                }
            };
            var metadata2 = Meta((2, "Vendor Item", "vendor.png"));
            var vendorNode = BuildViaRealSolver(tree2, prices2, metadata2, vendorOffers);

            Assert.Equal(CraftingDecision.BuyFromVendor, vendorNode.Decision);
            Assert.Equal(600, vendorNode.SubtreeCost); // 3 * 200
            Assert.Equal(200, vendorNode.UnitCost);    // 600 / 3
        }

        [Fact]
        public void SameItemDifferentPositions_SeparateNodes()
        {
            // Item 1 crafts from: item 2 (qty 3) + item 3 which also needs item 2 (qty 5)
            // Same item 2 at two tree positions should get distinct tree nodes
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 3),
                    Craftable(3, 1,
                        Option(20, 1, 1,
                            Leaf(2, 5)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 100000 } }
            };
            var metadata = Meta(
                (1, "Root", "r.png"),
                (2, "Shared", "s.png"),
                (3, "Mid", "m.png"));

            var root = BuildViaRealSolver(tree, prices, metadata);

            // Root should be Craft with 2 children: item 2 and item 3
            Assert.Equal(CraftingDecision.Craft, root.Decision);
            Assert.Equal(2, root.Children.Count);

            var directItem2 = root.Children[0];
            Assert.Equal(2, directItem2.ItemId);
            Assert.Equal(3, directItem2.Quantity);
            Assert.Equal(CraftingDecision.BuyFromTp, directItem2.Decision);

            var item3 = root.Children[1];
            Assert.Equal(3, item3.ItemId);
            Assert.Equal(CraftingDecision.Craft, item3.Decision);
            Assert.Single(item3.Children);

            var nestedItem2 = item3.Children[0];
            Assert.Equal(2, nestedItem2.ItemId);
            Assert.Equal(5, nestedItem2.Quantity);
            Assert.Equal(CraftingDecision.BuyFromTp, nestedItem2.Decision);

            // Distinct tree nodes even though same item ID
            Assert.NotSame(directItem2, nestedItem2);
            Assert.Equal(30, directItem2.SubtreeCost);  // 3 * 10
            Assert.Equal(50, nestedItem2.SubtreeCost);   // 5 * 10
        }

        [Fact]
        public void ChildrenNeverNull_AllNodeTypes()
        {
            // Craft node with a Buy child, a Currency child, and an Owned child
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 3),
                    Leaf(23, 50, "Currency"),
                    Leaf(4, 0)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } }
            };
            var metadata = Meta(
                (1, "Root", "r.png"),
                (2, "Mat", "m.png"),
                (4, "Owned", "o.png"));

            var root = BuildViaRealSolver(tree, prices, metadata);

            // Every node in the tree must have non-null Children
            AssertChildrenNeverNull(root);
        }

        [Fact]
        public void CurrencyNode_ResolvesKnownNames()
        {
            // Two known currencies: Spirit Shards (23) and Laurels (3)
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 3),
                    Leaf(23, 50, "Currency"),
                    Leaf(3, 10, "Currency")));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } }
            };
            var metadata = Meta(
                (1, "Root", "r.png"),
                (2, "Mat", "m.png"));

            var root = BuildViaRealSolver(tree, prices, metadata);

            Assert.Equal(CraftingDecision.Craft, root.Decision);
            var currencies = root.Children
                .Where(c => c.Decision == CraftingDecision.Currency)
                .OrderBy(c => c.ItemId)
                .ToList();
            Assert.Equal(2, currencies.Count);
            Assert.Equal("Laurels", currencies[0].Name);
            Assert.Equal(10, currencies[0].Quantity);
            Assert.Equal("Spirit Shards", currencies[1].Name);
            Assert.Equal(50, currencies[1].Quantity);
        }

        [Fact]
        public void CurrencyNode_UnknownId_FallsBackToCurrency()
        {
            // Currency ID 9999 is not in KnownCurrencyNames
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 3),
                    Leaf(9999, 10, "Currency")));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } }
            };
            var metadata = Meta(
                (1, "Root", "r.png"),
                (2, "Mat", "m.png"));

            var root = BuildViaRealSolver(tree, prices, metadata);

            var currencyChild = root.Children.First(
                c => c.Decision == CraftingDecision.Currency);
            Assert.Equal("Currency", currencyChild.Name);
        }

        [Fact]
        public void ItemNode_NotAffectedByCurrencyNaming()
        {
            // Item with ID 23 (same as Spirit Shards) must get its name from
            // metadata, not from the currency map
            var tree = Leaf(23, 5);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 23, new ItemPrice { ItemId = 23, BuyInstant = 100 } }
            };
            var metadata = Meta((23, "Vial of Blood", "vial.png"));

            var node = BuildViaRealSolver(tree, prices, metadata);

            Assert.Equal(CraftingDecision.BuyFromTp, node.Decision);
            Assert.Equal("Vial of Blood", node.Name);
        }

        [Fact]
        public void Children_SetToNull_CoercedToEmpty()
        {
            var node = new CraftingTreeNode { ItemId = 1, Name = "Test" };

            // Default is non-null
            Assert.NotNull(node.Children);
            Assert.Empty(node.Children);

            // Explicit null assignment coerced to empty
            node.Children = null;
            Assert.NotNull(node.Children);
            Assert.Empty(node.Children);
        }

        private static void AssertChildrenNeverNull(CraftingTreeNode node)
        {
            Assert.NotNull(node.Children);
            foreach (var child in node.Children)
            {
                AssertChildrenNeverNull(child);
            }
        }
    }
}
