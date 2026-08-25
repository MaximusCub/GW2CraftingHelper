using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class ResizeSettleDebounceTests
    {
        private const int SettleMs = 60;
        private const int WaitMs = 3000;

        [Fact]
        public async Task BurstOfResizeEventsRunsTheCallbackOnce()
        {
            int runs = 0;
            var debounce = new ResizeSettleDebounce(
                () => Interlocked.Increment(ref runs), RunInline, SettleMs, null);

            for (int i = 0; i < 200; i++)
            {
                debounce.Schedule();
            }

            Assert.True(await WaitForAsync(() => Volatile.Read(ref runs) > 0, WaitMs));
            await Task.Delay(SettleMs * 3);
            Assert.Equal(1, Volatile.Read(ref runs));
        }

        [Fact]
        public async Task TheCallbackTrailsTheLastEventByTheSettleWindow()
        {
            var clock = Stopwatch.StartNew();
            long runAtMs = -1;
            long lastEventMs = 0;

            var debounce = new ResizeSettleDebounce(
                () => Volatile.Write(ref runAtMs, clock.ElapsedMilliseconds),
                RunInline,
                SettleMs,
                null);

            // Re-arm a few times, then QUIESCE before measuring. Two
            // races had to go, both seen on GitHub runners 2026-08-25:
            // scheduling and stamping in the same loop iteration let a
            // stalled Task.Delay record an event AFTER the callback had
            // already run, and simply waiting for "runAtMs >= 0" returned
            // a value written during the burst rather than the one this
            // test is about. So the burst is allowed to finish, its
            // result is discarded, and only then is a single event timed.
            for (int i = 0; i < 4; i++)
            {
                debounce.Schedule();
                await Task.Delay(SettleMs / 4);
            }

            await Task.Delay(SettleMs * 4);
            Volatile.Write(ref runAtMs, -1);

            // Stamped BEFORE the final Schedule, so scheduling jitter can
            // only ever lengthen the measured trail, never shorten it.
            lastEventMs = clock.ElapsedMilliseconds;
            debounce.Schedule();

            Assert.True(await WaitForAsync(() => Volatile.Read(ref runAtMs) >= 0, WaitMs));

            // Floored at half the window rather than the whole of it: the
            // debounce stamps DateTime.UtcNow, whose Windows resolution is
            // ~15ms, so its idea of "now" can trail this Stopwatch's. Half
            // the window still separates a trailing callback from one that
            // fired on the event itself, which is the property under test.
            long trailedMs = Volatile.Read(ref runAtMs) - lastEventMs;
            Assert.True(
                trailedMs >= SettleMs / 2,
                $"ran {trailedMs}ms after the last event, inside the {SettleMs}ms settle window");
        }

        [Fact]
        public async Task ASecondDragArmsAgain()
        {
            int runs = 0;
            var debounce = new ResizeSettleDebounce(
                () => Interlocked.Increment(ref runs), RunInline, SettleMs, null);

            debounce.Schedule();
            Assert.True(await WaitForAsync(() => Volatile.Read(ref runs) == 1, WaitMs));

            debounce.Schedule();
            Assert.True(await WaitForAsync(() => Volatile.Read(ref runs) == 2, WaitMs));
        }

        [Fact]
        public async Task CancelDropsTheArmedCallbackAndRefusesFurtherArming()
        {
            int runs = 0;
            var debounce = new ResizeSettleDebounce(
                () => Interlocked.Increment(ref runs), RunInline, SettleMs, null);

            debounce.Schedule();
            debounce.Cancel();
            debounce.Schedule();

            await Task.Delay(SettleMs * 5);
            Assert.Equal(0, Volatile.Read(ref runs));
            Assert.False(debounce.Pending);
        }

        [Fact]
        public async Task ADroppedMarshalReleasesTheSlotSoALaterDragStillRuns()
        {
            int runs = 0;
            bool marshalWorks = false;

            var debounce = new ResizeSettleDebounce(
                () => Interlocked.Increment(ref runs),
                action =>
                {
                    if (!Volatile.Read(ref marshalWorks)) return false;
                    action();
                    return true;
                },
                SettleMs,
                null);

            debounce.Schedule();
            Assert.True(await WaitForAsync(() => !debounce.Pending, WaitMs));
            Assert.Equal(0, Volatile.Read(ref runs));

            Volatile.Write(ref marshalWorks, true);
            debounce.Schedule();
            Assert.True(await WaitForAsync(() => Volatile.Read(ref runs) == 1, WaitMs));
        }

        [Fact]
        public void NullCallbackOrMarshalIsRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ResizeSettleDebounce(null, RunInline, SettleMs, null));
            Assert.Throws<ArgumentNullException>(
                () => new ResizeSettleDebounce(() => { }, null, SettleMs, null));
        }

        [Fact]
        public void ANonPositiveSettleWindowFallsBackToTheModuleDefault()
        {
            var debounce = new ResizeSettleDebounce(() => { }, RunInline, 0, null);

            Assert.Equal(ResizeSettleDebounce.DefaultSettleMs, debounce.SettleMs);
        }

        private static bool RunInline(Action action)
        {
            action();
            return true;
        }

        private static async Task<bool> WaitForAsync(Func<bool> condition, int timeoutMs)
        {
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return true;
                await Task.Delay(5);
            }
            return condition();
        }
    }
}
