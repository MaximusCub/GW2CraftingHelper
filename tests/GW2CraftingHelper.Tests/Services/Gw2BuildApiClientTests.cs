using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // Without the build id the recipe overlay is neither read nor written for
    // the whole session, so a single slow or blocked /v2/build response would
    // silently cost every plan its persistent recipe cache. These tests
    // exercise the real HTTP path (the StubHandler pattern established by
    // Gw2ApiClient404Tests), with the retry delay injected so they do not
    // spend the production backoff.
    public class Gw2BuildApiClientTests
    {
        private class ScriptedHandler : HttpMessageHandler
        {
            private readonly Queue<Func<HttpResponseMessage>> _responses;

            public ScriptedHandler(params Func<HttpResponseMessage>[] responses)
            {
                _responses = new Queue<Func<HttpResponseMessage>>(responses);
            }

            public int Calls { get; private set; }

            public Uri LastRequestUri { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                LastRequestUri = request.RequestUri;
                return Task.FromResult(_responses.Dequeue()());
            }
        }

        private static Func<HttpResponseMessage> Ok(int buildId)
        {
            return () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($@"{{""id"":{buildId}}}")
            };
        }

        private static Func<HttpResponseMessage> Status(HttpStatusCode code)
        {
            return () => new HttpResponseMessage(code)
            {
                Content = new StringContent("")
            };
        }

        private static Func<HttpResponseMessage> Throws()
        {
            return () => throw new HttpRequestException("connection refused");
        }

        private static Gw2BuildApiClient NoDelay(HttpClient http)
        {
            return new Gw2BuildApiClient(http, (d, ct) => Task.CompletedTask);
        }

        [Fact]
        public async Task TryGetBuildId_FirstAttemptSucceeds_MakesOneRequest()
        {
            using (var handler = new ScriptedHandler(Ok(205780)))
            using (var http = new HttpClient(handler))
            {
                var result = await NoDelay(http).TryGetBuildIdAsync(CancellationToken.None);

                Assert.Equal(205780, result.BuildId);
                Assert.Equal(1, result.Attempts);
                Assert.Null(result.LastError);
                Assert.Equal(1, handler.Calls);
                Assert.Equal(
                    "https://api.guildwars2.com/v2/build",
                    handler.LastRequestUri.ToString());
            }
        }

        [Fact]
        public async Task TryGetBuildId_TransientFailures_AreRetriedAndRecover()
        {
            using (var handler = new ScriptedHandler(
                Throws(), Status(HttpStatusCode.InternalServerError), Ok(205780)))
            using (var http = new HttpClient(handler))
            {
                var result = await NoDelay(http).TryGetBuildIdAsync(CancellationToken.None);

                Assert.Equal(205780, result.BuildId);
                Assert.Equal(3, result.Attempts);
                Assert.Equal(3, handler.Calls);
            }
        }

        [Fact]
        public async Task TryGetBuildId_AllAttemptsFail_ReportsFailureInsteadOfThrowing()
        {
            using (var handler = new ScriptedHandler(Throws(), Throws(), Throws()))
            using (var http = new HttpClient(handler))
            {
                var result = await NoDelay(http).TryGetBuildIdAsync(CancellationToken.None);

                Assert.Null(result.BuildId);
                Assert.Equal(3, result.Attempts);
                Assert.IsType<HttpRequestException>(result.LastError);

                // Bounded: the caller is told to degrade, not left waiting.
                Assert.Equal(3, handler.Calls);
            }
        }

        [Fact]
        public async Task TryGetBuildId_CallerCancellation_StopsRetrying()
        {
            using (var handler = new ScriptedHandler(Throws(), Throws(), Throws()))
            using (var http = new HttpClient(handler))
            using (var cts = new CancellationTokenSource())
            {
                // Cancelled during the backoff between attempt 1 and 2 -
                // module unload must not be waited out.
                var client = new Gw2BuildApiClient(http, (d, ct) =>
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                });

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => client.TryGetBuildIdAsync(cts.Token));

                Assert.Equal(1, handler.Calls);
            }
        }
    }
}
