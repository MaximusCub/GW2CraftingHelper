using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// "A reflow is pending at width W", for a strip whose layout is a step
    /// function of its width. The plan tab's item input grid is one: it
    /// seats another column every time the window crosses a column-count
    /// boundary, so a drag that crosses several boundaries repacks the
    /// whole strip once per boundary and stretches every cell in between.
    /// The gate holds the newest width and hands it back exactly once, when
    /// the drag that produced it is over - see <see cref="TryTake"/> for
    /// what counts as over.
    /// <para>
    /// Blish-free and clock-injected: the caller passes the time and
    /// whether the pointer is still held, so the state machine is decidable
    /// without a game loop.
    /// </para>
    /// </summary>
    internal sealed class DeferredReflowGate
    {
        private readonly double _settleMs;
        private readonly double _heldStallMs;
        private bool _pending;
        private bool _pointerHeldWhilePending;
        private int _pendingWidth;
        private DateTime _pendingSinceUtc;

        /// <param name="settleMs">Quiet interval that releases a reflow no
        /// pointer was ever involved in.</param>
        /// <param name="heldStallMs">Ceiling on how long a held pointer may
        /// hold a reflow back - see <see cref="TryTake"/>.</param>
        public DeferredReflowGate(double settleMs, double heldStallMs)
        {
            _settleMs = settleMs > 0 ? settleMs : 0;
            _heldStallMs = heldStallMs > 0 ? heldStallMs : 0;
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
            _pointerHeldWhilePending = false;
        }

        /// <summary>Abandons a deferred width without applying it - the
        /// panel it was measured against is gone.</summary>
        public void CancelPending()
        {
            _pendingWidth = AppliedWidth;
            _pending = false;
            _pointerHeldWhilePending = false;
        }

        /// <summary>
        /// Records the width a resize tick reported, and returns whether a
        /// reflow is pending afterwards. A width equal to the applied one
        /// cancels the pending reflow rather than scheduling a no-op: a
        /// drag that returns to where it started has nothing to re-seat.
        /// <para>
        /// <paramref name="pointerHeld"/> is what makes this pending width a
        /// DRAG's rather than a resize the window performed on its own, and
        /// it is answered here rather than at the take because a take is
        /// attempted on frames the drag does not produce - including the one
        /// the pointer is released on.
        /// </para>
        /// </summary>
        public bool Observe(int width, DateTime nowUtc, bool pointerHeld)
        {
            if (width == AppliedWidth)
            {
                CancelPending();
                return false;
            }

            _pending = true;
            _pointerHeldWhilePending |= pointerHeld;
            _pendingWidth = width;
            _pendingSinceUtc = nowUtc;
            return true;
        }

        /// <summary>
        /// Hands back the deferred width once and only once. The gate
        /// counts it as applied from that moment, so a caller that takes a
        /// width must be able to act on it.
        /// <para>
        /// A held pointer waits for the release: a hand steady for a quiet
        /// interval is ordinary inside a drag, so releasing on that interval
        /// re-seats the strip mid-drag. The quiet interval therefore
        /// releases only a resize no pointer was involved in - the screen
        /// changing size under the window, or a size restored from settings.
        /// </para>
        /// <para>
        /// <c>heldStallMs</c> bounds that wait, because a held pointer is
        /// not always a real one: Blish stops sampling the mouse while the
        /// game is unfocused or the overlay hidden and keeps the last
        /// sample, so a button down then reads down until focus returns. It
        /// runs from the last width observed, which a live drag reaches only
        /// by holding the grip still for the whole of it.
        /// </para>
        /// </summary>
        public bool TryTake(DateTime nowUtc, bool pointerHeld, out int width)
        {
            width = AppliedWidth;
            if (!_pending)
            {
                return false;
            }

            double sincePending = (nowUtc - _pendingSinceUtc).TotalMilliseconds;
            if (pointerHeld)
            {
                _pointerHeldWhilePending = true;
                if (sincePending < _heldStallMs)
                {
                    return false;
                }
            }
            else if (!_pointerHeldWhilePending && sincePending < _settleMs)
            {
                return false;
            }

            width = _pendingWidth;
            AppliedWidth = _pendingWidth;
            _pending = false;
            _pointerHeldWhilePending = false;
            return true;
        }
    }
}
