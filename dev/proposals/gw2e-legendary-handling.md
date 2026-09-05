# What gw2efficiency does with legendaries

Status: RESEARCH. Nothing here is a decision and nothing here is implemented.
Written 2026-08-29 to answer one question before we design our own answer:

> What does the gw2efficiency crafting calculator actually do when you ask it
> for legendary armour, or any legendary whose tree contains things that
> cannot be bought?

Companion to `dev/proposals/legendary-gap-analysis.md`, which states the
problem from our side. That document ends on an unanswered product question
("is the module a price optimiser or a project planner?"). This document does
not answer it either, but it shows what the mature incumbent chose, and what
that choice cost them.

---

## 0. Method, and what could NOT be observed

gw2efficiency.com is a single-page Angular application. Fetching any
calculator URL returns a 39 KB shell with no calculator content in it, so the
rendered page could not be read directly and no screenshot was taken (the
repo rule in `CLAUDE.md` requires asking before using browser automation, and
this was research-only). Everything below is read from primary artefacts that
the site itself serves:

- `https://gw2efficiency.com/scripts/application.js` - the deployed 4.2 MB
  application bundle, which contains the compiled `recipe-calculation`
  library, the calculator controller, and the static tables.
- `https://gw2efficiency.com/views/Crafting/calculator.html` - the deployed
  calculator page template.
- `https://gw2efficiency.com/views/_directives/componentTree.html` - the
  deployed recipe-tree row renderer. Every UI string quoted below comes from
  this file or the one above, verbatim.
- `https://gw2efficiency.com/views/Crafting/legendaries.html` - the deployed
  "Precursors & Legendaries" page template.
- `https://edge.gw2efficiency.com/recipes?ids=all` - their **entire custom
  recipe corpus**, 21,082 nested recipe trees, 1.44 MB, served without
  authentication. This is the output of the private `custom-recipes` repo
  that the public library's `vendorItems.ts` comment points at. The repo is
  404; the data is public.
- `https://github.com/gw2efficiency/recipe-calculation` (MIT) and the org
  repository listing.
- `https://api.guildwars2.com/v2/items` and `/v2/currencies` for names.

**Not observed, and therefore not claimed:**

- The rendered page. All UI claims are read out of the deployed template
  source, not seen on screen. Conditional rows are quoted with their `ng-show`
  condition so the reader can check the inference.
- Anything behind an API key. There is no account and no key here. That
  covers: the "Used owned materials" section, the Unlocked/Missing column on
  Required Recipes, the achievement-derived ownership path, the homestead
  efficiency-tier warning, `/account/legendary-armory`, and
  `/account/achievements`. Their code paths were read; their behaviour was
  not exercised.
- Actual gold figures. No tree was priced. Where this document says a
  component contributes zero, that is read from the arithmetic in the
  deployed `calculateTreePrices`, not measured against a displayed total.
- Their private `custom-recipes` repository, and therefore how the corpus is
  authored, maintained, or validated. Only its published output was read.

---

## 1. What they do

### 1.1 They plan the whole tree. They do not refuse.

Twilight, Perfected Envoy Vestments and Obsidian Heavy Breastplate all have
full nested trees in the corpus and all resolve to a root recipe. There is no
"this item cannot be planned" state anywhere in the calculator. The only
empty state is `Please enter a craftable item to calculate recipe`, shown
when no item is selected.

Constructed links, using their own URL encoding (`a` material price,
`b` use own materials, `c` daily cooldowns, `d` items, `e` value own
materials, `f` allow Mystic Forge promotions):

- Twilight: <https://gw2efficiency.com/crafting/calculator/a~0!b~1!c~0!d~1-30704>
- Obsidian Heavy Breastplate: <https://gw2efficiency.com/crafting/calculator/a~0!b~1!c~0!d~1-101521>
- Perfected Envoy Vestments: <https://gw2efficiency.com/crafting/calculator/a~0!b~1!c~0!d~1-80190>
- Gift of Battle: <https://gw2efficiency.com/crafting/calculator/a~0!b~1!c~0!d~1-19678>

The Twilight tree, as served (abridged, quantities and disciplines as in the
data):

