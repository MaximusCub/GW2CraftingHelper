using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Plan Notes competency lines:
    /// PlanViewModelBuilder.BuildNotesSection's competency-line assembly,
    /// built on the shared BestCharacterRating helper (extracted from
    /// BuildCharacterAvailabilityText so the Required Disciplines column
    /// and this section's wording can never drift on what counts as
    /// "blocked").
    /// </summary>
    public class PlanViewModelBuilderNotesCompetencyTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        [Fact]
        public void RequiredDisciplineBelowEveryCharacterRating_ProducesHighestOnAccountLine()
        {
            var result = MakeResult(
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 },
                },
                characterDisciplines: new List<SnapshotCharacterDiscipline>
                {
                    new SnapshotCharacterDiscipline { CharacterName = "Aria", Discipline = "Weaponsmith", Rating = 400 },
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.NoteLine, section.Rows[0].RowType);
            Assert.Equal(
                "Weaponsmith 500 required - highest on this account: 400 (Aria)",
                section.Rows[0].Label);
        }

        [Fact]
        public void FullyCoveredDiscipline_ProducesNoLine()
        {
            var result = MakeResult(
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 400 },
                },
                characterDisciplines: new List<SnapshotCharacterDiscipline>
                {
                    new SnapshotCharacterDiscipline { CharacterName = "Aria", Discipline = "Weaponsmith", Rating = 400 },
                });

            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.Notes);
        }

        [Fact]
        public void NullCharacterDisciplines_NoSnapshot_ProducesNoLines()
        {
            var result = MakeResult(
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 },
                },
                characterDisciplines: null);

            var vm = _builder.Build(result);

            // Never a false "blocked" claim when no snapshot exists at all.
            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.Notes);
        }

        [Fact]
        public void NoCharacterHasDiscipline_ProducesNotTrainedWording()
        {
            var result = MakeResult(
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 },
                },
                characterDisciplines: new List<SnapshotCharacterDiscipline>
                {
                    new SnapshotCharacterDiscipline { CharacterName = "Aria", Discipline = "Armorsmith", Rating = 400 },
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.Single(section.Rows);
            Assert.Equal(
                "Weaponsmith 500 required - not trained on any character",
                section.Rows[0].Label);
        }
    }
}
