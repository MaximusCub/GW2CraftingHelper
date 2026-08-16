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

Orchestrator-checksummed audit finding (confirmed via live API): the
versioned GW2 API returns ingredient `{type:"GuildUpgrade", id:<upgradeId>,
count:N}` on Guild Decoration recipes (e.g. recipe 12002 -> item 80471,
guild upgrade id 829, ~70 occurrences in a 600-recipe sample, 678 across the
full current seed). The "Recipe-ingestion bug class" entry above first
surfaced this as a cosmetic display gap; this audit found it was actually
a real mis-costing bug too - see that entry's own RESOLVED note.

**Root cause (two independent sites):**

1. `Services/CraftingTreeBuilder.cs`'s non-`"Item"` branch labeled ANY
   non-Item ingredient `CraftingDecision.Currency` and resolved a
   **currency** name from the id via `Gw2Constants.ResolveCurrencyName` -
   wrong domain (a guild upgrade id and a wallet currency id are distinct
   id spaces with no defined relationship to each other; resolving one as
   if it were the other on the strength of a numeric match would silently
   show the wrong name on any collision).
2. `Services/PlanSolver.cs`'s ingredient loop (`Evaluate`) special-cased
   only `IngredientType == "Currency"`, so a `GuildUpgrade` ingredient fell
   through and was evaluated exactly like a normal item node. **Correction
   to this entry's own first draft of this root cause:** the TP
   item-pricing path is not actually reachable this way -
   `CraftingPlanPipeline.CollectItemIds` only ever collects `"Item"`-typed
   ids into the set that becomes `context.Prices`, so `GetBuyCost`'s
   TP-price-table lookup could never contain a `GuildUpgrade` id in
   production. The path that WAS reachable: `Evaluate` also calls
   `VendorBatchSolver.EvaluateVendorOffers` against the full `vendorOffers`
   dataset (53,536 offers in the current seed), keyed directly by the
   ingredient's raw id (`vendorOffers.TryGetValue(node.Id, ...)`, matched
   against each offer's `outputItemId`) with no `"Item"`-type gate anywhere
   in that method - so a `GuildUpgrade` id that happened to also be a real
   vendor offer's `outputItemId` would be priced off that unrelated offer
   and could win as a genuine `BuyFromVendor` `PlanStep`, silently
   inflating (or deflating) the containing recipe's real coin total.
   **Severity: latent-but-reachable-via-vendor-offers, not cosmetic** -
   measured against `ref/recipes_seed.json`: none of the 225 distinct
   `GuildUpgrade` ingredient ids collide with a vendor offer
   `outputItemId`, a seed `Item` ingredient/output id, or a
   `KnownCurrencyNames` key, so the mis-cost was never realized in the
   current seed - but nothing in the pre-fix code enforced that absence of
   collision, so it was one seed update away from firing. (The defects
   that WERE realized pre-fix were the CURRENCY mislabel from site 1 above
   and the missing fallback-tier demotion - see "Fix" below.)

**Fix (matches the approved direction exactly):**

- `Models/CraftingDecision.cs` gained a new `GuildUpgrade` member,
  deliberately **appended last** (after `Unknown`, not inserted earlier) -
  this enum has no `StringEnumConverter` and round-trips through
  `PersistedPlan`/`plan.json` as a raw ordinal int, so inserting it anywhere
  earlier would have silently reassigned `Unknown`'s on-disk integer and
  misread every previously-persisted plan on load. Caught during this
  fix's own Code Reviewer Mode self-review pass, not by a test.
- `CraftingTreeBuilder.BuildNode` gained an explicit `"GuildUpgrade"` branch
  (checked before the generic non-`"Item"` branch): sets
  `Decision = CraftingDecision.GuildUpgrade`, a generic ID-free display name
  (`"Guild upgrade (unresolved)"` - IDs are never displayed, repo
  invariant), and an acquisition-hint-style `AcquisitionHint` explanation.
  Never calls `Gw2Constants.ResolveCurrencyName`/`CurrencyDisplayResolver`
  and never consults `currencyMetadata` - confirmed via a dedicated
  id-collision test (`GuildUpgradeNode_NeverResolvesViaCurrencyMetadata_
  EvenWhenIdCollides`). Review-fix: the branch also now explicitly clears
  `IconUrl`/`Rarity` (both already populated above, keyed on the raw
  ingredient id, before this branch runs). The original draft of this fix
  left them alone on the reasoning that no live metadata source exists for
  a guild-upgrade id - `CollectTreeItemIds` only ever fetches `ItemMetadata`
  for `"Item"`-typed ids - but the `metadata` dict handed to this builder is
  populated from more than `CollectTreeItemIds` alone
  (`CraftingPlanPipeline`'s `metadataIds` also unions step item ids, the
  target item id, used-material ids, and vendor cost-component ids, none of
  which that guard constrains), so a same-numbered genuine item entry
  reaching `metadata` by one of those other routes could not actually be
  ruled out by code inspection. Explicitly nulling both fields makes the
  no-wrong-domain-icon/rarity invariant true by construction instead of by
  the current seed's luck (measured: no seed `GuildUpgrade` id collides
  with a seed `Item` id today, but nothing enforced that).
- `PlanSolver.Evaluate`'s ingredient loop gained an explicit `"GuildUpgrade"`
  branch (checked before the `"Currency"` branch): unconditionally sets
  `hasUnvaluedCurrency = true` and contributes zero to both `craftCost` and
  `craftRealCost` - **never** attempting a `currencyValuation` lookup on the
  ingredient's id, even when the user's `CurrencyValuation` happens to hold
  an entry for that exact numeric id (the same cross-domain collision risk
  as the display side). This reuses the EXACT SAME fallback-tier machinery
  an unvalued real `Currency` ingredient already drives (`Decision.
  HasUnvaluedCurrency`, the comparable/fallback recipe-tier split, and its
  existing transitive propagation up through Craft ancestors) rather than
  inventing a parallel mechanism - per instruction, and verified by a
  dedicated nested-Craft-ancestor test
  (`GuildUpgradeIngredient_TransitivelyDemotesAncestorCraft_
  TpWinsDespiteCheaperRealCraftCost`) that the existing propagation code
  needed zero changes to cover the new ingredient type correctly.
