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

    /// <summary>
    /// One batch of price lookups, and whether that response is evidence
    /// about the ids it did NOT contain.
    /// </summary>
    internal class PriceBatchResult
    {
        public PriceBatchResult(IReadOnlyList<RawPriceEntry> entries, bool absenceProven)
        {
            Entries = entries ?? new List<RawPriceEntry>();
            AbsenceProven = absenceProven;
        }

        public IReadOnlyList<RawPriceEntry> Entries { get; }

        /// <summary>
        /// True only when <see cref="Entries"/> was parsed from a 2xx body,
        /// which lists every requested id the trading post trades: a
        /// requested id missing from it is then genuinely untradeable and
        /// may be negative-cached. /v2/commerce/prices answers 404 both for
        /// "every id in this batch is untradeable" and for an
        /// endpoint-level outage, and the two are indistinguishable to a
        /// caller, so a 404 sets this false - an outage must never be
        /// mistaken for proof that a plan's items cannot be priced.
        /// </summary>
        public bool AbsenceProven { get; }
    }

    internal interface IPriceApiClient
    {
        Task<PriceBatchResult> GetPricesAsync(IReadOnlyList<int> itemIds, CancellationToken ct);
    }
}
