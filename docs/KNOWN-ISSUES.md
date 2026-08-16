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

Gate: [PENDING - the orchestrator fills in PASS/FAIL]
