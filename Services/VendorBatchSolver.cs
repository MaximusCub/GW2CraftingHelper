using System;
using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The vendor-batching sub-engine: batch-shape state types plus the
    /// methods that turn per-occurrence vendor-offer evaluations into one
    /// true per-item merged-ceil cost and post-solve "timegated" cap
    /// notices. PlanSolver holds one instance as an injected collaborator.
    /// The nested types are `internal` only because PlanSolver needs to
    /// declare fields/locals of them.
    /// <para>See docs/ARCHITECTURE.md section 7.</para>
    /// </summary>
    internal class VendorBatchSolver
    {
        // See PlanSolver.Decision.VendorBatch's doc comment.
        internal struct VendorOfferBatch
        {
            public int OutputCount;
            public long CoinCostPerBatch;
            public List<CostLine> CurrencyCostLinesPerBatch;
            public int? DailyCap;
            public int? WeeklyCap;

            // Wizard's Vault seasonal purchase cap. Independent of
            // DailyCap/WeeklyCap - FinalizeVendorBatches checks it
            // separately so an offer carrying both can surface both notices.
            public int? SeasonalCap;
        }

        // Per-item-id (BuyFromVendor stepKey) bookkeeping built up across
        // every tree occurrence during PlanSolver.Collect/AggregateStep:
        // which offer batch shape was seen, and whether every occurrence
        // agreed (a node's own per-occurrence ceil can, in principle, pick
        // a different offer at a different local quantity - see
        // AggregateStep). Conflict is a one-way ratchet: once true, it
        // never resets, and FinalizeVendorBatches leaves that step's
        // already-per-occurrence-summed cost alone rather than guessing
        // which of several genuinely different offers should apply to the
        // merged total.
        internal sealed class VendorBatchState
        {
            public VendorOfferBatch Batch;
            public bool Conflict;

            // Do NOT add a coarser cap ratchet here to sum cap notices for
            // mixed-offer steps: the wiki's per-row cap is a template
            // parameter, not a confirmed per-station aggregate, so
            // occurrences agreeing on the raw tuple does not mean a real
            // shared limit. Conflict alone suppresses the notice.
        }

        /// <summary>
        /// EvaluateVendorOffers' result - a pure data carrier; see that
        /// method's doc comment for the comparable/fallback split,
        /// valuation rules, and cap-notice plumbing this carries.
        /// </summary>
        internal readonly struct VendorOfferEvaluation
        {
            public readonly long? BestComparableValue;
            public readonly long? BestComparableCoinCost;
            public readonly List<CostLine> BestComparableCurrencyCosts;
            public readonly VendorOfferBatch? BestComparableBatch;
            public readonly long? FallbackCoinCost;
            public readonly List<CostLine> FallbackCurrencyCosts;
            public readonly VendorOfferBatch? FallbackBatch;

            // Additive outputs captured at the same per-line
            // multiplication EvaluateVendorOffers already performs (never
            // a second, divergent computation), so a display-tree leaf can
            // be built from real, already-computed numbers.
            // BestComparableItemCosts/FallbackItemCosts are the winning
            // offer's TP-valued Item cost lines, scaled to this
            // occurrence's unitsNeeded - null when the winning offer had
            // none. BestComparableHasRawCoin/
            // FallbackHasRawCoin report only whether the winning offer had a
            // genuine raw coin cost line (Type=="Currency",
            // Id==Gw2Constants.CoinCurrencyId) with Count > 0 - distinct
            // from an item-folded coin contribution - so a caller can tell
            // "coin" apart from "item money" as its own cost KIND without
            // re-deriving it from TotalCost (which mixes both). Neither
            // field changes any existing comparison/selection/scaling
            // logic in this method.
            public readonly List<VendorItemCostLine> BestComparableItemCosts;
            public readonly bool BestComparableHasRawCoin;
            public readonly List<VendorItemCostLine> FallbackItemCosts;
            public readonly bool FallbackHasRawCoin;

            public VendorOfferEvaluation(
                long? bestComparableValue,
                long? bestComparableCoinCost,
                List<CostLine> bestComparableCurrencyCosts,
                VendorOfferBatch? bestComparableBatch,
                long? fallbackCoinCost,
                List<CostLine> fallbackCurrencyCosts,
                VendorOfferBatch? fallbackBatch,
                List<VendorItemCostLine> bestComparableItemCosts = null,
                bool bestComparableHasRawCoin = false,
                List<VendorItemCostLine> fallbackItemCosts = null,
                bool fallbackHasRawCoin = false)
            {
                BestComparableValue = bestComparableValue;
                BestComparableCoinCost = bestComparableCoinCost;
                BestComparableCurrencyCosts = bestComparableCurrencyCosts;
                BestComparableBatch = bestComparableBatch;
                FallbackCoinCost = fallbackCoinCost;
                FallbackCurrencyCosts = fallbackCurrencyCosts;
                FallbackBatch = fallbackBatch;
                BestComparableItemCosts = bestComparableItemCosts;
                BestComparableHasRawCoin = bestComparableHasRawCoin;
                FallbackItemCosts = fallbackItemCosts;
                FallbackHasRawCoin = fallbackHasRawCoin;
            }
        }

        /// <summary>
        /// A Homestead Refinement offer whose tagged tier exceeds
        /// <paramref name="homesteadTiers"/>' configured tier for that
        /// material is skipped entirely - the seed carries every
        /// refinement row unconditionally, so without this gate the solver
        /// behaves as if every account had every efficiency upgrade.
        ///
        /// Splits vendor offers into two tiers. An offer is COMPARABLE (competes
        /// with TP/craft coin costs in PickCheapest) when it has no non-coin
        /// currency lines at all, OR every one of its non-coin currency lines
        /// has a user-provided valuation (<paramref name="currencyValuation"/>):
        /// its comparison value is coin part + sum(count * copperPerUnit) over
        /// those valued lines, reported via
        /// <see cref="VendorOfferEvaluation.BestComparableValue"/>.
        /// The winning comparable offer's real coin part and (if any) currency
        /// lines are reported separately via
        /// <see cref="VendorOfferEvaluation.BestComparableCoinCost"/>
        /// and <see cref="VendorOfferEvaluation.BestComparableCurrencyCosts"/> -
        /// the valuation affects comparison only, never the amounts committed
        /// to the plan.
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
        /// A DailyCap/WeeklyCap NEVER excludes an offer or affects its
        /// tier - gw2e only ever surfaces a cap as a post-solve notice,
        /// never re-routing the tree. Both tiers carry the raw caps
        /// through so FinalizeVendorBatches can produce the notice once,
        /// against aggregate demand; SeasonalCap is carried the same way
        /// and checked independently.
        /// </summary>
        internal VendorOfferEvaluation EvaluateVendorOffers(
            RecipeNode node,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            PriceBasis priceBasis,
            CurrencyValuation currencyValuation,
            HomesteadEfficiencyTiers homesteadTiers)
        {
            long? bestComparableValue = null;
            long? bestComparableCoinCost = null;
            List<CostLine> bestComparableCurrencyCosts = null;
            VendorOfferBatch? bestComparableBatch = null;
            long? fallbackCoinCost = null;
            List<CostLine> fallbackCurrencyCosts = null;
            VendorOfferBatch? fallbackBatch = null;
            long fallbackCurrencyUnits = 0;
            int fallbackSingleCurrencyId = -1;

            List<VendorItemCostLine> bestComparableItemCosts = null;
            bool bestComparableHasRawCoin = false;
            List<VendorItemCostLine> fallbackItemCosts = null;
            bool fallbackHasRawCoin = false;

            if (vendorOffers == null ||
                !vendorOffers.TryGetValue(node.Id, out var offers))
            {
                return new VendorOfferEvaluation(
                    bestComparableValue,
                    bestComparableCoinCost,
                    bestComparableCurrencyCosts,
                    bestComparableBatch,
                    fallbackCoinCost,
                    fallbackCurrencyCosts,
                    fallbackBatch);
            }

            foreach (var offer in offers)
            {
                if (offer.OutputCount <= 0)
                {
                    continue;
                }

                // Keyed on OutputItemId, not a merchant-name match:
                // HomesteadTier is only set on rows the seeding pass
                // already confirmed are Homestead Refinement, so a string
                // check here would be redundant work in a hot loop.
                if (offer.HomesteadTier.HasValue &&
                    offer.HomesteadTier.Value > homesteadTiers.GetTier(offer.OutputItemId))
                {
                    continue;
                }

                long coinCost = 0;
                bool priceable = true;
                var currencyCosts = new List<CostLine>();

                // Raw (unscaled, per-batch) capture of this offer's
                // coin-presence and Item cost lines, purely additive.
                // hasRawCoin distinguishes a genuine coin line from coin
                // that only exists because an Item line was folded in.
                // itemCostRaw is exactly what feeds the coinCost fold
                // below - captured so a display leaf can be built without
                // recomputing the multiplication. PriceSideFellBack
                // is this same line's own
                // GetUnitPrice out param, carried alongside so the scaled
                // VendorItemCostLine below can flag it the same way a
                // plain BuyFromTp node already is.
                bool hasRawCoin = false;
                List<(int ItemId, int Count, int UnitPrice, bool PriceSideFellBack)> itemCostRaw = null;

                foreach (var cost in offer.CostLines ?? Enumerable.Empty<CostLine>())
                {
                    if (string.Equals(cost.Type, "Currency", StringComparison.Ordinal))
                    {
                        if (cost.Id == Gw2Constants.CoinCurrencyId)
                        {
                            coinCost += (long)cost.Count;
                            if (cost.Count > 0)
                            {
                                hasRawCoin = true;
                            }
                        }
                        else
                        {
                            // Guarded with
                            // Count > 0, mirroring the raw-coin branch's own
                            // `if (cost.Count > 0)` guard 5 lines above and
                            // the identical Item-cost-line guard below - a
                            // zero/negative-count non-coin Currency cost
                            // line (e.g. malformed wiki-scraped seed data)
                            // must never invent a phantom "currency" cost
                            // KIND. Without this, a single-kind offer with a
                            // stray Count-0 Currency line would wrongly flip
                            // into leaf-synthesis mode
                            // (CraftingTreeBuilder.BuildVendorCostComponentLeaves'
                            // kindCount gate) and render a 0-quantity ghost
                            // leaf with a blank cost and no pill (a negative
                            // Count would render a negative-quantity leaf
                            // instead - survives the `scaled > int.MaxValue`
                            // check below just like the Item side). coinCost
                            // above is untouched either way - a Count of 0
                            // never reaches it from this branch.
                            if (cost.Count > 0)
                            {
                                currencyCosts.Add(cost);
                            }
                        }
                    }
                    else if (string.Equals(cost.Type, "Item", StringComparison.Ordinal))
                    {
                        // The 3-arg GetUnitPrice overload captures this
                        // barter item's own fell-back-side fact rather than
                        // discarding it - see itemCostRaw's doc comment
                        // above and VendorItemCostLine.PriceSideFellBack.
                        int unitPrice = 0;
                        bool itemPriceSideFellBack = false;
                        if (prices.TryGetValue(cost.Id, out var itemPrice))
                        {
                            unitPrice = PlanSolver.GetUnitPrice(itemPrice, priceBasis, out itemPriceSideFellBack);
                        }

                        if (unitPrice > 0)
                        {
                            coinCost += (long)cost.Count * unitPrice;
                            // Guard the raw
                            // capture with Count > 0, mirroring the raw-coin
                            // branch's own `if (cost.Count > 0)` guard above
                            // - a zero/negative-count Item cost line (e.g.
                            // malformed wiki-scraped seed data) must never
                            // invent a phantom "item" cost KIND. Without
                            // this, a single-kind offer with a stray
                            // Count-0 Item line would wrongly flip into
                            // leaf-synthesis mode (CraftingTreeBuilder.
                            // BuildVendorCostComponentLeaves' kindCount
                            // gate) and render a 0-quantity/negative-cost
                            // ghost leaf. coinCost above is left untouched -
                            // a Count of 0 contributes nothing to it either
                            // way.
                            if (cost.Count > 0)
                            {
                                (itemCostRaw ?? (itemCostRaw = new List<(int, int, int, bool)>()))
                                    .Add((cost.Id, cost.Count, unitPrice, itemPriceSideFellBack));
                            }
                        }
                        else
                        {
                            priceable = false;
                            break;
                        }
                    }
                    else
                    {
                        // An unrecognized CostLine.Type must never be
                        // silently dropped from the fold above - doing so
                        // would leave `priceable` true and cost this offer
                        // as if the line were not there at all, understating
                        // it and letting it win BuyFromVendor at a
                        // fabricated-low price. VendorOfferLoader performs
                        // no type validation at load and ref/vendor_offers.json
                        // is tool-scraped, so a future third CostLine.Type
                        // (today only "Currency"/"Item" appear) reaches this
                        // loop directly, the same way "GuildUpgrade" reached
                        // the recipe-ingredient guards this mirrors. Mirrors
                        // the Item-with-no-price branch immediately above:
                        // treat the whole offer as unpriceable rather than
                        // guess at a cost for a cost-line shape this solver
                        // has never seen.
                        priceable = false;
                        break;
                    }
                }

                if (!priceable)
                {
                    continue;
                }

                int unitsNeeded = (int)Math.Ceiling((double)node.Quantity / offer.OutputCount);

                long totalCoinCost = coinCost * unitsNeeded;

                // Scale itemCostRaw by the same unitsNeeded factor
                // totalCoinCost just applied - the same arithmetic, never
                // a second, potentially-diverging computation. Guarded the
                // same way the currency scaling guards CostLine.Count: a
                // scaled quantity too large for
                // VendorItemCostLine.Quantity (int) skips the offer rather
                // than truncating it silently.
                //
                // The `itemsScalable`/`continue` guard is structurally
                // identical to the pre-existing currency `scalable` guard
                // below, can only fire above int.MaxValue scaled quantity,
                // and skips rather than clamps - a clamp is silently wrong.
                List<VendorItemCostLine> scaledItemCosts = null;
                bool itemsScalable = true;
                if (itemCostRaw != null)
                {
                    scaledItemCosts = new List<VendorItemCostLine>(itemCostRaw.Count);
                    foreach (var ic in itemCostRaw)
                    {
                        long scaledQty = (long)ic.Count * unitsNeeded;
                        if (scaledQty > int.MaxValue)
                        {
                            itemsScalable = false;
                            break;
                        }

                        scaledItemCosts.Add(new VendorItemCostLine
                        {
                            ItemId = ic.ItemId,
                            Quantity = (int)scaledQty,
                            GoldValue = (long)ic.Count * unitsNeeded * ic.UnitPrice,
                            PriceSideFellBack = ic.PriceSideFellBack,
                        });
                    }
                }

                if (!itemsScalable)
                {
                    continue;
                }

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
                            Count = (int)scaled,
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
                        bestComparableItemCosts = scaledItemCosts;
                        bestComparableHasRawCoin = hasRawCoin;
                        bestComparableBatch = new VendorOfferBatch
                        {
                            OutputCount = offer.OutputCount,
                            CoinCostPerBatch = coinCost,
                            CurrencyCostLinesPerBatch = currencyCosts.Count > 0 ? currencyCosts : null,
                            DailyCap = offer.DailyCap,
                            WeeklyCap = offer.WeeklyCap,
                            SeasonalCap = offer.SeasonalCap,
                        };
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
                    fallbackItemCosts = scaledItemCosts;
                    fallbackHasRawCoin = hasRawCoin;
                    fallbackBatch = new VendorOfferBatch
                    {
                        OutputCount = offer.OutputCount,
                        CoinCostPerBatch = coinCost,
                        CurrencyCostLinesPerBatch = currencyCosts.Count > 0 ? currencyCosts : null,
                        DailyCap = offer.DailyCap,
                        WeeklyCap = offer.WeeklyCap,
                        SeasonalCap = offer.SeasonalCap,
                    };
                }
            }

            return new VendorOfferEvaluation(
                bestComparableValue,
                bestComparableCoinCost,
                bestComparableCurrencyCosts,
                bestComparableBatch,
                fallbackCoinCost,
                fallbackCurrencyCosts,
                fallbackBatch,
                bestComparableItemCosts,
                bestComparableHasRawCoin,
                fallbackItemCosts,
                fallbackHasRawCoin);
        }

        /// <summary>
        /// Sums <paramref name="add"/> into <paramref name="existing"/> by
        /// currency id (a node can be aggregated into the same PlanStep row
        /// from multiple tree occurrences - see PlanSolver.AggregateStep).
        /// Always returns a fresh list when there is anything to carry, so
        /// the solver-internal Decision's own list is never mutated/aliased
        /// into a PlanStep.
        /// </summary>
        internal List<CostLine> MergeVendorCurrencyCosts(
            List<CostLine> existing, IReadOnlyList<CostLine> add)
        {
            if (add == null || add.Count == 0)
            {
                return existing;
            }

            var merged = existing != null
                ? new List<CostLine>(existing)
                : new List<CostLine>();

            foreach (var line in add)
            {
                int idx = merged.FindIndex(c => c.Id == line.Id);
                if (idx >= 0)
                {
                    // CostLine.Count is int; clamp rather than let two
                    // near-int.MaxValue occurrences silently wrap negative.
                    long summed = (long)merged[idx].Count + line.Count;
                    merged[idx] = new CostLine
                    {
                        Type = merged[idx].Type,
                        Id = merged[idx].Id,
                        Count = ClampToInt(summed),
                    };
                }
                else
                {
                    merged.Add(new CostLine { Type = line.Type, Id = line.Id, Count = line.Count });
                }
            }

            return merged;
        }

        /// <summary>
        /// Re-derives every merged BuyFromVendor PlanStep's true cost from
        /// its AGGREGATE Quantity and the winning offer's batch shape,
        /// ceiling the purchase count exactly once (gw2e's convention).
        /// The sum of independently-ceil'd per-occurrence costs overstates
        /// the true cost whenever an item is needed via 2+ occurrences and
        /// bought via a bulk offer.
        ///
        /// Only applied when every occurrence resolved to the identical
        /// winning offer (Conflict false) - re-deriving one "true" cost
        /// across genuinely different offers has no principled answer, so
        /// a Conflict step keeps AggregateStep's sum of real
        /// per-occurrence purchases, a deliberately conservative fallback.
        ///
        /// Also folds every vendor step's final VendorCurrencyCosts into
        /// currencyMap (the single place vendor currency reaches the
        /// plan-wide total) and collects timegated notices for any uniform
        /// step whose aggregate purchase count exceeds the daily
        /// (preferred) or weekly cap, plus an independent Seasonal-cap
        /// notice - the checks do not suppress each other. Caps never
        /// exclude an offer or change Source/TotalCost.
        ///
        /// The recomputed step.UnitCost is the winning offer's own
        /// CoinCostPerBatch/OutputCount rate, not a truncating
        /// total/Quantity average.
        ///
        /// Do not re-add a branch that sums a cap notice for Conflict
        /// steps whose occurrences agree on the raw cap tuple - the
        /// premise is false, see VendorBatchState's own comment.
        /// </summary>
        internal List<TimegatedItem> FinalizeVendorBatches(
            Dictionary<(int, AcquisitionSource, int), PlanStep> stepMap,
            Dictionary<(int, AcquisitionSource, int), VendorBatchState> vendorBatchTracking,
            Dictionary<int, long> currencyMap)
        {
            var timegatedItems = new List<TimegatedItem>();

            foreach (var kvp in stepMap)
            {
                var step = kvp.Value;
                if (step.Source != AcquisitionSource.BuyFromVendor)
                {
                    continue;
                }

                if (vendorBatchTracking.TryGetValue(kvp.Key, out var state) &&
                    !state.Conflict && state.Batch.OutputCount > 0)
                {
                    var batch = state.Batch;
                    int unitsNeeded = step.Quantity > 0
                        ? (int)Math.Ceiling((double)step.Quantity / batch.OutputCount)
                        : 0;

                    step.TotalCost = batch.CoinCostPerBatch * unitsNeeded;
                    // The coin "Each" cell must show the winning offer's own
                    // true per-unit rate (its per-batch coin cost divided by
                    // its own OutputCount), not a truncating average of the
                    // corrected AGGREGATE total over aggregate Quantity -
                    // the same defect class B1 #2 already fixed for the
                    // currency "Each" cell via CurrencyDisplayResolver.
                    // ResolveUnitAmounts. Example: a "2 for 5" offer merged
                    // to demand 3 gives TotalCost=10 (2 batches); the old
                    // 10/3=3 truncated average implied a per-unit price no
                    // real purchase of this offer ever charges, whereas the
                    // offer's actual rate is 5/2=2 (batch.OutputCount is
                    // already guarded > 0 by the branch condition above).
                    step.UnitCost = batch.CoinCostPerBatch / batch.OutputCount;
                    step.VendorCurrencyCosts = ScaleCostLines(batch.CurrencyCostLinesPerBatch, unitsNeeded);
                    step.VendorOfferOutputCount = batch.OutputCount;
                    step.VendorOfferCurrencyCostLinesPerBatch = batch.CurrencyCostLinesPerBatch;

                    int? cap = batch.DailyCap.HasValue && batch.DailyCap.Value > 0
                        ? batch.DailyCap
                        : (batch.WeeklyCap.HasValue && batch.WeeklyCap.Value > 0 ? batch.WeeklyCap : (int?)null);
                    if (cap.HasValue && unitsNeeded > cap.Value)
                    {
                        timegatedItems.Add(new TimegatedItem
                        {
                            ItemId = step.ItemId,
                            CapType = (batch.DailyCap.HasValue && batch.DailyCap.Value > 0)
                                ? TimegatedCapType.Daily
                                : TimegatedCapType.Weekly,
                            CapValue = cap.Value,
                            NeededCount = unitsNeeded,
                        });
                    }

                    // SeasonalCap is checked independently of Daily/Weekly
                    // so an offer carrying both surfaces both notices.
                    // Same warn-only semantics and zero-means-uncapped
                    // convention as above.
                    if (batch.SeasonalCap.HasValue && batch.SeasonalCap.Value > 0 &&
                        unitsNeeded > batch.SeasonalCap.Value)
                    {
                        timegatedItems.Add(new TimegatedItem
                        {
                            ItemId = step.ItemId,
                            CapType = TimegatedCapType.Seasonal,
                            CapValue = batch.SeasonalCap.Value,
                            NeededCount = unitsNeeded,
                        });
                    }
                }

                // Conflict intentionally produces no cap notice - a cap
                // cannot be soundly computed across genuinely different
                // offers (see this method's doc comment).
                if (step.VendorCurrencyCosts != null)
                {
                    foreach (var cc in step.VendorCurrencyCosts)
                    {
                        currencyMap[cc.Id] = currencyMap.TryGetValue(cc.Id, out var existing)
                            ? checked(existing + cc.Count)
                            : cc.Count;
                    }
                }
            }

            return timegatedItems;
        }

        /// <summary>
        /// Redistributes each FinalizeVendorBatches-corrected merged vendor
        /// step's true aggregate TotalCost back to the individual per-
        /// occurrence memo (Decision) entries that fed it - without this,
        /// CraftingTreeNode.SubtreeCost (via the public Decisions dict)
        /// kept showing the stale, per-occurrence-overcounted sum after
        /// FinalizeVendorBatches corrected only the merged PlanStep/
        /// currencyMap view.
        ///
        /// Only touches stepKeys FinalizeVendorBatches actually corrected
        /// (step.VendorOfferOutputCount &gt; 0 - only ever set inside that
        /// method's own single-winning-offer branch, 0 for the Conflict/
        /// mixed-offer case - see FinalizeVendorBatches). When occurrences
        /// disagreed on the winning offer, each occurrence's own memo
        /// TotalCost is already individually correct (a genuinely different
        /// real purchase), so redistributing a uniform rate across them
        /// would REPLACE correct values with a wrong blended one - the same
        /// reasoning FinalizeVendorBatches itself already applies to
        /// step.TotalCost.
        ///
        /// Allocation is largest-remainder (Hamilton) apportionment,
        /// proportional to each occurrence's own Quantity share of the
        /// step's total demand: floor(step.TotalCost * quantity /
        /// totalQuantity) per occurrence, then the leftover copper(s) -
        /// step.TotalCost minus the sum of floors, always fewer than
        /// occurrences.Count - go one each to the occurrences with the
        /// largest fractional remainder (numerator mod totalQuantity),
        /// ties broken by first-seen (DFS) order for determinism. The
        /// allocated shares always sum to precisely step.TotalCost - no
        /// drift, no invented precision - and any two occurrences of
        /// equal quantity diverge by at most 1 copper. The multiply
        /// widens to decimal so this holds unconditionally - no long
        /// overflow is possible for any step.TotalCost/Quantity pair.
        /// A "last occurrence absorbs the remainder" shape is not
        /// acceptable here: it dumps the entire batch-overrun cost,
        /// unbounded for equal-quantity occurrences, onto whichever
        /// occurrence lands last in DFS order.
        ///
        /// A component leaf's raw VendorItemCosts/VendorCurrencyCosts
        /// (captured pre-merge, per occurrence) are NOT re-derived here -
        /// they can disagree with the corrected share whenever a step
        /// merges 2+ occurrences. The caller reads this method's outputs
        /// afterward to mark which decisions must suppress component-leaf
        /// display (see FlagUnreliableVendorComponentCosts).
        /// </summary>
        internal void AllocateVendorNodeCosts(
            Dictionary<(int, AcquisitionSource, int), PlanStep> stepMap,
            Dictionary<(int, AcquisitionSource, int), List<(int NodeId, int Quantity)>> vendorOccurrences,
            Dictionary<int, PlanSolver.Decision> memo)
        {
            foreach (var kvp in vendorOccurrences)
            {
                if (!stepMap.TryGetValue(kvp.Key, out var step) || step.VendorOfferOutputCount <= 0)
                {
                    continue;
                }

                var occurrences = kvp.Value;

                long totalQuantity = 0L;
                for (int i = 0; i < occurrences.Count; i++)
                {
                    totalQuantity += occurrences[i].Quantity;
                }

                if (totalQuantity <= 0)
                {
                    // Defensive only: vendorOccurrences' construction
                    // (PlanSolver.AggregateStep) never records a
                    // non-positive Quantity in practice. Leaves this
                    // step's occurrences untouched rather than divide by
                    // zero.
                    continue;
                }

                var shares = new long[occurrences.Count];
                var remainders = new long[occurrences.Count];
                long allocated = 0L;
                for (int i = 0; i < occurrences.Count; i++)
                {
                    // Overflow: step.TotalCost * quantity can
                    // exceed long range (up to totalQuantity times larger
                    // than the old UnitCost * quantity product this shape
                    // replaced), and on wrap the numerator goes negative,
                    // silently breaking the sum-to-step.TotalCost invariant
                    // below. Widened to decimal for the multiply/divide:
                    // step.TotalCost is long (<= ~9.2e18) and Quantity is
                    // int (<= ~2.1e9), so their product (<= ~1.98e28) is
                    // always well inside decimal's range (~7.9e28) - no
                    // overflow possible for any value either operand's own
                    // type can hold. Both operands are whole coppers, so
                    // truncating back to long after the divide is exact,
                    // matching the prior integer-division floor.
                    decimal numerator = (decimal)step.TotalCost * occurrences[i].Quantity;
                    shares[i] = (long)(numerator / totalQuantity);
                    remainders[i] = (long)(numerator % totalQuantity);
                    allocated += shares[i];
                }

                long leftover = step.TotalCost - allocated;
                if (leftover > 0)
                {
                    var byLargestRemainder = Enumerable.Range(0, occurrences.Count)
                        .OrderByDescending(i => remainders[i])
                        .ThenBy(i => i);
                    foreach (int i in byLargestRemainder)
                    {
                        if (leftover <= 0)
                        {
                            break;
                        }

                        shares[i]++;
                        leftover--;
                    }
                }

                for (int i = 0; i < occurrences.Count; i++)
                {
                    if (memo.TryGetValue(occurrences[i].NodeId, out var decision))
                    {
                        decision.TotalCost = shares[i];
                        memo[occurrences[i].NodeId] = decision;
                    }
                }
            }
        }

        /// <summary>
        /// Structural equality for the fields that determine whether two
        /// tree occurrences of the same item genuinely used the same
        /// vendor offer (see FinalizeVendorBatches). CurrencyCostLinesPerBatch
        /// is compared by content/order, not reference - both occurrences'
        /// lists ultimately come from the same offer's own CostLines, built
        /// independently but identically each time EvaluateVendorOffers
        /// scans that item's offer list.
        /// </summary>
        internal bool VendorBatchesEqual(VendorOfferBatch a, VendorOfferBatch b)
        {
            if (a.OutputCount != b.OutputCount || a.CoinCostPerBatch != b.CoinCostPerBatch)
            {
                return false;
            }

            var linesA = a.CurrencyCostLinesPerBatch;
            var linesB = b.CurrencyCostLinesPerBatch;
            if (linesA == null || linesB == null)
            {
                return linesA == null && linesB == null;
            }

            if (linesA.Count != linesB.Count)
            {
                return false;
            }

            for (int i = 0; i < linesA.Count; i++)
            {
                if (linesA[i].Id != linesB[i].Id ||
                    linesA[i].Count != linesB[i].Count ||
                    !string.Equals(linesA[i].Type, linesB[i].Type, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Scales a per-batch (one purchase's worth) currency cost-line list
        /// by the number of purchases, clamping to int.MaxValue rather than
        /// overflowing a CostLine's int Count (mirrors the identical clamp
        /// in MergeVendorCurrencyCosts).
        /// </summary>
        internal List<CostLine> ScaleCostLines(List<CostLine> perBatch, int unitsNeeded)
        {
            if (perBatch == null || perBatch.Count == 0)
            {
                return null;
            }

            var scaled = new List<CostLine>(perBatch.Count);
            foreach (var line in perBatch)
            {
                long count = (long)line.Count * unitsNeeded;
                scaled.Add(new CostLine
                {
                    Type = line.Type,
                    Id = line.Id,
                    Count = ClampToInt(count),
                });
            }

            return scaled;
        }

        /// <summary>
        /// Clamps a long to int.MaxValue rather than overflowing a
        /// CostLine's int Count - shared by MergeVendorCurrencyCosts and
        /// ScaleCostLines, the two places a currency amount can grow beyond
        /// int range (summing across occurrences, or scaling by a purchase
        /// count).
        /// </summary>
        private static int ClampToInt(long value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
