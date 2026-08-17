# D4 - Crafting Ranker tab: closeness ranking + priority watchlist

Status: design proposal (no code changes). Written against `master` at
commit `d62b7ed` (MEASURED, `git log -1` at proposal time - master is
advancing concurrently under the M38 wave; this is a read-only reference
point, not a claim that master is frozen). Repo accessed strictly
read-only for this proposal; nothing under `/mnt/c/Dev/Blish/wt-m38-*` was
touched.

Epistemic tags used throughout: **MEASURED** = read directly from code (or
a cited doc/perf report) in this session; **INFERRED** = a reasoned
conclusion this proposal draws from MEASURED facts, not itself directly
observed; **GUESS** = a judgment call with no code evidence either way.

---

## 0. Scope boundary vs. D5 (read this first)

A sibling proposal, `d5-next-step-feasibility.md`, already exists in this
folder and also targets the Crafting Ranker tab. It is a feasibility study
for a **different, larger** ask - the user's separate "tell me what to do
next" directive (buy/craft/farm guidance). D5's own scope line says its
target is "the Crafting Ranker tab... currently a placeholder," and it
sketches a `RankerStore` holding an ordered `List<PlanRequestItem>`
priority list, plus a `NextActionClassifier` action-bucketing engine
("Do Next" section), with its own effort classes and sequencing.

**This proposal (D4) is the two-fold ask the brief actually assigns it**:
closeness-to-completion ranking + priority ordering of a watchlist. It is
the **foundation** D5's "Do Next" layer would sit on top of, not a
competing design. Concretely:

- D4 owns: the watchlist (add/remove/quantity), the persisted priority
  order, and the per-item **closeness metric** (coin/currency/time-gate
  remaining vs. total). This is what renders when the Ranker tab is
  opened.
- D5 owns: given a plan and the current wallet/inventory, which specific
  actions ("buy this now," "craft this now," "blocked, short by X") are
  ready right now. This is a deeper drill-in on one item's plan, gated on
  its own feasibility tiers (D5 rates Tier 1 HIGH/buildable, Tier 2
  MEDIUM/lower-bound-only, Tier 3 LOW/out-of-scope-forever).
- Per this proposal's own brief (point 5): design the **seam** to D5's
  engine, do not build it. Section 6 below does exactly that, and points
  at D5's Section 3.2 (greedy-by-priority scheduling) rather than
  re-deriving it.
- **Reconciliation requirement, flagged as an open question (14.1):** D4
  and D5 must not ship two independent stores for the same ordered
  target-item list. This proposal adopts D5's `RankerStore` naming/shape
  (ordered `List<PlanRequestItem>` + per-entry display metadata, atomic
  write, `onError` callback per WP-16) as the canonical persistence and
  extends it only with the fields D4 needs (Section 3.4) - it does not
  invent a second `RankerWatchlistStore`.

---

## 1. Problem / intent

User directive (verbatim): *"the idea of the crafting ranker for me was
two-fold: one, to be able to see, of all the items i'm interested in,
which am i closest to completion on? and secondarily, to be able to set
the priority order of multiple items so that you are working to complete
them in your own priority order."*

Today (MEASURED): "Crafting Ranker" is tab #5 in `Module.cs`'s tab strip
(icon 156686), registered as `Module.BuildPlaceholder` - a single grey
"Coming Soon" `Label`, no view class, no store, no service. Nothing in the
repo computes a "percent complete" or "closest to completion" figure for
any item; the solver only ever produces pass/fail `CraftingDecision`
values per node, never a continuous completion metric. No store holds a
user-curated list of "items I'm interested in," and no view offers
add/remove/reorder over such a list.

---

## 2. The CLOSENESS metric

### 2.1 Candidates considered

| Candidate | What it measures | Problem |
|---|---|---|
| **Coin-denominated remaining/total** (recommended) | `1 - (remaining TotalCoinCost / from-scratch TotalCoinCost)`, both from real solves | Silent when currencies/time-gates dominate cost (2.4) |
| Item-count-based (e.g. "38 of 50 Mystic Clovers gathered") | Raw ingredient counts owned vs. needed | No single number for a whole tree with dozens of distinct ingredients at different depths/prices; a legendary's "count" is not one number without picking one bottleneck ingredient arbitrarily |
| Currency-inclusive single blended score | Coin + currencies converted to one number via `CurrencyValuation` | `CurrencyValuation` is a **user-guessed** coin-per-unit rate for karma/laurels/etc. (MEASURED: `ModuleSettings.CurrencyValuationsJson`, a hand-entered JSON string) - blending it into the *headline* ranking number means the ranking silently depends on a guess the user may not have filled in (defaults to `CurrencyValuation.None`, i.e. currencies valued at 0). That would make an item requiring 500 unvalued Provisioner Tokens read as "cheap," which is the exact kind of currency-comparison invariant violation the repo's Data & APIs rules warn against ("avoid invalid currency comparisons"). |

**Recommendation: coin-denominated remaining/total, computed by the real
solver via existing owned-materials reduction - not a new math model.**
This is the one candidate that reuses production code end-to-end rather
than inventing a parallel scoring formula, and it directly answers "which
am I closest to completion on" for the dominant case (a legendary whose
cost is overwhelmingly coin-priceable materials).

### 2.2 How it is computed (MEASURED, real production code path)

`CraftingPlanPipeline.GenerateStructuredAsync(int targetItemId, int
quantity, AccountSnapshot snapshot, CancellationToken ct, ...,
OwnMaterialsMode ownMaterialsMode = Free, ...)` (`Services/
CraftingPlanPipeline.cs:212`) already does exactly the "solve with the
snapshot and compare to solving without it" mechanism the brief asks for,
with zero new pipeline code:

- **Passing `snapshot: null`** skips inventory reduction entirely
  (MEASURED, `CraftingPlanPipeline.cs:335`: `if (snapshot != null &&
  _reducer != null)` gates Step 6 - a null snapshot means the tree
  solves exactly as if the player owned nothing). This gives the
  **from-scratch baseline cost** for that item/quantity at current TP
  prices.
- **Passing the real `_currentSnapshot`** runs `InventoryReducer` first,
  consuming owned units at zero cost before solving what remains. This
  gives the **remaining-to-finish cost**.
- `OwnMaterialsMode` is documented (MEASURED, `Models/
  OwnMaterialsMode.cs`) to be **inert whenever no snapshot drives
  reduction** - so the baseline call's mode argument doesn't matter.
  For the owned-snapshot call, this proposal recommends reusing
  `ModuleSettings.GetOwnMaterialsMode()` - the exact same setting the
  Crafting Plan tab already reads - rather than introducing a second,
  possibly-conflicting own-materials policy just for the Ranker. One
  global setting, one meaning, everywhere it's used.

