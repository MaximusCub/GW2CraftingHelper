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

### 4.1 Header bands: three tiers, one factory

Chrome that names something is banded; chrome that holds an interactive
control is not. There are exactly three tiers, and
`Views/Rendering/HeaderBands.cs` is the only place two of them are built:

- **Tier 1 - tab title.** One per tab, `PlanContentHeightMath.
  TabTitleBandHeight` tall, `UiFonts.SectionTitle`, wearing GW2 asset
  1032325 over an opaque base. It is the ONLY place a tab is named:
  `Blish_HUD.Controls.Tab.Draw` renders the tab's icon and nothing else,
  its `Name` is a hover tooltip, and `TabbedWindow2` never sets the
  window's `Subtitle`. `Views/ViewAdapter.cs` draws it instead of setting
  `Panel.Title`, because Blish's own header is pinned to 36px and
  `DefaultFont16` by literals inside private layout methods. A tab
  therefore does NOT repeat its own name in its content.
- **Tier 2 - section title.** No band: `UiFonts.SectionTitle` over a 2px
  rule at `SectionHeaderRowHeight - 3`. If tier 1 and tier 2 wore the same
  chrome the tab title would stop reading as the top of the hierarchy, and
  the Crafting Plan tab stacks six tier-2 headings in one scroll.
- **Tier 3 - column header.** `ColumnHeaderRowHeight` (32) of opaque base
  plus the same texture. 32 is the asset's native height, so this is the
  one surface in the module where it maps 1:1 with no vertical stretch.

The base colour and the texture are private to `HeaderBands`, so a call
site cannot hand-roll a band from them - the reason the class is a factory
and not the bag of constants it replaced, which eight sites borrowed a
colour from and seven of which built their own panel.

One rung further out, the same rule governs the container those heights
are measured against. `Views/ViewAdapter.cs` derives the hosted view's
container size from `Services/PanelChromeMath.cs` - a Blish-free mirror of
`Panel`'s own content-region arithmetic, fed Blish's public `Panel`
constants - rather than reading `Panel.ContentRegion` back after resizing
that panel. A `Panel` writes its `ContentRegion` only from
`RecalculateLayout`, and `Control.UpdateLayout` skips `RecalculateLayout`
entirely while the control's PARENT is layout-suspended, which a parent is
for the whole of its own layout pass. A window that resizes itself from
inside that pass - `Views/ResizableTabbedWindow.cs`'s minimum-size clamp
is called from `RecalculateLayout` and writes `Size` - therefore reaches
its `Resized` subscribers with the child panel's region still describing
the previous size. Sizes computed from it are wrong and stay wrong, since
nothing reads the region again. The window's own `ContentRegion` is
exempt and is still read directly: `WindowBase2.OnResized` assigns it from
the new size synchronously, before raising `Resized`, so it is never a
layout pass behind.

**Full history:** KNOWN-ISSUES items 12, 14, 19, 65.

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
- **Barter offers:** a vendor offer's `Item` cost line is money only when
  that item has a Trading Post price - it is then folded into the offer's
  real coin cost as before. An item with NO TP price is a *barter line*: an
  account-bound token whose units are the cost. Measured over
  `ref/vendor_offers.json` on 2026-08-28, 654 of the 1,032 distinct item
  ids used as vendor costs have no TP price at all, covering 49% of item
  cost-line usages, so this is the common case rather than the exotic one.
  A barter line obeys exactly the same rule a non-coin currency line does:
  with a valuation it folds into the offer's comparison value (never into
  the committed coin cost); with none it makes the offer fallback-tier.
  Either way the offer survives - dropping it reported "no vendor route"
  for items that are genuinely purchasable, just not with gold. Curated
  per-item defaults live in `Models/BarterItemDecisionDefaults.cs`, the
  Item-keyed twin of `CurrencyDecisionDefaults`, and an item with no entry
  there simply stays unvalued and fallback-only. A committed decision that
  is paid partly in barter is flagged `VendorHasBarterItemCost` on both
  `PlanStep` and `CraftingTreeNode`: it is the twin of a non-empty
  `VendorCurrencyCosts`, and every consumer that reads a coin figure as
  the whole cost must check both.
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

