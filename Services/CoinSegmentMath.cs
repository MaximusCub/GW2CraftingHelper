using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure coin/currency segment-width arithmetic (Blish-free,
    /// unit-testable) - moved verbatim out of CoinCurrencyRenderer (M38
    /// WP-21 findings fix) together with the plain data specs
    /// (CoinSegmentSpec/CurrencySegmentSpec) and the geometry constants
    /// (CoinIconSize/CoinLabelIconGap/CoinSegmentGap) the arithmetic is
    /// built from - the specs and constants have no meaning apart from the
    /// formulas below, so they moved with them. An earlier findings-fix
    /// pass on this branch instead added a test file that called
    /// CoinCurrencyRenderer (Views/Rendering, Blish-bound) directly; that
    /// would have violated the repo invariant that tests must never
    /// reference UI code. This extraction is the invariant-compliant fix:
    /// CoinCurrencyRenderer.TotalCoinSegmentsWidth/TotalCurrencySegmentsWidth
    /// now call straight through to this class; everything else that
    /// touches Label/Panel/BitmapFont (BuildCoinSegments, LayoutCoinSegments,
    /// control creation, ...) stays in CoinCurrencyRenderer since it is
    /// genuinely Blish-bound.
    /// </summary>
    public static class CoinSegmentMath
    {
        public const int CoinIconSize = 20;
        public const int CoinLabelIconGap = 2;
        public const int CoinSegmentGap = 6;

        public struct CoinSegmentSpec
        {
            public int AssetId;
            public string Text;
            public int TextWidth;
        }

        public struct CurrencySegmentSpec
        {
            public string IconUrl;
            public string Text;
            public int TextWidth;
        }

        public static int TotalCoinSegmentsWidth(List<CoinSegmentSpec> segments)
        {
            if (segments.Count == 0) return 0;
            int width = 0;
            foreach (var seg in segments)
            {
                width += seg.TextWidth + CoinLabelIconGap + CoinIconSize + CoinSegmentGap;
            }
            return width - CoinSegmentGap;
        }

        /// <summary>
        /// Delegates to ShoppingColumnMath.SegmentRunWidth (the same
        /// "label, gap, icon, gap" formula as TotalCoinSegmentsWidth above,
        /// parameterized) - verbatim from CoinCurrencyRenderer, unchanged.
        /// </summary>
        public static int TotalCurrencySegmentsWidth(List<CurrencySegmentSpec> segments)
        {
            var widths = new List<int>(segments.Count);
            foreach (var seg in segments) widths.Add(seg.TextWidth);
            return ShoppingColumnMath.SegmentRunWidth(widths, CoinIconSize, CoinLabelIconGap, CoinSegmentGap);
        }
    }
}