So per watchlist item, closeness needs **two calls** to the existing,
unmodified `GenerateStructuredAsync` entry point (or its `IReadOnlyList
<PlanRequestItem>` overload for a 1-item list - both short-circuit to the
identical single-item code path per the M35 doc comment, MEASURED):

```
baseline = await pipeline.GenerateStructuredAsync(itemId, qty, snapshot: null, ...);
owned    = await pipeline.GenerateStructuredAsync(itemId, qty, snapshot: currentSnapshot, ..., ownMaterialsMode: settings.GetOwnMaterialsMode());

coinCloseness = baseline.Plan.TotalCoinCost == 0
    ? (currency-only item - see 2.4)
    : Clamp01(1 - (double)owned.Plan.TotalCoinCost / baseline.Plan.TotalCoinCost);
```

### 2.3 Displaying the components, not just the ratio

The brief explicitly asks for the metric's components to be visible, not
just a single percentage. Per watchlist row:

- **Primary**: the coin-closeness percentage (2.2), rendered as a label
  (see 9.4 for why not a literal progress-bar control).
- **Remaining coin**: `owned.Plan.TotalCoinCost`, rendered gold/silver/
  copper via the shared coin renderer (coin-right-of-number invariant,
  Section 11).
- **Remaining currencies**: `owned.Plan.CurrencyCosts` (`List<CurrencyCost>
  {CurrencyId, Amount}`, MEASURED `Models/CraftingPlan.cs`), one line per
  currency, using `owned.CurrencyMetadata` for name/icon - **never**
  summed with coin or converted to a blended number (repo invariant:
  "avoid invalid currency comparisons"). Each currency line can itself
  show a component ratio against `baseline.Plan.CurrencyCosts` for that
  same currency id (see 2.4) if the maintainer wants per-currency progress
  too - optional, not required for v1.
- **Remaining time-gated units**: `owned.Plan.TimegatedItems`
  (`List<TimegatedItem>{ItemId, CapType, CapValue, NeededCount}`,
  MEASURED `Models/TimegatedItem.cs`) rendered as an informational note
  ("Obsidian Shard: needs 186, capped at 180/day - this alone takes at
  least 2 days"), never folded into the percentage - `TimegatedItem`'s own
  doc comment is explicit that a cap "never gates offer eligibility... is
  surfaced purely as a notice," and the closeness metric should honor
  that same posture.

### 2.4 Being honest when currencies/time-gates dominate

This is the brief's explicit ask (point 1's last clause), and it is a
real correctness issue, not a nice-to-have:

- **Currency-only items** (`baseline.Plan.TotalCoinCost == 0` but
  `baseline.Plan.CurrencyCosts` non-empty, e.g. a pure Ascended-shard/
  currency vendor purchase): the coin ratio is undefined (0/0), not
  "100% done." The row must show the percentage cell as **"N/A (currency
  cost)"** and let the remaining-currency breakdown (2.3) carry the
  honest signal, rather than silently defaulting to a misleading 100%.
- **Currency-dominated items** (coin cost near zero, but a large
  unpriceable currency requirement): the coin-closeness number can read
  as "nearly done" while the item is functionally far away. The tab
  should carry a persistent, non-dismissable caption under the list (not
  per-row noise) stating this plainly, e.g. *"Percentage reflects
  Trading-Post-priceable cost only. Items paid mostly in account
  currencies or time-gated purchases may show high percentages while
  still requiring significant currency or time to finish - check the
  currency and time-gate lines below each item."* This is the honesty
  the brief asks for, delivered as a standing disclosure rather than a
  per-item guess at blending currencies into one number (2.1 already
  rejected blending as the primary metric for exactly this reason).
- **Monotonicity is asserted, not proven, and needs a guard.** Owning
  materials should only ever reduce or match a from-scratch cost, never
  exceed it - `InventoryReducer` only removes need, and the
  `OwnMaterialsMode.Valued` force-buy pre-pass changes *which* nodes are
  bought vs. crafted for cost-efficiency, not whether inventory helps.
  This proposal did **not** trace `PlanSolver.FinalizeVendorBatches`/
  `EvaluateVendorOffers` far enough this session to prove
  `owned.Plan.TotalCoinCost <= baseline.Plan.TotalCoinCost` holds in every
  edge case (e.g. a pathological vendor-batch rounding interaction). The
  ratio must be `Clamp01`-guarded defensively (already shown in 2.2), and
  implementation should add a unit test asserting the inequality holds
  across representative fixtures (own-materials + vendor-batch tests
  already exist in `PlanSolverTests`/`MultiItemPlanTests` to model this
  on) - flagged as a Must-Fix-shaped implementation task, not asserted as
  fact here.

### 2.5 Optional secondary signal: can you afford it right now (Nice to have)

`AccountSnapshot.CoinCopper` (MEASURED, `Models/AccountSnapshot.cs`) is
the player's actual on-hand coin, already fetched by the existing
snapshot flow and unrelated to the solve. Comparing it to
`owned.Plan.TotalCoinCost` is free (no extra solve, no new fetch) and
answers a genuinely different question than material-closeness: "I have
0% of the crafting materials, but I have enough gold sitting in my wallet
to just buy everything remaining today." Recommendation: show this as a
small secondary chip ("Affordable now" / "Short Xg") next to the
percentage, clearly visually distinct from the primary closeness metric so
the two senses of "closest to completion" (material progress vs. can-
afford-the-rest) are never conflated into one number.

---

## 3. The WATCHLIST model

### 3.1 What "watchlist" means here

A user-curated, persisted, ordered list of `{ItemId, Quantity}` pairs -
"all the items I'm interested in." This is structurally identical to
`Models/PlanRequestItem.cs` (`{ItemId, Quantity}`, MEASURED, already used
by the multi-item Crafting Plan entry point) and to D5's own sketch of an
ordered `List<PlanRequestItem>`. Reusing `PlanRequestItem` directly (no new
item/quantity type) is the smallest-surface choice and keeps the ranker's
persisted shape trivially convertible into the exact type
`GenerateStructuredAsync`'s list overload already accepts.

### 3.2 Adding an item - reuse the existing search UI verbatim

