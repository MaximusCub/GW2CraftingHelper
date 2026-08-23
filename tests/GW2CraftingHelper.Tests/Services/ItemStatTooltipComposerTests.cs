using System.Linq;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Runs the whole live-JSON -> parser -> factory -> composer path for
    /// each item class the feature has to survive, and asserts on the lines
    /// a reader would actually see.
    /// </summary>
    public class ItemStatTooltipComposerTests
    {
        private static async Task<string[]> LinesFor(string itemJson)
        {
            var raw = await RealItemFixtures.ParseOneAsync(itemJson);
            return ItemStatTooltipComposer.BuildContent(ItemStatBlockFactory.Build(raw))
                .ToPlainLines().ToArray();
        }

        [Fact]
        public async Task FixedStatArmor_ReadsLikeTheInGameTooltip()
        {
            Assert.Equal(new[]
            {
                "Zojja's Warfists",
                "Defense: 191",
                "+47 Power",
                "+34 Precision",
                "+34 Ferocity",
                "1 Infusion Slot",
                "",
                "Ascended",
                "Gloves",
                "Heavy Armor",
                "Required Level: 80",
                "Account Bound on Use",
                "Vendor value: 0g 2s 40c",
                "",
                "Crafted in the style of the renowned asuran genius, Zojja."
            }, await LinesFor(RealItemJson.ZojjasWarfists));
        }

        [Fact]
        public async Task StatSelectableLegendary_ShowsSelectStatsAndNoInventedNumbers()
        {
            var lines = await LinesFor(RealItemJson.Bolt);

            Assert.Equal("Bolt", lines[0]);
            Assert.Equal("Weapon Strength: 950 - 1050", lines[1]);
            Assert.Equal("Select stats", lines[2]);
            Assert.Equal("1 Infusion Slot", lines[3]);
            Assert.Contains("Damage Type: Lightning", lines);
            Assert.Contains("Legendary", lines);
            Assert.Contains("Sword", lines);

            // NoSell, and a weapon's defense:0 - neither may appear.
            Assert.DoesNotContain(lines, l => l.StartsWith("Vendor value"));
            Assert.DoesNotContain(lines, l => l.StartsWith("Defense"));
            // No attribute lines at all - a stat-selectable item has no
            // resolved numbers until a combination is chosen.
            Assert.DoesNotContain(lines, l => l.StartsWith("+"));
        }

        [Fact]
        public async Task CraftingMaterial_GetsANameRarityTypeValueAndDescription()
        {
            Assert.Equal(new[]
            {
                "Mithril Ore",
                "",
                "Basic",
                "Crafting Material",
                "Vendor value: 0g 0s 7c",
                "",
                "Refine into Ingots."
            }, await LinesFor(RealItemJson.MithrilOre));
        }

        [Fact]
        public async Task Rune_ShowsItsSixPositionalBonuses()
        {
            var lines = await LinesFor(RealItemJson.RuneOfTheScholar);

            Assert.Equal("Superior Rune of the Scholar", lines[0]);
            Assert.Equal("(1): +25 Power", lines[1]);
            Assert.Equal("(6): +125 Ferocity", lines[6]);
            Assert.Contains("Rune", lines);
            Assert.Contains("Vendor value: 0g 0s 65c", lines);
        }

        [Fact]
        public async Task SigilAndInfusion_ShowTheirBuffLine()
        {
            Assert.Contains("+5% Damage", await LinesFor(RealItemJson.SigilOfForce));

            var infusion = await LinesFor(RealItemJson.AgonyInfusion);
            // Both the buff line and the resolved attribute line say the
            // same thing on this item - the API reports it twice.
            Assert.Equal(2, infusion.Count(l => l == "+1 Agony Resistance"));
        }

        [Fact]
        public async Task FineFood_ShowsItsNourishmentBlockAndDuration()
        {
            var lines = await LinesFor(RealItemJson.LotusFries);

            Assert.Equal("Cup of Lotus Fries", lines[0]);
            Assert.Equal("30% Magic Find", lines[1]);
            Assert.Equal("+70 Condition Damage", lines[2]);
            Assert.Equal("+10% Experience from Kills", lines[3]);
            Assert.Equal("Duration: 30 m", lines[4]);
        }

        [Fact]
        public async Task AscendedFood_SaysNothingAboutAnEffectItHasNoDataFor()
        {
            var lines = await LinesFor(RealItemJson.CilantroSteak);

            Assert.Equal("Cilantro Lime Sous-Vide Steak", lines[0]);
            Assert.DoesNotContain(lines, l => l.StartsWith("Duration"));
            Assert.DoesNotContain(lines, l => l.Contains("No effect"));
            Assert.Contains("Account Bound on Use", lines);
        }

        [Fact]
        public async Task SoulbindingItemShowsSoulboundAndSuppressesItsNoSellValue()
        {
            var lines = await LinesFor(RealItemJson.Rebreather);

            Assert.Contains("Soulbound on Use", lines);
            Assert.Contains("Defense: 73", lines);
            Assert.Contains("Select stats", lines);
            Assert.Contains("Helm Aquatic", lines);
            Assert.DoesNotContain(lines, l => l.StartsWith("Vendor value"));
        }

        [Fact]
        public async Task NameLineCarriesTheRarityColourRoleAndTheCoinLineStaysACoinSpan()
        {
            var raw = await RealItemFixtures.ParseOneAsync(RealItemJson.ZojjasWarfists);
            var content = ItemStatTooltipComposer.BuildContent(ItemStatBlockFactory.Build(raw));

            var nameSpan = content.Lines[0].Spans.Single();
            Assert.Equal(TooltipSpanRole.Rarity, nameSpan.Role);
            Assert.Equal("Ascended", nameSpan.RarityKey);

            var coinSpan = content.Lines
                .SelectMany(l => l.Spans)
                .Single(s => s.IsCoin);
            Assert.Equal(240, coinSpan.CoinCopper);
        }

        [Fact]
        public void NullBlockYieldsEmptyContentSoTheSurfaceStaysHidden()
        {
            Assert.True(ItemStatTooltipComposer.BuildContent(null).IsEmpty);
        }

        [Fact]
        public void ABlockWithNothingButANameStillRendersThatName()
        {
            var content = ItemStatTooltipComposer.BuildContent(new ItemStatBlock { ItemId = 1, Name = "Thing" });
            Assert.Equal("Thing", content.ToPlainText());
        }

        [Fact]
        public void AnUnnamedBlockNeverRendersABlankFirstLine()
        {
            var content = ItemStatTooltipComposer.BuildContent(new ItemStatBlock { ItemId = 1 });
            Assert.Equal("Unknown Item", content.ToPlainText());
        }

        [Theory]
        [InlineData(1800000, "Duration: 30 m")]
        [InlineData(3600000, "Duration: 1 h")]
        [InlineData(5400000, "Duration: 1 h 30 m")]
        public void DurationsAboveAnHourReadAsHoursAndMinutes(int durationMs, string expected)
        {
            var content = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Food",
                NourishmentDurationMs = durationMs
            });

            Assert.Contains(expected, content.ToPlainLines());
        }

        [Fact]
        public void ANegativeAttributeKeepsItsOwnSignRatherThanGainingAPlus()
        {
            var content = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Odd",
                Attributes = new[] { new ItemAttributeLine("Power", -5) }
            });

            Assert.Contains("-5 Power", content.ToPlainLines());
        }
    }
}
