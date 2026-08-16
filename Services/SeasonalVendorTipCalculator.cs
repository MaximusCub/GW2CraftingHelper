using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// opportunity-notes (SEASONAL VENDOR TIP): pure, Blish-free post-solve
    /// annotation pass, same architectural role/placement precedent as
    /// ExcessCraftOutputCalculator - walks the plan's own shopping/craft
    /// steps (NOT the recipe tree - a seasonal offer is unconditionally
    /// excluded from solving, see SeasonalOfferFilter, so it never shows up
    /// as a chosen decision to walk) looking for an item where a
    /// currently-active festival vendor's offer beats this plan's own
    /// chosen unit price.
    ///
    /// vendorOffers here is the RAW (unfiltered) dictionary - the same one
    /// SeasonalOfferFilter strips seasonal offers OUT OF before it ever
    /// reaches the solver - so this is the one place in the pipeline that
    /// still needs to see them. activeFestivalNames is the plain-string
    /// projection Module.cs reads once from Blish's FestivalContext (see
    /// that class's own doc comment); empty/null here simply means no
    /// active festival, never a guess.
    ///
    /// Writes only CraftingPlanResult.SeasonalVendorTips. Never mutates
    /// Plan/any total - same "advisory" contract as
    /// ExcessCraftOutputCalculator's own doc comment.
    /// </summary>
    internal static class SeasonalVendorTipCalculator
    {
        internal static void Apply(
            CraftingPlanResult result,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            IReadOnlyDictionary<int, ItemPrice> prices,
            PriceBasis priceBasis,
            IReadOnlyList<string> activeFestivalNames)
        {
            if (result == null)
            {
                return;
            }

            var tips = new List<SeasonalVendorTip>();

            if (result.Plan?.Steps != null && vendorOffers != null && prices != null &&
                activeFestivalNames != null && activeFestivalNames.Count > 0)
            {
                var activeSet = new HashSet<string>(activeFestivalNames, StringComparer.Ordinal);
                var seenItemIds = new HashSet<int>();

                foreach (var step in result.Plan.Steps)
                {
                    if (!seenItemIds.Add(step.ItemId))
                    {
                        // One tip per item - matches ExcessCraftOutputCalculator's
                        // own per-item aggregation precedent (a step's ItemId is
                        // already unique per PlanSolver.Collect's aggregation, so
                        // this only guards a genuinely duplicated Steps list).
                        continue;
                    }

                    // A step priced partly in non-coin vendor currency is not
                    // fully represented by UnitCost alone - not safely
                    // comparable to a pure-coin seasonal offer (repo invariant:
                    // avoid invalid currency comparisons).
                    if (step.VendorCurrencyCosts != null && step.VendorCurrencyCosts.Count > 0)
                    {
                        continue;
                    }

                    if (!vendorOffers.TryGetValue(step.ItemId, out var offers) || offers == null)
                    {
                        continue;
                    }

                    foreach (var offer in offers)
                    {
                        if (offer == null ||
                            string.IsNullOrEmpty(offer.SeasonalFestival) ||
                            !activeSet.Contains(offer.SeasonalFestival) ||
                            offer.OutputCount <= 0)
                        {
                            continue;
                        }

                        if (!CostLineValuation.TryGetCoinCost(offer.CostLines, prices, priceBasis, out long coinCost))
                        {
                            continue;
                        }

                        long offerUnitCost = coinCost / offer.OutputCount;
                        if (offerUnitCost >= step.UnitCost)
                        {
                            continue;
                        }

                        tips.Add(new SeasonalVendorTip
                        {
                            ItemId = step.ItemId,
                            Festival = offer.SeasonalFestival,
                            MerchantName = offer.MerchantName,
                            CostLines = offer.CostLines,
                            OutputCount = offer.OutputCount,
                            OfferUnitCost = offerUnitCost,
                            PlanUnitPrice = step.UnitCost,
                            DailyCap = offer.DailyCap,
                            WeeklyCap = offer.WeeklyCap
                        });
                        // First qualifying offer is enough - one tip per item.
                        break;
                    }
                }
            }

            result.SeasonalVendorTips = tips;
        }
    }
}
