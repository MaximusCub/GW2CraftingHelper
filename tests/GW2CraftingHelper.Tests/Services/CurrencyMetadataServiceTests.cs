using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CurrencyMetadataServiceTests
    {
        private class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _body;
            public int CallCount { get; private set; }

            public StubHandler(HttpStatusCode statusCode, string body = "")
            {
                _statusCode = statusCode;
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                var response = new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_body)
                };
                return Task.FromResult(response);
            }
        }

        // Real-shape sample of GET /v2/currencies?ids=all covering a few
        // wallet currencies referenced elsewhere in this repo's tests
        // (Gw2Constants.KnownCurrencyNames / PlanViewModelBuilderTests).
        private const string SampleJson = @"[
            {
                ""id"": 1,
                ""name"": ""Coin"",
                ""description"": ""The currency for goods and services."",
                ""order"": 1,
                ""icon"": ""https://render.guildwars2.com/file/coin.png""
            },
            {
                ""id"": 15,
                ""name"": ""Badges of Honor"",
                ""description"": ""Awarded for participating in WvW."",
                ""order"": 15,
                ""icon"": ""https://render.guildwars2.com/file/badges.png""
            },
            {
                ""id"": 23,
                ""name"": ""Spirit Shards"",
                ""description"": ""Gained after reaching level 80."",
                ""order"": 23,
                ""icon"": ""https://render.guildwars2.com/file/spirit_shard.png""
            },
            {
                ""id"": 32,
                ""name"": ""Unbound Magic"",
                ""description"": ""A currency from Living World Season 4."",
                ""order"": 32,
                ""icon"": ""https://render.guildwars2.com/file/unbound_magic.png""
            },
            {
                ""id"": 63,
                ""name"": ""Astral Acclaim"",
                ""description"": ""Earned through Astral Rewards."",
                ""order"": 63,
                ""icon"": ""https://render.guildwars2.com/file/astral_acclaim.png""
            }
        ]";

        [Fact]
        public async Task GetAllAsync_ParsesNameAndIcon_ForKnownCurrencies()
        {
            using (var handler = new StubHandler(HttpStatusCode.OK, SampleJson))
            using (var http = new HttpClient(handler))
            {
                var service = new CurrencyMetadataService(http);
                var result = await service.GetAllAsync(CancellationToken.None);

                Assert.Equal(5, result.Count);

                Assert.Equal("Spirit Shards", result[23].Name);
                Assert.Equal("https://render.guildwars2.com/file/spirit_shard.png", result[23].IconUrl);
                Assert.Equal(23, result[23].CurrencyId);

                Assert.Equal("Unbound Magic", result[32].Name);
                Assert.Equal("https://render.guildwars2.com/file/unbound_magic.png", result[32].IconUrl);

                Assert.Equal("Astral Acclaim", result[63].Name);
                Assert.Equal("https://render.guildwars2.com/file/astral_acclaim.png", result[63].IconUrl);

                Assert.Equal("Badges of Honor", result[15].Name);
                Assert.Equal("https://render.guildwars2.com/file/badges.png", result[15].IconUrl);
            }
        }

        [Fact]
        public async Task GetAllAsync_SecondCall_DoesNotRefetch()
        {
            using (var handler = new StubHandler(HttpStatusCode.OK, SampleJson))
            using (var http = new HttpClient(handler))
            {
                var service = new CurrencyMetadataService(http);

                await service.GetAllAsync(CancellationToken.None);
                await service.GetAllAsync(CancellationToken.None);

                Assert.Equal(1, handler.CallCount);
            }
        }

        [Fact]
        public async Task GetAllAsync_NonSuccessStatus_ReturnsEmpty_NoThrow()
        {
            using (var handler = new StubHandler(HttpStatusCode.InternalServerError))
            using (var http = new HttpClient(handler))
            {
                var service = new CurrencyMetadataService(http);
                var result = await service.GetAllAsync(CancellationToken.None);

                Assert.Empty(result);
            }
        }

        /// <summary>
        /// Fails the first N requests, then succeeds - lets a test exercise
        /// the SAME service instance across a failed call followed by a
        /// successful one, unlike swapping in a second service/handler
        /// (which would prove nothing about the real retry decision inside
        /// GetAllAsync).
        /// </summary>
        private class FlakyHandler : HttpMessageHandler
        {
            private readonly int _failCount;
            private readonly string _successBody;
            private int _calls;

            public FlakyHandler(int failCount, string successBody)
            {
                _failCount = failCount;
                _successBody = successBody;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _calls++;
                if (_calls <= _failCount)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("")
                    });
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_successBody)
                });
            }
        }

        [Fact]
        public async Task GetAllAsync_FailureThenSuccess_RetriesOnNextCall()
        {
            // A failed fetch must not be permanently negative-cached (unlike
            // ItemMetadataService's per-item _knownMissing set) - the next
            // call on the SAME instance retries the network request and
            // populates the cache once it succeeds.
            using (var handler = new FlakyHandler(failCount: 1, successBody: SampleJson))
            using (var http = new HttpClient(handler))
            {
                var service = new CurrencyMetadataService(http);

                var firstResult = await service.GetAllAsync(CancellationToken.None);
                Assert.Empty(firstResult);

                var secondResult = await service.GetAllAsync(CancellationToken.None);
                Assert.Equal(5, secondResult.Count);
                Assert.Equal("Spirit Shards", secondResult[23].Name);
            }
        }

        [Fact]
        public async Task GetAllAsync_MalformedJson_ReturnsEmpty_NoThrow()
        {
            using (var handler = new StubHandler(HttpStatusCode.OK, "not valid json"))
            using (var http = new HttpClient(handler))
            {
                var service = new CurrencyMetadataService(http);
                var result = await service.GetAllAsync(CancellationToken.None);

                Assert.Empty(result);
            }
        }

        [Fact]
        public async Task GetAllAsync_EmptyArray_ReturnsEmpty()
        {
            using (var handler = new StubHandler(HttpStatusCode.OK, "[]"))
            using (var http = new HttpClient(handler))
            {
                var service = new CurrencyMetadataService(http);
                var result = await service.GetAllAsync(CancellationToken.None);

                Assert.Empty(result);
            }
        }
    }
}
