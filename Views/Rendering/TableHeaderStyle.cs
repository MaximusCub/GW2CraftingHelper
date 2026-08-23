using Blish_HUD;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// The one chrome every column-header row in the Crafting Plan tab
    /// draws itself with.
    ///
    /// <para>
    /// <b>Inventory this replaces</b> (audit batch J, L3, re-taken at this
    /// HEAD rather than from the audit's own older reading): three styles
    /// across six tables. Four tables - Required Recipes, Required
    /// Disciplines, the Recipe Tree (banded by batch D) and the Total Cost
    /// section's currency table - drew a dark band with DefaultFont14 white
    /// labels at 26px. The Shopping List drew no band, DefaultFont12 in
    /// #999999, at 22px. Used Materials had a right-hand Amount column and
    /// no header at all.
    /// </para>
    ///
    /// <para>
    /// <b>The choice, and why.</b> The band wins, on three grounds. It is
    /// what four of the five existing headers already do, so unifying the
    /// other way would have rewritten the majority to match the minority.
    /// It is the more recent deliberate decision - batch D introduced the
    /// band for the tree AFTER the Shopping List's lighter treatment
    /// existed. And it is the one that survives this module's own row
    /// chrome: every table row already carries a 2px divider and, in most
    /// tables, an icon, so an unbanded header in a lighter grey reads as a
    /// faint first data row rather than as a header, which is exactly the
    /// complaint about the Shopping List's version.
    /// </para>
    ///
    /// <para>
    /// The cost of the choice, stated plainly: the Shopping List's header
    /// gets heavier (band, Font14, white, four more pixels tall) and Used
    /// Materials gains a header row it did not have, so both sections'
    /// bodies grow - both are paid for in PlanContentHeightMath in the same
    /// change, not left to drift.
    /// </para>
    /// </summary>
    internal static class TableHeaderStyle
    {
        internal static readonly Color BandColor = new Color(35, 35, 35);

        internal static readonly Color LabelColor = Color.White;

        /// <summary>
        /// Height of the band. Aliased to the height-math constant rather
        /// than duplicated: a header that draws at one height and is
        /// measured at another is how a section's rows drift off their
        /// container.
        /// </summary>
        internal const int RowHeight = PlanContentHeightMath.CTableHeaderRowHeight;

        /// <summary>
        /// Baseline y of every header label inside the band.
        /// </summary>
        internal const int LabelY = 5;

        internal static BitmapFont Font => GameService.Content.DefaultFont14;
    }
}
