using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Services.Recipes;

namespace GW2CraftingHelper.Services
{
    internal class CraftableItemSearchProvider : IItemSearchProvider
    {
        private readonly List<CachedEntry> _entries;

        public CraftableItemSearchProvider(ItemNameSeedData seedData)
        {
            if (seedData == null || seedData.Items == null || seedData.Items.Count == 0)
            {
                _entries = new List<CachedEntry>();
                return;
            }

            _entries = seedData.Items
                .Where(e => e != null && !string.IsNullOrEmpty(e.Name))
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => new CachedEntry
                {
                    Id = e.Id,
                    Name = e.Name,
                    NameLower = e.Name.ToLowerInvariant(),
                    Icon = e.Icon,
                })
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
                string queryLower = trimmed.ToLowerInvariant();
                var prefixMatches = new List<CachedEntry>();
                var substringMatches = new List<CachedEntry>();
                bool seenPrefix = false;
                int remaining = maxResults;

                foreach (var entry in _entries)
                {
                    if (entry.NameLower.StartsWith(queryLower, StringComparison.Ordinal))
                    {
                        prefixMatches.Add(entry);
                        seenPrefix = true;
                    }
                    else
                    {
                        // Entries are sorted case-insensitively, so prefix
                        // matches are contiguous. Once we leave that region,
                        // recalculate how many substring matches we still need.
                        if (seenPrefix)
                        {
                            seenPrefix = false;
                            remaining = maxResults - prefixMatches.Count;
                            if (remaining <= 0)
                            {
                                break;
                            }
                        }

                        if (entry.NameLower.IndexOf(queryLower, StringComparison.Ordinal) >= 0)
                        {
                            substringMatches.Add(entry);
                            if (substringMatches.Count >= remaining)
                            {
                                break;
                            }
                        }
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

        private static ItemSearchResult ToResult(CachedEntry entry)
        {
            return new ItemSearchResult
            {
                ItemId = entry.Id,
                Name = entry.Name,
                IconUrl = entry.Icon,
                IsPlanTarget = true,
            };
        }

        private struct CachedEntry
        {
            public int Id;
            public string Name;
            public string NameLower;
            public string Icon;
        }
    }
}
