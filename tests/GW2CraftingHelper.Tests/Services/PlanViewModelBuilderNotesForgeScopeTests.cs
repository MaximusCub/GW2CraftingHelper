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
        public void NonEmptyForgeOutputIds_ProducesExactlyThreeLines_RegardlessOfCount()
        {
            var result = MakeResult(
                probabilisticForgeOutputItemIds: new List<int> { 1, 2, 3 });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            // Review fix (finding 4, MEASURED): the single ~243-char row
            // overflowed NotesSectionRenderer's fixed 28px row and clipped
            // the "never models and never shows" caveat - split, verbatim,
            // into 3 NoteLine rows. Still exactly ONE logical note
            // regardless of forgeOutputIds.Count (see the "Notes (N)"
            // assertion below, which counts logical entries, not rows).
            Assert.Equal(3, section.Rows.Count);
            Assert.All(section.Rows, r => Assert.Equal(PlanRowType.NoteLine, r.RowType));
            string combined = string.Join(" ", section.Rows.Select(r => r.Label));
            Assert.Contains("Mystic Clover", combined);
            Assert.Contains("precursor forging", combined);
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
