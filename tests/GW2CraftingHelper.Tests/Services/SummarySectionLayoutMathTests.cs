using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
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
                Row(PlanRowType.SummaryFootnote)
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
                Row(PlanRowType.SummaryFootnote)
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
                Row(PlanRowType.SummaryFootnote)
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
                Row(PlanRowType.SummaryFootnote)
            };

            int expected = SummarySectionLayoutMath.CostBandHeight(true)
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
                Row(PlanRowType.SummaryFootnote)
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
                Row(PlanRowType.SummaryFootnote)
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
                Row(PlanRowType.SummaryFootnote)
            };

            int expected = SummarySectionLayoutMath.CostBandHeight(true)
                + PlanContentHeightMath.CostTileRowHeight
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
            // 6 margin + 6 pad + 32 caption line + 4 gap + 20 coin run
            // + 6 pad + 6 margin. The caption line is 32, not the 25 it was
            // at Caption: the tile captions moved to the ColumnHeader tier,
            // whose measured line height is 25 rather than 18.
            Assert.Equal(80, SummarySectionLayoutMath.CostBandHeight(false));
        }

        [Fact]
        public void CostBandHeight_WithCurrencyNote_AddsExactlyOneNoteLine()
        {
            Assert.Equal(80 + 23, SummarySectionLayoutMath.CostBandHeight(true));
            Assert.Equal(
                SummarySectionLayoutMath.CostBandHeight(false) + SummarySectionLayoutMath.CostBandCurrencyNoteHeight,
                SummarySectionLayoutMath.CostBandHeight(true));
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

            // The disclosure line under it stays Caption, and its own
            // reserve has to cover that tier rather than the caption's.
            Assert.True(
                TypeRampMetrics.CaptionInk.LineHeight
                    <= SummarySectionLayoutMath.CostBandCurrencyNoteHeight);
        }

        [Fact]
        public void CostBandHeight_IsTallerThanTheProfitBand_ByTheHighlightBoxsOwnRoom()
        {
            // The whole reason the cost band is still the taller of the
            // two: its result tile is boxed and the box needs its own
            // margin and padding, top and bottom. Nothing about the
            // amount font differs between the bands any more.
            Assert.True(SummarySectionLayoutMath.CostBandHeight(false) > PlanContentHeightMath.CostTileRowHeight);
            Assert.Equal(
                2 * (SummarySectionLayoutMath.CostBandBoxMarginY + SummarySectionLayoutMath.CostBandBoxPadY),
                SummarySectionLayoutMath.CostBandHeight(false)
                    - (SummarySectionLayoutMath.CostBandCaptionLineHeight
                        + SummarySectionLayoutMath.CostBandCaptionToAmountGap
                        + CoinSegmentMath.CoinIconSize));
        }

        [Fact]
        public void CostBandHeight_LeavesTheHighlightBoxInsideTheBand()
        {
            // The geometry the renderer actually builds, through the same
            // functions it calls: BandAmountY places the amount, the box
            // spans CostBandBoxTop to one pad below it, and that bottom
            // edge - the band's lowest ink - must sit inside the height the
            // math reserved. The renderer's DEBUG assert fails loud
            // otherwise, but only at runtime and only in DEBUG.
            //
            // captionBlockBottom is a MEASURED input (the caption font's
            // real metrics), so it is swept from the tightest plausible
            // value up to the full reserve rather than assumed: the clamp
            // inside BandAmountY is exactly what has to hold across it.
            foreach (bool hasNote in new[] { false, true })
            {
                int rowHeight = SummarySectionLayoutMath.CostBandHeight(hasNote);
                int reserve = SummarySectionLayoutMath.CostBandCaptionY
                    + SummarySectionLayoutMath.CostBandCaptionLineHeight
                    + (hasNote ? SummarySectionLayoutMath.CostBandCurrencyNoteHeight : 0);

                Assert.True(SummarySectionLayoutMath.CostBandBoxTop >= 0);

                for (int captionBlockBottom = SummarySectionLayoutMath.CostBandCaptionY;
                    captionBlockBottom <= reserve;
                    captionBlockBottom++)
                {
                    int amountY = SummarySectionLayoutMath.BandAmountY(
                        rowHeight,
                        CoinSegmentMath.CoinIconSize,
                        captionBlockBottom,
                        SummarySectionLayoutMath.CostBandAmountBottomPad);
                    int boxHeight = SummarySectionLayoutMath.CostBandBoxHeight(
                        amountY, CoinSegmentMath.CoinIconSize);

                    // The caption block must clear the amount run rather
                    // than be overprinted by it.
                    Assert.True(amountY >= captionBlockBottom);
                    Assert.True(SummarySectionLayoutMath.CostBandBoxTop + boxHeight <= rowHeight);
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
            // line ("+ N currencies required") at the caption font, which
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
                Row(PlanRowType.SummaryFootnote)
            };
            var withCurrency = new List<PlanRowViewModel>
            {
                Row(PlanRowType.CostFormulaTile),
                Row(PlanRowType.CurrencyCost),
                Row(PlanRowType.SummaryFootnote)
            };

            int currencyTableHeight =
                PlanContentHeightMath.ColumnHeaderRowHeight + PlanContentHeightMath.CurrencyRowHeight;

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
            Assert.Equal("+ 1 currency required",
                SummarySectionLayoutMath.CurrencyRequirementNote(1));
        }

        [Fact]
        public void CurrencyRequirementNote_ManyCurrencies_StatesTheCount()
        {
            Assert.Equal("+ 3 currencies required",
                SummarySectionLayoutMath.CurrencyRequirementNote(3));
        }

        [Fact]
        public void CurrencyRequirementNoteTooltip_ListsEveryCurrencyName()
        {
            var rows = new List<PlanRowViewModel>
            {
                new PlanRowViewModel { RowType = PlanRowType.CurrencyCost, Label = "Blue Prophet Shard" },
                new PlanRowViewModel { RowType = PlanRowType.CurrencyCost, Label = "Fractal Relic" },
                new PlanRowViewModel { RowType = PlanRowType.CurrencyCost, Label = "Spirit Shard" }
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
        // CurrencyMarkerWidth/CurrencyColumnGap/CurrencyNumberColumnWidth
        // constants (widestNumberWidth defaults to 0) and update the
        // literals here.
        [Fact]
        public void ComputeCurrencyColumnEdges_DerivesRightToLeftFromPanelWidth()
        {
            var edges800 = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(800);
            Assert.Equal(596, edges800.RequiredRightEdge);
            Assert.Equal(670, edges800.HaveRightEdge);
            Assert.Equal(744, edges800.NeededRightEdge);
            Assert.Equal(758, edges800.MarkerX);

            var edges1200 = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(1200);
            Assert.Equal(996, edges1200.RequiredRightEdge);
            Assert.Equal(1070, edges1200.HaveRightEdge);
            Assert.Equal(1144, edges1200.NeededRightEdge);
            Assert.Equal(1158, edges1200.MarkerX);
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
        public void ComputeCurrencyColumnEdges_WiderPanel_MovesTheWholeBlockByTheFullIncrease()
        {
            var narrow = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(1200, 90);
            var wide = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(1600, 90);

            Assert.Equal(400, wide.MarkerX - narrow.MarkerX);
            Assert.Equal(400, wide.NeededRightEdge - narrow.NeededRightEdge);
            Assert.Equal(400, wide.HaveRightEdge - narrow.HaveRightEdge);
            Assert.Equal(400, wide.RequiredRightEdge - narrow.RequiredRightEdge);
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_NameBudgetAbsorbsTheWidthIncrease()
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

            Assert.Equal(400, wide - narrow);
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
        public void ComputeCurrencyColumnEdges_NoWidestNumberWidthArg_ProducesFixedFloorGeometry()
        {
            // Omitting widestNumberWidth must reproduce the fixed-60px
            // geometry, pinned absolutely: rightEdge = 800 - 8 = 792,
            // MarkerX = 792 - 34, NeededRightEdge = MarkerX - gap(14),
            // then each further column steps left by gap(14) + width(60).
            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(800);

            Assert.Equal(758, edges.MarkerX);
            Assert.Equal(744, edges.NeededRightEdge);
            Assert.Equal(670, edges.HaveRightEdge);
            Assert.Equal(596, edges.RequiredRightEdge);
        }

        [Fact]
        public void ComputeCurrencyColumnEdges_WidestNumberWidthExceedsFloor_WidensRequiredAndHaveColumns()
        {
            const int panelWidth = 800;
            var fixedFloor = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth);
            var widened = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(panelWidth, 120);

            // Right-to-left layout: NeededRightEdge/MarkerX sit to the
            // right of the widened Have/Required bands, so they are
            // unaffected. Widening those bands pushes HaveRightEdge and
            // RequiredRightEdge further LEFT (smaller x) to make room for
            // the wider reserved space to their own right.
            Assert.Equal(fixedFloor.NeededRightEdge, widened.NeededRightEdge);
            Assert.Equal(fixedFloor.MarkerX, widened.MarkerX);
            Assert.True(widened.HaveRightEdge < fixedFloor.HaveRightEdge);
            Assert.True(widened.RequiredRightEdge < fixedFloor.RequiredRightEdge);

            // Exact widening amount: both columns grow by (120 - 60).
            int extra = 120 - SummarySectionLayoutMath.CurrencyNumberColumnWidth;
            Assert.Equal(fixedFloor.HaveRightEdge - extra, widened.HaveRightEdge);
            Assert.Equal(fixedFloor.RequiredRightEdge - 2 * extra, widened.RequiredRightEdge);
        }
    }
}
