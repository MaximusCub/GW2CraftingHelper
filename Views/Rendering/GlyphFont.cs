using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.TextureAtlases;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// Turns the parsed ref/glyphs.fnt plus its atlas page into a MonoGame
    /// <see cref="BitmapFont"/>. The only file in the module that touches
    /// both the glyph metrics and a graphics resource; the arithmetic all
    /// lives Blish-free in <see cref="GlyphFontDescriptor"/>.
    /// <para>
    /// Blish 1.3.0's own hook for this - <c>ContentsManager.GetBitmapFont</c> -
    /// throws NotImplementedException, and Blish rasterizes no TTFs at
    /// runtime, so the font is assembled through MonoGame.Extended 3.8.0's
    /// public constructors instead: BitmapFont(name, regions, lineHeight)
    /// over BitmapFontRegion(TextureRegion2D, codepoint, xOffset, yOffset,
    /// xAdvance). That is the same shape BitmapFontReader builds when it
    /// inflates one of Blish's own XNB faces, fed from our parser rather
    /// than from an XNB, so the result is a first-class font everywhere a
    /// BitmapFont is accepted.
    /// </para>
    /// </summary>
    internal static class GlyphFont
    {
        /// <summary>
        /// Highest codepoint <see cref="Merged"/> probes when copying a face's
        /// glyphs. MonoGame exposes no way to enumerate a BitmapFont's
        /// character map - <c>GetCharacterRegion</c> is the entire public
        /// surface - so the copy is a sweep. Menomonia's highest codepoint is
        /// U+E000, so the BMP is the whole story and the sweep is 65,536
        /// dictionary probes, run once per merged face at module load.
        /// </summary>
        private const int HighestProbedCodepoint = 0xFFFF;

        /// <summary>
        /// The glyph font alone, for a control whose entire text is one
        /// glyph. Ink is centred in the line box rather than seated on a
        /// baseline, because there is no neighbouring text to align to.
        /// </summary>
        internal static BitmapFont Standalone(GlyphFontDescriptor descriptor, Texture2D page)
        {
            var regions = new List<BitmapFontRegion>(descriptor.Count);
            foreach (var glyph in descriptor.Glyphs)
            {
                regions.Add(Region(glyph, page, descriptor.BoxCentredYOffset(glyph)));
            }

            return new BitmapFont("GwchGlyphs", regions, descriptor.LineHeight);
        }

        /// <summary>
        /// <paramref name="face"/>'s glyphs and ours in ONE font, seated on
        /// <paramref name="face"/>'s baseline and inheriting its line height
        /// and letter spacing.
        /// <para>
        /// This is what makes a sort indicator possible at all. The indicator
        /// is part of the header's own <c>Label.Text</c> (see
        /// SortableHeaderLabel) - which is what lets every right-aligned
        /// header keep tracking its column, since the relayout closures
        /// right-align off a width that already includes it - and a Label has
        /// exactly one Font. A separate glyph Label beside the title would
        /// have meant re-deriving nine call sites' worth of column arithmetic;
        /// one merged font means every existing MeasureString keeps measuring
        /// the whole string correctly and no call site learns anything new.
        /// </para>
        /// <para>
        /// The two fonts keep their own texture pages - a BitmapFontRegion
        /// carries its own TextureRegion2D - so a header string that has an
        /// indicator costs one extra texture switch inside its own
        /// DrawString. One per SORTED header, and only one header per table
        /// is ever sorted, so it is a handful of switches per frame against
        /// the alternative of re-deriving nine call sites' column arithmetic.
        /// </para>
        /// </summary>
        internal static BitmapFont Merged(
            string name, BitmapFont face, int faceBaselineY, GlyphFontDescriptor descriptor, Texture2D page)
        {
            var regions = new List<BitmapFontRegion>();
            for (int codepoint = 0; codepoint <= HighestProbedCodepoint; codepoint++)
            {
                // Ours wins on a collision rather than the constructor
                // throwing on a duplicate key. U+E000 is the only codepoint
                // Menomonia defines above Latin-1 punctuation, which is why
                // the atlas starts at U+E100 and this guard should never fire.
                if (descriptor.TryGet(codepoint, out _))
                {
                    continue;
                }

                var region = face.GetCharacterRegion(codepoint);
                if (region != null)
                {
                    // Reused, not rebuilt: BitmapFontRegion is immutable and
                    // carries the face's own kerning table with it.
                    regions.Add(region);
                }
            }

            foreach (var glyph in descriptor.Glyphs)
            {
                regions.Add(Region(glyph, page, descriptor.BaselineAlignedYOffset(glyph, faceBaselineY)));
            }

            return new BitmapFont(name, regions, face.LineHeight)
            {
                LetterSpacing = face.LetterSpacing,
            };
        }

        private static BitmapFontRegion Region(GlyphFontDescriptor.Glyph glyph, Texture2D page, int yOffset)
        {
            return new BitmapFontRegion(
                new TextureRegion2D(page, glyph.X, glyph.Y, glyph.Width, glyph.Height),
                glyph.Codepoint,
                glyph.XOffset,
                yOffset,
                glyph.XAdvance);
        }
    }
}
