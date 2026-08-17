using System.Collections.Generic;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    // M37 (KNOWN-ISSUES #24): Program.MergeIntoBaseline is what
    // "regenerate ONLY those pages' rows" actually means at the data
    // level - a scoped re-scrape of a handful of merchants must replace
    // only those merchants' offers in the full baseline, leaving every
    // other merchant's rows byte-for-byte untouched.
    public class MergeIntoBaselineTests
    {
        private static VendorOffer MakeOffer(
            string offerId, string merchantName, int outputItemId = 1)
        {
            return new VendorOffer
            {
                OfferId = offerId,
                OutputItemId = outputItemId,
                OutputCount = 1,
                CostLines = new List<CostLine>(),
                MerchantName = merchantName,
                Locations = new List<string>()
            };
        }

        [Fact]
        public void FreshMerchant_ReplacesAllBaselineOffersForThatMerchant()
        {
            var baseline = new List<VendorOffer>
            {
                MakeOffer("a", "Homestead Refinement\u2014Farm"),
                MakeOffer("b", "Homestead Refinement\u2014Farm"),
                MakeOffer("c", "Miyani")
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("d", "Homestead Refinement\u2014Farm")
            };

            var result = Program.MergeIntoBaseline(baseline, fresh);

            Assert.Equal(2, result.RemovedFromBaseline);
            Assert.Equal(2, result.Merged.Count);
            Assert.Contains(result.Merged, o => o.OfferId == "c");
            Assert.Contains(result.Merged, o => o.OfferId == "d");
            Assert.DoesNotContain(result.Merged, o => o.OfferId == "a" || o.OfferId == "b");
        }

        [Fact]
        public void UntouchedMerchants_PassThroughUnchanged()
        {
            var baseline = new List<VendorOffer>
            {
                MakeOffer("a", "Homestead Refinement\u2014Farm"),
                MakeOffer("c", "Miyani"),
                MakeOffer("d", "Battle Master")
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("e", "Homestead Refinement\u2014Farm")
            };

            var result = Program.MergeIntoBaseline(baseline, fresh);

            Assert.Contains(result.Merged, o => o.OfferId == "c");
            Assert.Contains(result.Merged, o => o.OfferId == "d");
            Assert.Equal(3, result.Merged.Count);
        }

        [Fact]
        public void StaleBaselineRowForReplacedMerchant_RemovedEvenIfFreshQueryDidNotReFindIt()
        {
            // The fresh query found 1 row for the merchant; the baseline
            // had 2 - the extra one must be dropped (it is stale), not kept
            // alongside the fresh row.
            var baseline = new List<VendorOffer>
            {
                MakeOffer("stale1", "Homestead Refinement\u2014Metal Forge"),
                MakeOffer("stale2", "Homestead Refinement\u2014Metal Forge")
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("fresh1", "Homestead Refinement\u2014Metal Forge")
            };

            var result = Program.MergeIntoBaseline(baseline, fresh);

            Assert.Equal(2, result.RemovedFromBaseline);
            Assert.Single(result.Merged);
            Assert.Equal("fresh1", result.Merged[0].OfferId);
        }

        [Fact]
        public void MultipleFreshMerchants_EachReplacedIndependently()
        {
            var baseline = new List<VendorOffer>
            {
                MakeOffer("a", "Homestead Refinement\u2014Farm"),
                MakeOffer("b", "Homestead Refinement\u2014Lumber Mill"),
                MakeOffer("c", "Miyani")
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("d", "Homestead Refinement\u2014Farm"),
                MakeOffer("e", "Homestead Refinement\u2014Lumber Mill")
            };

            var result = Program.MergeIntoBaseline(baseline, fresh);

            Assert.Equal(2, result.RemovedFromBaseline);
            Assert.Equal(2, result.MerchantNamesReplaced.Count);
            Assert.Equal(3, result.Merged.Count);
        }

        [Fact]
        public void EmptyFresh_LeavesBaselineCompletelyUnchanged()
        {
            var baseline = new List<VendorOffer>
            {
                MakeOffer("a", "Homestead Refinement\u2014Farm"),
                MakeOffer("b", "Miyani")
            };

            var result = Program.MergeIntoBaseline(baseline, new List<VendorOffer>());

            Assert.Equal(0, result.RemovedFromBaseline);
            Assert.Equal(2, result.Merged.Count);
        }

        [Fact]
        public void EmptyBaseline_MergedIsJustFresh()
        {
            var fresh = new List<VendorOffer>
            {
                MakeOffer("a", "Homestead Refinement\u2014Farm")
            };

            var result = Program.MergeIntoBaseline(new List<VendorOffer>(), fresh);

            Assert.Equal(0, result.RemovedFromBaseline);
            Assert.Single(result.Merged);
        }

        [Fact]
        public void MergedResult_IsSortedByOfferId()
        {
            var baseline = new List<VendorOffer>
            {
                MakeOffer("zzz", "Miyani")
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("bbb", "Homestead Refinement\u2014Farm"),
                MakeOffer("aaa", "Homestead Refinement\u2014Farm")
            };

            var result = Program.MergeIntoBaseline(baseline, fresh);

            Assert.Equal(new[] { "aaa", "bbb", "zzz" },
                result.Merged.ConvertAll(o => o.OfferId));
        }

        [Fact]
        public void NullBaselineOrFresh_TreatedAsEmpty()
        {
            var result1 = Program.MergeIntoBaseline(null, new List<VendorOffer> { MakeOffer("a", "M") });
            Assert.Single(result1.Merged);

            var result2 = Program.MergeIntoBaseline(new List<VendorOffer> { MakeOffer("a", "M") }, null);
            Assert.Single(result2.Merged);
        }

        // DATA LOSS fix (2026-08-17): a merchant flagged as having had a
        // GameId<=0 row this pass must NOT have its baseline offers
        // dropped, even though it also appears in the fresh batch -
        // exactly the mechanism that silently deleted 6 shipped offers in
        // a real run (Program.cs's own GameId<=0 filter meant the fresh
        // batch for that merchant was known-incomplete).
        [Fact]
        public void ProtectedMerchant_BaselineOffersSurviveAlongsideFreshOnes()
        {
            var baseline = new List<VendorOffer>
            {
                MakeOffer("stale-a", "Festival Rewards Vendor (Weekly)", 1),
                MakeOffer("stale-b", "Festival Rewards Vendor (Weekly)", 2),
                MakeOffer("c", "Miyani", 3)
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("fresh-a", "Festival Rewards Vendor (Weekly)", 4)
            };
            var skipped = new HashSet<string> { "Festival Rewards Vendor (Weekly)" };

            var result = Program.MergeIntoBaseline(baseline, fresh, skipped);

            Assert.Equal(0, result.RemovedFromBaseline);
            Assert.Equal(4, result.Merged.Count);
            Assert.Contains(result.Merged, o => o.OfferId == "stale-a");
            Assert.Contains(result.Merged, o => o.OfferId == "stale-b");
            Assert.Contains(result.Merged, o => o.OfferId == "fresh-a");
            Assert.Contains(result.Merged, o => o.OfferId == "c");
            Assert.Equal(
                new[] { "Festival Rewards Vendor (Weekly)" },
                result.MerchantNamesProtected);
            Assert.Empty(result.MerchantNamesReplaced);
        }

        // A run can have BOTH an unaffected merchant (wholesale-replaced
        // as normal) and a protected one (skipped-row merchant) in the
        // same fresh batch - each merchant's own outcome must be
        // independent.
        [Fact]
        public void MixOfProtectedAndUnprotectedMerchants_EachHandledIndependently()
        {
            var baseline = new List<VendorOffer>
            {
                MakeOffer("stale-protected", "Wintersday Trader (Weekly)", 1),
                MakeOffer("stale-replaced", "Homestead Refinement\u2014Farm", 2)
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("fresh-protected", "Wintersday Trader (Weekly)", 3),
                MakeOffer("fresh-replaced", "Homestead Refinement\u2014Farm", 4)
            };
            var skipped = new HashSet<string> { "Wintersday Trader (Weekly)" };

            var result = Program.MergeIntoBaseline(baseline, fresh, skipped);

            Assert.Equal(1, result.RemovedFromBaseline);
            Assert.Contains(result.Merged, o => o.OfferId == "stale-protected");
            Assert.DoesNotContain(result.Merged, o => o.OfferId == "stale-replaced");
            Assert.Contains(result.Merged, o => o.OfferId == "fresh-protected");
            Assert.Contains(result.Merged, o => o.OfferId == "fresh-replaced");
            Assert.Equal(
                new[] { "Wintersday Trader (Weekly)" }, result.MerchantNamesProtected);
            Assert.Equal(
                new[] { "Homestead Refinement\u2014Farm" }, result.MerchantNamesReplaced);
        }

        [Fact]
        public void MergedResult_DedupesByOfferId_WhenProtectedBaselineAndFreshShareAnId()
        {
            // Same OfferId means content-identical (VendorOfferHasher is a
            // content hash) - the union must not produce a visible
            // duplicate row.
            var baseline = new List<VendorOffer>
            {
                MakeOffer("shared", "Festival Rewards Vendor (Weekly)", 1)
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("shared", "Festival Rewards Vendor (Weekly)", 1)
            };
            var skipped = new HashSet<string> { "Festival Rewards Vendor (Weekly)" };

            var result = Program.MergeIntoBaseline(baseline, fresh, skipped);

            Assert.Single(result.Merged);
        }
    }
}
