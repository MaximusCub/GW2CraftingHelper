# GW2CraftingHelper — Roadmap

> **Supersedes** all prior plan documents (now archived in `docs/archive/plans/2026-02-15/`).
> **Status date**: 2026-02-19

---

## Completed Work

### Phase A — Core Pipeline (Milestones 1–4)

| Milestone | What shipped |
|-----------|-------------|
| M1 | Recipe tree subsystem — recursive GW2 API recipe expansion |
| M2 | Trading Post price lookup subsystem |
| M3 | Plan solver — buy-vs-craft decision engine with tests |
| M4 | Crafting plan view + initial TP pipeline wiring |

### Phase B — Enrichment (Milestones 5–7)

| Milestone | What shipped |
|-----------|-------------|
| M5 | Recipe metadata (disciplines, rating, flags) |
| M6 | Inventory reducer (use-own-materials toggle) |
| M7 | Pipeline integration + structured `CraftingPlanResult` |

### Phase C — Correctness, Performance & Search (Milestones 9-13)

| Milestone | What shipped |
|-----------|-------------|
| M9 | Plan view correctness -- discipline derivation, Rift Essence normalization, collapse reflow, recipe enrichment |
| M10 | Plan generation performance -- progress reporting, phase timing, profiling harness, concurrent fetching, seeded recipe cache |
| M11 | Crafting tree visualization -- collapsible tree DTO, solver-annotated nodes, tree rendering panel |
| M12 | Account data & material sourcing -- per-source item index, `UsedMaterial` enrichment, priority-based reduction |
| M13 | Item selection & search -- real `IItemSearchProvider`, prefix/substring matching, seed-based index |

### Phase D — Navigation & Tooling (Milestones 14-15)

| Milestone | What shipped |
|-----------|-------------|
| M14 | Left-hand navigation tabs -- `ShellView`, `TabRegistry`, vertical tab panel, state-preserving tab switching |
| M15 | Mystic Forge recipe seeder -- wiki SMW scraper tool, 1590 recipes generated, deterministic output |

### Additional Completed Work

- **Vendor offers**: data model, loader, store, baseline/overlay merge, wiki resolver, offline updater tool
- **Mystic Forge**: recipe source plumbing, `CompositeRecipeApiClient`, bundled recipe file
- **PlanViewModel pipeline**: `PlanViewModelBuilder`, collapsible sections, coin display, icon rendering
- **UI/UX Phase 1**: snapshot/crafting tab bar, item icons, status labels
- **UI/UX Phase 2**: resizable modal window, module settings, modal dialog confirmations
- **Account recipe client**: `Gw2AccountRecipeClient` for recipe-unlock checking

---

## 3-Lane Parallel Workflow

Work proceeds in three independent lanes. Each lane is a self-contained thread of work that can be planned and executed without blocking the others, provided cross-lane coordination rules are followed.

### Lane Definitions

| Lane | Owns | Avoids |
|------|------|--------|
| **1: UI/UX** | `Views/`, `Module.cs` (view wiring only), `Models/PlanViewModel.cs` | `Services/`, `Models/` (except PlanViewModel), `Contracts/` |
| **2: Backend** | `Services/`, `Models/` (except PlanViewModel), `tests/` | `Views/`, `Module.cs` |
| **3: Central** | `Contracts/` (shared abstractions), `.csproj` registration, `docs/`, `ref/`, `tools/`, `Module.cs` service composition (small wiring edits only) | Large file rewrites |

### Cross-Lane Policy

1. **Seam interfaces** live in `Contracts/` and are owned by Lane 3 (Central).
2. Lanes 1 and 2 **consume** seam interfaces but do not modify them without coordinating with Central.
3. When a lane needs a new shared abstraction, it requests Central to define the seam first.
4. `Module.cs` edits must be clearly scoped: view wiring (Lane 1) vs service composition (Lane 3).
5. **Default**: no edits outside lane ownership boundaries.
6. **Exception**: wiring edits of **≤20 lines** in another lane's file are permitted only when necessary for integration. Each such edit must be explicitly documented in the commit message and noted in this roadmap or the PR body.

### Thread Startup Procedure

When starting a new thread of work:

1. Identify which lane the work belongs to.
2. Check this roadmap for the relevant milestone stub.
3. Fill in the milestone details (scope, acceptance criteria) before beginning implementation.
4. Create a feature branch named after the milestone.
5. Follow the Edit → Review → Fix loop from `CLAUDE.md`.
6. PR back to `master` when complete.

