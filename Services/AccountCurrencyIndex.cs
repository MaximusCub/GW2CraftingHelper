using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Wallet-derived per-currency owned amounts (M34-B2a #4, gw2e parity -
    /// see m34-r2-gw2e-owned-materials.md Section 4.3): gw2efficiency
    /// structurally excludes currency from its tree's quantity/price/craft
    /// decision math (a Currency node's usedQuantity always equals its
    /// totalQuantity regardless of wallet balance). Audit row 56 PART B #3
    /// (corrected provenance): gw2efficiency nets owned currency out at
    /// least via a per-node "owned" display pill on the tree itself
    /// (componentTree.html, live-fetched) - it is not summary-layer-only,
    /// as this comment previously and incorrectly claimed. (Whether gw2e
    /// ALSO nets it out at a separate summary layer is not itself
    /// confirmed by that fetch; only the per-node pill is measured
    /// evidence - see docs/research/gw2e-convergence-matrix.md row 42.)
    /// The pill is display only, not decision math: gw2e's own quantity
    /// engine never nets owned currency into a decision either (docs/
    /// research/gw2e-convergence-matrix.md, calculateTreeQuantity.ts
    /// finding), same as this class. This class is that
    /// reconciliation's data source for this module - an
    /// AccountItemIndex-adjacent lookup over
    /// AccountSnapshot.Wallet, never consulted by InventoryReducer or
    /// PlanSolver, so owned currency can never affect a decision or total
    /// (see CraftingPlanPipelineTests' decisions-identical-with/without-
    /// wallet-data regression).
    /// </summary>
    public class AccountCurrencyIndex
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
