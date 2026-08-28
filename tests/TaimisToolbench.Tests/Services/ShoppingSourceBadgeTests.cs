using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;
using static TaimisToolbench.Tests.Helpers.CraftingPlanResultBuilders;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// Drives the badge mapping from real PlanViewModelBuilder output
    /// rather than hand-built row objects - the property under test is
    /// "every row the Shopping List actually emits is badged", which only
    /// means something if the rows come from the builder that emits them.
    /// </summary>
    public class ShoppingSourceBadgeTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        private static IReadOnlyList<PlanRowViewModel> ShoppingRows(PlanViewModel vm)
        {
            return vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList).Rows;
        }

        [Fact]
        public void BuyFromTp_IsBadgedTp()
        {
            var vm = _builder.Build(MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 },
            }));

            Assert.Equal("TP", ShoppingSourceBadge.ForRow(ShoppingRows(vm)[0]));
        }

        [Fact]
        public void BuyFromVendor_IsBadgedVendor()
        {
            var vm = _builder.Build(MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.BuyFromVendor, TotalCost = 100 },
            }));

            Assert.Equal("VENDOR", ShoppingSourceBadge.ForRow(ShoppingRows(vm)[0]));
        }

        [Fact]
        public void Currency_IsBadgedCurrency()
        {
            var vm = _builder.Build(MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 2, Source = AcquisitionSource.Currency },
            }));

            Assert.Equal("CURRENCY", ShoppingSourceBadge.ForRow(ShoppingRows(vm)[0]));
        }

        [Fact]
        public void UnknownSource_NoSeededBadge_FallsBackToUnknown()
        {
            var vm = _builder.Build(MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource },
            }));

            Assert.Equal("UNKNOWN", ShoppingSourceBadge.ForRow(ShoppingRows(vm)[0]));
        }

        [Fact]
        public void UnknownSource_SeededBadge_PrefersIt()
        {
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Salvaged from ascended gear.", Badge = "SALVAGE" },
            };
            var vm = _builder.Build(MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource },
                },
                acquisitionHints: hints));

            Assert.Equal("SALVAGE", ShoppingSourceBadge.ForRow(ShoppingRows(vm)[0]));
        }

        [Theory]
        [InlineData("VENDOR")]
        [InlineData("TP")]
        [InlineData("CURRENCY")]
        public void UnknownSource_SeededBadgeCollidingWithASourceBadge_FallsBackToUnknown(string badge)
        {
            // The unknown row and the real source row would carry the same
            // badge in the same list while meaning opposite things - the
            // source row has a cost in Plan.TotalCoinCost, the unknown row
            // contributes 0. The hint text still reaches TooltipForRow.
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Bought with account-bound tokens.", Badge = badge },
            };
            var vm = _builder.Build(MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource },
                },
                acquisitionHints: hints));

            var row = ShoppingRows(vm)[0];
            Assert.Equal("UNKNOWN", ShoppingSourceBadge.ForRow(row));
            Assert.Equal("Bought with account-bound tokens.", ShoppingSourceBadge.TooltipForRow(row));
        }

        /// <summary>
        /// The regression this finding is about: an unbadged shopping row
        /// used to silently mean "Trading Post". No row in a mixed list may
        /// be unbadged now, whatever its source.
        /// </summary>
        [Fact]
        public void MixedList_EveryRowIsBadged()
        {
            var vm = _builder.Build(MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 },
                new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.BuyFromVendor, TotalCost = 100 },
                new PlanStep { ItemId = 3, Quantity = 2, Source = AcquisitionSource.Currency },
                new PlanStep { ItemId = 4, Quantity = 1, Source = AcquisitionSource.UnknownSource },
            }));

            var rows = ShoppingRows(vm);
            Assert.Equal(4, rows.Count);
            Assert.All(rows, row => Assert.False(string.IsNullOrEmpty(ShoppingSourceBadge.ForRow(row))));
        }

        [Fact]
        public void NonShoppingRow_IsNotBadged()
        {
            // A Used Materials row is never rendered by the Shopping List,
            // so it must not acquire a source badge by accident.
            var vm = _builder.Build(MakeResult(
                metadata: MetaFor((10, "Ori Ingot", "ori.png")),
                usedMaterials: new List<UsedMaterial>
                {
                    new UsedMaterial { ItemId = 10, QuantityUsed = 5 },
                }));

            var used = vm.Sections.First(s => s.SectionType == PlanSectionType.UsedMaterials).Rows[0];
            Assert.Null(ShoppingSourceBadge.ForRow(used));
        }

        [Fact]
        public void NullRow_IsNotBadged()
        {
            Assert.Null(ShoppingSourceBadge.ForRow(null));
        }

        // --- Badge hover prose (the badge column's own tooltip: the
        // capitals say WHICH source, this says what to do about it) ---
        [Fact]
        public void EveryBadgedRow_AlsoCarriesProse()
        {
            var vm = _builder.Build(MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 },
                new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.BuyFromVendor, TotalCost = 100 },
                new PlanStep { ItemId = 3, Quantity = 2, Source = AcquisitionSource.Currency },
                new PlanStep { ItemId = 4, Quantity = 1, Source = AcquisitionSource.UnknownSource },
            }));

            Assert.All(
                ShoppingRows(vm),
                row => Assert.False(string.IsNullOrEmpty(ShoppingSourceBadge.TooltipForRow(row))));
        }

        [Fact]
        public void TpProse_NamesTheTradingPost()
        {
            var vm = _builder.Build(MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 },
            }));

            Assert.Equal("Buy on the Trading Post", ShoppingSourceBadge.TooltipForRow(ShoppingRows(vm)[0]));
        }

        [Fact]
        public void UnknownProse_PrefersTheSeededHintOverTheGenericLine()
        {
            // The seeded hint is specific to this item; the generic "check
            // the wiki" line is strictly less useful on top of it.
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Salvaged from ascended gear.", Badge = "SALVAGE" },
            };
            var vm = _builder.Build(MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource },
                },
                acquisitionHints: hints));

            Assert.Equal(
                "Salvaged from ascended gear.", ShoppingSourceBadge.TooltipForRow(ShoppingRows(vm)[0]));
        }

        [Fact]
        public void UnknownProse_NoHint_SaysThereIsNoKnownSource()
        {
            var vm = _builder.Build(MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource },
            }));

            Assert.Equal(
                "No known acquisition source - check the item's wiki page",
                ShoppingSourceBadge.TooltipForRow(ShoppingRows(vm)[0]));
        }

        [Fact]
        public void NonShoppingRow_AndNull_CarryNoProse()
        {
            var vm = _builder.Build(MakeResult(
                metadata: MetaFor((10, "Ori Ingot", "ori.png")),
                usedMaterials: new List<UsedMaterial>
                {
                    new UsedMaterial { ItemId = 10, QuantityUsed = 5 },
                }));

            var used = vm.Sections.First(s => s.SectionType == PlanSectionType.UsedMaterials).Rows[0];
            Assert.Null(ShoppingSourceBadge.TooltipForRow(used));
            Assert.Null(ShoppingSourceBadge.TooltipForRow(null));
        }
    }
}
