using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class SolverDecision
    {
        public AcquisitionSource Source { get; internal set; }
        public int RecipeId { get; internal set; }
        public long? TotalCost { get; internal set; }

        // Non-coin currency lines of a winning BuyFromVendor decision (e.g.
        // spirit shards, karma) - null/empty for every other Source. This is
        // the real, already-scaled-to-quantity cost that TotalCost cannot
        // represent (TotalCost is coin-only, see PlanSolver.Decision docs).
        // A later display task threads this into the tree/shopping UI.
        public IReadOnlyList<CostLine> VendorCurrencyCosts { get; internal set; }

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
    }
}