```
1x Twilight [Mystic Forge]
  1x Dusk [Weaponsmith] prereq Recipe 11180
    1x Spirit of the Perfected Nightsword [Salvage]
    1x Essence of Gloom [Double Click]
      1x Chest of Gloom [Achievement] achievement_id 2183
    ...
  1x Gift of Twilight [Mystic Forge]
    100x Icy Runestone [Merchant] Rojan the Penitent, Frostgorge Sound
      10000x Coin <Currency 1>
  1x Gift of Mastery [Mystic Forge]
    1x Gift of Battle <Item 19678>          <- bare leaf, no recipe
    1x Gift of Exploration <Item 19677>     <- bare leaf, no recipe
    250x Obsidian Shard [Merchant] Tactician Deathstrider
      2100x Karma <Currency 2>
    1x Bloodstone Shard [Merchant] Miyani / Mystic Forge Attendant
      200x Spirit Shard <Currency 23>
  1x Gift of Fortune [Mystic Forge]
    77x Mystic Clover [Mystic Forge] output=0.31
      1x Obsidian Shard, 1x Mystic Coin, 1x Glob of Ectoplasm,
      6x Philosopher's Stone [Merchant] Miyani, output=10
```

Legendary armour is planned to the same depth. The Obsidian Heavy Breastplate
root is a plain `Armorsmith` recipe gated by a recipe sheet, and its first
component is an achievement:

```
1x Obsidian Heavy Breastplate [Armorsmith] prereq Recipe 14073
  1x Arcanum of Astral Heartbeat [Achievement] achievement_id 7096
    1x Astral Ward Heavy Coat [Merchant] Lyhr, The Wizard's Tower   bit 0
    1x Rift Hunter Heavy Coat <Item 100592>                          bit 1
    1x Oneiros-Spun Heavy Coat [Merchant] Lyhr                       bit 2
    1x Lesser Vision Crystal [...]
```

Worth pausing on that one node. `docs/KNOWN-ISSUES.md:758-759` records
100509 Arcanum of Astral Heartbeat as "the one cost item in this chain with
no recipe at all". gw2efficiency does have a recipe for it: an `Achievement`
recipe carrying `achievement_id: 7096`, whose three components are the three
collection items, each tagged with its own `achievement_bit` (0, 1, 2). The
route is not missing from the game, it is missing from the vocabulary we use
to describe the game.

### 1.2 Three distinct treatments for "you cannot buy this"

The tree renderer has exactly four mutually exclusive price cells per row.
Verbatim from `componentTree.html`:

| Condition (`ng-show` / `ng-hide`) | Renders |
|---|---|
| `buyPrice !== false`, not a Currency | radio `TP` + gold amount |
| has `components` | radio `Crafting`, relabelled **`Merchant`** when `disciplines.includes('Merchant')`, + gold amount |
| `type === 'Currency'` | a flat grey badge reading **`Currency`**, no radio, `cursor: default` |
| `buyPrice === false && !components && type !== 'Currency'` | a desaturated label reading **`Not sold or crafted`**, no price |

So an unbuyable input lands in one of three buckets:

1. **A wallet currency** becomes a `Currency` leaf. 42 distinct wallet
   currencies appear in the corpus, Legendary Insight (70) and Provisioner
   Token (29) among them. It is never bought and never crafted; it carries a
   notional per-unit valuation used only for choosing between branches.
2. **An item with a non-transactional route** becomes a recipe under a
   pseudo-discipline: `Merchant`, `Mystic Forge`, `Achievement`, `Salvage`,
   `Double Click`, `Charge`, `Growing`, `Homesteader`.
3. **Everything else** is a bare `Item` leaf and renders as
   **"Not sold or crafted"**. Gift of Battle and Gift of Exploration are in
   this bucket: neither has any entry in the 21,082-tree corpus. So is
   Rift Hunter Heavy Coat, the drop half of the Obsidian armour collection.

Bucket 3 is a real, deliberate, user-visible label, and it is old: a user
complained about it on
<https://github.com/gw2efficiency/issues/issues/1161> in 2019, when a data
regression pushed Gen 2 gifts into it that had previously been decomposed.
The label is the honest end of their vocabulary, not a bug.

### 1.3 Currency cost is a separate line, never folded into gold

The Cost Breakdown section has two money rows, not one:

- `Cost` - `cost_breakdown.total.cost`, which is `tree.craftPrice`.
- `Cost of Currencies` with the caption **`(estimated opportunity cost)`** -
  computed in the deployed bundle as `decisionPrice - craftPrice`.

