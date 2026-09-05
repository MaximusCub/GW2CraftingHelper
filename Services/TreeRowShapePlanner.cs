using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// One tree row's shape: where its columns start, which of its optional
    /// parts exist, and what its caret says. Decided here rather than in the
    /// renderer, per CONTRIBUTING.md's STANDING RULE - the same split
    /// DecisionPillPlanner and TreeRowTooltipComposer already make. TreeSectionController turns the answer into Blish controls and
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

        // Tier 2 of the module's two-tier icon system: tree
        // rows carry in-game bag-sidebar-sized item art, like every other
        // row-level icon in the Crafting Plan tab.
        public const int IconSize = ItemIconTiers.BagSidebarIconSize;
        public const int IconBorder = PlanContentHeightMath.RowIconBorder;
        public const int IconFrameSize = IconSize + IconBorder * 2;
        public const int NameGap = 6;

        /// <summary>
        /// The name column's x measured from the row's own indent, so the
        /// column header can sit above a depth-0 row's name without
        /// restating the sum.
        /// </summary>
        public const int NameColumnOffset = CaretColumnWidth + IconFrameSize + NameGap;

        // The ASCII a caret DEGRADES to. What is normally drawn is the
        // module's own filled caret from ref/glyphs.fnt - see
        // UiGlyphs.ExpandCaret, which is where the renderer asks. Neither
        // U+25BC nor U+25B6 renders in Blish's font, which is why these two
        // are a lowercase letter and a greater-than sign.
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

            return new TreeRowShape(
                indent: indent,
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
        public readonly int IconX;
        public readonly int NameX;
        public readonly bool HasChildren;
        public readonly bool IsExpanded;

        /// <summary>
        /// Null when the row has no children to expand; otherwise the ASCII
        /// this row's caret degrades to. The renderer draws the shipped
        /// glyph font's filled caret instead whenever the atlas loaded, off
        /// <see cref="IsExpanded"/> - a BitmapFont is not something a
        /// Blish-free planner can see.
        /// </summary>
        public readonly string CaretGlyph;

        /// <summary>Empty when the row shows no quantity.</summary>
        public readonly string QuantityPrefix;

        public readonly bool ChildDimmed;

        public TreeRowShape(
            int indent, int iconX, int nameX, bool hasChildren, bool isExpanded,
            string caretGlyph, string quantityPrefix, bool childDimmed)
        {
            Indent = indent;
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
