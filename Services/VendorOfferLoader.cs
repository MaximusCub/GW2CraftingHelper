using System;
using System.IO;
using System.Text.Json;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class VendorOfferLoader
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public VendorOfferDataset Load(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                return JsonSerializer.Deserialize<VendorOfferDataset>(json, Options)
                       ?? new VendorOfferDataset();
            }
        }

        public string Serialize(VendorOfferDataset dataset)
        {
            return JsonSerializer.Serialize(dataset, Options);
        }
    }
}
