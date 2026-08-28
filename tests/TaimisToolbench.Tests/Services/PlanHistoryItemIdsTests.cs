using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// What the Plan History tab hands its background stat top-up. The rule
    /// that matters is the one the tooltip defect turns on: EVERY summary,
    /// not the first of each row, because every detail line draws its own
    /// icon with its own hover.
    /// </summary>
    public class PlanHistoryItemIdsTests
    {
        private static PlanHistoryEntry Entry(params int[] itemIds)
        {
            var summaries = new List<PlanHistoryItemSummary>();
            foreach (var id in itemIds)
            {
                summaries.Add(new PlanHistoryItemSummary { ItemId = id, Name = "Item " + id });
            }

            return new PlanHistoryEntry { ItemSummaries = summaries };
        }

        [Fact]
        public void EveryItemOfEveryEntryIsWarmed_NotJustTheRowIcon()
        {
            var ids = PlanHistoryItemIds.ForEntries(new List<PlanHistoryEntry>
            {
                Entry(19700, 19685, 46683),
                Entry(24289),
            });

            Assert.Equal(new[] { 19700, 19685, 46683, 24289 }, ids);
        }

        [Fact]
        public void AnItemUsedByTwoPlansIsAskedForOnce()
        {
            var ids = PlanHistoryItemIds.ForEntries(new List<PlanHistoryEntry>
            {
                Entry(19700, 24289),
                Entry(24289, 19700),
            });

            Assert.Equal(new[] { 19700, 24289 }, ids);
        }

        [Fact]
        public void SummariesWithNoCapturedIdAreSkipped()
        {
            // Written before summaries carried ids: the row still renders
            // from its own name and icon, and there is nothing to warm.
            var ids = PlanHistoryItemIds.ForEntries(new List<PlanHistoryEntry> { Entry(0, 19700, -1) });

            Assert.Equal(new[] { 19700 }, ids);
        }

        [Fact]
        public void NullsAnywhereYieldAnEmptyListRatherThanThrowing()
        {
            Assert.Empty(PlanHistoryItemIds.ForEntries(null));
            Assert.Empty(PlanHistoryItemIds.ForEntries(new List<PlanHistoryEntry>
            {
                null,
                new PlanHistoryEntry { ItemSummaries = null },
                new PlanHistoryEntry { ItemSummaries = new List<PlanHistoryItemSummary> { null } },
            }));
        }
    }
}
