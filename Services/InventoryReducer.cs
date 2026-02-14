using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class InventoryReducer
    {
        public ReducedTreeResult Reduce(RecipeNode tree, Dictionary<int, int> ownedItems)
        {
            var pool = new Dictionary<int, int>(ownedItems);
            var usedRaw = new List<UsedMaterial>();

            var clone = CloneNode(tree);
            ReduceNode(clone, pool, usedRaw);

            var aggregated = usedRaw
                .GroupBy(u => u.ItemId)
                .Select(g => new UsedMaterial
                {
                    ItemId = g.Key,
                    QuantityUsed = g.Sum(u => u.QuantityUsed)
                })
                .Where(u => u.QuantityUsed > 0)
                .ToList();

            return new ReducedTreeResult
            {
                ReducedTree = clone,
                UsedMaterials = aggregated
            };
        }

        private void ReduceNode(
            RecipeNode node,
            Dictionary<int, int> pool,
            List<UsedMaterial> used)
        {
            if (node.IngredientType != "Item")
            {
                return;
            }

            int available = 0;
            pool.TryGetValue(node.Id, out available);
            int consume = Math.Min(available, node.Quantity);

            if (consume > 0)
            {
                pool[node.Id] = available - consume;
                used.Add(new UsedMaterial
                {
                    ItemId = node.Id,
                    QuantityUsed = consume
                });
                node.Quantity -= consume;
            }

            if (node.Quantity <= 0)
            {
                node.Quantity = 0;
                node.Recipes.Clear();
                return;
            }

            if (node.Recipes.Count == 0)
            {
                return;
            }

            foreach (var option in node.Recipes)
            {
                int origCraftsNeeded = option.CraftsNeeded;
                int newCraftsNeeded = (int)Math.Ceiling((double)node.Quantity / option.OutputCount);
                option.CraftsNeeded = newCraftsNeeded;

                foreach (var ingredient in option.Ingredients)
                {
                    int perCraft = (ingredient.Quantity + origCraftsNeeded - 1) / origCraftsNeeded;
                    ingredient.Quantity = perCraft * newCraftsNeeded;

                    ReduceNode(ingredient, pool, used);
                }
            }
        }

        private static RecipeNode CloneNode(RecipeNode node)
        {
            var clone = new RecipeNode
            {
                Id = node.Id,
                IngredientType = node.IngredientType,
                Quantity = node.Quantity
            };

            foreach (var option in node.Recipes)
            {
                clone.Recipes.Add(CloneOption(option));
            }

            return clone;
        }

        private static RecipeOption CloneOption(RecipeOption option)
        {
            var clone = new RecipeOption
            {
                RecipeId = option.RecipeId,
                OutputCount = option.OutputCount,
                CraftsNeeded = option.CraftsNeeded,
                Disciplines = new List<string>(option.Disciplines),
                MinRating = option.MinRating,
                Flags = new List<string>(option.Flags)
            };

            foreach (var ingredient in option.Ingredients)
            {
                clone.Ingredients.Add(CloneNode(ingredient));
            }

            return clone;
        }
    }
}
