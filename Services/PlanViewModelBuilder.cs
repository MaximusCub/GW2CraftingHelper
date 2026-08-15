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
                CurrencyMetadata = result.CurrencyMetadata
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
            string actualCostTooltip = result.PriceBasis == PriceBasis.BuyOrder
                ? ActualCostTooltip + " (buy-order prices)"
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
        /// that case rather than silently showing a formula that would not
        /// visually balance.
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
                Label = "Total Materials Value",
                CoinValue = totalMaterialsValue,
                TooltipText = totalMaterialsValueTooltip
            });
            section.Rows.Add(new PlanRowViewModel
            {
                RowType = PlanRowType.ProfitFormulaTile,
                Label = profit >= 0 ? "Profit if Sold" : "Loss if Sold",
                CoinValue = Math.Abs(profit),
                TooltipText = ProfitTooltip + profitQualifier
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

            return section;
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
            if (characterDisciplines == null)
            {
                return null;
            }

            var matches = characterDisciplines
                .Where(cd => cd != null && string.Equals(cd.Discipline, disc.Discipline, StringComparison.Ordinal))
                .OrderByDescending(cd => cd.Rating)
                .ThenBy(cd => cd.CharacterName, StringComparer.Ordinal)
                .ToList();

            if (matches.Count == 0)
            {
                return "Not trained on any character";
            }

            var parts = matches.Select(cd => cd.Rating < disc.MinRating
                ? $"{cd.CharacterName} ({cd.Rating}/{disc.MinRating})"
                : $"{cd.CharacterName} ({cd.Rating})");

            return string.Join(", ", parts);
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
