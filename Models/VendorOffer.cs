using System.Collections.Generic;

namespace TaimisToolbench.Models
{
    internal class VendorOffer
    {
        public string OfferId { get; set; }

        public int OutputItemId { get; set; }

        public int OutputCount { get; set; }

        public List<CostLine> CostLines { get; set; } = new List<CostLine>();

        public string MerchantName { get; set; }

        public List<string> Locations { get; set; } = new List<string>();

        public int? DailyCap { get; set; }

        public int? WeeklyCap { get; set; }

        // Astral Acclaim package: Wizard's Vault
        // seasonal purchase cap (resets each Vault season, wiki property
        // "Has seasonal purchase cap"), or null for every non-Vault offer.
        // Additive, backward-compatible - existing offers deserialize with
        // this null. Consumed by VendorBatchSolver.FinalizeVendorBatches
        // exactly like DailyCap/WeeklyCap (warn-only, never gates or
        // reroutes the solve) via TimegatedCapType.Seasonal - see that
        // method's doc comment.
        public int? SeasonalCap { get; set; }

        // The Homestead Refinement
        // efficiency tier (0/1/2) this specific offer row corresponds to,
        // or null for every non-Homestead-Refinement offer. Additive,
        // backward-compatible - existing offers deserialize with this null.
        // Wiki-sourced per-row quantities already bake in the game's own
        // per-material tier anomalies (Onion/Potato/Iron Ore), so tagging
        // existing rows rather than collapsing them into a formula avoids
        // re-deriving those bugs in code - see VendorBatchSolver.EvaluateVendorOffers.
        public int? HomesteadTier { get; set; }

        // The festival this offer
        // is only available during (Blish_HUD.Contexts.FestivalContext.
        // Festival.Name, e.g. "halloween" - lowercase, MEASURED, see
        // Gw2Constants.HalloweenFestivalName), or null for every regular,
        // year-round offer. Additive, backward-compatible - existing
        // offers deserialize with this null. NEVER read by the solver
        // (VendorBatchSolver/PlanSolver) - a seasonal offer is
        // unconditionally excluded from the solver's own candidate set
        // regardless of this value (see Services/SeasonalOfferFilter) - the
        // plan always assumes the regular market. Consumed only by
        // Services/SeasonalVendorTipCalculator for the informational Plan
        // Notes tip.
        public string SeasonalFestival { get; set; }
    }
}
