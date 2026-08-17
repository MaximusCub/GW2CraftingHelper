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
    /// for every node, via a throwaway solve pass, and returns two NodeId
    /// sets (see ForceBuyPrePassResult's own doc comment): the real solve's
    /// forceBuyOnlyNodeIds, plus a narrower, competency-independent subset
    /// used only to gate Decision.CheapestCraftUntrained.
    /// </summary>
    public static class OwnedMaterialsForceBuyPrePass
    {
        // gw2e's cheapestTree.ts hardcodes the same 0.85 constant (see the
        // R2 report Section 2.3) - not the precise tradingpost-fees math,
        // a standalone approximation reused here for parity, not derived
        // from TradingPostMath.
        private const double ForceBuyDiscountFactor = 0.85;

        /// <summary>
        /// Verification-review fix: the two outputs of
        /// ComputeForceBuyOnlyNodeIds, kept as two distinct sets rather than
        /// one. ForceBuyOnlyNodeIds alone can be competency-CAUSED: this
        /// throwaway pre-pass solve is competency-aware (characterDisciplines
        /// threaded through, see that parameter's own doc comment), so its
        /// craft diagnostic is the COMPETENCY-RESOLVED cost - a node whose
        /// cheap recipe is untrained can land in ForceBuyOnlyNodeIds purely
        /// because competency demoted the diagnostic to a costlier competent
        /// recipe, when the untrained recipe itself would never have been
        /// forced. CompetencyIndependentForceBuyNodeIds is the (always
        /// smaller-or-equal) subset forced under BOTH that evaluation AND a
        /// second, competency-BLIND evaluation using the RAW cheapest recipe
        /// regardless of training - forced under both evaluations, a much
        /// stronger signal than ForceBuyOnlyNodeIds alone but NOT a strict
        /// training-independence guarantee (see the nuance below).
        /// PlanSolver.Evaluate gates
        /// Decision.CheapestCraftUntrained on THIS narrower set (via
        /// Solve's competencyIndependentForceBuyNodeIds parameter), not on
        /// ForceBuyOnlyNodeIds membership, so a competency-caused force-buy
        /// at the node's OWN recipe choice can no longer suppress a real
        /// training opportunity - see the nuance below for the residual
        /// child-inflation case that still can -
        /// PillSourceCostBreakdown/the solver's own forceBuyOnlyNodeIds
        /// parameter (solve behavior itself) still use ForceBuyOnlyNodeIds
        /// exactly as before this fix.
        /// <para>
        /// Doc nuance (recorded follow-up, srcsel verification; direction
        /// corrected on a later follow-up sweep - see docs/KNOWN-ISSUES.md
        /// for the reasoning): "competency-BLIND" above describes the raw
        /// evaluation only AT THE NODE'S OWN recipe choice - picking the
        /// numerically cheapest recipe among node.Recipes regardless of
        /// whether the account is trained for it. The ingredient costs THAT
        /// recipe sums are NOT similarly blind: PlanSolver.Evaluate's
        /// recursive call for each child ingredient (see the
        /// rawCraftCostDiagnostics-writing loop) still threads
        /// bestRatingByDiscipline through, so a child's contribution to this
        /// raw figure is its normal competency-RESOLVED cost, not a second
        /// training-blind recursion all the way down. This makes the raw
        /// craft cost look more expensive than a truly training-blind
        /// figure would (a costlier resolved child, never a cheaper
        /// untrained one it isn't allowed to use) - and since membership is
        /// `buyCost.Value &lt; rawCraftCost.Value * ForceBuyDiscountFactor`,
        /// an INFLATED rawCraftCost only makes that inequality EASIER to
        /// satisfy. A node can therefore only be ADDED to
        /// CompetencyIndependentForceBuyNodeIds by this effect, never
        /// dropped from it - the resolved-cost figure never falls below the
        /// true training-blind figure, so no genuinely-forced node is ever
        /// missed. The residual risk runs the OTHER way: a parent node whose
        /// own untrained recipe is genuinely cheap enough to survive a true
        /// blind evaluation can still get pulled into
        /// CompetencyIndependentForceBuyNodeIds by a resolved child's
        /// inflated contribution, which then suppresses THAT PARENT's own
        /// Decision.CheapestCraftUntrained (PlanSolver.cs's
        /// `cheapestCraftUntrained = !isCompetencyIndependentForceBuy &amp;&amp;
        /// ...` gate) - i.e. this can falsely exclude a real training
        /// opportunity at the parent, not miss a genuinely-independent one.
        /// A child's own untrained-recipe opportunity is not lost by this -
        /// it is evaluated and reported independently at that child's own
        /// node, via that child's own diagnostics entry.
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
        /// Adversarial-review fix (Critical #3, source-selection-
        /// simplification): the SAME per-character discipline snapshot the
        /// caller's zero-owned guide solve and real solve both receive.
        /// Without this, this throwaway solve was the ONLY solve of a
        /// generation still running competency-UNKNOWN: for a parent node P
        /// with a not-actually-craftable child ingredient C, THIS solve's
        /// own recursive Evaluate(C) call never excludes craft on
        /// competency grounds, so C commits Craft (its cheap, untrained
        /// price) and that price folds straight into P's own craftCost sum
        /// (via the ingredientCost accumulation in Evaluate's recipe loop) -
        /// while the real, competency-aware solve commits C to BuyFromTp
        /// instead, giving P a very different real craftCost. The 85%
        /// force-buy comparison was therefore derived from craft costs the
        /// real solve could never actually produce, silently diverging
        /// forceBuyOnlyNodeIds from the tree it gets applied to. Null (the
        /// default) reproduces this method's pre-existing behavior exactly
        /// - competency UNKNOWN, matching every other caller of
        /// PlanSolver.Solve that omits this parameter.
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
            // Verification-review fix: the RAW (competency-BLIND) twin of
            // diagnostics above - see ForceBuyPrePassResult's own doc
            // comment and PlanSolver.Solve's rawCraftCostDiagnostics
            // parameter for why this comes from the SAME throwaway solve
            // pass rather than a second Solve() call.
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

                // Second, competency-BLIND evaluation of the SAME 0.85 rule
                // - the RAW cheapest craft cost regardless of training. Only
                // a node forced under BOTH evaluations is genuinely forced
                // no matter what the account is trained in - see
                // ForceBuyPrePassResult's own doc comment.
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
