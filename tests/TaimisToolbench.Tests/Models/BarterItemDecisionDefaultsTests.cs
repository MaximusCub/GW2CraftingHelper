using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using Xunit;

namespace TaimisToolbench.Tests.Models
{
    // BarterItemDecisionDefaults is a static curated table, not a service -
    // these tests pin its structural invariants and the two deliberate
    // exclusions, rather than mirroring every one of its entries.
    public class BarterItemDecisionDefaultsTests
    {
        [Fact]
        public void Defaults_EveryValuePositive()
        {
            foreach (var kvp in BarterItemDecisionDefaults.Defaults)
            {
                Assert.True(
                    kvp.Value.CopperPerUnit > 0,
                    $"Item {kvp.Key} has a non-positive default value {kvp.Value.CopperPerUnit}.");
            }
        }

        [Fact]
        public void Defaults_EveryEntryNamed()
        {
            foreach (var kvp in BarterItemDecisionDefaults.Defaults)
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(kvp.Value.Name),
                    $"Item {kvp.Key} has no display name. Item ids are internal-only (repo " +
                    "invariant), so an unnamed row would have nothing to render.");
            }
        }

        [Fact]
        public void Defaults_NoDuplicateNames()
        {
            var duplicates = BarterItemDecisionDefaults.Defaults.Values
                .GroupBy(entry => entry.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.True(
                duplicates.Count == 0,
                "Two curated rows share a display name, which would render as two " +
                "indistinguishable Settings rows: " + string.Join(", ", duplicates));
        }

        [Theory]
        [InlineData(19925, 667)] // Obsidian Shard: 25 Fractal Relics at 80, for 3
        [InlineData(86069, 200)] // Kralkatite Ore: 4 Volatile Magic at 50
        public void TryGetDefault_KnownItem_ReturnsExpectedValue(int itemId, long expected)
        {
            Assert.True(BarterItemDecisionDefaults.TryGetDefault(itemId, out long copperPerUnit));
            Assert.Equal(expected, copperPerUnit);
        }

        [Fact]
        public void TryGetDefault_RetiredGaetingCrystalItemForm_ReturnsFalse()
        {
            // Item 86094 was retired in-game 2022-07-19 alongside its wallet
            // form, currency 39, and no vendor offer in ref/vendor_offers.json
            // charges or produces it. A default here would price a good that
            // can no longer change hands. docs/ARCHITECTURE.md section 8.3.
            Assert.False(BarterItemDecisionDefaults.TryGetDefault(86094, out long copperPerUnit));
            Assert.Equal(0, copperPerUnit);
        }

        // Maintainer decision: the Black Lion family is gem-store RNG-chest
        // currency and its gold worth is personal, so no rate is suggested
        // for it - the same posture Astral Acclaim already gets (see
        // CurrencyDecisionDefaultsTests). These are the highest-usage
        // unpriced barter items in ref/vendor_offers.json by a wide margin
        // (43992 alone appears on 2,365 offers), so an entry appearing here
        // by accident would silently re-rank a large slice of the corpus.
        [Theory]
        [InlineData(43992)] // Black Lion Claim Ticket
        [InlineData(86694)] // Black Lion Statuette
        [InlineData(78474)] // Black Lion Miniature Claim Ticket
        [InlineData(88305)] // Black Lion Outfit Voucher
        [InlineData(88260)] // Black Lion Weapons Voucher
        [InlineData(88339)] // Black Lion Backpack and Glider Voucher
        [InlineData(88308)] // Black Lion Glider Voucher
        public void TryGetDefault_BlackLionFamily_ReturnsFalse(int itemId)
        {
            Assert.False(BarterItemDecisionDefaults.TryGetDefault(itemId, out long copperPerUnit));
            Assert.Equal(0, copperPerUnit);
        }

        [Fact]
        public void ResolveName_UnlistedItem_ReturnsNull()
        {
            Assert.Null(BarterItemDecisionDefaults.ResolveName(43992));
        }

        [Fact]
        public void Defaults_KeysAreDisjointFromNothingInTheCurrencyTable()
        {
            // Not a collision test - an item id and a currency id are
            // different id spaces and MAY share a number. This pins that
            // the two tables are looked up separately: item 2 has no entry
            // here even though currency 2 (Karma) has one there, so a lookup
            // that confused the two would be caught.
            Assert.True(CurrencyDecisionDefaults.DefaultCopperPerUnit.ContainsKey(2));
            Assert.False(BarterItemDecisionDefaults.Defaults.ContainsKey(2));
        }

        [Fact]
        public void Defaults_CoverTheHighestUsageItemsThatHaveADefensibleRoute()
        {
            // Tripwire on the population, not on any one value: these are
            // the most-used unpriced barter items in ref/vendor_offers.json
            // for which a whole-cost vendor route exists (measured
            // 2026-08-28). One vanishing from the table is a deliberate act
            // that should be noticed.
            var expected = new[] { 46682, 92272, 94163, 19925, 92072, 86069 };

            var missing = expected.Where(id => !BarterItemDecisionDefaults.Defaults.ContainsKey(id)).ToList();

            Assert.True(missing.Count == 0, "Missing curated defaults for item ids: " + string.Join(", ", missing));
        }

        [Fact]
        public void EveryDefault_SurvivesConstructionAsAUserValuation()
        {
            // CurrencyValuation's constructor rejects a non-positive value
            // outright, so a bad curated entry would throw only at the
            // first WithDefaults call in a live session.
            var items = new Dictionary<int, long>();
            foreach (var kvp in BarterItemDecisionDefaults.Defaults)
            {
                items[kvp.Key] = kvp.Value.CopperPerUnit;
            }

            var valuation = new CurrencyValuation(null, null, items);

            Assert.Equal(BarterItemDecisionDefaults.Defaults.Count, valuation.ItemCopperPerUnit.Count);
        }
    }
}
