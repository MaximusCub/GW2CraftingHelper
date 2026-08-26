using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Tests.Helpers
{
    /// <summary>
    /// Shared RecipeNode/RecipeOption tree-builder helpers.
    /// Leaf/Craftable/Option/WrapperOf were
    /// byte-for-byte identical private static methods duplicated across
    /// PlanSolverTests and CraftingTreeBuilderTests, with near-identical
    /// narrower variants in several other test files - consolidated here
    /// so future RecipeNode/RecipeOption field additions (e.g. the
    /// achievement-bit fields) only need to be plumbed through once.
    ///
    /// Leaf's achievementId/achievementBit optional parameters exist only
    /// to cover AchievementBitDedupPrePassTests' variant; every other
    /// caller uses Leaf(id, quantity) or Leaf(id, quantity, type) and gets
    /// the same RecipeNode it always did (both new parameters default to
    /// null, which is also RecipeNode.AchievementId/AchievementBit's own
    /// default).
    /// </summary>
    internal static class RecipeNodeBuilders
    {
        public static RecipeNode Leaf(int id, int quantity, string type = "Item", int? achievementId = null, int? achievementBit = null)
        {
            return new RecipeNode
            {
                Id = id,
                IngredientType = type,
                Quantity = quantity,
                Recipes = new List<RecipeOption>(),
                AchievementId = achievementId,
                AchievementBit = achievementBit,
            };
        }

        public static RecipeNode Craftable(int id, int quantity, params RecipeOption[] recipes)
        {
            var node = new RecipeNode
            {
                Id = id,
                IngredientType = "Item",
                Quantity = quantity,
                Recipes = new List<RecipeOption>(),
            };
            if (recipes != null)
            {
                node.Recipes.AddRange(recipes);
            }

            return node;
        }

        public static RecipeOption Option(int recipeId, int outputCount, int craftsNeeded, params RecipeNode[] ingredients)
        {
            var opt = new RecipeOption
            {
                RecipeId = recipeId,
                OutputCount = outputCount,
                CraftsNeeded = craftsNeeded,
                Ingredients = new List<RecipeNode>(),
            };
            if (ingredients != null)
            {
                opt.Ingredients.AddRange(ingredients);
            }

            return opt;
        }

        // Same additive-optional-parameter
        // pattern Leaf's achievementId/achievementBit already established -
        // every existing Option(...) call keeps building a Disciplines-free/
        // MinRating-0 RecipeOption (RecipeOption's own field defaults)
        // unchanged; only a caller that needs to exercise
        // CraftCompetencyEvaluator against a real RecipeOption shape
        // (PlanSolverCraftCompetencyTests) passes these.
        public static RecipeOption Option(
            int recipeId, int outputCount, int craftsNeeded,
            IReadOnlyList<string> disciplines, int minRating,
            params RecipeNode[] ingredients)
        {
            var opt = Option(recipeId, outputCount, craftsNeeded, ingredients);
            opt.Disciplines = disciplines != null ? new List<string>(disciplines) : new List<string>();
            opt.MinRating = minRating;
            return opt;
        }

        // --- Synthetic multi-item wrapper root (gw2e parity) ---
        public static RecipeNode WrapperOf(params RecipeNode[] itemRoots)
        {
            return Craftable(
                Gw2Constants.MultiItemWrapperItemId, 1,
                Option(Gw2Constants.MultiItemWrapperRecipeId, 1, 1, itemRoots));
        }
    }
}
