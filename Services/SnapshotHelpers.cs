using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using Newtonsoft.Json;

namespace GW2CraftingHelper.Services
{

    internal static class SnapshotHelpers
    {

        /// <summary>
        /// Formats a copper value into the "Xg Ys Zc" display string.
        /// </summary>
        internal static string FormatCoin(int copper)
        {
            int gold = copper / 10000;
            int silver = (copper % 10000) / 100;
            int cop = copper % 100;
            return $"Coin: {gold}g {silver}s {cop}c";
        }

        /// <summary>
        /// Groups items by ItemId, summing their counts. Source is set to "Total".
        /// </summary>
        internal static List<SnapshotItemEntry> AggregateItems(IEnumerable<SnapshotItemEntry> items)
        {
            if (items == null) return new List<SnapshotItemEntry>();

            return items
                .GroupBy(i => i.ItemId)
                .Select(g => new SnapshotItemEntry
                {
                    ItemId = g.Key,
                    Name = g.First().Name,
                    Count = g.Sum(i => i.Count),
                    Source = "Total"
                })
                .ToList();
        }

        /// <summary>
        /// Splits a wallet entry list into coins (currency ID 1) and remaining wallet entries.
        /// </summary>
        internal static (int CoinCopper, List<SnapshotWalletEntry> Wallet) SplitWalletAndCoins(
            IEnumerable<SnapshotWalletEntry> walletEntries)
        {
            if (walletEntries == null)
                return (0, new List<SnapshotWalletEntry>());

            int coinCopper = 0;
            var wallet = new List<SnapshotWalletEntry>();

            foreach (var entry in walletEntries)
            {
                if (entry.CurrencyId == 1)
                {
                    coinCopper = entry.Value;
                }
                else
                {
                    wallet.Add(entry);
                }
            }

            return (coinCopper, wallet);
        }

        /// <summary>
        /// Serializes an AccountSnapshot to a JSON string.
        /// </summary>
        internal static string SerializeSnapshot(AccountSnapshot snapshot)
        {
            return JsonConvert.SerializeObject(snapshot, Formatting.Indented);
        }

        /// <summary>
        /// Deserializes an AccountSnapshot from a JSON string.
        /// Returns null for null, empty, or invalid JSON.
        /// </summary>
        internal static AccountSnapshot DeserializeSnapshot(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<AccountSnapshot>(json);
            }
            catch
            {
                return null;
            }
        }
    }

}
