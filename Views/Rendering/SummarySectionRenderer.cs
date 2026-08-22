using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView's "7.
    // Section builders (continued)" region - the Summary/Total Cost section.
    //
    // The section is two formula-band tile rows (CreateFormulaBand), a
    // c-table for the plan's non-coin currency costs
    // (CreateCurrencyTable), the multi-item batch MultiItemNote banner
    // row (still via
    // TextRowRenderer), and a new subdued footnote row (CreateFootnoteRow).
    // Height agreement for this new shape lives in
    // Services/SummarySectionLayoutMath.BodyHeight, not
    // PlanContentHeightMath (a high-evidence zone, formerly DO-NOT-TOUCH,
    // for this package - see docs/KNOWN-ISSUES.md's policy note) - see
    // that class's own doc comment for the full rationale.
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
            // inside CreateFormulaBand's/CreateCurrencyTableRow's first
            // AddRelayout call.
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            var costBandRows = new List<PlanRowViewModel>();
            var profitBandRows = new List<PlanRowViewModel>();
            var currencyRows = new List<PlanRowViewModel>();
            var noteRows = new List<PlanRowViewModel>();
            // A List, like noteRows - not a single "last row
            // wins" variable. SummarySectionLayoutMath.BodyHeight sums
            // FallbackTextRowHeight per SummaryFootnote row it counts
            // (its own doc comment: "summed rather than assumed so a
            // null/absent footnote degrades gracefully instead of
            // desyncing height from what actually rendered"); a renderer
            // that only drew the LAST such row while BodyHeight kept
            // counting all of them would silently reserve dead space (or
            // too little) the moment a second footnote row ever existed -
            // rendering every row it is handed keeps the two in agreement
            // by construction, the same way noteRows already does for
            // MultiItemNote.
            var footnoteRows = new List<PlanRowViewModel>();

            foreach (var row in section.Rows)
            {
                switch (row.RowType)
                {
                    case PlanRowType.CostFormulaTile:
                        costBandRows.Add(row);
                        break;
                    case PlanRowType.ProfitFormulaTile:
                        profitBandRows.Add(row);
                        break;
                    case PlanRowType.CurrencyCost:
                        currencyRows.Add(row);
                        break;
                    case PlanRowType.MultiItemNote:
                        noteRows.Add(row);
                        break;
                    case PlanRowType.SummaryFootnote:
                        footnoteRows.Add(row);
                        break;
                }
            }

            if (costBandRows.Count > 0)
            {
                // The cost band is the plan's headline: promoted result
                // font, plus the "the gold figure is not the whole cost"
                // disclosure line whenever currency rows follow it. Both
                // its row height and the height BodyHeight counts for it
                // come from SummarySectionLayoutMath.CostBandHeight with
                // the SAME currencyRows.Count > 0 condition that decides
                // whether the line is drawn.
                CreateFormulaBand(
                    costBandRows, contentFlow, panelWidth,
                    GameService.Content.DefaultFont32,
                    SummarySectionLayoutMath.CostBandHeight(currencyRows.Count > 0),
                    SummarySectionLayoutMath.CurrencyRequirementNote(currencyRows.Count),
                    SummarySectionLayoutMath.CurrencyRequirementNoteTooltip(currencyRows));
            }

            if (profitBandRows.Count > 0)
            {
                // Derived stats, not the headline - unpromoted result font
                // and the unchanged CostTileRowHeight band.
                CreateFormulaBand(
                    profitBandRows, contentFlow, panelWidth,
                    GameService.Content.DefaultFont16,
                    PlanContentHeightMath.CostTileRowHeight);
            }

            if (currencyRows.Count > 0)
            {
                CreateCurrencyTable(currencyRows, contentFlow, panelWidth);
            }

            foreach (var row in noteRows)
            {
                // CreateTextRow moved to
                // Views/Rendering/TextRowRenderer (see that class's doc
                // comment - it has two call sites still living in
                // CraftingPlanView, this one and the default fallback case
                // in CreateCollapsibleSection, so it could not move
                // wholesale into a single extracted renderer).
                TextRowRenderer.CreateTextRow(row.Label, contentFlow, panelWidth, _sink);
            }

            foreach (var row in footnoteRows)
            {
                CreateFootnoteRow(row.Label, contentFlow, panelWidth);
            }
        }

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

            // Each tile's own amount y - the result tile's promoted font
            // is bottom-anchored at a different y than its siblings', and
            // the relayout closure only moves runs horizontally.
            public int AmountY;
        }

        // Left edge a collapsed one-tile band's contents start at - the
        // same 8px content gutter the currency table's icon column and the
        // footnote row already use, so the section reads as one left edge.
        private const int LoneTileContentX = 8;

        private const int BandCaptionY = 4;

        // Amounts are bottom-anchored this far above the band's bottom
        // edge; 6 reproduces the previous fixed y=30 exactly for an
        // unpromoted CostTileRowHeight band (56 - 6 - 20 == 30).
        private const int BandAmountBottomPad = 6;

        private static readonly Color BandCaptionColor = new Color(153, 153, 153);

        // The result tile's caption is the one a user is actually looking
        // for; it stays the same size as its siblings (so the band still
        // reads as one formula) but is lifted out of the dim grey.
        private static readonly Color PromotedCaptionColor = new Color(235, 235, 235);

        // Warm, not red: the disclosure is a caveat about scope, not an
        // error, and it must not read as an alarm on an ordinary plan.
        private static readonly Color CurrencyNoteColor = new Color(206, 170, 92);

        /// <summary>
        /// Vertical space an amount run occupies: its own text height, or
        /// the coin icon's if that is taller (an unpromoted DefaultFont16
        /// run, where the 20px icons are the tallest thing in the row).
        /// </summary>
        private static int AmountBlockHeight(BitmapFont font)
        {
            int textHeight = (int)System.Math.Ceiling(font.MeasureString("0").Height);
            return textHeight > CoinSegmentMath.CoinIconSize ? textHeight : CoinSegmentMath.CoinIconSize;
        }

        /// <summary>
        /// A formula band - N equal-width stat tiles reading
        /// left-to-right as a formula ("Total Materials Value - Your
        /// Materials Used = Actual Cost to Craft", or "Sell Value - Total
        /// Materials Value = Profit if Sold"). Callers pass exactly the
        /// rows belonging to ONE band (PlanViewModelBuilder groups
        /// CostFormulaTile/ProfitFormulaTile separately, and Render above
        /// re-groups by that same RowType), so two bands render as two
        /// stacked tile rows, not one - the cost band at
        /// SummarySectionLayoutMath.CostBandHeight and the profit band at
        /// PlanContentHeightMath.CostTileRowHeight. rowHeight is the
        /// caller's, not this method's, so BodyHeight and the row panel
        /// built here are always the same number by construction; see
        /// Services/SummarySectionLayoutMath.BodyHeight.
        ///
        /// Geometry matches ComputeCostTileGeometry's tile layout - EXCEPT
        /// for a collapsed one-tile band, which is left-aligned at the
        /// section's own content gutter instead of centred. A lone tile
        /// centred on a full-width band reads as a stray caption floating
        /// in whitespace, and it is the only tile in this section that
        /// does not align with anything else in it (the currency table's
        /// icon column, the footnote and every section title all start at
        /// the left). Left-aligning it is preferred over the alternative
        /// of shrinking the band: this tile carries the promoted result
        /// font precisely because it is the plan's headline figure, so
        /// making its band SHORTER would fight the promotion. It gets the
        /// same band height as the three-tile case and simply starts where
        /// everything else in the section starts.
        ///
        /// resultAmountFont is the amount font for the band's RIGHTMOST
        /// (result) tile only; every other tile keeps DefaultFont16. A
        /// collapsed one-tile band is all result, so it is promoted too.
        /// Amounts are bottom-anchored inside rowHeight rather than pinned
        /// to a fixed y, so tiles of different amount fonts share one
        /// bottom line and no font metric can push a taller amount out of
        /// the band the caller's height math reserved.
        ///
        /// currencyNoteText, when non-null, draws a small disclosure line
        /// under the RESULT tile's caption - the plan has costs the coin
        /// figure above does not include. rowHeight must already account
        /// for it (SummarySectionLayoutMath.CostBandHeight).
        ///
        /// row.TooltipText is set directly on captionLabel itself, not on
        /// rowPanel, so hovering the header text always shows it
        /// regardless of overlapping controls.
        ///
        /// The "-"/"=" formula operators between tiles are drawn as small
        /// dim Labels centered on each boundary (no tooltip, so they
        /// never steal hover) - without them, same-shaped tiles have no
        /// visible relationship. Never drawn for a collapsed 1-tile band.
        ///
        /// The final boundary's symbol is no longer
        /// an unconditional "=". It reads the rightmost tile's own
        /// PlanRowViewModel.FormulaResultIsExact (see that field's doc
        /// comment) and draws NeutralResultSeparator instead of "=" when
        /// false - the profit band's loss case, where the rightmost tile
        /// deliberately shows Math.Abs(profit) under a "Loss if Sold"
        /// caption, so "left - middle = <abs loss>" would be an
        /// arithmetically false equation as drawn. Every other boundary
        /// (there is only ever one non-final boundary, tileCount == 3)
        /// keeps its unconditional "-": the left two tiles' own
        /// subtraction is never in question, only whether the FINAL
        /// result tile's displayed value is the true right-hand side.
        /// </summary>
        private void CreateFormulaBand(
            List<PlanRowViewModel> tileRows, FlowPanel parent, int panelWidth,
            BitmapFont resultAmountFont, int rowHeight,
            string currencyNoteText = null, string currencyNoteTooltip = null)
        {
            int tileCount = tileRows.Count;
            if (tileCount == 0) return;

            const int totalMargin = 40;
            const int minTileWidth = 80;

            // Drawn at the final boundary instead of
            // "=" when the rightmost tile's FormulaResultIsExact is false -
            // see this method's own doc comment. Deliberately not "-"
            // (would misread as a second subtraction) and not "=" (would
            // repeat the exact claim this fix removes); a colon reads as
            // plain, non-asserting punctuation grouping the two sides.
            const string NeutralResultSeparator = ":";
            bool lone = tileCount == 1;
            var geometry = PlanRelayoutMath.ComputeCostTileGeometry(panelWidth, tileCount, totalMargin, minTileWidth);

            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, rowHeight),
                Parent = parent
            };

            var captionFont = GameService.Content.DefaultFont12;
            var amountFont = GameService.Content.DefaultFont16;

            int captionHeight = (int)System.Math.Ceiling(captionFont.MeasureString("0").Height);
            int noteY = BandCaptionY + captionHeight + 2;
            int captionBlockBottom = (currencyNoteText != null ? noteY + captionHeight : BandCaptionY + captionHeight) + 2;

            int smallAmountHeight = AmountBlockHeight(amountFont);
            int resultAmountHeight = AmountBlockHeight(resultAmountFont);

            // A band is promoted only when its caller actually handed it a
            // bigger result font - the profit band passes DefaultFont16
            // and must keep the dim caption it already had, rather than
            // inheriting a brightening meant for the headline figure.
            bool promoted = resultAmountHeight > smallAmountHeight;

            // Clamped below the caption block: rowHeight is a fixed
            // constant while the promoted font's real metrics come from
            // whatever Blish loaded, so a font taller than the band was
            // sized for must overflow downward (loud - the DEBUG assert
            // below catches it) rather than silently overprint the caption.
            int smallAmountY = AmountY(rowHeight, smallAmountHeight, captionBlockBottom);
            int resultAmountY = AmountY(rowHeight, resultAmountHeight, captionBlockBottom);

            var tiles = new List<CostTileHandle>(tileCount);
            for (int i = 0; i < tileCount; i++)
            {
                int tileX = geometry.StartX + i * geometry.TileWidth;
                var row = tileRows[i];
                bool isResult = i == tileCount - 1;

                var tileAmountFont = isResult ? resultAmountFont : amountFont;
                int tileAmountY = isResult ? resultAmountY : smallAmountY;
                int tileAmountHeight = isResult ? resultAmountHeight : smallAmountHeight;
                int iconYOffset = (tileAmountHeight - CoinSegmentMath.CoinIconSize) / 2;

                string caption = row.Label ?? "";
                int captionWidth = (int)System.Math.Ceiling(captionFont.MeasureString(caption).Width);
                var captionLabel = new Label()
                {
                    Text = caption,
                    Font = captionFont,
                    TextColor = isResult && promoted ? PromotedCaptionColor : BandCaptionColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(ContentX(lone, tileX, geometry.TileWidth, captionWidth), BandCaptionY),
                    Parent = rowPanel,
                    BasicTooltipText = row.TooltipText
                };

                var segments = CoinCurrencyRenderer.BuildCoinSegments(row.CoinValue, tileAmountFont);
                int segmentsWidth = CoinCurrencyRenderer.TotalCoinSegmentsWidth(segments);
                int coinStartX = ContentX(lone, tileX, geometry.TileWidth, segmentsWidth);
                var segmentHandle = CoinCurrencyRenderer.LayoutCoinSegments(
                    rowPanel, segments, coinStartX, tileAmountY, tileAmountFont, 1f, iconYOffset);

                tiles.Add(new CostTileHandle
                {
                    CaptionLabel = captionLabel,
                    Segments = segmentHandle,
                    AmountY = tileAmountY
                });
            }

            // The disclosure line sits under the RESULT tile's caption -
            // it qualifies that tile's number, not the band as a whole.
            Label noteLabel = null;
            if (currencyNoteText != null)
            {
                int noteWidth = (int)System.Math.Ceiling(captionFont.MeasureString(currencyNoteText).Width);
                int resultTileX = geometry.StartX + (tileCount - 1) * geometry.TileWidth;
                noteLabel = new Label()
                {
                    Text = currencyNoteText,
                    Font = captionFont,
                    TextColor = CurrencyNoteColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(ContentX(lone, resultTileX, geometry.TileWidth, noteWidth), noteY),
                    Parent = rowPanel,
                    BasicTooltipText = currencyNoteTooltip
                };
            }

            // One operator per boundary between two tiles: "-" for every
            // boundary except the last, which reads "=" - except the
            // profit band's loss case, which gets NeutralResultSeparator.
            // Centered on the boundary x where tile i+1 begins. Never drawn
            // for a lone tile (there is no boundary).
            List<Label> operatorLabels = null;
            if (tileCount > 1)
            {
                operatorLabels = new List<Label>(tileCount - 1);
                for (int i = 1; i < tileCount; i++)
                {
                    bool isFinalBoundary = i == tileCount - 1;
                    string symbol = isFinalBoundary
                        ? (tileRows[i].FormulaResultIsExact ? "=" : NeutralResultSeparator)
                        : "-";
                    int boundaryX = geometry.StartX + i * geometry.TileWidth;
                    int symbolWidth = (int)System.Math.Ceiling(amountFont.MeasureString(symbol).Width);
                    var operatorLabel = new Label()
                    {
                        Text = symbol,
                        Font = amountFont,
                        TextColor = BandCaptionColor,
                        AutoSizeWidth = true,
                        AutoSizeHeight = true,
                        Location = new Point(boundaryX - symbolWidth / 2, smallAmountY),
                        Parent = rowPanel
                    };
                    operatorLabels.Add(operatorLabel);
                }
            }

            // [FANOUT]: every tile's caption + coin segments are
            // font-only (invariant to panelWidth) - only tileWidth/startX
            // and each tile's own centering offset move. No MeasureString.
            // A lone tile's anchor is a constant, so its closure is a
            // no-op beyond the row's own width.
            _sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, rowHeight);
                var g = PlanRelayoutMath.ComputeCostTileGeometry(w, tileCount, totalMargin, minTileWidth);
                for (int i = 0; i < tiles.Count; i++)
                {
                    int tileX = g.StartX + i * g.TileWidth;
                    var tile = tiles[i];

                    tile.CaptionLabel.Location = new Point(
                        ContentX(lone, tileX, g.TileWidth, tile.CaptionLabel.Width), BandCaptionY);

                    int segmentsWidth = ShoppingColumnMath.SegmentRunWidth(tile.Segments.TextWidths, CoinSegmentMath.CoinIconSize, CoinSegmentMath.CoinLabelIconGap, CoinSegmentMath.CoinSegmentGap);
                    int coinStartX = ContentX(lone, tileX, g.TileWidth, segmentsWidth);
                    CoinCurrencyRenderer.RepositionSegments(tile.Segments, coinStartX, tile.AmountY);
                }

                if (noteLabel != null)
                {
                    int resultTileX = g.StartX + (tileCount - 1) * g.TileWidth;
                    noteLabel.Location = new Point(ContentX(lone, resultTileX, g.TileWidth, noteLabel.Width), noteY);
                }

                if (operatorLabels != null)
                {
                    for (int i = 0; i < operatorLabels.Count; i++)
                    {
                        int boundaryX = g.StartX + (i + 1) * g.TileWidth;
                        var operatorLabel = operatorLabels[i];
                        operatorLabel.Location = new Point(boundaryX - operatorLabel.Width / 2, smallAmountY);
                    }
                }
            });

