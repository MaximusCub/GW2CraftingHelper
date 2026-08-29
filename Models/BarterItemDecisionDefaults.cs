using System.Collections.Generic;

namespace TaimisToolbench.Models
{
    /// <summary>
    /// Curated default DECISION-ONLY valuations (copper per unit) for
    /// untradeable barter items - the account-bound tokens a vendor takes in
    /// place of coin. The Item-line counterpart of CurrencyDecisionDefaults,
    /// a separate table because a GW2 item id and a GW2 currency id are
    /// different id spaces that collide numerically (item 39 and currency 39
    /// are unrelated things).
    /// <para>
    /// DECISION-ONLY: a value here may tip a vendor-vs-TP comparison but is
    /// never spent, never folded into a displayed gold total and never
    /// committed to a plan. CurrencyValuation.TryGetEffectiveItemCopperValue
    /// holds the user-override/cleared/default precedence.
    /// Adding an entry: each value is DERIVED under one stated rule - the
    /// cheapest repeatable vendor exchange in ref/vendor_offers.json whose
    /// entire cost is coin or an already-valued currency, divided by that
    /// offer's output count, recorded per entry below. An item with no such
    /// route is absent on purpose; absent is a supported state, not an
    /// unfinished one. Derivation: docs/ARCHITECTURE.md section 8.3.
    /// </para>
    /// </summary>
    internal static class BarterItemDecisionDefaults
    {
        /// <summary>
        /// One curated entry. Name lives beside the value rather than in
        /// Gw2Constants (where currency names live, covering ids beyond
        /// CurrencyDecisionDefaults' own keys) because these two sets have
        /// exactly the same keys - a second table would be a sync hazard
        /// with nothing to gain. Every name is the live /v2/items name.
        /// </summary>
        internal sealed class BarterItemDefault
        {
            public BarterItemDefault(string name, long copperPerUnit)
            {
                Name = name;
                CopperPerUnit = copperPerUnit;
            }

            public string Name { get; }

            public long CopperPerUnit { get; }
        }

        public static readonly IReadOnlyDictionary<int, BarterItemDefault> Defaults =
            new Dictionary<int, BarterItemDefault>
        {
            // Karma-priced (currency 2 at 1 copper per karma), so the
            // copper figure is the karma price itself.
            { 89537, new BarterItemDefault("Branded Mass", 455) },
            { 88955, new BarterItemDefault("Lump of Mistonium", 455) },
            { 96533, new BarterItemDefault("Writ of New Kaineng City", 1050) },
            { 96561, new BarterItemDefault("Writ of Echovald Wilds", 1050) },
            { 96680, new BarterItemDefault("Writ of Seitung Province", 1050) },
            { 95692, new BarterItemDefault("Writ of Dragon's End", 1050) },
            { 102494, new BarterItemDefault("Curious Mursaat Currency", 1050) },
            { 104331, new BarterItemDefault("Curious Mursaat Ruin Shard", 1050) },
            { 103038, new BarterItemDefault("Curious Lowland Honeycomb", 1050) },
            { 90783, new BarterItemDefault("Mistborn Mote", 2688) },
            { 92272, new BarterItemDefault("Eternal Ice Shard", 2668) },

            // 7560 karma for 15 (Charity Corps Seraph, gift wrapping).
            { 77612, new BarterItemDefault("Roll of Wrapping Paper", 504) },

            // 500 Festival Tokens (currency 50 at 25) for 50, at the
            // Festival Rewards Vendor's weekly bulk exchange.
            { 79469, new BarterItemDefault("Petrified Wood", 250) },
            { 79899, new BarterItemDefault("Fresh Winterberry", 250) },
            { 80332, new BarterItemDefault("Jade Shard", 250) },
            { 81706, new BarterItemDefault("Orrian Pearl", 250) },

            // Volatile Magic (currency 45 at 50), Traveling Elonian Trader:
            // 4 for Kralkatite Ore, 20 for the other two.
            { 86069, new BarterItemDefault("Kralkatite Ore", 200) },
            { 86977, new BarterItemDefault("Difluorite Crystal", 1000) },
            { 87645, new BarterItemDefault("Inscribed Shard", 1000) },

            // 25 Fractal Relics (currency 7 at 80) for 3, at the Fractal
            // Reliquary: 2000 / 3, rounded to the nearest copper.
            { 19925, new BarterItemDefault("Obsidian Shard", 667) },

            // 10 Tyrian Defense Seals (currency 60 at 310), Tyrn Ironmaw.
            { 94163, new BarterItemDefault("Prismaticite Crystal", 3100) },

            // 5 Elegy Mosaics (currency 35 at 720) plus 105 karma, at the
            // Heart of Maguuma bulk exchanges.
            { 92072, new BarterItemDefault("Hatched Chili", 3705) },

            // 1 Guild Commendation (currency 16 at 3600) each, Sigurlina
            // Jonsdottir. The only route for these three whose whole cost
            // is a valued currency; every other listed route pays in
            // another untradeable map currency.
            { 46682, new BarterItemDefault("Crystalline Ore", 3600) },
            { 70718, new BarterItemDefault("Tenebrous Crystal", 3600) },
            { 76254, new BarterItemDefault("Shimmering Crystal", 3600) },

            // Not a vendor route: item 86094 and wallet currencies 39 and
            // 77 are all named "Gaeting Crystal" on /v2/items and
            // /v2/currencies - the same in-game good in item and wallet
            // form. Carried at currency 39's own CurrencyDecisionDefaults
            // value so the two forms cannot disagree.
            { 86094, new BarterItemDefault("Gaeting Crystal", 3600) },
        };

        /// <summary>
        /// Looks up the curated default copper-per-unit value of
        /// <paramref name="itemId"/>. Returns false for any item with no
        /// entry - the same "no value" outcome as an unvalued currency,
        /// matching CurrencyValuation.TryGetItemCopperValue's contract.
        /// </summary>
        public static bool TryGetDefault(int itemId, out long copperPerUnit)
        {
            if (Defaults.TryGetValue(itemId, out var entry))
            {
                copperPerUnit = entry.CopperPerUnit;
                return true;
            }

            copperPerUnit = 0;
            return false;
        }

        /// <summary>
        /// The curated display name of <paramref name="itemId"/>, or null
        /// when this table has no entry for it. Item ids are internal-only
        /// (repo invariant); only the name ever reaches the UI.
        /// </summary>
        public static string ResolveName(int itemId)
        {
            return Defaults.TryGetValue(itemId, out var entry) ? entry.Name : null;
        }
    }
}
