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
                "",
                "Infusion Slot",
                "",
                "Ascended",
                "Heavy",
                "Gloves Armor",
                "Required Level: 80",
                "Crafted in the style of the renowned asuran genius, Zojja.",
                "Account Bound on Use",
                "2s 40c",
            }, await LinesFor(RealItemJson.ZojjasWarfists));
        }

        [Fact]
        public async Task StatSelectableLegendary_ShowsSelectStatsAndNoInventedNumbers()
        {
            var lines = await LinesFor(RealItemJson.Bolt);

            Assert.Equal("Bolt", lines[0]);
            Assert.Equal("Weapon Strength: 950 - 1,050", lines[1]);
            Assert.Equal("", lines[2]);
            Assert.Equal("Infusion Slot", lines[3]);
            // The game's own string for an unassigned stat-selectable
            // item, in the DESCRIPTION position inside the identity block.
            Assert.Contains("Double-click to select stats.", lines);
            Assert.Contains("(Main Hand)", lines);
            Assert.Contains("Damage Type: Lightning", lines);
            Assert.Contains("Legendary", lines);
            Assert.Contains("Sword", lines);

            // NoSell, and a weapon's defense:0 - neither may appear. The
            // value line is omitted ENTIRELY, so there is no last line to
            // hold it and no blank separator in front of one.
            Assert.False(await HasCoinSpan(RealItemJson.Bolt));
            Assert.DoesNotContain(lines, l => l.StartsWith("Defense"));
            // No attribute lines at all - a stat-selectable item has no
            // resolved numbers until a combination is chosen.
            Assert.DoesNotContain(lines, l => l.StartsWith("+"));
        }

        [Fact]
        public async Task CraftingMaterial_GetsANameRarityTypeValueAndDescription()
        {
            // No "Basic" line: the game suppresses that rarity word (G20).
            Assert.Equal(new[]
            {
                "Mithril Ore",
                "",
                "Crafting Material",
                "Refine into Ingots.",
                "",
                "7c",
            }, await LinesFor(RealItemJson.MithrilOre));
        }

        [Fact]
        public async Task AConsumablesValueFollowsTheLineAboveItWithNoBlank()
        {
            // Measured on steak.png, the one capture that shows a value
            // line: its body bands run 39, 57, 75 (blank), 93 ("Food"),
            // 111 ("Required Level: 10"), 129 (the coin row) - one 18px
            // pitch from the level line to the value, row 128 empty.
            // FWDekker's Consumable builder emits getValue() with no
            // leading break, as eight of the ten other builders that emit
            // a value at all do (fourteen builders, eleven getValue()
            // call sites, two of them behind a break).
            var lines = await LinesFor(RealItemJson.CilantroSteak);

            Assert.Equal("Account Bound on Use", lines[lines.Length - 2]);
            Assert.Equal("1s 65c", lines[lines.Length - 1]);
        }

        [Fact]
        public void ACraftingMaterialKeepsTheBlankAboveItsValue()
        {
            // The other side: FWDekker's Generic builder - its fallback,
            // and what a crafting material, a trait or a key gets - is one
            // of only two that put a break in front of getValue().
            // Inferred; no capture of a crafting material's value exists.
            var lines = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Mithril Ore",
                ItemType = "CraftingMaterial",
                VendorValue = 7,
            }).ToPlainLines();

            Assert.Equal("", lines[lines.Count - 2]);
            Assert.Equal("7c", lines[lines.Count - 1]);
        }

        [Theory]
        [InlineData("Gathering")]
        [InlineData("MiniPet")]
        [InlineData("Tool")]
        public void ATypeTheReplicaGivesNoValueLineIsGuessedContiguous(string itemType)
        {
            // Pins a GUESS, not a measurement. FWDekker's Gathering,
            // MiniPet and Tool builders emit no getValue() at all, so
            // neither shape can claim its agreement and no capture of one
            // exists. Contiguous is chosen by nearest body shape; see
            // ValueSitsAfterABlank. Flip this test, not just the table, if
            // the desktop gate measures a blank.
            var lines = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Copper Mining Pick",
                ItemType = itemType,
                Description = "Used to gather from copper ore.",
                VendorValue = 7,
            }).ToPlainLines();

            Assert.NotEqual("", lines[lines.Count - 2]);
            Assert.Equal("7c", lines[lines.Count - 1]);
        }

        [Fact]
        public void AnItemTypeThisModuleHasNeverSeenFallsToTheGenericShape()
        {
            // The type table is inverted on purpose: the API's vocabulary
            // grows, and a new type takes the replica's own fallback
            // rather than silently losing a blank.
            var lines = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Some Future Thing",
                ItemType = "MountSkin",
                VendorValue = 7,
            }).ToPlainLines();

            Assert.Equal("", lines[lines.Count - 2]);
        }

        [Fact]
        public async Task Rune_ShowsItsSixPositionalBonuses()
        {
            var lines = await LinesFor(RealItemJson.RuneOfTheScholar);

            // Header, one blank, then all six positional bonuses - the
            // shape FWDekker's UpgradeComponent builder emits and the one
            // the game shows for an unequipped rune (spec section 3.2).
            Assert.Equal("Superior Rune of the Scholar", lines[0]);
            Assert.Equal("", lines[1]);
            Assert.Equal("(1): +25 Power", lines[2]);
            Assert.Equal("(6): +125 Ferocity", lines[7]);
            Assert.Contains("Rune", lines);
            Assert.Equal("65c", lines[lines.Length - 1]);
        }

        [Fact]
        public async Task SigilAndInfusion_ShowTheirBuffLine()
        {
            Assert.Contains("+5% Damage", await LinesFor(RealItemJson.SigilOfForce));

            var infusion = await LinesFor(RealItemJson.AgonyInfusion);
            // The API reports this fact twice - once as the infix buff
            // description, once as a resolved attribute that renders to the
            // identical string. The game prints it once, and so must we.
            Assert.Equal(1, infusion.Count(l => l == "+1 Agony Resistance"));
        }

        [Fact]
        public void BuffDescriptionSurvivesWhenNoAttributeLineAlreadySaysIt()
        {
            // The other half of the de-duplication above: suppression is
            // exact-match only, so a buff that SUMMARISES several
            // attributes is its own distinct wording and still belongs.
            var stats = new ItemStatBlock
            {
                Name = "Test Upgrade",
                Attributes = new[]
                {
                    new ItemAttributeLine("Power", 5),
                    new ItemAttributeLine("Precision", 5),
                },
                BuffDescription = "+5 Power, +5 Precision",
            };

            var lines = ItemStatTooltipComposer.BuildContent(stats).ToPlainLines().ToArray();

            Assert.Contains("+5 Power", lines);
            Assert.Contains("+5 Precision", lines);
            Assert.Contains("+5 Power, +5 Precision", lines);
        }

        [Fact]
        public async Task FineFood_ShowsItsNourishmentBlockAndDuration()
        {
            var lines = await LinesFor(RealItemJson.LotusFries);

            // No blank under the header: a body that OPENS with the
            // nourishment block runs straight on, measured on steak.png
            // (icon bottom y=37, first text band y=39) and matching
            // FWDekker's Consumable builder, which emits its
            // getConsumableDescription() with no leading break.
            Assert.Equal("Cup of Lotus Fries", lines[0]);
            Assert.Equal("30% Magic Find", lines[1]);
            Assert.Equal("+70 Condition Damage", lines[2]);
            Assert.Equal("+10% Experience from Kills", lines[3]);
            Assert.Equal("Duration: 30 m", lines[4]);
        }

        [Fact]
        public async Task AFoodsFirstNourishmentLineIsWhiteAndItsTrailingEffectsAreGrey()
        {
            // First line white, trailing effect lines grey (~162), measured
            // on the allspice capture (fidelity-audit F7). Never the
            // upgrade-bonus blue - that is measured on runes and sigils
            // only.
            var raw = await RealItemFixtures.ParseOneAsync(RealItemJson.LotusFries);
            var content = ItemStatTooltipComposer.BuildContent(ItemStatBlockFactory.Build(raw));
            var spans = content.Lines.SelectMany(l => l.Spans).ToArray();

            var first = spans.Single(s => s.Text == "30% Magic Find");
            Assert.Equal(TooltipSpanRole.Default, first.Role);

            var trailing = spans
                .Where(s => s.Text == "+70 Condition Damage" ||
                            s.Text == "+10% Experience from Kills")
                .ToArray();

            Assert.Equal(2, trailing.Length);
            Assert.All(trailing, s => Assert.Equal(TooltipSpanRole.Muted, s.Role));
            Assert.DoesNotContain(spans, s => s.Role == TooltipSpanRole.Bonus);
        }

        [Fact]
        public void ARunesBonusLinesKeepTheUpgradeBonusRole()
        {
            var content = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Superior Rune of the Scholar",
                ItemType = "UpgradeComponent",
                SubType = "Rune",
                UpgradeBonuses = new[] { "+25 Power", "+35 Ferocity" },
            });

            var bonuses = content.Lines
                .SelectMany(l => l.Spans)
                .Where(s => s.Text.StartsWith("("))
                .ToArray();

            Assert.Equal(2, bonuses.Length);
            Assert.All(bonuses, s => Assert.Equal(TooltipSpanRole.Bonus, s.Role));
        }

        [Fact]
        public void ABodyThatOpensWithTheIdentityBlockKeepsItsBlankUnderTheHeader()
        {
            // The other side of the rule above, measured on xyaren.png
            // (icon bottom y=34, first text band y=53 - one 16px pitch of
            // empty space).
            var lines = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Toymaker's Bag",
                Rarity = "Exotic",
                ItemType = "Back",
            }).ToPlainLines();

            Assert.Equal("Toymaker's Bag", lines[0]);
            Assert.Equal("", lines[1]);
            Assert.Equal("Exotic", lines[2]);
        }

        [Fact]
        public void ADurationOnlyNourishmentBlockAlsoOpensTheBodyUnderTheHeader()
        {
            // The nourishment block is one block whichever of its two lines
            // the item actually carries.
            var lines = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Timed Snack",
                NourishmentDurationMs = 1800000,
            }).ToPlainLines();

            Assert.Equal("Timed Snack", lines[0]);
            Assert.Equal("Duration: 30 m", lines[1]);
        }

        [Fact]
        public void ABonusRunStillTakesItsBlankEvenWhenNourishmentFollowsIt()
        {
            // The flag is about which block OPENS the body. FWDekker's
            // UpgradeComponent builder breaks before its buffs, so a bonus
            // run keeps the blank even when a nourishment line sits under
            // it (inferred - no unequipped-rune capture exists).
            var lines = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Odd Hybrid",
                BuffDescription = "+5% Damage",
                NourishmentDurationMs = 1800000,
            }).ToPlainLines();

            Assert.Equal("Odd Hybrid", lines[0]);
            Assert.Equal("", lines[1]);
            Assert.Equal("+5% Damage", lines[2]);
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
            Assert.Contains("Double-click to select stats.", lines);
            Assert.Contains("Helm Aquatic Armor", lines);
            Assert.False(await HasCoinSpan(RealItemJson.Rebreather));
        }

        [Fact]
        public void UniqueSitsOnItsOwnLineAboveTheBindingLine()
        {
            var lines = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Unique Thing",
                IsUnique = true,
                Binding = "Account Bound",
            }).ToPlainLines();

            Assert.Equal(lines.IndexOf("Unique") + 1, lines.IndexOf("Account Bound"));
        }

        [Theory]
        [InlineData("Greatsword", "(Two-Handed)")]
        [InlineData("Focus", "(Off Hand)")]
        [InlineData("Trident", "(Aquatic)")]
        [InlineData("LargeBundle", null)]
        public void AWeaponNamesTheHandItIsHeldIn(string subType, string expected)
        {
            var lines = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Weapon",
                ItemType = "Weapon",
                SubType = subType,
            }).ToPlainLines();

            if (expected == null)
            {
                // An unknown weapon type renders no hand line rather than
                // a guessed one.
                Assert.DoesNotContain(lines, l => l.StartsWith("("));
                return;
            }

            Assert.Contains(expected, lines);
        }

        private static async Task<bool> HasCoinSpan(string itemJson)
        {
            var raw = await RealItemFixtures.ParseOneAsync(itemJson);
            return ItemStatTooltipComposer.BuildContent(ItemStatBlockFactory.Build(raw))
                .Lines.SelectMany(l => l.Spans).Any(s => s.IsCoin);
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
        public async Task TheRarityWordCarriesTheRarityColourAndTheRestOfTheIdentityBlockIsWhite()
        {
            // Measured on the 2026-08-25 live captures: the rarity word is
            // drawn in the rarity colour, same as the name line - s07's
            // "Fine" reads (82,146,240), eq-weapon-full's "Legendary"
            // (153,51,255), both non-comparison hovers. Every other
            // identity line stays white; the description's own <c=@flavor>
            // run is the only coloured prose.
            var raw = await RealItemFixtures.ParseOneAsync(RealItemJson.ZojjasWarfists);
            var content = ItemStatTooltipComposer.BuildContent(ItemStatBlockFactory.Build(raw));

            var rarityWord = content.Lines
                .SelectMany(l => l.Spans)
                .Single(s => s.Text == "Ascended");
            Assert.Equal(TooltipSpanRole.Rarity, rarityWord.Role);
            Assert.Equal("Ascended", rarityWord.RarityKey);

            var identity = content.Lines
                .SelectMany(l => l.Spans)
                .Where(s => s.Text == "Gloves Armor" ||
                            s.Text == "Heavy" || s.Text == "Account Bound on Use")
                .ToArray();

            Assert.Equal(3, identity.Length);
            Assert.All(identity, s => Assert.Equal(TooltipSpanRole.Default, s.Role));

            var flavor = content.Lines
                .SelectMany(l => l.Spans)
                .Single(s => s.Text.StartsWith("Crafted in the style"));
            Assert.Equal(TooltipSpanRole.Flavor, flavor.Role);
        }

        [Fact]
        public void ConsecutiveInfusionSlotsAreEachTheirOwnBlankSeparatedBlock()
        {
            // Measured on live/eq-weapon-full.png (2026-08-25): two sigil
            // blocks and two infusion lines each render blank / block /
            // blank, never one contiguous run (fidelity-audit F8).
            var content = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                ItemId = 1,
                Name = "Two-Slot Thing",
                InfusionSlotCount = 2,
            });

            Assert.Equal(new[]
            {
                "Two-Slot Thing",
                "",
                "Infusion Slot",
                "",
                "Infusion Slot",
            }, content.ToPlainLines().ToArray());
        }

        [Fact]
        public async Task TheHandLineIsMutedGreyNotWhite()
        {
            // "(Two-Handed)" measures (160,161,162) on
            // live/eq-weapon-full.png (2026-08-25, lossless) - the game's
            // parenthetical grey, not white.
            var raw = await RealItemFixtures.ParseOneAsync(RealItemJson.Bolt);
            var content = ItemStatTooltipComposer.BuildContent(ItemStatBlockFactory.Build(raw));

            var hand = content.Lines
                .SelectMany(l => l.Spans)
                .Single(s => s.Text == "(Main Hand)");
            Assert.Equal(TooltipSpanRole.Muted, hand.Role);
        }

        [Fact]
        public void AnItemWithNoIconStillOpensWithAHeaderThatDrawsTheEmptySlotSquare()
        {
            var content = ItemStatTooltipComposer.BuildContent(
                new ItemStatBlock { ItemId = 1, Name = "Iconless Thing", IconUrl = null });

            Assert.Equal(TooltipLineKind.Header, content.Lines[0].Kind);
            Assert.Equal("", content.Lines[0].IconUrl);
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
                NourishmentDurationMs = durationMs,
            });

            Assert.Contains(expected, content.ToPlainLines());
        }

        [Fact]
        public void ProfessionRestrictionsAreListedOnOneLine()
        {
            var content = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Restricted Thing",
                Restrictions = new[] { "Guardian", "Warrior" },
            });

            Assert.Contains("Restricted to: Guardian, Warrior", content.ToPlainLines());
        }

        [Fact]
        public void AnEmptyRestrictionListProducesNoLineAtAll()
        {
            var content = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Unrestricted Thing",
                Restrictions = new string[0],
            });

            Assert.DoesNotContain(content.ToPlainLines(), l => l.StartsWith("Restricted"));
        }

        [Fact]
        public void ANegativeAttributeKeepsItsOwnSignRatherThanGainingAPlus()
        {
            var content = ItemStatTooltipComposer.BuildContent(new ItemStatBlock
            {
                Name = "Odd",
                Attributes = new[] { new ItemAttributeLine("Power", -5) },
            });

            Assert.Contains("-5 Power", content.ToPlainLines());
        }
    }
}
