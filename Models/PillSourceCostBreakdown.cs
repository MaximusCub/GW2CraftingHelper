using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// A per-node, per-source raw cost decomposition, computed for EVERY
    /// feasible source regardless of which won, so
    /// PillSubduingEvaluator can compare a losing alternative's cost
    /// shape against the winning one. Purely informational - never read
    /// by PickCheapest or any cost total.
    ///
    /// CostLines uses the same (Type, Id, Count) shape as CostLine for
    /// both currency and item barter lines - raw quantities only, never
    /// gold-valued here (see RawCoin). Lines are pre-grouped per distinct
    /// (Type, Id). Count can be 0 for a craft ingredient fully covered by
    /// owned stock - harmless for StrictDomination.
    /// </summary>
    internal class PillSourceCostBreakdown
    {
        /// <summary>
        /// False when this source is not feasible at all for this node
        /// (mirrors CraftingTreeNode.CanCraft/CanBuyTp/CanBuyVendor) -
        /// every other field is meaningless/default when this is false.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// True when this breakdown does not fully represent every cost
        /// component of its source (a GuildUpgrade/unrecognized
        /// ingredient has no representable line). The pill's own cost
        /// total is unaffected - only this display-side decomposition is
        /// incomplete. Both subduing rules are refused whenever either
        /// compared side IsIncomplete.
        /// </summary>
        public bool IsIncomplete { get; set; }

        /// <summary>
        /// True when at least one of this CRAFT source's direct
        /// ingredients was reduced by owned account stock. Craft
        /// ingredient lines come from the reduced tree while a vendor
        /// offer's cost lines are never discounted, so comparing raw
        /// quantities kind-by-kind can be exactly backwards. Gates
        /// StrictDomination only - Weighted's DecisionValue already
        /// reflects the correctly-discounted economics.
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
