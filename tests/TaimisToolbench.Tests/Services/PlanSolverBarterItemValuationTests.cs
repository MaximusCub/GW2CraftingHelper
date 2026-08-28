using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;
using static TaimisToolbench.Tests.Helpers.RecipeNodeBuilders;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// Barter offers: a vendor offer whose cost includes an untradeable
    /// Item line. Measured over ref/vendor_offers.json plus
    /// /v2/commerce/prices and /v2/items: 1,032 distinct item ids appear as
    /// vendor cost lines, 654 of them have no Trading Post price at all,
    /// and those 654 account for 10,551 of the 21,489 item cost-line
    /// usages (49%).
    ///
    /// <para>
    /// CHARACTERIZATION: these pin what the solver does with such a line
    /// TODAY - an Item cost line with no Trading Post price makes the WHOLE
    /// offer unpriceable, so the offer is dropped from both the comparable
    /// and the fallback tier and the item reports no vendor route at all.
    /// </para>
    /// </summary>
    public class PlanSolverBarterItemValuationTests
    {
        private const int BarterTokenItemId = 43992;

        private static VendorOffer BarterOffer(
            int outputItemId, int barterItemId, int barterCount, int coinCost = 0, int outputCount = 1)
        {
            var costLines = new List<CostLine>();
            if (coinCost > 0)
            {
                costLines.Add(new CostLine
                {
                    Type = "Currency",
                    Id = Gw2Constants.CoinCurrencyId,
                    Count = coinCost,
                });
            }

            costLines.Add(new CostLine { Type = "Item", Id = barterItemId, Count = barterCount });

            return new VendorOffer
            {
                OfferId = $"test-barter-{outputItemId}-{barterItemId}-{barterCount}",
                OutputItemId = outputItemId,
                OutputCount = outputCount,
                CostLines = costLines,
                MerchantName = "Barter Vendor",
                Locations = new List<string>(),
            };
        }

        private static Dictionary<int, IReadOnlyList<VendorOffer>> Offers(params VendorOffer[] offers)
        {
            var byOutput = new Dictionary<int, IReadOnlyList<VendorOffer>>();
            foreach (var group in offers.GroupBy(o => o.OutputItemId))
            {
                byOutput[group.Key] = group.ToList();
            }

            return byOutput;
        }

        [Fact]
        public void UnpricedItemCostLine_NoValuation_OnlyRouteAvailable()
        {
            // Nothing else can supply item 1: no TP price and no recipe. The
            // barter offer is the only acquisition route there is, and today
            // the solver still reports the item as unobtainable.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = Offers(BarterOffer(1, BarterTokenItemId, 5));
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);

            Assert.Equal(AcquisitionSource.UnknownSource, result.Decisions[0].Source);
            Assert.False(result.Decisions[0].CanBuyVendor);
        }

        [Fact]
        public void UnpricedItemCostLine_NoValuation_AgainstDearerTp()
        {
            // The fallback tier must never win a comparison against a real
            // coin cost: 5 account-bound tokens have no coin equivalent, so
            // a 1000-copper TP price is the only comparable option.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
            };
            var vendorOffers = Offers(BarterOffer(1, BarterTokenItemId, 5));
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);

            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[0].Source);
            Assert.False(result.Decisions[0].CanBuyVendor);
        }

        [Fact]
        public void UnpricedItemCostLine_MixedWithCoin_AgainstDearerTp()
        {
            // A 30-copper coin line on the same offer as the unpriced token:
            // the coin part alone would beat the 1000-copper TP price, so
            // this pins that the whole offer - coin line included - is
            // discarded rather than the token line alone being dropped.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
            };
            var vendorOffers = Offers(BarterOffer(1, BarterTokenItemId, 5, coinCost: 30));
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);

            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[0].Source);
            Assert.False(result.Decisions[0].CanBuyVendor);
        }

        [Fact]
        public void UnpricedItemCostLine_SecondFullyPricedOfferStillFound()
        {
            // Two offers for the same item, one unpriceable: the priceable
            // one must still be found. Guards the `break`/`continue` pair -
            // an unpriceable offer aborts its OWN evaluation, never the loop.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
            };
            var vendorOffers = Offers(
                BarterOffer(1, BarterTokenItemId, 5),
                new VendorOffer
                {
                    OfferId = "test-coin-alt",
                    OutputItemId = 1,
                    OutputCount = 1,
                    CostLines = new List<CostLine>
                    {
                        new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 40 },
                    },
                    MerchantName = "Coin Vendor",
                    Locations = new List<string>(),
                });
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);

            Assert.Equal(AcquisitionSource.BuyFromVendor, result.Decisions[0].Source);
            Assert.Equal(40, result.Plan.Steps.Single(s => s.ItemId == 1).TotalCost);
        }

        [Fact]
        public void TpPricedItemCostLine_FoldsIntoRealCoinCost()
        {
            // The control: a barter item that DOES have a TP price is money,
            // and 5 * 10 = 50 copper of it is folded into the offer's real
            // coin cost.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { BarterTokenItemId, new ItemPrice { ItemId = BarterTokenItemId, BuyInstant = 10 } },
            };
            var vendorOffers = Offers(BarterOffer(1, BarterTokenItemId, 5));
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);

            Assert.Equal(AcquisitionSource.BuyFromVendor, result.Decisions[0].Source);
            Assert.Equal(50, result.Decisions[0].ComparisonValue);
            Assert.Equal(50, result.Plan.Steps.Single(s => s.ItemId == 1).TotalCost);
        }
    }
}
