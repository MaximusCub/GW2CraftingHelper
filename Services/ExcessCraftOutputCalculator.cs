using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure, Blish-free post-solve annotation pass: walks the display
    /// tree and aggregates every Craft occurrence's positive
    /// (CraftsNeeded * RecipeExpectedOutputCount - Quantity) surplus,
    /// grouped by ItemId.
    ///
    /// Aggregation is deliberately by ItemId across occurrences, not per
    /// node: surpluses are equally sellable/fungible, so do not "fix"
    /// this into a per-occurrence list. (The Crafting Steps section still
    /// merges the same occurrences into one "Craft Nx" step, which can
    /// read as contradicting the excess figure; both are correct.)
    ///
    /// Writes only CraftingPlanResult.ExcessCraftOutputs - never fed back
    /// into any decision or total.
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

            // SellSideEconomics already raises the root item's
            // sellableQuantity to the same EV surplus, so without this
            // exclusion the walk would advertise the same coins twice
            // under a different label. Exclude every requested root id.
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

        // Pre-order walk. A node contributes only when it is a Craft
        // decision with both batch-shape fields populated and is not
        // beneath a reference branch; every node still has its Children
        // walked so a deep Craft node is never skipped.
        //
        // insideReferenceBranch: a reference branch's hypothetical
        // children carry real solver decisions (often Craft) even though
        // nothing there is actually crafted; propagated as-is to every
        // descendant, never reset.
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
                // Recover "produced" on the same basis CraftsNeeded was
                // derived from (RecipeExpectedOutputCount), falling back
                // to RecipeOutputCount only for old trees/fixtures that
                // never set the field. Integer-yield recipes take a plain
                // integer fast path so the excess figure is never exposed
                // to floating-point representation error; only genuine
                // fractional-EV recipes take the double path.
                int excess;
                if (node.RecipeExpectedOutputCount.HasValue &&
                    node.RecipeExpectedOutputCount.Value == node.RecipeOutputCount.Value)
                {
                    excess = (node.CraftsNeeded.Value * node.RecipeOutputCount.Value) - node.Quantity;
                }
                else
                {
                    double basis = node.RecipeExpectedOutputCount.HasValue && node.RecipeExpectedOutputCount.Value > 0
                        ? node.RecipeExpectedOutputCount.Value
                        : node.RecipeOutputCount.Value;
                    double producedEv = node.CraftsNeeded.Value * basis;
                    excess = (int)Math.Floor(producedEv - node.Quantity);
                }

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
