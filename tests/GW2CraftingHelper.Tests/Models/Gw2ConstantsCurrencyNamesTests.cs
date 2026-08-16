using System.Collections.Generic;
using GW2CraftingHelper.Models;
using Xunit;

namespace GW2CraftingHelper.Tests.Models
{
    /// <summary>
    /// Audit row 56 PART B #2: pins Gw2Constants.KnownCurrencyNames' id-to-
    /// name pairs against a real snapshot of GET /v2/currencies?ids=all
    /// (captured 2026-08-16, v=2022-03-23 - see this file's own
    /// LiveApiNameById, which is the exact "name" field the live API
    /// returned for each id, not invented). KnownCurrencyNames itself
    /// consistently pluralizes the API's singular per-unit name for every
    /// countable wallet currency (e.g. API "Badge of Honor" -&gt;
    /// dict "Badges of Honor") and leaves mass-noun currencies (Karma,
    /// Magic, Essence, Acclaim) and already-plural API names (War Supplies)
    /// unchanged - ExpectedDictName below encodes that same, already-
    /// established convention per id so this test can assert an exact
    /// value, not just "is non-null". Guards specifically against the
    /// PRE-ingestion bug this audit found (ids 36/49/50/58/59/60 mispaired
    /// against the wrong currency, and id 68 Imperial Favor missing
    /// entirely) ever regressing.
    /// </summary>
    public class Gw2ConstantsCurrencyNamesTests
    {
        // Real "name" field per id, captured verbatim from
        // https://api.guildwars2.com/v2/currencies?ids=all&v=2022-03-23 on
        // 2026-08-16 - a representative subset covering every id this
        // audit specifically checked (the six previously-mispaired ids,
        // the newly-added id 68, plus a spread of long-standing entries as
        // a general regression net).
        private static readonly IReadOnlyDictionary<int, string> LiveApiNameById = new Dictionary<int, string>
        {
            { 2, "Karma" },
            { 6, "Shard of Zhaitan" },
            { 9, "Seal of Beetletun" },
            { 12, "Symbol of Koda" },
            { 15, "Badge of Honor" },
            { 22, "Lump of Aurillium" },
            { 23, "Spirit Shard" },
            { 32, "Unbound Magic" },
            { 36, "Testimony of Desert Heroics" },
            { 45, "Volatile Magic" },
            { 49, "Mistborn Key" },
            { 50, "Festival Token" },
            { 58, "War Supplies" },
            { 59, "Unstable Fractal Essence" },
            { 60, "Tyrian Defense Seal" },
            { 63, "Astral Acclaim" },
            { 65, "Testimony of Jade Heroics" },
            { 68, "Imperial Favor" },
            { 78, "Fine Rift Essence" }
        };

        // The exact value KnownCurrencyNames carries for each id above -
        // see this class' own doc comment for the pluralization convention
        // this encodes (mechanical "+s" on the head noun for a countable
        // per-unit currency name, unchanged for a mass noun or an
        // already-plural API name).
        private static readonly IReadOnlyDictionary<int, string> ExpectedDictName = new Dictionary<int, string>
        {
            { 2, "Karma" },
            { 6, "Shards of Zhaitan" },
            { 9, "Seals of Beetletun" },
            { 12, "Symbols of Koda" },
            { 15, "Badges of Honor" },
            { 22, "Lumps of Aurillium" },
            { 23, "Spirit Shards" },
            { 32, "Unbound Magic" },
            { 36, "Testimony of Desert Heroics" },
            { 45, "Volatile Magic" },
            { 49, "Mistborn Keys" },
            { 50, "Festival Tokens" },
            { 58, "War Supplies" },
            { 59, "Unstable Fractal Essence" },
            { 60, "Tyrian Defense Seals" },
            { 63, "Astral Acclaim" },
            { 65, "Testimony of Jade Heroics" },
            { 68, "Imperial Favors" },
            { 78, "Fine Rift Essence" }
        };

        [Fact]
        public void KnownCurrencyNames_PinnedIds_MatchLiveApiSnapshot()
        {
            foreach (var kvp in ExpectedDictName)
            {
                int id = kvp.Key;
                string expectedName = kvp.Value;

                Assert.True(
                    Gw2Constants.KnownCurrencyNames.ContainsKey(id),
                    $"KnownCurrencyNames is missing id {id} ({LiveApiNameById[id]} per the live API).");
                Assert.Equal(expectedName, Gw2Constants.KnownCurrencyNames[id]);
            }
        }

        [Fact]
        public void KnownCurrencyNames_Id60_IsTyrianDefenseSeal_NotImperialFavor()
        {
            // The specific PRE-ingestion mispairing this audit found: id 60
            // was labeled "Imperial Favors" but the live API's id 60 is
            // Tyrian Defense Seal - real Imperial Favor is id 68. Both
            // halves of that regression are pinned in one assertion.
            Assert.Equal("Tyrian Defense Seals", Gw2Constants.KnownCurrencyNames[60]);
            Assert.NotEqual(Gw2Constants.KnownCurrencyNames[60], Gw2Constants.KnownCurrencyNames[68]);
        }

        [Fact]
        public void KnownCurrencyNames_Id68_ImperialFavor_Present()
        {
            Assert.True(Gw2Constants.KnownCurrencyNames.ContainsKey(68));
            Assert.Equal("Imperial Favors", Gw2Constants.KnownCurrencyNames[68]);
        }

        [Fact]
        public void ResolveCurrencyName_Id68_ReturnsImperialFavors_NotGenericFallback()
        {
            Assert.Equal("Imperial Favors", Gw2Constants.ResolveCurrencyName(68));
        }

        [Fact]
        public void ResolveCurrencyName_UnknownId_FallsBackToGenericCurrency()
        {
            // Regression net for ResolveCurrencyName's own fallback path -
            // an id genuinely absent from the dict (unlike 68, now fixed
            // above) must still degrade gracefully rather than throw.
            Assert.Equal("Currency", Gw2Constants.ResolveCurrencyName(999999));
        }
    }
}
