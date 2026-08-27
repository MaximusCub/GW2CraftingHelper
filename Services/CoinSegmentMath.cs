using System.Collections.Generic;
using System.Text;

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
    internal static class CoinSegmentMath
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
        /// negative coin amount is never displayed.
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
        /// sub-1-gold amount starts at silver; copper always renders, even
        /// "0", so a zero total is never a blank cell). No zero-padding
        /// anywhere: the game renders trailing units as bare digits -
        /// MEASURED "2g 0s 0c" on live3 counterfeit-ticket (20000 copper)
        /// and "2s 0c" on relic-livingcity (200 copper), 2026-08-26, where
        /// the old "D2" would have printed "00". A non-zero sub-10 segment
        /// ("2s 5c") has no capture; bare digits are INFERRED from the
        /// zero samples. CoinCurrencyRenderer.BuildCoinSegments builds its
        /// specs from this, and the recipe tree's cost-column pre-scan
        /// measures the same strings, so the widths a column reserves can
        /// never differ from the text that lands in it.
        /// </summary>
        public static (string Gold, string Silver, string Copper) FormatSegmentTexts(long copper)
        {
            var (gold, silver, cop) = Split(copper);

            bool showGold = gold > 0;
            bool showSilver = showGold || silver > 0;

            return (
                showGold ? gold.ToString() : null,
                showSilver ? silver.ToString() : null,
                cop.ToString());
        }

        /// <summary>
        /// A coin amount as one plain string, spelled exactly the way the
        /// icons spell it: leading all-zero units omitted, trailing units
        /// as bare digits. 1005 copper is "10s 5c", never "0g 10s 05c" -
        /// the game prints "10c", not "0g 0s 10c", and "2g 0s 0c" with
        /// single zeros (measured, live3 counterfeit-ticket).
        ///
        /// <para>
        /// The module's ONE plain coin format. Four composers used to keep
        /// a private copy of this and three of them spelled it differently;
        /// each copy cited the same reason, that CoinCurrencyRenderer lives
        /// in Views.Rendering and a Blish-free class cannot reference it.
        /// The layer rule is real - it is why this method is here and not
        /// there - but the conclusion was not: nothing stopped the format
        /// living beside the split it is derived from.
        /// </para>
        /// </summary>
        public static string GameStyleText(long copper)
        {
            var (gold, silver, cop) = FormatSegmentTexts(copper);
            var sb = new StringBuilder(16);
            if (gold != null)
            {
                sb.Append(gold).Append("g ");
            }

            if (silver != null)
            {
                sb.Append(silver).Append("s ");
            }

            return sb.Append(cop).Append('c').ToString();
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
            if (segments.Count == 0)
            {
                return 0;
            }

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
            foreach (var seg in segments)
            {
                widths.Add(seg.TextWidth);
            }

            return ShoppingColumnMath.SegmentRunWidth(widths, CoinIconSize, CoinLabelIconGap, CoinSegmentGap);
        }
    }
}
