# GW2CraftingHelper — Roadmap

> **Supersedes** all prior plan documents (now archived in `docs/archive/plans/2026-02-15/`).
> **Status date**: 2026-02-15

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

### M9: Plan View Correctness & Integrity

**Lane**: 2 (Backend)
**Branch**: m9-plan-correctness
**Dependencies**: None

#### Scope
- Fix discipline derivation: required disciplines must match only those actually present in crafting steps (no spurious entries from intermediate recipes the solver chose to buy instead of craft)
- Resolve Rift Essences inconsistency: ensure Rift Essence quantities are consistent across summary, shopping list, and crafting steps sections
- Fix section collapse reflow: collapsed sections must reflow layout without leftover whitespace or stale height

#### Main-View-Composition Owner
Lane 2 (backend logic fixes only; no view wiring changes)

#### Acceptance Criteria
- [ ] Required disciplines list contains only disciplines that appear in at least one crafting step
- [ ] Rift Essence counts in summary, shopping list, and crafting steps are mutually consistent
- [ ] Collapsing any section reflows remaining content without leftover blank space
- [ ] No regressions in existing plan generation for Deldrimor Steel Ingot and Elonian Leather Square
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

---

### M10: Plan Generation Performance & Observability

**Lane**: 2 (Backend)
**Branch**: m10-performance-observability
**Dependencies**: None

#### Scope
- Extend `PlanStatus` reporting beyond vendor resolution to cover recipe expansion, price lookup, and solver phases
- Surface progress through existing `IProgress<PlanStatus>` parameter in `CraftingPlanPipeline`
- Profile plan generation for complex items (e.g., Legendary precursors) and identify bottleneck phases
- Add elapsed-time tracking per pipeline phase to `DebugLog` in `CraftingPlanResult`
- Optimize hot paths identified during profiling (e.g., redundant API calls, repeated tree traversals)

#### Main-View-Composition Owner
Lane 1 (UI consumes `PlanStatus` updates — already wired; no new wiring needed)

#### Acceptance Criteria
- [ ] `PlanStatus` reports progress for recipe expansion, price lookup, solver, and result-building phases
- [ ] `DebugLog` includes elapsed-time entries for each pipeline phase
- [ ] Plan generation for a complex item (≥50 intermediate recipes) completes without UI freeze
- [ ] No regressions in plan output correctness
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

---

### M11: Crafting Tree Visualization

**Lane**: 3 (Central/Integration) + 2 (Backend) + 1 (UI)
**Branch**: m11-tree-visualization
**Dependencies**: M9

#### Scope
- **Central (Lane 3)**: Define a stable tree DTO contract in `Contracts/` representing the crafting dependency graph (nodes = items/recipes, edges = ingredient relationships, annotations = buy/craft/have decisions)
- **Backend (Lane 2)**: Expose tree DTO from `RecipeNode` DAG structure through the pipeline, populated with solver decisions and quantities
- **UI (Lane 1)**: Render the tree as a collapsible, scrollable visual hierarchy in a new tab or panel within `CraftingPlanView`

#### Main-View-Composition Owner
Lane 1 (new tab/panel rendering within existing `CraftingPlanView`)

#### Acceptance Criteria
- [ ] Tree DTO contract defined in `Contracts/` with item ID, name, quantity, buy/craft/have decision, and child nodes
- [ ] Pipeline populates tree DTO for any generated plan
- [ ] UI renders tree with expandable/collapsible nodes showing item name, quantity, and decision
- [ ] Tree accurately reflects the solver's buy-vs-craft decisions for each node
- [ ] Leaf nodes (bought or owned items) are visually distinct from intermediate craft nodes
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

---

### M12: Account Data & Material Sourcing Transparency

**Lane**: 2 (Backend)
**Branch**: m12-account-sourcing
**Dependencies**: M9

#### Scope
- Introduce an account-item index abstraction that provides fast lookup of item quantities across bank, material storage, and character inventories (building on `AccountSnapshot` and `InventoryReducer`)
- Enhance `UsedMaterial` tracking to record source location (bank slot, material storage tab, character name) for each consumed material
- Surface "where did this come from?" information in plan results so the UI can display sourcing details
- Ensure greedy consumption strategy in `InventoryReducer` respects source priority (material storage → bank → characters)

