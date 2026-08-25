using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// No sleeps and no wall-clock waits anywhere in this file. Every
    /// assertion leans on one property of ForEachAsync: it starts its whole
    /// task list synchronously, so each action body runs on the calling
    /// thread up to its first real await. That means the instant
    /// ForEachAsync hands its Task back, exactly maxConcurrency actions have
    /// entered and the rest are parked on the semaphore - a state the test
    /// can assert directly instead of waiting for.
    /// </summary>
    public class BoundedConcurrencyTests
    {
        private const int Bound = 2;
        private const int ItemCount = 6;

        [Fact]
        public async Task NeverRunsMoreThanTheBoundAtOnce()
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int inFlight = 0;
            int observedMax = 0;
            var order = new List<int>();

            Task run = BoundedConcurrency.ForEachAsync(
                Enumerable.Range(0, ItemCount),
                Bound,
                async item =>
                {
                    int now = Interlocked.Increment(ref inFlight);
                    RaiseMax(ref observedMax, now);
                    lock (order)
                    {
                        order.Add(item);
                    }

                    await gate.Task;
                    Interlocked.Decrement(ref inFlight);
                },
                CancellationToken.None);

            // Bound reached, bound not exceeded - both proven without
            // waiting. Delete the semaphore and this reads ItemCount.
            Assert.Equal(Bound, Volatile.Read(ref inFlight));
            lock (order)
            {
                Assert.Equal(new[] { 0, 1 }, order);
            }

            gate.SetResult(true);
            await run;

            Assert.Equal(0, Volatile.Read(ref inFlight));
            Assert.Equal(Bound, Volatile.Read(ref observedMax));
            lock (order)
            {
                Assert.Equal(ItemCount, order.Count);
                Assert.Equal(Enumerable.Range(0, ItemCount), order.OrderBy(i => i));
            }
        }

        [Fact]
        public void EmptyInput_CompletesSynchronouslyAndNeverInvokesTheAction()
        {
            int calls = 0;

            Task run = BoundedConcurrency.ForEachAsync(
                new int[0],
                Bound,
                _ =>
                {
                    Interlocked.Increment(ref calls);
                    return Task.FromResult(true);
                },
                CancellationToken.None);

            // Already complete on return: the empty case takes neither the
            // semaphore nor a thread-pool hop.
            Assert.True(run.IsCompleted);
            Assert.False(run.IsFaulted);
            Assert.Equal(0, calls);
        }

        [Fact]
        public async Task AlreadyCancelledToken_ThrowsRatherThanRunningAnything()
        {
            int calls = 0;
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => BoundedConcurrency.ForEachAsync(
                        Enumerable.Range(0, ItemCount),
                        Bound,
                        _ =>
                        {
                            Interlocked.Increment(ref calls);
                            return Task.FromResult(true);
                        },
                        cts.Token));
            }

            Assert.Equal(0, calls);
        }

        [Fact]
        public async Task CancellingWhileParked_SurfacesTheCancellationInsteadOfHanging()
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int calls = 0;

            using (var cts = new CancellationTokenSource())
            {
                Task run = BoundedConcurrency.ForEachAsync(
                    Enumerable.Range(0, ItemCount),
                    1,
                    async _ =>
                    {
                        Interlocked.Increment(ref calls);
                        await gate.Task;
                        cts.Token.ThrowIfCancellationRequested();
                    },
                    cts.Token);

                Assert.Equal(1, Volatile.Read(ref calls));

                // The other five are parked on WaitAsync(ct). Cancelling
                // wakes them as cancelled; releasing the gate afterwards
                // lets the run finish rather than sit on WhenAll forever.
                // How many of the five lose the release-versus-cancel race
                // inside SemaphoreSlim is genuinely unspecified, so the
                // count is not pinned - the surfaced exception is.
                cts.Cancel();
                gate.SetResult(true);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public async Task NonPositiveBound_ThrowsInsteadOfDeadlocking(int maxConcurrency)
        {
            // new SemaphoreSlim(0) admits nobody and nothing releases it, so
            // the un-guarded form is an await that never returns.
            var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => BoundedConcurrency.ForEachAsync(
                    Enumerable.Range(0, ItemCount),
                    maxConcurrency,
                    _ => Task.FromResult(true),
                    CancellationToken.None));

            Assert.Equal("maxConcurrency", ex.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void RecipeService_RejectsANonPositiveBoundAtConstruction(int maxConcurrency)
        {
            // The same hang, one layer up: RecipeService passes this value
            // straight to ForEachAsync on the plan-generation path.
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new RecipeService(new InMemoryRecipeApiClient(), maxConcurrency));

            Assert.Equal("maxConcurrency", ex.ParamName);
        }

        private static void RaiseMax(ref int target, int candidate)
        {
            int seen = Volatile.Read(ref target);
            while (candidate > seen)
            {
                int prior = Interlocked.CompareExchange(ref target, candidate, seen);
                if (prior == seen)
                {
                    return;
                }

                seen = prior;
            }
        }
    }
}
