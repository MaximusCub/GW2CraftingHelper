using System;
using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TaimisToolbench.Views
{
    /// <summary>
    /// The input-eating layer that makes <see cref="ModalDialog"/> actually
    /// modal. Before it, a confirm was only visually on top: with the Clear
    /// Cache confirm open, a click on the Crafting Plan tab's "+" add-row
    /// button behind it still registered.
    ///
    /// <para>
    /// <b>Why a control at all.</b> Measured in BlishHUD 1.3.0:
    /// Container.TriggerMouseInput walks its children
    /// <c>OrderByDescending(ZIndex).ThenByDescending(index in _children)</c>,
    /// and the first child whose AbsoluteBounds contain the cursor and whose
    /// own TriggerMouseInput returns non-null wins - and BREAKS the loop, so
    /// no lower sibling is reached. Control.TriggerMouseInput returns
    /// <c>this</c> whenever CapturesInput() has the Mouse (or MouseWheel)
    /// flag. A bare control between the blocked window and the dialog is
    /// therefore all it takes; the one flag that would NOT block is
    /// CaptureType.Filter, which the loop deliberately steps past.
    /// </para>
    ///
    /// <para>
    /// <b>Why it covers the module window, not the screen.</b> A capturing
    /// control also stops the GAME from seeing the click. A screen-wide
    /// blocker would mean a confirm left open swallows every click in Guild
    /// Wars 2, which is not a trade a HUD overlay should make for a
    /// two-button confirm. The finding is about the surface the dialog
    /// belongs to, so that is exactly what is blocked: other modules'
    /// windows and the game stay live.
    /// </para>
    ///
    /// <para>
    /// <b>Z-order.</b> A window's effective ZIndex is
    /// <c>5 + Screen.WINDOW_BASEZINDEX + its rank among windows ordered by
    /// (TopMost, LastInteraction)</c>, so it is not a compile-time constant
    /// and a TopMost dialog can land exactly one above a non-TopMost module
    /// window. The backdrop tracks <c>dialog.ZIndex - 1</c> every frame it
    /// is visible. On the tie that arithmetic can produce with the blocked
    /// window, the sibling-index tiebreak above decides - which is why the
    /// backdrop is constructed lazily on the first Show(), after every
    /// window exists, so it is always the later child.
    /// </para>
    ///
    /// <para>Paints nothing: it is a hit-test, not a scrim.</para>
    /// </summary>
    internal sealed class ModalBackdrop : Control
    {
        private readonly WindowBase2 _dialogWindow;
        private readonly Func<Control> _blockedSurface;

        internal ModalBackdrop(WindowBase2 dialogWindow, Func<Control> blockedSurface)
        {
            _dialogWindow = dialogWindow ?? throw new ArgumentNullException(nameof(dialogWindow));
            _blockedSurface = blockedSurface ?? throw new ArgumentNullException(nameof(blockedSurface));

            Visible = false;
            Parent = GameService.Graphics.SpriteScreen;
        }

        protected override CaptureType CapturesInput()
        {
            return CaptureType.Mouse | CaptureType.MouseWheel;
        }

        /// <summary>
        /// Re-derives bounds and z-order right now rather than waiting for
        /// the next frame's DoUpdate - the dialog's own Show() can be
        /// followed by input in the same frame.
        /// </summary>
        internal void Sync()
        {
            var surface = _blockedSurface();
            if (surface == null || !surface.Visible || surface.Parent == null)
            {
                // Nothing to block. A zero-size control can never contain
                // the cursor, so it drops out of the hit test entirely
                // without needing to be hidden and re-shown.
                Size = Point.Zero;
                return;
            }

            var bounds = surface.AbsoluteBounds;
            var location = new Point(bounds.X, bounds.Y);
            var size = new Point(bounds.Width, bounds.Height);
            if (Location != location)
            {
                Location = location;
            }

            if (Size != size)
            {
                Size = size;
            }

            // WindowBase2.ZIndex throws unless the window is a direct child
            // of SpriteScreen (it derives its rank from that list), and
            // ModalDialog.Dispose detaches it.
            if (_dialogWindow.Parent == GameService.Graphics.SpriteScreen)
            {
                int target = _dialogWindow.ZIndex - 1;
                if (ZIndex != target)
                {
                    ZIndex = target;
                }
            }
        }

        public override void DoUpdate(GameTime gameTime)
        {
            // The blocked window can be dragged, resized or hidden while the
            // confirm is open, and the z-order ladder shifts with window
            // interaction, so both are re-derived per frame - but only while
            // the backdrop is up, which is the length of one confirm.
            if (!Visible)
            {
                return;
            }

            Sync();
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
        }
    }
}
