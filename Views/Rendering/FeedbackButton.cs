using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// The module's button. A <see cref="StandardButton"/> that answers a
    /// press, takes a font, takes a text colour, tints its icon and centres
    /// an icon-only label honestly - four things Blish's button cannot be
    /// talked into from the outside.
    /// <para>
    /// Measured from the vendored Blish HUD 1.3.0 binary (ilspycmd) - what
    /// StandardButton does and does not already do:
    /// </para>
    /// <list type="bullet">
    /// <item>Hover: <c>OnMouseEntered</c>/<c>OnMouseLeft</c> tween
    /// AnimationState 0 &lt;-&gt; 8 over 0.25s, stepping through the
    /// "common/button-states" atlas. That works, and is left alone - the
    /// paint below walks the same atlas with the same public
    /// AnimationState.</item>
    /// <item>Press: nothing. There is no OnLeftMouseButtonPressed override
    /// and no pressed frame in the atlas walk - the button looks identical
    /// held down as hovered.</item>
    /// <item>Sound: <c>OnClick</c> calls
    /// <c>PlaySoundEffectByName("audio\\button-click")</c>, but
    /// ContentService's audio reader is already rooted at ref.dat's "audio"
    /// folder, so the lookup becomes audio/audio/button-click.wav, fails the
    /// FileExists check, and returns silently. StandardButton is mute in
    /// 1.3.0; Checkbox and GlowButton, which pass the unprefixed
    /// "button-click", are not.</item>
    /// </list>
    /// <para>
    /// So the press-and-sound gap is supplied by <see cref="PressFeedback"/>.
    /// If a later Blish release fixes the double-prefixed path, this button
    /// will play the sound twice on a completed click and the PlayClick call
    /// in PressFeedback.Wire is what to drop.
    /// </para>
    ///
    /// <para>
    /// <b>Why the paint is overridden rather than the control replaced.</b>
    /// The three limits below all live in <c>StandardButton.Paint</c> and
    /// <c>RecalculateLayout</c>, both virtual; everything ABOVE them -
    /// the hover tween, the click event and its Enabled gate, the tooltip
    /// plumbing every one of this module's buttons relies on, focus, opacity
    /// and the whole Container/Control lifecycle - is inherited free and is
    /// the part that would have to be rebuilt, and kept rebuilt, if this
    /// derived from <c>Control</c> instead. The button art is Blish's own
    /// (<c>common/button-states</c> and <c>button-border</c>, both reachable
    /// through the public <c>GameService.Content.GetTexture</c>), so painting
    /// it ourselves costs two texture handles and no fidelity. Overriding two
    /// methods buys all four fixes; subclassing Control would buy them at the
    /// price of every behaviour that already works.
    /// </para>
    /// <list type="number">
    /// <item><b>No Font.</b> StandardButton draws in DefaultFont14 and
    /// exposes no way to change it, so a button could not sit on this
    /// module's type ramp and could not carry a glyph from the shipped
    /// glyph font (ref/glyphs.fnt) at all.</item>
    /// <item><b>Text colour is forced.</b> <c>Paint</c> assigns
    /// <c>_textColor</c> on EVERY frame, so a colour written from outside is
    /// overwritten before it is ever drawn.</item>
    /// <item><b>Icon is blitted untinted</b>, onto button art whose face
    /// samples about (200,193,175). Blish's own white affordance textures -
    /// 733269/733270, the matched X pair - are therefore invisible on a
    /// button, which is the measured reason Plan History reached for a
    /// Checkbox instead of a button wearing an icon.</item>
    /// <item><b>An icon-only button's icon is off centre by construction.</b>
    /// With no text, StandardButton seats it at
    /// <c>Width / 2 + 8 - iconWidth - 4</c> - the +8 is a text gap being paid
    /// for when there is no text - so it sits 4px right of centre at every
    /// width.</item>
    /// </list>
    /// <para>
    /// Defaults reproduce StandardButton's own rendering exactly, so this is
    /// a drop-in: a call site that sets none of the new properties draws
    /// pixel for pixel what it drew before.
    /// </para>
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
        /// How far a disabled icon is dimmed. The Ranker disables its reorder
        /// controls in one ordering mode, so a disabled icon has to read as
        /// disabled rather than as absent - Blish draws a disabled Control's
        /// children unchanged and leaves that entirely to the control.
        /// </summary>
        private const float DisabledIconDim = 0.4f;

        private static Texture2D _face;
        private static Texture2D _border;

        private Color _enabledTextColor = Color.Black;
        private Color _iconTint = Color.White;
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
            var plate = new Rectangle(3, 3, Width - 6, Height - 5);
            if (Enabled)
            {
                spriteBatch.DrawOnCtrl(
                    this,
                    Face,
                    plate,
                    new Rectangle(AnimationState * AtlasSpriteWidth, 0, AtlasSpriteWidth, AtlasSpriteHeight));
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
                spriteBatch.DrawOnCtrl(this, icon, _iconBounds, Enabled ? _iconTint : _iconTint * DisabledIconDim);
            }

            // Assigned per frame for the same reason StandardButton does it:
            // the enabled state can change between layout passes, and the
            // colour is the only thing that says so once the plate is flat.
            _textColor = Enabled ? _enabledTextColor : DisabledInk;
            DrawText(spriteBatch, _textBounds);
        }
    }
}
