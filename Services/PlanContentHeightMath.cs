using System;
using System.Collections.Generic;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure content-height arithmetic (Blish-free, unit-testable) for the
    /// plan view's collapsible section bodies and recipe-tree child
    /// containers. M33 C2a directive A: these containers used to rely on
    /// Blish's FlowPanel HeightSizingMode.AutoSize, which only converges
    /// one nested level per real engine frame (Container.DoUpdate sizes a
    /// container from its children's CURRENT bounds before recursing into
    /// those children's own Update for that same frame) - the root cause of
    /// KNOWN-ISSUES #12/#14's multi-frame flash/stutter window. Every row
    /// height in the plan view is a fixed constant (no text wrapping
    /// anywhere in the file - only single-line ellipsis truncation), so the
    /// total height of any section body or tree subtree is knowable
    /// synchronously from row counts/types and expansion state alone, with
    /// no need to wait for Blish layout to converge.
    /// CraftingPlanView uses these SAME constants both to size the
    /// AutoSize-replacement containers explicitly and to size the
    /// individual row Panels it creates, so the two paths cannot drift
    /// apart - mirrors ShoppingColumnMath's "one source of truth" shape.
    /// </summary>
    public static class PlanContentHeightMath
    {
        // --- Section row-height constants (mirrored 1:1 by CraftingPlanView's
        // row builders - CreateUsedMaterialRow, CreateShoppingRow, etc. use
        // these same constants rather than their own local copies). ---
        public const int UsedMaterialRowHeight = 36;
        public const int ShoppingHeaderRowHeight = 22;
        public const int ShoppingRowHeight = 36;
        public const int CraftStepRowHeight = 44;
        public const int CTableHeaderRowHeight = 26;
        public const int DisciplineRowHeight = 32;
        public const int RecipeRowHeightNoSublabel = 32;
        public const int RecipeRowHeightWithSublabel = 44;
        public const int CostTileRowHeight = 56;
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
                case PlanSectionType.Summary:
                    return SummaryBodyHeight(rows);
                case PlanSectionType.UsedMaterials:
                    return rows.Count * UsedMaterialRowHeight;
                case PlanSectionType.ShoppingList:
                    return ShoppingHeaderRowHeight + rows.Count * ShoppingRowHeight;
                case PlanSectionType.CraftingSteps:
                    return CraftingStepsBodyHeight(rows);
                case PlanSectionType.RequiredDisciplines:
                    return CTableHeaderRowHeight + rows.Count * DisciplineRowHeight;
                case PlanSectionType.RequiredRecipes:
                    return CTableHeaderRowHeight + RecipeRowsHeight(rows);
                default:
                    // Defensive fallback mirrors CreateCollapsibleSection's own
                    // default branch (CreateTextRow, one fixed-height row per
                    // PlanRowViewModel).
                    return rows.Count * FallbackTextRowHeight;
            }
        }

        /// <summary>
        /// M34-B1 #3: a Crafting Steps section can now mix numbered
        /// CraftStep rows (CraftStepRowHeight, via CreateCraftStepRow) with
        /// plain TimegatedNotice info rows (FallbackTextRowHeight, via the
        /// shared CreateTextRow helper - see CraftingPlanView.
        /// CreateCraftingStepsBody), so height is summed per-row rather than
        /// assumed uniform.
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

        private static int SummaryBodyHeight(IReadOnlyList<PlanRowViewModel> rows)
        {
            bool hasCoinRow = false;
            int currencyRowCount = 0;
            foreach (var row in rows)
            {
                if (row.RowType == PlanRowType.CoinTotal) hasCoinRow = true;
                else currencyRowCount++;
            }
            return (hasCoinRow ? CostTileRowHeight : 0) + currencyRowCount * CurrencyRowHeight;
        }

        private static int RecipeRowsHeight(IReadOnlyList<PlanRowViewModel> rows)
        {
            int height = 0;
            foreach (var row in rows)
            {
                height += string.IsNullOrEmpty(row.Sublabel) ? RecipeRowHeightNoSublabel : RecipeRowHeightWithSublabel;
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
            if (node == null) return 0;
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
            if (node?.Children == null || node.Children.Count == 0) return 0;
            if (!IsNodeExpanded(node.NodeId, depth, dimmed, expansionOverrides)) return 0;

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
            if (children == null) return 0;
            int total = 0;
            foreach (var child in children)
            {
                total += TreeNodeHeight(child, childDepth, childDimmed, expansionOverrides);
            }
            return total;
        }
    }
}
