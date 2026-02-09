using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class PlanSolver
    {
        private struct Decision
        {
            public AcquisitionSource Source;
            public long? TotalCost;
            public int RecipeId;
        }

        public CraftingPlan Solve(RecipeNode tree, IReadOnlyDictionary<int, ItemPrice> prices)
        {
            var memo = new Dictionary<(int, int), Decision>();

            // Pass 1: decide buy vs craft at every node
            Evaluate(tree, prices, memo);

            // Pass 2: collect steps and currency costs following pass-1 decisions
            var stepMap = new Dictionary<(int, AcquisitionSource, int), PlanStep>();
            var currencyMap = new Dictionary<int, int>();
            var craftOrder = new Dictionary<(int, int), int>();
            int craftCounter = 0;

            Collect(tree, memo, stepMap, currencyMap, craftOrder, ref craftCounter);

            // Build ordered step list: buys/unknowns first, then crafts in bottom-up order
            var buysAndUnknowns = new List<PlanStep>();
            var crafts = new List<(PlanStep step, int order)>();

            foreach (var step in stepMap.Values)
            {
                if (step.Source == AcquisitionSource.Craft)
                {
                    var craftKey = (step.ItemId, step.RecipeId);
                    int order = craftOrder.ContainsKey(craftKey) ? craftOrder[craftKey] : 0;
                    crafts.Add((step, order));
                }
                else
                {
                    buysAndUnknowns.Add(step);
                }
            }

            crafts.Sort((a, b) => a.order.CompareTo(b.order));

            var steps = new List<PlanStep>(buysAndUnknowns);
            steps.AddRange(crafts.Select(c => c.step));

            long totalCoinCost = 0L;
            foreach (var step in steps)
            {
                if (step.Source == AcquisitionSource.BuyFromTp)
                {
                    totalCoinCost += step.TotalCost;
                }
            }

            var currencyCosts = new List<CurrencyCost>();
            foreach (var kvp in currencyMap)
            {
                currencyCosts.Add(new CurrencyCost { CurrencyId = kvp.Key, Amount = kvp.Value });
            }

            return new CraftingPlan
            {
                TargetItemId = tree.Id,
                TargetQuantity = tree.Quantity,
                Steps = steps,
                TotalCoinCost = totalCoinCost,
                CurrencyCosts = currencyCosts
            };
        }

        private long? Evaluate(
            RecipeNode node,
            IReadOnlyDictionary<int, ItemPrice> prices,
            Dictionary<(int, int), Decision> memo)
        {
            if (node.IngredientType == "Currency")
            {
                return null;
            }

            var key = (node.Id, node.Quantity);
            if (memo.TryGetValue(key, out var cached))
            {
                return cached.TotalCost;
            }

            // Leaf item: no recipes
            if (node.IsLeaf)
            {
                long? buyCost = GetBuyCost(node.Id, node.Quantity, prices);
                if (buyCost.HasValue)
                {
                    memo[key] = new Decision
                    {
                        Source = AcquisitionSource.BuyFromTp,
                        TotalCost = buyCost.Value,
                        RecipeId = 0
                    };
                    return buyCost.Value;
                }

                memo[key] = new Decision
                {
                    Source = AcquisitionSource.UnknownSource,
                    TotalCost = null,
                    RecipeId = 0
                };
                return null;
            }

            // Has recipes: evaluate each option
            long? buyTotalCost = GetBuyCost(node.Id, node.Quantity, prices);

            long? bestCraftCost = null;
            int bestRecipeId = 0;

            foreach (var recipe in node.Recipes)
            {
                long craftCost = 0L;
                bool allPriceable = true;

                foreach (var ingredient in recipe.Ingredients)
                {
                    if (ingredient.IngredientType == "Currency")
                    {
                        continue;
                    }

                    long? ingredientCost = Evaluate(ingredient, prices, memo);
                    if (!ingredientCost.HasValue)
                    {
                        allPriceable = false;
                        break;
                    }

                    craftCost += ingredientCost.Value;
                }

                if (allPriceable)
                {
                    if (!bestCraftCost.HasValue || craftCost < bestCraftCost.Value)
                    {
                        bestCraftCost = craftCost;
                        bestRecipeId = recipe.RecipeId;
                    }
                }
                else if (!bestCraftCost.HasValue && bestRecipeId == 0)
                {
                    // Track unpriceable craft as fallback if no priceable option yet
                    bestRecipeId = recipe.RecipeId;
                }
            }

            // Compare buy vs craft
            if (buyTotalCost.HasValue && bestCraftCost.HasValue)
            {
                if (buyTotalCost.Value <= bestCraftCost.Value)
                {
                    memo[key] = new Decision
                    {
                        Source = AcquisitionSource.BuyFromTp,
                        TotalCost = buyTotalCost.Value,
                        RecipeId = 0
                    };
                    return buyTotalCost.Value;
                }
                else
                {
                    memo[key] = new Decision
                    {
                        Source = AcquisitionSource.Craft,
                        TotalCost = bestCraftCost.Value,
                        RecipeId = bestRecipeId
                    };
                    return bestCraftCost.Value;
                }
            }

            if (buyTotalCost.HasValue)
            {
                memo[key] = new Decision
                {
                    Source = AcquisitionSource.BuyFromTp,
                    TotalCost = buyTotalCost.Value,
                    RecipeId = 0
                };
                return buyTotalCost.Value;
            }

            if (bestRecipeId != 0)
            {
                memo[key] = new Decision
                {
                    Source = AcquisitionSource.Craft,
                    TotalCost = bestCraftCost,
                    RecipeId = bestRecipeId
                };
                return bestCraftCost;
            }

            memo[key] = new Decision
            {
                Source = AcquisitionSource.UnknownSource,
                TotalCost = null,
                RecipeId = 0
            };
            return null;
        }

        private void Collect(
            RecipeNode node,
            Dictionary<(int, int), Decision> memo,
            Dictionary<(int, AcquisitionSource, int), PlanStep> stepMap,
            Dictionary<int, int> currencyMap,
            Dictionary<(int, int), int> craftOrder,
            ref int craftCounter)
        {
            if (node.IngredientType == "Currency")
            {
                if (currencyMap.ContainsKey(node.Id))
                {
                    currencyMap[node.Id] += node.Quantity;
                }
                else
                {
                    currencyMap[node.Id] = node.Quantity;
                }
                return;
            }

            var key = (node.Id, node.Quantity);
            if (!memo.TryGetValue(key, out var decision))
            {
                return;
            }

            if (decision.Source == AcquisitionSource.Craft)
            {
                // Recurse into the chosen recipe's ingredients first (bottom-up)
                var chosenRecipe = node.Recipes.FirstOrDefault(r => r.RecipeId == decision.RecipeId);
                if (chosenRecipe != null)
                {
                    foreach (var ingredient in chosenRecipe.Ingredients)
                    {
                        Collect(ingredient, memo, stepMap, currencyMap, craftOrder, ref craftCounter);
                    }
                }

                // Record craft order (first time seeing this item+recipe as craft)
                var craftOrderKey = (node.Id, decision.RecipeId);
                if (!craftOrder.ContainsKey(craftOrderKey))
                {
                    craftOrder[craftOrderKey] = craftCounter++;
                }

                var stepKey = (node.Id, AcquisitionSource.Craft, decision.RecipeId);
                AggregateStep(stepMap, stepKey, node, decision);
            }
            else
            {
                var stepKey = (node.Id, decision.Source, 0);
                AggregateStep(stepMap, stepKey, node, decision);
            }
        }

        private void AggregateStep(
            Dictionary<(int, AcquisitionSource, int), PlanStep> stepMap,
            (int, AcquisitionSource, int) stepKey,
            RecipeNode node,
            Decision decision)
        {
            if (stepMap.TryGetValue(stepKey, out var existing))
            {
                existing.Quantity += node.Quantity;
                existing.TotalCost = decision.TotalCost.HasValue
                    ? existing.TotalCost + decision.TotalCost.Value
                    : existing.TotalCost;
                if (existing.Source == AcquisitionSource.BuyFromTp && existing.Quantity > 0)
                {
                    existing.UnitCost = existing.TotalCost / existing.Quantity;
                }
            }
            else
            {
                long unitCost = 0;
                if (decision.Source == AcquisitionSource.BuyFromTp && node.Quantity > 0 && decision.TotalCost.HasValue)
                {
                    unitCost = decision.TotalCost.Value / node.Quantity;
                }

                stepMap[stepKey] = new PlanStep
                {
                    ItemId = node.Id,
                    Quantity = node.Quantity,
                    Source = decision.Source,
                    UnitCost = unitCost,
                    TotalCost = decision.TotalCost ?? 0L,
                    RecipeId = decision.RecipeId
                };
            }
        }

        private long? GetBuyCost(int itemId, int quantity, IReadOnlyDictionary<int, ItemPrice> prices)
        {
            if (prices.TryGetValue(itemId, out var price) && price.BuyInstant > 0)
            {
                return (long)quantity * price.BuyInstant;
            }
            return null;
        }
    }
}
