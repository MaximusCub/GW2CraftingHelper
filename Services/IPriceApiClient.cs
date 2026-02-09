using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    public class RawPriceEntry
    {
        public int Id { get; set; }
        public int BuyUnitPrice { get; set; }
        public int SellUnitPrice { get; set; }
    }

    public interface IPriceApiClient
    {
        Task<IReadOnlyList<RawPriceEntry>> GetPricesAsync(IReadOnlyList<int> itemIds, CancellationToken ct);
    }
}
