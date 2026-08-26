> **Frozen record - committed 2026-08-17, written for the M38 cleanup wave.** M38 cleanup analysis, architecture lens, read-only against master at commit 85a738e.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

# M38 Cleanup Analysis - A1: Architecture & Structure

Analyst lens: **Architecture and structure**. Read-only pass over current `master`
(commit 85a738e). Feeds the M38 synthesis / PR-by-PR plan.

Every performance claim is tagged **MEASURED** (I ran/counted it) or **INFERRED**
(code reading). Every structural proposal is tagged **CLEANUP** (behavior-preserving,
mechanical) or **REDESIGN** (structural, needs careful review). Localization is out
of scope: string-hardcoding patterns are *flagged*, not proposed for fixing.

---

## 0. Method and what I measured

- Line counts across the real `master` production tree (excluded `.claude/worktrees/*`
  stale agent copies, `bin/`, `obj/`). **MEASURED.**
- Member/region inventory of the big files via signature grep. **MEASURED.**
- Namespace-level dependency-direction scan (`using GW2CraftingHelper.*` and
  `using Blish_HUD`) to detect layer inversions. **MEASURED.**
- Cross-referenced the scroll/wheel/relayout machinery against
  `docs/KNOWN-ISSUES.md` items 12, 13, 14, 19, 23 and `docs/gw2e-parity-spec.md`
  to separate essential from accidental complexity.
- Test topology: `[Fact]`/`[Theory]` count = **850 MEASURED** (873 incl. `InlineData`;
  the brief's "848 tests" is the same order and I treat 850 as the green-bar target).

**Headline sizes (MEASURED, production only):**

| File | Lines | Role |
|---|---:|---|
| `Views/CraftingPlanView.cs` | **4802** | God class (see S3) |
| `Services/PlanSolver.cs` | 1582 | craft/buy/vendor solver |
| `Services/CraftingPlanPipeline.cs` | 1463 | orchestration + sell-side economics |
| `Services/PlanViewModelBuilder.cs` | 604 | result -> view-model |
| `Module.cs` | 593 | composition root |
| `Services/RecipeService.cs` | 454 | recipe tree fetch/cache |
| `Views/MainView.cs` | 449 | snapshot tab |
| `Services/InventoryReducer.cs` | 409 | owned-material reduction |

Everything else is < ~320 lines and mostly well-scoped. The mass is concentrated
in four files; `CraftingPlanView.cs` alone is 26% of production LOC (4802 / 18373).

---

## 1. The real layering (and why it is mostly a strength)

Namespaces map to folders: `Module` (root) -> `Views` -> `Services`
(+ `Services.Recipes`, `Services.Diagnostics`) -> `Contracts` / `Models`.

**Dependency-direction scan came back clean (MEASURED):**
- No `Services` file imports `Views`. No inversion.
- No `Models` or `Contracts` file imports `Services` or `Views`.
- Only **3** `Services` files touch Blish at all: `Gw2AccountRecipeClient`,
  `Gw2AccountSnapshotService`, `ModuleSettings`. These are the legitimate
  Blish/Gw2Sharp adapter boundary (account API, settings wrapper). The
  HTTP-based API clients (`Gw2RecipeApiClient`, `Gw2PriceApiClient`,
  `Gw2ItemApiClient`) hit `api.guildwars2.com` directly and are Blish-free.

This is genuinely good and worth stating in the synthesis as a **preserve-this**
baseline: the domain/solve/pricing core is ~54 files that are Blish-free and
unit-testable, which is exactly why 850 Blish-free tests are even possible. The
`StatusUpdateGuard`, `PlanContentHeightMath`, `PlanRelayoutMath`, `ScrollMath`,
`WheelDeltaSanitizer`, `ShoppingColumnMath` helpers are pure C# extractions of
what would otherwise be untestable view logic - that pattern is the codebase's
best architectural idea and the cleanup should *extend* it, not fight it.

One thing I checked and **cleared** (no finding): `MainThreadMarshal` lives in
`Views/` and is referenced by `Services/StatusUpdateGuard.cs`, which looked like a
Services->Views inversion. It is not: the reference is a doc-comment mention only;
`StatusUpdateGuard` is Blish-free. `MainThreadMarshal`'s real consumers are all in
`Views/` + `Module.cs`. Placement is fine.

---

## 2. Contracts vs Models: the split is incoherent (CLEANUP + small REDESIGN)

`Contracts/` currently holds a grab-bag that does not match "contracts":

| File | What it actually is |
|---|---|
| `CraftingDecision.cs` | a display-facing enum |
| `CraftingTreeNode.cs` | a mutable domain/UI node class (setters, 100 lines) |
| `IItemSearchProvider.cs` | interface + `ItemSearchResult` DTO |
| `StaticItemSearchProvider.cs` | **a concrete implementation** |

Meanwhile the *other* implementation of the same interface,
`CraftableItemSearchProvider.cs`, plus `ItemSearchProviderFactory.cs`, live in
`Services/`. So one `IItemSearchProvider` impl is in `Contracts`, its sibling is in
`Services`. `StaticItemSearchProvider`'s own doc-comment calls itself a "temporary
default ... development placeholder," and `IItemSearchProvider.cs` carries a
`TODO: Consider renaming to IPlanTargetSearchProvider`. This is drift, not design.

`CraftingTreeNode` is a domain data carrier that lives in `Contracts` while every
peer data carrier (`RecipeNode`, `CraftingPlanResult`, `PlanViewModel`, etc.) lives
in `Models`. The only thing genuinely "contract-shaped" here is `IItemSearchProvider`.

**Target shape:** keep `Contracts/` for interfaces/seams only (`IItemSearchProvider`
+ `ItemSearchResult`). Move `CraftingTreeNode` and `CraftingDecision` to `Models/`
alongside their peers. Move `StaticItemSearchProvider` to `Services/` next to
`CraftableItemSearchProvider` and the factory, so all provider impls sit together.

**Migration (one PR, CLEANUP):** move 3 files, update `namespace`
`GW2CraftingHelper.Contracts` -> `.Models`/`.Services`, fix `using`s, reorder the 4
`<Compile Include>` lines. Tests reference these via `ProjectReference`; a namespace
move is a compile-time rename that the test project follows. **Risk: low** (pure
move; the churn is `using` lines and csproj). Do it early - it de-noises every later
diff that touches these types.

### 2a. Two parallel decision enums (INFERRED - justified, but under-guarded)

`Contracts/CraftingDecision` {Craft, BuyFromTp, BuyFromVendor, Have, Currency,
Unknown} and `Models/AcquisitionSource` {BuyFromTp, Craft, Currency, BuyFromVendor,
UnknownSource} are near-parallel, bridged by `CraftingTreeBuilder.MapSource`. This
is *not* pure duplication: `CraftingDecision` adds display-only states (`Have`) the
solver's `AcquisitionSource` has no concept of. The split (solver vocabulary vs
display vocabulary) is defensible. **Do not merge them.** The real risk is silent
drift: `MapSource` is a hand-written switch with no exhaustiveness guard, so a new
`AcquisitionSource` member compiles clean while falling through to a default. Flag
as: add a comment on both enums pointing at `MapSource` as the single bridge, and
consider a `default: throw` in `MapSource` so a new source fails loud. **CLEANUP,
low risk.**

