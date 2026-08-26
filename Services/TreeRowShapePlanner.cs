using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// One tree row's shape: where its columns start, which of its optional
    /// parts exist, and what its caret says. Decided here rather than in the
    /// renderer, per docs/ARCHITECTURE.md section 5's STANDING RULE - the
    /// same split DecisionPillPlanner and TreeRowTooltipComposer already
    /// make. TreeSectionController turns the answer into Blish controls and
    /// owns everything this cannot decide without a font (the ellipsized
    /// name, the quantity prefix's pixel width) or a palette (the rarity and
    /// dimming colors).
    /// </summary>
    internal static class TreeRowShapePlanner
    {
        // The fixed tree-row column grid (spec: "the key gw2e table look" -
        // every row aligns regardless of depth). Right-anchored columns
        // (pills, cost) sit at the same x on every row and belong to
        // PlanRelayoutMath; only the left side below shifts with indent.
        public const int IndentPerDepth = 24;
        public const int CaretColumnWidth = 18;
        public const int IconSize = 32;
        public const int IconBorder = 1;
        public const int IconFrameSize = IconSize + IconBorder * 2;
        public const int NameGap = 6;

        /// <summary>
        /// The name column's x measured from the row's own indent, so the
        /// column header can sit above a depth-0 row's name without
        /// restating the sum.
        /// </summary>
        public const int NameColumnOffset = CaretColumnWidth + IconFrameSize + NameGap;

        // Left-indent rule down a dimmed subtree: 2px wide (1px is not
        // guaranteed a physical scanline under Blish's non-integer UI scale
        // - see LabelHelpers.CreateRowDivider), drawn in every dimmed row's
        // own indent channel so consecutive rows at the same depth join into
        // one continuous line.
        public const int DimmedRuleWidth = 2;
        public const int DimmedRuleOffset = 8;

        // ASCII, matching the section headers - the U+25BC/U+25B6 triangles
        // do not render in Blish's font.
        public const string ExpandedCaret = "v";
        public const string CollapsedCaret = ">";

        public static TreeRowShape Plan(
            CraftingTreeNode node, int depth, bool dimmed, IReadOnlyDictionary<int, bool> expansionOverrides)
        {
            int indent = depth * IndentPerDepth;
            bool hasChildren = node.Children.Count > 0;

            // Delegated, not a hand-duplicated ternary, so this decision and
            // RefreshTreeContainerHeights' height arithmetic share one
            // formula and cannot silently desync.
            bool isExpanded = PlanContentHeightMath.IsNodeExpanded(node.NodeId, depth, dimmed, expansionOverrides);

            int ruleX = indent - DimmedRuleOffset;
            if (ruleX < 0)
            {
                ruleX = 0;
            }

            return new TreeRowShape(
                indent: indent,
                dimmedRuleX: ruleX,
                iconX: indent + CaretColumnWidth,
                nameX: indent + NameColumnOffset,
                hasChildren: hasChildren,
                isExpanded: isExpanded,
                caretGlyph: hasChildren ? (isExpanded ? ExpandedCaret : CollapsedCaret) : null,
                quantityPrefix: node.Quantity > 0 ? $"{node.Quantity}x " : string.Empty,
                // The dimmed flag does not stack: children of a non-Craft
                // decision are this module's own informational "what it
                // would cost to craft instead" reference branch, and a
                // branch already inside one stays exactly as dim.
                childDimmed: dimmed || node.Decision != CraftingDecision.Craft);
        }
    }

    internal readonly struct TreeRowShape
    {
        public readonly int Indent;

        /// <summary>
        /// x of the dimmed subtree's left rule, clamped at 0 so a depth-0
        /// dimmed row draws it flush rather than off the panel.
        /// </summary>
        public readonly int DimmedRuleX;

        public readonly int IconX;
        public readonly int NameX;
        public readonly bool HasChildren;
        public readonly bool IsExpanded;

        /// <summary>Null when the row has no children to expand.</summary>
        public readonly string CaretGlyph;

        /// <summary>Empty when the row shows no quantity.</summary>
        public readonly string QuantityPrefix;

        public readonly bool ChildDimmed;

        public TreeRowShape(
            int indent, int dimmedRuleX, int iconX, int nameX, bool hasChildren, bool isExpanded,
            string caretGlyph, string quantityPrefix, bool childDimmed)
        {
            Indent = indent;
            DimmedRuleX = dimmedRuleX;
            IconX = iconX;
            NameX = nameX;
            HasChildren = hasChildren;
            IsExpanded = isExpanded;
            CaretGlyph = caretGlyph;
            QuantityPrefix = quantityPrefix;
            ChildDimmed = childDimmed;
        }
    }
}
