using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// WP-15 (architecture S4a): the vendor-batching sub-engine extracted
    /// from PlanSolver - the batch-shape state types plus the six methods
    /// that turn per-occurrence vendor-offer evaluations into one true
    /// per-item merged-ceil cost and post-solve "timegated" cap notice
    /// (EvaluateVendorOffers, FinalizeVendorBatches, AllocateVendorNodeCosts,
    /// MergeVendorCurrencyCosts, VendorBatchesEqual, ScaleCostLines).
    /// PlanSolver holds one instance as an injected collaborator (see its
    /// constructor); every method here was byte-for-byte the same body it
    /// had as a PlanSolver private static at the time of this move - a
    /// class move, not a rewrite. The merged-ceil arithmetic itself was
    /// DO-NOT-TOUCH at the time (m38-cleanup-plan.md #7, KNOWN-ISSUES
    /// #20.1/#20.2/#28 - the Obsidian Shard 179-&gt;180-not-186 repro) and
    /// was unchanged by the move itself. That freeze was retired
    /// 2026-08-17 (characterization-first proof required, see
    /// KNOWN-ISSUES) and AllocateVendorNodeCosts has since been rewritten
    /// (largest-remainder/Hamilton apportionment - see that method's own
    /// doc comment), so "byte-for-byte"/"unchanged" no longer describes
    /// the class as a whole. The nested types below were `private` on
    /// PlanSolver; they are
    /// `internal` here only because PlanSolver (a different class, same
    /// assembly) still needs to declare fields/locals of these types -
    /// still no wider than the original private scope from outside this
    /// assembly.
    /// <para>See docs/ARCHITECTURE.md section 7 (M38 WP-27).</para>
    /// </summary>
    public class VendorBatchSolver
    {
        // See PlanSolver.Decision.VendorBatch's doc comment.
        internal struct VendorOfferBatch
        {
            public int OutputCount;
            public long CoinCostPerBatch;
            public List<CostLine> CurrencyCostLinesPerBatch;
            public int? DailyCap;
            public int? WeeklyCap;

            // Astral Acclaim package (KNOWN-ISSUES #33): Wizard's Vault
            // seasonal purchase cap. Independent of DailyCap/WeeklyCap -
            // see FinalizeVendorBatches, which checks it separately so an
            // offer carrying both a Seasonal cap and a Daily/Weekly cap can
            // surface both notices.
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

            // M37 (KNOWN-ISSUES #24/#25 3.3, gw2e parity - the Homestead
            // Refinement cap-notice gap) previously added a second, coarser
            // (CapDailyCap, CapWeeklyCap, CapConflict) ratchet here so a
            // mixed-offer step could still sum a cap notice when every
            // occurrence's offer agreed on the raw cap tuple, even if the
            // full batch shape (Conflict above) disagreed. Reverted:
            // adversarial review found the premise false - the wiki's
            // per-row WeeklyCap is a template parameter, not a confirmed
            // per-station aggregate (see KNOWN-ISSUES #24's "Cap data"
            // note), so two occurrences agreeing on that raw number does
            // not mean they agree on a real shared limit worth summing
            // against. Conflict alone continues to suppress the notice for
            // this step, as it did before this milestone - see
            // FinalizeVendorBatches and the MixedOffer*_DocumentedLimitation
            // tests in PlanSolverTests.
        }

        /// <summary>
        /// EvaluateVendorOffers' result (WP-11, simplify #4 - was 7
        /// out-params). Pure data carrier, mirrors the M37
        /// SellSideEconomics.PerItemEconomics pattern: a readonly
        /// struct returned by value from a single call site instead of
        /// mutated out-parameters. Field meanings are exactly the former
        /// out-params of the same name (see EvaluateVendorOffers' own doc
        /// comment for the comparable/fallback split, valuation rules, and
        /// cap-notice plumbing this carries) - this is a shape change
        /// only, not a behavior change.
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

            // W4B (vendor cost-component leaves): additive outputs alongside
            // the pre-existing fields above - captured at the SAME per-line
            // multiplication EvaluateVendorOffers already performed for
            // every offer (never a second/divergent computation), purely so
            // a display-tree leaf can be built from real, already-computed
            // numbers. BestComparableItemCosts/FallbackItemCosts are the
            // winning offer's TP-valued Item cost lines (Type=="Item" on the
            // raw offer), scaled to this occurrence's unitsNeeded - null
            // when the winning offer had none. BestComparableHasRawCoin/
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
        /// M37 (KNOWN-ISSUES #24, gw2e parity): before any of the above, a
        /// Homestead Refinement offer (VendorOffer.HomesteadTier.HasValue)
        /// whose tagged tier exceeds <paramref name="homesteadTiers"/>'
        /// configured tier for that output material is skipped entirely -
        /// it never competes as comparable OR fallback. Fixes a live
        /// defect (not merely a modeling gap): the baseline seed already
        /// carries all 236 wiki-scraped Homestead Refinement rows
        /// unconditionally, so before this gate the solver silently
        /// behaved as if every account had every efficiency upgrade.
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
        /// V2 purchase-cap semantics (M34-B1 #3, gw2efficiency parity): a
        /// DailyCap/WeeklyCap NEVER excludes an offer or affects which tier
        /// it lands in - gw2efficiency itself only ever surfaces a cap as a
        /// post-solve "this is timegated" notice (dailyCooldowns.ts), it
        /// never re-routes the tree. Both tiers below carry the offer's raw
        /// DailyCap/WeeklyCap through via <see cref="VendorOfferBatch"/> so
        /// FinalizeVendorBatches can produce that notice once, against the
        /// item's AGGREGATE (post-merge) demand rather than any single tree
        /// occurrence's local quantity. SeasonalCap (Astral Acclaim package,
        /// KNOWN-ISSUES #33) is carried through the exact same way and
        /// checked independently of Daily/Weekly by FinalizeVendorBatches.
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

            // W4B: see VendorOfferEvaluation's own doc comment.
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

                // M37 (KNOWN-ISSUES #24, gw2e parity): a Homestead
                // Refinement offer whose tagged tier exceeds the user's
                // configured tier for that output material is excluded
                // entirely - never comparable, never a fallback. Keyed on
                // offer.OutputItemId (not a merchant-name string match at
                // this hot-path call site) because HomesteadTier is only
                // ever set on rows the seeding pass already confirmed carry
                // a merchant name containing "Homestead Refinement" (see
                // ConvertToOffer/HomesteadTierResolver) - the family
                // mapping gw2e itself keys on (cheapestTree.ts's
                // merchant.name.includes('Homestead Refinement') check) is
                // therefore already baked into which rows have a non-null
                // tag, so re-checking the merchant name string here on
                // every offer/every solve would be redundant string work in
                // a loop that already runs per vendor offer per tree node.
                if (offer.HomesteadTier.HasValue &&
                    offer.HomesteadTier.Value > homesteadTiers.GetTier(offer.OutputItemId))
                {
                    continue;
                }

                long coinCost = 0;
                bool priceable = true;
                var currencyCosts = new List<CostLine>();

                // W4B: raw (unscaled, per-batch) capture of this offer's
                // coin-presence and Item cost lines, purely additive - read
                // alongside the existing coinCost fold below, never
                // altering it. hasRawCoin distinguishes a genuine coin cost
                // line from coin that only exists because an Item line was
                // folded in (see VendorOfferEvaluation.BestComparableHasRawCoin's
                // doc comment). itemCostRaw's (Count, UnitPrice) pair is
                // exactly what already fed the `coinCost += cost.Count *
                // unitPrice` line below - captured here so a display leaf
                // can be built from it after unitsNeeded scaling, without
                // ever recomputing the multiplication. PriceSideFellBack
                // (AUDIT ROW 20/38 review-fix) is this same line's own
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
                            // W4B review-fix (Must Fix): guard with
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
                        // AUDIT ROW 20/38 review-fix (DISPLAY CAVEAT gap):
                        // the 3-arg GetUnitPrice overload replaces the
                        // 2-arg one used here previously so this barter
                        // item's own fell-back-side fact is captured, not
                        // just discarded - see itemCostRaw's doc comment
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
                            // W4B review-fix (Must Fix): guard the raw
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
                        // Adversarial-review finding (2026-08-16, class-level
                        // sibling of the recipe-ingredient Item-positive
                        // sweep): an unrecognized CostLine.Type must never be
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

                // W4B: scale itemCostRaw by this occurrence's unitsNeeded -
                // the exact same scale factor totalCoinCost above just
                // applied. GoldValue below is (unscaled Count * unitPrice)
                // * unitsNeeded, i.e. byte-for-byte the same arithmetic
                // that line's own contribution to totalCoinCost already
                // used - never a second, potentially-diverging computation.
                // Guarded the same way the currency scaling below guards
                // CostLine.Count (int): a scaled quantity too large for
                // VendorItemCostLine.Quantity (int) skips the offer rather
                // than truncating it silently.
                //
                // W4B review-fix note: a follow-up review flagged this
                // `itemsScalable`/`continue` guard as new control flow added
                // inside EvaluateVendorOffers (one of the six DO-NOT-TOUCH
                // merged-ceil batching methods), asking for it to be either
                // explicitly justified or rewritten as a non-disqualifying
                // clamp. Kept as-is, deliberately: it is structurally
                // identical to - and only extends to a second cost
                // dimension - the pre-existing `scalable`/`continue` guard a
                // few lines below for the currency lines (same file, same
                // loop, same overflow-safety shape, predates this feature),
                // so it introduces no new KIND of control flow, only
                // coverage for a new field. It can only fire when a single
                // occurrence's scaled Item-cost quantity exceeds
                // int.MaxValue (billions of units of one vendor item in one
                // purchase) - unreachable with real GW2 data. Rewriting it
                // as a clamp instead (silently truncating the represented
                // cost/quantity rather than skipping the offer) would be
                // the actual behavior change and strictly worse: a clamped
                // value is silently wrong, while skipping the offer here -
                // exactly like its currency sibling - never touches
                // TotalCost/UnitCost/batch selection for any realistic
                // input and fails safe rather than silently.
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
                            PriceSideFellBack = ic.PriceSideFellBack
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
                        bestComparableItemCosts = scaledItemCosts;
                        bestComparableHasRawCoin = hasRawCoin;
                        bestComparableBatch = new VendorOfferBatch
                        {
                            OutputCount = offer.OutputCount,
                            CoinCostPerBatch = coinCost,
                            CurrencyCostLinesPerBatch = currencyCosts.Count > 0 ? currencyCosts : null,
                            DailyCap = offer.DailyCap,
                            WeeklyCap = offer.WeeklyCap,
                            SeasonalCap = offer.SeasonalCap
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
                        SeasonalCap = offer.SeasonalCap
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
                        Count = ClampToInt(summed)
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
        /// M34-B1 #1/#3: re-derives every merged BuyFromVendor PlanStep's
        /// true cost from its AGGREGATE Quantity (summed across every tree
        /// occurrence by AggregateStep) and the winning offer's batch shape,
        /// ceiling the purchase count exactly ONCE - matching gw2efficiency's
        /// own documented convention for bulk-output steps (`docs/gw2e-parity-spec.md`
        /// Section 6.5: quantities are merged across the whole tree before
        /// `Math.ceil` ever runs). This replaces the sum of several
        /// already-independently-ceil'd per-occurrence costs (AggregateStep's
        /// running total), which overstates the true cost whenever the same
        /// item is needed via 2+ tree occurrences and bought via a bulk
        /// (OutputCount > 1) offer - see PlanSolverTests for the exact
        /// 4/4/4/83/84 -&gt; 179 -&gt; 180 (not 186) live repro.
        ///
        /// Only applied when every occurrence of that item resolved to the
        /// IDENTICAL winning offer (vendorBatchTracking's Conflict flag is
        /// false) - a node's own per-occurrence ceil can, at a different
        /// local quantity, legitimately prefer a different offer (bulk
        /// discount thresholds), and re-deriving a single "true" cost across
        /// genuinely different offers has no principled answer. When
        /// occurrences disagree, this step is left exactly as AggregateStep
        /// already computed it (sum of real, individually-correct
        /// per-occurrence purchases) - a documented, intentionally
        /// conservative fallback, not a regression.
        ///
        /// Also folds every vendor step's final (possibly just-recomputed)
        /// VendorCurrencyCosts into currencyMap - the single place vendor
        /// currency contributions reach the plan-wide currency total, now
        /// that Collect no longer folds the still-per-occurrence amounts in
        /// directly (see PlanSolver.Collect's BuyFromVendor branch) - and
        /// collects a post-solve "timegated" notice (gw2e parity, M34-B1
        /// #3) for any uniform step whose aggregate purchase count exceeds
        /// the winning offer's daily (preferred) or weekly cap, PLUS an
        /// independent second notice (Astral Acclaim package, KNOWN-ISSUES
        /// #33) when that same offer also carries a SeasonalCap the
        /// aggregate exceeds - the two checks do not suppress each other,
        /// since Daily/Weekly and Seasonal are unrelated real-world limits
        /// (e.g. a Wizard's Vault offer's per-season Astral Acclaim cap).
        /// Caps never exclude an offer or change Source/TotalCost - purely
        /// informational.
        ///
        /// The recomputed step.UnitCost (M34 fix, sibling to B1 #2's
        /// identical currency-side fix) is the winning offer's own
        /// CoinCostPerBatch/OutputCount rate, not a truncating total/
        /// Quantity average of the just-corrected aggregate - see the
        /// inline comment at the assignment for the exact misleading-price
        /// example this replaces. Unlike the currency "Each" cell
        /// (CurrencyDisplayResolver.ResolveUnitAmounts), PlanStep.UnitCost/
        /// PlanRowViewModel.UnitCoinValue are plain non-nullable longs with
        /// no "N for M" bundle-label concept, so a non-evenly-divisible rate
        /// still truncates here rather than gaining new model/UI surface for
        /// a MustFix-level display nuance - a deliberate, narrower scope
        /// than the currency fix, not an oversight.
        ///
        /// M37 (KNOWN-ISSUES #24/#25 3.3) investigated a second branch here
        /// that would still sum a cap notice when occurrences disagreed on
        /// the winning offer's batch shape (Conflict true) but agreed on the
        /// raw (DailyCap, WeeklyCap) tuple - targeting the Homestead
        /// Refinement case, where many distinct input-material offers for
        /// the same output all carry an identical WeeklyCap. Adversarial
        /// review found the premise false (that shared number is the wiki's
        /// per-row template parameter, not a confirmed per-station
        /// aggregate - see KNOWN-ISSUES #24's "Cap data" note) and the
        /// summing itself unsound across occurrences that share only a
        /// subset of one offer, so this was reverted: Conflict alone still
        /// suppresses the notice for this step, as it did before this
        /// milestone. See the MixedOffer*_DocumentedLimitation tests in
        /// PlanSolverTests.
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
                    // M34 fix (MustFix review finding, PlanSolver.cs:1062):
                    // the coin "Each" cell must show the winning offer's own
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
                            NeededCount = unitsNeeded
                        });
                    }

                    // Astral Acclaim package (KNOWN-ISSUES #33): SeasonalCap
                    // is checked independently of Daily/Weekly above - not
                    // folded into the same "pick one" cap variable - so an
                    // offer carrying both a Seasonal cap and a Daily/Weekly
                    // cap surfaces BOTH notices when both are exceeded,
                    // rather than one suppressing the other the way Daily
                    // takes precedence over Weekly. Same warn-only semantics
                    // (never gates or reroutes the solve) and zero-cap
                    // convention (an explicit 0 means uncapped) as the
                    // Daily/Weekly check above.
                    if (batch.SeasonalCap.HasValue && batch.SeasonalCap.Value > 0 &&
                        unitsNeeded > batch.SeasonalCap.Value)
                    {
                        timegatedItems.Add(new TimegatedItem
                        {
                            ItemId = step.ItemId,
                            CapType = TimegatedCapType.Seasonal,
                            CapValue = batch.SeasonalCap.Value,
                            NeededCount = unitsNeeded
                        });
                    }
                }
                // Conflict == true (occurrences disagreed on the winning
                // offer's exact batch shape) intentionally produces no cap
                // notice here - see this method's doc comment and
                // KNOWN-ISSUES #24's "Cap data" note for why a cap notice
                // cannot be soundly computed across genuinely different
                // offers with only a wiki-scraped per-row cap number to
                // compare against.

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
        /// occurrence memo (Decision) entries that fed it - the fix for the
        /// Critical review finding that CraftingTreeNode.SubtreeCost (via
        /// the public Decisions dict) kept showing the stale, per-
        /// occurrence-overcounted sum after FinalizeVendorBatches corrected
        /// only the merged PlanStep/currencyMap view.
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
        /// EQUAL quantity now diverge by at most 1 copper. The multiply
        /// widens to decimal (see the loop below) specifically so this
        /// holds unconditionally - no long overflow is possible for any
        /// step.TotalCost/Quantity pair either field's own type can hold.
        ///
        /// Quorum verdict C6 (merged-ceil-remainder stream, 2026-08):
        /// replaces the prior "UnitCost * quantity per occurrence, last
        /// occurrence absorbs everything else" shape, which gave every
        /// non-last occurrence only its own per-unit rate (no share of
        /// any wasted batch-overrun cost) while dumping the ENTIRE
        /// overrun - unbounded for equal-quantity occurrences - onto
        /// whichever occurrence happened to land last in DFS order. See
        /// PlanSolverVendorBatchingTests.
        /// MultiOccurrenceEqualQuantityBulkVendorOffer_BatchOverrunSharedProportionally
        /// (renamed from ..._PreFix_LastOccurrenceAbsorbsEntireBatchOverrun by
        /// this same fix) for the canonical repro: two 1-unit
        /// occurrences of a "100 for 1000c" bulk offer used to render
        /// 10/990; now render 500/500 (1000 * 1/2 = 500 exactly, no
        /// remainder to distribute). The flagship 179-unit/"3 for 3"
        /// regression shape (4/4/4/83/84 quantities, step.TotalCost 180)
        /// is UNCHANGED by this: floors 4/4/4/83/84 sum to 179, leaving a
        /// single leftover copper that lands on the 84-quantity occurrence
        /// (remainder 84/179, the largest), giving the same 4/4/4/83/85
        /// split the prior algorithm happened to also produce for that
        /// specific shape - see
        /// MultiOccurrenceBulkVendorOffer_CorrectionPropagatesThroughFourCraftLevelsAndBranches.
        ///
        /// W4B review-fix note: a component leaf's raw VendorItemCosts/
        /// VendorCurrencyCosts (captured pre-merge, per occurrence, by
        /// EvaluateVendorOffers) are NOT re-derived here the way TotalCost
        /// is - they can disagree with the corrected share this method
        /// computes whenever a step merges 2+ tree occurrences, and that
        /// stays true across the C6 largest-remainder rewrite above: only
        /// the TotalCost allocation shape changed, not this gap. (Formerly
        /// DO-NOT-TOUCH: merged-ceil batching math - retired 2026-08-17;
        /// changes here now require characterization-first proof, see
        /// KNOWN-ISSUES.) The caller (PlanSolver.Solve, see FlagUnreliableVendorComponentCosts)
        /// reads this method's own already-public outputs (vendorOccurrences,
        /// stepMap) AFTER it returns to mark which decisions must suppress
        /// component-leaf display for that reason - see
        /// CraftingTreeBuilder.BuildVendorCostComponentLeaves.
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
                    // Review fix (overflow): step.TotalCost * quantity can
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
                    Count = ClampToInt(count)
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
