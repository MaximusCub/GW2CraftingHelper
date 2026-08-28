using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Converts a CurrencyValuation to/from the JSON string persisted by
    /// ModuleSettings. Kept separate from ModuleSettings (which references
    /// Blish_HUD.Settings and cannot be unit tested per repo invariant) so
    /// the actual conversion logic is covered by a real, Blish-free test.
    ///
    /// The persisted shape gained a
    /// "Cleared" array alongside the pre-existing "Values" map (see
    /// PersistedModel below) - a currency the user explicitly cleared of
    /// CurrencyDecisionDefaults' curated default must stay unvalued forever,
    /// not just until the next Deserialize invents one from the default
    /// table (CurrencyValuation.TryGetEffectiveCopperValue). It later
    /// gained "ItemValues"/"ItemCleared", the barter-item twins of that
    /// pair. Every addition has been additive on purpose: Deserialize still
    /// reads the OLD pre-Feature-1 flat-dict shape (a bare {"2":100,...}
    /// object, no "Values"/"Cleared" properties), and a settings value
    /// written before the item tables existed simply has no item entries -
    /// there is no migration step at any point in that chain.
    /// </summary>
    internal static class CurrencyValuationSerializer
    {
        private class PersistedModel
        {
            public Dictionary<int, long> Values { get; set; }

            public List<int> Cleared { get; set; }

            public Dictionary<int, long> ItemValues { get; set; }

            public List<int> ItemCleared { get; set; }
        }

        /// <summary>
        /// Serializes the valuation's entries to JSON. Returns an empty
        /// string when there is nothing at all to persist (no explicit
        /// values and no cleared ids, currency or item).
        /// </summary>
        internal static string Serialize(CurrencyValuation valuation)
        {
            if (valuation == null ||
                (valuation.CopperPerUnit.Count == 0 && valuation.ClearedCurrencyIds.Count == 0 &&
                 valuation.ItemCopperPerUnit.Count == 0 && valuation.ClearedItemIds.Count == 0))
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

            // ClearedCurrencyIds
            // is a HashSet<int>, whose enumeration order is not guaranteed
            // stable across otherwise-identical instances - sorted here so
            // the persisted "Cleared" array (and therefore the whole
            // persisted string) is deterministic/diffable across saves that
            // clear the same set of currencies.
            var cleared = new List<int>(valuation.ClearedCurrencyIds);
            cleared.Sort();

            var itemValues = new Dictionary<int, long>(valuation.ItemCopperPerUnit.Count);
            foreach (var kvp in valuation.ItemCopperPerUnit)
            {
                itemValues[kvp.Key] = kvp.Value;
            }

            var itemCleared = new List<int>(valuation.ClearedItemIds);
            itemCleared.Sort();
            var model = new PersistedModel
            {
                Values = values,
                Cleared = cleared,
                ItemValues = itemValues,
                ItemCleared = itemCleared,
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
                if (obj.Property("Values") != null || obj.Property("Cleared") != null ||
                    obj.Property("ItemValues") != null || obj.Property("ItemCleared") != null)
                {
                    var model = obj.ToObject<PersistedModel>();
                    return BuildValuation(model?.Values, model?.Cleared, model?.ItemValues, model?.ItemCleared);
                }

                var flat = obj.ToObject<Dictionary<int, long>>();
                return BuildValuation(flat, null, null, null);
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
            Dictionary<int, long> rawValues,
            List<int> rawCleared,
            Dictionary<int, long> rawItemValues,
            List<int> rawItemCleared)
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

            // Item side: same skip-the-bad-entry posture as the currency
            // side above, minus the coin-id guard (no item id names the
            // coin currency).
            var validItems = new Dictionary<int, long>();
            if (rawItemValues != null)
            {
                foreach (var kvp in rawItemValues)
                {
                    if (kvp.Value <= 0)
                    {
                        continue;
                    }

                    validItems[kvp.Key] = kvp.Value;
                }
            }

            var validItemCleared = new List<int>();
            if (rawItemCleared != null)
            {
                foreach (int itemId in rawItemCleared)
                {
                    if (validItems.ContainsKey(itemId))
                    {
                        continue;
                    }

                    validItemCleared.Add(itemId);
                }
            }

            return valid.Count == 0 && validCleared.Count == 0 &&
                   validItems.Count == 0 && validItemCleared.Count == 0
                ? CurrencyValuation.None
                : new CurrencyValuation(valid, validCleared, validItems, validItemCleared);
        }
    }
}
