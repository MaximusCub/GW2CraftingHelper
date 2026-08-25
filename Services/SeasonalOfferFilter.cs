using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// opportunity-notes (SEASONAL VENDOR TIP, maintainer decision): a
    /// seasonal vendor offer (VendorOffer.SeasonalFestival non-null) is
    /// UNCONDITIONALLY excluded from the solver's own offer set - the plan
    /// always assumes the regular market, regardless of whether the
    /// festival is currently active. This is deliberately NOT gated on
    /// festival-active state: an inactive seasonal offer was never
    /// solvable anyway (the shop is closed), and an ACTIVE one is still
    /// excluded on purpose so a plan never silently depends on a
    /// time-limited vendor that may not be there next time the plan is
    /// reopened - the informational Plan Notes tip (SeasonalVendorTipCalculator)
    /// is how an active, cheaper festival offer gets surfaced instead.
    ///
    /// CraftingPlanPipeline is the sole caller, applied ONLY to the
    /// dictionary handed to PlanSolver.Solve/OwnedMaterialsForceBuyPrePass
    /// (never to the raw vendorOffers dict itself, which stays available
    /// for metadata widening, owned-amount annotation, and
    /// SeasonalVendorTipCalculator - see each call site's own comment).
    /// </summary>
    internal static class SeasonalOfferFilter
    {
        internal static IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> ExcludeSeasonal(
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> offers)
        {
            if (offers == null || offers.Count == 0)
            {
                return offers;
            }

            // Fast path (the overwhelming common case: zero seasonal
            // offers anywhere in this plan's item set): no allocation,
            // return the same dictionary reference unchanged.
            bool anySeasonal = false;
            foreach (var list in offers.Values)
            {
                if (list == null)
                {
                    continue;
                }

                foreach (var offer in list)
                {
                    if (offer != null && !string.IsNullOrEmpty(offer.SeasonalFestival))
                    {
                        anySeasonal = true;
                        break;
                    }
                }

                if (anySeasonal)
                {
                    break;
                }
            }

            if (!anySeasonal)
            {
                return offers;
            }

            var filtered = new Dictionary<int, IReadOnlyList<VendorOffer>>(offers.Count);
            foreach (var kvp in offers)
            {
                if (kvp.Value == null)
                {
                    filtered[kvp.Key] = kvp.Value;
                    continue;
                }

                bool listHasSeasonal = false;
                foreach (var offer in kvp.Value)
                {
                    if (offer != null && !string.IsNullOrEmpty(offer.SeasonalFestival))
                    {
                        listHasSeasonal = true;
                        break;
                    }
                }

                if (!listHasSeasonal)
                {
                    // Unaffected key - reuse the same List reference.
                    filtered[kvp.Key] = kvp.Value;
                    continue;
                }

                var kept = new List<VendorOffer>(kvp.Value.Count);
                foreach (var offer in kvp.Value)
                {
                    if (offer == null || string.IsNullOrEmpty(offer.SeasonalFestival))
                    {
                        kept.Add(offer);
                    }
                }

                filtered[kvp.Key] = kept;
            }

            return filtered;
        }
    }
}
