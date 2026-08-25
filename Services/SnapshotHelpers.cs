using System.Collections.Generic;
using GW2CraftingHelper.Models;
using Newtonsoft.Json;

namespace GW2CraftingHelper.Services
{
    internal static class SnapshotHelpers
    {
        /// <summary>
        /// Formats a copper value into the "Xg Ys Zc" display string.
        /// Negative values are clamped to 0 for display purposes.
        /// </summary>
        internal static string FormatCoin(int copper)
        {
            var (gold, silver, cop) = CoinSegmentMath.Split(copper);
            return $"Coin: {gold}g {silver}s {cop}c";
        }

        /// <summary>
        /// Splits a wallet entry list into coins (currency ID 1) and remaining wallet entries.
        /// If multiple coin entries exist, their values are summed defensively.
        /// </summary>
        internal static (int CoinCopper, List<SnapshotWalletEntry> Wallet) SplitWalletAndCoins(
            IEnumerable<SnapshotWalletEntry> walletEntries)
        {
            if (walletEntries == null)
            {
                return (0, new List<SnapshotWalletEntry>());
            }

            int coinCopper = 0;
            var wallet = new List<SnapshotWalletEntry>();

            foreach (var entry in walletEntries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (entry.CurrencyId == 1)
                {
                    coinCopper += entry.Value;
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
        /// Returns null if snapshot is null.
        /// </summary>
        internal static string SerializeSnapshot(AccountSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return JsonConvert.SerializeObject(snapshot, Formatting.Indented);
        }

        /// <summary>
        /// Deserializes an AccountSnapshot from a JSON string.
        /// Returns null for null, whitespace, or invalid JSON.
        /// </summary>
        internal static AccountSnapshot DeserializeSnapshot(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

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
