using System;
using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TaimisToolbench.Views
{
    /// <summary>
    /// The input-eating layer that makes <see cref="ModalDialog"/> actually
    /// modal, over the module window only - other modules' windows and the
    /// game stay live. Paints nothing: it is a hit-test, not a scrim.
    /// <para>
    /// Measured in BlishHUD 1.3.0: Container.TriggerMouseInput walks its
    /// children <c>OrderByDescending(ZIndex).ThenByDescending(index in
    /// _children)</c> and BREAKS on the first whose bounds contain the
    /// cursor and whose own TriggerMouseInput returns non-null, which it
    /// does whenever CapturesInput() has the Mouse (or MouseWheel) flag -
    /// but NOT with CaptureType.Filter, which that loop steps past.
    /// </para>
    /// <para>
    /// A window's effective ZIndex is not a compile-time constant, so the
    /// backdrop tracks <c>dialog.ZIndex - 1</c> every visible frame and is
    /// built lazily on the first Show(), which puts it after the window it
    /// blocks in the sibling-index tiebreak above.
    /// </para>
    /// Derivations: docs/ARCHITECTURE.md, "Views: relocated design narrative".
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
