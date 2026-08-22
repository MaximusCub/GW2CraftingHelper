using System.Collections.Generic;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The row-state decisions CraftingPlanView's search-box TextChanged
    /// handler and TriggerGenerate's typed-name resolution pass call -
    /// Blish-free, exercising the real ItemRowSelection production code.
    /// </summary>
    public class ItemRowSelectionTests
    {
        private static ItemSearchResult Result(int id, string name)
        {
            return new ItemSearchResult { ItemId = id, Name = name, IsPlanTarget = true };
        }

        [Fact]
        public void SelectionIsStale_NothingResolved_IsNeverStale()
        {
            Assert.False(ItemRowSelection.SelectionIsStale(null, null, "Mystic Clover"));
            Assert.False(ItemRowSelection.SelectionIsStale(null, null, ""));
        }

        [Fact]
        public void SelectionIsStale_TextStillMatchesResolvedName_IsNotStale()
        {
            Assert.False(ItemRowSelection.SelectionIsStale(19721, "Mystic Clover", "Mystic Clover"));
        }

        [Fact]
        public void SelectionIsStale_TextEditedAfterPick_IsStale()
        {
            // The bug this class exists for: the box reads one item while
            // the row still carries the id of the previously picked one.
            Assert.True(ItemRowSelection.SelectionIsStale(19685, "Deldrimor Steel Ingot", "Mystic Clover"));
        }

        [Fact]
        public void SelectionIsStale_SingleCharacterEditAfterPick_IsStale()
        {
            Assert.True(ItemRowSelection.SelectionIsStale(19721, "Mystic Clover", "Mystic Clove"));
            Assert.True(ItemRowSelection.SelectionIsStale(19721, "Mystic Clover", "Mystic Clovers"));
        }

        [Fact]
        public void SelectionIsStale_BoxCleared_IsStale()
        {
            Assert.True(ItemRowSelection.SelectionIsStale(19721, "Mystic Clover", ""));
            Assert.True(ItemRowSelection.SelectionIsStale(19721, "Mystic Clover", null));
        }

        [Fact]
        public void SelectionIsStale_CaseOrSurroundingWhitespaceOnly_IsNotStale()
        {
            // Retyping the same name differently cased still means the same
            // item - the generate-time match is case-insensitive too.
            Assert.False(ItemRowSelection.SelectionIsStale(19721, "Mystic Clover", "mystic clover"));
            Assert.False(ItemRowSelection.SelectionIsStale(19721, "Mystic Clover", "  Mystic Clover "));
        }

        [Fact]
        public void SelectionIsStale_ResolvedNameMissing_IsStale()
        {
            // An id with no name can never be shown to still match; drop it
            // rather than plan for an item the box does not name.
            Assert.True(ItemRowSelection.SelectionIsStale(19721, null, "Mystic Clover"));
        }

        [Fact]
        public void FindExactNameMatch_ExactName_ReturnsThatResult()
        {
            var results = new List<ItemSearchResult>
            {
                Result(19976, "Mystic Coin"),
                Result(19721, "Mystic Clover")
            };

            var match = ItemRowSelection.FindExactNameMatch(results, "Mystic Clover");

            Assert.NotNull(match);
            Assert.Equal(19721, match.ItemId);
        }

        [Fact]
        public void FindExactNameMatch_IsCaseAndWhitespaceInsensitive()
        {
            var results = new List<ItemSearchResult> { Result(19721, "Mystic Clover") };

            Assert.Equal(19721, ItemRowSelection.FindExactNameMatch(results, "  mystic CLOVER ").ItemId);
        }

        [Fact]
        public void FindExactNameMatch_PartialName_ReturnsNull()
        {
            // A prefix must not silently adopt the top-ranked result.
            var results = new List<ItemSearchResult>
            {
                Result(19976, "Mystic Coin"),
                Result(19721, "Mystic Clover")
            };

            Assert.Null(ItemRowSelection.FindExactNameMatch(results, "Mystic"));
        }

        [Fact]
        public void FindExactNameMatch_NoResultsOrBlankText_ReturnsNull()
        {
            var results = new List<ItemSearchResult> { Result(19721, "Mystic Clover") };

            Assert.Null(ItemRowSelection.FindExactNameMatch(null, "Mystic Clover"));
            Assert.Null(ItemRowSelection.FindExactNameMatch(new List<ItemSearchResult>(), "Mystic Clover"));
            Assert.Null(ItemRowSelection.FindExactNameMatch(results, "   "));
            Assert.Null(ItemRowSelection.FindExactNameMatch(results, null));
        }

        [Fact]
        public void FindExactNameMatch_SkipsNullEntriesAndNullNames()
        {
            var results = new List<ItemSearchResult>
            {
                null,
                new ItemSearchResult { ItemId = 1, Name = null },
                Result(19721, "Mystic Clover")
            };

            Assert.Equal(19721, ItemRowSelection.FindExactNameMatch(results, "Mystic Clover").ItemId);
        }

        [Fact]
        public void FindExactNameMatch_NullNameAgainstBlankText_StillReturnsNull()
        {
            // Blank text short-circuits before any comparison, so a
            // null-named result can never be matched by an empty box.
            var results = new List<ItemSearchResult> { new ItemSearchResult { ItemId = 1, Name = null } };

            Assert.Null(ItemRowSelection.FindExactNameMatch(results, ""));
        }

        [Fact]
        public void EmptyRequestStatus_NoRowHasText_AsksForASelection()
        {
            Assert.Equal(ItemRowSelection.NoItemsStatus, ItemRowSelection.EmptyRequestStatus(false));
        }

        [Fact]
        public void EmptyRequestStatus_TypedButUnresolved_PointsAtTheSuggestionList()
        {
            Assert.Equal(ItemRowSelection.UnmatchedTextStatus, ItemRowSelection.EmptyRequestStatus(true));
        }
    }
}
