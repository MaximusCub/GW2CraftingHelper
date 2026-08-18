using System;
using System.IO;
using System.Text.Json;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Shared read-and-parse step for the static JSON seed loaders
    /// (AcquisitionHintService, DailyCooldownItemService,
    /// RecipeSheetItemSeedService): case-insensitive deserialize of the
    /// whole stream, null on any read/parse failure so each caller can
    /// degrade to its empty result. The catch deliberately covers ONLY
    /// reading and deserializing - the callers' own row loops are property
    /// copies and integer comparisons that cannot throw, so a seed loader
    /// still never throws.
    /// </summary>
    internal static class JsonSeedReader
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        internal static T Deserialize<T>(Stream stream)
            where T : class
        {
            if (stream == null)
            {
                return null;
            }

            try
            {
                using (var reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    return JsonSerializer.Deserialize<T>(json, Options);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
