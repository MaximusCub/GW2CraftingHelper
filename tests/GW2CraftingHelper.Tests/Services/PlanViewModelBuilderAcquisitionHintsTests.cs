using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanViewModelBuilderAcquisitionHintsTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        // --- Acquisition hints ---
        [Fact]
        public void ShoppingList_UnknownSource_WithHint_PopulatesHintText()
        {
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Salvaged from ascended gear." },
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource },
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Equal("Salvaged from ascended gear.", section.Rows[0].HintText);
        }

        [Fact]
        public void ShoppingList_UnknownSource_NoHintsDict_HintTextNull()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource },
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Null(section.Rows[0].HintText);
        }

        [Fact]
        public void ShoppingList_NonUnknownSource_HintsPresent_HintTextStaysNull()
        {
            // A hint entry exists for the item, but the row is a normal TP
            // purchase, not an unknown-source row - the hint must not bleed
            // onto a priced row's tooltip.
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Should never appear on a priced row." },
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 },
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Null(section.Rows[0].HintText);
        }

        [Fact]
        public void ShoppingList_UnknownSource_EmptyHintString_HintTextStaysNull()
        {
            // Empty-string Hint (as opposed to a missing dict entry) must
            // resolve to null, same guard as CraftingTreeBuilder's
            // ApplyAcquisitionHint uses for AcquisitionHint.
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "" },
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource },
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Null(section.Rows[0].HintText);
        }

        [Fact]
        public void ShoppingList_UnknownSource_WithBadge_PopulatesBadgeText()
        {
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Salvaged from ascended gear.", Badge = "SALVAGE" },
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource },
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Equal("SALVAGE", section.Rows[0].BadgeText);
        }

        [Fact]
        public void ShoppingList_UnknownSource_NoBadge_BadgeTextNull()
        {
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Salvaged from ascended gear." },
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource },
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Null(section.Rows[0].BadgeText);
        }

        [Fact]
        public void ShoppingList_NonUnknownSource_BadgePresent_BadgeTextStaysNull()
        {
            // Same non-bleed guarantee as HintText: a badge entry existing
            // for the item must not appear on a priced row's tag.
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Should never appear.", Badge = "SALVAGE" },
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 },
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Null(section.Rows[0].BadgeText);
        }
    }
}
