using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// opportunity-notes (RECIPE-SHEET SAVINGS) -
    /// PlanViewModelBuilder.BuildNotesSection's formatting, given an
    /// already-computed CraftingPlanResult.RecipeSheetSavingsOpportunities
    /// list (the calculator's own math is covered separately by
    /// RecipeSheetSavingsCalculatorTests).
    /// </summary>
    public class PlanViewModelBuilderNotesRecipeSheetSavingsTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        [Fact]
        public void PlainOpportunity_TwoRows_CorrectCoinValues()
        {
            var meta = MetaFor((10, "Spirit Shard", "s.png"));
            var result = MakeResult(
                metadata: meta,
                recipeSheetSavingsOpportunities: new List<RecipeSheetSavingsOpportunity>
                {
                    new RecipeSheetSavingsOpportunity
                    {
                        ItemId = 10, RecipeId = 999, SheetItemId = 500,
                        SheetCost = 200, SavingsPerUnit = 20, DisciplineBlocked = false
                    }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.Equal(2, section.Rows.Count);
            Assert.All(section.Rows, r => Assert.Equal(PlanRowType.NoteLine, r.RowType));
            Assert.Contains("Buy the Spirit Shard recipe", section.Rows[0].Label);
            Assert.Equal(200, section.Rows[0].CoinValue);
            Assert.Equal(20, section.Rows[1].CoinValue);
            Assert.Equal("Notes (1)", section.Title);
        }

        [Fact]
        public void DisciplineBlocked_UsesTrainingWording()
        {
            var meta = MetaFor((10, "Spirit Shard", "s.png"));
            var result = MakeResult(
                metadata: meta,
                recipeSheetSavingsOpportunities: new List<RecipeSheetSavingsOpportunity>
                {
                    new RecipeSheetSavingsOpportunity
                    {
                        ItemId = 10, RecipeId = 999, SheetItemId = 500,
                        SheetCost = 200, SavingsPerUnit = 20,
                        DisciplineBlocked = true, Discipline = "Chef", RequiredRating = 400
                    }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.StartsWith("Train Chef to 400 and buy the Spirit Shard recipe", section.Rows[0].Label);
        }

        [Fact]
        public void MultipleOpportunities_SortedAlphabeticallyByResolvedName()
        {
            var meta = MetaFor((10, "Zircon Ore", "z.png"), (20, "Apple", "a.png"));
            var result = MakeResult(
                metadata: meta,
                recipeSheetSavingsOpportunities: new List<RecipeSheetSavingsOpportunity>
                {
                    new RecipeSheetSavingsOpportunity { ItemId = 10, RecipeId = 1, SheetItemId = 100, SheetCost = 10, SavingsPerUnit = 1 },
                    new RecipeSheetSavingsOpportunity { ItemId = 20, RecipeId = 2, SheetItemId = 200, SheetCost = 10, SavingsPerUnit = 1 }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.Contains("Apple", section.Rows[0].Label);
            Assert.Contains("Zircon Ore", section.Rows[2].Label);
            Assert.Equal("Notes (2)", section.Title);
        }

        [Fact]
        public void NoOpportunities_NoRows()
        {
            var result = MakeResult(recipeSheetSavingsOpportunities: new List<RecipeSheetSavingsOpportunity>());

            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.Notes);
        }
    }
}
