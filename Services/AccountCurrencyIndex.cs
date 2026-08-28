using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Wallet-derived per-currency owned amounts: display-only data,
    /// never consulted by InventoryReducer or PlanSolver, so owned
    /// currency can never affect a decision or total (matching gw2e,
    /// whose quantity engine also excludes currency from decision math).
    /// </summary>
    internal class AccountCurrencyIndex
    {
        private readonly Dictionary<int, int> _index;

        public AccountCurrencyIndex(IReadOnlyList<SnapshotWalletEntry> wallet)
        {
            _index = new Dictionary<int, int>();

            if (wallet == null)
            {
                return;
            }

            foreach (var entry in wallet)
            {
                if (entry.Value <= 0)
                {
                    continue;
                }

                _index[entry.CurrencyId] = _index.TryGetValue(entry.CurrencyId, out int existing)
                    ? existing + entry.Value
                    : entry.Value;
            }
        }

        /// <summary>Owned amount of the given currency id; 0 if none/unknown.</summary>
        public int GetQuantity(int currencyId)
        {
            return _index.TryGetValue(currencyId, out int amount) ? amount : 0;
        }
    }
}
