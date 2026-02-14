# Phase B Milestone Plan

## Deliverable
Create `docs/milestones-phase-b.md` with the milestone plan below, then implement Milestones 5-7 only. Defer Milestone 8 (UI rewrite) until after Phase B logic is validated with Orrax Manifested.

## Context
Phase A (milestones 1-4) built: recipe tree, TP prices, plan solver (buy/craft/vendor), and a basic CraftingPlanView (hardcoded Mithril Ingot, qty 1). Phase B adds "use own materials" behavior, discipline/recipe tracking, and structured plan outputs — converging toward gw2efficiency's crafting calculator tabs.

**Core design decision: pre-solve tree reduction.** The PlanSolver remains unchanged. A new `InventoryReducer` clones the recipe tree, subtracts owned materials top-down (depth-first, mutable pool), recalculates `CraftsNeeded` and ingredient quantities, and prunes fully-covered subtrees. The solver then operates on the reduced tree, naturally excluding owned items from buy/craft steps. Required disciplines and recipes are derived only from the final Craft steps (post-reduction, post-solve).

**No `AlreadyOwned` acquisition source.** Ownership is expressed by quantity reduction on the tree + the `UsedMaterials` report. The solver never sees owned items.

## Global Constraints (apply to every milestone)
- Tests must be Blish-free — no references to BlishHUD, Blish HUD.exe, Gw2Sharp, or any UI code
- Tests must exercise real production code paths (no mirror implementations, no fake file I/O)
- Prefer incremental vertical slices; avoid broad UI refactors until core logic is stable
- Avoid GPL contamination
- `.csproj` uses explicit `<Compile Include>` — every new .cs file must be registered

---

## Milestone 0: Repo Orientation

| Area | Key Files |
|------|-----------|
| Recipe tree | `Services/RecipeService.cs`, `Models/RecipeNode.cs`, `Models/RecipeOption.cs` |
| Recipe API | `Services/IRecipeApiClient.cs` (RawRecipe, RawIngredient), `Services/Gw2RecipeApiClient.cs` |
| Solver | `Services/PlanSolver.cs` (Evaluate/Collect two-pass, memo key `(itemId, qty)`) |
| Plan output | `Models/CraftingPlan.cs`, `Models/PlanStep.cs`, `Models/AcquisitionSource.cs`, `Models/CurrencyCost.cs` |
| Pipeline | `Services/CraftingPlanPipeline.cs` (orchestrates tree→prices→vendor→solve→metadata) |
| Plan result | `Models/CraftingPlanResult.cs` — `{ Plan, ItemMetadata }` |
| Account data | `Models/AccountSnapshot.cs`, `Models/SnapshotItemEntry.cs`, `Services/SnapshotHelpers.cs` |
| Vendor system | `Services/VendorOfferStore.cs`, `Services/VendorOfferResolver.cs` |
| UI | `Views/CraftingPlanView.cs` (hardcoded 19685 qty 1), `Module.cs` |
| Tests | `PlanSolverTests.cs` (41), `CraftingPlanPipelineTests.cs` (6), `RecipeServiceTests.cs` (8) |
| Test helpers | `InMemoryRecipeApiClient`, `InMemoryPriceApiClient`, `InMemoryItemApiClient`, `InMemoryWikiVendorClient` |

**Key observations:**
- `RawRecipe` has NO `Disciplines`, `MinRating`, or `Flags` — `Gw2RecipeApiClient` skips them
- `RecipeOption` has NO discipline/flag fields
- `PlanSolver` is pure (no account awareness) — ideal for pre-solve reduction approach
- `SnapshotHelpers.AggregateItems()` already groups items by ID and sums counts
- `AcquisitionSource` enum: `BuyFromTp, Craft, Currency, BuyFromVendor, UnknownSource`
- GW2 API `/v2/recipes` returns `disciplines: [str]`, `min_rating: int`, `flags: [str]` (e.g. `["AutoLearned"]`)

---

## Milestone 5: Recipe Metadata (Disciplines, Rating, Flags)

> **Global constraints apply.** Tests Blish-free, real production paths, no mirror implementations.

**Goal:** Capture `disciplines`, `min_rating`, and `flags` from GW2 API recipes and carry them through to `RecipeOption`, so downstream code can determine required disciplines and whether recipes are auto-learned.

### Tasks
- Add to `RawRecipe` (`Services/IRecipeApiClient.cs`):
  - `List<string> Disciplines` (default: `new List<string>()`)
  - `int MinRating` (default: `0`)
  - `List<string> Flags` (default: `new List<string>()`)