The same split appears per node, behind a wallet icon on any row where
`craftPrice != craftDecisionPrice`:

> Crafting gold price: `<gold>`
> Currencies: `<gold>`
> *This is an estimated opportunity cost for the used currencies in the recipe.*
> Optimization Price: `<gold>`

That is the same two-number discipline this repo already implements
(`Models/CurrencyDecisionDefaults.cs`, decision-only valuations that must
never reach a displayed gold total), surfaced in the UI rather than hidden.
Their wording for the second number is worth stealing: "Optimization Price".

### 1.4 Warnings the calculator raises on a legendary

All five are in `calculator.html` and all five fire on real legendary trees.
Verbatim:

- **`This recipe includes the same achievements multiple times.`** ... **`You
  only have to complete these achievements once and do not need to repeat
  crafting their requirements.`** Raised as an `Alert--danger` when two nodes
  share an `achievement_id`. This is the "collections are once-per-account,
  the tree double-counts them" problem, handled by telling the user rather
  than by changing the arithmetic.
- **`The output of some crafted items is subject to randomness and not
  guaranteed.`** Triggered by a hardcoded three-item list in the controller:
  Mystic Clover (19675), Endless Gift Dolyak Tonic (38121), Mini Steam
  Minotaur (45010) - the three Mystic Forge random outputs.
- **`Some sub-components may be included in a higher amount than needed,
  because their recipes do not allow to craft for a single amount.`** with a
  per-item `Outputs N per craft` list. This is batch overshoot, made visible.
- **`Click to see N timegated items this recipe expects you to craft/buy.`**
  expanding to a per-item list with `Weekly limit: N / Daily limit: M`, and a
  trailing note `(Please note that for materials grown in the home instance,
  you can gather up to four per plot per day.)`
- **`These are the materials to craft N items instead of M, because the
  recipe doesn't allow for the amount you specified.`**

### 1.5 Merchant steps are first-class in the plan

In Crafting Steps, a step whose disciplines contain `Merchant` reads
**"Buy N x Item for ..."** instead of "Craft N x Item out of ...", and the
discipline cell is replaced by the merchant name with
`title="Locations: ..."`. The corpus carries `merchant: {name, locations}` on
3,823 distinct merchant-recipe outputs, across 119 merchant names and 124
location strings. Examples as served: `Tactician Deathstrider` at
`Cathedral of Glorious Victory, Straits of Devastation`;
`Miyani / Mystic Forge Attendant` at `Mystic Forge`; `Master Armorsmiths` at
`Crafting Station`; `Whispers Keeper (Dragon's Stand)` with two locations.

### 1.6 Recipe-sheet gates are modelled and account-checked

Every recipe node carries `prerequisites: [{type: "Recipe", id: N}]`. The
corpus holds 13,230 distinct such prerequisites, all of type `Recipe`. The
calculator collects them from crafted nodes only, resolves them through
`/recipe-sheets`, drops anything flagged `AutoLearned`, and renders a
**Required recipes** section listing name, disciplines, rating, and - with an
API key - `Unlocked` or `Missing`, with a "Hide Unlocked Recipes" checkbox.

This is the `docs/KNOWN-ISSUES.md:748-756` item ("the one worth doing"),
already shipped by them, on the recipe side rather than the vendor side.

### 1.7 Achievements are read from the account, as ownership

This is the part that was least expected. When an API key is present, the
owned-materials pass fetches `/v2/account/achievements` for every
`achievement_id` in the tree and then, for each completed bit:

- bits of type `Item` are added to the owned pool with the source label
  **`Achievements`**;
- bits of type `Skin` add 999 of every item that unlocks that skin, source
  `Skins`;
- bits of type `Text` get a synthetic id `3e11 + 1000 * achievement_id +
  bit + 1` so a non-item collection step can still be ticked off. The
  calculator filters synthetic ids in that range out of Crafting Steps unless
  they are owned.

Equipment sitting in the Legendary Armory is deliberately excluded from the
owned pool.

So a completed collection step is not re-planned. That is a genuine "what do
I still have to do" behaviour, living inside the cost calculator rather than
in a separate tracker.

---

## 2. How they do it

### 2.1 Everything is a recipe, and the discipline field carries the mechanism

The corpus has three node types by occurrence: `Recipe` 424,610, `Item`
323,380, `Currency` 122,888. Disciplines, by occurrence across all nodes:

