using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;
using static GW2CraftingHelper.Tests.Helpers.VendorOfferBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanSolverCapNoticeSuppressionTests
    {
        // --- M37: Homestead mixed-offer cap-notice gap (KNOWN-ISSUES
        // #24/#25 Section 3.3) - a fix was attempted here (summing each
        // occurrence's own true purchase count when occurrences disagreed
        // on the winning offer but agreed on the raw (DailyCap, WeeklyCap)
        // tuple) but reverted after adversarial review: the wiki's per-row
        // WeeklyCap the Homestead seed data carries is a template
        // parameter, not a confirmed per-station aggregate (see
        // KNOWN-ISSUES #24's "Cap data" note), so two occurrences agreeing
        // on that raw number does not mean they agree on a real shared
        // limit worth summing against - and every Homestead row within one
        // station shares that same number, so the summing branch fired for
        // the ordinary case, not a rare edge case. The pre-existing
        // suppress-on-Conflict behavior is kept; both tests below document
        // that as an intentional, narrower limitation rather than a silent
        // regression risk. ---

        [Fact]
        public void MixedOfferSameWeeklyCap_NoticeStillSuppressed_DocumentedLimitation()
        {
            // Same bulk-discount-threshold shape as
            // MultiOccurrenceDifferentWinningOffers_LeavesPerOccurrenceSumUnmerged
            // (qty=1 deterministically favors the 1-for-2 offer, qty=100
            // deterministically favors the 100-for-150 offer - genuine
            // disagreement, not a tie). Both offers happen to share the
            // identical WeeklyCap=1 (the normal Homestead shape - every
            // offer at one station carries the same wiki-scraped per-row
            // number), but Conflict (the offer-shape ratchet) alone still
            // suppresses the notice: there is no verified single cap to
            // check the mixed-offer total against.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(99, 1), Leaf(99, 100)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    99, new List<VendorOffer>
                    {
                        CoinVendorOffer(99, 2, outputCount: 1, weeklyCap: 1),
                        CoinVendorOffer(99, 150, outputCount: 100, weeklyCap: 1)
                    }
                }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy);
            var plan = result.Plan;

            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            // Confirms Conflict actually ratcheted true here (matching the
            // pre-existing sibling test's own proof for this exact shape),
            // so the empty-notice assertion below is testing genuine
            // Conflict suppression, not merely "no cap was ever exceeded".
            Assert.Equal(0, vendorStep.VendorOfferOutputCount);
            Assert.Equal(152, vendorStep.TotalCost);

            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void MixedOfferDifferentWeeklyCap_NoticeStillSuppressed_DocumentedLimitation()
        {
            // Same bulk-discount-threshold shape as
            // MultiOccurrenceDifferentWinningOffers_LeavesPerOccurrenceSumUnmerged
            // (qty=1 favors the 1-for-2 offer, qty=100 favors the
            // 100-for-150 offer - genuine, deterministic disagreement, not
            // a tie), but this time the two offers ALSO carry different
            // WeeklyCap values. Whether or not the raw cap number happens to
            // match across occurrences, Conflict alone suppresses the
            // notice - same as before this milestone. This documents that
            // limitation as intentional rather than a silent regression
            // risk.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(99, 1),
                    Leaf(99, 100)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    99, new List<VendorOffer>
                    {
                        CoinVendorOffer(99, 2, outputCount: 1, weeklyCap: 5),
                        CoinVendorOffer(99, 150, outputCount: 100, weeklyCap: 999)
                    }
                }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy).Plan;

            // Confirms Conflict actually ratcheted true here (matching the
            // pre-existing sibling test's own proof for this exact shape),
            // so the empty-notice assertion below is testing genuine
            // Conflict suppression, not merely "no cap was ever exceeded".
            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            Assert.Equal(0, vendorStep.VendorOfferOutputCount);

            Assert.Empty(plan.TimegatedItems);
        }

        // --- Adversarial review of the M37 mixed-offer Weekly pair above
        // found the Conflict-suppression parity claim for the KNOWN-ISSUES
        // #33 SeasonalCap package unverified: FinalizeVendorBatches checks
        // Seasonal inside the exact same "!state.Conflict" guard as Daily/
        // Weekly (an implementation coincidence, not a pinned contract), so
        // nothing failed if that guard were ever hoisted apart for Seasonal
        // specifically. These two tests mirror the Weekly pair exactly,
        // substituting seasonalCap for weeklyCap, to pin the same suppress-
        // on-Conflict behavior for Seasonal. ---

        [Fact]
        public void MixedOfferSameSeasonalCap_NoticeStillSuppressed_DocumentedLimitation()
        {
            // Same bulk-discount-threshold shape as
            // MixedOfferSameWeeklyCap_NoticeStillSuppressed_DocumentedLimitation
            // (qty=1 deterministically favors the 1-for-2 offer, qty=100
            // deterministically favors the 100-for-150 offer - genuine
            // disagreement, not a tie). Both offers happen to share the
            // identical SeasonalCap=1, but Conflict (the offer-shape
            // ratchet) alone still suppresses the notice - same as Weekly.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(99, 1), Leaf(99, 100)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    99, new List<VendorOffer>
                    {
                        CoinVendorOffer(99, 2, outputCount: 1, seasonalCap: 1),
                        CoinVendorOffer(99, 150, outputCount: 100, seasonalCap: 1)
                    }
                }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy);
            var plan = result.Plan;

            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            // Confirms Conflict actually ratcheted true here (matching the
            // pre-existing Weekly sibling test's own proof for this exact
            // shape), so the empty-notice assertion below is testing genuine
            // Conflict suppression, not merely "no cap was ever exceeded".
            Assert.Equal(0, vendorStep.VendorOfferOutputCount);
            Assert.Equal(152, vendorStep.TotalCost);

            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void MixedOfferDifferentSeasonalCap_NoticeStillSuppressed_DocumentedLimitation()
        {
            // Same bulk-discount-threshold shape as
            // MixedOfferDifferentWeeklyCap_NoticeStillSuppressed_DocumentedLimitation
            // (qty=1 favors the 1-for-2 offer, qty=100 favors the
            // 100-for-150 offer - genuine, deterministic disagreement, not
            // a tie), but this time the two offers ALSO carry different
            // SeasonalCap values. Whether or not the raw cap number happens
            // to match across occurrences, Conflict alone suppresses the
            // notice - same as Weekly.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(99, 1),
                    Leaf(99, 100)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    99, new List<VendorOffer>
                    {
                        CoinVendorOffer(99, 2, outputCount: 1, seasonalCap: 5),
                        CoinVendorOffer(99, 150, outputCount: 100, seasonalCap: 999)
                    }
                }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy).Plan;

            // Confirms Conflict actually ratcheted true here (matching the
            // pre-existing Weekly sibling test's own proof for this exact
            // shape), so the empty-notice assertion below is testing genuine
            // Conflict suppression, not merely "no cap was ever exceeded".
            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            Assert.Equal(0, vendorStep.VendorOfferOutputCount);

            Assert.Empty(plan.TimegatedItems);
        }
    }
}
