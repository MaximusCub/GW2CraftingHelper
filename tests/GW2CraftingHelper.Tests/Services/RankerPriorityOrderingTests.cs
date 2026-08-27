using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class RankerPriorityOrderingTests
    {
        private static List<RankerWatchlistEntry> Entries(params int[] itemIds)
        {
            return itemIds.Select(id => new RankerWatchlistEntry { ItemId = id, Quantity = 1 }).ToList();
        }

        private static int[] Ids(IEnumerable<RankerWatchlistEntry> entries)
        {
            return entries.Select(e => e.ItemId).ToArray();
        }

        [Fact]
        public void MoveUp_AtZero_ReturnsNoInvalidationAndDoesNotMutate()
        {
            var entries = Entries(1, 2, 3);

            Assert.Equal(RankerPriorityOrdering.NoInvalidation, RankerPriorityOrdering.MoveUp(entries, 0));
            Assert.Equal(new[] { 1, 2, 3 }, Ids(entries));
        }

        [Fact]
        public void MoveDown_AtLast_ReturnsNoInvalidationAndDoesNotMutate()
        {
            var entries = Entries(1, 2, 3);

            Assert.Equal(RankerPriorityOrdering.NoInvalidation, RankerPriorityOrdering.MoveDown(entries, 2));
            Assert.Equal(new[] { 1, 2, 3 }, Ids(entries));
        }

        [Fact]
        public void MoveUp_SwapsExactlyTwoAdjacentEntries()
        {
            var entries = Entries(1, 2, 3, 4);

            Assert.Equal(1, RankerPriorityOrdering.MoveUp(entries, 2));
            Assert.Equal(new[] { 1, 3, 2, 4 }, Ids(entries));
        }

        [Fact]
        public void MoveDown_SwapsExactlyTwoAdjacentEntries()
        {
            var entries = Entries(1, 2, 3, 4);

            Assert.Equal(1, RankerPriorityOrdering.MoveDown(entries, 1));
            Assert.Equal(new[] { 1, 3, 2, 4 }, Ids(entries));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-99)]
        [InlineData(3)]
        [InlineData(500)]
        public void OutOfRangeIndices_ReturnNoInvalidationWithoutThrowing(int index)
        {
            var entries = Entries(1, 2, 3);

            Assert.Equal(RankerPriorityOrdering.NoInvalidation, RankerPriorityOrdering.MoveUp(entries, index));
            Assert.Equal(RankerPriorityOrdering.NoInvalidation, RankerPriorityOrdering.MoveDown(entries, index));
            Assert.Equal(new[] { 1, 2, 3 }, Ids(entries));
        }

        [Fact]
        public void EmptyAndSingleEntryLists_AreNoOps()
        {
            var empty = Entries();
            var single = Entries(7);

            Assert.Equal(RankerPriorityOrdering.NoInvalidation, RankerPriorityOrdering.MoveUp(empty, 0));
            Assert.Equal(RankerPriorityOrdering.NoInvalidation, RankerPriorityOrdering.MoveDown(empty, 0));
            Assert.Equal(RankerPriorityOrdering.NoInvalidation, RankerPriorityOrdering.MoveUp(single, 0));
            Assert.Equal(RankerPriorityOrdering.NoInvalidation, RankerPriorityOrdering.MoveDown(single, 0));
            Assert.Equal(new[] { 7 }, Ids(single));
        }

        [Fact]
        public void IndexOfItem_FindsTheFirstMatchAndReturnsMinusOneForAbsent()
        {
            var entries = Entries(11, 22, 33);

            Assert.Equal(0, RankerPriorityOrdering.IndexOfItem(entries, 11));
            Assert.Equal(2, RankerPriorityOrdering.IndexOfItem(entries, 33));
            Assert.Equal(-1, RankerPriorityOrdering.IndexOfItem(entries, 44));
            Assert.Equal(-1, RankerPriorityOrdering.IndexOfItem(null, 11));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public void CanMoveUpDown_AgreeWithMoveUpDownForEveryIndex(int count)
        {
            for (int index = -1; index <= count; index++)
            {
                var forUp = Entries(Enumerable.Range(1, count).ToArray());
                bool upMoved = RankerPriorityOrdering.MoveUp(forUp, index) != RankerPriorityOrdering.NoInvalidation;
                Assert.Equal(RankerPriorityOrdering.CanMoveUp(index, count), upMoved);

                var forDown = Entries(Enumerable.Range(1, count).ToArray());
                bool downMoved = RankerPriorityOrdering.MoveDown(forDown, index) != RankerPriorityOrdering.NoInvalidation;
                Assert.Equal(RankerPriorityOrdering.CanMoveDown(index, count), downMoved);
            }
        }

        // The invalidation index is what stops a moved row from displaying a
        // number computed for a slot it no longer occupies - see the cascade.
        [Fact]
        public void MoveUp_InvalidatesFromTheHigherOfTheTwoPositions()
        {
            var entries = Entries(1, 2, 3, 4, 5);

            Assert.Equal(2, RankerPriorityOrdering.MoveUp(entries, 3));
        }

        [Fact]
        public void MoveDown_InvalidatesFromTheMovedRowsOwnPosition()
        {
            var entries = Entries(1, 2, 3, 4, 5);

            Assert.Equal(3, RankerPriorityOrdering.MoveDown(entries, 3));
        }

        [Fact]
        public void RemoveAt_RemovesTheEntryAndInvalidatesFromThatIndex()
        {
            var entries = Entries(1, 2, 3, 4);

            Assert.Equal(1, RankerPriorityOrdering.RemoveAt(entries, 1));
            Assert.Equal(new[] { 1, 3, 4 }, Ids(entries));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        public void RemoveAt_OutOfRange_IsANoOp(int index)
        {
            var entries = Entries(1, 2, 3, 4);

            Assert.Equal(RankerPriorityOrdering.NoInvalidation, RankerPriorityOrdering.RemoveAt(entries, index));
            Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(entries));
        }

        // ---------------------------------------------------------------
        // Metrics currency across mode toggles and reorders
        // ---------------------------------------------------------------
        private static RankerRowMetrics Metrics(
            RankerMode mode, int priorityIndex,
            RankerReadinessKind kind = RankerReadinessKind.Measured, double readiness = 0.5)
        {
            return new RankerRowMetrics
            {
                Mode = mode,
                PriorityIndex = priorityIndex,
                Kind = kind,
                Readiness = readiness,
            };
        }

        [Fact]
        public void AModeToggleStalesMetricsFromTheOtherMode_BothDirections()
        {
            var cascade = Metrics(RankerMode.Cascade, 0);
            var independent = Metrics(RankerMode.Independent, 0);

            Assert.True(RankerPriorityOrdering.MetricsAreCurrent(cascade, 0, RankerMode.Cascade));
            Assert.False(RankerPriorityOrdering.MetricsAreCurrent(cascade, 0, RankerMode.Independent));
            Assert.True(RankerPriorityOrdering.MetricsAreCurrent(independent, 0, RankerMode.Independent));
            Assert.False(RankerPriorityOrdering.MetricsAreCurrent(independent, 0, RankerMode.Cascade));
        }

        [Fact]
        public void CascadeMetricsAreStaleAtAnyOtherPosition_IndependentOnesAreNot()
        {
            // A cascade number is a function of the slot it was computed
            // for; an independent number is position-free.
            Assert.False(RankerPriorityOrdering.MetricsAreCurrent(
                Metrics(RankerMode.Cascade, 1), 2, RankerMode.Cascade));
            Assert.True(RankerPriorityOrdering.MetricsAreCurrent(
                Metrics(RankerMode.Independent, 1), 2, RankerMode.Independent));
        }

        [Fact]
        public void NullMetricsAreNeverCurrent()
        {
            Assert.False(RankerPriorityOrdering.MetricsAreCurrent(null, 0, RankerMode.Cascade));
            Assert.False(RankerPriorityOrdering.MetricsAreCurrent(null, 0, RankerMode.Independent));
        }

        // ---------------------------------------------------------------
        // Independent display order
        // ---------------------------------------------------------------
        [Fact]
        public void IndependentDisplayOrder_PutsTheClosestToDoneFirst()
        {
            var entries = Entries(10, 20, 30);
            var byId = new Dictionary<int, RankerRowMetrics>
            {
                [10] = Metrics(RankerMode.Independent, 0, readiness: 0.20),
                [20] = Metrics(RankerMode.Independent, 1, readiness: 0.90),
                [30] = Metrics(RankerMode.Independent, 2, readiness: 0.55),
            };

            var order = RankerPriorityOrdering.IndependentDisplayOrder(
                entries, e => byId[e.ItemId]);

            Assert.Equal(new List<int> { 1, 2, 0 }, order);
            // The stored priority order itself is untouched.
            Assert.Equal(new[] { 10, 20, 30 }, Ids(entries));
        }

        [Fact]
        public void FinishedRowsSortAboveMeasured_UnmeasuredSortLast()
        {
            var entries = Entries(1, 2, 3, 4);
            var byId = new Dictionary<int, RankerRowMetrics>
            {
                [1] = null, // not yet calculated
                [2] = Metrics(RankerMode.Independent, 1, RankerReadinessKind.NothingLeft),
                [3] = Metrics(RankerMode.Independent, 2, readiness: 0.99),
                [4] = Metrics(RankerMode.Independent, 3, RankerReadinessKind.NotMeasurable),
            };

            var order = RankerPriorityOrdering.IndependentDisplayOrder(
                entries, e => byId[e.ItemId]);

            Assert.Equal(new List<int> { 1, 2, 3, 0 }, order);
        }

        [Fact]
        public void TiesKeepTheUsersPriorityOrder()
        {
            var entries = Entries(5, 6, 7);
            var order = RankerPriorityOrdering.IndependentDisplayOrder(
                entries, e => Metrics(RankerMode.Independent, 0, readiness: 0.5));

            Assert.Equal(new List<int> { 0, 1, 2 }, order);
        }

        [Fact]
        public void NullEntriesOrResolverDegradeToTheStoredOrder()
        {
            Assert.Empty(RankerPriorityOrdering.IndependentDisplayOrder(null, e => null));
            Assert.Equal(new List<int> { 0, 1 },
                RankerPriorityOrdering.IndependentDisplayOrder(Entries(1, 2), null));
        }
    }
}
