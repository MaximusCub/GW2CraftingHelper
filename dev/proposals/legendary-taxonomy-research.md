# Legendary taxonomy: every class of legendary item and how it is obtained

Status: research note (no code changes). Written against `master` at commit
`d060c02` (MEASURED, `git log -1` at research time). Repo touched read-only
apart from this file.

Research date: 2026-08-29. All `wiki.guildwars2.com` pages and all
`api.guildwars2.com` calls were made on that date. Prices, caps and stock
rotate; nothing numeric here should be treated as durable.

Claims are tagged **MEASURED** (read directly off a page or an API response
fetched during this session), **INFERRED** (reasoned from measured facts, not
itself observed) or **GAP** (could not establish - stated as a gap rather than
filled with a guess).

---

## 0. Method, and its limits (read this first)

Three limits shape how much of this document you should trust as literal:

1. **The wiki was read through a summarizing fetch, not raw HTML.** The
   available fetch tool renders a page and then answers a prompt against it
   using a small model. Structural claims survive that round trip well ("this
   item is bought from a vendor", "this component is account bound"). Exact
   quantities do not always survive it: two fetches of related pages disagreed
   on the WvW Skirmish Claim Ticket total for a full armour set (one said 1,310
   for the base set, another said 2,620, a third said 7,880) and on the Mystic
   Clover price in the Wizard's Vault. **Every bare number in this document is
   therefore INFERRED unless it came from an API response.** Where a number
   matters to planner logic, re-verify it against raw wikitext
   (`?action=raw`) before encoding it, the way
   `dev/proposals/research-aa-spending-consensus.md` did.
2. **API measurements are exact.** Everything attributed to
   `api.guildwars2.com` below was run live and the response is quoted or
   summarised faithfully. These are the strongest claims here.
3. **Coverage is breadth-first.** The goal was a complete map of *classes* and
   *pathway shapes*, not a bill of materials for any one item. Per-item BOMs
   are deliberately shallow. Section 9 lists what was not pinned down.

---

## 1. Summary table

Counts are of distinct end products, not of skins or weights.

| Class | How many | Primary pathway | Game mode(s) | Worst planner hazard |
|---|---|---|---|---|
| Weapons Gen 1 | 21 enumerated (20 + Eternity) | Mystic Forge: precursor + weapon gift + Gift of Fortune + Gift of Mastery | PvE, but Gift of Battle is WvW | Precursor is tradeable, so craft-vs-buy is a real branch |
| Weapons Gen 2 | 16 | Mystic Forge, precursor crafting mandatory | PvE (HoT/PoF) | Precursor is account bound - there is no "buy" side to compare |
| Weapons Gen 3 | 16 + variant skins | Mystic Forge, precursor recipes bought from a vendor | PvE (EoD) | Repeatable variant-skin collections layered on top of the item |
| Weapon: Klobjarne Geirr | 1 (spear) | Mystic Forge, precursor Nyr Hrammr | PvE (Janthir Wilds) | Weekly-capped map shards impose a multi-week floor |
| Weapons: Ancora Bellum + Ancora Pax | 2 from 1 recipe | Open the Aetheric Anchor container | PvE (Visions of Eternity) | One recipe yields TWO legendaries as a bundle |
| Weapon: Wages of Stars | 1 (sword), unreleased | unknown | PvE | Announced, not shipped - see 2.5 |
| Armour: Obsidian | 18 pieces (3 weights x 6) | Learned recipe crafted at a station, OR pay Lyhr a fee to craft it for you | PvE open world (SotO) | Station is restricted to The Wizard's Tower and the API does not say so |
| Armour: Perfected Envoy | 18 pieces | Mystic Forge from a precursor armour set | PvE raids | Legendary Insights are kill-gated and weekly; unbuyable at any price |
| Armour: Triumphant Hero's / Mistforged | 18 pieces per skin line | Mystic Forge, consuming a vendor-bought ascended piece | WvW | The ascended input is destroyed by a normal stat swap |
| Armour: Ardent Glorious (Glorious Hero's / Mistforged) | 18 pieces per skin line | Mystic Forge, consuming a vendor-bought ascended piece | PvP | Tournament placement / rank gates on the skin line |
| Armour: Eikasia, Mists-Grasper | gloves only, 3 weights | Achievement for the first weight, vendor for the other two | PvE fractals (Fractal Incursion) | Only a glove exists - the set is incomplete by design |
| Aquabreather: Selachimorpha | 1, covers all 3 weights | Mystic Forge from precursor Agaleus | PvE open world (VoE) | One craft unlocks all three weights - not three crafts |
| Trinkets: rings | 3 | Mystic Forge; two also have a vendor shortcut | PvE raids / WvW / PvE open world | Two of three are non-unique (two can be equipped) |
| Trinkets: accessories | 4 | Mystic Forge, except Strife Unending which is a pure vendor buy | PvE / WvW | One is a vendor purchase with no recipe at all |
| Trinkets: amulets | 2 | Mystic Forge, or pure achievement completion | PvP / PvE story | One has no bill of materials whatsoever |
| Back items | 4 | Mystic Forge, gated on multi-tier collections | Fractals / PvP / WvW / PvE | One is permanently unrepeatable per account |
| Legendary relic | 1 | Mystic Forge | PvE | Crafting cost and unlock-catalogue cost are two separate tracks |
| Legendary rune | 1 item type, 7 wanted | Mystic Forge | PvE | Full coverage is a 7x multiple of one craft |
| Legendary sigil | 1 item type, 8 wanted | Mystic Forge | PvE | Full coverage is an 8x multiple of one craft |
| Legendary enrichment | **does not exist** | n/a | n/a | Do not model a node for it |
| Legendary infusion | **does not exist** | n/a | n/a | Do not model a node for it |

---

## 2. Weapons

### 2.1 Generation 1

MEASURED (<https://wiki.guildwars2.com/wiki/Legendary_weapon>), enumerated by
name: 20 weapons plus Eternity. They cover every land weapon type of the era
plus all three underwater types: Frostfang (axe), Incinerator (dagger), The Moot (mace), Quip
(pistol), Meteorlogicus (scepter), Bolt (sword), The Minstrel (focus), The
Flameseeker Prophecies (shield), Rodgort (torch), Howler (warhorn), Sunrise and
Twilight (greatsword), The Juggernaut (hammer), Kudzu (longbow), The Predator
(rifle), The Dreamer (short bow), The Bifrost (staff), Frenzy (harpoon gun),
Kamohoali'i Kotaki (spear), Kraitkin (trident).

GAP on the count: the summarizing fetch of that page printed a headline of "23
weapons" for Gen 1 and "15 weapons" for Gen 2, neither of which matches the name
lists it then produced (20 and 16 respectively). The name lists are what is used
here, since they are checkable; the headline totals are not. Re-derive the
counts from raw wikitext before quoting a total anywhere.

Eternity is structurally distinct: MEASURED from the API item description for
Twilight (`/v2/items/30704`), Eternity is forged from Sunrise + Twilight + 5
Piles of Crystalline Dust + 10 Philosopher's Stones. It consumes two finished
legendaries and needs no precursor and no legendary gift of its own.

Recipe shape (MEASURED): precursor + Gift of <weapon> + Gift of Fortune + Gift
of Mastery, combined in the Mystic Forge.

- Gift of Fortune = 77 Mystic Clovers + 250 Globs of Ectoplasm + Gift of Might
  + Gift of Magic. Gift of Might and Gift of Magic are each 4 x 250 tier-6
  materials, all tradeable, all deterministic.
  (<https://wiki.guildwars2.com/wiki/Gift_of_Fortune>)
- Gift of Mastery = Bloodstone Shard + 250 Obsidian Shards + Gift of
  Exploration + Gift of Battle.
  (<https://wiki.guildwars2.com/wiki/Gift_of_Mastery>)

Gates: level 80; the discipline that crafts the precursor and gift components
at 400/500; **Gift of Battle is a WvW reward track item with no PvE
substitute** and **Gift of Exploration comes only from 100 percent world
completion, twice per character, once per character ever**
(<https://wiki.guildwars2.com/wiki/Gift_of_Battle>,
<https://wiki.guildwars2.com/wiki/Gift_of_Exploration>). Both are account bound
and unpurchasable.

Precursors are exotic and tradeable, so precursor crafting is optional here and
"buy the precursor" is a genuine alternative branch (see 8.2).

### 2.2 Generation 2

MEASURED, enumerated by name (see the count gap in 2.1): 16 weapons, released
piecemeal across Heart of Thorns and Path of
Fire - Astralaria, Claw of the Khan-Ur, Eureka, HOPE, Xiuquatl, The Shining
Blade, The Binding of Ipos, Shooshadoo, Flames of War, Verdarach, Exordium,
Sharur, Pharus, The HMS Divinity, Chuka and Champawat, Nevermore. No underwater
weapon was ever added in this generation.

Recipe shape: precursor + weapon gift + **Mystic Tribute** + an expansion
mastery gift (Gift of Maguuma Mastery or Gift of Desert Mastery). Mystic Tribute
= 2 Gift of Condensed Magic + 2 Gift of Condensed Might + 77 Mystic Clovers +
250 Mystic Coins (<https://wiki.guildwars2.com/wiki/Mystic_Tribute>).

The structurally important fact: **Gen 2 precursors are account bound and not
tradeable**, so precursor crafting is mandatory and there is no buy side to
compare against (<https://wiki.guildwars2.com/wiki/Precursor_weapon>).

### 2.3 Generation 3 (Aurene)

MEASURED: 16 Aurene-named weapons covering the same 16 land types, introduced
with End of Dragons. Recipe shape: precursor + weapon gift + **Draconic
Tribute** + Gift of Jade Mastery. Draconic Tribute = 1 Gift of Condensed Might
+ 1 Gift of Condensed Magic + 38 Mystic Clovers + 5 Amalgamated Draconic
Lodestone (<https://wiki.guildwars2.com/wiki/Draconic_Tribute>) - notably about
half the clover load of Gen 1 and Gen 2.

Gen 3 skips collections for the precursor: the tier recipes are simply bought
from the vendor Leivas once the relevant Legendary Crafting mastery rank is
unlocked, and the precursors are tradeable exotics
(<https://wiki.guildwars2.com/wiki/Precursor_Crafting>). Note the wiki page
title capitalises "Crafting"; the lowercase form 404s.

On top of the item sits a set of **repeatable variant-skin collections** in the
style of each Elder Dragon (Zhaitan, Mordremoth, Kralkatorrik, Jormag,
Primordus, Soo-Won), several of which require content from other expansions or
Living World seasons. These are cosmetic unlocks layered on an already-finished
weapon, so they are a second, separate cost track a planner would have to model
apart from "acquire the weapon".

### 2.4 Klobjarne Geirr (Janthir Wilds spear)

MEASURED: legendary spear, precursor Nyr Hrammr, forged with Gift of Janthir
Wilds + Gift of the Homesteader + Gift of Klobjarne Geirr
(<https://wiki.guildwars2.com/wiki/Legendary_Spear:_Klobjarne_Geirr>). A
collection category of five achievements gates it, including map-mastery tracks
and a "250 enemies defeated with spears in Janthir Wilds" counter.

INFERRED: weekly-capped map shards for Lowland Shore and Janthir Syntri put a
multi-week floor under it. GAP: the exact weekly cap and the derived minimum
week count were reported through a summarizing fetch and were not independently
verified.

### 2.5 Visions of Eternity weapons

MEASURED (<https://wiki.guildwars2.com/wiki/Guild_Wars_2:_Visions_of_Eternity>,
released 2025-10-28, the sixth expansion):

- **Aetheric Anchor** (<https://wiki.guildwars2.com/wiki/Aetheric_Anchor>) is a
  legendary container forged from Gift of the Survivors + Gift of the People +
  Gift of Insight + Gift of the Elders. Opening it yields **both** Ancora Bellum
  (spear) and Ancora Pax (staff). This is the single most planner-hostile shape
  found in this research - see 8.1.
- Gift of the People is bought from Canach at Breezy Cay, Shipwreck Strand;
  Gift of the Elders from Captain Emund at Foothold Bivouac, Shimmering Basin
  (<https://wiki.guildwars2.com/wiki/Gift_of_the_People>,
  <https://wiki.guildwars2.com/wiki/Gift_of_the_Elders>).
- MEASURED from the Ancora Bellum page: "This legendary weapon can only be added
  to the Legendary Armory once, despite not being unique" - an Armory cap that
  does not follow from the item's uniqueness flag.
- **Wages of Stars** is a legendary sword announced for the "Code of Creation"
  release, dated 2026-09-15 (the third quarterly update of Visions of Eternity).
  As of the research date it has **not shipped**. Treat as announced-only.

---

## 3. Armour

### 3.1 Obsidian armour (PvE open world, Secrets of the Obscure)

This is the one legendary end product in the whole taxonomy that the official
recipe API knows about, and it is instructive.

MEASURED, live API: `/v2/recipes/search?output=101516` (Obsidian Light Crown)
returns `[13946]`, and `/v2/recipes/13946` returns
`disciplines: ["Tailor"], min_rating: 500, flags: ["LearnedFromItem"]` with four
ingredients. `/v2/recipes/search?output=101544` (Obsidian Heavy Helmet) returns
`[14079]`.

MEASURED from the wiki (<https://wiki.guildwars2.com/wiki/Obsidian_armor>):
"With the recipe learned the armor pieces can be crafted by a level 500
Armorsmith/Leatherworker/Tailor (depending on the armor weight) **but only in
The Wizard's Tower**." The API response above carries no field that expresses
that restriction. A planner reading the API alone will tell the player they can
craft this at any station.

MEASURED, the Lyhr pattern: "Alternatively, Lyhr can assist with the crafting
for an additional cost of 10 Globs of Ectoplasm per piece." So the same output
has two costs - self-craft (free, but requires the discipline at 500) or
vendor-assisted (10 ectoplasm, requires no discipline at all). The same vendor
(and Ward Crafter Lucirae at Lowland Shore) also sells pre-assembled Gift of
Condensed Might / Magic for an ectoplasm markup instead of forging the four
sub-gifts yourself.

Gates: the recipe is bought from Lyhr for coin, but only after the Obsidian
Armor Crafting mastery; the Astral Ward and Oneiros-Spun skin of the same slot
must be unlocked first; per-piece components include a location gift (Astral
Ward, Skywatch, Amnytas or Inner Nayos) and an Arcanum piece from a collection.

### 3.2 Perfected Envoy armour (PvE raids, Heart of Thorns)

MEASURED (<https://wiki.guildwars2.com/wiki/Perfected_Envoy_armor>): forged from
a Refined Envoy armour precursor set, itself the reward of the Envoy Armor I and
II collections. Legendary Insights drive the cost, and they come only from raid
encounters. The page reports 150 Legendary Insights for a first set and 300 for
subsequent sets, with a five-week minimum for the first and four weeks
thereafter - INFERRED numbers, summarizing fetch. Nothing in this chain is
tradeable.

MEASURED, API: `/v2/recipes/search?output=80190` (Perfected Envoy Vestments)
returns `[]`.

### 3.3 WvW armour

MEASURED (<https://wiki.guildwars2.com/wiki/Legendary_armor>): the legendary
item shares the name of the skin line - Triumphant Hero's armor, or Mistforged
Triumphant Hero's armor. It is forged in the Mystic Forge and **consumes the
matching ascended piece**, which is bought from the Skirmish Supervisor with WvW
Skirmish Claim Tickets, Memories of Battle and coin. Mistforged additionally
requires WvW rank 500; the base Triumphant Hero's legendary craft has no rank
requirement.

MEASURED, and the sharpest trap in this section
(<https://wiki.guildwars2.com/wiki/Mistforged_Triumphant_Hero%27s_armor>): "Do
not attempt to stat change your precursor item with the standard stat swap
recipe... it will have been replaced with a generic ascended item that cannot be
used as a legendary precursor." A vendor-bought ascended piece is not
interchangeable with an equivalently-statted ascended piece.

GAP: the ticket and Memory-of-Battle totals could not be pinned down. Three
fetches produced three different full-set figures. Do not encode any of them.

### 3.4 PvP armour

MEASURED: Ardent Glorious armor is the legendary PvP set, with Glorious Hero's
and Mistforged Glorious Hero's as the two skin lines. Same shape as WvW: the
Mystic Forge recipe consumes an ascended piece bought from the Ascended Armor
League Vendor in the Heart of the Mists. Glorious Hero's is gated on Monthly
Automated Tournament placement; Mistforged on PvP rank 100. The same stat-swap
warning applies verbatim.

### 3.5 Eikasia, Mists-Grasper (PvE fractals)

MEASURED (<https://wiki.guildwars2.com/wiki/Eikasia,_Mists-Grasper>): legendary
**gloves only**, in all three weights. The first weight is free from the
"Incursive Investigation" achievement chain, which requires collecting
Fractalline Dust from the Fractal Incursion quickplay mode. The other two
weights are bought from the "Mist Stranger" vendor at the Fractal Incursion
Conference, one per account, for a Fractalline Spark plus Gift of Magical
Prosperity, Gift of Mighty Prosperity and Icy Runestones.

This is the only fractal legendary armour piece. There is no fractal legendary
chest, helm, shoulders, leggings or boots. A planner that assumes "legendary
armour" means a six-piece set will mis-model this entirely.

### 3.6 Selachimorpha (legendary aquabreather)

MEASURED (<https://wiki.guildwars2.com/wiki/Selachimorpha>): the first and only
legendary aquabreather, added with Visions of Eternity. Precursor **Agaleus**,
unlocked by the 24-step "Acquiring Agaleus" collection spanning Shipwreck
Strand, Siren's Reef, Southsun Cove, Lion's Arch, Bloodtide Coast and Cursed
Shore (<https://wiki.guildwars2.com/wiki/Acquiring_Agaleus>). Forge recipe:
Agaleus + Gift of the Survivors + Gift of the People + Gift of Castoran Mastery.

**One craft unlocks all three armour weights** via the Selachimorpha Container.
Every other armour class in this taxonomy needs a separate craft per weight.

Mastery gates on Shipwreck Strand and Starlit Weald apply to some component
purchases. INFERRED per the fetch: repeat copies of first-time map-completion
gifts cost coin plus a large karma sum.

---

## 4. Trinkets and back items

Slot coverage is complete: every trinket slot and the back slot has at least one
legendary option, MEASURED off the wiki's own disambiguation pages.

**Rings (3)**

- **Coalescence** - PvE raids (Path of Fire wings). Mystic Forge; Coalescence
  I/II/III collections each gated on the prior. Legendary Insights are
  earn-only. Gift of Compassion is bought from Scholar Glenna at the raid wing.
  (<https://wiki.guildwars2.com/wiki/Coalescence>)
- **Conflux** - WvW only. Mystic Forge, **or** trade a Slumbering Conflux to
  Grandmaster Craftsman Hobbs in Lion's Arch. Non-unique: two can sit in the
  Armory. (<https://wiki.guildwars2.com/wiki/Conflux>)
- **Endless Summer** - PvE open world, Visions of Eternity. Mystic Forge from
  Gift of Rays + Gift of the Survivors + Gift of the People + Gift of the Hylek.
  Gated on the Shipwreck Strand and Starlit Weald mastery tracks (36 map
  achievements each) plus a "Radiance of the Sun God" achievement. Non-unique.
  (<https://wiki.guildwars2.com/wiki/Endless_Summer>)

**Accessories (4)**

- **Aurora** - PvE open world, requires all six Living World Season 3 episodes.
  Gift of Draconic Mastery needs 250 of each of five separate LWS3 map
  currencies, so it cannot be shortcut with coin.
  (<https://wiki.guildwars2.com/wiki/Aurora>)
- **Vision** - PvE open world, all six Living World Season 4 episodes.
  (<https://wiki.guildwars2.com/wiki/Vision>) MEASURED: it does **not** require
  Aurora first, despite reading as a sequel.
- **Stella Radians** - PvE, Visions of Eternity. Mystic Forge from Mystic
  Tribute + Gift of Galdra + Gift of Shadowstones + Vial of Liquid Shadowstone,
  the last being an achievement reward from the story chapter "So It Is
  Written". (<https://wiki.guildwars2.com/wiki/Stella_Radians>)
- **Strife Unending** - WvW only, and structurally the odd one out: it is a
  **straight vendor purchase from Dugan**, paid in three gifts, gated on the
  "Mists Research: Strife Unending" achievement. There is no Mystic Forge step.
  Capped at 2 per account. (<https://wiki.guildwars2.com/wiki/Strife_Unending>)

**Amulets (2)**

- **Transcendence** - primarily PvP. Mystic Forge, or trade a Slumbering
  Transcendence to Hobbs. Its Mist Pendant component has **three mutually
  exclusive recipes** in three different game modes (fractals: Pristine Fractal
  Relics + Integrated Fractal Matrix; PvP: Ascended Shards of Glory + Shards of
  Glory; WvW: Skirmish Claim Tickets + Memories of Battle). A now-discontinued
  PvP "Legendary Reliquary" reward track was a fourth path and must be excluded
  from any current cost model.
  (<https://wiki.guildwars2.com/wiki/Transcendence>)
- **Prismatic Champion's Regalia** - PvE story. **No bill of materials at all.**
  It is the terminal reward of the "Seasons of the Dragons" meta-achievement,
  which requires completing all 24 Return meta-achievements across Living World
  Seasons 2, 3, 4 and the Icebrood Saga. There is no vendor, no forge step and
  no material substitution. It cannot be priced.
  (<https://wiki.guildwars2.com/wiki/Prismatic_Champion%27s_Regalia>)

**Back items (4)**

- **Ad Infinitum** - PvE fractals. Four sequential collections, each gated on
  the prior, then a forge combine. Fractal Attunement mastery gates some
  intermediate purchases. The "Unbound" and "Gift of Infinity" achievement
  rewards are account bound and unbuyable.
  (<https://wiki.guildwars2.com/wiki/Ad_Infinitum>)
- **The Ascension** - PvP leagues. MEASURED: "can only be made once because the
  Gift of the Competitor is composed of achievement rewards that are rewarded
  only once." For a planner this is not a cost, it is a boolean: either the
  account already has the achievement flags or that account is permanently
  blocked from a second copy. (<https://wiki.guildwars2.com/wiki/The_Ascension>)
- **Warbringer** - WvW. MEASURED: "does not require any expansion to obtain",
  which makes it the only legendary found with no expansion gate at all. Four
  "Wings of War" sub-items gate on WvW world ranks 50/100/200/350. Skirmish
  Claim Tickets are weekly-capped and unbuyable, giving a real calendar floor.
  (<https://wiki.guildwars2.com/wiki/Warbringer>)
- **Orrax Manifested** - PvE open world, Janthir Wilds (Absolution release).
  Mystic Forge from Gift of the Mistburned Isles + Gift of Shadows + Gift of the
  Feast + the precursor Orrax Contained. Gated on the "Salvation's Cost" story
  achievement plus five "Unknown Nightmares" sub-collections. One collection
  step also yields a homestead decoration, so an intermediate has a second,
  non-crafting use that a planner must not let the player consume by accident.
  (<https://wiki.guildwars2.com/wiki/Orrax_Manifested>)

---

## 5. Underwater and newer weapon types

- **Spear, harpoon gun, trident**: all three have a Gen 1 legendary
  (Kamohoali'i Kotaki, Frenzy, Kraitkin) and **nothing since**. Gen 2 and Gen 3
  added zero underwater weapons. INFERRED from the generation tables on
  <https://wiki.guildwars2.com/wiki/Legendary_weapon>; GAP: not double-sourced
  against raw wikitext.
- **Land spears**: Janthir Wilds made spears usable on land
  (<https://wiki.guildwars2.com/wiki/Spear>). MEASURED: Kamohoali'i Kotaki is
  the *same item*, reclassified as amphibious - there is no separate land
  variant to acquire. The Legendary Armory cap for it was raised to 4.
- **New legendary spears since**: Klobjarne Geirr (Janthir Wilds, 2.4) and
  Ancora Bellum (Visions of Eternity, 2.5).
- **Legendary aquabreather**: Selachimorpha, Visions of Eternity (3.6). Before
  that expansion this slot had no legendary at all.
- **Remaining hole**: harpoon gun and trident have had no new legendary since
  Gen 1, and there is no second option in either type.

---

## 6. Relics, runes, sigils

MEASURED (<https://wiki.guildwars2.com/wiki/Legendary_relic>,
<https://wiki.guildwars2.com/wiki/Legendary_rune>,
<https://wiki.guildwars2.com/wiki/Legendary_sigil>): each of the three is a
single generic item, not a family of named variants. All three are forged in the
Mystic Forge from the same four-slot shape: a class gift (Gift of Relics / Gift
of Runes / Gift of Sigils) + Gift of Craftsmanship + Gift of Condensed Might +
Gift of Condensed Magic.

- **Legendary relic**: Gift of Relics needs Exotic Relics and a large Lucent
  Crystal load. Armory cap 1 - one relic covers every character. But the set of
  relic effects it can mimic is unlocked separately: core relics are automatic,
  SotO relics come with expansion ownership, and Janthir Wilds and later relics
  each need their own achievement collection. **Crafting cost and unlock-catalogue
  cost are two independent tracks.**
- **Legendary rune**: Gift of Runes needs Mystic Aspects, Piles of Lucent
  Crystal, Charms, Mystic Clovers, ectoplasm and Obsidian Shards. Armory cap 7 -
  six armour slots plus the aquabreather. Full coverage is **seven separate
  crafts**, not one.
- **Legendary sigil**: Gift of Sigils recipe is bought from Miyani for coin;
  needs Mystic Motes, Piles of Lucent Crystal, Symbols and Mystic Clovers.
  Armory cap 8. Full coverage is **eight separate crafts**.

The rune/relic split: MEASURED (<https://wiki.guildwars2.com/wiki/Relic>) relics
became a separate equipment slot in the second Secrets of the Obscure quarterly
release, taking over effects that had been the sixth-tier rune bonus. As
compensation "Three Relic Chests were distributed to all characters that were
level 60 or above at the time of release" - a general grant, not something
specific to legendary rune owners. GAP: whether existing legendary rune owners
got anything beyond that general grant could not be established from the wiki
pages read; it would need the release patch notes.

MEASURED, API: `/v2/recipes/search?output=91536` (Legendary Rune) returns `[]`.

---

## 7. Enrichments and infusions: neither exists

MEASURED (<https://wiki.guildwars2.com/wiki/Enrichment>,
<https://wiki.guildwars2.com/wiki/Infusion>, and the category list on
<https://wiki.guildwars2.com/wiki/Legendary>): there is **no legendary
enrichment** and **no legendary infusion**. The enrichment list is entirely
non-legendary (Gilded, Magical, Experienced, Karmic and the cosmetic ones); the
infusion list is agony, attribute, WvW, swim-speed and cosmetic, none legendary.
The wiki's own Legendary rarity page enumerates weapons, armour, back items,
trinkets, runes, relic and sigils - and omits both.

What legendary gear gives instead is structural: it ships with its slot's
infusion and upgrade slots already present and freely reconfigurable. The
infusions and enrichments socketed into those slots remain ordinary ascended or
exotic items.

**Planner consequence**: do not create an "acquire legendary enrichment" or
"acquire legendary infusion" node. If a user asks for one, the correct answer is
that it does not exist, not an empty plan.

GAP: absence was established from the current wiki, not from a developer
statement ruling it out permanently.

---

## 8. What would break a naive craft-vs-buy planner

Ordered by how badly.

### 8.1 One recipe, two outputs (Aetheric Anchor)

MEASURED: forging one Aetheric Anchor yields **both** Ancora Bellum and Ancora
Pax. Every cost model in this domain assumes a recipe has one output and that
the cost of that output is the cost of the recipe. Here the cost must be split
across two end products, or the planner will price the spear at the full cost of
the anchor while silently handing the player a free legendary staff. There is
also no way to want only one of them.

The same shape appears more mildly in Selachimorpha, where a single craft
unlocks all three armour weights, and in Eternity, where the recipe consumes two
finished legendaries.

### 8.2 The vendor that mirrors the recipe and charges a fee

The Lyhr pattern already found for Obsidian armour is not a one-off. It recurs
at three levels:

- **Output level**: Lyhr will craft an Obsidian armour piece for you for 10
  Globs of Ectoplasm, so a player with no crafting discipline at 500 has a
  strictly-worse-but-valid path the planner must know about.
- **Component level**: Lyhr and Ward Crafter Lucirae both sell pre-assembled
  Gift of Condensed Might and Gift of Condensed Magic for an ectoplasm markup,
  competing with forging the four sub-gifts yourself.
- **Whole-item level**: Grandmaster Craftsman Hobbs will take a Slumbering
  Conflux or Slumbering Transcendence and hand back the finished legendary.

GAP: whether the Slumbering items are themselves built from the same four gifts,
or have a distinct bill of materials, was not established. Until it is, do not
encode Hobbs as an equivalent alternative path.

### 8.3 Randomness in the middle of the tree (Mystic Clovers, Obsidian Shards)

MEASURED, API: `/v2/items/19675` describes Mystic Clover as "randomly made in
the Mystic Forge". The forge recipe (Obsidian Shard + Mystic Coin + Glob of
Ectoplasm + 6 Philosopher's Stones, or a Mystic Crystal variant) succeeds
roughly a third of the time and returns partial-value junk otherwise. A single
recipe price is not the cost; only a probability-weighted expectation with
failure recovery is.

Against that sit around nine **deterministic** vendor sources, each priced in a
different currency and most with a weekly cap: Manfred Njallson (Magnetite
Shards, weekly 15), BUY-4373 (Fractal Relics, weekly 10), Dugan (Badges of
Honor + Skirmish Tickets, weekly 5), the PvP League Vendor (weekly 5), Miyani
and the Mystic Forge Attendants (weekly 10), Lyhr and Ward Crafter Lucirae
(uncapped), plus the Wizard's Vault at a seasonal cap. Numbers INFERRED
(summarizing fetch) and in one case self-contradictory across two fetches.

Obsidian Shard has the same shape: a random forge recipe competing with laurel,
fractal, Volatile Magic, mastery-currency, festival, WvW and Wizard's Vault
vendors, in seven different currencies, with no single correct price.

This is the case the repo already partly understands - see
`dev/proposals/addendum-astral-acclaim.md` on the Wizard's Vault clover route
being economically inert because Astral Acclaim cannot be valued.

### 8.4 Components that cannot be bought at any price

A gold-denominated planner will either price these at zero or refuse to plan.
Neither is right. The list, all MEASURED as account bound and unpurchasable:

- **Gift of Battle** - WvW reward track only. Required by Gen 1, Gen 2 and Gen 3
  weapons via Gift of Mastery, and by Aurora, Conflux and Warbringer. NOT
  universal: a closure walk over ref/recipes_seed.json finds it absent from Nyr
  Hrammr, Obsidian armour, Perfected Envoy armour, Ad Infinitum and the
  Legendary Rune (7 of 18 trees in the harness sweep). An earlier draft of this
  document called it universal on a single source; that was wrong. There is no
  PvE substitute where it IS required.
- **Gift of Exploration** - 100 percent world completion, twice per character,
  once per character ever.
- **Legendary Insights** - raid boss kills. Coalescence and Perfected Envoy.
- **Spirit Shards** - level 80 XP overflow. Bloodstone Shard costs 200 of them,
  and several clover vendors want them. No gold path exists.
- **Provisioner Tokens** - traded from crafting materials at faction
  provisioners, weekly-capped at 7 per trade-in item at most vendors, stored in
  the wallet, account bound.
- **WvW Skirmish Claim Tickets, PvP League Tickets, Ascended Shards of Glory,
  Fractalline Dust, map currencies** - all earn-only.

### 8.5 The official recipe API does not contain the legendary tree

This is measured, not inferred, and it is the most actionable finding for this
module.

MEASURED, live `/v2/recipes/search?output=<id>` returns an empty array for every
one of: Twilight (30704), Astralaria (76158), Aurene's Fang (95675), Nyr Hrammr
(103973), Mystic Tribute (71820), Mystic Clover (19675), Legendary Rune (91536),
Warbringer (81462), Ad Infinitum (74155), Conflux (93105), Aurora (81908),
Prismatic Champion's Regalia (95380), Perfected Envoy Vestments (80190).

It returns a real recipe id for exactly one class tested: the station-crafted
Obsidian armour pieces (13946, 14079).

So the API models the one legendary that is crafted at a normal station and
models none of the Mystic Forge chain. The repo already compensates:
`ref/mystic_forge_recipes.json` holds 1,591 wiki-sourced forge recipes, and
`ref/recipes_seed.json` carries 14,966 recipes of which 1,595 have negative ids,
that is, are synthetic rather than API-derived. Spot-checked as present in the
seed: Twilight, Eternity, Astralaria, Aurene's Fang, Mystic Tribute, Mystic
Clover, Legendary Rune, Klobjarne Geirr and Nyr Hrammr. Correctly absent: Gift
of Battle, which has no recipe of any kind.

### 8.6 Restrictions the API cannot express

Recipe 13946 returns `Tailor`, `min_rating: 500`, `LearnedFromItem` - and no
field for "only craftable in The Wizard's Tower", which the wiki states as a
hard rule. Station restriction is invisible to any API-only model. So are:
mastery prerequisites, achievement prerequisites, expansion ownership, WvW and
PvP rank floors, per-account purchase limits, and the Armory caps.

### 8.7 Items with no bill of materials at all

Prismatic Champion's Regalia is pure achievement completion across four Living
World seasons. The Ascension is a one-time account flag that, if missed, cannot
be re-earned. A planner must be able to answer "this cannot be costed" rather
than producing an empty or zero-cost plan.

### 8.8 Second-and-later copies cost differently

MEASURED: Gen 2 and Gen 3 precursor collections become repeatable once completed
once, with the scavenger-hunt components then purchasable from Hobbs. Perfected
Envoy reports 150 Legendary Insights for the first set and 300 for later ones.
Eikasia's first weight is free and the other two are bought. Selachimorpha's
repeat map-completion gifts have their own coin-and-karma price. A flat
per-craft cost is wrong in both directions depending on the item.

### 8.9 Calendar floors that no amount of gold removes

Weekly-capped clover vendors, weekly Legendary Insight income, weekly skirmish
tickets, the daily-capped ascended refinement materials already tracked in
`ref/daily_cooldown_items.json`, and seasonal Wizard's Vault caps. Several
legendaries have a minimum number of weeks that is independent of wealth. The
repo's `TimegatedCapType` currently models Daily and Weekly only; seasonal caps
are unmodelled, as `dev/proposals/addendum-astral-acclaim.md` records.

### 8.10 Inputs that a normal operation destroys

MEASURED: stat-swapping a vendor-bought ascended WvW or PvP armour piece with
the standard Mystic Forge stat-swap recipe silently converts it into a generic
ascended item that is no longer a valid legendary precursor. A planner that
tells a player to stat-swap for convenience would destroy the input.

### 8.11 Legendary rarity is not the same as "a legendary"

MEASURED, API: `/v2/items/19675` reports Mystic Clover as `"type": "Trophy",
"rarity": "Legendary"`. Rarity is not a usable test for "this is a legendary end
product". Any classifier keyed on rarity alone will sweep in crafting
intermediates.

---

## 9. Gaps: what this research did not establish

Stated plainly rather than filled in.

1. **Numbers generally.** See section 0. The WvW armour ticket totals, the
   Mystic Clover price and seasonal cap in the Wizard's Vault, the Perfected
   Envoy week counts and the Klobjarne Geirr weekly shard cap all came through a
   summarizing fetch and at least two of them contradicted a second fetch.
2. **Gift of Maguuma Mastery, Gift of Desert Mastery, Gift of Jade Mastery** -
   not fetched individually. Whether they reuse Gift of Exploration and Gift of
   Battle or substitute expansion currencies is unverified, and it matters
   because Gift of Battle being universal is one of the load-bearing claims in
   8.4.
3. **The Slumbering Conflux / Slumbering Transcendence bill of materials** -
   whether the Hobbs shortcut is genuinely equivalent to the forge path.
4. **Gift of Insight** (Aetheric Anchor) - the reported clover and Amalgamated
   Draconic Lodestone quantities were not verified.
5. **Sub-recipes for Gift of Galdra, Gift of Shadowstones** (Stella Radians) and
   **Gift of the Mist Warrior, Gift of the Mistwalker, Gift of the Warclaw**
   (Strife Unending).
6. **The source of Exotic Relics** for the Gift of Relics.
7. **Whether the Legendary Rune description was updated post-SotO** to reflect
   five rune bonus tiers instead of six, and whether prior legendary rune owners
   received anything beyond the general three Relic Chests grant.
8. **Whether Gen 2 and Gen 3 truly added no underwater weapons** - high
   confidence, single-sourced from a summarizing fetch of the generation tables.
9. **Whether The Ascension's Path of the Ascension I-IV achievements are
   re-offered identically in later PvP league seasons.**
10. **Time gates inside individual precursor collections** - established that
    they exist structurally, not their durations.
11. **Wages of Stars** - announced for 2026-09-15, unreleased, no recipe known.
12. **Item ids** were resolved through the wiki's `askargs` semantic API for the
    handful used in section 8.5. No systematic id list for the newer items
    (Selachimorpha, Ancora Bellum, Ancora Pax, Agaleus, Orrax Manifested) was
    built.

---

## 10. Sources

Wiki hub pages, all fetched 2026-08-29:

- <https://wiki.guildwars2.com/wiki/Legendary>
- <https://wiki.guildwars2.com/wiki/Legendary_weapon>
- <https://wiki.guildwars2.com/wiki/Legendary_armor>
- <https://wiki.guildwars2.com/wiki/Legendary_trinket>
- <https://wiki.guildwars2.com/wiki/Legendary_Armory>
- <https://wiki.guildwars2.com/wiki/Precursor_Crafting>
- <https://wiki.guildwars2.com/wiki/Mystic_Forge>
- <https://wiki.guildwars2.com/wiki/Mystic_Clover>
- <https://wiki.guildwars2.com/wiki/Relic>

Per-item pages are cited inline in the section that uses them.

API calls, all run live 2026-08-29 against `api.guildwars2.com`:
`/v2/items/19675`, `/v2/items/30704`, `/v2/recipes/13946`, and
`/v2/recipes/search?output=<id>` for the fourteen ids listed in 8.5.

Repo files read (read-only): `ref/mystic_forge_recipes.json`,
`ref/recipes_seed.json`, `ref/daily_cooldown_items.json`,
`ref/acquisition_hints_seed.json`,
`dev/proposals/addendum-astral-acclaim.md`,
`dev/proposals/research-aa-spending-consensus.md`.
