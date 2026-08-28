using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    // The two answer sets and the rules for when each stops being true.
    // Exercised through the production cache, with production metrics and
    // production entries - the view holds one of these and asks it the same
    // questions these tests do.
    public class RankerResultCacheTests
    {
        private static List<RankerWatchlistEntry> List(params int[] itemIds)
        {
            return itemIds
                .Select(id => new RankerWatchlistEntry { ItemId = id, Quantity = 1, Name = "Item " + id })
                .ToList();
        }

        private static RankerRowMetrics Metrics(RankerMode mode, int priorityIndex)
        {
            return new RankerRowMetrics
            {
                Kind = RankerReadinessKind.Measured,
                Readiness = 0.5,
                Mode = mode,
                PriorityIndex = priorityIndex,
                HasSnapshot = true,
            };
        }

        private static CraftingPlanResult Solve()
        {
            return new CraftingPlanResult();
        }

        private static RankerResultCache Filled(RankerMode mode, IReadOnlyList<RankerWatchlistEntry> entries)
        {
            var cache = new RankerResultCache();
            for (int i = 0; i < entries.Count; i++)
            {
                cache.Store(mode, entries[i].ItemId, Metrics(mode, i), Solve());
            }

            return cache;
        }

        [Fact]
        public void EachModeKeepsItsOwnAnswers_SoAToggleBackCostsNothing()
        {
            var entries = List(1, 2, 3);
            var cache = Filled(RankerMode.Cascade, entries);

            Assert.True(cache.IsComplete(RankerMode.Cascade, entries));
            Assert.False(cache.IsComplete(RankerMode.Independent, entries));

            for (int i = 0; i < entries.Count; i++)
            {
                cache.Store(RankerMode.Independent, entries[i].ItemId,
                    Metrics(RankerMode.Independent, i), Solve());
            }

            // Both sets now stand at once - the point of holding two.
            Assert.True(cache.IsComplete(RankerMode.Cascade, entries));
            Assert.True(cache.IsComplete(RankerMode.Independent, entries));
        }

        [Fact]
        public void AReorder_StalesTheCascadeFromTheMoveDownAndNothingElse()
        {
            var entries = List(1, 2, 3, 4);
            var cache = Filled(RankerMode.Cascade, entries);
            for (int i = 0; i < entries.Count; i++)
            {
                cache.Store(RankerMode.Independent, entries[i].ItemId,
                    Metrics(RankerMode.Independent, i), Solve());
            }

            cache.InvalidateCascadeFrom(entries, 1);

            Assert.NotNull(cache.Metrics(RankerMode.Cascade, 1));
            Assert.Null(cache.Metrics(RankerMode.Cascade, 2));
            Assert.Null(cache.Metrics(RankerMode.Cascade, 3));
            Assert.Null(cache.Metrics(RankerMode.Cascade, 4));

            // Order is meaningless to the independent question, so its whole
            // set survives - that is the rule a toggle depends on.
            Assert.True(cache.IsComplete(RankerMode.Independent, entries));
            Assert.Equal(1, cache.FirstStaleIndex(RankerMode.Cascade, entries));
        }

        [Fact]
        public void AQuantityEdit_StalesThatRowInBothSets()
        {
            var entries = List(1, 2, 3);
            var cache = Filled(RankerMode.Cascade, entries);
            for (int i = 0; i < entries.Count; i++)
            {
                cache.Store(RankerMode.Independent, entries[i].ItemId,
                    Metrics(RankerMode.Independent, i), Solve());
            }

            cache.InvalidateItem(2);

            Assert.Null(cache.Metrics(RankerMode.Cascade, 2));
            Assert.Null(cache.Metrics(RankerMode.Independent, 2));
            Assert.NotNull(cache.Metrics(RankerMode.Independent, 1));
            Assert.NotNull(cache.Metrics(RankerMode.Independent, 3));
        }

        [Fact]
        public void ANewSnapshot_TakesBothSetsWithIt()
        {
            var entries = List(1, 2);
            var cache = Filled(RankerMode.Cascade, entries);
            cache.Store(RankerMode.Independent, 1, Metrics(RankerMode.Independent, 0), Solve());

            Assert.True(cache.HasAnyResults);
            cache.InvalidateEverything();

            Assert.False(cache.HasAnyResults);
            Assert.False(cache.IsComplete(RankerMode.Cascade, entries));
            Assert.False(cache.IsComplete(RankerMode.Independent, entries));
        }

        [Fact]
        public void ARefreshOfOneMode_LeavesTheOthersAnswersAlone()
        {
            var entries = List(1, 2);
            var cache = Filled(RankerMode.Cascade, entries);
            cache.Store(RankerMode.Independent, 1, Metrics(RankerMode.Independent, 0), Solve());
            cache.Store(RankerMode.Independent, 2, Metrics(RankerMode.Independent, 1), Solve());

            cache.InvalidateMode(RankerMode.Cascade);

            Assert.False(cache.IsComplete(RankerMode.Cascade, entries));
            Assert.True(cache.IsComplete(RankerMode.Independent, entries));
        }

        [Fact]
        public void ARemovedRow_IsDroppedFromBothSetsRatherThanKeptAlive()
        {
            var entries = List(1, 2, 3);
            var cache = Filled(RankerMode.Cascade, entries);
            cache.Store(RankerMode.Independent, 2, Metrics(RankerMode.Independent, 1), Solve());

            entries.RemoveAll(e => e.ItemId == 2);
            cache.KeepOnly(entries);

            Assert.Null(cache.Metrics(RankerMode.Cascade, 2));
            Assert.Null(cache.Metrics(RankerMode.Independent, 2));
            Assert.Null(cache.Owned(RankerMode.Cascade, 2));
        }

        [Fact]
        public void CascadeStaleness_IsAPrefixSoARunKnowsWhereToStartSolving()
        {
            var entries = List(1, 2, 3, 4);
            var cache = Filled(RankerMode.Cascade, entries);

            // The list re-orders: 3 moves to the top. Every row from the
            // move down is measured for a slot it no longer occupies.
            var moved = List(3, 1, 2, 4);

            Assert.Equal(0, cache.FirstStaleIndex(RankerMode.Cascade, moved));
        }

        [Fact]
        public void IndependentStaleness_IgnoresTheSlotARowSitsIn()
        {
            var entries = List(1, 2, 3);
            var cache = new RankerResultCache();
            for (int i = 0; i < entries.Count; i++)
            {
                cache.Store(RankerMode.Independent, entries[i].ItemId,
                    Metrics(RankerMode.Independent, i), Solve());
            }

            Assert.True(cache.IsComplete(RankerMode.Independent, List(3, 2, 1)));
        }

        [Fact]
        public void ARowWithoutTheSolveBehindIt_IsStaleUnderTheCascadeOnly()
        {
            var entries = List(1, 2);
            var cache = new RankerResultCache();
            cache.Store(RankerMode.Cascade, 1, Metrics(RankerMode.Cascade, 0), null);
            cache.Store(RankerMode.Cascade, 2, Metrics(RankerMode.Cascade, 1), Solve());
            cache.Store(RankerMode.Independent, 1, Metrics(RankerMode.Independent, 0), null);
            cache.Store(RankerMode.Independent, 2, Metrics(RankerMode.Independent, 1), null);

            // The cascade replays each row's claim from its solve, so a row
            // without one has to be solved again even though its numbers
            // still read true. Independent rows claim nothing.
            Assert.Equal(0, cache.FirstStaleIndex(RankerMode.Cascade, entries));
            Assert.True(cache.IsComplete(RankerMode.Independent, entries));
        }

        [Fact]
        public void AnEmptyListIsComplete_AndNullsAreNotFailures()
        {
            var cache = new RankerResultCache();

            Assert.True(cache.IsComplete(RankerMode.Cascade, new List<RankerWatchlistEntry>()));
            Assert.True(cache.IsComplete(RankerMode.Independent, null));
            Assert.Equal(-1, cache.FirstStaleIndex(RankerMode.Cascade, null));

            cache.InvalidateCascadeFrom(null, 0);
            cache.KeepOnly(null);
            Assert.False(cache.HasAnyResults);
        }
    }
}
