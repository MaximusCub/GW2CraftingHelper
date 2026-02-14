using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class PlanResultBuilder
    {
        public CraftingPlanResult Build(
            CraftingPlan plan,
            RecipeNode treeUsedForSolve,
            IReadOnlyDictionary<int, ItemMetadata> metadata,
            List<UsedMaterial> usedMaterials,
            ISet<int> learnedRecipeIds)
        {
            var debugLog = new List<string>();

            // Debug: reduction summary
            if (usedMaterials == null)
            {
                debugLog.Add("No inventory reduction (snapshot not provided)");
            }
            else if (usedMaterials.Count == 0)
            {
                debugLog.Add("No inventory reduction (no owned items matched)");
            }
            else
            {
                var parts = usedMaterials
                    .Select(u => $"{u.QuantityUsed} of item {u.ItemId}")
                    .ToList();
                debugLog.Add($"Reduced: used {usedMaterials.Count} owned items ({string.Join(", ", parts)})");
            }

            // Debug: source decisions
            foreach (var step in plan.Steps)
            {
                switch (step.Source)
                {
                    case AcquisitionSource.Craft:
                        debugLog.Add($"Item {step.ItemId} (qty {step.Quantity}): Craft via recipe {step.RecipeId}");
                        break;
                    case AcquisitionSource.BuyFromTp:
                        debugLog.Add($"Item {step.ItemId} (qty {step.Quantity}): BuyFromTp @ {step.UnitCost}c");
                        break;
                    case AcquisitionSource.BuyFromVendor:
                        debugLog.Add($"Item {step.ItemId} (qty {step.Quantity}): BuyFromVendor @ {step.UnitCost}c");
                        break;
                    case AcquisitionSource.Currency:
                        debugLog.Add($"Item {step.ItemId} (qty {step.Quantity}): Currency");
                        break;
                    default:
                        debugLog.Add($"Item {step.ItemId} (qty {step.Quantity}): {step.Source}");
                        break;
                }
            }

            // Derive required disciplines from Craft steps
            var craftSteps = plan.Steps.Where(s => s.Source == AcquisitionSource.Craft).ToList();
            var disciplineMap = new Dictionary<string, int>();

            foreach (var step in craftSteps)
            {
                var option = FindRecipeOption(treeUsedForSolve, step.RecipeId);
                if (option == null)
                {
                    continue;
                }

                foreach (var disc in option.Disciplines)
                {
                    if (!disciplineMap.ContainsKey(disc) || option.MinRating > disciplineMap[disc])
                    {
                        disciplineMap[disc] = option.MinRating;
                    }
                }
            }

            var requiredDisciplines = disciplineMap
                .OrderBy(kv => kv.Key)
                .Select(kv => new RequiredDiscipline
                {
                    Discipline = kv.Key,
                    MinRating = kv.Value
                })
                .ToList();

            // Debug: required disciplines
            if (requiredDisciplines.Count > 0)
            {
                var discParts = requiredDisciplines.Select(d => $"{d.Discipline} ({d.MinRating})");
                debugLog.Add($"Required disciplines: {string.Join(", ", discParts)}");
            }

            // Derive required recipes from Craft steps
            var seenRecipeIds = new HashSet<int>();
            var requiredRecipes = new List<RequiredRecipe>();

            foreach (var step in craftSteps)
            {
                if (!seenRecipeIds.Add(step.RecipeId))
                {
                    continue;
                }

                var option = FindRecipeOption(treeUsedForSolve, step.RecipeId);
                if (option == null)
                {
                    continue;
                }

                bool isAutoLearned = option.Flags.Contains("AutoLearned");
                bool? isMissing = learnedRecipeIds != null
                    ? (bool?)!learnedRecipeIds.Contains(step.RecipeId)
                    : null;

                requiredRecipes.Add(new RequiredRecipe
                {
                    RecipeId = step.RecipeId,
                    OutputItemId = step.ItemId,
                    IsAutoLearned = isAutoLearned,
                    MinRating = option.MinRating,
                    Disciplines = new List<string>(option.Disciplines),
                    IsMissing = isMissing
                });
            }

            // Debug: missing recipes
            if (learnedRecipeIds != null)
            {
                var missing = requiredRecipes.Where(r => r.IsMissing == true).ToList();
                if (missing.Count > 0)
                {
                    var parts = missing.Select(r =>
                    {
                        var disc = r.Disciplines.Count > 0 ? r.Disciplines[0] : "Unknown";
                        return $"{r.RecipeId} ({disc} {r.MinRating})";
                    });
                    debugLog.Add($"Missing recipes: {string.Join(", ", parts)}");
                }
            }
            else
            {
                debugLog.Add("Recipe permission not available");
            }

            return new CraftingPlanResult
            {
                Plan = plan,
                ItemMetadata = metadata,
                UsedMaterials = usedMaterials ?? new List<UsedMaterial>(),
                RequiredDisciplines = requiredDisciplines,
                RequiredRecipes = requiredRecipes,
                DebugLog = debugLog
            };
        }

        private static RecipeOption FindRecipeOption(RecipeNode node, int recipeId)
        {
            foreach (var option in node.Recipes)
            {
                if (option.RecipeId == recipeId)
                {
                    return option;
                }

                foreach (var ingredient in option.Ingredients)
                {
                    var found = FindRecipeOption(ingredient, recipeId);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }
    }
}
