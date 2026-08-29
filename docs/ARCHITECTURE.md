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

**The second Windows defect, `MouseWheelScrollLines = -1`:** Windows' "one
screen at a time" mouse-wheel setting (Control Panel / Settings mouse wheel
option) reports `SystemInformation.MouseWheelScrollLines` as `-1`, not a
usable line count. Blish's own `Scrollbar.HandleWheelScroll` has the
identical defect - its `Math.Sign(...) * -30 * MouseWheelScrollLines`
scrolls the *wrong direction* for every wheel event, wrapped or not, under
that setting - and this module cannot fix Blish's arithmetic.
`WheelDeltaSanitizer.SanitizeScrollLines` substitutes Windows' documented
out-of-box default of 3 lines whenever the raw value is not a usable
positive count (covering `-1` and any other non-positive or unexpected
value defensively), which at least keeps *this module's* correction
pointing the right way. It deliberately does not try to reproduce Blish's
own step size under that setting, since Blish's step is itself wrong there:
direction-correctness is chosen over an unreachable exact-step match for
this one OS setting value.

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

**Why the correction is computed in pixel space:**
`Services/ScrollMath.ApplyPixelDelta` converts a scrollbar ratio to pixels,
applies the delta, and converts back, rather than working in ratio space
directly. Blish's own `Scrollbar.HandleWheelScroll`/`ScrollAnimated` operate
in pixel space (a fixed per-notch pixel step added to the current pixel
offset), so a correction expressed in ratios would not compose the same way
across a changing scrollable range.

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

### 7.1 Offer tiering, in full

`EvaluateVendorOffers` splits offers on their non-coin cost lines, and two
kinds of line obey one rule: a non-coin wallet currency line, and a
*barter* line - an `Item` cost line whose item has no Trading Post price,
which is what an account-bound vendor token is. An `Item` line that does
have a TP price is money, not a barter line: it folds into the offer's
real coin cost and never consults a valuation.

An offer is **comparable** (competes with TP/craft coin costs in
`PickCheapest`) when it has no non-coin lines at all, or every one of them
has a valuation. Its comparison value is then coin part +
`sum(count * copperPerUnit)` over those valued lines, reported via
`VendorOfferEvaluation.BestComparableValue`. The winning comparable
offer's real coin part and (if any) currency lines are reported
separately, via `BestComparableCoinCost` and `BestComparableCurrencyCosts`;
the valuation affects comparison only, never the amounts committed to the
plan. A barter line's own scaled quantity rides on
`BestComparableItemCosts` with a null `GoldValue`, for the same reason.

An offer with at least one non-coin line that has **no** valuation
(including when it is mixed with other, valued lines) is incomparable with
coin costs and is reported only as a **fallback**, ranked by lowest coin
part. A fallback coin-part tie is broken by unit count only when both
offers cost the same single non-coin line, kind included; ties across
different lines keep the first-listed offer, because ranking across them
has no exchange rate and their unit counts must never be compared.

A `DailyCap`/`WeeklyCap`/`SeasonalCap` never excludes an offer or affects
its tier - gw2efficiency only ever surfaces a cap as a post-solve notice,
never re-routing the tree - so both tiers carry the raw caps through for
`FinalizeVendorBatches` to check once against aggregate demand.

### 7.2 Why the ceil is merged, and why a conflict is not

The sum of independently-ceil'd per-occurrence costs overstates the true
cost whenever an item is needed via 2+ occurrences and bought via a bulk
offer, so `FinalizeVendorBatches` re-derives the cost from the aggregate
quantity and ceils once. It only does so when every occurrence resolved to
the identical winning offer: re-deriving one "true" cost across genuinely
different offers has no principled answer, so a `Conflict` step keeps
`AggregateStep`'s sum of real per-occurrence purchases - a deliberately
conservative fallback.

### 7.3 Allocating a corrected total back to occurrences

Without `AllocateVendorNodeCosts`, `CraftingTreeNode.SubtreeCost` (via the
public `Decisions` dict) kept showing the stale, per-occurrence-overcounted
sum after `FinalizeVendorBatches` had corrected only the merged
`PlanStep`/`currencyMap` view.

