using System.Collections.Generic;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    public class MergeWikiCacheTests
    {
        private static WikiVendorResult MakeResult(
            string pageName,
            int? dailyCap = null,
            int? weeklyCap = null)
        {
            return new WikiVendorResult
            {
                PageName = pageName,
                GameId = 1,
                MerchantName = "Merchant",
                OutputQuantity = 1,
                CostEntries = new List<WikiCostEntry>(),
                Locations = new List<string>(),
                DailyCap = dailyCap,
                WeeklyCap = weeklyCap
            };
        }

        [Fact]
        public void FreshResultForExistingPage_OverwritesCachedFields()
        {
            // Exact bug scenario: a page was cached before purchase-cap data
            // existed, then re-queried with the new DailyCap populated. The
            // fresh copy must win, not the stale cached copy.
            var existing = new List<WikiVendorResult>
            {
                MakeResult("Some Item", dailyCap: null)
            };
            var fresh = new List<WikiVendorResult>
            {
                MakeResult("Some Item", dailyCap: 5)
            };

            var result = Program.MergeWikiCache(existing, fresh);

            Assert.Single(result.Merged);
            Assert.Equal(5, result.Merged[0].DailyCap);
            Assert.Equal(0, result.Added);
            Assert.Equal(1, result.Refreshed);
            Assert.Equal(0, result.Unchanged);
        }

        [Fact]
        public void CachedPageAbsentFromFreshBatch_IsPreservedUnchanged()
        {
            var existing = new List<WikiVendorResult>
            {
                MakeResult("Untouched Page", dailyCap: 3),
                MakeResult("Refreshed Page", dailyCap: null)
            };
            var fresh = new List<WikiVendorResult>
            {
                MakeResult("Refreshed Page", dailyCap: 7)
            };

            var result = Program.MergeWikiCache(existing, fresh);

            Assert.Equal(2, result.Merged.Count);
            var untouched = Assert.Single(result.Merged, r => r.PageName == "Untouched Page");
            Assert.Equal(3, untouched.DailyCap);
            var refreshed = Assert.Single(result.Merged, r => r.PageName == "Refreshed Page");
            Assert.Equal(7, refreshed.DailyCap);
            Assert.Equal(0, result.Added);
            Assert.Equal(1, result.Refreshed);
            Assert.Equal(1, result.Unchanged);
        }

        [Fact]
        public void FreshPageNotInExistingCache_IsAdded()
        {
            var existing = new List<WikiVendorResult>
            {
                MakeResult("Existing Page")
            };
            var fresh = new List<WikiVendorResult>
            {
                MakeResult("New Page")
            };

            var result = Program.MergeWikiCache(existing, fresh);

            Assert.Equal(2, result.Merged.Count);
            Assert.Equal(1, result.Added);
            Assert.Equal(0, result.Refreshed);
            Assert.Equal(1, result.Unchanged);
        }

        [Fact]
        public void EmptyExistingCache_AllFreshResultsAreAdded()
        {
            var fresh = new List<WikiVendorResult>
            {
                MakeResult("Page A"),
                MakeResult("Page B")
            };

            var result = Program.MergeWikiCache(new List<WikiVendorResult>(), fresh);

            Assert.Equal(2, result.Merged.Count);
            Assert.Equal(2, result.Added);
            Assert.Equal(0, result.Refreshed);
            Assert.Equal(0, result.Unchanged);
        }

        [Fact]
        public void EmptyFreshBatch_ExistingCacheIsPreservedUnchanged()
        {
            var existing = new List<WikiVendorResult>
            {
                MakeResult("Page A", dailyCap: 2),
                MakeResult("Page B", weeklyCap: 4)
            };

            var result = Program.MergeWikiCache(existing, new List<WikiVendorResult>());

            Assert.Equal(2, result.Merged.Count);
            Assert.Equal(0, result.Added);
            Assert.Equal(0, result.Refreshed);
            Assert.Equal(2, result.Unchanged);
        }

        [Fact]
        public void NullPageName_TreatedAsEmptyStringKey()
        {
            var existing = new List<WikiVendorResult>
            {
                MakeResult(null, dailyCap: 1)
            };
            var fresh = new List<WikiVendorResult>
            {
                MakeResult(null, dailyCap: 9)
            };

            var result = Program.MergeWikiCache(existing, fresh);

            Assert.Single(result.Merged);
            Assert.Equal(9, result.Merged[0].DailyCap);
            Assert.Equal(1, result.Refreshed);
        }

        [Fact]
        public void NullExistingAndFreshLists_ReturnsEmptyMergeResult()
        {
            var result = Program.MergeWikiCache(null, null);

            Assert.NotNull(result.Merged);
            Assert.Empty(result.Merged);
            Assert.Equal(0, result.Added);
            Assert.Equal(0, result.Refreshed);
            Assert.Equal(0, result.Unchanged);
        }
    }
}
