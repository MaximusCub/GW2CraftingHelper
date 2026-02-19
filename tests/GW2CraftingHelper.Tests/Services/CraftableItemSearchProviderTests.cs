using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Recipes;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftableItemSearchProviderTests
    {
        private static ItemNameSeedData Seed(params (int id, string name, string icon)[] items)
        {
            var entries = items.Select(i => new ItemNameEntry
            {
                Id = i.id,
                Name = i.name,
                Icon = i.icon
            }).ToList();
            return new ItemNameSeedData(entries);
        }

        [Fact]
        public async Task EmptyQuery_ReturnsAllUpToMaxResults()
        {
            var seed = Seed(
                (1, "Alpha", "a.png"),
                (2, "Beta", "b.png"),
                (3, "Gamma", "g.png"));
            var provider = new CraftableItemSearchProvider(seed);

            var results = await provider.SearchAsync("", 2, CancellationToken.None);

            Assert.Equal(2, results.Count);
            Assert.Equal("Alpha", results[0].Name);
            Assert.Equal("Beta", results[1].Name);
        }

        [Fact]
        public async Task PrefixMatch_RanksAboveSubstring()
        {
            var seed = Seed(
                (1, "Iron Ingot", "i.png"),
                (2, "Mithril Iron Bar", "m.png"),
                (3, "Iron Ore", "o.png"));
            var provider = new CraftableItemSearchProvider(seed);

            var results = await provider.SearchAsync("Iron", 10, CancellationToken.None);

            // Prefix matches first (Iron Ingot, Iron Ore), then substring (Mithril Iron Bar)
            Assert.Equal(3, results.Count);
            Assert.True(results[0].Name.StartsWith("Iron", StringComparison.OrdinalIgnoreCase));
            Assert.True(results[1].Name.StartsWith("Iron", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Mithril Iron Bar", results[2].Name);
        }

        [Fact]
        public async Task CaseInsensitive_SearchWorks()
        {
            var seed = Seed((1, "Bolt of Silk", "s.png"));
            var provider = new CraftableItemSearchProvider(seed);

            var results = await provider.SearchAsync("bolt", 10, CancellationToken.None);

            Assert.Single(results);
            Assert.Equal("Bolt of Silk", results[0].Name);
        }

        [Fact]
        public async Task MaxResults_LimitsOutput()
        {
            var seed = Seed(
                (1, "Alpha", null),
                (2, "Beta", null),
                (3, "Gamma", null),
                (4, "Delta", null));
            var provider = new CraftableItemSearchProvider(seed);

            var results = await provider.SearchAsync("", 2, CancellationToken.None);

            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task NoMatch_ReturnsEmpty()
        {
            var seed = Seed((1, "Copper Ore", "c.png"));
            var provider = new CraftableItemSearchProvider(seed);

            var results = await provider.SearchAsync("Zzzzz", 10, CancellationToken.None);

            Assert.Empty(results);
        }

        [Fact]
        public async Task EmptySeedData_ReturnsEmpty()
        {
            var provider = new CraftableItemSearchProvider(new ItemNameSeedData(null));

            var results = await provider.SearchAsync("", 10, CancellationToken.None);

            Assert.Empty(results);
        }

        [Fact]
        public async Task NullSeedData_ReturnsEmpty()
        {
            var provider = new CraftableItemSearchProvider(null);

            var results = await provider.SearchAsync("test", 10, CancellationToken.None);

            Assert.Empty(results);
        }

        [Fact]
        public async Task SingleCharQuery_Works()
        {
            var seed = Seed(
                (1, "Alpha", null),
                (2, "Zeta", null));
            var provider = new CraftableItemSearchProvider(seed);

            var results = await provider.SearchAsync("Z", 10, CancellationToken.None);

            Assert.Single(results);
            Assert.Equal("Zeta", results[0].Name);
        }

        [Fact]
        public async Task Results_SortedAlphabetically_WithinGroups()
        {
            var seed = Seed(
                (1, "Copper Ore", null),
                (2, "Copper Ingot", null),
                (3, "Fancy Copper Ring", null));
            var provider = new CraftableItemSearchProvider(seed);

            var results = await provider.SearchAsync("Copper", 10, CancellationToken.None);

            // Prefix matches sorted alphabetically
            Assert.Equal("Copper Ingot", results[0].Name);
            Assert.Equal("Copper Ore", results[1].Name);
            // Substring match
            Assert.Equal("Fancy Copper Ring", results[2].Name);
        }

        [Fact]
        public async Task AllResults_HaveIsPlanTarget_True()
        {
            var seed = Seed(
                (1, "Alpha", null),
                (2, "Beta", null));
            var provider = new CraftableItemSearchProvider(seed);

            var results = await provider.SearchAsync("", 10, CancellationToken.None);

            Assert.All(results, r => Assert.True(r.IsPlanTarget));
        }

        [Fact]
        public async Task WhitespaceQuery_Trimmed()
        {
            var seed = Seed((1, "Iron Ore", null));
            var provider = new CraftableItemSearchProvider(seed);

            var results = await provider.SearchAsync("  Iron  ", 10, CancellationToken.None);

            Assert.Single(results);
            Assert.Equal("Iron Ore", results[0].Name);
        }

        [Fact]
        public async Task NullQuery_TreatedAsEmpty()
        {
            var seed = Seed(
                (1, "Alpha", null),
                (2, "Beta", null));
            var provider = new CraftableItemSearchProvider(seed);

            var results = await provider.SearchAsync(null, 10, CancellationToken.None);

            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task IconUrl_PassedThrough()
        {
            var seed = Seed((1, "Test Item", "https://render.guildwars2.com/test.png"));
            var provider = new CraftableItemSearchProvider(seed);

            var results = await provider.SearchAsync("", 10, CancellationToken.None);

            Assert.Single(results);
            Assert.Equal("https://render.guildwars2.com/test.png", results[0].IconUrl);
        }
    }
}
