using System;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class DeferredReflowGateTests
    {
        // The plan tab's own settle interval (CraftingPlanView.
        // ResizeDebounceMs). The gate takes it as a constructor argument,
        // so this is the value under test and not a copy of a rule.
        private const int SettleMs = 150;

        // 28 is Views/Rendering/UiMetrics.ButtonHeight, which the strip
        // passes into the grid - see ItemInputGridLayoutTests.
        private const int ButtonSize = 28;

        private static readonly DateTime T0 = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void AWidthChangeIsNotAppliedWhileTheIntervalIsStillRunning()
        {
            var gate = new DeferredReflowGate(SettleMs);
            gate.Reset(1000);

            gate.Observe(1200, T0);

            int width;
            Assert.False(gate.TryTake(T0.AddMilliseconds(149), pointerHeld: true, out width));
            Assert.Equal(1000, width);
            Assert.Equal(1000, gate.AppliedWidth);
            Assert.True(gate.IsPending);
        }

        [Fact]
        public void ABurstOfWidthsCollapsesToASingleTakeAtTheLastOne()
        {
            var gate = new DeferredReflowGate(SettleMs);
            gate.Reset(1000);

            // Ticks 30ms apart: the interval never elapses between two of
            // them, so only the last one can be the width that survives.
            for (int i = 1; i <= 10; i++)
            {
                gate.Observe(1000 + (i * 40), T0.AddMilliseconds(i * 30));
                int early;
                Assert.False(gate.TryTake(T0.AddMilliseconds(i * 30), pointerHeld: true, out early));
            }

            int width;
            Assert.True(gate.TryTake(T0.AddMilliseconds(300 + SettleMs), pointerHeld: true, out width));
            Assert.Equal(1400, width);
            Assert.Equal(1400, gate.AppliedWidth);

            int again;
            Assert.False(gate.TryTake(T0.AddMilliseconds(1000), pointerHeld: true, out again));
            Assert.False(gate.IsPending);
        }

        [Fact]
        public void ReleasingThePointerAppliesTheWidthWithoutWaitingOutTheInterval()
        {
            var gate = new DeferredReflowGate(SettleMs);
            gate.Reset(1000);
            gate.Observe(1180, T0);

            int width;
            Assert.True(gate.TryTake(T0.AddMilliseconds(1), pointerHeld: false, out width));
            Assert.Equal(1180, width);
            Assert.Equal(1180, gate.AppliedWidth);
            Assert.False(gate.IsPending);
        }

        [Fact]
        public void ADragThatEndsWhereItStartedLeavesNothingToApply()
        {
            var gate = new DeferredReflowGate(SettleMs);
            gate.Reset(1000);

            gate.Observe(1400, T0);
            Assert.True(gate.IsPending);

            gate.Observe(1000, T0.AddMilliseconds(40));

            int width;
            Assert.False(gate.IsPending);
            Assert.False(gate.TryTake(T0.AddMilliseconds(400), pointerHeld: false, out width));
            Assert.Equal(1000, gate.AppliedWidth);
        }

        [Fact]
        public void AnIdleGateHasNothingToTake()
        {
            var gate = new DeferredReflowGate(SettleMs);
            gate.Reset(1000);

            int width;
            Assert.False(gate.TryTake(T0, pointerHeld: false, out width));
            Assert.Equal(1000, width);
        }

        [Fact]
        public void CancellingKeepsTheWidthTheStripIsAlreadyLaidOutAt()
        {
            var gate = new DeferredReflowGate(SettleMs);
            gate.Reset(1000);
            gate.Observe(1400, T0);

            gate.CancelPending();

            int width;
            Assert.False(gate.IsPending);
            Assert.Equal(1000, gate.AppliedWidth);
            Assert.False(gate.TryTake(T0.AddMilliseconds(400), pointerHeld: false, out width));
        }

        [Fact]
        public void RebuildingTheStripAdoptsTheNewWidthAndDropsTheDeferredOne()
        {
            var gate = new DeferredReflowGate(SettleMs);
            gate.Reset(1000);
            gate.Observe(1400, T0);

            gate.Reset(900);

            int width;
            Assert.False(gate.IsPending);
            Assert.Equal(900, gate.AppliedWidth);
            Assert.False(gate.TryTake(T0.AddMilliseconds(400), pointerHeld: false, out width));
        }

        // ---- The defect the gate exists for ----

        /// <summary>
        /// The reported symptom: dragging the window repacks the item input
        /// strip every time the width crosses a column-count boundary, and
        /// stretches every cell in between. Driving the same drag through
        /// the gate, the row count the strip is laid out for holds still
        /// for the whole drag and changes exactly once, at the end.
        /// </summary>
        [Fact]
        public void AWidthSweepAcrossColumnBoundariesRepacksTheStripOnceInsteadOfPerBoundary()
        {
            const int items = 5;
            const int start = 500;
            const int end = 1800;

            var gate = new DeferredReflowGate(SettleMs);
            gate.Reset(start);

            int startRows = ItemInputGridLayout.RowCount(items, start, ButtonSize);
            int liveRows = startRows;
            int gatedRows = startRows;
            int liveRepacks = 0;
            int gatedRepacks = 0;

            for (int w = start; w <= end; w += 4)
            {
                var now = T0.AddMilliseconds((w - start) / 4 * 8);
                gate.Observe(w, now);

                int live = ItemInputGridLayout.RowCount(items, w, ButtonSize);
                if (live != liveRows)
                {
                    liveRows = live;
                    liveRepacks++;
                }

                int gated = ItemInputGridLayout.RowCount(items, gate.AppliedWidth, ButtonSize);
                if (gated != gatedRows)
                {
                    gatedRows = gated;
                    gatedRepacks++;
                }

                int midDrag;
                Assert.False(gate.TryTake(now, pointerHeld: true, out midDrag));
                Assert.Equal(startRows, gated);
            }

            int settledWidth;
            Assert.True(gate.TryTake(T0.AddMilliseconds(100000), pointerHeld: false, out settledWidth));
            Assert.Equal(end, settledWidth);

            if (ItemInputGridLayout.RowCount(items, settledWidth, ButtonSize) != gatedRows)
            {
                gatedRepacks++;
            }

            Assert.True(
                liveRepacks >= 2,
                $"the sweep has to cross at least two boundaries to be the reported drag; crossed {liveRepacks}");
            Assert.Equal(1, gatedRepacks);
            Assert.Equal(
                ItemInputGridLayout.RowCount(items, end, ButtonSize),
                ItemInputGridLayout.RowCount(items, gate.AppliedWidth, ButtonSize));
        }
    }
}
