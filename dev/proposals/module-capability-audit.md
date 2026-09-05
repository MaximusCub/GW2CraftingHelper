# Module capability audit: what this codebase actually models

Status: AUDIT (read-only). No production code, `ref/` data or workflow was
changed to produce it. Written 2026-08-29 against commit
`d060c02` on branch `w3-audit-capability`.

Scope: what the crafting planner can and cannot REPRESENT. It is the
codebase half of a two-part exercise; the game half (the taxonomy of
legendary items and their real acquisition pathways) is being established
separately, and this document is written so the two can be diffed row by
row. Nothing here is a proposal. Nothing here is researched from the game -
every claim is read out of the source, the seed data, or a record already
in `docs/`.

Evidence labels, matching the convention the other proposals in this
directory use:

- **MEASURED** - read directly out of a file cited by path and line, or
  computed from a shipped `ref/*.json`.
- **INFERRED** - a conclusion drawn from what was read, not itself printed
  anywhere.
- **UNKNOWN** - could not be settled from the tree; the section says what
  would settle it.

A note on tone. The value of this document is in what it admits. Where the
module handles something well the entry is one line; where it does not, the
entry says exactly where the partition falls, because "partial" without a
boundary is not information.

---

## 1. The table

"Consequence" is what a user experiences, not what a developer would have
to write.

