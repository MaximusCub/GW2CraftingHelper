using TaimisToolbench.Services;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The module's shipped glyph font, read from the SAME ref/glyphs.fnt the
    /// .bhm packages (copied to the test output by this project's csproj) and
    /// through the same parser the module loads it with.
    /// <para>
    /// This is the whole reason GlyphFontDescriptor is Blish-free. Glyph
    /// metrics feed the width and height arithmetic that this suite pins
    /// everywhere else, and a codepoint MonoGame cannot resolve draws nothing
    /// AND advances zero pixels - so a font that silently lost a glyph, or
    /// gained a mismatched one, would show up in neither a screenshot nor a
    /// layout assertion. These tests are the only thing that can see it.
    /// </para>
    /// </summary>
    public class GlyphFontDescriptorTests
    {
        /// <summary>
        /// Blish sets LetterSpacing = -1 on every stock font, and a font
        /// merged into one inherits it, so the glyphs pay it too.
        /// </summary>
        private const int MenomoniaLetterSpacing = -1;

        private const int SortAscending = 0xE100;
        private const int SortDescending = 0xE101;
        private const int CaretUp = 0xE102;
        private const int CaretDown = 0xE103;
        private const int CaretRight = 0xE104;

        /// <summary>Fragments for the malformed-input cases below.</summary>
        private const string Common = "common lineHeight=24 base=18 scaleW=21 scaleH=8\n";

        private const string Char57600 =
            "char id=57600 x=1 y=1 width=9 height=6 xoffset=0 yoffset=7 xadvance=9\n";

        private static GlyphFontDescriptor Shipped()
        {
            using (var stream = File.OpenRead(Path.Combine("ref", "glyphs.fnt")))
            {
                return GlyphFontDescriptor.Parse(stream);
            }
        }

        [Fact]
        public void ShippedFont_CarriesExactlyTheCodepointsTheModuleNames()
        {
            var font = Shipped();

            // Both directions on purpose. A glyph the code names and the
            // atlas lacks is an invisible control; a glyph the atlas carries
            // and no code names is a vocabulary nothing re-measures when it
            // drifts. Services/UiGlyphs is the list, and the "UI glyph
            // escapes" CI step is the other half of the same gate.
            Assert.Equal(5, font.Count);
            Assert.True(font.TryGet(SortAscending, out _));
            Assert.True(font.TryGet(SortDescending, out _));
            Assert.True(font.TryGet(CaretUp, out _));
            Assert.True(font.TryGet(CaretDown, out _));
            Assert.True(font.TryGet(CaretRight, out _));
            Assert.Equal(UiGlyphs.SortAscending, char.ConvertFromUtf32(SortAscending));
            Assert.Equal(UiGlyphs.SortDescending, char.ConvertFromUtf32(SortDescending));
            Assert.Equal(UiGlyphs.CaretUp, char.ConvertFromUtf32(CaretUp));
            Assert.Equal(UiGlyphs.CaretDown, char.ConvertFromUtf32(CaretDown));
            Assert.Equal(UiGlyphs.CaretRight, char.ConvertFromUtf32(CaretRight));
        }

        [Fact]
        public void TheReadingSizeCarets_AreAMatchedTrio()
        {
            var font = Shipped();
            Assert.True(font.TryGet(CaretUp, out var up));
            Assert.True(font.TryGet(CaretDown, out var down));
            Assert.True(font.TryGet(CaretRight, out var right));

            // Up and down are one artwork mirrored, so they must agree on
            // every metric or an expand/collapse toggle moves under the
            // cursor.
            Assert.Equal(up.Width, down.Width);
            Assert.Equal(up.Height, down.Height);
            Assert.Equal(up.XAdvance, down.XAdvance);
            Assert.Equal(up.YOffset, down.YOffset);

            // The right caret is the same ink AREA turned on its side rather
            // than the same bounding box, which is what stops it reading as
            // the lighter member of the set beside its own partner.
            Assert.Equal(up.Width * up.Height, right.Width * right.Height);

            // A separate SIZE from the sort pair, which is why it is a
            // separate pair of codepoints - see UiGlyphs.CaretUp.
            Assert.True(font.TryGet(SortAscending, out var sortUp));
            Assert.True(up.Height > sortUp.Height);
        }

        [Theory]
        [InlineData(true, "v")]
        [InlineData(false, ">")]
        public void WithoutTheAtlas_ACaretDegradesToTheAsciiItReplaced(bool expanded, string ascii)
        {
            Assert.Equal(ascii, UiGlyphs.ExpandCaret(expanded, glyphsAvailable: false));

            string glyph = UiGlyphs.ExpandCaret(expanded, glyphsAvailable: true);
            Assert.NotEqual(ascii, glyph);
            Assert.True(Shipped().TryGet(char.ConvertToUtf32(glyph, 0), out _));
        }

        [Fact]
        public void SortIndicators_AreAMatchedPair()
        {
            var font = Shipped();
            Assert.True(font.TryGet(SortAscending, out var up));
            Assert.True(font.TryGet(SortDescending, out var down));

            // The defect this font exists to fix, stated as an assertion.
            // Menomonia's "^" is a circumflex - 10x7 ink, 3px down the line
            // box, 8px advance - and its "v" is a lowercase letter - 11x11
            // ink, 6px down, 9px advance. Mismatched in height, in advance
            // and in seat. The replacement pair must not be.
            Assert.Equal(up.Width, down.Width);
            Assert.Equal(up.Height, down.Height);
            Assert.Equal(up.XAdvance, down.XAdvance);
            Assert.Equal(up.XOffset, down.XOffset);
            Assert.Equal(up.YOffset, down.YOffset);
        }

        [Fact]
        public void SortIndicators_AdvanceEquallyAndMeasureEqually()
        {
            var font = Shipped();

            int up = font.AdvanceOf(SortAscending, MenomoniaLetterSpacing);
            int down = font.AdvanceOf(SortDescending, MenomoniaLetterSpacing);

            Assert.Equal(up, down);

            // Load-bearing: nine call sites measure a header string that may
            // or may not carry an indicator, and four of them floor a whole
            // column's geometry on the result (Used Materials' Amount band,
            // Shopping List's Amount and Source bands, the snapshot grid's
            // Amount band). If the two directions measured differently, a
            // table's columns would jump on the second click of a sort.
            Assert.Equal(
                font.MeasureRun(UiGlyphs.SortAscending, MenomoniaLetterSpacing),
                font.MeasureRun(UiGlyphs.SortDescending, MenomoniaLetterSpacing));
            Assert.True(font.MeasureRun(UiGlyphs.SortAscending, MenomoniaLetterSpacing) > 0);
        }

        [Fact]
        public void MergedIntoColumnHeader_TheInkLandsInsideTheHeaderBandItAlreadyHas()
        {
            var font = Shipped();
            var header = TypeRampMetrics.ColumnHeaderInk;
            Assert.True(font.TryGet(SortAscending, out var glyph));

            int top = font.BaselineAlignedYOffset(glyph, header.BaselineY);
            int bottom = top + glyph.Height;

            // No band grows. The header row's height and every divider
            // clearance under it are derived from CapTopY and LowestInk
            // (Services/PlanContentHeightMath via TypeRampMetrics), so an
            // indicator that reached above the caps or below the descenders
            // would silently change the height of six tables.
            Assert.True(top >= header.CapTopY, "indicator ink starts above the cap line");
            Assert.True(bottom <= header.LowestInk, "indicator ink hangs below the descender floor");

            // And it reads as a mark on the caps rather than a subscript:
            // its centre sits within a pixel of the cap band's centre.
            int glyphCentre = top + (glyph.Height / 2);
            int capCentre = (header.CapTopY + header.BaselineY) / 2;
            Assert.InRange(glyphCentre - capCentre, -1, 1);
        }

        [Fact]
        public void StandaloneSeat_CentresTheInkInItsLineBox()
        {
            var font = Shipped();
            Assert.True(font.TryGet(SortDescending, out var glyph));

            int top = font.BoxCentredYOffset(glyph);

            // A control whose whole label is one glyph has no neighbouring
            // text to share a baseline with, so the ink centres on the line
            // box instead - which is what puts it on the optical centre of
            // the button or label drawing it.
            Assert.Equal(font.LineHeight - top - glyph.Height, top);
        }

        [Fact]
        public void AtlasRectanglesStayInsideThePage()
        {
            var font = Shipped();

            // A region reaching past the page samples whatever is next to it
            // in the texture, which draws as garbage rather than as nothing -
            // the one glyph failure mode that IS visible, and worth catching
            // in the generator's output rather than on screen.
            foreach (var glyph in font.Glyphs)
            {
                Assert.InRange(glyph.X, 0, font.PageWidth - glyph.Width);
                Assert.InRange(glyph.Y, 0, font.PageHeight - glyph.Height);
                Assert.True(glyph.Width > 0 && glyph.Height > 0);
                Assert.True(glyph.Height <= font.LineHeight);
            }
        }

        [Fact]
        public void PageFileNamesSomethingTheModulePackages()
        {
            var font = Shipped();

            Assert.Equal("glyphs_0.png", font.PageFile);
            Assert.True(File.Exists(Path.Combine("ref", font.PageFile)));
        }

        [Theory]
        // No 'common' record at all.
        [InlineData("page id=0 file=\"glyphs_0.png\"\nchars count=0\n")]
        // A 'common' record but no glyphs.
        [InlineData(Common + "page id=0 file=\"glyphs_0.png\"\n")]
        // Glyphs but no atlas page to draw them from.
        [InlineData(Common + Char57600)]
        // A glyph rectangle that reaches past the page it names.
        [InlineData(Common + "page id=0 file=\"p.png\"\n"
            + "char id=57600 x=1 y=1 width=99 height=6 xoffset=0 yoffset=7 xadvance=9\n")]
        // A zero-area glyph, which would draw nothing while still measuring.
        [InlineData(Common + "page id=0 file=\"p.png\"\n"
            + "char id=57600 x=1 y=1 width=0 height=6 xoffset=0 yoffset=7 xadvance=9\n")]
        public void PartialFontsAreRefused(string text)
        {
            // Strict on purpose. A half-loaded glyph font is exactly the
            // failure this whole exercise exists to stop: the missing
            // codepoints draw nothing, advance nothing, and measure nothing.
            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes(text)))
            {
                Assert.Throws<FormatException>(() => GlyphFontDescriptor.Parse(stream));
            }
        }

        [Fact]
        public void DuplicateGlyphIsRefused()
        {
            string text = Common + "page id=0 file=\"p.png\"\n" + Char57600 + Char57600;

            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes(text)))
            {
                Assert.Throws<FormatException>(() => GlyphFontDescriptor.Parse(stream));
            }
        }

        [Fact]
        public void UnknownCodepointsMeasureAsNothing()
        {
            var font = Shipped();

            // Documenting MonoGame's own behaviour at the seam that models
            // it: a codepoint with no region is skipped by the blit AND by
            // the advance, so it costs zero width. That is why the ASCII
            // fallback below exists rather than trusting the font to be
            // there.
            Assert.Equal(0, font.AdvanceOf('A', MenomoniaLetterSpacing));
            Assert.Equal(0, font.MeasureRun("A", MenomoniaLetterSpacing));
            Assert.Equal(0, font.MeasureRun(null, MenomoniaLetterSpacing));
        }

        [Fact]
        public void EveryShippedGlyphHasAnAsciiFallback()
        {
            var font = Shipped();

            foreach (var glyph in font.Glyphs)
            {
                string text = char.ConvertFromUtf32(glyph.Codepoint);
                string fallback = UiGlyphs.AsciiFallback(text);

                // Total over the shipped set: a glyph with no fallback would
                // vanish on the corrupt-install path instead of degrading.
                Assert.NotEqual(text, fallback);
                Assert.All(fallback, c => Assert.InRange(c, ' ', '~'));
            }
        }
    }
}
