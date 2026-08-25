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
  Cited as: "Tooltip authenticity", tooltip-authenticity; carries the
  accepted divergence from the game's "Unused Infusion Slot" wording that
  `Services/ItemStatTooltipComposer.cs` points here for.
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
## Seed integrity: the reseeder silently deleted hand-authored recipes (seed-integrity)

Found while investigating the maintainer's "Gift of Rays shows UNKNOWN"
report, by running the RecipeSeeder into a scratch directory and diffing
its output against the shipped seed rather than trusting either.

**The defect.** `ref/recipes_seed.json` ships 1,595 negative-id recipes:
1,591 Mystic Forge rows regenerated from `ref/mystic_forge_recipes.json`,
and **four synthetic rows that exist in no source file at all** - ids
-1592..-1595, the Infinite Trebuchet Blueprint chain, carrying
`disciplines: ["Merchant"]` / `["Achievement"]` and per-ingredient
`achievementId`/`achievementBit` metadata. Nothing regenerates them: the
official API serves no negative ids, and the forge source file holds forge
rows only. A reseed therefore wrote 14,962 recipes where the shipped seed
has 14,966, deleting all four without a word.

This is the same defect class as the `expectedOutputCount` overrides that
`MergeMysticForgeRecipes` used to drop on every reseed (see its own comment):
hand-authored data that survives only until someone regenerates.

**A wrong fix, rejected.** The obvious move - backport the four rows into
`mystic_forge_recipes.json` - was implemented, then abandoned after
diffing: that file's merge path forces `disciplines: ["MysticForge"]` and
carries no achievement fields, so it would have silently reclassified a
Merchant row as a forge recipe and dropped the achievement metadata. A
smaller diff that corrupts data is worse than no fix.

**The fix taken.** Step 5a in the seeder reads the seed file it is about to
overwrite and carries forward every negative-id recipe that the regenerated
set does not contain, verbatim, adding its search entry. Preservation is now
structural rather than something a future maintainer has to remember, and it
covers any future hand-authored row, not just these four.

**Verified empirically** (the tool has no test project; this is the honest
check): with the fix, a full reseed against build 205780 reproduces
`recipes_seed.json` byte-identically, and all four rows keep their
disciplines and achievement metadata. The refreshed seed is committed:
search entries 16,022 -> 16,024 and item names 14,762 -> 14,766 (the four
outputs are now locally named, so the Trebuchet chain autocompletes and
renders its icons offline), manifest build 205,505 -> 205,780. Both count
pins in the test suite moved with a comment saying why.

**Note for the UNKNOWN investigation** (branch field-fixes-3 owns the code
rule): this reseed also aligns the seed's build id with the live one, so the
"seed negative entries will fall back to API" condition stops firing today.
That is an alignment, not a fix - the next game build breaks it again, which
is exactly why the durable fix is the fallback rule itself.

Gate: not required - dev-tool and data change; the module's behavior is
covered by the suite (2564 green) and the byte-identical round trip above.
No `.bhm` content changes except the four newly-named items.

## Zero-band retention, scroll anchoring, click default, MF recipes, first-load snapshot (field-fixes-3)

Five independent maintainer reports from one live 0.2.3 session.

### 1. The zero band keeps its cells even when a term is unmeasured

"The Total Cost section can make it ONLY display the 'Actual Cost to
Craft' section if everything in the recipe tree that requires
ingredients or purchases ends up ignored. It should retain the other
cells in the section and just show them with 0s rather than making them
disappear."

This overrides the v0.2.2 rule recorded in the
`root-ignore-summary-zero` section above, which kept the collapsed lone
tile whenever `PlanViewModelBuilder.HasUnpricedNode` was true. The
argument for that (a zero nobody measured must not be dressed as a
priced equation) was sound about the NUMBER and wrong about the
MECHANISM: real plans routinely carry UNKNOWN nodes (Globs of Dark
Matter, account-bound gifts), so the maintainer met the collapsed band
constantly, and a section that drops cells reads as broken rather than
as careful.

The honesty moved into text that states it. On an unpriced zero the band
renders all three tiles at 0 and:

- every tile caption takes `PlanViewModelBuilder.UnpricedTileMarker`
  (`*`),
