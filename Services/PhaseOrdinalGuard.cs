namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pure decision for whether a live <see cref="PlanPhaseEvent"/> should
    /// still be applied to the status strip (monotonic
    /// phase ordering). Mirrors StatusUpdateGuard's shape/spirit: a pure,
    /// Blish-free function CraftingPlanView calls at the moment each event
    /// actually drains, since the race it closes cannot be seen any other
    /// way.
    ///
    /// <c>Progress&lt;T&gt;</c> with no SynchronizationContext installed (see
    /// Views/MainThreadMarshal.cs's own doc comment - this module has none)
    /// posts every <c>Report</c> through an independent
    /// <c>ThreadPool.QueueUserWorkItem</c>, so two phase events reported
    /// milliseconds apart (a warm recipe/price cache, a small plan) can be
    /// executed out of order by different worker threads before either
    /// reaches the main-thread queue. StatusUpdateGuard alone cannot catch
    /// this: both events belong to the SAME generation, so its myGen check
    /// passes for both, and the later-draining OLDER event can overwrite a
    /// newer phase's text.
    ///
    /// <see cref="PlanPhase"/>'s declaration order is the pipeline's actual
    /// emission order (BuildingTree -&gt; FetchingPrices -&gt;
    /// SolvingDecisions -&gt; FetchingItemDetails -&gt;
    /// CheckingLearnedRecipes -&gt; BuildingDisplay -
    /// see PlanPhaseEvent's own doc comment and CraftingPlanPipeline's
    /// phaseTracker.Start call sites, which fire strictly in that order on
    /// both the single-item and multi-item paths), so its int ordinal is a
    /// reliable monotonic sequence per generation: an event whose phase
    /// ordinal is not strictly greater than the last one actually applied
    /// is stale and must be dropped, regardless of drain order.
    /// </summary>
    internal static class PhaseOrdinalGuard
    {
        public static bool ShouldApply(int eventPhaseOrdinal, int currentPhaseOrdinal)
        {
            return eventPhaseOrdinal > currentPhaseOrdinal;
        }
    }
}
