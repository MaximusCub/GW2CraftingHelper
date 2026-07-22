using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Tests.Helpers
{
    /// <summary>
    /// Shared VendorOffer builder helpers (M38 WP-20). CoinVendorOffer and
    /// MixedVendorOffer were private static methods on PlanSolverTests
    /// before that 2705-line file was split into focused test files - both
    /// helpers are called from several of the split files, so they moved
    /// here rather than being duplicated per file.
    /// </summary>
    public static class VendorOfferBuilders
    {
        public static VendorOffer CoinVendorOffer(
            int outputItemId, int coinCost, int outputCount = 1, int? dailyCap = null, int? weeklyCap = null,
            int? seasonalCap = null)
        {
            return new VendorOffer
            {
                OfferId = $"test-{outputItemId}-{coinCost}",
                OutputItemId = outputItemId,
                OutputCount = outputCount,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = coinCost }
                },
                MerchantName = "TestMerchant",
                Locations = new List<string> { "TestLoc" },
                DailyCap = dailyCap,
                WeeklyCap = weeklyCap,
                SeasonalCap = seasonalCap
            };
        }

        public static VendorOffer MixedVendorOffer(
            int outputItemId, int coinCost, int currencyId, int currencyCount, int outputCount = 1,
            int? dailyCap = null, int? weeklyCap = null, int? seasonalCap = null)
        {
            var costLines = new List<CostLine>();
            if (coinCost > 0)
            {
                costLines.Add(new CostLine
                {
                    Type = "Currency",
                    Id = Gw2Constants.CoinCurrencyId,
                    Count = coinCost
                });
            }
            costLines.Add(new CostLine { Type = "Currency", Id = currencyId, Count = currencyCount });

            return new VendorOffer
            {
                OfferId = "test-mixed-" + outputItemId + "-" + currencyId + "-" + currencyCount,
                OutputItemId = outputItemId,
                OutputCount = outputCount,
                CostLines = costLines,
                MerchantName = "Mixed Vendor",
                Locations = new List<string>(),
                DailyCap = dailyCap,
                WeeklyCap = weeklyCap,
                SeasonalCap = seasonalCap
            };
        }
    }
}
