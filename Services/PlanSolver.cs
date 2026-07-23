using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// See docs/ARCHITECTURE.md section 8 (solver decision rules: TP-buy
    /// baseline, strict-cheaper craft/vendor comparisons, Mystic Clover EV
    /// pricing, force-craft) for the durable rationale (M38 WP-27).
    /// </summary>
    public class PlanSolver
    {
        // WP-15 (architecture S4a): the vendor-batching sub-engine (batch
        // state types, EvaluateVendorOffers, FinalizeVendorBatches,
        // AllocateVendorNodeCosts, MergeVendorCurrencyCosts,
        // VendorBatchesEqual, ScaleCostLines) now lives in the injected
        // VendorBatchSolver collaborator (Services/VendorBatchSolver.cs)
        // instead of this class's own private statics. Pure move - the
        // merged-ceil arithmetic itself is DO-NOT-TOUCH
        // (m38-cleanup-plan.md #7) and is unchanged; only the call shape
        // moved. The parameterless constructor keeps every existing
        // `new PlanSolver()` call site unchanged.
        private readonly VendorBatchSolver _vendorBatchSolver;

        public PlanSolver()
            : this(new VendorBatchSolver())
        {
        }

        public PlanSolver(VendorBatchSolver vendorBatchSolver)
        {
            _vendorBatchSolver = vendorBatchSolver ?? new VendorBatchSolver();
        }

        // Was `private` - now `internal` (WP-15) so VendorBatchSolver's
        // AllocateVendorNodeCosts (a different class, same assembly) can
        // read/write a Decision by NodeId. No wider than that: still not
        // visible outside this assembly.
        internal struct Decision
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
            // is the sum of the chosen recipe's non-currency ingredient
            // ComparisonValues PLUS any valued Currency ingredient of that
            // same recipe (see the currency branch in Evaluate's recipe
            // loop) - never their TotalCost. Keeping this separate from
            // TotalCost stops a valued vendor offer's or currency
            // ingredient's coin-equivalent value from being "laundered"
            // away when an ancestor sums child costs to decide buy vs.
            // craft.
            public long? ComparisonValue;
            public int RecipeId;
            public List<CostLine> VendorCurrencyCosts;
            public bool CanCraft;
            public bool CanBuyTp;
            public bool CanBuyVendor;

            // Winning vendor offer's batch shape (Source == BuyFromVendor
            // only, null otherwise): the offer's own OutputCount and its
            // UNSCALED per-batch coin/currency cost (one purchase, before
            // this node's own occurrence-local unitsNeeded scaling). Carried
            // so Collect/AggregateStep/FinalizeVendorBatches can re-derive a
            // merged step's true cost from AGGREGATE demand and ceil once
            // (M34-B1 #1 - gw2e parity), instead of trusting the sum of
            // several already-independently-ceil'd per-occurrence costs.
            public VendorBatchSolver.VendorOfferBatch? VendorBatch;
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
            CurrencyValuation currencyValuation = null,
            // M34-B2a #3 (gw2e "Value Own Materials" force-buy pre-pass):
            // nodes in this set have craft excluded from the AUTOMATIC
            // buy-vs-craft-vs-vendor comparison for this solve (buying
            // outright beats crafting fresh components by gw2e's 15%
            // margin - see OwnedMaterialsForceBuyPrePass). A manual
            // per-node override in `overrides` still wins over this set,
            // same as gw2e's own manual craft/buy pill always overriding
            // its automatic pre-pass (docs/gw2e-parity-spec.md /
            // m34-r2-gw2e-owned-materials.md Section 3.2).
            ISet<int> forceBuyOnlyNodeIds = null,
            // M34-B2a #3: when non-null, populated with this node's raw
            // (buyCost, craftCost) - the SAME numbers Evaluate already
            // computes for every "Item" node regardless of decision - so a
            // caller (OwnedMaterialsForceBuyPrePass) can apply gw2e's exact
            // buyPrice &lt; craftDecisionPrice * 0.85 rule without
            // duplicating this method's cost-aggregation logic. Never
            // affects this solve's own Decisions/Plan.
            Dictionary<int, (long? BuyCost, long? CraftCost)> costDiagnostics = null,
            // M34-B2a #3: when false, this tree's existing node.NodeId
            // values are trusted as-is instead of being reassigned from
            // scratch - see RecipeNodeIds' doc comment for why a caller
            // (CraftingPlanPipeline, when the force-buy pre-pass is active)
            // needs this: the tree's ids were pre-assigned, and survived
            // pruning via InventoryReducer.CloneNode's NodeId preservation,
            // BEFORE this Solve() call, and must not be renumbered out from
            // under the pre-pass's own already-computed forceBuyOnlyNodeIds
            // set. Every other caller keeps the default (true), unchanged
            // from this method's original always-reassign behavior.
            bool assignNodeIds = true,
            // M34-B2b (gw2e "Ignore" pill): item ids in this set are
            // treated as fully in-hand tree-wide for THIS solve only -
            // every occurrence contributes zero cost, generates no
            // crafting step or shopping row, and does not recurse into its
            // own recipe's ingredients (matching gw2e's usedQuantity == 0
            // => free/no-step rule - see m34-r2-gw2e-owned-materials.md
            // Section 2.1/5). Unlike `overrides` (a per-NodeId craft/buy
            // choice), this is per-ItemId, matching gw2e's own "Ignore
            // marks every occurrence of that item id, tree-wide" semantics.
            // Null (the default) behaves exactly as before this feature.
            ISet<int> ignoredItemIds = null,
            // M37 (KNOWN-ISSUES #24, gw2e parity): per-material Homestead
            // Refinement efficiency tier configuration. Null (the default)
            // behaves as HomesteadEfficiencyTiers.Default - tier 0 for
            // every material, gw2e's own default - so every existing
            // caller that doesn't know about this setting keeps excluding
            // every Homestead Refinement offer above tier 0, exactly
            // matching the live-defect fix's intended default behavior.
            HomesteadEfficiencyTiers homesteadTiers = null)
        {
            var valuation = currencyValuation ?? CurrencyValuation.None;
            var tiers = homesteadTiers ?? HomesteadEfficiencyTiers.Default;
            var memo = new Dictionary<int, Decision>();

            // Pre-pass: assign unique NodeIds to every node in the tree.
            // Assignment is deterministic (DFS order), so NodeIds - and any
            // overrides keyed on them - are stable across re-solves of the
            // same tree.
            if (assignNodeIds)
            {
                RecipeNodeIds.Assign(tree);
            }

            // Pass 1: decide buy vs craft vs vendor at every node
            Evaluate(tree, prices, vendorOffers, memo, priceBasis, overrides, valuation, forceBuyOnlyNodeIds, costDiagnostics, ignoredItemIds, tiers);

            // Pass 2: collect steps and currency costs following pass-1 decisions
            var stepMap = new Dictionary<(int, AcquisitionSource, int), PlanStep>();
            var currencyMap = new Dictionary<int, long>();
            var craftOrder = new Dictionary<(int, int), int>();
            var vendorBatchTracking = new Dictionary<(int, AcquisitionSource, int), VendorBatchSolver.VendorBatchState>();
            var vendorOccurrences = new Dictionary<(int, AcquisitionSource, int), List<(int NodeId, int Quantity)>>();
            // M34 fix (wave-validator finding, post-fcbb277): every tree
            // occurrence's own NodeId that fed a merged Craft-type stepKey,
            // in first-seen (DFS) order - the Craft-side twin of
            // vendorOccurrences above, needed for the same reason: a Craft
            // PlanStep's TotalCost is Collect()'s running sum of
            // decision.TotalCost across every occurrence of that
            // (ItemId, RecipeId) craft, taken BEFORE the correction passes
            // below ever run (see RefreshCraftStepCosts's doc comment).
            var craftOccurrences = new Dictionary<(int, AcquisitionSource, int), List<int>>();
            int craftCounter = 0;

            Collect(tree, memo, stepMap, currencyMap, craftOrder, vendorBatchTracking, vendorOccurrences, craftOccurrences, ref craftCounter, ignoredItemIds);

            // Pass 2b (M34-B1 #1/#3): re-derive each merged vendor step's
            // true cost from its AGGREGATE Quantity and the winning offer's
            // batch shape, ceiling once instead of trusting the sum of
            // several already-per-occurrence-ceil'd costs; also folds the
            // (now-correct) vendor currency costs into currencyMap and
            // collects any post-solve "timegated" (cap-exceeded) notices.
            var timegatedItems = _vendorBatchSolver.FinalizeVendorBatches(stepMap, vendorBatchTracking, currencyMap);

            // Pass 2c (M34 fix - Critical review finding): FinalizeVendorBatches
            // only corrects the MERGED PlanStep/currencyMap view; it never
            // touches `memo`, which is what the public Decisions dict (and,
            // via CraftingTreeBuilder, every CraftingTreeNode.SubtreeCost -
            // including the root row) is built from below. Without this,
            // the Recipe Tree's own displayed totals kept showing the stale,
            // per-occurrence-overcounted sum while the Total Cost summary
            // (plan.TotalCoinCost, built from the corrected stepMap) showed
            // the right number - the two sections of the same page
            // disagreeing by exactly the rounding waste this fix eliminates.
            // Re-derives each corrected vendor step's true per-occurrence
            // share (AllocateVendorNodeCosts), then re-sums every Craft
            // ancestor bottom-up from those corrected leaf values
            // (RecomputeCraftCosts) so the correction propagates all the way
            // to the root, exactly mirroring Evaluate's own bottom-up
            // craftRealCost aggregation. RecomputeCraftCosts itself has no
            // depth bound - it walks the ENTIRE chosen-path tree from
            // `tree` down, so `memo`/Decisions/SubtreeCost are already
            // fully correct at every level after this line, however deep.
            _vendorBatchSolver.AllocateVendorNodeCosts(stepMap, vendorOccurrences, memo);
            RecomputeCraftCosts(tree, memo, ignoredItemIds);

            // Pass 2d (M34 fix - wave-validator finding): stepMap's
            // Craft-type PlanStep entries are NOT touched by anything
            // above - AggregateStep (inside Collect, pass 2) summed
            // decision.TotalCost across occurrences BEFORE this line ever
            // ran, so every Craft row of the "Crafting Steps" shopping
            // list would otherwise permanently show the stale
            // pre-correction total even though `memo`/the Recipe Tree
            // (just corrected above) and plan.TotalCoinCost (summed from
            // FinalizeVendorBatches' already-corrected vendor/TP steps
            // below) both show the right number - see
            // RefreshCraftStepCosts's doc comment for why a full
            // restructure to avoid this snapshot-then-correct ordering
            // entirely was assessed and rejected in favor of this
            // targeted refresh.
            RefreshCraftStepCosts(stepMap, craftOccurrences, memo);

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
                CurrencyCosts = currencyCosts,
                TimegatedItems = timegatedItems
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
                    VendorCurrencyCosts = kvp.Value.VendorCurrencyCosts,
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
        /// EVERY non-currency ingredient of EVERY recipe on this node is
        /// evaluated (and therefore gets its own memo entry) regardless of
        /// whether this node ends up bought, crafted via a different
        /// recipe, or unpriceable itself - see the recipe loop below.
        /// </summary>
        private long? Evaluate(
            RecipeNode node,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            Dictionary<int, Decision> memo,
            PriceBasis priceBasis,
            IReadOnlyDictionary<int, AcquisitionSource> overrides,
            CurrencyValuation currencyValuation,
            ISet<int> forceBuyOnlyNodeIds = null,
            Dictionary<int, (long? BuyCost, long? CraftCost)> costDiagnostics = null,
            ISet<int> ignoredItemIds = null,
            HomesteadEfficiencyTiers homesteadTiers = null)
        {
            if (node.IngredientType == "Currency")
            {
                return null;
            }

            var tiers = homesteadTiers ?? HomesteadEfficiencyTiers.Default;

            // M34-B2b: an "Ignore"-d item id is treated as fully in-hand for
            // THIS node - zero cost, no recipe/vendor/TP evaluation, and (by
            // never recursing into node.Recipes here) no draw on this
            // node's own ingredients either, matching gw2e's "an un-crafted
            // branch never asks for its ingredients" rule (Section 1.3 of
            // the r2 report) applied to the synthetic fully-owned case.
            // CanCraft/CanBuyTp/CanBuyVendor are left false: this node's own
            // real feasibility is irrelevant once ignored, and
            // CraftingTreeBuilder never reads them for an ignored node
            // anyway (it short-circuits to Have, same as Quantity == 0).
            if (ignoredItemIds != null && ignoredItemIds.Contains(node.Id))
            {
                memo[node.NodeId] = new Decision
                {
                    Source = AcquisitionSource.UnknownSource,
                    TotalCost = 0L,
                    ComparisonValue = 0L,
                    RecipeId = 0,
                    VendorCurrencyCosts = null,
                    CanCraft = false,
                    CanBuyTp = false,
                    CanBuyVendor = false,
                    VendorBatch = null
                };
                return 0L;
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
            var vendorEvaluation = _vendorBatchSolver.EvaluateVendorOffers(
                node, prices, vendorOffers, priceBasis, currencyValuation, tiers);
            long? comparableVendorValue = vendorEvaluation.BestComparableValue;
            long? comparableVendorCoinCost = vendorEvaluation.BestComparableCoinCost;
            List<CostLine> comparableVendorCurrencyCosts = vendorEvaluation.BestComparableCurrencyCosts;
            VendorBatchSolver.VendorOfferBatch? comparableVendorBatch = vendorEvaluation.BestComparableBatch;
            long? fallbackVendorCoinCost = vendorEvaluation.FallbackCoinCost;
            List<CostLine> fallbackVendorCurrencyCosts = vendorEvaluation.FallbackCurrencyCosts;
            VendorBatchSolver.VendorOfferBatch? fallbackVendorBatch = vendorEvaluation.FallbackBatch;

            // Evaluate recipe options. EVERY non-currency ingredient of
            // EVERY recipe is always evaluated (M33 Finding 1 fix) - no
            // short-circuit on the first unpriceable ingredient - so every
            // node in the tree always gets a memo/decision entry, even deep
            // under a recipe this node ultimately doesn't choose.
            //
            // An unpriceable ingredient no longer disqualifies its recipe
            // (M33 partial-pricing parity, echoing gw2e's craftPrice =
            // sum(component.craftResultPrice || 0)): it contributes ZERO to
            // this recipe's craft cost instead, so craftCost/craftRealCost
            // are always defined whenever node.Recipes is non-empty (gw2e's
            // "hasComponents"). This intentionally makes coin totals
            // partial when a descendant is genuinely unpriceable - the
            // descendant still surfaces as its own (Unknown or
            // force-crafted) node with its own decision.
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
                //
                // EV pricing (Mystic Clover-style fractional MF recipes,
                // M33 item 7 - CORRECTED): this recipe's ingredient
                // quantities were already scaled by RecipeService (and kept
                // in sync by InventoryReducer) using CraftsNeeded =
                // ceil(quantity / ExpectedOutputCount), i.e. the number of
                // Mystic Forge ATTEMPTS needed at the recipe's expected
                // success rate, not the nominal integer output. That means
                // every ingredient node's Quantity - and therefore its
                // already-summed cost below - already reflects the full
                // expected cost of producing enough successes. No further
                // ratio adjustment happens here: doing so on top of the
                // pre-scaled quantities would double-amortize the cost and
                // (per the M33 Critical fix) make this Craft decision's own
                // TotalCost unreconcilable with the sum of the Buy steps it
                // recursively spawns. A no-op for every ordinary recipe
                // either way, since ExpectedOutputCount defaults to
                // OutputCount there.
                long craftCost = 0L;
                long craftRealCost = 0L;

                foreach (var ingredient in recipe.Ingredients)
                {
                    if (ingredient.IngredientType == "Currency")
                    {
                        // Currencies contribute to the craft-vs-buy
                        // DECISION value only (via a caller-supplied
                        // per-unit valuation - the same CurrencyValuation
                        // mechanism EvaluateVendorOffers already uses below
                        // for vendor currency lines), never to the
                        // displayed real coin cost - matches r1 sections
                        // 4.2/4.3: a currency cost can tip a recipe out of
                        // being the cheapest option, but the plan's gold
                        // total never invents an exchange rate for it. An
                        // unvalued currency (the default - no invented
                        // rate) contributes zero to both, same as before
                        // this fix.
                        if (currencyValuation != null &&
                            currencyValuation.TryGetCopperValue(ingredient.Id, out long copperPerUnit))
                        {
                            try
                            {
                                craftCost = checked(craftCost + (long)ingredient.Quantity * copperPerUnit);
                            }
                            catch (OverflowException)
                            {
                                // Absurd valuation input; ignore rather than
                                // crash or silently misrank the recipe.
                            }
                        }
                        continue;
                    }

                    long? ingredientCost = Evaluate(
                        ingredient, prices, vendorOffers, memo, priceBasis, overrides, currencyValuation,
                        forceBuyOnlyNodeIds, costDiagnostics, ignoredItemIds, tiers);
                    craftCost += ingredientCost ?? 0L;
                    craftRealCost += memo[ingredient.NodeId].TotalCost ?? 0L;
                }

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

            // canCraft = "hasComponents" (gw2e): true whenever a recipe
            // exists at all, since craft cost is now always defined (see
            // above). A node with a recipe but no buy price therefore
            // always force-crafts via PickCheapest below (craftBeatsBuy is
            // true whenever buyCost is null), matching gw2e's
            // isCheaperToCraft = craftPrice-defined && (!buyPrice || ...).
            bool canCraft = bestCraftCost.HasValue;
            bool canBuyTp = buyTotalCost.HasValue;
            bool canBuyVendor = comparableVendorValue.HasValue ||
                                fallbackVendorCoinCost.HasValue;

            // M34-B2a #3: raw diagnostics for OwnedMaterialsForceBuyPrePass -
            // recorded regardless of forceBuyOnlyNodeIds/decision, so the
            // pre-pass (a throwaway solve with neither set) can read the
            // same numbers the real solve would have used.
            if (costDiagnostics != null)
            {
                costDiagnostics[node.NodeId] = (buyTotalCost, bestCraftCost);
            }

            // M34-B2a #3: gw2e's "Value Own Materials" force-buy pre-pass
            // marks this node craft:false BEFORE the automatic comparison
            // below - a manual override (checked next, using the
            // unmodified canCraft flag above) still always wins, matching
            // gw2e's own manual pill always beating its automatic pre-pass.
            bool craftExcludedFromAutoPick = forceBuyOnlyNodeIds != null &&
                forceBuyOnlyNodeIds.Contains(node.NodeId);

            // cost = real coin (Decision.TotalCost / display); comparisonValue
            // = parent-comparison value (Decision.ComparisonValue). Commit
            // returns comparisonValue - see Decision.ComparisonValue and the
            // Evaluate summary doc for why the two must stay separate.
            long? Commit(
                AcquisitionSource src, long? cost, long? comparisonValue,
                int recipeId, List<CostLine> vendorCurrencyCosts,
                VendorBatchSolver.VendorOfferBatch? vendorBatch = null)
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
                    CanBuyVendor = canBuyVendor,
                    VendorBatch = vendorBatch
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
                        ? Commit(AcquisitionSource.BuyFromVendor, comparableVendorCoinCost, comparableVendorValue, 0, comparableVendorCurrencyCosts, comparableVendorBatch)
                        : Commit(AcquisitionSource.BuyFromVendor, fallbackVendorCoinCost, fallbackVendorCoinCost, 0, fallbackVendorCurrencyCosts, fallbackVendorBatch);
                }
            }

            // Three-way comparison: vendor (coin part + any valued currency
            // lines) vs TP buy vs craft
            var source = PickCheapest(
                buyTotalCost,
                craftExcludedFromAutoPick ? null : bestCraftCost,
                comparableVendorValue);

            if (source == AcquisitionSource.BuyFromVendor)
            {
                return Commit(AcquisitionSource.BuyFromVendor, comparableVendorCoinCost, comparableVendorValue, 0, comparableVendorCurrencyCosts, comparableVendorBatch);
            }

            if (source == AcquisitionSource.BuyFromTp)
            {
                return Commit(AcquisitionSource.BuyFromTp, buyTotalCost, buyTotalCost, 0, null);
            }

            if (source == AcquisitionSource.Craft)
            {
                return Commit(AcquisitionSource.Craft, bestCraftRealCost, bestCraftCost, bestRecipeId, null);
            }

            // Fallback: nothing coin-priceable/craftable beat buy (source ==
            // UnknownSource here implies buyCost, bestCraftCost, and
            // comparableVendorValue are ALL null - if bestCraftCost had a
            // value, PickCheapest would already have returned Craft or
            // BuyFromTp, never UnknownSource, since craftCost is now always
            // defined whenever a recipe exists). A mixed-currency vendor
            // offer is a concrete, fully-known acquisition and is used as
            // the last resort; otherwise this is gw2e's "Not sold or
            // crafted" - no recipe, no price, genuinely no known source.
            if (fallbackVendorCoinCost.HasValue)
            {
                return Commit(AcquisitionSource.BuyFromVendor, fallbackVendorCoinCost, fallbackVendorCoinCost, 0, fallbackVendorCurrencyCosts, fallbackVendorBatch);
            }

            return Commit(AcquisitionSource.UnknownSource, null, null, 0, null);
        }

        /// <summary>
        /// Pick cheapest among TP buy, craft, and vendor - gw2e tie-break
        /// parity (r1 sections 1.1/3.2, normative directive #1): TP buy is
        /// the baseline and wins every tie. Craft wins only when STRICTLY
        /// cheaper than buy; a missing buy price counts as "beats buy"
        /// (force-craft - gw2e's isCheaperToCraft = craftPrice-defined &&
        /// (!buyPrice || decisionPrice &lt; buyPrice)). Vendor is modeled
        /// like a gw2e Merchant recipe and follows the identical rule
        /// against buy (strictly cheaper wins, tie -&gt; buy). When both
        /// craft and vendor beat buy, the numerically cheaper of the two
        /// wins; an exact craft/vendor tie keeps vendor (this engine's
        /// pre-existing precedent for that specific case - not specified
        /// by the gw2e source, which models vendor as just another recipe
        /// candidate rather than a separate comparison arm).
        /// Returns UnknownSource if none are available.
        /// </summary>
        private static AcquisitionSource PickCheapest(
            long? buyCost, long? craftCost, long? vendorCost)
        {
            bool craftBeatsBuy = craftCost.HasValue &&
                (!buyCost.HasValue || craftCost.Value < buyCost.Value);
            bool vendorBeatsBuy = vendorCost.HasValue &&
                (!buyCost.HasValue || vendorCost.Value < buyCost.Value);

            if (craftBeatsBuy && vendorBeatsBuy)
            {
                return vendorCost.Value <= craftCost.Value
                    ? AcquisitionSource.BuyFromVendor
                    : AcquisitionSource.Craft;
            }

            if (vendorBeatsBuy)
            {
                return AcquisitionSource.BuyFromVendor;
            }

            if (craftBeatsBuy)
            {
                return AcquisitionSource.Craft;
            }

            if (buyCost.HasValue)
            {
                return AcquisitionSource.BuyFromTp;
            }

            return AcquisitionSource.UnknownSource;
        }

        private void Collect(
            RecipeNode node,
            Dictionary<int, Decision> memo,
            Dictionary<(int, AcquisitionSource, int), PlanStep> stepMap,
            Dictionary<int, long> currencyMap,
            Dictionary<(int, int), int> craftOrder,
            Dictionary<(int, AcquisitionSource, int), VendorBatchSolver.VendorBatchState> vendorBatchTracking,
            Dictionary<(int, AcquisitionSource, int), List<(int NodeId, int Quantity)>> vendorOccurrences,
            Dictionary<(int, AcquisitionSource, int), List<int>> craftOccurrences,
            ref int craftCounter,
            ISet<int> ignoredItemIds = null)
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

            // M37 (KNOWN-ISSUES #26 fix-pass finding): a Quantity == 0
            // "Item" node draws no demand of its own and must never
            // generate a shopping/craft step - matches
            // CraftingTreeBuilder.BuildNode's own Quantity == 0 early
            // return (the "already owned" collapse, checked first there
            // too) for the SAME reason, regardless of WHY it is zero
            // (genuine full ownership via InventoryReducer, or a duplicate
            // occurrence zeroed by AchievementBitDedupPrePass). Without
            // this guard, a zeroed node whose "real" counterpart resolves
            // to a DIFFERENT stepKey (e.g. Craft vs. Buy - so the two never
            // merge via the ordinary per-stepKey aggregation below) leaves
            // a standalone "buy/craft 0 units, 0 cost" ghost row in
            // Plan.Steps: reproduced and confirmed via manual trace while
            // verifying this exact claim for the M37 achievement-bit dedup
            // feature (docs/research/m37-r3-achievement-dedup.md Section
            // 4.2 flags this as needing verification, not assumption) - see
            // PlanSolverTests' QuantityZeroNode_* cases for the covering
            // scenario.
            // This was already a latent gap for the pre-existing genuinely-
            // owned case (the comment immediately below, predating this
            // fix, already claimed a real Quantity == 0 node produces "no
            // step" - this guard is what makes that claim actually true).
            //
            // Invariant this guard relies on (not enforced here, only
            // documented): every "Item" node that reaches this line with
            // Quantity == 0 must already have empty Recipes. True today
            // because both InventoryReducer.ReduceNode and
            // AchievementBitDedupPrePass always pair Quantity = 0 with
            // Recipes.Clear(). If that pairing is ever broken by a future
            // pre-pass or bug, this guard would silently skip recursing
            // into that node's children too - dropping their real,
            // nonzero-Quantity costs from the plan, not just suppressing a
            // zero-cost ghost row for the parent - so keep any new
            // Quantity-zeroing code paired with clearing Recipes.
            if (node.Quantity == 0)
            {
                return;
            }

            // M34-B2b: an ignored item generates no crafting step and no
            // shopping row at all - it is fully in-hand, same as a real
            // Quantity == 0 node's "usedQuantity == 0 -> no step" gw2e
            // parity target (Section 5 of the r2 report). Evaluate already
            // committed a zero-cost memo entry for it (never recursing into
            // its own ingredients), so skipping it here as well keeps the
            // plan free of a bogus "buy 0-cost N units" row.
            if (ignoredItemIds != null && ignoredItemIds.Contains(node.Id))
            {
                return;
            }

            if (!memo.TryGetValue(node.NodeId, out var decision))
            {
                return;
            }

            // M35 (gw2e parity, multi-item plans): the synthetic multi-item
            // wrapper root (see Gw2Constants.MultiItemWrapperItemId) is
            // never a real acquisition - it exists purely so Evaluate can
            // price N selected item roots together under one throwaway
            // "recipe". Recurse straight into that recipe's own Ingredients
            // (the N real item roots) WITHOUT ever generating a step/
            // craftOrder entry for the wrapper's own Craft decision -
            // echoes gw2e's componentTree.html hiding the fake
            // `multipleRecipeTree` node from the rendered Crafting Steps
            // list (docs/gw2e-parity-spec.md, the M34 r1 multi-item
            // research report). Evaluate always force-crafts this node
            // (it has a recipe and no buy price - Gw2Constants sentinel ids
            // are never in `prices`), so decision.Source is always Craft
            // here; the explicit check still guards against future change.
            if (node.Id == Gw2Constants.MultiItemWrapperItemId &&
                decision.Source == AcquisitionSource.Craft)
            {
                var wrapperRecipe = node.Recipes.FirstOrDefault(r => r.RecipeId == decision.RecipeId);
                if (wrapperRecipe != null)
                {
                    foreach (var itemRoot in wrapperRecipe.Ingredients)
                    {
                        Collect(itemRoot, memo, stepMap, currencyMap, craftOrder, vendorBatchTracking, vendorOccurrences, craftOccurrences, ref craftCounter, ignoredItemIds);
                    }
                }
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
                        Collect(ingredient, memo, stepMap, currencyMap, craftOrder, vendorBatchTracking, vendorOccurrences, craftOccurrences, ref craftCounter, ignoredItemIds);
                    }
                }

                // Record craft order (first time seeing this item+recipe as craft)
                var craftOrderKey = (node.Id, decision.RecipeId);
                if (!craftOrder.ContainsKey(craftOrderKey))
                {
                    craftOrder[craftOrderKey] = craftCounter++;
                }

                var stepKey = (node.Id, AcquisitionSource.Craft, decision.RecipeId);
                AggregateStep(stepMap, stepKey, node, decision, vendorBatchTracking, vendorOccurrences, craftOccurrences);
            }
            else if (decision.Source == AcquisitionSource.BuyFromVendor)
            {
                // Vendor currency costs are folded into currencyMap once,
                // after the whole tree is collected and every merged vendor
                // step's true (aggregate-then-ceil) cost is known - see
                // VendorBatchSolver.FinalizeVendorBatches. Folding the still-
                // per-occurrence decision.VendorCurrencyCosts in here would
                // re-introduce the exact per-occurrence-then-sum overcount
                // FinalizeVendorBatches exists to fix (M34-B1 #1).
                var stepKey = (node.Id, AcquisitionSource.BuyFromVendor, 0);
                AggregateStep(stepMap, stepKey, node, decision, vendorBatchTracking, vendorOccurrences, craftOccurrences);
            }
            else
            {
                var stepKey = (node.Id, decision.Source, 0);
                AggregateStep(stepMap, stepKey, node, decision, vendorBatchTracking, vendorOccurrences, craftOccurrences);
            }
        }

        private void AggregateStep(
            Dictionary<(int, AcquisitionSource, int), PlanStep> stepMap,
            (int, AcquisitionSource, int) stepKey,
            RecipeNode node,
            Decision decision,
            Dictionary<(int, AcquisitionSource, int), VendorBatchSolver.VendorBatchState> vendorBatchTracking,
            Dictionary<(int, AcquisitionSource, int), List<(int NodeId, int Quantity)>> vendorOccurrences,
            Dictionary<(int, AcquisitionSource, int), List<int>> craftOccurrences)
        {
            if (decision.Source == AcquisitionSource.Craft)
            {
                // M34 fix (wave-validator finding): remembers every
                // individual tree occurrence's own NodeId that fed this
                // merged Craft stepKey, in first-seen (DFS) order, so
                // RefreshCraftStepCosts can re-sum this step's true total
                // from `memo` AFTER RecomputeCraftCosts corrects it - see
                // that method's doc comment. Mirrors the vendor-side
                // occurrence bookkeeping just below for BuyFromVendor.
                if (!craftOccurrences.TryGetValue(stepKey, out var craftOccurrenceList))
                {
                    craftOccurrenceList = new List<int>();
                    craftOccurrences[stepKey] = craftOccurrenceList;
                }
                craftOccurrenceList.Add(node.NodeId);
            }

            if (decision.Source == AcquisitionSource.BuyFromVendor && decision.VendorBatch.HasValue)
            {
                var batch = decision.VendorBatch.Value;

                if (vendorBatchTracking.TryGetValue(stepKey, out var trackedState))
                {
                    if (!trackedState.Conflict && !_vendorBatchSolver.VendorBatchesEqual(trackedState.Batch, batch))
                    {
                        // Ratchet only: a later occurrence agreeing with the
                        // tracked batch must not clear a conflict a prior
                        // occurrence already raised.
                        trackedState.Conflict = true;
                    }
                }
                else
                {
                    vendorBatchTracking[stepKey] = new VendorBatchSolver.VendorBatchState
                    {
                        Batch = batch,
                        Conflict = false
                    };
                }

                // M34 fix (Critical review finding, PlanSolver.cs:1038):
                // remembers every individual tree occurrence's own NodeId
                // and Quantity that fed this merged vendor stepKey, in
                // first-seen (DFS) order, so AllocateVendorNodeCosts can
                // redistribute FinalizeVendorBatches' corrected merged total
                // back to each occurrence's own memo entry afterward - see
                // that method's doc comment for why this per-node fixup is
                // necessary in addition to the stepMap-level one.
                if (!vendorOccurrences.TryGetValue(stepKey, out var occurrenceList))
                {
                    occurrenceList = new List<(int NodeId, int Quantity)>();
                    vendorOccurrences[stepKey] = occurrenceList;
                }
                occurrenceList.Add((node.NodeId, node.Quantity));
            }

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
                if (decision.Source == AcquisitionSource.BuyFromVendor)
                {
                    existing.VendorCurrencyCosts = _vendorBatchSolver.MergeVendorCurrencyCosts(
                        existing.VendorCurrencyCosts, decision.VendorCurrencyCosts);
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
                    RecipeId = decision.RecipeId,
                    VendorCurrencyCosts = decision.Source == AcquisitionSource.BuyFromVendor
                        ? _vendorBatchSolver.MergeVendorCurrencyCosts(null, decision.VendorCurrencyCosts)
                        : null
                };
            }
        }

        /// <summary>
        /// Re-sums every Craft decision's TotalCost bottom-up from its
        /// chosen recipe's (now possibly AllocateVendorNodeCosts-corrected)
        /// ingredient TotalCosts, mirroring Evaluate's own craftRealCost
        /// aggregation (non-currency ingredients only - a currency
        /// ingredient never contributes to real coin TotalCost, same as
        /// Evaluate). Necessary because Evaluate computed every Craft
        /// node's TotalCost bottom-up BEFORE FinalizeVendorBatches/
        /// AllocateVendorNodeCosts ever ran, so a Craft node anywhere above
        /// a corrected vendor-bought leaf - all the way up to the tree
        /// root - would otherwise keep summing the leaf's stale
        /// pre-correction share.
        ///
        /// Walks only the CHOSEN path (node.Recipes.FirstOrDefault(r =&gt;
        /// r.RecipeId == decision.RecipeId), exactly like Collect) - never
        /// the alternate, non-chosen recipes' ingredient nodes Evaluate also
        /// memoized for comparison purposes, since those never fed the
        /// solved plan and are not what the real tree displays.
        ///
        /// Depth is NOT bounded: this recurses down the entire chosen-path
        /// subtree from whatever `node` it is called with (Solve calls it
        /// once, with the tree root), so every Craft ancestor's `memo`
        /// entry - and therefore every CraftingTreeNode.SubtreeCost derived
        /// from it - is correct however many Craft levels separate it from
        /// a corrected leaf. Confirmed by a 4-Craft-level, multi-branch
        /// regression (see PlanSolverTests) and by a real-tree Harness dump
        /// against the live Exordium recipe tree: root and every
        /// intermediate Craft node's SubtreeCost already reconcile with
        /// their children's corrected costs after this call returns. The
        /// gap this class of bug actually hid in was elsewhere - see
        /// RefreshCraftStepCosts below, which fixes the Craft-type
        /// PlanStep (shopping-list row) side of the same correction that
        /// this method already handled correctly for the Decisions/tree
        /// side.
        /// </summary>
        private static long? RecomputeCraftCosts(
            RecipeNode node, Dictionary<int, Decision> memo, ISet<int> ignoredItemIds)
        {
            if (node.IngredientType == "Currency")
            {
                return null;
            }

            if (ignoredItemIds != null && ignoredItemIds.Contains(node.Id))
            {
                return memo.TryGetValue(node.NodeId, out var ignoredDecision)
                    ? ignoredDecision.TotalCost
                    : 0L;
            }

            if (!memo.TryGetValue(node.NodeId, out var decision))
            {
                return null;
            }

            if (decision.Source != AcquisitionSource.Craft)
            {
                return decision.TotalCost;
            }

            var chosenRecipe = node.Recipes.FirstOrDefault(r => r.RecipeId == decision.RecipeId);
            long craftRealCost = 0L;
            if (chosenRecipe != null)
            {
                foreach (var ingredient in chosenRecipe.Ingredients)
                {
                    if (ingredient.IngredientType == "Currency")
                    {
                        continue;
                    }
                    craftRealCost += RecomputeCraftCosts(ingredient, memo, ignoredItemIds) ?? 0L;
                }
            }

            decision.TotalCost = craftRealCost;
            memo[node.NodeId] = decision;
            return craftRealCost;
        }

        /// <summary>
        /// M34 fix (wave-validator finding, post-fcbb277): re-derives every
        /// Craft-type PlanStep's TotalCost from `memo` - AFTER
        /// AllocateVendorNodeCosts/RecomputeCraftCosts have already
        /// corrected it there - instead of trusting AggregateStep's running
        /// sum from Collect(), which is built BEFORE those correction
        /// passes ever run. Without this, a Craft row in the "Crafting
        /// Steps" shopping list stayed permanently stale (the pre-merge,
        /// per-occurrence-overcounted total) even though the SAME item's
        /// Recipe Tree row (CraftingTreeNode.SubtreeCost, sourced from the
        /// now-corrected `memo` via the public Decisions dict) and
        /// plan.TotalCoinCost (summed from FinalizeVendorBatches' own
        /// already-corrected Buy/Vendor steps) both showed the right
        /// number - the exact "two sections of the same page disagree"
        /// defect fcbb277 set out to eliminate, left half-fixed for the
        /// Craft-step side.
        ///
        /// A full restructure - running the vendor-batch merge/allocation
        /// BEFORE Collect ever builds a PlanStep, so no stale snapshot
        /// could exist in the first place - was considered and rejected:
        /// the merge needs each item's AGGREGATE demand across every tree
        /// occurrence (FinalizeVendorBatches' whole premise), which is
        /// only known once a full tree walk has completed, i.e. after a
        /// Collect-shaped pass has already run. Reordering would therefore
        /// mean two full tree walks (one to gather occurrence/demand data,
        /// a second to build the now-correct PlanSteps) instead of the one
        /// walk plus this narrow refresh - more moving parts for the same
        /// asymptotic cost, not less. This method is the second walk's
        /// cheaper equivalent: it revisits only the (few) Craft stepKeys
        /// that exist, not the tree.
        ///
        /// Uses <paramref name="craftOccurrences"/> (built by AggregateStep,
        /// the Craft-side twin of vendorOccurrences) rather than
        /// re-deriving occurrences from the tree, so this stays a flat
        /// stepMap-sized pass regardless of tree depth or branching. A
        /// missing/null memo TotalCost (an unpriceable ingredient
        /// somewhere in that craft's chosen recipe) contributes 0, matching
        /// AggregateStep's own original null-handling (a null decision.
        /// TotalCost never increments the running total there either).
        /// </summary>
        private static void RefreshCraftStepCosts(
            Dictionary<(int, AcquisitionSource, int), PlanStep> stepMap,
            Dictionary<(int, AcquisitionSource, int), List<int>> craftOccurrences,
            Dictionary<int, Decision> memo)
        {
            foreach (var kvp in craftOccurrences)
            {
                if (!stepMap.TryGetValue(kvp.Key, out var step))
                {
                    continue;
                }

                long total = 0L;
                foreach (var nodeId in kvp.Value)
                {
                    if (memo.TryGetValue(nodeId, out var decision) && decision.TotalCost.HasValue)
                    {
                        total += decision.TotalCost.Value;
                    }
                }

                step.TotalCost = total;
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
