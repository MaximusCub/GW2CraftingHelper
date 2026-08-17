# Architecture: the essential complexity

This document exists because several pieces of this module look, on first
read, like over-engineering for a Blish HUD addon. They are not - each one
is a direct, evidence-backed response to a real constraint (a missing
`SynchronizationContext`, a bug in the vendored Blish HUD binary, a race
between two independently-scheduled callbacks, and so on). This is the
durable "why" for each of those pieces: what it is, why it exists, and
where it lives. It intentionally does not repeat the full investigation
narrative (root-cause traces, live-verification transcripts, dated PASS
records) - that history is preserved in
[`docs/KNOWN-ISSUES.md`](KNOWN-ISSUES.md) (the current short tracker) and
[`docs/dev-notes/HISTORY.md`](dev-notes/HISTORY.md) (the full fix-pass
diary this document distills). Each section below names the KNOWN-ISSUES
item number(s) it is drawn from so you can go read the original
investigation.

This is a living map of *mechanisms*, not a tour of every file. See
`docs/gw2e-parity-spec.md` for the normative behavior the solver targets,
and `CONTRIBUTING.md` for build/test/style basics.

---

## 1. No `SynchronizationContext`: `MainThreadMarshal` and `FrameTicker`

**What:** Two small primitives that get code back onto Blish HUD's main
(UI) thread, for two different shapes of problem.

- `MainThreadMarshal.Run` (`Views/MainThreadMarshal.cs`) - queues a single
  one-shot action onto the main thread via
  `GameService.Overlay.QueueMainThreadUpdate`.
- `FrameTicker` (a private nested `Control` in
  `Views/CraftingPlanView.cs`) - drives a callback once per real engine
  frame via `Control.DoUpdate`, for work that must span multiple frames
  (scroll verify, resize-settle debounce, wheel-wrap correction verify).

**Why:** Blish HUD's XNA host installs no `SynchronizationContext`, so an
`await` continuation resumes on a ThreadPool thread by default; any code
that touches a Blish HUD control after an `await` must marshal back onto
the main thread first, or it corrupts control state from a non-UI thread.
`QueueMainThreadUpdate` looks like it should also work for multi-frame
work (call it again from inside its own callback to "wait a frame"), but
it does not: empirically confirmed via a live trace during M30, a
re-queued callback drains again **within the same frame** instead of
waiting for the next real `Update()` tick (400 same-frame re-queues
observed in one drain). `FrameTicker` exists because `Control.DoUpdate` is
documented to fire at most once per real frame, which `QueueMainThreadUpdate`
cannot guarantee under re-entrant re-queuing.

**Where:** `Views/MainThreadMarshal.cs`; `FrameTicker` in
`Views/CraftingPlanView.cs` has FOUR live instances (measured): `_scrollVerifyTicker`
(scroll verify), `_resizeDebounceTicker` (resize-settle debounce),
`_wheelWrapVerifyTicker` (wheel-wrap correction verify, driving
`ApplyWheelWrapCorrection` - see section 2), and `_spinnerTicker` (the W3B
status-strip spinner tick - see `ArmSpinnerTicker`/`SpinnerTick` and
`Services/PlanStripTickDecision.cs`). All four are canceled/nulled together
by `StopLiveTickers` (see `docs/KNOWN-ISSUES.md`'s `CraftingPlanView`
hazard row for the tab-switch race this class of field is exposed to).
Scroll restore itself is applied synchronously, not via a ticker - see
section 3.

