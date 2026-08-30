using System.Collections.Generic;
using System.IO;
using System.Linq;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    // The wiki describes vendor sales a patch removed years ago, sometimes
    // on pages not marked historical and sometimes on pages whose marker the
    // SMW vendor query never reads, so a scrape reintroduces them on every
    // refresh. ref/vendor_offer_exclusions.json is the hand-verified refusal
    // list; these tests pin that it is applied, that an entry carrying an
    // outputItemId is keyed on BOTH merchant and item, that an entry without
    // one refuses a whole merchant, and that a missing list is survivable.
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
        public void MerchantWithoutItemId_DropsEveryRowOfThatMerchantOnly()
        {
            string dir = WriteList(@"{ ""exclusions"": [
                { ""merchantName"": ""Battle Historian"" } ] }");

            var offers = new List<VendorOffer>
            {
                MakeOffer("Battle Historian", 46733),
                MakeOffer("Battle Historian", 46735),
                MakeOffer("Battle Historian", 19925),
                // The live successor sells overlapping stock; a vendor-wide
                // refusal must not reach it.
                MakeOffer("Skirmish Supervisor", 19925),
            };

            int removed = Program.ApplyExclusions(ref offers, dir);

            Assert.Equal(3, removed);
            Assert.Equal("Skirmish Supervisor", Assert.Single(offers).MerchantName);
        }

        [Fact]
        public void NonNumericItemId_DropsNothing_RatherThanTheWholeMerchant()
        {
            // A mistyped id must not silently widen into the merchant-wide
            // form, which would delete every row the vendor sells.
            string dir = WriteList(@"{ ""exclusions"": [
                { ""merchantName"": ""Battle Master"", ""outputItemId"": ""19678"" } ] }");

            var offers = new List<VendorOffer>
            {
                MakeOffer("Battle Master", 19678),
                MakeOffer("Battle Master", 12345),
            };

            int removed = Program.ApplyExclusions(ref offers, dir);

            Assert.Equal(0, removed);
            Assert.Equal(2, offers.Count);
        }

        [Fact]
        public void BlankMerchantName_DropsNothing_NotEveryUnnamedRow()
        {
            string dir = WriteList(@"{ ""exclusions"": [ { ""merchantName"": """" } ] }");

            var offers = new List<VendorOffer>
            {
                MakeOffer("Battle Master", 19678),
                new VendorOffer
                {
                    OfferId = "no-merchant",
                    MerchantName = null,
                    OutputItemId = 19678,
                    OutputCount = 1,
                    CostLines = new List<CostLine>(),
                },
            };

            int removed = Program.ApplyExclusions(ref offers, dir);

            Assert.Equal(0, removed);
            Assert.Equal(2, offers.Count);
        }

        [Fact]
        public void ShippedList_RefusesTheBattleHistorian_VendorWide()
        {
            // Against the real ref/ file, not a fixture: an entry that has
            // stopped matching (a renamed merchant, a typo) removes nothing
            // and reads exactly like a clean refresh.
            var offers = new List<VendorOffer>
            {
                MakeOffer("Battle Historian", 46733),
                MakeOffer("Battle Master", 19678),
                // A merchant whose wiki page has a live primary article
                // AND a historical parenthesised variant: only the exact
                // refused name goes, or the live raid vendor goes with it.
                MakeOffer("Scholar Glenna (Gaeting Crystal)", 86094),
                MakeOffer("Scholar Glenna (Hall of Chains)", 86094),
                MakeOffer("Skirmish Supervisor", 46733),
            };

            int removed = Program.ApplyExclusions(ref offers, RepoRefDirectory());

            Assert.Equal(3, removed);
            Assert.Equal(
                new[] { "Scholar Glenna (Hall of Chains)", "Skirmish Supervisor" },
                offers.Select(o => o.MerchantName).OrderBy(n => n).ToArray());
        }

        // Walks to the shipped list itself rather than to a ".git" marker,
        // which is a FILE, not a directory, in a git worktree.
        private static string RepoRefDirectory()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null &&
                   !File.Exists(Path.Combine(
                       dir.FullName, "ref", "vendor_offer_exclusions.json")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "ref");
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
