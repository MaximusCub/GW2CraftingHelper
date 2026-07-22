using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanViewModelBuilderSublabelTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        // --- FormatDisciplineSublabel ---

        [Fact]
        public void FormatDisciplineSublabel_SingleDiscipline()
        {
            var planDiscNames = new HashSet<string> { "Weaponsmith" };
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string> { "Weaponsmith" }, 400, planDiscNames);

            Assert.Equal("Weaponsmith 400", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_MultiDiscipline_FiltersToRelevant()
        {
            var planDiscNames = new HashSet<string> { "Weaponsmith" };
            // Recipe has 4 disciplines, but plan only uses Weaponsmith
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string> { "Weaponsmith", "Armorsmith", "Huntsman", "Artificer" },
                400, planDiscNames);

            Assert.Equal("Weaponsmith 400", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_MultiDiscipline_MultiRelevant()
        {
            var planDiscNames = new HashSet<string> { "Armorsmith", "Weaponsmith" };
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string> { "Weaponsmith", "Armorsmith", "Huntsman" },
                400, planDiscNames);

            Assert.Equal("Armorsmith / Weaponsmith 400", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_NoDisciplines_EmptyString()
        {
            var planDiscNames = new HashSet<string> { "Weaponsmith" };
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string>(), 0, planDiscNames);

            Assert.Equal("", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_NullDisciplines_EmptyString()
        {
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                null, 0, new HashSet<string>());

            Assert.Equal("", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_NoIntersection_FallbackToAll()
        {
            // Plan disciplines don't overlap with recipe disciplines
            var planDiscNames = new HashSet<string> { "Leatherworker" };
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string> { "Weaponsmith", "Armorsmith" },
                300, planDiscNames);

            Assert.Equal("Armorsmith / Weaponsmith 300", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_NullPlanDiscNames_ShowsAll()
        {
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string> { "Weaponsmith", "Armorsmith" }, 400, null);

            Assert.Equal("Armorsmith / Weaponsmith 400", result);
        }

        // --- Recipe sublabel integration ---

        [Fact]
        public void RequiredRecipes_Sublabel_ShowsRelevantDisciplines()
        {
            var result = MakeResult(
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 }
                },
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = 10,
                        OutputItemId = 1,
                        IsAutoLearned = true,
                        Disciplines = new List<string> { "Weaponsmith", "Armorsmith", "Huntsman" },
                        MinRating = 400
                    }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Weaponsmith 400", section.Rows[0].Sublabel);
        }

        [Fact]
        public void CraftingSteps_Sublabel_ShowsRelevantDisciplines()
        {
            var result = MakeResult(
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 }
                },
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = 10,
                        OutputItemId = 2,
                        IsAutoLearned = true,
                        Disciplines = new List<string> { "Weaponsmith", "Armorsmith", "Huntsman", "Artificer" },
                        MinRating = 400
                    }
                },
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Equal("Weaponsmith 400", section.Rows[0].Sublabel);
        }
    }
}
