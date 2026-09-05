# D3 - Plan History tab

Status: design proposal (no code changes). Written against `master` at
commit `d62b7ed` (MEASURED, `git log -1` at proposal time - master is
advancing concurrently under the M38 wave; this is a read-only reference
point, not a claim that master is frozen).

Epistemic tags used throughout: **MEASURED** = read directly from code;
**INFERRED** = a reasoned conclusion this proposal draws from MEASURED
facts, not itself directly observed; **GUESS** = a
judgment call with no code evidence either way.

---

## 1. Problem / intent

Intent: the Plan History tab should show previously generated crafting plans
that can be selected and opened, or reused. The exact shape was left open.

Today (MEASURED): the "Plan History" tab is a single grey "Coming Soon"
label (`Module.BuildPlaceholder`). Nothing about a generated plan survives
past the session - `CraftingPlanResult`/`PlanSolveContext` live only in
`CraftingPlanView`'s private fields (`_lastResult`, `_nodeOverrides`,
`_ignoredItemIds`) and vanish on tab close or module reload. There is no
service, store, or view code anywhere in the repo that persists a plan
request or result. This proposal resolves what a history *entry* is, what
"reopen" means, how dedup/retention/auto-capture work, and where the real fragility (per-node
override replay) actually lives in the code - rather than leaving those as
open questions.

## 2. Proposed UX

**Tab contents** (`Views/PlanHistoryTabContent.cs`, new, ~same shape as
`LogTabContent.cs`/`MainView.cs` - lightweight FlowPanel pattern, see
Architecture Impacts):

