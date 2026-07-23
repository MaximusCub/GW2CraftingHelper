using System;
using Blish_HUD;

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
    /// trace during M30 - 400 same-frame re-queues observed in one drain).
    /// For multi-frame work, drive it from Control.DoUpdate instead - see
    /// FrameTicker in CraftingPlanView.
    /// </para>
    /// <para>See docs/ARCHITECTURE.md section 1 (M38 WP-27).</para>
    /// </summary>
    internal static class MainThreadMarshal
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(MainThreadMarshal));

        /// <summary>
        /// Queues <paramref name="action"/> to run once on the main thread.
        /// A null action is ignored. Exceptions thrown by
        /// <paramref name="action"/> are caught and logged rather than
        /// propagated, since an unhandled exception inside the queued
        /// callback would otherwise take down Blish HUD's update loop.
        /// </summary>
        public static void Run(Action action)
        {
            if (action == null) return;

            var overlay = GameService.Overlay;
            if (overlay == null)
            {
                // Overlay is only null before module init completes or
                // after it has begun tearing down; either way the action
                // has nowhere to run. Previously dropped silently - logged
                // here so a caller who expected this to fire has a trail.
                Logger.Debug("MainThreadMarshal.Run dropped an action - GameService.Overlay was null");
                return;
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
                }
            });
        }
    }
}