- `PlanSolver.Collect` gained an explicit `"GuildUpgrade"` branch (checked
  before `"Currency"`): returns immediately without touching `currencyMap`
  - a `GuildUpgrade` ingredient must never surface in `plan.CurrencyCosts`,
  the Summary currency table, or any wallet lookup keyed off that table
  (confirmed empty in every new test). It also never generates a shopping/
  craft-step `PlanStep` (no memo entry ever exists for it, since
  `Evaluate`'s ingredient loop never calls `Evaluate` on it directly).
- `PlanSolver.RecomputeCraftCosts` gained matching `"GuildUpgrade"` guards
  (top-of-method + ingredient loop) for consistency with the other three
  sites, though the top-of-method guard is defense-in-depth only given the
  current call graph (a `GuildUpgrade` node never has a memo entry to
  recompute from).
- `DecisionPillPlanner.BuildPillSpecs` gained a distinct single, non-
  interactive `"GUILD UPGRADE"` locked pill (mirrors the `CURRENCY` pill's
  shape, reuses `PillKind.Locked` - no new pill color/kind needed).
- `Views/Rendering/TreeSectionController.cs` (Blish-bound, untestable
  directly) widened two existing `Decision == Unknown` tooltip branches to
  also cover `Decision == GuildUpgrade`, so the `AcquisitionHint`
  explanation the tree builder now always sets actually renders (both the
  row tooltip and the pill tooltip) instead of silently falling to the
  generic "Only available source" text a locked pill gets by default.

**Sweep (repo rule: fix the class, not the instance):** grepped every
`IngredientType`/`"Currency"`/`"Item"` comparison in `Services/`/`Models/`
(`AchievementBitDedupPrePass`, `CraftingPlanPipeline`'s override/id-
collection helpers, `InventoryReducer`, `RecipeService.BuildNodeAsync`'s
leaf-handling guard) - every one of them is already `"Item"`-gated
(`== "Item"` or `!= "Item"`), so a `"GuildUpgrade"` node was already routed
correctly as a non-consumable, non-recipe-expanding leaf at every one of
those sites with zero changes needed. `Services/VendorBatchSolver.cs` and
`Services/CraftingPlanPipeline.cs`'s vendor-offer-currency-id collectors
compare a DIFFERENT field (`VendorOffer.CostLines[].Type`, the vendor-offer
data model) that has no `"GuildUpgrade"` concept at all - confirmed
unrelated, not touched.

**Tests (11 net new, real production code paths, no Blish references):**
`PlanSolverGuildUpgradeTests.cs` (new file, 5 tests: never-priced-as-item,
never-priced-as-currency-even-with-a-colliding-valuation-id, never-in-
CurrencyCosts, transitive fallback-tier propagation through a Craft
ancestor, and a degenerate GuildUpgrade-only recipe still crafts at zero
cost); `CraftingTreeBuilderTests.cs` (+3: decision/name/hint shape, never
resolves via currencyMetadata even on an id collision, Ignore-toggle
scoping parity with the Currency case); `DecisionPillPlannerTests.cs` (+2:
pill shape, no Ignore/OwnedInfo pills); `AmalgamatedRiftEssenceIngestionTests.cs`
(the existing GuildUpgrade test rewritten in place to assert the fixed
behavior instead of documenting the old gap as intentional, plus 1 new test
using the exact real recipe 12002 -> item 80471 shape from
`ref/recipes_seed.json`, the audit's own reference case).

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - PASS (0
errors, only pre-existing StyleCop warnings, none on touched lines). Tests:
`dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` -
1380 total (1369 baseline + 11 net new) - PASS, 0 failed.

**Self-review findings (Code Reviewer Mode, fixed before commit):** the
`CraftingDecision` enum-ordinal/persistence risk above (Must Fix, fixed);
a stale `CraftingTreeNode.AcquisitionHint` doc comment that undersold its
now-two population sources (Nice to Have, fixed); a missing transitive-
propagation regression test for a nested `GuildUpgrade` descendant (Nice
to Have widened to Must Fix given the audit's explicit "be consistent with
that machinery" instruction - added). No Critical findings. Out of scope,
confirmed unaffected and left untouched per the milestone's DO-NOT-TOUCH
list: `Services/ModuleLog.cs`, `Services/PlanContentHeightMath.cs` (a
`GuildUpgrade` node's `Decision != CraftingDecision.Craft` already dims its
subtree exactly like `Currency`/`Have`/`Unknown` do today, with zero code
change needed there), `Services/PlanRelayoutMath.cs`, scroll machinery,
`VendorBatchSolver`'s merged-ceil batching math.

**Remaining / deferred (adversarial review addition, 2026-08-16):** this
fix makes a GuildUpgrade ingredient safe to price and display - never
mis-costed, never mislabeled as a wallet currency - but does not resolve
what the ingredient actually IS. Two pieces of the original "Recipe-
ingestion bug class" out-of-scope finding above remain unimplemented: (1)
the upgrade's real name - the leaf still renders the generic, ID-free
"Guild upgrade (unresolved)" label rather than the actual guild upgrade
name (would need a GW2 API `/v2/guildupgrades` lookup, a new metadata
service, and a name cache, none of which exist yet); (2) verifying the
active character's claimed guild actually owns/has-unlocked that upgrade
(would need the `/v2/guild/:id/upgrades` endpoint plus a guild-membership/
permission check this module has no concept of today). `Models/
CraftingDecision.cs`'s `GuildUpgrade` doc comment, `CraftingTreeBuilder.
BuildNode`'s `"GuildUpgrade"` branch, and the branch's own
`AcquisitionHint` text all point back to this document for this
remainder - this bullet is that pointer's target. Left for a future
milestone, same as the original finding.

## GuildUpgrade ingredient costing/display fix - adversarial review follow-up (2026-08-16)

A second, adversarial pass (Code Reviewer Mode, per this repo's mandatory
Edit -> Review -> Fix loop) over the fix above found three defects: one
false empirical claim used as the fix's own stated justification (repeated
at four production sites plus two test-comment sites), one wrong mechanism
named for the real mis-costing bug this document itself records, and one
gap the fix's reasoning left open for icon/rarity instead of name. None
required touching DO-NOT-TOUCH code (`Services/ModuleLog.cs`, `Services/
PlanContentHeightMath.cs`, `Services/PlanRelayoutMath.cs`, scroll
machinery, `VendorBatchSolver`'s merged-ceil batching math).

**Fixed (mustFix): the fix's own stated justification - a claimed numeric
overlap between GuildUpgrade ids and `KnownCurrencyNames`' keys - was
false, and cited a doc comment that says nothing about guild upgrades.**
The claim ("the two id spaces numerically overlap in the real seed -
several GuildUpgrade ids, e.g. 73-87, fall inside `KnownCurrencyNames`'
own 2-80 range") appeared in `Models/CraftingDecision.cs`, `Services/
CraftingTreeBuilder.cs`, `Services/PlanSolver.cs`, this document's own
"Root cause" item 1 above, and comments in
`tests/GW2CraftingHelper.Tests/Services/CraftingTreeBuilderTests.cs`.
Independently re-measured (not just re-checked against the finding) with
a script over `ref/recipes_seed.json` and `Models/Gw2Constants.cs`: 678
GuildUpgrade ingredient occurrences over 225 distinct ids, min 73 / max
1309; exactly one id (73) is `<= 80`, and 84/85/86/87 are all present but
all `> 80`, outside the claimed 2-80 span; `KnownCurrencyNames` has 44
keys total (2-7, 9-16, 18-20, 22-30, 32-34, 36, 45, 47, 49, 50, 58-63, 65,
78-80) and the intersection with the 225 GuildUpgrade ids is empty. The
cited doc comment (`Models/Gw2Constants.cs` ~62-88) is entirely about the
2026-08-15 currency-name-drift audit and never mentions guild upgrades.
The fix is correct on domain grounds alone and needed no invented overlap
to justify it - every site now states the real reasoning instead: a guild
upgrade id and a wallet currency id are distinct id spaces with no
defined relationship to each other, so resolving one as the other on the
strength of a numeric match would risk silently showing the wrong name,
price, icon, or rarity on any collision.

**Fixed (mustFix): this document's own "Root cause" item 2 above (and the
"RESOLVED" note it overrode on the original "Recipe-ingestion bug class"
finding) named the wrong mechanism for the real mis-costing bug.** Both
stated the ingredient "fell through to the item-pricing path (`GetBuyCost`
looked up its numeric id in the TP price table)". That path is
unreachable in production: `CraftingPlanPipeline.CollectItemIds` only
ever collects `"Item"`-typed ids, so `context.Prices` can never carry a
`GuildUpgrade` id there. The path that WAS reachable: `PlanSolver.
Evaluate`'s ingredient loop falls through to a recursive `Evaluate` call
on the ingredient node for any type it does not special-case, which
reaches `VendorBatchSolver.EvaluateVendorOffers` - keyed by the raw
ingredient id (`vendorOffers.TryGetValue(node.Id, ...)`) against
`outputItemId`, with no `"Item"`-type gate anywhere in that method - so a
colliding vendor-offer `outputItemId` could have produced a real,
wrongly-priced `BuyFromVendor` step pre-fix. Independently re-measured:
none of the 225 seed `GuildUpgrade` ids collide with a seed `Item`
ingredient/output id (16,024 distinct), a vendor offer `outputItemId`
(14,959 distinct across 53,536 offers), or a `KnownCurrencyNames` key -
so this was latent, not realized, on this branch; the realized defects
were the CURRENCY mislabel and the missing fallback-tier demotion.
Severity corrected to latent-but-reachable-via-vendor-offers (not
cosmetic) in both places this document stated the wrong mechanism. Added
a dedicated regression test that seeds a colliding vendor offer directly
(`GuildUpgradeIngredient_NeverPricedAsVendorOffer_EvenWhenVendorOfferExistsForSameId`
in `PlanSolverGuildUpgradeTests.cs`) - the previous suite exercised only
the (unreachable) TP-price collision, never the vendor-offer one that was
actually live.

**Fixed (mustFix): the same wrong-domain leak the fix eliminated for the
display Name was left open for IconUrl/Rarity.**
`CraftingTreeBuilder.BuildNode` sets `IconUrl`/`Rarity` from the
`metadata` dictionary keyed on the raw ingredient id for every node
(including the GuildUpgrade one) before the GuildUpgrade branch runs, and
that branch overrode only `Name`. `Views/Rendering/
TreeSectionController.cs` feeds `Rarity` to the icon frame color and the
row's name color, and renders `IconUrl` directly, so a numeric collision
would show an unrelated item's icon and rarity-colored name under the
"Guild upgrade (unresolved)" label - the same class of bug this fix
exists to eliminate, just for icon/rarity instead of name. The branch's
own comment argued this was safe because "`metadata` never carries a
genuine entry for this id", citing `CollectTreeItemIds`'s `"Item"`-only
fetch guard - true of that one contributor, but `CraftingPlanPipeline`'s
`metadataIds` also unions step item ids, the target item id, used-material
ids, and vendor cost-component ids, none of which that guard constrains,
so the claim was never actually enforced anywhere (unrealized today only
because no seed `GuildUpgrade` id happens to collide with a seed `Item`
id). Fixed by explicitly setting `IconUrl = null; Rarity = null;` in the
branch, making the invariant true by construction instead of by the
current seed's luck. Covered by a new test that seeds a colliding
`ItemMetadata` entry directly
(`GuildUpgradeNode_NeverResolvesIconOrRarityViaItemMetadata_EvenWhenIdCollides`
in `CraftingTreeBuilderTests.cs`).

**Tests**: 2 new regression tests (the vendor-offer-collision test in
`PlanSolverGuildUpgradeTests.cs`; the IconUrl/Rarity metadata-collision
test in `CraftingTreeBuilderTests.cs`), for 1382 total (1380 from the
base pass + 2). No new test file, `.csproj` unchanged. The false-claim
corrections in both files' doc comments do not change any assertion's
behavior.

Validation: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` -
PASS (0 errors, only pre-existing StyleCop warnings, none on touched
lines). Tests: `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` - 1382 total (1380 baseline + 2 net new)
- PASS, 0 failed. No new Blish HUD references in tests; both new tests
exercise real production code paths (`PlanSolver.Solve`,
`CraftingTreeBuilder.BuildTree` end-to-end, real `RecipeNode`/
`VendorOffer`/`ItemMetadata` fixtures), no contract-mirror/fake-logic
tests. Item/currency/vendor IDs remain internal-only (the GuildUpgrade id
is never displayed by either new test's assertions, matching every
existing test in these files). Pricing logic continues to preserve
multiple sources and avoid inventing currency exchange rates.

**Out of scope, not touched:** the same false overlap claim also appears
verbatim in commit `8ed76e6`'s own commit message (this milestone's first
commit) - rewriting published commit history was not requested by this
task and this repo's workflow avoids amend/rebase by default; it may also
be recorded in this project's orchestrator-side session memory/journal
logs outside the repository, which are conversation records rather than
project source and are out of this fix's scope.

## GuildUpgrade ingredient costing/display fix - Currency sibling icon/rarity leak (2026-08-16)

A third adversarial pass over the fix above found that the icon/rarity
leak closed for the `GuildUpgrade` branch in the follow-up pass immediately
above was left wide open one branch below it, in the same method, for the
`Currency` branch - an incomplete class sweep (repo rule: fix the class,
not the instance). No DO-NOT-TOUCH code was touched (`Services/
ModuleLog.cs`, `Services/PlanContentHeightMath.cs`, `Services/
PlanRelayoutMath.cs`, scroll machinery, `VendorBatchSolver`'s merged-ceil
batching math).

**Fixed (mustFix): `CraftingTreeBuilder.BuildNode`'s Currency branch set
only `Name`, leaving `IconUrl`/`Rarity` at whatever the generic
item-keyed `ResolveIcon`/`ResolveRarity` lookup (against `metadata`,
keyed on the raw ingredient id) had already populated them with above.**
A wallet currency id is the same distinct-id-space situation as a guild
upgrade id: no defined relationship to item ids, so a numeric collision is
possible and not merely hypothetical - `ref/vendor_offers.json` contains a
vendor offer with `outputItemId: 24`, and 24 is also a
`KnownCurrencyNames` key ("Pristine Fractal Relics"). Any plan whose steps
buy item 24 unions 24 into `CraftingPlanPipeline`'s `metadataIds` (via
`plan.Steps.Select(s => s.ItemId)`), so `metadata[24]` becomes that
Wintersday item's `ItemMetadata` and a Pristine Fractal Relics currency
leaf elsewhere in the same tree would render that item's icon and
rarity-colored name (`Views/Rendering/TreeSectionController.cs` feeds
`node.Rarity` to `RarityColors.GetRarityBorderColor`/
`GetRarityNameColor` and renders `node.IconUrl` directly, for every node
including Currency ones). Fixed by resolving `IconUrl` through
`CurrencyDisplayResolver.ResolveIconUrl` (currency-domain, keyed on
`currencyMetadata`, null when no live metadata has an icon for this id -
never a guess) instead of the item-keyed result, and explicitly clearing
`Rarity` to null - currencies have no rarity concept at all, matching
`BuildVendorCostComponentLeaves`'s own currency-component leaves just
below in the same file, which already never set `Rarity` for a currency
leaf.

**Sweep (repo rule: fix the class, not the instance):** re-read all of
`BuildNode`'s early-return branches (Have/Ignore/GuildUpgrade/Currency/
Unknown) and `BuildVendorCostComponentLeaves`'s two leaf kinds for the
same item-metadata-onto-non-item-id pattern. The Have/Ignore/Unknown
branches and the vendor-item-cost-component leaves all resolve icon/
rarity for a genuine `"Item"`-typed id (a real item id in every case),
so the generic `ResolveIcon`/`ResolveRarity` lookup is correct there and
was left unchanged. The vendor-currency-cost-component leaves already
used `CurrencyDisplayResolver.ResolveIconUrl` and never set `Rarity`
(the exact shape this fix now gives the plain Currency branch too). No
further sibling leaks found.

