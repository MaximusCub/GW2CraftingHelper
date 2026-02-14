using System.IO;
using System.Text;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
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
    }
}