**Verified: `Build()` itself also runs off the main thread.** Every one of
this module's `_mainWindow.Tabs` entries (`LogTabContent`, `MainView`,
`SettingsTabContent`, `AboutTabContent`, `CraftingPlanView`, and the Plan
History/Crafting Ranker placeholders - see Module.cs's `Initialize()`) is
wrapped in `Views/ViewAdapter.cs`, whose `Build(Container)` override is
called by Blish HUD's own view-loading pipeline, not by this module. Decompiling
the shipped Blish HUD v1.3.0 binary (`Blish HUD.exe`, via `ilspycmd`)
confirms the exact call chain and why it lands on a ThreadPool thread:
`Blish_HUD.Controls.TabbedWindow2.OnTabChanged` (fired from the `SelectedTab`
setter, synchronously on the main thread on a tab click) calls
`WindowBase2.ShowView(view)`, which does
`view.DoLoad(progress).ContinueWith(BuildView)`; `BuildView` calls
`CurrentView.DoBuild(this)`, and `View<TPresenter>.DoBuild` calls the
protected `Build(buildPanel)` method every view (including `ViewAdapter`)
overrides. `View<TPresenter>.DoLoad` is `async Task<bool>` and, for the base
`Load`/`NullPresenter.DoLoad` implementations this module's views use,
completes without any genuine `await` suspension - but `Task.ContinueWith`
called without `TaskContinuationOptions.ExecuteSynchronously` and with no
ambient `SynchronizationContext` schedules its callback onto
`TaskScheduler.Default` (the ThreadPool) regardless of whether the antecedent
task is already complete at the point `ContinueWith` is called. So `Build()`
reliably runs on a ThreadPool thread, never inline on the main thread that
triggered the tab switch - the same "no `SynchronizationContext`" constraint
this section's `MainThreadMarshal` exists for, just reached via Blish HUD's
own internals instead of this module's own `await`s. (`TabbedWindow2`'s
`Tabs`/`SelectedTab` machinery is what `Views/ResizableTabbedWindow.cs`, this
module's `_mainWindow`, derives from.)

