using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanViewModelBuilderTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        private static CraftingPlanResult MakeResult(
            int targetItemId = 1,
            int targetQuantity = 1,
            long totalCoinCost = 0,
            List<PlanStep> steps = null,
            List<CurrencyCost> currencyCosts = null,
            Dictionary<int, ItemMetadata> metadata = null,
            List<UsedMaterial> usedMaterials = null,
            List<RequiredDiscipline> requiredDisciplines = null,
            List<RequiredRecipe> requiredRecipes = null)
        {
            return new CraftingPlanResult
            {
                Plan = new CraftingPlan
                {
                    TargetItemId = targetItemId,
                    TargetQuantity = targetQuantity,
                    TotalCoinCost = totalCoinCost,
                    Steps = steps ?? new List<PlanStep>(),
                    CurrencyCosts = currencyCosts ?? new List<CurrencyCost>()
                },
                ItemMetadata = metadata != null
                    ? metadata
                    : new Dictionary<int, ItemMetadata>(),
                UsedMaterials = usedMaterials,
                RequiredDisciplines = requiredDisciplines ?? new List<RequiredDiscipline>(),
                RequiredRecipes = requiredRecipes ?? new List<RequiredRecipe>(),
                DebugLog = new List<string>()
            };
        }

        private static Dictionary<int, ItemMetadata> MetaFor(params (int id, string name, string icon)[] items)
        {
            var dict = new Dictionary<int, ItemMetadata>();
            foreach (var (id, name, icon) in items)
            {
                dict[id] = new ItemMetadata { ItemId = id, Name = name, IconUrl = icon };
            }
            return dict;
        }

        // --- Empty plan ---

        [Fact]
        public void EmptyPlan_ReturnsSummarySectionOnly()
        {
            var result = MakeResult();
            var vm = _builder.Build(result);

            Assert.Single(vm.Sections);
            Assert.Equal(PlanSectionType.Summary, vm.Sections[0].SectionType);
            Assert.Single(vm.Sections[0].Rows); // CoinTotal row
            Assert.Equal(PlanRowType.CoinTotal, vm.Sections[0].Rows[0].RowType);
            Assert.Equal(0L, vm.Sections[0].Rows[0].CoinValue);
        }

        // --- Target item resolution ---

        [Fact]
        public void TargetItem_ResolvesNameAndIcon()
        {
            var meta = MetaFor((1, "Zojja's Claymore", "claymore.png"));
            var result = MakeResult(targetItemId: 1, metadata: meta);
            var vm = _builder.Build(result);

            Assert.Equal("Zojja's Claymore", vm.TargetItemName);
            Assert.Equal("claymore.png", vm.TargetIconUrl);
        }

        [Fact]
        public void TargetItem_MissingMetadata_FallsBack()
        {
            var result = MakeResult(targetItemId: 999);
            var vm = _builder.Build(result);

            Assert.Equal("Unknown Item", vm.TargetItemName);
            Assert.Null(vm.TargetIconUrl);
        }

        // --- Summary section ---

        [Fact]
        public void SummarySection_CoinTotalRow()
        {
            var result = MakeResult(totalCoinCost: 123456);
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var coinRow = summary.Rows.First(r => r.RowType == PlanRowType.CoinTotal);
            Assert.Equal(123456L, coinRow.CoinValue);
            Assert.Equal("Total", coinRow.Label);
        }

        [Fact]
        public void SummarySection_CurrencyCosts()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 50 },
                new CurrencyCost { CurrencyId = 45, Amount = 100 }
            });
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var ccRows = summary.Rows.Where(r => r.RowType == PlanRowType.CurrencyCost).ToList();
            Assert.Equal(2, ccRows.Count);
            Assert.Equal("50x Currency#23", ccRows[0].Label);
            Assert.Equal("100x Currency#45", ccRows[1].Label);
        }

        // --- Used Materials ---

        [Fact]
        public void UsedMaterials_NonEmpty_CreatesSection()
        {
            var meta = MetaFor((10, "Ori Ingot", "ori.png"), (20, "Mithril Ore", "mith.png"));
            var result = MakeResult(
                metadata: meta,
                usedMaterials: new List<UsedMaterial>
                {
                    new UsedMaterial { ItemId = 10, QuantityUsed = 5 },
                    new UsedMaterial { ItemId = 20, QuantityUsed = 3 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.FirstOrDefault(s => s.SectionType == PlanSectionType.UsedMaterials);
            Assert.NotNull(section);
            Assert.Equal("Used Materials (2)", section.Title);
            Assert.Equal(2, section.Rows.Count);
            Assert.Equal("Ori Ingot", section.Rows[0].Label);
            Assert.Equal(5, section.Rows[0].Quantity);
            Assert.Equal("ori.png", section.Rows[0].IconUrl);
            Assert.Equal(PlanRowType.UsedMaterial, section.Rows[0].RowType);
        }

        [Fact]
        public void UsedMaterials_Empty_NoSection()
        {
            var result = MakeResult(usedMaterials: new List<UsedMaterial>());
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.UsedMaterials);
        }

        [Fact]
        public void UsedMaterials_Null_NoSection()
        {
            var result = MakeResult(usedMaterials: null);
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.UsedMaterials);
        }

        // --- Shopping List ---

        [Fact]
        public void ShoppingList_BuyFromTp_CorrectRowType()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.ShoppingBuy, section.Rows[0].RowType);
        }

        [Fact]
        public void ShoppingList_BuyFromVendor_CorrectRowType()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.BuyFromVendor, TotalCost = 100 }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.ShoppingVendor, section.Rows[0].RowType);
        }

        [Fact]
        public void ShoppingList_Currency_CorrectRowType()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 2, Source = AcquisitionSource.Currency }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.ShoppingCurrency, section.Rows[0].RowType);
        }

        [Fact]
        public void ShoppingList_UnknownSource_CorrectRowType()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.ShoppingUnknown, section.Rows[0].RowType);
        }

        [Fact]
        public void ShoppingList_CoinValueFromTotalCost()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 5, Source = AcquisitionSource.BuyFromTp, TotalCost = 5000 }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Equal(5000L, section.Rows[0].CoinValue);
        }

        // --- Crafting Steps ---

        [Fact]
        public void CraftingSteps_OnlyCraftSource()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp },
                new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                new PlanStep { ItemId = 3, Quantity = 2, Source = AcquisitionSource.BuyFromVendor }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.CraftStep, section.Rows[0].RowType);
        }

        [Fact]
        public void CraftingSteps_PreservesOrder()
        {
            var meta = MetaFor((2, "Blade", "blade.png"), (3, "Hilt", "hilt.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                    new PlanStep { ItemId = 3, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 20 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Equal(2, section.Rows.Count);
            Assert.Equal("Blade", section.Rows[0].Label);
            Assert.Equal("Hilt", section.Rows[1].Label);
        }

        [Fact]
        public void NoCraftSteps_NoCraftingSection()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 5, Source = AcquisitionSource.BuyFromTp }
            });
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.CraftingSteps);
        }

        // --- Required Disciplines ---

        [Fact]
        public void RequiredDisciplines_MapsCorrectly()
        {
            var result = MakeResult(requiredDisciplines: new List<RequiredDiscipline>
            {
                new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredDisciplines);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.DisciplineRow, section.Rows[0].RowType);
            Assert.Equal("Weaponsmith", section.Rows[0].Label);
            Assert.Equal("Level 500", section.Rows[0].Sublabel);
        }

        [Fact]
        public void RequiredDisciplines_Empty_NoSection()
        {
            var result = MakeResult(requiredDisciplines: new List<RequiredDiscipline>());
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.RequiredDisciplines);
        }

        // --- Required Recipes ---

        [Fact]
        public void RequiredRecipes_AutoLearned_StatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = true,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = null
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Auto-learned", section.Rows[0].StatusTag);
        }

        [Fact]
        public void RequiredRecipes_Missing_StatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = true
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Missing!", section.Rows[0].StatusTag);
        }

        [Fact]
        public void RequiredRecipes_Learned_StatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = false
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Learned", section.Rows[0].StatusTag);
        }

        [Fact]
        public void RequiredRecipes_NullMissing_EmptyStatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = null
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("", section.Rows[0].StatusTag);
        }

        [Fact]
        public void RequiredRecipes_OutputName_FromMetadata()
        {
            var meta = MetaFor((5, "Cool Blade", "blade.png"));
            var result = MakeResult(
                metadata: meta,
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = 10,
                        OutputItemId = 5,
                        IsAutoLearned = true,
                        Disciplines = new List<string> { "Weaponsmith" },
                        MinRating = 400
                    }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Cool Blade", section.Rows[0].Label);
            Assert.Equal("blade.png", section.Rows[0].IconUrl);
        }

        // --- Section order ---

        [Fact]
        public void SectionOrder_MatchesSpec()
        {
            var meta = MetaFor(
                (1, "Target", "t.png"),
                (2, "Blade", "b.png"),
                (3, "Ore", "o.png"),
                (10, "Used", "u.png"));
            var result = MakeResult(
                targetItemId: 1,
                metadata: meta,
                usedMaterials: new List<UsedMaterial>
                {
                    new UsedMaterial { ItemId = 10, QuantityUsed = 1 }
                },
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 3, Quantity = 5, Source = AcquisitionSource.BuyFromTp, TotalCost = 500 },
                    new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 20 }
                },
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 }
                },
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = 20,
                        OutputItemId = 2,
                        IsAutoLearned = true,
                        Disciplines = new List<string> { "Weaponsmith" },
                        MinRating = 500
                    }
                });
            var vm = _builder.Build(result);

            var types = vm.Sections.Select(s => s.SectionType).ToList();
            Assert.Equal(new[]
            {
                PlanSectionType.Summary,
                PlanSectionType.UsedMaterials,
                PlanSectionType.ShoppingList,
                PlanSectionType.CraftingSteps,
                PlanSectionType.RequiredDisciplines,
                PlanSectionType.RequiredRecipes
            }, types);
        }

        // --- Mixed steps ---

        [Fact]
        public void MixedSteps_CorrectSectionAssignment()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 },
                new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                new PlanStep { ItemId = 3, Quantity = 2, Source = AcquisitionSource.BuyFromVendor, TotalCost = 200 }
            });
            var vm = _builder.Build(result);

            var shopping = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Equal(2, shopping.Rows.Count);
            Assert.Contains(shopping.Rows, r => r.RowType == PlanRowType.ShoppingBuy);
            Assert.Contains(shopping.Rows, r => r.RowType == PlanRowType.ShoppingVendor);

            var crafting = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Single(crafting.Rows);
            Assert.Equal(PlanRowType.CraftStep, crafting.Rows[0].RowType);
        }

        // --- Target quantity ---

        [Fact]
        public void TargetQuantity_PassedThrough()
        {
            var result = MakeResult(targetQuantity: 5);
            var vm = _builder.Build(result);

            Assert.Equal(5, vm.TargetQuantity);
        }
    }
}
