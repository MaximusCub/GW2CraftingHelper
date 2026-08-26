using Blish_HUD;
using Blish_HUD.Input;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// Re-resolves which control the cursor is over, after this module has
    /// rebuilt controls under a cursor that never moved.
    ///
    /// <para>
    /// MEASURED (decompiled Blish HUD 1.3.0, <c>MouseHandler.Update</c>):
    /// the hover chain is recomputed ONLY when the mouse position changed
    /// between two frames -
    /// <c>if (previous.Position != State.Position) ActiveControl =
    /// SpriteScreen.TriggerMouseInput(MouseMoved, State);</c>. Everything
    /// downstream of that assignment - <c>Control.MouseOver</c>, the
    /// MouseEntered/MouseLeft pair it fires, and therefore every hover
    /// wash, pill highlight and hover-predicate in this module - is
    /// therefore frozen against a control set the cursor has not moved
    /// across.
    /// </para>
    ///
    /// <para>
    /// Every click in the plan view that rebuilds what it was clicked on
    /// hits that: a decision pill re-solves and rebuilds its own row, a
    /// sort header re-renders the table it labels, a caret rebuilds the
    /// subtree under it. The replacement control lands under a stationary
    /// cursor with <c>MouseOver == false</c> and no MouseEntered fired, so
    /// the pill the user is pointing at reads as un-hovered until they
    /// jiggle the mouse - and this module's own
    /// <c>AnyPillHovered</c> guard, which asks the same question, answers
    /// wrongly in the meantime.
    /// </para>
    ///
    /// <para>
    /// This calls the same entry point Blish's own motion branch calls,
    /// with the same live mouse state, which walks the CURRENT control tree
    /// and re-fires MouseEntered/MouseLeft accordingly. It does NOT restore
    /// <c>MouseHandler.ActiveControl</c> (that setter is private); the
    /// tooltip resolution and input-blocking that read it re-sync on the
    /// next real mouse move. What is fixed here is the visible hover state,
    /// which is what a stationary user actually sees.
    /// </para>
    ///
    /// <para>
    /// This is NOT the fix for a click being LOST. That is a different,
    /// also-measured mechanism: <c>MouseHandler</c> buffers exactly ONE
    /// pending mouse event (<c>_mouseEvent</c>, overwritten by the hook and
    /// consumed once per <c>Update</c>), and
    /// <c>Control.OnLeftMouseButtonReleased</c> only raises Click when that
    /// same control instance was primed by its own press. A frame long
    /// enough to contain both halves of the next click drops the press, so
    /// the release finds nothing primed. The answer to that is to make the
    /// rebuild frame short - see TreeSectionController.TryRefreshInPlace.
    /// </para>
    /// </summary>
    internal static class HoverChainResync
    {
        /// <summary>
        /// Call immediately after a click handler has rebuilt controls
        /// under the cursor. Safe to call when nothing moved: it resolves
        /// to whatever is genuinely under the pointer.
        /// </summary>
        internal static void AfterRebuild()
        {
            var screen = GameService.Graphics?.SpriteScreen;
            if (screen == null)
            {
                return;
            }

            screen.TriggerMouseInput(MouseEventType.MouseMoved, GameService.Input.Mouse.State);
        }
    }
}
