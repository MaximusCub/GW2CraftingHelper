using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;
using static GW2CraftingHelper.Tests.Helpers.VendorOfferBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftingTreeBuilderTests
    {
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
            Assert.False(node.IsReferenceBranch); // no recipe to build a reference branch from
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
            Assert.False(node.IsReferenceBranch); // real craft, not a reference branch
            Assert.Single(node.Children);

            var child = node.Children[0];
            Assert.Equal(2, child.ItemId);
            Assert.Equal("Ingot", child.Name);
            Assert.Equal(2, child.Quantity);
            Assert.Equal(CraftingDecision.BuyFromTp, child.Decision);
            Assert.Equal(100, child.UnitCost);
            Assert.Equal(200, child.SubtreeCost);
            Assert.False(child.IsReferenceBranch); // no recipe to build a reference branch from
            Assert.Empty(child.Children);
        }

        /// <summary>
        /// Item 2 could be crafted from item 3 (Option 20), but buying it
        /// directly is cheaper, so the solver buys it - Item 2 gets a
        /// reference branch built from Option 20. Item 3 itself could ALSO
        /// be crafted from item 4 (Option 30), and buying it is also
        /// cheaper - but item 3 only exists here as part of item 2's
        /// reference branch, so it must NOT sprout its own nested reference
        /// branch (capped to at most one reference branch per root-to-leaf
        /// path; see CraftingTreeBuilder.BuildNode's insideReferenceBranch
        /// comment - an earlier, uncapped version of this recursion hung
        /// for 60+ seconds building a real Deldrimor Steel Ingot plan).
        /// </summary>
        [Fact]
        public void BoughtNode_WithRecipe_GetsReferenceChildren_CappedAtOneLevel()
        {
            var tree = Craftable(2, 5,
                Option(20, 1, 1,
                    Craftable(3, 1,
                        Option(30, 1, 1,
                            Leaf(4, 2)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 5 } },       // buy: 5 * 5 = 25
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 50 } },      // buy: 1 * 50 = 50
                { 4, new ItemPrice { ItemId = 4, BuyInstant = 1000 } }     // craft(3) via 4: 2 * 1000 = 2000
            };
            var metadata = Meta(
                (2, "Bought Item", "b.png"),
                (3, "Also Bought", "a.png"),
                (4, "Raw", "r.png"));

            var root = BuildViaRealSolver(tree, prices, metadata);

            // Item 2: bought (25 < craft-via-3's 50), reference branch built
            // from Option 20 even though nothing here was crafted.
            Assert.Equal(CraftingDecision.BuyFromTp, root.Decision);
            Assert.True(root.IsReferenceBranch);
            Assert.Equal(25, root.SubtreeCost);
            Assert.Single(root.Children);

            // Item 3: also independently bought (50 < craft-via-4's 2000)
            // and also has a recipe, but it appears INSIDE item 2's
            // reference branch - it must render as a plain leaf, not start
            // its own nested reference branch.
            var refChild = root.Children[0];
            Assert.Equal(3, refChild.ItemId);
            Assert.Equal(CraftingDecision.BuyFromTp, refChild.Decision);
            Assert.False(refChild.IsReferenceBranch);
            Assert.Equal(50, refChild.SubtreeCost);
            Assert.Empty(refChild.Children);
        }

        /// <summary>
        /// Regression test for the exact bug class that caused a real hang:
        /// an initial fix reset the "inside a reference branch" state to
        /// false on every Craft step, so a chain that alternates
        /// buy-with-a-recipe -> craft -> buy-with-a-recipe (extremely common
        /// in real GW2 crafting data) kept restarting new reference
        /// branches forever. Item 2 is bought but has a recipe (reference
        /// branch starts). Its reference child, item 3, is independently
        /// CRAFTED (cheaper to craft than buy) - the craft step must
        /// propagate "inside a reference branch" rather than clearing it.
        /// Item 3's own ingredient, item 5, is bought but ALSO has a recipe
        /// - if the cap did not propagate through the craft step, item 5
        /// would incorrectly sprout its own reference branch.
        /// </summary>
        [Fact]
        public void ReferenceBranch_StaysSuppressed_AcrossAnInterveningCraftDecision()
        {
            var tree = Craftable(2, 5,
                Option(20, 1, 1,
                    Craftable(3, 1,
                        Option(30, 1, 1,
                            Craftable(5, 1,
                                Option(40, 1, 1,
                                    Leaf(6, 1)))))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 1 } },     // buy: 5 * 1 = 5
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 200 } },   // buy: 1 * 200 = 200 (craft via 5 is cheaper: 20)
                { 5, new ItemPrice { ItemId = 5, BuyInstant = 20 } },    // buy: 1 * 20 = 20 (craft via 6 is far pricier: 1000)
                { 6, new ItemPrice { ItemId = 6, BuyInstant = 1000 } }   // buy: 1 * 1000 = 1000
            };
            var metadata = Meta(
                (2, "Root Bought", "r.png"),
                (3, "Mid Crafted", "m.png"),
                (5, "Inner Bought", "i.png"),
                (6, "Raw", "raw.png"));

            var root = BuildViaRealSolver(tree, prices, metadata);

            // Item 2: bought (5 < craft-via-3's 20) - reference branch starts.
            Assert.Equal(CraftingDecision.BuyFromTp, root.Decision);
            Assert.True(root.IsReferenceBranch);
            Assert.Single(root.Children);

            // Item 3: independently CRAFTED (20 < buy's 200) - reached only
            // via item 2's reference branch, so it is hypothetical content,
            // not a real crafting step, even though its own Decision is
            // Craft. Craft nodes are never themselves flagged as reference
            // branches (only bought-with-a-recipe nodes are), but the
            // suppression must still propagate to ITS children.
            var craftedInsideRef = root.Children[0];
            Assert.Equal(3, craftedInsideRef.ItemId);
            Assert.Equal(CraftingDecision.Craft, craftedInsideRef.Decision);
            Assert.False(craftedInsideRef.IsReferenceBranch);
            Assert.Single(craftedInsideRef.Children);

            // Item 5: bought (20 < craft-via-6's 1000) and has its own
            // recipe - the exact shape that starts a reference branch when
            // NOT already inside one. Reached here through the craft step
            // above, so it must stay suppressed: plain leaf, no children.
            var innerBought = craftedInsideRef.Children[0];
            Assert.Equal(5, innerBought.ItemId);
            Assert.Equal(CraftingDecision.BuyFromTp, innerBought.Decision);
            Assert.False(innerBought.IsReferenceBranch);
            Assert.Empty(innerBought.Children);
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
            Assert.False(treeNode.IsReferenceBranch);
            Assert.Empty(treeNode.Children);
            Assert.False(treeNode.IsIgnored); // genuine ownership, not the M34-B2b Ignore toggle
            Assert.False(treeNode.IsAchievementBitDeduped); // genuine ownership, not the M37 dedup flag
        }

        // ---- M37 (KNOWN-ISSUES #26): achievement-bit dedup collapses to Have + IsAchievementBitDeduped ----

        [Fact]
        public void AchievementBitDedupedNode_CollapsesToHave_SetsFlag()
        {
            // Mirrors AchievementBitDedupPrePass's own contract: a deduped
            // occurrence has Quantity == 0 and IsAchievementBitDeduped ==
            // true set directly on the RecipeNode (no NodeId/decisions
            // lookup involved at all - matches how genuine ownership's
            // Quantity == 0 short-circuit works above).
            var node = Leaf(1, 0);
            node.NodeId = 0;
            node.AchievementId = 8493;
            node.AchievementBit = 0;
            node.IsAchievementBitDeduped = true;
            var decisions = new Dictionary<int, SolverDecision>();
            var metadata = Meta((1, "Pile of Recycled Trebuchets", "pile.png"));

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata);

            Assert.Equal(CraftingDecision.Have, treeNode.Decision);
            Assert.Equal(0, treeNode.Quantity);
            Assert.True(treeNode.IsAchievementBitDeduped);
            Assert.False(treeNode.IsIgnored);
            Assert.Empty(treeNode.Children);
        }

        // ---- M34-B2b: manually "Ignore"-d items collapse to Have + IsIgnored ----

        [Fact]
        public void IgnoredItem_CollapsesToHave_SetsIsIgnored()
        {
            var node = Leaf(1, 5); // real, non-zero demand
            node.NodeId = 0;
            var decisions = new Dictionary<int, SolverDecision>
            {
                { 0, new SolverDecision { Source = AcquisitionSource.BuyFromTp, TotalCost = 500 } }
            };
            var metadata = Meta((1, "Ignored Item", "ignored.png"));
            var ignoredItemIds = new HashSet<int> { 1 };

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata, ignoredItemIds: ignoredItemIds);

            Assert.Equal(CraftingDecision.Have, treeNode.Decision);
            Assert.True(treeNode.IsIgnored);
            Assert.Equal(5, treeNode.Quantity); // Quantity itself is untouched - only the display/decision collapses
            Assert.Empty(treeNode.Children);
        }

        [Fact]
        public void IgnoredItemIds_DifferentItemId_NotAffected()
        {
            var node = Leaf(1, 5);
            node.NodeId = 0;
            var decisions = new Dictionary<int, SolverDecision>
            {
                { 0, new SolverDecision { Source = AcquisitionSource.BuyFromTp, TotalCost = 500 } }
            };
            var metadata = Meta((1, "Item", "i.png"));
            var ignoredItemIds = new HashSet<int> { 999 }; // different item id

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata, ignoredItemIds: ignoredItemIds);

            Assert.Equal(CraftingDecision.BuyFromTp, treeNode.Decision);
            Assert.False(treeNode.IsIgnored);
        }

        [Fact]
        public void IgnoredItemIds_Null_BehavesExactlyAsBefore()
        {
            var node = Leaf(1, 5);
            node.NodeId = 0;
            var decisions = new Dictionary<int, SolverDecision>
            {
                { 0, new SolverDecision { Source = AcquisitionSource.BuyFromTp, TotalCost = 500 } }
            };
            var metadata = Meta((1, "Item", "i.png"));

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata);

            Assert.Equal(CraftingDecision.BuyFromTp, treeNode.Decision);
            Assert.False(treeNode.IsIgnored);
        }

        [Fact]
        public void IgnoredItemIds_CurrencyNode_NeverCollapsedByIgnore()
        {
            // Ignore is scoped to Item nodes only (M34-B2b) - a Currency
            // node sharing the same numeric id must keep its normal
            // Currency treatment, never collapse to Have.
            var node = Leaf(23, 100, "Currency");
            node.NodeId = 0;
            var decisions = new Dictionary<int, SolverDecision>();
            var metadata = Meta();
            var ignoredItemIds = new HashSet<int> { 23 };

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata, ignoredItemIds: ignoredItemIds);

            Assert.Equal(CraftingDecision.Currency, treeNode.Decision);
            Assert.False(treeNode.IsIgnored);
        }

        [Fact]
        public void IgnoredItemIds_PropagatesToMatchingChild()
        {
            var ingredient = Leaf(2, 3);
            var option = Option(10, 1, 1, ingredient);
            var root = Craftable(1, 1, option);

            var solver = new PlanSolver();
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } }
            };
            var solveResult = solver.Solve(root, prices, null);

            var metadata = Meta((1, "Root", "r.png"), (2, "Child", "c.png"));
            var ignoredItemIds = new HashSet<int> { 2 }; // ingredient's item id, not root's

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(root, solveResult.Decisions, metadata, ignoredItemIds: ignoredItemIds);

            Assert.NotEqual(CraftingDecision.Have, treeNode.Decision); // root unaffected
            Assert.Single(treeNode.Children);
            Assert.Equal(CraftingDecision.Have, treeNode.Children[0].Decision);
            Assert.True(treeNode.Children[0].IsIgnored);
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
            Assert.False(treeNode.IsReferenceBranch);
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
        public void VendorCurrencyCosts_ThreadedOntoBuyFromVendorNode()
        {
            // M33 item 5 (Finding 3): a vendor offer paid partly/wholly in
            // non-coin currency (spirit shards here) must surface its
            // currency lines on the tree node, not just in the plan-wide
            // currency total - see SolverDecision.VendorCurrencyCosts.
            var tree = Leaf(1, 2);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    1, new List<VendorOffer>
                    {
                        new VendorOffer
                        {
                            OfferId = "v-currency",
                            OutputItemId = 1,
                            OutputCount = 1,
                            CostLines = new List<CostLine>
                            {
                                new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 10 },
                                new CostLine { Type = "Currency", Id = 23, Count = 50 }
                            },
                            MerchantName = "Miyani",
                            Locations = new List<string>()
                        }
                    }
                }
            };
            var metadata = Meta((1, "Vendor Item", "vendor.png"));

            var node = BuildViaRealSolver(tree, prices, metadata, vendorOffers);

            Assert.Equal(CraftingDecision.BuyFromVendor, node.Decision);
            Assert.NotNull(node.VendorCurrencyCosts);
            Assert.Single(node.VendorCurrencyCosts);
            Assert.Equal(23, node.VendorCurrencyCosts[0].Id);
            Assert.Equal(100, node.VendorCurrencyCosts[0].Count); // 50 per unit * qty 2
        }

        [Fact]
        public void VendorCurrencyCosts_NullOnNonVendorNode()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 50 } }
            };
            var metadata = Meta((1, "Item", "item.png"));

            var node = BuildViaRealSolver(tree, prices, metadata);

            Assert.Equal(CraftingDecision.BuyFromTp, node.Decision);
            Assert.Null(node.VendorCurrencyCosts);
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

        // --- Acquisition hints (M32) ---

        [Fact]
        public void UnknownDecision_WithHint_SetsAcquisitionHint()
        {
            var node = Leaf(71994, 1);
            node.NodeId = 5; // not present in decisions -> Unknown
            var decisions = new Dictionary<int, SolverDecision>();
            var metadata = Meta((71994, "Ball of Dark Energy", "b.png"));
            var hints = new Dictionary<int, AcquisitionHint>
            {
                { 71994, new AcquisitionHint { ItemId = 71994, Hint = "Salvaged from ascended gear." } }
            };

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata, hints);

            Assert.Equal(CraftingDecision.Unknown, treeNode.Decision);
            Assert.Equal("Salvaged from ascended gear.", treeNode.AcquisitionHint);
        }

        [Fact]
        public void UnknownDecision_NoHintEntry_StaysNull()
        {
            var node = Leaf(99, 1);
            node.NodeId = 5;
            var decisions = new Dictionary<int, SolverDecision>();
            var metadata = Meta((99, "Mystery", "m.png"));
            var hints = new Dictionary<int, AcquisitionHint>
            {
                { 71994, new AcquisitionHint { ItemId = 71994, Hint = "Unrelated item's hint." } }
            };

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata, hints);

            Assert.Equal(CraftingDecision.Unknown, treeNode.Decision);
            Assert.Null(treeNode.AcquisitionHint);
        }

        [Fact]
        public void NonUnknownDecision_HintEntryPresent_NeverSet()
        {
            // Item 1 is bought via TP (a real, priced source) but a hint
            // entry happens to exist for it anyway - hints must never
            // bleed onto a node that actually has a known source.
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var metadata = Meta((1, "Copper Ore", "copper.png"));
            var hints = new Dictionary<int, AcquisitionHint>
            {
                { 1, new AcquisitionHint { ItemId = 1, Hint = "Should never appear on a priced node.", Badge = "SALVAGE" } }
            };

            var solver = new PlanSolver();
            var solveResult = solver.Solve(tree, prices, null);
            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(tree, solveResult.Decisions, metadata, hints);

            Assert.Equal(CraftingDecision.BuyFromTp, treeNode.Decision);
            Assert.Null(treeNode.AcquisitionHint);
            Assert.Null(treeNode.AcquisitionBadge);
        }

        [Fact]
        public void UnknownDecision_WithBadge_SetsAcquisitionBadge()
        {
            var node = Leaf(71994, 1);
            node.NodeId = 5; // not present in decisions -> Unknown
            var decisions = new Dictionary<int, SolverDecision>();
            var metadata = Meta((71994, "Ball of Dark Energy", "b.png"));
            var hints = new Dictionary<int, AcquisitionHint>
            {
                { 71994, new AcquisitionHint { ItemId = 71994, Hint = "Salvaged from ascended gear.", Badge = "SALVAGE" } }
            };

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata, hints);

            Assert.Equal(CraftingDecision.Unknown, treeNode.Decision);
            Assert.Equal("SALVAGE", treeNode.AcquisitionBadge);
        }

        [Fact]
        public void UnknownDecision_HintWithoutBadge_AcquisitionBadgeStaysNull()
        {
            var node = Leaf(70698, 1);
            node.NodeId = 5; // not present in decisions -> Unknown
            var decisions = new Dictionary<int, SolverDecision>();
            var metadata = Meta((70698, "Gift of the Jungle", "g.png"));
            var hints = new Dictionary<int, AcquisitionHint>
            {
                { 70698, new AcquisitionHint { ItemId = 70698, Hint = "Received for map completion." } }
            };

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata, hints);

            Assert.Equal(CraftingDecision.Unknown, treeNode.Decision);
            Assert.Equal("Received for map completion.", treeNode.AcquisitionHint);
            Assert.Null(treeNode.AcquisitionBadge);
        }

        [Fact]
        public void UnknownDecision_NoHintEntry_AcquisitionBadgeStaysNull()
        {
            var node = Leaf(99, 1);
            node.NodeId = 5;
            var decisions = new Dictionary<int, SolverDecision>();
            var metadata = Meta((99, "Mystery", "m.png"));
            var hints = new Dictionary<int, AcquisitionHint>
            {
                { 71994, new AcquisitionHint { ItemId = 71994, Hint = "Unrelated item's hint.", Badge = "SALVAGE" } }
            };

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata, hints);

            Assert.Equal(CraftingDecision.Unknown, treeNode.Decision);
            Assert.Null(treeNode.AcquisitionBadge);
        }

        // ---- M34-B2a #1: per-node owned-quantity attribution ----

        [Fact]
        public void OwnedQuantityUsedByNodeId_PopulatesMatchingNode()
        {
            var node = Leaf(1, 2);
            node.NodeId = 7;
            var decisions = new Dictionary<int, SolverDecision>
            {
                { 7, new SolverDecision { Source = AcquisitionSource.BuyFromTp, TotalCost = 200 } }
            };
            var metadata = Meta((1, "Item", "i.png"));
            var ownedUsage = new Dictionary<int, int> { { 7, 3 } };

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata, ownedQuantityUsedByNodeId: ownedUsage);

            Assert.Equal(3, treeNode.OwnedQuantityUsed);
        }

        [Fact]
        public void OwnedQuantityUsedByNodeId_NoEntryForNode_DefaultsToZero()
        {
            var node = Leaf(1, 2);
            node.NodeId = 7;
            var decisions = new Dictionary<int, SolverDecision>
            {
                { 7, new SolverDecision { Source = AcquisitionSource.BuyFromTp, TotalCost = 200 } }
            };
            var metadata = Meta((1, "Item", "i.png"));
            var ownedUsage = new Dictionary<int, int> { { 99, 3 } }; // different node id

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata, ownedQuantityUsedByNodeId: ownedUsage);

            Assert.Equal(0, treeNode.OwnedQuantityUsed);
        }

        [Fact]
        public void OwnedQuantityUsedByNodeId_NullDictionary_DefaultsToZero()
        {
            var node = Leaf(1, 2);
            node.NodeId = 7;
            var decisions = new Dictionary<int, SolverDecision>
            {
                { 7, new SolverDecision { Source = AcquisitionSource.BuyFromTp, TotalCost = 200 } }
            };
            var metadata = Meta((1, "Item", "i.png"));

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata);

            Assert.Equal(0, treeNode.OwnedQuantityUsed);
        }

        [Fact]
        public void OwnedQuantityUsedByNodeId_AppliesEvenToHaveNode()
        {
            // Quantity == 0 -> the "Have" early return - OwnedQuantityUsed
            // must still be set (it is populated before that branch).
            var node = Leaf(1, 0);
            node.NodeId = 7;
            var decisions = new Dictionary<int, SolverDecision>();
            var metadata = Meta((1, "Item", "i.png"));
            var ownedUsage = new Dictionary<int, int> { { 7, 5 } };

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(node, decisions, metadata, ownedQuantityUsedByNodeId: ownedUsage);

            Assert.Equal(CraftingDecision.Have, treeNode.Decision);
            Assert.Equal(5, treeNode.OwnedQuantityUsed);
        }

        [Fact]
        public void OwnedQuantityUsedByNodeId_PropagatesToChildren()
        {
            var ingredient = Leaf(2, 3);
            var option = Option(10, 1, 1, ingredient);
            var root = Craftable(1, 1, option);

            var solver = new PlanSolver();
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } }
            };
            var solveResult = solver.Solve(root, prices, null);
            // Child's real NodeId, assigned by the solve above (root=0, child=1).
            int childNodeId = ingredient.NodeId;

            var metadata = Meta((1, "Root", "r.png"), (2, "Child", "c.png"));
            var ownedUsage = new Dictionary<int, int> { { childNodeId, 2 } };

            var builder = new CraftingTreeBuilder();
            var treeNode = builder.BuildTree(root, solveResult.Decisions, metadata, ownedQuantityUsedByNodeId: ownedUsage);

            Assert.Equal(0, treeNode.OwnedQuantityUsed);
            Assert.Single(treeNode.Children);
            Assert.Equal(2, treeNode.Children[0].OwnedQuantityUsed);
        }

        // ---- M38 WP-05: MapSource fails loudly on an unmapped AcquisitionSource ----

        [Fact]
        public void MapSource_UnmappedAcquisitionSource_ThrowsArgumentOutOfRangeException()
        {
            // AcquisitionSource.Currency is a real enum member that MapSource's switch
            // deliberately gives no explicit arm (see MapSource's own doc comment) -
            // it can never reach MapSource through the real BuildNode flow (a
            // non-"Item" node is intercepted and set to CraftingDecision.Currency
            // directly, before any decision lookup). A manually-constructed decision
            // is the only way to drive the default arm, which pins the one
            // intentional M38 WP-05 behavior change: throw instead of silently
            // returning CraftingDecision.Unknown.
            var node = Leaf(1, 1);
            node.NodeId = 0;
            var decisions = new Dictionary<int, SolverDecision>
            {
                { 0, new SolverDecision { Source = AcquisitionSource.Currency, TotalCost = 100 } }
            };
            var metadata = Meta((1, "Item", "i.png"));

            var builder = new CraftingTreeBuilder();

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.BuildTree(node, decisions, metadata));
        }

        private static void AssertChildrenNeverNull(CraftingTreeNode node)
        {
            Assert.NotNull(node.Children);
            foreach (var child in node.Children)
            {
                AssertChildrenNeverNull(child);
            }
        }

        // ---- W4B: vendor cost-component leaves ----

        /// <summary>
        /// Mixed offer: 5x item 42 (TP 10 each) + 3x currency 23 (no raw
        /// coin) - 2 kinds, so leaves ARE synthesized. Real solve, real
        /// builder, exactly like BuildViaRealSolver but threading the W4B
        /// currencyMetadata/owned* params.
        /// </summary>
        private static (CraftingTreeNode Node, SolverDecision Decision) BuildMixedVendorNode(
            int itemCostItemId = 42,
            int itemCostCount = 5,
            int currencyId = 23,
            int currencyCount = 3,
            int quantity = 2,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata = null,
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts = null,
            IReadOnlyDictionary<int, int> ownedVendorItemAmounts = null)
        {
            var tree = Leaf(1, quantity);
            var prices = new Dictionary<int, ItemPrice>
            {
                { itemCostItemId, new ItemPrice { ItemId = itemCostItemId, BuyInstant = 10 } }
            };
            var offer = ItemAndCurrencyVendorOffer(
                1, new[] { (itemCostItemId, itemCostCount) }, new[] { (currencyId, currencyCount) });
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var metadata = Meta((itemCostItemId, "Glob of Ectoplasm", "ecto.png"));

            var solver = new PlanSolver();
            var solveResult = solver.Solve(tree, prices, vendorOffers);
            var decision = solveResult.Decisions.Values.Single(d => d.Source == AcquisitionSource.BuyFromVendor);

            var builder = new CraftingTreeBuilder();
            var node = builder.BuildTree(
                tree, solveResult.Decisions, metadata,
                currencyMetadata: currencyMetadata,
                ownedCurrencyAmounts: ownedCurrencyAmounts,
                ownedVendorItemAmounts: ownedVendorItemAmounts);
            return (node, decision);
        }

        [Fact]
        public void MixedOffer_SynthesizesItemAndCurrencyLeaves()
        {
            var (node, decision) = BuildMixedVendorNode();

            Assert.Equal(CraftingDecision.BuyFromVendor, node.Decision);
            Assert.Equal(2, node.Children.Count);

            var itemLeaf = node.Children.Single(c => c.ItemId == 42);
            Assert.True(itemLeaf.IsCostComponent);
            Assert.Equal("Glob of Ectoplasm", itemLeaf.Name);
            Assert.Equal("ecto.png", itemLeaf.IconUrl);
            Assert.Equal(10, itemLeaf.Quantity); // 5 * qty 2
            Assert.Equal(100, itemLeaf.SubtreeCost); // 10 * unit price 10
            Assert.Empty(itemLeaf.Children);

            var currencyLeaf = node.Children.Single(c => c.ItemId == 23);
            Assert.True(currencyLeaf.IsCostComponent);
            Assert.Equal(6, currencyLeaf.Quantity); // 3 * qty 2
            // Currency leaf's cost cell is deliberately blank - the
            // quantity IS the cost.
            Assert.Null(currencyLeaf.SubtreeCost);

            // Parent's own SubtreeCost equals the item leaf's exact
            // GoldValue - the same number, never independently recomputed.
            Assert.Equal(itemLeaf.SubtreeCost, node.SubtreeCost);
            Assert.Equal(decision.TotalCost, node.SubtreeCost);
        }

        [Fact]
        public void MixedOffer_NoDecisionPillFields_NoRecipeId()
        {
            var (node, _) = BuildMixedVendorNode();
            var itemLeaf = node.Children.Single(c => c.ItemId == 42);

            // Component leaves have no feasible-source flags at all - never
            // override-clickable (see DecisionPillPlannerTests for the pill
            // vocabulary itself).
            Assert.False(itemLeaf.CanCraft);
            Assert.False(itemLeaf.CanBuyTp);
            Assert.False(itemLeaf.CanBuyVendor);
            Assert.Null(itemLeaf.RecipeId);
            Assert.False(itemLeaf.IsReferenceBranch);
        }

        [Fact]
        public void SingleKindVendorOffer_ItemOnly_NoLeaves()
        {
            // Pure-item offer (item folds directly to coin) - only 1 kind,
            // so the unchanged path applies: no leaves at all.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 42, new ItemPrice { ItemId = 42, BuyInstant = 10 } }
            };
            var offer = ItemAndCurrencyVendorOffer(1, new[] { (42, 5) }, currencyCostLines: null);
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var metadata = Meta((1, "Vendor Item", "v.png"));

            var node = BuildViaRealSolver(tree, prices, metadata, vendorOffers);

            Assert.Equal(CraftingDecision.BuyFromVendor, node.Decision);
            Assert.Empty(node.Children);
            Assert.Equal(50, node.SubtreeCost);
        }

        [Fact]
        public void SingleKindVendorOffer_CurrencyOnly_NoLeaves()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, coinCost: 0, currencyId: 23, currencyCount: 10) } }
            };
            var metadata = Meta((1, "Vendor Item", "v.png"));

            var node = BuildViaRealSolver(tree, prices, metadata, vendorOffers);

            Assert.Equal(CraftingDecision.BuyFromVendor, node.Decision);
            Assert.Empty(node.Children);
        }

        [Fact]
        public void SingleKindVendorOffer_CoinOnly_NoLeaves()
        {
            var tree = Leaf(1, 3);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 50) } }
            };
            var metadata = Meta((1, "Vendor Item", "v.png"));

            var node = BuildViaRealSolver(tree, prices, metadata, vendorOffers);

            Assert.Equal(CraftingDecision.BuyFromVendor, node.Decision);
            Assert.Empty(node.Children);
            Assert.Equal(150, node.SubtreeCost);
        }

        [Fact]
        public void CoinPlusCurrencyOffer_TwoKinds_SynthesizesCurrencyLeafOnly_NoCoinLeaf()
        {
            // Coin + currency, no item - 2 kinds, so leaves ARE
            // synthesized, but only for the currency (coin never gets its
            // own leaf - see BuildVendorCostComponentLeaves' doc comment).
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, coinCost: 20, currencyId: 23, currencyCount: 10) } }
            };
            var metadata = Meta((1, "Vendor Item", "v.png"));

            var node = BuildViaRealSolver(tree, prices, metadata, vendorOffers);

            Assert.Single(node.Children);
            var currencyLeaf = node.Children[0];
            Assert.True(currencyLeaf.IsCostComponent);
            Assert.Equal(23, currencyLeaf.ItemId);
            Assert.Equal(10, currencyLeaf.Quantity);
            Assert.Equal(20, node.SubtreeCost); // raw coin folded silently, no leaf
        }

        [Fact]
        public void MixedOffer_HavePill_FullCoverage()
        {
            var ownedItems = new Dictionary<int, int> { { 42, 999 } }; // more than needed (10)
            var (node, _) = BuildMixedVendorNode(ownedVendorItemAmounts: ownedItems);
            var itemLeaf = node.Children.Single(c => c.ItemId == 42);

            Assert.Equal(10, itemLeaf.ComponentOwnedQuantity); // clamped to need
            // Quantity/cost themselves are UNCHANGED by ownership.
            Assert.Equal(10, itemLeaf.Quantity);
            Assert.Equal(100, itemLeaf.SubtreeCost);
        }

        [Fact]
        public void MixedOffer_HavePill_PartialCoverage()
        {
            var ownedItems = new Dictionary<int, int> { { 42, 4 } }; // less than needed (10)
            var (node, _) = BuildMixedVendorNode(ownedVendorItemAmounts: ownedItems);
            var itemLeaf = node.Children.Single(c => c.ItemId == 42);

            Assert.Equal(4, itemLeaf.ComponentOwnedQuantity);
            Assert.Equal(10, itemLeaf.Quantity);
        }

        [Fact]
        public void MixedOffer_HavePill_NoCoverage_ZeroOwnedQuantity()
        {
            var (node, _) = BuildMixedVendorNode(ownedVendorItemAmounts: null);
            var itemLeaf = node.Children.Single(c => c.ItemId == 42);
            var currencyLeaf = node.Children.Single(c => c.ItemId == 23);

            Assert.Equal(0, itemLeaf.ComponentOwnedQuantity);
            Assert.Equal(0, currencyLeaf.ComponentOwnedQuantity);
        }

        [Fact]
        public void MixedOffer_CurrencyLeaf_HavePill_FromOwnedCurrencyAmounts()
        {
            var ownedCurrency = new Dictionary<int, int> { { 23, 6 } }; // exact coverage (need 6)
            var (node, _) = BuildMixedVendorNode(ownedCurrencyAmounts: ownedCurrency);
            var currencyLeaf = node.Children.Single(c => c.ItemId == 23);

            Assert.Equal(6, currencyLeaf.ComponentOwnedQuantity);
            Assert.Equal(6, currencyLeaf.Quantity);
        }

        [Fact]
        public void MixedOffer_CurrencyLeaf_NameIconFromCurrencyMetadata()
        {
            var currencyMeta = new Dictionary<int, CurrencyMetadata>
            {
                { 23, new CurrencyMetadata { CurrencyId = 23, Name = "Spirit Shards", IconUrl = "shard.png" } }
            };
            var (node, _) = BuildMixedVendorNode(currencyMetadata: currencyMeta);
            var currencyLeaf = node.Children.Single(c => c.ItemId == 23);

            Assert.Equal("Spirit Shards", currencyLeaf.Name);
            Assert.Equal("shard.png", currencyLeaf.IconUrl);
        }

        [Fact]
        public void MixedOffer_CurrencyLeaf_NoMetadata_FallsBackToOfflineName()
        {
            var (node, _) = BuildMixedVendorNode(currencyId: 23);
            var currencyLeaf = node.Children.Single(c => c.ItemId == 23);

            // Gw2Constants' offline fallback for id 23 is "Spirit Shards" -
            // see CurrencyDisplayResolver.ResolveName's fallback chain.
            Assert.Equal("Spirit Shards", currencyLeaf.Name);
        }

        [Fact]
        public void ComponentLeafNodeIds_StableAcrossTwoBuilds()
        {
            var tree = Leaf(1, 2);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 42, new ItemPrice { ItemId = 42, BuyInstant = 10 } }
            };
            var offer = ItemAndCurrencyVendorOffer(1, new[] { (42, 5) }, new[] { (23, 3) });
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var metadata = Meta((42, "Ecto", "e.png"));
            var solver = new PlanSolver();
            var builder = new CraftingTreeBuilder();

            // First build (assigns real NodeIds).
            var solveResult1 = solver.Solve(tree, prices, vendorOffers);
            var node1 = builder.BuildTree(tree, solveResult1.Decisions, metadata);

            // Second build against the SAME tree object, assignNodeIds:
            // false - mirrors ResolveWithOverrides' local re-solve.
            var solveResult2 = solver.Solve(
                tree, prices, vendorOffers, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: null,
                assignNodeIds: false);
            var node2 = builder.BuildTree(tree, solveResult2.Decisions, metadata);

            Assert.Equal(node1.NodeId, node2.NodeId);
            var itemLeaf1 = node1.Children.Single(c => c.ItemId == 42);
            var itemLeaf2 = node2.Children.Single(c => c.ItemId == 42);
            var currencyLeaf1 = node1.Children.Single(c => c.ItemId == 23);
            var currencyLeaf2 = node2.Children.Single(c => c.ItemId == 23);

            Assert.Equal(itemLeaf1.NodeId, itemLeaf2.NodeId);
            Assert.Equal(currencyLeaf1.NodeId, currencyLeaf2.NodeId);
            // Every synthetic id is negative - cannot collide with a real
            // (always >= 0) RecipeNodeIds-assigned id.
            Assert.True(itemLeaf1.NodeId < 0);
            Assert.True(currencyLeaf1.NodeId < 0);
            Assert.NotEqual(itemLeaf1.NodeId, currencyLeaf1.NodeId);
        }

        [Fact]
        public void ComponentLeaves_DoNotCollideWithRealNodeIds()
        {
            var (node, _) = BuildMixedVendorNode();
            var realIds = new HashSet<int> { node.NodeId };
            foreach (var leaf in node.Children)
            {
                Assert.DoesNotContain(leaf.NodeId, realIds);
                Assert.True(leaf.NodeId < 0);
            }
        }
    }
}
