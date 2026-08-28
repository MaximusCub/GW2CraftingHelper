using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Contracts;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Temporary default <see cref="IItemSearchProvider"/> that returns a
    /// hardcoded list of known plan targets. This is a development placeholder
    /// and will be replaced by a real craftable-item index backed by GW2 API
    /// data once Lane 2 implements the search provider.
    /// </summary>
    internal class StaticItemSearchProvider : IItemSearchProvider
    {
        private static readonly IReadOnlyList<ItemSearchResult> AllItems = new List<ItemSearchResult>
        {
            new ItemSearchResult { ItemId = 46762, Name = "Zojja's Claymore", IconUrl = null, IsPlanTarget = true },
            new ItemSearchResult { ItemId = 19684, Name = "Mithril Ingot", IconUrl = null, IsPlanTarget = true },
        };

        /// <inheritdoc />
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
