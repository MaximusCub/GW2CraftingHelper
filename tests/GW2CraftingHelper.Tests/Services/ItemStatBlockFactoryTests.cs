using System.Linq;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Every fixture here is parsed by the real Gw2ItemApiClient first (see
    /// <see cref="RealItemFixtures"/>), so these assertions cover the whole
    /// live-JSON-to-stat-block path rather than the factory in isolation.
    /// </summary>
    public class ItemStatBlockFactoryTests
    {
        [Fact]
        public async Task FixedStatArmor_ResolvesAttributeNamesAndKeepsDefenseAndSlots()
        {
            var block = ItemStatBlockFactory.Build(
                await RealItemFixtures.ParseOneAsync(RealItemJson.ZojjasWarfists));

            Assert.Equal("Zojja's Warfists", block.Name);
            Assert.Equal("Ascended", block.Rarity);
            Assert.Equal("Armor", block.ItemType);
            Assert.Equal("Gloves", block.SubType);
            Assert.Equal("Heavy", block.WeightClass);
            Assert.Equal(191, block.Defense);
            Assert.Equal(80, block.RequiredLevel);
            Assert.Equal(1, block.InfusionSlotCount);
            Assert.Equal(0, block.StatChoiceCount);

            Assert.Equal(
                new[] { "Power=47", "Precision=34", "Ferocity=34" },
                block.Attributes.Select(a => a.DisplayName + "=" + a.Value).ToArray());

            // AccountBound AND AccountBindOnUse are both set; the game
            // shows the more specific one.
            Assert.Equal("Account Bound on Use", block.Binding);
            Assert.Equal(240L, block.VendorValue);
            Assert.Equal("Crafted in the style of the renowned asuran genius, Zojja.", block.FlavorText);
        }

        [Fact]
        public async Task CraftingMaterial_WithNoDetailsBlock_StillProducesANameRarityAndVendorValue()
        {
            var block = ItemStatBlockFactory.Build(
                await RealItemFixtures.ParseOneAsync(RealItemJson.MithrilOre));

            Assert.Equal("Mithril Ore", block.Name);
            Assert.Equal("Basic", block.Rarity);
            Assert.Equal("CraftingMaterial", block.ItemType);
            Assert.Equal(7L, block.VendorValue);
            Assert.Equal("Refine into Ingots.", block.FlavorText);

            Assert.Null(block.SubType);
            Assert.Null(block.Defense);
            Assert.Null(block.MinPower);
            Assert.Null(block.Binding);
            Assert.Empty(block.Attributes);
            Assert.Empty(block.UpgradeBonuses);
            Assert.Equal(0, block.InfusionSlotCount);
        }

        [Fact]
        public async Task StatSelectableLegendary_ReportsCombinationCountAndNeverInventsNumbers()
        {
            var block = ItemStatBlockFactory.Build(
                await RealItemFixtures.ParseOneAsync(RealItemJson.Bolt));

            Assert.Equal(950, block.MinPower);
            Assert.Equal(1050, block.MaxPower);
            Assert.Equal("Lightning", block.DamageType);
            Assert.Equal(39, block.StatChoiceCount);
            Assert.Empty(block.Attributes);

            // A weapon's defense:0 is "no defense figure", not "0 defense".
            Assert.Null(block.Defense);

            // NoSell: the game shows no vendor value for Bolt either.
            Assert.Null(block.VendorValue);
        }

        [Fact]
        public async Task SoulbindingItem_ReportsSoulboundEvenThoughAccountBoundIsAlsoFlagged()
        {
            var block = ItemStatBlockFactory.Build(
                await RealItemFixtures.ParseOneAsync(RealItemJson.Rebreather));

            Assert.Equal("Soulbound on Use", block.Binding);
            Assert.Equal(73, block.Defense);
            Assert.Equal(39, block.StatChoiceCount);
            Assert.Null(block.VendorValue);
        }

        [Fact]
        public async Task Rune_CarriesItsSixBonusesVerbatimAndNoAttributeLines()
        {
            var block = ItemStatBlockFactory.Build(
                await RealItemFixtures.ParseOneAsync(RealItemJson.RuneOfTheScholar));

            Assert.Equal("Rune", block.SubType);
            Assert.Equal(6, block.UpgradeBonuses.Count);
            Assert.Equal("+125 Ferocity", block.UpgradeBonuses[5]);
            Assert.Empty(block.Attributes);
            Assert.Null(block.Binding);
            Assert.Equal(65L, block.VendorValue);
            Assert.Equal("Element: Brilliance\nDouble-click to apply to a piece of armor.", block.FlavorText);
        }

        [Fact]
        public async Task SigilAndInfusion_CarryTheirBuffLine()
        {
            var sigil = ItemStatBlockFactory.Build(
                await RealItemFixtures.ParseOneAsync(RealItemJson.SigilOfForce));
            Assert.Equal("+5% Damage", sigil.BuffDescription);

            var infusion = ItemStatBlockFactory.Build(
                await RealItemFixtures.ParseOneAsync(RealItemJson.AgonyInfusion));
            Assert.Equal("+1 Agony Resistance", infusion.BuffDescription);
            var line = Assert.Single(infusion.Attributes);
            Assert.Equal("Agony Resistance", line.DisplayName);
            Assert.Equal(1, line.Value);
        }

        [Fact]
        public async Task FineFood_CarriesItsNourishmentBlock_AscendedFoodSilentlyCarriesNone()
        {
            var fine = ItemStatBlockFactory.Build(
                await RealItemFixtures.ParseOneAsync(RealItemJson.LotusFries));
            Assert.Equal(
                "30% Magic Find\n+70 Condition Damage\n+10% Experience from Kills",
                fine.NourishmentDescription);
            Assert.Equal(1800000, fine.NourishmentDurationMs);

            var ascended = ItemStatBlockFactory.Build(
                await RealItemFixtures.ParseOneAsync(RealItemJson.CilantroSteak));
            Assert.Null(ascended.NourishmentDescription);
            Assert.Null(ascended.NourishmentDurationMs);
            Assert.Equal("Account Bound on Use", ascended.Binding);
        }

        [Fact]
        public void NullRawItem_YieldsNullRatherThanAnEmptyBlock()
        {
            Assert.Null(ItemStatBlockFactory.Build(null));
        }

        [Fact]
        public void RawItemWithEveryCollectionNull_DoesNotThrowAndReportsNothing()
        {
            // A fixture-built or future client implementation may leave the
            // never-null-from-production lists null; the factory must not
            // assume the production parser's guarantees.
            var block = ItemStatBlockFactory.Build(new RawItem { Id = 7, Name = "X" });

            Assert.Equal(7, block.ItemId);
            Assert.Empty(block.Attributes);
            Assert.Empty(block.UpgradeBonuses);
            Assert.Empty(block.Restrictions);
            Assert.Null(block.Binding);
            Assert.Null(block.VendorValue);
            Assert.Equal("", block.FlavorText);
        }
    }
}
