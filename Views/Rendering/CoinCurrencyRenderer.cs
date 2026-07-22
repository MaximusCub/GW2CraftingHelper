using Blish_HUD.Content;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-21 (Tier-1 static renderer extraction, m38-a1-architecture.md
    // S3b-T1): moved verbatim out of CraftingPlanView's "10. Coin/currency
    // value rendering primitives" region - private static -> internal
    // static, no logic changes. The coin-icon-right-of-number invariant
    // (repo CLAUDE.md) now lives in this one named place. Callers in
    // CraftingPlanView repoint through this class name (e.g.
    // CoinCurrencyRenderer.RenderValueCellRightAligned).
    internal static class CoinCurrencyRenderer
    {
        // Plain "12g 34s 56c" text for contexts that cannot render coin
        // icons (BasicTooltipText has no inline-image support).
        internal static string FormatCoinText(long copper)
        {
            if (copper < 0) copper = 0;
            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;
            return $"{gold}g {silver}s {cop}c";
        }

        // --- Coin display helpers ---
        //
        // gw2e's Coins component renders NumberFormat(gold) -> icon ->
        // NumberFormat(silver, zero-padded once gold precedes it) -> icon ->
        // NumberFormat(copper, zero-padded once silver precedes it) -> icon,
        // omitting leading all-zero units (a sub-1-gold amount starts at
        // silver, un-padded). Segments are measured up front so the same
        // spec list can be laid out left-anchored, right-anchored (table
        // price columns), or centered (cost tiles) without re-measuring.

        internal const int CoinIconSize = 20;
        internal const int CoinLabelIconGap = 2;
        internal const int CoinSegmentGap = 6;

        internal struct CoinSegmentSpec
        {
            public int AssetId;
            public string Text;
            public int TextWidth;
        }

        internal static List<CoinSegmentSpec> BuildCoinSegments(long copper, BitmapFont font)
        {
            if (copper < 0) copper = 0;

            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;

            bool showGold = gold > 0;
            bool showSilver = showGold || silver > 0;

            var segments = new List<CoinSegmentSpec>(3);
            if (showGold)
            {
                AddSegmentSpec(segments, font, 156904, gold.ToString());
            }
            if (showSilver)
            {
                AddSegmentSpec(segments, font, 156907, showGold ? silver.ToString("D2") : silver.ToString());
            }
            // Copper always renders (even "0") so a zero total is never a blank row.
            AddSegmentSpec(segments, font, 156902, showSilver ? cop.ToString("D2") : cop.ToString());
            return segments;
        }

        private static void AddSegmentSpec(List<CoinSegmentSpec> segments, BitmapFont font, int assetId, string text)
        {
            int width = (int)System.Math.Ceiling(font.MeasureString(text).Width);
            segments.Add(new CoinSegmentSpec { AssetId = assetId, Text = text, TextWidth = width });
        }

        internal static int TotalCoinSegmentsWidth(List<CoinSegmentSpec> segments)
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
        /// M33 C2b: a coin/currency segment run's already-created controls
        /// plus each segment's cached (font-only, panelWidth-invariant)
        /// text width, so a relayout closure can reposition the whole run
        /// at a new x without ever calling MeasureString again - see
        /// RepositionSegments. Controls/TextWidths are always the same
        /// length and share indices.
        /// </summary>
        internal struct SegmentLayoutHandle
        {
            public (Label Label, Panel Icon)[] Controls;
            public int[] TextWidths;

            public static readonly SegmentLayoutHandle Empty =
                new SegmentLayoutHandle { Controls = System.Array.Empty<(Label, Panel)>(), TextWidths = System.Array.Empty<int>() };
        }

        /// <summary>
        /// Lays out coin segments left-to-right starting at x. alphaScale
        /// dims the number labels (not the icons - Panel has no tint
        /// property) for dimmed not-crafted subtree rows.
        /// </summary>
        internal static SegmentLayoutHandle LayoutCoinSegments(
            Panel parent, List<CoinSegmentSpec> segments, int startX, int y, BitmapFont font, float alphaScale = 1f)
        {
            var controls = new (Label, Panel)[segments.Count];
            var widths = new int[segments.Count];
            int x = startX;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                Color textColor = GetCoinColor(seg.AssetId);
                if (alphaScale < 1f) textColor *= alphaScale;

                var label = new Label()
                {
                    Text = seg.Text,
                    Font = font,
                    TextColor = textColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(x, y),
                    Parent = parent
                };

                var icon = new Panel()
                {
                    Size = new Point(CoinIconSize, CoinIconSize),
                    Location = new Point(x + seg.TextWidth + CoinLabelIconGap, y),
                    BackgroundTexture = AsyncTexture2D.FromAssetId(seg.AssetId),
                    Parent = parent
                };

                controls[i] = (label, icon);
                widths[i] = seg.TextWidth;
                x += seg.TextWidth + CoinLabelIconGap + CoinIconSize + CoinSegmentGap;
            }

            return new SegmentLayoutHandle { Controls = controls, TextWidths = widths };
        }

        /// <summary>
        /// M33 C2b: non-allocating reposition twin to LayoutCoinSegments/
        /// LayoutCurrencySegments (m2 3.7/4) - moves EXISTING segment
        /// controls to new x-positions using the cached TextWidths, never
        /// creating a control or calling MeasureString. Shared by both coin
        /// and currency segment runs since they follow the identical
        /// "label, gap, icon, gap" geometry (same CoinIconSize/
        /// CoinLabelIconGap/CoinSegmentGap constants).
        /// </summary>
        internal static void RepositionSegments(SegmentLayoutHandle handle, int startX, int y)
        {
            int x = startX;
            for (int i = 0; i < handle.Controls.Length; i++)
            {
                var (label, icon) = handle.Controls[i];
                int textWidth = handle.TextWidths[i];
                label.Location = new Point(x, y);
                icon.Location = new Point(x + textWidth + CoinLabelIconGap, y);
                x += textWidth + CoinLabelIconGap + CoinIconSize + CoinSegmentGap;
            }
        }

        private static Color GetCoinColor(int assetId)
        {
            switch (assetId)
            {
                case 156904: return new Color(255, 204, 0);
                case 156907: return new Color(192, 192, 192);
                case 156902: return new Color(205, 127, 50);
                default: return Color.White;
            }
        }

        // --- Currency + mixed value display helpers (KNOWN-ISSUES #16) ---
        //
        // A BuyFromVendor decision can be priced wholly or partly in a
        // non-coin currency (spirit shards, karma, ...). CurrencyAmountViewModel
        // (shopping rows, via PlanViewModelBuilder) and CraftingTreeNode.
        // VendorCurrencyCosts (tree, resolved here via CurrencyDisplayResolver)
        // both feed the same rendering below, so the two sibling sites named
        // in KNOWN-ISSUES #16 (shopping Each/Total cells and the tree cost
        // column) can never drift apart. Currency segments follow the exact
        // same "amount label, then icon to the RIGHT" convention as coin
        // segments (the coin invariant) and reuse its icon size/gaps; a
        // mixed value renders coin segments first, then currency segments.
        // A value with neither a coin price nor a currency cost is
        // genuinely unpriceable (gw2e: "Not sold or crafted") and renders a
        // plain dash - never a blank cell, never an invented "0".

        // ASCII-only source rule: em dash via escape, never a raw pasted
        // Unicode character - this IS the gw2e-style unpriceable dash
        // itself (KNOWN-ISSUES #16b), not incidental prose.
        private const string UnpricedDashText = "\u2014";
        private static readonly Color UnpricedDashColor = new Color(140, 140, 140);

        internal struct CurrencySegmentSpec
        {
            public string IconUrl;
            public string Text;
            public int TextWidth;
        }

        internal static List<CurrencySegmentSpec> BuildCurrencySegments(
            IReadOnlyList<CurrencyAmountViewModel> amounts, BitmapFont font)
        {
            var segments = new List<CurrencySegmentSpec>();
            if (amounts == null) return segments;

            foreach (var amount in amounts)
            {
                // M34-B1 #2: a fractional-per-unit "Each" amount carries a
                // literal "N for M" bundle label instead of a whole-number
                // Amount (CurrencyDisplayResolver.ResolveUnitAmounts) -
                // render that text verbatim rather than the numeric amount.
                string text = amount.BundleLabel ?? amount.Amount.ToString();
                int width = (int)System.Math.Ceiling(font.MeasureString(text).Width);
                segments.Add(new CurrencySegmentSpec { IconUrl = amount.IconUrl, Text = text, TextWidth = width });
            }
            return segments;
        }

        /// <summary>
        /// The actual width arithmetic lives in ShoppingColumnMath
        /// (Blish-free, tested) so the pre-scan (MeasureValueWidth) and the
        /// real layout (LayoutValueSegmentsRightAligned) below can never
        /// drift apart; only the per-segment text measurement
        /// (BitmapFont.MeasureString) is Blish-bound and stays here.
        /// </summary>
        internal static int TotalCurrencySegmentsWidth(List<CurrencySegmentSpec> segments)
        {
            var widths = new List<int>(segments.Count);
            foreach (var seg in segments) widths.Add(seg.TextWidth);
            return ShoppingColumnMath.SegmentRunWidth(widths, CoinIconSize, CoinLabelIconGap, CoinSegmentGap);
        }

        internal static SegmentLayoutHandle LayoutCurrencySegments(
            Panel parent, List<CurrencySegmentSpec> segments, int startX, int y, BitmapFont font, float alphaScale = 1f)
        {
            var controls = new (Label, Panel)[segments.Count];
            var widths = new int[segments.Count];
            int x = startX;
            Color textColor = new Color(220, 220, 220);
            if (alphaScale < 1f) textColor *= alphaScale;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                var label = new Label()
                {
                    Text = seg.Text,
                    Font = font,
                    TextColor = textColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(x, y),
                    Parent = parent
                };

                var icon = IconControls.CreateItemIcon(parent, seg.IconUrl, x + seg.TextWidth + CoinLabelIconGap, y, CoinIconSize);

                controls[i] = (label, icon);
                widths[i] = seg.TextWidth;
                x += seg.TextWidth + CoinLabelIconGap + CoinIconSize + CoinSegmentGap;
            }

            return new SegmentLayoutHandle { Controls = controls, TextWidths = widths };
        }

        /// <summary>
        /// Pixel width a coin/currency/mixed value would occupy if laid out
        /// via LayoutValueSegmentsRightAligned - built from the exact same
        /// BuildCoinSegments/BuildCurrencySegments + Total*SegmentsWidth
        /// path that layout call uses, so the shopping list's pre-scan
        /// (CreateShoppingListBody) can never drift from what actually
        /// renders. copper == 0 with at least one currency amount is a
        /// valid, currency-only case (not a "zero width" special case).
        /// </summary>
        internal static int MeasureValueWidth(
            long copper, IReadOnlyList<CurrencyAmountViewModel> currencyAmounts, BitmapFont font)
        {
            int coinWidth = copper > 0 ? TotalCoinSegmentsWidth(BuildCoinSegments(copper, font)) : 0;
            int currencyWidth = TotalCurrencySegmentsWidth(BuildCurrencySegments(currencyAmounts, font));
            return (coinWidth > 0 && currencyWidth > 0) ? coinWidth + CoinSegmentGap + currencyWidth : coinWidth + currencyWidth;
        }

        /// <summary>
        /// M33 C2b: everything a relayout closure needs to reposition an
        /// already-rendered value cell (RenderValueCellRightAligned's
        /// result) at a new rightEdgeX without any MeasureString call -
        /// either DashLabel is set (genuinely unpriceable row) or the two
        /// SegmentLayoutHandles are (each individually empty when that half
        /// of the mix is absent - e.g. CoinSegments.Controls.Length == 0
        /// for a currency-only row).
        /// </summary>
        internal sealed class ValueCellHandle
        {
            public SegmentLayoutHandle CoinSegments;
            public SegmentLayoutHandle CurrencySegments;
            public Label DashLabel;
        }

        /// <summary>
        /// Right-aligns coin segments (if copper &gt; 0) followed by
        /// currency segments (if any) to rightEdgeX - the "mixed
        /// coin+currency renders coin segments then currency segments"
        /// rule. Callers must not invoke this for a value with neither
        /// (RenderValueCellRightAligned handles that dash case instead).
        /// </summary>
        internal static ValueCellHandle LayoutValueSegmentsRightAligned(
            Panel parent, long copper, IReadOnlyList<CurrencyAmountViewModel> currencyAmounts,
            int rightEdgeX, int y, BitmapFont font, float alphaScale = 1f)
        {
            var coinSegments = copper > 0 ? BuildCoinSegments(copper, font) : new List<CoinSegmentSpec>();
            var currencySegments = BuildCurrencySegments(currencyAmounts, font);
            int coinWidth = TotalCoinSegmentsWidth(coinSegments);
            int currencyWidth = TotalCurrencySegmentsWidth(currencySegments);
            int gap = (coinWidth > 0 && currencyWidth > 0) ? CoinSegmentGap : 0;

            int startX = rightEdgeX - (coinWidth + gap + currencyWidth);
            var coinHandle = LayoutCoinSegments(parent, coinSegments, startX, y, font, alphaScale);
            var currencyHandle = LayoutCurrencySegments(parent, currencySegments, startX + coinWidth + gap, y, font, alphaScale);

            return new ValueCellHandle { CoinSegments = coinHandle, CurrencySegments = currencyHandle };
        }

        /// <summary>
        /// Single entry point for a shopping/tree value cell: coin-only,
        /// currency-only, and mixed all render via
        /// LayoutValueSegmentsRightAligned unchanged from (or, for
        /// currency/mixed, newly matching) the coin invariant; a value with
        /// neither a coin price nor a currency cost renders a plain dash
        /// instead of a blank cell or an invented "0" (KNOWN-ISSUES #16b).
        /// Returns a handle so a relayout closure can reposition the cell
        /// at a new rightEdgeX later - see RepositionValueCellRightAligned.
        /// </summary>
        internal static ValueCellHandle RenderValueCellRightAligned(
            Panel parent, long copper, IReadOnlyList<CurrencyAmountViewModel> currencyAmounts,
            int rightEdgeX, int y, BitmapFont font, float alphaScale = 1f)
        {
            bool hasCoin = copper > 0;
            bool hasCurrency = currencyAmounts != null && currencyAmounts.Count > 0;

            if (!hasCoin && !hasCurrency)
            {
                Color dashColor = alphaScale < 1f ? UnpricedDashColor * alphaScale : UnpricedDashColor;
                var dashLabel = LabelHelpers.CreateRightAlignedLabel(parent, UnpricedDashText, font, dashColor, rightEdgeX, y);
                return new ValueCellHandle
                {
                    CoinSegments = SegmentLayoutHandle.Empty,
                    CurrencySegments = SegmentLayoutHandle.Empty,
                    DashLabel = dashLabel
                };
            }

            return LayoutValueSegmentsRightAligned(parent, copper, currencyAmounts, rightEdgeX, y, font, alphaScale);
        }

        /// <summary>
        /// M33 C2b: non-allocating reposition twin to
        /// RenderValueCellRightAligned - moves an EXISTING value cell's
        /// controls to a new rightEdgeX, using only the cached per-segment
        /// TextWidths (ShoppingColumnMath.SegmentRunWidth, the same pure
        /// function the shopping column pre-scan uses, so the width this
        /// computes can never drift from what LayoutValueSegmentsRightAligned
        /// actually laid out). No MeasureString, no new controls.
        /// </summary>
        internal static void RepositionValueCellRightAligned(ValueCellHandle handle, int rightEdgeX, int y)
        {
            if (handle.DashLabel != null)
            {
                handle.DashLabel.Location = new Point(rightEdgeX - handle.DashLabel.Width, y);
                return;
            }

            int coinWidth = ShoppingColumnMath.SegmentRunWidth(handle.CoinSegments.TextWidths, CoinIconSize, CoinLabelIconGap, CoinSegmentGap);
            int currencyWidth = ShoppingColumnMath.SegmentRunWidth(handle.CurrencySegments.TextWidths, CoinIconSize, CoinLabelIconGap, CoinSegmentGap);
            int gap = (coinWidth > 0 && currencyWidth > 0) ? CoinSegmentGap : 0;

            int startX = rightEdgeX - (coinWidth + gap + currencyWidth);
            RepositionSegments(handle.CoinSegments, startX, y);
            RepositionSegments(handle.CurrencySegments, startX + coinWidth + gap, y);
        }
    }
}
