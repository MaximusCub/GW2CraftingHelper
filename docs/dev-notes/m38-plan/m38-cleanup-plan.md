# M38 Cleanup Wave - Execution Plan

Synthesis of six analyst reports (architecture, simplify, perf, public-repo, tests, csharp)
into a PR-by-PR cleanup wave. Starts **after M37 closes** (i.e. after `m37-homestead` and
`m37-audit-fixes` land). Every package is rebased against final `master` before execution -
see "In-Flight Branch Rebase Flags" below; several packages are gated on those merges.

The maintainer authorized significant rework. This plan does not timidly drop the high-value
structural items (God-class decomposition, pipeline/solver splits). It sequences them **after**
the mechanical safety-net packages (test-infra, analyzers, region markers) so the later refactors
land on a safer base, and it splits the one truly large decomposition (`CraftingPlanView`) into
survivable increments that each keep the build green and the M33 relayout/scroll contracts intact.

Test-count floor: **854 green** (reports measured 850/854; brief said 848). Treat **848+ green,
0 failed, 0 skipped** as the per-PR floor.

---

## EXECUTION PREAMBLE (every implementing agent inherits these rules)

1. **Behavior-preserving by default.** Unless a package's scope explicitly authorizes a behavior
   change (only WP-05 `MapSource default:throw`, WP-16 Release-build logging, WP-10 dead-code
   removal), the observable output, plan math, and UI must be byte-for-byte unchanged. Prove it.
2. **Tests are the floor, not the ceiling.** Run both suites every PR:
   - `<dotnet> build GW2CraftingHelper.csproj -p:Platform=x64`
   - `<dotnet> test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`
   - Tool-test suite where the package touches `tools/` (`tests/VendorOfferUpdater.Tests`).
   848+ green, 0 failed, 0 skipped. A red or newly-skipped test blocks the PR.
3. **Read the invariant docs first.** Before touching any file, read the relevant
   `docs/KNOWN-ISSUES.md` section(s) named in the package and the "DO-NOT-TOUCH" list below.
   The documented machinery is a **constraint, not a target**: move it or comment it, never gut it.
4. **`<dotnet>`/`<gh>` path probing** per `CLAUDE.md` Tool Paths. Windows-style project paths for
   `dotnet.exe` from WSL.
5. **ASCII-only in `.cs`; Allman braces; no em-dash** in source/config/tests. WP-02 makes this
   machine-enforced; until it lands, self-check.
6. **Self-review gate (CLAUDE.md Edit->Review->Fix loop) after every runtime-affecting file.**
   Switch to skeptical-reviewer mode, classify findings Critical/Must-Fix/Nice-to-Have, fix all
   Critical + Must-Fix before moving on. Non-trivial change with zero Nice-to-Have found = review again.
7. **No scope creep.** Do exactly the package. No drive-by refactors, no opportunistic renames, no
   unrelated formatting churn (it manufactures merge conflicts against the parallel packages and the
   hand-maintained `<Compile Include>` list). If you spot something, note it for a later package.
8. **`needsVisualLoop=true` packages** (anything touching `Views/` rendering) require the project's
   live screenshot/pixel-scan verification loop (KNOWN-ISSUES `THE METHOD`, Blish-over-Paint
   protocol) in addition to green tests - the entire view has **zero automated test net**, so green
   tests prove nothing about rendering. Capture before/after on the Exordium reference plan
   (`--profile 2`) and, where scroll/resize is in scope, the ~9,400px two-item batch.
9. **One logical change per commit; PR-per-package** unless a package explicitly says "one PR per
   increment". Follow the strict PR workflow + body template in CLAUDE.md. Do not commit
   intermediate caches (`wiki_vendor_cache.json`, `bin/`, `obj/`).
10. **Rebase against final `master` and re-derive line numbers** before starting any package flagged
    below. Analyst line numbers are current-master facts, not guarantees.

---

## IN-FLIGHT BRANCH REBASE FLAGS (resolve before execution)

**`m37-homestead`** (measured: 7 commits ahead, `dc04c64`) touches `PlanSolver` (new homestead
efficiency-tier solver gating + settings), `Models/HomesteadEfficiencyTiers`, `SettingsInputParser`,
the shipped `ref/*.json` seed data, **both** `VendorOfferHasher` copies (Services + tool), the
`VendorOfferUpdater` tool, `Harness/Program.cs`, and **rewrites/relocates large test files**
(`PlanSolverTests +388`, `CraftingPlanPipelineTests +122`, plus churn in
`DecisionPillPlannerTests`, `InventoryReducerTests`, `MultiItemPlanTests`, `PlanResultBuilderTests`,
`CraftingTreeBuilderTests`, `RecipeCacheSerializerTests`, `AchievementBitDedupPrePassTests`).
Invalidates or must-rebase: **WP-01** (test-helper duplication sites move under homestead's test
churn - re-derive), **WP-08** (RecipeCacheSerializerTests churn), **WP-11/WP-12/WP-13/WP-15**
(PlanSolver/pipeline logic + line numbers), **WP-19** (both hashers edited - golden vectors must be
generated from post-homestead hashers), **WP-20** (PlanSolverTests +388 changes the split seams).
=> All PlanSolver/pipeline/hasher/test-split packages are gated: **do not start until homestead is
merged into master**, then rebase.

