using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class InventoryReducer
    {
        public ReducedTreeResult Reduce(
            RecipeNode tree,
            AccountItemIndex index,
            string activeCharacterName,
            // Value-Own-Materials (VOM) design, Candidate A: the Decisions
            // dictionary from a throwaway zero-owned PlanSolver.Solve on the
            // SAME unreduced tree (with forceBuyOnlyNodeIds already applied)
            // - see CraftingPlanPipeline.RunPipelineAsync's zero-owned solve.
            // Keyed
            // by RecipeNode.NodeId, which must already be assigned on
            // `tree` (RecipeNodeIds.Assign) before this call, since it is
            // what CloneNode below preserves onto the clone this method
            // walks. Null reproduces the legacy i==0-primary-option
            // heuristic - see ReduceNodeSourced's own doc comment.
            IReadOnlyDictionary<int, SolverDecision> zeroOwnedDecisions = null)
        {
            // Build a mutable consumption pool: itemId -> source -> remaining
            var pool = new Dictionary<int, Dictionary<string, int>>();
            var usedRaw = new List<UsedMaterial>();
            var ownedUsageByNode = new Dictionary<RecipeNode, int>();

            var clone = CloneNode(tree);
            ReduceNodeSourced(clone, index, activeCharacterName, pool, usedRaw, ownedUsageByNode, consumeFromPool: true, zeroOwnedDecisions);

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
                            Quantity = sg.Sum(a => a.Quantity),
                        })
                        .Where(a => a.Quantity > 0)
                        .OrderBy(a => a.Source, StringComparer.Ordinal)
                        .ToList();

                    return new UsedMaterial
                    {
                        ItemId = g.Key,
                        QuantityUsed = g.Sum(u => u.QuantityUsed),
                        Sources = allSources,
                    };
                })
                .Where(u => u.QuantityUsed > 0)
                .ToList();

            return new ReducedTreeResult
            {
                ReducedTree = clone,
                UsedMaterials = aggregated,
                OwnedQuantityUsedByNode = ownedUsageByNode,
            };
        }

        /// <summary>
        /// Reduces <paramref name="node"/> and its descendants against the
        /// shared <paramref name="pool"/>, lazily initialized per
        /// item/source from <paramref name="index"/>.
        ///
        /// <paramref name="consumeFromPool"/> decides whether THIS node's
        /// own Quantity may be discounted (inherited from the caller/parent,
        /// unchanged by this design) and, together with
        /// <paramref name="zeroOwnedDecisions"/> below, which recipe
        /// option's descendants get to consume the pool:
        ///
        /// - VOM design (Candidate A), decision-guided mode: when
        ///   <paramref name="zeroOwnedDecisions"/> is non-null and contains
        ///   this node's NodeId, the recipe option that decision's
        ///   Source == Craft &amp;&amp; RecipeId matches is the one whose
        ///   descendants may consume the pool - every sibling option is left
        ///   at full zero-owned cost. If the node's zero-owned decision was
        ///   anything other than Craft (BuyFromTp/BuyFromVendor/
        ///   UnknownSource), NO option consumes the pool for its
        ///   descendants: an un-crafted branch never demands its own
        ///   ingredients, mirroring PlanSolver.Evaluate's own
        ///   ignoredItemIds/Quantity==0 handling elsewhere in this codebase.
        ///   Since discounting only ever lowers a cost, and only along the
        ///   path the zero-owned pass already declared the winner, owned
        ///   ingredient stock further down the tree can never pull the real
        ///   (post-reduction) Solve() toward a DIFFERENT recipe option than
        ///   the guide chose for this node.
        ///
        ///   KNOWN RESIDUAL (not guarded/tested - see KNOWN-ISSUES #20):
        ///   this does NOT guarantee the guide's Source/Craft-vs-Buy
        ///   decision for THIS node itself still holds after reduction. The
        ///   guide is computed on the UNREDUCED tree, but this node's OWN
        ///   Quantity (above, via consumeFromPool on the CALLER's side) can
        ///   still shrink here from owned stock of the node's own item id -
        ///   and because craft cost is non-linear in quantity
        ///   (ComputeCraftsNeeded's ceiling division, and
        ///   VendorBatchSolver's per-batch math), shrinking a node's own
        ///   Quantity can raise its effective per-unit cost enough to flip
        ///   the REAL solve's decision for this node away from what the
        ///   guide assumed (e.g. Craft -&gt; Buy) - after this node's
        ///   ingredients were already discounted and written into
        ///   UsedMaterials against the guide's Craft assumption. Requires a
        ///   node with owned stock of ITSELF plus owned stock of its own
        ///   ingredients, and a recipe/vendor batch whose output count is
        ///   greater than 1.
        /// - Legacy heuristic: used
        ///   whenever <paramref name="zeroOwnedDecisions"/> is null (every
        ///   pre-VOM caller/test) OR does not contain this node's NodeId
        ///   (defensive fallback) - true only along the single
        ///   chosen-recipe-candidate chain, the root, then recursively only
        ///   each node's PRIMARY option (node.Recipes[0], the option
        ///   RecipeService/the upstream recipe source puts first). Without a
        ///   guide, which recipe option will actually be chosen is
        ///   unknowable at reduction time; walking every option and letting
        ///   each one drain the shared pool
        ///   would let a recipe option the solver never picks steal owned
        ///   stock from a branch that IS chosen.
        ///
        /// Once an option's descendants are excluded from pool consumption,
        /// that stays false for the whole subtree below it - nothing under a
        /// non-chosen/non-primary option should ever touch the pool, no
        /// matter how deep, since the whole branch is hypothetical (or,
        /// under the guide, provably not the winning path) from here down.
        ///
        /// Every option's CraftsNeeded/ingredient Quantity is still rescaled
        /// here regardless of consumeFromPool - that math reflects THIS
        /// node's own (already-decided, pool-independent) Quantity and is
        /// required for PlanSolver's cost comparison across recipe options
        /// to stay internally consistent (every ingredient of every recipe
        /// is always evaluated, even one the solver ultimately doesn't
        /// choose).
        /// </summary>
        private void ReduceNodeSourced(
            RecipeNode node,
            AccountItemIndex index,
            string activeCharacterName,
            Dictionary<int, Dictionary<string, int>> pool,
            List<UsedMaterial> used,
            Dictionary<RecipeNode, int> ownedUsageByNode,
            bool consumeFromPool,
            IReadOnlyDictionary<int, SolverDecision> zeroOwnedDecisions)
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
                            Quantity = consume,
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
                        Sources = allocations,
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

            SolverDecision guideDecision = null;
            // Reduce is public API with an IReadOnlyDictionary
            // parameter - PlanSolver never emits a null VALUE, but nothing
            // stops a caller from doing so. TryGetValue alone returns true
            // for an entry whose value IS null, and the code below
            // dereferences guideDecision.Source unconditionally whenever
            // hasGuide is true - the extra null check keeps a
            // maliciously/accidentally null-valued entry falling back to
            // the safe legacy heuristic instead of throwing.
            bool hasGuide = zeroOwnedDecisions != null &&
                zeroOwnedDecisions.TryGetValue(node.NodeId, out guideDecision) &&
                guideDecision != null;

            for (int i = 0; i < node.Recipes.Count; i++)
            {
                var option = node.Recipes[i];
                int origCraftsNeeded = option.CraftsNeeded;
                int newCraftsNeeded = ComputeCraftsNeeded(node.Quantity, option);
                option.CraftsNeeded = newCraftsNeeded;

                bool optionConsumes = hasGuide
                    ? consumeFromPool &&
                        guideDecision.Source == AcquisitionSource.Craft &&
                        option.RecipeId == guideDecision.RecipeId
                    : consumeFromPool && i == 0;

                foreach (var ingredient in option.Ingredients)
                {
                    int perCraft = (ingredient.Quantity + origCraftsNeeded - 1) / origCraftsNeeded;
                    ingredient.Quantity = perCraft * newCraftsNeeded;

                    ReduceNodeSourced(ingredient, index, activeCharacterName, pool, used, ownedUsageByNode, optionConsumes, zeroOwnedDecisions);
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
        /// ratio from the reduced tree's new crafts count - CloneOption
        /// once dropped ExpectedOutputCount and silently disabled EV
        /// pricing whenever a snapshot triggered this reduction path.
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
                NodeId = node.NodeId,
                // Must be copied explicitly, same as every other field
                // here - see the ExpectedOutputCount comment in CloneOption
                // below for why a field silently missing from this clone is
                // a real, previously-hit bug class in this codebase.
                // AchievementBitDedupPrePass runs on the pre-reduction tree
                // (before this clone is made), so IsAchievementBitDeduped
                // must survive onto the tree PlanSolver/CraftingTreeBuilder
                // actually consume.
                AchievementId = node.AchievementId,
                AchievementBit = node.AchievementBit,
                IsAchievementBitDeduped = node.IsAchievementBitDeduped,
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
                // This field was once silently dropped here, zeroing it
                // out (C# default) on every cloned option - defeating EV
                // pricing (PlanSolver/RecipeService's
                // ExpectedOutputCount-based math) whenever an account
                // snapshot triggered a Reduce() clone, i.e. the normal
                // own-materials path for a real plan.
                ExpectedOutputCount = option.ExpectedOutputCount,
                Disciplines = new List<string>(option.Disciplines),
                MinRating = option.MinRating,
                Flags = new List<string>(option.Flags),
            };

            foreach (var ingredient in option.Ingredients)
            {
                clone.Ingredients.Add(CloneNode(ingredient));
            }

            return clone;
        }
    }
}
