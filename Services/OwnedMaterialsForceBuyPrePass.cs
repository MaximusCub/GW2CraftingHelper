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
        /// ForceBuyOnlyNodeIds alone can be competency-CAUSED (the
        /// pre-pass's craft diagnostic is competency-resolved, so an
        /// untrained cheap recipe can be forced only because competency
        /// demoted the diagnostic). CompetencyIndependentForceBuyNodeIds
        /// is the subset forced under BOTH that evaluation and a second,
        /// competency-blind one using the raw cheapest recipe; it gates
        /// Decision.CheapestCraftUntrained, while solve behavior itself
        /// still uses ForceBuyOnlyNodeIds.
        /// <para>
        /// Nuance: "competency-blind" applies only at the node's own
        /// recipe choice - ingredient costs are still the normal
        /// competency-resolved figures, which can only inflate the raw
        /// craft cost and therefore only ADD nodes to this set, never
        /// drop them. The residual risk: a parent whose untrained recipe
        /// would survive a true blind evaluation can be pulled in by an
        /// inflated child contribution, falsely excluding a real training
        /// opportunity at the parent (the child's own opportunity is still
        /// reported at the child's node).
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
            IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines = null)
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
                characterDisciplines: characterDisciplines);

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
