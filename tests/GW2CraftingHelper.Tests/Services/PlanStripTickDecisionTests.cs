using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanStripTickDecisionTests
    {
        [Fact]
        public void InFlight_SameGeneration_RendersSpinner()
        {
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.UpdatePhase(1, 0, "Building recipe tree...");

            var action = PlanStripTickDecision.Decide(board.Snapshot(), myGen: 1);

            Assert.Equal(PlanStripTickAction.RenderSpinner, action);
        }

        [Fact]
        public void FinishLandedBeforeFirstTick_RendersFinalAndStops()
        {
            // The exact "no-tab-switch" completion path: Finish() lands on
            // the board (via TriggerGenerate's success/catch callback)
            // before the spinner ticker's very first DoUpdate ever runs -
            // e.g. a very fast generation, or a ticker armed just as the
            // pipeline was already wrapping up. The very first tick must
            // still surface the final text, not silently stop with nothing
            // rendered.
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.Finish(1, "Plan generated - Aug 8, 2026 3:00 PM");

            var action = PlanStripTickDecision.Decide(board.Snapshot(), myGen: 1);

            Assert.Equal(PlanStripTickAction.RenderFinalAndStop, action);
        }

        [Fact]
        public void FinishLandsBetweenTwoTicks_SecondTickRendersFinalAndStops()
        {
            // The steady-state completion path: several spinner ticks
            // already rendered "in flight" while the generation ran, then
            // Finish() lands between two ticks - the very next tick must
            // flip from spinner to final text and stop, not keep spinning.
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.UpdatePhase(1, 0, "Building recipe tree...");

            var firstTick = PlanStripTickDecision.Decide(board.Snapshot(), myGen: 1);
            Assert.Equal(PlanStripTickAction.RenderSpinner, firstTick);

            board.Finish(1, "Plan generated - Aug 8, 2026 3:00 PM");

            var secondTick = PlanStripTickDecision.Decide(board.Snapshot(), myGen: 1);
            Assert.Equal(PlanStripTickAction.RenderFinalAndStop, secondTick);
        }

        [Fact]
        public void SupersededGeneration_Stops()
        {
            // A ticker armed for generation 1 that is still ticking after a
            // brand-new generation 2 has since begun (e.g. re-Generate
            // clicked mid-flight) must stop immediately without rendering -
            // ArmSpinnerTicker's own fresh ticker for generation 2 owns the
            // strip now.
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.Begin(2);

            var action = PlanStripTickDecision.Decide(board.Snapshot(), myGen: 1);

            Assert.Equal(PlanStripTickAction.Stop, action);
        }

        [Fact]
        public void NeverBegunBoard_Stops()
        {
            // A ticker somehow ticking against a virgin board (Sequence 0,
            // never Begin()'d) - defensive: must stop, not render "Ready"
            // or a null-text spinner line.
            var board = new PlanStripStatusBoard();

            var action = PlanStripTickDecision.Decide(board.Snapshot(), myGen: 1);

            Assert.Equal(PlanStripTickAction.Stop, action);
        }

        [Fact]
        public void NullSnapshot_Stops()
        {
            Assert.Equal(PlanStripTickAction.Stop, PlanStripTickDecision.Decide(null, myGen: 1));
        }

        [Fact]
        public void FormatPhaseText_NullEvent_ReturnsGenerating()
        {
            Assert.Equal("Generating...", PlanStripTickDecision.FormatPhaseText(null));
        }

        [Fact]
        public void FormatPhaseText_EmptyDisplayName_ReturnsGenerating()
        {
            var pe = new PlanPhaseEvent { Phase = PlanPhase.BuildingTree, DisplayName = "" };

            Assert.Equal("Generating...", PlanStripTickDecision.FormatPhaseText(pe));
        }

        [Fact]
        public void FormatPhaseText_WithTotal_AppendsItemCount()
        {
            var pe = new PlanPhaseEvent { Phase = PlanPhase.FetchingPrices, DisplayName = "Fetching prices", Total = 418 };

            Assert.Equal("Fetching prices (418 items)...", PlanStripTickDecision.FormatPhaseText(pe));
        }

        [Fact]
        public void FormatPhaseText_NoTotal_WithDetail_AppendsDetail()
        {
            // The documented Detail-fallback regression case: the very
            // first "Building recipe tree" event of a cold recipe cache
            // carries no item count (Total) but does carry the first-run
            // hint as Detail (CraftingPlanPipeline.FirstRunTreeHint) - this
            // is the ONLY way that hint still reaches the live status strip
            // now that CraftingPlanView passes progress: null to the old
            // IProgress<PlanStatus> channel. A regression here (e.g.
            // reordering the Total/Detail checks, or dropping the Detail
            // branch) would silently make the hint unreachable again.
            var pe = new PlanPhaseEvent
            {
                Phase = PlanPhase.BuildingTree,
                DisplayName = "Building recipe tree",
                Total = null,
                Detail = "may take several seconds on first run",
            };

            Assert.Equal(
                "Building recipe tree (may take several seconds on first run)...",
                PlanStripTickDecision.FormatPhaseText(pe));
        }

        [Fact]
        public void FormatPhaseText_TotalAndDetailBothPresent_TotalTakesPriority()
        {
            // Total is checked before Detail - a phase that somehow carries
            // both must never render both onto the same line.
            var pe = new PlanPhaseEvent
            {
                Phase = PlanPhase.FetchingPrices,
                DisplayName = "Fetching prices",
                Total = 5,
                Detail = "should not appear",
            };

            Assert.Equal("Fetching prices (5 items)...", PlanStripTickDecision.FormatPhaseText(pe));
        }

        [Fact]
        public void FormatPhaseText_NoTotalNoDetail_PlainEllipsis()
        {
            var pe = new PlanPhaseEvent { Phase = PlanPhase.SolvingDecisions, DisplayName = "Solving decisions" };

            Assert.Equal("Solving decisions...", PlanStripTickDecision.FormatPhaseText(pe));
        }
    }
}
