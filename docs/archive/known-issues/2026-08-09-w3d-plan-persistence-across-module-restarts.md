## W3D: Plan persistence across module restarts (2026-08-09)

User-directed, field-test feedback: a generated plan started empty every
session - the Crafting Plan tab had no memory of the last plan across a
module close/reopen. Implemented in the isolated `wt-w3d` worktree off
`master` (`63a4824`) on branch `w3d-plan-persistence`.

**1. Investigation: serialization fidelity (the whole risk of this
package).** `Models/CraftingPlanResult.cs`/`PlanSolveContext.cs` and the
crafting-tree node types (`RecipeNode`/`RecipeOption`, `CraftingTreeNode`)
were audited for reference cycles, interface-typed members, and computed
state before any store code was written. Findings: `RecipeNode`/
`RecipeOption`/`CraftingTreeNode` form a pure tree with no parent
back-pointers anywhere (confirmed by reading every field) - no cycles, so
no `ReferenceLoopHandling`/`[JsonIgnore]`-plus-fixup-pass is needed for
this package at all. `PlanSolveContext`'s several
`IReadOnlyDictionary<TKey,TValue>`/`IReadOnlyList<T>`/`ISet<int>`-typed
members and its `CurrencyValuation`/`HomesteadEfficiencyTiers` members
(both immutable, single-constructor, no parameterless constructor,
constructor parameter names matching their read-only property names only
case-insensitively - e.g. `copperPerUnit` binds to `CopperPerUnit`) were
verified to round-trip correctly through plain `Newtonsoft.Json 13.0.1`
with zero custom converters, via a disposable scratch console project
(deleted before implementation) proving the exact same shapes round-trip
byte-for-byte - Json.NET's built-in interface-collection support and
single-constructor parameter-matching both already handle every shape
this schema needs. The one genuinely inert member,
`RecipeNode.IsLeaf` (`Recipes.Count == 0`, get-only), was already silently
skipped on deserialize (no setter) but was still written into every
serialized payload; `[JsonIgnore]` added to keep the on-disk schema to
genuine state only (Models/RecipeNode.cs) - a schema-cleanliness fix, not
a correctness one. This was proven, not assumed: see item 4's real
pipeline-backed round-trip tests, which exercise every one of these
shapes through a real `CraftingPlanPipeline` result rather than a
hand-built object graph.

**2. `PlanStore` + `PersistedPlan` (`Services/PlanStore.cs`,
`Services/PlanStoreHelpers.cs`, `Models/PersistedPlan.cs`).** Mirrors
`SnapshotStore`'s shape exactly (single JSON file in the module's `data/`
directory, atomic `.tmp`+`Replace`/`Move` write, `onError` callback wired
to `ModuleLog` at Warn - the same `onStoreError` closure `Module.cs`
already builds for every other store) with one deliberate divergence: a
corrupt or too-degraded-to-render file is NOT silently swallowed to null
the way `SnapshotHelpers.DeserializeSnapshot` is - `PlanStoreHelpers.
DeserializePersistedPlan` lets a JSON parse failure, or a structurally
valid document missing `Result`/`Result.Plan`, propagate as a thrown
exception, which `PlanStore.LoadLatest`'s own try/catch turns into the
required Warn log line before returning null (spec item 4: "corrupt/
unreadable/old-schema file = fresh start with one Warn log line" -
distinct from `SnapshotStore`'s own silent-null precedent for a corrupt
`snapshot.json`, which this package does not touch). A missing file stays
silent (ordinary first-run case, not a failure). `PersistedPlan` holds the
generated-at timestamp, the original request (item ids + quantities +
"Use Own Materials" + price basis), and the full `CraftingPlanResult`
(whose own `SolveContext` member already carries everything a local
`ResolveWithOverrides` re-solve needs - no separate top-level field
required). `PlanStore.Save` takes an internal lock (unlike every other
store in this module, which relies on a higher-level in-flight guard -
see item 3) because it has two genuinely independent callers that can
race each other.

**3. Persist wiring (`Module.cs`).** After each successful Generate,
`PersistAfterGenerateAsync` (awaited as part of the `generateAsync`
delegate `CraftingPlanView` already calls) saves the full result plus a
fresh timestamp; a cancelled/failed generation propagates its exception
unchanged and persists nothing. Writes off the UI thread with no extra
dispatch needed - once the awaited pipeline call completes, this
continuation already resumes on a ThreadPool thread (no
`SynchronizationContext` installed - docs/ARCHITECTURE.md section 1), the
same reasoning `FetchAndSaveSnapshotAsync`'s own post-await
`_snapshotStore.Save` call already relies on. After each
`ResolveWithOverrides` (so pill overrides survive a restart too),
`PersistResolvedPlanInBackground` persists the override-updated result
"in place" - same `GeneratedAt`/original request as the plan's last full
Generate (tracked in four `_lastPersistedPlan*` fields, populated by
either a real Generate or a restored plan - see item 4), only `Result`
swapped. Unlike the Generate path, `ResolveWithOverrides`' caller runs
synchronously on the main thread (a pill Click handler chain via
`TreeSectionController.ApplyOverridesAndResolve`), so this write is
dispatched via a fire-and-forget `Task.Run` rather than running inline -
"no file I/O on the UI thread" (docs/ARCHITECTURE.md section 1). No
generation-sequence guard was needed for the Generate-path write: it is
proven safe by construction, not merely assumed - `PersistAfterGenerateAsync`
is now part of the single Task `TriggerGenerate` awaits with
`_generateButton.Enabled = false` for the whole duration (button
re-enable only runs in `TriggerGenerate`'s own `finally`, after that
await completes), so a second Generate cannot start while an earlier
one's persist is still running.

