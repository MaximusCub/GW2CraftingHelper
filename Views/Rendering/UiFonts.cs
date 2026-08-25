using Blish_HUD;
using GW2CraftingHelper.Services;
using MonoGame.Extended.BitmapFonts;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// The type sizes the module draws in, named by ROLE rather than by
    /// point size. Every view and renderer resolves its font through here,
    /// so <c>GameService.Content.DefaultFontNN</c> appears nowhere under
    /// Views/ and a size decision is one edit rather than sixty.
    ///
    /// <para>
    /// The ramp is two reading sizes (<see cref="Caption"/> 14,
    /// <see cref="Body"/> 16) and three emphatic tiers above them
    /// (<see cref="ColumnHeader"/>, <see cref="SectionTitle"/>,
    /// <see cref="Display"/>), with weight doing as much of the work as
    /// size. Which point size each promoted tier sits at is decided in
    /// Services/TypeRampMetrics, beside the measured glyph metrics the
    /// height constants are derived from; this file only turns that
    /// decision into a BitmapFont.
    /// </para>
    ///
    /// <para>
    /// Blish surfaces five sizes as DefaultFontNN properties; every other
    /// size in the installed Menomonia inventory (8-36, bold at 8-24 and
    /// 36) loads through <c>ContentService.GetFont</c>. Two entries of that
    /// inventory are unusable - 18-regular collapses word gaps, and
    /// 22-regular is really a 24 - measured in TypeRampMetrics and
    /// ENFORCED in <see cref="Regular"/>, so a tier seat cannot reach
    /// either by moving a point size.
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

        /// <summary>
        /// Every column header the module draws, and the Total Cost band's
        /// tile captions. Bold, because headers used to be the same size and
        /// weight as the rows under them.
        /// </summary>
        internal static BitmapFont ColumnHeader =>
            Bold(TypeRampMetrics.ColumnHeaderPointSize);

        /// <summary>Every section title in the module.</summary>
        internal static BitmapFont SectionTitle =>
            Bold(TypeRampMetrics.SectionTitlePointSize);

        /// <summary>
        /// Every tab's status line. Bold for a measured reason, not a
        /// stylistic one - see TypeRampMetrics on 18-regular's space glyph,
        /// which is why nothing here resolves 18-regular.
        /// </summary>
        internal static BitmapFont Status =>
            Bold(TypeRampMetrics.StatusPointSize);

        /// <summary>
        /// The plan header's " x N needed" suffix: regular weight so it
        /// stays subordinate to the Display title beside it.
        /// </summary>
        internal static BitmapFont SmallHeading =>
            Regular(TypeRampMetrics.SmallHeadingPointSize);

        /// <summary>
        /// The craft-step number badge: the bold twin of
        /// <see cref="SmallHeading"/>, digits only.
        /// </summary>
        internal static BitmapFont SmallHeadingBold =>
            Bold(TypeRampMetrics.SmallHeadingPointSize);

        /// <summary>The plan title. No bold exists at this size.</summary>
        internal static BitmapFont Display => GameService.Content.DefaultFont32;

        private static BitmapFont Bold(int pointSize)
        {
            return GameService.Content.GetFont(
                ContentService.FontFace.Menomonia, SizeOf(pointSize), ContentService.FontStyle.Bold);
        }

        /// <summary>
        /// Regular weight, at the promoted sizes that HAVE a usable
        /// regular face. The two that do not are refused here rather than
        /// in <see cref="SizeOf"/> because the ban is on the FACE, not on
        /// the size: 18-bold and 22-bold are both fine, and both are
        /// loaded.
        /// <para>
        /// Without this, a tier seat moved from 20 to 18 would turn
        /// <see cref="SmallHeading"/> into 18-regular and render
        /// " x 42 needed" at exactly the collapsed word gaps this ramp
        /// exists to escape - with no build error, no failing test and
        /// nothing on screen to name the cause. See TypeRampMetrics for
        /// both measurements.
        /// </para>
        /// </summary>
        private static BitmapFont Regular(int pointSize)
        {
            if (!TypeRampMetrics.HasUsableRegularFace(pointSize))
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(pointSize), pointSize,
                    "No usable Menomonia regular face at this size: 18-regular's space glyph "
                        + "advances 4px, and 22-regular is metrically a 24. Use the bold face.");
            }

            return GameService.Content.GetFont(
                ContentService.FontFace.Menomonia, SizeOf(pointSize), ContentService.FontStyle.Regular);
        }

        /// <summary>
        /// The four point sizes the ramp is allowed to name, and nothing
        /// else: an unmapped size is a size TypeRampMetrics has no measured
        /// ink for, so the height constants derived from it would be
        /// guesses. Fail loudly at the seam rather than silently render at
        /// a size no constant was sized for. Weight is <see cref="Bold"/>'s
        /// and <see cref="Regular"/>'s own concern - only one of the two
        /// can load all four.
        /// </summary>
        private static ContentService.FontSize SizeOf(int pointSize)
        {
            switch (pointSize)
            {
                case 18: return ContentService.FontSize.Size18;
                case 20: return ContentService.FontSize.Size20;
                case 22: return ContentService.FontSize.Size22;
                case 24: return ContentService.FontSize.Size24;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(pointSize), pointSize, "No measured TypeRampMetrics ink for this size.");
            }
        }
    }
}
