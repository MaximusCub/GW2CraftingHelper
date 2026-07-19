using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class PlanViewModelBuilder
    {
        public PlanViewModel Build(CraftingPlanResult result)
        {
            var vm = new PlanViewModel
            {
                TargetQuantity = result.Plan.TargetQuantity,
                TreeRoot = result.CraftingTree
            };

            // Resolve target name/icon
            if (result.ItemMetadata != null &&
                result.ItemMetadata.TryGetValue(result.Plan.TargetItemId, out var targetMeta))
            {
                vm.TargetItemName = !string.IsNullOrEmpty(targetMeta.Name)
                    ? targetMeta.Name
                    : "Unknown Item";
                vm.TargetIconUrl = targetMeta.IconUrl;
            }
            else
            {
                vm.TargetItemName = "Unknown Item";
                vm.TargetIconUrl = null;
            }

            // 1. Summary section (always present)
            vm.Sections.Add(BuildSummarySection(result));

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

            // 4. Crafting Steps section (only if non-empty)
            if (craftSteps.Count > 0)
            {
                vm.Sections.Add(BuildCraftingStepsSection(craftSteps, result));
            }

            // 5. Required Disciplines section (only if non-empty)
            if (result.RequiredDisciplines != null && result.RequiredDisciplines.Count > 0)
            {
                vm.Sections.Add(BuildDisciplinesSection(result));
            }

            // 6. Required Recipes section (only if non-empty)
            if (result.RequiredRecipes != null && result.RequiredRecipes.Count > 0)
            {
                vm.Sections.Add(BuildRecipesSection(result));
            }

            return vm;
        }

        private PlanSectionViewModel BuildSummarySection(CraftingPlanResult result)
        {
            var section = new PlanSectionViewModel
            {
                SectionType = PlanSectionType.Summary,
                Title = "Summary",
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

            // Sell-side rows: only when the target has a live sell price.
            if (result.NetSaleValue.HasValue)
            {
                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.CoinTotal,
                    Label = "Sell value (after 15% TP fees)",
                    CoinValue = result.NetSaleValue.Value
                });

                long profit = result.CraftingProfit ?? 0L;
                bool hasCurrencyCosts = result.Plan.CurrencyCosts != null &&
                                        result.Plan.CurrencyCosts.Count > 0;
                string qualifier = hasCurrencyCosts ? " (coin costs only)" : "";
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
                    string currencyName = ResolveCurrencyName(cc.CurrencyId);
                    section.Rows.Add(new PlanRowViewModel
                    {
                        RowType = PlanRowType.CurrencyCost,
                        Label = $"{cc.Amount}x {currencyName}",
                        Quantity = (int)cc.Amount
                    });
                }
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

                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = PlanRowType.UsedMaterial,
                    Label = name,
                    IconUrl = iconUrl,
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
                PlanRowType rowType = MapShoppingRowType(step.Source);

                section.Rows.Add(new PlanRowViewModel
                {
                    RowType = rowType,
                    Label = name,
                    IconUrl = iconUrl,
                    Quantity = step.Quantity,
                    CoinValue = step.TotalCost
                });
            }

            return section;
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
                    Quantity = step.Quantity
                });
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

        private static string ResolveCurrencyName(int currencyId)
        {
            return Gw2Constants.ResolveCurrencyName(currencyId);
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
