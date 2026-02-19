using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Services.Recipes;

namespace GW2CraftingHelper.Services
{
    public class CraftableItemSearchProvider : IItemSearchProvider
    {
        private readonly List<ItemNameEntry> _entries;

        public CraftableItemSearchProvider(ItemNameSeedData seedData)
        {
            if (seedData == null || seedData.Items == null || seedData.Items.Count == 0)
            {
                _entries = new List<ItemNameEntry>();
                return;
            }

            _entries = seedData.Items
                .Where(e => e != null && !string.IsNullOrEmpty(e.Name))
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public Task<IReadOnlyList<ItemSearchResult>> SearchAsync(
            string query, int maxResults, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (maxResults <= 0)
            {
                IReadOnlyList<ItemSearchResult> empty = Array.Empty<ItemSearchResult>();
                return Task.FromResult(empty);
            }

            string trimmed = (query ?? "").Trim();

            IReadOnlyList<ItemSearchResult> results;
            if (string.IsNullOrEmpty(trimmed))
            {
                results = _entries
                    .Take(maxResults)
                    .Select(ToResult)
                    .ToList();
            }
            else
            {
                var prefixMatches = new List<ItemNameEntry>();
                var substringMatches = new List<ItemNameEntry>();

                foreach (var entry in _entries)
                {
                    if (entry.Name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        prefixMatches.Add(entry);
                    }
                    else if (entry.Name.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        substringMatches.Add(entry);
                    }
                }

                results = prefixMatches
                    .Concat(substringMatches)
                    .Take(maxResults)
                    .Select(ToResult)
                    .ToList();
            }

            return Task.FromResult(results);
        }

        private static ItemSearchResult ToResult(ItemNameEntry entry)
        {
            return new ItemSearchResult
            {
                ItemId = entry.Id,
                Name = entry.Name,
                IconUrl = entry.Icon,
                IsPlanTarget = true
            };
        }
    }
}
