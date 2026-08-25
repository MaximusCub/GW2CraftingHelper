## W3B: Generation progress + rich logging (2026-08-08)

User-directed, field-test feedback: Generate Plan gave zero feedback while
running (a static "Generating..." for the whole ~19s a real plan can take)
and the log said nothing more useful than "Generation started (1 item)" /
"Generation finished in 19036ms". Implemented in the isolated `wt-w3b`
worktree off `master` (`ae68030`) on branch `w3b-generation-progress`.

**1. Live coarse-phase events (`Services/PlanPhaseEvent.cs`).** A new,
Blish-free `PlanPhase` enum (`BuildingTree`/`FetchingPrices`/
`SolvingDecisions`/`FetchingItemDetails`/`BuildingDisplay`) and
`PlanPhaseEvent` payload (phase, display name, optional `Done`/`Total`
counts, reserved `Detail` string). `CraftingPlanPipeline.GenerateStructuredAsync`
(both the single-item and `IReadOnlyList<PlanRequestItem>` overloads) and
the private `GenerateStructuredMultiAsync` gained a new, optional
`IProgress<PlanPhaseEvent> phaseProgress = null` parameter, reported once
per phase at the moment it STARTS - a new private `PhaseTracker` nested
class fires the live event, times the phase, and (see item 3) writes its
Debug completion log line, all from the same 5 call sites each method
already had a matching `PlanStatus` progress report at. The pre-existing,
finer-grained `IProgress<PlanStatus>` channel is completely unchanged -
this is a second, coarser, structured channel alongside it, not a
replacement at the pipeline level. Fully backward compatible: optional
parameter, defaults to `null`, every existing caller (`Module.cs`, every
pipeline test) needed no changes.

