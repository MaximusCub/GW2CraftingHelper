using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// The module's button. A <see cref="StandardButton"/> that answers a
    /// press, takes a font, takes a text colour, tints its icon, tints its
    /// plate and centres an icon-only label honestly - things Blish's button
    /// cannot be talked into from the outside, all of them living in
    /// StandardButton's own virtual Paint and RecalculateLayout.
    /// Defaults reproduce StandardButton's own rendering exactly, so this is
    /// a drop-in: a call site that sets none of the new properties draws
    /// pixel for pixel what it drew before.
    /// <para>
    /// StandardButton is MUTE in 1.3.0 - its OnClick asks ContentService for
    /// "audio\\button-click" against a reader already rooted at ref.dat's
    /// "audio" folder, so the lookup fails its FileExists check silently.
    /// The sound is supplied by <see cref="PressFeedback"/> instead; if a
    /// later Blish release fixes that double-prefixed path this button will
    /// play it twice, and the PlayClick call in PressFeedback.Wire is what
    /// to drop.
    /// </para>
    /// docs/ARCHITECTURE.md, "Views: relocated design narrative".
    /// </summary>
    internal class FeedbackButton : StandardButton
    {
        /// <summary>One frame of Blish's 8-frame button-face atlas.</summary>
        private const int AtlasSpriteWidth = 350;
        private const int AtlasSpriteHeight = 20;

        /// <summary>StandardButton's ICON_SIZE - what ResizeIcon resizes to.</summary>
        private const int StandardIconSize = 16;

        /// <summary>StandardButton's ICON_TEXT_OFFSET, between icon and text.</summary>
        private const int IconTextPadding = 4;

        /// <summary>
        /// StandardButton shifts the text right by 10 when an icon shares the
        /// button with it, to keep the pair optically centred.
        /// </summary>
        private const int IconTextGap = 10;

        /// <summary>Blish's own disabled plate and disabled ink.</summary>
        private static readonly Color DisabledFace = Color.FromNonPremultiplied(121, 121, 121, 255);
        private static readonly Color DisabledInk = Color.FromNonPremultiplied(51, 51, 51, 255);

        /// <summary>
        /// How far a disabled icon is dimmed, so it reads as disabled rather
        /// than as absent - Blish draws a disabled Control's children
        /// unchanged and leaves that entirely to the control. No module
        /// button currently carries an Icon; this and <see cref="IconTint"/>
        /// are here because the class promises StandardButton's whole
        /// surface, not because a caller is using them.
        /// </summary>
        private const float DisabledIconDim = 0.4f;

        private static Texture2D _face;
        private static Texture2D _border;

        private Color _enabledTextColor = Color.Black;
        private Color _iconTint = Color.White;
        private Color? _plateTint = null;
        private Rectangle _iconBounds = Rectangle.Empty;
        private Rectangle _textBounds = Rectangle.Empty;

        internal FeedbackButton()
        {
            // Named rather than inherited: DefaultFont14 is what
            // StandardButton happened to draw in, and Caption is the ramp
            // tier that means the same size. Every measurement of a button's
            // text in this module already assumes Caption (see UiFonts), and
            // this is what makes that true by construction.
            _font = UiFonts.Caption;
            PressFeedback.Wire(this);
        }

        /// <summary>
        /// The face this button draws its label in. Defaults to
        /// <see cref="UiFonts.Caption"/>, which is the size StandardButton
        /// paints at, so setting nothing changes nothing.
        /// </summary>
        public BitmapFont Font
        {
            get => _font;
            set => SetProperty(ref _font, value, true, nameof(Font));
        }

        /// <summary>
        /// Label colour while enabled. Defaults to black, which is what
        /// StandardButton forces - the button art is parchment, so a light
        /// colour here needs a reason.
        /// </summary>
        public Color TextColor
        {
            get => _enabledTextColor;
            set => SetProperty(ref _enabledTextColor, value, false, nameof(TextColor));
        }

        /// <summary>
        /// Multiplied into the icon. Defaults to white, i.e. untinted, which
        /// is what StandardButton does. Set it to bring a light affordance
        /// texture down onto the light button face.
        /// </summary>
        public Color IconTint
        {
            get => _iconTint;
            set => SetProperty(ref _iconTint, value, false, nameof(IconTint));
        }

        /// <summary>
        /// Plate modulation while the face draws, or null for the atlas
        /// exactly as StandardButton ships it. The tree's ignore toggle
        /// fills its plate with PillColors' ignore-active amber while its
        /// item is ignored - the same filled-key state signal the pill it
        /// replaced carried - and every other button leaves this null.
        /// <para>
        /// A tinted plate keeps the atlas face and the enabled ink even
        /// while the control is disabled: the flat grey plate is Blish's
        /// own disabled look, and repainting the toggle with it would
        /// erase the one state the tint exists to carry. Inertness for a
        /// tinted button is carried by Enabled itself (no Click, and
        /// PressFeedback.Wire's own Enabled gate) plus whatever wash and
        /// tooltip the caller adds, not by the plate.
        /// </para>
        /// </summary>
        public Color? PlateTint
        {
            get => _plateTint;
            set => SetProperty(ref _plateTint, value, false, nameof(PlateTint));
        }

        /// <summary>
        /// Labels the button with one glyph from the module's own atlas, and
        /// with the ASCII stand-in when that atlas failed to load. The one
        /// seam that pairs a glyph with the font that can draw it, for the
        /// same reason <see cref="Services.UiGlyphs.ExpandCaret"/> is one:
        /// a call site that chose the glyph and the font separately could
        /// seat a PUA codepoint on Menomonia, where it draws nothing and
        /// advances nothing.
        /// <para>
        /// Glyph TEXT, not an <see cref="StandardButton.Icon"/>: text takes
        /// the enabled/disabled ink this button already paints, so a set of
        /// row actions cannot end up half black text and half tinted
        /// texture (Services/UiGlyphs.RemoveMark).
        /// </para>
        /// </summary>
        internal void SetGlyph(string glyph)
        {
            bool available = UiFonts.GlyphsAvailable;
            Font = available ? UiFonts.Glyphs : UiFonts.Caption;
            Text = available ? glyph : UiGlyphs.AsciiFallback(glyph);
        }

        private static Texture2D Face =>
            _face ?? (_face = GameService.Content.GetTexture("common/button-states"));

        private static Texture2D Border =>
            _border ?? (_border = GameService.Content.GetTexture("button-border"));

        /// <summary>
        /// Icon and text bounds. Same arithmetic as StandardButton's, with
        /// the icon-only case centred rather than paying for a text gap that
        /// is not there. StandardButton keeps both rectangles private, so
        /// they are recomputed here rather than read.
        /// </summary>
        public override void RecalculateLayout()
        {
            // StandardButton measures unconditionally because its font is
            // private and can never be null. Ours can be assigned, so the
            // guard LabelBase.DrawText already has is needed here too.
            if (_font == null)
            {
                _iconBounds = Rectangle.Empty;
                _textBounds = new Rectangle(0, 0, Width, Height);
                return;
            }

            var textDimensions = GetTextDimensions();
            bool hasText = textDimensions.Width > 0f;
            int textLeft = (int)((Width / 2) - (textDimensions.Width / 2f));

            if (Icon == null)
            {
                _iconBounds = Rectangle.Empty;
                _textBounds = new Rectangle(textLeft, 0, Width - textLeft, Height);
                return;
            }

            // StandardButton dereferences Icon.Texture here and throws while
            // an AsyncTexture2D is still loading; 16x16 is both the size
            // Blish documents for a button icon and what ResizeIcon would
            // have produced, so it is the right answer to wait with.
            Point iconSize = ResizeIcon || Icon.Texture == null
                ? new Point(StandardIconSize)
                : Icon.Texture.Bounds.Size;

            if (!hasText)
            {
                // The whole button IS the icon. Blish's own formula adds the
                // text gap here anyway and lands 4px right of centre.
                _iconBounds = new Rectangle(
                    (Width - iconSize.X) / 2, (Height - iconSize.Y) / 2, iconSize.X, iconSize.Y);
                _textBounds = Rectangle.Empty;
                return;
            }

            textLeft += IconTextGap;
            _iconBounds = new Rectangle(
                textLeft - iconSize.X - IconTextPadding,
                (Height / 2) - (iconSize.Y / 2),
                iconSize.X,
                iconSize.Y);
            _textBounds = new Rectangle(textLeft, 0, Width - textLeft, Height);
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            // A tinted face is a state signal (see PlateTint), so a tinted
            // button keeps its enabled plate, ink and icon while disabled
            // rather than dropping to the grey. All three layers read this
            // one test: keying the icon on Enabled alone dimmed it over a
            // plate and ink that had stayed lit.
            bool showsState = Enabled || _plateTint.HasValue;

            var plate = new Rectangle(3, 3, Width - 6, Height - 5);
            if (showsState)
            {
                spriteBatch.DrawOnCtrl(
                    this,
                    Face,
                    plate,
                    new Rectangle(AnimationState * AtlasSpriteWidth, 0, AtlasSpriteWidth, AtlasSpriteHeight),
                    _plateTint ?? Color.White);
            }
            else
            {
                spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, plate, DisabledFace);
            }

            spriteBatch.DrawOnCtrl(this, Border, new Rectangle(2, 0, Width - 5, 4), new Rectangle(0, 0, 1, 4));
            spriteBatch.DrawOnCtrl(this, Border, new Rectangle(Width - 4, 2, 4, Height - 3), new Rectangle(0, 1, 4, 1));
            spriteBatch.DrawOnCtrl(this, Border, new Rectangle(3, Height - 4, Width - 6, 4), new Rectangle(1, 0, 1, 4));
            spriteBatch.DrawOnCtrl(this, Border, new Rectangle(0, 2, 4, Height - 3), new Rectangle(0, 3, 4, 1));

            var icon = Icon?.Texture;
            if (icon != null && _iconBounds != Rectangle.Empty)
            {
                spriteBatch.DrawOnCtrl(
                    this, icon, _iconBounds,
                    showsState ? _iconTint : _iconTint * DisabledIconDim);
            }

            // Assigned per frame for the same reason StandardButton does it:
            // the enabled state can change between layout passes, and the
            // colour is the only thing that says so once the plate is flat.
            _textColor = showsState ? _enabledTextColor : DisabledInk;
            DrawText(spriteBatch, _textBounds);
        }
    }
}
