> **Milestone record - 2026-08-29, branch `w5-deadvendors`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## The Battle Historian: a removed WvW vendor pricing legendary materials at zero (w5-deadvendors)

A legendary plan sweep reported Dragonite Ore and Empyreal Fragment routing
through a WvW vendor charging WvW Tournament Claim Tickets, an
account-bound item with no current source, at an effective cost of zero.
Confirmed in the shipped data: `Battle Historian` held 49 rows in
`ref/vendor_offers.json`, every one of them priced solely in item 66352,
which carries no coin valuation on either side of `CurrencyValuation`, so
each row ranked as free and undercut every real route.

**The premise of the report was half wrong, and the half that was wrong
matters.** The brief assumed this was the Gift of Battle case again - a
wiki page that is not marked historical, which the scrape therefore cannot
distinguish from a live one. The Battle Historian page IS marked
historical, and has been since long before this dataset was built:

| source | value |
|---|---|
| `Battle_Historian?action=raw`, NPC infobox | `status = Historical` |
| SMW `browsebysubject` on the same page | `Has_availability = Historical`, `Is_historical = t`, `_INST` includes `Historical_NPCs` |
| page text | "The Battle Historian was replaced with the [[Skirmish Supervisor]] in the [[June 2017 Competitive Feature Pack]]." |
| `WvW_Tournament_Claim_Ticket?action=raw`, item infobox | `status = discontinued`, `id = 66352` |
| same page | "In the June 2017 Competitive Feature Pack, these were replaced with WvW Skirmish Claim Tickets. Using the WvW Tournament Claim Ticket will turn it into a WvW Skirmish Claim Ticket." |
| same page, Contained in | every listed reward chest is marked historical or discontinued; the last award was the WvW Spring Tournament 2014 chest |

The ticket is not quite unheld - EU accounts were mailed 20 of them in
October 2021 as outage compensation - but there is no vendor to spend them
at, and consuming one converts it to a Skirmish Claim Ticket. The live
successor is already in this dataset: `Skirmish Supervisor`, 418 rows,
including the same Hero's and Mistforged Hero's weapons, the six WvW
infusions and the Mini Dolyak.

So the scrape's blind spot is not "the wiki does not say". It is that
`WikiSmwClient`'s vendor query asks for `[[Sells item::+]]` subobjects and
never reads the parent page's `Is historical` flag, so a removed vendor's
sales arrive looking exactly like a live one's.

**What was removed.** One vendor-wide exclusion entry, applied through
`Program.ApplyExclusions` and written with the tool's own serializer.
`--diff-summary` against the pre-change file: 59,414 -> 59,365, `removed:
49`, and `added`, `repriced`, `retagged`, `rehashed` all zero. The 49 rows
are 49 distinct items: 19 Mistforged Hero's weapons, 19 Hero's weapons, 6
WvW infusions, Obsidian Shard, Gift of Heroes, Dragonite Ore, Empyreal
Fragment and the Mini Dolyak.

**The cost, stated rather than glossed.** Five of those 49 items had no
other row in the dataset and now resolve to `UnknownSource` - no vendor
offer, no recipe in `ref/recipes_seed.json` or
`ref/mystic_forge_recipes.json`, and `NoSell` on `/v2/items` so no Trading
Post fallback either. Cross-checked against the wiki rather than against
this dataset alone: `[[Sells item::<name>]]` returns the Battle Historian
and nothing else for all five, so no live vendor was missed (the same
query returns 50 vendors for Obsidian Shard, which is the control):

- **Dragonite Ore (46733)** and **Empyreal Fragment (46735)**, ingredients
  in 9 and 10 recipes in `ref/recipes_seed.json` plus 2 each in
  `ref/mystic_forge_recipes.json`, including the Mystic Forge gifts that
  sit in legendary trees. No live vendor sells either; they come from
  world content. Their previous "route" was 40 or 50 units for 25 tickets
  at a vendor that has not existed since 2017, valued at zero - a plan that
  said "buy these" when nothing can be bought. `UnknownSource` is the
  honest answer, and it is the same answer the module already gives for
  every other world-content material.
- **Gift of Heroes (43244)**, itself `status = discontinued` on the wiki
  with an empty Sold by list. Nothing sells it any more.
