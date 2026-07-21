using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class AccountCurrencyIndexTests
    {
        private static SnapshotWalletEntry Entry(int currencyId, int value)
        {
            return new SnapshotWalletEntry { CurrencyId = currencyId, Value = value };
        }

        [Fact]
        public void NullWallet_AllQueriesReturnZero()
        {
            var index = new AccountCurrencyIndex(null);

            Assert.Equal(0, index.GetQuantity(2));
        }

        [Fact]
        public void EmptyWallet_AllQueriesReturnZero()
        {
            var index = new AccountCurrencyIndex(new List<SnapshotWalletEntry>());

            Assert.Equal(0, index.GetQuantity(2));
        }

        [Fact]
        public void SingleEntry_ReturnsItsValue()
        {
            var index = new AccountCurrencyIndex(new List<SnapshotWalletEntry>
            {
                Entry(2, 500)
            });

            Assert.Equal(500, index.GetQuantity(2));
        }

        [Fact]
        public void UnknownCurrencyId_ReturnsZero()
        {
            var index = new AccountCurrencyIndex(new List<SnapshotWalletEntry>
            {
                Entry(2, 500)
            });

            Assert.Equal(0, index.GetQuantity(3));
        }

        [Fact]
        public void DuplicateEntriesForSameCurrency_Summed()
        {
            var index = new AccountCurrencyIndex(new List<SnapshotWalletEntry>
            {
                Entry(2, 500),
                Entry(2, 250)
            });

            Assert.Equal(750, index.GetQuantity(2));
        }

        [Fact]
        public void ZeroOrNegativeEntries_Excluded()
        {
            var index = new AccountCurrencyIndex(new List<SnapshotWalletEntry>
            {
                Entry(2, 0),
                Entry(3, -5),
                Entry(4, 10)
            });

            Assert.Equal(0, index.GetQuantity(2));
            Assert.Equal(0, index.GetQuantity(3));
            Assert.Equal(10, index.GetQuantity(4));
        }
    }
}
