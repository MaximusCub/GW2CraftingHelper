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

            // REAL coin cost of this decision: what display, PlanStep, and
            // CraftingTreeNode.SubtreeCost show. Never includes a valued
            // currency's coin-equivalent - only the coin actually spent.
            public long? TotalCost;

            // The value used to compare this decision against siblings at
            // the PARENT level: same as TotalCost for TP buys, but for a
            // comparable vendor offer it also folds in valued non-coin
            // currency lines (see EvaluateVendorOffers), and for a craft it
            // is the sum of the chosen recipe's ingredient ComparisonValues
            // (not their TotalCost). Keeping this separate from TotalCost
            // stops a valued vendor offer's currency value from being
            // "laundered" away when an ancestor sums child costs to decide
            // buy vs. craft.
            public long? ComparisonValue;
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
            IReadOnlyDictionary<int, AcquisitionSource> overrides = null,
            CurrencyValuation currencyValuation = null)
        {
            var valuation = currencyValuation ?? CurrencyValuation.None;
            var memo = new Dictionary<int, Decision>();

            // Pre-pass: assign unique NodeIds to every node in the tree.
            // Assignment is deterministic (DFS order), so NodeIds - and any
            // overrides keyed on them - are stable across re-solves of the
            // same tree.
            int nextNodeId = 0;
            AssignNodeIds(tree, ref nextNodeId);

            // Pass 1: decide buy vs craft vs vendor at every node
            Evaluate(tree, prices, vendorOffers, memo, priceBasis, overrides, valuation);

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

        /// <summary>
        /// Evaluates the cheapest acquisition for <paramref name="node"/> and
        /// commits it to <paramref name="memo"/>. Returns the decision's
        /// ComparisonValue (see Decision), NOT its real coin TotalCost -
        /// callers summing ingredient costs for a parent craft are summing
        /// comparison values, which is required for the parent's own
        /// craft-vs-buy comparison to be correct (see Decision.ComparisonValue).
        /// </summary>
        private long? Evaluate(
            RecipeNode node,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            Dictionary<int, Decision> memo,
            PriceBasis priceBasis,
            IReadOnlyDictionary<int, AcquisitionSource> overrides,
            CurrencyValuation currencyValuation)
        {
            if (node.IngredientType == "Currency")
            {
                return null;
            }

            long? buyTotalCost = GetBuyCost(node.Id, node.Quantity, prices, priceBasis);

            // Evaluate vendor offers. Offers costing only coin (directly or via
            // TP-priced item barter) are comparable with TP/craft coin costs and
            // compete in PickCheapest. Offers with non-coin currency lines
            // (karma, essences, ...) are comparable too, but ONLY when every
            // one of those currencies has a user-provided valuation
            // (currencyValuation) - their coin-equivalent (coin part + valued
            // currency lines) is what competes. Offers with any unvalued
            // currency line are NOT comparable with coin - rating them by
            // their coin part alone would make e.g. a 500k-karma offer beat
            // every coin option - and are kept only as a fallback when
            // nothing priceable exists (repo invariant: avoid invalid
            // currency comparisons / never invent exchange rates). Either
            // way, a winning offer's non-coin currency lines are always
            // reported on the plan (VendorCurrencyCosts) - valuation only
            // affects comparison, never the displayed currency cost.
            EvaluateVendorOffers(
                node, prices, vendorOffers, priceBasis, currencyValuation,
                out long? comparableVendorValue,
                out long? comparableVendorCoinCost,
                out List<CostLine> comparableVendorCurrencyCosts,
                out long? fallbackVendorCoinCost,
                out List<CostLine> fallbackVendorCurrencyCosts);

            // Evaluate recipe options (children are always evaluated so their
            // decisions exist even if this node ends up bought).
            long? bestCraftCost = null;
            long? bestCraftRealCost = null;
            int bestRecipeId = 0;

            foreach (var recipe in node.Recipes)
            {
                // craftCost sums ingredient ComparisonValues (Evaluate's
                // return value) and drives recipe selection/PickCheapest.
                // craftRealCost sums the same ingredients' REAL TotalCost
                // (read back from memo, since Evaluate no longer returns it)
                // and becomes the committed decision's real coin cost.
                long craftCost = 0L;
                long craftRealCost = 0L;
                bool allPriceable = true;

                foreach (var ingredient in recipe.Ingredients)
                {
                    if (ingredient.IngredientType == "Currency")
                    {
                        continue;
                    }

                    long? ingredientCost = Evaluate(
                        ingredient, prices, vendorOffers, memo, priceBasis, overrides, currencyValuation);
                    if (!ingredientCost.HasValue)
                    {
                        allPriceable = false;
                        break;
                    }

                    craftCost += ingredientCost.Value;
                    craftRealCost += memo[ingredient.NodeId].TotalCost ?? 0L;
                }

                if (allPriceable)
                {
                    // Cost tie-break: lowest RecipeId, so the choice is
                    // deterministic regardless of recipe list order.
                    if (!bestCraftCost.HasValue ||
                        craftCost < bestCraftCost.Value ||
                        (craftCost == bestCraftCost.Value && recipe.RecipeId < bestRecipeId))
                    {
                        bestCraftCost = craftCost;
                        bestCraftRealCost = craftRealCost;
                        bestRecipeId = recipe.RecipeId;
                    }
                }
                else if (!bestCraftCost.HasValue && bestRecipeId == 0)
                {
                    bestRecipeId = recipe.RecipeId;
                }
            }

            // Craft is only a FORCEABLE path when its cost is fully
            // priceable. bestRecipeId alone also covers the unpriceable
            // last-resort recipe, and forcing that would commit a null cost
            // whose branch silently drops out of the plan total (misleading
            // pricing). The last-resort auto path below is unaffected: it
            // only fires when nothing else is priceable at all.
            bool canCraft = bestCraftCost.HasValue;
            bool canBuyTp = buyTotalCost.HasValue;
            bool canBuyVendor = comparableVendorValue.HasValue ||
                                fallbackVendorCoinCost.HasValue;

            // cost = real coin (Decision.TotalCost / display); comparisonValue
            // = parent-comparison value (Decision.ComparisonValue). Commit
            // returns comparisonValue - see Decision.ComparisonValue and the
            // Evaluate summary doc for why the two must stay separate.
            long? Commit(
                AcquisitionSource src, long? cost, long? comparisonValue,
                int recipeId, List<CostLine> vendorCurrencyCosts)
            {
                memo[node.NodeId] = new Decision
                {
                    Source = src,
                    TotalCost = cost,
                    ComparisonValue = comparisonValue,
                    RecipeId = recipeId,
                    VendorCurrencyCosts = vendorCurrencyCosts,
                    CanCraft = canCraft,
                    CanBuyTp = canBuyTp,
                    CanBuyVendor = canBuyVendor
                };
                return comparisonValue;
            }

            // A user override wins whenever it is feasible for this node;
            // infeasible overrides are ignored and the best path applies.
            if (overrides != null &&
                overrides.TryGetValue(node.NodeId, out var forced))
            {
                if (forced == AcquisitionSource.Craft && canCraft)
                {
                    return Commit(AcquisitionSource.Craft, bestCraftRealCost, bestCraftCost, bestRecipeId, null);
                }
                if (forced == AcquisitionSource.BuyFromTp && canBuyTp)
                {
                    return Commit(AcquisitionSource.BuyFromTp, buyTotalCost, buyTotalCost, 0, null);
                }
                if (forced == AcquisitionSource.BuyFromVendor && canBuyVendor)
                {
                    return comparableVendorValue.HasValue
                        ? Commit(AcquisitionSource.BuyFromVendor, comparableVendorCoinCost, comparableVendorValue, 0, comparableVendorCurrencyCosts)
                        : Commit(AcquisitionSource.BuyFromVendor, fallbackVendorCoinCost, fallbackVendorCoinCost, 0, fallbackVendorCurrencyCosts);
                }
            }

            // Three-way comparison: vendor (coin part + any valued currency
            // lines) vs TP buy vs craft
            var source = PickCheapest(buyTotalCost, bestCraftCost, comparableVendorValue);

            if (source == AcquisitionSource.BuyFromVendor)
            {
                return Commit(AcquisitionSource.BuyFromVendor, comparableVendorCoinCost, comparableVendorValue, 0, comparableVendorCurrencyCosts);
            }

            if (source == AcquisitionSource.BuyFromTp)
            {
                return Commit(AcquisitionSource.BuyFromTp, buyTotalCost, buyTotalCost, 0, null);
            }

            if (source == AcquisitionSource.Craft)
            {
                return Commit(AcquisitionSource.Craft, bestCraftRealCost, bestCraftCost, bestRecipeId, null);
            }

            // Fallback order when nothing is coin-priceable: a mixed-currency
            // vendor offer is a concrete, fully-known acquisition and is
            // preferred over descending into an unpriceable craft subtree.
            if (fallbackVendorCoinCost.HasValue)
            {
                return Commit(AcquisitionSource.BuyFromVendor, fallbackVendorCoinCost, fallbackVendorCoinCost, 0, fallbackVendorCurrencyCosts);
            }

            if (bestRecipeId != 0)
            {
                return Commit(AcquisitionSource.Craft, bestCraftRealCost, bestCraftCost, bestRecipeId, null);
            }

            return Commit(AcquisitionSource.UnknownSource, null, null, 0, null);
        }

        /// <summary>
        /// Splits vendor offers into two tiers. An offer is COMPARABLE (competes
        /// with TP/craft coin costs in PickCheapest) when it has no non-coin
        /// currency lines at all, OR every one of its non-coin currency lines
        /// has a user-provided valuation (<paramref name="currencyValuation"/>):
        /// its comparison value is coin part + sum(count * copperPerUnit) over
        /// those valued lines, reported via <paramref name="bestComparableValue"/>.
        /// The winning comparable offer's real coin part and (if any) currency
        /// lines are reported separately via <paramref name="bestComparableCoinCost"/>
        /// and <paramref name="bestComparableCurrencyCosts"/> - the valuation
        /// affects comparison only, never the amounts committed to the plan.
        /// An offer with at least one non-coin currency line that has NO
        /// valuation (including when it is mixed with other, valued lines) is
        /// incomparable with coin costs and reported only as a FALLBACK,
        /// ranked by lowest coin part. A fallback coin-part tie is broken by
        /// unit count only when both offers cost the same single currency (a
        /// genuine like-for-like comparison); ties across different currencies
        /// keep the first-listed offer, because ranking across currencies has
        /// no exchange rate and unit counts of different currencies must never
        /// be compared.
        ///
        /// V1 purchase-cap semantics: an offer with a positive DailyCap (or a
        /// positive WeeklyCap when DailyCap is absent/zero) that cannot supply
        /// the node's needed quantity within a single cap period - i.e. the
        /// node needs more purchases than the cap allows - is excluded from
        /// this node's evaluation entirely, from both the comparable and the
        /// fallback tier. Zero or absent caps mean uncapped, matching most
        /// offers. Non-goal: this does not split a node's need across a capped
        /// offer plus a second source once the cap is exhausted - a node is
        /// still sourced from exactly one acquisition (partial cap-split
        /// sourcing is left for a future milestone).
        /// </summary>
        private static void EvaluateVendorOffers(
            RecipeNode node,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            PriceBasis priceBasis,
            CurrencyValuation currencyValuation,
            out long? bestComparableValue,
            out long? bestComparableCoinCost,
            out List<CostLine> bestComparableCurrencyCosts,
            out long? fallbackCoinCost,
            out List<CostLine> fallbackCurrencyCosts)
        {
            bestComparableValue = null;
            bestComparableCoinCost = null;
            bestComparableCurrencyCosts = null;
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

                // Purchase cap (see V1 semantics above): DailyCap wins when
                // positive, else WeeklyCap when positive, else the offer is
                // uncapped. If the node needs more purchases than fit in one
                // cap period, the offer cannot fully supply this node and is
                // excluded from both tiers below.
                int? purchaseCap = offer.DailyCap.HasValue && offer.DailyCap.Value > 0
                    ? offer.DailyCap
                    : (offer.WeeklyCap.HasValue && offer.WeeklyCap.Value > 0 ? offer.WeeklyCap : null);
                if (purchaseCap.HasValue && unitsNeeded > purchaseCap.Value)
                {
                    continue;
                }

                long totalCoinCost = coinCost * unitsNeeded;

                // Scale and value the non-coin currency lines (no-op for a
                // pure-coin offer, which has none). allValued stays
                // vacuously true when there are no non-coin lines, so a
                // pure-coin offer always lands in the comparable branch
                // below with valuationCopper == 0 - unchanged from before.
                List<CostLine> scaledCurrencyCosts = null;
                long totalCurrencyUnits = 0;
                long valuationCopper = 0;
                bool scalable = true;
                bool allValued = true;

                if (currencyCosts.Count > 0)
                {
                    scaledCurrencyCosts = new List<CostLine>(currencyCosts.Count);
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

                        if (allValued)
                        {
                            if (currencyValuation != null &&
                                currencyValuation.TryGetCopperValue(cc.Id, out long copperPerUnit))
                            {
                                try
                                {
                                    valuationCopper = checked(valuationCopper + (scaled * copperPerUnit));
                                }
                                catch (OverflowException)
                                {
                                    // Absurd valuation input; fall back rather
                                    // than crash or silently misrank offers.
                                    allValued = false;
                                }
                            }
                            else
                            {
                                allValued = false;
                            }
                        }
                    }
                }

                if (!scalable)
                {
                    continue;
                }

                if (allValued)
                {
                    long comparisonValue;
                    try
                    {
                        comparisonValue = checked(totalCoinCost + valuationCopper);
                    }
                    catch (OverflowException)
                    {
                        continue;
                    }

                    if (!bestComparableValue.HasValue ||
                        comparisonValue < bestComparableValue.Value)
                    {
                        bestComparableValue = comparisonValue;
                        bestComparableCoinCost = totalCoinCost;
                        bestComparableCurrencyCosts = scaledCurrencyCosts;
                    }
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
        internal static int GetUnitPrice(ItemPrice price, PriceBasis priceBasis)
        {
            return priceBasis == PriceBasis.BuyOrder
                ? price.SellInstant
                : price.BuyInstant;
        }
    }
}
