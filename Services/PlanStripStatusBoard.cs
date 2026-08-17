namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pull-based, thread-safe, module-level state for the Crafting Plan
    /// tab's status strip (W3B gate round 1 fix - "tab-switch strip
    /// freeze/lost completion status", docs/KNOWN-ISSUES.md's W3B section).
    ///
    /// <para>
    /// Round-1 root cause: CraftingPlanView's status-strip fields
    /// (_generationInFlight/_currentPhaseText/_currentPhaseOrdinal) and its
    /// _statusLabel control are both rebuilt every time the Crafting Plan
    /// tab's Build() runs, but the completion callback only ever WROTE its
    /// final text into whichever _statusLabel happened to be live at the
    /// moment it drained - if the user had switched away and back while a
    /// generation was still running, or the completion landed while the
    /// user was on a different tab, that write either targeted a
    /// since-discarded label or (per the pre-fix liveness-check ordering)
    /// was skipped entirely, and nothing about the NEXT Build() cycle knew
    /// a finished generation's status text even existed to restore. This
    /// board inverts that: every write (Begin/UpdatePhase/Finish) only ever
    /// updates PURE state here, never a Blish control, so it can never be
    /// skipped by a view-liveness check and never race a rebuild. The
    /// status strip becomes a PULL consumer instead - see
    /// Views/CraftingPlanView.cs's SpinnerTick/RenderFromBoard (the live
    /// spinner ticker, which reads this board every tick while armed) and
    /// Build()'s own re-arm block (which reads a fresh Snapshot() on every
    /// rebuild, regardless of whether a generation happens to still be
    /// running, already finished, or never started).
    /// </para>
    ///
    /// <para>
    /// Ownership: constructed once by Module and passed into
    /// CraftingPlanView's constructor (LogViewFloor precedent - module-
    /// level state outlives a single view build cycle). Unlike
    /// LogViewFloor's watermark (a plain long re-injected into a brand
    /// new LogTabContent via constructor delegates on every tab visit,
    /// since Blish reconstructs LogTabContent per visit), CraftingPlanView
    /// itself is a SINGLETON - Module.Initialize() constructs exactly one
    /// instance and every later tab visit only re-invokes its Build()
    /// method - so a single constructor-injected reference is enough for
    /// this state to "survive a rebuild": there is no second instance to
    /// re-inject into. It still lives on Module (not as a CraftingPlanView
    /// field) to match the established module-level-state ownership
    /// pattern for exactly this class of bug, and to keep the door open
    /// for a future CraftingPlanView reconstruction-per-tab refactor
    /// (matching every other tab view in this module) without this state
    /// needing to move again.
    /// </para>
    ///
    /// <para>
    /// Threading: every write and the read both take one internal lock, so
    /// a reader always observes a mutually consistent combination of
    /// fields (never, for example, InFlight=true together with a
    /// FinalStatusText left over from a previous generation). Writers run
    /// on whichever thread they naturally land on - the main thread for
    /// <see cref="Begin"/> (TriggerGenerate, before any await), a
    /// ThreadPool thread for <see cref="UpdatePhase"/> (the pipeline's
    /// IProgress&lt;PlanPhaseEvent&gt; callback - Progress&lt;T&gt; with no
    /// SynchronizationContext installed posts through
    /// ThreadPool.QueueUserWorkItem, see Views/MainThreadMarshal.cs's own
    /// doc comment) and for <see cref="Finish"/> (the pipeline's
    /// success/cancel/failure continuation) - none of them need to marshal
    /// onto the main thread first, since nothing here touches a Blish HUD
    /// control. Only the PULL side (the spinner ticker's FrameTicker.DoUpdate
    /// step, and Build() itself) runs on the main thread and is the only
    /// place a Blish control is ever touched from this board's data.
    /// </para>
    ///
    /// <para>
    /// Stale-write rejection reuses the exact same pure predicates
    /// CraftingPlanView's pre-fix strip logic used directly
    /// (<see cref="StatusUpdateGuard"/> for cross-generation/already-closed
    /// staleness, <see cref="PhaseOrdinalGuard"/> for out-of-order phase
    /// events within the same generation - see each guard's own doc
    /// comment for the exact race it closes) - folded into this board's
    /// write side instead of being re-checked by every caller.
    /// </para>
    /// </summary>
    public sealed class PlanStripStatusBoard
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
                if (!StatusUpdateGuard.ShouldApply(sequence, _sequence, !_inFlight)) return;
                if (!PhaseOrdinalGuard.ShouldApply(phaseOrdinal, _phaseOrdinal)) return;

                _phaseOrdinal = phaseOrdinal;
                _phaseText = phaseText;
            }
        }

        /// <summary>
        /// Records generation <paramref name="sequence"/>'s final status
        /// text (success/cancel/failure wording, e.g. "Plan generated -
        /// ..."/"Error: ...") and marks it no longer in flight. Unlike the
        /// pre-fix direct-label-write path this replaces, this write is
        /// NEVER skipped because some view's panel happens to be disposed
        /// or detached at the moment it runs; that is precisely the bug
        /// this board exists to close (a pull-based reader picks this text
        /// up whenever it next asks, regardless of what the view was doing
        /// when Finish ran). Rejected (no-op) via the same
        /// <see cref="StatusUpdateGuard"/> UpdatePhase already uses, so
        /// this is rejected in the same two cases: <paramref name="sequence"/>
        /// is not the current generation (a superseded generation's own
        /// completion must never overwrite a newer generation's in-progress
        /// or already-finished state), or the current generation's status
        /// is already closed (a raw sequence-only check would otherwise
        /// accept a second Finish() for the same generation - silently
        /// overwriting the first-recorded wording - and would accept a
        /// Finish(0, ...) on a virgin, never-Begin()'d board, which is
        /// unreachable today only because the caller's myGen is always
        /// ++_generateSequence and therefore never 0 - not an invariant
        /// this class should rely on its caller to hold).
        /// </summary>
        public void Finish(int sequence, string finalStatusText)
        {
            lock (_lock)
            {
                if (!StatusUpdateGuard.ShouldApply(sequence, _sequence, !_inFlight)) return;

                _inFlight = false;
                _finalStatusText = finalStatusText;
            }
        }

        /// <summary>
        /// W3D (plan persistence across module restarts): seeds this board
        /// with a restored plan's staleness-banner text at module load,
        /// before any real Generate has run this session - see
        /// Views/CraftingPlanView.cs's ApplyRestoredPlan and
        /// Services/PlanStore.cs's own doc comment. Uses sequence 0, which
        /// CraftingPlanView's own ++_generateSequence convention can never
        /// produce (its first real generation is always sequence 1), so a
        /// genuine Begin(1) always supersedes this seed exactly like it
        /// would supersede any earlier generation's state - see Begin's own
        /// doc comment ("always applies... unconditionally"). Deliberately
        /// bypasses StatusUpdateGuard (unlike Begin/UpdatePhase/Finish
        /// above): this is not a write racing an in-flight generation, it
        /// is the board's own one-time initial seed - called at most once
        /// per module session, before the strip has shown anything else.
        /// <para>
        /// Review-fix: no-op if a real
        /// generation has already Begin()'n this session (_sequence != 0)
        /// or is currently in flight - "called at most once... before the
        /// strip has shown anything else" above is a caller EXPECTATION,
        /// not something this method used to enforce. Module.LoadAsync's
        /// restore drain can lag well behind the module's own Update() loop
        /// starting to tick (LoadAsync awaits a full account-snapshot
        /// network refresh AFTER arming the restore flag but BEFORE
        /// returning, and Blish HUD does not call a module's Update() until
        /// LoadAsync's Task completes) - so a user can open the window and
        /// click Generate while LoadAsync is still in flight, and have that
        /// generation's Begin(1)/UpdatePhase(1,...)/Finish(1,...) all land
        /// BEFORE this seed call finally runs. Unconditionally stomping
        /// _sequence back to 0 in that window would silently reject every
        /// subsequent UpdatePhase/Finish call for that in-flight generation
        /// (StatusUpdateGuard.ShouldApply(1, 0, ...) is false once _sequence
        /// is back to 0) and freeze its spinner on the next tick
        /// (PlanStripTickDecision.Decide sees Sequence 0 != myGen 1 and
        /// stops) - exactly the W3B "lost completion status" bug this board
        /// exists to prevent. Checking _sequence == 0 alone would already
        /// be sufficient (Begin only ever moves _sequence away from 0, and
        /// never back), but the _inFlight check is kept too as a defensive,
        /// self-documenting belt-and-braces guard rather than relying on
        /// that invariant alone.
        /// </para>
        /// </summary>
        public void SeedRestored(string finalStatusText)
        {
            lock (_lock)
            {
                if (_sequence != 0 || _inFlight) return;

                _sequence = 0;
                _inFlight = false;
                _phaseOrdinal = -1;
                _phaseText = null;
                _finalStatusText = finalStatusText;
            }
        }

        /// <summary>
        /// Round-3 review-fix (mustFix): undoes a <see cref="SeedRestored"/>
        /// call whose downstream render subsequently failed - see
        /// Views/CraftingPlanView.cs's shared rollback helper, called from
        /// both RenderPlan call sites that can reach a still-unvalidated
        /// restored plan (Build()'s own unguarded-until-now render tail,
        /// and ApplyRestoredPlan's live-tab branch). Only clears
        /// <c>_finalStatusText</c>, and only while this board still
        /// reflects nothing but that one seed - the exact same
        /// "<c>_sequence != 0 || _inFlight</c>" guard <see
        /// cref="SeedRestored"/> itself uses, for the same reason (see its
        /// own doc comment): a real Generate that raced in between the
        /// original seed and the render failure that triggered this
        /// rollback must never be clobbered by a rollback for a plan that
        /// generation has already superseded. Returns whether it actually
        /// cleared anything, so the caller knows whether it is also safe
        /// to reset the status label's already-painted text back to
        /// "Ready" - RenderFromBoard is pull-based and never overwrites a
        /// label with an empty FinalStatusText, so clearing the board
        /// alone is not enough to un-paint an already-rendered banner, but
        /// forcing that reset unconditionally would stomp a genuinely
        /// in-flight generation's live spinner text whenever this method's
        /// own guard (correctly) no-ops.
        /// </summary>
        public bool ClearRestoredSeed()
        {
            lock (_lock)
            {
                if (_sequence != 0 || _inFlight) return false;

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
    public sealed class PlanStripStatusSnapshot
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
