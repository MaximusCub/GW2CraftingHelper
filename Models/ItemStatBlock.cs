using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// An item's tooltip-ready facts: already-resolved attribute lines,
    /// already-decided binding wording, already-sanitized flavour text.
    /// The API soup (infix_upgrade, stat_choices, attribute_adjustment)
    /// stops at <see cref="Services.ItemStatBlockFactory"/>.
    ///
    /// <para>
    /// DELIBERATELY NOT A MEMBER OF <see cref="ItemMetadata"/>, and
    /// deliberately unreachable from <see cref="PersistedPlan"/>.
    /// PersistedPlan.Result is a <see cref="CraftingPlanResult"/> holding
    /// the ItemMetadata dictionary, and PersistedPlanSchemaMemberSetTests
    /// guards that whole reachable graph against
    /// PersistedPlan.CurrentSchemaVersion; hanging stats off ItemMetadata
    /// would force a schema bump, which
    /// PlanStoreHelpers.DeserializePersistedPlan answers by discarding
    /// every existing user's saved plan. Stat blocks are therefore a
    /// session-scoped side channel held by ItemMetadataService alone - a
    /// restored plan simply has none until something re-fetches (see
    /// docs/KNOWN-ISSUES.md, "Item stat tooltips").
    /// </para>
    /// </summary>
    public sealed class ItemStatBlock
    {
        public int ItemId { get; set; }

        public string Name { get; set; }

        /// <summary>GW2 API rarity string; null/empty = unknown.</summary>
        public string Rarity { get; set; }

        /// <summary>Top-level /v2/items "type" ("Armor", "Weapon", ...).</summary>
        public string ItemType { get; set; }

        /// <summary>details.type ("Gloves", "Sword", "Rune"); null when the
        /// item has no details block at all.</summary>
        public string SubType { get; set; }

        public string WeightClass { get; set; }

        /// <summary>Null when the item has no defense figure. A genuine 0
        /// (every weapon reports defense 0) is NOT rendered - see
        /// ItemStatBlockFactory.</summary>
        public int? Defense { get; set; }

        public int? MinPower { get; set; }

        public int? MaxPower { get; set; }

        public string DamageType { get; set; }

        public int RequiredLevel { get; set; }

        /// <summary>Fixed-stat attribute lines. Empty for a stat-selectable
        /// item, which reports <see cref="StatChoiceCount"/> instead.</summary>
        public IReadOnlyList<ItemAttributeLine> Attributes { get; set; }

        public int InfusionSlotCount { get; set; }

        /// <summary>A rune's bonus lines, verbatim API text.</summary>
        public IReadOnlyList<string> UpgradeBonuses { get; set; }

        /// <summary>A sigil/infusion/jewel's effect line, verbatim API text.</summary>
        public string BuffDescription { get; set; }

        /// <summary>How many stat combinations this item can be acquired
        /// with; 0 for a fixed-stat item. WHICH combination is a judgment
        /// call left open - see docs/KNOWN-ISSUES.md, "Item stat
        /// tooltips" (Q4) - so no numbers are computed from it here.</summary>
        public int StatChoiceCount { get; set; }

        public string NourishmentDescription { get; set; }

        public int? NourishmentDurationMs { get; set; }

        /// <summary>Display wording for the item's strongest binding flag
        /// ("Soulbound on Use"), or null when the item binds nothing.</summary>
        public string Binding { get; set; }

        /// <summary>Profession/race restrictions; empty, never null.</summary>
        public IReadOnlyList<string> Restrictions { get; set; }

        /// <summary>Vendor sale value in copper, or null when the item
        /// carries NoSell - which is exactly when the game shows no value
        /// either.</summary>
        public long? VendorValue { get; set; }

        /// <summary>
        /// The API's own description string, markup INTACT. The
        /// <c>&lt;c=@...&gt;</c> runs are the only thing that tells plain
        /// description prose (white) apart from flavour (teal) inside one
        /// string, so the split into roles happens at compose time via
        /// <see cref="Services.ItemDescriptionSanitizer.SanitizeToSpans"/>
        /// rather than being flattened away here.
        /// </summary>
        public string Description { get; set; }
    }
}