| Discipline | Occurrences | What it means |
|---|---|---|
| Artificer / Huntsman / Weaponsmith / Scribe / Armorsmith / Tailor / Leatherworker / Jeweler / Chef | 130,725 down to 9,333 | real crafting |
| **Merchant** | 124,547 | vendor exchange, 3,823 distinct outputs |
| **Mystic Forge** | 65,221 | forge recipe, 1,849 distinct outputs |
| **Double Click** | 4,913 | open a container |
| **Charge** | 1,822 | charge/consume mechanic |
| **Achievement** | 993 | complete a collection, 285 distinct outputs |
| **Salvage** | 625 | salvage an item to get a component |
| **Growing** | 71 | home-instance / garden plot |
| **Homesteader** | 1 | homestead refinement |

`src/static/vendorItems.ts` really is dead in the shipped build:
`VENDOR_ITEMS = {}` and `useVendorPrices = function(e){return e}`, an identity
function, in the deployed bundle. The whole mechanism migrated into the
recipe corpus.

The Mystic Forge question is therefore answered: **yes, fully,
as ordinary recipes with `disciplines: ["Mystic Forge"]`**, and forge steps do
appear in the tree and in Crafting Steps like any other craft.

### 2.2 Node schema

```
id, type: Recipe|Item|Currency, quantity, output,
components[], prerequisites[{type,id}],
min_rating, disciplines[],
upgrade_id, output_range,
achievement_id, achievement_bit,
merchant: {name, locations[]} | null,
multipleRecipeCount,
daily_purchase_cap, weekly_purchase_cap
```

Observations on the fields, measured over the whole corpus:

- `output_range` exists in the schema and is used **zero** times.
- Fractional `output` is used for exactly **one** item: Mystic Clover, at
  `0.31`. Everything else is an integer. Their expected-value machinery is as
  under-exercised as ours.
- `daily_purchase_cap` / `weekly_purchase_cap` are set on exactly **six**
  distinct items (43 node occurrences), all recent Janthir/SotO content. The
  classic time-gated items are not covered by this field at all; they live in
  a 25-entry hardcoded `DAILY_COOLDOWNS` table in the library, each entry just
  `{id, tradable, craftInterval?}`.
- `multipleRecipeCount` records how many variant recipes produce the node
  (Obsidian Shard: 28) and drives a shuffle glyph with the tooltip
  `This recipe has N variants.` Only one variant is ever planned.

### 2.3 The two-price algorithm, as deployed

The live `calculateTreePrices` in the bundle, de-minified:

```js
let a = priceMap[t.id] || false;
if (t.type === 'Currency') a = (t.id === 1) && 1;      // only Coin has a coin price
const o = !!a && t.usedQuantity * a;                    // buyPrice
let l = a || undefined;
if (t.type === 'Currency')
  l = (overrides && typeof overrides[t.id] === 'number')
      ? overrides[t.id]
      : CURRENCY_DECISION_PRICES[t.id];                 // may be undefined
let c = !!l && t.usedQuantity * l;                      // decisionPrice
if (!t.components) return {...t, buyPriceEach:a, buyPrice:o,
                            decisionPrice:c, craftResultPrice:o, craftDecisionPrice:c};

const u = t.components.map(recurse);
const d = u.map(e => e.decisionPrice   || 0).reduce(sum, 0);   // craftDecisionPrice
const h = u.map(e => e.craftResultPrice|| 0).reduce(sum, 0);   // craftPrice
// craft wins if explicitly forced, or there is no buy decision price, or d < c
```

Three consequences, all confirmed against the deployed code rather than the
GitHub source:

1. **The `|| 0` flattening is live.** A component with no price of any kind
   contributes exactly zero to both its parent's craft price and its parent's
   decision price. Gift of Battle and Gift of Exploration are free, in the
   arithmetic, in every legendary weapon.
2. **A currency with no valuation is also free.** The deployed
   `CURRENCY_DECISION_PRICES` has `70: undefined` (Legendary Insight) and
   `30: undefined` (PvP League Ticket). Perfected Envoy Vestments needs 50
   Legendary Insights across two nodes; they contribute nothing to any number
   the page shows. Provisioner Token (29) and Spirit Shard (23) *are* valued,
   at 3600c each; WvW Skirmish Claim Ticket (26) at 800c; Ascended Shards of
   Glory (33) at 1600c. This is the same partially-populated table this repo
   copied, and the same holes.
