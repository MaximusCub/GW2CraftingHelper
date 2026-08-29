using System;
using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Sell-side economics arithmetic (SellableQuantity/NetSaleValue/
    /// TargetUnitSellPrice/CraftingProfit/MaterialOpportunityCost), moved
    /// out of CraftingPlanPipeline as a pure,
    /// move-only extraction - same fields, same order, same arithmetic. See
    /// docs/KNOWN-ISSUES #25 for the full design rationale this
    /// class implements (single-item vs batch rollup, its documented
    /// divergences from gw2e's own multi-item economics). Blish-free and
    /// directly unit-testable; CraftingPlanPipeline calls these statics in
    /// place of the methods it used to own.
    /// </summary>
    internal static class SellSideEconomics
    {
        internal static void ApplySellSideEconomics(
            CraftingPlanResult result,
            RecipeNode treeUsedForSolve,
            SolveResult solveResult,
            IReadOnlyDictionary<int, ItemPrice> prices,
            int targetItemId,
            int quantity,
            PriceBasis priceBasis,
            List<UsedMaterial> usedMaterials,
            OwnMaterialsMode ownMaterialsMode)
        {
            // Sell-side economics: what the crafted quantity nets after TP
            // fees, and profit versus the plan's coin cost. Coin-only by
            // design - non-coin currency costs have no coin value here.
            //
            // The per-root arithmetic (over-
            // production bump, sell-price lookup) is now shared with the
            // batch path via ComputePerItemEconomics - a pure extraction,
            // not a behavior change (see that method's own doc comment).
            // This method's own output is unchanged: it still writes the
            // SAME fields in the SAME order using solveResult.Plan.
            // TotalCoinCost (the whole, single-item plan's cost - there is
            // only one root here) rather than ComputePerItemEconomics'
            // ItemCraftCost (a batch-only concept this call site never
            // reads).
            result.PriceBasis = priceBasis;

            var itemEconomics = ComputePerItemEconomics(
                treeUsedForSolve, targetItemId, quantity, solveResult, prices);
            result.SellableQuantity = itemEconomics.SellableQuantity;

            // Own-materials opportunity cost (gw2efficiency-style "value own
            // materials"): what selling the owned materials that inventory
            // reduction consumed would have netted after TP fees. In Valued
            // mode, reduction is
            // decision-aware (InventoryReducer.Reduce's zeroOwnedDecisions
            // guide, built by CraftingPlanPipeline's zero-owned solve),
            // so owned mats are consumed first at zero acquisition cost ONLY
            // along the branch a zero-owned baseline would actually choose
            // to craft - not along every node's primary recipe option
            // regardless of whether it is ever crafted, as before this
            // milestone. In Free mode (or with no zeroOwnedDecisions guide),
            // reduction is unchanged - still the legacy primary-option
            // heuristic.
            long? materialOpportunityCost = ComputeMaterialOpportunityCost(
                usedMaterials, prices, ownMaterialsMode);
            result.MaterialOpportunityCost = materialOpportunityCost;

            if (itemEconomics.NetSaleValue.HasValue)
            {
                result.TargetUnitSellPrice = itemEconomics.TargetUnitSellPrice;
                result.NetSaleValue = itemEconomics.NetSaleValue;
                long profit = result.NetSaleValue.Value - solveResult.Plan.TotalCoinCost;
                if (materialOpportunityCost.HasValue)
                {
                    profit -= materialOpportunityCost.Value;
                }

                result.CraftingProfit = profit;
            }
        }

        /// <summary>
        /// The calculator's single shape-dispatch entry point: routes to
        /// ApplySellSideEconomics (single-item) or
        /// ApplyBatchSellSideEconomics (multi-item) on the
        /// Gw2Constants.MultiItemWrapperItemId root sentinel. Both pipeline
        /// call sites (generation and ResolveWithOverrides) pass the tree
        /// actually solved, which may be a reduced clone - the sentinel
        /// check still holds only because InventoryReducer.CloneNode
        /// preserves Id and the wrapper root is never pruned. The check
        /// also agrees with the old generation-time `items == null` test
        /// only because the list overload routes single-entry lists to the
        /// single-item path (pinned by MultiItemPlanTests).
        /// targetItemId/quantity are consulted only by the single-item
        /// branch; requestedItems only by the batch branch. The
        /// `tree != null` guard is defensive only: both branches NRE on a
        /// null tree anyway, and no production caller passes one.
        /// </summary>
        internal static void ApplyForPlanShape(
            CraftingPlanResult result,
            RecipeNode tree,
            SolveResult solveResult,
            IReadOnlyDictionary<int, ItemPrice> prices,
            int targetItemId,
            int quantity,
            IReadOnlyList<PlanRequestItem> requestedItems,
            PriceBasis priceBasis,
            List<UsedMaterial> usedMaterials,
            OwnMaterialsMode ownMaterialsMode)
        {
            if (tree != null && tree.Id == Gw2Constants.MultiItemWrapperItemId)
            {
                ApplyBatchSellSideEconomics(
                    result, tree, solveResult, prices, requestedItems,
                    priceBasis, usedMaterials, ownMaterialsMode);
            }
            else
            {
                ApplySellSideEconomics(
                    result, tree, solveResult, prices, targetItemId, quantity,
                    priceBasis, usedMaterials, ownMaterialsMode);
            }
        }

        /// <summary>
        /// One requested root's own sell-side figures - the SellableQuantity/
        /// NetSaleValue/TargetUnitSellPrice arithmetic factored out of
        /// ApplySellSideEconomics so the single-item path and
        /// ApplyBatchSellSideEconomics share identical fee math and the same
        /// instant-sell revenue basis, with no parallel costing logic.
        ///
        /// ItemCraftCost is itemRoot's own SolverDecision.TotalCost - the
        /// post-correction, shared-vendor-batch-reconciled per-node real coin
        /// figure CraftingTreeBuilder also copies onto CraftingTreeNode.
        /// SubtreeCost - and 0 when the root has no decision entry at all. Only
        /// ApplyBatchSellSideEconomics reads it, to attribute each item's own
        /// fair share of a batch's (possibly materials-shared) total cost; the
        /// single-item path has its own equivalent in
        /// solveResult.Plan.TotalCoinCost.
        ///
        /// Why itemId is passed explicitly rather than read from itemRoot.Id:
        /// docs/ARCHITECTURE.md, "Services Q-Z: relocated design narrative".
        /// </summary>
        internal struct PerItemEconomics
        {
            public int SellableQuantity;
            public long? NetSaleValue;
            public long? TargetUnitSellPrice;
            public long ItemCraftCost;
        }

        internal static PerItemEconomics ComputePerItemEconomics(
            RecipeNode itemRoot,
            int itemId,
            int requestedQuantity,
            SolveResult solveResult,
            IReadOnlyDictionary<int, ItemPrice> prices)
        {
            // Revenue must cover what the batch actually PRODUCES: when the
            // chosen root recipe over-produces (OutputCount does not divide
            // the requested quantity), this root's own cost pays for the
            // whole batch, so the extra units are sellable too.
            //
            // "produced" must use the
            // SAME basis CraftsNeeded was derived from - ExpectedOutputCount
            // (EV), not the nominal OutputCount - exactly the finding-1 fix
            // ExcessCraftOutputCalculator.Walk already applies. For an
            // integer-yield recipe ExpectedOutputCount == OutputCount (a
            // no-op ratio of 1.0); only a Mystic-Clover-style fractional-EV
            // root recipe diverges, and using the nominal basis there was
            // inflating sellableQuantity (and therefore NetSaleValue/the
            // Profit tile - a real total, not an advisory one) far past
            // what the recipe actually expects to yield. Falls back to
            // OutputCount only when ExpectedOutputCount is unset (a
            // pre-existing tree/fixture that never populated it) - mirrors
            // ExcessCraftOutputCalculator.Walk's own fallback exactly.
            int sellableQuantity = requestedQuantity;
            long itemCraftCost = 0L;
            if (solveResult.Decisions.TryGetValue(itemRoot.NodeId, out var rootDecision))
            {
                itemCraftCost = rootDecision.TotalCost ?? 0L;
                if (rootDecision.Source == AcquisitionSource.Craft)
                {
                    var chosenRecipe = itemRoot.Recipes
                        .FirstOrDefault(r => r.RecipeId == rootDecision.RecipeId);
                    if (chosenRecipe != null && chosenRecipe.OutputCount > 0)
                    {
                        double basis = chosenRecipe.ExpectedOutputCount > 0
                            ? chosenRecipe.ExpectedOutputCount
                            : chosenRecipe.OutputCount;
                        int produced = (int)Math.Floor(chosenRecipe.CraftsNeeded * basis);
                        if (produced > sellableQuantity)
                        {
                            sellableQuantity = produced;
                        }
                    }
                }
            }

            long? netSaleValue = null;
            long? targetUnitSellPrice = null;
            if (prices.TryGetValue(itemId, out var itemPrice) && itemPrice.SellInstant > 0)
            {
                targetUnitSellPrice = itemPrice.SellInstant;
                netSaleValue = TradingPostMath.NetSaleRevenue(itemPrice.SellInstant, sellableQuantity);
            }

            return new PerItemEconomics
            {
                SellableQuantity = sellableQuantity,
                NetSaleValue = netSaleValue,
                TargetUnitSellPrice = targetUnitSellPrice,
                ItemCraftCost = itemCraftCost,
            };
        }

        /// <summary>
        /// Sum, over <paramref name="usedMaterials"/>, of the net TP sale
        /// value of the owned materials inventory reduction consumed -
        /// pure extraction of ApplySellSideEconomics' own-materials
        /// opportunity-cost arithmetic so
        /// ApplyBatchSellSideEconomics can reuse it unchanged over a
        /// batch's merged UsedMaterials list (already aggregated across
        /// every requested root by the shared InventoryReducer pool - no
        /// per-root split needed or meaningful here). Preserves
        /// CraftingPlanResult.MaterialOpportunityCost's own null-vs-zero
        /// contract exactly: null outside Valued mode or with nothing used;
        /// otherwise a sum where an unsellable material contributes 0
        /// rather than being excluded.
        /// </summary>
        internal static long? ComputeMaterialOpportunityCost(
            List<UsedMaterial> usedMaterials,
            IReadOnlyDictionary<int, ItemPrice> prices,
            OwnMaterialsMode ownMaterialsMode)
        {
            if (ownMaterialsMode != OwnMaterialsMode.Valued ||
                usedMaterials == null || usedMaterials.Count == 0)
            {
                return null;
            }

            long sum = 0;
            foreach (var used in usedMaterials)
            {
                if (prices.TryGetValue(used.ItemId, out var matPrice) &&
                    matPrice.SellInstant > 0)
                {
                    sum += TradingPostMath.NetSaleRevenue(matPrice.SellInstant, used.QuantityUsed);
                }
            }

            return sum;
        }

        /// <summary>
        /// Multi-item sell-side economics: the batch analog of
        /// ApplySellSideEconomics for a 2+ item request. Computes each requested
        /// root's economics via ComputePerItemEconomics - the SAME TradingPostMath
        /// fee math and instant-sell revenue basis the single-item path uses - and
        /// sums the survivors into the batch-level CraftingPlanResult fields.
        ///
        /// TargetUnitSellPrice is left null: a batch has N per-item unit sell
        /// prices and no single number generalizes them, matching that field's own
        /// "one item, one price" contract. SellableQuantity/NetSaleValue/
        /// CraftingProfit stay at their type defaults when NOT ONE requested root
        /// has a live sell price. MaterialOpportunityCost is set whenever Valued
        /// mode produced any usedMaterials, sellable roots or not, and is a SINGLE
        /// sum over the batch's whole merged UsedMaterials list rather than being
        /// scoped down to the roots that contribute to the three fields above.
        ///
        /// How this diverges from gw2efficiency's own rollup, and why
        /// UsedMaterials is decision-aware: docs/ARCHITECTURE.md,
        /// "Services Q-Z: relocated design narrative".
        /// </summary>
        internal static void ApplyBatchSellSideEconomics(
            CraftingPlanResult result,
            RecipeNode wrapperTree,
            SolveResult solveResult,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyList<PlanRequestItem> items,
            PriceBasis priceBasis,
            List<UsedMaterial> usedMaterials,
            OwnMaterialsMode ownMaterialsMode)
        {
            result.PriceBasis = priceBasis;

            long? materialOpportunityCost = ComputeMaterialOpportunityCost(
                usedMaterials, prices, ownMaterialsMode);
            result.MaterialOpportunityCost = materialOpportunityCost;

            var wrapperRecipe = wrapperTree?.Recipes?.FirstOrDefault(
                r => r.RecipeId == Gw2Constants.MultiItemWrapperRecipeId);
            if (wrapperRecipe == null || items == null || items.Count == 0)
            {
                return;
            }

            int sellableQuantitySum = 0;
            long netSaleValueSum = 0L;
            long craftCostOfSellableItemsSum = 0L;
            bool anySellable = false;

            // Both lists come from the same wrapper-build step
            // (RecipeService.BuildMultiItemTreeAsync's BuildWrapperNode),
            // in request order - see this method's own call sites
            // (GenerateStructuredMultiAsync/ResolveWithOverrides). Math.Min
            // is defensive only: a mismatch should never occur, but
            // degrading to the shared prefix is safer than an index
            // exception.
            int count = Math.Min(wrapperRecipe.Ingredients.Count, items.Count);
            for (int i = 0; i < count; i++)
            {
                var itemEconomics = ComputePerItemEconomics(
                    wrapperRecipe.Ingredients[i], items[i].ItemId, items[i].Quantity,
                    solveResult, prices);

                // Divergence item 2 from the doc comment above: a root
                // with no live sell price contributes nothing, regardless
                // of whether the solver bought or crafted it - no
                // craft-vs-buy filter (divergence item 1) here at all.
                if (!itemEconomics.NetSaleValue.HasValue)
                {
                    continue;
                }

                anySellable = true;
                sellableQuantitySum += itemEconomics.SellableQuantity;
                netSaleValueSum += itemEconomics.NetSaleValue.Value;
                craftCostOfSellableItemsSum += itemEconomics.ItemCraftCost;
            }

            if (!anySellable)
            {
                return;
            }

            result.SellableQuantity = sellableQuantitySum;
            result.NetSaleValue = netSaleValueSum;

            long profit = netSaleValueSum - craftCostOfSellableItemsSum;
            if (materialOpportunityCost.HasValue)
            {
                profit -= materialOpportunityCost.Value;
            }

            result.CraftingProfit = profit;
        }
    }
}
