using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// The module's ONE rich tooltip window. Deliberately a single shared
    /// instance repointed on hover rather than an instance per tooltip'd
    /// control - see docs/KNOWN-ISSUES.md, "Tooltip facility", for the
    /// decompiled evidence: <c>Control.Dispose</c> never touches its
    /// <c>_tooltip</c> field, and a Tooltip is not a child of its owner
    /// (<c>Tooltip.Show</c> reparents it to the SpriteScreen), so
    /// <c>Container.DisposeControl</c>'s descendant sweep never reaches it
    /// either. A per-control instance on tree rows and pills - controls
    /// this module rebuilds wholesale on every render - would leak one
    /// undisposed container and its whole child tree per row per render.
    ///
    /// Everything interesting happens in <see cref="TooltipLayoutMath"/>;
    /// this class is the thin Blish-coupled shell that turns a laid-out
    /// row into Labels and coin runs, keeps the box opaque, and keeps it
    /// on screen.
    /// </summary>
    internal sealed class RichTooltipSurface : Tooltip
    {
        /// <summary>
        /// Blish's own <c>Tooltip._contentEdgeBuffer</c> horizontal total
        /// (left 4 + right 3, measured), the chrome
        /// <c>Tooltip.RecalculateLayout</c> adds around whatever this
        /// surface's content panel measures.
        /// </summary>
        private const int ChromeWidth = 7;

        /// <summary>
        /// Fully opaque, and drawn as the content panel's own background so
        /// nothing behind the tooltip bleeds through its art. Blish's
        /// tooltip texture is drawn at 0.98 alpha over whatever is behind
        /// it, which is exactly the audit H6 complaint on the value-detail
        /// hover; the panel sits inside the content edge buffer, so the
        /// frame itself still reads as a Blish tooltip.
        /// </summary>
        private static readonly Color BackgroundColor = new Color(14, 14, 14);

        private readonly Func<Control, TooltipContent> _resolveContent;

        private Panel _contentPanel;

        internal RichTooltipSurface(Func<Control, TooltipContent> resolveContent)
        {
            _resolveContent = resolveContent ?? throw new ArgumentNullException(nameof(resolveContent));
        }

        /// <summary>
        /// The surface is invisible to the mouse, every child included.
        /// Without this a tooltip that the four-edge clamp moves under the
        /// cursor would win the hit test (Blish's default
        /// <c>CaptureType.Mouse</c> on Container/Label), become the active
        /// control, and fire <c>ActiveControlChanged</c> - which
        /// <c>Tooltip.ControlOnActiveControlChanged</c> answers by hiding
        /// the current tooltip, producing a show/hide flicker loop. Blish's
        /// own tooltips avoid it only by never being placed under the
        /// cursor, which is precisely the constraint the clamp relaxes.
        /// </summary>
        public override Control TriggerMouseInput(MouseEventType mouseEventType, MouseState ms)
        {
            return null;
        }

        public override void Show()
        {
            var content = CurrentControl == null ? null : _resolveContent(CurrentControl);
            if (content == null || content.IsEmpty)
            {
                // Nothing registered for the hovered control: stay hidden
                // rather than flash an empty frame. Blish retries on the
                // next mouse move, so a late registration still shows.
                return;
            }

            BuildContent(content);
            base.Show();
            Reposition();
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            // base re-runs Blish's own unclamped positioning on every tick
            // while visible, so the clamp has to run after it every tick
            // too - not once at Show.
            base.UpdateContainer(gameTime);
            if (Visible)
            {
                Reposition();
            }
        }

        private void Reposition()
        {
            var screen = GameService.Graphics.SpriteScreen;
            var mouse = GameService.Input.Mouse.Position;
            TooltipLayoutMath.Place(
                mouse.X, mouse.Y, Width, Height, screen.Width, screen.Height, out int x, out int y);
            if (x != Location.X || y != Location.Y)
            {
                Location = new Point(x, y);
            }
        }

        private void BuildContent(TooltipContent content)
        {
            DisposeContent();

            var font = GameService.Content.DefaultFont14;
            int rowHeight = font.LineHeight;
            int maxWidth = TooltipLayoutMath.MaxContentWidth(
                GameService.Graphics.SpriteScreen.Width, ChromeWidth);

            var layout = TooltipLayoutMath.LayoutContent(
                content, maxWidth, rowHeight,
                s => (int)System.Math.Ceiling(font.MeasureString(s).Width),
                copper => CoinSegmentMath.TotalCoinSegmentsWidth(
                    CoinCurrencyRenderer.BuildCoinSegments(copper, font)));

            _contentPanel = new Panel()
            {
                Size = new Point(System.Math.Max(1, layout.Width), System.Math.Max(1, layout.Height)),
                Location = Point.Zero,
                BackgroundColor = BackgroundColor,
                ShowBorder = false,
                Parent = this
            };

            for (int rowIndex = 0; rowIndex < layout.Rows.Count; rowIndex++)
            {
                RenderRow(layout.Rows[rowIndex], rowIndex * rowHeight, font);
            }
        }

        private void RenderRow(TooltipLayoutMath.LaidOutRow row, int y, BitmapFont font)
        {
            foreach (var placed in row.Spans)
            {
                if (placed.Span.IsCoin)
                {
                    // The coin invariant's own renderer, so a tooltip coin
                    // run is the same "number then icon" geometry as every
                    // table cell in the module.
                    CoinCurrencyRenderer.LayoutCoinSegments(
                        _contentPanel,
                        CoinCurrencyRenderer.BuildCoinSegments(placed.Span.CoinCopper, font),
                        placed.X,
                        y,
                        font);
                    continue;
                }

                if (placed.Span.Text.Length == 0)
                {
                    continue;
                }

                _ = new Label()
                {
                    Text = placed.Span.Text,
                    Font = font,
                    TextColor = Color.White,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(placed.X, y),
                    Parent = _contentPanel
                };
            }
        }

        private void DisposeContent()
        {
            // Container.ClearChildren only detaches (Parent = null) - it
            // does not dispose - so the previous hover's Labels and coin
            // icons have to be disposed explicitly or every hover for the
            // session accumulates another content tree.
            var previous = new List<Control>(Children);
            foreach (var child in previous)
            {
                child.Dispose();
            }
            _contentPanel = null;
        }

        protected override void DisposeControl()
        {
            DisposeContent();
            base.DisposeControl();
        }
    }
}
