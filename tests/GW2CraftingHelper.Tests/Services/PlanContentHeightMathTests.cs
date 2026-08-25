using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Exercises the real PlanContentHeightMath arithmetic against the same
    /// row-height constants CraftingPlanView's row builders use, so a future
    /// change to one side without the other would show up here as a wrong
    /// expected value rather than a silent drift.
    /// </summary>
    public class PlanContentHeightMathTests
    {
        // --- Chrome-band clearances (the type ramp's vertical half) ---
        //
        // Each band below holds text in a tier named by TypeRampMetrics,
        // and each has something directly under it that the text's
        // DESCENDERS must not touch: the band's own bottom edge, a 2px
        // divider, or a coin run. These are the assertions that make the
        // 20/24 ramp a constant swap - retreat the tier seats to 18/22 and
        // whichever height stops being derived correctly fails here.
        //
        // 2px, never 1: LabelHelpers.CreateRowDivider's scissor-defect note
        // (M36b) records that a 1px gap survives the default UI scale and
        // vanishes at "Small".
        private const int ScissorSafeClearance = 2;

        [Fact]
        public void ColumnHeaderBand_HoldsItsLabelsDescenders()
        {
            int inkBottom = TypeRampMetrics.InkBottom(
                TypeRampMetrics.ColumnHeaderInk, PlanContentHeightMath.CTableHeaderLabelY);

            Assert.True(
                inkBottom + ScissorSafeClearance <= PlanContentHeightMath.CTableHeaderRowHeight,
                $"header ink bottom {inkBottom} crowds the "
                    + $"{PlanContentHeightMath.CTableHeaderRowHeight}px band");
        }

        [Fact]
        public void ColumnHeaderBand_KeepsTheOpticalPlacementTheBodyHeaderHad()
        {
            // The placement being kept is the Body-16 header's: it sat at
            // LabelY 5, so its cap top was that far below the band's top
            // edge. The band grew by exactly what the taller font's
            // descenders needed, and the label did not drift up the band.
            //
            // Read out of the ink rather than written as the literal 8,
            // because 8 is only what THIS tier seat happens to make it. A
            // seat swap moves CTableHeaderLabelY with it (18/22 wants 5,
            // not 4), and this has to name the required value instead of
            // reading as "the other seat is a regression".
            const int bodyHeaderLabelY = 5;
            int inheritedCapTop = bodyHeaderLabelY + TypeRampMetrics.BodyInk.CapTopY;

            Assert.Equal(
                inheritedCapTop,
                PlanContentHeightMath.CTableHeaderLabelY + TypeRampMetrics.ColumnHeaderInk.CapTopY);
        }

        [Fact]
        public void SectionHeaderBand_TitleAndCaretBothClearTheDivider()
        {
            // The divider is a 2px rule bottom-anchored at height - 3.
            int dividerTop = PlanContentHeightMath.SectionHeaderRowHeight - 3;

            int titleInk = TypeRampMetrics.InkBottom(
                TypeRampMetrics.SectionTitleInk, PlanContentHeightMath.SectionHeaderTitleY);
            int caretInk = TypeRampMetrics.InkBottom(
                TypeRampMetrics.BodyInk, PlanContentHeightMath.SectionHeaderCaretY);

            Assert.True(
                titleInk + ScissorSafeClearance <= dividerTop,
                $"section title ink bottom {titleInk} crowds the divider at {dividerTop}");
            Assert.True(
                caretInk + ScissorSafeClearance <= dividerTop,
                $"caret ink bottom {caretInk} crowds the divider at {dividerTop}");
        }

        [Fact]
        public void SectionHeaderBand_CaretSitsOnTheTitlesReadingLine()
        {
            // Two tiers on one line are baseline-aligned, not top-aligned -
            // with the 1px optical lift the pair carried at the old sizes.
            int titleBaseline = PlanContentHeightMath.SectionHeaderTitleY
                + TypeRampMetrics.SectionTitleInk.BaselineY;
            int caretBaseline = PlanContentHeightMath.SectionHeaderCaretY
                + TypeRampMetrics.BodyInk.BaselineY;

            Assert.InRange(titleBaseline - caretBaseline, 0, 1);
        }

        [Fact]
        public void CostTileRow_CaptionBlockEndsAboveTheAmountRun()
        {
            // The band bottom-anchors a coin run (never shorter than the
            // 20px coin icon) above its own bottom pad; the caption block
            // is the caption's line box plus the 2px the renderer adds
            // under it.
            int captionBlockBottom = PlanContentHeightMath.CostTileCaptionY
                + TypeRampMetrics.ColumnHeaderInk.LineHeight
                + 2;
            int amountY = PlanContentHeightMath.CostTileRowHeight
                - PlanContentHeightMath.CostTileAmountBottomPad
                - CoinSegmentMath.CoinIconSize;

            Assert.True(
                amountY >= captionBlockBottom,
                $"amount run at {amountY} overprints a caption block ending at {captionBlockBottom}");
        }

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

        // Used Materials gained an Item/Amount header in audit batch J's
        // chrome unification, and the Shopping List's own 22px unbanded
        // header became the shared 26px band. Both are counted the way the
        // two c-tables already were - unconditionally, because all four
        // renderers emit the header before looking at the row count.
        [Fact]
        public void UsedMaterials_IncludesHeaderRowPlusRows()
        {
            var rows = new List<PlanRowViewModel> { Row(PlanRowType.UsedMaterial), Row(PlanRowType.UsedMaterial), Row(PlanRowType.UsedMaterial) };
            int expected = PlanContentHeightMath.CTableHeaderRowHeight + 3 * PlanContentHeightMath.UsedMaterialRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.UsedMaterials, rows));
        }

        [Fact]
        public void UsedMaterials_EmptyRows_IsTheHeaderAlone()
        {
            Assert.Equal(
                PlanContentHeightMath.CTableHeaderRowHeight,
                PlanContentHeightMath.SectionBodyHeight(PlanSectionType.UsedMaterials, new List<PlanRowViewModel>()));
        }

        [Fact]
        public void UsedMaterials_NullRows_IsTheHeaderAlone()
        {
            Assert.Equal(
                PlanContentHeightMath.CTableHeaderRowHeight,
                PlanContentHeightMath.SectionBodyHeight(PlanSectionType.UsedMaterials, null));
        }

        [Fact]
        public void ShoppingList_IncludesHeaderRowPlusRows()
        {
            var rows = new List<PlanRowViewModel> { Row(PlanRowType.ShoppingBuy), Row(PlanRowType.ShoppingVendor) };
            int expected = PlanContentHeightMath.CTableHeaderRowHeight + 2 * PlanContentHeightMath.ShoppingRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.ShoppingList, rows));
        }

        // Every table header in the plan is now one band of one height -
        // the drift this replaced was three styles across six tables.
        [Theory]
        [InlineData(PlanSectionType.UsedMaterials)]
        [InlineData(PlanSectionType.ShoppingList)]
        [InlineData(PlanSectionType.RequiredDisciplines)]
        [InlineData(PlanSectionType.RequiredRecipes)]
        public void EveryHeaderedSection_ReservesTheSameHeaderHeight(PlanSectionType sectionType)
        {
            Assert.Equal(
                PlanContentHeightMath.CTableHeaderRowHeight,
                PlanContentHeightMath.SectionBodyHeight(sectionType, new List<PlanRowViewModel>()));
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
            // A TimegatedNotice row renders via the shorter
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
        public void RecipeRowHeight_ExactlyFitsIconFramePlusDivider()
        {
            // Views/Rendering/RecipesSectionRenderer.CreateRecipeRow
            // places a 34px rarity-framed icon at y=0
            // and a bottom-anchored 2px row divider inside rowHeight - the
            // constant must equal exactly icon + divider (34 + 2 = 36) with
            // no overlap or slack, locking the fix that closed the
            // pre-existing overflow KNOWN-ISSUES #23 mis-described as
            // "several pixels of headroom" for this row.
            Assert.Equal(36, PlanContentHeightMath.RecipeRowHeight);
        }

        [Fact]
        public void Recipes_SublabelNoLongerChangesRowHeight()
        {
            // The discipline moved from a second line under the name to a
            // real column (Services/RecipesColumnMath), so a row carrying
            // one is exactly as tall as a row that does not. This is the
            // regression guard for the 48px twin that used to exist: a
            // section counted at two heights and drawn at one desyncs its
            // container from its rows.
            var rows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.RecipeRow, sublabel: null),
                Row(PlanRowType.RecipeRow, sublabel: "Armorsmith 400"),
                Row(PlanRowType.RecipeRow, sublabel: ""),
            };
            int expected = PlanContentHeightMath.CTableHeaderRowHeight
                + 3 * PlanContentHeightMath.RecipeRowHeight;
            Assert.Equal(expected, PlanContentHeightMath.SectionBodyHeight(PlanSectionType.RequiredRecipes, rows));
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
            // regardless of depth (KNOWN-ISSUES #47, the tree dimming rule).
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

        // --- MultiRootTreeFlowHeight ---

        [Fact]
        public void MultiRootTreeFlowHeight_NullRoots_IsZero()
        {
            Assert.Equal(0, PlanContentHeightMath.MultiRootTreeFlowHeight(null, null));
        }

        [Fact]
        public void MultiRootTreeFlowHeight_EmptyRoots_IsZero()
        {
            Assert.Equal(0, PlanContentHeightMath.MultiRootTreeFlowHeight(new List<CraftingTreeNode>(), null));
        }

        [Fact]
        public void MultiRootTreeFlowHeight_SingleRoot_IsTheColumnHeaderPlusThatRootsOwnHeight()
        {
            // The single-item case is the shared column header (one per
            // tree section, never one per root) plus exactly what
            // TreeNodeHeight reports for that root - no divider.
            var children = new List<CraftingTreeNode> { Node(2), Node(3) };
            var root = Node(1, children: children);
            var roots = new List<CraftingTreeNode> { root };

            int expected = PlanContentHeightMath.CTableHeaderRowHeight
                + PlanContentHeightMath.TreeNodeHeight(root, 0, false, null);
            Assert.Equal(expected, PlanContentHeightMath.MultiRootTreeFlowHeight(roots, null));
        }

        [Fact]
        public void MultiRootTreeFlowHeight_ColumnHeaderCountedOnce_NotPerRoot()
        {
            var one = new List<CraftingTreeNode> { Node(1) };
            var three = new List<CraftingTreeNode> { Node(1), Node(2), Node(3) };

            int perRootRowsAndDividers =
                3 * PlanContentHeightMath.TreeRowHeight + 2 * PlanContentHeightMath.MultiRootDividerHeight;

            Assert.Equal(
                PlanContentHeightMath.CTableHeaderRowHeight + PlanContentHeightMath.TreeRowHeight,
                PlanContentHeightMath.MultiRootTreeFlowHeight(one, null));
            Assert.Equal(
                PlanContentHeightMath.CTableHeaderRowHeight + perRootRowsAndDividers,
                PlanContentHeightMath.MultiRootTreeFlowHeight(three, null));
        }

        [Fact]
        public void MultiRootTreeFlowHeight_MultipleRoots_SumsEachRootsOwnHeightPlusDividers()
        {
            var rootA = Node(1, children: new List<CraftingTreeNode> { Node(2) });
            var rootB = Node(3);
            var rootC = Node(4, children: new List<CraftingTreeNode> { Node(5), Node(6) });
            var roots = new List<CraftingTreeNode> { rootA, rootB, rootC };

            // One divider between each pair of consecutive roots (2 gaps
            // for 3 roots) - never before the first or after the last.
            int expected =
                PlanContentHeightMath.CTableHeaderRowHeight +
                PlanContentHeightMath.TreeNodeHeight(rootA, 0, false, null) +
                PlanContentHeightMath.TreeNodeHeight(rootB, 0, false, null) +
                PlanContentHeightMath.TreeNodeHeight(rootC, 0, false, null) +
                2 * PlanContentHeightMath.MultiRootDividerHeight;
            Assert.Equal(expected, PlanContentHeightMath.MultiRootTreeFlowHeight(roots, null));
        }

        [Fact]
        public void MultiRootTreeFlowHeight_RespectsPerNodeExpansionOverrides()
        {
            var rootA = Node(1, children: new List<CraftingTreeNode> { Node(2) });
            var rootB = Node(3, children: new List<CraftingTreeNode> { Node(4) });
            var roots = new List<CraftingTreeNode> { rootA, rootB };
            var overrides = new Dictionary<int, bool> { { 1, false } }; // collapse root A only

            int expected =
                PlanContentHeightMath.CTableHeaderRowHeight +
                PlanContentHeightMath.TreeRowHeight + // rootA collapsed - own row only
                PlanContentHeightMath.MultiRootDividerHeight +
                (PlanContentHeightMath.TreeRowHeight + PlanContentHeightMath.TreeRowHeight); // rootB expanded (default)
            Assert.Equal(expected, PlanContentHeightMath.MultiRootTreeFlowHeight(roots, overrides));
        }

        // Three tests formerly here (under a "SummaryBodyHeight +
        // MultiItemNote" / "Multi-item batch sell-side economics
        //" heading: Summary_MultiItemNoteRow_
        // AddsFallbackTextRowHeight, Summary_NoMultiItemNoteRow_
        // UnaffectedByNewBranch, Summary_MultiItemFourCoinRowsPlusNoteRow_
        // StillOneCostTileRowHeight) asserted
        // PlanContentHeightMath.SummaryBodyHeight's shape via
        // PlanRowType.CoinTotal. Deleted as dead code (KNOWN-ISSUES #46,
        // closed under the high-evidence-zone policy - see
        // docs/KNOWN-ISSUES.md#policy-high-evidence-zones): CoinTotal was
        // never emitted by PlanViewModelBuilder, and SummaryBodyHeight
        // was unreachable
        // for a real Summary section once the redesign routed
        // PlanSectionType.Summary to SummarySectionLayoutMath.BodyHeight
        // instead. Deleted together with the enum member and the method
        // (see Models/PlanViewModel.cs, Services/PlanContentHeightMath.cs).
    }
}
