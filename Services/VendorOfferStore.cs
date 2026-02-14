using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class VendorOfferStore
    {
        private readonly string _overlayPath;
        private readonly VendorOfferLoader _loader;
        private VendorOfferDataset _baseline;
        private VendorOfferDataset _overlay;

        private Dictionary<string, VendorOffer> _mergedById;
        private Dictionary<int, List<VendorOffer>> _mergedByOutput;

        public VendorOfferStore(string dataDirectoryPath, VendorOfferLoader loader)
        {
            _overlayPath = Path.Combine(dataDirectoryPath, "vendor_offers_overlay.json");
            _loader = loader;
            _baseline = new VendorOfferDataset { SchemaVersion = 1, Offers = new List<VendorOffer>() };
            _overlay = new VendorOfferDataset { SchemaVersion = 1, Offers = new List<VendorOffer>() };
            RebuildIndex();
        }

        public void LoadBaseline(Stream baselineStream)
        {
            if (baselineStream != null)
            {
                try
                {
                    _baseline = _loader.Load(baselineStream);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load vendor baseline: {ex.Message}");
                    _baseline = new VendorOfferDataset { SchemaVersion = 1, Offers = new List<VendorOffer>() };
                }
            }
            else
            {
                _baseline = new VendorOfferDataset { SchemaVersion = 1, Offers = new List<VendorOffer>() };
            }
            RebuildIndex();
        }

        public void LoadOverlay()
        {
            try
            {
                if (File.Exists(_overlayPath))
                {
                    using (var fs = File.OpenRead(_overlayPath))
                    {
                        _overlay = _loader.Load(fs);
                    }
                }
                else
                {
                    _overlay = new VendorOfferDataset { SchemaVersion = 1, Offers = new List<VendorOffer>() };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load vendor overlay: {ex.Message}");
                _overlay = new VendorOfferDataset { SchemaVersion = 1, Offers = new List<VendorOffer>() };
            }
            RebuildIndex();
        }

        public void SaveOverlay(VendorOfferDataset overlay)
        {
            _overlay = overlay;
            string dir = Path.GetDirectoryName(_overlayPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = _loader.Serialize(overlay);
            string tmpPath = _overlayPath + ".tmp";
            File.WriteAllText(tmpPath, json, Encoding.UTF8);

            if (File.Exists(_overlayPath))
            {
                File.Replace(tmpPath, _overlayPath, null);
            }
            else
            {
                File.Move(tmpPath, _overlayPath);
            }

            RebuildIndex();
        }

        public void AddOffersToOverlay(IEnumerable<VendorOffer> offers)
        {
            var existingIds = new HashSet<string>(
                _overlay.Offers.Select(o => o.OfferId));

            foreach (var offer in offers)
            {
                if (string.IsNullOrEmpty(offer.OfferId))
                {
                    continue;
                }

                if (existingIds.Contains(offer.OfferId))
                {
                    var idx = _overlay.Offers.FindIndex(o => o.OfferId == offer.OfferId);
                    if (idx >= 0)
                    {
                        _overlay.Offers[idx] = offer;
                    }
                }
                else
                {
                    _overlay.Offers.Add(offer);
                    existingIds.Add(offer.OfferId);
                }
            }

            _overlay.GeneratedAt = DateTime.UtcNow.ToString("o");
            SaveOverlay(_overlay);
        }

        public IReadOnlyList<VendorOffer> GetOffersForItem(int outputItemId)
        {
            if (_mergedByOutput != null &&
                _mergedByOutput.TryGetValue(outputItemId, out var offers))
            {
                return offers;
            }
            return Array.Empty<VendorOffer>();
        }

        public IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> GetOffersForItems(
            IEnumerable<int> outputItemIds)
        {
            var result = new Dictionary<int, IReadOnlyList<VendorOffer>>();
            foreach (var id in outputItemIds)
            {
                var offers = GetOffersForItem(id);
                if (offers.Count > 0)
                {
                    result[id] = offers;
                }
            }
            return result;
        }

        public bool HasAnyOffer(int outputItemId)
        {
            return _mergedByOutput != null &&
                   _mergedByOutput.ContainsKey(outputItemId);
        }

        private void RebuildIndex()
        {
            _mergedById = new Dictionary<string, VendorOffer>();

            if (_baseline?.Offers != null)
            {
                foreach (var offer in _baseline.Offers)
                {
                    if (string.IsNullOrEmpty(offer.OfferId))
                    {
                        continue;
                    }
                    _mergedById[offer.OfferId] = offer;
                }
            }

            if (_overlay?.Offers != null)
            {
                foreach (var offer in _overlay.Offers)
                {
                    if (string.IsNullOrEmpty(offer.OfferId))
                    {
                        continue;
                    }
                    _mergedById[offer.OfferId] = offer;
                }
            }

            _mergedByOutput = new Dictionary<int, List<VendorOffer>>();
            foreach (var offer in _mergedById.Values)
            {
                if (!_mergedByOutput.TryGetValue(offer.OutputItemId, out var list))
                {
                    list = new List<VendorOffer>();
                    _mergedByOutput[offer.OutputItemId] = list;
                }
                list.Add(offer);
            }

            foreach (var list in _mergedByOutput.Values)
            {
                list.Sort((a, b) => string.Compare(a.OfferId, b.OfferId, StringComparison.Ordinal));
            }
        }
    }
}
