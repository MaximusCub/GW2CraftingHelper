# Legendary support: gap analysis and proposals

Status: PROPOSAL. Nothing here is implemented. Written 2026-08-29 by
synthesising two source documents produced in parallel:

- `dev/proposals/legendary-taxonomy-research.md` - what the game actually
  requires, researched from the wiki and the official API.
- `dev/proposals/module-capability-audit.md` - what this codebase can
  represent, read out of the source and the shipped seeds.

Read either of those for evidence. This document is the diff between them
and what to do about it. Every claim below is sourced from one of the two;
where they disagree with each other, or where a number is unverified, this
document says so rather than averaging them.

---

## 1. The finding, in one paragraph

The module is a **cost engine for transactional acquisition**: craft it, buy
it on the Trading Post, or buy it from a vendor. Within that vocabulary it is
well built. Legendary crafting is **substantially non-transactional**. Gift of
Gift of Battle comes from a WvW reward track. It is required by Gen 1, Gen 2
and Gen 3 weapons (via Gift of Mastery) and by several trinkets and back items
(Aurora, Conflux, Warbringer) - but NOT universally: a dependency-closure walk
over `ref/recipes_seed.json` finds it absent from Nyr Hrammr (a Janthir Wilds
weapon), Obsidian armour, Perfected Envoy armour, Ad Infinitum and the
Legendary Rune. Measured 7 of 18 trees in the harness sweep. Presence is proof;
absence is weaker, since a recipe missing from our seed looks identical to a
genuine absence. Gift of Exploration is world completion, once per
character ever. Legendary Insights are raid boss kills. Spirit Shards are
level-80 XP overflow, and a Bloodstone Shard costs 200 of them. Provisioner
Tokens are weekly-capped trade-ins. None of these has a price, and no amount
of gold produces them.

So the gap is not a bug and not a missing recipe. It is a **category
mismatch**: the module answers "what is the cheapest way to obtain this",
and a legendary asks "what do I have to *do*, and over how many weeks".

---

## 2. What is already sound, and should not be touched

Worth stating first, because the rest of this document is about weaknesses.

- **The recipe data is better than the official API.** MEASURED in the
  taxonomy research: `/v2/recipes/search?output=` returns an empty array for
  fourteen of the fifteen legendary outputs tested, including Twilight,
  Astralaria, Aurene's Fang, Mystic Tribute, Mystic Clover and Warbringer. It
  returns a real recipe for exactly one class, the station-crafted Obsidian
  pieces. The repo already compensates: 1,591 wiki-sourced forge recipes and
  1,595 synthetic negative-id entries, with the legendary chain spot-checked
  present. **The foundation is not the problem.**
- The cost engine itself - merged-ceil batching, two-tier comparability,
  decision-only valuations, and the cost-line expansion that just landed.
- Coverage of item classes is complete. All 13 trinkets and back items, every
  weapon generation, all five armour sets, the aquabreather. And a myth
  retired: **legendary enrichments and infusions do not exist** - legendary
  gear supplies the slots, not an item to put in them.

---

## 3. The diff

Each row is a pattern the game presents, against what the module can
represent. "Consequence" is what a user experiences.

| # | Game pattern (taxonomy) | Module today (audit) | Consequence |
|---|---|---|---|
| 1 | Components that cannot be bought at any price: Gift of Battle, Gift of Exploration, Legendary Insights, Spirit Shards, Provisioner Tokens | `AcquisitionSource` has five members; none is a reward track, drop, salvage, collection or map completion. Such an item resolves to `UnknownSource`. The entire fallback is **10 curated sentences** in `ref/acquisition_hints_seed.json` | The plan stops at the boundary and says UNKNOWN for the components that decide whether the project is feasible at all |
| 2 | Calendar floors independent of wealth: weekly clover vendors, weekly Legendary Insight income, weekly skirmish tickets, seasonal Wizard's Vault caps | `TimegatedCapType` models Daily and Weekly, **seasonal is unmodelled**. Both time gates are advisory notices, never a re-route. 375 daily / 364 weekly / 28 seasonal caps out of 59,414 offers | The plan cannot say "this takes at least N weeks", which for a legendary is often the answer that matters more than the gold |
| 3 | Vendor exchanges gated behind an unlock item ("Recipe: Legendary Obsidian Armor") | `VendorOffer` has **no required-item field**. Already recorded in KNOWN-ISSUES:748-756 as "the one worth doing" | The plan recommends an exchange the player cannot use |
| 4 | Achievement and collection gates (the Arcanum needs "Astral Heartbeat"; Gen 2/3 precursors are collections) | `/v2/account/achievements` is **called nowhere**; achievement bits are read as numbers for dedup arithmetic only | Cost is right; the player finds the item unpurchasable |
| 5 | Mode-specific pathways, PvE / WvW / PvP, with parallel armour sets | **No concept of game mode anywhere** - no enum, field, filter or setting. Yet 1,407 offers already carry "World vs. World" in `locations`, which nothing reads, and 3,583 carry a mode word in `merchantName` | Cannot label, prefer or exclude a mode route. A WvW ticket and a Laurel are the same kind of thing to the code |
| 6 | One recipe, two outputs: the Aetheric Anchor yields **both** Ancora Bellum and Ancora Pax | Every cost model assumes one recipe means one output | Cost is attributed to one item when it bought two. Milder variants: Selachimorpha (one craft, three weights), Eternity (consumes two finished legendaries) |
| 7 | Second-and-later copies cost differently: repeatable precursor collections, Perfected Envoy at 150 then 300 Legendary Insights, Eikasia's first weight free | No concept of "how many have you made" | A flat per-craft cost is wrong in both directions depending on the item |
| 8 | Randomness competing with ~9 capped deterministic alternatives for one leaf (Mystic Clover, Obsidian Shard) | Fractional-output EV exists and works - but **exactly one recipe in the whole seed sets `expectedOutputCount`** | The machinery is right and almost entirely unexercised |
| 9 | Station locality ("Wizard's Tower stations only"), mastery gates | Neither modelled. `VendorOffer.Locations` is parsed and never read | Cost is right, the trip is longer than implied |

