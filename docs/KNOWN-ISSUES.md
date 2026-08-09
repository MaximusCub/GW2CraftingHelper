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
Round-trip reduction: `Gw2AccountSnapshotService`'s per-character fetch
switched from the narrow `V2.Characters[name].Inventory.GetAsync` call to
the fuller `V2.Characters[name].GetAsync`, whose `.Bags` is byte-identical
in shape to the narrow endpoint's own `.Bags` (confirmed via Gw2Sharp
1.7.4 reflection) and additionally carries `.Crafting` - one round trip
per character now captures both signals instead of two, with no new
permission requirement (Crafting only needs the already-required
`account`/`characters` scopes). Inventory and crafting share the one
per-character try/catch, so a single character's fetch failing degrades
both signals for that character exactly like the pre-W3C code degraded
inventory alone - it still never fails the whole snapshot (the outer
character-LIST fetch failing is the only thing that does, unchanged).
Every learned discipline is captured regardless of `Active` (GW2 only
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
never a reposition, is needed when the panel is resized.

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

Validation: `dotnet build -p:Platform=x64` clean (0 errors); module test
suite green - 1199 passed (was 1191 at the base commit; +8 new tests,
all listed in item 3 above). No new Blish HUD references in tests; every
new test exercises real production code (`SnapshotStore`, `SnapshotHelpers`,
`PlanViewModelBuilder`) with no contract-mirror/fake-logic tests. Item/
currency/vendor IDs remain internal-only - only character names and
discipline names (both already user-facing concepts) appear in the new
`CharacterAvailabilityText` display strings.

Live desktop gate: [PENDING - the orchestrator fills in PASS/FAIL]