**Also verified: a tab switch detaches, it does not dispose.** A liveness
check shaped like `control.Parent != null` (this module's
`LogTabContent.IsLive`, and the inline `_headerPanel`/`_contentPanel`/
`_coinPanel`.`Parent == null` guards in `MainView.cs`) only detects that
`control` has been **disposed** - it does NOT detect that `control`'s tab was
merely switched away from, even though several of this module's own comments
previously claimed otherwise. Decompiling `WindowBase2.ShowView`/`ClearView`
shows `ClearView()` calls `Container.ClearChildren()` on the WINDOW itself
(`while (_children.Count > 0) { _children[0].Parent = null; }`) - detaching
only the outgoing view's top-level `ViewAdapter` panel, not anything below
it - and `CurrentView.DoUnload()`, whose `Unload()` call is a no-op for every
view in this module (`ViewAdapter` does not override `View<TPresenter>.
Unload()`). Only `Control.Dispose()` nulls a control's own `Parent`
(`Parent = null;` inside `Control.Dispose(bool disposing)`), and nothing on
the tab-switch path calls it - that only happens when `Module.Unload()`
disposes `_mainWindow`. Net effect: after a plain tab switch, every control
below the outgoing `ViewAdapter`'s own top-level panel (e.g.
`LogTabContent._contentPanel`, `MainView._headerPanel`/`_contentPanel`) keeps
a non-null `Parent`, so a `Parent != null`/`IsLive`-shaped guard does NOT
trip for that case - only for the module actually being unloaded. A
`MainThreadMarshal.Run` tail that lands after the user has already switched
away therefore still executes its render into a detached,
unreachable-but-not-disposed tree: wasted work (rebuilding rows or content
nobody will ever see), not a crash and not a correctness bug - but a call
site whose comment claims the guard catches that case is asserting something
false, which is its own defect (KNOWN-ISSUES #36, third fix-loop round).

**Also verified: `Container.Children` is lock-guarded - the hazard a
marshaled `Build()` tail actually closes is the compound dispose-then-add
sequence, not `Children` itself.** A tempting shorthand for why a
dispose-then-add `Build()` tail (`MainView.Build`'s
`UpdateCoinDisplay`/`ApplyStatusDisplay`/`RebuildContent`,
`LogTabContent.RebuildRows`) needs marshaling is "two threads would mutate
the same `Children` collection concurrently, corrupting it" - decompiling
`Blish_HUD.Controls.ControlCollection<T>` (`packages/BlishHUD.1.3.0/lib/
net472/Blish HUD.exe`, via `ilspycmd`) shows this is not actually why:
`ControlCollection<T>` holds a private `ReaderWriterLockSlim _listLock` and
takes it on every operation - `Add`/`Remove`/`AddRange`/the indexer setter
all `EnterWriteLock`; `Count`/the indexer getter `EnterReadLock`; and
`GetEnumerator` `EnterReadLock`s and releases it from its
`ControlEnumerator`'s `Dispose()`. `Container.AddChild`/`RemoveChild` build
their `ChildChangedEventArgs` from a `_children.ToList()` snapshot and then
call the locked `_children.Add`/`_children.Remove`. So concurrent `Children`
mutation cannot corrupt the collection's own internals the way an
unsynchronized `Queue<T>` can (LogTabContent's field crash above) - unlike
that crash, this module has never actually needed to guard against
`Children` itself being corrupted. The real hazard in a "dispose old
children, then add new ones" tail is that the sequence is a non-atomic
COMPOUND operation: `Children`'s own lock protects each individual
`Add`/`Remove` call, but nothing holds a lock across the whole
"dispose-every-old-child, then add-every-new-one" sequence, so two
interleaved rebuilds can each finish disposing before either starts adding,
and both survive - duplicated content, e.g. the doubled "No log entries
yet." placeholders `LogTabContent` hit live on 2026-07-23
(`LogTabContent.cs`'s `_buildComplete` doc comment). Marshaling the whole
tail onto the main thread still closes this correctly, just for the right
reason: it prevents two rebuilds from interleaving AT ALL (a single thread
cannot run two call stacks at the same instant), rather than relying on a
lock inside `Children` that was never the thing missing. A call site whose
comment instead claims `Children` itself would have been corrupted is
asserting something the decompiled source disproves - its own defect
(KNOWN-ISSUES #36, fourth fix-loop round).

**Full history:** KNOWN-ISSUES items 1, 12, 13, 36
(`docs/dev-notes/HISTORY.md` after the WP-27 split).

---

## 2. The shipped-Blish `WheelDelta` sign-unwrap bug: `WheelDeltaSanitizer`

**What:** `Services/WheelDeltaSanitizer.cs` classifies a raw Blish HUD
wheel delta as either genuine or corrupted by a real defect in the
vendored `Blish HUD.exe` binary, and corrects it when corrupted.

**Why:** Decompiling the shipped Blish HUD v1.3.0 binary
(`Blish_HUD.Input.MouseEventArgs.WheelDelta` getter) shows it extracts a
signed 16-bit Windows mouse-wheel delta as *unsigned*, then "corrects" the
sign only when the unsigned value exceeds the single-notch step (120).
That heuristic is wrong the moment Windows coalesces 2+ up-notches into
one hook message: a genuine `+240` (two up-notches) reads as unsigned
`240`, which is `> 120`, so the getter "un-wraps" a value that was never
wrapped, turning `+240` into `240 - 65536 = -65296`. This reproduces the
exact live-measured histogram from a 2026-07-21 instrumented trace
(`N*120 - 65536` for `N` coalesced up-notches). Down-notches never trigger
it (their unsigned representation is already above the threshold for a
legitimate reason). This is not a dev-harness artifact - both of Blish
HUD's mouse-hook backends feed the same buggy getter, so a real player
fast-flicking the wheel upward hits it. The module cannot patch Blish
HUD's own binary, so it classifies and corrects the value on the way in
instead.

**Where:** `Services/WheelDeltaSanitizer.cs` (pure, Blish-free,
unit-tested); consumed by `CraftingPlanView.ApplyWheelWrapCorrection`.

**Full history:** KNOWN-ISSUES item 12 (reopened/root-caused in M36).

---

## 3. Scroll preserve/restore/verify

**What:** Every mutation that can change section content height (a
decision-pill click, Expand/Collapse All, a resize) wraps its rebuild in
`CraftingPlanView.PreserveScrollAcross`, which snapshots the current
scroll offset, lets the rebuild run, and then re-asserts that offset for
several subsequent real frames (`StartScrollVerify`) - because Blish's own
`Panel`/`Scrollbar` machinery resets scroll to zero on certain content
changes, and there is a window during which a user's own wheel input can
arrive and must **not** be overwritten by the restore.

**Why:** This is a genuine contest, not a one-shot fix. The scrollbar
offset is read via reflection (`PanelScrollbarField`) because Blish HUD
does not expose it any other way. Two hard-won invariants make the
contest safe rather than janky:

- Container heights (section bodies, recipe-tree child containers) are
  finalized **synchronously at build time** (see `PlanContentHeightMath`
  below) instead of relying on Blish's `FlowPanel` `AutoSize`, which only
  converges one nested level per real frame - the old fluctuating-height
  window was the actual root cause of a reopened fast-wheel-up bug (a
  wheel notch landing during that window used to be silently overwritten).
- The verify loop yields immediately the moment it observes a real wheel
  event, rather than requiring the content height to have stopped
  changing first - so a user scrolling during a live restore is never
  contested.

**Where:** `Views/CraftingPlanView.cs`, region "Scroll preserve/restore/verify"
(`PreserveScrollAcross`, `StartScrollVerify`, the `PanelScrollbarField`
reflection handle).

**Full history:** KNOWN-ISSUES items 1, 12, 14, 19 (root-cause and fix
narrative for the reopened fast-wheel-up regression is the most detailed
single item in the history).

---

## 4. `PlanContentHeightMath`: the synchronous height contract

**What:** `Services/PlanContentHeightMath.cs` is pure, Blish-free,
unit-tested arithmetic that computes the exact pixel height of any section
body or recipe-tree subtree from row counts/types and expansion state
alone - no layout pass, no waiting for convergence.

**Why:** Every row height in the plan view is a fixed constant (nothing
wraps; only single-line ellipsis truncation), so the total height of any
container is knowable up front. `CraftingPlanView` uses these same
constants both to size containers explicitly (replacing Blish's
`FlowPanel` `AutoSize`) and to size the individual row `Panel`s it
creates, so the two paths cannot drift apart. This synchronous contract is
what closes the multi-frame flash/stutter window described in section 3
above - without it, "wait for layout to settle" would need to reappear
somewhere, reopening the same race.

**Where:** `Services/PlanContentHeightMath.cs`; mirrored by
`Services/PlanRelayoutMath.cs` for the width-dependent counterpart (column
anchors, cost-tile geometry) used by both the build path and the
in-place resize relayout path.

**Full history:** KNOWN-ISSUES items 12, 14, 19.

---

## 5. The relayout/re-ellipsis registry, `ISectionRelayoutSink`, and the section-renderer decomposition

**What:** When the plan window is resized, every row that has
width-dependent content (an ellipsized name, a right-aligned coin column)
needs to re-measure in place, without a full dispose+rebuild. Each section
builder registers a same-signature closure into one of two registries
(`_relayoutActions`, `_reellipsisActions`) at build time; a resize replays
every registered closure. A DEBUG-only assertion checks that a section
builder registered at least one relayout closure, so a future section
cannot silently opt out of resize support.

`ISectionRelayoutSink` (`Views/Rendering/ISectionRelayoutSink.cs`) is the
seam that let this registry be reached by section-renderer classes
extracted out of `CraftingPlanView` during M38, without those renderers
holding a reference to the view itself: `AddRelayout`/`AddReellipsis` are
a direct pass-through to the same two `List<Action<int>>.Add` calls the
inline builders always made, so every existing invariant (the DEBUG
must-register check, the scroll-neutral assert, `ReplayRelayout`'s own
foreach) sees a sink-registered closure exactly as it would have seen one
added inline.

**The M38 decomposition:** `CraftingPlanView` was originally a single
~4,800-line class covering navigation, layout, six content sections, the
recipe tree, and scroll/resize/wheel handling. M38 (WP-21, WP-23a-d,
WP-24, WP-25) extracted:

- Six stateless per-render section renderers under `Views/Rendering/`:
  `DisciplinesSectionRenderer`, `UsedMaterialsSectionRenderer`,
  `ShoppingListSectionRenderer`, `CraftStepsSectionRenderer`,
  `RecipesSectionRenderer`, `SummarySectionRenderer` - each pushes closures
  into the sink instead of the view's private fields, and is freshly
  constructed on every render (they own no state across renders).
- `TreeSectionController` (`Views/Rendering/TreeSectionController.cs`) -
  the one component that is **not** stateless: it owns the recipe tree
  render state and the interactive override loop
  (`_nodeOverrides`/`_ignoredItemIds`/`_nodeExpansion`/`_treeNodeStates`),
  which must survive a local pill-click re-solve (a pill click never
  resets the user's overrides). Because of that, it is constructed once in
  `CraftingPlanView`'s own constructor and held as a persistent field,
  unlike the six per-render renderers above.
- Tier-1 static rendering primitives with no instance state
  (`CoinCurrencyRenderer`, `RarityColors`, `IconControls`, `LabelHelpers`)
  also moved to `Views/Rendering/`.

**TreeSectionController state/render split: rejected by decision, not
deferred by oversight.** A later proposal to bisect `TreeSectionController`
into a stateful collaborator (owning `_nodeOverrides`/`_ignoredItemIds`/
`_nodeExpansion`/`_treeNodeStates`) and a separate stateless renderer,
mirroring the six per-render section renderers above, was evaluated and
rejected (quorum verdict D-2). The invariant this class exists to hold is
one owner, one lifetime: the whole reason it is constructed once in
`CraftingPlanView`'s own constructor (`Views/CraftingPlanView.cs` ~614)
instead of freshly per render is that its override state must survive a
local pill-click re-solve, and a two-class split would either duplicate
that lifetime management across both halves or reintroduce a second
implicit owner - the exact class of bug section 1's "one owner" primitives
above exist to prevent, just at the object-graph level instead of the
thread level. It is also already the most-coupled class in
`Views/Rendering/` outside its own file - named by type in 14 production
code refs, measured (`Module.cs` plus 9 `Services/` files and 4 `Views/`
files, not counting `Models/` shape-mirroring comments or test files) and
3 existing doc mentions (this section's own bullet and "Where:" line, plus
`docs/ROADMAP.md`) - not 18, an earlier over-count that conflated this
figure with something wider. A state/render split would not shrink that
coupling, only relocate half of it across a new seam. The accepted
alternative for future tree-row/pill features is not a class bisection: per
the STANDING RULE (`CONTRIBUTING.md`), extract the pure text/decision
computation for a given feature into a Blish-free, unit-tested composer
under `Services/` first - `TreeRowTooltipComposer` (tree-tooltip-composer
milestone) is the latest instance of the same pattern already behind
`DecisionPillPlanner`, `ValueDetailTooltipBuilder`, `PillSubduingEvaluator`/
`PillSubduingTooltipBuilder`, and `ReceiptCaptionHelper` - and keep wiring
it into `TreeSectionController`'s existing single-owner shape rather than
growing a second stateful class alongside it.

**What stayed, and why (WP-26 cut):** The scroll/resize/wheel controller
move (bundling `PreserveScrollAcross`, the wheel-wrap correction, and the
`FrameTicker`s then in the class into their own collaborator class) was
scoped as WP-26 and explicitly **cut** on 2026-07-23. It was the single
riskiest remaining move with zero functional payoff: the guarantees
involved (frame-timing, subscription order, synchronous-registration) are
asserted by construction and by the invariants in sections 1-4 above, not
by any automated test, so a regression would only surface in live use, and
a reliable synthetic drag-resize verification was not achievable. The five
completed extractions took `CraftingPlanView` from a ~4,802-line
plan-authoring baseline down to ~2,802 lines at the time WP-26 was cut -
real progress, even though short of the plan's own 2,000-line target - so
the remaining scroll/resize/wheel machinery stays in `CraftingPlanView.cs`,
fully region-mapped with KNOWN-ISSUES anchor comments at each region head.
Measured current: `Views/CraftingPlanView.cs` is 3,674 lines (2026-08-17) -
higher than the post-WP-26 figure above, which is expected, not a
regression of the decomposition: every line added since (W3B status
strip/spinner, currency-ux-package, gate-round fixes, the
tree-tooltip-composer extraction itself, ...) is a legitimate feature/fix
landing in the file the STANDING RULE (see the TreeSectionController
state/render split entry above and `CONTRIBUTING.md`) still routes pure
logic out of on the way in, not evidence the WP-21 through WP-25
extractions eroded.

**Where:** `Views/Rendering/ISectionRelayoutSink.cs`,
`Views/Rendering/TreeSectionController.cs`, the six
`Views/Rendering/*SectionRenderer.cs` files, and the surviving
`_relayoutActions`/`_reellipsisActions` registries plus scroll/resize/wheel
machinery in `Views/CraftingPlanView.cs`.

**Full history:** KNOWN-ISSUES items 13, 19 (registry); the WP-21 through
WP-25 entries and the WP-26 cut-decision entry (M38 section, near the end
of the history).

---

## 6. `StatusUpdateGuard`

**What:** `Services/StatusUpdateGuard.cs` is a single pure function,
`ShouldApply(tickGeneration, currentGeneration, currentGenerationStatusClosed)`,
that decides whether a queued plan-generation status update should still
be written to the status label.

**Why:** A generation's trailing progress tick and that same generation's
completion write are two independently-scheduled main-thread callbacks
with no FIFO guarantee between them - `Progress<T>`'s default
`SynchronizationContext` hop (used for every progress tick) takes one
extra ThreadPool round-trip versus the task-continuation path the
completion write rides, so in practice the completion write reliably
reaches the main-thread queue and drains **before** an earlier-queued
trailing tick from the exact same generation. A simple
"does this tick belong to the current generation" guard cannot catch this
race, since both callbacks belong to the same generation and pass that
check. The fix is to also track whether that generation's own completion
status has already been written, and refuse to overwrite it - checked at
the moment each tick's callback actually *runs*, not when it was queued,
which is what closes the race regardless of drain order.

**Where:** `Services/StatusUpdateGuard.cs`; consumed by
`CraftingPlanView.TriggerGenerate`'s progress callback.

**Full history:** KNOWN-ISSUES item 20.3 (M34-B1 #4).

---

## 7. Merged-ceil vendor batching

**What:** When a plan needs a vendor-sold item at more than one tree
position (or a vendor sells it only in fixed-size batches), the solver
must compute one true per-item cost across all occurrences, rounding batch
purchases up **once** for the combined total rather than once per
occurrence. This lives in `Services/VendorBatchSolver.cs`
(`EvaluateVendorOffers`, `FinalizeVendorBatches`,
`AllocateVendorNodeCosts`, `MergeVendorCurrencyCosts`, `VendorBatchesEqual`,
`ScaleCostLines`), injected into `PlanSolver` as a collaborator.

**Why:** Rounding per-occurrence instead of per-total overstates cost.
The canonical regression case: needing 179 of a vendor item sold in
batches of some size that should round up to a total of 180, not 186 -
the bug that motivated pinning this arithmetic as do-not-touch. The class
also carries the Astral Acclaim / Wizard's Vault seasonal purchase cap
(`SeasonalCap`, independent of the pre-existing daily/weekly cap fields)
and the Homestead Refinement efficiency-tier discount, both threaded
through the exact same merged-batch machinery rather than as separate
paths.

**Where:** `Services/VendorBatchSolver.cs`. This arithmetic is
documented-essential (do-not-touch): WP-11 and WP-15 restructured the
*shape* around it (an out-param bundle became a result struct; the whole
engine moved out of `PlanSolver` into its own class) but never touched the
arithmetic itself - both moves are diffable as pure code motion.

**Full history:** KNOWN-ISSUES items 20.1, 20.2, 28, 33.

---

## 8. Solver decision rules

**What:** `Services/PlanSolver.cs` decides, per node, whether to craft,
buy from the Trading Post, buy from a vendor, or fall back to "unknown
source" - echoing gw2efficiency's own `cheapestTree` behavior rather than
inventing a new one. The load-bearing rules:

- **TP buy is the baseline and wins every tie.** Craft beats buy only when
  *strictly* cheaper; a missing buy price counts as "beats buy" (force-craft
  - there is nothing else to compare against). Vendor follows the identical
  rule against buy. When both craft and vendor beat buy, the numerically
  cheaper of the two wins; an exact craft/vendor tie keeps vendor.
- **Buy-order vs sell-listing basis** is a caller-supplied price basis
  threaded through every comparison, matching whichever basis the user
  selected in the UI - but it is *preferred per item*, not force-applied
  regardless of data: `PlanSolver.GetUnitPrice` tries the basis-preferred
  TP side first, and only when that SAME item has no listings on its
  preferred side does it fall back to that same item's other TP side
  rather than treating the item as unpriceable (see KNOWN-ISSUES.md,
  "AUDIT ROW 20/38"). This is a per-item same-item substitution: no
  single item is ever priced on a mixed basis, and an item with listings
  on its preferred side never touches the other side. A total summed
  across items - e.g. a craft cost built from several ingredients - can
  still combine sides when a fallback fires on one of them, so the
  guarantee is scoped to "no single item," not "no comparison anywhere
  in the tree." Currencies (as recipe ingredients) contribute to the
  craft-vs-buy *decision* via an optional per-unit valuation, but never to the
  displayed real coin cost - an unvalued currency never has an invented
  exchange rate.
- **Craft/vendor comparability parity:** a recipe with an unvalued
  Currency-type ingredient is fallback-tier - never comparable with a real
  TP/vendor coin price in `PickCheapest` - exactly like a vendor offer
  carrying an unvalued non-coin currency line already is
  (`VendorBatchSolver.EvaluateVendorOffers`). Still offered (`CanCraft`
  stays true) and used as a last resort when nothing comparable exists at
  all. See KNOWN-ISSUES' "Craft/vendor comparability parity fix" entry.
- **Mystic Clover-style EV pricing:** fractional-output Mystic Forge
  recipes have their ingredient quantities pre-scaled upstream (by
  `RecipeService`, kept in sync by `InventoryReducer`) to the expected
  number of forge attempts needed at the recipe's success rate. `PlanSolver`
  does not re-apply any ratio on top of that - doing so would
  double-amortize the cost.
- **Force-craft:** a node with a recipe but no buy price always crafts
  (there is no buy cost to lose to), matching gw2efficiency's
  `isCheaperToCraft = craftPrice-defined && (!buyPrice || decisionPrice < buyPrice)`.

**Where:** `Services/PlanSolver.cs` (`Evaluate`, `PickCheapest`); the
normative spec these rules echo is `docs/gw2e-parity-spec.md`.

**Full history:** KNOWN-ISSUES items 20, 21, 24, 25, 26 (the M33-M37
parity waves); `docs/gw2e-parity-spec.md` for the researched gw2efficiency
behavior itself.

---

## 9. Data pipeline: seeds, wiki scrapes, dev-only caches

**What:** The module reads several JSON files under `ref/` at runtime
(recipes, item names, vendor offers, Mystic Forge recipes, acquisition
hints) - all produced **ahead of time** by offline tools under `tools/`
and committed to the repo. Nothing under `Services/`/`Views/` fetches from
gw2efficiency or the GW2 Wiki at runtime; `gw2efficiency` is research-only,
consulted at dev time to write `docs/gw2e-parity-spec.md` and never called
from module code.

- `tools/GW2CraftingHelper.RecipeSeeder` queries the official GW2 API
  (`api.guildwars2.com`) to build `ref/recipes_seed.json` and
  `ref/recipe_search_seed.json`.
- `tools/VendorOfferUpdater` scrapes the GW2 Wiki's Semantic MediaWiki
  `action=ask` API (`WikiSmwClient`) for vendor-sold items, resolves
  currency names via the official GW2 API, and writes
  `ref/vendor_offers.json`. It also seeds vendor purchase caps
  (daily/weekly/seasonal) and Homestead Refinement tier data from the same
  wiki properties.
- `tools/MysticForgeSeeder` scrapes the wiki for Mystic Forge recipes to
  build `ref/mystic_forge_recipes.json`.

Two of the files these tools produce as **intermediate working state**
(`ref/wiki_vendor_cache.json`, `ref/item_id_cache.json`) are dev-only
inputs to `VendorOfferUpdater`'s own incremental-scrape workflow, not
consumed by the shipped module at all; they are gitignored rather than
committed (see `docs/RELEASING.md` for the packaging implication of a
dev machine still having them on disk locally).

**Where:** loaders - `Services/VendorOfferLoader.cs`,
`Services/RecipeCacheSerializer.cs`, `Services/ItemNameSeedData.cs`; wiki
scraper - `tools/VendorOfferUpdater/WikiSmwClient.cs`.

**Full history:** KNOWN-ISSUES items 24, 28, 33; `CONTRIBUTING.md`'s
"Where seed/reference data comes from" section for the day-to-day workflow.
