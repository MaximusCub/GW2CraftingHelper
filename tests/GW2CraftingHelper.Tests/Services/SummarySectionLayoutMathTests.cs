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
                + PlanContentHeightMath.CTableHeaderRowHeight + 3 * PlanContentHeightMath.CurrencyRowHeight
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
                + PlanContentHeightMath.CTableHeaderRowHeight + 2 * PlanContentHeightMath.CurrencyRowHeight
                + 2 * PlanContentHeightMath.FallbackTextRowHeight;
            Assert.Equal(expected, SummarySectionLayoutMath.BodyHeight(rows));
        }

        // --- CostBandHeight + the currency disclosure line ---
        //
        // The cost band's result tile is the plan's headline figure and
        // renders at a promoted amount font, so the band is taller than
        // the profit band beside it; when the plan also has currency costs
        // the band carries a disclosure line and grows again. Both numbers
        // are pinned absolutely here (not recomputed from the same
        // constants the production formula reads, which could never fail):
        // a deliberate geometry change re-baselines these literals.

        [Fact]
        public void CostBandHeight_NoCurrencyNote_IsThePromotedTileRowHeight()
        {
            Assert.Equal(76, SummarySectionLayoutMath.CostBandHeight(false));
        }

        [Fact]
        public void CostBandHeight_WithCurrencyNote_AddsExactlyOneNoteLine()
        {
            Assert.Equal(76 + 18, SummarySectionLayoutMath.CostBandHeight(true));
            Assert.Equal(
                SummarySectionLayoutMath.CostBandHeight(false) + SummarySectionLayoutMath.CostBandCurrencyNoteHeight,
                SummarySectionLayoutMath.CostBandHeight(true));
        }

        [Fact]
        public void CostBandHeight_IsTallerThanTheUnpromotedProfitBand()
        {
            Assert.True(SummarySectionLayoutMath.CostBandHeight(false) > PlanContentHeightMath.CostTileRowHeight);
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
                PlanContentHeightMath.CTableHeaderRowHeight + PlanContentHeightMath.CurrencyRowHeight;

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

        // --- CurrencyBlockWidth / ComputeCurrencyColumnEdgesForPanel
        // (audit batch H: the numbers sat against the panel edge however
        // short the currency names were) ---

        [Fact]
        public void CurrencyBlockWidth_IsTheSpanTheEdgesActuallyLayOut()
        {
            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(800);
            int numberColumnWidth = SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(0);

            Assert.Equal(
                (800 - 8) - (edges.RequiredRightEdge - numberColumnWidth),
                SummarySectionLayoutMath.CurrencyBlockWidth(0));
        }

        [Fact]
        public void ComputeCurrencyColumnEdgesForPanel_LongNames_MatchesThePinnedEdges()
        {
            var pinned = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(800);
            var pulled = SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel(
                panelWidth: 800, widestNumberWidth: 0, widestNameEnd: 780);

            Assert.Equal(pinned.RequiredRightEdge, pulled.RequiredRightEdge);
            Assert.Equal(pinned.MarkerX, pulled.MarkerX);
        }

        [Fact]
        public void ComputeCurrencyColumnEdgesForPanel_ShortNames_CapsRequiredRelativeToTheNameColumn()
        {
            // "Spirit Shard" in a wide window: the Required column now
            // starts one breathing room past the name column instead of
            // 300+px away against the panel edge.
            const int widestNameEnd = 34 + 120;
            var pulled = SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel(
                panelWidth: 1200, widestNumberWidth: 0, widestNameEnd: widestNameEnd);

            int requiredLeft = pulled.RequiredRightEdge
                - SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(0);

            Assert.Equal(
                System.Math.Max(
                    widestNameEnd + PlanRelayoutMath.TableGutterBreathingRoom,
                    PlanRelayoutMath.TableRightBlockMinX),
                requiredLeft);
            Assert.True(pulled.MarkerX < 1200 - 8);
        }

        [Fact]
        public void ComputeCurrencyColumnEdgesForPanel_PulledIn_KeepsTheColumnsRelativeGeometry()
        {
            var pinned = SummarySectionLayoutMath.ComputeCurrencyColumnEdges(1200, 90);
            var pulled = SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel(
                panelWidth: 1200, widestNumberWidth: 90, widestNameEnd: 200);

            int shift = pinned.MarkerX - pulled.MarkerX;
            Assert.True(shift > 0);
            Assert.Equal(shift, pinned.NeededRightEdge - pulled.NeededRightEdge);
            Assert.Equal(shift, pinned.HaveRightEdge - pulled.HaveRightEdge);
            Assert.Equal(shift, pinned.RequiredRightEdge - pulled.RequiredRightEdge);
        }

        [Fact]
        public void ComputeCurrencyColumnEdgesForPanel_PulledIn_LeavesTheWidestNameItsFullWidth()
        {
            const int nameX = SummarySectionLayoutMath.CurrencyNameX;
            const int nameWidth = 160;

            var edges = SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel(
                panelWidth: 1200, widestNumberWidth: 0, widestNameEnd: nameX + nameWidth);

            int budget = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                edges.RequiredRightEdge,
                SummarySectionLayoutMath.EffectiveCurrencyNumberColumnWidth(0),
                SummarySectionLayoutMath.CurrencyColumnGap,
                nameX);

            Assert.True(budget >= nameWidth);
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
            // DefaultFont14 - comfortably past the 60px fixed floor.
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
