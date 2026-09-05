using System;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class DeferredReflowGateTests
    {
        // The plan tab's own settle interval (CraftingPlanView.
        // ResizeDebounceMs) and stall ceiling (StripReflowStallMs). The
        // gate takes both as constructor arguments, so these are the values
        // under test and not copies of a rule.
        private const int SettleMs = 150;
        private const int StallMs = 2000;

        private static DeferredReflowGate NewGate()
        {
            return new DeferredReflowGate(SettleMs, StallMs);
        }

        // 28 is Views/Rendering/UiMetrics.ButtonHeight, which the strip
        // passes into the grid - see ItemInputGridLayoutTests.
        private const int ButtonSize = 28;

        private static readonly DateTime T0 = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void AWidthChangeIsNotAppliedWhileThePointerIsStillHeld()
        {
            var gate = NewGate();
            gate.Reset(1000);

            gate.Observe(1200, T0, pointerHeld: true);

            int width;
            Assert.False(gate.TryTake(T0.AddMilliseconds(149), pointerHeld: true, out width));
            Assert.Equal(1000, width);
            Assert.Equal(1000, gate.AppliedWidth);
            Assert.True(gate.IsPending);
        }

        [Fact]
        public void ABurstOfWidthsCollapsesToASingleTakeAtTheLastOne()
        {
            var gate = NewGate();
            gate.Reset(1000);

            for (int i = 1; i <= 10; i++)
            {
                gate.Observe(1000 + (i * 40), T0.AddMilliseconds(i * 30), pointerHeld: true);
                int early;
                Assert.False(gate.TryTake(T0.AddMilliseconds(i * 30), pointerHeld: true, out early));
            }

            int width;
            Assert.True(gate.TryTake(T0.AddMilliseconds(400), pointerHeld: false, out width));
            Assert.Equal(1400, width);
            Assert.Equal(1400, gate.AppliedWidth);

            int again;
            Assert.False(gate.TryTake(T0.AddMilliseconds(1000), pointerHeld: false, out again));
            Assert.False(gate.IsPending);
        }

        /// <summary>
        /// The defect this gate's release rule was rewritten for: a hand
        /// steady for longer than the settle interval is ordinary inside a
        /// drag, and treating that as the end of one re-seats the strip in
        /// the middle of it. A held pointer holds the reflow back however
        /// long the pause runs.
        /// </summary>
        [Fact]
        public void APauseInTheMiddleOfADragDoesNotReleaseTheReflow()
        {
            var gate = NewGate();
            gate.Reset(1000);

            gate.Observe(1200, T0, pointerHeld: true);

            int width;
            for (int elapsed = SettleMs; elapsed <= 10 * SettleMs; elapsed += SettleMs)
            {
                Assert.False(gate.TryTake(T0.AddMilliseconds(elapsed), pointerHeld: true, out width));
                Assert.True(gate.IsPending);
                Assert.Equal(1000, gate.AppliedWidth);
            }

            // The drag resumes and then ends: one reflow, at the last width.
            gate.Observe(1300, T0.AddMilliseconds(1600), pointerHeld: true);
            Assert.True(gate.TryTake(T0.AddMilliseconds(1620), pointerHeld: false, out width));
            Assert.Equal(1300, width);
        }

        /// <summary>
        /// A pointer state stuck at "held" (Blish keeps its last mouse
        /// sample while the game is unfocused) must not strand the strip at
        /// its pre-drag width for the session.
        /// </summary>
        [Fact]
        public void AStuckPointerReleasesTheReflowAtTheStallCeiling()
        {
            var gate = NewGate();
            gate.Reset(1000);

            gate.Observe(1200, T0, pointerHeld: true);

            int width;
            Assert.False(gate.TryTake(T0.AddMilliseconds(StallMs - 1), pointerHeld: true, out width));
            Assert.True(gate.TryTake(T0.AddMilliseconds(StallMs), pointerHeld: true, out width));
            Assert.Equal(1200, width);
            Assert.False(gate.IsPending);
        }

        /// <summary>
        /// A resize no pointer drove - the sprite screen changing size under
        /// the window, or a size restored from settings - has no release to
        /// wait for, so the quiet interval is what releases it. It still has
        /// to coalesce: a burst of such writes is one reflow, not one each.
        /// </summary>
        [Fact]
        public void APointerlessResizeBurstStillCollapsesOnTheQuietInterval()
        {
            var gate = NewGate();
            gate.Reset(1000);

            int width;
            for (int i = 1; i <= 4; i++)
            {
                gate.Observe(1000 + (i * 40), T0.AddMilliseconds(i * 20), pointerHeld: false);
                Assert.False(gate.TryTake(T0.AddMilliseconds(i * 20), pointerHeld: false, out width));
            }

            Assert.False(gate.TryTake(T0.AddMilliseconds(80 + SettleMs - 1), pointerHeld: false, out width));
            Assert.True(gate.TryTake(T0.AddMilliseconds(80 + SettleMs), pointerHeld: false, out width));
            Assert.Equal(1160, width);
        }

        [Fact]
        public void ReleasingThePointerAppliesTheWidthWithoutWaitingOutTheInterval()
        {
            var gate = NewGate();
            gate.Reset(1000);
            gate.Observe(1180, T0, pointerHeld: true);

            int width;
            Assert.True(gate.TryTake(T0.AddMilliseconds(1), pointerHeld: false, out width));
            Assert.Equal(1180, width);
            Assert.Equal(1180, gate.AppliedWidth);
            Assert.False(gate.IsPending);
        }

        [Fact]
        public void ADragThatEndsWhereItStartedLeavesNothingToApply()
        {
            var gate = NewGate();
            gate.Reset(1000);

            gate.Observe(1400, T0, pointerHeld: true);
            Assert.True(gate.IsPending);

            gate.Observe(1000, T0.AddMilliseconds(40), pointerHeld: true);

            int width;
            Assert.False(gate.IsPending);
            Assert.False(gate.TryTake(T0.AddMilliseconds(400), pointerHeld: false, out width));
            Assert.Equal(1000, gate.AppliedWidth);
        }

        [Fact]
        public void AnIdleGateHasNothingToTake()
        {
            var gate = NewGate();
            gate.Reset(1000);

            int width;
            Assert.False(gate.TryTake(T0, pointerHeld: false, out width));
            Assert.Equal(1000, width);
        }

        [Fact]
        public void CancellingKeepsTheWidthTheStripIsAlreadyLaidOutAt()
        {
            var gate = NewGate();
            gate.Reset(1000);
            gate.Observe(1400, T0, pointerHeld: true);

            gate.CancelPending();

            int width;
            Assert.False(gate.IsPending);
            Assert.Equal(1000, gate.AppliedWidth);
            Assert.False(gate.TryTake(T0.AddMilliseconds(400), pointerHeld: false, out width));
        }

        [Fact]
        public void RebuildingTheStripAdoptsTheNewWidthAndDropsTheDeferredOne()
        {
            var gate = NewGate();
            gate.Reset(1000);
            gate.Observe(1400, T0, pointerHeld: true);

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

            var gate = NewGate();
            gate.Reset(start);

            int startRows = ItemInputGridLayout.RowCount(items, start, ButtonSize);
            int liveRows = startRows;
            int gatedRows = startRows;
            int liveRepacks = 0;
            int gatedRepacks = 0;

            for (int w = start; w <= end; w += 4)
            {
                var now = T0.AddMilliseconds((w - start) / 4 * 8);
                gate.Observe(w, now, pointerHeld: true);

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
