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
                PropertyNameCaseInsensitive = true,
            };

            // See VendorOfferLoader.Load for why this reads the UTF-8 bytes
            // directly (via DeserializeAsync, blocked synchronously) instead
            // of StreamReader.ReadToEnd() + Deserialize<string>: it avoids
            // ReadToEnd's full UTF-16 string materialization (and the
            // internal UTF-8 re-encoding System.Text.Json performs to parse
            // a string) on this seed file.
            // System.Text.Json 5.0.0 (net461) has no synchronous
            // Deserialize(Stream) overload. The File.ReadAllBytes() +
            // Deserialize<T>(ReadOnlySpan<byte>) sync overload was also
            // rejected: this file ships with a leading UTF-8 BOM, and
            // (verified empirically) the span-based overload throws
            // JsonException on a leading BOM instead of skipping it, unlike
            // the stream-based overloads.
            var entries = JsonSerializer.DeserializeAsync<List<ItemNameEntry>>(stream, options)
                .GetAwaiter().GetResult();
            return new ItemNameSeedData(entries);
        }
    }
}
