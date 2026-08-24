using Blish_HUD;
using Blish_HUD.Controls;
using System.Collections.Generic;
using System.Linq;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// Full release of Blish text-input focus.
    /// </summary>
    /// <remarks>
    /// <c>UnsetFocus()</c> is the only API that releases a text box
    /// completely; the <c>Focused</c> property is not. Measured against
    /// Blish HUD 1.3.0: <c>TextInputBase.Focused</c>'s setter assigns
    /// <c>GameService.Input.Keyboard.FocusedControl = this</c> on EVERY
    /// change, a change to false included, so every soft unfocus leaves that
    /// one global slot naming a box that is no longer focused. Blish itself
    /// soft-unfocuses in two places - the click-away handler
    /// (<c>Focused = _mouseOver &amp;&amp; _enabled</c>) and
    /// <c>DisposeControl</c> - and the second runs after
    /// <c>Control.Dispose</c> has already cleared <c>Parent</c>, so a box
    /// disposed while focused leaves the slot holding an orphan whose
    /// <c>GetAncestors()</c> is empty and which KeyboardHandler's
    /// ancestor-visibility sweep can therefore never heal.
    ///
    /// A slot naming one box while another still holds focus is what the
    /// user feels: Escape is consumed clearing the slot instead of the box,
    /// re-clicking the live box cannot repair it (the setter's
    /// change-detection skips the assignment when <c>_focused</c> is already
    /// true), and the still-focused box keeps KeyboardHandler's
    /// <c>_textInputDelegate</c>, which swallows every keystroke bound for
    /// the game.
    /// </remarks>
    internal static class FocusRelease
    {
        /// <summary>
        /// Releases focus, if this box holds it, before Blish's own
        /// soft unfocus in <c>DisposeControl</c> can strand the global slot.
        /// The <c>Disposed</c> event fires at the top of
        /// <c>Control.Dispose</c>, while the control is still parented.
        /// Returns the box so construction sites can chain.
        /// </summary>
        public static T ReleaseOnDispose<T>(this T input) where T : TextInputBase
        {
            if (input == null)
            {
                return null;
            }

            input.Disposed += (sender, e) => Release(sender as TextInputBase);
            return input;
        }

        /// <summary>
        /// Releases any focused text box in <paramref name="root"/>'s
        /// subtree. Called where the module takes focus away from the user
        /// without a click: hiding the window, and swapping a tab's view.
        /// </summary>
        public static void ReleaseWithin(Control root)
        {
            if (root == null)
            {
                return;
            }

            var pending = new Stack<Control>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var control = pending.Pop();
                Release(control as TextInputBase);

                var container = control as Container;
                if (container == null)
                {
                    continue;
                }

                // Snapshot: Release can dispose nothing itself, but a
                // handler on InputFocusChanged may still mutate the tree.
                foreach (var child in container.Children.ToArray())
                {
                    pending.Push(child);
                }
            }
        }

        private static void Release(TextInputBase input)
        {
            if (input == null)
            {
                return;
            }

            // The focus slot is shared with every other module, so only the
            // box that holds focus - or the one the slot already names - may
            // null it.
            if (input.Focused || ReferenceEquals(GameService.Input.Keyboard.FocusedControl, input))
            {
                input.UnsetFocus();
            }
        }
    }
}
