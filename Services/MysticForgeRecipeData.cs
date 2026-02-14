using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GW2CraftingHelper.Services
{
    public class MysticForgeRecipeData
    {
        public static readonly MysticForgeRecipeData Empty = new MysticForgeRecipeData(
            new Dictionary<int, RawRecipe>(),
            new Dictionary<int, List<int>>(),
            new List<string>());

        private readonly Dictionary<int, RawRecipe> _byRecipeId;
        private readonly Dictionary<int, List<int>> _byOutputItemId;
        private readonly List<string> _loadWarnings;

        private MysticForgeRecipeData(
            Dictionary<int, RawRecipe> byRecipeId,
            Dictionary<int, List<int>> byOutputItemId,
            List<string> loadWarnings)
        {
            _byRecipeId = byRecipeId;
            _byOutputItemId = byOutputItemId;
            _loadWarnings = loadWarnings;
        }

        public IReadOnlyList<string> LoadWarnings => _loadWarnings;

        public IReadOnlyList<int> SearchByOutput(int itemId)
        {
            if (_byOutputItemId.TryGetValue(itemId, out var ids))
            {
                return ids;
            }

            return new List<int>();
        }

        public RawRecipe GetRecipe(int recipeId)
        {
            if (_byRecipeId.TryGetValue(recipeId, out var recipe))
            {
                return recipe;
            }

            return null;
        }

        public static MysticForgeRecipeData Load(Stream stream)
        {
            if (stream == null)
            {
                return Empty;
            }

            JObject root;
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                try
                {
                    root = JObject.Load(jsonReader);
                }
                catch (JsonException)
                {
                    return Empty;
                }
            }

            var schemaVersion = root.Value<int?>("schemaVersion");
            if (schemaVersion == null || schemaVersion.Value != 1)
            {
                return Empty;
            }

            var recipesToken = root["recipes"];
            if (recipesToken == null || recipesToken.Type != JTokenType.Array)
            {
                return Empty;
            }

            var byRecipeId = new Dictionary<int, RawRecipe>();
            var byOutputItemId = new Dictionary<int, List<int>>();
            var warnings = new List<string>();

            foreach (var entry in recipesToken)
            {
                var id = entry.Value<int?>("id");
                if (id == null)
                {
                    warnings.Add("Skipped recipe with missing id");
                    continue;
                }

                if (id.Value >= 0)
                {
                    warnings.Add($"Skipped recipe id={id.Value}: MF recipe IDs must be negative");
                    continue;
                }

                var outputItemId = entry.Value<int?>("outputItemId");
                if (outputItemId == null || outputItemId.Value <= 0)
                {
                    warnings.Add($"Skipped recipe id={id.Value}: invalid outputItemId");
                    continue;
                }

                var outputItemCount = entry.Value<int?>("outputItemCount");
                if (outputItemCount == null || outputItemCount.Value <= 0)
                {
                    warnings.Add($"Skipped recipe id={id.Value}: outputItemCount must be > 0");
                    continue;
                }

                var ingredientsToken = entry["ingredients"];
                if (ingredientsToken == null || ingredientsToken.Type != JTokenType.Array || !ingredientsToken.Any())
                {
                    warnings.Add($"Skipped recipe id={id.Value}: missing or empty ingredients");
                    continue;
                }

                var ingredients = new List<RawIngredient>();
                bool valid = true;

                foreach (var ing in ingredientsToken)
                {
                    var type = ing.Value<string>("type");
                    var ingId = ing.Value<int?>("id");
                    var count = ing.Value<int?>("count");

                    if (string.IsNullOrEmpty(type) || ingId == null || count == null || count.Value <= 0)
                    {
                        warnings.Add($"Skipped recipe id={id.Value}: invalid ingredient (type={type}, id={ingId}, count={count})");
                        valid = false;
                        break;
                    }

                    ingredients.Add(new RawIngredient
                    {
                        Type = type,
                        Id = ingId.Value,
                        Count = count.Value
                    });
                }

                if (!valid)
                {
                    continue;
                }

                var recipe = new RawRecipe
                {
                    Id = id.Value,
                    OutputItemId = outputItemId.Value,
                    OutputItemCount = outputItemCount.Value,
                    Ingredients = ingredients,
                    Disciplines = new List<string> { "MysticForge" },
                    MinRating = 0,
                    Flags = new List<string>()
                };

                byRecipeId[recipe.Id] = recipe;

                if (!byOutputItemId.TryGetValue(recipe.OutputItemId, out var list))
                {
                    list = new List<int>();
                    byOutputItemId[recipe.OutputItemId] = list;
                }

                list.Add(recipe.Id);
            }

            return new MysticForgeRecipeData(byRecipeId, byOutputItemId, warnings);
        }
    }
}
