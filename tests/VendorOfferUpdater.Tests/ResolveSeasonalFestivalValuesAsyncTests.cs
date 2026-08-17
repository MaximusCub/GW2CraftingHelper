using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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

        // Review fix (2026-08-18): a null FetchWikitextAsync result
        // (missing/renamed page, or an "error" object in the API response
        // - valid JSON with no "parse" property) must NOT be cached as ""
        // ("checked - no {{Temporary}} template") the way a real, fetched-
        // fine page with no template legitimately is - see
        // NoTemporaryTemplate_CachedAsUntagged_StaysNull above for that
        // contrasting case. Left uncached, this page is retried on the
        // very next run instead of silently baking a false negative in
        // forever.
        [Fact]
        public async Task NullWikitext_LeftUncached_RetriedNextRun()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(
                url => url.Contains("action=parse"),
                "{\"error\":{\"code\":\"missingtitle\"}}");

            string cachePath = TempCachePath();
            try
            {
                var firstRun = new List<WikiVendorResult>
                {
                    new WikiVendorResult { PageName = "Renamed Vendor#vendor1", GameId = 1 }
                };
                await Program.ResolveSeasonalFestivalValuesAsync(
                    firstRun, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);

                Assert.Null(firstRun[0].TemporarySeasonalValue);
                Assert.Single(handler.RequestedUrls);

                var secondRun = new List<WikiVendorResult>
                {
                    new WikiVendorResult { PageName = "Renamed Vendor#vendor1", GameId = 1 }
                };
                await Program.ResolveSeasonalFestivalValuesAsync(
                    secondRun, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);

                // Re-fetched, not served from a stale "" cache entry.
                Assert.Equal(2, handler.RequestedUrls.Count);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        // Review fix (2026-08-18): a scoped --merge-into --query run's
        // fetch budget (and --max-seasonal-pages check) must count only
        // the pages THIS run's --query returned (queryScopedResults), not
        // every distinct page in the full merged wiki_vendor_cache.json
        // (wikiResults) - otherwise a narrow --query on a large existing
        // cache spuriously exceeds the limit and SafetyLimitException-
        // aborts a run that would otherwise complete fine.
        [Fact]
        public async Task QueryScopedResults_LimitsFetchBudget_NotFullMergedCache()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(
                url => url.Contains("action=parse"),
                BuildWikitextResponse("{{Temporary|release=Dragon Bash 2019|seasonal=Dragon Bash}}"));

            // The full merged cache (as Program.cs's Step 2 MergeWikiCache
            // union would produce) has 5 distinct pages, but this run's own
            // --query only touched 1 of them.
            var wikiResults = new List<WikiVendorResult>
            {
                new WikiVendorResult { PageName = "Dragon Bash Merchant (Weekly)#vendor1", GameId = 1 },
                new WikiVendorResult { PageName = "Vendor B#vendor1", GameId = 2 },
                new WikiVendorResult { PageName = "Vendor C#vendor1", GameId = 3 },
                new WikiVendorResult { PageName = "Vendor D#vendor1", GameId = 4 },
                new WikiVendorResult { PageName = "Vendor E#vendor1", GameId = 5 },
            };
            var queryScopedResults = new List<WikiVendorResult>
            {
                wikiResults[0]
            };

            string cachePath = TempCachePath();
            try
            {
                // maxSeasonalPages=1 would throw SafetyLimitException
                // against the full 5-page wikiResults, but must NOT throw
                // when scoped to the 1-page query result.
                await Program.ResolveSeasonalFestivalValuesAsync(
                    wikiResults, client, cachePath, maxSeasonalPages: 1, delayMs: 0,
                    CancellationToken.None, queryScopedResults);

                Assert.Single(handler.RequestedUrls);
                Assert.Equal("Dragon Bash", wikiResults[0].TemporarySeasonalValue);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        // Nice-to-have fix (2026-08-18 review): the cache-apply loop used
        // to only ever ASSIGN a non-empty cached value onto
        // TemporarySeasonalValue and never CLEAR it - a value that had
        // already round-tripped in from a prior run (e.g. via
        // wiki_vendor_cache.json) stayed set forever, even after this
        // pass re-checks the page and finds the {{Temporary}} template is
        // now gone.
        [Fact]
        public async Task PreviouslySetValue_ClearedWhenPageNoLongerHasTemplate()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(
                url => url.Contains("action=parse"),
                BuildWikitextResponse("{{NPC infobox\n| name = Miyani\n}}"));

            var results = new List<WikiVendorResult>
            {
                new WikiVendorResult
                {
                    PageName = "Miyani#vendor1",
                    GameId = 1,
                    // Simulates a value that round-tripped in from a prior
                    // run's wiki_vendor_cache.json save.
                    TemporarySeasonalValue = "Halloween"
                }
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

        // Throttle-class fix (2026-08-19): a null-wikitext `continue`
        // (and the two pre-existing HttpRequestException/JsonException
        // `continue`s) used to jump straight past the inter-request
        // Task.Delay, which only ran on the success path. A stretch of
        // missing/failing pages was fetched back-to-back with no
        // throttling at all, defeating both --delay and the 200ms floor
        // (Math.Max(200, delayMs)). The delay now lives in a per-
        // iteration `finally`, so it must fire after the FIRST (null-
        // wikitext) page here even though delayMs is 0 - only the 200ms
        // floor applies, but that floor must still be honored.
        [Fact]
        public async Task NullWikitextOnNonLastPage_StillThrottlesBeforeNextRequest()
        {
            var handler = new FakeHttpHandler();
            using var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);

            handler.MapUrl(
                url => url.Contains("action=parse") && url.Contains("Missing"),
                "{\"error\":{\"code\":\"missingtitle\"}}");
            handler.MapUrl(
                url => url.Contains("action=parse") && url.Contains("Good"),
                BuildWikitextResponse("{{Temporary|release=Dragon Bash 2019|seasonal=Dragon Bash}}"));

            var results = new List<WikiVendorResult>
            {
                new WikiVendorResult { PageName = "Missing Vendor#vendor1", GameId = 1 },
                new WikiVendorResult { PageName = "Good Vendor#vendor1", GameId = 2 },
            };

            string cachePath = TempCachePath();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await Program.ResolveSeasonalFestivalValuesAsync(
                    results, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);
                stopwatch.Stop();

                Assert.True(
                    stopwatch.ElapsedMilliseconds >= 180,
                    $"Expected the 200ms floor delay to fire after the null-wikitext page, " +
                    $"elapsed only {stopwatch.ElapsedMilliseconds}ms.");
                Assert.Null(results[0].TemporarySeasonalValue);
                Assert.Equal("Dragon Bash", results[1].TemporarySeasonalValue);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        // Nice-to-have (2026-08-19): a cache file written before the
        // &redirects=1 fix (WikiSmwClient.FetchWikitextAsync) may contain
        // "" entries that actually mean "this page's SMW subject was a
        // redirect and its wikitext came back as '#REDIRECT [[...]]',
        // which happened to parse as no {{Temporary}} template" rather
        // than a real, deliberate "checked, not tagged". A legacy cache
        // (no version marker) must have its "" entries purged and
        // re-fetched once, not trusted forever.
        [Fact]
        public async Task LegacyCacheWithNoVersionMarker_PurgesEmptyEntries_RefetchesThem()
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
                // Simulates a pre-version-marker cache file: a plain
                // pageName -> "" map, no "__cache_version__" key, as
                // SaveSeasonalWikitextCache would have written before this
                // fix. The redirect page genuinely has a {{Temporary}}
                // template once fetched with &redirects=1, but the stale
                // "" entry would previously have hidden that forever.
                File.WriteAllText(cachePath, JsonSerializer.Serialize(
                    new Dictionary<string, string> { ["Redirected Vendor"] = string.Empty }));

                var results = new List<WikiVendorResult>
                {
                    new WikiVendorResult { PageName = "Redirected Vendor#vendor1", GameId = 1 }
                };

                await Program.ResolveSeasonalFestivalValuesAsync(
                    results, client, cachePath, maxSeasonalPages: 10, delayMs: 0, CancellationToken.None);

                Assert.Single(handler.RequestedUrls);
                Assert.Equal("Dragon Bash", results[0].TemporarySeasonalValue);
            }
            finally
            {
                File.Delete(cachePath);
            }
        }

        // Companion to the above: once the cache has been saved WITH the
        // current version marker, a resolved non-empty value must still
        // be trusted and NOT re-fetched (the version bump only forces a
        // recheck of the ambiguous "" case, not every cached value).
        [Fact]
        public async Task CurrentVersionCache_ResolvedValue_NotRefetched()
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

                // Still exactly 1 request - the saved cache already carried
                // the current version marker, so the resolved value is
                // trusted rather than re-fetched.
                Assert.Single(handler.RequestedUrls);
                Assert.Equal("Dragon Bash", secondRun[0].TemporarySeasonalValue);
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
