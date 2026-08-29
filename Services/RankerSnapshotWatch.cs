using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// WHEN the Crafting Ranker rebuilds itself after the account snapshot
    /// moves. The stamp its numbers were measured against, and the single
    /// outstanding rebuild request - Blish-free, so the rule can be driven
    /// through real sequences instead of only through a running game.
    ///
    /// <para>
    /// The rule this type exists to hold: a rebuild is two plan solves per
    /// row over up to <see cref="RankerWatchlistLimits.MaxEntries"/> rows,
    /// each walking a recipe tree and pulling Trading Post prices, against a
    /// snapshot that re-fetches on a timer. So the request is REMEMBERED
    /// here and spent by the view only where the user can watch it happen -
    /// never on a timer behind a hidden tab.
    /// </para>
    /// </summary>
    internal sealed class RankerSnapshotWatch
    {
        private DateTime? _stamp;
        private bool _pending;

        /// <summary>The snapshot the numbers on screen were measured against.</summary>
        public DateTime? Stamp
        {
            get { return _stamp; }
        }

        /// <summary>Whether a rebuild is owed and has not been started.</summary>
        public bool RebuildPending
        {
            get { return _pending; }
        }

        /// <summary>
        /// Records the stamp a starting run read on the main thread, so the
        /// numbers it is about to produce are judged against the snapshot it
        /// actually used. Deliberately does NOT clear an outstanding
        /// request: what a run answers is the snapshot it read, not any that
        /// arrived after it.
        /// </summary>
        public void MeasuredAgainst(DateTime? stamp)
        {
            _stamp = stamp;
        }

        /// <summary>
        /// Reports the snapshot as it is NOW. Returns true when it is newer
        /// than the numbers on screen, which is the caller's cue to drop its
        /// answer sets.
        /// <para>
        /// <paramref name="hadResults"/> decides whether a rebuild is owed as
        /// well: a table that has never been calculated is not waiting on
        /// one, and the first run of a session stays the user's to start.
        /// </para>
        /// </summary>
        public bool Observe(DateTime? stamp, bool hadResults)
        {
            if (stamp == _stamp)
            {
                return false;
            }

            _stamp = stamp;
            _pending |= hadResults;
            return true;
        }

        /// <summary>
        /// Takes the outstanding request, if there is one and this is a
        /// moment to spend it.
        /// <para>
        /// While <paramref name="isRefreshing"/> the request is refused and
        /// KEPT: that is what collapses a burst of snapshot commits during
        /// one run into a single rebuild after it, rather than one per
        /// commit. An empty list consumes the request and starts nothing -
        /// there are no rows to measure.
        /// </para>
        /// </summary>
        public bool TryTakeRebuild(bool isRefreshing, bool hasEntries)
        {
            if (!_pending || isRefreshing)
            {
                return false;
            }

            _pending = false;
            return hasEntries;
        }
    }
}