3. **Craft is chosen whenever there is no buy price.** For any legendary
   (account-bound, no TP listing), `c` is false at the root, so the tree
   always resolves to craft. That is why the calculator never refuses.

The net effect for a legendary: the reported cost is *the cost of the
buyable subset*, presented with the same styling and the same confidence as a
complete answer. The unbuyable parts are visible as labels in the tree, and
absent from the number.

### 2.4 Where the mode tag lives

`/crafting/legendaries` is driven by a 201-entry hardcoded table in the
bundle: `{id, name, type, skin, precursorId, precursorName, precursorSkin,
generation, source}`. `source` is `PvE` (106), `PvP` (56) or `WvW` (39), and
it is a **filter on that page only**. The calculator has no concept of game
mode; nothing in the recipe corpus carries one.

---

## 3. What they do NOT do

Stated as flatly as the rest, because this is the half that tells us where
the frontier actually is.

1. **No modelling of non-transactional acquisition.** There is no reward
   track, no world completion, no raid encounter, no PvP league, no drop
   source, anywhere in the corpus or the vocabulary. Gift of Battle is a bare
   item id and nothing more. The player is told "Not sold or crafted" and
   left to find out what that means elsewhere. **This is the same boundary
   our gap analysis identifies as row 1, and the incumbent has not crossed
   it.**
2. **No time-to-complete.** They detect time gates and *list* them
   ("N timegated items this recipe expects you to craft/buy", with per-item
   daily/weekly limits) but never convert them into a duration. No "this plan
   has a floor of N weeks" exists. Nor is the cap ever used to re-route.
3. **No cost of the unbuyable, and no marker on the total.** The Cost
   Breakdown does not say "plus 1 Gift of Battle and 1 Gift of Exploration,
   unpriced". Nothing on the total warns that it is partial. The tree tells
   you; the number does not.
4. **No achievement gating.** Achievements are read as *ownership* (what you
   have finished) and as a *duplication warning*. They are never read as
   availability: the calculator will happily plan a vendor exchange behind an
   achievement the account has not unlocked.
5. **No multi-output recipes.** One recipe, one output. The Aetheric Anchor
   case from our gap analysis has no representation here either.
6. **No second-copy pricing.** Nothing models "you have already made one".
   The Perfected Envoy 150-then-300 Legendary Insight structure is not
   expressible, and would not change any number if it were, since Legendary
   Insight is unvalued.
7. **No mode preference or exclusion.** The mode tag exists on one static
   list and is a display filter.
8. **No mastery or station-locality gating.** Locations are a tooltip string.
9. **Only one variant is ever planned.** `multipleRecipeCount` counts the
   alternatives and the UI says how many there are, but the solver never
   compares them. Obsidian Shard has 28 variants and is always planned as
   2100 Karma.
10. **The vendor corpus is small.** 3,823 distinct merchant outputs over 119
    merchants. This repo ships 59,414 offers. Their advantage is the
    *modelling*, not the *coverage*.

---

## 4. What to adopt, what not to, and why

### Adopt

**A. A terminal "cannot be bought or crafted" state that the tree shows and
the total accounts for.**

Their `Not sold or crafted` row is the single most transferable idea here,
and it costs almost nothing: it is a fourth cell in an existing row, driven
by a condition we can already evaluate (no TP price, no recipe, no vendor
offer). Today such an item resolves to `UnknownSource` and falls through to a
ten-row hint table. A typed terminal state is strictly better than that, and
it is the low bar that `dev/proposals/legendary-gap-analysis.md` P1 sets.

But adopt it **with the fix they did not make**: their `|| 0` silently prices
the unobtainable at zero and prints a confident total. We should carry the
unpriced items as a *count* alongside the total, so the plan can say "18g
plus 2 items you must obtain yourself" rather than "18g". The repo already
has the vocabulary for this: the two-tier comparability split in the vendor
solver exists precisely so an unvalued thing cannot masquerade as a cheap
one. Extending that tier from currencies to items is the same idea one level
up. Note that the empty-`costLines` guard already filed as a fix in the gap
analysis is the same class of bug as their `|| 0`, arriving from the other
direction.

**B. The mechanism-as-pseudo-discipline idea, for labelling.**

