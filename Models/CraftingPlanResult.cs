using System.Collections.Generic;
using GW2CraftingHelper.Contracts;

namespace GW2CraftingHelper.Models
{
    public class CraftingPlanResult
    {
        public CraftingPlan Plan { get; set; }
        public IReadOnlyDictionary<int, ItemMetadata> ItemMetadata { get; set; }
        public List<UsedMaterial> UsedMaterials { get; set; }
        public List<RequiredDiscipline> RequiredDisciplines { get; set; }
        public List<RequiredRecipe> RequiredRecipes { get; set; }
        public CraftingTreeNode CraftingTree { get; set; }
        public List<string> DebugLog { get; set; }

        /// <summary>Price basis used for material costs in this plan.</summary>
        public PriceBasis PriceBasis { get; set; }

        /// <summary>
        /// Instant-sell unit price of the target item (buys.unit_price),
        /// null when the item has no buy orders / is untradable. Always
        /// null for a multi-item batch (M37, KNOWN-ISSUES #25) - a batch
        /// has N per-item unit prices, one per requested item, and no
        /// single number generalizes them (see
        /// CraftingPlanPipeline.ApplyBatchSellSideEconomics).
        /// </summary>
        public long? TargetUnitSellPrice { get; set; }

        /// <summary>
        /// Units the plan actually produces (>= requested quantity when the
        /// chosen root recipe over-produces). Sell-side figures use this.
        /// For a multi-item batch (M37), this is the SUM across every
        /// requested root that is both crafted and has a live sell price
        /// (see ApplyBatchSellSideEconomics) - a root that was bought, or
        /// has no sell price, is excluded from the sum entirely rather than
        /// contributing 0.
        /// </summary>
        public int SellableQuantity { get; set; }

        /// <summary>
        /// Net coin from instant-selling the crafted quantity after the 15%
        /// Trading Post fees; null when no sell price exists. For a
        /// multi-item batch (M37), this is the SUM of NetSaleValue across
        /// every requested root that is both crafted and has a live sell
        /// price (see ApplyBatchSellSideEconomics); null when NOT ONE
        /// requested root qualifies.
        /// </summary>
        public long? NetSaleValue { get; set; }

        /// <summary>
        /// NetSaleValue minus the plan's total COIN cost. Non-coin currency
        /// costs are not valued and are excluded; null when no sell price.
        /// For a multi-item batch (M37), the cost subtracted is the SUM of
        /// only the qualifying roots' own craft cost (each root's own
        /// SolverDecision.TotalCost) - NOT Plan.TotalCoinCost, which also
        /// includes every non-qualifying requested root's cost (bought
        /// roots, or crafted roots with no sell price) that this figure
        /// deliberately excludes (see ApplyBatchSellSideEconomics).
        /// </summary>
        public long? CraftingProfit { get; set; }

        /// <summary>
        /// Inputs for local re-solving (per-node overrides). Populated by
        /// GenerateStructuredAsync; null on the legacy path.
        /// </summary>
        public PlanSolveContext SolveContext { get; set; }

        /// <summary>
        /// Sum, over UsedMaterials, of TradingPostMath.NetSaleRevenue for
        /// that material's instant-sell unit price and quantity used: what
        /// selling those already-owned materials would have netted after
        /// Trading Post fees. Null in OwnMaterialsMode.Free, or when no
        /// materials were used by inventory reduction. A material with no
        /// instant-sell price (SellInstant 0/absent) contributes 0 rather
        /// than being excluded from the sum. For a multi-item batch (M37),
        /// this is computed once over the whole batch's already-merged
        /// UsedMaterials list, independent of SellableQuantity/
        /// NetSaleValue/CraftingProfit's per-root craft/sell-price filter -
        /// it is set whenever Valued mode produced any usedMaterials at
        /// all, even if the batch turns out to have zero qualifying
        /// sellable roots (see ApplyBatchSellSideEconomics).
        /// </summary>
        public long? MaterialOpportunityCost { get; set; }

        /// <summary>
        /// Name/icon metadata for wallet currencies referenced by
        /// Plan.CurrencyCosts, keyed by currency id. Null when the pipeline
        /// was not given a CurrencyMetadataService, or when that service's
        /// first fetch has not completed yet; CurrencyCost rows then render
        /// text-only using the Gw2Constants offline name fallback (see
        /// PlanViewModelBuilder).
        /// </summary>
        public IReadOnlyDictionary<int, CurrencyMetadata> CurrencyMetadata { get; set; }

        /// <summary>
        /// Wiki-derived acquisition hints for unpriceable items, keyed by
        /// item id (see AcquisitionHintService / ref/acquisition_hints_seed.json).
        /// Hint text is tooltip-only presentation; null when the module was
        /// not wired with hint data.
        /// </summary>
        public IReadOnlyDictionary<int, AcquisitionHint> AcquisitionHints { get; set; }

        /// <summary>
        /// Owned amount per currency id referenced by Plan.CurrencyCosts
        /// (M34-B2a #4 - see AccountCurrencyIndex). Cosmetic display data
        /// only, computed strictly after solving from the account wallet
        /// snapshot - never fed back into any decision or total. Null when
        /// no wallet snapshot was available or the plan needs no currency.
        /// </summary>
        public IReadOnlyDictionary<int, int> OwnedCurrencyAmounts { get; set; }

        /// <summary>
        /// M35 (gw2efficiency parity - multi-item plans): the original
        /// per-item request (item id + quantity) this result was generated
        /// for, in request order. Populated ONLY for a genuine multi-item
        /// batch (2+ requested items, solved via the synthetic wrapper -
        /// see Gw2Constants.MultiItemWrapperItemId); null for a single-item
        /// plan, including a single-item request made through the
        /// multi-item entry point (which short-circuits straight to the
        /// untouched single-item path, echoing gw2e's own `if
        /// (r.length===1) return r[0]` - see
        /// CraftingPlanPipeline.GenerateStructuredAsync's list overload).
        /// A caller must not fall back to Plan.TargetItemId/TargetQuantity
        /// for a multi-item batch: those hold the internal wrapper's own
        /// placeholder id/quantity there and must never be displayed - use
        /// MultiItemRoots (or this list) instead.
        /// </summary>
        public IReadOnlyList<PlanRequestItem> RequestedItems { get; set; }

        /// <summary>
        /// Populated instead of CraftingTree for a multi-item plan
        /// (RequestedItems has 2+ entries): one full CraftingTreeNode per
        /// requested item, in request order, each built exactly as
        /// CraftingTree would be for a single-item plan of that same
        /// item/quantity. The synthetic wrapper root used to solve them
        /// together never surfaces here - echoes gw2efficiency's own
        /// componentTree.html hiding its equivalent fake
        /// `multipleRecipeTree` node from the rendered tree
        /// (docs/gw2e-parity-spec.md, the M34 r1 multi-item research
        /// report). Null for a single-item plan, which continues to
        /// populate CraftingTree as before.
        /// </summary>
        public List<CraftingTreeNode> MultiItemRoots { get; set; }
    }
}
