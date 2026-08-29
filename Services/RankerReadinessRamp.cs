using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The continuous red -> amber -> green ramp the Crafting Ranker paints its
    /// readiness bars with, and the contrast arithmetic that decides what the
    /// ramp is allowed to be.
    ///
    /// WHITE TEXT SITS ON THIS FILL, which is the whole constraint: WCAG's 4.5:1
    /// floor for #FFFFFF caps every colour on the ramp at a relative luminance of
    /// 1.05 / 4.5 - 0.05 = 0.1833. That is why the three anchors are dark for
    /// their hues, and <see cref="ContrastWithWhite"/> plus the tests over it are
    /// what stop a later "let's brighten it" silently crossing the floor.
    /// Interpolation is in OKLCh, not sRGB, so the intermediate colours stay
    /// orange and olive rather than mud.
    ///
    /// Blish-free on purpose: the module's colour type is XNA's, so the
    /// arithmetic lives here over plain bytes and
    /// <see cref="Views.Rendering.RankerReadinessColors"/> is the one place that
    /// turns a sample into a Color. Derivation: docs/ARCHITECTURE.md,
    /// "Services Q-Z: relocated design narrative".
    /// </summary>
    internal static class RankerReadinessRamp
    {
        /// <summary>A ramp sample, in sRGB bytes.</summary>
        public readonly struct Rgb
        {
            public readonly byte R;
            public readonly byte G;
            public readonly byte B;

            public Rgb(byte r, byte g, byte b)
            {
                R = r;
                G = g;
                B = b;
            }
        }

        /// <summary>
        /// The WCAG 2.x contrast ratio this ramp is held to against white.
        /// Aimed at rather than merely met: the worst point on the whole
        /// sweep measures 5.07:1, at 54%.
        /// </summary>
        public const double WhiteTextContrastFloor = 4.5;

        /// <summary>0% - nothing done yet. Contrast with white 7.12:1.</summary>
        public static readonly Rgb Empty = new Rgb(166, 40, 34);

        /// <summary>50% - the midpoint anchor. Contrast with white 5.08:1.</summary>
        public static readonly Rgb Half = new Rgb(142, 104, 14);

        /// <summary>100% - done. Contrast with white 5.22:1.</summary>
        public static readonly Rgb Full = new Rgb(42, 124, 48);

        /// <summary>
        /// The unfilled part of a bar, which carries TWO contrast obligations,
        /// not one. Against white, because a low fill leaves the centred
        /// percentage sitting over the track rather than over the ramp: 19.42:1.
        /// Against the PANEL BEHIND IT, because a track nobody can see is not a
        /// bar - it is a floating coloured block with no scale, and the
        /// percentage beside it reads as detached.
        /// <para>
        /// Darker is the only direction that serves both: reaching 3:1 against
        /// the panel needs roughly Rgb(110), which drops white-text contrast to
        /// ~3.5:1, under <see cref="WhiteTextContrastFloor"/>. Pure black is the
        /// ceiling at 1.42:1; this sits at 1.32:1.
        /// </para>
        /// <para>
        /// How the second obligation was found, and the measurement behind it:
        /// docs/ARCHITECTURE.md, "Services Q-Z: relocated design narrative".
        /// </para>
        /// </summary>
        public static readonly Rgb Track = new Rgb(14, 13, 12);

        /// <summary>
        /// The Blish window panel a readiness bar is drawn on, sampled in
        /// game at 3440x1440 beside and behind the Ranker's bars. Not a
        /// colour this module paints - a measurement of the surface it
        /// paints onto, kept so <c>Track</c> can be held apart from it.
        /// The two samples were Rgb(38, 36, 34) and Rgb(42, 42, 41); this
        /// is the lighter, which is the harder case for a dark track.
        /// </summary>
        public static readonly Rgb PanelReference = new Rgb(42, 42, 41);

        /// <summary>
        /// Ramp colour for a 0..1 readiness fraction, clamped. Two OKLCh
        /// segments (Empty..Half, Half..Full) rather than one, because the
        /// amber the eye expects at 50% is not on the direct red-to-green
        /// hue arc at any chroma.
        /// </summary>
        public static Rgb Fill(double fraction)
        {
            if (double.IsNaN(fraction) || fraction <= 0.0)
            {
                return Empty;
            }

            if (fraction >= 1.0)
            {
                return Full;
            }

            if (fraction < 0.5)
            {
                return Blend(Empty, Half, fraction / 0.5);
            }

            if (fraction > 0.5)
            {
                return Blend(Half, Full, (fraction - 0.5) / 0.5);
            }

            return Half;
        }

        /// <summary>
        /// How many pixels of a bar of <paramref name="barWidth"/> are
        /// painted at <paramref name="fraction"/>. Rounded, then held off
        /// both ends: a measured non-zero readiness never paints zero
        /// pixels (it would read as "not started"), and a readiness under
        /// 100% never paints a full bar (it would read as "done").
        /// </summary>
        public static int FillWidth(int barWidth, double fraction)
        {
            if (barWidth <= 0 || double.IsNaN(fraction) || fraction <= 0.0)
            {
                return 0;
            }

            if (fraction >= 1.0)
            {
                return barWidth;
            }

            int width = (int)Math.Round(barWidth * fraction, MidpointRounding.AwayFromZero);
            if (width <= 0)
            {
                return 1;
            }

            return width >= barWidth ? barWidth - 1 : width;
        }

        /// <summary>WCAG 2.x relative luminance of an sRGB sample.</summary>
        public static double RelativeLuminance(Rgb color)
        {
            return (0.2126 * LinearOf(color.R))
                + (0.7152 * LinearOf(color.G))
                + (0.0722 * LinearOf(color.B));
        }

        /// <summary>
        /// WCAG 2.x contrast ratio of white text over this sample. White is
        /// the lighter of the pair by construction, so the ratio is
        /// 1.05 / (L + 0.05).
        /// </summary>
        public static double ContrastWithWhite(Rgb background)
        {
            return 1.05 / (RelativeLuminance(background) + 0.05);
        }

        /// <summary>
        /// WCAG contrast between any two colours, lighter over darker. Used
        /// for the track-against-panel obligation, where neither side is
        /// white and <see cref="ContrastWithWhite"/> cannot answer.
        /// </summary>
        public static double ContrastRatio(Rgb a, Rgb b)
        {
            double la = RelativeLuminance(a);
            double lb = RelativeLuminance(b);
            double hi = la > lb ? la : lb;
            double lo = la > lb ? lb : la;
            return (hi + 0.05) / (lo + 0.05);
        }

        private static Rgb Blend(Rgb from, Rgb to, double u)
        {
            ToOklch(from, out double l0, out double c0, out double h0);
            ToOklch(to, out double l1, out double c1, out double h1);

            // No shortest-arc wrap handling: the three anchors run 29.0 ->
            // 82.8 -> 144.5 degrees, strictly increasing and never crossing
            // 0/360, so a plain lerp IS the short way round. A fourth anchor
            // would have to keep that true or teach this to wrap.
            return FromOklch(
                l0 + ((l1 - l0) * u),
                c0 + ((c1 - c0) * u),
                h0 + ((h1 - h0) * u));
        }

        private static void ToOklch(Rgb color, out double l, out double c, out double h)
        {
            double r = LinearOf(color.R);
            double g = LinearOf(color.G);
            double b = LinearOf(color.B);

            double lp = Math.Pow((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b), 1.0 / 3.0);
            double mp = Math.Pow((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b), 1.0 / 3.0);
            double sp = Math.Pow((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b), 1.0 / 3.0);

            l = (0.2104542553 * lp) + (0.7936177850 * mp) - (0.0040720468 * sp);
            double a = (1.9779984951 * lp) - (2.4285922050 * mp) + (0.4505937099 * sp);
            double bb = (0.0259040371 * lp) + (0.7827717662 * mp) - (0.8086757660 * sp);

            c = Math.Sqrt((a * a) + (bb * bb));
            h = Math.Atan2(bb, a);
        }

        private static Rgb FromOklch(double l, double c, double h)
        {
            double a = c * Math.Cos(h);
            double b = c * Math.Sin(h);

            double lp = l + (0.3963377774 * a) + (0.2158037573 * b);
            double mp = l - (0.1055613458 * a) - (0.0638541728 * b);
            double sp = l - (0.0894841775 * a) - (1.2914855480 * b);

            double lc = lp * lp * lp;
            double mc = mp * mp * mp;
            double sc = sp * sp * sp;

            return new Rgb(
                ByteOf((4.0767416621 * lc) - (3.3077115913 * mc) + (0.2309699292 * sc)),
                ByteOf((-1.2684380046 * lc) + (2.6097574011 * mc) - (0.3413193965 * sc)),
                ByteOf((-0.0041960863 * lc) - (0.7034186147 * mc) + (1.7076147010 * sc)));
        }

        private static double LinearOf(byte channel)
        {
            double v = channel / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        private static byte ByteOf(double linear)
        {
            double clamped = linear <= 0.0 ? 0.0 : linear >= 1.0 ? 1.0 : linear;
            double encoded = clamped <= 0.0031308
                ? clamped * 12.92
                : (1.055 * Math.Pow(clamped, 1.0 / 2.4)) - 0.055;
            return (byte)Math.Round(encoded * 255.0, MidpointRounding.AwayFromZero);
        }
    }
}