**Tests (2 new, real production code paths, no Blish references):**
`CurrencyNode_NeverResolvesIconOrRarityViaItemMetadata_EvenWhenIdCollides`
(mirrors `GuildUpgradeNode_NeverResolvesIconOrRarityViaItemMetadata_
EvenWhenIdCollides`, seeding a colliding `ItemMetadata` entry for id 24
directly and asserting both `IconUrl` and `Rarity` stay null) and
`CurrencyNode_IconResolvedFromCurrencyMetadata_NotItemMetadata` (positive
case: `IconUrl` now comes from `currencyMetadata`, not the item-keyed
lookup). Both were verified to fail (not merely differ) against the
pre-fix code before the fix was applied, confirming they actually
exercise the regression. `.csproj` unchanged (no new files); no Blish HUD
references; both tests call `CraftingTreeBuilder.BuildTree` directly with
real `RecipeNode`/`ItemMetadata`/`CurrencyMetadata` fixtures, no
contract-mirror/fake-logic tests. Item/currency ids remain internal-only
(never asserted into a displayed string). Pricing logic is untouched;
this fix is display-only (icon/rarity), matching the class of bug it
closes.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - PASS (0
errors, only pre-existing StyleCop warnings, none on
`Services/CraftingTreeBuilder.cs` or the touched test file). Tests:
`dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`
- 1384 total (1382 baseline + 2 net new) - PASS, 0 failed.

**Self-review findings (Code Reviewer Mode, fixed before commit):** none
Critical or Must Fix beyond the finding itself. Nice to have, not applied
(scope discipline - the finding was narrowly about `CraftingTreeBuilder`):
`Services/PlanViewModelBuilder.cs` has several other `IconUrl = iconUrl`
assignments that were grepped and spot-checked as part of the sweep above;
none showed the same item-metadata-keyed-onto-a-non-item-id shape (each
resolves icon for a genuine item context), so none were changed - flagging
here rather than silently expanding scope.

