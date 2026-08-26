using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure, Blish-free post-solve annotation pass: walks the display
    /// tree for nodes where the numerically cheapest raw craft recipe is
    /// untrained (CraftingTreeNode.CheapestCraftUntrained) and the plan's
    /// committed cost is higher than that recipe's real cost - the
    /// competency flip would otherwise raise the plan's cost with no
    /// user-visible explanation (neither pill-subduing rule fires). The
    /// delta check (SubtreeCost strictly greater) naturally excludes a
    /// pick that lands on the cheap recipe itself, while still reporting
    /// an automatic pick that lands on a costlier competent recipe.
    ///
    /// Writes only CraftingPlanResult.CompetencyOpportunities - never fed
    /// back into any decision or total.
    /// </summary>
    internal static class CompetencyOpportunityCalculator
    {
        internal static void Apply(CraftingPlanResult result)
        {
            if (result == null)
            {
                return;
            }

            var byItemId = new Dictionary<int, CompetencyOpportunity>();

            if (result.CraftingTree != null)
            {
                Walk(result.CraftingTree, byItemId, insideReferenceBranch: false);
            }

            if (result.MultiItemRoots != null)
            {
                foreach (var root in result.MultiItemRoots)
                {
                    Walk(root, byItemId, insideReferenceBranch: false);
                }
            }

            result.CompetencyOpportunities = new List<CompetencyOpportunity>(byItemId.Values);
        }

        // Pre-order walk mirroring ExcessCraftOutputCalculator.Walk: a
        // synthesized IsReferenceBranch subtree never contributes (nothing
        // in it is a real decision). First occurrence per ItemId wins - a
        // second entry would only be near-duplicate noise.
        private static void Walk(
            CraftingTreeNode node, Dictionary<int, CompetencyOpportunity> byItemId, bool insideReferenceBranch)
        {
            if (node == null)
            {
                return;
            }

            if (!insideReferenceBranch &&
                node.CheapestCraftUntrained &&
                node.CheapestCraftRealCost.HasValue &&
                node.SubtreeCost.HasValue &&
                node.SubtreeCost.Value > node.CheapestCraftRealCost.Value &&
                !byItemId.ContainsKey(node.ItemId))
            {
                byItemId[node.ItemId] = new CompetencyOpportunity
                {
                    ItemId = node.ItemId,
                    CraftCost = node.CheapestCraftRealCost.Value,
                    DeltaCost = node.SubtreeCost.Value - node.CheapestCraftRealCost.Value,
                    Disciplines = node.CheapestCraftDisciplines,
                    MinRating = node.CheapestCraftMinRating,
                };
            }

            bool childInsideReferenceBranch = insideReferenceBranch || node.IsReferenceBranch;
            foreach (var child in node.Children)
            {
                Walk(child, byItemId, childInsideReferenceBranch);
            }
        }
    }
}
