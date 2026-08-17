using System;

namespace GW2CraftingHelper.Services
{
    public static class RecipeClientFactory
    {
        // Quality-audit fix (B3): mfData.LoadWarnings used to be collected
        // and silently discarded (a corrupt mystic_forge_recipes.json was
        // invisible despite the module having a log sink), and this catch
        // used to swallow the load exception itself just as silently.
        // Optional ModuleLog injection (defaults to the app-wide
        // ModuleLog.Shared singleton - see Module.cs's own construction
        // sites for the same idiom, none of which pass this) so tests can
        // inject an isolated `new ModuleLog()` instance instead of
        // touching Shared - see ModuleLog's own class doc comment on why
        // Shared is unsuitable for exact-count/content test assertions.
        //
        // Review fix: the individual LoadWarnings strings are NOT joined
        // into the logged message, even though a Warn line here (unlike a
        // thrown exception) would be worth the detail. One of them
        // (MysticForgeRecipeData's "invalid ingredient" warning) embeds
        // the ingredient's raw item id - and PlanStructuralValidator.
        // NoNullValues' own doc comment already establishes, for this
        // exact "Warn-level ModuleLog line the Log tab shows the user"
        // surface, that the item/currency/vendor-id-internal-only
        // invariant applies there exactly as much as to any other UI
        // surface. Only the count is logged; a maintainer chasing down a
        // corrupt seed file reads ref/mystic_forge_recipes.json directly.
        public static IRecipeApiClient Create(
            IRecipeApiClient primary,
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

            return new CompositeRecipeApiClient(primary, mfData);
        }
    }
}
