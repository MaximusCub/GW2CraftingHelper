using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public static class Gw2Constants
    {
        /// <summary>
        /// GW2 wallet currency ID for coins (gold/silver/copper).
        /// </summary>
        public const int CoinCurrencyId = 1;

        /// <summary>
        /// Reserved item id for the synthetic multi-item "wrapper"
        /// RecipeNode root (M35 - gw2efficiency parity: mirrors gw2e's own
        /// fake `{ id: false, name: "Multiple recipes" }` parent node - see
        /// docs/gw2e-parity-spec.md and the M34 r1 multi-item research
        /// report). Real GW2 item ids are always positive, so this can
        /// never collide with a genuine tree item. The wrapper this id
        /// marks must never be displayed to the user or surface in any
        /// public model - see RecipeService.BuildMultiItemTreeAsync,
        /// CraftingPlanPipeline's wrapper-aware tree building, and
        /// PlanSolver.Collect's matching skip.
        /// </summary>
        public const int MultiItemWrapperItemId = int.MinValue;

        /// <summary>
        /// Reserved recipe id for the multi-item wrapper's single synthetic
        /// "recipe" (whose Ingredients are the N selected items' own real
        /// trees, each already carrying its own requested amount as its
        /// ingredient quantity - gw2e's mechanism verbatim). Distinct from
        /// both real recipe ids (positive) and the small negative synthetic
        /// ids ref/mystic_forge_recipes.json assigns to Mystic Forge
        /// recipes (moot in practice - the wrapper's own step is never
        /// collected into a plan at all, see PlanSolver.Collect - but kept
        /// numerically distinct anyway). Note: PlanResultBuilder no longer
        /// uses a bare `recipeId &lt; 0` sign check to identify Mystic
        /// Forge recipes (a real id-space collision with the M37
        /// achievement/merchant seed recipes, ref/recipes_seed.json ids
        /// -1592..-1595, made that unsound) - it now checks the recipe's
        /// own declared Disciplines instead.
        /// </summary>
        public const int MultiItemWrapperRecipeId = int.MinValue;

        /// <summary>
        /// GW2 item ids of the three Homestead Refinement output materials
        /// (M37, KNOWN-ISSUES #24 - gw2e parity). These are exactly the
        /// item ids gw2efficiency's own cheapestTree.ts hardcodes
        /// ('102306', '102205', '103049') for its per-material
        /// userEfficiencyTiers gate. Confirmed via api.guildwars2.com/v2/items.
        /// </summary>
        public const int RefinedHomesteadFiberItemId = 102306;
        public const int RefinedHomesteadMetalItemId = 102205;
        public const int RefinedHomesteadWoodItemId = 103049;

        /// <summary>
        /// The three Homestead Refinement material ids, for iteration
        /// (e.g. validating a HomesteadEfficiencyTiers map's keys).
        /// </summary>
        public static readonly IReadOnlyList<int> HomesteadRefinementMaterialIds =
            new[] { RefinedHomesteadFiberItemId, RefinedHomesteadMetalItemId, RefinedHomesteadWoodItemId };

        /// <summary>
        /// opportunity-notes (SEASONAL VENDOR TIP): the festival name key
        /// this module uses for Halloween - MEASURED (2026-08-16, via
        /// System.Reflection against packages/BlishHUD.1.3.0/lib/net472/
        /// Blish HUD.exe) to be the exact runtime value of
        /// Blish_HUD.Contexts.FestivalContext.Festival.Halloween.Name:
        /// lowercase, "halloween" - NOT the capitalized DisplayName (see
        /// FestivalDisplayNames below for that). Matches both Module.cs's
        /// own festival-name projection (Festival.Name, not DisplayName -
        /// see that class's own comment) and ref/vendor_offers.json's
        /// seeded "seasonalFestival" value for the three known Candy Corn
        /// Vendor (Weekly) ecto offers.
        /// </summary>
        public const string HalloweenFestivalName = "halloween";

        /// <summary>
        /// opportunity-notes (SEASONAL VENDOR TIP): internal festival name
        /// key (Blish's own Festival.Name, e.g. "halloween") -> human-
        /// readable display name (Blish's own Festival.DisplayName, e.g.
        /// "Halloween") - MEASURED the same way as HalloweenFestivalName
        /// above. A small curated table (same shape/precedent as
        /// KnownCurrencyNames above), NOT a generic capitalizer: Blish's
        /// own DisplayName values are not a simple first-letter-uppercase
        /// of Name for every festival (e.g. "superadventurefestival" ->
        /// "Super Adventure Festival"), so guessing would risk a wrong
        /// display string for any FUTURE festival added here without also
        /// being measured.
        ///
        /// Festival-vendor auto-tagging follow-up (2026-08-16) review fix:
        /// this table used to be scoped to Halloween only, on the claim
        /// that the fallback below "can never happen today" because only
        /// a festival present in ref/vendor_offers.json's own seeded
        /// SeasonalFestival values could ever surface a tip - that claim
        /// was already false the moment that same commit's live run
        /// seeded dragonbash/wintersday/festivalofthefourwinds/
        /// lunarnewyear/superadventurefestival tags alongside halloween,
        /// so an active-festival tip for any of those five would have
        /// rendered the raw internal key verbatim (e.g. "During
        /// superadventurefestival:"). The five entries below close that
        /// gap; each display string is MEASURED, not guessed, from two
        /// independent sources agreeing verbatim: the GW2 Wiki's own
        /// festival vendor {{Temporary|...|seasonal=...}} display text
        /// (see tools/VendorOfferUpdater/Models/Gw2Constants.cs's
        /// FestivalKeysByWikiDisplayName, whose keys ARE these display
        /// strings) and a raw `strings -e s` (single-byte/ASCII) scan of
        /// packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe, which
        /// contains the literal resource text "Dragon Bash", "Festival of
        /// the Four Winds", "Lunar New Year", "Super Adventure Festival",
        /// and "Wintersday" verbatim (the earlier `-e l` UTF-16LE scan
        /// that found "halloween" found only the internal keys and
        /// property-getter names for these five, not their DisplayName
        /// text, because that text is UTF-8/ASCII-encoded resource data,
        /// not a UTF-16LE user-string-heap literal).
        ///
        /// PlanViewModelBuilder.ResolveFestivalDisplayName still falls
        /// back to the internal key verbatim for any key NOT listed here -
        /// now genuinely unreachable only for a SEVENTH, not-yet-existing
        /// festival Blish HUD's FestivalContext might add in the future,
        /// not for any of the six it recognizes today.
        /// </summary>
        public static readonly Dictionary<string, string> FestivalDisplayNames = new Dictionary<string, string>
        {
            { HalloweenFestivalName, "Halloween" },
            { "dragonbash", "Dragon Bash" },
            { "wintersday", "Wintersday" },
            { "festivalofthefourwinds", "Festival of the Four Winds" },
            { "lunarnewyear", "Lunar New Year" },
            { "superadventurefestival", "Super Adventure Festival" }
        };

        public static string ResolveFestivalDisplayName(string festivalName)
        {
            if (festivalName != null && FestivalDisplayNames.TryGetValue(festivalName, out var display))
            {
                return display;
            }
            return festivalName;
        }

        // Currency names are sourced from api.guildwars2.com/v2/currencies.
        // Review-fix (recipe-ingestion-fix, Must Fix): re-verified every
        // entry live (curl api.guildwars2.com/v2/currencies?ids=all,
        // 2026-08-15) after the reseed newly ingested 187 recipes with
        // Currency ingredients this table had never been cross-checked
        // against - several entries were shifted by one or more ids
        // (e.g. 49/50 held each other's names) and went unnoticed only
        // because no recipe had exercised them before now:
        //   36 was "Elegy Mosaics" (live: "Testimony of Desert Heroics";
        //       Elegy Mosaic is id 35) - corrected, not removed: ref/
        //       vendor_offers.json has real Currency-type cost lines keyed
        //       36, so a wrong-but-present name was still strictly better
        //       than dropping to the literal "Currency" fallback here.
        //   49 was "Festival Tokens" (live: "Mistborn Key")
        //   50 was "Mistborn Motes" (live: "Festival Token"; no currency
        //       named "Mistborn Motes" exists)
        //   58 was "Jade Slivers" (live: "War Supplies"; Jade Sliver is id 64)
        //   59 was "Research Notes" (live: "Unstable Fractal Essence";
        //       Research Note is id 61)
        //   60 was "Imperial Favors" (live: "Tyrian Defense Seal";
        //       Imperial Favor is id 68)
        // 61 (Research Note) and 65 (Testimony of Jade Heroics) were added:
        // both are used by real recipe Currency ingredients in
        // ref/recipes_seed.json (61 alone by 186 of the 187 newly-visible
        // recipes) and were previously entirely absent, falling back to the
        // literal displayed word "Currency" for every one of them.
        // Verify against the official API again if broadening coverage.
        public static readonly Dictionary<int, string> KnownCurrencyNames = new Dictionary<int, string>
        {
            { 2, "Karma" },
            { 3, "Laurels" },
            { 4, "Gems" },
            { 5, "Ascalonian Tears" },
            { 6, "Shards of Zhaitan" },
            { 7, "Fractal Relics" },
            { 9, "Seals of Beetletun" },
            { 10, "Manifesto of the Moletariate" },
            { 11, "Deadly Blooms" },
            { 12, "Symbols of Koda" },
            { 13, "Flame Legion Charr Carvings" },
            { 14, "Knowledge Crystals" },
            { 15, "Badges of Honor" },
            { 16, "Guild Commendations" },
            { 18, "Transmutation Charges" },
            { 19, "Airship Parts" },
            { 20, "Ley Line Crystals" },
            { 22, "Lumps of Aurillium" },
            { 23, "Spirit Shards" },
            { 24, "Pristine Fractal Relics" },
            { 25, "Geodes" },
            { 26, "WvW Skirmish Claim Tickets" },
            { 27, "Bandit Crests" },
            { 28, "Magnetite Shards" },
            { 29, "Provisioner Tokens" },
            { 30, "PvP League Tickets" },
            // currency-ux-package review fix (finding 2, MEASURED): ids 31,
            // 35, 39, 53, 57, 64, 67, 68, 69 were missing from this table
            // entirely, so ResolveCurrencyName returned the literal string
            // "Currency" for all nine - the Settings tab's now-curated
            // CuratedCurrencyIds list (SettingsTabContent.cs) surfaces every
            // one of them as a row, so all nine rendered as visually
            // identical, unlabeled "Currency" rows. Names below are cited
            // verbatim from CurrencyDecisionDefaults.cs's own inline
            // comments and cross-checked against the live GW2 API in
            // docs/research/gw2e-currency-decision-prices.md section 2
            // (2026-08-16 run) - not invented (repo invariant).
            { 31, "Proof of Heroics" },
            { 32, "Unbound Magic" },
            { 33, "Ascended Shards of Glory" },
            { 34, "Trade Contracts" },
            { 35, "Elegy Mosaic" },
            { 36, "Testimony of Desert Heroics" },
            { 39, "Gaeting Crystal" },
            { 45, "Volatile Magic" },
            { 47, "Racing Medallions" },
            { 49, "Mistborn Keys" },
            { 50, "Festival Tokens" },
            { 53, "Green Prophet Shard" },
            { 57, "Blue Prophet Shard" },
            { 58, "War Supplies" },
            { 59, "Unstable Fractal Essence" },
            { 60, "Tyrian Defense Seals" },
            { 61, "Research Notes" },
            { 62, "Unusual Coins" },
            { 63, "Astral Acclaim" },
            { 64, "Jade Sliver" },
            { 65, "Testimony of Jade Heroics" },
            { 67, "Canach Coins" },
            // Audit row 56 PART B #2: id 68 was missing entirely (verified
            // against https://api.guildwars2.com/v2/currencies?ids=all
            // 2026-08-16; live name is the singular "Imperial Favor" - a
            // plan costing this Cantha vendor currency previously fell back
            // to the generic "Currency" display name via ResolveCurrencyName.
            { 68, "Imperial Favor" },
            { 69, "Tales of Dungeon Delving" },
            { 78, "Fine Rift Essence" },
            { 79, "Rare Rift Essence" },
            { 80, "Masterwork Rift Essence" }
        };

        public static string ResolveCurrencyName(int currencyId)
        {
            if (KnownCurrencyNames.TryGetValue(currencyId, out var name))
            {
                return name;
            }
            return "Currency";
        }
    }
}
