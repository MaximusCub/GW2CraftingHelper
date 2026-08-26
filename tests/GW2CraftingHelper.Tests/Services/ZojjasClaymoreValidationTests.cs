using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Structural validation tests over a SYNTHETIC deep Weaponsmith tree
    /// modeled on Zojja's Claymore's shape. Every item/recipe ID is fake
    /// (9001+/9101+, deliberately outside real GW2 data) and the names are
    /// labels only - the real item's tree differs. All data is in-memory;
    /// no HTTP calls.
    ///
    /// Recipe tree (Weaponsmith-only):
    ///   Claymore (9001) - Recipe 9101, WS 500, AutoLearned
    ///   +-- GS Blade (9002) x1 - Recipe 9102, WS 450, AutoLearned
    ///   |   +-- Ori Ingot (9006) x3 - leaf
    ///   |   +-- Deldrimor Steel (9005) x3 - Recipe 9103, WS 450, AutoLearned
    ///   |       +-- Iron Ingot (9007) x1 - leaf
    ///   |       +-- Steel Ingot (9008) x1 - leaf
    ///   +-- GS Hilt (9003) x1 - leaf (no recipe)
    ///   +-- Inscription (9004) x1 - Recipe 9104, WS 500, NOT AutoLearned
    ///   |   +-- Ori Ingot (9006) x5 - leaf (shared with Blade)
    ///   |   +-- Glob of Ectoplasm (9009) x5 - leaf
    ///   +-- Glob of Dark Matter (9010) x1 - leaf
    /// </summary>
    public class ZojjasClaymoreValidationTests
    {
        // Item IDs
        private const int ZojjasClaymore = 9001;
        private const int OriGsBlade = 9002;
        private const int OriGsHilt = 9003;
        private const int Inscription = 9004;
        private const int DeldrimorSteel = 9005;
        private const int OriIngot = 9006;
        private const int IronIngot = 9007;
        private const int SteelIngot = 9008;
        private const int GlobEcto = 9009;
        private const int GlobDarkMatter = 9010;

        // Recipe IDs
        private const int RecipeClaymore = 9101;
        private const int RecipeBlade = 9102;
        private const int RecipeDeldrimor = 9103;
        private const int RecipeInscription = 9104;

        private static readonly int[] AllItemIds =
        {
            ZojjasClaymore, OriGsBlade, OriGsHilt, Inscription,
            DeldrimorSteel, OriIngot, IronIngot, SteelIngot,
            GlobEcto, GlobDarkMatter,
        };

        private static readonly int[] AllRecipeIds =
        {
            RecipeClaymore, RecipeBlade, RecipeDeldrimor, RecipeInscription,
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
                    new RawIngredient { Type = "Item", Id = GlobDarkMatter, Count = 1 },
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 500,
                Flags = new List<string> { "AutoLearned" },
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
                    new RawIngredient { Type = "Item", Id = DeldrimorSteel, Count = 3 },
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 450,
                Flags = new List<string> { "AutoLearned" },
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
                    new RawIngredient { Type = "Item", Id = SteelIngot, Count = 1 },
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 450,
                Flags = new List<string> { "AutoLearned" },
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
                    new RawIngredient { Type = "Item", Id = GlobEcto, Count = 5 },
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 500,
                Flags = new List<string>(),
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

        private static async Task<CraftingPlanResult> GenerateAsync()
        {
            var (pipeline, _) = BuildPipeline();
            return await pipeline.GenerateStructuredAsync(
                ZojjasClaymore, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);
        }

        private static List<PlanStep> CraftSteps(CraftingPlanResult result)
        {
            return result.Plan.Steps
                .Where(s => s.Source == AcquisitionSource.Craft).ToList();
        }

        /// <summary>
        /// Position of each item's Craft step in the plan. Callers assert
        /// that the keys they need are present before reading them - the
        /// ordering checks used to sit inside ContainsKey guards, so a
        /// solver that stopped emitting the root craft step (the exact
        /// regression bottom-up ordering exists to catch) ran zero
        /// assertions and passed.
        /// </summary>
        private static Dictionary<int, int> CraftStepIndex(CraftingPlanResult result)
        {
            var index = new Dictionary<int, int>();
            for (int i = 0; i < result.Plan.Steps.Count; i++)
            {
                var step = result.Plan.Steps[i];
                if (step.Source == AcquisitionSource.Craft)
                {
                    index[step.ItemId] = i;
                }
            }

            return index;
        }

        [Fact]
        public async Task NoSnapshot_TargetIdentityIsWhatWasAskedFor()
        {
            var result = await GenerateAsync();

            Assert.Equal(ZojjasClaymore, result.Plan.TargetItemId);
            Assert.Equal(1, result.Plan.TargetQuantity);
        }

        [Fact]
        public async Task NoSnapshot_PlanHasSteps()
        {
            var result = await GenerateAsync();

            Assert.NotEmpty(result.Plan.Steps);
        }

        [Fact]
        public async Task NoSnapshot_EveryCraftStepCarriesARecipeId()
        {
            var result = await GenerateAsync();
            var craftSteps = CraftSteps(result);

            Assert.NotEmpty(craftSteps);
            Assert.All(craftSteps, s => Assert.True(s.RecipeId > 0,
                $"Craft step for item {s.ItemId} has RecipeId {s.RecipeId}"));
        }

        [Fact]
        public async Task NoSnapshot_EveryStepHasAPositiveQuantity()
        {
            var result = await GenerateAsync();

            Assert.All(result.Plan.Steps, s => Assert.True(s.Quantity > 0,
                $"Step for item {s.ItemId} has Quantity {s.Quantity}"));
        }

        [Fact]
        public async Task NoSnapshot_RequiresWeaponsmithAtTheRootRecipesRating()
        {
            var result = await GenerateAsync();

            var weaponsmith = Assert.Single(result.RequiredDisciplines);
            Assert.Equal("Weaponsmith", weaponsmith.Discipline);
            Assert.Equal(500, weaponsmith.MinRating);
        }

        [Fact]
        public async Task NoSnapshot_RequiresAllFourRecipesInTheTree()
        {
            var result = await GenerateAsync();

            // Was "at least 3, Inscription may or may not appear depending
            // on solver". It does appear, on every run: the Inscription is
            // priced above the sum of its ingredients like every other
            // craftable intermediate here, so the solver crafts it. The set
            // is pinned rather than hedged.
            Assert.Equal(
                new[] { RecipeClaymore, RecipeBlade, RecipeDeldrimor, RecipeInscription },
                result.RequiredRecipes.Select(r => r.RecipeId).OrderBy(id => id));
        }

        [Fact]
        public async Task NoSnapshot_RequiredRecipesIncludeTheRoot()
        {
            var result = await GenerateAsync();

            Assert.Contains(result.RequiredRecipes,
                r => r.OutputItemId == ZojjasClaymore);
        }

        [Fact]
        public async Task NoSnapshot_TheThreeAutoLearnedRecipesAreFlaggedAsSuch()
        {
            var result = await GenerateAsync();

            Assert.Equal(
                new[] { RecipeClaymore, RecipeBlade, RecipeDeldrimor },
                result.RequiredRecipes.Where(r => r.IsAutoLearned)
                    .Select(r => r.RecipeId).OrderBy(id => id));
        }

        [Fact]
        public async Task NoSnapshot_RequiredRecipesHaveNoDuplicates()
        {
            var result = await GenerateAsync();

            var recipeIds = result.RequiredRecipes.Select(r => r.RecipeId).ToList();
            Assert.Equal(recipeIds.Count, recipeIds.Distinct().Count());
        }

        [Fact]
        public async Task NoSnapshot_EveryCraftStepsRecipeIsAlsoARequiredRecipe()
        {
            var result = await GenerateAsync();
            var craftSteps = CraftSteps(result);
            var requiredRecipeIds = new HashSet<int>(
                result.RequiredRecipes.Select(r => r.RecipeId));

            Assert.NotEmpty(craftSteps);
            Assert.All(craftSteps, s => Assert.True(
                requiredRecipeIds.Contains(s.RecipeId),
                $"Craft step recipe {s.RecipeId} not in RequiredRecipes"));
        }

        [Fact]
        public async Task NoSnapshot_EveryRequiredRecipesOutputHasACraftStep()
        {
            var result = await GenerateAsync();
            var craftStepItemIds = new HashSet<int>(CraftSteps(result).Select(s => s.ItemId));

            Assert.NotEmpty(result.RequiredRecipes);
            Assert.All(result.RequiredRecipes, r => Assert.True(
                craftStepItemIds.Contains(r.OutputItemId),
                $"RequiredRecipe {r.RecipeId} output {r.OutputItemId} not in Craft steps"));
        }

        [Fact]
        public async Task NoSnapshot_DebugLogNamesTheRequiredDisciplines()
        {
            var result = await GenerateAsync();

            Assert.NotEmpty(result.DebugLog);
            Assert.Contains(result.DebugLog,
                line => line.Contains("Required disciplines:"));
        }

        [Fact]
        public async Task NoSnapshot_CraftOrderingIsBottomUp()
        {
            var result = await GenerateAsync();
            var craftStepIndex = CraftStepIndex(result);

            // Preconditions, asserted rather than guarded: each of these
            // four craft steps must exist for the ordering below to mean
            // anything, and their absence is itself the regression.
            Assert.True(craftStepIndex.ContainsKey(ZojjasClaymore),
                "solver emitted no craft step for the root");
            Assert.True(craftStepIndex.ContainsKey(OriGsBlade),
                "solver emitted no craft step for the Blade");
            Assert.True(craftStepIndex.ContainsKey(Inscription),
                "solver emitted no craft step for the Inscription");
            Assert.True(craftStepIndex.ContainsKey(DeldrimorSteel),
                "solver emitted no craft step for the Deldrimor Steel");

            int rootIdx = craftStepIndex[ZojjasClaymore];

            Assert.True(craftStepIndex[OriGsBlade] < rootIdx,
                "Blade craft step should come before Claymore craft step");
            Assert.True(craftStepIndex[Inscription] < rootIdx,
                "Inscription craft step should come before Claymore craft step");
            Assert.True(craftStepIndex[DeldrimorSteel] < craftStepIndex[OriGsBlade],
                "Deldrimor Steel craft step should come before Blade craft step");
        }

        [Fact]
        public async Task WithSnapshot_OwnedBlade_PrunesBladeCraftStep()
        {
            var (pipeline, _) = BuildPipeline();

            // Own 1x GS Blade - a Weaponsmith-only craftable
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = OriGsBlade, Count = 1, Source = AccountItemIndex.SourceMaterialStorage },
                },
            };

            var result = await pipeline.GenerateStructuredAsync(
                ZojjasClaymore, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // 14. UsedMaterials reports the Blade consumed
            Assert.Contains(result.UsedMaterials,
                u => u.ItemId == OriGsBlade && u.QuantityUsed > 0);

            // 15. Blade recipe is pruned from RequiredRecipes
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

        [Fact]
        public async Task NoSnapshot_DebugLogContainsTimingEntries()
        {
            var (pipeline, _) = BuildPipeline();
            var result = await pipeline.GenerateStructuredAsync(
                ZojjasClaymore, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.NotNull(result.DebugLog);

            // All 9 pipeline phase prefixes must appear with timing in ms
            // (the dead "Resolve vendor offers" step was removed
            // along with the always-null VendorOfferResolver seam)
            var expectedPrefixes = new[]
            {
                "Build recipe tree",
                "Collect item IDs",
                "Fetch TP prices",
                "Query vendor offers",
                "Inventory reduction",
                "Solve",
                "Fetch item metadata",
                "Fetch learned recipes",
                "Build result",
            };

            var timingPattern = new Regex(@"\d+ms");

            foreach (var prefix in expectedPrefixes)
            {
                var match = result.DebugLog.FirstOrDefault(
                    line => line.StartsWith(prefix) && timingPattern.IsMatch(line));
                Assert.True(match != null,
                    $"DebugLog missing timing entry for phase '{prefix}'. "
                    + $"Entries: [{string.Join(", ", result.DebugLog)}]");
            }

            // Timing summary block must be present
            Assert.Contains(result.DebugLog,
                line => line == "--- Timing Summary ---");
        }
    }
}