| Capability | Modelled | Evidence | Consequence for a user |
| --- | --- | --- | --- |
| Craft from a recipe | yes | `Models/RecipeOption.cs:7-30`; `Services/PlanSolver.cs:1388` (`SelectBestRecipes`) | Works. 14,966 recipes shipped. |
| Buy from the Trading Post | yes | `Models/ItemPrice.cs:12,19`; `Services/PlanSolver.cs:886` (`Evaluate`) | Works, both price bases. |
| Buy from a vendor for coin | yes | `Services/VendorBatchSolver.cs:264-270`; 59,414 offers in `ref/vendor_offers.json` | Works. |
| Buy from a vendor for a wallet currency | partial | `Services/VendorBatchSolver.cs:294-299`; `Models/CurrencyDecisionDefaults.cs:54-97` | The offer is always shown, but it can only be RANKED against a coin route when the currency carries a curated valuation. 43 currencies have one; 21 of the 62 currencies the corpus actually charges do not. |
| Buy from a vendor for an untradeable item (barter) | partial | `Services/VendorBatchSolver.cs:337-390`; `Models/BarterItemDecisionDefaults.cs` | Since section 7.4 the line is costed through the solver when anything can price it. When nothing can, the offer is fallback-tier and can never win a comparison. 26 barter items have a curated valuation out of the 1,032 item ids the corpus charges. |
| Mystic Forge as a recipe source | yes | 1,591 recipes tagged `MysticForge` in `ref/recipes_seed.json`; `ref/mystic_forge_recipes.json` (1,591 rows) | Works, treated as an inherently available discipline. |
| Fractional-output forge EV (Mystic Clover) | yes | `Models/RecipeOption.cs:22` (`ExpectedOutputCount`); ARCHITECTURE section 8 | Works, but exactly ONE recipe in the shipped seed sets it. |
| Salvage, drops, reward tracks, map completion, achievements as a SOURCE | no | `Models/AcquisitionSource.cs:26-33` has five members and none of them is any of these | Such an item resolves to `UnknownSource` and shows an UNKNOWN pill. A curated text hint stands in for a route - 10 items have one (`ref/acquisition_hints_seed.json`). |
| Crafting discipline + rating gate | yes (soft) | `Services/CraftCompetencyEvaluator.cs:90-123`; `Models/SnapshotCharacterDiscipline.cs:16-22` | The only gate that touches the solver, and only to stop Craft winning AUTOMATICALLY. The CRAFT pill stays clickable. |
| Learned-recipe gate | partial (advisory) | `Services/CachingAccountRecipeClient.cs:17-23`; `Services/PlanResultBuilder.cs:286-300` | The plan is solved BEFORE learned ids are consulted. A "Required Recipes Missing!" row appears, but the route was already costed as if it were available. |
| Recipe-sheet ITEM requirement, craft side | partial (advisory) | `Models/RequiredRecipe.cs:20`; `Services/RecipeSheetSavingsCalculator.cs` | Surfaced as a Plan Notes savings tip only. Requires a curated map: `ref/recipe_sheet_items.json` has ONE entry. |
| Recipe-sheet ITEM requirement, vendor side | no | `Models/VendorOffer.cs` has no such field; KNOWN-ISSUES.md:748-756 | The plan can recommend a vendor exchange the player cannot use until they buy an unlock item first. Already recorded as "the one worth doing". |
| Daily craft cooldowns | partial (advisory) | `Models/DailyCooldownItem.cs:16-36`; `Services/PlanViewModelBuilder.cs:917-928` | A notice row, never a re-route. Fires only on `Craft` steps, and only for the 15 curated items in `ref/daily_cooldown_items.json`. |
| Vendor purchase caps (daily/weekly/seasonal) | partial (advisory) | `Models/TimegatedItem.cs:16-25`; `Services/VendorBatchSolver.cs` (`FinalizeVendorBatches`) | Warn-only by design. 375 offers carry a daily cap, 364 a weekly, 28 a seasonal - out of 59,414. Character and total caps are unseeded entirely (KNOWN-ISSUES.md:1024). |
| Achievement or collection completion gate | no | `/v2/account/achievements` is called nowhere; `Services/AchievementBitDedupPrePass.cs` reads bit NUMBERS only | A plan can cost an item honestly and the player finds it unpurchasable. Recorded at KNOWN-ISSUES.md:757-764. |
| Mastery gate | no | zero occurrences of `mastery`/`masteries` in any `.cs` in the tree | Not modelled, not surfaced, not mentioned. |
| Crafting-station locality | no | no station concept in `RecipeOption`; `VendorOffer.Locations` is parsed and never read | Cost is right, the trip is longer than the plan implies (KNOWN-ISSUES.md:765-772). |
| Account unlocks (Homestead, expansions, episodes) | no | KNOWN-ISSUES.md:1034 records the Homestead master gate as deliberately absent | The plan may route through content the account does not own. |
| Guild upgrades as an ingredient | no (leaf) | `Services/CraftingTreeBuilder.cs:135-137`; `Models/CraftingDecision.cs:31` | 678 ingredient rows resolve to an unpriced, unnamed leaf. Guild decoration crafting is out of scope (KNOWN-ISSUES #54). |
| PvE vs WvW vs PvP as a concept | no | see section 5; no enum, field, filter or setting anywhere | The planner cannot recommend, prefer, exclude or even label a mode-specific route. |
| Account wallet as a solver input | no | `Models/CraftingPlanResult.cs:121` (`OwnedCurrencyAmounts`) is display-only | Currency you already hold never makes a currency route cheaper. |
| Owned materials | yes | `Services/InventoryReducer.cs`; `Models/OwnMaterialsMode.cs` | Works, in two modes, with a recorded residual (ARCHITECTURE 8.2). |
| Multi-item batch plans | yes | `Models/CraftingPlanResult.cs:147` (`MultiItemRoots`); KNOWN-ISSUES #21 | Works. |
| Item binding (account-bound) | partial | `Models/ItemMetadata.cs:21` (`IsAccountBound`) | Captured and used for display and reclaim-value suppression, never as a routing constraint. |
| Seasonal/festival vendor offers | partial | `Services/SeasonalOfferFilter.cs:26-30` | Unconditionally EXCLUDED from the solve, surfaced as an informational tip. Tagging is partial: 597 of 59,414 offers tagged, across 6 festivals (KNOWN-ISSUES #63). |
| Live vendor data at runtime | no | KNOWN-ISSUES.md:370 (item 34): the live-wiki resolver was deleted | The vendor corpus is a frozen offline seed, refreshed only by a maintainer running `tools/VendorOfferUpdater`. |

---

## 2. Data model coverage

Counts are **MEASURED** from the shipped files at commit `d060c02`.

### 2.1 Recipes - `ref/recipes_seed.json` (11.6 MB)

14,966 recipes, matching the pinned `rowCount` in `ref/recipe_seed_manifest.json`
(seeded against GW2 build 205780, 2026-08-25).

Every row carries exactly: `id`, `outputItemId`, `outputItemCount`,
`expectedOutputCount`, `ingredients[]`, `disciplines[]`, `minRating`,
`flags[]`, `achievementId`. The runtime type is `Models/RecipeOption.cs`
plus `Models/RecipeNode.cs`.

- 49,110 ingredient rows: 48,237 `Item`, 678 `GuildUpgrade`, 195 `Currency`.
- Disciplines: Weaponsmith 2,608, Huntsman 2,339, Armorsmith 2,002,
  Leatherworker 1,997, Tailor 1,997, Artificer 1,679, MysticForge 1,591,
  Scribe 1,036, Chef 799, Jeweler 674, Merchant 3, Homesteader 2,
  Achievement 1.
- Flags: `LearnedFromItem` 4,732, `AutoLearned` 2,097. Those two are the
  ONLY flags present.
- 1,595 rows have a negative id - the Mystic Forge and hand-authored
  achievement/merchant recipes, which have no GW2 API recipe id.
- `expectedOutputCount` is non-null on exactly **1** recipe. The whole
  fractional-output EV machinery (ARCHITECTURE section 8) is exercised by a
  single row.
- `achievementBit` is set on exactly **4** ingredients; `achievementId` on
  exactly **1** recipe.

What it does NOT carry: any station, any location, any unlock item id, any
mastery, any account-unlock condition, any game mode, any "obtainable only
during X" marker, and no link from a recipe id to the item that teaches it
(that link is the separately curated `ref/recipe_sheet_items.json`, which
has one entry, and its own `source` field records that no API endpoint
provides it).

### 2.2 Vendor offers - `ref/vendor_offers.json` (14.8 MB)

59,414 offers, matching `ref/vendor_offers_manifest.json`. Source
`gw2wiki-smw`, generated 2026-08-25. Runtime type `Models/VendorOffer.cs`.

Fields, with how many offers populate each (MEASURED):

| Field | Populated | Notes |
| --- | --- | --- |
| `offerId` | 59,414 | Hash, not stable across a re-scrape (KNOWN-ISSUES #63) |
| `outputItemId` | 59,414 | 15,544 distinct items |
| `outputCount` | 59,414 | |
| `costLines` | 57,518 | **1,896 offers have NONE** - see 2.7 |
| `merchantName` | 59,414 | 2,158 distinct merchants |
| `locations` | 49,940 | 779 distinct strings; never read by production code |
| `dailyCap` | 375 | |
| `weeklyCap` | 364 | |
| `seasonalCap` | 28 | |
| `homesteadTier` | 216 | |
| `seasonalFestival` | 597 | 6 festivals |

Cost lines are `Models/CostLine.cs:5-9` - `{Type, Id, Count}`, and nothing
else. 73,978 lines total: 52,489 `Currency` (of which 19,655 are the coin
id) and 21,489 `Item`. 62 distinct currency ids and 1,032 distinct item ids
appear as costs. Cost-line arity per offer: 0 -> 1,896; 1 -> 43,559;
2 -> 12,399; 3 -> 693; 4 -> 793; 5 -> 74.

What the offer model does NOT carry: a required unlock ITEM (the recorded
gap at KNOWN-ISSUES.md:748-756), a required achievement, a required mastery,
a required account unlock, a game mode, a character/total purchase cap
(deliberately unseeded, KNOWN-ISSUES.md:1024), or any structured form of
the location it already stores as free text.

### 2.3 Mystic Forge recipes - `ref/mystic_forge_recipes.json`

1,591 rows, `{id, outputItemId, outputItemCount, ingredients[], comment}`
plus `expectedOutputCount` on one row. Ingredients are `{type, id, count}`
only - 6,364 of them. No success-rate field beyond the single
`expectedOutputCount`, no "one of these four slots may be any exotic"
concept, no promotion/upgrade semantics. Ids are negative and synthesized.

### 2.4 Currencies

There is no currency ENTITY in the data seeds. Currencies exist as:

- ids inside cost lines and ingredients (`Models/CostLine.cs`),
- a name table, `Models/Gw2Constants.cs` (fetched live from
  `/v2/currencies?ids=all` by `Services/CurrencyMetadataService.cs:24`, with
  the constant table as the offline fallback),
- a wallet balance, `Models/SnapshotWalletEntry.cs:5-11` -
  `{CurrencyId, CurrencyName, IconUrl, Value}`,
- a decision-only valuation, `Models/CurrencyDecisionDefaults.cs:54-97`.

The valuation table holds 43 ids, adapted from gw2efficiency under an
explicitly recorded one-time waiver of the "do not invent data" rule
(ARCHITECTURE 8.3). Cross-referenced against the corpus (MEASURED): of the
62 currency ids actually charged by a vendor offer, 21 have NO valuation -
ids 30, 46, 47, 52, 54, 58, 59, 63, 66, 70, 72, 73, 75, 76, 77, 78, 79, 80,
81, 82, 83 - covering 1,591 of the 52,489 currency cost lines. Every offer
that charges one of those is fallback-tier and can never win a price
comparison.

A currency carries no attribute of any kind beyond its id, name, icon and
optional copper value. Nothing records where a currency comes from, which
mode earns it, whether it is capped, or whether it decays.

### 2.5 Barter items

`Models/BarterItemDecisionDefaults.cs` holds 26 entries, each a curated
copper-per-unit derived under one stated rule (cheapest repeatable vendor
exchange payable in coin or an already-valued currency, divided by output
count). Against 1,032 distinct item ids used as vendor costs, that is 2.5%
coverage. ARCHITECTURE section 8 records that 654 of those 1,032 have no
Trading Post price at all.

### 2.6 Daily cooldowns and acquisition hints

`ref/daily_cooldown_items.json`: **15** entries, every one `perDayCap: 1`,
all verified 2026-08-16. `Models/DailyCooldownItem.cs:20-32` documents that
`PerDayCap` is compared directly against `PlanStep.Quantity`, which is only
correct while every seeded recipe outputs 1 per craft.

`ref/acquisition_hints_seed.json`: **10** entries. Badges present are
`EXPLORE` (4), `MERCHANT` (2), `SALVAGE`, `WVW`, `DAILY`, `ACHIEVEMENT`.
`Models/AcquisitionHint.cs:14,20` - `Hint` and `Badge` are opaque display
strings, applied at tree-build time
(`Services/CraftingTreeBuilder.cs:486-502`) and never read by
`Services/PlanSolver.cs`. This is the module's entire answer to every
acquisition mechanic it cannot model: ten hand-written sentences.

### 2.7 Achievement bits

`Models/RecipeNode.cs:33,35` carry `AchievementId`/`AchievementBit`, and
`Services/AchievementBitDedupPrePass.cs` is the only consumer. It classifies
occurrences and zeroes duplicates. It never asks whether an achievement is
complete, and no code path fetches account achievement data. The mechanism
exists for arithmetic, not for gating: 4 ingredient rows in the whole seed
carry a bit.

### 2.8 A latent gap: offers with no cost lines

**MEASURED.** 1,896 shipped offers (3.2%) have an empty `costLines` array,
across 721 distinct output items. `Services/VendorBatchSolver.cs:264` opens
its fold with `offer.CostLines ?? Enumerable.Empty<CostLine>()`, so such an
offer leaves the loop with `coinCost == 0`, `priceable == true` and
`allValued == true`, and lands in the COMPARABLE tier at a comparison value
of 0 - beating every priced route. The sibling helper
`Services/CostLineValuation.cs:34-39` guards exactly this case
("never invent a zero-cost offer out of an empty/missing cost-line list");
the solver does not.

It is **latent, not live**: none of those 721 output items appears anywhere
in `ref/recipes_seed.json` as an ingredient or an output, and none is in
`ref/item_name_seed.json`, which is what
`Services/CraftableItemSearchProvider.cs` searches - so no plan tree can
currently reach one. 13 of them are also vendor cost-line items, and that
path is separately safe because
`Services/PlanSolver.cs:1334-1340` treats a `TotalCost <= 0` subtree
decision as unresolved rather than free. **INFERRED:** a future vendor
re-scrape that adds a zero-cost-line row for an item the recipe corpus does
reach would produce a zero-cost plan. Not previously recorded anywhere in
`docs/`.

---

## 3. Acquisition sources the solver understands

`Models/AcquisitionSource.cs:26-33` is the complete solver vocabulary:

```
BuyFromTp, Craft, Currency, BuyFromVendor, UnknownSource
```

`Currency` is bookkeeping for `PlanStep.Source` aggregation and is never a
routing choice (ARCHITECTURE S1.5). The display vocabulary
`Models/CraftingDecision.cs:23-33` adds `Have`, `GuildUpgrade` and
`UnrecognizedIngredient`, none of which is a source the solver picks either.

So the solver decides between exactly **three** real routes per node:
craft it, buy it on the Trading Post, buy it from a vendor. The decision
rules are in ARCHITECTURE section 8 and are not restated here.

**What is not representable at all**, because there is no enum member and
no data field to carry it:

- A reward track (WvW, PvP). Gift of Battle is the documented case, though
  its hand-exclusion is often mis-described (including in an earlier draft
  of this audit). `ref/vendor_offer_exclusions.json` removes a Battle Master
  row because that 500-Badge sale was REMOVED FROM THE GAME in the Spring
  2016 Quarterly Update, hand-verified with a source - a correct claim that
  the wiki is stale, not a workaround for an inexpressible route. The gap is
  what remains once it is applied: with no vendor row and no recipe, the item
  resolves to `UnknownSource` with a `WVW` text badge, and that badge is the
  entirety of what the module can say about a component every legendary
  weapon of every generation requires.
- Salvaging. `SALVAGE` is a badge string on one hint, not a source.
- A container, chest or RNG drop, including precursor forging. KNOWN-ISSUES
  #17 records that probabilistic forge content never reaches the solved
  tree at all, so there is nothing in a plan to detect it from.
- Map completion, event reward, achievement reward, collection reward,
  meta-event reward.
- Playing a game mode for a currency. A currency is a cost the plan reports;
  it is never a thing the plan tells you how to get.
- Gem-store purchase, account-wide unlock consumption, wardrobe/skin unlock.

Each of these terminates the tree at `UnknownSource`, which is the only
decision that offers the interactive IGNORE toggle - the module's honest
admission that the user may already have the item and it has no way to know
(ARCHITECTURE S1.5).

**Where the partition falls:** the solver models COST OF ACQUISITION for
routes that are transactional (a purchase or a craft). It models nothing
about routes that are earned. That is the single largest structural
boundary in the module, and it is exactly the boundary a legendary
acquisition pathway crosses repeatedly.

---

## 4. Gates: what is modelled, and how

Every gate in the module except one is a post-solve annotation. This is
by design and each carries a doc comment saying so.

### 4.1 Crafting discipline and rating - the only gate in the solver

- Data: `RecipeOption.Disciplines` and `RecipeOption.MinRating`
  (`Models/RecipeOption.cs:26,28`), from the recipe seed / `/v2/recipes`.
- Account side: `Models/SnapshotCharacterDiscipline.cs:16-22`, captured from
  `GET /v2/characters/:id/crafting` at
  `Services/Gw2AccountSnapshotService.cs:323-351`. The literal API string is
  preserved via `cd.Discipline?.RawValue`.
- Evaluator: `Services/CraftCompetencyEvaluator.cs:51-74` folds to
  best-rating-per-discipline once per solve; `:90-123` answers
  `AccountCanCraft`.

It is a SOFT gate in three ways, all deliberate:

1. A null dictionary (no snapshot, no API key, or any character's crafting
   fetch failing) means "unknown" and never penalizes craft
   (`:95-98`).
2. It only removes Craft from the AUTOMATIC pick. `CanCraft` stays true and
   the CRAFT pill stays clickable (`:12-19`).
3. It is additionally gated on a genuine comparable alternative existing
   (`Services/PlanSolver.cs:979-984`): a node whose only route is Craft
   auto-crafts regardless of competency, or its cost would vanish from the
   plan.

Three discipline tags are treated as inherently available and never gate
anything: `MysticForge`, `Achievement`, `Merchant`
(`Services/CraftCompetencyEvaluator.cs:36-37`). Note that `Homesteader`,
which 2 recipes declare, is NOT in that set - harmless today only because
both of those recipes also declare nine real disciplines at `minRating` 0.

### 4.2 Learned recipes - advisory, and the plan is already solved

`Services/CachingAccountRecipeClient.cs:17-23` states it plainly:
"STALENESS IS ADVISORY, NEVER A SOLVER INPUT. Learned recipe ids never
affect a craft-vs-buy decision, a quantity or a cost - the plan is solved
before they are consulted." The verdict is computed at
`Services/PlanResultBuilder.cs:286-300` as a tri-state (`null` = no
permission, so unknown) and rendered as a "Required Recipes Missing!" row.

Consequence: a plan can be built entirely out of recipes the account cannot
use, at prices that assume it can, with a warning underneath.

### 4.3 Time gates - two independent, both informational

**Recipe-level daily cap.** Curated seed only, 15 items.
`Services/PlanViewModelBuilder.cs:917-928` keys strictly on `Craft` steps.
KNOWN-ISSUES #61 records the ceiling verbatim: an item whose recipe the
account has not learned, or that comes from a non-recipe mechanic (a Place
of Power, an achievement reward), resolves to a non-Craft row and gets no
notice at all.

**Vendor purchase caps.** `Models/TimegatedItem.cs:16-25`: a cap "NEVER
gates offer eligibility or re-routes the solver". Fixed by KNOWN-ISSUES
#20.2, which changed caps from hard-excluding an offer to warn-only. Daily
and weekly are mutually exclusive; seasonal is checked independently.
Suppressed entirely when a step's occurrences disagree on the winning offer.

There is no concept of a weekly instance lockout, a daily login reward, an
account-wide "once ever" reward, or a per-character cap
(KNOWN-ISSUES.md:1024 records the last of those as deliberately unseeded
because "the module has no account/character concept at all").

### 4.4 Gate kinds with NO representation

Established by exhaustive grep across the tree.

| Gate kind | Evidence of absence | Note |
| --- | --- | --- |
| Achievement / collection completion | `/v2/account/achievements` is called nowhere. The only `/v2/account/*` endpoints in the tree are `recipes`, `wallet`, `bank`, `inventory`, `materials` (`Services/Gw2AccountRecipeClient.cs:26`, `Services/Gw2AccountSnapshotService.cs:63,94,123,152`). | Recorded at KNOWN-ISSUES.md:757-764. `RawRecipe.AchievementId` exists and is documented as informational, read by nothing. |
| Required recipe ITEM held/bought, vendor side | `Models/VendorOffer.cs` has no field for it | Recorded at KNOWN-ISSUES.md:748-756 as "the one worth doing". Needs a field, a scrape and a check. |
| Mastery | zero occurrences of `mastery` or `masteries` in any `.cs` in the tree; `/v2/account/masteries` never called | Not modelled and not surfaced. |
| Crafting-station restriction | no station field on `RecipeOption` or `RawRecipe`; the API schema has none either | Recorded at KNOWN-ISSUES.md:765-772. `VendorOffer.Locations` holds 779 distinct location strings and is dead at runtime: `.Locations` appears in `tests/` and `tools/` only. |
| Account unlock (expansion, episode, Homestead) | KNOWN-ISSUES.md:1034 records the Homestead master gate as deliberately absent, echoing gw2efficiency | A player who never touched Janthir Wilds can be routed through Homestead Refinement pricing. |
| Game-mode-specific unlocks | see section 5 | |
| Character level, race, profession | no such field anywhere in `Models/` | |
| Wallet balance as a constraint | `Models/CraftingPlanResult.cs:121` `OwnedCurrencyAmounts` is display-only | A route costing 500,000 Karma is priced identically for an account with 0 Karma. |

---

## 5. Game mode awareness

**MEASURED verdict: none. The module has no concept of PvE, WvW or PvP
anywhere.** There is no enum, no field, no filter, no setting, and zero
occurrences of `game mode` or `gamemode` in the tree.

What exists, and what each thing actually is:

- **Currency NAMES that happen to be mode-specific.**
  `Models/Gw2Constants.cs:101,108,115,117,121,126` map ids 7, 15, 24, 26,
  30, 33 to "Fractal Relics", "Badges of Honor", "Pristine Fractal Relics",
  "WvW Skirmish Claim Tickets", "PvP League Tickets", "Ascended Shards of
  Glory". These are display strings in a flat `Dictionary<int,string>`.
  Nothing groups them, tags them, or knows what they mean.
- **Valuations for mode currencies.** `Models/CurrencyDecisionDefaults.cs`
  values Badge of Honor at 23c (`:67`), WvW Skirmish Claim Ticket at 800c
  (`:75`), Ascended Shards of Glory at 1600c (`:81`). Those are opaque
  ints in the same flat map as Karma. A WvW ticket and a Laurel are the
  same kind of thing to this code.
- **Mode words inside vendor metadata.** 3,583 of 59,414 offers contain a
  mode word, always inside `merchantName` (e.g. "Skirmish
  Supervisor/Weapons", "PvP Items (weaponsmith)") or `locations`
  ("World vs. World" appears on 1,407 offers). Neither field is parsed.
  `MerchantName` is read in exactly one production place, the seasonal tip
  string; `Locations` is read nowhere.
- **One acquisition hint.** `ref/acquisition_hints_seed.json` entry for item
  19678 carries `"badge": "WVW"` and a sentence naming the Gift of Battle
  reward track. That badge is an opaque string rendered as a pill.

**Can the planner ever recommend a mode-specific route?** Only in the same
sense it recommends any vendor route: if a vendor sells the item for Badges
of Honor and that currency carries a valuation, the offer competes on
price. The plan will not say "this is a WvW route", will not know the
account has never entered WvW, and will not offer a PvE alternative on that
basis. **MEASURED** offer counts by mode currency in the shipped corpus:

| Currency | id | cost lines | distinct output items |
| --- | --- | --- | --- |
| Badge of Honor | 15 | 2,019 | 840 |
| WvW Skirmish Claim Ticket | 26 | 1,052 | 202 |
| Proof of Heroics | 31 | 141 | 73 |
| Testimony of Jade Heroics | 65 | 237 | 103 |
| Testimony of Desert Heroics | 36 | 78 | 38 |
| PvP League Ticket | 30 | 56 | 34 |
| Ascended Shards of Glory | 33 | **0** | **0** |

Two things follow. PvP League Tickets are charged by 56 offers and have NO
valuation (id 30 is in the unvalued list from section 2.4), so every one of
those offers is permanently fallback-tier. And Ascended Shards of Glory -
the PvP legendary currency - is valued in the defaults table but is charged
by zero offers in the corpus, so the PvP legendary vendor path is simply
absent from the data.

Two recorded data gaps sit on the WvW side specifically:
KNOWN-ISSUES.md:1007 (the Skirmish Merchant family's wiki pages were split
into subpages, leaving 18 offers stale-shaped) and KNOWN-ISSUES.md:1019
(a ~5,400-offer wiki-drift superset, discarded uncommitted).

---

## 6. Structural limits already on record

These are not re-discovered here. They are cited so the audit does not
duplicate them, and so the comparison exercise can treat them as settled.

**Vendor cost and pricing**

- No official GW2 API exposes vendor BUY prices at all; `vendor_value` on
  `/v2/items` is the sell-to-NPC price and is unrelated. The whole vendor
  corpus exists because of this
  (`dev/archive/plans/2026-02-15/phase-a-gw2efficiency-research.md`, section
  "Critical Gap: Vendor Buy Prices").
- The vendor corpus is a frozen offline seed; the live-wiki resolver was
  deleted (KNOWN-ISSUES.md:370, item 34).
- Cap seeding covers a small fraction of offers, and a ~5,400-offer drift
  superset is known and unmerged (KNOWN-ISSUES #28; DEFERRED at :1019).
- Festival tagging is partial: 7 vendor pages checked out of ~2,088
  (KNOWN-ISSUES #63).
- `offerId` is not stable across a fresh scrape of any merchant.

**Currency valuation and comparability**

- Valuations are DECISION-ONLY and never reach a coin total. This is a repo
  invariant, not a convenience (ARCHITECTURE 8.3).
- The two-tier comparable/fallback split, and why an unvalued wallet
  currency and an unvalued barter line are treated asymmetrically, are
  ARCHITECTURE 7.1 and section 8.
- A documented, test-locked limitation: within the fallback tier a vendor
  offer costing 0 coin plus 500,000 units of an unvalued currency beats a
  craft fallback costing 500 real copper, because 0 <= 500. Pinned by
  `AllFallback_VendorZeroCoinPart_BeatsHigherRealCraftCost_DocumentedLimitation`.
- A genuine tie where both sides' priced amounts are identical cannot be
  broken more finely, because breaking it would require inventing an
  exchange rate (KNOWN-ISSUES #44).
- The permissive-vs-conservative comparability question is an open product
  call, written up at `docs/gw2e-considerations.md:590-625`.
- Under `PriceBasis.InstantBuy`, an item with no sell listings falls back to
  its buy-order price and still renders as a flat `Buy` row, "which reads as
  instantly fillable when it is not" (DEFERRED, KNOWN-ISSUES.md:1075).

**Corpus completeness**

- The GW2 API's own `/v2/recipes/search?output=` index is incomplete
  independently of any module bug; only the offline id-walk seeder finds
  those recipes.
- A from-scratch seeder run is destructive and silently drops the four
  hand-authored achievement-bit recipes.
- Guild-gated recipes: 678 `GuildUpgrade` ingredient rows, unpriceable by
  construction; guild decoration crafting is out of scope
  (KNOWN-ISSUES #54).

---

## 7. The vendor cost-line frontier (ARCHITECTURE 7.4)

This is the most recent capability change and the current edge of what the
module can price. The audit's job is to be exact about where it stops.

### 7.1 What it now handles

A vendor offer's `Item` cost line with no Trading Post price is handed to
the same `PlanSolver.Evaluate` a recipe ingredient gets, over a quantity-1
subtree, and the result folds into the offer's real coin cost by the same
`unit x count` multiplication a TP-priced line already used.

- Subtrees: `Services/VendorCostLineSubtrees.cs`, built by
  `Services/CraftingPlanPipeline.cs:858` (`ExpandVendorCostLinesAsync`),
  which iterates rounds, pricing and offer-looking-up whatever the previous
  round exposed.
- Recursion: `Services/PlanSolver.cs:1310`
  (`ResolveCostLineUnitValue`), wired at `:561-596`.
- Answer type: `Models/CostLineUnitValue.cs:20,27,36` - real coin,
  decision-only extra, and an "this subtree has an unvalued cost of its own"
  flag that keeps the offer fallback-tier.
- Consumption: `Services/VendorBatchSolver.cs:344-390`.

The concrete effect, MEASURED and recorded in ARCHITECTURE 7.4: the
Obsidian Heavy Breastplate went from being recommended as a 2g95s10c vendor
purchase (the price of the 10 Globs of Ectoplasm fee, which was the only
part of the offer anything costed) to the craft route's cost plus exactly
that fee.

A second, independent defence landed alongside it:
`Services/VendorOfferDomination.cs:42` bars an offer that charges a
craftable recipe's own ingredients PLUS something more from the comparable
tier, using no prices at all. 104 of the 59,414 shipped offers have that
shape.

### 7.2 Where it stops

Each of these is read directly from the code, not inferred from the doc.

1. **Currency cost lines are never expanded.**
   `Services/VendorCostLineSubtrees.cs:129` filters to
   `Type == "Item"` only. A wallet currency line still has exactly two
   states: a curated valuation, or fallback-tier. 21 of the 62 charged
   currencies have no valuation.

2. **A cut is permanent for the solve, and cuts are silent.** Three bounds
   apply, at `Services/PlanSolver.cs:1327`: a `Visiting` set (the cost-line
   graph is genuinely cyclic - 86094 and 91232 buy each other, among at
   least twelve cycles), a depth cap of 16
   (`VendorCostLineSubtrees.cs:46`), and a budget equal to the subtree
   count. Whichever fires, the answer is `null` and it is MEMOIZED
   (`:1329-1331`), so an id cut once stays uncosted for the rest of that
   solve. This is deliberate and in the safe direction, but nothing tells
   the user a line went uncosted.

3. **The expansion itself is bounded and can truncate deterministically.**
   `DefaultMaxDistinctItems` is 256 (`VendorCostLineSubtrees.cs:37`) and
   the round cap is 6 (`CraftingPlanPipeline.cs:25`). At the item ceiling
   the loop `break`s mid-round (`CraftingPlanPipeline.cs:898-901`); ids are
   walked in ascending numeric order so WHICH lines get dropped is
   deterministic but arbitrary. Measured headroom on the deepest shipped
   shape is 77 items over 3 rounds, so ~3x.

4. **It is a unit price, not a scaled solve.** The subtree is built at
   quantity 1 and multiplied by the line's count and `unitsNeeded`. Any
   batch-ceil non-linearity beneath the line is linearized away. This is the
   same approximation the TP path always made, and it is now applied to a
   whole crafted subtree rather than a single price.

5. **The subtree sees a deliberately narrower world than the plan tree.**
   `Services/PlanSolver.cs:570-584` constructs the cost-line context with
   `overrides: null`, `forceBuyOnlyNodeIds: null`,
   `competencyIndependentForceBuyNodeIds: null` and
   `ownedQuantityUsedByNode: null`. Only `ignoredItemIds` is carried across,
   because it is keyed by item id. **INFERRED consequences:** owned
   materials never discount a cost line (the subtrees are built by
   `_recipeService.BuildTreeAsync` and never passed through
   `InventoryReducer`), and gw2e's 15% cheaper-to-buy force-buy rule does
   not apply inside one. Both make a cost line cost at most what it really
   costs, which is the safe direction, but it means the same item can be
   costed differently as an ingredient and as a cost line in one plan.

6. **Nothing is displayed.** A cost line's subtree never enters the plan
   tree; the user sees the pre-existing cost-component leaf with a number in
   it. Full expansion was measured (4,215 nodes against an 842-node plan)
   and rejected. So the user can see THAT a cost line costs 29,160c and
   never see WHY.

7. **The domination check needs an account snapshot.**
   `Services/VendorOfferDomination.cs:52` returns false whenever
   `bestRatingByDiscipline` is null. Without an API key the second line of
   defence is entirely inert; only the costing survives. It also requires
   every recipe ingredient to be `Item`-typed (`:110-116`) and every
   ingredient quantity to divide exactly by `CraftsNeeded` (`:124-127`), so
   a recipe with a Currency ingredient or a quantity rewritten by inventory
   reduction is never dominated.

8. **It changes no gate.** This is the honest summary of the whole change:
   it made the vendor route's PRICE complete. It did nothing about whether
   the route is AVAILABLE. KNOWN-ISSUES.md:744-777 lists the three
   conditions the wiki names for this exact case - the "Recipe: Legendary
   Obsidian Armor" unlock item, the "Astral Heartbeat" achievement, and
   Wizard's Tower station locality - and records that "the module expresses
   none of them".

9. **The legendary bottom still falls out of comparability.** KNOWN-ISSUES
   #44 records it: legendary crafting bottoms out in Spirit Shards and
   Karma. Both carry valuations in the defaults table, but a subtree that
   reaches ANY unvalued cost sets `HasUnvaluedCost`
   (`Models/CostLineUnitValue.cs:36`) and pushes the whole offer to the
   fallback tier, where the documented zero-coin-part limitation from
   section 6 applies.

**Where the partition falls, in one sentence:** the module can now price a
vendor offer whose cost lines are themselves obtainable by craft, Trading
Post or another vendor; it cannot price one whose cost lines are earned,
and it cannot tell you whether you are allowed to make the purchase at all.

---

## 8. Marked UNKNOWN

Four things could not be settled from the tree.

1. **Whether `GET /v2/characters/:id/crafting` can return a discipline
   string the module does not expect** (for example `Homesteader`).
   `Services/Gw2AccountSnapshotService.cs:347-348` preserves
   `cd.Discipline?.RawValue` precisely so an unrecognized string survives,
   and `CraftCompetencyEvaluator` does an ordinal string match against
   `RecipeOption.Disciplines`. If the API never returns `Homesteader`, then
   a hypothetical future Homesteader-only recipe at a non-zero rating would
   be silently excluded from the automatic pick. Today the two Homesteader
   recipes also declare nine real disciplines at rating 0, so nothing is
   wrong. *To settle: one live `/v2/characters/:id/crafting` response from an
   account with Homestead Refinement, or the Gw2Sharp `CraftingDiscipline`
   enum members.*

2. **How much of GW2's real vendor surface the 59,414-offer corpus
   covers.** The corpus is a wiki SMW scrape with a known ~5,400-offer drift
   superset and a hand-maintained exclusion list of one row. Coverage
   against the game is not measurable from inside the repo. *To settle: a
   fresh full scrape diffed against the committed seed, which
   `tools/VendorOfferUpdater` supports (`VendorOfferDiff`), plus a judgement
   call on the drift rows.*

3. **Whether a legendary chain in practice terminates in routes the solver
   can price.** I established the shape of the boundary (earned routes are
   unrepresentable) but not how many nodes of a specific legendary tree land
   on the wrong side of it. *To settle: run
   `tools/TaimisToolbench.Harness` over a named legendary's item id and
   count the `UnknownSource` and fallback-tier terminals. The harness exists
   and already references a Homestead/Gift-of-the-Homesteader chain.*

4. **Whether the seven "Achievement"/"Merchant" seed recipes are the whole
   set of hand-authored non-API recipes, or a sample.** The recipe-ingestion
   record says a from-scratch seeder run drops the four achievement-bit
   recipes because "the seeder has no code path that produces them", which
   implies a manual re-add step whose completeness is not enforced by
   anything I could find. *To settle: read
   `tools/TaimisToolbench.RecipeSeeder`'s merge path and whatever re-add
   procedure exists, if one is written down.*

---

## 9. What this establishes, compressed

- The module is a **cost engine for transactional acquisition**. Craft, TP,
  vendor. That is the whole vocabulary, and it is well built - merged-ceil
  batching, two-tier comparability, decision-only valuations, and now
  solved cost lines.
- It is **not an availability engine**. Of the gate kinds a legendary
  pathway actually crosses - achievements, collections, masteries, account
  unlocks, station locality, mode participation, vendor-side unlock items -
  it models exactly one and a half: crafting competency (soft, snapshot
  dependent) and learned recipes (advisory, after the fact).
- It is **mode blind**. WvW and PvP currencies are ints in a flat map. The
  data already knows "World vs. World" on 1,407 offers and throws it away.
- Its non-transactional fallback is **ten sentences of curated text**.
