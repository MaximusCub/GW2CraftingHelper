using System;
using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// "Does this offer charge everything the recipe charges, and more?" -
    /// answered from the two ingredient lists alone, before any price is
    /// looked at.
    /// <para>
    /// It is the shape of a convenience vendor. Lyhr, in the Wizard's Tower,
    /// sells legendary armour pieces and the Gifts under them for exactly the
    /// craft ingredients plus 10 Globs of Ectoplasm; the wiki shows the same
    /// arrangement at three levels of the Obsidian armour chain. An offer of
    /// that shape cannot be the cheaper route for anyone who can craft, and
    /// saying so needs no prices, no valuations and no subtree - which is why
    /// it holds even where pricing cannot reach.
    /// </para>
    /// <para>
    /// Strictly a SECOND line of defence. Costing the cost lines
    /// (docs/ARCHITECTURE.md section 7.4) already prices such an offer above
    /// the craft it mirrors; this reaches the same verdict from the data's
    /// shape, so the two agree without either depending on the other.
    /// </para>
    /// </summary>
    internal static class VendorOfferDomination
    {
        /// <summary>
        /// True when some recipe of <paramref name="node"/> is one the
        /// account can actually craft AND <paramref name="offer"/> charges at
        /// least all of that recipe's ingredients plus something more.
        /// </summary>
        /// <remarks>
        /// Every arm below fails CLOSED - no domination claimed - because the
        /// consequence of claiming it wrongly is telling a player their only
        /// route does not exist. In particular it answers false whenever
        /// competency is unknown (<paramref name="bestRatingByDiscipline"/>
        /// null): "the recipe exists" is not "this account can use it", and
        /// only the second one makes the vendor redundant.
        /// </remarks>
        public static bool IsDominatedByAnyRecipe(
            VendorOffer offer,
            RecipeNode node,
            IReadOnlyDictionary<string, int> bestRatingByDiscipline)
        {
            // Ordered cheapest-first: bestRatingByDiscipline is null on every
            // account without a snapshot, and node.Recipes is empty for most
            // vendor-purchasable items, so the common calls cost one branch
            // and allocate nothing. This runs per offer per node, and a
            // single item id can carry hundreds of offers.
            if (bestRatingByDiscipline == null ||
                node == null || node.Recipes == null || node.Recipes.Count == 0 ||
                offer == null || offer.OutputCount <= 0 || offer.CostLines == null)
            {
                return false;
            }

            var offerItemCounts = SumItemCostLines(offer, out bool offerHasNonItemCost);
            if (offerItemCounts == null)
            {
                return false;
            }

            for (int i = 0; i < node.Recipes.Count; i++)
            {
                if (IsDominatedBy(node.Recipes[i], offerItemCounts, offerHasNonItemCost, offer.OutputCount, bestRatingByDiscipline))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDominatedBy(
            RecipeOption recipe,
            Dictionary<int, long> offerItemCounts,
            bool offerHasNonItemCost,
            int offerOutputCount,
            IReadOnlyDictionary<string, int> bestRatingByDiscipline)
        {
            if (recipe == null ||
                recipe.OutputCount <= 0 ||
                recipe.CraftsNeeded <= 0 ||
                recipe.Ingredients == null ||
                recipe.Ingredients.Count == 0)
            {
                return false;
            }

            if (!CraftCompetencyEvaluator.AccountCanCraft(
                    recipe.Disciplines, recipe.MinRating, bestRatingByDiscipline))
            {
                return false;
            }

            // How many crafts one batch of this offer replaces.
            long crafts = ((long)offerOutputCount + recipe.OutputCount - 1) / recipe.OutputCount;

            // Totalled per item id BEFORE anything is compared. A Mystic Forge
            // recipe fills four slots and may fill two of them with the same
            // item, and comparing each entry against the offer's whole count
            // separately would call a 1x charge sufficient for a 2x
            // requirement - domination claimed for an offer that is genuinely
            // the cheaper route.
            var needed = new Dictionary<int, long>(recipe.Ingredients.Count);
            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient == null ||
                    !string.Equals(ingredient.IngredientType, "Item", StringComparison.Ordinal))
                {
                    // A recipe charging anything but items cannot be lined up
                    // against cost lines this way.
                    return false;
                }

                // Ingredient quantities in a tree are already scaled by
                // CraftsNeeded, so the per-craft figure divides out - unless
                // something downstream rewrote a quantity (inventory
                // reduction, achievement-bit dedup), in which case the
                // division is not exact and there is no honest per-craft
                // number to compare against.
                if (ingredient.Quantity <= 0 || ingredient.Quantity % recipe.CraftsNeeded != 0)
                {
                    return false;
                }

                needed.TryGetValue(ingredient.Id, out long running);
                needed[ingredient.Id] = running + (crafts * (ingredient.Quantity / recipe.CraftsNeeded));
            }

            bool offerChargesMore = offerHasNonItemCost;

            foreach (var requirement in needed)
            {
                if (!offerItemCounts.TryGetValue(requirement.Key, out long charged) ||
                    charged < requirement.Value)
                {
                    return false;
                }

                if (charged > requirement.Value)
                {
                    offerChargesMore = true;
                }
            }

            // A cost line the recipe never asked for. Counting suffices: every
            // requirement matched a distinct entry above, so more entries than
            // requirements means at least one line is extra.
            if (!offerChargesMore && offerItemCounts.Count > needed.Count)
            {
                offerChargesMore = true;
            }

            // An offer charging EXACTLY the recipe's ingredients and nothing
            // else is not dominated: it is a real alternative that skips the
            // discipline and the recipe sheet at no extra cost, and hiding it
            // would be hiding a route that is genuinely as good.
            return offerChargesMore;
        }

        /// <summary>
        /// Item cost lines totalled per item id - an offer may list the same
        /// id twice - or null when a line's count is not a usable quantity.
        /// </summary>
        private static Dictionary<int, long> SumItemCostLines(VendorOffer offer, out bool hasNonItemCost)
        {
            hasNonItemCost = false;
            var counts = new Dictionary<int, long>(offer.CostLines.Count);

            foreach (var cost in offer.CostLines)
            {
                if (cost == null || cost.Count < 0)
                {
                    return null;
                }

                if (cost.Count == 0)
                {
                    continue;
                }

                if (string.Equals(cost.Type, "Item", StringComparison.Ordinal))
                {
                    counts.TryGetValue(cost.Id, out long existing);
                    counts[cost.Id] = existing + cost.Count;
                }
                else
                {
                    // Coin, a wallet currency, or a shape this solver has
                    // never seen: all of them are cost the recipe does not
                    // charge, which is what makes the offer strictly worse.
                    hasNonItemCost = true;
                }
            }

            return counts;
        }
    }
}
