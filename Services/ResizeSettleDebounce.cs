using System;
using System.Threading;
using System.Threading.Tasks;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Arms ONE trailing callback per resize drag, however many resize
    /// events that drag produces: every event only stamps the clock, and the
    /// single in-flight waiter re-arms itself against that stamp until the
    /// drag has been quiet for <see cref="SettleMs"/>. What views put behind
    /// it is the work that MEASURES text; positions and widths are cheap
    /// arithmetic and stay on the live path. Deliberately not a
    /// cancel-and-replace timer, which costs a CancellationTokenSource and a
    /// thrown cancellation per drag FRAME on the UI thread's own event path.
    /// Blish-free by construction: the caller supplies the marshal that puts
    /// the callback back on the UI thread, since the continuation after the
    /// delay may resume on a ThreadPool thread.
    /// </summary>
    internal sealed class ResizeSettleDebounce
    {
        /// <summary>The module's one settle window.</summary>
        public const int DefaultSettleMs = 150;

        private readonly Action _onSettled;
        private readonly Func<Action, bool> _marshal;
        private readonly Action<Exception> _onError;
        private readonly int _settleMs;
        private readonly Func<DateTime> _utcNow;
        private readonly Func<int, Task> _delay;

        private long _lastEventTicks;

        // Volatile: the waiter writes it too, on the paths where the
        // callback will never run, and a stale read there starves every
        // later drag.
        private volatile bool _pending;
        private volatile bool _cancelled;

        /// <param name="utcNow">Clock the settle window is measured against.
        /// Null means DateTime.UtcNow, which is what every view passes.</param>
        /// <param name="delay">How the waiter sleeps between re-checks. Null
        /// means Task.Delay. Both seams exist so the debounce's own tests can
        /// drive the window exactly instead of racing a real one - the
        /// pattern TradingPostService already uses for its utcNow.</param>
        public ResizeSettleDebounce(
            Action onSettled,
            Func<Action, bool> marshal,
            int settleMs,
            Action<Exception> onError,
            Func<DateTime> utcNow = null,
            Func<int, Task> delay = null)
        {
            if (onSettled == null)
            {
                throw new ArgumentNullException(nameof(onSettled));
            }

            if (marshal == null)
            {
                throw new ArgumentNullException(nameof(marshal));
            }

            _onSettled = onSettled;
            _marshal = marshal;
            _settleMs = settleMs > 0 ? settleMs : DefaultSettleMs;
            _onError = onError;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _delay = delay ?? (ms => Task.Delay(ms));
        }

        public int SettleMs => _settleMs;

        /// <summary>True while a trailing callback is armed.</summary>
        public bool Pending => _pending;

        public void Schedule()
        {
            if (_cancelled)
            {
                return;
            }

            Interlocked.Exchange(ref _lastEventTicks, _utcNow().Ticks);
            if (_pending)
            {
                return;
            }

            _pending = true;
            RunAfterSettleAsync();
        }

        /// <summary>Drops the armed callback and refuses further arming,
        /// for a view tearing its control tree down.</summary>
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
                        (_utcNow().Ticks - Interlocked.Read(ref _lastEventTicks))
                        / TimeSpan.TicksPerMillisecond;
                    if (elapsedMs >= _settleMs)
                    {
                        break;
                    }

                    // Clamped: a stamp landing between the two reads above
                    // can make this negative, which Task.Delay rejects.
                    int remaining = (int)(_settleMs - elapsedMs);

                    // ConfigureAwait(false) because the callback reaches the
                    // UI thread through _marshal, never through a captured
                    // context. Capturing one puts the settle window at the
                    // mercy of whatever else that context is serving: under
                    // a bounded SynchronizationContext a 1ms window has been
                    // observed taking 32 SECONDS to fire.
                    await _delay(remaining > 0 ? remaining : 1).ConfigureAwait(false);
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
            if (_cancelled)
            {
                return;
            }

            _onSettled();
        }
    }
}
