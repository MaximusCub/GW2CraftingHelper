using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VendorOfferUpdater;
using VendorOfferUpdater.Tests.Helpers;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// Festival-vendor auto-tagging follow-up (2026-08-16): exercises the
    /// full wikitext-fetch-and-cache pass (Program.ResolveSeasonalFestivalValuesAsync)
    /// end to end against a fake wiki API, including the SMW subobject-key
    /// stripping (Program.StripSubobjectSuffix) real "Sells item" subjects
    /// need - see that method's own doc comment for the live-confirmed
    /// "PageName#vendorN" shape this test's fixture PageNames mirror.
    /// </summary>
    public class ResolveSeasonalFestivalValuesAsyncTests
    {
        private static string BuildWikitextResponse(string wikitext)
        {
            // Mirrors the real api.php?action=parse&prop=wikitext response
            // shape (json-serialized): {"parse":{"wikitext":{"*":"..."}}}.
            string escaped = wikitext
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n");
            return $"{{\"parse\":{{\"wikitext\":{{\"*\":\"{escaped}\"}}}}}}";
        }

        private static string TempCachePath()
        {
            return Path.Combine(Path.GetTempPath(), $"seasonal_wikitext_cache_{Guid.NewGuid():N}.json");
        }

        [Fact]
        public async Task SubobjectRows_StrippedToPageName_FetchedOncePerVendor()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(
                url => url.Contains("action=parse") && url.Contains("Candy"),
                BuildWikitextResponse(
                    "{{Temporary|release=Shadow of the Mad King 2019|seasonal=Halloween}}"));

            var results = new List<WikiVendorResult>
            {
                new WikiVendorResult { PageName = "Candy Corn Vendor (Weekly)#vendor1", GameId = 1 },
                new WikiVendorResult { PageName = "Candy Corn Vendor (Weekly)#vendor2", GameId = 2 },
                new WikiVendorResult { PageName = "Candy Corn Vendor (Weekly)#vendor3", GameId = 3 },
            };

            string cachePath = TempCachePath();
            try
            {
                await Program.ResolveSeasonalFestivalValuesAsync(
                    results, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);

                // One vendor page shared by all three subobject rows -> one
                // fetch, not three.
                Assert.Equal(1, handler.RequestedUrls.Count(u => u.Contains("action=parse")));
                Assert.All(results, r => Assert.Equal("Halloween", r.TemporarySeasonalValue));
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        [Fact]
        public async Task SecondRun_UsesCache_NoNewRequests()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(
                url => url.Contains("action=parse"),
                BuildWikitextResponse("{{Temporary|release=Dragon Bash 2019|seasonal=Dragon Bash}}"));

            string cachePath = TempCachePath();
            try
            {
                var firstRun = new List<WikiVendorResult>
                {
                    new WikiVendorResult { PageName = "Dragon Bash Merchant (Weekly)#vendor1", GameId = 1 }
                };
                await Program.ResolveSeasonalFestivalValuesAsync(
                    firstRun, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);
                Assert.Single(handler.RequestedUrls);

                var secondRun = new List<WikiVendorResult>
                {
                    new WikiVendorResult { PageName = "Dragon Bash Merchant (Weekly)#vendor2", GameId = 2 }
                };
                await Program.ResolveSeasonalFestivalValuesAsync(
                    secondRun, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);

                // Still exactly 1 - the second run's page was already cached.
                Assert.Single(handler.RequestedUrls);
                Assert.Equal("Dragon Bash", secondRun[0].TemporarySeasonalValue);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        [Fact]
        public async Task NoTemporaryTemplate_CachedAsUntagged_StaysNull()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(
                url => url.Contains("action=parse"),
                BuildWikitextResponse("{{NPC infobox\n| name = Miyani\n}}"));

            var results = new List<WikiVendorResult>
            {
                new WikiVendorResult { PageName = "Miyani#vendor1", GameId = 1 }
            };

            string cachePath = TempCachePath();
            try
            {
                await Program.ResolveSeasonalFestivalValuesAsync(
                    results, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);

                Assert.Null(results[0].TemporarySeasonalValue);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        [Fact]
        public async Task UnmappedEventValue_StillCapturedAsRawValue()
        {
            // Program.ResolveSeasonalFestivalValuesAsync only extracts the
            // raw wiki string - resolving it against the six known
            // festivals (and leaving it untagged if unrecognized) is
            // ConvertToOffer's job (Gw2Constants.ResolveSeasonalFestivalKey),
            // tested separately. Real, live-confirmed value.
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(
                url => url.Contains("action=parse"),
                BuildWikitextResponse("{{temporary|event=Fractal Rush}}"));

            var results = new List<WikiVendorResult>
            {
                new WikiVendorResult { PageName = "Consortium Trader (Fractal Rush)#vendor1", GameId = 1 }
            };

            string cachePath = TempCachePath();
            try
            {
                await Program.ResolveSeasonalFestivalValuesAsync(
                    results, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);

                Assert.Equal("Fractal Rush", results[0].TemporarySeasonalValue);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        [Fact]
        public async Task TooManyUncachedPages_ThrowsSafetyLimitException()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(url => url.Contains("action=parse"), BuildWikitextResponse("{{NPC infobox}}"));

            var results = new List<WikiVendorResult>
            {
                new WikiVendorResult { PageName = "Vendor A#vendor1", GameId = 1 },
                new WikiVendorResult { PageName = "Vendor B#vendor1", GameId = 2 },
                new WikiVendorResult { PageName = "Vendor C#vendor1", GameId = 3 },
            };

            string cachePath = TempCachePath();
            try
            {
                await Assert.ThrowsAsync<SafetyLimitException>(() =>
                    Program.ResolveSeasonalFestivalValuesAsync(
                        results, client, cachePath, maxSeasonalPages: 2, delayMs: 0, CancellationToken.None));

                // No requests should have been made - the limit is checked
                // before any fetch starts.
                Assert.Empty(handler.RequestedUrls);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        [Fact]
        public async Task HttpFailureOnOnePage_LeavesItUncached_OthersStillResolved()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(
                url => url.Contains("action=parse") && url.Contains("Good"),
                BuildWikitextResponse("{{Temporary|release=Dragon Bash 2019|seasonal=Dragon Bash}}"));
            handler.MapUrl(
                url => url.Contains("action=parse") && url.Contains("Bad"),
                "{}", HttpStatusCode.InternalServerError);

            var results = new List<WikiVendorResult>
            {
                new WikiVendorResult { PageName = "Bad Vendor#vendor1", GameId = 1 },
                new WikiVendorResult { PageName = "Good Vendor#vendor1", GameId = 2 },
            };

            string cachePath = TempCachePath();
            try
            {
                await Program.ResolveSeasonalFestivalValuesAsync(
                    results, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);

                Assert.Null(results[0].TemporarySeasonalValue);
                Assert.Equal("Dragon Bash", results[1].TemporarySeasonalValue);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        // Resilience fix (2026-08-17): a non-JSON/HTML 200 response (the
        // shape a wiki maintenance page or a proxy error page would
        // return) used to throw an uncaught JsonException out of
        // FetchWitextAsync's JsonDocument.Parse, aborting the WHOLE method
        // - the loop never reached its other pages, and (before this fix)
        // the post-loop-only save call meant nothing fetched so far was
        // persisted either. Must now behave exactly like an HTTP failure:
        // warn, leave that one page uncached, continue with the rest.
        [Fact]
        public async Task NonJsonResponseOnOnePage_LeavesItUncached_OthersStillResolved()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(
                url => url.Contains("action=parse") && url.Contains("Good"),
                BuildWikitextResponse("{{Temporary|release=Dragon Bash 2019|seasonal=Dragon Bash}}"));
            handler.MapUrl(
                url => url.Contains("action=parse") && url.Contains("Bad"),
                "<html>not json</html>");

            var results = new List<WikiVendorResult>
            {
                new WikiVendorResult { PageName = "Bad Vendor#vendor1", GameId = 1 },
                new WikiVendorResult { PageName = "Good Vendor#vendor1", GameId = 2 },
            };

            string cachePath = TempCachePath();
            try
            {
                await Program.ResolveSeasonalFestivalValuesAsync(
                    results, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);

                Assert.Null(results[0].TemporarySeasonalValue);
                Assert.Equal("Dragon Bash", results[1].TemporarySeasonalValue);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        // Resilience fix (2026-08-17): an exception the per-page catches
        // don't handle (anything other than HttpRequestException/
        // JsonException - e.g. Ctrl-C's OperationCanceledException, or
        // any other unexpected failure) must still leave every page
        // already fetched THIS run persisted, not just the pages fetched
        // on a run that happens to complete normally. Deterministic
        // repro: the second page's URL matches nothing the fake handler
        // knows about, so it throws an arbitrary (uncaught-by-design)
        // exception straight out of the loop.
        [Fact]
        public async Task UnhandledExceptionMidLoop_StillSavesPagesFetchedSoFar()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(
                url => url.Contains("action=parse") && url.Contains("page=VendorA"),
                BuildWikitextResponse("{{Temporary|release=Dragon Bash 2019|seasonal=Dragon Bash}}"));
            // Deliberately no mapping for "VendorB" and no queued
            // fallback - FakeHttpHandler throws InvalidOperationException
            // for it, simulating an unanticipated failure mode.

            var results = new List<WikiVendorResult>
            {
                new WikiVendorResult { PageName = "VendorA#vendor1", GameId = 1 },
                new WikiVendorResult { PageName = "VendorB#vendor1", GameId = 2 },
            };

            string cachePath = TempCachePath();
            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    Program.ResolveSeasonalFestivalValuesAsync(
                        results, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None));

                Assert.True(
                    File.Exists(cachePath),
                    "Cache file should be saved even when the loop exits via an unhandled exception.");
                string json = File.ReadAllText(cachePath);
                Assert.Contains("VendorA", json);
                Assert.DoesNotContain("VendorB", json);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        [Fact]
        public async Task EmptyWikiResults_NoOp()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            string cachePath = TempCachePath();
            try
            {
                await Program.ResolveSeasonalFestivalValuesAsync(
                    new List<WikiVendorResult>(), client, cachePath,
                    maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);

                Assert.Empty(handler.RequestedUrls);
                Assert.False(File.Exists(cachePath));
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        // -- StripSubobjectSuffix --------------------------------------

        [Theory]
        [InlineData("Candy Corn Vendor (Weekly)#vendor1", "Candy Corn Vendor (Weekly)")]
        [InlineData("Miyani#vendor12", "Miyani")]
        [InlineData("Plain Page Title", "Plain Page Title")]
        [InlineData("", "")]
        public void StripSubobjectSuffix_RemovesHashSuffixOnly(string input, string expected)
        {
            Assert.Equal(expected, Program.StripSubobjectSuffix(input));
        }
    }
}