- Parse in `Gw2RecipeApiClient.GetRecipeAsync()`: read `disciplines` array, `min_rating` int, `flags` array from JObject. Missing fields → defaults (empty/zero).
- Add same three fields to `RecipeOption` (`Models/RecipeOption.cs`) with same defaults
- Copy fields in `RecipeService.BuildNodeAsync()` when constructing `RecipeOption` from `RawRecipe`:
  ```csharp
  Disciplines = new List<string>(raw.Disciplines),
  MinRating = raw.MinRating,
  Flags = new List<string>(raw.Flags)
  ```

### Auto-Learned vs Unlock-Required
The `flags` array on a recipe determines learning requirements:
- `"AutoLearned"` in flags → player learns this recipe automatically when their discipline reaches `min_rating`
- No `"AutoLearned"` flag → recipe must be explicitly unlocked (from item, achievement, etc.)
- If `flags` is absent or empty from API → default to empty list (treat as "not auto-learned" = safest assumption, since missing data shouldn't grant auto-learned status)

### Acceptance Criteria
- `RecipeOption` carries disciplines/rating/flags from API through to tree nodes
- Missing fields default safely (empty lists, 0 rating) — no crashes
- All 120 existing tests pass (backward-compatible defaults)

### Tests to Add (in `RecipeServiceTests.cs`)
- `RecipeOption_CarriesDisciplinesFromRawRecipe` — set disciplines on RawRecipe, verify on RecipeOption
- `RecipeOption_CarriesMinRatingAndFlags` — set min_rating and flags, verify propagation
- `RecipeOption_DefaultsWhenFieldsAbsent` — RawRecipe with no disciplines/flags/rating set → empty lists, 0 rating on RecipeOption
- `RecipeOption_MissingFlags_DefaultsToNotAutoLearned` — empty flags list means `Flags.Contains("AutoLearned")` is false

### Files Modified
| File | Change |
|------|--------|
| `Services/IRecipeApiClient.cs` | Add 3 fields to RawRecipe |
| `Services/Gw2RecipeApiClient.cs` | Parse new fields from JObject |
| `Models/RecipeOption.cs` | Add 3 fields |
| `Services/RecipeService.cs` | Copy fields in BuildNodeAsync |

---

## Milestone 6: Inventory Reducer

> **Global constraints apply.** Tests Blish-free, real production paths, no mirror implementations.

**Goal:** Implement `InventoryReducer.Reduce()` — clones a recipe tree, subtracts owned items, recalculates ingredient quantities, and tracks consumed materials. PlanSolver stays entirely unchanged. No `AlreadyOwned` acquisition source — ownership is expressed by quantity reduction + UsedMaterials report.

### New Files
- `Models/UsedMaterial.cs` — `{ ItemId: int, QuantityUsed: int }`
- `Models/ReducedTreeResult.cs` — `{ ReducedTree: RecipeNode, UsedMaterials: List<UsedMaterial> }`
- `Services/InventoryReducer.cs`
- `tests/.../Services/InventoryReducerTests.cs`

### Algorithm (InventoryReducer.Reduce)
```
public ReducedTreeResult Reduce(RecipeNode tree, Dictionary<int, int> ownedItems)

1. Deep-clone tree (preserve original for display/cache)
   - Clone copies Disciplines, MinRating, Flags on RecipeOption (from M5)
2. Walk clone top-down, depth-first:
   a. Currency nodes: pass through unchanged (never consumed from inventory)
   b. Item nodes: consume = min(pool[id], quantity)
      - If consume > 0: debit pool, record UsedMaterial, reduce node.Quantity
      - If quantity reaches 0: clear node.Recipes (has no recipes), do NOT recurse
        → solver will never see a Craft option for this node
        → required recipes/disciplines will not include it
      - If quantity > 0 and has recipes: recalculate each RecipeOption:
        newCraftsNeeded = ceil(newQuantity / outputCount)
        each ingredient.Quantity = (origIngQty / origCraftsNeeded) * newCraftsNeeded
        recurse into ingredients
3. Aggregate UsedMaterial entries by ItemId (sum QuantityUsed)
```

### Key Design Points
- Pool is global and mutable — shared across branches; depth-first order determines which branch consumes first
- `perCraft = origIngredientQty / origCraftsNeeded` is exact integer division (original tree was built with `ingredientQty = perCraft * craftsNeeded`)
- When a node is fully owned and its recipes are cleared, the solver will not produce a Craft step for it. This means `RequiredDisciplines` and `RequiredRecipes` derived from Craft steps (in M7) will naturally exclude it — no special logic needed
- Currency nodes are never consumed (they represent wallet currencies, not inventory items)

### Acceptance Criteria
- Original tree is never mutated
- Fully-owned nodes become zero-quantity nodes with no recipes
- Partially-owned nodes have correct reduced `CraftsNeeded` and ingredient quantities
- Shared items across branches consumed depth-first from pool
- Currency nodes never consumed
- UsedMaterials aggregated by ItemId
- Traversal order is deterministic: walk recipe options and ingredient lists in their existing list order (no unordered iteration, no sorting). Tests may rely on deterministic depth-first consumption results.

### Tests to Add (`InventoryReducerTests.cs`, NEW)
- `EmptyPool_TreeUnchanged` — empty pool, reduced tree has same quantities
- `OriginalTreeNotMutated` — verify original tree unchanged after reduce
- `LeafFullyOwned_QuantityZero` — own 5 of leaf needing 5 → quantity 0
- `LeafPartiallyOwned_ReducedQuantity` — own 3 of leaf needing 5 → quantity 2
- `CraftableFullyOwned_RecipesCleared_IngredientsNotConsumed` — own intermediate product → recipes cleared, pool NOT debited for ingredients
- `PartialOwnership_RecalcsCraftsNeeded_And_IngredientQuantities` — reduced parent → correct child quantities
- `SharedItemAcrossBranches_PoolConsumedDepthFirst` — same item in two branches, first gets pool
- `CurrencyNodes_NeverConsumed` — currency-type nodes ignored
- `UsedMaterials_Aggregated` — same item consumed at multiple nodes → single aggregated entry
- `MultiLevelTree_EndToEnd` — multi-level tree with mixed ownership, verify full structure
- `FullyOwnedIntermediate_NoRecipesOnNode` — explicitly verify `node.Recipes.Count == 0` and `node.IsLeaf == true` for a fully-owned craftable node (sets up the M7 integration invariant)

---

## Milestone 7: Pipeline Integration + Structured Plan Result

> **Global constraints apply.** Tests Blish-free, real production paths, no mirror implementations.

**Goal:** Wire `InventoryReducer` into `CraftingPlanPipeline`, extend `CraftingPlanResult` minimally with UsedMaterials, RequiredDisciplines, RequiredRecipes, and a DebugLog. Required recipes/disciplines MUST be derived only from actual Craft steps in the final plan (post-reduction, post-solve).

### New Files
- `Models/RequiredDiscipline.cs` — `{ Discipline: string, MinRating: int }`
- `Models/RequiredRecipe.cs` — `{ RecipeId: int, OutputItemId: int, IsAutoLearned: bool, MinRating: int, Disciplines: List<string>, IsMissing: bool? }` (null = no unlocks permission)
- `Services/PlanResultBuilder.cs` — derives structured outputs from solved plan + tree
- `Services/IAccountRecipeClient.cs` — `{ GetLearnedRecipeIdsAsync(ct): Task<IReadOnlySet<int>>, HasRequiredPermission(): bool }`
- `tests/.../Services/PlanResultBuilderTests.cs`
- `tests/.../Helpers/InMemoryAccountRecipeClient.cs`

### Extend `CraftingPlanResult` (`Models/CraftingPlanResult.cs`)
Add fields (null/empty when not populated — backward compatible):
```csharp
public List<UsedMaterial> UsedMaterials { get; set; }
public List<RequiredDiscipline> RequiredDisciplines { get; set; }
public List<RequiredRecipe> RequiredRecipes { get; set; }
public List<string> DebugLog { get; set; }
```
Keep existing `Plan` (with `Steps`, `TotalCoinCost`, `CurrencyCosts`) and `ItemMetadata` as primary outputs. Shopping list and crafting steps are derived by filtering `Plan.Steps` — no need for separate fields yet.

### PlanResultBuilder
```csharp
public class PlanResultBuilder
{
    public CraftingPlanResult Build(
        CraftingPlan plan,
        RecipeNode treeUsedForSolve,   // reduced tree when snapshot applied, original otherwise
        IReadOnlyDictionary<int, ItemMetadata> metadata,
        List<UsedMaterial> usedMaterials,     // from InventoryReducer, or empty
        IReadOnlySet<int> learnedRecipeIds)   // null = no unlocks permission
}
```

Note: `treeUsedForSolve` is the same tree passed to `PlanSolver.Solve()`. When inventory reduction is applied, this is the reduced tree (pruned nodes removed). This ensures RecipeOption lookups match the actual craft steps the solver chose — no mismatch between pruned nodes and metadata.

**RequiredDisciplines derivation:**
1. Filter `plan.Steps` where `Source == Craft`
2. For each craft step, find its `RecipeOption` in `treeUsedForSolve` by `RecipeId` (recursive tree search)
3. Collect `(discipline, minRating)` pairs from each RecipeOption.Disciplines × MinRating
4. Aggregate: for each discipline name, keep highest MinRating
5. Return as `List<RequiredDiscipline>` sorted by discipline name

**RequiredRecipes derivation:**
1. Filter `plan.Steps` where `Source == Craft`
2. For each craft step, find its `RecipeOption` in `treeUsedForSolve`
3. Create `RequiredRecipe`:
   - `IsAutoLearned = option.Flags.Contains("AutoLearned")`
   - `IsMissing = learnedRecipeIds != null ? !learnedRecipeIds.Contains(recipeId) : (bool?)null`
   - Copy `MinRating`, `Disciplines` from option
4. Return deduplicated by RecipeId

**DebugLog:**
Compact, user-copyable lines capturing key decisions. **Boundary:** DebugLog lines must be deterministic and derived only from planning inputs/outputs (no UI/Blish dependencies), so tests can assert on them reliably.
- Tree reduction summary: `"Reduced: used N owned items (X of item A, Y of item B, ...)"` or `"No inventory reduction (snapshot not provided)"`
- Source decisions: `"Item 19684 (qty 50): BuyFromTp @ 7018c"`, `"Item 46742 (qty 1): Craft via recipe 7319"`
- Missing vendor offers: `"No vendor offers found for items: [id1, id2]"` (if applicable)
- Required disciplines: `"Required disciplines: Weaponsmith (400), Armorsmith (450)"`
- Missing recipes: `"Missing recipes: 7319 (Weaponsmith 450)"` or `"Recipe permission not available"`

### Modify `CraftingPlanPipeline`
Add constructor params: `InventoryReducer reducer` (optional), `IAccountRecipeClient accountRecipeClient` (optional)

New method (keep existing `GenerateAsync` for backward compat):
```csharp
public async Task<CraftingPlanResult> GenerateStructuredAsync(
    int targetItemId, int quantity, AccountSnapshot snapshot,
    CancellationToken ct, IProgress<PlanStatus> progress = null)
```

Steps:
1. Build recipe tree (unchanged)
2. Collect item IDs, fetch TP prices (unchanged)
3. Resolve vendor offers (unchanged)
4. **NEW:** If `snapshot != null` and `_reducer != null`:
   - Build owned-item pool: `SnapshotHelpers.AggregateItems(snapshot.Items)` → `Dictionary<int, int>` of (ItemId → total count)
   - Call `_reducer.Reduce(tree, pool)` → `ReducedTreeResult` (reduced tree + used materials)
5. Solve (on reduced tree if available, original tree otherwise)
6. Fetch item metadata (unchanged)
7. **NEW:** If `_accountRecipeClient != null && _accountRecipeClient.HasRequiredPermission()`:
   - Fetch learned recipe IDs
8. **NEW:** Call `PlanResultBuilder.Build(plan, treeUsedForSolve, metadata, usedMaterials, learnedIds)` — where `treeUsedForSolve` is the reduced tree (if snapshot applied) or the original tree

### Update `manifest.json`
Add optional `unlocks` permission:
```json
"unlocks": { "optional": true, "details": "Used to check which recipes your account has learned" }
```

### Acceptance Criteria
- `GenerateStructuredAsync(null snapshot)` produces same plan as `GenerateAsync` (no reduction)
- With snapshot: plan reflects reduced quantities, UsedMaterials populated
- **Key parity invariant:** when inventory fully covers a craftable intermediate, that item has no Craft step in the plan, and its recipe/discipline are NOT in RequiredRecipes/RequiredDisciplines
- RequiredDisciplines aggregated to highest MinRating per discipline
- RequiredRecipes: AutoLearned flag correct from Flags, IsMissing from learned set, null when no permission
- DebugLog populated with key decisions
- Vendor currency costs still appear correctly in CurrencyCosts

### Tests to Add

**`PlanResultBuilderTests.cs` (NEW):**
- `RequiredDisciplines_FromCraftSteps_HighestRatingWins`
- `RequiredDisciplines_ExcludesNonCraftSteps` — BuyFromTp step's discipline not included
- `RequiredRecipes_AutoLearnedFlag` — recipe with AutoLearned flag, IsAutoLearned == true
- `RequiredRecipes_MissingFlag_WithLearnedSet` — recipe not learned, IsMissing == true
- `RequiredRecipes_LearnedFlag_WithLearnedSet` — recipe learned, IsMissing == false
- `RequiredRecipes_NullLearnedSet_MissingIsNull` — no unlocks permission, IsMissing == null
- `RequiredRecipes_DeduplicatedByRecipeId` — same recipe in multiple steps → single entry
- `UsedMaterials_PassedThrough` — used materials from reducer appear in result

**`CraftingPlanPipelineTests.cs` (additions):**
- `GenerateStructuredAsync_NullSnapshot_SameAsOriginal`
- `GenerateStructuredAsync_WithSnapshot_ReducesTree` — verify step counts differ
- `GenerateStructuredAsync_OwnedIntermediate_RemovesCraftStep_And_Discipline` — **integration test**: own an intermediate craftable, verify its Craft step is gone AND its discipline/recipe are absent from RequiredDisciplines/RequiredRecipes

**`InMemoryAccountRecipeClient` (NEW test helper):**
- `AddLearnedRecipe(int)`, `SetHasPermission(bool)` control methods

### Manual Test Checklist (Orrax Manifested, post-M7)

After M7 is complete, validate with a temporary test harness or debugger against a real recipe tree:

- [ ] **Use Own Mats OFF:** Generate plan for a multi-level recipe. Verify all intermediate crafts appear in Steps, and RequiredDisciplines/RequiredRecipes include all crafted items' disciplines/recipes.
- [ ] **Use Own Mats ON (no owned items):** Same result as OFF.
- [ ] **Use Own Mats ON (own leaf ingredient):** Step count decreases for that ingredient. UsedMaterials reports consumption. Other steps unchanged.
- [ ] **Use Own Mats ON (own craftable intermediate):** That item's Craft step disappears from Steps. Its discipline disappears from RequiredDisciplines. Its recipe disappears from RequiredRecipes. Its ingredient buy/craft steps also disappear.
- [ ] **Vendor currency costs:** An item with vendor source shows correct CurrencyCost entries (non-coin currencies).
- [ ] **DebugLog:** Contains readable summary of reduction + decisions.

---

## Milestone 8: UI Views (DEFERRED)

> Deferred until Phase B logic (M5-M7) is validated with Orrax Manifested.

**Goal:** Replace the hardcoded CraftingPlanView with structured sub-views, "Use Own Materials" toggle, and item/quantity input.

Scope (when implemented):
- CraftingPlanView rewrite with item ID + quantity input, "Use Own Materials" checkbox
- Sections: Used Materials, Shopping List, Required Disciplines, Required Recipes, Crafting Steps
- Recipe Tree annotated view (may defer further to polish)
- `Gw2AccountRecipeClient` wired into Module.cs
- Manual test only (no Blish HUD in test project)

---

## Implementation Order

```
M5 (Recipe Metadata) ──┐
                        ├──> M7 (Pipeline + Structured Result)
M6 (Inventory Reducer) ┘

M8 (UI Views) — deferred until M7 validated
```

M5 and M6 are independent. M6's clone helper should copy M5's new fields on RecipeOption. M7 depends on both.

---

## New Files Summary

| Milestone | File | Type |
|-----------|------|------|
| M6 | `Models/UsedMaterial.cs` | Model |
| M6 | `Models/ReducedTreeResult.cs` | Model |
| M6 | `Services/InventoryReducer.cs` | Service |
| M6 | `tests/.../Services/InventoryReducerTests.cs` | Test |
| M7 | `Models/RequiredDiscipline.cs` | Model |
| M7 | `Models/RequiredRecipe.cs` | Model |
| M7 | `Services/PlanResultBuilder.cs` | Service |
| M7 | `Services/IAccountRecipeClient.cs` | Interface |
| M7 | `tests/.../Services/PlanResultBuilderTests.cs` | Test |
| M7 | `tests/.../Helpers/InMemoryAccountRecipeClient.cs` | Test helper |

## Modified Files Summary

| Milestone | File | Change |
|-----------|------|--------|
| M5 | `Services/IRecipeApiClient.cs` | Add Disciplines, MinRating, Flags to RawRecipe |
| M5 | `Services/Gw2RecipeApiClient.cs` | Parse new fields |
| M5 | `Models/RecipeOption.cs` | Add Disciplines, MinRating, Flags |
| M5 | `Services/RecipeService.cs` | Copy new fields to RecipeOption |
| M7 | `Models/CraftingPlanResult.cs` | Add UsedMaterials, RequiredDisciplines, RequiredRecipes, DebugLog |
| M7 | `Services/CraftingPlanPipeline.cs` | Add GenerateStructuredAsync, wire reducer + builder |
| M7 | `manifest.json` | Add unlocks permission (optional) |
| All | `GW2CraftingHelper.csproj` | Add Compile Include for new files |

## Verification
After each milestone:
- `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` — 0 errors
- `dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` — all pass
- After M7: manual Orrax Manifested checklist above
