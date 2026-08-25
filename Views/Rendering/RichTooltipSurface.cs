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
    /// control - see KNOWN-ISSUES #41 for the
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
        /// the whole box.
        /// <para>
        /// 0.82, retuned from 0.92 against the maintainer's in-game
        /// capture: a real tooltip's interior is not flat - medians shift
        /// ~20 levels per channel across ONE box, (34,38,40) to (43,55,55) -
        /// which puts the game nearer 0.75-0.85. The UPPER end of that band,
        /// since audit H6 requires content behind never to bleed through
        /// legibly. The ONE translucent layer: Blish's own art (0.98) is
        /// suppressed by <see cref="PaintBeforeChildren"/>.
        /// </para>
        /// </summary>
        private static readonly Color BackgroundColor = new Color(0, 0, 0) * 0.82f;

        /// <summary>1px, near-black, all four edges - measured on column
        /// x=0 of the xyaren capture, whose x=1 is already interior (G2).</summary>
        private static readonly Color BorderColor = new Color(6, 10, 12);

        /// <summary>The light line the game runs immediately inside its
        /// dark border - the capture shows a pair, not one edge. This
        /// file's own chrome grey at low alpha: a highlight on the canvas,
        /// not a second border.</summary>
        private static readonly Color BevelColor = new Color(166, 175, 174) * 0.22f;

        /// <summary>
        /// Every glyph in a game tooltip carries a dark halo (measured at
        /// 3x, KNOWN-ISSUES #42, gap G8). Same pair the module's own row
        /// labels already use.
        /// </summary>
        private static readonly Color ShadowColor = Color.Black * 0.8f;

        /// <summary>
        /// The header icon: ~34x34 including its 1px frame, ~32px of art,
        /// with the name ~5px to its right - all measured off the xyaren
        /// capture (KNOWN-ISSUES #42, gap G11).
        /// </summary>
        private const int HeaderIconSize = 32;

        private const int HeaderIconBorder = 1;

        private const int HeaderIconFrameSize = HeaderIconSize + (2 * HeaderIconBorder);

        private const int HeaderIconGap = 5;

        private static readonly Color HeaderIconFrameColor = new Color(166, 175, 174);

        /// <summary>
        /// TOOLTIP-LOCAL, deliberately: the measured in-game boxes are
        /// 300-332px wide and gw2efficiency caps at 350, while Blish's own
        /// 500 stays the preferred width for every plain tooltip in the
        /// module (gap G24). The shared
        /// <c>TooltipLayoutMath.PreferredMaxContentWidth</c> is untouched.
        /// </summary>
        private const int MaxContentWidth = 350;

        /// <summary>
        /// The game's coin icon is ~0.8x its line height (~13px on a 16px
        /// line, measured on the steak capture) - not the module's shared
        /// 20px table icon, which under the +2pt font wave reads small on
        /// a 22px line and tall on a 16px one. TOOLTIP-LOCAL for the same
        /// reason as the width above: <c>CoinSegmentMath.CoinIconSize</c>
        /// is the plan tables' constant and stays theirs (gap G22).
        /// </summary>
        private static int CoinIconSizeFor(int lineHeight)
        {
            return System.Math.Max(8, (lineHeight * 4) / 5);
        }

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
        /// edges (KNOWN-ISSUES #42, gap G23), so the padding needs no work
        /// of its own once the art underneath it is gone.
        /// </para>
        /// </summary>
        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            var pixel = ContentService.Textures.Pixel;
            spriteBatch.DrawOnCtrl(this, pixel, bounds, BackgroundColor);

            DrawEdges(spriteBatch, pixel, bounds, BorderColor);

            // One pixel inside the border, and only where there is room
            // for both: at two pixels wide it would overdraw it.
            if (bounds.Width > 2 && bounds.Height > 2)
            {
                DrawEdges(
                    spriteBatch, pixel,
                    new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2),
                    BevelColor);
            }
        }

        private void DrawEdges(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, Color color)
        {
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            spriteBatch.DrawOnCtrl(
                this, pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            spriteBatch.DrawOnCtrl(
                this, pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
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

        /// <summary>
        /// Redraws the box for the control it is currently showing. The
        /// content itself is unchanged - what changed is an INPUT the
        /// deferred builder reads (the session stat cache gaining the
        /// hovered item's block, Q13).
        /// </summary>
        internal void RefreshCurrent()
        {
            if (Visible && CurrentControl != null)
            {
                RefreshShowing(CurrentControl);
            }
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
            var font = UiFonts.Body;
            int lineHeight = font.LineHeight;
            int coinIconSize = CoinIconSizeFor(lineHeight);

            DisposeContent();

            int maxWidth = TooltipLayoutMath.MaxContentWidth(
                GameService.Graphics.SpriteScreen.Width, ChromeWidth, MaxContentWidth);

            var layout = TooltipLayoutMath.LayoutContent(
                content, maxWidth, lineHeight,
                s => (int)System.Math.Ceiling(font.MeasureString(s).Width),
                copper => CoinSegmentMath.TotalCoinSegmentsWidth(
                    CoinCurrencyRenderer.BuildCoinSegments(copper, font), coinIconSize),
                // Only a coin row needs icon clearance, and only a header
                // row is icon-tall; a prose row is one line pitch, as the
                // game's 16px pitch is (gap G21).
                coinRowHeight: System.Math.Max(lineHeight, coinIconSize),
                headerRowHeight: System.Math.Max(lineHeight, HeaderIconFrameSize),
                headerIndent: HeaderIconFrameSize + HeaderIconGap);

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

            foreach (var row in layout.Rows)
            {
                RenderRow(row, font, lineHeight, coinIconSize);
            }

            // Sized NOW rather than on the next update tick. The content
            // panel's extent is explicit, so the base RecalculateLayout has
            // everything it needs, and Show()'s Reposition below would
            // otherwise clamp against the PREVIOUS hover's size for one
            // frame - Blish only recalculates a container's layout while it
            // is parented, and this one is parented by Show().
            RecalculateLayout();
        }

        private void RenderRow(
            TooltipLayoutMath.LaidOutRow row, BitmapFont font, int lineHeight, int coinIconSize)
        {
            if (row.IconUrl != null)
            {
                // The game frames the icon in a 1px light grey (measured
                // (166,175,174) on the xyaren capture's left edge) rather
                // than in the rarity colour the module frames its ROWS
                // with - the name beside it already carries the rarity.
                IconControls.CreateItemIcon(
                    _contentPanel, row.IconUrl, HeaderIconFrameColor,
                    0, row.Y, HeaderIconSize, HeaderIconBorder);
            }

            // The name is centred on the icon, not top-aligned (measured,
            // spec section 1.2); every other row kind sits at its top.
            int textY = row.Y + System.Math.Max(0, (row.Height - lineHeight) / 2);

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
                        textY,
                        font,
                        1f,
                        // Fixed-size coin icons centred against a taller
                        // number font, the same correction the Summary
                        // band's tiles make - without it the icons stick
                        // to the top of their row.
                        System.Math.Max(0, (lineHeight - coinIconSize) / 2),
                        showShadow: true,
                        iconSize: coinIconSize);
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
                    Location = new Point(placed.X, textY),
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
                // #5599ff replica - see KNOWN-ISSUES #42, gap G3. The exact triple is the
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

                // gw2efficiency's .desc-reminder (#afafaf = 175) - inferred,
                // and 25 levels per channel lighter than the annotation grey
                // below, which is measured. Two sources, two constants.
                case TooltipSpanRole.Reminder:
                    return new Color(175, 175, 175);

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
