using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// M37 (KNOWN-ISSUES #26, gw2e parity - achievement-bit ingredient
    /// dedup): echoes gw2efficiency's own two-part mechanism
    /// (initialTreeChecks + calculateTreeQuantity's achievement_bit check,
    /// docs/research/m37-r3-achievement-dedup.md Sections 1.1/1.2) for a
    /// small handful of real recipes (WvW "Infinite [siege weapon]
    /// Blueprint" achievement rewards) whose ingredients name a specific
    /// achievement "bit" - a one-time reward item that must never be
    /// counted twice just because it also happens to be needed directly
    /// elsewhere in the same plan.
    ///
    /// Exact rule (ported 1:1 from the ground-truth gw2e unit test quoted
    /// in the report, Section 1.4): walk the whole tree once to classify
    /// every non-Currency item id into "seen via an achievement-bit
    /// ingredient" and/or "seen via a plain ingredient" (the SAME id can be
    /// both, if it occurs both ways). Any id seen BOTH ways has every one of
    /// its achievement-bit occurrences zeroed, tree-wide - even the first
    /// one encountered. An id seen ONLY via achievement-bit occurrences
    /// keeps its first (DFS) occurrence and zeroes every later one. A plain
    /// ingredient with no achievement_bit at all - including an ordinary
    /// duplicate item id - is never touched by this pass; that is
    /// PlanSolver's own per-stepKey aggregation's job (Section 3.4 of the
    /// report), not this one's.
    ///
    /// Architectural note (Section 3.3/4.2 of the report): unlike gw2e's
    /// nested tree (which stores a small per-edge ratio and resolves every
    /// absolute quantity in one downstream pass), this module bakes each
    /// RecipeNode's absolute Quantity once, at tree-build time
    /// (RecipeService.BuildNodeAsync). Zeroing a duplicate occurrence here
    /// therefore also clears that occurrence's own Recipes - mirroring
    /// InventoryReducer.ReduceNode's identical "Quantity &lt;= 0 -&gt;
    /// Recipes.Clear()" treatment of a genuinely fully-owned node exactly -
    /// so PlanSolver.Evaluate has no craft path left to consider for it and
    /// the ordinary zero-quantity Buy/Have collapse (GetBuyCost(...,
    /// quantity: 0, ...) == 0 whenever a price exists, else UnknownSource)
    /// takes over cleanly. Without clearing Recipes, a duplicate occurrence
    /// with no TP/vendor price but a real craft recipe could still resolve
    /// to Craft (using its own, un-deduped children's costs) purely because
    /// nothing cheaper competed - re-introducing exactly the double count
    /// this pass exists to remove. This is a deliberate departure from
    /// literally "zeroing hits Quantity only" for that reason.
    ///
    /// Runs exactly ONCE, right after the tree is built - before inventory
    /// reduction and before Solve (see CraftingPlanPipeline) - and never
    /// again for that tree's lifetime, even across local override/Ignore
    /// re-solves (which reuse the same tree object). gw2e's own equivalent
    /// interactive-update path (updateTree.ts) does NOT re-run its
    /// classification pass and can let a "shared with a normal occurrence"
    /// dedup silently un-zero itself after a manual pill click (Section 1.5
    /// of the report, an upstream fragility) - running once and never again
    /// avoids that class of bug entirely, which is strictly safer than
    /// upstream, not a parity gap.
    ///
    /// Pure, Blish-free, no I/O - mutates the passed-in tree in place (the
    /// same seam OwnedMaterialsForceBuyPrePass occupies conceptually,
    /// though this pass needs no NodeIds and no throwaway solve).
    /// </summary>
    public static class AchievementBitDedupPrePass
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
        /// walks every node (root and descendants, every recipe option's
        /// ingredients - not just the eventually-chosen one, since
        /// PlanSolver.Evaluate itself evaluates every option for cost
        /// comparison) and classifies each "Item"-type node's id into
        /// bitItemIds (its OWN AchievementBit is set) or normalItemIds
        /// (else). Currency-type nodes and the synthetic multi-item wrapper
        /// sentinel are skipped entirely (never real GW2 items - see
        /// Gw2Constants.MultiItemWrapperItemId's own doc comment), matching
        /// upstream's explicit Currency exclusion, but their descendants
        /// (the wrapper's own N real item roots) are still walked normally.
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

            foreach (var option in node.Recipes)
            {
                foreach (var ingredient in option.Ingredients)
                {
                    CollectItemIdsForDedup(ingredient, bitItemIds, normalItemIds);
                }
            }
        }

        /// <summary>
        /// Mirrors gw2e's calculateTreeQuantityInner's achievement_bit
        /// check, walked in the same node.Recipes[i].Ingredients order
        /// PlanSolver.Evaluate/Collect will later use. For a node whose own
        /// AchievementBit is set: if its id is already in
        /// <paramref name="seenBitItemIds"/> (pre-seeded above, or pushed by
        /// an earlier achievement-bit occurrence of the same id seen
        /// earlier in THIS walk), zero this occurrence (Quantity, Recipes -
        /// see this class's own doc comment for why Recipes is cleared too
        /// - and the new IsAchievementBitDeduped flag) and stop descending
        /// into it: everything below an already-zeroed occurrence is dead
        /// weight the ordinary zero-quantity path already hides, and (per
        /// the verified 7-recipe/28-ingredient dataset this pass targets)
        /// never itself contains a further achievement-bit id that would
        /// need independent zeroing. Otherwise, this is the first
        /// occurrence of this id in the walk - record it as seen and keep
        /// walking normally.
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

            foreach (var option in node.Recipes)
            {
                foreach (var ingredient in option.Ingredients)
                {
                    ZeroDuplicateBitOccurrences(ingredient, seenBitItemIds);
                }
            }
        }
    }
}
