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
    // Section builders (continued)" region - the Summary/Total Cost section.
    //
    // W4A (Total Cost section redesign, 2026-08-15, user-designed spec - see
    // docs/KNOWN-ISSUES.md): rewritten. The section is now two formula-band
    // tile rows (CreateFormulaBand, replacing the old single flat
    // CreateCostTileRow call over every CoinTotal row at once - see
    // PlanViewModelBuilder.BuildSummarySection for why the cost/profit
    // tiles are now two distinct PlanRowType groups instead of one), a
    // c-table for the plan's non-coin currency costs (CreateCurrencyTable,
    // replacing the old plain-text CreateCurrencyRow), the pre-existing M35
    // multi-item batch MultiItemNote banner row (unchanged, still via
    // TextRowRenderer), and a new subdued footnote row (CreateFootnoteRow).
    // Height agreement for this new shape lives in
    // Services/SummarySectionLayoutMath.BodyHeight, not
    // PlanContentHeightMath (DO-NOT-TOUCH for this package) - see that
    // class's own doc comment for the full rationale.
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
            PlanRowViewModel footnoteRow = null;

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
                        footnoteRow = row;
                        break;
                        // PlanRowType.CoinTotal is never emitted by
                        // PlanViewModelBuilder any more (see that enum
                        // member's own doc comment) - no case needed here.
                }
            }

            if (costBandRows.Count > 0)
            {
                CreateFormulaBand(costBandRows, contentFlow, panelWidth);
            }

            if (profitBandRows.Count > 0)
            {
                CreateFormulaBand(profitBandRows, contentFlow, panelWidth);
            }

            if (currencyRows.Count > 0)
            {
                CreateCurrencyTable(currencyRows, contentFlow, panelWidth);
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

            if (footnoteRow != null)
            {
                CreateFootnoteRow(footnoteRow.Label, contentFlow, panelWidth);
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
        }

        /// <summary>
        /// W4A: a formula band - N equal-width stat tiles reading
        /// left-to-right as a formula ("Total Materials Value - Your
        /// Materials Used = Actual Cost to Craft", or "Sell Value - Total
        /// Materials Value = Profit if Sold"). Callers pass exactly the
        /// rows belonging to ONE band (PlanViewModelBuilder groups
        /// CostFormulaTile/ProfitFormulaTile separately, and Render above
        /// re-groups by that same RowType), so two bands render as two
        /// stacked CostTileRowHeight-tall rows, not one - see
        /// Services/SummarySectionLayoutMath.BodyHeight, which sizes for
        /// exactly that.
        ///
        /// Geometry is unchanged from the pre-W4A CreateCostTileRow (same
        /// PlanRelayoutMath.ComputeCostTileGeometry call, same centering).
        /// The only behavioral addition is the tooltip: row.TooltipText is
        /// set directly on captionLabel itself (M32 lesson - see
        /// PlanRowViewModel.TooltipText's own doc comment), not on
        /// rowPanel, so hovering the header text always shows it
        /// regardless of what other controls might overlap the row.
        /// </summary>
        private void CreateFormulaBand(List<PlanRowViewModel> tileRows, FlowPanel parent, int panelWidth)
        {
            int tileCount = tileRows.Count;
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
                var row = tileRows[i];

                string caption = row.Label ?? "";
                int captionWidth = (int)System.Math.Ceiling(captionFont.MeasureString(caption).Width);
                var captionLabel = new Label()
                {
                    Text = caption,
                    Font = captionFont,
                    TextColor = captionColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(tileX + PlanRelayoutMath.CenterX(geometry.TileWidth, captionWidth), 6),
                    Parent = rowPanel,
                    BasicTooltipText = row.TooltipText
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

        // --- Currency table (W4A - replaces the pre-W4A plain-text
        // CreateCurrencyRow) ---
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
            CreateCurrencyTableHeaderRow(parent, panelWidth);
            for (int i = 0; i < rows.Count; i++)
            {
                CreateCurrencyTableRow(rows[i], parent, panelWidth, i == rows.Count - 1);
            }
        }

        private void CreateCurrencyTableHeaderRow(FlowPanel parent, int panelWidth)
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

            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth);
            var requiredLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Required", font, Color.White, edges.RequiredRightEdge, 5);
            var haveLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Have", font, Color.White, edges.HaveRightEdge, 5);
            var neededLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Needed", font, Color.White, edges.NeededRightEdge, 5);

            _sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, PlanContentHeightMath.CTableHeaderRowHeight);
                var e = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(w);
                requiredLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.RequiredRightEdge, requiredLabel.Width), 5);
                haveLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.HaveRightEdge, haveLabel.Width), 5);
                neededLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.NeededRightEdge, neededLabel.Width), 5);
            });
        }

        // W4A full-coverage marker color: matches PillColors.PillKind.
        // Selected's own green (#1F8F0C) - the established "positive/
        // selected" hue in this codebase - without adding a new PillKind
        // for this single non-tree use (PillColors' enum is shared by the
        // recipe tree's decision pills; a one-off Summary-only marker does
        // not belong on that shared contract).
        private static readonly Color FullCoverageBorder = new Color(31, 143, 12);
        private static readonly Color FullCoverageFill = FullCoverageBorder * 0.15f;

        // W4A glyph note: the spec asked for a "\u2713" (check mark)
        // full-coverage marker, tinted green, with an explicit fallback to
        // a green "OK" text badge if the glyph cannot be verified to
        // render in the Blish font. This module's own prior investigation
        // (docs/dev-notes/HISTORY.md, "Carried follow-up resolved: caret
        // glyphs") deliberately chose ASCII carets over a technically-
        // representable Unicode triangle glyph after LIVE desktop
        // rendering showed the ASCII form was the reliable one across
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

        private void CreateCurrencyTableRow(PlanRowViewModel row, FlowPanel parent, int panelWidth, bool isLast)
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
            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth);
            int nameMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                edges.RequiredRightEdge, SummarySectionLayoutMath.CurrencyNumberColumnWidth, SummarySectionLayoutMath.CurrencyColumnGap, nameX);
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
                // panel + label cover almost the entire pill (outer is
                // only a 1px border ring) - stamping a tooltip on just the
                // returned outer Panel would be swallowed exactly the way
                // field-test finding D (docs/KNOWN-ISSUES.md, "Field-test
                // UX wave") already documented for the tree's pills, and
                // CreateSmallTag does not expose its inner panel/label to
                // stamp all three the way that fix did. Not spec-mandated,
                // so left off rather than shipped half-working.
            }

            // No LabelHelpers.CreateRowDivider here (unlike Required
            // Recipes/Disciplines' RowRelayoutHelpers.FinishRow-based
            // rows): CurrencyRowHeight (28px) was never part of the M36b
            // Container.Paint round-trip simulation sweep (LabelHelpers.
            // CreateRowDivider's doc comment only proves 44px/32px rows
            // vulnerable and 36px rows immune - 28px is neither), and the
            // pre-W4A Summary section deliberately had no per-row dividers
            // at all (this class's own original doc comment: "no
            // list-style rows here"). Introducing a divider at an unproven
            // row height would risk resurrecting exactly the vanishing-
            // divider defect DO-NOT-TOUCH #6 exists to keep away from, for
            // a visual element the W4A spec never explicitly asked for -
            // the header row's dark background already delineates the
            // table. isLast is accepted (matching every other row
            // builder's signature) but intentionally unused.
            _sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, rowHeight);
                var e = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(w);
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
                var e = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(w);
                int newMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                    e.RequiredRightEdge, SummarySectionLayoutMath.CurrencyNumberColumnWidth, SummarySectionLayoutMath.CurrencyColumnGap, nameX);
                string newDisplayName = LabelHelpers.EllipsizeToWidth(font, fullName, newMaxWidth);
                if (nameLabel.Text != newDisplayName)
                {
                    nameLabel.Text = newDisplayName;
                    rowPanel.BasicTooltipText = newDisplayName != fullName ? fullName : null;
                }
            });
        }

        // W4A (user-mandated): a single subdued footnote row at the bottom
        // of the section - deliberately smaller/dimmer than the plain
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
