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

        // --- ResolveAmounts ownedCurrencyAmounts (M34-B2b) ---

        [Fact]
        public void ResolveAmounts_OwnedAmountsNull_OwnedQuantityStaysNull()
        {
            var costLines = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 500 } };

            var result = CurrencyDisplayResolver.ResolveAmounts(costLines, null, null);

            Assert.Null(result[0].OwnedQuantity);
        }

        [Fact]
        public void ResolveAmounts_OwnedLessThanNeeded_OwnedQuantityIsRawWalletAmount()
        {
            var costLines = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 500 } };
            var owned = new Dictionary<int, int> { { 23, 200 } };

            var result = CurrencyDisplayResolver.ResolveAmounts(costLines, null, owned);

            Assert.Equal(200, result[0].OwnedQuantity);
        }

        [Fact]
        public void ResolveAmounts_OwnedExceedsNeeded_OwnedQuantityClampedToAmount()
        {
            // Owning MORE than this cost line needs must never surface an
            // "owned" figure bigger than the line's own Amount.
            var costLines = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 500 } };
            var owned = new Dictionary<int, int> { { 23, 999999 } };

            var result = CurrencyDisplayResolver.ResolveAmounts(costLines, null, owned);

            Assert.Equal(500, result[0].OwnedQuantity);
        }

        [Fact]
        public void ResolveAmounts_OwnedAmountsMissingThisCurrencyId_OwnedQuantityStaysNull()
        {
            var costLines = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 500 } };
            var owned = new Dictionary<int, int> { { 2, 100 } }; // different currency id

            var result = CurrencyDisplayResolver.ResolveAmounts(costLines, null, owned);

            Assert.Null(result[0].OwnedQuantity);
        }

        [Fact]
        public void ResolveAmounts_MultipleLines_OwnedQuantityResolvedPerLine()
        {
            var costLines = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 23, Count = 500 },
                new CostLine { Type = "Currency", Id = 2, Count = 1000 }
            };
            var owned = new Dictionary<int, int> { { 23, 100 } }; // only the first line has wallet data

            var result = CurrencyDisplayResolver.ResolveAmounts(costLines, null, owned);

            Assert.Equal(100, result[0].OwnedQuantity);
            Assert.Null(result[1].OwnedQuantity);
        }

        [Fact]
        public void ResolveAmounts_NeverExposesRawCurrencyId()
        {
            // CurrencyAmountViewModel has no id field at all - structural
            // enforcement of the no-displayed-IDs invariant.
            var props = typeof(CurrencyAmountViewModel).GetProperties();
            Assert.DoesNotContain(props, p => p.Name.IndexOf("Id", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // --- ResolveUnitAmounts (M34-B1 #2: winning-offer true per-unit
        // rate, not a truncated total/quantity average) ---

        [Fact]
        public void ResolveUnitAmounts_EvenDivision_ResolvesWholeNumberAmount_NoBundleLabel()
        {
            // A "3 for 3" batch (the exact Obsidian Shard live-repro shape):
            // 3 currency per 3 output units divides evenly to 1 each.
            var perBatch = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 3 } };

            var result = CurrencyDisplayResolver.ResolveUnitAmounts(3, perBatch, null);

            Assert.NotNull(result);
            Assert.Equal(1, result[0].Amount);
            Assert.Null(result[0].BundleLabel);
        }

        [Fact]
        public void ResolveUnitAmounts_UnevenDivision_UsesBundleLabel_NotRoundedAmount()
        {
            // A "2 for 3" batch does not divide evenly - the true per-unit
            // rate is not a whole number, so the resolver must not invent a
            // rounded figure; it carries the literal bundle text instead.
            var perBatch = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 2 } };

            var result = CurrencyDisplayResolver.ResolveUnitAmounts(3, perBatch, null);

            Assert.NotNull(result);
            Assert.Equal(0, result[0].Amount);
            Assert.Equal("2 for 3", result[0].BundleLabel);
        }

        [Fact]
        public void ResolveUnitAmounts_NeverTruncatesAggregateTotal()
        {
            // Regression guard for the pre-fix bug: a merged row's
            // aggregated total (186) and quantity (179) must play NO part
            // in the Each computation at all - only the winning offer's own
            // per-batch rate (3-for-3 = 1 each) matters, never
            // 186/179 (which truncates to a misleading "1" too, but for the
            // wrong reason and would differ for other aggregate shapes).
            var perBatch = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 3 } };

            var result = CurrencyDisplayResolver.ResolveUnitAmounts(3, perBatch, null);

            Assert.Equal(1, result[0].Amount);
        }

        [Fact]
        public void ResolveUnitAmounts_ZeroOutputCount_ReturnsNull_NoDivideByZero()
        {
            var perBatch = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 3 } };

            Assert.Null(CurrencyDisplayResolver.ResolveUnitAmounts(0, perBatch, null));
        }

        [Fact]
        public void ResolveUnitAmounts_NegativeOutputCount_ReturnsNull()
        {
            var perBatch = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 3 } };

            Assert.Null(CurrencyDisplayResolver.ResolveUnitAmounts(-1, perBatch, null));
        }

        [Fact]
        public void ResolveUnitAmounts_NullCostLines_ReturnsNull()
        {
            // Covers both a non-vendor row and a vendor row whose tree
            // occurrences resolved to more than one distinct offer (M34-B1
            // #1's Conflict case) - PlanStep leaves this null in both cases.
            Assert.Null(CurrencyDisplayResolver.ResolveUnitAmounts(3, null, null));
        }

        [Fact]
        public void ResolveUnitAmounts_EmptyCostLines_ReturnsNull()
        {
            Assert.Null(CurrencyDisplayResolver.ResolveUnitAmounts(3, new List<CostLine>(), null));
        }
    }
}
