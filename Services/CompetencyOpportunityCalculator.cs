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
    /// where the numerically cheapest raw craft recipe overall is untrained
    /// (CraftingTreeNode.CheapestCraftUntrained) and the plan's actual
    /// committed cost is genuinely higher than that cheap recipe's real
    /// cost. Without this, the competency flip silently raised the plan's
    /// cost with no user-visible explanation anywhere - the CRAFT pill (if
    /// it wins at all) is not subdued (it is CHEAPER-than-what-it-should-
    /// have-been, not more expensive than the committed source, so neither
    /// PillSubduingRule fires) and gets no tooltip either.
    ///
    /// Adversarial-review round-2 fix (finding #5): reads
    /// CheapestCraftUntrained/CheapestCraftRealCost instead of the
    /// narrower CraftExcludedByCompetency/CraftExcludedRealCost pair (which
    /// only ever populate when NO option in EITHER tier is competent) - see
    /// CheapestCraftUntrained's own doc comment for the two additional
    /// shapes this now also catches. The delta check below (SubtreeCost
    /// strictly greater than the cheap recipe's real cost) subsumes the
    /// old explicit "node.Decision != Craft" guard: a manual override to
    /// Craft, or an automatic pick that lands on that SAME cheap recipe,
    /// always makes SubtreeCost equal CheapestCraftRealCost exactly (delta
    /// 0, naturally excluded) - while an automatic pick that lands on a
    /// DIFFERENT, costlier competent recipe (Decision == Craft, but not
    /// the cheap one) now correctly still reports the gap.
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
                    MinRating = node.CheapestCraftMinRating
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
