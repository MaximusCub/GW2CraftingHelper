namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pull-based, thread-safe, module-level state for the Crafting Plan
    /// tab's status strip (KNOWN-ISSUES #45).
    /// <para>
    /// The rule the class rests on: every write (Begin/UpdatePhase/Finish)
    /// only ever updates PURE state here and never touches a Blish control,
    /// so no write can be skipped by a view-liveness check or race a
    /// rebuild. The strip is a PULL consumer - see CraftingPlanView's
    /// SpinnerTick/RenderFromBoard and Build()'s re-arm block. Module
    /// constructs it once, so it outlives any single view build cycle.
    /// </para>
    /// <para>
    /// Every write and the read take one internal lock, so a reader always
    /// sees a mutually consistent set of fields. Writers may run on any
    /// thread; only the pull side runs on the main thread, and only it ever
    /// touches a Blish control. Stale-write rejection reuses
    /// <see cref="StatusUpdateGuard"/> and <see cref="PhaseOrdinalGuard"/>.
    /// Derivation: docs/ARCHITECTURE.md section 6.2.
    /// </para>
    /// </summary>
    internal sealed class PlanStripStatusBoard
    {
        private readonly object _lock = new object();

        private int _sequence;
        private bool _inFlight;
        private int _phaseOrdinal = -1;
        private string _phaseText;
        private string _finalStatusText;

        /// <summary>
        /// Starts tracking generation <paramref name="sequence"/>: marks it
        /// in-flight and clears every field a PREVIOUS generation (finished
        /// or not) may have left behind, so a freshly armed strip never
        /// shows a stale phase/final-status string from an earlier run.
        /// Always applies - <paramref name="sequence"/> becomes the new
        /// "current" generation unconditionally, matching
        /// CraftingPlanView's own monotonically-increasing
        /// ++_generateSequence convention (a Begin() call is, by
        /// construction, always for a strictly newer generation than
        /// whatever came before it).
        /// </summary>
        public void Begin(int sequence)
        {
            lock (_lock)
            {
                _sequence = sequence;
                _inFlight = true;
                _phaseOrdinal = -1;
                _phaseText = null;
                _finalStatusText = null;
            }
        }

        /// <summary>
        /// Records phase <paramref name="phaseOrdinal"/>'s display text for
        /// generation <paramref name="sequence"/>. Rejected (no-op) if
        /// <paramref name="sequence"/> is not the current generation, if
        /// the current generation has already finished (both via
        /// <see cref="StatusUpdateGuard"/> - the "already closed" case
        /// covers a trailing phase-event tick draining after this same
        /// generation's own <see cref="Finish"/> already ran), or if
        /// <paramref name="phaseOrdinal"/> is not strictly greater than
        /// the last one actually applied (<see cref="PhaseOrdinalGuard"/> -
        /// an out-of-order ThreadPool post from Progress&lt;T&gt;).
        /// </summary>
        public void UpdatePhase(int sequence, int phaseOrdinal, string phaseText)
        {
            lock (_lock)
            {
                if (!StatusUpdateGuard.ShouldApply(sequence, _sequence, !_inFlight))
                {
                    return;
                }

                if (!PhaseOrdinalGuard.ShouldApply(phaseOrdinal, _phaseOrdinal))
                {
                    return;
                }

                _phaseOrdinal = phaseOrdinal;
                _phaseText = phaseText;
            }
        }

        /// <summary>
        /// Records generation <paramref name="sequence"/>'s final status
        /// text (success/cancel/failure wording, e.g. "Plan generated -
        /// ..."/"Error: ...") and marks it no longer in flight. Never
        /// skipped because some view's panel happens to be disposed or
        /// detached at the moment it runs - a pull-based reader picks this
        /// text up whenever it next asks.
        /// <para>
        /// Rejected (no-op) via the same <see cref="StatusUpdateGuard"/>
        /// UpdatePhase already uses, in its two cases:
        /// <paramref name="sequence"/> is not the current generation, or
        /// the current generation's status is already closed. A raw
        /// sequence-only check is not a substitute - see
        /// docs/ARCHITECTURE.md section 6.2.
        /// </para>
        /// </summary>
        public void Finish(int sequence, string finalStatusText)
        {
            lock (_lock)
            {
                if (!StatusUpdateGuard.ShouldApply(sequence, _sequence, !_inFlight))
                {
                    return;
                }

                _inFlight = false;
                _finalStatusText = finalStatusText;
            }
        }

        /// <summary>
        /// Seeds this board with a restored plan's staleness-banner text at
        /// module load, before any real Generate has run this session - see
        /// Views/CraftingPlanView.cs's ApplyRestoredPlan. Uses sequence 0,
        /// which CraftingPlanView's ++_generateSequence convention can never
        /// produce, so a genuine Begin(1) always supersedes this seed.
        /// Deliberately bypasses <see cref="StatusUpdateGuard"/>: this is
        /// the board's own one-time initial seed, not a write racing an
        /// in-flight generation.
        /// <para>
        /// No-op if a real generation has already Begin()'n this session
        /// (_sequence != 0) or is currently in flight. That is enforced, not
        /// merely expected: a user can click Generate while Module.LoadAsync
        /// is still in flight, so this seed can run AFTER that generation's
        /// whole Begin/UpdatePhase/Finish sequence, and stomping _sequence
        /// back to 0 there would freeze the strip - the exact "lost
        /// completion status" bug this board exists to prevent.
        /// Derivation: docs/ARCHITECTURE.md section 6.2.
        /// </para>
        /// </summary>
        public void SeedRestored(string finalStatusText)
        {
            lock (_lock)
            {
                if (_sequence != 0 || _inFlight)
                {
                    return;
                }

                _sequence = 0;
                _inFlight = false;
                _phaseOrdinal = -1;
                _phaseText = null;
                _finalStatusText = finalStatusText;
            }
        }

        /// <summary>
        /// Undoes a <see cref="SeedRestored"/> call whose downstream render
        /// subsequently failed - see Views/CraftingPlanView.cs's shared
        /// rollback helper. Only clears the seeded final status text, and
        /// only while this board still reflects nothing but that one seed
        /// (the same "_sequence != 0 || _inFlight" guard SeedRestored uses,
        /// for the same reason): a real Generate that raced in between must
        /// never be clobbered by a rollback for a plan it has superseded.
        /// <para>
        /// Returns whether it actually cleared anything, so the caller knows
        /// whether it is also safe to reset the status label's already-
        /// painted text back to "Ready". RenderFromBoard is pull-based and
        /// never overwrites a label with an empty FinalStatusText, so
        /// clearing the board alone does not un-paint an already-rendered
        /// banner - but forcing that reset unconditionally would stomp a
        /// genuinely in-flight generation's live spinner text whenever this
        /// method's own guard correctly no-ops.
        /// </para>
        /// </summary>
        public bool ClearRestoredSeed()
        {
            lock (_lock)
            {
                if (_sequence != 0 || _inFlight)
                {
                    return false;
                }

                _finalStatusText = null;
                return true;
            }
        }

        /// <summary>
        /// A consistent, immutable snapshot of every field at one instant.
        /// The only way to read this board - never expose the individual
        /// fields separately, or a reader could observe a torn combination
        /// (e.g. a Finish() landing between two separate field reads).
        /// </summary>
        public PlanStripStatusSnapshot Snapshot()
        {
            lock (_lock)
            {
                return new PlanStripStatusSnapshot(_sequence, _inFlight, _phaseText, _finalStatusText);
            }
        }
    }

    /// <summary>
    /// One consistent read of <see cref="PlanStripStatusBoard"/>'s state -
    /// see <see cref="PlanStripStatusBoard.Snapshot"/>.
    /// </summary>
    internal sealed class PlanStripStatusSnapshot
    {
        /// <summary>The generation this snapshot describes.</summary>
        public int Sequence { get; }

        /// <summary>True from Begin() until that same generation's own Finish() call.</summary>
        public bool InFlight { get; }

        /// <summary>
        /// The latest applied phase display text (e.g. "Fetching prices
        /// (418 items)..."), or null if no phase event has landed yet for
        /// this generation. Meaningless once <see cref="InFlight"/> is
        /// false - a finished generation's caller should read
        /// <see cref="FinalStatusText"/> instead.
        /// </summary>
        public string PhaseText { get; }

        /// <summary>
        /// The finished generation's final status text, or null if this
        /// generation (or no generation at all) has not finished yet.
        /// </summary>
        public string FinalStatusText { get; }

        public PlanStripStatusSnapshot(int sequence, bool inFlight, string phaseText, string finalStatusText)
        {
            Sequence = sequence;
            InFlight = inFlight;
            PhaseText = phaseText;
            FinalStatusText = finalStatusText;
        }
    }
}
