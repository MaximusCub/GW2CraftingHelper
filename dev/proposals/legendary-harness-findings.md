# Legendary harness findings: what a real plan actually fails to track

Settles UNKNOWN #3 of `dev/proposals/module-capability-audit.md` - "how many
nodes of a specific legendary tree land on the unrepresentable side of the
boundary" - by running the boundary rather than reasoning about it.

Every number below came out of `tools/TaimisToolbench.Harness --classify`
against the shipped seeds and the real solver. Nothing here is inferred from
the wiki or from the taxonomy research; where a fact is inferred it says so.

- Harness invocation: `--profile 30 --classify --live --force-craft-root`
  (measurement) and `--profile 30 --classify --force-craft-root` (offline
  control).
- Run date: 2026-08-29. Trading Post prices are live, so coin totals drift
  between runs. Bucket assignment barely does: three independent sweeps
  over about an hour produced IDENTICAL counts for vendor-coin (26),
  vendor-valued (122), vendor-unvalued (43), UnknownSource (60) and
  currency-leaf (6) terminals, and a byte-identical recurring-blocker
  table. Only the Trading Post count moved, 508 in the first run and 513
  in the other two, as a few nodes crossed the craft-versus-buy line.
  Every table below is from the first of those runs. What makes an item
  unpriceable is structural, so the findings do not depend on the day's
  prices.
- Account state: none. No API key, no snapshot, no inventory, no learned
  recipes, default settings (curated currency valuations on, Homestead
  tier 0).
- 18 items, one per legendary class, each id verified against
  `ref/item_name_seed.json` or `/v2/items` before use.

---

## 0. Why the headline numbers are from a live run

The brief asked for offline runs. They were done, and they are reported in
section 5, but they cannot answer the question. Offline the harness wires
`NullPriceApiClient`, which proves an empty Trading Post; the solver then
force-crafts every tradeable node down to raw materials, and every raw
material - Iron Ore, Vial of Blood, Glob of Ectoplasm - terminates as
`UnknownSource` for want of a price. The same 18 items produce:

| | terminals | Trading Post | Unknown |
|---|---|---|---|
| live | 765 | 508 (66.4%) | 60 (7.8%) |
| offline | 13,292 | 0 (0.0%) | 5,421 (40.8%) |

The offline pass counts Twilight at 969 nodes and 721 terminals, and asks
for 260,638,500 units of one raw material. Those are artefacts of the
missing price feed, not module behaviour any player sees. The live column
is what the module renders.

A second correction was needed before any number was trustworthy. The
harness passed no `CurrencyValuation` at all, which
`CraftingPlanPipeline.GenerateStructuredAsync` turns into
`CurrencyValuation.None` - a RAW instance that, per that class's own doc
comment, "silently yields zero curated defaults". Every running module gets
`ModuleSettings.GetEffectiveCurrencyValuation()`, which materializes them
through `CurrencyValuation.WithDefaults`. The harness now does the same, for
every mode, not just `--classify`.

---

## 1. Item x bucket

Live, root forced to Craft where the solver would otherwise buy the target
outright. "root recipes" is the count of recipes the corpus holds for the
target itself; 0 means the item has no bill of materials anywhere in
`ref/recipes_seed.json` or `ref/mystic_forge_recipes.json`.

