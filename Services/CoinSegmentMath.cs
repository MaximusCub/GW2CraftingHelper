using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure coin/currency segment-width arithmetic (Blish-free,
    /// unit-testable) - moved verbatim out of CoinCurrencyRenderer
    /// together with the plain data specs
    /// (CoinSegmentSpec/CurrencySegmentSpec) and the geometry constants
    /// (CoinIconSize/CoinLabelIconGap/CoinSegmentGap) the arithmetic is
    /// built from - the specs and constants have no meaning apart from the
    /// formulas below, so they moved with them. The extraction exists so
    /// the arithmetic is testable without a test referencing UI code
    /// (repo invariant: tests must never reference UI code).
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

        // GW2 coin asset ids (repo CLAUDE.md). Named here because the
        // recipe tree's per-denomination cost columns have to map an
        // already-built segment back to its denomination, which the raw
        // literals made unreadable at that call site.
        public const int GoldAssetId = 156904;
        public const int SilverAssetId = 156907;
        public const int CopperAssetId = 156902;

        /// <summary>What a coin icon says on hover - the one icon whose
        /// subject is a colour and a shape rather than a word. Null for
        /// anything else, the icon component's "no text of my own"
        /// input.</summary>
        public static string DenominationName(int assetId)
        {
            switch (assetId)
            {
                case GoldAssetId: return "Gold";
                case SilverAssetId: return "Silver";
                case CopperAssetId: return "Copper";
                default: return null;
            }
        }

        /// <summary>
        /// The three-way coin split every display site uses: 10000 copper
        /// per gold, 100 per silver. Negative input clamps to 0, matching
        /// the clamp every caller applied before this consolidation - a
        /// negative coin amount is never displayed. Formatting stays with
        /// the callers on purpose: sites legitimately differ (always three
        /// units vs leading-zero units omitted), only the split is shared.
        /// </summary>
        public static (long Gold, long Silver, long Copper) Split(long copper)
        {
            if (copper < 0)
            {
                copper = 0;
            }
            return (copper / 10000, (copper % 10000) / 100, copper % 100);
        }

        /// <summary>
        /// The exact strings a coin amount renders as, per denomination -
        /// null for a leading all-zero unit that is omitted entirely (a
        /// sub-1-gold amount starts at silver, un-padded; copper always
        /// renders, even "0", so a zero total is never a blank cell).
        /// CoinCurrencyRenderer.BuildCoinSegments builds its specs from
        /// this, and the recipe tree's cost-column pre-scan measures the
        /// same strings, so the widths a column reserves can never differ
        /// from the text that lands in it.
        /// </summary>
        public static (string Gold, string Silver, string Copper) FormatSegmentTexts(long copper)
        {
            var (gold, silver, cop) = Split(copper);

            bool showGold = gold > 0;
            bool showSilver = showGold || silver > 0;

            return (
                showGold ? gold.ToString() : null,
                showSilver ? (showGold ? silver.ToString("D2") : silver.ToString()) : null,
                showSilver ? cop.ToString("D2") : cop.ToString());
        }

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

            // Display name of this currency (field-test finding B's name-
            // tooltip sweep principle: anywhere a currency icon shows, its
            // name must be available) - never rendered as text here
            // (width-neutral), only surfaced via the icon's BasicTooltipText
            // in LayoutCurrencySegments. Null/empty is handled the same as
            // every other icon-only cell (no tooltip set at all).
            public string Name;
        }

        /// <summary>
        /// Width of a whole coin run. iconSize defaults to the shared
        /// CoinIconSize every plan table draws at; the rich tooltip passes
        /// its own smaller, line-height-derived size (gap G22) and must
        /// measure with the same number it draws with.
        /// </summary>
        public static int TotalCoinSegmentsWidth(List<CoinSegmentSpec> segments, int iconSize = 0)
        {
            if (segments.Count == 0) return 0;
            int effectiveIcon = iconSize > 0 ? iconSize : CoinIconSize;
            int width = 0;
            foreach (var seg in segments)
            {
                width += seg.TextWidth + CoinLabelIconGap + effectiveIcon + CoinSegmentGap;
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