`Merchant`, `Mystic Forge`, `Achievement`, `Salvage`, `Double Click`,
`Growing` is a compact and surprisingly complete vocabulary for "how does
this node happen", and it costs one string per node. It lets one renderer
say "Buy" instead of "Craft", show a merchant name and location, and mark a
forge step, with no new node types and no branching in the solver. Our
`AcquisitionSource` enum is the same shape with five members; the lesson is
that the useful list is nearer nine, and that the extra members are cheap
because the solver does not have to understand them - only the renderer does.

The specific members worth taking are the ones we do not have: **Mystic
Forge** (we hold 1,591 wiki-sourced forge recipes and label none of them as
forge steps), **Salvage**, **Container**, and **Achievement**.

Two smaller things worth taking with them, both nearly free:

- **"Optimization Price"** as the display name for the decision-price total,
  with the coin/currency split shown as two lines and the caption
  "(estimated opportunity cost)". We already compute both numbers and
  currently show one. Their wording is clearer than anything in the module
  today and it makes the never-fold-currencies-into-gold invariant visible to
  the user instead of only to the code.
- **Warn, do not re-arithmetic, on repeated once-only nodes.** Their
  duplicate-achievement alert is a red banner and a list of names, and it
  leaves the tree alone. That is the cheap correct move for a case where the
  right quantity is genuinely ambiguous.

### Do not adopt

**C. Do not adopt their currency-valuation table as a source of truth for
unpriced currencies.** We already ship it, correctly attributed and
decision-only. The finding to add is that its holes are load-bearing:
Legendary Insight (70) and PvP League Ticket (30) are `undefined` upstream,
and in their calculator that silently means "free". Our gap analysis already
recorded 56 offers charging PvP League Tickets with no valuation and
therefore permanently unrankable. Copying the table was right; copying its
*failure mode* is not. An unvalued currency must make a route
incomparable, never cheap.

**D. Do not adopt "always craft when there is no buy price".** Their rule
`craft wins if there is no buy price` is what makes the calculator answer
every legendary confidently. It is also what makes the answer partial without
saying so. For an account-bound item there is no buy price by definition, so
the rule never fires as a check - it fires as an unconditional yes. If we
want the module to be able to say "this is not fully plannable", we cannot
inherit a rule whose only outcome is "plannable".

**E. Do not adopt time gates as a tooltip only.** They detect exactly what is
needed for a duration - per-item daily and weekly caps, quantities required -
and stop at listing it. Given the same inputs, computing "at least N weeks,
set by X" is arithmetic we already have the pieces for
(`TimegatedCapType`, the plan quantities), and it is the answer a legendary
actually turns on. This is the clearest place where a mature incumbent has
left value on the table, and it is P3 in the gap analysis.

**F. Do not adopt a separate legendary page that is only a price table.**
`/crafting/legendaries` filters by type, subtype, generation, weight class
and mode, and then shows four columns of gold. It is a shopping index, not a
progress view. Their genuinely progress-shaped behaviour - achievement bits
counted as owned, recipe sheets marked Missing - lives *inside* the
calculator, which is the right place for it. If we build a legendary view, it
should be the plan, not a second table beside the plan.

### Do legendary prerequisites live in a different tool?

**Legendary prerequisites do not live in a different tool.** There is no
legendary-progress product at gw2efficiency. `/crafting/legendaries` is a
price index. `/account/legendary-armory` is an ownership list of what you
already have. `/account/achievements` is a general achievement browser. The
only place a legendary's prerequisites are assembled is the crafting
calculator, and the only account-aware progress it applies is (a) owned
materials, currencies and inventory, (b) completed achievement bits counted
as owned items, and (c) recipe sheets marked Unlocked or Missing. Points
(b) and (c) are code-read only; neither was exercised, because both need an
API key.

---

## 5. Confidence

- Everything in sections 1, 2 and 3 is **measured** against artefacts the
  live site served on 2026-08-29, listed in section 0. Counts come from
  parsing the 21,082-tree corpus and the deployed bundle directly.
- The mapping from a data condition to a rendered row is **inferred** from
  the deployed `ng-show` / `ng-hide` conditions, not seen rendered. The
  conditions are quoted so the inference is checkable.
- Everything requiring an account is **code-read, not observed**, and is
  labelled as such wherever it appears.
- No prices were fetched and no tree was costed; no gold figure appears in
  this document.