---

## Seam Scaffolding

### IItemSearchProvider

Decouples item selection from `CraftingPlanView`'s hardcoded dictionary. The interface lives in `Contracts/`, with a default `StaticItemSearchProvider` that returns the current two-item list.

- **Interface**: `Contracts/IItemSearchProvider.cs`
- **Default impl**: `Contracts/StaticItemSearchProvider.cs`
- **Consumer**: `Views/CraftingPlanView.cs` accepts `IItemSearchProvider` via constructor
- **Future**: Lane 2 can implement a real GW2 API-backed search provider without touching the view

### Seam Contracts

- `IItemSearchProvider` MUST return only **valid plan targets** — items for which `CraftingPlanPipeline` can generate a crafting plan.
- A future real provider must index **discipline recipe outputs** AND **Mystic Forge outputs** (including synthetic/special entities if present in the bundled recipe data).
- The provider is the **sole authority** on plan validity. The UI does not validate whether a returned item is craftable — it trusts the provider.

---

## High-Risk Surfaces

These files are touched by multiple lanes or have outsized blast radius. **Coordinate before editing.**

| File | Risk | Owner(s) |
|------|------|----------|
| `Module.cs` | Service composition + view wiring in one file | Lane 1 (view wiring), Lane 3 (service composition) |
| `Views/CraftingPlanView.cs` | Largest UI file, dropdown/plan rendering, seam consumer | Lane 1 only |
| `GW2CraftingHelper.csproj` | `<Compile Include>` registration; broken entries break build | Lane 3 only |

---

## Milestones

Milestones are planned and filled in as work begins. Each milestone follows this template:

### Milestone Template

```
### M<N>: <Title>

**Lane**: <1|2|3>
**Branch**: <branch-name>
**Dependencies**: <prior milestones or seams required>

#### Scope
- Bullet points describing what ships

#### Main-View-Composition Owner
<Lane that owns Module.cs wiring for this milestone>

#### Acceptance Criteria
- [ ] <Criterion — must be verifiable in-game or via tests>
- [ ] <Criterion — must be verifiable in-game or via tests>
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable
```

> **Note**: Acceptance criteria must be **verifiable** — either via automated tests or observable in-game behavior. Avoid subjective criteria like "works correctly".

### M9: Plan View Correctness & Integrity (COMPLETED - PR #13)

**Branch**: m9-plan-correctness
**Dependencies**: None

#### Problem Statement
The plan view has three known correctness issues that undermine user trust:
1. **Spurious disciplines**: `RequiredDisciplines` includes disciplines from intermediate recipes the solver chose to buy rather than craft. The derivation walks the full `RecipeNode` tree instead of filtering to nodes with `Decision == Craft`.
2. **Rift Essence inconsistency**: Rift Essences can appear as both a vendor ingredient cost (currency-like) and a normal material. Quantities in the summary, shopping list, and crafting steps sections may disagree because each section aggregates independently.
3. **Collapse reflow bug**: When a `PlanSectionViewModel` section is collapsed in the UI, the containing `FlowPanel` does not reclaim the freed vertical space, leaving a blank gap. This is a Lane 1 (UI) issue but qualifies for the ≤20-line cross-lane exception since it is a layout fix in `CraftingPlanView`.

Additionally, the Required Recipes section currently shows only recipe IDs/names without context. Displaying the discipline, rating, and learned/unlearned status for each recipe would make this section actionable.

#### Scope
- **Lane 2**: Fix discipline derivation in `PlanViewModelBuilder` to walk only solver-chosen craft nodes
- **Lane 2**: Normalize Rift Essence handling so a single canonical quantity flows through summary, shopping list, and crafting steps
- **Lane 1 (≤20-line exception)**: Fix section collapse reflow in `CraftingPlanView` so `FlowPanel` reclaims space on collapse/expand
- **Lane 2**: Enrich `RequiredRecipe` display data with discipline(s), rating, and learned status from `Gw2AccountRecipeClient` results

#### Non-Goals
- Solver algorithm changes (buy-vs-craft logic is not modified)
- Performance optimization of plan generation
- Adding new sections to the plan view
- Redesigning the plan view layout

#### Lane Ownership
| Lane | Responsibility |
|------|---------------|
| 2 (Backend) | Discipline derivation fix, Rift Essence normalization, Required Recipes enrichment |
| 1 (UI) | Section collapse reflow fix (≤20-line cross-lane exception, documented in commit message) |

