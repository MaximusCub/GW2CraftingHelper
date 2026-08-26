using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    internal class AccountItemIndex
    {
        public const string SourceMaterialStorage = "MaterialStorage";
        public const string SourceSharedInventory = "SharedInventory";
        public const string SourceBank = "Bank";

        // Character sources are stored as "Character:<name>" (see Gw2AccountSnapshotService).
        // The prefix also guarantees a character named e.g. "Bank" can never collide
        // with a storage-location source key.
        public const string CharacterSourcePrefix = "Character:";

        private static readonly IReadOnlyList<string> EmptySources = Array.Empty<string>();

        // itemId -> source -> count
        private readonly Dictionary<int, Dictionary<string, int>> _index;

        public AccountItemIndex(IReadOnlyList<SnapshotItemEntry> items)
        {
            _index = new Dictionary<int, Dictionary<string, int>>();

            if (items == null)
            {
                return;
            }

            foreach (var entry in items)
            {
                if (entry.Count <= 0)
                {
                    continue;
                }

                string source = entry.Source;
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                if (!_index.TryGetValue(entry.ItemId, out var sourceMap))
                {
                    sourceMap = new Dictionary<string, int>(StringComparer.Ordinal);
                    _index[entry.ItemId] = sourceMap;
                }

                if (sourceMap.TryGetValue(source, out int existing))
                {
                    sourceMap[source] = existing + entry.Count;
                }
                else
                {
                    sourceMap[source] = entry.Count;
                }
            }
        }

        public int GetQuantity(int itemId, string source)
        {
            if (source == null)
            {
                return 0;
            }

            if (_index.TryGetValue(itemId, out var sourceMap) &&
                sourceMap.TryGetValue(source, out int count))
            {
                return count;
            }

            return 0;
        }

        public IReadOnlyList<string> GetSources(int itemId)
        {
            if (_index.TryGetValue(itemId, out var sourceMap))
            {
                var keys = sourceMap.Keys.ToList();
                keys.Sort(StringComparer.Ordinal);
                return keys;
            }

            return EmptySources;
        }

        public static IReadOnlyList<string> GetPrioritizedSources(
            int itemId,
            AccountItemIndex index,
            string activeCharacterName)
        {
            var allSources = index.GetSources(itemId);
            if (allSources.Count == 0)
            {
                return allSources;
            }

            var sourceSet = new HashSet<string>(allSources, StringComparer.Ordinal);
            var result = new List<string>();

            // Priority 1: MaterialStorage
            if (sourceSet.Remove(SourceMaterialStorage))
            {
                result.Add(SourceMaterialStorage);
            }

            // Priority 2: Active character. Callers pass the bare character name;
            // index sources use the "Character:<name>" encoding.
            if (!string.IsNullOrEmpty(activeCharacterName))
            {
                string activeSource = CharacterSourcePrefix + activeCharacterName;
                if (sourceSet.Remove(activeSource))
                {
                    result.Add(activeSource);
                }
            }

            // Priority 3: SharedInventory
            if (sourceSet.Remove(SourceSharedInventory))
            {
                result.Add(SourceSharedInventory);
            }

            // Priority 4: Bank
            if (sourceSet.Remove(SourceBank))
            {
                result.Add(SourceBank);
            }

            // Priority 5: Remaining sources (other characters), sorted
            if (sourceSet.Count > 0)
            {
                var remaining = sourceSet.ToList();
                remaining.Sort(StringComparer.Ordinal);
                result.AddRange(remaining);
            }

            return result;
        }
    }
}
