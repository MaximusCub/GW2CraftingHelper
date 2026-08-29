namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pure decision for whether a queued generation-status update should still
    /// be applied.
    ///
    /// Once a generation's own completion status has been written, no
    /// later-draining tick for that same generation may overwrite it - checked
    /// at the moment each tick's callback actually RUNS, not when it was
    /// queued, which is what closes the race regardless of drain order. The
    /// pre-existing "myGen == currentGeneration" guard alone cannot catch this:
    /// both callbacks belong to the SAME generation, so that check passes for
    /// both.
    ///
    /// See CraftingPlanView.TriggerGenerate's statusProgress callback and its
    /// success/error MainThreadMarshal.Run callbacks, and docs/ARCHITECTURE.md
    /// section 6 for why the two callbacks have no FIFO guarantee between them.
    /// </summary>
    internal static class StatusUpdateGuard
    {
        public static bool ShouldApply(int tickGeneration, int currentGeneration, bool currentGenerationStatusClosed)
        {
            return tickGeneration == currentGeneration && !currentGenerationStatusClosed;
        }
    }
}