#### Acceptance Criteria (Verifiable)
- [ ] Required disciplines list contains only disciplines that appear in at least one crafting step where `Decision == Craft`
- [ ] Multi-discipline recipes (e.g., items craftable by Weaponsmith OR Huntsman) contribute only the discipline(s) used in the actual craft steps
- [ ] Rift Essence counts in summary, shopping list, and crafting steps are mutually consistent for any plan containing Rift Essences
- [ ] Collapsing any section reflows remaining content without leftover blank space
- [ ] Expanding a previously collapsed section restores content and reflows correctly
- [ ] Required Recipes entries display discipline, rating, and learned/unlearned status
- [ ] No regressions in existing plan generation for Deldrimor Steel Ingot and Elonian Leather Square
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

#### Risks / Edge Cases
- **Multi-discipline recipes**: A recipe craftable by multiple disciplines needs careful handling — only the discipline(s) relevant to the solver's chosen craft path should appear
- **Rift Essences as dual-nature items**: Rift Essences can appear as both a vendor currency cost and a normal material ingredient; normalization must handle both roles without double-counting
- **Collapse reflow on deep plans**: Plans with many sections (e.g., Legendary items) may expose resize/reflow performance issues in the `FlowPanel`

#### Parallelization Notes
- Discipline derivation fix and Rift Essence normalization are independent backend changes — can be developed and tested in separate commits
- The collapse reflow fix is a UI-only change that can be developed independently of the backend fixes
- Required Recipes enrichment depends on existing `Gw2AccountRecipeClient` and `RequiredRecipe` model — no new seams needed

---

### M10: Plan Generation Performance & Observability (COMPLETED - PRs #14-19)

**Branch**: m10-performance-observability
**Dependencies**: None

