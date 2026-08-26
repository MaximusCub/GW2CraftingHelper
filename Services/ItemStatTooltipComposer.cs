using System.Collections.Generic;
using System.Text;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// An <see cref="ItemStatBlock"/> rendered as tooltip content, in the
    /// line order the in-game item tooltip uses (spec section 1.6,
    /// KNOWN-ISSUES #42): the icon+name header,
    /// what the item DOES (strength/defense, attributes, granted bonuses),
    /// its infusion slots, then the identity block - rarity, type,
    /// level, DESCRIPTION AND FLAVOUR, then the binding flags - and last of
    /// all, unlabelled, the vendor value.
    ///
    /// <para>
    /// Blish-free, so the whole line-by-line contract is directly testable
    /// (repo invariant, same precedent as TreeRowTooltipComposer and
    /// ValueDetailTooltipBuilder). Nothing here decides what an absent
    /// field means - <see cref="ItemStatBlockFactory"/> already did, and
    /// this class only ever asks "is it present".
    /// </para>
    /// </summary>
    internal static class ItemStatTooltipComposer
    {
        private static readonly IReadOnlyList<ItemAttributeLine> EmptyAttributes = new List<ItemAttributeLine>();
        private static readonly IReadOnlyList<string> EmptyStrings = new List<string>();

        /// <summary>The game's own description string on a piece of gear
        /// whose stats have not been chosen yet (spec section 2.2, gap
        /// G12). Emitted verbatim rather than approximated.</summary>
        private const string SelectStatsPrompt = "Double-click to select stats.";

        public static TooltipContent BuildContent(ItemStatBlock stats)
        {
            if (stats == null)
            {
                return TooltipContent.Empty;
            }

            // The icon+name header row every in-game item tooltip opens
            // with (gap G11). The standing comment claiming the game shows
            // no icon was simply wrong - all three wiki captures show one.
            var builder = new TooltipContentBuilder();
            builder.Header(
                stats.IconUrl,
                string.IsNullOrEmpty(stats.Name) ? "Unknown Item" : stats.Name,
                stats.Rarity);

            var facts = BuildFacts(stats, out bool bodyOpensUnderHeader);

            // Blocks are collected first and joined with exactly one blank
            // line each, so an empty block never leaves a separator behind
            // and a name-only block never ends on a stray blank row.
            var blocks = new List<TooltipContent>(3);
            AddBlock(blocks, facts);
            AddBlock(blocks, BuildInfusionSlots(stats));
            AddBlock(blocks, BuildIdentityBlock(stats));

            for (int i = 0; i < blocks.Count; i++)
            {
                // Measured, and it splits on what the body OPENS with, not
                // on combat facts alone. A body that opens with the item's
                // combat facts or with its nourishment block runs straight
                // on under the header (steak.png: icon bottom y=37, first
                // text band y=39; warhelm.jpg: 37 -> 38). One that opens
                // with the identity block gets a blank first (xyaren.png:
                // icon bottom y=34, first text band y=53), and so does an
                // upgrade component, whose bonus lines FWDekker's
                // UpgradeComponent builder breaks before - inferred, no
                // unequipped-rune capture exists. Gap G15; the warhelm
                // divergence this leaves is in KNOWN-ISSUES #42.
                if (i > 0 || !bodyOpensUnderHeader)
                {
                    builder.Separator();
                }

                builder.Append(blocks[i]);
            }

            var value = BuildVendorValue(stats);
            if (!value.IsEmpty)
            {
                // The value is NOT unconditionally preceded by a blank.
                // The header rule still owns the case where the value is
                // the whole body; otherwise the item's own shape decides
                // (see ValueSitsAfterABlank).
                if (blocks.Count == 0 ? !bodyOpensUnderHeader : ValueSitsAfterABlank(stats))
                {
                    builder.Separator();
                }

                builder.Append(value);
            }

            return builder.Build();
        }

        /// <summary>
        /// Whether a blank row sits above the vendor value.
        /// <para>
        /// Measured absent on steak.png, the only capture that shows a
        /// value line at all: its body bands run at a 18px pitch - 39, 57,
        /// 75 (blank), 93 ("Food"), 111 ("Required Level: 10"), 129 (the
        /// coin row) - so the value follows the line above it contiguously,
        /// with row 128 carrying no glyph at all. Of FWDekker's fourteen
        /// builders only ELEVEN emit a value at all, and nine of those
        /// eleven emit it with no leading <c>&lt;br /&gt;</c>. The two that
        /// do not are <c>Generic</c> (its fallback, which is what a
        /// crafting material, a trait or a key gets) and an
        /// <c>UpgradeComponent</c> of type Gem.
        /// </para>
        /// <para>
        /// The table is inverted deliberately: a type this module has never
        /// seen falls to the Generic shape, exactly as it does in the
        /// replica's own <c>hasOwnProperty</c> fallback. The Generic blank
        /// itself is INFERRED - no capture of a crafting material's value
        /// line exists.
        /// </para>
        /// </summary>
        private static bool ValueSitsAfterABlank(ItemStatBlock stats)
        {
            if (stats.ItemType == "UpgradeComponent")
            {
                return stats.SubType == "Gem";
            }

            switch (stats.ItemType)
            {
                // The nine builders that emit getValue() contiguously.
                case "Armor":
                case "Back":
                case "Bag":
                case "Consumable":
                case "Container":
                case "Gizmo":
                case "Trinket":
                case "Trophy":
                case "Weapon":
                    return false;

                // GUESS, and the only one in this table. The replica emits
                // NO value line for these three - Gathering ends on
                // getLevel() + getFlags(), MiniPet on "Miniature" +
                // getFlags(), Tool on getDescription() + getFlags() - so it
                // cannot agree either way, and no capture of one exists.
                // This module does show their value (a mining pick and a
                // salvage kit both sell), so a shape has to be picked.
                // Picked contiguous by nearest body shape: Gathering's
                // description/level/flags body matches Gizmo's and
                // Trophy's, Tool's rarity/type/description/flags body
                // matches Container's and Consumable's, and MiniPet's
                // description/type/flags body matches Trophy's - all
                // contiguous. Desktop gate step 6 settles it.
                case "Gathering":
                case "MiniPet":
                case "Tool":
                    return false;

                default:
                    return true;
            }
        }

        private static void AddBlock(List<TooltipContent> blocks, TooltipContent block)
        {
            if (!block.IsEmpty)
            {
                blocks.Add(block);
            }
        }

        /// <summary>
        /// What the item does: weapon strength / defense / attributes, then
        /// the granted bonuses and nourishment that sit contiguously under
        /// them.
        /// <para>
        /// <paramref name="bodyOpensUnderHeader"/> reports whether this
        /// block leads with content the game runs straight on under the
        /// header - the combat facts, or the nourishment block - as opposed
        /// to a bonus run, which the game breaks before. It decides the
        /// header's blank line; see <see cref="BuildContent"/>.
        /// </para>
        /// </summary>
        private static TooltipContent BuildFacts(ItemStatBlock stats, out bool bodyOpensUnderHeader)
        {
            var facts = new TooltipContentBuilder();
            bool buffAlreadyShown = false;
            bool hasCombatFacts = false;

            if (stats.MinPower.HasValue && stats.MaxPower.HasValue)
            {
                facts.Text($"Weapon Strength: {FormatCount(stats.MinPower.Value)} - " +
                    FormatCount(stats.MaxPower.Value)).EndLine();
                hasCombatFacts = true;
            }

            if (stats.Defense.HasValue)
            {
                facts.Text($"Defense: {FormatCount(stats.Defense.Value)}").EndLine();
                hasCombatFacts = true;
            }

            // Null-tolerant even though ItemStatBlockFactory never leaves
            // these null: ItemStatBlock has public setters, so a future or
            // test-built block might.
            foreach (var attribute in stats.Attributes ?? EmptyAttributes)
            {
                // The API's modifier is already signed for the one
                // attribute type that can be negative; a positive one needs
                // its "+" added, as the game shows it.
                string sign = attribute.Value < 0 ? "" : "+";
                string line = $"{sign}{attribute.Value} {attribute.DisplayName}";
                facts.Text(line).EndLine();
                hasCombatFacts = true;
                buffAlreadyShown = buffAlreadyShown || line == stats.BuffDescription;
            }

            AppendUpgradeEffects(facts, stats, buffAlreadyShown);

            // Only when the nourishment block is what OPENS the body: an
            // item carrying a bonus run above it opens with the bonus, and
            // the game breaks before one.
            bool nourishmentOpensTheBody = facts.IsEmpty;
            AppendNourishment(facts, stats);
            nourishmentOpensTheBody = nourishmentOpensTheBody && !facts.IsEmpty;

            bodyOpensUnderHeader = hasCombatFacts || nourishmentOpensTheBody;
            return facts.Build();
        }

        private static void AppendUpgradeEffects(
            TooltipContentBuilder builder, ItemStatBlock stats, bool buffAlreadyShown)
        {
            // An agony infusion reports the same fact twice - once as
            // infix_upgrade.buff.description ("+1 Agony Resistance") and
            // once as an infix_upgrade.attributes entry that renders to the
            // identical string. The game prints it once, so the buff line
            // yields to the attribute line that already said it. Only an
            // exact match is suppressed: a buff description that summarises
            // several attributes ("+5 Power, +5 Precision") is its own
            // distinct wording and still belongs.
            if (!buffAlreadyShown && !string.IsNullOrEmpty(stats.BuffDescription))
            {
                builder.Styled(stats.BuffDescription, TooltipSpanRole.Bonus).EndLine();
            }

            // A rune's bonuses are positional - the Nth entry is the bonus
            // at N pieces equipped - so the index IS data, not decoration.
            // All of them, none greyed and no (x/6) counter: that needs the
            // character's equipped set, which is instance state /v2/items
            // cannot carry, and an unequipped rune in a bag is exactly what
            // the game shows this way (spec section 3.2).
            var bonuses = stats.UpgradeBonuses ?? EmptyStrings;
            for (int i = 0; i < bonuses.Count; i++)
            {
                builder.Styled($"({i + 1}): {bonuses[i]}", TooltipSpanRole.Bonus).EndLine();
            }
        }

        private static void AppendNourishment(TooltipContentBuilder builder, ItemStatBlock stats)
        {
            // WHITE, not the upgrade-bonus blue: steak.png's two
            // nourishment bands measure (252,254,253) and (252,255,255),
            // the same white as "Food" and "Required Level: 10" on that
            // capture. It is details.description - the very field this
            // renders - so the measurement is of this line, not of a
            // neighbour. The blue is measured on RUNE and SIGIL bonuses
            // only (Rune_effects_*.jpg).
            //
            // Ascended food returns details:{type:Food} and nothing else
            // (measured on 91805). Silence, not a "no effect data" marker:
            // the absence is not itself confusing, and inventing a line
            // would be the one thing this module never does.
            if (!string.IsNullOrEmpty(stats.NourishmentDescription))
            {
                builder.Text(stats.NourishmentDescription).EndLine();
            }

            if (stats.NourishmentDurationMs.HasValue && stats.NourishmentDurationMs.Value > 0)
            {
                builder.Text("Duration: " + FormatDuration(stats.NourishmentDurationMs.Value)).EndLine();
            }
        }

        /// <summary>
        /// The slot block: its own block between blanks, one line per slot
        /// so the block's height matches the game's (gap G16).
        /// </summary>
        private static TooltipContent BuildInfusionSlots(ItemStatBlock stats)
        {
            var slots = new TooltipContentBuilder();

            // The COUNT, never "unused": what is socketed in the player's
            // own copy is instance state /v2/items cannot know, and
            // claiming the slots are empty would be a guess. That wording
            // difference is an accepted divergence from the game's
            // "Unused Infusion Slot" - see KNOWN-ISSUES #42.
            for (int i = 0; i < stats.InfusionSlotCount; i++)
            {
                slots.Text("Infusion Slot").EndLine();
            }

            return slots.Build();
        }

        private static TooltipContent BuildIdentityBlock(ItemStatBlock stats)
        {
            var identity = new TooltipContentBuilder();

            // The rarity word carries the rarity colour, same as the name
            // line: measured on the 2026-08-25 live captures - s07's "Fine"
            // reads (82,146,240) and eq-weapon-full's "Legendary"
            // (153,51,255), both on non-comparison hovers. The 2012-2016
            // captures behind the old white reading (G5) are superseded;
            // the game changed. Basic is still suppressed outright, as the
            // game suppresses it (G20).
            if (!string.IsNullOrEmpty(stats.Rarity) && stats.Rarity != "Basic")
            {
                identity.RarityText(stats.Rarity, stats.Rarity).EndLine();
            }

            string type = !string.IsNullOrEmpty(stats.SubType) ? stats.SubType : stats.ItemType;
            bool isArmor = !string.IsNullOrEmpty(stats.WeightClass);

            // Armour is the one shape where the game splits these two: the
            // weight class alone, then the SLOT plus the word "Armor"
            // ("Heavy" / "Head Armor", measured on warhelm.jpg). The slot
            // word is the API's own (details.type), never a mapping table.
            if (isArmor)
            {
                identity.Text(stats.WeightClass).EndLine();
            }

            if (!string.IsNullOrEmpty(type))
            {
                identity.Text(SpaceCamelCase(type) + (isArmor ? " Armor" : "")).EndLine();
            }

            string hand = WeaponHand(stats);
            if (hand != null)
            {
                identity.Text(hand).EndLine();
            }

            if (!string.IsNullOrEmpty(stats.DamageType))
            {
                identity.Text("Damage Type: " + stats.DamageType).EndLine();
            }

            if (stats.RequiredLevel > 0)
            {
                identity.Text($"Required Level: {stats.RequiredLevel}").EndLine();
            }

            AppendDescription(identity, stats);

            // Above the binding line, which is where the game puts it (G18).
            if (stats.IsUnique)
            {
                identity.Text("Unique").EndLine();
            }

            if (!string.IsNullOrEmpty(stats.Binding))
            {
                identity.Text(stats.Binding).EndLine();
            }

            if (stats.Restrictions != null && stats.Restrictions.Count > 0)
            {
                identity.Text("Restricted to: " + string.Join(", ", stats.Restrictions)).EndLine();
            }

            return identity.Build();
        }

        /// <summary>
        /// The description sits INSIDE the identity block, after the
        /// type/level lines and before the binding flags - not appended
        /// after everything as a trailer (gap G13).
        /// <para>
        /// The description's own <c>&lt;c=@...&gt;</c> runs decide the
        /// colours: unmarked prose stays white, a flavour run goes teal, an
        /// abilitytype lead-in pale yellow (gap G7). Flattening the whole
        /// string to one role is what made "A gift bag!" indistinguishable
        /// from the quoted flavour after it.
        /// </para>
        /// </summary>
        private static void AppendDescription(TooltipContentBuilder builder, ItemStatBlock stats)
        {
            // A stat-selectable item's description in the game IS this
            // string. It is emitted ahead of whatever description the API
            // carries rather than instead of it, so nothing the item
            // actually says is lost.
            if (stats.StatChoiceCount > 0)
            {
                builder.Text(SelectStatsPrompt).EndLine();
            }

            var spans = ItemDescriptionSanitizer.SanitizeToSpans(stats.Description);
            if (spans.Count == 0)
            {
                return;
            }

            foreach (var span in spans)
            {
                builder.Styled(span.Text, span.Role);
            }

            builder.EndLine();
        }

        /// <summary>
        /// The vendor value the way the game shows it: unlabelled, alone on
        /// the last line, and absent entirely when the item cannot be sold
        /// (<see cref="ItemStatBlock.VendorValue"/> is null exactly then).
        /// Gap G14. Whether a blank precedes it is
        /// <see cref="ValueSitsAfterABlank"/>'s to say.
        /// </summary>
        private static TooltipContent BuildVendorValue(ItemStatBlock stats)
        {
            if (!stats.VendorValue.HasValue)
            {
                return TooltipContent.Empty;
            }

            return new TooltipContentBuilder()
                .Coin(stats.VendorValue.Value, CoinSegmentMath.GameStyleText(stats.VendorValue.Value))
                .Build();
        }

        /// <summary>
        /// The game's hand line under a weapon's type - "(Two-Handed)",
        /// "(Main Hand)", "(Off Hand)", "(Aquatic)" (gap G17). A weapon
        /// type this table does not know renders no line at all rather than
        /// a guessed one.
        /// </summary>
        private static string WeaponHand(ItemStatBlock stats)
        {
            if (stats.ItemType != "Weapon" || string.IsNullOrEmpty(stats.SubType))
            {
                return null;
            }

            switch (stats.SubType)
            {
                case "Greatsword":
                case "Hammer":
                case "LongBow":
                case "Rifle":
                case "ShortBow":
                case "Staff":
                    return "(Two-Handed)";
                case "Axe":
                case "Dagger":
                case "Mace":
                case "Pistol":
                case "Scepter":
                case "Sword":
                    return "(Main Hand)";
                case "Focus":
                case "Shield":
                case "Torch":
                case "Warhorn":
                    return "(Off Hand)";
                case "Harpoon":
                case "Speargun":
                case "Trident":
                    return "(Aquatic)";
                default:
                    return null;
            }
        }

        /// <summary>
        /// "CraftingMaterial" -> "Crafting Material". A mechanical split at
        /// camel-case boundaries, never a lookup table: the API's type
        /// vocabulary grows with every expansion, and a table would render
        /// a new type as nothing rather than as its own raw name.
        /// </summary>
        private static string SpaceCamelCase(string value)
        {
            var sb = new StringBuilder(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]) && value[i - 1] != ' ')
                {
                    sb.Append(' ');
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        private static string FormatDuration(int durationMs)
        {
            int totalMinutes = durationMs / 60000;
            if (totalMinutes < 60)
            {
                return $"{totalMinutes} m";
            }

            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            return minutes == 0 ? $"{hours} h" : $"{hours} h {minutes} m";
        }

        // Thousands-separated, as the game shows a four-figure weapon
        // strength ("1,045 - 1,155" - gap G19). Invariant culture, the
        // module's standing policy for its English-only strings.
        private static string FormatCount(int value)
        {
            return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