| Item | id | root decision | nodes | depth | ms | root recipes | 1 TP | 2 vendor coin | 3 vendor valued currency | 4 vendor UNVALUED | 5 UnknownSource | currency leaf | terminals |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Twilight (Gen 1 weapon) | 30704 | Craft | 52 | 5 | 1564 | 1 | 23 | 1 | 8 | 0 | 5 | 0 | 37 |
| Astralaria (Gen 2 weapon) | 76158 | Craft | 110 | 7 | 676 | 1 | 53 | 4 | 11 | 3 | 8 | 0 | 79 |
| Aurene's Fang (Gen 3 weapon) | 95675 | Craft | 149 | 8 | 795 | 1 | 79 | 5 | 10 | 0 | 10 | 0 | 104 |
| Klobjarne Geirr (Janthir spear) | 103815 | Craft (forced) | 112 | 7 | 706 | 1 | 55 | 6 | 8 | 3 | 6 | 3 | 81 |
| Obsidian Heavy Breastplate (PvE open world) | 101521 | Craft | 58 | 7 | 417 | 1 | 20 | 1 | 8 | 7 | 2 | 3 | 41 |
| Perfected Envoy Vestments (raid) | 80190 | Craft | 79 | 5 | 257 | 1 | 39 | 0 | 10 | 7 | 2 | 0 | 58 |
| Triumphant Hero's Breastplate (WvW) | 83394 | **Unknown** | 1 | 1 | 174 | **0** | 0 | 0 | 0 | 0 | 1 | 0 | 1 |
| Ardent Glorious Breastplate (PvP) | 83348 | **Unknown** | 1 | 1 | 166 | **0** | 0 | 0 | 0 | 0 | 1 | 0 | 1 |
| Eikasia, Mists-Grasper (fractal) | 105171 | BuyFromVendor | 1 | 1 | 734 | **0** | 0 | 0 | 0 | 1 | 0 | 0 | 1 |
| Selachimorpha Container (aquabreather) | 105743 | Craft | 9 | 3 | 702 | 1 | 0 | 1 | 3 | 2 | 1 | 0 | 7 |
| Aurora (trinket) | 81908 | Craft | 79 | 5 | 349 | 1 | 40 | 3 | 7 | 5 | 4 | 0 | 59 |
| Conflux (trinket) | 93105 | Craft | 112 | 8 | 3611 | 1 | 47 | 4 | 18 | 6 | 8 | 0 | 83 |
| Prismatic Champion's Regalia (achievement trinket) | 95380 | **Unknown** | 1 | 1 | 230 | **0** | 0 | 0 | 0 | 0 | 1 | 0 | 1 |
| Ad Infinitum (back item) | 74155 | Craft | 58 | 7 | 177 | 1 | 24 | 0 | 8 | 4 | 4 | 0 | 40 |
| Warbringer (WvW back item) | 81462 | Craft | 66 | 6 | 301 | 1 | 22 | 1 | 15 | 5 | 6 | 0 | 49 |
| Legendary Rune | 91536 | Craft | 53 | 4 | 176 | 1 | 35 | 0 | 5 | 0 | **0** | 0 | 40 |
| Legendary Sigil | 91505 | Craft | 53 | 4 | 205 | 1 | 35 | 0 | 5 | 0 | **0** | 0 | 40 |
| Legendary Relic | 101582 | Craft | 60 | 7 | 391 | 1 | 36 | 0 | 6 | 0 | 1 | 0 | 43 |
| **total** | | | **1,054** | | **11,631** | | **508** | **26** | **122** | **43** | **60** | **6** | **765** |

Bucket 6 ("not in the recipe corpus") and bucket 7 ("errors") are reported
as columns rather than terminal counts: bucket 6 is the four rows with
`root recipes = 0`, and bucket 7 is empty - every one of the 18 items
returned a plan without throwing, in 166 to 3,611 ms.

### Aggregate

| Bucket | Terminals | Share |
|---|---|---|
| 1 Trading Post | 508 | 66.4% |
| 2 vendor, coin | 26 | 3.4% |
| 3 vendor, valued currency | 122 | 15.9% |
| 4 vendor, at least one UNVALUED cost line | 43 | 5.6% |
| 5 UnknownSource | 60 | 7.8% |
| 6 currency leaf (raw currency ingredient) | 6 | 0.8% |
| Have / other | 0 | 0.0% |

Two thirds of a legendary tree is ordinary Trading Post shopping and the
module handles it. The interesting 13.4% is buckets 4 and 5, and it is not
spread evenly: three items - Ball of Dark Energy, Gift of Battle and Fine
Essence of Luck - account for 24 of the 60 UnknownSource terminals between
them, and appear in 8, 7 and 7 trees respectively.

---

## 2. The four items that cannot be planned at all

Stated plainly, because it is a finding and not a failure of the run.

**Triumphant Hero's Breastplate (83394, WvW legendary armour)** and **Ardent
Glorious Breastplate (83348, PvP legendary armour)**. `root recipes = 0`,
no vendor offer, no Trading Post price. The plan is one node reading
Unknown, with no acquisition hint and no badge. The module says nothing
about them at all. The wiki carries a Mystic Forge recipe for each (the ids
above came from those very `output item id` fields), so the gap is that the
recipe never reached `ref/mystic_forge_recipes.json`, not that the recipe
does not exist.

**Prismatic Champion's Regalia (95380)**. Same shape, but correctly so -
this item is pure achievement completion and has no bill of materials to
seed. A one-node Unknown plan is the honest answer; the module just has no
way to say "this cannot be costed, here is why" rather than "unknown".

**Eikasia, Mists-Grasper (105171, fractal gloves)**. `root recipes = 0`, but
a vendor offer does exist, so the plan is a single BuyFromVendor node
charging a Gift of Magical Prosperity, a Gift of Mighty Prosperity, 200 Icy
Runestones and a Fractalline Spark, none of them expanded. Its displayed
total of 4,085,740 copper is exactly 1,061,520 + 1,024,220 + 2,000,000 for
the first three; the Fractalline Spark has no price anywhere and silently
contributes nothing, which is why the row is bucket 4 rather than bucket 3.

