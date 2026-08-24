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
        private const int MaxReleaseAttempts = 3;

        /// <summary>
        /// Releases focus, if this box holds it, before Blish's own
        /// soft unfocus in <c>DisposeControl</c> can strand the global slot.
        /// The <c>Disposed</c> event fires at the top of
        /// <c>Control.Dispose</c>, while the control is still parented.
        /// Returns the box so construction sites can chain.
        /// </summary>
        public static T ReleaseOnDispose<T>(this T input)
            where T : TextInputBase
        {
            if (input == null)
            {
                return null;
            }

            input.Disposed += (sender, e) => Release(sender as TextInputBase);
            return input;
        }

        /// <summary>
        /// Releases the stale global slot Blish's Enter handling leaves
        /// behind. Measured against Blish HUD 1.3.0:
        /// <c>TextBox.OnEnterPressed</c> soft-unfocuses
        /// (<c>base.Focused = false</c>) BEFORE raising
        /// <c>EnterPressed</c>, so the shared slot still names the box and
        /// the next Escape is consumed clearing it instead of closing the
        /// window. Chained at construction, so this handler runs ahead of
        /// any site handler on the same event - a site that re-focuses in
        /// response to Enter ends with a coherent focused state, not a
        /// half-cleared one. Returns the box so construction sites can
        /// chain.
        /// </summary>
        public static T ReleaseOnEnter<T>(this T input)
            where T : TextBox
        {
            if (input == null)
            {
                return null;
            }

            input.EnterPressed += (sender, e) => Release(sender as TextInputBase);
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

            var keyboard = GameService.Input.Keyboard;

            // The focus slot is shared with every other module, so only the
            // box that holds focus - or the one the slot already names - may
            // null it.
            if (!input.Focused && !ReferenceEquals(keyboard.FocusedControl, input))
            {
                return;
            }

            input.UnsetFocus();

            // UnsetFocus is not atomic: it is `Focused = false`, which raises
            // InputFocusChanged synchronously, and only then
            // `FocusedControl = null`. A handler that re-focuses from inside
            // that notification therefore ends the call with the box focused
            // and no slot naming it - holding KeyboardHandler's
            // _textInputDelegate, invisible to its ancestor heal sweep, which
            // is the swallowed-keyboard state this type exists to prevent.
            // The attempt count is bounded because a handler that re-focuses
            // on every notification cannot be out-waited.
            for (int attempt = 0; input.Focused && attempt < MaxReleaseAttempts; attempt++)
            {
                input.Focused = false;
            }

            // Invariant: the slot names the box that holds focus, or nothing.
            // A box that will not let go keeps the slot - a live focus Blish
            // can still heal beats a slot naming nobody.
            if (!input.Focused && ReferenceEquals(keyboard.FocusedControl, input))
            {
                keyboard.FocusedControl = null;
            }
        }
    }
}
