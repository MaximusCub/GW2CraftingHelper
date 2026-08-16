using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanViewModelBuilderStepSectionsTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        // --- Crafting Steps ---

        [Fact]
        public void CraftingSteps_OnlyCraftSource()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp },
                new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                new PlanStep { ItemId = 3, Quantity = 2, Source = AcquisitionSource.BuyFromVendor }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.CraftStep, section.Rows[0].RowType);
        }

        [Fact]
        public void CraftingSteps_PreservesOrder()
        {
            var meta = MetaFor((2, "Blade", "blade.png"), (3, "Hilt", "hilt.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                    new PlanStep { ItemId = 3, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 20 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Equal(2, section.Rows.Count);
            Assert.Equal("Blade", section.Rows[0].Label);
            Assert.Equal("Hilt", section.Rows[1].Label);
        }

        [Fact]
        public void NoCraftSteps_NoCraftingSection()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 5, Source = AcquisitionSource.BuyFromTp }
            });
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.CraftingSteps);
        }

        [Fact]
        public void TimegatedItems_AppendedAsNoticeRowsInCraftingSteps()
        {
            // M34-B1 #3: a timegated (vendor purchase cap) notice renders as
            // a plain informational row alongside real craft steps, never
            // altering the numbered CraftStep rows themselves.
            var meta = MetaFor((2, "Blade", "blade.png"), (9, "Obsidian Shard", "shard.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 }
                },
                timegatedItems: new List<TimegatedItem>
                {
                    new TimegatedItem { ItemId = 9, CapType = TimegatedCapType.Daily, CapValue = 3, NeededCount = 4 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Equal(2, section.Rows.Count);
            Assert.Equal(PlanRowType.CraftStep, section.Rows[0].RowType);
            Assert.Equal(PlanRowType.TimegatedNotice, section.Rows[1].RowType);
            Assert.Contains("Obsidian Shard", section.Rows[1].Label);
            Assert.Contains("Daily", section.Rows[1].Label);
            Assert.Contains("3", section.Rows[1].Label);
            Assert.Contains("4", section.Rows[1].Label);
        }

        [Fact]
        public void TimegatedItems_NoCraftSteps_StillCreatesCraftingSection()
        {
            // A plan with zero real craft steps but a timegated vendor buy
            // must still surface the notice - the section is no longer
            // gated purely on craftSteps.Count.
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 9, Quantity = 4, Source = AcquisitionSource.BuyFromVendor }
                },
                timegatedItems: new List<TimegatedItem>
                {
                    new TimegatedItem { ItemId = 9, CapType = TimegatedCapType.Weekly, CapValue = 3, NeededCount = 4 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.TimegatedNotice, section.Rows[0].RowType);
        }

        [Fact]
        public void TimegatedItems_SeasonalCapType_RendersSeasonWording()
        {
            // Astral Acclaim package (KNOWN-ISSUES #33): Seasonal renders
            // with the noun "Season" (matching gw2e's own Wizard's Vault
            // wording), keeping the same "{CapLabel} limit: N (plan needs
            // M)" shape Daily/Weekly already use.
            var meta = MetaFor((9, "Obsidian Shard", "shard.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 9, Quantity = 60, Source = AcquisitionSource.BuyFromVendor }
                },
                timegatedItems: new List<TimegatedItem>
                {
                    new TimegatedItem { ItemId = 9, CapType = TimegatedCapType.Seasonal, CapValue = 20, NeededCount = 60 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            var notice = Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.TimegatedNotice, notice.RowType);
            Assert.Contains("Obsidian Shard", notice.Label);
            Assert.Contains("Season limit: 20", notice.Label);
            Assert.Contains("plan needs 60", notice.Label);
            Assert.DoesNotContain("Seasonal", notice.Label);
        }

        // --- Required Disciplines ---

        [Fact]
        public void RequiredDisciplines_MapsCorrectly()
        {
            var result = MakeResult(requiredDisciplines: new List<RequiredDiscipline>
            {
                new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredDisciplines);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.DisciplineRow, section.Rows[0].RowType);
            Assert.Equal("Weaponsmith", section.Rows[0].Label);
            Assert.Equal("Level 500", section.Rows[0].Sublabel);
        }

        [Fact]
        public void RequiredDisciplines_Empty_NoSection()
        {
            var result = MakeResult(requiredDisciplines: new List<RequiredDiscipline>());
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.RequiredDisciplines);
        }

        // --- W3C (per-character discipline display, gw2efficiency
        // parity): character availability text on DisciplineRow rows. ---

        [Fact]
        public void RequiredDisciplines_AllCharactersSufficient_ListsEachWithRating()
        {
            var result = MakeResult(
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 400 }
                },
                characterDisciplines: new List<SnapshotCharacterDiscipline>
                {
                    new SnapshotCharacterDiscipline { CharacterName = "Bob", Discipline = "Weaponsmith", Rating = 400, Active = true },
                    new SnapshotCharacterDiscipline { CharacterName = "Anna", Discipline = "Weaponsmith", Rating = 500, Active = true }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredDisciplines);
            // Highest rating first, no "/min" suffix - both meet the
            // required 400.
            Assert.Equal("Anna (500), Bob (400)", section.Rows[0].CharacterAvailabilityText);
        }

        [Fact]
        public void RequiredDisciplines_CharacterBelowRequiredRating_ShowsSlashMinConvention()
        {
            var result = MakeResult(
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 450 }
                },
                characterDisciplines: new List<SnapshotCharacterDiscipline>
                {
                    new SnapshotCharacterDiscipline { CharacterName = "Bob", Discipline = "Weaponsmith", Rating = 400, Active = true }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredDisciplines);
            Assert.Equal("Bob (400/450)", section.Rows[0].CharacterAvailabilityText);
        }

        [Fact]
        public void RequiredDisciplines_NoCharacterHasIt_SaysNotTrainedPlainly()
        {
            var result = MakeResult(
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 400 }
                },
                characterDisciplines: new List<SnapshotCharacterDiscipline>
                {
                    new SnapshotCharacterDiscipline { CharacterName = "Bob", Discipline = "Chef", Rating = 400, Active = true }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredDisciplines);
            Assert.Equal("Not trained on any character", section.Rows[0].CharacterAvailabilityText);
        }

        [Fact]
        public void RequiredDisciplines_NoSnapshotCharacterData_SublabelTextOmitted()
        {
            // characterDisciplines left null (default) - the snapshot never
            // captured this data at all (old snapshot / degraded fetch).
            // Must show nothing extra, never a fabricated "not trained"
            // claim.
            var result = MakeResult(requiredDisciplines: new List<RequiredDiscipline>
            {
                new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 400 }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredDisciplines);
            Assert.Null(section.Rows[0].CharacterAvailabilityText);
        }

        // --- Required Recipes ---

        [Fact]
        public void RequiredRecipes_AutoLearned_StatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = true,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = null
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Auto-learned", section.Rows[0].StatusTag);
        }

        [Fact]
        public void RequiredRecipes_Missing_StatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = true
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Missing!", section.Rows[0].StatusTag);
        }

        [Fact]
        public void RequiredRecipes_Learned_StatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = false
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Learned", section.Rows[0].StatusTag);
        }

        [Fact]
        public void RequiredRecipes_NullMissing_EmptyStatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = null
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("", section.Rows[0].StatusTag);
        }

        // --- UI-bundle milestone, Feature A (wiki links) ---

        [Fact]
        public void RequiredRecipes_Missing_NotLearnedFromItem_WikiUrlLinksToAcquisitionAnchor()
        {
            var meta = MetaFor((1, "Bolt of Damask", "bolt.png"));
            var result = MakeResult(metadata: meta, requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    IsLearnedFromItem = false,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = true
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal(
                "https://wiki.guildwars2.com/wiki/Bolt_of_Damask#Acquisition",
                section.Rows[0].WikiUrl);
        }

        [Fact]
        public void RequiredRecipes_Missing_LearnedFromItem_WikiUrlLinksToRecipeSheet()
        {
            var meta = MetaFor((1, "Bolt of Damask", "bolt.png"));
            var result = MakeResult(metadata: meta, requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    IsLearnedFromItem = true,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = true
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal(
                "https://wiki.guildwars2.com/wiki/Recipe:_Bolt_of_Damask",
                section.Rows[0].WikiUrl);
        }

        [Fact]
        public void RequiredRecipes_Learned_NoWikiUrl()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = false
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Null(section.Rows[0].WikiUrl);
        }

        [Fact]
        public void RequiredRecipes_AutoLearned_NoWikiUrl()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = true,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    // Verification-fix: true, not null - the real-data shape
                    // (an AutoLearned recipe below the character rating is
                    // IsAutoLearned AND IsMissing) is the axis this test must
                    // pin; null passed vacuously under any gate.
                    IsMissing = true
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Null(section.Rows[0].WikiUrl);
        }

        [Fact]
        public void RequiredRecipes_OutputName_FromMetadata()
        {
            var meta = MetaFor((5, "Cool Blade", "blade.png"));
            var result = MakeResult(
                metadata: meta,
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = 10,
                        OutputItemId = 5,
                        IsAutoLearned = true,
                        Disciplines = new List<string> { "Weaponsmith" },
                        MinRating = 400
                    }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Cool Blade", section.Rows[0].Label);
            Assert.Equal("blade.png", section.Rows[0].IconUrl);
        }

        // --- Wave-3 quick win #2: Mystic Forge rows excluded from the
        // Required Recipes SECTION (nothing to learn) ---

        [Fact]
        public void RequiredRecipes_MysticForgeOnly_SectionOmittedEntirely()
        {
            // A plan whose only "recipe" is a sole Mystic Forge combination
            // has nothing to learn at all - the section itself is dropped,
            // not left present with a "(0)" header and no rows.
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = -100,
                    OutputItemId = 1,
                    IsAutoLearned = true,
                    Disciplines = new List<string> { "MysticForge" },
                    MinRating = 0,
                    IsMissing = false
                }
            });
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.RequiredRecipes);
        }

        [Fact]
        public void RequiredRecipes_MixedMysticForgeAndReal_OnlyRealRecipeShownAndCounted()
        {
            // One sole-Mystic-Forge recipe alongside one real-discipline
            // recipe: only the real one survives, and the header count
            // reflects that post-filter total, not the raw
            // result.RequiredRecipes.Count of 2.
            var meta = MetaFor((1, "Forge Trinket", "f.png"), (2, "Blade", "b.png"));
            var result = MakeResult(
                metadata: meta,
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = -100,
                        OutputItemId = 1,
                        IsAutoLearned = true,
                        Disciplines = new List<string> { "MysticForge" },
                        MinRating = 0,
                        IsMissing = false
                    },
                    new RequiredRecipe
                    {
                        RecipeId = 10,
                        OutputItemId = 2,
                        IsAutoLearned = false,
                        Disciplines = new List<string> { "Weaponsmith" },
                        MinRating = 400,
                        IsMissing = true
                    }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Single(section.Rows);
            Assert.Equal("Blade", section.Rows[0].Label);
            Assert.Equal("Required Recipes (1)", section.Title);
        }

        [Fact]
        public void RequiredRecipes_MysticForgeWithRealDiscipline_StaysInSection()
        {
            // A recipe combining MysticForge with a genuine leveled
            // discipline still has something real to learn, so it is NOT
            // filtered - only a recipe whose ENTIRE Disciplines list is
            // MysticForge is excluded.
            var meta = MetaFor((2, "Blade", "b.png"));
            var result = MakeResult(
                metadata: meta,
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = 10,
                        OutputItemId = 2,
                        IsAutoLearned = false,
                        Disciplines = new List<string> { "MysticForge", "Weaponsmith" },
                        MinRating = 400,
                        IsMissing = true
                    }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Single(section.Rows);
            Assert.Equal("Blade", section.Rows[0].Label);
            Assert.Equal("Required Recipes (1)", section.Title);
        }

        // --- Section order ---

        [Fact]
        public void SectionOrder_MatchesSpec()
        {
            var meta = MetaFor(
                (1, "Target", "t.png"),
                (2, "Blade", "b.png"),
                (3, "Ore", "o.png"),
                (10, "Used", "u.png"));
            var result = MakeResult(
                targetItemId: 1,
                metadata: meta,
                usedMaterials: new List<UsedMaterial>
                {
                    new UsedMaterial { ItemId = 10, QuantityUsed = 1 }
                },
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 3, Quantity = 5, Source = AcquisitionSource.BuyFromTp, TotalCost = 500 },
                    new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 20 }
                },
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 }
                },
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = 20,
                        OutputItemId = 2,
                        IsAutoLearned = true,
                        Disciplines = new List<string> { "Weaponsmith" },
                        MinRating = 500
                    }
                });
            var vm = _builder.Build(result);

            var types = vm.Sections.Select(s => s.SectionType).ToList();
            Assert.Equal(new[]
            {
                PlanSectionType.Summary,
                PlanSectionType.UsedMaterials,
                PlanSectionType.ShoppingList,
                PlanSectionType.RequiredDisciplines,
                PlanSectionType.RequiredRecipes,
                PlanSectionType.CraftingSteps
            }, types);
        }

        // --- Mixed steps ---

        [Fact]
        public void MixedSteps_CorrectSectionAssignment()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 },
                new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                new PlanStep { ItemId = 3, Quantity = 2, Source = AcquisitionSource.BuyFromVendor, TotalCost = 200 }
            });
            var vm = _builder.Build(result);

            var shopping = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Equal(2, shopping.Rows.Count);
            Assert.Contains(shopping.Rows, r => r.RowType == PlanRowType.ShoppingBuy);
            Assert.Contains(shopping.Rows, r => r.RowType == PlanRowType.ShoppingVendor);

            var crafting = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Single(crafting.Rows);
            Assert.Equal(PlanRowType.CraftStep, crafting.Rows[0].RowType);
        }

        // --- Target quantity ---

        [Fact]
        public void TargetQuantity_PassedThrough()
        {
            var result = MakeResult(targetQuantity: 5);
            var vm = _builder.Build(result);

            Assert.Equal(5, vm.TargetQuantity);
        }
    }
}
