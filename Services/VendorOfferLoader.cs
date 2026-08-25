using System;
using System.IO;
using System.Text.Json;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// See docs/ARCHITECTURE.md section 9 (data pipeline: seeds, wiki
    /// scrapes, dev-only caches) for where this fits in the offline
    /// seed-generation pipeline.
    /// </summary>
    public class VendorOfferLoader
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        public VendorOfferDataset Load(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // Deserializes directly from the UTF-8 byte stream instead of
            // StreamReader.ReadToEnd() + Deserialize<string> - the old path
            // fully materialized the file as a UTF-16 string (and then
            // System.Text.Json re-encoded that string back to UTF-8
            // internally to parse it), doubling the transient memory/CPU
            // cost on the largest shipped seed file.
            // System.Text.Json 5.0.0 (net461) has no synchronous
            // Deserialize(Stream) overload, only DeserializeAsync(Stream);
            // blocking on it here is safe because Blish's XNA host has no
            // SynchronizationContext to deadlock against (see DO-NOT-TOUCH
            // #2/#12), and the call site remains synchronous by design
            // (P2b - moving load off Initialize - is explicitly excluded).
            return JsonSerializer.DeserializeAsync<VendorOfferDataset>(stream, Options)
                       .GetAwaiter().GetResult()
                   ?? new VendorOfferDataset();
        }

        public string Serialize(VendorOfferDataset dataset)
        {
            return JsonSerializer.Serialize(dataset, Options);
        }
    }
}
