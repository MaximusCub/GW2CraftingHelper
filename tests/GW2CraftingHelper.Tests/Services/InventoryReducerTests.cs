using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class InventoryReducerTests
    {
        private readonly InventoryReducer _reducer = new InventoryReducer();

        // Leaf comes from Helpers/RecipeNodeBuilders.cs.

        /// <summary>
        /// Helper: build a craftable node with one recipe option. Kept
        /// local (not folded into RecipeNodeBuilders.Craftable) because
        /// this one has a genuinely different shape - it takes a
        /// recipeId/outputCount pair instead of pre-built RecipeOptions,
        /// auto-computes CraftsNeeded from qty/outputCount, and bakes in
        /// Disciplines/MinRating/Flags that the shared builder leaves
        /// empty (M38 WP-01, tests T2).
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

        // --- M37 (KNOWN-ISSUES #26): CloneNode must preserve the new fields ---

        [Fact]
        public void CloneNode_PreservesAchievementFieldsAndDedupFlag()
        {
            // Same bug CLASS as the M33 Finding 2 fix for
            // RecipeOption.ExpectedOutputCount (see CloneOption's own doc
            // comment): any RecipeNode field not explicitly copied here is
            // silently dropped (C# default) on every Reduce() clone.
            // AchievementBitDedupPrePass runs on the pre-reduction tree, so
            // its IsAchievementBitDeduped/AchievementId/AchievementBit
            // fields MUST survive onto the reduced tree PlanSolver and
            // CraftingTreeBuilder actually consume.
            var dedupedIngredient = Leaf(55, 0);
            dedupedIngredient.AchievementId = 8493;
            dedupedIngredient.AchievementBit = 0;
            dedupedIngredient.IsAchievementBitDeduped = true;

            var tree = Craftable(1, 5, 10, 1, dedupedIngredient);
            var pool = new Dictionary<int, int>();

            var result = _reducer.Reduce(tree, pool);

            var clonedIngredient = result.ReducedTree.Recipes[0].Ingredients[0];
            Assert.Equal(8493, clonedIngredient.AchievementId);
            Assert.Equal(0, clonedIngredient.AchievementBit);
            Assert.True(clonedIngredient.IsAchievementBitDeduped);
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
            // Own 2 of item 1 - should clear recipes, NOT consume item 2
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

        // --- Mystic Clover-style EV recipe tests (M33 fix-pass Critical
        // finding 2: CloneOption previously dropped ExpectedOutputCount,
        // and the crafts-needed recompute previously always used the
        // nominal OutputCount instead of ExpectedOutputCount - either bug
        // alone silently disables EV pricing whenever an account snapshot
        // triggers this reduction path, the normal own-materials mode for
        // a real plan) ---

        [Fact]
        public void Reduce_ExpectedOutputCountPreservedAcrossClone()
        {
            // Before the fix, CloneOption dropped ExpectedOutputCount,
            // silently zeroing it (C# default) on every reduced tree.
            var option = new RecipeOption
            {
                RecipeId = -1591,
                OutputCount = 1,
                ExpectedOutputCount = 0.31,
                CraftsNeeded = 249,
                Ingredients = new List<RecipeNode> { Leaf(2, 249) }
            };
            var tree = new RecipeNode
            {
                Id = 19675,
                IngredientType = "Item",
                Quantity = 77,
                Recipes = new List<RecipeOption> { option }
            };
            var pool = new Dictionary<int, int>(); // nothing owned

            var result = _reducer.Reduce(tree, pool);

            Assert.Equal(0.31, result.ReducedTree.Recipes[0].ExpectedOutputCount);
        }

        [Fact]
        public void Reduce_EVRecipe_PartialOwnership_RecalcsCraftsNeeded_UsingExpectedOutputCount()
        {
            // Simulates a RecipeService-built EV tree: need 10 successes at
            // ExpectedOutputCount=0.5 (nominal OutputCount=1) -> 20 forge
            // attempts, ingredient (1 per attempt) scaled to 20.
            var option = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                ExpectedOutputCount = 0.5,
                CraftsNeeded = 20,
                Ingredients = new List<RecipeNode> { Leaf(2, 20) }
            };
            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 10,
                Recipes = new List<RecipeOption> { option }
            };

            // Own 4 of item 1 -> qty becomes 6. Recomputing crafts needed
            // from ExpectedOutputCount (0.5): ceil(6/0.5) = 12. Using the
            // (buggy) nominal OutputCount (1) instead would give 6.
            var pool = new Dictionary<int, int> { { 1, 4 } };

            var result = _reducer.Reduce(tree, pool);

            Assert.Equal(6, result.ReducedTree.Quantity);
            var reducedOption = result.ReducedTree.Recipes[0];
            Assert.Equal(12, reducedOption.CraftsNeeded);
            // perCraft = 20 (orig ingredient qty) / 20 (orig crafts) = 1;
            // 1 * 12 (new crafts) = 12.
            Assert.Equal(12, reducedOption.Ingredients[0].Quantity);
        }

        [Fact]
        public void Sourced_EVRecipe_PartialOwnership_RecalcsCraftsNeeded_UsingExpectedOutputCount()
        {
            // Same scenario as above through the sourced (AccountItemIndex)
            // overload - the actual code path a real own-materials plan
            // with a live account snapshot exercises.
            var option = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                ExpectedOutputCount = 0.5,
                CraftsNeeded = 20,
                Ingredients = new List<RecipeNode> { Leaf(2, 20) }
            };
            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 10,
                Recipes = new List<RecipeOption> { option }
            };

            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(1, 4, AccountItemIndex.SourceMaterialStorage)
            });

            var result = _reducer.Reduce(tree, index, null);

            Assert.Equal(6, result.ReducedTree.Quantity);
            var reducedOption = result.ReducedTree.Recipes[0];
            Assert.Equal(0.5, reducedOption.ExpectedOutputCount);
            Assert.Equal(12, reducedOption.CraftsNeeded);
            Assert.Equal(12, reducedOption.Ingredients[0].Quantity);
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

            // Pool has currency id 99 - should not be consumed
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
            // Pool has 10 of item 2 - both branches consume, aggregated to single entry
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
            // Own 3 of item 1 - fully owned craftable intermediate
            var tree = Craftable(1, 3, 10, 1, Leaf(2, 9));
            var pool = new Dictionary<int, int> { { 1, 3 } };

            var result = _reducer.Reduce(tree, pool);

            Assert.Equal(0, result.ReducedTree.Quantity);
            Assert.Empty(result.ReducedTree.Recipes);
            Assert.True(result.ReducedTree.IsLeaf);
        }

        // ---- M34-B2a #2: multi-recipe-option pool consumption fix ----
        // (m1 Finding 5 / m34-r2-gw2e-owned-materials.md Section 6.2.5:
        // previously EVERY RecipeOption on a node drained the shared pool,
        // not just the one the solver would eventually choose - untested
        // before this milestone, since every fixture above uses a single
        // recipe option.)

        [Fact]
        public void MultipleRecipeOptions_OnlyPrimaryOptionConsumesPool()
        {
            // Root item 1 (qty 1) has TWO recipe options, each needing 5 of
            // item 2. Pool has exactly 5 of item 2 - just enough for ONE
            // option. Only the primary (first-listed) option may consume
            // it; the alternate option's ingredient must be left untouched,
            // not silently double-spent against the same pool.
            var optionA = new RecipeOption { RecipeId = 10, OutputCount = 1, CraftsNeeded = 1 };
            optionA.Ingredients.Add(Leaf(2, 5));
            var optionB = new RecipeOption { RecipeId = 20, OutputCount = 1, CraftsNeeded = 1 };
            optionB.Ingredients.Add(Leaf(2, 5));

            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 1,
                Recipes = new List<RecipeOption> { optionA, optionB }
            };

            var pool = new Dictionary<int, int> { { 2, 5 } };
            var result = _reducer.Reduce(tree, pool);

            var reducedOptionA = result.ReducedTree.Recipes[0];
            var reducedOptionB = result.ReducedTree.Recipes[1];

            // Primary option's ingredient fully covered by the pool
            Assert.Equal(0, reducedOptionA.Ingredients[0].Quantity);
            // Alternate option's ingredient untouched - still needs all 5
            Assert.Equal(5, reducedOptionB.Ingredients[0].Quantity);

            // Only the primary option's consumption is recorded (not
            // double-counted against both options)
            var totalUsed = result.UsedMaterials.Where(u => u.ItemId == 2).Sum(u => u.QuantityUsed);
            Assert.Equal(5, totalUsed);
        }

        [Fact]
        public void Sourced_MultipleRecipeOptions_OnlyPrimaryOptionConsumesPool()
        {
            var optionA = new RecipeOption { RecipeId = 10, OutputCount = 1, CraftsNeeded = 1 };
            optionA.Ingredients.Add(Leaf(2, 5));
            var optionB = new RecipeOption { RecipeId = 20, OutputCount = 1, CraftsNeeded = 1 };
            optionB.Ingredients.Add(Leaf(2, 5));

            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 1,
                Recipes = new List<RecipeOption> { optionA, optionB }
            };

            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(2, 5, AccountItemIndex.SourceMaterialStorage)
            });

            var result = _reducer.Reduce(tree, index, null);

            var reducedOptionA = result.ReducedTree.Recipes[0];
            var reducedOptionB = result.ReducedTree.Recipes[1];

            Assert.Equal(0, reducedOptionA.Ingredients[0].Quantity);
            Assert.Equal(5, reducedOptionB.Ingredients[0].Quantity);

            var totalUsed = result.UsedMaterials.Where(u => u.ItemId == 2).Sum(u => u.QuantityUsed);
            Assert.Equal(5, totalUsed);
        }

        [Fact]
        public void MultipleRecipeOptions_BothOptionsGetCraftsNeededRescaled()
        {
            // Even though only the primary option consumes the pool, BOTH
            // options' CraftsNeeded/ingredient Quantity must still be
            // rescaled to the node's own (self-)reduced Quantity, so
            // PlanSolver's cost comparison across options stays consistent
            // (M33 Finding 1 parity - every option is always evaluated).
            var optionA = new RecipeOption { RecipeId = 10, OutputCount = 2, CraftsNeeded = 5 };
            optionA.Ingredients.Add(Leaf(2, 10)); // perCraft = 10/5 = 2
            var optionB = new RecipeOption { RecipeId = 20, OutputCount = 2, CraftsNeeded = 5 };
            optionB.Ingredients.Add(Leaf(3, 10)); // perCraft = 10/5 = 2

            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 10,
                Recipes = new List<RecipeOption> { optionA, optionB }
            };

            // Own 4 of item 1 itself -> Quantity becomes 6, newCrafts = ceil(6/2) = 3
            var pool = new Dictionary<int, int> { { 1, 4 } };
            var result = _reducer.Reduce(tree, pool);

            Assert.Equal(6, result.ReducedTree.Quantity);
            Assert.Equal(3, result.ReducedTree.Recipes[0].CraftsNeeded);
            Assert.Equal(3, result.ReducedTree.Recipes[1].CraftsNeeded);
            // perCraft(2) * newCrafts(3) = 6 for BOTH options' ingredients
            Assert.Equal(6, result.ReducedTree.Recipes[0].Ingredients[0].Quantity);
            Assert.Equal(6, result.ReducedTree.Recipes[1].Ingredients[0].Quantity);
        }

        // ---- M34-B2a #1: per-node owned-quantity attribution ----

        [Fact]
        public void OwnedQuantityUsedByNode_RecordsConsumptionKeyedByNodeObject()
        {
            var leaf = Leaf(100, 5);
            var pool = new Dictionary<int, int> { { 100, 3 } };

            var result = _reducer.Reduce(leaf, pool);

            Assert.Single(result.OwnedQuantityUsedByNode);
            var entry = result.OwnedQuantityUsedByNode.Single();
            Assert.Same(result.ReducedTree, entry.Key);
            Assert.Equal(3, entry.Value);
        }

        [Fact]
        public void OwnedQuantityUsedByNode_PerNodeNotAggregatedByItemId()
        {
            // Two DISTINCT node objects for the same item id (2), each
            // partially covered from the same pool - the per-node map must
            // keep them separate (unlike UsedMaterials, which aggregates by
            // item id), so a future per-node display can tell them apart.
            var ing1 = Leaf(2, 3);
            var ing2 = Leaf(2, 4);
            var option = new RecipeOption { RecipeId = 10, OutputCount = 1, CraftsNeeded = 1 };
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

            Assert.Equal(2, result.OwnedQuantityUsedByNode.Count);
            var reducedIng1 = result.ReducedTree.Recipes[0].Ingredients[0];
            var reducedIng2 = result.ReducedTree.Recipes[0].Ingredients[1];
            Assert.Equal(3, result.OwnedQuantityUsedByNode[reducedIng1]);
            Assert.Equal(2, result.OwnedQuantityUsedByNode[reducedIng2]);
        }

        [Fact]
        public void Sourced_OwnedQuantityUsedByNode_RecordsConsumptionKeyedByNodeObject()
        {
            var leaf = Leaf(100, 8);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 5, AccountItemIndex.SourceMaterialStorage),
                SnapEntry(100, 3, AccountItemIndex.SourceBank)
            });

            var result = _reducer.Reduce(leaf, index, null);

            Assert.Single(result.OwnedQuantityUsedByNode);
            var entry = result.OwnedQuantityUsedByNode.Single();
            Assert.Same(result.ReducedTree, entry.Key);
            Assert.Equal(8, entry.Value); // 5 + 3, across both sources
        }

        [Fact]
        public void OwnedQuantityUsedByNode_EmptyWhenNothingConsumed()
        {
            var tree = Craftable(1, 5, 10, 1, Leaf(2, 5));
            var pool = new Dictionary<int, int>();

            var result = _reducer.Reduce(tree, pool);

            Assert.Empty(result.OwnedQuantityUsedByNode);
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
                SnapEntry(100, 5, AccountItemIndex.SourceMaterialStorage)
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
                SnapEntry(100, 5, AccountItemIndex.SourceMaterialStorage),
                SnapEntry(100, 3, AccountItemIndex.SourceBank)
            });

            var result = _reducer.Reduce(tree, index, null);

            Assert.Equal(0, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);

            var sources = result.UsedMaterials[0].Sources;
            Assert.NotNull(sources);
            Assert.Equal(2, sources.Count);

            var matSource = sources.First(s => s.Source == AccountItemIndex.SourceMaterialStorage);
            var bankSource = sources.First(s => s.Source == AccountItemIndex.SourceBank);
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
                SnapEntry(100, 3, AccountItemIndex.SourceBank),
                SnapEntry(100, 3, AccountItemIndex.SourceMaterialStorage)
            });

            var result = _reducer.Reduce(tree, index, null);

            Assert.Equal(0, result.ReducedTree.Quantity);
            var sources = result.UsedMaterials[0].Sources;
            Assert.Single(sources);
            Assert.Equal(AccountItemIndex.SourceMaterialStorage, sources[0].Source);
            Assert.Equal(3, sources[0].Quantity);
        }

        [Fact]
        public void Sourced_ActiveCharConsumedBeforeBank()
        {
            var tree = Leaf(100, 5);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 3, AccountItemIndex.SourceBank),
                SnapEntry(100, 5, AccountItemIndex.CharacterSourcePrefix + "Alice")
            });

            var result = _reducer.Reduce(tree, index, "Alice");

            Assert.Equal(0, result.ReducedTree.Quantity);
            var sources = result.UsedMaterials[0].Sources;
            Assert.Single(sources);
            Assert.Equal(AccountItemIndex.CharacterSourcePrefix + "Alice", sources[0].Source);
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
            // Legacy contract: old overload leaves Sources null
            Assert.Null(result.UsedMaterials[0].Sources);
        }

        [Fact]
        public void Sourced_PartialReduction_OnlyConsumesAvailable()
        {
            var tree = Leaf(100, 10);
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 3, AccountItemIndex.SourceMaterialStorage),
                SnapEntry(100, 4, AccountItemIndex.SourceBank)
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
                SnapEntry(2, 2, AccountItemIndex.SourceMaterialStorage),
                SnapEntry(2, 1, AccountItemIndex.SourceBank)
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
            Assert.Equal(2, sources.First(s => s.Source == AccountItemIndex.SourceMaterialStorage).Quantity);
            Assert.Equal(1, sources.First(s => s.Source == AccountItemIndex.SourceBank).Quantity);
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
                SnapEntry(2, 7, AccountItemIndex.SourceMaterialStorage)
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
                SnapEntry(100, 5, AccountItemIndex.SourceMaterialStorage)
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
                SnapEntry(100, 6, AccountItemIndex.CharacterSourcePrefix + "Zephyr"),
                SnapEntry(100, 2, AccountItemIndex.SourceBank),
                SnapEntry(100, 3, AccountItemIndex.SourceSharedInventory),
                SnapEntry(100, 5, AccountItemIndex.SourceMaterialStorage),
                SnapEntry(100, 4, AccountItemIndex.CharacterSourcePrefix + "ActiveHero")
            });

            var result = _reducer.Reduce(tree, index, "ActiveHero");

            Assert.Equal(0, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(20, result.UsedMaterials[0].QuantityUsed);

            var sources = result.UsedMaterials[0].Sources;
            Assert.Equal(5, sources.Count);

            // Verify each source contributed the right amount
            Assert.Equal(5, sources.First(s => s.Source == AccountItemIndex.SourceMaterialStorage).Quantity);
            Assert.Equal(4, sources.First(s => s.Source == AccountItemIndex.CharacterSourcePrefix + "ActiveHero").Quantity);
            Assert.Equal(3, sources.First(s => s.Source == AccountItemIndex.SourceSharedInventory).Quantity);
            Assert.Equal(2, sources.First(s => s.Source == AccountItemIndex.SourceBank).Quantity);
            Assert.Equal(6, sources.First(s => s.Source == AccountItemIndex.CharacterSourcePrefix + "Zephyr").Quantity);
        }

        [Fact]
        public void Sourced_NullSnapshot_TreeNotReduced()
        {
            // Simulates useOwn=false: when no index is available,
            // Reduce still works with an empty index - nothing consumed.
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
                SnapEntry(100, 3, AccountItemIndex.SourceBank)
            });

            var result = _reducer.Reduce(tree, index, null);

            // Only Bank's 3 should be consumed (null/empty excluded from index)
            Assert.Equal(2, result.ReducedTree.Quantity);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(3, result.UsedMaterials[0].QuantityUsed);
            Assert.Single(result.UsedMaterials[0].Sources);
            Assert.Equal(AccountItemIndex.SourceBank, result.UsedMaterials[0].Sources[0].Source);
        }

        [Fact]
        public void Sourced_WhenConsumed_SourcesIsNonNullList()
        {
            // Sourced overload: when items are consumed, Sources is a non-null list
            var tree = Leaf(100, 5);
            var emptyIndex = new AccountItemIndex(null);

            var result = _reducer.Reduce(tree, emptyIndex, null);

            // Nothing consumed, so UsedMaterials is empty
            Assert.Empty(result.UsedMaterials);

            // When items ARE consumed, Sources is a non-null list
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(100, 5, AccountItemIndex.SourceBank)
            });
            var result2 = _reducer.Reduce(Leaf(100, 5), index, null);

            Assert.Single(result2.UsedMaterials);
            Assert.NotNull(result2.UsedMaterials[0].Sources);
            Assert.Single(result2.UsedMaterials[0].Sources);
        }

        [Fact]
        public void Sourced_OldOverload_SourcesIsNull()
        {
            // Legacy contract: old overload has no source concept, Sources stays null
            var tree = Leaf(100, 5);
            var pool = new Dictionary<int, int> { { 100, 3 } };

            var result = _reducer.Reduce(tree, pool);

            Assert.Single(result.UsedMaterials);
            Assert.Null(result.UsedMaterials[0].Sources);
        }

        [Fact]
        public void Sourced_AggregatedSources_DeterministicOrdering()
        {
            // Two branches consuming from Zephyr and Bank.
            // Aggregated Sources must be ordered alphabetically by source name.
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

            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(2, 4, AccountItemIndex.CharacterSourcePrefix + "Zephyr"),
                SnapEntry(2, 4, AccountItemIndex.SourceBank)
            });

            var result = _reducer.Reduce(tree, index, null);

            var sources = result.UsedMaterials
                .First(u => u.ItemId == 2).Sources;

            Assert.Equal(2, sources.Count);
            // Ordinal: "Bank" < "Character:Zephyr"
            Assert.Equal(AccountItemIndex.SourceBank, sources[0].Source);
            Assert.Equal(AccountItemIndex.CharacterSourcePrefix + "Zephyr", sources[1].Source);
        }

        [Fact]
        public void Sourced_CurrencyNodeWithItemSiblings_OnlyItemsReduced()
        {
            // Item 1 -> recipe 10 -> [item 2 (qty 3), currency 99 (qty 50)]
            // Sourced overload: item 2 is reduced, currency 99 is untouched
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

            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(2, 10, AccountItemIndex.SourceMaterialStorage)
            });

            var result = _reducer.Reduce(tree, index, null);

            // Item 2 fully consumed
            var reducedItem = result.ReducedTree.Recipes[0].Ingredients[0];
            Assert.Equal(0, reducedItem.Quantity);

            // Currency unchanged
            var currencyNode = result.ReducedTree.Recipes[0].Ingredients[1];
            Assert.Equal(50, currencyNode.Quantity);
            Assert.Equal("Currency", currencyNode.IngredientType);

            // Only item 2 in used materials
            Assert.Single(result.UsedMaterials);
            Assert.Equal(2, result.UsedMaterials[0].ItemId);
            Assert.NotNull(result.UsedMaterials[0].Sources);
        }

        [Fact]
        public void OldVsNew_SameQuantityResults_WhenSourcesProvided()
        {
            // Regression: sourced overload must produce the same tree quantities
            // as old overload when total owned quantities match.
            // Item 1 (qty 10) -> recipe 10 (output 2) -> leaf item 2 (qty 25)
            // Own 4 of item 1 from mixed sources.
            var tree = Craftable(1, 10, 10, 2, Leaf(2, 25));

            // Old overload: flat pool
            var pool = new Dictionary<int, int> { { 1, 4 } };
            var oldResult = _reducer.Reduce(tree, pool);

            // New overload: same total from two sources
            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(1, 2, AccountItemIndex.SourceMaterialStorage),
                SnapEntry(1, 2, AccountItemIndex.SourceBank)
            });
            var newResult = _reducer.Reduce(tree, index, null);

            // Tree quantities must match exactly
            Assert.Equal(oldResult.ReducedTree.Quantity, newResult.ReducedTree.Quantity);
            Assert.Equal(
                oldResult.ReducedTree.Recipes[0].CraftsNeeded,
                newResult.ReducedTree.Recipes[0].CraftsNeeded);
            Assert.Equal(
                oldResult.ReducedTree.Recipes[0].Ingredients[0].Quantity,
                newResult.ReducedTree.Recipes[0].Ingredients[0].Quantity);

            // Total consumed quantity matches
            Assert.Equal(
                oldResult.UsedMaterials.Sum(u => u.QuantityUsed),
                newResult.UsedMaterials.Sum(u => u.QuantityUsed));

            // New overload has Sources; old does not
            Assert.Null(oldResult.UsedMaterials[0].Sources);
            Assert.NotNull(newResult.UsedMaterials[0].Sources);

            // Original tree not mutated by either call
            Assert.Equal(10, tree.Quantity);
        }

        // ---- VOM design (Candidate A): decision-guided pool consumption ----
        // (design-value-own-materials.md Section 6 test plan - the guide
        // dictionary a throwaway zero-owned PlanSolver.Solve produces,
        // consumed via Reduce's new optional 4th argument.)

        [Fact]
        public void MultipleRecipeOptions_DecisionGuided_NonPrimaryOptionConsumesPoolWhenChosen()
        {
            // Same shape as MultipleRecipeOptions_OnlyPrimaryOptionConsumesPool
            // (root item 1, two recipe options each needing 5 of item 2,
            // pool has exactly 5), but this time the guide says the solver
            // actually chose option B (RecipeId 20), NOT the primary
            // (first-listed) option A - direct converse proving the fix
            // generalizes past the old i==0 heuristic.
            var optionA = new RecipeOption { RecipeId = 10, OutputCount = 1, CraftsNeeded = 1 };
            optionA.Ingredients.Add(Leaf(2, 5));
            var optionB = new RecipeOption { RecipeId = 20, OutputCount = 1, CraftsNeeded = 1 };
            optionB.Ingredients.Add(Leaf(2, 5));

            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 1,
                NodeId = 1,
                Recipes = new List<RecipeOption> { optionA, optionB }
            };

            var guide = new Dictionary<int, SolverDecision>
            {
                { 1, new SolverDecision { Source = AcquisitionSource.Craft, RecipeId = 20 } }
            };

            var pool = new Dictionary<int, int> { { 2, 5 } };
            var result = _reducer.Reduce(tree, pool, guide);

            var reducedOptionA = result.ReducedTree.Recipes[0];
            var reducedOptionB = result.ReducedTree.Recipes[1];

            // Chosen (non-primary) option's ingredient fully covered by the pool
            Assert.Equal(5, reducedOptionA.Ingredients[0].Quantity); // untouched
            Assert.Equal(0, reducedOptionB.Ingredients[0].Quantity); // discounted

            var totalUsed = result.UsedMaterials.Where(u => u.ItemId == 2).Sum(u => u.QuantityUsed);
            Assert.Equal(5, totalUsed);
        }

        [Fact]
        public void NodeDecidedBuy_IngredientsNeverConsumed_NoPhantomUsedMaterials()
        {
            // Node has a recipe (CanCraft would be true), but the guide's
            // decision for it is BuyFromTp - no option may consume the
            // pool for its descendants, and the ingredient must not appear
            // in UsedMaterials at all (the audited row-31 "phantom
            // UsedMaterials" bug this design fixes).
            var tree = Craftable(1, 1, 10, 1, Leaf(2, 5));
            tree.NodeId = 1;

            var guide = new Dictionary<int, SolverDecision>
            {
                { 1, new SolverDecision { Source = AcquisitionSource.BuyFromTp } }
            };

            var pool = new Dictionary<int, int> { { 2, 5 } };
            var result = _reducer.Reduce(tree, pool, guide);

            Assert.Equal(5, result.ReducedTree.Recipes[0].Ingredients[0].Quantity);
            Assert.DoesNotContain(result.UsedMaterials, u => u.ItemId == 2);
            Assert.Empty(result.UsedMaterials);
        }

        [Fact]
        public void NodeDecidedBuy_OwnStockOfNodeItself_StillCreditedAgainstThatNode()
        {
            // The node ITSELF is owned and its guided decision is Buy - its
            // own Quantity must still be discounted (consumeFromPool is
            // inherited from the caller/parent, unchanged by the guide),
            // even though its ingredients (below) never consume anything.
            var tree = Craftable(1, 5, 10, 1, Leaf(2, 5));
            tree.NodeId = 1;

            var guide = new Dictionary<int, SolverDecision>
            {
                { 1, new SolverDecision { Source = AcquisitionSource.BuyFromTp } }
            };

            var pool = new Dictionary<int, int> { { 1, 3 }, { 2, 5 } };
            var result = _reducer.Reduce(tree, pool, guide);

            // Node's own quantity discounted from 5 to 2 (3 owned units used).
            Assert.Equal(2, result.ReducedTree.Quantity);
            Assert.Contains(result.UsedMaterials, u => u.ItemId == 1 && u.QuantityUsed == 3);

            // The ingredient's own Quantity still rescales to match the
            // node's new (already-reduced) demand - unconditional, per
            // ReduceNode's doc comment (M33 Finding 1) - so 2, not the
            // original 5. What the guide actually gates is pool
            // CONSUMPTION: the ingredient's own 5 owned units in the pool
            // are never touched, since the node was decided Buy.
            Assert.Equal(2, result.ReducedTree.Recipes[0].Ingredients[0].Quantity);
            Assert.DoesNotContain(result.UsedMaterials, u => u.ItemId == 2);
        }

        [Fact]
        public void MissingNodeInGuide_FallsBackToPrimaryHeuristic()
        {
            // Guide is non-null but does not contain this node's NodeId
            // (defensive fallback) - must reproduce the exact legacy i==0
            // primary-option heuristic, same as MultipleRecipeOptions_
            // OnlyPrimaryOptionConsumesPool with a null guide.
            var optionA = new RecipeOption { RecipeId = 10, OutputCount = 1, CraftsNeeded = 1 };
            optionA.Ingredients.Add(Leaf(2, 5));
            var optionB = new RecipeOption { RecipeId = 20, OutputCount = 1, CraftsNeeded = 1 };
            optionB.Ingredients.Add(Leaf(2, 5));

            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 1,
                NodeId = 1,
                Recipes = new List<RecipeOption> { optionA, optionB }
            };

            // Guide references a totally different NodeId (99) - this
            // node's own NodeId (1) is absent from it.
            var guide = new Dictionary<int, SolverDecision>
            {
                { 99, new SolverDecision { Source = AcquisitionSource.Craft, RecipeId = 20 } }
            };

            var pool = new Dictionary<int, int> { { 2, 5 } };
            var result = _reducer.Reduce(tree, pool, guide);

            var reducedOptionA = result.ReducedTree.Recipes[0];
            var reducedOptionB = result.ReducedTree.Recipes[1];

            // Primary option's ingredient fully covered by the pool (legacy
            // heuristic), alternate option untouched.
            Assert.Equal(0, reducedOptionA.Ingredients[0].Quantity);
            Assert.Equal(5, reducedOptionB.Ingredients[0].Quantity);
        }

        // ---- Sourced_ mirrors (AccountItemIndex overload - the actual
        // production code path) ----

        [Fact]
        public void Sourced_MultipleRecipeOptions_DecisionGuided_NonPrimaryOptionConsumesPoolWhenChosen()
        {
            var optionA = new RecipeOption { RecipeId = 10, OutputCount = 1, CraftsNeeded = 1 };
            optionA.Ingredients.Add(Leaf(2, 5));
            var optionB = new RecipeOption { RecipeId = 20, OutputCount = 1, CraftsNeeded = 1 };
            optionB.Ingredients.Add(Leaf(2, 5));

            var tree = new RecipeNode
            {
                Id = 1,
                IngredientType = "Item",
                Quantity = 1,
                NodeId = 1,
                Recipes = new List<RecipeOption> { optionA, optionB }
            };

            var guide = new Dictionary<int, SolverDecision>
            {
                { 1, new SolverDecision { Source = AcquisitionSource.Craft, RecipeId = 20 } }
            };

            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(2, 5, AccountItemIndex.SourceMaterialStorage)
            });

            var result = _reducer.Reduce(tree, index, null, guide);

            var reducedOptionA = result.ReducedTree.Recipes[0];
            var reducedOptionB = result.ReducedTree.Recipes[1];

            Assert.Equal(5, reducedOptionA.Ingredients[0].Quantity);
            Assert.Equal(0, reducedOptionB.Ingredients[0].Quantity);

            var totalUsed = result.UsedMaterials.Where(u => u.ItemId == 2).Sum(u => u.QuantityUsed);
            Assert.Equal(5, totalUsed);
        }

        [Fact]
        public void Sourced_NodeDecidedBuy_IngredientsNeverConsumed_NoPhantomUsedMaterials()
        {
            var tree = Craftable(1, 1, 10, 1, Leaf(2, 5));
            tree.NodeId = 1;

            var guide = new Dictionary<int, SolverDecision>
            {
                { 1, new SolverDecision { Source = AcquisitionSource.BuyFromTp } }
            };

            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(2, 5, AccountItemIndex.SourceMaterialStorage)
            });

            var result = _reducer.Reduce(tree, index, null, guide);

            Assert.Equal(5, result.ReducedTree.Recipes[0].Ingredients[0].Quantity);
            Assert.DoesNotContain(result.UsedMaterials, u => u.ItemId == 2);
            Assert.Empty(result.UsedMaterials);
        }

        [Fact]
        public void Sourced_NodeDecidedBuy_OwnStockOfNodeItself_StillCreditedAgainstThatNode()
        {
            var tree = Craftable(1, 5, 10, 1, Leaf(2, 5));
            tree.NodeId = 1;

            var guide = new Dictionary<int, SolverDecision>
            {
                { 1, new SolverDecision { Source = AcquisitionSource.BuyFromTp } }
            };

            var index = new AccountItemIndex(new List<SnapshotItemEntry>
            {
                SnapEntry(1, 3, AccountItemIndex.SourceMaterialStorage),
                SnapEntry(2, 5, AccountItemIndex.SourceMaterialStorage)
            });

            var result = _reducer.Reduce(tree, index, null, guide);

            Assert.Equal(2, result.ReducedTree.Quantity);
            Assert.Contains(result.UsedMaterials, u => u.ItemId == 1 && u.QuantityUsed == 3);

            // See the non-sourced mirror's matching comment - the
            // ingredient's Quantity rescales to 2 unconditionally; only its
            // pool consumption (5 owned units, untouched) is guide-gated.
            Assert.Equal(2, result.ReducedTree.Recipes[0].Ingredients[0].Quantity);
            Assert.DoesNotContain(result.UsedMaterials, u => u.ItemId == 2);
        }
    }
}
