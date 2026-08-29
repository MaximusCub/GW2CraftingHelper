using Blish_HUD;
using Blish_HUD.Input;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// Re-resolves which control the cursor is over, after this module has
    /// rebuilt controls under a cursor that never moved.
    /// <para>
    /// MEASURED (decompiled Blish HUD 1.3.0, <c>MouseHandler.Update</c>):
    /// the hover chain is recomputed ONLY when the mouse position changed
    /// between two frames - <c>if (previous.Position != State.Position)
    /// ActiveControl = SpriteScreen.TriggerMouseInput(MouseMoved, State);</c>.
    /// So <c>Control.MouseOver</c>, the MouseEntered/MouseLeft pair it fires,
    /// and every hover wash and hover-predicate in this module are frozen
    /// against a control set the cursor has not moved across.
    /// </para>
    /// <para>
    /// This calls that same entry point with the same live mouse state. It
    /// does NOT restore <c>MouseHandler.ActiveControl</c> (that setter is
    /// private); the readers of it re-sync on the next real mouse move.
    /// </para>
    /// NOT the fix for a click being LOST: docs/ARCHITECTURE.md, "Views:
    /// relocated design narrative".
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
