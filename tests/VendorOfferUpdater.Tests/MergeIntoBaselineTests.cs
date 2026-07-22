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
                MakeOffer("a", "Homestead Refinement—Farm"),
                MakeOffer("b", "Homestead Refinement—Farm"),
                MakeOffer("c", "Miyani")
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("d", "Homestead Refinement—Farm")
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
                MakeOffer("a", "Homestead Refinement—Farm"),
                MakeOffer("c", "Miyani"),
                MakeOffer("d", "Battle Master")
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("e", "Homestead Refinement—Farm")
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
                MakeOffer("stale1", "Homestead Refinement—Metal Forge"),
                MakeOffer("stale2", "Homestead Refinement—Metal Forge")
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("fresh1", "Homestead Refinement—Metal Forge")
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
                MakeOffer("a", "Homestead Refinement—Farm"),
                MakeOffer("b", "Homestead Refinement—Lumber Mill"),
                MakeOffer("c", "Miyani")
            };
            var fresh = new List<VendorOffer>
            {
                MakeOffer("d", "Homestead Refinement—Farm"),
                MakeOffer("e", "Homestead Refinement—Lumber Mill")
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
                MakeOffer("a", "Homestead Refinement—Farm"),
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
                MakeOffer("a", "Homestead Refinement—Farm")
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
                MakeOffer("bbb", "Homestead Refinement—Farm"),
                MakeOffer("aaa", "Homestead Refinement—Farm")
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
    }
}
