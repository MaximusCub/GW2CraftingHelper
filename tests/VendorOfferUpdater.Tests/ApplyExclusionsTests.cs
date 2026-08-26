using System.Collections.Generic;
using System.IO;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    // The wiki describes vendor sales a patch removed years ago without
    // marking those pages historical, so a scrape reintroduces them on
    // every refresh. ref/vendor_offer_exclusions.json is the hand-verified
    // refusal list; these tests pin that it is applied, that it is keyed on
    // BOTH merchant and item, and that a missing list is survivable.
    public class ApplyExclusionsTests
    {
        private static VendorOffer MakeOffer(string merchantName, int outputItemId)
        {
            return new VendorOffer
            {
                OfferId = merchantName + ":" + outputItemId,
                MerchantName = merchantName,
                OutputItemId = outputItemId,
                OutputCount = 1,
                CostLines = new List<CostLine>(),
            };
        }

        private static string WriteList(string body)
        {
            string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "vendor_offer_exclusions.json"), body);
            return dir;
        }

        [Fact]
        public void RefusedRow_IsDropped_AndOnlyThatRow()
        {
            string dir = WriteList(@"{ ""exclusions"": [
                { ""merchantName"": ""Battle Master"", ""outputItemId"": 19678 } ] }");

            var offers = new List<VendorOffer>
            {
                MakeOffer("Battle Master", 19678),
                // Same merchant, different item.
                MakeOffer("Battle Master", 12345),
                // Same item, different merchant - the real Gift of Battle
                // path must never be caught by a merchant-scoped refusal.
                MakeOffer("Some Other Vendor", 19678),
            };

            int removed = Program.ApplyExclusions(ref offers, dir);

            Assert.Equal(1, removed);
            Assert.Equal(2, offers.Count);
            Assert.DoesNotContain(
                offers, o => o.MerchantName == "Battle Master" && o.OutputItemId == 19678);
        }

        [Fact]
        public void MissingList_ShipsEverything_RatherThanFailing()
        {
            string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dir);

            var offers = new List<VendorOffer> { MakeOffer("Battle Master", 19678) };
            int removed = Program.ApplyExclusions(ref offers, dir);

            Assert.Equal(0, removed);
            Assert.Single(offers);
        }

        [Fact]
        public void UnreadableList_IsWarnedAboutRatherThanThrowing()
        {
            string dir = WriteList("{ this is not json ");

            var offers = new List<VendorOffer> { MakeOffer("Battle Master", 19678) };
            int removed = Program.ApplyExclusions(ref offers, dir);

            Assert.Equal(0, removed);
            Assert.Single(offers);
        }
    }
}
