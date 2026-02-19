using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class AccountItemIndex
    {
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

                string source = entry.Source ?? "";

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
                return sourceMap.Keys.ToList();
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
            if (sourceSet.Remove("MaterialStorage"))
            {
                result.Add("MaterialStorage");
            }

            // Priority 2: Active character
            if (!string.IsNullOrEmpty(activeCharacterName) &&
                sourceSet.Remove(activeCharacterName))
            {
                result.Add(activeCharacterName);
            }

            // Priority 3: SharedInventory
            if (sourceSet.Remove("SharedInventory"))
            {
                result.Add("SharedInventory");
            }

            // Priority 4: Bank
            if (sourceSet.Remove("Bank"))
            {
                result.Add("Bank");
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
