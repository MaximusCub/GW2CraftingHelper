using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Contracts
{
    public class StaticItemSearchProvider : IItemSearchProvider
    {
        private static readonly IReadOnlyList<ItemSearchResult> AllItems = new List<ItemSearchResult>
        {
            new ItemSearchResult { ItemId = 46762, Name = "Zojja's Claymore", IconUrl = null },
            new ItemSearchResult { ItemId = 19684, Name = "Mithril Ingot", IconUrl = null }
        };

        public Task<IReadOnlyList<ItemSearchResult>> SearchAsync(
            string query, int maxResults, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            IEnumerable<ItemSearchResult> results = AllItems;

            if (!string.IsNullOrEmpty(query))
            {
                results = results.Where(
                    i => i.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            IReadOnlyList<ItemSearchResult> list = results.Take(maxResults).ToList();
            return Task.FromResult(list);
        }
    }
}
