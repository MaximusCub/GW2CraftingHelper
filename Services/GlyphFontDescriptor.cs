using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// ref/glyphs.fnt, parsed: the module's shipped glyph font as plain
    /// numbers, plus the two ways those numbers get placed against a line of
    /// Menomonia. Blish-free by design - this is the seam that lets the glyph
    /// font's advances and heights be asserted without a graphics device, so
    /// the sort indicator's geometry is pinned the same way every other piece
    /// of layout arithmetic in this module is.
    /// <para>
    /// Views/Rendering/GlyphFont turns a descriptor plus a Texture2D into a
    /// MonoGame BitmapFont; nothing here knows what a texture is.
    /// </para>
    /// <para>
    /// The file is BMFont's text format, which
    /// tools/build-glyph-font.py emits: strictly one <c>key=value</c> record
    /// per line, no comments. Parsing is deliberately strict - a font that
    /// half-loads is the exact failure this whole exercise exists to stop,
    /// because a codepoint with no region draws nothing AND advances zero
    /// pixels, so neither the screen nor a layout assertion reveals it.
    /// </para>
    /// </summary>
    internal sealed class GlyphFontDescriptor
    {
        private readonly Dictionary<int, Glyph> _glyphs;
        private readonly IReadOnlyList<Glyph> _ordered;

        private GlyphFontDescriptor(
            string pageFile, int pageWidth, int pageHeight, int lineHeight, int baseline,
            Dictionary<int, Glyph> glyphs)
        {
            PageFile = pageFile;
            PageWidth = pageWidth;
            PageHeight = pageHeight;
            LineHeight = lineHeight;
            Baseline = baseline;
            _glyphs = glyphs;

            var ordered = new List<Glyph>(glyphs.Values);
            ordered.Sort((a, b) => a.Codepoint.CompareTo(b.Codepoint));
            _ordered = ordered;
        }

        /// <summary>One glyph's atlas rectangle and its placement metrics.</summary>
        internal readonly struct Glyph
        {
            internal readonly int Codepoint;
            internal readonly int X;
            internal readonly int Y;
            internal readonly int Width;
            internal readonly int Height;
            internal readonly int XOffset;
            internal readonly int YOffset;
            internal readonly int XAdvance;

            internal Glyph(int codepoint, int x, int y, int width, int height, int xOffset, int yOffset, int xAdvance)
            {
                Codepoint = codepoint;
                X = x;
                Y = y;
                Width = width;
                Height = height;
                XOffset = xOffset;
                YOffset = yOffset;
                XAdvance = xAdvance;
            }
        }

        /// <summary>The atlas file name, relative to the module's ref folder.</summary>
        internal string PageFile { get; }

        /// <summary>
        /// The atlas page's dimensions as the generator recorded them. A
        /// glyph rectangle reaching past these samples whatever sits beside
        /// the page in the texture, which draws as garbage rather than as
        /// nothing - the one glyph failure mode that IS visible.
        /// </summary>
        internal int PageWidth { get; }

        internal int PageHeight { get; }

        /// <summary>The font's own line box height.</summary>
        internal int LineHeight { get; }

        /// <summary>
        /// Distance from the top of this font's line box to its baseline.
        /// Only ever read as a DIFFERENCE against the baseline of the face
        /// the glyphs are merged into - see <see cref="BaselineAlignedYOffset"/>
        /// - so its absolute value is a declaration, not a measurement.
        /// </summary>
        internal int Baseline { get; }

        /// <summary>
        /// Every glyph the atlas carries, ordered by codepoint. Sorted once
        /// in the constructor rather than per read: a property that allocates
        /// is a trap for the next caller who puts it in a loop.
        /// </summary>
        internal IReadOnlyList<Glyph> Glyphs => _ordered;

        internal int Count => _glyphs.Count;

        /// <summary>
        /// Reads a BMFont text-format stream. Throws
        /// <see cref="FormatException"/> rather than returning a partial font:
        /// a glyph font missing half its glyphs is invisible in exactly the
        /// way this class exists to prevent.
        /// </summary>
        internal static GlyphFontDescriptor Parse(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            string pageFile = null;
            int pageWidth = 0;
            int pageHeight = 0;
            int lineHeight = 0;
            int baseline = 0;
            bool sawCommon = false;
            var glyphs = new Dictionary<int, Glyph>();

            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    int tagEnd = line.IndexOf(' ');
                    string tag = tagEnd < 0 ? line : line.Substring(0, tagEnd);

                    if (tag == "common")
                    {
                        var fields = ReadFields(line);
                        lineHeight = RequireInt(fields, "lineHeight");
                        baseline = RequireInt(fields, "base");
                        pageWidth = RequireInt(fields, "scaleW");
                        pageHeight = RequireInt(fields, "scaleH");
                        sawCommon = true;
                    }
                    else if (tag == "page")
                    {
                        pageFile = ReadFields(line).TryGetValue("file", out string file)
                            ? file.Trim('"')
                            : null;
                    }
                    else if (tag == "char")
                    {
                        var fields = ReadFields(line);
                        int codepoint = RequireInt(fields, "id");
                        var glyph = new Glyph(
                            codepoint,
                            RequireInt(fields, "x"),
                            RequireInt(fields, "y"),
                            RequireInt(fields, "width"),
                            RequireInt(fields, "height"),
                            RequireInt(fields, "xoffset"),
                            RequireInt(fields, "yoffset"),
                            RequireInt(fields, "xadvance"));

                        if (glyphs.ContainsKey(codepoint))
                        {
                            throw new FormatException(
                                "glyphs.fnt declares U+" + codepoint.ToString("X4", CultureInfo.InvariantCulture)
                                    + " twice.");
                        }

                        glyphs.Add(codepoint, glyph);
                    }
                }
            }

            if (!sawCommon || lineHeight <= 0)
            {
                throw new FormatException("glyphs.fnt has no usable 'common lineHeight=' record.");
            }

            if (string.IsNullOrEmpty(pageFile))
            {
                throw new FormatException("glyphs.fnt names no atlas page.");
            }

            if (glyphs.Count == 0)
            {
                throw new FormatException("glyphs.fnt declares no glyphs.");
            }

            foreach (var glyph in glyphs.Values)
            {
                if (glyph.X < 0 || glyph.Y < 0
                    || glyph.Width <= 0 || glyph.Height <= 0
                    || glyph.X + glyph.Width > pageWidth
                    || glyph.Y + glyph.Height > pageHeight)
                {
                    throw new FormatException(
                        "glyphs.fnt places U+" + glyph.Codepoint.ToString("X4", CultureInfo.InvariantCulture)
                            + " outside its own atlas page.");
                }
            }

            return new GlyphFontDescriptor(pageFile, pageWidth, pageHeight, lineHeight, baseline, glyphs);
        }

        internal bool TryGet(int codepoint, out Glyph glyph)
        {
            return _glyphs.TryGetValue(codepoint, out glyph);
        }

        /// <summary>
        /// Where this glyph's ink starts, in a line box whose baseline sits
        /// <paramref name="targetBaselineY"/> pixels below the top - the
        /// placement for a glyph MERGED into a Menomonia face, where the two
        /// fonts share one line box and must share one baseline.
        /// <para>
        /// The measured Menomonia baselines are in
        /// <see cref="TypeRampMetrics"/>; column headers draw at Bold 20,
        /// whose baseline is 21.
        /// </para>
        /// </summary>
        internal int BaselineAlignedYOffset(in Glyph glyph, int targetBaselineY)
        {
            return glyph.YOffset + (targetBaselineY - Baseline);
        }

        /// <summary>
        /// Where this glyph's ink starts when the font stands ALONE and the
        /// glyph is the whole string - a button label or a bare indicator
        /// Label. There is no neighbouring text to share a baseline with, so
        /// the ink is centred in the line box instead, which is what puts it
        /// on the optical centre of the control drawing it.
        /// </summary>
        internal int BoxCentredYOffset(in Glyph glyph)
        {
            return (LineHeight - glyph.Height) / 2;
        }

        /// <summary>
        /// Pen travel for one glyph, including the negative letter spacing
        /// Blish sets on its own fonts (and which a merged font inherits, so
        /// the glyph pays it too).
        /// </summary>
        internal int AdvanceOf(int codepoint, int letterSpacing)
        {
            return _glyphs.TryGetValue(codepoint, out var glyph) ? glyph.XAdvance + letterSpacing : 0;
        }

        /// <summary>
        /// Width of a run drawn entirely in this font, by MonoGame's own rule:
        /// the pen advances per glyph, and the reported width is the furthest
        /// right edge of any ink, not the final pen position.
        /// </summary>
        internal int MeasureRun(string text, int letterSpacing)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int pen = 0;
            int right = 0;
            foreach (char c in text)
            {
                if (!_glyphs.TryGetValue(c, out var glyph))
                {
                    continue;
                }

                int inkRight = pen + glyph.XOffset + glyph.Width;
                if (inkRight > right)
                {
                    right = inkRight;
                }

                pen += glyph.XAdvance + letterSpacing;
            }

            return right;
        }

        private static Dictionary<string, string> ReadFields(string line)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string token in line.Split(' '))
            {
                int split = token.IndexOf('=');
                if (split > 0)
                {
                    fields[token.Substring(0, split)] = token.Substring(split + 1);
                }
            }

            return fields;
        }

        private static int RequireInt(Dictionary<string, string> fields, string key)
        {
            if (!fields.TryGetValue(key, out string raw)
                || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw new FormatException("glyphs.fnt record is missing an integer '" + key + "'.");
            }

            return value;
        }
    }
}
