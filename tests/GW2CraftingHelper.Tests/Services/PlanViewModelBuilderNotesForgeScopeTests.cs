using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;
using static TaimisToolbench.Tests.Helpers.CraftingPlanResultBuilders;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// Plan Notes forge-scope line:
    /// PlanViewModelBuilder.BuildNotesSection's forge-scope line, driven by
    /// CraftingPlanResult.ProbabilisticForgeOutputItemIds (populated by
    /// PlanResultBuilder - see PlanResultBuilderTests'
    /// ProbabilisticForgeOutputItemIds_* tests for that population logic).
    /// </summary>
    public class PlanViewModelBuilderNotesForgeScopeTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        [Fact]
        public void NonEmptyForgeOutputIds_ProducesExactlyOneRow_RegardlessOfCount()
        {
            var result = MakeResult(
                probabilisticForgeOutputItemIds: new List<int> { 1, 2, 3 });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            // One NoteLine row carrying the whole caveat: the renderer
            // width-wraps it across as many fixed-height rows as it needs,
            // so the builder no longer hand-splits it into complete
            // sentences to keep the tail on screen. Still exactly ONE
            // logical note regardless of forgeOutputIds.Count.
            var noteRow = Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.NoteLine, noteRow.RowType);
            Assert.Contains("Mystic Clover", noteRow.Label);
            Assert.Contains("precursor forging", noteRow.Label);
            Assert.Contains("never models or shows them", noteRow.Label);
            Assert.Equal("Notes (1)", section.Title);
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
