using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// gw2efficiency's "Value Own Materials" force-buy pre-pass: a node
    /// is force-flagged "craft excluded" when its TP buy price is cheaper
    /// than 85% of what its components would cost to buy fresh.
    ///
    /// Must be evaluated on a genuine zero-owned (unreduced) tree - the
    /// caller's responsibility. On an already-reduced tree the rule is a
    /// near no-op in exactly the scenario it exists for: owned components
    /// make post-reduction craft cost look cheap regardless of what a
    /// fresh purchase would cost.
    ///
    /// Reads only the (buyCost, craftCost) diagnostics from a throwaway
    /// solve pass and returns two NodeId sets (see ForceBuyPrePassResult);
    /// never touches reducer or solver state directly.
    /// </summary>
    internal static class OwnedMaterialsForceBuyPrePass
    {
        // gw2e hardcodes the same 0.85 constant - a standalone
        // approximation reused for parity, not derived from
        // TradingPostMath.
        private const double ForceBuyDiscountFactor = 0.85;

        /// <summary>
        /// The two outputs of ComputeForceBuyOnlyNodeIds, kept distinct.
        /// ForceBuyOnlyNodeIds alone can be competency-CAUSED, so an
        /// untrained cheap recipe can be forced only because competency
        /// demoted the diagnostic. CompetencyIndependentForceBuyNodeIds is
        /// the subset forced under BOTH that evaluation and a second,
        /// competency-blind one using the raw cheapest recipe; it gates
        /// Decision.CheapestCraftUntrained, while solve behavior itself
        /// still uses ForceBuyOnlyNodeIds.
        /// <para>
        /// "Competency-blind" applies only at the node's own recipe choice,
        /// which bounds the error in one direction and leaves one residual -
        /// see docs/ARCHITECTURE.md section 8.2.
        /// </para>
        /// </summary>
        public readonly struct ForceBuyPrePassResult
        {
            public ISet<int> ForceBuyOnlyNodeIds { get; }

            public ISet<int> CompetencyIndependentForceBuyNodeIds { get; }

            public ForceBuyPrePassResult(
                ISet<int> forceBuyOnlyNodeIds, ISet<int> competencyIndependentForceBuyNodeIds)
            {
                ForceBuyOnlyNodeIds = forceBuyOnlyNodeIds;
                CompetencyIndependentForceBuyNodeIds = competencyIndependentForceBuyNodeIds;
            }
        }

        /// <summary>
        /// Computes the set of NodeIds that should have craft excluded from
        /// the automatic buy-vs-craft-vs-vendor comparison for the given
        /// tree. Runs a throwaway PlanSolver.Solve pass (no overrides, no
        /// force-buy set) purely to gather per-node cost diagnostics and
        /// assign this tree's (deterministic, stable-across-repeat-solves)
        /// NodeIds - its own Plan/Decisions are discarded.
        /// </summary>
        /// <param name="characterDisciplines">
        /// The same discipline snapshot the caller's other solves receive.
        /// Without it, this throwaway solve alone would run
        /// competency-unknown and derive the 0.85 comparison from craft
        /// costs the real, competency-aware solve could never produce.
        /// Null means competency unknown.
        /// </param>
        public static ForceBuyPrePassResult ComputeForceBuyOnlyNodeIds(
            PlanSolver solver,
            RecipeNode tree,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            PriceBasis priceBasis,
            CurrencyValuation currencyValuation,
            IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines = null,
            // Threaded through so the throwaway diagnostic solve costs
            // vendor cost lines the same way the real solve will; without
            // it the 0.85 rule would compare a craft cost against a vendor
            // cost the real solve never sees.
            VendorCostLineSubtrees vendorCostSubtrees = null)
        {
            var diagnostics = new Dictionary<int, (long? BuyCost, long? CraftCost)>();
            // The competency-blind twin of diagnostics above, gathered
            // from the same throwaway solve (see ForceBuyPrePassResult).
            var rawCraftCostDiagnostics = new Dictionary<int, long?>();

            solver.Solve(
                tree, prices, vendorOffers, priceBasis,
                overrides: null, currencyValuation: currencyValuation,
                forceBuyOnlyNodeIds: null, costDiagnostics: diagnostics,
                rawCraftCostDiagnostics: rawCraftCostDiagnostics,
                characterDisciplines: characterDisciplines,
                vendorCostSubtrees: vendorCostSubtrees);

            var forced = new HashSet<int>();
            var competencyIndependentForced = new HashSet<int>();
            foreach (var kvp in diagnostics)
            {
                var (buyCost, craftCost) = kvp.Value;
                bool forcedHere = buyCost.HasValue && craftCost.HasValue &&
                    buyCost.Value < craftCost.Value * ForceBuyDiscountFactor;
                if (!forcedHere)
                {
                    continue;
                }

                forced.Add(kvp.Key);

                // Second, competency-blind evaluation of the same 0.85
                // rule; only a node forced under both evaluations counts
                // (see ForceBuyPrePassResult).
                if (rawCraftCostDiagnostics.TryGetValue(kvp.Key, out long? rawCraftCost) &&
                    rawCraftCost.HasValue &&
                    buyCost.Value < rawCraftCost.Value * ForceBuyDiscountFactor)
                {
                    competencyIndependentForced.Add(kvp.Key);
                }
            }

            return new ForceBuyPrePassResult(forced, competencyIndependentForced);
        }
    }
}
