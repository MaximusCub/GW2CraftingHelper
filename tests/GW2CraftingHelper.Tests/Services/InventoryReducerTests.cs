using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class InventoryReducerTests
    {
        private readonly InventoryReducer _reducer = new InventoryReducer();

        /// <summary>
        /// Helper: build a leaf node (no recipes).
        /// </summary>
        private static RecipeNode Leaf(int id, int qty, string type = "Item")
        {
            return new RecipeNode
            {
                Id = id,
                IngredientType = type,
                Quantity = qty
            };
        }

        /// <summary>
        /// Helper: build a craftable node with one recipe option.
        /// </summary>
        private static RecipeNode Craftable(
            int id, int qty, int recipeId, int outputCount,
            params RecipeNode[] ingredients)
        {
            int craftsNeeded = (int)Math.Ceiling((double)qty / outputCount);
            var option = new RecipeOption
            {
                RecipeId = recipeId,
                OutputCount = outputCount,
                CraftsNeeded = craftsNeeded,
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            };

            // Adjust ingredient quantities to match craftsNeeded
            foreach (var ing in ingredients)
            {
                option.Ingredients.Add(ing);
            }

            return new RecipeNode
            {
                Id = id,
                IngredientType = "Item",
                Quantity = qty,
                Recipes = new List<RecipeOption> { option }
            };
        }

        [Fact]
        public void EmptyPool_TreeUnchanged()
        {
            // Item 1 (qty 5) -> recipe 10 -> leaf item 2 (qty 5)
            var tree = Craftable(1, 5, 10, 1, Leaf(2, 5));
            var pool = new Dictionary<int, int>();

            var result = _reducer.Reduce(tree, pool);

            Assert.Equal(5, result.ReducedTree.Quantity);
            Assert.Single(result.ReducedTree.Recipes);
            Assert.Equal(5, result.ReducedTree.Recipes[0].CraftsNeeded);
            Assert.Equal(5, result.ReducedTree.Recipes[0].Ingredients[0].Quantity);
            Assert.Empty(result.UsedMaterials);
        }

        [Fact]
        public void OriginalTreeNotMutated()
        {
            var tree = Craftable(1, 5, 10, 1, Leaf(2, 5));
            var pool = new Dictionary<int, int> { { 1, 3 } };

            _reducer.Reduce(tree, pool);

            // Original tree must be unchanged
            Assert.Equal(5, tree.Quantity);
            Assert.Single(tree.Recipes);
            Assert.Equal(5, tree.Recipes[0].CraftsNeeded);
            Assert.Equal(5, tree.Recipes[0].Ingredients[0].Quantity);
        }

        [Fact]
        public void LeafFullyOwned_QuantityZero()
        {
            var tree = Leaf(100, 5);
            var pool = new Dictionary<int, int> { { 100, 5 } };

            var result = _reducer.Reduce(tree, pool);

            Assert.Equal(0, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(100, result.UsedMaterials[0].ItemId);
            Assert.Equal(5, result.UsedMaterials[0].QuantityUsed);
        }

        [Fact]
        public void LeafPartiallyOwned_ReducedQuantity()
        {
            var tree = Leaf(100, 5);
            var pool = new Dictionary<int, int> { { 100, 3 } };

            var result = _reducer.Reduce(tree, pool);

            Assert.Equal(2, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(3, result.UsedMaterials[0].QuantityUsed);
        }

        [Fact]
        public void CraftableFullyOwned_RecipesCleared_IngredientsNotConsumed()
        {
            // Item 1 (qty 2) -> recipe 10 -> leaf item 2 (qty 6)
            // Own 2 of item 1 — should clear recipes, NOT consume item 2
            var tree = Craftable(1, 2, 10, 1, Leaf(2, 6));
            var pool = new Dictionary<int, int> { { 1, 2 }, { 2, 100 } };

            var result = _reducer.Reduce(tree, pool);

            Assert.Equal(0, result.ReducedTree.Quantity);
            Assert.Empty(result.ReducedTree.Recipes);

            // Only item 1 consumed, not item 2
            Assert.Single(result.UsedMaterials);
            Assert.Equal(1, result.UsedMaterials[0].ItemId);
            Assert.Equal(2, result.UsedMaterials[0].QuantityUsed);

            // Pool for item 2 should still be 100
            Assert.Equal(100, pool[2]);
        }

        [Fact]
        public void PartialOwnership_RecalcsCraftsNeeded_And_IngredientQuantities()
        {
            // Item 1 (qty 10) -> recipe 10 (output 2) -> leaf item 2 (qty 25)
            // craftsNeeded = ceil(10/2) = 5, so ingredient qty = 25 (perCraft = 5)
            // Own 4 of item 1 → qty becomes 6, newCrafts = ceil(6/2) = 3
            // ingredient qty = 5 * 3 = 15
            var tree = Craftable(1, 10, 10, 2, Leaf(2, 25));
            var pool = new Dictionary<int, int> { { 1, 4 } };

            var result = _reducer.Reduce(tree, pool);

            Assert.Equal(6, result.ReducedTree.Quantity);
            var option = result.ReducedTree.Recipes[0];
            Assert.Equal(3, option.CraftsNeeded);
            Assert.Equal(15, option.Ingredients[0].Quantity);
        }

        [Fact]
        public void SharedItemAcrossBranches_PoolConsumedDepthFirst()
        {
            // Root item 1 -> recipe 10 -> [item 2 (qty 3), item 2 (qty 4)]
            // Two ingredients both referencing item 2; pool has 5 of item 2
            // Depth-first: first branch gets min(5,3)=3, second gets min(2,4)=2
            var ing1 = Leaf(2, 3);
            var ing2 = Leaf(2, 4);
            var option = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                CraftsNeeded = 1
            };
            option.Ingredients.Add(ing1);
            option.Ingredients.Add(ing2);
            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 1,
                Recipes = new List<RecipeOption> { option }
            };

            var pool = new Dictionary<int, int> { { 2, 5 } };
            var result = _reducer.Reduce(tree, pool);

            var reducedIng1 = result.ReducedTree.Recipes[0].Ingredients[0];
            var reducedIng2 = result.ReducedTree.Recipes[0].Ingredients[1];

            // First ingredient fully covered: 3-3=0
            Assert.Equal(0, reducedIng1.Quantity);
            // Second ingredient partially covered: 4-2=2
            Assert.Equal(2, reducedIng2.Quantity);

            // Total used: 3+2=5
            var totalUsed = result.UsedMaterials
                .Where(u => u.ItemId == 2)
                .Sum(u => u.QuantityUsed);
            Assert.Equal(5, totalUsed);
        }

        [Fact]
        public void CurrencyNodes_NeverConsumed()
        {
            // Item 1 -> recipe 10 -> [leaf item 2 (qty 3), currency 99 (qty 50)]
            var option = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                CraftsNeeded = 1
            };
            option.Ingredients.Add(Leaf(2, 3));
            option.Ingredients.Add(Leaf(99, 50, "Currency"));
            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 1,
                Recipes = new List<RecipeOption> { option }
            };

            // Pool has currency id 99 — should not be consumed
            var pool = new Dictionary<int, int> { { 99, 999 } };

            var result = _reducer.Reduce(tree, pool);

            var currencyNode = result.ReducedTree.Recipes[0].Ingredients[1];
            Assert.Equal(50, currencyNode.Quantity);
            Assert.Equal("Currency", currencyNode.IngredientType);
            Assert.Empty(result.UsedMaterials);
            Assert.Equal(999, pool[99]);
        }

        [Fact]
        public void UsedMaterials_Aggregated()
        {
            // Root item 1 -> recipe 10 -> [item 2 (qty 3), item 2 (qty 4)]
            // Pool has 10 of item 2 — both branches consume, aggregated to single entry
            var option = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                CraftsNeeded = 1
            };
            option.Ingredients.Add(Leaf(2, 3));
            option.Ingredients.Add(Leaf(2, 4));
            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 1,
                Recipes = new List<RecipeOption> { option }
            };

            var pool = new Dictionary<int, int> { { 2, 10 } };
            var result = _reducer.Reduce(tree, pool);

            // Both branches fully covered, aggregated into one entry
            var item2Used = result.UsedMaterials.Where(u => u.ItemId == 2).ToList();
            Assert.Single(item2Used);
            Assert.Equal(7, item2Used[0].QuantityUsed); // 3 + 4
        }

        [Fact]
        public void MultiLevelTree_EndToEnd()
        {
            // Root (item 1, qty 4)
            //   -> recipe 10, outputCount=2, craftsNeeded=2
            //     -> item 2 (qty 6, perCraft=3)
            //       -> recipe 20, outputCount=1, craftsNeeded=6
            //         -> item 3 (qty 12, perCraft=2)
            //     -> item 4 (qty 4, perCraft=2)
            //
            // Pool: item 1=1, item 3=5
            // After reduction:
            //   item 1: qty=4-1=3, newCrafts=ceil(3/2)=2 (unchanged)
            //   item 2: qty=3*2=6 (unchanged), crafts=6
            //   item 3: qty=12-5=7
            //   item 4: qty=2*2=4 (unchanged)
            var leaf3 = Leaf(3, 12);
            var item2 = Craftable(2, 6, 20, 1, leaf3);
            var leaf4 = Leaf(4, 4);
            var root = Craftable(1, 4, 10, 2, item2, leaf4);

            var pool = new Dictionary<int, int> { { 1, 1 }, { 3, 5 } };
            var result = _reducer.Reduce(root, pool);

            // Root: 4-1=3, newCrafts=ceil(3/2)=2
            Assert.Equal(3, result.ReducedTree.Quantity);
            var rootOption = result.ReducedTree.Recipes[0];
            Assert.Equal(2, rootOption.CraftsNeeded);

            // Item 2: perCraft=3, qty=3*2=6 (unchanged)
            var reducedItem2 = rootOption.Ingredients[0];
            Assert.Equal(6, reducedItem2.Quantity);

            // Item 3: 12-5=7
            var reducedItem3 = reducedItem2.Recipes[0].Ingredients[0];
            Assert.Equal(7, reducedItem3.Quantity);

            // Item 4: perCraft=2, qty=2*2=4 (unchanged)
            var reducedItem4 = rootOption.Ingredients[1];
            Assert.Equal(4, reducedItem4.Quantity);

            // Used materials: item 1 (1), item 3 (5)
            Assert.Equal(2, result.UsedMaterials.Count);
            Assert.Contains(result.UsedMaterials, u => u.ItemId == 1 && u.QuantityUsed == 1);
            Assert.Contains(result.UsedMaterials, u => u.ItemId == 3 && u.QuantityUsed == 5);
        }

        [Fact]
        public void PartialReduction_NonDivisibleQuantity_RoundsUpPerCraft()
        {
            // Root (id=1, qty=1) -> recipe 100 (output 1, crafts 1)
            //   -> Intermediate (id=500, qty=9) -> recipe 200 (output 3, crafts 3)
            //     -> Ingredient (id=600, qty=7)
            //
            // Own 3 of intermediate (id=500):
            //   500 qty: 9-3=6, newCrafts=ceil(6/3)=2
            //   600 perCraft: ceil(7/3)=3, qty=3*2=6
            //
            // Bug (floor division): perCraft=7/3=2, qty=2*2=4 (wrong!)
            var leaf = Leaf(600, 7);
            var intermediate = Craftable(500, 9, 200, 3, leaf);
            var root = Craftable(1, 1, 100, 1, intermediate);

            var pool = new Dictionary<int, int> { { 500, 3 } };
            var result = _reducer.Reduce(root, pool);

            var reducedIntermediate = result.ReducedTree.Recipes[0].Ingredients[0];
            Assert.Equal(6, reducedIntermediate.Quantity);
            Assert.Equal(2, reducedIntermediate.Recipes[0].CraftsNeeded);

            var reducedIngredient = reducedIntermediate.Recipes[0].Ingredients[0];
            Assert.Equal(6, reducedIngredient.Quantity); // ceil(7/3)*2 = 3*2 = 6
        }

        [Fact]
        public void FullyOwnedIntermediate_NoRecipesOnNode()
        {
            // Item 1 (qty 3) -> recipe 10 -> leaf item 2 (qty 9)
            // Own 3 of item 1 — fully owned craftable intermediate
            var tree = Craftable(1, 3, 10, 1, Leaf(2, 9));
            var pool = new Dictionary<int, int> { { 1, 3 } };

            var result = _reducer.Reduce(tree, pool);

            Assert.Equal(0, result.ReducedTree.Quantity);
            Assert.Empty(result.ReducedTree.Recipes);
            Assert.True(result.ReducedTree.IsLeaf);
        }

        // ---- Source-aware overload tests ----

        private static SnapshotItemEntry SnapEntry(int itemId, int count, string source)
        {
            return new SnapshotItemEntry
            {
                ItemId = itemId,
                Count = count,
                Source = source
            };
        }

        [Fact]
        public void Sourced_BasicReduction_ReducesCorrectly()
        {
            var tree = Leaf(100, 5);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 5, "MaterialStorage")
            });

            var result = _reducer.Reduce(tree, index, null);

            Assert.Equal(0, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(100, result.UsedMaterials[0].ItemId);
            Assert.Equal(5, result.UsedMaterials[0].QuantityUsed);
        }

        [Fact]
        public void Sourced_SourcesPopulated_InUsedMaterial()
        {
            var tree = Leaf(100, 8);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 5, "MaterialStorage"),
                SnapEntry(100, 3, "Bank")
            });

            var result = _reducer.Reduce(tree, index, null);

            Assert.Equal(0, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);

            var sources = result.UsedMaterials[0].Sources;
            Assert.NotNull(sources);
            Assert.Equal(2, sources.Count);

            var matSource = sources.First(s => s.Source == "MaterialStorage");
            var bankSource = sources.First(s => s.Source == "Bank");
            Assert.Equal(5, matSource.Quantity);
            Assert.Equal(3, bankSource.Quantity);
        }

        [Fact]
        public void Sourced_PriorityOrder_MaterialStorageFirst()
        {
            // Tree needs 3 of item 100. Both MaterialStorage and Bank have 3.
            // MaterialStorage should be consumed first.
            var tree = Leaf(100, 3);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 3, "Bank"),
                SnapEntry(100, 3, "MaterialStorage")
            });

            var result = _reducer.Reduce(tree, index, null);

            Assert.Equal(0, result.ReducedTree.Quantity);
            var sources = result.UsedMaterials[0].Sources;
            Assert.Single(sources);
            Assert.Equal("MaterialStorage", sources[0].Source);
            Assert.Equal(3, sources[0].Quantity);
        }

        [Fact]
        public void Sourced_ActiveCharConsumedBeforeBank()
        {
            var tree = Leaf(100, 5);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 3, "Bank"),
                SnapEntry(100, 5, "Alice")
            });

            var result = _reducer.Reduce(tree, index, "Alice");

            Assert.Equal(0, result.ReducedTree.Quantity);
            var sources = result.UsedMaterials[0].Sources;
            Assert.Single(sources);
            Assert.Equal("Alice", sources[0].Source);
            Assert.Equal(5, sources[0].Quantity);
        }

        [Fact]
        public void Sourced_ItemNotInIndex_NotReduced()
        {
            var tree = Leaf(100, 5);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>());

            var result = _reducer.Reduce(tree, index, null);

            Assert.Equal(5, result.ReducedTree.Quantity);
            Assert.Empty(result.UsedMaterials);
        }

        [Fact]
        public void Sourced_OldOverload_StillWorksUnchanged()
        {
            var tree = Leaf(100, 5);
            var pool = new Dictionary<int, int> { { 100, 3 } };

            var result = _reducer.Reduce(tree, pool);

            Assert.Equal(2, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(3, result.UsedMaterials[0].QuantityUsed);
            // Old overload does not populate Sources
            Assert.Null(result.UsedMaterials[0].Sources);
        }

        [Fact]
        public void Sourced_PartialReduction_OnlyConsumesAvailable()
        {
            var tree = Leaf(100, 10);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 3, "MaterialStorage"),
                SnapEntry(100, 4, "Bank")
            });

            var result = _reducer.Reduce(tree, index, null);

            Assert.Equal(3, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(7, result.UsedMaterials[0].QuantityUsed);

            var sources = result.UsedMaterials[0].Sources;
            Assert.Equal(2, sources.Count);
        }

        [Fact]
        public void Sourced_MultiLevel_SourceTrackingAcrossTree()
        {
            // Root (id=1, qty=1) -> recipe 10 -> leaf (id=2, qty=3)
            // Own 2 of item 2 from MaterialStorage, 1 from Bank
            var tree = Craftable(1, 1, 10, 1, Leaf(2, 3));
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(2, 2, "MaterialStorage"),
                SnapEntry(2, 1, "Bank")
            });

            var result = _reducer.Reduce(tree, index, null);

            // Root is still qty=1, needs crafting
            Assert.Equal(1, result.ReducedTree.Quantity);
            // Ingredient fully consumed
            var ingredient = result.ReducedTree.Recipes[0].Ingredients[0];
            Assert.Equal(0, ingredient.Quantity);

            Assert.Single(result.UsedMaterials);
            Assert.Equal(2, result.UsedMaterials[0].ItemId);
            Assert.Equal(3, result.UsedMaterials[0].QuantityUsed);

            var sources = result.UsedMaterials[0].Sources;
            Assert.Equal(2, sources.Count);
            Assert.Equal(2, sources.First(s => s.Source == "MaterialStorage").Quantity);
            Assert.Equal(1, sources.First(s => s.Source == "Bank").Quantity);
        }

        [Fact]
        public void Sourced_PoolNeverGoesNegative()
        {
            // Two branches both need item 2: first needs 5, second needs 5.
            // Only 7 available from MaterialStorage.
            // After first branch: pool=2. Second branch: consume min(2,5)=2, pool=0.
            // Pool must never go negative.
            var ing1 = Leaf(2, 5);
            var ing2 = Leaf(2, 5);
            var option = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                CraftsNeeded = 1
            };
            option.Ingredients.Add(ing1);
            option.Ingredients.Add(ing2);
            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 1,
                Recipes = new List<RecipeOption> { option }
            };

            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(2, 7, "MaterialStorage")
            });

            var result = _reducer.Reduce(tree, index, null);

            var reducedIng1 = result.ReducedTree.Recipes[0].Ingredients[0];
            var reducedIng2 = result.ReducedTree.Recipes[0].Ingredients[1];

            Assert.Equal(0, reducedIng1.Quantity);  // 5-5=0
            Assert.Equal(3, reducedIng2.Quantity);   // 5-2=3

            var totalUsed = result.UsedMaterials
                .Where(u => u.ItemId == 2)
                .Sum(u => u.QuantityUsed);
            Assert.Equal(7, totalUsed);
        }

        [Fact]
        public void Sourced_ExactConsumption_PoolReachesZeroNotNegative()
        {
            // Exactly enough items: need 5, have 5. Pool should reach exactly 0.
            var tree = Leaf(100, 5);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 5, "MaterialStorage")
            });

            var result = _reducer.Reduce(tree, index, null);

            Assert.Equal(0, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(5, result.UsedMaterials[0].QuantityUsed);
        }

        [Fact]
        public void Sourced_ComprehensivePriority_FullChainWithPoolVerification()
        {
            // Item 100, need 20.
            // Sources: MaterialStorage=5, ActiveChar=4, SharedInventory=3, Bank=2, OtherChar=6
            // Priority: MaterialStorage(5) -> ActiveChar(4) -> SharedInventory(3) -> Bank(2) -> OtherChar(6)
            // Total available = 20, exactly enough. All consumed in priority order.
            var tree = Leaf(100, 20);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 6, "Zephyr"),
                SnapEntry(100, 2, "Bank"),
                SnapEntry(100, 3, "SharedInventory"),
                SnapEntry(100, 5, "MaterialStorage"),
                SnapEntry(100, 4, "ActiveHero")
            });

            var result = _reducer.Reduce(tree, index, "ActiveHero");

            Assert.Equal(0, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(20, result.UsedMaterials[0].QuantityUsed);

            var sources = result.UsedMaterials[0].Sources;
            Assert.Equal(5, sources.Count);

            // Verify each source contributed the right amount
            Assert.Equal(5, sources.First(s => s.Source == "MaterialStorage").Quantity);
            Assert.Equal(4, sources.First(s => s.Source == "ActiveHero").Quantity);
            Assert.Equal(3, sources.First(s => s.Source == "SharedInventory").Quantity);
            Assert.Equal(2, sources.First(s => s.Source == "Bank").Quantity);
            Assert.Equal(6, sources.First(s => s.Source == "Zephyr").Quantity);
        }

        [Fact]
        public void Sourced_NullSnapshot_TreeNotReduced()
        {
            // Simulates useOwn=false: when no index is available,
            // Reduce still works with an empty index — nothing consumed.
            var tree = Craftable(1, 5, 10, 1, Leaf(2, 5));
            var emptyIndex = new AccountItemIndex(null);

            var result = _reducer.Reduce(tree, emptyIndex, null);

            Assert.Equal(5, result.ReducedTree.Quantity);
            Assert.Single(result.ReducedTree.Recipes);
            Assert.Equal(5, result.ReducedTree.Recipes[0].Ingredients[0].Quantity);
            Assert.Empty(result.UsedMaterials);
        }

        [Fact]
        public void Sourced_NullSourceEntries_Excluded()
        {
            // Items with null/empty sources should not affect reduction
            var tree = Leaf(100, 5);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 10, null),
                SnapEntry(100, 10, ""),
                SnapEntry(100, 3, "Bank")
            });

            var result = _reducer.Reduce(tree, index, null);

            // Only Bank's 3 should be consumed (null/empty excluded from index)
            Assert.Equal(2, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(3, result.UsedMaterials[0].QuantityUsed);
            Assert.Single(result.UsedMaterials[0].Sources);
            Assert.Equal("Bank", result.UsedMaterials[0].Sources[0].Source);
        }
    }
}