- **Hero's Harpoon Gun (64283)** and **Hero's Trident (64285)**. The wiki
  notes on both: "Prior to the June 2017 Competitive Feature Pack the
  weapon was sold by Battle Historian." The Skirmish Supervisor sells the
  other 17 Hero's weapons in this dataset but not these two, matching the
  wiki's own tables.

**So the hints ship with the exclusion, not after it.** An honest UNKNOWN
still beats a confident lie, but a bare UNKNOWN badge with no text is a
worse display than the wrong vendor route it replaces, and there was no
reason to ship those two states one behind the other.
`ref/acquisition_hints_seed.json` gains five rows, each read off the
item's own wiki page:

| item | badge | what the hint says |
|---|---|---|
| Dragonite Ore (46733) | `WORLD` | gathering nodes, world boss chests (3-30), reward-track loot boxes (the WvW Hero Weapon Box gives 50) |
| Empyreal Fragment (46735) | `CHESTS` | any open-world, jumping puzzle or mini-dungeon chest gives 2-9; dungeon explorable paths 20 for each of the first two completions per day; Cracked Fractal Encryptions 15 or 50 |
| Gift of Heroes (43244) | `REMOVED` | not obtainable at all - the item is `status = discontinued` and the Battle Historian was its only seller; held copies still upgrade Hero's weapons in the Mystic Forge |
| Hero's Harpoon Gun (64283) | `WVW` | chosen from the Hero Weapon Box, the final reward of the WvW Hero Weapon Reward Track, or a WvW Exclusives Choice Chest |
| Hero's Trident (64285) | `WVW` | same box; the skirmish merchants stock 17 of the 19 Hero's weapons but not these two |

None of the five badges collides with a module-owned source pill
(`DecisionPillPlanner.IsReservedSourceBadgeText`), and none of the five
items has a shipped vendor offer any more, so
`AcquisitionHintSeedVendorAgreementTests`' pinned population of
hinted-items-that-also-have-offers is unchanged at {105804, 106712,
106986}. The seed's own count literal moved 10 -> 15, which is the guard
working: its comment says an eleventh row is "exactly the edit that
should stop and read this test".

## A second removed vendor: Scholar Glenna (Gaeting Crystal)

The same defect, one page over, and it shows why the merchant NAME is
worth reading. `Scholar Glenna` is live - she sells for Magnetite Shards
in every raid, and this dataset carries her current inventory as the
per-raid pages `Scholar Glenna (Hall of Chains)` (22 rows), `(Mythwright
Gambit)` (48) and `(The Key of Ahdashim)` (65). `Scholar Glenna (Gaeting
Crystal)` is a different page: `status = historical`, `Is historical = t`,
opening "This page lists the items formerly available from Scholar Glenna
... The following offers were removed and replaced with equivalent
offers." The three locations on its rows are exactly the three raids whose
live pages already ship.

The 2022-07-19 patch note covers both halves at once: "Gaeting Crystals
have been retired and players will automatically have any Gaeting Crystals
in their possession exchanged for an equal amount of Magnetite Shards" and
"Merchants who previously traded items for Gaeting Crystals now accept
Magnetite Shards in their place."

**Measured, and the brief's figure was low.** The report that reached this
branch said 110 offers over 10 output items. The merchant holds **121**
rows over **112** distinct output items: 110 charge the retired crystal
plus coin, 10 more are the buy-back rows that PAY 40 crystals for a raid
miniature, and 1 sells Legendary Divination for a Legendary Insight. A
merchant-wide refusal takes all 121, which is correct - the page says the
whole inventory was replaced, not part of it. 59,365 -> 59,244,
`--diff-summary` reporting 121 removed and nothing else touched.

**Two items lose their only route, and both deserve to.** Everything else
on that page is still sold by the live per-raid Glenna pages.

- **Legendary Divination (88485)** - itself `status = discontinued`,
  "Replaced by Legendary Insights with the July 19, 2022 game update", and
  `[[Sells item::Legendary Divination]]` returns the retired page alone.
  Still SPENT at 10 live offers in this dataset, which is the shape the
  sweep warned about: an item can be a live cost and a dead output at the
  same time.
- **Gaeting Crystal, the item form (86094)** - `Gaeting Crystal
  (historical)`, "Replaced by Magnetite Shards with the July 19, 2022 game
  update".

Both get a `REMOVED` hint, so the plan says what happened instead of
showing a bare UNKNOWN.

