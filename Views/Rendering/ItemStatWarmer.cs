using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// THE background stat top-up, for a tab that draws item rows whose
    /// stat blocks nothing has fetched yet.
    ///
    /// <para>
    /// It exists because the session stat cache
    /// (<c>ItemMetadataService.GetCachedStatBlock</c>) is a PURE READ - it
    /// never fetches, because its caller is a hover on the UI thread. A tab
    /// handed only that accessor can therefore render a full item tooltip
    /// only for items some earlier plan happened to touch, and shows the
    /// identity-only fallback for everything else. Warming is what closes
    /// the gap between "this row knows its name, icon and rarity" and "this
    /// row shows the same tooltip the game does".
    /// </para>
    ///
    /// <para>
    /// Fire and forget by design: the rows are already on screen and their
    /// hovers are DEFERRED, so the next hover picks up whatever landed
    /// without a re-render. <c>RefreshCurrent</c> covers the one case that
    /// cannot wait for a next hover - the cursor already resting on a row
    /// when the batch lands.
    /// </para>
    ///
    /// <para>
    /// Cancellation lives in the delegate, not here: every call site binds
    /// <c>WarmStatBlocksAsync</c> to the module lifetime token in Module.cs,
    /// so unloading the module ends an in-flight warm before the HttpClient
    /// underneath it is disposed.
    /// </para>
    /// </summary>
    internal sealed class ItemStatWarmer
    {
        private readonly Func<IReadOnlyList<int>, Task<int>> _warmAsync;
        private readonly string _logTag;

        /// <summary>
        /// <paramref name="warmAsync"/> may be null - a view constructed
        /// without one keeps the identity-only tooltips it had, which is
        /// the pre-existing behaviour rather than a failure.
        /// </summary>
        internal ItemStatWarmer(Func<IReadOnlyList<int>, Task<int>> warmAsync, string logTag)
        {
            _warmAsync = warmAsync;
            _logTag = logTag;
        }

        /// <summary>
        /// Warms the stat blocks for the items a tab is about to draw.
        /// Cheap to call on every tab visit: <c>WarmStatBlocksAsync</c>
        /// filters out every id the cache already holds and issues no
        /// request at all when nothing is left, so a revisit costs one
        /// dictionary scan.
        /// </summary>
        internal void Start(IReadOnlyList<int> itemIds)
        {
            if (_warmAsync == null || itemIds == null || itemIds.Count == 0)
            {
                return;
            }

            _ = RunAsync(itemIds);
        }

        private async Task RunAsync(IReadOnlyList<int> itemIds)
        {
            try
            {
                int filled = await _warmAsync(itemIds).ConfigureAwait(false);
                if (filled > 0)
                {
                    MainThreadMarshal.Run(TooltipFacility.RefreshCurrent);
                }
            }
            catch (Exception ex)
            {
                // Best effort, and quiet: failing to warm degrades to the
                // identity-only tooltip the row would otherwise have shown,
                // which is not an error the player needs to hear about.
                ModuleLog.Shared.Write(
                    ModuleLogLevel.Debug,
                    _logTag,
                    $"Item stat top-up did not complete: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