`Views/SuggestionPanel.cs` + `Views/AutocompleteTextBox.cs` +
`Contracts/IItemSearchProvider.cs` (MEASURED, full read this session) are
already a clean, decoupled, event-based "search for a plan-valid item"
control: `SuggestionPanel` wraps an `AutocompleteTextBox`, calls
`IItemSearchProvider.SearchAsync(query, maxResults, ct)`, and raises
`ItemSelected(ItemId, Name, IconUrl)`. This is exactly the control
`CraftingPlanView`'s own item rows already use, and `Module.cs` already
constructs and injects a single `_itemSearchProvider` instance
(`ItemSearchProviderFactory.Create(...)` or `StaticItemSearchProvider`
fallback, MEASURED `Module.cs:204-215`).

**Ranker's "Add" row**: one `AutocompleteTextBox` + `SuggestionPanel`
(same provider instance, injected the same way `CraftingPlanView` gets it)
+ a quantity `TextBox` + an "Add to Ranker" button. On `ItemSelected` +
quantity parse (same "invalid/blank/<1 silently corrected to 1" rule
`ItemRowRequestBuilder.Build` already uses, MEASURED, for consistency),
append `{ItemId, Quantity}` to the end of the persisted list (lowest
priority by default - matches "add to the bottom of my list" as the least
surprising default) and persist immediately.

**Duplicate add**: if the selected `ItemId` is already on the watchlist,
this proposal recommends **updating that entry's quantity in place**
(and leaving its priority position unchanged) rather than creating a
second row for the same item - flagged as an explicit open question
(14.3) since "replace" vs. "prompt the user" vs. "allow duplicates with
different quantities" are all defensible and the brief doesn't specify.

### 3.3 Removing an item

A per-row "Remove" button, always visible (unlike the Crafting Plan
input strip's `ItemRowRequestBuilder.CanRemoveRow`, which hides Remove
below 2 rows because that strip must always have at least one row to
generate a plan from - the Ranker watchlist has no such floor; zero
watched items is a valid, first-run state). Remove deletes the entry and
re-persists; no confirmation dialog (mirrors the low-friction Remove
already used in the Crafting Plan input strip, MEASURED - no modal there
either).

### 3.4 Entry shape and persistence

Per the reconciliation in Section 0, this adopts D5's `RankerStore` shape
and fleshes it out:

```
RankerWatchlistEntry
{
    int    ItemId
    int    Quantity

    // Denormalized display fields, same pattern as SnapshotItemEntry's
    // own Name/IconUrl duplication (MEASURED) - avoids an ItemMetadataService
    // round-trip just to render the row list before any solve has run.
    string Name
    string IconUrl
}
```

List **order is the priority order** (index 0 = highest priority) - no
separate `PriorityOrder` int field. This mirrors the existing
`CraftingPlanView._itemRows`/`ItemRowRequestBuilder` convention where row
list-order already carries meaning (MEASURED), and avoids a field that
could drift out of sync with the list's actual order.

Store: `Services/RankerStore.cs` (name reconciled with D5), same shape as
`Services/StatusStore.cs`/`Services/VendorOfferStore.cs`'s atomic-write
idiom - **not** `SnapshotStore.cs`'s idiom. **Correction to the scout
notes, MEASURED this session (matching an identical correction already
made in `d3-plan-history.md`):** `SnapshotStore.Save` uses a plain
`File.WriteAllText`, no tmp file, no atomic replace at all;
`StatusStore.Save` genuinely does `.tmp` + `File.Copy(..., overwrite:
true)` + `File.Delete`. `RankerStore` should follow `StatusStore`'s
proven shape. Constructor/save method takes an `Action<string, Exception>
onError = null` parameter from day one (WP-16's planned shape for the
other four stores - Section 13) so this is not a fifth/sixth store WP-16
has to retrofit later.

File: `data/ranker_watchlist.json` (name flagged as an open question -
Section 14.1 - since D5 independently guessed `data/ranker.json`; these
must converge on one filename since they are meant to be one store).
Newtonsoft `JsonConvert.SerializeObject`/`DeserializeObject`, indented,
matching every other store in the repo.

Test: `tests/GW2CraftingHelper.Tests/Services/RankerStoreTests.cs`, real
temp-dir file IO, Blish-free, following `VendorOfferStoreTests.cs`'s exact
template (MEASURED) - covering save/load round-trip, add-updates-existing
(3.2), remove, reorder persistence, and the `onError` callback firing on a
forced-failure path.

---

## 4. Priority ORDERING UX

### 4.1 What control support actually exists (MEASURED, not assumed)

Before choosing drag vs. buttons, this proposal checked what Blish HUD
actually exposes:

- The shipped `Blish HUD.xml` doc (`packages/BlishHUD.1.3.0/lib/net472/
  Blish HUD.xml`) documents only a small subset of `Blish_HUD.Controls`
  types (`CaptureType`, `Container`, `ContextMenuStrip`, `DetailsButton`,
  `Dropdown`, `Panel`, `StandardWindow`, `Tab`, `TabCollection`,
  `TabbedWindow2`, `TextInputBase`, `Thickness`, `ViewContainer`, a
  handful more) - **no** type with "Drag," "Reorder," "Sortable," or
  similar in its name appears anywhere in that doc.
- `strings` on the shipped `Blish HUD.exe` surfaces a bare `DragStart`
  token somewhere in the assembly, but with no accompanying type/method
  context recoverable this way - **INFERRED, not proven**, that this is
  almost certainly the window title-bar drag-to-move mechanism
  (`StandardWindow`/`TabbedWindow2` already support dragging the whole
  window), not a list-row reorder primitive. No `ProgressBar`, `Meter`,
  or `Bar` control type was found either (relevant to 9.4).
- Grepping this repo's own `Views/*.cs` for any existing drag/reorder
  code (`MoveUp`, `MoveDown`, `Reorder`, `DragHandle`) returns nothing.
  No view in this codebase has ever implemented row reordering.
- `docs/KNOWN-ISSUES.md`'s M35 section (item 21.2, MEASURED) explicitly
  records that gw2efficiency's own `moveRecipe` up/down-arrow reordering
  was **deliberately left unimplemented** for the multi-item input strip
  - "out of scope for this milestone," listed again in the DEFERRED
  tail ("Multi-item row reordering (gw2e moveRecipe): out of scope per
  M35"). This is a **scope decision from that milestone**, not a
  feasibility finding against the up/down-arrow idiom itself - gw2e's own
  UI uses exactly that idiom, and nothing in the KNOWN-ISSUES entry
  suggests it was hard to build, only that it wasn't prioritized then.

**Conclusion: no drag-and-drop reordering primitive is confirmed to exist
anywhere in this framework's documented surface or this codebase's prior
art. Up/down buttons are the only idiom with any precedent (gw2e's own
UI, and this repo's own prior *decision* to defer exactly that idiom -
not a decision against it, just a deferred one) and zero unproven
framework risk.**

### 4.2 Recommended UX

Two small buttons per row - up-triangle and down-triangle glyphs, written
in source as the ASCII-escaped literals `"▲"`/`"▼"` per the
project's Unicode convention (matching the CLAUDE.md example escapes),
built from whatever button
control the codebase already uses (`StandardButton`/`GlowButton`,
MEASURED present in the shipped exe's type strings and already used
elsewhere in `Views/`). Clicking Move-Up/Move-Down calls a new, pure,
Blish-free static helper:

```
public static class RankerPriorityOrdering
{
    public static void MoveUp(List<RankerWatchlistEntry> entries, int index) { /* swap index, index-1 */ }
    public static void MoveDown(List<RankerWatchlistEntry> entries, int index) { /* swap index, index+1 */ }
}
```

- mirrors `ItemRowRequestBuilder`'s existing shape (pure list-state
  transitions, unit-testable with no Blish reference) rather than
  inventing a new style of helper.