**`m37-audit-fixes`** (measured: **0 commits ahead** of master today; worktree exists but empty).
The marshal/error-path audit work is **not yet done**, so it does **not** pre-empt WP-16/WP-17 - but
its eventual scope overlaps them directly (Debug.WriteLine persistence logging, `Module.cs` catch
blocks, `MainThreadMarshal`/FrameTicker error paths). => Before starting **WP-16/WP-17** (and the
`ObserveFault` comment in WP-05), confirm the audit-fixes final diff; if it already added
persistence-store logging or Module.cs catch logging, WP-16/WP-17 are partially or fully pre-empted
and must be re-scoped to only what remains.

---

## WORK PACKAGES

Risk key: **low** = mechanical/behavior-preserving; **medium** = structural, test/harness safety net
required; **high** = touches invariant-adjacent machinery (justified inline).

### WAVE A - Safety net & tooling (do first; makes later refactors safer)

#### WP-01 - Test-infrastructure consolidation
- **Subsumes:** tests T2/T3/T4/T5; simplify #5/#6.
- **Scope:** `tests/GW2CraftingHelper.Tests/Helpers/` - new `RecipeNodeBuilders.cs`
  (`Leaf`/`Craftable`/`Option`/`WrapperOf`, parameterized for the achievement-bit optional args),
  `RepoFileLocator.cs` (`FindRepoFile`), `TempDirectory : IDisposable`. Repoint the byte-identical
  copies in `PlanSolverTests`, `CraftingTreeBuilderTests`, `InventoryReducerTests`,
  `AchievementBitDedupPrePassTests`, `DecisionPillPlannerTests`, `OwnedMaterialsForceBuyPrePassTests`,
  `PlanResultBuilderTests`, `AcquisitionHintServiceTests`, `RecipeCacheSerializerTests`, and the
  ~12 inline temp-dir blocks in `CraftingPlanPipelineTests`/`MultiItemPlanTests`/`RecipeCacheStoreTests`.
  `using System.IO;` cleanup in the two fully-qualified files.
- **Approach:** Move the canonical helper; reconcile the divergent `Craftable` (InventoryReducer's
  auto-`craftsNeeded`) as an optional parameter or keep-local-with-comment. Test-only; zero production risk.
- **Risk:** low. **Verify:** suites green (proves each repoint is equivalent).
- **Deps/order:** Rebase after `m37-homestead` (heavy test churn moves the duplication sites).
  Parallelizable with WP-02/WP-03 once rebased.

#### WP-02 - Analyzers, .editorconfig, csproj hygiene
- **Subsumes:** csharp #3/#4/#12/#13; architecture S8.2.
- **Scope:** root `.editorconfig` (Allman, `_camelCase` fields, `using`-sort - codify existing style);
  `StyleCop.Analyzers` via `packages.config` with a custom ruleset (suppress SA1600/SA1602 doc-mandate
  + file-header rules; keep SA1500/one-statement-per-line/using-order; **disable CA2007**), severity
  **warning not error**; pin `<LangVersion>` in the 3 legacy net48 csprojs; remove vestigial csproj
  cruft (`Prefer32Bit`, empty `DocumentationFile`, dead `CodeAnalysisRuleSet`); a CI/pre-commit grep
  for non-ASCII `.cs`.
- **Approach:** **MEASURE-FIRST** two items before editing: (a) LangVersion - one diag build
  (`-v:diag | grep -i langversion`) to read the currently-resolved value, pin exactly that;
  (b) unused `<Reference>`s (`System.Numerics`, `AsyncClipboardService`) - remove then build to confirm
  the packaged `.targets` import chain doesn't need them.
- **Risk:** low. **Verify:** clean build both configs; suites green; analyzer emits only warnings.
- **Deps/order:** early. **Not** parallelizable with any other csproj-touching package (WP-06/WP-08).

#### WP-03 - CI builds the shipped module
- **Subsumes:** public-repo §7 (CI gap).
- **Scope:** `.github/workflows/tests.yml` - add `dotnet build GW2CraftingHelper.csproj
  -p:Platform=x64` before the test step so a PR can't pass CI while the module fails to build.
- **Risk:** low. **Verify:** workflow green on a throwaway PR. **Deps:** none. Parallelizable.

### WAVE B - Mechanical code cleanups (low risk, behavior-preserving)

#### WP-04 - CraftingPlanView navigation & diag-guard dedupe
- **Subsumes:** architecture S3c/S11; csharp 1e.
- **Scope:** `Views/CraftingPlanView.cs` only. Add `#region` markers matching the 11 responsibilities
  + a one-line `// See docs/KNOWN-ISSUES.md #12/#13/#19/#23` anchor at each machinery region head.
  Add `private bool ScrollDiagEnabled => _settings != null && _settings.ScrollDiagnosticsEnabled.Value;`
  and replace the ~7 verbatim guard expressions.
- **Approach:** Comment/marker insertion + one extracted property. **No code moves, no logic edits.**
- **Risk:** low. **Verify:** build + suites green (region markers don't change rendering; the diag
  property is a pure read-through). **Deps/order:** Prereq for WP-21/WP-23 (makes those diffs reviewable).

