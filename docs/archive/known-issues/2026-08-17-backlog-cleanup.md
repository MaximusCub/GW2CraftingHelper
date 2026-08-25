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
