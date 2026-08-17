using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class SolverDecision
    {
        public AcquisitionSource Source { get; internal set; }
        public int RecipeId { get; internal set; }
        public long? TotalCost { get; internal set; }

        // currency-ux-package (Feature 3): the internal comparison figure
        // PlanSolver actually ranked this decision on (real coin/craft cost
        // PLUS any valued currency contribution, recursively rolled up
        // through descendants - see PlanSolver.Evaluate's Decision.
        // ComparisonValue doc comment, the private counterpart this is
        // copied from). DECISION-ONLY (repo invariant, restated here since
        // this is the one place that internal figure crosses into public
        // API surface): TotalCost above remains the sole real/displayed
        // coin figure everywhere in the app; ComparisonValue exists only so
        // a hover detail (TreeSectionController's value-detail tooltip) can
        // explain WHY a CRAFT/BuyFromVendor decision won, never to be
        // summed into any displayed total. Equal to TotalCost whenever no
        // currency valuation contributed anywhere in this decision's own
        // subtree (the common case) - null only when TotalCost is also
        // null (UnknownSource).
        public long? ComparisonValue { get; internal set; }

        // Non-coin currency lines of a winning BuyFromVendor decision (e.g.
        // spirit shards, karma) - null/empty for every other Source. This is
        // the real, already-scaled-to-quantity cost that TotalCost cannot
        // represent (TotalCost is coin-only, see PlanSolver.Decision docs).
        // A later display task threads this into the tree/shopping UI.
        public IReadOnlyList<CostLine> VendorCurrencyCosts { get; internal set; }

        // W4B (vendor cost-component leaves): TP-valued Item cost lines of
        // a winning BuyFromVendor decision (e.g. Globs of Ectoplasm),
        // already scaled to this occurrence's own quantity - null/empty for
        // every other Source, and also null for a BuyFromVendor decision
        // whose offer had no Item cost lines at all. Each entry's GoldValue
        // is the exact amount already folded into TotalCost for that line
        // (see VendorItemCostLine's own doc comment) - CraftingTreeBuilder
        // reads this to synthesize display-only cost-component leaves
        // without ever recomputing the fold.
        public IReadOnlyList<VendorItemCostLine> VendorItemCosts { get; internal set; }

        // W4B: true only when the winning BuyFromVendor decision's offer
        // had a genuine raw coin cost line (Type=="Currency",
        // Id==Gw2Constants.CoinCurrencyId, Count > 0) - distinct from coin
        // that exists only because an Item cost line got TP-valued and
        // folded in. False for every other Source. Used solely to decide
        // whether "coin" counts as one of the offer's 2+ cost KINDS when
        // deciding whether to synthesize component leaves (see
        // CraftingTreeBuilder.BuildVendorCostComponentLeaves) - a raw coin
        // component never gets its own leaf either way (see that method's
        // doc comment for why).
        public bool VendorHasRawCoin { get; internal set; }

        // W4B review-fix (Critical): true when this decision's
        // VendorCurrencyCosts/VendorItemCosts are stale relative to the
        // corrected TotalCost above - set only when PlanSolver.Solve's
        // AllocateVendorNodeCosts pass has reallocated a merged vendor
        // step's true cost across 2+ tree occurrences of the same item (see
        // PlanSolver.FlagUnreliableVendorComponentCosts' own doc comment).
        // CraftingTreeBuilder reads this to suppress cost-component leaf
        // synthesis whenever it is true, rather than display a component
        // number that can no longer be proven to sum to this decision's own
        // (corrected) TotalCost.
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

        // AUDIT ROW 20/38 (gw2e price-side fallback parity): true only when
        // Source is BuyFromTp and this node's committed unit price came
        // from the item's NON-preferred TP side because the preferred side
        // (per the solve's PriceBasis) had no listings - see
        // PlanSolver.GetUnitPrice's fallback overload. Always false for
        // every other Source. CraftingTreeBuilder reads this to flag the
        // matching CraftingTreeNode so the recipe-tree unit-price tooltip
        // can tell the user which side was actually used.
        public bool PriceSideFellBack { get; internal set; }

        // source-selection-simplification (maintainer-approved redesign,
        // docs/gw2e-considerations.md): raw cost breakdowns for EVERY
        // feasible source at this node, straight passthrough of
        // PlanSolver.Decision's own matching fields - see
        // PillSourceCostBreakdown's own doc comment for why these exist
        // independent of Source/TotalCost above (unlike VendorCurrencyCosts/
        // VendorItemCosts, populated for every source, not just the
        // winner). Always non-null (IsAvailable reflects CanCraft/CanBuyTp/
        // CanBuyVendor above). Feeds CraftingTreeNode's own matching fields
        // via CraftingTreeBuilder, ultimately consumed by
        // PillSubduingEvaluator - never read by any cost total.
        public PillSourceCostBreakdown CraftCostBreakdown { get; internal set; }
        public PillSourceCostBreakdown BuyFromTpCostBreakdown { get; internal set; }
        public PillSourceCostBreakdown BuyFromVendorCostBreakdown { get; internal set; }

        // Adversarial-review fix (#7, source-selection-simplification
        // design-law gap): straight passthrough of PlanSolver.Decision's
        // own matching fields - see that field's own doc comment. True
        // only when craft was excluded from the AUTOMATIC pick because no
        // character meets the winning recipe's discipline requirement
        // (never for the force-buy pre-pass's own, separately-explained
        // exclusion). CraftExcludedRealCost/Disciplines/MinRating describe
        // the recipe that would have won - only meaningful when
        // CraftExcludedByCompetency is true.
        public bool CraftExcludedByCompetency { get; internal set; }
        public long? CraftExcludedRealCost { get; internal set; }
        public IReadOnlyList<string> CraftExcludedDisciplines { get; internal set; }
        public int CraftExcludedMinRating { get; internal set; }
    }
}
