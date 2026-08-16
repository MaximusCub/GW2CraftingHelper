using System.Collections.Generic;
using GW2CraftingHelper.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Converts a CurrencyValuation to/from the JSON string persisted by
    /// ModuleSettings. Kept separate from ModuleSettings (which references
    /// Blish_HUD.Settings and cannot be unit tested per repo invariant) so
    /// the actual conversion logic is covered by a real, Blish-free test.
    ///
    /// currency-ux-package (Feature 1): the persisted shape gained a
    /// "Cleared" array alongside the pre-existing "Values" map (see
    /// PersistedModel below) - a currency the user explicitly cleared of
    /// CurrencyDecisionDefaults' curated default must stay unvalued forever,
    /// not just until the next Deserialize invents one from the default
    /// table (CurrencyValuation.TryGetEffectiveCopperValue). Deserialize
    /// still reads the OLD pre-Feature-1 flat-dict shape (a bare
    /// {"2":100,...} object, no "Values"/"Cleared" properties) so an
    /// already-persisted settings value from before this feature keeps
    /// working with no migration step.
    /// </summary>
    internal static class CurrencyValuationSerializer
    {
        private class PersistedModel
        {
            public Dictionary<int, long> Values { get; set; }
            public List<int> Cleared { get; set; }
        }

        /// <summary>
        /// Serializes the valuation's entries to JSON. Returns an empty
        /// string when there is nothing at all to persist (no explicit
        /// values and no cleared currencies).
        /// </summary>
        internal static string Serialize(CurrencyValuation valuation)
        {
            if (valuation == null ||
                (valuation.CopperPerUnit.Count == 0 && valuation.ClearedCurrencyIds.Count == 0))
            {
                return string.Empty;
            }

            // .NET Framework 4.8's Dictionary<TKey,TValue> has no
            // constructor overload accepting IReadOnlyDictionary<TKey,
            // TValue> (only IDictionary<TKey,TValue>) - CopperPerUnit is
            // exposed as the former, so this is a manual copy rather than
            // a one-line constructor call.
            var values = new Dictionary<int, long>(valuation.CopperPerUnit.Count);
            foreach (var kvp in valuation.CopperPerUnit)
            {
                values[kvp.Key] = kvp.Value;
            }
            var model = new PersistedModel
            {
                Values = values,
                Cleared = new List<int>(valuation.ClearedCurrencyIds)
            };
            return JsonConvert.SerializeObject(model);
        }

        /// <summary>
        /// Deserializes a previously-persisted JSON string back into a
        /// CurrencyValuation. Returns CurrencyValuation.None for null,
        /// blank, or malformed input rather than throwing - a corrupt
        /// settings value must never crash plan generation. Entries with a
        /// non-positive copper-per-unit rate or keyed on the coin currency
        /// id are individually SKIPPED (CurrencyValuation's constructor
        /// rejects them outright) rather than discarding the whole
        /// valuation - a single bad entry, however it got into settings,
        /// must not silently drop every other currency the user priced.
        /// </summary>
        internal static CurrencyValuation Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return CurrencyValuation.None;
            }

            try
            {
                var token = JToken.Parse(json);
                if (token.Type != JTokenType.Object)
                {
                    return CurrencyValuation.None;
                }

                var obj = (JObject)token;
                // New shape ({"Values":{...},"Cleared":[...]}) is detected
                // by the presence of either property; an old-format flat
                // dict ({"2":100,...}) has neither (its own keys are
                // currency ids, never the literal strings "Values"/
                // "Cleared").
                if (obj.Property("Values") != null || obj.Property("Cleared") != null)
                {
                    var model = obj.ToObject<PersistedModel>();
                    return BuildValuation(model?.Values, model?.Cleared);
                }

                var flat = obj.ToObject<Dictionary<int, long>>();
                return BuildValuation(flat, null);
            }
            catch (JsonException)
            {
                return CurrencyValuation.None;
            }
        }

        /// <summary>
        /// Resolves a possibly-overlapping (value, cleared) pair read from
        /// persisted JSON into the non-overlapping sets
        /// CurrencyValuation's constructor requires (it fails loud on
        /// overlap by design - see that constructor's own doc comment): an
        /// explicit positive value for a currency id always wins over that
        /// same id also appearing in "Cleared", exactly like
        /// SettingsTabContent.SaveValuations resolves the same conflict at
        /// the UI layer.
        /// </summary>
        private static CurrencyValuation BuildValuation(
            Dictionary<int, long> rawValues, List<int> rawCleared)
        {
            var valid = new Dictionary<int, long>();
            if (rawValues != null)
            {
                foreach (var kvp in rawValues)
                {
                    if (kvp.Key == Gw2Constants.CoinCurrencyId || kvp.Value <= 0)
                    {
                        continue;
                    }
                    valid[kvp.Key] = kvp.Value;
                }
            }

            var validCleared = new List<int>();
            if (rawCleared != null)
            {
                foreach (int currencyId in rawCleared)
                {
                    if (currencyId == Gw2Constants.CoinCurrencyId || valid.ContainsKey(currencyId))
                    {
                        continue;
                    }
                    validCleared.Add(currencyId);
                }
            }

            return valid.Count == 0 && validCleared.Count == 0
                ? CurrencyValuation.None
                : new CurrencyValuation(valid, validCleared);
        }
    }
}