**A test rested on those rows, and it took the interesting part with
it.** `VendorCostLineExpansionRealCorpusTests.TheSolveTerminates_
OverACostLineGraphThatHasCycles` used items 86094 and 91232 as its cycle:
the retired page both charged crystals for a raid weapon and paid 40
crystals for a raid miniature, which is a genuine two-item loop in the
cost-line graph. Removing the merchant removed the loop, and the test went
red on `BuildAsync(86094, 1)` returning nothing to expand. It was NOT
deleted or weakened: it now builds from Crystalline Ore (46682) and
asserts Tenebrous Crystal (70718) was expanded, a pair that six live Heart
of Maguuma bulk-exchange merchants sell for each other in both directions,
none of them historical. Measured over the corpus as it now stands: 23
two-item cycles inside 10 cyclic components, so the property has plenty of
real data left to hold against.

Worth noting why the old cycle disappeared rather than moved: the live
successors sell item 91232 for coin plus **currency 28**, Magnetite
Shards. A wallet currency is not an Item cost line, so the replacement
offers cannot form a cost-line cycle at all - the loop was an artifact of
the retired item-form currency, exactly as the patch note describes.

**No live vendor charges the retired crystal.** All 110 offers that paid
in item 86094 were this merchant's own; after the exclusion the corpus
contains zero cost lines in that item and zero offers producing it. That
was worth checking rather than assuming, since a live vendor still
charging a currency retired in 2022 would have been a bigger finding than
the dead vendor itself.

**One thing this turned up that is NOT mine to fix.**
`Models/BarterItemDecisionDefaults.cs` values item 86094 at 3600 copper,
with a comment that item 86094 and wallet currencies 39 and 77 "are all
named 'Gaeting Crystal' on /v2/items and /v2/currencies - the same
in-game good in item and wallet form". The live API disagrees: currency 39
is "Earned from bosses and events inside Path of Fire raids" (the retired
one) and currency 77 is "Earned from bosses and events inside Janthir
Wilds raids" (the current one). They are two different currencies sharing
a name, and the item is the retired one. Left alone deliberately -
`Models/CurrencyDecisionDefaults.cs` is another branch's file and the two
values are coupled by that comment. The exclusion does defuse it for now:
after this change NO offer in the dataset charges item 86094, so the
valuation has nothing left to price.

### The finding that outlives this branch: 3,817 offers from 116 removed vendors

**The two vendors this branch refused were 2 of 118.** Two
wiki-authoritative lists were pulled and intersected with the shipped data
rather than pattern-matching on names:

```
# every NPC page the wiki marks historical (659 pages)
https://wiki.guildwars2.com/api.php?action=query&list=categorymembers
    &cmtitle=Category:Historical%20NPCs&cmlimit=500&format=json

# every item page marked discontinued that carries a game id (1,375 pages)
https://wiki.guildwars2.com/api.php?action=ask&format=json&query=
    [[Has availability::Discontinued]][[Has game id::+]]|?Has game id|limit=500

# per-page check, either namespace (Is_historical, Has_availability, _INST)
https://wiki.guildwars2.com/api.php?action=browsebysubject
    &subject=Battle_Historian&format=json

# every vendor row the wiki has for one item, live-vendor check
https://wiki.guildwars2.com/api.php?action=ask&format=json&query=
    [[Sells item::Dragonite Ore]]|?Has vendor
```

- **118 merchants in `ref/vendor_offers.json` were Historical NPCs**,
  holding 3,987 offers. This branch refused two of them, leaving **116
  merchants and 3,817 offers across 1,798 distinct output items**.
  Largest survivors: Black Lion Voucher Dealer (273), Weapon Master (NPC)
  (171), Weapon Trader (171), Merchant (WvW weaponsmith) (171), Zakka
  Hideslicer (157). Every one of those rows can be picked by the solver
  today, and any priced in an unvalued token ranks as free, which is the
  exact defect this branch fixed for two vendors out of 118.
- **The exclusion list cannot absorb this and should not try.** 3,817
  hand-written entries is not a hand-verified list; the file's own header
  says keep it small, and each entry is supposed to be a claim somebody
  checked.
- **Nor is a blanket filter safe as it stands: 619 output items have
  offers ONLY from a historical NPC.** Dropping the class wholesale would
  strand every one of them the way these two exclusions stranded seven,
  and seven was small enough to research by hand in an afternoon.