- The top row's Move-Up button and the bottom row's Move-Down button are
  disabled (not hidden - avoids layout jump) - same idiom as
  `ItemRowRequestBuilder.CanRemoveRow`'s row-count gate, applied to
  position instead of count.
- After a move, save immediately and rebuild the row `FlowPanel` (dispose
  + recreate rows) - the lightweight pattern (A) idiom `LogTabContent.
  Refresh()` already uses (MEASURED), not the M33 relayout-closure
  registry. A watchlist of a few dozen simple rows has no live-resize
  reflow need that would justify the heavier contract.

**Drag-and-drop is flagged as a stretch enhancement, not recommended for
v1.** If the maintainer wants it, it needs its own feasibility spike
first (confirm whether `Container` exposes mouse-drag events usable for
free-position or list-swap dragging without breaking the ViewAdapter's
documented "don't nest two scrollable panels, hit-testing breaks" caution
- an analogous hit-testing risk to the one already documented for nested
scroll panels) - not something this proposal can sign off on from a
`strings` grep alone.

---

## 5. REFRESH cost

### 5.1 Explicit button, never automatic (per the brief)

A single "Refresh" button atop the tab, matching the brief's explicit
instruction. No per-frame recompute, no auto-refresh on tab-open, no
polling `FrameTicker`. This also sidesteps WP-17's FrameTicker-teardown
concern entirely (Section 13) - there is nothing to tear down.

### 5.2 Cost model (MEASURED baseline + INFERRED extrapolation, explicitly labeled)

Each watchlist item needs **up to two** `GenerateStructuredAsync` calls
(baseline + owned, Section 2.2) - N watched items -> up to 2N solves per
Refresh click. Per-solve cost, MEASURED from `m38-plan/m38-a3-perf.md`'s
own harness numbers (Exordium, item 90551, `--profile 2 --iterations 6`):

| Condition | Total |
|---|---|
| Cold (1st solve of a fresh item, empty caches) | 116-129ms |
| Warm (repeat solve of the same item, caches populated) | 35ms median |

These numbers are for **one specific item** (Exordium) and are explicitly
flagged in that report as the benchmark reference tree, not a ceiling - a
deep legendary-precursor tree is very plausibly larger/slower and this
proposal has **not measured one**. Before shipping, the maintainer should
run `GW2CraftingHelper.Harness.exe --profile <a legendary> --iterations 6`
(same tool WP-07 already uses for its own before/after proof) against a
representative watchlist target, not assume Exordium generalizes.

**Why 2N is more tolerable than it sounds (MEASURED, real caching
infrastructure already in place, provided the Ranker reuses existing
singletons rather than constructing its own):**

- `RecipeService` caches recipe/search lookups in-memory
  (`_recipeCache`/`_searchCache`, MEASURED `Services/RecipeService.cs`),
  backed by a persistent `IRecipeCacheStore` seed - a repeat solve of the
  same item (the *second* of the pair, or any later Refresh click) skips
  most of the "Build recipe tree" cold-phase cost.
- `TradingPostService` caches prices with an explicit 15-minute TTL and
  in-flight-request dedup (MEASURED `Services/TradingPostService.cs`,
  `CacheTtl`/`_inFlight`) - the baseline and owned solves for the *same*
  item need the *same* prices, so the second call of the pair is
  effectively free on the pricing side.
- **This benefit is only real if the Ranker is wired against the same
  singleton `_craftingPipeline` `Module.cs` already constructs** (MEASURED
  `Module.cs:257-267`), the same way `CraftingPlanView` is - not a
  second, independently-constructed pipeline with cold caches of its own.
  This is stated explicitly in Section 9.1's wiring recommendation.

**WP-07 (not yet landed - MEASURED via `git log`, current master
predates any M38 package) targets exactly the dominant warm-run cost**:
"Build result" is 51.4% of a warm run today (18ms of 35ms median,
MEASURED `m38-a3-perf.md`). When WP-07 lands, the Ranker benefits
passively with zero Ranker-side changes - it is calling the same public
`GenerateStructuredAsync` entry point WP-07's memoization sits behind.
This proposal does not depend on WP-07 landing first (Section 13).

### 5.3 Orchestration: sequential, not parallel, for v1

Recommendation: a plain sequential `await`-in-a-loop over the watchlist
during Refresh, not `Task.WhenAll` fan-out. Rationale: the underlying
services (`TradingPostService._cacheLock`, `RecipeService._cacheGate`)
already serialize concurrent access internally (MEASURED locks), so
parallelizing does not obviously buy wall-clock time proportional to N -
it has not been measured to help, and sequential is simpler to reason
about for the generation-guard below. Flagged as an open question (14.5)
if the maintainer wants parallel fan-out measured as a follow-up.

### 5.4 Threading and cancellation

- Refresh runs as a plain `async Task` (no `FrameTicker`, no
  `SynchronizationContext` assumption - matches every other async call
  site in this module, MEASURED). Reports progress via the same
  `IProgress<PlanStatus>`-shaped callback `GenerateStructuredAsync`
  already accepts, rendered as a status label ("Refreshing 3 of 12...") -
  reusing an existing idiom rather than inventing a spinner control.
- A generation-guard integer (the same `myGen` pattern
  `CraftingPlanPipeline`'s own callers already use, MEASURED pattern
  present in this codebase) discards a stale in-flight Refresh's results
  if the user clicks Refresh again before it finishes, or if the tab/
  module is torn down mid-run.
- A `CancellationTokenSource` created per Refresh click, cancelled by a
  second click (turns the button into "Refreshing... (click to cancel)")
  and by `Module.Unload()` - the same lifecycle every other cancellable
  operation in this module already has, no new pattern.
