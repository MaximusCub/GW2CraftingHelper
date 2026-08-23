using System.Collections.Generic;
using System.Text;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// An <see cref="ItemStatBlock"/> rendered as tooltip content, in the
    /// order the in-game item tooltip uses: what the item DOES first
    /// (strength/defense, attributes, slots, granted bonuses), then a
    /// subdued identity block (rarity, type, level, binding, restrictions,
    /// vendor value), then flavour.
    ///
    /// <para>
    /// Blish-free, so the whole line-by-line contract is directly testable
    /// (repo invariant, same precedent as TreeRowTooltipComposer and
    /// ValueDetailTooltipBuilder). Nothing here decides what an absent
    /// field means - <see cref="ItemStatBlockFactory"/> already did, and
    /// this class only ever asks "is it present".
    /// </para>
    /// <para>
    /// No item icon: GW2's own item tooltips have none, and the hovered
    /// surface already shows one a few pixels away.
    /// </para>
    /// </summary>
    public static class ItemStatTooltipComposer
    {
        private static readonly IReadOnlyList<ItemAttributeLine> EmptyAttributes = new List<ItemAttributeLine>();
        private static readonly IReadOnlyList<string> EmptyStrings = new List<string>();

        public static TooltipContent BuildContent(ItemStatBlock stats)
        {
            if (stats == null)
            {
                return TooltipContent.Empty;
            }

            var builder = new TooltipContentBuilder();
            builder.RarityText(string.IsNullOrEmpty(stats.Name) ? "Unknown Item" : stats.Name, stats.Rarity)
                .EndLine();

            bool buffAlreadyShown = AppendCombatFacts(builder, stats);
            AppendUpgradeEffects(builder, stats, buffAlreadyShown);
            AppendNourishment(builder, stats);
            AppendIdentityBlock(builder, stats);
            AppendFlavor(builder, stats);

            return builder.Build();
        }

        /// <summary>
        /// Returns whether one of the attribute lines it emitted already
        /// says, verbatim, what <see cref="ItemStatBlock.BuffDescription"/>
        /// says - see <see cref="AppendUpgradeEffects"/>.
        /// </summary>
        private static bool AppendCombatFacts(TooltipContentBuilder builder, ItemStatBlock stats)
        {
            bool buffAlreadyShown = false;

            if (stats.MinPower.HasValue && stats.MaxPower.HasValue)
            {
                builder.Text($"Weapon Strength: {stats.MinPower.Value} - {stats.MaxPower.Value}").EndLine();
            }

            if (stats.Defense.HasValue)
            {
                builder.Text($"Defense: {stats.Defense.Value}").EndLine();
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
                builder.Text(line).EndLine();
                buffAlreadyShown = buffAlreadyShown || line == stats.BuffDescription;
            }

            if (stats.StatChoiceCount > 0)
            {
                // No numbers: which of the combinations to show is an open
                // judgment call (docs/KNOWN-ISSUES.md, "Item stat
                // tooltips", Q4) and the only part of this feature that
                // would need a /v2/itemstats request. The game shows the
                // same prompt on an unassigned item.
                builder.Text("Select stats").EndLine();
            }

            if (stats.InfusionSlotCount > 0)
            {
                // The COUNT, never "unused": what is socketed in the
                // player's own copy is instance state /v2/items cannot
                // know, and claiming the slots are empty would be a guess.
                string label = stats.InfusionSlotCount == 1 ? "Infusion Slot" : "Infusion Slots";
                builder.Styled($"{stats.InfusionSlotCount} {label}", TooltipSpanRole.Muted).EndLine();
            }

            return buffAlreadyShown;
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
            var bonuses = stats.UpgradeBonuses ?? EmptyStrings;
            for (int i = 0; i < bonuses.Count; i++)
            {
                builder.Styled($"({i + 1}): {bonuses[i]}", TooltipSpanRole.Bonus).EndLine();
            }
        }

        private static void AppendNourishment(TooltipContentBuilder builder, ItemStatBlock stats)
        {
            // Ascended food returns details:{type:Food} and nothing else
            // (measured on 91805). Silence, not a "no effect data" marker:
            // the absence is not itself confusing, and inventing a line
            // would be the one thing this module never does.
            if (!string.IsNullOrEmpty(stats.NourishmentDescription))
            {
                builder.Styled(stats.NourishmentDescription, TooltipSpanRole.Bonus).EndLine();
            }

            if (stats.NourishmentDurationMs.HasValue && stats.NourishmentDurationMs.Value > 0)
            {
                builder.Styled(
                    "Duration: " + FormatDuration(stats.NourishmentDurationMs.Value),
                    TooltipSpanRole.Muted).EndLine();
            }
        }

        private static void AppendIdentityBlock(TooltipContentBuilder builder, ItemStatBlock stats)
        {
            var identity = new TooltipContentBuilder();

            if (!string.IsNullOrEmpty(stats.Rarity))
            {
                identity.RarityText(stats.Rarity, stats.Rarity).EndLine();
            }

            string type = !string.IsNullOrEmpty(stats.SubType) ? stats.SubType : stats.ItemType;
            if (!string.IsNullOrEmpty(type))
            {
                identity.Styled(SpaceCamelCase(type), TooltipSpanRole.Muted).EndLine();
            }

            if (!string.IsNullOrEmpty(stats.WeightClass))
            {
                identity.Styled(stats.WeightClass + " Armor", TooltipSpanRole.Muted).EndLine();
            }

            if (!string.IsNullOrEmpty(stats.DamageType))
            {
                identity.Styled("Damage Type: " + stats.DamageType, TooltipSpanRole.Muted).EndLine();
            }

            if (stats.RequiredLevel > 0)
            {
                identity.Styled($"Required Level: {stats.RequiredLevel}", TooltipSpanRole.Muted).EndLine();
            }

            if (!string.IsNullOrEmpty(stats.Binding))
            {
                identity.Styled(stats.Binding, TooltipSpanRole.Muted).EndLine();
            }

            if (stats.Restrictions != null && stats.Restrictions.Count > 0)
            {
                identity.Styled(
                    "Restricted to: " + string.Join(", ", stats.Restrictions),
                    TooltipSpanRole.Muted).EndLine();
            }

            if (stats.VendorValue.HasValue)
            {
                identity.Styled("Vendor value: ", TooltipSpanRole.Muted)
                    .Coin(stats.VendorValue.Value, FormatCoin(stats.VendorValue.Value))
                    .EndLine();
            }

            var content = identity.Build();
            if (!content.IsEmpty)
            {
                builder.Separator().Append(content);
            }
        }

        private static void AppendFlavor(TooltipContentBuilder builder, ItemStatBlock stats)
        {
            if (!string.IsNullOrEmpty(stats.FlavorText))
            {
                builder.Separator().Styled(stats.FlavorText, TooltipSpanRole.Muted).EndLine();
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

        // Deliberately duplicates CoinCurrencyRenderer.FormatCoinText's
        // plain format rather than referencing it - that class is
        // Blish-coupled and this one must stay Blish-free. Same split,
        // same precedent as TreeRowTooltipComposer.FormatCoin.
        private static string FormatCoin(long copper)
        {
            var (gold, silver, cop) = CoinSegmentMath.Split(copper);
            return $"{gold}g {silver}s {cop}c";
        }
    }
}
