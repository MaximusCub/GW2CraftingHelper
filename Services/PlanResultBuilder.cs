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

            // Derive required disciplines from Craft steps.
            // Goal: produce a minimal, actionable set of disciplines the user needs.
            // Two-pass approach:
            //   Pass 1 — single-discipline recipes are must-use (no choice).
            //   Pass 2 — multi-discipline recipes prefer reusing an already-selected
            //             discipline; if none overlap, pick alphabetically first
            //             for determinism.
            // Max MinRating is tracked per selected discipline.
            var craftSteps = plan.Steps.Where(s => s.Source == AcquisitionSource.Craft).ToList();
            var disciplineMap = new Dictionary<string, int>(); // discipline → max rating

            // Resolve all craft step options up front
            var stepOptions = new List<RecipeOption>();
            foreach (var step in craftSteps)
            {
                var option = FindRecipeOption(treeUsedForSolve, step.RecipeId);
                if (option != null && option.Disciplines.Count > 0)
                {
                    stepOptions.Add(option);
                }
            }

            // Pass 1: single-discipline recipes (must-use)
            foreach (var option in stepOptions)
            {
                if (option.Disciplines.Count == 1)
                {
                    var disc = option.Disciplines[0];
                    if (!disciplineMap.ContainsKey(disc) || option.MinRating > disciplineMap[disc])
                    {
                        disciplineMap[disc] = option.MinRating;
                    }
                }
            }

            // Pass 2: multi-discipline recipes (prefer reuse, else alphabetically first)
            foreach (var option in stepOptions)
            {
                if (option.Disciplines.Count <= 1)
                {
                    continue;
                }

                // Try to reuse an already-selected discipline
                var reusable = option.Disciplines
                    .Where(d => disciplineMap.ContainsKey(d))
                    .OrderBy(d => d)
                    .FirstOrDefault();

                var selected = reusable ?? option.Disciplines.OrderBy(d => d).First();

                if (!disciplineMap.ContainsKey(selected) || option.MinRating > disciplineMap[selected])
                {
                    disciplineMap[selected] = option.MinRating;
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
                bool? isMissing;
                if (IsMysticForgeRecipeId(step.RecipeId))
                {
                    // Mystic Forge recipes are inherently available — no unlock needed
                    isMissing = false;
                }
                else
                {
                    isMissing = learnedRecipeIds != null
                        ? (bool?)!learnedRecipeIds.Contains(step.RecipeId)
                        : null;
                }

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

        private static bool IsMysticForgeRecipeId(int recipeId)
        {
            return recipeId < 0;
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
