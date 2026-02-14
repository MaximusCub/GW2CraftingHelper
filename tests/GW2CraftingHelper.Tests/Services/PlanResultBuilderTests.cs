using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanResultBuilderTests
    {
        private readonly PlanResultBuilder _builder = new PlanResultBuilder();

        /// <summary>
        /// Helper: build a minimal tree with one recipe option on the root.
        /// </summary>
        private static RecipeNode TreeWithCraftStep(
            int itemId, int recipeId, int outputCount,
            List<string> disciplines, int minRating, List<string> flags,
            params RecipeNode[] ingredients)
        {
            var option = new RecipeOption
            {
                RecipeId = recipeId,
                OutputCount = outputCount,
                CraftsNeeded = 1,
                Disciplines = disciplines ?? new List<string>(),
                MinRating = minRating,
                Flags = flags ?? new List<string>()
            };

            foreach (var ing in ingredients)
            {
                option.Ingredients.Add(ing);
            }

            return new RecipeNode
            {
                Id = itemId,
                IngredientType = "Item",
                Quantity = 1,
                Recipes = new List<RecipeOption> { option }
            };
        }

        private static RecipeNode Leaf(int id, int qty)
        {
            return new RecipeNode
            {
                Id = id,
                IngredientType = "Item",
                Quantity = qty
            };
        }

        [Fact]
        public void RequiredDisciplines_FromCraftSteps_HighestRatingWins()
        {
            // Two craft steps for the same discipline with different ratings
            var leaf1 = Leaf(2, 1);
            var leaf2 = Leaf(3, 1);
            var innerNode = TreeWithCraftStep(
                3, 20, 1,
                new List<string> { "Weaponsmith" }, 400, new List<string> { "AutoLearned" },
                Leaf(4, 1));

            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Weaponsmith" }, 500, new List<string> { "AutoLearned" },
                leaf1, innerNode);

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                    new PlanStep { ItemId = 3, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 20 }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            Assert.Single(result.RequiredDisciplines);
            Assert.Equal("Weaponsmith", result.RequiredDisciplines[0].Discipline);
            Assert.Equal(500, result.RequiredDisciplines[0].MinRating);
        }

        [Fact]
        public void RequiredDisciplines_ExcludesNonCraftSteps()
        {
            // BuyFromTp step for item 2 — its discipline should NOT appear
            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Armorsmith" }, 300, new List<string>(),
                Leaf(2, 1));

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                    new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.BuyFromTp }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            // Only Armorsmith from the Craft step
            Assert.Single(result.RequiredDisciplines);
            Assert.Equal("Armorsmith", result.RequiredDisciplines[0].Discipline);
        }

        [Fact]
        public void RequiredRecipes_AutoLearnedFlag()
        {
            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Weaponsmith" }, 400,
                new List<string> { "AutoLearned" },
                Leaf(2, 1));

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            Assert.Single(result.RequiredRecipes);
            Assert.True(result.RequiredRecipes[0].IsAutoLearned);
        }

        [Fact]
        public void RequiredRecipes_MissingFlag_WithLearnedSet()
        {
            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Weaponsmith" }, 400, new List<string>(),
                Leaf(2, 1));

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 }
                }
            };

            // Learned set does NOT contain recipe 10
            var learnedIds = new HashSet<int> { 99 };
            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, learnedIds);

            Assert.Single(result.RequiredRecipes);
            Assert.True(result.RequiredRecipes[0].IsMissing);
        }

        [Fact]
        public void RequiredRecipes_LearnedFlag_WithLearnedSet()
        {
            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Weaponsmith" }, 400, new List<string>(),
                Leaf(2, 1));

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 }
                }
            };

            // Learned set CONTAINS recipe 10
            var learnedIds = new HashSet<int> { 10 };
            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, learnedIds);

            Assert.Single(result.RequiredRecipes);
            Assert.False(result.RequiredRecipes[0].IsMissing);
        }

        [Fact]
        public void RequiredRecipes_NullLearnedSet_MissingIsNull()
        {
            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Weaponsmith" }, 400, new List<string>(),
                Leaf(2, 1));

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            Assert.Single(result.RequiredRecipes);
            Assert.Null(result.RequiredRecipes[0].IsMissing);
        }

        [Fact]
        public void RequiredRecipes_DeduplicatedByRecipeId()
        {
            // Two steps reference the same recipe ID
            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Weaponsmith" }, 400, new List<string>(),
                Leaf(2, 1));

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            Assert.Single(result.RequiredRecipes);
            Assert.Equal(10, result.RequiredRecipes[0].RecipeId);
        }

        [Fact]
        public void UsedMaterials_PassedThrough()
        {
            var tree = Leaf(1, 5);

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 5,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 2, Source = AcquisitionSource.BuyFromTp }
                }
            };

            var usedMaterials = new List<UsedMaterial>
            {
                new UsedMaterial { ItemId = 1, QuantityUsed = 3 }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, usedMaterials, null);

            Assert.Single(result.UsedMaterials);
            Assert.Equal(1, result.UsedMaterials[0].ItemId);
            Assert.Equal(3, result.UsedMaterials[0].QuantityUsed);
        }
    }
}
