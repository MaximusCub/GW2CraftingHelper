using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{

    public class SnapshotSearchResultBuilderTests
    {
        private static SnapshotItemEntry Entry(int itemId, string name, int count, string source, string iconUrl = "")
        {
            return new SnapshotItemEntry
            {
                ItemId = itemId,
                Name = name,
                Count = count,
                Source = source,
                IconUrl = iconUrl
            };
        }

        private static string CharSource(string name) => AccountItemIndex.CharacterSourcePrefix + name;

        // The filter shape MainView hands the builder once the user
        // unchecks one or more per-character boxes: everything else stays
        // checked, and any character not named here is visible.
        private static SnapshotSourceFilter Unchecked(params string[] characterNames)
        {
            var filter = new SnapshotSourceFilter();
            foreach (var name in characterNames)
            {
                filter.UncheckedCharacters.Add(name);
            }

            return filter;
        }

        // Builds the itemId -> representative-entry map the way MainView
        // does once per snapshot (see SnapshotSearchResultBuilder.
        // BuildRepresentativeIndex) - every BuildItemRows test below feeds
        // its raw items list through this helper first, exactly like the
        // real caller, rather than passing the raw list to BuildItemRows
        // directly (BuildItemRows now takes the already-deduped map, not
        // the raw per-source entry list).
        private static IReadOnlyDictionary<int, SnapshotItemEntry> ItemsById(IReadOnlyList<SnapshotItemEntry> items) =>
            SnapshotSearchResultBuilder.BuildRepresentativeIndex(items);

        // ---- BuildRepresentativeIndex ----

        [Fact]
        public void BuildRepresentativeIndex_NullItems_ReturnsEmpty()
        {
            var result = SnapshotSearchResultBuilder.BuildRepresentativeIndex(null);

            Assert.Empty(result);
        }

        [Fact]
        public void BuildRepresentativeIndex_EmptyItems_ReturnsEmpty()
        {
            var result = SnapshotSearchResultBuilder.BuildRepresentativeIndex(new List<SnapshotItemEntry>());

            Assert.Empty(result);
        }

        [Fact]
        public void BuildRepresentativeIndex_NullEntryInList_Skipped()
        {
            var items = new List<SnapshotItemEntry> { Entry(100, "Iron Ore", 5, AccountItemIndex.SourceBank), null };

            var result = SnapshotSearchResultBuilder.BuildRepresentativeIndex(items);

            Assert.Single(result);
            Assert.True(result.ContainsKey(100));
        }

        [Fact]
        public void BuildRepresentativeIndex_DuplicateItemId_FirstSeenEntryWins()
        {
            var first = Entry(100, "Iron Ore", 10, AccountItemIndex.SourceBank);
            var second = Entry(100, "Iron Ore", 7, AccountItemIndex.SourceMaterialStorage);
            var items = new List<SnapshotItemEntry> { first, second };

            var result = SnapshotSearchResultBuilder.BuildRepresentativeIndex(items);

            Assert.Single(result);
            Assert.Same(first, result[100]);
        }

        [Fact]
        public void BuildRepresentativeIndex_DistinctItemIds_OneEntryPerId()
        {
            var items = new List<SnapshotItemEntry>
            {
                Entry(1, "Iron Ore", 10, AccountItemIndex.SourceBank),
                Entry(2, "Linen Scrap", 5, AccountItemIndex.SourceBank)
            };

            var result = SnapshotSearchResultBuilder.BuildRepresentativeIndex(items);

            Assert.Equal(2, result.Count);
        }

        // ---- BuildItemRows ----

        [Fact]
        public void BuildItemRows_NullItemsById_ReturnsEmpty()
        {
            var index = new AccountItemIndex(null);
            var result = SnapshotSearchResultBuilder.BuildItemRows(null, index, "", new SnapshotSourceFilter(), null);

            Assert.Empty(result);
        }

        [Fact]
        public void BuildItemRows_NullIndex_ReturnsEmpty()
        {
            var items = new List<SnapshotItemEntry> { Entry(1, "Iron Ore", 5, "Bank") };
            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), null, "", new SnapshotSourceFilter(), null);

            Assert.Empty(result);
        }

        [Fact]
        public void BuildItemRows_EmptyItems_ReturnsEmpty()
        {
            var index = new AccountItemIndex(new List<SnapshotItemEntry>());
            var result = SnapshotSearchResultBuilder.BuildItemRows(
                ItemsById(new List<SnapshotItemEntry>()), index, "", new SnapshotSourceFilter(), null);

            Assert.Empty(result);
        }

        [Fact]
        public void BuildItemRows_SingleItemSingleSource_TotalMatchesAndSingleBreakdownEntry()
        {
            var items = new List<SnapshotItemEntry> { Entry(100, "Iron Ore", 40, AccountItemIndex.SourceBank) };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", new SnapshotSourceFilter(), null);

            Assert.Single(result);
            Assert.Equal(100, result[0].ItemId);
            Assert.Equal("Iron Ore", result[0].Name);
            Assert.Equal(40, result[0].TotalCount);
            Assert.Single(result[0].Breakdown);
            Assert.Equal("Bank", result[0].Breakdown[0].Label);
            Assert.Equal(40, result[0].Breakdown[0].Count);
        }

        [Fact]
        public void BuildItemRows_ItemAcrossMultipleSources_TotalsAndBreaksDownEachSource()
        {
            var items = new List<SnapshotItemEntry>
            {
                Entry(100, "Iron Ore", 150, AccountItemIndex.SourceMaterialStorage),
                Entry(100, "Iron Ore", 100, AccountItemIndex.SourceBank)
            };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", new SnapshotSourceFilter(), null);

            Assert.Single(result);
            Assert.Equal(250, result[0].TotalCount);
            Assert.Equal(2, result[0].Breakdown.Count);
            // MaterialStorage outranks Bank in GetPrioritizedSources.
            Assert.Equal("Material Storage", result[0].Breakdown[0].Label);
            Assert.Equal(150, result[0].Breakdown[0].Count);
            Assert.Equal("Bank", result[0].Breakdown[1].Label);
            Assert.Equal(100, result[0].Breakdown[1].Count);
        }

        [Fact]
        public void BuildItemRows_SearchText_MatchesNameCaseInsensitiveSubstring()
        {
            var items = new List<SnapshotItemEntry>
            {
                Entry(1, "Iron Ore", 10, AccountItemIndex.SourceBank),
                Entry(2, "Linen Scrap", 5, AccountItemIndex.SourceBank)
            };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "iron", new SnapshotSourceFilter(), null);

            Assert.Single(result);
            Assert.Equal("Iron Ore", result[0].Name);
        }

        [Fact]
        public void BuildItemRows_SearchText_NoMatch_ReturnsEmpty()
        {
            var items = new List<SnapshotItemEntry> { Entry(1, "Iron Ore", 10, AccountItemIndex.SourceBank) };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "linen", new SnapshotSourceFilter(), null);

            Assert.Empty(result);
        }

        [Fact]
        public void BuildItemRows_SearchText_NeverMatchesSourceOrCharacterLabel()
        {
            // Feature 1 Open Question 2's accepted choice: search is scoped
            // to item names only, never source/character labels.
            var items = new List<SnapshotItemEntry> { Entry(1, "Iron Ore", 10, CharSource("Zaeed")) };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "zaeed", new SnapshotSourceFilter(), null);

            Assert.Empty(result);
        }

        [Fact]
        public void BuildItemRows_SourceFilter_ExcludedSourceDropsFromBreakdownAndTotal()
        {
            var items = new List<SnapshotItemEntry>
            {
                Entry(100, "Iron Ore", 150, AccountItemIndex.SourceMaterialStorage),
                Entry(100, "Iron Ore", 100, AccountItemIndex.SourceBank)
            };
            var index = new AccountItemIndex(items);
            var filter = new SnapshotSourceFilter { Bank = false };

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", filter, null);

            Assert.Single(result);
            Assert.Equal(150, result[0].TotalCount);
            Assert.Single(result[0].Breakdown);
            Assert.Equal("Material Storage", result[0].Breakdown[0].Label);
        }

        [Fact]
        public void BuildItemRows_SourceFilter_AllSourcesExcluded_ItemDropsEntirely()
        {
            var items = new List<SnapshotItemEntry> { Entry(100, "Iron Ore", 40, AccountItemIndex.SourceBank) };
            var index = new AccountItemIndex(items);
            var filter = new SnapshotSourceFilter { Bank = false, MaterialStorage = false, SharedInventory = false };

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", filter, null);

            Assert.Empty(result);
        }

        // ---- BuildItemRows: per-character source filtering ----

        [Fact]
        public void BuildItemRows_UncheckedCharacter_HidesOnlyThatCharactersContribution()
        {
            var items = new List<SnapshotItemEntry>
            {
                Entry(100, "Iron Ore", 5, CharSource("Alice")),
                Entry(100, "Iron Ore", 3, CharSource("Bob")),
                Entry(100, "Iron Ore", 10, AccountItemIndex.SourceBank)
            };
            var index = new AccountItemIndex(items);
            var filter = Unchecked("Alice");

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", filter, null);

            Assert.Single(result);
            Assert.Equal(13, result[0].TotalCount);
            Assert.DoesNotContain(result[0].Breakdown, b => b.Label == "Character: Alice");
            Assert.Contains(result[0].Breakdown, b => b.Label == "Character: Bob");
        }

        [Fact]
        public void BuildItemRows_CharacterAbsentFromUncheckedSet_DefaultsVisible()
        {
            // A character seen for the first time in a fresh snapshot is not
            // in the exclusion set, so it shows without anyone having to
            // seed a checked flag for it.
            var items = new List<SnapshotItemEntry>
            {
                Entry(100, "Iron Ore", 5, CharSource("Alice")),
                Entry(100, "Iron Ore", 7, CharSource("Newcomer"))
            };
            var index = new AccountItemIndex(items);
            var filter = Unchecked("Alice");

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", filter, null);

            Assert.Single(result);
            Assert.Equal(7, result[0].TotalCount);
            Assert.Single(result[0].Breakdown);
            Assert.Equal("Character: Newcomer", result[0].Breakdown[0].Label);
        }

        [Fact]
        public void BuildItemRows_ItemHeldOnlyByUncheckedCharacter_DropsEntirely()
        {
            var items = new List<SnapshotItemEntry> { Entry(100, "Iron Ore", 5, CharSource("Alice")) };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", Unchecked("Alice"), null);

            Assert.Empty(result);
        }

        [Fact]
        public void BuildItemRows_EveryCharacterUnchecked_LeavesStorageSourcesOnly()
        {
            var items = new List<SnapshotItemEntry>
            {
                Entry(100, "Iron Ore", 5, CharSource("Alice")),
                Entry(100, "Iron Ore", 3, CharSource("Bob")),
                Entry(100, "Iron Ore", 10, AccountItemIndex.SourceMaterialStorage)
            };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(
                ItemsById(items), index, "", Unchecked("Alice", "Bob"), null);

            Assert.Single(result);
            Assert.Equal(10, result[0].TotalCount);
            Assert.Single(result[0].Breakdown);
            Assert.Equal("Material Storage", result[0].Breakdown[0].Label);
        }

        [Fact]
        public void BuildItemRows_UncheckedCharacterName_MatchedOrdinally()
        {
            // Character names come from the same snapshot strings the index
            // encodes its source keys from, so only an exact match hides a
            // character - a differently-cased name is a different character.
            var items = new List<SnapshotItemEntry> { Entry(100, "Iron Ore", 5, CharSource("Zaeed")) };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", Unchecked("zaeed"), null);

            Assert.Single(result);
            Assert.Equal(5, result[0].TotalCount);
        }

        [Fact]
        public void BuildItemRows_NullSourceFilter_TreatedAsShowEverything()
        {
            var items = new List<SnapshotItemEntry> { Entry(100, "Iron Ore", 40, AccountItemIndex.SourceBank) };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", null, null);

            Assert.Single(result);
            Assert.Equal(40, result[0].TotalCount);
        }

        [Fact]
        public void BuildItemRows_MultipleItems_SortedByNameOrdinalCaseInsensitive()
        {
            var items = new List<SnapshotItemEntry>
            {
                Entry(1, "zinc Ore", 1, AccountItemIndex.SourceBank),
                Entry(2, "Ancient Wood", 1, AccountItemIndex.SourceBank),
                Entry(3, "bronze Ingot", 1, AccountItemIndex.SourceBank)
            };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", new SnapshotSourceFilter(), null);

            Assert.Equal(3, result.Count);
            Assert.Equal("Ancient Wood", result[0].Name);
            Assert.Equal("bronze Ingot", result[1].Name);
            Assert.Equal("zinc Ore", result[2].Name);
        }

        [Fact]
        public void BuildItemRows_SameNameDifferentItemIds_TieBrokenByItemIdAscending()
        {
            var items = new List<SnapshotItemEntry>
            {
                Entry(200, "Recipe: Iron Ingot", 1, AccountItemIndex.SourceBank),
                Entry(100, "Recipe: Iron Ingot", 1, AccountItemIndex.SourceBank)
            };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", new SnapshotSourceFilter(), null);

            Assert.Equal(2, result.Count);
            Assert.Equal(100, result[0].ItemId);
            Assert.Equal(200, result[1].ItemId);
        }

        [Fact]
        public void BuildItemRows_DuplicateEntriesSameItemIdAndSource_CountsSummed()
        {
            var items = new List<SnapshotItemEntry>
            {
                Entry(100, "Iron Ore", 10, AccountItemIndex.SourceBank),
                Entry(100, "Iron Ore", 7, AccountItemIndex.SourceBank)
            };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", new SnapshotSourceFilter(), null);

            Assert.Single(result);
            Assert.Equal(17, result[0].TotalCount);
        }

        [Fact]
        public void BuildItemRows_BlankName_FallsBackToUnknownItem()
        {
            var items = new List<SnapshotItemEntry> { Entry(100, "", 5, AccountItemIndex.SourceBank) };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", new SnapshotSourceFilter(), null);

            Assert.Single(result);
            Assert.Equal("Unknown Item", result[0].Name);
        }

        [Fact]
        public void BuildItemRows_NullEntryInList_SkippedViaRepresentativeIndex()
        {
            var items = new List<SnapshotItemEntry> { Entry(100, "Iron Ore", 5, AccountItemIndex.SourceBank), null };
            var index = new AccountItemIndex(items.Where(i => i != null).ToList());

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", new SnapshotSourceFilter(), null);

            Assert.Single(result);
        }

        [Fact]
        public void BuildItemRows_ActiveCharacter_PrioritizedAheadOfSharedAndBank()
        {
            var items = new List<SnapshotItemEntry>
            {
                Entry(100, "Iron Ore", 1, AccountItemIndex.SourceBank),
                Entry(100, "Iron Ore", 2, AccountItemIndex.SourceSharedInventory),
                Entry(100, "Iron Ore", 3, CharSource("Zaeed"))
            };
            var index = new AccountItemIndex(items);

            var result = SnapshotSearchResultBuilder.BuildItemRows(ItemsById(items), index, "", new SnapshotSourceFilter(), "Zaeed");

            Assert.Single(result);
            Assert.Equal("Character: Zaeed", result[0].Breakdown[0].Label);
            Assert.Equal("Shared Inventory", result[0].Breakdown[1].Label);
            Assert.Equal("Bank", result[0].Breakdown[2].Label);
        }

        // ---- FilterWallet ----

        [Fact]
        public void FilterWallet_Null_ReturnsEmpty()
        {
            Assert.Empty(SnapshotSearchResultBuilder.FilterWallet(null, ""));
        }

        [Fact]
        public void FilterWallet_EmptySearch_ReturnsAllEntries()
        {
            var wallet = new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 100 },
                new SnapshotWalletEntry { CurrencyId = 3, CurrencyName = "Gems", Value = 5 }
            };

            var result = SnapshotSearchResultBuilder.FilterWallet(wallet, "");

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void FilterWallet_SearchText_MatchesCurrencyNameCaseInsensitive()
        {
            var wallet = new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 100 },
                new SnapshotWalletEntry { CurrencyId = 3, CurrencyName = "Gems", Value = 5 }
            };

            var result = SnapshotSearchResultBuilder.FilterWallet(wallet, "KARMA");

            Assert.Single(result);
            Assert.Equal("Karma", result[0].CurrencyName);
        }

        [Fact]
        public void FilterWallet_NullEntriesInList_Skipped()
        {
            var wallet = new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 100 },
                null
            };

            var result = SnapshotSearchResultBuilder.FilterWallet(wallet, "");

            Assert.Single(result);
        }

        [Fact]
        public void FilterWallet_NoMatch_ReturnsEmpty()
        {
            var wallet = new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 100 }
            };

            var result = SnapshotSearchResultBuilder.FilterWallet(wallet, "gems");

            Assert.Empty(result);
        }

        // ---- IsSourceEnabled ----

        [Fact]
        public void IsSourceEnabled_NullFilter_AlwaysTrue()
        {
            Assert.True(SnapshotSearchResultBuilder.IsSourceEnabled(AccountItemIndex.SourceBank, null));
        }

        [Fact]
        public void IsSourceEnabled_NullOrEmptySource_False()
        {
            Assert.False(SnapshotSearchResultBuilder.IsSourceEnabled(null, new SnapshotSourceFilter()));
            Assert.False(SnapshotSearchResultBuilder.IsSourceEnabled("", new SnapshotSourceFilter()));
        }

        [Fact]
        public void IsSourceEnabled_KnownSources_RespectEachFlagIndependently()
        {
            var filter = new SnapshotSourceFilter { Bank = false, MaterialStorage = true, SharedInventory = false };
            filter.UncheckedCharacters.Add("Bob");

            Assert.False(SnapshotSearchResultBuilder.IsSourceEnabled(AccountItemIndex.SourceBank, filter));
            Assert.True(SnapshotSearchResultBuilder.IsSourceEnabled(AccountItemIndex.SourceMaterialStorage, filter));
            Assert.False(SnapshotSearchResultBuilder.IsSourceEnabled(AccountItemIndex.SourceSharedInventory, filter));
            Assert.True(SnapshotSearchResultBuilder.IsSourceEnabled(CharSource("Zaeed"), filter));
            Assert.False(SnapshotSearchResultBuilder.IsSourceEnabled(CharSource("Bob"), filter));
        }

        [Fact]
        public void IsSourceEnabled_NullUncheckedCharacterSet_TreatedAsEveryCharacterChecked()
        {
            var filter = new SnapshotSourceFilter { UncheckedCharacters = null };

            Assert.True(SnapshotSearchResultBuilder.IsSourceEnabled(CharSource("Zaeed"), filter));
        }

        [Fact]
        public void IsSourceEnabled_UnknownSourceShape_FailsOpenAndIsAlwaysTrue()
        {
            var filter = new SnapshotSourceFilter { Bank = false, MaterialStorage = false, SharedInventory = false };

            Assert.True(SnapshotSearchResultBuilder.IsSourceEnabled("SomeFutureSource", filter));
        }

        // ---- CollectCharacterNames ----

        [Fact]
        public void CollectCharacterNames_NullSnapshot_ReturnsEmpty()
        {
            Assert.Empty(SnapshotSearchResultBuilder.CollectCharacterNames(null));
        }

        [Fact]
        public void CollectCharacterNames_EmptySnapshot_ReturnsEmpty()
        {
            Assert.Empty(SnapshotSearchResultBuilder.CollectCharacterNames(new AccountSnapshot()));
        }

        [Fact]
        public void CollectCharacterNames_StorageSourcesIgnored_CharacterSourcesDeduped()
        {
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    Entry(1, "Iron Ore", 5, AccountItemIndex.SourceBank),
                    Entry(1, "Iron Ore", 5, AccountItemIndex.SourceMaterialStorage),
                    Entry(1, "Iron Ore", 5, CharSource("Zaeed")),
                    Entry(2, "Linen Scrap", 5, CharSource("Zaeed"))
                }
            };

            var result = SnapshotSearchResultBuilder.CollectCharacterNames(snapshot);

            Assert.Equal(new[] { "Zaeed" }, result);
        }

        [Fact]
        public void CollectCharacterNames_CharacterWithNoItems_StillListedFromDisciplines()
        {
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry> { Entry(1, "Iron Ore", 5, CharSource("Alice")) },
                CharacterDisciplines = new List<SnapshotCharacterDiscipline>
                {
                    new SnapshotCharacterDiscipline { CharacterName = "Emptyhands", Discipline = "Chef", Rating = 400 },
                    new SnapshotCharacterDiscipline { CharacterName = "Alice", Discipline = "Armorsmith", Rating = 500 }
                }
            };

            var result = SnapshotSearchResultBuilder.CollectCharacterNames(snapshot);

            Assert.Equal(new[] { "Alice", "Emptyhands" }, result);
        }

        [Fact]
        public void CollectCharacterNames_ZeroCountEntry_StillListsTheCharacter()
        {
            // AccountItemIndex drops zero-count entries; the roster does not
            // - an empty character still gets its own checkbox.
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry> { Entry(1, "Iron Ore", 0, CharSource("Emptyhands")) }
            };

            var result = SnapshotSearchResultBuilder.CollectCharacterNames(snapshot);

            Assert.Equal(new[] { "Emptyhands" }, result);
        }

        [Fact]
        public void CollectCharacterNames_SortedCaseInsensitiveWithOrdinalTiebreak()
        {
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    Entry(1, "Iron Ore", 1, CharSource("zara")),
                    Entry(1, "Iron Ore", 1, CharSource("Alice")),
                    Entry(1, "Iron Ore", 1, CharSource("Zara")),
                    Entry(1, "Iron Ore", 1, CharSource("bob"))
                }
            };

            var result = SnapshotSearchResultBuilder.CollectCharacterNames(snapshot);

            Assert.Equal(new[] { "Alice", "bob", "Zara", "zara" }, result);
        }

        [Fact]
        public void CollectCharacterNames_NullAndBlankEntriesSkipped()
        {
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    null,
                    Entry(1, "Iron Ore", 1, null),
                    Entry(1, "Iron Ore", 1, AccountItemIndex.CharacterSourcePrefix),
                    Entry(1, "Iron Ore", 1, CharSource("Alice"))
                },
                CharacterDisciplines = new List<SnapshotCharacterDiscipline>
                {
                    null,
                    new SnapshotCharacterDiscipline { CharacterName = "", Discipline = "Chef" }
                }
            };

            var result = SnapshotSearchResultBuilder.CollectCharacterNames(snapshot);

            Assert.Equal(new[] { "Alice" }, result);
        }

        // ---- FormatSourceLabel ----

        [Fact]
        public void FormatSourceLabel_NullOrEmpty_ReturnsUnknown()
        {
            Assert.Equal("Unknown", SnapshotSearchResultBuilder.FormatSourceLabel(null));
            Assert.Equal("Unknown", SnapshotSearchResultBuilder.FormatSourceLabel(""));
        }

        [Fact]
        public void FormatSourceLabel_KnownStorageSources_SpacedOutDisplayNames()
        {
            Assert.Equal("Bank", SnapshotSearchResultBuilder.FormatSourceLabel(AccountItemIndex.SourceBank));
            Assert.Equal("Material Storage", SnapshotSearchResultBuilder.FormatSourceLabel(AccountItemIndex.SourceMaterialStorage));
            Assert.Equal("Shared Inventory", SnapshotSearchResultBuilder.FormatSourceLabel(AccountItemIndex.SourceSharedInventory));
        }

        [Fact]
        public void FormatSourceLabel_CharacterSource_StripsRawEncodingPrefix()
        {
            Assert.Equal("Character: Zaeed", SnapshotSearchResultBuilder.FormatSourceLabel(CharSource("Zaeed")));
        }

        [Fact]
        public void FormatSourceLabel_UnknownSource_ReturnedAsIs()
        {
            Assert.Equal("SomeFutureSource", SnapshotSearchResultBuilder.FormatSourceLabel("SomeFutureSource"));
        }
    }

}
