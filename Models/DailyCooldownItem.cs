namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Wiki-verified record of a craftable item whose RECIPE ITSELF is
    /// timegated (wiki.guildwars2.com Category:Time gated recipes / a
    /// "{{Recipe | timegate = y}}" template parameter plus an explicit
    /// "once per day" acquisition note) - a server-enforced daily cap on
    /// how many times the crafting action can succeed, independent of and
    /// never conflated with a vendor purchase cap (see TimegatedItem /
    /// TimegatedCapType, which cover vendor offers only). Modeled on
    /// AcquisitionHint (see that class's own doc comment) - PerDayCap is
    /// the only field ever used to compute the notice text; SourceUrl and
    /// LastVerified are provenance for maintainers only, never rendered.
    /// See ref/daily_cooldown_items.json and DailyCooldownItemService.
    /// </summary>
    public class DailyCooldownItem
    {
        public int ItemId { get; set; }

        /// <summary>Server-enforced cap on successful crafts of this recipe per day, per account. Every curated entry today is 1, but the field is not hard-coded to that value.</summary>
        public int PerDayCap { get; set; }

        public string SourceUrl { get; set; }
        public string LastVerified { get; set; }
    }
}
