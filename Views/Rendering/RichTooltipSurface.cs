using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    /// row into Labels and coin runs, paints the game's own canvas in
    /// place of Blish's tooltip art, and keeps the box on screen.
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
        /// The game's own canvas: pure black, faintly translucent, over
        /// the whole box. Measured per-row background medians run
        /// (20,25,28)..(57,59,56) over a bright scene, i.e. ~0.88-0.92
        /// alpha (spec section 1.1, gap G1).
        /// <para>
        /// This is the ONE translucent layer. Blish's own tooltip art is
        /// drawn at 0.98 alpha and is suppressed entirely by the
        /// <see cref="PaintBeforeChildren"/> override below - stacking a
        /// second translucent layer on top of it would match neither the
        /// game nor audit finding H6 (content bleeding through the box).
        /// </para>
        /// </summary>
        private static readonly Color BackgroundColor = new Color(0, 0, 0) * 0.92f;

        /// <summary>1px, near-black, all four edges - measured on column
        /// x=0 of the xyaren capture, whose x=1 is already interior (G2).</summary>
        private static readonly Color BorderColor = new Color(6, 10, 12);

        /// <summary>
        /// Every glyph in a game tooltip carries a dark halo (measured at
        /// 3x, spec section 1.3, gap G8). Same pair the module's own row
        /// labels already use.
        /// </summary>
        private static readonly Color ShadowColor = Color.Black * 0.8f;

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

        /// <summary>
        /// The game's canvas instead of Blish's. Blish's own override
        /// (decompiled, 1.3.0) draws its "tooltip" texture at
        /// <c>Color.White * 0.98f</c> plus four dark inner edge bands;
        /// replacing it outright is what lets the fill be translucent
        /// without stacking two translucent layers, and what makes the
        /// border a single measured pixel rather than Blish's gradient.
        /// <para>
        /// Blish's content edge buffer - <c>Thickness(4 top, 4 right,
        /// 3 bottom, 6 left)</c>, which <c>RecalculateLayout</c> turns into
        /// the ContentRegion every child is positioned inside - already IS
        /// the game's measured 6px left padding with 3-4px on the other
        /// edges (spec section 1.2, gap G23), so the padding needs no work
        /// of its own once the art underneath it is gone.
        /// </para>
        /// </summary>
        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            var pixel = ContentService.Textures.Pixel;
            spriteBatch.DrawOnCtrl(this, pixel, bounds, BackgroundColor);

            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), BorderColor);
            spriteBatch.DrawOnCtrl(
                this, pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), BorderColor);
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), BorderColor);
            spriteBatch.DrawOnCtrl(
                this, pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), BorderColor);
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

            var font = UiFonts.Body;
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
                // No fill of its own: the canvas is painted across the
                // whole box by PaintBeforeChildren, so a second fill here
                // would be the stacked-translucency case that matches
                // neither the game nor H6.
                BackgroundColor = Color.Transparent,
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
                        iconYOffset,
                        showShadow: true);
                    continue;
                }

                if (placed.Span.Text.Length == 0)
                {
                    continue;
                }

                // Same class as the plan's row labels: these spans carry
                // item and character names, so they need the descender
                // clearance too. Height is never read back here (the panel
                // is sized from the line layout), so pinning it is inert
                // beyond the two pixels.
                _ = LabelHelpers.WithDescenderClearance(new Label()
                {
                    Text = placed.Span.Text,
                    Font = font,
                    TextColor = ResolveColor(placed.Span),
                    ShowShadow = true,
                    ShadowColor = ShadowColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(placed.X, y),
                    Parent = _contentPanel
                });
            }
        }

        /// <summary>
        /// The one place a tooltip span's semantic role becomes a colour.
        /// The roles live in Blish-free <c>Services/TooltipContent.cs</c>
        /// precisely so no composer has to reference XNA to say "this line
        /// is an item name".
        /// </summary>
        private static Color ResolveColor(TooltipSpan span)
        {
            switch (span.Role)
            {
                // Same palette the plan's own item-name labels use, so a
                // name reads identically on the row and in its tooltip.
                case TooltipSpanRole.Rarity:
                    return RarityColors.GetRarityNameColor(span.RarityKey);

                // Light blue, not green: measured on the wiki's
                // Rune_effects_*.jpg captures (per-row peaks
                // 95-115/118-138/148-180) and corroborated by FWDekker's
                // #5599ff replica - see docs/KNOWN-ISSUES.md,
                // "Tooltip authenticity", gap G3. The exact triple is the
                // spec's recommendation, not a measurement.
                case TooltipSpanRole.Bonus:
                    return new Color(120, 170, 235);

                // A tier above the wearer's equipped count. Unreachable
                // today - see TooltipSpanRole.BonusInactive.
                case TooltipSpanRole.BonusInactive:
                    return new Color(150, 150, 150);

                // Pale aquamarine, measured off File:User Xyaren
                // Tooltip.png rows 149-177 (median #B1D7D2). Upright, not
                // italic - the game does not italicise flavour.
                case TooltipSpanRole.Flavor:
                    return new Color(170, 210, 205);

                // gw2efficiency's .desc-abilitytype (#fea) - inferred, no
                // in-game capture of an abilitytype run exists.
                case TooltipSpanRole.AbilityType:
                    return new Color(255, 238, 170);

                case TooltipSpanRole.Warning:
                    return new Color(255, 0, 0);

                // Genuine secondary annotations only ("0/500 in Material
                // Storage", measured #939496). The identity block is white.
                case TooltipSpanRole.Muted:
                    return new Color(150, 150, 150);

                default:
                    return Color.White;
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
