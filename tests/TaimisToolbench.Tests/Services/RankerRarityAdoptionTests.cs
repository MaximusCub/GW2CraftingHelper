using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    // The policy the Ranker's rows take their rarity by. Exercised through
    // the production type - no mirrored logic.
    public class RankerRarityAdoptionTests
    {
        private static RankerWatchlistEntry Entry(int itemId, string rarity = null)
        {
            return new RankerWatchlistEntry
            {
                ItemId = itemId,
                Quantity = 1,
                Name = "Item " + itemId,
                Rarity = rarity,
            };
        }

        private static IReadOnlyDictionary<int, ItemMetadata> Metadata(int itemId, string rarity)
        {
            return new Dictionary<int, ItemMetadata>
            {
                [itemId] = new ItemMetadata { ItemId = itemId, Name = "x", Rarity = rarity },
            };
        }

        [Fact]
        public void ASolveThatKnowsTheRarity_FillsInAnEntryThatDidNot()
        {
            var entry = Entry(30704);

            Assert.True(RankerRarityAdoption.AdoptFromMetadata(entry, Metadata(30704, "Legendary")));
            Assert.Equal("Legendary", entry.Rarity);
        }

        [Fact]
        public void AnUnchangedRarity_ReportsNoChangeSoNothingIsSaved()
        {
            var entry = Entry(30704, "Legendary");

            Assert.False(RankerRarityAdoption.AdoptFromMetadata(entry, Metadata(30704, "Legendary")));
            Assert.Equal("Legendary", entry.Rarity);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void MetadataWithNoRarity_NeverClearsOneTheEntryAlreadyHad(string rarity)
        {
            var entry = Entry(30704, "Exotic");

            Assert.False(RankerRarityAdoption.AdoptFromMetadata(entry, Metadata(30704, rarity)));
            Assert.Equal("Exotic", entry.Rarity);
        }

        [Fact]
        public void AnItemMissingFromTheSolvesMetadata_LeavesTheEntryAlone()
        {
            var entry = Entry(30704);

            Assert.False(RankerRarityAdoption.AdoptFromMetadata(entry, Metadata(19684, "Basic")));
            Assert.Null(entry.Rarity);
        }

        [Fact]
        public void NoMetadataAtAll_IsNotAFailure()
        {
            var entry = Entry(30704, "Rare");

            Assert.False(RankerRarityAdoption.AdoptFromMetadata(entry, null));
            Assert.False(RankerRarityAdoption.AdoptFromMetadata(null, Metadata(30704, "Rare")));
            Assert.Equal("Rare", entry.Rarity);
        }

        [Fact]
        public void TheSessionStatCache_ColoursRowsThatHaveNeverBeenRefreshed()
        {
            var entries = new List<RankerWatchlistEntry> { Entry(30704), Entry(19684), Entry(46762) };
            var blocks = new Dictionary<int, ItemStatBlock>
            {
                [30704] = new ItemStatBlock { ItemId = 30704, Rarity = "Legendary" },
                [19684] = new ItemStatBlock { ItemId = 19684, Rarity = "Basic" },
            };

            Assert.True(RankerRarityAdoption.AdoptFromStatCache(
                entries, id => blocks.TryGetValue(id, out var block) ? block : null));

            Assert.Equal("Legendary", entries[0].Rarity);
            Assert.Equal("Basic", entries[1].Rarity);

            // Nothing in the cache for the third row, and nothing guessed.
            Assert.Null(entries[2].Rarity);
        }

        [Fact]
        public void TheSessionStatCache_NeverOverwritesTheRarityAnEntryPersisted()
        {
            var entries = new List<RankerWatchlistEntry> { Entry(30704, "Legendary") };

            Assert.False(RankerRarityAdoption.AdoptFromStatCache(
                entries, id => new ItemStatBlock { ItemId = id, Rarity = "Junk" }));
            Assert.Equal("Legendary", entries[0].Rarity);
        }

        [Fact]
        public void ASecondPassOverAnAlreadyAdoptedList_SavesNothing()
        {
            var entries = new List<RankerWatchlistEntry> { Entry(30704) };
            System.Func<int, ItemStatBlock> cache =
                id => new ItemStatBlock { ItemId = id, Rarity = "Exotic" };

            Assert.True(RankerRarityAdoption.AdoptFromStatCache(entries, cache));
            Assert.False(RankerRarityAdoption.AdoptFromStatCache(entries, cache));
        }

        [Fact]
        public void NullsAndEmptyListsAreNoOps()
        {
            Assert.False(RankerRarityAdoption.AdoptFromStatCache(null, id => null));
            Assert.False(RankerRarityAdoption.AdoptFromStatCache(
                new List<RankerWatchlistEntry>(), id => null));
            Assert.False(RankerRarityAdoption.AdoptFromStatCache(
                new List<RankerWatchlistEntry> { null, Entry(0) }, id => null));
        }
    }
}
