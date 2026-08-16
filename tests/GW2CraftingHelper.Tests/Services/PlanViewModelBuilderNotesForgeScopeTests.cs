using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// design-plan-notes.md (Notes section, gambling-forge scope) -
    /// PlanViewModelBuilder.BuildNotesSection's forge-scope line, driven by
    /// CraftingPlanResult.ProbabilisticForgeOutputItemIds (populated by
    /// PlanResultBuilder - see PlanResultBuilderTests'
    /// ProbabilisticForgeOutputItemIds_* tests for that population logic).
    /// </summary>
    public class PlanViewModelBuilderNotesForgeScopeTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        [Fact]
        public void NonEmptyForgeOutputIds_ProducesExactlyOneLine_RegardlessOfCount()
        {
            var result = MakeResult(
                probabilisticForgeOutputItemIds: new List<int> { 1, 2, 3 });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.NoteLine, section.Rows[0].RowType);
            Assert.Contains("Mystic Clover", section.Rows[0].Label);
            Assert.Contains("precursor forging", section.Rows[0].Label);
        }

        [Fact]
        public void EmptyForgeOutputIds_ProducesNoLine()
        {
            var result = MakeResult(probabilisticForgeOutputItemIds: new List<int>());

            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.Notes);
        }
    }
}
