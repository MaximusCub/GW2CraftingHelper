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
[`docs/KNOWN-ISSUES.md`](KNOWN-ISSUES.md) (the current-state tracker: the
numbered catalog, the open list, and a ledger pointing into
[`dev/archive/known-issues/`](../dev/archive/known-issues/), where the full
milestone records live one file each) and
[`dev/dev-notes/HISTORY.md`](../dev/dev-notes/HISTORY.md) (the pre-M38 fix-pass
diary this document distills). Each section below names the KNOWN-ISSUES
item number(s) it is drawn from so you can go read the original
investigation.

This is a living map of *mechanisms*, not a tour of every file. For a map
of *files* - which folder holds what, and the handful worth opening first -
see [`docs/README.md`](README.md). Designs that were proposed and
deliberately not built live in [`docs/DECISIONS.md`](DECISIONS.md), so this
document can describe what exists rather than argue against what does not.
See `docs/gw2e-parity-spec.md` for the normative behavior the solver
targets, and `CONTRIBUTING.md` for build/test/style basics.

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
false, which is its own defect (KNOWN-ISSUES #36).

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
yet." placeholders `LogTabContent` hit live on 2026-07-23, and - on the
second path, where `Module.cs`'s `TabChanged` handler called `Refresh()`
on the main thread while `Build()`'s own tail was still running on a
ThreadPool thread - two threads enqueuing into `_renderedRows` at once,
crashing with "Destination array was not long enough" inside
`Queue<T>.SetCapacity`. Marshaling the whole
tail onto the main thread still closes this correctly, just for the right
reason: it prevents two rebuilds from interleaving AT ALL (a single thread
cannot run two call stacks at the same instant), rather than relying on a
lock inside `Children` that was never the thing missing. A call site whose
comment instead claims `Children` itself would have been corrupted is
asserting something the decompiled source disproves - its own defect
(KNOWN-ISSUES #36).

### The logging-channel rule for these guards

Every one of these primitives ends in a catch that swallows rather than
propagates - `MainThreadMarshal.Run` (an unhandled exception in a queued
callback would take down Blish HUD's update loop), `FrameTicker.DoUpdate`,
`TooltipFacility.ResolveContent` (a deferred builder runs inside Blish's
own mouse-moved handler), and `ResizeSettleDebounce`'s `_onError`. That
makes the choice of log channel load-bearing, so it is a rule and not a
preference:

> **Anything a user could plausibly report goes to
> `ModuleLog.Shared.Write`. Blish HUD's `Logger` is additive - never the
> sole channel.**

The module ships its own diagnostic surface (`Services/ModuleLog.cs`,
`Views/LogTabContent.cs`) with a Copy-to-clipboard button, and that button
is what a bug report will actually contain. A failure written only to
Blish's own file log is invisible there, which is the worst possible place
for exactly these symptoms to land: "the tooltip does nothing on that row",
"the plan strip froze on a phase", "I clicked Generate and nothing
happened". Before this rule was written down the split was 39 `Logger`
calls to 37 `ModuleLog` calls with no stated convention, so every new catch
block was a coin flip.

Two riders:

- **`Logger` stays.** It carries the full stack; `ModuleLog` entries are
  one ring-buffer line each and keep only the exception's type and message.
  A catch that discards recoverable state (`CraftingPlanView.
  RollBackFailedPlanRender`) writes both, plus enough plan identity to
  reproduce - never item ids, which stay internal-only, log tab included.
- **The log system's own failures never route back into the log system.**
  `ModuleLogStore`'s IO-failure callback goes straight to `Logger`
  (Module.cs, `Initialize`), because writing into the sink whose write just
  failed is unbounded recursion. `MainThreadMarshal` is the subtle case:
  `LogTabContent` rebuilds its rows THROUGH `MainThreadMarshal.Run`, so a
  rebuild that throws would write the entry that schedules the rebuild that
  throws. It carries a `[ThreadStatic]` re-entrancy guard for the
  synchronous half and suppresses consecutive duplicate signatures for the
  asynchronous half; `Logger` still gets every occurrence.

**Full history:** KNOWN-ISSUES items 1, 12, 13, 36
(`dev/dev-notes/HISTORY.md` after the WP-27 split).

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

The decompiled getter, verbatim:

```csharp
int num = Convert.ToInt32((MouseData & 0xFFFF0000u) >> 16);
if (num > SystemInformation.MouseWheelScrollDelta) num -= 65536;
return num;
```

**The `-60000` threshold, derived:** a wrapped-positive event's raw value
is `N*120 - 65536` for an intended up-notch count `N >= 2`, i.e. the band
`[-65416 .. -60016]` (`N=46`, an already-absurd flick), falling further for
larger `N`. A genuine down-delta never comes near it: the largest measured
is `-840` (7 coalesced down-notches), and an implausible 40-notch down-flick
is only `-4800`. `-60000` sits between the two, so `raw <= -60000` selects
exactly the corruption. A single up-notch (`N=1`, unsigned 120) sits *at*
the threshold, not above it, so the vendored getter leaves it alone - which
is why single notches in both directions are clean.

**Why 120 is hardcoded** in the sanitizer and in
`CraftingPlanView.ApplyWheelWrapCorrection`'s `intendedDelta / 120.0`
notch arithmetic, while `MouseWheelScrollLines` is read live: 120 is Win32's
`WHEEL_DELTA`, the fixed unit a low-level mouse hook reports for one notch.
`SystemInformation.MouseWheelScrollDelta` is Microsoft's managed accessor
for that same constant and is not user-configurable, unlike
`MouseWheelScrollLines`, which is.

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

Two sections own extra arithmetic of the same kind, in the same shape
(pure, Blish-free, called from both the build path and the relayout
closures, so the two cannot drift):
`Services/SummarySectionLayoutMath.cs` (the Total Cost section's body
height, its promoted cost band, and the currency table's column edges) and
`Services/TreeCostColumnMath.cs` (the recipe tree's per-denomination cost
sub-columns and the whole-tree pre-scan that sizes them).

The same shape covers the non-scrolling top strip:
`Services/TopRegionLayoutMath.cs` holds its Y offsets, read by the initial
`Build`, the item-row add/remove reflow and the resize handler. Its rows
are not all unconditional - the Recipe Tree toolbar row appears only for a
plan that has a tree - and the invariant it guarantees is that a hidden row
costs exactly zero, so the strip with no toolbar is byte-identical to the
strip before the row existed.

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

The registry's hard rule is that a replayed closure may never change a
row's height - that is what lets the settle pass skip scroll preservation
entirely. The Plan Notes section is the one place where the right answer
at a new width genuinely is a different height (it spends one fixed-height
row per WRAPPED LINE, so a width that changes a note's line count changes
the section's height). Rather than weaken the rule, its re-ellipsis
closure calls `RequestRerenderAfterSettle`, and `ResizeSettleStep` runs a
single `PreserveScrollAcross(() => RenderPlan(...))` once the pass has
finished - deferred because `RenderPlan` clears the registry the pass is
iterating.

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
  unlike the per-render renderers above. (`NotesSectionRenderer` joined
  them on 2026-08-16 for the Plan Notes section, making **seven** today -
  same stateless, freshly-constructed-per-render shape.)
- Tier-1 static rendering primitives with no instance state
  (`CoinCurrencyRenderer`, `RarityColors`, `IconControls`, `LabelHelpers`)
  also moved to `Views/Rendering/`.

**Dependencies point one way.** `CraftingPlanView` and `MainView` call into
`Views/Rendering`; nothing in `Views/Rendering` references either. A
renderer that needs a view-private helper extracts the helper into
`Views/Rendering` rather than widening the view's surface, and what a
renderer needs from the view arrives through a narrow interface the view
implements explicitly - `ISectionRelayoutSink` for relayout registration,
`ITreePlanHost` (`Views/Rendering/ITreePlanHost.cs`) for everything
`TreeSectionController` needs beyond it - or, where null is itself a
meaningful value, a constructor delegate. This was violated once - a `GetPillColors`
`private -> internal` bump on `CraftingPlanView` - and reverted for this
reason; the rule is stated here, once, so it does not have to be restated
at each call site. `MainView -> Views/Rendering` (e.g.
`CoinCurrencyRenderer.AddSegmentSpec`) is a forward call and fine.

`TreeSectionController` is constructed once, in `CraftingPlanView`'s own
constructor (`Views/CraftingPlanView.cs` ~743), and lives as long as the
view: one owner, one lifetime. That is what lets a pill click re-solve
locally without resetting the user's overrides. The constructor takes the
two sinks, the view-model builder, the solver callback and two optional
hooks; it used to take fourteen positional arguments, ten of them bare
delegates, two of which shared the type `Action<PlanViewModel>` with
opposite meanings, so transposing them compiled. Splitting it into a
stateful/stateless pair was proposed and rejected - see
[`docs/DECISIONS.md`](DECISIONS.md). New tree-row and pill features grow
the `Services/` side of the boundary instead, under `CONTRIBUTING.md`'s
STANDING RULE.

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

The file then grew back past its own pre-decomposition baseline - 5,281
lines on 2026-08-25, +2,156 in the 33 days after the decomposition
landed - with nothing in CI to notice. It stands at **4,987 lines,
measured 2026-08-26** (`wc -l Views/CraftingPlanView.cs`), against the
~4,802 above. `Views/Rendering/` holds 9,109 lines across 37 files on the
same date, so the split of plan-tab code is roughly 65% outside the view -
a ratio that can move in either direction, unlike the one-off before/after
figure. Both numbers, and the date, so a later reader can re-run the two
commands rather than take a characterization on trust.

Two things changed on that date so the regrowth cannot repeat quietly.
`docs/file-budgets.txt` pins every tracked `.cs` file to its size that
day and a CI step fails when a file exceeds its entry, so growth now
costs a visible line in a checked-in file rather than nothing. And the
view's `#region` markers, which had numbered eight responsibilities but
shipped twenty-three disjoint blocks with eleven headers reading
"(continued)", were renamed: each marker now names its own block and no
two names repeat. The numbering went rather than the code, because making
it true would mean reordering exactly the scroll/wheel/ticker machinery
the WP-26 cut above is about.

**Where:** `Views/Rendering/ISectionRelayoutSink.cs`,
`Views/Rendering/ITreePlanHost.cs`,
`Views/Rendering/TreeSectionController.cs`, the seven
`Views/Rendering/*SectionRenderer.cs` files (the six M38 ones plus
`NotesSectionRenderer`), `Views/Rendering/PlanHeaderRenderer.cs` and
`Views/Rendering/EmptyPlanStateRenderer.cs` (the plan title and the
no-plan state, moved onto the same sink on 2026-08-25),
`Views/ItemInputRowStrip.cs` (the multi-item request editor - in `Views/`
rather than `Views/Rendering/`, because its controls are `Views` types
and the dependency points one way), and the surviving
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
the bug that motivated pinning this arithmetic as a high-evidence zone
(formerly do-not-touch; see `docs/KNOWN-ISSUES.md`'s policy note). The
class also carries the Astral Acclaim / Wizard's Vault seasonal purchase
cap (`SeasonalCap`, independent of the pre-existing daily/weekly cap
fields) and the Homestead Refinement efficiency-tier discount, both
threaded through the exact same merged-batch machinery rather than as
separate paths.

**Where:** `Services/VendorBatchSolver.cs`. This arithmetic is a
documented-essential high-evidence zone (formerly do-not-touch): WP-11
and WP-15 restructured the *shape* around it (an out-param bundle became
a result struct; the whole engine moved out of `PlanSolver` into its own
class) but never touched the arithmetic itself - both moves are diffable
as pure code motion. A change to the arithmetic itself is permitted when
it carries characterization tests of current behavior, the standard
adversarial review pipeline, and an explicit improved/regressed-nothing
statement, per the high-evidence-zone policy.

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

**Where:** `Services/PlanSolver.cs` (`Evaluate`, `SelectBestRecipes`,
`PickCheapest`); the normative spec these rules echo is
`docs/gw2e-parity-spec.md`. Whole-result goldens for these decisions live
in `tests/TaimisToolbench.Tests/Goldens/plan-solver/` - a difference
there is a finding to investigate, never a file to re-baseline.

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

- `tools/TaimisToolbench.RecipeSeeder` queries the official GW2 API
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

**Staleness policy (recipe cache):** the shipped recipe seed plus the
runtime overlay under `<dataDir>/recipe_cache/` are kept forever - a game
build change never invalidates either. Learned negatives are not stored
at all: "no recipe outputs this item" is derived at lookup time from the
corpus the module holds, and licensed as exact by a once-per-build
background probe of the `/v2/recipes` id list that fetches and folds in
any recipes the corpus lacks. The build id in the overlay manifest is
provenance and a probe cheap-out, not a wipe trigger; the manual route
out of a bad overlay is Clear Cache. Where:
`Services/Recipes/CompositeRecipeCacheStore.cs` (derived negatives),
`Services/Recipes/RecipeCorpusVerifier.cs` (the probe),
`Services/Recipes/OverlayRecipeCacheStore.cs` (keep-forever overlay).

**Where:** loaders - `Services/VendorOfferLoader.cs`,
`Services/Recipes/RecipeCacheSerializer.cs`,
`Services/Recipes/ItemNameSeedData.cs`; wiki
scraper - `tools/VendorOfferUpdater/WikiSmwClient.cs`.

**Full history:** KNOWN-ISSUES items 24, 28, 33; `CONTRIBUTING.md`'s
"Where seed/reference data comes from" section for the day-to-day workflow.

---

## 10. Post-solve annotation passes

**What:** four pure, Blish-free calculators run after the display tree
(`CraftingTree`/`MultiItemRoots`) is built: `CompetencyOpportunityCalculator`,
`ExcessCraftOutputCalculator`, `RecipeSheetSavingsCalculator`, and
`SeasonalVendorTipCalculator`. Each writes exactly one `CraftingPlanResult`
list (`CompetencyOpportunities`/`ExcessCraftOutputs`/
`RecipeSheetSavingsOpportunities`/`SeasonalVendorTips`) - one collection
each, never another pass's, never `Plan` or a total. `SellSideEconomics`
sits adjacent (same "pure, post-tree" shape) but is **not** a member: it
writes displayed totals the Total Cost section renders directly
(`NetSaleValue`/`CraftingProfit`), not an advisory Notes list.

All four are wired at three producer call sites, by name -
`CraftingPlanPipeline.GenerateStructuredAsync` (single-item),
`GenerateStructuredMultiAsync`, `ResolveWithOverrides` - plus one consumer
edit site, `PlanViewModelBuilder.BuildNotesSection`, which reads all four
lists to render their Notes rows. A fifth pass means touching all four
sites; there is deliberately no `ApplyAll` seam collapsing the three
producer calls into one, because the four calculators do not share a
signature (differing inputs - `learnedRecipeIds`, `vendorOffers`,
`characterDisciplines`). Rejected as premature; see
[`docs/DECISIONS.md`](DECISIONS.md).

The call order at each producer site - `SellSideEconomics` first,
`CompetencyOpportunityCalculator` last - is convention (kept identical
across all three sites for readability), not a data dependency: every
pass here reads only the already-built display tree, never another
pass's output, so any order between them is byte-identical.

**Where:** `Services/CompetencyOpportunityCalculator.cs`,
`Services/ExcessCraftOutputCalculator.cs`,
`Services/RecipeSheetSavingsCalculator.cs`,
`Services/SeasonalVendorTipCalculator.cs`; wiring in
`Services/CraftingPlanPipeline.cs`; consumer in
`Services/PlanViewModelBuilder.cs` (`BuildNotesSection`).

**Full history:**
[`dev/archive/known-issues/2026-08-17-annotation-detection-post-solve-advisory-list.md`](../dev/archive/known-issues/2026-08-17-annotation-detection-post-solve-advisory-list.md)
(the mutation-testing gap that produced these four passes); each
calculator's own class doc comment for its individual rationale.

---

## 11. Typography: the measured type ramp

**What:** `Services/TypeRampMetrics.cs` holds the measured Menomonia glyph
metrics behind every vertical constant in the plan view, and names which
ramp tier each chrome role sits in (section title, column header, status,
body, caption). It is pure and Blish-free; `Views/Rendering/UiFonts.cs`
is the only place that resolves an actual `BitmapFont`.

**Why:** the numbers are measured, not chosen, and two of them are
measured *defects* that a later contributor would otherwise rediscover the
expensive way, in a live desktop session:

- **18-regular is unusable for prose.** Its space glyph advances 4px,
  against 7 at 16-regular and 9 at 18-bold, so any multi-word string at
  18-regular renders with collapsed word gaps. The status line is
  therefore bold at 18, not regular.
- **22-regular is metrically a 24** - same line height, cap height and
  advances as 24-regular, different file bytes - so there is no
  regular-weight step between 20 and 24, and 22-regular must never be
  loaded. 22-bold is a genuine intermediate.
  `TypeRampMetrics.HasUsableRegularFace` refuses exactly these two sizes,
  and `UiFonts.Regular` refuses them again at the seam.
- **Two pixels of clearance under a descender, never one.** Blish's
  UI-scale transform is non-integer, so a single-pixel margin can be lost
  in the rounding. Every band height and divider clearance is a statement
  about `TypeRampMetrics.InkBottom`, derived from the lowest ink of any
  printable ASCII glyph at that size.
- **Some Blish controls are locked to Font14** (`Checkbox`,
  `StandardButton`, `TextBox`, `Dropdown`). Measure them in the caption
  tier; do not try to restyle them.

The metrics come from parsing the installed
`Content/fonts/menomonia/menomonia-{size}-{style}.xnb` files directly -
uncompressed MonoGame XNB containers holding one BitmapFont asset - and
they reproduce, glyph for glyph, the figures published in
[`docs/research/minimum-window-width.md`](research/minimum-window-width.md).
The atlas covers 8-36 regular and 8-24 plus 36 bold, reached via
`ContentService.GetFont`, not only the five Blish defaults.

**Where:** `Services/TypeRampMetrics.cs` (metrics, tier seats, and the
`InkBottom`/`BaselineAlignedY` arithmetic every constant is written in);
`Views/Rendering/UiFonts.cs` (font resolution);
`Services/PlanContentHeightMath.cs` (the band heights those metrics feed).
[`.impeccable.md`](../.impeccable.md) at the repo root carries the
tool-facing design summary and points back here.
