using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class VendorOfferHasherTests
    {
        [Fact]
        public void SameInputs_ProduceSameHash()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };
            var locations = new List<string> { "Lion's Arch" };

            string hash1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", locations, null, null);
            string hash2 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", locations, null, null);

            Assert.Equal(hash1, hash2);
            Assert.Equal(64, hash1.Length);
        }

        [Fact]
        public void DifferentOutputItemId_ProducesDifferentHash()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hash1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, null);
            string hash2 = VendorOfferHasher.ComputeOfferId(
                19686, 1, costs, "Merchant", new List<string>(), null, null);

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void DifferentOutputCount_ProducesDifferentHash()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hash1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, null);
            string hash2 = VendorOfferHasher.ComputeOfferId(
                19685, 5, costs, "Merchant", new List<string>(), null, null);

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void CostLineOrder_DoesNotAffectHash()
        {
            var costs1 = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 },
                new CostLine { Type = "Item", Id = 50, Count = 3 }
            };
            var costs2 = new List<CostLine>
            {
                new CostLine { Type = "Item", Id = 50, Count = 3 },
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hash1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs1, "Merchant", new List<string>(), null, null);
            string hash2 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs2, "Merchant", new List<string>(), null, null);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void LocationOrder_DoesNotAffectHash()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hash1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant",
                new List<string> { "Lion's Arch", "Divinity's Reach" },
                null, null);
            string hash2 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant",
                new List<string> { "Divinity's Reach", "Lion's Arch" },
                null, null);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void NullLocations_HandledGracefully()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hash1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", null, null, null);
            string hash2 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, null);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void NullCostLines_HandledGracefully()
        {
            string hash1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, null, "Merchant", new List<string>(), null, null);
            string hash2 = VendorOfferHasher.ComputeOfferId(
                19685, 1, new List<CostLine>(), "Merchant", new List<string>(), null, null);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void DifferentCaps_ProduceDifferentHashes()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hashNoCap = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, null);
            string hashDailyCap = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), 5, null);
            string hashWeeklyCap = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, 10);

            Assert.NotEqual(hashNoCap, hashDailyCap);
            Assert.NotEqual(hashNoCap, hashWeeklyCap);
            Assert.NotEqual(hashDailyCap, hashWeeklyCap);
        }

        [Fact]
        public void DifferentMerchant_ProducesDifferentHash()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hash1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant A", new List<string>(), null, null);
            string hash2 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant B", new List<string>(), null, null);

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void HashIsLowercaseHex()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hash = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, null);

            Assert.Matches("^[0-9a-f]{64}$", hash);
        }
    }
}
