# Known Issues

This is the short, current-state issue tracker for GW2 Crafting Helper. It
holds:

1. The **numbered issue catalog** below - every issue this project has ever
   logged, kept under its original number, with a short resolution
   summary. `.cs` source comments cite these as `KNOWN-ISSUES #N` (or a
   decimal sub-item like `#20.1`); this file is where `#N` still resolves.
2. The **[DEFERRED](#deferred-recorded-not-implemented)** list at the
   bottom: items that are genuinely still open.

**Looking for the full investigation?** Every item below - hypotheses,
instrumentation, root-cause traces, live-verification transcripts, and
dated gate PASS records - has its complete original write-up in
[`docs/dev-notes/HISTORY.md`](dev-notes/HISTORY.md), under the identical
item number. This file only keeps the short version.

**Looking for *why* a piece of code is shaped the way it is**, rather than
the story of how a specific bug was found and fixed?
[`docs/ARCHITECTURE.md`](ARCHITECTURE.md) distills the durable rationale
for the handful of mechanisms (scroll preserve/restore, the resize
relayout registry, the wheel-delta sanitizer, merged-ceil vendor batching,
solver decision rules, and so on) that this history produced.

---

## Numbered issue catalog

### Items 1-11: M30 wave (initial hands-on pass, post-M29)

All eleven were fixed in M30. Briefly: (1) pill-click scroll reset -
restore/verify is now driven by a per-frame `FrameTicker` rather than a
same-frame `QueueMainThreadUpdate` re-queue. (2) resize-drag flicker/
transient tree collapse - full dispose+rebuild on every drag tick replaced
with a 150ms trailing debounce. (3) Total Cost currency rows gained icons.
(4) more vertical spacing between major sections. (5) collapsed Recipe
Tree containers now contract correctly (no stale whitespace). (6) Shopping
List Amount/Each/Total columns gained inter-column spacing. (7) row
dividers now use a consistent higher-contrast color. (8) Ball of Dark
Energy (and four HoT map-completion Gifts) - unpriceable items now render
a wiki-verified acquisition-hint tooltip instead of a misleading "only
available source" message. (9) rarity text brightened for parchment-
background contrast. (10) window content region resized to 684px high /
26px margin to stop texture bleed-through. (11) decision pill labels now
render in white instead of their own border color.

### 12. Fast wheel-up scroll: net-downward stutter (FIXED in M33, reopened and root-caused in M36)

Rapid wheel-up bursts could scroll up then jump back down further than
they went up. Root cause (M33): the old scroll-verify loop required
content height to be stable frame-to-frame before trusting the scrollbar's
live value, and nested `AutoSize` convergence kept height fluctuating for
several frames after every rebuild - closed by making container heights
synchronous (`PlanContentHeightMath`) and having the verify window yield
immediately on any observed wheel event. Reopened in M36: a real defect in
the *shipped* Blish HUD v1.3.0 binary (`MouseEventArgs.WheelDelta`
mis-unwraps a Windows-coalesced multi-notch wheel event as a huge negative
delta) was root-caused by decompiling the vendored binary and fixed
module-side with `WheelDeltaSanitizer`. See `docs/ARCHITECTURE.md`
sections 1-3.

### 13. Resize UX rework: live reflow, no settle stutter (FIXED in M33)

Live in-place relayout during a resize drag (no dispose+rebuild), driven
by the `_relayoutActions`/`_reellipsisActions` registry described in
`docs/ARCHITECTURE.md` section 5.

### 14. Pill-click viewport flash (jump to top and back) (FIXED in M33)

Closed by the same synchronous-height-contract fix as item 12.

### 15. Shopping tag text contrast (VENDOR / SALVAGE / UNKNOWN)

Logged as a contrast follow-up to the M30 #11 pill-label fix (which only
covered tree pills, not shopping-list source tags). No explicit resolution
note was recorded against this item number in the original backlog; full
context in `dev-notes/HISTORY.md`.

### 16. Vendor-source items show no price

Logged: vendor-decision rows rendered empty Each/Total cells, including
non-coin currency costs. The currency-icon rendering pipeline
(`CoinCurrencyRenderer`, `CurrencyDisplayResolver`) that this item asked
for is in production use today. No explicit "(FIXED in Mxx)" note was
recorded against this item number in the original backlog; full context
in `dev-notes/HISTORY.md`.

### 17. Seed data gaps: false UNKNOWNs in the Exordium tree (FIXED in M33)

Three of four original hypotheses were wrong (confirmed by wiki research
and an offline Harness dump): most "false UNKNOWN" nodes were actually a
solver bug (stopped evaluating siblings after the first unpriceable
ingredient), not a seed gap. The one real gap - Mystic Clover had no
Mystic Forge recipe seeded - was added (wiki-verified EV pricing). A
stale, wiki-removed vendor offer for Gift of Battle was also identified
and later removed as a Wave B follow-up.

### 18. Multi-source decision display is inconsistent

Logged: a node priced via one source (e.g. TP) could render a pill for a
different source (e.g. VENDOR). The decision-pill-must-match-committed-
source invariant this item asked for is covered today by
`DecisionPillPlannerTests`. No explicit "(FIXED in Mxx)" note was recorded
against this item number in the original backlog; full context in
`dev-notes/HISTORY.md`.

### 19. Resize-drag scroll reset on height change (FIXED in M33)

Part of the same synchronous height-contract / relayout-registry fix as
items 12-14.

### 20. M34: gw2efficiency owned-materials parity + correctness fixes

- **20.1** Obsidian Shard 179x showing Total 186 (not 180): a merged-ceil
  vendor-batch rounding bug, fixed - see `docs/ARCHITECTURE.md` section 7.
- **20.2** Vendor purchase caps no longer hard-exclude an offer; they now
  produce a warn-only notice instead of removing the offer from
  consideration.
- **20.3** Stale "Building final result..." status race between two
  independently-scheduled callbacks, fixed by `StatusUpdateGuard` - see
  `docs/ARCHITECTURE.md` section 6.
- **20.4** Owned-materials parity scope: per-node owned-quantity
  attribution, primary-option-only pool consumption, the "Value Own
  Materials" force-buy pre-pass (gw2e's `valueOwnItems` rule), owned
  currency as a display-only annotation (never fed back into the solve),
  the "Using N owned" pill, and the Ignore-pill mechanism (zeroes a node's
  own cost contribution tree-wide by item id, deliberately not cascading
  gw2e's full quantity re-derivation - an explicitly recorded, narrower
  substitute).

### 21. M35: gw2efficiency parity - multi-item plans

Multi-item batch planning: a synthetic wrapper recipe prices N selected
item roots together in one pass (Services layer), with a multi-row UI
(Views layer) showing each root's own decision. Several deliberate,
recorded divergences from gw2e's own multi-item UX (no per-node "Crafting
Profit" pill on individual roots yet; no drag-to-reorder rows).

### 22. Ignore-pill click sets status to "Best path restored" (cosmetic) (FIXED in M37, see #27)

### 23. Horizontal dividers appear/disappear with scroll position (FIXED in M36; M36b follow-up for 44px/32px rows)

Root cause: Blish's `Container.Paint` clips a divider drawn exactly at a
row's bottom edge unpredictably depending on scroll phase. M36b gave
`CreateRowDivider` a `bottomClearance` parameter (1 extra logical pixel of
gap for the vulnerable 44px/32px row types), proven immune by simulation
across every row height and all four GW2 UI Size scale factors - this is
DO-NOT-TOUCH machinery, see `docs/ARCHITECTURE.md` section 3.
Required Recipes and Crafting Steps were live pixel-scan verified at
multiple scroll offsets after the fix; Required Disciplines (32px rows)
was simulation-proven at the time but not yet individually pixel-scanned
in that same pass - **this gap was closed by item 30 below** (M37 live
desktop session, 2026-07-22, scanned Required Disciplines directly and
confirmed the same clean result the simulation predicted).

### M38 view-decomposition entries (WP-21 through WP-25)

Not separately numbered in the original backlog (recorded there as
`WP-22`/`WP-23`/`WP-23b`/`WP-23c`/`WP-23d + WP-24`/`WP-25` narrative
entries, plus the WP-26 cut decision). See `docs/ARCHITECTURE.md` section
5 for the durable summary of what moved where and why, and
`dev-notes/HISTORY.md` for the full diff-evidence and live-verification
record of each increment.

### 24. Homestead refinement handling (parity gap) (FIXED in M37)

The module's seed already carried all Homestead Refinement offer rows,
unconditionally, with no tier concept - so it silently priced every
account as if it owned every efficiency upgrade at every station (the
opposite of gw2e's conservative tier-0 default), with no way to turn it
off. Fixed: a per-material (Fiber/Metal/Wood) tier setting
(`HomesteadEfficiencyTiers`, default tier 0, matching gw2e's own default),
wiki-scraped `HomesteadTier` tagging on the relevant vendor offers, and a
solver-side filter that excludes any offer above the configured tier.
Deliberately NOT implemented: a "do you even own Homestead" master gate
(gw2e has none either) and the Homestead Black Market purchase path
(wiki game-id resolution gap, still unseeded).

### 25. Multi-item sell-side economics (parity gap, deliberate M35 gap) (FIXED in M37)

M35 left sell-value/profit rollups unset for multi-item batches. Fixed:
`SellSideEconomics.ApplyBatchSellSideEconomics` sums each qualifying
root's own sellable quantity/net sale value/profit into batch totals.
Deliberately diverges from gw2e in two ways (both recorded in
`dev-notes/HISTORY.md` with the reasoning): the rollup does not filter
out a bought-but-tradable root the way gw2e's `craft === true` filter
does, and an untradable crafted root is excluded entirely (contributes
zero) rather than silently absorbed as a hidden cost the way gw2e's own
upstream code does it.

### 26. Achievement-bit ingredient dedup (parity micro-gap) (FIXED in M37)

gw2e de-duplicates ingredients tagged with an achievement-reward
`achievement_bit` across the whole tree (relevant to 7 recovered custom
recipes - WvW siege-weapon-blueprint achievement rewards). Ported 1:1 via
a new `AchievementBitDedupPrePass` (pure, Blish-free), run once per fresh
tree build. A `DecisionPillPlanner` "COUNTED ELSEWHERE" pill replaces the
plain HAVE pill on a deduped node. Two related latent bugs found and
fixed during this work: a zeroed-but-mismatched-source node could leave a
standalone zero-cost ghost row in the plan (general `PlanSolver.Collect`
guard added), and new achievement/merchant recipe ids adjacent to the
Mystic Forge id range could NRE on a cold cache miss (fixed with a real
membership check).

### 27. Ignore-pill click status label (FIXED in M37, closes #22)

Root cause: the status-text choice used "did any overrides exist" as a
proxy for "was this the Best Path preset", so the Ignore toggle (which
never touches overrides) incorrectly printed the Best Path preset's own
label. Fixed with an explicit `isBestPathPreset` flag threaded to a new
`StatusText.ForOverrideResolve` helper.

### 28. Vendor cap data seeding + stale-offer sweep (PARTIAL - core FIXED in M37; gaps deferred)

Seeded real daily/weekly purchase caps for 689 of ~53,530 vendor offers
from wiki SMW properties (previously all null, making the M34 warn-only
cap machinery inert). A scoped stale-offer sweep (offers reachable from
seeded recipe trees only, ~10.2% of the total) found 2 genuinely
discontinued offers (removed, wiki-confirmed via patch notes) out of 394
raw candidates - the rest were scraper coverage gaps or wiki page
renames, not real removals. Character/total purchase caps remain
unseeded (no account/character concept exists in this module). See the
DEFERRED list below for the specific named gaps (Skirmish Merchant page
split, one unresolvable vendor-page rename, the wiki-drift superset).

### 29. Owned-materials UI live verification (VERIFIED in M37, live desktop session 2026-07-22)

### 30. Required Disciplines divider pixel-scan (VERIFIED in M37, live desktop session 2026-07-22)

Closes the gap item 23 left open: Required Disciplines (32px rows) was
scanned directly at two scroll offsets and matched the simulation's
predicted clean result exactly.

### 31. Concurrency and degradation audits (verification debt) (FIXED in M37)

Three formal audits (cross-thread await marshaling; offline/API-down
degradation per endpoint; price-cache thread-safety), 9 confirmed
findings fixed. Highlights: a Clear-Cache-races-a-background-refresh
resurrection bug (fixed with a new `SnapshotEpochGuard`); a validation
no-op that could permanently stick-disable the Generate button; a
snapshot-fetch path that silently returned an empty, fully-timestamped
snapshot on total failure instead of throwing (now throws
`SnapshotFetchFailedException`, never persisted); several unguarded batch
loops that aborted an entire fetch on one bad batch instead of degrading
per-batch; a price-fetch stampede under overlapping generate calls (fixed
with in-flight fetch tracking in `TradingPostService`).

### 32. StyleCop analyzer debt: SA1101/SA1200 suppressed, smaller rules tracked (M38 WP-02)

A code-review finding caught that two default-severity rules (SA1101
"prefix local calls with this", SA1200 "using directive must appear
within the namespace") were 61% of every warning the WP-02 ruleset
produced, fighting an established codebase convention rather than
catching drift against one. Both suppressed with the same inline-rationale
style as the pre-existing SA1309 suppression. Three smaller rules with a
non-trivial footprint (SA1413/SA1516/SA1503) are left enabled and recorded
as a future cleanup candidate.

### 33. Astral Acclaim package: Wizard's Vault seasonal purchase cap seeding (2026-07-22)

Seeded the wiki's "Has seasonal purchase cap" SMW property (item 28's
deferred fifth cap type) for Wizard's Vault offers, and wired a new
`TimegatedCapType.Seasonal` through `VendorBatchSolver.FinalizeVendorBatches`
so a seasonal cap produces its own warn-only notice independently of
daily/weekly. Confirmed Bag of Coins (1 Gold) is genuinely tiered (two
separate vendor offers/game ids, not one row with two prices) - no
model/solver change needed, the solver's existing cheapest-offer selection
already handles it. One offer (Lesser Essence of Gold) hit a reproducible
SMW property-chain resolution gap and was left byte-identical rather than
guessing its game id.

### 34. Dead live-wiki vendor-offer resolver removed (M38 WP-10)

`VendorOfferResolver`/`IWikiVendorClient`/`WikiLookupOptions` and their
test double were deleted: `Module.cs` always constructed the pipeline with
`resolver: null`, so every branch guarded on it was unreachable by
construction. Superseded by the static seed-store path
(`VendorOfferLoader`/`VendorOfferStore`, populated offline). Recorded
dissent: the architecture analyst argued for keep-and-comment instead of
delete; deletion was chosen (also removed the test suite's single largest
real-time cost - retry/backoff `Task.Delay`s). A clean `git revert`
restores the seam if a live-wiki resolve mode is wanted again.

### 35. M39 module log system: WP-16/WP-17 subsumption + JSONL/rotation rationale (FIXED in M39)

The M39 log-system PR fully implemented M38's planned WP-16 (persistence-
store failure logging, replacing silent `Debug.WriteLine`) and WP-17
(Module.cs catch-consistency + FrameTicker unload teardown) as a side
effect; this entry records that subsumption so neither gets duplicated.
Also records why the log store uses newline-delimited JSON rather than one
big JSON array (crash-safety: a partial trailing line is recoverable, a
mid-write corrupt array is not) and its two independent rotation caps
(2 MB size / 14 day age, both user-configurable and clamped against
out-of-range persisted values).

### 36. LogTabContent field crash: TabChanged raced Build() on two threads, corrupting a Queue<T> (FIXED, 2026-08-06)

**Field evidence:** a real user install crashed with `System.ArgumentException`
("Destination array was not long enough") from `Queue<T>.SetCapacity` <-
`Queue<T>.Enqueue` <- `LogTabContent.RebuildRows()` <- `LogTabContent.Refresh()`
<- Module.cs's `TabChanged` handler <- `TabbedWindow2.OnTabChanged` <- a
main-thread mouse click, while ThreadPool-thread WARN writes from failing
snapshot fetches (invalid API token) were landing in `ModuleLog` throughout
(`ModuleLog` itself is correctly synchronized - two-lock + `ConcurrentQueue`
- and was not implicated).

**Root cause:** Blish HUD's own `WindowBase2.ShowView` runs a tab's `Build()`
via `View.DoLoad().ContinueWith(...)` (docs/ARCHITECTURE.md Section 1) - with
no `SynchronizationContext` installed and no
`TaskContinuationOptions.ExecuteSynchronously` passed to `ContinueWith`, that
continuation is scheduled onto the ThreadPool, not the main/game thread,
regardless of whether `DoLoad`'s own task has already completed by the time
`ContinueWith` runs. (This mechanism was re-verified 2026-08-06 during this
fix-loop by decompiling the shipped Blish HUD v1.3.0 binary with `ilspycmd`
after a prior pass cited docs/ARCHITECTURE.md Section 1 for it without that
section actually containing it yet - see Section 1's own "Verified"
paragraph for the decompiled call chain. The underlying claim held up; only
the citation target was missing, and is now filled in.) `LogTabContent.
Build()`'s own tail called `RebuildRows()` directly on that ThreadPool
thread. Meanwhile, `SelectedTab` flips to the Log tab (and Module.cs's
`TabChanged` handler fires, synchronously, on the main thread) independently
of - and not necessarily after - that ThreadPool-scheduled `Build()` call
completing. `TabChanged` calls `LogTabContent.Refresh()` -> `RebuildRows()`
against the SAME `LogTabContent` instance whenever `_logContent` is already
non-null and its `_contentPanel` is already live, which `IsLive` alone does
not rule out mid-`Build()`. PR #99 (item 35's follow-up polish PR) had added
a `_buildComplete` latch guarding `PollForUpdates()` against exactly this
ThreadPool-vs-main-thread shape, on the assumption (recorded in that PR's
own review) that `TabChanged` could not fire before `_logContent` pointed at
a fully-built instance - but `Refresh()` itself never checked the latch, and
the field crash proves that assumption's ordering is timing-dependent and
CAN interleave. Two threads concurrently calling `Queue<(long,Label)>.
Enqueue` on the SAME `_renderedRows` field corrupted its internal
circular-buffer bookkeeping, producing the `Queue<T>.SetCapacity`
`ArgumentException` on a later `Enqueue`.

**Fix:** `LogTabContent.Build()`'s tail (the `RebuildRows()` call plus the
`_buildComplete = true` write) is now marshaled onto the main thread via
`MainThreadMarshal.Run` (`Views/MainThreadMarshal.cs`). `Refresh()`,
`PollForUpdates()`, the level-dropdown `ValueChanged` handler, the
search-box `TextChanged` handler, and `ClearView()` all now check
`_buildComplete` before calling `RebuildRows()` - a first fix pass only
gated `PollForUpdates()`/`Refresh()` and left the other three ungated (still
main-thread, so not a crash, but reachable while Build's body is still
executing and unable to state a real enforced invariant); a review pass on
2026-08-06 closed that gap. With all six `RebuildRows()` call sites either
gated on `_buildComplete` or IS the marshaled tail itself, they are
inherently serialized: a single thread cannot run two call stacks at the
same instant, so the race is impossible BY CONSTRUCTION, not merely guarded.
`_buildComplete` is KEPT (not removed as obsolete) - it no longer has a
cross-thread safety job, but still avoids a wasted, redundant `RebuildRows()`
pass on any of its five callers if they fire before Build()'s own queued
tail has landed; its `volatile` qualifier was removed since every read/write
is now main-thread-only. This does not make every field `LogTabContent`
touches main-thread-only - the eight control fields (`_toolbarPanel`,
`_levelDropdown`, `_searchBox`, `_followCheckbox`, `_clearViewButton`,
`_copyButton`, `_statusLabel`, `_contentPanel`) are still first published by
the rest of `Build()`'s body on the ThreadPool thread; every main-thread read
of one of them stays behind its existing null guard (`IsLive`, `_searchBox?`,
`_statusLabel == null`) rather than assuming it is already non-null - see
the corrected invariant note on `_buildComplete`'s own doc comment in
`Views/LogTabContent.cs`.

The same review pass found `MainView.Build()`'s own top-of-body
cancel-dispose-and-null-out of `_searchDebounceCts` (added by the
class-sweep fix below) had been left running on the ThreadPool thread
instead of being marshaled with the rest of Build's tail, while
`RebuildContent()` and `ScheduleSearchRebuild()` write that same field on
the main thread - the identical hazard shape one level down, on a field the
class sweep introduced rather than the original crash. That cleanup now also
runs inside `MainView.Build()`'s `MainThreadMarshal.Run` tail, ahead of the
existing liveness guard so a stale debounce is still cancelled even if the
tab was switched away before the queued callback runs.

**Class sweep** (Blish `Build()`/`DoLoad` continuation mutating UI state that
a main-thread path also touches):

| View | Result |
| --- | --- |
| `LogTabContent` (Log tab) | **HAZARD FIXED** - see above; all six `RebuildRows()` entry points are now either main-thread-and-gated on `_buildComplete` or are the marshaled tail itself. |
| `MainView` (Snapshot tab) | **HAZARD FIXED** - same PATTERN as `LogTabContent` (an un-marshaled `Build()` tail mutating UI state a main-thread path also touches), but NOT the same underlying mechanism: unlike `LogTabContent`'s plain unsynchronized `Queue<T>`, `Container.Children` is itself `ReaderWriterLockSlim`-guarded and cannot be corrupted by concurrent access (`docs/ARCHITECTURE.md` Section 1's `ControlCollection<T>` finding, fourth round below) - the real hazards here are the non-atomic dispose-then-add compound sequence (duplicated content, same shape as `LogTabContent`'s doubled-placeholder incident) and a `_searchDebounceCts` `ObjectDisposedException` race this branch also closed. Broader than `LogTabContent` in one respect: this instance is never recreated per tab visit (Module.cs builds ONE `MainView` in `Initialize()` and reuses it), and `Module.Update()` calls `SetSnapshot()`/`SetStatus()` on it every tick a background refresh completes, unconditional on which tab is selected. `Build()`'s tail (`UpdateCoinDisplay`/`ApplyStatusDisplay`/`RebuildContent`, plus the `_searchDebounceCts` cleanup - see above) is marshaled onto the main thread via `MainThreadMarshal.Run`, matching every other `RebuildContent`/`ApplyStatusDisplay`/`UpdateCoinDisplay`/`_searchDebounceCts` call site in the file (all already main-thread: user-input handlers, or already-marshaled async continuations). This does not make every field `MainView` touches main-thread-only - its panel/control fields are, like `LogTabContent`'s, still first published by the rest of `Build()`'s body on the ThreadPool thread, and this file's own Clear Cache/Refresh Now/checkbox/dropdown handlers (wired mid-body, so reachable while later controls are still under construction) rely on the same null-guard pattern, not exclusivity. |
| `SettingsTabContent` (Settings tab) | NO HAZARD - `Build()` re-runs off the main thread on every tab revisit, but nothing outside `Build()` (no `Module.Update()` polling, no `TabChanged` handling) ever touches this class's fields; every other mutation is a button-`Click`/`CheckedChanged` handler, which cannot fire before `Build()` has already finished and the control exists. |
| `AboutTabContent` (About tab) | NO HAZARD - static, render-once content; nothing outside its own `Build()` touches its fields. |
| Plan History / Crafting Ranker placeholders (`Module.BuildPlaceholder`) | NO HAZARD - creates one `Label` and returns; nothing else ever references it. |
| `CraftingPlanView` (Crafting Plan tab) | **HAZARD PRESENT, OUT OF SCOPE** - not modified (scroll/`FrameTicker` machinery is DO-NOT-TOUCH per M38, hardened M31-M36). A first sweep pass incorrectly recorded this row as no live race; a 2026-08-06 review corrected it: `Build()` calls `StopLiveTickers()` (`Views/CraftingPlanView.cs:1511`) on the ThreadPool thread; that method `Cancel()`s -> `Dispose()`s three `SpriteScreen`-parented `FrameTicker` Controls (`_scrollVerifyTicker`, `_resizeDebounceTicker`, `_wheelWrapVerifyTicker`) whose `DoUpdate` runs on the main thread and survives tab switches (they are parented to `GameService.Graphics.SpriteScreen`, not this view's own control tree - by design, per their own field comments), and zeroes `_resizeSettlePending`/`_resizeScrollRestorePending`/`_resizeScrollSavedOffset`/`_lastWheelEventUtc`, which those same main-thread ticker steps read and write. Same hazard class as the two fixed rows above; deferred to a dedicated pass that can safely touch the M31-M36 scroll machinery rather than fixed here. |

A third review round (2026-08-06) found the second round's own fix had left
behind a documentation defect of the exact class this issue exists to
prevent: the marshaled tails' and `IsLive`'s doc comments in both files
claimed the `Parent == null` liveness guard protects against a plain "tab
switched away" case. Independently re-verified via `ilspycmd` decompilation
(now recorded permanently in `docs/ARCHITECTURE.md` Section 1, "a tab
switch detaches, it does not dispose") that this is false:
`WindowBase2.ClearView` only detaches (`Container.ClearChildren`,
`Parent = null`, no `Dispose`) the outgoing view's OWN top-level
`ViewAdapter` panel from the window, and `ViewAdapter` does not override
`Unload()` - so every control below that top-level panel, including
`LogTabContent._contentPanel` and `MainView._headerPanel`/`_contentPanel`,
keeps a non-null `Parent` after a plain tab switch. Only `Control.Dispose()`
(reached via `Module.Unload()` disposing `_mainWindow`) nulls a control's
own `Parent`. Practical effect: none of the marshaled tails were ever
unsafe against a real tab switch - a tail that lands after the user
switches away simply does slightly more work than necessary (rendering
into a real, just no-longer-visible, tree instead of a genuinely
torn-down one), which the comments now say correctly instead of
certifying a guard against a case it cannot detect. Corrected four call
sites: `Views/LogTabContent.cs` (`Build()`'s marshaled tail, `IsLive`'s
doc comment) and `Views/MainView.cs` (the Refresh Now handler's marshaled
tail, `Build()`'s marshaled tail, `RunSearchDebounceAsync`'s marshaled
tail) - all now pointing at the single canonical explanation in
`docs/ARCHITECTURE.md` Section 1 instead of repeating (and having drifted
from) it independently at each site. No behavior changed; this round is
comment-only. The class-sweep table above and the `_buildComplete`
keep-vs-remove decision were both re-checked against the current code
during this round and still hold as written - no updates needed there.

A fourth review round (2026-08-06) found two more documentation defects of
the same class, both in `Views/MainView.cs`. First, the justification
recorded for marshaling `Build()`'s tail claimed "if `Update()` ever landed
while `Build()` was still executing this tail on the ThreadPool thread, two
threads would mutate the same `Children` collections concurrently, the same
shape that corrupted `LogTabContent`'s `_renderedRows` `Queue<T>`".
Independently re-verified via `ilspycmd` decompilation of the vendored
`Blish HUD.exe` (now recorded permanently in `docs/ARCHITECTURE.md` Section
1) that this is false: `Container.Children` (`ControlCollection<T>`) holds
its own `ReaderWriterLockSlim` and takes it on every operation, so unlike
`LogTabContent`'s plain `Queue<T>`, concurrent `Children` access cannot
corrupt the collection's internals - that is precisely why the field crash
landed in the module's own unsynchronized `Queue<T>` and not in Blish's
`Children`. The two hazards that DO justify marshaling this tail, neither of
which the prior comment named, are (a) the compound dispose-then-add
sequence being non-atomic, so two interleaved rebuilds can each dispose
before either adds and both survive (the same doubled-placeholder shape
`LogTabContent` hit live on 2026-07-23), and (b) the top-of-Build
`_searchDebounceCts?.Cancel();?.Dispose();` this branch moved into the
marshaled tail used to run directly on the ThreadPool thread, racing
`ScheduleSearchRebuild()`/`RebuildContent()`'s main-thread writes to the
same field - `CancellationTokenSource.Cancel()` calls `ThrowIfDisposed()`,
so whichever call landed second on the shared reference could throw
`ObjectDisposedException`, a genuine crash path this branch closed without
previously claiming credit for it. Second, `RunSearchDebounceAsync`'s
marshaled tail claimed the token "is usually already cancelled" because
"`CancelSearchDebounce()` also runs at the top of every fresh `Build()`" -
this branch had itself moved that cancel out of the top of `Build()` and
into `Build()`'s own marshaled tail (see the third-round fix above), so the
cancel is no longer synchronous with a same-tab revisit; the comment
contradicted a change made in the same diff. A debounce armed on a previous
visit can therefore stay live for the window between `Build`'s ThreadPool
body finishing and its own tail draining, and could render into whichever
`_contentPanel` the field currently holds (potentially the outgoing visit's
panel, since `Build` assigns `_contentPanel` last among the fields
`RebuildContent` reads) before being superseded moments later by `Build`'s
own tail - wasted work, not a wrong final state, which the comment now says
plainly. Both call sites corrected in `Views/MainView.cs`; the
`ControlCollection<T>` finding added permanently to `docs/ARCHITECTURE.md`
Section 1, and this table's `MainView` row above corrected to stop
repeating the disproven "same shape" claim. No behavior
changed; this round is comment-only. The class-sweep table's other rows and
the `_buildComplete` keep-vs-remove decision were re-checked against the
current code during this round and still hold as written.

**Validation:** `dotnet build -p:Platform=x64` - 0 errors. Module test suite
(`tests/GW2CraftingHelper.Tests`) - 1101/1101 passing. VendorOfferUpdater
suite (`tests/VendorOfferUpdater.Tests`) - 135/135 passing (re-measured a
fourth time after the 2026-08-06 comment-correction round above; counts
unchanged, since that round touches only comments and documentation, none
of which the repo invariants permit or require test coverage for).
`LogTabContent`/`MainView` are Blish HUD UI code with no test net (repo
invariant: tests must stay Blish-free) - the fix is proven by construction
(every racing path is now provably main-thread-only, and gated against
acting before Build's own tail has landed where relevant) plus the live
orchestrator gate below.

**Live gate:** PASS (orchestrator, 2026-08-06, live branch-build sandbox
session under the hardened desktop protocol, captures logfix_01_empty.png /
logfix_02_search.png):
- Empty-state placeholder renders exactly ONCE on first Log-tab open
  (the doubled-placeholder interleave shape is gone).
- Crash-condition recreation: four rounds of Snapshot Refresh Now (each
  spawning failing-fetch WARN writes from ThreadPool threads - the exact
  field trigger, InvalidAccessTokenException per source) immediately
  followed by Log-tab opens plus Snapshot/Log tab churn; Blish alive and
  Responding=True throughout, zero FATAL lines (the 2026-08-06 field
  crash reproduced fatally on the FIRST such open on the pre-fix build).
- Log entries render with level colors; the search box filters live
  ("material" -> exactly the four matching WARN rows) - also the first
  live verification of Log search, previously blocked by synthetic-
  keystroke death in long sessions.
- MainView search-debounce hazard (the ObjectDisposedException path this
  branch also closed): typed into the Snapshot search box then immediately
  churned Snapshot->Log->Snapshot->Log to force Build-tail vs debounce
  interleaves; no exception, no error lines, process responsive.
- Blish log sweep: zero FATAL, zero ERROR, zero Unhandled/ObjectDisposed
  across the session.

---

## DEFERRED (recorded, not implemented)

Carried over verbatim from the original backlog (full context:
`dev-notes/HISTORY.md`), plus two additional still-open items folded in
from items 31 and 32 below (marked as such) so this list covers every
genuinely open item, not just the ones originally filed under a
"DEFERRED" heading.

- Localization (en/de/fr/es via API lang param): user-deferred backlog,
  "not core functionality". Full-milestone scale when picked up.
- Upstream Blish HUD issue/PR for the wheel-delta wrap: REMOVED from the
  backlog entirely by user decision (2026-07-22) - no upstream posts are
  planned. The module-side sanitizer stays until a fixed Blish release
  ships, then can be retired at leisure.
- Ignore-pill cascade semantics + own-materials gating divergences
  (#20.4): revisit only on user feedback.
- Multi-item row reordering (gw2e moveRecipe): out of scope per M35.
- Skirmish Merchant-family wiki page split (#28, 18 offers): Skirmish
  Supervisor / Lionguard (Skirmish Merchant) / Mercenary (Skirmish
  Merchant) wiki pages were restructured into /Armor, /Weapons, /Others
  subpages; the items are still sold in-game under the split pages, but
  the seed's merchant-page linkage is now stale-shaped. Missing-offer/
  rename gap for a future re-scrape to follow up; not removed.
- "Merchant (Untamed Crags)" vendor-page-name mismatch (#28, 1 offer):
  the Hydrocatalytic Reagent / 50 Research Note offer's exact vendor
  page no longer resolves on the wiki (no page, no redirect), while the
  item and cost remain valid via other crafting-material vendors.
  Deferred pending research into whether the page was renamed or the
  original scrape mislabeled the vendor.
- Wiki-drift missing-offers superset (#28, ~5,400 offers): M37's full
  from-scratch re-scrape (for cap seeding) incidentally picked up new
  Homestead recipes and unrelated vendor page changes beyond the
  stale-offer-sweep scope. Discarded uncommitted; recorded here as a
  candidate for a future dedicated "missing offers" pass.
- Character/total purchase caps (#28): the wiki's "Has character
  purchase cap" and "Has total purchase cap" SMW properties are real and
  populated (confirmed in M37) but remain deliberately unseeded - the
  module has no account/character concept at all. Left for a future
  milestone's own design pass. ("Has seasonal purchase cap" is no longer
  in this bucket - seeded for Wizard's Vault by item 33, and item 33's
  own later entry records that its runtime consumption was also wired up
  via `TimegatedCapType.Seasonal`, so nothing about seasonal caps remains
  deferred; this parenthetical is corrected from the original record,
  which predated that wiring.)
- Homestead `HomesteadUnlocked` master gate (#24): gw2e has no "do you
  even own Homestead" gate at all; this module echoes that (no gate),
  matching v1's fixed design decision. A prior research draft flagged a
  divergence option (default-off master gate, since this module runs
  in-client for players who may never have touched Janthir Wilds) -
  recorded as a future confirm-with-maintainer option, not implemented.
- Homestead Black Market path (#24): 300 purchases of 25/week per
  station, coin-only, tier-independent - confirmed still entirely
  unseeded (the live re-scrape used for tier tagging failed wiki
  game-id resolution for all 30 Black Market rows across the three
  stations). A future milestone could seed these as plain vendor
  offers once that resolution gap is separately investigated.
- **(folded in from item 31c-2, NiceToHave)** Price-cache eviction
  policy: `TradingPostService`'s price cache has no eviction - every item
  id ever priced during the module's process lifetime stays resident
  (refreshed in place, never removed). Bounded and unlikely to matter at
  current GW2 item-id cardinality; a periodic sweep or LRU/size cap
  remains a future candidate.
- **(folded in from item 32)** StyleCop SA1413/SA1516/SA1503: left at
  default severity with a non-trivial pre-existing footprint (253/174/123
  warnings respectively); a future cleanup wave candidate.

---

## Field-test UX wave (six S-sized display fixes, 2026-08-06)

Six pre-investigated display-layer fixes from a live field-test pass over
the recipe tree; every finding was root-caused against master (e2e0936)
before implementation. All six commits: display-layer only, no
InventoryReducer/PlanSolver/VendorBatchSolver changes.

**A. Owned/needed pill clarity.** The tree row's "Nx" prefix shows the
post-reduction REMAINING need, so the "USING N OWNED" pill alongside it
read as a paradox in the field (a large owned count next to a smaller
remaining-need number looked contradictory). `DecisionPillPlanner.
AppendOwnershipPills` now shows "USING {used} OF {total} OWNED"
(total = OwnedQuantityUsed + Quantity, per CraftingTreeNode's own
documented contract) plus an explanatory hover tooltip.

**B. Non-coin vendor unit price + currency name tooltips.** A pure-
currency vendor offer (spirit shards, karma, ...) has UnitCost == 0 (not
null), so the tree row's "Unit price:" tooltip rendered a misleading
"0g 0s 0c" and silently dropped the real currency cost. Added
`CurrencyDisplayResolver.ResolveTreeNodeUnitAmounts` (a total/quantity
approximation with the same "N for M" bundle fallback as
ResolveUnitAmounts - CraftingTreeNode carries no per-offer batch data, so
this is not the shopping list's true per-batch rate). Separately, swept
the currency-icon-only cost cells (tree cost column + shopping list
Each/Total, both routed through `CoinCurrencyRenderer.
LayoutCurrencySegments`) to add a hover tooltip naming the currency,
per the field-test principle "anywhere a currency icon shows, the name
must be available."

**C. Pill border contrast.** Three pill border colors measured below the
3:1 WCAG non-text contrast minimum against white (Selected 2.31:1,
OwnedInfo 2.42:1, active-Ignore 2.10:1). Darkened to 4.21:1/4.90:1/4.29:1
respectively, same hue, same fill/text treatment otherwise.

**D. Pill tooltip coverage.** Interactive pills already set
`outer.BasicTooltipText`, but the inner fill panel and its label cover
almost the entire pill (outer is only a 1px border ring), so the tooltip
was swallowed whenever the cursor was over inner/label instead of the
thin outer ring - the user never saw it live (labels capture mouse, the
same M32 lesson). Tooltip text is now stamped onto outer/inner/label
together. Also extended tooltip coverage to four previously tooltip-less
pill kinds: Selected, Have, OwnedInfo (finding A's text), and
AchievementBitDeduped (explains the COUNTED ELSEWHERE semantics per
#26).

**E. Mystic Forge is a facility, not a discipline (user-approved).** The
forge has no rating requirement and nothing to unlock, so it no longer
appears in the Required Disciplines section (joins the existing
Achievement/Merchant filter in `PlanResultBuilder.
NonCraftingDisciplines`) - supersedes the M37-era comment/tests that
deliberately kept it in that list. Its step/recipe sublabel used to
render "MysticForge 0" (the internal id string, plus a meaningless
rating); `FormatDisciplineSublabel` now shows "Mystic Forge" with no
level number for the sole-facility case.

Validation: `dotnet build -p:Platform=x64` clean (0 errors); both test
suites green - module suite 1114 passed (was 1101; +13 new tests across
DecisionPillPlannerTests/CurrencyDisplayResolverTests/
PlanViewModelBuilderSublabelTests), VendorOfferUpdater.Tests 135 passed
(untouched, unaffected). No new Blish HUD references in tests; all new
tests exercise real production code (DecisionPillPlanner,
CurrencyDisplayResolver, PlanResultBuilder, PlanViewModelBuilder) with no
contract-mirror/fake-logic tests. Pure-Blish view code (PillColors,
TreeSectionController's pill rendering, IconControls, CoinCurrencyRenderer)
has no new tests per the Blish-free-tests invariant - covered by the live
desktop gate instead.

Live desktop gate: PASS (orchestrator, 2026-08-06, live branch-build
sandbox session under the hardened desktop protocol, captures uxg_01-11
in preflight/captures; Exordium plan with the synthetic snapshot, Use Own
Materials on, tree fully expanded):
- Finding A + final wording: the partial-coverage pill renders
  "HAVE 30/75 NEEDED" live (45x Stabilizing Matrix row: needs 75, owns 30)
  directly above a fully-covered row's plain blue "HAVE" (Mystic
  Runestone) - the vocabulary alignment reads exactly as the maintainer
  intended; hover shows the approved tooltip verbatim: "Needs 75 total -
  30 covered by your materials, 45 left to acquire".
- Finding B1: hovering a non-coin vendor row's price area shows
  "Unit price: 92 for 912 Spirit Shard" (Philosopher's Stone; the
  N-for-M bundle fallback for a non-divisible rate, currency NAME
  included) - the misleading "0g 0s 0c" is gone. (B2 icon-name tooltips
  verified by review; the same name-threading resolver is proven live
  by B1.)
- Finding C: pill border colors pixel-sampled from captures - selected
  green renders (30,143,11) vs target #1F8F0C, owned-info gold matches
  the darkened #8A6D1F range (269 px in-range), ignore-active amber
  renders (156,115,38) vs target #9C7327. All three darkened borders
  live; fill technique and white text untouched.
- Finding D: hovering an UNSELECTED VENDOR pill shows "Switch to VENDOR"
  with a hover highlight - the previously-swallowed label hover now
  fires; the OwnedInfo tooltip above proves the previously tooltip-less
  kinds are covered.
- Finding E: "Required Disciplines (3)" lists Armorsmith 450 / Jeweler
  225 / Weaponsmith 500 - MysticForge row GONE, count consistent; recipe
  sublabels render "Mystic Forge" (space, no level) while real
  disciplines keep levels ("Gift of Condensed Magic / Mystic Forge" seen
  alongside "Weaponsmith 400" rows).
- Ignore round-trip on the new pill layout: IGNORE on the partial-HAVE
  row -> HAVE + IGNORED (amber), ancestor costs updated; un-ignore
  restores "TP | HAVE 30/75 NEEDED | IGNORE" and all costs exactly.
- Session sweep: zero FATAL, zero relayout warnings.

---

## Field-test UX wave 2: MysticForge sublabel drop fix (2026-08-06)

One pre-investigated display-layer fix, following up on field-test wave
finding E above. Display-layer only, no InventoryReducer/PlanSolver/
VendorBatchSolver changes.

**MysticForge silently dropped from a mixed-discipline sublabel.**
`FormatDisciplineSublabel`'s planDiscNames intersection ran "MysticForge"
through the same filter as any other discipline, but planDiscNames can
never contain "MysticForge" in production (`PlanResultBuilder.
NonCraftingDisciplines` strips it out of every option's Disciplines list
before `disciplineMap`/`RequiredDisciplines` is built). So a recipe whose
own Disciplines combined "MysticForge" with a genuine leveled discipline
(not seen in real game data today, but not structurally impossible) had
the forge silently dropped from its sublabel - the intersection kept only
the real discipline, and the sole-MysticForge special case never matched
because "MysticForge" was no longer present in the filtered list. Fixed by
splitting the MysticForge flag out of `recipeDisciplines` before the
planDiscNames intersection runs (so it is never subject to that filter at
all) and always re-prepending "Mystic Forge" to the display text when the
flag is set, so it can no longer be silently dropped regardless of what
planDiscNames does or does not contain. The pre-existing regression test
for this combined case hand-fed a planDiscNames value the real pipeline
could never produce (`{"MysticForge", "Weaponsmith"}`), which let it pass
while validating a codepath no real caller could reach; updated to the
real production shape (`{"Weaponsmith"}` only) and added a companion
end-to-end test that goes through the real `BuildPlanDiscNames` path via
`_builder.Build(result)`, matching the pattern already used by the
adjacent sole-MysticForge end-to-end test.

Validation: `dotnet build -p:Platform=x64` clean (0 errors); both test
suites green - module suite 1115 passed (was 1114; +1 new test,
`PlanViewModelBuilderSublabelTests.
CraftingSteps_Sublabel_MysticForgeWithRealDiscipline_RelabelsButKeepsLevel`),
VendorOfferUpdater.Tests 135 passed (untouched, unaffected). No new Blish
HUD references in tests; the new test exercises real production code
(`PlanViewModelBuilder.Build`/`FormatDisciplineSublabel`) with no
contract-mirror/fake-logic tests.

Live desktop gate: PASS (orchestrator, 2026-08-06, same session as the
wave-1 gate above): the corrected sublabel path rendered live - recipes
whose only source is the forge show "Mystic Forge" with no level, and
the mixed-discipline safeguard is additionally covered by the end-to-end
test; no MysticForge row in Required Disciplines (count header (3)
consistent).

### 37. Snapshot refresh failure classification + "GW2 API access is not ready" dialog (2026-08-06)

**Field evidence:** a real user hit the Snapshot tab's Refresh Now while at
the GW2 CHARACTER SELECT screen (not yet in-world) - Blish only resolves the
account's Mumble identity, and therefore its API key, once a character is
loaded into the game world, so every account data source call failed with
an invalid/missing access token. The tab's only feedback was the bare
"Refresh Failed - {time}" label, giving no hint that logging into a
character (not restarting Blish, not re-adding the API key) was the actual
fix - a full hour lost to this before the cause was found.

**Fix:** `Services/SnapshotFailureKind`/`SnapshotFailureClassification`/
`SnapshotFailureClassifier` (Blish-free, unit-tested) classify a failed
refresh into `ApiAccessNotReady` (invalid/missing/under-scoped token -
matched by exception TYPE NAME string, not a Gw2Sharp `is` check, so the
classifier itself never needs a Gw2Sharp reference), `NetworkOrApiDown` (a
total failure with a known transport/5xx cause), `PartialFailure` (some
sources succeeded), or `Unknown`. `Gw2AccountSnapshotService` (the one file
allowed to reference Gw2Sharp) now threads each failed source's exception
type name through `SnapshotFetchFailedException`'s new
`FailedSourceExceptionTypeNames`. `Views/MainView.cs`'s Refresh Now handler
(refactored into `RefreshNowAsync()` so both the button and the new
dialog's Retry button share one code path) shows a new
`Views/ApiAccessDialog` - the three checks (in-world, API key added, module
has permission) plus Retry/Close - on `ApiAccessNotReady`, and otherwise
appends a classified cause to the status label (e.g. "Refresh failed -
could not reach the GW2 API", "Refresh partially failed - 2 of 5 sources")
instead of the bare message. `ApiAccessDialog` follows the existing
`ModalDialog` StandardWindow construction technique but is a separate class
(different title/buttons/multi-line wrapped content - via
`DrawUtil.WrapText`, the `AboutTabContent.AddInfoLine` fix pattern - rather
than a generalization of `ModalDialog`'s single-sentence Confirm/Cancel
shape) and skips its settings-backed position persistence (a rare
error-path dialog, always re-centered on `Show()`).

**Validation:** `dotnet build -p:Platform=x64` - 0 errors. Module test
suite (`tests/GW2CraftingHelper.Tests`) - 1130/1130 passing (up from the
1101 floor; +29 new tests: 22 in the new `SnapshotFailureClassifierTests`,
+3 `SnapshotFetchFailedExceptionTests`, +4 `StatusTextTests`).
VendorOfferUpdater suite (`tests/VendorOfferUpdater.Tests`) - 135/135
passing, unchanged (this branch does not touch that tool). The
classification decision logic is real-unit-tested end to end (priority
ordering, all four kinds, both the raw-type-name and `Classify(Exception)`
entry points); `ApiAccessDialog`/`MainView` are Blish HUD UI code with no
test net (repo invariant: tests must stay Blish-free) - not yet visually
verified in a live Blish session.

**Live gate:** PASS (orchestrator, 2026-08-06, live branch-build sandbox
session, captures pop_01-03 in preflight/captures): Refresh Now with no
API key raised the classified ApiAccessNotReady dialog - title "GW2 API
access is not ready", the three maintainer-specified checks rendered
verbatim (in-world character with the Mumble explanation, key registered
in Blish, module permission path), Retry re-fired the refresh (status
timestamp advanced behind the still-open dialog on repeat failure),
Close dismissed cleanly; the status line shows the classified
"Refresh failed - GW2 API access not ready - <time>" in place of the
bare "Refresh Failed". Zero FATAL lines in the session log.

---

### 38. Review-pass fixes on item 37: ApiAccessDialog self-defense + background-refresh status parity (2026-08-06)

**Context:** a review of item 37's dialog/classification landing (before
its own live gate above was exercised) found three gaps in the diff, all
fixed in this follow-up pass without touching the pure classification
logic itself.

**Fix 1 (`Views/ApiAccessDialog.cs`):** `Show()` had no defensive check of
its own for having already been `Dispose()`'d - it relied entirely on
`Views/MainView.cs`'s `_headerPanel == null || _headerPanel.Parent == null`
guard, a proxy check over an unrelated, Module-owned object's lifecycle
that only happened to also protect `ApiAccessDialog` today because
`Module.Unload()` disposes both back-to-back in one synchronous method -
coincidence of ordering, not design. `Show()` and `Hide()` now check a new
private `_disposed` flag (set at the top of `Dispose()`) and return early;
`Dispose()` itself is now idempotent (a second call is a no-op) rather
than risking a double-dispose of the underlying `StandardWindow`.

**Fix 2 (`Views/ApiAccessDialog.cs`):** the dialog never reset
`_isShowing` when dismissed via the window's own built-in title-bar X
button or Escape key (`CanClose`/`CanCloseWithEscape` both default true
and were never overridden) - both bypass the Retry/Close `StandardButton`
click handlers entirely (confirmed by decompiling the shipped Blish HUD
assembly: `WindowBase2.OnLeftMouseButtonPressed` calls `Hide()` directly
on an X-button click), so `_isShowing` stayed stuck true and every later
`Show()` call silently no-op'd for the rest of the session - reproducing
the exact "no explanation, dead end" pain item 37 exists to fix. The
constructor now subscribes `_window.Hidden += OnWindowHidden` (a named
handler, matching `ModalDialog`'s own `OnWindowMoved` idiom; unsubscribed
in `Dispose()`), which fires whenever the window's Visible=false
transition completes regardless of which path triggered it.

**Fix 3 (`Module.cs`):** `RefreshSnapshotInBackgroundAsync`'s catch block
(the auto-refresh path fired on module load, every stale-snapshot
`Update()` tick, and `OnSubtokenUpdated`) still wrote only the bare
"Refresh failed - {time}" status, even though it can hit the exact same
`InvalidAccessTokenException` root cause as the manual Refresh Now
button - a returning user who alt-tabs to character select while a
cached snapshot goes stale would silently get the old, uninformative
message through a path they never clicked anything for. It now calls the
same `SnapshotFailureClassifier.Classify(ex)` +
`StatusText.ForRefreshFailure(...)` pair `Views/MainView.cs`'s
`RefreshNowAsync` already uses, for status-text parity. Deliberately does
NOT pop `ApiAccessDialog` from this background path: showing a top-level
window unprompted while the user is off doing something else in-game
(rather than having just clicked Refresh Now themselves) is a separate,
more debatable UX call, left open rather than folded into this fix pass.

**Validation:** `dotnet build -p:Platform=x64` - 0 errors. No new
StyleCop warning categories introduced; the two new early-return guards
(`if (_disposed) return;`) add two more instances of the file's own
pre-existing braceless-if style (matching the sibling `if (_isShowing)
return;` line right next to each), already covered by the SA1503 debt
item 32 tracks - not a new class of finding. Module test suite
(`tests/GW2CraftingHelper.Tests`) - 1130/1130 passing, unchanged from
item 37's count: no pure/Blish-free logic changed in this pass, only its
call sites (`Module.cs`) and view-layer plumbing (`ApiAccessDialog.cs`),
both Blish HUD code with no test net under the repo's own Blish-free
testing invariant - `SnapshotFailureClassifier`/`StatusText` themselves
remain fully covered by their existing real unit tests, unmodified.
VendorOfferUpdater suite (`tests/VendorOfferUpdater.Tests`) - 135/135
passing, unchanged (this pass does not touch that tool). Both floors
(1101+/135) cleared. `ApiAccessDialog`/`Module.cs` remain untested-by-
design UI/host code - not yet visually verified in a live Blish session.

**Live gate:** PASS (orchestrator, 2026-08-06, live branch-build sandbox
session, captures pop_01-03 in preflight/captures): Refresh Now with no
API key raised the classified ApiAccessNotReady dialog - title "GW2 API
access is not ready", the three maintainer-specified checks rendered
verbatim (in-world character with the Mumble explanation, key registered
in Blish, module permission path), Retry re-fired the refresh (status
timestamp advanced behind the still-open dialog on repeat failure),
Close dismissed cleanly; the status line shows the classified
"Refresh failed - GW2 API access not ready - <time>" in place of the
bare "Refresh Failed". Zero FATAL lines in the session log.

---

## Wave-3 quick wins (2026-08-06)

Four user-directed changes from the same 2026-08-06 field-testing session,
implemented in the isolated `wt-wave3a` worktree off master (4ac5461,
includes PR #102) on branch `wave3a-quick-wins`. One commit per item.

**1. Use Own Materials defaults to checked.** The Crafting Plan strip's
"Use Own Materials" checkbox (`CraftingPlanView._useOwnMaterials`) now
starts `true` for a fresh session, per explicit maintainer direction - a
deliberate divergence from gw2efficiency's own unchecked default. The
field is purely in-memory session state, never read from or written to
`ModuleSettings`, so only the fresh-session starting point changes; there
is no persisted user choice to override.

**2. Mystic Forge recipes excluded from Required Recipes.** A Mystic
Forge combination has nothing to "learn" - it just exists - so listing it
in the Required Recipes section read as an unlock task that does not
exist. `PlanViewModelBuilder.BuildRecipesSection` now skips any recipe
whose ENTIRE `Disciplines` list is `MysticForge`; a recipe combining the
forge with a real leveled discipline still has something to learn and
stays. Builds on PR #102's `NonCraftingDisciplines`/sublabel work - only
this section's own row list is filtered, not the raw `RequiredRecipes`
list or the Crafting Steps section's per-step "Mystic Forge" sublabel
lookup (both read from the same unfiltered list and are unaffected). The
header count reflects the post-filter total, and the whole section is
omitted (not left present with a "(0)" header) when nothing survives the
filter.

**3. "Hide Unlocked Recipes" checkbox, default checked.** Added to the
Required Recipes section header: hides Learned/Auto-learned rows so only
Missing! recipes show by default; rows with unknown status (recipe
permission unavailable) stay visible rather than being silently treated
as "nothing to do here." The header always states the real total,
switching to "(showing K missing of N)" while the filter is active; when
every recipe is unlocked and the filter hides them all, a single friendly
line ("All N recipes already unlocked.") replaces the empty section body.
The filter predicate and header-text formatting live in a new Blish-free
`Services/RequiredRecipesVisibility` class; toggling the checkbox
re-renders through the existing `RenderPlan(_currentPlan)` rebuild path
(mirroring `TreeSectionController`'s own pill-click/preset re-render), not
a new parallel relayout mechanism. State is session-only (not persisted in
`ModuleSettings` - no per-plan-view boolean setting precedent exists there
today), matching every other sticky toggle already on `CraftingPlanView`.

**4. Log tab "Clear view" now survives tab rebuilds.** The "Clear view"
floor (a ring version watermark) used to live on `LogTabContent` itself,
which Blish reconstructs fresh every time the Log tab is selected (the
tab's own view-factory in `Module.cs` calls `new LogTabContent(...)` on
every build) - so a cleared view resurrected the moment a user switched
tabs and back. The watermark now lives on `Module` itself
(`_logViewClearedBeforeVersion`), injected into `LogTabContent` via a
constructor getter/setter delegate pair (mirroring
`TreeSectionController`'s own pattern for view state that outlives a
single render), so it persists for the whole module session instead.
`ModuleLog`'s own locking design is untouched; the watermark stays a
plain, main-thread-only `long` per the PR #101 threading rules - written
only from the Clear-view button's `Click` handler, read only from
`LogTabContent`'s already main-thread-only rebuild paths
(`GetFilteredEntries`/`AppendNewRows`, both gated by the existing
`_buildComplete` discipline). The floor comparison itself moved into a new
Blish-free `Services/LogViewFloor.IsVisible`. Also added
`BasicTooltipText` to the Clear view button: "Hide current entries from
this view. New entries still appear; the log file keeps everything."

Validation: `dotnet build -p:Platform=x64` clean (0 errors); both test
suites green - module suite 1140 passed (was 1115; +25 new tests: 17 in
`RequiredRecipesVisibilityTests`, 5 in `LogViewFloorTests`, 3 added to
`PlanViewModelBuilderStepSectionsTests` for the Mystic-Forge-exclusion
behavior), `VendorOfferUpdater.Tests` 135 passed (untouched, unaffected).
No new Blish HUD references in tests; every new test exercises real
production code (`PlanViewModelBuilder.Build`, `RequiredRecipesVisibility`,
`LogViewFloor`) with no contract-mirror/fake-logic tests. Pure-Blish view
code (`CraftingPlanView`'s checkbox wiring, `LogTabContent`'s
constructor-injected delegates, `Module`'s tab-factory wiring) has no new
tests per the Blish-free-tests invariant.

Live desktop gate: PASS (orchestrator, 2026-08-06, live branch-build
sandbox session, captures w3a_01-06 in preflight/captures):
- Use Own Materials starts CHECKED with zero interaction (fresh module
  session, capture w3a_01).
- Required Recipes: header reads "(showing 14 missing of 34)" with Hide
  Unlocked Recipes CHECKED by default; unfiltering restores all 34 with
  Auto-learned tags; the pre-wave total for the same plan was 47 - the
  13 Mystic Forge combos are gone from the section entirely in both
  filter states; unverifiable-status rows correctly stay visible under
  the filter (sandbox has no API key, so unlock status is unknowable).
- Clear view: after clearing, tab-away to Snapshot and back shows the
  view still cleared ("No entries match the current filter." empty
  state); a subsequent refresh-failure burst appears ABOVE the floor
  while cleared entries stay hidden - the floor survives tab rebuilds
  and does not block new entries.
- Zero FATAL lines in the session log.

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

## W3C: Per-character discipline display (2026-08-08)

User-directed, field-test feedback (gw2efficiency parity): the Required
Disciplines section of a generated plan listed each required discipline
and its minimum rating, but never said WHO on the account could actually
craft it. Implemented in the isolated `wt-w3c` worktree, STACKED on the
unmerged `w3b-generation-progress` branch (base commit `1ffaa65`) on
branch `w3c-character-disciplines`.

**1. Snapshot capture (`Models/SnapshotCharacterDiscipline.cs`,
`Services/Gw2AccountSnapshotService.cs`).** A new `SnapshotCharacterDiscipline`
model (`CharacterName`, `Discipline`, `Rating`, `Active`) and a new
`AccountSnapshot.CharacterDisciplines` list, captured inside the same
per-character loop that already fetched each character's inventory.
Per-character fetch: the existing narrow `V2.Characters[name].Inventory.
GetAsync` call for items, plus a second, separate `V2.Characters[name].
Crafting.GetAsync` call for the discipline signal - both need only the
already-required `account`/`characters`(/`inventories`) scopes, no new
permission requirement (**review-fix, see item 4**: an initial version
combined both signals into one round trip via the fuller `V2.Characters
[name].GetAsync` record; reverted back to two lean endpoints, since the
full record's extra recipe/equipment/build-tab payload widened this
cosmetic feature's failure surface onto plan-affecting inventory data).
Inventory failures are tolerated per-character exactly like the pre-W3C
code always has (a conservative item under-count, never a false claim);
a crafting-fetch failure for ANY character instead nulls
`CharacterDisciplines` for the WHOLE snapshot, discarding entries already
gathered from other, successfully-fetched characters too - a partial list
would read as an affirmative "not trained on any character" claim for a
discipline the fetch simply never reached, exactly the case this
null-vs-empty distinction exists to prevent (see item 4). The outer
character-LIST fetch failing is the only thing that fails the whole
snapshot, unchanged from pre-W3C. Every learned discipline is captured
regardless of `Active` (GW2 only
allows 2 concurrently active disciplines per character, but a levelled
rating persists on an inactive one), using `CharacterCraftingDiscipline.
Discipline.RawValue` (not the Gw2Sharp enum's `.Value`/`.ToEnumString()`)
so the captured string matches `RequiredDiscipline.Discipline`'s own
plain-string shape (from `Recipe.Disciplines`) byte-for-byte, including
for a discipline value the enum does not recognize.
`AccountSnapshot.CharacterDisciplines` is deliberately NOT defaulted to
an empty list like `Items`/`Wallet` - null ("never captured": a pre-W3C
snapshot.json, or a snapshot from before the character-list fetch even
started) is a distinct, meaningful state from a non-null empty list
("captured, and it came back empty"), preserved end-to-end through
`SnapshotStore`'s existing Newtonsoft (de)serialization with zero store
changes needed.

**2. Display (`Models/CraftingPlanResult.cs`, `Models/PlanSolveContext.cs`,
`Services/CraftingPlanPipeline.cs`, `Services/PlanViewModelBuilder.cs`,
`Views/Rendering/DisciplinesSectionRenderer.cs`).** `CraftingPlanResult.
CharacterDisciplines` is a straight passthrough of the snapshot (same
"cosmetic, never fed into solving" shape as the existing M34-B2a
`OwnedCurrencyAmounts` pattern), threaded through both `GenerateStructuredAsync`
overloads (single- and multi-item) and snapshotted onto `PlanSolveContext`
so a local `ResolveWithOverrides` re-solve keeps showing it without a
network round trip. `PlanViewModelBuilder.BuildDisciplinesSection` adds a
new `PlanRowViewModel.CharacterAvailabilityText` per discipline row via
`BuildCharacterAvailabilityText`: characters that have the discipline are
listed highest-rating-first as `"Anna (500), Bob (400)"`; a character
below the row's required `MinRating` gets the `"Bob (400/450)"` slash-min
suffix instead of being hidden or miscounted as sufficient; a discipline
nobody has yields the plain `"Not trained on any character"` string
(never silently blank); a snapshot with no character-crafting data at all
(old snapshot / degraded fetch - `CharacterDisciplines` null) yields a
null `CharacterAvailabilityText`, and the renderer shows nothing extra
for that row rather than fabricate a claim either way. `DisciplinesSectionRenderer.
CreateDisciplineRow` renders the non-null text as a secondary
(`DefaultFont12`, grey) label between the discipline name and the
right-aligned Level column, ellipsized to whatever room is left via the
same `LabelHelpers.EllipsizeToWidth` + tooltip-on-truncate convention
`UsedMaterialsSectionRenderer` already uses, with the full text on hover
via `BasicTooltipText` when truncated. No new layout machinery: the row
stays the existing fixed `PlanContentHeightMath.DisciplineRowHeight`
(32px, untouched), and the new label's X position is fixed at build time
(it sits after the discipline name, whose text never changes on resize)
so only a settle-time re-ellipsis (`ISectionRelayoutSink.AddReellipsis`),
never a reposition, is needed when the panel is resized. **Review-fix,
see item 4:** `Module.cs`'s wiring used to pass a fully-null
`AccountSnapshot` (not just null owned-materials data) whenever "Use Own
Materials" was unchecked, silently dropping this whole cosmetic feature
along with it; and `PlanResultBuilder`'s pre-existing multi-discipline
greedy-cover tiebreak (unrelated to the passthrough above, but directly
feeding `BuildCharacterAvailabilityText`'s "not trained" claim) picked
alphabetically among equally-covering disciplines with no account
preference, so it could name a discipline the account doesn't have over
one it does. Both fixed - see item 4 for the full findings.

**3. Tests.** `SnapshotStoreTests` gained 2 (a real store, temp-directory
round trip of populated `CharacterDisciplines`; null `CharacterDisciplines`
round-trips as null, not an empty list). `SnapshotSerializationTests`
gained 2 (a legacy snapshot.json missing the field entirely deserializes
cleanly to null - the "no data captured yet" backward-compat case; a
populated list round-trips through `SnapshotHelpers.Serialize`/
`DeserializeSnapshot` byte-for-byte). `PlanViewModelBuilderStepSectionsTests`
gained 4 against the real `PlanViewModelBuilder`/`CraftingPlanResultBuilders.
MakeResult` production path: all matching characters meet the required
rating (ordered highest-first, no slash suffix); one character below the
required rating (slash-min convention); no character has the discipline
("Not trained on any character"); and no snapshot character data at all
(`CharacterAvailabilityText` null, not an empty string). No Blish HUD
references in any new test; no fake file I/O (`SnapshotStoreTests` uses a
real `SnapshotStore` against a real temp directory, matching the
project's existing storage-test convention).

**4. Review-fix pass (this round) - 2 Critical + 3 Must Fix findings from
adversarial review, all fixed.**

- *Critical: a per-character crafting-fetch failure produced a PARTIAL
  `CharacterDisciplines` list instead of the "no data" null state.*
  `Gw2AccountSnapshotService`'s per-character loop only ever counted the
  character-LIST fetch as a failure; any individual character's data
  fetch failing was silently skipped with no flag set, so a real,
  plausible failure mode (list succeeds, some or all per-character
  detail calls then fail/rate-limit) left `CharacterDisciplines` as a
  non-null list missing exactly the failed characters' entries -
  indistinguishable from "captured, and this account genuinely has
  nobody trained in it." `BuildCharacterAvailabilityText` treats any
  non-null list as authoritative, so this fabricated an affirmative "Not
  trained on any character" claim from missing data, violating both the
  repo's "never invent data" invariant and the W3C spec's own item 4
  ("degraded fetch -> show nothing"). Fixed: a new
  `characterDisciplineDataDegraded` flag is set on ANY per-character
  crafting-fetch exception (or an unexpected null response with no
  exception); if set after the loop, `snapshot.CharacterDisciplines` is
  reset to null wholesale, discarding even the entries successfully
  gathered from other characters - a coarse but honest "we don't have
  complete data, so make no claim" behavior, matching the null/empty
  distinction's own binary design.
- *Must Fix: the single-round-trip full-character-record fetch traded a
  tiny payload for one of the heaviest v2 endpoints and widened the
  cosmetic feature's failure blast radius onto plan-affecting inventory
  data.* `V2.Characters[name].GetAsync` pulls in the character's full
  learned-recipe id list plus up to 8 equipment/build tabs whenever the
  (typically granted) `builds` scope is present - none of it used here -
  adding latency (risking the whole-snapshot 60s budget on larger
  accounts) and a new deserialization failure surface that, on a hiccup,
  would drop that character's INVENTORY (which feeds owned-materials
  reduction) rather than just its cosmetic discipline data. Reverted to
  two small, independently-caught endpoints: the pre-W3C
  `V2.Characters[name].Inventory.GetAsync` (unchanged) plus a new
  `V2.Characters[name].Crafting.GetAsync` for the discipline signal -
  both need only the already-required `account`/`characters`(/
  `inventories`) scopes.
- *Must Fix: the "Use Own Materials" checkbox silently hid this whole
  cosmetic feature when unchecked.* `Module.cs`'s `generateAsync` lambda
  passed a fully-null `AccountSnapshot` on the `useOwn: false` branch, so
  `result.CharacterDisciplines` came back null and every discipline row
  quietly lost its character text even though the on-disk snapshot had
  full data - unrelated cosmetic account info should not be gated on the
  solver's owned-materials toggle. Fixed: the lambda is now `async` and,
  on that branch, overlays `_currentSnapshot?.CharacterDisciplines` (and
  the matching `PlanSolveContext.CharacterDisciplines`, which has a
  public setter) onto the already-generated result after the pipeline
  call returns - `snapshot: null` still correctly disables owned-materials
  reduction/the force-buy pre-pass/owned-currency annotation, all
  independently gated on `snapshot != null` inside the pipeline.
- *Must Fix: the multi-discipline greedy-cover tiebreak could name a
  discipline the account doesn't have over one it does.*
  `PlanResultBuilder`'s pre-existing Pass 2 set-cover loop (unrelated to
  W3C's own passthrough code, but directly feeding
  `BuildCharacterAvailabilityText`'s claim) broke coverage-count ties by
  "prefer already-selected, then alphabetical" - for a recipe craftable
  by, say, Armorsmith/Leatherworker/Tailor with no other craft step to
  seed a Pass 1 preference, it always picked "Armorsmith" (alpha-first)
  regardless of the account, so a player with only Tailor read "Armorsmith
  - Not trained on any character" and could conclude they needed a second
  500 discipline they don't. `Build` gained an optional
  `characterDisciplines` parameter (defaults to null, so every
  pre-existing test/caller is unaffected) used ONLY to add a third
  tiebreak tier - "prefer a discipline the account has ANY character
  trained in" - between "prefer already-selected" and alphabetical; this
  can only relabel which equally-good discipline is reported, never
  change which recipes need a discipline, how many are required, or any
  cost/decision.
- *Must Fix: zero test coverage on the pipeline wiring that makes the
  feature appear at all.* Only the leaf builder
  (`PlanResultBuilderTests`) and the store (`SnapshotStoreTests`/
  `SnapshotSerializationTests`) had coverage; the three
  `result.CharacterDisciplines = ...`/`context.CharacterDisciplines`
  assignments inside `CraftingPlanPipeline` (single-item generate,
  multi-item generate, `ResolveWithOverrides` carry-forward) were
  unverified - deleting any one of them still left the full suite green.
  Five new `CraftingPlanPipelineTests` now cover: single- and multi-item
  `GenerateStructuredAsync` carrying a populated `CharacterDisciplines`
  into both `result` and `result.SolveContext`; a null-snapshot
  generation keeping it null in both places; and `ResolveWithOverrides`
  carrying it forward across a local re-solve for both the
  populated and the null case.

New tests: `PlanResultBuilderTests` gained 2 (the account-preference
tiebreak itself; a companion regression guard proving the pre-W3C
alphabetical fallback is unchanged when `characterDisciplines` is null/
omitted). `CraftingPlanPipelineTests` gained 5, listed above. No test
exercises `Gw2AccountSnapshotService` directly (it references
`Blish_HUD`/`Gw2Sharp`, out of scope for the Blish-free-tests invariant,
matching the file's existing zero direct-test-coverage pattern) - the
per-character degradation fix there is covered by build + code review
only, same as every other branch in that file.

Validation: `dotnet build -p:Platform=x64` clean (0 errors); module test
suite green - 1206 passed (was 1199 before this review-fix pass; +7 new
tests, all listed in item 4 above). No new Blish HUD references in
tests; every new test exercises real production code
(`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`,
`PlanResultBuilder.Build`) with no contract-mirror/fake-logic tests.
Item/currency/vendor IDs remain internal-only - only character names and
discipline names (both already user-facing concepts) appear in the
`CharacterAvailabilityText` display strings.

**5. Review-fix pass round 2 (2026-08-08) - 2 further Must Fix findings
from a follow-up adversarial review, both fixed.** Both findings were
newly introduced BY item 4's own fixes interacting with each other -
neither existed before that round.

- *Must Fix: the item-4 "Use Own Materials" overlay fix bypassed the
  account-preference tiebreak on the very branch it was meant to fix, and
  then silently changed the reported discipline on the first local
  override re-solve.* Item 4's fix backfilled `result.CharacterDisciplines`
  onto the ALREADY-BUILT result after `Module.cs`'s useOwn:false pipeline
  call returned - but `PlanResultBuilder.Build()` had already run, inside
  that call, with `characterDisciplines` null (since `snapshot: null` was
  passed to disable reduction, and the pipeline derived the tiebreak list
  solely from `snapshot?.CharacterDisciplines` at the time). So the
  account-preference tiebreak (item 4's OWN third fix) never actually ran
  on this branch: a recipe coverable by, say, Armorsmith/Leatherworker/
  Tailor with an account that only has Tailor still reported "Armorsmith -
  Not trained on any character" with "Use Own Materials" off - the exact
  misleading claim the tiebreak fix was supposed to close. Worse,
  `PlanSolveContext` is mutable and the overlay also patched
  `result.SolveContext.CharacterDisciplines`, so the FIRST local override
  re-solve after that (`ResolveWithOverrides` -> `Build()` again, now with
  a non-null `context.CharacterDisciplines`) silently re-picked "Tailor" -
  the Required Disciplines section rewrote itself with no discipline-
  related user action. Fixed by threading the cosmetic list into the
  pipeline as its own argument instead of patching the result after the
  fact: `GenerateStructuredAsync` (both the single-item and
  `IReadOnlyList<PlanRequestItem>` overloads) and the private
  `GenerateStructuredMultiAsync` all gained an optional
  `characterDisciplines` parameter, used via
  `characterDisciplines ?? snapshot?.CharacterDisciplines` wherever the
  old `snapshot?.CharacterDisciplines`-only computation fed `Build()` and
  `result.CharacterDisciplines` - so it is available to the tiebreak on
  the VERY FIRST `Build()` call, on both branches, regardless of whether
  `snapshot` itself is null. `Module.cs`'s lambda now passes
  `characterDisciplines: _currentSnapshot?.CharacterDisciplines`
  explicitly on BOTH the useOwn:true and useOwn:false branches (alongside
  `snapshot: null` on the latter, still correctly disabling reduction/the
  force-buy pre-pass/owned-currency annotation) and the post-hoc overlay
  is gone entirely - the lambda is a plain (non-`async`) expression again,
  matching its pre-W3C shape. `PlanSolveContext.CharacterDisciplines` is
  populated from this same value at generation time, so a local
  `ResolveWithOverrides` re-solve now carries forward an already-correct
  list instead of "discovering" it partway through a session.
- *Must Fix: doubling this feature's per-character API round trips (1 ->
  2, sequential) doubled its exposure to the exact class of transient
  failure that item 4's own all-or-nothing rule turns into a silent,
  whole-account, every-refresh feature loss.* A 30-character account went
  from 30 to 60 sequential per-character round trips inside the hard 60s
  `SnapshotFetchTimeout` (`Module.cs`'s `CancelAfter`) whose expiry
  discards the whole snapshot; independently, a single transient 429/500
  on just ONE character's `/crafting` call - not implausible for GW2's
  API - permanently wiped `CharacterDisciplines` for every character, on
  every refresh, with only a `Warn` log line and no in-UI signal (item 4's
  all-or-nothing rule is otherwise correct: see item 4's own rationale for
  why a partial list is unacceptable). Fixed with two cheap mitigations
  that do not reopen the reverted full-record endpoint: (1) each
  character's inventory and crafting-discipline fetches now run
  CONCURRENTLY via `Task.WhenAll` (two new private helpers,
  `FetchCharacterInventoryItemsAsync`/`FetchCharacterCraftingAsync`, each
  catching its own failures internally so neither one's failure faults
  the other's `Task`), restoring the wall-clock cost to roughly one round
  trip per character instead of two; and (2)
  `FetchCharacterCraftingAsync` gained one bounded retry (2 attempts, no
  artificial delay - mirroring `ItemMetadataService.GetMetadataAsync`'s
  own first-wave + retry-wave pattern) before a character's crafting data
  counts as failed, so a single transient hit self-heals instead of
  wiping the whole account's discipline data. Concurrency is capped at 2
  in-flight requests at a time (one character's own pair; the `foreach`
  loop still awaits each character before moving to the next), so this
  does not turn into an unbounded request burst against the GW2 API.

New tests: `CraftingPlanPipelineTests` gained 2, proving the item-5 fix
end to end through the exact call shape `Module.cs`'s useOwn:false branch
uses (`snapshot: null`, `characterDisciplines` supplied explicitly) - one
through the list overload's single-item short-circuit, one through the
genuine multi-item path - both asserting the account-owned discipline
(not the alphabetically-first one) is reported, and that a subsequent
`ResolveWithOverrides` no-op re-solve reports the identical discipline
rather than changing it. No test exercises the
`Gw2AccountSnapshotService` concurrency/retry change directly, for the
same reason item 4's per-character degradation fix has none (the file
references `Blish_HUD`/`Gw2Sharp`, out of scope for the Blish-free-tests
invariant) - covered by build + code review only, consistent with the
file's existing zero-direct-test-coverage pattern.

Validation (round 2): `dotnet build -p:Platform=x64` clean (0 errors);
module test suite green - 1208 passed (was 1206 after round 1; +2 new
tests, listed above). No new Blish HUD references in tests; both new
tests exercise real production code
(`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`)
with no contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only.

Live desktop gate: PASS (2026-08-08, orchestrator session). Sandbox
Blish (isolated preflight settings, dummy-window mode) with the
synthetic snapshot seeded with 4 `CharacterDisciplines` entries across
2 characters. Verified live across two generated plans:

- "Zojja's Claymore" (Weaponsmith 500): the Required Disciplines row
  rendered "Weaponsmith | Maximus Test (500), Alt Number Two (400/500)
  | Level 500" - sufficient character plain, below-threshold character
  in the slash form, sorted highest rating first; the "Characters"
  column header present and aligned over the text.
- "Zojja's Breastplate" (Armorsmith 500, deliberately absent from the
  seed): the row rendered "Not trained on any character".

No exceptions in the Blish log across the session. Alongside the W3C
checks, the seeded snapshot rendered correctly on the Snapshot tab, the
Required Recipes "(showing N missing of M)" header and Hide Unlocked
default were intact, and craft steps showed "Mystic Forge" as a plain
location tag with no fake level.

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

## W4B: Vendor cost-component leaves (2026-08-15)

User-designed after live field observation: an "Amalgamated Rift Essence"
node inside an Endless Summer plan showed a vendor offer costing 3 wallet
currencies PLUS Globs of Ectoplasm (a TP-valued item). `CraftingPlanPipeline.
AugmentWithVendorCostPricesAsync` already TP-values an offer's Item cost
lines and folds them into one effective coin price - correct math, but the
tree row's right side then rendered a very long run (gold-including-
hidden-ectos + 3 currency segments) that collided with the row layout and
hid that part of the "gold" total was actually paid in items, not coins.
Implemented in the isolated `wt-w4b` worktree (stacked on the unmerged
`tree-tooltips` branch, `4095fd2`) on branch `w4b-vendor-cost-leaves`.

**Design.** For any vendor-acquired display-tree node whose winning offer's
price mixes 2+ cost KINDS (coin / non-coin currency / TP-valued item),
`CraftingTreeBuilder.BuildVendorCostComponentLeaves` now synthesizes
DISPLAY-ONLY child leaves - one per non-coin-currency cost line and one
per TP-valued item cost line - instead of the "what it would cost to craft
instead" reference branch that node would otherwise get (component leaves
take precedence; a vendor-only item with no recipe at all, the common
real-world case, was never going to get a reference branch anyway). A
single-kind offer (the overwhelming majority - plain coin, or all-currency,
or all-item-folded-to-coin) is completely unaffected: no leaves, exactly
today's rendering. A raw coin component never gets its own leaf even when
it is one of the 2+ kinds - it stays folded into the parent's compact total
exactly as before, the simplest presentation that still keeps every VISIBLE
leaf's number consistent with the parent. The collapsed parent's cost cell
now shows ONLY that compact gold total (`Views/Rendering/
TreeSectionController.cs` skips the currency-segment run whenever the row's
own children are component leaves) - the collision is gone, and the
breakdown is one click away via the existing expander machinery.

**Data plumbing (all additive, no existing math touched).** The item cost
component's TP-valued gold amount did not previously survive past
`VendorBatchSolver.EvaluateVendorOffers` (an Item cost line was folded
straight into `coinCost` and discarded as a discrete number) - the
merged-ceil batching arithmetic itself (DO-NOT-TOUCH) is unchanged; a new
`VendorItemCostLine` (`ItemId`/`Quantity`/`GoldValue`) is captured at the
EXACT SAME multiplication site that already fed `coinCost`, scaled by the
same `unitsNeeded`, and threaded through `VendorOfferEvaluation` ->
`PlanSolver.Decision.VendorItemCosts` -> `SolverDecision.VendorItemCosts`
(mirroring the existing `VendorCurrencyCosts` passthrough exactly) -> the
display leaf's own `SubtreeCost`. A leaf's gold amount is therefore
literally the same number already folded into the parent's total, never
independently recomputed. `SolverDecision.VendorHasRawCoin` (also additive)
records only whether the winning offer had a genuine raw coin line, so the
"2+ kinds" gate can tell "coin" apart from "item money that became coin"
without re-deriving it from `TotalCost`.

**Synthetic node ids.** Each leaf gets a deterministic, stable, always-
NEGATIVE `NodeId` (`-(parentNodeId * 1000 + componentIndex + 1)`) -
`RecipeNodeIds.Assign` only ever produces small non-negative ids, so
collision is structurally impossible, and the id is stable across a
`ResolveWithOverrides` re-solve of the same tree (the parent's own NodeId
is preserved verbatim, and a leaf's `componentIndex` is a fixed position in
a list built from the same winning offer's own `CostLines`), so expansion
state survives a decision-pill click elsewhere in the tree.

**Decision pills stay decision-free.** Component leaves are facts about a
price, not an acquisition choice: `DecisionPillPlanner.BuildPillSpecs`
checks `CraftingTreeNode.IsCostComponent` FIRST and returns only an
informational HAVE / "HAVE x/y NEEDED" pill (or none) - never a CRAFT/TP/
VENDOR pill, never Ignore, never `Source`-bearing (so it can never be
clicked into an override). The HAVE data itself comes from a NEW,
deliberately separate field, `ComponentOwnedQuantity` - unlike the existing
`OwnedQuantityUsed` (which means "already subtracted from Quantity"), a
component leaf's `Quantity`/cost are NEVER reduced for ownership; owning
some of a cost component is purely informational, read from the account
snapshot's wallet (`AccountCurrencyIndex`) and inventory
(`AccountItemIndex`) via two new pipeline-computed, cosmetic-only
dictionaries (`ownedCurrencyAmounts`, already existed; the new
`OwnedVendorItemAmounts`) that - like their existing `OwnedCurrencyAmounts`
sibling - are never consulted by `InventoryReducer` or `PlanSolver`, so
they cannot affect a decision or a total. `CraftingPlanPipeline` also
widens its one bulk item-metadata fetch to include every vendor Item cost
component id (`AddVendorItemComponentIds`) - those ids are never real tree
ingredients, so without this an item leaf would show "Unknown Item"
instead of "Glob of Ectoplasm".

**Verified separation from the solver.** `CraftingPlanPipeline.
CollectPresetOverrides`/`BuildPresetOverrides` and `PlanSolver.Evaluate`'s
override lookup both walk `RecipeNode`/`RecipeOption` (the SOLVER tree)
exclusively - a component leaf corresponds to no `RecipeNode` at all and is
never fed back into either, so Craft All/Buy All/Best Path and a plain
per-node override click are provably unaffected (a dedicated pipeline test
builds a preset override map and re-solves with component leaves present,
asserting the leaves survive untouched and every produced override key is
`>= 0`, i.e. a real solver id, never a synthetic negative one).

`Models/CraftingTreeNode.cs` gained `IsCostComponent`/`ComponentOwnedQuantity`
- both additive with `false`/`0` defaults, so an old `plan.json` simply
deserializes every existing node with no component leaves (renders exactly
as it did before this milestone) until the plan is regenerated;
`PlanStructuralValidator.IsValidCraftingTreeNode`'s existing recursive
`Children` walk already covers a component leaf with zero changes needed.

New tests (all Blish-free, real production paths - no Blish/BlishHUD/
Gw2Sharp references, no fake file I/O): `VendorBatchSolver`/`PlanSolver`
level tests proving `VendorItemCosts`/`VendorHasRawCoin` populate correctly
for mixed offers and stay null/false for single-kind ones
(`PlanSolverVendorOfferTests`); `CraftingTreeBuilder` tests covering leaf
labels/amounts/flags, parent-total-equals-leaf-value consistency, blank
currency-leaf cost, no leaves for every single-kind offer shape (coin-only,
currency-only, item-only, coin+currency-with-currency-leaf-only), HAVE-pill
coverage (full/partial/none) from owned-amount dictionaries, currency-leaf
name/icon from live `CurrencyMetadata` with the offline fallback, and
stable/collision-free synthetic ids across two builds of the same tree
(`CraftingTreeBuilderTests`); pill-vocabulary tests proving a component
leaf never gets a decision/Ignore pill and only ever the informational
HAVE/OwnedInfo pair (`DecisionPillPlannerTests`); end-to-end pipeline tests
through a real `VendorOfferStore` + `AccountSnapshot` proving the metadata
widening, the owned-amount wiring, a `ResolveWithOverrides` round-trip
preserving leaf NodeIds/values, and the preset-override separation
(`CraftingPlanPipelineTests`); and a real `PlanStore` save/load round trip
proving component leaves survive gzip-compressed persistence and pass
`PlanStructuralValidator` unchanged (`PlanStoreTests`). Full module suite:
1273 baseline + 30 new W4B tests, all green.

**Review-fix round (2026-08-15) - 7 findings from an adversarial review (2
Critical, 4 Must Fix, 1 Must Fix flagged for explicit justification), all
addressed.** The two Critical findings were the same defect surfacing twice:
`VendorItemCosts`/`VendorCurrencyCosts` are captured PRE-merge, per tree
occurrence, by `VendorBatchSolver.EvaluateVendorOffers` - but when the same
vendor item is needed via 2+ tree occurrences that merge into one true
batched purchase (the exact shape the merge-then-ceil machinery exists for),
`AllocateVendorNodeCosts` reallocates each occurrence's corrected
`TotalCost` share WITHOUT re-deriving those raw component numbers the same
way. A component leaf built from them could show a value that no longer
summed to (and could even exceed) its own parent's corrected total - a
reproduced, concrete regression of the exact "two sections of the same page
disagree" defect class the batching correction passes exist to prevent, one
level lower.

- *Fix (Critical x2, `Services/VendorBatchSolver.cs` lines 767/743,
  `Services/CraftingTreeBuilder.cs` line 283).* Added
  `SolverDecision.VendorComponentCostsUnreliable` (additive bool, default
  false) and a new `PlanSolver.FlagUnreliableVendorComponentCosts` pass,
  run immediately after `AllocateVendorNodeCosts` in `PlanSolver.Solve`: it
  marks every occurrence of a step that genuinely merged 2+ tree occurrences
  (`vendorOccurrences[stepKey].Count > 1` AND
  `step.VendorOfferOutputCount > 0`, the same gate `AllocateVendorNodeCosts`
  itself uses to decide whether a step was actually corrected).
  `CraftingTreeBuilder.BuildVendorCostComponentLeaves` now short-circuits to
  "no leaves" whenever this flag is set, regardless of kind count - the
  node still shows its own correctly reallocated `SubtreeCost`, just without
  an unprovable item/currency breakdown. Deliberately kept OUT of
  `VendorBatchSolver.cs` (DO-NOT-TOUCH: merged-ceil batching math) - the new
  pass lives in `PlanSolver.cs`, reads `AllocateVendorNodeCosts`'s own
  already-public inputs/outputs (`vendorOccurrences`, `stepMap`) strictly
  after it returns, and writes only the new auxiliary flag; `VendorBatchSolver.cs`'s
  `AllocateVendorNodeCosts` method body is byte-for-byte unchanged (only its
  doc comment gained a cross-reference). A single-occurrence vendor buy is
  unaffected (nothing was actually reallocated there, so the original
  numbers stay accurate) - every pre-existing W4B leaf test keeps passing
  unmodified. New test: `CraftingTreeBuilderTests.
  MultiOccurrence_MergedMixedVendorOffer_SuppressesComponentLeaves_ParentStaysConsistent`
  reproduces the exact two-occurrence bulk-offer shape from the finding
  (batch size 15, two 6-unit occurrences merging to one true batch) and
  asserts both occurrences get no leaves while their reallocated
  `SubtreeCost` values still sum exactly to the real merged `PlanStep`
  total.

- *Fix (Must Fix, `Services/CraftingTreeBuilder.cs` line 155): reference
  branch silently dropped when a vendor node also got component leaves.*
  The two no longer compete for the same `Children` slot - a vendor node
  whose offer both mixed 2+ cost kinds AND has a known recipe now STACKS
  them: component leaves first (so `TreeSectionController`'s
  `Children[0].IsCostComponent` cost-cell-suppression check keeps working
  unmodified), then the reference branch's own recipe ingredients appended
  as additional, ordinary children. Verified safe to mix: `IsReferenceBranch`
  is purely informational today (not read by any renderer - grepped), and
  per-child dimming already comes from the PARENT's `Decision != Craft`
  uniformly, not from any per-child reference-branch flag, so a mixed
  `Children` list renders exactly as consistently dimmed as either kind
  alone. `CraftingTreeNode.IsReferenceBranch`'s doc comment updated to
  record the now-possible mixed case. New test: `CraftingTreeBuilderTests.
  MixedOfferNode_AlsoHasRecipe_StacksComponentLeavesThenReferenceBranch`.

- *Fix (Must Fix, `Services/VendorBatchSolver.cs` line 300): missing
  `Count > 0` guard on the Item cost-line capture.* A zero/negative-count
  Item cost line (malformed wiki-scraped seed data) could invent a phantom
  "item" cost KIND, flipping an otherwise single-kind offer into
  leaf-synthesis mode with a 0-quantity/0-gold ghost leaf. Guarded the raw
  `itemCostRaw` capture with `cost.Count > 0`, mirroring the raw-coin
  branch's own identical guard a few lines above it; `coinCost` itself is
  untouched (a Count of 0 already contributed nothing to it). New test:
  `PlanSolverVendorOfferTests.ZeroCountItemCostLine_DoesNotPopulateVendorItemCosts`.

- *Fix (Must Fix, `Services/CraftingPlanPipeline.cs` line 859):
  `ResolveWithOverrides` metadata gap for a non-baseline-winning vendor
  offer.* `ResolveWithOverrides` never re-fetches metadata (by design - it
  is purely local, no network calls); the pre-existing
  `AddVendorItemComponentIds` only scanned the BASELINE winning decisions'
  `VendorItemCosts`, so a node whose original decision was Craft - later
  manually overridden to `BuyFromVendor`, an ordinary and commonly-used
  interaction - could surface an item component leaf whose id was never
  widened into `PlanSolveContext.Metadata`, rendering "Unknown Item" with
  no icon until the whole plan was regenerated. Added
  `AddAllVendorOfferItemComponentIds`, called at both generation entry
  points (`GenerateStructuredAsync`/`GenerateStructuredMultiAsync`) right
  alongside the existing decisions-only widening: it scans every `Item`
  cost line on EVERY vendor offer already fetched for the tree (not just
  the winning one), using data already resident in memory from the
  existing `vendorOffers` fetch - no extra network round trip, and
  `ResolveWithOverrides` itself needed no change. New test:
  `CraftingPlanPipelineTests.
  MixedVendorOffer_NotBaselineWinner_ResolveWithOverrides_StillResolvesRealItemMetadata`.

- *Fix (Must Fix, `tests/.../CraftingTreeBuilderTests.cs` line 1160):
  missing 3-distinct-currency single-kind coverage.* Added
  `SingleKindVendorOffer_ThreeDistinctCurrencies_NoItemNoCoin_CountsAsSingleKind_NoLeaves`,
  locking down that `kindCount` counts by
  `decision.VendorCurrencyCosts.Count > 0` (a boolean per KIND) rather than
  per distinct currency id, so an offer spanning 3 different non-coin
  currencies still gets no leaves - a deliberate design choice that was
  previously unverified by any test.

- *Justified, not changed (`Services/VendorBatchSolver.cs` line 333):
  the `itemsScalable`/`continue` overflow guard added inside
  `EvaluateVendorOffers`.* Flagged as new control flow inside a DO-NOT-TOUCH
  method. Kept as-is: it is structurally identical to - and only extends to
  a second cost dimension - the pre-existing `scalable`/`continue` guard a
  few lines below it for currency lines (same file, same loop, same
  overflow-safety shape, predates this feature), so it introduces no new
  KIND of control flow. It can only fire when a single occurrence's scaled
  Item-cost quantity exceeds `int.MaxValue` - unreachable with real GW2
  data. Rewriting it as a clamp instead (silently truncating the
  represented cost) would be the actual behavior change, and a strictly
  worse one - a clamped value is silently wrong, while skipping the offer
  fails safe exactly like its currency sibling already does. Documented
  inline with this reasoning for future reviewers.

Validation: `dotnet build -p:Platform=x64` clean (0 errors); full module
test suite green - 1308 passed (1273 baseline + 30 original W4B + 5 new
review-fix tests: 3 in `CraftingTreeBuilderTests`, 1 in
`CraftingPlanPipelineTests`, 1 in `PlanSolverVendorOfferTests`). No new
Blish HUD references in tests; every new test exercises real production
code (`PlanSolver.Solve`, `CraftingTreeBuilder.BuildTree`,
`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`) with
no contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only. `VendorBatchSolver.cs`'s DO-NOT-TOUCH merged-ceil methods
(`EvaluateVendorOffers`, `FinalizeVendorBatches`, `AllocateVendorNodeCosts`,
`MergeVendorCurrencyCosts`, `VendorBatchesEqual`, `ScaleCostLines`) had
their dollar-amount arithmetic (coin costs, ceil/batch selection,
allocation shares) left byte-for-byte unchanged throughout this round - the
only edits inside that file are a `Count > 0` capture guard and doc
comments, both confirmed by diff review to touch no cost computation.
Performance note: the new `AddAllVendorOfferItemComponentIds` scan runs
once per plan generation (not a render/UI hot path) over `vendorOffers`
data already resident in memory from the pre-existing fetch; its only
allocation is additional entries in the `metadataIds` HashSet that was
already being built, no new collection type.

**Review-fix round 2 (2026-08-15) - 2 Must Fix findings from a further
adversarial review, both the same shape as round 1's fixes one field/map
over, both addressed.**

- *Fix (Must Fix, `Services/VendorBatchSolver.cs` line 292): missing
  `Count > 0` guard on the non-coin Currency cost-line capture.* Round 1
  guarded the Item cost-line capture (`itemCostRaw`) against a
  zero/negative-count line inventing a phantom "item" cost KIND, but left
  the identical `currencyCosts.Add(cost)` five lines above it unguarded -
  a zero/negative-count non-coin Currency cost line could still invent a
  phantom "currency" cost KIND the same way, flipping an otherwise
  single-kind offer into leaf-synthesis mode with a 0-quantity (or, for a
  negative Count, negative-quantity) ghost leaf: blank cost, no pill. Now
  guarded with `if (cost.Count > 0)`, mirroring both the raw-coin branch's
  own guard immediately above it and the Item-line guard below it;
  `coinCost` is untouched either way. New test:
  `PlanSolverVendorOfferTests.ZeroCountCurrencyCostLine_DoesNotPopulateVendorCurrencyCosts`
  (sibling to round 1's `ZeroCountItemCostLine_DoesNotPopulateVendorItemCosts`).

- *Fix (Must Fix, `Services/CraftingPlanPipeline.cs`
  `BuildOwnedVendorItemComponentAmounts`): the ownership map was never
  widened the way metadata was.* Round 1 widened metadata fetching
  (`AddAllVendorOfferItemComponentIds`) so a vendor offer NOT chosen at
  baseline still resolves a real name/icon after a manual
  `BuyFromVendor` override via `ResolveWithOverrides`. The parallel
  ownership computation, `BuildOwnedVendorItemComponentAmounts`, was left
  scoped to only the BASELINE winning decisions'
  `VendorItemCosts` (via `AddVendorItemComponentIds` alone) - so the same
  override scenario surfaced a correctly-named item component leaf with
  permanently NO have pill, even with the item sitting in the account,
  until the whole plan was regenerated. `PlanSolveContext.
  OwnedVendorItemAmounts` is, like `Metadata`, captured once at generation
  time and passed to `ResolveWithOverrides` verbatim - never recomputed.
  Widened `BuildOwnedVendorItemComponentAmounts` to also call
  `AddAllVendorOfferItemComponentIds` over `vendorOffers` (reusing the
  round 1 method rather than duplicating its scan), at both call sites
  (single-item and multi-item generation entry points). Extended test:
  `CraftingPlanPipelineTests.
  MixedVendorOffer_NotBaselineWinner_ResolveWithOverrides_StillResolvesRealItemMetadataAndOwnership`
  (renamed from round 1's `...StillResolvesRealItemMetadata`, now also
  attaching a snapshot with partial ownership of the item component and
  asserting `ComponentOwnedQuantity` survives the override re-solve).

Validation: `dotnet build -p:Platform=x64` clean (0 errors); full module
test suite green - 1309 passed (1308 from round 1 + 1 new round-2 test;
the round-2 ownership fix extended an existing test rather than adding a
new one). No new Blish HUD references in tests; both new/extended tests
exercise real production code (`PlanSolver.Solve`,
`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`)
with no contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only. `VendorBatchSolver.cs`'s DO-NOT-TOUCH merged-ceil methods
had their dollar-amount arithmetic left byte-for-byte unchanged - the only
edit inside that file this round is the one `Count > 0` capture guard
(plus its doc comment). `ResolveWithOverrides`/`BuildPresetOverrides`
themselves needed no change in either fix - both walk the SOLVER tree via
`solveResult.Decisions`, not the display tree, and neither fix touches
decision-making, only the cosmetic metadata/ownership maps consulted
afterward when building display leaves.

**Review-fix round 3 (2026-08-15) - 1 Must Fix finding: the round-2
ownership widening covered the ITEM component map only and missed its
exact currency-side sibling.**

- *Fix (Must Fix, `Services/CraftingPlanPipeline.cs`
  `BuildOwnedCurrencyAmounts`): the currency ownership map was never
  widened the way the item one was.* Round 2 widened
  `BuildOwnedVendorItemComponentAmounts` to scan every vendor offer's Item
  cost lines (`AddAllVendorOfferItemComponentIds`), not just the baseline
  winning decisions - but `BuildOwnedCurrencyAmounts` still keyed its
  dictionary strictly off `plan.CurrencyCosts`, the baseline plan's
  aggregated currency totals. Same failure shape as round 2, currency
  side: a node whose baseline decision is Craft, manually overridden to
  `BuyFromVendor` via `ResolveWithOverrides`, surfaces a currency
  cost-component leaf with a correct name/icon/quantity but permanently NO
  have pill, even with a full wallet, because that currency id was never
  in `plan.CurrencyCosts` and `PlanSolveContext.OwnedCurrencyAmounts` is
  captured once at generation time and reused verbatim (never
  recomputed) by `ResolveWithOverrides` - exactly the reuse-verbatim
  argument that justified the round-2 item-side fix. Added
  `AddAllVendorOfferCurrencyComponentIds`, the currency-side twin of
  `AddAllVendorOfferItemComponentIds` (same non-coin-Currency /
  `Count > 0` filter `VendorBatchSolver.EvaluateVendorOffers` itself
  uses), and widened `BuildOwnedCurrencyAmounts` to scan `vendorOffers`
  through it in addition to `plan.CurrencyCosts`, at both call sites
  (single-item and multi-item generation entry points). Harmless for the
  pre-existing currency summary rows (`PlanViewModelBuilder`), which only
  ever look up the ids they themselves iterate from `plan.CurrencyCosts` -
  extra keys in the returned map are simply never read by that caller.
  Extended test: `CraftingPlanPipelineTests.
  MixedVendorOffer_NotBaselineWinner_ResolveWithOverrides_StillResolvesRealItemMetadataAndOwnership`
  now also attaches a wallet entry for the offer's non-coin currency
  component and asserts `ComponentOwnedQuantity` on the currency leaf
  survives the override re-solve, the currency-side sibling of the
  existing item-leaf assertion in the same test.

Validation: `dotnet build -p:Platform=x64` clean (0 errors); full module
test suite green - 1309 passed (unchanged count - the round-3 fix
extended the same existing test the round-2 fix had already extended,
rather than adding a new one). No new Blish HUD references in tests; the
extended test exercises real production code (`PlanSolver.Solve`,
`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`)
with no contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only. `VendorBatchSolver.cs`'s DO-NOT-TOUCH merged-ceil methods
were not touched at all this round (the fix is entirely inside
`CraftingPlanPipeline.cs`, a cosmetic ownership-annotation map consulted
strictly after solving). `ResolveWithOverrides`/`BuildPresetOverrides`
themselves needed no change: `BuildPresetOverrides` walks `context.Tree`
(the solver's `RecipeNode` tree) and never references
`OwnedCurrencyAmounts` at all - confirmed by reading it. `ResolveWithOverrides`'s
own decision-making call, `_solver.Solve(context.Tree, ...)`, is likewise
never given `OwnedCurrencyAmounts` and cannot see it; that map is only
passed, read-only, into `BuildCraftingTreeResult` afterward, which
annotates the DISPLAY tree's already-decided leaves with a HAVE pill - it
cannot feed back into `solveResult.Decisions`, so this fix cannot affect a
solver decision, only the cosmetic HAVE pill a display leaf reads
afterward.

**Pre-gate addendum (2026-08-15): "OWN n"/"CURRENCY" badge wording pass.**
Maintainer field-testing found the component leaves' informational HAVE/
"HAVE x/y NEEDED" pill (Section "Decision pills stay decision-free" above)
misleading: that vocabulary means "your stock covers this need and reduced
the plan cost" everywhere else in the tree, but a component leaf's
ownership never reduces this line's cost (see `ComponentOwnedQuantity`'s
own doc comment) - it is purely informational.
`DecisionPillPlanner.BuildPillSpecs`'s `IsCostComponent` branch now shows a
subdued "OWN n" badge instead (n = the raw `ComponentOwnedQuantity` holding, no full-vs-partial
split since coverage never changes the cost either way), rendered only
when n > 0 (no "OWN 0" clutter) - reusing `PillKind.OwnedInfo` (the same
muted-gold kind the ordinary partial-ownership annotation already uses, no
new color). Also added: a "CURRENCY" badge (`PillKind.Locked`, the SAME
kind/text the plain currency-ingredient leaf's own pill already uses a few
lines above in `BuildPillSpecs`) on the currency-type component shape
(`SubtreeCost` never set - the "deliberately blank cost cell"
`BuildVendorCostComponentLeaves`' currency-line branch produces),
explaining at a glance why no gold value is shown - gw2efficiency's own grey Currency-
badge pattern. The two badges are independent and may both appear on one
leaf; `TreeSectionController.RenderDecisionPills` needed no layout change
- both badge strings are short ("CURRENCY" plus "OWN n") and comfortably
fit the existing 240px pill-column budget together (the pill column
already fits the old, longer "HAVE x/y NEEDED" text alongside a source
pill - see the `RealSolver_*`/`PartialOwnership_*` tests above), so
`PlanRelayoutMath.ComputeVisiblePillCount` (untouched) never needs to drop
either one in practice.

Regular (non-component) currency-ingredient leaves already carried the
identical "CURRENCY" / `PillKind.Locked` badge before this pass (the
`CraftingDecision.Currency` short-circuit in `BuildPillSpecs`) - the
vocabulary-consistency extension this addendum considered for those leaves
was therefore already in place with zero additional diff.

Tooltips follow the existing pattern exactly (`BasicTooltipText` stamped on
all three of `outer`/`inner`/`label` together in
`TreeSectionController.RenderDecisionPills`, per that method's own "tooltip resolved once... then
stamped onto outer/inner/label together" comment - a tooltip on `outer`
alone is swallowed by `inner`/`label`, which cover almost the entire pill).
"OWN n"'s tooltip: "You own {n} - informational only, does not change the
plan cost". "CURRENCY"'s tooltip (component-leaf case only; the ordinary
currency-ingredient leaf's ambient "Only available source" default is
unchanged): "Paid in a non-coin currency - no gold value to show here".
The now-dead `IsCostComponent` branch of the `PillKind.Have` tooltip (a
component leaf can no longer reach that `Kind` at all) was simplified back
to the plain-node wording only.

Updated: `Models/CraftingTreeNode.cs` (`IsCostComponent`'s and
`ComponentOwnedQuantity`'s doc comments), `Services/CraftingTreeBuilder.cs`
(`ResolveOwnedQuantity`'s doc comment), `Services/DecisionPillPlanner.cs`,
`Views/Rendering/TreeSectionController.cs`. `DO-NOT-TOUCH` files (`Services/
ModuleLog.cs`, `Services/PlanContentHeightMath.cs`, `Services/
PlanRelayoutMath.cs`, scroll machinery, `VendorBatchSolver`'s merged-ceil
vendor batching math) were not touched. `DecisionPillPlannerTests` updated:
3 existing cost-component tests renamed/adjusted for the new "OWN n"
wording (no more full/partial HAVE split), plus 3 new tests covering the
CURRENCY-badge threshold (blank vs. non-null `SubtreeCost`) and the two
badges coexisting in emission order (CURRENCY first, then OWN). Full module
suite: 1312 passed (1309 baseline + 3 net new).
`dotnet build -p:Platform=x64` clean (0 errors). No new Blish HUD
references in tests; the extended/added tests exercise the real
`DecisionPillPlanner.BuildPillSpecs` production code, no contract-mirror/
fake-logic tests. Item/currency/vendor IDs remain internal-only (badge text
is `"OWN n"`/`"CURRENCY"` only, never an id).

Live desktop gate: Gate: PASS 2026-08-16 (orchestrator live sandbox session, combined wave-4 staging build). Verified on the real Amalgamated Rift Essence vendor path: component leaves render under the vendor-selected root (ecto leaf's gold share exactly equals the parent's collapsed total; three currency leaves blank-cost with CURRENCY badges), OWN badges show the RAW wallet holding (300/150/100 against 250/100/50 needs) after the gate-found clamp fix in this branch, no OWN badge at zero holding, manual override to VENDOR re-solves and the overridden state survives module restart via the persisted plan, tree-button tooltips render live (Best Path text verified verbatim). Known composition note: component leaves and the dimmed what-crafting-would-cost reference branch both render under a vendor root with a recipe - as designed; a visual separator is a queued UX question.

## Craft/vendor comparability parity fix (2026-08-15)

Root-caused via user-approved investigation: `Services/PlanSolver.cs`'s
craft-cost path (the recipe loop inside `Evaluate`) silently valued
UNVALUED currency ingredients at ZERO coin while still letting the craft
option compete fully on coin cost in `PickCheapest` - the opposite of how
`Services/VendorBatchSolver.cs`'s `EvaluateVendorOffers` already treats an
unvalued non-coin currency line on a vendor offer (demoted to a
FALLBACK-ONLY tier that never competes on coin cost - see that method's
own doc comment, M33-era). Consequence: a recipe with a heavy, unpriced
currency cost could be declared "cheapest" purely because that cost was
invisible to the comparison, while a vendor offer charging the identical
currency was correctly barred from the same trick. This asymmetry was
about to go live the moment a parallel branch's ~188 restored
currency-ingredient recipes (e.g. Amalgamated Rift Essence: 3 currencies +
50 ectos crafted vs. the same currencies + 60 ectos vendor) started
ingesting.

**The fix: give craft the SAME comparable/fallback tier semantics vendor
already has.** `PlanSolver.Evaluate`'s recipe loop now splits candidate
recipes into two tiers, exactly mirroring `EvaluateVendorOffers`:

- **Comparable** - no `Currency`-type ingredient, or every one has a
  user-provided valuation (`CurrencyValuation`/`ModuleSettings.
  CurrencyValuationsJson`, the same mechanism vendor offers already read).
  Competes on equal footing with TP buy and a comparable vendor offer in
  `PickCheapest`, unchanged from before this fix.
- **Fallback** - at least one `Currency`-type ingredient has NO valuation
  (or its valuation arithmetic overflows - mirrors `EvaluateVendorOffers`'
  identical per-line overflow handling). Still fully offered (`CanCraft`
  stays true, the CRAFT pill still shows - the M33 guarantee is
  preserved unchanged) but never wins the automatic decision against any
  comparable option; used only when NOTHING comparable exists anywhere
  for that node (no TP price, no comparable vendor offer, no comparable
  recipe).

Two tracked "best" candidates (`bestComparableCraftCost`/
`bestFallbackCraftCost`, each with its own real-cost and RecipeId
sibling) replace the old single `bestCraftCost`, with the pre-existing
lowest-RecipeId tie-break now applied per tier. Only the comparable
value is ever passed into `PickCheapest`; a manual per-node override
(`forced == AcquisitionSource.Craft`) uses comparable-first-else-fallback,
mirroring `VendorBatchSolver`'s own override precedence for
`BuyFromVendor`. The terminal fallback branch (previously vendor-only:
"nothing beat buy, but a fallback vendor offer exists") now also
considers a fallback craft, and when BOTH a fallback craft and a
fallback vendor offer exist, applies the exact same tie-break
`PickCheapest` already uses for the comparable tier: the numerically
cheaper of the two wins, an exact tie keeps vendor - "someone must still
be picked," extended from the pre-existing vendor-only fallback
precedent to cover craft's new fallback tier too. A force-buy-only node
(`OwnedMaterialsForceBuyPrePass`) is excluded from the fallback branch
the same way it was already excluded from the primary comparison -
craft stays off every automatic path for that node, not just the
primary one; `OwnedMaterialsForceBuyPrePass`'s own raw-diagnostics
`craftCost` figure now reports the comparable-tier cost when one
exists, else the fallback-tier cost, mirroring the real decision's own
tier priority (previously the single undifferentiated number).

**Item 2 (valued currencies already competed symmetrically) - verified,
no change needed.** A recipe's valued `Currency` ingredient already fed
the craft-vs-buy DECISION value exactly like a vendor offer's valued
currency line does (`PlanSolver.cs`'s pre-existing currency-ingredient
branch in the recipe loop, unchanged by this fix) - this was already
correct and is untouched.

**Item 3 (decision-only valuation - never inflates a displayed coin
total) - verified already correct, now locked by a dedicated test.**
Audited both paths: `Decision.TotalCost`/`craftRealCost` (craft) and
`VendorOfferEvaluation.BestComparableCoinCost`/`totalCoinCost` (vendor)
already excluded valuation-derived coin from every real, committed cost
- both were already documented as intentional (`Decision`'s own doc
comment, "Never includes a valued currency's coin-equivalent"). No
display-layer code (`PlanResultBuilder`, `PlanViewModelBuilder`) touches
cost totals at all - `Plan.TotalCoinCost`/`PlanStep.TotalCost`/
`CraftingTreeNode.SubtreeCost` are the only user-visible coin surfaces
and are all sourced from `Decision.TotalCost`. Pre-existing tests
(`PlanSolverCurrencyValuationTests`,
`PlanSolverCoreDecisionTests.CurrencyIngredient_ValuedButCraftStillWins_RealCostExcludesCurrencyValue`)
already locked the step-level case; a new test
(`PlanSolverCraftVendorComparabilityTests.
ValuedCurrencyIngredient_ComparableCraftWins_PlanTotalCoinCostExcludesValuation`)
locks it explicitly at the `plan.TotalCoinCost` level too. No fix was
needed for this item.

**Item 4 (fallback picks must not display a false coin cheapness) -
verified, no UI change needed.** `DecisionPillPlanner.BuildPillSpecs` and
`CraftingTreeBuilder.BuildNode` are driven purely by
`CanCraft`/`CanBuyTp`/`CanBuyVendor`/`Source`/`TotalCost` - they never
distinguish comparable from fallback tier, and no UI surface anywhere in
the module claims a decision is "cheapest" (grepped `Views/`/`Services/`/
`Models/` for the word - no hits). A fallback craft decision now flows
through the exact same `Commit`/`Decision`/`PlanStep` shape a fallback
vendor decision already used (real coin `TotalCost`, no invented
number), so it already presents identically - reuse, not a new UI path,
per the repo's "reuse existing UI, do not invent new UI" rule.

**Flagged limitation (spec item 5, documented rather than expanded in
scope): a true tie inside the ARE-shaped fallback case.** When a craft
recipe and a vendor offer are both fallback-tier (identical unvalued
currency ingredients on each side) AND their priced/real portions are
ALSO numerically equal, the terminal fallback tie-break has no finer
signal than its existing "exact tie keeps vendor" rule - it cannot
express "these two options are ACTUALLY identical in total real cost
because the currency lines net out," because currencies are ignored
entirely on both sides (decision-only valuation), never compared or
"cancelled" against each other. This is not a regression: the pre-fix
code could not express this either, and a coin/currency exchange rate
would have to be invented to do better, which the repo invariant (avoid
inventing currency exchange rates) rules out. The common case - priced
portions genuinely differ, as in the real Amalgamated Rift Essence
example (50 ectos crafted vs. 60 vendor) - is handled correctly: see
`PlanSolverCraftVendorComparabilityTests.
AmalgamatedRiftEssenceShaped_IdenticalUnvaluedCurrencies_CraftWinsOnRealItemCostDifference`.

**Tests
(`tests/GW2CraftingHelper.Tests/Services/PlanSolverCraftVendorComparabilityTests.cs`,
11 new; plus 3 pre-existing tests updated because they encoded the old
buggy behavior).** New file covers: a fallback craft stays offered
(`CanCraft` true) even when the automatic decision picks buy; a
comparable recipe is chosen over a numerically cheaper fallback recipe
on the same node; multiple-fallback-recipe tie-break by lowest RecipeId;
a valued-currency recipe still competes as comparable and can beat both
a comparable vendor offer and TP buy; `plan.TotalCoinCost` excludes a
winning comparable recipe's currency valuation; both directions of the
all-fallback craft-vs-vendor tie-break (cheaper wins) plus the exact-tie
case (vendor wins); `OwnedMaterialsForceBuyPrePass` exclusion also
blocks the fallback-craft last resort; a manual per-node override still
forces a fallback-only recipe; and the Amalgamated-Rift-Essence-shaped
case itself. The 3 pre-existing tests that encoded the bug
(`PlanSolverCoreDecisionTests.CurrencyIngredient_AppearsInCurrencyCostsNotSteps`,
`PlanSolverCoreDecisionTests.CurrencyIngredient_Unvalued_ContributesZeroToDecisionAndCost`
- renamed to `..._ComparableBuyWins_RegardlessOfFakeZeroCost` and its
assertion flipped to the corrected outcome,
`CraftingTreeBuilderTests.CurrencyNode_ResolvesKnownNames`) were each
updated to remove the item's TP buy price, so the fallback craft is
still exercised as the last resort and each test's real subject
(currency display, not the buy-vs-craft decision itself) still applies -
found via a full-suite run surfacing the regression, then a repo-wide
grep for every `Leaf(..., "Currency")`/`IngredientType = "Currency"`
construction site (fix the class, not the instance) to confirm no
further sibling was missed.

Validation: `dotnet build -p:Platform=x64` clean (0 errors, no new
warnings from any touched file). Module test suite green - 1284 passed
(was 1273; +11 new `PlanSolverCraftVendorComparabilityTests`, 3
pre-existing tests updated in place, net test count unchanged for those
three). No new Blish HUD references in tests; every new/updated test
exercises real production code (`PlanSolver.Solve` end-to-end, real
`RecipeNode`/`RecipeOption`/`VendorOffer` fixtures), no contract-mirror/
fake-logic tests. Item/currency/vendor IDs remain internal-only. Pricing
logic continues to preserve multiple sources and avoid inventing
currency exchange rates - this fix tightens that invariant for craft
rather than relaxing it.

No live desktop gate for this pass (solver-only change, seed data with a
currency-ingredient recipe not yet ingested on this branch - see the
"parallel branch" note above).

## Craft/vendor comparability parity fix - adversarial review follow-up (2026-08-15)

A second, adversarial pass over the fix above (Code Reviewer Mode, per
this repo's mandatory Edit -> Review -> Fix loop) found five defects, all
in `Services/PlanSolver.cs`'s recipe loop and terminal fallback branch.
Four were fixed; one is a flagged, deliberately-unfixed heuristic
limitation (documented below, per the task's own "flag genuinely
debatable large fixes rather than expand scope" instruction).

**Fixed (critical): cross-tier scale mismatch at the terminal fallback
comparison.** The terminal tie-break compared `bestFallbackCraftCost` (a
ComparisonValue that could include valued-currency valuation copper)
against `fallbackVendorCoinCost` (real coin only, by the donor's own
"discard valuation once not allValued" design) - two different scales.
Fixed by ranking the craft fallback tier itself on `craftRealCost`
(never the valuation-tainted `craftCost`) and comparing that against
`fallbackVendorCoinCost` at the terminal branch - both sides are now
real coin only, exactly mirroring `EvaluateVendorOffers`' own
fallback-vs-fallback ranking. `bestFallbackCraftCost` and
`bestFallbackCraftRealCost` are now always assigned the same value for
a fallback-tier recipe, so a fallback decision's returned
`ComparisonValue` can never smuggle hidden valuation to a parent either
(closes the same class of leak as the propagation fix below).

**Fixed (mustFix): mixed valued/unvalued currency on one recipe no
longer partially contaminates the fallback ranking.** A recipe's
valuation contribution (`valuationCopper`) is now accumulated separately
from `craftCost` and only folded in when the recipe stays comparable
(`!hasUnvaluedCurrency`) - mirrors `EvaluateVendorOffers`' identical
`valuationCopper`/`allValued` split byte-for-byte. Previously a valued
line's copper was added directly into `craftCost` inline, so a LATER
unvalued line on the same recipe demoted it to fallback without ever
retracting the earlier contribution. Covered by
`RecipeWithBothValuedAndUnvaluedCurrency_DiscardsValuation_RanksOnRealCostOnly`
(also exercises the cross-tier fix above - the two defects compounded on
the same code path).

**Fixed (mustFix): fallback-tier taint now propagates transitively
through ancestor Craft decisions.** `Decision` gained an internal
`HasUnvaluedCurrency` bool (never surfaced on the public
`SolverDecision` - purely a tier-tracking aid, same scope as
`ComparisonValue`), set true at every fallback-tier `Commit` call site.
The recipe loop now ORs a chosen ingredient's own
`HasUnvaluedCurrency` into `hasUnvaluedCurrency` after evaluating it, so
a recipe with NO currency ingredient of its own but that consumes an
ingredient whose OWN decision is fallback-tier is itself demoted to
fallback too. Without this, a currency cost hidden one Craft level down
would "launder" back into a fully-comparable-looking ancestor - the
transitive shape of the exact asymmetry this whole fix exists to close.
Covered by
`FallbackTaintPropagatesThroughAncestorCraft_NeverLaundersHiddenCurrencyCost`.
This also uncovered two pre-existing `PlanSolverVendorOfferTests` (
`VendorCurrencyCosts_MergedAcrossDeduplicatedOccurrences`,
`VendorCurrencyCosts_MergeOverflow_ClampsRatherThanWraps`) that
inadvertently relied on the OLD (buggy) non-propagating behavior to
force an intermediate item to craft via a fallback-vendor-sourced
ingredient despite having its own real TP price - updated in place
(remove that intermediate item's TP price so the fallback craft is still
exercised as the last resort) using the same established pattern already
applied to the 3 pre-existing tests fixed in the base pass above; their
real subject (VendorCurrencyCosts merging across tree occurrences) is
unaffected.

**Fixed (mustFix): a `Currency`-type ingredient tagged with the coin
currency id is now treated as real copper, not an unvaluable currency.**
`Models/CurrencyValuation.cs` hard-throws if ever keyed on
`Gw2Constants.CoinCurrencyId`, so without a dedicated branch a
coin-typed ingredient could never be valued and would unconditionally
demote its recipe to the fallback tier - turning a data quirk (GW2's
v2/recipes ingredients can carry `IngredientType: "Currency"` tagged
with the coin id itself) into a wrong decision. Mirrors
`EvaluateVendorOffers`' identical coin-vs-currency routing
(`VendorBatchSolver.cs` ~230-240): the ingredient's `Quantity` is added
directly to both `craftCost` and `craftRealCost`, no valuation lookup
involved. This fix has two sibling sites that needed the identical
carve-out and were found only because the first regression test written
against the primary fix failed (`RecomputeCraftCosts`, which
re-derives every Craft decision's `TotalCost` bottom-up AFTER
`Evaluate`'s initial commit and previously skipped ALL Currency-type
ingredients unconditionally, silently stripping the coin contribution
back out; and `Collect`, whose top-of-method Currency-node handling
previously folded a coin-typed ingredient into `currencyMap` -
`plan.CurrencyCosts` - alongside genuine non-coin currencies, which
would have mis-tagged real copper as currency id 1 and double-reported
it against the coin already counted in `TotalCost`). All three sites
now agree. Covered by
`CoinTypedCurrencyIngredient_IsRealCoin_NeverDemotesRecipeToFallback`.

**Fixed (adversarial-review self-catch, not in the original finding
list): the new `craftCost = checked(craftCost + valuationCopper)` fold
could throw an uncaught `OverflowException`.** `craftCost` (from
non-currency ingredients) and `valuationCopper` could each individually
stay within `long` range while their sum overflows - the original
inline `checked` add (pre-existing code, one accumulator) caught this at
the point of addition; splitting the accumulation into two variables
(the finding-5 fix above) moved the final combine outside any
try/catch. Wrapped in the same try/catch-and-demote-to-fallback pattern
used everywhere else in this loop for absurd valuation input, rather
than letting a crafting-tree with an extreme currency valuation crash
the whole `Solve()` call.

**Flagged limitation (finding 4, deliberately not fixed - reported per
the task's "flag genuinely debatable large fixes" instruction): the
terminal fallback tie-break can let a vendor offer with a near-zero
coin part beat a craft fallback with a materially higher real coin
cost, even though the vendor's true total cost (its own large unvalued
currency line) is unknown and could be higher.** Concretely: a craft
fallback costing 500 real copper loses to a vendor fallback offer
costing 0 coin + 500,000 units of an unvalued currency, purely because
0 <= 500. This is NOT a scale mismatch (both sides are real coin, after
the finding-1 fix above) and not new unsoundness introduced by this
milestone: it is the identical heuristic `EvaluateVendorOffers`' own
DO-NOT-TOUCH fallback-vs-fallback ranking already uses today (rank by
coin part alone, since currency is unknowable/incomparable across
offers - see that method's doc comment), now visible in a new pairing
(craft vs. vendor) that never existed before this milestone added a
craft fallback tier at all. Rejecting a low-coin-part vendor offer in
favor of craft would require inventing some notion of "this coin part
isn't a meaningful proxy for total cost," which is exactly the kind of
currency-exchange-rate judgment the repo invariant (avoid inventing
currency comparisons) forbids, and the vendor-side ranking rule is
explicitly DO-NOT-TOUCH pattern-donor code this milestone was told to
mirror, not redesign. Per the task's own guidance for this class of
finding, the deliberate choice is documented here (not just left silent)
and locked by a dedicated regression test,
`AllFallback_VendorZeroCoinPart_BeatsHigherRealCraftCost_DocumentedLimitation`,
so a future change to this heuristic is a conscious decision rather than
an untested drift.

**Tests**: 4 new regression tests added to
`PlanSolverCraftVendorComparabilityTests.cs` (one per fixed finding,
plus the flagged-limitation lock), for 15 new tests total in that file
since the base pass; 2 additional pre-existing `PlanSolverVendorOfferTests`
updated in place (see the transitive-propagation entry above) for 5
pre-existing tests updated in place across this whole milestone.

Validation: `dotnet build -p:Platform=x64` clean (0 errors, no new
warnings from any touched file). Module test suite green - 1288 passed
(was 1273 baseline; 1284 after the base pass; +4 new
`PlanSolverCraftVendorComparabilityTests` in this follow-up, 2 more
pre-existing tests updated in place, net test count change +4 from the
base pass's 1284). No new Blish HUD references in tests; every
new/updated test exercises real production code (`PlanSolver.Solve`
end-to-end, real `RecipeNode`/`RecipeOption`/`VendorOffer` fixtures), no
contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only. Pricing logic continues to preserve multiple sources and
avoid inventing currency exchange rates.

## Craft/vendor comparability parity fix - external review, fourth-site finding (2026-08-15)

An external review of the two passes above found one more defect in
`Services/PlanSolver.cs`, in the coin-typed-currency-ingredient carve-out
added by the adversarial-review follow-up pass (the "all three sites now
agree" fix documented above).

**The asymmetry.** That earlier fix made a `Currency`-type ingredient
tagged with `Gw2Constants.CoinCurrencyId` (real copper paid directly as
part of a recipe, not a currency needing a user valuation) contribute to
`decision.TotalCost` via `Evaluate`'s recipe loop and
`RecomputeCraftCosts`, and confirmed it reached the Recipe Tree
(`CraftingTreeNode.SubtreeCost`, sourced from `memo`/`Decisions`) and the
Crafting Steps shopping-list row (`RefreshCraftStepCosts`, which sums
`decision.TotalCost` per craft-step occurrence). It did not reach
`plan.TotalCoinCost` - the Total Cost summary band. That total is built
by summing only `BuyFromTp`/`BuyFromVendor` step costs (deliberately,
to avoid double-counting a Craft step's already-recursive total against
its own Buy-step children) - a coin-typed currency ingredient has no
Buy step of its own, so its copper fell through that sum entirely. The
same reproduction the earlier fix's own test used demonstrates it: a
recipe costing 10 copper (a TP-bought sub-ingredient) plus 50 copper
(a coin-typed currency ingredient) reported `decision.TotalCost == 60`
(Recipe Tree, Crafting Steps row) but `plan.TotalCoinCost == 10` (Total
Cost summary band) - the same "two sections of the same page disagree"
defect class the M34 fix (fcbb277) eliminated for the vendor-batch
correction passes, now reintroduced for this one ingredient shape.
Confirmed latent rather than live on this branch: no seeded recipe in
`ref/recipes_seed.json` currently carries a `Currency` ingredient tagged
with the coin id, but the whole premise of the fix this defect was found
in is that the pending ~188-recipe ingest may bring that shape in, so it
would have shipped armed.

**Fixed (mustFix): `plan.TotalCoinCost` now includes coin-typed currency
ingredients.** `Collect`'s Currency-node handling no longer special-cases
the coin id with an early return; it now folds into `currencyMap` via
the exact same per-tree-occurrence accumulation every other currency
already uses (visited once per occurrence, matching how
`Evaluate`/`RecomputeCraftCosts` already count it exactly once per
occurrence - no double count introduced). `Solve`'s plan-building step
then routes that one `currencyMap` key into `totalCoinCost` (instead of
the ordinary `BuyFromTp`/`BuyFromVendor`-step sum, which it has no step
to be caught by) and excludes it from `plan.CurrencyCosts`, so it still
never double-displays as a "currency 1" line - preserving the original
fix's own display intent, just reaching all four sites instead of three.
No other `currencyMap` consumer is affected:
`VendorBatchSolver.FinalizeVendorBatches` only ever writes non-coin
vendor currency lines into `currencyMap` (vendor's own coin cost is
already routed straight into a Buy step's coin cost, never into
`currencyMap`, at `VendorBatchSolver.cs` ~230-240), so it can never
collide with or double-count the new coin key.

This also corrects `SellSideEconomics`' profit calculation
(`NetSaleValue - Plan.TotalCoinCost`), which previously would have
overstated profit by exactly the hidden coin-ingredient amount for any
plan carrying one - not a separate fix, a direct consequence of
`TotalCoinCost` now being correct.

**Tests**: extended the existing
`CoinTypedCurrencyIngredient_IsRealCoin_NeverDemotesRecipeToFallback`
test (same fixture, no new test method - the finding was that this
exact test's own scenario proved the bug once `plan.TotalCoinCost` was
inspected) with an assertion that `plan.TotalCoinCost == 60` (previously
would have been `10`) and that `plan.CurrencyCosts` never carries a
`CoinCurrencyId` entry. Net test count unchanged (1288) from the prior
pass.

Validation: `dotnet build -p:Platform=x64` clean (0 errors, no new
warnings from any touched file). Module test suite green - 1288 passed
(unchanged from the prior pass's count; no new test methods, one
existing test extended with additional assertions). No new Blish HUD
references in tests; the extended test exercises real production code
(`PlanSolver.Solve` end-to-end). Item/currency/vendor IDs remain
internal-only. Pricing logic continues to preserve multiple sources and
avoid inventing currency exchange rates - this fix corrects a real-coin
total, not a valuation-derived one, so it does not touch the
decision-only valuation principle.

Gate: PASS 2026-08-16 (orchestrator live sandbox session, combined wave-4 staging build). Verified: Amalgamated Rift Essence offers CRAFT and VENDOR side by side with CRAFT winning honestly on the real priced portion (50 vs 60 ectos, identical currency lines washing out); manual VENDOR override honored and re-solved; fallback-tier vendor behavior observed live on a shard-priced vendor path in the Zojja plan. Coin-typed currency TotalCoinCost routing covered by the suite (no live coin-ingredient recipe in the gate scenarios).

## Timestamp date display (all user-facing timestamps gain dates)

User-directed, field-test feedback: "a session could wrap over midnight" -
plain HH:mm/h:mm tt time-only displays are ambiguous across a midnight
boundary, so every user-facing timestamp in the module gains its date. No
transient-event exclusions - the directive covered every one of them, not
just the long-lived ones.

**Sites converted (10 in scope, plus 3 out-of-scope sites pulled in as a
tracked scope expansion).** All ten formerly time-only sites now render with
a date, using `CultureInfo.InvariantCulture`:
- `Module.cs`: the `SaveStatusThreadSafe` "Updated" snapshot status and the
  refresh-failure "{cause}" status (~line 1049 and ~line 1079).
- `Views/LogTabContent.cs`: `FormatLine`'s log-row timestamp, now
  `yyyy-MM-dd HH:mm:ss` (was `HH:mm:ss`).
- `Views/MainView.cs`: the `_clearButton.Click` handler's "Cache Cleared"
  status (~line 247), `RefreshNowAsync`'s "Updated" status (~line 526), and
  its own refresh-failure "{cause}" status (~line 581).
- `Views/SettingsTabContent.cs`: the four "Saved" labels (homestead tiers,
  log retention, snapshot refresh interval, currency valuations).

Scope expansion (needs PR-body ratification, not silently absorbed): three
additional `Views/CraftingPlanView.cs` sites that were already
date-formatted (`SeedRestored`'s "Generated ..." line 724,
`_statusBoard.Finish`'s "Plan generated - ..." line 2618, the W3D banner's
"Generated: ..." line 3083) were converted from ambient `CurrentCulture`
interpolation to `CultureInfo.InvariantCulture` too, to keep all thirteen
timestamp sites in the module agreeing on one culture policy. This was
outside this feature's original brief ("already date-formatted, leave
untouched") and needs the user's explicit sign-off in the PR body, not
silent inclusion.

**InvariantCulture policy.** Every one of the thirteen sites above formats
with `CultureInfo.InvariantCulture` rather than the ambient
`CurrentCulture`: the module's UI strings are English-only, Invariant keeps
month abbreviations and the AM/PM designator stable (under de-DE, `h:mm tt`
yields an EMPTY AM/PM designator - "2:14" would be ambiguous with "14:14"),
and it stops ':' from being culture-substituted inside `HH:mm:ss`.
Documented in-repo at `Views/LogTabContent.cs`'s `FormatLine` (the log
format is the strongest case) and at `Views/CraftingPlanView.cs`'s
`SeedRestored` call (the three out-of-scope sites' own anchor).

**OPEN USER DECISION (layout risk, not silently patched around).** At the
default 930x710 window, the header status label's free run before
`_clearButton`'s left edge is ~524px; the worst-case failure composite
("Refresh partially failed - 3 of 5 sources - Aug 15, 2026 3:41 PM (2h 5m
ago)") is inferred at roughly 490-530px. Overflow slides UNDER the Clear
Cache button rather than being clipped, because the buttons draw on top of
the status label. Options: widen the label's free run, give status its own
row, or accept the risk as-is - awaiting the user's call.

**Recorded facts (not bugs, not fixed).**
- Log search matches the FORMATTED line (post-`FormatLine`), so dates are
  now searchable text - a short numeric query (e.g. "15" or "20") now
  matches nearly every row via the date, not just a rare timestamp
  coincidence.
- Log rows clip ~11 chars more on the right than before (the `yyyy-MM-dd `
  prefix is 11 characters). Copy still exports the full, unclipped line via
  `CopyToClipboard`; a horizontal scroll on the Log tab's content panel is
  the real fix if that tab is revisited.

Gate: PASS 2026-08-16 (orchestrator live sandbox session, combined wave-4 staging build). Verified: Log tab rows render "[INFO] 2026-08-15 21:14:10" ISO-dated; a freshly-produced failure status renders "Refresh failed - GW2 API access not ready - Aug 15, 2026 9:32 PM (25d ago)" with no clipping at the default window width; plan strip and W3D banner dated. Note: a status string persisted by a PRE-fix build renders in the old time-only format until the next status write - expected, not a defect.
---

## Recipe-ingestion bug class: missing schema-version parameter (2026-08-15)

Root-caused by two independent investigations plus orchestrator-verified
live API probes: every recipe API call in this module omitted the GW2
API's `v=` schema-version query parameter entirely. The GW2 API hides an
entire era of recipes - every recipe whose ingredient list can include a
`Currency` (or other non-`Item`) entry - from UNVERSIONED responses:

- Unversioned `/v2/recipes` (the full id list) returns 13,183 ids;
  versioned `/v2/recipes?v=<date>` returns 13,371 - 188 recipes are
  invisible to any unversioned caller, full stop.
- Unversioned `/v2/recipes/14025` (Amalgamated Rift Essence -> item
  100930) 404s outright - "no such id" - even though the recipe fully
  exists; the versioned request returns it complete.
- Even where a recipe exists in both shapes, ingredient JSON differs:
  versioned ingredients always key their item id as `"id"`; unversioned
  ingredients key it as `"item_id"`. The old parser
  (`Gw2RecipeApiClient.ParseRecipe`, and the offline seeder's
  `ParseRecipeBatch`) read `"item_id"` unconditionally, so even an
  ordinary recipe fetched through a hypothetical versioned call would
  have silently parsed every ingredient id as 0 (Newtonsoft's
  `Value<int>` on a missing key) or, in the seeder's `System.Text.Json`
  path, would have THROWN outright (`GetProperty` has no missing-key
  tolerance) - the seeder could not have completed a versioned run at
  all without this fix.

**The fix (`Services/Gw2RecipeApiClient.cs`,
`tools/GW2CraftingHelper.RecipeSeeder/Program.cs`).** Both `/v2/recipes`
callers (the live runtime client and the offline seeder) now append
`v=<pinned date>` to every recipe URL - search, single-recipe detail, and
the seeder's own id-list and batch-detail endpoints. Pinned to a literal
date (each file keeps its own `SchemaVersion` constant, matching the
existing `BaseUrl` duplication pattern between the two files) rather than
`v=latest`: the module wants "the schema version that exists today,
permanently" - a literal date keeps returning today's shape even after a
future upstream schema revision, where `v=latest` would silently start
returning a new shape with no code change to review. Re-pinning the date
is a deliberate, reviewed action, not automatic. Both ingredient parsers
now read `"id"` first, falling back to `"item_id"` only for defense (an
accidental unversioned call, or a future regression) - verified that no
row currently in `ref/recipes_seed.json` needs the fallback at all (every
existing seed row already stores `RawIngredient`'s own C# property name,
not the raw API key). The seeder's `System.Text.Json` fallback also fixes
a real crash: its old unconditional `GetProperty("item_id")` throws on
any versioned typed ingredient, which - now that every seeder call is
versioned - would otherwise fail the very first batch containing a
Currency ingredient.

**Test:** the seeder's own contract-mirror test
(`Gw2RecipeApiClientParseTests.ParseRecipe_IngredientsWithExplicitType_
PreservesType`) fabricated a "hypothetical" shape keyed on `item_id` for
a typed ingredient - not what the real API ever sends, and exactly the
wrong shape to have caught this bug. Replaced with a test built from the
real, byte-for-byte captured response of
`curl "https://api.guildwars2.com/v2/recipes/14025?v=2026-08-15"`, plus a
second test proving the `item_id` fallback still works for the genuinely
hypothetical legacy shape.

**Re-seeded (`ref/recipes_seed.json`, `ref/recipe_search_seed.json`,
`ref/recipe_seed_manifest.json`, `ref/item_name_seed.json`).**
`tools/GW2CraftingHelper.RecipeSeeder` was re-run against the live API
(build 205505; the previous seed was build 195497, generated
2026-02-20 - about six months stale). Recipe 14025 is now present with
`outputItemId: 100930` and its 3 typed `Currency` ingredients (78/80/79)
plus the 1 `Item` ingredient (Glob of Ectoplasm, 19721, count 50); the
stale negative search entry `"100930": []` is gone, replaced by
`"100930": [14025]`. Net +230 recipes (14736 -> 14966) and +248 search
entries (15774 -> 16022, one entry - `100930` - flipping from negative to
real rather than being newly added). This is LARGER than the isolated
188-recipe schema gap alone: six months of ordinary GW2 content patches
landed in between, independently of this fix (confirmed live - e.g.
recipe 7924's `outputItemId` genuinely moved from 48200 to 107474
upstream, same item name "Wei Qi's Warfists Armor" on both ids, a
real ANet re-ID unrelated to this bug). Every RawRecipe/RawIngredient row
also gained the M37 achievement-dedup schema's nullable fields
(`expectedOutputCount`, `achievementId`, `achievementBit`), added to the
model after this seed was last generated - inflating the raw file diff
far beyond the ~230-recipe content change, but not a value change
for any pre-existing recipe. Spot-checked byte-identical (modulo those
new null fields): the full Zojja's Claymore recipe chain (7836/11539/
11517/11548, already covered by `ZojjasClaymoreValidationTests`).

**Adversarial-review catch during re-seeding:** a from-scratch seeder run
is a full regeneration, not a merge - it silently DROPPED the 4
hand-authored M37 achievement-bit recipes (`-1592` through `-1595`,
Infinite Trebuchet Blueprint and its 3 Merchant sub-recipes), since those
were manually spliced into the previously-shipped seed files and are not
derivable from either the live `/v2/recipes` list or
`ref/mystic_forge_recipes.json` - the seeder has no code path that
produces them. Caught only because `RecipeCacheSerializerTests` already
pins their presence. Restored via a throwaway console tool (deleted
before this fix's commits - same "disposable scratch project" precedent
as the W3D KNOWN-ISSUES entry) that loaded both the old and newly
regenerated seed files through the real `RecipeCacheSerializer`
production path and re-serialized the union, so the on-disk shape is
byte-identical to what a seeder that also knew about achievement recipes
would have produced. `RecipeCacheSerializerTests`' pinned counts (recipes
14736 -> 14966, searches 15774 -> 16022) and `ItemNameSeedDataTests`'
pinned count (14587 -> 14762) were updated to match.

**Runtime discovery caveat (documented in code at
`Gw2RecipeApiClient.SearchByOutputAsync` too):** versioning the search URL
only fixes recipes this client can otherwise SEE via search. The GW2
API's own `/v2/recipes/search` index has a SEPARATE gap, independent of
this bug: `/v2/recipes/search?output=100930&v=latest` returns an EMPTY
array live, even though recipe 14025 fully exists and is fetchable by id.
Live search-by-output cannot discover these recipes at all, versioned or
not - only the seeded search index (built by the offline seeder walking
the full `/v2/recipes` id list, never the search endpoint) can. A cache
miss on one of these output items during a live module session will
therefore still come back empty from `SearchByOutputAsync`'s own API
fallback; only the shipped seed protects against that.

**Out-of-scope finding, deferred (not fixed by this pass):** re-running
the seeder with schema versioning incidentally changed how GUILD-gated
recipes are shaped too - an unversioned response puts a guild-upgrade
requirement in a separate top-level `guild_ingredients` array (which this
module has never read, on either side of this fix); the versioned
response folds it directly into `ingredients` as a new, previously-unseen
`"GuildUpgrade"` ingredient type (678 such ingredient rows across
Guild Decoration/Scribe recipes in the current seed, e.g. recipe 9917 ->
item 75375, guild upgrade id 279). Neither `PlanSolver` nor
`CraftingTreeBuilder` has a `"GuildUpgrade"` arm: `PlanSolver.Evaluate`
only short-circuits `"Currency"` to a free leaf, and
`CraftingTreeBuilder.BuildNode` buckets ANY non-`"Item"` type as a
display `Currency` leaf - so a guild-upgrade ingredient renders today as
a generically-named "Currency" leaf (`Gw2Constants.ResolveCurrencyName`
has no entry for a guild upgrade id and falls back to the literal string
"Currency") that costs nothing. Verified NOT a crash risk (empirically,
via `AmalgamatedRiftEssenceIngestionTests.
GuildUpgradeIngredient_DiscoveredByTheSameSchemaFix_DoesNotThrow`, and by
code inspection: `CollectItemIds` already excludes non-`"Item"` node ids
from the TP price fetch, so a guild-upgrade id is never looked up as if
it were a tradeable item). Cosmetic-only, bounded to Guild Decoration
recipes, and needs real design work (a new ingredient-type concept, not
a one-line fix) - left for a future milestone.

**PARTIALLY RESOLVED (2026-08-16):** this finding under-described the real
severity, though not quite the way first written here either (see the
correction below) - a live-API audit found a GuildUpgrade ingredient does
NOT reach the item-pricing path described above (that observation held up:
`CollectItemIds`' `"Item"`-only gate means it never is), but it DOES reach
`PlanSolver`'s vendor-offer evaluation, which prices offers by raw
ingredient id with no `"Item"`-type gate at all - a genuine mis-costing
bug (latent-but-reachable-via-vendor-offers in the current seed, not
merely cosmetic), not just a display gap. See "GuildUpgrade ingredient
costing/display fix" below for the corrected mechanism and the fix (the
renamed test above is now
`GuildUpgradeIngredient_NeverPricedAsItemOrCurrency_DisplaysAsUnresolvedGuildUpgrade`).
**Scope of "resolved" (adversarial review correction, 2026-08-16):** only
the mis-costing bug and the wrong-domain "Currency" mislabel are fixed -
the ORIGINAL deferred item's other half (resolving a GuildUpgrade
ingredient's real upgrade name, and verifying the active character's
guild actually owns/has-unlocked it) remains unimplemented; the leaf
still renders the generic, ID-free "Guild upgrade (unresolved)" label.
See the "GuildUpgrade ingredient costing/display fix" section's own
"Remaining / deferred" note below - this marker previously read simply
"RESOLVED" with no such scoping, which left the deferred remainder
recorded nowhere even though three production comments point readers at
this document for exactly that remainder.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - PASS (0
errors). Tests: `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` - 1276 total (1273 baseline + 3 net new:
the replaced contract-mirror test became 2 real tests, plus the 2 new
`AmalgamatedRiftEssenceIngestionTests`) - PASS.

Gate: PASS 2026-08-16 (orchestrator live sandbox session, combined wave-4 staging build). Verified: Amalgamated Rift Essence is searchable via the regenerated name seed, recipe 14025 resolves from the regenerated recipe seed with its three currency ingredients plus 50 Globs of Ectoplasm across all nine disciplines, currency leaf names resolve correctly via live metadata (Fine/Masterwork/Rare Rift Essence), full plan generates in ~2s. The orchestrator independently verified the committed seed files contain 14025 and the cleared "100930" negative search entry before the gate.

---

## W4A: Total Cost section redesign (2026-08-15)

User-designed spec (the user personally iterated on this layout before
handing it off). Implemented in the isolated `wt-cost` worktree off
`origin/master` (`727c90b`) on branch `cost-section-redesign`.

**1. Two formula bands, replacing the old flat cost-tile row
(`Services/PlanViewModelBuilder.cs` `BuildCostFormulaBand`/
`BuildProfitFormulaBand`, `Views/Rendering/SummarySectionRenderer.cs`
`CreateFormulaBand`).** Band 1 reads "Total Materials Value - Your
Materials Used = Actual Cost to Craft"; Band 2 (only when
`CraftingPlanResult.NetSaleValue.HasValue`) reads "Sell Value - Total
Materials Value = Profit/Loss if Sold". Actual Cost to Craft and Sell
Value/Profit are exactly the pre-existing `TotalCoinCost`/`NetSaleValue`/
`CraftingProfit` math, untouched; Total Materials Value is new, computed
for display only. COLLAPSE RULE (user-mandated): Band 1 collapses to a
single "Actual Cost to Craft" tile when `MaterialOpportunityCost` is null
or 0 - the formula is meaningless with no middle term. Both bands render
through the same `CreateFormulaBand` tile-row geometry the pre-W4A
`CreateCostTileRow` already used (`PlanRelayoutMath.
ComputeCostTileGeometry`, unchanged), just called once per band instead
of once over every coin row flattened together - two bands now render as
two stacked tile rows, not one wider one.

**2. Band 2's identity was verified, not assumed - and does NOT
universally hold.** The task's own instruction was to verify
`CraftingProfit == NetSaleValue - TotalCoinCost - MaterialOpportunityCost`
before wiring Band 2's middle tile to `TotalCoinCost + MaterialOpportunityCost`
(Band 1's formula). Reading `Services/SellSideEconomics.cs` end to end:
the identity holds exactly for a single-item plan
(`ApplySellSideEconomics`, `profit = NetSaleValue - solveResult.Plan.
TotalCoinCost - materialOpportunityCost`) but explicitly NOT for a
multi-item batch - `CraftingPlanResult.CraftingProfit`'s own doc comment
states the batch cost subtracted is "NOT Plan.TotalCoinCost, which also
includes every requested root that has no live sell price" (i.e. every
unsellable root in the batch). Using `TotalCoinCost + MaterialOpportunityCost`
for Band 2 would therefore show a middle tile that does not arithmetically
balance the visible Sell Value/Profit numbers for a multi-item batch with
any unsellable root. Fixed by deriving Band 2's Total Materials Value as
`NetSaleValue - CraftingProfit` instead - reusing ONLY the two
already-stored, already-correct fields (never recomputing `CraftingProfit`,
never reading `TotalCoinCost` in this band at all). This is algebraically
IDENTICAL to Band 1's own Total Materials Value for every single-item plan
(proven by `PlanViewModelBuilderSummaryTests.
ProfitBand_TotalMaterialsValueMatchesCostBand_ForSingleItemPlan`), so the
two bands always agree there; for a multi-item batch with a
partially-unsellable root mix the two bands can legitimately show
different numbers under the same "Total Materials Value" label (Band 1
prices the whole batch, Band 2 only its sellable portion, matching what
`CraftingProfit` itself measures) - Band 2's tile carries an extra
tooltip clause for that case rather than silently showing a formula that
would not visually balance.

**3. Mouseover tooltips on every formula-band header (user-mandated).**
`PlanRowViewModel` gained `TooltipText`; `SummarySectionRenderer.
CreateFormulaBand` sets it directly on the caption `Label` control
itself, never on the tile's containing `Panel` - the M32 lesson
(`docs/KNOWN-ISSUES.md`'s "Field-test UX wave", finding D) is that a
label captures the mouse before a container tooltip underneath it would
ever be reached, so the tooltip has to live on the exact control that
receives the hover. Wording matches the spec's exact text for all five
headers; the pre-existing "(buy-order prices)"/"(Nx, ...)"/"(batch
total, ...)"/"(coin costs only)" qualifiers that used to live inline in
row Labels all moved into these tooltips instead, since a formula-band
caption has to stay short to read as a formula.

**4. Currency table replaces the plain-text currency rows
(`SummarySectionRenderer.CreateCurrencyTable`/
`CreateCurrencyTableHeaderRow`/`CreateCurrencyTableRow`).** Columns:
Currency (icon + name) | Required | Have | Needed. The 4-column shape
does not fit `CTableHeaderRenderer`'s left/middle/right (3-slot)
signature, so the header is hand-rolled - the same precedent
`ShoppingListSectionRenderer.CreateShoppingListHeaderRow` already set for
its own 4-column (Item/Amount/Each/Total) header, rather than stretching
`CTableHeaderRenderer` to fit a shape it was not designed for. Column
geometry is new pure arithmetic in `Services/SummarySectionLayoutMath.
ComputeCurrencyColumnEdges` (fixed-width right-to-left columns - Required/
Have/Needed are always short plain integers, no coin icons, so no
per-render widest-value pre-scan is needed the way the Shopping List's
Each/Total columns need one). Rows sort alphabetically by resolved
currency name (user-mandated) via a stable `OrderBy`, not `List.Sort`
(unstable) - two different unknown currency ids both fall back to the
same generic "Currency" name, and an unstable sort could reorder that
tied pair nondeterministically run to run. `Have` is now the RAW,
UNCLAMPED wallet holding (`PlanRowViewModel.CurrencyOwnedQuantity`'s
contract changed from `Math.Min(owned, Required)` pre-W4A to the real
holding - user-mandated); `Needed` (`CurrencyNeededQuantity`, new field)
is `max(0, Required - Have)`; both are null (never a fabricated 0) when
no wallet snapshot exists. Rows where `Have >= Required` get
`CurrencyFullyCovered = true` (new field), rendered as a green "OK" badge
at the row's right edge.

**5. Glyph verification: check-mark vs "OK" badge fallback.** The spec
asked for a green check-mark glyph, with an explicit authorization to
fall back to a green "OK" text badge if the glyph could not be verified
to render in the Blish font, and an explicit ban on color-emoji
codepoints. No live Blish HUD session was available in this environment
to render-test the glyph directly. This module's own prior investigation
(`docs/dev-notes/HISTORY.md`, "Carried follow-up resolved: caret glyphs")
already found that a technically-representable Unicode glyph (a triangle
expand/collapse indicator) was NOT the reliable choice for this exact
font once live-tested across multiple desktop sessions/machines - ASCII
carets were kept instead. Given that precedent and no way to independently
verify a different, also-unverified glyph here, this package takes the
pre-authorized safe fallback: a small green "OK" pill via the existing
`LabelHelpers.CreateSmallTag` helper (same one the tree's Locked/Available
pills and the shopping source tag already use), colored to match
`PillColors.PillKind.Selected`'s green (#1F8F0C) rather than adding a new
`PillKind` for this single non-tree use. **A live desktop check of this
one glyph decision remains open** - if a future session confirms the
check-mark glyph (the escaped form, backslash-u-2713 - see the
ASCII-only-source rule) renders cleanly in this font, swapping it in is
a one-line change in `SummarySectionRenderer.FullCoverageMarkerText`.

**6. Footnote row (user-mandated).** A new `PlanRowType.SummaryFootnote`
row, always exactly one, always last (after the pre-existing multi-item
`MultiItemNote` banner when both are present) - subdued styling
(`DefaultFont12`, dim grey `(130,130,130)`, via a new `CreateFootnoteRow`)
distinct from `MultiItemNote`'s plain `TextRowRenderer.CreateTextRow`
styling, so it reads as fine print rather than plan-specific information.
Text: "Prices are Trading Post data - actual purchase and sale prices are
likely to vary."

**7. Height agreement lives in a new class, not
`PlanContentHeightMath.cs` (DO-NOT-TOUCH for this package).** The
redesigned section's shape (two independently-present tile rows, a
currency table header + N rows, a note row, a footnote row) cannot be
expressed by `PlanContentHeightMath.SummaryBodyHeight`'s pre-W4A formula
(a single boolean "has a coin row" flag good for exactly one
`CostTileRowHeight`, not two independently-gated bands) without editing
that method - and `Services/PlanContentHeightMath.cs`/`PlanRelayoutMath.cs`
were both explicitly DO-NOT-TOUCH for this package (shared infrastructure
several other sections' row builders depend on, plus other in-flight
work touching the same files). Resolution: a new `Services/
SummarySectionLayoutMath.cs` (`BodyHeight`, `ComputeCurrencyColumnEdges`) -
Blish-free, unit-tested, reusing `PlanContentHeightMath`'s existing public
row-height CONSTANTS directly rather than redefining them, only owning
the Summary-specific COUNTING logic. `Views/CraftingPlanView.cs`'s one
real call site (`CreateCollapsibleSection`) now special-cases
`PlanSectionType.Summary` to call `SummarySectionLayoutMath.BodyHeight`
instead of `PlanContentHeightMath.SectionBodyHeight`.
`PlanContentHeightMath.cs` itself has ZERO diff from this package -
its own private `SummaryBodyHeight` method and its existing
`PlanContentHeightMathTests.cs` coverage still compile and still pass
exactly as before, they are simply no longer reached for a real Summary
section. `PlanRowType.CoinTotal` (the enum member that dead method still
references by name) is likewise kept, unused by new code, purely so that
file keeps compiling unmodified - see that enum member's own doc comment.

**8. No per-row divider on the new currency table rows (review
self-catch).** `RowRelayoutHelpers.FinishRow` (the divider-plus-relayout
helper every other c-table row in this file uses) was tried first and
then deliberately backed out: `CurrencyRowHeight` (28px) was never part
of the M36b `Container.Paint` round-trip simulation sweep that
`LabelHelpers.CreateRowDivider`'s own doc comment documents (only 44px/
32px rows are proven vulnerable to the vanishing-divider defect and only
36px rows are proven immune - 28px is neither), and the pre-W4A Summary
section deliberately had no per-row dividers at all by its own original
doc comment. Adding one at an unproven row height, for a visual element
the spec never actually asked for, would have risked resurrecting
exactly the defect DO-NOT-TOUCH #6 (divider math) exists to stay clear
of. Currency rows resize via a plain `AddRelayout` closure instead, with
no divider - the header row's dark background alone delineates the
table, matching gw2e's own header-only table styling.

**9. Review self-catch, then a second self-catch on the first
(adversarial-review fix round): a raw Unicode check-mark character was
never shipped in any `.cs` source file - `Views/Rendering/
SummarySectionRenderer.cs`'s own glyph-decision comment (see item 5)
has always used the properly-escaped textual form (backslash-u-2713),
verified by a non-ASCII grep of every touched `.cs` file before commit
(zero hits). The raw character instead leaked TWICE into this very
markdown file's own prose while drafting items 5 and 9 above - a
record about catching a Unicode paste that itself contained a Unicode
paste, which was also factually wrong about what the `.cs` file
contains (it does not, and never did, carry a checkmark glyph in any
form - `SummarySectionRenderer.FullCoverageMarkerText` ships the ASCII
`"OK"` text badge per item 5). Caught in a later adversarial-review
pass and replaced with plain ASCII description; this file itself now
carries zero non-ASCII bytes, matching its pre-W4A state.

**10. Tests (Blish-free, real `PlanViewModelBuilder.Build` production
path).** `PlanViewModelBuilderSummaryTests.cs`,
`PlanViewModelBuilderSellEconomicsTests.cs`, and
`PlanViewModelBuilderMultiItemTests.cs` were extended/rewritten in place
(same files, same focus, new row shape) rather than duplicated: cost-band
collapse rule (both the null AND the exactly-zero
`MaterialOpportunityCost` case), cost-band arithmetic (`Total Materials
Value == Actual Cost to Craft + Your Materials Used`), profit-band
presence/absence, profit-band arithmetic including the single-item/
multi-item identity divergence (item 2 above), loss sign, tooltip
qualifier placement (buy-order basis, overproduction, batch/coin-costs-
only, all now asserted via `Contains` on `TooltipText` rather than the
old `Label`-suffix assertions), currency-row alphabetical ordering,
unclamped Have plus the derived Needed/FullyCovered fields across
covered/gap/no-wallet-data/wrong-currency-id cases, and the always-present
footnote row. A new `SummarySectionLayoutMathTests.cs` covers `BodyHeight`
(null/empty, collapsed vs. expanded cost band collapsing to the SAME one
tile-row height, both bands stacking to two, currency header+rows,
note+footnote rows, and a full-section combination) and
`ComputeCurrencyColumnEdges` (right-to-left ordering, panel-width
scaling). No Blish HUD/`Gw2Sharp` references in any new/changed test; no
fake file I/O; every assertion drives the real `PlanViewModelBuilder.
Build(CraftingPlanResult)` entry point.

**11. Adversarial-review fix round (2026-08-15) - 7 findings fixed from
an independent code review of this package (5 file-scoped, 2 process-
level).** All fixed in the same `wt-cost` worktree/branch, small logical
commits, before any push/PR:

- **Footnote height desync (Critical-adjacent Must Fix,
  `Views/Rendering/SummarySectionRenderer.cs` `Render`).** The renderer
  kept only the LAST `SummaryFootnote` row (`footnoteRow = row`,
  overwriting) while `SummarySectionLayoutMath.BodyHeight` sums
  `FallbackTextRowHeight` per footnote row it counts - the two agreed by
  coincidence only because exactly one footnote row is ever emitted
  today. Fixed by collecting into a `footnoteRows` List (mirrors the
  pre-existing `noteRows` pattern) and rendering every entry, so the
  renderer and the height math can never desync regardless of how many
  footnote rows a future change emits.
- **Ellipsized currency-name tooltip swallowed (Must Fix,
  `CreateCurrencyTableRow`, both the build path and its `AddReellipsis`
  closure).** The M32 lesson (this file's own "Field-test UX wave"
  finding D) is that a label captures the mouse before a tooltip on a
  control underneath it is ever reached; the currency table's `nameLabel`
  sat directly on top of its own truncated text with the tooltip stamped
  only on the containing `rowPanel`, so hovering the visibly-truncated
  name showed nothing. Fixed by stamping `BasicTooltipText` on
  `nameLabel` AND `rowPanel` in both places. This is a repo-wide pattern
  (confirmed by grep: `Views/Rendering/DisciplinesSectionRenderer.cs`:193/
  220, `UsedMaterialsSectionRenderer.cs`:89/121, and
  `ShoppingListSectionRenderer.cs`:227/284 all stamp the tooltip on the
  row panel only, never on the name label sitting over the truncated
  text) - per the "fix the class, not the instance" directive, the sweep
  is reported here, but those three files are pre-existing, untouched by
  this branch, and outside this package's scope (only the
  `SummarySectionRenderer.cs` instance introduced by W4A is fixed here);
  the same one-line fix applies to each and is left as a follow-up.
- **Formula-band operators never drawn (Must Fix,
  `CreateFormulaBand`).** The band read as three same-shaped tiles with
  no visible "-"/"=" relationship between them - exactly the "two-tile
  split-column band" ambiguity the redesign exists to remove, arguably
  worse (now two such unlabelled bands instead of one). Fixed by drawing
  a small dim `Label` centered on each tile boundary (no tooltip, so it
  never steals hover from a neighboring caption) - `"-"` between every
  pair but the last, `"="` for the last, matching the spec's formula
  text exactly. Never drawn for a collapsed 1-tile band.
- **Band 2's middle tile shared Band 1's caption despite legitimately
  differing (Must Fix, `Services/PlanViewModelBuilder.cs`
  `BuildProfitFormulaBand`).** For a multi-item batch with an unsellable
  root, Band 1's and Band 2's "Total Materials Value" tiles can hold
  DIFFERENT numbers (see item 2 above) with only a tooltip to
  disambiguate - two identically-labeled tiles showing different numbers
  reads as a bug, not a scoping nuance, especially in a section whose
  whole point is now to read as a balancing formula. Fixed by giving the
  multi-item case its own caption, `"Materials Value (sellable)"` (new
  `MaterialsValueSellableLabel` const); single-item plans are unaffected
  (still `"Total Materials Value"`, matching Band 1 exactly, per the
  proven identity).
- **The multi-item divergence itself was asserted only in
  prose/comments, never by a running test (Must Fix,
  `tests/.../PlanViewModelBuilderMultiItemTests.cs`).** New
  `MultiItemRequest_UnsellableRootPresent_ProfitBandMiddleTileDivergesFromCostBand`
  models a batch where `CraftingProfit` is set the way
  `ApplyBatchSellSideEconomics` would actually produce it for a
  partially-unsellable root mix (sellable-root-only cost, never the
  whole-batch `TotalCoinCost`) and asserts Band 1's and Band 2's middle
  tiles both hold their own correct-but-different values, plus the new
  distinct caption above.
- **`CurrencyNumberColumnWidth`'s fixed 60px floor had no widest-value
  pre-scan, unlike the sibling `ShoppingColumnMath` (Must Fix,
  `Services/SummarySectionLayoutMath.cs` +
  `Views/Rendering/SummarySectionRenderer.cs`).** The class doc comment's
  claim that Required/Have/Needed have "no realistic risk of a value
  needing more than a handful of digits" stopped being true the moment
  the Have column was unclamped to the real wallet holding (item 4
  above) - Karma routinely reaches 6-7 digits, which can plausibly
  exceed 60px, and `CreateRightAlignedLabel` grows leftward from the
  column's own right edge, so an unreserved overlong value would
  visually intrude into its left neighbor rather than clip. Fixed the
  same way `ShoppingColumnMath.ComputeEdges` already solves this: a new
  `EffectiveCurrencyNumberColumnWidth(widestNumberWidth)` widens the
  reserved band past the 60px floor when needed, and
  `ComputeCurrencyColumnEdges` gained an optional `widestNumberWidth`
  parameter (defaults to 0, reproducing the exact prior fixed-60px
  geometry for every existing caller/test) that the renderer now feeds
  from a per-render pre-scan of the section's own Required/Have/Needed
  strings - mirrors `ShoppingListSectionRenderer.Render`'s own
  `maxEachWidth`/`maxTotalWidth` pre-scan shape exactly. Five new
  `SummarySectionLayoutMathTests.cs` cases cover the floor/widen
  boundary and prove the default-parameter path is byte-identical to
  the pre-fix geometry.
- **`docs/KNOWN-ISSUES.md` (this file) contained two raw Unicode
  check-mark characters despite claiming, in the very sentence
  containing one of them, that the check-mark had been reduced to its
  escaped form (process-level Must Fix, not a `.cs` file).** Neither
  ever shipped in source - `SummarySectionRenderer.cs` has always used
  the properly-escaped textual form in its own comment, verified again
  by a fresh non-ASCII grep of every touched `.cs` file (zero hits) as
  part of this round. The raw characters existed only in this markdown
  file's own prose (items 5 and 9 above) - a record about catching a
  Unicode paste that itself contained a Unicode paste, and factually
  wrong about what the `.cs` file contains. Both instances rewritten in
  plain ASCII; item 9's text above corrected to describe the reality
  precisely. This file now contains zero non-ASCII bytes again, matching
  its pre-W4A state.

Re-validated after all seven fixes above: `dotnet build -p:Platform=x64`
clean, 0 errors - a fresh warning check against the touched files found
none from `SummarySectionRenderer.cs`/`SummarySectionLayoutMath.cs`, and
the pre-existing StyleCop warnings elsewhere in `PlanViewModelBuilder.cs`
all sit on lines outside this round's diff. `Services/
PlanContentHeightMath.cs`/`PlanRelayoutMath.cs`/`Services/ModuleLog.cs`
remain zero diff; `Views/Rendering/TreeSectionController.cs` was not
touched. Full validation numbers below.

Validation: `dotnet build -p:Platform=x64` clean (0 errors; zero new
warnings from any touched/new file, across both the original pass and
the review-fix round). Module test suite green - 1303 passed, 0 failed
(was 1273 before this whole package; +30 net new tests: +24 from the
original pass, +6 from the review-fix round above - 1 multi-item
divergence test plus 5 `SummarySectionLayoutMathTests.cs` cases for the
widened-column geometry). No new Blish HUD references in tests; every
new/changed test exercises real production code paths with no
contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only (the currency table's Label is now the resolved NAME only,
never the id). Coin amounts still render icon-right-of-number throughout
(both formula bands reuse `CoinCurrencyRenderer.BuildCoinSegments`/
`LayoutCoinSegments` unchanged). Not regressed: `PlanContentHeightMath.cs`/
`PlanRelayoutMath.cs` have zero diff; `Views/Rendering/
TreeSectionController.cs` was not touched; every other section renderer
(Used Materials, Shopping List, Crafting Steps, Required Disciplines,
Required Recipes) is untouched.

No live desktop verification was performed for this package (browser/game
automation was out of scope for this session) - item 5's glyph choice and
the overall visual layout (including the review-fix round's new formula-
band operators and widened currency columns) are unverified live and
should get a look in a real Blish HUD session before this is considered
fully done.

**12. Adversarial-review fix round 2 (2026-08-15) - 1 blocking finding
fixed from a second independent code review, this one specifically
targeting round 1's own fixes.**

- **Loss-band `"="` operator asserts a false equation (Must Fix,
  `Views/Rendering/SummarySectionRenderer.cs` `CreateFormulaBand`,
  introduced by round 1's "formula-band operators never drawn" fix
  above).** Round 1 started literally drawing `"-"`/`"="` between tiles,
  but the profit band's loss tile has always shown `Math.Abs(profit)`
  under a `"Loss if Sold"` caption (the pre-existing sign convention,
  predating both review rounds - coin amounts render via
  `CoinCurrencyRenderer.BuildCoinSegments`, which clamps negative input to
  0, so there was never a way to show a signed coin value without
  touching that shared, reused-not-modified machinery). Once round 1 made
  the band's final boundary a literal `"="`, that pre-existing convention
  became actively wrong on screen: `PlanViewModelBuilderSummaryTests.
  ProfitBand_NegativeProfit_LabeledLossWithAbsoluteValue`'s own numbers
  (`NetSaleValue = 340`, `TotalMaterialsValue = 500`, `profit = -160`)
  render as `"340 - 500 = 160"`, which is false (the true right-hand side
  is -160). This is the common case, not an edge case - most GW2 recipes
  craft at a loss. Fixed by giving `PlanRowViewModel` a new
  `FormulaResultIsExact` field (default `true`, read only on a band's
  LAST tile): `PlanViewModelBuilder.BuildProfitFormulaBand` sets it to
  `profit >= 0` on the Profit/Loss tile (the only row either band ever
  sets it false on - Band 1's three non-negative terms always balance
  exactly, so every other tile keeps the true default);
  `CreateFormulaBand` reads it to choose the final boundary's symbol -
  `"="` when true, a new neutral `":"` separator (`NeutralResultSeparator`
  - deliberately not `"-"`, which would misread as a second subtraction,
  and not `"="`, the exact claim being removed) when false. The
  non-final boundary (Band size is always 1 or 3 tiles; only a 3-tile
  band has a non-final boundary at all) is untouched - the left two
  tiles' own subtraction was never in question, only whether the FINAL
  tile's displayed value is the true right-hand side. `Math.Abs(profit)`
  and the `"Loss if Sold"` caption are both UNCHANGED (grep-swept: the
  only `Math.Abs` call in `Services/`/`Views/`/`Models/` outside test
  code is this one, so there is no sibling instance of this pattern to
  fix elsewhere) - this fix only changes which punctuation mark is drawn
  at one boundary, never the coin math, never the caption text, and never
  touches `CoinSegmentMath`/`CoinCurrencyRenderer` (reused as-is, per
  task instruction). Covered by three new/extended
  `PlanViewModelBuilderSummaryTests.cs` cases:
  `ProfitBand_NegativeProfit_LabeledLossWithAbsoluteValue` now also
  asserts `FormulaResultIsExact == false`; a new
  `ProfitBand_ZeroProfit_FormulaResultIsExactTrue` covers the `profit ==
  0` boundary of the `>= 0` check (identity holds exactly there too, not
  just for strictly positive profit); and both
  `ProfitBand_SellPricePresent_ThreeTilesWithIdentityArithmetic` and
  `CostBand_MaterialsUsedPositive_ExpandsToThreeTilesWithCorrectArithmetic`
  gained an explicit `FormulaResultIsExact == true` assertion on their
  respective bands' last tile. `SummarySectionRenderer.cs` itself stays
  Blish-bound and untestable directly per the repo's test invariants, so
  the operator-selection LOGIC is asserted at the data-flag level
  (`FormulaResultIsExact`) rather than the rendered glyph - the same
  boundary the class's own existing tests already draw for tooltip text
  and coin values, which are likewise never rendered in a test.

Re-validated after this round-2 fix: `dotnet build -p:Platform=x64`
clean, 0 errors (only pre-existing StyleCop warnings, none on lines this
round touched). Module test suite green - 1304 passed, 0 failed (was
1303 after round 1, before this whole W4A package's baseline of 1273;
+31 net new tests overall, +1 from this round: three cases extended/
added, but `ProfitBand_ZeroProfit_FormulaResultIsExactTrue` is the only
wholly new `[Fact]`). No new Blish HUD references in tests; the new/
changed assertions all still drive the real `PlanViewModelBuilder.
Build(CraftingPlanResult)` entry point. Not regressed: `Services/
ModuleLog.cs`/`PlanContentHeightMath.cs`/`PlanRelayoutMath.cs`/scroll
machinery/merged-ceil vendor batching all remain zero diff across both
review rounds; `Views/Rendering/TreeSectionController.cs` was not
touched; `CoinSegmentMath`/`CoinCurrencyRenderer` were read but not
modified - the fix lives entirely in `PlanRowViewModel` (new field),
`PlanViewModelBuilder.BuildProfitFormulaBand` (sets it), and
`SummarySectionRenderer.CreateFormulaBand` (reads it). Item/currency/
vendor IDs remain internal-only; coin amounts still render icon-right-
of-number throughout (unchanged). No live desktop verification was
performed for this round either, same caveat as item 11's own closing
paragraph above.

Gate: PASS 2026-08-16 (orchestrator live sandbox session, combined wave-4 staging build). Verified: cost-band collapse rule (single Actual Cost to Craft tile when opportunity cost is zero - ARE and Zojja plans), currency table alphabetical with icons and correct Required/Have/Needed math, green OK badges on all fully-covered rows, TP-variance footnote, band-caption tooltip renders on hover, coin icons right of numbers. Checkmark-glyph experiment FAILED live (U+2713 renders as an empty tag in the Blish font) - the OK badge is the permanent marker choice.

Follow-ups (recorded during a later polish pass, not yet implemented):

- Follow-up: delete `PlanContentHeightMath.SummaryBodyHeight`, its tests
  (`PlanContentHeightMathTests.cs` ~348-390), and `PlanRowType.CoinTotal`
  once the DO-NOT-TOUCH freeze on `PlanContentHeightMath` lifts - all
  three are dead for production since `CraftingPlanView` routes Summary
  to `SummarySectionLayoutMath`.
- Follow-up (user decision pending): the Summary currency table now
  shows the RAW wallet holding in Have, while the shopping list still
  clamps its per-currency owned amount to the required amount
  (`CurrencyDisplayResolver.ResolveAmounts`) - the same currency can
  show two different owned numbers in one window; decide whether to
  unclamp the shopping list to match.

---

## GuildUpgrade ingredient costing/display fix (2026-08-16)

Orchestrator-checksummed audit finding (confirmed via live API): the versioned
GW2 API returns ingredient `{type:"GuildUpgrade", id:<upgradeId>, count:N}` on
Guild Decoration recipes (e.g. recipe 12002 -> item 80471, guild upgrade id
829; 678 occurrences across 225 distinct ids in the current seed). The
"Recipe-ingestion bug class" entry above first surfaced this as a cosmetic
display gap; a deeper audit found it was a real mis-costing bug too,
reachable via `PlanSolver.Evaluate`'s vendor-offer path
(`VendorBatchSolver.EvaluateVendorOffers` keys `vendorOffers` by the raw
ingredient id with no `"Item"`-type gate) - latent in the current seed (no
`GuildUpgrade` id collides with a vendor-offer `outputItemId`, seed `Item`
id, or `KnownCurrencyNames` key) but not enforced by any guard before this
fix.

**Delivered behavior.** A `GuildUpgrade`-typed ingredient, and any OTHER
ingredient type this module does not specifically recognize (a future GW2
API type), is now handled consistently and safely at every site that
touches ingredient types, on an Item-positive basis
(`IngredientType != "Item"`) rather than an enumerated deny-list - so a type
the module has never seen is unpriceable and undisplayable-as-an-item by
construction, not by luck:

- `Models/CraftingDecision.cs` has two new members, `GuildUpgrade` and
  `UnrecognizedIngredient`, both appended LAST (this enum has no
  `StringEnumConverter` and round-trips through `plan.json` as a raw
  ordinal int - inserting either earlier would misread previously-persisted
  plans). Its XML doc comment is the one canonical explanation of the
  id-space rationale (a guild upgrade id, a wallet currency id, and an item
  id are three distinct id spaces with no defined relationship to each
  other) - other sites below point back to it instead of repeating the
  rationale.
- `Services/CraftingTreeBuilder.cs`'s `BuildNode` has three ordered leaf
  branches before the ordinary decision lookup: `"GuildUpgrade"` (generic
  "Guild upgrade (unresolved)" label plus an `AcquisitionHint`
  explanation), `"Currency"` (name/icon resolved through
  `CurrencyDisplayResolver`, not item metadata), and a catch-all for
  anything else (`"Unrecognized ingredient type"` label). All three
  explicitly clear `IconUrl`/`Rarity` (and, for the catch-all,
  `Name`/`AcquisitionHint`/`AcquisitionBadge` too) rather than leaving them
  at the generic item-keyed lookup every node gets by default -
  `metadata`/`hints` can carry a colliding entry for the same raw id via
  routes other than the Item-only ingestion path. Each of the three gets
  its own `CraftingDecision` value (never shares `Unknown` with a genuine
  no-source Item node), because `DecisionPillPlanner` cannot otherwise tell
  them apart from a real "no feasible source" Item leaf and would attach a
  live, interactive IGNORE pill keyed on a non-item id.
- `Services/PlanSolver.cs`'s `Evaluate`, `Collect`, and `RecomputeCraftCosts`
  all guard on `IngredientType != "Item"`. Currency ingredients keep their
  existing valuation-aware pricing; every other non-Item type (GuildUpgrade,
  or an unrecognized type) contributes zero to `craftCost`/`craftRealCost`,
  never touches `currencyValuation`/`GetBuyCost`/vendor offers, never
  accumulates into `plan.CurrencyCosts`, and demotes its containing recipe
  to the fallback tier via the existing `HasUnvaluedCurrency` machinery
  (propagated transitively through Craft ancestors, unchanged).
- `Services/RecipeService.cs`'s `BuildNodeAsync` guards the same way
  (`ingredientType != "Item"`), so a null or empty `IngredientType` - a
  defensive case, not reachable with today's seed data, but a real
  historical shape (commit e81b7e4) - is treated as an unexpanded leaf
  consistently with how `PlanSolver`/`CraftingTreeBuilder` will handle that
  same node, instead of being expanded as if it were a real item.
- `Services/VendorBatchSolver.cs`'s `EvaluateVendorOffers` cost-line
  classification loop has a final `else` alongside its `Currency`/`Item`
  branches: an unrecognized `CostLine.Type` marks the offer unpriceable
  (`priceable = false; break;`) instead of silently contributing nothing
  and letting the offer win at an understated price. The merged-ceil
  batching math below the loop is unchanged.
- `Services/DecisionPillPlanner.cs` gives `GuildUpgrade`/`Currency`/
  `UnrecognizedIngredient` each a single, non-interactive locked pill
  (`GUILD UPGRADE`/`CURRENCY`/`UNRECOGNIZED`) - never the `IGNORE` toggle.
- `Views/Rendering/TreeSectionController.cs` (Blish-bound, untestable
  directly) gives the `Unknown`/`GuildUpgrade`/`UnrecognizedIngredient`/
  `Currency` locked pills real, honest tooltips instead of the generic
  "Only available source" text a locked pill gets by default (accurate
  only when there genuinely is exactly one feasible source).

**Sweep (repo rule: fix the class, not the instance).** Every
`IngredientType`/`RawIngredient.Type`/`CostLine.Type` comparison in
`Services/`/`Models/` was grepped and checked against this Item-positive
shape: `AchievementBitDedupPrePass`, `CraftingPlanPipeline`'s
override/id-collection helpers, and `InventoryReducer` were already
`"Item"`-gated with no changes needed; `CraftingPlanPipeline`'s
vendor-offer-currency-id collectors compare `CostLine.Type` (a different
field with no `"GuildUpgrade"` concept) and are unrelated.

**Tests:** 1396 total (0 failed), spanning `PlanSolverGuildUpgradeTests.cs`,
`CraftingTreeBuilderTests.cs`, `DecisionPillPlannerTests.cs`,
`AmalgamatedRiftEssenceIngestionTests.cs`, `RecipeServiceTests.cs`, and
`PlanSolverVendorOfferTests.cs` - real production code paths
(`PlanSolver.Solve`, `CraftingTreeBuilder.BuildTree`,
`RecipeService.BuildTreeAsync` end-to-end against real `RecipeNode`/
`ItemPrice`/`VendorOffer`/`CurrencyValuation`/`ItemMetadata` fixtures), no
contract-mirror/fake-logic tests, no Blish HUD references.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - PASS, 0
errors. Pre-existing StyleCop analyzer warnings unchanged in nature
(measured via a forced full rebuild; an incremental no-op build prints
0 warnings and must not be quoted as the warning count). The only
warnings on lines this fix added are two instances (SA1513/SA1515,
`Views/Rendering/TreeSectionController.cs:1146-1147`) of the
comment-placement pattern that already warns three times in the same
else-if chain. Tests: `dotnet test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` - 1396 total,
0 failed.

**Remaining / deferred.** This fix makes a `GuildUpgrade` ingredient (and
any other unrecognized ingredient type) safe to price and display - never
mis-costed, never mislabeled as a wallet currency or a real item - but does
not resolve what a `GuildUpgrade` ingredient actually IS. Two pieces remain
unimplemented: (1) the upgrade's real name and icon - the leaf still
renders the generic, ID-free "Guild upgrade (unresolved)" label; the live
GW2 API's `/v2/guild/upgrades/{id}` endpoint is the candidate future source
for both, needing a new metadata service and name/icon cache, neither of
which exist yet; (2) verifying the active character's claimed guild
actually owns/has-unlocked that upgrade, which would need the
authenticated `/v2/guild/:id/upgrades` endpoint plus a guild-membership/
permission concept this module has none of today. Separately,
`RecipeService.BuildNodeAsync` computes every ingredient's node `Quantity`
uniformly as `craftsNeeded * ingredient.Count`, correct for a consumable
Item but not obviously correct for a `GuildUpgrade` requirement, which
behaves like a one-time claimed prerequisite rather than a
per-craft-multiplied consumable; invisible today since no UI surfaces a
`GuildUpgrade` node's `Quantity`, but a future real name/requirement
display will need to decide how (or whether) to show that scaled number
rather than assuming it means "N needed per craft" the way it does for a
real item. `Models/CraftingDecision.cs`'s `GuildUpgrade` doc comment,
`CraftingTreeBuilder.BuildNode`'s `"GuildUpgrade"` branch, and the
branch's own `AcquisitionHint` text all point back to this document for
this remainder.

**Review history** (eight adversarial passes; most-recent fix per topic
only - see git log for full per-commit detail):

1. Initial fix: root-caused the CraftingTreeBuilder mislabel and the
   PlanSolver.Evaluate special-case gap; added the `GuildUpgrade` enum
   member/branches; 11 tests.
2. Adversarial follow-up: corrected a false id-overlap justification and a
   wrong mis-costing mechanism (vendor-offer path, not TP-price path);
   closed the GuildUpgrade branch's IconUrl/Rarity leak; 2 tests.
3. Closed the same IconUrl/Rarity leak for the plain Currency branch (a
   real seed collision on id 24); 2 tests.
4. Inverted every guard from an enumerated deny-list to Item-positive
   (class, not instance); fixed a triplicated Gate line; corrected the
   original finding's RESOLVED marker to PARTIALLY RESOLVED; 6 tests.
5. Completed the unrecognized-type leaf's five-field sweep
   (Name/AcquisitionHint/AcquisitionBadge, not just IconUrl/Rarity);
   corrected a false "one Gate: line" claim to "one PENDING Gate: line";
   1 test extended.
6. Fixed a false memo-contract doc comment on `Evaluate`; hoisted the
   unrecognized-type catch-all before the decisions lookup so it holds by
   construction; 1 test.
7. Gave the unrecognized-type leaf its own `UnrecognizedIngredient`
   decision (it was sharing `Unknown`, which meant it got a live,
   clickable IGNORE pill keyed on a non-item id); 3 tests plus 1 rename.
8. Orchestrator fix-loop: fixed a recurring Gate-line duplication; closed
   the fourth Item-positive guard site (`RecipeService.BuildNodeAsync`);
   closed the structurally identical unrecognized-`CostLine.Type` gap in
   `VendorBatchSolver`; 2 tests.

Gate: PARTIAL PASS 2026-08-16 (orchestrator live desktop session). Solver-side safety fully suite-covered; the GuildUpgrade pill/label visuals were unreachable live (no guild-decoration output is plannable via the search list) - visual slice rides the next natural opportunity.
## AUDIT ROW 20/38: TP price-side fallback parity (2026-08-16)

**Bug**: `PlanSolver.GetUnitPrice` returned 0 whenever the selected
price basis's preferred TP side (buy orders / `SellInstant`, or
instant-buy / `BuyInstant`) had no listings for an item, even when that
SAME item's OTHER side had a real, usable price. `GetBuyCost`'s
`unitPrice > 0` check then treated the item as fully unpriceable,
dropping the BuyFromTp option entirely - an item with an empty
preferred side became unpurchasable in the plan, forcing it to Craft
(if a recipe existed) or Unknown. Confirmed against gw2efficiency's own
live bundle (verified twice first-hand): preferred side first, cross-
side fallback to the same item's other side when the preferred side is
missing/zero, unpriced only when BOTH sides are empty.

**Fix**: `PlanSolver.GetUnitPrice` gained a 3-arg overload
`(ItemPrice price, PriceBasis priceBasis, out bool priceSideFellBack)`
that tries the basis-preferred side first and falls back to the item's
other side only when the preferred side is 0; the existing 2-arg
overload now delegates to it (`out _`), so its remaining external
caller (`CraftingPlanPipeline.CollectPresetOverrides`'s Buy-All
feasibility check, which only ever needs the `> 0` priceable check, not
the fell-back fact) gains the fallback automatically with no call-site
change. `GetBuyCost` (the only caller inside `PlanSolver.Evaluate`)
captures the out param into a local `buyPriceSideFellBack`, which
`Commit` folds into a new `Decision.PriceSideFellBack` field gated to
`src == AcquisitionSource.BuyFromTp` (always false for Craft/
BuyFromVendor/UnknownSource commits). `VendorBatchSolver.
EvaluateVendorOffers`'s Item cost-line pricing calls the same 3-arg
overload directly and carries the fact into `VendorItemCostLine.
PriceSideFellBack`. The merged-ceil vendor batching math itself
(`VendorBatchSolver`'s `FinalizeVendorBatches`/`AllocateVendorNodeCosts`)
was not touched anywhere in this history - only the per-item unit price
it already multiplies by can now be a fallback-side number.

**Display**: `CraftingTreeNode.PriceSideFellBack` (read by the
recipe-tree row tooltip in `Views/Rendering/TreeSectionController.cs`)
has three producers, all set by `CraftingTreeBuilder.BuildNode`:

1. A plain `BuyFromTp` node - copied straight from `SolverDecision.
   PriceSideFellBack`. The flag describes THIS node's own TP price.
2. A `BuyFromVendor` cost-component leaf (`IsCostComponent`,
   `BuildVendorCostComponentLeaves`) representing a TP-valued Item
   barter line - copied from that line's own `VendorItemCostLine.
   PriceSideFellBack`. The flag describes THIS leaf's own price, not
   the parent vendor node's.
3. A `BuyFromVendor` node itself - the OR across every one of its
   `VendorItemCosts` lines' own `PriceSideFellBack`, set whenever the
   node's `Source` is `BuyFromVendor` and `VendorItemCosts` is
   non-null, regardless of whether that same offer also produced
   cost-component leaves (a 2+-cost-kind offer gets both a flagged
   parent and a flagged leaf with no double-counting - they are
   separate tree nodes rendering separate tooltip lines). This flag
   describes one of the node's VENDOR COST ITEMS falling back, not the
   node's own item - a `BuyFromVendor` node was never priced on the TP
   at all.

The tooltip renders one of TWO DIFFERENT sentences depending on which
case set the flag, chosen by `_getCurrentPlan()?.PriceBasis` (threaded
through via `CraftingPlanResult.PriceBasis` -> `PlanViewModel.
PriceBasis`, `PlanViewModelBuilder.Build`):

- Cases 1 and 2 (`node.Decision == CraftingDecision.BuyFromTp ||
  node.IsCostComponent`) get "Buy-order price unavailable - instant-buy
  price shown" (or the InstantBuy-basis reverse) - accurate here,
  because the flag genuinely describes this row's own price.
- Case 3 (a plain `BuyFromVendor` node, i.e. not itself a
  cost-component leaf - checked as an explicit "not case 1/2" carve-out
  since a leaf's own `Decision` is always `BuyFromVendor` too) gets a
  distinct sentence naming the component instead of the row: "A vendor
  cost item's buy-order price is unavailable - its instant-buy price is
  used" (or the reverse). Reusing the case-1/2 sentence here would
  assert THIS row's item has no buy orders, which is false in general -
  the row's own item may have a perfectly healthy TP presence, or no TP
  presence at all; only one of its vendor cost items fell back.

Shown regardless of the existing `Quantity > 1` unit-price-line gate,
since the caveat concerns which TP side priced a value rather than
whether a separate per-unit line is useful. The shopping list row
tooltip (`PlanViewModelBuilder.BuildShoppingListSection`) does **not**
carry either caveat - it would need `PriceSideFellBack` threaded through
`PlanStep` and `PlanSolver.Collect`'s per-step-key merge across
possibly-multiple tree occurrences; recorded as an open follow-up, not
implemented, out of scope for this change.

`docs/ARCHITECTURE.md` section 8 ("Solver decision rules") was reworded
to match: the basis is *preferred per item*, with a same-item other-side
fallback when the preferred side has no listings, cross-referenced to
this section by name. A single item is never priced on a mixed basis -
but a TOTAL summed across several items (e.g. a craft cost built from
multiple ingredients) CAN combine sides when a fallback fires on one of
them; the earlier absolute claim that "the solver never compares one
item's buy-order price against a different item's sell-listing price"
was false once totals are considered and has been removed.

**Tests** (all exercise real `PlanSolver.Solve` / `CraftingTreeBuilder.
BuildTree` / `PlanViewModelBuilder.Build` production code paths via each
test file's existing real-solver helpers - no contract-mirror or
fake-logic tests anywhere in this change):

- `PlanSolverPriceBasisAndOverrideTests.cs`:
  `BuyOrderBasis_NoBuyOrders_FallsBackToInstantBuyPrice` (fallback
  chosen, replaces the old bug-asserting
  `BuyOrderBasis_NoBuyOrders_ItemNotPriceable`),
  `BuyOrderBasis_BothSidesEmpty_ItemNotPriceable` (both sides empty
  stays unpriceable), `BuyOrderBasis_UsesBuyOrderPrice`
  (`PriceSideFellBack == false` when no fallback is needed),
  `BuyOrderBasis_VendorItemBarter_BarterItemFallsBackToOtherSide`
  (fallback reaches `VendorBatchSolver`'s per-item pricing),
  `BuyOrderBasis_CraftWinsOverFallbackPricedBuy_DecisionFlagStaysFalse`
  and
  `BuyOrderBasis_VendorWinsOverFallbackPricedBuy_DecisionFlagStaysFalse`
  (the `src == BuyFromTp` gate stays closed when Craft/BuyFromVendor
  wins instead), and
  `BuyOrderBasis_FallbackPricedBuyWinsOverCraft_SourceIsBuyFromTp`
  (pins that a fallback-priced buy must WIN a real three-way comparison
  on cost, not merely be available when nothing else competes).
- `CraftingTreeBuilderTests.cs`:
  `LeafBuyNode_PriceSideFellBack_ReachesCraftingTreeNode` (flag reaches
  the tree node), `CraftNode_WinsOverFallbackPricedBuy_
  PriceSideFellBackStaysFalseOnNode` and `VendorNode_
  WinsOverFallbackPricedBuy_PriceSideFellBackStaysFalseOnNode` (gate
  stays closed on the winning node when Craft/BuyFromVendor beats a
  fallback-priced buy), `MixedOffer_ItemCostPreferredSideEmpty_
  LeafFlagsPriceSideFellBack` and its negative sibling `MixedOffer_
  ItemCostPreferredSidePresent_LeafPriceSideFellBackFalse` (both assert
  the leaf AND the parent, case 2 and case 3 together for a 2-kind
  offer), `SingleKindVendorOffer_ItemOnly_FallsBackToOtherSide_
  ParentFlagsPriceSideFellBack` and its negative sibling
  `SingleKindVendorOffer_ItemOnly_NoLeaves` (case 3, `kindCount==1`,
  no leaves at all), and `MultiOccurrence_MergedVendorOffer_
  ItemFallsBackToOtherSide_ParentFlagsPriceSideFellBack` (case 3 under
  `VendorComponentCostsUnreliable` batch reallocation).
- `PlanViewModelBuilderSummaryTests.cs`:
  `Build_PriceBasisBuyOrder_PassedThroughToViewModel` and
  `Build_PriceBasisInstantBuy_PassedThroughToViewModel` (the
  `PriceBasis` passthrough feeding the tooltip's sentence choice is
  actually asserted, not just assumed).

`Views/Rendering/TreeSectionController.cs` itself remains untested -
Blish-bound UI code, outside this repo's Blish-free test-runnable
surface (same constraint every other UI-adjacent entry in this file
notes). The exact tooltip sentence text - both the original pair and
the case-3 vendor-component pair - is verified by code inspection only,
not by an automated assertion; the trigger CONDITION under which some
line is added (as opposed to its exact wording) is fully covered by the
tree-node-level tests above, since `CraftingTreeNode.PriceSideFellBack`
is the only input the renderer's `if`/`else if` branches read.

**Self-review findings** (consolidated across eight adversarial review
rounds over this change; the findings below are the ones that produced
a real fix - many further confirmations of no-regression were also
recorded across those rounds and are not repeated individually here):
confirmed `PlanStructuralValidator.cs`'s NRE-semantics comment for
`GetUnitPrice`'s unchecked field access stays accurate under the new
overload; confirmed `PlanViewModelBuilder.Build` is the sole production
`PlanViewModel` construction site, so the `PriceBasis` passthrough is
never left at its `InstantBuy` default by a different code path;
confirmed `RenderTreeNode` is the sole recursive tree-row renderer, so
the tooltip logic automatically covers nested/reference-branch rows;
confirmed no existing test fixture silently depended on the old
"empty side = fully unpriceable" behavior; confirmed the widened
case-3 parent flag (OR across `VendorItemCosts`, unconditional on
whether leaves were also built) can never regress cases 1/2 - it is a
pure OR-widening, never removing a previously-reachable `true`; confirmed
`decision.VendorItemCosts != null` is checked before `.Any(...)` in
every case-3 site, so a coin/currency-only `BuyFromVendor` decision
(no item cost lines at all) cannot NRE; confirmed the round-8 tooltip
wording split covers the exact same trigger SET as the single-sentence
version it replaced (case 1 OR case 2 OR case 3, unchanged) - only
which of two sentences fires per case changed, not whether any line
appears at all; confirmed `VendorBatchSolver`'s merged-ceil batching
arithmetic (`FinalizeVendorBatches`/`AllocateVendorNodeCosts`/the
`unitsNeeded` scaling) was never touched anywhere across all eight
rounds - every change here only ever read or carried an
already-computed boolean alongside it; grepped every file this change
touched for non-ASCII bytes and for an em dash character - none found;
grepped every added/edited test file for "Blish"/"BlishHUD"/"Gw2Sharp"
- none found, all test files stay Blish-free and exercise real
production entry points; confirmed exactly one `Gate: [PENDING`
occurrence remains in this file (this section's own, at the true EOF).

Nice-to-have (recorded, not applied - out of scope for this change):
the shopping-list row tooltip still does not carry either caveat (see
Display above); a `SolverDecision`-level precomputed
`VendorItemPriceSideFellBack` field could replace the inline `.Any()`
scan `CraftingTreeBuilder.BuildNode` re-runs per node, but `VendorItemCosts`
lists are always small (single digits of cost lines per real vendor
offer) and adding a new `SolverDecision` field would be an unrequested
new abstraction; a small script wrapping `dotnet build -t:Rebuild` with
an automated before/after warning-count diff over `git diff
--name-only` would make "no new StyleCop warnings" a mechanically
verified claim instead of a manually-run one each round.

- Follow-up (shopping-list caveat asymmetry): the missing-caveat gap
  above is not symmetric between the two bases. Under
  `PriceBasis.InstantBuy`, if an item's `BuyInstant` side has zero
  listings, the same per-item fallback swaps in its `SellInstant` (buy-
  order) price instead - a price no seller is currently offering, only
  what buyers are bidding. The shopping list still renders that item as
  a flat `Buy` row at that coin figure with no caveat, which reads as an
  instantly-fillable price when it is not one - a buy order posted at
  that figure still has to wait for a seller to fill it, and the true
  instant-fill cost (if any seller exists at all) could be higher. The
  reverse direction (`PriceBasis.BuyOrder` falling back to `BuyInstant`)
  does not have this problem the same way: the fallback number there IS
  an instantly-fillable price, just not the preferred one. Recorded as
  an explicit follow-up alongside the general shopping-list gap above,
  not implemented, out of scope for this change.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64 -t:Rebuild`
- clean, 0 errors. StyleCop warning-code histogram for both files this
final round touched (`Views/Rendering/TreeSectionController.cs`,
`Models/CraftingTreeNode.cs`) is byte-for-byte identical before and
after (verified via `git stash`/rebuild/`git stash pop`, comparing
warning-code counts, not just line-by-line text, since every added
comment line shifts subsequent line numbers) - zero new warnings
anywhere in either file. Tests: 1383 passed, 0 failed (`dotnet test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`) - this
final round changed tooltip wording and doc comments only, no new
`[Fact]` added or removed, so the total is unchanged from the prior
round. No Blish HUD/BlishHUD.exe references in any test file; every
test exercises a real production entry point, no contract mirrors. IDs
remain internal-only; coin icons unaffected throughout this entire
history (pricing/tooltip logic only, no coin-rendering code touched).
No live desktop verification was performed at any point in this
history - `TreeSectionController.cs` is Blish-bound and outside this
repo's test-runnable surface, same constraint every UI-adjacent entry
in this file notes.

Gate: PARTIAL PASS 2026-08-16 (orchestrator live desktop session). Fallback pricing exercised implicitly throughout live plans; the caveat tooltip's specific fallen-back shape did not occur in the tested plans - suite-covered, visual slice deferred.

## AUDIT ROW 56: daily craft-cooldown notices + three small fixes (2026-08-16)

### PART A: daily craft-cooldown notices

Server-enforced daily crafting cooldowns (as distinct from the existing
M34-B1 #3 vendor PURCHASE cap notices - see `TimegatedItem`/
`TimegatedCapType`) were entirely unmodeled: a plan telling the user to
craft e.g. 30 Lump of Mithrillium via `AcquisitionSource.Craft` omitted
the ~30 real-world days that recipe's own daily reset actually requires.

Fix, purely additive/informational, no solver or pricing change,
`VendorBatchSolver` untouched:

- `ref/daily_cooldown_items.json` (new, mirrors
  `ref/acquisition_hints_seed.json`'s precedent): 15 wiki-verified
  entries, each with an item id, a per-day cap, and a
  `wiki.guildwars2.com` citation. Curated by fetching each candidate
  item's RAW wikitext (`index.php?title=...&action=raw`) via
  `api.php`/`index.php`, not by trusting the task's own suggested item
  list at face value - that research turned up a real correction: the
  task's suggested set (Deldrimor Steel Ingot, Spiritwood Plank, Elonian
  Leather Square, Bolt of Damask, Xunlai Electrum Ingot) are the
  ascended-refinement STEP-2 outputs, and the wiki confirms those five
  are explicitly NOT recipe-capped ("The step 2 materials are not
  time-gated and can be traded on the Trading Post" -
  `wiki.guildwars2.com/wiki/Crafting_material#Ascended_crafting_materials`).
  The real daily cap lives one tier earlier, on the STEP-1 precursor
  each of those five (four of them; Xunlai Electrum Ingot shares
  Deldrimor's own precursor) consumes: Lump of Mithrillium, Glob of
  Elder Spirit Residue, Spool of Silk Weaving Thread, Spool of Thick
  Elonian Cord - each confirmed via its own item page's raw wikitext
  ("This item can only be acquired once per day per account...",
  `timegate = y`, `[[Category:Time gated recipes]]`). The task's
  "obsidian refinement" example did NOT verify - the wiki's own Obsidian
  Refinement subsection explicitly has no time-gating note ("unlike the
  Ectoplasm Refinement section above it") and Vision Crystal's own
  recipe carries no `timegate` flag - so no obsidian-refinement entry
  was added, per the task's own "do NOT include entries you could not
  verify" instruction. The remaining eleven entries (Heat Stone, Clay
  Pot, Vial of Maize Balm, Gossamer Stuffing, Grow Lamp, Plate of Meaty
  Plant Food, Plate of Piquant Plant Food, plus the four Dragon Hatchling
  Doll parts below) came from the wiki's own
  `Category:Time gated recipes` listing.
  - **Review fix (audit row 56 PART C, finding 1):** the first cut of
    this seed excluded four `Category:Time gated recipes` members - the
    Dragon Hatchling Doll parts (Adornments 79795, Eye 79726, Frame
    79817, Hide 79790) - on the stated grounds that they "carry
    `timegate = y` but no explicit prose sentence on their own pages,"
    while keeping Gossamer Stuffing (79763), a fifth Dragon Hatchling
    Doll component from the same category. Re-checking each item's raw
    wikitext (`index.php?action=raw`) shows Gossamer Stuffing's own page
    has exactly the same evidence shape as the four excluded parts -
    only `| timegate = y` plus `[[Category:Time gated recipes]]`, no
    separate prose sentence either - so the exclusion line was not a
    real distinction, it just never re-checked the one entry it had
    already decided to keep. All four omitted items are real and
    reachable (live API recipes 11885/11878/11888/11889, confirmed
    outputs in `ref/recipes_seed.json`), and a Gift of Aurene plan crafts
    all five Dragon Hatchling Doll parts together - warning on 1 of 5 and
    staying silent on the other 4 read as "the other 4 are
    unconstrained," which is worse than warning on none. Fixed by adding
    all four at `perDayCap 1` (the cap `timegate = y` itself signals, same
    as every other entry in this seed) rather than dropping Gossamer
    Stuffing for consistency, since all five are genuinely capped.
  - **Review fix (audit row 56 PART C, finding 2):** the first cut also
    seeded Charged Quartz Crystal (43772, the task's other named
    example) at `perDayCap 1` - real per the wiki, but dead data in this
    module: `AppendDailyCooldownNotices` only ever inspects
    `AcquisitionSource.Craft` steps, and Charged Quartz Crystal is made
    at a Place of Power, not via any recipe this module resolves - it is
    not a recipe OUTPUT anywhere in `ref/recipes_seed.json` or
    `ref/mystic_forge_recipes.json` (`GET /v2/recipes/search?output=43772`
    also returns `[]`), and it has no `ref/acquisition_hints_seed.json`
    entry either. A plan needing 30 of them surfaces as a
    shopping/unknown leaf with no cooldown warning at all, while the
    seed entry made it look covered. Removed the entry;
    `DailyCooldownItemServiceTests` now pins its absence as a regression
    guard. **General limitation this exposes, not fully fixed here:**
    the notice pass only ever covers items reachable via a Craft step -
    any gated item whose recipe the account has not learned, or that is
    produced by a non-recipe mechanic (Place of Power, achievement
    reward, etc.), resolves to a non-Craft row today and gets no
    cooldown notice regardless of whether it is in this seed. Extending
    the pass to also cover `ShoppingUnknown`/non-craft rows is a real
    follow-up, out of scope for this fix.
  - **`itemName`/`note` fields are maintainer-only documentation.**
    `DailyCooldownItemService.Load` never reads either field (see its
    own `DailyCooldownEntry` shape) and no test pins them - they exist
    purely to make the JSON file human-readable during curation/review
    and can drift from the live API silently if an item is ever renamed.
    Spot-checked against `GET /v2/items` for all entries as of this
    review; not otherwise enforced.
- `Models/DailyCooldownItem.cs` / `Services/DailyCooldownItemService.cs`
  (new): loader, byte-for-byte the same shape/never-throws contract as
  `AcquisitionHintService.Load`.
- `CraftingPlanResult.DailyCooldownItems` / `PlanSolveContext.
  DailyCooldownItems` (new fields) wired through `CraftingPlanPipeline`
  at every site `AcquisitionHints` already flows through (both
  `GenerateStructured*Async` result-builds + their `PlanSolveContext`
  snapshots, plus `ResolveWithOverrides`) - loaded once in `Module.cs`
  with the same try/catch-degrades-to-null seed-load convention as the
  acquisition hints seed immediately above it.
- `PlanViewModelBuilder.AppendDailyCooldownNotices` (new, called from
  `BuildCraftingStepsSection`): an additive pass over the section's
  already-filtered Craft-source steps. A step whose aggregate `Quantity`
  exceeds the seed's `PerDayCap` for that item id gets one
  `PlanRowType.TimegatedNotice` row appended - reusing that row's exact
  plain-`Label`-text shape (the same generic `TextRowRenderer` branch
  the pre-existing vendor-cap notices already render through, see
  `CraftStepsSectionRenderer.Render`), never the `TimegatedItem`
  model/`Plan.TimegatedItems` list itself, so a recipe-level cooldown
  can never be confused with (or accidentally validated by
  `PlanStructuralValidator` as) a vendor purchase cap. Wording: `"{item}
  is timegated - {cap} per day per account - crafting {qty} will take
  about {days} day(s)"`, `days = Ceiling(qty / cap)`.

Tests (new): `DailyCooldownItemServiceTests` (7 cases, mirrors
`AcquisitionHintServiceTests` including a shipped-seed-file pin) and
`PlanViewModelBuilderDailyCooldownTests` (7 cases: exceeds-cap,
at-cap/no-notice, not-in-seed, null-seed-no-throw, non-Craft-step
never triggers, non-divisible-quantity rounds up, and a vendor-cap +
craft-cooldown notice coexisting in one section).

### PART B: three small fixes

1. **Magenta missing-texture icons.** `Views/MainView.cs`'s
   `CreateItemRow`/`CreateWalletRow` (Snapshot tab - the reported case
   was the Spirit Shards wallet row) and `Views/SuggestionPanel.cs`'s
   search-suggestion rows all fell back to `ContentService.Textures.
   Error` - Blish's alarming magenta missing-texture placeholder -
   whenever `IconUrl` was empty, conflating an ordinary data gap with a
   genuine texture-load failure. All three now call the existing
   `Views/Rendering/IconControls.CreateItemIcon` helper (which already
   degrades an empty `iconUrl` to a neutral dark-grey empty-slot square,
   used everywhere else in the crafting-plan tree/rows), removing the
   duplicated inline icon-loading logic entirely rather than patching it
   three times. `Module.cs`'s own `ContentService.Textures.Error`
   fallback (module icon texture failed to *load*, a real failure) is a
   different case and was left untouched.
2. **`Gw2Constants.KnownCurrencyNames` audit.** Verified every existing
   entry against a live `GET /v2/currencies?ids=all&v=2022-03-23` fetch
   (2026-08-16). The six ids the task flagged as pre-ingestion
   mispairs (36, 49, 50, 58, 59, 60) are already correctly paired on the
   current master - the `recipe-ingestion-fix` PR (#113) already fixed
   them. The one real remaining gap: id 68 (Imperial Favor, a Cantha
   vendor currency) was missing from the dict entirely, so any plan
   costing it fell back to the generic "Currency" label via
   `ResolveCurrencyName` - added as `{ 68, "Imperial Favors" }`,
   matching the dict's own established singular-API-name -> pluralized-
   display-name convention (confirmed exceptionless across all ~44
   pre-existing entries before extending it). New test:
   `Gw2ConstantsCurrencyNamesTests` (5 cases) pins 19 ids' exact display
   strings against a real, verbatim-captured `/v2/currencies` snapshot
   (not invented), including an explicit "id 60 is Tyrian Defense Seal,
   not Imperial Favor - real Imperial Favor is id 68" regression guard
   for the exact bug class this audit found.
   - **Not fixed, flagged for a follow-up pass:** `ref/vendor_offers.json`
     references ~19 further currency ids with no `KnownCurrencyNames`
     entry at all (31, 35, 46, 52-54, 57, 64, 66, 69, 70, 72, 73, 75-77,
     81-83 - Legendary Insight and Ancient Coin among them). Each would
     need its own real-vs-mass-noun pluralization judgment call the task
     did not ask for and this pass did not verify community-standard
     wording for; left as a known completeness gap rather than guessed.
3. **Two stale/incorrect gw2efficiency-provenance doc comments.**
   `Views/Rendering/TreeSectionController.cs`'s dimmed-reference-branch
   comment claimed the branch was "gw2e's `.not-crafted` informational
   reference branch" - gw2efficiency has no such concept; it is a module
   original. `Services/AccountCurrencyIndex.cs` claimed gw2efficiency
   "only ever nets owned currency out at the summary layer" - gw2e
   also has a per-node "owned" pill on the tree itself. Both comments
   corrected in place; no behavior change.

### PART C: code-review fixes (post-merge review round)

1. **`ref/daily_cooldown_items.json` coverage/consistency fixes** - see
   the corrections inline in PART A above: added the four Dragon
   Hatchling Doll parts (finding 1), removed the dead Charged Quartz
   Crystal entry (finding 2), documented the Craft-step-only limitation
   this exposes, and noted `itemName`/`note` are maintainer-only
   documentation fields. Seed count: 15 (was 12: +4 Dragon Hatchling
   Doll parts, -1 Charged Quartz Crystal). `DailyCooldownItemServiceTests`
   extended (same `[Fact]` methods, more assertions - no test count
   change) to pin the four new ids at `perDayCap 1` and pin 43772's
   absence as a regression guard.
2. **`Gw2ConstantsCurrencyNamesTests` was a contract-mirror test.** Its
   sole non-trivial assertion compared `Gw2Constants.KnownCurrencyNames`
   against `ExpectedDictName`, a hand-copied duplicate of that same
   production dictionary - `LiveApiNameById` (the "real snapshot" the
   file's doc comment sells) was only ever interpolated into a failure
   message, never asserted against, so the test would have passed
   unchanged even if every `LiveApiNameById` value were wrong. Fixed by
   asserting `ExpectedDictName[id]` against `LiveApiNameById[id]` for
   every pinned id (equal, or the dict's established pluralization of
   it) alongside the existing equality check, so a future entry added
   with a mispaired id now fails instead of sailing through. No
   production code changed; the underlying data was independently
   re-verified against a live fetch and found correct.
3. **Dead singular-day branch removed.** `PlanViewModelBuilder.
   AppendDailyCooldownNotices`'s `day{(days == 1 ? "" : "s")}` was
   unreachable - the loop `continue`s whenever `step.Quantity <=
   cooldown.PerDayCap`, so every emitted notice already has `days =
   Ceiling(qty / cap) >= 2`. Simplified to the always-true plural form;
   no behavior change (existing tests already only assert the plural
   wording).
4. **`Services/AccountCurrencyIndex.cs` doc comment precision.** The
   PART B #3 correction above (gw2e nets owned currency out via a
   per-node pill, not summary-layer-only) is now explicit that the pill
   is *display* only - gw2e's own quantity engine never nets owned
   currency into a decision either (matching this class and
   `docs/research/gw2e-convergence-matrix.md`'s `calculateTreeQuantity.
   ts` finding), so the correction cannot be misread as gw2e netting
   currency into decision math.
5. **`CraftingPlanResultBuilders.MakeResult`'s `dailyCooldownItems`
   parameter moved to the end of the parameter list**, matching
   `CraftingPlanPipeline`'s own constructor convention (which appends it
   after `moduleLog` for exactly this reason) - it previously sat
   between `acquisitionHints` and `timegatedItems`, a positional hazard
   for any future caller not using named arguments. All 119 existing
   `MakeResult(...)` call sites use named arguments, so this is a
   no-op for current callers.
6. **`docs/gw2e-considerations.md` Section 12 / `docs/research/gw2e-
   convergence-matrix.md` row 46 marked resolved**, pointing at the
   PART B #3 fix - both previously still described the
   `TreeSectionController` provenance comment as an open
   recommendation after it had already been corrected.
7. **Dangling `FindRepoFile` comment in `DailyCooldownItemServiceTests.cs`
   moved** to sit with the `using static` it documents, rather than
   floating disconnected at the end of the class body.

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-cooldowns/GW2CraftingHelper.csproj -p:Platform=x64` -
0 errors, warning count/content unchanged from baseline (all new
warnings, if any, are the project's pre-existing StyleCop noise
pattern, not introduced by this change; confirmed no new warning in
any file this round touched). Tests: `"/mnt/c/Program
Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-cooldowns/tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` -
1429 passed, 0 failed (1410 baseline + 14 PART A + 5 PART B; PART C
added assertions to existing `[Fact]`s rather than new ones, so the
total is unchanged). No Blish HUD/BlishHUD.exe reference in any test
file; every new/changed assertion exercises a real production entry
point (`DailyCooldownItemService.Load`, `PlanViewModelBuilder.Build`,
`Gw2Constants.KnownCurrencyNames`/`ResolveCurrencyName` directly), no
contract mirrors, no fake file I/O (the shipped-seed-file test reads
the real `ref/daily_cooldown_items.json` from disk via the existing
`RepoFileLocator` helper). IDs remain internal-only - the new craft-
cooldown notice text never surfaces an item id, only its resolved name.
No live desktop verification was performed - `Views/MainView.cs`,
`Views/SuggestionPanel.cs`, and `Views/Rendering/TreeSectionController.cs`
are all Blish-bound and outside this repo's test-runnable surface; the
icon-placeholder fix in particular (a Snapshot-tab render change) has
not been visually confirmed in a live client.

### PART D: post-PART-C follow-up review fixes

1. **Coverage gap against a named brief target: Charged Quartz Crystal
   (43772).** PART C finding 2 correctly removed 43772 from
   `ref/daily_cooldown_items.json` (dead data - the notice pass only ever
   inspects `AcquisitionSource.Craft` steps, and 43772 is never a recipe
   output). But 43772 is one of the brief's own named motivating examples,
   is `AccountBound`/no-TP/no-vendor-offer, and had no
   `ref/acquisition_hints_seed.json` entry either - so it resolved to a
   `ShoppingUnknown` leaf with zero timegate signal at all. Concrete
   consequence: a plan for one Grow Lamp (66993, seeded) needs 10x Charged
   Quartz Crystal and emitted no notice anywhere. Fixed with the minimal,
   fully-additive remedy: added a `ref/acquisition_hints_seed.json` entry
   for 43772 (hint text names the Place of Power source, the 1-per-day
   cap, and account-bound/no-TP status; badge `DAILY`), reusing the
   existing `ShoppingUnknown` hint/badge path
   (`PlanViewModelBuilder.ResolveHintText`/`ResolveBadgeText`) with no new
   code. `AcquisitionHintServiceTests.Load_ShippedSeedFile_*` updated
   (6 -> 7 entries) to pin the new entry. The general limitation PART C
   already documented (the notice pass only ever covers `Craft`-source
   steps) still stands and is unchanged by this fix.
2. **Nice-to-haves taken alongside (all cheap, same-file as their own
   finding):**
   - `Models/DailyCooldownItem.cs`: `PerDayCap`'s doc comment now states
     it is output-UNITS per day (matching how
     `AppendDailyCooldownNotices` actually compares it against
     `PlanStep.Quantity`) and flags that every seeded recipe today has
     `output_item_count == 1`, so a future multi-output entry would need
     the comparison divided by the recipe's own output count, not
     `PerDayCap` reinterpreted. No behavior change (still latent, no
     seeded recipe triggers it).
   - `Services/PlanViewModelBuilder.cs`: craft-cooldown notice wording
     appends "(runs in parallel with other daily-gated items)" - each row
     was already individually accurate, but nothing said multiple rows'
     day-estimates are independent maxima, not a sum (the flagship
     Gift of Aurene / multi-Dragon-Hatchling-Doll-component case).
     `PlanViewModelBuilderDailyCooldownTests`' existing substring
     assertions (`"30 days"`, `"3 days"`) still pass unchanged.
   - `tests/.../Services/CraftingPlanPipelineTests.cs`: new
     `DailyCooldownItems_SurvivesGenerateStructuredAsync_
     AndResolveWithOverridesRoundTrip` pins the seed dictionary through a
     `GenerateStructuredAsync` -> `ResolveWithOverrides` round trip
     (mirrors the file's own `ResolveWithOverrides_
     CarriesCharacterDisciplinesForward` shape) - closes the previously
     untested 5-site hand-copied wiring in `CraftingPlanPipeline.cs`.
   - `docs/gw2e-considerations.md` Section 11 / `docs/research/
     gw2e-convergence-matrix.md` row 42 marked **Resolved**, matching the
     sibling Section 12 / row 46 resolution PART C already recorded for
     the same PART B #3 fix - both had been left describing the
     `AccountCurrencyIndex.cs` comment fix as still-open.
   - `Services/AccountCurrencyIndex.cs`: the PART B #3 comment correction
     is refined - it previously asserted gw2efficiency nets owned
     currency out "at BOTH the Shopping List/summary display layer AND
     via a per-node pill," but only the per-node pill is measured
     evidence (a live `componentTree.html` fetch); the summary-layer half
     was the original unverified M34-era claim carried forward unchanged.
     Now reads "at least via a per-node display pill," with an explicit
     note that the summary-layer half is unconfirmed. No behavior change.
   - `tests/.../Models/Gw2ConstantsCurrencyNamesTests.cs`: the bare
     `LiveApiNameById[id]` indexer inside the `foreach` is now preceded by
     an `Assert.True(...ContainsKey(id)...)` check with a legible failure
     message, so a future id added to `ExpectedDictName` without a
     matching `LiveApiNameById` entry fails cleanly instead of throwing
     an undiagnostic `KeyNotFoundException`.
   - `Services/DailyCooldownItemService.cs`: `Load` now also skips an
     entry with `ItemId <= 0` (previously only `PerDayCap` was
     validated) - no `PlanStep` ever carries one, matching the existing
     malformed-seed-data guard shape. New test
     `Load_ZeroOrNegativeItemId_EntrySkipped_NoThrow`.
   - Not taken: the split-source under-reporting and parallel-vs-additive
     *aggregation* (as opposed to wording) nice-to-haves remain latent
     only (no seeded item currently has a vendor offer, per
     `ref/vendor_offers.json`) and would need solver-adjacent design
     work, out of scope for a same-file cheap fix.

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-cooldowns/GW2CraftingHelper.csproj -p:Platform=x64` -
0 errors, warning count/content unchanged from baseline (all warnings
are the project's pre-existing StyleCop noise pattern). Tests:
`"/mnt/c/Program Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-cooldowns/tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` -
1431 passed, 0 failed (1429 baseline + 2 new: `Load_
ZeroOrNegativeItemId_EntrySkipped_NoThrow` and `DailyCooldownItems_
SurvivesGenerateStructuredAsync_AndResolveWithOverridesRoundTrip`). No
Blish HUD/BlishHUD.exe reference in any test file; every new/changed
assertion exercises a real production entry point
(`AcquisitionHintService.Load`, `DailyCooldownItemService.Load`,
`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`,
`Gw2ConstantsCurrencyNamesTests`), no contract mirrors, no fake file
I/O. IDs remain internal-only - the new/changed hint text never
surfaces an item id. No live desktop verification was performed (same
Blish-bound surface as PART A/B/C).

Gate: PASS 2026-08-16 (orchestrator live desktop session). Deldrimor Steel Ingot x5 rendered the timegate notice verbatim ('Lump of Mithrillium is timegated - 1 per day per account - crafting 5 will take about 5 days'); the empty-IconUrl magenta fix verified on the Snapshot tab (Spirit Shards row degrades to no icon); currency-name corrections suite-covered.
## Currency UX package: defaults, plan-scope pills, value-detail hover (2026-08-16)

Three maintainer-approved currency-UX features shipped as one coherent
package on branch `currency-ux-package`, in three feature-sized commits.

**Feature 1 - default currency decision values.** Ships gw2efficiency's
`CURRENCY_DECISION_PRICES` table (MIT-licensed, attributed - see
`Models/CurrencyDecisionDefaults.cs`'s header comment and
`docs/research/gw2e-currency-decision-prices.md`'s full provenance writeup
from the extraction run) as curated default decision-only currency
valuations - a maintainer-ratified, one-time waiver of the "no invented
data" rule for this specific table only. `CurrencyValuation` gained a
three-state precedence (user-set value, else explicit clear, else
curated default, else none) via `TryGetEffectiveCopperValue` and a
persisted `ClearedCurrencyIds` set, so a user can suppress a default and
have it stay suppressed rather than resurrecting on next load.
`CurrencyValuationSerializer`'s persisted JSON shape grew a `"Cleared"`
array alongside `"Values"`, with full backward compatibility for the old
flat-dict format (verified via round-trip tests reading the pre-existing
shape).

Design decision, found only after the first implementation attempt broke
8 pre-existing tests: `PlanSolver.Solve`'s top-level entry already
coalesces a `null` `currencyValuation` argument to `CurrencyValuation.None`
internally (line ~204) before threading it through `Evaluate`/
`EvaluateVendorOffers` - so wiring `TryGetEffectiveCopperValue` directly
into the solver's currency-lookup call sites would have applied curated
defaults to *every* solver/pipeline test in the suite, not just real
settings-driven callers, since `CurrencyValuation.None` is the exact same
static singleton both a bare unit test and an unconfigured real user's
settings produce. Reverted that approach; defaults are instead applied at
exactly one seam - the new `ModuleSettings.GetEffectiveCurrencyValuation`,
whose sole caller is `Module.cs`'s Generate button handler - so every
existing solver/pipeline test that constructs its own `CurrencyValuation`
(or omits one, or reads `ModuleSettings.GetCurrencyValuation`'s raw form)
is completely unaffected. This is why Feature 1's own commit left the
1410-test baseline byte-for-byte unmodified (+25 new tests only, no
existing test needed a currency-id swap to avoid an unintended default) -
scoped to Feature 1 alone; see the correction below for the package as a
whole, since Feature 2 did intentionally rewrite one pre-existing test.

**Feature 2 - plan-scope currency pills (maintainer's own design).**
Every currency leaf row in the Recipe Tree (ordinary `Currency`-decision
nodes and W4B vendor cost-component currency leaves alike) now gets a
`HAVE {have}/{planTotal} TOTAL` pill alongside its existing `CURRENCY`
tag - `have` = whole-plan wallet holding, `planTotal` = the whole plan's
need for that currency (`plan.CurrencyCosts`), both plan-scope facts
(deliberately no per-row allocation), so the identical pill text is
truthful at every tree occurrence of the same currency id. Full coverage
collapses to the plain blue `HAVE` pill (matching item-pill vocabulary);
the pill is omitted entirely (not shown as `HAVE 0`) when no wallet
snapshot is available. A currency-type cost-component leaf's row-scope
`OWN n` badge is replaced by this same plan-scope pill; an item-type
cost-component leaf's `OWN n` badge is unchanged, out of this feature's
scope. `PlanViewModel` gained `CurrencyPlanTotals`/`OwnedCurrencyAmounts`
passthroughs; `DecisionPillPlanner.BuildPillSpecs` gained two optional
parameters (default `null`, so every pre-existing call site/test is
unaffected).

**Feature 3 - value-detail hover on CRAFT/VENDOR pills.** Duplicated
verbatim from gw2efficiency's own crafting-pill tooltip template: when a
node's decision-only comparison figure diverges from its displayed real
gold cost, its committed CRAFT/VENDOR pill gains a hover block
("Crafting gold price" / "Currencies" delta with its opportunity-cost
caption / "Optimization price", plus a "Vendor cap: N per day/week" line
when the winning vendor offer carries one - maintainer-ratified #21
resolution). Plumbing: `SolverDecision`/`CraftingTreeNode` each gained a
`ComparisonValue`/`DecisionValue` passthrough of an internal figure
`PlanSolver.Evaluate` already computed and discarded (`Decision.
ComparisonValue`, private) - no new solver math, purely a public-surface
passthrough of an already-correct, already-recursive rollup. Both new
fields are explicitly documented DECISION-ONLY at every hop. The actual
tooltip text/divergence decision lives in the new Blish-free
`ValueDetailTooltipBuilder`, matching this repo's established pattern of
keeping tree-rendering logic unit-testable outside `TreeSectionController`.
Audited (not assumed) that "currency names available wherever an icon/
amount appears" was already satisfied by prior work (field-test finding
B's tooltip sweep, `IconControls.CreateItemIcon`'s `tooltipText`
parameter) - no changes needed for that sub-requirement.

**Cross-feature interaction checked:** a currency-type cost-component
leaf's `Locked` (`CURRENCY`) pill has `node.Decision == BuyFromVendor`,
which matches Feature 3's pill-kind/decision gate - but that leaf's
`DecisionValue` is never set (`CraftingTreeBuilder.BuildVendorCostComponentLeaves`
constructs those nodes directly, bypassing the `BuildNode` path that
sets it), so `ValueDetailTooltipBuilder.TryBuild`'s null guard silently
declines rather than attaching an incorrect tooltip - verified by
tracing the construction path, not merely asserted.

Repo invariants: ASCII-only .cs (checked via `perl -ne 'print if
/[^\x00-\x7F]/'` over every new/edited file across all three features -
zero hits); no em dashes (same sweep); Allman braces; new `.cs` files
registered in `GW2CraftingHelper.csproj`'s explicit `<Compile Include>`
list (`CurrencyDecisionDefaults.cs`, `ValueDetailTooltipBuilder.cs` -
the test project is SDK-style/globbed, so its two new test files needed
no registration); no Blish HUD/BlishHUD.exe/Gw2Sharp references in any
test file (grepped); tests exercise real production paths, no contract
mirrors; IDs remain internal-only (pill/tooltip text shows names/counts,
never raw currency ids); coin icons unaffected (no coin-rendering code
touched); currency valuations stay decision-only and are restated as
such in a comment at every place a decision-only figure surfaces near
display code (`CurrencyDecisionDefaults`, `CurrencyValuation`,
`ModuleSettings`, `Module.cs`, `SolverDecision`, `CraftingTreeNode`,
`ValueDetailTooltipBuilder`); `Services/ModuleLog.cs`,
`PlanContentHeightMath`, `PlanRelayoutMath`, scroll machinery, and
`VendorBatchSolver`'s merged-ceil batching math were never touched -
`VendorBatchSolver.cs` itself has zero diff across all three commits
(Feature 3's vendor-cap line only ever *reads* `CraftingPlan.
TimegatedItems`, an output that solver already computes).

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - clean,
0 errors, no new warnings in any new/edited file. Tests: 1410 (baseline)
-> 1435 (Feature 1, +25) -> 1446 (Feature 2, +11) -> 1470 (Feature 3,
+24), all green at every step (`dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj`). No live desktop verification was
performed - `Views/SettingsTabContent.cs` and
`Views/Rendering/TreeSectionController.cs` are Blish-bound and outside
this repo's test-runnable surface, same constraint every UI-adjacent
entry in this file notes; the Settings tab's new default/Clear-checkbox
layout in particular (new pixel columns past the existing `ErrorX`) has
not been visually confirmed to fit within a real Settings window at
typical widths.

**Correction (currency-ux-package review fix, finding 6, MEASURED):** two
claims above are false as written for the package as a whole. (1) The
"byte-for-byte unmodified" wording above is scoped to Feature 1's own
commit only and is accurate there, but Feature 2's commit (`c7bac28`)
intentionally rewrote one pre-existing test - `DecisionPillPlannerTests.cs`
gained a `+172/-3` diff, and
`CostComponent_CurrencyType_WithOwnership_ShowsBothBadgesTogether_CurrencyFirst`
was renamed to `..._WithRowScopeOwnershipOnly_ShowsOnlyCurrencyBadge` with
its assertions rewritten, because Feature 2 replaces the currency
component's own row-scope `OWN n` badge with the new plan-scope pill (see
Feature 2's own paragraph above) - a behavior change absorbed into an
existing test, not preserved unmodified. (2) The final test count stated
above (1470) is stale: the review-fix commit `a49ba19` added tests
without updating this entry (bringing the count to 1478), and a further
review-fix round (finding 5, adding real-production-path coverage for the
merged-vendor valued-currency ComparisonValue shape) brings the measured
count on this branch to 1479. Both corrections per this repo's measured/
inferred/guess discipline - an entry that overstates baseline preservation
is worse than none.

**Nice-to-have note (currency-ux-package review fix):** a persisted plan
snapshot's `PlanSolveContext.CurrencyValuation` stores the POST-`WithDefaults`
materialized valuation (curated defaults baked in at solve time, not
re-resolved on load - round-trip verified working). A user who later
clears a currency default in Settings will still see the OLD default
applied when that already-saved plan is re-solved locally, until the plan
is regenerated fresh. This is consistent with how a user-set override
value already behaves in a saved snapshot (both are frozen at solve time),
so it is arguably correct snapshot semantics rather than a bug - noted
here so it is not rediscovered as one.

Gate: PASS with one deferred slice 2026-08-16 (orchestrator live desktop session). Plan-scope currency pills verified live in collapsed full-coverage form (plain HAVE on all covered leaves incl. W4B component leaves - the unified vocabulary); Settings defaults suite-covered; value-detail hover: all three suppression paths verified live correct (unvalued ARE, component leaves, non-diverging rows) but no positive render occurred in the tested shapes - a propagation question (vendor-child currency valuations folding into a craft root's DecisionValue) is under investigation on branch gate-fixes.
## Decision-invariant "Value Own Materials" (VOM, 2026-08-16)

**Bug (audit row 31)**: `InventoryReducer.Reduce` walked the recipe
tree and consumed owned inventory stock **before** `PlanSolver.Solve`
ever decided which nodes would actually be crafted, bought from the
TP, or bought from a vendor - reduction had no idea what the solver
would choose, so it used a price-blind heuristic (only a node's
first-listed recipe option, `node.Recipes[0]`, consumed the pool, and
every visited node was assumed craftable-and-chosen). Two concrete,
confirmed failure modes resulted:

1. **Phantom `UsedMaterials` / understated `CraftingProfit`.** A node
   force-flagged to Buy by the (already-shipped)
   `OwnedMaterialsForceBuyPrePass` 15% force-buy guard still had
   reduction walk its primary recipe and phantom-consume owned stock
   for ingredients that were never actually needed, since the node was
   never crafted - inflating `UsedMaterials` and deducting a phantom
   `MaterialOpportunityCost` from `CraftingProfit` for a branch that
   does not exist in the real plan.
2. **Recipe-option bias.** Only `Recipes[0]` (the option the upstream
   recipe source happens to list first) ever got discounted by owned
   stock, regardless of which option `PlanSolver.Evaluate` would
   actually pick as cheapest at market prices - so owning the FIRST
   option's ingredients could make it look artificially cheaper than
   an objectively cheaper alternative, flipping the solver's own
   choice toward a worse-at-market-prices recipe.

Confirmed via a re-baseline audit of the full existing test suite (see
Fix below): both failure modes were live and untested before this
change - two Valued-mode fixtures' expected numbers changed once the
fix landed, and both were traced to exactly these two bugs, not a
regression (see Tests below).

**Fix (Candidate A - zero-owned decision pass, chosen over two other
designs considered and rejected: a unified solve+reduce rewrite of
`PlanSolver.Evaluate`, and a post-solve `UsedMaterials` reconciliation
filter that only patches the display symptom without fixing the
underlying recipe-option-bias decision bug)**:
`InventoryReducer.Reduce` (both the flat-`Dictionary<int,int>` overload
and the `AccountItemIndex`-sourced production overload) gained an
optional `zeroOwnedDecisions` parameter - the `Decisions` dictionary
from one more throwaway `PlanSolver.Solve` call, on the SAME
zero-owned/unreduced tree `OwnedMaterialsForceBuyPrePass` already
solves for its own force-buy flag, this time with `forceBuyOnlyNodeIds`
applied. `ReduceNode`/`ReduceNodeSourced` now consume the owned-stock
pool down a recipe option only when that node's zero-owned decision was
`Craft` AND `option.RecipeId` matches the chosen option; a node decided
`BuyFromTp`/`BuyFromVendor`/`UnknownSource` lets NO option consume the
pool for its descendants. A `null` guide (every pre-existing caller and
test, and every Free-mode generation) reproduces today's exact
`i == 0`-primary-option heuristic byte-for-byte; a guide missing a
specific NodeId falls back to the same heuristic defensively.
`CraftingPlanPipeline` (both `GenerateStructuredAsync` and
`GenerateStructuredMultiAsync`) moved the existing force-buy pre-pass
ahead of the `Reduce` call (it now must run first, to produce the
guide `Reduce` consumes) and added the new zero-owned decision solve
right after it - both only run when `useForceBuyPrePass` is true (Valued
mode + a live snapshot + a reducer), the same pre-existing gate. Since
discounting only ever lowers a cost, and only along the path the
zero-owned pass already declared the winner, owned stock can never pull
the real (post-reduction) solve toward a chain that is worse at market
prices - it can only make the zero-owned winner an even stronger
winner.

**UI relocation**: `ModuleSettings.ValueOwnMaterials` (a global
Settings-tab checkbox, default true, that only ever drove the 15%
force-buy guard + `MaterialOpportunityCost` display, never reduction
itself) is extended and relocated rather than replaced - `Valued` now
covers both the pre-existing guard and the new decision-invariant
reduction under the same flag, avoiding a second, confusingly-named
toggle. The live control moved inline into
`Views/CraftingPlanView.cs`'s controls panel as a new
`_valueOwnMaterials` per-plan session checkbox (default true), next to
the existing `Use Own Materials`/price-basis controls - session state
like its two neighbors, never read from/written to `ModuleSettings`.
`.Enabled` stays synced to `Use Own Materials` (disabled, not hidden,
when Use Own Materials is off; the last-chosen value is preserved, not
reset). `ModuleSettings.ValueOwnMaterials` itself stays defined
(unused on the live path) purely so an already-persisted `settings.json`
value needs no special handling, mirroring the `ScrollDiagnosticsEnabled`
precedent; the Settings tab now shows an info line instead of a live
checkbox. **Inspection finding, not part of the original design
premise**: the design doc that authored this milestone's plan claimed
`UseOwnMaterials`/`PriceBasis` already had a precedent of restoring
their checkbox's DISPLAYED state across a module restart - inspecting
`Views/CraftingPlanView.cs::ApplyRestoredPlan` during implementation
showed this is not actually true (only `Module.cs`'s own on-disk
round-trip exists; the restored value was not fed back into the live
checkbox).

**Correction (post-review)**: the premise being false was found during
implementation, but `PersistedPlan.ValueOwnMaterials` was then shipped
matching that same non-restoring behavior (round-trips to disk, never
reaches the live checkbox) instead of being wired up - meaning the
field, and the schema bump that came with it, earned nothing: every
user's persisted plan would be discarded on upgrade for a value that
still would not have been restored to the control that mattered.
**Fixed**: `ApplyRestoredPlan` now takes a `valueOwnMaterials`
parameter and sets both `_valueOwnMaterials` and (when the tab has
already been built) `_valueOwnMaterialsCheckbox.Checked` from it -
`Module.cs`'s restore call site threads `_pendingPlanRestore.
ValueOwnMaterials` through. `UseOwnMaterials`/`PriceBasis` keep their
pre-existing (out of scope for this fix) non-restoring behavior - only
the NEW field this milestone added was in scope.

**Schema bump**: `PersistedPlan.ValueOwnMaterials` (new field) bumped
`PersistedPlan.CurrentSchemaVersion` from 1 to 2 - the first real
exercise of this reject-and-regenerate mechanism since it was
introduced. Effect: on first load after this ships, a `SchemaVersion`-1
persisted plan is rejected outright by
`PlanStoreHelpers.DeserializePersistedPlan` (one Warn log line), and
Module falls back to its existing "no restored plan" path (empty
Crafting Plan tab) - a known, already-exercised, safe degrade, not a
crash. **One-time cost**: every user's currently-persisted plan is
discarded on first load after this milestone ships. Now justified by
the fix above (the field is actually restored to the live control), not
by a field nobody reads.

**Re-baseline audit** (full suite run, not assumed-green - exactly two
Valued-mode fixtures' numbers changed, both traced to the audited bug
fix below, not a regression):

- `CraftingPlanPipelineTests.ResolveWithOverrides_ForceBuyPrePass_ManualOverrideStillWins`:
  **correction (post-review)** - an earlier draft of this entry
  re-baselined this test's expected `TotalCoinCost` from 30 to 150 and
  described the new number as "a known, accepted limitation of the
  override-replays-against-a-fixed-tree architecture... not a new
  regression." That framing was WRONG: 30 was the correct, real-world
  number (master already returned it), and 150 was a genuine regression
  this same milestone introduced, not a pre-existing limitation - the
  fixture's root item is force-buy-flagged (zero-owned decision =
  `BuyFromTp`), so the guided reduction correctly never discounts its
  owned ingredient stock down the never-chosen craft branch at
  GENERATION time; but `ResolveWithOverrides` used to replay a manual
  override to Craft against that same frozen, never-discounted tree,
  showing the user a plan to buy 5x item 2 for 150 coin when they
  actually own 4 of the 5 needed and would really spend 30. **Fixed**:
  `PlanSolveContext` now also snapshots the GENERATION-time unreduced
  tree (`UnreducedTree`) and the raw account items/character
  (`AccountItems`/`ActiveCharacterName`) whenever the force-buy pre-pass
  ran. `ResolveWithOverrides` uses them to re-run the SAME zero-owned-
  decision-pass-then-`Reduce` dance `GenerateStructuredAsync` uses at
  generation time, but with `overrides`/`ignoredItemIds` folded into the
  decision pass, so a node an override flips to Craft gets its
  ingredients correctly re-discounted against the user's real owned
  stock. `TotalCoinCost` for this test is back to 30, matching master.
  Falls back to the old frozen-`context.Tree` behavior verbatim
  whenever the pre-pass did not run at generation time (Free mode, or
  no snapshot) - no change to that path. **Cost note**: `PlanSolveContext`
  (persisted to disk verbatim as part of `PersistedPlan.Result.
  SolveContext` - see `PlanStoreHelpers`) now also carries
  `UnreducedTree` whenever the pre-pass ran, roughly doubling the
  tree-shaped portion of a Valued-mode-with-snapshot plan's persisted
  JSON (Metadata/Prices/VendorOffers are unaffected - same reference,
  not duplicated). It also carries `AccountItems` - the raw owned-item
  list `ResolveWithOverrides` rebuilds its `AccountItemIndex` from -
  which the original entry above omitted entirely; for a real account
  this list is plausibly thousands of entries, dwarfing the tree-shaped
  cost. **Post-review fix**: `AccountItems` is now projected down to
  the three fields `AccountItemIndex`'s constructor actually reads
  (`ItemId`/`Count`/`Source`) before being captured -
  `SnapshotItemEntry.Name`/`IconUrl` (a full render-service URL) were
  dead weight nobody downstream of `PlanSolveContext` reads. Still
  O(account item count)
  bytes, not O(tree size); not measured against a real large account,
  and gzip in `PlanStore.Save` mitigates on-disk size but not
  serialize/deserialize CPU. Accepted as the cost of correctness on
  this path, not further optimized.
- `MultiItemPlanTests.GenerateStructuredAsync_MultiItem_ValuedMode_MixedBuyCraftBatch_MaterialOpportunityCostNullForBoughtRootOwnedIngredient`
  (**post-review rename** - the original name,
  `..._MaterialOpportunityCostIsWholeTreeSum`, no longer matched what the
  test asserts once the fix below landed):
  `MaterialOpportunityCost` changed from a non-zero phantom credit to
  `null` throughout (standalone and batch) - the bought root's owned
  craft ingredient is no longer phantom-consumed, directly closing the
  audited row-31 bug. `SellSideEconomics.cs`'s own doc comments (which
  had explicitly documented the old phantom-credit behavior as
  "intentional, not a new gap") were updated to describe the fixed
  behavior.

**New tests** (all exercise real production code paths - `InventoryReducer.Reduce`
directly, or the full `CraftingPlanPipeline`/`PlanStore` pipeline - no
contract-mirror or fake-logic tests):

- `InventoryReducerTests.cs` (7 new): decision-guided non-primary-option
  consumption (converse of the pre-existing
  `MultipleRecipeOptions_OnlyPrimaryOptionConsumesPool`), a Buy-decided
  node's ingredients never consumed (no phantom `UsedMaterials`), a
  Buy-decided node's OWN owned stock still credited (its ingredient's
  Quantity still rescales to the new demand unconditionally - only pool
  CONSUMPTION is guide-gated), a missing-NodeId defensive fallback to
  the legacy heuristic, and `Sourced_` mirrors of the first three
  against the `AccountItemIndex` production overload. Full pre-existing
  suite (39+ facts, all called via the un-guided overloads) stays green
  unchanged, confirming `zeroOwnedDecisions: null` is byte-identical to
  today.
- `CraftingPlanPipelineTests.cs` (2 new):
  `Structured_ValuedMode_ForceBuyPrePass_NoPhantomUsedMaterialsOrOpportunityCost`
  (direct proof of the audited bug fix - `UsedMaterials`/
  `MaterialOpportunityCost` no longer phantom-populated) and
  `Structured_ValuedMode_CompetingRecipeOptions_DecisionInvariant_OwnedStockNeverFlipsChoice`
  (two recipe options where the non-primary option is objectively
  cheaper at zero-owned market prices; fully owns the primary option's
  ingredient - proves the winning choice does not flip toward the
  listed-first option). Two design test-plan bullets are satisfied by
  already-existing/already-updated tests rather than new duplicates:
  the manual-override-still-wins case by (following the correction
  above) `ResolveWithOverrides_ForceBuyPrePass_ManualOverrideStillWins`,
  and the Free-mode regression pin by the pre-existing
  `Structured_FreeMode_SameOwnershipScenario_CraftsFromReducedRemainder`
  (stayed green unchanged, since Free mode never builds a guide).
- `PlanStoreTests.cs` (1 new):
  `LoadLatest_VomSchemaVersion1File_ReturnsNullAndLogsWarn` pins that a
  realistic `SchemaVersion`-1 file (the actual previous
  `CurrentSchemaVersion`, not a synthetic 0) is rejected under the new
  `CurrentSchemaVersion` 2. `Save_Load_RequestAndTimestampRoundTrip`
  extended to assert `ValueOwnMaterials` round-trips independently of
  `UseOwnMaterials`.

**Perf spot-check**: measured via a temporary, non-committed CLI flag
added to `tools/GW2CraftingHelper.Harness` (reverted immediately after
measurement - `git status` confirmed clean before this milestone's
final commit), comparing `--profile 2` (Exordium - a real legendary
precursor tree, offline seed data) in Free mode against Valued mode
with an empty-but-non-null account snapshot (enough to exercise
`useForceBuyPrePass`'s new solve without needing real ownership data),
200 iterations each, warm-median timing:

- Free mode (1 `Solve()` call): Total 10ms, `Solve` line 6ms.
- Valued mode + snapshot (3 `Solve()` calls - the pre-existing
  force-buy-diagnostics solve, the new zero-owned-decision solve, and
  the real post-reduction solve): Total 21ms, `Solve` line (the final,
  named solve only) 5ms - the two untimed extra passes account for the
  remaining ~11ms gap (roughly 5-6ms each, in line with the named
  `Solve` line's own per-call cost).

Net: this design's own new solve pass (one of the three, since the
force-buy-diagnostics solve already existed pre-milestone) adds
roughly one solve-worth of time (~5-6ms) to a real, moderately deep
precursor tree - consistent with the design doc's own risk assessment
("acceptable given documented real tree depths... a dozen levels").
No cross-call memoization exists in `PlanSolver` today (a fresh
`Dictionary` every call), so this cost is linear in tree size and would
scale accordingly on a substantially larger tree than tested here.

**Perf spot-check #2 (post-review, VOM finding #3)**: the spot-check
above measures only the GENERATION path (async, off the UI thread). The
more latency-sensitive path is `ResolveWithOverrides`, reached
synchronously on the MAIN thread by every override pill click (see
`Module.cs`'s own doc comment on that wiring). Measured the same way
(temporary, non-committed `--profile-resolve` flag added to
`tools/GW2CraftingHelper.Harness`, reverted immediately after
measurement - `git status` confirmed clean before this milestone's
final commit), 200 iterations, same Exordium tree, Valued mode +
snapshot (so every click re-runs the guideSolve + re-reduction path -
see `PlanSolveContext.UnreducedTree`'s doc comment):

- `ResolveWithOverrides` median: ~15-16ms per click (empty-but-non-item
  snapshot and a 5000-synthetic-item snapshot measured the same,
  post-cache - see below). Roughly 2-3x a single generation-path
  `Solve()` call's own ~5-6ms, consistent with the design doing a
  guideSolve + `_reducer.Reduce` + the real `Solve()` per click, on top
  of the pre-existing force-buy-diagnostics solve already inside
  `GenerateStructuredAsync` (not repeated per click).
- `AccountItemIndex`'s own constructor, isolated: ~2.05ms per build for
  5000 synthetic entries (`Bank`-sourced, one call per iteration) - a
  real account's item list is plausibly this size (see finding #2's
  cost note above). **Fix applied**: `CraftingPlanPipeline` now caches
  the built `AccountItemIndex` keyed by reference equality on the
  `PlanSolveContext` (see `GetOrBuildAccountItemIndex`'s doc comment) -
  a restored/generated context's `AccountItems` list never changes
  underneath it, so every click after the first against the same
  context skips this ~2ms rebuild entirely, rather than paying it on
  every single pill click. Not measured as a percentage of total click
  latency across a range of account sizes; the isolated 2.05ms figure
  above is the concrete number the cache removes from the repeat-click
  path.

**Known residual (post-review, not guarded/tested)**: the decision-
invariance guarantee above is narrower than earlier drafts of this
entry (and `InventoryReducer`'s own doc comments) claimed. The guide
is computed on the UNREDUCED tree, but a node's OWN Quantity can still
shrink from owned stock of that node's own item id (unrelated to the
guide), and craft cost is non-linear in quantity
(`ComputeCraftsNeeded`'s ceiling division, `VendorBatchSolver`'s
per-batch math) - so shrinking a node's own Quantity can raise its
effective per-unit cost enough to flip the REAL (post-reduction) solve's
decision for THAT node away from what the guide assumed, after its
ingredients were already discounted and written into `UsedMaterials`
against the guide's Craft assumption. This is the audited row-31
phantom-`UsedMaterials` bug re-entering through a second door. Requires
a node with owned stock of ITSELF plus owned stock of its own
ingredients, and a recipe/vendor batch whose output count is greater
than 1 - not exercised by any existing fixture. See
`InventoryReducer.ReduceNode`'s doc comment for the precise mechanism.
Left undone (not treated as blocking this milestone) rather than
attempting a fix: closing it properly needs the same
"solve-then-detect-a-flip-then-re-reduce" shape as the `ResolveWithOverrides`
fix above, applied to `GenerateStructuredAsync`/`GenerateStructuredMultiAsync`
themselves - a real design change, not a small guard.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - clean,
0 errors (StyleCop warning count unchanged from before this milestone -
every edited file already carried pre-existing warnings of the same
codes, no new ones introduced by this change's own lines). Tests: 1425
passed, 0 failed (`dotnet test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`)
(**post-review correction**: this entry originally claimed 1420
passed / 10 new, written before commit 582a44a and never refreshed -
measured on HEAD it is 1410 baseline + 15 new: 10 `InventoryReducerTests`
[5 flat + 5 `Sourced_`, including the two StaleRecipeIdInGuide pins] + 3
`CraftingPlanPipelineTests` [including
`Structured_FreeMode_CompetingRecipeOptions_PrimaryOptionOwnedStockFlipsChoice`]
+ 1 `MultiItemPlanTests` + 1 `PlanStoreTests` = 1425). No Blish HUD/
BlishHUD.exe references in any test file; every test exercises a real
production entry point (`InventoryReducer.Reduce`,
`CraftingPlanPipeline.GenerateStructuredAsync`/
`GenerateStructuredMultiAsync`, `PlanStore.LoadLatest`), no contract
mirrors, no fake file I/O. IDs remain internal-only throughout; coin
icons unaffected (pricing/reduction logic only, no coin-rendering code
touched). No live desktop verification was performed -
`Views/CraftingPlanView.cs`'s new inline checkbox is Blish-bound and
outside this repo's test-runnable surface, same constraint every
UI-adjacent entry in this file notes; the checkbox's layout (fixed
x=350, clear of the price-basis dropdown ending at x=328 and the
right-anchored Generate button even at the window's 930x710 minimum
size) was verified by inspection of `ComputeTopRegionLayout`'s
constants only, not a live screenshot.

**Post-review fix pass (VOM findings #1-3 + nice-to-haves)**: fixed the
`PlanStructuralValidator` gap for `UnreducedTree`/`AccountItems` (finding
#1, three new `PlanStoreTests` facts pinning the `UnreducedTree.Recipes`-
null case, the `AccountItems` null-entry case, and the bonus
`UnreducedTree`-set-but-`AccountItems`-null pair check); projected
`PlanSolveContext.AccountItems` down to the three fields
`AccountItemIndex` actually reads before capture, and corrected this
entry's own "Cost note" to mention `AccountItems` at all (finding #2);
measured and documented the `ResolveWithOverrides` UI-thread click path
(see the new Perf spot-check #2 above) and added an `AccountItemIndex`
cache keyed by `PlanSolveContext` reference equality so a repeat click
against the same restored/generated plan skips rebuilding it (finding
#3). Nice-to-haves also taken: renamed the now-misleadingly-named
`MultiItemPlanTests` fact; corrected this section's own stale test count
(above); fixed the one non-ASCII byte in `InventoryReducerTests.cs`;
seeded `CraftingPlanView._valueOwnMaterials` from
`ModuleSettings.ValueOwnMaterials` at construction so a user's prior
choice survives a module reload instead of always resetting to Valued;
widened the "Value Own Materials" checkbox's tooltip to also mention the
15% force-buy guard and the `MaterialOpportunityCost` deduction it
gates; added a comment on `ResolveWithOverrides`' stale
`context.Tree`/`UsedMaterials`/`OwnedQuantityUsedByNodeId` after a
re-reducing re-solve. Build: `dotnet build GW2CraftingHelper.csproj
-p:Platform=x64` - clean, 0 errors. Tests: 1428 passed, 0 failed (1425
+ the 3 new `PlanStoreTests` facts above).

Gate: PASS 2026-08-16 (orchestrator live desktop session). Inline toggle renders next to Use Own Materials (checked default); schema-v2 one-time plan reset consumed the old v1 file cleanly (strip showed Ready, no restored plan); decision behavior suite-covered.

## Plan Notes section: excess/reclaim, competency, forge scope (2026-08-16)

Implemented `design-plan-notes.md` (Option 1 - a single flat "Notes"
section, last in the plan, one shared `NoteLine` row shape) against a
worktree branched from current `master` (post currency-ux-package and
cooldowns merges) - the design doc was written against a slightly older
`master`, so every file/line reference was re-located against the real
current code before editing; the design's structure held unchanged.

**What changed, in commit order:**

1. **Data model** (`aa3d21b`) - `PlanSectionType.Notes`/`PlanRowType.
   NoteLine`; `CraftingTreeNode.CraftsNeeded`/`RecipeOutputCount`
   (nullable, Craft-decision-only); the new `ExcessCraftOutput` model;
   `CraftingPlanResult.ExcessCraftOutputs`/`ProbabilisticForgeOutputItemIds`;
   `ItemMetadata.IsAccountBound`; `RawItem.Flags`.
2. **Item-flags plumbing** (`58dc043`) - `Gw2ItemApiClient` now parses
   `/v2/items`' `flags` array (previously silently discarded);
   `ItemMetadataService` derives `IsAccountBound` from `"AccountBound"`
   membership. This was the design's single largest scope item (section
   9's risk note) - implemented per the design's **primary path**, no
   fallback substitution needed: it stayed within the documented
   three-file estimate exactly (`IItemApiClient.cs`, `Gw2ItemApiClient.cs`,
   `ItemMetadataService.cs`).
3. **`CraftingTreeBuilder`** (`0476d63`) - copies the chosen recipe's
   `CraftsNeeded`/`OutputCount` onto the display tree node, closing
   `docs/gw2e-considerations.md` #4 (computed but never surfaced).
4. **`ExcessCraftOutputCalculator`** (`6662925`, new, `internal static`,
   mirrors `SellSideEconomics`' shape) - walks `CraftingTree`/
   `MultiItemRoots`, aggregates every `Decision == Craft` occurrence's
   positive `(CraftsNeeded * RecipeExpectedOutputCount - Quantity)`
   surplus (the EV basis, not the nominal `RecipeOutputCount` - using the
   nominal basis fabricates a large fake surplus for a fractional-EV
   recipe like Mystic Clover) by `ItemId`, resolves `ReclaimValue` (null
   when unpriced OR account-bound) / `IsAccountBound`. Advisory-only by
   construction: it writes nothing but `CraftingPlanResult.
   ExcessCraftOutputs`. Excludes two categories of node from the walk:
   any node beneath an `IsReferenceBranch` subtree (hypothetical "what it
   would cost to craft instead" content, never actually crafted), and
   every requested root item id (single-item `CraftingTree`, or each
   `MultiItemRoots` entry) - the root's own over-production is already
   advertised via `SellSideEconomics.ComputePerItemEconomics`'
   `sellableQuantity` bump, so including it here would double-advertise
   the same coins under a different label.
5. **`PlanResultBuilder`** (`4fd0a1e`) - extends the existing
   `craftSteps`/`recipeOptionIndex` loop with the Mystic-Clover-style
   signal (`MysticForge` discipline + `ExpectedOutputCount <
   OutputCount`), populating `ProbabilisticForgeOutputItemIds`.
   Deliberately does **not** attempt true multi-outcome gambles
   (precursor forging etc.) - per KNOWN-ISSUES #17 those never reach the
   solved tree at all, so there is nothing in a plan to detect them from.
6. **`CraftingPlanPipeline`** (`bf37c95`) - `ExcessCraftOutputCalculator.
   Apply` wired into all three `SellSideEconomics.Apply(Batch)
   SellSideEconomics` call sites (single-item generate, multi-item
   generate, `ResolveWithOverrides`'s single-vs-batch branch), grepped
   1:1 against the existing precedent.
7. **`PlanViewModelBuilder`** (`2b2ebd3`) - extracted `MatchingCharacterDisciplines`/
   `BestCharacterRating` out of `BuildCharacterAvailabilityText` (pure
   refactor, byte-identical output for that method) so the Required
   Disciplines column and the new competency notes can never drift on
   what counts as "blocked". Added `BuildNotesSection`: excess/reclaim
   lines (alphabetical by item name via `StringComparer.Ordinal`, plus a
   total line only when 2+ entries exist), competency lines (a
   `RequiredDiscipline` whose account-best rating is missing or below
   `MinRating` - gated on `CharacterDisciplines != null` so no snapshot
   never produces a false "blocked" claim), and the forge-scope line (0
   or 1). Wired into `Build()` as section 7 (last), appended only when
   non-empty. Forge-scope wording explicitly distinguishes the two
   mechanics design section 9 flags as easy to conflate: this plan's own
   Mystic-Clover-style yield IS probability-adjusted (EV already priced
   in); true multi-outcome gambles are a different mechanic never
   modeled and never shown.
8. **`NotesSectionRenderer`** (`d50ba53`, new) + `CraftingPlanView.
   CreateCollapsibleSection`'s new `PlanSectionType.Notes` case - label
   left, a right-aligned coin cell via `CoinCurrencyRenderer` only when
   `CoinValue > 0` (no cell, not an unpriced dash, for a plain
   competency/forge-scope line - those have no price concept at all).
   The default fallback case was NOT sufficient here: it calls plain
   `TextRowRenderer.CreateTextRow` for every row, which never draws a
   coin value, silently dropping every reclaim amount.

**Repo invariants checked:**

- Reclaim value is advisory-only, never folded into `Plan.TotalCoinCost`/
  `CraftingProfit`/any Summary tile - `ExcessCraftOutputCalculator`
  writes only `CraftingPlanResult.ExcessCraftOutputs`, verified by
  reading every write site in the calculator and grepping for any other
  reference to it outside `PlanViewModelBuilder.BuildNotesSection`
  (display-only reader).
- `Services/ModuleLog.cs`, `Services/PlanContentHeightMath.cs`,
  `Services/PlanRelayoutMath.cs`, scroll machinery, and
  `VendorBatchSolver`'s merged-ceil math were never touched (confirmed
  via `git diff --stat` across all eight commits). `PlanSectionType.Notes`
  gets no case in `PlanContentHeightMath.SectionBodyHeight`'s switch on
  purpose - it falls through to that method's pre-existing `default` arm
  (`rows.Count * FallbackTextRowHeight`), which is only correct because
  every `NoteLine` row renders at exactly that height; a DEBUG assert in
  `NotesSectionRenderer` guards this invariant going forward.
- ASCII-only `.cs` (swept every new/edited file with
  `grep -rlP '[^\x00-\x7F]'` - zero hits); no em dashes (same sweep, plain
  hyphens throughout); Allman braces; `GW2CraftingHelper.csproj`'s
  explicit `<Compile Include>` list updated for all three new production
  files (`Models/ExcessCraftOutput.cs`, `Services/
  ExcessCraftOutputCalculator.cs`, `Views/Rendering/
  NotesSectionRenderer.cs`) - the test project is SDK-style/globbed, so
  its four new test files needed no registration.
- No Blish HUD/`BlishHUD.exe`/`Gw2Sharp` references in any test file
  (grepped); every new test exercises a real production code path -
  `PlanViewModelBuilder.Build()` for the three Notes-section test files,
  `ExcessCraftOutputCalculator.Apply` directly (plain `CraftingTreeNode`
  fixtures, no Blish) for the calculator tests, and `PlanResultBuilder.
  Build()` directly for the three new forge-scope-detection tests added
  to `PlanResultBuilderTests.cs` (not just asserted against a stub
  `CraftingPlanResult` - the render-layer forge-scope tests use a stub,
  but the detection logic itself is covered against the real builder).
- IDs remain internal-only (excess/competency/forge-scope lines all
  resolve item/discipline **names**, never raw item ids, for display).
- Coin icons render right of the number wherever a Notes row shows one -
  unchanged shared `CoinCurrencyRenderer` path, no new coin-rendering
  code.

**Test plan, matching the design's spec:** four new test files
(`PlanViewModelBuilderNotesExcessTests.cs`,
`PlanViewModelBuilderNotesCompetencyTests.cs`,
`PlanViewModelBuilderNotesForgeScopeTests.cs`,
`ExcessCraftOutputCalculatorTests.cs`) plus three new tests appended to
the existing `PlanResultBuilderTests.cs` for the forge-scope detection
logic itself (not spelled out as a separate file in the design's test
plan, added here for real-production-path coverage of `PlanResultBuilder.
Build`'s new branch, per this repo's "tests must exercise real
production code paths" invariant).

**No fallback substitutions were needed anywhere in this package** - the
account-bound item-flags plumbing (the design's only flagged risk of
exceeding its own estimate) was implemented in full per the primary path.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - clean, 0
errors (only pre-existing StyleCop warnings, none in new/edited files).
Tests (measured, `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj`): 1501 (baseline) -> 1533 after the
initial implementation, -> 1536 (+35 total) after the review-fix round
(findings 6/7/8 each added real-production-path test coverage:
`ItemMetadataServiceTests.FetchBatchIntoCache_DerivesIsAccountBound_
FromRawItemFlags`, `Gw2ApiClient404Tests.ItemClient_ParsesFlags_
MissingFieldYieldsEmptyList`, `SellSideEconomicsTests.
ComputePerItemEconomics_FractionalEvRoot_UsesExpectedOutputCountNotNominalOutputCount`,
plus two new assertions appended to the existing
`CraftingTreeBuilderTests.CraftNode_ChildrenAreIngredients`). All green
at every one of the eight commit checkpoints (verified individually via
`git stash push --keep-index` before each commit, not just at the end).
One unrelated flaky failure was observed once
(`TradingPostServiceTests.ConcurrentCalls_OverlappingIds_
SharedIdsCoalesce_UniqueIdFetchedSeparately`, a pre-existing concurrency
test) and confirmed non-reproducing on immediate re-run, both isolated
and in the full suite.

**Nice-to-have (not fixed, noted for a future pass):**
`ExcessCraftOutputCalculator.Walk` is a plain recursive pre-order walk,
consistent with every other tree walk in this module
(`CraftingTreeBuilder.BuildChildren`, `PlanResultBuilder.
IndexRecipeOptions`) but therefore shares their same unbounded recursion
depth on a pathologically deep crafting tree; not a regression, not
addressed here.

No live desktop verification was performed - `Views/CraftingPlanView.cs`
and `Views/Rendering/NotesSectionRenderer.cs` are Blish-bound and outside
this repo's test-runnable surface, same constraint every UI-adjacent
entry in this file notes. The Notes section's real on-screen layout
(row spacing, coin-cell alignment at 28px row height, section
collapse/expand) has not been visually confirmed in a running Blish HUD
client.

Gate: PASS (hidden-when-empty) 2026-08-16 (orchestrator live desktop session). Verified hidden on two plans that generate zero notes - correct per design; positive note rendering is suite-covered (cooldown notice, a sibling feature, rendered live); a notes-generating live fixture rides the next natural field test.
## UI bundle: wiki links, snapshot status row, receipt/what-if captions (2026-08-16)

Three maintainer-approved UI features, developed together in one
worktree (`ui-bundle` branch) since they touch disjoint files except
where noted.

**Feature A (wiki links)**: two placements, both maintainer-authorized
context actions rather than visible icons.

1. Recipe Tree rows (`TreeSectionController.RenderTreeNode`): every row
   (item leaf, internal node, cost-component leaf, reference-branch
   child alike) gets a right-click handler that opens the item's GW2
   wiki page (`WikiLinkBuilder.BuildItemPageUrl(node.Name)`) in the
   default browser, plus a "Right-click: Open wiki page" tooltip line.
   This is the module's first external-URL launch
   (`WikiLinkLauncher.Open`, a thin try/caught `Process.Start` wrapper
   logging failures to `ModuleLog` rather than crashing the row click).
   Right-click was chosen over a visible icon because the row's fixed
   column grid (caret/icon/name/pill column/cost column, all
   right-anchored per `PlanRelayoutMath.ComputeTreeColumnEdges`) has no
   spare pixels at typical window widths, and right-click cannot
   collide with the row's existing left-click expand/collapse toggle.
2. Required Recipes rows (`RecipesSectionRenderer`): same right-click
   pattern, scoped to `StatusTag == "Missing!"` rows only (a row the
   user has nothing left to unlock for gets no wiki affordance at all)
   - per the spec's own "Required Recipes Missing! rows" wording, a
   narrower scope than the tree row affordance's "each item row".
   Target is flag-based (`RequiredRecipe.IsLearnedFromItem`, set from
   `RecipeOption.Flags.Contains("LearnedFromItem")` in
   `PlanResultBuilder`, mirroring the existing `IsAutoLearned` flag
   exactly): a LearnedFromItem recipe links to its own
   "Recipe: &lt;output item name&gt;" wiki sheet page
   (`WikiLinkBuilder.BuildRecipeSheetUrl`); every other recipe links to
   the output item's own page with a "#Acquisition" anchor
   (`BuildItemAcquisitionUrl`) - anchors degrade gracefully to page-top
   on a wiki page with no such section, and page titles match item
   names via wiki redirects, as the spec assumes.

URL construction (`Services/WikiLinkBuilder.cs`) is a pure, unit-tested
helper: spaces become underscores, then the whole title is percent-
encoded via `Uri.EscapeDataString` (RFC 3986 unreserved characters -
letters/digits/`-`/`.`/`_`/`~` - survive unescaped, so the underscores
just inserted are untouched) - except the recipe sheet page's literal
`Recipe:_` namespace prefix, built separately so its colon is never
percent-encoded (`%3A` would not match the site's real URLs). Tested
against both spec-named examples: `"Zojja's Claymore"` ->
`Zojja%27s_Claymore` (and `Recipe:_Zojja%27s_Claymore` for the sheet
page), `"Bolt of Damask"` -> `Bolt_of_Damask`. No item/vendor id is
ever part of a built URL or displayed anywhere - IDs stay internal-only
throughout, unchanged.

**Feature B (status own row)**: `Views/MainView.cs`'s Snapshot tab
status label (`_statusLabel`, the "Updated - &lt;date&gt; (age)"/
failure-status text) moved out of `_headerPanel` - where it shared a
fixed x=140 slot with the Clear Cache/Refresh Now buttons, a layout the
method's own long-standing "Layout risk" doc comment already flagged as
capable of running out of room at the window's clamped 930x710 minimum
size - into its own new full-width `_statusPanel` row directly beneath
the header. Every row below (search box, source-filter checkboxes,
coin panel, scrollable content) shifts down by `StatusRowHeight` (24px)
plus the same 5px gap the header already used before the search row;
every other existing gap in the chain is preserved exactly via the same
additive `const` pattern the file already used for its row Y positions.
Verified before touching layout, per the milestone's own caution: the
Snapshot tab's rows are plain fixed-Y `Panel`s sized from local
`private const int` fields in `MainView.cs` itself, not
`PlanContentHeightMath`/`PlanRelayoutMath` - those two frozen files
size only the Crafting Plan tab's section/tree rows and are untouched
by this change. Both duplicated status-string build sites
(`MainView.ApplyStatusDisplay` and `Module.cs`'s Updated/Cache
Cleared/failure-status string composition) are unedited and keep
producing the same text; only the label's layout home moved.

**Feature C (receipt/what-if captions)**: under a `BuyFromVendor` node
whose `Children` stack BOTH W4B's synthesized cost-component leaves
(`IsCostComponent == true`) AND the dimmed "what it would cost to craft
instead" reference branch appended after them
(`CraftingTreeBuilder.BuildNode`'s `componentLeaves != null &&
wantsReferenceBranch` case - `node.IsReferenceBranch` true with
`Children[0].IsCostComponent` true is exactly this case, detectable
from the node alone with no new model field), two subdued captions mark
the group boundary: "Vendor price:" ahead of the component leaves,
"If crafted instead:" ahead of the reference-branch children.

**Substitution from the spec's own sanctioned fallback**: implemented
as extra tooltip lines on each group's first child row, NOT as real
inserted tree rows. Reason, per the spec's own caution: tree row
heights flow through `PlanContentHeightMath`'s tree arm
(`ChildrenHeight`/`TreeChildFlowHeight`/`TreeNodeHeight`, all frozen,
untouched), which sums `TreeRowHeight` over exactly
`node.Children.Count` - a caption is not one of this node's existing
children, so inserting a real row would desync the height math
(computed from `Children.Count`) from the actually-rendered row count,
reproducing the exact multi-frame flash/stutter class M33 C2a's
explicit-height rework was written to eliminate (see that section's own
entry above). `Services/ReceiptCaptionHelper.cs` (pure, Blish-free,
12 unit tests) computes `ComputeCaptionSplitIndex` (the child index
where the component-leaf run ends and the reference-branch run begins,
or -1 when a node is not this stacked case) once per node, and
`CaptionForChildIndex` maps a child index to caption text or null.
`TreeSectionController.RenderTreeNode` gained an optional
`captionText` parameter (default null, so every pre-existing call site
compiles unchanged) threaded through all three places children are
rendered - the initial default-expanded pass, the lazy-build
expand/collapse toggle handler, and the Expand All button's lazy-build
loop - each computing the split index once and passing the right
child's caption text down. The row itself already carries a tooltip
(`UpdateTreeRowTooltip`/`extraTooltipLines`); the caption is inserted
at the front of that same list so it reads first, ahead of any
unit-price/caveat lines a component leaf or reference-branch child
already shows.

**Risk/follow-up (reported, not fixed)**: because the caption is
tooltip-only, a user who never hovers the first row of either group
will not see it - the row-insertion approach the spec preferred would
be strictly more discoverable, but requires either widening
`PlanContentHeightMath`'s tree arm to understand a new "caption pseudo-
row" concept or synthesizing captions as real (Blish-free-serializable)
`CraftingTreeNode` children the existing math would count for free;
both are real design changes to a frozen file's contract or the
solver-adjacent tree-node model, out of scope for this milestone's
"tooltip fallback, report the substitution" instruction.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - clean,
0 errors (StyleCop warning count unchanged from before this milestone;
no new warning codes introduced by any edited/added file's own lines).
Tests: 1576 passed, 0 failed after two review-fix commits (84538e6, cc99f3f; initially 1549 = baseline 1519 + 30 new: 15
`WikiLinkBuilderTests` + 12 `ReceiptCaptionHelperTests` + 2
`PlanResultBuilderTests` [`RequiredRecipes_LearnedFromItemFlag`/
`_NoLearnedFromItemFlag_DefaultsFalse`] + 4
`PlanViewModelBuilderStepSectionsTests` [wiki-URL scoping/flag-target
cases] = 1549) via `dotnet test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`. No
Blish HUD/`BlishHUD.exe`/`Gw2Sharp` reference in any test file; every
new test exercises a real production entry point (`WikiLinkBuilder`'s
public static methods, `ReceiptCaptionHelper`'s public static methods,
`PlanResultBuilder.Build`, `PlanViewModelBuilder.Build`), no contract
mirrors, no fake file I/O. `WikiLinkLauncher`/the `RightMouseButtonPressed`
handlers and Feature B's layout are Blish-bound UI surface outside this
repo's test-runnable boundary, same constraint every UI-adjacent entry
in this file notes - not live-verified in a running Blish HUD/GW2
client this session (browser automation requires asking the user
first, per this repo's own rule, and was not requested this session).
No `PersistedPlan` schema bump: `RequiredRecipe.IsLearnedFromItem` and
`PlanRowViewModel.WikiUrl` are both additive with safe defaults
(`false`/`null`) - the same precedent `IsCostComponent`'s own doc
comment already established - and `PlanRowViewModel` itself is never
persisted at all (only `CraftingPlanResult`, which
`PlanViewModelBuilder.Build` rebuilds fresh on every render/restore).
IDs remain internal-only throughout; coin icons unaffected (no
coin-rendering code touched by any of the three features).

Gate: MIXED 2026-08-16 (orchestrator live desktop session). Status own-row PASS (full-width, dated failure status, no button collision); wiki-link affordance PASS-visual ('Right-click: Open wiki page' tooltip renders on rows; the actual browser launch deliberately untested with guests present); receipt/what-if captions FAIL - they do not render in the reachable override-re-solve state (ARE vendor flip showed both child groups uncaptioned); root-cause + fix in flight on branch gate-fixes.

## Opportunity notes: recipe-sheet savings + seasonal vendor tips (2026-08-16)

Extended the Plan Notes section (previous entry, immediately above) with
two maintainer-directed OPPORTUNITY note kinds, both carrying concrete
numbers, per the maintainer's design law: structured sections show the
BEST-NOW option; opportunities/considerations go to Plan Notes.

**1. RECIPE-SHEET SAVINGS.** For a bought (not crafted) item whose
reference branch (`CraftingTreeBuilder`'s "what it would cost to craft
instead" hypothetical subtree) is blocked on a missing, `LearnedFromItem`
recipe that a curated map says is purchasable, and where crafting would
actually be cheaper: "Buy the `<output>` recipe (`<sheet cost>`) to craft
it instead - saves `<delta>` per unit", or the training variant when no
character meets the recipe's own discipline/rating.

**Design decision worth flagging explicitly (data-availability gap, not
a scope cut):** the task's own join ("VendorOfferStore offers whose
output is the sheet item") requires a recipe-id -> unlocking-sheet-item-id
mapping. Neither the real GW2 `/v2/recipes` API nor this repo's existing
data (recipes seed, vendor offers, item metadata) captures that
linkage anywhere - the GW2 API only exposes it from the OTHER direction
(an item's own `details.recipe_id`, on `/v2/items`, for `Unlock`/
`CraftingRecipe` consumables), and building a live index over that
(fetch-and-cross-reference every vendor offer's own item metadata to
find which are recipe sheets) is exactly the "reverse-sheet-index
plumbing" the task explicitly ruled out. Rather than fabricate a
recipe/sheet pairing I could not verify against a real wiki source (repo
invariant: never invent data), `RecipeSheetSavingsCalculator.Apply`
takes `recipeSheetItemIdByRecipeId` as an injectable, optional
dictionary - `CraftingPlanPipeline`'s own constructor default is empty.
**Since fixed (review-fix round):** `Module.cs`'s `Initialize()` now
loads a small, wiki-verified `ref/recipe_sheet_items.json` seed via the
new `Services/RecipeSheetItemSeedService.Load` (same try/catch,
Blish-`ContentsManager`-stream loading shape as the neighboring
`acquisition_hints_seed.json`/`daily_cooldown_items.json` reads
immediately above it) and passes the result as
`recipeSheetItemIdByRecipeId:` on the real `CraftingPlanPipeline`
construction - **this note now fires in production** whenever a plan's
reference branch matches a seeded recipe. Every other piece of the
feature (missing+LearnedFromItem detection, craft-vs-chosen-cost delta
math, "not comparable" skip rules, discipline-training-blocked
detection, sheet-price lookup via the ordinary
`VendorOfferStore.GetOffersForItem`, and the two-row Notes rendering) is
fully implemented and covered by real, injected fixture data in
`RecipeSheetSavingsCalculatorTests`/
`PlanViewModelBuilderNotesRecipeSheetSavingsTests`, plus the now-real
`ref/recipe_sheet_items.json` seed wiring exercised via
`RecipeSheetItemSeedService`.

**2. SEASONAL VENDOR TIP.** Blish's `FestivalContext` is read via
`Module.cs`'s `ReadActiveFestivalNames()` and projected to plain
`Festival.Name` strings (e.g. `"halloween"`) before crossing into the
Blish-free `Services`/`Models` layers. **Since fixed (review-fix round
#3):** the read is no longer a one-shot `Initialize()`-time call - it is
now a `Func<IReadOnlyList<string>>` (`CraftingPlanPipeline`'s
`activeFestivalNames` constructor parameter) that `Module.cs` passes as
`ReadActiveFestivalNames`, invoked LAZILY at plan-generation time
instead. A one-shot `Initialize()`-time read could observe `NotReady`
(the context loads asynchronously) and silently disable the feature for
the whole session; the lazy read re-checks on every plan instead. Every
failure state (context not registered, `NotReady`/`Unavailable`/`Failed`,
or any exception) still collapses to an empty list, now logged at Info
(an expected, common, benign state) so "seasonal tips disabled by
<availability>" is distinguishable in the module log from "no festival
active" (Available with an empty list, which logs nothing). Only the
exception path still logs at Warn. **MEASURED, not guessed:** `Festival.Name` and
`Festival.DisplayName` were read via `System.Reflection` directly against
the shipped `packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe` (no live
game client needed - `Festival` instances are plain static fields) -
`Name` is lowercase (`"halloween"`), NOT the capitalized `DisplayName`
(`"Halloween"`) an unverified guess would likely have used, which would
have silently broken every string match. `Gw2Constants.
FestivalDisplayNames` is a small curated Name->DisplayName table (same
measurement), not a capitalizer, since Blish's own DisplayName is not a
simple capitalization of Name for every festival (`"superadventurefestival"`
-> `"Super Adventure Festival"`).

`VendorOffer.SeasonalFestival` seeds exactly the three real
Candy Corn Vendor (Weekly) ecto (Glob of Ectoplasm, item 19721) offers
already present in `ref/vendor_offers.json`'s wiki-scraped baseline -
every other offer in that 53,536-row file is untouched. Per the
maintainer's explicit decision, seasonal offers are excluded from the
solver's own candidate set UNCONDITIONALLY (`SeasonalOfferFilter.
ExcludeSeasonal`, applied only at the actual `_solver.Solve`/
`OwnedMaterialsForceBuyPrePass` call sites in `CraftingPlanPipeline` -
every other consumer of the vendor-offers dictionary, e.g. metadata
widening, keeps seeing the raw/unfiltered data unchanged) - a plan
always assumes the regular market, active festival or not.
`SeasonalVendorTipCalculator` is the separate, purely-informational pass
that surfaces an active, cheaper festival offer as a Notes row; its cost
description is built ONLY from Item-type cost lines (the only kind the
three seeded offers have) - a coin cost line would have no safe way to
render inline as text without violating the "coin icons right of the
number" invariant with the row's one `CoinValue` slot already spent on
the plan's own price, so that case is skipped entirely rather than
rendered incorrectly (currently unreachable with the seeded data, but a
real restriction, not a hypothetical one).

The wiki-scrape-updater-side automation to detect/tag FUTURE seasonal
offers from the wiki's Temporary template (so new festival vendors don't
need a hand edit like this one) is a recorded follow-up, not this pass.

**What changed:** `Models/CraftingTreeNode.cs` (`ReferenceRecipeId`/
`Disciplines`/`MinRating`/`IsLearnedFromItem`, reference-branch-only),
`Models/RecipeSheetSavingsOpportunity.cs`, `Models/SeasonalVendorTip.cs`,
`Models/CraftingPlanResult.cs` (+2 fields), `Models/VendorOffer.cs`
(+`SeasonalFestival`), `Models/Gw2Constants.cs` (+`HalloweenFestivalName`,
`FestivalDisplayNames`), `Services/CraftingTreeBuilder.cs`
(`ApplyReferenceRecipeInfo`), `Services/CostLineValuation.cs` (new,
shared coin-valuation helper - never touches `VendorBatchSolver`, one of
the DO-NOT-TOUCH files), `Services/SeasonalOfferFilter.cs` (new),
`Services/RecipeSheetSavingsCalculator.cs` (new),
`Services/SeasonalVendorTipCalculator.cs` (new),
`Services/CraftingPlanPipeline.cs` (two new optional constructor
parameters, both default-empty; wired at all three result-building call
sites), `Services/PlanViewModelBuilder.cs` (`BuildNotesSection` gains the
two new note kinds), `Module.cs` (`FestivalContext` read, now lazy;
loads and wires the `ref/recipe_sheet_items.json` seed),
`ref/vendor_offers.json` (3 rows tagged),
`ref/recipe_sheet_items.json` (new, curated recipe-id ->
unlocking-sheet-item-id seed), `Services/RecipeSheetItemSeedService.cs`
(new, loads the seed file), `tools/VendorOfferUpdater/Models/VendorOffer.cs`
(seasonal-festival tagging support for the updater side),
`tests/VendorOfferUpdater.Tests/SeasonalFestivalRoundTripTests.cs` (new),
`.github/workflows/tests.yml` (updated to run the updater/seeder test
projects alongside the main suite).

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests (both new calculators and
  `SeasonalOfferFilter`/`CostLineValuation` are plain `internal static`
  classes over `Models` types only; `Module.cs`'s own `FestivalContext`
  read is the ONLY place in this whole package that touches Blish, and
  it is the one file this repo does not unit-test).
- [x] Tests exercise real production paths (real `VendorOfferStore`
  backed by a temp-directory baseline in
  `RecipeSheetSavingsCalculatorTests`, not a fake/mirrored store -
  matches `VendorOfferStoreTests`' own precedent).
- [x] No fake file I/O tests introduced.
- [x] Pricing logic preserves multi-source correctness (`CostLineValuation`
  refuses - never guesses - on a non-coin currency line, an unpriced Item
  line, or any unrecognized `CostLine.Type`, mirroring `VendorBatchSolver.
  EvaluateVendorOffers`' own posture without touching that DO-NOT-TOUCH
  file).
- [x] IDs remain internal-only (every note resolves item/recipe/discipline
  **names**, never raw ids).

**Validation performed:** `dotnet build GW2CraftingHelper.csproj
-p:Platform=x64` - clean, 0 errors (only pre-existing StyleCop warnings,
none in new/edited files). `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` (measured, at this pass's own commit):
1554 (baseline) -> 1601 (+47): `CostLineValuationTests` (7),
`SeasonalOfferFilterTests` (5), `RecipeSheetSavingsCalculatorTests` (12),
`SeasonalVendorTipCalculatorTests` (10 - includes a review-fix-round
addition, see below), `PlanViewModelBuilderNotesRecipeSheetSavingsTests`
(4), `PlanViewModelBuilderNotesSeasonalVendorTipTests` (5), plus 3 new
cases appended to the existing `CraftingTreeBuilderTests`, plus one
existing test file gained two new constructor parameters
(`CraftingPlanResultBuilders`, not itself a test). All 1601 green at
that point.

**Updated (later review-fix rounds, measured 2026-08-16):** further
review-fix rounds (activating the recipe-sheet seed, seasonal tag
round-trip/lazy festival read/tip wrap/craft-cost math fixes, the
recursive vendor-currency guard, and the updater CI gap) added more
tests and a new CI-wired test project. Current totals: `dotnet test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` - 1616
green. `dotnet test tests/VendorOfferUpdater.Tests/
VendorOfferUpdater.Tests.csproj` - 136 green (includes the new
`SeasonalFestivalRoundTripTests`). `dotnet test
tests/GW2CraftingHelper.RecipeSeeder.Tests/
GW2CraftingHelper.RecipeSeeder.Tests.csproj -p:Platform=x64` - 3 green.
All three suites green as of this pass.

**Review-fix round (self-review, before handoff):**
`SeasonalVendorTipCalculator` was picking the FIRST qualifying seasonal
offer per item rather than the cheapest - the three real seeded ecto
offers are exactly this case (three Halloween candy colors, each its
own TP price, all trading for the same 5x ecto), so a plan could have
surfaced a real but non-optimal deal. Fixed to scan every qualifying
offer and keep the cheapest, mirroring `RecipeSheetSavingsCalculator`'s
own identical "cheapest priceable offer wins" precedent - new test
`MultipleQualifyingOffers_PicksCheapest`.

**Risks / follow-ups:**
- `recipeSheetItemIdByRecipeId` now ships from `ref/recipe_sheet_items.json`
  (see the RECIPE-SHEET SAVINGS section above) - the seed is small and
  curated by hand; growing its coverage (more wiki-verified recipe/sheet
  pairs) is the natural next step to widen when the feature can fire,
  not to activate it for the first time.
- Seasonal-offer detection is a one-time hand tag of three known rows,
  not an automated wiki-scrape pass - see the SEASONAL VENDOR TIP section
  above.
- No live desktop verification was performed - `Views/CraftingPlanView.cs`
  and `Views/Rendering/NotesSectionRenderer.cs` are Blish-bound and
  outside this repo's test-runnable surface, same constraint every
  UI-adjacent entry in this file notes (including the immediately
  preceding Plan Notes entry, which this one extends without touching
  `NotesSectionRenderer.cs`/`PlanContentHeightMath.cs`/
  `PlanRelayoutMath.cs`/scroll machinery at all - every new row is a
  plain `NoteLine`, already covered by that renderer's existing 28px
  contract). The two new note kinds' real on-screen wording/wrapping have
  not been visually confirmed in a running Blish HUD client - the
  RECIPE-SHEET SAVINGS row shape has been verified via injected test
  fixtures and is now wired to a real, non-empty
  `recipeSheetItemIdByRecipeId` seed in production (see above), but has
  not yet been confirmed against a real generated plan on-screen.

Gate: PASS (negative checks) 2026-08-16 (orchestrator live desktop session). Seasonal exclusion verified as the headline: the ARE craft path now prices ectos at the real TP rate (~26s vs the old ~4s26 phantom Halloween vendor), the ecto row's vendor source is gone entirely, and the Candy Corn tip correctly does NOT render out of season; sheet-savings positive render suite-covered.
Gate: not yet run live - queued for the next batched desktop session (maintainer is currently holding the desktop). Merged after the full review pipeline resolved every finding (2 adversarial rounds, verification zero-blocking, 1536/1536 pre-merge), under the maintainer's standing merge directive (2026-08-16).

## Source selection simplification: competency-aware default + subdued losing pills (2026-08-16)

Maintainer-approved redesign (docs/gw2e-considerations.md context, the
merged Plan Notes/competency machinery). Two independent rules on branch
`source-selection-simplification`:

**Rule 1 - competency-aware default.** A Craft source now only wins the
AUTOMATIC buy-vs-craft-vs-vendor comparison when some character actually
has one of the winning recipe's Disciplines at its MinRating
(`CraftCompetencyEvaluator`, a pure/testable Services class). Snapshot
data absent (`characterDisciplines == null`) is competency UNKNOWN and
never penalizes craft - byte-identical to pre-existing behavior for every
caller that doesn't pass the new parameter.

*Seam decision:* studied how `PickCheapest` (pure economics) and
`DecisionPillPlanner` (a pure mirror of `node.Decision`, no independent
logic of its own) divide responsibility, then implemented the check
INSIDE `PlanSolver.Evaluate`, folded into the SAME
`craftExcludedFromAutoPick` flag the M34-B2a #3 force-buy pre-pass
already uses (excludes Craft from the automatic `PickCheapest`/terminal-
fallback race; `canCraft` and the manual-override branch both read the
UNMODIFIED `bestComparable`/`bestFallback` values, so `CanCraft`/the
CRAFT pill and a manual override are both unaffected). This is cheaper
and more surgical than a separate throwaway-solve prepass (mirroring
`OwnedMaterialsForceBuyPrePass`'s own mechanism) would have been: the
winning recipe's Disciplines/MinRating are already in scope at the exact
point `craftExcludedFromAutoPick` is read, with zero extra solves.
Additionally gated on `(canBuyTp || canBuyVendor)` - a node whose ONLY
feasible source is Craft still auto-crafts regardless of competency,
since excluding it there would drop real, priced cost data out of the
plan entirely (UnknownSource) rather than merely changing a default.
`CraftingPlanPipeline` threads `characterDisciplines` into every
`Solve()` call for a generation, including the zero-owned guide solve
(so `InventoryReducer` never discounts ingredients for a Craft path
competency will end up overriding) and into `ResolveWithOverrides`' local
re-solves via the already-existing `PlanSolveContext.CharacterDisciplines`
field.

**Rule 2 - subdued losing pills.** A non-selected, multi-option pill that
decisively loses to the selected pill renders subdued (reuses
`PillKind.Locked`'s exact muted-gray color via a new `PillKind.Subdued`
case - "no new colors", per the maintainer's own wording) with a tooltip
explaining why, under two independently-checked rules:
- **StrictDomination** (checked first - a stronger, valuation-free
  claim): the losing option's raw coin and every currency/item cost-line
  kind are each >= the selected option's (missing kind on either side
  reads as 0), with at least one strictly greater. Needs NO valuation at
  all - covers the maintainer's own canonical Amalgamated Rift Essence
  shape (vendor needs the same coin, 10 more raw Globs of Ectoplasm than
  crafting does) with a real end-to-end test through the actual solver.
- **Weighted**: both options' fully-valued decision-value figures are
  non-null and the losing one is strictly greater ("more expensive at
  your current currency values"). Any strictly-positive margin counts -
  a pill only reaches this comparison at all when it is one of 2-3 real
  offered choices, so an objectively (if narrowly) worse valued option is
  still worth flagging rather than under-reporting it behind an invented
  percentage threshold (no such threshold was specified).
- Unvalued AND non-dominated (a genuine tradeoff, e.g. less of one kind,
  more of another) leaves both pills normal, per spec.
- The selected pill is never evaluated as a "losing" candidate at all -
  structurally impossible for it to be subdued.

*Seam decision:* `PlanSolver.Evaluate` now computes a raw
`PillSourceCostBreakdown` (RawCoin + non-coin currency/item cost lines,
raw quantities, never gold-valued for the raw-comparison fields) for
EVERY feasible source at a node - not just the winner - mirroring
`costDiagnostics`' own "always computed, never filtered by decision"
precedent, attached to `PlanSolver.Decision`/`SolverDecision`/
`CraftingTreeNode` via the SAME winner-agnostic passthrough chain
`CanCraft`/`CanBuyTp`/`CanBuyVendor` already use. Vendor's breakdown
reuses `VendorBatchSolver`'s own already-evaluated
`VendorCurrencyCosts`/`VendorItemCosts`/coin-cost output verbatim
(`VendorBatchSolver.cs` itself - the merged-ceil math - was never
touched, per the DO-NOT-TOUCH list); Craft's breakdown decomposes the
candidate recipe's DIRECT (non-recursive) ingredient list, which needs
no pricing/recursion since domination compares raw ingredient quantities
by id, the same granularity `VendorItemCostLine.Quantity` already uses.
Detection itself (`PillSubduingEvaluator`) is a pure, Blish-free,
directly-testable Services class operating only on two
`PillSourceCostBreakdown` values - never reads a `CraftingTreeNode`,
never resolves a name, never decides which pill is selected
(`DecisionPillPlanner`'s own job). Tooltip TEXT is built by a second
pure class (`PillSubduingTooltipBuilder`, mirroring the pre-existing
`ValueDetailTooltipBuilder` "Blish-free builder, the View only assigns
the string" split) so raw currency/item ids never cross into
`DecisionPillPlanner`/`PillSubduingEvaluator` at all (repo invariant:
IDs internal-only) - name resolution happens only at the View layer,
which already had `CurrencyMetadata` for this purpose and gained a new
`PlanViewModel.ItemMetadata` passthrough (mirroring `CurrencyMetadata`'s
own precedent exactly) for the item-kind case.

*Adversarial-review fix (self-caught, not maintainer-flagged):* a merged
multi-occurrence vendor step's per-occurrence `VendorCurrencyCosts`/
`VendorItemCosts` (and therefore this node's own
`BuyFromVendorCostBreakdown`, built from those same local numbers) can
disagree with the corrected `TotalCost` once `AllocateVendorNodeCosts`
reallocates it - the exact signal `CraftingTreeNode.
VendorComponentCostsUnreliable` already exists for, and
`ValueDetailTooltipBuilder`/`CraftingTreeBuilder.
BuildVendorCostComponentLeaves` already gate on. `DecisionPillPlanner`
now takes the same conservative posture: subduing detection is
suppressed entirely (every pill stays plain `Available`) whenever that
flag is set on the node, rather than risk a wrong verdict off stale
numbers when a merged Vendor decision is the SELECTED baseline every
other pill gets compared against.

**Repo invariants checklist:**
- ASCII-only / no em-dashes in every new/edited `.cs` file (verified via
  a non-ASCII grep sweep across the full changed-file list - zero hits).
- Allman brace style throughout.
- Tests exercise real production code paths - `PlanSolver.Solve`,
  `CraftingTreeBuilder.BuildTree`, and `DecisionPillPlanner.
  BuildPillSpecs` are called directly and unmocked in every new test
  file; no Blish HUD reference anywhere in tests.
- IDs remain internal-only - `PillCostDelta.Id` is a raw currency/item
  id but is a Services-layer, id-only DTO never displayed directly;
  `PillSubduingTooltipBuilder` is the sole place it gets resolved to a
  name before ever reaching a tooltip string.
- Coin amounts in the new tooltip text use the same "Xg Ys Zc" plain-text
  convention `ValueDetailTooltipBuilder` already established for its own
  hover (not a coin-icon rendering context, so the icon-right-of-number
  rule for the coin PANEL/shopping rows does not apply here).
- `Services/VendorBatchSolver.cs` (merged-ceil math), `Services/
  ModuleLog.cs`, `PlanContentHeightMath`, `PlanRelayoutMath`, and scroll
  machinery were never touched.

**New files:** `Services/CraftCompetencyEvaluator.cs`,
`Services/PillSubduingEvaluator.cs`, `Services/
PillSubduingTooltipBuilder.cs`, `Models/PillSourceCostBreakdown.cs`.

**Test plan (real path per rule, as specified):** competency flips the
default (`PlanSolverCraftCompetencyTests.
NonCompetentAccount_CraftCheapestButNotCraftable_DefaultsToNextBestSource`);
unknown competency preserves prior behavior
(`NoCharacterDisciplines_CompetencyUnknown_CraftStillAutoWins`); domination
detected on the real Amalgamated Rift Essence shape through the actual
solver (`PlanSolverPillSubduingTests.
AmalgamatedRiftEssenceShape_VendorNeedsMoreRawEcto_StrictlyDominated`);
weighted subduing through a real `CurrencyValuation`
(`WeightedValuation_VendorCheaperInRawCoinButPricierWhenValued_Subdued`);
unvalued+non-dominated left untouched, both at the pure-evaluator level
(`PillSubduingEvaluatorTests.UnvaluedAndNonDominated_BothPillsStayNormal`)
and through the real solver
(`PlanSolverPillSubduingTests.UnvaluedNonDominatedAlternative_
StaysAvailable_NotSubdued`) - plus the self-caught
`VendorComponentCostsUnreliable` suppression fix, and the "no alternative
source exists" guard for Rule 1 that stops competency from silently
dropping a node's cost out of the plan.

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
GW2CraftingHelper.csproj -p:Platform=x64` - clean, 0 errors (only
pre-existing StyleCop warnings scattered across the file set, none of
them new patterns this package introduced beyond one cosmetic SA1204
"static members before non-static" in the brand-new
`PillSubduingEvaluator.cs`, left as-is - matches this repo's own
already-extensive, pre-existing, unaddressed StyleCop backlog rather
than a regression).

Tests (measured, `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj`): 1554 (baseline) -> 1575 after Rule 1
(+21) -> 1605 after Rule 2 (+30, including the VendorComponentCostsUnreliable
suppression fix's own test). All green at every commit checkpoint.

**Nice-to-have (not fixed, noted for a future pass):**
- `PlanResultBuilder` already carries two independent, byte-identical
  copies of the "MysticForge/Achievement/Merchant are inherently
  available" tag set (`InherentlyAvailableDisciplines`/
  `NonCraftingDisciplines`); `CraftCompetencyEvaluator` now adds a THIRD
  independent copy (`NonLevelableDisciplineTags`) rather than couple a
  solver-path pure class to `PlanResultBuilder`'s display-adjacent
  internals - a future pass could extract one shared canonical set.
- The cosmetic SA1204 StyleCop warning noted above.
- StrictDomination's ITEM-kind comparison only decomposes a candidate
  craft recipe's DIRECT ingredients, not a full recursive expansion - a
  domination that only becomes visible several craft levels deep (rather
  than at the immediate ingredient list) is not detected. Not believed to
  affect the maintainer's own Amalgamated-Rift-Essence-shaped cases
  (which are direct-ingredient-level by construction), and no case
  requiring deeper recursion was specified.

No live desktop verification was performed - `Views/Rendering/
TreeSectionController.cs` and `Views/Rendering/PillColors.cs` are
Blish-bound and outside this repo's test-runnable surface, same
constraint every UI-adjacent entry in this file notes. The Subdued
pill's actual on-screen color/tooltip rendering has not been visually
confirmed in a running Blish HUD client.

Gate: not yet run live - queued for the next desktop session (subdued-pill + competency-default visuals). Merged after the deepest pipeline of the wave: implementation, two adversarial rounds, three verification passes (the second MEASURED an overcorrection suppressing a real 70c opportunity; the third revert-tested both direction pins on the final design), under the maintainer's standing merge directive (2026-08-16).

## Source selection simplification: adversarial-review fix round (8 findings) (2026-08-16)

A follow-up adversarial code review of the `source-selection-simplification`
work above (competency-aware default + subdued losing pills) found 8
Critical/Must-Fix defects plus several nice-to-haves. All 8 fixed on
branch `source-selection-simplification`, plus the cheap same-file
nice-to-haves.

**1 - competency gate inspected only the single cheapest recipe option.**
`PlanSolver.Evaluate` now tracks the best COMPETENT option per tier
(`bestCompetentComparableOption`/`bestCompetentFallbackOption`) alongside
the existing unfiltered `bestComparable`/`bestFallback` pair -
`craftExcludedFromAutoPick` (competency branch) fires only when NO option
in EITHER tier is competent. `canCraft` and the manual-override branch
still read the unfiltered pair, unchanged. `autoPickCraftOption`/
`craftBreakdownDecisionValue`/`autoPickCraftRealCost`/`autoPickRecipeId`
resolve to whichever of the four (comparable/fallback x competent/raw)
buckets actually applies, so PickCheapest, both Craft `Commit` sites, and
`BuildCraftCostBreakdown` all operate on the SAME recipe. Test:
`PlanSolverCraftCompetencyTests.
MultiRecipeNode_OneCompetentOneNot_CompetentSiblingAutoWinsOverExcludedCheaperOne`.

**2 - Weighted subduing wording blamed "currency values" for pure-gold
gaps.** Added `PillSubduingResult.HasNonCoinCost` (true when either
side's `CostLines` is non-empty), computed in `PillSubduingEvaluator`.
`PillSubduingTooltipBuilder` now says plain "More expensive (N more)"
when no currency was ever involved. Nice-to-have folded in (same file):
StrictDomination's "same currencies" claim was also wrong whenever the
union treated a missing kind as 0 on the selected side - reworded to
"needs everything the selected option needs, plus ...". Tests:
`PillSubduingEvaluatorTests.Weighted_PureCoinBothSides_HasNonCoinCostFalse`,
`PillSubduingTooltipBuilderTests.Weighted_PureCoinDifference_NoCurrencyMentioned`.

**3 - force-buy pre-pass's throwaway solve was competency-UNAWARE.**
`OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds` gained a
`characterDisciplines` parameter, threaded from `effectiveCharacterDisciplines`
at both real call sites in `CraftingPlanPipeline` (single-item and
batch). Test:
`OwnedMaterialsForceBuyPrePassTests.
ChildIngredientNotCraftable_CharacterDisciplinesThreaded_ChangesForceBuyResult`
(the same tree/prices produce a DIFFERENT force-buy result depending
solely on whether this parameter is passed).
Correction (round-2 adversarial review, 2026-08-16): the sentence above
originally claimed "all 3 real call sites ... and the pre-pass's own
re-run inside `ResolveWithOverrides`" - there is no such re-run. A grep
for `ComputeForceBuyOnlyNodeIds(` shows exactly 2 real call sites
(`CraftingPlanPipeline.cs:270` and `:770`); the override path reuses the
already-frozen `context.ForceBuyOnlyNodeIds` instead of recomputing it.
Stale guidance, now corrected in place.

**4 - StrictDomination compared post-reduction craft quantities against
un-reduced vendor quantities.** Added `PillSourceCostBreakdown.
RawQuantitiesReducedByOwnedStock`, set by `PlanSolver.
AnyIngredientReducedByOwnedStock` (reference-keyed lookup against
InventoryReducer's own `OwnedQuantityUsedByNode`, threaded through a new
`PlanSolver.Solve`/`Evaluate` parameter). `PillSubduingEvaluator` skips
StrictDomination (only - Weighted is unaffected, its DecisionValue
already reflects real discounted economics) whenever either side is
flagged. Threaded at the 3 real solve call sites; NOT threaded for
`ResolveWithOverrides`' frozen-tree branch (no fresh reduction there to
source a reference-keyed dictionary from) - documented gap, not a
regression (this check did not exist there before either).

**5 - craft breakdown silently dropped GuildUpgrade/unrecognized
ingredients.** Added `PillSourceCostBreakdown.IsIncomplete`, set by
`BuildCraftCostBreakdown` whenever an ingredient has no representable
line. `PillSubduingEvaluator` refuses BOTH rules when either side is
incomplete (same conservative posture as
`VendorComponentCostsUnreliable`). Nice-to-have folded in (same file):
corrected the "Count is always >= 1" doc claim on
`PillSourceCostBreakdown` (false for an owned-stock-reduced-to-0
ingredient line).

**6 - the "genuine alternative" guard counted a fallback-tier vendor
offer.** The competency-exclusion guard now requires
`buyTotalCost.HasValue || comparableVendorValue.HasValue` (a real
COMPARABLE alternative), not `canBuyTp || canBuyVendor` (which is also
true for an unvalued-currency-only offer). Without this, a node with a
fully-priced but untrained craft and only a karma-only vendor offer
would silently default onto the unvalued vendor purchase, dropping the
real priced cost from the plan. Test: `PlanSolverCraftCompetencyTests.
NonCompetentAccount_OnlyAlternativeIsFallbackTierVendor_
StillAutoCraftsRatherThanDroppingCost`.

**7 - the competency flip had no user-visible explanation (design-law
gap).** Added `SolverDecision.CraftExcludedByCompetency`/
`CraftExcludedRealCost`/`CraftExcludedDisciplines`/`CraftExcludedMinRating`
(passthrough from a new `PlanSolver.Decision` set of fields, straight
through `CraftingTreeNode`), and a new `CompetencyOpportunityCalculator`
(same shape/placement precedent as `ExcessCraftOutputCalculator`) that
walks the built display tree for a node where craft was excluded on
competency grounds, did NOT end up crafted anyway (a manual override to
Craft is excluded - nothing to report, the user already chose), and
would genuinely have been cheaper. Writes
`CraftingPlanResult.CompetencyOpportunities`, rendered by
`PlanViewModelBuilder.BuildNotesSection` as a new Plan Notes bucket
("{item}: could be crafted for less - no character has {discipline}
{rating}"), per the maintainer's own design law (opportunities go to
Plan Notes with concrete numbers). Tests:
`CompetencyOpportunityCalculatorTests` (8 cases: basic delta, manual-
override suppression, not-excluded, cost-neutral, reference-branch
exclusion, cross-occurrence dedup, null/empty), plus a real pipeline
round-trip in `CraftingPlanPipelineTests.
GenerateStructuredAsync_CraftExcludedByCompetency_PopulatesCompetencyOpportunities`.

**8 - a partial character-fetch failure could leave `CharacterDisciplines`
non-null.** `Gw2AccountSnapshotService`'s outer
`catch (Exception ex) when (!(ex is OperationCanceledException))` around
the per-character loop now nulls `snapshot.CharacterDisciplines`
explicitly - before this fix, anything escaping the loop (WhenAll
faulting, a `.Result` rethrow) left whatever partial list had already
been gathered, which read as an affirmative "not trained on any
character" for every character the loop never reached. No test added:
this class directly references `Blish_HUD`/`Gw2Sharp` types
(`Gw2ApiManager`), which the repo's test invariants forbid importing
into any test file - the fix is a one-line, low-risk null-out with no
production code path this repo's test suite is permitted to exercise.

**Files touched:** `Services/PlanSolver.cs`, `Services/SolverDecision.cs`,
`Services/PillSubduingEvaluator.cs`, `Services/PillSubduingTooltipBuilder.cs`,
`Services/OwnedMaterialsForceBuyPrePass.cs`, `Services/CraftingPlanPipeline.cs`,
`Services/CraftingTreeBuilder.cs`, `Services/Gw2AccountSnapshotService.cs`,
`Services/CompetencyOpportunityCalculator.cs` (new),
`Models/PillSourceCostBreakdown.cs`, `Models/CraftingTreeNode.cs`,
`Models/CraftingPlanResult.cs`, `Models/CompetencyOpportunity.cs` (new).

**Deliberately NOT applied** (each explicitly flagged by the review as
needing maintainer sign-off, not a unilateral call): Weighted subduing's
"any strictly-positive margin" threshold (the brief said "decisive",
none was specified - **superseded, see the round-2 entry below**: a
round-2 finding directed gating this rather than continuing to defer it,
so it is no longer un-signed-off/live-by-default as of that entry);
extracting the now-three-times-duplicated
`NonLevelableDisciplineTags`/`NonCraftingDisciplines`/
`InherentlyAvailableDisciplines` set (flagged for a future pass, not
this one, and STILL not applied in round 2 either); the persisted-plan
JSON size of the 3 new `PillSourceCostBreakdown`s per node (no
measurement taken, no `[JsonIgnore]` added; also still not applied in
round 2).

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
GW2CraftingHelper.csproj -p:Platform=x64` - clean, 0 errors (StyleCop
warnings only, all pre-existing patterns, none new).

Tests (measured, `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj`): 1619 total, 0 failures, at the final
checkpoint (one pre-existing test's own expected string needed updating
for finding 2's wording change - `PillSubduingTooltipBuilderTests.
Weighted_PureCoinDifference_NoCurrencyMentioned`'s own initial FormatCoin
expectation, fixed within the same pass before commit).

Gate: not yet run live - Blish-bound rendering (the new Plan Notes rows,
the reworded subduing tooltips) has not been visually confirmed in a
running Blish HUD client, same constraint every UI-adjacent entry in
this file notes.

## Source selection simplification: adversarial-review fix round 2 (5 findings) (2026-08-16)

A further adversarial code review of the round-1 fix round above found 5
Critical/Must-Fix defects (two of them only-half-fixed round-1 items) plus
several nice-to-haves. All 5 fixed on branch `source-selection-
simplification`, plus one cheap same-file nice-to-have.

**1 - Weighted tooltip wording still blamed "currency values" for
pure-gold gaps (round-1 finding #2 only half-fixed).** Round-1's
`HasNonCoinCost` fired whenever either side's `CostLines` was non-empty -
but `PlanSolver.BuildCraftCostBreakdown` emits a Type == "Item" line for
EVERY craft ingredient regardless of valuation (TP-priced, never
user-valued), so any craft-vs-TP comparison had non-empty `CostLines`
purely from its ingredient list. `HasNonCoinCost` now checks for a Type
== "Currency" line specifically (`PillSubduingEvaluator.HasCurrencyLine`)
- the only `CostLine` kind a `CurrencyValuation` can ever price. Tests:
`PillSubduingEvaluatorTests.Weighted_ItemLinesOnlyNoCurrencyLine_HasNonCoinCostFalse`
/ `Weighted_CurrencyLinePresent_HasNonCoinCostTrue`,
`PlanSolverPillSubduingTests.WeightedCraftLosing_PureGoldNoValuation_HasNonCoinCostFalse`
(real Solve()-path, the exact reported TP-400c-vs-craft-500c shape).

**2 - costDiagnostics still recorded the competency-unfiltered craft cost
(round-1 finding #3 fixed only the recursion half of the same
divergence).** `PlanSolver.Evaluate` still wrote `costDiagnostics[node.NodeId]
= (buyTotalCost, bestComparableCraftCost ?? bestFallbackCraftCost)` -
ignoring competency entirely, always the numerically cheapest recipe in
each tier - while the real decision path commits
`craftBreakdownDecisionValue`/`autoPickCraftRealCost` (competency-
resolved). `OwnedMaterialsForceBuyPrePass`'s 85% rule was therefore
derived from a craft cost the real solve could never actually commit to
whenever competency demoted the pick to a costlier competent sibling
recipe (or excluded craft entirely). Fixed by moving the write to AFTER
competency resolution and changing the recorded figure to
`craftBreakdownDecisionValue ?? autoPickCraftRealCost` - the exact
tier/competency-resolved pair the Craft commit sites use. Test:
`PlanSolverForceBuyOnlyTests.
CostDiagnostics_CompetencyResolved_UsesCompetentRecipeNotCheapestOverall`.

**3 - Weighted subdued on a 1-copper margin; brief says "decisive".**
Round-1 deferred this to maintainer sign-off and shipped the bare
strictly-positive-margin behavior live (not merely "documented as
deferred"), so a genuinely near-equal alternative rendered in Locked's
muted gray and was told it was "more expensive". Gated with an
absolute-AND-relative floor (`PillSubduingEvaluator.IsDecisiveMargin`):
the margin must clear BOTH a 100-copper (1 silver) absolute floor AND a
1% relative floor of the selected option's own value - requiring both
(not either) is the more conservative reading, since a margin that only
clears one measure (e.g. 101c on a 10g/100000c purchase - past the
absolute floor but only 0.1%) still is not "decisive" by the other. No
maintainer-specified numbers exist for either constant - these are a
deliberately modest, easily-tunable starting point, not a precisely-
derived figure. Tests: `PillSubduingEvaluatorTests.
Weighted_OneCopperMarginOnMultiGoldPurchase_NotDecisive_NotSubdued`,
`Weighted_MarginClearsAbsoluteButNotRelativeFloor_NotSubdued`,
`Weighted_MarginClearsRelativeButNotAbsoluteFloor_NotSubdued`,
`Weighted_MarginClearsBothFloors_Subdued`,
`Weighted_SelectedValueZero_AnyPositiveMarginClearingAbsoluteFloorIsDecisive`.

**4 - three parallel reference-equality ternary chains had to stay in
sync by hand (future merge hazard).** `PlanSolver.Evaluate` resolved
`autoPickCraftOption` via a 4-term `??` chain, then re-derived "which
bucket did it come from" three more times (for `craftBreakdownDecisionValue`,
`autoPickCraftRealCost`, `autoPickRecipeId`) via independent reference-
equality ternary chains against the same four `best*Option` variables -
correct today, but a future edit to the `??` precedence (or a fifth
bucket) could silently desynchronise them, producing a Commit with one
recipe's cost and another recipe's RecipeId with no test catching it.
Collapsed into a single `PlanSolver.CraftAutoPickCandidate` (a small
readonly struct holding Option/RealCost/ComparisonValue/RecipeId),
resolved once via an if/else-if chain, with the other three values read
straight off the one resolved candidate. Pure refactor - existing
behavior (and the full pre-existing test suite) unchanged; no new test
needed beyond the suite continuing to pass.

**5 - competency demotion inside the craft arm had no user-visible
explanation for two shapes (round-1 finding #7 accepted this exact gap,
closed only for the all-untrained case).** `CraftExcludedByCompetency` is
only true when NO option in EITHER tier is competent, so two real shapes
raised the plan's cost silently: (a) the cheapest COMPARABLE recipe is
untrained but a competent recipe exists only in the FALLBACK tier -
`craftBreakdownDecisionValue` becomes null and craft never enters the
comparable-tier PickCheapest race at all, TP/vendor commits, nothing
explains why; (b) a costlier competent SIBLING recipe wins Craft over a
cheaper untrained one - `CraftExcludedByCompetency`'s own "Decision ==
Craft -> nothing to report" precedent incorrectly suppressed this, even
though the user never got the cheap recipe. Added a second, independent
field set - `Decision`/`SolverDecision`/`CraftingTreeNode.
CheapestCraftUntrained`/`CheapestCraftRealCost`/`CheapestCraftDisciplines`/
`CheapestCraftMinRating` - true whenever the numerically cheapest raw
craft candidate overall (`bestComparableOption ?? bestFallbackOption`,
same tier priority as `autoPickCraftOption` but WITHOUT the competent-
first override) is untrained, independent of whether the AUTOMATIC pick
itself got excluded. Deliberately does NOT drive
`craftExcludedFromAutoPick` or any other decision-affecting behavior -
purely additive display data, same as `CraftExcludedRealCost` before it.
`CompetencyOpportunityCalculator` now reads these new fields instead of
the narrower `CraftExcludedByCompetency` pair; the existing `Decision !=
Craft` guard was DROPPED (the delta-based check - SubtreeCost strictly
greater than the cheap recipe's real cost - subsumes it: a manual
override or an automatic pick landing on that SAME cheap recipe always
makes the delta exactly 0). `CraftExcludedByCompetency` and its own
fields are UNCHANGED and still drive the real `craftExcludedFromAutoPick`
behavioral gate - only the notification/Plan-Notes path was
re-pointed. Tests: `CompetencyOpportunityCalculatorTests.
CraftUsingACostlierCompetentSiblingRecipe_StillReported`,
`PlanSolverCraftCompetencyTests.
FallbackTierCompetentRecipe_CheaperComparableUntrained_ReportsOpportunity`
(shape a, full Solve+CraftingTreeBuilder+CompetencyOpportunityCalculator
round trip), `CostlierCompetentSiblingWinsCraft_CheaperUntrainedSibling_ReportsOpportunity`
(shape b, same round trip) plus updated assertions on the existing
`MultiRecipeNode_OneCompetentOneNot_CompetentSiblingAutoWinsOverExcludedCheaperOne`.

**Nice-to-have folded in (same file):** `docs/KNOWN-ISSUES.md` finding #3's
own entry above claimed `characterDisciplines` was threaded "at all 3
real call sites in `CraftingPlanPipeline` ... and the pre-pass's own
re-run inside `ResolveWithOverrides`" - there is no such re-run; a grep
for `ComputeForceBuyOnlyNodeIds(` shows exactly 2 real call sites
(`CraftingPlanPipeline.cs:270` and `:770`), the override path reuses the
frozen `context.ForceBuyOnlyNodeIds`. Corrected in place above.

**Deliberately NOT applied** (each explicitly still needing a decision
this round did not make): the Subdued pill's missing "why" tooltip on
the non-interactive path (`TreeSectionController.cs:1157` - a Views file,
outside this round's Services/Models scope); a manual override to Craft
still commits `bestComparableRecipeId`/`bestComparableCraftRealCost`
(possibly the untrained recipe) while the CRAFT pill's own displayed
breakdown uses `autoPickCraftOption` (the competent one) - a real
display/commit mismatch, but changing WHICH recipe a manual override
commits is a behavioral decision, not a display-only fix, and needs
explicit maintainer sign-off before changing; Plan Notes wording for
MinRating 0 / 3+ disciplines joined by "or" (`PlanViewModelBuilder.cs` -
untouched this round); persisted-plan JSON size (`PlanStoreHelpers.cs` -
untouched this round, still unmeasured); per-render
`PillSubduingEvaluator` allocation (`TreeSectionController.cs`'s
`RenderDecisionPills` - untouched this round); `NonLevelableDisciplineTags`
triplication (still deferred, per round-1).

**Files touched:** `Services/PlanSolver.cs`, `Services/SolverDecision.cs`,
`Services/PillSubduingEvaluator.cs`, `Services/CraftingTreeBuilder.cs`,
`Services/CompetencyOpportunityCalculator.cs`,
`Models/CraftingTreeNode.cs`, `docs/KNOWN-ISSUES.md` (this file).

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
GW2CraftingHelper.csproj -p:Platform=x64` - clean, 0 errors (StyleCop
warnings only, all pre-existing patterns, none new - verified by diffing
warning output for the touched files specifically).

Tests (measured, `"/mnt/c/Program Files/dotnet/dotnet.exe" test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`): 1631
total, 0 failures (up from round-1's 1619 - 12 new tests added this
round, no existing test deleted).

Gate: not yet run live - Blish-bound rendering (Plan Notes rows for the
two newly-reported competency shapes, the now-gated Weighted subduing
tooltip) has not been visually confirmed in a running Blish HUD client,
same constraint every UI-adjacent entry in this file notes.
Gate: not yet run live - scheduled for tonight's 7:15 PM batched desktop session (recipe-sheet savings row and seasonal-tip negative check are explicit scenarios). Merged after the full review pipeline resolved every finding (verification's docs-staleness hold corrected in 30d66de), under the maintainer's standing merge directive (2026-08-16).

## Gate investigation: receipt/what-if captions + value-detail hover (2026-08-16)

Two live gate findings from tonight's batched desktop session against the
`ui-bundle`/`currency-ux-package` features (both entries earlier in this
file). Both were investigated to the deepest reachable, Blish-free seam via
new real-production-path tests; neither investigation found a code defect.

**Item 1 (GATE FAIL as reported): receipt/what-if captions not rendering on
an override-re-solve.** Live repro: Amalgamated Rift Essence plan, root
manually overridden to VENDOR (the "Decisions updated (1 override(s))"
re-solve via `CraftingPlanPipeline.ResolveWithOverrides`), root expanded
shows 4 synthesized cost-component leaves then 4 dimmed reference-branch
children, with no "Vendor price:"/"If crafted instead:" caption or caption
tooltip on either group's first row.

Traced the full chain the live path exercises: `PlanSolver.Solve` ->
`CraftingPlanPipeline.ResolveWithOverrides` ->
`CraftingPlanPipeline.BuildCraftingTreeResult` ->
`CraftingTreeBuilder.BuildTree` (sets `IsReferenceBranch`/`IsCostComponent`
per node) -> `Services/ReceiptCaptionHelper.cs`'s
`ComputeCaptionSplitIndex`/`CaptionForChildIndex` ->
`Views/Rendering/TreeSectionController.cs`'s three render call sites
(the initial default-expanded build inside `RenderTreeNode`, the lazy
expand/collapse toggle handler, and the Expand All button's lazy-build
loop - the report's own "TWO call sites" undercounts; there are three, and
all three correctly compute `captionSplitIndex` from the parent node once
and thread the right child's caption into `RenderTreeNode`'s `captionText`
parameter) -> `UpdateTreeRowTooltip` -> `rowPanel.BasicTooltipText`. Every
step reads correctly on inspection: `CraftingPlanPipeline.
BuildCraftingTreeResult` passes `currencyMetadata`/owned-amount dictionaries
through to `CraftingTreeBuilder.BuildTree` unchanged on the override path
exactly as the initial generation does; `PlanViewModelBuilder.Build` assigns
`vm.TreeRoot = result.CraftingTree` verbatim (no reordering/cloning of
`Children`); `TreeSectionController.CreateTreeSection` receives that same
list unmodified; `ResetContentPanelToEmpty` fully disposes the previous
render's controls before every rebuild (no stale-panel reuse).

Wrote a new real-path test,
`CraftingPlanPipelineTests.MixedVendorOffer_NotBaselineWinner_ResolveWithOverrides_ProducesReferenceBranchWithValidCaptionSplit`
(`tests/GW2CraftingHelper.Tests/Services/CraftingPlanPipelineTests.cs`),
reproducing the exact live shape: a Craft-baseline item whose recipe is
non-empty, manually overridden to `BuyFromVendor` via
`ResolveWithOverrides` against a 2-kind vendor offer (item + currency).
It asserts `resolved.CraftingTree.IsReferenceBranch`, the 2-leaves-then-
1-reference-child `Children` shape (the same stacking
`CraftingTreeBuilder`'s own
`MixedOfferNode_AlsoHasRecipe_StacksComponentLeavesThenReferenceBranch`
test already locks down for the non-override case), and that
`ReceiptCaptionHelper.ComputeCaptionSplitIndex`/`CaptionForChildIndex`
return the expected non-null split and both caption strings on the
resulting node. **This test passes** - the data and helper layer that
ultimately feeds the tooltip is correct for this exact live scenario.

**Item 2 (investigate, fix if real): value-detail hover not firing on a
CRAFT pill above a currency-valued vendor child.** Live repro: Deldrimor
Steel Ingot x5 root CRAFT pill, subtree contains a Philosopher's Stone
`BuyFromVendor` child priced in spirit shards (curated default 3600
copper/unit, `Models/CurrencyDecisionDefaults.cs`) - so the root's
`DecisionValue` was expected to exceed its `SubtreeCost` and
`ValueDetailTooltipBuilder.TryBuild` was expected to fire, but did not.

The sibling test
`PlanSolverCurrencyValuationTests.ComparisonValue_RollsUpThroughAncestorCraft_MatchesDecisionOnlyExpectation`
already proved the raw `SolverDecision.ComparisonValue` rolls up correctly
through `PlanSolver` for this exact shape (a Craft ancestor over a
currency-valued `BuyFromVendor` child). Added a new test one layer
further down the real chain,
`PlanSolverCurrencyValuationTests.CraftRoot_VendorChildValuedInCuratedCurrency_ValueDetailTooltipFires`,
that walks `PlanSolver.Solve` -> `CraftingTreeBuilder.BuildTree` ->
`ValueDetailTooltipBuilder.TryBuild` for a Deldrimor-shaped tree (craft
root, vendor-only child priced purely in spirit shards, valuation supplied
via `CurrencyValuation.WithDefaults(CurrencyValuation.None)` so the
curated default - not a hand-picked test value - is what is exercised,
matching the live report's own wording). **This test passes on the first
attempt**: `root.SubtreeCost == 0`, `root.DecisionValue == 360000` (100
shards at 3600 copper/unit), and `TryBuild` returns true with all three
expected lines ("Crafting gold price:", "Currencies:", "Optimization
price:"). `CraftingTreeBuilder.BuildNode` does copy
`decision.ComparisonValue`/`decision.TotalCost` onto
`CraftingTreeNode.DecisionValue`/`SubtreeCost` unconditionally for every
decision (`Services/CraftingTreeBuilder.cs` lines 185/188), so the
DecisionValue genuinely folds up vendor-child currency valuations all the
way to a Craft root - this is not the gap.

Also read `TreeSectionController.RenderDecisionPills`' own value-detail
wiring (`Views/Rendering/TreeSectionController.cs` ~1462-1483): it gates
on `spec.Kind == Selected || Locked` and
`node.Decision == Craft || BuyFromVendor`, calls
`ValueDetailTooltipBuilder.TryBuild(node, plan?.VendorCapsByItemId, out
valueDetailText)`, and appends the result onto the pill's own
`BasicTooltipText` - structurally correct on inspection, no defect found.

**Conclusion for both items**: no code defect was found in the reachable
chain (solver -> pipeline -> tree builder -> caption/tooltip helper). Both
new tests are real production-path regression coverage for the exact
reported live shapes and pass cleanly. The residual, un-fixed possibility
for both is either (a) tonight's live session ran against a Blish HUD
build that predated the commits under test in this same session (this
file's own `ui-bundle`/`currency-ux-package` entries both note their gate
was "not yet run live" as of the point they were merged, and several
`fill gate line` merge commits landed the same day), or (b) a genuine
Blish-only rendering/tooltip-binding gap in
`Views/Rendering/TreeSectionController.cs` that is outside this repo's
test-runnable boundary (`Blish_HUD.Controls.Panel.BasicTooltipText` and
the mouse-hover binding that reads it cannot be exercised from an xunit
test per this repo's Blish-free test invariant) - the same constraint
every other UI-adjacent entry in this file already notes. If a future live
session reproduces either miss against a confirmed-current build, the next
step is temporary Blish-side instrumentation (a log line in
`TreeSectionController.RenderTreeNode`/`RenderDecisionPills` recording the
computed `captionText`/`valueDetailText` at build time) rather than further
static tracing, since every reachable real-path test now confirms the data
layer is correct.

Tests: 1673 -> 1675 (2 new: `MixedVendorOffer_NotBaselineWinner_
ResolveWithOverrides_ProducesReferenceBranchWithValidCaptionSplit`,
`CraftRoot_VendorChildValuedInCuratedCurrency_ValueDetailTooltipFires`),
via `dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`.
Both new tests exercise real production entry points (`PlanSolver`,
`CraftingPlanPipeline.ResolveWithOverrides`, `CraftingTreeBuilder`,
`ReceiptCaptionHelper`, `ValueDetailTooltipBuilder`) with real
`VendorOfferStore`/`InventoryReducer` where applicable - no Blish HUD
reference, no fake logic, no fake file I/O. Build:
`dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - clean, 0 errors.
No files on the DO-NOT-TOUCH list (`ModuleLog`, `PlanContentHeightMath`,
`PlanRelayoutMath`, scroll machinery, `VendorBatchSolver` merged-ceil
batching) were edited.

Gate: investigation outcome recorded 2026-08-16 - no code defect found; both live-gate anomalies (captions on override-re-solve, value-detail on a valued-vendor-child craft root) reproduce as PASSING real-path tests at every Blish-free seam (2 new tests); residual is Blish-side render binding or an observation artifact - both visuals re-verify at the next desktop session. Merged under the maintainer's standing merge directive (2026-08-16).

## Shopping-list row tooltip: scope collision + swallowed hover (shoplist-have-format, 2026-08-16)

Follow-up review of the shoplist-have-format branch (three own commits:
`7d4cb2a` add raw unclamped currency holding, `fa36cb7` reword the
shopping-row tooltip off the banned "N owned, M needed" phrasing,
`fa1829c` fix the tooltip-currency-lines-dropped-on-resize divergence)
found two Critical/Must-Fix issues in the new wording and its wiring,
fixed in this pass.

**SCOPE COLLISION (Critical).** The reworded wording rendered bare
`HAVE {cc.Amount}/{cc.Amount}` - a per-row (PlanStep) total, never the
whole plan's need for that currency id - with no scope marker, in the
exact vocabulary `DecisionPillPlanner.AppendCurrencyOwnershipPill`
reserves for PLAN-scope facts (`HAVE {have}/{planTotal} TOTAL`). Two
shopping rows drawing on the same wallet currency (e.g. a 700-Karma need
split into 300 + 400 across two vendor rows, wallet holds 500) could
each independently render as fully covered - "HAVE 300/300" and
"HAVE 400/400" - double-counting the one wallet balance, with the new
"(you hold 500)" aside actively reinforcing the false reading by
splicing a wallet-wide figure into a row-scope coverage fraction.
`Services/ShoppingRowTooltipFormatter.cs` now appends a `THIS ROW`
scope marker to both the shortfall and covered lines (mirroring
`AppendCurrencyOwnershipPill`'s own `TOTAL` marker on the plan-scope
pill), and the surplus aside now reads `(wallet N)` instead of "(you
hold N)" - the same "wallet" term the Summary c-table's Have column and
the tree's `HAVE x/y TOTAL` pill already use, so no third phrasing for
the same concept survives. New/updated wording:
`Karma: HAVE 200/500 THIS ROW, NEED 300` (shortfall),
`Spirit Shards: HAVE 500/500 THIS ROW` (covered, no surplus),
`Spirit Shards: HAVE 500/500 THIS ROW (wallet 999999)` (covered,
surplus). A plan-scope fix (threading `PlanViewModel.CurrencyPlanTotals`/
`OwnedCurrencyAmounts` into `ShoppingListSectionRenderer.Render` and
building the line exactly like `AppendCurrencyOwnershipPill` does) was
considered and rejected for this pass: `CurrencyAmountViewModel`
deliberately carries no currency id field at all (`CurrencyDisplayResolver`'s
own doc comment: "the no-displayed-IDs invariant is enforced by
construction here... so a caller cannot accidentally surface a raw
currency id"), so a plan-scope lookup would require adding one - a
larger, cross-file change out of scope for this pass. The row-scope
`THIS ROW` marker fully resolves the misreading (both halves now
honestly say "this row", never implying plan-wide coverage) without
touching that invariant.

**TOOLTIP SWALLOWED BY CHILD CONTROLS (Must Fix).** `BuildTooltip()`
stamped the new HAVE/NEED text on `rowPanel` only; `nameLabel` and the
Total cell's segment controls (`CoinCurrencyRenderer.RenderValueCellRightAligned`'s
labels/icons) have no `BasicTooltipText` of their own and silently
captured the mouse first over most of the row - including over the
Total cell's own currency amount, the one place a user hovering "do I
have enough?" would look. This repo has already root-caused and fixed
this exact class twice (the "Field-test UX wave" finding D and the
ellipsized-currency-name tooltip fix in `SummarySectionRenderer.cs`).
Fixed in `Views/Rendering/ShoppingListSectionRenderer.cs`:
`BuildTooltip()` now also assigns the same string to
`nameLabel.BasicTooltipText` and, via a new `SetValueCellTooltip`
helper, to every control in the Total cell's `ValueCellHandle` (coin
segments, currency segments, or the dash label for an unpriceable row).
`BuildTooltip()`'s definition and initial call were moved to after
`nameLabel`/`totalCell` are constructed so both are in scope for every
rebuild, including the existing `AddReellipsis` resize/settle path -
that rebuild already ran through the same `BuildTooltip()` closure, so
no new divergence risk is introduced.

**Verification split.** The wording change (`ShoppingRowTooltipFormatter.
BuildCurrencyLines`) is Blish-free and fully suite-covered - see
`ShoppingRowTooltipFormatterTests.cs`. The wiring fix
(`ShoppingListSectionRenderer.BuildTooltip`/`SetValueCellTooltip`,
`AddReellipsis` reassigning `BasicTooltipText` on resize/settle, and the
on-screen hover behavior itself - does hovering the name or the Total
cell now actually show the tooltip) is Blish-bound and outside this
repo's test-runnable surface, same constraint every UI-adjacent entry in
this file notes. Not yet confirmed live.

**Sweep note.** Re-ran the required sweep for other `"(N owned, M
needed)"`-style sites (repo rule: fix the class, not the instance):
zero remaining production `.cs` sites - the only `OwnedQuantity`
consumers are `CurrencyDisplayResolver`, `ShoppingRowTooltipFormatter`,
and the model itself; `SummarySectionRenderer` uses a Have/Needed column
table and `DecisionPillPlanner` already uses `HAVE`/`NEEDED`. The four
hits in `docs/dev-notes/HISTORY.md` (lines 745, 1496, 1765, 2615) are
historical field-test transcripts and are deliberately left verbatim,
not updated - recorded here so a future reviewer does not need to
re-derive that exclusion.

**Tests:** `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` - 1682 green (measured, this pass's own
commit), 0 failed. `ShoppingRowTooltipFormatterTests.cs` updated for the
new `THIS ROW`/`wallet` wording; its tautological
`BuildCurrencyLines_NeverMentionsPlanRequires` case (asserted a string
that appears nowhere in the formatter, so it could never fail) was
removed - `BuildCurrencyLines_MultipleCurrencies_OneLinePerCurrencyInOrder`
already pins the full multi-currency line set with exact-string
assertions.

**Build:** `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` -
PASS, 0 errors (pre-existing StyleCop warnings only, none in
edited files).

Gate: not yet run live - queued for the next desktop session. Merged after the full review pipeline resolved every finding, under the maintainer's standing merge directive (2026-08-16).

## Festival-vendor auto-tagging follow-up (2026-08-16)

**Milestone goal:** the previous SEASONAL VENDOR TIP pass (see section
above) hand-tagged exactly three known Candy Corn Vendor (Weekly) ecto
offers with `seasonalFestival`. This pass extends
`tools/VendorOfferUpdater` to DERIVE the tag for any vendor, by parsing
the GW2 Wiki's own `{{Temporary|...}}` template off each vendor NPC
page's wikitext, so seasonal tagging is no longer a hand-curated
one-off.

**What changed:**
- `TemporaryTemplateParser` (new): extracts the `seasonal=` (or, on a
  minority of vendor pages, `event=` - both confirmed live) parameter
  value out of a page's `{{Temporary|...}}`/`{{temporary|...}}` template.
- `Gw2Constants.FestivalKeysByWikiDisplayName` /
  `ResolveSeasonalFestivalKey` (tool copy, `Models/Gw2Constants.cs`):
  curated, MEASURED mapping from the wiki's own festival display-name
  text to the six internal `FestivalContext` name keys
  (`halloween`/`dragonbash`/`wintersday`/`festivalofthefourwinds`/
  `lunarnewyear`/`superadventurefestival`) - both sides independently
  measured, never invented (wiki display names via six live
  `api.php?action=parse&prop=wikitext` fetches, one per festival;
  internal keys via a raw UTF-16LE string scan of `Blish HUD.exe`,
  same technique the runtime `HalloweenFestivalName` constant already
  used for `halloween` alone). A value not in this table (e.g. the
  real, live-confirmed non-festival event vendors "Consortium Trader
  (Fractal Rush)" / "Starter Equipment Vendor") is left untagged with a
  console warning - never guessed.
- `WikiSmwClient.FetchWikitextAsync` (new): fetches a single page's raw
  wikitext via `action=parse&prop=wikitext` (no Semantic MediaWiki
  property exists for a page's `{{Temporary}}` template, unlike every
  other field this tool scrapes).
- `Program.ResolveSeasonalFestivalValuesAsync` / `StripSubobjectSuffix`
  (new, opt-in via `--tag-seasonal-festivals` + `--max-seasonal-pages`):
  a new pipeline pass that fetches each distinct vendor PAGE's wikitext
  (stripping the SMW subobject suffix - `WikiVendorResult.PageName` is
  actually `"Page Title#vendorN"`, live-confirmed, NOT a fetchable page
  title on its own) and caches the raw seasonal/event value by page
  title in a new gitignored dev cache, `ref/seasonal_wikitext_cache.json`.
  Deliberately opt-in, not part of every default run: no SMW property
  means one extra HTTP request per distinct vendor page, which would
  silently balloon the cost/time of the default
  `./tools/refresh-vendor-data.sh` workflow if unioned into every run.
- `ConvertToOffer` (Program.cs): resolves `WikiVendorResult.
  TemporarySeasonalValue` to `VendorOffer.SeasonalFestival` via
  `Gw2Constants.ResolveSeasonalFestivalKey` - deliberately NOT hashed
  into `OfferId` (matches `VendorOffer.SeasonalFestival`'s own existing
  doc comment), so tagging an already-shipped offer never changes its
  identity.

**`ref/vendor_offers.json` regeneration (partial coverage - read
carefully):** `ref/wiki_vendor_cache.json` did not exist on disk in this
worktree (dev-local, gitignored) at the start of this pass, so a full
from-scratch re-scrape was explicitly NOT performed (would be an
unbounded, many-thousand-page live operation, and is not what this
follow-up asked for). Instead, a SCOPED live run
(`--query` restricted to the known festival vendor list +
`--tag-seasonal-festivals` + `--merge-into ref/vendor_offers.json`)
live-tagged the six OTHER known festival vendor NPC pages: Dragon Bash
Merchant (Weekly), Wintersday Trader (Weekly), Festival Rewards Vendor
(Weekly), Gauntlet Ticket Vendor, New Year Vendor, Super Adventure Box
Weekly Trader - 63 wiki rows -> 54 converted offers, reported at the time
as net +2 vs the prior baseline (53536 -> 53538; 52 stale rows for those
6 merchants replaced).

**Correction (2026-08-17 review fix, Critical): that "net +2" figure
CONCEALED a real data-loss bug.** The scoped run's own
`ref/wiki_vendor_cache.json` had 9 rows resolve `GameId 0` (a wiki-query
defect in that pass, not the wiki actually dropping the items - live-
reconfirmed after the fact via `api.php?action=ask` that the wiki still
serves real game ids for every one of them). `Program.cs`'s
`GameId <= 0` filter silently skipped those 9 rows before conversion,
and `MergeIntoBaseline`'s per-merchant WHOLESALE replacement then deleted
the baseline's only copies of the offers those rows would have replaced,
with no fresh row to take their place: Wintersday Trader (Weekly) and
Festival Rewards Vendor (Weekly) each lost `outputItemId` 64736
(Transmutation Charge), 79431 (Chest of Legendary Shards), and 86804
(Tyrian Exchange Voucher) - 6 shipped offers gone. (The same mechanism
also means Super Adventure Box Weekly Trader shipped incomplete - 9
cache rows produced only 6 offers - but that merchant had no prior
baseline row to lose, so nothing was deleted there, only under-added.)
Fixed by (1) restoring the 6 deleted offers byte-for-byte from the
pre-rescrape baseline (merge-base `4735064`) rather than re-guessing
their content, bringing the baseline to 53544 offers, and (2)
`MergeIntoBaseline` now refuses to wholesale-drop a merchant's baseline
rows when this pass's own fresh batch had a `GameId <= 0` row for that
merchant (see its updated doc comment and
`MergeIntoBaselineTests.ProtectedMerchant_*` /
`MixOfProtectedAndUnprotectedMerchants_*`) - a future scoped run with the
same kind of incomplete resolution now keeps the stale baseline rows
(visible, fixable by a follow-up run) instead of silently deleting them.
Also fixed in the same review pass: `Gw2Constants.FestivalDisplayNames`
(`Models/Gw2Constants.cs`) was missing a display-name entry for five of
the six newly-tagged festival keys (only `halloween` was present), so an
active-festival Plan Notes tip for dragonbash/wintersday/
festivalofthefourwinds/lunarnewyear/superadventurefestival would have
rendered the raw internal key verbatim (e.g. "During
superadventurefestival:") - all six now have a MEASURED display name.
Also fixed in this review pass, lower-severity: `WikiVendorResult.
TemporarySeasonalValue`'s doc comment claimed the raw wiki value "still
round-trips through `wiki_vendor_cache.json` for a later run" - false as
written, because `Program.cs` saved that cache BEFORE the seasonal-tag
pass populated the field, so every row in a run's own cache had it as
null; the cache is now re-saved after tagging so the claim is true.
`ResolveSeasonalFestivalValuesAsync` also used to save its wikitext
cache only after its whole fetch loop completed and catch only
`HttpRequestException` per page, so a malformed/non-JSON response or
Ctrl-C anywhere in the loop discarded every page already fetched that
run; it now saves in a `finally` around the loop and treats a JSON parse
failure the same as an HTTP failure (warn, leave that one page
uncached, continue). A stray U+2500 box-drawing comment separator (the
only non-ASCII byte in any file this follow-up touched) was replaced
with ASCII hyphens per the repo's ASCII-only-in-`.cs` rule. Finally, a
module-side test now asserts every distinct `seasonalFestival` value in
the shipped `ref/vendor_offers.json` has a `Gw2Constants.
FestivalDisplayNames` entry and is one of the six known
`FestivalContext` keys, closing the regression-guard gap that let the
display-name bug above ship unnoticed in the first place.

Candy Corn Vendor (Weekly) was deliberately EXCLUDED from this scoped
`--query` (confirmed by first attempting a run that included it, which
recomputed new `OfferId` hashes for all nine of its rows via
`VendorOfferHasher`'s pre-existing, documented "any freshly-touched
merchant gets new OfferIds" behavior - see that file's own M37/Astral
Acclaim doc comment - breaking the "3 known offer IDs survive
identically" requirement; reverted and re-run without it). Its three
original hand-tagged offer IDs, and all nine of its offers, are
confirmed byte-for-byte unchanged (see
`SeasonalFestivalRoundTripTests`, updated this pass). The shipped
baseline now carries `seasonalFestival` on 57 offers across all six
known festivals, up from the original 3 (Halloween only). **This is
still far from full coverage**: the vast majority of vendor pages on
the wiki (thousands, well beyond the seven curated festival-vendor
pages this pass touched) have not been checked for a `{{Temporary}}`
tag at all - a real, non-partial pass requires a full Pass 1 re-scrape
(populating `ref/wiki_vendor_cache.json` from scratch) followed by a
full `--tag-seasonal-festivals` run, which is a many-hour live
operation outside this pass's scope.

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests (`tools/VendorOfferUpdater`
  and its test project reference neither Blish HUD nor Gw2Sharp, same as
  every existing file in that tree).
- [x] Tests exercise real production paths (`TemporaryTemplateParser`,
  `Gw2Constants.ResolveSeasonalFestivalKey`, `Program.ConvertToOffer`,
  and `Program.ResolveSeasonalFestivalValuesAsync` are all exercised
  directly - the latter through a real `WikiSmwClient` against a fake
  `HttpMessageHandler`, matching `WikiSmwClientTests`' own established
  pattern, not a mirrored/fake implementation of the pipeline logic).
- [x] No fake file I/O tests introduced (the cache read/write path is
  exercised through real `File.Exists`/`File.ReadAllText`/
  `File.WriteAllText` calls against real temp files, cleaned up in a
  `finally` block per test).
- [x] Pricing logic preserves multi-source correctness (this pass adds
  no new cost-line/currency logic at all - `SeasonalFestival` is a pure
  metadata tag, untouched cost-resolution code path).
- [x] IDs remain internal-only (no ids surfaced to any UI - this pass is
  entirely inside `tools/VendorOfferUpdater`, a build-time data tool with
  no UI surface).

**Validation performed (measured, this pass's final state):**
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/tools/VendorOfferUpdater/
VendorOfferUpdater.csproj` - 0 errors, 0 warnings.
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/GW2CraftingHelper.csproj -p:Platform=x64`
- 0 errors (only pre-existing StyleCop warnings, none in files this pass
touched). `"/mnt/c/Program Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-festivalscrape/tests/VendorOfferUpdater.Tests/
VendorOfferUpdater.Tests.csproj` - 185 green (26 new: 13
`TemporaryTemplateParserTests`, 5 `SeasonalFestivalMappingTests`, 8
`ResolveSeasonalFestivalValuesAsyncTests`, plus `ConvertToOfferTests`
grew by 9 new `SeasonalFestival`-threading cases; `SeasonalFestivalRoundTripTests`
updated in place for the new 57-tag baseline, not counted as new).
`"/mnt/c/Program Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-festivalscrape/tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` - 1673 green (0 new; one existing pinned
count, `VendorOfferStoreTests.ShippedSeedFile_VendorOfferLoader_
ParsesAllOffers`, updated from 53536 to 53538 to match the regenerated
baseline). Both suites fully green.

**Review-fix pass re-validation (2026-08-17, measured, after the
Critical/Must-Fix corrections above):**
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/tools/VendorOfferUpdater/
VendorOfferUpdater.csproj` - 0 errors, 0 warnings.
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/GW2CraftingHelper.csproj -p:Platform=x64`
- 0 errors (1685 warnings, all pre-existing StyleCop findings, none in
any file this review-fix pass touched - confirmed by grepping the build
output for each touched file's name). `"/mnt/c/Program Files/dotnet/
dotnet.exe" test C:/Dev/Blish/wt-festivalscrape/tests/
VendorOfferUpdater.Tests/VendorOfferUpdater.Tests.csproj` - 191 green (6
new: 3 `MergeIntoBaselineTests` DATA LOSS-guard cases, 2
`ResolveSeasonalFestivalValuesAsyncTests` resilience cases, 1
`TemporaryTemplateParserTests` multi-template case). `"/mnt/c/Program
Files/dotnet/dotnet.exe" test C:/Dev/Blish/wt-festivalscrape/tests/
GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` - 1675 green (2
new: `PlanViewModelBuilderNotesSeasonalVendorTipTests.
ItemCostTip_NonHalloweenFestival_*` and `VendorOfferStoreTests.
ShippedSeedFile_EveryDistinctSeasonalFestivalValue_*`; one existing
pinned count, `ShippedSeedFile_VendorOfferLoader_ParsesAllOffers`,
updated from 53538 to 53544 for the 6 restored offers). Both suites
fully green.

**Risks / follow-ups:**
- Partial coverage, restated: only 7 vendor pages total (the 3
  already-tagged Candy Corn Vendor (Weekly) rows left untouched, plus 6
  freshly live-tagged) have ever been checked for a `{{Temporary}}` tag.
  Every other vendor in the ~53.5k-offer dataset is unswept - a future
  full Pass 1 re-scrape + `--tag-seasonal-festivals` run is needed for
  real coverage.
  **Correction (2026-08-20 fix): the "would need explicit raising"
  framing above is stale.** `--max-seasonal-pages` (default 500 at the
  `Program.cs` CLI level) used to throw `SafetyLimitException` and abort
  the WHOLE run - before fetching anything - the moment the uncached-page
  count exceeded the budget, which a from-scratch sweep of the measured
  ~2,088 distinct vendor pages against an empty (gitignored,
  fresh-clone-absent) `ref/seasonal_wikitext_cache.json` hit
  unconditionally on its very first invocation; `tools/refresh-vendor-
  data.sh` exited 2, Pass 2 never ran, and a re-run made no progress at
  all (same empty cache, same over-budget count, same throw).
  `ResolveSeasonalFestivalValuesAsync`'s budget is now self-healing
  instead: it fetches up to the budget, saves the cache (as before), and
  logs how many pages remain for a subsequent run rather than aborting -
  only a budget `<= 0` still throws `SafetyLimitException`. The script
  also now exposes the budget as `MAX_SEASONAL_PAGES` (matching
  `MAX_RUNTIME`/`MAX_REQUESTS`/`DELAY_PASS1`/`DELAY_PASS2`), defaulted to
  2500 there so the documented one-command full refresh completes the
  seasonal-tag sweep in one run under normal conditions instead of
  needing the limit raised by hand.
- The six wiki-display-name -> internal-key mappings are Halloween,
  Dragon Bash, Wintersday, Festival of the Four Winds, Lunar New Year,
  and Super Adventure Festival ONLY - if Blish HUD's `FestivalContext`
  ever adds a seventh festival, `Gw2Constants.FestivalKeysByWikiDisplayName`
  needs a new MEASURED entry (both a live wiki-page fetch and a
  `Blish HUD.exe` string-heap check) before any vendor for it can be
  tagged; until then any such vendor is silently left untagged with a
  console warning (never guessed) - by design, not a bug.
- `VendorOfferHasher`'s OfferId is NOT stable across a fresh scrape of
  any merchant (pre-existing, documented behavior - see its own
  M37/Astral Acclaim doc comment) - this pass deliberately worked around
  that by excluding Candy Corn Vendor (Weekly) from its scoped query
  rather than fixing the hasher itself (out of scope: it is not this
  pass's task, and a hash-format change would ripple across all ~53.5k
  offers in the shipped dataset).
- **Correction (2026-08-17 review fix, Must Fix): the claim above that
  this pass adds "no new runtime UI-facing code path" is WRONG.** The
  data change alone alters runtime behavior in two measured ways, so a
  live desktop gate is warranted (not optional) before this can be
  considered fully validated:
  - (a) the display-name gap described above (now fixed in this review
    pass) - before the fix, any plan touching an active dragonbash/
    wintersday/festivalofthefourwinds/lunarnewyear/superadventurefestival
    vendor tip would have rendered the raw internal key in the Notes
    section.
  - (b) **Correction (2026-08-18 review fix): the "six items" count below
    was itself measured against the wrong baseline (the post-tagging
    file, not the merge-base one) and undercounted.** Measured against
    merge-base commit `4735064`, **13 items** go from >=1 untagged
    (solver-usable) vendor offer to 0 usable offers, not six: Blood Ruby
    (79280), Petrified Wood (79469), Fresh Winterberry (79899), Jade
    Shard (80332), Fire Orchid Blossom (81127), Orrian Pearl (81706),
    Eitrite Ingot (92317, a real crafting material, not a
    cosmetic/currency item), and 102002, 102175, 104132, 104836, 105086,
    106848. Each is unconditionally removed from the solver by
    `Services/SeasonalOfferFilter`, with no compensating Plan Notes tip
    (their Festival Token cost has no coin price, so
    `SeasonalVendorTipCalculator`'s `TryGetCoinCost` fails for them). A
    further 33 items lose one usable vendor offer but keep at least one
    other. This is arguably the correct policy (an out-of-season festival
    offer should not silently count as always-available), but it is an
    undisclosed PLANNING-BEHAVIOR change for these items, not a purely
    internal data update, and was not stated or gated before now.
    `SeasonalOfferFilter`/`SeasonalVendorTipCalculator` are pre-existing,
    prior-gated code paths, but this pass changed what data flows through
    them.
  - Live desktop verification for (a)/(b) above has not yet been
    performed as part of this review-fix pass either - flagging it here
    so the orchestrator's gate step (below) covers it rather than
    treating this as settled.
- **Per-vendor tag coverage is internally inconsistent for three of the
  seven vendors this pass touched (2026-08-18 review finding).** A
  vendor's own wiki page is checked once for `{{Temporary}}` and its raw
  value applies to every offer that page sells, but tagging is currently
  keyed per-offer, not per-vendor, so a vendor can ship with some rows
  tagged and others not: Candy Corn Vendor (Weekly) 3/9 offers tagged
  (untagged `outputItemId` 103702, 105376, 73430, 64736, 79431, 86804),
  Wintersday Trader (Weekly) 4/7 (untagged 64736, 79431, 86804), Festival
  Rewards Vendor (Weekly) 9/12 (untagged 64736, 79431, 86804) - all
  measured directly against the shipped `ref/vendor_offers.json`. The
  untagged siblings remain solver-visible year-round via
  `SeasonalOfferFilter` despite selling on a festival-only vendor page -
  the same phantom-year-round-vendor failure mode this whole pass exists
  to close, just left open for these 12 rows. Not fixed in this pass
  (would require either a live re-tag run or deriving the tag from a
  per-page rather than per-offer signal - see the `Requirement`-field
  follow-up idea below); recorded here so the partial-coverage caveat
  above is understood to include per-vendor gaps, not just
  dataset-wide ones.
- **Correction (2026-08-18 review fix, Critical): `MergeIntoBaseline`'s
  protected-merchant union path silently discarded the fresh
  `SeasonalFestival` tag it exists to add.** `kept.Concat(fresh)` put the
  baseline row first, so `GroupBy(OfferId).Select(g => g.First())` kept
  the untagged BASELINE row on any OfferId collision - and since
  `SeasonalFestival` is deliberately not hashed into `OfferId` (by
  design, so tagging a shipped offer never changes its identity), a
  protected merchant whose row content was otherwise unchanged collided
  every time, so the feature's whole output was dropped for exactly the
  merchants the protected-merchant guard exists to preserve data for.
  Separately, a protected merchant's baseline row that predates a
  `VendorOfferHasher` hash-format change gets a DIFFERENT `OfferId` for
  content-identical data, so the OfferId-based dedupe alone would ship a
  duplicate tagged+untagged pair. Fixed by concatenating `fresh` first
  (so it wins any OfferId collision) and adding a second, protected-
  merchant-scoped dedupe pass keyed by offer content
  (`Program.ComputeContentKey` - item/count/costs/locations/caps,
  deliberately excluding `SeasonalFestival` itself) that also prefers the
  fresh-tagged row. The prior test asserting this path (`
  MergeIntoBaselineTests.MergedResult_DedupesByOfferId_...`) carried the
  wrong premise in its own comment ("Same OfferId means content-
  identical") and never asserted which copy survived a collision; it now
  asserts the surviving row carries the fresh tag, and a second test
  covers the content-key (different-OfferId) case.
- **Correction (2026-08-18 review fix, Must Fix): a scoped `--query` run
  could hard-abort AFTER doing live scrape work, discarding it.**
  `ResolveSeasonalFestivalValuesAsync`'s fetch budget (and
  `--max-seasonal-pages` safety check) was scoped to the FULL merged
  `wiki_vendor_cache.json` (Step 2's `MergeWikiCache` union), not to the
  pages this run's own `--query` returned - on a real dev-machine cache
  (thousands of distinct vendor pages), a narrow `--query` computed
  thousands of "uncached" pages and threw `SafetyLimitException` before
  Steps 4-6 ever wrote output, even though the run's own live scrape had
  already completed. Fixed by threading the query-scoped result list
  through as a new optional parameter that the fetch budget now checks
  instead (the existing `--resolve-item-currencies-only` path, which has
  no `--query` and intentionally processes the whole cache, is
  unaffected - it passes no scoped list and keeps its old behavior).
- **Correction (2026-08-18 review fix, Must Fix):
  `WikiSmwClient.FetchWikitextAsync` could permanently miscache a page as
  "checked, not tagged."** `action=parse` does not resolve redirects by
  default (unlike `action=ask`'s SMW queries) - a vendor page whose SMW
  subject title is a redirect returned `#REDIRECT [[Target]]` as its
  wikitext, in which the `{{Temporary}}` parser correctly finds no
  template, silently caching a false negative. The same silent-permanent-
  miss happened when the API returned an `error` object (missing/renamed
  page): the method returned null, and the caller cached that identically
  to a real "no template" result. Fixed by adding `&redirects=1` to the
  request, and by having the caller warn and leave a null-wikitext page
  UNCACHED (retried next run) instead of caching it as `""`.
- Nice-to-have fixes in the same review pass: the `README.md` table row
  for `ref/seasonal_wikitext_cache.json` used an em-dash (repo rule bans
  them outside correctly-encoded UI text), corrected to `-`.
  `ResolveSeasonalFestivalValuesAsync`'s cache-apply loop only ever
  ASSIGNED a non-empty cached value and never CLEARED one - combined with
  the Step 3.5 cache re-save, a value that round-tripped in could never
  be un-set even if the wiki later drops the `{{Temporary}}` template;
  now assigned unconditionally (including `"" -> null`).
  `--max-seasonal-pages` is now rejected at parse time if `<= 0` (used to
  make every tagging run throw `SafetyLimitException` with a message that
  read like a data problem). Recorded but not acted on: `WikiVendorResult.
  Requirement` is already populated on every SMW row at zero extra HTTP
  cost and carries the same "the festival [[X]]" signal `{{Temporary}}`
  does (confirmed in `ref/wiki_vendor_cache.json`); deriving the tag from
  it (with `{{Temporary}}` kept as the authority where the two disagree)
  would give dataset-wide coverage with no separate opt-in fetch pass and
  no `--max-seasonal-pages` trap - worth a future pass, out of scope here
  since this pass was explicitly asked for `{{Temporary}}` parsing.
- **Accepted limitation of the 2026-08-20 tag harvest (recorded, not
  fixed): a shipped `seasonalFestival` tag is now sticky, and a vendor
  that genuinely stops being seasonal will not un-tag itself.**
  `MergeIntoBaseline`'s harvest carries a replaced merchant's baseline
  tag onto its untagged fresh rows, and at that point an untagged fresh
  row is indistinguishable from a transiently-missed one: "the wikitext
  fetch failed / the page was left uncached this run" and "the wiki
  genuinely dropped the `{{Temporary}}` template, so this vendor is
  year-round now" both arrive as `SeasonalFestival == null`. The
  cache-apply loop's `"" -> null` clear (2026-08-18 nice-to-have above)
  un-sets the value on the `WikiVendorResult`, but the harvest then puts
  the old tag straight back, so the tag survives until someone edits
  `ref/vendor_offers.json` by hand. Accepted deliberately: a stale
  seasonal tag only makes `SeasonalOfferFilter` EXCLUDE that offer
  outside its festival window, i.e. the solver ignores a source it could
  legally have used (a missed-saving, visible in a plan) - the opposite
  failure, dropping a shipped tag, re-introduces the phantom
  year-round-vendor mispricing this whole feature exists to close, and
  is silent. Same "never silently delete shipped data" trade the
  protected-merchant path already makes. A future pass wanting real
  un-tagging needs a positive "this page was fetched successfully AND
  carries no `{{Temporary}}`" signal threaded from
  `ResolveSeasonalFestivalValuesAsync` into the merge, rather than
  inferring it from a null.
- Nice-to-have (recorded, not fixed) on the 2026-08-20 self-healing
  budget: `toFetch` is deterministic (wiki-result order, uncached pages
  only) and `Take(maxSeasonalPages)` takes its head, so a truncated run
  converges only via the pages it actually CACHES - a failed fetch is
  deliberately left uncached and retried. A block of permanently-failing
  pages at least as large as the budget, sitting at the head of
  `toFetch`, would therefore be re-attempted every run and starve the
  pages behind it; the run-time NOTE's "the remaining count only shrinks
  from here" is accurate for successful fetches, not for that
  pathological shape. Not reachable under the shipped defaults (budget
  2500 vs the measured ~2,088 distinct vendor pages, so the truncation
  branch never runs at all); a future fix would rotate the fetch window
  or track per-page failure counts.

**Review-fix pass re-validation (2026-08-18, measured, after the
`MergeIntoBaseline`/`ResolveSeasonalFestivalValuesAsync`/
`FetchWikitextAsync` corrections above):**
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/tools/VendorOfferUpdater/
VendorOfferUpdater.csproj` - 0 errors, 0 warnings.
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/GW2CraftingHelper.csproj -p:Platform=x64` -
0 errors, 0 warnings. `"/mnt/c/Program Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-festivalscrape/tests/VendorOfferUpdater.Tests/
VendorOfferUpdater.Tests.csproj` - 196 green (5 new: 1
`MergeIntoBaselineTests` content-key case
(`ProtectedMerchant_DedupesByContent_...`), 3
`ResolveSeasonalFestivalValuesAsyncTests` cases (null-wikitext-left-
uncached, query-scoped fetch budget, stale-value-cleared), 1
`WikiSmwClientTests` redirects=1 case; the existing
`MergedResult_DedupesByOfferId_...` test was corrected in place to assert
which row survives a collision rather than only the count, not counted
as new). `"/mnt/c/Program
Files/dotnet/dotnet.exe" test C:/Dev/Blish/wt-festivalscrape/tests/
GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` - 1675 green (0
new; this pass touched no code the module-side suite exercises). Both
suites fully green. `ref/vendor_offers.json` was NOT regenerated by this
review-fix pass (no live wiki run performed) - the `MergeIntoBaseline`/
`ResolveSeasonalFestivalValuesAsync`/`FetchWikitextAsync` fixes above are
verified by unit test only; the per-vendor tag-coverage gap noted above
is documentation-only for the same reason.

**Post-merge re-validation (2026-08-20, measured, on the
`origin/master` merge commit that precedes this entry):**
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/GW2CraftingHelper.csproj -p:Platform=x64`
- 0 errors (pre-existing StyleCop warnings only).
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/tools/VendorOfferUpdater/
VendorOfferUpdater.csproj` - 0 errors, 0 warnings.
`GW2CraftingHelper.Tests` - 1765/1765 green (the 1675 figure recorded
above was measured pre-merge; master added 90 tests of its own).
`VendorOfferUpdater.Tests` - 205/205 green. `ref/vendor_offers.json`
untouched by the merge, 57 `seasonalFestival` tags intact.

Gate: not yet run live - updater-side tooling verified by suite; the 57 seasonal tags and solver exclusion were live-gated 2026-08-16. Merged after the full review pipeline (three verification rounds) resolved every finding, under the maintainer's standing merge directive (2026-08-16).
## Recorded follow-ups batch sweep (2026-08-17)

**Milestone goal:** four small, explicitly-logged non-blocking follow-ups
from recent review rounds, taken now as a batch, plus a sweep of
docs/KNOWN-ISSUES.md's last two days of sections for any other purely
cosmetic (comment/doc/test-only) follow-up bullets cheap enough to take
alongside them.

**What changed:**
1. **Cooldown notice wording (`Services/PlanViewModelBuilder.cs`,
   `AppendDailyCooldownNotices`).** The "(runs in parallel with other
   daily-gated items)" clause rendered unconditionally on every daily
   craft-cooldown notice, even a lone one with no sibling notice to run
   in parallel with. The method now collects qualifying notices first
   and appends the clause only when the plan has 2+ of them.
   Wording corrected on a later follow-up sweep (recorded-followups-
   sweep verification finding): the 2+ count (`pending`) only ever
   counts daily craft-cooldown notices from this loop, never the
   separate Daily-cap vendor notices this same section also emits from
   `Plan.TimegatedItems` - so a plan with exactly one craft-cooldown
   notice running alongside a Daily-cap vendor notice IS genuinely
   running in parallel with another daily-gated item, yet the old
   "daily-gated" wording implied that broader population was what the
   gate measured. The clause text is now "(runs in parallel with other
   daily-crafted items)", naming only the population the count actually
   covers.
   `PlanViewModelBuilderDailyCooldownTests` updated: the existing single-
   notice test now asserts the clause is absent, and a new
   `TwoCraftCooldownNotices_BothAppendParallelClause` test pins it
   present on both rows of a 2-notice plan.
2. **`AcquisitionHintServiceTests.cs` header comment.** Corrected the
   claim that the in-file fixture "mirrors" the real
   `ref/acquisition_hints_seed.json` content - the fixture holds 6 of
   the now-7 seed entries. Reworded to describe the fixture as an
   isolated parsing-shape exercise, pointing drift coverage at the
   separate `Load_ShippedSeedFile_*` test that pins the real file.
3. **`PlanSolveContext.CompetencyIndependentForceBuyNodeIds` persistence
   gap (srcsel verification finding).** `ForceBuyOnlyNodeIds` had a
   dedicated `PlanStoreTests` round-trip test; its sibling
   `CompetencyIndependentForceBuyNodeIds` had none. Added two real
   `PlanStore`/`PlanStoreHelpers` round-trip tests: a populated set
   surviving save+load, and both sets round-tripping correctly as an
   explicit JSON `null` (`OwnMaterialsMode.Free`, pre-pass never ran)
   without `PlanStructuralValidator` rejecting the reload. (The new
   test's own comment initially assumed Newtonsoft omits a null
   property from the written JSON entirely - measured false, the
   project uses no custom `JsonSerializerSettings` so the default
   `NullValueHandling.Include` writes an explicit `null`; the test and
   its comment were corrected to assert that instead before this was
   committed.)
4. **`ForceBuyPrePassResult` doc nuance
   (`Services/OwnedMaterialsForceBuyPrePass.cs`) - direction corrected
   (recorded-followups-sweep verification finding).** The doc comment
   read as if the "competency-blind" raw evaluation was training-
   independent top to bottom. In reality it is competency-blind only at
   the node's OWN recipe choice (picks the cheapest recipe among
   `node.Recipes` regardless of training); each child ingredient's
   contribution to that raw figure still comes from
   `PlanSolver.Evaluate`'s normal competency-RESOLVED recursive call
   (`bestRatingByDiscipline` threaded through), which makes the raw
   craft cost look pricier than a truly training-blind figure would.
   An earlier version of this entry (and the doc comment it described)
   drew the wrong conclusion from that correct premise: membership is
   `buyCost < rawCraftCost * 0.85`, so an INFLATED rawCraftCost only
   makes that test EASIER to satisfy - it can only ADD nodes to
   `CompetencyIndependentForceBuyNodeIds`, never cause a miss. The real
   risk is the opposite of what was originally written: a parent node
   whose own untrained recipe would genuinely survive a true blind
   evaluation can still be pulled into the set by a resolved child's
   inflated cost, suppressing that PARENT's own
   `Decision.CheapestCraftUntrained` - i.e. this can falsely EXCLUDE a
   real training opportunity, not miss an independent one. Corrected the
   doc comment and this entry to state that direction plainly. No
   runtime behavior changed; the code on this branch is unchanged from
   master.

**Sweep of docs/KNOWN-ISSUES.md's last two days of sections (2026-08-15/
16, plus the Festival-vendor entry's own later-dated review-fix notes)
for other pure comment/doc/test "follow-up:"/"nice-to-have" bullets:**
every other open item found is either (a) a real feature/behavior change
deferred out of scope (shopping-list caveat threading, wiki-scrape
auto-detection of new seasonal vendors, per-vendor tag-coverage
incoherence, currency-id pluralization judgment calls, extracting a
shared discipline-tag constant set, `ExcessCraftOutputCalculator`'s
recursion depth, the caret-tooltip sweep across three untouched
renderer files), (b) already resolved/taken in an earlier pass (PART D's
own nice-to-haves, the currency-name-index guard test, the
`DailyCooldownItemService.Load` `ItemId <= 0` guard), (c) an accepted
design tradeoff explicitly not a bug (the currency-valuation snapshot
staleness note, the sticky seasonal-tag limitation), or (d) touches a
DO-NOT-TOUCH file (`PlanContentHeightMath`'s tree-arm caption-row
widening). None qualified as both pure comment/doc/test AND genuinely
cheap beyond the four items already taken above - nothing further was
taken.

**Validation performed:**
- Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
  C:/Dev/Blish/wt-followups/GW2CraftingHelper.csproj -p:Platform=x64` -
  0 errors (1782 pre-existing StyleCop warnings, none in any file this
  pass touched).
- Tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test
  C:/Dev/Blish/wt-followups/tests/GW2CraftingHelper.Tests/
  GW2CraftingHelper.Tests.csproj` - 1768/1768 green (baseline 1765 + 3
  new: `TwoCraftCooldownNotices_BothAppendParallelClause`,
  `Save_Load_CompetencyIndependentForceBuyNodeIds_
  PopulatedSetRoundTrips`, `Save_Load_ForceBuyNodeIdSets_NullInJson_
  DeserializeToNullWithoutValidatorRejection`). One test failed on first
  run (the "absent-in-JSON" assumption above) and was corrected before
  the final green run.
- Manual: `git status --short` confirmed no intermediate cache files
  (`ref/wiki_vendor_cache.json`/`ref/item_id_cache.json`) were touched;
  a full-diff ASCII scan (`grep -P '[^\x00-\x7F]'`) confirmed no non-ASCII
  bytes (and therefore no em-dashes) in any touched file.

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests
- [x] Tests exercise real production paths
- [x] No fake file I/O tests introduced
- [x] Pricing logic preserves multi-source correctness (no cost/pricing
  logic touched at all - wording, a doc comment, and two new persistence
  tests only)
- [x] IDs remain internal-only (not displayed)

**Risks / follow-ups:** none new; the sweep's own "not taken" list above
restates why each remaining candidate stays open.

Gate: not applicable - comment/test/wording cleanup with no visual surface beyond a conditional notice clause (suite-pinned). Merged under the maintainer's standing merge directive (2026-08-16).

## Value-detail hover investigation, pipeline-level follow-up (value-detail-pipeline, 2026-08-17)

Follow-up to "Gate investigation: receipt/what-if captions + value-detail
hover (2026-08-16)" above: that entry's Item 2 traced the value-detail
hover only as far as `PlanSolver.Solve -> CraftingTreeBuilder.BuildTree ->
ValueDetailTooltipBuilder.TryBuild` (the seam test) and found no defect,
but the live miss reproduced again on two separate desktop builds after
that entry was merged - a stronger signal than "stale build", so this
pass went one layer further down: the full `CraftingPlanPipeline.
GenerateStructuredAsync` path the seam test does not model at all (VOM
force-buy pre-pass, `InventoryReducer`, real vendor-offer-store lookups,
`ModuleSettings.GetEffectiveCurrencyValuation()`'s actual return value on
a fresh settings state).

Reproduced a simplified analogue of the live shape end to end (2 levels,
single vendor occurrence - NOT the live tree's actual depth, which
matters for the untested factors listed at the bottom of this entry): a
craft root (Deldrimor Steel Ingot-style, quantity 5) whose recipe has a
vendor-only child priced
purely in spirit shards (currency 23, curated default 3600 copper/unit,
`Models/CurrencyDecisionDefaults.cs` line 109) plus an ordinary TP-priced
sibling, `OwnMaterialsMode.Valued` with a real `AccountSnapshot` owning
some of the SIBLING (not the vendor child - reduction never touches the
node the divergence comes from), through `CraftingPlanPipeline.
GenerateStructuredAsync` itself (fake HTTP fixtures, real
`VendorOfferStore`/`InventoryReducer`, matching this file's established
pattern). New test:
`CraftingPlanPipelineTests.GenerateStructuredAsync_
CraftRootWithVendorChildValuedInCuratedCurrency_VomOn_
ValueDetailTooltipFires`. **Passes on the first run**: `root.SubtreeCost
== 140` (real coin only, the sibling's un-owned 7 units at the InstantBuy
basis' sell price of 20), `root.DecisionValue == 360140` (the same 140
plus the vendor child's 100 shards x 3600 copper/unit), and `TryBuild`
returns true with all three expected lines carrying those exact figures.

Checked the four live factors the prior entry's seam test could not
exercise, all confirmed not to be the gap:
- (a) `ModuleSettings.GetEffectiveCurrencyValuation()` is exactly
  `CurrencyValuation.WithDefaults(GetCurrencyValuation())`
  (`Services/ModuleSettings.cs` line 329); on a fresh/empty persisted
  state `GetCurrencyValuation()` deserializes to `CurrencyValuation.None`
  - byte-for-byte the same valuation the test constructs.
- (b) VOM's force-buy pre-pass, zero-owned guide solve, and
  `InventoryReducer` all ran (`useForceBuyPrePass` requires
  `OwnMaterialsMode.Valued` + a non-null snapshot + a non-null reducer -
  all three supplied).
- (c) `PlanSolver.RecomputeComparisonValues` and the vendor-currency
  reallocation pass both ran as part of the real `Solve()` call inside
  the pipeline (not bypassed) and produced the correct rolled-up
  `DecisionValue`.
- (d) the snapshot's owned quantity sits on the sibling, confirmed not to
  touch the vendor child's own reduction.

Went one step further than the prior entry: also reproduced the case
where the root's pill is genuinely `PillKind.Selected` (2+ options,
craft beating an intentionally-uncompetitive TP price) rather than
`PillKind.Locked` (single option) - the prior entry's seam test left the
root single-option, whose own base tooltip would actually read "Only
available source", not the "Current source: CRAFT" wording the live
report quoted (`Views/Rendering/TreeSectionController.cs`'s
`spec.Kind == PillKind.Selected` branch, line ~1381, is the only site
that produces that exact wording). New test:
`CraftingPlanPipelineTests.GenerateStructuredAsync_
CraftRootSelectedAmongMultipleOptions_ValueDetailTooltipFires` - asserts
`DecisionPillPlanner.BuildPillSpecs` returns a `CRAFT` pill with
`PillKind.Selected` (the same Blish-free data `TreeSectionController`
consumes to pick a render branch) AND that `TryBuild` fires. **Also
passes.** Since the append gate at
`TreeSectionController.RenderDecisionPills` (line ~1490) calls
`ValueDetailTooltipBuilder.TryBuild(node, ...)` on the SAME `node`/`spec`
already established as Selected/Craft in that same loop iteration where
the base "Current source: CRAFT" tooltip was just set two branches
above, and `TryBuild` is a pure function of that node's own fields, a
live miss on this exact wording requires the node reaching line 1490 live
to carry different `SubtreeCost`/`DecisionValue`/
`VendorComponentCostsUnreliable` values than the ones
`GenerateStructuredAsync` produces for THIS shape - which the tests above
rule out only for this shape, not for the live tree.

**Conclusion: correct-by-design for the shape modelled here; no code
defect found in it.** Both tests pass, so the data layer (solver through
`CraftingTreeNode`) is correct for a shallow craft-over-valued-vendor-
child tree at every depth an xunit test can reach.

This does NOT clear the whole pipeline, and the live behaviour has NOT
been verified either way - no live capture was taken during this pass.
Two suppression paths inside `ValueDetailTooltipBuilder.TryBuild` itself
remain untested for a Craft ROOT, and both would produce exactly the
reported symptom:

- **Fallback-tier propagation (the strongest untested candidate).**
  `PlanSolver.RecomputeComparisonValues` (line 2443) sets
  `ComparisonValue = TotalCost` whenever `decision.HasUnvaluedCurrency`,
  and that flag propagates transitively up through every Craft ancestor
  (line 1061). One unvalued currency or `GuildUpgrade` ingredient
  ANYWHERE in the chosen subtree therefore forces `delta == 0` on the
  root and suppresses this hover - the scope limit already documented in
  `ValueDetailTooltipBuilder.cs` lines 26-36. A real Deldrimor Steel
  Ingot tree is far deeper than the 2-level fixture used here and can
  easily contain one. The only existing test on this path
  (`PlanSolverCurrencyValuationTests.
  MixedCoinValuedUnvaluedFallbackOffer_ComparisonValueMatchesTotalCost_
  NoTooltip`) covers a FLAT vendor leaf, never the ancestor rollup.
- **`VendorComponentCostsUnreliable`.** Set by
  `FlagUnreliableVendorComponentCosts` on every occurrence of a vendor
  step merged across 2+ tree occurrences. No test anywhere passes a node
  with this flag true to `TryBuild`. It lands on vendor nodes rather than
  Craft ancestors, so it is the weaker candidate for a root-pill miss,
  but it is untested.

Next step if a third live repro occurs: rule out fallback-tier
propagation FIRST (a test with an unvalued-currency ingredient buried
under the craft root, asserting whether the root hover survives), since
that is a cheap Blish-free test and a genuine code-level explanation.
Only if that comes back clean is Blish-side instrumentation warranted - a
log line in `TreeSectionController.RenderDecisionPills` at line ~1490
recording `node.ItemId`, `node.Decision`, `node.SubtreeCost`, `node.
DecisionValue`, `node.VendorComponentCostsUnreliable`, and the `TryBuild`
return value at the moment of the live render.

Tests: 1768 -> 1770 (2 new:
`GenerateStructuredAsync_CraftRootWithVendorChildValuedInCuratedCurrency_
VomOn_ValueDetailTooltipFires`,
`GenerateStructuredAsync_CraftRootSelectedAmongMultipleOptions_
ValueDetailTooltipFires`), via `"/mnt/c/Program Files/dotnet/dotnet.exe"
test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`. Both
new tests exercise real production entry points
(`CraftingPlanPipeline.GenerateStructuredAsync`, real `VendorOfferStore`/
`InventoryReducer`, `DecisionPillPlanner`, `ValueDetailTooltipBuilder`) -
no Blish HUD reference, no fake logic, no fake file I/O. Build:
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-valuedetail/GW2CraftingHelper.csproj -p:Platform=x64` -
clean, 0 errors (pre-existing StyleCop warnings only, none in either
touched file). No files on the DO-NOT-TOUCH list (`ModuleLog`,
`PlanContentHeightMath`, `PlanRelayoutMath`, scroll machinery,
`VendorBatchSolver` merged-ceil batching) were edited - only a test file
and this doc.

Gate: [PENDING - the orchestrator fills in PASS/FAIL]
