using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// gw2efficiency's "Value Own Materials" force-buy pre-pass (M34-B2a #3,
    /// gw2e parity - see docs/gw2e-parity-spec.md Section 1 and the M34 R2
    /// research report, m34-r2-gw2e-owned-materials.md Section 2.2): a node
    /// is force-flagged "craft excluded" (letting normal buy-vs-vendor
    /// competition decide instead) when its TP buy price is cheaper than
    /// 85% of what its own components would cost to buy fresh
    /// (cheapestTree.ts's getCheaperToBuyItemIds/disableCraftForItemIds).
    ///
    /// gw2e computes this on a strictly zero-owned baseline so that an
    /// already-owned component's cost being free never masks a bad
    /// marginal-craft trade. This module implements exactly that rule; it
    /// is the CALLER's responsibility to pass a genuine zero-owned tree,
    /// which is exactly what CraftingPlanPipeline's Step 5.5 does - it
    /// invokes ComputeForceBuyOnlyNodeIds against `tree`, the pipeline's
    /// ORIGINAL, UNREDUCED tree (InventoryReducer.Reduce only ever mutates
    /// a clone, never `tree` itself), never the post-reduction tree the
    /// real solve goes on to use. Evaluating this rule on an
    /// already-reduced tree would make it a near no-op in precisely the
    /// scenario it exists for: owning a pile of components already makes
    /// their post-reduction craft cost look cheap regardless of what a
    /// FRESH purchase would cost, which is exactly the masking gw2e's
    /// zero-owned baseline is designed to prevent. See the pipeline's own
    /// Step 5.5 comment and the passing
    /// Structured_ValuedMode_ForceBuyPrePass_UsesZeroOwnedBaseline test
    /// (CraftingPlanPipelineTests.cs) for confirmation this module is wired
    /// against the correct (unreduced) tree at runtime.
    ///
    /// Never touches InventoryReducer's pool, PlanSolver's own Decision
    /// memo, or any owned-materials data directly - it only reads the
    /// (buyCost, craftCost) diagnostics PlanSolver.Solve already computes
    /// for every node, via a throwaway solve pass, and returns a NodeId set
    /// for the real solve's forceBuyOnlyNodeIds parameter.
    /// </summary>
    public static class OwnedMaterialsForceBuyPrePass
    {
        // gw2e's cheapestTree.ts hardcodes the same 0.85 constant (see the
        // R2 report Section 2.3) - not the precise tradingpost-fees math,
        // a standalone approximation reused here for parity, not derived
        // from TradingPostMath.
        private const double ForceBuyDiscountFactor = 0.85;

        /// <summary>
        /// Computes the set of NodeIds that should have craft excluded from
        /// the automatic buy-vs-craft-vs-vendor comparison for the given
        /// tree. Runs a throwaway PlanSolver.Solve pass (no overrides, no
        /// force-buy set) purely to gather per-node cost diagnostics and
        /// assign this tree's (deterministic, stable-across-repeat-solves)
        /// NodeIds - its own Plan/Decisions are discarded.
        /// </summary>
        public static ISet<int> ComputeForceBuyOnlyNodeIds(
            PlanSolver solver,
            RecipeNode tree,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            PriceBasis priceBasis,
            CurrencyValuation currencyValuation)
        {
            var diagnostics = new Dictionary<int, (long? BuyCost, long? CraftCost)>();

            solver.Solve(
                tree, prices, vendorOffers, priceBasis,
                overrides: null, currencyValuation: currencyValuation,
                forceBuyOnlyNodeIds: null, costDiagnostics: diagnostics);

            var forced = new HashSet<int>();
            foreach (var kvp in diagnostics)
            {
                var (buyCost, craftCost) = kvp.Value;
                if (buyCost.HasValue && craftCost.HasValue &&
                    buyCost.Value < craftCost.Value * ForceBuyDiscountFactor)
                {
                    forced.Add(kvp.Key);
                }
            }
            return forced;
        }
    }
}
