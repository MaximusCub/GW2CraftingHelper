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

### M9–M12: TBD

_To be planned when work begins. Candidate areas:_

- _API-backed item search (replace static dropdown)_
- _Tree visualization (crafting dependency graph)_
- _Multi-plan management (save/load plans)_
- _Progress tracking (mark steps complete)_
