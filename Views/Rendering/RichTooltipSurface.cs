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
        /// (left 6 + right 4), the chrome <c>Tooltip.RecalculateLayout</c>
        /// adds around whatever this surface's content panel measures.
        /// <c>Tooltip.EnableTooltips</c> builds the buffer as
        /// <c>new Thickness(4f, 4f, 3f, 6f)</c>, and that overload's argument
        /// order is (top, right, bottom, left): the 4 and 3 that look like a
        /// horizontal pair are the VERTICAL edges.
        /// </summary>
        private const int ChromeWidth = 10;

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

        /// <summary>
        /// Redraws the box when the content registered for the control it is
        /// already showing has been replaced. Blish's plain path refreshes
        /// itself on a content change - the <c>BasicTooltipText</c> setter
        /// either writes the new text straight into the live
        /// <c>BasicTooltipView</c> or drops <c>_tooltip</c> so the next hover
        /// rebuilds - and <c>Tooltip.HandleMouseMoved</c> calls <c>Show</c>
        /// only while the tooltip is HIDDEN, so without this the rich path
        /// would keep drawing the previous content for as long as the cursor
        /// stayed on the control.
        /// </summary>
        internal void RefreshShowing(Control control)
        {
            if (control == null || !Visible || CurrentControl != control)
            {
                return;
            }

            var content = _resolveContent(control);
            if (content == null || content.IsEmpty)
            {
                Hide();
                return;
            }

            BuildContent(content);
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
            // Never shorter than a coin icon: the content panel clips its
            // children, and a row that cannot hold a 20px icon would clip
            // the bottom off every coin run on the last line.
            int rowHeight = System.Math.Max(font.LineHeight, CoinSegmentMath.CoinIconSize);
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

            // Fixed-size coin icons centred against a taller number font,
            // the same correction the Summary band's tiles make - without
            // it the icons stick to the top of their row.
            int iconYOffset = System.Math.Max(0, (rowHeight - CoinSegmentMath.CoinIconSize) / 2);
            for (int rowIndex = 0; rowIndex < layout.Rows.Count; rowIndex++)
            {
                RenderRow(layout.Rows[rowIndex], rowIndex * rowHeight, font, iconYOffset);
            }

            // Sized NOW rather than on the next update tick. The content
            // panel's extent is explicit, so the base RecalculateLayout has
            // everything it needs, and Show()'s Reposition below would
            // otherwise clamp against the PREVIOUS hover's size for one
            // frame - Blish only recalculates a container's layout while it
            // is parented, and this one is parented by Show().
            RecalculateLayout();
        }

        private void RenderRow(TooltipLayoutMath.LaidOutRow row, int y, BitmapFont font, int iconYOffset)
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
                        font,
                        1f,
                        iconYOffset);
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
            // session accumulates another content tree. Snapshot via the
            // collection's own ToArray: ControlCollection.CopyTo throws by
            // design, so a List built from it crashes on the second hover.
            foreach (var child in Children.ToArray())
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
