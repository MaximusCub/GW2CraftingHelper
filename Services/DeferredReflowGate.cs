using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// "A reflow is pending at width W", for a strip whose layout is a step
    /// function of its width. The plan tab's item input grid is one: it
    /// seats another column every time the window crosses a column-count
    /// boundary, so a drag that crosses several boundaries repacks the
    /// whole strip once per boundary and stretches every cell in between.
    /// The gate holds the newest width and hands it back exactly once - at
    /// the end of a quiet interval, or as soon as the pointer is released,
    /// whichever comes first.
    /// <para>
    /// Blish-free and clock-injected: the caller passes the time and
    /// whether the pointer is still held, so the state machine is decidable
    /// without a game loop.
    /// </para>
    /// </summary>
    internal sealed class DeferredReflowGate
    {
        private readonly double _settleMs;
        private bool _pending;
        private int _pendingWidth;
        private DateTime _pendingSinceUtc;

        public DeferredReflowGate(double settleMs)
        {
            _settleMs = settleMs > 0 ? settleMs : 0;
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
        /// </summary>
        public bool TryTake(DateTime nowUtc, bool pointerHeld, out int width)
        {
            width = AppliedWidth;
            if (!_pending)
            {
                return false;
            }

            if (pointerHeld && (nowUtc - _pendingSinceUtc).TotalMilliseconds < _settleMs)
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
