using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class PlanViewModelBuilder
    {
        public PlanViewModel Build(CraftingPlanResult result)
        {
            // M35 (gw2efficiency parity - multi-item plans): RequestedItems
            // is populated ONLY for a genuine multi-item batch (2+ items -
            // see CraftingPlanResult.RequestedItems' own doc comment); a
            // single-item request, including one made through the
            // multi-item entry point, always has it null and continues
            // through the untouched single-item branch below byte-for-byte.
            bool isMultiItem = result.RequestedItems != null && result.RequestedItems.Count > 1;

            var vm = new PlanViewModel
            {
                TargetQuantity = isMultiItem ? 0 : result.Plan.TargetQuantity,
                TreeRoot = isMultiItem ? null : result.CraftingTree,
                MultiItemRoots = isMultiItem ? result.MultiItemRoots : null,
                CurrencyMetadata = result.CurrencyMetadata,
                PriceBasis = result.PriceBasis,
                // currency-ux-package (Feature 2): whole-plan currency
                // totals/holding, unaffected by isMultiItem - result.Plan
                // is already the single combined Plan object for a batch
                // (same source BuildCurrencyTableRows already reads for the
                // Summary section's currency table), so this passthrough
                // needs no branching.
                CurrencyPlanTotals = BuildCurrencyPlanTotals(result.Plan.CurrencyCosts),
                OwnedCurrencyAmounts = result.OwnedCurrencyAmounts,
                // currency-ux-package (Feature 3): same whole-plan-source
                // reasoning as CurrencyPlanTotals above - result.Plan is
                // already the single combined Plan for a multi-item batch.
                VendorCapsByItemId = BuildVendorCapsByItemId(result.Plan.TimegatedItems)
            };

            if (isMultiItem)
            {
                // No single target item/icon/rarity exists for a batch - the
                // header instead shows gw2e's own document-title convention
                // ("Gift of Exordium and 2 others" - see the M34 r1 multi-
                // item research report) and TargetQuantity is suppressed
                // (0) so CraftingPlanView's existing "x{qty}" suffix never
                // renders a meaningless combined number.
                vm.TargetItemName = BuildMultiItemTitle(result.RequestedItems, result.ItemMetadata);
                vm.TargetIconUrl = null;
                vm.TargetRarity = null;
            }
            else if (result.ItemMetadata != null &&
                result.ItemMetadata.TryGetValue(result.Plan.TargetItemId, out var targetMeta))
            {
                vm.TargetItemName = !string.IsNullOrEmpty(targetMeta.Name)
                    ? targetMeta.Name
                    : "Unknown Item";
                vm.TargetIconUrl = targetMeta.IconUrl;
                vm.TargetRarity = targetMeta.Rarity;
            }
            else
            {
                vm.TargetItemName = "Unknown Item";
                vm.TargetIconUrl = null;
                vm.TargetRarity = null;
            }

            // Section emission order mirrors gw2efficiency's calculator page
            // (header/cost-breakdown -> recipe-tree -> used-owned-materials ->
            // shopping-list -> required-disciplines -> required-recipes ->
            // crafting-steps). The recipe tree itself is not in this list
            // (it renders from vm.TreeRoot, positioned second by the view);
            // everything else below is exactly the gw2e ordering.

            // 1. Total Cost section (always present)
            vm.Sections.Add(BuildSummarySection(result, isMultiItem));

            // 2. Used Materials section (only if non-null and non-empty)
            if (result.UsedMaterials != null && result.UsedMaterials.Count > 0)
            {
                vm.Sections.Add(BuildUsedMaterialsSection(result));
            }

            // Partition steps by source
            var shoppingSteps = result.Plan.Steps
                .Where(s => s.Source != AcquisitionSource.Craft)
                .ToList();
            var craftSteps = result.Plan.Steps
                .Where(s => s.Source == AcquisitionSource.Craft)
                .ToList();

            // 3. Shopping List section (only if non-empty)
            if (shoppingSteps.Count > 0)
            {
                vm.Sections.Add(BuildShoppingListSection(shoppingSteps, result));
            }

            // 4. Required Disciplines section (only if non-empty)
            if (result.RequiredDisciplines != null && result.RequiredDisciplines.Count > 0)
            {
                vm.Sections.Add(BuildDisciplinesSection(result));
            }

            // 5. Required Recipes section (only if there is at least one
            // non-Mystic-Forge recipe left to learn once BuildRecipesSection
            // filters MF-only rows out - wave-3 quick win #2. A plan whose
            // only "recipes" are Mystic Forge combinations now surfaces no
            // Required Recipes section at all rather than an empty one.
            if (result.RequiredRecipes != null && result.RequiredRecipes.Count > 0)
            {
                var recipesSection = BuildRecipesSection(result);
                if (recipesSection.Rows.Count > 0)
                {
                    vm.Sections.Add(recipesSection);
                }
            }

            // 6. Crafting Steps section (only if non-empty, OR there is a
            // timegated notice to show - M34-B1 #3) - last, per gw2e order
            bool hasTimegatedItems = result.Plan.TimegatedItems != null && result.Plan.TimegatedItems.Count > 0;
            if (craftSteps.Count > 0 || hasTimegatedItems)
            {
                vm.Sections.Add(BuildCraftingStepsSection(craftSteps, result));
            }

            // 7. Notes section (design-plan-notes.md, Option 1) - only if
            // it has at least one note to show. Last, per that design's
            // section 5: every note kind is a caveat ABOUT facts shown in
            // an earlier section (excess reclaim references craft-step
            // quantities; competency notes reference the Required
            // Disciplines rows just above; the forge-scope note is a
            // "read this after you've seen the plan" caveat).
            var notesSection = BuildNotesSection(result);
            if (notesSection.Rows.Count > 0)
            {
                vm.Sections.Add(notesSection);
            }

            return vm;
        }

        /// <summary>
        /// gw2e's own document-title convention for a multi-item batch
        /// (M34 r1 report): the first requested item's name, plus " and N
        /// other(s)" when 2+ items are selected. items is guaranteed
        /// non-empty by the isMultiItem gate above (2+ entries).
        /// </summary>
        private static string BuildMultiItemTitle(
            IReadOnlyList<PlanRequestItem> items, IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            string firstName = ResolveName(items[0].ItemId, metadata);
            int rest = items.Count - 1;
            if (rest <= 0)
            {
                return firstName;
            }
            return $"{firstName} and {rest} other" + (rest > 1 ? "s" : "");
        }

        // W4A (Total Cost section redesign) tooltip bodies - shared between
        // the collapsed and uncollapsed cost-band branches below (both
        // still show an "Actual Cost to Craft" tile with the identical
        // meaning) and reused verbatim by the tests, so the exact wording
        // lives in exactly one place.
        internal const string TotalMaterialsValueTooltip =
            "Full market value of everything this craft consumes - coins you spend plus the sell value of your own materials used.";
        internal const string YourMaterialsUsedTooltip =
            "Instant-sell value (after 15% TP fees) of materials you already own that this plan consumes - what you give up by using them instead of selling them.";
        internal const string ActualCostTooltip =
            "What you still pay out of pocket - materials you already own are subtracted before pricing.";
        internal const string SellValueTooltip =
            "Instant-sell revenue after 15% TP fees.";
        internal const string ProfitTooltip =
            "Sell Value minus Total Materials Value.";
        internal const string FootnoteText =
            "Prices are Trading Post data - actual purchase and sale prices are likely to vary.";

        // Review fix: distinct caption for Band 2's middle tile in a
        // multi-item batch - see BuildProfitFormulaBand's own doc comment
        // for why the two bands' "Total Materials Value" can legitimately
        // hold DIFFERENT numbers for a batch with a partially-unsellable
        // root. A tooltip-only distinction was not enough: two
        // identically-captioned tiles ~56px apart showing different
        // numbers reads as a bug, not a scoping nuance, in a section whose
        // whole point is to read as a balancing formula at a glance.
        internal const string MaterialsValueSellableLabel = "Materials Value (sellable)";

        private PlanSectionViewModel BuildSummarySection(CraftingPlanResult result, bool isMultiItem)
        {
            var section = new PlanSectionViewModel
            {
                SectionType = PlanSectionType.Summary,
                Title = "Total Cost",
                IsDefaultExpanded = true
            };

            BuildCostFormulaBand(section, result);
            BuildProfitFormulaBand(section, result, isMultiItem);
            BuildCurrencyTableRows(section, result);

            // M35 (gw2efficiency parity - multi-item plans): echoes gw2e's
            // own Cost Breakdown banner concept for a multi-item batch (M34
            // r1 report). M37 (KNOWN-ISSUES #25) added the real batch-level
            // Sell value/Profit rows above (see
            // SellSideEconomics.ApplyBatchSellSideEconomics) - gated on
            // the SAME result.NetSaleValue.HasValue condition as those rows
            // (mirroring gw2e's own shared ng-show condition, research
            // report Section 1.3b) so this note never references a profit
            // figure that is not actually on the page (e.g. every requested
            // root bought outright, or none tradable). The wording is NOT
            // gw2e's own verbatim banner text ("...sum of all crafted
            // recipes") because this module's rollup has no craft-vs-buy
            // filter at all (SellSideEconomics.ApplyBatchSellSideEconomics'
            // own doc comment, divergence item 1) - a bought-but-tradable
            // root can contribute too, so "crafted recipes" would be
            // inaccurate.
            if (isMultiItem && result.NetSaleValue.HasValue)
            {
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.MultiItemNote,
                    Label = "Sell value and profit are the sum across every requested item that has a live Trading Post sell price."
                });
            }

            // W4A (user-mandated): a single subdued footnote, always
            // present, at the very bottom of the section.
            section.Rows.Add(new PlanRowViewModel
            {
                RowType = PlanRowType.SummaryFootnote,
                Label = FootnoteText
            });

            return section;
        }

        /// <summary>
        /// W4A formula band 1 ("Total Materials Value - Your Materials Used
        /// = Actual Cost to Craft"). COLLAPSE RULE (user-mandated): when
        /// MaterialOpportunityCost is null or 0 (Value-own-materials off,
        /// or nothing owned consumed) the formula's middle term does not
        /// exist, so the band collapses to a single "Actual Cost to Craft"
        /// tile instead of a meaningless 3-term formula. Actual Cost to
        /// Craft is exactly the pre-W4A "Total" row (result.Plan.
        /// TotalCoinCost, unchanged math) - the "(buy-order prices)" basis
        /// qualifier that used to live in that row's Label now lives in
        /// this tile's tooltip instead (spec: "keep it somewhere sensible",
        /// and a formula-band caption needs to stay short).
        /// </summary>
        private static void BuildCostFormulaBand(PlanSectionViewModel section, CraftingPlanResult result)
        {
            long actualCost = result.Plan.TotalCoinCost;

            // Nice-to-have (soften unconditional basis claim): the old
            // suffix (" (buy-order prices)") read as an unqualified claim
            // that every item in this total priced on the buy-order side.
            // AUDIT ROW 20/38's per-item TP price-side fallback means that
            // is not always true - an item with no buy orders at all
            // still prices via its instant-buy side and folds into this
            // same total. The suffix now says so instead of overclaiming.
            string actualCostTooltip = result.PriceBasis == PriceBasis.BuyOrder
                ? ActualCostTooltip + " (buy-order prices, or instant-buy where an item has none)"
                : ActualCostTooltip;
            var actualCostTile = new PlanRowViewModel
            {
                RowType = PlanRowType.CostFormulaTile,
                Label = "Actual Cost to Craft",
                CoinValue = actualCost,
                TooltipText = actualCostTooltip
            };

            bool hasMaterialsUsed = result.MaterialOpportunityCost.HasValue && result.MaterialOpportunityCost.Value > 0;
            if (hasMaterialsUsed)
            {
                long materialsUsed = result.MaterialOpportunityCost.Value;
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.CostFormulaTile,
                    Label = "Total Materials Value",
                    CoinValue = actualCost + materialsUsed,
                    TooltipText = TotalMaterialsValueTooltip
                });
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.CostFormulaTile,
                    Label = "Your Materials Used",
                    CoinValue = materialsUsed,
                    TooltipText = YourMaterialsUsedTooltip
                });
            }

            // Collapsed case: this is the band's only tile. Uncollapsed:
            // it is the formula's rightmost ("= Actual Cost to Craft")
            // term, added last either way.
            section.Rows.Add(actualCostTile);
        }

        /// <summary>
        /// W4A formula band 2 ("Sell Value (after fees) - Total Materials
        /// Value = Profit if Sold"), only when result.NetSaleValue.HasValue -
        /// mirroring band 1, but never present without it (the profit
        /// formula is meaningless with no sell price at all).
        ///
        /// IDENTITY VERIFICATION (task-mandated): SellSideEconomics proves
        /// CraftingProfit == NetSaleValue - Plan.TotalCoinCost -
        /// (MaterialOpportunityCost ?? 0) for a SINGLE-ITEM plan
        /// (ApplySellSideEconomics, line ~66) but explicitly NOT for a
        /// multi-item batch (ApplyBatchSellSideEconomics subtracts only the
        /// SELLABLE roots' own cost, never Plan.TotalCoinCost, which also
        /// includes every unsellable requested root - see
        /// CraftingPlanResult.CraftingProfit's own doc comment, "NOT
        /// Plan.TotalCoinCost"). Band 1's "Total Materials Value" (Plan.
        /// TotalCoinCost + MaterialOpportunityCost) would therefore NOT
        /// balance this band's visible formula for a multi-item batch with
        /// any unsellable requested root - the middle tile is instead
        /// derived as NetSaleValue - CraftingProfit, reusing ONLY the two
        /// already-stored, already-correct fields (never recomputing
        /// CraftingProfit itself, never touching Plan.TotalCoinCost here).
        /// This is algebraically IDENTICAL to Band 1's Total Materials
        /// Value for every single-item plan (the identity above rearranges
        /// to exactly that), so the two bands always show the same number
        /// there; for a multi-item batch with a partially-unsellable root
        /// mix the two bands can legitimately differ (Band 1 prices the
        /// WHOLE batch, Band 2 only the batch's sellable portion, matching
        /// what CraftingProfit itself measures) - the tooltip below flags
        /// that case, AND (review fix) the tile's own Label changes to
        /// MaterialsValueSellableLabel for a multi-item batch so the
        /// divergence is visible without a mouseover: two tiles sharing
        /// the "Total Materials Value" caption but showing different
        /// numbers would read as a bug in the plan, not as a legitimate
        /// scoping difference.
        /// </summary>
        private static void BuildProfitFormulaBand(PlanSectionViewModel section, CraftingPlanResult result, bool isMultiItem)
        {
            if (!result.NetSaleValue.HasValue)
            {
                return;
            }

            long netSaleValue = result.NetSaleValue.Value;
            long profit = result.CraftingProfit ?? 0L;
            long totalMaterialsValue = netSaleValue - profit;

            string sellQualifier;
            if (isMultiItem)
            {
                sellQualifier = " (batch total across every requested item with a live sell price)";
            }
            else
            {
                sellQualifier = result.SellableQuantity > result.Plan.TargetQuantity
                    ? $" ({result.SellableQuantity}x, overproduction)"
                    : "";
            }

            bool hasCurrencyCosts = result.Plan.CurrencyCosts != null && result.Plan.CurrencyCosts.Count > 0;
            string profitQualifier;
            if (isMultiItem)
            {
                profitQualifier = hasCurrencyCosts ? " (batch total, coin costs only)" : " (batch total)";
            }
            else
            {
                profitQualifier = hasCurrencyCosts ? " (coin costs only)" : "";
            }

            // See this method's own doc comment - only a multi-item batch
            // can make this band's Total Materials Value diverge from Band
            // 1's own (whole-plan) figure, so only that case gets the extra
            // disambiguating clause.
            string totalMaterialsValueTooltip = isMultiItem
                ? TotalMaterialsValueTooltip + " (this band only covers items with a live sell price)"
                : TotalMaterialsValueTooltip;

            // Review fix: a multi-item batch gets its own caption for this
            // tile (see this method's own doc comment) - single-item plans
            // keep the plain "Total Materials Value" label, matching Band
            // 1 exactly (the identity proven above guarantees the two
            // numbers always agree there).
            string totalMaterialsValueLabel = isMultiItem
                ? MaterialsValueSellableLabel
                : "Total Materials Value";

            section.Rows.Add(new PlanRowViewModel
            {
                RowType = PlanRowType.ProfitFormulaTile,
                Label = "Sell Value",
                CoinValue = netSaleValue,
                TooltipText = SellValueTooltip + sellQualifier
            });
            section.Rows.Add(new PlanRowViewModel
            {
                RowType = PlanRowType.ProfitFormulaTile,
                Label = totalMaterialsValueLabel,
                CoinValue = totalMaterialsValue,
                TooltipText = totalMaterialsValueTooltip
            });

            // Review fix (round 2): FormulaResultIsExact false exactly when
            // profit < 0 - see that field's own doc comment. This is the
            // ONLY row in either band where it is ever set false; both
            // Band 1's collapsed/expanded tiles and this band's Sell
            // Value/Total Materials Value tiles keep the true default
            // (nothing to falsify - the field is only read on a band's
            // last tile).
            section.Rows.Add(new PlanRowViewModel
            {
                RowType = PlanRowType.ProfitFormulaTile,
                Label = profit >= 0 ? "Profit if Sold" : "Loss if Sold",
                CoinValue = Math.Abs(profit),
                TooltipText = ProfitTooltip + profitQualifier,
                FormulaResultIsExact = profit >= 0
            });
        }

        /// <summary>
        /// W4A currency table rows, replacing the pre-W4A plain-text
        /// CurrencyCost rows: Label is now just the resolved currency name
        /// (the Required amount moved to its own Quantity-driven column,
        /// rendered by SummarySectionRenderer's c-table), rows are sorted
        /// alphabetically by that name (user-mandated), and
        /// CurrencyOwnedQuantity is now the RAW unclamped wallet holding -
        /// see that field's own updated doc comment. CurrencyNeededQuantity/
        /// CurrencyFullyCovered are derived from it here so the c-table
        /// renderer stays a dumb read of already-computed fields.
        /// </summary>
        private static void BuildCurrencyTableRows(PlanSectionViewModel section, CraftingPlanResult result)
        {
            if (result.Plan.CurrencyCosts == null || result.Plan.CurrencyCosts.Count == 0)
            {
                return;
            }

            var currencyRows = new List<PlanRowViewModel>(result.Plan.CurrencyCosts.Count);
            foreach (var cc in result.Plan.CurrencyCosts)
            {
                string currencyName = CurrencyDisplayResolver.ResolveName(cc.CurrencyId, result.CurrencyMetadata);
                string iconUrl = CurrencyDisplayResolver.ResolveIconUrl(cc.CurrencyId, result.CurrencyMetadata);
                int required = (int)cc.Amount;

                // W4A (user-mandated): UNCLAMPED - the real wallet holding,
                // even when it exceeds what the plan needs. Null (not 0)
                // when no wallet snapshot was available at all.
                int? owned = null;
                if (result.OwnedCurrencyAmounts != null &&
                    result.OwnedCurrencyAmounts.TryGetValue(cc.CurrencyId, out int ownedRaw))
                {
                    owned = ownedRaw;
                }
                int? needed = owned.HasValue ? Math.Max(0, required - owned.Value) : (int?)null;
                bool fullyCovered = owned.HasValue && owned.Value >= required;

                currencyRows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.CurrencyCost,
                    Label = currencyName,
                    Quantity = required,
                    IconUrl = iconUrl,
                    CurrencyOwnedQuantity = owned,
                    CurrencyNeededQuantity = needed,
                    CurrencyFullyCovered = fullyCovered
                });
            }

            // OrderBy (stable), not List.Sort (unstable) - two different
            // unknown currency ids both fall back to the same generic
            // "Currency" display name (CurrencyDisplayResolver), and an
            // unstable sort could reorder that tied pair nondeterministically
            // run to run.
            section.Rows.AddRange(currencyRows.OrderBy(r => r.Label, StringComparer.Ordinal));
        }

        /// <summary>
        /// currency-ux-package (Feature 2): converts the plan's currency
        /// cost list into a currency-id-keyed dictionary for
        /// PlanViewModel.CurrencyPlanTotals - the Recipe Tree's per-leaf
        /// pill needs O(1) lookup by currency id, unlike
        /// BuildCurrencyTableRows above, which only ever iterates the list
        /// once in Summary-table order.
        /// </summary>
        private static IReadOnlyDictionary<int, long> BuildCurrencyPlanTotals(List<CurrencyCost> currencyCosts)
        {
            if (currencyCosts == null || currencyCosts.Count == 0)
            {
                return null;
            }

            var totals = new Dictionary<int, long>(currencyCosts.Count);
            foreach (var cc in currencyCosts)
            {
                totals[cc.CurrencyId] = cc.Amount;
            }
            return totals;
        }

        /// <summary>
        /// currency-ux-package (Feature 3): re-indexes the plan's
        /// informational timegated-cap notices by ItemId for
        /// PlanViewModel.VendorCapsByItemId - pure passthrough/reindex of
        /// an already-computed list (VendorBatchSolver.FinalizeVendorBatches
        /// owns the actual cap computation, untouched here), one entry per
        /// item id by construction (TimegatedItem is already a per-item
        /// merged notice).
        /// </summary>
        private static IReadOnlyDictionary<int, TimegatedItem> BuildVendorCapsByItemId(
            List<TimegatedItem> timegatedItems)
        {
            if (timegatedItems == null || timegatedItems.Count == 0)
            {
                return null;
            }

            var byItemId = new Dictionary<int, TimegatedItem>(timegatedItems.Count);
            foreach (var item in timegatedItems)
            {
                byItemId[item.ItemId] = item;
            }
            return byItemId;
        }

        private PlanSectionViewModel BuildUsedMaterialsSection(CraftingPlanResult result)
        {
            var section = new PlanSectionViewModel
            {
                SectionType = PlanSectionType.UsedMaterials,
                Title = $"Used Materials ({result.UsedMaterials.Count})",
                IsDefaultExpanded = true
            };

            foreach (var um in result.UsedMaterials)
            {
                string name = ResolveName(um.ItemId, result.ItemMetadata);
                string iconUrl = ResolveIconUrl(um.ItemId, result.ItemMetadata);
                string rarity = ResolveRarity(um.ItemId, result.ItemMetadata);

                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.UsedMaterial,
                    Label = name,
                    IconUrl = iconUrl,
                    Rarity = rarity,
                    Quantity = um.QuantityUsed
                });
            }

            return section;
        }

        private PlanSectionViewModel BuildShoppingListSection(
            List<PlanStep> steps, CraftingPlanResult result)
        {
            var section = new PlanSectionViewModel
            {
                SectionType = PlanSectionType.ShoppingList,
                Title = $"Shopping List ({steps.Count})",
                IsDefaultExpanded = true
            };

            foreach (var step in steps)
            {
                string name = ResolveName(step.ItemId, result.ItemMetadata);
                string iconUrl = ResolveIconUrl(step.ItemId, result.ItemMetadata);
                string rarity = ResolveRarity(step.ItemId, result.ItemMetadata);
                PlanRowType rowType = MapShoppingRowType(step.Source);

                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = rowType,
                    Label = name,
                    IconUrl = iconUrl,
                    Rarity = rarity,
                    Quantity = step.Quantity,
                    CoinValue = step.TotalCost,
                    UnitCoinValue = step.UnitCost,
                    HintText = ResolveHintText(rowType, step.ItemId, result.AcquisitionHints),
                    BadgeText = ResolveBadgeText(rowType, step.ItemId, result.AcquisitionHints),
                    // M34-B2b: owned/needed split, cosmetic only (mirrors
                    // BuildSummarySection's CurrencyCost rows) - only the
                    // Total column, never Each (a per-unit rate has no
                    // ownership concept - see ResolveAmounts' doc comment).
                    CurrencyCosts = CurrencyDisplayResolver.ResolveAmounts(
                        step.VendorCurrencyCosts, result.CurrencyMetadata, result.OwnedCurrencyAmounts),
                    UnitCurrencyCosts = CurrencyDisplayResolver.ResolveUnitAmounts(
                        step.VendorOfferOutputCount, step.VendorOfferCurrencyCostLinesPerBatch, result.CurrencyMetadata)
                });
            }

            return section;
        }

        /// <summary>
        /// Acquisition-hint tooltip text for shopping rows. Only ever
        /// populated for ShoppingUnknown rows - a hint entry existing for
        /// an item that actually has a priced/vendor source must not bleed
        /// onto that row's tooltip.
        /// </summary>
        private static string ResolveHintText(
            PlanRowType rowType,
            int itemId,
            IReadOnlyDictionary<int, AcquisitionHint> acquisitionHints)
        {
            if (rowType != PlanRowType.ShoppingUnknown || acquisitionHints == null)
            {
                return null;
            }
            if (acquisitionHints.TryGetValue(itemId, out var hint) &&
                hint != null && !string.IsNullOrEmpty(hint.Hint))
            {
                return hint.Hint;
            }
            return null;
        }

        /// <summary>
        /// Badge/pill-tag text for shopping rows, same "ShoppingUnknown
        /// only" guard as ResolveHintText - a badge existing for an item
        /// that actually has a priced/vendor source must not bleed onto
        /// that row's tag.
        /// </summary>
        private static string ResolveBadgeText(
            PlanRowType rowType,
            int itemId,
            IReadOnlyDictionary<int, AcquisitionHint> acquisitionHints)
        {
            if (rowType != PlanRowType.ShoppingUnknown || acquisitionHints == null)
            {
                return null;
            }
            if (acquisitionHints.TryGetValue(itemId, out var hint) &&
                hint != null && !string.IsNullOrEmpty(hint.Badge))
            {
                return hint.Badge;
            }
            return null;
        }

        private PlanSectionViewModel BuildCraftingStepsSection(
            List<PlanStep> steps, CraftingPlanResult result)
        {
            var section = new PlanSectionViewModel
            {
                SectionType = PlanSectionType.CraftingSteps,
                Title = $"Crafting Steps ({steps.Count})",
                IsDefaultExpanded = true
            };

            var planDiscNames = BuildPlanDiscNames(result);

            foreach (var step in steps)
            {
                string name = ResolveName(step.ItemId, result.ItemMetadata);
                string iconUrl = ResolveIconUrl(step.ItemId, result.ItemMetadata);
                string rarity = ResolveRarity(step.ItemId, result.ItemMetadata);

                // Find discipline info from RequiredRecipes
                string sublabel = "";
                if (result.RequiredRecipes != null)
                {
                    var recipe = result.RequiredRecipes
                        .FirstOrDefault(r => r.RecipeId == step.RecipeId);
                    if (recipe != null)
                    {
                        sublabel = FormatDisciplineSublabel(
                            recipe.Disciplines, recipe.MinRating, planDiscNames);
                    }
                }

                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.CraftStep,
                    Label = name,
                    Sublabel = sublabel,
                    IconUrl = iconUrl,
                    Rarity = rarity,
                    Quantity = step.Quantity
                });
            }

            // Timegated (vendor purchase cap) notices (M34-B1 #3) - a plain
            // informational line per item, gw2efficiency parity: caps are
            // surfaced, never solved around. Appended after the real craft
            // steps so a section made up ENTIRELY of notices (no craft
            // steps at all) still renders correctly.
            if (result.Plan.TimegatedItems != null)
            {
                foreach (var timegated in result.Plan.TimegatedItems)
                {
                    string itemName = ResolveName(timegated.ItemId, result.ItemMetadata);

                    // Astral Acclaim package (KNOWN-ISSUES #33): Seasonal
                    // uses the noun "Season" (matching gw2e's own Wizard's
                    // Vault wording), not the adjective "Seasonal" - keeps
                    // the same "{CapLabel} limit: N" shape as Daily/Weekly.
                    string capLabel;
                    if (timegated.CapType == TimegatedCapType.Daily)
                    {
                        capLabel = "Daily";
                    }
                    else if (timegated.CapType == TimegatedCapType.Weekly)
                    {
                        capLabel = "Weekly";
                    }
                    else
                    {
                        capLabel = "Season";
                    }

                    section.Rows.Add(new PlanRowViewModel
                    {
                        RowType = PlanRowType.TimegatedNotice,
                        Label = $"{itemName} is timegated - {capLabel} limit: {timegated.CapValue} (plan needs {timegated.NeededCount})"
                    });
                }
            }

            // Daily craft-cooldown notices (audit row 56, gw2e parity in
            // spirit with the vendor-cap notices above): additive,
            // informational-only pass over the real craft steps just built,
            // keyed on the wiki-verified seed (DailyCooldownItemService /
            // ref/daily_cooldown_items.json). Never touches the solver or
            // Plan.TimegatedItems - this reuses only the TimegatedNotice ROW
            // SHAPE (plain Label text, see CraftStepsSectionRenderer.Render's
            // generic TextRowRenderer branch), not the vendor-cap model
            // type, so a recipe-level cooldown can never be confused with a
            // vendor purchase cap in PlanStructuralValidator or anywhere
            // else that reads Plan.TimegatedItems directly.
            AppendDailyCooldownNotices(section, steps, result);

            return section;
        }

        /// <summary>
        /// Appends one TimegatedNotice row per Craft-source step whose
        /// aggregate Quantity (already merged across the whole tree, same
        /// aggregate PlanStep.Quantity every other row in this section
        /// reads) exceeds the seed's PerDayCap for that item - "crafting N
        /// takes about N/cap days (cap per day per account)" wording,
        /// mirroring the vendor-cap notice's own plain-informational tone.
        /// A step at or under the cap gets no notice (a single day's worth
        /// needs no warning). result.DailyCooldownItems is null whenever
        /// the module was not wired with the seed (Module.cs's own
        /// try/catch degrades to null on a missing/bad file) - this method
        /// is then a no-op, exactly like every other optional-seed lookup
        /// in this class.
        /// </summary>
        private static void AppendDailyCooldownNotices(
            PlanSectionViewModel section, List<PlanStep> craftSteps, CraftingPlanResult result)
        {
            if (result.DailyCooldownItems == null || result.DailyCooldownItems.Count == 0)
            {
                return;
            }

            foreach (var step in craftSteps)
            {
                if (!result.DailyCooldownItems.TryGetValue(step.ItemId, out var cooldown) ||
                    cooldown == null || cooldown.PerDayCap <= 0 || step.Quantity <= cooldown.PerDayCap)
                {
                    continue;
                }

                string itemName = ResolveName(step.ItemId, result.ItemMetadata);
                int days = (int)Math.Ceiling((double)step.Quantity / cooldown.PerDayCap);

                // Review fix (audit row 56 PART C nice-to-have): the
                // singular "day" branch was dead code - this loop already
                // `continue`s above whenever step.Quantity <= cooldown.
                // PerDayCap, so every notice reaching this point has
                // Quantity > PerDayCap, making days = Ceiling(qty / cap)
                // always >= 2. Always plural.
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.TimegatedNotice,
                    // Review nice-to-have (post-PART-C follow-up): each
                    // notice row is individually accurate but says nothing
                    // about how multiple rows combine - the real floor
                    // across several gated items in one plan is max(days),
                    // not the sum, since the per-account daily caps run
                    // independently of each other (e.g. the flagship
                    // Gift of Aurene case, which needs several gated
                    // Dragon Hatchling Doll components at once).
                    Label = $"{itemName} is timegated - {cooldown.PerDayCap} per day per account - " +
                        $"crafting {step.Quantity} will take about {days} days " +
                        "(runs in parallel with other daily-gated items)"
                });
            }
        }

        private PlanSectionViewModel BuildDisciplinesSection(CraftingPlanResult result)
        {
            var section = new PlanSectionViewModel
            {
                SectionType = PlanSectionType.RequiredDisciplines,
                Title = $"Required Disciplines ({result.RequiredDisciplines.Count})",
                IsDefaultExpanded = true
            };

            foreach (var disc in result.RequiredDisciplines)
            {
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.DisciplineRow,
                    Label = disc.Discipline,
                    Sublabel = $"Level {disc.MinRating}",
                    CharacterAvailabilityText = BuildCharacterAvailabilityText(disc, result.CharacterDisciplines)
                });
            }

            return section;
        }

        /// <summary>
        /// W3C (per-character discipline display, gw2efficiency parity):
        /// which characters have `disc`, and at what rating - see
        /// PlanRowViewModel.CharacterAvailabilityText's own doc comment for
        /// the exact output shapes. characterDisciplines is
        /// CraftingPlanResult.CharacterDisciplines, a straight passthrough
        /// of the account snapshot - null means the snapshot never
        /// captured this data at all (old snapshot / degraded fetch), which
        /// must never be conflated with "captured, and nobody has it".
        /// </summary>
        private static string BuildCharacterAvailabilityText(
            RequiredDiscipline disc, IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines)
        {
            var matches = MatchingCharacterDisciplines(disc.Discipline, characterDisciplines);
            if (matches == null)
            {
                return null;
            }

            if (matches.Count == 0)
            {
                return "Not trained on any character";
            }

            var parts = matches.Select(cd => cd.Rating < disc.MinRating
                ? $"{cd.CharacterName} ({cd.Rating}/{disc.MinRating})"
                : $"{cd.CharacterName} ({cd.Rating})");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// design-plan-notes.md (Notes section, competency notes): shared
        /// filter/sort BuildCharacterAvailabilityText and BestCharacterRating
        /// both build on, extracted so the two call sites can't drift on
        /// which characters count as "having" a discipline or how ties
        /// break. Same null contract as BuildCharacterAvailabilityText's own
        /// doc comment: null (not an empty list) when characterDisciplines
        /// itself is null (no snapshot captured this data at all) - a
        /// caller must not conflate that with "captured, and nobody has
        /// it" (empty list). Highest rating first, then character name
        /// alphabetical for ties - matches this method's pre-extraction
        /// ordering byte-for-byte.
        /// </summary>
        private static List<SnapshotCharacterDiscipline> MatchingCharacterDisciplines(
            string discipline, IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines)
        {
            if (characterDisciplines == null)
            {
                return null;
            }

            return characterDisciplines
                .Where(cd => cd != null && string.Equals(cd.Discipline, discipline, StringComparison.Ordinal))
                .OrderByDescending(cd => cd.Rating)
                .ThenBy(cd => cd.CharacterName, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// design-plan-notes.md (Notes section, competency notes): the
        /// account's best rating for `discipline`, plus which character
        /// achieved it (ties broken alphabetically, same as
        /// MatchingCharacterDisciplines) - used by BuildNotesSection to
        /// decide whether a RequiredDiscipline is "blocked" (best == null,
        /// or best.Rating &lt; the discipline's MinRating) and to word the
        /// note. Null under the identical two conditions
        /// BuildCharacterAvailabilityText already distinguishes: no
        /// snapshot at all (characterDisciplines == null) and a snapshot
        /// with zero characters on this discipline (matches.Count == 0) -
        /// a caller cannot tell those apart from this return value alone,
        /// by design; BuildNotesSection reads characterDisciplines == null
        /// directly wherever that distinction matters (never renders a
        /// competency line at all without a snapshot).
        /// </summary>
        private static (int Rating, string CharacterName)? BestCharacterRating(
            string discipline, IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines)
        {
            var matches = MatchingCharacterDisciplines(discipline, characterDisciplines);
            if (matches == null || matches.Count == 0)
            {
                return null;
            }

            return (matches[0].Rating, matches[0].CharacterName);
        }

        /// <summary>
        /// design-plan-notes.md (Notes section, Option 1 - single flat
        /// section, one shared NoteLine row shape). Assembles rows in a
        /// fixed order - excess/reclaim lines, then a total (only when 2+
        /// excess lines exist), then competency lines, then (opportunity-
        /// notes) RECIPE-SHEET SAVINGS opportunities, then SEASONAL VENDOR
        /// TIP opportunities, then the gambling-forge scope line (0 or 1) -
        /// so re-solves and screenshots stay diffable. Returns a section
        /// with zero rows when every note kind is empty; the caller
        /// (Build()) only appends it to vm.Sections when Rows.Count > 0, so
        /// an empty Notes section never renders a header at all.
        /// </summary>
        private PlanSectionViewModel BuildNotesSection(CraftingPlanResult result)
        {
            var section = new PlanSectionViewModel
            {
                SectionType = PlanSectionType.Notes,
                IsDefaultExpanded = true
            };

            // Review fix (nice-to-have): every other section's "(N)" counts
            // real entries, not a rollup row - tracked separately from
            // section.Rows.Count so the "Total reclaimable value" row (and,
            // below, every physical row the forge-scope note now spans)
            // never inflates the count.
            int noteEntryCount = 0;

            // 1. Excess/reclaim lines, alphabetical by resolved item name -
            // same StringComparer.Ordinal stable-sort precedent
            // BuildCurrencyTableRows already uses for its own rows. Review
            // fix (finding 3, MEASURED): sort on the resolved NAME itself,
            // not the composed Label - every label starts with the shared
            // "Excess: <qty>x " prefix, so sorting the whole Label put
            // quantity digits ahead of the name ("12x Zircon Ore" sorted
            // before "3x Apple"), contradicting this very comment.
            if (result.ExcessCraftOutputs != null && result.ExcessCraftOutputs.Count > 0)
            {
                var excessRows = new List<(string Name, PlanRowViewModel Row)>(result.ExcessCraftOutputs.Count);
                long totalReclaim = 0;
                // Review fix (finding 5, MEASURED): an unpriced row
                // (ReclaimValue == null because no live SellInstant, not
                // because the item is account-bound) rendered identically
                // to a genuinely worthless one and silently understated
                // "Total reclaimable value" - flag it on the row and on the
                // total whenever any contributor was unpriced.
                bool anyUnpriced = false;
                foreach (var excess in result.ExcessCraftOutputs)
                {
                    string name = ResolveName(excess.ItemId, result.ItemMetadata);
                    long coinValue = excess.ReclaimValue ?? 0;
                    totalReclaim += coinValue;

                    bool unpriced = !excess.IsAccountBound && !excess.ReclaimValue.HasValue;
                    if (unpriced)
                    {
                        anyUnpriced = true;
                    }

                    string suffix = excess.IsAccountBound
                        ? " (account-bound, not sellable)"
                        : unpriced
                            ? " (no sell price)"
                            : string.Empty;

                    excessRows.Add((name, new PlanRowViewModel
                    {
                        RowType = PlanRowType.NoteLine,
                        Label = $"Excess: {excess.ExcessQuantity}x {name}{suffix}",
                        CoinValue = coinValue
                    }));
                    noteEntryCount++;
                }

                section.Rows.AddRange(excessRows
                    .OrderBy(r => r.Name, StringComparer.Ordinal)
                    .Select(r => r.Row));

                // A single excess line is already its own total - matches
                // SummarySectionRenderer's own "don't show a redundant
                // single-item rollup" instinct.
                if (result.ExcessCraftOutputs.Count > 1)
                {
                    section.Rows.Add(new PlanRowViewModel
                    {
                        RowType = PlanRowType.NoteLine,
                        Label = anyUnpriced
                            ? "Total reclaimable value (excludes unpriced items)"
                            : "Total reclaimable value",
                        CoinValue = totalReclaim
                    });
                }
            }

            // 2. Competency lines, alphabetical by discipline - matches
            // RequiredDisciplines' own display order (disciplineMap.OrderBy
            // in PlanResultBuilder.Build). A discipline is "blocked" only
            // when a real snapshot exists AND the account's best rating for
            // it is missing or below MinRating - CharacterDisciplines ==
            // null (no snapshot) must never produce a false "blocked"
            // claim, mirroring BuildCharacterAvailabilityText's own null
            // contract.
            if (result.CharacterDisciplines != null && result.RequiredDisciplines != null)
            {
                foreach (var disc in result.RequiredDisciplines)
                {
                    var best = BestCharacterRating(disc.Discipline, result.CharacterDisciplines);
                    bool blocked = best == null || best.Value.Rating < disc.MinRating;
                    if (!blocked)
                    {
                        continue;
                    }

                    string label = best == null
                        ? $"{disc.Discipline} {disc.MinRating} required - not trained on any character"
                        : $"{disc.Discipline} {disc.MinRating} required - highest on this account: {best.Value.Rating} ({best.Value.CharacterName})";

                    section.Rows.Add(new PlanRowViewModel
                    {
                        RowType = PlanRowType.NoteLine,
                        Label = label
                    });
                    noteEntryCount++;
                }
            }

            // 3. RECIPE-SHEET SAVINGS opportunities (opportunity-notes),
            // alphabetical by resolved item name - same stable-sort
            // precedent as the excess/reclaim rows above. Two physical rows
            // per opportunity (the sheet's own cost, then the per-unit
            // savings once learned) - NoteLine only has ONE CoinValue slot
            // per row (NotesSectionRenderer), and this note carries two
            // distinct concrete numbers, so it is split the same way the
            // forge-scope note below is split for a different reason (line-
            // wrap avoidance) - one logical note, two/three physical rows.
            if (result.RecipeSheetSavingsOpportunities != null && result.RecipeSheetSavingsOpportunities.Count > 0)
            {
                var sheetRows = new List<(string Name, List<PlanRowViewModel> Rows)>(
                    result.RecipeSheetSavingsOpportunities.Count);
                foreach (var opp in result.RecipeSheetSavingsOpportunities)
                {
                    string itemName = ResolveName(opp.ItemId, result.ItemMetadata);
                    var rows = new List<PlanRowViewModel>(2);

                    string leadIn = opp.DisciplineBlocked && !string.IsNullOrEmpty(opp.Discipline)
                        ? $"Train {opp.Discipline} to {opp.RequiredRating} and buy the {itemName} recipe to craft it instead - recipe costs"
                        : $"Buy the {itemName} recipe to craft it instead of buying it - recipe costs";

                    rows.Add(new PlanRowViewModel
                    {
                        RowType = PlanRowType.NoteLine,
                        Label = leadIn,
                        CoinValue = opp.SheetCost
                    });
                    rows.Add(new PlanRowViewModel
                    {
                        RowType = PlanRowType.NoteLine,
                        Label = "  Saves per unit crafted",
                        CoinValue = opp.SavingsPerUnit
                    });

                    sheetRows.Add((itemName, rows));
                    noteEntryCount++;
                }

                foreach (var entry in sheetRows.OrderBy(r => r.Name, StringComparer.Ordinal))
                {
                    section.Rows.AddRange(entry.Rows);
                }
            }

            // 4. SEASONAL VENDOR TIP opportunities (opportunity-notes),
            // alphabetical by resolved item name. Two physical rows per
            // tip (review fix, finding 4) - the trade description, then
            // the "cheaper than this plan's price" comparison - same
            // NotesSectionRenderer.LabelHelpers.EllipsizeToWidth exposure
            // the RECIPE-SHEET SAVINGS note above was already split to
            // avoid: a single ~150-char combined label ellipsizes at the
            // panel edge, and the trailing clause (what the CoinValue on
            // the SAME row actually means) is exactly what gets cut,
            // leaving a bare coin number with no stated meaning. Splitting
            // also resolves the "5x Glob of Ectoplasm" / "PlanUnitPrice"
            // adjacency ambiguity (PlanUnitPrice is a PER-UNIT price, not
            // the price of the 5x bundle just before it) by saying "per
            // unit" explicitly on the row that actually carries the
            // CoinValue. The tip's own "cost" description is built ONLY
            // from Item-type cost lines (see BuildSeasonalCostDescription's
            // own doc comment for why a coin-priced cost line is skipped
            // entirely rather than rendered as raw text) - the only offers
            // this module seeds today (Candy Corn Vendor (Weekly)) are
            // pure single-Item-cost-line, so this never fires in practice
            // yet.
            if (result.SeasonalVendorTips != null && result.SeasonalVendorTips.Count > 0)
            {
                var tipRows = new List<(string Name, List<PlanRowViewModel> Rows)>(result.SeasonalVendorTips.Count);
                foreach (var tip in result.SeasonalVendorTips)
                {
                    string costDescription = BuildSeasonalCostDescription(tip.CostLines, result.ItemMetadata);
                    if (costDescription == null)
                    {
                        continue;
                    }

                    string itemName = ResolveName(tip.ItemId, result.ItemMetadata);
                    // Review fix (finding 2): the cap number here is a
                    // PURCHASE/trade limit, not an output-unit limit - see
                    // ref/vendor_offers.json's own outputCount/weeklyCap
                    // pairs (e.g. Dragon Bash Merchant, outputCount 50 /
                    // weeklyCap 1) and VendorBatchSolver.FinalizeVendorBatches,
                    // which compares this same cap against unitsNeeded
                    // (a purchase count), never against an output-unit
                    // count. The old "(capped N/week)" wording, placed
                    // right after "Nx <item>", read as "N <item>s/week"
                    // - off by a factor of OutputCount whenever OutputCount
                    // != 1. Wording it as a purchase count instead removes
                    // the ambiguity without needing the multiply-through.
                    string capClause = tip.WeeklyCap.HasValue
                        ? $" (limit {tip.WeeklyCap.Value} purchase{(tip.WeeklyCap.Value == 1 ? "" : "s")}/week)"
                        : tip.DailyCap.HasValue
                            ? $" (limit {tip.DailyCap.Value} purchase{(tip.DailyCap.Value == 1 ? "" : "s")}/day)"
                            : "";

                    string festivalDisplayName = Gw2Constants.ResolveFestivalDisplayName(tip.Festival);
                    string tradeLabel = $"During {festivalDisplayName}: {tip.MerchantName} trades {costDescription} for " +
                        $"{tip.OutputCount}x {itemName}{capClause}";

                    var rows = new List<PlanRowViewModel>(2)
                    {
                        new PlanRowViewModel
                        {
                            RowType = PlanRowType.NoteLine,
                            Label = tradeLabel
                        },
                        new PlanRowViewModel
                        {
                            RowType = PlanRowType.NoteLine,
                            Label = "  Cheaper than this plan's price per unit",
                            CoinValue = tip.PlanUnitPrice
                        }
                    };

                    tipRows.Add((itemName, rows));
                    noteEntryCount++;
                }

                foreach (var entry in tipRows.OrderBy(r => r.Name, StringComparer.Ordinal))
                {
                    section.Rows.AddRange(entry.Rows);
                }
            }

            // 5. Gambling-forge scope note (0 or 1 logical entry). Wording
            // deliberately distinguishes the two mechanics design-plan-
            // notes.md section 9 flags as easy to conflate: this plan's own
            // Mystic-Clover-style fractional yield IS probability-adjusted
            // (EV already priced in) - true multi-outcome gambles (e.g.
            // precursor forging) are a DIFFERENT mechanic this module has
            // no data for at all and are never represented in a plan,
            // in either direction.
            //
            // Review fix (finding 4, INFERRED - no live desktop
            // verification was performed, see docs/KNOWN-ISSUES.md): the
            // single-row, ~243-char version of this note would have
            // clipped horizontally at NotesSectionRenderer's panel edge
            // (panelWidth ~884px at DefaultFont14, AutoSizeWidth label with
            // no max-width cap) - a label cannot overflow a fixed-height
            // row's HEIGHT, only its own horizontal extent, so the failure
            // mode here is edge-clipping, not row overflow. The clipped
            // portion would have been exactly the "true multi-outcome
            // gambles... never models and never shows" caveat the note
            // exists to deliver. Split at the existing sentence break plus
            // one clause break, into 3 NoteLine rows, each now a complete
            // sentence - this preserves the 28px-per-row contract exactly
            // (section height is rows.Count * FallbackTextRowHeight) while
            // keeping every word of the original text visible regardless
            // of panel width.
            if (result.ProbabilisticForgeOutputItemIds != null &&
                result.ProbabilisticForgeOutputItemIds.Count > 0)
            {
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.NoteLine,
                    Label = "This plan includes a Mystic Clover-style Mystic Forge yield - its expected " +
                        "output is already probability-adjusted."
                });
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.NoteLine,
                    Label = "True multi-outcome Mystic Forge gambles (e.g. precursor forging) are a " +
                        "different mechanic."
                });
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.NoteLine,
                    Label = "This plan never models or shows them."
                });
                noteEntryCount++;
            }

            // Matches every other section's "Title (N)" convention (Used
            // Materials/Shopping List/Crafting Steps/Required Disciplines/
            // Required Recipes all count their own final row list this same
            // way) - computed last so it reflects every note kind above.
            // Review fix (nice-to-have): counts real note ENTRIES
            // (noteEntryCount), not section.Rows.Count - the latter also
            // includes the "Total reclaimable value" rollup row and, as of
            // the finding-4 split above, 3 physical rows for what is still
            // one logical forge-scope note, either of which would inflate
            // "Notes (N)" past the number of things actually being said.
            section.Title = $"Notes ({noteEntryCount})";

            return section;
        }

        private PlanSectionViewModel BuildRecipesSection(CraftingPlanResult result)
        {
            var section = new PlanSectionViewModel
            {
                SectionType = PlanSectionType.RequiredRecipes,
                IsDefaultExpanded = true
            };

            var planDiscNames = BuildPlanDiscNames(result);

            foreach (var recipe in result.RequiredRecipes)
            {
                // Wave-3 quick win #2 (2026-08-06 field testing, maintainer
                // direction): a sole-Mystic-Forge recipe has nothing to
                // learn - the forge combination just exists, there is no
                // unlock concept (PlanResultBuilder.InherentlyAvailableDisciplines
                // already always marks it IsMissing = false for the same
                // reason). Listing it here read as a recipe-unlock task that
                // does not exist, so it is skipped entirely rather than
                // shown as an always-"Learned"/"Auto-learned" row. A recipe
                // that combines MysticForge with a genuine leveled
                // discipline (not seen in real game data today - see
                // FormatDisciplineSublabel's own doc comment - but not
                // structurally impossible) still has a real discipline to
                // learn, so only a recipe whose ENTIRE Disciplines list is
                // MysticForge is filtered here.
                //
                // This only touches the Required Recipes SECTION's own row
                // list, built fresh in this loop - the raw
                // result.RequiredRecipes list itself, and
                // BuildCraftingStepsSection's per-step sublabel lookup that
                // reads it above, are both untouched. A Mystic Forge craft
                // STEP therefore still shows its "Mystic Forge" location
                // sublabel exactly as PR #102 left it - only this section
                // drops the row.
                if (IsMysticForgeOnly(recipe.Disciplines))
                {
                    continue;
                }

                string name = ResolveName(recipe.OutputItemId, result.ItemMetadata);
                string iconUrl = ResolveIconUrl(recipe.OutputItemId, result.ItemMetadata);
                string rarity = ResolveRarity(recipe.OutputItemId, result.ItemMetadata);

                string statusTag;
                if (recipe.IsAutoLearned)
                {
                    statusTag = "Auto-learned";
                }
                else if (recipe.IsMissing == true)
                {
                    statusTag = "Missing!";
                }
                else if (recipe.IsMissing == false)
                {
                    statusTag = "Learned";
                }
                else
                {
                    statusTag = "";
                }

                string sublabel = FormatDisciplineSublabel(
                    recipe.Disciplines, recipe.MinRating, planDiscNames);

                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.RecipeRow,
                    Label = name,
                    Sublabel = sublabel,
                    IconUrl = iconUrl,
                    Rarity = rarity,
                    StatusTag = statusTag
                });
            }

            // Title reflects the count AFTER the Mystic-Forge filter above,
            // not result.RequiredRecipes.Count - keeps the header honest
            // about what is actually listed below it. CraftingPlanView
            // (wave-3 quick win #3's "Hide Unlocked Recipes" checkbox)
            // recomputes its OWN header title at render time from this same
            // section.Rows.Count plus its live filter state
            // (RequiredRecipesVisibility.BuildHeaderTitle) rather than
            // reading this Title verbatim - this value is still the correct
            // "filter off" baseline for any other consumer (e.g. tests) and
            // matches every other section's Title convention.
            section.Title = $"Required Recipes ({section.Rows.Count})";
            return section;
        }

        // Wave-3 quick win #2: true only when EVERY entry in the recipe's
        // Disciplines list is "MysticForge" (real production Mystic Forge
        // recipes always carry exactly Disciplines = ["MysticForge"] -
        // MysticForgeRecipeData.Load sets this unconditionally, mirrored by
        // FormatDisciplineSublabel's own hasMysticForge comment above).
        // Empty/null Disciplines is NOT Mystic-Forge-only (vacuous truth
        // over an empty list would otherwise wrongly match a recipe with no
        // discipline data at all).
        private static bool IsMysticForgeOnly(List<string> disciplines)
        {
            if (disciplines == null || disciplines.Count == 0)
            {
                return false;
            }
            foreach (var discipline in disciplines)
            {
                if (discipline != "MysticForge")
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// opportunity-notes (SEASONAL VENDOR TIP): renders an offer's cost
        /// lines as a plain "{Count}x {Name}[ + {Count}x {Name}...]" phrase
        /// for the note's inline text. Returns null (never a partial/
        /// misleading description) when costLines is null/empty or contains
        /// ANY non-Item line - a coin (or other currency) cost line has no
        /// safe way to render inline as raw text without violating the
        /// repo's "coin icons MUST appear to the right of the number"
        /// invariant, and NoteLine only has ONE CoinValue slot per row
        /// (already spent on the plan's own price at the end of this same
        /// row - see BuildNotesSection). The three offers this module seeds
        /// today are pure single-Item-cost-line, so this restriction never
        /// bites in practice yet.
        /// </summary>
        private static string BuildSeasonalCostDescription(
            List<CostLine> costLines, IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            if (costLines == null || costLines.Count == 0)
            {
                return null;
            }

            var parts = new List<string>(costLines.Count);
            foreach (var line in costLines)
            {
                if (line == null || !string.Equals(line.Type, "Item", StringComparison.Ordinal))
                {
                    return null;
                }
                parts.Add($"{line.Count}x {ResolveName(line.Id, metadata)}");
            }

            return string.Join(" + ", parts);
        }

        private static PlanRowType MapShoppingRowType(AcquisitionSource source)
        {
            switch (source)
            {
                case AcquisitionSource.BuyFromTp: return PlanRowType.ShoppingBuy;
                case AcquisitionSource.BuyFromVendor: return PlanRowType.ShoppingVendor;
                case AcquisitionSource.Currency: return PlanRowType.ShoppingCurrency;
                default: return PlanRowType.ShoppingUnknown;
            }
        }

        private static string ResolveName(
            int itemId, IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            if (metadata != null &&
                metadata.TryGetValue(itemId, out var meta) &&
                !string.IsNullOrEmpty(meta.Name))
            {
                return meta.Name;
            }
            return "Unknown Item";
        }

        private static string ResolveIconUrl(
            int itemId, IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            if (metadata != null &&
                metadata.TryGetValue(itemId, out var meta))
            {
                return meta.IconUrl;
            }
            return null;
        }

        private static string ResolveRarity(
            int itemId, IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            if (metadata != null &&
                metadata.TryGetValue(itemId, out var meta))
            {
                return meta.Rarity;
            }
            return null;
        }

        private static HashSet<string> BuildPlanDiscNames(CraftingPlanResult result)
        {
            if (result.RequiredDisciplines == null || result.RequiredDisciplines.Count == 0)
            {
                return new HashSet<string>();
            }
            return new HashSet<string>(result.RequiredDisciplines.Select(d => d.Discipline));
        }

        internal static string FormatDisciplineSublabel(
            List<string> recipeDisciplines,
            int recipeMinRating,
            ISet<string> planDiscNames)
        {
            if (recipeDisciplines == null || recipeDisciplines.Count == 0)
            {
                return "";
            }

            // Field-test finding E: a sole-MysticForge recipe (real
            // production Mystic Forge recipes always carry
            // Disciplines = ["MysticForge"] - MysticForgeRecipeData.Load
            // sets this unconditionally) used to render "MysticForge 0" -
            // the internal id string verbatim, plus a meaningless rating of
            // 0 (the forge has no level requirement). The forge is a
            // facility, not a discipline (PlanResultBuilder.
            // NonCraftingDisciplines now excludes it from Required
            // Disciplines entirely - see that field's doc comment), so its
            // step/recipe sublabel shows the facility's real name with no
            // level number instead.
            //
            // Follow-up fix: "MysticForge" is stripped out of planDiscNames
            // upstream (it is never a member of RequiredDisciplines), so it
            // can never survive the planDiscNames intersection below on its
            // own merits. It used to be run through that intersection like
            // any other discipline anyway, which meant a recipe combining
            // MysticForge with a genuine leveled discipline (not seen in
            // real game data today, but not structurally impossible) had
            // MysticForge silently dropped whenever the real discipline was
            // present - only the real discipline survived the intersection,
            // so the facility name never made it back in. Splitting the
            // MysticForge flag out before filtering, and always
            // re-prepending it to the display text, means it can no longer
            // be silently dropped no matter what planDiscNames does or does
            // not contain - the OTHER discipline's rating remains
            // meaningful information, so the level number stays too.
            bool hasMysticForge = recipeDisciplines.Contains("MysticForge");
            List<string> otherDisciplines = hasMysticForge
                ? recipeDisciplines.Where(d => d != "MysticForge").ToList()
                : recipeDisciplines;

            List<string> relevant;
            if (otherDisciplines.Count == 0)
            {
                // Sole facility - nothing else to filter or fall back to.
                relevant = new List<string>();
            }
            else if (planDiscNames == null || planDiscNames.Count == 0)
            {
                // No filtering - show all of the recipe's real disciplines
                relevant = new List<string>(otherDisciplines);
            }
            else
            {
                relevant = otherDisciplines.Where(d => planDiscNames.Contains(d)).ToList();

                // Fallback: if no intersection, show all recipe disciplines
                if (relevant.Count == 0)
                {
                    relevant = new List<string>(otherDisciplines);
                }
            }

            relevant.Sort();

            if (hasMysticForge && relevant.Count == 0)
            {
                return "Mystic Forge";
            }

            var displayParts = new List<string>(relevant);
            if (hasMysticForge)
            {
                displayParts.Insert(0, "Mystic Forge");
            }

            string displayText = string.Join(" / ", displayParts);
            return $"{displayText} {recipeMinRating}";
        }
    }
}