- One root `FlowPanel(SingleTopToBottom, CanScroll=true)`, refreshed on
  `TabChanged` (mirrors `LogTabContent.Refresh()`'s exact idiom) and
  immediately after every successful Generate (auto-capture, see 3.4).
- Each row: up to 3 item icons (icon-only, GW2 API icon URLs already in
  `ItemMetadata.IconUrl`/the entry's own denormalized copy) + item name(s)
  ("Iron Ingot x50" or "Iron Ingot x50, +2 more" for a batch) + relative
  date ("2h ago" / absolute on hover tooltip) + total cost **at
  generation time** rendered gold/silver/copper via the shared coin
  renderer (right-of-number invariant - see 6) + a pin toggle + a delete
  button + a **Reuse** button.
- Clicking a row (or a dedicated "Details" affordance) expands an inline,
  read-only summary directly under the row: shopping-list line items,
  required disciplines, total cost - all pulled from a `PlanViewModel`
  built fresh (see 4.3), not the stale generation-time numbers. This is
  the "view" action, folded into the row itself rather than a separate screen.
- **Reuse** button: re-runs the stored request through the real pipeline
  at current prices/settings and replaces the inline expansion with the
  new result (see 4.2 for exactly what is/isn't replayed).
- Empty state: single centered label, same idiom as `LogTabContent`'s
  implicit "no lines" case - *"No plans generated yet. Generate a plan
  from the Crafting Plan tab and it will appear here."*
- A small footer row: entry count ("12 of 50 kept") + "Clear History"
  (deletes all unpinned entries, confirmation via the existing modal
  dialog pattern `ModalDialogX/Y` settings already back) + retention-cap
  hint.

Rows are sorted pinned-first, then newest-first within each group.

## 3. Core design questions - resolved

### 3.1 What is a history entry: request vs. result

**Recommendation: the REQUEST + a small generation-time display summary.
Not the full `CraftingPlanResult`.**

Rationale (grounded in MEASURED facts):

- `CraftingPlanResult.SolveContext` (`PlanSolveContext`, `Models/PlanSolveContext.cs`)
  is explicitly documented as "everything needed to re-solve a generated
  plan locally" and carries the **full pre-reduction `RecipeNode` tree**,
  a full `IReadOnlyDictionary<int, ItemPrice> Prices` dict, a full vendor
  offers dict, item metadata, currency metadata, and more - all
  **snapshotted at generation time**. Persisting this wholesale is (a)
  large per entry (a multi-item batch's tree + price dict can run into the
  hundreds of ingredient nodes) and (b) stale the instant TP prices move,
  which for a Trading-Post-driven module is minutes, not days.
- The only genuinely small, stable, honestly-reusable part of a plan
  request is: `RequestedItems` (`List<PlanRequestItem>{ItemId,Quantity}`),
  the `useOwn` bool, `PriceBasis`, and the settings snapshot that gated
  generation (`OwnMaterialsMode`/`HomesteadTiers`). All four are already
  first-class fields threaded through
  `CraftingPlanPipeline.GenerateStructuredAsync` today (MEASURED, its
  parameter list and `PlanSolveContext`'s own fields).
- `IgnoredItemIds` is keyed by **item id**, not node id (MEASURED:
  `CraftingPlanView._ignoredItemIds` is a `HashSet<int>` of item ids, fed
  into `PlanSolver.Solve`'s `ignoredItemIds` parameter) - this is stable
  across any regeneration regardless of tree shape, and is cheap to store.
- Per-node overrides (`IReadOnlyDictionary<int, AcquisitionSource>`, keyed
  by `NodeId`) are **not** similarly safe - see 3.6.

So an entry is:

```
PlanHistoryEntry
{
    Guid   EntryId
    DateTime CreatedAtUtc
    DateTime LastGeneratedAtUtc
    bool   Pinned

    // The request (small, stable, re-solvable)
    List<PlanRequestItem> RequestedItems   // {ItemId, Quantity}
    bool         UseOwnMaterials           // the `useOwn` flag
    PriceBasis   PriceBasis
    bool         ValueOwnMaterials         // OwnMaterialsMode snapshot
    Dictionary<int,int> HomesteadTiers     // itemId -> tier, snapshot
    List<int>    IgnoredItemIds            // item-id keyed, safely replayable

    // Display-only summary captured at generation time (denormalized so
    // the list renders without re-fetching metadata)
    List<PlanHistoryItemSummary> ItemSummaries  // {ItemId, Name, IconUrl, Quantity}
    long   TotalCoinCostAtGeneration
    int    ManualOverrideCountAtGeneration      // informational only, see 3.6

    // Optional: cheap repeat-generation samples for the sparkline gravy (3.7)
    List<PlanHistorySample> CostSamples         // {TimestampUtc, TotalCoinCost}, capped
}
```

This is INFERRED to be on the order of 0.5-2 KB per entry even for a
dozen-item batch with icon URLs, versus a full `CraftingPlanResult`+
`PlanSolveContext` which can be tens to hundreds of KB per entry
(unbounded by tree size). A capped list of 50 such entries stays in the
tens-of-KB range for the whole store file.

### 3.2 Reopen semantics: "view" vs. "reuse"

Two distinct, clearly-labeled actions, not one ambiguous "open":

- **View** (default, free): render the entry's own captured
  `ItemSummaries`/`TotalCoinCostAtGeneration`/date inline - no solve, no
  network, shows numbers **as they were at generation time**. This is
  "frozen and possibly stale" by design, and is labeled as such in the UI
  (e.g. a small "as of {date}" caption next to the cost).
- **Reuse** (button, does work): calls the exact same
  `GenerateStructuredAsync`-shaped delegate `CraftingPlanView` already
  gets from `Module.cs`, with the stored `RequestedItems`/`UseOwnMaterials`/
  `PriceBasis` and current live prices/settings, then renders the fresh
  result inline via `PlanViewModelBuilder.Build(result)` (MEASURED:
  `Services/PlanViewModelBuilder.cs`, `Build(CraftingPlanResult) ->
  PlanViewModel` - Blish-free, already produces exactly the row-list shape
  needed: `ShoppingList`, `RequiredDisciplines`, `Summary` sections).
  `IgnoredItemIds` is replayed; per-node overrides are not (3.6).
  `LastGeneratedAtUtc`/`TotalCoinCostAtGeneration`/`CostSamples` update on
  every Reuse, same as auto-capture (3.4) - a Reuse **is** a Generate for
  bookkeeping purposes.

A stale full-result "replay exactly what I saw before, pill-clicks and
all" mode (a third option that was floated) is **not** proposed: it
would require persisting the full `PlanSolveContext` (rejected in 3.1) to
get the interactive tree back, for a use case ("I want to click pills on
a frozen historical snapshot") that was only ever raised as a maybe.
"View" already answers the cheaper, more common question ("what did this
cost me when I last checked").

### 3.3 Cross-tab handoff ("open in the interactive Crafting Plan tab")

This is the one place "selected and opened" pulls against the constraint
that the Crafting Plan tab is out of scope and takes no changes. Resolved as an explicit **V1 vs. V2 split**:

- **V1 (this proposal's core scope):** Reuse renders its result inline in
  Plan History's own lightweight panel (3.2), built entirely from
  `PlanViewModelBuilder` output. **Zero changes to `Views/CraftingPlanView.cs`.**
  The user does not get pill-click overrides back on a reused plan; they
  get an accurate, current-price read-only re-solve.
- **V2 (stretch, sequenced after M38's view waves - see 7):** add one
  small, additive public method to `CraftingPlanView`
  (e.g. `LoadRequest(IReadOnlyList<PlanRequestItem>, bool useOwn,
  PriceBasis, HashSet<int> ignoredItemIds)`) that populates its existing
  item-row UI (`AddItemRow`/`RemoveItemRow`, MEASURED to exist already at
  `Views/CraftingPlanView.cs:1223`) and triggers Generate as if the user
  had, then have `Module.cs` switch `SelectedTab` to the Crafting Plan
  tab. This is a genuinely new call surface on a 4812-line file that
  M38 WP-04/21/23/24/25/26 are actively carving up - see 7 for why it must
  wait.

### 3.4 Auto-capture vs. explicit save

**Recommendation: auto-capture on every successful Generate** (including
every Reuse), with the dedup+cap machinery below keeping the list useful
rather than a junk drawer. Matches the account-snapshot module's own
precedent (`SnapshotStore` captures automatically on the refresh cadence;
the user's only manual controls are Clear/Refresh-Now) rather than
inventing a third idiom (opt-in "Save to History" buttons exist nowhere
else in this codebase). An explicit "Save" step is easy to forget and
directly undercuts the intent ("previously generated ... plans" implies
"just show me what I already did").

Manual controls that DO exist: per-row **Pin** (exempts from cap/eviction)
and **Delete**, plus a footer **Clear History** (unpinned only).

### 3.5 Dedup / retention

- **Dedup key**: a hash (or simple structural equality check - see note
  below) over `(sorted RequestedItems, UseOwnMaterials, PriceBasis,
  ValueOwnMaterials, HomesteadTiers, sorted IgnoredItemIds)` - i.e.
  everything that defines "the same request under the same assumptions."
  Generating the *same* request twice does **not** create a second row:
  it bumps the existing entry's `LastGeneratedAtUtc`, overwrites
  `TotalCoinCostAtGeneration`, and appends one `{timestamp, cost}` sample
  (capped list, e.g. last 20 - discipline needed so a request the user
  regenerates daily for months doesn't grow unbounded; see 3.7).
- Changing `PriceBasis`/`ValueOwnMaterials`/`HomesteadTiers` counts as a
  **different** entry under this key (open question 8.7 - looser dedup that
  ignores basis/settings and only compares `RequestedItems`+`IgnoredItemIds`
  is a defensible alternative).
- **Retention cap**: a `PlanHistoryMaxEntries` int setting (default GUESS
  50 - no existing precedent to anchor a number to; open question). Oldest
  **unpinned** entry is evicted first when the cap is exceeded. Pinned
  entries are exempt from both the cap and any future age-based eviction.

### 3.6 Per-node overrides and NodeId stability - the fragile part

This piece cannot be resolved from code alone, and it deserves the most
precise answer this proposal can give, because getting
it wrong doesn't crash - it silently misapplies an override to the wrong
ingredient.

**MEASURED**: `RecipeNodeIds.Assign` (`Services/RecipeNodeIds.cs`) is a
*deterministic pre-order DFS over the pre-reduction `RecipeNode` tree*,
assigning `NodeId` purely from structural position (`node.Recipes[i]
.Ingredients[j]`, in list order) - **not** a function of price, decisions,
ownership, or which acquisition source was chosen. It runs once per fresh
solve, before `InventoryReducer` prunes the tree
(`CraftingPlanPipeline.cs:316-325`, "Pre-assign stable NodeIds to the
UNREDUCED tree BEFORE Step 6 clones/prunes it").

**INFERRED** from that: for the *identical* request re-solved with
*identical* recipe-affecting inputs, the same logical ingredient is very
likely to receive the same `NodeId` on a fresh regenerate, because tree
construction is deterministic given the same recipe seed data and the
same request/settings inputs. This is a materially better starting point
than "NodeIds are just random and never stable" - the assignment *is*
reproducible in principle.

But three concrete drift vectors break that reproducibility, and none of
them are guarded against anywhere in the current code:

1. **Recipe seed drift.** `ref/mystic_forge_recipes.json` / the recipe
   cache can be updated between when an entry was saved and when it is
   reused (a module update, a recipe-cache refresh). Any change to
   ingredient list order or count for a recipe in the path shifts every
   downstream `NodeId` in that subtree. Silent, no error.
2. **Settings-driven tree-shape differences.** This proposal could not
   confirm from code alone whether `OwnMaterialsMode`/`HomesteadTiers`
   affect only *decisions* (Craft vs. Buy at a fixed node) or also *which
   recipe options exist as nodes at all* in the raw pre-reduction tree.
   If the latter, a Reuse at different current settings than the stored
   snapshot could shift shape even with byte-identical recipe data. Left
   as open question 8.2 - it needs a codebase check, not a guess.
3. **No structural fingerprint exists today.** Nothing in `RecipeNodeIds`,
   `PlanSolveContext`, or `CraftingPlanPipeline` computes or checks a
   tree-shape hash before trusting a `NodeId`-keyed dictionary against a
   *different* solve's tree. Building one would be new engineering (a
   node-count/shape hash comparison gate), not a UI decision, and the
   specific missing mechanism is identified here.

**Recommendation**: v1 does **not** attempt to replay per-node overrides.
`IgnoredItemIds` (item-id keyed, MEASURED stable regardless of tree
shape) is replayed on every Reuse. The override *count* at generation
time is stored and shown for information ("2 manual overrides were
applied when this was generated - not restored on Reuse") but the
dictionary itself is either not persisted at all, or persisted
best-effort/display-only (never fed back into a solve). Building safe
override replay (the structural-fingerprint guard in point 3 above) is
flagged as a real, separate follow-up - not something this UI proposal
should paper over by just wiring the dictionary through and hoping the
shape matches.

### 3.7 Cost-over-time sparkline - assessed honestly

This is optional gravy. Assessed honestly:

- **Data capture is nearly free**: once dedup/bump logic exists (3.5), the
  `{timestamp, cost}` sample this needs is a one-line append inside the
  same code path. Capping at ~20 samples per entry keeps it bounded.
- **Rendering is not free**: there is no chart/sparkline/point-plotting
  control anywhere in this codebase today (MEASURED - `Views/` has icon
  controls, labels, panels, dropdowns, checkboxes, textboxes; no chart
  primitive). A sparkline needs a small new Blish-free-adjacent drawing
  helper (a handful of lines/rects on a `Panel`), which is genuinely new
  UI infrastructure, however small.
- **Value is narrow**: only meaningful for a request the user
  *repeatedly* regenerates (e.g. a daily-farmed legendary component). For
  the common "what does this ascended insignia cost me right now" query
  it is dead weight with exactly one data point.

**Recommendation**: capture the sample data in v1 (cheap, and useful even
without a chart - "last generated 3 days ago at 1g 20s cheaper" is a
one-line text derivation from two samples, no drawing required). Defer
the actual sparkline *rendering* to a v2/gravy increment - it is the one
piece of this proposal that would introduce a wholly new UI primitive
rather than reusing an existing one.

## 4. Data & architecture

### 4.1 Reused (no changes needed)

- `CraftingPlanPipeline.GenerateStructuredAsync` (`Services/CraftingPlanPipeline.cs:212,557`)
  - the exact same delegate shape `Module.cs` already builds inline for
    `CraftingPlanView`'s constructor
    (`Func<IReadOnlyList<PlanRequestItem>, bool, PriceBasis,
    CancellationToken, IProgress<PlanStatus>, Task<CraftingPlanResult>>`,
    MEASURED at `Views/CraftingPlanView.cs:91` and `Module.cs:298-307`).
    **Recommendation**: factor that inline lambda in `Module.cs` into a
    single private method/field and pass the *same delegate instance* to
    both `CraftingPlanView` and the new `PlanHistoryTabContent` - a
    `Module.cs`-only change, touching neither `CraftingPlanPipeline.cs`
    nor `CraftingPlanView.cs`.
  - `Services/PlanViewModelBuilder.cs` - `Build(CraftingPlanResult) ->
    PlanViewModel` (Blish-free, MEASURED) for turning a Reuse result into
    the same `ShoppingList`/`RequiredDisciplines`/`Summary` row shapes
    already used elsewhere, without touching `CraftingPlanView.cs`'s
    rendering code at all.
  - `Models/PlanRequestItem`, `Models/AcquisitionSource` (display-only
    override count, not replayed), `PriceBasis`, `OwnMaterialsMode`,
    `HomesteadEfficiencyTiers` - all existing model types, reused as the
    request-snapshot shape.
  - The lightweight FlowPanel scroll pattern (A) already used by
    `MainView`/`LogTabContent`/`SettingsTabContent` - **not** the M33
    `PlanContentHeightMath`/relayout-registry contract (B), which is
    `CraftingPlanView`-only and explicitly out of scope. Plan History's
    rows are flat label/icon/button rows with no live-resize reflow need.
  - The shared coin renderer landing from WP-21/22 (`CoinCurrencyRenderer`)
    for the per-row total-cost display - **do not** write a third private
    `AddCoinSegment`/`GetCoinColor` copy (see 6, and Dependencies 7).

### 4.2 New

- `Services/PlanHistoryStore.cs` - new store, same shape as
  `StatusStore`/`VendorOfferStore`'s atomic-write idiom: write to
  `plan_history.json.tmp`, then `File.Replace`/`File.Move` over the real
  path (see the correction in the note below). Takes an `Action<string,
  Exception> onError = null` constructor/method parameter from day one
  (matches WP-16's planned shape for the other four stores - see
  Dependencies 7) instead of a bare `Debug.WriteLine`, so it is not a
  fifth store WP-16 has to retrofit.
  - **The three stores do NOT share one atomic pattern.** MEASURED (full
    read of each file): `SnapshotStore.Save` uses a plain
    `File.WriteAllText(_filePath, json)` with **no** tmp file or atomic
    replace at all. `StatusStore.Save` and `VendorOfferStore.SaveOverlay`
    *do* use the tmp+Replace/Copy pattern. `PlanHistoryStore` should
    follow the `StatusStore`/`VendorOfferStore` shape, not `SnapshotStore`'s.
- `Models/PlanHistoryEntry.cs` (+ `PlanHistoryItemSummary`,
  `PlanHistorySample`) - the shape in 3.1.
- `Views/PlanHistoryTabContent.cs` - new view class, `Build(Container)`
  entry point wired the same way `LogTabContent`/`SettingsTabContent` are
  in `Module.cs` (constructor injection of the store + the shared
  generate delegate + `ModuleSettings`).
- `Services/ModuleSettings.cs` - one new primitive setting,
  `PlanHistoryMaxEntries` (int, default GUESS 50), following the exact
  `DefineSetting<int>` idiom already used for `HomesteadFiberTier` etc. No
  new control idiom needed in `SettingsTabContent` - a numeric cap fits
  idiom (b) (TextBox + shared Save button + `SettingsInputParser`
  validation) already established there.
- `tests/GW2CraftingHelper.Tests/Services/PlanHistoryStoreTests.cs` - real
  temp-dir file IO, Blish-free, following `VendorOfferStoreTests.cs`'s
  exact `IDisposable` + `Path.GetTempPath()`/`Guid.NewGuid()` template
  (MEASURED, read in full). Covers: save/load round-trip,
  dedup-bump-not-duplicate, cap eviction (oldest unpinned first, pinned
  survives), delete, and the `onError` callback firing on a forced-failure
  path (mirrors what WP-16 will add to the other four stores).
  A parallel `PlanHistoryDedupKeyTests.cs` (or folded into the same file)
  proving the dedup key is order-insensitive for `RequestedItems`/
  `IgnoredItemIds` and settings-sensitive per 3.5's chosen strictness.

### 4.3 Threading notes

All work here is either (a) synchronous JSON read/write on the store
(same cost profile as `StatusStore`/`SnapshotStore` today - small files,
no async needed, matches existing precedent) or (b) the exact same
`GenerateStructuredAsync` call `CraftingPlanView` already makes, which is
already `async`/cancellable and already returns to the main thread via
whatever mechanism `Module.cs`'s existing lambda uses (MEASURED: no new
threading pattern introduced). No new `FrameTicker` is needed for v1 - the
tab refreshes on `TabChanged` and immediately after a Reuse completes,
mirroring `LogTabContent.Refresh()`'s exact trigger points. If the v2
sparkline *rendering* or any "auto-reprice pinned entries on an interval"
gravy feature is ever added, it would need its own ticker and must
register teardown through whatever `Module.Unload()` path WP-17
centralizes (see Dependencies 7) - not a new ad-hoc one.

## 5. Settings introduced

| Setting | Type | Default | Idiom |
|---|---|---|---|
| `PlanHistoryMaxEntries` | `SettingEntry<int>` | 50 (GUESS) | (b) TextBox + shared Save + `SettingsInputParser` validation, same section-add pattern as Homestead tiers |

No new control idiom is introduced (open question 8.6 raises whether
"unlimited until the user clears" is preferable to a numeric setting at
all).

## 6. Invariant / contract impacts

- **Coin icons right of number**: total-cost-at-generation and any Reuse
  total must render via the shared coin renderer (post-WP-21/22), never a
  new private copy - this is the third place (after `MainView`,
  `CraftingPlanView`) that would duplicate the invariant's implementation
  if built independently; sequencing after WP-21/22 avoids that (see 7).
- **No raw ids shown to users**: `PlanHistoryEntry.ItemSummaries` carries
  `ItemId` for internal lookups only; rows render `Name`/`IconUrl`,
  never the id. `NodeId`s are never surfaced in this UI at all (overrides
  aren't replayed - 3.6 - so there's no path where a `NodeId` integer
  would leak into a tooltip or label).
- **ASCII-only source / no em-dash**: applies to the new `.cs` files as
  usual; this document itself is Markdown, not source, so is exempt from
  the ASCII-source rule but has been kept em-dash-free anyway per the
  project's broader style.
- **Blish-free tests**: `PlanHistoryStore`/`PlanHistoryEntry`/dedup-key
  logic must have zero `Blish_HUD`/`Gw2Sharp` references so
  `PlanHistoryStoreTests` stays in the Blish-free suite, matching every
  existing `*StoreTests.cs`.
- **M33 relayout contract**: explicitly not engaged (4.1) - Plan History
  uses pattern (A), stays entirely outside `PlanContentHeightMath`/the
  relayout-closure registry's blast radius.
- **Pricing multi-source correctness**: Reuse calls the real pipeline, so
  it inherits whatever multi-source price logic already exists; this
  proposal adds no new pricing code.

## 7. Effort class & dependencies/sequencing

**Overall: L** - justification: a new store + model + tests (S-shaped on
its own), a new view class with real interactive rows (buttons, expand/
collapse, pin/delete) rather than plain label rows (M-shaped), a
`Module.cs` refactor to share the generate delegate across two views
(small but touches wiring both views depend on), a dedup/retention/
cap engine with its own edge cases (bump vs. new entry, pinned-exempt
eviction), and a genuinely researched but still-open fragility question
(3.6) that constrains what "Reuse" is allowed to promise. If scope must be
*cut*, dropping the sparkline sample capture
(3.7) and the V2 cross-tab handoff (3.3) brings the remaining core (store
+ list + view/reuse-inline-summary + settings + tests) down to a solid
**M**.

Sequencing:

- **No M38 work package currently targets Log/Plan History/Crafting
  Ranker/About/Settings** (confirmed by full read of
  `m38-plan/m38-cleanup-plan.md`) - this feature is
  genuinely greenfield relative to the cleanup wave, and can start
  independently of it.
- **Sequence coin rendering after WP-21/22** (`CoinCurrencyRenderer`
  extraction + `MainView` repoint): building Plan History's cost display
  before WP-21/22 lands would create a *third* independent coin-rendering
  implementation, which is exactly the duplication WP-21/22 exists to
  eliminate. If Plan History must ship first for scheduling reasons,
  coordinate directly with whoever owns WP-21 so its extraction absorbs
  Plan History's usage too, rather than the reverse.
- **Adopt WP-16's onError-callback shape from inception** (not a hard
  dependency - `PlanHistoryStore` can be built before or after WP-16
  lands, but must match the `Action<string, Exception> onError = null`
  shape WP-16 is standardizing across the other four stores, so it isn't a
  fifth store retrofitted later).
- **V2's cross-tab handoff (3.3) must wait until after WP-04, WP-21,
  WP-23, WP-24, WP-25, WP-26 all land** - adding a new public entry point
  to `CraftingPlanView.cs` while six separate packages are actively
  decomposing that same file guarantees merge conflicts on every one of
  them. V1 (this proposal's core scope) needs none of those waves to be
  done first, since it makes zero edits to that file.
- **No dependency on WP-17** for v1 (no `FrameTicker` used); a future
  ticker-based gravy feature would need to register through whatever
  `Module.Unload()` path WP-17 centralizes.
- **No dependency on `m37-homestead`/`m37-audit-fixes`** in-flight
  branches - this feature touches none of `PlanSolver`, the pipeline's
  internals, or the stores those branches modify (`VendorOfferStore` is
  read-only prior art here, not edited).

## 8. Open questions

1. Is the V1 "reuse renders an inline read-only summary in Plan History
   itself" acceptable, or is jumping straight to "reopen the interactive
   Crafting Plan tab" (V2, gated on M38's view-decomposition waves
   finishing first) a hard requirement? This materially changes both
   effort class and start date.
2. **Needs a codebase check this proposal could not complete**: does
   `OwnMaterialsMode`/`HomesteadTiers` affect only which *decision*
   (Craft/Buy) a fixed node gets, or also *which recipe-option nodes
   exist at all* in the raw pre-reduction tree? This determines how much
   the settings-drift NodeId-stability risk (3.6, point 2) actually
   matters in practice.
3. Given the NodeId-stability findings (3.6): is "IgnoredItemIds replay
   only, per-node overrides shown as a count but never replayed" an
   acceptable v1 answer, or should the larger structural-fingerprint-guard
   engineering be built now to attempt safe override replay?
4. What should `PlanHistoryMaxEntries` default to, and should it be a cap
   at all vs. "unlimited until the user manually clears" (mirrors an open
   question the Log-tab retention design will independently need to
   answer for its own store)?
5. Confirm auto-capture-on-every-Generate (recommended, 3.4) versus an
   explicit "Save to History" button - a genuine UX-feel call, not just
   an implementation detail.
6. Confirm the dedup key's strictness (3.5): should a `PriceBasis`/
   `HomesteadTiers`/`ValueOwnMaterials` change count as "a different
   request" (this proposal's default) or should dedup ignore those and
   key only on `RequestedItems`+`IgnoredItemIds`?
7. Sparkline gravy (3.7): capture sample data now but defer rendering
   (recommended), skip both for v1, or is a small chart worth building
   now despite being new UI infrastructure with zero precedent in this
   codebase?
8. Tab ordering: `Tab.OrderPriority`'s default/tie-break behavior is
   unproven from code - once Plan History stops being a placeholder, should
   it be pinned to an explicit `OrderPriority` rather than relying on
   whatever the untested default tie-break produces?