#if DEBUG
            // The band's own rowHeight came from
            // SummarySectionLayoutMath (CostBandHeight / CostTileRowHeight)
            // and is the SAME number BodyHeight counted for this band. Its
            // contents are placed from measured font metrics, so this is
            // the one place the two can diverge - fail loud here rather
            // than let contentFlow reserve a height the band overflows.
            System.Diagnostics.Debug.Assert(
                resultAmountY + resultAmountHeight <= rowHeight && smallAmountY + smallAmountHeight <= rowHeight,
                "SummarySectionRenderer: a formula band's amounts must fit inside the row height " +
                "SummarySectionLayoutMath reserved for it - see CostBandHeight.");
#endif
        }

        /// <summary>
        /// Left x of one tile's content: the shared gutter for a lone
        /// (left-aligned) tile, otherwise centred inside its own tile
        /// slice. One helper so the build pass and the relayout closure
        /// cannot anchor a band differently.
        /// </summary>
        private static int ContentX(bool lone, int tileX, int tileWidth, int contentWidth)
        {
            return lone ? LoneTileContentX : tileX + PlanRelayoutMath.CenterX(tileWidth, contentWidth);
        }

        /// <summary>
        /// Top y of an amountHeight-tall amount run: bottom-anchored
        /// inside rowHeight, never allowed above the caption block.
        /// </summary>
        private static int AmountY(int rowHeight, int amountHeight, int captionBlockBottom)
        {
            int y = rowHeight - BandAmountBottomPad - amountHeight;
            return y > captionBlockBottom ? y : captionBlockBottom;
        }

        // --- Currency table ---
        //
        // 4 columns (Currency | Required | Have | Needed) do not fit
        // CTableHeaderRenderer's left/middle/right (3-slot) shape, so this
        // hand-rolls its own header row - the same precedent
        // ShoppingListSectionRenderer.CreateShoppingListHeaderRow already
        // set for its own 4-column (Item/Amount/Each/Total) header, rather
        // than stretching CTableHeaderRenderer's signature to fit a shape
        // it was not designed for.

        private const int CurrencyRowHeight = PlanContentHeightMath.CurrencyRowHeight;

        private void CreateCurrencyTable(List<PlanRowViewModel> rows, FlowPanel parent, int panelWidth)
        {
            // Pre-scan the actual widest rendered
            // Required/Have/Needed value across every row this render -
            // mirrors ShoppingListSectionRenderer.Render's own maxEachWidth/
            // maxTotalWidth pre-scan (see SummarySectionLayoutMath's
            // EffectiveCurrencyNumberColumnWidth doc comment for why the
            // fixed 60px floor alone is not always enough: an unclamped
            // Have value for a currency like Karma can run 6-7 digits).
            // One pass over rows (a plan's currency list is short - a
            // handful of entries in practice) with the SAME font both the
            // header and every data row already use.
            var font = GameService.Content.DefaultFont14;
            int widestNumberWidth = 0;
            foreach (var row in rows)
            {
                int rowWidest = MeasureWidestCurrencyNumber(row, font);
                if (rowWidest > widestNumberWidth) widestNumberWidth = rowWidest;
            }

            CreateCurrencyTableHeaderRow(parent, panelWidth, widestNumberWidth);
            for (int i = 0; i < rows.Count; i++)
            {
                CreateCurrencyTableRow(rows[i], parent, panelWidth, widestNumberWidth);
            }
        }

        /// <summary>
        /// Widest of a single currency row's own rendered Required/Have/
        /// Needed strings (Have/Needed already "-" rather than a fabricated
        /// number when no wallet snapshot exists - see
        /// PlanRowViewModel.CurrencyOwnedQuantity's doc comment - "-" is
        /// always narrower than a real value, so it never drives the max).
        /// </summary>
        private static int MeasureWidestCurrencyNumber(PlanRowViewModel row, BitmapFont font)
        {
            int widest = (int)System.Math.Ceiling(font.MeasureString(row.Quantity.ToString()).Width);

            string haveText = row.CurrencyOwnedQuantity.HasValue ? row.CurrencyOwnedQuantity.Value.ToString() : "-";
            int haveWidth = (int)System.Math.Ceiling(font.MeasureString(haveText).Width);
            if (haveWidth > widest) widest = haveWidth;

            string neededText = row.CurrencyNeededQuantity.HasValue ? row.CurrencyNeededQuantity.Value.ToString() : "-";
            int neededWidth = (int)System.Math.Ceiling(font.MeasureString(neededText).Width);
            if (neededWidth > widest) widest = neededWidth;

            return widest;
        }

        private void CreateCurrencyTableHeaderRow(FlowPanel parent, int panelWidth, int widestNumberWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, PlanContentHeightMath.CTableHeaderRowHeight),
                BackgroundColor = new Color(35, 35, 35),
                Parent = parent
            };
            var font = GameService.Content.DefaultFont14;
            new Label()
            {
                Text = "Currency", Font = font, TextColor = Color.White,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(SummarySectionLayoutMath.CurrencyNameX, 5), Parent = rowPanel
            };

            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth, widestNumberWidth);
            var requiredLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Required", font, Color.White, edges.RequiredRightEdge, 5);
            var haveLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Have", font, Color.White, edges.HaveRightEdge, 5);
            var neededLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Needed", font, Color.White, edges.NeededRightEdge, 5);

            // WidestNumberWidth is cached from the build-time
            // pre-scan (data-derived, not panelWidth-derived - it never
            // needs to re-run on resize, same reasoning as
            // ShoppingListSectionRenderer's own cached maxEachWidth/
            // maxTotalWidth).
            _sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, PlanContentHeightMath.CTableHeaderRowHeight);
                var e = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(w, widestNumberWidth);
                requiredLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.RequiredRightEdge, requiredLabel.Width), 5);
                haveLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.HaveRightEdge, haveLabel.Width), 5);
                neededLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.NeededRightEdge, neededLabel.Width), 5);
            });
        }

        // Full-coverage marker color: matches PillKind.Selected's green,
        // without adding a new PillKind for this single non-tree use.
        private static readonly Color FullCoverageBorder = new Color(31, 143, 12);
        private static readonly Color FullCoverageFill = FullCoverageBorder * 0.15f;

        // Glyph note: a "\u2713" check-mark marker was considered, but
        // live desktop rendering has shown ASCII to be the reliable form
        // for glyphs in the Blish font across
        // sessions/machines - i.e. this exact font has already shown
        // Unicode-glyph rendering is not something to assume without a
        // live check. No live Blish HUD session was available to verify
        // the check-mark glyph the same way, so this package takes the
        // pre-authorized safe fallback rather than gambling on an
        // unverified glyph: a
        // small green "OK" pill (LabelHelpers.CreateSmallTag, the same
        // helper the tree's Locked/Available pills and the shopping
        // source tag already use), never a raw Unicode character.
        private const string FullCoverageMarkerText = "OK";

        private void CreateCurrencyTableRow(PlanRowViewModel row, FlowPanel parent, int panelWidth, int widestNumberWidth)
        {
            const int rowHeight = CurrencyRowHeight;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };
            var font = GameService.Content.DefaultFont14;

            if (!string.IsNullOrEmpty(row.IconUrl))
            {
                int iconY = (rowHeight - SummarySectionLayoutMath.CurrencyIconSize) / 2;
                IconControls.CreateItemIcon(
                    rowPanel, row.IconUrl, SummarySectionLayoutMath.CurrencyIconX, iconY,
                    SummarySectionLayoutMath.CurrencyIconSize, row.Label);
            }

            const int nameX = SummarySectionLayoutMath.CurrencyNameX;
            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth, widestNumberWidth);
            int numberColumnWidth = SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(widestNumberWidth);
            int nameMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                edges.RequiredRightEdge, numberColumnWidth, SummarySectionLayoutMath.CurrencyColumnGap, nameX);
            string fullName = row.Label ?? "";
            string displayName = LabelHelpers.EllipsizeToWidth(font, fullName, nameMaxWidth);
            var nameLabel = new Label()
            {
                Text = displayName, Font = font, TextColor = Color.White,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(nameX, 4), Parent = rowPanel
            };
            if (displayName != fullName)
            {
                // Stamp BOTH the label AND its containing panel - a label
                // captures the mouse before a tooltip on a control
                // underneath it is ever reached, so a tooltip on rowPanel
                // alone only fires on the blank strip beside the name.
                nameLabel.BasicTooltipText = fullName;
                rowPanel.BasicTooltipText = fullName;
            }

            var numberColor = new Color(220, 220, 220);
            var requiredLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, row.Quantity.ToString(), font, numberColor, edges.RequiredRightEdge, 4);
            string haveText = row.CurrencyOwnedQuantity.HasValue ? row.CurrencyOwnedQuantity.Value.ToString() : "-";
            var haveLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, haveText, font, numberColor, edges.HaveRightEdge, 4);
            string neededText = row.CurrencyNeededQuantity.HasValue ? row.CurrencyNeededQuantity.Value.ToString() : "-";
            var neededLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, neededText, font, numberColor, edges.NeededRightEdge, 4);

            Panel marker = null;
            if (row.CurrencyFullyCovered)
            {
                int markerY = (rowHeight - 18) / 2;
                marker = LabelHelpers.CreateSmallTag(rowPanel, FullCoverageMarkerText, edges.MarkerX, markerY, FullCoverageBorder, FullCoverageFill);
                // No BasicTooltipText here: CreateSmallTag's inner fill
                // panel + label cover almost the entire pill, so a
                // tooltip on the returned outer Panel alone would be
                // swallowed, and CreateSmallTag does not expose its inner
                // controls to stamp all three. Left off rather than
                // shipped half-working.
            }

            // No CreateRowDivider here: the 28px CurrencyRowHeight was
            // never proven immune to the vanishing-divider defect (only
            // 36px rows are proven immune), and the header row's dark
            // background already delineates the table - introducing a
            // divider at an unproven row height risks resurrecting that
            // defect for a visual element nothing asked for.
            _sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, rowHeight);
                var e = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(w, widestNumberWidth);
                requiredLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.RequiredRightEdge, requiredLabel.Width), 4);
                haveLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.HaveRightEdge, haveLabel.Width), 4);
                neededLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.NeededRightEdge, neededLabel.Width), 4);
                if (marker != null)
                {
                    marker.Location = new Point(e.MarkerX, (rowHeight - 18) / 2);
                }
            });
            _sink.AddReellipsis(w =>
            {
                var e = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(w, widestNumberWidth);
                int newMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                    e.RequiredRightEdge, numberColumnWidth, SummarySectionLayoutMath.CurrencyColumnGap, nameX);
                string newDisplayName = LabelHelpers.EllipsizeToWidth(font, fullName, newMaxWidth);
                if (nameLabel.Text != newDisplayName)
                {
                    nameLabel.Text = newDisplayName;
                    // Both controls, same reasoning as the
                    // build-time tooltip assignment above.
                    string tooltip = newDisplayName != fullName ? fullName : null;
                    nameLabel.BasicTooltipText = tooltip;
                    rowPanel.BasicTooltipText = tooltip;
                }
            });
        }

        // A single subdued footnote row at the bottom of the section -
        // deliberately smaller/dimmer than the plain
        // MultiItemNote banner (TextRowRenderer.CreateTextRow's default
        // styling) so it reads as fine print, not as plan-specific
        // information.
        private static readonly Color FootnoteColor = new Color(130, 130, 130);

        private void CreateFootnoteRow(string text, FlowPanel parent, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, PlanContentHeightMath.FallbackTextRowHeight),
                Parent = parent
            };
            new Label()
            {
                Text = "  " + text,
                Font = GameService.Content.DefaultFont12,
                TextColor = FootnoteColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(8, 7),
                Parent = rowPanel
            };

            // Not width-dependent beyond the row's own cosmetic width (m2
            // 3.6): fixed left-anchored text, same as TextRowRenderer.
            _sink.AddRelayout(w => rowPanel.Size = new Point(w, PlanContentHeightMath.FallbackTextRowHeight));
        }
    }
}
