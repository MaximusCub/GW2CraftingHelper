using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Structural validation tests using Zojja's Claymore (46762) as a
    /// representative deep ascended Weaponsmith tree. All data is in-memory;
    /// no HTTP calls.
    ///
    /// Recipe tree (Weaponsmith-only):
    ///   Zojja's Claymore (46762) - Recipe 7836, WS 500, AutoLearned
    ///   +-- Orichalcum GS Blade (46738) x1 - Recipe 11539, WS 450, AutoLearned
    ///   |   +-- Orichalcum Ingot (19685) x3 - leaf
    ///   |   +-- Deldrimor Steel Ingot (46739) x3 - Recipe 11517, WS 450, AutoLearned
    ///   |       +-- Iron Ingot (19683) x1 - leaf
    ///   |       +-- Steel Ingot (19688) x1 - leaf
    ///   +-- Orichalcum GS Hilt (46742) x1 - leaf (no recipe)
    ///   +-- Inscription (46688) x1 - Recipe 11548, WS 500, NOT AutoLearned
    ///   |   +-- Orichalcum Ingot (19685) x5 - leaf (shared with Blade)
    ///   |   +-- Glob of Ectoplasm (19721) x5 - leaf
    ///   +-- Glob of Dark Matter (46746) x1 - leaf
    /// </summary>
    public class ZojjasClaymoreValidationTests
    {
        // Item IDs
        private const int ZojjasClaymore = 46762;
        private const int OriGsBlade = 46738;
        private const int OriGsHilt = 46742;
        private const int Inscription = 46688;
        private const int DeldrimorSteel = 46739;
        private const int OriIngot = 19685;
        private const int IronIngot = 19683;
        private const int SteelIngot = 19688;
        private const int GlobEcto = 19721;
        private const int GlobDarkMatter = 46746;

        // Recipe IDs
        private const int RecipeClaymore = 7836;
        private const int RecipeBlade = 11539;
        private const int RecipeDeldrimor = 11517;
        private const int RecipeInscription = 11548;

        private static readonly int[] AllItemIds =
        {
            ZojjasClaymore, OriGsBlade, OriGsHilt, Inscription,
            DeldrimorSteel, OriIngot, IronIngot, SteelIngot,
            GlobEcto, GlobDarkMatter
        };

        private static readonly int[] AllRecipeIds =
        {
            RecipeClaymore, RecipeBlade, RecipeDeldrimor, RecipeInscription
        };

        private static (CraftingPlanPipeline pipeline, InMemoryAccountRecipeClient accountRecipes)
            BuildPipeline()
        {
            var recipeApi = new InMemoryRecipeApiClient();

            // Zojja's Claymore recipe
            recipeApi.AddSearchResult(ZojjasClaymore, RecipeClaymore);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = RecipeClaymore,
                OutputItemId = ZojjasClaymore,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = OriGsBlade, Count = 1 },
                    new RawIngredient { Type = "Item", Id = OriGsHilt, Count = 1 },
                    new RawIngredient { Type = "Item", Id = Inscription, Count = 1 },
                    new RawIngredient { Type = "Item", Id = GlobDarkMatter, Count = 1 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 500,
                Flags = new List<string> { "AutoLearned" }
            });

            // Orichalcum GS Blade recipe
            recipeApi.AddSearchResult(OriGsBlade, RecipeBlade);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = RecipeBlade,
                OutputItemId = OriGsBlade,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = OriIngot, Count = 3 },
                    new RawIngredient { Type = "Item", Id = DeldrimorSteel, Count = 3 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 450,
                Flags = new List<string> { "AutoLearned" }
            });

            // Deldrimor Steel Ingot recipe
            recipeApi.AddSearchResult(DeldrimorSteel, RecipeDeldrimor);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = RecipeDeldrimor,
                OutputItemId = DeldrimorSteel,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = IronIngot, Count = 1 },
                    new RawIngredient { Type = "Item", Id = SteelIngot, Count = 1 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 450,
                Flags = new List<string> { "AutoLearned" }
            });

            // Inscription recipe (NOT AutoLearned)
            recipeApi.AddSearchResult(Inscription, RecipeInscription);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = RecipeInscription,
                OutputItemId = Inscription,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = OriIngot, Count = 5 },
                    new RawIngredient { Type = "Item", Id = GlobEcto, Count = 5 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 500,
                Flags = new List<string>()
            });

            // Leaf items: no recipes
            // (InMemoryRecipeApiClient returns empty list for unknown item IDs)

            // Pricing: craftable intermediates are expensive on TP so solver
            // prefers crafting; leaf materials are cheap so buying is preferred.
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(ZojjasClaymore, buyUnitPrice: 500000, sellUnitPrice: 600000);
            priceApi.AddPrice(OriGsBlade, buyUnitPrice: 100000, sellUnitPrice: 200000);
            priceApi.AddPrice(Inscription, buyUnitPrice: 100000, sellUnitPrice: 200000);
            priceApi.AddPrice(DeldrimorSteel, buyUnitPrice: 50000, sellUnitPrice: 100000);
            // Leaves: cheap to buy
            priceApi.AddPrice(OriGsHilt, buyUnitPrice: 100, sellUnitPrice: 200);
            priceApi.AddPrice(OriIngot, buyUnitPrice: 100, sellUnitPrice: 200);
            priceApi.AddPrice(IronIngot, buyUnitPrice: 10, sellUnitPrice: 20);
            priceApi.AddPrice(SteelIngot, buyUnitPrice: 10, sellUnitPrice: 20);
            priceApi.AddPrice(GlobEcto, buyUnitPrice: 200, sellUnitPrice: 400);
            priceApi.AddPrice(GlobDarkMatter, buyUnitPrice: 500, sellUnitPrice: 1000);

            // Item metadata
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(ZojjasClaymore, "Zojja's Claymore", "icon.png");
            itemApi.AddItem(OriGsBlade, "Orichalcum GS Blade", "icon.png");
            itemApi.AddItem(OriGsHilt, "Orichalcum GS Hilt", "icon.png");
            itemApi.AddItem(Inscription, "Inscription", "icon.png");
            itemApi.AddItem(DeldrimorSteel, "Deldrimor Steel Ingot", "icon.png");
            itemApi.AddItem(OriIngot, "Orichalcum Ingot", "icon.png");
            itemApi.AddItem(IronIngot, "Iron Ingot", "icon.png");
            itemApi.AddItem(SteelIngot, "Steel Ingot", "icon.png");
            itemApi.AddItem(GlobEcto, "Glob of Ectoplasm", "icon.png");
            itemApi.AddItem(GlobDarkMatter, "Glob of Dark Matter", "icon.png");

            // Account recipe client
            var accountRecipes = new InMemoryAccountRecipeClient();
            accountRecipes.SetHasPermission(true);
            foreach (int id in AllRecipeIds)
            {
                accountRecipes.AddLearnedRecipe(id);
            }

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer(),
                accountRecipeClient: accountRecipes);

            return (pipeline, accountRecipes);
        }

        [Fact]
        public async Task NoSnapshot_AllStructuralAssertions()
        {
            var (pipeline, _) = BuildPipeline();
            var result = await pipeline.GenerateStructuredAsync(
                ZojjasClaymore, 1, null, CancellationToken.None);

            // 1. Target identity
            Assert.Equal(ZojjasClaymore, result.Plan.TargetItemId);
            Assert.Equal(1, result.Plan.TargetQuantity);

            // 2. Has plan steps
            Assert.True(result.Plan.Steps.Count > 0);

            // 3. Every Craft step has a RecipeId > 0
            var craftSteps = result.Plan.Steps
                .Where(s => s.Source == AcquisitionSource.Craft).ToList();
            Assert.All(craftSteps, s => Assert.True(s.RecipeId > 0,
                $"Craft step for item {s.ItemId} has RecipeId {s.RecipeId}"));

            // 4. Every step has Quantity > 0
            Assert.All(result.Plan.Steps, s => Assert.True(s.Quantity > 0,
                $"Step for item {s.ItemId} has Quantity {s.Quantity}"));

            // 5. RequiredDisciplines contains Weaponsmith with MinRating >= 500
            Assert.Contains(result.RequiredDisciplines,
                d => d.Discipline == "Weaponsmith" && d.MinRating >= 500);

            // 6. At least 3 RequiredRecipes (Claymore, Blade, Deldrimor;
            //    Inscription may or may not appear depending on solver)
            Assert.True(result.RequiredRecipes.Count >= 3,
                $"Expected >= 3 RequiredRecipes, got {result.RequiredRecipes.Count}");

            // 7. RequiredRecipes contains the root recipe
            Assert.Contains(result.RequiredRecipes,
                r => r.OutputItemId == ZojjasClaymore);

            // 8. At least one RequiredRecipe is AutoLearned
            Assert.Contains(result.RequiredRecipes, r => r.IsAutoLearned);

            // 9. No duplicate RecipeIds in RequiredRecipes
            var recipeIds = result.RequiredRecipes.Select(r => r.RecipeId).ToList();
            Assert.Equal(recipeIds.Count, recipeIds.Distinct().Count());

            // 10. Every Craft step's RecipeId appears in RequiredRecipes
            var requiredRecipeIds = new HashSet<int>(
                result.RequiredRecipes.Select(r => r.RecipeId));
            Assert.All(craftSteps, s => Assert.True(
                requiredRecipeIds.Contains(s.RecipeId),
                $"Craft step recipe {s.RecipeId} not in RequiredRecipes"));

            // 11. Every RequiredRecipe's OutputItemId appears in some Craft step
            var craftStepItemIds = new HashSet<int>(craftSteps.Select(s => s.ItemId));
            Assert.All(result.RequiredRecipes, r => Assert.True(
                craftStepItemIds.Contains(r.OutputItemId),
                $"RequiredRecipe {r.RecipeId} output {r.OutputItemId} not in Craft steps"));

            // 12. DebugLog contains "Required disciplines:"
            Assert.True(result.DebugLog.Count > 0);
            Assert.Contains(result.DebugLog,
                line => line.Contains("Required disciplines:"));

            // 13. Bottom-up craft ordering: each ingredient's Craft step
            //     appears before the consumer's Craft step
            var craftStepIndex = new Dictionary<int, int>();
            for (int i = 0; i < result.Plan.Steps.Count; i++)
            {
                var step = result.Plan.Steps[i];
                if (step.Source == AcquisitionSource.Craft)
                {
                    craftStepIndex[step.ItemId] = i;
                }
            }

            // The root (ZojjasClaymore) must come after its craft-step ingredients
            if (craftStepIndex.ContainsKey(ZojjasClaymore))
            {
                int rootIdx = craftStepIndex[ZojjasClaymore];
                // Blade is a crafted ingredient of Claymore
                if (craftStepIndex.ContainsKey(OriGsBlade))
                {
                    Assert.True(craftStepIndex[OriGsBlade] < rootIdx,
                        "Blade craft step should come before Claymore craft step");
                }
                // Inscription is a crafted ingredient of Claymore
                if (craftStepIndex.ContainsKey(Inscription))
                {
                    Assert.True(craftStepIndex[Inscription] < rootIdx,
                        "Inscription craft step should come before Claymore craft step");
                }
            }

            // Deldrimor Steel is a crafted ingredient of Blade
            if (craftStepIndex.ContainsKey(OriGsBlade)
                && craftStepIndex.ContainsKey(DeldrimorSteel))
            {
                Assert.True(
                    craftStepIndex[DeldrimorSteel] < craftStepIndex[OriGsBlade],
                    "Deldrimor Steel craft step should come before Blade craft step");
            }
        }

        [Fact]
        public async Task WithSnapshot_OwnedBlade_PrunesBladeCraftStep()
        {
            var (pipeline, _) = BuildPipeline();

            // Own 1x Orichalcum GS Blade (46738) — a Weaponsmith-only craftable
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = OriGsBlade, Count = 1 }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(
                ZojjasClaymore, 1, snapshot, CancellationToken.None);

            // 14. UsedMaterials reports the Blade consumed
            Assert.Contains(result.UsedMaterials,
                u => u.ItemId == OriGsBlade && u.QuantityUsed > 0);

            // 15. Blade recipe (11539) is pruned from RequiredRecipes
            Assert.DoesNotContain(result.RequiredRecipes,
                r => r.RecipeId == RecipeBlade);

            // 16. No Craft step for the Blade
            Assert.DoesNotContain(result.Plan.Steps,
                s => s.ItemId == OriGsBlade && s.Source == AcquisitionSource.Craft);

            // 17. Deldrimor Steel recipe is also pruned (sub-ingredient of Blade)
            Assert.DoesNotContain(result.RequiredRecipes,
                r => r.RecipeId == RecipeDeldrimor);

            // 18. Claymore root recipe still present
            Assert.Contains(result.RequiredRecipes,
                r => r.RecipeId == RecipeClaymore);

            // 19. Inscription recipe still present
            Assert.Contains(result.RequiredRecipes,
                r => r.RecipeId == RecipeInscription);

            // 20. Weaponsmith discipline still required (root + Inscription)
            Assert.Contains(result.RequiredDisciplines,
                d => d.Discipline == "Weaponsmith");
        }
    }
}
