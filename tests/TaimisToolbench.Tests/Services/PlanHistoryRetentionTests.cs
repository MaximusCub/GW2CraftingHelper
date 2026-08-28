using System;
using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class PlanHistoryRetentionTests
    {
        private static PlanHistoryEntry Entry(string id, int ageDays, bool pinned = false, bool blob = true)
        {
            return new PlanHistoryEntry
            {
                EntryId = id,
                LastGeneratedAtUtc = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc).AddDays(-ageDays),
                Pinned = pinned,
                BlobPresent = blob,
            };
        }

        [Fact]
        public void SelectForEviction_EvictsOldestUnpinnedFirst()
        {
            var entries = new List<PlanHistoryEntry>
            {
                Entry("newest", 0),
                Entry("middle", 1),
                Entry("oldest", 2),
            };

            var evicted = PlanHistoryRetention.SelectForEviction(entries, 2);

            Assert.Equal(new[] { "oldest" }, evicted);
        }

        [Fact]
        public void SelectForEviction_PinnedSurvivesPastTheCap()
        {
            var entries = new List<PlanHistoryEntry>
            {
                Entry("pinned-oldest", 9, pinned: true),
                Entry("unpinned-old", 5),
                Entry("unpinned-new", 1),
            };

            var evicted = PlanHistoryRetention.SelectForEviction(entries, 2);

            // The pinned entry counts against the cap, so only ONE
            // unpinned survivor is allowed - the oldest unpinned goes,
            // the pinned one never does.
            Assert.Equal(new[] { "unpinned-old" }, evicted);
        }

        [Fact]
        public void CapSmallerThanPinnedCount_EvictsEveryUnpinnedAndNoPinned()
        {
            var entries = new List<PlanHistoryEntry>
            {
                Entry("pin-a", 1, pinned: true),
                Entry("pin-b", 2, pinned: true),
                Entry("pin-c", 3, pinned: true),
                Entry("loose-a", 4),
                Entry("loose-b", 5),
            };

            var evicted = PlanHistoryRetention.SelectForEviction(entries, 2);

            Assert.Equal(2, evicted.Count);
            Assert.Contains("loose-a", evicted);
            Assert.Contains("loose-b", evicted);
            Assert.DoesNotContain(evicted, id => id.StartsWith("pin-", StringComparison.Ordinal));
        }

        [Fact]
        public void UnderCap_ReturnsEmpty()
        {
            var entries = new List<PlanHistoryEntry> { Entry("a", 0), Entry("b", 1) };

            Assert.Empty(PlanHistoryRetention.SelectForEviction(entries, 5));
            Assert.Empty(PlanHistoryRetention.SelectForEviction(null, 5));
        }

        [Fact]
        public void SelectForBlobEviction_SamePinnedExemption_OnlyBlobHoldersCount()
        {
            var entries = new List<PlanHistoryEntry>
            {
                Entry("pinned-old-blob", 9, pinned: true),
                Entry("no-blob-oldest", 20, blob: false),
                Entry("blob-old", 5),
                Entry("blob-new", 1),
            };

            var evicted = PlanHistoryRetention.SelectForBlobEviction(entries, 2);

            // Three blobs, cap 2, one of them pinned: the oldest unpinned
            // BLOB goes. The blob-less row is invisible to this cap no
            // matter how old it is.
            Assert.Equal(new[] { "blob-old" }, evicted);
        }

        [Fact]
        public void SortForDisplay_PinnedFirst_ThenNewest_ThenEntryIdTieBreak()
        {
            var tiedA = Entry("aaa", 3);
            var tiedB = Entry("bbb", 3);
            var entries = new List<PlanHistoryEntry>
            {
                tiedB,
                Entry("newest", 0),
                Entry("pinned-old", 9, pinned: true),
                tiedA,
            };

            var sorted = PlanHistoryRetention.SortForDisplay(entries);

            Assert.Equal(new[] { "pinned-old", "newest", "aaa", "bbb" },
                sorted.Select(e => e.EntryId).ToArray());
        }

        [Fact]
        public void SortForDisplay_IsStable_AcrossRepeatedCalls()
        {
            var entries = new List<PlanHistoryEntry>
            {
                Entry("bbb", 3),
                Entry("aaa", 3),
            };

            var first = PlanHistoryRetention.SortForDisplay(entries).Select(e => e.EntryId).ToArray();
            entries.Reverse();
            var second = PlanHistoryRetention.SortForDisplay(entries).Select(e => e.EntryId).ToArray();

            Assert.Equal(first, second);
        }
    }
}
