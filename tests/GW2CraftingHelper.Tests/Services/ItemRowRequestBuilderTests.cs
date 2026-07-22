using System.Collections.Generic;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// M35 (gw2efficiency parity - multi-item plans): pure row-list state
    /// transitions for the multi-item input strip - Blish-free, exercises
    /// the real ItemRowRequestBuilder production code CraftingPlanView's
    /// TriggerGenerate/row builders call.
    /// </summary>
    public class ItemRowRequestBuilderTests
    {
        private static ItemRowRequestBuilder.RowInput Row(int? itemId, string qtyText = "1")
        {
            return new ItemRowRequestBuilder.RowInput(itemId, qtyText);
        }

        [Fact]
        public void CanRemoveRow_SingleRow_ReturnsFalse()
        {
            Assert.False(ItemRowRequestBuilder.CanRemoveRow(1));
        }

        [Fact]
        public void CanRemoveRow_TwoOrMoreRows_ReturnsTrue()
        {
            Assert.True(ItemRowRequestBuilder.CanRemoveRow(2));
            Assert.True(ItemRowRequestBuilder.CanRemoveRow(5));
        }

        [Fact]
        public void CanRemoveRow_ZeroRows_ReturnsFalse()
        {
            // Defensive: never observed in practice (Build() always seeds
            // one row), but must not claim removability of nothing.
            Assert.False(ItemRowRequestBuilder.CanRemoveRow(0));
        }

        [Fact]
        public void Build_NullRows_ReturnsEmptyList()
        {
            var result = ItemRowRequestBuilder.Build(null);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Build_EmptyRowList_ReturnsEmptyList()
        {
            var result = ItemRowRequestBuilder.Build(new List<ItemRowRequestBuilder.RowInput>());
            Assert.Empty(result);
        }

        [Fact]
        public void Build_RowWithNoItemSelected_IsSkipped()
        {
            var rows = new List<ItemRowRequestBuilder.RowInput> { Row(null, "3") };
            var result = ItemRowRequestBuilder.Build(rows);
            Assert.Empty(result);
        }

        [Fact]
        public void Build_SingleSelectedRow_MapsIdAndQuantity()
        {
            var rows = new List<ItemRowRequestBuilder.RowInput> { Row(19721, "5") };
            var result = ItemRowRequestBuilder.Build(rows);

            Assert.Single(result);
            Assert.Equal(19721, result[0].ItemId);
            Assert.Equal(5, result[0].Quantity);
        }

        [Fact]
        public void Build_MultipleSelectedRows_PreservesOrder()
        {
            var rows = new List<ItemRowRequestBuilder.RowInput>
            {
                Row(1, "1"),
                Row(2, "2"),
                Row(3, "3")
            };
            var result = ItemRowRequestBuilder.Build(rows);

            Assert.Equal(3, result.Count);
            Assert.Equal(1, result[0].ItemId);
            Assert.Equal(2, result[1].ItemId);
            Assert.Equal(3, result[2].ItemId);
        }

        [Fact]
        public void Build_MixOfSelectedAndEmptyRows_SkipsOnlyEmptyOnes()
        {
            var rows = new List<ItemRowRequestBuilder.RowInput>
            {
                Row(10, "2"),
                Row(null, "1"),
                Row(20, "4")
            };
            var result = ItemRowRequestBuilder.Build(rows);

            Assert.Equal(2, result.Count);
            Assert.Equal(10, result[0].ItemId);
            Assert.Equal(20, result[1].ItemId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not a number")]
        [InlineData("0")]
        [InlineData("-5")]
        public void Build_InvalidQuantityText_DefaultsToOne(string qtyText)
        {
            var rows = new List<ItemRowRequestBuilder.RowInput> { Row(1, qtyText) };
            var result = ItemRowRequestBuilder.Build(rows);

            Assert.Single(result);
            Assert.Equal(1, result[0].Quantity);
        }

        [Fact]
        public void Build_ValidQuantityText_ParsesExactly()
        {
            var rows = new List<ItemRowRequestBuilder.RowInput> { Row(1, "250") };
            var result = ItemRowRequestBuilder.Build(rows);

            Assert.Equal(250, result[0].Quantity);
        }
    }
}