It touches only the stepKeys that method actually corrected -
`step.VendorOfferOutputCount > 0`, which is only ever set inside its
single-winning-offer branch and is 0 for the conflict/mixed-offer case.
Where occurrences disagreed on the winning offer, each occurrence's own
memo `TotalCost` is already individually correct (a genuinely different
real purchase), so redistributing a uniform rate across them would replace
correct values with a wrong blended one - the same reasoning
`FinalizeVendorBatches` itself applies to `step.TotalCost`.

The allocation is largest-remainder (Hamilton) apportionment, proportional
to each occurrence's own `Quantity` share of the step's total demand:
`floor(step.TotalCost * quantity / totalQuantity)` per occurrence, then the
leftover copper(s) - `step.TotalCost` minus the sum of floors, always fewer
than `occurrences.Count` - go one each to the occurrences with the largest
fractional remainder (numerator mod `totalQuantity`), ties broken by
first-seen (DFS) order for determinism. The allocated shares always sum to
precisely `step.TotalCost` (no drift, no invented precision) and any two
occurrences of equal quantity diverge by at most 1 copper. The multiply
widens to `decimal` so this holds unconditionally - no `long` overflow is
possible for any `step.TotalCost`/`Quantity` pair. A "last occurrence
absorbs the remainder" shape is not acceptable here: it dumps the entire
batch-overrun cost, unbounded for equal-quantity occurrences, onto
whichever occurrence lands last in DFS order.

A component leaf's raw `VendorItemCosts`/`VendorCurrencyCosts` are captured
pre-merge, per occurrence, and are *not* re-derived here - they can
disagree with the corrected share whenever a step merges 2+ occurrences.
The caller reads this method's outputs afterward to mark which decisions
must suppress component-leaf display, in
`FlagUnreliableVendorComponentCosts`.

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
uncompressed MonoGame XNB containers holding one `BitmapFontReader` asset
(lineHeight, then nine int32 per glyph region). Widths follow
MonoGame.Extended's own `MeasureString` rule, which is what a Blish `Label`'s
autosize calls. The parse reproduces, glyph for glyph, the figures published
in [`docs/research/minimum-window-width.md`](research/minimum-window-width.md).
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

## S2. Services Q-Z: relocated design narrative

Design narrative moved out of doc comments in `Services/` (files whose
names begin Q-Z) under CLAUDE.md's comment rule: the invariant a caller can
violate stays at the member, the derivation that explains it lives here.
Each subsection names the class and member it came from, so a reader who
arrives from the citation lands on the same argument the comment used to
carry.

### S2.1 Sell-side economics for a batch

