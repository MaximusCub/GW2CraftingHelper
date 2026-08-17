using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Direct
    /// unit tests on SeasonalOfferFilter.ExcludeSeasonal - the solver's
    /// offer set must unconditionally drop any offer with a non-null
    /// SeasonalFestival, regardless of festival-active state (this filter
    /// has no concept of "active" at all - see its own doc comment).
    /// </summary>
    public class SeasonalOfferFilterTests
    {
        [Fact]
        public void NullOffers_ReturnsNullUnchanged()
        {
            Assert.Null(SeasonalOfferFilter.ExcludeSeasonal(null));
        }

        [Fact]
        public void NoSeasonalOffersAnywhere_ReturnsSameReference()
        {
            var offers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { new VendorOffer { OutputItemId = 1 } } }
            };

            var result = SeasonalOfferFilter.ExcludeSeasonal(offers);

            Assert.Same(offers, result);
        }

        [Fact]
        public void SeasonalOffer_ExcludedFromItsList()
        {
            var regular = new VendorOffer { OutputItemId = 1, SeasonalFestival = null };
            var seasonal = new VendorOffer { OutputItemId = 1, SeasonalFestival = "halloween" };
            var offers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { regular, seasonal } }
            };

            var result = SeasonalOfferFilter.ExcludeSeasonal(offers);

            Assert.Single(result[1]);
            Assert.Same(regular, result[1][0]);
        }

        [Fact]
        public void UnaffectedItemKeys_ReuseOriginalListReference()
        {
            var seasonalList = new List<VendorOffer>
            {
                new VendorOffer { OutputItemId = 1, SeasonalFestival = "halloween" }
            };
            var unaffectedList = new List<VendorOffer> { new VendorOffer { OutputItemId = 2 } };
            var offers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, seasonalList },
                { 2, unaffectedList }
            };

            var result = SeasonalOfferFilter.ExcludeSeasonal(offers);

            Assert.Same(unaffectedList, result[2]);
            Assert.Empty(result[1]);
        }

        [Fact]
        public void EmptyStringFestival_TreatedAsNotSeasonal()
        {
            var offer = new VendorOffer { OutputItemId = 1, SeasonalFestival = "" };
            var offers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };

            var result = SeasonalOfferFilter.ExcludeSeasonal(offers);

            Assert.Same(offers, result);
        }
    }
}
