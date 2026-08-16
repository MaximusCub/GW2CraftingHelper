using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// design-plan-notes.md (Notes section, excess/reclaim): pure,
    /// Blish-free post-solve annotation pass, moved-out-for-testability
    /// shape/placement precedent identical to SellSideEconomics - walks the
    /// already-built display tree (CraftingTreeResult.CraftingTree for a
    /// single-item plan, MultiItemRoots for a batch) and aggregates every
    /// Decision == Craft occurrence's positive (CraftsNeeded *
    /// RecipeOutputCount - Quantity) surplus, grouped by ItemId.
    ///
    /// Aggregation is deliberately by ItemId across every tree occurrence,
    /// not per tree node: a shared sub-ingredient crafted in two unrelated
    /// branches with independent rounding reports one merged excess figure.
    /// This is correct for the reclaim-value math (both surpluses are
    /// equally sellable/fungible on the Trading Post) - do not "fix" this
    /// into a per-occurrence list. Nice-to-have callout: the Crafting Steps
    /// section still MERGES those same occurrences into a single "Craft
    /// Nx" step (e.g. 10x from two branches each needing 5), which alone
    /// implies zero waste - a user reconciling that section against this
    /// one's "Excess: 2x" can read the two as contradictory even though
    /// both are correct; only this doc comment names the tension today.
    ///
    /// Writes only CraftingPlanResult.ExcessCraftOutputs. Never mutates
    /// Plan, Plan.TotalCoinCost, result.NetSaleValue, or
    /// result.CraftingProfit - same "cosmetic display data only... never
    /// fed back into any decision or total" contract SellSideEconomics'
    /// own doc comment establishes for OwnedCurrencyAmounts/
    /// OwnedVendorItemAmounts.
    /// </summary>
    internal static class ExcessCraftOutputCalculator
    {
        internal static void Apply(
            CraftingPlanResult result,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            if (result == null)
            {
                return;
            }

            var excessByItemId = new Dictionary<int, int>();

            if (result.CraftingTree != null)
            {
                Walk(result.CraftingTree, excessByItemId, insideReferenceBranch: false);
            }

            if (result.MultiItemRoots != null)
            {
                foreach (var root in result.MultiItemRoots)
                {
                    Walk(root, excessByItemId, insideReferenceBranch: false);
                }
            }

            // Review fix (finding 6, MEASURED): SellSideEconomics.
            // ComputePerItemEconomics already raises the ROOT item's own
            // sellableQuantity (and therefore NetSaleValue/the Profit tile)
            // to CraftsNeeded * OutputCount whenever the root recipe
            // over-produces - the Total Cost section already advertises
            // this exact surplus. The display-tree root is also a Craft
            // node, so without this exclusion the walk above would emit
            // the SAME surplus units again here, double-advertising the
            // same coins under a different label. Exclude every requested
            // root item id (single-item CraftingTree, or each MultiItemRoots
            // entry) from the Notes list entirely - their over-production is
            // already visible elsewhere.
            if (result.CraftingTree != null)
            {
                excessByItemId.Remove(result.CraftingTree.ItemId);
            }

            if (result.MultiItemRoots != null)
            {
                foreach (var root in result.MultiItemRoots)
                {
                    if (root != null)
                    {
                        excessByItemId.Remove(root.ItemId);
                    }
                }
            }

            var outputs = new List<ExcessCraftOutput>(excessByItemId.Count);
            foreach (var kvp in excessByItemId)
            {
                int itemId = kvp.Key;
                int excessQuantity = kvp.Value;

                bool isAccountBound = metadata != null &&
                    metadata.TryGetValue(itemId, out var meta) && meta.IsAccountBound;

                // Account-bound items can never be sold on the Trading
                // Post, regardless of what price data exists for them - the
                // surplus is stranded, not reclaimable.
                long? reclaimValue = null;
                if (!isAccountBound && prices != null &&
                    prices.TryGetValue(itemId, out var price) && price.SellInstant > 0)
                {
                    reclaimValue = TradingPostMath.NetSaleRevenue(price.SellInstant, excessQuantity);
                }

                outputs.Add(new ExcessCraftOutput
                {
                    ItemId = itemId,
                    ExcessQuantity = excessQuantity,
                    ReclaimValue = reclaimValue,
                    IsAccountBound = isAccountBound
                });
            }

            result.ExcessCraftOutputs = outputs;
        }

        // Recursive pre-order walk. A node contributes only when it is
        // itself a Craft decision with both batch-shape fields populated
        // (CraftingTreeBuilder only sets them for Decision == Craft - see
        // CraftingTreeNode.CraftsNeeded's own doc comment) AND is not
        // beneath a reference branch (finding 2, below); every node,
        // Craft or not, still has its Children walked so a Craft node
        // nested arbitrarily deep beneath a Buy/Have/Currency ancestor is
        // never skipped.
        //
        // insideReferenceBranch (review fix, finding 2, MEASURED): true
        // once the walk has passed a node with IsReferenceBranch == true -
        // CraftingTreeBuilder.BuildNode synthesizes gw2e's "what it would
        // cost to craft instead" hypothetical subtree under a BuyFromTp/
        // BuyFromVendor node that also has a recipe, and those hypothetical
        // children carry real solver decisions (often Craft) plus their own
        // CraftsNeeded/RecipeOutputCount, even though nothing in that
        // subtree is actually crafted. Propagated as-is to every descendant
        // (never reset back to false), mirroring CraftingTreeBuilder's own
        // "propagate insideReferenceBranch as-is" precedent for the same
        // reason: a Craft decision reached while already inside a
        // reference branch is still hypothetical content.
        private static void Walk(
            CraftingTreeNode node, Dictionary<int, int> excessByItemId, bool insideReferenceBranch)
        {
            if (node == null)
            {
                return;
            }

            if (!insideReferenceBranch &&
                node.Decision == CraftingDecision.Craft &&
                node.CraftsNeeded.HasValue && node.RecipeOutputCount.HasValue)
            {
                // Review fix (finding 1, MEASURED): recover "produced" on
                // the SAME basis CraftsNeeded was derived from
                // (RecipeExpectedOutputCount), not the nominal
                // RecipeOutputCount - see CraftingTreeNode.
                // RecipeExpectedOutputCount's own doc comment. Falls back to
                // RecipeOutputCount only for a pre-existing tree/fixture
                // that never set the new field (old plan.json, direct test
                // fixtures) - a real Craft node always has one of the two
                // populated together with CraftsNeeded.
                double basis = node.RecipeExpectedOutputCount.HasValue && node.RecipeExpectedOutputCount.Value > 0
                    ? node.RecipeExpectedOutputCount.Value
                    : node.RecipeOutputCount.Value;
                double producedEv = node.CraftsNeeded.Value * basis;
                int excess = (int)Math.Floor(producedEv - node.Quantity);
                if (excess > 0)
                {
                    excessByItemId[node.ItemId] = excessByItemId.TryGetValue(node.ItemId, out var existing)
                        ? existing + excess
                        : excess;
                }
            }

            bool childInsideReferenceBranch = insideReferenceBranch || node.IsReferenceBranch;
            foreach (var child in node.Children)
            {
                Walk(child, excessByItemId, childInsideReferenceBranch);
            }
        }
    }
}
