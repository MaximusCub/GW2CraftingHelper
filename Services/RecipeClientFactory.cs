using System;

namespace GW2CraftingHelper.Services
{
    internal static class RecipeClientFactory
    {
        // Quality-audit B3 (KNOWN-ISSUES #53): mfData.LoadWarnings was
        // collected and never logged, and this catch swallowed the load
        // exception silently. Wired both to ModuleLog (optional injection,
        // defaults to ModuleLog.Shared - see Module.cs's other
        // construction sites; tests inject an isolated instance instead).
        // Only a warning COUNT is logged, not the raw strings - one
        // LoadWarnings category embeds a raw item id, and this Warn line
        // is a Log-tab-visible surface the item/currency/vendor-id-
        // internal-only invariant covers (see PlanStructuralValidator.
        // NoNullValues for the same precedent).
        public static IRecipeApiClient Create(
            IRecipeApiClient primary,
            IMysticForgeRecipeSource mfSource,
            ModuleLog moduleLog = null)
        {
            return Create(primary, LoadData(mfSource, moduleLog));
        }

        /// <summary>
        /// The same client over already-loaded data, for a caller that
        /// needs the data itself as well - Module folds it into the recipe
        /// seed (SeededRecipeCacheStore.MergeMysticForgeRecipes) so the
        /// wiki recipes are served from cache instead of only rescuing an
        /// API round trip.
        /// </summary>
        public static IRecipeApiClient Create(IRecipeApiClient primary, MysticForgeRecipeData mfData)
        {
            return new CompositeRecipeApiClient(primary, mfData ?? MysticForgeRecipeData.Empty);
        }

        public static MysticForgeRecipeData LoadData(
            IMysticForgeRecipeSource mfSource,
            ModuleLog moduleLog = null)
        {
            var log = moduleLog ?? ModuleLog.Shared;

            MysticForgeRecipeData mfData;
            try
            {
                using (var stream = mfSource.Open())
                {
                    mfData = MysticForgeRecipeData.Load(stream);
                }
            }
            catch (Exception ex)
            {
                log.Write(ModuleLogLevel.Warn, "startup", $"Mystic Forge recipes unavailable: [{ex.GetType().Name}] {ex.Message}");
                mfData = MysticForgeRecipeData.Empty;
            }

            if (mfData.LoadWarnings.Count > 0)
            {
                log.Write(ModuleLogLevel.Warn, "startup",
                    $"Mystic Forge recipes: loaded {mfData.RecipeCount}, {mfData.LoadWarnings.Count} warning(s) during load - see ref/mystic_forge_recipes.json");
            }

            return mfData;
        }
    }
}