- **A sub-shape that is cheap to find today: the parenthesised variant.**
  Both `Scholar Glenna` and `Scholar Glenna (Gaeting Crystal)` exist as
  wiki pages, and our corpus already carries the disambiguator in the
  merchant string, so this kind is detectable by NAME before the scrape
  learns to read any infobox. Same for the `/historical` subpage suffix:
  30 merchant strings carry it over 3,650 rows (1,332 for `Black Lion
  Weapons Specialist/historical`, 1,113 for `Gem Store/historical`), and
  another 12 strings over 291 rows carry a `(historical)` parenthesis.
  Neither pattern is proof on its own - `Priory Historian (bandit crest
  collector)` has "Historian" in its name, 37 rows, and is NOT in
  Category:Historical NPCs - so treat a name match as a candidate list to
  check against `Is historical`, never as the filter itself.
- **The fix, for whoever picks this up.** `WikiSmwClient`'s vendor query
  asks for `[[Sells item::+]]` subobjects and never looks at the parent
  page. `Is historical` is available on the parent (`Has vendor.Is
  historical` as a printout, or `[[Has vendor::<q>[[Is historical::t]]</q>]]`
  as a condition - both verified working against the live API on
  2026-08-29). Recording it PER OFFER rather than filtering at query time
  is what makes it reversible: the data then says which rows are
  historical, the module can decide what to do with them, and the 621
  stranded items are visible as a list to work through rather than a
  silent deletion.

**Everything else the sweep turned up, with its verdict.**

- **61 discontinued items are used as payment across 613 offers.** Most
  are NOT dead routes and were left alone: Collector Terksli, the Snowflake
  Trader and Evon Gnashblade still take old claim tickets (the wiki's own
  "Currency for" tables for Fused Weapon Claim Ticket query
  `status=Current`), and Scholar Glenna, Titan Specialist Tante and the
  Mists Vault take legacy raid and vault tokens people still hold. The
  currency being unobtainable is a valuation problem, not a vendor problem.
- **Dead on both sides, and left for a later pass with evidence
  recorded.** The eight "Support Mark" merchants - Merchant (Exalted) 51,
  (Olmakhan) 38, (Ebon Vanguard) 24, (Kodan) 22, (Crystal Bloom) 20,
  (Tengu) 18, (Skritt) 3, (Deldrimor) 2 - are all `status = historical`
  with `status notes` saying the content ran 16 February to 2 March 2021
  and that "the goods can now be purchased from Tactician Erlandson", who
  is in this dataset with 178 rows, all but one priced in Tyrian Defense
  Seals rather than in a Support Mark. Also:
  `Lionguard Lyns` (20 rows, Captain's Council Commendation, whose page
  says "the only collector was removed from the game as part of the
  February 26, 2013 update"), `Vigil Weapon Specialist (historical)` (16),
  `Fortune Scrap Vendor (historical)` (15), `Token Trader (historical)`
  (6), `Charity Corps Seraph (historical)` (5). Each is a defensible
  exclusion on the evidence above; none was taken, because 178 near-
  identical entries for one Living World chapter is the same failure the
  file's own header warns against, and because none of them prices a
  crafting ingredient at zero the way the Battle Historian did.

**Mechanism change.** `ApplyExclusions` now reads an entry with
`merchantName` and no `outputItemId` as a refusal of that merchant's whole
inventory. The claim being made about the Battle Historian is that the NPC
does not exist, which is one claim, not 49; encoding it 49 times would
also let a re-scrape that finds a 50th row slip it through. Three guards
came with it, because a merchant-wide entry fails silently in the
direction that ships bad data: a non-numeric `outputItemId` drops nothing
rather than widening to the whole merchant, a blank `merchantName` is
refused outright, and any entry that matches no offer is warned about.
Four tests in `tests/VendorOfferUpdater.Tests/ApplyExclusionsTests.cs`
pin all of it, one of them against the real shipped `ref/` file.

Gate: not required - dev-tool and data change, no rendering path touched.
Module 3,971, RecipeSeeder 3, VendorOfferUpdater 238 (4,212 total, from a
4,208 baseline plus the four new tests), all green. Two exclusions, 170
offers removed, seven acquisition hints added. The byte-identical round
trip of the untouched dataset through the tool's own serializer, run again
before the second exclusion, is the verification a desktop gate could not
add.