---

## 3. The God class: `Views/CraftingPlanView.cs` (4802 lines) - REDESIGN, staged

This is the central finding. **MEASURED:** 4802 lines, **zero `#region` markers**,
**35 static methods**, plus ~10 nested types (`ItemRowState`, `FrameTicker`,
`SectionHeaderHandle`, `CostTileHandle`, `TreeNodeState`, `CoinSegmentSpec`,
`SegmentLayoutHandle`, `CurrencySegmentSpec`, `ValueCellHandle`, `TopRegionLayout`).
By inspection it carries at least **eleven** distinct responsibilities:

1. **Input rows** (`ItemRowState`, `RebuildItemRowControls`, `CreateItemRowControls`,
   `AddItemRow`, `RemoveItemRow`, `ReflowInputRegion`) - multi-item entry UI.
2. **Generate orchestration** (`TriggerGenerate`, ~180 lines: async, sequencing,
   Mumble character read, status wiring).
3. **Scroll preserve/restore/verify** (`PreserveScrollAcross`,
   `ApplySavedScrollSynchronously`, `StartScrollVerify`, `MeasureContentHeight`,
   `PanelScrollbarField` reflection). **ESSENTIAL** (KNOWN-ISSUES #12/#14/#19).
4. **Wheel-wrap correction** (`OnContentWheelObserved`, `ApplyWheelWrapCorrection`,
   `StartWheelWrapVerify`, `OnScrollDiagWheelScrolled`). **ESSENTIAL** (#12 reopened,
   Blish binary bug).
5. **Resize relayout** (`OnPanelResized`, `PreserveScrollAcrossResize`,
   `StartResizeScrollVerify`, `ReplayRelayout`, `ResizeSettleStep`, `RunReellipsis`
   + the `_relayoutActions`/`_reellipsisActions` registries). **ESSENTIAL** (#13).
6. **The `FrameTicker` control** (nested `Control` subclass driving multi-frame work
   because Blish has no `SynchronizationContext`). **ESSENTIAL.**
7. **Section builders** - the largest cluster: `CreatePlanHeader`,
   `CreateSectionHeader`, `CreateCollapsibleSection`, `CreateUsedMaterials*`,
   `CreateShoppingList*`, `CreateShoppingRow`, `CreateCraftingSteps*`,
   `CreateCraftStepRow`, `CreateDisciplines*`, `CreateRecipes*`, `CreateCostTileRow`,
   `CreateSummarySectionBody`, `CreateCurrencyRow`, `CreateTextRow` (~lines
   2140-3440).
8. **Tree rendering** (`TreeNodeState`, `CreateTreeSection`, `RenderTreeNode`,
   `RefreshTreeContainerHeights`, `UpdateTreeRowTooltip`, `ApplyPreset`,
   `ApplyOverridesAndResolve`) - lazy-expand tree + override resolve loop.
9. **Decision pills** (`GetPillColors`, `RenderDecisionPills`).
10. **Coin/currency value rendering primitives** (`FormatCoinText`,
    `BuildCoinSegments`, `LayoutCoinSegments`, `RepositionSegments`, `GetCoinColor`,
    `BuildCurrencySegments`, `LayoutCurrencySegments`, `ValueCellHandle`,
    `RenderValueCellRightAligned`, `RepositionValueCellRightAligned`,
    `MeasureValueWidth`) - ~500 lines, mostly `static`.
11. **Generic control/format helpers** (`EllipsizeToWidth`, `GetRarityBorderColor`,
    `GetRarityNameColor`, `CreateRarityFramedIcon` x2, `CreateItemIcon`,
    `CreateSmallTag`, `CreateRowDivider`, `CreateRightAlignedLabel`).

### 3a. The constraint that makes this hairy (do not fight it)

Responsibilities 3-6 are **documented essential complexity** and must not be gutted.
Critically, the section/tree builders (7, 8) are **not independent** of the resize
machinery (5): **MEASURED**, `_relayoutActions.Add(...)` / `_reellipsisActions.Add(...)`
is called at ~20 sites *inside* the builders (e.g. `CreateShoppingRow` @2782/2905,
`CreateCraftStepRow` @3044, `CreateCurrencyRow` @3437, `RenderDecisionPills` @4023,
tree @3548/3577). Each closure captures local control references and repositions them
for a new width. `RenderPlan` (@2065) clears both registries per render; a DEBUG
invariant fires if a section renders rows but registers no relayout closure
(@2481), and another asserts no closure ever moves the scrollbar (@1750). This is
the M33 C2b contract: **build path and relayout path must never drift**, which is why
the width math already lives in the pure `PlanRelayoutMath`/`ShoppingColumnMath`.

Implication: you cannot lift a section builder into its own class by copy-paste,
because it needs to (a) push into the two registries, (b) call the shared coin/currency
layout primitives, (c) honor the synchronous-height contract of `PlanContentHeightMath`.
Any extraction must thread those seams explicitly.

### 3b. Extraction candidates, ranked by risk

**Tier 1 - static primitives, near-zero risk (CLEANUP).** The ~500 lines of coin/
currency segment rendering (responsibility 10) and the generic helpers (11) are
`static` and touch **no instance state**. **MEASURED:** 35 static methods total.
Move them to dedicated static classes:
- `Views/Rendering/CoinCurrencyRenderer.cs` (coin+currency segment build/layout/
  reposition, `ValueCellHandle`, `MeasureValueWidth`, `FormatCoinText`, `GetCoinColor`).
- `Views/Rendering/RarityColors.cs` (`GetRarityBorderColor`, `GetRarityNameColor`).
- `Views/Rendering/IconControls.cs` (`CreateRarityFramedIcon`, `CreateItemIcon`).
- `Views/Rendering/LabelHelpers.cs` (`EllipsizeToWidth`, `CreateRightAlignedLabel`,
  `CreateSmallTag`, `CreateRowDivider`).

These are `private static` today; make them `internal static` on the new class. The
section builders keep calling them (now `CoinCurrencyRenderer.LayoutCoinSegments(...)`).
Relayout closures that call `RepositionSegments`/`RepositionValueCellRightAligned`
still work - those become static calls on the handle-owning class. **Risk: low**;
this is mechanical and immediately removes ~700-900 lines from the God class with no
behavior change. It also makes the coin invariant (icon-right-of-number) live in ONE
named place instead of being smeared through the view (see S6 - the duplication fix
depends on this extraction existing).

**Tier 2 - section builders behind a registry sink, moderate risk (REDESIGN).**
Introduce a small seam so builders can move out while preserving the M33 contract:

```
interface ISectionRelayoutSink {
    void AddRelayout(Action<int> closure);
    void AddReellipsis(Action<int> closure);
}
```

`CraftingPlanView` implements it (backed by the existing two lists). Each section
builder becomes a class (`UsedMaterialsSectionRenderer`, `ShoppingListSectionRenderer`,
`CraftingStepsSectionRenderer`, `DisciplinesSectionRenderer`, `RecipesSectionRenderer`,
`SummarySectionRenderer`) constructed with `(IItemSearchProvider?, sink, sharedFonts)`
and a `Render(section, contentFlow, panelWidth)` method that pushes closures into the
sink instead of into the view's private fields directly. The tree renderer (8) is the
hardest - it owns `_treeNodeStates`, `_nodeOverrides`, `_ignoredItemIds`, and the
`ApplyOverridesAndResolve` callback into the pipeline - so extract it **last** and as
its own `TreeSectionController` that takes the resolve delegate the view already
receives from `Module`.

**Migration (one PR per renderer, keep 850 green):** the section renderers produce
no numeric output the tests assert on directly - the Blish-free tests target
`PlanViewModelBuilder`, `PlanContentHeightMath`, `PlanRelayoutMath`,
`ShoppingColumnMath`, `DecisionPillPlanner`, which are *upstream* of the view. So an
extraction that preserves the view-model -> control mapping is invisible to the suite.
That is both the opportunity (tests won't block you) and the danger (**tests won't
catch a regression either** - there is zero automated coverage of the view itself;
see S9). Each renderer PR must be validated by the KNOWN-ISSUES live-verification
loop (screenshot + pixel scan), not just green tests. **Risk: moderate-high**;
sequence it one section at a time, smallest first (`DisciplinesSectionRenderer` @3082
is ~40 lines and a good pilot).

**Tier 3 - the essential machinery, extract for readability only, not to change it
(REDESIGN, high care).** The scroll/wheel/resize machinery (3-6) *could* move into a
`ScrollController` / `ResizeReflowController` collaborator that owns the `FrameTicker`s,
the registries, and the reflection field. This would carve out ~1500 lines and give
the machinery a name that matches its KNOWN-ISSUES documentation. But it is the single
riskiest change in the repo: the exact frame-timing, subscription-order, and
synchronous-registration guarantees documented in KNOWN-ISSUES #12/#13 are what make
it correct, and they are asserted "by construction," not by tests. **Recommendation:**
defer to a *late* PR, do it purely as a move (no logic edits), and treat the live
wheel/resize verification loop as the acceptance gate. If M38 has to cut scope, cut
this one first - the Tier 1+2 extractions already get the file under ~2000 lines.

### 3c. Interim, zero-risk quick win

Before any extraction, add `#region` markers matching the 11 responsibilities above.
**MEASURED:** there are currently none in 4802 lines. This is a 5-minute CLEANUP that
makes the file navigable and makes every subsequent extraction PR reviewable, and it
carries no behavioral risk at all. Pair each region header with a one-line pointer to
the governing KNOWN-ISSUES item (see S8).

---

## 4. Large service classes: internal seams worth naming (REDESIGN, optional)

### 4a. `Services/PlanSolver.cs` (1582 lines)

The solver proper (`Solve`, `Evaluate`, `PickCheapest`, `Collect`, `AggregateStep`)
is tangled with a **vendor-batching sub-engine**: `VendorOfferBatch`/`VendorBatchState`
structs, `EvaluateVendorOffers` (@604), `FinalizeVendorBatches` (@1202),
`AllocateVendorNodeCosts` (@1303), `MergeVendorCurrencyCosts` (@1118),
`VendorBatchesEqual`, `ScaleCostLines`. This "merged-ceil vendor batching" is
**essential** (documented; KNOWN-ISSUES #20.2, #28, `docs/research/m37-r4-vendor-caps.md`)
but it is a cohesive concern that could be its own `VendorBatchSolver` collaborator
the `PlanSolver` delegates to. **Target:** extract the batch structs + the 6 vendor
methods into `Services/VendorBatchSolver.cs`, injected into `PlanSolver`. **Risk:
moderate** - `PlanSolverTests.cs` is 2705 lines and exercises this heavily through
the public `Solve`, so a pure extraction that keeps `Solve`'s signature is well-fenced
by tests. This is the best-tested big file, so it is the *safest* large-file split.
**Label: REDESIGN, but low-risk thanks to test density.**

### 4b. `Services/CraftingPlanPipeline.cs` (1463 lines)

Two concerns cohabit: (i) orchestration (recipe fetch -> price -> solve -> reduce ->
build result, the `GenerateStructuredAsync` family) and (ii) **sell-side economics**
(`ApplySellSideEconomics` @1067, `ComputePerItemEconomics` @1156,
`ComputeMaterialOpportunityCost` @1218, `ApplyBatchSellSideEconomics` @1306,
`PerItemEconomics` struct). The economics block (~340 lines) is a self-contained,
well-researched unit (KNOWN-ISSUES #25, `docs/research/m37-r2-batch-economics.md`).
**Target:** extract to `Services/SellSideEconomics.cs` (static, Blish-free, directly
unit-testable in isolation - it is pure arithmetic over the result). **Risk: low-
moderate**; today it is only tested transitively through the pipeline, so extracting
it *improves* testability. **Label: REDESIGN, recommended.**

### 4c. Pipeline public API surface (CLEANUP)

**MEASURED:** the pipeline exposes `GenerateAsync` (legacy single-item, @50-211),
`GenerateStructuredAsync(int,int,...)` (@212), `GenerateStructuredAsync(IReadOnlyList,
...)` (@534, the one `Module` actually calls), plus `ResolveWithOverrides` and a
static `BuildPresetOverrides`. `Module` wires **only** the `IReadOnlyList` overload.

- **`GenerateAsync` is called from 10 sites and every one is a test** (`CraftingPlan
  PipelineTests.cs`, `MysticForgeIntegrationTests.cs`). No production path uses it.
  This is a legacy public method kept alive solely by its own tests - the tests pass,
  but they exercise a code path the app never runs, which is a soft violation of the
  repo's "tests must exercise real production code paths" invariant. **Finding:** either
  (a) delete `GenerateAsync` and repoint those tests at `GenerateStructuredAsync`
  (a single-item list short-circuits to the same core per the class doc-comment), or
  (b) if it is a deliberately-kept simple harness entry point, mark it clearly as such.
  **Recommend (a).** **Risk: moderate** - it touches ~10 tests; do it as its own PR so
  the diff is obviously test-repointing. **Label: CLEANUP (with test edits).**

- **`VendorOfferResolver` is dead in production. MEASURED:** `Module` passes
  `resolver: null`; there is **no `new VendorOfferResolver`** anywhere in prod; the
  only construction is in `VendorOfferResolverTests`. The three `if (_resolver != null
  && _vendorOfferStore != null)` guards (@109, @277, @651) therefore never fire at
  runtime. This is **intentional and essential** - `VendorOfferResolver` does live
  wiki fetches, which the repo invariant forbids at runtime (offline seed model;
  fetching happens in `tools/VendorOfferUpdater`). But nothing *in the code* says so:
  a future reader sees a wired dependency and three call sites and cannot tell they
  are deliberately disabled. **Finding:** add a doc-comment on the `resolver` ctor
  param and at the `Module` call site stating "always null at runtime by design - the
  no-runtime-wiki invariant; offline resolution only." **Label: CLEANUP (comment),
  low risk.** Do NOT delete the seam - it is exercised by tests and by tooling intent.

---

## 5. The Services "grab-bag" (CLEANUP - foldering)

**MEASURED:** 57 files directly under `Services/`, with only two sub-namespaces
(`Recipes/`, `Diagnostics/`) that show the pattern was *started* but not carried
through. The flat folder mixes at least six kinds of thing:

- **Pure UI-math** (Blish-free but view-serving): `ScrollMath`, `PlanRelayoutMath`,
  `PlanContentHeightMath`, `ShoppingColumnMath`, `WheelDeltaSanitizer`,
  `CurrencyDisplayResolver`, `DecisionPillPlanner`, `ItemRowRequestBuilder`.
- **API clients + seams**: `Gw2*ApiClient`, `I*ApiClient`, `CompositeRecipeApiClient`,
  `RecipeClientFactory`, `Gw2AccountRecipeClient`, `IAccountRecipeClient`.
- **Domain/solve**: `PlanSolver`, `CraftingPlanPipeline`, `CraftingTreeBuilder`,
  `PlanResultBuilder`, `PlanViewModelBuilder`, `RecipeService`, `InventoryReducer`,
  `AchievementBitDedupPrePass`, `OwnedMaterialsForceBuyPrePass`.
- **Persistence/stores**: `SnapshotStore`, `StatusStore`, `VendorOfferStore`,
  `VendorOfferLoader`, `VendorOfferHasher`, `CurrencyValuationSerializer`.
- **Vendor domain**: `VendorOfferResolver`, `MysticForgeRecipeData`,
  `FileMysticForgeRecipeSource`, `IMysticForgeRecipeSource`, `IWikiVendorClient`.
- **Config/misc**: `ModuleSettings`, `SettingsInputParser`, `StatusText`,
  `StatusUpdateGuard`, `BoundedConcurrency`, indexes.

**Target shape:** finish the foldering the repo already implies:
`Services/Pricing/`, `Services/Planning/`, `Services/Persistence/`,
`Services/Vendor/`, `Services/Layout/` (the pure UI-math), `Services/Api/`. Keep the
`GW2CraftingHelper.Services` namespace flat if you prefer (folders need not equal
namespaces in this old-style csproj), OR nest namespaces to match.

**Migration (CLEANUP, but chunky):** because this is a **non-SDK csproj with explicit
`<Compile Include>`** (see S8), every moved file is a manual csproj path edit. Do it in
themed PRs (one folder per PR: "move pricing services", "move persistence services")
so each diff is a coherent set of moves + csproj edits. **Risk: low behaviorally, but
high merge-conflict potential** against the in-flight m37-homestead branch (which
touches `PlanSolver`/settings/vendor seed) and the audit-fix branch (marshal/error
paths). **Sequencing note:** land the folder moves *after* those branches merge, or the
rename churn will conflict badly. This is the clearest case where PR ordering matters.

---

## 6. Coin-render duplication across views (CLEANUP - fix the class)

**MEASURED:** `Views/MainView.GetCoinColor` (@411) is **byte-identical** to
`Views/CraftingPlanView.GetCoinColor` (@4519) - same three `case` asset ids, same RGB
literals. `MainView.AddCoinSegment` (@422) re-implements the same "amount label, then
coin icon to the RIGHT, iconSize=20, gap=2, segmentGap=6" layout that
`CraftingPlanView`'s coin-segment code implements (with the same magic constants,
`CoinIconSize = 20`, `CoinLabelIconGap = 2`, `CoinSegmentGap = 6`). The coin-icon
invariant from CLAUDE.md is thus encoded **twice, independently**. If someone changes
the gold RGB or the icon gap in one view, the two coin displays silently diverge -
exactly the "fix the class, not the instance" hazard.

Note the codebase already solved the *currency* (non-coin) version of this correctly:
`Services/CurrencyDisplayResolver` is a shared, Blish-free, tested resolver
specifically so shopping rows and tree cost cells "never drift apart" (its own
doc-comment, KNOWN-ISSUES #16). Coin rendering just never got the same treatment.

**Target:** the `CoinCurrencyRenderer` extracted in S3b-Tier1 becomes the single home
for coin colors + segment layout; `MainView` calls it too. **Migration:** do S3b-Tier1
first, then repoint `MainView` (one small PR). **Risk: low** - `MainView`'s simpler
non-right-aligned layout is a strict subset of `CraftingPlanView`'s; the shared helper
already needs a "left-anchored" mode which `MainView` uses and the tree/shopping cells
don't. Add one Blish-free test asserting the coin color table if any part can be made
XNA-free (the color table returns `Microsoft.Xna.Framework.Color`, which the existing
Blish-free tests avoid - so the color function itself may not be newly testable, but
the *segment-width arithmetic* can be, mirroring `ShoppingColumnMath`).

---

## 7. Public-vs-internal surface discipline (CLEANUP - low risk, high signal)

**MEASURED:** 137 `public` types vs **4** `internal` types across production. For a
single assembly that Blish loads via one MEF export (`[Export(typeof(Module))]
class Module`), essentially nothing else needs to be `public`. A public type is a
promise about a stable API; here it is noise that makes "the public surface" mean
nothing to a prospective contributor - directly counter to the "exemplary public
project" goal.

The enabling infrastructure is **already in place (MEASURED):**
`Properties/AssemblyInfo.cs` has `[assembly: InternalsVisibleTo("GW2CraftingHelper.
Tests")]`, and the test project consumes production via `ProjectReference` (not linked
`Compile Include`). So flipping `public` -> `internal` keeps all 850 tests compiling.

**Target:** default everything to `internal` except `Module` (must stay public for
MEF). **Migration:** this is mechanical but touches ~130 files, so do it in themed
batches aligned with the S5 foldering (flip a folder's visibility in the same PR that
moves it) to avoid a single 130-file diff. **Risk: low** - the compiler + tests are a
hard backstop; anything that genuinely must stay public (MEF-discovered types, if any
beyond `Module`) will fail the build immediately. **Verify** first with a build after
flipping a single leaf type. Note: this pairs naturally with each S5 folder PR.

---

## 8. Project & tooling posture for "exemplary public project" (CLEANUP + REDESIGN)

**MEASURED facts:**
- Old-style **non-SDK csproj** (`xmlns=".../developer/msbuild/2003"`, `ToolsVersion
  15.0`), `TargetFrameworkVersion v4.8`, `packages.config`, ~100 hand-maintained
  `<Compile Include>` entries in **non-alphabetical, historically-accreted order**
  (Models and Services interleaved by when they were added).
- **No `.editorconfig`, no `Directory.Build.props`, no analyzer package, no
  `stylecop.json`, no nullable, no explicit `LangVersion`.**
- `README.md` is 36 lines. No `CONTRIBUTING.md`, no architecture doc, no
  `docs/ARCHITECTURE.md`.

Assessment against the public-project goal:

1. **Manual `<Compile Include>` is a real maintenance + merge hazard (MEASURED
   pattern).** Every new file, move, or rename is a manual csproj edit, and the file
   is a guaranteed merge-conflict magnet across parallel branches (the current
   multi-agent worktree situation is living proof). **Options:** (a) minimal CLEANUP -
   just alphabetize/group the existing includes so diffs are readable and conflicts
   are local; (b) REDESIGN - convert to SDK-style globbing (`<Project Sdk="Microsoft.
   NET.Sdk">` with default `**/*.cs` includes). Option (b) is the right end state but
   **carries real risk**: Blish's module template, the MonoGame `mgfxc` content step
   (note `mgfxc.pdb` in the repo root), the `.ref` assembly setup, and `packages.config`
   vs `<PackageReference>` all interact with the project style. **INFERRED** this is
   doable (Blish modules have been migrated to SDK-style in the community) but it is a
   milestone-sized change that must be validated by a clean build + full test run +
   an actual in-Blish load, not just `dotnet build`. **Recommend:** do the cheap
   CLEANUP (a) now; scope the SDK migration (b) as its own dedicated PR with the load
   test as the gate. Propose a cheap measurement: `git log --oneline -- GW2Crafting
   Helper.csproj | wc -l` to quantify how often the csproj has been a churn point
   (if it is high, the migration pays for itself in reduced future conflicts).

2. **No analyzers / `.editorconfig` (MEASURED).** CLAUDE.md mandates Allman braces
   and ASCII-only source, but nothing *enforces* it - it relies on reviewer vigilance.
   For a community project this is the single highest-leverage addition: an
   `.editorconfig` encoding the Allman/ASCII/naming rules, plus enabling the built-in
   .NET analyzers, turns the written conventions into build-time checks and is exactly
   what makes a repo feel "high quality" to a new contributor. **Label: CLEANUP (add
   config), low risk.** Keep the analyzer severity at `suggestion`/`warning` initially
   so it does not break the build on day one. Flag: an ASCII-only analyzer rule is not
   built-in; a tiny custom check (or a CI grep for non-ASCII in `*.cs`) would enforce
   the CLAUDE.md rule mechanically.

3. **Nullable reference types are off (MEASURED).** Turning `<Nullable>enable</Nullable>`
   on across 18k lines is a large REDESIGN with many warnings to triage (the codebase
   leans on null sentinels: `_currentPlan`, `resolver: null`, nullable `long?` costs).
   **Do not** bundle this into M38 as a blanket flip. If desired, enable per-file with
   `#nullable enable` on the pure Blish-free helpers first (they are small and already
   null-disciplined). **Label: REDESIGN, defer / opt-in.**

4. **README + contributor docs (CLEANUP).** For public consumption, a 36-line README
   is thin. Add a short `docs/ARCHITECTURE.md` that draws the layer map from S1 and the
   pipeline from S9 - most of the raw material already exists in KNOWN-ISSUES and the
   parity spec; it just needs a reader-facing summary that is not a 116KB changelog.

---

## 9. The pipeline data flow is clean; the view is the only tangle (INFERRED)

Traced the documented pipeline
`RecipeService -> CraftingPlanPipeline -> PlanSolver -> CraftingTreeBuilder ->
PlanViewModelBuilder -> View` and it holds up: each stage consumes the prior stage's
model and produces the next, no layer reaches around another. `CraftingPlanPipeline`
is the composition point (it owns the service instances and sequences them);
`PlanViewModelBuilder` is a clean `CraftingPlanResult -> PlanViewModel` transform with
no Blish types; `CraftingPlanView` consumes `PlanViewModel` and never re-derives
domain facts. This is a proper MVVM-ish separation and the reason the solver/pricing
logic is so testable.

The **one** place the flow is muddied is inside `CraftingPlanView` itself:
`ApplyOverridesAndResolve`/`ApplyPreset` call back into the pipeline's
`ResolveWithOverrides` synchronously from the tree UI, and the view holds
`_nodeOverrides`/`_ignoredItemIds`/`_lastResult` as mutable UI state that feeds that
call. That is a legitimate interactive-override loop, not a layer violation, but it is
the coupling that makes the tree renderer (S3b-Tier2) the hardest to extract - the
tree owns a slice of application state, not just presentation.

**The structural test gap (MEASURED):** all 850 tests live under
`tests/.../Services/` - **zero** target `Views/`. This is correct per the Blish-free
invariant, but it means the entire 4802-line view, including the essential scroll/
resize/wheel machinery, has **no automated regression net** and is verified only by
the manual live-capture loop documented in KNOWN-ISSUES. This is the strongest
argument for the S3 strategy of **pushing logic *out* of the view into Blish-free
helpers** (as M33 did with `PlanContentHeightMath`/`PlanRelayoutMath`): every line
moved into a pure helper is a line that becomes testable. Frame the S3 extractions to
the synthesis not just as "smaller file" but as "convert untested view logic into
tested helper logic."

---

## 10. Localization landmines (FLAG ONLY - do not fix; localization is deferred)

Per the brief, flagging patterns that a future i18n milestone would have to unwind:

- **Hardcoded UI strings scattered through `CraftingPlanView`, `MainView`,
  `SettingsTabContent`, `Module`**: section captions, `"Coming Soon"` (Module @569),
  status text, tab titles (`"Crafting Plan"`, `"Snapshot"`, etc. in `Module`),
  `TileCaptionFor`, `ShoppingSourceTag` ("VENDOR"/"SALVAGE"/"UNKNOWN"), pill labels.
  None go through a resource/string-table indirection.
- **`Services/StatusText.cs`** centralizes *some* status strings - good precedent -
  but most user-facing text does not route through it.
- **Composite/format strings** like `$"Updated — {...:t}"` (`Module` @483) and
  `FormatCoinText`'s `"{gold}g {silver}s {cop}c"` bake word order and units into
  interpolation, which is the classic i18n trap.
- **Enum-to-label mapping** (`CraftingDecision`/source -> display text) happens inline
  in the view rather than through a single label provider.

Again: **not for M38.** The value of flagging now is that S3's `CoinCurrencyRenderer`
and any label-provider extraction are natural future seams for a string table, so the
cleanup should at least *centralize* labels where convenient (e.g. one `PillLabels`/
`SectionCaptions` static) even while keeping them hardcoded, to shrink the future i18n
surface for free.

---

## 11. Under-commented essential complexity (CLEANUP - the cheapest high-value work)

The brief is right that the *why* lives in `docs/KNOWN-ISSUES.md` (116KB) far more
than in the code. Several load-bearing mechanisms are correct-by-construction but a
new contributor reading only the code cannot tell essential from arbitrary:

- `PreserveScrollAcross`, `StartScrollVerify`, the `PanelScrollbarField` reflection,
  and the `FrameTicker` control - **do** have good inline comments, but they point at
  concepts ("the blind-overwrite window") whose evidence is only in KNOWN-ISSUES.
  Add a one-line `// See docs/KNOWN-ISSUES.md #12/#13/#19` anchor at each machinery
  region head so the doc trail is discoverable from the code.
- The `resolver: null` intentional-dead seam (S4c) - **uncommented**; add the note.
- The `MapSource` bridge / dual-enum design (S2a) - **uncommented** at the enum
  definitions; add cross-references.
- `_relayoutActions`/`_reellipsisActions` - well-commented at the field, but the
  ~20 `.Add(...)` call sites are terse; the DEBUG invariants (@2481, @1750) are the
  real spec and deserve a pointer from each builder.

This is pure comment work, zero behavioral risk, and it is the single best way to make
the "hairy machinery" survivable for outside contributors - which is a stated M38 goal.
**Do it as part of, or immediately before, the S3c region-marking pass.**

---

## 12. Prioritized, PR-sized roadmap

Ordered for (a) low-risk-first, (b) unblocking later PRs, (c) avoiding conflict with
the in-flight m37-homestead and audit-fix branches. **No time estimates** per house
rules.

| # | PR | Type | Risk | Notes / sequencing |
|---|---|---|---|---|
| 1 | `#region` markers + KNOWN-ISSUES anchors + intentional-dead-seam comments in `CraftingPlanView`, and enum/`resolver` comments (S3c, S4c, S11) | CLEANUP | very low | pure comments; do first, de-risks all later view PRs |
| 2 | Move `CraftingTreeNode`, `CraftingDecision` -> `Models`; `StaticItemSearchProvider` -> `Services`; slim `Contracts` to seams (S2) | CLEANUP | low | small; de-noises later diffs |
| 3 | Extract Tier-1 static renderers: `CoinCurrencyRenderer`, `RarityColors`, `IconControls`, `LabelHelpers` (S3b-T1) | CLEANUP | low | removes ~700-900 lines from the God class |
| 4 | Repoint `MainView` coin rendering at `CoinCurrencyRenderer`; kill the duplicate (S6) | CLEANUP | low | depends on #3 |
| 5 | Extract `SellSideEconomics` from `CraftingPlanPipeline` (S4b) | REDESIGN | low-mod | improves testability; well-fenced |
| 6 | Delete legacy `GenerateAsync`, repoint its ~10 tests at `GenerateStructuredAsync` (S4c) | CLEANUP | mod | isolated test-repointing PR |
| 7 | Extract `VendorBatchSolver` from `PlanSolver` (S4a) | REDESIGN | mod | 2705-line test file fences it; do after m37-homestead merges (it touches PlanSolver) |
| 8 | Section-renderer extractions behind `ISectionRelayoutSink`, one section per PR, smallest first (S3b-T2) | REDESIGN | mod-high | live-verify each; NO test net on the view |
| 9 | Add `.editorconfig` (Allman/ASCII/naming) + enable .NET analyzers at `warning` (S8.2) | CLEANUP | low | high contributor-facing value |
| 10 | `public` -> `internal` sweep, batched with S5 folders (S7) | CLEANUP | low | compiler+tests backstop; needs InternalsVisibleTo (present) |
| 11 | Services foldering into Pricing/Planning/Persistence/Vendor/Layout/Api (S5) | CLEANUP | low behav / high conflict | **after** in-flight branches merge; themed PRs |
| 12 | Alphabetize/group `<Compile Include>` (S8.1a) | CLEANUP | low | reduces csproj conflicts |
| 13 | Tree-section controller extraction (S3b-T2, hardest) | REDESIGN | high | owns override state; do late |
| 14 | Scroll/Resize/Wheel controller extraction (S3b-T3) | REDESIGN | high | move-only, live-verify gate; cut first if scope shrinks |
| 15 | (Separate milestone) SDK-style csproj migration (S8.1b); nullable opt-in (S8.3) | REDESIGN | high | own PR + in-Blish load test; not core M38 |

**Cross-cutting sequencing warning:** PRs #7, #11 collide with the m37-homestead
(PlanSolver/settings/vendor) and audit-fix (marshal/error paths) branches. Hold the
foldering/solver-split PRs until those land, or plan explicit rebases. The comment/
region/extraction PRs (#1-#5) are largely orthogonal and can proceed in parallel.

---

## Appendix: what I deliberately did NOT flag as a problem

- The scroll/wheel/resize/`FrameTicker`/`MainThreadMarshal` machinery: **essential,
  documented, evidence-backed.** Proposals above only ever *move* or *comment* it.
- The dual `CraftingDecision`/`AcquisitionSource` enums: **kept** (justified split).
- `VendorOfferResolver` + its guards: **kept** (intentional runtime-disabled seam).
- The Blish-free-service discipline and the pure-math-helper pattern: **a strength to
  extend**, not a target.
- `MainThreadMarshal` in `Views/`: checked for a layer inversion, found none.
