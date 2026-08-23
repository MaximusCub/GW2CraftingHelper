using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// RawItem (+ its optional details block) -> <see cref="ItemStatBlock"/>.
    /// Every decision about what an absent field MEANS is made here, once,
    /// so the composer downstream only ever renders facts:
    /// <list type="bullet">
    /// <item><description>A null details block is the crafting-material
    /// case (measured on 19700/19685/46683), not an error - the block still
    /// carries name, rarity, type, level, vendor value and flavour.</description></item>
    /// <item><description>A weapon's <c>defense: 0</c> is "no defense", not
    /// "0 defense": every weapon in the API reports it. Same for a 0-value
    /// attribute modifier.</description></item>
    /// <item><description><c>NoSell</c> suppresses the vendor value
    /// entirely, which is what the game itself shows for those
    /// items.</description></item>
    /// </list>
    /// <para>
    /// Stat-SELECTABLE items (non-empty stat_choices) record only how many
    /// combinations exist. Computing numbers for one nominated combination
    /// is possible - see <see cref="ItemStatMath"/> - but WHICH one is an
    /// open judgment call (docs/KNOWN-ISSUES.md, "Item stat tooltips",
    /// Q4), and it is the only thing in this feature that would need a
    /// /v2/itemstats request. Nothing is guessed here.
    /// </para>
    /// </summary>
    public static class ItemStatBlockFactory
    {
        private static readonly IReadOnlyList<ItemAttributeLine> NoAttributes = new List<ItemAttributeLine>();
        private static readonly IReadOnlyList<string> NoStrings = new List<string>();

        public static ItemStatBlock Build(RawItem raw)
        {
            if (raw == null)
            {
                return null;
            }

            var flags = new HashSet<string>(raw.Flags ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            var block = new ItemStatBlock
            {
                ItemId = raw.Id,
                Name = raw.Name ?? "",
                Rarity = raw.Rarity,
                ItemType = raw.ItemType,
                RequiredLevel = raw.Level,
                Restrictions = raw.Restrictions ?? NoStrings,
                Binding = ResolveBinding(flags),
                VendorValue = ResolveVendorValue(raw.VendorValue, flags),
                FlavorText = ItemDescriptionSanitizer.Sanitize(raw.Description),
                Attributes = NoAttributes,
                UpgradeBonuses = NoStrings
            };

            var detail = raw.Detail;
            if (detail == null)
            {
                return block;
            }

            block.SubType = detail.SubType;
            block.WeightClass = detail.WeightClass;
            block.DamageType = detail.DamageType;
            block.InfusionSlotCount = detail.InfusionSlotCount;
            block.BuffDescription = detail.BuffDescription;
            block.StatChoiceCount = detail.StatChoiceIds == null ? 0 : detail.StatChoiceIds.Count;
            block.NourishmentDurationMs = detail.NourishmentDurationMs;

            if (detail.Defense.HasValue && detail.Defense.Value > 0)
            {
                block.Defense = detail.Defense;
            }

            if (detail.MinPower.HasValue && detail.MaxPower.HasValue &&
                detail.MaxPower.Value > 0)
            {
                block.MinPower = detail.MinPower;
                block.MaxPower = detail.MaxPower;
            }

            if (detail.InfixAttributes != null && detail.InfixAttributes.Count > 0)
            {
                var attributes = new List<ItemAttributeLine>(detail.InfixAttributes.Count);
                foreach (var attribute in detail.InfixAttributes)
                {
                    if (attribute == null || attribute.Modifier == 0)
                    {
                        continue;
                    }
                    attributes.Add(new ItemAttributeLine(
                        ItemStatMath.AttributeDisplayName(attribute.Attribute), attribute.Modifier));
                }
                if (attributes.Count > 0)
                {
                    block.Attributes = attributes;
                }
            }

            if (detail.Bonuses != null && detail.Bonuses.Count > 0)
            {
                block.UpgradeBonuses = detail.Bonuses;
            }

            if (!string.IsNullOrEmpty(detail.NourishmentDescription))
            {
                block.NourishmentDescription =
                    ItemDescriptionSanitizer.Sanitize(detail.NourishmentDescription);
            }

            return block;
        }

        /// <summary>
        /// The one binding line the game shows, not every flag the item
        /// carries: an ascended armour piece reports AccountBound AND
        /// AccountBindOnUse (48074), and a soulbinding one reports
        /// AccountBound AND SoulBindOnUse (68357). Most-specific wins, so
        /// each renders the state the player actually sees.
        /// </summary>
        private static string ResolveBinding(HashSet<string> flags)
        {
            if (flags.Contains("SoulbindOnAcquire"))
            {
                return "Soulbound on Acquire";
            }
            if (flags.Contains("SoulBindOnUse"))
            {
                return "Soulbound on Use";
            }
            if (flags.Contains("AccountBindOnUse"))
            {
                return "Account Bound on Use";
            }
            if (flags.Contains("AccountBound"))
            {
                return "Account Bound";
            }
            return null;
        }

        private static long? ResolveVendorValue(int vendorValue, HashSet<string> flags)
        {
            if (vendorValue <= 0 || flags.Contains("NoSell"))
            {
                return null;
            }
            return vendorValue;
        }
    }
}
