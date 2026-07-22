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
            // BuyFromTp step for item 2 - its discipline should NOT appear
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
        public void RequiredDisciplines_MultiDisciplineRecipe_SelectsExactlyOne()
        {
            // Recipe craftable by four disciplines.
            // Algorithm must select exactly one from the recipe's allowed set.
            var allowed = new HashSet<string> { "Weaponsmith", "Armorsmith", "Huntsman", "Artificer" };
            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string>(allowed),
                500, new List<string> { "AutoLearned" },
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

            Assert.Single(result.RequiredDisciplines);
            Assert.Contains(result.RequiredDisciplines[0].Discipline, allowed);
            Assert.Equal(500, result.RequiredDisciplines[0].MinRating);
        }

        [Fact]
        public void RequiredDisciplines_MultiDisciplineRecipe_PrefersAlreadySelected()
        {
            // Inner recipe is single-discipline Weaponsmith (must-use).
            // Outer recipe is multi-discipline including Weaponsmith - should reuse it.
            var innerNode = TreeWithCraftStep(
                3, 20, 1,
                new List<string> { "Weaponsmith" }, 400, new List<string> { "AutoLearned" },
                Leaf(4, 1));

            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Armorsmith", "Weaponsmith" }, 500, new List<string> { "AutoLearned" },
                Leaf(2, 1), innerNode);

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

            // Only Weaponsmith needed - reused for the multi-discipline recipe
            Assert.Single(result.RequiredDisciplines);
            Assert.Equal("Weaponsmith", result.RequiredDisciplines[0].Discipline);
            Assert.Equal(500, result.RequiredDisciplines[0].MinRating);
        }

        [Fact]
        public void RequiredDisciplines_TwoMultiDisciplineRecipes_NoOverlap_SelectsTwoDisciplines()
        {
            // Two multi-discipline recipes with disjoint discipline sets.
            // Must select one discipline from each recipe's set.
            var setA = new HashSet<string> { "Armorsmith", "Weaponsmith" };
            var setB = new HashSet<string> { "Leatherworker", "Tailor" };
            var innerNode = TreeWithCraftStep(
                3, 20, 1,
                new List<string>(setB), 300, new List<string>(),
                Leaf(4, 1));

            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string>(setA), 500, new List<string>(),
                Leaf(2, 1), innerNode);

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

            Assert.Equal(2, result.RequiredDisciplines.Count);
            var names = result.RequiredDisciplines.Select(d => d.Discipline).ToList();
            Assert.Single(names, n => setA.Contains(n));
            Assert.Single(names, n => setB.Contains(n));
        }

        [Fact]
        public void RequiredDisciplines_OverlappingMultiDiscipline_GreedyCoverSelectsShared()
        {
            // Recipe A: {Armorsmith, Weaponsmith}, Recipe B: {Weaponsmith, Leatherworker}.
            // Weaponsmith appears in both - greedy cover picks it alone.
            var innerNode = TreeWithCraftStep(
                3, 20, 1,
                new List<string> { "Weaponsmith", "Leatherworker" }, 400, new List<string>(),
                Leaf(4, 1));

            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Armorsmith", "Weaponsmith" }, 500, new List<string>(),
                Leaf(2, 1), innerNode);

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

            // Greedy cover: Weaponsmith covers both, max rating = 500
            Assert.Single(result.RequiredDisciplines);
            Assert.Equal("Weaponsmith", result.RequiredDisciplines[0].Discipline);
            Assert.Equal(500, result.RequiredDisciplines[0].MinRating);
        }

        [Fact]
        public void RequiredDisciplines_MultiDiscipline_PreCoveredByPass1_MaxRating()
        {
            // Step 1: single-discipline Weaponsmith at 300 (Pass 1 must-use).
            // Step 2: multi-discipline {Weaponsmith, Armorsmith, Huntsman} at 500.
            // Pre-cover should recognize Step 2 is already covered by Weaponsmith
            // and bump Weaponsmith's MinRating to 500 without adding new disciplines.
            var innerNode = TreeWithCraftStep(
                3, 20, 1,
                new List<string> { "Weaponsmith", "Armorsmith", "Huntsman" }, 500,
                new List<string> { "AutoLearned" },
                Leaf(4, 1));

            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Weaponsmith" }, 300,
                new List<string> { "AutoLearned" },
                Leaf(2, 1), innerNode);

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

            // Only Weaponsmith selected; max rating is 500 from the multi-discipline recipe
            Assert.Single(result.RequiredDisciplines);
            Assert.Equal("Weaponsmith", result.RequiredDisciplines[0].Discipline);
            Assert.Equal(500, result.RequiredDisciplines[0].MinRating);
        }

        [Fact]
        public void RequiredDisciplines_PreCover_MultiplePass1Overlap_OnlyHighestRatedBumped()
        {
            // Pass 1 selects Weaponsmith (400) and Armorsmith (300).
            // Multi-disc recipe {Weaponsmith, Armorsmith} at 500 is coverable by either.
            // Pre-cover should pick Weaponsmith (highest existing rating) and bump only
            // Weaponsmith to 500. Armorsmith must stay at 300.
            var node3 = TreeWithCraftStep(
                5, 30, 1,
                new List<string> { "Weaponsmith", "Armorsmith" }, 500, new List<string>(),
                Leaf(6, 1));

            var node2 = TreeWithCraftStep(
                3, 20, 1,
                new List<string> { "Armorsmith" }, 300, new List<string>(),
                Leaf(4, 1));

            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Weaponsmith" }, 400, new List<string>(),
                Leaf(2, 1), node2, node3);

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                    new PlanStep { ItemId = 3, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 20 },
                    new PlanStep { ItemId = 5, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 30 }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            Assert.Equal(2, result.RequiredDisciplines.Count);
            var ws = result.RequiredDisciplines.First(d => d.Discipline == "Weaponsmith");
            var arm = result.RequiredDisciplines.First(d => d.Discipline == "Armorsmith");
            Assert.Equal(500, ws.MinRating);
            Assert.Equal(300, arm.MinRating);
        }

        [Fact]
        public void RequiredDisciplines_ExcludesBuyFromVendorSteps()
        {
            // Craft step for item 1, BuyFromVendor step for item 3
            var innerNode = TreeWithCraftStep(
                3, 20, 1,
                new List<string> { "Leatherworker" }, 400, new List<string>(),
                Leaf(4, 1));

            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Weaponsmith" }, 500, new List<string>(),
                Leaf(2, 1), innerNode);

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                    new PlanStep { ItemId = 3, Quantity = 1, Source = AcquisitionSource.BuyFromVendor }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            // Only Weaponsmith from the Craft step; Leatherworker from BuyFromVendor excluded
            Assert.Single(result.RequiredDisciplines);
            Assert.Equal("Weaponsmith", result.RequiredDisciplines[0].Discipline);
        }

        [Fact]
        public void RequiredDisciplines_ExcludesCurrencySteps()
        {
            var tree = TreeWithCraftStep(
                1, 10, 1,
                new List<string> { "Weaponsmith" }, 500, new List<string>(),
                Leaf(2, 1));

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                    new PlanStep { ItemId = 5, Quantity = 10, Source = AcquisitionSource.Currency }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            Assert.Single(result.RequiredDisciplines);
            Assert.Equal("Weaponsmith", result.RequiredDisciplines[0].Discipline);
        }

        [Fact]
        public void RequiredDisciplines_MysticForgeNoDisciplines_EmptyList()
        {
            // Mystic Forge recipe (negative ID) with no disciplines
            var tree = TreeWithCraftStep(
                1, -100, 1,
                new List<string>(), 0, new List<string>(),
                Leaf(2, 1), Leaf(3, 1), Leaf(4, 1), Leaf(5, 1));

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = -100 }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            Assert.Empty(result.RequiredDisciplines);
        }

        [Fact]
        public void RequiredDisciplines_RealMysticForgeDiscipline_StillShown()
        {
            // Regression guard for the adversarial-review fix-pass: real
            // production Mystic Forge recipes always carry
            // Disciplines = ["MysticForge"] (MysticForgeRecipeData.Load
            // sets this unconditionally), unlike the empty-Disciplines test
            // fixture above. Confirms the fix-pass's narrower
            // NonCraftingDisciplines filter (Achievement/Merchant only)
            // leaves this pre-existing, out-of-scope behavior unchanged.
            var tree = TreeWithCraftStep(
                1, -100, 1,
                new List<string> { "MysticForge" }, 0, new List<string>(),
                Leaf(2, 1));

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = -100 }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            Assert.Single(result.RequiredDisciplines);
            Assert.Equal("MysticForge", result.RequiredDisciplines[0].Discipline);
            Assert.Single(result.RequiredRecipes);
            Assert.False(result.RequiredRecipes[0].IsMissing);
        }

        [Fact]
        public void RequiredDisciplines_AchievementOrMerchantDiscipline_ExcludedFromList()
        {
            // M37 fix-pass (adversarial review finding): "Achievement"/
            // "Merchant" are gw2e-borrowed informational source tags on the
            // new achievement-bit seed recipes, not real, player-levelable
            // GW2 crafting disciplines - they must never appear in Required
            // Disciplines (which the player reads as "disciplines to
            // unlock/level for this plan").
            var tree = TreeWithCraftStep(
                1, -1592, 1,
                new List<string> { "Achievement" }, 0, new List<string>(),
                Leaf(2, 1));

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = -1592 }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            Assert.Empty(result.RequiredDisciplines);
            Assert.Single(result.RequiredRecipes);
            Assert.Contains("Achievement", result.RequiredRecipes[0].Disciplines);
        }

        [Fact]
        public void RequiredRecipes_AchievementRecipe_IsMissingFalse_NotFlaggedAsUnlockable()
        {
            // M37 fix-pass: an achievement-sourced recipe (negative id,
            // adjacent to but distinct from the Mystic Forge id range) is
            // inherently available - no "learn this recipe" concept
            // applies - exactly like a real Mystic Forge recipe, even
            // though the underlying check is no longer a bare
            // "recipeId < 0" sign check (see PlanResultBuilder's
            // InherentlyAvailableDisciplines).
            var tree = TreeWithCraftStep(
                1, -1592, 1,
                new List<string> { "Achievement" }, 0, new List<string>(),
                Leaf(2, 1));

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 1,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = -1592 }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var learnedRecipeIds = new HashSet<int>(); // player has learned nothing
            var result = _builder.Build(plan, tree, metadata, null, learnedRecipeIds);

            Assert.Single(result.RequiredRecipes);
            Assert.False(result.RequiredRecipes[0].IsMissing);
        }

        [Fact]
        public void RequiredDisciplines_NoCraftSteps_EmptyList()
        {
            var tree = Leaf(1, 5);

            var plan = new CraftingPlan
            {
                TargetItemId = 1,
                TargetQuantity = 5,
                Steps = new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 5, Source = AcquisitionSource.BuyFromTp }
                }
            };

            var metadata = new Dictionary<int, ItemMetadata>();
            var result = _builder.Build(plan, tree, metadata, null, null);

            Assert.Empty(result.RequiredDisciplines);
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
