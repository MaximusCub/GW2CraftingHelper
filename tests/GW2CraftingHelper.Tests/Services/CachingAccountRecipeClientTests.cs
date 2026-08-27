using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class CachingAccountRecipeClientTests
    {
        private static readonly DateTime Start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task SecondCallWithinTtl_ServedFromCache_NoSecondUpstreamQuery()
        {
            var inner = new InMemoryAccountRecipeClient();
            inner.AddLearnedRecipe(10);
            var clock = Start;
            var svc = new CachingAccountRecipeClient(inner, TimeSpan.FromMinutes(5), () => clock);

            var first = await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);
            clock = clock.AddMinutes(4);
            var second = await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);

            Assert.Equal(1, inner.GetCallCount);
            Assert.Contains(10, first);
            Assert.Contains(10, second);
        }

        [Fact]
        public async Task CallAfterTtlExpiry_RefetchesAndPicksUpNewlyLearnedRecipes()
        {
            var inner = new InMemoryAccountRecipeClient();
            inner.AddLearnedRecipe(10);
            var clock = Start;
            var svc = new CachingAccountRecipeClient(inner, TimeSpan.FromMinutes(5), () => clock);

            await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);

            inner.AddLearnedRecipe(11); // learned in-game while the entry was cached
            clock = clock.AddMinutes(6);
            var afterExpiry = await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);

            Assert.Equal(2, inner.GetCallCount);
            Assert.Contains(11, afterExpiry);
        }

        [Fact]
        public async Task ExactlyAtTtl_Refetches()
        {
            // < _ttl, not <=, matching TradingPostService's own boundary.
            var inner = new InMemoryAccountRecipeClient();
            var clock = Start;
            var svc = new CachingAccountRecipeClient(inner, TimeSpan.FromMinutes(5), () => clock);

            await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);
            clock = clock.AddMinutes(5);
            await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);

            Assert.Equal(2, inner.GetCallCount);
        }

        [Fact]
        public async Task FailedFetch_IsNotCached_NextCallRetries()
        {
            var inner = new InMemoryAccountRecipeClient();
            inner.AddLearnedRecipe(10);
            inner.ThrowOnGet = true;
            var clock = Start;
            var svc = new CachingAccountRecipeClient(inner, TimeSpan.FromMinutes(5), () => clock);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.GetLearnedRecipeIdsAsync(CancellationToken.None));

            inner.ThrowOnGet = false;
            var recovered = await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);

            Assert.Equal(2, inner.GetCallCount);
            Assert.Contains(10, recovered);
        }

        [Fact]
        public async Task ReturnedSetIsACopy_MutatingItDoesNotPoisonTheCache()
        {
            var inner = new InMemoryAccountRecipeClient();
            inner.AddLearnedRecipe(10);
            var clock = Start;
            var svc = new CachingAccountRecipeClient(inner, TimeSpan.FromMinutes(5), () => clock);

            var first = await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);
            first.Add(99);
            first.Remove(10);

            var second = await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);

            Assert.Equal(1, inner.GetCallCount);
            Assert.Contains(10, second);
            Assert.DoesNotContain(99, second);
        }

        // The TTL is a clock, and a clock cannot notice that the API key now
        // addresses a different GW2 account. Module.OnSubtokenUpdated can,
        // and says so here.
        [Fact]
        public async Task Invalidate_DropsTheCache_EvenWellInsideTheTtl()
        {
            var inner = new InMemoryAccountRecipeClient();
            inner.SetLearnedRecipes(10); // account A
            var clock = Start;
            var svc = new CachingAccountRecipeClient(inner, TimeSpan.FromMinutes(5), () => clock);

            var accountA = await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);
            Assert.Contains(10, accountA);

            inner.SetLearnedRecipes(20); // the key now points at account B
            svc.Invalidate();

            clock = clock.AddSeconds(1);
            var accountB = await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);

            Assert.Equal(2, inner.GetCallCount);
            Assert.Contains(20, accountB);
            Assert.DoesNotContain(10, accountB);
        }

        // A fetch already in flight when the key changed is carrying the old
        // account's ids; letting it complete into the cache would silently
        // undo the invalidation for a further TTL.
        [Fact]
        public async Task FetchInFlightWhenInvalidated_IsNotCached()
        {
            var inner = new InMemoryAccountRecipeClient();
            inner.SetLearnedRecipes(10); // account A
            var gate = new TaskCompletionSource<bool>();
            inner.Gate = gate.Task;
            var clock = Start;
            var svc = new CachingAccountRecipeClient(inner, TimeSpan.FromMinutes(5), () => clock);

            var inFlight = svc.GetLearnedRecipeIdsAsync(CancellationToken.None);
            Assert.Equal(1, inner.GetCallCount); // entered, awaiting the gate

            svc.Invalidate();

            inner.Gate = null;
            gate.SetResult(true);
            var stillAccountA = await inFlight;
            Assert.Contains(10, stillAccountA); // the caller that asked still gets an answer

            inner.SetLearnedRecipes(20); // account B
            clock = clock.AddSeconds(1);
            var next = await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);

            Assert.Equal(2, inner.GetCallCount);
            Assert.Contains(20, next);
            Assert.DoesNotContain(10, next);
        }

        [Fact]
        public void HasRequiredPermission_DelegatesToInner()
        {
            var inner = new InMemoryAccountRecipeClient();
            var svc = new CachingAccountRecipeClient(inner);

            Assert.True(svc.HasRequiredPermission());

            inner.SetHasPermission(false);

            // Not latched: a token whose Unlocks scope appears later in the
            // session must still be picked up.
            Assert.False(svc.HasRequiredPermission());
        }

        [Fact]
        public async Task DefaultTtl_CachesBackToBackCallsWithoutAnExplicitClock()
        {
            var inner = new InMemoryAccountRecipeClient();
            inner.AddLearnedRecipe(10);
            var svc = new CachingAccountRecipeClient(inner);

            await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);
            await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);

            Assert.Equal(1, inner.GetCallCount);
        }

        [Fact]
        public async Task InnerReturnsNull_TreatedAsEmpty_AndStillCached()
        {
            var inner = new NullReturningAccountRecipeClient();
            var svc = new CachingAccountRecipeClient(inner);

            var result = await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);
            await svc.GetLearnedRecipeIdsAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result);
            Assert.Equal(1, inner.CallCount);
        }

        // Not in Helpers/: the production Gw2AccountRecipeClient never
        // returns null, so this exists only to prove the decorator's own
        // null tolerance and has no other caller.
        private sealed class NullReturningAccountRecipeClient : IAccountRecipeClient
        {
            public int CallCount { get; private set; }

            public Task<ISet<int>> GetLearnedRecipeIdsAsync(CancellationToken ct)
            {
                CallCount++;
                return Task.FromResult<ISet<int>>(null);
            }

            public bool HasRequiredPermission()
            {
                return true;
            }
        }
    }
}
