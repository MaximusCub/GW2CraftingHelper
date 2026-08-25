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

## Policy: high-evidence zones (formerly DO-NOT-TOUCH)

High-evidence zones (formerly the do-not-touch list): `Services/ModuleLog.cs`,
`Services/PlanContentHeightMath.cs`, `Services/PlanRelayoutMath.cs`, the
scroll/resize/wheel machinery in `Views/CraftingPlanView.cs`, and
`Services/VendorBatchSolver.cs`'s merged-ceil batching math. Changes are
permitted when they carry their proof - characterization tests pinning
current behavior BEFORE the change (for visual/geometry code: the
pixel-scanner and a live desktop check), the standard adversarial review
pipeline, and an explicit statement of what improved with evidence of zero
regression. The burden of proof scales with the file's regression history;
it never becomes prohibition. (Policy set by the maintainer 2026-08-17,
replacing the M38-era freeze.)

Numbered items below that still say "DO-NOT-TOUCH" are historical records
narrating what applied to that specific past change at the time it was
made, and are left in their original wording to preserve the record; this
section is the current, active statement of the rule.

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
high-evidence-zone machinery (formerly DO-NOT-TOUCH; see the policy note
above), see `docs/ARCHITECTURE.md` section 3.
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
| `CraftingPlanView` (Crafting Plan tab) | **HAZARD PRESENT, OUT OF SCOPE** - not modified (scroll/`FrameTicker` machinery is a high-evidence zone, formerly DO-NOT-TOUCH per M38, hardened M31-M36). A first sweep pass incorrectly recorded this row as no live race; a 2026-08-06 review corrected it: `Build()` calls `StopLiveTickers()` (`Views/CraftingPlanView.cs:1511`) on the ThreadPool thread; that method `Cancel()`s -> `Dispose()`s three `SpriteScreen`-parented `FrameTicker` Controls (`_scrollVerifyTicker`, `_resizeDebounceTicker`, `_wheelWrapVerifyTicker`) whose `DoUpdate` runs on the main thread and survives tab switches (they are parented to `GameService.Graphics.SpriteScreen`, not this view's own control tree - by design, per their own field comments), and zeroes `_resizeSettlePending`/`_resizeScrollRestorePending`/`_resizeScrollSavedOffset`/`_lastWheelEventUtc`, which those same main-thread ticker steps read and write. Same hazard class as the two fixed rows above; deferred to a dedicated pass that can safely touch the M31-M36 scroll machinery rather than fixed here. Follow-up (2026-08-17, tree-tooltip-composer milestone doc pass): the 2026-08-06 count of three `FrameTicker`s was itself stale by then - `_spinnerTicker` (the W3B status-strip spinner, added between the two reviews) is a fourth `SpriteScreen`-parented `FrameTicker` `StopLiveTickers()` also `Cancel()`s on the same ThreadPool-thread `Build()` call, in the identical hazard class as the other three; not independently verified live, same OUT OF SCOPE deferral as the rest of this row. |

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
  to `SummarySectionLayoutMath`. **DONE (2026-08-17, high-evidence-zones
  branch).** Characterized first per the new policy (see this file's
  policy note above): confirmed by grep that no production call site
  ever passes `PlanSectionType.Summary` into `SectionBodyHeight`
  (`CraftingPlanView` always special-cases it to
  `SummarySectionLayoutMath.BodyHeight` first) and that
  `PlanViewModelBuilder` never emits `PlanRowType.CoinTotal`. Deleted:
  `SummaryBodyHeight`, its `SectionBodyHeight` switch case,
  `PlanRowType.CoinTotal`, and 4 tests that referenced `CoinTotal`
  directly and would not otherwise compile (`Summary_CoinRowPlusCurrencyRows`
  plus the three originally estimated at ~348-390) - one more than this
  bullet's own estimate, found during characterization. **Follow-up
  correction (2026-08-17, same branch, code review):** the fifth
  Summary-shape test, `Summary_NoCoinRow_OmitsTileRow`, does not reference
  `CoinTotal` and still compiled/passed, so it was initially left as-is -
  but review found it had gone vacuous: with `SummaryBodyHeight`'s switch
  case gone, it exercises `SectionBodyHeight`'s `default` arm and only
  passes because `CurrencyRowHeight`/`FallbackTextRowHeight` are both 28,
  a coincidence unrelated to Summary. It duplicated
  `UnknownSectionType_FallsBackToTextRowHeightPerRow` under a name
  claiming Summary-specific semantics that no longer exist, and would
  false-fail the moment either constant is retuned independently. Deleted.
  Full suite: 1765 before the first deletion pass, 1761 after it, 1760
  after this correction (5 dead/vacuous tests removed total).
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
`InventoryReducer.ReduceNodeSourced`'s doc comment for the precise
mechanism.
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

## High-evidence zones: policy rewrite + PlanContentHeightMath dead-code sweep (high-evidence-zones, 2026-08-17)

Two-part change, the first application of the new high-evidence-zone
policy (maintainer, 2026-08-17, replacing the M38-era DO-NOT-TOUCH
freeze - see this file's own policy note near the top).

**Part 1 (policy docs).** Swept `docs/KNOWN-ISSUES.md` and
`docs/ARCHITECTURE.md` for DO-NOT-TOUCH/frozen-file language that
functions as an active, current-state statement (as opposed to a
historical gate/review record narrating what applied to a specific past
change at the time it was made, which was left untouched to preserve the
record): added the policy note near the top of this file; reworded item
23's and the concurrency-audit table's `CraftingPlanView` row's
DO-NOT-TOUCH phrasing to "high-evidence zone (formerly DO-NOT-TOUCH)";
reworded `docs/ARCHITECTURE.md` section 7's two do-not-touch statements
about `VendorBatchSolver.cs`'s merged-ceil arithmetic the same way, and
added the proof requirement to that section. `CLAUDE.md` (in-repo) was
checked and does not carry DO-NOT-TOUCH/frozen-file language, so it
needed no change. Historical narration of specific past PRs' DO-NOT-TOUCH
compliance (roughly two dozen entries throughout this file's numbered
catalog and PR write-ups) was deliberately left in its original wording -
rewriting those would misrepresent what rule actually applied at the
time.

**Part 2 (first application - the freeze-trapped dead code).**
Characterized before touching anything, per the new policy's proof
requirement: grepped every reference to `SummaryBodyHeight` and
`CoinTotal` across `src` and `tests`; confirmed `PlanViewModelBuilder`
never emits `PlanRowType.CoinTotal` and that `Views/CraftingPlanView.cs`
always special-cases `PlanSectionType.Summary` to
`SummarySectionLayoutMath.BodyHeight` before it would ever reach
`PlanContentHeightMath.SectionBodyHeight`, so the private
`SummaryBodyHeight` method (and the `PlanSectionType.Summary` case that
called it) has been unreachable from any live path since W4A. Ran the
full suite first: 1765 passed. Then deleted
`PlanContentHeightMath.SummaryBodyHeight`, its `SectionBodyHeight` switch
case, the `PlanRowType.CoinTotal` enum member, and updated the
comments in `Models/PlanViewModel.cs`, `Services/PlanContentHeightMath.cs`,
`Services/SummarySectionLayoutMath.cs`, `Views/CraftingPlanView.cs`, and
`Views/Rendering/SummarySectionRenderer.cs` that referenced them as
pending deletion or still-live. Characterization surfaced one more dead
test than this file's own W4A follow-up bullet had estimated:
`Summary_CoinRowPlusCurrencyRows` (outside the bullet's ~348-390
estimate) also referenced `PlanRowType.CoinTotal` directly and would not
compile once the member was removed, so it was deleted alongside the
three tests the bullet did name
(`Summary_MultiItemNoteRow_AddsFallbackTextRowHeight`,
`Summary_NoMultiItemNoteRow_UnaffectedByNewBranch`,
`Summary_MultiItemFourCoinRowsPlusNoteRow_StillOneCostTileRowHeight`) -
4 deleted tests total. `Summary_NoCoinRow_OmitsTileRow` does not
reference `CoinTotal`, still compiles, and still passes (the
`PlanSectionType.Summary` case now falls through to
`SectionBodyHeight`'s existing `default` branch, which happens to return
the same value for its one-`CurrencyCost`-row input since
`CurrencyRowHeight`/`FallbackTextRowHeight` are both 28) - initially left
unchanged on the theory that the larger `SummarySectionLayoutMath`
fold-back is out of scope for this branch. Re-ran the full suite after:
1761 passed - exactly baseline (1765) minus the 4 deleted dead tests,
nothing else changed.

**Follow-up correction (same day, code review).** Leaving
`Summary_NoCoinRow_OmitsTileRow` in place was itself a defect, not a
scope call: with the `Summary` switch case gone, the test exercises only
`default`'s `rows.Count * FallbackTextRowHeight` arithmetic, passing
solely because `CurrencyRowHeight` and `FallbackTextRowHeight` happen to
both equal 28 - a coincidence with no connection to `Summary`. It had
become a duplicate of `UnknownSectionType_FallsBackToTextRowHeightPerRow`
under a name asserting Summary-specific tile-omission behavior that no
longer exists anywhere in the codebase, and would produce a confusing
false failure (naming `Summary`/`OmitsTileRow`) the first time either
constant is retuned independently of the other. Deleted. Full suite:
1760 passed after this correction (5 dead/vacuous tests removed from the
1765 baseline total). The `SummaryBodyHeight` deletion did not remove
`PlanRowType.CoinTotal`'s underlying enum value from a serialized/
persisted format anywhere: `PlanRowType`/`PlanRowViewModel` are rebuilt
fresh on every render by `PlanViewModelBuilder` and never
serialized/persisted, and grep found no `(int)PlanRowType`/
`Enum.GetValues(typeof(PlanRowType))` call sites, so the enum's ordinal
values shifting by one position (removed member was first) has no
runtime effect.

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-hezone/GW2CraftingHelper.csproj -p:Platform=x64` - 0
errors both before and after (pre-existing StyleCop warnings only).
Tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-hezone/tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` - 1765 before, 1761 after the first
deletion pass, 1760 after the same-day follow-up correction that deleted
`Summary_NoCoinRow_OmitsTileRow` (see above), 0 failed at every step.

Repo Invariants Checklist:
- [x] No Blish HUD references added to tests.
- [x] Tests exercise real production paths (deleted tests exercised only
  the now-removed dead code; the remaining suite is unchanged production
  coverage).
- [x] No fake file I/O tests introduced.
- [x] Pricing logic preserves multi-source correctness (untouched by this
  change).
- [x] IDs remain internal-only (untouched by this change).

Not pushed; not committed to `master`. This branch does not attempt the
larger `SummarySectionLayoutMath`/`PlanContentHeightMath` fold-back - that
remains a real behavior-bearing change awaiting the audit fleet's
architecture findings, per this task's own instruction.

Gate: PASS (non-visual change: docs policy rewrite plus dead-code
deletion, so no desktop gate applies; evidence is the unreachability
grep proof plus the suite arithmetic above - 1765 baseline to 1760
passing with only the 5 dead/vacuous tests removed, 0 failures, build
clean at b420460).

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

Gate: not run live this pass - test-and-docs change with no runtime
code touched; pipeline-level behaviour is suite-pinned (mutation-checked
per the review record above) and the live hover re-check stays on the
next desktop gate batch, where fallback-tier propagation is the first
thing to rule out. Merged under the maintainer's standing merge
directive (2026-08-16).

## Merged-ceil remainder: largest-remainder apportionment + display-layer narrowing fix (2026-08-17)

**Milestone goal:** quorum verdict C6 (TARGETED_FIX_ONLY plus the
judge's own new finding) on the `merged-ceil-remainder` stream, which
enters `VendorBatchSolver` - a former high-evidence/freeze zone
(maintainer-retired 2026-08-17) - so per that retirement's own terms,
characterize the current behavior in tests BEFORE changing it, then
fix, then prove improved-X/regressed-nothing.

**What changed:**
1. **Characterization commit (`25fc887`).** Pinned
   `AllocateVendorNodeCosts`' pre-fix "UnitCost * quantity per non-last
   occurrence, last occurrence absorbs the entire remaining balance"
   shape before touching it: the unbounded equal-quantity case (a "100
   for 1000c" bulk offer split 1+1 rendered 10/990), the sum invariant,
   and three real downstream consumers -
   `CompetencyOpportunityCalculator` (real Solve()+
   `CraftingTreeBuilder`+calculator round trip),
   `RecipeSheetSavingsCalculator` (fixture bridging the same
   arithmetic), and `SellSideEconomics.ApplyBatchSellSideEconomics`/
   `CraftingProfit` (real Solve() round trip). Every assertion is
   commented with the exact number the fix commit re-baselines it to.
2. **Fix commit (`938f6c9`).** Replaced that shape with largest-
   remainder (Hamilton) apportionment: each occurrence's floor share is
   `step.TotalCost * quantity / totalQuantity`, and the leftover
   (always strictly fewer coppers than there are occurrences - a
   standard apportionment identity, proven in the commit message) goes
   one each to the occurrences with the largest fractional remainder,
   ties broken by first-seen (DFS) order. Divergence between any two
   equal-quantity occurrences is now bounded to <=1 copper (was
   unbounded). The flagship 179-unit/"3 for 3"-Laurel regression shape
   (quantities 4/4/4/83/84, hand-verified floors 4/4/4/83/84 + the one
   leftover copper landing on the 84-quantity occurrence via its 84/179
   fraction, the largest) is **unchanged**: still 4/4/4/83/85 summing
   to 180. Re-baselined the four pinned characterization tests plus one
   pre-existing (not new) test that turned out to depend on the old
   skewed shape (`MultiItemPlanTests.
   GenerateStructuredAsync_TwoItems_SharedBulkVendorMaterial_
   BothTradable_...`: two symmetric roots sharing a "5 for 20 coin"
   material used to split 8/12 by tree-position accident, now split
   evenly 10/10).
3. **New bug (judge-found, real, unrelated to the vendor-batch math):
   `Services/PlanViewModelBuilder.cs` `BuildCurrencyTableRows` narrowed
   `CurrencyCost.Amount` (long) to int with a plain `(int)` cast. Past
   `int.MaxValue` this silently wraps NEGATIVE, and
   `fullyCovered = owned >= required` then reads true for almost any
   owned amount - the opposite of what a currency requirement that
   large should show. Class-swept (grepped Services/Models/Views for
   any other unchecked long-to-int narrowing of an Amount/TotalCost/
   UnitCost/Count-shaped field): this was the only one. Fixed with
   `ClampToInt` (clamp to `int.MaxValue`), the identical convention
   `VendorBatchSolver.ClampToInt` already uses for the same class of
   risk. New boundary test
   (`PlanViewModelBuilderSummaryTests.
   CurrencyTable_AmountExceedsIntRange_ClampsRatherThanWrapsNegative`)
   confirmed reproducing the bug pre-fix and passing post-fix.
4. **C6(b) currencyMap "overstates" claim - verified NOT a bug.** The
   quorum verdict named a prior claim that a Conflict-tier vendor
   step's `currencyMap` accumulation "overstates" cost. Searched this
   repo exhaustively (`docs/KNOWN-ISSUES.md`, `docs/ARCHITECTURE.md`,
   `docs/gw2e-considerations.md`, `docs/research/gw2e-convergence-
   matrix.md`, every other tracked doc, and code comments across
   `Services/`/`Models/`) plus every sibling worktree on this machine
   (`wt-hezone`, `wt-qp1`, `wt-valuedetail`) for the exact wording or
   any equivalent ("double-count", "inflate", "overcounts") tied to
   `currencyMap`/Conflict - found no such claim anywhere accessible to
   this stream. Recording the correct verdict here as the authoritative
   reference regardless, so any surviving reference elsewhere resolves
   against this entry: a Conflict-tier step (two tree occurrences that
   genuinely prefer different vendor offers - see
   `PlanSolverVendorBatchingTests.
   MultiOccurrenceDifferentWinningOffers_LeavesPerOccurrenceSumUnmerged`)
   never runs through `AllocateVendorNodeCosts` at all
   (`VendorOfferOutputCount` stays 0, guarded out at that method's own
   entry). Its `currencyMap`/`Required` total is exactly the sum of
   each occurrence's own genuinely-different, individually-correct
   currency cost - which is also exactly the shopping list's summed
   `PlanStep.Quantity` and the sum of the real tree leaves' own
   `Decision.TotalCost` (152 coin in that test's own 1-for-2 + 100-for-
   150 shape: 2 + 150 = 152, matching `vendorStep.TotalCost`,
   `plan.TotalCoinCost`, and `result.Decisions[tree.NodeId].TotalCost`
   all at once). This is correct, not an overstatement: there is no
   single true merged offer to ceil across two occurrences that
   genuinely used different offers, so forcing one would misrepresent
   the real purchases rather than fix anything. Changing Conflict-tier
   `currencyMap` to disagree with `Required`/the shopping list/the tree
   leaves would create the real internal inconsistency the alternative
   claim would have introduced.
5. **Review-fix commit (`0b60ceb`).** A follow-up review found the
   class sweep for item 2/938f6c9's largest-remainder apportionment had
   missed a second runtime path: `PlanSolver.RecomputeComparisonValues`'
   currency-equivalent share loop still used the deleted "last
   occurrence absorbs the remainder" shape, letting `ComparisonValue`
   diverge from the corrected `TotalCost` by up to `step.Quantity - 1`
   copper for a merged step. Converted to the same largest-remainder
   (Hamilton) apportionment `AllocateVendorNodeCosts` uses. Also fixed
   the caller comment describing the deleted shape, a dangling renamed-
   test reference, one (of several) self-contradicting "DO-NOT-TOUCH"
   line, the flagship regression test's explanatory comment, and added
   a three-equal-quantity-occurrence tie-break test. This runtime change
   shipped with no new characterization pin of its own - see item 6.
6. **Review-response commit (this one).** A further review on `0b60ceb`
   found: (a) item 5's `RecomputeComparisonValues` rewrite was still
   unpinned - the only test touching that path asserted the summed
   `ComparisonValue` across occurrences, which is identical under both
   the old and new algorithm for its 2x qty-1 shape, so the actual
   per-occurrence divergence was never exercised; added
   `MultiOccurrenceMergedVendorOffer_ValuedCurrency_
   ComparisonValueDivergesPerOccurrenceUnderOldSharingRule` (two qty-3
   occurrences, currency value 10 not evenly divisible by total
   quantity 6 - old algorithm gives 3/7, new gives 5/5) to close that
   gap. (b) item 5's DO-NOT-TOUCH sweep fixed only one of five stale
   instances (`VendorBatchSolver.cs:873`); the remaining four
   (`VendorBatchSolver.cs` class doc, `PlanSolver.cs` class doc, and two
   more `PlanSolver.cs` call-site comments) still asserted the merged-
   ceil arithmetic was frozen/unchanged when this stream had already
   rewritten it - corrected all four to note the 2026-08-17 retirement
   instead. (c) both largest-remainder apportionment sites
   (`AllocateVendorNodeCosts` and `RecomputeComparisonValues`) multiply
   a `long` total by an occurrence's `int` quantity without an overflow
   guard; on a large-enough total the product silently wraps negative,
   breaking the "shares always sum to the total" invariant both doc
   comments assert unconditionally. Widened both multiply/divide sites
   to `decimal` (whose range comfortably covers any `long` x `int`
   product either field's own type can hold), removing the overflow
   risk entirely rather than just documenting it as a limitation.

**Validation performed:**
- Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
  C:/Dev/Blish/wt-qceil/GW2CraftingHelper.csproj -p:Platform=x64` -
  0 errors. StyleCop warnings are pre-existing project-wide (1789 at
  HEAD, none new in this commit's own diff), but MEASURED across the
  whole `merged-ceil-remainder` stream (ce64423 baseline vs. this
  commit's HEAD, full rebuild): `PlanSolver.cs` 134->142,
  `VendorBatchSolver.cs` 50->54, `PlanViewModelBuilder.cs` 158->160 -
  +14 new SA1512/SA1513/SA1515 warnings introduced across the stream's
  five commits (mostly comment-blank-line spacing in the new blocks),
  correcting item 3/4/5's repeated "none in any touched file" claim,
  which was false for this stream from `0b60ceb` onward.
- Tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test
  C:/Dev/Blish/wt-qceil/tests/GW2CraftingHelper.Tests/
  GW2CraftingHelper.Tests.csproj` - MEASURED 1775/1775 green (1774 at
  `0b60ceb` + 1 new per-occurrence characterization test from item 6a),
  correcting the stale "1773/1773" count this entry previously carried
  (actual count at `0b60ceb` was already 1774/1774, one more than
  recorded, from the tie-break test item 5 added).
- Self-review (Code Reviewer Mode) on all runtime-affecting edits: the
  `decimal` widening cannot itself overflow for any `long`/`int` pair
  either field's own type can hold (long max ~9.2e18 x int max ~2.1e9
  ~= 1.98e28, decimal max ~7.9e28); truncation back to `long` after the
  divide is exact since both operands are whole coppers; the new test's
  expected 5/5 vs. the old algorithm's 3/7 was hand-verified against
  both algorithms' own arithmetic before asserting it.

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests
- [x] Tests exercise real production paths (all characterization/
  regression tests are genuine `Solve()`+builder round trips, not
  mirrored logic)
- [x] No fake file I/O tests introduced
- [x] Pricing logic preserves multi-source correctness (Conflict-tier
  currency handling explicitly re-verified unchanged per item 4 above;
  the overflow fix changes no value for any realistic input, only
  removes a wrap-around failure mode)
- [x] IDs remain internal-only (not displayed)

**Risks / follow-ups:** none new. The C6(b) correction (item 4) is
recorded here as the authoritative verdict since no prior claim was
locatable to edit in place; if the orchestrator has the original claim
in a stream this session could not see, that record should be updated
to point back here rather than restate the (incorrect) claim.
`VendorBatchSolver.cs:409`'s comment recounting a past review's own
"one of the six DO-NOT-TOUCH merged-ceil batching methods" wording was
left as-is (it accurately describes history at the time, not a present-
tense claim about current code) - flagged here in case a future sweep
disagrees.

**Merge note (orchestrator, post-review):** `origin/master` had moved
(the `high-evidence-zones` stream immediately above this entry, deleting
5 dead/vacuous tests unrelated to this stream's own changes) since this
stream's own 1775/1775 count above was measured at its pre-merge HEAD
(`81598bf`). Merged with `git merge origin/master`; only this file
conflicted (both streams appended an entry at the same location) and was
resolved both-sides, master-first, as the two entries above. All other
files (`Models/PlanViewModel.cs`, `Services/PlanContentHeightMath.cs`,
`Services/SummarySectionLayoutMath.cs`, `Views/CraftingPlanView.cs`,
`Views/Rendering/NotesSectionRenderer.cs`,
`Views/Rendering/SummarySectionRenderer.cs`, `docs/ARCHITECTURE.md`,
`docs/gw2e-considerations.md`,
`docs/research/gw2e-convergence-matrix.md`,
`tests/GW2CraftingHelper.Tests/Services/PlanContentHeightMathTests.cs`)
merged automatically with no conflict; grepped the merged
`VendorBatchSolver.cs`/`PlanSolver.cs` afterward and confirmed this
stream's largest-remainder/decimal-widened apportionment logic is
present unchanged post-merge. Rebuilt clean (0 errors) and re-ran the
full suite post-merge: 1770/1770 passed - exactly this stream's own
1775 baseline minus the 5 tests `high-evidence-zones` deleted (1775 - 5
= 1770), confirming the two streams' changes compose without
interaction. This 1770/1770 count supersedes the 1775/1775 figure in
this entry's own "Validation performed" section above, which remains
accurate as a historical record of this stream's state at `81598bf`
before the merge.

Gate: not applicable - quorum-verdict cleanup with characterization-first proof where the high-evidence zone was entered; suite-pinned. Merged under the maintainer's standing merge directive (2026-08-16).

**Second merge note (orchestrator):** `origin/master` moved again (PR #131,
`value-detail-pipeline`, the entry immediately above this one - test-and-
docs only, no runtime code touched) while this branch's own PR #130 was
polling CI. Merged with `git merge origin/master` a second time; again only
this file conflicted (both streams appended at the same location),
resolved both-sides, master-first, as the two entries above.
`tests/GW2CraftingHelper.Tests/Services/CraftingPlanPipelineTests.cs`
merged automatically with no conflict. Rebuilt clean (0 errors) and re-ran
the full suite: 1772/1772 passed - exactly the first merge note's
1770/1770 plus the 2 new tests `value-detail-pipeline` added
(`GenerateStructuredAsync_CraftRootWithVendorChildValuedInCuratedCurrency_
VomOn_ValueDetailTooltipFires`,
`GenerateStructuredAsync_CraftRootSelectedAmongMultipleOptions_
ValueDetailTooltipFires`), confirming the three streams' changes compose
without interaction. This 1772/1772 count supersedes both this entry's own
1775/1775 figure and the first merge note's 1770/1770 figure.

## Quality-audit cleanup, phase 1: four bug fixes (B1-B4, 2026-08-17)

Cross-dimension quality-audit triage (comment hygiene / dead code /
duplication / correctness / test hygiene / architecture drift)
identified four behavior-affecting bugs as the highest-priority phase
of a larger cleanup plan - small, independent, each landed as its own
commit on `quality-phase1-bugs`, in the triage's own recommended
order, gates green after every commit:

- **B1** (`Models/PersistedPlan.cs`): `CurrentSchemaVersion` was stale
  at 2 while the persisted graph grew ~275 lines of new fields
  (`CraftingTreeNode`'s `CraftCostBreakdown`/`BuyFromTpCostBreakdown`/
  `BuyFromVendorCostBreakdown`, `PlanSolveContext`'s
  `CompetencyIndependentForceBuyNodeIds`/`UnreducedTree`/
  `AccountItems`/`ActiveCharacterName`, `CraftingPlanResult`'s
  `ExcessCraftOutputs`/`RecipeSheetSavingsOpportunities`/
  `SeasonalVendorTips`, among others) across `CraftingPlanResult.cs`/
  `CraftingTreeNode.cs`/`PlanSolveContext.cs` after the 1 -> 2 bump
  with no matching version bump - the exact silent-default failure the
  schema-version gate exists to reject. Bumped to 3, plus a new
  reflection-based member-set guard test
  (`PersistedPlanSchemaMemberSetTests`) that fails independent of
  whether a future change remembers to bump `CurrentSchemaVersion`,
  and a `LoadLatest_QualityAuditSchemaVersion2File_...` test mirroring
  the existing SchemaVersion-1 rejection test. **User-visible effect
  of this fix, one time only:** the very first module load after this
  change, any plan.json a pre-fix build wrote (SchemaVersion 2) is
  rejected by the existing tolerance gate exactly like every other
  schema mismatch already handles it - one Warn log line, the
  Crafting Plan tab comes up empty instead of restoring, the user
  generates a fresh plan. This is the same known, already-exercised,
  safe fresh-start path the 1 -> 2 bump itself used (see that bump's
  own doc comment in `PersistedPlan.cs`), not a new failure mode; it
  fires once per installation, not on every load.
- **B2** (`Services/PlanStructuralValidator.cs`): four restored lists
  (`CompetencyOpportunities`/`ExcessCraftOutputs`/
  `RecipeSheetSavingsOpportunities`/`SeasonalVendorTips`) were missing
  the per-entry null check every other restored list already has,
  reachable from `PlanViewModelBuilder.BuildNotesSection`'s unguarded
  per-entry dereference. Added the same `NoNullEntries` call already
  used for the other ten lists, plus one corruption test per list.
- **B3** (`Services/RecipeClientFactory.cs`): `MysticForgeRecipeData.
  LoadWarnings` was collected on every seed load and never read; the
  load-failure `catch` swallowed the exception wholesale too. Wired
  both to `ModuleLog.Shared.Write(Warn, "startup", ...)` via an
  optional `ModuleLog` injection parameter (mirroring
  `CraftingPlanPipeline`'s existing pattern), logging a warning COUNT
  only - not the raw warning text, since one `LoadWarnings` category
  embeds a raw item id and a Warn-level `ModuleLog` line is a
  Log-tab-visible surface the item/currency/vendor-id-internal-only
  invariant covers (per `PlanStructuralValidator.NoNullValues`'s own
  precedent for the same tension). `RecipeCount` folded into the same
  line instead of staying unreferenced.
- **B4** (`tools/VendorOfferUpdater/Program.cs`): `MergeWikiCache`'s
  `Unchanged` counter (`existing.Count - refreshed`) could under-report
  or go negative because `refreshed` was incremented against the
  `merged` dictionary the same loop was mutating, so a duplicate
  PageName within one fresh batch double-counted as a refresh of the
  existing cache. Fixed by counting against sets built from the
  original `existing`/`fresh` inputs rather than the mutating
  dictionary; `Merged` output is byte-identical for every non-
  duplicate input. Console-only counter, dev tool, no shipped-plan
  impact.

**Validation (2026-08-17, measured):**
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-qp1/GW2CraftingHelper.csproj -p:Platform=x64` (clean
rebuild) - 0 errors, 1788 warnings, all pre-existing StyleCop findings
unrelated to this change (confirmed no new warnings in any of the four
touched files individually). `"/mnt/c/Program Files/dotnet/dotnet.exe"
test C:/Dev/Blish/wt-qp1/tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` - 1776/1776 green (11 new: B1 adds 3 -
2 `PersistedPlanSchemaMemberSetTests` cases + 1
`LoadLatest_QualityAuditSchemaVersion2File_...` in `PlanStoreTests`;
B2 adds 4 null-entry corruption tests in `PlanStoreTests`; B3 adds 4
`RecipeClientFactoryTests` cases; B4 touches only the updater suite.
Measured after each commit: 1770 after B1, 1774 after B2, 1778 after
B3, consistent with a pre-B1 count of 1765; B1's own commit shipped 5
new tests, and the later follow-up commit b5fe6e6 consolidated its 4
member-set [Fact]s into 2, taking the branch total to its final 1776 -
the "B1 adds 3" breakdown above describes HEAD's tree, not B1's own
commit). `"/mnt/c/Program Files/
dotnet/dotnet.exe" build C:/Dev/Blish/wt-qp1/tools/VendorOfferUpdater/
VendorOfferUpdater.csproj` - 0 errors, 0 warnings. `"/mnt/c/Program
Files/dotnet/dotnet.exe" test C:/Dev/Blish/wt-qp1/tests/
VendorOfferUpdater.Tests/VendorOfferUpdater.Tests.csproj` - 207/207
green (2 new `MergeWikiCacheTests` cases for B4). Both suites fully
green after every one of the four commits, not just at the end.

Gate: PASS (ratified by the orchestrator, 2026-08-17; a subagent had
filled this line and the orchestrator re-judged it rather than letting
the self-fill stand). No live desktop check: B1-B4 touch no rendered
UI surface beyond B3's Log-tab warning line, which flows through the
already-live-gated ModuleLog pipeline, only fires on a corrupted or
incomplete Mystic Forge seed, and so cannot be exercised by a live
sandbox session running on healthy data; the wiring is suite-pinned by
`RecipeClientFactoryTests` against a real `ModuleLog` instance. Module build 0 errors/1788 warnings (clean
rebuild, unchanged), module suite 1776/1776 green, updater build 0
errors/0 warnings, updater suite 207/207 green - re-measured after
fixing this block's own wrong counts above and the retype blind spot
in `PersistedPlanSchemaMemberSetTests` (see that file and
`PersistedPlan.cs` for the fix).

---

## Tree row tooltip composer extraction + architecture doc corrections (2026-08-17)

**Milestone goal:** apply quorum verdict D-2 (TARGETED_FIX_ONLY): correct
and extend `docs/ARCHITECTURE.md`'s TreeSectionController state/render
split entry (record the split as rejected by decision, not deferred;
correct the coupling figure), add a STANDING RULE line for future
tree-row/pill features, extract the Recipe Tree row's extra-tooltip-line
construction into a pure, Blish-free `Services/` composer with real-path
tests, and fold in quorum verdict D-1's targeted `FrameTicker`/line-count
doc corrections plus the `CraftingPlanView.FormatPhaseText` -> `Services/`
move. No `TreeSectionController` state/render split and no
`PlanStripController` were implemented - both are explicitly rejected by
this verdict, not merely out of scope.

**What changed:**
1. **`docs/ARCHITECTURE.md` section 5 (TreeSectionController entry).**
   Added a new "TreeSectionController state/render split: rejected by
   decision, not deferred by oversight" paragraph beside the existing
   TreeSectionController bullet: states the one-owner/one-lifetime
   invariant, the constructed-once-in-`CraftingPlanView`'s-constructor
   fact (`Views/CraftingPlanView.cs` ~614), the preferred future shape
   (Blish-free pure composers under `Services/`, never a class bisection),
   and the measured coupling figure - `TreeSectionController` is
   mentioned by name in 14 production `.cs` files (`Module.cs` plus 9
   `Services/` files and 4 `Views/` files, not counting `Models/`
   shape-mirroring comments or test files), of which 13 are comment-only;
   the actual compile-time coupling is 2 references, both in
   `Views/CraftingPlanView.cs` (the field declaration and the constructor
   call site) - plus, as measured pre-change at `ce64423`, 3 mentions in
   `docs/ARCHITECTURE.md` and `docs/ROADMAP.md` (`docs/KNOWN-ISSUES.md`
   carried 42 more as historical narrative) - not 18. (That doc-mention count is a snapshot, not a live figure: every doc entry that names the class (including this one) adds mentions on landing, so no post-change total is stated here - reproduce the current count with `git grep -c TreeSectionController -- '*.md'`.) (The file count moved
   from a pre-change 13 to 14 because this same milestone's own new
   `TreeRowTooltipComposer.cs` documents its provenance with a
   "TreeSectionController.RenderTreeNode" reference in its own doc
   comment - a real, verified `grep` count taken after the change, not
   the pre-change snapshot.)
2. **`CONTRIBUTING.md` Code Style section.** Added the STANDING RULE: every
   new tree-row/pill feature extracts its pure text/decision computation
   into a tested, Blish-free `Services/` composer BEFORE wiring it into
   `TreeSectionController` - the 8-for-8 proven pattern (now including
   `TreeRowTooltipComposer`), with an explicit note that this is not a
   staged step toward eventually splitting `TreeSectionController` -
   that split is rejected by decision, cross-referenced to the
   `docs/ARCHITECTURE.md` entry above.
3. **`Services/TreeRowTooltipComposer.cs` (new) + real-path tests
   (`TreeRowTooltipComposerTests.cs`, 24 test methods / 28 test cases).**
   Extracted the `extraTooltipLines` build (`Views/Rendering/
   TreeSectionController.cs`, formerly ~726-940) verbatim into a static,
   Blish-free `BuildExtraTooltipLines(node, captionText, currentPlan)`
   method: the qty>1 unit-price line(s) (including the Field-test finding
   B zero-coin-with-currency-cost suppression), the AUDIT ROW 20/38
   TP-price-side-fallback caveat (including the b18fb03 null-plan/
   PriceBasis hazard class - a null `currentPlan` gets a basis-agnostic
   sentence rather than silently reading `null?.PriceBasis` as `false`),
   the Unknown/GuildUpgrade acquisition hint, the receipt/what-if caption
   insert-at-front, and the wiki-link tooltip line. The actual Blish-bound
   right-click event wiring (`RightMouseButtonPressed`/`MouseLeft`/
   `RightMouseButtonReleased`) stays in `TreeSectionController.
   RenderTreeNode`, gated by the same `WikiLinkBuilder.HasWikiPage`
   predicate the composer also calls - calling that cheap pure predicate
   twice per row is deliberate (keeps the composer Blish-free rather than
   threading a bool back out for one call site). `FormatCoin` deliberately
   duplicates `CoinCurrencyRenderer.FormatCoinText`'s format rather than
   referencing it, matching `ValueDetailTooltipBuilder`'s own precedent
   and stated rationale (that class lives in `Views.Rendering` and is
   Blish-coupled). The pills cascade (`RenderDecisionPills`, ~1252-1499
   pre-change) was explicitly NOT touched - out of scope per the verdict
   (interleaved click wiring).
4. **The pills-cascade sibling (`PlanStripTickDecision.cs`) gained
   `FormatPhaseText`.** `CraftingPlanView.FormatPhaseText` (private
   static, ~2976-2982 pre-change, pure - no Blish/instance-state
   dependency) moved verbatim into the existing pure, Blish-free,
   already-unit-tested `Services/PlanStripTickDecision.cs`, alongside its
   sibling status-strip decision `Decide`. No `PlanStripController` class
   was created - rejected by verdict D-1. `CraftingPlanView`'s sole call
   site (`_statusBoard.UpdatePhase(myGen, (int)pe.Phase,
   FormatPhaseText(pe))`) now reads
   `PlanStripTickDecision.FormatPhaseText(pe)`. Six new tests added to
   `PlanStripTickDecisionTests.cs` cover null event, empty display name,
   the Total-present branch, the documented Detail-fallback regression
   case (the first-run "Building recipe tree" hint - the ONLY surviving
   path for that hint once `CraftingPlanView` started passing
   `progress: null` to the old `IProgress<PlanStatus>` channel, per
   `CraftingPlanPipeline.FirstRunTreeHint`'s own doc comment), the
   Total-takes-priority-over-Detail ordering, and the plain-ellipsis
   fallback. Every stale `CraftingPlanView.FormatPhaseText` cross-reference
   left behind by the move (`Services/CraftingPlanPipeline.cs` x2,
   `Services/PlanPhaseEvent.cs`, `tests/.../CraftingPlanPipelineTests.cs`)
   was corrected to `PlanStripTickDecision.FormatPhaseText` (fix the
   class, not the instance) - `docs/KNOWN-ISSUES.md`'s own historical W3B
   narrative entry was deliberately left as-is (an accurate record of
   that milestone's state at the time, not a live cross-reference).
5. **Quorum verdict D-1 fold-in, `docs/ARCHITECTURE.md` section 1
   (`FrameTicker`).** Corrected "three live instances" to "FOUR live
   instances (measured)": `_spinnerTicker` (the W3B status-strip spinner
   ticker, added between the section's original writing and now) is a
   fourth `FrameTicker` field on `CraftingPlanView`, canceled/nulled by
   `StopLiveTickers()` alongside the other three. Added a matching
   follow-up sentence to `docs/KNOWN-ISSUES.md`'s `CraftingPlanView`
   tab-switch hazard-class row (the table entry originally recorded via a
   2026-08-06 review correcting a first sweep pass's "no live race"
   miss) noting the row's own three-ticker count was itself stale by
   the time of this pass, and that `_spinnerTicker` sits in the identical
   hazard class as the other three (not independently verified live -
   same OUT OF SCOPE deferral as the rest of that row).
6. **Quorum verdict D-1 fold-in, `docs/ARCHITECTURE.md` section 5
   (stale line-count figure).** The "~2,802 lines" post-WP-26 figure is
   now explicitly scoped as "at the time WP-26 was cut" rather than
   read as still-current, with a new "Measured current" sentence stating
   `Views/CraftingPlanView.cs`'s real current line count (3,674 lines,
   2026-08-17) and explaining the growth as expected feature/fix landing
   (W3B status strip, currency-ux-package, gate-round fixes, this
   milestone's own extraction) routed through the STANDING RULE on the
   way in, not a regression of the WP-21 through WP-25 decomposition.

**Validation performed:**
- Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
  C:/Dev/Blish/wt-qtooltip/GW2CraftingHelper.csproj -p:Platform=x64
  -t:Rebuild` - 0 errors, 1781 StyleCop warnings, none newly introduced in
  any touched file (`Services/TreeRowTooltipComposer.cs`, `Views/Rendering/
  TreeSectionController.cs`, `Services/PlanStripTickDecision.cs`,
  `Views/CraftingPlanView.cs`, `Services/CraftingPlanPipeline.cs`,
  `Services/PlanPhaseEvent.cs`) - one transient SA1512 warning
  (single-line comment followed by blank line) introduced by the
  `FormatPhaseText` removal was caught and fixed before the final build.
  (This entry previously claimed 1782 pre-existing warnings with none
  newly introduced; an independent `-t:Rebuild` found that claim false in
  two ways. First, the underlying `c1c52e3` commit's "0 warnings, 0
  errors" was an incremental-build artifact - `dotnet build` without
  `-t:Rebuild` reports "Nothing to do" when nothing changed since the
  last build, not a true warning count. Second, a real `-t:Rebuild`
  surfaced 6 warning sites in the two changed pure files:
  `Services/PlanStripTickDecision.cs` (52,65)/(86,69)/(87,36)/(88,51)
  SA1503 and `Services/TreeRowTooltipComposer.cs` (67,18)/(225,14)
  SA1513. Five are the same warnings moved verbatim from their
  pre-extraction locations (the four `FormatPhaseText` single-line `if`s
  and the (67,18) construct, character-identical to `ce64423`'s
  `TreeSectionController.cs:740-744`), so the total warning count was
  unaffected by their relocation. The sixth, (225,14) in the new private
  `FormatCoin`, was on genuinely new code - a multi-line
  `if (copper < 0) { copper = 0; }` missing the blank line after its
  closing brace that `CoinCurrencyRenderer.FormatCoinText`'s single-line
  form never needed - and has been fixed by adding that blank line,
  dropping the true total from 1782 to 1781.)
- Tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test
  C:/Dev/Blish/wt-qtooltip/tests/GW2CraftingHelper.Tests/
  GW2CraftingHelper.Tests.csproj` - 1802/1802 green (baseline 1768 + 34
  new: 28 `TreeRowTooltipComposerTests` cases, 6
  `PlanStripTickDecisionTests` cases; independently re-run and confirmed
  at HEAD). Two tests failed on first run (the two null-caption/null-hint
  composer tests did not account for the default test-fixture item name
  also triggering the wiki-link line) and were corrected before the final
  green run. (This figure previously read 1799/1799 with 25
  `TreeRowTooltipComposerTests` cases; the c1c52e3 follow-up commit added
  3 more branch-coverage cases to that file without updating this record
  - the same stale-validation defect class the ARCHITECTURE.md coupling
  figure above was corrected for.)
- Manual: `git status --short` confirmed no intermediate cache files
  (`ref/wiki_vendor_cache.json`/`ref/item_id_cache.json`) were touched; a
  full-diff ASCII scan (`grep -P '[^\x00-\x7F]'`) over every file this
  pass touched confirmed no non-ASCII bytes (and therefore no em-dashes).

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests (`TreeRowTooltipComposerTests`/
  `PlanStripTickDecisionTests` use only `Models`/`Services` types)
- [x] Tests exercise real production paths (`TreeRowTooltipComposer.
  BuildExtraTooltipLines`/`PlanStripTickDecision.FormatPhaseText` are the
  exact methods `TreeSectionController`/`CraftingPlanView` now call)
- [x] No fake file I/O tests introduced
- [x] Pricing logic preserves multi-source correctness (no solver/pricing
  logic touched - pure text-composition code motion plus documentation)
- [x] IDs remain internal-only (not displayed) - the composer's tooltip
  lines carry only names/amounts, matching the pre-move code verbatim

**Risks / follow-ups:** the `TreeSectionController` pills cascade
(`RenderDecisionPills`) remains un-extracted, unchanged from before this
milestone - explicitly out of scope per the verdict (interleaved click
wiring), a candidate for a future STANDING-RULE-following pass of its
own. `docs/ROADMAP.md`'s own `TreeSectionController` mention was not
independently re-verified against the new coupling figure beyond the
`grep` count already cited above.

Gate: not run live this pass - pure extraction of tooltip composition
with the emitted line content pinned by the suite (1802 green at the
stream's verification, re-run post-merge below); the visual surface is
unchanged by construction, and the next desktop gate batch covers
tooltips incidentally through its deferred value-detail hover check.
Merged under the maintainer's standing merge directive (2026-08-16).

## Annotation-detection: post-solve advisory-list characterization tests + B8 shape fixes (2026-08-17)

**Milestone goal:** quorum verdict D-3 (TARGETED_FIX_ONLY). A mutation
deleting all four post-solve annotation-pass calls
(`CompetencyOpportunityCalculator.Apply`/`ExcessCraftOutputCalculator.
Apply`/`RecipeSheetSavingsCalculator.Apply`/`SeasonalVendorTipCalculator.
Apply`) at the multi-item generation site and inside `ResolveWithOverrides`
left the suite green at 1765 - none of the existing coverage asserted on
those four `CraftingPlanResult` properties from either of those two call
shapes, only from the single-item `GenerateStructuredAsync` path. Close
that gap with characterization tests, apply the accompanying B8 shape
fixes, and document the whole annotation-pass group in
`docs/ARCHITECTURE.md`.

**What changed:**
1. **Annotation-detection characterization tests
   (`tests/.../CraftingPlanPipelineTests.cs`).** Six new tests, real
   pipeline paths throughout (no mocked calculators): three pin the exact
   mutation described above -
   `GenerateStructuredAsync_ListOverload_MultiItem_
   PopulatesAllFourAdvisoryLists` (via the public list overload at
   `CraftingPlanPipeline`), `ResolveWithOverrides_SingleItemContext_
   PopulatesAllFourAdvisoryLists`, and `ResolveWithOverrides_
   MultiItemContext_PopulatesAllFourAdvisoryLists` - each asserting
   `NotNull` on all four advisory lists (every one of the four
   calculators unconditionally assigns its own property, empty list or
   not, once called - a plain `NotNull` is therefore a precise, minimal
   wiring proof; calculator CONTENT correctness stays with the dedicated
   `*CalculatorTests` classes, not duplicated here). A fourth test,
   `GenerateStructuredAsync_ListOverload_SingleItem_
   RoutesToSingleItemPath_NotMultiItemWrapper`, pins the list overload's
   own dispatcher invariant (items.Count == 1 routes to the untouched
   single-item overload, never the multi-item wrapper path). A fifth,
   `NullOffersForItemDelegate_NoOffersSource_EmitsNothing_NoCrash`
   (`RecipeSheetSavingsCalculatorTests.cs`), pins the B8 null-delegate
   guard below. **Verified against the actual mutation**: temporarily
   commented out the four `Apply` calls at both sites, rebuilt (0
   errors), ran the full suite - exactly the three targeted tests failed
   (`Assert.NotNull() Failure: Value is null`), the other 1770 stayed
   green - then reverted and confirmed 1773/1773 green again. A sixth
   test, `GenerateStructuredAsync_RecipeSheetSavings_EndToEnd_
   PopulatesOpportunity`, was added later (gate finding 1, 2026-08-17
   correction commit) - see item 3 below.
2. **B8: `SellSideEconomics.ApplyForPlanShape` self-dispatch
   (`Services/SellSideEconomics.cs`).** `ResolveWithOverrides` used to
   branch on `context.Tree.Id != Gw2Constants.MultiItemWrapperItemId`
   itself to pick `ApplySellSideEconomics` vs
   `ApplyBatchSellSideEconomics`. That if/else now lives inside
   `SellSideEconomics` as `ApplyForPlanShape`, using the SAME
   `Gw2Constants.MultiItemWrapperItemId` constant - `ResolveWithOverrides`
   calls the one new method instead of duplicating the shape check.
   **Correction (gate 2026-08-17): not a pure move.** The constant is
   unchanged but the OPERAND is not: the deleted if/else read
   `context.Tree.Id` (the frozen, generation-time tree); the new call
   passes `solveTree`, which is `reduced.ReducedTree` - a fresh
   `InventoryReducer` clone - whenever `context.UnreducedTree != null &&
   _reducer != null`. Equivalent today only because
   `InventoryReducer.CloneNode` copies `Id` onto the clone and the
   wrapper root is never pruned - an invariant nothing asserts. The new
   `tree != null &&` guard does NOT change null-tree behavior: a null
   tree used to throw NRE at `context.Tree.Id`; it still NREs, one frame
   deeper, at `itemRoot.NodeId` inside `ComputePerItemEconomics` (MEASURED
   via a temporary probe test, since reverted). The guard is defensive
   only, and the state is unreachable in production anyway -
   `ResolveWithOverrides` passes the same `solveTree` to `_solver.Solve`
   and `BuildCraftingTreeResult` before this call. See
   `SellSideEconomics.ApplyForPlanShape`'s own doc comment for the
   corrected description.
3. **B8: `RecipeSheetSavingsCalculator.Apply` narrowed to
   `Func<int, IReadOnlyList<VendorOffer>>`
   (`Services/RecipeSheetSavingsCalculator.cs`).** The `vendorOfferStore`
   parameter (a full `VendorOfferStore`) is replaced with `offersForItem`
   - the one method (`GetOffersForItem`) this calculator ever called on
   it. The old `vendorOfferStore != null` gate is now a null-delegate
   guard with the identical meaning ("no offer source available -> emit
   nothing"). `CraftingPlanPipeline` computes the narrowed delegate once
   in its constructor (`_offersForRecipeSheetItem`, null when
   `vendorOfferStore` is null) and threads it to all three call sites.
   Added a defensive `?? Array.Empty<VendorOffer>()` on the delegate's
   return in `TryEmit`: `VendorOfferStore.GetOffersForItem` itself never
   returns null, but the parameter is now caller-suppliable rather than
   bound to that one class's own contract. `RecipeSheetSavingsCalculatorTests.
   cs` keeps its real, temp-directory-backed `VendorOfferStore` fixture
   (repo invariant: real stores with temp dirs, never a fake) - only the
   17 call sites' argument changed, from `vendorOfferStore: store` to
   `offersForItem: store.GetOffersForItem` (a plain delegate over the
   same real store).
   **Correction (gate finding 1, 2026-08-17):** this narrowing moved the
   offer source onto a previously-unpinned `CraftingPlanPipeline` field,
   `_offersForRecipeSheetItem`, computed once in the constructor. Nothing
   in the suite asserted on its content: replacing the whole assignment
   with `_offersForRecipeSheetItem = null;` left all 1773 pre-existing
   tests green (verified), silently disabling every recipe-sheet-savings
   note in production with no crash. Closed with an end-to-end pipeline
   test, `GenerateStructuredAsync_RecipeSheetSavings_EndToEnd_
   PopulatesOpportunity` (real temp-directory `VendorOfferStore` plus a
   non-empty `recipeSheetItemIdByRecipeId`, no fakes), asserting
   `RecipeSheetSavingsOpportunities` is non-empty with correct content,
   not merely `NotNull`. Verified against the mutation: reintroducing
   `_offersForRecipeSheetItem = null;` fails this test; reverting
   restores 1774/1774 green.
4. **`docs/ARCHITECTURE.md` Section 10, "Post-solve annotation passes."**
   New section naming the four calculators and their one-collection-each
   contract, why `SellSideEconomics` is adjacent but not a member (it
   writes displayed totals, not an advisory Notes list), the three
   producer wiring sites plus the fourth (consumer) edit site in
   `PlanViewModelBuilder.BuildNotesSection`, and that the SellSide-first/
   Competency-last call order is convention (kept identical for
   readability), not a data dependency - every pass reads only the
   already-built display tree, never another pass's output. Explicitly
   notes there is no `ApplyAll` seam collapsing the three producer calls
   - rejected on review as premature (quorum verdict D-3): the four
   calculators do not share a signature.

**Validation performed:**
- Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
  C:/Dev/Blish/wt-qannot/GW2CraftingHelper.csproj -p:Platform=x64` - 0
  errors (1782 pre-existing StyleCop warnings, none new).
- Tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test
  C:/Dev/Blish/wt-qannot/tests/GW2CraftingHelper.Tests/
  GW2CraftingHelper.Tests.csproj` - 1774/1774 green (baseline 1768 + 6
  new tests listed above).
- Mutation verification (see item 1 above): the exact judge's mutation
  (deleting all four `Apply` calls at the multi-item and
  override-resolve sites) reproduced by hand, rebuilt, and run - exactly
  the three targeted characterization tests failed, no others; reverted
  and re-confirmed 1774/1774 green.

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests
- [x] Tests exercise real production paths (real `CraftingPlanPipeline`/
  `PlanSolver`/`RecipeService`/`VendorOfferStore` throughout, no mocked
  calculators)
- [x] No fake file I/O tests introduced (`RecipeSheetSavingsCalculatorTests`
  keeps its real, temp-directory-backed `VendorOfferStore`)
- [x] Pricing logic preserves multi-source correctness (no solver/pricing
  logic touched - annotation-pass wiring, a parameter-type narrowing, and
  a dispatch move only)
- [x] IDs remain internal-only (not displayed)

**Risks / follow-ups:** none identified beyond what is already itemized
above; the B8 narrowing's defensive `?? Array.Empty<VendorOffer>()` guard
is currently unreachable from any production caller (only
`VendorOfferStore.GetOffersForItem`, which never returns null, is ever
wired in) - kept as cheap, correct defense-in-depth now that the
parameter type accepts an arbitrary delegate, not dead code removal
material.

Gate: not run live this pass - annotation-calculator wiring and B8
shape fixes carry no new rendered surface; the previously-unpinned
call shapes are now suite-pinned by the characterization tests this
branch adds (the mutation that silently deleted all four annotation
passes now fails the suite). Merged under the maintainer's standing
merge directive (2026-08-16).

---

## Quality-audit phase 2: safe-mechanical batch (quality-phase2-mechanical)

Triage source: quality-audit-triage.md sections A1/A3/A4/C/E, verified at
master e21a280; every site re-located and re-verified against d1092f5
before editing (10 PRs of drift). Six commits, build + module suite
green after each; the sixth fixes review findings against the first
five (two stale method-name repoints, a dangling "Cap data" anchor
retargeted to dev-notes/HISTORY.md, the m37-r1..r4 duplicates dropped).

**A1 factual comment/doc fixes (8 of 9 applied):**
- A1.1 SnapshotFailureClassifier "only place" claim corrected (its own
  Classify(Exception) overload also derives a type name)
- A1.2 CraftingPlanResult + CompetencyOpportunity repointed from the
  never-existent CraftingPlanPipeline.BuildCompetencyOpportunities to
  CompetencyOpportunityCalculator.Apply
- A1.3 ShoppingColumnMath repointed to
  ShoppingListSectionRenderer.Render (the pre-scan's actual home;
  CreateShoppingListBody survives only as the CraftingPlanView method
  the Render body was moved from)
- A1.4 ARCHITECTURE.md: literal CraftingPlanView line count (3 lines
  stale again at re-check) replaced with non-rotting wording
- A1.5 SKIPPED - already fixed on master (doc says FOUR instances)
- A1.6 AccountIndex -> AccountItems at all three comment sites
- A1.7 PillSubduingTooltipBuilder comment now says the two FormatCoins
  share the split, not the format
- A1.8 VendorBatchState reverted-ratchet history rewritten as a
  do-not-re-add warning (15 lines -> 8)
- A1.9 PlanSolver reallocation-guard comment extended: skipped
  fallback-tier decisions keep pre-correction ComparisonValue
- C4 trapped symbols and C6(a): SKIPPED - already fixed by merged PRs

**A3 dead-symbol deletions (measured, repo-wide grep re-verified):**
ModuleSettings.ResetToDefaults, RecipeService.CacheStats,
InMemoryRecipeCacheStore.GetAllSearches/GetAllRecipes,
CraftingPlanView.RarityFramedIconOuterSize, Harness Percentile,
MysticForgeSeeder WikiRecipeClient.RequestCount, the 2-arg
ItemSearchProviderFactory.Create (5 test call sites ported to 3-arg),
and 3 stale System.Linq usings. SKIPPED: PlanStoreHelpers' System.IO
using - triage claim wrong, the file throws InvalidDataException
(caught by the build gate, restored before commit). Kept per triage:
ValueOwnMaterials, ScrollDiagnosticsEnabled, TierByMaterialId.

**A4 test fixes (6 items):** absolute-value pins replacing the two
identical-IL default-argument comparisons; Homestead Exordium-shape test
now walks a real non-Homestead offer so the tier gate is actually
reached; the four "v=" substring asserts upgraded to a date-shape regex
(v=latest now fails); WikiLinkBuilder agreement test made a
discriminating Theory; ZojjasClaymore fixture renumbered to a fake
9001+/9101+ range and marked synthetic (real-ID collision sites for
46742 verified untouched). Module suite 1823 -> 1827 (Theory expansion).

**D-4 + C2:** /mnt/c/Dev/Blish/m38-plan committed at
docs/dev-notes/m38-plan/ (9 top-level docs + 1 json snapshot +
9 proposals); the four dangling anchors in ModuleLog.cs and
CraftingPlanView.cs retargeted to the committed paths. The m37-r1..r4
research records were dropped from the copy: no anchor points at them,
and docs/research/ already holds the canonical versions (corrected
Lumber Mill tables, machine-local paths scrubbed) which the stale
copies contradicted.

**C1 + C3 (former-frozen files, comment-only):** PlanRelayoutMath mirror
comment retargeted to SummarySectionRenderer.CreateFormulaBand (which
absorbed the old CreateCostTileRow's geometry); the
21-line EvaluateVendorOffers reviewer exchange compressed to 4 lines
(three load-bearing facts kept); nine review-round prefixes stripped;
FinalizeVendorBatches reverted-branch history compressed to a pointer at
VendorBatchState; the rotting PlanSolver.cs:1062 citation dropped.

**Final counts (measured):** build 0 errors; module suite 1827/1827;
updater suite 207/207; RecipeSeeder suite 3/3.

Gate: PASS (comment/doc/test-only batch plus dead-symbol deletions
with re-verified zero references; no rendered surface changed, so no
desktop gate applies; evidence is the per-commit build/suite record
above and the verification pass at 9022c9b - module 1827/1827, updater
207/207, build 0 errors).

---

## Comment-minimalism sweep (repo-wide, comment-minimalism-sweep branch)

Repo-wide pass applying the maintainer's comment bar: a comment survives
only if it states a constraint the code cannot express, once, in 1-2
lines. Review-round narration, gate/round markers, dates, milestone
tags, and superseded-design retellings deleted; genuine invariants kept
trimmed at one canonical site. Comments and blank lines only - verified
code-identical (comment-stripped before/after diff clean per commit).

**Per-area line deltas (files touched, comment lines +added/-removed;
measured via `git diff --numstat` against the master merge-base,
including the findings-fix and verification passes below):**

- Services (incl. Diagnostics/Recipes): 75 files, +1877 -4658
- Models: 31 files, +369 -853
- Views (incl. Rendering): 25 files, +855 -1682
- Module.cs: 1 file, +149 -426
- Contracts: 1 file, +4 -4
- tools (VendorOfferUpdater/RecipeSeeder/Harness/MysticForgeSeeder):
  9 files, +99 -175
- tests (module + updater suites): 118 files, +569 -617
- Total: 260 files, +3,922 -8,415, net -4,493 lines. 12 touched files
  had non-ASCII bytes (em-dashes/arrows/box glyphs) converted to ASCII,
  including Contracts/IItemSearchProvider.cs (missed by the original
  sweep, fixed in the findings-fix pass); a repo-wide
  `grep -P '[^\x00-\x7F]'` over every `.cs` file now returns zero
  hits. The flagged InMemoryRecipeCacheStore em-dash was already fixed
  on origin/master - the remaining instances of that class were in
  Services/Recipes and tests, all fixed here.

**Invariants relocated / canonicalized (from -> to):**

- CompetencyOpportunityCalculator.Apply rationale: three byte-identical
  4-line blocks in CraftingPlanPipeline reduced to one-line
  "annotation-only" notes; canonical contract stays on the calculator's
  own class doc.
- CraftingPlanPipeline Solve()-overload twin blocks (Step 5.5/5.6,
  seasonal filter, force-buy pre-pass, metadata widening): stated once
  at the single-item overload; multi-item overload reduced to "see the
  single-item overload's matching X" pointers.
- PlanSolver comparability-tier rationale: canonical at
  Decision.ComparisonValue/HasUnvaluedCurrency and the Evaluate
  vendor/recipe tier blocks; restatement sites (SolverDecision,
  CraftingTreeNode, CraftingPlanResult, CompetencyOpportunity,
  CompetencyOpportunityCalculator) trimmed to short passthrough notes.
- AccountCurrencyIndex: gw2efficiency provenance litigation deleted;
  the display-only/never-affects-decisions invariant kept as the class
  doc.
- DecisionPillPlanner: three successive pill-wording narrations reduced
  to the current wording only.
- No invariant was orphaned: every deletion either had a surviving
  canonical statement (owning class doc, ARCHITECTURE.md sections 4/5/7/8,
  or this file) or was process metadata with no invariant content.

**Kept intact (verified):** PlanContentHeightMath's one-level-per-frame
AutoSize convergence class doc; WheelDeltaSanitizer's root-cause/
threshold contract doc; ISectionRelayoutSink's seam contract;
CurrencyDecisionDefaults' MIT permission notice; scroll
preserve-restore-verify invariants in CraftingPlanView (comment edits
only, no code touched in the high-evidence zones).

**Flagged suspect claims (not rewritten; verify separately):** none
found - no surviving comment's technical claim was determined false
during the sweep. Two former false claims were already self-documented
as corrected upstream (Gw2Constants festival-table scoping,
VendorOfferUpdater round-trip save ordering) and were compressed to
their corrected form.

**Findings-fix pass (post-review):** the review of the sweep found the
original commits had (a) missed Contracts/ entirely (em-dashes in
IItemSearchProvider.cs), (b) reported three mutually inconsistent
line-delta totals (the numbers above are now measured, not summed by
hand), (c) left review-narration labels ("gate finding", "fix-pass",
round markers, dates) and M##/WP-## process tags in both touched and
untouched files, and (d) in a few places dropped a KNOWN-ISSUES pointer
while keeping the milestone tag. All four were corrected in one
follow-up commit: milestone/process tags and review-round narration
removed repo-wide from `.cs` comments (doc-section pointers such as
"KNOWN-ISSUES #26" or "docs/KNOWN-ISSUES.md's W4A entry" kept), dates
removed from comments, and the dropped pointers restored. The pass was
comment-only (every changed line is a comment or a `#region` label).

**Validation:** build 0 errors after every batch; module suite
1827/1827 after every batch (and after the findings-fix pass); updater
suite 207/207 after the tools and tests batches (and after the
findings-fix pass).

Gate: PASS (comments and whitespace only - the review pass verified
the diff contains no code-token change, so no desktop gate applies;
evidence is the no-semantic-change check plus both suites green at the
verification pass - module 1827/1827, updater 207/207, build 0
errors).

## Quality-audit phase 3: structural dedup (quality-phase3-dedup)

Two structural deduplications, no behavior change intended; module
suite 1827/1827 after each commit (zero count change).

**B5+B12+B7 - CraftingPlanPipeline shared pipeline body (commit 1):**
GenerateStructuredAsync (single-item) and GenerateStructuredMultiAsync
each duplicated Steps 2-through-return (~230 lines). Both now keep only
Step 1 (tree build - PlanPhaseTimingSummary keys on the "Build recipe
tree"/"Build recipe trees" phase-label literals, so those stay in the
callers) and delegate to one private RunPipelineAsync. Pure code
motion: the single-item body was absorbed in place; a mechanical diff
of the shared body against each old body shows only the parameterized
divergence hunks. Divergence hunks eliminated (now parameterized once):
Step 8 metadata-id add (targetItemId vs foreach items),
result.RequestedItems, the crafting-tree build (B12: the single-item
path's inlined tree build now goes through the existing
BuildCraftingTreeResult helper; its else-branch was
argument-for-argument the inlined code and its MultiItemRoots = null
write is a no-op on a fresh result), the SellSideEconomics dispatch
(per-path calls preserved exactly; the single/multi asymmetry remains
a known hazard, out of scope here), and the 27-field PlanSolveContext
initializer + five-call annotation block (B7 - previously two copies
differing only in TargetItemId/Quantity/RequestedItems). Phase-event
ordering, the ObserveFault/currencyTask concurrency shape, and the
assignNodeIds:!useForceBuyPrePass coupling moved together unchanged.

**B6 option (a) - InventoryReducer flat overload deleted (commit 2):**
the flat Reduce(RecipeNode, Dictionary<int,int>, guide) overload +
ReduceNode were a complete second reduction implementation with no
production caller (both CraftingPlanPipeline sites call the
AccountItemIndex "sourced" overload), including a ~46-line
zero-owned-guide/recipe-rescale tail duplicated from
ReduceNodeSourced. Deleted; canonical VOM doc comments (including the
KNOWN RESIDUAL note) moved onto the surviving sourced members. Port
stats: 28 flat call sites, all in InventoryReducerTests.cs (repo-wide
grep found no others), ported with intent preserved; 6 ports that
would have exactly duplicated an existing Sourced_ twin split the same
owned total across two sources instead. Re-pinned flat-specific
assertions: two trivially-true external-pool checks (flat Reduce
copied the caller's dictionary) replaced with UsedMaterials pins
(CraftableFullyOwned, CurrencyNodes_NeverConsumed); Assert.Null(
Sources) pins re-pinned to production sourced behavior (single-source
allocation listing; same-source-across-branches allocation merging);
the flat-vs-sourced equivalence test became
Sourced_SourceSplitInvariance_SameQuantityResults with an absolute
quantity anchor. [Fact] count 51 -> 51; suite total 1827 unchanged.
Follow-up (review finding): the deletion sweep covered call sites
only; 6 comment references to the deleted ReduceNode symbol (in
AchievementBitDedupPrePass.cs, AchievementBitDedupPrePassTests.cs,
InventoryReducerTests.cs, MultiItemPlanTests.cs) plus one live
KNOWN-ISSUES pointer were retargeted to ReduceNodeSourced, where the
cited behavior now lives. Historical records (HISTORY.md, the gw2e
convergence matrix, and this section's own deletion narrative) keep
the old name deliberately.

Gate: PASS (structural dedup with measured move-purity - the review
diffed the shared body against both original paths and found only the
four parameterization hunks, and three targeted reducer mutations were
killed by the ported tests; no rendered surface changed, so no desktop
gate applies; suite 1827/1827 and build clean at the verification
pass).

## Quality-audit phase 4a: PlanSolver best-recipe tracker (B9, quality-phase4a-tracker)

**Target:** PlanSolver.Evaluate's recipe-selection loop carried four
copies of the "improve best, tie-break on lowest RecipeId" block over
16 parallel locals (bestComparable*/bestFallback*/
bestCompetentComparable*/bestCompetentFallback*) - the same
parallel-locals shape whose cost/id desync CraftAutoPickCandidate's
doc records.

**Characterization coverage (commit 1):** measured by mutation - ten
mutations (RecipeId tie-break inversion, strict-to-non-strict
improvement swap, and fallback craftCost-for-craftRealCost ranking
desync, each per applicable block) ALL survived the pre-existing
1827-test suite; none of the four tiers' tie-break or ranking behavior
was pinned. Added PlanSolverRecipeSelectionTieBreakTests (10 tests,
real Solve() paths): comparable-tier ties built from a valued-currency
recipe (equal comparison cost, divergent real cost), fallback-tier
desync from an ingredient whose committed ComparisonValue (100)
diverges from TotalCost (50), override-path variants for the raw
(non-competent) bests that only the manual-Craft commit sites read.
One finding en route: RecomputeComparisonValues overwrites every Craft
decision's ComparisonValue post-solve (fallback tier forced to
TotalCost), so the fallback cost slot's only unrecomputed observable
is its RANKING role - the desync tests pin that, not the erased
stored value.

**Mutation kill table (per block: fallback F, competent-fallback CF,
comparable C, competent-comparable CC):**

| Mutation | Before (1827 suite) | After commit 1 | After commit 2 (re-expressed vs tracker) |
|---|---|---|---|
| Tie-break invert F/CF/C/CC | survived x4 | killed x4 (2 fails each) | killed (single Offer site, 8 fails) |
| Strictness swap F/CF/C/CC | survived x4 | killed x4 (1-2 fails each) | killed (single Offer site, 4 fails) |
| Fallback craftCost desync F | survived | killed | killed (call-site arg swap) |
| Fallback craftCost desync CF | survived | killed | killed (call-site arg swap) |

**Refactor (commit 2):** one private nested struct BestRecipeTracker
(Cost/RealCost/RecipeId/Option + Offer(cost, realCost, recipe))
replaced the four blocks; four tracker locals replaced the 16
parallel locals. Preserved exactly: comparison order (strict
improvement, then lowest-RecipeId tie), the fallback tier passing
craftRealCost for BOTH Offer slots, one AccountCanCraft evaluation
per recipe (hoisted; verified still the always-evaluated first
operand at current HEAD before hoisting). VendorBatchSolver's
merged-ceil region untouched (high-evidence zone, out of scope).
Build 0 errors, warning count 1745 -> 1744; suite 1837/1837 green.

Gate: PASS (solver-internal refactor with characterize-first proof -
the tie-break mutations that survived the baseline suite are killed by
the new characterization tests both before and after the tracker
refactor, per the mutation table above; no rendered surface changed,
so no desktop gate applies; review found zero blocking findings).

## Quality-audit phase 4b: pure parameter bundling (B10, quality-phase4b-bundling)

Three private-surface signature refactors, one commit each. Zero
behavior change: contexts are private sealed classes with get-only
properties, constructed once per top-level call from the same
expressions at the same point the old calls evaluated them. No public
surface changed; no test edited; suite 1837/1837 green after each
commit.

| Method | Params before | Params after |
|---|---|---|
| PlanSolver.Evaluate | 15 (node + 14 threaded) | 2 (node + EvaluateContext) |
| PlanSolver.Collect | 10 + ref int | 2 + ref int (node + CollectContext) |
| CraftingTreeBuilder.BuildNode | 10 | 3 (node + BuildContext + insideReferenceBranch) |
| CraftingTreeBuilder.BuildChildren | 10 | 3 (recipe + BuildContext + insideReferenceBranch) |
| CraftingTreeBuilder.BuildVendorCostComponentLeaves | 6 | 3 (parentNodeId + decision + BuildContext) |

Bundled vs kept as parameters:

- **EvaluateContext** (14 fields): prices, vendorOffers, memo,
  priceBasis, overrides, currencyValuation, forceBuyOnlyNodeIds,
  competencyIndependentForceBuyNodeIds, costDiagnostics,
  rawCraftCostDiagnostics, ignoredItemIds, homesteadTiers (normalized
  `?? Default` in the ctor, preserving Evaluate's old defensive
  normalization), bestRatingByDiscipline, ownedQuantityUsedByNode.
  Kept: `node` (varies per recursive call).
- **CollectContext** (8 fields): memo, stepMap, currencyMap,
  craftOrder, vendorBatchTracking, vendorOccurrences,
  craftOccurrences, ignoredItemIds. Kept: `node` (varies per call)
  and `ref int craftCounter` (mutable accumulation stays a ref
  parameter, not context state). AggregateStep's signature unchanged.
- **BuildContext** (8 fields): decisions, metadata, hints,
  ownedQuantityUsedByNodeId, ignoredItemIds, currencyMetadata,
  ownedCurrencyAmounts, ownedVendorItemAmounts. Kept: `node`/`recipe`
  and `decision` (vary per call), `insideReferenceBranch` (flips to
  true inside reference branches). BuildTree's public signature is
  byte-identical.

Call-site claims re-verified at HEAD before acting: Evaluate had
exactly 2 call sites (Solve + self-recursion); Collect had 3 (Solve +
2 self-recursion sites); the CraftingTreeBuilder build methods are
private with BuildTree as the only entry point.

Build 0 errors each commit; suite 1837/1837 green each commit (zero
count change).

Gate: PASS (private-signature refactor with zero behavior change -
review confirmed public surfaces byte-identical, no test edits, and
the classic bundling hazards absent; suite held at exactly 1837; no
rendered surface changed, so no desktop gate applies).

## Desktop gate batch: value-detail closure + partial currency coverage (2026-08-17, orchestrator live session)

Sandbox at master 51bdf88 (m38-final copy, preflight settings, Paint
dummy, PID-scoped teardown).

**Value-detail hover: CLOSED, both directions verified live.**
- Deldrimor Steel Ingot plan: the chosen path TP-buys both ingot
  children, so the shard-bearing branch (Philosopher's Stone under the
  dust promotion) is subdued/unchosen and the root's decision carries
  no currency. Root CRAFT hover shows base tooltip only - CORRECT
  suppression, matching the pipeline-level correct-by-design verdict.
  The unchosen vendor leaf itself renders the full value-detail block
  when hovered (gold 0, Currencies 36s = 1 Spirit Shard at the curated
  3600c, Optimization 36s), proving the builder fires live.
- Mystic Clover x77 plan (chosen path rich in currencies): root CRAFT
  hover renders the full value-detail block live - "Crafting gold
  price: 41g 26s 80c / Currencies: 143g 64s 0c / Optimization price:
  184g 90s 80c" - arithmetic exact. POSITIVE LIVE RENDER. The
  2026-08-16 "live miss" is thereby explained: that plan's chosen
  subtree was gold-only, and the hover correctly showed only the base
  tooltip. No code defect; no log-line instrumentation needed.

**Partial currency coverage verified live** (previously only the
full-coverage collapsed HAVE form was seen): the currency table
rendered Spirit Shard Required 244 / Have 50 / Needed 194 with
Blue Prophet Shard and Fractal Relic rows at 0 holdings. The tree-leaf
HAVE pill partial variant had no reachable surface in either tested
tree (currencies render in price columns and the table; no currency
leaf rows exist in these shapes) - same reachability status as before,
not a failure.

**Incidental re-verifications:** VOM consumed the 30 owned Mystic
Clovers (Used Materials section; qty 77 -> root 47x); merged-ceil
vendor math live (912 Philosopher's Stones priced 92 shards = ceil of
91.2); vendor unit-price tooltip ("Unit price: 1275 for 152" with
currency icon) and the "Right-click: Open wiki page" affordance line;
W3B phase text + spinner ("Fetching item details (27 items)");
dated plan timestamps and dated Log-tab rows (a store WARN for the
stale sandbox plan.json rendered with full date, correctly routed);
coin and currency icons right of numbers throughout; shopping list
four-row format with per-currency Each/Total columns.

**Still unreachable:** GuildUpgrade pill/label visuals (no
guild-decoration output is plannable from the search list - unchanged
since the 2026-08-16 partial pass; rides the next natural
opportunity).

## Backlog cleanup batch (B8/B11/B13/B14/B15 + solver ctor hardening, backlog-cleanup)

Six commits on backlog-cleanup off master 9b63594, one per item. All
audit line references were relocated by symbol/content and re-verified
at HEAD before acting.

- **B8 close-out (SellSideEconomics dispatch):** the generation path's
  hand-rolled `items == null` if/else now routes through the existing
  ApplyForPlanShape sentinel dispatch (Tree.Id vs
  Gw2Constants.MultiItemWrapperItemId), matching ResolveWithOverrides
  and the calculator family; calculator order unchanged (SellSide
  first, Competency last). The discriminator agreement (single-entry
  list -> single-item path) is now pinned by a new MultiItemPlanTests
  case. ApplyForPlanShape's self-contradicting "centralized so no call
  site needs its own copy" doc updated.
- **B13 (seed-loading dedup):** Module.cs's three byte-identical
  static-seed load blocks collapsed into LoadSeedOrNull<T> (broad
  catch (Exception) kept - seed failure must never block module load).
  The three seed services now share JsonSeedReader.Deserialize<T>,
  catch narrowed to read+parse only; re-verified first that each row
  loop is property copies + integer comparisons that cannot throw.
- **B15 (seeder concurrency):** both hand-rolled SemaphoreSlim +
  Task.WhenAll blocks in RecipeSeeder replaced with the module's
  BoundedConcurrency.ForEachAsync; empty-list early-return matches
  WhenAll-of-nothing, CancellationToken.None matches parameterless
  WaitAsync. Seeder builds; seeder suite 3/3, updater suite 207/207.
- **B11 (coin split):** CoinSegmentMath.Split(long) added with the
  negative-clamp every site had; SEVEN sites repointed (audit's ~6 had
  drifted - TreeRowTooltipComposer gained a copy). The two tooltip
  formats stay deliberately different; BuildCoinSegments show/hide/D2
  logic untouched. Split pinned by tests incl. boundaries and negative.
- **B14 (save-row dedup):** four byte-identical save-row builders in
  SettingsTabContent collapsed into AddSaveRow(panelWidth, onSave)
  returning the status Label; invoked at the same points, so control
  order is unchanged. Identity evidence: the four builders normalized
  (names -> placeholders) hash md5-identical, and the helper is that
  same byte sequence parameterized. HONEST DEFERRAL: the one-look
  visual check of the Settings tab has NOT been performed - deferred
  to the next desktop session.
- **EvaluateContext ctor hardening:** the single construction site in
  PlanSolver.Solve now names all 14 arguments (three same-typed
  ISet<int> params were a silent-transposition hazard); no signature
  change.

Validation per commit: module build 0 errors; module suite green
throughout - 1837 baseline -> 1838 after B8's new test -> 1846 after
B11's Split tests (both increases are new tests, zero regressions).
Updater 207/207 and seeder 3/3 after the tools change. Note: the
seeder DOES have a test project (tests/GW2CraftingHelper.RecipeSeeder.
Tests, 3 tests), contrary to the batch brief.

Gate: PASS (orchestrator live desktop session, 2026-08-17 late,
sandbox at this branch's own build). B14's deferred visual check ran
and PASSED: all four settings save rows (Currency Valuations,
Homestead Refinement, Logging, Snapshot) render identically styled at
their sections' ends, and a live Save click produced the green
"Saved - <dated timestamp>" status label. The other five items carry
no rendered surface beyond suite coverage (B8/B13/B11 pinned by
tests incl. the new dispatcher-invariant and Split tests; B15 is
tool-only). Session note: the settings Clear-checkbox suppress
tooltip and the curated defaults (Spirit Shard 3600, Pristine Fractal
Relic 1200) were re-verified live incidentally. Merged under the
maintainer's standing merge directive (2026-08-16).

## Final test polish: three Nice to Have items (final-test-polish)

- InventoryReducerTests: new Reduce_PreservesRootId_IncludingWrapperSentinel
  pins CloneNode carrying the wrapper-root Id through Reduce (real path).
- MultiItemPlanTests SingleEntryList pin: dropped the inert reducer arg
  (snapshot is null), matching sibling no-reduction pipeline constructions.
- InventoryReducerTests: trimmed five near-verbatim "split across two
  sources" comment repeats; section headers state the relationship once.
Validation: module build 0 errors; suite 1847 green (1846 baseline + 1).
Gate: PASS (test-only change; the review's mutation check proved the
new root-Id test fails through the real Reduce path when CloneNode
drops the wrapper Id; suite 1847/1847; no rendered surface).

## Log + Snapshot UX: three small items (log-snapshot-ux)

- **Staleness-threshold unification:** MainView's private 10-minute
  StaleThreshold constant (and its now-false "setting deliberately not
  added" comment) deleted; the staleness label now reads the same
  clamped SnapshotRefreshIntervalMinutes setting Module.Update()'s
  auto-refresh gate reads, re-read on every ApplyStatusDisplay call so
  a Settings save moves both together. Both sides share the new pure
  StatusText.IsStale predicate, pinned by 4 Blish-free tests
  (boundary at age == threshold; same age flips verdict under 5m vs
  10m thresholds). MainView takes ModuleSettings via its ctor
  (CraftingPlanView's DI shape).
- **Delete Log File (d2 OQ4):** ModuleLogStore.DeleteAll was dead API;
  now wired to a "Delete Log File" toolbar button on the Log tab,
  confirm-gated via the existing ModalDialog (whose Show gained a
  confirm-button label parameter - required after review, so every
  caller states its own verb; the regenerate call site passes
  "Regenerate" explicitly). Ring-reset seam: new
  ModuleLog.DeleteFileAndReset - bounded 250ms flush-queue drain,
  file delete under the file gate, ring clear via Clear() (Version
  stays monotonic), then one Info trace entry recording the deletion
  (also recreates the file). Review fix: the confirm callback no
  longer runs DeleteFileAndReset on the main thread - the drain plus
  an unbounded file-gate acquisition (FlushLoop can hold it through a
  slow append or full-file trim) could hitch a render frame - it runs
  on Task.Run, with the status/rebuild tail marshaled back via
  MainThreadMarshal.Run. 3 new Blish-free tests against a real
  ModuleLogStore/temp dir, including a next-session SeedFromStore
  proving deleted entries cannot be resurrected from the file.
- **Sticky content-type dropdown:** the Snapshot tab's All/Items/Wallet
  dropdown now session-sticky via _lastFilterSelection, matching the
  search text and four source checkboxes; the comment defending the
  reset-to-default asymmetry is deleted. Restored before the
  ValueChanged subscription, so the read-back cannot trigger a
  redundant rebuild.

Validation per commit: module build 0 errors; suite green throughout -
1847 baseline -> 1851 (IsStale tests) -> 1854 (DeleteFileAndReset
tests) -> 1854 (commit 3 is view-only). Two rendered surfaces await
the orchestrator's desktop check: the Log tab's new Delete Log File
button (placement left of Copy/Clear view; confirm dialog shows a
"Delete" button; post-confirm the view shows only the trace entry) and
the Snapshot staleness label recoloring against a changed
SnapshotRefreshIntervalMinutes setting. The sticky dropdown is also a
one-look check (pick Wallet, switch tabs, return).
Gate: PASS (2026-08-19, live desktop session over the Paint dummy,
branch build 21aa2ac). All three surfaces verified from captures
(preflight/ux1-ux6): (1) Delete Log File button renders leftmost of
the three right-aligned Log toolbar buttons with the cannot-be-undone
tooltip; clicking it raises the Confirm dialog with an explicit
"Delete" button; post-confirm the status label reads "Log file
deleted", the view rebuilds to exactly one entry ("[INFO] ... [log]
Log file deleted by user"), and the on-disk module_log.jsonl was
recreated containing exactly that one line. (2) Staleness label: the
29-day-old canned preflight snapshot rendered the status line in the
warning color with the "(29d ago)" age suffix under the
setting-driven threshold; the new ModuleSettings ctor wiring
introduced no render fault. (The line's "Aug 15" base timestamp
alongside "(29d ago)" is the pre-existing persisted-failure-status +
snapshot-age composition, not a defect - the base status was stamped
at failure time by RefreshNowAsync, the suffix from CapturedAt.)
(3) Sticky dropdown: picked Wallet, switched to Log and back -
selection held and the list showed only currencies. Boundary
behavior of the unified threshold is pinned by the 4 Blish-free
IsStale tests; the recolor-flip-on-setting-change interaction was
not exercised live (auto-refresh interference makes it observable
only under API failure; covered by tests).

## Per-character source checkboxes + character-name search (char-source-search)

Maintainer directive, verbatim: *"i want per character source checkboxes
and search matching character labels"* - which resolves d1-snapshot-about-
settings.md Feature 1's Open Questions 1 and 2 (both recorded as RESOLVED
in that proposal, against their original opposite choices).

- **Per-character checkboxes (commit 1):** the Snapshot tab's single
  "Characters" checkbox is replaced by one checkbox per character in the
  current snapshot, labeled with the character's name. Bank / Material
  Storage / Shared Inventory are unchanged. The filter decision moved
  fully into the Blish-free service layer: SnapshotSourceFilter dropped
  its Characters bool for an UncheckedCharacters exclusion set keyed by
  bare character name, and SnapshotSearchResultBuilder.IsSourceEnabled
  resolves a "Character:<name>" source against it. Exclusion (rather than
  inclusion) is what makes a character new in a fresh snapshot default to
  checked without the filter knowing the roster; the view holds the same
  kind of set as its session-sticky state, so unchecked characters survive
  a tab bounce exactly like the search text and the content-type dropdown.
  The sticky set is copied into the filter per rebuild rather than shared
  by reference - SnapshotSourceFilter is a mutable public carrier, and a
  later normalizing or pruning pass on the service side would otherwise
  reach straight into the user's UI state and re-check their boxes.
  Stale names (a deleted character) are deliberately not pruned - they
  match nothing, and pruning would forget the user's choice whenever a
  degraded snapshot happened to omit a character.
- **Layout mechanism:** the row is account-sized (1 to 15+ characters), so
  it can no longer use fixed X positions. MainView measures each label
  with DefaultFont14 (plus a 40px box-and-gap constant chosen to land
  close to the four widths the row previously hardcoded - an
  approximation, not a reproduction: no single constant can make two
  different 16-character labels both measure 170) and hands the widths to a new
  Blish-free SourceFilterFlowLayout, which wraps them left-to-right and
  returns per-cell offsets plus the height the row needs. CoinRowY /
  ContentY / TopRegionHeight became computed properties over that measured
  height, so the coin and content rows shift down by however many rows the
  filter wrapped onto - on build, on every snapshot, and on every resize
  (a narrower window re-wraps). Single-row height is floored at the exact
  30px the row had before, so the common case is unchanged vertically -
  the exact part of the reproduction; the cells' own X positions are the
  measured approximation above.
- **The row is bounded, and scrolls past the bound:** an account-sized row
  cannot be allowed to grow without limit - a large roster in a short
  window would otherwise push the result list to zero height, with no way
  for the user to shrink the row back (it cannot be collapsed, and the only
  recourse would be enlarging a window that may already be at the display's
  limit). MainView caps the row at whichever is smaller: four flowed rows,
  or whatever leaves the result list 120px. Past the cap the panel gets
  CanScroll and the cells are re-flowed clear of the scrollbar strip, so
  every checkbox stays reachable rather than being clipped away. The
  content panel's own clamp-at-zero stays as a floor for the case of a
  window shorter than the fixed rows above the filter row.
- **New roster seam:** SnapshotSearchResultBuilder.CollectCharacterNames
  merges the "Character:<name>" item sources with CharacterDisciplines, so
  a character holding no items still gets a checkbox, and keeps
  zero-count entries (which AccountItemIndex drops) for the same reason.
- **Checkbox row construction moved to the main thread:** the checkboxes
  are now created only by RebuildSourceFilterRow, called from Build's
  marshaled tail and from SetSnapshot, instead of inline in Build's
  ThreadPool-thread body. Two reasons: a roster change has to rebuild the
  row, not just the result list, and one creation path cannot drift from
  the other. SetSnapshot rebuilds the row **only when the roster actually
  changed** (ordinal element-wise compare against the previous names, which
  CollectCharacterNames sorts, so it is stable): SetSnapshot is driven by
  the periodic background refresh, and an unconditional rebuild disposes
  the very checkbox a click may be mid-press on, silently losing the click,
  besides reallocating the whole row for a byte-identical roster. An
  unchanged roster still re-runs the layout pass. An "All Characters"
  master toggle (present only when there
  is more than one character) cascades check/uncheck-all behind a
  re-entrancy guard, so one user click stays one content rebuild.
  **Known two-state quirk:** after unchecking the master, a character
  first appearing in a later snapshot renders checked while the master
  still reads unchecked - a deliberate consequence of the exclusion model
  (only named characters are excluded, so an unknown name defaults to
  visible); a tri-state master visual was considered and deferred.
  RebuildContent now reads the sticky fields rather than the controls,
  which is also what makes it safe while the row does not yet exist.
- **Character-name search (commit 2):** typing a character's name
  surfaces every item that character holds. Implemented in
  BuildItemRows, not the view: an item is kept when its own name matches
  OR a character holding it matches (case-insensitive substring either
  way). The character check only consults sources that already survive
  the source filter, so **an unchecked character's rows stay hidden even
  when its own name is typed** - deliberate AND-composition, chosen over
  letting a typed name re-enable a box the user unchecked. Scope limits,
  also deliberate: the scan starts past the "Character:" encoding token
  (so that internal token is not itself searchable), storage-location
  labels do not match, and the wallet list is untouched - currencies have
  no per-character holding, so a character search never lists them. A
  character-matched row still reports the account-wide total and full
  breakdown across the checked sources, not just the matched character's
  share, so a total means the same thing on every row in the list.
  Character labels later gained a 2-character minimum query length
  (char-search-min2 below); item and currency names still match from the
  first letter.
- **Perf note (keystroke path):** character matching costs a full source
  walk for every item whose name does not match, where the old name-only
  search skipped straight past it. That is bounded above by the
  empty-search rebuild, which already walked every source of every item,
  so the worst case is unchanged. Per-resize cost gains one small list
  plus one placement object per checkbox; the row is otherwise rebuilt
  once per snapshot, not per keystroke.

Validation per commit: module build 0 errors throughout; suite 1854
baseline -> 1876 (commit 1: per-character filter semantics, roster
collection, flow-layout wrapping) -> 1884 (commit 2: character-label
matching, AND-composition, wallet-unaffected). Commit 3 is docs plus one
redundant loop-variable copy dropped from the row builder; suite 1884.
Commit 4 applies the review's three Must Fix findings (row height bound +
scroll, roster-change guard on the rebuild, defensive copy of the sticky
set); all three land in MainView, which is Blish-coupled and therefore
outside the test suite's reach - build 0 errors, suite still 1884.

Desktop gate should look at: (1) the per-character checkbox row rendering
with a multi-character snapshot - labels not clipped, no overflow past
the window's right edge, wrapping onto a second row when needed, and the
coin row and result list shifting down by exactly that much (then drag
the window narrower and confirm it re-wraps and re-anchors); (2)
stickiness across a tab bounce - uncheck a character (and the All
Characters master), switch to another tab and back, confirm the boxes
come back as left, and that a Refresh Now does not silently re-check
them; (3) character-name search - type a character's name and confirm
its items appear while currencies do not, then uncheck that same
character and confirm the list empties rather than the search overriding
the box; (4) the bounded row - shrink the window until the filter row hits
its cap and confirm the result list keeps its minimum height, the row
gains a working scrollbar, and the checkboxes do not sit under it.
Gate: PASS on items (1)-(3), (4) not exercisable live (2026-08-19,
Paint-dummy desktop session, branch build b59df59, captures
preflight/cs1-cs5). The canned preflight snapshot was enriched with
three character-sourced items (Mystic Coin 25 + Mystic Clover 5 on
"Maximus Test", Orichalcum Ore 50 on "Alt Number Two"; original
backed up as snapshot.json.pre-charsrc-bak) so both rosters were
live. (1) The row rendered Bank / Material Storage / Shared
Inventory / All Characters / Alt Number Two / Maximus Test on one
measured row, no clipping; the merged Mystic Clover row showed
"x35 - Material Storage 30, Character: Maximus Test 5". (2)
Unchecking Alt Number Two hid Orichalcum Ore and dropped the All
Characters master; a Log-tab bounce restored the boxes exactly as
left with the row still filtered. (3) Typing lowercase "maximus"
surfaced exactly the two Maximus Test items (account-wide
breakdowns intact) and no wallet rows; unchecking Maximus Test with
the search still active emptied the list to the message 'No items
match "maximus" in the selected sources' - AND-composition
confirmed live. (4) The 4-row cap cannot be reached with a
2-character roster and synthetic resize-grip drags are documented
unreliable; the cap/scroll math was review-verified and the
CanScroll-after-construction caveat stands as the one untested
behavior - re-check visually if a large-roster account ever shows
a scrolling filter row.

## Nice to Have batch (nth-cleanup)

The nine non-controversial Nice to Have findings from the PR #142 and
#143 adversarial reviews, applied together on branch `nth-cleanup`.
Behavior is preserved except where a bullet says otherwise.

- **Flush drain budget hoisted:** Module.Unload and
  ModuleLog.DeleteFileAndReset each spelled out 250ms, with the latter's
  doc asserting in prose that they matched. `ModuleLog.FlushDrainBudget`
  now carries it for both.
- **Log status moved to its own row (behavior change):** the auto-sized
  status label shared the toolbar row with the three right-anchored
  buttons and ran under the leftmost of them - at the enforced 930px
  minimum window width the gap between them is only ~48px, so any real
  status ("Log file deleted", "Nothing to copy") collided. It now sits
  in a full-width 24px row beneath the toolbar, the same shape
  MainView's `_statusPanel` already uses for the same reason; the
  content panel starts below it. No truncation - the full text always
  renders.
- **Source-filter re-flow skipped when its inputs have not moved:** the
  Snapshot tab re-flowed the checkbox row on every resize event,
  including height-only ones. The flow pass now runs only when the
  available width or the height-driven row cap changed - the cap
  because it decides whether the row scrolls and so re-flows narrower.
- **Outgoing checkbox references dropped with the panel:** Build
  replaces `_sourceFilterPanel` on a ThreadPool thread, but the three
  fields holding its checkboxes were only refilled from the marshaled
  tail; a resize in that window wrote Location on controls belonging to
  the replaced panel. Cleared by reference swap next to the panel
  construction, so a concurrent main-thread read still sees a
  consistent list - which holds only because each reader now takes the
  field into a local once (`SetAllCharactersChecked` hoists the
  checkbox list before its bounds check; `OnCharacterToggled` hoists the
  master before its null check; `ApplyTopRegionLayout` hoists the cell
  list before its count/indexer walk), rather than re-reading the field
  after its own guard.
- **No substring per character source:** SnapshotSearchResultBuilder.
  IsSourceEnabled compares the name half of "Character:<name>" in place
  with string.CompareOrdinal instead of allocating a Substring per
  source per item on the keystroke path. Pinned by two tests the old
  exact-hit/exact-miss pair could not catch: whole-name-only matching
  (a strict prefix, a strict extension, and a case-only variant of an
  excluded name all stay visible) and the zero-length name half.
- **Comment/doc corrections:** ApiAccessDialog's claim that ModalDialog
  has fixed Regenerate/Cancel buttons (its confirm button is
  caller-named); ApplyStatusDisplay's claim that a Settings save changes
  the label and the auto-refresh gate together (it is re-read on the
  next ApplyStatusDisplay call); the sticky-state field block's inert
  `<para>` tags and design narration; CheckboxChromeWidth's
  "reproduces the four widths" (it approximates them - only the
  single-row height is exact, corrected in this file too);
  SourceFilterFlowLayout's class-level `<paramref>` moved onto the
  method that has the parameter; stray blank lines in
  SourceFilterFlowLayoutTests.

Open by choice - the two behavioral Nice to Haves this batch
deliberately skipped:

- **Character-search minimum query length:** a one-character query still
  walks every source of every non-matching item. Left as is; the worst
  case is still bounded by the empty-search rebuild. **CLOSED
  (char-search-min2):** the maintainer set a 2-character minimum, for
  the result-list reason rather than the perf one - see that section
  below.
- **Tri-state master checkbox:** the two-state quirk recorded in the
  char-source-search section above stands.

Validation: build 0 errors and the full suite green before each commit
(1884, then 1886 once the two IsSourceEnabled boundary tests landed).
Gate: PASS (2026-08-19, Paint-dummy desktop session, branch build
3310169, capture preflight/nth1-log-status.png). The one visual
surface in the batch is the Log status row rework (review Must Fix):
clicking Copy on an empty log rendered "Nothing to copy" on its own
full-width row directly below the toolbar - full text, no overlap
with the Delete Log File / Copy / Clear view buttons, with the "No
log entries yet." empty state below it. Everything else in the batch
is comments/docs, a constant hoist, allocation removal (pinned by 2
new Blish-free tests), and the resize early-out + reader hoists,
which are code-review-verified (the verify pass caught and the
orchestrator fixed a third un-hoisted reader in ApplyTopRegionLayout
before release). Suite 1886/1886.

## Character-name search minimum query length (char-search-min2)

Maintainer decision, closing the "Character-search minimum query length"
item the nth-cleanup batch left open by choice. The reason is the result
list, not the per-keystroke cost: with one-letter matching, typing "ar"
on the way to an item name first passes through "a", which surfaces
everything held by every character whose name contains an "a" - so the
opening keystrokes of an item search widen the list instead of narrowing
it.

- **Rule:** a character label matches only from 2 characters on. Item
  names and wallet currency names are unchanged - a single letter still
  matches them, so the common search is untouched.
- **Where:** SnapshotSearchResultBuilder.CharacterNameMatches, behind the
  named MinCharacterSearchLength constant, so the one seam that decides
  "does this source's character name match" carries the floor and the
  BuildItemRows call site keeps its existing shape. The length compared
  is the trimmed query the builder already computes, so padding a single
  letter with spaces does not buy character matching.
- **Not a perf change:** the source walk itself is unchanged (BuildItemRows
  walks every checked source of every item regardless, to total it), so
  the one-character query costs what it always did, minus the substring
  scans it no longer performs. The bound recorded in char-source-search
  still holds.
- **Tests (+3, 1886 -> 1889):** a 1-character query returns the item whose
  own name matches and *not* the item held by a character whose name
  matches; the same pair at exactly 2 characters returns the
  character-held item (the boundary is exact - 2 matches, 1 does not); a
  whitespace-padded single letter stays below the floor.

Validation: module build 0 errors; suite 1889/1889.

Desktop gate should look at: type a single letter that begins a character
name into the Snapshot search and confirm only item/currency name matches
appear (no character holdings), then add the second letter and confirm
that character's items appear. The per-character checkboxes and the
AND-composition from char-source-search are unaffected and need no
re-gating.
Gate: PASS (2026-08-22, Paint-dummy desktop session, branch build
651375c, captures preflight/m2a-one-char.png / m2b-two-char.png).
The preflight roster's holder names all contain "t" (Maximus Test,
Alt Number Two, Third Wheel, Ranger Of The North...), giving a clean
discriminator: typing "t" returned only item/currency name matches
with the 6-holder Green Wood Log ABSENT (the floor holding at one
character); adding "h" ("th", matched by no item name) returned
exactly Green Wood Log via Third Wheel / Ranger Of The North (the
floor lifting at two). The reviewer's noted empty-state wording gap
(a one-letter query's message does not mention the character-label
floor) was observed as accurate-but-unexplained live; left as the
recorded maintainer call.

## Audit batch I: log entry readability (audit-i-log)

UX audit finding (M7): every Log tab entry was ONE `AutoSizeWidth`
Label built from the whole flat line, tinted end to end by
`ColorForLevel`, hard-clipped at the panel's right edge with no wrap,
no ellipsis and no indication that text had been lost - a WARN
carrying a long path plus an exception simply lost its tail, and the
level tint was the only structure in a wall of same-shaped text.

**Row split.** Each entry is now a fixed-height row `Panel` holding two
columns:

- a prefix Label at x=0 showing `[LEVEL] timestamp [tag]`, dimmed to
  70% alpha (this repo's existing `Color.White * 0.35f` idiom) but
  still carrying the level color, so severity still reads at a glance
  down the column while the chrome recedes behind the message;
- the message Label at the shared message-column x, with an explicit
  width (row width minus the prefix column, the 8px gap and the 8px
  right pad).

Both columns run through the existing `LabelHelpers.EllipsizeToWidth`,
and a row that had to shorten EITHER column gets `BasicTooltipText`
with the full line - assigned to the row Panel AND to both Labels,
because Blish resolves a tooltip on the control under the mouse and
does not bubble to the parent (the swallowed-hover class already
recorded for `ShoppingListSectionRenderer` in this file). The `...`
plus that tooltip are the truncation indicator the audit asked for.

The prefix column is sized from a worst-case template - the widest
level name, a timestamp built from the widest decimal digit, and a
14-character tag allowance - rather than from the rows currently on
screen. That is load-bearing, not decoration: the incremental
`AppendNewRows` path only ever sees the new entries, so a width derived
from what a pass can see would drift away from the rows the last full
`RebuildRows` produced and stagger the message column.

The tag allowance is counted in `'w'` glyphs and sized off the longest
tag actually written anywhere in the tree - `snapshot-fetch`, 14
characters. The margin is the glyph: every tag in the module is
lowercase ASCII plus `-`, all narrower than `'w'`, so 14 `'w'`s clear a
14-character tag with room to spare. The first draft reserved 10 on the
stated (wrong) belief that `scrolldiag` was the longest tag; at that
width `[snapshot-fetch]`, the module's most common WARN source, risked
rendering permanently truncated AND permanently tooltip-flagged at every
window width, in the very column this change exists to make readable.

**Ellipsize, not wrap (decision).** Wrapping reads better for a long
exception, but it makes row height a function of content, and this
tab's whole row model is built on uniform rows: the eviction trim, the
append path and the Follow tail-scroll (`VerticalScrollOffset =
int.MaxValue`, an overshoot that clamps) all assume the panel's content
height is settled at the moment they run. Blish measures a wrapped
`AutoSizeHeight` label during its own deferred layout pass, so the
overshoot would fire against a stale height and Follow would land short
of the bottom. Wrapping also lets one stack-trace ERROR fill the whole
viewport in what is meant to be a tail view. Ellipsize + tooltip
preserves fixed row heights, leaves every one of those mechanisms
untouched, and is what the audit accepts as the minimum.

**Resize.** The container's `Resized` handler re-fits every visible row
after resizing the content panel, walking `_renderedRows` (the same
FIFO the eviction trim uses) - the same shape the recent status-row
rework in this file uses for the toolbar/status/content panels. Two
cheap outs keep a resize drag off the hot path: a vertical-only drag
leaves the content width unchanged and returns before touching a single
row, and a row already showing its untruncated text whose column only
grew skips the `MeasureString` binary search inside `EllipsizeToWidth`.

The walk itself is wrapped in `_contentPanel.SuspendLayout()` /
`ResumeLayout(false)`, the same pair `CraftingPlanView.ReplayRelayout`
uses and for the same reason - and the reason the first draft's "worst
case is still bounded by the ring's 2000 entries" was the wrong cost
model. Assigning a row Panel's `Size` fires that Panel's own `Resized`,
which `FlowPanel` wires to a full reflow of every sibling, so an
unsuspended loop over a full ring is O(rows^2) position writes plus a
fresh children array per reflow - on every frame of a horizontal drag,
on the UI thread - not O(rows). Suspending the parent propagates down
(Blish's `IsLayoutSuspended` walks the parent chain) and
`ResumeLayout(false)` leaves the single coalesced reflow to Blish's own
next-frame `UpdateLayout`. With the suspend in place the per-drag-frame
cost is back to linear in the ring's 2000 entries.

`RebuildRows` re-parents up to 2000 rows on every search-box keystroke
and carries the same unsuspended-reflow shape. That is pre-existing (the
old label-per-row build did the same) and is deliberately left alone
here; it is the obvious next candidate if the Log tab ever needs a
second perf pass.

**Extraction.** `LogTabContent.FormatLine` moved to the Blish-free
`Services/LogLineFormat`, which also splits an entry into its prefix
and message halves; `Line()` recomposes them into exactly the string
`FormatLine` produced, so the search filter, the Copy button and the
tooltip all still work in one unchanged flat line (Copy still emits
full lines - unaffected by the split).

`Message()` has one deliberate departure from the old `FormatLine`
output: every run of CR/LF/TAB collapses to a single space (leading runs
dropped, no trailing whitespace kept). Without it a multi-line message
lost everything after its first line, silently - a fixed-height row Panel
clips lines 2..n, and `BitmapFont.MeasureString` reports a multi-line
string's WIDEST LINE rather than its full extent, so `EllipsizeToWidth`
sees a string that "fits", returns it unchanged, and the row is marked
neither shortened nor tooltipped. No in-tree call site embeds a newline
today, but any `ex.Message` interpolation is one BCL/HTTP/serialization
exception away from one (e.g. `CraftingPlanPipeline`'s generation-failure
WARN). Flattening in the formatter rather than at the label also keeps
Copy's `Environment.NewLine` join at one line per entry.

`Services/LogRowLayout` carries
the column arithmetic, so the degenerate widths that would otherwise
blank every row (a message column ellipsized to zero) are pinned by
tests rather than only observable live. Row virtualization/build
behavior - `RebuildRows`, `AppendNewRows`, the eviction trim,
`RebuildRowsIfBuilt`, the `_buildComplete` gate - is untouched; this is
a per-row presentation change.

The class doc comment's "label-per-row, no multi-column ellipsized rows
that must reflow live during a resize drag" claim is now false and was
rewritten: rows ARE multi-column and DO reflow, but the tab still does
not opt into the `PlanContentHeightMath`/relayout-registry contract,
and the comment now says why (uniform row heights, overshoot scroll -
no per-section height math and no settle/verify pass to defer into).

**Validation per commit:** module build 0 errors (pre-existing StyleCop
warnings only; no new warning class in the edited files). Suite 1886
baseline -> 1900 after commit 1 (14 new Blish-free tests:
`LogLineFormatTests` pins that prefix + " " + message is byte-identical
to the old flat line, including the no-tag and null-message cases;
`LogRowLayoutTests` pins the narrow-row prefix cap and the
never-collapse floor) -> 1900 after commit 2 (view-only) -> 1904 after
the review-fix commit (4 more, pinning the CR/LF/TAB flattening and the
unchanged-reference fast path).

**Desktop gate items** (rendered surface, outside the test-runnable
Blish-free layer):

1. A long WARN line (long path + exception) shows a dim
   `[WARN] timestamp [tag]` prefix, an ellipsized message ending in
   `...`, and a tooltip carrying the full untruncated line - hovering
   the prefix, the message and the row's empty right edge all raise it.
2. Narrowing and widening the module window re-fits the rows: the
   message re-ellipsizes to the new width, previously-truncated rows
   recover their full text when the window grows, and the tooltip
   appears/disappears with the truncation. Do this with the level filter
   on `Debug+` and a full ring (2000 entries) and watch for drag stutter -
   that is the case the `SuspendLayout` wrap above exists for, and it has
   only ever been reasoned about, never measured on hardware.
3. The level tint is still legible at a glance - ERROR/WARN rows read
   red/amber down the prefix column, and the message keeps the full
   (undimmed) level color.
4. Follow still snaps: with Follow checked, new entries append at the
   bottom and the view stays pinned there; unchecking Follow freezes it.
5. Copy still writes the full untruncated lines to the clipboard, not
   the ellipsized display text.

Gate: PASS (2026-08-22 evening desktop batch, branch build 8026242,
captures preflight/gI1-gI3). At Debug+ with the seeded session log:
every entry rendered as a dim level-tinted prefix column ([WARN]
orange, [INFO] white, [DEBUG] grey) plus an aligned message column;
the long plan-timing line ended in a visible "..." instead of the
old hard clip; hovering a row that fits showed no tooltip (correct
narrowed semantics) while hovering the ellipsized row showed the
full line in a multi-line tooltip. Follow was on and the newest
entry sat at the bottom. Drag-resize refit not exercised live
(synthetic resize drags unreliable); covered by the SuspendLayout
fix, the width-guard early-outs, and the Blish-free layout tests.

## Audit batch F: input flow (audit-f-input-flow)

Four maintainer-approved UX-audit findings on the Crafting Plan tab's
input flow, plus one regression the first of them exposed.

- **H4, stale resolved item (the correctness bug):** a row's item id was
  set only by a suggestion pick and never cleared, and nothing else ever
  assigned it. Editing the search box afterwards therefore left the plan
  generating for the previously picked item while the box read the new
  name - "Mystic Clover" on screen, Deldrimor Steel Ingot in the plan.
  Three parts: (1) a search-box `TextChanged` handler drops the row's
  resolved item once the text diverges from the resolved name, with case
  and surrounding whitespace not counting as divergence; (2) Generate
  first resolves typed-but-unpicked rows against the item search
  provider, adopting an exact case-insensitive name match only - a
  partial name stays unresolved rather than planning for whatever ranked
  first, and the adoption is re-checked on the main thread against what
  the row holds at that moment, so a pick or a further keystroke landing
  during the search cannot be overwritten by a result describing older
  text; (3) with nothing resolved, the status now distinguishes "Select
  at least one item before generating." (every row blank) from "No item
  matched what you typed - pick an item from the suggestion list."
  (text that resolved to nothing), where the old copy told someone
  staring at a filled-in box to select an item.
  A name that belongs to **several items** is a third case, and the one
  with no way to notice it: GW2 reuses item names freely (4136 of the
  14762 seeded names are shared, and three distinct items are called
  "Amethyst Gold Ring"), the provider sorts by name so they all land in
  one result window, and item ids are never displayed - adopting the
  first would have generated a full plan for an arbitrary one of them
  with nothing on screen to say which. Such a name now stays unresolved
  and says so: "More than one item has that name - pick the one you want
  from the suggestion list."
  A Generate where only SOME rows resolved no longer drops the rest in
  silence either: the plan is still generated from the rows that
  resolved, and the strip carries "N row(s) has/have no item selected and
  is/are not in this plan." for as long as that plan is on screen.
  While the resolution pass runs, Generate is disabled and the strip
  reads "Resolving items..." - the pass is awaited, and nothing
  downstream disables the button until a generation actually starts, so
  clicks during it would otherwise be silent and each would start another
  full generation.
  The decisions live in the new Blish-free `Services/ItemRowSelection.cs`
  (staleness rule, exact/ambiguous name match, status copy), covered by
  20 tests. `TriggerGenerate` is now a thin wrapper that owns the
  resolution await, the Generate button for its duration, and the marshal
  back to the main thread before the generate body, which touches
  controls from its first line; the body is `GenerateFromResolvedRows`.
- **Typed text across a row add/remove (regression from H4):** rebuilt
  rows seeded their search box from `ItemName`, which H4 now clears, so
  typing a name and pressing "+" wiped it. Rows keep the text they last
  showed (`ItemRowState.TypedText`) and seed from that.
- **M1, deferred controls honesty:** the Prices dropdown, Value Own
  Materials, and Use Own Materials on the no-plan path now put
  "Settings changed - press Generate Plan to update" on the status strip
  as they change - they look like the instant-apply controls on other
  tabs but only affect the next plan. (Use Own Materials with a plan on
  screen already regenerates behind a confirm, so it is not deferred.)
  The warning is standing state, not a one-shot status write, and is
  appended to whatever the status board says: a generation in flight
  re-renders the strip about seven times a second and would otherwise
  have erased it within 150ms, ending on "Plan generated - &lt;time&gt;"
  for a plan built with the price basis the user had just changed away
  from. It survives a tab switch for the same reason, and is cleared when
  a generation actually starts - which is the run that includes it. The
  "rows not in this plan" notice above rides the same mechanism.
  A generation also dims the plan area to 0.45 opacity, restored in the
  `finally` that already covers success, failure and cancellation alike;
  a superseded generation returns at its `myGen` check and leaves the dim
  to the newer generation that owns it.
- **M15, suggestion list occlusion:** the list opened directly under the
  search box, over this row's quantity field and every row below it.
  `SuggestionPanel` takes an anchor offset and opens that far right of
  the text box - right of the Qty stepper - clamped so a window at the
  right screen edge cannot push it off. It still overlaps part of the
  persistent controls row (the Prices dropdown's right half and Value Own
  Materials) and this row's own +/- buttons while open; anchoring cannot
  clear a full-width controls row, and this is the position the finding
  approved.
- **M16, the "+" button:** moved right of the quantity field so it no
  longer abuts it and reads as a Qty stepper, and given the tooltip "Add
  another item to this plan". The "-" button beside it got the same
  treatment ("Remove this item from the plan") rather than leaving the
  sibling half-fixed.

Validation: build 0 errors and the full suite green per commit (1906,
up from 1886 with the 20 new ItemRowSelection tests). No new test
references Blish.

What the desktop gate should look at:

1. **Stale-pick invalidation, live:** pick an item from the suggestion
   list, then edit the box to a different item's name and press Generate.
   The plan must be for what the box says (or, for a partial name, the
   "pick an item from the suggestion list" status) - never for the
   earlier pick. Also: type a full item name without ever opening the
   list and press Generate; it should plan that item.
2. **Shared name:** type "Amethyst Gold Ring" in full, do not open the
   suggestion list, press Generate. No plan may be generated - the status
   must read "More than one item has that name - pick the one you want
   from the suggestion list." Picking one of the three from the list and
   pressing Generate must then plan normally.
3. **Partly resolved Generate:** row 1 picked from the suggestion list,
   row 2 typed with a name that matches nothing ("Mystic Clove"), press
   Generate. The plan must be generated for row 1 AND the strip must
   carry "1 row has no item selected and is not in this plan." for as
   long as that plan is on screen - the old behavior planned row 1 and
   said nothing at all about row 2.
4. **Settings-changed status:** change Prices or Value Own Materials with
   a plan on screen; the status line under the toolbar must switch to
   "Settings changed - press Generate Plan to update" instead of leaving
   the "Plan generated - <time>" line up. Then the harder case: press
   Generate and change Prices WHILE it runs. The warning must appear
   immediately, survive the whole run (the spinner re-renders the strip
   about seven times a second), and still be there beside
   "Plan generated - <time>" when the run finishes - the plan on screen
   was built with the old basis. It must also survive a tab switch away
   and back, and disappear the moment the next Generate starts.
5. **Dimmed stale plan:** press Generate with a plan already on screen -
   the plan area should visibly dim for the run and return to full
   opacity when it finishes, on a successful run and on a failing one
   (an offline/error run is the one worth checking).
6. **Suggestion list position:** open the list on the first of two rows
   and confirm the second row's search box and quantity field stay
   visible and clickable, and that the list stays inside the window at
   minimum window width (930).
7. **Button tooltips:** hover "+" and "-" and confirm the tooltips read
   plainly and do not clip.

Gate: PASS (2026-08-22 evening desktop batch, branch build 454681b,
captures preflight/gF0-gF5). (1) Typed "mystic clover" lowercase,
never opened the suggestion list, pressed Generate: the module log
recorded "Plan for Mystic Clover x1" - unique-exact-name adoption
working end to end. (2) The suggestion list opened to the right of
the qty stepper, no longer covering Use Own Materials or the Prices
label (the documented partial-overlap tradeoff visible and
acceptable). (3) Toggling Value Own Materials appended the standing
notice "Settings changed - press Generate Plan to update" after the
board status with a separator; it survived subsequent renders and
was still standing alongside a later honest-status line. (4) The
"+" button showed "Add another item to this plan" on hover at its
separated position. (5) Appending "xx" to the resolved name and
pressing Generate produced "No item matched what you typed - pick
an item from the suggestion list." with the previous plan untouched
- stale-pick invalidation plus the honest empty status, no wrong
plan. Ambiguous-name and multi-row partial-resolution statuses were
not staged live (no duplicate-named craftable in the fixture path);
both are pinned by the ItemRowSelection tests. Bonus: the x1
all-owned plan rendered the HAVE pill and a 0c cost tile - the
zero-cost plan state previously uncaptured.

## Audit batch G: Settings restructure (audit-g-settings)

Commits on audit-g-settings off master 47bb2c5, covering the three
maintainer-approved UX audit findings against Views/SettingsTabContent.
cs (M4 currency-list density, M5 save buttons + empty heading, M6
visual structure), plus the review round that followed them. Persistence semantics are untouched - every setting
is written by the same code, with the same validation, the same
"invalid rows keep their persisted value" contract and the same
three-state currency precedence as before; only layout, control
placement and the confirmation surface changed.

**Supersedes B14** (backlog-cleanup, gate PASS 2026-08-17): that batch
deduplicated the four per-section save rows into one AddSaveRow helper
and live-verified all four rendering identically with their green
dated "Saved" labels. Under the maintainer-approved M5 the four rows
and their four status labels are gone entirely, replaced by one Save,
so that gate observation no longer describes the shipping UI. The
dated green confirmation pattern itself is kept, once.

- **One line per currency, two-up (M4):** each currency was a 54px
  two-line row spanning the full panel while using only its left
  portion - name/input/hint/error on line one, a default-state label
  and Clear checkbox on line two - stacked 47 deep. Each is now a 30px
  cell: name (ellipsized to 170px, full name on hover only when it did
  not fit), input, Clear, and one tag slot at the right of the cell.
  That slot shows the persisted default state ("default N", or
  "cleared" when suppressed) and is taken over by the red "Invalid"
  warning while an amount will not parse - only ever one of the two, so
  a half-width cell needs room for one. The gw2efficiency attribution +
  editable/clearable wording is on the input's tooltip. The input's
  placeholder is the unit ("copper") on every row: Blish's TextBox
  insets a placeholder 10px a side and draws it untruncated inside the
  control's own scissor, so a 70px box shows ~50px of it - enough for
  "copper", not for "default: 3600", which is why the default estimate
  is a label and not the placeholder it briefly was. Cells are packed
  left-to-right, top-to-bottom into an absolutely-positioned grid
  panel. Section height: ~2,690px -> ~880px two-up (the row block
  itself 2,538 -> 720).
- **Filter box (M4):** a "Filter currencies..." TextBox above the grid
  hides non-matching cells and re-packs the rest, with a "N of 47
  shown" counter beside it. Hidden rows are still read and written by
  Save - filtering is display-only, nothing is dropped. A row whose
  amount did not parse is forced back on screen by the next filter pass
  whatever the query says (SettingsCurrencyGridLayout.Compute's
  alwaysShow), so the save bar's "N invalid entries not saved" can
  never point at a tag the filter is concealing.
- **The grid panel holds its unfiltered height (M4):** Blish's
  Scrollbar zeroes ScrollDistance/TargetScrollDistance whenever the
  scrolling container's content height changes - its RecalculateLayout
  captures the previous scrollbar percent, recomputes it from the
  visible children, and resets on any difference - and it does so a
  frame later, so the reset cannot be undone in place. Sizing the grid
  to the match count therefore snapped the tab back to scroll-top on
  every filter keystroke that changed the count. The grid panel is now
  fixed at SettingsCurrencyGridLayout.ComputeHeight (the full 47-row
  height for the current column count) and only the cells move; the
  cost is trailing blank space under a filtered list, which is why the
  grid is deliberately the last thing in the panel and the Astral
  Acclaim note moved above it.
- **Width changes are re-laid out (M4):** the row/header panels, the
  header rules, the grid panel and every cell + cell rule are re-sized
  from container.Resized (ApplyPanelWidth, early-out when the width did
  not move, so a height-only resize or a vertical drag costs nothing).
  Without it the tab kept the width it was first opened at: narrowing
  the window left the second column of cells beyond the panel's right
  edge, invisible and untypeable until the tab was closed and
  re-opened.
- **Section order (M4):** the three short sections (Homestead
  Refinement, Logging, Snapshot) now build before the long currency
  section, so the tab opens on controls rather than on a wall of
  currency rows.
- **One Save for the tab (M5):** the four per-section Save buttons are
  replaced by a single Save in a bar that is a sibling of the scrolling
  FlowPanel, so it never scrolls away. SaveAll runs all four persists
  in order - currency valuations (with its defensive "Save failed - see
  log" branch), Homestead tiers, log max size (including the live
  ModuleLog.MaxFileSizeBytes push) + retention days, snapshot refresh
  interval - sums their invalid-entry counts and writes one status:
  green "Saved - <date>" when everything parsed, amber "Saved - N
  invalid entries not saved" otherwise. Per-row error labels are
  unchanged. Placement note: the audit suggested a fixed footer; the
  bar is anchored at the TOP instead, because LogTabContent already
  builds a fixed toolbar this way above its own CanScroll FlowPanel
  and a top bar needs only ContentRegion.Width, while a bottom footer
  would also depend on ContentRegion.Height being final at Build time
  (its failure mode being a Save bar floating over the rows).
- **Empty heading demoted (M5):** "Plan Defaults" was a section header
  with three info lines and no controls at all. It is now a single note
  line under Currency Valuations, the pricing section it points at.
- **Dividers (M6):** AddSectionHeader draws the same 2px
  SectionDividerColor rule CraftingPlanView's section headers do
  (bottom-anchored with 1px clearance in the 30px header), and each
  currency cell carries a LabelHelpers.CreateRowDivider rule, hidden on
  the cells of the last populated grid row so it re-anchors as the
  filter re-packs the list. The cell's input sits at y=1 so it ends
  clear of the rule at y=27.
- **Layout math is Blish-free (M4):** Services/
  SettingsCurrencyGridLayout.cs owns the filter predicate, the packing
  math (column count, column width, per-cell X/Y/row, grid height) AND
  the cell's horizontal constants; the view aliases those constants at
  compile time and only copies placements onto controls. MinColumnWidth
  is now derived (CellTagX + CellTagWidth = 424) rather than
  hand-estimated: the previous 340 was short of the cell it claimed to
  size, so a two-up column between 680 and ~722px clipped the invalid
  tag. Two-up now needs a 848px panel, below which the grid falls back
  to one column - including at the window's 930px minimum, where the
  section is 1,410px of rows rather than 720. 42 tests cover the
  one/two-column boundary, blank/trimmed/case-insensitive matching,
  re-packing around hidden entries, alwaysShow overrides (including a
  short array), the empty result, null names, non-positive
  width/height, the fixed height, and the width budgets - the tag
  budget against every real value in CurrencyDecisionDefaults, so a
  future six-figure default fails the suite instead of clipping.
- **Review-pass fixes (own commit):** Build now nulls the currency
  grid/filter/count/status fields alongside the row lists it already
  cleared (same stale-disposed-control class as the _homesteadRows
  comment records); the scroll panel's height is clamped at 0 now that
  the save bar is subtracted from it; the demoted note was shortened to
  fit the panel width at the window's 930px minimum.
- **Measured, not assumed:** the filter's re-flow relies on FlowPanel
  subscribing to each child's Resized and skipping invisible children -
  both confirmed by decompiling the shipped Blish HUD 1.3.0 binary
  (FlowPanel.OnChildAdded -> ChangedChildOnResized ->
  ReflowChildLayout, which filters on c.Visible), so setting the grid
  panel's Height is enough and the first draft's extra Invalidate was
  removed as a second reflow per keystroke.

Validation: build 0 errors and the full suite green before each commit
(1886 baseline -> 1928 with the new layout tests; the increase is all
new tests, zero regressions).

Desktop gate items (all in the Settings tab):
1. Currency rows are one line each, with Clear on the same line and a
   readable "default N" tag at the right of every defaulted cell (the
   whole number, not a clipped one) - check a 4-digit default such as
   Guild Commendation or Spirit Shard. Two cells per line once the
   window is wide enough (panel >= 848px); one per line at the 930px
   window minimum. The whole section fits in roughly a screen and a
   half two-up instead of four-plus screens.
2. Typing in the filter box hides non-matching currencies and re-packs
   the rest with no gaps; the counter reads "N of 47 shown"; clearing
   the box restores all 47. Scroll down to the filter box first: the
   panel must NOT jump back to the top on any keystroke, including
   backspaces. The grid keeps its full height, so a short match list
   leaves blank space below it.
3. One Save button, visible without scrolling from any scroll position,
   and one green dated "Saved - <date>" confirmation. Change one value
   in EVERY section (a currency amount, a Homestead tier, log max size,
   log retention, snapshot interval), click Save once, reopen the tab
   and confirm all five persisted. Enter one bad value and confirm the
   amber "1 invalid entry not saved" wording plus the per-row tag.
4. A 2px rule under every section header, and a rule between currency
   rows with none dangling under the last populated row (check both
   unfiltered and with a filter that leaves an odd number of matches).
5. Section order top to bottom: Homestead Refinement, Logging,
   Snapshot, Currency Valuations; no "Plan Defaults" header anywhere,
   with its note present under Currency Valuations.
6. Resize the window while the Settings tab is open, both wider and
   back down to the 930px minimum, and confirm every currency cell
   stays inside the panel and stays typeable, the columns switch
   between one-up and two-up, and the section-header rules span the new
   width.
7. Type a bad amount into one currency, filter it off screen, click
   Save: the amber "1 invalid entry not saved" must be accompanied by
   that row reappearing with its red "Invalid" tag despite the filter.
Gate: PASS (2026-08-22 evening desktop batch, branch build b740035,
captures preflight/gG1-gG7). (1) The tab rendered top-down as: top
Save bar, Homestead / Logging / Snapshot short sections each with
the 2px header rule, then Currency Valuations with the filter box,
"47 currencies" count, and the one-line two-up grid - "copper"
placeholder inputs, Clear checkboxes, grey default tags, row rules,
name ellipsis on "Manifesto of the Moletaria...". (2) Typing
"shard" filtered to "6 of 47 shown" with the grid repacked two-up
and no scroll jump. (3) Save produced the green "Saved - Aug 22,
2026 8:33 PM" label beside the button (all sections saved in one
click; Save's "Save every section on this tab." tooltip verified).
(4) The "was N" override tag and amber "cleared" tag were NOT
exercised live: late-session synthetic keyboard degradation kept
the override keystrokes landing in the filter box (a documented
input-death mode, not a module fault - the filter box accepting
them proves the click-to-focus path). Both tags are pinned by the
RefreshCurrencyRowDefaultState logic restored verbatim from
master's proven three-state code plus the CellTagWidth fit test.
One-column fallback at the 930px minimum also not exercised
(synthetic resize unreliable); pinned by SettingsCurrencyGridLayout
tests.

## Audit batch K: Plan Notes wrapping (audit-k-notes)

UX audit finding M14. `Views/Rendering/NotesSectionRenderer` locked each
note to one `PlanContentHeightMath.FallbackTextRowHeight` (28px) row and
ellipsized it with `LabelHelpers.EllipsizeToWidth`, with a DEBUG assert
forbidding any child from exceeding that height. At ~830px usable that
capped a note near 100 characters before the rest became hover-only
tooltip text - while the maintainer's UI law routes every opportunity and
every complex consideration into exactly this section.

### Reconciliation: M14's one-row claim vs. the live capture's three lines

The 2026-08-22 desktop captures (`preflight/ph11-scroll2.png`,
`preflight/ph12-scroll3.png`, Mystic Clover x77) show the Notes header
reading "Notes (1)" above THREE stacked text lines. That is not a note
containing line breaks and is not evidence against M14:

- `PlanViewModelBuilder.BuildNotesSection` block 5 emitted the forge-scope
  caveat as **three separate `PlanRowType.NoteLine` rows**, one complete
  sentence each, with its own comment saying why ("a single ~243-char row
  would edge-clip exactly the caveat the note exists to deliver, and the
  split preserves the fixed height-per-row contract"). It was a
  hand-authored workaround for M14, written at the builder, one plan-
  content-specific note at a time. **This branch retires it**: the caveat
  is now one row carrying all three sentences, and the renderer wraps it.
  The builder no longer hand-splits notes to keep text on screen, and the
  comment that told the next author to do so is gone.
- The header's "(N)" counts logical note ENTRIES (`noteEntryCount`), not
  physical rows, which is why three rows read as "Notes (1)".
- No note Label in the builder contains a `\n` today. M14's claim -
  every note row renders as exactly one ellipsized 28px line - was and is
  accurate at the renderer.

So the gap to close was greedy WIDTH-wrapping of a long unbroken line,
which is what still ellipsized at ~100 characters. Explicit line breaks
are supported anyway (they compose with width wrapping, each hard line
wrapping on its own), so a note can carry its own breaks without the
builder having to split it into rows.

### What changed

- **`Services/TextWrapMath`** (new, Blish-free): greedy word wrap with a
  separate first-line budget, explicit-line-break splitting, hard-split
  of a token wider than a whole line, and the
  single-line ellipsis moved verbatim out of
  `LabelHelpers.EllipsizeToWidth`. Measurement is a `Func<string,int>`
  seam rather than a `BitmapFont` - the same shape
  `SummarySectionLayoutMath` uses when it takes an already-measured
  `widestNumberWidth`. `LabelHelpers.EllipsizeToWidth` is now the font
  adapter over it, so the two truncation paths cannot drift.
- **`Services/NotesSectionLayoutMath`** (new, Blish-free): the note text
  budget (via the shared `PlanRelayoutMath.NameMaxWidthBeforeColumn`, not
  a second copy), the per-note wrap, and the body height.
- **`NotesSectionRenderer`**: one 28px row Panel per wrapped LINE. The
  fixed row-height contract and the DEBUG child-extent assert are
  unchanged - only the row COUNT became width-dependent.
- **Height math**: the Notes arm now counts wrapped lines. `rows.Count`
  is no longer the row count on screen, so `Render` returns the height it
  built and `CreateCollapsibleSection` uses that (the same special-casing
  Summary already has, with the stronger property that the number cannot
  drift from what was built because it IS what was built).
  `PlanContentHeightMath.SectionBodyHeight` is untouched; its class doc's
  "no text wrapping anywhere in the file" sentence and
  `PlanViewModel`'s matching comment were corrected.
- **`PlanViewModelBuilder`**: the forge-scope caveat collapses from three
  hand-split sentence rows to one row, retiring the M14 workaround.
- **`ISectionRelayoutSink.RequestRerenderAfterSettle`** (new): the seam a
  re-ellipsis closure uses to ask for one deferred rebuild when the
  settled width changes a note's line count - see the resize decision
  below.

### Design decisions (recorded, not incidental)

- **Over-long single word hard-splits, it does not ellipsize.** A token
  wider than a whole line is broken across lines at the widest prefix
  that fits, so nothing is lost - ellipsis is exactly the text loss this
  work removes. Ellipsis survives only as the tail of a note past
  `TextWrapMath.MaxWrappedLines` (24).
- **Coin cell on the first line only**, so only the first line's budget
  is reduced by it and every later line gets the full width.
- **Continuation lines carry the same two-space indent** as the first, so
  a wrapped note reads as one block rather than as several notes.
- **Resize re-wraps at settle; a changed line count defers to a rebuild.**
  `RunReellipsis`/`ReplayRelayout` must never change a row's height (see
  `CraftingPlanView`'s `_relayoutActions` field comment; that property is
  what lets the settle pass skip scroll preservation entirely), and this
  section spends one row per line - so a width that changes a note's line
  count changes the section's height. The closure writes the new text
  back in place while the count is unchanged; when it moves, it calls
  `ISectionRelayoutSink.RequestRerenderAfterSettle` and `ResizeSettleStep`
  runs one `PreserveScrollAcross(() => RenderPlan(_currentPlan))` after
  the pass finishes - deferred because `RenderPlan` clears the registry
  `RunReellipsis` is iterating. At most one rebuild per settled drag, only
  when a line count actually moved, and through the same scroll-preserving
  path every other rebuild (Generate, pill re-solve, hide-unlocked toggle)
  uses. The earlier slot-pinned variant of this pass was rejected in
  review: padding a shortened note to its old slot count leaves permanent
  blank 28px rows INSIDE the section, mid-note, for the rest of the
  session's plan.
- **A truncated note puts the full text on the tooltip of every one of
  its rows**, not just the last one, so a hover anywhere on the note
  reads the whole thing.

### Tests (+39, 1890 -> 1929)

`TextWrapMathTests` (24): ellipsis parity cases (fits whole, truncates,
budget narrower than "...", non-positive budget, null measure); short
text stays one line; empty and null text still produce one line; long
text breaks at word boundaries with every word kept; no line exceeds its
budget; wrapping consults the measurement rather than counting characters
(a proportional measure where "i" is narrow); over-long word hard-splits
losslessly, both alone and after normal words; explicit `\n`, `\r\n` and
`\r` breaks compose with width wrapping and force a break even when the
text would fit; blank source line keeps its own row; leading indent is
content; the first-line budget is honored separately; past the line cap
the tail ellipsizes and reports truncation; a zero budget terminates with
the text intact; the wrap is deterministic at a fixed width (what the
renderer's line-count comparison rests on) and a wider budget genuinely
needs fewer lines (the widen case behind the deferred rebuild).

`NotesSectionLayoutMathTests` (15): the text budget reserves the coin
cell plus its gap and matches the shared `NameMaxWidthBeforeColumn`
formula; short note is one indented line; empty and null notes still
occupy one row; the real forge-scope note wraps with no ellipsis and no
lost words; every line of a wrapped note is indented; a valued note's
first line is shorter than the rest and both respect their own budgets;
explicit breaks compose with width wrapping; narrowing then widening back
recovers exactly the original lines, with no blank line and no leftover
ellipsis; a very
narrow panel does not degenerate; null measure throws; body height counts
wrapped LINES (and would have been undercounted by the old per-row arm),
zero/negative lines are zero, and one line is exactly
`FallbackTextRowHeight`.

`PlanViewModelBuilderNotesForgeScopeTests` now pins the caveat to ONE
`NoteLine` row carrying all three sentences (it asserted three rows
before), which is the regression guard on the retired hand-split.

Validation: module build 0 errors; suite 1929/1929.

Desktop gate should look at:
1. Generate the Mystic Clover x77 plan and open Notes. The forge-scope
   caveat must render in full with no "..." anywhere in the section, and
   the section body must end flush against its last line of text - no
   clipped final row and no blank rows, which is the check that the
   renderer's returned height matches what it built.
2. The forge-scope caveat is now ONE ~243-char note row, so item 1 is
   itself the wrap check: it must read as two or more indented lines that
   hang together as one block, with no "...". Also worth confirming on a
   second long note if the preflight account can produce one - a seasonal
   vendor tip ("During <festival>: <merchant> trades <cost> for Nx <item>
   (limit N purchases/week)") or a recipe-sheet savings lead-in.
3. Valued notes (any Excess/reclaim line, or the "Total reclaimable
   value" rollup): the coin amount must sit at the right of the note's
   FIRST line with the icons still to the RIGHT of each number, and the
   text must not run under it.
4. Narrow the window until a note re-wraps, then widen it back. Mid-drag
   the section must not jump or lose scroll position; at settle the note
   must re-flow to its correct line count at the new width, with NO blank
   28px rows anywhere inside the section and no "..." at a width the text
   fits. Scroll position must survive the settle-time rebuild that the
   changed line count triggers - this is the item that exercises
   `RequestRerenderAfterSettle`, so also confirm repeated narrow/widen
   cycles do not accumulate drift or flicker.
Gate: PASS (2026-08-22 evening desktop batch, branch build 35b83f9,
captures preflight/gK1-gK3). Generated Mystic Clover x77 live on
the branch build: the Notes section read "Notes (1)" and the forge
caveat rendered as ONE note wrapping naturally across two width-fit
lines (breaking mid-sentence at "e.g. / precursor forging"),
replacing the three hand-split single-sentence rows the 2026-08-22
photography captures show on master. Section heights composed
correctly below Crafting Steps with the timegate line intact. The
resize-across-a-line-count-change rebuild (RequestRerenderAfterSettle)
was not exercised live - synthetic resize-grip drags are documented
unreliable - and stands on the TextWrapMath/NotesSectionLayoutMath
tests plus the verify pass's height-contract walk.

---

## Audit batch E: tree interaction honesty (audit-de-cost-tree)

Four maintainer-approved UX-audit findings, all one story: a pill's
appearance should tell you whether you can click it, and the tree's
actions should be reachable when you need them.

- **H3, dimmed pills lied.** A reference branch under a bought item ("what
  it would cost to craft instead") drew a full, full-strength-looking pill
  set that no click handler was ever wired to, and nothing explained the
  silence. Three parts: (1) any pill whose click is dead only because the
  row is dimmed now carries "Under a bought item - switch the parent to
  CRAFT to change this", appended rather than assigned over, so a dimmed
  Subdued pill keeps its "why it loses" text and a dimmed committed pill
  keeps its value-detail hover; (2) the predicate for "is this a click
  target" moved to `DecisionPillPlanner.IsInteractive`, because the view
  read it twice - once to wire handlers, once to decide who needs
  explaining - and those two readings drifting apart is how the dimmed set
  ended up silent; (3) a dimmed row's pills dim to 0.6 rather than the
  0.35 its name/quantity/cost still use (at 0.35 every hue crushed to the
  same near-black ring), and the "this whole branch is inactive" job moved
  to a 2px rule down the dimmed row's own indent channel, which joins
  across consecutive rows into one continuous line per depth. The rule
  sits inside the existing `TreeRowHeight`; no height math changed.

- **M3, the toolbar scrolled away.** Best Path / Craft All / Buy All /
  Expand All / Collapse All lived in the Recipe Tree's section header,
  inside the scrolling content area, so a long plan scrolled Collapse All
  off screen at exactly the point it became worth pressing. They now sit
  in a dedicated row of the non-scrolling top strip, right-anchored, under
  a "Recipe Tree:" label naming what they act on; the section title stays
  in the scroll flow with the tree. The three plan-mutating presets are
  separated from the two view-only actions by a wider gap. The state they
  mutate stays with `TreeSectionController`, which publishes a
  `TreeToolbarCommands` instance on every tree render and withdraws it in
  `ResetTreeRenderState`; the buttons read that field at click time, so a
  click between one render dropping a tree and the next publishing one
  does nothing rather than reaching into disposed controls. The strip's Y
  arithmetic moved to the Blish-free `Services/TopRegionLayoutMath`, whose
  guarantee is that a hidden toolbar row costs exactly zero - the strip
  without it is byte-identical to the strip before the row existed.

- **M11, chrome looked clickable and TP looked like nothing.**
  UNKNOWN/UNRECOGNIZED/CURRENCY/GUILD UPGRADE drew the same crisp grey
  ring an Available pill draws; `PillKind.Locked` now recesses its ring to
  45% alpha and drops its label to 78% white. `PillKind.Subdued`
  deliberately does not follow - it looks muted but is a real click
  target, which is exactly what its separate switch arm was kept for.
  Separately, the Shopping List badged VENDOR/CURRENCY/UNKNOWN rows and
  left Trading Post rows bare, so "no badge" silently meant TP; every row
  is badged now, which makes an unbadged row a defect rather than a
  statement. Badging the majority row type promoted a latent overlap - the
  name column's ellipsis budget never reserved room for the tag - so the
  tag width is now subtracted from that budget in both the build and the
  re-ellipsis pass.

- **L6, pills vanished silently.** A tree row's pill column is a fixed
  240px budget and pills past it were simply not rendered, with nothing on
  the row to say they existed; the IGNORE toggle is emitted last, so it
  was usually first to go. `PlanRelayoutMath.ComputePillFit` now escalates:
  draw everything at normal padding; failing that, tighten side padding
  from 6px to 3px and draw everything; failing that, draw as many tightened
  pills as fit alongside a trailing "+N" pill whose tooltip names exactly
  what was left out. Tightening alone resolves the common case, so "+N" is
  the rare tail. Its tooltip deliberately does not say "widen the window":
  `pillColX` and `maxRightEdge` move together, so the budget is the same at
  every panel width and no window size would have shown the hidden pills.
  That same width-invariance is why the fit is resolved once at build time
  while the resize closure only repositions. Per the finding, the "+N" pill
  is not wired to a popup offering the hidden options: the hidden pills are
  almost always the trailing annotation and the IGNORE toggle, and a real
  affordance means a new popup surface with its own dismiss/focus/scroll
  behaviour hanging off a case tightening already resolves - the desktop
  gate decides whether the fact needs an affordance.

Review round (audit-de-cost-tree), two defects found against the batch as
written and fixed on the same branch:

- **H3's dimmed Subdued pill did not actually keep its "why it loses"
  text.** `PillSubduingTooltipBuilder.Build` was called only inside the
  `if (interactive)` arm, and `interactive` is false on a dimmed row, so
  the pill the bullet above (and gate step 3) names as the both-tooltips
  case showed only the "Under a bought item" line. The subduing text is
  now built from the spec before the interactivity branch - it is pure
  text and never depended on the click wiring - and `PillKind.Subdued`
  gained its own tooltip arm for the not-wired case. Gate step 3's
  "must show BOTH" is now the behaviour the code implements.

- **M3 moved the five actions out of the region batch F dims.**
  `SetContentDimmed` (the "this is the plan you are replacing" fade
  applied for the length of a Generate run) writes only
  `_contentPanel.Opacity`, and the toolbar row is parented to the strip,
  so the buttons that mutate the superseded plan sat at full brightness
  above a faded tree - and, since Opacity does not block hit-testing,
  Best Path / Buy All still re-solved it mid-run. `SetContentDimmed` now
  drives the toolbar panel's opacity and disables the five buttons for
  the length of the run, restoring both on every exit path the content
  dim is restored on.

Height-math check at this HEAD: nothing in this batch changes a
renderer-emitted height. The dimmed rule and every pill (including "+N")
live inside the unchanged `TreeRowHeight`; the shopping tag lives inside
the unchanged `ShoppingRowHeight`; the tree's section header is a fixed
30px whether or not it holds buttons. The one new height contract is the
top strip's, and `TopRegionLayoutMathTests` pins it with absolute pixel
literals in both toolbar states.

Validation: build 0 errors, full suite 1968 passed / 0 failed (1906
baseline, 1939 after batch D). No new test references Blish.

What the desktop gate should look at:

1. **Toolbar placement:** generate a plan long enough to scroll, expand
   several branches, scroll to the bottom. All five buttons must still be
   on screen and working. Confirm Collapse All from that scrolled position
   actually collapses, and that Best Path / Craft All / Buy All still
   re-solve. At minimum window width (930) the five buttons must not
   collide with the "Recipe Tree:" label.
2. **Toolbar row appearing:** with no plan on screen, the strip must show
   no toolbar row and no gap where one would be. Press Generate; the row
   must appear once, with everything below it shifting down by exactly one
   row, and the content area must not flash or lose scroll position.
   Generating a second plan must not shift anything.
3. **Dimmed branch:** expand a bought node's reference branch. The pills
   must be readable as pills (not a row of near-black rings), a single
   continuous vertical rule must run down the branch's left indent, and
   hovering a dimmed CRAFT/TP/VENDOR or IGNORE pill must show the "Under a
   bought item" line. Hovering a dimmed pill that already had a tooltip
   (a Subdued pill, or a committed pill with value detail) must show BOTH,
   not one replacing the other.
4. **Non-interactive chrome:** find an UNKNOWN or CURRENCY pill next to a
   clickable Available pill on another row. The two must be
   distinguishable at a glance without hovering.
5. **"+N" pill:** find a row with many pills (a partly-owned item with
   three sources, so CRAFT/TP/VENDOR + "HAVE n/m NEEDED" + IGNORE). Either
   every pill fits at the tightened padding - which is the expected common
   outcome - or a "+N" pill sits at the end whose tooltip names exactly the
   pills that are missing. A row must never simply end early with pills
   silently gone. Confirm the tightened pills still read cleanly.
6. **Shopping badges:** every Shopping List row must carry a badge, TP
   rows included, and a long item name must not push its badge into the
   Amount column.
7. **Toolbar during a re-generate:** with a plan on screen, change a
   setting and press Generate. For the length of the run the toolbar row
   must fade with the plan below it and its five buttons must be
   unclickable, then both must come back at full strength when the new
   plan lands - including on a cancelled or failed run.

Gate: PASS (2026-08-22 evening desktop batch, branch build at the
review-fix HEAD, captures preflight/gDE1-gDE4) - this is the live
desktop verification the maintainer required before this batch may
merge. Verified on the restored Mystic Clover x77 plan: (1) plan
header as one left-aligned Font32 "Mystic Clover x 42 needed" line,
no in-scroll timestamp; (2) promoted Actual Cost to Craft tile with
"+ 3 currencies required" under its caption, three tiles sharing a
bottom line, section headers demoted below the title; (3) the tree's
Item / Source / Cost header row with per-denomination sub-columns -
coin and currency icons forming straight vertical rules down the
cost column; (4) the five tree actions in the fixed strip
("Recipe Tree:" cluster with the wider gap before Expand/Collapse);
Collapse All clicked from the strip WHILE SCROLLED TO THE BOTTOM
collapsed the tree with the viewport staying put - the M3 scenario
end to end; (5) a live "+2" overflow pill on the root row (its cost
cell spans three denominations, narrowing the pill column exactly as
L6 predicted); (6) dimmed subtree pills at the raised dim floor with
the value-detail tooltip still served on the dimmed selected pill
("Only available source / Crafting gold price ... Optimization
price"); (7) every Shopping List row badged - VENDOR rows and the
Glob of Ectoplasm TP badge. Not exercised live: gate item 7's
mid-generate toolbar fade (the restored-plan session did not run a
fresh generate on this build; the dim shares SetContentDimmed's
single restore funnel review-verified in batch F) and the narrow-
width pill-drop escalation beyond the +2 case. Suite 1969/1969 at
HEAD; the height-math contract was re-walked clean by the verify
round.

## Audit batches A+B+C tier 1 (audit-abc)

Three maintainer-approved audit items, each surgical: the tree's invisible
row carets (H1), an unconfirmed destructive Clear Cache (H5), and unwrapped
tooltip lines (H6 tier 1). Tooltip-container work is explicitly DEFERRED -
only the module-side content wrap is in scope here.

### A - visible tree carets (H1)

`Views/Rendering/TreeSectionController` set its expand/collapse caret to
`U+25BC`/`U+25B6` at four sites (Expand All, Collapse All, initial row
render, per-row toggle). Blish's font does not render those triangles, so
the caret column painted blank at every depth: nothing on a row said it was
expandable, and nothing said which state it was in.

The triangles date to the tree's original implementation (`d82d596`). When
`8e1ea34` moved the plan's SECTION headers to ASCII `"v"`/`">"` - after
pixel-level screenshot scans showed the triangles failing to render, a
finding `CraftingPlanView.cs` still records in a "do not re-attempt Unicode
without a fresh render check" comment - the row carets were left behind.
This branch completes that migration; both caret vocabularies now match
exactly.

No layout or height change: the caret Label is `AutoSizeWidth`, and the
icon column sits at a fixed `indent + TreeCaretColWidth` offset that never
consulted the caret's own width.

### B - Clear Cache confirmation (H5)

`Views/MainView`'s Clear Cache button destroyed the only cached account
snapshot on a single click - no confirm, no tooltip, no undo. The snapshot
can only be rebuilt from a reachable GW2 API, which is frequently the very
condition a user is stuck on when they reach for that button, so a misclick
could leave the module with no data at all until the API came back.

- The click now routes through the existing `Views/ModalDialog` behind an
  **unconditional** confirm (body: "Discard the cached account snapshot? It
  can only be rebuilt when the GW2 API is reachable.", verb: **Discard**),
  the same gate `LogTabContent`'s Delete Log File uses. `MainView` receives
  the shared dialog instance from `Module.Initialize` exactly the way
  `LogTabContent` already does - one `ModalDialog` for the module, disposed
  once, no second window.
- The button gains `BasicTooltipText` stating the consequence before the
  click, matching Delete Log File's own tooltip.
- Status casing drift fixed: "Cache Cleared" -> "Cache cleared", matching
  every other status string in the module.

The destructive work is deliberately unchanged and stays **inline on the
main thread**, unlike Delete Log File's `Task.Run`. `Module.ClearCache` is a
`CancellationTokenSource` cancel plus one `SnapshotStore.Delete` and three
field resets inside `SnapshotCommitGate.Clear` - no flush-queue drain, no
lock a background loop can hold - and the `SetSnapshot(null)`/`SetStatus`
tail is control mutation that must run on the main thread regardless.

Interposing a dialog does open an interleaving the single-click version
could not have: Refresh Now disables Clear Cache for its whole duration,
but not the reverse, so a user could open the confirm, click Refresh Now
behind it, and then Discard. `Module.ClearCache` resets `_pendingSnapshot`/
`_snapshotDirty` under `SnapshotCommitGate`, which covers the BACKGROUND
refresh path - but `MainView.RefreshNowAsync`'s own awaited continuation
runs outside that gate and would either repaint the snapshot the user just
discarded (printing "Updated - ..." over "Cache cleared") or, on the
cancellation `ClearCache` itself causes, overwrite the status with a
classified refresh failure. Two guards, because either alone has a hole:

- **Both buttons are disabled for the confirm's lifetime**, re-enabled from
  the confirm callback and from the cancel callback alike (the cancel
  callback now also runs on X/Escape - see below). A `_clearConfirmOpen`
  flag, not just the buttons' `Enabled` flags, is what holds the gate:
  `Build()` recreates both buttons on every tab visit, so a tab switch
  while the dialog is open would otherwise hand back a live Refresh Now.
- **A `_clearGeneration` counter**, bumped by the confirm callback and
  captured by `RefreshNowAsync` before it awaits. An in-flight refresh whose
  generation changed drops its own tail - no persist, no repaint, no failure
  status - and lets the "Cache cleared" the user asked for stand. The field
  is `volatile`: every write is a main-thread click, but the comparison runs
  on a continuation Blish's context-less XNA host may resume on a
  ThreadPool thread, so it is re-checked inside the marshalled block too.

`ModalDialog.Show` now returns `bool` so a caller that arms state for the
dialog's lifetime can tell an opened dialog from a request refused because
another caller's dialog is already on screen.

### B2 - the shared ModalDialog could be stranded closed (regression class)

`Views/ModalDialog` had the exact defect item 37 already found and fixed in
`Views/ApiAccessDialog` (see "Fix 2" above): `_isShowing` was cleared only
by the Confirm/Cancel `StandardButton` handlers, while `CanClose` and
`CanCloseWithEscape` both default true and `WindowBase2.OnLeftMouseButtonPressed`
calls `Hide()` directly on a title-bar X click. Dismissing the dialog with X
or Escape therefore left `_isShowing` stuck true, and because this is ONE
instance shared by the whole module, every later `Show()` from every caller
- Clear Cache, the Log tab's Delete Log File, the Crafting Plan tab's
regenerate gate - silently no-op'd for the rest of the session. The fix was
written up for the sibling class and never applied here.

`ModalDialog` now subscribes `_window.Hidden += OnWindowHidden` the same
way, and both button handlers and the X/Escape path funnel through one
private `Dismiss(bool confirmed)` that clears `_isShowing`, reads the
callbacks into locals and nulls the fields before invoking either. X and
Escape therefore behave as **Cancel**, which is what the callers already
want: `CraftingPlanView`'s cancel callback reverts and re-enables the
own-materials checkbox (previously left disabled forever by an X dismissal),
and Clear Cache's re-enables the Snapshot buttons. The same revert also runs
when `Show` REFUSES (another tab's confirm already on screen): both callers
that arm state before showing - `MainView` and `CraftingPlanView` - now read
the `bool` and unwind, so a refused request cannot strand a control either. `Dispose()` unsubscribes
BEFORE hiding so module teardown cannot fire a caller's cancel callback into
controls already being disposed.

### C tier 1 - centrally wrapped tooltips (H6)

`Services/ValueDetailTooltipBuilder` and `Services/TreeRowTooltipComposer`
compose multi-line tooltip text whose individual lines were unbounded: the
opportunity-cost sentence is 76 characters, the vendor price-side caveats
83.

`Services/TooltipTextFormat` (new, Blish-free) is the single wrap seam.
It wraps each line of a composed tooltip to a **75-character** budget at
word boundaries by reusing `TextWrapMath` - batch K's tested wrapper -
through a character-count measure function; no wrap logic is duplicated.
Both composers route their finished output through it at their **return
seam**, so every present and future caller inherits the wrap without
knowing it exists. Existing hard breaks and blank separator lines are
preserved, short lines are returned untouched, and an unbreakable
over-budget token is hard-split rather than ellipsized.

**What this seam is and is not for.** It is NOT what keeps a tooltip inside
the module window: the measured 500px `BasicTooltipView.MAX_WIDTH` below is
already comfortably inside the window's 930px clamped minimum, and Blish
applies it unconditionally. What the seam adds is a break point the module
controls and, more importantly, `TextWrapMath`'s hard split for a token that
Blish's space-only wrapper would let overflow the cap outright.

That is also why the budget is **75 and not lower**. 500px at
`DefaultFont14`'s roughly 6.5px-per-character prose average is about 76
characters, so 75 reproduces the break Blish would have made anyway instead
of narrowing to it. A narrower budget (the 60 this branch first shipped,
~390px) would have added lines to every over-budget tooltip - and height is
the axis with the real defect: per (a) below, a tooltip that does not fit
above the cursor is placed 36px BELOW it and never clamped to the bottom
screen edge, so extra wrapped lines are extra lines that can fall off the
screen.

The budget is characters, not pixels, because a tooltip string is composed
in `Services`, far from any font; threading a measured `Func<string,int>`
down from `Views/Rendering` would put a Blish dependency on the very seam
this class exists to keep Blish-free.

Wrapping is applied **per source line** rather than by handing the whole
composed string to `TextWrapMath.Wrap` in one call: that method caps a
single wrap at `MaxWrappedLines` (24) and ellipsizes the tail past it - a
cap sized for one note in a fixed-height panel. A tooltip is many
independent lines, and dropping its tail would be exactly the silent text
loss this wrap exists to remove.

### Measured: what Blish itself does about tooltip position and width

Decompiled from `packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe` with
`ilspycmd` (repo precedent). These are **measured** readings of BlishHUD
1.3.0, not inference, and they are what justifies doing content width in
the module today.

**(a) POSITION - two one-sided flips, no clamp.**
`Blish_HUD.Controls.Tooltip.UpdateTooltipPosition` is the only positioning
logic (it runs on every `MouseMoved` and every `UpdateContainer` tick):

```
int y = (mouse.Y - 36 - tooltip.Height > 0) ? (-36 - tooltip.Height) : 36;
int x = (mouse.X + tooltip.Width >= SpriteScreen.Width) ? (-tooltip.Width) : 0;
tooltip.Location = mouse.Position + new Point(x, y);
```

- Vertical: places the tooltip above the cursor when it fits, else 36px
  BELOW it. That protects the top screen edge only - a tall tooltip near
  the bottom of the screen is placed below the cursor and is never clamped
  to the bottom edge.
- Horizontal: shifts left by the tooltip's full width at the right edge.
  That protects the right edge only - the result is never clamped to
  `>= 0`, so a tooltip wider than the cursor's X lands at a NEGATIVE X.
  With the 500px content cap plus chrome that needs the cursor within
  ~510px of the left edge AND within a tooltip-width of the right edge, so
  it is unreachable above roughly 1020px of screen width; the unclamped
  BOTTOM edge is the reachable one.

**(b) WIDTH - a fixed 500px cap, space-split wrapping, no indication.**
`Blish_HUD.Common.UI.Views.BasicTooltipView` (the view `Control`
auto-creates for any `BasicTooltipText`) holds `MAX_WIDTH = 500`. On every
text assignment it measures unwrapped at `AutoSizeWidth`, and if the
measured label exceeds 500px it flips to `AutoSizeWidth = false;
WrapText = true; Width = 500`. So Blish does cap width - but:

- 500px is an absolute constant: it knows nothing about the module window
  (930px clamped minimum) or the screen. It happens to sit inside that
  window, so it does keep a tree-row tooltip within it - but by coincidence
  of two unrelated numbers, and it would not on a narrower window or a
  window the user can shrink further.
- The wrapper, `Blish_HUD.DrawUtil.WrapText`, splits each `\n` segment on
  `' '` only and never splits an over-long single token, so an unbroken
  run wider than 500px overflows the cap outright. The module's
  `TextWrapMath` hard-splits instead.
- Nothing indicates the cap was hit - no ellipsis, no marker.
- `LabelBase.GetTextDimensions` wraps against
  `LabelRegion.X > 0 ? LabelRegion.X : _size.X`, and `LabelRegion.X` is
  still the OLD unwrapped width on the pass that follows the 500px clamp,
  so the auto-height for a freshly-set tooltip is measured against the
  wrong wrap width for one layout pass.

**Deferred - possible future upstream Blish PR (note only, not scoped
here).** Two candidates, both small and both upstream: clamp
`UpdateTooltipPosition`'s result to the `SpriteScreen` bounds on all four
edges rather than flipping on two, and make `BasicTooltipView`'s max width
relative to the screen (or settable) instead of a hard 500. Neither is
attempted in this repo; recorded so a future contributor does not have to
re-derive the readings above.

### Desktop gate (live, required)

1. **Tree carets visible and flipping:** generate any plan with a
   multi-level tree. Every expandable row must show an ASCII `>` when
   collapsed and `v` when expanded, in the same vocabulary as the section
   headers above it. Click a row: its caret must flip. Click Expand All
   then Collapse All: every row's caret must flip with it, at every depth,
   with no row left showing a blank caret column and no row shifting
   vertically.
2. **Clear Cache dialog, both paths + tooltip:** on the Snapshot tab, hover
   Clear Cache - the tooltip must state that the snapshot can only be
   rebuilt when the GW2 API is reachable. Click it: the confirm dialog must
   appear with the verb **Discard**. Press **Cancel** - the snapshot must
   be completely untouched (same items, same coin total, unchanged status
   line). Click Clear Cache again and press **Discard** - the snapshot must
   clear, the status must read "Cache cleared" (lowercase "cleared") ahead of
   its dash and timestamp, and Refresh Now must still rebuild it.
3. **Dialog dismissed by X/Escape does not strand the module:** click Clear
   Cache and dismiss the confirm with the title-bar **X**, then again with
   **Escape**. Each time the snapshot must be untouched and BOTH Snapshot
   buttons must come back enabled. Then, without reloading the module,
   confirm all three ModalDialog callers still open: Clear Cache again, the
   Log tab's Delete Log File, and the Crafting Plan tab's own-materials
   regenerate prompt. Any one failing to appear is the stranded-`_isShowing`
   regression. Also dismiss the own-materials prompt with X: the checkbox
   must revert to its previous state and be re-enabled, not left greyed.
4. **Clear Cache while the confirm is open:** click Clear Cache - Refresh
   Now must go disabled with it while the dialog is up, and both must
   re-enable on Cancel. Then switch tabs with the dialog still open, come
   back, and confirm Refresh Now is still disabled. With both guards in
   place the overlap should be unreachable from the UI; the check is that
   the disabling actually holds across the tab switch. The `_clearGeneration`
   guard behind it has no live gesture left to exercise - if one is ever
   found, its expected result is that the status settles on "Cache cleared",
   never "Updated - ..." and never a refresh-failure line.
5. **A refused Show strands nothing:** with the Clear Cache confirm still
   open, switch to the Crafting Plan tab (with a plan on screen) and click
   Use Own Materials. No second dialog may appear, and the checkbox must
   snap back to its previous state and stay clickable - not left greyed
   with its new value.
6. **Value-detail tooltip inside the window:** generate a
   currency-bearing plan (a Mystic Clover / spirit-shard chain, anything
   whose committed CRAFT or VENDOR pill diverges) and hover that pill. The
   value-detail tooltip must show the opportunity-cost sentence wrapped
   across lines, entirely inside the module window, with no line running
   past the window edge and no text cut off. Hover a vendor row whose
   price-side caveat fires and confirm the same for its longer sentence.

Gate: PASS (2026-08-23 desktop session, branch build at the
review-fix HEAD, captures preflight/gA1-gA2, gB1-gB5). (A) The tree
row carets rendered visibly for the first time - "v" beside the
expanded root and Obsidian Shard rows, matching the section-header
vocabulary. (B) The Clear Cache button showed its consequence
tooltip; clicking it raised the Confirm dialog with Discard/Cancel
and BOTH Snapshot buttons disabled for the dialog's lifetime (the
interleave guard live); the Critical's regression path was
exercised explicitly - Escape dismissed the dialog and the NEXT
Clear Cache click reopened it (pre-fix, the stuck flag would have
dead-buttoned every confirm in the module for the session); Cancel
restored both buttons with the snapshot intact. The Discard path
itself was not fired (preserving the canned fixture); its handler
body is unchanged from the previously-shipped synchronous code.
(C) The value-detail tooltip's opportunity-cost sentence wrapped
onto a second line fully inside the window (previously cut dead at
the edge); the translucent background remains, as scoped - that is
tier 2's job. The two unwrapped feeder lines the review noted
(item-name prefix, pill-tooltip left operand) are recorded for the
tier-2 facility to absorb.

## Audit batch H: table density (audit-h-density)

Two maintainer-approved UX-audit findings, one premise: every data table
in the module splits the name (pinned far left) from the numbers (pinned
far right), so widening the window widens an empty band down the middle
of every row 1:1 rather than making the table more readable. The audit
measured 330-520px of that band at ordinary widths.

- **M2, dead gutters in the plan tables.** All seven tables in the
  Crafting Plan's scroll column - the recipe tree (pill + cost columns),
  the Summary currency table (Required/Have/Needed + the full-coverage
  marker), Used Materials (Amount), the Shopping List
  (Amount/Each/Total), Required Recipes (Status), Required Disciplines
  (Level) and Crafting Steps (its right-aligned sublabel) - anchored
  their right-hand block to `panelWidth - 8`. Each now pulls that block
  in beside the names:
  `PlanRelayoutMath.RightBlockX` takes the block's pinned x and the widest
  name extent the table renders and returns the pulled-in x, clamped so it
  never moves RIGHT of the pinned position (a narrow window degrades to
  exactly the previous layout) and never left of `TableRightBlockMinX` (a
  table of two-letter names should still read as a table). The widest name
  extent comes from a per-render, data-derived measure pass over the
  UNTRUNCATED names - truncated widths would be circular, since the
  ellipsis budget is derived from the block position. It is measured once
  and cached alongside the column maxima each table already cached, so a
  resize tick re-derives edges without re-measuring anything.

  The tree rides the whole-tree pre-scan batch D introduced
  (`TreeCostColumnMath.ScanColumns` - the cost sub-column widths and the
  name extent now come out of the SAME single walk), for the same reason
  that scan covers unbuilt rows: rows are built lazily, so a
  visible-rows-only extent would move every column the first time a node
  was expanded. The Shopping List already pre-measured Each/Total per
  render and simply measures two more things in that loop; Used
  Materials, Required Recipes, Required Disciplines and Crafting Steps
  had no pre-scan and get one.

  The four flat tables share one expression for the anchor,
  `PlanRelayoutMath.RightBlockRightEdge` (pull the block in, then derive
  its right edge), and one margin constant, `TableRightMargin`. Two of
  the three later arrivals have a specific worth recording. Required
  Disciplines' Level column is bounded by the character-availability
  text rather than the discipline names, and that column's own
  "Characters" header counts into its extent; because the breathing room
  (24) exceeds the availability text's gap (12), the pull-in can only
  widen that ellipsis budget, never narrow it. Crafting Steps' name run
  is cursor-concatenated and never ellipsized, so its scan reproduces
  that concatenation exactly, and its TimegatedNotice rows - plain text
  rows with no columns - take no part in it. A section with no right
  column at all (no status tag, no level, no sublabel) measures a zero
  name extent and stays pinned, byte-identical to the previous layout.

  Fix round: the chrome had to follow the columns. A row divider or a
  header band spanning the full panel width no longer bounds the table
  it belongs to once that table's block has moved in - at 1600px the
  shopping list's rules ran ~1000px past the last number, and the
  currency table's dark header band spanned the window with its labels
  clustered in the left half. `RowRelayoutHelpers.FinishRow` takes a
  `dividerWidthForWidth`, `CTableHeaderRenderer` bounds its band by the
  `rightXForWidth` its callers already pass, and the currency table has
  `SummarySectionLayoutMath.CurrencyHeaderBandWidth`. All three resolve
  to exactly the panel width whenever the table's block is still pinned,
  and all three clamp to it, so a narrow window draws what it always
  drew and no caller's arithmetic can outrun its own row.

  Two invariants make this safe rather than merely tighter. First, the
  tree's pill and cost columns move as ONE block, so
  `maxRightEdge - pillColX` is exactly what it was before and batch E's
  `ComputePillFit` escalation (tighten, then "+N") sees an unchanged
  budget - a pill that fitted still fits, and no pill that was hidden
  becomes visible at some width, which is what its "+N" tooltip already
  promises. Second, `TableGutterBreathingRoom` (24px) exceeds every
  name-to-column gap that feeds `NameMaxWidthBeforeColumn` (8 in the tree,
  12 in Used Materials/Shopping List, 14 in the currency table), so
  closing the gutter can never ellipsize the very name it was measured
  from - asserted directly, per table, in `PlanRelayoutMathTests`,
  `ShoppingColumnMathTests` and `SummarySectionLayoutMathTests`.

  The tree's "Cost" header had to follow its column, so
  `CTableHeaderRenderer` gained `rightXForWidth` beside the
  `middleXForWidth` batch D added for "Source".

- **M8, Snapshot header density.** The header spent ~179px on five sparse
  rows before the first result: title+buttons, status, a search row empty
  for everything right of the dropdown, a full-width checkbox row, and a
  24px unlabelled coin row. The source checkboxes now occupy the search
  row's empty right half - but only while the whole run fits there in ONE
  row. Sharing the row halves the width the run flows into, and the fix
  round measured what that costs a real roster: 19 cells (15 characters
  plus the storage locations and the master toggle) flowed ~6 per row at
  full width - 4 rows, exactly the cap, every filter visible - and ~3 per
  row beside the search box, putting roughly a third of the filter set
  behind a scrollbar inside a 117px box to save 38px of header. Past one
  row the run therefore falls back to its own full-width row below the
  search box, gap included, exactly as it sat before. The saving that
  motivated the move is untouched, since a run that fits beside the
  search box is precisely the case where sharing costs nothing.

  `SourceFilterFlowLayout` remains the layout engine - its 4-row cap and
  past-the-cap scrolling are untouched - and is simply handed the
  placement's width, with the panel carrying the start offset so cells
  still flow from 0 in their own coordinates. Cells are still laid out
  sequentially from their own measured widths (verified, not changed).
  `Services/SnapshotHeaderLayout` holds what follows: the reduced width,
  the shared/own-row placement (x, offset y, width), and the band height
  - the taller of the search row and the run while they share, the search
  row plus the gap plus the run when they do not. The mode is the flow's
  OUTCOME, not an input, so `ApplyTopRegionLayout` flows beside the
  search box first and re-flows full width when that wrapped; both modes'
  caps are read up front and both join the resize early-out's cache key,
  which is the container width. The search row's own panel is sized there
  too: it stops at the run's start x while they share (two overlapping
  full-width panels would leave which one receives a checkbox click to
  child ordering) and spans the row when they do not. The coin total
  gained a dim "Coin" caption (rebuilt with the segments, since the
  refresh disposes that panel's children) so it stops reading as a stray
  list row.

Known limit, deliberate: below a content width of ~470px a SHARED filter
run would have no width to flow into. The window enforces a 930px minimum
(884px content region), so that state is unreachable; `SourceFilterWidth`
floors at 0 rather than going negative, and `SourceFilterFlowLayout`
already degrades to one cell per row - wrapped, then scrolled, never
clipped away. A run that wrapped would in any case have dropped to its
own full-width row before reaching that width.

Height-math check at this HEAD: no renderer-emitted height changes.
Batch H moves columns horizontally only - every row height, every
`PlanContentHeightMath` contract and the Summary section's own
`BodyHeight` are untouched. The Snapshot tab's Y arithmetic does change,
but it is view-local (`CoinRowY`/`ContentY`) and now routed through
`SnapshotHeaderLayout.SearchBandHeight`, pinned by tests.

Validation: build 0 errors, full suite 2113 passed / 0 failed (2072
baseline). No new test references Blish.

What the desktop gate should look at:

1. **Tree gutter closed:** generate a plan and look at the Recipe Tree at
   the default window width. The pill column must sit just right of the
   longest item name rather than out at the panel edge, with no wide empty
   band between name and pills. Expand a deep branch: the columns must NOT
   jump when previously-unbuilt rows appear.
2. **Header row tracking:** the "Item / Source / Cost" header must sit
   over the columns it names in both states, and must stay over them while
   the window is dragged wider and narrower - including at the 930px
   minimum, where the layout should look exactly as it did before this
   branch.
3. **Shopping List and Used Materials:** the Amount/Each/Total block (and
   Used Materials' Amount) must be pulled in beside the names, header
   labels still aligned with their columns. A long item name must still
   ellipsize and keep its source badge out of the Amount column - and a
   name that was NOT truncated before must not have become truncated.
4. **Currency table:** Required must start relative to the currency name
   column, and the green "OK" marker must stay at the block's right end,
   not the panel's.
5. **Required Recipes, Required Disciplines, Crafting Steps:** scroll the
   whole plan at a wide window (1400px+) and watch the right-hand
   columns. Status, Level and the craft-step sublabels must sit beside
   their names like the other four tables - no column may still be out at
   the panel edge, and nothing may zig-zag between two anchors down the
   scroll. The "Recipe / Status" and "Discipline / Characters / Level"
   headers must stay over their columns at every width, and the
   "Characters" header must not be covered by the Level column when the
   availability text is short.
6. **Row rules and header bands:** the 2px rule under each shopping /
   used-material / recipe / discipline / craft-step row must stop just
   past that table's last column, not run on to the panel edge; the dark
   header bands (currency table, Recipes, Disciplines, the tree) must
   likewise end just past their last column. At the 930px minimum both
   should look exactly as they did before this branch.
7. **Snapshot search row holding the checkboxes:** with a small roster the
   source checkboxes must sit to the right of the content-type dropdown
   on the same row, clickable (each click must still filter the results),
   with the result list starting visibly higher than before. With a large
   roster (10+ characters) the run must instead drop to its own
   full-width row below the search box and use the full width there - the
   pre-branch layout - rather than wrapping into the narrow half. Check
   the handover both ways by dragging the window wider and narrower: the
   run must move between the two rows cleanly, with no overlap of the
   search box or dropdown at any width, and every character reachable in
   both modes.
8. **Coin caption:** the wallet total must read as a dim "Coin" label
   followed by the gold/silver/copper figures, with each coin icon still
   to the RIGHT of its number.

Gate: PASS (2026-08-23 desktop session, branch build at the
review-fix HEAD, captures preflight/gH1-gH3). (1) The dead gutter is
visibly closed across the plan's scroll column on the restored x77
plan: Used Materials quantities sit directly beside the names
(x~338 vs the old ~955), Shopping List's Amount/Each/Total columns
pulled in with their HEADERS tracking the moved positions, tree
pills beside the names, and - the fix-round's work - the row
dividers end at their table's right edge instead of running the
full panel. (2) Snapshot header: the dim "Coin" caption renders
before the total, and with the 10-source test roster the checkbox
run correctly took the fall-back-below-the-search-row path (the
run does not fit in the shared-row width at 1400px), preserving
the wrap + cap behavior - the shared-row density win applies to
smaller rosters and was not demonstrable with this fixture.
Recipes/Disciplines sections have no live coverage in the fixture
plans (Mystic Forge plans render neither) - their migration stands
on the shared RightBlockX primitive and its per-table tests.

## Tooltip facility (tooltip-facility)

Audit item C tier 2. Tier 1 fixed the CONTENT of two tooltips (wrapping
composed lines through `Services/TooltipTextFormat`) and explicitly
deferred the container. This branch does the container - once, centrally
- because sizing, wrapping, placement and opacity are a UI-toolkit job,
not something each of the module's ~40 tooltip sites should re-derive.

`Views/Rendering/TooltipFacility` is the single choke point, with two
paths chosen by CONTENT rather than by caller:

- **PLAIN** - composed or long prose. Wrapped through
  `TooltipTextFormat` (tier 1's seam, kept as the text core) and handed
  to Blish's `BasicTooltipText`. Five short one-line literals stay bare
  assignments; a button label needs no facility.
- **RICH** - anything carrying a coin amount, which a string tooltip can
  only spell out as "1g 23s 45c". Drawn by
  `Views/Rendering/RichTooltipSurface` with real gold/silver/copper icons
  (icons RIGHT of their numbers, repo invariant) via
  `CoinCurrencyRenderer`, on an opaque background, clamped to all four
  screen edges.

The tree ROW tooltip and the decision PILL tooltip - audit H6's two
worst surfaces - are on the rich path. Everything else that composes or
runs long routes through the plain path.

### Blish-free split

`Services/TooltipContent` models a tooltip as lines of spans where a
coin span still knows its copper value; every span also carries plain
text, so `ToPlainText`/`ToPlainLines` reproduce byte-for-byte what the
three composers (`ValueDetailTooltipBuilder`,
`PillSubduingTooltipBuilder`, `TreeRowTooltipComposer`) returned before.
Each composer gained a `*Content` entry point and its old string entry
point became a plain-text view of it - which is why every pre-existing
composer test passes untouched.

`Services/TooltipLayoutMath` holds the row breaking (reusing
`TextWrapMath`, with the current row's remaining width as the first-line
budget so a span after a coin run wraps against what is left of the row)
and the placement. Both are unit-tested against synthetic measure
functions; the Blish-coupled shell is the ~180-line surface class.

**One wrap policy per path, never two on one string.** The structured
content is UNWRAPPED. The plain path wraps it at the composer's return
seam with `TooltipTextFormat`'s 75-character budget; the rich path wraps
the same content against a real font at a real pixel width. Applying
both would break every long line twice.

### THE LIFECYCLE DECISION: one shared surface, never one per control

**Decision.** There is exactly ONE `RichTooltipSurface` for the whole
module, repointed on hover. Content is held in a
`ConditionalWeakTable<Control, TooltipContent>` keyed by the hovered
control, so the facility never holds a control alive and a disposed
row's content is collected with it. `Module.Unload` disposes the single
surface. The facility deliberately offers NO API for a per-control
instance.

**Measured basis** (decompiled from
`packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe` with `ilspycmd`, repo
precedent - these are readings of BlishHUD 1.3.0, not inference):

- `Control.Dispose(bool)` clears effects, nulls every event, cancels
  tweens, sets `Parent = null` and calls `DisposeControl()`. **It never
  touches the control's `_tooltip` field.**
- A Tooltip is **not its owner's child**: `Tooltip.Show()` sets
  `Parent = Control.Graphics.SpriteScreen` and `Hide()` sets
  `Parent = null`. So `Container.DisposeControl`'s `GetDescendants()`
  sweep never reaches it either.
- Therefore **nothing in Blish ever disposes a Tooltip.** One instance
  per tree row and per pill - controls this module rebuilds wholesale on
  every render - would leak one undisposed container plus its whole
  child tree (Labels, coin icon Panels holding `AsyncTexture2D`
  references) per row per render.
- Correction to a common assumption: `BasicTooltipText` does **not**
  share one basic view. `Control.Tooltip`'s getter lazily creates
  `new Tooltip(new BasicTooltipView(_basicTooltipText))` **per control**,
  and only on hover, because `Tooltip.HandleMouseMoved` is the only
  reader (`Control.ActiveControl?.Tooltip`). Blish's own basic path
  therefore leaks too - one small Tooltip+Label per hovered control, plus
  another each time the text changes, since the `BasicTooltipText` setter
  nulls `_tooltip` without disposing it. It is small enough to have gone
  unnoticed; at this module's control counts it would not be.
- The one thing Blish does do on owner disposal:
  `Tooltip.ControlOnActiveControlChanged` subscribes `Disposed` on the
  ACTIVE control and answers with `SetCurrent(null)` -> `Hide()`. That
  prevents a visible orphan tooltip on screen; it does not prevent the
  leak.
- The shared instance is safe against use-after-dispose by Blish's own
  guard: `Control.Tooltip`'s getter checks `_tooltip._disposedValue` and
  returns null (or the basic fallback) rather than returning a disposed
  container. So a control that outlived `Shutdown()` simply shows nothing.

**Ordering hazard the facility encodes.** `Control`'s `BasicTooltipText`
setter nulls `_tooltip` whenever the text changes. `ApplyRich` therefore
clears `BasicTooltipText` BEFORE assigning the surface; the reverse order
would silently drop it.

**Content replaced under a stationary cursor.** `ApplyRich` finishes by
calling `RichTooltipSurface.RefreshShowing`, which redraws the box when
the surface is already visible for that same control (and hides it when
the new content is empty). This is parity with the plain path, not a
nicety: the `BasicTooltipText` setter either writes the new text into the
live `BasicTooltipView` (`ActiveControl == this`) or drops `_tooltip` so
the next hover rebuilds - both branches refresh. The rich path has no
such setter, and `Tooltip.HandleMouseMoved` calls `Show` only while the
tooltip is HIDDEN, so a re-applied content would otherwise stay stale
until the pointer left. The reachable path is the tree's settle
re-ellipsis (`UpdateTreeRowTooltip` from the re-ellipsis closure) firing
after a window resize while the cursor rests on the row: the full-name
line appears or disappears, and the visible box has to follow.

### What the surface adds over Blish's own tooltip

**(a) Four-edge placement.** Measured (and already recorded under "Audit
batches A+B+C tier 1"): `Tooltip.UpdateTooltipPosition` flips
above/below the cursor to protect the TOP edge and shifts left by the
tooltip's full width at the RIGHT edge, and clamps neither result - a
tall tooltip placed below the cursor runs off the BOTTOM of the screen,
and the left shift can land at a NEGATIVE X.
`TooltipLayoutMath.Place` keeps Blish's above-when-it-fits preference
and its 36px cursor gap, then clamps all four edges. When neither side
can hold the box with its gap it takes the roomier side and clamps in -
the only case where it may reach across the cursor, and it needs a
tooltip taller than the screen minus the gap to happen at all. Re-run
every tick while visible, because `Tooltip.UpdateContainer` re-runs
Blish's unclamped positioning every tick.

**(b) Mouse transparency.** `RichTooltipSurface.TriggerMouseInput`
returns null unconditionally. Blish's tooltips avoid becoming the active
control only by never being placed under the cursor - exactly the
constraint the clamp relaxes. Without the opt-out, a clamped tooltip
would win the hit test (Container and Label both default to
`CaptureType.Mouse`), fire `ActiveControlChanged`, and be hidden by
Blish's own handler on the next frame: a show/hide flicker loop.

**(c) Opacity.** Blish draws its tooltip art at `Color.White * 0.98f`
over whatever is behind it, which is the audit H6 bleed-through
complaint. The surface's content sits on an opaque panel inside the
content edge buffer, so the frame still reads as a Blish tooltip and
nothing shows through the middle.

**(d) A width that knows about the screen.**
`BasicTooltipView.MAX_WIDTH` is a hard 500 that knows nothing about the
display. `TooltipLayoutMath.MaxContentWidth` keeps 500 as the preferred
width - so a rich tooltip reads like every plain one - and narrows it on
a screen that cannot afford it, down to a 120px floor.

**(e) A wrapper that cannot overflow.** `DrawUtil.WrapText` splits on
spaces only and never splits an over-long token, so an unbroken run
wider than the cap overflows it outright. `TextWrapMath` hard-splits.

### Feeder lines fixed

Both lines the audit named as reaching a tooltip unwrapped now go
through the facility:

- the full item name prepended by `TreeSectionController.
  UpdateTreeRowTooltip` on an ellipsized row (item names are unbounded);
- the pill tooltip's head prose - the acquisition hints and the
  HAVE/OWN/"needs N total" sentences - which the pill loop used to
  assign raw and concatenate with `"\n\n"`.

Both are on the rich path, so they are wrapped against the real font at
the real pixel width rather than at the 75-character approximation of
it.

### Swallowed hover on the tree row (found migrating it)

The tree row tooltip was assigned to the row Panel only. Tooltip lookup
reads exactly ONE control - `Tooltip.HandleMouseMoved` uses
`Control.ActiveControl`, the deepest capturing control under the cursor -
so the row's own name and quantity Labels swallowed it. The tooltip fired
only over the bare strip of Panel beside them, which is the one place a
reader is not pointing. That is the same swallowed-hover class already
fixed in `ShoppingListSectionRenderer`, in `LogTabContent`'s rows, and in
this same file's pill outer/inner/label stamping; the tree row was the
remaining instance, and it hid exactly the full-item-name line the
tooltip exists to show. Both Labels now carry the content alongside the
Panel.

**Tooltips only - do NOT copy the row's handlers onto the Labels.** Mouse
EVENTS, unlike tooltip lookup, already reach the parent:
`Container.TriggerMouseInput` calls `base.TriggerMouseInput` on ITSELF -
which fires that container's own `Click` / `RightMouseButtonPressed` /
`MouseMoved` handlers - before it walks its children, and the deepest
child only wins the RETURN value (`control2 ?? control`, the value that
becomes `ActiveControl`) and `break`s out of its SIBLINGS. So a
left-click or right-drag over the row's name text already reaches
`rowPanel`, which is also why the row's hover wash lights up over the
name today. Stamping `toggleHandler` onto `nameLabel` would fire the
toggle twice per click and leave the row visually unchanged.

### Deliberately out of scope

The confirm-dialog-across-tab-switch callback hazard the ABC review
flagged is untouched here. Two upstream Blish candidates remain noted
and unattempted: clamping `UpdateTooltipPosition` on all four edges, and
making `BasicTooltipView`'s max width relative to the screen.

### Desktop gate (live, required)

1. **Opaque value tooltip with coin icons:** generate a currency-bearing
   plan (a Mystic Clover / spirit-shard chain - anything whose committed
   CRAFT or VENDOR pill diverges) and hover that pill. "Crafting gold
   price", "Currencies" and "Optimization price" must each show real
   gold/silver/copper ICONS, each icon to the RIGHT of its number - not
   the letters "g"/"s"/"c". The opportunity-cost sentence must be
   wrapped across lines with nothing cut off. Nothing behind the tooltip
   may show through it: hover a pill over a busy part of the tree and
   confirm no row text, divider or icon is visible through the tooltip's
   middle.
2. **Tree row tooltip, coin icons and the name line:** hover a
   quantity>1 TP row - its "Unit price" line must show coin icons. Hover
   a row whose NAME is ellipsized: the tooltip must open with the full
   item name, wrapped if long, above the other lines. Hover directly over
   the row's NAME TEXT and over its quantity prefix, not just the empty
   strip beside them - the tooltip must appear over all three (before
   this branch it appeared over none of the text). With the cursor on the
   NAME text: left-click must expand/collapse the row exactly ONCE per
   click (not toggle-and-toggle-back), and right-click must open the wiki
   page - both handlers still live on the row Panel only.
   Then park the cursor on an ellipsized row's name and resize the module
   window until that name FITS: the open tooltip must drop its full-name
   line while the cursor sits still, and gain it again when the window is
   narrowed back.
3. **Stays inside the screen on all four edges:** drag the module window
   so a tree row sits near the BOTTOM of the screen and hover a pill
   with a tall tooltip - the tooltip must stay fully on screen (this is
   the edge Blish never clamps). Repeat near the RIGHT edge, near the
   LEFT edge with a wide tooltip, and near the TOP. In every case the
   whole box must be visible, and the tooltip must not flicker
   on and off as the cursor sits still (the hit-test opt-out).
4. **Pill tooltip composition:** hover a subdued (decisively-losing)
   pill that is still clickable - the tooltip must read "Switch to X", a
   blank line, then the "more expensive" explanation, with its gold
   margin drawn as coin icons. On a dimmed reference branch, the same
   pill must show its explanation plus the dead-click line, with no
   blank line at the very top.
5. **Plain tooltips still fine:** hover Clear Cache, Delete Log File,
   the Use Own Materials checkbox, Hide Unlocked Recipes, a settings
   currency row's input, a shopping list row, a truncated log row, and a
   Summary cost tile. Each must show its usual text, wrapped, with no
   line running past the window edge and nothing missing.
6. **No leak across renders:** regenerate a plan a dozen times, hovering
   pills and rows between regenerations, then keep using the module.
   There must be no growing stutter on hover and no second tooltip ever
   visible at once.

Gate: PASS after one gate-found Critical was fixed and re-gated
(2026-08-23 desktop sessions, captures preflight/gT1-gT6). The
FIRST gate run crashed Blish fatally on the second hover:
RichTooltipSurface.DisposeContent built a List directly from
Children, and ControlCollection.CopyTo throws by design - the empty
first build survived, the first content REPOINT died
(InvalidOperationException through Tooltip.HandleMouseMoved,
process-fatal). Fixed in b57be54 with the repo's established
Children.ToArray() idiom (ten existing sites; this file was the
sole deviation) and re-gated: four hover repoints across different
controls survived. Verified live on the fixed build: the rich
value-detail tooltip renders OPAQUE (zero bleed-through of the
table behind - the H6 translucency defect is dead), with coin
amounts as real icon runs (gold/silver/copper icons right of the
numbers, per the invariant) for Currencies and Optimization price,
the opportunity-cost sentence wrapped inside the box, and the box
inside the window; a 3-second held hover showed no flicker (the
mouse opt-out working). The bottom/right edge-clamp cases and the
wiki right-click-on-name behavior were not staged live; both are
pinned by TooltipLayoutMath tests and the review round's
label-handler verification respectively.

## Audit batch J: consistency sweep (audit-j-consistency)

The last audit batch, deliberately: every item here is a vocabulary or
chrome decision, and running it last let it adopt the words batches
A-K had already settled rather than inventing a tenth spelling that
would then have to be swept again. Each finding was re-located against
this HEAD before it was touched - the audit's own line numbers are
stale and three of its claims no longer held.

### Audit findings

- **M9, quantity notation - DONE.** The Snapshot tab spelled a quantity
  three ways: the recipe tree's prefix ("47x Mystic Clover"), the item
  row's suffix ("Mystic Clover x30") and the wallet row's colon
  ("Spirit Shards: 50"). All three are the tree's prefix form now.
  Two exemptions, both because the number is not counting the thing
  the label names: a tabular Amount column, whose header already
  labels its bare numbers, and the per-source breakdown line under an
  item, whose labels are LOCATIONS - "20x Bank" parses as twenty banks
  and "10x Character: Maximus Test" collides the multiplier with the
  label's own colon, so that line keeps "Bank 20   Character: Maximus
  Test 10" (fix round 1; the sweep had reached it). The wallet row
  keeps its thousands separator, since balances run to seven figures
  where an item count does not.
- **M10, status lines - DONE.** `StatusText.Stamp(verb, when)` is now
  the only place a "&lt;verb&gt; &lt;separator&gt; &lt;timestamp&gt;"
  line is composed; MainView's cache-cleared/updated/failed lines,
  SettingsTabContent's "Saved", CraftingPlanView's "Plan generated" and
  the restored-plan seed all call it. Separator is the em-dash (the
  majority spelling; the hyphen was already in use as the module's
  WITHIN-clause separator, so reusing it gave one line two identical
  separators at two grammatical levels), and `ForRefreshFailure`'s cause
  clause moved to a colon for the same reason.
  The Snapshot line's two-times-read-as-one confusion is fixed by
  `ForSnapshotAgeSuffix`: "(snapshot 29d old)" instead of a bare
  "(29d ago)" straight after an absolute timestamp. It is now the
  module's ONLY age wording: the older `ForSnapshotAge` was left
  standing with no caller and nine tests holding it up, one of them a
  Theory that only mirrored the two formatters against each other -
  the contract-mirror shape the repo invariant forbids. Deleted in fix
  round 1, with the bucket coverage retargeted onto
  `ForSnapshotAgeSuffix` as literal boundary assertions. Placement was
  NOT touched: which row a status lives in was settled by batches
  F/I/G.
- **M12, label vocabulary - DONE**, all four parts. Placeholders are one
  "Search {scope}..." shape (the Log tab's bare "Search..." names its
  scope; Settings' "Filter currencies..." was the lone "Filter"
  spelling). The Log toolbar is textbox-then-dropdown, matching the
  Snapshot search row it used to mirror; nothing else on the row moved,
  since the Follow checkbox's offset is the same sum. "Clear view" ->
  "Clear View", the only sentence-case button label in a module of Title
  Case ones.
  The per-currency "Clear" checkbox named an ACTION it does not perform
  - it is a persistent flag that suppresses the curated default
  estimate, not a button that empties the box beside it. It reads
  "Ignore" now, with the tag slot showing "ignored" and the tooltip and
  section info line reworded. NOT the longer "Ignore default": the
  cell's total extent is what decides whether the currency grid gets two
  columns at the 930px window minimum (2 * MinColumnWidth = 848 against
  an 864px settings panel), so the four extra pixels came out of the
  input-to-checkbox gap instead, leaving MinColumnWidth untouched - now
  pinned by a test asserting two columns at that minimum.
- **L1, plan empty state - DONE.** A dim centered "No plan yet. Search
  for an item above, then click Generate Plan." replaces the blank
  parchment. It is an ordinary child of the content panel, so the first
  real render sweeps it through `ResetContentPanelToEmpty` rather than a
  second disposal path that could drift; the gap above it is a spacer
  Panel because the content panel is a SingleTopToBottom FlowPanel that
  positions its own children. `ShowEmptyPlanState` resets the content
  panel first, which is load-bearing rather than defensive: it registers
  a relayout closure and `_relayoutActions` is cleared only there, so a
  no-plan tab visit would otherwise leave the previous visit's closures
  writing `Size` into controls that visit had already disposed. Also
  shown after a rolled-back plan render, which used to leave the tab
  blank.
  Two fix-round-1 corrections. It is shown only when the status board
  reports nothing in flight: a solver started before a tab switch is
  still running on the way back, and "No plan yet... click Generate
  Plan." beside a "Generating..." strip told the user to do the thing
  already happening. And its own relayout closure was dead - both
  `ReplayRelayout` call sites were gated on `_currentPlan != null`,
  which is exactly when the empty state does NOT exist, so a no-plan
  tab dragged narrower kept the label centered on the build-time width
  and overflowed the panel. The per-tick replay dropped that gate
  (`ReplayRelayout` already no-ops on an empty registry); the settle
  ticker keeps it, since every job in that pass is about rendered plan
  content.
- **L2, counts - DONE; L2, Used Materials header - DONE (the audit's
  "batch H already did this" was wrong).** Rule adopted: ALL-COUNTABLE.
  Every section whose body is a list of like rows names how many,
  because that count is what a COLLAPSED header owes the reader (three
  rows or ninety?), and because the alternative - dropping the five
  counts that already existed - throws information away to buy
  tidiness. The Recipe Tree gains "(N)". Total Cost keeps none under
  the same rule, deliberately: its body is a fixed formula band plus an
  optional currency table, not a list, so any number in its header
  would be counting one of its parts.
  The tree's N is every node at every depth - what Expand All reveals -
  not the rows currently on screen, which would change under the reader
  on every caret click. It rides the existing whole-tree pre-scan
  (`TreeCostColumnMath.ScanColumns` gained a `NodeCount`, five new
  tests) rather than a second walk; the scan is simply hoisted above the
  header call it now feeds, and reads nothing the header produces.
  Used Materials was verified at this HEAD and had NO header row - batch
  H pulled its Amount column in beside the names but never named it. It
  has an Item/Amount header now, on the shared c-table renderer.
- **L3, chrome drift - DONE**, with the inventory re-taken at this HEAD
  rather than trusted from the audit. Three header styles across six
  tables: banded/Font14/white/26px on Required Recipes, Required
  Disciplines, the Recipe Tree (banded by batch D) and the Total Cost
  currency table; unbanded/Font12/#999999/22px on the Shopping List; and
  nothing at all on Used Materials.
  **The band wins**, and `Views/Rendering/TableHeaderStyle` owns the
  tokens all three builders read. Three grounds: it is what four of the
  five existing headers already do, so unifying the other way would
  rewrite the majority to match the minority; it is the more recent
  deliberate decision (batch D chose it AFTER the lighter treatment
  existed, which is why the audit's own suggestion of the Shopping
  List's style is superseded); and every data row in this module already
  carries a 2px divider and usually an icon, so an unbanded grey header
  reads as a faint first data row - which is the complaint. The cost is
  stated rather than hidden: the Shopping List's header grows four
  pixels and Used Materials gains one, and both are paid for in
  `PlanContentHeightMath.SectionBodyHeight` in the same commit, counted
  unconditionally exactly as the two c-tables already were. The new
  Shopping band is bounded by its own last column, the rule batch H's
  fix round established for every other band, and resolves to exactly
  the panel width whenever the columns are still pinned.
  Buttons: 30 (Snapshot's two), 28 (the Log tab's three, Save, Generate
  Plan) and 24 (the five tree actions - re-checked, they did move to the
  strip in batch E but kept their 24 - and the item row's +/- pair)
  become one `UiMetrics.ButtonHeight = 28` applied at the construction
  sites. 28 wins on button count, and is the height of the one input row
  a button already shares - the plan's item row, whose search and
  quantity boxes are both 28 beside its +/- pair. It is NOT the module's
  input height, and fix round 1 corrected the constant's doc comment
  which claimed it was: TextBoxes are 26 at nine of eleven sites and the
  two Dropdowns outside the plan tab are 30, so the Log toolbar still
  runs three input heights and a button on it does not share a baseline
  with the search box beside it. Bringing the inputs to 28 is a separate,
  unmade decision, recorded here rather than implied by the constant.
  The Snapshot pair's y is derived from the header height rather than
  rewritten; the tree toolbar's y already derived from its row height.
  Scope, corrected in verification round 1: the constant covers the ten
  buttons that live on a TAB. The four dialog footer buttons
  (`ModalDialog`'s confirm/cancel, `ApiAccessDialog`'s retry/close) are
  still 25 and were missing from the inventory above, so the constant's
  summary line - "every StandardButton in the module" - was false as
  written. They are left at 25 deliberately: each is hand-placed against
  a fixed window size, so changing its height moves it relative to a
  window edge rather than to a row of neighbours, which is a separate
  unmade decision and not verifiable without the live gate.
- **L5, missing wallet icon - DONE**, root cause first. `IconUrl` is
  empty for that entry. Live it comes from
  `Gw2AccountSnapshotService.ResolveCurrencyDetailsAsync`, which
  resolves the v2/currencies icon for every wallet row, so the captured
  hole is the seeded fixture
  `docs/dev-notes/m38-plan/m37-item29-snapshot.json`, which carries
  `"IconUrl": ""` for currency 23. The fixture is left as-is (inventing
  an icon URL would be inventing data), because the state is reachable
  live anyway whenever that currencies fetch fails or a currency is
  absent from its cache - so the fix is the general no-icon case, not
  one row. `IconControls` already degraded a missing icon to a neutral
  square instead of Blish's magenta missing-texture; the square just
  read as a HOLE. It now carries a dim centered ASCII mark and, when the
  caller supplied no tooltip of its own, "No icon available for this
  entry." - stamped on the mark as well as the square, since Blish
  resolves a tooltip on the deepest control under the cursor. Marking
  rather than collapsing the column: an un-iconed row whose text starts
  32px left of every other row's is the worse artifact, and the plan's
  tables anchor their name column to a fixed x a per-row collapse would
  break. Built only on the missing path.
- **L7, About wording - DONE.** "unknown" (version), "Not set in
  manifest.json" (source URL) and "Not listed in manifest.json" (author)
  all become the single `NotAvailableText` the data-directory row
  already used, "Not available". Two of the three named an
  implementation detail the reader cannot act on.

### Photography findings

- **P1, ModalDialog did not block background input - DONE.**
  `Views/ModalBackdrop` is a bare capturing `Control` raised beneath the
  dialog for its lifetime. Measured against BlishHUD 1.3.0, not assumed:
  `Container.TriggerMouseInput` walks children by ZIndex descending then
  sibling index descending and BREAKS on the first whose bounds hold the
  cursor and whose own `TriggerMouseInput` returns non-null - which
  `Control.TriggerMouseInput` does for anything carrying the Mouse or
  MouseWheel capture flag. `CaptureType.Filter` is the one flag that
  loop steps past, so the backdrop must not carry it. That is the entire
  mechanism; it paints nothing.
  **It covers the module window, not the screen.** A capturing control
  also stops Guild Wars 2 itself from seeing the click, and a confirm
  left open swallowing every click in the game is not a trade a HUD
  overlay should make for a two-button dialog. Other modules' windows
  and the game stay live.
  Z-order is not a constant - a window's ZIndex is
  `5 + Screen.WINDOW_BASEZINDEX + its rank among windows ordered by
  (TopMost, LastInteraction)` - so the backdrop tracks
  `dialog.ZIndex - 1` on every frame it is visible, and is constructed
  on the FIRST `Show()` rather than in the constructor so that on the
  tie that arithmetic can produce with a non-TopMost module window it is
  the later SpriteScreen child and wins the sibling-index tiebreak.
  Module hands the blocked surface over as a lambda because the module
  window is built after the dialog. Dropped on every exit path the
  dialog has - both buttons, `Hide()`, and the title-bar X / Escape
  route through `Dismiss` - before the callbacks run. ApiAccessDialog is
  deliberately NOT given one: it is an error dialog with Retry/Close,
  not a confirm gating destructive state.
- **P2, Snapshot breakdown hard-clip - DONE.** Both lines of an item row
  (and the wallet row) run through `LabelHelpers.EllipsizeToWidth`, and
  a shortened line carries the full text through the tooltip facility's
  plain path - stamped on the Label itself as well as the row Panel,
  because Blish resolves a tooltip on the deepest control under the
  cursor and does not bubble. A width change re-fits the rows in place -
  each row Panel takes the new width and each line is re-ellipsized
  against it, tooltip re-decided - so a widened window stops showing
  "..." on text that now fits; a height-only drag arms nothing.
  Fix round 1 replaced the first attempt, which routed the resize
  through the EXISTING search debounce and claimed "a drag costs nothing
  per frame". It cost a CancellationTokenSource allocated, cancelled and
  disposed, plus a thrown-and-caught cancellation exception, on EVERY
  drag frame, on the UI thread's own event path - and its callback then
  disposed and recreated every row inside a scrolling FlowPanel,
  re-running the whole search and risking the scroll position, to change
  nothing but text. The trailing wait is now armed once per drag (later
  events only stamp the last-event time and the single pending waiter
  re-arms itself, the bounded shape the plan tab's settle ticker uses)
  and is gated on the width the rows were actually laid out at, so a
  drag ending where it started re-fits nothing. Build-time fit and
  resize re-fit share one rule, `FitRowTextLabel`.
- **P3, doubled log tag - DONE, at the root.** Two sinks with different
  shapes: `ModuleLogEntry` carries the tag as a FIELD, which
  `LogLineFormat` renders in the row's own prefix column, while Blish's
  `Logger` has no tag column and needs it inside the message. All
  fourteen call sites prepended the bracketed form to the message AND
  handed the same tag to ModuleLog. `LogScrollDiag` - the single method
  writing to both - now adds the bracketed form for Blish's Logger only.
  Class sweep over every `ModuleLog.Shared.Write` in the tree: no other
  site embeds its own tag in its message (the "[TypeName]" runs in
  Module.cs and RecipeClientFactory are exception type names, not tags),
  so this was the sole instance.
- **P4, ApiAccessDialog title/close-X collision - DONE.** Two changes,
  because either alone leaves no margin: the window is 560 wide (was
  480) and the title drops the word carrying none ("GW2 API access not
  ready"). Measured rather than guessed: `WindowBase2` draws the title
  in DefaultFont32 - the largest font in the toolkit, not the one a
  title this long was sized against - at a fixed 80px offset into the
  left title-bar texture, clipped to that texture's bounds, which stop
  2px short of the right section; the exit button sits 32px plus its own
  width inside that section's right edge. The title's budget therefore
  scales 1:1 with window width. Everything inside the dialog derives
  from `ContentWidth`, so the checklist simply wraps to fewer lines and
  the buttons re-center.
- **P5 - SKIPPED, already resolved by batch D.** Verified at this HEAD:
  `CreatePlanHeader` emits `" x {vm.TargetQuantity} needed"`, with a
  comment recording why it is "needed" rather than a bare count. No work
  done.

### Validation

Build 0 errors and the full suite green per commit. Suite 2168 baseline
-> 2192 after the batch, then -> 2186 after fix round 1 deleted the
caller-less age formatter's nine tests and retargeted its bucket
coverage onto `ForSnapshotAgeSuffix` (net +18 Blish-free tests over the
baseline: `StatusText`'s stamp and age suffix, 5 on the tree scan's node
count, 1 pinning two currency columns at the window minimum, and the
rest folded into the reworked `PlanContentHeightMath` header
assertions). No new test references Blish.

Height-math check: two renderer-emitted heights DO change in this batch,
and both are paid for in `PlanContentHeightMath.SectionBodyHeight` in
the same commit as the renderer - the Shopping List's header (22 -> the
shared 26) and Used Materials' new header (0 -> 26). Nothing else in the
batch moves a height: the empty-state label lives outside every
section's math, the button-height change is bounded by rows whose
heights are fixed constants, and the tree's node count is text.

### What the desktop gate should look at

1. **Modal really blocks:** open the Snapshot tab's Clear Cache confirm
   and click the Crafting Plan tab's "+" add-row button behind it, the
   tab strip, and the module window's own title bar. None may respond.
   Then click OUTSIDE the module window - the game and any other
   module's window must still respond. Cancel, and confirm the module
   window is live again. Repeat for the Log tab's Delete Log File and
   the plan's regenerate confirm, and dismiss one with Escape and one
   with the title-bar X - both must release the block.
2. **Snapshot breakdown ellipsizes:** find (or filter to) an item held
   by several characters so the breakdown line is long, at the 930px
   minimum width. The line must end in "..." rather than a clipped
   word, and hovering it - and the row's own name line, and the bare
   strip beside them - must show the full text. Then drag the window
   wider: about a fifth of a second after the drag settles the rows must
   re-fit and the "..." disappear on lines that now fit. Scroll the
   result list part-way down FIRST and confirm the drag does not move
   the scroll position, and that the drag itself stays smooth (the
   re-fit is in place now - no row is rebuilt and the search does not
   re-run).
3. **Log tags single:** turn diagnostics on, scroll the Crafting Plan
   tab, then read the Log tab at Debug+. Every scrolldiag line must show
   "[scrolldiag]" exactly once, in the dim prefix column. Copy a few
   lines and confirm the clipboard text matches.
4. **About wording:** the About tab's Source, Author, Version and Data
   directory rows must each read either a real value or "Not available"
   - no "unknown", no "Not set in manifest.json".
5. **Snapshot quantity notation:** item rows read "30x Mystic Clover"
   and wallet rows "50x Spirit Shards" - no suffix "x30" and no
   "Name: value" colon anywhere on the tab. The breakdown line beneath
   an item is the exemption and must read "Bank 20   Character: Maximus
   Test 10", counting the item at each location, NOT "20x Bank". The
   Spirit Shards row's icon slot must show the dim placeholder mark with
   its "No icon available" tooltip rather than an empty hole.
6. **Empty plan state:** open the Crafting Plan tab with no plan (a
   fresh profile, or after a plan fails to restore). The dim "No plan
   yet..." line must be centered in the content area, and must vanish
   the instant the first plan renders. Generate, then switch tabs away
   and back - the plan must still be there and the empty state must NOT
   reappear. Two more: with no plan, drag the window narrower and wider
   and confirm the line stays centered and never overflows the panel.
   Then click Generate Plan, switch to the Snapshot tab while it is
   still solving, and switch back - the content area must show the
   spinner's status only, never "No plan yet..." beside "Generating".
7. **Chrome, the two visible costs:** the Shopping List's header is now
   a dark band with white Font14 labels like every other table, and Used
   Materials has an Item/Amount header it did not have. Confirm both
   bands stop just past their own last column rather than running to the
   panel edge, at 930px and at 1400px+, and that the rows below them did
   not shift out of their section (nothing overlapping the next section
   header, no gap). Also confirm the Recipe Tree header reads "Recipe
   Tree (N)" and that N does not change when branches are expanded or
   collapsed.
8. **Button heights:** on the Snapshot header, the Log toolbar, the plan
   controls row and the Recipe Tree strip, every BUTTON must be the same
   height. The item row's "+"/"-" pair must line up with the quantity
   box, not sit short of it. Buttons are NOT expected to share a
   baseline with the textboxes and dropdowns beside them - those are
   still 26 and 30 outside the plan tab. Record how bad the Log
   toolbar's three-height run actually looks; that is the evidence for
   whether the inputs should follow to 28.
9. **Settings "Ignore":** the per-currency checkbox reads "Ignore",
   fits without touching the tag beside it, and ticking it still shows
   "ignored" in that tag and still suppresses the default on save. At
   the 930px window minimum the currency grid must still be TWO columns.
10. **API-access dialog title:** force the ApiAccessNotReady path (press
    Refresh Now at character select). The title must read "GW2 API
    access not ready" in full with clear space before the close X, and
    the checklist must wrap inside the wider window with the buttons
    centered.

### Fix round 1 (review findings)

Six Must Fix findings, all re-located against this HEAD first and all
fixed; the affected item bullets above are rewritten rather than
appended to, so they describe what the code does now.

1. The Snapshot tab's resize path armed the search debounce per drag
   frame (CTS churn plus a thrown cancellation exception per frame) and
   rebuilt every row. Now a bounded once-per-drag wait and an in-place
   re-fit. See P2 above.
2. M9's prefix sweep had reached the location breakdown, where "20x
   Bank" reads as twenty banks. Exempted, alongside tabular Amount
   columns. See M9 above.
3. `UiMetrics.ButtonHeight`'s doc claimed 28 was the module's TextBox
   and Dropdown height. It is not; the comment now records the real
   reason and names the input-height decision as unmade. See L3 above.
4. The plan's empty state contradicted an in-flight generation.
5. The plan's empty state registered a relayout closure that could
   never run. Both in L1 above.
6. `StatusText.ForSnapshotAge` had no production caller and nine tests,
   one of them a pure contract mirror. Deleted, coverage retargeted.
   See M10 above.

Nice to Have items from the same review are not addressed here and stay
open: the duplicated header-band rule
(`ShoppingListSectionRenderer.HeaderBandWidth` vs
`CTableHeaderRenderer.BandWidth`), `IconControls`' run-on comment block,
`ModalBackdrop`'s over-broad "other modules' windows stay live" claim,
`ApiAccessDialog` having no backdrop, the redundant
`ResetContentPanelToEmpty` on the rollback path, `SettingsTabContent`'s
stale "Clear checkbox" doc wording, the 2px input-to-checkbox gap, the
plan header's "x N needed" suffix versus M9's prefix rule, the
single-fetch currency cache behind L5's placeholder, and `UiMetrics`
living in `Views.Rendering`.

### Verification round 1

All six fix-round-1 findings re-read at HEAD and confirmed fixed: the
Snapshot resize path no longer touches the search debounce (one bounded
waiter per drag, in-place `RefitResultRows`, gated on
`_lastRowLayoutWidth`); the breakdown reads `{Label} {Count}`; the empty
state is `else if (!boardSnapshot.InFlight)` and its relayout closure is
reachable (the per-tick `ReplayRelayout` gate is `widthChanged` alone);
`StatusText.ForSnapshotAge` is gone from the tree, tests included.
Inventories re-taken independently and they hold: TextBoxes 26 at nine
of eleven sites, Dropdowns 30 outside the plan tab, the plan item row's
quantity box and +/- pair all 28 at y=3 in a 35px row.

One residual defect of the same class as finding 3, fixed here:
`UiMetrics.ButtonHeight`'s summary line still claimed "every
StandardButton in the module" while four dialog footer buttons are 25.
Comment scoped to tabs, the exclusion and its reason recorded, and the
L3 bullet above corrected. Doc-only - no control height changed.

Build 0 errors (2082 pre-existing StyleCop warnings), suite 2186 passed
/ 0 failed, tree clean, nothing pushed.

Gate: PASS (2026-08-23 desktop session, branch build at the
review-fix HEAD, captures preflight/gJ1-gJ6). (L1) With plan.json
moved aside, the Crafting Plan tab rendered the centered dim empty
state. (M9) Snapshot rows read "75x Green Wood Log", the wallet
"50x Spirit Shards" - prefix notation throughout. (P2) The
multi-character breakdown line ellipsized and its hover showed the
FULL breakdown wrapped in the opaque facility tooltip. (M10) The
status line read "... Aug 23, 2026 12:23 AM (snapshot 32d old)" -
the failure timestamp and the snapshot age no longer read as one
moment; the fix-round breakdown format kept "Character: Maximus
Test 10" label-first (no "20x Bank"). (L5) Spirit Shards rendered a
placeholder icon instead of a column hole. (P4) The widened
ApiAccessDialog title sits clear of its close button. (P1) With the
Clear Cache confirm open, a click on the Bank checkbox behind it
was eaten by the backdrop - Bank stayed checked, where the
2026-08-22 photography session proved such clicks used to land.
(P3) Zero doubled tags in module_log.jsonl and fresh entries write
single tags - fixed at the write site. Observed, tolerable, noted:
ModalDialog can stack on top of an open ApiAccessDialog (different
dialog classes; backdrop still gates the module beneath both). Not
staged live: L3's unified header bands and button heights across
every tab (spot-checked on the surfaces above; pinned by the
re-baselined height tests), M12's Log row-order swap (code-reviewed).

## Cost band restyle (cost-band-restyle)

Revises audit-D's promotion of the Total Cost section's result tile,
from the maintainer's live field test: "the currency table under total
cost in craftin plan needs to be centered. the size of the
gold/silver/copper text for total materials value and your materials
used is not the same as for Actual cost to craft - they should be the
same. if you want to visually highlight actual cost to craft, draw a box
around it and give it a colored tint and semi transparency so the
background texture still peeks through.. this will draw the eye to focus
there while keeping overall visual balance."

### What audit-D did, and why it is being undone

Audit-D promoted "Actual Cost to Craft" to `DefaultFont32` and paid for
the extra leading with `PlanContentHeightMath.PromotedCostTileRowHeight`
(76). The promotion worked - the eye did land on the figure - but it
broke the thing the band exists to say: three tiles that read as one
formula, `Total Materials Value - Your Materials Used = Actual Cost to
Craft`, cannot read as one formula when the right-hand side is drawn at
twice the size of the left. The field test is the first look at it in
the live game, and it says so directly.

### The three changes

**One amount font.** All three tiles render their coin runs at
`DefaultFont16` - the SMALL tiles' existing size, not one size up. Two
reasons for taking the smaller of the two options the directive allowed:
each tile's coin run is centred inside its own `(panelWidth - 40) / 3`
slice, and a three-denomination run (`123g 45s 67c`, six controls) is
already close to that slice at Font16, so growing every tile's font
would push all three toward overlapping their neighbours at ordinary
window widths rather than only the one that overflows today. And the
emphasis is now carried by the box, which is exactly what the directive
asked for ("No font-size-based emphasis") - a larger shared font would
be re-introducing a weaker version of the thing being removed.

**The highlight box.** The result tile's caption, its `+ N currencies
required` disclosure line and its coin run are wrapped in a box: a warm
gold tint (`214, 176, 96`) at alpha 0.14 for the fill and 0.5 for the
1px border, both scaled from one tint with the same premultiplied
`Color * f` idiom `FullCoverageFill` already uses. Blish composites a
Panel's `BackgroundColor` over what is behind it, so the window's
parchment texture reads through the fill - that is the "semi
transparency so the background texture still peeks through" the
directive asked for, and it is the reason the fill is not simply a solid
dark swatch.

Structurally the box is a real `Panel` and the result tile's controls
are its CHILDREN, not its siblings. That buys two things: the fill is
painted behind them by the container's own paint order, so nothing
depends on sibling z-order; and because the box's width is font-derived
(width-invariant), a resize repositions one control instead of
re-centring three runs. The box is never clamped to the tile width -
Blish clips a container's children, so a box narrower than its content
would CUT the amount off, where an unboxed run merely overlaps its
neighbour.

The box panel IS the fill, and the 1px frame is four edge panels drawn
ON it. The first draft instead copied `LabelHelpers.CreateSmallTag` - a
border-coloured OUTER panel with the fill inset inside it - which is
wrong here: that idiom under-paints the whole interior with the border
colour, and every existing caller only gets away with it because its
border is OPAQUE, so the under-paint is invisible by construction. This
is the first caller with a translucent border, and the under-paint made
the interior composite at `1 - 0.5 * 0.86 ~= 0.57` instead of the
documented `0.14`: a near-solid gold slab with no discernible ring,
i.e. the exact opposite of what the directive asked for. With nothing
beneath the fill, the interior is 0.14 (parchment reads through at 86%)
and an edge - frame over fill - lands at ~0.57, four times the
interior's density, which is what makes a 1px ring read as an edge. The
edges are siblings of the tile's labels but can never overlap them:
content is inset by `CostBandBoxPadX`/`PadY`, both larger than the
border width.

The box's geometry (`CostBandBoxTop`, `CostBandBoxHeight`,
`CostBandBoxWidth`) and the amount's bottom-anchoring clamp
(`BandAmountY`) live in `SummarySectionLayoutMath` beside the constants
they are built from, not inline in the Blish-bound renderer, so the
tests below call the production expressions rather than restating them.

**The centred currency table.** Batch H pulled the Required/Have/Needed
block in beside the currency names, which closed the dead gutter but
left the finished table pinned against the section's left edge with all
the recovered space dead to its right. `CurrencyTableOffsetX` now
centres it. Centring moves ONE control per row - a content panel holding
the whole row, the header band included - rather than shifting every
column's x, so the columns keep the panel-relative geometry
`SummarySectionLayoutMath` already computes for them, the header cannot
centre differently from the rows under it, and a table still spanning
the panel gets offset 0, i.e. byte-identically the old layout.

### Height math

`CostBandHeight` is re-derived from the new geometry instead of a
promoted font's leading: `6` box margin + `6` box pad + `20` caption
line + `4` gap + `20` coin run + `6` box pad + `6` box margin = **68**,
and **86** with the disclosure line (`+18`, unchanged). It was 76/94.
`PlanContentHeightMath.PromotedCostTileRowHeight`, whose only reader was
`CostBandHeight` (compiler-verified - `PlanContentHeightMath` is a
high-evidence zone, and this is a deletion of a constant nothing reads,
not a change to one that is read), is gone.

The 20px caption-line reserve is deliberately larger than the ~17 the
font measures: the renderer places the caption from real font metrics
and clamps the amount below it, so the reserve has to cover the tallest
plausible metric or the band clips its own amount. The DEBUG assert is
kept honest by asserting on the BOX's bottom edge for a highlighted band
(the box extends one pad below the amount, so it, not the amount, is the
band's lowest ink).

Not touched, by scope: `TreeCostColumnMath` and every other tree file
(concurrent branch), and `CoinCurrencyRenderer.SegmentLayoutHandle.
IconYOffset`, which is still used - `RichTooltipSurface` centres its own
coin icons with it - so only its doc comment's now-wrong example (the
promoted tile) was corrected.

Sweep for the same class of defect (two font sizes in one row of
comparable stats): none found. The remaining `DefaultFont32` use is the
plan's TITLE and `DefaultFont18` is a craft-step NUMBER - both are a
different kind of thing from the stat beside them, not the same kind at
a different size.

### Validation

Build 0 errors, suite 2192 passed / 0 failed (2186 baseline, +6: the two
re-baselined `CostBandHeight` literals rewritten, four new
`CurrencyTableOffsetX` tests (pinned/centred/narrow-panel/geometry-
preserved), one test driving `BandAmountY` + `CostBandBoxHeight` to pin
the box inside the reserved band across the whole plausible range of
measured caption heights, and one pinning `CostBandBoxWidth` against its
tile slice at the narrowest panel the module can present). Tree clean,
nothing pushed.

### What the desktop gate should look at

1. **Equal sizes.** Total Materials Value, Your Materials Used and
   Actual Cost to Craft draw their gold/silver/copper numbers at the
   SAME size, and all three sit on one baseline with the `-` and `=`
   operators between them.
2. **The box.** A tinted, semi-transparent box surrounds the Actual Cost
   tile's caption + disclosure line + amount, with breathing room on all
   four sides - and the parchment texture is visible THROUGH the fill,
   not painted over by it. The eye should land there first without the
   band looking lopsided.
3. **The centred table.** The Currency table under the band sits in the
   middle of the section, with roughly equal margin either side, and its
   header band tracks its columns (Required/Have/Needed still right-
   aligned over their own numbers, the OK marker still at the right end).
4. **Resize.** Drag the window narrower and wider: the box stays centred
   on its tile, the table stays centred, and at narrow widths the table
   degrades to the left-pinned layout rather than overrunning the panel.
5. **A plan with no currency costs.** The disclosure line is absent, the
   band is shorter by exactly one line, and the box still fits inside it
   with no clipping at the top or bottom.

Gate: PASS (2026-08-23 desktop session, branch build at the
review-fix HEAD, capture preflight/gWB1-cost-band.png, restored
Mystic Clover x77 plan). All three tiles' coin runs render at one
size; the Actual Cost to Craft tile sits in the gold-tinted
translucent box with the parchment texture visibly reading through
its interior and a discernible ring (the review's recomposited
edge-strip construction working as intended); the "+ 3 currencies
required" note renders inside the box; the Currency table is
centered in the section with its internal column alignment and
header band intact. Narrow-width box-vs-operator overlap and the
resize relayout of the boxed tile were not staged live; both are
pinned by the new SummarySectionLayoutMath box-geometry tests.

## Field-test fixes wave 1 (field-fixes-1)

The first feedback from outside the build loop: the maintainer ran v0.2.0
as a user, in game, and reported five defects in their own words. Every
one was reproduced from the code before it was touched, and three of them
are visible in the existing gate captures - which is the useful lesson of
this wave. The captures had them all along; nobody was looking for them,
because each gate was reading for the item it was staged to prove.

- **Bug 1, the confirm dialog did not fit its own sentence - DONE.**
  `ModalDialog`'s message Label was 380px wide, centered, and never
  wrapped, so Clear Cache's ~640px sentence was centered on the label's
  midpoint and clipped at BOTH ends: `preflight/gB2-confirm-dialog`
  shows "ched account snapshot? It can only be rebuilt when the GW2 A".
  The 400px window additionally squeezed WindowBase2's left title-bar
  texture into ~200px, which rasterizes as coloured streaks behind the
  title - the "title seems poorly aligned too" half of the report. Both
  are fixed by adopting `ApiAccessDialog`'s proven geometry and text
  handling: 560x170 and a wrap against the content width. Blish draws
  the title itself at a fixed 80px indent in DefaultFont32 with no
  alignment control, so window width is the only lever either dialog has
  over its title bar.
  Review fix (round 1): the button line is now FIXED and the message is
  capped to the lines that fit above it, rather than the message pushing
  the buttons down. The window cannot grow to absorb a longer sentence -
  `WindowBase2` derives its content region from the region handed to its
  protected `ConstructWindow`, and `Container.ContentRegion` has no
  public setter - so pushing the buttons walked them out of the content
  region: at four wrapped lines their bottom 2px fell outside it, at five
  the pair was effectively unclickable and the title-bar X was the only
  exit. All three current callers wrap to two lines, so this was latent,
  but the old `Math.Max` guard read as protection it could not provide.
  The wrap now goes through `TextWrapMath.Wrap` with a caller-supplied
  line cap (new overload; the four-argument one still uses
  `MaxWrappedLines`), which ellipsizes the tail into the last line and
  reports it, and the dialog puts the full text on the label's tooltip
  when anything was dropped.
- **Bug 2, the currency valuation box did not read as an input - DONE.**
  The mechanic was never broken - typed digits go through
  `SettingsInputParser.TryParseCopperValue` into `CurrencyValuation` and
  out to the settings file on Save, and come back into the box on the
  next open - but nothing in the cell said so. "copper" as the
  placeholder named the unit the box holds, which reads as a label on a
  read-only field, and the grey "default 3600" beside it states a fact
  without offering an action. The unit moved out of the box into a
  "Currency"/"Copper per unit" column header over the grid (stated once
  per column), the box now hints with the currency's own default value,
  and one info line names the interaction outright. The three-state tag,
  the Ignore checkbox and every cell X are unchanged.
  Review fix (round 1): the placeholder was set once at build and never
  revisited, so an ignored currency kept a greyed default in its box
  while the tag beside it read "ignored" - the box would have stated the
  opposite of the tag. `RefreshCurrencyRowDefaultState` now owns the
  placeholder alongside the tag and the checkbox: cleared while the
  currency is ignored, restored to the default digits when it is not, so
  all three always describe one state.
- **Bug 3, the typeahead list floated far right - DONE.** The offset was
  deliberate (M15): the list was anchored past the Qty stepper so it
  would not cover the row's own quantity field or the rows below it. In
  use it reads as a detached panel with no visible tie to the box being
  typed into, so it is back to the classic dropdown position - the text
  box's own left edge, immediately below it. Transient occlusion of the
  controls underneath is what a dropdown does; the list closes on pick,
  on focus loss and on any outside click. Screen clamp and the
  flip-above-the-box branch kept; `anchorOffsetX` and
  `SuggestionAnchorGap` removed.
- **Bug 4, tree cost values sat left of the Cost header - DONE.** The
  sub-column layout (batch D) meeting the pulled-in column edge (batch
  H): the cost column reserves a trailing band as wide as the widest
  currency run any row in the tree draws, and every row's copper
  sub-column ends one gap left of it, while the "Cost" header
  right-aligns on the far side of that band. So in any tree containing a
  vendor-currency cost, coin-only rows ended a whole band short of the
  header and currency rows landed under it - `preflight/gDE1-top` shows
  the two ~80px apart. `TreeCostColumnMath.ComputeRowEdges` now
  collapses the band for a row that does not fill it, so every row's
  rightmost segment (coin run, currency run, or the unpriceable dash)
  ends on the header's own edge; rows that do draw currency keep the
  shared band and stay aligned with each other. Reserved column width
  is unchanged, so no row reaches further right than the column already
  owned.
- **Bug 5, descenders clipped - DONE.** `AutoSizeHeight` sizes a Label
  to exactly its font's measured text height, and Blish clips a control
  to its own bounds - so a descender lands in the last row of the clip
  window, which the `Container.Paint` scissor round trip recorded under
  #23 can shave off by a logical pixel. Scroll-phase and UI-scale
  dependent, hence intermittent. Reported for character names in
  Required Disciplines; also visible in the wallet/item rows of
  `preflight/ph01-snapshot-multichar`, which clips the tail off
  "Green Wood Log".
  `LabelHelpers.WithDescenderClearance` pins a label to its measured
  height plus two pixels - the clearance the Log tab's row metrics have
  carried since they were written - and is applied to the class: the
  shared label factories (right-aligned, c-table header, icon+name row,
  snapshot row text) and the hand-rolled row labels in the tree,
  disciplines, recipes, craft steps, notes, summary and fallback-text
  renderers. Not applied to pills and small tags (fixed-height chrome,
  uppercase text) or the Log tab (already clear). Row heights unchanged.
  Review fix (round 1): the helper now also pins
  `VerticalAlignment.Top`, and that is what makes a partial sweep safe.
  `Blish_HUD.Controls.Label.VerticalAlignment` is a public settable
  property whose default this module does not control; if it were
  `Middle`, growing a box by two would push its glyphs down by one while
  an unswept sibling on the same row stayed put - "Craft " and "12x " on
  one baseline, the item name a pixel below, which is worse than the clip
  it fixes. `Top` puts both pixels below the glyphs, so a swept label
  renders at exactly the y it did before: additive clearance, never
  motion. The prefix labels that share a baseline with a swept one are
  swept too, so every label on one row now has the same box shape -
  the craft-step row's "Craft "/"12x ", the tree row's quantity prefix,
  the shopping list's own "Item" header (it builds its header by hand
  rather than through `CTableHeaderRenderer`, so it did not get the
  treatment its Amount/Each/Total siblings got for free) and the
  shopping-list and used-materials quantity cells. Rich tooltip text
  spans were swept as the same class - they carry item and character
  names too.

Build 0 errors, 2102 StyleCop warnings (2082 before this wave; the 20
added sit in the same rule families the codebase trips throughout -
trailing commas in multi-line initializers, comment spacing). Suite 2197
passed / 0 failed (2186 baseline, +11: six on the new per-row cost edges, one on
the currency column header, four on the caller-supplied wrap line cap; the
stale "copper" placeholder width test was rewritten against the real defaults
table), tree clean, nothing pushed.

Desktop gate items, one per bug:

1. Open Snapshot, press Clear Cache: the confirm reads its whole
   sentence with margin on both sides, the title bar draws clean, and
   Discard/Cancel sit on one line. Repeat for the Crafting Plan tab's
   regenerate confirm and the Log tab's Delete Log File - both still fit
   on one line. Confirm/Cancel sit at the SAME height in all three (the
   button line no longer moves with the message length). With the confirm
   up, a click on a checkbox behind it is still eaten by the backdrop, and
   Escape still cancels.
2. Settings > Currency Valuations: each grid column carries a
   "Currency"/"Copper per unit" header, a currency with a default shows
   that number greyed in its box, typing a number and pressing Save
   persists it (tag flips to "was N"), and clearing the box and saving
   restores "default N". Tick Ignore and Save: the tag reads "ignored"
   and the box is EMPTY (no greyed default contradicting it); untick and
   Save and the greyed default comes back. Narrow the window to one
   column and the header follows.
3. Crafting Plan: type in an item search box - the suggestion list opens
   directly under that box, left edges flush, and closes on pick and
   on a click outside it. Drag the window to the right screen edge
   and repeat: the list stays fully on screen.
4. Generate a plan with a vendor-currency cost in it (Mystic Clover
   does): in the Recipe Tree, gold-only rows, mixed coin+currency rows
   and any unpriceable dash all end on the same x as the "Cost" header's
   right edge. Drag-resize the window and they still do.
5. A plan whose Required Disciplines rows carry character names with
   descenders ('y', 'g', 'p'): the tails render whole, at more than one
   scroll position. Same check on the Snapshot tab's item rows ("Green
   Wood Log") and the plan's shopping list. Then read the baselines
   WITHIN a row, which is what the clearance could have broken: a craft
   step's "Craft 12x <name>" is one unbroken line of text, a tree row's
   "12x <name>" likewise, and the shopping list's "Item" header sits on
   the same line as its Amount/Each/Total headers.

Gate: PASS after one gate-found fix (2026-08-23 desktop sessions,
captures preflight/gA1w-gA7w). (1) Modal: the FIRST gate run showed
the second wrapped line clipped mid-glyph - AutoSizeHeight with a
fixed Width takes Blish's stale-layout-pass measure; fixed in
cf193ea by adopting ApiAccessDialog's auto-size-both-and-parent-last
shape, re-gated: both lines fully visible ("...when the GW2 API is /
reachable."), buttons anchored, title-bar chrome clean at 560px.
(2) Settings: the typed-override path was LIVE-PROVEN end to end
for the first time - typed 5 into Karma's box (placeholder shows
the row's default digits under the new Currency / Copper per unit
headers), Save produced the green dated label and the tag flipped
to "was 1"; override then reverted to keep the fixture canonical.
(3) Typeahead: eight results dropped directly under the search box,
left-aligned with it. (4) The root row's coin run ends under the
Cost header's right edge (previously ~80px short); currency rows
unchanged. (5) Zoomed crop confirms full descenders on "Log",
"Augur's" and "Mystic" where the ph01 capture shows clipping.
Blish's fixed 80px title indent (title cannot be centered without
reimplementing window chrome) is recorded as the accepted limit;
the Emblem option noted for a future maintainer call.

## Sortable plan tables (sortable-tables)

Maintainer field-test directive, verbatim: "the used materials section
should be column sortable by clicking the column headers and have visual
indicators - sort by item, sort by amount. shopping list too - column
sortable."

Both tables' column headers are now their own sort controls. Used
Materials sorts on Item/Amount, the Shopping List on Item/Amount/Each/
Total. One click cycle per column - **None -> Ascending -> Descending ->
None**: the third click restores the plan's own emission order rather
than stranding a reader in a sort they cannot undo, and clicking a
different column starts that column ascending and abandons the previous
one (a table has exactly one active sort column). The default order is
the plan's own, with no indicator drawn anywhere.

- **State and comparators are Blish-free.**
  `Services/TableSortState<TColumn>` holds one table's active column and
  direction plus the click cycle; `Services/PlanTableSorter` orders the
  already-built `PlanRowViewModel`s. Sorting never mutates the caller's
  list and hands the same instance back when no sort is active, so the
  default path allocates nothing. 26 tests.
- **Item sorts ordinal-ignore-case; Amount sorts numerically.** A string
  sort would put 111 before 9; the Amount test pins 9/111/136/816 in
  both directions. Ties keep their original relative order (stable) in
  both directions.
- **The Shopping List's Each/Total columns are not one scale, so they
  sort in three blocks.** A cell there is a coin price, a price paid in
  some non-coin currency (spirit shards, karma), or a genuinely
  unpriceable dash - and a copper amount is not comparable to a
  spirit-shard amount, since the module refuses to invent an exchange
  rate between them. The order is: coin rows (including mixed
  coin+currency rows, keyed on their copper part - the one magnitude
  every coin row shares), then currency-only rows (keyed by currency
  name, then amount within that currency, so every karma row lands
  beside every other karma row), then the unpriceable rows. The BLOCK
  order is deliberately direction-invariant and only the order WITHIN a
  block flips: reversing the blocks would express nothing - 5 spirit
  shards is neither more nor less than 3 gold - and it would float the
  dash rows to the top, where they are pure noise. A row carrying more
  than one currency keys on its ordinally-first currency name and that
  entry's amount, which is stable regardless of the order the resolver
  emitted them in; no attempt is made to add amounts across currencies.
  Within a currency the numeric key is the amount's exact per-unit rate
  where one exists (`CurrencyAmountViewModel.UnitRate`, set by
  `CurrencyDisplayResolver` beside every "Each" amount), NOT `Amount`: a
  rate that does not divide evenly deliberately leaves `Amount` at 0 and
  shows the rate as bundle text ("912 for 92" - the live Philosopher's
  Stone case), so keying on `Amount` would sort every bundle-priced row
  as if it were free and tie them all with each other. `UnitRate` is a
  sort key only; nothing renders it.
- **The indicator rides inside the clickable header label.** The label
  IS the click target, and its text carries the ASCII "^"/"v" (the
  tree's caret vocabulary - M12 unified the module on ASCII). That keeps
  `CTableHeaderRenderer`'s and the Shopping List header's relayout
  closures correct for free: both right-align off the label control's
  own `Width`, which already includes the indicator, so the x-tracking
  that follows a drag-resize never sees a separate control to miss.
  `CTableHeaderRenderer` gained two optional click actions, omitted by
  every other c-table caller (Required Recipes, Required Disciplines,
  the tree), whose labels stay inert exactly as before. Since an
  unsorted column deliberately shows no indicator, a hover tint and a
  one-line tooltip are what say "clickable" before the first click. That
  tooltip is load-bearing, not decoration: a Blish `Label` only captures
  the mouse while it carries one (this file's repeated finding that a
  label swallows its container's tooltip), so removing it would silently
  kill click delivery to every sort header - stated in
  `SortableHeaderLabel.MakeClickable` so a future edit cannot drop it
  unaware. Gate item 1 exercises a real click on a `Label`, which is the
  one assumption in this branch that is inferred rather than measured.
- **A click re-renders the plan.** Section rows are a `FlowPanel`'s
  children in flow order, which is not reorderable in place, so the
  sort is applied the one way it can be: `PreserveScrollAcross(() =>
  RenderPlan(_currentPlan))` - the same synchronous full rebuild the
  "Hide Unlocked Recipes" checkbox and a tree pill's re-solve already
  run from inside their own event handlers, rather than a second
  deferred mechanism. Row COUNT and row heights are identical before and
  after, so `PlanContentHeightMath` lands on exactly the same section
  height and the reader keeps their scroll position.
- **Sort state survives a re-render of the SAME plan, and only that.**
  ~~It lives on the view for the session (never persisted), unlike
  `_sectionExpansion`, which a new Generate deliberately resets to the
  section defaults.~~ **Superseded by the font-and-polish round** - the
  maintainer's field test asked for "reset to defaults when you gen a new
  plan", and the claim above was the behaviour being complained about. It
  now has exactly `_sectionExpansion`'s lifetime and resets in the same
  place: a re-sort, a tree pill override and a re-solve all keep it (they
  re-render the same plan and never reach TriggerGenerate's commit
  point), a new Generate clears both tables to `None`. Still never
  persisted. See the "Font bump and decision-round polish" section.

Build 0 errors, 2147 StyleCop warnings (2135 before; the 12 added sit in
the same rule families the codebase trips throughout). Suite 2229 passed
/ 0 failed (2203 baseline, +26: eight on the click cycle, eighteen on
the comparators; existing resolver tests gained assertions pinning the
per-unit rate), tree clean, nothing pushed.

Desktop gate items:

1. Generate a plan with a long Used Materials list. Click the "Item"
   header: the rows reorder A-Z and the header reads "Item ^". Click it
   again: the order reverses and the header reads "Item v". Click a
   third time: the plan's own order is back and no header carries an
   indicator. Hovering any of the two headers tints it before any click.
2. Click "Amount" on the same table: rows sort by quantity NUMERICALLY -
   the fixture's 111x/136x/816x rows land in that order ascending and
   816/136/111 descending, and a single-digit row (9x) sorts below 111x,
   never above it. The Amount column and its header stay aligned on the
   same right edge as before, and still do after a window drag-resize.
3. Shopping List: each of Item / Amount / Each / Total sorts on click and
   shows its indicator, and only one header carries an indicator at a
   time. On Each/Total, coin-priced rows come first ordered by value,
   then rows priced in a currency (grouped per currency), then any dash
   row - and the dash rows stay at the BOTTOM when the direction is
   flipped to descending.
4. With a sort active on both tables, press Generate again for the same
   item: both tables come back sorted the same way with the same
   indicators showing. Scroll down to the Shopping List, click a header
   there, and the view stays where it was rather than jumping to the top.

Gate: PASS (2026-08-23 desktop session, branch build at the
review-fix HEAD, captures preflight/gD1-gD3, restored x77 plan).
Clicking the Shopping List's Amount header sorted ascending with the
"^" indicator (111x Mystic Coin, 136x Obsidian Shard, 136x Glob of
Ectoplasm, 816x Philosopher's Stone - the 136x tie kept source
order, stability live); the second click flipped to "v" descending
with the tie STILL in source order both directions; scroll position
held across both rebuilds and the label-disposing click handler
(the repo's first Label doing the rebuild-from-own-event pattern)
survived repeatedly. Used Materials' headers carry the same wiring
(same factory path); the bundle-rate Each ordering ("1 for 10"
Philosopher's Stone keyed on its true 0.1/unit rate) is pinned by
the new comparator tests and visible implicitly in the ascending
capture (816x at 82 currency total sorts within its currency
block). Third-click reset-to-plan-order not captured (two clicks
shown); pinned by TableSortStateTests.

## Minimum width raise (min-width-1436)

Maintainer-directed, from the measured research now committed at
[`docs/research/minimum-window-width.md`](research/minimum-window-width.md):
the module's 930px minimum ellipsized recipe-tree rows from roughly depth
6 down, and the deepest chain in the game is far deeper than that. The
research derived the width from the module's own seeds and the installed
bitmap fonts rather than from an estimate - exact graph depths via SCC
condensation over `ref/recipes_seed.json`, string widths parsed out of
`menomonia-{12,14,16}-regular.xnb` with MonoGame.Extended's own advance
rule, and the truncation threshold verified by replaying
`PlanRelayoutMath.ComputeTreeColumnEdges`.

- **The defining item is not a legendary.** `+24 Agony Infusion` (49447)
  is a forced 23-level chain - one recipe per level, `2x +{N-1} Agony
  Infusion` + `1x Thermocatalytic Reagent` - whose deepest row renders
  `4194304x Thermocatalytic Reagent` at depth 23, 849px of `nameX` + qty
  prefix + name. Legendaries are wide, not deep: the deepest
  (Transcendence, Conflux) reach depth 14 and would only have needed a
  ~1170px window. The chain terminates because Thermocatalytic Reagent is
  vendor-only.
- **`WindowSizing.MinWindowWidth` = 1436, height unchanged at 710.** The
  research prescribed no height and nothing in the layout derives a height
  from the window width - every renderer takes `panelWidth` only - so the
  vertical budget is exactly what it was. The window/content region
  rectangles keep the texture-authored 930x710 pair: they are
  texture-space regions and Blish grows the content region by the same
  delta as the window, so the 46px of horizontal chrome they encode holds
  at every size.
- **The minimum is enforced on three paths, one of them new.**
  `ResizableTabbedWindow` already clamped a drag (`HandleWindowResize`) and
  every layout pass (`RecalculateLayout`, which is what catches a size
  persisted by an earlier session - `SavesSize = true`). Added: the same
  clamp in the constructor. Until now the constructed size EQUALLED the
  minimum, so the layout clamp had never actually had to fire for it;
  with the minimum above the texture-derived constructed size, the window
  would otherwise have existed at 930px for the frames between
  construction and its first layout pass, and a view built in that window
  would have been laid out against an 804px panel and then resized out of
  it. Clamping in the constructor removes the transient rather than
  relying on the resize registry to repair it. The clamp only ever grows a
  window: a user whose saved window is 1100px wide opens at 1436 and keeps
  their saved height, and a saved 1900px window is untouched.
  `ResizableTabbedWindow` is `sealed` for this reason: the constructor
  clamp writes `Size`, i.e. it dispatches through the virtual
  OnResized/RecalculateLayout chain, which a subclass override would
  receive against a half-constructed instance.
- **The enforced minimum is fitted to the game client
  (`WindowSizing.EffectiveMinWindowWidth`).** Blish's
  `GameService.Graphics.SpriteScreen` is the GW2 CLIENT area, not the
  monitor, so 1280x720 and 1366x768 are ordinary windowed sizes on any
  display. Enforcing 1436 there would put the window's right edge - the
  tree cost column, the right-anchored Generate button, and critically the
  bottom-right resize grip - off-screen, and with `HandleWindowResize`
  refusing anything under 1436 the user could not shrink it back: a
  regression against a configuration that worked before this change. So
  the enforced minimum is `max(930, min(1436, client width))` - a client
  narrower than 1436 gets a window exactly its own width and deep tree
  rows ellipsize there as they did before the raise, which is the honest
  trade. 930 is the floor because the module was never usable below it.
  Pinned by `WindowSizingTests`.
  - *Residual*: the effective minimum is computed once, when the window is
    built. A player who changes the client's resolution mid-session keeps
    the minimum their launch resolution earned, so going from fullscreen
    to a 1280-wide window in the same session still leaves a 1436px
    window. Recovering needs a Blish restart. Gate item 6.
- **The centered launch position is now clamped at 0.** Only reachable on
  a client narrower than even the 930 fallback, where a negative centered
  x would put the title bar (and its close button) off the left edge with
  nothing left to drag.
- **`PlanRelayoutMath.TreePillColumnWidth` 240 -> 256.** Measured:
  the standard CRAFT/TP/VENDOR/IGNORE run is 222px against the 236px
  budget a 240px column leaves, so any slightly wider label pushed the row
  through the tightened-padding pass for no reason. 256 gives it 252px.
  This is the only lever that changes the pill budget - `maxRightEdge -
  pillColX` is `TreePillColumnWidth - 4` at every window width, because
  the pill and cost columns move as one block - so the "+N" overflow pill
  on a `HAVE n/m NEEDED` row (a measured 436px run) is unchanged by the
  width raise and stays a known limit.

**The two changes are sized against each other.** The 16px the pill column
takes comes out of every row's name column, so at the 1436 minimum
(1310px plan panel, after 126px of window-to-panel chrome):

| row | name budget | measured need | slack |
|---|---|---|---|
| depth 23, deepest real row | 198px | 174px | 24px = the designed gutter |
| depth 24, one vendor-leaf level below it | 174px | 174px | 0 - exact fit |
| depth 23 at the old 930px minimum | 10px | 174px | clamped to the floor |

The depth-24 line is why the number is 1436 rather than 1428:
`CraftingTreeBuilder.BuildVendorCostComponentLeaves` can synthesise a leaf
one `TreeIndentPer` below the recipe graph, and the minimum covers that
unconditionally. All three rows are pinned by
`PlanRelayoutMathTests.ComputeTreeColumnEdges_DeepestRow*`, against the
measured 65px quantity prefix and 174px name.

Those three cases are the PINNED layout (`widestNameEnd` 0), which is the
worst case but not the layout the renderer produces: `TreeSectionController`
always passes the scanned `_widestNameEnd`, which pulls the pill/cost block
LEFT to `widestNameEnd + TableGutterBreathingRoom` and leaves the depth-23
row 190px rather than 198px.
`..._DeepestRowInTheScannedLayout_FitsAtTheWindowMinimum` covers that
configuration, so retuning the breathing room cannot silently ellipsize
the shipped row.

**Both sides of every one of these numbers now come from one place.** The
minimum, the 126px window-to-panel chrome and the pill column were
literals in `Module.cs`/`TreeSectionController.cs` with copies in the
tests, so a later maintainer taking the +2pt option (1472) or trimming the
pill column back would have left every "at the window minimum" test
passing while the shipped window truncated. `Services/WindowSizing.cs`
(Blish-free) holds the window sizing and
`PlanRelayoutMath.TreePillColumnWidth` the column; production and tests
both read them.

### Correction: the settings panel was never 864px wide

`SettingsCurrencyGridLayoutTests.SettingsPanelWidthAtWindowMinimum` was
`864`, commented as "884px content region - 20px right padding". That
skips the `ViewAdapter` chain every tab's content sits inside, so it
overstated the panel by 60px. The real chain, all of it read from this
repo's own source except the border term:

```
window minimum
 - 46  window region 930 - content region 884        (Module.cs)
 - 32  ViewAdapter OUTER_PADDING x2                  (ViewAdapter.cs)
 -  8  Blish Panel border chrome, ~4 a side          (ViewAdapter.cs)
 - 20  ViewAdapter INNER_PADDING x2                  (ViewAdapter.cs)
 - 20  RightEdgePadding, clear of the scrollbar      (SettingsTabContent.cs)
= window - 126
```

So the settings panel was **804px** at the old 930px minimum, not 864 -
and since two columns need `2 * MinColumnWidth` = 848px, the currency grid
was falling back to ONE column there. The test asserted two columns and
passed for the wrong reason; the prose elsewhere in this file that says
the grid "falls back to one column at the 930px minimum" (audit batch J
and the M4 layout notes) was right, and the constant was wrong. Both
statements are now reconciled: the constant is derived through the full
chain, and a second test states the actual two-column threshold - a 848px
panel, i.e. a **974px window**, which the old minimum missed by 44px.

The same class of assumption - reading 884 as a tab's usable panel width -
also sits in `SnapshotHeaderLayoutTests`, where 884 is the width the
roster-wrap audit's cell counts were taken at. Those assertions are about
the wrap decision at whatever width they are handed, not about the
minimum, so the number was annotated rather than changed; moving it would
have discarded the audit's own measurement.

At the new minimum the settings panel is **1310px**, so the Settings tab
is now always two-column and the one-column path is only reachable by a
tab whose panel is under 848px - none, at this minimum. The cell-extent
budget stops being load-bearing: `CellInputToClearGap` was squeezed from 6
to 2 to protect a boundary that is now ~460px away.

### Not implemented, deliberately

- **The +2pt font variant (1472) is a pending maintainer decision.** The
  research measured it at Menomonia 16/14 rather than scaling it, so the
  number is real, but nothing here changes a font.
  `WindowSizing.MinWindowWidth` is a single constant specifically so that
  bump is a one-line change.
- **Fitting the whole pill run** (`HAVE 4194304/8388608 NEEDED` and
  friends) needs a ~440px pill column and a ~1612px window, not a wider
  minimum. Out of scope; the "+N" pill and its tooltip continue to state
  the fact.
- **`Views/SettingsTabContent.cs` still carries a stale comment** citing
  "the 884px content region the 930px window minimum leaves" as the reason
  the Ignore checkbox stayed short. The file is concurrently owned by the
  `settings-dirty-prompt` branch, so it was left alone rather than made a
  merge hazard for a comment; the reasoning it records is superseded by
  this section.
- **Residual gutter risk**: the tree's cost column is measured, not fixed
  (`max(150, scanned)`), and the 165px used above is a six-digit gold
  total. An eight-digit total would add ~21px and spend the depth-23
  gutter. The depth-24 row would ellipsize by that much in that case; a
  plan that expensive is not reachable from any single item.
- **Clients narrower than 1436** do not get the raise at all: the enforced
  minimum drops to the client's own width (floor 930) rather than pushing
  the window's right edge, and the resize grip with it, off-screen. Deep
  rows ellipsize there exactly as they did before this change - the
  truncation is not removed on such a client, it is chosen over an
  unreachable window.

Validation: build 0 errors and the suite green before each commit; 2203
baseline -> 2218 (+3 tree-width edges pinned, +1 the same edges in the
scanned layout the renderer produces, +10 `WindowSizing` cases, +1
settings two-column threshold; zero regressions, one existing settings
test corrected in place). Tree clean, nothing pushed.

Desktop gate items:

1. The window opens at least 1436px wide on a fresh profile AND with an
   existing narrower saved size - delete nothing, just launch: a session
   that last closed the window at ~930px must come back at 1436, keeping
   its saved height and position. This is the item that proves the
   constructed-then-restored ordering, which is INFERRED, not measured:
   the constructor clamps to 1436 and Blish then restores the saved size,
   so if a restore wrote the size without invalidating, the window would
   open below the floor. Watch the first frame specifically - a window
   that flashes narrow and settles wide is a pass with a note; one that
   stays narrow is a FAIL. Then try to drag the left/right edge inward: it
   must refuse to go below 1436 and must not judder or snap back visibly
   while dragging.
2. Deep-tree readability, the actual point of the change. Generate
   `+24 Agony Infusion` LIVE (it is the real defining item, not a
   fixture): scroll to the bottom of the Recipe Tree and confirm the
   deepest row reads `4194304x Thermocatalytic Reagent` in full, with no
   ellipsis, and that its cost column and pills still sit on the same x as
   every other row's. A `+20 Agony Infusion` plan is the cheaper check if
   the full chain is unwieldy - it must be untruncated with room to spare.
3. Decision pills: on an ordinary plan, a row showing CRAFT / TP / VENDOR
   / IGNORE draws all four at normal padding (they should look no tighter
   than before this change), and a row with the `HAVE n/m NEEDED`
   annotation still ends in the "+N" pill with its tooltip - the wider
   column does not fix that case and must not pretend to.
4. Settings tab at the minimum width: the currency grid is TWO columns,
   every cell's "default N" tag is whole, and the section-header rules
   span the panel. Drag wider and back to the minimum - it stays two-up
   the whole way (the one-column fallback is now unreachable).
5. Every other tab at the minimum width: Snapshot, Crafting Plan, Log,
   Plan History, About. Nothing centered is off-center, nothing
   right-anchored has drifted off the panel, and no section has developed
   a dead horizontal band where a fixed-width block used to fill the row.
6. The narrow-client fallback, launched fresh with the GAME windowed at
   roughly 1280 wide (not the monitor - the client). The module window
   must open no wider than the client, its bottom-right resize grip must
   be visible and grabbable, the Generate button must be on-screen, and
   dragging the window narrower must still work down to the client width.
   Deep tree rows are expected to ellipsize here; that is the trade, not a
   defect. Then restart Blish at full screen and confirm the 1436 minimum
   is back.

Gate: PASS after two gate-found fixes (2026-08-23 desktop sessions,
captures preflight/gMW1-gMW12). The gate found the directive's core
path broken TWICE: (1) the effective floor was computed once at
module build against a sprite screen that had not settled - a
persisted ~990px window opened unclamped (gMW1); fixed by deriving
the floor at every clamp (c3f1b3a). (2) Even then, Blish adopts the
client size AFTER modules load and no window layout pass runs on
its own post-attach, so a launch on a wide client kept the smaller
floor (persisted-size ground truth read from settings.json between
sessions); fixed by re-clamping on SpriteScreen.Resized (570af3e).
Verified on the fixed build: the window opens clamped to the
screen-fitted floor every launch (990 -> 1161 on a ~1170px dummy;
the full 1436 layout live on the widened dummy - ten source
checkboxes on one row, header buttons at the far right). Programmatic
clamps do not re-persist the size (Blish saves on drag-end only) -
cosmetic, since the clamp lifts the window at every launch. THE
DEFINING ITEM LIVE: generated +24 Agony Infusion; the optimal plan
buys +22s off the TP (40,000g), and Craft All forced the full
23-level chain - "Decisions updated (23 override(s))", the reagent
cascade readable at every depth, the Shopping List carrying the
research-predicted 8388608x quantities and six-digit gold totals
(124,990g) inside the widened cost columns with no truncation and
no dropped pills. First-ever live captures of the Required
Disciplines and Required Recipes sections landed as a bonus
(gMW11). Settings two-column at the minimum implicitly verified by
the earlier gA1w-era sessions at narrower widths already showing
two columns.

## Settings dirty prompt (settings-dirty-prompt)

Maintainer field-test directive, verbatim: "if settings have been edited
before tabbing away.. it should prompt the user to ask if they want to
save the changes they have made or discard before proceeding." The
Settings tab has one Save button covering four sections, and nothing
told a user who typed a currency override and then clicked another tab
that the override was about to evaporate - the tab is rebuilt from
persisted settings on every re-entry, so the edit was silently gone.

**Dirty detection.** `Models/SettingsFormState` is a flat key/value bag
of the tab's save-gated control values - one amount and one Ignore flag
per currency row (47 of each), one tier per Homestead row, the two
logging fields and the snapshot interval. Dirty is "this capture differs
from the baseline taken at the last load or successful save", so typing
a value and reverting it reads as clean, as does typing whitespace and
deleting it (every value is trimmed on capture, matching what every
`SettingsInputParser` entry point does before parsing). A key present in
only one of the two captures counts as a change in both directions, and
duplicate keys are rejected outright rather than overwritten - two
controls sharing a key would collapse into one comparison and silently
stop reporting edits to whichever lost. The type has no Blish reference,
so the whole comparison is covered by real tests; only the thin
`CaptureFormState` reader in `SettingsTabContent` touches controls.

The Diagnostics checkbox is deliberately NOT part of the dirty model.
Its `CheckedChanged` handler writes straight through to `ModuleSettings`
and to the live `ModuleLog`, so it is never an unsaved change; listing
it would raise a save prompt for a value already on disk.

**Hook mechanics, measured from the vendored Blish HUD 1.3.0 binary**
(`packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe`, decompiled with
`ilspycmd`). There is no cancellable before-change hook, and the reason
is structural rather than an omission:

- `TabbedWindow2.SelectedTab`'s setter is a plain (non-virtual) property.
  It calls `SetProperty(ref _selectedTab, value, invalidateLayout: true)`
  - which assigns the backing field - and only then calls
  `OnTabChanged(new ValueChangedEventArgs<Tab>(previous, value))`.
  By the time anything else runs, the tab has already changed.
- `OnTabChanged` is `protected virtual`, but its FIRST act is
  `ShowView(e.NewValue?.View())`, and it raises the public `TabChanged`
  event LAST. So the outgoing view is already torn down and the incoming
  one already requested before any module handler is reached. There is
  no `TabChanging`, no `Cancel` flag on the event args, and no return
  value the handler can use.
- Two earlier interception points were measured and rejected.
  (a) Overriding `OnTabChanged` in `ResizableTabbedWindow` and deferring
  the `base` call until the user answers: `_selectedTab` is private and
  has already moved, so the sidebar would highlight the new tab while the
  old view is still on screen for the dialog's lifetime - a visibly
  inconsistent state, and the deferred `base` call re-enters `ShowView`
  from a dialog callback. (b) Overriding `OnClick` and swallowing the
  click before `SelectedTab = HoveredTab` runs: `HoveredTab` is private,
  so this means re-deriving the hit test from `SidebarActiveBounds`
  (protected), `RelativeMousePosition` (public) and `Tabs.FromIndex`
  (public) against TAB_VERTICALOFFSET=40 / TAB_HEIGHT=50, which are
  private consts. That is a hand-copy of Blish's private layout geometry
  whose drift mode is a missed or spurious prompt, and the click it reads
  can differ from the cached `HoveredTab` the base would have used if the
  mouse moved between the last `UpdateTabStates` and the click.

So the prompt is raised AFTER the switch, from the public `TabChanged`
event, keyed on `e.PreviousValue == _settingsTab`. This is safe because
of a second measurement: `WindowBase2.ShowView` -> `ClearView` ->
`Container.ClearChildren`, and `ClearChildren` only unparents
(`while (_children.Count > 0) _children[0].Parent = null;`) - it does not
dispose. The Settings TextBoxes and Checkboxes are therefore still alive
and still holding the user's typed text when the handler runs, so both
Save (persists exactly what was on screen) and the dirty comparison
itself are reading real values, not a torn-down form.

**Window close is deliberately NOT hooked.** `TabChanged` never fires
when the window is closed, and hanging the same prompt off
`_mainWindow.Hidden` was tried and rejected on two measurements against
the vendored binary:

- `Hidden` is not "the user closed the window". Every `WindowBase2`
  subscribes in its constructor to
  `Gw2Mumble.PlayerCharacter.IsInCombatChanged ->
  UpdateWindowBaseDynamicHUDCombatState` and to
  `Gw2Instance.IsInGameChanged -> UpdateWindowBaseDynamicHUDLoadingState`,
  and both statics call `wb.Hide()`. A user running Blish's overlay
  options "hide windows in combat" (`DynamicHUDWindows = ShowPeaceful`)
  or "hide during loading screens" (`DynamicHUDLoading = NeverShow`)
  would get a modal save prompt over gameplay for pulling a mob or
  zoning, with the auto-restored module window behind it. Both options
  default to `AlwaysShow`, so it was configuration-dependent - which
  makes it worse to diagnose, not better.
- Closing is not destructive in the first place. Hiding the window does
  not call `ShowView`/`ClearView`, so the Settings TextBoxes keep the
  user's typed text and still have it when the window is reopened.
  Prompting there put an Escape/X keypress (which routes to cancel, i.e.
  Discard) between the user and edits that were never at risk.

So closing the window is left exactly as it behaved before this branch:
edits stay in the controls, and the prompt appears only when the user
actually leaves the tab.

**Off-thread build.** `WindowBase2.ShowView` runs the view's build as
`view.DoLoad(progress).ContinueWith(BuildView)` with no scheduler, so
`SettingsTabContent.Build` executes on a ThreadPool thread while
`UnsavedChangeCount` is called from the main thread's `TabChanged`
handler. `Build` clears and refills `_rows` (47 entries) and
`_homesteadRows`, which `CaptureFormState` enumerates - a tab switch
landing mid-build would throw "Collection was modified" out of Blish's
own input dispatch, where the module has no try/catch. A `volatile bool
_buildComplete`, cleared as the first statement of `Build` and set as the
last of `LoadAll`, makes `UnsavedChangeCount` return 0 for that window.
The cost is a missed prompt for a switch within a frame or two of the tab
building, which is the same benign outcome the null baseline already had.

Clearing it in `Build` is not early enough on its own. `OnTabChanged`
commits to the rebuild on the MAIN thread - it evaluates the tab's view
factory and calls `ShowView`, which queues `BuildView` off an
already-completed `DoLoad` - and only afterwards raises `TabChanged`. In
the ThreadPool scheduling gap between those two, `Build` has not run its
first statement yet and the flag still reads true from the PREVIOUS
build, so a second tab click landing there would enumerate the row lists
the queued `Build` is about to clear. `SettingsTabContent.BeginRebuild`
therefore clears the flag from the Settings tab's view factory, which is
evaluated on the main thread inside `OnTabChanged` before either the
queued build or the event. Only the tab-entry path calls it, so it
cannot suppress the leave-the-tab prompt (leaving Settings evaluates the
INCOMING tab's factory, not this one).

**Prompt shape.** The existing `ModalDialog` has exactly two buttons, and
no third was added. Confirm = Save, cancel = Discard, and `cancelText` is
now an optional `Show` parameter (defaulting to the "Cancel" every
existing caller already got) so the second button says "Discard" rather
than "Cancel" - a button labelled Cancel would promise to put the user
back on the Settings tab, which is exactly what Blish gives no way to do.
Both button widths floor at their historical values (100 confirm, 70
cancel) and grow to fit a longer label rather than being clipped by
StandardButton's centered, unpadded text region - every existing dialog
is pixel-identical, and the second prompt's "Open Settings" fits. Discard restores the last loaded/saved values into the
controls and clears the save bar's status line.

**Saving from the prompt reports its own failures.** The tab's save bar
is unparented the moment the view is torn down, so a `SaveAll` driven
from the prompt has nowhere on screen to say "3 entries were rejected" -
and `SaveAll` rebases its baseline on the controls, so nothing
re-prompts either. The user asked to save and half of it silently would
not. `SaveAll` therefore returns a `SaveOutcome` (invalid-entry count
plus a failed-write flag; the in-tab Save button still ignores it and
uses the status label), and the prompt raises a second dialog when the
outcome is not `AllSaved`, offering "Open Settings" / "Dismiss". Note
that reopening rebuilds the form from persisted settings, so the message
says "re-enter" rather than promising the rejected text back.

**Re-raising the shared dialog needed a fix in `ModalDialog` itself.**
The second prompt is raised from inside the first one's confirm callback,
and `Dismiss` used to hide the window BEFORE running that callback. That
does not work, for reasons measured in the vendored binary:
`WindowBase2.Hide()` does not set `Visible = false` - it resumes the
0.2s reflecting fade tween built in the constructor, and only that
tween's `OnComplete` sets `Visible = false` and raises `Hidden`. Meanwhile
`WindowBase2.Show()` begins `BringWindowToFront(); if (Visible) return;`.
So the re-raised dialog's `Show()` early-returned into a window that was
already fading out: it painted the second message for ~0.2s, faded to
nothing, and its own `Hidden` event ran `Dismiss(confirmed: false)` -
a flash, then the same silent partial save the second dialog exists to
prevent. `Dismiss` now runs the callback first and skips its own
`Hide()` when that callback re-armed the dialog (`_isShowing` true
again), inside a `try`/`finally` so a throwing callback still closes it.
The early return then does the right thing: the still-visible window
carries the replacement's content. Nothing else changes for the four
single-shot callers - their callbacks do not re-arm, so the window is
hidden the moment the callback returns, one main-thread statement later
than before and with no frame drawn in between.

**Accepted limits.**

- The switch itself cannot be vetoed, so the tab has already changed when
  the prompt appears. On the tab path this is benign - returning to
  Settings rebuilds the form from persisted settings either way - but the
  prompt cannot offer "stay here".
- The dialog's title-bar X and the Escape key both route to cancel, which
  here means Discard. Benign now that the prompt is raised only on the
  tab path: returning to Settings rebuilds the form from persisted
  settings either way, so Discard is what leaving the tab already meant.
- A tab switch that lands while the Settings tab is still building on
  Blish's worker thread does not prompt (see "Off-thread build").
- A module unload with dirty settings (Blish shutting down, module
  disabled) tears the window down without prompting; `Unload` has no
  user-interaction budget.
- If another module dialog is already on screen `ModalDialog.Show`
  returns false and the prompt is skipped for that leave. Not reachable
  in practice - `ModalBackdrop` blocks the module window while any dialog
  is up, so the tab click that would trigger it cannot land.

Desktop gate:

1. Settings tab, edit one currency amount (type a number into an empty
   box), then click another tab. The prompt appears, headed "Confirm",
   reading "You have 1 unsaved change on the Settings tab. Save now, or
   discard and keep the last saved values?", with Save and Discard
   buttons - both fully labelled, neither clipped.
2. Choose Save on that prompt, return to the Settings tab: the typed
   value is in the box and its tag reads "was N". Restore the fixture
   afterwards (clear the box, Save).
3. Repeat the edit, click another tab, choose Discard, return to the
   Settings tab: the box is back to its pre-edit value and the tag reads
   "default N" again.
4. Clean tab switch: open the Settings tab, touch nothing, click another
   tab. No prompt. Then scroll the tab, use the currency search box, and
   switch away - still no prompt (neither is a save-gated field).
5. Revert-to-original: type over a value, then retype the original text
   exactly (or blank a box and retype what was in it), and switch away.
   No prompt.
6. Multi-section count: edit a Homestead tier AND the snapshot interval
   AND one currency Ignore checkbox, then switch away. The prompt reads
   "3 unsaved changes" (plural).
7. Window close: edit a field on the Settings tab and click the window's
   title-bar X. NO prompt appears. Reopen the window (corner icon) - the
   Settings tab is still selected and the typed text is still in the box,
   untouched.
8. Diagnostics checkbox alone: toggle it, switch away. No prompt (it
   applies immediately). Toggle it back afterwards to restore the
   fixture.
9. Invalid entry, saved in the tab: type "abc" into a currency box, press
   Save (status reads "Saved - 1 invalid entry not saved", the row tag
   reads "Invalid"), then switch away. No prompt - the user has already
   been told, and re-prompting would loop on a value that can never be
   saved.
10. Invalid entry, saved from the prompt: type "abc" into a currency box
    AND a valid number into a second one, switch away, choose Save. A
    second dialog appears reading "1 Settings entry could not be saved -
    the value was not a valid number. Everything else was saved. Open the
    Settings tab to re-enter it?" with Open Settings / Dismiss. It must
    STAY on screen until a button is pressed - watch it for a few seconds
    and confirm it neither fades out nor closes itself. Choose
    Open Settings: the tab is selected, the valid edit is persisted, and
    the "abc" box is back to its persisted value. Restore the fixture
    (clear the second box, Save).
11. Dismiss on that second dialog closes it and leaves the module window
    interactive (the backdrop is gone), with the tab the user switched to
    still selected.
12. Rapid switching: click Settings and immediately another tab, back and
    forth several times without editing anything. No crash, no prompt, and
    the Settings tab still renders correctly when it settles.
13. The other three confirm dialogs still behave (the Dismiss reordering
    is shared chrome): Clear Cache on the main tab, Delete log file on the
    Log tab, and the own-materials Regenerate confirm - each closes on
    Confirm and on Cancel, and Escape/X still cancels, with the module
    window interactive again afterwards.

Gate: PASS (2026-08-23 desktop session, branch build at the
review-round-2 HEAD, captures preflight/gF2a-gF2d). Typed 7 into
Karma's box and tabbed to Snapshot: the prompt appeared with the
exact dirty count ("You have 1 unsaved change on the Settings tab.
Save now, or discard and keep the last saved values?") and
Save/Discard verbs, body fully wrapped. Discard returned the tab to
the persisted state (Karma back to the greyed default-1 placeholder,
"default 1" tag). A clean tab-away raised no prompt
(dialog-region luma probe). Not staged live: the Save-from-prompt
path (mechanically the same SaveAll the Settings button runs,
live-proven in the field-fixes-1 gate), the window-close prompt, and
the invalid-entry rejection message; all pinned by
SettingsFormStateTests plus the review rounds' binary-verified
teardown ordering.

## Spinner and button feedback (spinner-feedback)

Two maintainer field-test directives, verbatim:

> "the loading spinner we made using ascii text is kinda lame -- Blish
> has its own spinner that i see used underneath the module icons in the
> top of the screen overlay that sits under them when they first load..
> it looks like a circular painterly spinner. we should try to find and
> use that.. its way cooler."

> "when buttons are clicked.. there is no feedback onclick visually or
> audio wise. it would be good to help the user know the button click
> was registered by depressing the button or shading it on mousedown and
> then unshading on release .. a sound would be a nice touch too."

Everything below marked "measured" was read out of the vendored Blish
HUD 1.3.0 binary (`packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe`,
decompiled with `ilspycmd`) and out of Blish's asset archive
(`C:\Blish.HUD\ref.dat`, a plain zip).

**The spinner, measured.** `Blish_HUD.Controls.LoadingSpinner` is a
public `Control` with a parameterless constructor whose entire body is
`Size = new Point(64, 64)`. Its `Paint` does one thing: hand its own
bounds to `Blish_HUD.LoadingSpinnerUtil.DrawLoadingSpinner`, which draws

    spriteBatch.DrawOnCtrl(control, _loadingSpinnerTexture, bounds,
        new Rectangle((int)(GameService.Overlay.CurrentGameTime
            .TotalGameTime.TotalSeconds * 21.3333) % 64 * 64, 0, 64, 64));

so:

- The texture is `GameService.Content.GetTexture("spinner-atlas")`.
  `spinner-atlas.png` in ref.dat measures 4096x64 - 64 frames of 64x64,
  laid out horizontally.
- The frame index is derived from GLOBAL game time at 21.33 frames/sec
  (a 3.0s loop), not from per-control state. The animation therefore
  costs us no ticker, cannot be paused or restarted, and starts at
  whatever phase the clock is at when the control becomes visible.
- The source rect is a fixed 64x64 but the destination is `bounds`, so
  the control renders correctly at ANY size. Nothing needed cropping;
  only the 64x64 default had to change, since neither status row here is
  anywhere near 64px tall.
- It can simply be constructed and parented - no service registration,
  no atlas handling, no disposal of ours (the texture is a static field
  on LoadingSpinnerUtil, shared by every instance).

**Where ours went.** The rotating ASCII glyph was
`CraftingPlanView.SpinnerFrames` (`'|' '/' '-' '\'`), appended to the
plan strip's status text by `RenderFromBoard` and advanced once per
150ms by `SpinnerTick`. It is gone; `SpinnerTickInterval` and
`_lastSpinnerTickUtc` stay, now rate-limiting only the strip's text
re-render (writing an `AutoSizeWidth` Label's `Text` re-triggers a text
measure, so the ticker must not do it 60x/sec for a whole generation).
The Snapshot tab had no spinner at all - "Refreshing..." sat there
static - so it gained one for the life of a Refresh Now.

**Row fit, measured.** The plan strip's status row is exactly
`TopRegionLayoutMath.StatusToSeparatorGap` = 21 logical px (status label
Y to separator Y); the spinner is 18. The Snapshot tab's status row is
its own 24px `_statusPanel`; the spinner is 20. Both sizes and the
label-trailing placement arithmetic live in the Blish-free
`Services/InlineSpinnerLayout`, with tests pinning both fits and the
"tracks an AutoSizeWidth label's right edge" property.

The spinner trails the status text rather than leading it, for the same
reason the ASCII glyph did: the phase text then always lays out from the
label's fixed x=0 origin and only the spinner moves.

**Button feedback, measured.** The maintainer's report is accurate, and
it is not only about our custom controls - Blish's own `StandardButton`
is worse than it looks:

- Hover works. `OnMouseEntered`/`OnMouseLeft` tween `AnimationState`
  between 0 and 8 over 0.25s, stepping through the
  "common/button-states" atlas.
- Press does nothing. There is no `OnLeftMouseButtonPressed` override
  and no pressed frame in that atlas walk. A held button is pixel-
  identical to a hovered one.
- The click sound is dead code. `OnClick` calls
  `Control.Content.PlaySoundEffectByName("audio\\button-click")`, but
  `ContentService.Load` builds its audio reader as
  `zipArchiveReader.GetSubPath("audio")` and `PlaySoundEffectByName`
  looks up `Path.Combine(_subPath, soundName + ".wav")`. The name
  therefore resolves to `audio/audio/button-click.wav`, which is not in
  ref.dat (`audio/button-click.wav` is), the `FileExists` guard rejects
  it, and the method returns without playing or logging. Blish's
  `Checkbox`, `GlowButton` and `CornerIcon` pass the unprefixed
  `"button-click"` and DO play - which is why checkboxes on the Settings
  tab click and buttons next to them do not.

So the gap is press shading on everything, plus sound on every
StandardButton. `Views/Rendering/PressFeedback` is the single helper:
on `LeftMouseButtonPressed` it captures the control's own `Opacity`,
multiplies it by 0.8, and plays `"button-click"` (unprefixed, at
`GameService.GameIntegration.Audio.Volume`, which `PlaySoundEffectByName`
applies itself); on `LeftMouseButtonReleased` AND on `MouseLeft` it
restores the captured value.

Two decisions worth keeping:

- It writes `Opacity`, not `BackgroundColor` or `TextColor`. Every
  click target here already owns a different hover vocabulary on a
  different property (a decision pill swaps BackgroundColor to white, a
  sortable header swaps TextColor, a tree row and a section header each
  swap to a different translucent wash). A helper writing those same
  properties would have to restore a resting value the site's own
  MouseLeft handler is also writing, making correctness depend on which
  handler was subscribed first. Nothing else touches Opacity on any of
  these controls, so this composes with all four schemes, and measured
  `Control.AbsoluteOpacity()` walks the parent chain into every
  `DrawOnCtrl`/`DrawStringOnCtrl` call - which is what makes a 20% dim
  legible on a target whose own background is transparent, since its
  labels and icons dim with it.
- `MouseLeft` restores as well as release, because Blish routes mouse
  events to the control under the cursor: a press dragged off the target
  is delivered `MouseLeft` and never a release.

The restore runs before the click fires - measured,
`Control.OnLeftMouseButtonReleased` raises `LeftMouseButtonReleased`
first and only then calls `OnClick` - so a button that disables itself
in its own handler (Generate) cannot be left stuck dim.

**Coverage.** All 14 of the module's buttons, via `FeedbackButton`, a
`StandardButton` subclass that wires itself in its constructor;
decision pills in both interactive arms (never the dead-click ones);
expandable tree rows (leaf rows are not clickable and get nothing);
plan section headers; sortable table headers; suggestion rows. Blish's
checkboxes, dropdowns, text boxes and tab strip already have working
sound and were left alone.

If a later Blish release fixes the double-prefixed path in
`StandardButton.OnClick`, `FeedbackButton` will play the sound twice on
a completed click, and the `PlayClick()` call in `PressFeedback.Wire` is
what to drop.

**Nested click targets (review round 1).** Measured:
`Container.TriggerMouseInput` calls `base.TriggerMouseInput` - which
raises the container's OWN mouse events - BEFORE it walks its children,
and the deepest child only wins the RETURN value (ActiveControl) and the
suppression of its siblings. So a press inside a wired child reaches the
wired parent as well. Exactly one such nesting exists in the module: a
decision pill inside an expandable tree row, which would otherwise have
played two click sounds and dimmed two controls for one press. The row's
click handler already had a "bail if a pill is hovered" guard for the
same reason; `PressFeedback.Wire` takes an optional suppression
predicate and the row now passes that same guard, extracted to
`TreeSectionController.AnyPillHovered` so the two cannot drift. No other
wired control is a descendant of another - the labels inside a tree row
and inside a section header do receive the events, but they carry
tooltips, not press wiring.

### Desktop gate

1. Plan tab, press Generate Plan on a real multi-item request: a
   circular painterly spinner turns to the right of the status text for
   the whole run, at roughly one revolution every 3 seconds, and
   disappears the instant the final "Plan generated..." text lands. The
   phase text itself must not jitter horizontally as it changes.
2. Switch to another tab mid-generation and back: the spinner is still
   turning next to the live phase text (the strip re-arms from the
   status board on rebuild).
3. A generation whose status carries a standing notice (leave one item
   row with unresolvable typed text, then Generate): confirm the longer
   composed status text has not pushed the spinner off the right edge of
   the window at the minimum window width. If it has, that is a real
   finding - the spinner is anchored to the label's right edge with no
   clamp.
4. Snapshot tab, Refresh Now: a spinner turns beside "Refreshing..."
   and stops when the timestamp lands. Switching tabs mid-refresh and
   back leaves it still turning.
5. Press-and-hold a decision pill in the Recipe Tree: the pill visibly
   dims while held and returns to its hover white on release. Press it,
   drag off it without releasing, and confirm it returns to its resting
   border color rather than staying dim or staying white.
6. Press-and-hold a sortable column header (e.g. Shopping List's Item):
   the header text visibly dims while held, returns to the hover tint on
   release, and the sort still applies once.
7. Press-and-hold a plan section header and an expandable tree row: the
   whole row dims, including its labels and icons, and restores on
   release.
8. Press-and-hold Generate Plan, then any Settings tab button: the
   button dims while held and restores on release. Disabled buttons
   (Generate during a run) must NOT dim or click.
9. Press a decision pill on a row that also expands: ONLY the pill
   dims, not the whole row behind it, and the press produces one click
   sound rather than two.
10. Sound is NOT verifiable in the muted dummy session used for these
   captures - the screenshot harness runs Blish with no audio device, in
   which case `PlaySoundEffectByName` returns at its first guard. The
   click sound needs a live audio check by the maintainer; what IS
   verified here is only that the asset exists (ref.dat contains
   `audio/button-click.wav`, 22,616 bytes uncompressed) and that the
   name passed is the unprefixed one Blish's own working callers use.

Gate: PASS (2026-08-24 desktop sessions, captures
preflight/gC1-gC14). (1) SPINNER: "Building recipe tree..." rendered
with Blish's golden circular spinner inline in the plan strip, and
two captures ~350ms apart show DIFFERENT rotation frames - the
atlas animation live. (2) PRESS FEEDBACK, measured numerically on
the Expand All FeedbackButton: hover luma 165.6 -> held 143.3
(~13% press-dim while the mouse is down) -> released 165.6, an
exact restore. A pill press was not stageable (the session's plan
was all-owned - its only pill is the HAVE chrome, correctly
non-interactive); pill wiring shares the same PressFeedback helper
verified on the button. (3) Sound not verifiable in the muted dummy
session (PlaySoundEffectByName no-ops without an audio device) -
the corrected unprefixed asset name is measured fact; the
maintainer's first real click is the audio gate. BONUS gate-found
fix folded into this branch: the persisted +24 Agony plan exceeded
Newtonsoft's default read MaxDepth of 64 and silently failed to
restore - raised to 512 with a mutation-checked 30-level real
pipeline round-trip test, and the deep plan then RESTORED live
("Recipe Tree (47)", 147,639g total) in this gate's own session.

## Snapshot item grid (snapshot-grid)

Maintainer field test: "wondering if we should consider multiple column
display for snapshot window. a single column list seems wasteful of
screen realestate at the moment". It was: at the 1436px window minimum a
Snapshot result row spanned the whole 1330px content panel to show a name
line that needs roughly 420px of it, and the row below it started 52px
further down whatever was left over.

The result list is now a grid, built the way the Settings tab's currency
grid was (batch G): a Blish-free layout service
(`Services/SnapshotItemGridLayout.cs`) that owns the arithmetic and is
covered by real tests, and a view (`Views/MainView.cs`) that only copies
the placements onto controls.

### The minimum column width is 464px, and where it comes from

Derived from the cell content, not chosen:

```
  40  text column left edge - the 32px icon at x=2 plus its gap
 416  a 52-character name line: the count prefix ("9,999x ", 7 chars)
      plus a 45-character item name, at 8px/char - the repo's existing
      upper bound on DefaultFont14, which averages ~7.7px
   8  right pad, clear of the cell edge
= 464
```

Rounding 7.7 up to 8 is what pays for the breathing room; there is no
separate fudge term.

**The breakdown line is deliberately outside that budget.** A row's second
line is its source breakdown ("Character: <name> 250   Bank 250
Material Storage 2000"), whose width is unbounded in the roster's name
lengths and in how many sources hold the item. Sizing a column to it would
price the second column out of every window a player actually uses. It
already ellipsizes with the full text on the row tooltip at every width
(batch J's P2), so per column it simply ellipsizes earlier - which is the
trade this change makes, stated plainly.

### The column count is derived, not written down

`gridWidth / MinColumnWidth`, floored at one. Not capped at two either:
the count comes from the width the player gave the window, and every
column is at least MinColumnWidth across at any count.

The widths it lands on, through `WindowSizing`'s own chain rather than
literals (the tests assert against the constants, not copies):

| window | content panel | grid (less 20px scrollbar) | columns | column |
|---|---|---|---|---|
| 1436 (the minimum) | 1330 | 1310 | 2 | 655 |
| ~1518 | 1412 | 1392 | 3 | 464 |
| under ~1054 | under 948 | under 928 | 1 | the whole grid |

The single-column case is byte-for-byte the list the tab shipped with, so
the narrow path is unchanged rather than newly special-cased. It is only
reachable on a game client too narrow to enforce the 1436 minimum (see
`WindowSizing.EffectiveMinWindowWidth`) - on any client at or above the
minimum the tab is always at least two-up.

### ONE grid panel, not one per run

Item rows are 52px tall and wallet rows 36px, and the two runs share the
list with wallet after items. Three ways to do that; the third is what
shipped:

1. One uniform cell height - rejected, it stretches every wallet row to
   the taller of the two for no reason.
2. Two sibling grid panels in the scrolling FlowPanel, items then wallet -
   rejected, because it makes the wallet run's position a bet on Blish's
   FlowPanel re-flowing a LATER sibling when an earlier one changes
   height. The Settings tab's grid never tested that: its grid panel is
   the last child of its panel, so a missed re-flow there would be
   invisible.
3. One grid panel holding both runs, the wallet run laid out at the item
   run's own height (`Compute`'s `offsetY`). The order is this module's
   own arithmetic, and the FlowPanel has exactly one child whose height
   ever moves.

Reading order is left-to-right then top-to-bottom throughout, so a
two-column list reads the way the one-column list did.

### The refit machinery is grid-aware now

`RefitResultRows` (the trailing, width-only, settle-debounced repack from
the batch J fix round) no longer just re-ellipsizes rows at a new panel
width. It recomputes the grid - a widened window can gain a column, a
narrowed one drop back to the fallback - moves every cell to its new slot,
and re-ellipsizes each line against its COLUMN width, re-deciding the
per-line and row-strip tooltips through the same `FitRowTextLabel` /
`ApplyRowStripTooltip` rules as before. Still no search re-run and no
dispose-and-recreate.

**What that does and does not buy for the scroll position.** A repack that
keeps the column count leaves the scroll alone: the grid panel's WIDTH
moves, its height does not. A repack that CHANGES the column count is a
different story - the panel's height moves with it (2 -> 3 columns drops it
by about a third), and Blish's Scrollbar zeroes the scroll position a frame
after any content-height change, measured and written up under "The grid
panel holds its unfiltered height" in the Settings grid section above. So a
drag across a column boundary snaps the list to the top. That is not
defended against: the Snapshot tab has no scroll-restore machinery (the
module's only one is `CraftingPlanView.PreserveScrollAcross`, which needs a
reflection handle on Blish's private scrollbar field plus a frame-ticker
verify), and a column-count change re-flows every row anyway, so there is
no old position left to hold. The Settings grid's own answer - pin the
panel to a height the filter cannot move - does not port: there the height
is a function of a FIXED 47-cell list, here it is a function of the column
count itself.

`LayoutResultGrid` is the single writer for the grid's geometry, shared by
the rebuild and the repack; the rebuild passes `refitText: false` because
its cells were just built at that same column width, and re-ellipsizing
every label twice would double the MeasureString work of a rebuild that
already runs once per pause in typing over a list that can reach into the
thousands of rows.

### Two incidental corrections

- The grid is laid out inside the content panel's width less a 20px
  scrollbar allowance (`LogTabContent`'s own precedent), so the rightmost
  column ellipsizes before it runs under the scrollbar rather than behind
  it. The single-column list used to run under it by 12px.
- `RowTextX` / `RowTextRightPad` now come from the layout service, the way
  `SettingsTabContent` shares its cell constants, so the minimum column
  width cannot drift from the geometry the cells are actually built with.

### Unchanged, deliberately

The empty-state line ("No snapshot available...") and the no-match lines
("No items match \"x\" in the selected sources.", "No currencies match
...") are still parented to the content panel and span it - they are
messages about the whole list, not cells in it. The search box, the
content-type dropdown, the source-filter checkboxes and their master
toggle, and the coin row above the list are all untouched.

### Desktop gate (live, required)

1. Snapshot tab at the window minimum (1436): the result list renders
   **two columns**, and reading it left-to-right then down matches the
   order the single-column list had - the first four items are 1, 2 on
   the top row and 3, 4 on the second, NOT 1, 3 / 2, 4. With the list
   long enough to show the scrollbar, confirm the RIGHTMOST column's text
   stops clear of it. That is the one live check on the chain the unit
   tests cannot make: they can pin the arithmetic, but only the running
   tab can confirm MainView's content panel really is
   `TabPanelWidthFor(window) + 20` wide (i.e. that this tab still adds no
   right-edge padding of its own).
2. Type into the search box and watch the list repack: the grid refills
   from the top left with no gaps and the last row is the only partial
   one. Toggle a source checkbox and a content-type dropdown value for
   the same check. Scroll position is deliberately NOT part of this step
   - a search rebuilds the row set, which moves the content height, and
   Blish resets the scroll to top on that. Pre-existing behaviour of this
   tab, unchanged by the grid.
3. An item whose breakdown is too long for one column (search for a
   material held by several characters plus bank and material storage):
   the second line ends in an ellipsis and hovering the row shows the
   FULL breakdown text in a tooltip. Confirm the same for a long item
   name on the first line.
4. Drag the window narrower than ~1054px (only possible on a client that
   cannot enforce the 1436 minimum): the grid falls back to one column,
   every row full width, nothing clipped at the right edge, and the
   tooltips still carry whatever no longer fits.
5. Drag the window wider, past ~1518px: a third column appears and the
   rows repack into it without a rebuild - no row is left stranded at an
   old column position and no cell overlaps its neighbour. Two separate
   scroll checks here, per "What that does and does not buy for the
   scroll position" above: a drag that stays inside one column band
   leaves the scroll where it was; the drag that adds the third column
   snaps it to the top. Both are the expected result.
6. With the content-type dropdown on "All" and a search that matches both
   (e.g. a term hitting an item and a currency): the wallet rows still
   render **below** the last item row, never interleaved with it, and the
   wallet run starts its own new grid row rather than filling the gap
   beside a half-empty item row.
7. Empty states: clear the cache (no snapshot) and separately search for
   nonsense text - both messages span the panel at the top left, not
   inside a column-width cell.

Gate: PASS (2026-08-24 desktop session, branch build at the
review-fix HEAD, captures preflight/gSG1-gSG3). At the 1436 window
minimum the result list rendered as TWO ~655px columns in
left-to-right reading order (Augur's Stone | Green Wood Log /
Mystic Clover | Mystic Coin / ...), the wallet run starting its own
row below the items with its shorter cells, and the whole
7-item + 4-wallet fixture occupying roughly half its former
vertical space. Green Wood Log's six-holder breakdown ellipsized at
the COLUMN edge with the row tooltip carrying the full text
(gSG2). Filtering "essence" repacked to three wallet cells in
reading order with no scroll jump at the unchanged column count
(gSG3). Not staged live: the 3-column layout (needs a >1518px
window - the dummy maxes near 1490 effective), the one-column
fallback (unreachable at the enforced minimum), and the documented
scroll-reset-on-column-count-change (accepted, recorded with its
measured Blish cause rather than defended against).

## Item stat tooltips (item-stat-tooltips)

Hovering an item anywhere in a plan now shows what the game's own item
tooltip shows: attributes, defense or weapon strength, upgrade bonuses,
rarity, binding, vendor value, flavour text.

### The headline measurement: this costs zero API requests

`Gw2ItemApiClient.GetItemsAsync` already called `/v2/items?ids=...` and
already received `description`, `type`, `level`, `vendor_value`,
`restrictions` and the entire `details{}` block in that response. It
parsed `id/name/icon/rarity/flags` and threw the rest away. The whole
feature is therefore a larger walk of a `JObject` that was already in
memory - no new endpoint, no new request, no extra round trip on the
plan path, and nothing at all on the hover path.

The one thing that WOULD need a request (`/v2/itemstats?ids=all`, 191
entries, 74 KB) is not made, because the only feature that needs it is
behind an open judgment call - see Q4 below.

### Why stats are not on ItemMetadata

`PersistedPlan.Result` is a `CraftingPlanResult`, which holds the
`ItemMetadata` dictionary, and `PersistedPlanSchemaMemberSetTests`
reflectively guards that whole reachable graph against
`PersistedPlan.CurrentSchemaVersion`. Adding stat fields to
`ItemMetadata` would have bumped the schema 3 -> 4, and
`PlanStoreHelpers.DeserializePersistedPlan` answers a version mismatch
by rejecting the file outright - i.e. it would have silently discarded
every existing user's saved plan, and bloated every future plan file
with stat blocks for 25-50 items.

`ItemStatBlock` is therefore a session-scoped side table on
`ItemMetadataService`, unreachable from `PersistedPlan`. It is filled by
the same `/v2/items` response that fills the metadata cache, and read
back by `GetCachedStatBlock`, which NEVER fetches: its caller is a hover
on the UI thread, and a network round trip inside a hover window is not
something the tooltip facility can cancel.

It is also the only one of that service's dictionaries read from a
thread other than the one writing it (UI render vs. background plan
generation), which is why it alone is locked.

### The attribute formula is the API's own, not a reconstruction

`attribute value = round(multiplier * details.attribute_adjustment)`,
where the multipliers come from the item's `/v2/itemstats` entry.

For every FIXED-stat item the endpoint publishes both the inputs and the
answer, so this is checkable rather than inferred. Measured against
Berserker's (itemstats 161, .35/.25/.25):

| Item | attribute_adjustment | API's own modifiers | formula |
|---|---|---|---|
| Zojja's Warfists / Pauldrons | 134.442 | 47 / 34 / 34 | 47 / 34 / 34 |
| Zojja's Visor | 179.256 | 63 / 45 / 45 | 63 / 45 / 45 |
| Zojja's Tassets | 268.884 | 94 / 67 / 67 | 94 / 67 / 67 |
| Zojja's Breastplate / Doublet | 403.326 | 141 / 101 / 101 | 141 / 101 / 101 |

`ItemStatMathTests` asserts against the published modifiers, so it
proves agreement with the API rather than with its own arithmetic.

The legacy `value` field on a `/v2/itemstats` attribute is NOT part of
this and is not read: entry 161 reports `value: 0` on all three
attributes while a different entry of the same name reports non-zero
ones. Multiplier only.

### What the tooltip facility gained

`RichTooltipSurface.RenderRow` hardcoded `Color.White`, so a
rarity-coloured item name, a green rune bonus and muted flavour text
were all unrepresentable. A `TooltipSpan` now carries a semantic ROLE
(`Default` / `Rarity` / `Bonus` / `Muted`) - and, for a name, the GW2
rarity STRING - rather than a colour, because `Services/` must stay
XNA-free for composer tests to stay Blish-free. Only the surface maps a
role to a colour.

Found while doing it: `TooltipLayoutMath`'s wrapper re-created every
wrapped piece with `TooltipSpan.FromText`, which would have reset each
wrapped line to `Default` - a long item name would have lost its colour
the moment it wrapped. It now clones the source span via `WithText`.

`ToPlainText()` output is byte-for-byte unchanged. That is the
regression gate, and every pre-existing tooltip test passes untouched.

### What the API cannot tell us, and is therefore never claimed

- What is actually socketed in the player's copy of an item. Infusion
  slots report a COUNT ("1 Infusion Slot"), never "unused" - the
  contents are instance state `/v2/items` does not carry.
- Agony resistance totals, transmuted skins, attunement state.
- Salvage or upgrade-extraction results.
- Ascended food's effect. `91805` returns `details: {type: Food}` and
  nothing else, so the tooltip says nothing about an effect rather than
  announcing that it has no data (fine food, e.g. `12472`, returns the
  full nourishment block and shows it).

### What shipped

- `RawItemDetail` parsing in `Gw2ItemApiClient` (zero new requests).
- `ItemStatBlock` / `ItemAttributeLine` models, deliberately unreachable
  from `PersistedPlan`.
- `ItemStatMath` (the formula above, plus the attribute-token to
  in-game-name map: CritDamage -> Ferocity, Healing -> Healing Power,
  BoonDuration -> Concentration, ConditionDuration -> Expertise).
- `ItemDescriptionSanitizer` for the API's closed markup vocabulary
  (`<c=...>` spans, `<br>`). Any OTHER angle-bracket text is passed
  through verbatim: deleting unknown text would be a silent bug,
  showing it is a reportable one.
- `ItemStatBlockFactory` - the fixed-stat path plus the null-`details`
  crafting-material path, and one place that decides what an absent
  field MEANS.
- `ItemStatTooltipComposer` - the in-game line order, Blish-free.
- `ItemMetadataService.GetCachedStatBlock` - the session side cache.
- Wiring on the recipe-tree rows and the plan header only - row panel,
  name Label, quantity Label AND the framed icon, since Blish resolves a
  tooltip on the deepest control under the cursor and never bubbles to
  the parent.

### The id-space gate

`CraftingTreeNode.ItemId` is one numeric slot shared by three id spaces
(see `CraftingDecision`): real item ids, wallet currency ids, and guild
upgrade ids. Id 24 is BOTH a real vendor-offer output item and the
currency "Pristine Fractal Relics", and `CraftingPlanPipeline`'s
metadata id union can put the genuine item-24 entry into the very
dictionary the stat cache is filled from.

An item-keyed stat lookup on a currency row is therefore the same
cross-domain collision `CraftingTreeBuilder` already guards icon and
rarity against - only worse, because a stat block's FIRST line is the
item's name in its rarity colour, and it displaces the row's own name
line. `TreeRowTooltipComposer.RowIdIsAnItemId` is the single gate: it
rejects `Currency`, `GuildUpgrade` and `UnrecognizedIngredient` rows,
plus vendor cost-component leaves with no `SubtreeCost` (the
currency half of a barter offer - the item half carries its gold value).

### What is stubbed, and behind which judgment call

The design pass left 14 numbered judgment calls for the maintainer.
This branch built only what is unambiguous and applied the recommended
default wherever it was cheap and reversible. Everything below is a
deliberate stub, not an oversight:

- **Q1 (which surfaces)** - built (a): tree rows + plan header, both
  already on the rich path. Used Materials and the Shopping List are NOT
  wired. The Shopping List is the expensive one: it would force
  `ShoppingRowTooltipFormatter` from `string` to `TooltipContent` plus
  five stamping sites and its test file, and that is the merge hotspot
  of this area.
- **Q4 (stat-selectable gear)** - built (a): a bare `Select stats` line,
  no numbers, no `/v2/itemstats` dependency at all.
  `ItemStatBlock.StatChoiceCount` carries the count as the seam, so
  (b)/(c)/(d) are a composer change plus one catalog service, not a
  re-architecture. Affects e.g. Bolt (39 combinations) and every
  stat-selectable exotic/ascended/legendary.
- **Q5 (itemstats live vs. bundled seed)** - not applicable while Q4 is
  (a). Nothing fetches `/v2/itemstats` today.
- **Q13 (restored-from-disk plans)** - NOT implemented. `PlanStore`
  restore makes no network call by design, so a restored plan's stat
  cache is empty and its rows fall back to exactly the tooltip they had
  before this feature. That is a graceful degradation, never an error
  and never a blank box (`RichTooltipSurface.Show` stays hidden on empty
  content), but it does mean the feature looks absent on the first
  screen after a module restart until the user regenerates. The
  recommended fix (one background `GetMetadataAsync` over the restored
  plan's item ids, then re-stamp on the main thread) was judged too
  expensive to lock in unasked.
- **Snapshot result list** - NOT wired, and not touchable here:
  `Views/MainView.cs` is owned by the concurrent `snapshot-grid`
  branch, which is rewriting that result list. Recorded as post-merge
  wiring.

Defaults applied where the call was cheap and reversible: vendor value
shown and suppressed on `NoSell` (Q7a); binding and restrictions shown
(Q8a); rune bonus lists and sigil/infusion buff lines shown (Q6a);
flavour text shown, muted, markup stripped (Q12a); no item icon in the
tooltip (Q9a - the game has none either); Blish's stock hover delay
(Q10a - `TOOLTIP_HOVER_DELAY` is a private const with no public knob, so
changing it means re-implementing show/hide); `PreferredMaxContentWidth`
left at 500 (Q11a); crafting materials do get a tooltip (Q2a) and it is
always shown rather than only when the name is truncated (Q3a).

Q2a/Q3a are the one VISIBLE behaviour change on every plan: rows that
previously had no tooltip at all now have one.

### Reviewer notes

- **Render-path cost (inferred, not measured):** a row's stat content is
  composed once per row per render and reused verbatim by the settle
  re-ellipsis closure, exactly as `extraTooltipContent` already was. That
  is roughly 15 builder lines per row against the ~10 Blish controls the
  same loop constructs, so it is not expected to be visible - but it is
  new work in the tree render path and nobody has profiled it.
- `ItemStatTooltipComposer.SpaceCamelCase` is a mechanical camel-case
  split, not a lookup table, so a future API type renders as its own raw
  name rather than as nothing. It would mangle an acronym-bearing token
  ("PvP" -> "Pv P"); no such token exists in the type vocabulary today.
- `ItemStatBlockFactory`'s shared empty `NoAttributes`/`NoStrings` lists
  are exposed as `IReadOnlyList` and could in principle be cast and
  mutated by a caller.
- An agony infusion reports the same fact twice - as
  `infix_upgrade.buff.description` and as an `infix_upgrade.attributes`
  entry that renders to the identical string. The composer suppresses
  the buff line when an attribute line already said it VERBATIM; a buff
  description that summarises several attributes is distinct wording and
  still renders.

### Desktop gate (live, required)

1. Generate a plan whose tree contains ordinary crafting materials
   (Mithril Ore, Orichalcum Ingot). Hover a material row: a tooltip
   appears showing the item name in its rarity colour, a blank line,
   then rarity / "Crafting Material" / vendor value with real coin
   ICONS to the RIGHT of their numbers, then the item's description.
   Confirm the coin icons are icons, not the text "0g 0s 7c".
2. Hover the plan header's target item name AND its " x N needed"
   suffix AND the 44px ICON to their left: all three show the same stat
   tooltip (nothing lying over the panel may swallow the hover and show
   nothing). Repeat on a tree row's 34px icon, including a dimmed
   not-crafted reference row, whose scrim overlays the icon.
3. Hover a tree row whose name is ELLIPSIZED: the tooltip's first line
   must be the FULL name, exactly once - not twice. Resize the window
   narrower and wider across the truncation boundary with the cursor
   held still on that row and confirm the tooltip re-renders without
   duplicating or losing the name line.
4. Hover a tree row that also has plan lines (a BuyFromTp row with
   quantity > 1, so it carries a "Unit price:" line): the stat block
   renders first, then ONE blank line, then the unit price with coin
   icons, then "Right-click: Open wiki page". Right-click still opens
   the wiki page.
5. Generate a plan for an item with real attributes (any ascended
   armour piece) and hover its root row: "Defense: N" and the "+N Power"
   attribute lines must match what the game shows for that item.
6. Rarity colour check: hover an Exotic row and an Ascended row and
   confirm the tooltip's name line and its rarity line are the expected
   orange and pink, matching the row's own name colour.
7. Tallest routine case: put a Superior Rune in a plan and hover it.
   The tooltip runs to roughly 16 lines (6 positional bonuses plus the
   identity block). Confirm it stays fully on screen when hovered near
   the BOTTOM edge of the game window - the surface clamps but cannot
   scroll.
8. Restart Blish so the plan restores from disk WITHOUT regenerating.
   Hover a tree row: the tooltip must be the pre-feature one (full name
   when truncated, plan lines) with no stat block, and must never be an
   empty or flickering box. Then press Generate and confirm the stat
   block appears.
9. Confirm no tooltip anywhere shows a raw item id, currency id or
   vendor id.
10. Id-space gate: generate a plan whose tree contains a CURRENCY row
    (a vendor offer paid in karma / fractal relics / spirit shards) and
    hover it, plus a currency cost-component leaf under a vendor node.
    The tooltip must be the pre-feature one - the row's own currency
    name and its plan lines - and must never open with an unrelated
    ITEM's name, rarity colour, type or vendor value.
11. Infusion de-duplication: put a +1 Agony Infusion in a plan and hover
    it. "+1 Agony Resistance" must appear exactly ONCE.

Gate: PASS (2026-08-24 desktop session, branch build at the
review-fix HEAD, captures preflight/gIS3-gIS4). Generated +22 Agony
Infusion live (typed adoption); hovering the root tree row rendered
the full stat block on the opaque facility surface: the item name
in the ascended rarity colour (semantic span roles resolving to
colour in the surface only), the "+22 Agony Resistance" infix buff
line rendered ONCE (the dedup fix live), the rarity and type lines,
the sanitized description with the <c=@...> markup stripped and
wrapped, and the retained "Right-click: Open wiki page" action
line. Materials-with-null-details tooltips (Q2a default) and the
plan-header surface share the same composer path; the cross-domain
id gate (the review's Critical - currency/guild-upgrade ids must
never be looked up as item ids) is pinned by its Blish-free tests.
Judgment calls Q1-Q14 ride to the maintainer unresolved beyond the
implemented cheap-reversible defaults.
---

## Font bump and decision-round polish (font-and-polish)

Four maintainer decisions from the same field-test round, taken as four
commits. The first is the module-wide type change the
minimum-window-width research had been holding open; the other three are
small, independent fixes to things the field test tripped over.

### 1. The +2pt bump ("do it")

Body text moves **Menomonia 14 -> 16** and small/caption/pill text
**12 -> 14**. Title (18) and the plan's display title (32) are unchanged.

`Views/Rendering/UiFonts` names the four sizes by ROLE - `Body`,
`Caption`, `Title`, `Display` - and is now the only place
`GameService.Content.DefaultFontNN` is read from anywhere under `Views/`.
That is the point: the previous size decision was spread over ~60 call
sites plus every Label that silently took Blish's own default, so
"is the module consistent?" was not a question anyone could answer by
looking. It is now `grep -rn DefaultFont Views/` returning comments only,
and `grep -rn "new Label" --include=*.cs .` outside `Views/` returning a
single site (`Module.BuildPlaceholder`) that names a font too.

- **50 Labels were taking Blish's DefaultFont14 default** rather than
  setting a font. Under the old scheme that was invisibly correct; under
  the bump it would have left a third of the module one size behind.
  Every one of them now names a font. The 50th is
  `Module.BuildPlaceholder`'s "Coming Soon", the body of the Plan History
  and Crafting Ranker tabs - live UI that a `Views/`-scoped grep does not
  reach.
- **Four control types are deliberately excluded and stay at Blish's
  own DefaultFont14**: `Checkbox` and `StandardButton` (which
  `FeedbackButton` derives from) expose no `Font` property at all, and
  `TextBox`/`Dropdown` have internal padding Blish authors against its
  default while holding typed values rather than module prose. Anything
  MEASURING one of those four measures in `Caption`, which is the size
  they actually paint - `MainView.MeasureCheckboxWidth`,
  `SettingsCurrencyGridLayout.CellClearWidth` and `ModalDialog`'s button
  sizing all say so at the point of use, and
  `SettingsCurrencyGridLayoutTests` carries a second char-width bound for
  exactly those controls. The same reasoning covers text the module never
  builds a control for at all: `TooltipTextFormat`'s line budget sizes
  Blish's own `BasicTooltipView`, so it is measured at Font14 too.

#### Measured font metrics behind every re-derived constant

Taken by parsing the installed
`C:\Blish.HUD\Content\fonts\menomonia\menomonia-{12,14,16,18}-regular.xnb`
(MonoGame.Extended `BitmapFontReader` XNB, uncompressed) and measuring
with MG.Extended's own advance / `XOffset+Width` rule - the same method
`docs/research/minimum-window-width.md` used, re-run and cross-checked
against that report's published figures before being trusted (it
reproduces the report's pill-run table exactly: 222/198, 242/218,
436/406, 482/452, and its 174px `Thermocatalytic Reagent`).

| size | line height | lowest ASCII ink, past the line box | `M` | `w` | `0` |
|---|---|---|---|---|---|
| 12 | 13 | +3 | 11 | 11 | 8 |
| 14 | 18 | +1 | 13 | 13 | 9 |
| 16 | 20 | +1 | 15 | 14 | 10 |
| 18 | 20 | +3 | 16 | 16 | 11 |

Real strings measure **1.10-1.11x** wider at 16 than at 14 (730 -> 810,
174 -> 192, 263 -> 292), not the naive 16/14 = 1.143.

#### Constants re-derived (old -> new, and on what basis)

| constant | old | new | basis |
|---|---|---|---|
| `WindowSizing.MinWindowWidth` | 1436 | **1478** | the research's +2pt variant (measured at Menomonia 16, not scaled), plus one `TreeIndentPer` of vendor-leaf headroom, plus the widest-digit rather than example-digit cost column (see below) |
| `TooltipTextFormat.LineBudgetChars` | 75 | **71** | NOT a body-bump consequence: this budget sizes text Blish renders itself, in its own `BasicTooltipView` at DefaultFont14, which the module has no seam to re-font. Re-measured at **Font14** over every >=55-character prose string the module builds (73 of them): 7.03px/char average, so Blish's 500px cap is 71 characters, not the 76 the shipped 6.5px/char estimate assumed |
| `SnapshotItemGridLayout.MaxCharWidthPx` | 8 | **9** | item names measure ~8.4px/char at Font16 (192px over 23 characters), rounded up |
| `SnapshotItemGridLayout.MinColumnWidth` | 464 | **516** | derived: `40 + 52*9 + 8`. Two columns at 1158px, three at 1674px |
| `SettingsCurrencyGridLayout.CellNameWidth` | 170 | **190** | 170 x 1.11, so the same currency names still fit before ellipsis |
| `SettingsCurrencyGridLayout.CellTagWidth` | 100 | **110** | "default 3600" measures 98px at Font16; keeps the ~11% slack the 100px slot gave its 89px at Font14 |
| `SettingsCurrencyGridLayout.CellClearWidth` | 74 | **74** | unchanged - it sizes a `Checkbox` label Blish keeps at Font14 |
| `SettingsCurrencyGridLayout.MinColumnWidth` | 424 | **454** | derived from the three above. Two columns need a 908px panel (a 1034px window), clearing the 1478 minimum by ~444px |
| `PlanContentHeightMath.CTableHeaderRowHeight` | 26 | **28** | header label at `LabelY` 5, lowest Font16 ink y=26 - exactly the old band |
| `PlanContentHeightMath.DisciplineRowHeight` | 32 | **36** | two labels at y=7/y=9, ink y=28, divider top was y=29. 36 is what every other single-line table row uses and is on `CreateRowDivider`'s proven-immune list |
| `PlanContentHeightMath.RecipeRowHeightWithSublabel` | 44 | **48** | name line box 18 -> 20 pushed the sublabel y=22 -> 24, and the sublabel's own font grew: ink y=43 against a divider at y=41 |
| `RecipesSectionRenderer` sublabel y | 22 | **24** | sits directly under the name's new 20px line box |
| `TopRegionLayoutMath.StatusToSeparatorGap` | 21 | **23** | the plan status label's Font16 ink landed exactly on the separator |
| `SummarySectionLayoutMath.CostBandCaptionLineHeight` | 20 | **25** | caption font 12 -> 14, measured line height 13 -> 18; keeps the same slack over the real metric |
| `SummarySectionLayoutMath.CostBandCurrencyNoteHeight` | 18 | **23** | same +5 |
| `SummarySectionLayoutMath.CostBandHeight(false/true)` | 68 / 86 | **73 / 96** | falls out of the two above |
| `TreeSectionController.PillHeight` | 20 | **24** | the pill label sits at y=2 in an inset panel of `PillHeight - 2`; its Font14 ink is y=21 against an 18px interior |
| `LabelHelpers.SmallTagHeight` | 18 (literal) | **22** (named) | same shape one level out; promoted to a constant because two call sites centred a tag with a hand-repeated `- 18` |
| `MainView.ItemRowHeight` | 52 | **56** | name line box ends y=24, so the breakdown moved y=24 -> 26 and its ink y=43 -> 47; keeps the old 9px bottom slack |
| `MainView` breakdown line y | 24 | **26** | as above |
| `SettingsTabContent.CurrencyRowHeight` | 30 | **32** | cell labels at y=6, Font16 ink y=27, divider top y=27 |
| `SettingsTabContent.CurrencyHeaderRowHeight` | 24 | **26** | header labels' Font16 ink y=25 |
| `SettingsTabContent.InfoRowHeight` | 20 | **22** | info line at y=2, Font16 ink y=23 - same 1px overhang 20 gave Font14's y=21 |
| `AboutTabContent.InfoRowHeight` | 20 | **22** | same site shape (`AddLabeledInfoSection`'s fixed-height heading panel) |
| `CraftingPlanView` section header font | Font16 | **Font18 (`Title`)** | Body moved onto the 16 this header sat at, flattening the page to one level. Font18 no longer collides with the plan title (Font32) and matches what Settings/About already use |
| `CraftingPlanView.SectionHeaderRowHeight` | 30 (literal) | **32** (named) | the promoted Font18 title's ink is y=28 against a divider at y=27 |
| `ModalDialog.WindowHeight` | 170 | **190** | the message is capped to whole lines of the body font; +20 is exactly one Font16 line, taking the cap from three lines back to four to pay for ~11% wider text |

**Deliberately unchanged, each for a stated reason** (recorded beside the
constant, not just here):

- `PlanRelayoutMath.TreePillColumnWidth` stays **256**. The research's
  four-pill `CRAFT/TP/VENDOR/IGNORE` run measures **242px** at Font14
  against the **252px** budget a 256px column leaves, so it still fits at
  normal padding. The `CURRENCY` + `HAVE n/m TOTAL` run does now need the
  tightened-padding pass (263 normal / 251 tight), which
  `ComputePillFit` already applies unprompted; the `HAVE n/m NEEDED`
  annotation overflows to `+N` as it always did.
- `UsedMaterialRowHeight` / `ShoppingRowHeight` /
  `RecipeRowHeightNoSublabel` (36) and `TreeRowHeight` (40) are
  **icon-driven**: a 34px rarity frame plus a 2px divider already exceeds
  the tallest text run in them.
- `CraftStepRowHeight` (44): its body text was **already Font16** before
  the bump, so only its Font12 -> Font14 sublabel moved, and that
  sublabel's new ink (y=35) still clears its divider at y=41.
- `CurrencyRowHeight` / `FallbackTextRowHeight` (28): a single line at
  y=4 (ink 25) or y=7 (ink 26), with no divider beneath either.
- `CostTileRowHeight` (56): its amount is bottom-anchored by
  `BandAmountY` and its caption block bottom, at the grown caption
  metric, still lands above it (24 vs 30).
- `SnapshotHeaderLayout.StatusRowHeight` / `LogTabContent.StatusRowHeight`
  (24): status labels sit at y=2, Font16 ink y=23.
- `MainView.WalletRowHeight` (36): icon-driven (32px icon at y=2, plus
  2), and its single Font16 line's ink (y=27) sits well inside.
- `SuggestionPanel.RowHeight` (28): its one line is now centred on the
  font's OWN `LineHeight` rather than on a hand-tuned 16, so the offset
  moves with any future size change instead of stranding a descender on
  the next row's top edge (these rows stack flush and opaque).
- `LogTabContent`'s row metrics are **measured from the font at runtime**
  (`Measure(font, "Ag").Height + 2`), so they moved on their own.
  `NotesSectionLayoutMath` likewise takes a measure function.
- `ApiAccessDialog.WindowHeight` (300): **inferred, not measured** - by
  line count its three checks wrap to six lines at both sizes, putting
  the button line near y=193 inside a ~255px content region, i.e. ~60px
  of headroom. Gate item 1 is what actually confirms it.

#### Modelling honesty on the 1478 figure

The window minimum is **measured for the fonts and inferred for the
chrome**, exactly as it was at 1436: the 126px window-to-panel chain has
one ~8px term (Blish's `Panel` border) taken from this repo's own comment
rather than a decompile, so the whole figure carries +/-2px there. That
is the **only** uncertainty in the figure.

All three deepest-row constants in `PlanRelayoutMathTests` are direct
measurements in the one convention the production code uses -
MonoGame.Extended's advance / `XOffset+Width` rule, which is what
`TreeSectionController`'s `nameFont.MeasureString` and
`TreeCostColumnMath`'s pre-scan compute, and the rule that reproduces the
research's `65` for `4194304x ` and `174` for `Thermocatalytic Reagent`:
`DeepestRowQtyPrefixWidth` 65 -> **73** and `DeepestRowNameWidth`
174 -> **192**. So the depth-23 row's designed **24px gutter** and the
depth-24 vendor leaf's exact zero-gutter fit are measured facts, not
approximations.

`DeepestPlanCostColumnWidth` 165 -> **181** is the constant that moved
the window minimum past the research's own +2pt prediction, and it is
worth stating why. Menomonia's digits are **not one width**: at Font16
`0` advances 10px and inks 12, `2` and `7` advance 10 and ink 11, `1`
advances 6, and every other digit advances 9 and inks 11. A run's
measured rect is the leading digits' advances plus the last digit's ink,
so the widest run is drawn from `0`/`2`/`7` and ends in `0` - all-twos
is 3px short of it, all-nines 10px short.
The cost column is the three digit runs' measured widths plus 78px of
fixed chrome (`TreeCostColumnMath.SegmentWidth` = text +
`CoinLabelIconGap` 2 + `CoinIconSize` 20, three segments, two
`CoinSegmentGap` 6 between them), so a six-digit gold total plus two
two-digit units measures:

| gold digits | Font14 | Font16 |
|---|---|---|
| all nines (or 3/4/5/6/8 - one advance class) | 161 | 171 |
| all twos (or sevens) | 168 | 178 |
| widest run: `0`/`2`/`7` ending in `0` | 171 | **181** |
| the research's live example, ~174,000 gold | 166 | 176 |

The constant is now the **worst case**, 181, and the minimum is derived
from it - 1472 -> **1478**. A figure taken at any one example total is
light for a plan whose gold happens to run wider: the withdrawn 175 (an
all-nines figure) by 6px, the live example's 176 by 5px. That is exactly
the kind of digit-choice artifact this section already withdrew once; at
1472 such a plan would have spent
the depth-24 vendor leaf's headroom and cut the depth-23 gutter to 18px.
There is no residual term here to trade against the +/-2px chrome
uncertainty: the constant covers every total the module can price.

An earlier draft of this section recorded two other caveats - a Font16
quantity prefix of 76-77 and a "3px cost-column convention gap". Both
were artifacts of summing `xAdvance` instead of measuring the inked rect,
which is not what either call site does. They are withdrawn rather than
left to send a future maintainer chasing a gap that is not there.

`docs/research/minimum-window-width.md` derived the original Font14 cost
column as `76+6+40+6+40 = 165`, whose components in fact sum to 168 (and
to 171 measured on the inked rect the renderer uses). The report's
arithmetic is corrected in place; the shipped 1436 minimum was derived
from the slipped 165 and is superseded by this section either way.

### 2. The one-letter empty-state hint ("add a hint")

`SnapshotSearchResultBuilder` holds character-name matching back below
`MinCharacterSearchLength` (2) for a good reason - one letter surfaces
everything a character whose name contains it holds, so the opening
keystroke of an item search would widen the list instead of narrowing it
- but the hold-back is invisible: the list comes back empty and reads as
a broken search.

`ShortQueryCharacterHint` returns one extra line for the "No items
match ..." message on **exactly** the case the rule caused: a query
shorter than the minimum whose next keystroke really would match a roster
name. It is silent on a two-letter query (that already searches character
names, so an empty list there is a genuine no-result and the hint would
be a lie), on a one-letter query no roster name carries, on a blank query,
and with no roster at all. It takes the unchecked-character set so a
character the source filter has excluded never triggers a promise the
filter cannot keep. It names no character and no id. MainView appends it
on the items branch only - the Wallet filter has no character matching at
all. Seven Blish-free cases, one of which drives the real `BuildItemRows`
path at one and then two letters to show the hint's premise is true.

### 3. The background-refresh spinner ("use spinner")

Only a clicked Refresh Now turned the inline spinner; the timer-driven
auto-refresh (`Update()`'s staleness gate, `OnSubtokenUpdated`, module
load) ran silently.

`Module` carries a `volatile _backgroundRefreshInFlight` around
`RefreshSnapshotInBackgroundAsync`'s body, set **past** both early
returns so a tick that declines to refresh (already running, or inside
the failure backoff) never spins over nothing. `Update()` drains it to
the view on change - the same dirty-flag shape `SaveStatusThreadSafe`
already uses for status text, because the `finally` may resume on a
ThreadPool thread. The drain sits ABOVE `Update()`'s
`if (_refreshInProgress) return;`, or it could only ever switch the
spinner on after the refresh it belongs to had finished, and it only
marks a value applied once a view existed to receive it, so the
module-load refresh is not lost against a null `_snapshotContent`.

`MainView` keeps **two** flags and shows the OR of them. One shared flag
would let a Refresh Now clicked DURING an auto-refresh switch the running
refresh's spinner off: `UserRefreshAsync`'s own `_refreshInProgress` gate
returns null immediately in that case and MainView's `finally` runs at
once. `_refreshInProgress` remains the gate on whether a refresh may
START and is deliberately not reused for this.

Spinner only, not the status text: the user did not ask for this refresh,
so replacing the timestamp they are reading with "Refreshing..." is a
surprise rather than feedback - and the background path's cancellation
arm writes no status that would restore the label afterwards.

### 4. Sort reset on a new plan ("reset to defaults when you gen a new plan")

`ResetPerPlanSortState` clears both sortable tables to `None` at
`TriggerGenerate`'s commit point, beside the existing
`_sectionExpansion.Clear()`. That point is precisely what distinguishes a
new plan from a re-render of the same one: a re-sort, a tree pill
override and a re-solve all re-render through `RenderPlan` without ever
reaching it, so they keep the sort exactly as before - which is the
behaviour the sortable-tables round deliberately built and which stays.

One method rather than two calls at the site, so a future third sortable
table cannot be reset in one place and forgotten in another - and it is
called from **all three** sites that clear `_sectionExpansion`
(`TriggerGenerate`'s commit point, `ApplyRestoredPlan`,
`RollBackFailedPlanRender`), so "arriving at a different plan" is one
pairing rather than three independent ones.

Only the first of those three can carry a stale sort today:
`ApplyRestoredPlan` cannot run after a Generate in the same session
(`Module`'s `_generateCompletedThisSession` guards it) and
`RollBackFailedPlanRender` leaves no plan and therefore no sortable table
rendered at all. Both calls are no-ops as the code stands. They are there
because the alternative is a local invariant resting on a guard in
another file: relax or reorder that guard, or add a second restore path
(plan history is on the roadmap), and a restored plan would inherit the
previous plan's sort column and header indicator - precisely the
behaviour this commit removes.

`TableSortState`'s class doc claimed the superseded lifetime and is
corrected. The struck-through claim in the sortable-tables section above
points here.

### Reviewer-scrutiny list

Things a reviewer should look at hardest, stated rather than buried:

1. **`UiFonts` is a new abstraction.** It is justified by the 60+ call
   sites it replaces and by `UiMetrics`' existing precedent in the same
   namespace, but it IS new surface.
2. **43 of the 49 Label font insertions were mechanical.** Each was
   verified to be body prose, not a caption; the ones inside fixed-height
   rows were then checked against that row's ink budget, which is where
   the eight row-height growths came from.
3. **`ModalDialog.WindowHeight` 170 -> 190 is the one growth not forced
   by a clipping calculation** - three lines still fit at 170. It buys
   back the line ~11% wider text can now need.
4. **The 1478 minimum's +/-2px chrome term** is the figure's one soft
   spot, described above rather than papered over. Everything else in the
   chain is measured in the convention the renderer itself measures in,
   and the cost column is taken at its widest digits rather than at an
   example total - which is the 6px that separates 1478 from 1472.
5. **The spinner wiring has no automated coverage.** `Module` and
   `MainView` are Blish-bound; the two-flag OR and the `Update()` drain
   are argued from source and pinned only by desktop gate item 4.
6. **`Checkbox` staying at Font14** is a visible inconsistency in the
   Settings tab and the Snapshot source filters - Blish gives no seam.
   Worth a maintainer look at the gate.
7. **The `CURRENCY` + `HAVE n/m TOTAL` pill run now needs the tightened
   padding pass** where it did not before. Not a regression (the pass
   exists for this), but it is a visible density change on those rows.

### Desktop gate

1. At the **1478** minimum, read a plan and a snapshot end to end on
   every tab. Row text is legibly larger than before and nothing is
   clipped: check the Required Disciplines character line (the reported
   descender site), Required Recipes rows WITH a sublabel, the Summary
   cost band's caption and disclosure line, the tree's decision pills,
   and the Shopping List's source tags.
2. Resize from the minimum outward and back. No row, divider, tag or pill
   overlaps its neighbour at any width, the Snapshot grid still gives two
   columns at the minimum and a third past ~1674px, and the Settings
   currency grid stays two-up.
3. Snapshot tab: type ONE letter that a character's name contains but no
   item's does - the empty-state message carries "Type another letter to
   match character names." Type a letter no character carries, and a
   two-letter query, and confirm the line does NOT appear.
4. Leave the module past the snapshot refresh interval with the Snapshot
   tab open: the spinner appears beside the status label for the whole of
   the automatic refresh and stops when it lands. Click Refresh Now while
   that automatic refresh is running and confirm the spinner keeps
   turning rather than stopping early.
5. Sort Used Materials by Amount, then Generate a new plan: the header
   indicator is gone and the rows are in the plan's own order. Re-sort,
   then click a tree decision pill (re-solve) and confirm the sort and
   its indicator SURVIVE that.

Gate: PASS (2026-08-23 desktop session, branch build at the fix HEAD,
captures preflight/gFP5-gFP36), with two sub-cases left to the live
install. (1) READING PASS at the effective minimum: every tab read end
to end - snapshot rows and source tags, tree pills including the "+2"
overflow run, the Required Disciplines character line, Required Recipes
rows WITH sublabel (zoomed: name descenders and sublabel both clear of
the 48px row's divider), the cost band caption, Shopping List tags,
Settings currency grid two-up, About prose, Log rows - nothing clipped
or overlapping. (3) HINT: fixture gained "Quiet Quinn" (only q-carrying
name); "q" produced the empty state WITH "Type another letter to match
character names.", "j" produced it WITHOUT the hint, "qu" listed Green
Wood Log via the character match with no hint. (5) SORT RESET: Shopping
List sorted by Amount showed the indicator; a fresh Generate (Mystic
Tribute, live prices) cleared it and returned plan order; Used
Materials re-sorted ascending (Coin 25x before Clover 35x), and a
VENDOR pill re-solve ("Decisions updated (1 override(s))", list 36->33)
kept the sort, its indicator, and the scroll position. (4) SPINNER:
partially machine-verified - Refresh Now showed "Refreshing..." with
the inline spinner turning and both buttons disabled 300ms in, and the
failure landed with a fresh status timestamp, spinner stopped, buttons
re-enabled. The AUTOMATIC path never starts a refresh in the keyless
sandbox (the API-ready guard skips the timer attempt), so
spinner-during-auto and the overlap click stand on the shared two-flag
wiring plus the manual-path evidence; confirm on a live install. (2)
RESIZE: the client-growth path re-clamped live (window grew from the
1064-client floor to the full minimum when the client widened; the
sandbox renders at Blish UI scale 0.81, so 1478 logical = ~1197
physical, measured against the search-row constants); grip drag-resize
remains synthetically uncatchable (longstanding), so
outward-and-back dragging and the 3-column snapshot threshold are the
user's live checks. Bonus: the Clear Cache ModalDialog message wraps
un-clipped with Caption-measured button padding, the ApiAccessDialog
stacking case renders as recorded, and both plan generations logged
single [plan] tags.

---

## Tooltip authenticity (tooltip-authenticity)

The maintainer's mandate, verbatim: *"tool tips should try as hard as
possible to duplicate the in-game visual style pixel perfectly.
divergences where necessary because of the item representations being
slightly different are ok.. but lets try to make it match."*

Everything below is built from `/mnt/c/Dev/Blish/tooltip-research/spec.md`,
the read-only research artefact that measured the game's own tooltip off
five wiki captures, two third-party replicas, and the live `/v2/items`
responses. Its gap map numbers the deltas G1-G25; this section says what
each one became. **Where the spec labels a claim `inferred` (chiefly
FWDekker's per-item-type replica of the LINE ORDER - no modern in-game
equipment tooltip capture exists on the wiki) the implementation follows
the replica and this section says so.**

### One correction to the record

`ItemStatTooltipComposer` carried the standing comment *"No item icon:
GW2's own item tooltips have none"*. That is **false** - all three wiki
captures open with a ~34x34 framed item icon (spec section 1.5). It was
the largest single visual divergence in the feature and is now the header
row.

### What changed, in the spec's own build order

**1. Colours and roles** (G3, G4, G5, G6, G7, G9). `TooltipSpanRole`
gained `Flavor`, `AbilityType`, `Warning` and a reserved `BonusInactive`;
`Default` (white) took over the whole identity block, and `Muted` narrowed
to genuine secondary annotations. `Bonus` went from green to the measured
light blue - the old comment asserting *"the game's own green for granted
bonuses"* was wrong; `Rune_effects_full.jpg` and
`Rune_effects_partial.jpg` both show blue, and FWDekker uses `#5599ff`.
Flavour went from grey to the measured pale teal `#B1D7D2`. Legendary
moved to the wiki dark skin's `#974EFF`; the rest of `RarityColors` was
already an exact match and is untouched.

`ItemDescriptionSanitizer` stopped discarding the API's `<c=@...>` runs
and now emits role-carrying spans, which is the only way "A gift bag!"
(white) can be told from the quoted flavour after it (teal) inside one
description string. `Sanitize()` is still exactly the concatenation of
those spans, and a nested run restores its outer role on close.
`ItemStatBlock` carries the description verbatim; the split happens at
compose time. `Services/` stays XNA-free - only
`RichTooltipSurface.ResolveColor` maps a role to a `Color`.

**2. Canvas** (G1, G2, G8, G23). `RichTooltipSurface` overrides
`PaintBeforeChildren`, so Blish's own tooltip texture (decompiled: drawn
at `Color.White * 0.98f`, plus four dark inner edge bands) is never drawn
at all. The box is ONE translucent layer - `Color(0,0,0) * 0.92f` - plus a
1px `rgb(6,10,12)` border on all four edges. That is the G1 collision's
recommended resolution: the content covers Blish's chrome entirely, so
audit finding H6 (content bleeding through the box) stays fixed while the
fill is the game's own translucency rather than an opaque `(14,14,14)`.
**Never reduce the alpha while Blish's own art is still drawn underneath**
- that stacks two translucent layers and matches neither.

G23 needed no code: Blish's content edge buffer is `Thickness(4 top, 4
right, 3 bottom, 6 left)` and `RecalculateLayout` turns it into the
ContentRegion every child is positioned inside, which already IS the
game's measured 6px left padding.

**3. Line order and blanks** (G12, G13, G14, G15, G20) and **6. content
completeness** (G16, G17, G18, G19), taken in one commit because both are
lines in the same rewritten identity block.

```
[icon] <Name>                     rarity colour
Weapon Strength / Defense         white   (thousands-separated, G19)
+<N> <Attribute>                  white
<buff> / (N): <bonus>              upgrade-bonus blue
<nourishment> / Duration          white   (measured, steak.png)
(blank)
Infusion Slot                     white   (one line per slot, G16)
(blank)
<Rarity>                          white   (omitted when Basic, G20)
<Weight class> / <Type> Armor     white
(<Hand>)                          white   (G17)
Damage Type / Required Level      white
Double-click to select stats.     white   (G12)
<description / flavour>           white / teal   (G13)
Unique                            white   (G18)
<binding> / <restrictions>        white
(blank)                           ONLY on the Generic shape - see below
<value>                           per-denomination coin run, unlabelled (G14)
```

Whether a blank sits under the header depends on what the body OPENS
with, not on combat facts alone. A body opening with the combat facts or
with the nourishment block runs straight on; one opening with the
identity block, or with an upgrade component's bonus run, takes a blank
first. See "Post-review corrections" below for the measurements and for
the one capture this still diverges from.

The blank ABOVE the value splits the same way, on the item's own shape:
measured absent on `steak.png` (body bands 39, 57, 75-blank, 93, 111,
129 - one 18px pitch from `Required Level: 10` to the coin row, row 128
empty). FWDekker has FOURTEEN builders, only ELEVEN of which emit a
value at all, and it puts a break before `getValue()` in just two of
those eleven - `Generic`, its fallback, and an `UpgradeComponent` of
type Gem. So a weapon, armour piece, consumable, trinket, bag or rune
runs its value straight on under the line above, while a crafting
material, a trait, a key, a Gem or any type the API adds later takes a
blank first. The Generic blank is INFERRED; only its absence is
measured. The three builders that emit no value line - `Gathering`,
`MiniPet`, `Tool` - are a GUESS and the only one in the table; see
"Accepted divergences" below. The vendor value is omitted entirely on a `NoSell`
item - there is no last line and no blank in front of one - and its
plain rendering now drops leading zero units through
`CoinSegmentMath.FormatSegmentTexts`, so "7c", never "0g 0s 7c".

**4. Header icon** (G11). A new HEADER line KIND on `TooltipContent`
carrying an icon URL. `TooltipLayoutMath` gives every row its own `Y` and
`Height`, indents a header line's name column past the icon (continuation
rows included, with the wrap budget narrowed to match), and hands the icon
to the first row of the line only. The icon rides `ItemStatBlock` out of
the same `/v2/items` response the name does - no new request.

**5. Metrics** (G21, G22, G24). Row heights are per-row: a prose row is
one line pitch, only a coin row takes icon clearance, only a header row is
icon-tall. Coin icons in a tooltip are drawn AND measured at ~0.8x the
line height, and the tooltip wraps at 350px. Both of those are
TOOLTIP-LOCAL by design - `CoinSegmentMath.CoinIconSize` and
`TooltipLayoutMath.PreferredMaxContentWidth` keep their values for every
plan table and every plain tooltip; the tooltip passes its own numbers in.

### Beyond the gap map: the maintainer's own scope

**Q1, all surfaces.** The rich item tooltip is now on Used Materials rows,
Shopping List rows and the Snapshot result list as well as the recipe tree
and the plan header. There is no per-surface fork: all five compose
through `Services/ItemRowTooltipComposer`, whose one rule is that the stat
block OPENS the box (it already carries the full name in its rarity
colour, so the ellipsis fallback line would be a duplicate) and the
surface's own extra lines follow after a blank.
`ShoppingRowTooltipFormatter` grew a `BuildRowContent` returning
`TooltipContent`; its HAVE/NEED wording and `BuildCurrencyLines` are
untouched.

`PlanRowViewModel` carries the row's ITEM id, **0 on a currency row** -
the same cross-domain collision `TreeRowTooltipComposer.RowIdIsAnItemId`
guards on the tree side (id 24 is both a real item and the currency
"Pristine Fractal Relics"), gated in `PlanViewModelBuilder` where the
row's source is known.

**Q13, restored plans.** `TooltipFacility.ApplyRichDeferred` composes a
row's content when the box is about to be drawn rather than at render
time. `ApplyRestoredPlan` then fills the session stat cache in the
background via `ItemMetadataService.WarmStatBlocksAsync`, and the next
hover picks the blocks up with no re-render at all; a cursor already
resting on a row when the fetch lands is redrawn by
`TooltipFacility.RefreshCurrent`, marshalled to the main thread.

`WarmStatBlocksAsync` deliberately is NOT `GetMetadataAsync`: that method
writes the unlocked `_cache` and `_knownMissing`, which only the
plan-generation thread touches, and a restore-time top-up racing a
Generate would be two threads writing one Dictionary. The warm path
writes only the locked stat side table, which was already designed for a
background writer and a UI-thread reader. **The stat cache stays OFF the
`PersistedPlan` graph** - nothing here touches the schema, and
`PersistedPlanSchemaMemberSetTests` is unchanged.

Deferring also moved the compose work off the render path, where it ran
once per row per render for rows nobody points at, and let the settle
re-ellipsis closures stop re-stamping tooltips entirely.

### Accepted divergences (G16-style: representation differences, not bugs)

- **Infusion slots say "Infusion Slot", not "Unused Infusion Slot"** (the
  game's exact string, captured on
  `Applying_Infusion_To_Ascended_Staff.jpg`). What is socketed in the
  player's own copy is instance state `/v2/items` does not carry, so the
  module will not claim the slot is empty. It is white and one line per
  slot, so the BLOCK HEIGHT matches the game even though the wording does
  not. Two slots therefore read "Infusion Slot / Infusion Slot".
- **A gathering tool, a salvage kit and a miniature run their vendor
  value straight on under the line above - GUESSED, not measured.** These
  are the only three entries in `ValueSitsAfterABlank` with no evidence
  behind them in either direction: FWDekker's `Gathering`, `MiniPet` and
  `Tool` builders emit no `getValue()` at all (they end on
  `getLevel() + getFlags()`, `"Miniature" + getFlags()` and
  `getDescription() + getFlags()`), and no capture of one exists. This
  module does show their value, because a mining pick and a salvage kit
  both sell, so a shape had to be picked. Picked contiguous by nearest
  body shape - Gathering's description/level/flags body matches Gizmo's
  and Trophy's, Tool's matches Container's and Consumable's, MiniPet's
  matches Trophy's, and all three of those neighbours are contiguous.
  Desktop gate step 6 settles it in one hover.
  **Judgment call - flagged for the maintainer.**
- **Armour's slot line is the API's own noun plus "Armor"** - "Gloves
  Armor" where the game says "Hand Armor". The game's slot vocabulary
  (Head/Hand/Chest/Leg/Foot/Shoulder) is a different word set from
  `details.type` (Helm/Gloves/Coat/Leggings/Boots/Shoulders), and a
  mapping table is exactly the kind of invented data this module refuses.
  The ORDER is the game's (weight class first, measured on
  `warhelm.jpg`); only the noun differs. **Judgment call - flagged for the
  maintainer.**
- **"Damage Type: <x>" has no counterpart in the game's weapon block.**
  Kept because it is a real API fact the tooltip already showed, placed
  after the hand line. **Judgment call - flagged.**
- **A stat-selectable item shows "Double-click to select stats." AND its
  own description, if it has one.** The game's description for exactly
  that item state IS that string, so for the common case the two are the
  same line; emitting both rather than choosing loses nothing. No
  `/v2/itemstats` request is made and `StatChoiceCount` remains the seam
  for a future numbered representation. **Judgment call - flagged.**
- **No `(x/6)` counter and no greyed inactive rune tiers.** Both need the
  character's equipped set - instance state the plan surfaces have no
  business asking for. `TooltipSpanRole.BonusInactive` exists, reserved
  and unused, so an equipped-aware surface does not have to re-plumb the
  role through every composer (spec section 3.2).
- **`<c=@reminder>` has its own `Reminder` role at (175,175,175)**, the
  spec's section-1.4 constant for it (gw2efficiency `.desc-reminder`
  `#afafaf`, inferred). It is deliberately NOT folded into `Muted`
  (150,150,150), which is the measured `#939496` annotation grey off
  `xyaren.png` - two sources, 25 levels per channel apart.
- **The header icon is framed in the measured light grey (166,175,174)**,
  not in the rarity colour the module frames its ROW icons with. The name
  beside it already carries the rarity, and the grey is what the capture
  shows. **Judgment call - flagged.**
- **The upgrade-bonus blue `Color(120,170,235)` is a recommendation, not a
  measurement.** The direction (blue, not green) is measured and
  unambiguous; the exact triple comes from a heavily-compressed JPEG over
  a dark-blue scene plus one replica.
- **Coin number tints are unchanged** (G10). The only measurement is a
  2012 capture; the module's constants are brighter. Not chased without a
  modern sample.
- **A consumable's use description stays inside the identity block, so
  `warhelm.jpg` keeps its blank under the header where the game has
  none.** The game (and FWDekker's `Consumable` / `Container` / `Generic`
  builders) put a consumable's description FIRST, above the identity
  block; the module puts every description in the identity position,
  which is what G13 measured off `xyaren.png` and what keeps one line
  order for every item type. Given that placement the identity block is
  what opens the body, and a blank before it is correct. Reversing it
  means per-item-type builders. **Judgment call - flagged.**
- **No blank before the binding-flags block**, which gap G15 lists as one
  of four required blanks. The primary evidence does not support it:
  `warhelm.jpg` renders `Consumable` and `Account Bound` on consecutive
  16px pitches with no gap (measured, bands y=160-168 and y=176-184), and
  FWDekker's `getFlags()` carries no leading break in any of its fifteen
  builders. `xyaren.png` does show a blank above `Unique`, but the line
  above that blank is the `0/500 in Material Storage` annotation - its
  own block, blank-separated on both sides - which this module does not
  have. Emitting the blank would make `warhelm` measurably worse.

### Reviewer-scrutiny list

- **`PaintBeforeChildren` replaces Blish's art outright.** If a future
  Blish version moves the tooltip's painting, the box would lose its
  background rather than gain a second one. Measured against 1.3.0 by
  decompilation (`ilspycmd`, repo precedent).
- **Deferred builders run inside Blish's mouse-moved handler.** An
  exception in one would be a crash on hover, so resolution is wrapped -
  the box shows nothing and the failure is logged. Every builder should
  stay a pure read of already-built state.
- **A deferred builder retains what it closes over** for as long as its
  control lives: the row view model, the name Label, the renderer's stat
  lookup. Value-references-key cycles in a `ConditionalWeakTable` are the
  documented ephemeron case and still collect, but the retained graph per
  row is larger than the finished `TooltipContent` it replaced.
- **Compose-per-hover cost is unmeasured.** It is ~20 builder lines plus a
  short `StringBuilder` walk of the description, once per hover, against
  the per-render composition it replaced. Inferred to be a net win; nobody
  has profiled it.
- **Icon-note clobbering.** `ApplyRichDeferredToIconTree` cannot skip an
  empty payload the way the eager version does (nothing is composed yet),
  so it would overwrite an icon's own "no icon available for this entry"
  note with silence. `TooltipFacility.Register` now captures each
  control's plain text as the source's fallback
  (`TooltipContent.OrText`), which covers every call site including the
  recipe tree, and a builder that throws degrades to it too. The capture
  lives in the facility rather than at the icon-tree call site precisely
  because a re-stamp - `MainView.ApplyItemRowTooltip` runs again on every
  column resize - reads a `BasicTooltipText` the FIRST stamp already
  nulled; the fallback is carried forward from the previous source unless
  the control has since been given real plain text, or has since had it
  deliberately cleared (`ApplyPlain` records both - see "Post-review
  corrections, round 2"). The `row.ItemId > 0`
  gates that remain are about a currency icon naming its own currency,
  NOT about emptiness - they never prevented it (see "Post-review
  corrections").
- **The header row's wrap budget** is `maxWidth - indent` for continuation
  rows. A very long item name in a narrow box is the case to look at.
- **`RefreshCurrent` is main-thread only.** It is reached from a
  background continuation exclusively through `MainThreadMarshal.Run`.
- **`ItemStatTooltipComposer.SpaceCamelCase`** is unchanged and would
  still mangle an acronym-bearing type token ("PvP" -> "Pv P"); no such
  token exists in the type vocabulary today.

### Post-review corrections

An adversarial review of the milestone raised six Must Fix findings.
Each was verified before being acted on; one was refuted with
measurements and is recorded as a divergence instead of a fix.

**1. The header's blank contradicted `warhelm.jpg` - FIXED, and the rule
re-derived.** The rule was "no combat facts -> blank", and its comment
cited `warhelm.jpg` and `steak.png` as captures of items that HAVE
combat facts. Neither does. Re-measured (PIL, per-row glyph-band profile
of each capture):

| Capture | icon bottom | first body band | blank? |
|---|---|---|---|
| `xyaren.png` (Exotic back item) | y=34 | y=53 | yes |
| `warhelm.jpg` (Fine transmutation consumable) | y=37 | y=38 | no |
| `steak.png` (Fine food) | y=37 | y=39 | no |

That also settles a contradiction inside the spec itself: section 1.2
measures `steak.png` as `37 -> 39` while section 1.6's ASCII
transcription of the same image draws a blank under the name. The
measurement wins. FWDekker's per-type builders agree and explain the
split - `Armor` / `Weapon` / `Consumable` / `Container` / `Generic` emit
their leading block with no break, while `Back` / `Bag` /
`UpgradeComponent` emit `getHeader() + "<br />"`. So the blank is owed
when the body opens with the identity block or with a bonus run, not
when combat facts are merely absent. Food now runs its nourishment block
straight on under the header. The residual `warhelm` divergence is in
the divergence list above.

**2. The missing fourth blank of G15 (before the flags block) -
REFUTED.** See the divergence list: `warhelm.jpg` measures the flags
line contiguous with the line above it, and FWDekker emits no break
before `getFlags()`. The finding cited `warhelm.jpg` as showing the
blank; it does not. Recorded rather than implemented.

**3. A header line with a null icon url - FIXED.** `LayoutContent`
reserved the 39px name indent for every header line, while `RenderRow`
drew only when `IconUrl != null`, so an item whose `/v2/items` response
carries no `icon` rendered its name floating over an empty reserved
column with the body below it at x=0. `TooltipLine.IconUrl`'s own
contract promised the opposite. `HeaderLine` now normalises null to
empty, so a header row always has an icon to draw and null keeps one
meaning: this row draws no icon.

**4. The `row.ItemId > 0` gate did not prevent icon-note clobbering -
FIXED.** A real item id does not make the builder non-empty:
`ItemRowTooltipComposer.BuildRowContent(null stats, short name, no
extras)` returns `Empty`, which is exactly the state of a plan restored
from disk before the Q13 top-up lands. See the reviewer-scrutiny entry
above for the fix.

**5. `ShoppingRowTooltipFormatter` doc-comment hijack - FIXED.** The
`BuildCurrencyLines` summary had been left in place above the newly
inserted `BuildRowContent`, leaving the new method with two `<summary>`
elements and `BuildCurrencyLines` bare of the "THIS ROW" scope-collision
rationale that keeps its suffix from reading as noise. Moved back.

**6. The reminder-grey justification was wrong by an order of magnitude
- FIXED.** `#afafaf` is 175, not "within two levels" of `Muted`'s 150.
`Reminder` is now its own role at the spec's `Color(175,175,175)`.

### Post-review corrections, round 2

A second adversarial review raised three Must Fix findings. All three
were re-verified against the captures before being acted on, and all
three held.

**1. A food's nourishment line was painted upgrade-bonus BLUE - FIXED.**
Re-measured on `steak.png` (PIL, median RGB of pixels at or above 88% of
each band's peak): the two nourishment bands read (252,254,253) and
(252,255,255), against "Food" (251,255,252) and "Required Level: 10"
(254,254,251) on the same capture and the Fine-blue name (106,150,209)
above them. That line IS `details.description` - the field
`ItemStatBlock.NourishmentDescription` carries
(`Gw2ItemApiClient.ParseDetail`) - so the measurement is of this line,
not of a neighbour. The blue is measured on RUNE and SIGIL bonuses only
(`Rune_effects_*.jpg`, FWDekker `#5599ff`), and step 1's re-colouring
swept the food line up with them. `TooltipSpanRole.Bonus`'s own doc
comment asserted the same wrong thing and no longer does.

*Judgment call, flagged.* The 2012 capture's nourishment line is a prose
sentence ("Meal: Double-click to gain +10 power. Lasts 30 minutes.").
The modern API returns an EFFECT LIST in the same field (measured on
12452, 12457, 12345-12348, 9440, 50082 - e.g. "30% Magic Find\n40% Gold
from Monsters\n+10% Experience from Kills", with no `<c=@...>` markup in
any of the eight samples, which is why the factory's flat `Sanitize`
still serves this field). No modern capture of a food tooltip exists on
the wiki, so nobody has measured what the game paints an effect LIST.
White is the only measurement that exists for the field, and it is what
ships; one live sandbox capture of any food item would settle it.

**2. An unconditional blank above the vendor value - FIXED.** Asserted
here as the game's shape and contradicted by the only capture of a value
line. `steak.png` re-measured: body bands at 39, 57, 75 (blank), 93
("Food"), 111 ("Required Level: 10"), 129 (the coin row - number band
y=130-139 at x4-23, coin icon x23-41). Row 128 carries no glyph and rows
122-127 only the `q` descender of "Required": 111 -> 129 is ONE 18px
pitch, contiguous. FWDekker agrees for nine of the eleven builders that
emit a value. The blank now splits on the item's shape - see the
line-order block above - and desktop gate step 6 was corrected, since it
had told the tester to confirm the divergence.

**3. A tooltip CLEAR could be undone by the next re-stamp - FIXED.**
`TooltipFacility.Register` inferred a source's fallback text from
`control.BasicTooltipText`, a field the facility itself nulls on the
first stamp, so a null could not be told from a deliberate clear and the
previous source's note was carried forward over it.
`MainView.FitRowTextLabel` clears a row line's tooltip the moment the
line fits and then re-stamps the row's deferred builder, so widening the
window past a Snapshot row's truncation boundary resurrected the full
item name - and with no stat block for that item yet (a plan restored
from disk before the Q13 top-up) the builder returns empty content, so
the row showed its own full name as a tooltip over the name it was
already showing in full. `ApplyPlain` now records the caller's intent -
clears included - on any source the control already carries. It costs
one `ConditionalWeakTable` lookup per plain tooltip assignment, on the
render path. Not unit-testable: `TooltipFacility` takes a Blish
`Control` and tests stay Blish-free, so desktop gate step 12 carries the
check and now names the no-stat-block precondition it needs.

### Post-review corrections, round 3

**1. The round-2 blank-above-the-value table cited evidence that does
not exist, and miscounted its source twice - FIXED.** Re-read
`tooltip-research/fwd-ItemTooltip.js` line by line. `tooltipString` has
FOURTEEN builders, not thirteen (Armor 433, Back 457, Bag 477,
Consumable 494, Container 513, Gathering 531, Gizmo 548, MiniPet 563,
Tool 580, Trinket 596, Trophy 618, UpgradeComponent 635, Weapon 653,
Generic 680), and only ELEVEN of them call `getValue()` at all (448,
468, 485, 504, 522, 554, 609, 626, 644, 670, 687). So "twelve of its
thirteen builders agree" was wrong in both numbers: the true figure is
nine of the eleven that emit a value. Worse, `Gathering`, `MiniPet` and
`Tool` emit NO value line whatsoever, so they cannot agree with either
shape - yet round 2 had listed all three in the no-blank arm on the
strength of that same sentence, while the arm's own doc comment said an
unknown shape falls to Generic. They are now their own labelled arm,
still contiguous but on a stated nearest-body-shape inference rather
than a borrowed measurement, recorded under "Accepted divergences" as
the one guess in the table, pinned by a Theory test that says so, and
handed to desktop gate step 6. The counts are corrected here, in
`ItemStatTooltipComposer.ValueSitsAfterABlank`'s doc comment and in
`ItemStatTooltipComposerTests`.

### Desktop gate (live, required)

1. Hover a crafting material row in the recipe tree. The box opens with a
   ~34x34 FRAMED ICON and the item name beside it, vertically centred on
   the icon - not a name-only first line.
2. The box itself: square corners, a 1px near-black border on all four
   edges, a faintly translucent black fill with the scene visible through
   it, and NO Blish tooltip art (no rounded/lit texture, no gradient edge
   bands). Every glyph carries a dark halo.
3. Hover an item with attributes (any ascended armour piece): the
   attribute lines are WHITE, and so are the rarity word, the type, the
   weight class and the binding line. Nothing in that block is grey.
   Only the NAME carries the rarity colour.
4. Hover a Superior Rune: all six `(N): <bonus>` lines are LIGHT BLUE,
   not green, none greyed, and there is no `(x/6)` counter.
5. Hover an item whose description has a flavour run (Zojja's anything):
   the flavour is pale TEAL and any unmarked description prose beside it
   stays white.
6. The vendor value is the LAST line, with no "Vendor value:" label,
   per-denomination tints, and coin ICONS to the RIGHT of their numbers.
   On a sellable weapon, armour piece, food or rune it sits directly
   under the line above it with NO blank row (measured on steak.png); on
   a crafting material there IS one blank above it (inferred, FWDekker's
   Generic builder). On a NoSell item (Bolt, a Rebreather) there is no
   value line and no trailing blank at all. THEN settle the one guess in
   the table: hover a Copper Mining Pick (Gathering), a Master's Salvage
   Kit (Tool) and any sellable miniature (MiniPet) and record whether a
   blank sits above the coin row. Nothing measures these three - if the
   game shows a blank, move them to the default arm in
   `ValueSitsAfterABlank` and flip
   `ATypeTheReplicaGivesNoValueLineIsGuessedContiguous`.
7. A Basic item (Mithril Ore) shows NO "Basic" line; an Exotic one shows
   "Exotic" in white.
8. Coin icons in the tooltip are noticeably smaller than the plan
   tables' - about 0.8x the line height - and the plan tables' own coin
   icons are UNCHANGED. Prose rows are one line pitch apart with no extra
   padding.
9. Flavour text wraps at roughly 350px, not 500. Plain tooltips
   elsewhere in the module are unchanged.
10. Hover a row in Used Materials, a row in the Shopping List (name,
    quantity, source tag, Each cell, Total cell, and the icon), and a row
    in the Snapshot result list. All show the same rich item tooltip. A
    Shopping List row with a currency cost still shows its HAVE/NEED
    lines, after the stat block and one blank.
11. Id-space gate: hover a CURRENCY shopping row and a currency
    cost-component leaf in the tree. Neither may open with an unrelated
    ITEM's name, rarity, type or vendor value.
12. Resize the window across a row's truncation boundary with the cursor
    held still on that row: the tooltip's name must appear exactly once,
    never twice, on either side of the boundary. Do it once on a
    Snapshot row whose stat block has NOT arrived (restart so the plan
    restores from disk, and hover before the Q13 top-up lands): once the
    row is wide enough to show its name in full, hovering it must show
    NOTHING rather than that same name repeated as a tooltip.
13. Q13: restart Blish so the plan restores from disk WITHOUT
    regenerating. Within a few seconds of the tab opening, hovering a
    tree row shows the full stat block - no Generate needed. Confirm the
    first hover before the fetch lands is the plain fallback and never an
    empty or flickering box.
14. Confirm no tooltip anywhere shows a raw item id, currency id or
    vendor id.
15. Hover a FOOD item (a Cup of Lotus Fries, any feast) and a UTILITY
    consumable (a sharpening stone). Its nourishment lines start on the
    line immediately under the header - no blank between the name and
    the first effect - and they are WHITE, the same white as the type
    and level lines below them, NOT the runes' light blue. An item with
    no combat facts and no nourishment (a back item, a crafting
    material) still opens with one blank.
16. Hover an item whose icon never loads or whose entry has no icon at
    all. The header shows the neutral dark empty-slot square with its
    "-" mark, the name sits beside it in the same column every other
    tooltip's name sits in, and the body lines below start at the left
    padding - the name must not float over an empty gap.
17. Restart Blish so the plan restores from disk, and BEFORE the stat
    top-up lands hover the ICON of a Used Materials / Shopping List /
    Snapshot row whose icon is missing. It must still say "No icon
    available for this entry." - never an empty box. After the top-up
    lands the same hover shows the full stat block.

Gate: PASS (2026-08-23 night desktop session, branch build at a291fb1,
captures preflight/gTT1-gTT28). Verified live: the full ascended block
(Wupwup Claymore - icon header with framed 32px icon and pink name,
thousands-separated strength, nine white attribute lines, one line per
infusion slot, white identity block with hand line and damage type,
teal flavour inside the block, unlabelled trailing 1g 00s 00c with
icons right, dark 0.92 canvas with no Blish art, shadowed glyphs);
rune (six light-blue (N): tiers, no counter, white Exotic word, the
Element: abilitytype span in pale yellow, white apply-description);
food (nourishment white and flush under the header, NoSell = no value
line, no trailing blank); Basic ore (no rarity line, white prose,
blank-then-value for a material, API icon in the header even though
the ROW icon is the neutral dash placeholder); Fine transmutable and
plank and dowel and mini (Exotic mini: account-bound, no value, no
ids anywhere). All four Q1 surfaces live-hovered with the same rich
box: tree rows, Snapshot list, Shopping List, Used Materials. Q13:
after a restart with no Generate, the restored plan's root hover
showed the full stat block within seconds. Recorded partials: the
Gathering/Tool/MiniPet blank-above-value guess stays unsettled (no
sellable specimen reachable in the sandbox fixture); step 11 stands on
the Blish-free id-space tests plus live UNKNOWN/mini hovers (no
currency-item row renders in the current plans); steps 12 and 17's
timing races (truncation-boundary double-name, pre-top-up icon note)
are pinned by TooltipLayoutMath/deferred-builder tests - the top-up
lands faster than synthetic input can race it. Snapshot rows without
cached stat blocks show no tooltip on an untruncated name - correct
per the no-stats fallback rule.
## Keyboard focus release (kb-focus-release)

Field repro: type into the Crafting Plan search box, do not press Enter,
press Escape. The window closes with the caret still visibly in the box,
and from then on GW2 receives no keyboard input at all until the user
clicks somewhere.

### Diagnosis

Measured by decompiling the vendored `packages/BlishHUD.1.3.0/lib/net472/
Blish HUD.exe` (ilspycmd 10.1.1). Everything below is read off that
source, not inferred:

- `TextInputBase.Focused`'s setter assigns
  `GameService.Input.Keyboard.FocusedControl = this` on EVERY change,
  including a change to **false**. `UnsetFocus()` is the only method that
  nulls the slot (`Focused = false;` then `FocusedControl = null;`), and
  so the only full release.
- Blish soft-unfocuses in two places, both of which therefore leave the
  slot naming a box that is no longer focused: the click-away handler
  (`Focused = _mouseOver && _enabled`) and `DisposeControl`
  (`Focused = false`).
- `Control.Dispose` clears `Parent` BEFORE calling `DisposeControl`, so a
  box disposed while focused leaves the slot holding an orphan whose
  `GetAncestors()` is empty. `KeyboardHandler.Update`'s self-heal only
  walks the named control's ancestors looking for an invisible one, so it
  can never reach that orphan.
- A slot naming one control while another box actually holds focus is
  what the user feels. `KeyboardHandler.ProcessInput`'s Escape branch
  consumes the key clearing the slot and returns, so the first Escape
  does nothing visible; the second finds the slot null and closes the
  window instead. The still-focused box keeps
  `KeyboardHandler._textInputDelegate` (set in `UpdateFocusState(true)`),
  and every keystroke then goes to `_textInputDelegate?.Invoke` and is
  blocked from the game. Clicking anywhere ends it, because the
  click-away handler finally sets `Focused = false`.
- Re-clicking the box does NOT repair it: the setter is guarded by
  `SetProperty`, so with `_focused` already true the assignment to the
  slot is skipped.

The module's own contribution to that desynced state is
`SuggestionPanel.OnFocusChanged`, which re-focused its text box from
inside the `InputFocusChanged` notification whenever the mouse was over
the suggestion list. `UnsetFocus()` raises that notification as its first
step and nulls the slot as its second, so an Escape pressed while
hovering the suggestion list produced exactly the reported state: box
focused, slot empty, listener live. Mouse events are raised from
`MouseHandler.Update` on the main thread (`HandleInput` only stashes the
event), so this is ordinary single-threaded reentrancy, not a race.

### Fix

`Views/FocusRelease.cs` (new) is the module's only full release. It
guards every call - a box may only null the shared slot if it holds
focus or is the control the slot already names - and offers two entry
points:

- `ReleaseOnDispose()`, chained onto a construction site, releases on the
  `Disposed` event, which fires at the top of `Control.Dispose` while the
  control is still whole and ahead of Blish's own soft unfocus. Applied
  at all **11** module text box sites (Snapshot search, Crafting Plan
  search + quantity, Log search, six in Settings, the copyable About
  field).
- `ReleaseWithin(root)` walks a subtree. `ResizableTabbedWindow` calls it
  where the module takes focus away without a click: `Hide()` (the intent,
  ahead of the fade the box would otherwise eat keys through), the
  `Hidden` event (a direct `Visible = false`), `OnTabChanged` BEFORE the
  base implementation swaps and disposes the outgoing view, and
  `DisposeControl`.

`SuggestionPanel` now re-focuses only for the press that is landing on
the panel, observed from the same `LeftMouseButtonPressed` event that
drives the unfocus. The hook is taken in the constructor on purpose:
Blish raises the event in subscription order, and `TextInputBase`
subscribes its own handler when the box first gains focus, so the panel
has to already be ahead of it to classify the release. Keeping that hook
honest needed the panels torn down on unload - they are SpriteScreen
parented, so disposing the window never reached them - which
`CraftingPlanView.DisposeSuggestionPanels()` now does from
`Module.Unload`, alongside the tickers it already had to clean up for the
same reason.

### Fix, second pass (adversarial review)

Review hardened the first pass in two places. Neither is a reproduced
field failure; both are stated as what the code now guarantees.

- `FocusRelease.Release()` called `UnsetFocus()` and returned without
  checking its own post-condition. `UnsetFocus()` is not atomic: it is
  `Focused = false;` - which raises `InputFocusChanged` synchronously,
  through `OnInputFocusChanged`, before the slot is touched - and only
  then `FocusedControl = null;`. The module ships exactly one handler on
  that event, `SuggestionPanel.OnFocusChanged`, and it re-focuses. A
  re-focus landing inside a `FocusRelease` call would end it with
  `Focused == true`, `FocusedControl == null` and
  `UpdateFocusState(true)` having re-armed `SetTextInputListner` - the
  exact swallowed-keyboard state, now with no slot for
  `KeyboardHandler`'s heal sweep to name; through `ReleaseOnDispose()` it
  would re-subscribe a control mid-teardown into Blish's static input
  handler, leaving `_textInputDelegate` pointing at a disposed box for
  the rest of the session. That handler re-focuses only while
  `_pressOverPanel` is set, which the next bullet shows cannot be true
  outside the press dispatch that set it, so this is a hazard closed
  rather than a bug observed. `Release()` walks boxes shared with
  whatever handlers the module adds later and must not depend on that
  analysis holding, so it now verifies: after `UnsetFocus()`, while the
  box still reports focus, it forces `Focused = false` (bounded to 3
  attempts - a handler that re-focuses on every notification cannot be
  out-waited and a spin is worse than a stale slot), then nulls the slot
  only if the box is genuinely unfocused and the slot still names it.
  The invariant it holds: the slot names the box that holds focus, or
  nothing.
- `SuggestionPanel._pressOverPanel`, the press-landed-on-the-panel
  discriminator, is now cleared in `ShowPanel()`, `HidePanel()` and
  `Dispose()` as well as on the global `LeftMouseButtonReleased` and
  where `OnFocusChanged` consumes it. This is hardening, not a repair.
  Re-checked against the decompile, the flag cannot outlive the press
  dispatch that sets it: `Control.Input` is `GameService.Input`, so this
  panel's constructor hook and `TextInputBase`'s own
  `OnGlobalMouseLeftMouseButtonPressed` are two handlers on one event,
  this panel's first. The flag only goes true while the panel is visible;
  the panel is only visible while the box is focused (`ShowPanel()` is
  reachable only under a `_textBox.Focused` guard, and `HidePanel()` runs
  on every unfocus); and a focused box always has `TextInputBase`'s
  handler attached, because `UpdateFocusState(true)` adds it. That
  handler therefore runs later in the same dispatch, sets
  `Focused = _mouseOver && _enabled` - false for a press that landed on
  the panel rather than the box - and `OnFocusChanged` consumes the flag
  synchronously. A dropped `LeftMouseButtonReleased`, which Blish is free
  to do (`MouseHandler.HandleInput` returns without stashing the event
  when a foreground `Form`'s client rectangle contains the point, while
  `CameraDragging`, and while the cursor is hidden, and
  `MouseHandler.Update` skips dispatch entirely when GW2 does not have
  focus), therefore cannot latch it. The clears cost nothing and state
  the lifetime bound at each site, so it survives any of those
  preconditions moving.

No test was added. Every step of this is Blish-bound: which release API
is called, the order Blish raises two of its own events in, and a walk
over `Container.Children`. The testable-looking residue is a three-bool
predicate that would only mirror the implementation, which this repo does
not accept. It stands on the desktop gate. Neither second-pass change has
a scripted repro - the dead keyboard the user reported is exercised by
step 1, and step 6 is a regression check on the discriminator itself,
which is the only part of this pass with behaviour a gate operator can
observe.

`Views/Rendering/TreeSectionController.cs`, the tooltip composers and
`RichTooltipSurface` were not touched.

### Desktop gate

1. **The repro.** Crafting Plan tab, click the item search box, type a
   few letters, do NOT press Enter. Press Escape. Whatever the design
   does - the box unfocuses, or the window closes - the caret must be
   GONE from the box, and typing must reach the game immediately, with
   no click needed first. Run it twice: once with the mouse resting over
   the suggestion list that dropped under the box, once with the mouse
   over the box itself. The hover-the-list run is the one that used to
   fail.
2. **Escape is not eaten.** From that same state, count the Escapes. The
   first releases the box, the next closes the window. Neither one
   should be silently swallowed with nothing happening.
3. **Tab switch while focused.** Type into the Crafting Plan search box
   and, without pressing Enter or Escape, click straight onto another
   tab. Keyboard reaches the game right away. Then press Escape once and
   confirm it closes the window rather than being consumed by a slot the
   old, disposed box left behind. Repeat with the Snapshot search box and
   with a Settings number field.
4. **Suggestion picking still works.** Type enough to raise the
   suggestion list and pick a row with the mouse. The row is selected,
   the name lands in the box, the list dismisses. Do it slowly and with a
   fast click.
5. **Window close by other means.** With a text box focused, close the
   window with its X and with the corner icon toggle. Keyboard reaches
   the game in both cases.
6. **The suggestion-panel discriminator survives an interrupted press.**
   Type into the Crafting Plan search box until the suggestion list
   drops. Press and HOLD the left mouse button over a suggestion row, and
   while still holding it Alt-Tab out of GW2 and release the button
   outside the game client - this is the case where Blish never delivers
   the release. Return to GW2 with the box still focused and check both
   halves of the discriminator from that state, without clicking
   anywhere else first: clicking a suggestion row still selects it (name
   in the box, list dismissed), and a single Escape still hard-releases
   (caret gone, typing reaches the game with no click needed). Then
   repeat the interrupted press and, instead of Escape, click straight
   onto another tab: keyboard must again reach the game immediately.
   Note for the operator: this is a regression check, not a repro. The
   flag is consumed inside the press that sets it, so a dropped release
   is not expected to change any behaviour here - and note that clicking
   anywhere off the suggestion panel, the search box included, reassigns
   the flag and ends the interrupted-press state, which is why the steps
   above go straight from the Alt-Tab to the check.

7. **Enter does not strand the slot.** Click the Qty box, type a
   number, press Enter (caret gone), then press Escape ONCE: the window
   closes immediately - no swallowed first Escape. Measured basis:
   Blish's TextBox.OnEnterPressed is a soft unfocus (Focused = false
   before EnterPressed is raised), leaving the shared FocusedControl
   slot naming the box; every module text box now chains
   ReleaseOnEnter(), whose handler runs ahead of any site handler on
   the same event and clears the stale slot. Repeat on the Crafting
   Plan search box with NO suggestions showing (a query with no
   matches), where Enter falls through AutocompleteTextBox to the same
   base path.

Gate: PASS (2026-08-23 night desktop session, branch build, captures
preflight/gKB1-gKB8). (1)+(2) The exact user repro with the mouse over
the suggestion list: first Escape released the box (dropdown gone,
caret gone, window OPEN - eaten by design, not by a stale slot),
second Escape closed the window. (3) Tab-switch with a focused,
typed-in search box: a single Escape afterwards closed the window
immediately - the disposed box left no slot behind. (7) Qty box +
Enter, then a single Escape: window closed immediately - the
ReleaseOnEnter chain healed Blish's Enter soft-unfocus. (4) Suggestion
picking still works (name lands, list dismisses; one eaten
first-click-after-activation, a known sandbox artifact, resolved on
the repeat). (5) Corner-icon toggle with prior box focus produced no
stranding across subsequent interactions. (6) The interrupted-press
Alt-Tab case is not safely synthesizable in the sandbox; it remains
the regression-check the section describes, pinned by the
discriminator's press-consumed design. Keyboard reach was verified
through Escape semantics rather than typed-into-Paint checks: every
single-Escape-closes result above requires the slot and listener to be
clean, which is precisely the reported failure's negation.
## Root ignore suppression and the zero-cost band (root-ignore-summary-zero)

Two maintainer findings from the same field-test round, both about what
the plan shows once nothing is left to buy.

### 1. The root row must not offer IGNORE

"You should probably not be able to IGNORE the top level item in the
recipe tree." The Ignore pill means "treat this item as already in hand
tree-wide"; on the plan's own target that is a request to plan nothing,
and gw2e offers it only because gw2e's tree has no separate target row.

`CraftingTreeNode.IsPlanRoot` is set in `CraftingTreeBuilder.BuildTree` -
the one method that knows which node the caller asked for a tree OF, and
the method a multi-item batch calls once per requested root, so all N
roots carry it. `DecisionPillPlanner.AppendOwnershipPills` then skips the
"IGNORE" spec for a root, which removes the affordance everywhere at
once: `TreeSectionController` wires the click handler FROM the spec list,
so a suppressed spec is a suppressed handler. Nothing under `Views/` was
edited.

The un-ignore half of the toggle is deliberately NOT suppressed, but not
for the reason first written here. Ignores do NOT outlive the plan they
were set in: `TreeSectionController.ResetForNewPlan` clears
`_ignoredItemIds` on every Generate, and `GenerateStructuredAsync` takes
no ignore set at all, so a freshly generated plan's root can never arrive
already ignored. The genuinely reachable route is a **multi-item batch**:
ignores are keyed by item id and apply tree-wide within one solve, so
ignoring an item where it appears as an *ingredient* under one requested
root also flips a *sibling requested root* of that same item to ignored.
That root offers no "IGNORE" pill, so the "IGNORED" pill in the
`Decision == Have` branch is the only way back out
(`PlanRootIgnoreTests.MultiItemBatch_IgnoringAnIngredient...`).

`PersistedPlan.CurrentSchemaVersion` stays at **3**. `IsPlanRoot` is
`internal`, and Newtonsoft's default contract serializes public
properties only, so the flag never enters the persisted graph and no bump
is needed - a bump would have discarded every existing user's saved plan
(overrides and ignores included) to avoid a pill that self-heals on the
next re-solve anyway. Restore is the one path that builds a
`CraftingPlanResult` without `CraftingTreeBuilder`, so
`PlanStoreHelpers.DeserializePersistedPlan` re-derives the flag on the
roots it already knows - `CraftingTree`, or every `MultiItemRoots` entry
(`PlanStoreTests.LoadLatest_*_ReMarks*AsPlanRoot`).

### 2. A zero-cost plan must still render the whole band

"If you do ignore it the display layout in Total Cost section gets all
messed up and reverts to just showing the Actual Cost to Craft section
with 0c while the rest of the layout disappears when it should revert to
0s."

The cause is `PlanViewModelBuilder.BuildCostFormulaBand`'s collapse rule,
not the renderer: with no owned-materials term the band emits ONE tile,
and `SummarySectionRenderer` left-aligns a lone tile in a full-width
band. That reads fine next to a real number and reads as a broken
section next to `0c`. The rule now collapses only when there is a cost to
show - a plan whose coin cost AND owned-materials term are both zero
renders the full "Total Materials Value - Your Materials Used = Actual
Cost to Craft" formula at zero, tooltips and footnote included. Both of
those zeros have to be zeros somebody computed; the two paragraphs below
are the qualification, not a footnote to it.

The zero middle term must be a **known** zero, not merely an absent one.
`MaterialOpportunityCost` is null by contract outside
`OwnMaterialsMode.Valued` (`SellSideEconomics.ComputeMaterialOpportunityCost`),
so "Use Own Materials" on with "Value Own Materials" off and an inventory
covering the whole plan produces coin cost 0, real `UsedMaterials`, and no
valuation at all. Printing the band there would assert "Your Materials
Used 0c" directly above a Used Materials section listing the materials
actually consumed - a number nobody computed. That case keeps the
collapsed single tile
(`PlanViewModelBuilderSummaryTests.CostBand_ZeroCostButMaterialsConsumedUnvalued_StaysCollapsed`);
Valued mode that genuinely computed 0 still gets the full band.

The same measured-vs-unmeasured test governs the **cost** term. A plan
also totals 0 when nothing in it could be priced: an item with no recipe
and no TP listing generates normally (nothing gates it), and
`CraftingTreeBuilder.BuildNode` maps that node to `Decision = Unknown`
while an ignored node maps to `Have` + `IsIgnored` - so the two are
cleanly separable. `PlanViewModelBuilder.HasUnpricedNode` walks the
display tree (`CraftingTree`, or every `MultiItemRoots` entry, skipping
reference branches) from the zero-cost gate only, never on the priced
path.

**SUPERSEDED (field-fixes-3, item 1).** This round made the unpriced zero
keep the collapsed tile, on the argument that "Total Materials Value 0c"
would state the full market value of a craft the pipeline never valued.
The maintainer field-tested that on real plans - which routinely contain
UNKNOWN nodes (Globs of Dark Matter, account-bound gifts) - and ruled
against it: the band now retains all three cells at 0 in that case too,
and the unmeasured-zero fact is carried by
`PlanViewModelBuilder.UnpricedTileMarker` on every tile caption, an
appended tooltip sentence, and an extra `SummaryFootnote` row. See the
"Zero-band retention, scroll anchoring, click default, MF recipes,
first-load snapshot" section for the current rule. The profit band's
suppression on an unpriced zero survives unchanged. Everything below
about `HasUnpricedNode`'s walk and the Free-mode collapse still holds.

So the class fixed here is every **known** zero - ignoring every child, a
currency-only plan, and Valued mode that priced the consumed materials at
0 all get the same band. One zero-cost state stays collapsed on purpose,
because a term is unmeasured rather than zero: Free mode with owned
materials actually consumed (the paragraph above). It therefore still
shows the lone 0c tile - that is the deliberate shape, not a residual of
the reported bug; see the known-vs-absent-zero paragraph above before
"fixing" it. Band height is unaffected either way -
`SummarySectionLayoutMath.BodyHeight` counts one cost band whether it
holds 1 or 3 tiles.

Coverage: `PlanRootIgnoreTests` (pill suppression across every
`BuildPillSpecs` return path, the flag's single write site, a real
multi-item batch, the reachable ignored-sibling-root case, the end-to-end
"ignore every ingredient" plan through
`CraftingPlanPipeline.ResolveWithOverrides` into `PlanViewModelBuilder`,
and the end-to-end unpriced-ingredient case - since superseded, it now
asserts the marked band), zero-band cases in
`PlanViewModelBuilderSummaryTests` (including the unvalued-materials
collapse, the unpriced single-item and batch cases, and the
reference-branch exemption), and two restore cases in `PlanStoreTests`.
Two pre-existing `DecisionPillPlannerTests` end-to-end cases asserted an
IGNORE pill on a `BuildTree` root and were updated to the new
expectation.

### Desktop gate

1. Generate any plan. The **root row shows no IGNORE pill** - source
   pills and any HAVE annotation are unchanged, and every child row still
   offers IGNORE. Check a multi-item batch too: all N top-level rows.
2. Force the known-zero branch first: turn **"Use Own Materials" OFF**
   (or pick a target whose ingredients you own none of), then ignore
   **every child** of the root until the plan costs nothing. The Total
   Cost section still shows the **full band** - "Total Materials Value -
   Your Materials Used = Actual Cost to Craft" - with 0 amounts, the
   "-"/"=" operators between the tiles, the result tile's highlight box,
   and the footnote line. It does not collapse to a lone 0c tile.
   The precondition matters: with "Use Own Materials" on and stock in
   hand, ignoring a child does NOT stop `InventoryReducer` consuming it
   (the reducer is guided by the solve, not by the ignore set), so the
   plan is no longer a zero-material one and step 2 cannot be read.
3. Repeat step 2 with **"Use Own Materials" ON** on an item you DO own
   ingredients for. Both outcomes below are correct - neither is a
   failure of this change:
   - "Value Own Materials" ON (the default): the full band renders with
     **non-zero** amounts. Total Materials Value and Your Materials Used
     both carry the valuation of what your inventory covered; only Actual
     Cost to Craft reads 0c.
   - "Value Own Materials" OFF: the band collapses to the **lone 0c
     tile**, by design - nothing computed the middle term, so printing it
     as 0 would contradict the Used Materials section right below.
4. Back in the step 2 state, un-ignore one of those children: the band
   returns to its ordinary shape and the numbers come back.
5. (SUPERSEDED by field-fixes-3 item 1 - the band now renders all three
   marked cells at 0 here; the profit-band half of this step still
   stands.) Plan an item with **no recipe and no Trading Post price**
   (root row reads UNKNOWN). The plan totals 0c but the band stays a
   **lone 0c
   tile** - a zero nobody measured must not be dressed up as a priced
   equation. The profit band below obeys the same rule: on such a plan
   **no "Sell Value / Total Materials Value / Profit if Sold" tiles
   render at all** (an unpriced-zero cost would otherwise read as
   "profit = the entire sale price"). Also try a variant whose TARGET
   has a sell price but whose ingredient is unpriced - same
   expectation: lone 0c tile, no profit band.
6. Generate a **multi-item batch** where one requested item is also an
   ingredient of another requested item (e.g. request a weapon and one of
   its components). Ignore that component where it appears as a child row
   under the other root. Its own top-level row flips to HAVE + IGNORED
   (not IGNORE), and clicking IGNORED restores it. This is the only
   reachable ignored-root state - a fresh Generate always clears prior
   ignores, so ignoring an ingredient and then planning that item alone
   yields an ordinary un-ignored root.

Gate: PASS with recorded partials (2026-08-23 night desktop session,
branch build, captures preflight/gRZ1-gRZ6). LIVE-VERIFIED: the plan
root offers no IGNORE pill - and on the strongest possible variant:
a schema-3 plan RESTORED from disk into this build (PlanStoreHelpers
re-derives IsPlanRoot on restore, so suppression holds on restored
plans; children keep their IGNORE pills). The regenerate-confirm
backdrop also demonstrably ate mispositioned clicks (batch-J modality
working). PARTIALS: the all-ignored zero band, un-ignore restoration,
the pre-ignored root's IGNORED escape hatch, and the unpriced-zero
profit-band suppression were not reachable live this session - the
sandbox's known synthetic-input decay (M33/M38 class) killed keyboard
then clicks before the multi-step flows completed. All four stand on
real-production-path tests (PlanRootIgnoreTests end-to-end through
CraftingPlanPipeline + the three zero-band viewmodel cases + the
profit-band assertion) and are one-hover checks on the maintainer's
live install.
Morning re-run (2026-08-24, screens awake, captures preflight/gM1-gM8):
those partials are now LIVE-VERIFIED - a fresh-generated root also
offers no IGNORE pill; ignoring the sole child rendered the FULL
three-tile band at 0 ("Total Materials Value 0c - Your Materials Used
0c = Actual Cost to Craft 0c", profit band legitimately present since
the plan is priced); un-ignoring restored every number. The pre-ignored
root's escape hatch is live-UNREACHABLE by design (TriggerGenerate
clears the ignore set, so a root cannot arrive ignored through the UI);
its unit pin in PlanRootIgnoreTests is the correct and complete
coverage. Bonus: the dimmed-pill dead-click tooltip fired correctly.
## Click volume slider (click-sound-gain)

Field feedback, verbatim: the module's click sound is "VERY quiet. I can
barely hear it over my own mouse physical click sound." This section
records what the playback path actually was, the mapping that replaced
it, and the two judgment calls taken along the way.

### The measured playback path

All of the following is decompiled from the vendored binaries with
`ilspycmd` - Blish HUD 1.3.0
(`packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe`) and MonoGame
3.8.0.1641 (`packages/MonoGame.Framework.WindowsDX.3.8.0.1641`).

1. `PressFeedback.PlayClick` called
   `ContentService.PlaySoundEffectByName("button-click")`.
2. That method's body, in full: skip if `_playRemainingAttempts <= 0` or
   `GameService.GameIntegration.Audio.AudioDevice == null`, then
   `SoundEffect.FromStream(_audioDataReader.GetFileStream(name + ".wav"))
   .Play(GameService.GameIntegration.Audio.Volume, 0f, 0f)`.
3. `AudioIntegration.Volume` is `GetVolume()`, which is:
   `MathHelper.Clamp(mean(last 20 samples of the GW2 audio session's
   MasterPeakValue), 0f, 0.4f)` when "use game volume" is on (the
   default); Blish's own `Volume` setting otherwise, which is itself
   `SetRange(0f, 0.4f)` with a **0.2 default**; and a hard `0f` when
   "mute if no game audio" is on (also the default) and that mean is
   below 0.0001.
4. `SoundEffect.Play(volume, pitch, pan)` assigns straight to
   `SoundEffectInstance.Volume`, whose setter **throws
   ArgumentOutOfRangeException outside [0,1]** - it does not clamp - and
   then multiplies by the static `SoundEffect.MasterVolume`, which is
   MonoGame's untouched `1f` default (`grep MasterVolume` over the whole
   decompiled Blish assembly: no hits).

So the one input that controls loudness is that first argument, its
ceiling is **0.4**, and its everyday value is whatever the game happened
to be peaking at.

The asset itself explains the rest. `audio/button-click.wav` inside
`ref.dat` is 44.1 kHz stereo, 128 ms, 22,616 bytes, and peaks at
**0.357 of full scale** (-8.9 dBFS) with an RMS of 0.024. At Blish's
0.2 fixed default that peak lands at 0.071 (-23 dBFS); at a realistic
game-audio mean it is lower still. A -23 dBFS transient under a physical
mouse click is exactly the reported symptom.

### The mapping, and where the default came from

The module now plays the effect itself: same asset, read once from
`ref.dat`'s `audio` subpath - the same archive and subpath
`ContentService.Load` reads - decoded once into a cached `SoundEffect`,
played at a volume the user sets.

`Services/ClickSoundVolume` is the whole mapping, Blish-free and
therefore unit-tested (39 cases):

- `ToVolume(percent) = Clamp(percent, 0, 100) / 100f`, linear in
  amplitude, handed to `Play` as-is.
- `0` is not "volume 0" - `IsSilent` short-circuits before the asset
  load and before any pooled voice is taken.
- `100` is exactly `1f`, the loudest the asset can be played.
- The clamp is load-bearing, not decoration: see the throwing setter
  above.

Percent that reproduces today's loudness: **40** is the loudest today
could ever be (the 0.4 clamp), and **20** is Blish's fixed-volume
default. The shipped default is **`ClickSoundVolume.DefaultPercent =
75`** - 1.875x the absolute old ceiling (+5.5 dB) and 3.75x the old
fixed default (+11.5 dB), with headroom left above it. It sits at
-11.4 dBFS peak. That constant is the single line to edit when the
maintainer's field test returns a number; nothing else encodes a
default.

### Deliberate divergence 1: the slider is not save-gated

The Settings tab has one Save button and an unsaved-changes prompt driven
by `CaptureFormState`/`UnsavedChangeCount`. The click volume row is
**deliberately outside** that model: it writes through to
`ModuleSettings` on every `ValueChanged` - and from there to the live
player, see the two-sliders section below - exactly like the Diagnostics
checkbox (idiom (a)), and it is **absent from
`CaptureFormState`**. Auditioning a volume through a Save button - and
through a save prompt on the next tab switch - is hostile for the one
setting a user tunes by ear, and listing it in the form state would
report every drag as an unsaved change to a value already on disk. This
is the same reasoning `SettingsFormState`'s own doc comment already gives
for the Diagnostics checkbox.

Cost of writing on every change, measured: `SettingEntry.Value` ignores
an unchanged value, the TrackBar snaps to whole numbers (`SmallStep` is
off), and `SettingsService.Save()` only sets a dirty flag - the JSON
write is debounced until 4 seconds past the last change.

### Deliberate divergence 2: no game-volume coupling

Playing the effect ourselves gives up Blish's game-derived volume,
including its "mute if the game makes no sound" rule. Kept anyway, on
purpose: that rule is the cause of the complaint, and its zero case is
not only a muted game - the peak buffer also reads zero when GW2 is not
running or simply is not making noise at that moment, which would leave
the Settings tab's own Test button dead exactly when someone is setting
the volume with the game quiet. The user-facing mute is the slider's own
0.

The no-audio-device guard is kept, reading a different signal.
Blish tests `Audio.AudioDevice == null`, whose type is NAudio's
`MMDevice`; referencing it here pulls the whole `NAudio.Wasapi` assembly
into the module for one null check (measured: it is a `CS0012` without
it). MonoGame answers the same question in a type the module already
references - `SoundEffect`'s stream constructor throws
`NoAudioHardwareException` when the sound system failed to initialize -
and the loader turns that into a permanent, quiet give-up.

### Two Blish behaviors this row had to work around

- **Nothing in a Blish teardown disposes this tab's controls.**
  `ViewContainer.DisposeControl` runs `Clear()` - and so
  `Container.ClearChildren`, which only sets each child's `Parent` to
  `null` - *before* `base.DisposeControl()`. `Container.GetDescendants`
  is a lazy iterator that enqueues a container's children only after the
  caller has already disposed it, so the walk that disposes the
  `ViewContainer` then finds it empty: disposing the host window
  disposes nothing underneath this tab. `TrackBar` is the one control
  type used here that subscribes to a **static** event in its
  constructor (`Control.Input.Mouse.LeftMouseButtonReleased`, released
  only in its `DisposeControl` override), so the slider is disposed
  explicitly on **both** exits - the previous cycle's at the top of
  `Build`, and the last one from `SettingsTabContent.Teardown`, called
  by `Module.Unload`. Disposing only on rebuild would have left the
  final slider on Blish's mouse handler - and, through its
  `ValueChanged` closure, the entire `SettingsTabContent` graph - for the
  rest of the Blish process, accumulating one graph per module
  disable/re-enable. Swept the other control types used here:
  `TextInputBase` takes its global hooks on focus and releases them on
  unfocus; `Checkbox`, `StandardButton`, `Panel` and `Label` take none.
- `TrackBar.MinValue`/`MaxValue` are assigned even though 0 and 100 are
  already the defaults. Their setters are the only callers of the
  private `MinMaxChanged`, which fills the ten-increment table that
  Ctrl+drag snaps against with `Enumerable.Aggregate`; on a TrackBar
  that never had either assigned that table is empty and the first
  Ctrl+drag throws.

### There are TWO sliders for this setting, and the wiring assumes it

This module never overrides `Module.GetSettingsView`. Blish's default
returns `new SettingsView(ModuleParameters.SettingsManager.ModuleSettings)`,
which renders **every** `SettingEntry` the module defines - and a
`SettingEntry<int>` renders as `IntSettingView : NumericSettingView<int>`,
whose `BuildSetting` builds its own 277x16 `TrackBar`. So Blish's Manage
Modules pane already shows a second, fully draggable 0-100 click-volume
slider, and it is the one the maintainer may well reach first.

Everything therefore hangs off the **setting**, not off either control:

- `SettingEntry.ValueChanged` -> `Module.OnClickSoundVolumeChanged` ->
  `ClickSound.VolumePercent`. Without this the Blish-side slider would
  persist a value that did not take effect until the next relaunch.
- `SettingEntry.ValueChanged` -> `SettingsTabContent.
  OnClickVolumeSettingChanged` -> this tab's slider and "NN%" readout, so
  a drag in the other pane does not leave this tab displaying a value
  that is no longer true.
- This tab's own `ValueChanged` writes the setting and **nothing else**;
  the two hops above do the rest. One path, whichever slider moved.

The loop terminates: the setting-side handler skips the slider write when
the slider already rounds to that percent, and when it does write, the
`ValueChanged` it raises writes back an unchanged setting, which
`SettingEntry` does not re-announce.

Both subscriptions are dropped on unload (`Module.Unload`,
`SettingsTabContent.Teardown`) and that is not optional:
`SettingsManager` hands out `module.State.Settings`, and
`SettingCollection.DefineSetting` returns the **existing** entry for a
key it already holds - so a disable/re-enable cycle re-defines onto the
same objects, and an unsubscribed handler would root each dead `Module`
in turn.

**Swept the sibling settings for the same defect.** The shape that breaks
is "pushed once at `Initialize` into a live object, and otherwise only by
this tab" - every setting Blish also renders has that second UI, so any
setting with that shape was already silently ignoring it.

- `LogDiagnosticsEnabled` - **same defect, fixed the same way.** It is
  pushed into `ModuleLog.Shared.DiagnosticsEnabled`, so toggling Blish's
  own checkbox for it persisted without taking effect until relaunch.
  Now `Module.OnLogDiagnosticsEnabledChanged` carries it, and this tab's
  checkbox writes the setting only.
- `SnapshotRefreshIntervalMinutes` - not affected. The stale-check tick
  re-reads it from the setting on every `Update`.
- `ScrollDiagnosticsEnabled` - not affected. `CraftingPlanView` reads it
  from the setting at each use.
- `LogMaxSizeBytes` - **same defect, and the first sweep got this one
  wrong.** It is not once-per-session: `ModuleLog.Configure` only seeds
  it, `ModuleLog.MaxFileSizeBytes` is a live-settable property that every
  file write re-reads for its self-trim check, and the Settings tab was
  already pushing it there on Save with a comment saying exactly why. So
  the tab's own Save applied immediately while a drag of the TrackBar
  Blish renders for the same entry persisted a value the running file
  sink ignored for the rest of the session. Now
  `Module.OnLogMaxSizeBytesChanged` carries it and the tab's Save writes
  the setting only, same shape as the two above. The handler reads
  `GetClampedLogMaxSizeBytes()` rather than the raw new value, which is
  load-bearing here and not just symmetry: `IntSettingView.RefreshValue`
  widens the bar to `Math.Max(MaxValue, value)` and leaves `MinValue` at
  0, so Blish's bar for this entry spans 0 to the persisted byte count
  and can hand over a few hundred bytes - far under the 1 MB floor the
  tab's own parser enforces.
  Residual, deliberately not fixed: the tab's size TextBox does not
  follow a Blish-side drag, so a Save there afterwards writes back what
  the box still shows. Live-refreshing it would clobber a half-typed
  entry, and the Save is an explicit user action; last writer wins, as
  it did before.
- `LogRetentionDays` - excluded, and this one holds. Age-based pruning
  runs exactly once, in `Module.Initialize`, so a change applies next
  session regardless of which UI made it. Nothing holds a live copy to
  keep current.

### The setting does not reach checkboxes, and cannot cheaply

Swept every Blish control the module instantiates for one that plays a
sound of its own. Measured from the 1.3.0 decompile - the controls that
call `PlaySoundEffectByName` are `Checkbox`, `ColorBox`, `CornerIcon`,
`GlowButton`, `MenuItem`, `StandardButton`, `TabbedWindow` and
`WindowBase`; of those the module uses `Checkbox` (7 sites:
`SettingsTabContent` x2, `CraftingPlanView` x3, `MainView`,
`LogTabContent`), `CornerIcon` (1), `StandardButton` (every
`FeedbackButton`) and `TabbedWindow2`/`WindowBase` as its window chrome.

- `StandardButton` is already mute - it passes `"audio\\button-click"`
  to a reader already rooted at `audio`, so the `FileExists` check fails
  and it returns silently. That bug is why `FeedbackButton` exists, and
  it is what makes the module's buttons fully controlled by the slider.
- `Checkbox` (`OnLeftMouseButtonReleased`) and `CornerIcon` (`OnClick`)
  are **not** covered. They play `"button-click"` at
  `GameIntegration.Audio.Volume` - the same quiet, game-derived path
  this branch replaced everywhere else.
- Window/tab chrome (`window-close`, `tab-swap-N`) is Blish's own and
  out of scope for a module setting.

So at 0 the module is silent except for a checkbox tick and the corner
icon, and at 100 a checkbox is audibly quieter than a button in the same
window. That is a real seam and the field test will meet it.

Why it is not fixed here: the sound sits **inside** those overrides,
ahead of the base call. `Checkbox.OnLeftMouseButtonReleased` is
`if (Enabled) { Content.PlaySoundEffectByName("button-click"); }
base.OnLeftMouseButtonReleased(e);`, and `Control`'s base method is what
raises `Click` (via `_clickPrimed`/`_lastClickTime`, both private). A
subclass that skipped the base to skip the sound would break clicking;
there is no hook, no volume argument and no static to swap. The only
full fix is a module-owned checkbox: `LabelBase` is public, `DrawText`
and `LabelRegion` are protected, and `Checkable.TextureRegionsCheckbox`
is public, so Blish's own 25-line `Checkbox` can be reimplemented
verbatim minus the sound - but that is a new control plus 7 call-site
changes across 4 view files, which does not belong in a branch whose
subject is a volume slider. Deferred, with the recipe recorded here.

### Incidental: this is now cheaper per click, not dearer

`PlaySoundEffectByName` re-read and re-decoded the 22 KB wav into a
brand-new `SoundEffect` on **every** press and never disposed any of
them. The module now decodes once and reuses one cached effect, disposed
on module unload (statics outlive a module instance inside one Blish
session, same as `TooltipFacility`).

### Gate items

1. The Settings tab's new **Sound** section renders at the top: label,
   slider, "NN%" readout, Test button, none overlapping, and the slider
   drags with the readout tracking it live.
2. The **Test** button audibly plays the click, and its loudness follows
   the slider - clearly louder at 100 than at 25.
3. **0 is silent for the clicks this module plays**: drag to 0 and
   neither the Test button nor any other button, row or pill in the
   module makes a sound. Checkboxes and the corner icon are knowingly
   NOT covered - they still tick at Blish's own volume (see the section
   above); hearing those at 0 is expected, not a gate failure.
4. The value **survives a relaunch**: set something distinctive (say 40),
   close Blish, reopen, and the slider, the readout and the actual
   loudness all come back at 40.
5. The **other** slider works too: with this tab open, drag the click
   volume slider Blish renders under Manage Modules and confirm the next
   click is immediately at the new loudness (no relaunch) and that this
   tab's slider and readout follow it.
6. **Log size cap, same wiring, no regression**: with the Settings tab's
   Logging section on 1 MB, Save, and confirm `data/module_log.jsonl`
   still trims at the saved cap this session (the tab no longer pushes
   it directly - `Module` does).
7. Report the number that feels right - it replaces `DefaultPercent`.

Gate: PASS on the render half (2026-08-23 night desktop session,
captures preflight/gSND1-gSND2): the Sound section renders first on
the Settings tab with the Click volume label, the TrackBar at the 75
default, the live "75%" readout, the Test button beside it, and the
instant-apply/zero-off/checkbox-exception prose. The audible half -
how loud 75 actually feels, the Test button's playback at the dragged
value, silence at 0, persistence across a relaunch - is the
maintainer's field check by nature (the sandbox cannot hear); the
percent-to-volume mapping and clamps are pinned by
ClickSoundVolumeTests. The maintainer's number becomes the new
DefaultPercent in a one-line change.
Morning re-run (2026-08-24, captures preflight/gM9-gM14): thumb DRAG
moves the value with the readout live-updating (75 -> 21), the value
SURVIVED a full Blish relaunch (restored at 21), and the slider was
returned to ~the default afterwards. Notes: click-on-track does not
jump and the wheel scrolls the panel, not the slider - both stock
Blish TrackBar behavior. Only audibility remains with the maintainer.


## Follow-up: snapshot rows without plan-cached stats have no rich tooltip

Observed on the tooltip-authenticity gate (2026-08-23): hovering a
Snapshot row whose item has never been part of a generated or restored
plan shows NO tooltip (correct per the no-stats fallback rule, since
the name is untruncated) - the rich stat block only exists for items
the plan pipeline has fetched. The Q5 decision (live per-session stat
fetch) is therefore only partially realized on the Snapshot surface:
bank items never planned get no in-game-style hover. Candidate fix for
the app-wide rollout wave: an on-hover metadata fetch through the
deferred builders + ItemMetadataService's side-table warm path. Until
then this is a recorded, deliberate gap - not a regression.

## Plan-view redesign (plan-view-redesign)

Branched from the unmerged `tooltip-authenticity` head, so its deferred
rich-tooltip facility is part of this work's baseline - the ellipsis
plus full-name idiom below is stamped through `TooltipFacility`.

Built from `/mnt/c/Dev/Blish/plan-redesign/`: `spec.md` (build order,
phases 0-4), `decisions.md` (the maintainer's rulings, which override
the spec where they touch), and the four dossiers `typography.md`,
`layout.md`, `minwidth.md`, `status-ux.md`. Where a dossier and the spec
disagree the spec's cross-check wins; where anything and `decisions.md`
disagree, `decisions.md` wins.

### Phase 0 - foundations

**Tables justify, they do not pull in.** Audit batch H pulled every
table's right-hand block LEFT to sit one 24px breathing room past the
widest name it rendered. The recovered space landed to the RIGHT of the
block, which is what the maintainer's field test rejected: *"columns
start off being smushed over to the left rather than justifying out to
take up the available space dynamically... it leaves stranded dead
space."* Every block is now pinned at
`PlanRelayoutMath.PinnedRightEdge(P) = P - 8`, the name column is the
only one that flexes, and ellipsis plus a full-text tooltip is the sole
overflow idiom.

Deleted rather than left unused, so no caller can reintroduce the
pull-in: `RightBlockX`, `RightBlockRightEdge`, `TableGutterBreathingRoom`,
`TableRightBlockMinX`; `ComputeTreeColumnEdges`' `widestNameEnd`
PARAMETER; `TreeCostColumnMath`'s `measureNameEnd` callback,
`TreeColumnScan.WidestNameEnd` and the depth-carrying walk that existed
only for them; `ShoppingColumnMath.BlockWidth`;
`SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel` /
`CurrencyHeaderBandWidth` / `CurrencyTableOffsetX` (the currency table's
CENTRING dies with the pull-in that motivated it);
`RowRelayoutHelpers`' `dividerWidthForWidth` and
`ShoppingListSectionRenderer.HeaderBandWidth`, both of which now compute
exactly `P`. Header bands and row dividers are full-width again for
free. Six per-render name pre-scans lose their name half; two lose the
whole loop, so net `MeasureString` work per render goes DOWN.

Two consequences the pinned model REQUIRES, since names can now actually
be clipped where the pull-in guaranteed they could not:

- a name's ellipsis budget stops at its neighbouring column's BAND (the
  max across the table), not at that one row's own value width -
  otherwise a row reading "1x" lets its name run under the column's
  widest "429750x";
- every band is `max(widest data, its own header label)`, because a
  header at the ColumnHeader tier routinely out-measures the data under
  it (measured: "Amount" 79px at 20-bold against a 32px "12x";
  "Required" 89px against the currency number column's 60px floor).

**The type ramp is named once.** `Services/TypeRampMetrics` holds the
measured Menomonia ink for every size the module draws in and names the
two promoted tiers: ColumnHeader 20 bold, SectionTitle 24 bold - JC-1
resolved to Alternative B, maintainer: *"lets try 20/24 for now and if
its too big we can go 18/22"*. The retreat is a two-line swap there,
with the height constants and their tests following from it.
`Views/Rendering/UiFonts` turns that into a `BitmapFont` and nothing
else; an unmapped point size throws at the seam rather than rendering at
a size no constant was derived for.

Both measured font-inventory defects are recorded in code, not only in
the dossier. **Menomonia 18-REGULAR's space glyph advances 4px** (against
7 at 16-regular and 9 at 18-bold), so multi-word text at that size
renders with collapsed word gaps - measured, `" x 42 needed"` is 104px at
both 16-regular and 18-regular. That is why status is 18 BOLD, and why
18-regular is now retired from the plan view entirely (it survives only
in the Settings and About tabs, which this milestone does not restyle).
**Menomonia 22-REGULAR is metrically identical to 24-regular** - same
line height, cap and advances, different file bytes - so there is no
regular-weight step between 20 and 24 and it must never be loaded.
22-bold is a genuine intermediate.

**Minimum window width 1478 -> 1378.** 1478 fitted the deepest chain
that EXISTS ("+24 Agony Infusion", depth 23) untruncated. 1378 is
derived for the deepest REALISTIC chain instead - the legendary trinkets
Transcendence and Conflux, both exactly depth 14, whose widest row at
every font size is `429750x Pile of Glittering Dust`. Every term
measured at Menomonia 16 against the installed XNBs
(`plan-redesign/minwidth.md`, which reproduces every anchor figure of
`docs/research/minimum-window-width.md` byte for byte):

```
 629  widestNameEnd = nameX(14) 394 + "429750x " 69 + name 166
 +24  the designed name-to-pill gutter at the deepest row
+256  TreePillColumnWidth
+335  cost column: 181 worst-digit six-digit-gold coin run
                 + 154 widest two-currency vendor run
  +8  TableRightMargin
---- 1252 tab panel  +126 chrome  ==== 1378
```

The +154 rider is JC-5, and it is the maintainer's call: the
like-for-like depth-14 figure is **1232**, which accepts that a row
combining a forced-craft dust chain with a vendor currency run
ellipsizes. Declined - *"We are designing for a minimum resolution of
1920x1080, so cramming down to a smaller min-size that will result in
cramped renders seems bad... Full HD is on everything people game on."*
A two-currency vendor run now always fits at the floor. The agony chain
reads whole to depth 19 and ellipsizes from depth 20, six levels past
the deepest realistic plan; that boundary is pinned by a test so the
accepted degradation cannot quietly get worse. The controls row's own
floor is subsumed and now measured rather than estimated: "Value Own
Materials" is 145px at Blish's Font14, putting that row under 700px.

**`StatusText.Count`** is the module's one spelling of a counted noun,
so `"(s)"` never reaches the interface. `LogTabContent` routes through
it. `ForOverrideResolve` still writes `"(N override(s))"` - the one
remaining offender, and the string the events/state split rewrites
outright rather than repairs, so it moves with that work in phase 3.

### Phase 1 - typography rollout

Column headers (all six tables, through `TableHeaderStyle.Font`'s single
seam) and the Total Cost tile captions to ColumnHeader; the eight
section titles to SectionTitle; the status line to Status (18 bold,
JC-2); the plan header's `" x N needed"` suffix to SmallHeading (20
regular) and the craft-step badge to SmallHeadingBold (20 bold), which
between them retire 18-regular. Body rows are untouched - that is what
keeps the depth-14 minimum valid.

The Disciplines character-availability line goes Caption -> Body and
keeps its grey: it was the one text in the view both smaller AND greyer
than its neighbours, and it carries character names, which a user reads
letter by letter. One channel of de-emphasis, not two. The craft-step
sublabel stays Caption grey (JC-7) - it annotates a quantity, not a
name.

Heights, each derived from measured ink rather than chosen, and each
moved in the same commit as its renderer because they are load-bearing
for scroll math:

| Constant | Was | Now | Derivation |
|---|---|---|---|
| `CTableHeaderRowHeight` / `CTableHeaderLabelY` | 28 / 5 | **32 / 4** | 20-bold lowest ink 26; y=4 reproduces the Body header's exact optics (cap top 8px down, ink bottom 2px clear) |
| `SectionHeaderRowHeight` | 32 | **38** | 24-bold lowest ink 30; title y=3, ink bottom 33, divider top 35 |
| `SectionHeaderCaretY` | 6 | **10** | the caret is Body against a SectionTitle title - baseline-aligned, with the same 1px optical lift the pair had |
| `CostTileRowHeight` | 56 | **58** | caption block bottom 31 against an amount run bottom-anchored at 30 - a 1px overprint |
| `CostBandCaptionLineHeight` | 25 | **32** | same 7px of slack over the real line height (18 -> 25) as before |
| `StatusToSeparatorGap` | 23 | **25** | status lowest ink 23 plus the 2px it has always kept off the rule |
| `InlineSpinnerLayout.PlanStripSize` | 18 | **20** | centred on a 23px line box rather than a 20px one |

The Total Cost disclosure line stays Caption and gains its own measured
height; it had been sharing the caption's, which would have grown it by
7px for nothing.

Three constants move from Views into `PlanContentHeightMath` and are
aliased back (`TableHeaderStyle.LabelY`, the section-header band's three
y's, the cost tile's caption y and amount pad). A label y and the band
height it sits in are one piece of arithmetic, and only one of the two
was testable where they lived.

### Phase 2 - layout per section

Built in the spec's dependency order. Every section's rightmost column
was already pinned by phase 0; this phase gave each one the columns and
the overflow idiom the pinned model requires.

**Total Cost.** Only cleanup was left: each currency row was two nested
panels, because the inner one was the table's centred slice. The table
justifies now, so the slice was exactly the size of the row around it -
a control per row, and a second `Size` write per resize tick, for
geometry that had become the identity. Collapsed to one panel carrying
its own background; the row's truncation tooltip moved with it.

**Used Materials.** Nothing left - phase 1 already gave it the
header-aware Amount band and both halves of the tooltip stamp.

**Required Disciplines.** The character run's full text was stamped on
the row panel alone. Blish resolves a tooltip on the deepest capturing
control under the cursor and never bubbles, so it fired on the blank
strip BESIDE the truncated names and not on the names themselves - the
one place a reader points to find out what was cut off. Both stamps go
through one helper now, so the build pass and the settle re-ellipsis
cannot stamp different control sets.

**Crafting Steps.** `Craft Nx <name>` had no width cap at all: a long
name ran under the right-aligned sublabel and off the panel. It takes
the standard idiom now - ellipsis, full name on the label AND the row
panel, re-derived at settle - budgeted against the widest SUBLABEL this
render draws rather than the row's own (a row with a short sublabel, or
none, must not let its name run under the widest one in the column).
That band is the section's only pre-scan, and unlike every other
table's it has no header label to floor it: the column is unlabelled.

**Required Recipes - the Discipline column.** The discipline was
`row.Sublabel`, a Caption line UNDER the recipe name. That cost the
section a second row height (48 against 36), put a name and its
discipline on different reading lines, and made the discipline both
smaller AND greyer than the text beside it - the double punishment the
type rules ban for a name a reader picks the letters of.

Recipe (flex) | Discipline | Status, one line at 36px:

- `Services/RecipesColumnMath` owns the edges (Blish-free, tested).
  Status pins to `PinnedRightEdge`; Discipline is LEFT-ruled at its own
  x - discipline names are words, not numerics, and a ragged right edge
  under a left rule still reads as one column; the name absorbs the
  rest.
- Both bands are `max(widest data, own header label)`. Measured at the
  ColumnHeader tier, "Discipline" out-measures a short "Chef 400". The
  Status pre-scan comes BACK here - phase 0 deleted it because nothing
  consumed a band width yet, and the accepted divergence that recorded
  that is now discharged.
- The recipe name gains the standard ellipsis, with the full name
  COMPOSED with the row's existing wiki hint rather than assigned over
  it, and both stamped on the name label as well as the row panel (the
  hint was on the panel alone, where the name label swallowed it).
- `RecipeRowHeightWithSublabel` and the per-row height branch are
  deleted; `RecipeRowHeightNoSublabel` is renamed `RecipeRowHeight`,
  there being nothing left to distinguish it from.
- The column and its header are reserved only when some row carries a
  discipline - the same gate Required Disciplines puts on its Characters
  column, so a mystic-forge-only list gives that width to the name.

**Shopping List - the Source column.** The badge was glued to the
name's right edge, so its x moved with every row's own name length and
no two badges lined up; its width had to come out of that row's own
ellipsis budget; and every badge rendered in the same recessed grey, so
the column said WHICH source only to a reader who stopped and read four
capitals on every row.

Item (flex) | Source | Amount | Each | Total:

- `ShoppingColumnMath` grows `SourceX`, derived right-to-left off the
  same pinned edge as the rest of the block. Badges LEFT-rule at that
  x and the name's budget stops there - one fixed x for the whole
  table. The band is `max(widest badge, own header label)`, floored at
  the header for the mirror image of the right-aligned columns' reason:
  a left-ruled header wider than its data overhangs RIGHT, into Amount.
- Fifth sortable column, ordered by the badge TEXT the column shows
  rather than by `PlanRowType`, so a seeded SALVAGE badge groups with
  the S's instead of with the other unknown-source rows.
- Two colours, and only two (`Views/Rendering/ShoppingBadgeColors`):
  VENDOR teal `#2E8B84`, the one hue with no existing meaning in the
  module, for the "go somewhere and buy it" class; UNKNOWN `#B24A4A`,
  darkened out of the Missing!-red family Required Recipes already
  uses, for "the plan cannot price or source this". TP and CURRENCY
  keep Locked's chrome - TP because it is the majority row, CURRENCY
  because the tree's own CURRENCY pill is Locked chrome and one meaning
  must not have two looks. `PillColors` is untouched: its vocabulary is
  the tree's DECISIONS, and none of them means "vendor".
- An UNKNOWN row's unpriceable dash takes the same red, so "no source"
  and "no price" read as one statement. The name keeps its rarity
  colour at full strength - an unknown source is a fact about
  acquisition, not a defect of the item.
- The badge carries its own prose hover (Blish-free, beside the text
  mapping in `ShoppingSourceBadge`), stamped on all three of the tag's
  nested controls because the outer panel is a 1px border.
- The badge's reposition moves from the settle pass to the per-tick
  relayout: its x is width-derived now rather than trailing an
  ellipsis.

**Recipe Tree.** Columns were done in phase 0; what this phase owed it
is the click fix below. **Notes** needed nothing - it was already the
model section for the width principle.

Settle-pipeline inventory after this phase: tree names, Used Materials,
Shopping List, Disciplines characters, Summary currency names, Notes
re-wrap, plus the two new closures - Required Recipes names and
Crafting Steps names. Net `MeasureString` work per render is still down
on the pre-redesign figure.

### Phase 2 - the pill update-in-place fix

Field report: rapid IGNORE toggling with a stationary mouse drops
clicks, and the pill stops highlighting. Two distinct mechanisms, both
measured against decompiled Blish HUD 1.3.0.

**Dropped clicks.** `MouseHandler` buffers exactly ONE pending mouse
event - `_mouseEvent`, written by the hook thread on every event and
consumed once per `Update` - and `Control.OnLeftMouseButtonReleased`
raises `Click` only when that same control INSTANCE was primed by its
own `OnLeftMouseButtonPressed` (`_clickPrimed`). A frame long enough to
contain both halves of the next click therefore loses the press, and
the release finds nothing primed. Every pill click was rebuilding every
control in the plan inside that frame.

Note what this is NOT: click dispatch does not go through
`ActiveControl`. `MouseHandler.HandleMouseEvent` routes button events
through `SpriteScreen.TriggerMouseInput`, and `Container.TriggerMouseInput`
hit-tests `AbsoluteBounds.Contains(position)` against the LIVE tree, so
a freshly built pill under a stationary cursor does receive the click.
Only the priming is lost.

So a local re-solve now asks the tree to update ITSELF, and rebuilds
the plan around it. `TreeSectionController.TryRefreshInPlace` matches
the new solve's tree against the rows already on screen and, when they
present the same rows at the same depth and dim state with the same
children counts, the same cost sub-column widths and the same node
count, repaints each row's pill column, cost cell, qty prefix and
tooltip into the controls it already has. Icons, names, carets and the
row panels themselves - most of the row, and all of its texture work -
are never touched. Ignoring a LEAF material, the case the report is
about, satisfies the gate; ignoring a node with children does not (an
ignored node is built as a leaf - `CraftingTreeBuilder` returns before
its children), and that click still pays for a full rebuild. Every
rejection is a correct full rebuild rather than a wrong cheap one.

Rows are keyed by solver NodeId, not by build order: a lazy expand
appends its children at the END of the build list, so build order stops
being tree order the first time anyone expands anything.

The view keeps the tree section's controls across such a render by
detaching them before the dispose sweep and re-attaching them at the
point the tree occupies in the flow - `_contentPanel` lays children out
in child order, so re-parenting at the right moment IS the ordering.
The tree's relayout/re-ellipsis closures move to their own registry for
the same reason: a closure whose controls survive has to survive with
them. Both registries are replayed together and touch disjoint
controls, so their relative order cannot matter.

**Stale hover.** `MouseHandler.Update` recomputes the hover chain ONLY
when the mouse position changed between frames
(`if (previous.Position != State.Position) ActiveControl =
SpriteScreen.TriggerMouseInput(MouseMoved, State)`). A replacement
control landing under a stationary cursor therefore has `MouseOver`
false and never fires `MouseEntered` - the pill reads as un-hovered,
and this module's own `AnyPillHovered` guard answers wrongly, until the
user jiggles the mouse. `Views/Rendering/HoverChainResync` calls the
same entry point Blish's own motion branch calls, with the live mouse
state. It does NOT restore `MouseHandler.ActiveControl` (private
setter), so tooltip resolution and input blocking still wait for a real
move; the visible hover state, which is what a stationary user sees, is
what this fixes.

Sweep of the other rebuild-on-click surfaces, per the fix-the-class
rule:

- **Sort headers** (Used Materials, Shopping List) and the **Hide
  Unlocked** filter rebuilt the whole plan including the tree, although
  neither re-solves anything: the tree is a pure function of the plan,
  and the plan is unchanged. Both preserve the tree outright now - not
  even refreshed, because its contents are already this plan's - and
  resync the hover chain.
- **Expand/collapse carets** and Expand All / Collapse All build or
  hide rows directly under the cursor, so they resync the hover chain.
  They do not need the in-place path: they never re-solve, and already
  touch only the subtree they own.
- **Section header collapse toggles** flip `Visible` without rebuilding
  anything, and are left alone.

### Phase 3 - status, chips, confirms

Maintainer correction, verbatim: *"1 is about the status of actions and
the other is about the state of your own edits... they are not
connected."*

The status line carried both. `Decisions updated (3 override(s))` mixed
an EVENT - a re-solve just finished - with STATE, how many decisions
you have overridden, which stays true until you change it. The state
half then vanished the moment anything else wrote to the strip, so the
one fact worth keeping was the one that did not last.

- `StatusText.ForOverrideResolve` reports the event alone: `Plan
  updated`, or `Best path restored` when that preset is what fired it,
  never inferred from a zero count. Its `overrideCount` parameter is
  gone, not ignored.
- `Overrides: N` and `Ignored: N` are persistent chips in the top
  strip's LEFT slot, each hidden entirely at zero - a standing
  `Overrides: 0` spends attention on the absence of a thing, and a
  permanently disabled clear button beside it invites "why is this
  disabled?". `Services/TreeChipStripLayout` owns their x's, Blish-free
  and tested. They sit where the grey `Recipe Tree:` caption was: small
  AND grey, labelling five buttons whose own verbs and tooltips already
  said what they act on.
- Each chip has a clear action, and both go through the confirm matrix.

**The confirm matrix.** A dialog appears ONLY when the click would
change something; otherwise the click skips the dialog AND the
re-solve, and the strip says why (three new no-op lines). Predicates
are read at CLICK time from live tree state through `TreeToolbarCommands`
- Craft All and Buy All each build their preset and compare it against
the current override map, a bounded walk that is cheap for a click and
wasteful per render. Generate Plan is deliberately exempt: it clears
both overrides and ignore marks, but it is the tab's primary action and
gating it would punish the ordinary case. Its tooltip is its entire
safety mechanism, so it ships in the same change - what it does, and
what it costs you.

**Measured finding: Clear Overrides and Best Path are the same
action.** `decisions.md` distinguishes them ("clear = back to solver
defaults, Best Path = apply cheapest preset"), but
`TreeSectionController.ApplyBestPathPreset` clears `_nodeOverrides` and
re-solves, and that is exactly what clearing does - the solver's own
choices ARE the cheapest plan it can find. The two differ only in the
status line they write and the dialog they ask. Both shipped as
specified rather than one being silently dropped, and the finding is
recorded here for the maintainer: either Best Path is renamed, or one
of the two goes.

**Wording**, per the status dossier's table: the failure verb splits
(`Generation failed:` leaves the tab without a plan, `Update failed:`
leaves the plan on screen intact with only the change unapplied); the
restored-plan seed drops its second hyphen clause and names a button
that exists; the quantity-reset, settings-changed, no-items, unmatched,
ambiguous and unresolved-rows lines all trim. `"(s)"` is now absent
from the module entirely, including the two remaining non-user-facing
offenders. The `Use Own Materials` dialog is aligned to the matrix
(JC-11) - it was the one dialog left that did not say what is lost.

**Width floor.** The chip cluster is bounded by its widest realistic
form: `Overrides: 12` + 8 + a 124px button + 20 + `Ignored: 12` + 8 +
a 110px button, roughly 455px at Body 16, against the right cluster's
414px of buttons + 32px of gaps + the 20px right padding. Under 950px
together, comfortably inside the 1378 floor, so the floor does not
move.

### Accepted divergences

1. ~~**`RecipeRowHeightWithSublabel` (48) survives phase 1.**~~
   DISCHARGED in phase 2: the sublabel became a column and the constant
   went with it. `RecipeRowHeightNoSublabel` is now `RecipeRowHeight`.
2. **`StatusToSeparatorGap` is 25, not the spec's "+3px".** The spec
   derived the move from the LINE HEIGHT (20 -> 23); the constant's own
   doc comment derives it from the LOWEST INK plus 2px of clearance,
   which is 23 -> 25. The measured-clearance rule is the one that ships
   and the one the test asserts.
3. ~~**The Required Recipes status pre-scan is gone entirely.**~~
   DISCHARGED in phase 2: the scan is back, header-floored, feeding the
   Discipline column's edges.
4. ~~**The currency table keeps its nested full-width content panel.**~~
   DISCHARGED in phase 2: collapsed to one panel, with the row's
   truncation tooltip moved onto it.
5. **`UiFonts.Title` (18 regular) still exists** for the Settings and
   About tabs' own section headers. They have the same collapsed-space
   defect, but restyling two tabs this milestone does not otherwise
   touch is not a font rollout, it is a second redesign.
6. **The chips read `Overrides: N` / `Ignored: N`, not
   `StatusText.Count`'s `N overrides`.** `decisions.md` gives the
   literal wording, and a labelled count is what a gauge should be: two
   chips side by side read as one instrument panel, where "1 override"
   beside "3 items" reads as prose that forgot to be a sentence. The
   dialog copy does use `StatusText.Count`, where the counts sit inside
   real sentences.
7. **A row's in-place repaint is unconditional, not gated on a per-row
   "did anything change" test.** A pill's text, colour, tooltip and
   click wiring derive from the node AND from plan-scope facts
   (currency totals, owned amounts, subduing results), so a cheaper
   test would have to re-derive nearly all of it to be correct - and a
   wrong skip leaves a stale, still-clickable pill. The saving that
   matters is structural (no dispose/rebuild of icons, names, carets,
   row panels or child containers), and it is taken either way.
8. **Clear Overrides ships despite being the same action as Best
   Path.** See the phase 3 finding above: implementing the maintainer's
   stated design, and recording that the code says the two are one, is
   more useful than inventing a difference to justify the second
   button.
9. **`Views/Rendering/HoverChainResync` does not restore
   `MouseHandler.ActiveControl`.** That setter is private in Blish
   1.3.0. Tooltip resolution and input blocking therefore still wait
   for a real mouse move; the visible hover state does not. Splitting
   the two is a divergence from "the hover chain is fully resynced",
   and it is the half a stationary user can see.

### For reviewer scrutiny

1. The band-vs-row-width change in the name budgets (Used Materials,
   Shopping List, Disciplines characters). Every one of those three now
   budgets against the column's widest value rather than the row's own,
   which is correct for a pinned band and is a REAL behaviour change:
   short-value rows lose a few pixels of name they used to keep.
2. `PlanContentHeightMath.SectionHeaderCaretY = 10` is baseline
   alignment, not centring - `layout.md` suggested centring the caret in
   the band (y=9 at 38px). Baseline was chosen because the pair reads as
   one line and the old 18pt pairing was already baseline-aligned to
   within a pixel. Cheap to swap.
3. Whether 20/24 is in fact too big. Every height above is derived from
   `TypeRampMetrics`' tier seats, and the tests assert derivations
   rather than literals, so the 18/22 retreat is a constant swap plus
   whatever the test failures then name. MEASURED after the review
   corrections below: applying it and running the suite gives 2501 green
   on six constants and no test edits - see "Post-review corrections",
   finding 2.
3a. **The tree-preserving render path is the highest-risk change in
   this milestone and has had no live run.** It detaches three
   `_contentPanel` children, disposes the rest, and re-parents them
   mid-rebuild; it keeps a second relayout registry alive across a
   render that clears the first; and it is entered from three places
   (a local re-solve, a sort click, the Hide Unlocked filter). Read
   `ResetContentPanelToEmpty`, `RenderPlan`'s preserve branch and
   `RenderPlanAfterResolve` together. Desktop gate steps 2, 4 and 7
   are what actually exercise it.
3b. **`TryRefreshInPlace`'s gate is deliberately conservative and its
   rejections are invisible.** A rejected refresh is a correct full
   rebuild, so a gate that is too tight looks like nothing at all -
   the click simply stays slow. If the field test says clicks are
   still dropped, instrument the gate before changing anything else:
   the node-count and cost-width checks are the two most likely to
   reject a case that would have been fine. `TreeRowIdentity` is a
   third rejector now (post-review finding 1) and the one to check
   LAST - it is measured on both sides, rejecting the vendor-leaf
   collision and accepting the ordinary ignore.
3c. **The Shopping List's Source band is floored at its own header for
   the OPPOSITE reason to every other column.** It is left-ruled, so a
   header wider than its widest badge overhangs RIGHT into Amount, not
   left into the name. Worth one look at a list whose only badge is
   `TP` (39px against a ~96px header).
4. `UiFonts` resolves `GetFont` per property access rather than
   memoizing. Blish caches internally (`_loadedBitmapFonts`), and the
   call sites are per-section, never per-row; a static cache here would
   outlive a module reload and hold a disposed font.
5. The 1378 figure spends 146px on a rider that only the widest
   two-currency vendor offer needs. 1232 is one constant away.

### Post-review corrections

Nine findings from the adversarial review of this milestone, all
verified against the code before being fixed. Every one reproduced.

**1 (critical). The in-place tree refresh trusted `NodeId` as item
identity, which is false for synthetic cost-component leaves.**
`MatchRows` paired a built row to a fresh node on `NodeId` alone and
`RepaintRow` then deliberately never re-derived Name, IconUrl or Rarity.
That premise holds for a real recipe node - `RecipeNodeIds.Assign` gives
it a stable pre-order id - but `CraftingTreeBuilder.cs:371,406` assign
`NodeId = SyntheticComponentNodeId(parentNodeId, componentIndex++)`, so
a vendor cost-component leaf's id is its POSITION in the offer's cost
lines while its display strings come from that line's own `ItemId`. A
re-solve picking a different offer of the same shape (`{item,
currency}` becoming `{other item, currency}`) keeps every id, depth,
children count and column width, and the row would have kept one item's
name and icon over another item's quantity, cost cell and tooltip - a
state a fresh render disagrees with, which is the second-use rule
outright.

Fixed by `Services/TreeRowIdentity.SameRow`, which the pairing now asks:
item id, cost-component-ness and the three display strings the refresh
keeps, plus the structural pair (`Children.Count`, quantity-presence)
that used to be inline. The hazard is not asserted from a hand-built
model - the first test BUILDS it through the real `PlanSolver` and
`CraftingTreeBuilder` and shows the two leaves sharing a `NodeId` while
naming different items. A second test pins the other direction:
ignoring a leaf material still leaves every row repaintable, so the
stricter gate did not quietly take back the click fix it guards.

**2 (must fix). The 18/22 retreat decisions.md ordered kept "one commit
away" was blocked by a test asserting it is wrong.**
`TypeRampMetricsTests` asserted `ColumnHeaderPointSize >= 16 * 1.25`,
i.e. `>= 20`, so JC-1's own documented fallback failed by construction;
and `PlanContentHeightMathTests` pinned the literal `8` for the header's
cap top, which reads as "the other seat is a regression" rather than
naming the `CTableHeaderLabelY` that seat needs.

The absolute gate is gone. What survives is the relation it was
pretending to be - the title/header/body steps in INK, which is what a
hierarchy actually is - and the optical placement is read out of the ink
it was inherited from (a Body-16 header at LabelY 5). A new test pins
each tier seat's ink to its own point size, so a half-done swap fails
instead of silently deriving every band height from a font the view has
stopped drawing in.

The retreat is now recorded as MEASURED rather than asserted: applied,
suite run, 2501 green. Six constants, no test edits, every band height
unchanged:

    ColumnHeaderPointSize 20 -> 18, ColumnHeaderInk Bold20 -> Bold18
    SectionTitlePointSize 24 -> 22, SectionTitleInk Bold24 -> Bold22
    PlanContentHeightMath.CTableHeaderLabelY   4 -> 5
    PlanContentHeightMath.SectionHeaderCaretY 10 -> 9

The last two are not free-standing choices - a label y is one half of a
band's arithmetic, and the shorter font's cap top and baseline both move
- and each is named by the assertion that fails without it. Removing
them from the swap and re-running gives exactly two failures, both
naming the number to write.

**3 (must fix). `UiFonts`' "fail loudly at the seam" guard blocked
neither banned font face.** `SizeOf()` validated the point SIZE only, so
`Regular(18)` and `Regular(22)` - the two measured defects the milestone
exists to escape - resolved happily, while the file's own doc comment
and `TypeRampMetrics` both stated the ban. Moving `SmallHeadingPointSize`
to 18 during a retreat would have rendered " x 42 needed" at 4px word
gaps with no build error, no test failure and nothing on screen to name
the cause.

The ban now lives once, in `TypeRampMetrics.HasUsableRegularFace`:
`UiFonts.Regular` throws on it at the seam (Bold keeps all four sizes,
because the defect is in the FACE, not the size), and a test refuses to
seat the ramp's one regular-weight role on either - so CI fires before a
screenshot would.

**4 (must fix). The two new chips could overlap the five right-anchored
toolbar buttons, and the guard built for it was never called.**
`TreeChipStripLayout.Slots.EndX` was documented as "what a caller checks
the right-hand button cluster against" and grep found it only in its own
tests. The chips replaced a fixed ~90px grey caption with up to ~438px
of live content against a button cluster starting at `rowWidth - 466`;
below a ~924px row they overlap, and two live buttons on the same pixels
is a click landing on whichever Blish hit-tests last. Reachable inside
the module's supported range: `EffectiveMinWindowWidth` falls back to
the client width below 1378, so a 1024x768 windowed client renders an
918px row and the 930 narrow-screen floor renders 824.

`Fit()` is now the only way to place the strip - `Compute` is deleted,
since a public entry with no production caller is one nothing
re-measures when it drifts. It degrades once, dropping the two clear
buttons and keeping both counts: what the plan's state IS is the
information, and the actions that change it stay reachable through
Generate Plan (clears both) and Best Path (clears overrides), so nothing
becomes unreachable on a window already below the designed floor.
`PlaceTreeToolbarRow` publishes where the button cluster starts - it is
the only place that knows the row's width - and re-fits on every resize
tick, which the chips previously never saw.

**5 (must fix). Craft All / Buy All reported a no-op for a state that is
actually unavailable.** `PresetWouldChange` returned FALSE when
`_lastResult.SolveContext` was null and `ConfirmPreset` read any
non-true answer as "nothing to do", so a plan restored without its solve
context - a real state, since `PlanStructuralValidator` only validates a
`SolveContext` when one is present - answered a Craft All click with
"Already crafting everything craftable". A confident statement about a
plan nothing had examined, on the one line this milestone rebuilt around
a dead click having to say why.

The predicate is tri-state now (true / false / null = cannot be
answered), and the class rather than the instance:
`ApplyOverridesAndResolve`'s silent return on the same condition made
EVERY local change dead in that state - each decision pill, Best Path,
both chip clears - so it reports `StatusText.ReSolveUnavailable` too,
and the confirms ask `CanReSolve` before opening a dialog whose action
cannot run. The new line deliberately claims nothing about the plan's
contents, which is exactly what cannot be known there.

**6 (must fix). `TryRefreshInPlace`'s doc comment described a mechanism
the code does not implement.** It claimed that "keeping the pill's own
instance alive across the re-solve removes the priming hazard outright";
`RepaintRow` disposes and rebuilds every pill Panel, Label and click
handler on a matched row, and only the `List<Panel>` the hover guard
closes over survives. The frame-shortening half is the real argument and
the whole of it. The clause is deleted rather than softened - the
surrounding prose is a measured argument, so a wrong sentence in it
carries a wrong constant's weight, and this is the sentence a maintainer
would trust if the field test still reported dropped clicks.
`HoverChainResync` states the mechanism correctly and is now the wording
of record, pointed at from here.

**7 (must fix). The narrow-client tier assertion sat 6px from asserting
the opposite of what ships.** `TreeChipStripLayoutTests` asserted
`CountsOnly` at a 1024px client - the one case showing the chips degrade
on a real narrow window, and the one gate step 14 was written from.
Recomputed from the production constants: `TabPanelWidthFor(1024)` 898,
a 918px row, a 432px limit against a 438px full strip. A 6px margin, and
90/78 of that 438 are the two count labels the test's own comment
concedes it cannot resolve glyph-for-glyph, because a `Label`'s font is
Blish's and the module measures them live. Real glyphs 12px narrower
combined and the strip renders Full at 1024 while the test keeps passing
on CountsOnly: a green suite certifying a degradation the module does
not perform, at the exact width the overlap finding came from.

What is asserted at every rendered width is now what holds whatever the
labels measure - the counts survive (188px against the narrowest row's
338px) and the strip stops short of the buttons. The TIER is asserted
only where the margin is not a glyph's width: 930, where Full misses by
100px and CountsOnly clears by 150, and 1378, where Full has 348px of
slack. The 1024 arithmetic is recorded in the file as the reason that
width carries no tier assertion.

**8 (must fix). The number the fit negotiates against lived in the view
as five literals, and the test re-typed it.** The repo invariant puts
column/height/ramp arithmetic in `Services/` with tests;
`TreeChipStripLayout.Fit` honoured it but its `limitX` did not.
`PlaceTreeToolbarRow` derived the limit from a walk over widths that
existed only as arguments to `CreateTreeToolbarRow`'s five `PlaceRight`
calls, so the test hard-coded `414 + 32 + 20` beside a comment admitting
a width changed there would leave the boundary cases describing a row
that no longer exists. Renaming "Craft All" to something 34px wider
would have kept production correct (it measures the walk) and quietly
turned every boundary case in the suite into a statement about a row
nobody ships.

`Services/TreeToolbarRowLayout` now owns the row's fixed geometry - each
button's width and the gap to its left, the two chip clear buttons'
widths, and `ChipLimitX(rowWidth)` derived from their sum.
`PlaceTreeToolbarRow` reads `ChipLimitX`, `CreateTreeToolbarRow` places
the same slots, and the tests fit against the same function, so a width
change moves all three at once. Proven by mutation: widening one slot by
400px fails five tests in this suite; before the change the same edit
moved production and left the suite green. `WindowSizing.RightEdgePadding`
is named for the same reason - the cluster stands off the row's right
edge by the same 20px `WindowToTabPanelChrome` already accounts for.

**9 (must fix). Finding 8's fix traded a self-correcting derivation for
a static one, and only prose bound them.** `PlaceTreeToolbarRow` still
PLACED the buttons by walking `_treeToolbarButtons` - whatever
`CreateTreeToolbarRow` put there - but DERIVED the chip limit from
`TreeToolbarRowLayout.ChipLimitX(w)`, a static sum over `RightButtons`.
Measured, the two agreed exactly: the walk consumes 414px of widths and
32px of gaps from `w - RightEdgePadding`, ending at `w - 466`, and
`ChipLimitX` returns `w - (20 + 446) - 20 = w - 486`, the walk's end
less `GroupGap`. So the round was behaviour-neutral - but `ButtonSlot`'s
constructor is public, so a sixth `PlaceRight("Export", new
TreeToolbarRowLayout.ButtonSlot(90, 4), ...)` compiles and ships without
touching `RightButtons`. The strip would then believe it had 94px more
room than the row has: invisible at 1378, where 348px of slack absorbs
it, and on a narrow client the chips paint over the leftmost button -
two live controls on the same pixels, the click landing on whichever
Blish hit-tests last. That is the defect `TreeChipStripLayout` exists to
prevent, and the walk-derived limit finding 8 replaced could not produce
it, because it measured the buttons actually placed.

The limit is now `Math.Min(x - GroupGap, ChipLimitX(w))`: the walk's own
end x is the self-correcting term, the modelled limit the tests assert
is the cap, and production is never looser than the model. A divergence
between them can now only cost the chips room. The alternative - driving
the placement loop from `RightButtons` zipped against a same-length spec
array - was rejected on failure modes, not cost: a spec entry with no
slot would then silently not be placed, trading an invisible overlap for
an invisible missing button, while the clamp cannot produce either. The
residual is a documentation defect only: a slot missing from
`RightButtons` leaves the suite's boundary cases describing a row 94px
narrower than the one that ships. A stale test over-claiming a tier is
worth strictly less than two controls sharing a click target.

### Desktop gate checklist (live Blish, real plan)

1. Every section at the 1378px minimum width: the ramp is legible -
   section title, then column header, then row, each visibly a step
   above the next, in the Total Cost band, Recipe Tree, Used Materials,
   Shopping List, Required Disciplines, Required Recipes, Crafting
   Steps and Notes.
2. Full-width justification at 1378 AND at a wide client (1920+ and
   wider): every table's rightmost column ends one margin in from the
   panel edge, header bands and row dividers run the full width, and no
   table leaves a stranded band of dead space beside it at any width.
   Drag the window across the whole range and watch for a column that
   stops tracking.
3. Ellipsis plus full-name tooltip on a DEEP tree (a legendary, expand
   to depth 14+): truncated names end in an ellipsis and hovering shows
   the whole name. Confirm the same on a truncated Used Materials,
   Shopping List and Disciplines-characters row - hovering the LABEL,
   not only the strip beside it.
4. The Overrides and Ignored chips appear with a non-zero count,
   disappear at zero, and show the right numbers after each of: a pill
   click, Craft All, Buy All, Best Path, Clear Overrides, Clear Ignored,
   Generate Plan.
5. Both chip clear actions go through the confirm matrix's
   would-change-anything guard, and each is distinct from Best Path
   (clear = back to solver defaults; Best Path = apply the cheapest
   preset).
6. Confirm matrix including the no-op cases: Best Path with no
   overrides, Craft All when everything craftable is already crafted,
   Buy All when everything buyable is already bought. Each must SKIP the
   dialog, skip the re-solve, and say why on the status line.
7. Rapid stationary IGNORE toggling: click one pill repeatedly WITHOUT
   moving the mouse. Every click lands, the pill's own highlight tracks
   its state, and no click is swallowed by a rebuild frame.
8. The Generate Plan tooltip is present and states both facts (fetches
   prices and rebuilds; clears manual decisions and ignore marks) - it
   is that button's entire safety mechanism.
9. Status strip at 18 bold with the 20px spinner: no descender touches
   the separator rule, the spinner sits inside the band, and the longest
   real status line still fits at 1378.
10. Confirm no ID of any kind became visible anywhere in the redesign.
11. The Shopping List's Source column: badges LEFT-rule on one x for
    the whole table (not trailing each name), the fifth header sorts
    and groups them, VENDOR reads teal and UNKNOWN red, an UNKNOWN
    row's dash carries the same red, and each badge's own hover names
    the source in prose. Check a list whose only badge is TP - the
    header is wider than the badge there and must not overhang into
    Amount.
12. Required Recipes is one line per row with a real Discipline column
    that lines up under its header, and a truncated recipe name's
    tooltip shows the full name AND still offers the wiki hint.
13. The tree survives a click that does not re-solve it: sort the Used
    Materials and Shopping List headers, and toggle Hide Unlocked, with
    the tree scrolled and partly expanded. Expansion state, scroll
    position and column tracking must all be exactly as they were, and
    a window drag afterwards must still move every tree column.
14. **Narrow client, which steps 1-13 never reach.** Run the game
    windowed at 1024x768 (and again at the 930 floor) so
    `EffectiveMinWindowWidth` falls back below 1378. With BOTH counts
    non-zero: both counts stay readable at every width, and nothing in
    the left cluster paints on "Best Path" or any other toolbar button.
    At the 930 floor the two clear buttons are gone (100px past the
    boundary, so this one is certain); at 1024 the strip is within 6px
    of the boundary, so RECORD which way it falls rather than expecting
    an answer - that measurement is the only thing that can settle the
    count labels' real widths. Drag back out to 1378+ and both clear
    buttons must be present. Post-review findings 4, 7, 9.
15. **A vendor node whose offer carries an item cost AND a currency
    cost** (two synthesised cost-component leaves - expand one). Ignore
    a sibling material so the re-solve can change which bulk offer the
    node takes, then read the two leaves: each name and icon must match
    the quantity, cost cell and tooltip beside it. If a leaf ever names
    one item and prices another, the row-identity gate has a hole.
    Post-review finding 1.

Gate: PASS (2026-08-24 morning desktop session, branch build, captures
preflight/gRD1-gRD17). Verified live: (1) the ramp reads as three
clear tiers in every section; (2) full-width justification holds at
the 1900 client, at 1024, and at the ~930 floor - headers, dividers
and right-anchored columns track the panel at every width; (4) both
chips appear with correct counts, sit side by side in the old grey
label's slot, and disappear at zero (a fresh Generate cleared the
restored "Overrides: 1"); (5) Clear Ignored raised its
consequence-stating confirm ("Stop ignoring 1 item? Their material
costs count toward the plan again.") and its button tooltip reads
"Clears every ignore mark and re-solves"; (6) Best Path with nothing
to change SKIPPED dialog and re-solve with a status explanation -
wording nit recorded: it says "No decision overrides to clear", a
Clear-Overrides phrase, where a Best-Path-specific line would read
better; (7) FIVE rapid stationary IGNORE clicks all landed (odd
parity held through five rebuild frames - the update-in-place fix
proven on the exact field repro); (8) the Generate tooltip states
both facts; (9) the 18-bold status + 20px spinner rendered through a
live generation; (10) no ids anywhere; (11) badges left-rule on one
x, VENDOR teal, UNKNOWN red with matching red dashes, DAILY distinct;
(12) Required Recipes is one line per row with a real Discipline
column and a green Auto-learned status; (13) Hide Unlocked toggled
with the sections above pixel-identical; (14) MEASURED at 1024: both
count labels AND both clear buttons fit with clear space before Best
Path - and they still render at the ~930 floor, against the review's
certain-vanish prediction (the cluster is narrower than modeled).
Sub-minimum clients CLIP an already-wide window (pre-existing
effective-min behavior, not a redesign regression). Recorded
partials, all test-pinned or one-hover checks: deep-tree ellipsis
tooltips (pinned by the depth-19/20 boundary tests + the deferred
full-name builders), item 15's dual-cost vendor leaves (row-identity
gate is test-pinned; one expand on a live plan settles it), badge
hover prose, and the longest-status-at-1378 measurement.
