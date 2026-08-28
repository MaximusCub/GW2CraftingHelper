using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// Every settle-window test here drives a MANUAL clock through the
    /// debounce's utcNow/delay seams. The test's delay implementation does
    /// not sleep: it moves the fake clock to the wait's deadline and returns
    /// an already-completed task, so the whole settle loop runs inline on
    /// the test thread and the callback has either fired or not by the time
    /// Schedule() returns. Nothing here polls, sleeps, or measures a real
    /// duration.
    /// <para>
    /// This replaces a file that carried its own record of two GitHub-runner
    /// races and a tolerance halved to absorb Windows' ~15ms clock
    /// resolution. Only the last test still uses the real Task.Delay, and it
    /// asserts an event, never an interval.
    /// </para>
    /// </summary>
    public class ResizeSettleDebounceTests
    {
        private const int SettleMs = 60;

        /// <summary>
        /// Fake clock. Time moves only when the debounce asks to wait or a
        /// test explicitly advances it, so "the window elapsed" and "the
        /// waiter woke" are the same event and never two racing ones.
        /// </summary>
        private sealed class ManualClock
        {
            private DateTime _now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            public readonly List<int> RequestedWaits = new List<int>();

            public DateTime UtcNow => _now;

            public double ElapsedMs =>
                (_now - new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

            /// <summary>Runs before the clock advances, i.e. while the
            /// waiter is notionally asleep - where a resize event landing
            /// mid-window belongs.</summary>
            public Action<int> DuringWait { get; set; }

            /// <summary>
            /// Advances to the DEADLINE, not by the duration: whatever the
            /// hook consumed while the waiter was asleep counts against the
            /// wait, exactly as real elapsed time would.
            /// </summary>
            public Task Delay(int ms)
            {
                DateTime deadline = _now.AddMilliseconds(ms);
                RequestedWaits.Add(ms);
                DuringWait?.Invoke(RequestedWaits.Count);
                if (_now < deadline)
                {
                    _now = deadline;
                }

                return Task.FromResult(true);
            }

            public void Advance(int ms)
            {
                _now = _now.AddMilliseconds(ms);
            }
        }

        private static ResizeSettleDebounce Build(
            ManualClock clock, Action onSettled, Func<Action, bool> marshal = null)
        {
            return new ResizeSettleDebounce(
                onSettled,
                marshal ?? RunInline,
                SettleMs,
                null,
                () => clock.UtcNow,
                clock.Delay);
        }

        [Fact]
        public void ABurstOfResizeEventsRunsTheCallbackOnce()
        {
            var clock = new ManualClock();
            int runs = 0;
            var debounce = Build(clock, () => runs++);

            // A 199ms drag: 200 resize events, one per millisecond, all of
            // them landing while the first waiter is asleep. That is the
            // shape the debounce exists for.
            clock.DuringWait = waitNumber =>
            {
                if (waitNumber != 1)
                {
                    return;
                }

                for (int i = 0; i < 199; i++)
                {
                    clock.Advance(1);
                    debounce.Schedule();
                }
            };

            debounce.Schedule();

            Assert.Equal(1, runs);
            Assert.False(debounce.Pending);

            // Two waits, not two hundred: the drag re-arms the one waiter
            // rather than queueing a callback per event.
            Assert.Equal(new[] { SettleMs, SettleMs }, clock.RequestedWaits);
            Assert.Equal(199 + SettleMs, clock.ElapsedMs);
        }

        [Fact]
        public void TheCallbackTrailsTheLASTEventByTheWholeSettleWindow()
        {
            var clock = new ManualClock();
            double lastEventAtMs = -1;
            double ranAtMs = -1;
            var debounce = Build(clock, () => ranAtMs = clock.ElapsedMs);

            // Halfway through the first window a second resize event lands.
            // The waiter must re-arm against IT, not fire on the original
            // stamp - the whole point of a trailing debounce.
            clock.DuringWait = waitNumber =>
            {
                if (waitNumber != 1)
                {
                    return;
                }

                clock.Advance(SettleMs / 2);
                lastEventAtMs = clock.ElapsedMs;
                debounce.Schedule();
            };

            debounce.Schedule();

            // Exactly two waits: the original window, then the remainder
            // measured from the second event.
            Assert.Equal(new[] { SettleMs, SettleMs / 2 }, clock.RequestedWaits);
            Assert.Equal(SettleMs, ranAtMs - lastEventAtMs);
        }

        [Fact]
        public void TheCallbackDoesNotRunWhileTheWindowIsStillOpen()
        {
            var clock = new ManualClock();
            int runs = 0;
            var debounce = Build(clock, () => runs++);

            // Time is frozen one millisecond short of the window. The
            // waiter's only continuation is the delay it just asked for, so
            // "has not run yet" is a fact here, not a race lost.
            clock.DuringWait = _ =>
            {
                clock.Advance(SettleMs - 1);
                Assert.Equal(0, runs);
                Assert.True(debounce.Pending);
            };

            debounce.Schedule();

            Assert.Equal(1, runs);
        }

        [Fact]
        public void ASecondDragArmsAgain()
        {
            var clock = new ManualClock();
            int runs = 0;
            var debounce = Build(clock, () => runs++);

            debounce.Schedule();
            Assert.Equal(1, runs);

            debounce.Schedule();
            Assert.Equal(2, runs);
            Assert.Equal(new[] { SettleMs, SettleMs }, clock.RequestedWaits);
        }

        [Fact]
        public void CancelDropsTheArmedCallbackAndRefusesFurtherArming()
        {
            var clock = new ManualClock();
            int runs = 0;
            var debounce = Build(clock, () => runs++);

            // Cancel lands while the window is open, which is the teardown
            // case: a view disposing its control tree mid-drag.
            clock.DuringWait = _ => debounce.Cancel();

            debounce.Schedule();
            Assert.Equal(0, runs);
            Assert.False(debounce.Pending);

            debounce.Schedule();
            Assert.Equal(0, runs);
            Assert.False(debounce.Pending);
            Assert.Single(clock.RequestedWaits);
        }

        [Fact]
        public void ADroppedMarshalReleasesTheSlotSoALaterDragStillRuns()
        {
            var clock = new ManualClock();
            int runs = 0;
            bool marshalWorks = false;

            var debounce = Build(
                clock,
                () => runs++,
                action =>
                {
                    if (!marshalWorks)
                    {
                        return false;
                    }

                    action();
                    return true;
                });

            debounce.Schedule();
            Assert.False(debounce.Pending);
            Assert.Equal(0, runs);

            marshalWorks = true;
            debounce.Schedule();
            Assert.Equal(1, runs);
        }

        [Fact]
        public void AThrowingMarshalIsReportedAndReleasesTheSlot()
        {
            var clock = new ManualClock();
            Exception reported = null;
            var boom = new InvalidOperationException("marshal exploded");

            var debounce = new ResizeSettleDebounce(
                () => { },
                _ => throw boom,
                SettleMs,
                ex => reported = ex,
                () => clock.UtcNow,
                clock.Delay);

            debounce.Schedule();

            Assert.Same(boom, reported);
            Assert.False(debounce.Pending);
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

        [Fact]
        public async Task TheProductionWiringRunsTheCallbackWithNoSeamsSupplied()
        {
            // The one test on the real DateTime.UtcNow and the real
            // Task.Delay, because every other test above stubs both and
            // something has to prove the default wiring is connected. It
            // asserts an EVENT, never an interval, so no runner speed can
            // change its verdict; the timeout only turns a hang into a
            // failure.
            var ran = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var debounce = new ResizeSettleDebounce(
                () => ran.TrySetResult(true), RunInline, 1, null);

            debounce.Schedule();

            Task finished = await Task.WhenAny(ran.Task, Task.Delay(30000));
            Assert.Same(ran.Task, finished);
        }

        private static bool RunInline(Action action)
        {
            action();
            return true;
        }
    }
}
