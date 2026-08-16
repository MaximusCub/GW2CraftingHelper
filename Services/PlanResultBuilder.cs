using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class PlanResultBuilder
    {
        // Disciplines that are informational source tags, not real,
        // player-levelable GW2 crafting disciplines: a recipe carrying one
        // of these is inherently available whenever its ingredients are,
        // with no "learn this recipe" unlock concept at all (mirrors the
        // pre-existing Mystic Forge treatment below).
        private static readonly HashSet<string> InherentlyAvailableDisciplines =
            new HashSet<string> { "MysticForge", "Achievement", "Merchant" };

        // M37 fix-pass (adversarial review finding): "Achievement"/
        // "Merchant" are gw2e-borrowed informational tags on the new
        // achievement-bit seed recipes (ref/recipes_seed.json ids
        // -1592..-1595) - not real GW2 crafting disciplines the player can
        // select or level. This module otherwise models vendor purchases
        // via ref/vendor_offers.json, never as a discipline (see
        // docs/KNOWN-ISSUES.md #26). Filtered out of Required Disciplines
        // below so the player is never told to "level" a discipline that
        // does not exist; RequiredRecipes' own per-recipe Disciplines field
        // is left untouched (accurate, informational metadata about that
        // specific recipe's real source).
        //
        // Field-test finding E (user-approved, supersedes the M37 comment
        // this replaces): the Mystic Forge is a facility, not a player-
        // levelable discipline either - it has no rating requirement and
        // nothing to unlock, so listing it under "Required Disciplines"
        // read as asking the player to "level" a facility that has no
        // levels. MysticForge now joins Achievement/Merchant here; its
        // per-recipe Disciplines field (RequiredRecipes, PlanViewModelBuilder.
        // FormatDisciplineSublabel) is unaffected, same as Achievement/
        // Merchant - see that method's own MysticForge special case for how
        // its sublabel text renders instead ("Mystic Forge" facility name,
        // no level number).
        private static readonly HashSet<string> NonCraftingDisciplines =
            new HashSet<string> { "Achievement", "Merchant", "MysticForge" };

        public CraftingPlanResult Build(
            CraftingPlan plan,
            RecipeNode treeUsedForSolve,
            IReadOnlyDictionary<int, ItemMetadata> metadata,
            List<UsedMaterial> usedMaterials,
            ISet<int> learnedRecipeIds,
            // W3C review-fix (mustFix): which disciplines the account
            // actually has, used ONLY to break the Pass 2 greedy-cover tie
            // among equally-covering, not-yet-selected disciplines below -
            // see the accountDisciplineNames doc comment further down.
            // Optional/defaults to null so every pre-existing caller (every
            // test in PlanResultBuilderTests, any future caller with no
            // snapshot) is unaffected and falls back to the pre-W3C
            // coverage-then-alphabetical order.
            IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines = null)
        {
            var debugLog = new List<string>();

            // W3C review-fix: a discipline the account has ANY character
            // trained in (any rating, active or not - see
            // SnapshotCharacterDiscipline's own doc comment on why rating
            // persists regardless of Active) is preferred over an
            // equally-covering discipline nobody has, when the greedy cover
            // below would otherwise fall through to a pure alphabetical
            // pick. This never changes WHICH recipes need a discipline or
            // HOW MANY disciplines are required - only, among ties, which
            // discipline's name is reported - so it cannot affect
            // usedMaterials/plan cost/decisions, only this cosmetic
            // labeling choice. Empty (not null) when characterDisciplines
            // is null, so the ThenByDescending below is a harmless no-op
            // and every discipline ties at 0, preserving the exact
            // pre-W3C alphabetical fallback.
            var accountDisciplineNames = characterDisciplines != null
                ? new HashSet<string>(
                    characterDisciplines.Where(cd => cd != null).Select(cd => cd.Discipline),
                    StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

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
                // M38 WP-07 (perf P5): manual loop instead of Select().ToList() -
                // same content/order, one fewer LINQ iterator+delegate per Build().
                //
                // Scope note (documented descope, not silently dropped): the
                // approved WP-07 scope also asked to gate this construction
                // behind an "is anyone reading this debug log" check. No such
                // flag exists anywhere in this codebase - CraftingPlanView
                // stores this result on CraftingPlanView.LastDebugLog
                // (public, currently consumed by no view - M39 moved the Log
                // tab onto the separate module-wide ModuleLog ring instead of
                // this per-plan DebugLog; LastDebugLog is left untouched as
                // out-of-scope per d2-log-system.md's own brief, and remains
                // available for a future consumer, e.g. Plan History). Any
                // reader would only ever see this at some later, unrelated
                // point in time, long after this Build() call has already
                // returned with every line pre-formatted into strings. Wiring
                // a real "will this be read" check through to here would mean
                // either plumbing a "something is reading LastDebugLog right
                // now" bool from whatever consumer exists down through
                // CraftingPlanPipeline into this method, or changing
                // CraftingPlanResult.DebugLog's element type to defer
                // formatting to read time - both reach outside this package's
                // Services/PlanResultBuilder.cs scope and are scope creep for
                // this work package. Left eager/unconditional; noted here as
                // a candidate follow-up (perf P5) rather than dropped
                // silently.
                var parts = new List<string>(usedMaterials.Count);
                foreach (var used in usedMaterials)
                {
                    parts.Add($"{used.QuantityUsed} of item {used.ItemId}");
                }

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
            //   Pass 1 - single-discipline recipes are must-use (no choice).
            //   Pre-cover - remove multi-discipline recipes already coverable by
            //               a Pass 1 discipline; update max rating if needed.
            //   Pass 2 - greedy set cover for remaining uncovered recipes:
            //            highest coverage count, prefer already-selected, alpha.
            // Max MinRating is tracked per selected discipline.
            var craftSteps = plan.Steps.Where(s => s.Source == AcquisitionSource.Craft).ToList();
            var disciplineMap = new Dictionary<string, int>(); // discipline -> max rating

            // M38 WP-07 (perf P1): one tree walk, indexed by RecipeId,
            // replaces the two independent per-recipe-id DFS re-walks that
            // used to happen below (one while building stepOptions, one
            // while building requiredRecipes) via the old private
            // FindRecipeOption. See BuildRecipeOptionIndex/IndexRecipeOptions
            // for why "first write per RecipeId wins" reproduces the same
            // result the old recursive short-circuit search would have
            // returned, even if the same RecipeId legitimately appears at
            // more than one tree position.
            //
            // Only walk the tree when there is at least one Craft step:
            // both loops below that consult recipeOptionIndex are
            // `foreach (var step in craftSteps)`, so when craftSteps is
            // empty the old FindRecipeOption was never called at all and a
            // null treeUsedForSolve never threw. Matching that laziness
            // here (rather than unconditionally indexing treeUsedForSolve)
            // preserves that exact behavior instead of newly throwing on a
            // null tree whenever there happen to be zero craft steps.
            var recipeOptionIndex = craftSteps.Count > 0
                ? BuildRecipeOptionIndex(treeUsedForSolve)
                : new Dictionary<int, RecipeOption>();

            // Resolve craft step options (deduplicated by RecipeId)
            var seenOptionIds = new HashSet<int>();
            var stepOptions = new List<RecipeOption>();
            foreach (var step in craftSteps)
            {
                if (!seenOptionIds.Add(step.RecipeId))
                {
                    continue;
                }
                if (!recipeOptionIndex.TryGetValue(step.RecipeId, out var option))
                {
                    continue;
                }

                // M38 WP-07 (perf P5): manual loop instead of Where().ToList() -
                // same content/order, one fewer LINQ iterator+delegate per
                // craft step, every Build() call.
                var realDisciplines = new List<string>();
                foreach (var discipline in option.Disciplines)
                {
                    if (!NonCraftingDisciplines.Contains(discipline))
                    {
                        realDisciplines.Add(discipline);
                    }
                }

                if (realDisciplines.Count == 0)
                {
                    continue;
                }

                stepOptions.Add(new RecipeOption
                {
                    RecipeId = option.RecipeId,
                    Disciplines = realDisciplines,
                    MinRating = option.MinRating
                });
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

            // Pre-cover: multi-discipline recipes already coverable by a Pass 1
            // discipline do not need greedy selection. Pick exactly one covering
            // discipline per recipe (highest current rating, then alpha) and
            // update only that discipline's max rating.
            var uncovered = new List<RecipeOption>(
                stepOptions.Where(o => o.Disciplines.Count > 1));

            uncovered.RemoveAll(o =>
            {
                var covering = o.Disciplines
                    .Where(d => disciplineMap.ContainsKey(d))
                    .OrderByDescending(d => disciplineMap[d])
                    .ThenBy(d => d)
                    .FirstOrDefault();

                if (covering == null)
                {
                    return false;
                }

                if (o.MinRating > disciplineMap[covering])
                {
                    disciplineMap[covering] = o.MinRating;
                }
                return true;
            });

            // Pass 2: greedy set cover for remaining uncovered recipes.
            // Highest coverage count, then prefer already-selected, then alpha.
            while (uncovered.Count > 0)
            {
                // Count how many uncovered recipes each discipline covers
                var freq = new Dictionary<string, int>();
                foreach (var opt in uncovered)
                {
                    foreach (var d in opt.Disciplines)
                    {
                        freq[d] = freq.ContainsKey(d) ? freq[d] + 1 : 1;
                    }
                }

                // Best discipline: highest coverage, then prefer already-
                // selected, then prefer a discipline the account actually
                // has (W3C review-fix - see accountDisciplineNames' doc
                // comment above), then alpha.
                string best = freq.Keys
                    .OrderByDescending(d => freq[d])
                    .ThenByDescending(d => disciplineMap.ContainsKey(d) ? 1 : 0)
                    .ThenByDescending(d => accountDisciplineNames.Contains(d) ? 1 : 0)
                    .ThenBy(d => d)
                    .First();

                // Track max MinRating across covered recipes for this discipline
                foreach (var opt in uncovered)
                {
                    if (opt.Disciplines.Contains(best))
                    {
                        if (!disciplineMap.ContainsKey(best) || opt.MinRating > disciplineMap[best])
                        {
                            disciplineMap[best] = opt.MinRating;
                        }
                    }
                }

                uncovered.RemoveAll(o => o.Disciplines.Contains(best));
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

                if (!recipeOptionIndex.TryGetValue(step.RecipeId, out var option))
                {
                    continue;
                }

                bool isAutoLearned = option.Flags.Contains("AutoLearned");
                // UI-bundle milestone, Feature A (wiki links): same
                // Flags-membership pattern as isAutoLearned immediately
                // above - GW2 API recipe flags include "LearnedFromItem"
                // for a recipe unlocked via a consumable recipe sheet.
                bool isLearnedFromItem = option.Flags.Contains("LearnedFromItem");
                bool? isMissing;
                if (option.Disciplines.Any(d => InherentlyAvailableDisciplines.Contains(d)))
                {
                    // Membership check on the recipe's own declared
                    // Disciplines, not a bare "recipeId < 0" sign check
                    // (adversarial review finding: the M37 achievement/
                    // merchant seed recipes also use negative ids, adjacent
                    // to but distinct from the Mystic Forge id range, so a
                    // sign check alone cannot tell them apart). Mystic
                    // Forge/Achievement/Merchant recipes are all inherently
                    // available - no unlock needed.
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
                    IsLearnedFromItem = isLearnedFromItem,
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

        // M38 WP-07 (perf P1): builds a RecipeId -> RecipeOption index with a
        // single tree walk, replacing the old FindRecipeOption's repeated
        // per-recipe-id DFS re-walk from the root (called once per unique
        // craft-step RecipeId, twice per Build - once for stepOptions, once
        // for requiredRecipes - so the old code paid for the whole tree scan
        // up to 2x uniqueRecipeCount times per Build/pill-click).
        //
        // Deliberately no null check on root: the caller only invokes this
        // when craftSteps.Count > 0, which is exactly when the old
        // FindRecipeOption(treeUsedForSolve, ...) would have run and thrown
        // a NullReferenceException on a null tree. Guarding here instead
        // would silently swap that fail-loud crash for a quietly-empty
        // index (every craft step's recipe/discipline data dropped with no
        // error), which is a behavior change WP-07's scope does not
        // authorize.
        private static Dictionary<int, RecipeOption> BuildRecipeOptionIndex(RecipeNode root)
        {
            var index = new Dictionary<int, RecipeOption>();
            IndexRecipeOptions(root, index);
            return index;
        }

        // Preorder DFS over node.Recipes then each option's Ingredients (in
        // list order, fully descending into one ingredient's subtree before
        // moving to the next) - exactly the traversal order the old
        // recursive FindRecipeOption used. First write per RecipeId wins, so
        // if the same RecipeId legitimately appears at more than one tree
        // position, the indexed RecipeOption is the same one the old
        // per-query search would have returned first (the old search
        // stopped as soon as it matched; this walk cannot stop early since
        // it is indexing every id in one pass, but visiting order - and
        // therefore which occurrence is "first" - is unchanged).
        private static void IndexRecipeOptions(RecipeNode node, Dictionary<int, RecipeOption> index)
        {
            foreach (var option in node.Recipes)
            {
                if (!index.ContainsKey(option.RecipeId))
                {
                    index[option.RecipeId] = option;
                }

                foreach (var ingredient in option.Ingredients)
                {
                    IndexRecipeOptions(ingredient, index);
                }
            }
        }
    }
}
