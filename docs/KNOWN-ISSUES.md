# Known Issues

This is the short, current-state issue tracker for GW2 Crafting Helper. It
holds:

1. The **numbered issue catalog** below - every issue this project has ever
   logged, kept under its original number, with a short resolution
   summary. `.cs` source comments cite these as `KNOWN-ISSUES #N` (or a
   decimal sub-item like `#20.1`); this file is where `#N` still resolves.
2. The **[DEFERRED](#deferred-recorded-not-implemented)** list: items that
   are genuinely still open.
3. The **[milestone record ledger](#milestone-record-ledger)**: one stub per
   rotated milestone record, carrying the names code cites and the path to
   the full record.

The written record lives in three tiers:

- **this file** - current state: the catalog, the open list, the ledger, and
  the append zone at the bottom where a branch's fresh milestone record
  lands before it rotates.
- [`docs/archive/known-issues/`](archive/known-issues/) - the full milestone
  records, one file per section, moved out of this file verbatim. Every
  ledger stub points at one.
- [`docs/dev-notes/HISTORY.md`](dev-notes/HISTORY.md) - the pre-M38 fix-pass
  diary: hypotheses, instrumentation, root-cause traces and dated gate PASS
  records for every numbered item, under the identical item number.

**Looking for *why* a piece of code is shaped the way it is**, rather than
the story of how a specific bug was found and fixed?
[`docs/ARCHITECTURE.md`](ARCHITECTURE.md) distills the durable rationale
for the handful of mechanisms (scroll preserve/restore, the resize
relayout registry, the wheel-delta sanitizer, merged-ceil vendor batching,
solver decision rules, and so on) that this history produced.

## How this file works

The convention below is what every feature branch and every orchestrator
session already practices; it is written down here so it is normative
rather than oral.

1. **One section per branch, appended at true EOF.** A branch that finishes
   a milestone appends exactly one `## <title> (<branch-slug>)` section to
   the very end of this file, below everything else. Never insert
   mid-file: because all writers append at EOF, two branches that both
   touched this file merge by concatenating their tails ("tail-splice") -
   take both sections, keep both, in either order.
2. **Every new section ends with exactly one gate line**, at column 0,
   verbatim: `Gate: [PENDING - the orchestrator fills in PASS/FAIL]`.
   Before opening the PR, confirm
   `grep -c '^Gate: \[PENDING' docs/KNOWN-ISSUES.md` returns exactly 1 and
   that the hit is this section's own, at the true EOF. Use the anchored
   pattern: the marker also appears indented inside this section and inside
   archived records that narrate the same check. Exception: a rule-5
   late-gate section reports a gate that has already run, so it ends with
   the finished `Gate: PASS/FAIL/...` line and carries no PENDING marker.
   On a branch whose only new section is that one, the count is 0, and 0
   is the correct answer - do not invent a PENDING line to reach 1.
3. **The orchestrator edits that line in place** after the live desktop
   session, turning it into a `Gate: PASS/FAIL/PARTIAL PASS ...` record
   with the verification transcript. By then the section may no longer be
   at EOF - that is expected.
4. **Rotation.** Once a section is gated and its follow-ups are swept, it
   moves verbatim into `docs/archive/known-issues/YYYY-MM-DD-<slug>.md`
   (date from the section title, else the introducing merge commit; slug
   from the branch slug in the title) and leaves a stub in the ledger
   below. The stub MUST repeat, verbatim, every quoted name that `.cs`,
   test, or `ref/` files cite the section by, so those citations still
   resolve by grep in this file and land the reader one hop from the full
   record. Un-gated or un-swept sections stay here in full. (Exception of
   record: the 2026-08-24 pass rotated six 2026-08-16 sections whose gate
   line still reads `Gate: not yet run live` - the recipe-sheet/seasonal
   opportunity notes, the three source-selection-simplification sections,
   `shoplist-have-format`, and the festival-vendor auto-tagging follow-up.
   Their gates are still owed; rule 5 says where the outcome lands.)
5. **Never edit an archived record.** A correction to a rotated section is
   an entry in a new section, not a rewrite of the old one - the record is
   evidence (see the high-evidence-zones policy below). This includes a
   gate that runs *after* its section was rotated: record the outcome by
   (a) updating that record's one-line ledger stub below with the verdict
   and date, and (b) appending a short new section here carrying the
   verification transcript and naming the archive file it settles. The
   archived record itself stays byte-frozen.
6. **Tripwire:** whenever `wc -c docs/KNOWN-ISSUES.md` exceeds ~100KB, a
   rotation pass is due. Growth is ~11.5KB per merged branch, so that is
   roughly every eight branches. The 2026-08-24 rotation ran at 917KB;
   don't let it get there again.

**Sequencing hazard for a rotation pass:** a branch cut before the rotation
has its insertion context (the old EOF) deleted underneath it, so its merge
conflicts. Recovery is mechanical - take the branch's `## ` section
verbatim, append it at the new EOF, discard the conflict hunk.

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

### 37. Snapshot refresh failure classification + "GW2 API access is not ready" dialog (2026-08-06)

FIXED 2026-08-06. A Snapshot "Refresh Now" fired at the GW2 character-select
screen failed every source (Blish only resolves the account's API key once a
character is in-world) and reported only a bare "Refresh Failed" label.
`SnapshotFailureClassifier` now sorts a failed refresh into
`ApiAccessNotReady` / `NetworkOrApiDown` / `PartialFailure` / `Unknown`
(Blish-free, by exception type name), and `Views/ApiAccessDialog` explains
the three checks with a Retry that shares `MainView.RefreshNowAsync()`.
Full record:
`docs/archive/known-issues/2026-08-06-field-test-ux-wave-2-mysticforge-sublabel-drop-fix.md`.

### 38. Review-pass fixes on item 37: ApiAccessDialog self-defense + background-refresh status parity (2026-08-06)

FIXED 2026-08-06. Three gaps found reviewing item 37 before its gate:
`ApiAccessDialog.Show()`/`Hide()` leaned on an unrelated Module-owned
object's lifecycle instead of their own `_disposed` flag (and `Dispose()`
was not idempotent); the dialog never reset its own state; and the
background-refresh path did not reach the same status wording as the manual
one. Same full record as item 37.

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

Extracted from milestone records during the 2026-08-24 rotation - still
open, so they live here rather than only in the archive:

- **(from the AUDIT ROW 56 record, 2026-08-16)** Shopping-list row tooltip
  caveats: the shopping-list row tooltip still carries neither price-side
  fallback caveat, in either direction. Under `PriceBasis.InstantBuy`, an
  item whose `BuyInstant` side has zero listings falls back to its
  `SellInstant` (buy-order) price and still renders as a flat `Buy` row at
  that coin figure, which reads as instantly fillable when it is not. Also
  recorded there: a `SolverDecision`-level precomputed
  `VendorItemPriceSideFellBack` field could replace the inline `.Any()`
  scan `CraftingTreeBuilder.BuildNode` re-runs per node - declined at the
  time as an unrequested new abstraction on small lists, not as a wrong
  idea. Full record:
  `docs/archive/known-issues/2026-08-16-audit-row-56-daily-craft-cooldown-notices-three.md`.
- **(from the font-and-polish gate, 2026-08-23)** Two sub-cases left to the
  live install, never closed: grip drag-resize outward-and-back (the grip
  path is synthetically uncatchable, a longstanding limitation) together
  with the 3-column snapshot threshold, and spinner-during-*automatic*
  refresh (the keyless sandbox never starts an automatic refresh, so that
  half stands on the shared two-flag wiring plus the manual path's
  evidence). Full record:
  `docs/archive/known-issues/2026-08-23-font-and-polish.md`.

---

## Milestone record ledger

One stub per rotated milestone record, oldest first. The full record - what
changed, the adversarial-review rounds, the measured-constant derivations,
the repo-invariant sweep, the gate checklist and the gate transcript - is in
the linked file, verbatim as it was written. Names in quotes are the exact
strings `.cs`, test, and `ref/` files cite the record by; they are repeated
here so those citations resolve in this file.

Rotated 2026-08-24 out of a 917KB active file. See "How this file works"
above for when the next rotation is due.

- **Field-test UX wave (six S-sized display fixes, 2026-08-06)** - gate PASS 2026-08-06.
  `docs/archive/known-issues/2026-08-06-field-test-ux-wave.md`
- **Field-test UX wave 2: MysticForge sublabel drop fix (2026-08-06)** - gate PASS 2026-08-06.
  `docs/archive/known-issues/2026-08-06-field-test-ux-wave-2-mysticforge-sublabel-drop-fix.md`
- **Wave-3 quick wins (2026-08-06)** - gate PASS 2026-08-06.
  `docs/archive/known-issues/2026-08-06-wave-3-quick-wins.md`
- **W3B: Generation progress + rich logging (2026-08-08)** - gate PASS 2026-08-08.
  Cited as: W3B section.
  `docs/archive/known-issues/2026-08-08-w3b-generation-progress-rich-logging.md`
- **W3C: Per-character discipline display (2026-08-08)** - gate PASS 2026-08-08.
  `docs/archive/known-issues/2026-08-08-w3c-per-character-discipline-display.md`
- **W3D: Plan persistence across module restarts (2026-08-09)** - gate PASS 2026-08-16.
  `docs/archive/known-issues/2026-08-09-w3d-plan-persistence-across-module-restarts.md`
- **W4B: Vendor cost-component leaves (2026-08-15)** - gate PASS 2026-08-16.
  Cited as: tree dimming rule.
  `docs/archive/known-issues/2026-08-15-w4b-vendor-cost-component-leaves.md`
- **Craft/vendor comparability parity fix (2026-08-15)** - gate: see record.
  Cited as: "Craft/vendor comparability parity".
  `docs/archive/known-issues/2026-08-15-craft-vendor-comparability-parity-fix.md`
- **Craft/vendor comparability parity fix - adversarial review follow-up (2026-08-15)** - gate: see record.
  `docs/archive/known-issues/2026-08-15-craft-vendor-comparability-parity-fix-adversarial.md`
- **Craft/vendor comparability parity fix - external review, fourth-site finding (2026-08-15)** - gate PASS 2026-08-16.
  `docs/archive/known-issues/2026-08-15-craft-vendor-comparability-parity-fix-external.md`
- **Timestamp date display (all user-facing timestamps gain dates)** - gate PASS 2026-08-16.
  `docs/archive/known-issues/2026-08-15-timestamp-date-display.md`
- **Recipe-ingestion bug class: missing schema-version parameter (2026-08-15)** - gate PASS 2026-08-16.
  Cited as: recipe-ingestion bug class.
  `docs/archive/known-issues/2026-08-15-recipe-ingestion-bug-class-missing-schema-version.md`
- **W4A: Total Cost section redesign (2026-08-15)** - gate PASS 2026-08-16.
  Cited as: W4A entry.
  `docs/archive/known-issues/2026-08-15-w4a-total-cost-section-redesign.md`
- **GuildUpgrade ingredient costing/display fix (2026-08-16)** - gate PARTIAL PASS 2026-08-16.
  Cited as: guildupgrade-ingredients fix, the corrected "Root cause" paragraph.
  `docs/archive/known-issues/2026-08-16-guildupgrade-ingredient-costing-display-fix.md`
- **AUDIT ROW 20/38: TP price-side fallback parity (2026-08-16)** - gate PARTIAL PASS 2026-08-16.
  `docs/archive/known-issues/2026-08-16-audit-row-20-38-tp-price-side-fallback-parity.md`
- **AUDIT ROW 56: daily craft-cooldown notices + three small fixes (2026-08-16)** - gate PASS 2026-08-16.
  `docs/archive/known-issues/2026-08-16-audit-row-56-daily-craft-cooldown-notices-three.md`
- **Currency UX package: defaults, plan-scope pills, value-detail hover (2026-08-16)** - gate PASS with one deferred slice 2026-08-16.
  `docs/archive/known-issues/2026-08-16-currency-ux-package-defaults-plan-scope-pills-value.md`
- **Decision-invariant "Value Own Materials" (VOM, 2026-08-16)** - gate PASS 2026-08-16.
  `docs/archive/known-issues/2026-08-16-decision-invariant-value-own-materials.md`
- **Plan Notes section: excess/reclaim, competency, forge scope (2026-08-16)** - gate PASS 2026-08-16.
  `docs/archive/known-issues/2026-08-16-plan-notes-section-excess-reclaim-competency-forge.md`
- **UI bundle: wiki links, snapshot status row, receipt/what-if captions (2026-08-16)** - gate MIXED 2026-08-16.
  `docs/archive/known-issues/2026-08-16-ui-bundle-wiki-links-snapshot-status-row-receipt.md`
- **Opportunity notes: recipe-sheet savings + seasonal vendor tips (2026-08-16)** - gate not yet run live.
  Cited as: RECIPE-SHEET SAVINGS entry.
  `docs/archive/known-issues/2026-08-16-opportunity-notes-recipe-sheet-savings-seasonal.md`
- **Source selection simplification: competency-aware default + subdued losing pills (2026-08-16)** - gate not yet run live.
  `docs/archive/known-issues/2026-08-16-source-selection-simplification-competency-aware.md`
- **Source selection simplification: adversarial-review fix round (8 findings) (2026-08-16)** - gate not yet run live.
  `docs/archive/known-issues/2026-08-16-source-selection-simplification-adversarial-review.md`
- **Source selection simplification: adversarial-review fix round 2 (5 findings) (2026-08-16)** - gate not yet run live.
  `docs/archive/known-issues/2026-08-16-source-selection-simplification-adversarial-review-2.md`
- **Gate investigation: receipt/what-if captions + value-detail hover (2026-08-16)** - gate investigation outcome recorded 2026-08-16.
  `docs/archive/known-issues/2026-08-16-gate-investigation-receipt-what-if-captions-value.md`
- **Shopping-list row tooltip: scope collision + swallowed hover (shoplist-have-format, 2026-08-16)** - gate not yet run live 2026-08-16.
  Cited as: shoplist-have-format.
  `docs/archive/known-issues/2026-08-16-shoplist-have-format.md`
- **Festival-vendor auto-tagging follow-up (2026-08-16)** - gate not yet run live 2026-08-16.
  `docs/archive/known-issues/2026-08-16-festival-vendor-auto-tagging-follow-up.md`
- **Recorded follow-ups batch sweep (2026-08-17)** - gate not applicable 2026-08-16.
  `docs/archive/known-issues/2026-08-17-recorded-follow-ups-batch-sweep.md`
- **High-evidence zones: policy rewrite + PlanContentHeightMath dead-code sweep (high-evidence-zones, 2026-08-17)** - gate PASS.
  Cited as: high-evidence-zones.
  `docs/archive/known-issues/2026-08-17-high-evidence-zones.md`
- **Value-detail hover investigation, pipeline-level follow-up (value-detail-pipeline, 2026-08-17)** - gate not run live this pass.
  `docs/archive/known-issues/2026-08-17-value-detail-pipeline.md`
- **Merged-ceil remainder: largest-remainder apportionment + display-layer narrowing fix (2026-08-17)** - gate not applicable 2026-08-16.
  `docs/archive/known-issues/2026-08-17-merged-ceil-remainder-largest-remainder.md`
- **Quality-audit cleanup, phase 1: four bug fixes (B1-B4, 2026-08-17)** - gate PASS 2026-08-17.
  Cited as: Quality-audit B1, Quality-audit B2, Quality-audit B3, Quality-audit B4, quality-phase1-bugs, the full quality-audit rationale.
  `docs/archive/known-issues/2026-08-17-quality-audit-cleanup-phase-1-four-bug-fixes.md`
- **Tree row tooltip composer extraction + architecture doc corrections (2026-08-17)** - gate not run live this pass.
  `docs/archive/known-issues/2026-08-17-tree-row-tooltip-composer-extraction-architecture.md`
- **Annotation-detection: post-solve advisory-list characterization tests + B8 shape fixes (2026-08-17)** - gate not run live this pass.
  `docs/archive/known-issues/2026-08-17-annotation-detection-post-solve-advisory-list.md`
- **Quality-audit phase 2: safe-mechanical batch (quality-phase2-mechanical)** - gate PASS.
  `docs/archive/known-issues/2026-08-17-quality-phase2-mechanical.md`
- **Comment-minimalism sweep (repo-wide, comment-minimalism-sweep branch)** - gate PASS.
  `docs/archive/known-issues/2026-08-17-comment-minimalism-sweep.md`
- **Quality-audit phase 3: structural dedup (quality-phase3-dedup)** - gate PASS.
  `docs/archive/known-issues/2026-08-17-quality-phase3-dedup.md`
- **Quality-audit phase 4a: PlanSolver best-recipe tracker (B9, quality-phase4a-tracker)** - gate PASS.
  `docs/archive/known-issues/2026-08-17-quality-phase4a-tracker.md`
- **Quality-audit phase 4b: pure parameter bundling (B10, quality-phase4b-bundling)** - gate PASS.
  `docs/archive/known-issues/2026-08-17-quality-phase4b-bundling.md`
- **Desktop gate batch: value-detail closure + partial currency coverage (2026-08-17, orchestrator live session)** - gate: see record.
  `docs/archive/known-issues/2026-08-17-desktop-gate-batch-value-detail-closure-partial.md`
- **Backlog cleanup batch (B8/B11/B13/B14/B15 + solver ctor hardening, backlog-cleanup)** - gate PASS 2026-08-17.
  `docs/archive/known-issues/2026-08-17-backlog-cleanup.md`
- **Final test polish: three Nice to Have items (final-test-polish)** - gate PASS.
  `docs/archive/known-issues/2026-08-19-final-test-polish.md`
- **Log + Snapshot UX: three small items (log-snapshot-ux)** - gate PASS 2026-08-19.
  `docs/archive/known-issues/2026-08-19-log-snapshot-ux.md`
- **Per-character source checkboxes + character-name search (char-source-search)** - gate PASS on items 2026-08-19.
  `docs/archive/known-issues/2026-08-19-char-source-search.md`
- **Nice to Have batch (nth-cleanup)** - gate PASS 2026-08-19.
  `docs/archive/known-issues/2026-08-19-nth-cleanup.md`
- **Character-name search minimum query length (char-search-min2)** - gate PASS 2026-08-22.
  Cited as: char-search-min2.
  `docs/archive/known-issues/2026-08-22-char-search-min2.md`
- **Audit batch I: log entry readability (audit-i-log)** - gate PASS 2026-08-22.
  `docs/archive/known-issues/2026-08-22-audit-i-log.md`
- **Audit batch F: input flow (audit-f-input-flow)** - gate PASS 2026-08-22.
  `docs/archive/known-issues/2026-08-22-audit-f-input-flow.md`
- **Audit batch G: Settings restructure (audit-g-settings)** - gate PASS 2026-08-22.
  `docs/archive/known-issues/2026-08-22-audit-g-settings.md`
- **Audit batch K: Plan Notes wrapping (audit-k-notes)** - gate PASS 2026-08-22.
  `docs/archive/known-issues/2026-08-22-audit-k-notes.md`
- **Audit batch E: tree interaction honesty (audit-de-cost-tree)** - gate PASS 2026-08-22.
  `docs/archive/known-issues/2026-08-22-audit-de-cost-tree.md`
- **Audit batches A+B+C tier 1 (audit-abc)** - gate PASS 2026-08-23.
  Cited as: "Audit batches A+B+C tier 1".
  `docs/archive/known-issues/2026-08-22-audit-abc.md`
- **Audit batch H: table density (audit-h-density)** - gate PASS 2026-08-23.
  `docs/archive/known-issues/2026-08-22-audit-h-density.md`
- **Tooltip facility (tooltip-facility)** - gate PASS after one gate-found Critical was fixed and re-gated 2026-08-23.
  Cited as: "Tooltip facility", tooltip-facility.
  `docs/archive/known-issues/2026-08-22-tooltip-facility.md`
- **Audit batch J: consistency sweep (audit-j-consistency)** - gate PASS 2026-08-23.
  `docs/archive/known-issues/2026-08-22-audit-j-consistency.md`
- **Cost band restyle (cost-band-restyle)** - gate PASS 2026-08-23.
  Cited as: "Cost band restyle", cost-band-restyle.
  `docs/archive/known-issues/2026-08-23-cost-band-restyle.md`
- **Field-test fixes wave 1 (field-fixes-1)** - gate PASS after one gate-found fix 2026-08-23.
  `docs/archive/known-issues/2026-08-23-field-fixes-1.md`
- **Sortable plan tables (sortable-tables)** - gate PASS 2026-08-23.
  Cited as: "Sortable plan tables", sortable-tables.
  `docs/archive/known-issues/2026-08-23-sortable-tables.md`
- **Minimum width raise (min-width-1436)** - gate PASS after two gate-found fixes 2026-08-23.
  Cited as: "Minimum width raise", min-width-1436.
  `docs/archive/known-issues/2026-08-23-min-width-1436.md`
- **Settings dirty prompt (settings-dirty-prompt)** - gate PASS 2026-08-23.
  Cited as: "Settings dirty prompt", settings-dirty-prompt.
  `docs/archive/known-issues/2026-08-23-settings-dirty-prompt.md`
- **Spinner and button feedback (spinner-feedback)** - gate PASS 2026-08-24.
  `docs/archive/known-issues/2026-08-23-spinner-feedback.md`
- **Snapshot item grid (snapshot-grid)** - gate PASS 2026-08-24.
  Cited as: "Snapshot item grid".
  `docs/archive/known-issues/2026-08-23-snapshot-grid.md`
- **Item stat tooltips (item-stat-tooltips)** - gate PASS 2026-08-24.
  Cited as: "Item stat tooltips", item-stat-tooltips.
  `docs/archive/known-issues/2026-08-23-item-stat-tooltips.md`
- **Font bump and decision-round polish (font-and-polish)** - gate PASS 2026-08-23.
  Cited as: "Font bump and decision-round polish", font-and-polish.
  `docs/archive/known-issues/2026-08-23-font-and-polish.md`
- **Tooltip authenticity (tooltip-authenticity)** - gate PASS 2026-08-23.
  Cited as: "Tooltip authenticity", tooltip-authenticity.
  `docs/archive/known-issues/2026-08-23-tooltip-authenticity.md`
- **Keyboard focus release (kb-focus-release)** - gate PASS 2026-08-23.
  `docs/archive/known-issues/2026-08-23-kb-focus-release.md`
- **Root ignore suppression and the zero-cost band (root-ignore-summary-zero)** - gate PASS with recorded partials 2026-08-23.
  `docs/archive/known-issues/2026-08-23-root-ignore-summary-zero.md`
- **Click volume slider (click-sound-gain)** - gate PASS on the render half 2026-08-23.
  Cited as: "Click volume slider", click-sound-gain.
  `docs/archive/known-issues/2026-08-23-click-sound-gain.md`
- **Plan-view redesign (plan-view-redesign)** - gate PASS 2026-08-24.
  Cited as: "Plan-view redesign", plan-view-redesign.
  `docs/archive/known-issues/2026-08-23-plan-view-redesign.md`

---

## Append zone

Everything below is a milestone record that has not rotated yet - un-gated,
or gated but with follow-ups still open. New sections append here, at the
true end of the file. See "How this file works" above.

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