**2. Live status-strip spinner (`Views/CraftingPlanView.TriggerGenerate`).**
The status label now shows a rotating ASCII spinner (`| / - \`) prefixed
onto the current phase's text (e.g. "/ Fetching prices (418 items)..."),
replacing the old static "Generating...". A new `_spinnerTicker`
(`FrameTicker`, same mechanism as the pre-existing scroll-verify/resize-
debounce tickers) advances the spinner glyph roughly every 150ms;
`phaseProgress`'s callback updates the phase text as each new event
arrives. Both the ticker's own step and the phase-event callback funnel
through one `RenderSpinnerStatus` local function, which rechecks
`StatusUpdateGuard.ShouldApply` (the exact M34-B1 #4 guard the pre-
existing `PlanStatus` wiring already used) before touching the label - a
stale tick from a superseded or already-finished generation can never
clobber a newer one's text or the final "Plan generated -"/"Error:" text,
regardless of how `QueueMainThreadUpdate`/`FrameTicker.DoUpdate` happen to
interleave on any given frame. The old `IProgress<PlanStatus>` wiring to
the status label is removed (the view now passes `progress: null` to the
pipeline) - its frequent, static-feeling per-step text is exactly what the
spinner + coarse phase text replaces; the pipeline itself still accepts
and reports it (item 1) for any other future caller. The ticker is
cancelled in `TriggerGenerate`'s own `finally` block (alongside the
existing button re-enable) and in `StopLiveTickers` (tab switch / module
unload), matching the other three tickers' teardown discipline exactly.

**3. Rich `ModuleLog` logging, category "plan".** `CraftingPlanPipeline`
gained an optional constructor-injected `ModuleLog moduleLog = null`
(defaults to `ModuleLog.Shared` - `Module.cs`'s construction site never
passes it), replacing every direct `ModuleLog.Shared.Write` call in the
class with `_moduleLog.Write`, so tests can inject an isolated instance
(see item 4). The `IReadOnlyList<PlanRequestItem>` wrapper also gained an
optional `string requestLabel = null` - a best-effort "name x quantity[,
name x quantity...]" label (e.g. "Orrax Manifested x1") that
`CraftingPlanView` builds from its own already-resolved item-row search
selection (no extra network round trip; falls back to the pre-W3B
"(N items)" wording when absent, e.g. every pipeline test). Logging shape:
Info on start ("Generating plan for Orrax Manifested x1"); Debug, one
bounded entry per phase as it completes ("Fetching prices: 8400ms (418
items)", written by `PhaseTracker`, never touching the OLD per-item-count
detail); Info on finish, one compact per-phase summary line via a new
`Services/Diagnostics/PlanPhaseTimingSummary` ("Plan for Orrax Manifested
x1: tree 120ms, prices 8400ms (418 items), solve 30ms, item details
9200ms, display 250ms - total 19036ms") - computed by bucketing the SAME
raw timing lines `FinishTimingLog` already prepends to
`CraftingPlanResult.DebugLog` into the 5 coarse phases (no separate
timing plumbing needed between the single/multi methods and the wrapper);
cancelled/failed lines keep their pre-existing wording, just with the
label appended. `PlanTimingAnalyzer` gained a public `SummaryHeaderLine`
constant (was an inline literal) so `PlanPhaseTimingSummary` can locate
exactly where the raw per-step timing lines end within a full `DebugLog`
and never mis-bucket a later, unrelated line (verified by a dedicated
regression test - see item 4).

**4. Tests.** `PlanPhaseTimingSummaryTests` (8 tests, pure-function
coverage: null/empty input, the exact single- and multi-item bucketing
shape, the summary-header-marker stop behavior against a full realistic
`DebugLog` including `PlanResultBuilder`'s own trailing reduction/
decision lines, forward-compatible handling of an unrecognized future
step name, graceful degradation when a bucket is absent). Five new tests
added to `CraftingPlanPipelineTests`: phase events fire in the expected
order with sane payloads (only `FetchingPrices`/`FetchingItemDetails`
carry a `Total`, `Done` always null) on a real single-item pipeline run
and again on a real multi-item run; a null `phaseProgress` produces a
byte-identical plan/economics result to omitting the parameter entirely;
and two tests against a real, isolated `ModuleLog` instance (`new
ModuleLog()`, never `ModuleLog.Shared`) configured with a real
`ModuleLogStore` pointed at a `TempDirectory` - one proving the full
`requestLabel` path (Info start/finish wording, exactly 5 Debug per-phase
entries, every entry tagged "plan") after `WaitForPendingFileWrites`
confirms they reached the on-disk JSONL file (not just the in-memory
ring), the other proving the no-`requestLabel` fallback wording
("Generating plan for 1 item").

**5. Review-fix pass (this round) - 4 Must Fix findings from adversarial
review, all fixed.**

- *Tab-switch strip freeze.* `CraftingPlanView.Build()` calls
  `StopLiveTickers` (cancels `_spinnerTicker`) and then constructs a
  brand-new `_statusLabel` ("Ready") on every tab rebuild, but nothing
  ever re-armed the ticker or re-rendered the current phase text for a
  generation still genuinely in flight - the strip stuck on "Ready"
  (silently, no spinner) until the generation's NEXT phase event, which
  for the longest phase ("Fetching item details") can be most of the
  run. `RenderSpinnerStatus`/`SpinnerTick` are now instance methods
  parameterized on `myGen` (were TriggerGenerate-local closures), plus a
  new `ArmSpinnerTicker(int myGen)` and `_generationInFlight` field;
  `StopLiveTickers` no longer nulls `_currentPhaseText`; Build() re-arms
  via `ArmSpinnerTicker(_generateSequence)` immediately after
  reconstructing `_statusLabel` whenever `_generationInFlight` is true.
- *No monotonic phase ordering.* `Progress<PlanPhaseEvent>` with no
  `SynchronizationContext` posts every `Report` through an independent
  `ThreadPool.QueueUserWorkItem`, so two phase events reported
  milliseconds apart (warm cache, small plan) could be marshaled to the
  main thread out of order - `StatusUpdateGuard` alone cannot catch this
  since both events share the same generation. New pure
  `Services/PhaseOrdinalGuard.cs` (mirrors `StatusUpdateGuard`'s shape) +
  a `_currentPhaseOrdinal` field (reset to -1 per generation, alongside
  `_currentPhaseText`) drop any event whose `(int)pe.Phase` is not
  strictly greater than the last one actually applied.
- *Finish summary lost the real wall-clock duration.* The phase-summed
  "total" the compact summary line logged silently excluded every
  un-instrumented gap between raw timing steps, so a real ~19s
  generation could log "total 18158ms" with `sw.ElapsedMilliseconds`
  (the number a field tester actually experiences) discarded entirely.
  `PlanPhaseTimingSummary.FormatCompactSummary` gained an optional
  `long? wallClockMs = null` parameter (default preserves the exact
  pre-existing wording for every current caller/test); the pipeline's
  `IReadOnlyList<PlanRequestItem>` wrapper now passes its own wrapper
  `sw.ElapsedMilliseconds`, producing e.g. "... - total 19036ms (phases
  18158ms)".
- *`progress: null` silently dropped two real diagnostics.* Passing
  `null` for the old `IProgress<PlanStatus>` channel (replaced for the
  live strip by the coarse phase events) also silently dropped
  `RecipeService.OnStatusUpdate`'s first-run recipe-discovery notice and
  stale-recipe-seed warning, plus the tree-building phase's own "(may
  take several seconds on first run)" hint - none of which have any
  other surface. `CraftingPlanPipeline`'s `OnStatusUpdate` closures
  (both the single-item and multi-item Step 1) now also write straight
  to `ModuleLog` (Info, "plan") regardless of whether a live
  `IProgress<PlanStatus>` consumer is attached (bounded to at most one
  line each per generation by RecipeService's own
  `statusReported`/`staleReported` flags); the first-run hint now rides
  a new optional `detail` parameter on `PhaseTracker.Start` into
  `PlanPhaseEvent.Detail`, surfaced live via
  `CraftingPlanView.FormatPhaseText`.

New tests: `PhaseOrdinalGuardTests` (4, pure-function coverage mirroring
`StatusUpdateGuardTests`); `PlanPhaseTimingSummaryTests` gained 2
(`wallClockMs` present/absent); `CraftingPlanPipelineTests` gained 3
(finish summary shows a wall-clock total distinct from the phase sum;
the recipe-discovery diagnostic reaches a real isolated `ModuleLog` even
with `progress: null`; the `BuildingTree` phase event carries the
first-run hint as `Detail`, no other phase does). The tab-switch
re-arm/ordinal-guard call-site wiring inside `CraftingPlanView` itself
has no new tests, same Blish-free-tests-invariant rationale as item 4
above - covered by the live desktop gate below.

Validation: `dotnet build -p:Platform=x64` clean (0 errors); module test
suite green - 1191 passed (was 1182 after the original milestone; +9
new tests from this review-fix pass: 4 in `PhaseOrdinalGuardTests`, 2
added to `PlanPhaseTimingSummaryTests`, 3 added to
`CraftingPlanPipelineTests`). No new Blish HUD references in tests;
every new test exercises real production code (`CraftingPlanPipeline`,
`PlanPhaseTimingSummary`, `PhaseOrdinalGuard`, a real
`ModuleLog`/`ModuleLogStore`) with no contract-mirror/fake-logic tests.

Live desktop gate round 1 (2026-08-08, orchestrator session) - core
behaviors PASSED:

- Live phase text with trailing spinner observed in the plan strip
  across three generations ("Building recipe tree (may take several
  seconds on first run)..", "Fetching prices (85 items).."), leading
  text anchored with no proportional-font jitter, spinner glyph
  advancing between captures.
- Plan replacement via re-Generate works; on the no-tab-switch path the
  strip correctly ends at "Plan generated - <time>".
- Rich logging verified both in data/module_log.jsonl and rendered in
  the Log tab: "Generating plan for <name> x<qty>" start lines,
  per-phase finish summaries with counts and wall-clock vs phase-sum
  totals, and the RecipeService seed notices. NOTE for gate tooling:
  the old "Generation finished in Xms" wording is replaced - waits must
  now grep for "Plan for <name>".
- No exceptions in the Blish log; the Log tab stayed stable throughout.

FAILED scenario - tab switch mid-generation (Must Fix, fix in flight):
switching Plan -> Snapshot -> Plan while a generation is in flight
leaves the strip on "Ready" - the live phase text never re-arms on the
rebuilt view AND the completion status ("Plan generated - <time>") is
lost (stuck on "Ready" until the next Generate), even though the
finished plan content itself renders below. Root cause: this module
REBUILDS tab views as new instances per tab switch (the same lesson
that produced W3A's module-level Clear-view floor), so the item-1
re-arm fix's instance fields (`_generationInFlight`,
`_currentPhaseText`, `_generateSequence`) reset with the new instance,
and the completion callback's liveness check correctly bails on the
disposed old panel - nothing carries status to the new instance.
Threading guards all held (no crash, no corruption). Fix direction:
hoist the plan strip's generation status to Module level (LogViewFloor
precedent) so a freshly built view re-arms from module state and
completion writes are view-instance-independent.

**Gate round 1 fix (2026-08-08): pull-based module-level status board.**

New `Services/PlanStripStatusBoard.cs` (Blish-free, thread-safe - one
internal lock; mirrors `SnapshotCommitGate`'s
lock-plus-pure-guard-predicate style) is now the single holder of record
for the status strip's generation sequence, in-flight flag, live phase
text, and final completion/error text. `Begin(sequence)` (main thread,
TriggerGenerate before any await) resets all of it for a new generation;
`UpdatePhase(sequence, ordinal, text)` (the phaseProgress callback, any
thread) and `Finish(sequence, finalStatusText)` (the pipeline's success/
cancel/failure continuation, any thread) both write directly with no
`MainThreadMarshal` hop, since neither touches a Blish control any more -
`StatusUpdateGuard`/`PhaseOrdinalGuard` (unchanged, public surface intact)
are re-applied internally under the board's own lock instead of by each
caller. `CraftingPlanView`'s strip became a PULL consumer: the spinner
`FrameTicker`'s per-tick step reads a fresh `Snapshot()` every frame,
renders phase text + spinner while `InFlight`, and renders the final text
and self-stops (`return false`) the moment the board reports finished -
no completion-callback write into `_statusLabel` exists any more.
`Build()` (any rebuild, tab switch or otherwise) also reads a fresh
`Snapshot()` directly: in-flight re-arms the ticker (which immediately
renders the board's current phase text, not "Ready"); finished-with-status
renders that final text directly (this also closes the pre-existing quirk
where a rebuilt view showed "Ready" despite an already-completed plan);
nothing yet leaves "Ready". The four pre-fix instance fields
(`_generationInFlight`, `_currentPhaseText`, `_currentPhaseOrdinal`,
`_statusClosedForCurrentGeneration`) are removed entirely.
`PlanStripStatusBoard` is owned by `Module` (`_planStripStatusBoard`,
`GW2CraftingHelper.Services`) and constructor-injected into
`CraftingPlanView`, the same module-level-state-outlives-a-rebuild
ownership `LogViewFloor` established for the Log tab's Clear-view
watermark - though unlike that getter/setter-delegate injection (needed
because Blish reconstructs a fresh `LogTabContent` on every tab visit),
`CraftingPlanView` is a singleton `Module.Initialize()` constructs exactly
once and only re-invokes `Build()` on each visit, so a single
constructor-injected reference is sufficient here.

Root-cause correction: round 1's own write-up above attributed the bug to
this module rebuilding tab views as brand-new instances per tab switch,
by analogy with `LogTabContent`. That analogy does not hold for
`CraftingPlanView` specifically - `Module.cs` constructs exactly one
instance in `Initialize()` and every tab visit only re-invokes its
`Build()` method, so the pre-fix instance fields did NOT reset on a tab
switch the way `LogTabContent`'s fields did. The real mechanism: `Build()`
unconditionally hardcoded `_statusLabel.Text = "Ready"` and only knew how
to re-arm a STILL-IN-FLIGHT generation (via `_generationInFlight`) - it
had no way to recover an ALREADY-FINISHED generation's completion text,
because the completion callback only ever wrote that text directly into
whichever `_statusLabel` was live at the moment it drained, gated behind a
`_contentPanel.Parent == null` liveness bail. A completion landing while
the user was on a different tab (panel detached-but-not-yet-disposed, or
already disposed by the next `ViewAdapter.Build`'s defensive child-dispose
sweep, depending on timing) either wrote into a since-discarded label or
was skipped by that bail entirely - either way, nothing persisted the fact
that the generation had finished, so the very next `Build()` had no state
to consult and fell through to the hardcoded "Ready". The pull-based board
fixes this by construction: `Finish()` is unconditional (no view-liveness
check at all) and `Build()` always asks the board fresh, so which
particular view instance or control existed at completion time no longer
matters.

New tests: `PlanStripStatusBoardTests` (11, pure-function/thread-safety
coverage - `Begin`/`UpdatePhase`/`Finish` transitions, stale-sequence
rejection on both `UpdatePhase` and `Finish`, stale-ordinal rejection,
rejection of a trailing `UpdatePhase` after `Finish` has already closed
the generation, a final-status read by an unrelated later `Snapshot()`
call standing in for a rebuilt view's `Build()`, `Begin()` clearing a
prior finished generation's leftover state, and a parallel-writers
smoke test proving no exception/torn state under concurrent
`UpdatePhase`/`Snapshot` calls). The `CraftingPlanView`/`Module.cs` wiring
itself has no new tests, same Blish-free-tests-invariant rationale as
every other pass in this file - covered by the live desktop gate below.

Validation: `dotnet build -p:Platform=x64` clean (0 errors, no new
warnings). Module test suite - 1210 passed (was 1199; +11 new
`PlanStripStatusBoardTests`).

**Gate round 1 fix, review pass (2026-08-08) - 1 Critical + 3 Must Fix from
adversarial review, all fixed.**

- *Critical: the ordinary (no-tab-switch) completion path never showed the
  final status.* `TriggerGenerate`'s `finally` block canceled
  `_spinnerTicker` (`_spinnerTicker?.Cancel(); _spinnerTicker = null;`) in
  the SAME `MainThreadMarshal.Run` drain as the success/catch callback's
  `_statusBoard.Finish(myGen, ...)` call - both callbacks are queued
  back-to-back with no `await` between them, and
  `GameService.Overlay.QueueMainThreadUpdate` drains its whole queue in one
  pass (docs/ARCHITECTURE.md section 1), so no real engine frame
  (`Control.DoUpdate`) can land between them. `Finish()` is a pure state
  write with no render side effect by design (the pull model), so
  `RenderFromBoard`/`SpinnerTick` were the ONLY remaining renderers of the
  final text - and `Cancel()` synchronously `Dispose()`s the ticker
  (`Parent = null`, removed from `SpriteScreen`'s children) before
  `SpinnerTick` ever gets a `DoUpdate` to observe the just-written `Finish()`
  state. Net effect: the strip froze on the last phase text + spinner glyph
  forever on the primary, most common completion path, never showing "Plan
  generated - `<time>`" / "Error: ..." until the next Generate or a tab
  flip - a regression against "preserve... the no-tab-switch path's
  behavior" introduced by this same milestone's own fix. Fixed by calling
  `RenderFromBoard(_statusBoard.Snapshot())` in the `finally` callback
  immediately before `_spinnerTicker?.Cancel()`, flushing the final text
  deterministically through the board before the ticker that would
  otherwise have to render it is torn down.
- *`PlanStripStatusBoard.Finish()` bypassed `StatusUpdateGuard`.* Checked
  only `sequence != _sequence`, so it accepted a write onto a board that is
  not in flight - including a virgin, never-`Begin()`'d board (`_sequence
  == 0`, unreachable today only because the caller's `myGen` is always
  `++_generateSequence` and therefore never 0 - not an invariant this class
  should rely on its caller to hold) and a second `Finish()` for an
  already-finished generation (would silently overwrite the first-recorded
  wording). `Finish` now calls
  `StatusUpdateGuard.ShouldApply(sequence, _sequence, !_inFlight)`, the same
  guard `UpdatePhase` already used, making both methods consistent.
- *`Build()`'s finished branch duplicated `RenderFromBoard`'s render
  decision.* Re-derived its own inline "has a final status -> `SetStatus`
  it, otherwise leave Ready" copy of `RenderFromBoard`'s own ladder, so two
  independent copies of "what the strip shows for a given snapshot" existed
  and could silently drift apart - contradicting `RenderFromBoard`'s own doc
  comment claim of being "the ONLY place" that writes a snapshot into
  `_statusLabel`. `Build()`'s not-in-flight branch now calls
  `RenderFromBoard(boardSnapshot)` directly instead.
- *The ticker's stop/render decision was untestable.* The exact contract the
  milestone calls out ("when the board reports finished, render the final
  status and stop itself") lived inline in `SpinnerTick`, a Blish-coupled
  view method no test could reach - the board was provably correct in
  isolation yet the feature broke because nothing proved the consumer side
  ever rendered a finished snapshot. New pure `Services/PlanStripTickDecision.cs`
  (`Decide(snapshot, myGen)` -> `Stop`/`RenderSpinner`/`RenderFinalAndStop`,
  mirrors `StatusUpdateGuard`/`PhaseOrdinalGuard`'s shape) now owns that
  decision; `SpinnerTick` just carries out whatever it returns.

New tests: `PlanStripTickDecisionTests` (6 - in-flight renders spinner,
Finish landing before the ticker's first tick renders final and stops,
Finish landing between two ticks flips from spinner to final-and-stop, a
superseded generation stops, a never-`Begin()`'d board stops, a null
snapshot stops); 2 added to `PlanStripStatusBoardTests` (`Finish` on a
virgin board rejected, a second `Finish` for the same generation rejected -
the two cases the guard fix above closes). `CraftingPlanView`'s
`SpinnerTick`/`Build()`/`TriggerGenerate` wiring itself has no new tests,
same Blish-free-tests-invariant rationale as every other pass in this file
- covered by the live desktop gate below.

Validation: `dotnet build -p:Platform=x64` clean (0 errors, 0 warnings from
any touched file). Module test suite - 1218 passed (was 1210; +8 new: 6
`PlanStripTickDecisionTests`, 2 added to `PlanStripStatusBoardTests`).

Live desktop gate round 2: PASS (2026-08-08, orchestrator session,
fresh sandbox on the fixed build). All three scenarios verified:

- Normal untouched completion: strip ended at "Plan generated - Aug 8,
  2026 8:50 PM" (exercises the review-caught critical - the final text
  flush before the ticker cancel on the ordinary path).
- Tab switch mid-generation (Plan -> Snapshot -> Plan during a 21s
  Orrax Manifested generation): the rebuilt view showed the LIVE phase
  text with the spinner still animating between captures, and on
  completion the same flipped view transitioned to "Plan generated -
  Aug 8, 2026 8:53 PM". This is the exact round-1 failure, now fixed.
- Tab switch after completion: the rebuilt view showed the preserved
  "Plan generated - Aug 8, 2026 8:51 PM" instead of the pre-existing
  stale "Ready" quirk.

No exceptions in the Blish log across the session. The round-1 PASS
items (live phase text, no jitter, rich file + Log-tab logging, plan
replacement) were implicitly re-exercised across four generations in
the two sessions and remained correct.
