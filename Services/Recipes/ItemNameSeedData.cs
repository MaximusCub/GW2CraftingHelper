using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GW2CraftingHelper.Services.Recipes
{
    public class ItemNameEntry
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
    }

    public class ItemNameSeedData
    {
        public IReadOnlyList<ItemNameEntry> Items { get; }

        public ItemNameSeedData(IReadOnlyList<ItemNameEntry> items)
        {
            Items = items ?? Array.Empty<ItemNameEntry>();
        }

        public static ItemNameSeedData Load(Stream stream)
        {
            if (stream == null)
            {
                return new ItemNameSeedData(null);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // See VendorOfferLoader.Load for why this reads the UTF-8 bytes
            // directly (via DeserializeAsync, blocked synchronously) instead
            // of StreamReader.ReadToEnd() + Deserialize<string> - this file
            // ships with a leading UTF-8 BOM, which only the stream-based
            // overload strips automatically (M38 WP-08 / perf P2a).
            var entries = JsonSerializer.DeserializeAsync<List<ItemNameEntry>>(stream, options)
                .GetAwaiter().GetResult();
            return new ItemNameSeedData(entries);
        }
    }
}
