using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Audit row 56 (daily craft-cooldown notices): the additive notice
    /// pass PlanViewModelBuilder.AppendDailyCooldownNotices runs over
    /// Craft-source steps, keyed on CraftingPlanResult.DailyCooldownItems.
    /// Mirrors PlanViewModelBuilderStepSectionsTests' existing
    /// TimegatedItems_* coverage for the pre-existing vendor-cap notice,
    /// but exercises the new craft-cooldown pass instead - the two are
    /// independent (see PlanViewModelBuilder.AppendDailyCooldownNotices'
    /// own doc comment) and this file never touches Plan.TimegatedItems.
    /// </summary>
    public class PlanViewModelBuilderDailyCooldownTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        [Fact]
        public void CraftStep_ExceedsDailyCap_AppendsNoticeWithDaysEstimate()
        {
            var meta = MetaFor((46742, "Lump of Mithrillium", "lump.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 46742, Quantity = 30, Source = AcquisitionSource.Craft, RecipeId = 7319 }
                },
                dailyCooldownItems: new Dictionary<int, DailyCooldownItem>
                {
                    [46742] = new DailyCooldownItem { ItemId = 46742, PerDayCap = 1, SourceUrl = "https://wiki.guildwars2.com/wiki/Lump_of_Mithrillium", LastVerified = "2026-08-16" }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Equal(2, section.Rows.Count);
            Assert.Equal(PlanRowType.CraftStep, section.Rows[0].RowType);
            var notice = section.Rows[1];
            Assert.Equal(PlanRowType.TimegatedNotice, notice.RowType);
            Assert.Contains("Lump of Mithrillium", notice.Label);
            Assert.Contains("30", notice.Label);
            Assert.Contains("30 days", notice.Label);
            // Follow-up fix (recorded non-blocking): a single notice has no
            // other daily-gated item to run in parallel with, so the
            // clause must be dropped, not rendered unconditionally.
            Assert.DoesNotContain("runs in parallel", notice.Label);
        }

        [Fact]
        public void CraftStep_AtOrUnderDailyCap_NoNotice()
        {
            var meta = MetaFor((46742, "Lump of Mithrillium", "lump.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 46742, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 7319 }
                },
                dailyCooldownItems: new Dictionary<int, DailyCooldownItem>
                {
                    [46742] = new DailyCooldownItem { ItemId = 46742, PerDayCap = 1 }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.CraftStep, section.Rows[0].RowType);
        }

        [Fact]
        public void CraftStep_NotInSeed_NoNotice()
        {
            var meta = MetaFor((2, "Ordinary Blade", "b.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 2, Quantity = 500, Source = AcquisitionSource.Craft, RecipeId = 10 }
                },
                dailyCooldownItems: new Dictionary<int, DailyCooldownItem>
                {
                    [46742] = new DailyCooldownItem { ItemId = 46742, PerDayCap = 1 }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.CraftStep, section.Rows[0].RowType);
        }

        [Fact]
        public void NullDailyCooldownItems_NoNotice_NoThrow()
        {
            // Module.cs degrades a missing/bad seed file to null - the
            // builder must be a complete no-op in that case, not throw.
            var meta = MetaFor((46742, "Lump of Mithrillium", "lump.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 46742, Quantity = 30, Source = AcquisitionSource.Craft, RecipeId = 7319 }
                },
                dailyCooldownItems: null);

            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Single(section.Rows);
        }

        [Fact]
        public void NonCraftStep_MatchingSeedId_NoNotice()
        {
            // These curated items are all account-bound with no vendor/TP
            // source in real data, but the notice pass itself must only
            // ever look at Craft-source steps - a BuyFromVendor/BuyFromTp
            // step sharing the same item id must never trigger it.
            var meta = MetaFor((46742, "Lump of Mithrillium", "lump.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 46742, Quantity = 30, Source = AcquisitionSource.BuyFromVendor }
                },
                dailyCooldownItems: new Dictionary<int, DailyCooldownItem>
                {
                    [46742] = new DailyCooldownItem { ItemId = 46742, PerDayCap = 1 }
                });

            var vm = _builder.Build(result);

            // No craft steps and no timegated vendor items -> no Crafting
            // Steps section at all (matches NoCraftSteps_NoCraftingSection).
            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.CraftingSteps);
        }

        [Fact]
        public void CraftStep_ExceedsCap_NotDivisibleByCap_RoundsUpDays()
        {
            var meta = MetaFor((43772, "Charged Quartz Crystal", "q.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 43772, Quantity = 5, Source = AcquisitionSource.Craft, RecipeId = 99 }
                },
                dailyCooldownItems: new Dictionary<int, DailyCooldownItem>
                {
                    [43772] = new DailyCooldownItem { ItemId = 43772, PerDayCap = 2 }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            var notice = section.Rows[1];
            // Ceiling(5 / 2) = 3 days, not a truncated 2.
            Assert.Contains("3 days", notice.Label);
        }

        [Fact]
        public void TwoCraftCooldownNotices_BothAppendParallelClause()
        {
            // Follow-up fix (recorded non-blocking): with 2+ daily-gated
            // notices in the plan, each row DOES have another notice to run
            // in parallel with, so the clause must be present on both.
            var meta = MetaFor(
                (46742, "Lump of Mithrillium", "lump.png"),
                (43772, "Charged Quartz Crystal", "q.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 46742, Quantity = 30, Source = AcquisitionSource.Craft, RecipeId = 7319 },
                    new PlanStep { ItemId = 43772, Quantity = 5, Source = AcquisitionSource.Craft, RecipeId = 99 }
                },
                dailyCooldownItems: new Dictionary<int, DailyCooldownItem>
                {
                    [46742] = new DailyCooldownItem { ItemId = 46742, PerDayCap = 1 },
                    [43772] = new DailyCooldownItem { ItemId = 43772, PerDayCap = 2 }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            var notices = section.Rows.Where(r => r.RowType == PlanRowType.TimegatedNotice).ToList();
            Assert.Equal(2, notices.Count);
            Assert.All(notices, n => Assert.Contains("runs in parallel with other daily-gated items", n.Label));
        }

        [Fact]
        public void VendorCapNotice_AndCraftCooldownNotice_BothAppear()
        {
            // The two notice families are independent - a plan can surface
            // both a vendor-cap notice (Plan.TimegatedItems) and a craft-
            // cooldown notice (DailyCooldownItems) at once, one per row.
            var meta = MetaFor(
                (46742, "Lump of Mithrillium", "lump.png"),
                (9, "Obsidian Shard", "shard.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 46742, Quantity = 10, Source = AcquisitionSource.Craft, RecipeId = 7319 }
                },
                timegatedItems: new List<TimegatedItem>
                {
                    new TimegatedItem { ItemId = 9, CapType = TimegatedCapType.Daily, CapValue = 3, NeededCount = 4 }
                },
                dailyCooldownItems: new Dictionary<int, DailyCooldownItem>
                {
                    [46742] = new DailyCooldownItem { ItemId = 46742, PerDayCap = 1 }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Equal(3, section.Rows.Count);
            Assert.Equal(PlanRowType.CraftStep, section.Rows[0].RowType);
            Assert.Equal(PlanRowType.TimegatedNotice, section.Rows[1].RowType);
            Assert.Contains("Obsidian Shard", section.Rows[1].Label);
            Assert.Equal(PlanRowType.TimegatedNotice, section.Rows[2].RowType);
            Assert.Contains("Lump of Mithrillium", section.Rows[2].Label);
        }
    }
}
