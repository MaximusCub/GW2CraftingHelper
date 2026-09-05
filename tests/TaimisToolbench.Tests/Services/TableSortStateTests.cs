using System;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The click cycle behind the Crafting Plan's sortable column headers.
    /// <para>
    /// What each direction DRAWS is SortIndicatorLayoutTests' half; this one
    /// asserts only which direction a given column reports, since every
    /// sortable column carries a mark in all three states.
    /// </para>
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
            Assert.Equal(TableSortDirection.None, state.DirectionFor(PlanTableColumn.Item));
            Assert.Equal(TableSortDirection.None, state.DirectionFor(PlanTableColumn.Amount));
        }

        [Fact]
        public void FirstClick_SortsAscending()
        {
            var state = NewState();

            state.Cycle(PlanTableColumn.Item);

            Assert.Equal(TableSortDirection.Ascending, state.Direction);
            Assert.Equal(PlanTableColumn.Item, state.Column);
            Assert.True(state.IsActive(PlanTableColumn.Item));
            Assert.Equal(TableSortDirection.Ascending, state.DirectionFor(PlanTableColumn.Item));
        }

        [Fact]
        public void SecondClickSameColumn_SortsDescending()
        {
            var state = NewState();

            state.Cycle(PlanTableColumn.Item);
            state.Cycle(PlanTableColumn.Item);

            Assert.Equal(TableSortDirection.Descending, state.Direction);
            Assert.Equal(TableSortDirection.Descending, state.DirectionFor(PlanTableColumn.Item));
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
            Assert.Equal(TableSortDirection.None, state.DirectionFor(PlanTableColumn.Item));
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
            Assert.Equal(TableSortDirection.None, state.DirectionFor(PlanTableColumn.Item));
            Assert.Equal(TableSortDirection.Ascending, state.DirectionFor(PlanTableColumn.Amount));
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

        // Reset is what a NEW plan generation runs on both sortable tables
        // (CraftingPlanView.ResetPerPlanSortState, called at TriggerGenerate's
        // commit point beside the section-expansion reset). The cases below
        // pin the property that decision depends on: after it, the table is
        // indistinguishable from one that was never clicked - which is what
        // resetting to defaults on a new plan means for a reader looking
        // at the header row.
        [Fact]
        public void Reset_FromDescending_AlsoClears()
        {
            var state = NewState();

            state.Cycle(PlanTableColumn.Amount);
            state.Cycle(PlanTableColumn.Amount);
            Assert.Equal(TableSortDirection.Descending, state.Direction);

            state.Reset();

            Assert.Equal(TableSortDirection.None, state.Direction);
            Assert.Null(state.Column);
        }

        [Fact]
        public void Reset_LeavesEveryColumnUnsorted()
        {
            var state = NewState();
            state.Cycle(PlanTableColumn.Each);

            state.Reset();

            foreach (PlanTableColumn column in Enum.GetValues(typeof(PlanTableColumn)))
            {
                Assert.Equal(TableSortDirection.None, state.DirectionFor(column));
                Assert.False(state.IsActive(column));
            }
        }

        [Fact]
        public void Reset_ThenAClick_StartsTheCycleAtAscending()
        {
            // The user's next click on the fresh plan's header must behave
            // like a first click, not like the fourth click of a cycle the
            // previous plan left half-finished.
            var state = NewState();
            state.Cycle(PlanTableColumn.Item);
            state.Cycle(PlanTableColumn.Item);

            state.Reset();
            state.Cycle(PlanTableColumn.Item);

            Assert.Equal(TableSortDirection.Ascending, state.Direction);
            Assert.Equal(TableSortDirection.Ascending, state.DirectionFor(PlanTableColumn.Item));
        }

        [Fact]
        public void Reset_IsIdempotent()
        {
            var state = NewState();
            state.Cycle(PlanTableColumn.Total);

            state.Reset();
            state.Reset();

            Assert.Equal(TableSortDirection.None, state.Direction);
            Assert.Null(state.Column);
        }

        [Fact]
        public void OnlyTheActiveColumnReportsADirection()
        {
            var state = NewState();

            state.Cycle(PlanTableColumn.Each);

            Assert.Equal(TableSortDirection.Ascending, state.DirectionFor(PlanTableColumn.Each));
            Assert.Equal(TableSortDirection.None, state.DirectionFor(PlanTableColumn.Total));
            Assert.Equal(TableSortDirection.None, state.DirectionFor(PlanTableColumn.Item));
            Assert.Equal(TableSortDirection.None, state.DirectionFor(PlanTableColumn.Amount));
        }
    }
}
