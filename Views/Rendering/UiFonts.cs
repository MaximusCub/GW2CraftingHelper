using Blish_HUD;
using MonoGame.Extended.BitmapFonts;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// The four type sizes the module draws in, named by ROLE rather than
    /// by point size. Every view and renderer resolves its font through
    /// here, so <c>GameService.Content.DefaultFontNN</c> appears nowhere
    /// under Views/ and a size decision is one edit rather than sixty.
    ///
    /// <para>
    /// The maintainer's field-test bump raised <see cref="Body"/> from
    /// DefaultFont14 to DefaultFont16 and <see cref="Caption"/> from
    /// DefaultFont12 to DefaultFont14. The measured Menomonia metrics
    /// behind every constant that had to move with it - line heights
    /// 13/18/20 at 12/14/16, and the ~1.11x width factor between 14 and
    /// 16 on real strings - are in
    /// docs/research/minimum-window-width.md.
    /// </para>
    ///
    /// <para>
    /// Blish's own Label default is DefaultFont14, so a Label this module
    /// builds without an explicit Font renders one step below Body. Every
    /// label site therefore sets one. Four control types are excluded and
    /// stay at Blish's own default: Checkbox and StandardButton (which
    /// FeedbackButton derives from) expose no Font property at all, and
    /// TextBox and Dropdown have internal padding Blish authors against
    /// DefaultFont14 while holding typed values rather than module prose.
    /// Anything MEASURING one of those four measures in
    /// <see cref="Caption"/>, which is the size they actually paint - and
    /// so does anything sizing a tooltip Blish renders itself
    /// (Services.TooltipTextFormat).
    /// </para>
    /// </summary>
    internal static class UiFonts
    {
        /// <summary>Row text, table cells, tooltips - the module's prose.</summary>
        internal static BitmapFont Body => GameService.Content.DefaultFont16;

        /// <summary>Sublabels, pills, tags, footnotes - one step under Body.</summary>
        internal static BitmapFont Caption => GameService.Content.DefaultFont14;

        /// <summary>Section headers. Not part of the body bump.</summary>
        internal static BitmapFont Title => GameService.Content.DefaultFont18;

        /// <summary>The plan title. Not part of the body bump.</summary>
        internal static BitmapFont Display => GameService.Content.DefaultFont32;
    }
}
