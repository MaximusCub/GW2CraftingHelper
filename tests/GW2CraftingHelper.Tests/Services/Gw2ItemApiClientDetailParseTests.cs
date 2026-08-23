using System.Linq;
using System.Threading.Tasks;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Drives the real <see cref="Gw2ItemApiClient"/> parser over verbatim
    /// live /v2/items responses (see <see cref="RealItemJson"/>). The point
    /// is that the details block the module already paid for is now read,
    /// and that the classes with NO details block still parse cleanly.
    /// </summary>
    public class Gw2ItemApiClientDetailParseTests
    {
        [Fact]
        public async Task FixedStatArmor_CarriesDefenseWeightAndInfixAttributes()
        {
            var items = await RealItemFixtures.ParseAsync(RealItemJson.ZojjasWarfists);
            var item = items[48074];

            Assert.Equal("Armor", item.ItemType);
            Assert.Equal(80, item.Level);
            Assert.Equal(240, item.VendorValue);
            Assert.Empty(item.Restrictions);

            var detail = item.Detail;
            Assert.NotNull(detail);
            Assert.Equal("Gloves", detail.SubType);
            Assert.Equal("Heavy", detail.WeightClass);
            Assert.Equal(191, detail.Defense);
            Assert.Equal(1, detail.InfusionSlotCount);
            Assert.Equal(134.442d, detail.AttributeAdjustment, 3);
            Assert.Equal(161, detail.InfixStatId);
            Assert.Equal(
                new[] { "Power:47", "Precision:34", "CritDamage:34" },
                detail.InfixAttributes.Select(a => a.Attribute + ":" + a.Modifier).ToArray());
            Assert.Empty(detail.StatChoiceIds);
            Assert.Null(detail.BuffDescription);
        }

        [Fact]
        public async Task StatSelectableWeapon_CarriesPowerRangeAndStatChoicesButNoInfixAttributes()
        {
            var items = await RealItemFixtures.ParseAsync(RealItemJson.Bolt);
            var detail = items[30699].Detail;

            Assert.Equal("Sword", detail.SubType);
            Assert.Equal("Lightning", detail.DamageType);
            Assert.Equal(950, detail.MinPower);
            Assert.Equal(1050, detail.MaxPower);
            Assert.Equal(0, detail.Defense);
            Assert.Equal(39, detail.StatChoiceIds.Count);
            Assert.Null(detail.InfixStatId);
            Assert.Empty(detail.InfixAttributes);
        }

        [Fact]
        public async Task CraftingMaterial_HasNoDetailsBlockAtAll()
        {
            var items = await RealItemFixtures.ParseAsync(RealItemJson.MithrilOre);
            var item = items[19700];

            Assert.Null(item.Detail);
            Assert.Equal("CraftingMaterial", item.ItemType);
            Assert.Equal(7, item.VendorValue);
            Assert.Equal("Refine into Ingots.", item.Description);
        }

        [Fact]
        public async Task Rune_CarriesItsSixPreformattedBonusLines()
        {
            var items = await RealItemFixtures.ParseAsync(RealItemJson.RuneOfTheScholar);
            var detail = items[24836].Detail;

            Assert.Equal("Rune", detail.SubType);
            Assert.Equal(
                new[] { "+25 Power", "+35 Ferocity", "+50 Power", "+65 Ferocity", "+100 Power", "+125 Ferocity" },
                detail.Bonuses.ToArray());
            Assert.Empty(detail.InfixAttributes);
        }

        [Fact]
        public async Task Sigil_CarriesItsBuffDescription()
        {
            var items = await RealItemFixtures.ParseAsync(RealItemJson.SigilOfForce);
            var detail = items[24615].Detail;

            Assert.Equal("Sigil", detail.SubType);
            Assert.Equal("+5% Damage", detail.BuffDescription);
            Assert.Empty(detail.Bonuses);
        }

        [Fact]
        public async Task Infusion_CarriesBothItsBuffAndItsAgonyResistanceAttribute()
        {
            var items = await RealItemFixtures.ParseAsync(RealItemJson.AgonyInfusion);
            var detail = items[49424].Detail;

            Assert.Equal("+1 Agony Resistance", detail.BuffDescription);
            var attribute = Assert.Single(detail.InfixAttributes);
            Assert.Equal("AgonyResistance", attribute.Attribute);
            Assert.Equal(1, attribute.Modifier);
        }

        [Fact]
        public async Task FineFood_CarriesItsNourishmentBlock_AscendedFoodCarriesNone()
        {
            var items = await RealItemFixtures.ParseAsync(RealItemJson.LotusFries, RealItemJson.CilantroSteak);

            var fine = items[12472].Detail;
            Assert.Equal("Food", fine.SubType);
            Assert.Equal(1800000, fine.NourishmentDurationMs);
            Assert.Equal(
                "30% Magic Find\n+70 Condition Damage\n+10% Experience from Kills",
                fine.NourishmentDescription);

            var ascended = items[91805].Detail;
            Assert.Equal("Food", ascended.SubType);
            Assert.Null(ascended.NourishmentDurationMs);
            Assert.Null(ascended.NourishmentDescription);
        }

        [Fact]
        public async Task NoSellFlagAndDescriptionMarkupSurviveTheParseUntouched()
        {
            var items = await RealItemFixtures.ParseAsync(RealItemJson.Bolt, RealItemJson.Sunrise);

            Assert.Contains("NoSell", items[30699].Flags);
            Assert.StartsWith("<c=@flavor>", items[30703].Description);
            Assert.Equal(2, items[30703].Detail.InfusionSlotCount);
        }

        [Fact]
        public async Task ExistingNameIconRarityFlagsParseIsUnchanged()
        {
            var items = await RealItemFixtures.ParseAsync(RealItemJson.Rebreather);
            var item = items[68357];

            Assert.Equal("Rime-Rimmed Mariner's Rebreather", item.Name);
            Assert.Equal("Exotic", item.Rarity);
            Assert.Contains("SoulBindOnUse", item.Flags);
            Assert.Equal(0, item.Detail.InfusionSlotCount);
            Assert.Equal(73, item.Detail.Defense);
        }
    }
}
