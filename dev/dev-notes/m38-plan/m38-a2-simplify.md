# M38 Cleanup Analysis - Simplification Lens (A2)

Analyzed: current `master` (`be8ebda`, post-M37). Read-only; nothing modified.

## Scope and method

- Excluded `.claude/worktrees/**` (5 stale agent worktree copies of the whole
  tree - not part of master), `bin/`, `obj/`, `.vs/`, `packages/`.
- Real source: ~187 `.cs` files, ~45,000 lines (`Services`, `Models`,
  `Contracts`, `Views`, `Module.cs`, `tools/*`, `tests/*`).
- Read `docs/KNOWN-ISSUES.md` (full, both halves) end to end before touching
  code, plus skimmed `docs/gw2e-parity-spec.md` and `docs/research/*` for
  context. Did **not** propose touching any of the documented essential
  machinery below - it is treated as a hard constraint throughout this
  report:
  - Scroll restore/verify contests (`StartScrollVerify`, `FrameTicker`,
    `PreserveScrollAcross`/`PreserveScrollAcrossResize`, `ScrollMath`).
  - `PlanContentHeightMath` synchronous height contracts and the
    `PlanRelayoutMath` relayout-closure registry
    (`_relayoutActions`/`_reellipsisActions`).
  - `WheelDeltaSanitizer` (shipped-Blish-binary bug workaround).
  - `MainThreadMarshal`/`FrameTicker` (Blish has no `SynchronizationContext`).
  - `StatusUpdateGuard`, merged-ceil vendor batching
    (`PlanSolver.FinalizeVendorBatches`), `AchievementBitDedupPrePass`.
  - The 2px/`bottomClearance` divider math and all UI-scale-derived pixel
    constants in `CraftingPlanView.CreateRowDivider`.
- Method: systematic grep/wc sweeps for duplication, dead code, long
  methods, deep nesting, boolean-param APIs, stale TODOs, commented-out
  code, and cross-file naming drift - not spot sampling. A few "checked,
  found clean" results are reported alongside the real findings so the
  coverage claim is verifiable.
- Not built/tested (task is read-only; `docs/KNOWN-ISSUES.md` already
  records 812+ tests green on this commit).
- In-flight branches (`m37-homestead` worktree; audit-fix branches touching
  marshal/error paths) were not read. Findings below avoid citing exact
  line numbers as a *dependency* where that branch is plausibly touching
  the same area (`PlanSolver`, settings, vendor seed) - line numbers given
  are current-master facts for verification, not an assumption they will
  still be exactly there.

---

## Top findings (see full list below for everything)

1. **Dead subsystem: `VendorOfferResolver`/`IWikiVendorClient` live-wiki-at-runtime path (~725 lines incl. tests), never wired in production** - `Module.cs:213` passes `resolver: null` explicitly. Mechanical, safe deletion.
2. **`CraftingPlanPipeline`'s three `Generate*Async` methods duplicate ~120-150 lines of pipeline steps each** (tree-build, price fetch, dead vendor-resolve call, currency/learned-recipe fetch-with-swallow, timing-log wrap-up) - mechanical extraction into shared helpers.
3. **`CraftingPlanView.cs` is 4,802 lines (74% of all `Views/*.cs`)**, and five-plus `CreateXRow` methods repeat the same row-panel/icon/name-label/ellipsis/divider/relayout-registration shape. Splitting into partial-class files by concern, and factoring the repeated row shape into 1-2 shared helpers, is mechanical but high-value.
4. **`PlanSolver.EvaluateVendorOffers` takes 7 `out` parameters** - the codebase already has the right pattern to fix this (`CraftingPlanPipeline.PerItemEconomics`, a private result struct) and just didn't apply it here.

---

## Findings

Each finding: **Location(s)** | **Category** | **Mechanical/Judgment** | what simpler looks like.

### 1. Dead code: the live-wiki vendor-offer resolver subsystem

**Category:** dead code. **Mechanical.**