#### WP-05 - Cross-cutting intent comments
- **Subsumes:** architecture 2a; simplify #8; csharp 2b.
- **Scope:** doc-comment cross-refs on `Models/AcquisitionSource` <-> `Contracts/CraftingDecision`
  naming `CraftingTreeBuilder.MapSource` as the single bridge; add `default: throw` in `MapSource`
  (fail-loud on a new unmapped source - the one intentional behavior change, on an unreachable path);
  one-line comment at the 3 `ObserveFault` call sites in `CraftingPlanPipeline` explaining the
  fault-observation rationale.
- **Risk:** low. **Verify:** suites green. **Deps/order:** Confirm `m37-audit-fixes` didn't already
  comment `ObserveFault`. Rebase pipeline line numbers after homestead.

#### WP-06 - Contracts/Models coherence
- **Subsumes:** architecture S2; simplify #9 (IItemSearchProvider TODO).
- **Scope:** move `Contracts/CraftingTreeNode.cs` + `Contracts/CraftingDecision.cs` -> `Models/`;
  move `Contracts/StaticItemSearchProvider.cs` -> `Services/` beside `CraftableItemSearchProvider`;
  leave `Contracts/` = `IItemSearchProvider` + `ItemSearchResult` only. Resolve the
  `IItemSearchProvider` rename TODO (rename to `IPlanTargetSearchProvider` **or** delete the TODO -
  do not leave it dangling). Namespace renames + `using` fixes + `<Compile Include>` reorder.
- **Risk:** low (compile-time rename; tests follow via ProjectReference). **Verify:** build + suites green.
- **Deps/order:** touches csproj - serialize against WP-02/WP-08. Do early to de-noise later view diffs.

#### WP-07 - PlanResultBuilder hot-path memoization
- **Subsumes:** perf P1 (dominant 51.4% warm-run cost) + P5 (LINQ churn, same method).
- **Scope:** `Services/PlanResultBuilder.cs` - replace the two unmemoized `FindRecipeOption` DFS
  re-walks with one `Dictionary<int, RecipeOption>` built via a single tree walk at the top of `Build`;
  hoist the per-craft-step `Disciplines.Where(...).ToList()` and gate the debug-log `Select(...).ToList()`
  construction behind an "is anyone reading this" check.
- **Approach:** Behavior-preserving (same options found, same disciplines/recipes derived). This is
  the highest-value perf item - it sits on the synchronous UI-thread pill-click path.
- **Risk:** low. **Verify:** suites green **plus** harness before/after regression proof:
  `Harness --profile 2 --iterations 6 --print-cache-stats`; "Build result" must drop from the dominant
  warm phase toward near-zero with "Solve" becoming largest. **Deps:** none. Parallelizable (disjoint file).

#### WP-08 - Seed loaders: stream deserialize + parse-pin tests
- **Subsumes:** perf P2a (ReadToEnd->string doubling, 4 loaders); tests T7 (3 shipped files unpinned).
- **Scope:** `VendorOfferLoader.Load`, `RecipeCacheSerializer.LoadRecipeSeed`/`LoadSearchSeed`,
  `ItemNameSeedData.Load` - swap `StreamReader.ReadToEnd()` + `Deserialize<string>` for the
  `Deserialize(Stream)` overload (same package version). Add "shipped file still parses via the real
  loader" tests for `ref/vendor_offers.json`, `ref/mystic_forge_recipes.json`, `ref/item_name_seed.json`
  (sane lower-bound count + a couple of stable rows; not full snapshots).
- **Approach:** **Explicitly excludes** perf P2b (moving the load off `Initialize` into `LoadAsync`) -
  that is MEASURE-FIRST + needs a "not ready yet" UX decision; see Measure-First list.
- **Risk:** low. **Verify:** suites green; new parse tests green. **Deps:** rebase T-file targets after
  homestead. Parallelizable with WP-07.

#### WP-09 - Misc low-risk consistency
- **Subsumes:** perf P4 (ItemMetadataService cache); csharp #5 (nullable on net8.0 tools), #7, #11 (structs).
- **Scope:** `ItemMetadataService` `_cache`/`_knownMissing` - add a size cap or session-TTL mirroring
  `TradingPostService`'s documented 15-min pattern, **or** a one-line comment stating the unbounded
  choice is deliberate (either resolves the drift). Mark the 5 layout-math structs (`PillSpec`,
  `TreeColumnEdges`, `CostTileGeometry`, `ColumnEdges`, `RowInput`) `readonly struct`. Enable
  `<Nullable>enable</Nullable>` on the two net8.0 tool projects (`MysticForgeSeeder`, `VendorOfferUpdater`)
  and fix the resulting warnings.
- **Risk:** low. **Verify:** both suites green; tool projects build clean with nullable on.
- **Deps:** VendorOfferUpdater is homestead-touched - rebase. Parallelizable otherwise.

### WAVE C - Pipeline/solver structural (GATED on m37-homestead; test-fenced)

#### WP-10 - Remove dead live-wiki vendor-offer resolver
- **Subsumes:** simplify #1 (delete ~725 lines) **vs** architecture S4c (keep seam + comment). perf and
  csharp both confirm it is runtime-dead (`Module` passes `resolver: null`; no production
  `IWikiVendorClient` impl exists; the runtime no-scrape invariant is already satisfied by the seed-store
  path `VendorOfferLoader`/`VendorOfferStore`; wiki scraping lives in `tools/VendorOfferUpdater`'s own
  `WikiSmwClient`, not this interface).
