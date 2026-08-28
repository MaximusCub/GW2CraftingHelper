using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// (#7, source-selection-simplification
    /// design-law gap) - direct unit tests on
    /// CompetencyOpportunityCalculator's pure tree-walk aggregation, same
    /// "plain CraftingTreeNode fixtures, no solver/pipeline round-trip
    /// needed" precedent ExcessCraftOutputCalculatorTests already
    /// established.
    /// </summary>
    public class CompetencyOpportunityCalculatorTests
    {
        private static CraftingTreeNode ExcludedNode(
            int itemId, CraftingDecision decision, long? subtreeCost, long? cheapestCraftRealCost,
            IReadOnlyList<string> disciplines = null, int minRating = 400,
            bool cheapestCraftUntrained = true, bool isReferenceBranch = false,
            params CraftingTreeNode[] children)
        {
            return new CraftingTreeNode
            {
                ItemId = itemId,
                Decision = decision,
                SubtreeCost = subtreeCost,
                CheapestCraftUntrained = cheapestCraftUntrained,
                CheapestCraftRealCost = cheapestCraftRealCost,
                CheapestCraftDisciplines = disciplines ?? new List<string> { "Weaponsmith" },
                CheapestCraftMinRating = minRating,
                IsReferenceBranch = isReferenceBranch,
                Children = children,
            };
        }

        private static CraftingTreeNode WrapAsRoot(int wrapperItemId, params CraftingTreeNode[] children)
        {
            return new CraftingTreeNode
            {
                ItemId = wrapperItemId,
                Decision = CraftingDecision.BuyFromTp,
                Children = children,
            };
        }

        [Fact]
        public void ExcludedNode_CraftWouldHaveBeenCheaper_EmitsOpportunity()
        {
            var node = ExcludedNode(
                itemId: 2, decision: CraftingDecision.BuyFromTp,
                subtreeCost: 1000, cheapestCraftRealCost: 60);
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
        public void CraftUsingTheCheapestUntrainedRecipeItself_NotReported_NoDelta()
        {
            // CheapestCraftUntrained
            // can still be true even when the COMMITTED decision is Craft
            // (a manual override, or an automatic pick that lands on this
            // SAME cheap recipe because competency is unknown/irrelevant
            // to it) - SubtreeCost equals CheapestCraftRealCost exactly in
            // that case, so the delta-based check naturally reports
            // nothing, without needing an explicit "Decision != Craft"
            // guard.
            var node = ExcludedNode(
                itemId: 2, decision: CraftingDecision.Craft,
                subtreeCost: 60, cheapestCraftRealCost: 60);
            var root = WrapAsRoot(1, node);
            var result = new CraftingPlanResult { CraftingTree = root };

            CompetencyOpportunityCalculator.Apply(result);

            Assert.Empty(result.CompetencyOpportunities);
        }

        [Fact]
        public void CraftUsingACostlierCompetentSiblingRecipe_StillReported()
        {
            // Regression: a
            // costlier COMPETENT sibling recipe won Craft (Decision ==
            // Craft) over the cheaper untrained one - the plan still
            // crafts, so the old "Decision != Craft -> nothing to report"
            // guard would have silently suppressed this, even though the
            // user never got the cheap recipe and genuinely could save
            // more by training the cheap one's discipline instead.
            var node = ExcludedNode(
                itemId: 2, decision: CraftingDecision.Craft,
                subtreeCost: 100, cheapestCraftRealCost: 60);
            var root = WrapAsRoot(1, node);
            var result = new CraftingPlanResult { CraftingTree = root };

            CompetencyOpportunityCalculator.Apply(result);

            var opp = Assert.Single(result.CompetencyOpportunities);
            Assert.Equal(2, opp.ItemId);
            Assert.Equal(60, opp.CraftCost);
            Assert.Equal(40, opp.DeltaCost);
        }

        [Fact]
        public void CheapestCraftNotUntrained_NotReported()
        {
            var node = ExcludedNode(
                itemId: 2, decision: CraftingDecision.BuyFromTp,
                subtreeCost: 1000, cheapestCraftRealCost: 60,
                cheapestCraftUntrained: false);
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
                subtreeCost: 60, cheapestCraftRealCost: 1000);
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
                subtreeCost: 1000, cheapestCraftRealCost: 60);
            var referenceRoot = new CraftingTreeNode
            {
                ItemId = 3,
                Decision = CraftingDecision.BuyFromTp,
                IsReferenceBranch = true,
                Children = new[] { node },
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
                subtreeCost: 1000, cheapestCraftRealCost: 60);
            var nodeB = ExcludedNode(
                itemId: 2, decision: CraftingDecision.BuyFromTp,
                subtreeCost: 500, cheapestCraftRealCost: 30);
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
