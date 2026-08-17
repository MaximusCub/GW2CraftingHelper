using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class SolverDecision
    {
        public AcquisitionSource Source { get; internal set; }
        public int RecipeId { get; internal set; }
        public long? TotalCost { get; internal set; }

        // The comparison figure PlanSolver actually ranked this decision
        // on (see PlanSolver's Decision.ComparisonValue). DECISION-ONLY:
        // TotalCost remains the sole displayed coin figure; this exists
        // only so the value-detail tooltip can explain WHY a decision
        // won, never to be summed into any displayed total. Equal to
        // TotalCost when no currency valuation contributed; null only
        // when TotalCost is also null.
        public long? ComparisonValue { get; internal set; }

        // Non-coin currency lines of a winning BuyFromVendor decision (e.g.
        // spirit shards, karma) - null/empty for every other Source. This is
        // the real, already-scaled-to-quantity cost that TotalCost cannot
        // represent (TotalCost is coin-only, see PlanSolver.Decision docs).
        // A later display task threads this into the tree/shopping UI.
        public IReadOnlyList<CostLine> VendorCurrencyCosts { get; internal set; }

        // TP-valued Item cost lines of a winning BuyFromVendor decision,
        // already scaled to this occurrence's quantity - null for every
        // other Source or when the offer had no Item lines. Each entry's
        // GoldValue is the exact amount already folded into TotalCost, so
        // CraftingTreeBuilder can synthesize component leaves without
        // recomputing the fold.
        public IReadOnlyList<VendorItemCostLine> VendorItemCosts { get; internal set; }

        // True only when the winning offer had a genuine raw coin cost
        // line - distinct from coin that exists only because an Item line
        // got TP-valued and folded in. Used solely to decide whether
        // "coin" counts as one of the offer's 2+ cost kinds for
        // component-leaf synthesis.
        public bool VendorHasRawCoin { get; internal set; }

        // True when VendorCurrencyCosts/VendorItemCosts are stale
        // relative to the corrected TotalCost (a merged vendor step's
        // cost was reallocated across 2+ occurrences).
        // CraftingTreeBuilder suppresses component-leaf synthesis then,
        // rather than display numbers that no longer sum to TotalCost.
        public bool VendorComponentCostsUnreliable { get; internal set; }

        // Which acquisition paths were feasible for this node, independent
        // of which one was chosen. Drives the per-node override UI.
        //
        // CanCraft is true whenever this node has at least one recipe
        // (gw2e's "hasComponents") - a recipe's craft cost is always
        // defined now (see PlanSolver.Evaluate), even when it is inflated
        // by zero-filled unpriceable descendants, so this is no longer
        // gated on full priceability.
        //
        // CanBuyVendor is true whenever overriding this node to
        // BuyFromVendor would succeed - either because a coin-comparable
        // vendor offer exists (competes with TP/craft on equal footing) or
        // because only a fallback-tier offer (unvalued non-coin currency)
        // exists and would be used as a last resort. It intentionally does
        // NOT distinguish "genuinely comparable" from "fallback-only" by
        // itself - a future display-layer pass (M33 backlog #18) should
        // read Source/TotalCost/VendorCurrencyCosts on the committed
        // decision to decide how to present a non-chosen Vendor
        // alternative, rather than treating this flag alone as "equally
        // comparable" to TP/Craft.
        public bool CanCraft { get; internal set; }
        public bool CanBuyTp { get; internal set; }
        public bool CanBuyVendor { get; internal set; }

        // True only when Source is BuyFromTp and the committed unit price
        // came from the non-preferred TP side because the preferred side
        // had no listings. Drives the unit-price tooltip caveat.
        public bool PriceSideFellBack { get; internal set; }

        // Raw cost breakdowns for every feasible source (not just the
        // winner). Always non-null; IsAvailable reflects the flags above.
        // Ultimately consumed by PillSubduingEvaluator - never read by
        // any cost total.
        public PillSourceCostBreakdown CraftCostBreakdown { get; internal set; }
        public PillSourceCostBreakdown BuyFromTpCostBreakdown { get; internal set; }
        public PillSourceCostBreakdown BuyFromVendorCostBreakdown { get; internal set; }

        // True only when craft was excluded from the automatic pick
        // because no character meets the winning recipe's discipline
        // requirement (never for the force-buy pre-pass's exclusion).
        // The companion fields describe the recipe that would have won -
        // only meaningful when CraftExcludedByCompetency is true.
        public bool CraftExcludedByCompetency { get; internal set; }
        public long? CraftExcludedRealCost { get; internal set; }
        public IReadOnlyList<string> CraftExcludedDisciplines { get; internal set; }
        public int CraftExcludedMinRating { get; internal set; }

        // True whenever the numerically cheapest raw craft recipe overall
        // is untrained, independent of whether the automatic pick got
        // excluded - unlike CraftExcludedByCompetency, this also covers a
        // competent costlier/other-tier recipe winning instead. The
        // companion fields describe that cheap recipe. Always false only
        // for a node genuinely force-bought regardless of training (see
        // OwnedMaterialsForceBuyPrePass.ForceBuyPrePassResult) - a
        // competency-caused force-buy must not hide a real training
        // opportunity.
        public bool CheapestCraftUntrained { get; internal set; }
        public long? CheapestCraftRealCost { get; internal set; }
        public IReadOnlyList<string> CheapestCraftDisciplines { get; internal set; }
        public int CheapestCraftMinRating { get; internal set; }
    }
}
