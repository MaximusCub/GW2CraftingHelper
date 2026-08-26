using System.Collections.Generic;
using System.Linq;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// The diff report is the only reviewable artifact a `data(vendor):` change
    /// produces - `git diff` on ref/vendor_offers.json is one 14.8MB line. If it
    /// miscounts, or classifies a repricing as an unrelated add plus remove, a
    /// reviewer reads a summary that is worse than none.
    /// </summary>
    public class VendorOfferDiffTests
    {
        private static VendorOffer Offer(
            int itemId,
            string merchant,
            int coinCost,
            int outputCount = 1,
            string festival = null,
            int? weeklyCap = null)
        {
            var offer = new VendorOffer
            {
                OutputItemId = itemId,
                OutputCount = outputCount,
                MerchantName = merchant,
                Locations = null,
                WeeklyCap = weeklyCap,
                SeasonalFestival = festival,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = 1, Count = coinCost },
                },
            };

            // The real dataset's offerId is a content hash over every field but
            // SeasonalFestival. Computing it the same way here is what makes
            // these tests exercise the classification the tool actually faces:
            // a price change genuinely produces a different id, not a mutation.
            offer.OfferId = VendorOfferHasher.ComputeOfferId(
                offer.OutputItemId,
                offer.OutputCount,
                offer.CostLines,
                offer.MerchantName,
                offer.Locations,
                offer.DailyCap,
                offer.WeeklyCap,
                offer.HomesteadTier,
                offer.SeasonalCap);

            return offer;
        }

        [Fact]
        public void IdenticalDatasets_ReportNoChange()
        {
            var a = new List<VendorOffer> { Offer(1, "Miyani", 100), Offer(2, "Miyani", 200) };
            var b = new List<VendorOffer> { Offer(1, "Miyani", 100), Offer(2, "Miyani", 200) };

            var result = VendorOfferDiff.Compute(a, b);

            Assert.True(result.IsEmpty);
            Assert.Contains("No offer changed", VendorOfferDiff.Format(result, "old", "new"));
        }

        /// <summary>
        /// The whole reason this class exists. offerId hashes the cost, so a
        /// price move deletes one id and creates another; reported raw, that is
        /// two unrelated hex strings instead of "this got more expensive".
        /// </summary>
        [Fact]
        public void PriceChange_IsOneRepricing_NotAnAddPlusARemove()
        {
            var before = new List<VendorOffer> { Offer(1, "Miyani", 100) };
            var after = new List<VendorOffer> { Offer(1, "Miyani", 250) };

            Assert.NotEqual(before[0].OfferId, after[0].OfferId);

            var result = VendorOfferDiff.Compute(before, after);

            Assert.Empty(result.Added);
            Assert.Empty(result.Removed);
            Assert.Single(result.Repriced);
            Assert.Equal(100, result.Repriced[0].Before.CostLines[0].Count);
            Assert.Equal(250, result.Repriced[0].After.CostLines[0].Count);
            Assert.Contains("100x currency 1 -> 250x currency 1",
                VendorOfferDiff.Format(result, "old", "new"));
        }

        [Fact]
        public void CapChangeWithSamePrice_IsAlsoARepricing()
        {
            var before = new List<VendorOffer> { Offer(1, "Miyani", 100) };
            var after = new List<VendorOffer> { Offer(1, "Miyani", 100, weeklyCap: 5) };

            var result = VendorOfferDiff.Compute(before, after);

            Assert.Single(result.Repriced);
            Assert.Contains("weekly cap 5", VendorOfferDiff.Format(result, "old", "new"));
        }

        [Fact]
        public void NewAndDroppedMerchantItemPairs_AreAddsAndRemoves()
        {
            var before = new List<VendorOffer> { Offer(1, "Miyani", 100), Offer(2, "Miyani", 100) };
            var after = new List<VendorOffer> { Offer(1, "Miyani", 100), Offer(3, "Arriske", 50) };

            var result = VendorOfferDiff.Compute(before, after);

            Assert.Single(result.Added);
            Assert.Equal(3, result.Added[0].OutputItemId);
            Assert.Single(result.Removed);
            Assert.Equal(2, result.Removed[0].OutputItemId);
            Assert.Empty(result.Repriced);
        }

        /// <summary>
        /// SeasonalFestival is the one field the offerId hash excludes, so a
        /// retag keeps the id. Reporting it as "nothing changed" would hide a
        /// tag regression, which is a real failure mode this dataset has had.
        /// </summary>
        [Fact]
        public void SeasonalFestivalRetag_KeepsTheIdAndIsReportedSeparately()
        {
            var before = new List<VendorOffer> { Offer(1, "Miyani", 100) };
            var after = new List<VendorOffer> { Offer(1, "Miyani", 100, festival: "Wintersday") };

            Assert.Equal(before[0].OfferId, after[0].OfferId);

            var result = VendorOfferDiff.Compute(before, after);

            Assert.Single(result.Retagged);
            Assert.Empty(result.Added);
            Assert.Empty(result.Removed);
            Assert.Empty(result.Repriced);
            Assert.Contains("festival (none) -> Wintersday",
                VendorOfferDiff.Format(result, "old", "new"));
        }

        /// <summary>
        /// The offerId is a hash the tool computes, not a guarantee the data
        /// carries. A hand-edited row keeps its old id while its price moves;
        /// trusting the id alone would report that row as unchanged, in the one
        /// situation a reviewer most needs the report not to lie.
        /// </summary>
        [Fact]
        public void SameIdWithDifferentContent_IsStillReportedAsARepricing()
        {
            var before = new List<VendorOffer> { Offer(1, "Miyani", 100) };
            var after = new List<VendorOffer> { Offer(1, "Miyani", 100) };
            after[0].CostLines[0].Count = 4000; // hand-edit: id left stale

            Assert.Equal(before[0].OfferId, after[0].OfferId);

            var result = VendorOfferDiff.Compute(before, after);

            Assert.Single(result.Repriced);
            Assert.Empty(result.Retagged);
            Assert.Contains("100x currency 1 -> 4000x currency 1",
                VendorOfferDiff.Format(result, "old", "new"));
        }

        [Fact]
        public void SurplusRowsForOneMerchantItemPair_FallThroughAsAddsAndRemoves()
        {
            var before = new List<VendorOffer> { Offer(1, "Miyani", 100) };
            var after = new List<VendorOffer>
            {
                Offer(1, "Miyani", 150),
                Offer(1, "Miyani", 150, outputCount: 5),
            };

            var result = VendorOfferDiff.Compute(before, after);

            Assert.Single(result.Repriced);
            Assert.Single(result.Added);
            Assert.Empty(result.Removed);
        }

        [Fact]
        public void EmptyAndNullInputs_AreHandledWithoutThrowing()
        {
            Assert.True(VendorOfferDiff.Compute(null, null).IsEmpty);

            var added = VendorOfferDiff.Compute(
                new List<VendorOffer>(), new List<VendorOffer> { Offer(1, "Miyani", 100) });
            Assert.Single(added.Added);
            Assert.Equal(0, added.OldCount);
            Assert.Equal(1, added.NewCount);

            var removed = VendorOfferDiff.Compute(
                new List<VendorOffer> { Offer(1, "Miyani", 100) }, new List<VendorOffer>());
            Assert.Single(removed.Removed);
        }

        [Fact]
        public void CountsInTheHeaderAreExactEvenWhenTheListingIsTruncated()
        {
            int rows = VendorOfferDiff.MaxListedPerSection + 7;
            var after = Enumerable.Range(1, rows)
                .Select(i => Offer(i, "Miyani", 100))
                .ToList();

            var result = VendorOfferDiff.Compute(new List<VendorOffer>(), after);
            string report = VendorOfferDiff.Format(result, "old", "new");

            Assert.Equal(rows, result.Added.Count);
            Assert.Contains($"added:    {rows}", report);
            Assert.Contains("... and 7 more", report);
        }

        /// <summary>
        /// Two runs over the same data must produce the same report text, or
        /// the summary cannot be pasted into a PR body and trusted.
        /// </summary>
        [Fact]
        public void ReportTextIsStableAcrossRuns()
        {
            var before = new List<VendorOffer>
            {
                Offer(2, "Zho", 100), Offer(1, "Miyani", 100), Offer(3, "Arriske", 100),
            };
            var after = new List<VendorOffer>
            {
                Offer(3, "Arriske", 200), Offer(1, "Miyani", 300), Offer(9, "Zho", 100),
            };

            string first = VendorOfferDiff.Format(VendorOfferDiff.Compute(before, after), "a", "b");
            string second = VendorOfferDiff.Format(VendorOfferDiff.Compute(before, after), "a", "b");

            Assert.Equal(first, second);
        }
    }
}
