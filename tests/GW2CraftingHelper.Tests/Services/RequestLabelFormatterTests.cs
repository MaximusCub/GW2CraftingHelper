using System.Collections.Generic;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// W3B review-fix: pure requestLabel capping - Blish-free, exercises
    /// the real RequestLabelFormatter production code
    /// CraftingPlanView.TriggerGenerate calls before handing requestLabel
    /// to CraftingPlanPipeline.GenerateStructuredAsync.
    /// </summary>
    public class RequestLabelFormatterTests
    {
        [Fact]
        public void Format_NullList_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, RequestLabelFormatter.Format(null));
        }

        [Fact]
        public void Format_EmptyList_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, RequestLabelFormatter.Format(new List<string>()));
        }

        [Fact]
        public void Format_SingleEntry_ReturnsEntryUnchanged()
        {
            var entries = new List<string> { "Orrax Manifested x1" };
            Assert.Equal("Orrax Manifested x1", RequestLabelFormatter.Format(entries));
        }

        [Fact]
        public void Format_ExactlyThreeEntries_JoinsAllWithNoSuffix()
        {
            var entries = new List<string> { "A x1", "B x2", "C x3" };
            Assert.Equal("A x1, B x2, C x3", RequestLabelFormatter.Format(entries));
        }

        [Fact]
        public void Format_FourEntries_CapsToFirstThreePlusOneMore()
        {
            var entries = new List<string> { "A x1", "B x2", "C x3", "D x4" };
            Assert.Equal("A x1, B x2, C x3, +1 more", RequestLabelFormatter.Format(entries));
        }

        [Fact]
        public void Format_TwentyEntries_CapsToFirstThreePlusSeventeenMore()
        {
            var entries = new List<string>();
            for (int i = 1; i <= 20; i++)
            {
                entries.Add($"Item{i} x1");
            }

            Assert.Equal("Item1 x1, Item2 x1, Item3 x1, +17 more", RequestLabelFormatter.Format(entries));
        }

        [Fact]
        public void Format_RealItemNames_PreservesOrderAndWording()
        {
            var entries = new List<string>
            {
                "Orrax Manifested x1",
                "Bolt of Damask x5",
                "Vial of Powerful Blood x250",
                "Pile of Bloodstone Dust x10"
            };

            Assert.Equal(
                "Orrax Manifested x1, Bolt of Damask x5, Vial of Powerful Blood x250, +1 more",
                RequestLabelFormatter.Format(entries));
        }
    }
}
