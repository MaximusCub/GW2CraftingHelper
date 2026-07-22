using System.Collections.Generic;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using Xunit;

namespace VendorOfferUpdater.Tests
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
        public void NullCostLines_EqualsEmptyList()
        {
            string hash1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, null, "Merchant", new List<string>(), null, null);
            string hash2 = VendorOfferHasher.ComputeOfferId(
                19685, 1, new List<CostLine>(), "Merchant", new List<string>(), null, null);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void NullLocations_EqualsEmptyList()
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
        public void NullMerchant_TreatedAsEmpty()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hash1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, null, new List<string>(), null, null);
            string hash2 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "", new List<string>(), null, null);

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
        public void SameCapValues_ProduceSameHash()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hash1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), 5, 10);
            string hash2 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), 5, 10);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void DifferentCapValues_ProduceDifferentHash()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            // Same "has a cap" shape (both non-null), different magnitude - the
            // hasher must fold the actual value, not just presence/absence.
            string hashDaily5 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), 5, null);
            string hashDaily7 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), 7, null);
            string hashWeekly1 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, 1);
            string hashWeekly3 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, 3);

            Assert.NotEqual(hashDaily5, hashDaily7);
            Assert.NotEqual(hashWeekly1, hashWeekly3);
        }

        [Fact]
        public void HashIsLowercaseHex64Chars()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hash = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, null);

            Assert.Matches("^[0-9a-f]{64}$", hash);
        }

        // M37 (KNOWN-ISSUES #24): omitting homesteadTier is equivalent to
        // passing explicit null - it does NOT reproduce the pre-M37 hash,
        // since ComputeOfferId appends the ";homesteadTier=" segment
        // unconditionally (as "null" when omitted). This only pins down
        // self-consistency of the default value, not backward
        // compatibility with hashes computed before this parameter
        // existed.
        [Fact]
        public void OmittedHomesteadTier_MatchesExplicitNull()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hashOmitted = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, null);
            string hashExplicitNull = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, null, null);

            Assert.Equal(hashOmitted, hashExplicitNull);
        }

        [Fact]
        public void DifferentHomesteadTiers_ProduceDifferentHashes()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Item", Id = 19697, Count = 8 }
            };

            string hashNoTier = VendorOfferHasher.ComputeOfferId(
                102205, 1, costs, "Homestead Refinement\u2014Metal Forge", new List<string>(), null, null, null);
            string hashTier0 = VendorOfferHasher.ComputeOfferId(
                102205, 1, costs, "Homestead Refinement\u2014Metal Forge", new List<string>(), null, null, 0);
            string hashTier1 = VendorOfferHasher.ComputeOfferId(
                102205, 1, costs, "Homestead Refinement\u2014Metal Forge", new List<string>(), null, null, 1);
            string hashTier2 = VendorOfferHasher.ComputeOfferId(
                102205, 1, costs, "Homestead Refinement\u2014Metal Forge", new List<string>(), null, null, 2);

            Assert.NotEqual(hashNoTier, hashTier0);
            Assert.NotEqual(hashTier0, hashTier1);
            Assert.NotEqual(hashTier1, hashTier2);
            Assert.NotEqual(hashTier0, hashTier2);
        }

        // Astral Acclaim package (KNOWN-ISSUES #28): seasonalCap is appended
        // AFTER homesteadTier (not between it and weeklyCap) specifically so
        // every existing positional call above - including the ones that
        // already pass homesteadTier - keeps meaning exactly what it meant
        // before this parameter existed.

        [Fact]
        public void OmittedSeasonalCap_MatchesExplicitNull()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            string hashOmitted = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, null);
            string hashExplicitNull = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, null, null, null);

            Assert.Equal(hashOmitted, hashExplicitNull);
        }

        [Fact]
        public void DifferentSeasonalCaps_ProduceDifferentHashes()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 63, Count = 60 }
            };

            string hashNoCap = VendorOfferHasher.ComputeOfferId(
                19675, 1, costs, "Wizard's Vault", new List<string>(), null, null);
            string hashSeasonal20 = VendorOfferHasher.ComputeOfferId(
                19675, 1, costs, "Wizard's Vault", new List<string>(), null, null, null, 20);
            string hashSeasonal60 = VendorOfferHasher.ComputeOfferId(
                19675, 1, costs, "Wizard's Vault", new List<string>(), null, null, null, 60);

            Assert.NotEqual(hashNoCap, hashSeasonal20);
            Assert.NotEqual(hashNoCap, hashSeasonal60);
            Assert.NotEqual(hashSeasonal20, hashSeasonal60);
        }

        [Fact]
        public void SeasonalCap_IsIndependentOfDailyAndWeeklyCap()
        {
            var costs = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 1, Count = 100 }
            };

            // Same numeric value (5), but in different cap slots - must not
            // collide, since dailyCap/weeklyCap/seasonalCap are semantically
            // distinct even when their magnitude happens to match.
            string hashDaily5 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), 5, null);
            string hashWeekly5 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, 5);
            string hashSeasonal5 = VendorOfferHasher.ComputeOfferId(
                19685, 1, costs, "Merchant", new List<string>(), null, null, null, 5);

            Assert.NotEqual(hashDaily5, hashSeasonal5);
            Assert.NotEqual(hashWeekly5, hashSeasonal5);
        }
    }
}
