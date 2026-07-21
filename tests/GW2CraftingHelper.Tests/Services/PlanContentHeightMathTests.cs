using System.Collections.Generic;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Exercises the real PlanContentHeightMath arithmetic against the same
    /// row-height constants CraftingPlanView's row builders use, so a future
    /// change to one side without the other would show up here as a wrong
    /// expected value rather than a silent drift (M33 C2a directive A).
    /// </summary>
    public class PlanContentHeightMathTests
    {
        private static PlanRowViewModel Row(PlanRowType type, string sublabel = null)
        {
            return new PlanRowViewModel { RowType = type, Sublabel = sublabel };
        }

        private static CraftingTreeNode Node(
            int nodeId, CraftingDecision decision = CraftingDecision.Craft,
            IReadOnlyList<CraftingTreeNode> children = null)
        {
            return new CraftingTreeNode
            {
                NodeId = nodeId,
                Decision = decision,
                Children = children
            };
        }

        // --- SectionBodyHeight: simple per-row-count sections ---

        [Fact]
        public void UsedMaterials_HeightIsRowCountTimesRowHeight()
        {
            var rows = new List<PlanRowViewModel> { Row(PlanRowType.UsedMaterial), Row(PlanRowType.UsedMaterial), Row(PlanRowType.UsedMaterial) };
            int expected = 3 * PlanContentHeightMath.UsedMaterialRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.UsedMaterials, rows));
        }

        [Fact]
        public void UsedMaterials_EmptyRows_HeightIsZero()
        {
            Assert.Equal(0, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.UsedMaterials, new List<PlanRowViewModel>()));
        }

        [Fact]
        public void UsedMaterials_NullRows_HeightIsZero()
        {
            Assert.Equal(0, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.UsedMaterials, null));
        }

        [Fact]
        public void ShoppingList_IncludesHeaderRowPlusRows()
        {
            var rows = new List<PlanRowViewModel> { Row(PlanRowType.ShoppingBuy), Row(PlanRowType.ShoppingVendor) };
            int expected = PlanContentHeightMath.ShoppingHeaderRowHeight + 2 * PlanContentHeightMath.ShoppingRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.ShoppingList, rows));
        }

        [Fact]
        public void CraftingSteps_HeightIsRowCountTimesRowHeight()
        {
            var rows = new List<PlanRowViewModel> { Row(PlanRowType.CraftStep), Row(PlanRowType.CraftStep) };
            int expected = 2 * PlanContentHeightMath.CraftStepRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.CraftingSteps, rows));
        }

        [Fact]
        public void CraftingSteps_MixedWithTimegatedNotice_SumsPerRowHeight()
        {
            // M34-B1 #3: a TimegatedNotice row renders via the shorter
            // plain-text row pattern (FallbackTextRowHeight), not the taller
            // numbered CraftStep row - height must be summed per row rather
            // than assumed uniform once a section can mix row kinds.
            var rows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CraftStep),
                Row(PlanRowType.TimegatedNotice)
            };
            int expected = PlanContentHeightMath.CraftStepRowHeight + PlanContentHeightMath.FallbackTextRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.CraftingSteps, rows));
        }

        [Fact]
        public void CraftingSteps_OnlyTimegatedNotices_UsesFallbackTextRowHeight()
        {
            var rows = new List<PlanRowViewModel> { Row(PlanRowType.TimegatedNotice) };
            Assert.Equal(
                PlanContentHeightMath.FallbackTextRowHeight,
                PlanContentHeightMath.SectionBodyHeight(PlanSectionType.CraftingSteps, rows));
        }

        [Fact]
        public void Disciplines_IncludesHeaderRowPlusRows()
        {
            var rows = new List<PlanRowViewModel> { Row(PlanRowType.DisciplineRow) };
            int expected = PlanContentHeightMath.CTableHeaderRowHeight + PlanContentHeightMath.DisciplineRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.RequiredDisciplines, rows));
        }

        [Fact]
        public void Recipes_MixOfSublabelAndNoSublabel_UsesPerRowHeight()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.RecipeRow, sublabel: null),
                Row(PlanRowType.RecipeRow, sublabel: "Missing!"),
                Row(PlanRowType.RecipeRow, sublabel: ""),
            };
            int expected = PlanContentHeightMath.CTableHeaderRowHeight
                + PlanContentHeightMath.RecipeRowHeightNoSublabel
                + PlanContentHeightMath.RecipeRowHeightWithSublabel
                + PlanContentHeightMath.RecipeRowHeightNoSublabel;
            Assert.Equal(expected, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.RequiredRecipes, rows));
        }

        [Fact]
        public void Summary_CoinRowPlusCurrencyRows()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CoinTotal),
                Row(PlanRowType.CurrencyCost),
                Row(PlanRowType.CurrencyCost),
            };
            int expected = PlanContentHeightMath.CostTileRowHeight + 2 * PlanContentHeightMath.CurrencyRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.Summary, rows));
        }

        [Fact]
        public void Summary_NoCoinRow_OmitsTileRow()
        {
            var rows = new List<PlanRowViewModel> { Row(PlanRowType.CurrencyCost) };
            Assert.Equal(PlanContentHeightMath.CurrencyRowHeight,
                PlanContentHeightMath.SectionBodyHeight(PlanSectionType.Summary, rows));
        }

        [Fact]
        public void UnknownSectionType_FallsBackToTextRowHeightPerRow()
        {
            var rows = new List<PlanRowViewModel> { Row(PlanRowType.CraftStep), Row(PlanRowType.CraftStep) };
            int expected = 2 * PlanContentHeightMath.FallbackTextRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.RecipeTree, rows));
        }

        // --- IsNodeExpanded ---

        [Fact]
        public void IsNodeExpanded_OverridePresent_WinsOverDefault()
        {
            var overrides = new Dictionary<int, bool> { { 5, false } };
            Assert.False(PlanContentHeightMath.IsNodeExpanded(5, depth: 0, dimmed: false, overrides));
        }

        [Fact]
        public void IsNodeExpanded_NoOverride_NonDimmedShallow_DefaultsExpanded()
        {
            Assert.True(PlanContentHeightMath.IsNodeExpanded(5, depth: 1, dimmed: false, null));
        }

        [Fact]
        public void IsNodeExpanded_NoOverride_DeepNode_DefaultsCollapsed()
        {
            Assert.False(PlanContentHeightMath.IsNodeExpanded(5, depth: 2, dimmed: false, null));
        }

        [Fact]
        public void IsNodeExpanded_NoOverride_Dimmed_AlwaysDefaultsCollapsed()
        {
            Assert.False(PlanContentHeightMath.IsNodeExpanded(5, depth: 0, dimmed: true, null));
        }

        // --- Tree height ---

        [Fact]
        public void TreeNodeHeight_Leaf_IsSingleRow()
        {
            var leaf = Node(1);
            Assert.Equal(PlanContentHeightMath.TreeRowHeight,
                PlanContentHeightMath.TreeNodeHeight(leaf, depth: 0, dimmed: false, null));
        }

        [Fact]
        public void TreeNodeHeight_NullNode_IsZero()
        {
            Assert.Equal(0, PlanContentHeightMath.TreeNodeHeight(null, 0, false, null));
        }

        [Fact]
        public void TreeNodeHeight_ExpandedByDefault_IncludesChildren()
        {
            var children = new List<CraftingTreeNode> { Node(2), Node(3) };
            var root = Node(1, children: children);

            // depth 0, non-dimmed -> default-expanded (depth < 2), so both
            // leaf children (depth 1, default-expanded too, but they have no
            // children of their own) contribute one row each.
            int expected = PlanContentHeightMath.TreeRowHeight + 2 * PlanContentHeightMath.TreeRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.TreeNodeHeight(root, depth: 0, dimmed: false, null));
        }

        [Fact]
        public void TreeNodeHeight_CollapsedViaOverride_ExcludesChildren()
        {
            var children = new List<CraftingTreeNode> { Node(2), Node(3) };
            var root = Node(1, children: children);
            var overrides = new Dictionary<int, bool> { { 1, false } };

            Assert.Equal(PlanContentHeightMath.TreeRowHeight,
                PlanContentHeightMath.TreeNodeHeight(root, depth: 0, dimmed: false, overrides));
        }

        [Fact]
        public void TreeNodeHeight_ExpandedViaOverride_AtDefaultCollapsedDepth_IncludesChildren()
        {
            // depth 2 defaults to collapsed; an explicit override still wins.
            var children = new List<CraftingTreeNode> { Node(2) };
            var node = Node(1, children: children);
            var overrides = new Dictionary<int, bool> { { 1, true } };

            int expected = PlanContentHeightMath.TreeRowHeight + PlanContentHeightMath.TreeRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.TreeNodeHeight(node, depth: 2, dimmed: false, overrides));
        }

        [Fact]
        public void TreeNodeHeight_NonCraftDecision_DimsChildrenAndDefaultsThemCollapsed()
        {
            // A BuyFromTp node's children are the "what it would cost to
            // craft instead" reference branch - dimmed, default-collapsed
            // regardless of depth (KNOWN-ISSUES tree dimming rule).
            var grandchildren = new List<CraftingTreeNode> { Node(3) };
            var child = Node(2, children: grandchildren);
            var root = Node(1, decision: CraftingDecision.BuyFromTp, children: new List<CraftingTreeNode> { child });

            // root's own row + child's row; child is dimmed (root.Decision
            // != Craft) so child defaults collapsed and its own grandchild
            // row never contributes.
            int expected = PlanContentHeightMath.TreeRowHeight + PlanContentHeightMath.TreeRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.TreeNodeHeight(root, depth: 0, dimmed: false, null));
        }

        [Fact]
        public void TreeChildFlowHeight_NoChildren_IsZero()
        {
            var leaf = Node(1);
            Assert.Equal(0, PlanContentHeightMath.TreeChildFlowHeight(leaf, 0, false, null));
        }

        [Fact]
        public void ChildrenHeight_SumsEachSiblingsTreeNodeHeight()
        {
            var siblings = new List<CraftingTreeNode> { Node(2), Node(3), Node(4) };
            int expected = 3 * PlanContentHeightMath.TreeRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.ChildrenHeight(siblings, childDepth: 1, childDimmed: false, null));
        }

        [Fact]
        public void ChildrenHeight_NullChildren_IsZero()
        {
            Assert.Equal(0, PlanContentHeightMath.ChildrenHeight(null, 0, false, null));
        }
    }
}
