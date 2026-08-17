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
    /// second near-identical model type; lines are pre-grouped (one entry
    /// per distinct (Type, Id) pair, duplicate ingredient/cost-line
    /// entries already summed). Adversarial-review correction: Count can
    /// be 0 for a craft ingredient line fully covered by owned stock
    /// (InventoryReducer reduces the underlying RecipeNode.Quantity to 0
    /// post-reduction, never removes the line) - harmless for
    /// StrictDomination (0 vs 0 never trips a strict inequality), but the
    /// "always >= 1" claim this doc comment previously made was false.
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
        /// Adversarial-review fix (Critical #5, source-selection-
        /// simplification): true when this breakdown does NOT fully
        /// represent every cost component of its source - currently, a
        /// craft recipe with a GuildUpgrade or other unrecognized
        /// ingredient type (BuildCraftCostBreakdown has no representable
        /// "kind" for it, so the line is silently omitted entirely, unlike
        /// an Item/Currency line whose Count can legitimately be 0). A
        /// pill's own cost total (TotalCost/ComparisonValue) is
        /// unaffected - only this DISPLAY-side raw decomposition is
        /// incomplete. Both PillSubduingRule.StrictDomination and Weighted
        /// are refused whenever either compared side IsIncomplete - the
        /// same conservative "no breakdown data, don't claim a
        /// comparison" posture CraftingTreeNode.VendorComponentCostsUnreliable
        /// already takes for an unreliable merged-vendor-step breakdown.
        /// False (the default) for every other source.
        /// </summary>
        public bool IsIncomplete { get; set; }

        /// <summary>
        /// Adversarial-review fix (Critical #4, source-selection-
        /// simplification): true when at least one of this CRAFT source's
        /// direct ingredients had its RecipeNode.Quantity reduced by owned
        /// account stock (InventoryReducer.ReducedTreeResult.
        /// OwnedQuantityUsedByNode) before this breakdown was built. Craft
        /// ingredient lines come from the (possibly owned-stock-reduced)
        /// crafting tree, while a losing VENDOR offer's own item cost
        /// lines are computed independently and are NEVER discounted by
        /// owned stock (a vendor trade-in is not a tree node at all) - so
        /// comparing the two sides' raw quantities kind-by-kind can be
        /// exactly backwards (a craft ingredient reduced to 10 by 10 owned
        /// Globs of Ectoplasm can look "cheaper" than a vendor's un-
        /// reduced 15, even when the real out-of-pocket craft cost is
        /// actually the higher of the two). Gates StrictDomination ONLY -
        /// Weighted stays valid, since DecisionValue already reflects the
        /// real, correctly-discounted economics on both sides. False (the
        /// default) whenever the caller does not thread owned-usage data
        /// through (every pre-existing caller/test), reproducing pre-
        /// existing behavior exactly.
        /// </summary>
        public bool RawQuantitiesReducedByOwnedStock { get; set; }

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