`Services/VendorOfferResolver.cs` (242 lines) implements a rate-limited,
retrying, concurrency-bounded live wiki-lookup client
(`EnsureVendorOffersAsync`), backed by `Services/IWikiVendorClient.cs` (24
lines, defines `RawWikiVendorOffer`), `Services/WikiLookupOptions.cs` (10
lines), and `Models/ResolveResult.cs` (11 lines). None of this is reachable
in production:

- `Module.cs:207-217` constructs the real `CraftingPlanPipeline` with
  `resolver: null` explicitly.
- `grep -rn "new VendorOfferResolver"` outside `tests/` returns **zero**
  hits anywhere in the repo.
- `CraftingPlanPipeline.cs` guards every call site with
  `if (_resolver != null && _vendorOfferStore != null)` (lines 109-114,
  277-282, 651-656) - three copies of a branch that can never execute in
  the shipped module.
- The only implementer of `IWikiVendorClient` anywhere in the repo is
  `tests/GW2CraftingHelper.Tests/Helpers/InMemoryWikiVendorClient.cs` (78
  lines) - the interface has never had a production implementation.
- `Models/ResolveResult` is referenced from exactly one production file
  (`VendorOfferResolver.cs` itself) - it exists solely to be the return
  type of a method nothing calls.
- `tests/GW2CraftingHelper.Tests/Services/VendorOfferResolverTests.cs`
  (360 lines) is real, passing test coverage - **of dead code**.

