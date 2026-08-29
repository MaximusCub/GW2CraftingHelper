using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// Blish's own module-load spinner, used inline in this module's status
    /// rows in place of the rotating ASCII glyph the strips used to append
    /// to their text.
    /// <para>
    /// Measured from the vendored Blish HUD 1.3.0 binary:
    /// <c>LoadingSpinner</c> hands its own bounds straight to
    /// <c>LoadingSpinnerUtil.DrawLoadingSpinner</c>, which draws one 64x64
    /// source frame into whatever destination bounds it is given - so the
    /// control scales to any size, and only its 64x64 default needed
    /// changing (taller than either status row here). The frame index comes
    /// from global game time, not per-control state, so the animation costs
    /// no ticker and starts mid-cycle rather than at frame 0.
    /// </para>
    /// docs/ARCHITECTURE.md, "Views: relocated design narrative".
    /// </summary>
    internal static class InlineSpinner
    {
        /// <summary>
        /// Creates a hidden spinner of the given square size parented to
        /// <paramref name="parent"/>. Callers show it on the same condition
        /// their old ASCII glyph appeared on and position it with
        /// <see cref="PlaceAfter"/>.
        /// </summary>
        internal static LoadingSpinner Create(Container parent, int size)
        {
            return new LoadingSpinner()
            {
                Size = new Point(size, size),
                Visible = false,
                Parent = parent,
            };
        }

        /// <summary>
        /// Re-anchors <paramref name="spinner"/> to the right of
        /// <paramref name="label"/>. Must be called after every write to
        /// the label's Text or Location: a Blish Label with AutoSizeWidth
        /// recalculates its own Size synchronously inside the Text setter,
        /// so the width read here is always the one just laid out.
        /// </summary>
        internal static void PlaceAfter(LoadingSpinner spinner, Label label, int gap)
        {
            if (spinner == null || label == null)
            {
                return;
            }

            var placement = InlineSpinnerLayout.Place(
                label.Location.X, label.Location.Y, label.Width, label.Height, spinner.Width, gap);
            spinner.Location = new Point(placement.X, placement.Y);
        }
    }
}
