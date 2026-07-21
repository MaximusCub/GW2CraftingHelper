using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class RecipeServiceTests
    {
        [Fact]
        public async Task LeafNode_NoRecipe_ReturnsLeafWithQuantity()
        {
            var api = new InMemoryRecipeApiClient();
            var svc = new RecipeService(api);

            var node = await svc.BuildTreeAsync(100, 5, CancellationToken.None);

            Assert.Equal(100, node.Id);
            Assert.Equal("Item", node.IngredientType);
            Assert.Equal(5, node.Quantity);
            Assert.True(node.IsLeaf);
            Assert.Empty(node.Recipes);
        }

        [Fact]
        public async Task SingleLevelRecipe_IngredientsAreLeaves()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 },
                    new RawIngredient { Type = "Item", Id = 3, Count = 1 }
                }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            Assert.False(node.IsLeaf);
            Assert.Single(node.Recipes);

            var option = node.Recipes[0];
            Assert.Equal(10, option.RecipeId);
            Assert.Equal(1, option.OutputCount);
            Assert.Equal(1, option.CraftsNeeded);
            Assert.Equal(2, option.Ingredients.Count);

            Assert.Equal(2, option.Ingredients[0].Id);
            Assert.Equal(3, option.Ingredients[0].Quantity);
            Assert.True(option.Ingredients[0].IsLeaf);

            Assert.Equal(3, option.Ingredients[1].Id);
            Assert.Equal(1, option.Ingredients[1].Quantity);
            Assert.True(option.Ingredients[1].IsLeaf);
        }

        [Fact]
        public async Task MultiLevelChain_ThreeLevelsDeep()
        {
            var api = new InMemoryRecipeApiClient();

            // A (item 1) -> recipe 10 -> ingredient B (item 2)
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                }
            });

            // B (item 2) -> recipe 20 -> ingredient C (item 3, leaf)
            api.AddSearchResult(2, 20);
            api.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            // Level 1: A
            Assert.False(node.IsLeaf);
            var bNode = node.Recipes[0].Ingredients[0];

            // Level 2: B
            Assert.Equal(2, bNode.Id);
            Assert.False(bNode.IsLeaf);
            var cNode = bNode.Recipes[0].Ingredients[0];

            // Level 3: C (leaf)
            Assert.Equal(3, cNode.Id);
            Assert.Equal(2, cNode.Quantity);
            Assert.True(cNode.IsLeaf);
        }

        [Fact]
        public async Task QuantityPropagation_CeilDivision()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 2,  // makes 2 per craft
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 4 }
                }
            });

            var svc = new RecipeService(api);
            // Need 3, recipe makes 2 -> ceil(3/2) = 2 crafts
            var node = await svc.BuildTreeAsync(1, 3, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.Equal(2, option.CraftsNeeded);
            // 2 crafts * 4 per craft = 8
            Assert.Equal(8, option.Ingredients[0].Quantity);
        }

        [Fact]
        public async Task MultipleRecipes_BothPresent()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10, 11);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                }
            });
            api.AddRecipe(new RawRecipe
            {
                Id = 11,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            Assert.Equal(2, node.Recipes.Count);
            Assert.Equal(10, node.Recipes[0].RecipeId);
            Assert.Equal(11, node.Recipes[1].RecipeId);
        }

        [Fact]
        public async Task CurrencyIngredient_IsLeaf()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 },
                    new RawIngredient { Type = "Currency", Id = 99, Count = 50 }
                }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            var currencyNode = node.Recipes[0].Ingredients[1];
            Assert.Equal(99, currencyNode.Id);
            Assert.Equal("Currency", currencyNode.IngredientType);
            Assert.Equal(50, currencyNode.Quantity);
            Assert.True(currencyNode.IsLeaf);
        }

        [Fact]
        public async Task SelfReferentialIngredient_BecomesLeaf_QuantityDoesNotCompound()
        {
            // M33 item 4 (m5 Finding 2 / r2 report): a real, wiki-verified
            // Mystic Forge "trophy tier-up" recipe shape - N of the tier
            // below + 1 of ITS OWN output + junk items -> a few of itself
            // (Obsidian Shard, id 19925, is the exact real example already
            // in ref/recipes_seed.json). Echoes gw2e's recipe-nesting
            // "the component is the recipe! Abort!" rule: the self-ingredient
            // becomes an inert leaf (no further recipe expansion), so its
            // quantity is a single scale-up from THIS craft only - it must
            // NOT recurse into its own recipe again and compound.
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(19925, -496);
            api.AddRecipe(new RawRecipe
            {
                Id = -496,
                OutputItemId = 19925,
                OutputItemCount = 3,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 19925, Count = 1 }, // self
                    new RawIngredient { Type = "Item", Id = 19976, Count = 1 }, // Mystic Coin
                    new RawIngredient { Type = "Item", Id = 24335, Count = 1 },
                    new RawIngredient { Type = "Item", Id = 39090, Count = 1 }
                }
            });

            var svc = new RecipeService(api);
            // Need 300 Obsidian Shards; recipe makes 3 -> ceil(300/3) = 100 crafts.
            var node = await svc.BuildTreeAsync(19925, 300, CancellationToken.None);

            Assert.False(node.IsLeaf);
            var option = node.Recipes[0];
            Assert.Equal(100, option.CraftsNeeded);

            var selfIngredient = option.Ingredients.Single(i => i.Id == 19925);
            // One-time scale-up (100 crafts * 1 per craft) - a sane,
            // wiki-scale number, NOT a recursively-compounded explosion.
            Assert.Equal(100, selfIngredient.Quantity);
            Assert.True(selfIngredient.IsLeaf, "the self-referential ingredient must not re-expand its own recipe");
            Assert.Empty(selfIngredient.Recipes);

            var coinIngredient = option.Ingredients.Single(i => i.Id == 19976);
            Assert.Equal(100, coinIngredient.Quantity);
        }

        [Fact]
        public async Task SelfReferentialMultiTierChain_SaneWikiScaleQuantities()
        {
            // Realistic 4-tier salvage-trophy chain (Small/Claw/Sharp/Large
            // Claw shape from the real seed, ref/recipes_seed.json ids
            // -592..-595): each tier needs 50 of the tier below + 1 of ITS
            // OWN output + dust + Philosopher's Stones -> 7 of itself.
            // Verified (m5's "explosion to millions" is real wiki-scale
            // math for this brutal ratio, not a compounding bug - see the
            // M33 structured-output concerns for the full trace): the
            // demand must grow by a bounded, deterministic multiplier per
            // tier (not runaway/unbounded), and every self-ingredient at
            // every tier must stay an inert, non-recursing leaf.
            var api = new InMemoryRecipeApiClient();
            const int tinyClaw = 90101, smallClaw = 90102, claw = 90103, sharpClaw = 90104, largeClaw = 90105;
            const int dustA = 90201, dustB = 90202, dustC = 90203, dustD = 90204, philStone = 90300;

            api.AddSearchResult(smallClaw, -1592);
            api.AddRecipe(new RawRecipe
            {
                Id = -1592,
                OutputItemId = smallClaw,
                OutputItemCount = 7,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = tinyClaw, Count = 50 },
                    new RawIngredient { Type = "Item", Id = smallClaw, Count = 1 },
                    new RawIngredient { Type = "Item", Id = dustA, Count = 5 },
                    new RawIngredient { Type = "Item", Id = philStone, Count = 1 }
                }
            });
            api.AddSearchResult(claw, -1593);
            api.AddRecipe(new RawRecipe
            {
                Id = -1593,
                OutputItemId = claw,
                OutputItemCount = 7,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = smallClaw, Count = 50 },
                    new RawIngredient { Type = "Item", Id = claw, Count = 1 },
                    new RawIngredient { Type = "Item", Id = dustB, Count = 5 },
                    new RawIngredient { Type = "Item", Id = philStone, Count = 2 }
                }
            });
            api.AddSearchResult(sharpClaw, -1594);
            api.AddRecipe(new RawRecipe
            {
                Id = -1594,
                OutputItemId = sharpClaw,
                OutputItemCount = 7,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = claw, Count = 50 },
                    new RawIngredient { Type = "Item", Id = sharpClaw, Count = 1 },
                    new RawIngredient { Type = "Item", Id = dustC, Count = 5 },
                    new RawIngredient { Type = "Item", Id = philStone, Count = 3 }
                }
            });
            api.AddSearchResult(largeClaw, -1595);
            api.AddRecipe(new RawRecipe
            {
                Id = -1595,
                OutputItemId = largeClaw,
                OutputItemCount = 7,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = sharpClaw, Count = 50 },
                    new RawIngredient { Type = "Item", Id = largeClaw, Count = 1 },
                    new RawIngredient { Type = "Item", Id = dustD, Count = 5 },
                    new RawIngredient { Type = "Item", Id = philStone, Count = 4 }
                }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(largeClaw, 500, CancellationToken.None);

            int LargeCraftsNeeded = node.Recipes[0].CraftsNeeded;
            Assert.Equal(72, LargeCraftsNeeded); // ceil(500/7)

            var sharpNode = node.Recipes[0].Ingredients.Single(i => i.Id == sharpClaw);
            Assert.Equal(72 * 50, sharpNode.Quantity); // 3600 - one-time scale-up, sane
            Assert.False(sharpNode.IsLeaf);

            var selfLarge = node.Recipes[0].Ingredients.Single(i => i.Id == largeClaw);
            Assert.Equal(72, selfLarge.Quantity);
            Assert.True(selfLarge.IsLeaf);

            var sharpOption = sharpNode.Recipes[0];
            var selfSharp = sharpOption.Ingredients.Single(i => i.Id == sharpClaw);
            Assert.True(selfSharp.IsLeaf);
            Assert.Equal(sharpOption.CraftsNeeded, selfSharp.Quantity);

            // Every level's demand is a deterministic, bounded multiple of
            // its parent's need (real 50-per-craft/7-out ratio compounding
            // across genuinely distinct tiers) - not an unbounded/incorrect
            // blow-up. The whole chain resolves without infinite recursion.
            var tinyNode = sharpNode.Recipes[0]
                .Ingredients.Single(i => i.Id == claw).Recipes[0]
                .Ingredients.Single(i => i.Id == smallClaw).Recipes[0]
                .Ingredients.Single(i => i.Id == tinyClaw);
            Assert.True(tinyNode.IsLeaf); // no recipe seeded for Tiny Claw
            Assert.True(tinyNode.Quantity > 0);
        }

        [Fact]
        public async Task RawRecipe_ExpectedOutputCountSet_PropagatesToRecipeOption()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(19675, -1);
            api.AddRecipe(new RawRecipe
            {
                Id = -1,
                OutputItemId = 19675,
                OutputItemCount = 1,
                ExpectedOutputCount = 0.31,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(19675, 1, CancellationToken.None);

            Assert.Equal(0.31, node.Recipes[0].ExpectedOutputCount);
        }

        [Fact]
        public async Task RawRecipe_NoExpectedOutputCount_DefaultsToOutputCount()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 5,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                }
                // ExpectedOutputCount left null (the common case)
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            Assert.Equal(5.0, node.Recipes[0].ExpectedOutputCount);
        }

        [Fact]
        public async Task OutputCountGreaterThanOne_CraftsNeededRoundsUp()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 5,  // makes 5 per craft
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 }
                }
            });

            var svc = new RecipeService(api);
            // Need 7, recipe makes 5 -> ceil(7/5) = 2 crafts
            var node = await svc.BuildTreeAsync(1, 7, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.Equal(5, option.OutputCount);
            Assert.Equal(2, option.CraftsNeeded);
            // 2 crafts * 3 per craft = 6
            Assert.Equal(6, option.Ingredients[0].Quantity);
        }

        [Fact]
        public async Task RecipeOption_CarriesDisciplinesFromRawRecipe()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Disciplines = new List<string> { "Weaponsmith", "Huntsman" }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.Equal(2, option.Disciplines.Count);
            Assert.Contains("Weaponsmith", option.Disciplines);
            Assert.Contains("Huntsman", option.Disciplines);
        }

        [Fact]
        public async Task RecipeOption_CarriesMinRatingAndFlags()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Disciplines = new List<string> { "Armorsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.Equal(400, option.MinRating);
            Assert.Single(option.Flags);
            Assert.Contains("AutoLearned", option.Flags);
        }

        [Fact]
        public async Task RecipeOption_DefaultsWhenFieldsAbsent()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                }
                // No Disciplines, MinRating, or Flags set — use defaults
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.Empty(option.Disciplines);
            Assert.Equal(0, option.MinRating);
            Assert.Empty(option.Flags);
        }

        [Fact]
        public async Task RecipeOption_MissingFlags_DefaultsToNotAutoLearned()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Flags = new List<string>()  // empty flags
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.DoesNotContain("AutoLearned", option.Flags);
        }
    }
}
