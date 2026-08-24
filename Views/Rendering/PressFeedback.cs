using Blish_HUD.Controls;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// Press feedback for this module's own click targets: the control dims
    /// while the left button is held and plays Blish's UI click sound the
    /// moment the press lands.
    /// <para>
    /// The dim is applied to <c>Control.Opacity</c>, not to the target's
    /// background or text color, and that is the point. Every clickable
    /// thing here already owns its own hover vocabulary written to a
    /// different property - a decision pill swaps BackgroundColor, a
    /// sortable header swaps TextColor, a tree row and a section header each
    /// swap to a different translucent wash - and a helper that wrote to
    /// those same properties would have to capture and restore a resting
    /// value that the site's own MouseLeft handler is also writing, making
    /// correctness depend on which handler was subscribed first. Opacity is
    /// touched by nothing else on any of these controls, so this helper
    /// composes with all four schemes and restores the control's OWN
    /// resting opacity rather than a hardcoded 1f.
    /// </para>
    /// <para>
    /// Measured from the vendored Blish HUD 1.3.0 binary
    /// (packages/BlishHUD.1.3.0/lib/net472/"Blish HUD.exe", decompiled with
    /// ilspycmd): <c>Control.Opacity</c> reaches the GPU through
    /// <c>AbsoluteOpacity()</c>, which every <c>SpriteBatchExtensions.
    /// DrawOnCtrl</c>/<c>DrawStringOnCtrl</c> overload multiplies into its
    /// draw color, and which walks the parent chain - so dimming a panel
    /// dims its label and icon children with it, which is what makes this
    /// legible on a target whose own background is transparent.
    /// </para>
    /// </summary>
    internal static class PressFeedback
    {
        /// <summary>
        /// Multiplier applied to the target's resting opacity while held -
        /// a 20% dim.
        /// </summary>
        internal const float PressedOpacityFactor = 0.8f;

        /// <summary>
        /// Plays Blish's own UI click sound at the user's configured volume
        /// (see <see cref="ClickSound"/> for why not PlaySoundEffectByName).
        /// Reaches only the controls this module wires: Blish's Checkbox and
        /// CornerIcon play the click themselves, ahead of the base call a
        /// subclass would have to skip to silence it, so those stay at
        /// Blish's volume - KNOWN-ISSUES carries the sweep and deferred fix.
        /// </summary>
        internal static void PlayClick()
        {
            ClickSound.Play();
        }

        /// <summary>
        /// Wires press-dim + click sound onto <paramref name="control"/>.
        /// The restore runs on release AND on the mouse leaving, because a
        /// press that drags off the control is delivered MouseLeft and never
        /// a release.
        /// <para>
        /// <paramref name="suppress"/> is for a container whose own click
        /// handler already ignores clicks that landed on a wired child.
        /// Measured: <c>Container.TriggerMouseInput</c> raises the
        /// container's OWN mouse events first and only then walks its
        /// children, so a press inside a wired child reaches BOTH - without
        /// the predicate that is two click sounds and two dimmed controls
        /// for one press. It is evaluated at press time, so a container may
        /// be wired before the child the predicate reads exists.
        /// </para>
        /// </summary>
        internal static void Wire(Control control, Func<bool> suppress = null)
        {
            if (control == null) return;

            float restingOpacity = 1f;
            bool held = false;

            Action release = () =>
            {
                if (!held) return;
                held = false;
                control.Opacity = restingOpacity;
            };

            control.LeftMouseButtonPressed += (_, __) =>
            {
                // Blish raises this before its own Enabled check (only
                // Click is gated on Enabled), so a disabled Generate button
                // would otherwise answer a click it is about to ignore.
                if (held || !control.Enabled) return;
                if (suppress != null && suppress()) return;

                held = true;
                restingOpacity = control.Opacity;
                control.Opacity = restingOpacity * PressedOpacityFactor;
                PlayClick();
            };

            control.LeftMouseButtonReleased += (_, __) => release();
            control.MouseLeft += (_, __) => release();
        }
    }
}