**4. Restore-on-load (`Module.cs`, `Views/CraftingPlanView.cs`,
`Services/PlanStripStatusBoard.cs`).** Mirrors the existing "Applying
snapshot to view" dirty-flag drain shape exactly:
`LoadAsync` calls `_planStore.LoadLatest()` and, if non-null, sets
`_pendingPlanRestore`/`_planRestoreDirty`; `Update()` (main thread) drains
the flag - ahead of the `_refreshInProgress`/`_currentSnapshot` early
returns, so a fresh account with no snapshot yet still restores its
persisted plan - populating the same `_lastPersistedPlan*` fields item 3
reads (so a pill click right after a restore, with no Generate run yet
this session, still persists correctly) and calling the new
`CraftingPlanView.ApplyRestoredPlan(result, generatedAt)`. That method
mirrors `TriggerGenerate`'s own success-path shape: adopts the restored
result as `TreeSectionController`'s override-loop baseline
(`ResetForNewPlan`, so a restored plan's decision pills re-solve correctly
with zero network calls - the correctness bar for this package), rebuilds
the view model, and seeds the RECOMMENDED banner wiring via a new
`PlanStripStatusBoard.SeedRestored(text)` method (sequence 0, which
`CraftingPlanView`'s own `++_generateSequence` convention can never
produce, so a genuine first Generate always supersedes it) - the existing
pull-based status strip renders "Generated `<time>` - prices may have
changed - Regenerate" with zero new layout. Render itself is guarded
exactly like `TriggerGenerate`'s own liveness check: the tab has usually
not been `Build()` yet at restore time (the common case), in which case
only state is set and `Build()`'s own existing
`if (_currentPlan != null) RenderPlan(_currentPlan)` tail renders it on
first visit; if the tab is already live, it renders directly instead of
waiting for a rebuild that may never come. Search box/quantity inputs are
deliberately left at their session defaults (spec item 5) - no attempt is
made to reconstruct the typed search text.

**5. Tests (`tests/GW2CraftingHelper.Tests/Services/PlanStoreTests.cs`,
11 new, Blish-free, real paths).** Mirrors `SnapshotStoreTests`' shape (a
real `PlanStore` against a real temp directory) but builds its round-trip
fixtures from a REAL `CraftingPlanPipeline` result (the same
`InMemoryRecipeApiClient`/`InMemoryPriceApiClient`/`InMemoryItemApiClient`
fake API clients `CraftingPlanPipelineTests` already uses) rather than a
hand-built `CraftingPlanResult`, so the serialization-fidelity risk item 1
investigated is actually exercised, not just asserted. Coverage: the
reloaded result renders the identical `PlanViewModelBuilder` output as the
original (byte-for-byte JSON-serialized comparison of both view models);
`ResolveWithOverrides` on the reloaded `SolveContext` produces identical
decisions/economics/view-model output to the same override applied to the
original in-memory context (the W3D spec item 3 correctness bar); an
override-updated result persists and reloads correctly "in place"; the
original request (items/quantities/useOwn/priceBasis) and the
generated-at timestamp round-trip exactly; a missing file returns null
silently; a truncated/corrupt JSON file and a wrong-schema file (valid
JSON, no `Result`) both return null with no throw and invoke the `onError`
callback exactly once; the atomic-write `.tmp` file is never left behind;
a directory-creation I/O failure invokes `onError` instead of throwing.
No test references Blish HUD/`Gw2Sharp`; no fake file I/O (`PlanStore`
runs against a real temp directory throughout).

Validation: `dotnet build -p:Platform=x64` clean (0 errors, no new
warnings from any touched file). Module test suite green - 1246 passed
(was 1235; +11 new `PlanStoreTests`). No new Blish HUD references in
tests; every new test exercises real production code
(`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`,
`PlanStore`, `PlanViewModelBuilder`) with no contract-mirror/fake-logic
tests. Item/currency/vendor IDs remain internal-only. Not regressed:
W3B's `PlanStripStatusBoard` pull-based status strip (only additive
surface added - `SeedRestored` - every existing method/guard unchanged)
and W3C's per-character discipline display (`CharacterDisciplines` flows
through `PersistedPlan.Result`/`SolveContext` exactly like every other
cosmetic field, no special-casing needed).

**6. Review-fix pass (2026-08-09) - 3 Critical + 8 Must Fix findings from
adversarial code review, all fixed.**

