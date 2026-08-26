using System.Threading;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The single-fetch slot the account-snapshot refresh runs in: at most
    /// one refresh owns it at a time, and it owns that refresh's
    /// CancellationTokenSource.
    /// <para>
    /// Three threads reach Module's two refresh entry points - LoadAsync on a
    /// ThreadPool task, Update() on the main thread, and OnSubtokenUpdated on
    /// a thread the module does not control - and both entry points used to
    /// gate on a check-then-set over a <c>volatile bool</c>. Volatile makes a
    /// write VISIBLE; it does not make check-then-set ATOMIC, so two entrants
    /// could both get past it. Each would then run the same three-statement
    /// sequence (cancel the live source, dispose it, assign a fresh one) and
    /// each could dispose the source the other had just published, after
    /// which the loser's own <c>_refreshCts.Token</c> read threw
    /// ObjectDisposedException - or NullReferenceException, if a Clear Cache
    /// click nulled the field in the same window. Module's generic catch
    /// reported that as "refresh failed" and armed a 60-second retry backoff
    /// for a call that never reached the network.
    /// </para>
    /// <para>
    /// The claim is an Interlocked.CompareExchange, the same pattern
    /// ModuleLog.ScheduleFlush uses, and the source swap is a single
    /// Interlocked.Exchange so exactly one caller ever owns the outgoing
    /// source. <see cref="BeginFetch"/> hands back the token by value, before
    /// the source is published, so a concurrent <see cref="CancelCurrent"/>
    /// cancels the fetch (which is what it means) instead of racing a field
    /// read the fetch would otherwise have to make.
    /// </para>
    /// </summary>
    public sealed class SnapshotRefreshSlot
    {
        private int _claimed;
        private CancellationTokenSource _cts;

        /// <summary>
        /// Whether a refresh currently holds the slot. Advisory only - a
        /// caller that intends to refresh must go through
        /// <see cref="TryClaim"/>, which is the actual gate.
        /// </summary>
        public bool IsClaimed => Volatile.Read(ref _claimed) != 0;

        /// <summary>
        /// Claims the slot for this caller, or returns false if another
        /// caller already holds it. Exactly one of any number of concurrent
        /// callers gets true. A caller that gets true MUST call
        /// <see cref="Release"/> in a finally.
        /// </summary>
        public bool TryClaim()
        {
            return Interlocked.CompareExchange(ref _claimed, 1, 0) == 0;
        }

        public void Release()
        {
            Interlocked.Exchange(ref _claimed, 0);
        }

        /// <summary>
        /// Publishes a fresh CancellationTokenSource as the live one,
        /// cancelling and disposing whatever it replaced, and returns the new
        /// token. The token is captured before publication precisely so the
        /// caller never has to re-read the field it just wrote.
        /// </summary>
        public CancellationToken BeginFetch()
        {
            var next = new CancellationTokenSource();
            var token = next.Token;
            Swap(next);
            return token;
        }

        /// <summary>
        /// Cancels and disposes the live source, if any, and leaves the slot
        /// with none - Clear Cache and Unload. Safe to call any number of
        /// times and from any thread; the source is disposed exactly once.
        /// </summary>
        public void CancelCurrent()
        {
            Swap(null);
        }

        private void Swap(CancellationTokenSource next)
        {
            var previous = Interlocked.Exchange(ref _cts, next);
            previous?.Cancel();
            previous?.Dispose();
        }
    }
}
