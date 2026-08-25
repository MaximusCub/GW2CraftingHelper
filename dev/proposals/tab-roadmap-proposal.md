# Tab Roadmap - Synthesis of D1-D5 (Design Proposal)

Status: SYNTHESIS PROPOSAL (design only, no code changes). Written by the
synthesis architect over the five per-tab design proposals
(`d1-snapshot-about-settings.md`, `d2-log-system.md`, `d3-plan-history.md`,
`d4-crafting-ranker.md`, `d5-next-step-feasibility.md`) and the M38 cleanup
plan (`m38-cleanup-plan.md`). Read-only against the repo.

Everything below is a PROPOSAL for the maintainer's reaction, not a
commitment. Effort is classed S/M/L/XL with a one-line reason; no time
estimates anywhere. Evidence tags: **MEASURED** = read from code/the source
proposals this session; **INFERRED** = reasoned from measured facts; **GUESS**
= judgement call.

The module has 7 tabs. **Crafting Plan is out of scope** (the M38
`CraftingPlanView` decomposition, WP-04/21/23/24/25/26, owns it). The other
six are covered below: Snapshot, About, Settings, Log, Plan History, Crafting
Ranker.

---

## 1. Executive summary (two-minute read)

| Tab | What it becomes | Effort | Standout open question |
|---|---|---|---|
| **Snapshot** (D1) | Redesigned into a search-as-you-type account-inventory browser: plain `TextBox` filter + source-filter checkboxes over the already-built-but-unused `AccountItemIndex`/`GetPrioritizedSources`, one grouped row per item with a per-source breakdown (retires the Aggregate checkbox). **Subsumes a confirmed traced bug**: disk-restored snapshots never reach the view until a network refresh (`Module.LoadAsync` never sets `_snapshotDirty`). | **M** (S once the bug fix is split out) | Session-sticky search/filter state, or clean reset each visit? |
| **About** (D1) | New static tab: name/version/source/author read live from `ModuleParameters.Manifest` (with a `manifest.json` fallback), Blish-HUD MIT credit, an ArenaNet fan-content disclaimer, and the module data-directory path for bug reports. | **S** | The exact ArenaNet-required disclaimer wording is unverified - draft only, needs sign-off. Branding (MaximusCub vs Lachlan Mulcahy) is WP-28's call. |
| **Settings** (D1 + all) | Audited; **exactly the additions the other tabs need**, deduped into one plan (see Section 2.1): a shared snapshot-refresh interval, a unified logging/diagnostics section, a plan-history cap. Six candidate settings explicitly rejected (default price basis, reset-to-defaults, Settings Clear Cache, etc.). | **S** | Is `1`-`120` min the right refresh-interval clamp? Section grouping/titles. |
| **Log** (D2) | New module-level structured log system (`ModuleLog` ring buffer + gated `ModuleLogStore` JSONL file sink) layered ON TOP OF Blish's own untouched `Logger`. Log tab becomes a live search / level-filter / follow-tail / copy pane. Disk policy keeps Error/Warn/Info always-on-but-rare, gates the noisy Debug tier (incl. `[scrolldiag]`) behind one toggle. | **M** | Clipboard API usable for a real Copy button, or fall back to Select-All? Default level filter (`Info+` vs `All`)? |
| **Plan History** (D3) | New tab. A history entry is the small stable REQUEST (item ids/qty, price basis, settings snapshot, item-id-keyed ignore set) + a tiny generation-time summary - never the large/stale full result. "View" = frozen historical numbers (free); "Reuse" = re-solve at current prices via the same delegate `CraftingPlanView` already uses, zero edits to that file. Auto-capture + dedup/cap/pin. | **L** (M if V2 cross-tab handoff + sparkline cut) | Is V1 inline read-only reuse acceptable, or is reopening the interactive Crafting Plan tab a hard requirement (forces waiting on all view-decomposition waves)? |
| **Crafting Ranker** (D4 + D5) | New tab, two layers. **D4 (foundation):** persisted priority-ordered watchlist (up/down buttons; no drag primitive exists) + per-item coin-denominated closeness (`1 - remaining/from-scratch cost`) computed by two real solves per item, honest N/A when currencies/time-gates dominate. **D5 (on top):** a Tier-1 "Do Next" classifier bucketing plan steps into Buy-TP-now / Buy-vendor-now / Craft-now / Blocked / Time-gated. | **L** (D4) + **M** (D5 Tier-1) | D4 and D5 must converge on ONE `RankerStore`, one filename, one entry shape before either starts. |

