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
            // In the current GW2 recipe model, only "Item" nodes are consumable
            // from inventory and can have recipes. Currency nodes are leaves.
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

        public ReducedTreeResult Reduce(
            RecipeNode tree,
            AccountItemIndex index,
            string activeCharacterName)
        {
            // Build a mutable consumption pool: itemId -> source -> remaining
            var pool = new Dictionary<int, Dictionary<string, int>>();
            var usedRaw = new List<UsedMaterial>();

            var clone = CloneNode(tree);
            ReduceNodeSourced(clone, index, activeCharacterName, pool, usedRaw);

            var aggregated = usedRaw
                .GroupBy(u => u.ItemId)
                .Select(g =>
                {
                    var allSources = g
                        .Where(u => u.Sources != null)
                        .SelectMany(u => u.Sources)
                        .GroupBy(s => s.Source, StringComparer.Ordinal)
                        .Select(sg => new MaterialSourceAllocation
                        {
                            Source = sg.Key,
                            Quantity = sg.Sum(a => a.Quantity)
                        })
                        .Where(a => a.Quantity > 0)
                        .OrderBy(a => a.Source, StringComparer.Ordinal)
                        .ToList();

                    return new UsedMaterial
                    {
                        ItemId = g.Key,
                        QuantityUsed = g.Sum(u => u.QuantityUsed),
                        Sources = allSources
                    };
                })
                .Where(u => u.QuantityUsed > 0)
                .ToList();

            return new ReducedTreeResult
            {
                ReducedTree = clone,
                UsedMaterials = aggregated
            };
        }

        private void ReduceNodeSourced(
            RecipeNode node,
            AccountItemIndex index,
            string activeCharacterName,
            Dictionary<int, Dictionary<string, int>> pool,
            List<UsedMaterial> used)
        {
            // In the current GW2 recipe model, only "Item" nodes are consumable
            // from inventory and can have recipes. Currency nodes are leaves.
            if (node.IngredientType != "Item")
            {
                return;
            }

            int needed = node.Quantity;
            if (needed <= 0)
            {
                return;
            }

            var prioritized = AccountItemIndex.GetPrioritizedSources(
                node.Id, index, activeCharacterName);

            var allocations = new List<MaterialSourceAllocation>();
            int totalConsumed = 0;

            foreach (var source in prioritized)
            {
                if (needed <= 0)
                {
                    break;
                }

                int available = GetPoolRemaining(pool, index, node.Id, source);
                int consume = Math.Min(available, needed);

                if (consume > 0)
                {
                    ConsumeFromPool(pool, index, node.Id, source, consume);
                    allocations.Add(new MaterialSourceAllocation
                    {
                        Source = source,
                        Quantity = consume
                    });
                    totalConsumed += consume;
                    needed -= consume;
                }
            }

            if (totalConsumed > 0)
            {
                used.Add(new UsedMaterial
                {
                    ItemId = node.Id,
                    QuantityUsed = totalConsumed,
                    Sources = allocations
                });
                node.Quantity -= totalConsumed;
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

                    ReduceNodeSourced(ingredient, index, activeCharacterName, pool, used);
                }
            }
        }

        private static int GetPoolRemaining(
            Dictionary<int, Dictionary<string, int>> pool,
            AccountItemIndex index,
            int itemId,
            string source)
        {
            if (pool.TryGetValue(itemId, out var sourcePool) &&
                sourcePool.TryGetValue(source, out int remaining))
            {
                return Math.Max(0, remaining);
            }

            // First access: initialize from index
            return index.GetQuantity(itemId, source);
        }

        private static void ConsumeFromPool(
            Dictionary<int, Dictionary<string, int>> pool,
            AccountItemIndex index,
            int itemId,
            string source,
            int amount)
        {
            if (!pool.TryGetValue(itemId, out var sourcePool))
            {
                sourcePool = new Dictionary<string, int>(StringComparer.Ordinal);
                pool[itemId] = sourcePool;
            }

            if (!sourcePool.TryGetValue(source, out int remaining))
            {
                remaining = index.GetQuantity(itemId, source);
            }

            sourcePool[source] = Math.Max(0, remaining - amount);
        }

        private static RecipeNode CloneNode(RecipeNode node)
        {
            var clone = new RecipeNode
            {
                Id = node.Id,
                IngredientType = node.IngredientType,
                Quantity = node.Quantity,
                NodeId = node.NodeId
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
