using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The Recipe Tree toolbar row's RIGHT-anchored button cluster, and the
    /// x the left cluster (the state chips) has to stop short of.
    ///
    /// <para>
    /// The row has two clusters that share one width. The chips negotiate
    /// through <see cref="TreeChipStripLayout.Fit"/>; the number they
    /// negotiate AGAINST is this cluster's total width, which is why the
    /// buttons' widths and gaps live here rather than as literals in the
    /// view's placement calls. A width that can only be read off a
    /// PlaceRight argument is a width no test can assert the boundary
    /// cases against without re-typing it, and a re-typed width is one a
    /// later rename silently invalidates.
    /// </para>
    ///
    /// <para>
    /// Invariant: <see cref="RightButtons"/> is exactly the set of buttons
    /// the row places, in the right-to-left order it places them.
    /// <see cref="ChipLimitX"/> is only correct while that holds. The view
    /// clamps its chip limit to the buttons it actually placed as well, so
    /// a slot missing from this list costs the chips room rather than
    /// putting them on top of a live click target.
    /// </para>
    /// </summary>
    internal static class TreeToolbarRowLayout
    {
        /// <summary>Gap between two buttons in the same group.</summary>
        public const int TreeToolbarButtonGap = 4;

        /// <summary>
        /// Separates the three plan-mutating presets from the two view-only
        /// actions, and the whole cluster from the chip strip. Wider than
        /// <see cref="TreeToolbarButtonGap"/> on purpose: "Buy All" re-solves
        /// the whole plan and "Expand All" only opens branches, and sitting
        /// them 4px apart in one undifferentiated run invited exactly the
        /// misclick that costs a set of manual overrides.
        /// </summary>
        public const int GroupGap = 20;

        /// <summary>
        /// One button's horizontal footprint: its own width and the gap left
        /// BEFORE the next button placed, which lands to its left.
        /// </summary>
        public readonly struct ButtonSlot
        {
            public readonly int Width;
            public readonly int GapToLeft;

            public ButtonSlot(int width, int gapToLeft)
            {
                Width = width;
                GapToLeft = gapToLeft;
            }
        }

        public static readonly ButtonSlot CollapseAll = new ButtonSlot(96, TreeToolbarButtonGap);
        public static readonly ButtonSlot ExpandAll = new ButtonSlot(92, GroupGap);
        public static readonly ButtonSlot BuyAll = new ButtonSlot(70, TreeToolbarButtonGap);
        public static readonly ButtonSlot CraftAll = new ButtonSlot(76, TreeToolbarButtonGap);
        public static readonly ButtonSlot BestPath = new ButtonSlot(80, 0);

        /// <summary>
        /// Every button the row anchors on its right, right to left. The
        /// view places these same slots; adding a button to the row means
        /// adding it here.
        /// </summary>
        public static readonly IReadOnlyList<ButtonSlot> RightButtons =
            new[] { CollapseAll, ExpandAll, BuyAll, CraftAll, BestPath };

        /// <summary>
        /// The two state chips' clear buttons. Fixed, like the action
        /// buttons' widths; only the count labels beside them are measured
        /// at runtime, because only their text changes.
        /// </summary>
        public const int ClearOverridesButtonWidth = 124;

        /// <summary>See <see cref="ClearOverridesButtonWidth"/>.</summary>
        public const int ClearIgnoredButtonWidth = 110;

        /// <summary>
        /// Width the cluster occupies at the row's right edge, including the
        /// trailing <see cref="WindowSizing.RightEdgePadding"/> it stands off
        /// that edge by.
        /// </summary>
        public static readonly int RightButtonClusterWidth = ClusterWidth();

        /// <summary>
        /// Rightmost x the chip strip may reach in a row of the given width:
        /// the cluster's left edge, less the gap that makes the two clusters
        /// read apart rather than merely not overlap.
        /// </summary>
        public static int ChipLimitX(int rowWidth)
        {
            return rowWidth - RightButtonClusterWidth - GroupGap;
        }

        private static int ClusterWidth()
        {
            int total = WindowSizing.RightEdgePadding;
            foreach (var slot in RightButtons)
            {
                total += slot.Width + slot.GapToLeft;
            }

            return total;
        }
    }
}
