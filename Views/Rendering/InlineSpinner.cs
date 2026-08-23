using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// Blish's own module-load spinner, used inline in this module's status
    /// rows in place of the rotating ASCII glyph the strips used to append
    /// to their text.
    /// <para>
    /// Measured from the vendored Blish HUD 1.3.0 binary
    /// (packages/BlishHUD.1.3.0/lib/net472/"Blish HUD.exe", decompiled with
    /// ilspycmd): <c>Blish_HUD.Controls.LoadingSpinner</c> is a plain
    /// public Control with a parameterless constructor whose only body is
    /// Size = 64x64, and whose Paint hands its own bounds straight to
    /// <c>LoadingSpinnerUtil.DrawLoadingSpinner</c>. That helper draws one
    /// 64x64 source frame of the "spinner-atlas" texture (4096x64 in
    /// ref.dat, i.e. 64 frames) into whatever destination bounds it is
    /// given, so the control scales to any size. The frame index is
    /// <c>GameService.Overlay.CurrentGameTime.TotalGameTime.TotalSeconds *
    /// 21.333 % 64</c> - global game time, not per-control state, so the
    /// animation costs us no ticker and starts mid-cycle rather than at
    /// frame 0. Only the default size needed changing: 64x64 is taller than
    /// either status row here.
    /// </para>
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
                Parent = parent
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
            if (spinner == null || label == null) return;

            var placement = InlineSpinnerLayout.Place(
                label.Location.X, label.Location.Y, label.Width, label.Height, spinner.Width, gap);
            spinner.Location = new Point(placement.X, placement.Y);
        }
    }
}
