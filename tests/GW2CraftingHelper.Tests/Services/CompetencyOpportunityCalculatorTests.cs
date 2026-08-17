using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Adversarial-review fix (#7, source-selection-simplification
    /// design-law gap) - direct unit tests on
    /// CompetencyOpportunityCalculator's pure tree-walk aggregation, same
    /// "plain CraftingTreeNode fixtures, no solver/pipeline round-trip
    /// needed" precedent ExcessCraftOutputCalculatorTests already
    /// established.
    /// </summary>
    public class CompetencyOpportunityCalculatorTests
    {
        private static CraftingTreeNode ExcludedNode(
            int itemId, CraftingDecision decision, long? subtreeCost, long? excludedRealCost,
            IReadOnlyList<string> disciplines = null, int minRating = 400,
            bool craftExcludedByCompetency = true, bool isReferenceBranch = false,
            params CraftingTreeNode[] children)
        {
            return new CraftingTreeNode
            {
                ItemId = itemId,
                Decision = decision,
                SubtreeCost = subtreeCost,
                CraftExcludedByCompetency = craftExcludedByCompetency,
                CraftExcludedRealCost = excludedRealCost,
                CraftExcludedDisciplines = disciplines ?? new List<string> { "Weaponsmith" },
                CraftExcludedMinRating = minRating,
                IsReferenceBranch = isReferenceBranch,
                Children = children
            };
        }

        private static CraftingTreeNode WrapAsRoot(int wrapperItemId, params CraftingTreeNode[] children)
        {
            return new CraftingTreeNode
            {
                ItemId = wrapperItemId,
                Decision = CraftingDecision.BuyFromTp,
                Children = children
            };
        }

        [Fact]
        public void ExcludedNode_CraftWouldHaveBeenCheaper_EmitsOpportunity()
        {
            var node = ExcludedNode(
                itemId: 2, decision: CraftingDecision.BuyFromTp,
                subtreeCost: 1000, excludedRealCost: 60);
            var root = WrapAsRoot(1, node);
            var result = new CraftingPlanResult { CraftingTree = root };

            CompetencyOpportunityCalculator.Apply(result);

            Assert.Single(result.CompetencyOpportunities);
            var opp = result.CompetencyOpportunities[0];
            Assert.Equal(2, opp.ItemId);
            Assert.Equal(60, opp.CraftCost);
            Assert.Equal(940, opp.DeltaCost);
            Assert.Equal("Weaponsmith", Assert.Single(opp.Disciplines));
            Assert.Equal(400, opp.MinRating);
        }

        [Fact]
        public void ManualOverrideToCraft_NotReported_UserAlreadyChoseIt()
        {
            // CraftExcludedByCompetency can still be true even when the
            // COMMITTED decision is Craft (a manual override always wins
            // over the automatic exclusion) - nothing to report, the user
            // already made the tradeoff explicitly.
            var node = ExcludedNode(
                itemId: 2, decision: CraftingDecision.Craft,
                subtreeCost: 60, excludedRealCost: 60);
            var root = WrapAsRoot(1, node);
            var result = new CraftingPlanResult { CraftingTree = root };

            CompetencyOpportunityCalculator.Apply(result);

            Assert.Empty(result.CompetencyOpportunities);
        }

        [Fact]
        public void NotExcludedByCompetency_NotReported()
        {
            var node = ExcludedNode(
                itemId: 2, decision: CraftingDecision.BuyFromTp,
                subtreeCost: 1000, excludedRealCost: 60,
                craftExcludedByCompetency: false);
            var root = WrapAsRoot(1, node);
            var result = new CraftingPlanResult { CraftingTree = root };

            CompetencyOpportunityCalculator.Apply(result);

            Assert.Empty(result.CompetencyOpportunities);
        }

        [Fact]
        public void CraftWouldNotHaveBeenCheaper_NotReported()
        {
            // Defensive: craft's own real cost was not actually lower than
            // the committed source - nothing genuinely lost here.
            var node = ExcludedNode(
                itemId: 2, decision: CraftingDecision.BuyFromTp,
                subtreeCost: 60, excludedRealCost: 1000);
            var root = WrapAsRoot(1, node);
            var result = new CraftingPlanResult { CraftingTree = root };

            CompetencyOpportunityCalculator.Apply(result);

            Assert.Empty(result.CompetencyOpportunities);
        }

        [Fact]
        public void InsideReferenceBranch_NotReported()
        {
            // A synthesized "what it would cost to craft instead"
            // hypothetical subtree carries real-looking solver decisions
            // but represents nothing actually in the plan.
            var node = ExcludedNode(
                itemId: 2, decision: CraftingDecision.BuyFromTp,
                subtreeCost: 1000, excludedRealCost: 60);
            var referenceRoot = new CraftingTreeNode
            {
                ItemId = 3,
                Decision = CraftingDecision.BuyFromTp,
                IsReferenceBranch = true,
                Children = new[] { node }
            };
            var root = WrapAsRoot(1, referenceRoot);
            var result = new CraftingPlanResult { CraftingTree = root };

            CompetencyOpportunityCalculator.Apply(result);

            Assert.Empty(result.CompetencyOpportunities);
        }

        [Fact]
        public void SameItemAtTwoOccurrences_DedupedToOneEntry()
        {
            var nodeA = ExcludedNode(
                itemId: 2, decision: CraftingDecision.BuyFromTp,
                subtreeCost: 1000, excludedRealCost: 60);
            var nodeB = ExcludedNode(
                itemId: 2, decision: CraftingDecision.BuyFromTp,
                subtreeCost: 500, excludedRealCost: 30);
            var root = WrapAsRoot(1, nodeA, nodeB);
            var result = new CraftingPlanResult { CraftingTree = root };

            CompetencyOpportunityCalculator.Apply(result);

            Assert.Single(result.CompetencyOpportunities);
            // First (pre-order DFS) occurrence wins.
            Assert.Equal(940, result.CompetencyOpportunities[0].DeltaCost);
        }

        [Fact]
        public void NullResult_NoOp()
        {
            CompetencyOpportunityCalculator.Apply(null);
        }

        [Fact]
        public void EmptyTree_EmptyList()
        {
            var result = new CraftingPlanResult();

            CompetencyOpportunityCalculator.Apply(result);

            Assert.NotNull(result.CompetencyOpportunities);
            Assert.Empty(result.CompetencyOpportunities);
        }
    }
}