Cross-tab facts worth two lines: **all six tabs stay on the lightweight
scroll pattern (A)** and never engage the M33 `PlanContentHeightMath`/relayout
contract, so none of them touch the `CraftingPlanView` blast radius. **Three
tabs show coin** (Snapshot panel, Plan History cost, Ranker remaining cost) and
must all call the shared `CoinCurrencyRenderer` landing from WP-21/22 rather
than a fourth private copy of the coin-right-of-number invariant.

---

## 2. Cross-cutting consistency fixes

The five proposals were written largely independently. They agree on the big
architectural calls (pattern A, no M33, reuse the pipeline) but they collide or
duplicate in four concrete places. Resolve these once, here, before any
implementation starts.

### 2.1 One Settings plan, not four

Deduped set of NEW settings across all proposals:

| Setting | Type / default | Owner | Settings section |
|---|---|---|---|
| `SnapshotRefreshIntervalMinutes` | int, 10, clamp 1-120 | D1 | Snapshot |
| `LogMaxSizeBytes` | int, 2 MB | D2 | Logging |
| `LogRetentionDays` | int, 14, clamp 1-365 | D2 | Logging |
| `LogDiagnosticsEnabled` | bool, false | D2 (**subsumes** `ScrollDiagnosticsEnabled`) | Diagnostics |
| `PlanHistoryMaxEntries` | int, 50 (GUESS) | D3 | Plan History |

**The one real collision to resolve: the diagnostics toggle.** D1 proposes
surfacing today's `ScrollDiagnosticsEnabled` as a UI checkbox (discoverability
fix). D2 proposes *renaming/subsuming* it into `LogDiagnosticsEnabled`, which
gates both the `[scrolldiag]` channel AND the log file's Debug tier. These are
the same setting with two different names and two different scopes.

**Recommendation:** the logging system owns the diagnostics toggle. Ship ONE
`LogDiagnosticsEnabled` checkbox in ONE shared "Diagnostics" settings section;
do **not** also surface a separate `ScrollDiagnosticsEnabled` checkbox. If the
log system lands before D1's Settings work (recommended order, Section 3), D1
simply references the unified toggle and drops its own diagnostics-surfacing
item. If D1's Settings work lands first, it should introduce the checkbox
already named `LogDiagnosticsEnabled` so D2 does not have to rename a
user-visible setting later. (Note: renaming the persisted key resets any
hand-set value; low impact, few users hand-edit it, but worth a one-line
migration note.) This is exactly D1's open question 3.2 and D2's subsume note,
resolved.

**Rejected settings (do not add), consolidated from D1/D2/D4/D5:** default
price basis / default quantity / default tree depth (all require editing the
out-of-scope `CraftingPlanView`); a Settings-tab Clear Cache button (duplicates
the Snapshot header button); a reset-to-defaults button (Blish's platform
settings screen already exposes reset; `ResetToDefaults()` is dead code today);
a search-scope toggle with only one meaningful position; persisting
Snapshot source-filters across restarts (in-session stickiness covers it);
`LogMinFileLevel` as a UI control (hardcode Info floor); `RankerMaxEntries` and
the Tier-2 "use daily-crafting API" toggle (deferred until their features exist
and real usage justifies them).

`SettingsTabContent.Build()` is a flat sequence of `BuildXSection()` calls
(MEASURED, D1), so each new section slots in without collision **provided the
section titles are distinct** - coordinate "Snapshot" / "Logging" /
"Diagnostics" / "Plan History" so two proposals do not both add a section with
the same header.

### 2.2 One store convention, not four

Four new file-backed stores are proposed: `ModuleLogStore` (D2),
`PlanHistoryStore` (D3), `RankerStore` (D4 and D5 - the same store). They must
share ONE convention:

1. **Atomic write via `.tmp` + `File.Replace`/`File.Copy`**, matching
   `StatusStore`/`VendorOfferStore`. **Three proposals (D2, D3, D4)
   independently corrected the scout note that claimed `SnapshotStore` is
   atomic** - it is not, it uses a plain non-atomic `File.WriteAllText`
   (MEASURED in all three). The new stores must copy the RIGHT sibling
   (`StatusStore`), not `SnapshotStore`. Fold this correction into the eventual
   docs/architecture write-up (WP-27) so the wrong claim does not propagate.
