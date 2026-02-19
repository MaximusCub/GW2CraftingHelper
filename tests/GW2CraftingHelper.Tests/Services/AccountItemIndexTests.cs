using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class AccountItemIndexTests
    {
        private static SnapshotItemEntry Entry(int itemId, int count, string source)
        {
            return new SnapshotItemEntry
            {
                ItemId = itemId,
                Count = count,
                Source = source
            };
        }

        [Fact]
        public void EmptyItems_AllQueriesReturnZero()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>());

            Assert.Equal(0, index.GetQuantity(1, "Bank"));
            Assert.Empty(index.GetSources(1));
        }

        [Fact]
        public void NullItems_AllQueriesReturnZero()
        {
            var index = new AccountItemIndex(null);

            Assert.Equal(0, index.GetQuantity(1, "Bank"));
            Assert.Empty(index.GetSources(1));
        }

        [Fact]
        public void SingleSourceSingleItem_CorrectQuantity()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                Entry(100, 25, "MaterialStorage")
            });

            Assert.Equal(25, index.GetQuantity(100, "MaterialStorage"));
            Assert.Equal(0, index.GetQuantity(100, "Bank"));
            Assert.Equal(0, index.GetQuantity(999, "MaterialStorage"));
        }

        [Fact]
        public void MultipleSourcesSameItem_EachSourceCorrect()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                Entry(100, 10, "MaterialStorage"),
                Entry(100, 5, "Bank"),
                Entry(100, 3, "Alice")
            });

            Assert.Equal(10, index.GetQuantity(100, "MaterialStorage"));
            Assert.Equal(5, index.GetQuantity(100, "Bank"));
            Assert.Equal(3, index.GetQuantity(100, "Alice"));
        }

        [Fact]
        public void DuplicateEntries_QuantitiesSummed()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                Entry(100, 10, "Bank"),
                Entry(100, 7, "Bank")
            });

            Assert.Equal(17, index.GetQuantity(100, "Bank"));
        }

        [Fact]
        public void GetSources_ReturnsAllSourcesForItem()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                Entry(100, 10, "MaterialStorage"),
                Entry(100, 5, "Bank"),
                Entry(200, 3, "SharedInventory")
            });

            var sources100 = index.GetSources(100);
            Assert.Equal(2, sources100.Count);
            Assert.Contains("MaterialStorage", sources100);
            Assert.Contains("Bank", sources100);

            var sources200 = index.GetSources(200);
            Assert.Single(sources200);
            Assert.Equal("SharedInventory", sources200[0]);
        }

        [Fact]
        public void GetSources_UnknownItem_ReturnsEmpty()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                Entry(100, 10, "Bank")
            });

            Assert.Empty(index.GetSources(999));
        }

        [Fact]
        public void GetQuantity_NullSource_ReturnsZero()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                Entry(100, 10, "Bank")
            });

            Assert.Equal(0, index.GetQuantity(100, null));
        }

        [Fact]
        public void ZeroCountEntries_Excluded()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                Entry(100, 0, "Bank"),
                Entry(100, 5, "MaterialStorage")
            });

            Assert.Equal(0, index.GetQuantity(100, "Bank"));
            Assert.Equal(5, index.GetQuantity(100, "MaterialStorage"));
            var sources = index.GetSources(100);
            Assert.Single(sources);
            Assert.Equal("MaterialStorage", sources[0]);
        }

        [Fact]
        public void GetPrioritizedSources_RespectsPriorityOrder()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                Entry(100, 1, "Bank"),
                Entry(100, 2, "SharedInventory"),
                Entry(100, 3, "MaterialStorage"),
                Entry(100, 4, "Alice")
            });

            var prioritized = AccountItemIndex.GetPrioritizedSources(
                100, index, "Alice");

            Assert.Equal(4, prioritized.Count);
            Assert.Equal("MaterialStorage", prioritized[0]);
            Assert.Equal("Alice", prioritized[1]);
            Assert.Equal("SharedInventory", prioritized[2]);
            Assert.Equal("Bank", prioritized[3]);
        }

        [Fact]
        public void GetPrioritizedSources_NullActiveChar_SkipsCharPriority()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                Entry(100, 1, "Bank"),
                Entry(100, 2, "MaterialStorage"),
                Entry(100, 3, "Bob")
            });

            var prioritized = AccountItemIndex.GetPrioritizedSources(
                100, index, null);

            Assert.Equal(3, prioritized.Count);
            Assert.Equal("MaterialStorage", prioritized[0]);
            Assert.Equal("Bank", prioritized[1]);
            Assert.Equal("Bob", prioritized[2]);
        }

        [Fact]
        public void GetPrioritizedSources_OtherChars_SortedAlphabetically()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                Entry(100, 1, "Charlie"),
                Entry(100, 2, "Alice"),
                Entry(100, 3, "Bob")
            });

            var prioritized = AccountItemIndex.GetPrioritizedSources(
                100, index, null);

            Assert.Equal(3, prioritized.Count);
            Assert.Equal("Alice", prioritized[0]);
            Assert.Equal("Bob", prioritized[1]);
            Assert.Equal("Charlie", prioritized[2]);
        }

        [Fact]
        public void GetPrioritizedSources_UnknownItem_ReturnsEmpty()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>());

            var prioritized = AccountItemIndex.GetPrioritizedSources(
                999, index, "Alice");

            Assert.Empty(prioritized);
        }

        [Fact]
        public void GetPrioritizedSources_ActiveCharNotInSources_Skipped()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                Entry(100, 5, "Bank"),
                Entry(100, 3, "MaterialStorage")
            });

            var prioritized = AccountItemIndex.GetPrioritizedSources(
                100, index, "NonexistentChar");

            Assert.Equal(2, prioritized.Count);
            Assert.Equal("MaterialStorage", prioritized[0]);
            Assert.Equal("Bank", prioritized[1]);
        }
    }
}
