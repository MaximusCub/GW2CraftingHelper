using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    // SummarySectionLayoutMath is the
    // redesigned Summary section's own layout arithmetic, deliberately kept
    // separate from PlanContentHeightMath/PlanRelayoutMath (both DO-NOT-
    // TOUCH for this package) - see that class's own doc comment.
    public class SummarySectionLayoutMathTests
    {
        private static PlanRowViewModel Row(PlanRowType type)
        {
            return new PlanRowViewModel { RowType = type };
        }

        // --- BodyHeight ---
        [Fact]
        public void BodyHeight_Null_ReturnsZero()
        {
            Assert.Equal(0, SummarySectionLayoutMath.BodyHeight(null));
        }

        [Fact]
        public void BodyHeight_Empty_ReturnsZero()
        {
            Assert.Equal(0, SummarySectionLayoutMath.BodyHeight(new List<PlanRowViewModel>()));
        }

        [Fact]
        public void BodyHeight_CollapsedCostBandPlusFootnote()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile), // collapsed - still just ONE tile ROW
                Row(PlanRowType.SummaryFootnote),
            };

            int expected = SummarySectionLayoutMath.CostBandHeight(false) + PlanContentHeightMath.FallbackTextRowHeight;
            Assert.Equal(expected, SummarySectionLayoutMath.BodyHeight(rows));
        }

        [Fact]
        public void BodyHeight_ExpandedCostBand_StillOneTileRowRegardlessOfTileCount()
        {
            // 3 CostFormulaTile rows (the uncollapsed band) render as ONE
            // CostTileRowHeight-tall row of 3 tiles, not 3 separate rows.
            var rows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.SummaryFootnote),
            };

            int expected = SummarySectionLayoutMath.CostBandHeight(false) + PlanContentHeightMath.FallbackTextRowHeight;
            Assert.Equal(expected, SummarySectionLayoutMath.BodyHeight(rows));
        }

        [Fact]
        public void BodyHeight_BothBandsPresent_TwoSeparateTileRows()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.ProfitFormulaTile),
                Row(PlanRowType.ProfitFormulaTile),
                Row(PlanRowType.ProfitFormulaTile),
                Row(PlanRowType.SummaryFootnote),
            };

            int expected = SummarySectionLayoutMath.CostBandHeight(false)
                + PlanContentHeightMath.CostTileRowHeight
                + PlanContentHeightMath.FallbackTextRowHeight;
            Assert.Equal(expected, SummarySectionLayoutMath.BodyHeight(rows));
        }

        [Fact]
        public void BodyHeight_CurrencyRows_HeaderPlusOnePerRow()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.CurrencyCost),
                Row(PlanRowType.CurrencyCost),
                Row(PlanRowType.CurrencyCost),
                Row(PlanRowType.SummaryFootnote),
            };

            int expected = SummarySectionLayoutMath.CostBandHeight(true)
                + SummarySectionLayoutMath.CurrencyTableTopGap
                + PlanContentHeightMath.ColumnHeaderRowHeight + 3 * PlanContentHeightMath.CurrencyRowHeight
                + PlanContentHeightMath.FallbackTextRowHeight;
            Assert.Equal(expected, SummarySectionLayoutMath.BodyHeight(rows));
        }

        [Fact]
        public void BodyHeight_NoCurrencyRows_NoHeaderHeightCounted()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.SummaryFootnote),
            };

            int expected = SummarySectionLayoutMath.CostBandHeight(false) + PlanContentHeightMath.FallbackTextRowHeight;
            Assert.Equal(expected, SummarySectionLayoutMath.BodyHeight(rows));
        }

        [Fact]
        public void BodyHeight_MultiItemNoteRow_AddsOneFallbackTextRow()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.ProfitFormulaTile),
                Row(PlanRowType.ProfitFormulaTile),
                Row(PlanRowType.ProfitFormulaTile),
                Row(PlanRowType.MultiItemNote),
                Row(PlanRowType.SummaryFootnote),
            };

            int expected = SummarySectionLayoutMath.CostBandHeight(false)
                + PlanContentHeightMath.CostTileRowHeight
                + 2 * PlanContentHeightMath.FallbackTextRowHeight;
            Assert.Equal(expected, SummarySectionLayoutMath.BodyHeight(rows));
        }

        [Fact]
        public void BodyHeight_FullSection_EveryElementPresent()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.ProfitFormulaTile),
                Row(PlanRowType.ProfitFormulaTile),
                Row(PlanRowType.ProfitFormulaTile),
                Row(PlanRowType.CurrencyCost),
                Row(PlanRowType.CurrencyCost),
                Row(PlanRowType.MultiItemNote),
                Row(PlanRowType.SummaryFootnote),
            };

            int expected = SummarySectionLayoutMath.CostBandHeight(true)
                + PlanContentHeightMath.CostTileRowHeight
                + SummarySectionLayoutMath.CurrencyTableTopGap
                + PlanContentHeightMath.ColumnHeaderRowHeight + 2 * PlanContentHeightMath.CurrencyRowHeight
                + 2 * PlanContentHeightMath.FallbackTextRowHeight;
            Assert.Equal(expected, SummarySectionLayoutMath.BodyHeight(rows));
        }

        // --- CostBandHeight + the currency disclosure line ---
        //
        // Re-baselined for the cost-band restyle: the result tile no
        // longer carries a promoted display-font amount (which is what
        // made the band 76 tall), it carries a highlight box at the band's
        // one shared amount font, so the band's height is now the box's
        // margin+padding around a caption line, an optional disclosure
        // line and one coin run. When the plan has currency costs the band
        // still grows by exactly that disclosure line. Both numbers are
        // pinned absolutely here (not recomputed from the same constants
        // the production formula reads, which could never fail): a
        // deliberate geometry change re-baselines these literals.
        [Fact]
        public void CostBandHeight_NoCurrencyNote_IsTheBoxedCaptionPlusAmountBand()
        {
            // 6 margin + 6 pad + 29 caption line + 8 label-to-value gap
            // + 20 amount run + 6 pad + 6 margin. The amount run is 20
            // because the amount TEXT is 20; it read as "the coin icon" only
            // while inline coins also drew at 20, and stayed 20 when they
            // moved onto the 16px wallet BAR tier.
            Assert.Equal(81, SummarySectionLayoutMath.CostBandHeight(false));
        }

        [Fact]
        public void CostBandHeight_WithCurrencyNote_AddsExactlyOneNoteLine()
        {
            Assert.Equal(81 + 23, SummarySectionLayoutMath.CostBandHeight(true));
            Assert.Equal(
                SummarySectionLayoutMath.CostBandHeight(false) + SummarySectionLayoutMath.CostBandCurrencyNoteHeight,
                SummarySectionLayoutMath.CostBandHeight(true));
        }

        // --- The label-to-value gap (defect: "'Sell Value' and the gold
        // line are a little cramped") ---
        [Fact]
        public void LabelToValueGap_IsOneConstantSharedByBothBands()
        {
            // The whole point of the constant: the cost band and the profit
            // band read the SAME number, so no future edit can leave one
            // breathing and the other cramped.
            Assert.Equal(
                PlanContentHeightMath.CostTileLabelToValueGap,
                SummarySectionLayoutMath.CostBandCaptionToAmountGap);
        }

        [Fact]
        public void LabelToValueGap_IsSizedFromTheCaptionTiersOwnMetrics()
        {
            // Derived, not eyeballed: a label and its value read as one
            // group while the space between them stays under about one cap
            // height, and read as touching well below half of it. The gap
            // is the 4pt-scale step at half the caption tier's cap height.
            var caption = TypeRampMetrics.ColumnHeaderInk;
            int gap = PlanContentHeightMath.CostTileLabelToValueGap;

            // Half the tier's cap height, snapped down to the 4pt scale.
            Assert.Equal(caption.CapHeight / 2 / 4 * 4, gap);

            // And it must actually clear the caption's descenders, which
            // hang one pixel past its own line box at this tier.
            Assert.True(
                gap > caption.LowestInk - caption.LineHeight,
                $"a {gap}px gap does not clear a {caption.LowestInk - caption.LineHeight}px descender overhang");
        }

        [Fact]
        public void BandAmountY_HangsTheAmountTheGapUnderTheCaption_WhateverTheBandIs()
        {
            // Both bands call this with their own measured caption bottom
            // and get the same distance - it takes no row height and no
            // bottom pad, which is exactly what made the two drift apart
            // when each bottom-anchored inside its own fixed row.
            Assert.Equal(
                31 + PlanContentHeightMath.CostTileLabelToValueGap,
                SummarySectionLayoutMath.BandAmountY(31));
            Assert.Equal(
                12 + PlanContentHeightMath.CostTileLabelToValueGap,
                SummarySectionLayoutMath.BandAmountY(12));
        }

        // --- The disclosure line moved BELOW the amount (defect: "the dead
        // space between 'Total Materials Value' and the currency data below
        // it looks odd") ---
        [Fact]
        public void BandNoteY_HangsTheDisclosureUnderTheAmount_NotAboveIt()
        {
            int amountY = SummarySectionLayoutMath.BandAmountY(37);
            int noteY = SummarySectionLayoutMath.BandNoteY(amountY, PlanContentHeightMath.AmountRunHeight);

            Assert.Equal(
                amountY + PlanContentHeightMath.AmountRunHeight + SummarySectionLayoutMath.CostBandAmountToNoteGap,
                noteY);
            Assert.True(noteY > amountY + PlanContentHeightMath.AmountRunHeight);
        }

        [Fact]
        public void CurrencyNote_DoesNotMoveTheAmountRun_SoAllThreeTilesShareOneCoinLine()
        {
            // The defect this replaced: the note was counted between the
            // caption and a BOTTOM-anchored amount, so a currency-bearing
            // plan pushed every tile's coin run down by the note's height
            // while only the result tile had anything in the space it left.
            // BandAmountY now has no note term to pass - the structural
            // guarantee - and the band grows DOWNWARD for the note instead.
            int amountBottom = SummarySectionLayoutMath.BandAmountY(
                SummarySectionLayoutMath.CostBandCaptionY + TypeRampMetrics.ColumnHeaderInk.LineHeight)
                + PlanContentHeightMath.AmountRunHeight;

            Assert.True(
                amountBottom + SummarySectionLayoutMath.CostBandAmountBottomPad
                    <= SummarySectionLayoutMath.CostBandHeight(false),
                "the note-free band must already hold the amount run at the shared y");
            Assert.True(
                amountBottom + SummarySectionLayoutMath.CostBandCurrencyNoteHeight
                    + SummarySectionLayoutMath.CostBandAmountBottomPad
                    <= SummarySectionLayoutMath.CostBandHeight(true),
                "the note-bearing band holds the same run PLUS the footnote under it");
            Assert.Equal(
                SummarySectionLayoutMath.CostBandCurrencyNoteHeight,
                SummarySectionLayoutMath.CostBandHeight(true) - SummarySectionLayoutMath.CostBandHeight(false));
        }

        [Fact]
        public void CostBandBoxHeight_MeasuredOffTheNote_EnclosesIt()
        {
            // Blish clips a container's children, so a box measured off the
            // amount alone would crop the footnote hanging under it.
            int amountY = SummarySectionLayoutMath.BandAmountY(37);
            int amountBottom = amountY + PlanContentHeightMath.AmountRunHeight;
            int noteBottom = SummarySectionLayoutMath.BandNoteY(amountY, PlanContentHeightMath.AmountRunHeight)
                + TypeRampMetrics.CaptionInk.LineHeight;

            int amountOnlyBox = SummarySectionLayoutMath.CostBandBoxHeight(amountBottom);
            int withNoteBox = SummarySectionLayoutMath.CostBandBoxHeight(noteBottom);

            Assert.True(withNoteBox > amountOnlyBox);
            Assert.Equal(noteBottom - amountBottom, withNoteBox - amountOnlyBox);
        }

        // --- The section-separation gap above the currency table (defect:
        // "there isn't enough padding or open space between the bottom of
        // the Total cost section before the currency table's header row") ---
        [Fact]
        public void CurrencyTableTopGap_SeparatesLessThanAWholeSectionBoundary()
        {
            // On the 4pt scale, and deliberately one step under the 16px
            // CraftingPlanView.SectionSpacing puts between two SECTIONS:
            // this is a boundary inside one, so it must read as the lesser
            // of the two. (16 is aliased here rather than referenced - a
            // Blish-free Services test may not reach into a view.)
            const int sectionSpacing = 16;

            Assert.Equal(0, SummarySectionLayoutMath.CurrencyTableTopGap % 4);
            Assert.InRange(SummarySectionLayoutMath.CurrencyTableTopGap, 8, sectionSpacing - 4);
        }

        [Fact]
        public void BodyHeight_CurrencyTable_ReservesItsSeparationGapExactlyOnce()
        {
            var oneRow = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.CurrencyCost),
            };
            var threeRows = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.CurrencyCost),
                Row(PlanRowType.CurrencyCost),
                Row(PlanRowType.CurrencyCost),
            };

            // The gap is a property of the BOUNDARY, not of the rows: two
            // more currency rows add two row heights and nothing else.
            Assert.Equal(
                2 * PlanContentHeightMath.CurrencyRowHeight,
                SummarySectionLayoutMath.BodyHeight(threeRows) - SummarySectionLayoutMath.BodyHeight(oneRow));

            Assert.Equal(
                SummarySectionLayoutMath.CostBandHeight(true)
                    + SummarySectionLayoutMath.CurrencyTableTopGap
                    + PlanContentHeightMath.ColumnHeaderRowHeight
                    + PlanContentHeightMath.CurrencyRowHeight,
                SummarySectionLayoutMath.BodyHeight(oneRow));
        }

        [Fact]
        public void CostBandCaptionReserve_CoversTheCaptionTierItActuallyDraws()
        {
            // The reserve is deliberately above the real metric: the
            // renderer places the caption from live font metrics and clamps
            // the amount below it, so a reserve under the real line height
            // makes the band clip its own amount (its DEBUG assert is what
            // catches that at runtime - this catches it at build time).
            Assert.True(
                TypeRampMetrics.ColumnHeaderInk.LineHeight
                    <= SummarySectionLayoutMath.CostBandCaptionLineHeight,
                $"caption line box {TypeRampMetrics.ColumnHeaderInk.LineHeight} exceeds the "
                    + $"{SummarySectionLayoutMath.CostBandCaptionLineHeight}px reserve");

            // The disclosure line under the AMOUNT stays Caption, and its
            // own reserve is that tier's lowest ink plus the gap above it,
            // so a descender on "+ 2 Currencies Required" lands inside the
            // band rather than on the row under it.
            Assert.Equal(
                SummarySectionLayoutMath.CostBandCurrencyNoteHeight,
                SummarySectionLayoutMath.CostBandAmountToNoteGap + TypeRampMetrics.CaptionInk.LowestInk);
        }

        [Fact]
        public void CostBandHeight_IsTallerThanTheProfitBand_ByTheHighlightBoxsOwnRoom()
        {
            // The whole reason the cost band is still the taller of the
            // two: its result tile is boxed and the box needs its own
            // margin and padding, top and bottom. Nothing about the amount
            // font differs between the bands any more, and since the
            // label-to-value gap became one shared constant, nothing about
            // their internal spacing does either - the two bands now differ
            // by exactly the box's own room.
            Assert.True(SummarySectionLayoutMath.CostBandHeight(false) > PlanContentHeightMath.CostTileRowHeight);
            Assert.Equal(
                2 * (SummarySectionLayoutMath.CostBandBoxMarginY + SummarySectionLayoutMath.CostBandBoxPadY),
                SummarySectionLayoutMath.CostBandHeight(false)
                    - (SummarySectionLayoutMath.CostBandCaptionLineHeight
                        + SummarySectionLayoutMath.CostBandCaptionToAmountGap
                        + PlanContentHeightMath.AmountRunHeight));

            Assert.Equal(
                2 * SummarySectionLayoutMath.CostBandBoxMarginY
                    + 2 * SummarySectionLayoutMath.CostBandBoxPadY
                    - PlanContentHeightMath.CostTileCaptionY
                    - PlanContentHeightMath.CostTileAmountBottomPad,
                SummarySectionLayoutMath.CostBandHeight(false) - PlanContentHeightMath.CostTileRowHeight);
        }

        [Fact]
        public void CostBandHeight_LeavesTheHighlightBoxInsideTheBand()
        {
            // The geometry the renderer actually builds, through the same
            // functions it calls: BandAmountY hangs the amount under the
            // measured caption, BandNoteY hangs the footnote under that,
            // the box spans CostBandBoxTop to one pad below the lowest of
            // them, and that bottom edge - the band's lowest ink - must sit
            // inside the height the math reserved. The renderer's DEBUG
            // assert fails loud otherwise, but only at runtime and only in
            // DEBUG.
            //
            // captionBlockBottom is a MEASURED input (the caption font's
            // real metrics), so it is swept from the tightest plausible
            // value up to the full reserve rather than assumed. The reserve
            // is what the band's own height was sized from, so a caption
            // anywhere inside it must still leave the box enclosed.
            foreach (bool hasNote in new[] { false, true })
            {
                int rowHeight = SummarySectionLayoutMath.CostBandHeight(hasNote);
                int reserve = SummarySectionLayoutMath.CostBandCaptionY
                    + SummarySectionLayoutMath.CostBandCaptionLineHeight;

                Assert.True(SummarySectionLayoutMath.CostBandBoxTop >= 0);

                for (int captionBlockBottom = SummarySectionLayoutMath.CostBandCaptionY;
                    captionBlockBottom <= reserve;
                    captionBlockBottom++)
                {
                    int amountY = SummarySectionLayoutMath.BandAmountY(captionBlockBottom);
                    int contentBottom = amountY + PlanContentHeightMath.AmountRunHeight;
                    if (hasNote)
                    {
                        contentBottom = SummarySectionLayoutMath.BandNoteY(
                            amountY, PlanContentHeightMath.AmountRunHeight)
                            + TypeRampMetrics.CaptionInk.LineHeight;
                    }

                    int boxHeight = SummarySectionLayoutMath.CostBandBoxHeight(contentBottom);

                    // The caption block must clear the amount run rather
                    // than be overprinted by it.
                    Assert.True(amountY >= captionBlockBottom);
                    Assert.True(
                        SummarySectionLayoutMath.CostBandBoxTop + boxHeight <= rowHeight,
                        $"box overflows a {rowHeight}px band (hasNote={hasNote}, "
                            + $"captionBlockBottom={captionBlockBottom})");
                }
            }
        }

        [Fact]
        public void CostBandBoxWidth_AddsExactlyOnePadEachSide_AndFitsItsTileSlice()
        {
            // The box is never clamped to its tile slice (Blish clips a
            // container's children, so a narrow box would cut the amount
            // off), which makes the padding its only margin for error.
            // These are the band's own arguments to ComputeCostTileGeometry
            // - see Views/Rendering/SummarySectionRenderer.CreateFormulaBand.
            const int totalMargin = 40;
            const int minTileWidth = 80;

            // Deliberately narrower than any band the module can now
            // present: the plan panel at the 1436px window minimum is
            // 1310px wide (see PlanRelayoutMathTests' chrome derivation),
            // so 860 leaves the three-tile arithmetic tested with ~450px in
            // hand rather than at the boundary.
            var geometry = PlanRelayoutMath.ComputeCostTileGeometry(860, 3, totalMargin, minTileWidth);

            Assert.Equal(
                2 * SummarySectionLayoutMath.CostBandBoxPadX,
                SummarySectionLayoutMath.CostBandBoxWidth(0));

            // The widest run a real result tile carries is the disclosure
            // line ("+ N Currencies Required") at the caption font, which
            // measures well under 160px; the box must clear that with both
            // pads even in the narrowest tile slice.
            Assert.True(SummarySectionLayoutMath.CostBandBoxWidth(160) <= geometry.TileWidth);

            // And the boundary itself, so a later pad bump is caught.
            int widestThatFits = geometry.TileWidth - 2 * SummarySectionLayoutMath.CostBandBoxPadX;
            Assert.True(SummarySectionLayoutMath.CostBandBoxWidth(widestThatFits) <= geometry.TileWidth);
            Assert.True(SummarySectionLayoutMath.CostBandBoxWidth(widestThatFits + 1) > geometry.TileWidth);
        }

        [Fact]
        public void BodyHeight_CurrencyRowsPresent_CostBandGrowsByTheNoteLine()
        {
            // The exact coupling the disclosure line depends on: the same
            // "at least one CurrencyCost row" condition that makes the
            // renderer draw the line must make BodyHeight reserve room for
            // it, or the section body clips its own headline figure.
            var withoutCurrency = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.SummaryFootnote),
            };
            var withCurrency = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.CurrencyCost),
                Row(PlanRowType.SummaryFootnote),
            };

            int currencyTableHeight =
                SummarySectionLayoutMath.CurrencyTableTopGap
                + PlanContentHeightMath.ColumnHeaderRowHeight
                + PlanContentHeightMath.CurrencyRowHeight;

            Assert.Equal(
                SummarySectionLayoutMath.BodyHeight(withoutCurrency)
                    + currencyTableHeight
                    + SummarySectionLayoutMath.CostBandCurrencyNoteHeight,
                SummarySectionLayoutMath.BodyHeight(withCurrency));
        }

        [Fact]
        public void CurrencyRequirementNote_NoCurrencies_IsNull()
        {
            Assert.Null(SummarySectionLayoutMath.CurrencyRequirementNote(0));
            Assert.Null(SummarySectionLayoutMath.CurrencyRequirementNote(-1));
        }

        [Fact]
        public void CurrencyRequirementNote_OneCurrency_ReadsSingular()
        {
            Assert.Equal("+ 1 Currency Required",
                SummarySectionLayoutMath.CurrencyRequirementNote(1));
        }

        [Fact]
        public void CurrencyRequirementNote_ManyCurrencies_StatesTheCount()
        {
            Assert.Equal("+ 3 Currencies Required",
                SummarySectionLayoutMath.CurrencyRequirementNote(3));
        }

        [Fact]
        public void CurrencyRequirementNoteTooltip_ListsEveryCurrencyName()
        {
            var rows = new List<PlanRowViewModel>
            {
                new PlanRowViewModel { RowType = PlanRowType.CurrencyCost, Label = "Blue Prophet Shard" },
                new PlanRowViewModel { RowType = PlanRowType.CurrencyCost, Label = "Fractal Relic" },
                new PlanRowViewModel { RowType = PlanRowType.CurrencyCost, Label = "Spirit Shard" },
            };

            string tooltip = SummarySectionLayoutMath.CurrencyRequirementNoteTooltip(rows);

            Assert.StartsWith("Blue Prophet Shard, Fractal Relic, Spirit Shard", tooltip);
            Assert.Contains("Currency table below", tooltip);
        }

        [Fact]
        public void CurrencyRequirementNoteTooltip_NoRowsOrNoNames_IsNull()
        {
            Assert.Null(SummarySectionLayoutMath.CurrencyRequirementNoteTooltip(null));
            Assert.Null(SummarySectionLayoutMath.CurrencyRequirementNoteTooltip(new List<PlanRowViewModel>()));
            Assert.Null(SummarySectionLayoutMath.CurrencyRequirementNoteTooltip(
                new List<PlanRowViewModel> { Row(PlanRowType.CurrencyCost) }));
        }

        // --- ComputeCurrencyColumnEdges ---

        // The previous
        // version of this test recomputed ComputeCurrencyColumnEdges' exact
        // formula from the same public constants it was verifying, so it
        // could never fail unless both sides moved together. Expected
        // values below are hard-coded pixel numbers for two panel widths -
        // a conscious re-baseline point. If a deliberate geometry change
        // moves these, recompute by hand from SummarySectionLayoutMath.cs's
        // CurrencyMarkerWidth/CurrencyColumnGap/CurrencyNameX/
        // CurrencyTrackCount constants (widestNumberWidth defaults to 0)
        // and update the literals here.
        [Fact]
        public void ComputeCurrencyColumnEdges_DistributesTheColumnsAcrossThePanel()
        {
            // 800: pinned right edge 792, marker 758, table right edge 744.
            // The name starts at CurrencyNameX 48 (8 gutter + the 32px
            // wallet-LIST icon + 8), so 696px carry four equal 174px tracks.
            // A 60px band CENTRED on a 174px track starts (174-60)/2 = 57
            // into it and ends 117 into it, so Required ends at 48+174+117
            // = 339, Have one track later at 513 and Needed at 687 - half a
            // track short of the last track's own end, which is where a
            // centred band stops.
            var edges800 = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(800);
            Assert.Equal(339, edges800.RequiredRightEdge);
            Assert.Equal(513, edges800.HaveRightEdge);
            Assert.Equal(687, edges800.NeededRightEdge);
            Assert.Equal(758, edges800.MarkerX);

            // 1200: table right edge 1144, span 1096, tracks 274, a 60px
            // band 107 into each one.
            var edges1200 = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(1200);
            Assert.Equal(489, edges1200.RequiredRightEdge);
            Assert.Equal(763, edges1200.HaveRightEdge);
            Assert.Equal(1037, edges1200.NeededRightEdge);
            Assert.Equal(1158, edges1200.MarkerX);
        }

        [Fact]
        public void CurrencyColumnEdges_CarryTheBandEveryHeaderCentresOver()
        {
            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(1200, 90);

            // Floored at the widest of the three header labels by the
            // caller's pre-scan; widened here to 90, which is what a
            // 7-digit Karma balance measures.
            Assert.Equal(90, edges.NumberColumnWidth);
            Assert.Equal(edges.RequiredRightEdge - 90, edges.RequiredBandX);
            Assert.Equal(edges.HaveRightEdge - 90, edges.HaveBandX);
            Assert.Equal(edges.NeededRightEdge - 90, edges.NeededBandX);
        }

        [Fact]
        public void CurrencyHeader_AndTheNumbersUnderIt_ShareTheTracksCentreLine()
        {
            // The law: header and cells centre on the same axis. The
            // numbers right-align inside the band, so the band's centre is
            // the axis both have to agree on.
            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(1200);
            const int headerWidth = 44;

            int headerX = JustifiedColumnTracks.CenteredInBand(
                edges.HaveBandX, edges.NumberColumnWidth, headerWidth);

            Assert.Equal(
                edges.HaveRightEdge - (edges.NumberColumnWidth / 2),
                headerX + (headerWidth / 2));
        }

        [Fact]
        public void CurrencyRowTextY_CentresTheRowsTextTheSameWayItsIconIsCentred()
        {
            // The row's icon and coverage marker centre themselves in the
            // row; its name and numbers used a hard-coded 4, which centred
            // a Body line box only in the 28px row this table drew before
            // the icon took the wallet-LIST tier. Derived from the row it
            // is actually drawn in, the two cannot part company again.
            int rowHeight = PlanContentHeightMath.CurrencyRowHeight;
            int textY = SummarySectionLayoutMath.CurrencyRowTextY;

            Assert.Equal(
                (rowHeight - TypeRampMetrics.BodyInk.LineHeight) / 2,
                textY);

            // Same centring rule the icon beside it uses, within the pixel
            // integer division can cost.
            int iconY = (rowHeight - SummarySectionLayoutMath.CurrencyIconSize) / 2;
            Assert.InRange(
                (textY + TypeRampMetrics.BodyInk.LineHeight / 2)
                    - (iconY + SummarySectionLayoutMath.CurrencyIconSize / 2),
                -1, 1);

            // And the text's own ink stays inside the row.
            Assert.True(textY >= 0);
            Assert.True(textY + TypeRampMetrics.BodyInk.LowestInk <= rowHeight);
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_TracksAreEvenlySpaced_NotPackedRight()
        {
            // The defect: "the currency name all the way left aligned with
            // the columns with their data all the way right aligned.. its
            // hard to track which label belongs to which row with so much
            // wide distance in between". The fix is a regular pitch - the
            // three numeric anchors sit one track apart, so the eye has
            // something to land on between the name and the last column.
            foreach (int panelWidth in new[] { 800, 1200, 1310, 1920 })
            {
                var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth);

                int band = SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(0);
                int firstPitch = edges.HaveRightEdge - edges.RequiredRightEdge;
                int secondPitch = edges.NeededRightEdge - edges.HaveRightEdge;

                // Integer division puts at most a pixel between tracks.
                Assert.InRange(firstPitch - secondPitch, -1, 1);

                // And each band sits on its own track's CENTRE line - the
                // name takes track 0, so the band of column i (1-based)
                // centres (2i+1) half-tracks past the name's left edge.
                // Doubled rather than halved so a half-track stays an
                // integer; the span is the table's own, not a pitch, so the
                // only slack left is what integer division costs.
                int span = edges.MarkerX - SummarySectionLayoutMath.CurrencyColumnGap
                    - SummarySectionLayoutMath.CurrencyNameX;
                int[] bandRightEdges = new[]
                {
                    edges.RequiredRightEdge, edges.HaveRightEdge, edges.NeededRightEdge,
                };
                for (int i = 0; i < bandRightEdges.Length; i++)
                {
                    int centre = bandRightEdges[i] - (band / 2);
                    Assert.InRange(
                        2 * (centre - SummarySectionLayoutMath.CurrencyNameX)
                            - (((2 * (i + 1)) + 1) * span / SummarySectionLayoutMath.CurrencyTrackCount),
                        -2, 2);
                }

                // And the first number lands near the middle of the row
                // rather than out at its right edge, which is what the
                // packed stack did.
                Assert.InRange(
                    edges.RequiredRightEdge,
                    panelWidth / 4,
                    panelWidth / 2 + SummarySectionLayoutMath.CurrencyNameX);
            }
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_NameColumnStaysReadable()
        {
            // Even distribution is only right while the name keeps a real
            // budget: a quarter of the row, less the Required column's own
            // reserve. At every width the plan panel can present, that is
            // several times the widest currency name the API returns
            // ("Legendary Insight" measures well under 200px at Body).
            foreach (int panelWidth in new[] { 800, 1310, 1920 })
            {
                var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth);
                int nameBudget = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                    edges.RequiredRightEdge,
                    SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(0),
                    SummarySectionLayoutMath.CurrencyColumnGap,
                    SummarySectionLayoutMath.CurrencyNameX);

                Assert.True(nameBudget >= 200, $"name budget {nameBudget} at panel {panelWidth}");
            }
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_NarrowPanel_FallsBackToThePackedStack()
        {
            // Below the width a track can hold a reserved number band plus
            // its gap there is nothing to distribute, and spreading anyway
            // would overlap the columns. 300: right edge 292, marker 258,
            // Needed 244, and 196px of span cannot give four tracks 74px
            // each - so the columns pack right-to-left as they always did.
            //
            // The threshold is a function of CurrencyNameX, so the wallet-
            // LIST icon moving that from 34 to 48 raised it from a ~386px
            // panel to ~400px. Both are far under the module's own window
            // minimum (a 1436px window leaves a 1310px plan panel), so the
            // distributed regime is what actually ships and this branch
            // exists to degrade rather than overlap.
            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(300);

            Assert.Equal(258, edges.MarkerX);
            Assert.Equal(244, edges.NeededRightEdge);
            Assert.Equal(170, edges.HaveRightEdge);
            Assert.Equal(96, edges.RequiredRightEdge);
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_EveryRegime_KeepsColumnsOutOfEachOther()
        {
            // The invariant both regimes exist to hold: a value right-
            // aligned on its own edge grows LEFTWARD by up to the reserved
            // band width, and must never reach the column to its left. Swept
            // across the regime boundary and across a reserve wide enough
            // for a 7-digit Karma balance.
            foreach (int widest in new[] { 0, 60, 90, 140 })
            {
                int band = SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(widest);
                for (int panelWidth = 200; panelWidth <= 2000; panelWidth += 7)
                {
                    var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth, widest);

                    Assert.True(
                        edges.HaveRightEdge - band >= edges.RequiredRightEdge,
                        $"Have intrudes on Required at panel {panelWidth}, band {band}");
                    Assert.True(
                        edges.NeededRightEdge - band >= edges.HaveRightEdge,
                        $"Needed intrudes on Have at panel {panelWidth}, band {band}");
                    Assert.True(edges.NeededRightEdge < edges.MarkerX);
                }
            }
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_ColumnsOrderedLeftToRight_RequiredHaveNeededMarker()
        {
            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(800);

            Assert.True(edges.RequiredRightEdge < edges.HaveRightEdge);
            Assert.True(edges.HaveRightEdge < edges.NeededRightEdge);
            Assert.True(edges.NeededRightEdge < edges.MarkerX);
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_WiderPanel_ShiftsEdgesRight()
        {
            var narrow = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(600);
            var wide = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(900);

            Assert.True(wide.MarkerX > narrow.MarkerX);
            Assert.True(wide.RequiredRightEdge > narrow.RequiredRightEdge);
        }

        // --- The justified-width invariant (replaces the pull-in and
        // centring pair CurrencyHeaderBandWidth/CurrencyTableOffsetX built
        // on, both deleted with the machinery) ---
        [Fact]
        public void ComputeCurrencyColumnEdges_MarkerEndsAtThePinnedPanelEdge()
        {
            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(800);

            Assert.Equal(
                PlanRelayoutMath.PinnedRightEdge(800),
                edges.MarkerX + SummarySectionLayoutMath.CurrencyMarkerWidth);
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_WiderPanel_SharesTheIncreaseAcrossEveryTrack()
        {
            // The right-hand block used to move by the whole increase with
            // the name column absorbing all of it. Under distribution every
            // track takes an equal share, so a wider panel spreads the
            // columns rather than dragging them further from the name.
            // 400px of panel is 100px of track: a band centred on track i
            // moves by i whole tracks plus HALF of its own track's growth,
            // so 150, 250 and 350 - the marker alone still tracks the panel
            // edge by the full 400, because it is pinned to that edge
            // rather than centred on a track.
            var narrow = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(1200, 90);
            var wide = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(1600, 90);

            Assert.Equal(400, wide.MarkerX - narrow.MarkerX);
            Assert.Equal(350, wide.NeededRightEdge - narrow.NeededRightEdge);
            Assert.Equal(250, wide.HaveRightEdge - narrow.HaveRightEdge);
            Assert.Equal(150, wide.RequiredRightEdge - narrow.RequiredRightEdge);
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_NameBudgetTakesItsOwnShareOfTheIncrease()
        {
            const int nameX = SummarySectionLayoutMath.CurrencyNameX;

            int narrow = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                SummarySectionLayoutMath.ComputeCurrencyColumnEdges(1200).RequiredRightEdge,
                SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(0),
                SummarySectionLayoutMath.CurrencyColumnGap,
                nameX);
            int wide = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                SummarySectionLayoutMath.ComputeCurrencyColumnEdges(1600).RequiredRightEdge,
                SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(0),
                SummarySectionLayoutMath.CurrencyColumnGap,
                nameX);

            // The name's budget stops one gap before the Required BAND, and
            // that band is centred on track 1: it moves by the name track's
            // own 100px share plus half of its own track's growth. The rest
            // of the 400px increase goes to the two tracks right of it.
            Assert.Equal(150, wide - narrow);
        }

        // --- Regression: EffectiveCurrencyNumberColumnWidth / widened
        // ComputeCurrencyColumnEdges (a large unclamped Have value, e.g. a
        // 6-7 digit Karma balance, must not intrude into the Required
        // column - see the class doc comment above the currency-table
        // geometry region) ---
        [Fact]
        public void EffectiveCurrencyNumberColumnWidth_BelowFloor_ReturnsFixedFloor()
        {
            Assert.Equal(
                SummarySectionLayoutMath.CurrencyNumberColumnWidth,
                SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(10));
        }

        [Fact]
        public void EffectiveCurrencyNumberColumnWidth_Zero_ReturnsFixedFloor()
        {
            Assert.Equal(
                SummarySectionLayoutMath.CurrencyNumberColumnWidth,
                SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(0));
        }

        [Fact]
        public void EffectiveCurrencyNumberColumnWidth_AboveFloor_ReturnsMeasuredWidth()
        {
            // A plausible width for a 7-digit Karma balance rendered at
            // the body font - comfortably past the 60px fixed floor.
            Assert.Equal(90, SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(90));
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_WidestNumberWidth_DoesNotMoveADistributedTrack()
        {
            // Under distribution the reserve decides only whether the row
            // is wide enough to distribute at all - a track already holds
            // its band plus the gap, so a wider value grows SYMMETRICALLY
            // about its track's centre line and the column itself does not
            // move. That is the difference from the packed stack, where
            // every wider value shoved the columns to its left further left
            // again.
            const int panelWidth = 800;
            var fixedFloor = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth);
            var widened = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth, 120);
            int floorBand = SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(0);
            int widerBand = SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(120);

            Assert.Equal(fixedFloor.MarkerX, widened.MarkerX);
            Assert.Equal(
                fixedFloor.NeededRightEdge - (floorBand / 2),
                widened.NeededRightEdge - (widerBand / 2));
            Assert.Equal(
                fixedFloor.HaveRightEdge - (floorBand / 2),
                widened.HaveRightEdge - (widerBand / 2));
            Assert.Equal(
                fixedFloor.RequiredRightEdge - (floorBand / 2),
                widened.RequiredRightEdge - (widerBand / 2));

            // The band really did widen - the assertion above would also
            // hold if nothing had changed at all.
            Assert.True(widened.RequiredRightEdge > fixedFloor.RequiredRightEdge);
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_WidestNumberWidth_PacksTheStackWhenATrackCannotHoldIt()
        {
            // The reserve DOES decide the geometry at the boundary: a band
            // wide enough that four equal tracks can no longer each hold
            // one drops the row back to the packed stack, where the widened
            // bands push Have and Required left exactly as they always did.
            const int panelWidth = 500;
            var floor = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth);
            var widened = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth, 140);

            Assert.Equal(floor.MarkerX, widened.MarkerX);

            // Packed, the last column IS the table's right edge (marker
            // less one gap); distributed, it centres on the last track and
            // stops short of it. That gap is how the two regimes are told
            // apart from the outside.
            Assert.Equal(
                widened.MarkerX - SummarySectionLayoutMath.CurrencyColumnGap,
                widened.NeededRightEdge);
            Assert.True(floor.NeededRightEdge < widened.NeededRightEdge);

            Assert.Equal(widened.NeededRightEdge - 140 - SummarySectionLayoutMath.CurrencyColumnGap,
                widened.HaveRightEdge);
            Assert.Equal(widened.HaveRightEdge - 140 - SummarySectionLayoutMath.CurrencyColumnGap,
                widened.RequiredRightEdge);
        }
    }
}