- Results are marshaled back for UI application using whatever mechanism
  `Module.cs`'s existing `GenerateStructuredAsync`-calling lambda already
  relies on (`MainThreadMarshal`, MEASURED present in `Views/
  MainThreadMarshal.cs`) - no new cross-thread primitive introduced.

---

## 6. Seam to the future "what-to-do-next" engine

Per the brief: design the seam, do not build the engine. D5
(`d5-next-step-feasibility.md`) already did the hard scheduling-semantics
thinking here - Section 3 of that document works through shared-material/
shared-cap scheduling in detail and lands on a specific, well-reasoned
recommendation: **greedy-by-priority** (pour all available capped
throughput into priority-1 until it is satisfied, then priority-2, etc.),
explicitly rejecting a global balanced optimizer as unnecessary
complexity for a worse default outcome (D5 Section 3.2). This proposal
does not re-derive that - it adopts it as the answer to "how do
priorities feed the engine."

What D4 contributes to that seam, concretely:

1. **The persisted, stable order itself** (Section 3.4) - list index is
   priority; D5's greedy scheduler needs exactly this signal and nothing
   more from D4 to decide "which item first."
2. **An ephemeral, per-item last-Refresh result** the Ranker already has
   in hand after Section 5's refresh: `{ItemId, owned CraftingPlanResult}`
   for every watched item. This is not proposed to be *persisted* (it
   would go stale the moment TP prices move, same staleness argument
   `d3-plan-history.md` makes against persisting full results) but it is
   available in-memory for a same-session "Do Next" consumer to read
   without re-solving. A future `NextActionClassifier` (D5's proposed
   name) could take this dictionary directly rather than D5 having to
   call the pipeline a third time.

**Explicit non-goals for D4**: no action-bucketing (Ready/Blocked/
Time-gated), no vendor-cap-consumption tracking, no wallet-affordability
classification beyond the single optional chip in Section 2.5, no
scheduling math. All of that is D5's territory, gated on D5's own
feasibility tiers and open questions.

---

## 7. Shared-materials honesty

### 7.1 v1: independent per-item solves (recommended)

Each watchlist item is solved **on its own** via the single-item
`GenerateStructuredAsync(itemId, quantity, ...)` overload (Section 2.2) -
not the multi-item list overload. This is the "independent" option the
brief poses, and it is what this proposal recommends for v1, for a
concrete reason grounded in what the pipeline can actually report:

**`CraftingPlanResult` has no per-root cost breakdown for a joint/multi-
item solve today (MEASURED, from the model's own doc comments,
`Models/CraftingPlanResult.cs`).** `SellableQuantity`/`NetSaleValue`/
`CraftingProfit` are each explicitly documented as "the SUM across every
requested root" for a multi-item batch; `Plan.TotalCoinCost`/
`Plan.CurrencyCosts` are the batch's merged totals. `MultiItemRoots`
gives you each root's own `CraftingTreeNode` (the tree shape/decisions),
but there is no field anywhere that says "this specific root's share of
the merged cost after shared-material dedup." Computing per-item
closeness from a joint solve would require **new pipeline output**, not
just new UI - out of scope for a UI-only Ranker proposal, and explicitly
flagged as a separate future item below.

### 7.2 What independent solving gets wrong, honestly stated

