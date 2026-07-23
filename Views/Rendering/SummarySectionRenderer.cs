using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-23d (m38-a1-architecture.md S3b-T2, continuing the WP-23/WP-23b/
    // WP-23c extractions): moved verbatim out of CraftingPlanView's "7.
    // Section builders (continued)" region - the Summary/Total Cost section:
    // the cost-tile row (CreateCostTileRow, its CostTileHandle relayout
    // cache, and the TileCaptionFor label-shortening helper), the M35
    // multi-item batch MultiItemNote banner row (rendered via the same
    // TextRowRenderer path the CraftingSteps/default-fallback cases already
    // use), and the per-currency CreateCurrencyRow rows with their M34-B2b
    // owned/needed annotations. Behavior is unchanged: same tile/row
    // geometry, same PlanContentHeightMath/PlanRelayoutMath calls (including
    // PlanRelayoutMath.ComputeCostTileGeometry, which CreateCostTileRow's
    // build AND relayout closure both call - untouched, still Services/-only
    // arithmetic, verbatim), same M37 batch-economics tile rows
    // (Total/Own materials/Sell value/Profit render through this same
    // generic per-CoinTotal-row tile band, unchanged - see
    // docs/research/m37-r2-batch-economics.md Section 3.6/4.3). This
    // section has no LabelHelpers.CreateRowDivider usage (no list-style rows
    // here, just the tile band and standalone currency/note lines), so
    // DO-NOT-TOUCH #6's divider math does not apply to this class. The only
    // edits inside the moved bodies are _relayoutActions.Add -> the injected
    // ISectionRelayoutSink.AddRelayout (a semantics-preserving pass-through -
    // see ISectionRelayoutSink's doc comment) and CreateTextRow(..., this)
    // -> TextRowRenderer.CreateTextRow(..., _sink) for the MultiItemNote row
    // (the helper itself already lived in Views/Rendering/TextRowRenderer as
    // of WP-23c; only the sink argument changes here).
    //
    // CreateSummarySectionBody is renamed Render, matching every other
    // section renderer's entry point; CreateCostTileRow, TileCaptionFor,
    // CreateCurrencyRow, and the CurrencyRowHeight/CurrencyIconSize
    // constants keep their original names, moved byte-identical apart from
    // the one sink substitution each of CreateCostTileRow/CreateCurrencyRow
    // needed.
    //
    // TextRowRenderer's own doc comment (WP-23c) named
    // CreateSummarySectionBody's noteRows loop as a call site staying in
    // CraftingPlanView "because Summary is not part of this package's
    // scope" - this package closes that: the noteRows loop moves here, so
    // TextRowRenderer.CreateTextRow now has exactly one remaining call site
    // left inside CraftingPlanView itself (the default fallback case in
    // CreateCollapsibleSection) plus this class's own (_sink-qualified)
    // call - see TextRowRenderer's updated doc comment.
    //
    // M38 WP-24 (m38-a2-simplify.md finding #3, same package): surveyed
    // this class's two rows against both new shared row-shape helpers
    // (RowRelayoutHelpers.FinishRow, IconNameRowHelpers) and adopted
    // neither. CreateCostTileRow/CreateCurrencyRow build no
    // LabelHelpers.CreateRowDivider at all (RowRelayoutHelpers.FinishRow's
    // entire reason to exist), and neither has an ellipsized name label
    // (IconNameRowHelpers' shape) - CreateCurrencyRow's icon is a plain
    // CoinSegmentMath-adjacent currency glyph, not a rarity-framed item
    // icon, and its text is the full, uncapped row label. Forcing either
    // helper onto these rows would not remove any real duplication and
    // would risk exactly the kind of pixel drift this package's brief
    // warns against, so both rows stay hand-rolled, unchanged by WP-24.
    internal sealed class SummarySectionRenderer
    {
        private readonly ISectionRelayoutSink _sink;

        internal SummarySectionRenderer(ISectionRelayoutSink sink)
        {
            // Mirrors the constructor-null-guard convention already used
            // for injected dependencies elsewhere in Views/ (ViewAdapter's
            // buildAction, SettingsTabContent's settings, FrameTicker's
            // step) and by every other section renderer on this pattern -
            // the sole production call site always passes `this`
            // (CraftingPlanView), but a later section renderer built on
            // this same pattern should fail loud, not with a deferred NRE
            // inside CreateCostTileRow's/CreateCurrencyRow's first
            // AddRelayout call.
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        // Moved verbatim from CraftingPlanView.CreateSummarySectionBody
        // (renamed Render, matching every other section renderer). Only
        // change: CreateTextRow(..., this) -> TextRowRenderer.CreateTextRow(
        // ..., _sink).
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            var coinRows = new List<PlanRowViewModel>();
            var otherRows = new List<PlanRowViewModel>();
            var noteRows = new List<PlanRowViewModel>();
            foreach (var row in section.Rows)
            {
                if (row.RowType == PlanRowType.CoinTotal) coinRows.Add(row);
                // M35 (gw2efficiency parity - multi-item plans): the
                // multi-item batch note is a plain text row, not a
                // CurrencyCost row - must not fall into the CreateCurrencyRow
                // branch below (which assumes an icon/quantity that a note
                // row never has).
                else if (row.RowType == PlanRowType.MultiItemNote) noteRows.Add(row);
                else otherRows.Add(row);
            }

            if (coinRows.Count > 0)
            {
                CreateCostTileRow(coinRows, contentFlow, panelWidth);
            }

            // The only other row type in this section is CurrencyCost.
            foreach (var row in otherRows)
            {
                CreateCurrencyRow(row, contentFlow, panelWidth);
            }

            foreach (var row in noteRows)
            {
                // M38 WP-23c: CreateTextRow moved to
                // Views/Rendering/TextRowRenderer (see that class's doc
                // comment - it has two call sites still living in
                // CraftingPlanView, this one and the default fallback case
                // in CreateCollapsibleSection, so it could not move
                // wholesale into a single extracted renderer).
                TextRowRenderer.CreateTextRow(row.Label, contentFlow, panelWidth, _sink);
            }
        }

        /// <summary>
        /// gw2e's cost-breakdown: a centered row of equal-width stat tiles,
        /// one per CoinTotal row (Total, Sell value, Profit/Loss - up to the
        /// spec's 5 when all are applicable). Non-coin rows (currency costs)
        /// are handled separately as full-width rows underneath.
        /// </summary>
        /// <summary>
        /// One tile's already-created controls, cached for relayout - m2
        /// 3.5's [FANOUT] case: unlike a single-anchor row, every tile's
        /// caption AND coin segments are independently re-centered inside
        /// their own tileWidth-wide slice on every drag tick.
        /// </summary>
        private sealed class CostTileHandle
        {
            public Label CaptionLabel;
            public CoinCurrencyRenderer.SegmentLayoutHandle Segments;
        }

        // Moved verbatim from CraftingPlanView.CreateCostTileRow. Only
        // change: _relayoutActions.Add(...) -> _sink.AddRelayout(...).
        private void CreateCostTileRow(List<PlanRowViewModel> coinRows, FlowPanel parent, int panelWidth)
        {
            int tileCount = coinRows.Count;
            if (tileCount == 0) return;

            const int rowHeight = PlanContentHeightMath.CostTileRowHeight;
            const int totalMargin = 40;
            const int minTileWidth = 80;
            var geometry = PlanRelayoutMath.ComputeCostTileGeometry(panelWidth, tileCount, totalMargin, minTileWidth);

            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, rowHeight),
                Parent = parent
            };

            var captionFont = GameService.Content.DefaultFont12;
            var amountFont = GameService.Content.DefaultFont16;
            var captionColor = new Color(153, 153, 153);

            var tiles = new List<CostTileHandle>(tileCount);
            for (int i = 0; i < tileCount; i++)
            {
                int tileX = geometry.StartX + i * geometry.TileWidth;
                var row = coinRows[i];

                string caption = TileCaptionFor(row.Label);
                int captionWidth = (int)System.Math.Ceiling(captionFont.MeasureString(caption).Width);
                var captionLabel = new Label()
                {
                    Text = caption,
                    Font = captionFont,
                    TextColor = captionColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(tileX + PlanRelayoutMath.CenterX(geometry.TileWidth, captionWidth), 6),
                    Parent = rowPanel
                };

                var segments = CoinCurrencyRenderer.BuildCoinSegments(row.CoinValue, amountFont);
                int segmentsWidth = CoinCurrencyRenderer.TotalCoinSegmentsWidth(segments);
                int coinStartX = tileX + PlanRelayoutMath.CenterX(geometry.TileWidth, segmentsWidth);
                var segmentHandle = CoinCurrencyRenderer.LayoutCoinSegments(rowPanel, segments, coinStartX, 30, amountFont);

                tiles.Add(new CostTileHandle { CaptionLabel = captionLabel, Segments = segmentHandle });
            }

            // M33 C2b [FANOUT]: every tile's caption + coin segments are
            // font-only (invariant to panelWidth) - only tileWidth/startX
            // and each tile's own centering offset move. No MeasureString.
            _sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, rowHeight);
                var g = PlanRelayoutMath.ComputeCostTileGeometry(w, tileCount, totalMargin, minTileWidth);
                for (int i = 0; i < tiles.Count; i++)
                {
                    int tileX = g.StartX + i * g.TileWidth;
                    var tile = tiles[i];

                    tile.CaptionLabel.Location = new Point(tileX + PlanRelayoutMath.CenterX(g.TileWidth, tile.CaptionLabel.Width), 6);

                    int segmentsWidth = ShoppingColumnMath.SegmentRunWidth(tile.Segments.TextWidths, CoinSegmentMath.CoinIconSize, CoinSegmentMath.CoinLabelIconGap, CoinSegmentMath.CoinSegmentGap);
                    int coinStartX = tileX + PlanRelayoutMath.CenterX(g.TileWidth, segmentsWidth);
                    CoinCurrencyRenderer.RepositionSegments(tile.Segments, coinStartX, 30);
                }
            });
        }

        /// <summary>
        /// Strips the parenthetical qualifier off a Summary row label
        /// ("Sell value (5x, after 15% TP fees)" -> "Sell value") so tile
        /// captions stay short, like gw2e's "Buy price" / "Sell price".
        /// </summary>
        private static string TileCaptionFor(string rowLabel)
        {
            if (string.IsNullOrEmpty(rowLabel)) return "";
            int parenIdx = rowLabel.IndexOf('(');
            return (parenIdx > 0 ? rowLabel.Substring(0, parenIdx) : rowLabel).Trim();
        }

        // Sized between the tree/row item-icon (32px) and the coin-segment
        // icon (20px) since it sits inside a plain 28px text row; reuses
        // CoinSegmentMath.CoinLabelIconGap (M38 WP-21 findings fix: moved out of
        // CoinCurrencyRenderer) for the text-to-icon gap so both follow the
        // same "number/text first, gap, icon" convention.
        private const int CurrencyRowHeight = PlanContentHeightMath.CurrencyRowHeight;
        private const int CurrencyIconSize = 18;

        /// <summary>
        /// CurrencyCost row: identical "  {label}" text to CreateTextRow,
        /// plus the currency's icon immediately to its right when known.
        /// IconUrl null (no data available - service not wired up, fetch
        /// not yet complete, or the currency was absent from the API
        /// response) renders exactly like CreateTextRow - never a
        /// placeholder guess for a missing icon. When CurrencyOwnedQuantity
        /// is set (M34-B2b, wallet data present), an "(X owned, Y needed)"
        /// annotation follows the icon - gw2e's ownedCurrencies/
        /// shoppingCurrencies split (r2 report Section 4.3), cosmetic only.
        /// </summary>
        // Moved verbatim from CraftingPlanView.CreateCurrencyRow. Only
        // change: _relayoutActions.Add(...) -> _sink.AddRelayout(...).
        private void CreateCurrencyRow(PlanRowViewModel row, FlowPanel parent, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, CurrencyRowHeight),
                Parent = parent
            };
            var label = new Label()
            {
                Text = "  " + row.Label,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(8, 4),
                Parent = rowPanel
            };

            int cursorX = 8 + label.Width;
            if (!string.IsNullOrEmpty(row.IconUrl))
            {
                int iconX = cursorX + CoinSegmentMath.CoinLabelIconGap;
                int iconY = (CurrencyRowHeight - CurrencyIconSize) / 2;
                IconControls.CreateItemIcon(rowPanel, row.IconUrl, iconX, iconY, CurrencyIconSize);
                cursorX = iconX + CurrencyIconSize;
            }

            if (row.CurrencyOwnedQuantity.HasValue)
            {
                int needed = row.Quantity - row.CurrencyOwnedQuantity.Value;
                new Label()
                {
                    Text = $"({row.CurrencyOwnedQuantity.Value} owned, {needed} needed)",
                    TextColor = new Color(153, 153, 153),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(cursorX + CoinSegmentMath.CoinLabelIconGap, 4),
                    Parent = rowPanel
                };
            }

            // Not width-dependent beyond the row's own cosmetic width (m2
            // 3.6): label/icon/owned-annotation sit at a fixed left-anchored
            // x regardless of panelWidth.
            _sink.AddRelayout(w => rowPanel.Size = new Point(w, CurrencyRowHeight));
        }
    }
}
