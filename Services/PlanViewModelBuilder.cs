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

            // 5. Required Recipes section (only if non-empty)
            if (result.RequiredRecipes != null && result.RequiredRecipes.Count > 0)
            {
                vm.Sections.Add(BuildRecipesSection(result));
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

        private PlanSectionViewModel BuildSummarySection(CraftingPlanResult result, bool isMultiItem)
        {
            var section = new PlanSectionViewModel
            {
                SectionType = PlanSectionType.Summary,
                Title = "Total Cost",
                IsDefaultExpanded = true
            };

            // CoinTotal row
            string basisSuffix = result.PriceBasis == PriceBasis.BuyOrder
                ? " (buy-order prices)"
                : "";
            section.Rows.Add(new PlanRowViewModel
            {
                RowType = PlanRowType.CoinTotal,
                Label = "Total" + basisSuffix,
                CoinValue = result.Plan.TotalCoinCost
            });

            // Own-materials opportunity cost (Valued mode only, and only
            // when it is actually non-zero - a material with no
            // instant-sell price contributes nothing worth surfacing).
            if (result.MaterialOpportunityCost.HasValue && result.MaterialOpportunityCost.Value > 0)
            {
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.CoinTotal,
                    Label = "Own materials (sell value forgone)",
                    CoinValue = result.MaterialOpportunityCost.Value
                });
            }

            // Sell-side rows: only when the target(s) have a live sell
            // price. M37 (KNOWN-ISSUES #25): in multi-item mode,
            // NetSaleValue/CraftingProfit are the BATCH sum across every
            // requested item that has one (see
            // CraftingPlanPipeline.ApplyBatchSellSideEconomics) - labels
            // are worded as a batch total, and the single-item "Nx"
            // overproduction qualifier is dropped since there is no single
            // requested quantity to compare a batch sum against.
            if (result.NetSaleValue.HasValue)
            {
                string sellLabel;
                if (isMultiItem)
                {
                    sellLabel = "Sell value (batch total, after 15% TP fees)";
                }
                else
                {
                    sellLabel = result.SellableQuantity > result.Plan.TargetQuantity
                        ? $"Sell value ({result.SellableQuantity}x, after 15% TP fees)"
                        : "Sell value (after 15% TP fees)";
                }
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.CoinTotal,
                    Label = sellLabel,
                    CoinValue = result.NetSaleValue.Value
                });

                long profit = result.CraftingProfit ?? 0L;
                bool hasCurrencyCosts = result.Plan.CurrencyCosts != null &&
                                        result.Plan.CurrencyCosts.Count > 0;
                string qualifier;
                if (isMultiItem)
                {
                    qualifier = hasCurrencyCosts ? " (batch total, coin costs only)" : " (batch total)";
                }
                else
                {
                    qualifier = hasCurrencyCosts ? " (coin costs only)" : "";
                }
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.CoinTotal,
                    Label = (profit >= 0 ? "Profit if sold" : "Loss if sold") + qualifier,
                    CoinValue = Math.Abs(profit)
                });
            }

            // CurrencyCost rows
            if (result.Plan.CurrencyCosts != null)
            {
                foreach (var cc in result.Plan.CurrencyCosts)
                {
                    string currencyName = CurrencyDisplayResolver.ResolveName(cc.CurrencyId, result.CurrencyMetadata);
                    string iconUrl = CurrencyDisplayResolver.ResolveIconUrl(cc.CurrencyId, result.CurrencyMetadata);

                    // M34-B2a #4: owned/needed split, cosmetic only - null
                    // when no wallet snapshot was available (distinct from
                    // "0 owned").
                    int? ownedQuantity = null;
                    if (result.OwnedCurrencyAmounts != null &&
                        result.OwnedCurrencyAmounts.TryGetValue(cc.CurrencyId, out int owned))
                    {
                        ownedQuantity = Math.Min(owned, (int)cc.Amount);
                    }

                    section.Rows.Add(new PlanRowViewModel
                    {
                        RowType = PlanRowType.CurrencyCost,
                        Label = $"{cc.Amount}x {currencyName}",
                        Quantity = (int)cc.Amount,
                        IconUrl = iconUrl,
                        CurrencyOwnedQuantity = ownedQuantity
                    });
                }
            }

            // M35 (gw2efficiency parity - multi-item plans): echoes gw2e's
            // own Cost Breakdown banner concept for a multi-item batch (M34
            // r1 report). M37 (KNOWN-ISSUES #25) added the real batch-level
            // Sell value/Profit rows above (see
            // CraftingPlanPipeline.ApplyBatchSellSideEconomics) - gated on
            // the SAME result.NetSaleValue.HasValue condition as those rows
            // (mirroring gw2e's own shared ng-show condition, research
            // report Section 1.3b) so this note never references a profit
            // figure that is not actually on the page (e.g. every requested
            // root bought outright, or none tradable). The wording is NOT
            // gw2e's own verbatim banner text ("...sum of all crafted
            // recipes") because this module's rollup has no craft-vs-buy
            // filter at all (ApplyBatchSellSideEconomics' own doc comment,
            // divergence item 1) - a bought-but-tradable root can
            // contribute too, so "crafted recipes" would be inaccurate.
            if (isMultiItem && result.NetSaleValue.HasValue)
            {
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.MultiItemNote,
                    Label = "Sell value and profit are the sum across every requested item that has a live Trading Post sell price."
                });
            }

            return section;
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
                    string capLabel = timegated.CapType == TimegatedCapType.Daily ? "Daily" : "Weekly";
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
                    Sublabel = $"Level {disc.MinRating}"
                });
            }

            return section;
        }

        private PlanSectionViewModel BuildRecipesSection(CraftingPlanResult result)
        {
            var section = new PlanSectionViewModel
            {
                SectionType = PlanSectionType.RequiredRecipes,
                Title = $"Required Recipes ({result.RequiredRecipes.Count})",
                IsDefaultExpanded = true
            };

            var planDiscNames = BuildPlanDiscNames(result);

            foreach (var recipe in result.RequiredRecipes)
            {
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

            return section;
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

            List<string> relevant;
            if (planDiscNames == null || planDiscNames.Count == 0)
            {
                // No filtering - show all recipe disciplines
                relevant = new List<string>(recipeDisciplines);
            }
            else
            {
                relevant = recipeDisciplines.Where(d => planDiscNames.Contains(d)).ToList();

                // Fallback: if no intersection, show all recipe disciplines
                if (relevant.Count == 0)
                {
                    relevant = new List<string>(recipeDisciplines);
                }
            }

            relevant.Sort();
            return $"{string.Join(" / ", relevant)} {recipeMinRating}";
        }
    }
}
