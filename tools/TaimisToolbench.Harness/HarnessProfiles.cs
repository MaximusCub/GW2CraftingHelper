using System;
using System.Collections.Generic;
using System.Linq;

namespace TaimisToolbench.Harness
{
    /// <summary>
    /// The harness's built-in item lists. Kept out of Program.cs so adding
    /// a profile is an edit to a table rather than to the argument parser.
    /// </summary>
    internal static class HarnessProfiles
    {
        public static List<ProfileItem> GetProfileItems(int profile, bool live)
        {
            switch (profile)
            {
                case 1:
                    var items = new List<ProfileItem>
                    {
                        new ProfileItem
                        {
                            Name = "Gift of Fortune",
                            ItemId = 19626,
                            Quantity = 1,
                            RequiresLive = false,
                        },
                    };
                    if (live)
                    {
                        items.Add(new ProfileItem
                        {
                            Name = "Zojja's Claymore",
                            ItemId = 46762,
                            Quantity = 1,
                            RequiresLive = true,
                        });
                    }

                    return items;
                case 2:
                    return new List<ProfileItem>
                    {
                        new ProfileItem
                        {
                            Name = "Exordium",
                            ItemId = 90551,
                            Quantity = 1,
                            RequiresLive = false,
                        },
                    };
                case 3:
                    // Klobjarne Geirr is the
                    // concrete, currently-generatable plan the milestone's
                    // research report identified as reaching Homestead
                    // Refinement - via Gift of the Homesteader -> Gift of
                    // Embracing Refuge -> 250 each Refined Homestead
                    // Metal/Wood/Fiber (docs/research/m37-r1-homestead.md
                    // Section 3.6). Use with --homestead-tier to compare
                    // decisions/quantities at tier 0 vs tier 2.
                    return new List<ProfileItem>
                    {
                        new ProfileItem
                        {
                            Name = "Klobjarne Geirr",
                            ItemId = 103815,
                            Quantity = 1,
                            RequiresLive = false,
                        },
                    };
                default:
                    return LegendaryProfile(profile);
            }
        }

        /// <summary>
        /// One representative item per major legendary class, plus a sweep
        /// (profile 30) that runs all of them in one process so
        /// --classify can rank blockers by how many trees they appear in.
        /// Every id was verified against ref/item_name_seed.json or
        /// /v2/items before being added; the two that the name seed does
        /// not carry (83394, 83348) came from the wiki's own
        /// "output item id" recipe field and were then confirmed against
        /// /v2/items.
        /// </summary>
        private static List<ProfileItem> LegendaryProfile(int profile)
        {
            var catalogue = new List<Tuple<int, string, int>>
            {
                Tuple.Create(10, "Twilight (Gen 1 weapon)", 30704),
                Tuple.Create(11, "Astralaria (Gen 2 weapon)", 76158),
                Tuple.Create(12, "Aurene's Fang (Gen 3 weapon)", 95675),
                Tuple.Create(13, "Klobjarne Geirr (Janthir spear)", 103815),
                Tuple.Create(14, "Obsidian Heavy Breastplate (PvE open world armour)", 101521),
                Tuple.Create(15, "Perfected Envoy Vestments (raid armour)", 80190),
                Tuple.Create(16, "Triumphant Hero's Breastplate (WvW armour)", 83394),
                Tuple.Create(17, "Ardent Glorious Breastplate (PvP armour)", 83348),
                Tuple.Create(18, "Eikasia, Mists-Grasper (fractal armour)", 105171),
                Tuple.Create(19, "Selachimorpha Container (aquabreather)", 105743),
                Tuple.Create(20, "Aurora (trinket)", 81908),
                Tuple.Create(21, "Conflux (trinket)", 93105),
                Tuple.Create(22, "Prismatic Champion's Regalia (achievement trinket)", 95380),
                Tuple.Create(23, "Ad Infinitum (back item)", 74155),
                Tuple.Create(24, "Warbringer (WvW back item)", 81462),
                Tuple.Create(25, "Legendary Rune", 91536),
                Tuple.Create(26, "Legendary Sigil", 91505),
                Tuple.Create(27, "Legendary Relic", 101582),
            };

            if (profile == 30)
            {
                return catalogue
                    .Select(entry => new ProfileItem
                    {
                        Name = entry.Item2,
                        ItemId = entry.Item3,
                        Quantity = 1,
                        RequiresLive = false,
                    })
                    .ToList();
            }

            foreach (var entry in catalogue)
            {
                if (entry.Item1 == profile)
                {
                    return new List<ProfileItem>
                    {
                        new ProfileItem
                        {
                            Name = entry.Item2,
                            ItemId = entry.Item3,
                            Quantity = 1,
                            RequiresLive = false,
                        },
                    };
                }
            }

            return null;
        }
    }
}
