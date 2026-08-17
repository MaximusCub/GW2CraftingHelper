using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// (redesign,
    /// docs/gw2e-considerations.md): pure unit coverage of
    /// CraftCompetencyEvaluator, independent of PlanSolver - see
    /// PlanSolverCraftCompetencyTests for the real Solve()-path coverage of
    /// the default-flip behavior this class drives.
    /// </summary>
    public class CraftCompetencyEvaluatorTests
    {
        [Fact]
        public void BuildBestRatingByDiscipline_NullList_ReturnsNull()
        {
            // "No snapshot captured this data at all" must stay
            // distinguishable from "captured, and it is empty" - see the
            // method's own doc comment.
            Assert.Null(CraftCompetencyEvaluator.BuildBestRatingByDiscipline(null));
        }

        [Fact]
        public void BuildBestRatingByDiscipline_EmptyList_ReturnsEmptyNonNullDictionary()
        {
            var result = CraftCompetencyEvaluator.BuildBestRatingByDiscipline(
                new List<SnapshotCharacterDiscipline>());

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void BuildBestRatingByDiscipline_MultipleCharacters_KeepsHighestRatingPerDiscipline()
        {
            var characters = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Alice", Discipline = "Weaponsmith", Rating = 300 },
                new SnapshotCharacterDiscipline { CharacterName = "Bob", Discipline = "Weaponsmith", Rating = 400 },
                new SnapshotCharacterDiscipline { CharacterName = "Carol", Discipline = "Armorsmith", Rating = 100 }
            };

            var result = CraftCompetencyEvaluator.BuildBestRatingByDiscipline(characters);

            Assert.Equal(400, result["Weaponsmith"]);
            Assert.Equal(100, result["Armorsmith"]);
        }

        [Fact]
        public void BuildBestRatingByDiscipline_IgnoresNullEntriesAndNullDisciplineNames()
        {
            var characters = new List<SnapshotCharacterDiscipline>
            {
                null,
                new SnapshotCharacterDiscipline { CharacterName = "Alice", Discipline = null, Rating = 400 },
                new SnapshotCharacterDiscipline { CharacterName = "Bob", Discipline = "Weaponsmith", Rating = 300 }
            };

            var result = CraftCompetencyEvaluator.BuildBestRatingByDiscipline(characters);

            Assert.Single(result);
            Assert.Equal(300, result["Weaponsmith"]);
        }

        [Fact]
        public void AccountCanCraft_NullBestRating_UnknownCompetency_NeverPenalizes()
        {
            Assert.True(CraftCompetencyEvaluator.AccountCanCraft(
                new List<string> { "Weaponsmith" }, 400, null));
        }

        [Fact]
        public void AccountCanCraft_EmptyDisciplines_AlwaysTrue()
        {
            var bestRating = new Dictionary<string, int>();
            Assert.True(CraftCompetencyEvaluator.AccountCanCraft(new List<string>(), 400, bestRating));
            Assert.True(CraftCompetencyEvaluator.AccountCanCraft(null, 400, bestRating));
        }

        [Fact]
        public void AccountCanCraft_RatingMeetsMinimum_True()
        {
            var bestRating = new Dictionary<string, int> { { "Weaponsmith", 400 } };
            Assert.True(CraftCompetencyEvaluator.AccountCanCraft(
                new List<string> { "Weaponsmith" }, 400, bestRating));
        }

        [Fact]
        public void AccountCanCraft_RatingBelowMinimum_False()
        {
            var bestRating = new Dictionary<string, int> { { "Weaponsmith", 399 } };
            Assert.False(CraftCompetencyEvaluator.AccountCanCraft(
                new List<string> { "Weaponsmith" }, 400, bestRating));
        }

        [Fact]
        public void AccountCanCraft_KnownButNoCharacterHasDiscipline_False()
        {
            // Known snapshot data (non-null, empty dictionary) - distinct
            // from "unknown" - correctly blocks.
            var bestRating = new Dictionary<string, int>();
            Assert.False(CraftCompetencyEvaluator.AccountCanCraft(
                new List<string> { "Weaponsmith" }, 400, bestRating));
        }

        [Fact]
        public void AccountCanCraft_AnyOfMultipleDisciplinesQualifying_True()
        {
            // A recipe offering a choice of disciplines (rare but modeled)
            // is craftable if ANY one of them qualifies.
            var bestRating = new Dictionary<string, int> { { "Armorsmith", 500 } };
            Assert.True(CraftCompetencyEvaluator.AccountCanCraft(
                new List<string> { "Weaponsmith", "Armorsmith" }, 400, bestRating));
        }

        [Theory]
        [InlineData("MysticForge")]
        [InlineData("Achievement")]
        [InlineData("Merchant")]
        public void AccountCanCraft_NonLevelableTagOnly_InherentlyAvailable_True(string tag)
        {
            // No unlock/level concept for these facility/source tags - never
            // "blocked" regardless of known-and-empty account data.
            var bestRating = new Dictionary<string, int>();
            Assert.True(CraftCompetencyEvaluator.AccountCanCraft(
                new List<string> { tag }, 0, bestRating));
        }

        [Fact]
        public void AccountCanCraft_MixOfRealAndNonLevelableTag_RealDisciplineStillGates()
        {
            // A recipe declaring both a real discipline and a non-levelable
            // tag is not automatically inherently-available - the real
            // discipline still has to qualify.
            var bestRating = new Dictionary<string, int>();
            Assert.False(CraftCompetencyEvaluator.AccountCanCraft(
                new List<string> { "MysticForge", "Weaponsmith" }, 400, bestRating));
        }
    }
}
