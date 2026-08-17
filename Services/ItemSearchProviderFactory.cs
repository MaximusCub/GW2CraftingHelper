using System;
using System.IO;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Services.Recipes;

namespace GW2CraftingHelper.Services
{
    public static class ItemSearchProviderFactory
    {
        /// <summary>
        /// Creates an <see cref="IItemSearchProvider"/> from the given seed stream.
        /// Returns a <see cref="CraftableItemSearchProvider"/> when the stream
        /// contains valid seed data, or a <see cref="StaticItemSearchProvider"/>
        /// fallback otherwise.
        /// </summary>
        /// <param name="seedStream">
        /// Stream containing JSON item name seed data, or null if unavailable.
        /// The caller is responsible for disposing the stream after this call.
        /// </param>
        /// <param name="fallbackReason">
        /// Set to null on success, or a diagnostic string when the fallback
        /// provider is returned.
        /// </param>
        /// <param name="seedData">
        /// The parsed seed data, so other services (e.g. metadata fallback)
        /// can reuse it without re-reading the file; null when the fallback
        /// provider is returned.
        /// </param>
        public static IItemSearchProvider Create(
            Stream seedStream, out string fallbackReason, out ItemNameSeedData seedData)
        {
            seedData = null;

            if (seedStream == null)
            {
                fallbackReason = "seed stream is null";
                return new StaticItemSearchProvider();
            }

            try
            {
                var loaded = ItemNameSeedData.Load(seedStream);

                if (loaded.Items.Count == 0)
                {
                    fallbackReason = "seed data contains no items";
                    return new StaticItemSearchProvider();
                }

                fallbackReason = null;
                seedData = loaded;
                return new CraftableItemSearchProvider(loaded);
            }
            catch (Exception ex)
            {
                fallbackReason = ex.Message;
                return new StaticItemSearchProvider();
            }
        }
    }
}
