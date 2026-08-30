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
Post fallback either:

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

Follow-up worth taking, not taken here: `ref/acquisition_hints_seed.json`
has no entry for 46733 or 46735, so those two now show as UNKNOWN with no
guidance. Two hand-verified hints would turn an honest blank into an
honest answer.

**Sweep: the class this belongs to.** Two wiki-authoritative lists were
pulled and intersected with the shipped data rather than pattern-matching
on names: `Category:Historical NPCs` (659 pages) and every page with
`[[Has availability::Discontinued]][[Has game id::+]]` (1,375 pages, 1,221
in `Category:Discontinued items`).

- **118 merchants in `ref/vendor_offers.json` are Historical NPCs**,
  holding 3,987 offers before this change and 3,938 after. Largest:
  Black Lion Voucher Dealer (273), Weapon Master (NPC) (171), Weapon
  Trader (171), Merchant (WvW weaponsmith) (171), Zakka Hideslicer (157).
  This is the real class, and the exclusion list is the wrong instrument
  for it: 3,938 hand-written entries is not a hand-verified list, and a
  blanket scrape-time `Is historical` filter would strand the **621 output
  items whose only offers come from a historical NPC**. Teaching
  `WikiSmwClient` to read the flag and record it per offer - filterable,
  reversible, and visible in the data - is a project of its own.
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
4,208 baseline plus the four new tests), all green, and the byte-identical
round trip of the untouched dataset through the tool's own serializer is
the verification a desktop gate could not add.