Two watchlist legendaries that both need 38 Mystic Clovers will each
report "need 38 Mystic Clovers, X% remaining" **as if the other did not
exist** - the two independent solves do not know about each other, so
neither discounts the other's demand on the shared material, and if both
are shown as "100 more clovers needed" it double-counts the true combined
need (which the M35 multi-item wrapper, used by the Crafting Plan tab
today for a *simultaneous* multi-item request, already solves correctly
via merged-ceil vendor batching - MEASURED, `docs/KNOWN-ISSUES.md` #21.1).
This is the exact asymmetry the brief's point 6 asks to be surfaced, not
hidden.

**UI mitigation**: a standing caption under the list (can share space with
2.4's currency/time-gate caption or sit just below it): *"Each item's
percentage is calculated as if you were crafting it alone. Crafting
several ranked items together may cost less overall than the sum of these
percentages suggests, because they can share materials - see the Crafting
Plan tab's multi-item entry for the combined cost."* This both discloses
the limitation and actively points the user at the one place in the
module that already computes the joint number correctly (the Crafting
Plan tab's existing multi-item input strip) - reusing what exists instead
of promising a joint Ranker mode this proposal does not build.

### 7.3 The joint/merged variant - flagged, not built

A "joint closeness" mode - solving the entire watchlist as one
`GenerateStructuredAsync(IReadOnlyList<PlanRequestItem>, ...)` batch and
somehow attributing a per-root remaining-cost share - is the Ranker's
direct analog of the M35 multi-item wrapper, exactly as the brief's point
6 names it. It cannot be built as a v1 UI feature because (7.1) the
pipeline does not expose a per-root cost split within a joint solve.
Building it would mean extending `PlanResultBuilder`/the solve pipeline
with a new "attribute merged-batch cost back to each contributing root"
computation - a real, separate, pipeline-level feature, logged here as an
explicit open question (14.4) for the maintainer, not something this
proposal's effort estimate (Section 12) includes.

---

## 8. Proposed UX (tab layout)

`Views/RankerTabContent.cs` (new, same `Build(Container)` entry-point
shape `LogTabContent`/`SettingsTabContent` already use, MEASURED, wired
into `Module.cs`'s tab #5 in place of `BuildPlaceholder`):

- **Header row**: tab title/status line + a **Refresh** button (Section
  5) + a small "as of &lt;last refresh time&gt;" caption once at least one
  refresh has run (mirrors the Snapshot tab's own `CapturedAt` disclosure
  idiom, MEASURED pattern reused, not invented).
- **Add row**: search box + `SuggestionPanel` (Section 3.2) + quantity
  box + "Add to Ranker" button.
- **List** (`FlowPanel(SingleTopToBottom, CanScroll=true)` - pattern (A),
  Section 9.3): one row per watchlist entry, priority-ordered top to
  bottom, each row showing:
  - icon + name + quantity (never the raw item id, invariant Section 11)
  - Move-Up / Move-Down buttons (Section 4.2)
  - Remove button (Section 3.3)
  - closeness percentage cell (or "N/A (currency cost)," Section 2.4) +
    remaining coin (coin-right-of-number, shared renderer) + remaining
    currency lines + time-gated note if any (Section 2.3) + the optional
    "Affordable now / Short Xg" chip (Section 2.5)
  - Before the first Refresh, the metric cells show a neutral placeholder
    ("Not yet calculated - click Refresh") rather than a stale or zero
    value - avoids the empty-state trap of reading "0%" as "0% owned"
    when it actually means "never solved."
- **Standing captions** (Sections 2.4 and 7.2) below the list, always
  visible once the list is non-empty.
- **Empty state** (no watchlist entries at all): centered label, same
  idiom as `LogTabContent`'s implicit "no lines" case - *"No items on
  your list yet. Search above and add the items you're working toward."*
- **No-snapshot state**: if `_currentSnapshot` is null (no permissions
  granted yet, or never fetched - MEASURED this is a real, reachable
  state per `Gw2AccountSnapshotService.HasRequiredPermissions()`), a
  banner above the list: *"No account snapshot available - percentages
  will show 0% (nothing owned) until you fetch one from the Snapshot
  tab."* This is honest rather than silently degraded: with a null
  snapshot, the "owned" solve call in Section 2.2 degenerates to being
  identical to the baseline call (both effectively unreduced), so every
  item legitimately shows 0% - correct behavior, but confusing without
  the explanation.

---

## 9. Data & architecture

### 9.1 Reused (no changes needed)

- `CraftingPlanPipeline.GenerateStructuredAsync` (`Services/
  CraftingPlanPipeline.cs:212`, single-item overload) - called twice per
  watchlist item per Refresh (Section 2.2). **Wired against the same
  singleton `_craftingPipeline` instance `Module.cs` already constructs
  and hands to `CraftingPlanView`** (MEASURED `Module.cs:257-267`,
  `298-345`) - not a second pipeline instance - so `RecipeService`/
  `TradingPostService` caches stay warm across both the Crafting Plan tab
  and the Ranker (Section 5.2).
- `ModuleSettings.GetOwnMaterialsMode()` / `GetCurrencyValuation()` /
  `GetHomesteadEfficiencyTiers()` (MEASURED, `Services/ModuleSettings.cs`)
  - reused as-is for the owned solve, so the Ranker's numbers agree with
  whatever the Crafting Plan tab would currently produce for the same
  item, rather than a second, independently-configured policy.
- `Views/SuggestionPanel.cs`, `Views/AutocompleteTextBox.cs`,
  `Contracts/IItemSearchProvider.cs`, and the single `_itemSearchProvider`
  instance `Module.cs` already constructs (Section 3.2) - zero new search
  code.
- `Models/PlanRequestItem` - the watchlist entry's `ItemId`/`Quantity`
  shape (Section 3.1/3.4), no new item/quantity type.
- The lightweight pattern (A) `FlowPanel(CanScroll=true)` idiom already
  used by `MainView`/`LogTabContent`/`SettingsTabContent` (MEASURED) -
  explicitly **not** the M33 `PlanContentHeightMath`/relayout-closure
  registry contract, which is `CraftingPlanView`-only.
- The shared `CoinCurrencyRenderer` landing from WP-21/22 for every coin
  cell (Section 11) - **not** a new private coin-rendering copy.
- `MainThreadMarshal` (`Views/MainThreadMarshal.cs`, MEASURED) for
  marshaling Refresh's async results back for UI application - no new
  cross-thread primitive.
- `Module.cs`'s existing `_currentSnapshot` field (read-only from the
  Ranker's perspective) for the owned solve.

### 9.2 New

- `Services/RankerStore.cs` - reconciled name/shape with D5 (Section 0),
  atomic `.tmp`+`File.Copy`+`File.Delete` write matching `StatusStore`/
  `VendorOfferStore` (Section 3.4), `onError` callback from day one
  (WP-16 shape, Section 13).
- `Models/RankerWatchlistEntry.cs` (Section 3.4).
- `Services/RankerPriorityOrdering.cs` - pure `MoveUp`/`MoveDown` static
  helper (Section 4.2), Blish-free, mirrors `ItemRowRequestBuilder`'s
  shape.
- `Services/RankerClosenessCalculator.cs` (or a static method group) -
  pure, Blish-free: takes a baseline `CraftingPlanResult` + an owned
  `CraftingPlanResult` and returns the display numbers of Section 2.2-2.4
  (coin ratio, per-currency remaining, time-gated notes, N/A detection).
  Kept separate from `Views/RankerTabContent.cs` specifically so it is
  unit-testable without any Blish reference, following the same
  separation `ItemRowRequestBuilder`/`PlanViewModelBuilder` already model
  in this codebase.
- `Views/RankerTabContent.cs` - new view class (Section 8), constructor-
  injected with the shared pipeline delegate, `_itemSearchProvider`,
  `ModuleSettings`, `RankerStore`, and a snapshot accessor - the same
  injection shape `CraftingPlanView`/`MainView` already receive from
  `Module.cs`.
- `tests/GW2CraftingHelper.Tests/Services/RankerStoreTests.cs`,
  `RankerPriorityOrderingTests.cs`, `RankerClosenessCalculatorTests.cs` -
  real temp-dir file IO for the store (Blish-free, `VendorOfferStoreTests`
  template), pure-logic tests for the other two (no file IO needed at
  all, they take/return plain models).

### 9.3 Scroll pattern

Pattern (A) only (Section 8/9.1) - a watchlist of icon+label+button rows
has no live-resize reflow requirement that would justify opting into the
M33 heavy contract. This keeps the Ranker entirely outside the WP-21..26
blast radius, same conclusion D3 and D5 both independently reached for
their own tabs.

### 9.4 No native progress-bar control (MEASURED)

Neither the shipped `Blish HUD.exe`'s type-name strings nor its XML doc
contain a `ProgressBar`, `Meter`, or `Bar` control (grepped both this
session). This codebase's own `Views/` never renders one either. Two
low-risk options for the percentage cell, both usable with controls
already confirmed to exist:

1. **Text-only** - a colored `Label` ("73%"), color-coded via a small
   threshold helper (mirrors the existing `RarityColors`-style helper
   pattern landing from WP-21) - lowest risk, zero new visual primitive.
2. **Faked bar** - two stacked `Panel`s with `BackgroundColor` set, the
   foreground one's width scaled to the ratio (`Panel` is MEASURED to
   exist and be used pervasively for chrome throughout `Views/`) - a
   legitimate way to get a visual bar without a dedicated control, at the
   cost of a small amount of new layout math (width-only, no relayout-
   registry involvement needed since it's a simple proportional fill, not
   a reflow-sensitive multi-column row).

Recommendation: start with option 1 (Section 8 already assumes a text
cell); option 2 is a low-risk, easy follow-up polish pass once the tab
exists, not a blocker for v1. Flagged as an open question (14.6) in case
the maintainer has a visual preference up front.

### 9.5 Threading notes

Fully covered in Section 5.4. Summary: synchronous JSON store IO (small
file, same cost profile as every other store); the Refresh flow is the
one genuinely async piece, using the pipeline's existing async/
cancellable shape and the existing `MainThreadMarshal` primitive - no new
threading pattern introduced anywhere in this proposal.

---

## 10. Settings introduced

**None required for v1.** The Ranker deliberately reuses the existing
global `ValueOwnMaterials`/`CurrencyValuationsJson`/`HomesteadFiberTier`
et al. settings (Section 9.1) rather than duplicating a second copy of
the same policy choice under a Ranker-specific setting - one source of
truth for "how should owned materials be valued," used identically by
the Crafting Plan tab and the Ranker.

Possible future settings (explicitly **not** proposed for v1, listed only
because the brief's format asks what settings a feature introduces - the
efficiency principle in the project's own review rules argues against
adding these speculatively):

- A watchlist size cap (`RankerMaxEntries`, int) - only worth adding if
  real usage shows the list growing unreasonably large; no evidence for
  that yet.
- An "auto-refresh on tab open" toggle - explicitly discouraged by the
  brief's own "explicit Refresh button, not per-frame" instruction; not
  recommended even as an opt-in, given the 2N-solve cost model (5.2).

---

## 11. Invariant / contract impacts

- **Coin icons right of number**: every coin cell (remaining cost per
  row) must call the shared `CoinCurrencyRenderer` landing from WP-21/22
  - not a fourth independent encoding of this invariant. (`MainView` and
  `CraftingPlanView` are the two existing copies WP-21/22 targets; D3's
  Plan History proposal already flagged itself as a third; this would be
  a fourth if built independently of that renderer.)
- **No raw ids shown to users**: `RankerWatchlistEntry.ItemId` is
  internal-only, used for solve calls and store dedup; rows render
  `Name`/`IconUrl` only. `TimegatedItem.ItemId` in the time-gate note
  (Section 2.3) is likewise resolved to a display name before rendering,
  never shown raw.
- **ASCII-only source / no em-dash**: applies to every new `.cs` file
  listed in Section 9.2 as usual; this document is Markdown and kept
  em-dash-free anyway per the project's broader style, matching D3/D5.
- **Blish-free tests**: `RankerStore`, `RankerPriorityOrdering`, and
  `RankerClosenessCalculator` all take/return plain models with zero
  `Blish_HUD`/`Gw2Sharp` references, keeping their tests in the Blish-free
  suite alongside every existing `*StoreTests.cs`/pure-logic test.
- **`gw2efficiency` is research-only, never called at runtime**: this
  proposal's web research (Section 15) is dev-time-only, as instructed;
  the shipped feature makes zero network calls to gw2efficiency or the
  wiki - every number comes from this module's own pipeline and the
  official GW2 API paths that pipeline already uses.
- **Pricing logic preserves multi-source correctness**: no new pricing
  code is introduced anywhere in this proposal - both solves per item
  call the identical, unmodified pipeline every other tab already uses.
- **M33 relayout contract**: explicitly not engaged (9.3) - pattern (A)
  only, stays entirely outside `PlanContentHeightMath`/the relayout-
  closure registry's blast radius.

---

## 12. Effort class

**Overall: L.**

Justification (component-by-component, none of which alone would be more
than S/M, but they compound):

- New store + model + tests (`RankerStore`, `RankerWatchlistEntry`,
  `RankerStoreTests`) - **S** on its own, closely follows `StatusStore`/
  `VendorOfferStoreTests` precedent almost mechanically.
- New pure ordering + closeness-math helpers + tests
  (`RankerPriorityOrdering`, `RankerClosenessCalculator`) - **S**, small,
  well-specified, no Blish surface, but the closeness math (Section 2)
  has real edge cases (N/A currency-only items, the unproven-monotonicity
  guard, per-currency lines) that need deliberate test coverage, not
  just a happy-path check.
- New view (`RankerTabContent`) with real interactivity - search-box
  reuse, add/remove, move-up/move-down, per-row multi-cell rendering
  (coin + currencies + time-gate note + affordability chip), multiple
  named empty/degraded states (no watchlist, no snapshot, pre-refresh) -
  **M**, materially more UI surface than `LogTabContent`'s plain label
  rows, though still pattern (A), not the M33 heavy contract.
- Refresh orchestration - up to 2N async solves, sequential loop,
  progress reporting, generation-guard, cancellation on re-click/tab-
  close/unload - **M**-shaped correctness surface (the race conditions a
  half-built version of this would get wrong are exactly the kind of
  thing the project's own Edit->Review->Fix discipline exists to catch),
  even though no new threading *primitive* is introduced.

What keeps this from being **XL**: zero changes to `Views/
CraftingPlanView.cs` or `Services/CraftingPlanPipeline.cs`/`PlanSolver.cs`
(this proposal calls existing, stable public entry points only, Section
13); zero changes to `Views/SettingsTabContent.cs` (Section 10, no new
settings); no new Blish control primitive is required (Section 9.4 keeps
the percentage cell text-only for v1); and the joint/multi-item closeness
variant (Section 7.3) - the one piece that *would* require pipeline-level
work - is explicitly deferred as a separate future item, not part of this
estimate.

If the maintainer wants to cut scope: dropping the optional "Affordable
now" chip (2.5), the per-currency progress sub-lines (2.3's optional
extra), and the faked visual bar (9.4 option 2) brings the remaining core
(store + ordering + closeness math + basic list view + Refresh) down
toward a solid **M**, at the cost of a slightly less rich per-row display.

---

## 13. Dependencies & sequencing

- **No M38 work package currently targets Log/Plan History/Crafting
  Ranker/About/Settings** (confirmed by the full read of `m38-plan/
  m38-cleanup-plan.md` this session) - this feature is genuinely
  greenfield relative to the cleanup wave and can start independently of
  it, same conclusion D3/D5 both reached.
- **Sequence coin-cell rendering after WP-21/22** (`CoinCurrencyRenderer`
  extraction + `MainView` repoint) - building the Ranker's coin cells
  before WP-21/22 lands creates a fourth independent coin-rendering copy,
  exactly what those packages exist to eliminate (Section 11). If the
  Ranker must ship first for scheduling reasons, coordinate with whoever
  owns WP-21 so its extraction absorbs the Ranker's usage too, mirroring
  D3's own stated fallback.
- **Adopt WP-16's `onError`-callback shape from inception** for
  `RankerStore` (not a hard dependency - can be built before or after
  WP-16 lands, but must match the `Action<string, Exception> onError =
  null` shape WP-16 is standardizing across the other stores, so this
  isn't a store retrofitted later).
- **No dependency on `m37-homestead`/`m37-audit-fixes`** in-flight
  branches, and in fact **already moot**: this session's own reads of
  current `master` (`Services/CraftingPlanPipeline.cs`, `Models/
  OwnMaterialsMode.cs`, `Services/ModuleSettings.cs`) show
  `HomesteadEfficiencyTiers`/`OwnMaterialsMode` already present in the
  exact signatures this proposal calls - homestead's relevant surface is
  already on `master` as of this proposal (MEASURED, current session).
- **Low risk, but D5 recommends waiting for Wave C (WP-11/12/13/15) to
  settle before its own "Do Next" layer ships**, reasoning that building
  against a pipeline mid-refactor invites churn. D4's dependency is
  narrower: it only calls the **stable public** `GenerateStructuredAsync`
  signature, which none of WP-11 (private `PlanSolver` struct), WP-12
  (internal pipeline extraction), WP-13 (private helper dedup), or WP-15
  (private `PlanSolver` sub-engine extraction) changes per their own
  scope lines (Section headers in `m38-cleanup-plan.md`, MEASURED - all
  four are explicitly move/extract-only, signature-preserving). **D4's
  own watchlist/closeness slice can reasonably start in parallel with
  Wave C**; if D4 and D5 ship as one combined tab, follow D5's more
  conservative sequencing for the combined whole.
- **No dependency on WP-17** - no `FrameTicker` introduced anywhere in
  this proposal (Section 5.4).
- **Reconcile with D5 before either starts implementation** (Section 0):
  one `RankerStore`, one filename, one entry shape. D4 is the natural
  first increment (the tab needs to exist and hold a list before "Do
  Next" has anything to act on); D5's Tier 1 layers on top once D4 ships.
- **Reconcile with D3** on nothing structural (different stores, no
  shared file) - only the shared WP-21/22 coin-rendering sequencing note
  above, which both proposals independently flag.

---

## 14. Prior art (dev-time web research; gw2efficiency is reference-only, never called at runtime)

- `gw2efficiency.com` was **unreachable during this research session**
  (redirects to a maintenance page, `maintenance.gw2efficiency.com`, live
  as of this session) - its current live UI could not be directly
  observed. All findings below are from secondary sources (its own
  GitHub issue tracker, and a community-built alternative), not a direct
  screenshot/read of the page.
- The original 2016 feature-request issue for gw2efficiency's own
  `crafting/legendaries` overview (`gw2efficiency/issues#617`) frames it
  as *"an overview of the mats that I'm missing still"* - i.e. a per-
  legendary missing-materials view, the same "remaining, not owned" framing
  Section 2 recommends, but scoped to one item viewed at a time in the
  issue's own framing; no evidence surfaced (via search or the issue
  tracker) of a cross-legendary priority-ordered watchlist feature on
  that page.
- A community alternative, `davidyell/GW2-Legendary` (open-source, single-
  legendary focus), confirms the well-trodden shape of this domain's
  expected UX: *"a nice big percentage progress bar"* plus a hierarchical
  breakdown of quantities gathered vs. needed, state shareable via a URL
  hash. Explicitly **single-legendary only** - no multi-item comparison,
  ordering, or prioritization concept at all (its own README, fetched this
  session).
- **Echo-worthy** (well-established, low-risk to copy the *idea*, not the
  code): a per-item percentage/closeness readout paired with a
  remaining-materials breakdown is exactly what both the gw2e ecosystem
  and this proposal converge on independently - Section 2's design is not
  inventing an unfamiliar pattern.
- **Original ground** (not found in any prior-art source checked this
  session): a persisted, priority-ordered, multi-item watchlist that
  feeds a downstream "what to finish first" decision. Neither
  gw2efficiency's own legendaries overview (per its originating feature
  request) nor the community `GW2-Legendary` tool models more than one
  tracked item at a time. This is the genuinely novel half of the user's
  two-fold ask, and Section 4's priority-ordering UX has no direct prior
  art to lean on beyond gw2efficiency's own (deferred-in-this-repo)
  `moveRecipe` up/down-arrow idiom for a *different* feature (the input
  strip, not a watchlist).

Sources consulted this session: `gw2efficiency.com/crafting/legendaries`
(redirected to maintenance), `github.com/gw2efficiency/issues/issues/617`,
`github.com/davidyell/GW2-Legendary`.

---

## 15. Open questions for the maintainer

1. **Cross-proposal reconciliation with D5 (Section 0/13, highest
   priority to resolve before either starts)**: confirm both proposals
   converge on one `RankerStore`, one persisted filename
   (`data/ranker_watchlist.json` this proposal's guess vs. D5's
   independent `data/ranker.json` guess - neither is load-bearing yet,
   pick one), and one entry shape.
2. Is the coin-denominated remaining/total ratio (Section 2.1-2.2) the
   right primary metric, or does the maintainer want the blended-
   currency-valuation variant despite its dependency on a user-guessed
   `CurrencyValuation` rate (Section 2.1's rejected candidate)?
3. Duplicate-add behavior (Section 3.2): update quantity in place (this
   proposal's recommendation), prompt the user, or allow multiple rows
   for the same item at different quantities?
4. The joint/merged closeness variant (Section 7.3) is flagged as needing
   new pipeline-level output (a per-root cost attribution within a joint
   solve) that this proposal does not build. Is that a wanted future
   milestone, or should the independent-only model (7.1) stand
   permanently, with the standing caption (7.2) as the sole mitigation?
5. Sequential vs. parallel Refresh solves (Section 5.3): sequential is
   this proposal's default for simplicity; worth measuring parallel
   fan-out as a follow-up, or is sequential clearly fine given the
   watchlist sizes the maintainer expects (unknown - no data on expected
   watchlist size exists anywhere in this session)?
6. Percentage-cell visual (Section 9.4): text-only percentage (this
   proposal's v1 recommendation) or invest in the faked two-`Panel` bar
   from the start?
7. Should the maintainer run a Harness `--profile` measurement against a
   real legendary/precursor item (Section 5.2) before this ships, to
   confirm the Exordium-derived cost numbers generalize? This proposal
   flags the need but has not performed that measurement itself.
8. Tab ordering: `Tab.OrderPriority`'s default/tie-break behavior is
   unproven from code (same open gap D3 independently flagged) - once
   the Ranker stops being a placeholder, does the maintainer want it
   pinned to an explicit `OrderPriority`?
9. Is a watchlist size cap (Section 10) wanted preemptively, or only if
   real usage shows it growing large (this proposal's default: don't add
   a setting speculatively)?
