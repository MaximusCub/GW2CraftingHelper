using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Makes the account-snapshot epoch re-check and the field commit that
    /// follows atomic with respect to Module.ClearCache: the epoch bump
    /// (<see cref="Clear"/>) and the epoch re-check (<see cref="TryCommit"/>)
    /// share one lock, so a caller of one always either fully precedes or
    /// fully follows a caller of the other - no interleaving, and no torn
    /// writes across _currentSnapshot/_pendingSnapshot/_snapshotDirty.
    ///
    /// Both methods run their callback synchronously while holding the lock;
    /// callers must NEVER await inside the callback. Neither Module.cs call
    /// site has anything to await - the network fetch has already completed by
    /// the time TryCommit is called, and ClearCache's own work is all
    /// synchronous field and file writes.
    ///
    /// What the narrower SnapshotEpochGuard left open: docs/ARCHITECTURE.md,
    /// "Services Q-Z: relocated design narrative"; KNOWN-ISSUES #31/31a-F1.
    /// </summary>
    internal sealed class SnapshotCommitGate
    {
        private readonly object _lock = new object();
        private int _epoch;

        /// <summary>
        /// The epoch to capture before starting a fetch, so it can later be
        /// passed to <see cref="TryCommit"/>. Locked for consistency with
        /// the rest of this class even though a lone read needs no
        /// exclusion on its own.
        /// </summary>
        public int Epoch
        {
            get
            {
                lock (_lock)
                {
                    return _epoch;
                }
            }
        }

        /// <summary>
        /// Bumps the epoch and runs <paramref name="clear"/> atomically
        /// with respect to any concurrent <see cref="TryCommit"/> call -
        /// either this fully precedes that call (which will then see the
        /// bumped epoch and discard) or fully follows it (which committed
        /// under the old epoch and is a legitimate result that Clear's own
        /// callback should now be free to wipe/delete).
        /// </summary>
        public void Clear(Action clear)
        {
            if (clear == null)
            {
                throw new ArgumentNullException(nameof(clear));
            }

            lock (_lock)
            {
                _epoch++;
                clear();
            }
        }

        /// <summary>
        /// Re-checks <paramref name="myEpoch"/> against the live epoch and,
        /// only if it still matches, runs <paramref name="commit"/> - all
        /// under the same lock <see cref="Clear"/> uses, so a Clear Cache
        /// racing this call can never land between the check and the
        /// commit. Returns false (and never invokes <paramref name="commit"/>)
        /// on a mismatch.
        /// </summary>
        public bool TryCommit(int myEpoch, Action commit)
        {
            if (commit == null)
            {
                throw new ArgumentNullException(nameof(commit));
            }

            lock (_lock)
            {
                if (!SnapshotEpochGuard.ShouldCommit(myEpoch, _epoch))
                {
                    return false;
                }

                commit();
                return true;
            }
        }
    }
}
