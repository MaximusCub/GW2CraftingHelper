using System.Collections.Generic;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class ShoppingColumnMathTests
    {
        [Fact]
        public void TypicalValues_FallBackToFixedMinimums()
        {
            // Small coin values (well under the fixed minimums) -> edges
            // fall back to the same minimums as the old fixed-width
            // geometry, so ordinary short lists render exactly as before.
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge: 792, maxEachWidth: 40, maxTotalWidth: 60);

            Assert.Equal(792, edges.TotalRightEdge);
            Assert.Equal(792 - 150 - 20, edges.EachRightEdge);
            Assert.Equal(792 - 150 - 20 - 110 - 20, edges.QtyRightEdge);
        }

        [Fact]
        public void FourDigitGold_BothColumns_ExpandBeyondMinimums()
        {
            // Reproduces the reported bug: 4-digit-gold coin strings (e.g.
            // "1234g 56s 78c") measure wider than the fixed minimums in
            // both the Each and Total columns - this is the Mystic Coin
            // row overflow ("2502x 02 26") from the user's capture.
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge: 792, maxEachWidth: 180, maxTotalWidth: 220);

            Assert.Equal(792, edges.TotalRightEdge);
            Assert.Equal(792 - 220 - 20, edges.EachRightEdge);
            Assert.Equal(792 - 220 - 20 - 180 - 20, edges.QtyRightEdge);
        }

        [Fact]
        public void ZeroWidths_FallBackToMinimums()
        {
            // No row had a non-zero coin value in a column (e.g. an
            // all-currency shopping list) - the pre-scan yields 0 for that
            // column, and the fixed minimums keep it from collapsing to a
            // zero-width column.
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge: 792, maxEachWidth: 0, maxTotalWidth: 0);

            Assert.Equal(792 - 150 - 20, edges.EachRightEdge);
            Assert.Equal(792 - 150 - 20 - 110 - 20, edges.QtyRightEdge);
        }

        [Fact]
        public void OnlyOneColumnWide_OtherStaysAtMinimum()
        {
            // A list where only Total has wide values (e.g. large
            // quantities of a cheap item) must not widen Each too - the two
            // columns are sized independently.
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge: 792, maxEachWidth: 30, maxTotalWidth: 300);

            Assert.Equal(792 - 300 - 20, edges.EachRightEdge);
            Assert.Equal(792 - 300 - 20 - 110 - 20, edges.QtyRightEdge);
        }

        [Theory]
        [InlineData(792, 0, 0)]
        [InlineData(792, 180, 220)]
        [InlineData(400, 300, 300)]
        [InlineData(200, 0, 0)]
        public void OrderingInvariant_QtyLessThanEachLessThanTotal(
            int totalRightEdge, int maxEachWidth, int maxTotalWidth)
        {
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge, maxEachWidth, maxTotalWidth);

            Assert.True(edges.QtyRightEdge < edges.EachRightEdge);
            Assert.True(edges.EachRightEdge < edges.TotalRightEdge);
        }

        // --- Source column (the badge stopped trailing the name and
        // became an aligned column inside the pinned right-hand block) ---
        [Fact]
        public void SourceColumn_SitsOneGapAndOneAmountBandLeftOfTheAmountEdge()
        {
            var edges = ShoppingColumnMath.ComputeEdges(
                totalRightEdge: 792, maxEachWidth: 40, maxTotalWidth: 60,
                maxQtyWidth: 79, sourceColumnWidth: 96);

            Assert.Equal(
                edges.QtyRightEdge - 79 - ShoppingColumnMath.ColumnGap - 96,
                edges.SourceX);
        }

        [Fact]
        public void SourceColumn_LeftEdgeIsTheNameBudgetsStop_NotTheAmountEdge()
        {
            // The name used to budget against QtyRightEdge with its OWN
            // badge width subtracted, so no two rows' badges lined up. The
            // budget stops at one fixed x for the whole table now, and that
            // x is strictly left of the Amount column.
            var edges = ShoppingColumnMath.ComputeEdges(792, 40, 60, 79, 96);

            Assert.True(edges.SourceX < edges.QtyRightEdge);
        }

        [Fact]
        public void WiderBadge_MovesTheSourceColumnLeft_AndNothingElse()
        {
            // The badge column widens into the NAME's space, never into
            // Amount/Each/Total - every one of those hangs off the pinned
            // right edge and is unaffected.
            var narrow = ShoppingColumnMath.ComputeEdges(792, 40, 60, 79, 60);
            var wide = ShoppingColumnMath.ComputeEdges(792, 40, 60, 79, 100);

            Assert.Equal(narrow.SourceX - 40, wide.SourceX);
            Assert.Equal(narrow.QtyRightEdge, wide.QtyRightEdge);
            Assert.Equal(narrow.EachRightEdge, wide.EachRightEdge);
            Assert.Equal(narrow.TotalRightEdge, wide.TotalRightEdge);
        }

        [Fact]
        public void SourceColumn_MovesRightWithThePanel_ByItsOwnTracksShare()
        {
            // Both widths distribute (the module's own plan panel is past
            // the threshold at every supported window size), so the Source
            // column takes its share of the increase rather than the whole
            // of it - and the Total column still ends on the pinned edge.
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(
                panelWidth: 1252, maxEachWidth: 40, maxTotalWidth: 60,
                maxQtyWidth: 79, sourceColumnWidth: 96);
            var wider = ShoppingColumnMath.ComputeEdgesForPanel(1452, 40, 60, 79, 96);

            Assert.True(edges.Distributed);
            Assert.InRange(wider.SourceX - edges.SourceX, 1, 200);
            Assert.Equal(PlanRelayoutMath.PinnedRightEdge(1252), edges.TotalRightEdge);
            Assert.Equal(PlanRelayoutMath.PinnedRightEdge(1452), wider.TotalRightEdge);
        }

        // --- Header centring (a header sits over the band its own cells
        // occupy, not against the edge they share) ---
        [Fact]
        public void BandWidths_AreTheReservesTheColumnsActuallyUse()
        {
            var edges = ShoppingColumnMath.ComputeEdges(
                totalRightEdge: 792, maxEachWidth: 180, maxTotalWidth: 40,
                maxQtyWidth: 79, sourceColumnWidth: 96);

            // Measured where it beats the floor, floored where it does not.
            Assert.Equal(180, edges.EachBandWidth);
            Assert.Equal(150, edges.TotalBandWidth);
            Assert.Equal(79, edges.QtyBandWidth);
            Assert.Equal(96, edges.SourceBandWidth);

            // And each band ends exactly on its column's own edge, which is
            // what makes centring a header in it centre it over the cells.
            Assert.Equal(edges.QtyRightEdge - 79, edges.QtyBandX);
            Assert.Equal(edges.EachRightEdge - 180, edges.EachBandX);
            Assert.Equal(edges.TotalRightEdge - 150, edges.TotalBandX);
        }

        [Fact]
        public void HeaderX_CentresTheHeaderInItsBand()
        {
            // A 40px word in a 150px band starts 55px in - not at 110,
            // where right-aligning it to the band's edge would have put it.
            Assert.Equal(155, ShoppingColumnMath.HeaderX(100, 150, 40));

            // A header exactly as wide as its band fills it.
            Assert.Equal(100, ShoppingColumnMath.HeaderX(100, 150, 150));
        }

        [Fact]
        public void HeaderX_AndTheCellsUnderIt_ShareTheBandsCentreLine()
        {
            // The law: a header and its cells centre on one axis. The Total
            // column's values right-align inside their band, so the band's
            // centre is the axis the header has to meet them on.
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(
                panelWidth: 1000, maxEachWidth: 0, maxTotalWidth: 0,
                maxQtyWidth: 79, sourceColumnWidth: 96);
            const int headerWidth = 44;

            int headerX = ShoppingColumnMath.HeaderX(
                edges.TotalBandX, edges.TotalBandWidth, headerWidth);

            Assert.Equal(
                edges.TotalBandX + (edges.TotalBandWidth / 2),
                headerX + (headerWidth / 2));
        }

        // --- SegmentRunWidth (currency-segment width computation, KNOWN-ISSUES #16) ---
        [Fact]
        public void SegmentRunWidth_Null_ReturnsZero()
        {
            Assert.Equal(0, ShoppingColumnMath.SegmentRunWidth(null, 20, 2, 6));
        }

        [Fact]
        public void SegmentRunWidth_Empty_ReturnsZero()
        {
            Assert.Equal(0, ShoppingColumnMath.SegmentRunWidth(new List<int>(), 20, 2, 6));
        }

        [Fact]
        public void SegmentRunWidth_SingleSegment_NoTrailingGap()
        {
            // 30 (text) + 2 (label-icon gap) + 20 (icon) = 52, no trailing
            // segmentGap since there is only one segment.
            var width = ShoppingColumnMath.SegmentRunWidth(new List<int> { 30 }, 20, 2, 6);

            Assert.Equal(52, width);
        }

        [Fact]
        public void SegmentRunWidth_TwoSegments_IncludesGapBetweenNotAfter()
        {
            // Each segment is textWidth + 2 + 20; a single segmentGap (6)
            // separates them, none trails after the last one.
            var width = ShoppingColumnMath.SegmentRunWidth(new List<int> { 30, 15 }, 20, 2, 6);

            Assert.Equal((30 + 2 + 20) + 6 + (15 + 2 + 20), width);
        }

        [Fact]
        public void SegmentRunWidth_UsesCallerSuppliedConstants_NotHardcoded()
        {
            // Different icon/gap constants than CraftingPlanView's own
            // (20/2/6) must change the result - proves the arithmetic is
            // fully parameterized, not silently defaulting to a baked-in
            // set of pixel values.
            var width = ShoppingColumnMath.SegmentRunWidth(new List<int> { 10 }, iconSize: 100, labelIconGap: 5, segmentGap: 1);

            Assert.Equal(10 + 5 + 100, width);
        }

        // --- SegmentRunWidth(int[], ...) overload
        // (the per-frame resize hot path passes SegmentLayoutHandle.TextWidths,
        // a concrete int[], to a non-allocating overload rather than the
        // IReadOnlyList<int> one above; both must agree on every result) ---
        [Fact]
        public void SegmentRunWidthArrayOverload_Null_ReturnsZero()
        {
            Assert.Equal(0, ShoppingColumnMath.SegmentRunWidth((int[])null, 20, 2, 6));
        }

        [Fact]
        public void SegmentRunWidthArrayOverload_Empty_ReturnsZero()
        {
            Assert.Equal(0, ShoppingColumnMath.SegmentRunWidth(new int[0], 20, 2, 6));
        }

        [Fact]
        public void SegmentRunWidthArrayOverload_SingleSegment_NoTrailingGap()
        {
            var width = ShoppingColumnMath.SegmentRunWidth(new int[] { 30 }, 20, 2, 6);

            Assert.Equal(52, width);
        }

        [Fact]
        public void SegmentRunWidthArrayOverload_MatchesListOverload_ForSameInput()
        {
            // Both overloads implement the same formula; a resize-tick call
            // through the int[] overload must never drift from a
            // build-time call through the IReadOnlyList<int> overload for
            // the same segment widths.
            var widths = new int[] { 30, 15, 42 };

            int arrayResult = ShoppingColumnMath.SegmentRunWidth(widths, 20, 2, 6);
            int listResult = ShoppingColumnMath.SegmentRunWidth(new List<int>(widths), 20, 2, 6);

            Assert.Equal(listResult, arrayResult);
        }

        // --- ComputeEdgesForPanel (the justified-width invariant) ---
        [Fact]
        public void ComputeEdgesForPanel_AnchorsTheTotalColumnToThePinnedPanelEdge()
        {
            var fromEdge = ShoppingColumnMath.ComputeEdges(
                PlanRelayoutMath.PinnedRightEdge(1000), maxEachWidth: 0, maxTotalWidth: 0);
            var fromPanel = ShoppingColumnMath.ComputeEdgesForPanel(
                panelWidth: 1000, maxEachWidth: 0, maxTotalWidth: 0);

            Assert.Equal(fromEdge.TotalRightEdge, fromPanel.TotalRightEdge);
            Assert.Equal(fromEdge.EachRightEdge, fromPanel.EachRightEdge);
            Assert.Equal(fromEdge.QtyRightEdge, fromPanel.QtyRightEdge);
        }

        [Fact]
        public void ComputeEdgesForPanel_Packed_WiderPanel_MovesEveryColumnByTheFullIncrease()
        {
            // The packed stack hangs entirely off the pinned right edge, so
            // every column in it moves with that edge and the name column
            // absorbs the whole increase. Both widths are below the
            // distribution threshold.
            var narrow = ShoppingColumnMath.ComputeEdgesForPanel(800, 0, 0, 79, 96);
            var wide = ShoppingColumnMath.ComputeEdgesForPanel(1000, 0, 0, 79, 96);

            Assert.False(narrow.Distributed);
            Assert.False(wide.Distributed);
            Assert.Equal(200, wide.TotalRightEdge - narrow.TotalRightEdge);
            Assert.Equal(200, wide.EachRightEdge - narrow.EachRightEdge);
            Assert.Equal(200, wide.QtyRightEdge - narrow.QtyRightEdge);
            Assert.Equal(200, wide.SourceX - narrow.SourceX);
        }

        [Fact]
        public void ComputeEdgesForPanel_Distributed_SharesTheIncreaseAcrossTheTracks()
        {
            // Distributed, only Total still tracks the panel edge by the
            // whole increase - it is the pinned column. 400px of panel is
            // 66px of track (six of them), and a band centred on track i
            // takes i tracks plus half of its own track's growth: Each on
            // track 4 moves 300, Amount on track 3 by 234, and Source's
            // LEFT edge on track 2 by 167. Which is the point of the
            // change: the columns spread into the row instead of huddling
            // against its right-hand edge.
            var narrow = ShoppingColumnMath.ComputeEdgesForPanel(1400, 0, 0, 79, 96);
            var wide = ShoppingColumnMath.ComputeEdgesForPanel(1800, 0, 0, 79, 96);

            Assert.True(narrow.Distributed);
            Assert.True(wide.Distributed);
            Assert.Equal(400, wide.TotalRightEdge - narrow.TotalRightEdge);
            Assert.Equal(300, wide.EachRightEdge - narrow.EachRightEdge);
            Assert.Equal(234, wide.QtyRightEdge - narrow.QtyRightEdge);
            Assert.Equal(167, wide.SourceX - narrow.SourceX);
        }

        [Fact]
        public void ComputeEdgesForPanel_Distributed_PutsTheDataColumnsOneTrackApart()
        {
            // The law: six equal tracks from the name's left edge to the
            // pinned right edge, the name spanning two, then one each for
            // Source, Amount, Each and Total - and each of the first three
            // bands CENTRED on the track it owns. Doubled rather than
            // halved so a half-track stays an integer.
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(1400, 0, 0, 79, 96);
            int span = edges.TrackSpan;

            Assert.True(edges.Distributed);
            Assert.Equal(edges.TotalRightEdge - ShoppingColumnMath.NameX, span);

            int[] bandCentres = new[]
            {
                edges.SourceX + (edges.SourceBandWidth / 2),
                edges.QtyRightEdge - (edges.QtyBandWidth / 2),
                edges.EachRightEdge - (edges.EachBandWidth / 2),
            };
            for (int i = 0; i < bandCentres.Length; i++)
            {
                int track = ShoppingColumnMath.NameTrackSpan + i;
                Assert.InRange(
                    (2 * (bandCentres[i] - ShoppingColumnMath.NameX))
                        - (((2 * track) + 1) * span / ShoppingColumnMath.TrackCount),
                    -2, 2);
            }

            // Total is the exception the law names: it pins to the panel
            // edge, which is where the track grid ends anyway, so the table
            // still reaches its own right margin.
            Assert.Equal(PlanRelayoutMath.PinnedRightEdge(1400), edges.TotalRightEdge);
        }

        [Fact]
        public void ComputeEdgesForPanel_Distributed_MovesTheSourceColumnWellRightOfThePackedStack()
        {
            // The defect: "the item name is stranded far left with dead
            // space before the first data column". Distribution is only a
            // fix if the first data column actually moves left-to-right
            // into that space, so this compares the two regimes at one
            // width rather than trusting the arithmetic.
            const int panelWidth = 1400;
            var distributed = ShoppingColumnMath.ComputeEdgesForPanel(panelWidth, 0, 0, 79, 96);
            var packed = ShoppingColumnMath.ComputeEdges(
                PlanRelayoutMath.PinnedRightEdge(panelWidth), 0, 0, 79, 900);

            Assert.True(distributed.Distributed);
            Assert.False(packed.Distributed);
            Assert.True(
                distributed.SourceX < packed.QtyRightEdge - 300,
                "the Source column leaves the right-hand block entirely");
        }

        [Fact]
        public void ComputeEdgesForPanel_NarrowPanel_FallsBackToThePackedStack()
        {
            // Below the width a track can hold the widest band plus its gap
            // there is nothing to distribute, and spreading anyway would
            // overlap the columns. On a narrow panel a legible cramped
            // table beats an evenly spaced illegible one.
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(1000, 0, 0, 79, 96);

            Assert.False(edges.Distributed);
            Assert.Equal(0, edges.TrackSpan);
            Assert.Equal(edges.TotalRightEdge - 150 - ShoppingColumnMath.ColumnGap, edges.EachRightEdge);
            Assert.Equal(edges.EachRightEdge - 110 - ShoppingColumnMath.ColumnGap, edges.QtyRightEdge);
        }

        [Fact]
        public void ComputeEdges_AWideBandDropsTheTableBackToThePackedStack()
        {
            // The reserve decides the regime: a Total band wide enough that
            // six equal tracks can no longer each hold one packs the table
            // even at a width that would otherwise distribute.
            var distributed = ShoppingColumnMath.ComputeEdgesForPanel(1400, 0, 0, 79, 96);
            var packed = ShoppingColumnMath.ComputeEdgesForPanel(1400, 0, 900, 79, 96);

            Assert.True(distributed.Distributed);
            Assert.False(packed.Distributed);
            Assert.Equal(900, packed.TotalBandWidth);
        }

        [Fact]
        public void ComputeEdgesForPanel_NameBudgetStopsAtTheSourceColumnAndGrowsWithThePanel()
        {
            // The Item column flexes up to the Source column's left edge,
            // measured exactly as CreateShoppingRow budgets it
            // (NameToQtyGap 12, no trailing band of its own). Under
            // distribution it takes its two tracks' share of a wider panel
            // plus half of the Source track's - 167px of a 400px increase -
            // rather than all of it: the rest goes to the columns, which is
            // the trade the change makes.
            int narrow = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                ShoppingColumnMath.ComputeEdgesForPanel(1400, 0, 0, 79, 96).SourceX,
                0, 12, ShoppingColumnMath.NameX);
            int wide = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                ShoppingColumnMath.ComputeEdgesForPanel(1800, 0, 0, 79, 96).SourceX,
                0, 12, ShoppingColumnMath.NameX);

            Assert.Equal(167, wide - narrow);
            Assert.True(narrow > 200, $"name budget {narrow} at the module's own widths");
        }

        // Which column a click in the band sorts by. The failure pinned:
        // a boundary between the two WORDS puts the Source cell over the
        // right-hand end of the item NAMES.
        [Fact]
        public void HeaderCellBoundaries_Packed_SplitTheGapsBetweenTheColumns()
        {
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(
                panelWidth: 1000, maxEachWidth: 0, maxTotalWidth: 0,
                maxQtyWidth: 79, sourceColumnWidth: 90);
            Assert.False(edges.Distributed);

            var boundaries = new int[4];
            ShoppingColumnMath.HeaderCellBoundaries(edges, 12, boundaries);

            // Item ends just before the source badges begin...
            Assert.Equal(edges.SourceX - 6, boundaries[0]);

            // ...and every other is the middle of the columns' own gap.
            Assert.Equal(edges.SourceX + 90 + 10, boundaries[1]);

            // The same boundary from the other side.
            Assert.Equal((edges.QtyRightEdge - 79) - 10, boundaries[1]);
            Assert.Equal(edges.QtyRightEdge + 10, boundaries[2]);
            Assert.Equal(edges.EachRightEdge + 10, boundaries[3]);

            for (int i = 1; i < boundaries.Length; i++)
            {
                Assert.True(boundaries[i] > boundaries[i - 1], "boundaries run left to right");
            }
        }

        [Fact]
        public void HeaderCellBoundaries_Distributed_AreTheTracksThemselves()
        {
            // Distributed there is no gap to split: each cell is its
            // column's whole track, so the partition is the track grid and
            // the Item cell reaches the Source column's own track.
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(
                panelWidth: 1400, maxEachWidth: 0, maxTotalWidth: 0,
                maxQtyWidth: 79, sourceColumnWidth: 90);
            Assert.True(edges.Distributed);

            var boundaries = new int[4];
            ShoppingColumnMath.HeaderCellBoundaries(edges, 12, boundaries);

            for (int i = 0; i < boundaries.Length; i++)
            {
                int track = ShoppingColumnMath.NameTrackSpan + i;
                Assert.Equal(
                    ShoppingColumnMath.NameX
                        + (edges.TrackSpan * track / ShoppingColumnMath.TrackCount),
                    boundaries[i]);
            }

            // The Item cell still covers the whole name column: the name's
            // own budget stops before the boundary, not past it.
            int nameRightEdge = ShoppingColumnMath.NameX
                + PlanRelayoutMath.NameMaxWidthBeforeColumn(
                    edges.SourceX, 0, 12, ShoppingColumnMath.NameX);

            Assert.True(boundaries[0] < nameRightEdge);
            Assert.True(boundaries[0] < edges.SourceX);

            for (int i = 1; i < boundaries.Length; i++)
            {
                Assert.True(boundaries[i] > boundaries[i - 1], "boundaries run left to right");
            }
        }

        [Fact]
        public void HeaderCellBoundaries_IgnoreABufferItCannotFill()
        {
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(1000, 0, 0);

            ShoppingColumnMath.HeaderCellBoundaries(edges, 12, null);
            var tooShort = new int[2];
            ShoppingColumnMath.HeaderCellBoundaries(edges, 12, tooShort);

            Assert.Equal(new[] { 0, 0 }, tooShort);
        }

        [Fact]
        public void ComputeEdgesForPanel_VeryNarrowPanel_StillEndsOneMarginInFromTheEdge()
        {
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(
                panelWidth: 500, maxEachWidth: 0, maxTotalWidth: 0);

            Assert.Equal(500 - PlanRelayoutMath.TableRightMargin, edges.TotalRightEdge);
        }
    }
}
