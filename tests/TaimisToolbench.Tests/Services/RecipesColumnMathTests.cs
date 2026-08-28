using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class RecipesColumnMathTests
    {
        private const int NameX = 50;

        [Fact]
        public void StatusPinsToThePanelEdge_AtEveryWidth()
        {
            // The whole point of the pinned model: the rightmost column's
            // right edge is a function of panel width alone. No pull-in,
            // no dependence on how wide the names happen to be.
            foreach (int panelWidth in new[] { 400, 1252, 3000 })
            {
                var edges = RecipesColumnMath.ComputeEdges(panelWidth, 90, 140, NameX);
                Assert.Equal(PlanRelayoutMath.PinnedRightEdge(panelWidth), edges.StatusRightEdge);
            }
        }

        [Fact]
        public void DisciplineSitsOneGapLeftOfTheStatusBand()
        {
            var edges = RecipesColumnMath.ComputeEdges(
                panelWidth: 1252, statusColumnWidth: 90, disciplineColumnWidth: 140, nameX: NameX);

            Assert.Equal(
                PlanRelayoutMath.PinnedRightEdge(1252) - 90 - RecipesColumnMath.ColumnGap - 140,
                edges.DisciplineX);
        }

        [Fact]
        public void NameBudgetStopsOneGapShortOfTheDisciplineColumn()
        {
            var edges = RecipesColumnMath.ComputeEdges(
                panelWidth: 1252, statusColumnWidth: 90, disciplineColumnWidth: 140, nameX: NameX);

            Assert.Equal(
                edges.DisciplineX - RecipesColumnMath.NameToDisciplineGap - NameX,
                edges.NameMaxWidth);
        }

        [Fact]
        public void WiderStatusBandCostsTheNameColumn_NotTheDisciplineColumn()
        {
            // "Auto-learned" is wider than "Learned". The extra width comes
            // out of the flexing name, and the gap between Discipline and
            // Status is unchanged - the two bands are sized independently
            // and both hang off the same pinned edge.
            var narrow = RecipesColumnMath.ComputeEdges(1252, 60, 140, NameX);
            var wide = RecipesColumnMath.ComputeEdges(1252, 110, 140, NameX);

            Assert.Equal(narrow.DisciplineX - 50, wide.DisciplineX);
            Assert.Equal(narrow.NameMaxWidth - 50, wide.NameMaxWidth);
            Assert.Equal(narrow.StatusRightEdge, wide.StatusRightEdge);
        }

        [Fact]
        public void NoDisciplineColumn_GivesItsWidthBackToTheName()
        {
            // A recipe list with no disciplines at all (mystic-forge only)
            // reserves nothing for the column, and the renderer draws no
            // header over it.
            var reserved = RecipesColumnMath.ComputeEdges(1252, 90, 140, NameX);
            var none = RecipesColumnMath.ComputeEdges(1252, 90, 0, NameX);

            Assert.Equal(reserved.NameMaxWidth + 140, none.NameMaxWidth);
        }

        [Fact]
        public void NarrowPanel_NameBudgetHoldsTheSharedFloor()
        {
            // PlanRelayoutMath.NameMaxWidthBeforeColumn's 20px floor is the
            // one thing standing between a very narrow panel and a
            // zero-or-negative ellipsis width.
            var edges = RecipesColumnMath.ComputeEdges(
                panelWidth: 200, statusColumnWidth: 90, disciplineColumnWidth: 140, nameX: NameX);

            Assert.Equal(20, edges.NameMaxWidth);
        }
    }
}
