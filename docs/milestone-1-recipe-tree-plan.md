# Milestone 1: Recipe Tree -- Implementation Plan

> **Status**: Approved. This is the reference document for the next implementation step.

---

## Context

Implement the recipe tree subsystem (S2 from the approved plan). Given a target item ID, recursively resolve its recipe into a dependency tree using the official GW2 API. Pure data + logic -- no UI, no pricing, no account data.

**Key discovery**: Orrax Manifested (item 104857) is a Mystic Forge recipe -- NOT in `/v2/recipes` (search returns `[]`). Standard crafting recipes (e.g., Mithril Ingot 19685, recipe 21) ARE in the API. The tree system must handle both cases: items with API recipes get resolved; items without (including Mystic Forge items) become leaf nodes.

## Data Model

### `Models/RecipeNode.cs`
```
RecipeNode
  int Id                          -- item or currency ID
  string IngredientType           -- "Item" or "Currency"
  int Quantity                    -- how many needed (propagated from parent)
  List<RecipeOption> Recipes      -- 0 = leaf, 1+ = craftable
  bool IsLeaf => Recipes.Count == 0
```

### `Models/RecipeOption.cs`
```
RecipeOption
  int RecipeId
  int OutputCount                 -- items produced per craft
  int CraftsNeeded                -- ceil(parent.Quantity / OutputCount)
  List<RecipeNode> Ingredients    -- sub-nodes with propagated quantities
```

Quantity propagation: `ingredient.Quantity = CraftsNeeded * ingredient.CountPerCraft`

## API Abstraction

### `Services/IRecipeApiClient.cs`
Interface + DTOs (RawRecipe, RawIngredient). Blish-free. Real implementation deferred to Module wiring.

```
IRecipeApiClient
  Task<IReadOnlyList<int>> SearchByOutputAsync(int itemId, CancellationToken ct)
  Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)

RawRecipe { Id, OutputItemId, OutputItemCount, List<RawIngredient> Ingredients }
RawIngredient { Type ("Item"/"Currency"), Id, Count }
```

## Service

### `Services/RecipeService.cs`
- Constructor takes `IRecipeApiClient`
- `BuildTreeAsync(int itemId, int quantity, CancellationToken ct)` -> `RecipeNode`
- Recursive: for each item, search for recipes, resolve ingredients recursively
- Currency-type ingredients are always leaves (no recipe search)
- Cycle detection via `HashSet<int> visiting` (DFS ancestor set; removed after processing so same item can appear in different branches)
- Response caching: `Dictionary<int, ...>` for search results and recipe lookups to avoid redundant API calls

## Files to Create/Modify

| File | Action |
|---|---|
| `Models/RecipeNode.cs` | **NEW** |
| `Models/RecipeOption.cs` | **NEW** |
| `Services/IRecipeApiClient.cs` | **NEW** (interface + RawRecipe + RawIngredient) |
| `Services/RecipeService.cs` | **NEW** |
| `GW2CraftingHelper.csproj` | Add 4 `<Compile Include>` entries |
| `tests/.../Helpers/InMemoryRecipeApiClient.cs` | **NEW** (test double) |
| `tests/.../Services/RecipeServiceTests.cs` | **NEW** |

## Test Scenarios

Using `InMemoryRecipeApiClient` (in-memory test double implementing `IRecipeApiClient`):

1. **Leaf node**: item with no recipe -> IsLeaf, Quantity preserved
2. **Single-level recipe**: item A -> recipe with ingredients B, C (both leaves)
3. **Multi-level chain**: A -> B -> C (leaf) -- 3 levels deep
4. **Quantity propagation**: need 3, recipe makes 2 -> 2 crafts -> ingredient counts doubled
5. **Multiple recipes**: item has 2 recipes -> both in node.Recipes
6. **Currency ingredient**: recipe with Currency-type ingredient -> leaf node
7. **Output count > 1**: recipe makes 5, need 7 -> 2 crafts needed

## Verification
- `dotnet build GW2CraftingHelper.csproj -p:Platform=x64`
- `dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`
