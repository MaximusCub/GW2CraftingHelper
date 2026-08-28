using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class PlanHistoryDedupKeyTests
    {
        private static List<PlanRequestItem> Items(params (int Id, int Qty)[] items)
        {
            var list = new List<PlanRequestItem>();
            foreach (var (id, qty) in items)
            {
                list.Add(new PlanRequestItem { ItemId = id, Quantity = qty });
            }

            return list;
        }

        [Fact]
        public void ExactKeyString_IsPinnedVerbatim()
        {
            // Locks the format; changing it silently would re-key every
            // user's existing dedup behaviour.
            string key = PlanHistoryDedupKey.Compute(
                Items((30684, 1), (19721, 250)),
                useOwnMaterials: true,
                priceBasis: PriceBasis.BuyOrder,
                valueOwnMaterials: false,
                ignoredItemIds: new[] { 99, 7 });

            Assert.Equal("i:19721*250,30684*1|o:1|b:1|v:0|x:7,99", key);
        }

        [Fact]
        public void OrderInsensitive_OverRequestItemsAndIgnoredIds()
        {
            string a = PlanHistoryDedupKey.Compute(
                Items((1, 2), (3, 4)), false, PriceBasis.InstantBuy, false, new[] { 5, 6 });
            string b = PlanHistoryDedupKey.Compute(
                Items((3, 4), (1, 2)), false, PriceBasis.InstantBuy, false, new[] { 6, 5 });

            Assert.Equal(a, b);
        }

        [Fact]
        public void SensitiveTo_UseOwnMaterials_PriceBasis_ValueOwnMaterials()
        {
            var items = Items((1, 1));
            string baseline = PlanHistoryDedupKey.Compute(items, false, PriceBasis.InstantBuy, false, null);

            Assert.NotEqual(baseline, PlanHistoryDedupKey.Compute(items, true, PriceBasis.InstantBuy, false, null));
            Assert.NotEqual(baseline, PlanHistoryDedupKey.Compute(items, false, PriceBasis.BuyOrder, false, null));
            Assert.NotEqual(baseline, PlanHistoryDedupKey.Compute(items, false, PriceBasis.InstantBuy, true, null));
        }

        [Fact]
        public void SameItems_DifferentQuantities_ProduceDifferentKeys()
        {
            Assert.NotEqual(
                PlanHistoryDedupKey.Compute(Items((1, 1)), false, PriceBasis.InstantBuy, false, null),
                PlanHistoryDedupKey.Compute(Items((1, 2)), false, PriceBasis.InstantBuy, false, null));
        }

        [Fact]
        public void NullAndEmptyCollections_ProduceTheSameKey()
        {
            string fromNulls = PlanHistoryDedupKey.Compute(null, false, PriceBasis.InstantBuy, false, null);
            string fromEmpties = PlanHistoryDedupKey.Compute(
                new List<PlanRequestItem>(), false, PriceBasis.InstantBuy, false, new List<int>());

            Assert.Equal(fromNulls, fromEmpties);
            Assert.Equal("i:|o:0|b:0|v:0|x:", fromNulls);
        }

        [Fact]
        public void ForEntry_MatchesComputeOverTheSameIdentity()
        {
            var entry = new PlanHistoryEntry
            {
                RequestItems = Items((10, 3)),
                UseOwnMaterials = true,
                PriceBasis = PriceBasis.BuyOrder,
                ValueOwnMaterials = true,
                IgnoredItemIds = new List<int> { 4 },
            };

            Assert.Equal(
                PlanHistoryDedupKey.Compute(entry.RequestItems, true, PriceBasis.BuyOrder, true, new[] { 4 }),
                PlanHistoryDedupKey.ForEntry(entry));
        }
    }
}
