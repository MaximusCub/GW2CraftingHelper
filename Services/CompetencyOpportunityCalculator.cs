using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Adversarial-review fix (#7, source-selection-simplification design-
    /// law gap): pure, Blish-free post-solve annotation pass, same shape/
    /// placement precedent as ExcessCraftOutputCalculator - walks the
    /// already-built display tree (CraftingTreeResult.CraftingTree for a
    /// single-item plan, MultiItemRoots for a batch) looking for a node
    /// where CraftCompetencyEvaluator excluded craft from the AUTOMATIC
    /// pick (CraftingTreeNode.CraftExcludedByCompetency), the node did NOT
    /// end up crafted anyway (a manual override can still choose Craft
    /// despite the flag - nothing to report there, the user already made
    /// the tradeoff explicitly), and crafting would genuinely have been
    /// cheaper than what the plan actually committed to. Without this, the
    /// competency flip silently raised the plan's cost with no user-
    /// visible explanation anywhere - the CRAFT pill is not subdued (it is
    /// CHEAPER, not more expensive, so neither PillSubduingRule fires) and
    /// gets no tooltip either.
    ///
    /// Writes only CraftingPlanResult.CompetencyOpportunities. Never
    /// mutates Plan, Plan.TotalCoinCost, or any other displayed total -
    /// same "cosmetic display data only... never fed back into any
    /// decision or total" contract ExcessCraftOutputCalculator's own doc
    /// comment establishes.
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

        // Recursive pre-order walk, mirroring ExcessCraftOutputCalculator.
        // Walk's own precedent exactly (insideReferenceBranch propagated
        // as-is, every node's Children still walked regardless of whether
        // THIS node itself contributes) - see that method's own doc
        // comment for why a synthesized "what it would cost to craft
        // instead" hypothetical subtree (IsReferenceBranch) must never
        // contribute here either: nothing in it is a real decision.
        //
        // First occurrence per ItemId wins (dictionary insertion order,
        // deterministic pre-order DFS) - the same tree occurrence's own
        // recipe/discipline requirement is virtually always identical at
        // every position a shared item appears, so a second entry would
        // only ever be near-duplicate noise, not new information.
        private static void Walk(
            CraftingTreeNode node, Dictionary<int, CompetencyOpportunity> byItemId, bool insideReferenceBranch)
        {
            if (node == null)
            {
                return;
            }

            if (!insideReferenceBranch &&
                node.CraftExcludedByCompetency &&
                node.Decision != CraftingDecision.Craft &&
                node.CraftExcludedRealCost.HasValue &&
                node.SubtreeCost.HasValue &&
                node.SubtreeCost.Value > node.CraftExcludedRealCost.Value &&
                !byItemId.ContainsKey(node.ItemId))
            {
                byItemId[node.ItemId] = new CompetencyOpportunity
                {
                    ItemId = node.ItemId,
                    CraftCost = node.CraftExcludedRealCost.Value,
                    DeltaCost = node.SubtreeCost.Value - node.CraftExcludedRealCost.Value,
                    Disciplines = node.CraftExcludedDisciplines,
                    MinRating = node.CraftExcludedMinRating
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
