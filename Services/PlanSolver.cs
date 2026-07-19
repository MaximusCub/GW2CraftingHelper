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
            public List<CostLine> VendorCurrencyCosts;
            public bool CanCraft;
            public bool CanBuyTp;
            public bool CanBuyVendor;
        }

        public SolveResult Solve(RecipeNode tree, IReadOnlyDictionary<int, ItemPrice> prices)
        {
            return Solve(tree, prices, null);
        }

        public SolveResult Solve(
            RecipeNode tree,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            PriceBasis priceBasis = PriceBasis.InstantBuy,
            IReadOnlyDictionary<int, AcquisitionSource> overrides = null)
        {
            var memo = new Dictionary<int, Decision>();

            // Pre-pass: assign unique NodeIds to every node in the tree.
            // Assignment is deterministic (DFS order), so NodeIds - and any
            // overrides keyed on them - are stable across re-solves of the
            // same tree.
            int nextNodeId = 0;
            AssignNodeIds(tree, ref nextNodeId);

            // Pass 1: decide buy vs craft vs vendor at every node
            Evaluate(tree, prices, vendorOffers, memo, priceBasis, overrides);

            // Pass 2: collect steps and currency costs following pass-1 decisions
            var stepMap = new Dictionary<(int, AcquisitionSource, int), PlanStep>();
            var currencyMap = new Dictionary<int, long>();
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
                if (step.Source == AcquisitionSource.BuyFromTp ||
                    step.Source == AcquisitionSource.BuyFromVendor)
                {
                    totalCoinCost += step.TotalCost;
                }
            }

            var currencyCosts = new List<CurrencyCost>();
            foreach (var kvp in currencyMap)
            {
                currencyCosts.Add(new CurrencyCost { CurrencyId = kvp.Key, Amount = checked(kvp.Value) });
            }

            var plan = new CraftingPlan
            {
                TargetItemId = tree.Id,
                TargetQuantity = tree.Quantity,
                Steps = steps,
                TotalCoinCost = totalCoinCost,
                CurrencyCosts = currencyCosts
            };

            // Convert internal memo to public decisions dict
            var decisions = new Dictionary<int, SolverDecision>(memo.Count);
            foreach (var kvp in memo)
            {
                decisions[kvp.Key] = new SolverDecision
                {
                    Source = kvp.Value.Source,
                    RecipeId = kvp.Value.RecipeId,
                    TotalCost = kvp.Value.TotalCost,
                    CanCraft = kvp.Value.CanCraft,
                    CanBuyTp = kvp.Value.CanBuyTp,
                    CanBuyVendor = kvp.Value.CanBuyVendor
                };
            }

            return new SolveResult
            {
                Plan = plan,
                Decisions = decisions
            };
        }

        private long? Evaluate(
            RecipeNode node,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            Dictionary<int, Decision> memo,
            PriceBasis priceBasis,
            IReadOnlyDictionary<int, AcquisitionSource> overrides)
        {
            if (node.IngredientType == "Currency")
            {
                return null;
            }

            long? buyTotalCost = GetBuyCost(node.Id, node.Quantity, prices, priceBasis);

            // Evaluate vendor offers. Offers costing only coin (directly or via
            // TP-priced item barter) are comparable with TP/craft coin costs and
            // compete in PickCheapest. Offers with non-coin currency lines (karma,
            // essences, ...) are NOT comparable with coin - rating them by their
            // coin part alone would make e.g. a 500k-karma offer beat every coin
            // option. They are kept only as a fallback when nothing priceable
            // exists (repo invariant: avoid invalid currency comparisons).
            EvaluateVendorOffers(
                node, prices, vendorOffers, priceBasis,
                out long? comparableVendorCoinCost,
                out long? fallbackVendorCoinCost,
                out List<CostLine> fallbackVendorCurrencyCosts);

            // Evaluate recipe options (children are always evaluated so their
            // decisions exist even if this node ends up bought).
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

                    long? ingredientCost = Evaluate(
                        ingredient, prices, vendorOffers, memo, priceBasis, overrides);
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
                    bestRecipeId = recipe.RecipeId;
                }
            }

            bool canCraft = bestRecipeId != 0;
            bool canBuyTp = buyTotalCost.HasValue;
            bool canBuyVendor = comparableVendorCoinCost.HasValue ||
                                fallbackVendorCoinCost.HasValue;

            long? Commit(AcquisitionSource src, long? cost, int recipeId, List<CostLine> vendorCurrencyCosts)
            {
                memo[node.NodeId] = new Decision
                {
                    Source = src,
                    TotalCost = cost,
                    RecipeId = recipeId,
                    VendorCurrencyCosts = vendorCurrencyCosts,
                    CanCraft = canCraft,
                    CanBuyTp = canBuyTp,
                    CanBuyVendor = canBuyVendor
                };
                return cost;
            }

            // A user override wins whenever it is feasible for this node;
            // infeasible overrides are ignored and the best path applies.
            if (overrides != null &&
                overrides.TryGetValue(node.NodeId, out var forced))
            {
                if (forced == AcquisitionSource.Craft && canCraft)
                {
                    return Commit(AcquisitionSource.Craft, bestCraftCost, bestRecipeId, null);
                }
                if (forced == AcquisitionSource.BuyFromTp && canBuyTp)
                {
                    return Commit(AcquisitionSource.BuyFromTp, buyTotalCost, 0, null);
                }
                if (forced == AcquisitionSource.BuyFromVendor && canBuyVendor)
                {
                    return comparableVendorCoinCost.HasValue
                        ? Commit(AcquisitionSource.BuyFromVendor, comparableVendorCoinCost, 0, null)
                        : Commit(AcquisitionSource.BuyFromVendor, fallbackVendorCoinCost, 0, fallbackVendorCurrencyCosts);
                }
            }

            // Three-way comparison: vendor (pure coin) vs TP buy vs craft
            var source = PickCheapest(buyTotalCost, bestCraftCost, comparableVendorCoinCost);

            if (source == AcquisitionSource.BuyFromVendor)
            {
                return Commit(AcquisitionSource.BuyFromVendor, comparableVendorCoinCost, 0, null);
            }

            if (source == AcquisitionSource.BuyFromTp)
            {
                return Commit(AcquisitionSource.BuyFromTp, buyTotalCost, 0, null);
            }

            if (source == AcquisitionSource.Craft)
            {
                return Commit(AcquisitionSource.Craft, bestCraftCost, bestRecipeId, null);
            }

            // Fallback order when nothing is coin-priceable: a mixed-currency
            // vendor offer is a concrete, fully-known acquisition and is
            // preferred over descending into an unpriceable craft subtree.
            if (fallbackVendorCoinCost.HasValue)
            {
                return Commit(AcquisitionSource.BuyFromVendor, fallbackVendorCoinCost, 0, fallbackVendorCurrencyCosts);
            }

            if (bestRecipeId != 0)
            {
                return Commit(AcquisitionSource.Craft, bestCraftCost, bestRecipeId, null);
            }

            return Commit(AcquisitionSource.UnknownSource, null, 0, null);
        }

        /// <summary>
        /// Splits vendor offers into two tiers. Pure-coin offers (coin and/or
        /// TP-priceable item barter only) are cost-comparable with TP/craft and
        /// reported via <paramref name="bestComparableCoinCost"/>. Offers with
        /// non-coin currency lines are incomparable with coin costs and reported
        /// only as a fallback, ranked by lowest coin part. A coin-part tie is
        /// broken by unit count only when both offers cost the same single
        /// currency (a genuine like-for-like comparison); ties across different
        /// currencies keep the first-listed offer, because ranking across
        /// currencies has no exchange rate and unit counts of different
        /// currencies must never be compared.
        /// </summary>
        private static void EvaluateVendorOffers(
            RecipeNode node,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            PriceBasis priceBasis,
            out long? bestComparableCoinCost,
            out long? fallbackCoinCost,
            out List<CostLine> fallbackCurrencyCosts)
        {
            bestComparableCoinCost = null;
            fallbackCoinCost = null;
            fallbackCurrencyCosts = null;
            long fallbackCurrencyUnits = 0;
            int fallbackSingleCurrencyId = -1;

            if (vendorOffers == null ||
                !vendorOffers.TryGetValue(node.Id, out var offers))
            {
                return;
            }

            foreach (var offer in offers)
            {
                if (offer.OutputCount <= 0)
                {
                    continue;
                }

                long coinCost = 0;
                bool priceable = true;
                var currencyCosts = new List<CostLine>();

                foreach (var cost in offer.CostLines ?? Enumerable.Empty<CostLine>())
                {
                    if (string.Equals(cost.Type, "Currency", StringComparison.Ordinal))
                    {
                        if (cost.Id == Gw2Constants.CoinCurrencyId)
                        {
                            coinCost += (long)cost.Count;
                        }
                        else
                        {
                            currencyCosts.Add(cost);
                        }
                    }
                    else if (string.Equals(cost.Type, "Item", StringComparison.Ordinal))
                    {
                        int unitPrice = prices.TryGetValue(cost.Id, out var itemPrice)
                            ? GetUnitPrice(itemPrice, priceBasis)
                            : 0;
                        if (unitPrice > 0)
                        {
                            coinCost += (long)cost.Count * unitPrice;
                        }
                        else
                        {
                            priceable = false;
                            break;
                        }
                    }
                }

                if (!priceable)
                {
                    continue;
                }

                int unitsNeeded = (int)Math.Ceiling((double)node.Quantity / offer.OutputCount);
                long totalCoinCost = coinCost * unitsNeeded;

                if (currencyCosts.Count == 0)
                {
                    if (!bestComparableCoinCost.HasValue ||
                        totalCoinCost < bestComparableCoinCost.Value)
                    {
                        bestComparableCoinCost = totalCoinCost;
                    }
                    continue;
                }

                var scaledCurrencyCosts = new List<CostLine>();
                long totalCurrencyUnits = 0;
                bool scalable = true;
                foreach (var cc in currencyCosts)
                {
                    long scaled = (long)cc.Count * unitsNeeded;
                    if (scaled > int.MaxValue)
                    {
                        // A quantity this large cannot be represented in a
                        // CostLine; skip the offer rather than crash the solve.
                        scalable = false;
                        break;
                    }
                    totalCurrencyUnits += scaled;
                    scaledCurrencyCosts.Add(new CostLine
                    {
                        Type = cc.Type,
                        Id = cc.Id,
                        Count = (int)scaled
                    });
                }

                if (!scalable)
                {
                    continue;
                }

                // The offer's single currency id, or -1 when it spans several
                // currencies (unit counts are then never compared).
                int singleCurrencyId = currencyCosts.Count == 1 ? currencyCosts[0].Id : -1;

                bool better =
                    !fallbackCoinCost.HasValue ||
                    totalCoinCost < fallbackCoinCost.Value ||
                    (totalCoinCost == fallbackCoinCost.Value &&
                     singleCurrencyId != -1 &&
                     singleCurrencyId == fallbackSingleCurrencyId &&
                     totalCurrencyUnits < fallbackCurrencyUnits);

                if (better)
                {
                    fallbackCoinCost = totalCoinCost;
                    fallbackCurrencyCosts = scaledCurrencyCosts;
                    fallbackCurrencyUnits = totalCurrencyUnits;
                    fallbackSingleCurrencyId = singleCurrencyId;
                }
            }
        }

        /// <summary>
        /// Pick cheapest among TP buy, craft, and vendor (by coin cost).
        /// Ties: BuyFromVendor beats BuyFromTp beats Craft.
        /// Returns UnknownSource if none are available.
        /// </summary>
        private static AcquisitionSource PickCheapest(
            long? buyCost, long? craftCost, long? vendorCost)
        {
            long? best = null;
            var source = AcquisitionSource.UnknownSource;

            if (vendorCost.HasValue)
            {
                best = vendorCost.Value;
                source = AcquisitionSource.BuyFromVendor;
            }

            if (buyCost.HasValue)
            {
                if (!best.HasValue || buyCost.Value < best.Value)
                {
                    best = buyCost.Value;
                    source = AcquisitionSource.BuyFromTp;
                }
            }

            if (craftCost.HasValue)
            {
                if (!best.HasValue || craftCost.Value < best.Value)
                {
                    best = craftCost.Value;
                    source = AcquisitionSource.Craft;
                }
            }

            return source;
        }

        private void Collect(
            RecipeNode node,
            Dictionary<int, Decision> memo,
            Dictionary<(int, AcquisitionSource, int), PlanStep> stepMap,
            Dictionary<int, long> currencyMap,
            Dictionary<(int, int), int> craftOrder,
            ref int craftCounter)
        {
            if (node.IngredientType == "Currency")
            {
                if (currencyMap.ContainsKey(node.Id))
                {
                    currencyMap[node.Id] = checked(currencyMap[node.Id] + node.Quantity);
                }
                else
                {
                    currencyMap[node.Id] = node.Quantity;
                }
                return;
            }

            if (!memo.TryGetValue(node.NodeId, out var decision))
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
            else if (decision.Source == AcquisitionSource.BuyFromVendor)
            {
                // Add vendor currency costs to the currency map
                if (decision.VendorCurrencyCosts != null)
                {
                    foreach (var cc in decision.VendorCurrencyCosts)
                    {
                        if (currencyMap.ContainsKey(cc.Id))
                        {
                            currencyMap[cc.Id] = checked(currencyMap[cc.Id] + cc.Count);
                        }
                        else
                        {
                            currencyMap[cc.Id] = cc.Count;
                        }
                    }
                }

                var stepKey = (node.Id, AcquisitionSource.BuyFromVendor, 0);
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
                if ((existing.Source == AcquisitionSource.BuyFromTp ||
                     existing.Source == AcquisitionSource.BuyFromVendor) &&
                    existing.Quantity > 0)
                {
                    existing.UnitCost = existing.TotalCost / existing.Quantity;
                }
            }
            else
            {
                long unitCost = 0;
                if ((decision.Source == AcquisitionSource.BuyFromTp ||
                     decision.Source == AcquisitionSource.BuyFromVendor) &&
                    node.Quantity > 0 && decision.TotalCost.HasValue)
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

        private static void AssignNodeIds(RecipeNode node, ref int nextNodeId)
        {
            node.NodeId = nextNodeId++;
            foreach (var recipe in node.Recipes)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    AssignNodeIds(ingredient, ref nextNodeId);
                }
            }
        }

        private long? GetBuyCost(
            int itemId, int quantity,
            IReadOnlyDictionary<int, ItemPrice> prices,
            PriceBasis priceBasis)
        {
            if (prices.TryGetValue(itemId, out var price))
            {
                int unitPrice = GetUnitPrice(price, priceBasis);
                if (unitPrice > 0)
                {
                    return (long)quantity * unitPrice;
                }
            }
            return null;
        }

        /// <summary>
        /// Unit acquisition cost under the chosen basis: lowest sell listing
        /// (instant) or highest buy order (patient). 0 = not priceable.
        /// </summary>
        private static int GetUnitPrice(ItemPrice price, PriceBasis priceBasis)
        {
            return priceBasis == PriceBasis.BuyOrder
                ? price.SellInstant
                : price.BuyInstant;
        }
    }
}
