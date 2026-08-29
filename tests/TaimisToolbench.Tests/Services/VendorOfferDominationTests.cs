using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The price-free half of the vendor-cost fix: an offer charging a
    /// craftable recipe's own ingredients plus a fee cannot be the cheaper
    /// route, and every arm of the check fails closed because the cost of
    /// claiming domination wrongly is telling a player their only route does
    /// not exist. docs/ARCHITECTURE.md section 7.4.
    /// </summary>
    public class VendorOfferDominationTests
    {
        private static readonly Dictionary<string, int> MasterArmorsmith =
            new Dictionary<string, int> { { "Armorsmith", 500 } };

        private static RecipeNode NodeWithRecipe(
            int outputCount, int craftsNeeded, params RecipeNode[] ingredients)
        {
            var option = new RecipeOption
            {
                RecipeId = 1,
                OutputCount = outputCount,
                CraftsNeeded = craftsNeeded,
                ExpectedOutputCount = outputCount,
                Disciplines = new List<string> { "Armorsmith" },
                MinRating = 500,
            };
            option.Ingredients.AddRange(ingredients);

            var node = new RecipeNode { Id = 100, IngredientType = "Item", Quantity = 1 };
            node.Recipes.Add(option);
            return node;
        }

        private static RecipeNode Ingredient(int itemId, int quantity)
        {
            return new RecipeNode { Id = itemId, IngredientType = "Item", Quantity = quantity };
        }

        private static VendorOffer Offer(int outputCount, params CostLine[] costLines)
        {
            return new VendorOffer
            {
                OfferId = "test",
                OutputItemId = 100,
                OutputCount = outputCount,
                CostLines = new List<CostLine>(costLines),
                MerchantName = "Lyhr",
            };
        }

        private static CostLine Item(int itemId, int count)
        {
            return new CostLine { Type = "Item", Id = itemId, Count = count };
        }

        [Fact]
        public void TheIngredientsPlusAFee_IsDominated()
        {
            var node = NodeWithRecipe(1, 1, Ingredient(2, 1), Ingredient(3, 1));
            var offer = Offer(1, Item(2, 1), Item(3, 1), Item(19721, 10));

            Assert.True(VendorOfferDomination.IsDominatedByAnyRecipe(offer, node, MasterArmorsmith));
        }

        [Fact]
        public void TheIngredientsAndNothingMore_IsNotDominated()
        {
            // Charging exactly the ingredients is a real alternative: it skips
            // the discipline and the recipe sheet at no extra cost, and hiding
            // it would hide a route that is genuinely as good.
            var node = NodeWithRecipe(1, 1, Ingredient(2, 1), Ingredient(3, 1));
            var offer = Offer(1, Item(2, 1), Item(3, 1));

            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(offer, node, MasterArmorsmith));
        }

        [Fact]
        public void ChargingLessOfAnIngredient_IsNotDominated()
        {
            var node = NodeWithRecipe(1, 1, Ingredient(2, 5));
            var offer = Offer(1, Item(2, 4), Item(19721, 10));

            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(offer, node, MasterArmorsmith));
        }

        [Fact]
        public void TheSameIngredientInTwoRecipeSlots_IsTotalledBeforeComparing()
        {
            // A Mystic Forge recipe fills four slots and may fill two with the
            // same item. Comparing each slot against the offer's whole count
            // separately calls a 1x charge sufficient for a 2x requirement,
            // and claims domination for an offer that is genuinely cheaper.
            var node = NodeWithRecipe(1, 1, Ingredient(2, 1), Ingredient(2, 1));
            var offer = Offer(1, Item(2, 1), Item(19721, 10));

            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(offer, node, MasterArmorsmith));

            // Charging both, plus the fee, is dominated.
            var covering = Offer(1, Item(2, 2), Item(19721, 10));
            Assert.True(VendorOfferDomination.IsDominatedByAnyRecipe(covering, node, MasterArmorsmith));
        }

        [Fact]
        public void ANonItemCostLine_IsItselfTheSomethingMore()
        {
            // Coin or a wallet currency is cost the recipe does not charge, so
            // an offer with the exact ingredients plus one is still worse.
            var node = NodeWithRecipe(1, 1, Ingredient(2, 1));
            var offer = Offer(1, Item(2, 1), new CostLine { Type = "Currency", Id = 1, Count = 500 });

            Assert.True(VendorOfferDomination.IsDominatedByAnyRecipe(offer, node, MasterArmorsmith));
        }

        [Fact]
        public void AnUntrainedOrUnknownAccount_NeverDominates()
        {
            var node = NodeWithRecipe(1, 1, Ingredient(2, 1));
            var offer = Offer(1, Item(2, 1), Item(19721, 10));

            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(
                offer, node, new Dictionary<string, int>()));
            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(
                offer, node, new Dictionary<string, int> { { "Armorsmith", 400 } }));
            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(offer, node, null));
        }

        [Fact]
        public void ARecipeChargingACurrency_CannotBeLinedUpAgainstCostLines()
        {
            var node = NodeWithRecipe(
                1, 1,
                Ingredient(2, 1),
                new RecipeNode { Id = 23, IngredientType = "Currency", Quantity = 5 });
            var offer = Offer(1, Item(2, 1), Item(19721, 10));

            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(offer, node, MasterArmorsmith));
        }

        [Fact]
        public void AQuantityThatDoesNotDivideByCraftsNeeded_ClaimsNothing()
        {
            // Inventory reduction and achievement-bit dedup both rewrite a
            // node's Quantity, and the per-craft figure only divides out of an
            // untouched one. There is no honest number to compare, so there is
            // no claim to make.
            var node = NodeWithRecipe(1, 2, Ingredient(2, 5));
            var offer = Offer(1, Item(2, 100), Item(19721, 10));

            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(offer, node, MasterArmorsmith));
        }

        [Fact]
        public void AMultiOutputOfferMustCoverEveryCraftItReplaces()
        {
            // One batch of 4 replaces 4 crafts of a 1-output recipe, so it has
            // to charge 4x the ingredient before it is a worse copy.
            var node = NodeWithRecipe(1, 1, Ingredient(2, 1));

            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(
                Offer(4, Item(2, 3), Item(19721, 10)), node, MasterArmorsmith));
            Assert.True(VendorOfferDomination.IsDominatedByAnyRecipe(
                Offer(4, Item(2, 4), Item(19721, 10)), node, MasterArmorsmith));
        }

        [Fact]
        public void ANodeWithNoRecipe_HasNothingToBeDominatedBy()
        {
            var leaf = new RecipeNode { Id = 100, IngredientType = "Item", Quantity = 1 };

            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(
                Offer(1, Item(2, 1), Item(19721, 10)), leaf, MasterArmorsmith));
        }

        [Fact]
        public void ANegativeCostLine_ClaimsNothing()
        {
            var node = NodeWithRecipe(1, 1, Ingredient(2, 1));
            var offer = Offer(1, Item(2, 1), Item(19721, -10));

            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(offer, node, MasterArmorsmith));
        }
    }
}