A fifth item, **Klobjarne Geirr (103815)**, plans fine but only if you make
it. Left alone, the solver sees a live Trading Post buy order at 22,507,000
copper, decides buying beats crafting, and returns a ONE-node plan. Every
blocker in its tree - three unvalued Janthir map gifts, two Memories, Gift
of Battle - is invisible unless the user clicks CRAFT on the root. All
tabled numbers for this item come from a re-solve with the root pinned to
Craft (`--force-craft-root`).

---

## 3. Recurring blockers, ranked by how many trees they appear in

Only items in bucket 4 or 5. 18 trees analysed.

| Blocking item | id | trees | units across trees | bucket | why the module cannot price it |
|---|---|---|---|---|---|
| Ball of Dark Energy | 71994 | 8 | 13 | 5 Unknown | salvage-only output of an ascended item; badge `SALVAGE`, no route |
| Fine Essence of Luck | 45175 | 7 | 20,506 | 5 Unknown | account bound, no vendor, no TP |
| Gift of Battle | 19678 | 7 | 14 | 5 Unknown | WvW reward track; its one vendor row is deliberately excluded (`ref/vendor_offer_exclusions.json`), badge `WVW` |
| Dragonite Ore | 46733 | 4 | 3,500 | 4 unvalued | routed through a vendor charging **WvW Tournament Claim Tickets** - see 3.1 |
| Empyreal Fragment | 46735 | 4 | 3,500 | 4 unvalued | same vendor, same defunct ticket |
| Bloodstone Brick | 46730 | 4 | 30 | 4 unvalued | forge trade costing Pile of Bloodstone Dust, itself unvalued |
| Pile of Auric Dust | 69432 | 3 | 401 | 4 unvalued | vendor takes an Auric Basin Commander's Choice Chest |
| Glob of Dark Matter | 46681 | 3 | 30 | 5 Unknown | salvage-only, no hint seeded |
| Pile of Bloodstone Dust | 46731 | 2 | 606 | 5 Unknown | account-bound ascended material with no route at all |
| Bottle of Airship Oil | 69434 | 2 | 251 | 4 unvalued | Verdant Brink Commander's Choice Chest |
| Ley Line Spark | 69392 | 2 | 251 | 4 unvalued | Tangled Depths Commander's Choice Chest |
| Legendary Spike | 81296 | 2 | 2 | 5 Unknown | achievement/collection reward |

