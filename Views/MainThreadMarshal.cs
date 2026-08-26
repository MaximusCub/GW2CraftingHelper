using System;
using System.Threading;
using Blish_HUD;
using GW2CraftingHelper.Services;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// Marshals a single action onto the Blish HUD main (UI) thread. Blish
    /// HUD's XNA host installs no SynchronizationContext, so await
    /// continuations resume on ThreadPool threads by default; any code that
    /// mutates Blish HUD controls after an await must marshal back onto the
    /// main thread first.
    /// <para>
    /// This helper is for ONE-SHOT marshaling of async continuations onto
    /// the main thread ONLY - never re-queue from inside the callback
    /// passed to <see cref="Run"/>. GameService.Overlay.QueueMainThreadUpdate
    /// drains a re-queued callback again within the SAME frame instead of
    /// waiting for the next real Update() tick, so it cannot be used to
    /// step work across multiple frames (empirically confirmed via live
    /// trace - 400 same-frame re-queues observed in one drain).
    /// For multi-frame work, drive it from Control.DoUpdate instead - see
    /// FrameTicker in CraftingPlanView.
    /// </para>
    /// <para>See docs/ARCHITECTURE.md section 1.</para>
    /// </summary>
    internal static class MainThreadMarshal
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(MainThreadMarshal));

        // True while this class is already reporting a swallowed failure on
        // this thread. The Log tab rebuilds its rows through
        // MainThreadMarshal.Run (LogTabContent.RebuildRows), so a rebuild
        // that throws would otherwise write the entry that schedules the next
        // rebuild that throws. Same rule Module.cs applies to ModuleLogStore's
        // own IO failures: the log system's failures go to Blish's Logger,
        // never back into the log system.
        [ThreadStatic]
        private static bool _reportingFailure;

        // Signature of the last failure mirrored into the ring, so a
        // repeating failure costs one line rather than one per occurrence.
        // The recursion above is synchronous and the guard closes it; this
        // closes the ASYNCHRONOUS version of the same loop, where the entry
        // written here is what schedules the Log tab rebuild that throws
        // again. Only the bounded ring is deduplicated - Blish's Logger
        // still records every occurrence, so the repetition itself is not
        // lost.
        private static string _lastReportedSignature;

        /// <summary>
        /// Queues <paramref name="action"/> to run once on the main thread.
        /// A null action is ignored. Exceptions thrown by
        /// <paramref name="action"/> are caught and logged rather than
        /// propagated, since an unhandled exception inside the queued
        /// callback would otherwise take down Blish HUD's update loop.
        /// <para>
        /// Returns false when the action was dropped rather than queued, so
        /// a caller holding state the callback was meant to release (a
        /// disabled button, say) can tell that it will never run.
        /// </para>
        /// </summary>
        public static bool Run(Action action)
        {
            if (action == null)
            {
                return false;
            }

            var overlay = GameService.Overlay;
            if (overlay == null)
            {
                // Overlay is only null before module init completes or
                // after it has begun tearing down; either way the action
                // has nowhere to run. Previously dropped silently - logged
                // here so a caller who expected this to fire has a trail.
                Logger.Debug("MainThreadMarshal.Run dropped an action - GameService.Overlay was null");
                return false;
            }

            overlay.QueueMainThreadUpdate(_ =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "MainThreadMarshal queued action threw");
                    ReportSwallowedFailure(ex);
                }
            });
            return true;
        }

        /// <summary>
        /// Mirrors the swallowed exception into the module's own Log tab.
        /// This is the swallow point for every marshaled UI mutation in the
        /// module, so the symptoms it converts - "the plan strip froze on a
        /// phase", "I clicked Generate and nothing happened" - are exactly
        /// the ones a user reports and cannot diagnose, and Blish's file log
        /// is not what a bug report contains. Guarded rather than
        /// unconditional; see <see cref="_reportingFailure"/>.
        /// </summary>
        private static void ReportSwallowedFailure(Exception ex)
        {
            if (_reportingFailure)
            {
                return;
            }

            _reportingFailure = true;
            try
            {
                string signature = $"{ex.GetType().Name} - {ex.Message}";

                // string.Equals, not ==: the operands are two separately
                // interpolated strings, so reference equality would never
                // match and the suppression would never fire.
                string previous = Interlocked.Exchange(ref _lastReportedSignature, signature);
                if (string.Equals(previous, signature, StringComparison.Ordinal))
                {
                    return;
                }

                ModuleLog.Shared.Write(
                    ModuleLogLevel.Warn,
                    "ui",
                    $"A queued main-thread action threw and was swallowed: {signature}");
            }
            catch (Exception writeEx)
            {
                Logger.Warn(writeEx, "MainThreadMarshal could not record a queued-action failure to the module log");
            }
            finally
            {
                _reportingFailure = false;
            }
        }
    }
}
