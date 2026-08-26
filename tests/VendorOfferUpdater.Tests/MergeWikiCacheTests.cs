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
        public void DuplicatePageNameWithinFreshBatch_RefreshedAndUnchangedNotDoubleCounted()
        {
            // Quality-audit B4 (KNOWN-ISSUES #53): the bug this pins
            // down - refreshed used to be incremented once per FRESH entry
            // whose key was already in `merged`, and `merged` is the same
            // dictionary the loop writes to on every iteration. Two fresh
            // entries sharing one PageName ("Refreshed Page" here) used to
            // count as TWO refreshes against a cache that only ever had
            // ONE matching page - existing.Count(2) - refreshed(2) yields
            // Unchanged 0 instead of the correct 1 for this exact shape
            // (measured; a smaller existing count than duplicate refreshes
            // would go negative, but this fixture does not).
            var existing = new List<WikiVendorResult>
            {
                MakeResult("Untouched Page", dailyCap: 3),
                MakeResult("Refreshed Page", dailyCap: null)
            };
            var fresh = new List<WikiVendorResult>
            {
                MakeResult("Refreshed Page", dailyCap: 5),
                MakeResult("Refreshed Page", dailyCap: 7)
            };

            var result = Program.MergeWikiCache(existing, fresh);

            Assert.Equal(2, result.Merged.Count);
            var refreshed = Assert.Single(result.Merged, r => r.PageName == "Refreshed Page");
            // The LAST fresh entry for the duplicated key wins, same as a
            // plain dictionary-overwrite merge.
            Assert.Equal(7, refreshed.DailyCap);
            Assert.Equal(0, result.Added);
            Assert.Equal(1, result.Refreshed);
            Assert.Equal(1, result.Unchanged);
        }

        [Fact]
        public void DuplicatePageNameForNewPageWithinFreshBatch_AddedNotDoubleCounted()
        {
            // Same root cause as the refreshed case above, on the Added
            // side: two fresh entries for a PageName absent from existing
            // are one net new page, not two.
            var existing = new List<WikiVendorResult>
            {
                MakeResult("Existing Page")
            };
            var fresh = new List<WikiVendorResult>
            {
                MakeResult("New Page", dailyCap: 1),
                MakeResult("New Page", dailyCap: 2)
            };

            var result = Program.MergeWikiCache(existing, fresh);

            Assert.Equal(2, result.Merged.Count);
            var added = Assert.Single(result.Merged, r => r.PageName == "New Page");
            Assert.Equal(2, added.DailyCap);
            Assert.Equal(1, result.Added);
            Assert.Equal(0, result.Refreshed);
            Assert.Equal(1, result.Unchanged);
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
