using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The transmuted-name path end to end: which copies let a row take a
    /// skin's name, what the row is then called, what still finds it, and
    /// what the tooltip reads. The stat block comes from verbatim live
    /// /v2/items JSON through the real parser and the real factory, so the
    /// tooltip assertions are about the game's own data.
    /// </summary>
    public class TransmutedNameTests
    {
        private const int Warfists = 48074;

        private static SnapshotItemEntry Stack(
            string source, string name = "Zojja's Warfists", int skinId = 0, string skinName = "")
        {
            return new SnapshotItemEntry
            {
                ItemId = Warfists,
                Name = name,
                Count = 1,
                Source = source,
                SkinId = skinId,
                SkinName = skinName,
            };
        }

        private static SnapshotItemEntry Skinned(string source, int skinId, string skinName)
        {
            return Stack(source, skinId: skinId, skinName: skinName);
        }

        private static List<SnapshotSearchRow> Rows(
            IReadOnlyList<SnapshotItemEntry> items,
            string searchText,
            IReadOnlyDictionary<int, TransmutedItemNames> transmuted)
        {
            return SnapshotSearchResultBuilder.BuildItemRows(
                SnapshotSearchResultBuilder.BuildRepresentativeIndex(items),
                new AccountItemIndex(items),
                searchText,
                new SnapshotSourceFilter(),
                activeCharacterName: null,
                transmutedNames: transmuted);
        }

        // ---- TransmutedNameIndex ----
        [Fact]
        public void Build_OneSkinnedCopy_ReportsTheSkinAsTheNameToShow()
        {
            var index = TransmutedNameIndex.Build(new List<SnapshotItemEntry>
            {
                Skinned("Bank", 5432, "Glyphic Gauntlets"),
            });

            Assert.Equal("Glyphic Gauntlets", index[Warfists].DisplayName);
            Assert.Equal(new[] { "Glyphic Gauntlets" }, index[Warfists].AllNames);
        }

        [Fact]
        public void Build_EveryCopyWearsTheSameSkin_StillReportsIt()
        {
            var index = TransmutedNameIndex.Build(new List<SnapshotItemEntry>
            {
                Skinned("Bank", 5432, "Glyphic Gauntlets"),
                Skinned("Equipped:Taimi", 5432, "Glyphic Gauntlets"),
            });

            Assert.Equal("Glyphic Gauntlets", index[Warfists].DisplayName);
        }

        [Fact]
        public void Build_NoCopyIsTransmuted_OmitsTheItem()
        {
            var index = TransmutedNameIndex.Build(new List<SnapshotItemEntry>
            {
                Stack("Bank"),
                Stack("Character:Taimi"),
            });

            Assert.Empty(index);
        }

        [Fact]
        public void Build_CopiesWearDifferentSkins_NamesNeitherButKeepsBoth()
        {
            var index = TransmutedNameIndex.Build(new List<SnapshotItemEntry>
            {
                Skinned("Bank", 5432, "Glyphic Gauntlets"),
                Skinned("Equipped:Taimi", 99, "Chaos Gloves"),
            });

            Assert.Equal("", index[Warfists].DisplayName);
            Assert.Equal(new[] { "Glyphic Gauntlets", "Chaos Gloves" }, index[Warfists].AllNames);
        }

        [Fact]
        public void Build_OneBareCopyAndOneSkinnedCopy_NamesNeitherButKeepsTheSkin()
        {
            var index = TransmutedNameIndex.Build(new List<SnapshotItemEntry>
            {
                Skinned("Bank", 5432, "Glyphic Gauntlets"),
                Stack("Character:Taimi"),
            });

            Assert.Equal("", index[Warfists].DisplayName);
            Assert.Equal(new[] { "Glyphic Gauntlets" }, index[Warfists].AllNames);
        }

        [Fact]
        public void Build_SkinNamedAfterTheItemItself_IsNotATransmutation()
        {
            var index = TransmutedNameIndex.Build(new List<SnapshotItemEntry>
            {
                Skinned("Bank", 116, "Zojja's Warfists"),
            });

            Assert.Empty(index);
        }

        [Fact]
        public void Build_SkinIdWithNoResolvedName_ReadsAsNoSkin()
        {
            var index = TransmutedNameIndex.Build(new List<SnapshotItemEntry>
            {
                Skinned("Bank", 5432, ""),
            });

            Assert.Empty(index);
        }

        [Fact]
        public void Build_NullOrEmptyItems_ReturnsEmpty()
        {
            Assert.Empty(TransmutedNameIndex.Build(null));
            Assert.Empty(TransmutedNameIndex.Build(new List<SnapshotItemEntry>()));
        }

        [Fact]
        public void Build_SkipsNullEntriesAndIdlessRows()
        {
            var index = TransmutedNameIndex.Build(new List<SnapshotItemEntry>
            {
                null,
                new SnapshotItemEntry { ItemId = 0, SkinName = "Glyphic Gauntlets" },
                Skinned("Bank", 5432, "Glyphic Gauntlets"),
            });

            Assert.Equal(new[] { Warfists }, index.Keys.ToArray());
        }

        // ---- the name a row shows, and what finds it ----
        [Fact]
        public void BuildItemRows_TransmutedItem_RowIsNamedAfterTheSkin()
        {
            var items = new List<SnapshotItemEntry> { Skinned("Bank", 5432, "Glyphic Gauntlets") };
            var row = Assert.Single(Rows(items, "", TransmutedNameIndex.Build(items)));

            Assert.Equal("Glyphic Gauntlets", row.Name);
            Assert.Equal("Glyphic Gauntlets", row.SkinName);
        }

        [Fact]
        public void BuildItemRows_TransmutedItem_IsFoundByTheNameTheGameShows()
        {
            var items = new List<SnapshotItemEntry> { Skinned("Bank", 5432, "Glyphic Gauntlets") };
            var row = Assert.Single(Rows(items, "Glyphic", TransmutedNameIndex.Build(items)));

            Assert.Equal("Glyphic Gauntlets", row.Name);
        }

        [Fact]
        public void BuildItemRows_TransmutedItem_IsStillFoundByTheItemsOwnName()
        {
            var items = new List<SnapshotItemEntry> { Skinned("Bank", 5432, "Glyphic Gauntlets") };
            var row = Assert.Single(Rows(items, "Zojja", TransmutedNameIndex.Build(items)));

            Assert.Equal("Glyphic Gauntlets", row.Name);
        }

        [Fact]
        public void BuildItemRows_CopiesWearDifferentSkins_RowKeepsTheItemsOwnName()
        {
            var items = new List<SnapshotItemEntry>
            {
                Skinned("Bank", 5432, "Glyphic Gauntlets"),
                Skinned("Equipped:Taimi", 99, "Chaos Gloves"),
            };
            var index = TransmutedNameIndex.Build(items);

            var row = Assert.Single(Rows(items, "", index));
            Assert.Equal("Zojja's Warfists", row.Name);
            Assert.Equal("", row.SkinName);

            // Neither spelling may lose an item the row does not name.
            Assert.Equal("Zojja's Warfists", Assert.Single(Rows(items, "Glyphic", index)).Name);
            Assert.Equal("Zojja's Warfists", Assert.Single(Rows(items, "Chaos", index)).Name);
        }

        [Fact]
        public void BuildItemRows_WithoutTheIndex_EveryRowKeepsTheItemsOwnName()
        {
            var items = new List<SnapshotItemEntry> { Skinned("Bank", 5432, "Glyphic Gauntlets") };

            var row = Assert.Single(Rows(items, "", null));
            Assert.Equal("Zojja's Warfists", row.Name);
            Assert.Equal("", row.SkinName);
            Assert.Empty(Rows(items, "Glyphic", null));
        }

        // ---- the tooltip block ----
        private static async Task<string[]> Tooltip(string skinName)
        {
            var raws = await RealItemFixtures.ParseAsync(RealItemJson.ZojjasWarfists);
            var stats = ItemStatBlockFactory.Build(raws[Warfists]);
            return ItemStatTooltipComposer
                .BuildContent(stats, SocketedUpgradeView.None, skinName)
                .ToPlainLines()
                .ToArray();
        }

        [Fact]
        public async Task Tooltip_TransmutedItem_HeadingIsTheSkinsName()
        {
            var lines = await Tooltip("Glyphic Gauntlets");

            Assert.Equal("Glyphic Gauntlets", lines[0]);
            Assert.DoesNotContain("Zojja's Warfists", lines[0]);
        }

        [Fact]
        public async Task Tooltip_TransmutedItem_NamesTheItemUnderATransmutedLine()
        {
            var lines = await Tooltip("Glyphic Gauntlets");

            int at = System.Array.IndexOf(lines, "Transmuted");
            Assert.True(at > 0, "the tooltip carries no Transmuted line");
            Assert.Equal("Zojja's Warfists", lines[at + 1]);

            // One blank row above the pair and one below it, and the
            // identity block follows: measured on the ascended-sword
            // capture ItemStatTooltipComposer.BuildTransmutedBlock cites.
            Assert.Equal("", lines[at - 1]);
            Assert.Equal("", lines[at + 2]);
            Assert.Equal("Ascended", lines[at + 3]);

            // Below the socket lines, not above them.
            Assert.True(System.Array.IndexOf(lines, "Unused Infusion Slot") < at);
        }

        [Fact]
        public async Task Tooltip_ItemWearingItsOwnLook_HasNoTransmutedLine()
        {
            foreach (string skin in new[] { null, "", "Zojja's Warfists" })
            {
                var lines = await Tooltip(skin);

                Assert.Equal("Zojja's Warfists", lines[0]);
                Assert.DoesNotContain("Transmuted", lines);
            }
        }
    }
}
