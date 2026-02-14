using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class RawWikiVendorOffer
    {
        public int OutputItemId { get; set; }
        public int OutputCount { get; set; }
        public List<CostLine> CostLines { get; set; } = new List<CostLine>();
        public string MerchantName { get; set; }
        public List<string> Locations { get; set; } = new List<string>();
        public int? DailyCap { get; set; }
        public int? WeeklyCap { get; set; }
    }

    public interface IWikiVendorClient
    {
        Task<IReadOnlyList<RawWikiVendorOffer>> GetVendorOffersForItemAsync(
            int itemId, CancellationToken ct);
    }
}
