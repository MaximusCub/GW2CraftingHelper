using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The click cycle behind the Crafting Plan's sortable column headers.
    /// </summary>
    public class TableSortStateTests
    {
        private static TableSortState<PlanTableColumn> NewState()
        {
            return new TableSortState<PlanTableColumn>();
        }

        [Fact]
        public void FreshState_IsUnsorted_AndShowsNoIndicator()
        {
            var state = NewState();

            Assert.Equal(TableSortDirection.None, state.Direction);
            Assert.Null(state.Column);
            Assert.False(state.IsActive(PlanTableColumn.Item));
            Assert.Equal(string.Empty, state.IndicatorFor(PlanTableColumn.Item));
            Assert.Equal(string.Empty, state.IndicatorFor(PlanTableColumn.Amount));
        }

        [Fact]
        public void FirstClick_SortsAscending()
        {
            var state = NewState();

            state.Cycle(PlanTableColumn.Item);

            Assert.Equal(TableSortDirection.Ascending, state.Direction);
            Assert.Equal(PlanTableColumn.Item, state.Column);
            Assert.True(state.IsActive(PlanTableColumn.Item));
            Assert.Equal("^", state.IndicatorFor(PlanTableColumn.Item));
        }

        [Fact]
        public void SecondClickSameColumn_SortsDescending()
        {
            var state = NewState();

            state.Cycle(PlanTableColumn.Item);
            state.Cycle(PlanTableColumn.Item);

            Assert.Equal(TableSortDirection.Descending, state.Direction);
            Assert.Equal("v", state.IndicatorFor(PlanTableColumn.Item));
        }

        [Fact]
        public void ThirdClickSameColumn_RestoresDefaultOrder()
        {
            var state = NewState();

            state.Cycle(PlanTableColumn.Item);
            state.Cycle(PlanTableColumn.Item);
            state.Cycle(PlanTableColumn.Item);

            Assert.Equal(TableSortDirection.None, state.Direction);
            Assert.Null(state.Column);
            Assert.Equal(string.Empty, state.IndicatorFor(PlanTableColumn.Item));
        }

        [Fact]
        public void FourthClickSameColumn_StartsTheCycleOver()
        {
            var state = NewState();

            state.Cycle(PlanTableColumn.Amount);
            state.Cycle(PlanTableColumn.Amount);
            state.Cycle(PlanTableColumn.Amount);
            state.Cycle(PlanTableColumn.Amount);

            Assert.Equal(TableSortDirection.Ascending, state.Direction);
            Assert.Equal(PlanTableColumn.Amount, state.Column);
        }

        [Fact]
        public void ClickingAnotherColumn_StartsThatColumnAscending_AndClearsThePrevious()
        {
            var state = NewState();

            state.Cycle(PlanTableColumn.Item);
            state.Cycle(PlanTableColumn.Item);
            state.Cycle(PlanTableColumn.Amount);

            Assert.Equal(PlanTableColumn.Amount, state.Column);
            Assert.Equal(TableSortDirection.Ascending, state.Direction);
            Assert.False(state.IsActive(PlanTableColumn.Item));
            Assert.Equal(string.Empty, state.IndicatorFor(PlanTableColumn.Item));
            Assert.Equal("^", state.IndicatorFor(PlanTableColumn.Amount));
        }

        [Fact]
        public void Reset_ClearsColumnAndDirection()
        {
            var state = NewState();

            state.Cycle(PlanTableColumn.Total);
            state.Reset();

            Assert.Equal(TableSortDirection.None, state.Direction);
            Assert.Null(state.Column);
            Assert.False(state.IsActive(PlanTableColumn.Total));
        }

        [Fact]
        public void OnlyTheActiveColumnCarriesAnIndicator()
        {
            var state = NewState();

            state.Cycle(PlanTableColumn.Each);

            Assert.Equal("^", state.IndicatorFor(PlanTableColumn.Each));
            Assert.Equal(string.Empty, state.IndicatorFor(PlanTableColumn.Total));
            Assert.Equal(string.Empty, state.IndicatorFor(PlanTableColumn.Item));
            Assert.Equal(string.Empty, state.IndicatorFor(PlanTableColumn.Amount));
        }
    }
}
