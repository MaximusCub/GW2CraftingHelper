using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class MultiItemHeaderTooltipComposerTests
    {
        private static List<PlanHeaderItem> Items(int count, int from = 1)
        {
            var items = new List<PlanHeaderItem>(count);
            for (int i = 0; i < count; i++)
            {
                int n = from + i;
                items.Add(new PlanHeaderItem
                {
                    ItemId = n,
                    Name = "Item " + n,
                    IconUrl = "icon" + n + ".png",
                    Rarity = "Exotic",
                });
            }

            return items;
        }

        [Fact]
        public void NothingHidden_SaysNothing()
        {
            var items = Items(3);

            Assert.True(MultiItemHeaderTooltipComposer
                .BuildHiddenItemsContent(items, firstHidden: 3).IsEmpty);
            Assert.True(MultiItemHeaderTooltipComposer
                .BuildHiddenItemsContent(items, firstHidden: 9).IsEmpty);
            Assert.True(MultiItemHeaderTooltipComposer
                .BuildHiddenItemsContent(null, firstHidden: 0).IsEmpty);
        }

        [Fact]
        public void ListsOnlyTheItemsFromFirstHiddenOnward()
        {
            var content = MultiItemHeaderTooltipComposer
                .BuildHiddenItemsContent(Items(5), firstHidden: 3);

            Assert.Equal(new[] { "Item 4", "Item 5" }, content.ToPlainLines());
        }

        [Fact]
        public void EveryListedItemIsAnIconAndNameHeaderRow()
        {
            // The row kind is the contract: it is what draws the item's own
            // icon and colours its name by rarity, exactly as every other
            // item tooltip in the module opens.
            var content = MultiItemHeaderTooltipComposer
                .BuildHiddenItemsContent(Items(3), firstHidden: 1);

            Assert.All(content.Lines, l => Assert.Equal(TooltipLineKind.Header, l.Kind));
            Assert.Equal(new[] { "icon2.png", "icon3.png" }, content.Lines.Select(l => l.IconUrl));
            Assert.All(
                content.Lines.SelectMany(l => l.Spans),
                span =>
                {
                    Assert.Equal(TooltipSpanRole.Rarity, span.Role);
                    Assert.Equal("Exotic", span.RarityKey);
                });
        }

        [Fact]
        public void AnItemWithNoIconOrRarity_StillGetsItsOwnRow()
        {
            var items = new List<PlanHeaderItem>
            {
                new PlanHeaderItem { ItemId = 1, Name = "Shown" },
                new PlanHeaderItem { ItemId = 2, Name = "Unknown Item" },
            };

            var content = MultiItemHeaderTooltipComposer.BuildHiddenItemsContent(items, 1);

            var line = Assert.Single(content.Lines);
            Assert.Equal(TooltipLineKind.Header, line.Kind);
            // HeaderLine normalises a null url to empty so the row always
            // draws the neutral empty-slot square rather than nothing.
            Assert.Equal("", line.IconUrl);
            Assert.Null(Assert.Single(line.Spans).RarityKey);
        }

        [Fact]
        public void ExactlyTheCap_IsStillListedInFull()
        {
            int cap = MultiItemHeaderTooltipComposer.MaxListedItems;

            var content = MultiItemHeaderTooltipComposer
                .BuildHiddenItemsContent(Items(cap), firstHidden: 0);

            Assert.Equal(cap, content.Lines.Count);
            Assert.All(content.Lines, l => Assert.Equal(TooltipLineKind.Header, l.Kind));
        }

        [Fact]
        public void PastTheCap_TheTailBecomesACount()
        {
            // Uncapped, a large batch's list would run off the bottom of
            // the screen: the rich surface clamps a tooltip's position, not
            // its height.
            int cap = MultiItemHeaderTooltipComposer.MaxListedItems;

            var content = MultiItemHeaderTooltipComposer
                .BuildHiddenItemsContent(Items(cap + 4), firstHidden: 0);

            Assert.Equal(cap + 1, content.Lines.Count);
            Assert.Equal("and 4 more items", content.ToPlainLines().Last());

            // The listed rows are the FIRST cap items, in request order.
            Assert.Equal("Item 1", content.ToPlainLines()[0]);
            Assert.Equal("Item " + cap, content.ToPlainLines()[cap - 1]);
        }

        [Fact]
        public void OneItemPastTheCap_UsesTheSingular()
        {
            int cap = MultiItemHeaderTooltipComposer.MaxListedItems;

            var content = MultiItemHeaderTooltipComposer
                .BuildHiddenItemsContent(Items(cap + 1), firstHidden: 0);

            Assert.Equal("and 1 more item", content.ToPlainLines().Last());
        }

        [Fact]
        public void TheCapCountsHiddenItems_NotTheWholeBatch()
        {
            // 20 items with 15 of them already drawn as icons leaves 5
            // hidden - well under the cap, so nothing is summarised away.
            int cap = MultiItemHeaderTooltipComposer.MaxListedItems;

            var content = MultiItemHeaderTooltipComposer
                .BuildHiddenItemsContent(Items(20), firstHidden: 15);

            Assert.Equal(5, content.Lines.Count);
            Assert.True(cap >= 5);
            Assert.All(content.Lines, l => Assert.Equal(TooltipLineKind.Header, l.Kind));
        }
    }
}
