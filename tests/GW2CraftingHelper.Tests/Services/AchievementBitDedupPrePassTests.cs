using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// M37 (KNOWN-ISSUES #26, gw2e parity - achievement-bit ingredient
    /// dedup). The BitDuplicate_/BitOnlyDuplicate_/OrdinaryDuplicate_ tests
    /// below port the exact scenario and asserted values from gw2e's own
    /// ground-truth unit test
    /// (recipe-calculation@master, tests/calculateTreeQuantity.spec.ts,
    /// "handles achievement bit items correctly" - quoted verbatim in
    /// docs/research/m37-r3-achievement-dedup.md Section 1.4) using ids
    /// 55/56/999 exactly as upstream does, since this pass is a
    /// general-purpose algorithm and does not itself need real GW2 item
    /// ids to be correctly exercised - the real, wiki/API-verified Infinite
    /// Trebuchet Blueprint scenario (docs/research/m37-r3-achievement-dedup.md
    /// Section 4.6) is exercised separately, through the real production
    /// pipeline, in MultiItemPlanTests.
    /// </summary>
    public class AchievementBitDedupPrePassTests
    {
        private static RecipeNode Leaf(int id, int quantity, int? achievementId = null, int? achievementBit = null, string type = "Item")
        {
            return new RecipeNode
            {
                Id = id,
                IngredientType = type,
                Quantity = quantity,
                AchievementId = achievementId,
                AchievementBit = achievementBit
            };
        }

        private static RecipeOption Option(int recipeId, params RecipeNode[] ingredients)
        {
            var opt = new RecipeOption { RecipeId = recipeId, OutputCount = 1, CraftsNeeded = 1 };
            opt.Ingredients.AddRange(ingredients);
            return opt;
        }

        [Fact]
        public void Apply_NullTree_DoesNotThrow()
        {
            AchievementBitDedupPrePass.Apply(null);
        }

        [Fact]
        public void Apply_TreeWithNoAchievementFields_IsCompletelyUnchanged()
        {
            // M37 regression requirement: every one of the 14,732 existing
            // seed rows has no achievement fields at all - this pass must
            // be a byte-identical no-op for them.
            var root = Leaf(1, 5);
            root.Recipes.Add(Option(10,
                Leaf(2, 3),
                Leaf(2, 3), // an ordinary duplicate id, unaffected either way
                Leaf(3, 1)));

            AchievementBitDedupPrePass.Apply(root);

            Assert.Equal(5, root.Quantity);
            Assert.False(root.IsAchievementBitDeduped);
            var ingredients = root.Recipes[0].Ingredients;
            Assert.Equal(3, ingredients.Count);
            Assert.All(ingredients, i => Assert.False(i.IsAchievementBitDeduped));
            Assert.Equal(3, ingredients[0].Quantity);
            Assert.Equal(3, ingredients[1].Quantity);
            Assert.Equal(1, ingredients[2].Quantity);
        }

        [Fact]
        public void BitAndNormalCoexist_BothBitOccurrencesZeroed_NormalOccurrenceUntouched()
        {
            // Ports gw2e's id 55: a top-level bit occurrence, a bit
            // occurrence nested one level deeper (inside another recipe),
            // and a normal (non-bit) occurrence, quantity 2. Asserted:
            // BOTH bit occurrences go to 0 (even the very first one
            // encountered - the pre-seed applies before any node is
            // visited), the normal occurrence is completely untouched.
            var id55TopBit = Leaf(55, 1, achievementId: 1, achievementBit: 0);
            var id55Normal = Leaf(55, 2);
            var id200 = Leaf(200, 1);
            var id55NestedBit = Leaf(55, 1, achievementId: 1, achievementBit: 0);
            id200.Recipes.Add(Option(20, id55NestedBit));

            var root = Leaf(1, 1);
            root.Recipes.Add(Option(10, id55TopBit, id200, id55Normal));

            AchievementBitDedupPrePass.Apply(root);

            Assert.Equal(0, id55TopBit.Quantity);
            Assert.True(id55TopBit.IsAchievementBitDeduped);
            Assert.Empty(id55TopBit.Recipes);

            Assert.Equal(0, id55NestedBit.Quantity);
            Assert.True(id55NestedBit.IsAchievementBitDeduped);

            Assert.Equal(2, id55Normal.Quantity);
            Assert.False(id55Normal.IsAchievementBitDeduped);
        }

        [Fact]
        public void BitOnlyDuplicates_FirstOccurrenceKept_SecondZeroed()
        {
            // Ports gw2e's id 56: two achievement-bit occurrences, no
            // normal occurrence anywhere. First (DFS order) keeps its
            // quantity; the second is zeroed.
            var first = Leaf(56, 1, achievementId: 2, achievementBit: 1);
            var second = Leaf(56, 1, achievementId: 2, achievementBit: 1);

            var root = Leaf(1, 1);
            root.Recipes.Add(Option(10, first, second));

            AchievementBitDedupPrePass.Apply(root);

            Assert.Equal(1, first.Quantity);
            Assert.False(first.IsAchievementBitDeduped);

            Assert.Equal(0, second.Quantity);
            Assert.True(second.IsAchievementBitDeduped);
            Assert.Empty(second.Recipes);
        }

        [Fact]
        public void OrdinaryDuplicateNonBitItems_NeitherOccurrenceTouched()
        {
            // Ports gw2e's id 999: an ordinary repeated item with no
            // achievement_bit anywhere - each occurrence keeps its own
            // independently-computed quantity. This pass never touches it;
            // cross-branch aggregation is PlanSolver's own job.
            var first = Leaf(999, 1);
            var second = Leaf(999, 3);

            var root = Leaf(1, 1);
            root.Recipes.Add(Option(10, first, second));

            AchievementBitDedupPrePass.Apply(root);

            Assert.Equal(1, first.Quantity);
            Assert.Equal(3, second.Quantity);
            Assert.False(first.IsAchievementBitDeduped);
            Assert.False(second.IsAchievementBitDeduped);
        }

        [Fact]
        public void ZeroedOccurrence_ClearsOwnRecipes_DescendantsUnreachable()
        {
            // The Recipe-shaped (not bare leaf) case: a zeroed
            // achievement-bit node's own Recipes are cleared so nothing
            // downstream (PlanSolver) can still find a craft path into its
            // now-irrelevant children - see the class's own doc comment for
            // why this is necessary, not merely "does not recurse".
            var normal = Leaf(55, 1);
            var bitWithChildren = Leaf(55, 1, achievementId: 1, achievementBit: 0);
            bitWithChildren.Recipes.Add(Option(30, Leaf(999, 100)));

            var root = Leaf(1, 1);
            root.Recipes.Add(Option(10, bitWithChildren, normal));

            AchievementBitDedupPrePass.Apply(root);

            Assert.Equal(0, bitWithChildren.Quantity);
            Assert.True(bitWithChildren.IsAchievementBitDeduped);
            Assert.Empty(bitWithChildren.Recipes);
        }

        [Fact]
        public void CurrencyIngredient_NeverClassifiedOrZeroed_EvenWithAchievementBitSet()
        {
            // Upstream explicitly excludes Currency-type nodes from the
            // dedup mechanism (achievement_bit is not expected on a
            // Currency ingredient in practice, but the exclusion is a type
            // check, not a value check - defensive parity either way).
            var currencyBit = Leaf(77, 500, achievementId: 1, achievementBit: 0, type: "Currency");
            var normal = Leaf(77, 10); // same numeric id, but as a real Item elsewhere

            var root = Leaf(1, 1);
            root.Recipes.Add(Option(10, currencyBit, normal));

            AchievementBitDedupPrePass.Apply(root);

            Assert.Equal(500, currencyBit.Quantity);
            Assert.False(currencyBit.IsAchievementBitDeduped);
            Assert.Equal(10, normal.Quantity);
        }

        [Fact]
        public void MultiItemWrapperNode_SkippedForClassification_ChildrenStillWalked()
        {
            // The synthetic multi-item wrapper root (Gw2Constants.
            // MultiItemWrapperItemId) is never a real GW2 item and must
            // never itself be classified, but its own recipe's Ingredients
            // (the N real item roots) must still be walked normally - this
            // is exactly how the multi-item double-count scenario
            // (MultiItemPlanTests) gets seen by this pass at all.
            var bitOccurrence = Leaf(55, 1, achievementId: 1, achievementBit: 0);
            var normalOccurrence = Leaf(55, 1);

            var wrapper = new RecipeNode { Id = Gw2Constants.MultiItemWrapperItemId, IngredientType = "Item", Quantity = 1 };
            var wrapperRecipe = new RecipeOption { RecipeId = Gw2Constants.MultiItemWrapperRecipeId, OutputCount = 1, CraftsNeeded = 1 };
            wrapperRecipe.Ingredients.Add(bitOccurrence);
            wrapperRecipe.Ingredients.Add(normalOccurrence);
            wrapper.Recipes.Add(wrapperRecipe);

            AchievementBitDedupPrePass.Apply(wrapper);

            Assert.False(wrapper.IsAchievementBitDeduped);
            Assert.Equal(0, bitOccurrence.Quantity);
            Assert.True(bitOccurrence.IsAchievementBitDeduped);
            Assert.Equal(1, normalOccurrence.Quantity);
            Assert.False(normalOccurrence.IsAchievementBitDeduped);
        }

        [Fact]
        public void MultipleRecipeOptions_BothWalkedForClassificationAndZeroing()
        {
            // This module's architecture (unlike gw2e's single-recipe-per-
            // node nested tree) carries multiple alternate RecipeOptions per
            // node for cost comparison (PlanSolver.Evaluate walks every
            // option) - the dedup walk must cover every option's
            // Ingredients too, not just the first.
            var bitInOptionA = Leaf(55, 1, achievementId: 1, achievementBit: 0);
            var bitInOptionB = Leaf(55, 1, achievementId: 1, achievementBit: 0);

            var root = Leaf(1, 1);
            root.Recipes.Add(Option(10, bitInOptionA));
            root.Recipes.Add(Option(11, bitInOptionB));

            AchievementBitDedupPrePass.Apply(root);

            Assert.Equal(1, bitInOptionA.Quantity);
            Assert.False(bitInOptionA.IsAchievementBitDeduped);
            Assert.Equal(0, bitInOptionB.Quantity);
            Assert.True(bitInOptionB.IsAchievementBitDeduped);
        }
    }
}
