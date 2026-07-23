namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure decision for whether a queued generation-status update should
    /// still be applied (M34-B1 #4 - CraftingPlanView's stale "Building
    /// final result..." status race).
    ///
    /// A generation's trailing progress tick and that same generation's own
    /// completion write are two independently-scheduled main-thread
    /// callbacks with no FIFO guarantee between them: <c>Progress&lt;T&gt;</c>'s
    /// default SynchronizationContext hop (used for every progress tick)
    /// takes one extra ThreadPool round-trip versus the task-continuation
    /// path the completion write rides, so the completion write can - and,
    /// in practice, reliably does - reach the main-thread queue and drain
    /// BEFORE an earlier-queued trailing tick from the exact same
    /// generation, leaving the stale tick to overwrite the just-written
    /// completion text on the very next queued callback. The pre-existing
    /// "myGen == currentGeneration" guard alone cannot catch this: both
    /// callbacks belong to the SAME generation, so that check passes for
    /// both.
    ///
    /// The fix: once a generation's own completion status has been
    /// written, no later-draining tick for that same generation may
    /// overwrite it - checked here at the moment each tick's callback
    /// actually runs (not when it was queued), which is what closes the
    /// race regardless of drain order. See
    /// CraftingPlanView.TriggerGenerate's statusProgress callback and its
    /// success/error MainThreadMarshal.Run callbacks.
    /// <para>See docs/ARCHITECTURE.md section 6 (M38 WP-27).</para>
    /// </summary>
    public static class StatusUpdateGuard
    {
        public static bool ShouldApply(int tickGeneration, int currentGeneration, bool currentGenerationStatusClosed)
        {
            return tickGeneration == currentGeneration && !currentGenerationStatusClosed;
        }
    }
}
