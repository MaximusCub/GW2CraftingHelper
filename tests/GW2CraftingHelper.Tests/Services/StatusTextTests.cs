using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{

    public class StatusTextTests
    {

        [Fact]
        public void Normalize_NonNull_ReturnsSameString()
        {
            Assert.Equal("Updated \u2014 1:00 PM", StatusText.Normalize("Updated \u2014 1:00 PM"));
        }

        [Fact]
        public void Normalize_Null_ReturnsEmpty()
        {
            Assert.Equal("", StatusText.Normalize(null));
        }

        [Fact]
        public void Normalize_Empty_ReturnsEmpty()
        {
            Assert.Equal("", StatusText.Normalize(""));
        }
    }

}