## GuildUpgrade ingredient costing/display fix - residual class-gap follow-up (2026-08-16)

A fourth adversarial pass (external orchestrator review of the three
sections above) found three defects: the earlier "GuildUpgrade" fixes
were still instance-scoped rather than class-scoped at their own root
cause, this document's Gate-line contract was broken by accumulating
three independently-pending Gate lines instead of one, and the RESOLVED
marker on the original deferred finding overclaimed what was actually
fixed. No DO-NOT-TOUCH code was touched (`Services/ModuleLog.cs`,
`Services/PlanContentHeightMath.cs`, `Services/PlanRelayoutMath.cs`,
scroll machinery, `VendorBatchSolver`'s merged-ceil batching math).

**Fixed (Critical - repo rule "fix the class, not the instance"): every
prior guard in this saga was scoped to the literal string
`"GuildUpgrade"`, leaving the wrong-domain catch-all that caused the
original bug in place for any OTHER non-Item ingredient type.**
`PlanSolver.Evaluate`'s top guard and ingredient loop, `PlanSolver.
Collect`'s top guard, and `PlanSolver.RecomputeCraftCosts`' top guard and
ingredient loop all special-cased `"Currency"` and `"GuildUpgrade"`
explicitly and let ANY other type fall through - in `Evaluate`'s
ingredient loop specifically, straight into a recursive `Evaluate` call
that reaches `GetBuyCost` and `VendorBatchSolver.EvaluateVendorOffers`
(which keys `vendorOffers` by the raw ingredient id with no type gate at
all) - the exact reachable mis-costing mechanism this document's own
"Root cause" write-up above identifies as the real bug, just not closed
for the general case. `CraftingTreeBuilder.BuildNode` had the same shape
in the display direction: its non-`"GuildUpgrade"` branch read
`IngredientType != "Item"`, so any unrecognized type was labeled
`CraftingDecision.Currency` and named via `CurrencyDisplayResolver` -
the wrong-domain mislabel this whole fix exists to eliminate, reintroduced
one catch-all wider. Verified end-to-end that an unknown type reaches
these sites: `Gw2RecipeApiClient` passes the API's `type` string through
verbatim with no allow-list, and `RecipeService` makes any non-`"Item"`
type a leaf with no further gating - so a future GW2 API ingredient type
this module has never seen (not just "GuildUpgrade") would have silently
reached the same mis-costing/mislabeling paths.

Fixed by inverting every guard to be `"Item"`-positive instead of an
enumerated deny-list, per the minimal, no-new-abstraction direction: in
`PlanSolver`, `Evaluate`'s top guard is now `IngredientType != "Item"` ->
unpriceable (was `== "Currency" || == "GuildUpgrade"`); its ingredient
loop now checks `"Currency"` first (the one type with real, valuation-
aware pricing logic), then treats ANY other non-`"Item"` type via the
same `hasUnvaluedCurrency` fallback-tier machinery `"GuildUpgrade"` used
alone before (contributes zero to both `craftCost` and `craftRealCost`,
demotes the recipe to fallback tier, never consults `currencyValuation`/
`GetBuyCost`/vendor offers). `Collect` and `RecomputeCraftCosts` got the
matching Item-positive inversion at both their top guards and (for
`RecomputeCraftCosts`) its own ingredient loop, for the same reason -
consistency across all three sites was already this saga's stated goal,
just not carried through to the guard's actual condition. In
`CraftingTreeBuilder.BuildNode`, the Currency branch is now scoped to the
literal `"Currency"` string; anything that is none of `"Item"`,
`"GuildUpgrade"`, or `"Currency"` falls through to the pre-existing
Unknown-with-hint leaf instead - which, as an adversarial-review follow-up
caught during this fix's own Code Reviewer Mode pass, still needed the
same IconUrl/Rarity-clearing treatment the GuildUpgrade and Currency
branches already have (that leaf previously only served genuine `"Item"`
nodes with no decision, where the item-keyed icon/rarity lookup is
correct; serving it to a non-Item type too meant the same wrong-domain
icon/rarity leak those two branches already closed for their own types
was reopened for anything landing here) - fixed by clearing both fields
whenever the falling-through node's `IngredientType != "Item"`.

**Tests (6 net new, real production code paths, no Blish references):**
`PlanSolverGuildUpgradeTests.cs` (+4: item-price collision, vendor-offer
collision, currency-valuation collision, and never-in-CurrencyCosts, all
using a made-up `"MysteryIngredientType"` ingredient type instead of the
literal string `"GuildUpgrade"`, proving the general guard rather than
re-proving the one instance the existing suite already covered);
`CraftingTreeBuilderTests.cs` (+2: decision is `Unknown` not `Currency`
for an unrecognized type, and IconUrl/Rarity stay null on that same
fallthrough even when `metadata` collides on the raw id). `.csproj`
unchanged (no new test files). Item/currency/vendor ids remain
internal-only in every new assertion; no Blish HUD references; all six
tests call `PlanSolver.Solve`/`CraftingTreeBuilder.BuildTree` directly
against real `RecipeNode`/`ItemPrice`/`VendorOffer`/`CurrencyValuation`/
`ItemMetadata` fixtures, no contract-mirror/fake-logic tests.

**Fixed (Must Fix - deliverable-contract violation): this document
accumulated three separate sections above, each independently ending in
the literal line `Gate: [PENDING - the orchestrator fills in PASS/FAIL]`,
even though each fix task is specified as appending exactly ONE such
section.** A naive string replace targeting that literal line would have
matched all three, marking earlier, already-superseded write-ups with
whatever PASS/FAIL verdict was meant only for the most recent one - or,
after this section's own append, four. Fixed by stripping the trailing
`Gate:` line from all three prior sections (all now superseded by this
one, which incorporates everything from all three plus the class-level
fix above) and leaving exactly one PENDING `Gate:` line in the document
as of this commit - the one at the end of this section. (The document
also carries several pre-existing, already-filled `Gate: PASS ...`
lines from earlier, unrelated milestones; those are untouched and out
of scope - a naive replace targeting the literal string `Gate:` rather
than the specific PENDING placeholder would still have matched them
too.) Nothing else in those three sections was altered besides the
"Remaining / deferred" bullet noted below and the removal of each stale
`Gate:` line itself. NOTE this "exactly one" claim describes only the
state as of this commit, not a standing document-wide fact: it is an
invariant every later-appended section is individually responsible for
re-establishing (by stripping whichever line was previously at EOF
before adding its own) each time a further adversarial pass appends
another section below this one - see the fix recorded near the end of
this document for a case where a later commit dropped that
responsibility and left two PENDING lines standing at once, and the
follow-up commit that restored the invariant.

**Fixed (Must Fix - dropped backlog item / dangling forward reference):
the "RESOLVED (2026-08-16)" marker on the original out-of-scope
"Recipe-ingestion bug class" GuildUpgrade finding overclaimed its own
scope.** Only the mis-costing bug and the wrong-domain "Currency"
mislabel were actually resolved by the fix it points to; resolving a
GuildUpgrade ingredient's real upgrade name and verifying the active
character's guild actually owns/has-unlocked it were never implemented -
the leaf still renders the generic "Guild upgrade (unresolved)" label,
exactly as `CraftingTreeBuilder.BuildNode`'s own `"GuildUpgrade"` branch
comment, `Models/CraftingDecision.cs`'s `GuildUpgrade` doc comment, and
the branch's own `AcquisitionHint` text all still say - all three point
readers at this document for that remainder, which the RESOLVED marker
gave no trace of. Fixed by re-labeling the marker "PARTIALLY RESOLVED"
with an explicit scope note, and adding a "Remaining / deferred" bullet
to the "GuildUpgrade ingredient costing/display fix" section above
(immediately before this one) that records both unimplemented pieces
explicitly, so the deferred work now has a real entry instead of existing
only as comments pointing at a document that claimed it was done.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - PASS (0
errors, only pre-existing StyleCop warnings, none new on
`Services/PlanSolver.cs`, `Services/CraftingTreeBuilder.cs`,
`Models/CraftingDecision.cs` (the pre-existing `SA1413` warning on its
enum body only shifted line number, from the added doc-comment lines
above it), or either touched test file. Tests: `dotnet test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` - 1390
total (1384 baseline + 6 net new) - PASS, 0 failed.

**Self-review findings (Code Reviewer Mode, fixed before commit):** the
`CraftingTreeBuilder` Unknown-leaf icon/rarity leak noted inline above
(Must Fix, caught during this fix's own review and fixed in the same
commit, not left for a fifth pass); `Models/CraftingDecision.cs`'s
`Currency` doc-comment entry (Must Fix) still literally described the
just-eliminated bug ("set directly for non-\"Item\"/non-\"GuildUpgrade\"
ingredient nodes") - fixed to match the now-`"Currency"`-scoped branch, and
the `Unknown` entry's doc comment extended to note it now also covers an
unrecognized ingredient type; confirmed the reordered `Currency`-before-
general-non-Item checks in `PlanSolver`'s three ingredient loops preserve
the coin-currency (`Gw2Constants.CoinCurrencyId`) fast path and the
valuation-overflow fallback exactly as before (no behavior change for
real Currency ingredients, only for the ones that previously fell through
uncaught). No Critical findings beyond the one fixed above. Nice to have,
not applied (scope discipline): the "Remaining / deferred" bullet added
above could eventually become its own tracked milestone item rather than
a bullet inside a fix write-up, but restructuring this document's own
organization was not part of this task's scope.

## GuildUpgrade ingredient costing/display fix - unrecognized-type class sweep completion + Gate-line claim fix (2026-08-16)

A fifth adversarial pass (external orchestrator review of the section
immediately above) found two defects: the unrecognized-ingredient-type
fallthrough's class sweep was only 2/5 complete, and this document's own
previous section made a false factual claim about how many `Gate:` lines
the document contains. No DO-NOT-TOUCH code was touched
(`Services/ModuleLog.cs`, `Services/PlanContentHeightMath.cs`,
`Services/PlanRelayoutMath.cs`, scroll machinery, `VendorBatchSolver`'s
merged-ceil batching math).

**Fixed (Must Fix - class sweep left 3/5 fields leaking): the
unrecognized-ingredient-type fallthrough in `CraftingTreeBuilder.
BuildNode` (the `!decisions.TryGetValue(...)` branch) only cleared
IconUrl and Rarity for a non-`"Item"` node - Name, AcquisitionHint, and
AcquisitionBadge were still resolved from the ITEM-domain `metadata`/
`hints` dictionaries keyed on the raw ingredient id, the exact same
wrong-domain leak class the branch's own comment claimed to close.**
Verified: with a colliding `metadata`/`hints` entry, a
`{Id=829, IngredientType="MysteryIngredientType"}` node rendered an
unrelated item's Name, AcquisitionHint, and AcquisitionBadge - the badge
in particular becomes the literal pill text `DecisionPillPlanner.
BuildPillSpecs` renders, making the wrong-domain value the single most
visible thing on the row. Separately, with no collision, `ResolveName`'s
"Unknown Item" fallback mislabeled a non-item as an item - the same
mislabel class the sibling `GuildUpgrade` branch's "Guild upgrade
(unresolved)" label exists to avoid one branch above. The existing test
`UnrecognizedIngredientType_NeverResolvesIconOrRarityViaItemMetadata_
EvenWhenIdCollides` seeded `Name = "Unrelated Item"` into the colliding
fixture and never asserted on Name, so the leak sat inside the fixture of
the test meant to prove it closed. Fixed by resetting `Name` to a
generic, ID-free label (`"Unrecognized ingredient (unresolved)"`,
matching the `GuildUpgrade` branch's shape) and skipping
`ApplyAcquisitionHint` entirely for non-`"Item"` nodes in this branch
(it looks `hints` up by the same raw-id/ITEM-domain key `metadata` uses,
so a partial fix leaving it in place would have kept the
AcquisitionHint/AcquisitionBadge leak open). Extended the existing test
to also seed a colliding `hints` entry and assert Name, AcquisitionHint,
and AcquisitionBadge all stay clear of it, alongside the pre-existing
IconUrl/Rarity assertions.

**Fixed (Must Fix - false claim in the deliverable record): the section
immediately above claimed stripping the three stale Gate lines left
"exactly one `Gate:` line in the entire document."** Measured:
`grep -c '^Gate:' docs/KNOWN-ISSUES.md` returned 5, not 1 - four
pre-existing, already-filled `Gate: PASS 2026-08-16 ...` lines from
earlier, unrelated milestones plus the one `PENDING` line the claim was
actually describing. The functional contract (exactly one `PENDING` Gate
line, file ending with the exact required line) was met; the stated fact
about the literal string `Gate:` was not, and it was wrong about the
precise hazard the paragraph exists to eliminate - a naive replace
targeting `Gate:` (rather than the specific `PENDING` placeholder) still
matches five lines, not one. Fixed by correcting the claim to "exactly
one PENDING `Gate:` line" and adding a parenthetical noting the
pre-existing filled Gate lines are untouched and out of scope.

**Tests (0 net new; 1 existing test extended, real production code
paths, no Blish references):**
`CraftingTreeBuilderTests.cs`'s
`UnrecognizedIngredientType_NeverResolvesIconOrRarityViaItemMetadata_
EvenWhenIdCollides` now also seeds a colliding `AcquisitionHint` entry
and asserts Name, AcquisitionHint, and AcquisitionBadge, in addition to
its pre-existing IconUrl/Rarity assertions. `.csproj` unchanged (no new
test files). Item/currency ids remain internal-only in the assertion;
no Blish HUD references; the test calls `CraftingTreeBuilder.BuildTree`
directly against real `RecipeNode`/`ItemMetadata`/`AcquisitionHint`
fixtures, no contract-mirror/fake-logic test.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - PASS (0
errors, only pre-existing StyleCop warnings, none new on
`Services/CraftingTreeBuilder.cs` or the touched test file). Tests:
`dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`
- 1390 total (unchanged - existing test extended, not a new one) - PASS,
0 failed.

**Self-review findings (Code Reviewer Mode, fixed before commit):** none
beyond the two fixed above - the non-`"Item"` branch's early `return`
was checked to confirm it cannot fall through into the `ApplyAcquisitionHint`
call meant only for genuine `"Item"` nodes with no decision; the `"Item"`
path through this same branch (a real item with no recipe/price, the
`AcquisitionSource.UnknownSource` case) was confirmed unchanged - it
still resolves Name/IconUrl/Rarity from `metadata` and still calls
`ApplyAcquisitionHint`. Nice to have, not applied (scope discipline): the
five-field class sweep is now complete for this one fallthrough branch,
but no other site in this file was audited for the same collision
pattern - out of scope for this targeted fix.

## GuildUpgrade ingredient costing/display fix - Evaluate doc-comment invariant + decision-lookup guard hoist (2026-08-16)

A sixth adversarial pass (external orchestrator review of the section
immediately above) found two defects: `PlanSolver.Evaluate`'s own doc
comment stated a false memo-entry invariant, and
`CraftingTreeBuilder.BuildNode`'s non-`"Item"` catch-all (hoisted into
its own branch by the previous section) was still nested inside the
`!decisions.TryGetValue(...)` branch instead of sitting before the
lookup like its `GuildUpgrade`/`Currency` siblings. No DO-NOT-TOUCH code
was touched (`Services/ModuleLog.cs`,
`Services/PlanContentHeightMath.cs`, `Services/PlanRelayoutMath.cs`,
scroll machinery, `VendorBatchSolver`'s merged-ceil batching math).

**Fixed (Must Fix - false invariant in the one doc comment that states
the memo contract): `Evaluate`'s doc comment claimed "EVERY non-currency,
non-guild-upgrade ingredient of EVERY recipe on this node is evaluated
(and therefore gets its own memo entry)".** The code it documents is
Item-positive (`if (ingredient.IngredientType != "Item") { hasUnvaluedCurrency
= true; continue; }`), so any ingredient type other than `"Item"` -
`"Currency"`, `"GuildUpgrade"`, or an unrecognized fourth type - gets no
memo entry, not just Currency and GuildUpgrade. A reader trusting the old
wording could write `memo[ingredient.NodeId]` for an unrecognized-type
ingredient and get a `KeyNotFoundException` - the exact instance-vs-class
drift the two preceding class-sweep commits (`c9ebc32`, `e42125e`) exist
to eliminate, left standing in the one comment that states the contract
itself. Swept the rest of `PlanSolver.cs` for the same phrasing
(`grep -n "non-currency, non-guild-upgrade\|gets its own memo\|EVERY.*
ingredient.*evaluated"`) - no other hit; `Collect`'s and
`RecomputeCraftCosts`' own doc comments make no equivalent per-ingredient
memo-entry claim, so this was a single instance, not a second site of the
same class. Fixed by rewording to "EVERY `"Item"` ingredient ... is
evaluated" and adding an explicit sentence naming Currency, GuildUpgrade,
and an unrecognized type as the three cases that get no memo entry,
pointing at the Item-positive top guard and the `hasUnvaluedCurrency`
recipe-loop skip as the two places that enforce it.

**Fixed (Must Fix - guard silently conditional on solver behavior, not
enforced by this method's own construction): the non-`"Item"` catch-all
in `BuildNode` sat inside the `!decisions.TryGetValue(node.NodeId, out
var decision)` branch, unlike the sibling `"GuildUpgrade"` and
`"Currency"` branches immediately above it, which return before the
decisions lookup runs at all.** Today this is not reachable -
`Evaluate`'s Item-positive top guard plus its recipe-loop skip mean no
non-`"Item"` node is ever memoized - but the guard's correctness rested
entirely on that external fact holding, the same unenforced-by-construction
shape this branch's own five preceding fixes on this file exist to
eliminate for the icon/rarity/name/hint/badge leaks. Had a memo entry
ever existed for such a `NodeId` (a future `PlanSolver` change, a shared
`NodeId` collision), the node would have fallen through to the
decision-found path below it and rendered with the ITEM-domain
Name/IconUrl/Rarity/AcquisitionHint/AcquisitionBadge this whole class of
fix exists to strip, reopening the leak from first principles rather than
by any actual behavior change in `PlanSolver`. Fixed by hoisting the
`node.IngredientType != "Item"` catch-all to its own branch immediately
after the `"Currency"` branch, before the `decisions` lookup, matching
where `GuildUpgrade` and `Currency` sit - it now returns unconditionally
for a non-`"Item"` node regardless of whether a memo entry exists,
exactly like its two siblings.

**Tests (1 net new, real production code paths, no Blish references):**
added `CraftingTreeBuilderTests.
UnrecognizedIngredientType_IgnoresStaleMemoEntry_EvenWhenOneExistsForThisNodeId`,
which hands `CraftingTreeBuilder.BuildTree` a `decisions` dictionary that
DOES contain a `BuyFromTp` entry for the unrecognized-type node's own
`NodeId` (a scenario the real solver cannot produce today, proving the
guard by this method's own construction rather than by relying on that
external fact) and asserts the node still renders `CraftingDecision.
Unknown` with the generic label, null IconUrl/Rarity, and null
SubtreeCost/UnitCost - never the `BuyFromTp` decision or its cost. No new
test files (`.csproj` unchanged). Item/currency ids remain internal-only
in the assertions; no Blish HUD references; the test calls
`CraftingTreeBuilder.BuildTree` directly against a real `RecipeNode`/
`SolverDecision`/`ItemMetadata` fixture, no contract-mirror/fake-logic
test.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - PASS (0
errors, only pre-existing StyleCop warnings, none new on
`Services/PlanSolver.cs`, `Services/CraftingTreeBuilder.cs`, or the
touched test file). Tests:
`dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`
- 1391 total (1390 + 1 new) - PASS, 0 failed.

**Self-review findings (Code Reviewer Mode, fixed before commit):** none
beyond the two fixed above - re-read the full hoisted branch after the
move to confirm it still returns unconditionally (no accidental
fallthrough into the `decisions` lookup below it), and confirmed the
`"Item"`-with-no-decision path immediately below (a real item with no
recipe/price, the `AcquisitionSource.UnknownSource` case) is unchanged -
it still falls through to `ApplyAcquisitionHint` exactly as before,
since that call sits in its own now-unconditional-on-non-Item-only
branch. Nice to have, not applied (scope discipline): the hoisted branch
and the decision-not-found branch immediately below it both assign
`treeNode.Decision = CraftingDecision.Unknown` for different reasons (one
unconditionally for a non-`"Item"` type, one only when no memo entry
exists for an `"Item"` type); a shared local could deduplicate the two
four-line assignments, but the branches' preconditions are different
enough, and the duplication small enough, that introducing a new
abstraction for it is not justified under this repo's "avoid
infrastructure unless required" efficiency principle - out of scope for
this targeted fix.

## GuildUpgrade ingredient costing/display fix - pill-layer instance-vs-class gap closed (2026-08-16)

A seventh adversarial pass (external orchestrator review) found the
instance-vs-class gap this whole fix series exists to eliminate,
reintroduced one layer up, in the pill rendering: an unrecognized-
ingredient-type leaf rendered a LIVE, clickable IGNORE pill keyed on a
non-item id. No DO-NOT-TOUCH code was touched (`Services/ModuleLog.cs`,
`Services/PlanContentHeightMath.cs`, `Services/PlanRelayoutMath.cs`,
scroll machinery, `VendorBatchSolver`'s merged-ceil batching math).

**Fixed (Critical - live clickable IGNORE pill reachable on a non-item
id, the exact bug class this fix series exists to close):
`CraftingTreeBuilder.BuildNode`'s non-`"Item"` catch-all set
`CraftingDecision.Unknown`, the SAME value a genuine no-source `"Item"`
node gets. `DecisionPillPlanner.BuildPillSpecs` cannot tell the two
`Unknown` cases apart by `Decision` alone, so the unrecognized-type leaf
took the `options.Count == 0` branch and got `AppendOwnershipPills`'
unconditional interactive `IGNORE` pill.** `TreeSectionController`'s pill
click handler reads `node.ItemId` and adds it to the tree-wide
`_ignoredItemIds` set (`PlanSolver.Evaluate`/`Collect` key strictly by
numeric item id), so clicking that IGNORE pill either did nothing (no
`"Item"` node in the tree shares the raw id) or silently zeroed the cost
of an unrelated genuine `"Item"` node elsewhere in the tree that happened
to share the same number - persisting past restart via
`PersistedPlan.IgnoredItemIds`. Separately, the toggle was permanently
dead either way: `BuildNode`'s Have/`IsIgnored` collapse is scoped to
`IngredientType == "Item"`, so this node itself can never flip state -
the user would click a control that visibly does nothing, forever, on
top of the silent cross-domain zeroing risk. Latent today (the current
seed data carries only `Item`/`GuildUpgrade`/`Currency` ingredient types,
and `Gw2RecipeApiClient` defaults a missing type to `"Item"`), but the
API's ingredient-type string passes through with no allow-list, so a
future fourth GW2 ingredient type reaches this path directly. Fixed by
giving this leaf its own `CraftingDecision.UnrecognizedIngredient` value
(appended LAST in the enum - see its own doc comment for why the ordinal
position matters) instead of sharing `Unknown`, and adding a matching
single-locked-pill, no-`IGNORE` short-circuit for it in
`DecisionPillPlanner.BuildPillSpecs` - the same treatment `Currency` and
`GuildUpgrade` already get, returning before the `options.Count == 0`
branch is ever reached. `TreeSectionController`'s Locked-pill tooltip
switch also gained a matching branch (mirroring its existing
`Unknown`/`GuildUpgrade` arms) so the new `"UNRECOGNIZED"` pill does not
fall into the generic-but-misleading `"Only available source"` tooltip
text (there is no available source here at all).

**Tests (3 net new, 1 renamed for accuracy; real production code paths,
no Blish references):**
- `DecisionPillPlannerTests.UnrecognizedIngredient_SingleLockedPill_NotInteractive`
  and `UnrecognizedIngredient_NeverGetsIgnorePill_EvenWithOwnedQuantityUsed`
  - direct mirrors of the existing `GuildUpgrade_*` pair, exercising
  `DecisionPillPlanner.BuildPillSpecs` directly, closing the exact test
  gap the review named (`GuildUpgrade` had this pair, the unrecognized-
  type case did not).
- `CraftingTreeBuilderTests.UnrecognizedIngredientType_NeverGetsIgnorePill_EvenThoughDecisionLooksLikeNoSource`
  - the direct end-to-end regression test for the finding itself: feeds a
  `"MysteryIngredientType"` leaf through the real `CraftingTreeBuilder.
  BuildTree`, then the real `DecisionPillPlanner.BuildPillSpecs` on the
  resulting node, and asserts exactly one `Locked` `"UNRECOGNIZED"` pill
  with no `IGNORE` pill anywhere in the result - closing the second gap
  the review named (the builder's own tests asserted Name/IconUrl/Rarity/
  AcquisitionHint/AcquisitionBadge but never the resulting pill shape).
- `UnrecognizedIngredientType_DecisionIsUnknown_NeverMislabeledCurrency`
  renamed to `UnrecognizedIngredientType_DecisionIsUnrecognizedIngredient_
  NeverMislabeled` and its assertion updated from `CraftingDecision.
  Unknown` to `CraftingDecision.UnrecognizedIngredient` (plus a new
  `Assert.NotEqual(CraftingDecision.Unknown, ...)`), since the decision
  value itself changed; the two other pre-existing `MysteryIngredientType`
  tests (`..._NeverResolvesIconOrRarityViaItemMetadata_EvenWhenIdCollides`,
  `..._IgnoresStaleMemoEntry_EvenWhenOneExistsForThisNodeId`) had their
  `CraftingDecision.Unknown` assertions updated to `CraftingDecision.
  UnrecognizedIngredient` for the same reason; their actual coverage
  (Name/IconUrl/Rarity/AcquisitionHint/AcquisitionBadge/SubtreeCost/
  UnitCost isolation from item-domain collisions) is unchanged. No new
  test files (`.csproj` unchanged). Item ids remain internal-only in the
  assertions; no Blish HUD references; every new/changed assertion calls
  real `CraftingTreeBuilder.BuildTree` and/or `DecisionPillPlanner.
  BuildPillSpecs` directly, no contract-mirror/fake-logic test.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - PASS (0
errors, no new warnings on any touched file: `Models/CraftingDecision.cs`,
`Services/CraftingTreeBuilder.cs`, `Services/DecisionPillPlanner.cs`,
`Views/Rendering/TreeSectionController.cs`). Tests:
`dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`
- 1394 total (1391 + 3 new) - PASS, 0 failed.

**Self-review findings (Code Reviewer Mode, fixed before commit):**
swept every switch/case and `if (node.Decision == ...)` site over
`CraftingDecision` project-wide (`grep -rn "case CraftingDecision\."` and
`CraftingDecision\."`) to confirm no other site silently treats
`UnrecognizedIngredient` as `Unknown` or falls into a stale default arm -
`DecisionPillPlanner`'s `AcquisitionSource` switch (`Craft`/`BuyFromTp`/
`BuyFromVendor` only, with a defensive `default`) is unreachable for this
value since the new branch returns first, and `PlanContentHeightMath`'s/
`TreeSectionController`'s own `!= CraftingDecision.Craft` dimming checks
already treat every non-`Craft` decision uniformly, so they need no
change. Confirmed no `Enum.IsDefined`/range-validation guard exists
anywhere on `CraftingTreeNode.Decision` deserialization (matching the
precedent already set when `GuildUpgrade` was appended in an earlier
section of this same fix series) - appending `UnrecognizedIngredient`
strictly additively at the end of the enum is consistent with that
precedent and does not reopen it. Nice to have, not applied (scope
discipline): `DecisionPillPlanner`'s `Currency`/`GuildUpgrade`/
`UnrecognizedIngredient` branches are now three near-identical four-line
single-locked-pill blocks; a shared private helper
(`AddSingleLockedPill(specs, text)`) could deduplicate them, but the
duplication is small, pre-existing (`Currency`/`GuildUpgrade` already
duplicated each other before this fix), and introducing a new
abstraction for it is not justified under this repo's "avoid
infrastructure unless required" efficiency principle for a targeted
fix - out of scope here, same call made in the immediately preceding
section for a structurally identical case.

## GuildUpgrade ingredient costing/display fix - orchestrator fix-loop: Gate-line duplication, RecipeService type-guard drift, vendor cost-line classification sibling closed (2026-08-16)

An eighth adversarial pass (external orchestrator review of the whole
saga above) found three defects: this document's own Gate-line contract
was broken again by the two immediately preceding sections, a fourth
Item-positive guard site (`RecipeService.BuildNodeAsync`) was left out
of the class sweep the rest of this saga performed, and the vendor
cost-line classification loop this saga never touched has the
structurally identical silently-drop-unrecognized-type shape the whole
saga exists to close, in a different domain (`VendorOffer.CostLines[].Type`
rather than `RecipeNode.IngredientType`/`RawIngredient.Type`). No
DO-NOT-TOUCH code was touched (`Services/ModuleLog.cs`,
`Services/PlanContentHeightMath.cs`, `Services/PlanRelayoutMath.cs`,
scroll machinery); `VendorBatchSolver`'s merged-ceil batching math
(the `unitsNeeded`/scaling arithmetic below the cost-line loop) was
also left untouched, per the finding's own explicit carve-out - only
the cost-line classification loop above it was in scope.

**Fixed (Must Fix - deliverable-contract violation, recurrence of the
defect the "residual class-gap follow-up" section above already fixed
once): the "pill-layer instance-vs-class gap closed" section immediately
above this one appended its own `Gate: [PENDING - the orchestrator fills
in PASS/FAIL]` line without stripping the one already sitting at the end
of the "Evaluate doc-comment invariant..." section before it, leaving
two PENDING lines standing in the document at once.** The two sections
between the original "residual class-gap follow-up" fix and this one
("unrecognized-type class sweep completion...", "Evaluate doc-comment
invariant...") had each correctly stripped their own predecessor's Gate
line before appending a new one, so the single-PENDING-line invariant
actually held for two more appends after the original fix before the
"pill-layer" section's append broke it again. A naive string replace
targeting the literal PENDING marker would have stamped whatever verdict
was meant only for the latest section onto the superseded one as well -
the exact hazard the "residual class-gap follow-up" section's own fix
already exists to prevent. That earlier fix's own wording stated the
outcome as a standing fact about the document ("leaving exactly one
PENDING `Gate:` line in the entire document") rather than a per-append
responsibility every later section must independently uphold, which is
plausibly why the lapse went uncaught for two appends before it
recurred. Fixed by stripping the stale `Gate:` line from the "Evaluate
doc-comment invariant..." section (superseded by the "pill-layer"
section appended after it) and reworking the earlier "residual
class-gap follow-up" section's wording from a standing document-wide
claim to an explicitly commit-scoped one ("...in the document as of
this commit"), with a new sentence pointing readers at this section as
the place a later violation was found and fixed - so a future reader of
that earlier section is told the invariant needs active per-append
maintenance rather than reading it as a fact that holds forever.
Exactly one PENDING `Gate:` line now remains in the document: the one
at the end of this section.

**Fixed (Must Fix - class sweep left a fourth Item-positive guard site
untouched, reintroducing the exact instance-vs-class drift this whole
saga exists to eliminate): `RecipeService.BuildNodeAsync`'s guard read
`if (!string.IsNullOrEmpty(ingredientType) && ingredientType != "Item")`
- unlike every sibling guard this saga already converted
(`PlanSolver.Evaluate`/`Collect`/`RecomputeCraftCosts`,
`CraftingTreeBuilder.BuildNode`, all now flatly `!= "Item"`), a null or
empty `ingredientType` fell through this guard's `IsNullOrEmpty` clause
and was treated AS an item - its recipes were searched and its subtree
fully expanded, exactly like a genuine `"Item"` node.** That is now
inconsistent with every downstream site: `PlanSolver.Evaluate`'s
Item-positive top guard treats that same null-typed node as unpriceable
the moment it is reached (contributes zero to `craftCost`/
`craftRealCost`, demotes to fallback tier, generates no shopping steps
for the whole subtree `RecipeService` bothered to build), and
`CraftingTreeBuilder.BuildNode` renders it as the generic "Unrecognized
ingredient (unresolved)" leaf with no recursion into the children
`RecipeService` attached beneath it - so the mismatch does not crash or
mis-price anything today, it silently discards a real, wiki-verified
subtree's worth of cost and shopping-list detail. Not reachable with
today's data (`ref/recipes_seed.json`: 48237 Item / 678 GuildUpgrade /
195 Currency, 0 missing type; `Gw2RecipeApiClient.cs` defaults a missing
API `type` string to `"Item"`; `MysticForgeRecipeData.cs` rejects an
empty `type` outright during ingestion), but `RawIngredient.Type` is a
plain `string` property with no default, and `RecipeCacheSerializer`
deserializes seed/overlay `RawRecipe`/`RawIngredient` rows straight
through `System.Text.Json` with no default-value handling - commit
e81b7e4 (2026-02-14) shows this exact field WAS null-valued before the
API-client default was added, so the shape is a real historical
regression risk, not a hypothetical one. Fixed by inverting the guard to
match every sibling site: `if (ingredientType != "Item")`, so null,
empty, and any other non-`"Item"` string all short-circuit to an
unexpanded leaf node the same way `"Currency"`/`"GuildUpgrade"`/an
unrecognized type already do, keeping `RecipeService`'s tree-shape
decision in sync with what `PlanSolver` and `CraftingTreeBuilder` will
do with that same node afterward.

**Fixed (Must Fix - class, not instance: structurally identical
unrecognized-cost-line-type sibling in the vendor domain, left open by
the "Currency sibling icon/rarity leak" section's own exclusion note
above, which answered only the specific `GuildUpgrade`-vs-`CostLine.Type`
instance and not the general class): `VendorBatchSolver.EvaluateVendorOffers`'s
cost-line fold handled `cost.Type == "Currency"` and `else if == "Item"`
with no final `else` - an unrecognized `CostLine.Type` fell through both
branches, contributing nothing to `coinCost` and never touching
`priceable`, so the offer was costed as though that line did not exist
at all.** Concretely: a vendor offer whose only real cost is an
unrecognized-type line would price at 0 coin and can win `BuyFromVendor`
over a correctly-priced TP alternative - an understated, fabricated-low
price, not a crash, which is the harder-to-notice failure mode.
`VendorOfferLoader.Load` performs no type validation at load (only a
null-stream check) and `ref/vendor_offers.json` is tool-scraped from the
wiki, the same ingestion shape that let `"GuildUpgrade"` reach the
recipe-ingredient side of this saga in the first place, so a future
third `CostLine.Type` (today only `"Currency"`/`"Item"` appear in
practice) reaches this loop directly. Fixed by adding a final `else`
that mirrors the Item-with-no-price branch immediately above it:
`priceable = false; break;` - the whole offer is excluded rather than
priced as if the unrecognized line were absent. The merged-ceil batching
math below this loop (`unitsNeeded` and everything that scales off it)
was not touched, per this finding's own explicit DO-NOT-TOUCH carve-out
- this fix only changes which offers reach that math, never how it
computes once an offer is deemed priceable.

**Tests (2 net new, real production code paths, no Blish references):**
`RecipeServiceTests.NullTypedIngredient_BecomesLeaf_RecipeNeverExpanded`
- feeds `RecipeService.BuildTreeAsync` a real recipe (via
`InMemoryRecipeApiClient`) whose second ingredient has `Type = null`,
asserting it lands as an unexpanded leaf (`IsLeaf`, empty `Recipes`) with
its id/quantity intact rather than being recipe-expanded as an item.
`PlanSolverVendorOfferTests.VendorOfferWithUnrecognizedCostLineType_TreatedAsUnpriceable_NeverWinsOverTp`
- gives `PlanSolver.Solve` a real `VendorOffer` whose only `CostLine` has
an unrecognized `Type` and a real TP price for the same item, asserting
the plan falls back to `BuyFromTp` at the real price rather than
`BuyFromVendor` at the fabricated zero. No new test files (`.csproj`
unchanged - `RecipeServiceTests.cs` and `PlanSolverVendorOfferTests.cs`
both already existed and are already registered). Item/currency ids
remain internal-only in both assertions; no Blish HUD references; both
tests call `RecipeService.BuildTreeAsync`/`PlanSolver.Solve` directly
against real fixtures (`InMemoryRecipeApiClient`, `RawRecipe`/
`RawIngredient`, `VendorOffer`/`CostLine`, `ItemPrice`), no
contract-mirror/fake-logic test.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - PASS (0
errors, only pre-existing StyleCop warnings, none new on
`Services/RecipeService.cs` or `Services/VendorBatchSolver.cs`). Tests:
`dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`
- 1396 total (1394 + 2 new) - PASS, 0 failed.

**Self-review findings (Code Reviewer Mode, fixed before commit):** swept
`Services/PlanSolver.cs` and `Services/CraftingTreeBuilder.cs` again for
any other `IsNullOrEmpty`-style guard on `IngredientType` that the
earlier class sweeps might have missed alongside `RecipeService`'s (
`grep -n "IsNullOrEmpty.*[Ii]ngredient\|[Ii]ngredientType.*IsNullOrEmpty"`)
- no other hit; `RecipeService.BuildNodeAsync` was the only remaining
non-Item-positive site. Confirmed the `RecipeService` guard change does
not alter behavior for the two paths that matter most: a genuine
`"Item"` node still proceeds to `visiting.Add`/recipe search exactly as
before (guard condition for `"Item"` is `false` either way, old and
new), and a `"Currency"`/`"GuildUpgrade"`/other-non-empty-non-Item type
still short-circuits exactly as before (old guard's `!= "Item"` clause
already caught those; only the null/empty case changed). Confirmed
`VendorBatchSolver`'s new `else` branch is unreachable for both
currently-real `CostLine.Type` values (`"Currency"`, `"Item"`) via the
full test-suite run showing no change in any pre-existing vendor-offer
test's outcome, and reachable exactly once for the new test's
deliberately-unrecognized type. Checked whether `CraftingPlanPipeline`'s
three other `cost.Type ==` loops (`Currency`/`Item` id-prefetch
collectors for TP prices and currency/item metadata) have the same
silent-drop hazard - they do not: an unrecognized type there simply adds
nothing to a prefetch `HashSet`, which is the correct behavior for a
"gather ids to look up" loop and not the "does this line count toward
the price" gate `VendorBatchSolver`'s loop is, so those sites were
correctly left untouched rather than being swept as siblings that do not
actually share the bug's precondition. No Critical findings. Nice to
have, not applied (scope discipline): the `RecipeService` test above
covers `Type = null` but not `Type = ""` (empty string) - the fixed
guard treats both identically (`!= "Item"` is true for both), and the
prior guard's own `IsNullOrEmpty` check already proves the code author's
awareness of the empty-string case, so a second near-duplicate test
asserting the same code path for a different falsy input was judged
redundant rather than additive coverage; out of scope for this targeted
fix. `DecisionPillPlanner`'s `Currency`/`GuildUpgrade`/
`UnrecognizedIngredient` three-way duplication (noted as a deferred nice
to have in the immediately preceding section) remains deferred for the
same reason given there - not part of this finding set, no new
duplication introduced by this section's own changes.

Gate: [PENDING - the orchestrator fills in PASS/FAIL]
