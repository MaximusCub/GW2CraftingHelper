using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure content-height arithmetic (Blish-free, unit-testable) for the
    /// plan view's collapsible section bodies and recipe-tree child
    /// containers. These containers used to rely on
    /// Blish's FlowPanel HeightSizingMode.AutoSize, which only converges
    /// one nested level per real engine frame (Container.DoUpdate sizes a
    /// container from its children's CURRENT bounds before recursing into
    /// those children's own Update for that same frame) - the root cause of
    /// KNOWN-ISSUES #12/#14's multi-frame flash/stutter window. Every row
    /// height in the plan view is a fixed constant, so the
    /// total height of any section body or tree subtree is knowable
    /// synchronously from row counts/types and expansion state alone, with
    /// no need to wait for Blish layout to converge.
    /// Every row height is still a fixed constant after the Plan Notes
    /// section learned to wrap: a wrapped note renders one
    /// FallbackTextRowHeight row per LINE, so only the row COUNT became
    /// width-dependent, which is why Notes is sized by
    /// NotesSectionLayoutMath/its renderer rather than by SectionBodyHeight
    /// below (the same split Summary already has).
    /// CraftingPlanView uses these SAME constants both to size the
    /// AutoSize-replacement containers explicitly and to size the
    /// individual row Panels it creates, so the two paths cannot drift
    /// apart - mirrors ShoppingColumnMath's "one source of truth" shape.
    /// <para>See docs/ARCHITECTURE.md section 4.</para>
    /// </summary>
    public static class PlanContentHeightMath
    {
        // --- Section row-height constants (mirrored 1:1 by CraftingPlanView's
        // row builders - CreateUsedMaterialRow, CreateShoppingRow, etc. use
        // these same constants rather than their own local copies). ---
        //
        // Every height below was re-derived against the +2pt body font
        // (Views/Rendering/UiFonts): measured Menomonia line heights are 13
        // at 12pt, 18 at 14pt and 20 at 16pt, and the lowest ASCII ink sits
        // 1px past the line box at 14pt and 16pt, 3px past it at 12pt. A
        // row keeps its height only where that ink still clears whatever
        // sits under it (the 2px divider, or the row's own bottom edge).
        //
        // Unchanged and why: the three 36px rows and TreeRowHeight are
        // ICON-driven, not text-driven - a 34px rarity frame at y=0 plus a
        // 2px divider already exceeds the tallest text run in them.
        // CraftStepRowHeight's body text was already Font16 before the
        // bump, so only its Font12 -> Font14 sublabel moved, and that
        // sublabel's new ink (y=16 + 19 = 35) still clears its divider at
        // 41. The two 28px rows put a single line at y=4 (ink 25) and y=7
        // (ink 26) with no divider beneath either.
        public const int UsedMaterialRowHeight = 36;
        public const int ShoppingRowHeight = 36;
        public const int CraftStepRowHeight = 44;

        // 32, not the 28 a Body-16 header band needed: column headers moved
        // to the ColumnHeader tier (TypeRampMetrics.ColumnHeaderInk), whose
        // lowest ink is 26 rather than 21. CTableHeaderLabelY 4 reproduces
        // the band the 16pt header drew - cap top 8px down, ink bottom 2px
        // clear of the band's own bottom edge - at the taller font.
        public const int CTableHeaderRowHeight = 32;

        // Baseline y of every column-header label inside that band. Lives
        // here rather than with the chrome that draws it
        // (Views/Rendering/TableHeaderStyle, which aliases this) because it
        // is half of the arithmetic above: a label y and a band height that
        // move independently are how a header's descenders end up on the
        // row under them.
        public const int CTableHeaderLabelY = 4;

        // --- Section header band (drawn by CraftingPlanView.
        // CreateSectionHeader, which aliases all three). ---

        // 38, not the 32 an 18pt title needed: section titles moved to the
        // SectionTitle tier (TypeRampMetrics.SectionTitleInk), lowest ink
        // 30 rather than 23. The divider is bottom-anchored at
        // height - 3, so its top is y=35 and the title's ink bottom
        // (SectionHeaderTitleY 3 + 30 = 33) clears it by 2px - the
        // clearance LabelHelpers.CreateRowDivider's scissor-defect note
        // requires, never 1px.
        public const int SectionHeaderRowHeight = 38;

        public const int SectionHeaderTitleY = 3;

        // The caret is Body, not SectionTitle - two fonts on one reading
        // line, so it is BASELINE-aligned to the title rather than
        // top-aligned or centred in the band, with the same 1px optical
        // lift the 18pt header gave it. Ink bottom 10 + 21 = 31, also clear
        // of the divider top at 35.
        public const int SectionHeaderCaretY = 10;

        // 36, not 32: this row's two labels sit at y=7 and y=9, whose
        // Font16/Font14 ink (28) landed on the 32px row's divider top (29).
        // 36 is the height every other single-line table row in this file
        // already uses, and LabelHelpers.CreateRowDivider's scale
        // simulation lists it as immune rather than merely clear.
        public const int DisciplineRowHeight = 36;

        // 36 = 34px icon (y=0) + 2px divider, an exact, non-overlapping
        // fit, mirroring UsedMaterialRowHeight/ShoppingRowHeight.
        //
        // EVERY recipe row, since the discipline became a column
        // (Services/RecipesColumnMath) rather than a second line under the
        // name. The 48px twin this section used to need for a sublabel row
        // is gone with the sublabel, so the section is shorter than it was
        // despite the taller chrome above it.
        public const int RecipeRowHeight = 36;

        // 58, not 56: the cost tiles' captions moved to the ColumnHeader
        // tier, whose 25px line box puts the caption block's bottom at
        // CostTileCaptionY 4 + 25 + 2 = 31, one past the y=30 a 56px band
        // bottom-anchored its 20px coin run at. 58 restores the clearance
        // (amount y = 58 - 6 - 20 = 32).
        public const int CostTileRowHeight = 58;

        // Caption y and amount bottom pad of an UNHIGHLIGHTED formula band
        // (the profit band; a highlighted one uses
        // SummarySectionLayoutMath's box-derived pair instead). Here rather
        // than with the renderer that draws them because they are the other
        // two thirds of CostTileRowHeight's arithmetic - the caption's line
        // box has to end above the amount run the band bottom-anchors.
        public const int CostTileCaptionY = 4;
        public const int CostTileAmountBottomPad = 6;

        // The Summary section's COST formula band is no longer a taller
        // twin of this row: its result tile shares the band's one amount
        // font and is highlighted with a tinted box instead, so its height
        // is the box's own geometry - Services/SummarySectionLayoutMath.
        // CostBandHeight. The profit band still uses CostTileRowHeight.
        public const int CurrencyRowHeight = 28;
        public const int FallbackTextRowHeight = 28;
        public const int TreeRowHeight = 40;

        /// <summary>
        /// Total height of a collapsible section's AutoSize-replacement
        /// content FlowPanel, computed purely from its rows (no Blish
        /// objects touched). CraftingPlanView.CreateCollapsibleSection
        /// assigns this to contentFlow.Height synchronously right after
        /// dispatching to the section's CreateXBody builder, so the
        /// container's true height is valid before the next paint - no
        /// multi-frame AutoSize convergence window remains to race.
        /// </summary>
        public static int SectionBodyHeight(PlanSectionType sectionType, IReadOnlyList<PlanRowViewModel> rows)
        {
            rows = rows ?? Array.Empty<PlanRowViewModel>();
            switch (sectionType)
            {
                // Both of these gained a CTableHeaderRowHeight band in
                // audit batch J's chrome unification: Used Materials had no
                // header at all, and the Shopping List's was its own 22px
                // unbanded style. Counted unconditionally, exactly as the
                // two c-tables below are, because all four renderers emit
                // the header before looking at the row count.
                case PlanSectionType.UsedMaterials:
                    return CTableHeaderRowHeight + rows.Count * UsedMaterialRowHeight;
                case PlanSectionType.ShoppingList:
                    return CTableHeaderRowHeight + rows.Count * ShoppingRowHeight;
                case PlanSectionType.CraftingSteps:
                    return CraftingStepsBodyHeight(rows);
                case PlanSectionType.RequiredDisciplines:
                    return CTableHeaderRowHeight + rows.Count * DisciplineRowHeight;
                case PlanSectionType.RequiredRecipes:
                    return CTableHeaderRowHeight + rows.Count * RecipeRowHeight;
                default:
                    // Defensive fallback mirrors CreateCollapsibleSection's own
                    // default branch (CreateTextRow, one fixed-height row per
                    // PlanRowViewModel).
                    return rows.Count * FallbackTextRowHeight;
            }
        }

        /// <summary>
        /// A Crafting Steps section can now mix numbered
        /// CraftStep rows (CraftStepRowHeight, via
        /// Views/Rendering/CraftStepsSectionRenderer.CreateCraftStepRow) with
        /// plain TimegatedNotice info rows (FallbackTextRowHeight, via the
        /// shared Views/Rendering/TextRowRenderer.CreateTextRow helper - see
        /// Views/Rendering/CraftStepsSectionRenderer.Render),
        /// so height is summed per-row rather than assumed uniform.
        /// </summary>
        private static int CraftingStepsBodyHeight(IReadOnlyList<PlanRowViewModel> rows)
        {
            int height = 0;
            foreach (var row in rows)
            {
                height += row.RowType == PlanRowType.TimegatedNotice
                    ? FallbackTextRowHeight
                    : CraftStepRowHeight;
            }

            return height;
        }

        /// <summary>
        /// Whether a tree node is expanded: an explicit user override
        /// (persisted per NodeId across local re-solves) wins; otherwise
        /// falls back to the same default CraftingPlanView.RenderTreeNode
        /// has always used - non-dimmed (real crafting-path) nodes default
        /// open through depth 1, dimmed reference branches always default
        /// closed. Both the view's row builder and this height arithmetic
        /// call this same method so the two can never disagree about which
        /// nodes are expanded.
        /// </summary>
        public static bool IsNodeExpanded(
            int nodeId, int depth, bool dimmed, IReadOnlyDictionary<int, bool> expansionOverrides)
        {
            if (expansionOverrides != null && expansionOverrides.TryGetValue(nodeId, out bool overrideValue))
            {
                return overrideValue;
            }

            return !dimmed && depth < 2;
        }

        /// <summary>
        /// Total height of one tree node's own row plus (if expanded) every
        /// descendant currently expanded beneath it - the value
        /// CraftingPlanView assigns to the Recipe Tree section's top-level
        /// contentFlow.Height (node = TreeRoot, depth = 0, dimmed = false).
        /// </summary>
        public static int TreeNodeHeight(
            CraftingTreeNode node, int depth, bool dimmed, IReadOnlyDictionary<int, bool> expansionOverrides)
        {
            if (node == null)
            {
                return 0;
            }

            return TreeRowHeight + TreeChildFlowHeight(node, depth, dimmed, expansionOverrides);
        }

        /// <summary>
        /// Total height of a single node's OWN childFlow container - the
        /// sum of every child's TreeNodeHeight, or 0 when the node has no
        /// children or is not currently expanded. This is exactly what
        /// CraftingPlanView.RenderTreeNode's childFlow.Height must be set
        /// to right after (lazily or eagerly) populating it.
        /// </summary>
        public static int TreeChildFlowHeight(
            CraftingTreeNode node, int depth, bool dimmed, IReadOnlyDictionary<int, bool> expansionOverrides)
        {
            if (node?.Children == null || node.Children.Count == 0)
            {
                return 0;
            }

            if (!IsNodeExpanded(node.NodeId, depth, dimmed, expansionOverrides))
            {
                return 0;
            }

            bool childDimmed = dimmed || node.Decision != CraftingDecision.Craft;
            return ChildrenHeight(node.Children, depth + 1, childDimmed, expansionOverrides);
        }

        /// <summary>
        /// Sum of TreeNodeHeight over a set of sibling nodes that share the
        /// same depth and dimmed flag. Exposed separately from
        /// TreeChildFlowHeight so a toggle handler that already has a
        /// node's own ChildDimmed cached (RenderTreeNode computes it once
        /// per node and stores it on TreeNodeState) can recompute that
        /// node's childFlow height directly, without re-deriving dimmed
        /// state from scratch.
        /// </summary>
        public static int ChildrenHeight(
            IReadOnlyList<CraftingTreeNode> children, int childDepth, bool childDimmed,
            IReadOnlyDictionary<int, bool> expansionOverrides)
        {
            if (children == null)
            {
                return 0;
            }

            int total = 0;
            foreach (var child in children)
            {
                total += TreeNodeHeight(child, childDepth, childDimmed, expansionOverrides);
            }

            return total;
        }

        // Thin visual gap
        // CraftingPlanView draws between two consecutive top-level trees in
        // a multi-item batch, so N stacked full item trees read as N
        // distinct blocks rather than blending into one continuous list of
        // rows. Never inserted for a single root (roots.Count == 1), which
        // is what keeps that case byte-identical to the single-item height.
        public const int MultiRootDividerHeight = 12;

        /// <summary>
        /// Total height of
        /// the Recipe Tree section's single shared content FlowPanel when
        /// it holds N top-level trees stacked (gw2e's own "N independent
        /// top-level recipe trees" render, its synthetic wrapper node never
        /// surfacing - docs/gw2e-parity-spec.md) rather
        /// than one. Each requested item's own root node already IS its own
        /// full icon/name/quantity/pill/cost row (CraftingTreeNode, same
        /// shape TreeNodeHeight already sizes for a single-item plan) - so
        /// this is simply that same per-root arithmetic summed across every
        /// root, plus one MultiRootDividerHeight gap between each pair of
        /// consecutive roots (never before the first or after the last). A
        /// one-element roots list (the single-item case) has no divider at
        /// all, so a single-item plan differs from
        /// TreeNodeHeight(roots[0], 0, false, ...) by exactly the column
        /// header below.
        /// <para>
        /// One CTableHeaderRowHeight column header
        /// (TreeSectionController.CreateTreeSection builds it into the
        /// same FlowPanel, above every root) precedes the roots whenever
        /// there is a tree at all - the tree's right-hand columns are
        /// unlabelled otherwise, unlike every other c-table in the plan.
        /// An empty/absent roots list renders no tree and therefore no
        /// header either, and still measures 0.
        /// </para>
        /// </summary>
        public static int MultiRootTreeFlowHeight(
            IReadOnlyList<CraftingTreeNode> roots, IReadOnlyDictionary<int, bool> expansionOverrides)
        {
            if (roots == null || roots.Count == 0)
            {
                return 0;
            }

            int total = CTableHeaderRowHeight;
            for (int i = 0; i < roots.Count; i++)
            {
                if (i > 0)
                {
                    total += MultiRootDividerHeight;
                }

                total += TreeNodeHeight(roots[i], 0, false, expansionOverrides);
            }

            return total;
        }
    }
}