#### Problem Statement
`CraftingPlanPipeline` currently reports progress only during vendor resolution. For complex items (e.g., Zojja's Claymore with 50+ intermediate recipes), the UI shows no feedback during the majority of plan generation, creating the impression of a hang. Additionally, there is no timing data to identify which pipeline phases are bottlenecks.

The pipeline has ~10 sequential phases in the structured variant:
1. Recipe tree expansion (`BuildRecipeTreeAsync`)
2. Item ID collection
3. TP price lookup (`FetchPricesAsync`)
4. Vendor offer resolution (`ResolveVendorOffersAsync`)
5. Vendor offer querying
6. Inventory reduction (`InventoryReducer`)
7. Solving (`PlanSolver`)
8. Item metadata fetch
9. Recipe-unlock checking (`Gw2AccountRecipeClient`)
10. Result building (`CraftingPlanResult` assembly)

Only phase 4 currently reports progress via `IProgress<PlanStatus>`.

#### Scope
- Extend `PlanStatus` reporting to cover all pipeline phases listed above
- Report progress through existing `IProgress<PlanStatus>` parameter — no new UI wiring needed
- Add elapsed-time tracking per pipeline phase to `DebugLog` in `CraftingPlanResult`
- Profile plan generation using Zojja's Claymore as the benchmark item (complex Legendary precursor with deep recipe tree)
- Optimize hot paths identified during profiling (e.g., redundant API calls, repeated tree traversals)

#### Non-Goals
- Pipeline restructuring or reordering of phases
- Cancellation UI (cancellation token already threaded through; UI for it is a future concern)
- Cross-generation caching (caching price or recipe data between plan generations)
- Changing the `PlanStatus` model structure (Message/Current/Total is sufficient)

#### Lane Ownership
| Lane | Responsibility |
|------|---------------|
| 2 (Backend) | All progress reporting, timing, profiling, and optimization |
| 1 (UI) | No changes — already consumes `PlanStatus` updates via existing wiring |

#### Acceptance Criteria (Verifiable)
- [ ] `PlanStatus` reports a descriptive message for each of the ~10 pipeline phases
- [ ] Phases with countable work (e.g., price lookup for N items, metadata fetch for N items) report Current/Total progress
- [ ] `DebugLog` includes elapsed-time entries (milliseconds) for each pipeline phase
- [ ] Plan generation for Zojja's Claymore completes without UI freeze
- [ ] No regressions in plan output correctness for existing test items
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

#### Risks / Edge Cases
- **Phases with no meaningful denominator**: Some phases (e.g., solving, result assembly) have no countable progress — these report phase-start/phase-end messages only
- **API rate limits**: Price lookup and metadata fetch for large item sets may hit GW2 API rate limits; progress reporting should not mask underlying fetch failures
- **Timer overhead**: `Stopwatch` per phase adds negligible overhead but must not accumulate in `DebugLog` if the pipeline is called in a tight loop (it is not currently, but guard against it)

#### Parallelization Notes
- Progress reporting additions are independent per phase — can be added incrementally
- Elapsed-time tracking can be added in a single pass after progress reporting is wired
- Profiling and optimization are separate commits from the reporting infrastructure

---

### M11: Crafting Tree Visualization (COMPLETED - PR #20)

**Branch**: m11-tree-visualization
**Dependencies**: M9

#### Problem Statement
The plan view presents crafting data as flat lists (summary, shopping list, crafting steps). Users cannot see the dependency structure — which items feed into which intermediates, and how the solver's buy/craft decisions propagate through the tree. A visual tree would make complex plans (Legendaries, Ascended gear) comprehensible at a glance.

The `RecipeNode` model (`Models/RecipeNode.cs`) captures the full recipe tree during expansion, but it is consumed and discarded within `CraftingPlanPipeline` — `CraftingPlanResult` does not include it. The solver's decisions (buy/craft/have) are also not attached to `RecipeNode`. A new DTO is needed to capture the tree with solver annotations and pass it through to the UI.

#### Scope
- **Central (Lane 3)**: Define a stable tree DTO contract in `Contracts/` representing the crafting dependency graph
  - Nodes: item ID, item name, quantity needed, buy/craft/have decision
  - Edges: parent-child ingredient relationships
  - Annotations: solver decision, unit cost, total cost
- **Backend (Lane 2)**: After solving, walk the `RecipeNode` tree and `CraftingPlan` decisions to populate the tree DTO; attach it to `CraftingPlanResult`
- **UI (Lane 1)**: Render the tree as a collapsible, scrollable visual hierarchy in a new panel within `CraftingPlanView`

#### Non-Goals
- Editable trees (user cannot change buy/craft decisions via the tree)
- Drag-and-drop reordering
- Tree diffing (comparing two plans)
- Alternative recipe paths (the tree shows only the solver's chosen path, not all options)

#### Lane Ownership
| Lane | Responsibility |
|------|---------------|
| 3 (Central) | Tree DTO contract in `Contracts/`; `.csproj` registration |
| 2 (Backend) | Tree DTO population from `RecipeNode` + solver decisions; attach to `CraftingPlanResult` |
| 1 (UI) | Tree rendering panel in `CraftingPlanView` |

#### Required Seams / Contracts
- **Tree DTO** in `Contracts/`: The cross-lane seam. Must be defined by Lane 3 before Lanes 1 and 2 can begin implementation.
  - Minimum fields: `int ItemId`, `string Name`, `int Quantity`, `Decision` (enum: Buy/Craft/Have), `IReadOnlyList<TreeNode> Children`
  - Lane 2 populates; Lane 1 consumes

#### Acceptance Criteria (Verifiable)
- [ ] Tree DTO contract defined in `Contracts/` with item ID, name, quantity, decision enum, and child nodes
- [ ] Pipeline populates tree DTO for any generated plan and attaches it to `CraftingPlanResult`
- [ ] UI renders tree with expandable/collapsible nodes showing item name, quantity, and decision icon/label
- [ ] Tree accurately reflects the solver's buy-vs-craft decisions for each node
- [ ] Leaf nodes (bought or owned items) are visually distinct from intermediate craft nodes
- [ ] Deep trees (e.g., Legendary items with 5+ levels) remain scrollable and navigable
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

#### Risks / Edge Cases
- **Multiple recipe options per node**: `RecipeNode.Recipes` is a `List<RecipeOption>` — the solver picks one. The tree DTO must show only the chosen path, not all alternatives
- **Deep trees for Legendaries**: Legendary items can have 5–7 levels of nesting; the UI must handle deep indentation without horizontal overflow
- **Quantity propagation**: The tree must correctly reflect quantity multiplication (e.g., needing 3 of an intermediate that itself requires 2 of a sub-component = 6 total)
- **Items appearing at multiple tree positions**: The same item may appear in multiple branches; the tree shows each occurrence independently (not merged)

#### Parallelization Notes
- Lane 3 (DTO contract) must complete first — this is the blocking seam
- Lane 2 (population) and Lane 1 (rendering) can begin in parallel once the DTO is defined, using stub data for initial UI development

#### Reference Requests
- gw2efficiency crafting tree visualization for UX inspiration (layout, expand/collapse behavior, decision indicators)

---

### M12: Account Data & Material Sourcing Transparency (COMPLETED - PR #21)

**Branch**: m12-account-sourcing
**Dependencies**: M9

#### Problem Statement
When the reducer uses owned materials to offset crafting costs, the plan says "you have X of this item" but not *where* it is. Users need to know whether to visit the bank, check material storage, or switch characters.

Two structural issues block sourcing transparency:
1. **`SnapshotHelpers.AggregateItems`** collapses all storage sources into `Source = "Total"`, discarding per-location provenance. The reducer consumes aggregated data and cannot report where materials came from.
2. **`UsedMaterial`** has only `ItemId` and `QuantityUsed` — no field for source location.

The fix requires preserving per-source item entries through the reduction pipeline and enriching `UsedMaterial` with source metadata.

#### Scope
- **Lane 2**: Introduce an account-item index abstraction that provides fast lookup of item quantities per storage location (material storage, bank, shared inventory, per-character bags) — building on `AccountSnapshot` without replacing it
- **Lane 2**: Modify `InventoryReducer` to consume per-source entries (not aggregated) and record source location in `UsedMaterial`
- **Lane 2**: Extend `UsedMaterial` with source metadata (storage type + detail, e.g., "MaterialStorage", "Bank", "Character:Zojja")
- **Lane 2**: Ensure greedy consumption respects priority: MaterialStorage → active character inventory → SharedInventory → other character inventories
- **Lane 2**: Surface per-material sourcing information in `CraftingPlanResult` so `PlanViewModelBuilder` can consume it

#### Non-Goals
- UI rendering of sourcing information (Lane 1 concern, deferred to future milestone or M16)
- Configurable source priority order (hardcoded priority is sufficient)
- Real-time inventory tracking (snapshot-based only)
- Modifying `SnapshotHelpers.AggregateItems` — it remains available for contexts where aggregation is appropriate (e.g., snapshot display)

#### Lane Ownership
| Lane | Responsibility |
|------|---------------|
| 2 (Backend) | Account index, `UsedMaterial` enrichment, `InventoryReducer` changes, test coverage |

#### Acceptance Criteria (Verifiable)
- [ ] Account index provides O(1) lookup for any item ID, returning quantity per storage location
- [ ] `UsedMaterial` records include source location (storage type string + detail string)
- [ ] Reducer consumes per-source entries and draws from highest-priority source first
- [ ] Source priority order: MaterialStorage → active character → SharedInventory → other characters
- [ ] Plan results expose per-material sourcing information consumable by `PlanViewModelBuilder`
- [ ] No regressions in inventory reduction correctness (existing reducer tests pass)
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

#### Risks / Edge Cases
- **Active character identification**: `AccountSnapshot` does not currently identify which character is "active" (logged in). The snapshot captures all characters equally. May need to accept a character name parameter or default to treating all characters equally until Mumble link or similar identification is available.
- **Split consumption**: If an item exists in both MaterialStorage (50) and Bank (30), and the plan needs 70, the reducer must split the `UsedMaterial` into two entries with different sources
- **Empty storage locations**: Some accounts may have no bank access, no shared inventory slots, or single-character accounts — the index must handle missing/empty locations gracefully
- **`SnapshotItemEntry.Source` values**: Current source strings in snapshot data (e.g., "Bank", "MaterialStorage", "Character:Name") must be cataloged and standardized

#### Parallelization Notes
- Account index design and `UsedMaterial` model extension can be done in parallel
- `InventoryReducer` modification depends on both the index and the extended model
- Tests should be written alongside each component (index tests, reducer tests with sourcing assertions)

---

### M13: Item Selection & Search Expansion (COMPLETED - PR #22)

**Branch**: m13-item-search
**Dependencies**: M9

#### Problem Statement
The module currently uses `StaticItemSearchProvider` which returns a hardcoded two-item list. Users cannot search for or select arbitrary craftable items. The `IItemSearchProvider` contract (`Contracts/IItemSearchProvider.cs`) already defines `SearchAsync(string query, int maxResults, CancellationToken ct)` returning `IReadOnlyList<ItemSearchResult>` with `IsPlanTarget` guarantees — the contract appears adequate as-is.

A real provider must index all items that `CraftingPlanPipeline` can generate plans for: discipline recipe outputs from the GW2 API and Mystic Forge outputs from `CompositeRecipeApiClient`'s bundled recipe data.

#### Scope
- **Central (Lane 3)**: Review `IItemSearchProvider` contract — extend only if the existing signature is insufficient (likely no changes needed)
- **Backend (Lane 2)**: Implement a real item search provider that indexes:
  - Discipline recipe outputs (items produced by recipes from `/v2/recipes`)
  - Mystic Forge outputs (items produced by recipes from `CompositeRecipeApiClient`'s bundled data)
- Provider must honour the `IsPlanTarget` contract: every returned item is a valid plan target
- Support prefix and substring text matching for item names
- **Central (Lane 3)**: Wire new provider in `Module.cs` service composition, replacing `StaticItemSearchProvider` as the default

#### Non-Goals
- Fuzzy matching (Levenshtein distance, typo tolerance) — prefix/substring is sufficient for V1
- Rarity or discipline filtering in search results
- Arbitrary GW2 item search (only craftable items are indexed)
- Persisting the index to disk (rebuilt on module load)

#### Lane Ownership
| Lane | Responsibility |
|------|---------------|
| 3 (Central) | Contract review, `Module.cs` wiring, `.csproj` registration |
| 2 (Backend) | Search provider implementation, index building, tests |

#### Acceptance Criteria (Verifiable)
- [ ] New provider indexes all craftable items from discipline recipes and bundled Mystic Forge recipes
- [ ] Search returns results matching prefix or substring of item name (case-insensitive)
- [ ] Every returned item satisfies `IsPlanTarget` — `CraftingPlanPipeline` can generate a plan for it
- [ ] `StaticItemSearchProvider` remains available as a fallback but is no longer the default
- [ ] Search results return within a reasonable time for the full item index (~10k+ items)
- [ ] Items craftable via both discipline recipes and Mystic Forge are deduplicated in the index
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

#### Risks / Edge Cases
- **Slow index building on first launch**: Fetching all recipe outputs from the GW2 API on module load could be slow; may need to build the index lazily or cache recipe IDs
- **Deduplication**: Some items have both discipline recipes and Mystic Forge recipes (e.g., certain ascended weapons). The index must present each item once, not as duplicates
- **Name collisions**: Different items can share names (e.g., "Bolt of Damask" as intermediate vs final product). `ItemId` disambiguation is internal; search results must be unambiguous to the user (e.g., via icon or context)
- **Recipe output items without names**: The recipe API returns item IDs, not names. A metadata fetch is needed to populate names for the index, adding to initialization cost

#### Parallelization Notes
- Contract review (Lane 3) is quick and can unblock Lane 2 early
- Index building and search logic are independent of UI changes
- `Module.cs` wiring (Lane 3) is a final integration step after the provider is tested

---

### M14: Navigation & Multi-Tab Expansion (COMPLETED - PR #23)

**Branch**: m14-navigation-tabs
**Dependencies**: M9

#### Problem Statement
The current UI uses a simple two-button tab bar (`MainView` creates "Snapshot" and "Crafting Plan" buttons in a horizontal panel). This does not scale to additional views (tree visualization, debug log, account explorer, settings). The tab switching mechanism is ad-hoc — buttons with click handlers that call back into `Module.cs` to swap views.

A proper navigation system is needed: a persistent left-hand vertical tab panel (similar to the Event Table module's layout) that supports adding tabs without modifying core layout code.

#### Scope
- **Lane 1**: Replace the horizontal button tab bar with a left-hand vertical tab panel
- **Lane 1**: Implement initial tabs: Snapshot, Crafting Plan, Log (displaying `DebugLog` from `CraftingPlanResult`)
- **Lane 1**: Add placeholder tab stubs for future views: Plan History, Crafting Ranker, Settings, About
- **Lane 1**: Display a large item icon in the top-left area of the window (above or beside the tab panel)
- **Lane 1**: Ensure tab switching preserves state — returning to a tab shows its last state (e.g., a generated plan is not lost when switching to snapshot and back)
- **Lane 1**: Establish a tab registration pattern so future milestones can add tabs by registering with the system rather than editing layout code

#### Non-Goals
- Implementing content for placeholder tabs (they show a "Coming Soon" label or similar)
- Backend changes (all data is already available via existing models)
- Deep view redesign of Snapshot or Crafting Plan content (layout changes only for tab integration)

#### Lane Ownership
| Lane | Responsibility |
|------|---------------|
| 1 (UI) | All navigation, tab panel, view switching, state preservation |

#### Acceptance Criteria (Verifiable)
- [ ] Left-hand vertical tab panel with icon+label for each tab
- [ ] Active tab is visually highlighted; clicking a tab switches the content area
- [ ] Snapshot, Crafting Plan, and Log tabs are functional with real content
- [ ] Placeholder tabs (Plan History, Crafting Ranker, Settings, About) display stub content
- [ ] Switching tabs preserves state — returning to Crafting Plan shows the last generated plan
- [ ] Large item icon displayed in the top-left area
- [ ] Tab panel does not overflow or clip with 7+ tabs at minimum supported window size
- [ ] New tabs can be added by registering with the tab system (no core layout file edits required)
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

#### Risks / Edge Cases
- **Narrower content area**: A vertical tab panel consumes horizontal space, reducing the content area width. Plans and snapshot lists must remain readable at the narrower width.
- **Building from primitives**: Blish HUD does not provide a tab panel control. The vertical tab panel must be built from `Panel`, `Label`, `Image`, and click handlers — more complex than using a library widget.
- **State preservation memory**: Keeping all tab views alive (not disposing on switch) increases memory usage. For the current tab count (7) this is negligible, but the pattern should support lazy initialization for future tabs.
- **Window resize**: The tab panel must handle window resizing gracefully (the module uses a resizable `StandardWindow`)

#### Parallelization Notes
- Tab panel infrastructure and individual tab content can be developed independently
- Log tab depends on `CraftingPlanResult.DebugLog` which already exists — no backend work needed
- Placeholder tabs are trivial and can be added last

#### Reference Requests
- Event Table module's left-hand vertical tab layout for UX reference
- Blish HUD settings window tab structure for additional layout inspiration

---

### M15: Mystic Forge Recipe Seeder (COMPLETED - PR #24)

**Lane**: 3 (Central)
**Branch**: m15-mystic-forge-seeder
**Dependencies**: None

#### Problem Statement
`ref/mystic_forge_recipes.json` has only 4 hand-crafted recipes. The GW2 API has no Mystic Forge endpoint. The GW2 Wiki has ~2,534 MF recipe entries queryable via Semantic MediaWiki. A seeder tool is needed to scrape the wiki, resolve item IDs, and generate the complete recipe file so items like "Orrax Manifested" appear in autocomplete search.

#### Scope
- **New tool**: `tools/MysticForgeSeeder/` (.NET 8.0 console app, no external NuGet packages)
- **WikiRecipeClient**: POST-based SMW queries with retry, backoff, jitter, pagination
- **5-step pipeline**: Query recipes, collect names, resolve IDs (with persistent cache), build recipe objects, write output
- **Deterministic output**: Byte-identical `ref/mystic_forge_recipes.json` for unchanged wiki data + cache
- **Safety limits**: `--max-requests`, `--delay`, `--dry-run`, `--force-resolve` CLI flags

#### Acceptance Criteria
- [ ] `ref/mystic_forge_recipes.json` has 2000+ recipes after seeder run
- [ ] Spot-check: "Orrax Manifested", "Eternity", "Sunrise" present with correct IDs
- [ ] All recipe IDs negative, all ingredient IDs positive
- [ ] Repeated run produces byte-identical output (determinism invariant)
- [ ] `ref/mf_item_id_cache.json` persists resolved IDs across runs
- [ ] Existing RecipeSeeder, build, and tests still pass
- [ ] Build passes (0 errors, 0 warnings)

---

### M16: Snapshot -> Account Explorer Evolution

**Branch**: m16-account-explorer
**Dependencies**: M12

#### Problem Statement
The current snapshot tab (`MainView`) displays a flat scrollable list of items and wallet entries. With hundreds of items across bank, material storage, and multiple characters, the flat list is unwieldy. Users cannot quickly find specific items, sort by value, or understand where their items are stored.

M12 introduces an account-item index with per-source location data. M16 leverages that index to present a structured, searchable, grouped account explorer -- transforming the snapshot tab from a debug-style dump into a useful inventory management view.

#### Scope
- **Backend (Lane 2)**: Reuse the M12 account-item index to provide grouped views by storage location (MaterialStorage, Bank, SharedInventory, per-character)
- **Backend (Lane 2)**: Support sort keys: item name (alphabetical), quantity (descending), TP value (descending, using existing price data)
- **UI (Lane 1)**: Replace the current flat `FlowPanel` list in `MainView` with a categorized, collapsible layout grouped by storage location
- **UI (Lane 1)**: Add a search bar for filtering items by name substring
- **UI (Lane 1)**: Add sort controls (dropdown or toggle buttons) for name/quantity/TP value
- Preserve the existing refresh/snapshot workflow (`SnapshotStore` persistence, background API fetch, `_snapshotDirty` pattern in `Module.cs`)

#### Non-Goals
- Real-time inventory tracking (remains snapshot-based with manual refresh)
- Item editing or moving (read-only view)
- Duplicating M12 logic — reuse the account index, do not reimplement source tracking
- Wallet section redesign (wallet entries remain as-is for now)

#### Lane Ownership
| Lane | Responsibility |
|------|---------------|
| 2 (Backend) | Grouped data projection from M12 index, sort logic |
| 1 (UI) | Account explorer layout, search bar, sort controls, collapsible groups |

#### Acceptance Criteria (Verifiable)
- [ ] Account explorer groups items by storage location (MaterialStorage, Bank, SharedInventory, per-character)
- [ ] Each group is collapsible; collapsing reflows layout cleanly (no leftover whitespace)
- [ ] Items within each group can be sorted by name, quantity, or TP value
- [ ] Search bar filters displayed items by name substring (case-insensitive)
- [ ] Snapshot refresh workflow unchanged — background fetch, dirty-flag update, `SetSnapshot()` pattern preserved
- [ ] Empty storage groups are either hidden or show a "No items" indicator (not blank sections)
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

#### Risks / Edge Cases
- **Large inventories**: Accounts with 1000+ items across all storage may cause UI performance issues in the `FlowPanel`. May need virtualization or pagination for very large groups.
- **Missing price data**: Not all items have TP prices (account-bound, untradeable). Sort-by-value must handle items with no price gracefully (sort to bottom or show "N/A").
- **M12 dependency**: If M12's account index API changes during development, the explorer must adapt. Pin to the M12 contract early.
- **`SnapshotItemEntry.Source` before M12**: Lane 1 can begin development using existing `SnapshotItemEntry.Source` field values (which already contain "Bank", "MaterialStorage", "Character:Name" strings) before M12 delivers the full index. This allows early UI prototyping.

#### Parallelization Notes
- Lane 1 (UI) can start with existing `SnapshotItemEntry.Source` for grouping, independent of M12's index
- Lane 2 (sort logic) can be developed and tested independently of the UI
- Search filtering is a pure UI concern and can be developed independently of grouping/sorting

#### Reference Requests
- GW2 in-game bank and material storage UI for layout inspiration (tabbed storage groups, grid vs list)
- gw2efficiency account page for feature comparison (what grouping/sorting/filtering options do users expect?)

---

### M17: Visual Parity with Event Table

**Lane**: 1 (UI)
**Branch**: m17-visual-parity
**Dependencies**: M14

#### Problem Statement
The module's window uses a custom `ShellView` with manual tab rendering (text labels, flat panels, hardcoded dark colors) inside a `ResizableModuleWindow` (extends `StandardWindow`). This looks noticeably different from other polished Blish HUD modules like Event Table, which use the built-in `TabbedWindow2` control for native GW2-style window chrome, icon-based sidebar tabs, and an overlapping emblem badge.

#### Scope
- Replace `ResizableModuleWindow` + `ShellView` with `TabbedWindow2`
- Icon-based sidebar tabs with tooltip names (matching Event Table's pattern)
- Large app emblem in top-left corner (overlapping sidebar)
- GW2-native window textures, borders, title bar, sidebar rendering
- Preserve all resize behavior and minimum size enforcement
- Preserve all existing functionality -- pure presentation-layer change
- Create `ViewAdapter` (thin `View` wrapper) to bridge existing `Build(Container)` classes to `TabbedWindow2`'s `Func<IView>` tab interface

#### Non-Goals
- Backend, service, model, or contract changes
- Crafting logic, plan solver, or pricing changes
- Font or typography changes (deferred unless trivially achievable)
- New features or functional behavior changes

#### Acceptance Criteria (Verifiable)
- [ ] Window renders with GW2-native chrome (title bar, sidebar, split line, fade gradient)
- [ ] Sidebar tabs are icon-only with tooltip names on hover
- [ ] App emblem displays in top-left, overlapping sidebar area
- [ ] All existing tabs function identically to current behavior
- [ ] Window resize enforces minimum size and persists position/size
- [ ] No visible flicker on tab switch
- [ ] No performance regression (frame rate, resize latency)
- [ ] Corner icon toggles window as before
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass
