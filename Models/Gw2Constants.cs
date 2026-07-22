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

        // TODO: Currency names are sourced from api.guildwars2.com/v2/currencies.
        // Verify against the official API if broadening coverage beyond this set.
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
            { 32, "Unbound Magic" },
            { 33, "Ascended Shards of Glory" },
            { 34, "Trade Contracts" },
            { 36, "Elegy Mosaics" },
            { 45, "Volatile Magic" },
            { 47, "Racing Medallions" },
            { 49, "Festival Tokens" },
            { 50, "Mistborn Motes" },
            { 58, "Jade Slivers" },
            { 59, "Research Notes" },
            { 60, "Imperial Favors" },
            { 62, "Unusual Coins" },
            { 63, "Astral Acclaim" },
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
