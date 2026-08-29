using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Achievement-bit ingredient dedup (KNOWN-ISSUES #26, gw2e parity): a
    /// few real recipes name an achievement "bit" as an ingredient - a
    /// one-time reward item that must never be counted twice because it is
    /// also needed directly elsewhere in the same plan.
    /// <para>
    /// Exact rule, ported 1:1 from the ground-truth gw2e unit test: classify
    /// every non-Currency item id as seen via an achievement-bit ingredient,
    /// via a plain ingredient, or both. An id seen BOTH ways has every
    /// achievement-bit occurrence zeroed tree-wide, the first included; an
    /// id seen ONLY that way keeps its first (DFS) occurrence and zeroes the
    /// rest; a plain ingredient with no achievement_bit is never touched
    /// here - that is PlanSolver's own per-stepKey aggregation's job.
    /// </para>
    /// <para>
    /// Runs exactly ONCE, after the tree is built and before inventory
    /// reduction and Solve, never again for that tree. Pure, Blish-free,
    /// mutates the tree in place. Derivation: docs/ARCHITECTURE.md section 8.1.
    /// </para>
    /// </summary>
    internal static class AchievementBitDedupPrePass
    {
        public static void Apply(RecipeNode tree)
        {
            if (tree == null)
            {
                return;
            }

            var bitItemIds = new HashSet<int>();
            var normalItemIds = new HashSet<int>();
            CollectItemIdsForDedup(tree, bitItemIds, normalItemIds);

            // Pre-seed: any id present via BOTH an achievement-bit
            // occurrence and a plain occurrence anywhere in the tree must
            // have every one of its achievement-bit occurrences zeroed -
            // including the very first one the walk below encounters
            // (ground-truth test's id 55: "Bit exists as real item
            // elsewhere, zeroed" applies even to the top-level occurrence).
            var seenBitItemIds = new HashSet<int>();
            foreach (var id in bitItemIds)
            {
                if (normalItemIds.Contains(id))
                {
                    seenBitItemIds.Add(id);
                }
            }

            ZeroDuplicateBitOccurrences(tree, seenBitItemIds);
        }

        /// <summary>
        /// Mirrors gw2e's collectItemDataForIgnoringBits/initialTreeChecks:
        /// walks the tree and classifies each "Item"-type node's id into
        /// bitItemIds (its OWN AchievementBit is set) or normalItemIds.
        /// Currency-type nodes and the synthetic multi-item wrapper
        /// sentinel are skipped entirely - neither is ever a real GW2 item,
        /// matching upstream's explicit Currency exclusion - but their
        /// descendants are still walked normally.
        /// <para>
        /// Only descends through each node's PRIMARY option
        /// (node.Recipes[0]), the precedent
        /// InventoryReducer.ReduceNodeSourced sets for the identical
        /// ambiguity: PlanSolver has not run at pre-pass time, so which
        /// alternate RecipeOption it will choose is unknowable here.
        /// Derivation: docs/ARCHITECTURE.md section 8.1.
        /// </para>
        /// </summary>
        private static void CollectItemIdsForDedup(
            RecipeNode node, HashSet<int> bitItemIds, HashSet<int> normalItemIds)
        {
            if (node.IngredientType == "Item" && node.Id != Gw2Constants.MultiItemWrapperItemId)
            {
                if (node.AchievementBit.HasValue)
                {
                    bitItemIds.Add(node.Id);
                }
                else
                {
                    normalItemIds.Add(node.Id);
                }
            }

            if (node.Recipes.Count > 0)
            {
                foreach (var ingredient in node.Recipes[0].Ingredients)
                {
                    CollectItemIdsForDedup(ingredient, bitItemIds, normalItemIds);
                }
            }
        }

        /// <summary>
        /// Mirrors gw2e's calculateTreeQuantityInner achievement_bit check.
        /// For a node whose own AchievementBit is set: if its id is already
        /// in <paramref name="seenBitItemIds"/>, zero this occurrence
        /// (Quantity, Recipes - see the class doc comment for why Recipes
        /// goes too - and IsAchievementBitDeduped) and stop descending;
        /// otherwise record it as seen and keep walking.
        /// <para>
        /// Only descends through each node's PRIMARY option
        /// (node.Recipes[0]), for the reason CollectItemIdsForDedup's doc
        /// comment gives, and critically so here: zeroing an occurrence in
        /// an option PlanSolver never chooses would discard that option's
        /// true cost from Evaluate's comparison and can make a worse option
        /// look artificially cheap enough to be picked.
        /// Derivation: docs/ARCHITECTURE.md section 8.1.
        /// </para>
        /// </summary>
        private static void ZeroDuplicateBitOccurrences(
            RecipeNode node, HashSet<int> seenBitItemIds)
        {
            if (node.IngredientType == "Item" &&
                node.Id != Gw2Constants.MultiItemWrapperItemId &&
                node.AchievementBit.HasValue)
            {
                if (seenBitItemIds.Contains(node.Id))
                {
                    node.Quantity = 0;
                    node.IsAchievementBitDeduped = true;
                    node.Recipes.Clear();
                    return;
                }

                seenBitItemIds.Add(node.Id);
            }

            if (node.Recipes.Count > 0)
            {
                foreach (var ingredient in node.Recipes[0].Ingredients)
                {
                    ZeroDuplicateBitOccurrences(ingredient, seenBitItemIds);
                }
            }
        }
    }
}
