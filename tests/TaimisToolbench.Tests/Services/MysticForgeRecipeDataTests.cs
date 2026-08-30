using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TaimisToolbench.Services;
using Xunit;
using static TaimisToolbench.Tests.Helpers.RepoFileLocator;

namespace TaimisToolbench.Tests.Services
{
    public class MysticForgeRecipeDataTests
    {
        private static Stream ToStream(string json)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }

        [Fact]
        public void Load_ValidRecipe_IndexesByRecipeId()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 },
                            { ""type"": ""Item"", ""id"": 24283, ""count"": 250 },
                            { ""type"": ""Item"", ""id"": 24300, ""count"": 250 },
                            { ""type"": ""Item"", ""id"": 24277, ""count"": 250 }
                        ],
                        ""comment"": ""Gift of Magic""
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));
            var recipe = data.GetRecipe(-1);

            Assert.NotNull(recipe);
            Assert.Equal(-1, recipe.Id);
            Assert.Equal(19673, recipe.OutputItemId);
            Assert.Equal(1, recipe.OutputItemCount);
            Assert.Equal(4, recipe.Ingredients.Count);
            Assert.Single(recipe.Disciplines);
            Assert.Contains("MysticForge", recipe.Disciplines);
            Assert.Equal(0, recipe.MinRating);
            Assert.Empty(recipe.Flags);
        }

        [Fact]
        public void Load_ValidRecipe_IndexesByOutputItemId()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 }
                        ]
                    },
                    {
                        ""id"": -2,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24283, ""count"": 250 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));
            var ids = data.SearchByOutput(19673);

            Assert.Equal(2, ids.Count);
            Assert.Contains(-1, ids);
            Assert.Contains(-2, ids);
        }

        [Fact]
        public void SearchByOutput_UnknownItem_ReturnsEmpty()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));
            var ids = data.SearchByOutput(99999);

            Assert.Empty(ids);
        }

        [Fact]
        public void SearchByOutput_UnknownItem_ReturnsSameInstance()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));
            var first = data.SearchByOutput(99999);
            var second = data.SearchByOutput(88888);

            Assert.Empty(first);
            Assert.True(ReferenceEquals(first, second), "Empty results should be the same instance");
        }

        [Fact]
        public void GetRecipe_UnknownId_ReturnsNull()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));
            var recipe = data.GetRecipe(-99);

            Assert.Null(recipe);
        }

        [Fact]
        public void Load_EmptyRecipesArray_ReturnsEmptyData()
        {
            var json = @"{ ""schemaVersion"": 1, ""recipes"": [] }";

            var data = MysticForgeRecipeData.Load(ToStream(json));

            Assert.Empty(data.SearchByOutput(19673));
            Assert.Null(data.GetRecipe(-1));
            Assert.Empty(data.LoadWarnings);
        }

        [Fact]
        public void Load_MissingSchemaVersion_ReturnsEmpty()
        {
            var json = @"{ ""recipes"": [] }";

            var data = MysticForgeRecipeData.Load(ToStream(json));

            Assert.Same(MysticForgeRecipeData.Empty, data);
        }

        [Fact]
        public void Load_UnknownSchemaVersion_ReturnsEmpty()
        {
            var json = @"{ ""schemaVersion"": 99, ""recipes"": [] }";

            var data = MysticForgeRecipeData.Load(ToStream(json));

            Assert.Same(MysticForgeRecipeData.Empty, data);
        }

        [Fact]
        public void Load_NullStream_ReturnsEmpty()
        {
            var data = MysticForgeRecipeData.Load(null);

            Assert.Same(MysticForgeRecipeData.Empty, data);
        }

        [Fact]
        public void Load_InvalidJson_ReturnsEmpty()
        {
            var data = MysticForgeRecipeData.Load(ToStream("not valid json"));

            Assert.Same(MysticForgeRecipeData.Empty, data);
        }

        [Fact]
        public void Load_PositiveId_SkipsWithWarning()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": 5,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));

            Assert.Null(data.GetRecipe(5));
            Assert.Single(data.LoadWarnings);
            Assert.Contains("must be negative", data.LoadWarnings[0]);
        }

        [Fact]
        public void Load_ZeroId_SkipsWithWarning()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": 0,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));

            Assert.Null(data.GetRecipe(0));
            Assert.Single(data.LoadWarnings);
            Assert.Contains("must be negative", data.LoadWarnings[0]);
        }

        [Fact]
        public void Load_ZeroOutputItemCount_SkipsWithWarning()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 0,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));

            Assert.Null(data.GetRecipe(-1));
            Assert.Single(data.LoadWarnings);
            Assert.Contains("outputItemCount must be > 0", data.LoadWarnings[0]);
        }

        [Fact]
        public void Load_ZeroIngredientCount_SkipsWithWarning()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 0 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));

            Assert.Null(data.GetRecipe(-1));
            Assert.Single(data.LoadWarnings);
            Assert.Contains("invalid ingredient", data.LoadWarnings[0]);
        }

        [Fact]
        public void Load_UnknownFieldsIgnored()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""extraField"": ""ignored"",
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""unknownProp"": true,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250, ""name"": ""ignored"" }
                        ],
                        ""comment"": ""this is also ignored""
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));
            var recipe = data.GetRecipe(-1);

            Assert.NotNull(recipe);
            Assert.Equal(19673, recipe.OutputItemId);
            Assert.Empty(data.LoadWarnings);
        }

        [Fact]
        public void Load_IngredientsPreserveTypeAndValues()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 },
                            { ""type"": ""Item"", ""id"": 24283, ""count"": 100 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));
            var recipe = data.GetRecipe(-1);

            Assert.Equal(2, recipe.Ingredients.Count);

            Assert.Equal("Item", recipe.Ingredients[0].Type);
            Assert.Equal(24295, recipe.Ingredients[0].Id);
            Assert.Equal(250, recipe.Ingredients[0].Count);

            Assert.Equal("Item", recipe.Ingredients[1].Type);
            Assert.Equal(24283, recipe.Ingredients[1].Id);
            Assert.Equal(100, recipe.Ingredients[1].Count);
        }

        [Fact]
        public void Load_MixOfValidAndInvalid_LoadsValidOnly()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 }
                        ]
                    },
                    {
                        ""id"": 5,
                        ""outputItemId"": 19672,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24357, ""count"": 250 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));

            Assert.NotNull(data.GetRecipe(-1));
            Assert.Null(data.GetRecipe(5));
            Assert.Single(data.LoadWarnings);
        }

        // --- expectedOutputCount (Mystic Clover EV support) ---
        [Fact]
        public void Load_ExpectedOutputCount_ParsedAsFractionalDouble()
        {
            // Mystic Clover shape: nominal outputItemCount=1, real gw2e
            // expected yield 0.31 (r2 report).
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19675,
                        ""outputItemCount"": 1,
                        ""expectedOutputCount"": 0.31,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 19925, ""count"": 1 },
                            { ""type"": ""Item"", ""id"": 19976, ""count"": 1 },
                            { ""type"": ""Item"", ""id"": 19721, ""count"": 1 },
                            { ""type"": ""Item"", ""id"": 20796, ""count"": 6 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));
            var recipe = data.GetRecipe(-1);

            Assert.NotNull(recipe);
            Assert.Equal(1, recipe.OutputItemCount);
            Assert.Equal(0.31, recipe.ExpectedOutputCount);
        }

        [Fact]
        public void Load_NoExpectedOutputCount_DefaultsToNull()
        {
            // The overwhelming majority of recipes never set this field -
            // RecipeService is responsible for defaulting it to
            // OutputItemCount (a no-op) when null.
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));
            var recipe = data.GetRecipe(-1);

            Assert.NotNull(recipe);
            Assert.Null(recipe.ExpectedOutputCount);
        }

        [Fact]
        public void Load_ZeroExpectedOutputCount_SkipsWithWarning()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19675,
                        ""outputItemCount"": 1,
                        ""expectedOutputCount"": 0,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 19925, ""count"": 1 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));

            Assert.Null(data.GetRecipe(-1));
            Assert.Single(data.LoadWarnings);
            Assert.Contains("expectedOutputCount must be > 0", data.LoadWarnings[0]);
        }

        [Fact]
        public void Load_NegativeExpectedOutputCount_SkipsWithWarning()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19675,
                        ""outputItemCount"": 1,
                        ""expectedOutputCount"": -0.5,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 19925, ""count"": 1 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));

            Assert.Null(data.GetRecipe(-1));
            Assert.Single(data.LoadWarnings);
            Assert.Contains("expectedOutputCount must be > 0", data.LoadWarnings[0]);
        }

        [Fact]
        public void Load_NullExpectedOutputCount_TreatedAsAbsent()
        {
            var json = @"{
                ""schemaVersion"": 1,
                ""recipes"": [
                    {
                        ""id"": -1,
                        ""outputItemId"": 19673,
                        ""outputItemCount"": 1,
                        ""expectedOutputCount"": null,
                        ""ingredients"": [
                            { ""type"": ""Item"", ""id"": 24295, ""count"": 250 }
                        ]
                    }
                ]
            }";

            var data = MysticForgeRecipeData.Load(ToStream(json));
            var recipe = data.GetRecipe(-1);

            Assert.NotNull(recipe);
            Assert.Empty(data.LoadWarnings);
            Assert.Null(recipe.ExpectedOutputCount);
        }

        // --- ref/mystic_forge_recipes.json (868KB)
        // was never exercised through the production MysticForgeRecipeData
        // loader by a committed test - mirrors the FindRepoFile shipped-
        // file pattern established in AcquisitionHintServiceTests /
        // RecipeCacheSerializerTests.
        [Fact]
        public void Load_ShippedSeedFile_ParsesAllRecipesWithNoWarnings()
        {
            string path = FindRepoFile(Path.Combine("ref", "mystic_forge_recipes.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/mystic_forge_recipes.json by walking up from the test assembly's directory.");

            using (var stream = File.OpenRead(path))
            {
                var data = MysticForgeRecipeData.Load(stream);

                Assert.Empty(data.LoadWarnings);
                Assert.Equal(1681, data.RecipeCount);

                // Recipe: Tray of Banana Cream Pies (from wiki).
                var recipe = data.GetRecipe(-1);
                Assert.NotNull(recipe);
                Assert.Equal(9638, recipe.OutputItemId);
                Assert.Equal(1, recipe.OutputItemCount);
                Assert.Equal(4, recipe.Ingredients.Count);
                Assert.Contains("MysticForge", recipe.Disciplines);

                Assert.NotEmpty(data.SearchByOutput(9638));
                Assert.Contains(-1, data.SearchByOutput(9638));
            }
        }

        // Ids -1596..-1685 are hand-authored and a MysticForgeSeeder rerun
        // DELETES them rather than reproducing them: that tool resolves a
        // wiki ingredient by name, and all 90 of these recipes name their
        // ascended precursor with a variant anchor (".. Breastplate#item2"),
        // which resolves to no item id, so its Step 4 skips the recipe
        // whole. Without this pin the module silently loses every WvW and
        // PvP legendary armour plan again.
        [Fact]
        public void Load_ShippedSeedFile_KeepsTheHandAuthoredLegendaryArmourBlock()
        {
            string path = FindRepoFile(Path.Combine("ref", "mystic_forge_recipes.json"));

            using (var stream = File.OpenRead(path))
            {
                var data = MysticForgeRecipeData.Load(stream);

                for (int id = -1596; id >= -1685; id--)
                {
                    var row = data.GetRecipe(id);
                    Assert.True(row != null, "recipe " + id + " is missing");
                    Assert.Equal(4, row.Ingredients.Count);
                    Assert.Equal(1, row.OutputItemCount);
                    Assert.Contains("MysticForge", row.Disciplines);
                    Assert.Contains(id, data.SearchByOutput(row.OutputItemId));
                }

                // Triumphant Hero's Breastplate (WvW): ascended 81304 plus
                // the three Gifts of War.
                var wvw = Assert.Single(data.SearchByOutput(83394));
                AssertUpgradeRecipe(
                    data.GetRecipe(wvw), 81304, 82746, 84168, 83259);

                // Ardent Glorious Breastplate (PvP): ascended 67143 plus
                // the three Gifts of Competitive.
                var pvp = Assert.Single(data.SearchByOutput(83348));
                AssertUpgradeRecipe(
                    data.GetRecipe(pvp), 67143, 84174, 82350, 84203);
            }
        }

        private static void AssertUpgradeRecipe(
            RawRecipe recipe, int precursorItemId, params int[] giftItemIds)
        {
            Assert.NotNull(recipe);
            Assert.All(recipe.Ingredients, i => Assert.Equal("Item", i.Type));
            Assert.All(recipe.Ingredients, i => Assert.Equal(1, i.Count));

            var ids = new List<int> { precursorItemId };
            ids.AddRange(giftItemIds);
            Assert.Equal(ids, recipe.Ingredients.Select(i => i.Id).ToList());
        }
    }
}