- *Critical: the user's decision-pill overrides were not persisted at
  all - only the override-updated `Result`.* `PersistedPlan` had no field
  for `TreeSectionController`'s `_nodeOverrides`/`_ignoredItemIds`, and
  `ApplyRestoredPlan` called `_treeController.ResetForNewPlan(result)`,
  which clears both. A restored session's very next pill click would
  therefore re-solve with only that ONE new override applied, silently
  discarding every override set before the restart - the exact
  correctness bar spec item 3 names. Fixed: `PersistedPlan` gained
  `NodeOverrides`/`IgnoredItemIds`; `Module.cs`'s `resolveOverridesSync`
  lambda now passes the SAME `overrides`/`ignoredItemIds`
  `TreeSectionController.ApplyOverridesAndResolve` calls it with straight
  into `PersistResolvedPlanInBackground` (copied into independent
  collections synchronously, before any backgrounding - see that
  method's own doc comment for why); `TreeSectionController` gained
  `RestoreOverrides`, called from `ApplyRestoredPlan` right after
  `ResetForNewPlan`. A new `PlanStoreTests` case
  (`Save_Load_NodeOverridesAndIgnoredItemIds_RoundTripAndDriveIdenticalReResolve`)
  proves a FURTHER re-solve against the reloaded overrides matches the
  original.
- *Critical: `PlanStripStatusBoard.SeedRestored` unconditionally stomped
  `_sequence`/`_inFlight`, bypassing `StatusUpdateGuard`.* `Module.LoadAsync`
  arms the restore flag BEFORE awaiting its own network refresh, but
  Blish HUD does not call a module's `Update()` until `LoadAsync`'s Task
  fully completes - so a user can open the window and have an entire
  Generate complete before the restore drain ever runs. Seeding in that
  window would silently reject every subsequent
  `UpdatePhase`/`Finish` call for the in-flight/just-finished generation
  and freeze its spinner - the exact W3B "lost completion status" bug
  this board exists to prevent. Fixed: `SeedRestored` is now a no-op
  unless `_sequence == 0 && !_inFlight` (the board's pristine initial
  state). 4 new `PlanStripStatusBoardTests` cover the seed itself, a real
  `Begin` superseding it, and both rejection cases (in-flight, already
  finished).
- *Critical: the restore drain had no "a real Generate already ran this
  session" guard, and a narrower residual race in the first fix of this
  same finding.* `Module.cs`'s restore drain unconditionally overwrote
  the persisted-metadata fields and called `ApplyRestoredPlan`, whose own
  doc comment asserted "always before the user can possibly have clicked
  Generate" - false whenever `LoadAsync` is slow, per the `SeedRestored`
  finding above. Fixed with a `_generateCompletedThisSession` flag,
  checked by the drain before applying a restore. The first pass of this
  fix used a bare `volatile bool`, which closed the multi-second network-
  refresh window but left a narrow (few-CPU-instruction) TOCTOU race
  between Update()'s flag check and `PersistAfterGenerateAsync`'s flag
  set + metadata publish, on two different threads. Closed by moving the
  compound "check flag, publish restore metadata" (drain side) and "set
  flag, publish generate metadata" (generate side) sequences under one
  new `_generateCompletionLock` - scoped to only the cheap field
  read/write pair on each side, never held across `PlanStore.Save`'s disk
  I/O or `ApplyRestoredPlan`'s Blish rendering work, so it cannot stall
  the UI thread or delay `TriggerGenerate`'s own await chain.
- *Must Fix: `ApplyRestoredPlan` had no try/catch and ran straight out of
  `Module.Update()`.* `PlanStoreHelpers`' tolerance gate only checks
  `Result?.Plan`/`SchemaVersion` structurally, so a structurally valid
  but still-degraded `plan.json` (e.g. a null `Steps`/`UsedMaterials`
  entry from a future schema change) could throw inside
  `PlanViewModelBuilder.Build`/`RenderPlan`, taking the whole module's
  update loop down with it - snapshot drain, log poll, staleness refresh,
  all of it. Fixed: wrapped in two narrow try/catches (vm build; render),
  each logging one Warn line via `ModuleLog` instead of throwing. The vm
  build now happens BEFORE any state field is mutated (matching
  `TriggerGenerate`'s own established ordering), so a build failure
  leaves `_currentPlan` untouched - a clean "fresh start" (spec item 4),
  not a half-applied one.
- *Must Fix: `PersistAfterGenerateAsync` had no stale-generation guard.*
  Justified by "a second Generate cannot start while an earlier one's
  persist is still running" - false once `OnOwnMaterialsToggled`'s
  modal-confirm path is considered: it fires a second `TriggerGenerate`
  gated only on `_currentPlan != null`, which W3D now makes true from
  module load onward (a restored plan), not on the Generate button's own
  disabled state. Fixed with a new `_persistGenerateSequence` counter,
  mirroring `CraftingPlanView`'s own `++_generateSequence` convention but
  scoped to Module's own disk-write decision - stamped synchronously,
  in lockstep with the view's own counter, immediately before each
  `generateTask` is created; `PersistAfterGenerateAsync` skips its disk
  write entirely if a newer call has since started.
- *Must Fix: every override re-solve re-serialized the FULL
  `PersistedPlan` graph with `Formatting.Indented`, with no coalescing.*
  Measured on a synthetic 364-node/400-priced-item tree: 527 KB indented
  vs. 216 KB compact. Rapid pill clicking (or a Best Path/Craft All/Buy
  All preset) queued one such multi-hundred-KB serialize+write per click,
  all serialized behind `PlanStore`'s own internal lock. Fixed:
  `PlanStoreHelpers.SerializePersistedPlan` switched to
  `Formatting.None`; `Module.PersistResolvedPlanInBackground` gained a
  latest-write-wins coalescing worker (`_pendingPlanSaveLock`/
  `DrainPendingPlanSaves`) - a superseded pending write is dropped before
  it ever reaches `PlanStore.Save`, self-healing under the same
  "whichever write lands last wins" contract `PlanStore.Save`'s own lock
  already establishes.
- *Must Fix: the round-trip tests never actually exercised the
  serialization-fidelity risk item 1 investigated.* Every existing
  `PlanStoreTests` fixture built its pipeline with 4 args (no vendor
  store, no account recipe client, no snapshot, no non-default
  `CurrencyValuation`/`HomesteadEfficiencyTiers`), so `LearnedRecipeIds`,
  `ForceBuyOnlyNodeIds`, `VendorOffers`, `CurrencyValuation`,
  `HomesteadEfficiencyTiers`, `OwnedCurrencyAmounts`,
  `CharacterDisciplines`, and `RequestedItems`/`MultiItemRoots` were
  always null/empty in every round trip. 3 new tests close this: a
  full-featured single-item fixture exercising every one of those shapes
  at once with real content (`Save_Load_FullFeaturedFixture_...`), a
  force-buy-pre-pass fixture proving `ForceBuyOnlyNodeIds` (an `ISet<int>`)
  round-trips and a manual override still beats it after reload
  (`Save_Load_ForceBuyOnlyNodeIds_...`), and a genuine multi-item batch
  proving `ResolveWithOverrides`' OTHER branch
  (`ApplyBatchSellSideEconomics`, gated on
  `Tree.Id == MultiItemWrapperItemId`) also round-trips correctly
  (`Save_Load_MultiItemBatch_...`).
- *Must Fix: no schema-version field - the only "old-schema" detection
  was the structural `Result?.Plan != null` check.* Any future
  rename/removal inside `CraftingPlanResult`/`PlanSolveContext` would
  produce a file that still passes that check and restores with the
  changed members silently defaulted to null - a partial render, which
  spec item 4 forbids. Fixed: `PersistedPlan` gained
  `SchemaVersion`/`CurrentSchemaVersion` (currently 1), checked
  alongside the structural gate in
  `PlanStoreHelpers.DeserializePersistedPlan`. `PriceBasis`/
  `AcquisitionSource` also gained `[JsonConverter(typeof(StringEnumConverter))]`
  (matching `ModuleLogEntry`'s own precedent for `ModuleLogLevel`), so a
  future member reorder can no longer silently remap an already-persisted
  plan's price basis or a decision's source.
- *Must Fix: an unguarded cross-thread race on the four persisted-metadata
  fields.* `PersistAfterGenerateAsync` wrote `GeneratedAt`/`RequestItems`/
  `UseOwnMaterials`/`PriceBasis` one-at-a-time with no lock from a
  ThreadPool continuation, while `PersistResolvedPlanInBackground` read
  all four synchronously on the main thread from a pill click - a pill
  click's read interleaving between two of the sequential writes could
  persist a `PersistedPlan` whose `GeneratedAt` no longer matched its
  `RequestItems`/`UseOwnMaterials`/`PriceBasis`. Fixed by bundling all
  four into one immutable `PersistedPlanMetadata` object published
  through a single `volatile` field - object construction always fully
  completes before the reference is published, so a reader observing a
  given instance sees all four values as they were at that SAME publish.
- *Must Fix: `ApplyRestoredPlan` never pushed the seeded staleness banner
  into an already-live tab.* Its own doc comment claimed the live-tab
  branch "renders into it directly" - true for `RenderPlan(vm)`, but it
  never called `RenderFromBoard`, the file's own documented "ONLY place
  that writes a snapshot into `_statusLabel`". In the (reachable, if
  narrow) window where the Crafting Plan tab is already built by the time
  the restore drain runs, the plan content rendered but the required
  banner text stayed invisible until the user switched tabs away and
  back. Fixed with a one-line `RenderFromBoard(_statusBoard.Snapshot())`
  call alongside the seed.
- *Must Fix: `PlanStripStatusBoardTests` had zero coverage for the new
  `SeedRestored` method.* Folded into the `SeedRestored` critical fix
  above (4 new tests) rather than tracked separately.

New tests: `PlanStoreTests` gained 7 (overrides/ignored-item-ids round
trip + fresh-generate-is-empty, schema-version mismatch + default-matches-
current, force-buy-pre-pass round trip, the full-featured fixture, the
multi-item batch). `PlanStripStatusBoardTests` gained 4 (`SeedRestored`
itself, `Begin` superseding it, both rejection cases). All Blish-free,
built against real `CraftingPlanPipeline`/`PlanStore`/
`PlanStripStatusBoard` production code paths - no contract-mirror/
fake-logic tests, no fake file I/O (`PlanStoreTests` runs against a real
temp directory throughout, matching the `SnapshotStoreTests` precedent).

Validation: `dotnet build -p:Platform=x64` clean (0 errors). Module test
suite green - 1257 passed (was 1246 before this review-fix pass; +11 new
tests, all listed above). Pre-existing StyleCop analyzer warnings (SA15xx/
SA1201/etc., ~1370 across the project before this pass, none treated as
errors) were not specifically re-audited line-by-line against this
pass's ~350-line `Module.cs` growth - no attempt was made to keep that
count exactly flat, unlike item 5's original (smaller) diff. No new
Blish HUD references in tests; every new test exercises real production
code with no contract-mirror/fake-logic tests. Item/currency/vendor IDs
remain internal-only. Not regressed: W3B's `PlanStripStatusBoard`
pull-based status strip (`SeedRestored`'s own
guard is now stricter, every pre-existing Begin/UpdatePhase/Finish
behavior and test is unchanged) and W3C's per-character discipline
display (`CharacterDisciplines` still flows through
`PersistedPlan.Result`/`SolveContext` unchanged).

**7. Review-fix pass round 2 (2026-08-09) - 2 Must Fix findings from a
second adversarial code review, both fixed.**

- *Must Fix: `SchemaVersion`'s own property initializer defeated the
  mismatch gate it exists to enforce, and its doc comment's claim about
  what happens to a pre-field file was false.* `public int SchemaVersion
  { get; set; } = CurrentSchemaVersion;` runs in the default constructor,
  and Newtonsoft.Json only overwrites properties actually present in the
  source JSON - so a file whose JSON omits "SchemaVersion" entirely (the
  one real class of old file this branch's own dev-iteration history could
  produce) deserialized as `CurrentSchemaVersion` (1), not the doc
  comment's claimed 0, sailing straight through `PlanStoreHelpers.
  DeserializePersistedPlan`'s `plan.SchemaVersion != PersistedPlan.
  CurrentSchemaVersion` gate and restoring with `NodeOverrides`/
  `IgnoredItemIds` (or any future renamed/removed member) silently null -
  exactly the "partial render" spec item 4 forbids. Verified against the
  project's pinned `Newtonsoft.Json 13.0.1`: missing field deserializes as
  1 with the initializer in place, an explicit `"SchemaVersion": 0`
  deserializes as 0 - which is why the pre-existing `LoadLatest_
  SchemaVersionMismatch_ReturnsNullAndLogsWarn` test (writes an explicit
  0) never caught this. Fixed: dropped the property initializer -
  `SchemaVersion` now defaults to the CLR's 0, matching the existing
  `VendorOfferDataset`/`RecipeCacheSerializer` `SchemaVersion` fields
  elsewhere in this codebase, which follow the same no-initializer
  pattern - and both real construction sites
  (`Module.PersistAfterGenerateAsync`/`PersistResolvedPlanInBackground`)
  now set `SchemaVersion = PersistedPlan.CurrentSchemaVersion` explicitly.
  New test `LoadLatest_MissingSchemaVersionField_ReturnsNullAndLogsWarn`
  writes JSON that omits the member entirely (rather than an explicit 0)
  and proves it is now correctly rejected; `Save_Load_
  DefaultSchemaVersion_MatchesCurrentAndRoundTrips` (which had asserted
  the now-corrected-away "unset in C# equals current" behavior) was
  renamed to `Save_Load_ExplicitCurrentSchemaVersion_RoundTrips` and now
  sets the field explicitly, matching every real construction site.
- *Must Fix: a degraded-but-structurally-valid restored plan could poison
  the Crafting Plan tab permanently, not just once.* `ApplyRestoredPlan`'s
  second try/catch (around the live-tab `RenderPlan` call) only logged on
  failure - `_treeController.ResetForNewPlan(result)`/`RestoreOverrides`,
  `_currentPlan = vm`, and `_planGeneratedAt` were all already committed
  before that guarded call, so a `RenderPlan` failure left `_currentPlan`
  pointing at a vm that had just proven it cannot render. This is
  reachable with a structurally valid file: `PlanViewModelBuilder` copies
  the crafting tree by REFERENCE rather than validating it
  (`TreeRoot = result.CraftingTree`), so a null child inside
  `CraftingTreeNode.Children` is never touched by the vm build the FIRST
  try/catch guards - only `RenderPlan`'s own tree recursion dereferences
  it. `Build()`'s own tail (`if (_currentPlan != null) RenderPlan
  (_currentPlan)`) has no try/catch of its own, and neither does
  `ViewAdapter.Build` around it, so the SAME exception would escape into
  Blish's view construction on every later visit to the tab, not just the
  one during restore. Fixed: the catch now rolls every piece of state the
  method committed back to the tab's ordinary empty fresh-start shape
  (`_treeController.ResetForNewPlan(null)`, `_lastDebugLog = null`,
  `_currentPlan = null`) before returning, matching spec item 4's "never
  partially render" for the live-tab path the same way the first
  try/catch already did for the build-time path. No new automated test:
  `CraftingPlanView` is Blish HUD UI code (constructs `Blish_HUD.
  Controls.Panel`/`Label` etc. directly), which this repo's Blish-free
  test invariant puts out of reach of the xunit suite - round 1's
  original try/catch fix shipped the same way, without a dedicated test,
  for the same reason. Verified by code inspection only.

Validation: `dotnet build -p:Platform=x64` clean (0 errors). Module test
suite green - 1258 passed (was 1257 before this round-2 pass; +1 new
test, `LoadLatest_MissingSchemaVersionField_ReturnsNullAndLogsWarn`;
`Save_Load_DefaultSchemaVersion_MatchesCurrentAndRoundTrips` was renamed
to `Save_Load_ExplicitCurrentSchemaVersion_RoundTrips`, not counted as
new). Pre-existing StyleCop analyzer warnings unchanged in nature (not
re-audited line-by-line, per item 6's own validation note). No new Blish
HUD references in tests; every changed/new test exercises real
production code (`PlanStore`/`PlanStoreHelpers` against a real temp
directory) with no contract-mirror/fake-logic tests. Item/currency/vendor
IDs remain internal-only. Not regressed: W3B's `PlanStripStatusBoard`
pull-based status strip and W3C's per-character discipline display
(neither touched by this pass).

**8. Review-fix pass round 3 (2026-08-09) - 2 Must Fix findings from a
third adversarial code review, both fixed.**

- *Must Fix: finding 2's round-2 fix protected only the rare live-tab
  branch, leaving the dominant restore-render path completely
  unguarded.* `ApplyRestoredPlan` runs at module load, before the user
  can possibly have switched to the Crafting Plan tab yet - the method's
  own doc comment calls this "the common case". In that case
  `ApplyRestoredPlan` only sets state fields (`_currentPlan = vm` among
  them) and returns; the actual render happens later, on the tab's first
  `Build()`, via that method's own tail:
  `if (_currentPlan != null) RenderPlan(_currentPlan)`. That tail had no
  try/catch of its own, and `Views/ViewAdapter.cs`'s `_buildAction(
  contentPanel)` call around `Build()` has none either - so a
  structurally valid but degraded `plan.json` (e.g. a null
  `CraftingTreeNode.Children` entry, invisible to `PlanViewModelBuilder`'s
  reference-copying vm build and only dereferenced once `RenderPlan`
  walks the tree) escaped into Blish's own view construction on the
  tab's first visit, and re-threw the SAME exception on every visit
  after, since nothing ever cleared `_currentPlan`. Fixed: `Build()`'s
  tail is now wrapped in the same try/catch shape as
  `ApplyRestoredPlan`'s live-tab branch, both now calling one shared
  `RollBackFailedPlanRender` helper.
- *Must Fix: the round-2 rollback itself was incomplete - it never
  undid the seeded staleness banner, the label text that had already
  painted it, or `_contentPanel`'s own partially-built children.*
  `PlanStripStatusBoard` had no clear/unseed API, so a rolled-back
  restore left `FinalStatusText` (and the `_statusLabel` text
  `RenderFromBoard` had already written from it, before the render
  attempt) claiming "Generated \<time\> - prices may have changed -
  Regenerate" forever - a persistent banner over a tab whose plan was
  explicitly discarded, violating the repo invariant that a missing or
  corrupt persisted plan means "no plan", never a fabricated one.
  Separately, `RenderPlan` disposes `_contentPanel`'s existing children
  before rebuilding, so an exception partway through left a half-built
  plan parented in the live panel with no cleanup. Fixed three ways:
  (1) `PlanStripStatusBoard` gained `ClearRestoredSeed()`, guarded by
  the exact same `_sequence != 0 || _inFlight` check `SeedRestored`
  itself uses, so a real Generate that raced in between the original
  seed and the render failure is never clobbered by a rollback for a
  plan it has already superseded; (2) the rollback calls it and, only
  when it reports success, explicitly resets the status label back to
  "Ready" (`RenderFromBoard` is pull-based and never overwrites a label
  with an empty `FinalStatusText`, so clearing the board alone cannot
  un-paint an already-rendered banner); (3) `RenderPlan`'s own
  dispose-then-rebuild top was factored into a new
  `ResetContentPanelToEmpty` helper, which the rollback also calls, so
  a partial build is swept back to the same empty panel a fresh,
  never-generated tab starts with. `_planGeneratedAt` is reset alongside
  `_currentPlan` too, so no stale timestamp can outlive the plan it
  described.

Both fixes share one new private helper, `RollBackFailedPlanRender`,
called from both `RenderPlan` call sites that can reach a
still-unvalidated restored vm (`ApplyRestoredPlan`'s live-tab branch and
`Build()`'s render tail) - a single rollback shape instead of two copies
that could drift apart.

New tests: `PlanStripStatusBoardTests` gained 4 for the new
`ClearRestoredSeed` method (clears an active seed and returns true;
harmless on a virgin board; rejected while a real generation is
in-flight; rejected once a real generation has already finished),
following the same coverage pattern round 1's `SeedRestored` tests
established. No new `CraftingPlanView` test - unchanged from round 2's
own note: it is Blish HUD UI code, out of reach of this repo's Blish-free
xunit suite; both fixes were verified by code inspection plus the build/
test run below.

Validation: `dotnet build -p:Platform=x64` clean (0 errors). Module test
suite green - 1262 passed (was 1258 before this round-3 pass; +4 new
tests, all listed above). Pre-existing StyleCop analyzer warnings
unchanged in nature (not re-audited line-by-line, per item 6's own
validation note). No new Blish HUD references in tests; every new test
exercises real production code (`PlanStripStatusBoard`) with no
contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only. Not regressed: W3B's `PlanStripStatusBoard` pull-based
status strip (`ClearRestoredSeed` is additive - every pre-existing
Begin/UpdatePhase/Finish/SeedRestored behavior and test is unchanged) and
W3C's per-character discipline display (untouched by this pass).

**9. Review-fix pass round 4 (2026-08-09) - the one finding that survived
rounds 1-3, closed with a class-level fix (1 Critical from a fourth
adversarial code review).**

**Why rounds 1-3 never closed this.** Each prior round guarded one more
individual RENDER call site (`ApplyRestoredPlan`'s vm build, its live-tab
`RenderPlan` call, then `Build()`'s own render tail) against a
structurally-valid-but-degraded `plan.json` - e.g. a null entry inside
`CraftingTreeNode.Children`. That pattern cannot converge: it only
protects a call site someone already thought to guard, and this file's
own render machinery has more than one. Two survived all three rounds
because they are not part of any render PASS at all -
`Views/Rendering/TreeSectionController.cs`'s "Expand All" button and the
per-node expand/collapse toggle both call `RenderTreeNode` directly from a
Click handler, on a node that was never visited during the guarded initial
render because it was collapsed by default
(`PlanContentHeightMath.TreeChildFlowHeight` returns 0 without recursing
for a collapsed node, and `RenderTreeNode` itself only recurses into
already-expanded children - the real-world default is every node past
depth 1). A null `CraftingTreeNode.Children` entry at depth 2+ therefore
sails through every existing try/catch untouched and only throws later,
from a click, with no catch anywhere nearby -
`node.Children.Count`/`foreach (var child in state.Node.Children)` crash
outside any rollback machinery. A third, similarly unguarded site was
found while building this fix and had not been reported before:
`TreeSectionController`'s Craft All/Buy All buttons call
`CraftingPlanPipeline.BuildPresetOverrides`, which walks the WHOLE
`PlanSolveContext.Tree` (`RecipeNode`/`RecipeOption` graph) BEFORE
`ApplyOverridesAndResolve`'s own try/catch is ever reached.

**Fix (class-level, not another call-site guard).** A new
`Services/PlanStructuralValidator.cs` (Blish-free, pure) walks the ENTIRE
restored object graph once, at the deserialization boundary
(`PlanStoreHelpers.DeserializePersistedPlan`, right after the existing
Result/Plan/SchemaVersion gate) - both trees (`CraftingTreeNode`/
`MultiItemRoots` the display path renders, and `PlanSolveContext.Tree`'s
`RecipeNode`/`RecipeOption` graph the local override re-solve and
`BuildPresetOverrides` both walk unconditionally on every single click,
not just when there happens to be a Craft step) plus every list/dictionary
`PlanViewModelBuilder`/`PlanResultBuilder`/`CraftingTreeBuilder`/
`PlanSolver`/`CurrencyDisplayResolver` dereference with NO per-call null
guard: `Plan.Steps` (required non-null; every entry non-null),
`Plan.CurrencyCosts`/`Plan.TimegatedItems`/`UsedMaterials`/
`RequiredDisciplines`/`RequiredRecipes`/`RequestedItems` (no null entries
where non-null), `ItemMetadata`/`CurrencyMetadata` dictionaries (no null
VALUES for a present key - a missing key was already handled everywhere),
and, whenever a `SolveContext` is present, `Tree` (required non-null,
recursively valid), `Prices` (required non-null, no null values -
`PlanSolver.GetBuyCost`/`CollectPresetOverrides` both call
`prices.TryGetValue` with no null check on the dictionary itself),
`VendorOffers` (no null list values or entries), `Metadata`/
`CurrencyMetadata`/`RequestedItems` (same shape as the result-level
copies). Every `CraftingTreeNode`/`RecipeNode` recursion is bounded to a
generous, explicit depth (200 - 10x+ any realistic GW2 crafting tree,
though Newtonsoft's own unconfigured `JsonReader.MaxDepth` of 64 already
rejects JSON nested this deep before the walk ever runs; the walk itself
must not be the weak point per the round 4 mandate). A single invalid
field anywhere rejects the WHOLE file - `PlanStoreHelpers` throws, which
propagates to `PlanStore.LoadLatest`'s own existing try/catch: one Warn
log line, then a null return (fresh start), the same "never partially
accept" contract every other tolerance-gate check in that method already
follows. The round 1-3 render-tail try/catch + rollback machinery
(`RollBackFailedPlanRender`, `PlanStripStatusBoard.ClearRestoredSeed`) is
kept unchanged as defense in depth, not removed - it still protects
against any future degraded shape this walk does not yet know to name.

New tests (`PlanStoreTests`, 6 new, Blish-free, real `PlanStore` + temp
dir): every fixture starts from a REAL pipeline-produced `PersistedPlan`
(a new `BuildDeepPipeline` helper gives a genuine 3-level tree so
`CraftingTree.Children[0].Children[0]` is a real depth-2 node), serialized
via the actual production `PlanStoreHelpers.SerializePersistedPlan`, then
surgically corrupted at one exact JSON location via a `JObject`. A null
entry inside `CraftingTreeNode.Children` at depth 2 is rejected (null +
exactly one Warn, asserted by count and by the exact `PlanStructuralValidator`
reason string, distinguishing it from every pre-existing rejection
reason). An explicit `"Children": null` on a tree node is proven to LOAD
SUCCESSFULLY, not rejected - `CraftingTreeNode.Children`'s own
null-coalescing setter already neutralizes that exact shape one layer
below the validator, so this documents why "null Children list" could not
be reproduced as a corrupt-file case the way the mandate's wording
literally describes, and proves the validator does not false-reject it. A
null `RecipeNode.Recipes` LIST and a null `RecipeNode` ENTRY inside
`RecipeOption.Ingredients`, both inside `SolveContext.Tree`, are each
rejected - the closest real equivalent to a "null Children list"
corruption, since `RecipeNode.Recipes`/`RecipeOption.Ingredients` have no
such setter guard. A solve-context collection nulled (`SolveContext.Prices`
set to `null`) is rejected. A null entry inside `Plan.Steps` is rejected.
Every pre-existing `PlanStoreTests` fixture - including the full-featured,
multi-item, and override-round-trip ones that already exercise every
non-trivial shape `PlanSolveContext` carries - continues to pass
unmodified, proving the validator accepts a real pipeline-generated plan
unchanged.

Validation: `dotnet build -p:Platform=x64` clean (0 errors, no new
warnings from either touched/new file). Module test suite green - 1268
passed (was 1262 before this round-4 pass; +6 new tests, all listed
above). No new Blish HUD references in tests; every new test exercises
real production code (`PlanStore`/`PlanStoreHelpers`/`PlanStructuralValidator`
via a real `CraftingPlanPipeline`-produced fixture) with no contract-mirror/
fake-logic tests, no fake file I/O. Item/currency/vendor IDs remain
internal-only. Pricing/solve logic itself is untouched - this pass adds
one validation-only gate ahead of deserialization returning, nothing in
the solve/render path changed. `PlanStructuralValidator.IsStructurallyValid`
runs exactly once per module session (`Module.LoadAsync`'s single
`PlanStore.LoadLatest()` call) - not a hot/per-frame path, so its O(graph
size) walk carries no per-frame or per-click performance cost.

**10. Review-fix pass round 5 (2026-08-09) - 1 Must Fix finding from a
fifth adversarial code review (the asymmetry that survived round 4's own
class-level rewrite).**

Round 4's `PlanStructuralValidator` validated
`CraftingPlanResult.UsedMaterials` (line 141) but not the SEPARATELY
serialized `PlanSolveContext.UsedMaterials` copy of the same list -
plain `Newtonsoft.Json` writes no `$ref`, so the two fields are two
independent arrays on disk even though they point at the same in-memory
list at generation time. A `plan.json` with a clean
`Result.UsedMaterials` but a null entry inside
`Result.SolveContext.UsedMaterials[i]` therefore passed the entire round-4
walk. `CraftingPlanPipeline.ResolveWithOverrides` (reached from any
decision-pill click or the Best Path preset, inside
`TreeSectionController.ApplyOverridesAndResolve`'s try/catch) passes
`context.UsedMaterials` straight into `PlanResultBuilder.Build`
(`foreach (var used in usedMaterials) { ... used.ItemId ... }`,
`Services/PlanResultBuilder.cs:120-122`) and, for a single-item context,
`SellSideEconomics.ComputeMaterialOpportunityCost`
(`used.ItemId`/`used.QuantityUsed`, `Services/SellSideEconomics.cs:184-186`)
- neither with a per-entry null check. Because both sites sit inside that
guarded re-solve, the practical outcome was a logged "Override re-solve
failed" and a dead pill rather than a crash, but that is exactly the
already-covered failure class every other `IsValidSolveContext` check
exists to close - the class's own doc comment claims every collection
the re-solve path dereferences is covered, and this one field was simply
missed.

**Fix.** `PlanStructuralValidator.IsValidSolveContext` gained one more
check, `NoNullEntries(context.UsedMaterials, "SolveContext.UsedMaterials", ...)`,
alongside its existing `RequestedItems` check - same helper, same
"null list is fine (optional field, matches a snapshot-less Generate),
null entry is not" contract already used for eleven other fields in this
file, no new abstraction introduced.

New test (`PlanStoreTests.LoadLatest_NullEntryInSolveContextUsedMaterials_ReturnsNullAndLogsWarnExactlyOnce`,
Blish-free, real `PlanStore` + temp dir): reuses the existing
`BuildOwnMaterialsPipeline` fixture with `OwnFourOfIngredient()` and
`OwnMaterialsMode.Valued` (a real pipeline result with a genuinely
non-empty `UsedMaterials` on BOTH `Result` and `Result.SolveContext`),
corrupts ONLY the `SolveContext` copy via `JObject` surgery (leaving
`Result.UsedMaterials` clean, to actually exercise the asymmetry rather
than a shape the round-4 check already caught), and asserts reject + the
exact `SolveContext.UsedMaterials[0] is null` reason string + exactly one
Warn. Every pre-existing `PlanStoreTests` fixture, including the other
five round-4 rejection tests and the full round-trip/override-round-trip
tests, continues to pass unmodified.

Validation: `dotnet build -p:Platform=x64` clean (0 errors, no new
warnings from either touched file). Module test suite green - 1269
passed (was 1268 before this round-5 pass; +1 new test). No new Blish
HUD references in tests; the new test exercises real production code
(`PlanStore`/`PlanStoreHelpers`/`PlanStructuralValidator` via a real
`CraftingPlanPipeline`-produced fixture), no contract-mirror/fake-logic
tests, no fake file I/O. Item/currency/vendor IDs remain internal-only.
Pricing/solve logic itself is untouched - this pass only widens the
existing round-4 validation gate to cover one more field.

Live desktop gate: PASS (2026-08-15, orchestrator session, fresh
sandbox, three scenarios across a real Blish restart cycle):

- Generate + persist: a real Zojja's Claymore generation (4.2s)
  produced data/plan.json (689 KB) and the normal "Plan generated -
  <time>" strip.
- Restart + restore: Blish killed and relaunched; the module log
  showed ZERO generation activity (no auto-resolve), and the Plan tab
  rendered the full plan instantly with the exact staleness banner
  "Generated Aug 15, 2026 1:39 PM - prices may have changed -
  Regenerate"; search box back at defaults per spec. On the RESTORED
  data: "Expand All" rendered depth-3/4 nodes with zero exceptions
  (the round-4 validator's crash class), and a decision-pill override
  (TP -> CRAFT on the inscription) re-solved locally (total cost
  52g30s33c -> 57g05s75c) and re-persisted the file (689 KB ->
  712 KB, fresh mtime). Note: the first pill click hit the
  already-selected TP pill - a semantic no-op per the M38 lesson -
  correctly causing no re-solve and no rewrite.
- Corrupt-file recovery: plan.json surgically corrupted
  (CraftingTree.Children[0].Children[0] = null, the exact round-4
  repro shape); relaunch produced EXACTLY one Warn naming the
  validator reason ("... failed structural validation
  (CraftingTree.Children[0].Children[0] is null) - corrupt or
  degraded file."), a clean fresh-start Plan tab ("Ready", no plan),
  and zero exceptions in the Blish log.

**Post-W3D quick fix: gzip-compress the on-disk plan file
(2026-08-15).** User-directed, "quick and dirty" scope: the ~700 KB
plan.json this section measured above is now written gzip-compressed
instead of as plain compact JSON. `PlanStore.Save` gzips the same
serialized JSON bytes it always produced (`PlanStoreHelpers.
SerializePersistedPlan`/`DeserializePersistedPlan` and the
`PlanStructuralValidator` gate above are completely untouched - only
the container encoding changed); the file name stays `plan.json` (no
`.gz` rename - simplest, and avoids leaving an orphaned old-named file
around). `PlanStore.LoadLatest` sniffs the first two on-disk bytes for
the gzip magic number (0x1F 0x8B, RFC 1952) and decompresses when
present, otherwise falls back to parsing the bytes as plain UTF-8
JSON directly - so an existing plain-JSON `plan.json` written by the
pre-fix PR #107 code still loads unchanged. Both decompression and
JSON parsing happen inside `LoadLatest`'s single existing try/catch,
so every prior tolerance guarantee (truncated/corrupt data, a
gzip-wrapped-but-invalid-JSON file, a structurally-invalid plan) still
produces exactly one Warn and a null return - never a partial load -
with no new failure paths introduced. `System.IO.Compression.
GZipStream` is in-box for net48; the csproj gained one plain
`<Reference Include="System.IO.Compression" />` entry (no NuGet
package, matching `System`/`System.Windows.Forms`'s own
no-HintPath style).

New tests (`PlanStoreTests`, four, all against a real `PlanStore` +
temp dir, no fake file I/O): a save-then-load round trip asserts the
on-disk file starts with the gzip magic bytes and is materially
smaller than the raw serialized JSON (measured on the existing
two-item fixture: 4146 bytes raw vs. 1306 bytes gzipped, about a 68%
reduction); a plain uncompressed `plan.json` written directly via the
production serializer (no gzip) still loads, proving backward
compatibility with files in the wild; truncated gzip bytes and a
gzip-wrapped invalid-JSON payload each return null with exactly one
Warn logged, matching every other corrupt-file test in this section.
All 30 `PlanStoreTests` and the full 1273-test module suite (was 1269
before this pass; +4 new tests) pass.

No live desktop gate for this pass - container-encoding-only change,
user-sanctioned quick fix, validated by real-file unit round-trip
tests instead (see above).

**Recipe Tree header button tooltips (2026-08-15).** Small user-requested
diff, riding along with the W4A cost-section gate: the five Recipe Tree
section header buttons (`Views/Rendering/TreeSectionController.cs`,
`CreateTreeSection` - Best Path, Craft All, Buy All, Expand All, Collapse
All) now set `BasicTooltipText` directly on the `StandardButton` itself
(the control that actually captures the mouse - see the M32 lesson noted
elsewhere in this doc). Each tooltip's wording was derived from the real
click handler, not guessed: Craft All/Buy All call `ApplyPreset`, which
clears every existing manual override and rebuilds it from
`CraftingPlanPipeline.BuildPresetOverrides` walking the FULL solver tree
(including nodes hidden under bought intermediates) - forcing Craft (or
Buy from TP) on every node where that source is feasible, and leaving
every infeasible node to the solver's own normal pick, exactly as
`PlanSolver.Evaluate`'s override handling (`canCraft`/`canBuyTp` gates)
actually resolves it. Best Path clears `_nodeOverrides` entirely
(covering both individual pill clicks and a prior Craft All/Buy All) and
re-solves for the solver's unforced cheapest plan; `_ignoredItemIds` is
untouched by any of the three presets, matching the existing field-level
doc comment on that collection. Expand All/Collapse All tooltips describe
the existing recursive expand/build-lazy-children and hide-children
behavior verbatim. Pure tooltip-string change - no production logic
touched, no new tests (out of the Blish-free test scope for pure
BasicTooltipText strings on Blish controls; hover text is covered by the
live desktop gate).

Live desktop gate: PASS 2026-08-16 (orchestrator live sandbox session, combined wave-4 staging build; shipped via the W4B PR #109 which stacks on this branch). Verified: hovering Best Path renders its handler-derived tooltip verbatim ("Clears every manual override, including Craft All/Buy All, and re-solves for the solver's cheapest plan. Ignore selections are left unchanged.").
