using System.Collections.Generic;
using GW2CraftingHelper.Models;
using Newtonsoft.Json;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Converts a CurrencyValuation to/from the JSON string persisted by
    /// ModuleSettings. Kept separate from ModuleSettings (which references
    /// Blish_HUD.Settings and cannot be unit tested per repo invariant) so
    /// the actual conversion logic is covered by a real, Blish-free test.
    /// </summary>
    internal static class CurrencyValuationSerializer
    {
        /// <summary>
        /// Serializes the valuation's entries to JSON. Returns an empty
        /// string for null or empty valuations (nothing to persist).
        /// </summary>
        internal static string Serialize(CurrencyValuation valuation)
        {
            if (valuation == null || valuation.CopperPerUnit.Count == 0)
            {
                return string.Empty;
            }

            return JsonConvert.SerializeObject(valuation.CopperPerUnit);
        }

        /// <summary>
        /// Deserializes a previously-persisted JSON string back into a
        /// CurrencyValuation. Returns CurrencyValuation.None for null,
        /// blank, or malformed input rather than throwing - a corrupt
        /// settings value must never crash plan generation.
        /// </summary>
        internal static CurrencyValuation Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return CurrencyValuation.None;
            }

            try
            {
                var entries = JsonConvert.DeserializeObject<Dictionary<int, long>>(json);
                return entries == null || entries.Count == 0
                    ? CurrencyValuation.None
                    : new CurrencyValuation(entries);
            }
            catch (JsonException)
            {
                return CurrencyValuation.None;
            }
        }
    }
}