Below two trees the tail is long and item-specific: 25 of the remaining
blockers are named "Gift of ..." or "Memory of ..." (Gift of Tarir, Gift of
Dragon's End, Gift of Inner Nayos, Gift of the Ursus, Memory of the
Bearkin's Hunts, ...), and the rest are precursor-collection Spirits and
Essences, Legendary Insight x25, Agaleus, Envoy Insignia and Emblem of the
Conqueror. Every one is earned, not bought. There is no single fix for
them; there is a single SHAPE, described in section 6.

### 3.1 The most alarming single result

Dragonite Ore and Empyreal Fragment - the two most common ascended
materials in the game - are both account bound, so they have no Trading Post
price. The corpus does hold a vendor offer for each, and the solver takes
it. The offer is from a **WvW Battle Historian** and costs **325 WvW
Tournament Claim Tickets** for 500 Dragonite Ore, and 250 for 500 Empyreal
Fragment. `/v2/items/66352` confirms the ticket is Rare, `AccountBound`,
`NoSell`; it was awarded by WvW seasonal tournaments that no longer run.

The module therefore routes 3,500 units of each material, in 4 of 18 trees,
through a vendor no living account can pay, and reports the whole line as
costing 0 coin. The four trees are Perfected Envoy Vestments, Ad Infinitum,
Conflux and Warbringer.

This is a corpus-quality finding, not a solver bug: the offer is real, it is
just dead. `ref/vendor_offer_exclusions.json` already exists as the
mechanism for exactly this (it holds one row today, for Gift of Battle).

---

## 4. The finding that was not asked for: bucket 3 costs nothing

The brief expected bucket 3 (vendor for a valued currency) to be a healthy
state. Measured, it is the largest silent hole in the gold total.

A currency valuation is DECISION-ONLY by design
(`Models/CurrencyDecisionDefaults.cs`): it can tip a comparison but is never
folded into a displayed total. So a vendor node whose whole price is
non-coin has `SubtreeCost = 0`. Counting them:

| Item | plan gold total (copper) | vendor terminals | of which cost ZERO coin |
|---|---|---|---|
| Twilight | 7,291,754 | 9 | 7 |
| Astralaria | 9,320,136 | 18 | 11 |
| Aurene's Fang | 7,012,488 | 15 | 10 |
| Klobjarne Geirr | 14,463,998 | 17 | 11 |
| Obsidian Heavy Breastplate | 3,278,674 | 16 | 15 |
| Perfected Envoy Vestments | 3,065,371 | 17 | 15 |
| Eikasia, Mists-Grasper | 4,086,440 | 1 | 0 |
| Selachimorpha Container | 6,130,450 | 6 | 2 |
| Aurora | 6,522,950 | 15 | 12 |
| Conflux | 8,225,416 | 28 | 20 |
| Ad Infinitum | 7,257,821 | 12 | 10 |
| Warbringer | 7,196,616 | 21 | 19 |
| Legendary Rune | 2,907,010 | 5 | 5 |
| Legendary Sigil | 3,241,583 | 5 | 5 |
| Legendary Relic | 15,977,159 | 6 | 6 |

148 of the 191 vendor terminals across the sweep cost zero coin, in 14 of
the 18 trees. What they actually cost, summed over all 18 trees and
appearing in no gold total anywhere:

| Currency | id | units charged | trees |
|---|---|---|---|
| Fractal Relic | 7 | 28,230 | 14 |
| Spirit Shard | 23 | 5,741 | 14 |
| Blue Prophet Shard | 57 | 28,180 | 13 |
| Provisioner Token | 29 | 250 | 5 |
| Research Note | 61 | 10,500 | 4 |
| Guild Commendation | 16 | 259 | 4 |
| WvW Skirmish Claim Ticket | 26 | 4,650 | 2 |
| Badge of Honor | 15 | 2,500 | 2 |
| Testimony of Jade Heroics | 65 | 1,250 | 2 |
| Airship Part / Ley Line Crystal / Lump of Aurillium | 19/20/22 | 1,050 each | 2 each |
| Ancient Coin | 66 | 1,000 | 2 |
| Karma | 2 | 102,000 | 1 |
| Ursus Oblige | 76 | 5,250 | 1 |
| Legendary Insight | 70 | 25 | 1 |
| (nine more, one tree each) | | | |

Legendary Rune is the clearest case: zero Unknown terminals, zero unvalued
terminals, a plan that looks completely solved at 2,907,010 copper - and all
five of its vendor terminals cost no coin at all. A player reading that
total has no idea it also costs Fractal Relics and Spirit Shards.

---

## 5. Offline control

`--profile 30 --classify --force-craft-root`, no `--live`. Reported for
completeness and because it isolates what the SEED CORPUS alone represents,
with the Trading Post removed.

| Bucket | Terminals | Share |
|---|---|---|
| 1 Trading Post | 0 | 0.0% |
| 2 vendor, coin | 105 | 0.8% |
| 3 vendor, valued currency | 7,676 | 57.7% |
| 4 vendor, unvalued | 84 | 0.6% |
| 5 UnknownSource | 5,421 | 40.8% |
| 6 currency leaf | 6 | 0.0% |

17,819 nodes across the 18 items, versus 1,054 live; deepest tree 13 levels
versus 8. Two things survive the distortion and are worth keeping:

- The four unplannable items are unplannable offline too, identically. That
  is structural, not a pricing artefact.
- Solve time is 410 ms total offline against 11,631 ms live, so essentially
  all of the live time is network, not solver.

---

## 6. What the module would have to TRACK to close each

Ranked by how many trees it fixes.

**1. Non-coin cost as a first-class part of the plan total (148 of 191
vendor terminals, 14 of 18 trees).** Nothing new needs to be scraped - the
solver already carries every line, correctly scaled, on
`CraftingTreeNode.VendorCurrencyCosts`. What is missing is a place to show
them summed at the plan level, next to the gold. This is the cheapest fix in
this document and the one with the widest reach: a "this plan also costs
28,230 Fractal Relics and 5,741 Spirit Shards" line changes what the number
at the top of the plan means. It requires no new data model, no new source
enum, and no invented exchange rate - which is the whole reason valuations
are decision-only today.

**2. An "earned, not bought" terminal state with the reward track named
(60 UnknownSource terminals, 15 of 18 trees).** `UnknownSource` currently
means both "I have no idea" and "this is a WvW reward track / a salvage
output / an achievement". The seeded `AcquisitionHint` machinery already
distinguishes some of them - `WVW` on Gift of Battle, `SALVAGE` on Ball of
Dark Energy, `EXPLORE` on the four Heart of Thorns gifts - and it works. It
just covers 6 of the 60. To close the rest the module needs one seeded hint
per recurring earned item, and the highest-value additions measured here
are, in order: Fine Essence of Luck (7 trees), Glob of Dark Matter (3),
Pile of Bloodstone Dust (2), Legendary Spike (2), Legendary Insight, and the
20 map-completion and collection gifts. This is a seed-data task, not a
solver task; `ref/acquisition_hints_seed.json` is 3.4 KB today.

**3. Offer liveness (3,500 units each of two materials, 4 of 18 trees).**
The corpus needs to know that a vendor's price is no longer payable. The
mechanism exists (`ref/vendor_offer_exclusions.json`); what does not exist
is any signal that would have caught the WvW Tournament Claim Ticket rows
before a plan routed through them. A cheap approximation, measurable from
data already on disk: flag any offer whose cost line names an item that has
no acquisition route of its own anywhere in the corpus.

Below the top three, and stated as scope rather than as a recommendation:

- **Seed the missing WvW and PvP armour forge recipes** (2 items, currently
  unplannable). Both exist on the wiki with an explicit `output item id`.
- **A "this cannot be costed" verdict** distinct from Unknown, for
  Prismatic Champion's Regalia and anything else that is pure achievement
  completion. Today a one-node Unknown plan and a genuine data gap look
  identical.
- **Whether the target itself should be buyable.** Klobjarne Geirr's default
  plan is one node because the Trading Post beats a craft tree whose Unknown
  terminals cost 0 by construction. That comparison is not apples to apples,
  and it hides the entire tree from a user who did not think to click CRAFT.

Not needed, on this evidence: any change to how Mystic Clovers, Obsidian
Shards, Spirit Shards or Provisioner Tokens are handled - see section 7.

---

## 7. Confirmed and refuted

The brief predicted six recurring blockers. Measured:

| Predicted | Verdict | Evidence |
|---|---|---|
| Gift of Battle | **CONFIRMED** | Unknown in 7 of 18 trees, 14 units. Badge `WVW`. The single most recurring true blocker. |
| Gift of Exploration | partly | Unknown, but in 1 tree only (Twilight). Gen 2, Gen 3 and every armour line here need none. |
| Mystic Clover | **REFUTED** | Never a terminal. It is an internal Craft node - Twilight crafts 77 of them at a solved subtree cost of 713,634 copper, from Obsidian Shard, Mystic Coin, Ectoplasm and Philosopher's Stone. |
| Obsidian Shard | **REFUTED as a blocker** | The single most common terminal in the sweep - 14 of 18 trees, 3,220 units - and every occurrence is bucket 3, priced from a vendor for a valued currency. It is however part of finding 4: those 3,220 shards contribute 0 coin to any total. |
| Spirit Shards | **REFUTED** | Currency 23 is valued at 3,600 copper by `CurrencyDecisionDefaults`, so every offer taking it is comparable. 5,741 charged across 14 trees, all in bucket 3. |
| Provisioner Tokens | **REFUTED** | Currency 29, also valued at 3,600. 250 charged across 5 trees, bucket 3. |

The prediction was directionally right about which items recur and wrong
about why they are a problem. The recurring items are mostly PRICED; what is
missing is that their price is not in the plan's total. The genuinely
unrouted items are a different set - salvage outputs, essences of luck, and
reward-track gifts.

One structural gap the curated table makes visible: `CurrencyDecisionDefaults`
is adapted from gw2efficiency and its highest id is 69. Every currency
introduced since is unvalued, and the sweep hit eight of them - Astral
Acclaim (63), Ancient Coin (66), Legendary Insight (70), Static Charge (72),
Pinch of Stardust (73), Calcified Gasp (75), Ursus Oblige (76), Antiquated
Ducat (81), Aether-Rich Sap (83). Those are the Secrets of the Obscure,
Janthir Wilds and Visions of Eternity map currencies, which is precisely why
the newest four legendaries in this sweep carry the most bucket-4 terminals.

---

## 8. Reproducing

```
dotnet run --project tools/TaimisToolbench.Harness/TaimisToolbench.Harness.csproj -- \
  --profile 30 --classify --live --force-craft-root
```

Drop `--live` for the section 5 control. Individual classes are profiles
10 to 27; see `tools/TaimisToolbench.Harness/README.md`.

No production code was changed to produce this document. The classifier
lives in `tools/TaimisToolbench.Harness/TerminalClassifier.cs` and reads
only what `CraftingPlanPipeline` already returns.
