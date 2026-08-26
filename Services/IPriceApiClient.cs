using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    internal class RawPriceEntry
    {
        public int Id { get; set; }

        public int BuyUnitPrice { get; set; }

        public int SellUnitPrice { get; set; }
    }

    internal interface IPriceApiClient
    {
        Task<IReadOnlyList<RawPriceEntry>> GetPricesAsync(IReadOnlyList<int> itemIds, CancellationToken ct);
    }
}
