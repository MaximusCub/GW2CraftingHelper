using System;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Arms ONE trailing callback per resize drag, however many resize
    /// events that drag produces: every event only stamps the clock, and the
    /// single in-flight waiter re-arms itself against that stamp until the
    /// drag has been quiet for <see cref="SettleMs"/>. What a view puts
    /// behind it is the work that MEASURES text - wrapping and ellipsizing
    /// are hundreds of MeasureString calls, and a drag delivers events at
    /// frame rate; positions and widths are cheap arithmetic and stay on the
    /// live path.
    ///
    /// <para>
    /// Deliberately not a cancel-and-replace timer, which costs a
    /// CancellationTokenSource and a thrown cancellation per drag FRAME on
    /// the UI thread's own event path.
    /// </para>
    /// <para>
    /// Blish-free by construction: the caller supplies the marshal that puts
    /// the callback back on the UI thread, since the continuation after the
    /// delay may resume on a ThreadPool thread. A marshal returning false
    /// means the callback was dropped and will never run, which is what
    /// releases the in-flight slot rather than starving every later drag.
    /// </para>
    /// </summary>
    public sealed class ResizeSettleDebounce
    {
        /// <summary>The module's one settle window.</summary>
        public const int DefaultSettleMs = 150;

        private readonly Action _onSettled;
        private readonly Func<Action, bool> _marshal;
        private readonly Action<Exception> _onError;
        private readonly int _settleMs;

        private long _lastEventTicks;
        private bool _pending;
        private volatile bool _cancelled;

        public ResizeSettleDebounce(
            Action onSettled,
            Func<Action, bool> marshal,
            int settleMs,
            Action<Exception> onError)
        {
            if (onSettled == null) throw new ArgumentNullException(nameof(onSettled));
            if (marshal == null) throw new ArgumentNullException(nameof(marshal));

            _onSettled = onSettled;
            _marshal = marshal;
            _settleMs = settleMs > 0 ? settleMs : DefaultSettleMs;
            _onError = onError;
        }

        public int SettleMs => _settleMs;

        /// <summary>True while a trailing callback is armed.</summary>
        public bool Pending => _pending;

        public void Schedule()
        {
            if (_cancelled) return;

            Interlocked.Exchange(ref _lastEventTicks, DateTime.UtcNow.Ticks);
            if (_pending) return;

            _pending = true;
            RunAfterSettleAsync();
        }

        /// <summary>
        /// Drops the armed callback and refuses further arming - for a view
        /// tearing its control tree down, whose callback would otherwise
        /// reach disposed controls.
        /// </summary>
        public void Cancel()
        {
            _cancelled = true;
        }

        private async void RunAfterSettleAsync()
        {
            try
            {
                while (true)
                {
                    long elapsedMs =
                        (DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastEventTicks))
                        / TimeSpan.TicksPerMillisecond;
                    if (elapsedMs >= _settleMs) break;

                    // Clamped: a stamp landing between the two reads above
                    // can make this negative, which Task.Delay rejects.
                    int remaining = (int)(_settleMs - elapsedMs);
                    await Task.Delay(remaining > 0 ? remaining : 1);
                }

                if (_cancelled || !_marshal(Invoke))
                {
                    _pending = false;
                }
            }
            catch (Exception ex)
            {
                // async void: an escaping exception has no caller to reach
                // and would take down the host rather than this one wait.
                _pending = false;
                _onError?.Invoke(ex);
            }
        }

        private void Invoke()
        {
            // Cleared BEFORE the callback, so a resize landing during it
            // arms a fresh waiter instead of being swallowed.
            _pending = false;
            if (_cancelled) return;

            _onSettled();
        }
    }
}
