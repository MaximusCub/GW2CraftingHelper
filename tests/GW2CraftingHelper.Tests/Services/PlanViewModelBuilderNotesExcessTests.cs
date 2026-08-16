using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// design-plan-notes.md (Notes section, excess/reclaim) -
    /// PlanViewModelBuilder.BuildNotesSection's excess-line assembly, given
    /// an already-computed CraftingPlanResult.ExcessCraftOutputs list (the
    /// calculator's own aggregation arithmetic is covered separately by
    /// ExcessCraftOutputCalculatorTests).
    /// </summary>
    public class PlanViewModelBuilderNotesExcessTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        [Fact]
        public void SingleExcessEntry_OneRow_NoTotalRow()
        {
            var meta = MetaFor((10, "Iron Ingot", "iron.png"));
            var result = MakeResult(
                metadata: meta,
                excessCraftOutputs: new List<ExcessCraftOutput>
                {
                    new ExcessCraftOutput { ItemId = 10, ExcessQuantity = 12, ReclaimValue = 500 }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.NoteLine, section.Rows[0].RowType);
            Assert.Equal("Excess: 12x Iron Ingot", section.Rows[0].Label);
            Assert.Equal(500, section.Rows[0].CoinValue);
        }

        [Fact]
        public void TwoOrMoreExcessEntries_PerItemRowsPlusTotalRow()
        {
            var meta = MetaFor((10, "Iron Ingot", "iron.png"), (20, "Wood Log", "wood.png"));
            var result = MakeResult(
                metadata: meta,
                excessCraftOutputs: new List<ExcessCraftOutput>
                {
                    new ExcessCraftOutput { ItemId = 10, ExcessQuantity = 12, ReclaimValue = 500 },
                    new ExcessCraftOutput { ItemId = 20, ExcessQuantity = 3, ReclaimValue = 90 }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            // 2 per-item rows + 1 total row.
            Assert.Equal(3, section.Rows.Count);
            Assert.All(section.Rows, r => Assert.Equal(PlanRowType.NoteLine, r.RowType));

            var total = section.Rows.Last();
            Assert.Equal("Total reclaimable value", total.Label);
            Assert.Equal(590, total.CoinValue);
        }

        [Fact]
        public void PerItemRows_SortedAlphabeticallyByResolvedName()
        {
            var meta = MetaFor((10, "Zircon Ore", "z.png"), (20, "Apple", "a.png"));
            var result = MakeResult(
                metadata: meta,
                excessCraftOutputs: new List<ExcessCraftOutput>
                {
                    new ExcessCraftOutput { ItemId = 10, ExcessQuantity = 1, ReclaimValue = 10 },
                    new ExcessCraftOutput { ItemId = 20, ExcessQuantity = 1, ReclaimValue = 10 }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.Contains("Apple", section.Rows[0].Label);
            Assert.Contains("Zircon Ore", section.Rows[1].Label);
        }

        [Fact]
        public void AccountBoundEntry_RendersWithNoCoinValue()
        {
            var meta = MetaFor((10, "Bound Widget", "widget.png"));
            var result = MakeResult(
                metadata: meta,
                excessCraftOutputs: new List<ExcessCraftOutput>
                {
                    new ExcessCraftOutput
                    {
                        ItemId = 10,
                        ExcessQuantity = 5,
                        ReclaimValue = null,
                        IsAccountBound = true
                    }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.Single(section.Rows);
            Assert.Equal(0, section.Rows[0].CoinValue);
            Assert.Contains("account-bound", section.Rows[0].Label);
        }

        [Fact]
        public void EmptyExcessList_NoRows_SectionAbsent()
        {
            var result = MakeResult(excessCraftOutputs: new List<ExcessCraftOutput>());

            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.Notes);
        }
    }
}
