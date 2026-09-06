using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// "A reflow is pending at width W", for a strip whose layout is a step
    /// function of its width. The plan tab's item input grid is one: it
    /// seats another column every time the window crosses a column-count
    /// boundary, so a drag that crosses several boundaries repacks the
    /// whole strip once per boundary and stretches every cell in between.
    /// The gate holds the newest width and hands it back exactly once, on
    /// the first take after the drag stops - see <see cref="TryTake"/>.
    /// <para>
    /// Blish-free and clock-injected: the caller passes the time and
    /// whether a drag is still running, so the state machine is decidable
    /// without a game loop.
    /// </para>
    /// </summary>
    internal sealed class DeferredReflowGate
    {
        private readonly double _stallMs;
        private bool _pending;
        private int _pendingWidth;
        private DateTime _pendingSinceUtc;

        /// <param name="stallMs">Ceiling on how long a drag that reads as
        /// still running may hold a reflow back - see <see cref="TryTake"/>.
        /// </param>
        public DeferredReflowGate(double stallMs)
        {
            _stallMs = stallMs > 0 ? stallMs : 0;
        }

        /// <summary>
        /// The width the strip is laid out at right now. Every layout
        /// derived from that width - the strip's own cells, and the height
        /// the content below it starts at - has to read this rather than
        /// the window's live width, or the two disagree for the length of
        /// a drag and the content moves ahead of the strip it sits under.
        /// </summary>
        public int AppliedWidth { get; private set; }

        public bool IsPending
        {
            get { return _pending; }
        }

        /// <summary>Width a take would hand back if one succeeded now.</summary>
        public int PendingWidth
        {
            get { return _pending ? _pendingWidth : AppliedWidth; }
        }

        /// <summary>
        /// Declares the strip laid out at <paramref name="width"/> and
        /// drops any deferred width, for a caller that just rebuilt the
        /// strip outright.
        /// </summary>
        public void Reset(int width)
        {
            AppliedWidth = width;
            _pendingWidth = width;
            _pending = false;
        }

        /// <summary>Abandons a deferred width without applying it - the
        /// panel it was measured against is gone.</summary>
        public void CancelPending()
        {
            _pendingWidth = AppliedWidth;
            _pending = false;
        }

        /// <summary>
        /// Records the width a resize tick reported, and returns whether a
        /// reflow is pending afterwards. A width equal to the applied one
        /// cancels the pending reflow rather than scheduling a no-op: a
        /// drag that returns to where it started has nothing to re-seat.
        /// </summary>
        public bool Observe(int width, DateTime nowUtc)
        {
            if (width == AppliedWidth)
            {
                CancelPending();
                return false;
            }

            _pending = true;
            _pendingWidth = width;
            _pendingSinceUtc = nowUtc;
            return true;
        }

        /// <summary>
        /// Hands back the deferred width once and only once. The gate
        /// counts it as applied from that moment, so a caller that takes a
        /// width must be able to act on it.
        /// <para>
        /// The release is the end of the drag and nothing else. No quiet
        /// interval releases a reflow: a hand steady for a moment is
        /// ordinary inside a drag, and re-seating the strip on that pause
        /// is what the drag felt clunky for.
        /// </para>
        /// <para>
        /// <c>stallMs</c> bounds the wait, because a drag flag can outlive
        /// the drag. It runs from the last width observed, so a live drag
        /// reaches it only by holding the grip still for the whole of it,
        /// and the number the caller passes has to be far longer than any
        /// pause a person makes mid-drag.
        /// </para>
        /// </summary>
        public bool TryTake(DateTime nowUtc, bool dragActive, out int width)
        {
            width = AppliedWidth;
            if (!_pending)
            {
                return false;
            }

            if (dragActive && (nowUtc - _pendingSinceUtc).TotalMilliseconds < _stallMs)
            {
                return false;
            }

            width = _pendingWidth;
            AppliedWidth = _pendingWidth;
            _pending = false;
            return true;
        }
    }
}
