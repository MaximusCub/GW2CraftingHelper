using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Sell-side economics arithmetic (SellableQuantity/NetSaleValue/
    /// TargetUnitSellPrice/CraftingProfit/MaterialOpportunityCost), moved
    /// out of CraftingPlanPipeline (M38 WP-12, architecture S4b) as a pure,
    /// move-only extraction - same fields, same order, same arithmetic. See
    /// docs/KNOWN-ISSUES.md #25 for the full M37 design rationale this
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
            // M37 (KNOWN-ISSUES #25): the per-root arithmetic (over-
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
            // reduction consumed would have netted after TP fees. Reduction
            // itself never changes - owned mats are still consumed first at
            // zero acquisition cost in both modes; this only affects the
            // profit figure below.
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
        /// One requested root's own sell-side figures - the SellableQuantity/
        /// NetSaleValue/TargetUnitSellPrice arithmetic factored out of
        /// ApplySellSideEconomics (M20/M37) so both the single-item path
        /// (one call, on the plan's own tree root) and
        /// ApplyBatchSellSideEconomics (M37, one call per requested root)
        /// share IDENTICAL fee math and instant-sell revenue basis - no
        /// parallel/duplicate costing logic. itemId is passed explicitly
        /// rather than read from itemRoot.Id: both call sites already
        /// guarantee itemRoot.Id == itemId by construction (RecipeService.
        /// BuildTreeAsync/BuildMultiItemTreeAsync), but keeping it explicit
        /// means this method never silently depends on that invariant
        /// holding.
        ///
        /// ItemCraftCost is itemRoot's own SolverDecision.TotalCost (the
        /// same post-correction, shared-vendor-batch-reconciled per-node
        /// real coin figure CraftingTreeBuilder copies onto
        /// CraftingTreeNode.SubtreeCost for that node's own pill display -
        /// see PlanSolver.Solve's AllocateVendorNodeCosts/RecomputeCraftCosts
        /// passes) - 0 when the root has no decision entry at all (should
        /// never happen for a real tree root passed to a completed Solve,
        /// but defensive rather than throwing). The single-item path never
        /// reads this field (it already has its own, equivalent
        /// solveResult.Plan.TotalCoinCost); only ApplyBatchSellSideEconomics
        /// uses it, to attribute each item's own fair share of a batch's
        /// (possibly materials-shared) total cost.
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
            // Review fix (finding 8, MEASURED): "produced" must use the
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
                ItemCraftCost = itemCraftCost
            };
        }

        /// <summary>
        /// Sum, over <paramref name="usedMaterials"/>, of the net TP sale
        /// value of the owned materials inventory reduction consumed -
        /// pure extraction of ApplySellSideEconomics' own-materials
        /// opportunity-cost arithmetic (M28/M34-B2a #3) so
        /// ApplyBatchSellSideEconomics (M37) can reuse it unchanged over a
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
        /// M37 (gw2efficiency parity - multi-item sell-side economics,
        /// closes KNOWN-ISSUES #25): batch analog of ApplySellSideEconomics
        /// for a 2+ item request. Computes each requested root's own
        /// economics via ComputePerItemEconomics (the SAME TradingPostMath
        /// fee math and SellInstant/instant-sell revenue basis the
        /// single-item path already uses) and sums the survivors into the
        /// batch-level CraftingPlanResult fields. See
        /// docs/KNOWN-ISSUES.md #25's "FIXED in M37" record for the full
        /// design rationale; summary of how this diverges from gw2e's own
        /// multi-item rollup (the `o()` function in the live app bundle -
        /// see docs/research/m37-r2-batch-economics.md Sections 1.2/4.1):
        ///   1. DIVERGED: unlike gw2e's rollup, there is NO craft-vs-buy
        ///      filter here - any requested root with a live TP sell price
        ///      contributes its own SellableQuantity/NetSaleValue/
        ///      CraftingProfit regardless of whether the solver bought or
        ///      crafted it. This matches this module's own already-shipped
        ///      single-item ApplySellSideEconomics semantics (which has
        ///      never filtered by craft-vs-buy - a flip/arbitrage number is
        ///      still meaningful) and the research report's explicit
        ///      recommendation (Section 4.1.1) NOT to add gw2e's own
        ///      craft===true filter - see
        ///      MultiItemPlanTests.GenerateStructuredAsync_MultiItem_OneRootBoughtButTradable_IncludedInSum.
        ///   2. DIVERGED: a CRAFTED root with no live TP sell price still
        ///      contributes NOTHING to the sum (excluded entirely - both
        ///      its revenue AND its own craft cost drop out together) -
        ///      NOT gw2e's silent "-cost" drag for an untradable crafted
        ///      root.
        ///   3. DIVERGED: single profit basis (instant-sell/buy-order, via
        ///      SellInstant), matching the single-item row - gw2e always
        ///      shows a second sell-listing-basis figure this module has
        ///      never surfaced.
        ///
        /// TargetUnitSellPrice is left null (batch fields stay at their
        /// type default there): a batch has N per-item unit sell prices,
        /// one per requested item, and no single number generalizes them -
        /// mirrors that field's own "one item, one price" contract (see
        /// CraftingPlanResult.TargetUnitSellPrice's doc comment).
        ///
        /// MaterialOpportunityCost is always set when Valued mode produced
        /// any usedMaterials, regardless of whether any root turns out
        /// sellable - matching ApplySellSideEconomics' own "opportunity
        /// cost is not gated on target sellability" contract
        /// (CraftingPlanResult.MaterialOpportunityCost's doc comment).
        /// SellableQuantity/NetSaleValue/CraftingProfit stay at their type
        /// defaults (0/null/null) when NOT ONE requested root has a live
        /// sell price - the batch equivalent of the single-item "no sell
        /// price at all" case.
        ///
        /// Documented nuance (M37 review): MaterialOpportunityCost is a
        /// SINGLE sum over the batch's whole merged UsedMaterials list
        /// (Reduce runs on the entire wrapper tree before Solve ever picks
        /// Buy vs Craft per root - see GenerateStructuredMultiAsync's own
        /// step ordering) - it is NOT scoped down to only the roots that
        /// end up contributing to SellableQuantity/NetSaleValue/
        /// CraftingProfit above. A root the solver decides to buy can
        /// still have owned ingredient stock recorded as "used" against
        /// its own never-crafted subtree, and that forgone value is
        /// deducted from the batch's CraftingProfit regardless. This
        /// matches the single-item path's own pre-existing behavior
        /// exactly (ApplySellSideEconomics' MaterialOpportunityCost is
        /// likewise never gated on the target's own craft/buy decision),
        /// so it is intentional, not a new gap - see
        /// MultiItemPlanTests.GenerateStructuredAsync_MultiItem_ValuedMode_MixedBuyCraftBatch_MaterialOpportunityCostIsWholeTreeSum.
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
