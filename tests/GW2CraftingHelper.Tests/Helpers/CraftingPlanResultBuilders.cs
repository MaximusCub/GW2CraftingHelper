using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Tests.Helpers
{
    /// <summary>
    /// Shared CraftingPlanResult/ItemMetadata builder helpers (M38 WP-20).
    /// MakeResult and MetaFor were private static methods on
    /// PlanViewModelBuilderTests before that 1798-line file was split into
    /// focused test files - both helpers are called from every split file,
    /// so they moved here rather than being duplicated per file.
    /// </summary>
    public static class CraftingPlanResultBuilders
    {
        public static CraftingPlanResult MakeResult(
            int targetItemId = 1,
            int targetQuantity = 1,
            long totalCoinCost = 0,
            List<PlanStep> steps = null,
            List<CurrencyCost> currencyCosts = null,
            Dictionary<int, ItemMetadata> metadata = null,
            List<UsedMaterial> usedMaterials = null,
            List<RequiredDiscipline> requiredDisciplines = null,
            List<RequiredRecipe> requiredRecipes = null,
            Dictionary<int, CurrencyMetadata> currencyMetadata = null,
            Dictionary<int, AcquisitionHint> acquisitionHints = null,
            List<TimegatedItem> timegatedItems = null,
            List<PlanRequestItem> requestedItems = null,
            List<CraftingTreeNode> multiItemRoots = null,
            List<SnapshotCharacterDiscipline> characterDisciplines = null,
            Dictionary<int, DailyCooldownItem> dailyCooldownItems = null)
        {
            return new CraftingPlanResult
            {
                Plan = new CraftingPlan
                {
                    TargetItemId = targetItemId,
                    TargetQuantity = targetQuantity,
                    TotalCoinCost = totalCoinCost,
                    Steps = steps ?? new List<PlanStep>(),
                    CurrencyCosts = currencyCosts ?? new List<CurrencyCost>(),
                    TimegatedItems = timegatedItems ?? new List<TimegatedItem>()
                },
                ItemMetadata = metadata != null
                    ? metadata
                    : new Dictionary<int, ItemMetadata>(),
                UsedMaterials = usedMaterials,
                RequiredDisciplines = requiredDisciplines ?? new List<RequiredDiscipline>(),
                RequiredRecipes = requiredRecipes ?? new List<RequiredRecipe>(),
                DebugLog = new List<string>(),
                CurrencyMetadata = currencyMetadata,
                AcquisitionHints = acquisitionHints,
                DailyCooldownItems = dailyCooldownItems,
                RequestedItems = requestedItems,
                MultiItemRoots = multiItemRoots,
                // W3C: defaults to null (not an empty list) - matches
                // AccountSnapshot.CharacterDisciplines/CraftingPlanResult.
                // CharacterDisciplines' own "no data captured" null
                // convention, so a test that doesn't pass this explicitly
                // exercises the same "no data" path production code hits
                // for every pre-W3C/degraded snapshot.
                CharacterDisciplines = characterDisciplines
            };
        }

        public static Dictionary<int, ItemMetadata> MetaFor(params (int id, string name, string icon)[] items)
        {
            var dict = new Dictionary<int, ItemMetadata>();
            foreach (var (id, name, icon) in items)
            {
                dict[id] = new ItemMetadata { ItemId = id, Name = name, IconUrl = icon };
            }
            return dict;
        }
    }
}
