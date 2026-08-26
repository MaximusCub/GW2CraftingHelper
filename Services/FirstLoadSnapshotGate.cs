using System;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Whether the module should fetch an account snapshot right now
    /// because it has none at all (Blish-free, so the rule is testable
    /// away from Module's timer).
    /// <para>
    /// The interval refresh in Module.Update only ever re-fetches a
    /// snapshot that has gone stale, so an install with nothing cached -
    /// a first run, or a Clear Cache - had no automatic route to its first
    /// one: Module.LoadAsync's own attempt is skipped when Blish has not
    /// granted the subtoken yet, and the SubtokenUpdated event that would
    /// cover that can have fired before the handler was attached. The tab
    /// then sat empty until the user pressed Refresh.
    /// </para>
    /// </summary>
    internal static class FirstLoadSnapshotGate
    {
        /// <summary>
        /// True on the one tick that should start the fetch. The caller
        /// records the attempt (alreadyAttempted) as it fires, so this
        /// stays a ONE-shot: a fetch that fails must leave the tab empty
        /// with its failure status rather than retry forever, and a fetch
        /// that succeeds hands over to the interval timer, which from then
        /// on has a snapshot to age. Clear Cache re-arms the shot
        /// (Module.ClearCache) - it recreates the nothing-cached state
        /// this gate is the only automatic answer to.
        /// <para>
        /// An attempt is only spent when it actually starts. Not-ready
        /// inputs - no API key yet, a refresh already running, the shared
        /// post-failure backoff still open (Module.LoadAsync's own failed
        /// attempt opens it) - return false WITHOUT the caller recording
        /// anything, so the one shot is still there when the module
        /// becomes able to use it.
        /// </para>
        /// </summary>
        public static bool ShouldRefreshNow(
            bool hasCachedSnapshot,
            bool apiReady,
            bool alreadyAttempted,
            bool refreshInProgress,
            bool inFailureBackoff)
        {
            if (hasCachedSnapshot || alreadyAttempted || refreshInProgress || inFailureBackoff)
            {
                return false;
            }

            return apiReady;
        }

        /// <summary>
        /// Throttle for the gate above. Its inputs are live readings, not
        /// cached flags (a permission probe, a clock read), and while the
        /// shot is still armed nothing else stops the caller re-taking them
        /// every frame - with no API key configured, for the whole session.
        /// True on the tick that has accumulated a full interval; carried
        /// is the caller's new accumulator either way.
        /// </summary>
        public static bool ShouldCheckNow(
            TimeSpan sinceLastCheck,
            TimeSpan elapsed,
            TimeSpan interval,
            out TimeSpan carried)
        {
            // Clamped, not trusted: a paused or resumed game can hand back
            // a wild frame delta, and a negative one must not walk the
            // accumulator backwards into never firing again.
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            if (interval <= TimeSpan.Zero || sinceLastCheck >= interval - elapsed)
            {
                carried = TimeSpan.Zero;
                return true;
            }

            carried = sinceLastCheck + elapsed;
            return false;
        }
    }
}