#### Main-View-Composition Owner
Lane 2 (backend-only; UI displays results via existing plan view model pipeline)

#### Acceptance Criteria
- [ ] Account index provides O(1) lookup for any item ID across all storage locations
- [ ] `UsedMaterial` records include source location (storage type + detail)
- [ ] Plan results expose per-material sourcing information consumable by `PlanViewModelBuilder`
- [ ] Source priority order: material storage → bank → character inventories
- [ ] No regressions in inventory reduction correctness
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

---

### M13: Item Selection & Search Expansion

**Lane**: 2 (Backend) + 3 (Central)
**Branch**: m13-item-search
**Dependencies**: M9

#### Scope
- **Central (Lane 3)**: Extend `IItemSearchProvider` contract if needed to support search-by-name with pagination or result limits
- **Backend (Lane 2)**: Implement a real item search provider that replaces `StaticItemSearchProvider`, indexing discipline recipe outputs and Mystic Forge outputs from `CompositeRecipeApiClient`
- Provider must honour the `IsPlanTarget` contract: returned items are guaranteed valid plan targets for `CraftingPlanPipeline`
- Support fuzzy/prefix text matching for item names

#### Main-View-Composition Owner
Lane 3 (service composition in `Module.cs` to wire new provider; Lane 1 already consumes `IItemSearchProvider`)

#### Acceptance Criteria
- [ ] New provider indexes all craftable items from discipline recipes and bundled Mystic Forge recipes
- [ ] Search returns results matching prefix or substring of item name
- [ ] Every returned item satisfies `IsPlanTarget` — `CraftingPlanPipeline` can generate a plan for it
- [ ] `StaticItemSearchProvider` remains available as a fallback but is no longer the default
- [ ] Search results return within a reasonable time for the full item index
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

---

### M14: Navigation & Multi-Tab Expansion

**Lane**: 1 (UI)
**Branch**: m14-navigation-tabs
**Dependencies**: M9

#### Scope
- Refactor main view composition to support additional tabs beyond the current snapshot/crafting pair
- Add a debug/log tab surfacing `DebugLog` from `CraftingPlanResult` for troubleshooting
- Establish tab registration pattern so future milestones can add tabs without modifying core layout code
- Ensure tab switching preserves state (e.g., a generated plan is not lost when switching to the snapshot tab and back)

#### Main-View-Composition Owner
Lane 1 (owns main view composition and tab layout in `Module.cs` view wiring)

#### Acceptance Criteria
- [ ] Tab bar supports 3+ tabs without layout overflow
- [ ] Debug/log tab displays `DebugLog` entries from the most recent plan generation
- [ ] Switching tabs preserves plan state — returning to crafting tab shows the last generated plan
- [ ] New tabs can be added by registering with the tab system (no core layout file edits required)
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable

---

### M15: Snapshot → Account Explorer Evolution

**Lane**: 1 (UI) + 2 (Backend)
**Branch**: m15-account-explorer
**Dependencies**: M12

#### Scope
- Evolve the snapshot tab (`MainView`) from a flat item/wallet list into a structured account explorer
- **Backend (Lane 2)**: Reuse the account-item index from M12 to provide grouped views (by storage location, by item category, by value)
- **UI (Lane 1)**: Replace the current flat `FlowPanel` list with a categorized, searchable, collapsible layout
- Add filtering/sorting options (by name, by quantity, by TP value) using existing `AccountSnapshot` and price data
- Preserve the existing refresh/snapshot workflow (`SnapshotStore` persistence, background API fetch, `_snapshotDirty` pattern)

#### Main-View-Composition Owner
Lane 1 (view evolution of existing `MainView`)

#### Acceptance Criteria
- [ ] Account explorer groups items by storage location (bank, material storage, characters)
- [ ] Items within each group can be sorted by name, quantity, or TP value
- [ ] Search/filter narrows displayed items by name substring
- [ ] Snapshot refresh workflow unchanged — background fetch, dirty-flag update, `SetSnapshot()` pattern preserved
- [ ] Collapsing a storage group reflows layout cleanly (no leftover whitespace)
- [ ] Build passes (0 errors, 0 warnings)
- [ ] All existing tests pass; new tests added where applicable
