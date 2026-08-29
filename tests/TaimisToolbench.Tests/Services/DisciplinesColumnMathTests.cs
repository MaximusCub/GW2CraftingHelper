using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The real column arithmetic the Required Disciplines table renders
    /// from. Band widths arrive already floored at their own header labels,
    /// exactly as DisciplinesSectionRenderer.Render floors them.
    /// </summary>
    public class DisciplinesColumnMathTests
    {
        [Fact]
        public void ComputeEdges_LevelPinsToThePanelEdge()
        {
            var edges = DisciplinesColumnMath.ComputeEdges(1200, 90, 200, 44);

            Assert.Equal(PlanRelayoutMath.PinnedRightEdge(1200), edges.LevelRightEdge);
        }

        [Fact]
        public void ComputeEdges_Distributed_PutsTheCharacterRunOnItsOwnTrack()
        {
            // The defect: Discipline and Characters packed against the row's
            // left edge with Level alone at the far right and the whole
            // middle of the row empty. The character run starts on the
            // second of three equal tracks now.
            var edges = DisciplinesColumnMath.ComputeEdges(1200, 90, 200, 44);

            Assert.True(edges.Distributed);
            Assert.Equal(
                JustifiedColumnTracks.LeftEdge(
                    DisciplinesColumnMath.NameX,
                    edges.LevelRightEdge - DisciplinesColumnMath.NameX,
                    DisciplinesColumnMath.TrackCount,
                    1),
                edges.CharX);
            Assert.Equal(402, edges.CharX);
        }

        [Fact]
        public void ComputeEdges_Distributed_CharacterBudgetStopsAtTheLevelBand()
        {
            var edges = DisciplinesColumnMath.ComputeEdges(1200, 90, 200, 44);

            Assert.Equal(
                edges.LevelRightEdge - 44 - DisciplinesColumnMath.ColumnGap - edges.CharX,
                edges.CharBandWidth);
            Assert.Equal(726, edges.CharBandWidth);
        }

        [Fact]
        public void ComputeEdges_ACharacterRunTooWideForATrack_PacksInsteadOfCrampingIt()
        {
            // A player with a dozen characters in one discipline: the run is
            // wider than an equal track, and packing gives it the whole row
            // between the discipline names and the Level band, which is the
            // most room it can have.
            var wide = DisciplinesColumnMath.ComputeEdges(1200, 90, 900, 44);
            var ordinary = DisciplinesColumnMath.ComputeEdges(1200, 90, 200, 44);

            Assert.False(wide.Distributed);
            Assert.Equal(8 + 90 + DisciplinesColumnMath.ColumnGap, wide.CharX);
            Assert.True(wide.CharBandWidth > ordinary.CharBandWidth);
        }

        [Fact]
        public void ComputeEdges_NarrowPanel_PacksAgainstTheDisciplineNames()
        {
            var edges = DisciplinesColumnMath.ComputeEdges(400, 90, 200, 44);

            Assert.False(edges.Distributed);
            Assert.Equal(8 + 90 + DisciplinesColumnMath.ColumnGap, edges.CharX);
        }

        [Theory]
        [InlineData(150)]
        [InlineData(400)]
        [InlineData(1200)]
        [InlineData(2400)]
        public void ComputeEdges_TheCharacterRunNeverStartsInsideTheDisciplineColumn(int panelWidth)
        {
            // charX is one column X for the whole section, so it has to
            // clear the WIDEST discipline name in it at every width - a row
            // whose own name is short still must not have the run land on
            // top of a longer name two rows down.
            var edges = DisciplinesColumnMath.ComputeEdges(panelWidth, 90, 200, 44);

            Assert.True(edges.CharX >= DisciplinesColumnMath.NameX + 90 + DisciplinesColumnMath.ColumnGap);
        }

        [Fact]
        public void ComputeEdges_APanelTooNarrowForAnyOfIt_ReportsNoBudgetRatherThanANegativeOne()
        {
            // EllipsizeToWidth is the consumer; a negative budget there is
            // not a narrower string, it is an argument nothing checks.
            var edges = DisciplinesColumnMath.ComputeEdges(150, 90, 200, 44);

            Assert.Equal(0, edges.CharBandWidth);
        }

        [Fact]
        public void ComputeEdges_ASectionWithNoCharacterTextAtAll_StillReservesTheLevelBand()
        {
            // The Level column is reserved whether or not any row carries a
            // level, and a zero-width Characters column must not be allowed
            // to swallow it.
            var edges = DisciplinesColumnMath.ComputeEdges(1200, 90, 0, 44);

            Assert.Equal(PlanRelayoutMath.PinnedRightEdge(1200), edges.LevelRightEdge);
            Assert.Equal(
                edges.LevelRightEdge - 44 - DisciplinesColumnMath.ColumnGap - edges.CharX,
                edges.CharBandWidth);
        }

        [Fact]
        public void ComputeEdges_AWiderPanel_SpreadsTheColumnsRatherThanOnlyMovingLevel()
        {
            // Packed, every pixel of a wider panel went to the dead space
            // between the character run and the Level column. Distributed,
            // the run takes a third of it.
            var narrow = DisciplinesColumnMath.ComputeEdges(1200, 90, 200, 44);
            var wide = DisciplinesColumnMath.ComputeEdges(1500, 90, 200, 44);

            Assert.True(narrow.Distributed);
            Assert.True(wide.Distributed);
            Assert.Equal(300, wide.LevelRightEdge - narrow.LevelRightEdge);
            Assert.Equal(100, wide.CharX - narrow.CharX);
        }
    }
}
