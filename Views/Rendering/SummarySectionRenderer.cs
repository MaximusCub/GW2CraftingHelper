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
                // The cost band is the plan's headline: its result tile is
                // boxed, plus the "the gold figure is not the whole cost"
                // disclosure line whenever currency rows follow it. Both
                // its row height and the height BodyHeight counts for it
                // come from SummarySectionLayoutMath.CostBandHeight with
                // the SAME currencyRows.Count > 0 condition that decides
                // whether the line is drawn.
                CreateFormulaBand(
                    costBandRows, contentFlow, panelWidth,
                    SummarySectionLayoutMath.CostBandHeight(currencyRows.Count > 0),
                    highlightResult: true,
                    currencyNoteText: SummarySectionLayoutMath.CurrencyRequirementNote(currencyRows.Count),
                    currencyNoteTooltip: SummarySectionLayoutMath.CurrencyRequirementNoteTooltip(currencyRows));
            }

            if (profitBandRows.Count > 0)
            {
                // Derived stats, not the headline - no highlight box and
                // the unchanged CostTileRowHeight band.
                CreateFormulaBand(
                    profitBandRows, contentFlow, panelWidth,
                    PlanContentHeightMath.CostTileRowHeight,
                    highlightResult: false);
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
            public Label NoteLabel;

            // Band-space y of this tile's amount run and disclosure line.
            public int AmountY;
            public int NoteY;

            // The highlight box, on the result tile of a highlighted band
            // only. Its caption/note/amount are its CHILDREN, laid out
            // once against the box's own width, so a resize moves the box
            // and nothing inside it - see CreateFormulaBand.
            public Panel Box;
        }

        // Left edge a collapsed one-tile band's contents start at - the
        // same 8px content gutter the currency table's icon column and the
        // footnote row already use, so the section reads as one left edge.
        private const int LoneTileContentX = 8;

        // Caption y and amount bottom pad of an UNHIGHLIGHTED band (the
        // profit band); 4/6 reproduce the fixed y=30 a CostTileRowHeight
        // band has always drawn its amounts at (56 - 6 - 20 == 30). A
        // highlighted band uses SummarySectionLayoutMath's box-derived
        // pair instead - see CreateFormulaBand.
        private const int BandCaptionY = 4;
        private const int BandAmountBottomPad = 6;

        private static readonly Color BandCaptionColor = new Color(153, 153, 153);

        // The result tile's caption is the one a user is actually looking
        // for; it stays the same size as its siblings (so the band still
        // reads as one formula) but is lifted out of the dim grey.
        private static readonly Color HighlightedCaptionColor = new Color(235, 235, 235);

        // The result tile's highlight box. Warm gold, the coin band's own
        // hue - the eye lands on it without a second colour entering the
        // section. Both are scaled down from one tint rather than written
        // as two literals, the same premultiplied "Color * f" idiom
        // FullCoverageFill below already uses: Blish composites a Panel's
        // BackgroundColor over whatever is behind it, so the window's
        // parchment texture still reads through the fill.
        private static readonly Color ResultHighlightTint = new Color(214, 176, 96);
        private static readonly Color ResultHighlightFill = ResultHighlightTint * 0.14f;
        private static readonly Color ResultHighlightBorder = ResultHighlightTint * 0.5f;

        // Warm, not red: the disclosure is a caveat about scope, not an
        // error, and it must not read as an alarm on an ordinary plan.
        private static readonly Color CurrencyNoteColor = new Color(206, 170, 92);

        /// <summary>
        /// Vertical space an amount run occupies: its own text height, or
        /// the coin icon's if that is taller (at DefaultFont16 the 20px
        /// icons are the tallest thing in the row).
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
        /// the left). It gets the same band height as the three-tile case
        /// and simply starts where everything else in the section starts.
        ///
        /// Every tile's amount renders at the SAME font. The cost band's
        /// result tile used to be promoted to DefaultFont32; the
        /// maintainer's field test replaced that with highlightResult - a
        /// tinted, semi-transparent box around the result tile's
        /// caption+note+amount, which draws the eye without breaking the
        /// band's visual balance. The box is a real Panel and the result
        /// tile's controls are its CHILDREN, so the fill is painted behind
        /// them by the container's own paint order (no z-index games) and a
        /// resize moves one control instead of re-centring three runs.
        /// Amounts are bottom-anchored inside rowHeight rather than pinned
        /// to a fixed y, so no font metric can push an amount out of the
        /// band the caller's height math reserved.
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
            int rowHeight, bool highlightResult,
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

            // A highlighted band carries its result tile's box, so its
            // caption starts one box margin+padding down and its amount
            // stops the same distance above the band's bottom edge; both
            // numbers are the ones CostBandHeight reserved room for.
            int captionY = highlightResult ? SummarySectionLayoutMath.CostBandCaptionY : BandCaptionY;
            int amountBottomPad = highlightResult
                ? SummarySectionLayoutMath.CostBandAmountBottomPad
                : BandAmountBottomPad;

            int captionHeight = (int)System.Math.Ceiling(captionFont.MeasureString("0").Height);
            int noteY = captionY + captionHeight + 2;
            int captionBlockBottom = (currencyNoteText != null ? noteY + captionHeight : captionY + captionHeight) + 2;

            int amountHeight = AmountBlockHeight(amountFont);
            int iconYOffset = (amountHeight - CoinSegmentMath.CoinIconSize) / 2;

            // Clamped below the caption block: rowHeight is a fixed
            // constant while the font's real metrics come from whatever
            // Blish loaded, so a font taller than the band was sized for
            // must overflow downward (loud - the DEBUG assert below catches
            // it) rather than silently overprint the caption.
            int amountY = AmountY(rowHeight, amountHeight, captionBlockBottom, amountBottomPad);

            int boxTop = captionY - SummarySectionLayoutMath.CostBandBoxPadY;
            int boxHeight = amountY + amountHeight + SummarySectionLayoutMath.CostBandBoxPadY - boxTop;

            var tiles = new List<CostTileHandle>(tileCount);
            for (int i = 0; i < tileCount; i++)
            {
                int tileX = geometry.StartX + i * geometry.TileWidth;
                var row = tileRows[i];
                bool isResult = i == tileCount - 1;
                bool boxed = isResult && highlightResult;

                // The disclosure line sits under the RESULT tile's caption -
                // it qualifies that tile's number, not the band as a whole.
                string noteText = isResult ? currencyNoteText : null;

                string caption = row.Label ?? "";
                int captionWidth = (int)System.Math.Ceiling(captionFont.MeasureString(caption).Width);
                int noteWidth = noteText != null
                    ? (int)System.Math.Ceiling(captionFont.MeasureString(noteText).Width)
                    : 0;
                var segments = CoinCurrencyRenderer.BuildCoinSegments(row.CoinValue, amountFont);
                int segmentsWidth = CoinCurrencyRenderer.TotalCoinSegmentsWidth(segments);

                // Where this tile's runs are centred, and what they are
                // centred INSIDE: the box for a boxed tile (whose own x
                // then carries the tile centring), the tile slice
                // otherwise. Never clamped to the tile width - Blish clips
                // a container's children, so a box narrower than its
                // content would cut the amount off where an unboxed tile
                // merely overlaps its neighbour.
                Panel box = null;
                Panel host = rowPanel;
                int hostTop = 0;
                int hostWidth = 0;
                if (boxed)
                {
                    int widest = captionWidth;
                    if (noteWidth > widest) widest = noteWidth;
                    if (segmentsWidth > widest) widest = segmentsWidth;
                    int boxWidth = widest + 2 * SummarySectionLayoutMath.CostBandBoxPadX;

                    box = CreateHighlightBox(
                        rowPanel,
                        ContentX(lone, tileX, geometry.TileWidth, boxWidth),
                        boxTop, boxWidth, boxHeight,
                        out host);
                    hostTop = boxTop + HighlightBoxBorder;
                    hostWidth = boxWidth - 2 * HighlightBoxBorder;
                }

                var captionLabel = new Label()
                {
                    Text = caption,
                    Font = captionFont,
                    TextColor = boxed ? HighlightedCaptionColor : BandCaptionColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(
                        TileContentX(boxed, hostWidth, lone, tileX, geometry.TileWidth, captionWidth),
                        captionY - hostTop),
                    Parent = host
                };
                TooltipFacility.ApplyPlain(captionLabel, row.TooltipText);

                Label noteLabel = null;
                if (noteText != null)
                {
                    noteLabel = new Label()
                    {
                        Text = noteText,
                        Font = captionFont,
                        TextColor = CurrencyNoteColor,
                        AutoSizeWidth = true,
                        AutoSizeHeight = true,
                        Location = new Point(
                            TileContentX(boxed, hostWidth, lone, tileX, geometry.TileWidth, noteWidth),
                            noteY - hostTop),
                        Parent = host
                    };
                    TooltipFacility.ApplyPlain(noteLabel, currencyNoteTooltip);
                }

                var segmentHandle = CoinCurrencyRenderer.LayoutCoinSegments(
                    host, segments,
                    TileContentX(boxed, hostWidth, lone, tileX, geometry.TileWidth, segmentsWidth),
                    amountY - hostTop, amountFont, 1f, iconYOffset);

                tiles.Add(new CostTileHandle
                {
                    CaptionLabel = captionLabel,
                    Segments = segmentHandle,
                    NoteLabel = noteLabel,
                    AmountY = amountY,
                    NoteY = noteY,
                    Box = box
                });
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
                        Location = new Point(boundaryX - symbolWidth / 2, amountY),
                        Parent = rowPanel
                    };
                    operatorLabels.Add(operatorLabel);
                }
            }

            // [FANOUT]: every tile's caption + coin segments are
            // font-only (invariant to panelWidth) - only tileWidth/startX
            // and each tile's own centering offset move. No MeasureString.
            // A boxed tile is one control: its runs are centred inside the
            // box, whose width never changes, so moving the box moves them.
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

                    if (tile.Box != null)
                    {
                        tile.Box.Location = new Point(
                            ContentX(lone, tileX, g.TileWidth, tile.Box.Width), boxTop);
                        continue;
                    }

                    tile.CaptionLabel.Location = new Point(
                        ContentX(lone, tileX, g.TileWidth, tile.CaptionLabel.Width), captionY);

                    if (tile.NoteLabel != null)
                    {
                        tile.NoteLabel.Location = new Point(
                            ContentX(lone, tileX, g.TileWidth, tile.NoteLabel.Width), tile.NoteY);
                    }

                    int segmentsWidth = ShoppingColumnMath.SegmentRunWidth(tile.Segments.TextWidths, CoinSegmentMath.CoinIconSize, CoinSegmentMath.CoinLabelIconGap, CoinSegmentMath.CoinSegmentGap);
                    int coinStartX = ContentX(lone, tileX, g.TileWidth, segmentsWidth);
                    CoinCurrencyRenderer.RepositionSegments(tile.Segments, coinStartX, tile.AmountY);
                }

                if (operatorLabels != null)
                {
                    for (int i = 0; i < operatorLabels.Count; i++)
                    {
                        int boundaryX = g.StartX + (i + 1) * g.TileWidth;
                        var operatorLabel = operatorLabels[i];
                        operatorLabel.Location = new Point(boundaryX - operatorLabel.Width / 2, amountY);
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
            // The highlight box is the taller of the two, so a highlighted
            // band is asserted on the box's own bottom edge.
            System.Diagnostics.Debug.Assert(
                (highlightResult ? boxTop + boxHeight : amountY + amountHeight) <= rowHeight,
                "SummarySectionRenderer: a formula band's amounts (and its highlight box) must fit " +
                "inside the row height SummarySectionLayoutMath reserved for it - see CostBandHeight.");
#endif
        }

        // The highlight box's 1px border, drawn the way
        // LabelHelpers.CreateSmallTag draws a pill's: an outer panel in the
        // border colour with the fill panel inset inside it.
        private const int HighlightBoxBorder = 1;

        /// <summary>
        /// The result tile's highlight box. Returns the OUTER panel (what a
        /// resize moves) and hands back the inner fill panel every control
        /// inside the box is parented to - children paint over their
        /// container's background, so nothing here depends on sibling
        /// z-order.
        /// </summary>
        private static Panel CreateHighlightBox(
            Panel parent, int x, int y, int width, int height, out Panel content)
        {
            var outer = new Panel()
            {
                Size = new Point(width, height),
                Location = new Point(x, y),
                BackgroundColor = ResultHighlightBorder,
                Parent = parent
            };
            content = new Panel()
            {
                Size = new Point(width - 2 * HighlightBoxBorder, height - 2 * HighlightBoxBorder),
                Location = new Point(HighlightBoxBorder, HighlightBoxBorder),
                BackgroundColor = ResultHighlightFill,
                Parent = outer
            };
            return outer;
        }

        /// <summary>
        /// Left x of one run inside its tile: centred in the highlight box
        /// for a boxed tile (the box's own x carries the tile centring),
        /// otherwise <see cref="ContentX"/> against the tile slice.
        /// </summary>
        private static int TileContentX(
            bool boxed, int boxContentWidth, bool lone, int tileX, int tileWidth, int contentWidth)
        {
            return boxed
                ? PlanRelayoutMath.CenterX(boxContentWidth, contentWidth)
                : ContentX(lone, tileX, tileWidth, contentWidth);
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
        private static int AmountY(int rowHeight, int amountHeight, int captionBlockBottom, int bottomPad)
        {
            int y = rowHeight - bottomPad - amountHeight;
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
            // The same pass measures the widest UNTRUNCATED currency name,
            // which caps where the Required column may start (audit batch
            // H: the numbers used to sit against the panel edge however
            // short "Karma"/"Spirit Shard" are) - see
            // SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel.
            var font = GameService.Content.DefaultFont14;
            int widestNumberWidth = 0;
            int widestNameEnd = 0;
            foreach (var row in rows)
            {
                int rowWidest = MeasureWidestCurrencyNumber(row, font);
                if (rowWidest > widestNumberWidth) widestNumberWidth = rowWidest;

                int nameEnd = SummarySectionLayoutMath.CurrencyNameX
                    + (int)System.Math.Ceiling(font.MeasureString(row.Label ?? "").Width);
                if (nameEnd > widestNameEnd) widestNameEnd = nameEnd;
            }

            CreateCurrencyTableHeaderRow(parent, panelWidth, widestNumberWidth, widestNameEnd);
            for (int i = 0; i < rows.Count; i++)
            {
                CreateCurrencyTableRow(rows[i], parent, panelWidth, widestNumberWidth, widestNameEnd);
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

        private void CreateCurrencyTableHeaderRow(
            FlowPanel parent, int panelWidth, int widestNumberWidth, int widestNameEnd)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(
                    SummarySectionLayoutMath.CurrencyHeaderBandWidth(panelWidth, widestNumberWidth, widestNameEnd),
                    TableHeaderStyle.RowHeight),
                BackgroundColor = TableHeaderStyle.BandColor,
                Parent = parent
            };
            var font = TableHeaderStyle.Font;
            new Label()
            {
                Text = "Currency", Font = font, TextColor = TableHeaderStyle.LabelColor,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(SummarySectionLayoutMath.CurrencyNameX, TableHeaderStyle.LabelY),
                Parent = rowPanel
            };

            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel(
                panelWidth, widestNumberWidth, widestNameEnd);
            var requiredLabel = LabelHelpers.CreateRightAlignedLabel(
                rowPanel, "Required", font, TableHeaderStyle.LabelColor, edges.RequiredRightEdge, TableHeaderStyle.LabelY);
            var haveLabel = LabelHelpers.CreateRightAlignedLabel(
                rowPanel, "Have", font, TableHeaderStyle.LabelColor, edges.HaveRightEdge, TableHeaderStyle.LabelY);
            var neededLabel = LabelHelpers.CreateRightAlignedLabel(
                rowPanel, "Needed", font, TableHeaderStyle.LabelColor, edges.NeededRightEdge, TableHeaderStyle.LabelY);

            // WidestNumberWidth is cached from the build-time
            // pre-scan (data-derived, not panelWidth-derived - it never
            // needs to re-run on resize, same reasoning as
            // ShoppingListSectionRenderer's own cached maxEachWidth/
            // maxTotalWidth).
            _sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(
                    SummarySectionLayoutMath.CurrencyHeaderBandWidth(w, widestNumberWidth, widestNameEnd),
                    TableHeaderStyle.RowHeight);
                var e = SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel(
                    w, widestNumberWidth, widestNameEnd);
                requiredLabel.Location = new Point(
                    PlanRelayoutMath.RightAlignedX(e.RequiredRightEdge, requiredLabel.Width), TableHeaderStyle.LabelY);
                haveLabel.Location = new Point(
                    PlanRelayoutMath.RightAlignedX(e.HaveRightEdge, haveLabel.Width), TableHeaderStyle.LabelY);
                neededLabel.Location = new Point(
                    PlanRelayoutMath.RightAlignedX(e.NeededRightEdge, neededLabel.Width), TableHeaderStyle.LabelY);
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

        private void CreateCurrencyTableRow(
            PlanRowViewModel row, FlowPanel parent, int panelWidth, int widestNumberWidth, int widestNameEnd)
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
            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel(
                panelWidth, widestNumberWidth, widestNameEnd);
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
                TooltipFacility.ApplyPlain(nameLabel, fullName);
                TooltipFacility.ApplyPlain(rowPanel, fullName);
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
                var e = SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel(
                    w, widestNumberWidth, widestNameEnd);
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
                var e = SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel(
                    w, widestNumberWidth, widestNameEnd);
                int newMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                    e.RequiredRightEdge, numberColumnWidth, SummarySectionLayoutMath.CurrencyColumnGap, nameX);
                string newDisplayName = LabelHelpers.EllipsizeToWidth(font, fullName, newMaxWidth);
                if (nameLabel.Text != newDisplayName)
                {
                    nameLabel.Text = newDisplayName;
                    // Both controls, same reasoning as the
                    // build-time tooltip assignment above.
                    string tooltip = newDisplayName != fullName ? fullName : null;
                    TooltipFacility.ApplyPlain(nameLabel, tooltip);
                    TooltipFacility.ApplyPlain(rowPanel, tooltip);
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