This is architecturally consistent with the rest of the module (vendor
offers are genuinely resolved from the static `ref/vendor_offers.json` seed
via `VendorOfferLoader`/`VendorOfferStore`, matching the repo's "seed at
dev time, never scrape at runtime" invariant) - this class looks like a
pre-seed-era design (an earlier milestone's "resolve missing vendor data
live from the wiki on generate" idea) that was superseded by the seeding
approach and never removed, with its constructor parameter left wired
through `CraftingPlanPipeline` as a permanently-`null` optional dependency.

**What simpler looks like:** delete `VendorOfferResolver.cs`,
`IWikiVendorClient.cs`, `WikiLookupOptions.cs`, `Models/ResolveResult.cs`,
the matching test file and test helper, and the `resolver`
parameter/field/three dead-branch checks in `CraftingPlanPipeline.cs`
(collapsing e.g. lines 106-114 down to just the vendor-offer query step).
~725 lines removed, zero behavior change (the branches are unreachable
today by construction).

### 2. Duplication: `CraftingPlanPipeline`'s three `Generate*Async` overloads

**Location:** `Services/CraftingPlanPipeline.cs` - `GenerateAsync` (lines
50-210, 161 lines), `GenerateStructuredAsync` single-item (212-516, 305
lines), `GenerateStructuredMultiAsync` (585-838, 254 lines).
**Category:** duplication / long methods. **Mechanical**, given the
existing test suite (`CraftingPlanPipelineTests`, `MultiItemPlanTests`)
already pins the exact behavior byte-for-byte per the M37 KNOWN-ISSUES
record.

All three methods repeat, nearly verbatim:

- Build tree + `AchievementBitDedupPrePass.Apply(tree)` (differs only in
  which `RecipeService` method builds the tree).
- Collect item ids (`CollectItemIds`), fetch TP prices, the dead
  `_resolver`-guarded branch (finding #1), query vendor offers, and
  `AugmentWithVendorCostPricesAsync` - **identical** in all three (grep:
  `"Resolving vendor offers"` appears 3x verbatim).
- The "kick off currency metadata fetch, `ObserveFault`, await item
  metadata, then await-with-swallow the currency task" block -
  **identical**, 3 copies (grep: `"Fetching currency details"` 3x).
- The "fetch learned recipe ids if permission available" block -
  identical, 2 copies (the two structured overloads).
- The trailing "prepend timing log, append `PlanTimingAnalyzer.Summarize`"
  block - identical, 3 copies.

The two structured overloads additionally duplicate the force-buy
pre-pass gating (`useForceBuyPrePass`/`RecipeNodeIds.Assign`/
`OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds`) and the
inventory-reduction block verbatim - which the class's own doc comment on
`GenerateStructuredMultiAsync` already admits ("mirrors the single-item
overload's own pipeline step-for-step").

**What simpler looks like:** extract small private helpers shared by all
three (or at minimum the two structured overloads):
`FetchPricedVendorContextAsync(tree, ct, progress, timingLog)` returning
`(prices, vendorOffers)`, `FetchCurrencyMetadataAsync(ct, progress,
timingLog)`, `FetchLearnedRecipeIdsAsync(ct, progress, timingLog)`, and a
`FinishTimingLog(result, timingLog)` tail helper. This is pure
extract-method - no algorithm changes - and should cut roughly 150-200
duplicated lines while leaving the "single item vs. wrapper tree" logic
(the part that actually differs) untouched and easier to read in each
method. Given how carefully these three methods are already documented
milestone-by-milestone, do this as its own small PR with the full test
suite as the safety net, not bundled with a behavior change.

### 3. `CraftingPlanView.cs` is a 4,802-line god-file; row-creator methods repeat the same shape

**Category:** long methods / file-level over-concentration / duplication.
**Judgment-needed for the file split** (mechanical in principle - partial
classes don't change behavior - but it's a large mechanical diff that
should be its own PR, verified by full test run + one screenshot-loop
smoke check per this repo's own verification convention).
**Mechanical for the row-shape extraction.**

`Views/CraftingPlanView.cs` is 4,802 of the 6,487 total lines under
`Views/` (74%). It already has internal section-divider comments (`// ---
Used Materials section ---`, `// --- Shopping List section ---`, `// ---
Crafting Steps section ---`, `// --- Required Disciplines / Required
Recipes sections (c-table) ---`, `// --- Recipe tree section ---`, `// ---
Decision pills ---`, `// --- Coin display helpers ---`, `// --- Currency +
mixed value display helpers ---`, `// --- Icon helper ---`) that already
mark natural, ready-made partial-class boundaries:

- Core: fields/constants, constructor/build wiring, scroll+resize+
  wheel-wrap machinery (lines ~1-2140) - this is the essential,
  KNOWN-ISSUES-documented machinery; leave the logic untouched, just move
  it into its own file (`CraftingPlanView.ScrollAndResize.cs` or similar,
  as a `partial class`).
- Header/section chrome (`CreatePlanHeader`, `CreateSectionHeader`,
  `CreateCollapsibleSection`, `CreateRowDivider`, ~2140-2618).
- The six table-section row builders (~2618-3440): Used Materials,
  Shopping List, Crafting Steps, Disciplines/Recipes, Summary/cost tiles.
- The Recipe Tree section + `RenderTreeNode` + decision pills
  (~3440-4373).
- Low-level display helpers: coin segments, currency segments, rarity-
  framed icon (~4373-4802).

None of this changes behavior; it only reduces the "scroll to find the
method I need to edit" cost and the single-file merge-conflict surface
(every M3x milestone in `KNOWN-ISSUES.md` touched this one file).

Separately, within the row-builder region, the same construction sequence
repeats with minor per-row variation across `CreateUsedMaterialRow`
(2626-2704), `CreateShoppingRow` (2807-2932ish, 126 lines), `CreateCraftStepRow`
(2959-3057ish), `CreateDisciplineRow` (3091-3124), `CreateRecipeRow`
(3147-3241ish):

1. `var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };`
2. `CreateRarityFramedIcon(rowPanel, ...)` (6 call sites, `CraftingPlanView.cs:2183,2637,2817,2994,3156,3814`).
3. Measure a right-aligned secondary label's width, derive
   `nameMaxWidth` via `PlanRelayoutMath.NameMaxWidthBeforeColumn`, call
   `EllipsizeToWidth` (7 call sites total), build the name `Label`.
4. `CreateRowDivider(rowPanel, panelWidth, rowHeight, bottomClearance)`
   (5 call sites: `2682, 2898, 3038, 3114, 3210`).
5. Register a width-only `_relayoutActions.Add(...)` closure (16 total in
   the file) and, for rows with a name label, a `_reellipsisActions.Add`
   closure (3 of the row types).

**What simpler looks like:** two small shared helpers - one that builds
"icon + ellipsized name label + right-aligned secondary label" and
registers its own reellipsis closure, and one that wraps "row panel +
divider + size-only relayout closure" - called from each `CreateXRow`
with the 2-3 fields that actually differ (icon position, which columns
exist, `bottomClearance`). This does **not** touch `PlanContentHeightMath`
or `PlanRelayoutMath` (the actual essential-complexity math stays exactly
as-is); it only removes the boilerplate wrapper around calling them 5-6
times with the same shape. Do this only after finding #2/#3's file-split,
since the row builders will have moved to their own partial-class file
anyway.

### 4. `PlanSolver.EvaluateVendorOffers` has 7 `out` parameters

**Location:** `Services/PlanSolver.cs:604-803ish` (private method, single
call site at line 353, inside `Evaluate`, itself 280 lines,
`Services/PlanSolver.cs:292-571ish`). **Category:** under-abstraction /
long parameter list. **Mechanical** - one call site, private method, no
test calls it by name (only `Evaluate`/`Solve` are tested through the
public surface).

```
private static void EvaluateVendorOffers(
    RecipeNode node, IReadOnlyDictionary<int, ItemPrice> prices,
    IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
    PriceBasis priceBasis, CurrencyValuation currencyValuation,
    out long? bestComparableValue,
    out long? bestComparableCoinCost,
    out List<CostLine> bestComparableCurrencyCosts,
    out VendorOfferBatch? bestComparableBatch,
    out long? fallbackCoinCost,
    out List<CostLine> fallbackCurrencyCosts,
    out VendorOfferBatch? fallbackBatch)
```

The codebase already has the right fix pattern in the same milestone
family: `CraftingPlanPipeline.PerItemEconomics` (a small private `struct`
returned by value, introduced in M37 specifically to let two call sites
share logic without an out-param pile). `EvaluateVendorOffers` predates
that pattern and just never got the same treatment.

**What simpler looks like:** a private `struct VendorOfferEvaluation {
BestComparableValue, BestComparableCoinCost, BestComparableCurrencyCosts,
BestComparableBatch, FallbackCoinCost, FallbackCurrencyCosts,
FallbackBatch }` returned by value; the one call site in `Evaluate`
destructures it. No behavior change, meaningfully easier to read the
call site (currently 7 `out` declarations inline at the call, lines
353-361).

### 5. Test-fixture duplication: `Leaf`/`Craftable`/`Option` tree builders reimplemented in 7 test files

**Category:** duplication (test fixtures, exactly the pattern the mission
brief calls out). **Mechanical.**

`grep -rl "private static RecipeNode Leaf("` over `tests/` finds the same
(or near-same) hand-rolled `RecipeNode`/`RecipeOption` tree-builder
helpers independently defined in:

- `PlanSolverTests.cs` (`Leaf`, `Craftable`, `Option`, `WrapperOf`)
- `CraftingTreeBuilderTests.cs` (`Leaf`, `Craftable`)
- `InventoryReducerTests.cs` (`Leaf`, `Craftable`, using `qty` as the
  parameter name where every other file uses `quantity` - the naming-drift
  the mission also asked about, in miniature)
- `AchievementBitDedupPrePassTests.cs` (`Leaf` with extra
  `achievementId`/`achievementBit` params, `Option`)
- `DecisionPillPlannerTests.cs` (`Leaf`, one-liner variant)
- `OwnedMaterialsForceBuyPrePassTests.cs` (`Leaf`, one-liner variant)
- `PlanResultBuilderTests.cs` (`Leaf`, one-liner variant, `qty` param
  again)

These are not byte-identical (some set `Recipes = new List<RecipeOption>()`
explicitly and redundantly - `RecipeNode.Recipes` already defaults to an
empty list; some don't; the achievement-bit variant adds two extra
nullable params) - which is itself the risk this creates: seven
independent copies mean a future `RecipeNode` field that test fixtures
need to set (the same class of change M37's achievement-bit fields just
went through) has to be remembered in seven places, not one.

**What simpler looks like:** one shared
`tests/GW2CraftingHelper.Tests/Helpers/RecipeTreeTestBuilders.cs` (the
`Helpers/` directory convention already exists - `CapturingProgress.cs`,
the four `InMemory*Client.cs` doubles) with a single canonical
`Leaf`/`Craftable`/`Option`/`WrapperOf` set (parameterized to cover the
achievement-bit optional args), consumed by all seven files. Test-only
change, zero production risk, straightforward mechanical `using static`
or instance-helper conversion per file.

### 6. Test-fixture duplication: `FindRepoFile` walk-up-directory helper duplicated verbatim

**Location:** `tests/GW2CraftingHelper.Tests/Services/AcquisitionHintServiceTests.cs:172-183`
and `tests/GW2CraftingHelper.Tests/Services/Recipes/RecipeCacheSerializerTests.cs:124-137`.
**Category:** duplication. **Mechanical.**

Byte-for-byte identical 12-line private helper (walks up to 12 parent
directories from `AppContext.BaseDirectory` looking for a repo-relative
`ref/...json` file) copy-pasted between the two files, including the
identical doc comment. Same fix as #5: move to the shared `tests/Helpers`
folder (e.g. `RepoFileLocator.Find(relativePath)`), used by both (and any
future test that needs to load a real `ref/*.json` seed file - the
`docs/KNOWN-ISSUES.md` M37 record for `RecipeCacheSerializerTests` itself
says it was written "mirroring `AcquisitionHintServiceTests`' `FindRepoFile`
pattern," i.e. the second copy was made *by design*, not by accident -
worth fixing before a third copy appears).

### 7. Minor duplication: `tools/VendorOfferUpdater/Models/*.cs` are physical copies of `Models/*.cs`

**Location:** `tools/VendorOfferUpdater/Models/{CostLine,VendorOffer,VendorOfferDataset}.cs`
vs. `Models/{CostLine,VendorOffer,VendorOfferDataset}.cs`.
**Category:** duplication (cross-project). **Judgment-needed** - the
duplication is *structurally motivated*, not accidental: `VendorOfferUpdater`
targets `net8.0` (SDK-style csproj) while the main module targets
`net48` (old-style csproj, no `ProjectReference` possible the way
`tools/GW2CraftingHelper.Harness` and `tools/GW2CraftingHelper.RecipeSeeder`
use one, since those two are themselves `net48` and can `ProjectReference`
the main csproj directly - `MysticForgeSeeder` and `VendorOfferUpdater`
are the only `net8.0` tools, and are the only ones with this duplication).

`diff` shows `CostLine.cs` and `VendorOffer.cs` differ **only** in
namespace; `VendorOfferDataset.cs` differs in namespace plus one
already-real drift: the tool's copy defaults `SchemaVersion` to `1` in the
property initializer, the main model's copy has no default (defaults to
`0`). In practice this is harmless today (every construction site sets
`SchemaVersion = 1` explicitly - confirmed via
`grep -rn "SchemaVersion"`), but it is a live illustration of the exact
risk plain copy-paste creates: the two files *have already silently
diverged once* with nobody needing to notice.

**What simpler looks like:** since these are plain POCOs with no
framework-specific APIs, use MSBuild file linking instead of physical
copies - `<Compile Include="../../Models/CostLine.cs" Link="Models/CostLine.cs" />`
(and same for `VendorOffer.cs`/`VendorOfferDataset.cs`) in
`VendorOfferUpdater.csproj`, deleting the tool's own copies. This works
across differing TFMs for framework-agnostic C# and removes the
namespace-only diff entirely (the files literally become the same file);
the `SchemaVersion` default would need reconciling once, by hand, as part
of the same change. `MysticForgeSeeder` was not checked for the identical
pattern in this pass (it does not depend on `VendorOffer`/`CostLine` -
worth a quick check when this is picked up, per "fix the class, not the
instance").

### 8. Two parallel "how was this item acquired" enums

**Location:** `Models/AcquisitionSource.cs` (`BuyFromTp, Craft, Currency,
BuyFromVendor, UnknownSource`) vs. `Contracts/CraftingDecision.cs`
(`Craft, BuyFromTp, BuyFromVendor, Have, Currency, Unknown`).
**Category:** naming consistency / possible over-lapping concepts.
**Judgment-needed** - these are not simply the same enum twice (`CraftingDecision`
adds the display-only `Have` state and is genuinely a different layer:
`AcquisitionSource` is the solver's internal choice, `CraftingDecision` is
what a `CraftingTreeNode` shows), but:

- Member order and the "unknown" member's name differ (`UnknownSource` vs.
  `Unknown`) for no apparent reason, which is exactly the kind of
  drift that makes a newcomer assume they're interchangeable and reach for
  the wrong one.
- Neither enum's own file has a doc comment cross-referencing the other,
  despite `CraftingTreeNode.Decision`'s own field comment, `SolverDecision.Source`'s
  field comment, and `DecisionPillPlanner` all needing to reason about the
  mapping between them. The relationship is discoverable only by reading
  `CraftingTreeBuilder.BuildNode` (where the mapping actually happens).
- There is also a *third*, differently-named but related type,
  `Services/SolverDecision.cs` (a class, not an enum - the full record of
  a node's committed decision) - "Decision" is used for at least three
  distinct-but-related things in this codebase (`SolverDecision` the
  class, `CraftingDecision` the enum, `CraftingTreeNode.Decision` the
  property of enum type `CraftingDecision`).

**What simpler looks like, low-risk version:** add a one-line doc comment
on each enum cross-referencing the other and naming
`CraftingTreeBuilder.BuildNode` as the mapping site - near-zero cost,
immediately reduces the "which one do I use" confusion for a new
contributor. **Bigger, optional REDESIGN version** (not recommended for
this pass given the ~1,860 combined call sites across the two enums):
rename `CraftingDecision` to something that doesn't share a word with
`SolverDecision`/`AcquisitionSource` at all (e.g. `DisplayDecision` or
`TreeNodeDecision`) - real value for a "ready for public consumption"
codebase, but large mechanical churn for cosmetic gain; sequence after
the higher-value items above if done at all.

### 9. Two still-open, non-stale TODOs (housekeeping, not blocking)

**Category:** stale/forward-looking TODOs. **Judgment-needed** (each is a
one-line decision, not a code change).

- `Models/Gw2Constants.cs:44` - "Currency names are sourced from
  api.guildwars2.com/v2/currencies. Verify against the official API if
  broadening coverage beyond this set." This is the necessary,
  intentional offline fallback for `CurrencyMetadataService` (per
  `docs/KNOWN-ISSUES.md`'s own note under "Carried follow-ups") - not
  stale, but still an open action item with no tracking beyond the
  comment. Low priority; unrelated to the deferred localization work.
- `Contracts/IItemSearchProvider.cs:35-37` - "Consider renaming to
  `IPlanTargetSearchProvider`..." - a genuinely still-relevant naming
  question (the interface's real contract, per its own doc comment, is
  "valid plan target," not generic item search), not resolved one way or
  the other. Either rename it now (2 implementers, `CraftableItemSearchProvider`
  and `Contracts/StaticItemSearchProvider`, both in-repo - cheap) or
  remove the TODO and accept the current name; leaving it indefinitely is
  the worst of both.

No genuine commented-out code was found anywhere in `Services/`, `Models/`,
`Views/`, or `Contracts/` - every `// ...` line matching a "looks like
dead code" grep turned out to be a real doc comment (checked systematically,
not sampled; see Method above).

---

## Checked, found clean (for balance and to substantiate the coverage claim)

- **Boolean-parameter APIs:** grepped for methods with 2+ adjacent `bool`
  parameters (zero hits) and for call sites passing bare positional
  `true`/`false` literals into multi-parameter calls (one hit, a
  `File.Copy(..., true)` BCL call in `StatusStore.cs` - not a
  module-authored API). The codebase consistently uses C# named arguments
  at call sites for optional/boolean parameters (e.g.
  `assignNodeIds: !useForceBuyPrePass`, `isBestPathPreset: true`) - this
  is already good practice and does not need fixing.
- **Vestigial fields from superseded milestones:** cross-referenced every
  public property in `Models/*.cs` and `Contracts/*.cs` against whole-repo
  usage counts (script-driven, not sampled) - none came back as unused.
  `CraftingTreeNode.cs` and `RecipeNode.cs` in particular are exemplary:
  every field has an inline doc comment naming the milestone that added
  it and why, which is exactly the "essential complexity commented in
  code" pattern the mission asked to look for and preserve - worth
  holding up as the house style when the `PlanSolver.Evaluate`/
  `EvaluateVendorOffers` region (finding #4) gets touched.
- **Single-implementation interfaces / over-abstraction:** checked every
  `I*.cs` interface in `Services`/`Contracts` against its implementers.
  All either have 2+ real implementations (`IItemSearchProvider`,
  `IRecipeApiClient`) or are a genuine seam with exactly one production
  implementation plus a test double by design (`IPriceApiClient`,
  `IItemApiClient`, `IAccountRecipeClient`, `IMysticForgeRecipeSource`) -
  standard for swapping in an offline `Harness`/test double, not
  over-engineering. The one interface with **no** production
  implementation at all is `IWikiVendorClient` (finding #1 - dead, not
  merely single-implementer).
- **`IRecipeCacheStore` family** (`CompositeRecipeCacheStore`,
  `InMemoryRecipeCacheStore`, `OverlayRecipeCacheStore`,
  `SeededRecipeCacheStore`) - four implementations, but each is wired to a
  distinct real role (default/composite/live-overlay/static-seed); not
  redundant.
- **Dead classes/methods repo-wide:** a whole-repo word-frequency sweep
  (every `class`/`interface` declaration outside `tests/`, checked for
  zero references outside its own defining file) found only expected
  file-local private DTOs (e.g. `RecipeSeedData` nested in
  `RecipeCacheSerializer.cs`, `NullRecipeApiClient` nested in the
  `Harness`'s `Program.cs`) plus the already-reported finding #1 family.

---

## Suggested sequencing (mechanical risk, lowest to highest)

1. Finding #1 (delete dead wiki-resolver subsystem) - pure subtraction,
   safest possible change, do first.
2. Findings #5/#6 (test-fixture consolidation) - test-only, zero
   production risk.
3. Finding #4 (`EvaluateVendorOffers` out-params -> struct) - single call
   site, private method, full existing `PlanSolverTests`/
   `MysticForgeIntegrationTests` coverage as the safety net.
4. Finding #7 (linked model files for `VendorOfferUpdater`) - touches
   `.csproj`, so build-behavior-adjacent; verify both the tool and the
   main module still build after.
5. Finding #2 (`CraftingPlanPipeline` extract-method) - larger mechanical
   diff, but the M35/M37 regression tests already assert single-vs-multi
   byte-identical output, which is exactly the safety net an extract-method
   refactor needs.
6. Finding #3 (`CraftingPlanView.cs` partial-class split + row-shape
   helper) - largest mechanical diff in this report; do the file split
   first (zero logic change, verify via build+tests only), then the
   row-shape extraction as a separate follow-up PR, then a live
   screenshot-loop smoke check per this repo's own convention before
   calling it done (this file is exactly where every prior milestone's
   visual regressions have come from).
7. Finding #8 (enum naming) - at minimum, add the cross-reference doc
   comments now (nearly free); leave the actual rename as an optional,
   separately-scoped item given its size.
8. Finding #9 - a five-minute decision per TODO, whenever convenient.
