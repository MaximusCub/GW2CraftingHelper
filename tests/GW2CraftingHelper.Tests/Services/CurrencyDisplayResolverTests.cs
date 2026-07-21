using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CurrencyDisplayResolverTests
    {
        // --- ResolveName ---

        [Fact]
        public void ResolveName_MetadataPresent_PrefersMetadataName()
        {
            var metadata = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, Name = "Spirit Shard (Live)", IconUrl = "s.png" }
            };

            Assert.Equal("Spirit Shard (Live)", CurrencyDisplayResolver.ResolveName(23, metadata));
        }

        [Fact]
        public void ResolveName_MetadataNull_FallsBackToGw2Constants()
        {
            Assert.Equal("Spirit Shards", CurrencyDisplayResolver.ResolveName(23, null));
        }

        [Fact]
        public void ResolveName_IdMissingFromMetadata_FallsBackToGw2Constants()
        {
            var metadata = new Dictionary<int, CurrencyMetadata>
            {
                [2] = new CurrencyMetadata { CurrencyId = 2, Name = "Karma" }
            };

            Assert.Equal("Spirit Shards", CurrencyDisplayResolver.ResolveName(23, metadata));
        }

        [Fact]
        public void ResolveName_EmptyNameInMetadata_FallsBackToGw2Constants()
        {
            var metadata = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, Name = "" }
            };

            Assert.Equal("Spirit Shards", CurrencyDisplayResolver.ResolveName(23, metadata));
        }

        [Fact]
        public void ResolveName_UnknownId_FallsBackToGenericCurrency_NoIdDisplayed()
        {
            var name = CurrencyDisplayResolver.ResolveName(99999, null);

            Assert.Equal("Currency", name);
            Assert.DoesNotContain("99999", name);
        }

        // --- ResolveIconUrl ---

        [Fact]
        public void ResolveIconUrl_MetadataPresent_ReturnsIcon()
        {
            var metadata = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, Name = "Spirit Shards", IconUrl = "spirit_shard.png" }
            };

            Assert.Equal("spirit_shard.png", CurrencyDisplayResolver.ResolveIconUrl(23, metadata));
        }

        [Fact]
        public void ResolveIconUrl_MetadataNull_ReturnsNull()
        {
            Assert.Null(CurrencyDisplayResolver.ResolveIconUrl(23, null));
        }

        [Fact]
        public void ResolveIconUrl_IdMissingFromMetadata_ReturnsNull()
        {
            var metadata = new Dictionary<int, CurrencyMetadata>
            {
                [2] = new CurrencyMetadata { CurrencyId = 2, IconUrl = "karma.png" }
            };

            Assert.Null(CurrencyDisplayResolver.ResolveIconUrl(23, metadata));
        }

        [Fact]
        public void ResolveIconUrl_EmptyIconInMetadata_ReturnsNull()
        {
            var metadata = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, IconUrl = "" }
            };

            Assert.Null(CurrencyDisplayResolver.ResolveIconUrl(23, metadata));
        }

        // --- ResolveAmounts ---

        [Fact]
        public void ResolveAmounts_Null_ReturnsNull()
        {
            Assert.Null(CurrencyDisplayResolver.ResolveAmounts(null, null));
        }

        [Fact]
        public void ResolveAmounts_Empty_ReturnsNull()
        {
            Assert.Null(CurrencyDisplayResolver.ResolveAmounts(new List<CostLine>(), null));
        }

        [Fact]
        public void ResolveAmounts_SingleLine_ResolvesNameIconAmount()
        {
            var metadata = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, Name = "Spirit Shards", IconUrl = "s.png" }
            };
            var costLines = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 500 } };

            var result = CurrencyDisplayResolver.ResolveAmounts(costLines, metadata);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(500, result[0].Amount);
            Assert.Equal("Spirit Shards", result[0].Name);
            Assert.Equal("s.png", result[0].IconUrl);
        }

        [Fact]
        public void ResolveAmounts_MultipleLines_ResolvesEachIndependently()
        {
            var metadata = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, Name = "Spirit Shards", IconUrl = "s.png" }
            };
            var costLines = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 23, Count = 50 },
                new CostLine { Type = "Currency", Id = 2, Count = 1000 }
            };

            var result = CurrencyDisplayResolver.ResolveAmounts(costLines, metadata);

            Assert.Equal(2, result.Count);
            Assert.Equal("Spirit Shards", result[0].Name);
            Assert.Equal("s.png", result[0].IconUrl);
            Assert.Equal("Karma", result[1].Name); // Gw2Constants fallback, no metadata entry for id 2
            Assert.Null(result[1].IconUrl);
        }

        [Fact]
        public void ResolveAmounts_NeverExposesRawCurrencyId()
        {
            // CurrencyAmountViewModel has no id field at all - structural
            // enforcement of the no-displayed-IDs invariant.
            var props = typeof(CurrencyAmountViewModel).GetProperties();
            Assert.DoesNotContain(props, p => p.Name.IndexOf("Id", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // --- ResolveUnitAmounts ---

        [Fact]
        public void ResolveUnitAmounts_DividesByQuantity()
        {
            var costLines = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 500 } };

            var result = CurrencyDisplayResolver.ResolveUnitAmounts(costLines, 5, null);

            Assert.NotNull(result);
            Assert.Equal(100, result[0].Amount);
        }

        [Fact]
        public void ResolveUnitAmounts_TruncatesLikeUnitCost()
        {
            // 500 / 3 = 166 (integer division truncates), matching
            // PlanSolver.AggregateStep's UnitCost = TotalCost / Quantity.
            var costLines = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 500 } };

            var result = CurrencyDisplayResolver.ResolveUnitAmounts(costLines, 3, null);

            Assert.Equal(166, result[0].Amount);
        }

        [Fact]
        public void ResolveUnitAmounts_ZeroQuantity_ReturnsNull_NoDivideByZero()
        {
            var costLines = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 500 } };

            Assert.Null(CurrencyDisplayResolver.ResolveUnitAmounts(costLines, 0, null));
        }

        [Fact]
        public void ResolveUnitAmounts_NegativeQuantity_ReturnsNull()
        {
            var costLines = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 500 } };

            Assert.Null(CurrencyDisplayResolver.ResolveUnitAmounts(costLines, -1, null));
        }

        [Fact]
        public void ResolveUnitAmounts_NullCostLines_ReturnsNull()
        {
            Assert.Null(CurrencyDisplayResolver.ResolveUnitAmounts(null, 5, null));
        }
    }
}
