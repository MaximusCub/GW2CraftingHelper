namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The four possible outcomes of one spinner-ticker frame reading
    /// <see cref="PlanStripStatusBoard"/> for its own generation - see
    /// <see cref="PlanStripTickDecision.Decide"/>.
    /// </summary>
    public enum PlanStripTickAction
    {
        /// <summary>
        /// This ticker's generation has been superseded by a newer one (or
        /// the board was never Begin()'d for it at all) - render nothing
        /// and stop; a fresh ArmSpinnerTicker call for the newer
        /// generation, not this ticker, owns the strip now.
        /// </summary>
        Stop,

        /// <summary>
        /// Still in flight - render the current phase text + spinner glyph
        /// and keep ticking.
        /// </summary>
        RenderSpinner,

        /// <summary>
        /// Just observed finished (InFlight flipped false since the last
        /// tick, or was already false on the very first tick) - render the
        /// final status text exactly once, then stop.
        /// </summary>
        RenderFinalAndStop,
    }

    /// <summary>
    /// Pure decision for what a status-strip spinner tick should do with a
    /// freshly read <see cref="PlanStripStatusSnapshot"/> (Gate round 1
    /// review-fix). Mirrors <see cref="StatusUpdateGuard"/>/
    /// <see cref="PhaseOrdinalGuard"/>'s shape/spirit: the race-sensitive
    /// "stop or render, and render what" decision the mandate calls out
    /// ("when the board reports finished, render the final status and stop
    /// itself") is pulled out of <c>CraftingPlanView.SpinnerTick</c> (a
    /// Blish-coupled instance method no test can reach) into a free
    /// function that takes only a snapshot and the ticker's own generation
    /// number, so the two orderings that actually matter -
    /// <see cref="PlanStripStatusBoard.Finish"/> landing before this
    /// ticker's very first tick ever runs, versus landing between two
    /// already-live ticks - are both directly testable without any Blish
    /// HUD control in the loop.
    /// </summary>
    public static class PlanStripTickDecision
    {
        public static PlanStripTickAction Decide(PlanStripStatusSnapshot snapshot, int myGen)
        {
            if (snapshot == null || snapshot.Sequence != myGen) return PlanStripTickAction.Stop;

            return snapshot.InFlight ? PlanStripTickAction.RenderSpinner : PlanStripTickAction.RenderFinalAndStop;
        }
    }
}
