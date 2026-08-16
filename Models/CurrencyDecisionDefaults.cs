using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Maintainer-curated default DECISION-ONLY currency valuations (copper
    /// per unit), adapted from gw2efficiency's own CURRENCY_DECISION_PRICES
    /// table so the module ships usable comparison values out of the box
    /// instead of requiring every user to hand-populate the Settings tab
    /// before a currency-priced vendor offer or recipe can ever compete
    /// against a Trading Post price (currency-ux-package, Feature 1).
    ///
    /// Source (MIT-licensed): @gw2efficiency/recipe-calculation,
    /// src/static/currencyDecisionPrices.ts -
    /// https://github.com/gw2efficiency/recipe-calculation
    /// License: MIT, Copyright (c) 2016 queicherius (David Reess).
    ///
    /// currency-ux-package review fix (nice-to-have): the MIT license
    /// requires the permission notice below be included with substantial
    /// portions of the licensed work, not just the copyright line -
    /// included here verbatim (source: the upstream repo's own LICENCE
    /// file) alongside the attribution above, since this table IS a
    /// substantial, verbatim-adapted portion of that work.
    ///
    /// Permission is hereby granted, free of charge, to any person
    /// obtaining a copy of this software and associated documentation
    /// files (the "Software"), to deal in the Software without
    /// restriction, including without limitation the rights to use, copy,
    /// modify, merge, publish, distribute, sublicense, and/or sell copies
    /// of the Software, and to permit persons to whom the Software is
    /// furnished to do so, subject to the following conditions:
    ///
    /// The above copyright notice and this permission notice shall be
    /// included in all copies or substantial portions of the Software.
    ///
    /// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    /// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
    /// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
    /// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS
    /// BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN
    /// ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
    /// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    /// SOFTWARE.
    /// Extracted and cross-checked entry-by-entry against the live GW2 API
    /// (api.guildwars2.com/v2/currencies?ids=all) 2026-08-16 - every key
    /// below is the OFFICIAL GW2 wallet currency id (numerically identical
    /// to gw2efficiency's own table; no id remapping was needed - see
    /// docs/research/gw2e-currency-decision-prices.md for the full
    /// provenance writeup, byte offsets, and live cross-check this run
    /// performed).
    ///
    /// Maintainer decision (currency-ux-package): shipping this curated
    /// table as defaults is an explicit, one-time waiver of the repo's
    /// "do not invent data" rule for THIS table only - every value below is
    /// sourced and attributed to the upstream MIT package above, not
    /// invented by this module.
    ///
    /// DECISION-ONLY, same invariant as the hand-entered CurrencyValuation
    /// table this augments: a value here may tip a craft-vs-buy/vendor-vs-TP
    /// comparison, but must never be surfaced as, or folded into, any
    /// displayed gold total (plan.TotalCoinCost, a step's TotalCost, ...).
    /// See CurrencyValuation.TryGetEffectiveCopperValue for the
    /// user-override/cleared/default precedence this table participates in
    /// (currency-ux-package review fix, finding 3: that method is not read
    /// by the solver at runtime - it is CurrencyValuation.WithDefaults'
    /// own implementation detail, which materializes this table's defaults
    /// into a plain dictionary before the solver ever runs - see
    /// TryGetEffectiveCopperValue's doc comment for the full seam). Direct
    /// readers of this table's keys/values are WithDefaults,
    /// CurrencyDecisionDefaults.TryGetDefault below, and (for the curated
    /// id list only) SettingsTabContent.BuildCuratedCurrencyIds.
    ///
    /// Entries gw2efficiency's own table marks `undefined` (meaning gw2e
    /// itself assigns that currency no decision value, not zero) are simply
    /// absent below, matching gw2e exactly. Among the currencies this
    /// module knows by name (Gw2Constants.KnownCurrencyNames - a broader
    /// set than what SettingsTabContent.CuratedCurrencyIds actually
    /// surfaces as a row; neither of these two named ids is curated for
    /// that or any other reason), that means Transmutation Charges (18),
    /// PvP League Tickets (30), and Racing Medallions (47) have no entry
    /// here. Astral Acclaim (63)
    /// and the three Rift Essence tiers (78/79/80) have no row at all in
    /// gw2e's table (it does not reach those ids) and so are also absent -
    /// the Settings tab and the solver alike must leave them blank/unvalued
    /// rather than invent a rate for them (see SettingsTabContent's own
    /// Astral Acclaim info line).
    /// </summary>
    public static class CurrencyDecisionDefaults
    {
        public static readonly IReadOnlyDictionary<int, long> DefaultCopperPerUnit = new Dictionary<int, long>
        {
            { 2, 1 },        // Karma
            { 3, 3500 },     // Laurel
            { 4, 3000 },     // Gem
            { 5, 32 },       // Ascalonian Tear
            { 6, 32 },       // Shard of Zhaitan
            { 7, 80 },       // Fractal Relic
            { 9, 32 },       // Seal of Beetletun
            { 10, 32 },      // Manifesto of the Moletariate
            { 11, 32 },      // Deadly Bloom
            { 12, 32 },      // Symbol of Koda
            { 13, 32 },      // Flame Legion Charr Carving
            { 14, 32 },      // Knowledge Crystal
            { 15, 23 },      // Badge of Honor
            { 16, 3600 },    // Guild Commendation
            { 19, 70 },      // Airship Part
            { 20, 70 },      // Ley Line Crystal
            { 22, 70 },      // Lump of Aurillium
            { 23, 3600 },    // Spirit Shard
            { 24, 1200 },    // Pristine Fractal Relic (15 * 80 in gw2e's own source)
            { 25, 100 },     // Geode
            { 26, 800 },     // WvW Skirmish Claim Ticket
            { 27, 45 },      // Bandit Crest
            { 28, 3600 },    // Magnetite Shard
            { 29, 3600 },    // Provisioner Token
            { 31, 50 },      // Proof of Heroics
            { 32, 25 },      // Unbound Magic
            { 33, 1600 },    // Ascended Shards of Glory
            { 34, 9 },       // Trade Contract
            { 35, 720 },     // Elegy Mosaic
            { 36, 135 },     // Testimony of Desert Heroics
            { 39, 3600 },    // Gaeting Crystal (id 39 only - a second, newer id 77 postdates gw2e's table)
            { 45, 50 },      // Volatile Magic
            { 50, 25 },      // Festival Token
            { 53, 3500 },    // Green Prophet Shard
            { 57, 300 },     // Blue Prophet Shard
            { 60, 310 },     // Tyrian Defense Seal
            { 61, 200 },     // Research Note
            { 62, 100 },     // Unusual Coin
            { 64, 35 },      // Jade Sliver
            { 65, 135 },     // Testimony of Jade Heroics
            { 67, 35 },      // Canach Coins
            { 68, 320 },     // Imperial Favor
            { 69, 32 }       // Tales of Dungeon Delving
        };

        /// <summary>
        /// Looks up the default copper-per-unit value gw2efficiency assigns
        /// <paramref name="currencyId"/>. Returns false for the coin
        /// currency id, any id gw2e's own table marks `undefined`, and any
        /// id absent from gw2e's table entirely (it stops at id 70) -
        /// exactly the same "no value" outcome for all three cases, matching
        /// CurrencyValuation.TryGetCopperValue's own no-value contract.
        /// </summary>
        public static bool TryGetDefault(int currencyId, out long copperPerUnit)
        {
            return DefaultCopperPerUnit.TryGetValue(currencyId, out copperPerUnit);
        }
    }
}