- every tile tooltip takes `UnpricedTooltipSuffix` ("... could not be
  priced and count as 0, so this figure is a floor rather than a
  measured total"),
- the section grows a second `SummaryFootnote` row,
  `UnpricedFootnoteText`, above the standing Trading Post line. The
  renderer already drew every footnote row it was handed and
  `SummarySectionLayoutMath.BodyHeight` already counted every one, so
  neither needed a change.

A measured zero therefore still reads differently from an unmeasured
one - no marker, one footnote. `HasUnpricedNode` is now walked ONCE per
section build, in `BuildSummarySection`, and still only from the
zero-cost gate.

Two things deliberately did NOT change. The **profit band** still
suppresses entirely on an unpriced zero: its tiles would not be zeros at
all - an unmeasured 0 materials value makes "Profit if Sold" the entire
sale price, a large invented number no footnote makes safe. And **Free
mode with owned materials consumed** still collapses to the lone tile
(the middle term is absent, not zero - see the older section's
known-vs-absent-zero paragraph).

The profit band's own absence is nonetheless the same complaint one band
lower - the maintainer's state ("only the Actual Cost to Craft section")
is a Total Cost section with the cost band collapsed AND no Sell
Value / Profit tiles - so it is now accounted for in text rather than
left as two missing cells: a third `SummaryFootnote` row,
`ProfitSuppressedFootnoteText`, added on exactly the condition
`BuildProfitFormulaBand` returns on (`NetSaleValue.HasValue`), so it
never claims a band was hidden on a plan that had no sell price to show
one for. The multi-item `MultiItemNote` row ("sell value and profit are
the sum across every requested item ...") is gated on the same
condition for the same reason - it used to scope two tiles that were not
on the page.

### 2. Scroll anchoring across a re-solve

"When you toggle IGNORE on stuff in the recipe tree - it can adjust
which currencies show up in the Total section at the top and cause the
current location under you to 'move' as the view reflows."

`CraftingPlanView.PreserveScrollAcross` preserved the scroll OFFSET,
which holds the view still only while the content ABOVE the viewport
keeps its height. A re-solve changes it - the Total Cost currency table
gains or loses rows - so the same offset now points at different
content.

`Services/ScrollAnchorMath` (Blish-free) is the whole decision:
`AnchorLine` (the cursor's content-space line when the cursor is over
the panel, else the viewport top), `TryCapture` (the lowest-starting
candidate at or above that line, ties to the shortest - so a tree row
wins over the section containing it, with no nesting description
needed), `FindTop`, and `RestoredOffset` (old offset plus how far the
anchored element moved, clamped; the cursor cancels out of the
arithmetic and only decides WHICH element is anchored).

The view stays a thin shim: a `_scrollAnchors` key -> control registry
(sections keyed by `PlanSectionType` in `CreateSectionHeader`, tree rows
keyed by solver `NodeId` through a new optional
`TreeSectionController` delegate - the same identity that class's own
in-place row pairing already trusts across a re-solve), a parent walk to
content space that skips anything invisible or no longer under the
content panel, and `GameService.Input.Mouse` for the cursor. An anchor
that no longer exists after the rebuild (its subtree was ignored away)
falls back to the previous offset-only restore rather than jumping to
where a missing row "would" be.

### 3. Click sound default 75 -> 35

The field test that the click-volume section above was waiting on
returned: "I found 35% or so is a reasonable click default volume."
`ClickSoundVolume.DefaultPercent` is now 35 - 1.75x Blish's 0.2 fixed
volume (+4.9 dB), 0.875x its 0.4 absolute ceiling (-1.2 dB), putting the
asset's own 0.357 peak at -18.1 dBFS. The setting is persisted, so a
user who already moved the slider keeps their value; only the default
changed. That section's dB derivation and the test that pinned "louder
than the old ceiling" (true of 75, not of 35) were both restated.

### 4. UNKNOWN Mystic Forge gifts - measured: not the build bump, and mostly not a defect

Report: "Gift of Rays, Gift of the Survivors and Gift of the People and
Gift of the Hylek all show UNKNOWN in the recipe tree", alongside the
per-generation log line "Recipe seed built for build 205505; current
build 205780; seed negative entries will fall back to API."

**That log line is not about negative recipe ids.** "Negative entries"
there means negative CACHE rows - `SeededRecipeCacheStore.TryGetSearch`
invalidates an entry only when it is an EMPTY list AND the seed build
differs. Synthetic Mystic Forge ids were never touched by it.
`MysticForgeSeedStalenessTests` proves this several ways, including one
test that plans item 107040 (Gift of Rays) through the REAL shipped
`ref/` seed files with the seed build bumped and an API client that
404s the MF id: it still crafts.

Measured data state, audited directly against `ref/`: all **1591**
recipes in `ref/mystic_forge_recipes.json` are already present in
`ref/recipes_seed.json`, and every one of their output items already has
a non-empty `ref/recipe_search_seed.json` row that already lists that MF
id (0 missing on each count). 107040 -> `[-1587]` is one of them.

**The parent plan is Endless Summer (107022)**, named by the
maintainer's own `module_log.jsonl` and `plan.json`, not Orrax: 107040
is an ingredient of -1586, and the other three gifts are its siblings.
`MysticForgeSeedStalenessTests.RealShippedSeed_EndlessSummerGifts_...`
plans that exact parent through the REAL shipped seed under a build
bump: **Gift of Rays resolves to Craft on recipe -1587**. The reported
UNKNOWN for it was not reproduced by any route. The maintainer's own
persisted `plan.json` from that session carries
`IgnoredItemIds: [107040]` and renders the row `IsIgnored` - the IGNORE
pill is offered on every non-root item row, so an ignored row is not
evidence of an UNKNOWN one, and an ignored row reads IGNORED, not
UNKNOWN.

The other three are not a recipe-lookup failure either - each has an
EMPTY search row in the seed (the seeder's "the API knows no recipe for
this item") and the wiki confirms none exists - but calling them
"correct UNKNOWNs" was wrong. **The module ships a vendor offer for all
three and silently discards it.** Audited directly against
`ref/vendor_offers.json` (and against the unpacked shipped 0.2.3 `.bhm`,
which carries the same rows): 106712 -> Castaway Agnes (Hullgarden),
paid in items 105848 + 106467 + 106370 and 500 of currency 83; 105804 ->
Canach (Breezy Cay), items 105933 + 106672 + 106627 and 500 of currency
81; 106986 -> Palak (Hullgarden), 250x item 19717, 300,000 karma and 200
copper. None carries a seasonal tag, so `SeasonalOfferFilter` is not
involved.

The mechanism is `VendorBatchSolver.EvaluateVendorOffers`: an `Item`
cost line whose item has no Trading Post price sets `priceable = false`
and discards the WHOLE offer. These are barter offers paid in
account-bound tokens, which can never have a TP price, so every offer
for the node is discarded, nothing comparable and no fallback survives,
and `PlanSolver` commits `UnknownSource` - exactly what the maintainer's
`plan.json` records (Source `UnknownSource`, RecipeId 0, for all three).
`AcquisitionHintSeedVendorAgreementTests.ShippedBarterOffer_...` proves
it on the real solver path with the real shipped offer.

**Class size, counted from `ref/vendor_offers.json`:** 17,802 of 53,544
offers carry an `Item` cost line, and 4,664 of the 14,965 distinct
output items have NO offer that is free of one. Every one of those
renders a bare UNKNOWN while the module holds the merchant and location
on disk. **Follow-up (not done in this wave):** when every offer for a
node is discarded as unpriceable, surface that offer's merchant and
location on the node itself, instead of hand-typing them into a second
file. That would cover the class; the three hints below cover only the
three items the report named.

So the fix for these three is the pre-existing acquisition-hint
mechanism (`CraftingTreeBuilder.ApplyAcquisitionHint`, unchanged), with
two corrections from review. **They break the precedent the other seven
hints set**: all seven pre-existing entries (71994, 70698, 70797, 71943,
74528, 19678, 43772) are for items with ZERO vendor offers, so the
mechanism's implicit contract was "no source anywhere in our data".
These three each have exactly one offer, and 106986's first draft
contradicted it (it said "Shaman Palak in Shipwreck Strand"; the record
says Palak, Hullgarden). The vendor record is now the authority: each
hint names only the merchant and location that record carries, makes no
cost claim at all, and adds only what the record has no field for (the
achievement, "not craftable", and why the row still counts as 0).
`AcquisitionHintSeedVendorAgreementTests` pins that agreement against
both real files and trip-wires on a fourth hinted item gaining an offer,
so a re-scrape that relocates a merchant now fails a test instead of
showing two answers. Their names come from the item API, so their
absence from `ref/item_name_seed.json` never mattered.

The badges are `MERCHANT` / `MERCHANT` / `ACHIEVEMENT`, not `VENDOR`: a
`VENDOR` badge was byte-identical to the single-source VENDOR pill
(`DecisionPillPlanner`), which means the opposite thing - a priced
purchase inside `Plan.TotalCoinCost` rather than an Unknown node
contributing 0 - and on this very plan would have sat directly above
item 1's new "no recipe and no Trading Post price ... count as 0"
footnote, arguing with it. `DecisionPillPlanner` now drops any badge
equal to a module-owned source badge back to `UNKNOWN`, so the seed
cannot reintroduce the collision. The sweep found one sibling with the
same defect - `ShoppingSourceBadge.ForRow`, which renders the same
seeded badge on a Shopping List `ShoppingUnknown` row next to real
`TP`/`VENDOR`/`CURRENCY` rows - and it now shares the one predicate. The
hint TEXT still reaches the tooltip in both places, so the guard costs
nothing but the misleading badge.

`SeededRecipeCacheStore.MergeMysticForgeRecipes` is kept but is a
**measured no-op on today's data** - every iteration hits
`existing.Contains(recipe.Id)` and re-stores an equal recipe - and must
not be read as the explanation for any of the four reports. What it buys
is future-proofing in the direction the seed is weak: a seed row saying
"the API knows no recipe" is an EMPTY list served as a cache HIT, so
nothing consults `MysticForgeRecipeData`, and a recipe added to the
wiki-sourced forge file without re-running the seeder would be invisible
(and, via the stale-seed fallback that turns an empty row into an API
call `CompositeRecipeApiClient` rescues, its visibility would depend on
the live game build id - nonsense for wiki data). With the merge, adding
a forge recipe to `ref/mystic_forge_recipes.json` alone is enough.
Genuine API-sourced staleness still falls back exactly as before.

### 5. First-load snapshot

"When you load for the first time we should trigger a snapshot
immediately to try to fetch your API inventory stuff otherwise
everything is empty and requires either I assume waiting 10mins OR
clicking manually to pull a snapshot."

An install with nothing cached had no automatic route to its first
snapshot at all - not a slow one. `Module.LoadAsync` fetches only when
Blish has already granted the subtoken (usually it has not at that
point), and the `SubtokenUpdated` handler that would cover the late
grant is attached on the very next line, so an event that fired first is
missed. `Update()`'s interval refresh then returns on
`_currentSnapshot == null` every tick forever.

`Update()` now runs a one-shot first-load fetch through the existing
`RefreshSnapshotInBackgroundAsync` (spinner, status text and failure
classification unchanged). `Services/FirstLoadSnapshotGate` holds the
rule Blish-free: fire once, only with the API ready and no refresh or
post-failure backoff in the way - and a blocked tick does NOT spend the
one shot, so a module whose key arrives late still gets its fetch.
`Module.IsInRefreshFailureBackoff` was extracted so the gate and the
refresh itself read the same window. `Views/MainView.cs` is untouched.

Two follow-ups from review, both in that branch of `Update()`.
**Clear Cache re-arms the shot** (`_firstLoadRefreshAttempted = false`
inside the `SnapshotCommitGate.Clear` callback, alongside the other
field resets): an in-session clear recreates exactly the nothing-cached
state the shot exists for, and without the reset it left the tab with no
automatic route to a snapshot until Blish restarted. And **that branch
no longer probes the API manager per frame**. Both of the gate's inputs
are live readings evaluated as arguments -
`_snapshotService.HasRequiredPermissions()` (`Gw2ApiManager.HasPermissions`
takes an `IEnumerable`, so an enumerator allocation) and
`IsInRefreshFailureBackoff()` (a `UtcNow` read) - and the branch itself
is reached every frame for as long as nothing is cached, which with no
API key configured is the whole session. Checking the spent flag at the
call site only silences it AFTER the shot is used, and the shot is never
used in exactly that case, so the flag alone left the probe running at
frame rate on the UI thread. `FirstLoadSnapshotGate.ShouldCheckNow` is
the second guard: a Blish-free frame-delta accumulator that lets the
gate be re-evaluated once every two seconds instead. It is seeded full
(and reset the same way by Clear Cache) so an armed shot is still
checked on the very next tick, and the coarse interval costs no first-
load latency - a granted subtoken reaches the fetch through
`OnSubtokenUpdated`; this poll is only the backstop for the grant that
fired before the handler was attached.

### Desktop gate

1. **Zero band, unmeasured**: plan an item with no recipe and no
   Trading Post price (root row reads UNKNOWN), or any plan carrying an
   UNKNOWN node whose total comes to 0. The Total Cost band shows all
   THREE tiles at 0 with the "-"/"=" operators and the result
   highlight, each caption ending in `*`, a footnote line above the
   Trading Post line explaining the `*`, and the `*` explanation again
   in each tile's hover text. No profit band renders on such a plan -
   and when the target itself has a Trading Post sell price, a SECOND
   footnote line says so ("Sell Value and Profit if Sold are hidden
   here ..."), so no cells go missing without an explanation on screen.
2. **Zero band, measured**: with "Use Own Materials" OFF, ignore every
   child until the plan costs nothing. Same three tiles at 0, but NO
   `*` on any caption and only the one Trading Post footnote - a
   measured zero must still look different from an unmeasured one.
3. **Scroll anchoring**: generate a deep plan (a legendary), scroll
   into the middle of the recipe tree, put the cursor ON a row, and
   click IGNORE on a row at or below it - the gesture that used to jar.
   The row under the cursor stays under the cursor while the Total Cost
   section above gains or loses currency rows. Repeat with the cursor
   off the panel (click IGNORE via a row further down, then move the
   mouse away first): the topmost visible row holds instead. Also
   collapse/expand a section header and a tree caret - neither should
   jump.
4. **Click default**: on a profile that has never touched the slider
   (or after Clear Cache / a fresh install), Settings shows **35%** and
   the click is audible but unobtrusive. On a profile that HAS moved
   the slider, the old value is still there - the default change must
   not overwrite it.
5. **Mystic Forge**: search for and plan **Endless Summer** (the exact
   item to search - it is the parent the report came from). First clear
   the carried-over ignore: **Gift of Rays** will show the IGNORED pill
   from the 0.2.3 session, so click it to un-ignore. Its row must then
   read CRAFT, not UNKNOWN, and expand into Gift of the Sun, Gift of
   the Beach, Gift of Infused Gems and Purified Rift Essence. Gift of
   the Survivors, Gift of the People and Gift of the Hylek are NOT
   craftable (barter purchase / achievement - verified on the wiki and
   against the shipped vendor offer), so they correctly stay
   uncraftable, but their pill must now read **MERCHANT**, **MERCHANT**
   and **ACHIEVEMENT** instead of UNKNOWN, with the merchant name and
   location in each row's hover text - and NOT "VENDOR", which is the
   priced single-source pill and must not appear on a row the Total Cost
   footnote simultaneously calls unpriced.
6. **First-load snapshot**: with a valid API key set, clear the cached
   snapshot (Clear Cache) and restart Blish. The Snapshot tab starts
   fetching by itself within a few seconds - spinner visible, then real
   inventory - with no click and no 10-minute wait. Then, WITHOUT
   restarting, press Clear Cache again: a fresh fetch must start by
   itself the same way (the one shot is re-armed by the clear). Then
   repeat with the API key removed: the tab stays empty with its normal
   status, the log shows no repeating fetch attempts, and adding the key
   mid-session starts exactly one fetch.

Gate: PASS with two recorded partials (2026-08-25 desktop session,
branch build merged with master, captures preflight/gFX1-gFX8,
gAN1-gAN3, gFL1-gFL2, gMF1; display-sleep inhibitor held the session).

1. ZERO BAND: live PASS on the known-zero case - crafting Green Wood
   Dowel and ignoring its only child rendered the full three tiles
   ("Total Materials Value 0c - Your Materials Used 0c = Actual Cost to
   Craft 0c") with the profit band beside it, where the old build showed
   a lone tile. PARTIAL: the unpriced-zero variant (marker + footnote)
   could not be reached live - it needs a zero total produced by an
   unpriceable node, and an unpriceable item has no recipe, so it is
   absent from the craftable search index and cannot be planned
   directly. Pinned by the round's discriminator tests instead.
2. SCROLL ANCHORING: live PASS, measured rather than eyeballed. Scrolled
   deep into an expanded 154-node Wupwup tree (Total Cost off-screen
   above), clicked IGNORE on "3x Deldrimor Steel Ingot" with the cursor
   resting on it: that row and every one of the eight rows above it held
   their EXACT pixel y across the re-solve, while the row's own subtree
   collapsed and new content flowed in below. This is the maintainer's
   reported gesture, and the jar is gone.
3. CLICK DEFAULT: ClickSoundVolume.DefaultPercent is 35. Live check
   confirmed the other half of the contract - a persisted 78 from an
   earlier session survived the change, so only new installs take 35.
4. MYSTIC FORGE UNKNOWN: live PASS on the maintainer's own report -
   "Gift of Rays" now generates a complete plan (legendary-purple
   header, 892g 88s 38c, eight currency requirements listed) where it
   previously rendered UNKNOWN. Note the orchestrator's stale-build lead
   was DISPROVEN by this round's tests; the defect actually fixed was
   the empty-seed-row cache hit, and this is its live proof.
5. FIRST-LOAD SNAPSHOT: PARTIAL by environment. With the cached snapshot
   deleted and a fresh launch, the module correctly did NOT fetch - the
   sandbox has no API key and the status read "GW2 API access not
   ready". The half that IS verifiable passed and was the actual risk:
   the module log stayed EMPTY across the whole session, so the blocked
   gate neither spends its one shot nor re-probes per frame. Firing on a
   real key belongs to the maintainer's install.

FOLLOW-UP within the same session (maintainer, watching the Gift of Rays
capture): "Actual Cost to Craft is all that that screen is showing... it
should have 3 fields". The band still collapsed whenever the middle term
was zero - the ORIGINAL "no middle term to subtract" rule, older than
this round's zero-plan work, and the ordinary case for any plan whose
materials you do not already own. Rule deleted: all three tiles now
always render. The one genuinely unmeasured case (Free mode with
materials consumed, where MaterialOpportunityCost is null by contract)
keeps its 0 and moves the "nobody priced these" fact into the tile's own
tooltip, with a test asserting the two zeros do not read identically.
Eight tests pinned the old contract and moved with it. Live re-verified
on the same plan: "Total Materials Value 895g 13s 39c - Your Materials
Used 0 = Actual Cost to Craft 895g 13s 39c".

Bonus verified in passing: the seed-integrity fix from PR #176 works end
to end - "Pile of Recycled Trebuchets" now autocompletes with its icon
and resolves through VENDOR at 1940g, where its chain was previously
absent from the local name seed.

## App-wide UI consistency wave (app-typography)

Branched from v0.2.3, which redesigned the Crafting Plan tab alone. This
wave carries that work to the REST of the module, on five maintainer
directives. It deliberately reuses the 0.2.3 seams rather than growing
new ones: `Services/TypeRampMetrics` for the tier seats,
`Views/Rendering/UiFonts` for the fonts, `PlanContentHeightMath` for the
band heights those tiers need, `PlanRelayoutMath`'s pinned-right-edge
model for every column, and `IconControls` / `TooltipFacility` for icons
and hovers.

### A - the ramp is the whole app's, not the plan tab's

`UiFonts.Title` (18 regular) is **deleted**. It was accepted divergence
5 of the plan-view redesign, kept alive only because restyling Settings
and About was "a second redesign"; this is that redesign. Nothing in the
module resolves 18-regular any more, so the measured defect behind its
retirement - the space glyph advances 4px against 7 at 16-regular and 9
at 18-bold, so multi-word text renders with collapsed word gaps - can no
longer reach the screen through a tier seat. Every promoted role is bold
for that reason, not for a stylistic one.

Placed, each at the band height its tier's measured ink needs:

| Surface | Was | Now |
|---|---|---|
| "Account Snapshot", Settings' five section headers, About's title and its two labelled sections | Title 18 regular / Body 16 | **SectionTitle 24 bold**, 38px band, 2px rule |
| Settings' currency grid header, the Log tab's rows (previously unlabelled), the Snapshot tab's two runs | Body 16 / nothing | **ColumnHeader 20 bold** on `TableHeaderStyle.BandColor`, 32px band |
| Snapshot, Log and Settings status lines | Body 16 | **Status 18 bold** |

Two rows grow because of it: `SnapshotHeaderLayout.StatusRowHeight` and
the Log tab's own, 24 -> 26 (the Status tier's lowest ink is 23 against
Body's 21, drawn at y=2 with the 1px clearance the row has always kept).
A test asserts the clearance against the measured ink rather than
against the literal, so a future tier swap is told which number to write.

**The no-small-grey rule reached one offender outside the plan**: the
Snapshot coin caption was Caption 14 in #828282, smaller AND greyer than
the figure it labels. It keeps the grey and joins the coin run's own size
and y - one channel of de-emphasis, the same fix the plan's Disciplines
line got.

The Log tab's new header names its prefix column **"Time"**. Judgment
call: the column actually holds level, timestamp and tag, and "Time" is
what a reader scans it for; "Level / Time / Tag" is honest and reads as
a legend rather than a header.

### B - one item-icon component

Reported: *"item icon displays are not using a standardized code path
everywhere.. some places give the tooltips, others do not, some places
give colored borders, other places just use the icon image with no
border"*. The inventory, taken at v0.2.3:

| Site | Frame | Hover |
|---|---|---|
| Recipe Tree, Used Materials, Shopping List, Crafting Steps, Required Recipes, plan header, rich tooltip header | rarity | tree/materials/shopping: yes. **Crafting Steps, Required Recipes: none** |
| Snapshot item rows, Snapshot wallet rows, item-search dropdown, Total Cost currency table | **none** | items: rich. **wallet, dropdown: none** |
| Inline coin/currency runs (`CoinCurrencyRenderer`) | none | currency: name. **coin: none** |

`IconControls.CreateItemIcon` IS the framed builder now (it was
`CreateRarityFramedIcon`), it takes the icon's hover text, and it stamps
that text on the frame as well as on the square and the missing-art
placeholder inside it - Blish resolves a tooltip on the deepest control
under the cursor and never bubbles, so each was its own hole.
`CreateUnframedIcon` is the one remaining unframed path, named so it
cannot be picked by accident.

Every site above is routed through it except two, both deliberate and
both stated here rather than left to be re-discovered:

- **the inline coin/currency runs stay UNFRAMED** (they still go through
  the component - see the review fixes below). A frame adds 2px to every
  segment's advance, and that advance is a term in the module's own
  minimum-window-width derivation; it would also draw a rarity border
  around a denomination that has no rarity.
- **the About tab's module icon.** It is the module's logo, not an item.

Each converted site passes its art size INSET by the border rather than
growing its box, so no layout arithmetic moved anywhere. Crafting Steps
and Required Recipes gained `ApplyPlainToIconTree`, so their row note now
covers the biggest target on the row.

**Snapshot rarity comes from the session stat cache**, which is the only
source that tab has: `AccountSnapshot` carries no rarity and is
schema-guarded against gaining fields (see `ItemStatBlock`'s own note on
why stats are a session side channel). A row whose block has not been
fetched frames NEUTRAL rather than guessing, and picks up its colour on
the next rebuild. Consequence, stated plainly: on a fresh session most
Snapshot frames are the unknown-rarity grey, and the uniform treatment -
not the colour - is what this directive buys there.

### C - the Snapshot tab's layout

Reported: *"the snapshot tab for sure needs the layout overhaul because
it has no tooltips and the same icon issue"*.

The results were a grid of unlabelled two-line cards. They are two
sortable tables now, each with a SectionTitle band and rule ("Items",
"Currencies") and a ColumnHeader band under it. Both bands span the full
grid and track the panel; a run with no rows is ABSENT, not an empty
heading over nothing.

- **The header band carries one label pair per grid column**, on the same
  x's as the cells beneath it. The Settings currency grid already labels
  a multi-column grid this way; the alternative labels columns two and
  three with nothing.
- **The count is a column, not a prefix.** "30x Mystic Clover" became
  `Mystic Clover ... 30x` with the amount right-pinned, because a
  quantity a reader can SORT by has to line up down the column rather
  than move with each name's length. The name is the only part of a cell
  that flexes, its budget stops at the Amount band, and the band is
  `max(widest amount, its own header label)` - the header-floored rule
  the plan tables needed once headers went to 20 bold.
- **`MinColumnWidth` is re-derived term by term** for the new cell:
  40 icon column + 45 chars x 9px + 12 gap + 79 amount floor + 8 pad =
  **544** (was 516 for a 52-char name-plus-prefix run). Two columns still
  fit the 1252px grid the 1378px window minimum leaves; a third now needs
  a 1758px window rather than 1674.
- **Sorting** goes through the existing `TableSortState` cycle
  (asc -> desc -> back to the search's own order), one state per run,
  session-sticky across a tab switch, in a Blish-free
  `SnapshotTableSorter` shaped like `PlanTableSorter`. A click re-PLACES
  the rows it already has rather than rebuilding them - see the review
  fixes below.
- **Hovers.** Wallet rows had none at all and now carry the currency name
  on the panel, the name label and the icon. Item rows had the deferred
  rich path already but showed NOTHING for the common case - see the
  "Follow-up: snapshot rows without plan-cached stats" note above - and
  now always head with the item's name and always carry the full source
  breakdown. **The on-hover metadata fetch that note offers as the other
  candidate fix was NOT taken**: it is a network call on a hover path,
  and snapshot fetch triggering belongs to a sibling branch.

Blish-free and tested: `SnapshotResultLayout` stacks the two sections
(the view writes every y itself rather than betting on a FlowPanel
re-flowing a later sibling - the reason the two runs already shared one
panel), `SnapshotTableSorter` holds the comparators, and
`SnapshotItemGridLayout` grew the cell's own column edges.

### D - sortable headers are cells, not text

Reported: *"the header rows of columns that you can click to sort should
highlight lightly when you mouse over them to show that an action can be
triggered from them. also the tooltip should probably and click action
should probably trigger for mouseover of the entire column header cell,
not just the text"*.

`Views/Rendering/SortableHeaderCells` owns both halves for every sortable
table in the module. The mechanism is measured against decompiled Blish
HUD 1.3.0 rather than assumed, and it is the same finding
`PressFeedback` already records: `Container.TriggerMouseInput` raises the
CONTAINER's own mouse events first and only then walks its children, and
`Control.CheckMouseLeft` clears `MouseOver` only when the cursor leaves
that control's own bounds. The header row panel therefore sees every
move, press and click inside the band INCLUDING those over its labels,
and the cell under the cursor follows from `RelativeMousePosition`. The
wash panels are passive scenery; every handler lives on the row.

Two things do not follow from that:

- the "click to sort" note is stamped on the label AND on the cell's
  wash, because a tooltip resolves on the deepest control under the
  cursor and never bubbles - whichever of the two the cursor is over is
  the only one that can answer;
- the label's own `Click` handler is GONE (`MakeClickable` is now
  `MarkSortable`). A second handler on the label would fire alongside
  the row's for one press and cycle the sort TWICE - the exact bug the
  container-first dispatch order creates, and the reason it is called out
  here rather than left as a comment.

The washes carry `ZIndex = -1`, because Blish draws children in ZIndex
order and a wash created after its label would otherwise paint over the
text.

`Services/HeaderCellMath` does the split, Blish-free and tested: a
PARTITION rather than padded boxes, so no click lands in a dead strip
between two columns. Each boundary is the caller's own COLUMN edge where
it has one, and the midpoint between two labels only where it does not -
see the review fixes below, where the label midpoint turned out to be
the wrong rule for the columns that matter. Its degenerate cases -
labels that touch, a right-aligned label that has slid left of its
neighbour in a narrow window, a supplied boundary outside the band -
collapse rather than inverting. On the Snapshot tab every cell ends at
its own grid column's edge, and the last column absorbs the remainder
integer division leaves.

### E - tooltip translucency

From the maintainer's own in-game inventory capture. A real GW2 tooltip's
interior is NOT flat: background medians shift about 20 levels per
channel across one box - (34,38,40) at one end to (43,55,55) at the
other - because the scene behind shows through, which puts the game's
alpha nearer 0.75-0.85. `RichTooltipSurface` painted a flat
`Color(0,0,0) * 0.92f`, which reads as an opaque card beside it.

Two changes, and nothing else. **The alpha constant goes 0.92 -> 0.82**,
the UPPER end of the measured band deliberately: audit finding H6 is that
content behind a tooltip must never bleed through LEGIBLY, and 0.82
leaves 18% of the scene where the bottom of the band would leave 25%.
And **a 1px light bevel immediately inside the dark border**, which the
capture shows as a pair rather than a single edge - the chrome grey this
file already carries for the header icon's frame, at 0.22 alpha, a
highlight on the canvas rather than a second border. Both are cheaply
reversible: one constant and one call.

### Judgment calls, all cheap to reverse

1. **Log column header reads "Time"** over a level+timestamp+tag prefix.
2. **Snapshot sections read "Items" / "Currencies"**, matching the empty
   state's own wording rather than the filter dropdown's "Wallet".
3. **Snapshot item names take their rarity colour only when there IS
   one**, and keep white otherwise (revised - see the review fixes).
4. **Snapshot icons draw the plan's own 32px art in a 34px frame**
   (revised); the item-search dropdown and the Total Cost currency table
   still inset their art by the border, because on those two rows the
   box size is itself a layout term.
5. **The wash is white at 0.07 (0.14 held)** and the label keeps its
   amber hover tint, because an unsorted column shows no sort indicator
   and the wash alone is deliberately faint.
6. **Theme B's tests reach only as far as the Blish-free rule allows.**
   Its surface is Views-layer control construction; what could be pinned
   was pinned (the coin denominations' hover text), and the gate below
   is the evidence for the rest.

### Review fixes

An adversarial pass over the five themes above found six things worth
fixing. All six are in this branch; each is stated here with what it was
and why the new behaviour is the right one.

**The Snapshot's sort click re-ran the account search.** `SortBy` called
`RebuildContent`, which re-ran `SnapshotSearchResultBuilder.BuildItemRows`
over the whole account index and then disposed and recreated every
control - on a full snapshot, thousands of rows of synchronous
main-thread work to change nothing but the order. It also dropped the
scroll position (Blish's `Scrollbar` zeroes itself when the content
height changes) and replaced the header panel under a stationary cursor,
which leaves `MouseOver` false until the mouse moves - the exact
stale-hover class `HoverChainResync` exists for, made easier to hit by
theme D's whole-cell target. The plan view had already been fixed for
this class (`RerenderForSortChange`).

The fix is not to copy the plan's scroll-preserve machinery but to
remove the need for it: the cells are held in the SEARCH's order and a
click derives a placement ORDER over them
(`SnapshotTableSorter.ItemOrder` / `WalletOrder`, the same comparators
`SortItems` applies). Nothing is re-queried, nothing is disposed, the
row count and grid height are identical across a click - so the scroll
offset and the hover chain are untouched by construction and no resync
call is needed. Cycling back to None restores the search's own order
without the view keeping a second copy of it.

**Two re-layout paths still measured strings.** Commit 6201777 set the
contract for the plan's headers (position-and-width work per tick,
measuring at build and settle only) and the Snapshot had not been held
to it: `PlaceAmountLabel` measured a FIXED string per cell, and
`LayoutSectionChrome` allocated a column list, two arrays and two
closures per grid column and measured both header labels. The amount
width is captured at build, and the chrome now owns a `HeaderCellPlan`
rebuilt only when the column count or a header's width changes.

*Correction to how often those ran.* This was first recorded as a
per-frame path on both counts, and it is not one. The Snapshot's
re-layout is trailing-debounced: `ScheduleRowRefit` stamps a tick and
returns, a ThreadPool wait loops until `ResizeSettleMs` of quiet and then
marshals `RefitResultRows` ONCE, so `LayoutResultGrid` runs once per drag
and once per sort click, never per pixel. `HeaderCellPlan`'s OTHER
callers - `CTableHeaderRenderer` and `ShoppingListSectionRenderer`, which
register through `ISectionRelayoutSink` - DO run per frame, because
`CraftingPlanView.ReplayRelayout` replays those closures straight off
Blish's `Resized` event. One class, two rates. The fixes stand either way
(a repeated path that allocates and measures for nothing is still worth
removing); only the stated frequency was wrong, and the dual rate is now
stated once, at `HeaderCellPlan`.

**The repack re-stamped every tooltip on the row.** Follow-on from the
same pass, and the cheap thing was removed while the expensive one
stayed: the Snapshot re-fit closure called back into the row's whole
tooltip stamp - a fresh builder closure, four `TooltipFacility.Register`
calls (each a `TooltipContentSource` allocation plus a
`ConditionalWeakTable` Remove+Add) and a recursive walk of the icon's
child tree - per row, for content no part of which is a function of the
column width. Only the two text lines were ever invalidated, and only
because `FitRowTextLabel` wrote a plain tooltip: a non-null
`BasicTooltipText` write nulls `Control._tooltip` and so drops the rich
surface stamped over it. `FitRowTextLabel` no longer writes tooltips at
all - the row owns them, as it already did for the strip, the amount and
the icon - so the repack now fits text and moves the amount, and nothing
else. The plain note those labels carried was overwritten by the rich
stamp on the same line, but it was NOT dead: `Register` captures a
control's `BasicTooltipText` as the source's `FallbackText`, and
`ResolveContent` returns exactly that when a deferred builder throws -
and the item builder calls into the session stat cache from inside
Blish's mouse-moved handler. Round 1 dropped it and called the change
behaviour-neutral on the strength of a fallback nothing could reach,
which was wrong. `CreateItemRow` now stamps the line's own text as a
plain note once, at build, before the rich stamp takes the label over:
unconditional rather than shorten-conditional as before, so the fallback
does not depend on the column width and the repack still owns no
tooltip.

**Coin icons answered no hover.** `CoinCurrencyRenderer` built its coin
icon as a raw `Panel` with a `BackgroundTexture`, entirely outside
`IconControls` - so in a Total Cost row the spirit-shard icon named
itself and the gold coin beside it said nothing. That is the module's
most numerous icon draw and the site directive B's report literally
describes. `IconControls.CreateAssetIcon` is the asset-id twin of the
unframed path (no missing-art branch: an asset id is a constant, so
there is no data gap to degrade), and `CoinSegmentMath.DenominationName`
names the three denominations beside the ids it already owns. The
inventory table above understated this and is corrected.

**Header cells stopped at the midpoint between two WORDS.** The split
took `gapStart + (gapEnd - gapStart) / 2` over the label extents, and a
header's text is a fraction of the column it names: on the Shopping List
the boundary landed roughly halfway between "Item" and "Source", so a
click above the right-hand end of the item NAMES sorted by Source.
`HeaderCellMath.LabelExtent` now carries an optional explicit boundary
(the midpoint remains the fallback), `ShoppingColumnMath` derives its
four from the same pre-scan its columns come from,
`SnapshotItemGridLayout.CellHeaderSplitX` does the same for a grid cell,
and Used Materials hands `CTableHeaderRenderer` the three terms its name
column already budgets against. The inert headers keep the fallback -
their cells answer no click and paint no wash, so a boundary between
them decides nothing.

**Snapshot names got dimmer for the common case.** The rows took
`GetRarityNameColor(RarityFor(...))` unconditionally, and `RarityFor`
answers null for any item no plan has fetched - so on a fresh session
every name dropped from white to the palette's 200-grey unknown entry,
while the frame that colour was paying for stayed neutral anyway.
Directive A's own no-small-grey rule argues the other way: the rarity
colour is taken when there IS one, white otherwise. The art also goes
back to the 32px the rows drew before the frame arrived (the box grows
to 34, which the 40px text column clears), so the visible delta on this
tab is not "smaller icons, dimmer names".

The order is always derived from the rows as the SEARCH produced them,
never from what is currently on screen, so ties still break in the
search's own order exactly as the rebuild path made them - a click is
not a compounding sort. `SortItems`/`SortWallet` are gone with the
rebuild that used them; the comparators they wrapped are what the order
is built from, and what the tests drive.

**The wave's comment ratio was over the bar.** The brief set "well under
25%" for new code and the wave measured 32% (896 comment lines of 2719
added `.cs` lines against v0.2.3). The heaviest offenders were prose
re-narrating a decision this file already records at length - the sorter's
24-line preamble on returning an order rather than a copy, restated a
third time on `MainView.SortSection`. Compressed to the invariant plus a
pointer at each canonical site: the wave is now 22% (522 of 2342), and
every file the wave ADDED is under 25% on its own
(`SnapshotResultLayout` 16%, `SortableHeaderCells` 19%,
`SnapshotTableSorter` 20%, `HeaderCellMath` 23%, `HeaderCellPlan` 24%).
`MainView`'s share of the wave is 26%, against that file's own 43% at
v0.2.3. What survived is measured numbers (the 79px header floor, the
544px column, Blish's container-first dispatch order and its
`BasicTooltipText` setter) stated once where they are used.

### Out of scope, untouched

The sibling `field-fixes-3` branch owns the Total Cost zero-band
retention rule, scroll anchoring across re-solves, the click-sound
default, the UNKNOWN Mystic-Forge recipe fallback and first-load snapshot
triggering. None is touched here. `Services/PlanViewModelBuilder.cs` and
`Views/CraftingPlanView.cs` are untouched apart from one mechanical
rename of the icon component in the latter.

### Desktop gate

1. **The ramp, on each of the four tabs.** Snapshot: "Account Snapshot"
   at 24 bold over a full-width rule, the status line at 18 bold,
   "Items"/"Currencies" at 24 bold, "Item"/"Currency"/"Amount" at 20 bold
   on the dark band. Settings: five section headings at 24 bold, the
   currency grid's "Currency"/"Copper per unit" on a banded 20-bold
   header, the save-bar status at 18 bold. Log: status at 18 bold,
   "Time"/"Message" on a banded 20-bold header. About: the module title
   and both "Disclaimer:"/"Credits:" headings at 24 bold. Nothing
   anywhere renders multi-word text at 18 REGULAR (the collapsed-word-gap
   defect); nothing renders a name at Caption grey.
2. **One icon treatment everywhere.** Three formerly-inconsistent sites,
   side by side with a plan row's icon: (a) a Snapshot item row, (b) a
   Snapshot wallet row, (c) the item-search dropdown under the Crafting
   Plan tab's search box. All three must show the same 1px frame, and all
   three must answer a hover. Then hover a Crafting Steps icon and a
   Required Recipes icon - both must now show the row's own note, which
   they never did. Finally hover the GOLD COIN icon in a Total Cost row
   and then the spirit-shard icon beside it: the coin must name its
   denomination where it used to say nothing. Snapshot item names must
   read WHITE on a fresh session (no plan generated), not grey.
3. **Snapshot full-width tracking, at several widths.** At the 1378
   floor, at ~1600, and maximised: both section rules and both header
   bands must run the full width of the result panel at every width, the
   Amount column must stay pinned the same distance from each cell's
   right edge, and the header pair over EVERY grid column must sit on the
   x's of the cells beneath it (check the rightmost column especially -
   it absorbs the division remainder). Drag slowly across the 2 -> 3
   column threshold (~1758px window) and confirm the third column's
   header pair appears with it.
4. **Snapshot item tooltips.** Hover an item row for something never
   planned this session: a rich tooltip must appear, headed with the
   item's name and carrying the full source breakdown. Hover a wallet
   row: the currency's name. Hover a wallet row's ICON: the same. Then
   generate a plan containing one of those items, return, and hover it
   again - the full stat block must now head the box instead.
5. **Sortable-header hover wash and whole-cell click, on a plan table AND
   the snapshot.** On Used Materials: move the cursor into the header
   band well AWAY from the word "Item" - the whole left cell must wash
   and show the "click to sort" note, and a click there must sort. Same
   for "Amount" on the right. Confirm ONE click cycles the sort ONE step
   (a double cycle is the regression this design has to avoid). Repeat on
   the Snapshot items header. Confirm the Recipe Tree's inert "Source"
   header does NOT wash.
   **Then the column boundary specifically**, on the Shopping List: hover
   the header directly above the right-hand end of the longest item NAME.
   The Item cell must be the one that washes and the one that answers the
   click - it used to be Source from about halfway across the names.
6. **The Snapshot sort click keeps its place.** Take a full snapshot,
   filter All, empty search, and scroll down to the Currencies run. Click
   its Amount header: the run must re-order with no perceptible stall,
   the list must NOT jump to the top, and the header under the
   still-stationary cursor must stay washed (no mouse jiggle needed).
   Click twice more - descending, then back to the search's own order.
   Do the same on the Items header with the list scrolled deep, and
   confirm the Currencies run's order is untouched by it.
7. **Tooltip translucency against the capture.** Open the maintainer's
   own inventory screenshot beside a module tooltip over the same kind of
   bright scene: the interior must no longer read as an opaque card, the
   dark border must have a light line immediately inside it, and NO text
   behind the tooltip may be legible through it (audit H6).

Gate: PASS (2026-08-25 desktop session, branch build, captures
preflight/gTY1-gTY15; display-sleep inhibitor held the session).

A. RAMP: Settings, Log and About now carry 24-bold section titles with
   rules where they previously had one 18-regular size and, on Log, no
   headings at all. Settings reads as a hierarchy ("Sound", "Homestead
   Refinement", "Logging", "Snapshot"); About's title and both labelled
   sections match; Log gained "Time"/"Message" column headers over
   columns that were unlabelled before.
B. ONE ICON PATH: verified on three formerly-unframed surfaces - the
   Snapshot item rows (Augur's Stone renders its ascended frame,
   Zojja's Claymore its own), the neutral placeholder on an art-less row
   (Mithril Ore), and the framed icons in the search dropdown.
C. SNAPSHOT: the overhaul is the visible one. Two-column grid with a
   24-bold "Items" title, per-column "Item"/"Amount" headers, amounts
   right-pinned to their column edge, and holdings as a sublabel under
   each name. Hovering a row now opens the rich item tooltip (account
   binding, value, holdings) - the recorded "snapshot rows have no rich
   tooltip" gap, closed without adding a network call on the hover path.
D. SORTABLE HEADER CELLS: proven on the strongest case rather than the
   easy one. Hovering EMPTY space at x=400, far from the "Item" text,
   lit the whole cell's wash and tinted the label; CLICKING that empty
   space sorted the table ("Item ^", Augur's Stone first). The Amount
   cell behaves the same (1x, 5x, 30x, 42x ascending with its own
   indicator). No dead strip between the two columns.
E. TOOLTIP TRANSLUCENCY: 0.82 alpha plus the 1px inner bevel ships; the
   rich tooltip over the Snapshot list reads as a translucent panel
   rather than the old flat slab, and text behind it stays illegible
   (audit H6 holds).

Recorded, NOT a defect in this wave: these tabs now have the plan tab's
TYPE hierarchy but not its LAYOUT - Settings, Log and About are still
left-packed, with the panel's right half empty, because this wave scoped
the ramp plus Snapshot's grid rather than a per-tab redesign. The
maintainer saw the same thing on the Settings capture and asked for the
full treatment on every tab; that is its own milestone, not a fix to
this one.

## Rotation note: the append zone is due a pass

At the merge that landed this rotation the active file sat at ~112KB -
the ~65KB rotated core plus the three milestone records this release
cycle added (seed-integrity, field-fixes-3, app-typography), all three
of which are gated and closed. That is past the ~100KB tripwire above,
and it is the tripwire working as designed rather than a defect: fresh
records land in the append zone, and the NEXT rotation takes them. They
were deliberately left in place here rather than archived minutes after
being written, so this release's record reads in one file.

## Vendor data refresh, and the stale row it tried to ship (vendor-refresh)

The maintainer authorised running the vendor updater unattended - "i have
never run it by hand or supervised it before so i will not be planning to
start now. if you have concerns about it.. do work to mitigate your
concerns first" - so the concerns were mitigated first, and one of them
turned out to be real.

**Method.** Dedicated branch; the 13MB baseline copied outside the repo
before anything ran; the WRAPPER script rather than a manual invocation,
because tools/VendorOfferUpdater/README.md is explicit that a manual pass
without `--tag-seasonal-festivals` and `--merge-into` wholesale-replaces
the dataset. The local wiki cache was gone, so both passes re-scraped from
scratch (~15 min, rate-limited).

**Result, measured against the backup rather than trusted.** 53,544 ->
59,414 offers. By CONTENT (item, count, merchant, cost lines) only 254 of
51,679 distinct offers disappeared, and every single one had all-null cost
lines - rows the previous pass could not resolve and which no consumer
could price. 581 items became purchasable that were not before, including
**Gift of the Survivors (106712) from Castaway Agnes** - one of the four
items the maintainer reported as UNKNOWN, and the one this data could
actually fix (it is a Visions of Eternity vendor item, not a recipe).
Seasonal tags went 57 -> 597.

**The defect the refresh introduced, and the repo's own trip-wire that
caught it.** `AcquisitionHintSeedVendorAgreementTests` pins WHICH items
carry both a hand-verified acquisition hint and a shipped vendor offer,
with the comment "a fourth arriving unnoticed is worth a manual look". A
fourth arrived: item 19678, **Gift of Battle**, newly scraped as sold by
`Battle Master` in Eternal Battlegrounds for 500 Badges of Honor. This
repo's own hint for that item, hand-verified 2026-07-20, says that vendor
path "was removed in the Spring 2016 Quarterly Update". The wiki still
describes the sale on a page that is not marked historical, so the scrape
ingested it. Shipping it would have priced a legendary component from a
decade-dead vendor and quietly changed every legendary plan that uses it.

**Fix, as data rather than heuristics.** New `ref/vendor_offer_exclusions.json`
refuses rows by (merchant, item) with a reason, a source URL and a
verification date; the tool applies it after the merge, so neither a fresh
scrape nor a surviving baseline row can reintroduce one. Three tests in
VendorOfferUpdater.Tests pin that a refused row is dropped, that the
refusal is keyed on BOTH merchant and item (the genuine Gift of Battle
path must never be caught by it), and that a missing or corrupt list is
survivable rather than fatal. With the row excluded the agreement
trip-wire passes unchanged - the population is back to the three recorded
items.

**A second finding, recorded because it invalidates a stated premise.**
OfferId is NOT stable across a full re-scrape. A pinned row
(Drojkor, Spirit Squall / item 84618) came back with byte-identical item,
count, cost lines, merchant and location under a different hash, because
`VendorOfferHasher`'s own doc comment says a recompute appends hash
segments (homesteadTier, seasonalCap) that the committed baseline predates
- ids only stayed stable while `--merge-into` copied untouched baseline
objects through. Two tests pinned literal hashes on the premise that only
a content change could move them; both now key on content, which is what
they meant.

Gate: not required - dev-tool and data change. Both suites green (module
2650, tool 210), and the loss analysis above is the verification a desktop
gate could not add.
