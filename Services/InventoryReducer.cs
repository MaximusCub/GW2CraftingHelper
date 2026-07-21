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
            var ownedUsageByNode = new Dictionary<RecipeNode, int>();

            var clone = CloneNode(tree);
            ReduceNode(clone, pool, usedRaw, ownedUsageByNode, consumeFromPool: true);

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
                UsedMaterials = aggregated,
                OwnedQuantityUsedByNode = ownedUsageByNode
            };
        }

        /// <summary>
        /// Reduces <paramref name="node"/> and its descendants against the
        /// shared <paramref name="pool"/>.
        ///
        /// <paramref name="consumeFromPool"/> (M34-B2a #2, gw2e parity / M1
        /// Finding 5): true only along the single chosen-recipe-candidate
        /// chain - the root, then recursively only each node's PRIMARY
        /// option (node.Recipes[0], the option RecipeService/the upstream
        /// recipe source puts first). PlanSolver has not run yet at
        /// reduction time, so which recipe option will actually be chosen is
        /// unknowable here; gw2efficiency's own tree never has this
        /// ambiguity because recipe-nesting nests exactly ONE recipe per
        /// node. Walking every option and letting each one drain the shared
        /// pool (the pre-fix behavior) would let a recipe option the solver
        /// never picks steal owned stock from a branch that IS chosen.
        /// Once false, it stays false for the whole subtree - nothing below
        /// a non-primary option should ever touch the pool, no matter how
        /// deep, since the whole branch is hypothetical from here down.
        ///
        /// Every option's CraftsNeeded/ingredient Quantity is still rescaled
        /// here regardless of consumeFromPool - that math reflects THIS
        /// node's own (already-decided, pool-independent) Quantity and is
        /// required for PlanSolver's cost comparison across recipe options
        /// to stay internally consistent (M33 Finding 1: every ingredient of
        /// every recipe is always evaluated, even one the solver ultimately
        /// doesn't choose).
        /// </summary>
        private void ReduceNode(
            RecipeNode node,
            Dictionary<int, int> pool,
            List<UsedMaterial> used,
            Dictionary<RecipeNode, int> ownedUsageByNode,
            bool consumeFromPool)
        {
            // In the current GW2 recipe model, only "Item" nodes are consumable
            // from inventory and can have recipes. Currency nodes are leaves.
            if (node.IngredientType != "Item")
            {
                return;
            }

            if (consumeFromPool)
            {
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
                    ownedUsageByNode[node] = consume;
                    node.Quantity -= consume;
                }
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

            for (int i = 0; i < node.Recipes.Count; i++)
            {
                var option = node.Recipes[i];
                int origCraftsNeeded = option.CraftsNeeded;
                int newCraftsNeeded = ComputeCraftsNeeded(node.Quantity, option);
                option.CraftsNeeded = newCraftsNeeded;

                bool optionConsumes = consumeFromPool && i == 0;

                foreach (var ingredient in option.Ingredients)
                {
                    int perCraft = (ingredient.Quantity + origCraftsNeeded - 1) / origCraftsNeeded;
                    ingredient.Quantity = perCraft * newCraftsNeeded;

                    ReduceNode(ingredient, pool, used, ownedUsageByNode, optionConsumes);
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
            var ownedUsageByNode = new Dictionary<RecipeNode, int>();

            var clone = CloneNode(tree);
            ReduceNodeSourced(clone, index, activeCharacterName, pool, usedRaw, ownedUsageByNode, consumeFromPool: true);

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
                UsedMaterials = aggregated,
                OwnedQuantityUsedByNode = ownedUsageByNode
            };
        }

        /// <summary>
        /// See ReduceNode's doc comment for <paramref name="consumeFromPool"/>
        /// (M34-B2a #2, gw2e parity / M1 Finding 5) - identical reasoning
        /// applies to this sourced overload: only the primary (first-listed)
        /// recipe option at each node may recurse with pool consumption
        /// enabled, so an alternate, un-chosen recipe option never drains
        /// owned stock a real branch needs.
        /// </summary>
        private void ReduceNodeSourced(
            RecipeNode node,
            AccountItemIndex index,
            string activeCharacterName,
            Dictionary<int, Dictionary<string, int>> pool,
            List<UsedMaterial> used,
            Dictionary<RecipeNode, int> ownedUsageByNode,
            bool consumeFromPool)
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

            if (consumeFromPool)
            {
                var prioritized = AccountItemIndex.GetPrioritizedSources(
                    node.Id, index, activeCharacterName);

                var allocations = new List<MaterialSourceAllocation>();
                int totalConsumed = 0;
                int remaining = needed;

                foreach (var source in prioritized)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    int available = GetPoolRemaining(pool, index, node.Id, source);
                    int consume = Math.Min(available, remaining);

                    if (consume > 0)
                    {
                        ConsumeFromPool(pool, index, node.Id, source, consume);
                        allocations.Add(new MaterialSourceAllocation
                        {
                            Source = source,
                            Quantity = consume
                        });
                        totalConsumed += consume;
                        remaining -= consume;
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
                    ownedUsageByNode[node] = totalConsumed;
                    node.Quantity -= totalConsumed;
                }
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

            for (int i = 0; i < node.Recipes.Count; i++)
            {
                var option = node.Recipes[i];
                int origCraftsNeeded = option.CraftsNeeded;
                int newCraftsNeeded = ComputeCraftsNeeded(node.Quantity, option);
                option.CraftsNeeded = newCraftsNeeded;

                bool optionConsumes = consumeFromPool && i == 0;

                foreach (var ingredient in option.Ingredients)
                {
                    int perCraft = (ingredient.Quantity + origCraftsNeeded - 1) / origCraftsNeeded;
                    ingredient.Quantity = perCraft * newCraftsNeeded;

                    ReduceNodeSourced(ingredient, index, activeCharacterName, pool, used, ownedUsageByNode, optionConsumes);
                }
            }
        }

        /// <summary>
        /// Recomputes how many crafting attempts are needed to produce
        /// <paramref name="quantity"/> of a node, using the SAME basis
        /// RecipeService used when it first built this option's
        /// CraftsNeeded/ingredient quantities: the fractional
        /// ExpectedOutputCount when the recipe has one (Mystic Clover-style
        /// EV recipes), falling back to the nominal integer OutputCount
        /// otherwise (a no-op for every ordinary recipe, where the two are
        /// equal). Using a DIFFERENT basis here than RecipeService used
        /// originally would desync origCraftsNeeded's per-craft ingredient
        /// ratio from the reduced tree's new crafts count - see M33 Finding
        /// 2 (CloneOption dropping ExpectedOutputCount silently disabled EV
        /// pricing whenever a snapshot triggered this reduction path).
        /// </summary>
        private static int ComputeCraftsNeeded(int quantity, RecipeOption option)
        {
            double effectiveOutputCount = option.ExpectedOutputCount > 0
                ? option.ExpectedOutputCount
                : option.OutputCount;

            try
            {
                return checked((int)Math.Ceiling((double)quantity / effectiveOutputCount));
            }
            catch (OverflowException)
            {
                // Malformed seed data (an absurdly tiny ExpectedOutputCount)
                // - fall back to the nominal integer output rather than
                // crash the whole reduction.
                return (int)Math.Ceiling((double)quantity / option.OutputCount);
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
                // M33 Finding 2 fix: this field was silently dropped here,
                // which zeroed it out (C# default) on every cloned option -
                // defeating EV pricing (PlanSolver/RecipeService's
                // ExpectedOutputCount-based math) whenever an account
                // snapshot triggered a Reduce() clone, i.e. the normal
                // own-materials path for a real plan.
                ExpectedOutputCount = option.ExpectedOutputCount,
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
