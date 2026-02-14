using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class VendorOfferResolverTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly VendorOfferStore _store;

        public VendorOfferResolverTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(),
                "GW2CraftingHelper_ResolverTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);

            var loader = new VendorOfferLoader();
            _store = new VendorOfferStore(_tempDir, loader);
            _store.LoadBaseline(null);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); }
            catch { }
        }

        [Fact]
        public async Task MissingItems_FetchedFromWiki_SavedToOverlay()
        {
            var wiki = new InMemoryWikiVendorClient();
            wiki.AddOffers(100, new RawWikiVendorOffer
            {
                OutputItemId = 100,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 50 }
                },
                MerchantName = "Test NPC"
            });
            wiki.AddOffers(200, new RawWikiVendorOffer
            {
                OutputItemId = 200,
                OutputCount = 5,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 200 }
                },
                MerchantName = "Another NPC"
            });
            wiki.AddOffers(300, new RawWikiVendorOffer
            {
                OutputItemId = 300,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Item", Id = 50, Count = 3 }
                },
                MerchantName = "Item Trader"
            });

            var options = new WikiLookupOptions
            {
                MinDelayBetweenRequestsMs = 0,
                JitterMs = 0,
                MaxRetries = 0
            };
            var resolver = new VendorOfferResolver(wiki, _store, options);

            var result = await resolver.EnsureVendorOffersAsync(
                new[] { 100, 200, 300 }, null, CancellationToken.None);

            Assert.Equal(3, result.ItemsChecked);
            Assert.Equal(3, result.OffersAdded);
            Assert.Empty(result.FailedItemIds);

            Assert.True(_store.HasAnyOffer(100));
            Assert.True(_store.HasAnyOffer(200));
            Assert.True(_store.HasAnyOffer(300));
        }

        [Fact]
        public async Task ItemsAlreadyInStore_NoWikiCalls()
        {
            _store.AddOffersToOverlay(new[]
            {
                new VendorOffer
                {
                    OfferId = "existing-offer",
                    OutputItemId = 100,
                    OutputCount = 1,
                    CostLines = new List<CostLine>
                    {
                        new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 10 }
                    },
                    MerchantName = "Existing NPC"
                }
            });

            var wiki = new InMemoryWikiVendorClient();
            var options = new WikiLookupOptions
            {
                MinDelayBetweenRequestsMs = 0,
                JitterMs = 0,
                MaxRetries = 0
            };
            var resolver = new VendorOfferResolver(wiki, _store, options);

            var result = await resolver.EnsureVendorOffersAsync(
                new[] { 100 }, null, CancellationToken.None);

            Assert.Equal(0, result.ItemsChecked);
            Assert.Equal(0, result.OffersAdded);
            Assert.Empty(wiki.CalledItemIds);
        }

        [Fact]
        public async Task ConcurrencyNeverExceedsMax()
        {
            var wiki = new InMemoryWikiVendorClient();
            wiki.LatencyMs = 100;

            for (int i = 1; i <= 10; i++)
            {
                wiki.AddOffers(i, new RawWikiVendorOffer
                {
                    OutputItemId = i,
                    OutputCount = 1,
                    CostLines = new List<CostLine>
                    {
                        new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 10 }
                    },
                    MerchantName = "NPC"
                });
            }

            var options = new WikiLookupOptions
            {
                MaxConcurrentRequests = 2,
                MinDelayBetweenRequestsMs = 0,
                JitterMs = 0,
                MaxRetries = 0
            };
            var resolver = new VendorOfferResolver(wiki, _store, options);

            var ids = Enumerable.Range(1, 10).ToList();
            await resolver.EnsureVendorOffersAsync(ids, null, CancellationToken.None);

            Assert.True(wiki.MaxObservedConcurrency <= 2,
                $"Max concurrency was {wiki.MaxObservedConcurrency}, expected <= 2");
        }

        [Fact]
        public async Task CancellationStopsRequests()
        {
            var wiki = new InMemoryWikiVendorClient();
            wiki.LatencyMs = 200;

            for (int i = 1; i <= 20; i++)
            {
                wiki.AddOffers(i, new RawWikiVendorOffer
                {
                    OutputItemId = i,
                    OutputCount = 1,
                    CostLines = new List<CostLine>
                    {
                        new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 10 }
                    },
                    MerchantName = "NPC"
                });
            }

            var options = new WikiLookupOptions
            {
                MaxConcurrentRequests = 1,
                MinDelayBetweenRequestsMs = 0,
                JitterMs = 0,
                MaxRetries = 0
            };
            var resolver = new VendorOfferResolver(wiki, _store, options);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(500);

            var ids = Enumerable.Range(1, 20).ToList();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => resolver.EnsureVendorOffersAsync(ids, null, cts.Token));

            Assert.True(wiki.CalledItemIds.Count < 20,
                $"Expected fewer than 20 calls but got {wiki.CalledItemIds.Count}");
        }

        [Fact]
        public async Task ProgressEventsEmitted()
        {
            var wiki = new InMemoryWikiVendorClient();
            wiki.LatencyMs = 10;

            wiki.AddOffers(100, new RawWikiVendorOffer
            {
                OutputItemId = 100,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 10 }
                },
                MerchantName = "NPC"
            });
            wiki.AddOffers(200, new RawWikiVendorOffer
            {
                OutputItemId = 200,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 20 }
                },
                MerchantName = "NPC"
            });

            var options = new WikiLookupOptions
            {
                MaxConcurrentRequests = 1,
                MinDelayBetweenRequestsMs = 0,
                JitterMs = 0,
                MaxRetries = 0
            };
            var resolver = new VendorOfferResolver(wiki, _store, options);

            var reports = new List<PlanStatus>();
            var progressHandler = new Progress<PlanStatus>(s => reports.Add(s));

            await resolver.EnsureVendorOffersAsync(
                new[] { 100, 200 }, progressHandler, CancellationToken.None);

            // Allow Progress<T> callbacks to flush (they post to SynchronizationContext)
            await Task.Delay(100);

            Assert.True(reports.Count >= 1,
                $"Expected at least 1 progress report but got {reports.Count}");
        }

        [Fact]
        public async Task TransientFailure_RetriesAndSucceeds()
        {
            var wiki = new InMemoryWikiVendorClient();
            wiki.LatencyMs = 10;

            wiki.AddOffers(100, new RawWikiVendorOffer
            {
                OutputItemId = 100,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 50 }
                },
                MerchantName = "NPC"
            });
            // First call fails, second succeeds
            wiki.SetFailures(100, 1);

            var options = new WikiLookupOptions
            {
                MaxConcurrentRequests = 1,
                MinDelayBetweenRequestsMs = 0,
                JitterMs = 0,
                MaxRetries = 3
            };
            var resolver = new VendorOfferResolver(wiki, _store, options);

            var result = await resolver.EnsureVendorOffersAsync(
                new[] { 100 }, null, CancellationToken.None);

            Assert.Equal(1, result.OffersAdded);
            Assert.Empty(result.FailedItemIds);
            Assert.True(_store.HasAnyOffer(100));
        }

        [Fact]
        public async Task AllRetriesFail_ItemInFailedList()
        {
            var wiki = new InMemoryWikiVendorClient();
            wiki.LatencyMs = 10;

            // Will fail 10 times, more than MaxRetries
            wiki.SetFailures(100, 10);

            var options = new WikiLookupOptions
            {
                MaxConcurrentRequests = 1,
                MinDelayBetweenRequestsMs = 0,
                JitterMs = 0,
                MaxRetries = 2
            };
            var resolver = new VendorOfferResolver(wiki, _store, options);

            var result = await resolver.EnsureVendorOffersAsync(
                new[] { 100 }, null, CancellationToken.None);

            Assert.Equal(0, result.OffersAdded);
            Assert.Contains(100, result.FailedItemIds);
            Assert.False(_store.HasAnyOffer(100));
        }

        [Fact]
        public async Task DuplicateInputIds_CoalescedToSingleLookup()
        {
            var wiki = new InMemoryWikiVendorClient();
            wiki.LatencyMs = 10;

            wiki.AddOffers(100, new RawWikiVendorOffer
            {
                OutputItemId = 100,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 10 }
                },
                MerchantName = "NPC"
            });
            wiki.AddOffers(200, new RawWikiVendorOffer
            {
                OutputItemId = 200,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 20 }
                },
                MerchantName = "NPC"
            });

            var options = new WikiLookupOptions
            {
                MaxConcurrentRequests = 3,
                MinDelayBetweenRequestsMs = 0,
                JitterMs = 0,
                MaxRetries = 0
            };
            var resolver = new VendorOfferResolver(wiki, _store, options);

            // Duplicate IDs: 100 appears 3 times, 200 appears twice
            await resolver.EnsureVendorOffersAsync(
                new[] { 100, 100, 200, 100, 200 }, null, CancellationToken.None);

            // Should only make 2 distinct wiki calls
            var distinctCalls = wiki.CalledItemIds.Distinct().ToList();
            Assert.Equal(2, distinctCalls.Count);
            Assert.Equal(2, wiki.CalledItemIds.Count);
        }
    }
}
