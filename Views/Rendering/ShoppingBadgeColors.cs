using Microsoft.Xna.Framework;
using TaimisToolbench.Models;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// Chrome for the Shopping List's source badges, now that they are an
    /// aligned column rather than a tag glued to the name.
    ///
    /// <para>
    /// Every badge used to render in <see cref="PillKind.Locked"/>'s
    /// recessed grey, so the column said WHICH source only to a reader who
    /// stopped to read four capital letters on every row. Two hues fix
    /// that without spending the accent budget: the majority rows stay
    /// neutral (accents mean nothing if the common case has one), and only
    /// the two classes that are actually a different KIND of action get a
    /// colour.
    /// </para>
    ///
    /// <para>
    /// Deliberately NOT an arm of <see cref="PillColors"/>. That switch is
    /// the recipe tree's decision vocabulary - green means selected, blue
    /// owned, amber ignore-active - and none of those meanings is "go to a
    /// vendor". Reusing one would dilute a vocabulary the tree depends on;
    /// these are non-interactive chrome that shares only the pill's shape.
    /// </para>
    /// </summary>
    internal static class ShoppingBadgeColors
    {
        /// <summary>
        /// The "go somewhere in the world and buy it" class. Teal is the
        /// one hue with no existing meaning anywhere in the module, which
        /// is exactly why it was available.
        /// </summary>
        private static readonly Color VendorBorder = new Color(46, 139, 132); // #2E8B84

        /// <summary>
        /// The warning class - the plan cannot price or source this row.
        /// Darkened out of the Missing!-red family the Required Recipes
        /// status column already uses (#FF6464), so red keeps meaning
        /// "problem" everywhere in the view rather than meaning two things.
        /// Shared with the unpriceable dash on the same row, so "no source"
        /// and "no price" read as one statement instead of two marks.
        /// </summary>
        internal static readonly Color UnknownBorder = new Color(178, 74, 74); // #B24A4A

        /// <summary>
        /// Border and fill for one row's badge. TP and CURRENCY keep
        /// Locked's chrome exactly - TP because it is the majority row, and
        /// CURRENCY because the recipe tree's own CURRENCY pill is Locked
        /// chrome and one meaning should not have two looks.
        /// </summary>
        internal static void For(PlanRowType rowType, out Color border, out Color fill)
        {
            switch (rowType)
            {
                case PlanRowType.ShoppingVendor:
                    border = VendorBorder;
                    fill = border * 0.15f;
                    return;
                case PlanRowType.ShoppingUnknown:
                    border = UnknownBorder;
                    fill = border * 0.15f;
                    return;
                default:
                    PillColors.GetPillColors(PillKind.Locked, false, out border, out fill);
                    return;
            }
        }
    }
}
