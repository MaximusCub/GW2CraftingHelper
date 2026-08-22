using Blish_HUD.Content;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView's "10. Coin/currency
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
            var (gold, silver, cop) = CoinSegmentMath.Split(copper);
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

        // CoinIconSize/CoinLabelIconGap/CoinSegmentGap,
        // CoinSegmentSpec/CurrencySegmentSpec, and the TotalCoinSegmentsWidth/
        // TotalCurrencySegmentsWidth arithmetic moved to Services/CoinSegmentMath.cs
        // (Blish-free, unit-tested) so the width formulas can be tested without
        // referencing UI code. Everything below that still touches Label/Panel/
        // BitmapFont references CoinSegmentMath's constants/structs directly.

        internal static List<CoinSegmentMath.CoinSegmentSpec> BuildCoinSegments(long copper, BitmapFont font)
        {
            var (gold, silver, cop) = CoinSegmentMath.Split(copper);

            bool showGold = gold > 0;
            bool showSilver = showGold || silver > 0;

            var segments = new List<CoinSegmentMath.CoinSegmentSpec>(3);
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

        // private -> internal: MainView needs to build its own
        // 3-segment gold/silver/copper spec list (its own show-all/no-
        // padding formatting - a deliberate MainView behavior, out of this
        // package's scope to change) without duplicating this measure-
        // and-wrap one-liner.
        // NOTE: this is NOT the same precedent as the earlier
        // GetPillColors private -> internal bump. That bump was reverted
        // back to private (commit 5c56b2a) specifically to stop
        // Views/Rendering from depending back on CraftingPlanView and keep
        // Views/Rendering a true leaf layer; CraftingPlanView.GetPillColors
        // is private static again on current master. This bump is
        // different in kind: MainView -> Views/Rendering is a normal
        // forward consumer dependency (a leaf class exposing a helper to a
        // caller), not a reverse edge back into CraftingPlanView. Do not
        // cite this as precedent for adding a reverse
        // Views/Rendering -> CraftingPlanView dependency.
        internal static void AddSegmentSpec(List<CoinSegmentMath.CoinSegmentSpec> segments, BitmapFont font, int assetId, string text)
        {
            int width = (int)System.Math.Ceiling(font.MeasureString(text).Width);
            segments.Add(new CoinSegmentMath.CoinSegmentSpec { AssetId = assetId, Text = text, TextWidth = width });
        }

        internal static int TotalCoinSegmentsWidth(List<CoinSegmentMath.CoinSegmentSpec> segments)
        {
            return CoinSegmentMath.TotalCoinSegmentsWidth(segments);
        }

        /// <summary>
        /// A coin/currency segment run's already-created controls
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

            /// <summary>
            /// Extra y applied to the ICONS only, so a run whose number
            /// labels are taller than CoinSegmentMath.CoinIconSize (the
            /// Summary cost band's promoted result tile) can centre its
            /// fixed-size coin icons against the text instead of leaving
            /// them stuck to the text's top edge. Cached on the handle so
            /// RepositionSegments reproduces the same offset without the
            /// caller having to remember it. 0 everywhere else, which is
            /// exactly the prior behaviour.
            /// </summary>
            public int IconYOffset;

            public static readonly SegmentLayoutHandle Empty =
                new SegmentLayoutHandle { Controls = System.Array.Empty<(Label, Panel)>(), TextWidths = System.Array.Empty<int>() };
        }

        /// <summary>
        /// Lays out coin segments left-to-right starting at x. alphaScale
        /// dims the number labels (not the icons - Panel has no tint
        /// property) for dimmed not-crafted subtree rows. iconYOffset
        /// vertically centres the fixed-size icons against a taller number
        /// font - see SegmentLayoutHandle.IconYOffset.
        /// </summary>
        internal static SegmentLayoutHandle LayoutCoinSegments(
            Panel parent, List<CoinSegmentMath.CoinSegmentSpec> segments, int startX, int y, BitmapFont font,
            float alphaScale = 1f, int iconYOffset = 0)
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
                    Size = new Point(CoinSegmentMath.CoinIconSize, CoinSegmentMath.CoinIconSize),
                    Location = new Point(x + seg.TextWidth + CoinSegmentMath.CoinLabelIconGap, y + iconYOffset),
                    BackgroundTexture = AsyncTexture2D.FromAssetId(seg.AssetId),
                    Parent = parent
                };

                controls[i] = (label, icon);
                widths[i] = seg.TextWidth;
                x += seg.TextWidth + CoinSegmentMath.CoinLabelIconGap + CoinSegmentMath.CoinIconSize + CoinSegmentMath.CoinSegmentGap;
            }

            return new SegmentLayoutHandle { Controls = controls, TextWidths = widths, IconYOffset = iconYOffset };
        }

        /// <summary>
        /// Non-allocating reposition twin to LayoutCoinSegments/
        /// LayoutCurrencySegments (m2 3.7/4) - moves EXISTING segment
        /// controls to new x-positions using the cached TextWidths, never
        /// creating a control or calling MeasureString. Shared by both coin
        /// and currency segment runs since they follow the identical
        /// "label, gap, icon, gap" geometry (same CoinSegmentMath.CoinIconSize/
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
                icon.Location = new Point(x + textWidth + CoinSegmentMath.CoinLabelIconGap, y + handle.IconYOffset);
                x += textWidth + CoinSegmentMath.CoinLabelIconGap + CoinSegmentMath.CoinIconSize + CoinSegmentMath.CoinSegmentGap;
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

        // --- Currency + mixed value display helpers ---
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
        // itself, not incidental prose.
        private const string UnpricedDashText = "\u2014";
        private static readonly Color UnpricedDashColor = new Color(140, 140, 140);

        internal static List<CoinSegmentMath.CurrencySegmentSpec> BuildCurrencySegments(
            IReadOnlyList<CurrencyAmountViewModel> amounts, BitmapFont font)
        {
            var segments = new List<CoinSegmentMath.CurrencySegmentSpec>();
            if (amounts == null) return segments;

            foreach (var amount in amounts)
            {
                // A fractional-per-unit "Each" amount carries a
                // literal "N for M" bundle label instead of a whole-number
                // Amount (CurrencyDisplayResolver.ResolveUnitAmounts) -
                // render that text verbatim rather than the numeric amount.
                string text = amount.BundleLabel ?? amount.Amount.ToString();
                int width = (int)System.Math.Ceiling(font.MeasureString(text).Width);
                segments.Add(new CoinSegmentMath.CurrencySegmentSpec
                {
                    IconUrl = amount.IconUrl,
                    Text = text,
                    TextWidth = width,
                    Name = amount.Name
                });
            }
            return segments;
        }

        /// <summary>
        /// The actual width arithmetic lives in CoinSegmentMath (Blish-free,
        /// tested), which itself delegates to ShoppingColumnMath, so the
        /// pre-scan (MeasureValueWidth) and the real layout
        /// (LayoutValueSegmentsRightAligned) below can never drift apart;
        /// only the per-segment text measurement (BitmapFont.MeasureString)
        /// is Blish-bound and stays here.
        /// </summary>
        internal static int TotalCurrencySegmentsWidth(List<CoinSegmentMath.CurrencySegmentSpec> segments)
        {
            return CoinSegmentMath.TotalCurrencySegmentsWidth(segments);
        }

        internal static SegmentLayoutHandle LayoutCurrencySegments(
            Panel parent, List<CoinSegmentMath.CurrencySegmentSpec> segments, int startX, int y, BitmapFont font, float alphaScale = 1f)
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

                // A currency icon carries no visible
                // name text anywhere in this cell (unlike SummarySectionRenderer.
                // CreateCurrencyRow, which prints the name as a label before
                // the icon) - a hover tooltip is the only way to identify it.
                var icon = IconControls.CreateItemIcon(
                    parent, seg.IconUrl, x + seg.TextWidth + CoinSegmentMath.CoinLabelIconGap, y,
                    CoinSegmentMath.CoinIconSize, seg.Name);

                controls[i] = (label, icon);
                widths[i] = seg.TextWidth;
                x += seg.TextWidth + CoinSegmentMath.CoinLabelIconGap + CoinSegmentMath.CoinIconSize + CoinSegmentMath.CoinSegmentGap;
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
            return (coinWidth > 0 && currencyWidth > 0) ? coinWidth + CoinSegmentMath.CoinSegmentGap + currencyWidth : coinWidth + currencyWidth;
        }

        /// <summary>
        /// Everything a relayout closure needs to reposition an
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
            var coinSegments = copper > 0 ? BuildCoinSegments(copper, font) : new List<CoinSegmentMath.CoinSegmentSpec>();
            var currencySegments = BuildCurrencySegments(currencyAmounts, font);
            int coinWidth = TotalCoinSegmentsWidth(coinSegments);
            int currencyWidth = TotalCurrencySegmentsWidth(currencySegments);
            int gap = (coinWidth > 0 && currencyWidth > 0) ? CoinSegmentMath.CoinSegmentGap : 0;

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
        /// instead of a blank cell or an invented "0".
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
        /// Non-allocating reposition twin to
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

            int coinWidth = ShoppingColumnMath.SegmentRunWidth(handle.CoinSegments.TextWidths, CoinSegmentMath.CoinIconSize, CoinSegmentMath.CoinLabelIconGap, CoinSegmentMath.CoinSegmentGap);
            int currencyWidth = ShoppingColumnMath.SegmentRunWidth(handle.CurrencySegments.TextWidths, CoinSegmentMath.CoinIconSize, CoinSegmentMath.CoinLabelIconGap, CoinSegmentMath.CoinSegmentGap);
            int gap = (coinWidth > 0 && currencyWidth > 0) ? CoinSegmentMath.CoinSegmentGap : 0;

            int startX = rightEdgeX - (coinWidth + gap + currencyWidth);
            RepositionSegments(handle.CoinSegments, startX, y);
            RepositionSegments(handle.CurrencySegments, startX + coinWidth + gap, y);
        }
    }
}