**`SellSideEconomics.ApplyBatchSellSideEconomics`.** The batch rollup is
gw2efficiency parity work (KNOWN-ISSUES #25), and it diverges from gw2e's
own multi-item rollup - the `o()` function in the live app bundle, see
[`docs/research/m37-r2-batch-economics.md`](research/m37-r2-batch-economics.md)
sections 1.2 and 4.1 - in three deliberate ways:

1. **No craft-vs-buy filter.** Any requested root with a live TP sell
   price contributes its own `SellableQuantity`/`NetSaleValue`/
   `CraftingProfit` regardless of whether the solver bought or crafted it.
   This matches the module's already-shipped single-item
   `ApplySellSideEconomics` semantics, which has never filtered by
   craft-vs-buy - a flip/arbitrage number is still meaningful - and the
   research report's explicit recommendation (4.1.1) *not* to adopt gw2e's
   `craft === true` filter. Pinned by
   `MultiItemPlanTests.GenerateStructuredAsync_MultiItem_OneRootBoughtButTradable_IncludedInSum`.
2. **A crafted root with no live TP sell price contributes nothing** -
   excluded entirely, its revenue and its own craft cost dropping out
   together - rather than gw2e's silent "-cost" drag for an untradable
   crafted root.
3. **A single profit basis** (instant-sell/buy-order, via `SellInstant`),
   matching the single-item row. gw2e always shows a second
   sell-listing-basis figure this module has never surfaced.

**Why `MaterialOpportunityCost` is a single batch-wide sum.** `Reduce`
still runs on the entire wrapper tree before `Solve` ever picks buy vs
craft per root (see `GenerateStructuredMultiAsync`'s step ordering), so the
figure is one sum over the merged `UsedMaterials` list and is not scoped
down to the roots that end up contributing to the sellable totals. What
makes that safe is that `UsedMaterials` is itself decision-aware:
`InventoryReducer.Reduce`'s `zeroOwnedDecisions` guide, fed by a throwaway
zero-owned `Solve()` on the same unreduced tree, means a root the solver
decides to buy no longer has its never-crafted subtree's owned ingredient
stock recorded as "used" at all, so there is nothing left to deduct from
`CraftingProfit` for that root. The single-item path was updated to the
same guided reduction, so both behave identically. Pinned by
`MultiItemPlanTests.GenerateStructuredAsync_MultiItem_ValuedMode_MixedBuyCraftBatch_MaterialOpportunityCostNullForBoughtRootOwnedIngredient`.

**`SellSideEconomics.PerItemEconomics`.** `itemId` is passed explicitly
rather than read from `itemRoot.Id`. Both call sites already guarantee
`itemRoot.Id == itemId` by construction (`RecipeService.BuildTreeAsync` and
`BuildMultiItemTreeAsync`), but keeping it explicit means the struct never
silently depends on that invariant holding.

**Full history:** KNOWN-ISSUES item 25;
[`docs/research/m37-r2-batch-economics.md`](research/m37-r2-batch-economics.md).

### S2.2 Crafting Ranker: cascade, ramp, weights, cache

**`RankerPriorityCascade` - what the ledger tracks.** "What a plan takes
from you" is several different things, and the ledger keeps them apart:

- materials, from `CraftingPlanResult.UsedMaterials` - the solver's own
  post-solve consumption record, which already reflects every buy-vs-craft
  decision, including decisions caused by what was owned;
- currencies and coin, netted by the cascade itself, because the solver
  never consults the wallet (see `AccountCurrencyIndex`);
- daily-cooldown crafting actions, which are capped per **account**, so two
  items needing the same gated ingredient queue rather than run in
  parallel.

**`RankerReadinessRamp` - why the ramp is deep rather than pastel.** A
naive red-to-green ramp peaks at a bright yellow around the midpoint, and
white on bright yellow is illegible. WCAG's 4.5:1 floor for white
(`#FFFFFF`) means every colour on the ramp has to keep its relative
luminance at or under `1.05 / 4.5 - 0.05 = 0.1833`, which is a deep,
saturated ramp and is why the three anchors are dark for their hues.

Interpolation is in OKLCh, not in sRGB. An sRGB lerp between two saturated
hues cuts through the interior of the colour solid and desaturates on the
way - red to green goes through brown - because sRGB's axes are not
perceptual. OKLab (Bjorn Ottosson, 2020) is a perceptual space whose polar
form OKLCh separates lightness, chroma and hue, so walking the hue angle
keeps chroma up all the way across and the intermediate colours stay orange
and olive rather than mud. The 25% sample is `(159, 76, 0)`, a real orange,
which is the measurement that says the space is doing its job.

**`RankerReadinessRamp.Track` - the second contrast obligation.** The track
constant was first chosen against white alone. At `Rgb(38, 36, 34)` it
scored 1.05:1 against the panel behind it, while the panel's own texture
varies by 1.076:1 - so the track was literally less distinguishable from
the surface than the surface is from itself. Measured in game at 3440x1440.
Darker is the only direction that serves both obligations, and the current
value sits at 1.32:1 against the panel with pure black as the 1.42:1
ceiling.

**`RankerReadinessWeights` - the per-gate argument.** The weights are not
derived from each gate's magnitude; deriving them that way sounds
principled and is the exchange-rate trap in disguise. They are judgement
calls about substitutability, which is a property the game itself decides:

- A daily reset cannot be bought at any price. It is the only barrier with
  no substitute, so it takes the largest share.
- Coin is the bulk of the work and the one gate measured exactly, by the
  real solver at real prices. Equal claim on precision grounds; no better
  claim than time on difficulty grounds.
- Currencies are a real barrier measured only as within-currency ratios, so
  each point carries less information than a coin point. Weighted below
  materials for that reason, not because currencies matter less.
- A discipline is a hard wall - you cannot craft at all without it - but a
  short one next to a legendary's materials bill, and usually either
  satisfied already or cheap to satisfy. Non-zero because it is real; small
  because it is short.
- A recipe unlock sits on the same substitutability rung as a discipline: a
  hard wall, but most recipes are purchasable sheets or cheap unlocks, so it
  takes the disciplines weight rather than inventing a new tier. First call,
  reviewable like the others.

**`RankerResultCache` - why two sets.** The two comparison modes answer
different questions about the same rows, and a row's answer under one says
nothing about its answer under the other. Keeping only the last mode's set
made every toggle a full recompute, including a toggle straight back to
numbers the session had already paid for (owner ruling, 2026-08-27).

### S2.3 Column and section geometry (Q-Z)

**`RecipesColumnMath` - why discipline is a column.** The discipline used to
be a second `Caption` line *under* the recipe name, which forced the
section to carry two row heights and put a name and its discipline on
different reading lines. As a column, every recipe row is one line at
`PlanContentHeightMath.RecipeRowHeight`.

**`ShoppingColumnMath` - why the bands are distributed.** Distributing the
bands over equal tracks rather than packing them against the panel's right
edge is what stops a short item name being stranded far left with the
middle of the row empty. Each header centres over its band rather than
sharing an edge with it; `JustifiedColumnTracks` carries the argument for
why a shared edge is not enough.

**`SnapshotHeaderLayout` - what the shared row buys and costs.** The header
used five sparse rows to say what four can, and the widest of them - the
search row - was empty for everything right of the content-type dropdown.
Sharing that row halves the width the source-filter run has to flow into,
so a roster that used to fit inside the 4-row cap can wrap past it and hide
filters behind a scrollbar: a third of the filter set for 38px of header.
That is why the sharing is conditional on the whole run fitting in one row,
and why the fallback is exactly the full-width row the flow had before.

**`SummarySectionLayoutMath` - why it is its own class.** Its role is the
same kind of thing `Services/PlanContentHeightMath.cs` and
`Services/PlanRelayoutMath.cs` already do for every other section, and it
is deliberately kept out of both: they are shared infrastructure several
other sections' row builders depend on, and they are high-evidence zones
(see [`docs/KNOWN-ISSUES.md`](KNOWN-ISSUES.md#policy-high-evidence-zones)) -
off-limits for the broader fold-back this class's existence sidesteps.
KNOWN-ISSUES #46 carries the original rationale.

**`TreeChipStripLayout` - what the slot held before, and why zero hides.**
The slot the state chips occupy used to hold a grey "Recipe Tree:" caption:
small *and* grey, labelling five buttons whose own verbs and tooltips
already said what they act on. Real information replaces a caption that
named nothing. A chip is hidden entirely at zero rather than shown reading
zero because a standing "Overrides: 0" spends attention on the absence of a
thing, and a permanently-disabled clear button beside it invites "why is
this disabled?".

**`TreeToolbarRowLayout` - why the button widths live in the class.** A
width that can only be read off a `PlaceRight` argument is a width no test
can assert the boundary cases against without re-typing it, and a re-typed
width is one a later rename silently invalidates.

**`TreeCostColumnMath` - what the column looked like before.** It used to
right-align one ragged run per row: a gold/silver/copper row and a currency
row both ended at the same x but shared no interior alignment, so no two
coin icons in the whole tree lined up vertically. Scanning the whole tree
once per render pass costs one walk of an already-materialised tree and
buys a column that never shifts under the user; the node count the section
header shows rides on the same walk for the same stability reason.

**`UiSpacing` - the coincidences on record.** 8px also ships as
`LogToolbarLayout.Gap` and `LogRowLayout.RightPad`, and 20px as both
`SettingsFormLayout.SectionGap` (vertical, between section blocks) and
`TreeToolbarRowLayout.GroupGap` (horizontal, between button groups). Those
are coincidences, they stay where they are, and coupling them would make a
deliberate change to one silently move the others.

### S2.4 Tooltip text: wrap seams and scope vocabulary

**`ShoppingRowTooltipFormatter.BuildCurrencyLines` - why "THIS ROW" and
"wallet" are load-bearing.** Both numbers on a shopping-row currency line
are that *row's* own total (`cc.Amount`, one `PlanStep`'s
`VendorCurrencyCosts`), never the whole plan's requirement for that
currency id. Without a scope marker, two shopping rows drawing on the same
wallet currency - Karma split across two vendor rows, say - can each
independently read as "fully covered" and double-count the one wallet
balance. That is the same misreading class `DecisionPillPlanner`'s
plan-scope `HAVE {have}/{planTotal} TOTAL` pill
(`AppendCurrencyOwnershipPill`) exists to avoid, via its own explicit
"TOTAL" suffix; "THIS ROW" is the row-scope mirror of that convention, and
the vocabulary must never look plan-scope when it is not. The "(wallet N)"
aside is worded the same way for the same reason: "wallet" is the one term
this codebase uses for a raw account-wide holding figure, matching the
Summary column-header table's "Have" column and the tree's
`HAVE x/y TOTAL` pill.

**`TooltipLayoutMath.ItemTooltipMaxContentWidth` - the corpus.** The cap is
derived from the game's own break decisions rather than from a game-pixel
cap converted by a scale factor, because a mean font ratio hides a real
per-string spread (0.99x to 1.03x at Menomonia 14): `LetterSpacing = -1`
tightens tracking on a face whose glyph boxes are already wider than the
game's, so how a given string lands depends on its letter count as much as
its length. Each live capture that wraps a paragraph pins the cap twice -
it must be at least the width of the line the game *kept whole*, and below
that line plus the word the game *pushed down*. Measured through this face,
in this constant's units:

- Gift of Twilight 19648: 320 kept / 381 with "Twilight." pushed down; its
  "Made by combining these items in the Mystic Forge:" line, 359, stays
  whole.
- eyes-of-kormir 83103: 354 kept / 415 with "because"; 357 kept / 400 with
  "under".
- heart-of-destroyer 67017: 330 kept / 408 with "Bloodstone"; 372 kept /
  442 with "Destroyer".
- fury-scorched 86967: 406 kept / 430 with "for" - the one outlier.

Every constraint but fury's intersects at [372, 381); 376 is its midpoint,
so no decision sits within 4px of flipping. Fury's kept line would need a
cap of 406+, which would un-wrap Gift of Twilight *and* eyes' second line,
so it loses 1 constraint to 5.

**`TooltipTextFormat.LineBudgetChars` - why 71, not the shipped 75.** Every
prose string of 55 characters or more that this module builds (73 of them,
swept out of `Services/` and `Views/`) was measured against the installed
Menomonia 14 XNB with MonoGame.Extended's own advance / `XOffset+Width`
rule - the same parse behind
[`docs/research/minimum-window-width.md`](research/minimum-window-width.md).
They average 7.03px per character, so 500px is 71 characters, not the 76 the
original 6.5px/char estimate assumed. Per-string the spread is
6.7 to 7.5px/char, so prose at the wide end still crosses 500px inside a
71-character line; Blish's own space wrap takes those, which costs a break
it would have made anyway and never loses text. The one case only this seam
handles - a single token wider than the cap, which Blish's wrapper will not
split - is hard-cut by `TextWrapMath` before the budget matters.

The budget is a **character** count, not pixels, because a tooltip string
is composed in `Services/`, far from any font; the alternative - threading a
measured `Func<string, int>` down from `Views/Rendering/` - would put a
Blish dependency on the very seam the class exists to keep Blish-free.

**`TreeRowTooltipComposer.BuildExtraTooltipContent`.** The returned content
is computed once and reused verbatim by `RenderTreeNode`'s
`extraTooltipLines`. There was once a second entry point returning
pre-wrapped strings; nothing called it and it is gone.

**`ValueDetailTooltipBuilder`.** The hover template is duplicated verbatim
from gw2efficiency's own crafting-pill hover, and the class is kept
Blish-free - unlike `TreeSectionController`, which only calls it and assigns
the result to `BasicTooltipText` - so the text-building logic is directly
unit-testable, matching this repo's established pattern for tree-rendering
logic (`DecisionPillPlanner`, `CoinSegmentMath`, and the rest). The
divergence it surfaces can be an unpriceable descendant's own divergence
rolled up recursively; see `DecisionValue`'s own doc comment.

### S2.5 Account-snapshot concurrency and search

**`SnapshotCommitGate` - what `SnapshotEpochGuard` alone left open.** The
original KNOWN-ISSUES #31/31a-F1 fix captured `myEpoch` before a snapshot
fetch's await and re-checked it afterwards against a bare
`volatile int _snapshotEpoch`, with the field commit that follows (write
`_currentSnapshot`/`_pendingSnapshot`/`_snapshotDirty`, save to disk) as
several more unguarded instructions after that check. `Module.ClearCache`
bumps the same epoch and nulls those same fields with no synchronization of
its own. The check and the commit were never atomic with respect to
`ClearCache` - just narrowed from "the whole fetch" down to "the few
instructions between the check and the last field write" - so a Clear Cache
landing in that gap could still resurrect a just-cleared snapshot, or leave
the three fields in a combination that never legitimately occurs. The gate
closes the gap for real by putting the bump and the re-check under one lock.

**`SnapshotRefreshSlot` - what the check-then-set gate cost.** Three threads
reach `Module`'s two refresh entry points - `LoadAsync` on a ThreadPool
task, `Update()` on the main thread, and `OnSubtokenUpdated` on a thread the
module does not control - and both entry points used to gate on a
check-then-set over a `volatile bool`. Volatile makes a write *visible*; it
does not make check-then-set *atomic*, so two entrants could both get past
it. Each would then run the same three-statement sequence (cancel the live
source, dispose it, assign a fresh one) and each could dispose the source
the other had just published, after which the loser's own
`_refreshCts.Token` read threw `ObjectDisposedException` - or
`NullReferenceException`, if a Clear Cache click nulled the field in the
same window. `Module`'s generic catch reported that as "refresh failed" and
armed a 60-second retry backoff for a call that never reached the network.

**`SnapshotSearchResultBuilder.ShortQueryCharacterHint` - why the hint
exists.** The `MinCharacterSearchLength` hold-back is deliberate but
invisible: a one-letter query that a character's name does contain looks
like a plain no-results, so the user reads the tab as broken rather than as
waiting for a second letter.

**`SnapshotSearchResultBuilder.BuildItemRows` - inputs and cost.**
`itemsById` is the already-deduped itemId -> representative-entry map (see
`BuildRepresentativeIndex`); the method never re-scans the raw per-source
entry list itself, so it stays cheap to call on every keystroke as long as
the caller builds the map once per snapshot rather than once per call.
Character matching costs a full source walk for every item whose name does
not match, where a name-only search could skip straight past it; that is
bounded above by the empty-search rebuild, which already walks every source
of every item. The match is against character names only - storage-location
labels stay unmatched (Feature 1 Open Question 2, resolved in favour of
source-label matching). A row surfaced by a character match reports the
account-wide total rather than the matched character's share, so the total
keeps meaning the same thing on every row in the list.

### S2.6 Receipt captions and the multi-item tree wrapper

**`ReceiptCaptionHelper` - where the stacked shape comes from.** The stack
this helper detects is produced by one branch of
`CraftingTreeBuilder.BuildNode`: `componentLeaves != null &&
wantsReferenceBranch`. That branch synthesizes the cost-component leaves,
appends the reference branch's own recipe ingredients after them, and sets
`node.IsReferenceBranch` - which is why "IsReferenceBranch and the first
child is a cost component" identifies the case from the node alone, with no
new model field. The helper is Blish-free by design so it can be exercised
by a real test over plain `CraftingTreeNode` objects, independently of the
`Views/Rendering` pass that consumes it. The caution about never touching
`Children` is not stylistic: row heights flow through
`PlanContentHeightMath`'s tree arm, which counts exactly
`node.Children.Count` rows per level, so a caption rendered as an extra
row - rather than as an extra tooltip line on an existing child's row -
would desync a height the view assigns synchronously.

**`RecipeService.BuildMultiItemTreeAsync` - why a synthetic wrapper.** For
2+ items the per-item trees are wrapped under a synthetic root `RecipeNode`
the same way gw2efficiency's frontend does for its own Calculator (see
[`docs/gw2e-parity-spec.md`](gw2e-parity-spec.md)): a reserved-id,
never-rendered "recipe" whose `Ingredients` are the N real item trees, each
already carrying its own requested amount as its own `Quantity` (set by
`BuildTreeAsync` itself, exactly like an ordinary recipe ingredient's
quantity). Feeding that wrapper through the unmodified
`PlanSolver`/`InventoryReducer`/`CraftingTreeBuilder` pipeline is what gives
merged shopping-list, steps and currency totals for free, via the existing
per-item-id aggregation in `PlanSolver.Collect`'s `AggregateStep`: no
multi-item-specific solver logic exists, or is needed. The single-entry
short-circuit echoes gw2e's own `if (r.length === 1) return r[0]`.

### S2.7 Recipe-tree row identity

**`TreeRowIdentity` - why a shared `NodeId` is not enough.**
`RecipeNodeIds` gives a real recipe node a stable pre-order id, so there the
id does fix the item for the row's life. A vendor cost-component leaf's id
is `CraftingTreeBuilder.SyntheticComponentNodeId(parentNodeId,
componentIndex)` - the leaf's *position* in the chosen offer's cost lines -
while its name, icon and rarity come from that line's own `ItemId`. A
re-solve that picks a different offer of the same shape (`{item, currency}`
becoming `{other item, currency}`) keeps every id and every structural fact
and changes only which items the lines name, so an identity-blind refresh
would repaint one item's quantity, cost cell and tooltip under another
item's name and icon.

### S2.8 The re-solve status line

**`StatusText.ForOverrideResolve` - why the count left the line.** The line
used to carry the standing override count - "Decisions updated (3
override(s))" - which is a different kind of fact. How many decisions you
have overridden is the plan's *state*, true until you change it; this line
says what just happened and is replaced by the next thing that does. The two
are not connected, and a line that mixed them made the count vanish the
moment anything else happened. The count lives in the top strip's Overrides
chip now, where it persists and can be acted on.

### S2.9 Window placement and the measured width floor

**`WindowPlacement` - why the arithmetic is split out.** It is split out of
`Views/ResizableTabbedWindow` on the same terms as `WindowSizing` and
`PanelChromeMath`: the arithmetic is the part that has to be pinned, and a
Blish control cannot be constructed in a Blish-free test.

**What Blish's own clamp does and does not do.** `WindowBase2.Show` reads
the persisted position and applies `Clamp(x, 0, SpriteScreen.Width - 64)`
per axis (BlishHUD 1.3.0, decompiled). Nothing on that path consults the
window's size, so a position saved against a wide client leaves an arbitrary
amount of the window's right-hand side - cost column, Generate button,
resize grip - past the edge of a narrower one, with no way to drag it back.
A restored *size* gets no clamp at all on that same path, which is what
`ClampExtent` exists for.

**`WindowSizing.MinWindowWidth` - the term-by-term chain.** Measured at
Menomonia 16 against the installed XNBs
([`docs/research/minimum-window-width.md`](research/minimum-window-width.md)
section 9 reproduces the method and every anchor figure of that report's own
1478-era derivation):

```
 629  widestNameEnd  = nameX(14) 394 + "429750x " 69 + name 166
 +24  the designed name-to-pill gutter at the deepest row
+256  TreePillColumnWidth
+335  cost column: 181 worst-digit six-digit-gold coin run
                   + 154 widest two-currency vendor run
  +8  TableRightMargin
---- 1252  tab panel
+126  WindowToTabPanelChrome
==== 1378
```

1378, not the 1232 the like-for-like depth-14 arithmetic gives on its own:
1232 accepts that a row combining a forced-craft dust chain with a vendor
currency run ellipsizes, and the maintainer declined that trade - "we are
designing for a minimum resolution of 1920x1080, so cramming down to a
smaller min-size that will result in cramped renders seems bad". The +154
rider is what buys "a two-currency vendor run always fits at the floor".

Down from 1478, which fitted the depth-23 "+24 Agony Infusion" chain
untruncated. That chain now ellipsizes from depth 20 - six levels past the
deepest realistic plan, and exactly the idiom of record everywhere else in
the view (ellipsis, full name on the tooltip).

The other contributor to this floor is the controls row, which is subsumed:
its widest arrangement is the "Value Own Materials" checkbox at x=350 (its
label measures 145px at Blish's own Font14, plus the box) clearing the
right-anchored 120px Generate Plan button and `WindowToTabPanelChrome`'s
trailing padding - under 700px all told, half of what the tree needs.

### S2.10 Wiki link launch

**`WikiLinkLauncher` - the first external-URL launch.** This is the module's
first launch of an external URL, a deliberate maintainer decision. The
try/catch exists because ShellExecute can throw for reasons outside the
module's control - `Win32Exception` for no registered URL handler, a
locked-down environment, and so on. The `Task.Run` offload was a later
fix-pass: `ShellExecuteEx` blocks the calling thread until the shell hands
the URL off, and a cold browser start, DDE negotiation, or a "choose an app"
prompt can stall that call for hundreds of milliseconds to seconds, freezing
the whole overlay - scroll and relayout included - for as long as it runs.

### S2.11 Recipe corpus refresh

**`RecipeCorpusRefresher` - the case that motivates it.** Recipe 14025's
rift-essence ingredients turned from items into wallet currencies without
the recipe id changing (KNOWN-ISSUES #48), and `RecipeCorpusVerifier` cannot
see such a change because it only ever fetches ids the corpus lacks. The one
comparison the refresher does make - is the fetched row identical to the
seed's? - exists to keep the overlay from becoming a 10 MB duplicate of the
shipped seed for no gain.
