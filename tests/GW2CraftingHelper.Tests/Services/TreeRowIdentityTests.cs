using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;
using static GW2CraftingHelper.Tests.Helpers.VendorOfferBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The gate the in-place tree refresh pairs rows through. The first
    /// test builds BOTH trees through the real PlanSolver and
    /// CraftingTreeBuilder rather than hand-writing the collision it is
    /// about - the point being that the collision is something production
    /// code produces, not something a test can only imagine.
    /// </summary>
    public class TreeRowIdentityTests
    {
        [Fact]
        public void TwoVendorOffersOfTheSameShape_ReuseNodeIdsForDifferentItems_AndAreRejected()
        {
            // Same plan, same offer SHAPE (one item cost + one currency
            // cost), different barter item. The solver has exactly one
            // offer to take in each build, which is what a re-solve after
            // an ignore looks like when the required quantity crosses a
            // bulk-offer boundary.
            var before = BuildMixedVendorNode(barterItemId: 42, barterItemName: "Glob of Ectoplasm");
            var after = BuildMixedVendorNode(barterItemId: 19976, barterItemName: "Mystic Coin");

            var beforeItemLeaf = before.Children.Single(c => c.ItemId == 42);
            var afterItemLeaf = after.Children.Single(c => c.ItemId == 19976);

            // The hazard itself, measured from the builder: the two leaves
            // name different items and carry the SAME synthetic NodeId,
            // because that id encodes the leaf's position in the offer's
            // cost lines.
            Assert.Equal(beforeItemLeaf.NodeId, afterItemLeaf.NodeId);
            Assert.NotEqual(beforeItemLeaf.ItemId, afterItemLeaf.ItemId);

            // Everything the structural half of the gate looks at agrees,
            // so structure alone would have accepted the repaint.
            Assert.Equal(before.Children.Count, after.Children.Count);
            Assert.Equal(beforeItemLeaf.Children.Count, afterItemLeaf.Children.Count);
            Assert.True(beforeItemLeaf.Quantity > 0 && afterItemLeaf.Quantity > 0);

            Assert.False(TreeRowIdentity.SameRow(beforeItemLeaf, afterItemLeaf));
        }

        [Fact]
        public void ARowRebuiltFromTheSameOffer_IsStillTheSameRow()
        {
            var before = BuildMixedVendorNode(barterItemId: 42, barterItemName: "Glob of Ectoplasm");
            var after = BuildMixedVendorNode(barterItemId: 42, barterItemName: "Glob of Ectoplasm");

            for (int i = 0; i < before.Children.Count; i++)
            {
                Assert.True(TreeRowIdentity.SameRow(before.Children[i], after.Children[i]));
            }
            Assert.True(TreeRowIdentity.SameRow(before, after));
        }

        [Fact]
        public void IgnoringALeafMaterial_LeavesEveryRowRepaintable()
        {
            // The case the in-place refresh was built for, and the one a
            // stricter gate could quietly have taken back: if identity
            // rejected here, every IGNORE click would pay a full rebuild
            // again and the dropped-click fix would be gone with no test
            // saying so.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2), Leaf(3, 4)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 50 } }
            };
            var metadata = new Dictionary<int, ItemMetadata>
            {
                { 1, new ItemMetadata { ItemId = 1, Name = "Finished Thing", IconUrl = "a.png" } },
                { 2, new ItemMetadata { ItemId = 2, Name = "Material Two", IconUrl = "b.png" } },
                { 3, new ItemMetadata { ItemId = 3, Name = "Material Three", IconUrl = "c.png" } }
            };

            var before = BuildDisplayTree(tree, prices, metadata, ignored: null);
            var after = BuildDisplayTree(tree, prices, metadata, ignored: new HashSet<int> { 2 });

            var byNodeId = new Dictionary<int, CraftingTreeNode>();
            Flatten(before, byNodeId);

            var afterNodes = new Dictionary<int, CraftingTreeNode>();
            Flatten(after, afterNodes);

            Assert.Equal(byNodeId.Count, afterNodes.Count);
            foreach (var pair in afterNodes)
            {
                Assert.True(
                    TreeRowIdentity.SameRow(byNodeId[pair.Key], pair.Value),
                    $"node {pair.Key} stopped being repaintable across an ignore");
            }
        }

        [Fact]
        public void QuantityMoving_IsWhatTheRefreshExistsToRepaint()
        {
            var built = Node(itemId: 42, quantity: 4);
            var fresh = Node(itemId: 42, quantity: 9);

            Assert.True(TreeRowIdentity.SameRow(built, fresh));
        }

        [Fact]
        public void AQtyPrefixAppearingOrDisappearing_ChangesWhichControlsTheRowHas()
        {
            Assert.False(TreeRowIdentity.SameRow(Node(quantity: 0), Node(quantity: 3)));
            Assert.False(TreeRowIdentity.SameRow(Node(quantity: 3), Node(quantity: 0)));
        }

        [Fact]
        public void ChildrenAppearingOrDisappearing_IsAStructuralChange()
        {
            var childless = Node();
            var parent = Node();
            parent.Children = new List<CraftingTreeNode> { Node(itemId: 7) };

            Assert.False(TreeRowIdentity.SameRow(childless, parent));
            Assert.False(TreeRowIdentity.SameRow(parent, childless));
        }

        // Each of the five below differs from the baseline in exactly one
        // fact the repaint never re-derives, so accepting it would leave
        // that fact on screen describing the wrong item.

        [Fact]
        public void ADifferentItemId_IsPartOfIdentity()
        {
            Assert.False(TreeRowIdentity.SameRow(Node(), Node(itemId: 43)));
        }

        [Fact]
        public void ADifferentName_IsPartOfIdentity()
        {
            Assert.False(TreeRowIdentity.SameRow(Node(), Node(name: "Mystic Coin")));
        }

        [Fact]
        public void ADifferentIcon_IsPartOfIdentity()
        {
            Assert.False(TreeRowIdentity.SameRow(Node(), Node(icon: "coin.png")));
        }

        [Fact]
        public void ADifferentRarity_IsPartOfIdentity()
        {
            Assert.False(TreeRowIdentity.SameRow(Node(), Node(rarity: "Legendary")));
        }

        [Fact]
        public void TheCostComponentFlag_IsPartOfIdentity()
        {
            var component = Node();
            component.IsCostComponent = true;

            Assert.False(TreeRowIdentity.SameRow(Node(), component));
        }

        [Fact]
        public void ANullOnEitherSide_IsNeverAMatch()
        {
            Assert.False(TreeRowIdentity.SameRow(null, Node()));
            Assert.False(TreeRowIdentity.SameRow(Node(), null));
            Assert.False(TreeRowIdentity.SameRow(null, null));
        }

        private static CraftingTreeNode BuildDisplayTree(
            RecipeNode tree,
            Dictionary<int, ItemPrice> prices,
            Dictionary<int, ItemMetadata> metadata,
            ISet<int> ignored)
        {
            var solveResult = new PlanSolver().Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null, ignoredItemIds: ignored);
            return new CraftingTreeBuilder().BuildTree(tree, solveResult.Decisions, metadata);
        }

        private static void Flatten(CraftingTreeNode node, Dictionary<int, CraftingTreeNode> into)
        {
            into[node.NodeId] = node;
            foreach (var child in node.Children)
            {
                Flatten(child, into);
            }
        }

        private static CraftingTreeNode Node(
            int itemId = 42,
            int quantity = 4,
            string name = "Glob of Ectoplasm",
            string icon = "ecto.png",
            string rarity = "Exotic")
        {
            return new CraftingTreeNode
            {
                ItemId = itemId,
                NodeId = -1001,
                Quantity = quantity,
                Name = name,
                IconUrl = icon,
                Rarity = rarity
            };
        }

        /// <summary>
        /// One BuyFromVendor node with a mixed item+currency offer, built
        /// through the real solver and the real tree builder, so the
        /// synthetic cost-component leaves under it are the ones the
        /// module actually renders.
        /// </summary>
        private static CraftingTreeNode BuildMixedVendorNode(int barterItemId, string barterItemName)
        {
            var tree = Leaf(1, 2);
            var prices = new Dictionary<int, ItemPrice>
            {
                { barterItemId, new ItemPrice { ItemId = barterItemId, BuyInstant = 10 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    1,
                    new List<VendorOffer>
                    {
                        ItemAndCurrencyVendorOffer(
                            1, new[] { (barterItemId, 5) }, new[] { (23, 3) })
                    }
                }
            };
            var metadata = new Dictionary<int, ItemMetadata>
            {
                { barterItemId, new ItemMetadata { ItemId = barterItemId, Name = barterItemName, IconUrl = "barter.png" } }
            };

            var solveResult = new PlanSolver().Solve(tree, prices, vendorOffers);
            return new CraftingTreeBuilder().BuildTree(tree, solveResult.Decisions, metadata);
        }
    }
}
