using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class VendorOfferStoreTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly VendorOfferLoader _loader;

        public VendorOfferStoreTests()
        {
            _tempDir = Path.Combine(
                Path.GetTempPath(),
                "GW2CraftingHelper_Tests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
            _loader = new VendorOfferLoader();
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        private MemoryStream MakeDatasetStream(params VendorOffer[] offers)
        {
            var dataset = new VendorOfferDataset
            {
                SchemaVersion = 1,
                GeneratedAt = "2026-01-01T00:00:00Z",
                Source = "test",
                Offers = new List<VendorOffer>(offers)
            };
            string json = _loader.Serialize(dataset);
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }

        private VendorOffer MakeOffer(string offerId, int outputItemId, int coinCost)
        {
            return new VendorOffer
            {
                OfferId = offerId,
                OutputItemId = outputItemId,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = coinCost }
                },
                MerchantName = "TestMerchant",
                Locations = new List<string> { "TestLocation" }
            };
        }

        [Fact]
        public void LoadBaseline_FromStream_PopulatesOffers()
        {
            var store = new VendorOfferStore(_tempDir, _loader);

            using (var stream = MakeDatasetStream(MakeOffer("aaa", 100, 50)))
            {
                store.LoadBaseline(stream);
            }

            var offers = store.GetOffersForItem(100);
            Assert.Single(offers);
            Assert.Equal("aaa", offers[0].OfferId);
            Assert.Equal(100, offers[0].OutputItemId);
        }

        [Fact]
        public void LoadBaseline_NullStream_ReturnsEmptyDataset()
        {
            var store = new VendorOfferStore(_tempDir, _loader);
            store.LoadBaseline(null);

            Assert.Empty(store.GetOffersForItem(100));
            Assert.False(store.HasAnyOffer(100));
        }

        [Fact]
        public void OverlayWinsByOfferId()
        {
            var store = new VendorOfferStore(_tempDir, _loader);

            var baselineOffer = MakeOffer("shared-id", 100, 50);
            baselineOffer.MerchantName = "BaselineMerchant";
            using (var stream = MakeDatasetStream(baselineOffer))
            {
                store.LoadBaseline(stream);
            }

            var overlayOffer = MakeOffer("shared-id", 100, 25);
            overlayOffer.MerchantName = "OverlayMerchant";
            var overlayDataset = new VendorOfferDataset
            {
                SchemaVersion = 1,
                GeneratedAt = "2026-01-02T00:00:00Z",
                Source = "overlay",
                Offers = new List<VendorOffer> { overlayOffer }
            };
            store.SaveOverlay(overlayDataset);
            store.LoadOverlay();

            var offers = store.GetOffersForItem(100);
            Assert.Single(offers);
            Assert.Equal("OverlayMerchant", offers[0].MerchantName);
            Assert.Equal(25, offers[0].CostLines[0].Count);
        }

        [Fact]
        public void SaveOverlay_RoundTrips()
        {
            var store = new VendorOfferStore(_tempDir, _loader);

            var dataset = new VendorOfferDataset
            {
                SchemaVersion = 1,
                GeneratedAt = "2026-01-01T00:00:00Z",
                Source = "overlay",
                Offers = new List<VendorOffer>
                {
                    MakeOffer("offer-1", 100, 50),
                    MakeOffer("offer-2", 200, 75)
                }
            };
            store.SaveOverlay(dataset);

            var store2 = new VendorOfferStore(_tempDir, _loader);
            store2.LoadOverlay();

            Assert.Single(store2.GetOffersForItem(100));
            Assert.Single(store2.GetOffersForItem(200));
            Assert.Equal("offer-1", store2.GetOffersForItem(100)[0].OfferId);
            Assert.Equal("offer-2", store2.GetOffersForItem(200)[0].OfferId);
        }

        [Fact]
        public void AddOffersToOverlay_AppendsAndDedupes()
        {
            var store = new VendorOfferStore(_tempDir, _loader);

            store.AddOffersToOverlay(new[]
            {
                MakeOffer("offer-1", 100, 50),
                MakeOffer("offer-2", 200, 75)
            });

            Assert.True(store.HasAnyOffer(100));
            Assert.True(store.HasAnyOffer(200));

            var updatedOffer = MakeOffer("offer-1", 100, 30);
            updatedOffer.MerchantName = "Updated";
            store.AddOffersToOverlay(new[]
            {
                updatedOffer,
                MakeOffer("offer-3", 300, 90)
            });

            Assert.Equal("Updated", store.GetOffersForItem(100)[0].MerchantName);
            Assert.Equal(30, store.GetOffersForItem(100)[0].CostLines[0].Count);
            Assert.True(store.HasAnyOffer(300));
        }

        [Fact]
        public void AddOffersToOverlay_SkipsNullOrEmptyOfferId()
        {
            var store = new VendorOfferStore(_tempDir, _loader);

            var nullIdOffer = MakeOffer(null, 100, 50);
            var emptyIdOffer = MakeOffer("", 200, 75);
            var validOffer = MakeOffer("valid", 300, 90);

            store.AddOffersToOverlay(new[] { nullIdOffer, emptyIdOffer, validOffer });

            Assert.False(store.HasAnyOffer(100));
            Assert.False(store.HasAnyOffer(200));
            Assert.True(store.HasAnyOffer(300));
        }

        [Fact]
        public void GetOffersForItems_ReturnsCorrectSubset()
        {
            var store = new VendorOfferStore(_tempDir, _loader);
            using (var stream = MakeDatasetStream(
                MakeOffer("a", 100, 10),
                MakeOffer("b", 200, 20),
                MakeOffer("c", 300, 30)))
            {
                store.LoadBaseline(stream);
            }

            var result = store.GetOffersForItems(new[] { 100, 200, 999 });

            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey(100));
            Assert.True(result.ContainsKey(200));
            Assert.False(result.ContainsKey(999));
        }

        [Fact]
        public void EmptyBaselineAndOverlay_ReturnsEmpty()
        {
            var store = new VendorOfferStore(_tempDir, _loader);
            store.LoadBaseline(null);
            store.LoadOverlay();

            Assert.Empty(store.GetOffersForItem(100));
            Assert.False(store.HasAnyOffer(100));
            Assert.Empty(store.GetOffersForItems(new[] { 100, 200 }));
        }

        [Fact]
        public void MultipleOffersForSameItem_SortedByOfferId()
        {
            var store = new VendorOfferStore(_tempDir, _loader);
            using (var stream = MakeDatasetStream(
                MakeOffer("zzz", 100, 50),
                MakeOffer("aaa", 100, 25),
                MakeOffer("mmm", 100, 75)))
            {
                store.LoadBaseline(stream);
            }

            var offers = store.GetOffersForItem(100);
            Assert.Equal(3, offers.Count);
            Assert.Equal("aaa", offers[0].OfferId);
            Assert.Equal("mmm", offers[1].OfferId);
            Assert.Equal("zzz", offers[2].OfferId);
        }

        [Fact]
        public void RebuildIndex_SkipsOffersWithNullOfferId()
        {
            var store = new VendorOfferStore(_tempDir, _loader);

            var dataset = new VendorOfferDataset
            {
                SchemaVersion = 1,
                GeneratedAt = "2026-01-01T00:00:00Z",
                Source = "test",
                Offers = new List<VendorOffer>
                {
                    new VendorOffer
                    {
                        OfferId = null,
                        OutputItemId = 100,
                        OutputCount = 1,
                        CostLines = new List<CostLine>(),
                        Locations = new List<string>()
                    },
                    MakeOffer("valid", 200, 50)
                }
            };
            string json = _loader.Serialize(dataset);
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                store.LoadBaseline(stream);
            }

            Assert.False(store.HasAnyOffer(100));
            Assert.True(store.HasAnyOffer(200));
        }

        // --- M37 (KNOWN-ISSUES #24): shipped Homestead Refinement tier
        // data invariant. PlanSolver.EvaluateVendorOffers admits any offer
        // whose HomesteadTier is null at every configured tier setting -
        // correct for the 21 one-time "Upgrade" purchase rows the same
        // three merchant pages also sell, but WRONG for a material-
        // conversion row, which would then silently reintroduce the
        // pre-M37 always-max-tier defect PR #57 fixed. The solver has no
        // independent way to catch a mistagged row - this test is the
        // only defense against a future wiki re-scrape shipping one, since
        // otherwise the only existing check is a dev-time console warning
        // in tools/VendorOfferUpdater/Program.cs. Loads the REAL shipped
        // ref/vendor_offers.json through the production VendorOfferLoader,
        // following the FindRepoFile convention established in
        // AcquisitionHintServiceTests.
        [Fact]
        public void ShippedSeedFile_HomesteadRefinementMaterialRows_AllHaveNonNullTierInRange()
        {
            string path = FindRepoFile(Path.Combine("ref", "vendor_offers.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/vendor_offers.json by walking up from the test assembly's directory.");

            using (var stream = File.OpenRead(path))
            {
                var dataset = _loader.Load(stream);

                var homesteadOffers = dataset.Offers
                    .Where(o => !string.IsNullOrEmpty(o.MerchantName) &&
                                o.MerchantName.Contains("Homestead Refinement"))
                    .ToList();

                var materialRows = homesteadOffers
                    .Where(o => Gw2Constants.HomesteadRefinementMaterialIds.Contains(o.OutputItemId))
                    .ToList();
                var nonMaterialRows = homesteadOffers
                    .Where(o => !Gw2Constants.HomesteadRefinementMaterialIds.Contains(o.OutputItemId))
                    .ToList();

                // Exact-count trip-wires (this repo's established seed-pin
                // style): any change to these numbers means a wiki
                // re-scrape altered the shipped Homestead Refinement rows
                // and warrants a manual look before this test is updated.
                Assert.Equal(237, homesteadOffers.Count);
                Assert.Equal(216, materialRows.Count);
                Assert.Equal(21, nonMaterialRows.Count);

                foreach (var offer in materialRows)
                {
                    Assert.True(
                        offer.HomesteadTier.HasValue,
                        $"Homestead Refinement material-conversion offer '{offer.OfferId}' " +
                        $"(output item {offer.OutputItemId}) has a null HomesteadTier. " +
                        "PlanSolver.EvaluateVendorOffers admits a null-tier offer at every " +
                        "configured tier by design (that is correct for the 21 one-time " +
                        "Upgrade-purchase rows, not for a material conversion row), so this " +
                        "would silently reintroduce the always-max-tier defect PR #57 fixed.");
                    Assert.InRange(offer.HomesteadTier.Value, 0, 2);
                }

                // The 21 non-material rows are expected to be null-tier
                // (one-time Upgrade purchases are tier-independent by
                // design) - documented here so this test also notices if
                // that population unexpectedly starts carrying tiers.
                Assert.All(nonMaterialRows, o => Assert.Null(o.HomesteadTier));
            }
        }

        /// <summary>
        /// Walks up from the running test assembly's directory looking for
        /// relativePath, so this test finds the repo's ref/ folder
        /// regardless of build configuration (Debug/Release) or platform
        /// subfolder depth. Returns null if not found within a reasonable
        /// number of levels, rather than throwing or scanning unrelated
        /// directories.
        /// </summary>
        private static string FindRepoFile(string relativePath)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; dir != null && i < 12; i++)
            {
                string candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