- **Decision:** proceed with **deletion** - `VendorOfferResolver.cs`, `IWikiVendorClient.cs`,
  `WikiLookupOptions.cs`, `Models/ResolveResult.cs`, `VendorOfferResolverTests.cs`,
  `InMemoryWikiVendorClient.cs`, and the `resolver` param/field/3 dead guard branches in
  `CraftingPlanPipeline`. Bonus: removes the suite's single largest runtime cost (tests T1: 32% of
  wall time is real `Task.Delay` in `VendorOfferResolverTests`).
- **Reviewer GATE (honest dissent):** the architecture analyst wants the seam kept as a documented
  intentional-dead dependency. Deletion wins on the evidence, but this is a maintainer call - if the
  maintainer wants the seam retained, fall back to architecture S4c (keep + add the "always null at
  runtime by design" doc comment) and skip only the deletion, keeping the rest of the wave intact.
- **Risk:** medium (removes wired dependency; branches are unreachable-by-construction so runtime
  behavior is unchanged). **Verify:** suites green after repointing/removing tests. **Deps/order:**
  after homestead; do **before** WP-13 (removing the guards simplifies the dedup).

#### WP-11 - EvaluateVendorOffers: 7 out-params -> result struct
- **Subsumes:** simplify #4.
- **Scope:** `Services/PlanSolver.cs` - introduce a private `readonly struct VendorOfferEvaluation`
  returned by value; the single call site in `Evaluate` destructures it. Mirrors the existing M37
  `PerItemEconomics` pattern.
- **Risk:** low-medium. Do **not** touch the merged-ceil arithmetic (DO-NOT-TOUCH #7) - only the
  parameter-passing shape. **Verify:** `PlanSolverTests`/`MysticForgeIntegrationTests` green (they
  fence this through `Solve`). **Deps:** after homestead (PlanSolver churn); rebase.

#### WP-12 - Extract SellSideEconomics from the pipeline
- **Subsumes:** architecture S4b.
- **Scope:** move `ApplySellSideEconomics`/`ComputePerItemEconomics`/`ComputeMaterialOpportunityCost`/
  `ApplyBatchSellSideEconomics`/`PerItemEconomics` (~340 lines, KNOWN-ISSUES #25) out of
  `CraftingPlanPipeline` into a static Blish-free `Services/SellSideEconomics.cs`.
- **Approach:** pure move; improves testability (becomes directly unit-testable). Optionally add
  targeted unit tests for the extracted arithmetic.
- **Risk:** medium. **Verify:** suites green; pipeline signatures unchanged. **Deps:** after homestead; rebase.

#### WP-13 - Deduplicate the three Generate*Async methods
- **Subsumes:** simplify #2; csharp 1c (triplicated currency-await/catch).
- **Scope:** `CraftingPlanPipeline` - extract shared private helpers
  (`FetchPricedVendorContextAsync`, `AwaitCurrencyMetadataOrNullAsync`, `FetchLearnedRecipeIdsAsync`,
  `FinishTimingLog`) for the verbatim blocks across `GenerateAsync`/`GenerateStructuredAsync`(x2).
- **Approach:** pure extract-method; the M35/M37 byte-identical-output regression tests are the safety net.
- **Risk:** medium. **Verify:** suites green (single-vs-multi byte-identical assertions must hold).
  **Deps:** after homestead **and** after WP-10 (guards gone) - rebase.

#### WP-14 - Delete legacy GenerateAsync, repoint its tests
- **Subsumes:** architecture S4c (GenerateAsync is test-only: 10 call sites, all tests; violates
  "tests exercise real production paths").
- **Scope:** delete `CraftingPlanPipeline.GenerateAsync`; repoint the ~10 tests in
  `CraftingPlanPipelineTests`/`MysticForgeIntegrationTests` at the production
  `GenerateStructuredAsync` (single-item list short-circuits to the same core).
- **Risk:** medium (isolated test-repointing). **Verify:** suites green. **Deps:** after homestead +
  after WP-13 (so the surviving method is already deduped). Its own PR (diff should read as pure
  test-repointing).

#### WP-15 - Extract VendorBatchSolver from PlanSolver
- **Subsumes:** architecture S4a.
- **Scope:** move the vendor-batching sub-engine (`VendorOfferBatch`/`VendorBatchState`,
  `EvaluateVendorOffers`, `FinalizeVendorBatches`, `AllocateVendorNodeCosts`,
  `MergeVendorCurrencyCosts`, `VendorBatchesEqual`, `ScaleCostLines`) into
  `Services/VendorBatchSolver.cs`, injected into `PlanSolver`; keep `Solve`'s signature.
- **Approach:** the merged-ceil arithmetic itself is DO-NOT-TOUCH (#7) - this is a move, not a rewrite.
  The 2705-line `PlanSolverTests` fences it well (safest large split).
- **Risk:** medium. **Verify:** suites green; harness output identical on `--profile 2`. **Deps:** after
  homestead + after WP-11 (struct already in place); rebase. Builds on WP-01's shared test helpers.

### WAVE D - Logging & teardown (confirm m37-audit-fixes final scope first)

#### WP-16 - Persistence-store failure logging (Release no-op fix)
- **Subsumes:** csharp 1a (Must Fix - `Debug.WriteLine` erased in Release, 14 sites, 4 stores).
- **Scope:** add `Action<string, Exception> onError = null` (no-op default) to `SnapshotStore`,
  `StatusStore`, `VendorOfferStore`, `OverlayRecipeCacheStore`; call it instead of `Debug.WriteLine`
  on each catch path; `Module.cs` wires `Logger.Warn` at construction.
- **Approach:** preserves the Blish-free test boundary (no direct `Blish_HUD` reference in the stores;
  parameterless ctor keeps tests Blish-free). One behavior change: Release users now get a log line
  where they previously got silence - the point of the fix.
- **Risk:** medium (Must Fix). **Verify:** suites green; optional new test asserting the callback fires
  on a forced failure path. **Deps:** **confirm audit-fixes didn't already add this**; re-scope if so.

#### WP-17 - Module.cs catch-consistency + FrameTicker unload teardown
- **Subsumes:** csharp 1b (6 silent catches vs 2 logged) + csharp 3b/10 (FrameTickers not stopped on unload).
- **Scope:** convert the 6 bare `catch` in `Module.cs` to `catch (Exception ex)` + the same
  `Logger.Info("<fallback>: [{0}] {1}", ...)` shape the two sibling sites already use; add
  `StopLiveTickers()` to `CraftingPlanView` (cancel the 3 verify/debounce tickers) and call it from
  `Module.Unload()`.
- **Approach:** does not touch the FrameTicker mechanism itself, only adds a deliberate teardown call.
- **Risk:** low. **Verify:** suites green; module enable/disable smoke check for no spurious
  "FrameTicker step failed" warning. **Deps:** confirm audit-fixes scope first. `needsVisualLoop` optional
  (unload path - a smoke check suffices).

### WAVE E - Test coverage & structure

#### WP-18 - Close pipeline-level coverage gaps
- **Subsumes:** tests T6 (Ignore x owned untested), T8 (no pipeline cancellation/failure tests), T9
  (`InMemoryPriceApiClient` lacks failure injection).
- **Scope:** add `ThrowOnCallNumber`-style failure injection to `InMemoryPriceApiClient` (parity with
  its 3 siblings); add a pipeline cancellation test and a "dependency throws mid-generation" test to
  `CraftingPlanPipelineTests`; add at least one Ignore+partial/full-ownership test at both the
  `PlanSolver`/`CraftingPlanPipeline` layer and the `DecisionPillPlanner` display layer (proving the
  documented dual-mechanism interaction is deliberate, per KNOWN-ISSUES 20.4).
- **Risk:** low-medium (new tests + one fixture hook; no production change). **Verify:** suites green;
  new tests fail-loud when the behavior they pin is broken. **Deps:** after homestead (CraftingPlanPipelineTests
  churn); builds on WP-01 helpers. Parallelizable with docs packages.

#### WP-19 - VendorOfferHasher cross-suite golden-vector parity
- **Subsumes:** tests T13 (two independently-maintained hashers, one per TFM, zero proof they agree).
- **Scope:** a shared `{inputs, expectedOfferId}` golden-vector fixture loaded by **both**
  `tests/GW2CraftingHelper.Tests` and `tests/VendorOfferUpdater.Tests`, so a divergence in either
  `VendorOfferHasher` copy fails in both suites at once.
- **Risk:** medium (cross-project test-infra). **Verify:** both suites green; hand-verify a deliberate
  edit to one hasher turns both suites red. **Deps:** **after homestead** - homestead edits both hasher
  copies (12-line digest change), so vectors must be generated from the post-homestead algorithm.

#### WP-20 - Split the two test monoliths
- **Subsumes:** tests T12 (`PlanSolverTests` 2705 lines / `PlanViewModelBuilderTests` 1770 lines are
  merge hotspots).
- **Scope:** split each along its existing `// --- Section (M-nn) ---` banners into focused files
  (e.g. `PlanSolverVendorBatchingTests`, `PlanSolverIgnoreTests`), routing shared private helpers
  through WP-01's `Helpers/`.
- **Approach:** mechanical but needs care not to duplicate/drop a helper in the split.
- **Risk:** medium (REDESIGN-adjacent test refactor). **Verify:** identical test count before/after,
  suites green. **Deps:** **after homestead** (PlanSolverTests +388) + after WP-01/WP-15.

### WAVE F - Tier-1 view extraction (needsVisualLoop; near-zero-risk static primitives)

#### WP-21 - Extract Tier-1 static renderers from CraftingPlanView
- **Subsumes:** architecture S3b-T1; simplify #3 (helper region).
- **Scope:** move the ~500-700 lines of `static`, instance-state-free rendering primitives into
  `Views/Rendering/`: `CoinCurrencyRenderer` (coin+currency segment build/layout/reposition,
  `ValueCellHandle`, `MeasureValueWidth`, `FormatCoinText`, `GetCoinColor`), `RarityColors`,
  `IconControls`, `LabelHelpers` (`EllipsizeToWidth`, `CreateRightAlignedLabel`, `CreateSmallTag`,
  `CreateRowDivider`). `private static` -> `internal static`; section builders keep calling them.
- **Approach:** the coin-icon-right-of-number invariant now lives in ONE named place. `CreateRowDivider`'s
  divider math is DO-NOT-TOUCH #6 - move the method verbatim, do not alter the constants. Where any
  segment-width arithmetic can be made XNA-free, add a Blish-free unit test (mirrors `ShoppingColumnMath`)
  to convert previously-untested view logic into tested helper logic.
- **Risk:** medium (mechanical, but the view has no test net). **Verify:** suites green + **visual loop**
  on `--profile 2` (coin/currency cells, rarity frames, dividers pixel-identical). **Deps:** after WP-04.

#### WP-22 - Repoint MainView coin rendering; kill the duplicate
- **Subsumes:** architecture S6 (byte-identical `GetCoinColor`/`AddCoinSegment` duplicated across views).
- **Scope:** `Views/MainView.cs` - delete its private coin color/segment code, call the WP-21
  `CoinCurrencyRenderer` (left-anchored mode). Removes the second independent encoding of the coin invariant.
- **Risk:** low-medium. **Verify:** suites green + **visual loop** on both the snapshot tab (MainView coin
  panel) and the crafting plan. **Deps:** WP-21.

### WAVE G - Tier-2/3 view decomposition (highest structural value; needsVisualLoop; survivable increments)

#### WP-23 - Section renderers behind ISectionRelayoutSink (incremental, one section per PR)
- **Subsumes:** architecture S3b-T2; simplify #3 (partial-class split); public-repo §10; perf "Other".
- **Scope:** introduce `interface ISectionRelayoutSink { AddRelayout(Action<int>); AddReellipsis(Action<int>); }`
  implemented by `CraftingPlanView` over its existing two registries. Extract each section builder into its
  own renderer class (`DisciplinesSectionRenderer`, `UsedMaterialsSectionRenderer`,
  `ShoppingListSectionRenderer`, `CraftingStepsSectionRenderer`, `RecipesSectionRenderer`,
  `SummarySectionRenderer`) that pushes closures into the sink instead of the view's private fields.
- **Approach:** **one section per PR, smallest first** (pilot = `DisciplinesSectionRenderer`, ~40 lines).
  This preserves the M33 C2b contract (build path and relayout path must never drift): the ~20
  `_relayoutActions.Add`/`_reellipsisActions.Add` sites and the DEBUG must-register / scroll-neutral
  invariants stay intact, threaded through the sink. Tests are upstream of the view and won't catch a
  regression, so **every increment is live-verify-gated**.
- **Risk:** medium-high (invariant-adjacent: the relayout registry is documented-essential, KNOWN-ISSUES
  #13/#19). Justified: this is the core of the 4802-line God-class and the stated structural goal; splitting
  it per-section with a live gate each is the survivable path, and each PR keeps the build green.
- **Verify:** suites green + **visual loop** on `--profile 2` and the ~9,400px two-item batch (relayout on
  resize-drag, scroll neutrality) for **every** increment. **Deps:** after WP-04 + WP-21. Serialize the
  increments (shared file).

#### WP-24 - Factor the repeated row-builder shape
- **Subsumes:** simplify #3 (CreateXRow boilerplate); architecture S3b.
- **Scope:** two shared helpers - "icon + ellipsized name label + right-aligned secondary label (+ its
  reellipsis closure)" and "row panel + divider + size-only relayout closure" - called from each
  `CreateUsedMaterialRow`/`CreateShoppingRow`/`CreateCraftStepRow`/`CreateDisciplineRow`/`CreateRecipeRow`.
- **Approach:** does **not** touch `PlanContentHeightMath`/`PlanRelayoutMath` (DO-NOT-TOUCH #4/#5) - only
  the boilerplate wrapper around calling them.
- **Risk:** medium. **Verify:** suites green + **visual loop** (every row type, ellipsis + divider).
  **Deps:** after WP-23 (rows have moved to their renderer files).

#### WP-25 - Extract the tree section controller
- **Subsumes:** architecture S3b-T2 (hardest); perf P1 context (ApplyOverridesAndResolve path).
- **Scope:** move the tree renderer + interactive-override loop (`TreeNodeState`, `CreateTreeSection`,
  `RenderTreeNode`, `RefreshTreeContainerHeights`, `UpdateTreeRowTooltip`, `ApplyPreset`,
  `ApplyOverridesAndResolve`, and the `_treeNodeStates`/`_nodeOverrides`/`_ignoredItemIds`/`_lastResult`
  state) into a `TreeSectionController` taking the `ResolveWithOverrides` delegate the view already receives.
- **Risk:** high (this component owns a slice of application state, not just presentation; it is the one
  place the clean MVVM flow is muddied). Justified: it is the last big block keeping `CraftingPlanView`
  oversized, and isolating it is what makes the override loop testable. **Verify:** suites green +
  **visual loop** on pill-click / Best Path / Ignore-toggle / Expand-Collapse. **Deps:** after WP-23.

#### WP-26 - Scroll/Resize/Wheel controller (move-only) - CUT-FIRST candidate
- **Subsumes:** architecture S3b-T3.
- **Scope:** move the scroll preserve/restore/verify, wheel-wrap correction, resize relayout, and the
  `FrameTicker`(s) into a `ScrollController`/`ResizeReflowController` collaborator - **as a pure move, no
  logic edits.**
- **Risk:** high - the single riskiest change in the repo. The exact frame-timing, subscription-order, and
  synchronous-registration guarantees (KNOWN-ISSUES #12/#13, DO-NOT-TOUCH #1-#6) are asserted
  by-construction, not by tests. **If M38 must cut scope, cut this first** - WP-21+WP-23+WP-25 already get
  the file well under 2000 lines. **Verify:** suites green + **exhaustive visual loop** (fast wheel-up,
  drag-resize on the large plan, pill-click viewport-flash) as the acceptance gate. **Deps:** last view package.
- **DECISION 2026-07-23: CUT.** Rationale: (1) highest-risk change in the repo with zero functional payoff -
  the move is purely organizational; (2) the guarantees it would relocate are asserted by construction and
  timing, not tests, so a regression would only surface in live use; (3) WP-21/WP-23a-d/WP-25 already
  achieve the maintainability goal (view file well under 2000 lines); (4) the machinery is fully
  region-mapped with KNOWN-ISSUES anchors (#12/#13/#14/#19/#23), so future maintainers can navigate it
  in place; (5) the acceptance gate would require a human drag-resize pass (synthetic drags proven
  unreliable), making the verification cost disproportionate. The ReplayRelayout SuspendLayout/ResumeLayout
  MEASURE-FIRST item loses its purpose (it existed to precede WP-26) and downgrades to an optional
  Nice-to-Have perf close-out. Repo-side record: appended to docs/KNOWN-ISSUES.md on the WP-25 branch.

### WAVE H - Public-repo readiness (docs; parallelizable with everything)

#### WP-27 - Documentation restructure & architecture surfacing
- **Subsumes:** public-repo §1/§2/§4/§9 (partial); architecture S8.4; csharp §12 flag context.
- **Scope:** rewrite `README.md` (headline = crafting-plan solver, not inventory cache; screenshots;
  CI badge; end-user install section; link the gw2e parity spec). New `docs/ARCHITECTURE.md` extracting
  the durable "why" for each essential-complexity piece (FrameTicker/MainThreadMarshal, WheelDeltaSanitizer,
  scroll-restore contest, PlanContentHeightMath, PlanRelayoutMath, merged-ceil batching) with `.cs`
  cross-references + one-line class-doc pointers back to it. Split `docs/KNOWN-ISSUES.md`: keep a short
  current-issues list (the existing DEFERRED tail is already public-appropriate); move the historical
  fix-pass diary to `docs/dev-notes/`; **remove** the AI-session-handoff tail (personal `C:\Dev\...` paths,
  agent-orchestration notes). Add `docs/research/README.md` framing + scrub dangling scratchpad path refs.
  Refresh `ROADMAP.md` status or replace with a `CHANGELOG.md`; move `docs/icon_candidates.html` to `docs/archive/`.
- **Approach:** relocate/re-title the technical rationale - **do not reword or delete** the essential-complexity
  evidence trail. Docs-only; can be split into README / ARCHITECTURE / KNOWN-ISSUES-split sub-PRs.
- **Risk:** low. **Verify:** links resolve; no personal paths remain (grep); no code touched.
  **Deps:** none. Parallelizable.

#### WP-28 - Community scaffolding & repo hygiene
- **Subsumes:** public-repo §3/§5/§6/§7/§8/§10.
- **Scope:** add `CONTRIBUTING.md` (platform-neutral build/test, style summary, PR expectations - split
  from CLAUDE.md's agent-specific tooling), `.github/PULL_REQUEST_TEMPLATE.md` (reuse CLAUDE.md's PR body),
  `.github/ISSUE_TEMPLATE/*`, `CODE_OF_CONDUCT.md`, `SECURITY.md` (API-scope disclosure). Resolve
  `ref/wiki_vendor_cache.json` (18MB dev-only, never shipped): gitignore going forward + reconcile
  CLAUDE.md's contradicted cache rule + add the `.gitignore` guard. Verify `manifest.json`
  `"directories":["data"]` vs actual `ref/` via a real `.bhm` pack-and-load (MEASURE-FIRST) and fix or
  remove the vestigial key. Add a `docs/RELEASING.md` + optional CI release step. Add `MysticForgeSeeder`
  to `GW2CraftingHelper.sln`; add READMEs to the 3 undocumented tools. Reconcile
  "Lachlan Mulcahy" vs "MaximusCub" branding.
- **GATE (maintainer sign-off):** deleting the stale `v1.0.0`/`v2.0.0` template tags and any
  `wiki_vendor_cache.json` **history rewrite** are disruptive (force-push, breaks clones/forks) - do not
  fold into a routine PR; get explicit approval. Going-forward gitignore does not need the gate.
- **Risk:** low (files) / gated (tags+history). **Verify:** GitHub Community-Standards checklist green;
  `.sln` opens all tools; `.bhm` pack verified. **Deps:** none. Parallelizable (mostly non-code).

---

## DO-NOT-TOUCH (documented-essential complexity - constraint, not target)

Move or comment only; never edit the logic. Doc reference in parentheses.

1. **`WheelDeltaSanitizer`** thresholds/classification - shipped-Blish-binary WheelDelta getter bug
   (KNOWN-ISSUES #12; csharp §5/`Explicitly sound`).
2. **`FrameTicker` / `MainThreadMarshal`** - Blish's XNA host has no `SynchronizationContext`; multi-frame
   work cannot use `QueueMainThreadUpdate` (KNOWN-ISSUES #12/#13; csharp "confirmed sound").
3. **Scroll preserve/restore/verify** (`PreserveScrollAcross`, `StartScrollVerify`, `PanelScrollbarField`
   reflection) - the blind-overwrite window (KNOWN-ISSUES #12/#14/#19).
4. **Resize relayout registry** (`_relayoutActions`/`_reellipsisActions`, `ReplayRelayout`,
   `PlanRelayoutMath`) + the DEBUG must-register/scroll-neutral invariants (KNOWN-ISSUES #13/#19).
5. **`PlanContentHeightMath`** synchronous height contract (KNOWN-ISSUES #12/#19).
6. **`CreateRowDivider`** divider math + 1px scissor clearance (KNOWN-ISSUES #23 / M36b follow-up).
7. **Merged-ceil vendor batching** arithmetic (`PlanSolver.FinalizeVendorBatches`, the ceil math inside
   `EvaluateVendorOffers`) - Obsidian Shard 179->180-not-186 (KNOWN-ISSUES #20.1/#20.2/#28). WP-11/WP-15
   may restructure the *shape* around it (out-params, class move) but never the arithmetic.
8. **`StatusUpdateGuard`** race logic (KNOWN-ISSUES #20.3).
9. **`AchievementBitDedupPrePass`** (KNOWN-ISSUES #26).
10. **`ObserveFault`** fault-observation (csharp 2b) - add a comment (WP-05), do not remove.
11. **`SuggestionPanel.OnTextChanged` `async void`** - already exemplary; do not "fix" (csharp 2a).
12. **Absence of `ConfigureAwait`** - correct for a no-SynchronizationContext host; do not add;
    disable CA2007 in WP-02 (csharp 2c).
13. **Exact-count seed-pin tests** (`recipes_seed.json` == 14736 etc.) - working-as-intended trip-wires;
    update the expected count when the seed legitimately changes, do not delete (tests T10).
14. **`net48` TFM on the main module** - Blish HUD 1.3.0 / MonoGame platform constraint (csharp §10).
15. **Dual `CraftingDecision` / `AcquisitionSource` enums** - keep both (solver vs display vocabulary);
    WP-05 adds only the cross-reference (architecture 2a, simplify #8).
16. **SDK-style csproj migration & nullable-on-main-module** - explicitly OUT of M38; separate milestone
    with an in-Blish load-test gate (architecture S8.1b/S8.3, csharp §4).

## MEASURE-FIRST (perf/tooling items that start with a measurement, not a change)

- **RenderPlan full rebuild on pill click** (perf P3): live Stopwatch around `ApplyOverridesAndResolve`
  on the ~9,400px two-item batch **before** any restructure. Likely no change needed (discrete, human-paced
  interaction). Do **not** extend the M33 relayout-registry to this path without evidence. No WP owns a
  rewrite here - it is a measurement that could green-light or (likely) close the item.
- **ReplayRelayout SuspendLayout/ResumeLayout** (perf P3): live drag-resize Stopwatch on the same large
  plan to close the code's own self-flagged "reasoned, not measured" PERF CAVEAT **before** WP-26 touches
  the file it lives in.
- **Seed load in `Initialize` -> `LoadAsync`** (perf P2b): Stopwatch + `ManagedThreadId` log at module
  enable to confirm it blocks the UI thread, plus a "not ready yet" UX decision, **before** deciding the
  move is worth doing. Explicitly excluded from WP-08.
- **LangVersion resolved value** (csharp §10, in WP-02): one diag build to read it before pinning.
- **Unused csproj `<Reference>`s** (csharp §11, in WP-02): remove then build - the packaged `.targets`
  import chain may need one despite zero source usages.
- **`manifest.json "directories":["data"]`** (public-repo §6, in WP-28): a real `.bhm` pack-and-load to
  learn whether the key is load-bearing or vestigial before editing it.

---

## PRIORITIZATION RATIONALE (value x risk-adjusted confidence)

- **Wave A first** (test-infra, analyzers, CI): highest confidence, unblocks everything. Centralizing
  test helpers and turning the written style rules into machine checks makes every later refactor's diff
  smaller and safer; CI-builds-the-module closes a hole where a green CI could hide a broken build.
- **Wave B next** (mechanical, behavior-preserving): WP-07 (memoization) is the single highest
  value/confidence code change - a measured 51% of warm-run cost on a UI-thread path, fixed mechanically
  with a harness before/after proof. Region markers (WP-04) are a near-free prerequisite for the view work.
- **Wave C/D gated on the in-flight merges** - sequenced after homestead/audit-fixes to avoid guaranteed
  conflicts in PlanSolver/pipeline/stores; each is test-fenced.
- **Wave E** (test coverage) is deliberately placed before the deep view decomposition so the pipeline/solver
  behavior is better pinned before Wave G rearranges the code around it.
- **Waves F/G last** - the God-class decomposition is the biggest structural payoff and the biggest risk.
  Tier-1 (F) is near-zero-risk and immediately halves the file; Tier-2/3 (G) is invariant-adjacent and
  live-verify-gated per increment, with WP-26 explicitly marked cut-first if scope must shrink.
- **Wave H** (docs) runs in parallel with all of it - it is almost entirely additive/organizational and
  gates only on the maintainer sign-off for tag deletion / history rewrite.
