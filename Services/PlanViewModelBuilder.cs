using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class PlanViewModelBuilder
    {
        public PlanViewModel Build(CraftingPlanResult result)
        {
            var vm = new PlanViewModel
            {
                TargetQuantity = result.Plan.TargetQuantity
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
            section.Rows.Add(new PlanRowViewModel
            {
                RowType = PlanRowType.CoinTotal,
                Label = "Total",
                CoinValue = result.Plan.TotalCoinCost
            });

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
                    if (recipe != null && recipe.Disciplines.Count > 0)
                    {
                        sublabel = $"{recipe.Disciplines[0]} {recipe.MinRating}";
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

                string sublabel = "";
                if (recipe.Disciplines.Count > 0)
                {
                    sublabel = $"{recipe.Disciplines[0]} {recipe.MinRating}";
                }

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

        private static readonly Dictionary<int, string> KnownCurrencyNames = new Dictionary<int, string>
        {
            { 2, "Karma" },
            { 3, "Laurels" },
            { 4, "Gems" },
            { 5, "Ascalonian Tears" },
            { 6, "Shards of Zhaitan" },
            { 7, "Fractal Relics" },
            { 9, "Seals of Beetletun" },
            { 10, "Manifesto of the Moletariate" },
            { 11, "Deadly Blooms" },
            { 12, "Symbols of Koda" },
            { 13, "Flame Legion Charr Carvings" },
            { 14, "Knowledge Crystals" },
            { 15, "Badges of Honor" },
            { 16, "Guild Commendations" },
            { 18, "Transmutation Charges" },
            { 19, "Airship Parts" },
            { 20, "Ley Line Crystals" },
            { 22, "Lumps of Aurillium" },
            { 23, "Spirit Shards" },
            { 24, "Pristine Fractal Relics" },
            { 25, "Geodes" },
            { 26, "WvW Skirmish Claim Tickets" },
            { 27, "Bandit Crests" },
            { 28, "Magnetite Shards" },
            { 29, "Provisioner Tokens" },
            { 30, "PvP League Tickets" },
            { 32, "Unbound Magic" },
            { 33, "Ascended Shards of Glory" },
            { 34, "Trade Contracts" },
            { 36, "Elegy Mosaics" },
            { 45, "Volatile Magic" },
            { 47, "Racing Medallions" },
            { 49, "Festival Tokens" },
            { 50, "Mistborn Motes" },
            { 58, "Jade Slivers" },
            { 59, "Research Notes" },
            { 60, "Imperial Favors" },
            { 62, "Unusual Coins" },
            { 63, "Rift Essences" }
        };

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
            if (KnownCurrencyNames.TryGetValue(currencyId, out var name))
            {
                return name;
            }
            return "Currency";
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
    }
}
