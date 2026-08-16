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
    /// into a per-occurrence list.
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
                Walk(result.CraftingTree, excessByItemId);
            }

            if (result.MultiItemRoots != null)
            {
                foreach (var root in result.MultiItemRoots)
                {
                    Walk(root, excessByItemId);
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
        // CraftingTreeNode.CraftsNeeded's own doc comment); every node,
        // Craft or not, still has its Children walked so a Craft node
        // nested arbitrarily deep beneath a Buy/Have/Currency ancestor is
        // never skipped.
        private static void Walk(CraftingTreeNode node, Dictionary<int, int> excessByItemId)
        {
            if (node == null)
            {
                return;
            }

            if (node.Decision == CraftingDecision.Craft &&
                node.CraftsNeeded.HasValue && node.RecipeOutputCount.HasValue)
            {
                int produced = node.CraftsNeeded.Value * node.RecipeOutputCount.Value;
                int excess = produced - node.Quantity;
                if (excess > 0)
                {
                    excessByItemId[node.ItemId] = excessByItemId.TryGetValue(node.ItemId, out var existing)
                        ? existing + excess
                        : excess;
                }
            }

            foreach (var child in node.Children)
            {
                Walk(child, excessByItemId);
            }
        }
    }
}