2. **`onError` callback from day one** (`Action<string, Exception> onError =
   null`, the exact shape WP-16 is retrofitting onto the existing four stores).
   Building it in now means none of these is "a fifth/sixth store WP-16 has to
   revisit." No hard ordering dependency with WP-16 in either direction.
3. **Blish-free, constructed with `dataDirectoryPath`, Newtonsoft JSON,
   tested with real temp-dir IO** following the `VendorOfferStoreTests` /
   `StatusStoreTests` template. No contract-mirror tests.

**The one principled exception:** `ModuleLogStore` uses newline-delimited JSON
(JSONL, append-only) rather than a single JSON document, because a log is
append-heavy and a crash mid-append to JSONL loses at most the last line
(D2's justification, MEASURED-sound). It still uses the atomic rewrite for its
rotation trim. Plan History and Ranker each hold a single JSON document. That
is the whole convention: one atomic-write base, one `onError` shape, JSONL only
where append-only semantics justify it.

**Filename collision to resolve:** D4 guesses `data/ranker_watchlist.json`, D5
guesses `data/ranker.json` - for what is meant to be **one** store. Neither is
load-bearing. **Recommendation: `data/ranker.json`** (the store will grow
beyond a bare watchlist as D5's Tier-1 layer lands). Pick it and move on. Full
proposed data-dir layout after all features land: `snapshot.json`,
`status.txt`, the vendor overlay, `module_log.jsonl`, `plan_history.json`,
`ranker.json`.

### 2.3 The logging system is a dependency of every other tab's diagnostics

D2's `ModuleLog` is not just the Log tab's backing store - it is the natural
sink for lifecycle/diagnostic events the other tabs generate:

- Snapshot refresh lifecycle (D2 already lists `Module.cs` snapshot sites as
  migration targets; D1's Snapshot redesign generates the same events).
- The `[scrolldiag]` channel, gated on the unified `LogDiagnosticsEnabled`.
- Plan-generation lifecycle (start/finish/failure/elapsed) - relevant to the
  Ranker's 2N-solve Refresh (D4) and Plan History's Reuse (D3).
- The **currently-silent `catch {}` blocks at `Module.cs:164-195`** (D2,
  MEASURED) that swallow vendor-baseline/recipe-seed/manifest load failures
  with no log call at all - a real gap the migration closes.

**Consequence for order:** build the log system's core early (Section 3) so
the other tabs log through one structured system and share one diagnostics
toggle, rather than each scattering `Logger` calls and each proposing its own
diagnostics flag. The one wrinkle: D2's plan-lifecycle logging and its
`[scrolldiag]` reroute touch files under active M38 churn (WP-13 pipeline,
WP-04/23 view) - so those specific migration sites sequence around those WPs
even though the log *core* is fully independent.

### 2.4 One shared "plan request" model primitive

D3 (Plan History) and D4/D5 (Ranker) both persist a small
`List<PlanRequestItem>` + denormalized display fields (Name, IconUrl). D5
explicitly flags this for reconciliation. **Recommendation:** both reuse the
existing `Models/PlanRequestItem` (`{ItemId, Quantity}`) directly plus a shared
denormalized item-summary shape (`{ItemId, Name, IconUrl, Quantity}`) - but
keep their **stores separate**, because the semantics differ: Plan History is
auto-captured past generations (with cost samples, pins, dedup); the Ranker is
a curated forward-looking priority list. Shared model primitive, separate
stores.

Related: both D3 and D4 benefit from factoring the inline
`GenerateStructuredAsync` lambda in `Module.cs` into one shared delegate passed
to `CraftingPlanView`, Plan History (Reuse), and the Ranker (Refresh) - a
`Module.cs`-only change that also keeps `RecipeService`/`TradingPostService`
caches warm across tabs. Whichever of D3/D4 lands first establishes this
plumbing.

### 2.5 Two small correctness/docs fixes no M38 WP owns

- **`Module.LoadAsync` disk-snapshot bug** (D1, traced and MEASURED): a
  disk-restored snapshot sits valid in `_currentSnapshot` but is never pushed
  to the view because `LoadAsync` never sets `_snapshotDirty`; the tab shows
  "No snapshot available" until a network refresh succeeds. Two-line fix,
  reusing the existing dirty-flag drain. Can ship as a standalone early PR
  (adjacent to WP-17's `Module.cs` catch work - review together to avoid a
  needless merge conflict).
- **Stale "TimegatedItems always empty" comments** in `CraftingPlan.cs` and
  `TimegatedItem.cs` (D5, MEASURED): now false - 692 of 53,529 vendor offers
  carry caps and `PlanSolver` populates `TimegatedItems`. No M38 WP covers
  this docs correction; fold it into the Ranker/Do-Next work or a docs sweep.

### 2.6 Tab ordering and visual verification (minor, shared)

- `Tab.OrderPriority` default/tie-break behavior is unproven from code (D3 and
  D4 both flag). Once Plan History, Crafting Ranker, and About stop being
  placeholders, pin them to explicit `OrderPriority` values rather than relying
  on an untested default.
- Every new tab touches `Views/` rendering, so per M38 rule 8 each needs the
  live visual-verification loop - but because they all use pattern (A) with no
  scroll/resize invariants, a lighter smoke check suffices (they carry none of
  `CraftingPlanView`'s frame-timing/scroll-restore risk). Still, the view layer
  has zero automated test net, so the pure/Blish-free services behind each tab
  (`ModuleLog`, stores, `RankerClosenessCalculator`, `NextActionClassifier`,
  dedup/ordering helpers) carry the real test coverage.

---

## 3. Recommended build order (interleaved with M38)

Guiding principles: quick correctness wins first; infrastructure (logging,
store convention) before the tabs that lean on it; user-conviction-weighted
value; and respect the two hard M38 gates - **WP-21/22 (coin renderer)** for
anything showing coin, and **WP-04/21/23/24/25/26 (all view-decomposition
waves)** for anything that must edit `CraftingPlanView`.

M38 runs Waves A->H. The coin renderer is **Wave F** (late), so coin cells
cannot repoint to it until then - but every feature below can build its
non-coin surface in parallel with M38 Waves A-E and gate only the coin cell.

**Phase 1 - quick wins + substrate (parallel with M38 Waves A-E, no gate):**

1. **Snapshot `LoadAsync` bug fix** (D1, from Section 2.5). Standalone,
   two-line, independent. Fixes real existing-behavior breakage. Do it early.
2. **Log system core** (D2). Greenfield; no M38 WP targets it. Build the
   service + store + settings + Log tab, plus the `Module.cs`/snapshot/settings
   call-site migrations. Establishes the store convention (2.2), the `onError`
   shape, and the unified `LogDiagnosticsEnabled` toggle (2.1) that everything
   else builds on. **Defer** only the two migration slices that touch M38-churn
   files: the `[scrolldiag]` reroute (align with WP-04/23) and the
   plan-lifecycle logging (align with WP-13).
3. **Snapshot search + Settings additions** (D1 Features 1+3). Independent; the
   `SnapshotRefreshIntervalMinutes` setting is shared with the staleness label
   so they land together. Coin panel keeps its private methods with a
   `// TODO(WP-22)` marker until Wave F, then repoints. Drop the separate
   diagnostics checkbox in favour of the log system's unified toggle (2.1).
4. **About tab** (D1 Feature 2). Small and independent, but best **after
   WP-27/28** so its branding/author line reflects the reconciled name; if it
   ships earlier it uses the `manifest.json` fallback and only static strings
   need touch-up later.

**Phase 2 - the Ranker (the flagship; D4 then D5):**

5. **Ranker watchlist + closeness** (D4). The foundation: watchlist store
   (the reconciled `RankerStore`), priority ordering, per-item closeness. Its
   coin cells gate on **WP-21/22**; everything else can build in parallel with
   Waves A-E. Calls only the stable public `GenerateStructuredAsync`, so it does
   NOT need the WP-11/12/13/15 pipeline refactors to finish first (D4's own
   argument, MEASURED against those WPs' move-only scope).
6. **Ranker Tier-1 "Do Next"** (D5). Layers the `NextActionClassifier` on top
   of D4's watchlist, sharing the one `RankerStore`. D5 recommends waiting for
   the WP-11/12/13/15 pipeline dedupe to settle before this ships (building the
   classifier against a moving pipeline invites churn); it consumes only the
   stable `CraftingPlanResult` surface. This delivers the user's flagship "tell
   me what to do next" ask - see the feasibility verdict, Section 4.

**Phase 3 - Plan History (lower user conviction; more self-contained):**

7. **Plan History V1** (D3). The user framed this as "needs thinking thru a bit
   more maybe" (lowest conviction of the five), and its highest-value form (V2
   cross-tab reopen) is blocked on all view waves anyway - so it sits after the
   Ranker. V1 is self-contained: store + list + View/Reuse-inline + tests, zero
   edits to `CraftingPlanView`. Coin cell gates on **WP-21/22**. Note: if the
   maintainer prefers a lower-risk warm-up before the Ranker's 2N-solve
   orchestration, Plan History V1 is the safer first tab and would establish the
   shared generate-delegate plumbing (2.4) that the Ranker also uses - a
   defensible reordering, flagged rather than hidden.

**Deferred / gated (revisit, do not build now):**

- **Plan History V2 cross-tab handoff** - adds a new public method to
  `CraftingPlanView`; must wait until **WP-04/21/23/24/25/26 all land** (six
  packages actively carving that file). Revisit after M38 view decomposition.
- **Ranker Tier-2 time-gate projector** (D5) - gated on THREE preconditions
  (REVISED, maintainer ecto directive, 2026-07-22): the `progression`-scope
  decision, a cap-coverage measurement, AND a working binding-gate filter /
  route-splitter (shared with the Tier-1 classifier). See Section 4.
- **The `[scrolldiag]` reroute + plan-lifecycle logging** (D2 tail) - complete
  these log migrations as WP-13 and WP-04/23 stabilize their files.

**Why this order, in one line each:** bug fix first (real breakage, trivial);
log core second (substrate + one diagnostics toggle + store convention for
everyone); Snapshot + Settings third (high-conviction "search my bags" ask,
independent); About fourth (small, waits on branding); Ranker fifth/sixth (the
detailed high-conviction two-fold ask + its flagship next-step layer); Plan
History last of the active set (lowest conviction, and its best form is
view-wave-blocked regardless).

**Does the Ranker depend on Plan-History-style persistence? No.** They share
the `PlanRequestItem` model primitive and the store *convention*, but use
separate stores with different semantics (2.4). The Ranker's only intra-feature
dependency is D4-before-D5 (same `RankerStore`).

---

## 4. Next-step engine: feasibility verdict

### 4.1 D5's verdict, quoted faithfully

From `d5-next-step-feasibility.md` Section 5.1 (**REVISED - maintainer ecto
directive, 2026-07-22**; the quote below reflects D5's revised verdict, which
now carries the binding-gate rule):

> | Tier | Feasibility | One-line justification |
> | **Tier 1 - actionable now** | **HIGH / build it, with the binding-gate
> rule** | ~90% of the data is already emitted; the classifier is mostly
> bookkeeping BUT its time-gate bucket MUST apply the binding rule (v1: surface
> caps only for NO-TP items) plus cap-aware route splitting, or it reproduces
> the ecto specimen. Still no new API, no new game-knowledge; self-consistent by
> construction *once the binding rule is in*. |
> | **Tier 2 - time-gate scheduler** | **MEDIUM / binding-gate lower-bound
> projector only** | Cap data is live but only ~1.3% coverage AND must be
> filtered for bindingness BEFORE projection - a projector that projects a
> non-binding gate (the ecto specimen) is wrong by construction, not merely
> incomplete. Honest as "at least N periods", never a live countdown.
> Daily-crafting consumption still needs the new `progression` scope. |
> | **Tier 3 - acquisition guidance** | **LOW / out of scope, possibly forever**
> | Requires drop/earn-rate data no API provides; even gw2efficiency doesn't
> attempt it; any quantified answer risks inventing data (invariant violation).
> Ship existing `AcquisitionHint` text at most. |

**What the ecto directive changed (D5 Section 1.6, new):** a timegate is
advice-worthy only when BINDING on the chosen path. The live build renders
"Glob of Ectoplasm is timegated - Weekly limit: 1 (plan needs 86)" (KNOWN-ISSUES
M37 observation (d)) - technically true, economically irrelevant, since 85+ of
the 86 ectos come from the TP regardless. Three tests decide bindingness (route
dominance; currency opportunity cost vs the watched-set's aggregate demand; no
un-gated escape hatch), with a first-order liquid-vs-bound filter (TP-liquid ->
caps are noise; account-bound / no-TP items like Mystic Clovers or homestead
materials -> caps are real gates). This is now a hard requirement threaded
through both buildable tiers, not a nuance.

And its scheduling position (Section 3.2, paraphrased faithfully):
**greedy-by-priority** as the default and only v1 mode - pour capped throughput
into priority-1 until satisfied, then priority-2 - explicitly rejecting a global
balanced optimizer as more complexity for a worse default outcome (a finished
legendary at week 3 beats two half-legendaries at week 3).

### 4.2 My independent assessment

**I endorse D5's tiering.** It is honest, well-evidenced, and correctly anchored
to the repo's no-invented-data invariant. Specifically:

- **Tier 1 (HIGH / build it): agreed, with one now-explicit condition
  (REVISED - maintainer ecto directive, 2026-07-22).** The classifier is
  bookkeeping over data the solver already emits (`PlanStep.Source`,
  `VendorCurrencyCosts`, `OwnedCurrencyAmounts`, `AccountItemIndex`,
  `TimegatedItems`) and needs no new API scope. But the original "cannot be
  wrong in a trust-destroying way because it only restates the plan against the
  live wallet" is now qualified: it holds for the four affordability buckets,
  NOT automatically for the fifth (time-gated) bucket. Restating `TimegatedItems`
  naively IS the ecto specimen, and IS trust-destroying. So the binding-gate
  rule (D5 Section 1.6; v1 = surface caps only for NO-TP items) plus cap-aware
  route splitting are part of the first cut, not deferrable polish. With that
  rule in, this is still the right MVP and the right first build in the Ranker -
  but the rule is load-bearing, not optional, and it makes the Tier-1 classifier
  slightly more than pure bookkeeping (D5 bumps its effort class M -> M/L for the
  route-splitter).
- **Tier 3 (LOW / out of scope): agreed, strongly.** The prior-art evidence is
  the clincher: gw2efficiency, the most mature tool in the ecosystem, ships a
  farming *tracker* (a stopwatch), not a farming *advisor*. That is the
  strongest available signal that Tier 3 is a genuinely unsolved problem, not a
  gap someone just hasn't filled. Any quantified "farm map Z for ~40/hour"
  answer would require inventing numbers and would violate the invariant.

**Where I sharpen D5 - Tier 2 has TWO opposite failure modes, and the ecto
directive added the second (REVISED - maintainer ecto directive, 2026-07-22).**
The original text here flagged only ONE failure mode - under-warning by
*omission*: with ~1.3% cap coverage ("we cannot prove completeness"), if a
legendary path routes through an item capped in-game but uncapped in the seed,
the projector under-reports the longest pole and says "at least 1 week" when
reality is three. That still stands, and my added gate still stands: Tier 2
should be greenlit only after *measuring* cap coverage against the specific
legendary trees users actually craft (run the harness against the top few
legendaries, confirm the seed covers the real gating poles), not on the
progression-scope decision alone.

The ecto directive adds the OPPOSITE failure mode, which D5 now internalizes:
over-warning by *non-bindingness*. A projector that faithfully computes
"ceil(86/1) = 86 weeks" for the ecto specimen is confidently, loudly wrong -
worse than the omission case, because omission silently under-reports a real
pole while this over-reports a phantom one. D5's revised Tier-2 design therefore
runs the Section 1.6 binding filter (drop non-binding gates; project only the
bound units) BEFORE the ceil arithmetic. A projector that skips that pass is
wrong *by construction*, independent of coverage.

**Net gating change (stated explicitly, per the revision brief).** The Tier-2
deferral in Section 3 previously listed TWO preconditions (progression-scope
decision AND cap-coverage measurement). It now has THREE: those two PLUS a
working binding-filter/route-splitter (shared with the Tier-1 classifier, so no
duplicate build). The recommended *order* is unchanged - D4, then D5 Tier-1,
then Tier-2 deferred - but the Tier-2 gate is strictly stronger, and Tier-1's
first cut now carries the binding rule as non-negotiable scope (it was implied
"pure bookkeeping" before). No other build-order step changes.

**Where I fully agree - scheduling.** Greedy-by-priority is the right default
for legendary crafting for exactly D5's reason: a legendary has value only when
complete, so finishing the priority-1 item first strictly dominates balanced
progress. The global multi-cap/multi-period optimizer is more code for a worse
default and belongs on the never list (Section 5).

**One practical build-path note the verdict does not spell out:** D5's Tier-1
MVP ("Do Next over a single plan first, then a stored priority list") overlaps
D4's watchlist. In synthesis, D4 IS the priority-list foundation D5 needs -
build D4 first, layer D5's Tier-1 classifier on the same `RankerStore` (both
proposals already reconcile this in D4 Section 0). The feasibility verdict is
sound; the sequencing is D4-then-D5-Tier-1.

**Net (REVISED - maintainer ecto directive, 2026-07-22):** Tier 1 - build it
(Phase 2, step 6), with the binding-gate rule and cap-aware route splitting as
first-cut scope, not polish. Tier 2 - buildable as a *binding-gate*
coverage-gated lower-bound projector, deferred behind three preconditions now
(progression-scope decision, cap-coverage measurement, AND the binding-filter/
route-splitter). Tier 3 - out of scope, and its absence is itself the honest
answer. Open refinements (thin-TP-book depth data, salvage false-positives on
bound items, valuation-coupled splitting) are recorded in D5 Section 5.5.

---

## 5. Honest "not worth building" list

Pieces that fail the value test outright (do not build, on principle):

- **Tier 3 farming/earning guidance** (D5) - blocked on drop-table/earn-rate
  data no official API provides; any answer invents data and violates the
  invariant. The most that is honest here is surfacing the *existing*
  `AcquisitionHint` text next to blocked items, which is Tier-1 polish, not a
  new engine.
- **Global multi-cap / multi-period scheduling optimizer** (D5 Section 3.3) -
  greedy-by-priority is both simpler AND a better default outcome; the optimizer
  is a constraint-scheduling problem that buys a worse user experience.
- **Live "K purchases left today" vendor-cap countdown** (D5) - unbuildable
  honestly: no API exposes per-vendor consumption. Phrase caps as "at least N
  periods," never as a live countdown.
- **Persisting the full `CraftingPlanResult`/`PlanSolveContext` in Plan
  History, incl. a "replay pill-clicks on a frozen snapshot" mode** (D3) - large
  per entry, stale within minutes for a Trading-Post-driven module, and for a
  use case the user only floated as a maybe. The small REQUEST + summary is the
  right entry.
- **Reset-to-defaults Settings button** (D1) - Blish's platform settings screen
  already exposes reset; `ResetToDefaults()` is dead code today; duplicating it
  is redundant scope.
- **A Settings-tab Clear Cache button** (D1) - duplicates the Snapshot header
  button one-for-one; textbook settings-soup.
- **`LogMinFileLevel` as a UI control** (D2) - a fifth logging setting for a
  knob that matters only to the maintainer debugging a report; hardcode the Info
  floor.
- **Drag-and-drop watchlist reordering for v1** (D4) - no reorder primitive is
  confirmed anywhere in Blish's documented surface or this codebase; up/down
  buttons are the proven idiom. Drag needs its own feasibility spike first.

Value-positive but **defer, not never** (build later, when justified):

- **Plan History V2 cross-tab reopen** - genuinely wanted, but gated on all six
  view-decomposition waves; revisit after M38.
- **Plan History cost-over-time sparkline *rendering*** (D3 3.7) - capturing the
  `{timestamp, cost}` samples is cheap and worth doing in v1 (a "3 days ago,
  1g20s cheaper" text line needs no chart); building an actual chart primitive
  (zero precedent in this codebase) fails the value test for the common
  single-data-point case. Capture samples, defer the chart.
- **Ranker blended-currency closeness as the *headline* metric** (D4) - depends
  on a user-guessed `CurrencyValuation` rate and risks the invalid-currency-
  comparison invariant; keep coin-denominated as primary, show currencies as
  separate honest lines. A blended *secondary* view could come later.
- **Ranker faked visual progress bar, per-currency progress sub-lines,
  "affordable now" chip, parallel Refresh fan-out** (D4 optional extras) -
  legitimate polish, explicitly cuttable; add after the text-first v1 exists,
  and only measure parallel fan-out if sequential Refresh proves slow (the
  underlying services already serialize internally).
- **`RankerMaxEntries` / Tier-2 "use daily-crafting API" settings** - add only
  when their features exist and real usage justifies them.

---

## Appendix: source proposals

`d1-snapshot-about-settings.md`, `d2-log-system.md`, `d3-plan-history.md`,
`d4-crafting-ranker.md`, `d5-next-step-feasibility.md`, all in this
directory; M38 packages from `../dev-notes/m38-plan/m38-cleanup-plan.md`. All effort classes and
MEASURED claims above are carried from those documents; this synthesis adds no
new code-level measurements of its own beyond cross-checking the four store
corrections and the two collisions (diagnostics toggle, RankerStore filename)
that the proposals themselves already flagged.