---

## 4. Proposals, ranked by value per unit of effort

### P1. Make non-transactional acquisition a first-class source (LARGE, highest value)

Row 1 is the boundary legendaries cross repeatedly, and everything else in
this table is smaller than it. `AcquisitionSource` should be able to say
"earned: WvW reward track", "earned: world completion", "earned: raid
encounter" and carry a short description plus, where known, a rate or a cap.

The bar is deliberately low to begin with: **even a typed source with a
sentence beats `UnknownSource` plus a curated hint table with ten rows in
it.** It does not need to cost these routes. It needs to stop pretending it
does not know what they are.

Gift of Battle is the case to reason from, but NOT for the reason an earlier
draft of this document gave. `ref/vendor_offer_exclusions.json` does remove a
Battle Master vendor row by hand - and the recorded reason is that the sale was
removed from the game in the Spring 2016 Quarterly Update, hand-verified with a
source. That exclusion is a correct claim that the wiki is stale, it is right on
its own merits, and P1 must NOT revert it: doing so would reintroduce pricing
for a vendor path that has not existed for a decade. What Gift of Battle
actually demonstrates is the gap AFTER the exclusion is applied - with no vendor
row and no recipe, the item resolves to `UnknownSource` carrying a `WVW` text
badge, which is the whole of what the module can say about a component every
legendary weapon of every generation requires.

### P2. Vendor-side required-item gate (SMALL, already scoped)

Row 3. A field on `VendorOffer`, a scrape in `tools/VendorOfferUpdater`, and
a check beside the discipline gate. The repo already nominated this as the
one worth doing, and the Obsidian case is a live example.

### P3. Time-to-complete as a first-class output (MEDIUM, high user value)

Row 2. The module already tracks daily and weekly caps and already computes
a plan. Turning that into "this plan has a floor of N weeks, set by X" is a
genuinely useful answer that no amount of gold changes, and it is the kind of
thing a player actually plans around. Seasonal caps would need adding.

### P4. Mode labelling from data already held (SMALL, cheap signal)

Row 5. 1,407 offers already say "World vs. World" and the field is read by
nothing. Parsing what is already there into a mode tag, showing it on a
route, and letting a user exclude a mode they do not play is a small change
against data that costs nothing to obtain.

Worth knowing before starting: **Ascended Shards of Glory is valued in the
defaults table but charged by zero offers in the corpus**, so the PvP
legendary vendor path is absent from the data, not merely unmodelled. And
PvP League Tickets are charged by 56 offers with no valuation, so all 56 are
permanently unrankable. Mode labelling would make both visible instead of
silently wrong.

### P5. Multi-output recipes (MEDIUM, correctness)

Row 6. Rare but genuinely wrong today, and the Aetheric Anchor is a shipped
example. Worth scoping only after P1-P4.

### Not recommended yet

- **Achievement gating (row 4)** - needs a new API surface and account
  scope for a gate that changes availability, not cost. Real, but P1 covers
  more ground for less.
- **Mastery and station locality (row 9)** - informational only. Cost is
  already right.

---

## 5. Two fixes that need no decision

Both are uncontroversial and I intend to do them unless told otherwise.

1. **Empty cost-line guard.** MEASURED: 1,896 shipped offers (3.2%) have an
   empty `costLines` array. `VendorBatchSolver.cs:264` folds over
   `CostLines ?? Empty`, so such an offer exits at `coinCost == 0`,
   `allValued == true`, lands in the comparable tier at value 0, and beats
   every priced route. The sibling helper `CostLineValuation.cs:34-39` guards
   exactly this case; the solver does not. Latent today only because none of
   the 721 affected outputs appears in the recipe corpus - which a future
   re-scrape could change without anyone noticing.
2. **The flaky `Gw2BuildApiClientTests` wall-clock race.** It asserts against
   `Task.Delay(10s)`; it runs in 470ms locally and took 16s on a loaded CI
   runner, failing PR #232 spuriously. It will do it again.

---

## 6. The question this analysis cannot answer

**Is the module's goal "the cheapest way" or "how do I get there"?**

Every proposal above P2 depends on the answer. If the module is a price
optimiser, then non-transactional routes are correctly out of scope and the
right response to a legendary is to cost what can be costed and clearly
refuse the rest. If it is a project planner, then P1 and P3 are the product
and the cost engine is a component of it.

The taxonomy research makes the case that for legendaries specifically, gold
is often not the binding constraint - weeks are, and participation in a game
mode is. But that is a product decision, not a technical one.

---

## 7. Confidence

- Everything drawn from the capability audit is MEASURED against the tree at
  commit `d060c02` and cited to file and line in that document.
- The taxonomy research is reliable on **structure** (what exists, what
  gates what, which pathway) and explicitly **unreliable on numbers**: its
  own method section records the wiki fetch path contradicting itself three
  ways on WvW ticket totals. No quantity from it is repeated here as fact.
- The taxonomy's twelve stated gaps are unresolved. The two that would most
  change the picture: the Slumbering-item bill of materials is unknown, and
  "Gift of Battle is universal" rests on a single source.
