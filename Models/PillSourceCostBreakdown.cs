using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// source-selection-simplification (maintainer-approved redesign,
    /// docs/gw2e-considerations.md): a per-node, per-source (Craft/
    /// BuyFromTp/BuyFromVendor) raw cost decomposition, computed by
    /// PlanSolver.Evaluate for EVERY feasible source regardless of which
    /// one actually won (mirrors costDiagnostics' own "always computed,
    /// never filtered by decision" precedent) - unlike SolverDecision's
    /// existing VendorCurrencyCosts/VendorItemCosts (populated ONLY for
    /// the winning BuyFromVendor decision), this exists specifically so
    /// PillSubduingEvaluator can compare a LOSING alternative's cost shape
    /// against the winning one. Purely additive/informational - never
    /// read by PickCheapest or any cost total (repo invariant: decision
    /// math stays pure).
    ///
    /// CostLines uses the SAME (Type, Id, Count) shape as CostLine for
    /// BOTH non-coin currency lines (Type == "Currency") and TP-priced
    /// item barter lines (Type == "Item", e.g. Globs of Ectoplasm) - raw
    /// quantities only, deliberately never gold-valued here (see
    /// RawCoin's own doc comment for why). Reusing CostLine avoids a
    /// second near-identical model type; Count is always >= 1 and lines
    /// are pre-grouped (one entry per distinct (Type, Id) pair, duplicate
    /// ingredient/cost-line entries already summed).
    /// </summary>
    public class PillSourceCostBreakdown
    {
        /// <summary>
        /// False when this source is not feasible at all for this node
        /// (mirrors CraftingTreeNode.CanCraft/CanBuyTp/CanBuyVendor) -
        /// every other field is meaningless/default when this is false.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Raw coin this source needs, EXCLUDING any TP-value folded from
        /// an Item cost line (see CostLines) - deliberately NOT the same
        /// number as SolverDecision.TotalCost for a BuyFromVendor decision
        /// (which folds Item cost lines' gold value in). Strict-domination
        /// detection compares raw, unpriced quantities kind-by-kind (same
        /// currency/item ids, no valuation needed at all) - folding item
        /// value into "coin" here would make an Item-line difference
        /// invisible to that comparison, silently defeating the "N more X,
        /// no valuation needed" case (e.g. Amalgamated Rift Essence: same
        /// coin, but vendor wants 10 more raw Globs of Ectoplasm than
        /// crafting does).
        /// </summary>
        public long RawCoin { get; set; }

        /// <summary>Non-coin currency and TP-priced item cost lines, raw quantities, pre-grouped by (Type, Id).</summary>
        public List<CostLine> CostLines { get; set; } = new List<CostLine>();

        /// <summary>
        /// The valued, fully-comparable coin-equivalent figure for this
        /// source (SolverDecision.ComparisonValue's own per-source
        /// counterpart) - null whenever any cost component of THIS source
        /// is unvalued (an unvalued currency ingredient/line, or this
        /// source only has a fallback-tier candidate). WEIGHTED subduing
        /// requires both sides' DecisionValue to be non-null.
        /// </summary>
        public long? DecisionValue { get; set; }
    }
}