That probe answers "does a recipe exist", not "what does it consume": it
only ever fetches ids the corpus lacks, so a recipe whose id never moved
but whose ingredients changed in place would be served stale forever.
Recipe 14025's rift essences turning from items into wallet currencies
(KNOWN-ISSUES #48) is the case that actually happened. A second, lazy
phase closes it - `Services/Recipes/RecipeCorpusRefresher.cs` refetches
the content of every held positive recipe once per game build, in batches
of 200 with a pause between them, and stores what comes back rather than
diffing to decide whether to believe it. The response IS the current
shape; the one comparison in that class decides only whether the row needs
*writing*, which keeps the overlay from becoming a 10 MB duplicate of the
shipped seed. It walks ids ascending after a priority pass over the recipes
reachable from the Ranker watchlist, the restored plan and plan history
(`Services/Recipes/PriorityRecipeIds.cs`), so an interrupted sweep resumes
from a single cursor in the overlay manifest and has already repaired what
the user was most likely to hit. Nothing waits on it: the verifier licenses
negatives, this repairs positives, and plan generation uses the best corpus
it has while this improves it underneath.

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

## 12. Plan persistence compatibility: the request/result split

**The contract.** A plan file written by *any* shipped build must remain
loadable by *every* later build, with graceful degradation of the parts
that cannot be read. Concretely, in the order the guarantees weaken:

1. The **request** - the items, quantities, `UseOwnMaterials`,
   `PriceBasis`, `ValueOwnMaterials` and ignored ids - always survives.
   It is versioned by `PersistedPlan.RequestSchemaVersion`, and that
   version has never been bumped.
2. The **result** - `Result` and `NodeOverrides`, the whole solved tree
   with its prices, offers and metadata - survives only when
   `PersistedPlan.SchemaVersion` matches this build exactly. Anything
   else discards the result and keeps the request.
3. Nothing is ever *partially* restored. A degraded result is discarded
   whole; the module never renders half a plan.

What a schema bump costs a user is therefore one click, not their plan:
the tab comes back with their items and settings, and Generate Plan
re-solves them at current prices. Before this split, a bump cost every
saved plan on every user's disk, which is why the version had been left
stale at 2 for the whole of a ~275-line graph change.

**Why the layers can be read apart.** The document is one JSON object
with one set of property names - there is no second file and no second
on-disk shape. `PlanStoreHelpers.DeserializeRequestLayer` binds
`PersistedPlan` through a contract resolver that marks every member in
`ResultGraphMembers` as ignored, so Json.NET *skips those tokens without
binding them to a type at all*. That is the whole trick, and it is why
the split is not tolerant deserialization: the result graph is not read
leniently, it is not read.

`NodeOverrides` sits with the result rather than the request because it
is keyed by solver `NodeId`, which is meaningful only inside the tree
that produced it. `IgnoredItemIds` is keyed by item id and so belongs
with the request - the same line `PlanHistoryEntry` already draws
between its index row and its blob.

**Plan History was already split** along the same line, across two files
rather than within one: the index row in `plan_history.json` carries the
request identity, and the expensive result lives in a per-entry blob. A
`PersistedPlan` bump therefore already discarded blobs and kept rows.

**The index answers the same contract by a different mechanism.** It is a
*collection*, so its compatibility unit is the row, not a layer - there is
no cheaper half to fall back to, and a user who loses 200 saved plans is
no happier than one who loses one. Two things make a row survive:

- `PlanHistoryStore.Load` accepts any file stamped in
  `[PlanHistoryIndex.MinimumReadableSchemaVersion, CurrentSchemaVersion]`
  and `Save` restamps it. Exact-match rejection was what made a bump cost
  the whole history; a range costs nothing, and needs no migration code
  because there is nothing to migrate.
- That range is only safe because the row graph is **additive-only**.
  `PlanHistoryIndex.SchemaShapeHash` is what holds it to that: a rename,
  removal or retype anywhere reachable from `PlanHistoryIndex` moves the
  hash and cannot land without editing the line next to both version
  constants. An addition is free - Newtonsoft leaves an absent member at
  its default, so every existing row still loads.

`MinimumReadableSchemaVersion` is therefore the single constant in the
module whose value *is* the amount of user data a release destroys. It is
1, and it is pinned twice - by `PlanHistorySchemaMemberSetTests` and by
the CI corpus step - so raising it is a deliberate, reviewed act and never
a side effect of a merge. A newer-than-current file is still discarded:
this build cannot know what a later one wrote, which is the same answer
the plan gives to the same question.

**What enforces it.** `tests/shared/plan_fixtures/` holds one serialized
plan per shipped schema version and one index per shipped index version,
captured from the real serializers, plus a hostile fixture whose entire
result graph has been renamed out from under the loader.
`tests/TaimisToolbench.Tests/Services/PlanCompatibilityFixtureTests.cs`
loads every one of them through the real `PlanStore`; the "Saved plans
from older builds still load" step in `.github/workflows/tests.yml`
checks the corpus is complete. Adding a fixture is one command, described
in that directory's own `README.md`.

**Where:** `Models/PersistedPlan.cs` (the two versions and which member
belongs to which layer), `Models/PersistedPlanLoad.cs` (what a read
returns), `Services/PlanStoreHelpers.cs` (both readers and the resolver),
`Services/PlanStore.cs` (the two severities and the "nothing restorable"
case), `Views/CraftingPlanView.cs` (`ApplyRestoredRequest`, the
request-only restore), `Models/PlanHistoryEntry.cs` and
`Services/PlanHistoryStore.cs` (the index's readable range and the
additive-only row graph behind it).

---

## V. Views: relocated design narrative

The `Views/` tree carries a lot of hard-won reasoning: decompiled Blish HUD
1.3.0 behaviour, pixel simulations, bug post-mortems, and the arguments
behind choices that look arbitrary from the outside. That reasoning is
worth keeping, but a forty-line XML doc comment stops being read. This
section is where the derivations live. Each member's own doc comment keeps
the part a caller can violate - the invariant, the measured constant, the
Blish quirk you need to know to use it - and points here for the rest.

The subsections below are ordered by file, top-level `Views/` first, then
`Views/Rendering/`.

### V.1 `AboutTabContent`: rebuilt per visit, and the SemVer reflection

The About tab is the same shape as `LogTabContent`: one
`FlowPanel(CanScroll)`, a `Build(Container)` that populates it once, and no
relayout registry. Nothing on it is interactive beyond plain
selectable/copyable text, so there is no state worth keeping "sticky"
across a tab revisit and the rebuild-per-visit cost buys correctness for
free. `MainView` carries the cross-cutting note on that rebuild policy.

The manifest read cannot fail under normal operation: `ModuleParameters.Manifest`
is the exact object Blish HUD itself already parsed and validated in order
to load this module at all. The hand-parse of the packaged `manifest.json`
exists for the cases where it somehow does anyway - a null `Manifest`, an
unexpectedly blank `Name`, or any exception - and mirrors the
try/catch-with-graceful-fallback shape `Module.Initialize()` already uses
four times for seed files.

`Manifest.Version` and a dependency's `VersionRange` are typed
`SemVer.Version` / `SemVer.Range`, from the external "SemVer" NuGet package
Blish HUD embeds via Costura at runtime. This project has no compile-time
reference to it, so a direct property access does not compile. Reflection
(`ToString()` only) avoids adding a package reference for a two-field,
display-only read.

### V.2 `ApiAccessDialog`: why not a generalized `ModalDialog`

It follows the same `StandardWindow` construction technique as
`ModalDialog` - a 1x1 pixel background stretched to the window's own size,
`TopMost`, a stable `Id`, `Show()`/`Hide()` semantics - but is a separate
class rather than a generalization of it. `ModalDialog`'s shape is one
short sentence, a fixed "Confirm" title, and a caller-named confirm button
beside a fixed Cancel; this dialog is a multi-line numbered checklist under
a different title with a Retry/Close pair. `ModalDialog`'s message `Label`
is also not wrapped at all, which is fine for its own short sentence and
wrong for full-sentence checklist items.

It deliberately skips `ModalDialog`'s settings-backed drag position
persistence. This is a rare error-path dialog, not a workflow a user
repeatedly opens and repositions, so it simply centers on every `Show()`
and needs no new `ModuleSettings` entries.

The failure it explains is real and was reported: at character select,
Blish has not yet resolved the game's Mumble identity, every account data
source call fails with an invalid or missing API key, and the Snapshot
tab's Refresh Now used to show only the unhelpful "Refresh Failed -
{time}".

### V.3 `FocusRelease`: how Blish's focus slot gets orphaned

`TextInputBase.Focused`'s setter assigns
`GameService.Input.Keyboard.FocusedControl = this` on every change, a
change to `false` included. Blish itself soft-unfocuses in two places: the
click-away handler (`Focused = _mouseOver && _enabled`) and
`DisposeControl`. The second runs after `Control.Dispose` has already
cleared `Parent`, so a box disposed while focused leaves that one global
slot holding an orphan whose `GetAncestors()` is empty - which
`KeyboardHandler`'s ancestor-visibility sweep can therefore never heal.

A slot naming one box while another still holds focus is what the user
feels. Escape is consumed clearing the slot instead of the box, and
re-clicking the live box cannot repair it, because the setter's
change-detection skips the assignment when `_focused` is already true. That
is why the release has to go through `UnsetFocus()` and why it retries.

### V.4 `ItemInputRowStrip`: why it is not under `Views/Rendering/`

Its controls are `AutocompleteTextBox`, `SuggestionPanel` and
`FocusRelease`, all of which are `Views` types. Putting the strip under
`Views/Rendering/` would make that folder reference `Views` and reverse the
one-way dependency section 5 above states.

### V.5 `LogTabContent`: the three-column row split, and the follow poll

`LogRow` splits each entry into four controls (panel plus three labels)
where the previous shape used three, and pays one more `EllipsizeToWidth`
per row per refit. Both are bounded by the ring cap (2000) and by what the
filter admits, the refit loop is `SuspendLayout`-wrapped, and on a resize
the ellipsize half runs once per drag rather than once per drag event.

One divergence is accepted rather than fixed: timestamps do not align
pixel-for-pixel between an `[INFO]` row and a `[DEBUG]` one, because the
level word and the stamp share the Time label. Fixing it costs a fifth
control per row on the module's heaviest render path. The Tag and Message
columns - the two a reader actually scans - do align.

`PollForUpdates` is the "plus a poll" half of the refresh design in
[`dev/proposals/d2-log-system.md`](../dev/proposals/d2-log-system.md)
section 4.3, layered on top of the `TabChanged`-driven `Refresh`. That
design is also what calls for the append-only incremental update rather
than a full-rebuild `Refresh()` on every version bump.

### V.6 `MainView`: Clear Cache, Refresh Now, status, and the result repack

Interposing a confirm dialog in front of Clear Cache opens a window in
which a refresh can start, which the old single-click version could not:
Refresh Now disables Clear Cache for its whole duration, but not the
reverse. Both buttons are therefore disabled for the dialog's lifetime, and
because `Build()` recreates them on every tab visit - which would re-enable
them mid-dialog - the confirm also bumps `_clearGeneration`.

`RefreshNowAsync` is a method rather than an inline lambda because the
`ApiAccessDialog`'s Retry button invokes it too. Both entry points are
Blish UI event handlers (a `Click`, or the dialog's own `Click`-driven
Retry callback), so both always start on the main thread - the same
argument `CraftingPlanView.TriggerGenerate` makes about its own
confirm-modal callback.

`ApplyStatusDisplay`'s parentheses around the elapsed time are the method's
own, and they are what keep the age from reading as part of the timestamp
beside it ("Updated - Aug 15, 2026 3:41 PM (2m ago)"). The `_statusLabel`
now lives in its own full-width `_statusPanel` row beneath the header
rather than in the header's shared, button-crowded run, which is why the
composed text is not truncated: a full-width row is far less likely to run
out of space, and truncating a status message is worse than letting a rare
long one reach the edge.

`RefitResultRows` keeps the scroll position across a repack that KEEPS the
column count - the grid panel's width moves, its height does not. A repack
that CHANGES the column count writes a new grid-panel height, and Blish's
`Scrollbar` zeroes the scroll position a frame after any content-height
change (measured: KNOWN-ISSUES #55, "The grid panel holds its unfiltered
height"), so the list snaps to top. That is not defended against: this tab
has no scroll-restore machinery - `CraftingPlanView.PreserveScrollAcross`
is the module's only one - and a column-count change re-flows every row
anyway, so there is no old position left to hold.

### V.7 `ModalBackdrop`: what it is for, and what it must not block

Before it existed, a confirm was only visually on top: with the Clear Cache
confirm open, a click on the Crafting Plan tab's "+" add-row button behind
it still registered.

It covers the module window rather than the screen because a capturing
control also stops the GAME from seeing the click. A screen-wide blocker
would mean a confirm left open swallows every click in Guild Wars 2, which
is not a trade a HUD overlay should make for a two-button confirm. The
finding is about the surface the dialog belongs to, so that is exactly what
is blocked.

The Z-order arithmetic behind the lazy construction: a window's effective
`ZIndex` is `5 + Screen.WINDOW_BASEZINDEX + its rank among windows ordered
by (TopMost, LastInteraction)`, so it is not a compile-time constant and a
`TopMost` dialog can land exactly one above a non-`TopMost` module window.
On the tie that arithmetic can produce with the blocked window, the
sibling-index tiebreak in `Container.TriggerMouseInput` decides - which is
why the backdrop is constructed on the first `Show()`, after every window
exists, so it is always the later child.

### V.8 `SettingsTabContent.EnsureCurrencyRowIcon`: two different readiness tests

A currency row is held to the currency LIST having resolved; a barter item
row is held to the ITEM ID it resolved. The asymmetry is deliberate,
because the two fetches answer different questions. Once the currency list
has resolved, every currency row gets an icon control, and a currency the
list carries with no icon URL of its own gets `IconControls`' empty-slot
placeholder - which is the state it really is in. The item fetch answers
per id, so an id absent from the reply is one nobody has an icon for yet,
not one the API says has none; holding a barter row to "the fetch happened"
would draw the placeholder over the first case.

### V.9 `CraftingPlanView.ApplyRestoredPlan` and `RollBackFailedPlanRender`

`ApplyRestoredPlan` mirrors `TriggerGenerate`'s success-path shape: it adopts
the restored `result` as the override loop's baseline, restores the user's
prior decision-pill overrides, reseeds the request inputs (rows, checkboxes,
price basis) that produced the plan, resets section expansion, rebuilds the
view model, and seeds the status board with the staleness banner text.
`RestoreOverrides` is not optional there - see V.17 for what a restored
session loses without it.

Two narrow try/catches guard the restore. `PlanStoreHelpers`' tolerance gate
is only structural, so a degraded `plan.json` can still throw inside the view
model build or inside `RenderPlan` - the builder copies the tree by
reference, so a null child is only dereferenced when `RenderPlan` walks it.
`RollBackFailedPlanRender` is shared with `Build()`'s guarded tail so a
poisoned view model can never be committed on either path.

Each thing the rollback restores is on the list for its own reason:

- The tree controller's override/ignore/expansion baseline
  (`ResetForNewPlan(null)`) and its per-render tree state, because the
  restored result was adopted as `_lastResult` before the render was
  attempted.
- `_lastDebugLog` / `_currentPlan` / `_planGeneratedAt`, because a committed
  view model that cannot render would re-throw out of `Build()`'s tail on
  every later tab visit.
- `_contentPanel`'s children, because a mid-build exception can leave a
  partially-built plan parented in a live panel; `ResetContentPanelToEmpty`
  sweeps it.
- The status board's seeded staleness banner and its painted label text -
  both skipped when `ClearRestoredSeed` reports a real Generate has raced
  in, so a superseding generation's status is never clobbered.

The catch is `catch (Exception)` on purpose: the rollback is the load-bearing
part, and narrowing it would trade a vanished plan for a crash on every later
tab visit.

### V.10 `ApplyWheelWrapCorrection`: why cancel-then-direct-write

Verified against the decompiled vendored Glide: `TweenerImpl.Tween` registers
a new tween in the by-target dictionary synchronously, before returning - so
by the time this handler runs, the wrong duration-0 tween is already
registered and `TargetCancel` finds it immediately. `Tween.Cancel` nulls the
`"ScrollDistance"` lerper slot synchronously, so even an `Update()` that runs
before removal skips the write: the wrong step never lands, rather than being
canceled one frame late.

That is why the shape is cancel-then-direct-write rather than a counter-tween
or a deferred correction, either of which would add a wrong frame this
mechanism does not have. (`Scrollbar` itself never calls `TargetCancel`;
rapid `ScrollAnimated` calls overwrite each other via `Tween`'s default
overwrite parameter, an internal-only path.)

Section 2 above covers the vendored `WheelDelta` defect this corrects.

### V.11 `PlaceTreeToolbarRow`: the collapsed row, and publishing the cluster

The strip's arithmetic collapses the toolbar row entirely when it is hidden,
which puts its Y exactly on the status row. A full-height panel left there
would sit over the top few pixels of the scrollable content area, so a hidden
row is given zero height as well as `Visible = false` and cannot intercept
anything even if Blish's hit-testing ever stopped honouring `Visible`.

Publishing where the button cluster starts matters because the two clusters
share one row and only this method knows its width. A left cluster laid out
without that number is a left cluster laid out over the buttons - which is
exactly what the chips did before `TreeChipStripLayout.Fit` existed.

### V.12 `PreserveScrollAcrossResize`: why the reset lands a frame late

Confirmed by decompiling the vendor assembly
(`packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe`,
`Blish_HUD.Controls.Scrollbar` and `Panel`):

`Scrollbar.RecalculateLayout` caches
`_scrollbarPercent = ContentRegion.Height / containerLowestContent` and zeroes
`ScrollDistance` (and, via `UpdateAssocContainer`, `VerticalScrollOffset`)
whenever that ratio differs from the previously cached value.
`RecalculateLayout` runs from two places:

1. Synchronously, nested inside `Panel`'s own `"Height"` `PropertyChanged`
   handler - `UpdatePanelScrollbarOnOwnPropertyChanged` sets
   `_panelScrollbar.Height`, itself a `Control.Height` write that
   invalidates/recalculates the scrollbar. But .NET's `PropertyChanged` event
   fires BEFORE `Control.Size`'s own
   `OnPropertyChanged("Height", invalidateLayout: true)` call to
   `Invalidate()`, so this nested call runs before `Panel`'s own
   `RecalculateLayout` has refreshed `ContentRegion` for the new size, reads
   the STALE (pre-resize) `ContentRegion.Height`, and sees no change.
2. Once every real engine frame, unconditionally, from `Scrollbar.DoUpdate`'s
   own `Invalidate()` call. By the time THAT runs, `ContentRegion.Height` has
   already been refreshed - the panel's own `RecalculateLayout` ran
   synchronously earlier in the same `Height`-setter chain - so it now sees a
   genuine change and resets.

Net effect: the reset lands on a later real frame, typically the next one,
not synchronously inside the tick's `Size` write. This is the same
delayed-reset window `ApplySavedScrollSynchronously`'s class doc describes
for rebuilds, and the reason `StartScrollVerify` exists there.

A per-tick verify window is deliberately not used: it would spawn (or
cancel-and-replace) a `FrameTicker` on every single drag frame, and the
per-tick synchronous write already keeps each tick visually correct without
one. The bounded window is armed once, at drag settle.

### V.13 `ReplayRelayout` and `ResizeSettleStep`: the drag-frame budget

`SuspendLayout`/`ResumeLayout` around the replay is about comparison cost.
For a long shopping list or a deep tree, replaying dozens of per-row closures
in a single tick without it would trigger that many redundant full sibling
reflows in the same frame (the `O(rows^2)` risk raised as m2 risk 2). The
coalesced reflow is a no-op for vertical position anyway, because these
writes only ever touch `Width`/`X` - row heights stay fixed, and
`SingleTopToBottom` flow positions children from cumulative `Height`.

The perf caveat on `ReplayRelayout` is real and stated inline: this shape
replaced a ONE-TIME dispose+rebuild 150ms after the drag settled with a full
replay of `_relayoutActions` on EVERY real drag frame. That is a genuine
change in perf character, not just a different trigger, and the mitigation
above is reasoned rather than measured - no live drag-resize check on a
large, fully-expanded plan (deep tree plus long shopping list) has been
performed against a running Blish instance.

`ResizeSettleStep` defers only the MEASURE half, and only because
`MeasureString` is comparatively expensive to run on every tick across a long
list or deep tree. The visible cost of deferring it is small: truncated text
stays unchanged mid-drag and is corrected once the drag settles.

### V.14 `TriggerGenerate`: why the resolution await lives in the wrapper

The await could have gone inside `GenerateFromResolvedRows`, and that is the
version this replaced. It cannot: `IItemSearchProvider` may complete
asynchronously, Blish's host installs no `SynchronizationContext` (section 1
above), and everything after such an await would therefore run on a
ThreadPool thread - while the generate body touches controls from its first
line. Keeping the await in a thin wrapper puts exactly one marshal hop
between the resolution and a body that has no async seams of its own.

### V.15 `SetGenerateInputsEnabled`: the run it was added for

The Generate button used to be the only control disabled for the length of a
run, which left "Use Own Materials" clickable while a plan was still
generating - and its confirm callback starts another generation. Two runs
then shared one `ItemMetadataService`, which is a data race, and
`_generateSequence` does not help: it makes the last result win, it does not
stop the redundant work. `ItemMetadataService` is now internally locked
(`_cacheLock`), so this is no longer a crash guard; it is the single-flight
rule that stops the redundant run from starting at all.

### V.16 `SpinnerTick`, `CreateSectionHeader`, `CreateRequiredRecipesSection`

`PlanStripTickAction.RenderFinalAndStop` is what makes "the board reports
finished, so render final status and stop" true without any separate
completion-callback write into this control ever being needed. The tick reads
the board; nothing writes back into the tick.

`CreateSectionHeader`'s `suppressToggle`/`suppressPress` pair has exactly one
remaining user: Required Recipes' "Hide Unlocked" checkbox, the only
interactive control left in any section header now that the Recipe Tree's
five buttons moved to the non-scrolling strip (see `TreeToolbarCommands`).

Toggling that checkbox re-renders through `RenderPlan(_currentPlan)` - the
same full rebuild path a pill click's local re-solve and a fresh Generate
both already use - rather than inventing a second, parallel relayout
mechanism for one section.
