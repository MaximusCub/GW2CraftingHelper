using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;
using static TaimisToolbench.Tests.Helpers.RecipeNodeBuilders;
using static TaimisToolbench.Tests.Helpers.RepoFileLocator;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// ref/acquisition_hints_seed.json is hand-maintained prose while
    /// ref/vendor_offers.json is re-scraped by tools/VendorOfferUpdater.
    /// Where both describe the same item they are two answers to one
    /// question, and nothing but these tests keeps them from diverging.
    /// The vendor record is the authority: a hint may add facts the record
    /// has no field for (an achievement, "not craftable"), but may not
    /// contradict the merchant or location it carries.
    /// </summary>
    public class AcquisitionHintSeedVendorAgreementTests
    {
        private static IReadOnlyDictionary<int, AcquisitionHint> LoadShippedHints()
        {
            string path = FindRepoFile(Path.Combine("ref", "acquisition_hints_seed.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/acquisition_hints_seed.json by walking up from the test assembly's directory.");
            using (var stream = File.OpenRead(path))
            {
                return AcquisitionHintService.Load(stream);
            }
        }

        private static List<VendorOffer> LoadShippedOffers()
        {
            string path = FindRepoFile(Path.Combine("ref", "vendor_offers.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/vendor_offers.json by walking up from the test assembly's directory.");
            using (var stream = File.OpenRead(path))
            {
                return new VendorOfferLoader().Load(stream).Offers;
            }
        }

        [Fact]
        public void ShippedHints_NoBadgeRendersAsAnAcquisitionSourcePill()
        {
            foreach (var hint in LoadShippedHints().Values)
            {
                Assert.False(
                    DecisionPillPlanner.IsReservedSourceBadgeText(hint.Badge),
                    $"Hint badge '{hint.Badge}' for item {hint.ItemId} renders identically to an " +
                    "acquisition-source pill, which means the opposite thing (a priced source whose " +
                    "cost is in Plan.TotalCoinCost, versus an Unknown node contributing 0). " +
                    "DecisionPillPlanner drops such a badge back to UNKNOWN, so shipping one only " +
                    "throws the hint's badge away.");
            }
        }

        [Fact]
        public void ShippedHints_ForItemsTheModuleAlsoShipsAVendorOfferFor_AgreeWithThatOffer()
        {
            var hints = LoadShippedHints();
            var offersByItem = LoadShippedOffers()
                .GroupBy(o => o.OutputItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var hintedItemsWithOffers = hints.Keys.Where(offersByItem.ContainsKey).ToList();

            // Trip-wire on the population itself: for the first seven
            // hints the module held no vendor data at all, and the
            // mechanism's implicit contract was "no source anywhere in our
            // data". These three break that precedent deliberately (see
            // KNOWN-ISSUES #8), so a fourth arriving unnoticed is
            // worth a manual look.
            Assert.Equal(
                new[] { 105804, 106712, 106986 },
                hintedItemsWithOffers.OrderBy(id => id).ToArray());

            foreach (int itemId in hintedItemsWithOffers)
            {
                string text = hints[itemId].Hint;
                var offers = offersByItem[itemId];

                Assert.True(
                    offers.Any(o => !string.IsNullOrEmpty(o.MerchantName) &&
                                    text.Contains(o.MerchantName)),
                    $"Hint for item {itemId} names no merchant the shipped vendor offer carries " +
                    $"(offer merchants: {string.Join(", ", offers.Select(o => o.MerchantName))}).");

                Assert.True(
                    offers.SelectMany(o => o.Locations ?? new List<string>())
                        .Any(loc => !string.IsNullOrEmpty(loc) && text.Contains(loc)),
                    $"Hint for item {itemId} names no location the shipped vendor offer carries " +
                    $"(offer locations: {string.Join(", ", offers.SelectMany(o => o.Locations ?? new List<string>()))}).");
            }
        }

        [Fact]
        public void ShippedBarterOffer_WithNoTradingPostPriceForItsCostItems_SolvesToUnknownSource()
        {
            // Why the three Endless Summer gifts read UNKNOWN even though
            // the module ships a vendor offer for each: VendorBatchSolver
            // drops a whole offer the moment one "Item" cost line has no TP
            // price, and these are barter offers paid in account-bound
            // tokens, which never have one. With no comparable and no
            // fallback offer left, PlanSolver commits UnknownSource.
            const int GiftOfTheSurvivors = 106712;

            var offers = LoadShippedOffers()
                .Where(o => o.OutputItemId == GiftOfTheSurvivors)
                .ToList();
            Assert.Single(offers);
            Assert.Contains(offers[0].CostLines, c => c.Type == "Item");

            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { GiftOfTheSurvivors, offers },
            };

            var result = new PlanSolver().Solve(
                Leaf(GiftOfTheSurvivors, 1),
                new Dictionary<int, ItemPrice>(),
                vendorOffers);

            var decision = result.Decisions[0];
            Assert.Equal(AcquisitionSource.UnknownSource, decision.Source);
            Assert.False(decision.CanBuyVendor);
            Assert.Null(decision.TotalCost);
        }
    }
}
