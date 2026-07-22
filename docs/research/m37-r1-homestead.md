# M37 R1: Homestead Refinement Handling (gw2e parity gap #24) - Research Report

Scope: KNOWN-ISSUES.md item 24. THE METHOD followed: upstream gw2e mechanism researched
first (dev-time only, from public repos + the live app bundle, fetched read-only), then
wiki ground truth, then current module state (read, not assumed), then an echo design.
No data in this report is invented; every load-bearing number is tagged MEASURED,
OBSERVED, or INFERRED, and anything I could not pin down is tagged UNVERIFIED explicitly.

---

## 1. Upstream gw2e mechanism

### 1.1 Where the conversion data structurally lives

Homestead Refinement conversions are **ordinary Merchant recipes**, not a separate table -
this matches (and is the exact case cited by) `docs/gw2e-parity-spec.md` Section 3.2, which
I re-confirmed rather than re-derived. `recipe-nesting`'s recipe shape carries an optional
`merchant?: { name: string; locations: Array<string> }` field
(**MEASURED**, `recipe-nesting/src/api.d.ts`, fetched `master`,
`https://raw.githubusercontent.com/gw2efficiency/recipe-nesting/master/src/api.d.ts`):

```ts
export interface API_Recipes_Entry {
  type: string
  output_item_id: number
  ...
  merchant?: { name: string; locations: Array<string> }
  ...
}
export interface API_Recipes_Entry_Next extends API_Recipes_Entry {
  multipleRecipeCount: number
  daily_purchase_cap?: number
  weekly_purchase_cap?: number
}
```

`nestRecipes` (`recipe-nesting/src/index.ts`, same fetch) passes `merchant`,
`daily_purchase_cap`, and `weekly_purchase_cap` straight through into every `NestedRecipe`
node (**MEASURED**, `transformRecipe()`):

```ts
return {
  id: recipe.output_item_id,
  type: 'Recipe',
  ...
  merchant: recipe.merchant,
  ...
  daily_purchase_cap: recipe.daily_purchase_cap ? recipe.daily_purchase_cap : 0,
  weekly_purchase_cap: recipe.weekly_purchase_cap ? recipe.weekly_purchase_cap : 0,
}
```

I could **not** locate where gw2efficiency's own backend actually populates
`daily_purchase_cap`/`weekly_purchase_cap` for a live Homestead Refinement recipe entry -
that data pipeline is not in either public repo, and (per the parity spec's own prior
finding, which I did not need to re-verify) the `custom-recipes` repo that historically held
vendor/merchant recipe data is deleted from GitHub. **UNVERIFIED**: the actual numeric
`weekly_purchase_cap` value gw2efficiency's live data assigns to a Homestead Refinement
recipe (whether it's the wiki's base 200, the max-upgraded 800, or something else/unset).

### 1.2 The efficiency-tier mechanism itself (the actual "gap" content)

`cheapestTree.ts` (**MEASURED** - fetched directly via
`curl https://raw.githubusercontent.com/gw2efficiency/recipe-calculation/master/src/cheapestTree.ts`
and cross-checked twice against an independent WebFetch summarization; both matched
byte-for-byte on the function bodies quoted below) takes a `userEfficiencyTiers` parameter
with this exact default:

```ts
export function cheapestTree(
  amount: number,
  tree: NestedRecipe,
  itemPrices: Record<string, number>,
  availableItems: Record<string, number> = {},
  forceBuyItems: Array<number> = [],
  valueOwnItems = false,
  userEfficiencyTiers: Record<string, string> = {
    '102306': '0',
    '102205': '0',
    '103049': '0',
  },
  customCurrencyPrices: Record<string, number> = {}
): RecipeTreeWithCraftFlags {
  ...
  tree = initialTreeChecks(tree, userEfficiencyTiers, ignoredBitItemIds)
  ...
```

`initialTreeChecks` walks the whole tree and calls `applyEfficiencyTiersToTree` on every
node **before** any pricing pass runs. The full function (**MEASURED**, same fetch):

```ts
function applyEfficiencyTiersToTree(
  tree: NestedRecipe,
  userEfficiencyTiers: Record<string, string>
): NestedRecipe {
  if (!tree.id) return tree
  const id = tree.id ? tree.id.toString() : ''

  if (
    !['102306', '102205', '103049'].includes(id) ||
    !tree.merchant ||
    !tree.merchant.name.includes('Homestead Refinement')
  ) {
    return tree
  }

  const efficiencyTier = Number(userEfficiencyTiers[id])
  if (!(efficiencyTier > 0)) return tree

  const component = { ...tree.components[0] }

  // Each efficiency tier lowers input by 50%, if it drops below one then doubles output
  component.quantity = component.quantity / (efficiencyTier * 2)

  // Bug: Onions are discounted by 75% with first tier
  if (component.id === 12142) {
    component.quantity = efficiencyTier === 1 ? 1 : 0.5
  }

  // Bug: Potatoes are not discounted with first tier
  if (component.id === 12135) {
    component.quantity = efficiencyTier === 1 ? 8 : 4
  }

  let updatedTree = {
    ...tree,
    output: component.quantity < 1 ? tree.output * 2 : tree.output,
  }

  // Bug: Iron ore output also halves with second tier
  if (component.id === 19699 && efficiencyTier === 2) {
    updatedTree.output = updatedTree.output / 2
  }

  component.quantity = component.quantity < 1 ? 1 : component.quantity
  updatedTree = { ...updatedTree, components: [component, ...tree.components.slice(1)] }
  tree = updatedTree
  return tree
}
```

Identifiers, MEASURED via the GW2 API (`api.guildwars2.com/v2/items/{id}`):
`102306` = Refined Homestead Fiber, `102205` = Refined Homestead Metal, `103049` = Refined
Homestead Wood (all `CraftingMaterial`/`Exotic`). `12142` = Onion, `12135` = Potato,
`19699` = Iron Ore.

**Exact mechanism**: only these 3 output item ids are ever touched, and only when the node's
`merchant.name` contains the literal substring `"Homestead Refinement"` (matches all three
of `"Homestead Refinement—Farm"`, `"—Lumber Mill"`, `"—Metal Forge"` since JS `.includes()`
is a substring test, not exact match). The tier setting is a **per-output-material** string
`"0"`/`"1"`/`"2"` (never higher - see Section 2, only two efficiency upgrades exist per
material family), defaulting to `"0"` (base/no upgrade) for all three. Tier `> 0` halves
**only `tree.components[0]`'s quantity** (the tree's *actual chosen* input recipe/ingredient
for that particular occurrence, not a generic "the cheapest ore" - whichever specific input
material `cheapestTree`'s own pricing pass already picked upstream of this call ends up
tier-adjusted) per tier, and once the halved quantity drops below 1 it clamps to 1 and
**doubles `tree.output` instead** - i.e. the algorithm re-derives "half the input" as "double
the output" once input can't go below a whole unit, which is exactly the real game's
behavior for Platinum Ore/Orichalcum Ore/etc at tier 2 (Section 2). Three item-specific
"bug" overrides are hardcoded verbatim in the algorithm (Onion, Potato, Iron Ore) - see
Section 2.2 for wiki confirmation these are documented **game** anomalies, not upstream
authoring mistakes, which is presumably why gw2efficiency's own comments call them "Bug"
without reference to their own code.

### 1.3 Where/how the user actually sets the tier (the "toggle" the task asked about)

**Not on the Crafting Calculator's own settings panel.** I fetched the live bundle
(`https://gw2efficiency.com/scripts/application.js?cb=1783715316`, 4.22MB, **MEASURED**
via direct `curl`) and traced the wiring:

```js
// Crafting Calculator controller, on load:
E.getAccountName(E.getCurrentKey()).then(t => {
  e.userEfficiencyTiers = a.get("efficiencyTiers", {})[t] || {102306:"0",102205:"0",103049:"0"}
})
// ... calculate() is called with e.userEfficiencyTiers as one positional arg,
// and it's in the calculator's own $watchGroup so any external change re-solves live:
e.$watchGroup(["price","useOwnItems","dailyCooldowns","valueOwnItems",
               "userEfficiencyTiers","allowMysticForgePromotions"], () => _())
```

`a` is gw2efficiency's persistence wrapper (browser `localStorage`), `t` is the currently
logged-in GW2 account name (resolved from the user's stored API key via
`getAccountName(getCurrentKey())`). **The tier value is persisted keyed by account name**,
not globally - i.e. gw2efficiency's own architecture assumes different GW2 accounts may
have upgraded to different tiers, and switching API keys switches the effective tier set.
**With no API key configured, `t` is undefined and the lookup misses, so the calculator
always falls back to the hardcoded `{102306:"0",102205:"0",103049:"0"}` default** - the
same base/no-upgrade default as the package's own signature default.

The actual **Efficiency Tier** control lives on a completely separate page - a dedicated
account tool, not the Crafting Calculator:

```js
e.controller("Account_HomesteadRefinementsController", n(731))
r("/account/homestead/refinements", "Account_HomesteadRefinementsController", "/views/toReact.html")
```

That page (route `/account/homestead/refinements`, a React-rendered "which raw material is
most efficient to refine, sorted by profit" comparison tool - it has its own `refinementType`
Select (Fiber/Metal/Wood, same 3 ids) and its own `efficiencyTier` Select with exactly the
labelled options **"0"/"1"/"2"** (default `"0"`), i18n-labelled `"Efficiency Tier"`:

```js
e.createElement(s.Select,{value:y,onChange:e=>{
  g[n.refinementType]=e,
  u.set("efficiencyTiers", {...m, [p]: g}),   // p = account name
  d({efficiencyTier:e})
},options:[{value:"0",label:c.i18n._("0")},{value:"1", ...}, {value:"2", ...}]})
```

Setting it here writes the SAME per-account `localStorage["efficiencyTiers"]` map the
Calculator reads. **There is no Homestead-specific control on the Calculator page itself** -
a user who has never visited the Homestead Refinements account page gets the calculator's
hardcoded `"0"` default for every material, forever. (**MEASURED**, both snippets quoted
verbatim from the fetched bundle.)

One more UI signal, also MEASURED from the bundle: after solving, the calculator computes
`e.hasEfficiencyTierNonMaxCrafts = O(o.tree, e.userEfficiencyTiers)` - a boolean (helper `O`
not traced function-body-deep, module-numbering makes exact extraction slow, but its
call-site signature - tree + tiers in, single boolean out, feeding a scope var literally
named "has an efficiency tier that is Non-Max and gets Crafted" - makes the intent
unambiguous) surfaced to the template, presumably to hint "you could get more by upgrading."
**INFERRED** (from naming + call shape) rather than **MEASURED** (I did not trace the exact
comparison logic inside `O`) - flagged as such.

### 1.4 Daily/weekly purchase-cap handling (re-verified directly, not just inherited from the deleted M34 report)

`docs/KNOWN-ISSUES.md` Section 20.2 asserts, citing a since-deleted M34 report, that gw2e's
purchase caps are informational-only. I independently re-verified this from source rather
than trusting the citation. `recipe-calculation/src/helpers/dailyCooldowns.ts`
(**MEASURED**, `curl https://raw.githubusercontent.com/gw2efficiency/recipe-calculation/master/src/helpers/dailyCooldowns.ts`), in full:

```ts
import { DAILY_COOLDOWNS } from '../static/dailyCooldowns'
import { RecipeTreeWithCraftFlags } from '../types'

const dailyCooldownIds = DAILY_COOLDOWNS.filter((x) => x.craftInterval === 'daily').map((x) => x.id)
export type DailyCooldownsBreakdown = Record<string, number>

export function dailyCooldowns(
  tree: RecipeTreeWithCraftFlags,
  breakdown: DailyCooldownsBreakdown = {}
) {
  if (!tree.components || tree.craft === false || tree.type === 'Currency') {
    return breakdown
  }
  if (dailyCooldownIds.indexOf(tree.id) !== -1) {
    breakdown[tree.id] = (breakdown[tree.id] || 0) + tree.usedQuantity
  }
  const dailyCap = tree.daily_purchase_cap ? tree.daily_purchase_cap : 0
  const weeklyCap = tree.weekly_purchase_cap ? tree.weekly_purchase_cap : 0
  if (dailyCap + weeklyCap > 0) {
    breakdown[tree.id] = (breakdown[tree.id] || 0) + tree.usedQuantity
  }
  tree.components.map((component) => dailyCooldowns(component, breakdown))
  return breakdown
}
```

This function **only ever writes into a plain read-only summary map** - it is never called
from `cheapestTree`, `calculateTreeCraftFlags`, or anywhere else that could feed back into a
craft-flag or price decision (confirmed by exhaustively listing `recipe-calculation`'s
`src/` tree - `cheapestTree.ts`, `calculateTreeCraftFlags.ts`, `calculateTreePrices.ts`,
`calculateTreeQuantity.ts` have no import of it). **Any node whose `weekly_purchase_cap` (or
`daily_purchase_cap`) is set gets tallied into the breakdown purely for display, regardless
of craft/buy/vendor outcome.** This confirms M34's "warn-only, never gates the solve" claim
is correct (now MEASURED by me directly, not merely inherited) - and additionally shows the
exact mechanism is generic (not Homestead-specific): **any** merchant recipe with a
purchase-cap field participates identically. `daily_purchase_cap`/`weekly_purchase_cap`
strings are present in the live bundle (confirming the code path is shipped), but - per
Section 1.1 - I could not verify the live numeric cap value(s) actually attached to Homestead
recipes specifically. **UNVERIFIED**: whether gw2efficiency's live Homestead data has caps
populated at all, and if so at which of the wiki's 200/800 values (Section 2).

### 1.5 No ownership/expansion gating anywhere in the algorithm

I found **no code anywhere** in `cheapestTree`/`nestRecipes`/`calculateTreeCraftFlags` that
checks whether the calculating account owns Janthir Wilds (or any other expansion) before
including a Merchant recipe as a candidate. Every Merchant recipe, Homestead or otherwise,
is always eligible; the buy-vs-craft-vs-vendor price comparison (parity spec Section 1)
decides purely on price. **INFERRED** (absence of any such check across every fetched
source file) rather than a positive "gw2e explicitly declined to gate" statement I can
quote - but the inference is strong given how small and fully-fetched these packages are.

### 1.6 Summary table (Section 7-style normative directives, homestead-specific)

| Behavior | gw2e's exact rule | Source |
|---|---|---|
| Which items are tier-adjustable | Exactly item ids 102306/102205/103049, and only when `merchant.name` contains `"Homestead Refinement"` | `cheapestTree.ts` |
| Tier value range/default | Per-material string `"0"`/`"1"`/`"2"`, default `"0"` for all three | `cheapestTree.ts` signature + live bundle account-scoped default |
| Tier formula | `qty = baseQty / (tier * 2)`, clamp to 1, double output if it would go below 1 | `cheapestTree.ts` |
| Hardcoded exceptions | Onion (12142), Potato (12135), Iron Ore (19699 @ tier2) | `cheapestTree.ts`, wiki-confirmed real bugs (Section 2) |
| Where the user sets it | A separate "Homestead Refinements" account tool, NOT the Calculator's settings panel; persisted per-GW2-account in localStorage; Calculator reads it live via `$watchGroup` | live bundle |
| No-API-key fallback | Base tier `"0"` for all three | live bundle |
| Ownership/unlock gating | None found anywhere in the algorithm | absence across all fetched source |
| Daily/weekly purchase caps | Informational-only summary map (`dailyCooldowns()`), never gates craft/buy/vendor decisions; generic mechanism, not Homestead-specific | `helpers/dailyCooldowns.ts`, re-verified directly |
| Live numeric cap value used for Homestead | UNVERIFIED - data pipeline not public | - |

---

## 2. Ground-truth data (wiki-verified, complete)

All of this section is **MEASURED** directly from `wiki.guildwars2.com` raw wikitext
(`action=raw` fetches, not summarized HTML) unless flagged otherwise; item ids cross-checked
against `api.guildwars2.com/v2/items`.

### 2.1 Unlock requirements

- The Homestead itself is an **account-wide** personal-housing feature
  (wiki: "The homestead is an account-wide personal housing system introduced with Janthir
  Wilds"), unlocked via the Homesteading Mastery track, specifically the **"Home Sweet Home"**
  story chapter of the Janthir Wilds personal story, which unlocks the Hearth's Glow plot.
  A second plot, Comosus Isle, additionally requires owning **both** Janthir Wilds and
  Visions of Eternity.
- All three refinement-station objects (`Homestead Refinement—Farm`, `—Lumber Mill`,
  `—Metal Forge`) have an infobox field `requires = jw` (Janthir Wilds ownership) and no
  other unlock requirement is stated on their pages - i.e. once a player has any homestead
  plot at all, all three stations are present (**INFERRED** from absence of any additional
  "must be built/placed via Scribe" note on the three station pages themselves - the
  Homestead overview page's "Handiwork Workbench" is described as the player-placed
  crafting station for *decorations*, distinct from these three refiner NPCs, which read as
  fixed homestead furniture. **UNVERIFIED**: I did not find an explicit "these are always
  present, cannot be removed" statement, only the absence of a construction requirement).
- No mastery-**tier** gating was found for the refinement stations or their efficiency/
  capacity upgrades - the upgrades (Section 2.3) are one-time **item purchases** (bought
  with coin + crafting materials, `total=1` per account) from the same refiner NPC, not
  mastery-point unlocks.

### 2.2 Conversion tables (complete, all three stations, all tiers)

**Weekly trade cap** (identical structure on all three station pages, MEASURED, raw
wikitext): base 200 trades/account/week; +100 per Masterwork/Rare capacity upgrade; +150 per
Exotic capacity upgrade; max 800 trades/week fully upgraded. **The cap counts *trades*
(vendor interactions), not *material units*** - explicitly stated on all three pages: "The
weekly maximum amount of Refined Homestead X available per week is based on the number of
trades performed, not the overall quantity." This cap is **shared across every input-material
choice at that one station** (it is not a per-input-material cap).

#### Metal Forge -> Refined Homestead Metal (102205)

| Input (item id) | Tier 0 (base) | Tier 1 (one efficiency upgrade) | Tier 2 (two efficiency upgrades) |
|---|---|---|---|
| Copper Ore (19697) | 8 -> 1 | 4 -> 1 | 2 -> 1 |
| Iron Ore (19699) | 4 -> 2 | 2 -> 2 | 1 -> 1 (**game bug**, see below) |
| Silver Ore (19703) | 20 -> 1 | 10 -> 1 | 5 -> 1 |
| Gold Ore (19698) | 8 -> 1 | 4 -> 1 | 2 -> 1 |
| Platinum Ore (19702) | 2 -> 1 | 1 -> 1 | 1 -> 2 |
| Mithril Ore (19700) | 4 -> 1 | 2 -> 1 | 1 -> 1 |
| Orichalcum Ore (19701) | 2 -> 1 | 1 -> 1 | 1 -> 2 |

Wiki's own bug note (verbatim): *"Iron ore to refined metal trade goes from 2:2 to 1:1 with
the second Homestead Upgrade: Ore Trade Efficiency, which is the same ratio but a lower
total quantity."* - this is the exact real-game anomaly `cheapestTree.ts`'s
`if (component.id === 19699 && efficiencyTier === 2) updatedTree.output /= 2` compensates
for (Section 1.2); the two sources agree exactly.

Max weekly output (wiki-stated): 1300 Refined Homestead Metal/week fully upgraded (both
efficiency tiers + capacity maxed).

#### Lumber Mill -> Refined Homestead Wood (103049)

| Input (item id) | Tier 0 | Tier 1 | Tier 2 |
|---|---|---|---|
| Green Wood Log (19723) | 20 -> 1 | 10 -> 1 | 5 -> 1 |
| Hard Wood Log (19724) | 4 -> 1 | 2 -> 1 | 1 -> 1 |
| Ancient Wood Log (19725) | 2 -> 1 | 1 -> 1 | 1 -> 2 |
| Soft Wood Log (19726) | 12 -> 1 | 6 -> 1 | 3 -> 1 |
| Elder Wood Log (19722) | 4 -> 1 | 2 -> 1 | 1 -> 1 |
| Seasoned Wood Log (19727) | 4 -> 1 | 2 -> 1 | 1 -> 1 |

Max weekly output: 1050 Refined Homestead Wood/week fully upgraded.

#### Farm -> Refined Homestead Fiber (102306)

**60 distinct crop inputs**, complete table (MEASURED, raw wikitext `{{Homestead refinement
row}}` templates + the base/tier1/tier2 vendor-table rows cross-checked against each other):

| Input | T0 | T1 | T2 || Input | T0 | T1 | T2 |
|---|---|---|---||---|---|---|---|
| Blueberry | 8->1 | 4->1 | 2->1 || Leek | 28->1 | 14->1 | 7->1 |
| Mushroom | 4->1 | 2->1 | 1->1 || Raspberry | 2->1 | 1->1 | 1->2 |
| Carrot | 4->1 | 2->1 | 1->1 || Clove | 4->1 | 2->1 | 1->1 |
| Black Peppercorn | 2->1 | 1->1 | 1->2 || Parsnip | 28->1 | 14->1 | 7->1 |
| Parsley Leaf | 4->1 | 2->1 | 1->1 || Saffron Thread | 2->1 | 1->1 | 1->2 |
| Thyme Leaf | 4->1 | 2->1 | 1->1 || Nopal | 24->1 | 12->1 | 6->1 |
| Chili Pepper | 8->1 | 4->1 | 2->1 || Prickly Pear | 24->1 | 12->1 | 6->1 |
| Head of Garlic | 4->1 | 2->1 | 1->1 || Pile of Flax Seeds | 2->1 | 1->1 | 1->2 |
| Vanilla Bean | 2->1 | 1->1 | 1->2 || Ghost Pepper | 4->1 | 2->1 | 1->1 |
| Head of Lettuce | 2->1 | 1->1 | 1->2 || Pile of Allspice Berries | 4->1 | 2->1 | 1->1 |
| Onion | 4->1 | **1->1 (bug)** | 1->2 || Handful of Red Lentils | 4->1 | 2->1 | 1->1 |
| Potato | 8->1 | **8->1 (bug)** | 4->1 || Lotus Root | 16->1 | 8->1 | 4->1 |
| Bay Leaf | 32->1 | 16->1 | 8->1 || Omnomberry | 4->1 | 2->1 | 1->1 |
| Oregano Leaf | 40->1 | 20->1 | 10->1 || Orrian Truffle | 2->1 | 1->1 | 1->2 |
| Sage Leaf | 2->1 | 1->1 | 1->2 || Sawgill Mushroom | 24->1 | 12->1 | 6->1 |
| Spinach Leaf | 4->1 | 2->1 | 1->1 || Seaweed | 2->1 | 1->1 | 1->2 |
| Strawberry | 4->1 | 2->1 | 1->1 || Snow Truffle | 8->1 | 4->1 | 2->1 |
| Beet | 60->1 | 30->1 | 15->1 || | | | |
| Turnip | 48->1 | 24->1 | 12->1 || | | | |
| Head of Cabbage | 40->1 | 20->1 | 10->1 || | | | |
| Grape | 32->1 | 16->1 | 8->1 || | | | |
| Kale Leaf | 4->1 | 2->1 | 1->1 || | | | |
| Yam | 32->1 | 16->1 | 8->1 || | | | |
| Portobello Mushroom | 13->1 | 6->1 (wiki flags this as an odd/rounded number) | 3->1 || | | | |
| Dill Sprig | 40->1 | 20->1 | 10->1 || | | | |
| Rosemary Sprig | 4->1 | 2->1 | 1->1 || | | | |
| Sesame Seed | 8->1 | 4->1 | 2->1 || | | | |
| Zucchini | 8->1 | 4->1 | 2->1 || | | | |
| Blackberry | 4->1 | 2->1 | 1->1 || | | | |
| Head of Cauliflower | 32->1 | 16->1 | 8->1 || | | | |
| Mint Leaf | 28->1 | 14->1 | 7->1 || | | | |
| Green Onion | 24->1 | 12->1 | 6->1 || | | | |
| Sugar Pumpkin | 32->1 | 16->1 | 8->1 || | | | |
| Rutabaga | 4->1 | 2->1 | 1->1 || | | | |
| Artichoke | 28->1 | 14->1 | 7->1 || | | | |
| Asparagus Spear | 4->1 | 2->1 | 1->1 || | | | |
| Passion Fruit | 20->1 | 10->1 | 5->1 || | | | |
| Butternut Squash | 28->1 | 14->1 | 7->1 || | | | |
| Cayenne Pepper | 4->1 | 2->1 | 1->1 || | | | |
| Lemongrass | 16->1 | 8->1 | 4->1 || | | | |
| Tarragon Leaves | 4->1 | 2->1 | 1->1 || | | | |
| Cassava Root | 4->1 | 2->1 | 1->1 || | | | |

Wiki bug notes (verbatim): *"Onions are discounted from 4 to 1 with only one Homestead
Upgrade: Fiber Trade Efficiency"*; *"Potatoes are not discounted with first Homestead
Upgrade: Fiber Trade Efficiency."* Ten materials get the output-doubling behavior at tier 2
(Black Peppercorn, Vanilla Bean, Head of Lettuce, Onion, Sage Leaf, Raspberry, Saffron
Thread, Pile of Flax Seeds, Orrian Truffle, Seaweed) - wiki's own summary list names exactly
these materials as doubling, and the per-row vendor-table data agrees exactly: the tier-2
Onion row's raw wikitext carries `quantity=2` (1 Onion -> 2 Fiber), the same doubling shape
as every other material on this list. Onion's tier-1 quantity discount (bugged to 1, instead
of the formula's expected 2) still triggers the generic "output doubles once input drops
below 1" rule at tier 2, exactly as `cheapestTree.ts`'s own explicit Onion special-case
computes (`component.quantity = 0.5` at tier 2, which is `< 1`, so `output *= 2` fires,
Section 1.2). There is no wiki-internal inconsistency: the wiki's per-row table and its prose
summary agree, and both agree with the algorithm. (An earlier draft of this table
mistranscribed the Onion tier-2 cell as `1->1`; corrected above to `1->2` after re-checking
the raw wikitext directly - see Verification section.)

Max weekly output: 1600 Refined Homestead Fiber/week fully upgraded.

### 2.3 Efficiency/capacity upgrade acquisition (one-time, per-account, purchased from the same refiner)

Each of the three stations has its own two-tier Efficiency upgrade and up to ~5-6 discrete
Capacity upgrades, all `total=1` (one-time, account-bound purchase), bought from the same
merchant with coin + crafting materials (not achievement points, not mastery points, not
gems). Example (Metal Forge, MEASURED, raw wikitext):

```
Homestead Upgrade: Ore Trade Efficiency (id=102415) - 200 Ursus Oblige + 30 Honey Flower
Homestead Upgrade: Ore Trade Efficiency (id=102416) - 80000 coin + 30 Charged Titan Ore
Homestead Upgrade: Ore Trade Capacity  (id=103146) - 30000 coin + 100 Ursus Oblige
Homestead Upgrade: Ore Trade Capacity  (id=103531) - 200 Ursus Oblige + 45 Lowland Pine Log
Homestead Upgrade: Ore Trade Capacity  (id=102500) - 70000 coin + 75 Refined Homestead Fiber
Homestead Upgrade: Ore Trade Capacity  (id=103247) - 100 Rotted Titan Amber + 70 Hardened Leather Section
Homestead Upgrade: Ore Trade Capacity  (id=102653) - 100 Refined Homestead Wood + 250 Gossamer Scrap
```
Farm and Lumber Mill have symmetric upgrade lists (different ids, same cost shapes,
cross-referencing the *other two* stations' refined materials as ingredients - e.g. the
Metal Forge's own capacity upgrade needs Refined Homestead **Fiber**). Each `Efficiency`
upgrade is exactly what raises `userEfficiencyTiers[materialId]` from `"0"`->`"1"`->`"2"` in
gw2efficiency's model; each `Capacity` upgrade is what raises the weekly trade cap from its
200 base toward 800.

### 2.4 A fourth acquisition path not yet discussed: Black Market

All three stations additionally have a **"Black Market"** vendor offer: escalating-price
purchases of a **25-unit lot** of the refined material, capped at **300 purchases of 25/
account/week** (= 7500 units/week max), price resets weekly, starting at roughly 616g/lot
and climbing (wiki gives the first 10 prices per station: 61600, 62096, 62600, 63112,
63624, 64144, 64664, 65192, 65720, 66256 copper for lots 1-10; identical schedule quoted on
all three station pages). This is a **separate, coin-only, always-available (no efficiency
tier, no raw-material cost) vendor path** competing with the refinement conversions
themselves - **currently entirely unseeded** in `ref/vendor_offers.json` (checked: zero
`merchantName` entries containing "Black Market" across all 53,530 seeded offers).

### 2.5 Account-wide, no per-character distinction

Confirmed by the Homestead overview page framing ("account-wide personal housing system")
and by the caps being stated as "per account per week" throughout - there is no
per-character refinement allowance.

### 2.6 Patch changes since release

**UNVERIFIED** - I did not find (and did not exhaustively search for) a changelog/patch-note
history specifically for Homestead Refinement rate or cap changes since Janthir Wilds'
release. The wiki pages show no visible "changed in patch X" annotations on the rows I
fetched. Treat the tables above as the *current* wiki state only, not a change history.

---

## 3. Current module state (read directly, not assumed)

### 3.1 The data is already seeded - accidentally, and without tier tagging

`ref/vendor_offers.json` (53,530 total offers, `schemaVersion: 1`, `source: "gw2wiki-smw"`)
already contains **236 offers** whose `merchantName` is exactly one of
`"Homestead Refinement—Farm"` (183), `"—Lumber Mill"` (25), `"—Metal Forge"` (28)
(counts **MEASURED** by loading and filtering the file directly). I diffed these against
the wiki tables in Section 2.2 and they match: every tier-0/1/2 row for every input material
on all three stations is present as an **independent, untagged, unconditional** `VendorOffer`
- there is no `Tier` field on the `VendorOffer` model (`Models/VendorOffer.cs`) or in the
JSON payload, so these 3 (or up to 18/21-per-station) parallel rows for the *same* output
item look, to the solver, like N independent competing offers with no relationship to each
other. `DailyCap`/`WeeklyCap` are `null` on every one of them (checked all 236; also true
for all 53,530 per KNOWN-ISSUES #28, independently re-confirmed here for the homestead
subset specifically).

### 3.2 Effect on today's solver (a real, live, currently-shipping defect - not merely "we model nothing")

`Services/PlanSolver.cs`'s `EvaluateVendorOffers` (private static method, ~line 604) has no
special-casing at all for merchant name - it treats every offer in `vendorOffers[node.Id]`
uniformly: prices any `Item`-type cost line via the current price map/basis, computes
`unitsNeeded = ceil(node.Quantity / offer.OutputCount)`, and picks the numerically cheapest
comparable offer. Since our seed carries all 3 tiers of, say, Iron Ore -> Metal
(4:2, 2:2, 1:1) as three separate offers with no gating, **the solver will always silently
select whichever tier is cheapest** (generally the highest tier, since it needs the least
raw ore per unit output) - i.e. **the module currently behaves as if every account has both
efficiency upgrades on every Homestead station, and even owns Janthir Wilds at all,
unconditionally, with no way to turn it off.** This is the opposite of gw2e's own conservative
default (tier `"0"`, Section 1.3) and is a real defect independent of the "we model nothing"
framing in KNOWN-ISSUES #24 - the module *does* model something today, just silently and
wrongly relative to upstream.

### 3.3 The M34 cap machinery, and a second gap specific to Homestead's shape

`CraftingPlan.TimegatedItems` / `PlanSolver.FinalizeVendorBatches` (confirmed by reading
both files) already implement exactly the gw2e-parity warn-only behavior from Section 1.4:
a cap is read from the *winning offer's* `DailyCap`/`WeeklyCap`, never gates
`Source`/`TotalCost`, and only produces a `TimegatedItem` entry when the merged step's
`unitsNeeded > cap`. Per #28 this is inert today (no offer anywhere carries a cap).

I found an **additional, Homestead-specific gap** that would still apply even after caps are
seeded: the cap-check block only runs when
`vendorBatchTracking[stepKey].Conflict == false` (`PlanSolver.cs` ~line 1178,
`FinalizeVendorBatches`). `stepKey` for any `BuyFromVendor` decision is
`(node.Id, AcquisitionSource.BuyFromVendor, 0)` (line 955) - i.e. **every occurrence of the
same item id across the whole tree merges into one step regardless of which specific offer
each occurrence picked.** `Conflict` is set the first time two occurrences disagree on the
*exact* winning offer's batch shape (`VendorBatchesEqual`, comparing `OutputCount`/
`CoinCostPerBatch`/`CurrencyCostLinesPerBatch`). Homestead Refinement is the prototypical
case where this fires: a plan needing, say, 500 units of Refined Homestead Fiber is very
likely to satisfy different tree occurrences via different specific crop offers (176
candidate crop recipes exist for Fiber alone), since each occurrence's local price
comparison is independent. The moment two occurrences disagree, `Conflict = true`, and the
**entire cap-check block is skipped for that item** - not double-counted, but silently
**never evaluated at all**, even though every Homestead offer for a given output shares the
identical station-wide weekly cap (Section 2.2) and so a per-offer "which exact batch shape
won" disagreement is irrelevant to whether the cap check *could* still be evaluated
correctly. This is worth fixing but is separable from "seed the cap value" - flagged fully
in Section 6.

### 3.4 No merchant/station name reaches the UI at all

`VendorOffer.MerchantName`/`.Locations` exist on the raw model but I found no reference to
either field anywhere in `Models/PlanStep.cs`, `Services/PlanViewModelBuilder.cs`, or any
Views file - they are read by `EvaluateVendorOffers` for none of its own logic and then
discarded. A plan that picks a Homestead Refinement offer today shows only a generic
"Vendor" source tag, with no indication it's specifically a homestead station (or which
one).

### 3.5 ModuleSettings / snapshot pattern to build on

`Services/ModuleSettings.cs` has no Homestead-related setting. The closest existing
precedent is `ValueOwnMaterials` (a plain `SettingEntry<bool>`, default `true`, exposed via
a `Checkbox` in `Views/SettingsTabContent.cs`'s "Plan Defaults" section, applies immediately,
snapshotted onto `Models/PlanSolveContext.cs.OwnMaterialsMode` at generation time so a local
re-solve via `ResolveWithOverrides` never silently re-prices under the user) and
`CurrencyValuationsJson` (a `SettingEntry<string>` holding a JSON-serialized
`Dictionary<int,long>`, converted via a small Blish-free `Services/CurrencyValuationSerializer.cs`
with defensive `Serialize`/`Deserialize` static methods, unit-tested in
`tests/GW2CraftingHelper.Tests/Services/CurrencyValuationSerializerTests.cs`). Both are
directly reusable templates for the new setting (Section 4).

### 3.6 Ground-truthed effect on plans: Exordium is unaffected; Klobjarne Geirr (a real, currently-generatable legendary) is affected

Per the task's explicit instruction not to assume, I loaded `ref/recipes_seed.json`
(14,732 recipes, UTF-8-BOM) and did a full BFS ingredient closure from Exordium's item id
(`90551`, confirmed via WebSearch -> `gw2efficiency.com/crafting/calculator/90551-Exordium`).
**168 distinct items are reachable from Exordium's tree in the module's own recipe seed, and
none of them is 102205/102306/103049 (or the one Mystic Forge recipe, `outputItemId 103242`,
that consumes all three).** **Homestead Refinement has zero effect on any Exordium plan** -
confirmed by direct graph traversal of the module's own data, not inferred.

A real, currently-generatable plan that **is** affected: `Refined Homestead Metal` (102205),
`... Wood` (103049), and `... Fiber` (102306) are consumed 250 each by a Mystic Forge recipe
(module recipe id `-1534`, alongside 250x `Shard of the Homestead` / item 103587) producing
**Gift of Embracing Refuge** (103242, `CraftingMaterial`/`Legendary`) - confirmed via GW2 API
item description, which also states this in turn feeds **Gift of the Homesteader** (102376)
alongside Gift of Condensed Might, Gift of Condensed Magic, and 38 Mystic Clovers. Gift of
the Homesteader is a documented ingredient of the Janthir Wilds legendary spear
**Klobjarne Geirr** (item id `103815`, confirmed via `ref/item_name_seed.json` and via
web search of community legendary-spear guides). I re-ran the same BFS from `103815` against
`ref/recipes_seed.json` and confirmed **Klobjarne Geirr is itself present as a full recipe
target in the module's own seed data** (167 reachable items), and its tree **does** reach
102376 -> 103242 -> {102205, 102306, 103049} (all five ids appear in the BFS's `found`
dictionary with the correct parent-child edges). **This is the concrete, verifiable,
currently-generatable plan to validate any implementation against** - a
`GenerateStructuredAsync`/plan-generation run for item `103815` should be the manual/
automated check that Homestead Refinement participation actually changes behavior once
implemented (today it silently participates already, wrongly, per Section 3.2).

---

## 4. Recommended echo design

### 4.1 Seed schema change (small, additive, backward-compatible)

Add one nullable field to `Models/VendorOffer.cs`:

```csharp
// Homestead Refinement tier this specific offer row corresponds to (0/1/2),
// or null for every non-Homestead-Refinement offer. Wiki-sourced per-row
// quantities already bake in the game's own per-material tier anomalies
// (Onion/Potato/Iron Ore, Section 2.2) - tagging existing rows, rather
// than collapsing them into a formula, avoids re-deriving those bugs in code.
public int? HomesteadTier { get; set; }
```

Tag all 236 already-seeded `"Homestead Refinement—*"` offers with the correct tier by
extending `tools/VendorOfferUpdater` to retain the `requirement=` attribute it currently
parses-and-drops from the wiki's `{{vendor table row}}` templates (confirmed by grep: zero
references to "requirement"/"efficiency"/"Homestead" anywhere in the tool's `.cs` files
today - it captures cost/output/merchant/location and nothing else). Whether this attribute
is exposed as a queryable SMW property or only recoverable via raw-wikitext parsing of the
`requirement=one/two [[Homestead Upgrade: ...]]` string is **UNVERIFIED** - I did not
inspect `WikiSmwClient`'s actual SMW query printouts closely enough to say; this is a
concrete open question for the implementing session (Section 6). Absent-tier rows (no
`requirement=`) are tier 0. Additionally seed `WeeklyCap = 200` (the wiki's base,
un-upgraded cap - the conservative, verifiable-without-account-data floor, per the repo's
"never invent data the user hasn't confirmed" instinct) on all 236 rows, per Section 2.2's
"cap is shared per-station, not per-input-material" finding - every row for a given output
item at a given station should carry the identical `WeeklyCap` value.

**Also seed the Black Market offers** (Section 2.4) as three additional plain vendor offers
(one per material, `outputCount = 25`, coin-only cost lines per the wiki's price ladder,
`HomesteadTier = null` since it's tier-independent, `WeeklyCap = 300 * 25 = 7500` unit-cap or
`300` if the cap should be expressed in "trades" like the refinement rows - needs a decision,
since our `VendorOfferBatch`/`TimegatedItem` cap-check currently compares `unitsNeeded`
(purchases), matching the wiki's own "trades, not quantity" framing, so `WeeklyCap = 300`
purchases is the correct unit to seed). This is presently entirely unseeded and is a small,
independent addition alongside the tier work.

### 4.2 Settings

New setting mirroring `CurrencyValuationsJson`'s exact pattern: a `SettingEntry<string>`
(`HomesteadEfficiencyTiersJson`) holding a JSON `Dictionary<int,int>` (material item id ->
tier 0/1/2), converted via a new Blish-free `Services/HomesteadEfficiencyTierSerializer.cs`
(same shape as `CurrencyValuationSerializer`: defensive `Serialize`/`Deserialize`, invalid/
out-of-range entries (tier outside 0-2, or a key not in {102306,102205,103049}) individually
skipped rather than discarding the whole map, malformed JSON caught and treated as "all
zero," never throws). **Default: tier 0 for all three materials** - this is not a new
invented default, it is gw2e's own default *and* its own no-API-key fallback (Section 1.3),
and matches the repo's "no invented data" posture better than assuming any upgrade level.

**Divergence recommendation (flagged explicitly, not silently added):** gw2efficiency has no
"do you even own Homestead" gate at all (Section 1.5) - it always offers the tier-0 rate to
every account. I recommend our module add one beyond pure parity: a master
`HomesteadUnlocked` `SettingEntry<bool>`, **default `false`**, that when unchecked excludes
every `HomesteadTier`-tagged offer from `EvaluateVendorOffers`'s candidate set entirely
(simplest implementation: skip the offer in the loop when `offer.HomesteadTier.HasValue &&
!settings.HomesteadUnlocked.Value`). Rationale: gw2efficiency is a browser tool typically
used by already-progressed players; this module runs inside the live GW2 client for anyone,
including players who have never touched Janthir Wilds, and recommending a purchase path the
player cannot execute is a worse in-client UX failure than it is on a website. This is a
judgment call, not a research finding - it should be confirmed with the user/maintainer
before implementation, since it is a deliberate divergence from the researched upstream
behavior, not an echo of it.

### 4.3 Settings UI

Extend `Views/SettingsTabContent.cs`'s existing "Plan Defaults" section (or add a new
"Homestead Refinement" section immediately after it, following the same
`AddSectionHeader`/`AddInfoLine`/row-panel pattern already used for Currency Valuations and
Value Own Materials): one `Checkbox` for `HomesteadUnlocked` (label: "I have Homestead
refinement stations unlocked", same immediate-apply/no-Save-button pattern as
`ValueOwnMaterials`'s checkbox), plus three tier controls (one per material: Fiber/Metal/
Wood), each constrained to 0/1/2. Blish HUD's `Checkbox` control doesn't natively express a
3-state value; the two lowest-risk options, both consistent with existing patterns in this
codebase, are (a) two checkboxes per material ("Efficiency upgrade 1", "Efficiency upgrade
2", second one disabled/unchecked-and-ignored unless the first is checked - mirrors the
wiki's own "one upgrade"/"two upgrades" phrasing exactly) or (b) a numeric stepper/dropdown
if one is already in use elsewhere in this codebase. **I did not find an existing Dropdown/
stepper control in this codebase's `Views/` to confirm which primitive is idiomatic here** -
this is a UI-implementation detail for the implementing session to resolve against whatever
Blish_HUD.Controls are already imported, not something I can settle from research alone.
All four controls disabled (or hidden) entirely when `HomesteadUnlocked` is unchecked, matching
the master-gate design in 4.2.

### 4.4 Solver participation

In `PlanSolver.EvaluateVendorOffers`, before the existing per-offer loop, filter the
candidate `offers` list to those where `!offer.HomesteadTier.HasValue || (context allows
Homestead && offer.HomesteadTier.Value <= configuredTier[offer.OutputItemId])`. Using `<=`
rather than `==` is intentional and matches real game behavior (Section 2.2 confirms a
tier-2 station can still be used at the tier-0/1 rate for the same input - though the
"cheapest wins" comparison already makes this moot in practice, since a strictly-better
tier's row is never worse, `<=` is the correct and simplest filter, not merely a
convenience). This requires threading the configured tier map (and the `HomesteadUnlocked`
flag) through `PlanSolver.Solve`'s parameter list, following the exact precedent already set
by `currencyValuation` (an optional parameter, snapshotted onto
`Models/PlanSolveContext.CurrencyValuation` at generation time, re-used as-is by
`ResolveWithOverrides` per that class's own documented "freshly edited settings apply
starting with the next full Generate" rule - the new Homestead settings should follow the
identical snapshot-and-reuse contract, added as `PlanSolveContext.HomesteadEfficiencyTiers`/
`.HomesteadUnlocked`).

This deliberately keeps our module's data shape (N pre-expanded wiki-sourced rows per
material, Section 3.1) rather than collapsing to gw2e's shape (1 row + a halving formula with
three hardcoded item-id exceptions, Section 1.2) - the wiki-sourced rows already encode the
Onion/Potato/Iron-Ore anomalies correctly as plain numbers, so no bug-for-bug C# port is
needed, and the existing `EvaluateVendorOffers` cost/comparison logic needs no change beyond
the candidate-filter step above.

### 4.5 Cap check fix (Section 3.3's gap) - recommended but separable/optional for v1

The minimal, targeted fix: in `AggregateStep`/`VendorBatchState`, track cap agreement
independently of full batch-shape agreement (`VendorBatchesEqual`) - i.e. a second, coarser
flag that only compares `(DailyCap, WeeklyCap)` tuples across occurrences, since Homestead's
real-world cap is identical across every input-material offer for the same output+station
(Section 2.2) even when the specific offers disagree (which is the normal, expected case for
Homestead specifically). `FinalizeVendorBatches`'s cap-check block would then key off this
new flag instead of the existing `Conflict` flag, while cost recomputation keeps using
`Conflict` unchanged. This is a genuine, real gap but is separable from the core tier-gating
fix (Section 4.4) - I recommend documenting it explicitly as a known, accepted v1 limitation
(with a test proving the current suppressed-notice behavior, Section 4.7) rather than
bundling it into the same change, since caps are inert today regardless (#28) and can be
revisited once real cap data is seeded and observed to actually matter for a real plan.

### 4.6 Display

Surface `VendorOffer.MerchantName` (currently discarded, Section 3.4) onto `PlanStep` for
any `BuyFromVendor` step, at minimum for Homestead offers specifically if a general fix is
out of scope here - render "Homestead Refinement — Farm" (etc.) as the source label instead
of a generic vendor tag, matching gw2efficiency's own "reuse the Crafting pill, relabel it
'Merchant'" pattern (parity spec Section 3.2/6.1) rather than inventing new UI vocabulary.
Optionally add a plan-level informational flag mirroring gw2e's `hasEfficiencyTierNonMaxCrafts`
(Section 1.3) - "a used Homestead conversion is below your configured tier" - lower priority,
nice-to-have.

### 4.7 Test plan

- `HomesteadEfficiencyTierSerializerTests` (new, mirrors `CurrencyValuationSerializerTests`):
  round-trip, malformed JSON, out-of-range tier, unknown material id, empty/null input.
- `VendorOfferHasherTests` / dataset schema version: adding `HomesteadTier` changes the
  seeded offers' content, and `VendorOfferHasher` derives `offerId` from offer content
  (per #28's own warning) - confirm whether tagging existing rows changes their `offerId`s,
  and update any hard-coded-id tests/snapshots accordingly. This must be checked, not
  assumed, before touching the seed file.
- `PlanSolverTests` (real production path, per repo invariant - no contract mirrors):
  (1) default settings (`HomesteadUnlocked=false`) exclude all `HomesteadTier`-tagged offers
  even when cheapest; (2) `HomesteadUnlocked=true`, tier 0 picks only tier-0 rows even when a
  tier-2 row is seeded and cheaper; (3) raising a material's configured tier to 1/2 unlocks
  the better row; (4) a non-Homestead vendor offer (`HomesteadTier == null`) is unaffected by
  either setting; (5) an end-to-end small synthetic tree reproducing the Klobjarne Geirr /
  Gift of Embracing Refuge chain (Section 3.6) with a handful of real ids, confirming the
  tier setting changes the winning offer/cost at the top of a multi-level tree, not just at
  the immediate leaf.
- A test that explicitly captures the Section 3.3/4.5 cap-suppression behavior as it exists
  today (two occurrences of the same Homestead output resolved via different input-material
  offers -> no `TimegatedItem` even with `WeeklyCap` seeded and exceeded) - so the limitation
  is a documented, intentional-for-now behavior rather than a silent regression risk if/when
  someone eventually "fixes" it.
- `CraftingPlanPipelineTests`: confirm `PlanSolveContext.HomesteadEfficiencyTiers`/
  `.HomesteadUnlocked` are snapshotted at generation time and `ResolveWithOverrides` reuses
  the snapshot rather than re-reading live settings mid-session, matching every other
  settings-snapshot field on that class.
- Manual/offline-harness verification (per the project's existing Harness tool pattern):
  generate a plan for item `103815` (Klobjarne Geirr) with `--profile`-style real data and
  confirm the Homestead-sourced portion of the tree changes visibly between
  `HomesteadUnlocked=false` (excluded entirely) and `=true` at tier 0 vs tier 2.

---

## 5. Sources

- `github.com/gw2efficiency/recipe-calculation` @ `master` - `src/cheapestTree.ts`,
  `src/helpers/dailyCooldowns.ts`, `src/static/dailyCooldowns.ts` (fetched via
  `raw.githubusercontent.com`, direct `curl`, cross-checked against an independent WebFetch
  summarization of the same URL - both agreed verbatim on the quoted function bodies).
- `github.com/gw2efficiency/recipe-nesting` @ `master` - `src/api.d.ts`, `src/index.ts`
  (same fetch method).
- `gw2efficiency.com/crafting/calculator` (live HTML) and its referenced bundle
  `gw2efficiency.com/scripts/application.js?cb=1783715316` (4.22MB, fetched directly via
  `curl`, grepped for `Homestead`/`efficiencyTier`/`userEfficiencyTiers`/
  `HomesteadRefinementsController`/`daily_purchase_cap`/`weekly_purchase_cap`).
- `wiki.guildwars2.com` raw wikitext (`action=raw`) for: `Homestead`,
  `Homestead_Refinement—Metal_Forge`, `Homestead_Refinement—Farm`,
  `Homestead_Refinement—Lumber_Mill`, `Homestead_Upgrade:_Ore_Trade_Efficiency`.
- `api.guildwars2.com/v2/items/{id}` for every item id named in this report (102306, 102205,
  103049, 12142, 12135, 19699, 19697, 19698, 19700, 19701, 19702, 19703, 19722-19727, 103242,
  103587, 102376, 90551 via search, 103815 via `ref/item_name_seed.json`).
- Module source (read directly this session): `ref/vendor_offers.json`, `ref/recipes_seed.json`,
  `ref/item_name_seed.json`, `Models/VendorOffer.cs`, `Models/VendorOfferDataset.cs`,
  `Models/CraftingPlan.cs`, `Models/PlanStep.cs`, `Models/PlanSolveContext.cs`,
  `Models/CurrencyValuation.cs`, `Models/TimegatedItem.cs`, `Models/AcquisitionSource.cs`,
  `Services/PlanSolver.cs`, `Services/ModuleSettings.cs`, `Services/CurrencyValuationSerializer.cs`,
  `Views/SettingsTabContent.cs`, `tools/VendorOfferUpdater/*.cs`.
- `docs/gw2e-parity-spec.md`, `docs/KNOWN-ISSUES.md` (project references, ground rules and
  prior findings I was told to start from - re-verified rather than blindly trusted where the
  task asked me to establish something specific, per Section 1.4).
- WebSearch: "Guild Wars 2 wiki Homestead refinement station tier efficiency..." (used only
  to locate wiki page URLs and the Klobjarne Geirr / Gift of the Homesteader relationship,
  cross-checked against the module's own recipe seed and the GW2 API rather than trusted on
  its own).

## 6. Open questions

1. **UNVERIFIED**: The actual live numeric `weekly_purchase_cap`/`daily_purchase_cap` value
   gw2efficiency's own (non-public) data pipeline assigns to Homestead Refinement recipes -
   I could not access their backend data. Recommend seeding the wiki's verified base value
   (200/week) rather than guessing at gw2e's actual live number.
2. Does `tools/VendorOfferUpdater`'s wiki ingestion (SMW query or raw-wikitext parse) already
   have access to the `requirement=` attribute on `{{vendor table row}}`, or does this need a
   new parsing path? Not resolved in this research pass - the tool currently drops it
   entirely and I did not trace `WikiSmwClient`'s query printouts far enough to say which.
3. Should the Black Market path (Section 2.4, entirely unseeded) be added in the same change
   as the tier work, or tracked separately? It's independent of tiers (no efficiency
   interaction) but shares the "Homestead" identity and the same three output items.
4. Confirm with the user/maintainer whether the recommended `HomesteadUnlocked` master gate
   (Section 4.2) - a deliberate divergence from gw2e, which has no such gate - is wanted, or
   whether pure tier-0-default parity (still a large improvement over today's unconditional
   best-tier blending) is preferred for v1.
5. Section 3.3/4.5's cap-check `Conflict`-suppression gap: fix now, or document-and-defer?
   Recommend defer (Section 4.5's reasoning), but flagging for an explicit decision since it
   is exactly the kind of "silently still broken after the seed work looks done" trap the
   project's own review discipline exists to catch.
6. **RESOLVED (was: wiki-internal inconsistency)**: the original draft of this report
   claimed Section 2.2's per-row table and the wiki's own prose summary disagreed on whether
   Onion doubles at tier 2. Re-verification found no such inconsistency - the report's
   original per-row table had simply mistranscribed the Onion tier-2 cell as `1->1`; the
   wiki's raw wikitext, the wiki's prose, and `cheapestTree.ts`'s own Onion special-case all
   agree that Onion doubles at tier 2 (`1->2`), and the module's already-seeded
   `ref/vendor_offers.json` already has the correct value. No action item remains here; this
   entry is kept (rather than deleted) only so a reader of an earlier cached copy of this
   report knows the "inconsistency" claim was retracted, not silently dropped.
7. Whether the three refinement-station NPCs are unconditionally present in every homestead
   plot (Section 2.1) or require some placement/build step I didn't find documented -
   inferred from absence of a stated requirement, not positively confirmed.

---

## Verification

An independent verifier re-fetched every primary source cited in this report directly
(rather than trusting this report's quotes/tables) and re-derived the load-bearing claims.
Scope of the cross-check: `cheapestTree.ts` and `dailyCooldowns.ts`
(`recipe-calculation@master`), `recipe-nesting`'s `api.d.ts`/`index.ts`, a fresh fetch of the
live gw2efficiency bundle, GW2 API lookups for all 11 item ids named in this report, fresh
raw-wikitext fetches of all four wiki pages (`Homestead`, `Homestead_Refinement—Metal_Forge`,
`—Lumber_Mill`, `—Farm`), and direct reads of every repo file this report cites
(`Models/VendorOffer.cs`, `ref/vendor_offers.json`, `Services/PlanSolver.cs`,
`ref/recipes_seed.json`, plus a full BFS re-run from both Exordium and Klobjarne Geirr).

**Corrections applied as a result of this pass:**

- **Section 2.2, Farm table, Onion row.** The tier-2 cell was mistranscribed as `1->1`; the
  wiki's raw wikitext for that row carries `quantity=2` (1 Onion -> 2 Refined Homestead
  Fiber), a doubling, matching every other tier-2 doubler on the page and matching
  `cheapestTree.ts`'s own Onion special-case (`component.quantity = 0.5` at tier 2, which is
  `< 1`, so the generic output-doubling rule fires). Corrected to `4->1 | 1->1 (bug) | 1->2`.
- **Section 2.2, prose paragraph following the Farm table.** Previously claimed a
  "wiki-internal inconsistency" between the per-row table and the wiki's own prose summary of
  which ten materials double at tier 2, and resolved that (non-existent) inconsistency in
  favor of the report's own (incorrect) per-row transcription. Rewritten: the wiki's per-row
  data, its prose summary, and the algorithm all agree that Onion is among the ten tier-2
  doublers; there was no inconsistency, only a transcription error in this report's first
  draft.
- **Open Question 6.** Previously asked the reader to treat the (non-existent) wiki
  inconsistency as an unresolved authoring ambiguity requiring in-game confirmation. Replaced
  with a note marking the item resolved, so anyone working from an earlier cached copy of
  this report does not chase a bug that isn't there or re-derive a formula off the wrong
  table value.
- **Section 3.1's diff claim** ("I diffed these against the wiki tables in Section 2.2 and
  they match") was re-checked against the corrected table: the module's seeded
  `ref/vendor_offers.json` row for Farm/Onion/tier-2 has `outputCount=2`,
  `costLines=[{type:Item,id:12142,count:1}]` - i.e. `1->2` - which now matches the corrected
  Section 2.2 table exactly. (Against the original, uncorrected table this claim was false
  for that one cell; the seed data was right and the report's table was wrong. No other
  discrepancies were found across the remaining 235 seeded Homestead offers.)
- **Section 2.2, Lumber Mill table, item id column.** A later adversarial review caught a
  transposition error this pass's own "match the wiki exactly, no corrections needed" claim
  (below) had missed: a live GW2 API lookup (`api.guildwars2.com/v2/items`) for the six wood
  log ids confirms `19722`=Elder Wood Log, `19723`=Green Wood Log, `19724`=Hard Wood Log,
  `19725`=Ancient Wood Log, `19726`=Soft Wood Log, `19727`=Seasoned Wood Log, but the table
  had paired `19724` with "Soft Wood Log", `19725` with "Seasoned Wood Log", `19726` with
  "Hard Wood Log", and `19727` with "Ancient Wood Log" - Soft/Hard and Seasoned/Ancient
  transposed. Corrected above: only the name column changed per row; the id column was left
  untouched, since `ref/vendor_offers.json`'s rows key off the real item id (confirmed
  correct) rather than this report's name column, so no runtime data was affected. **This
  pass's claim that "every ratio cell" was also untouched and therefore fine was itself
  wrong** - see the follow-up correction immediately below.
- **Section 2.2, Lumber Mill table, ratio cells (second-pass correction).** A further
  adversarial review caught what the previous correction introduced: moving the *name*
  column onto the row whose *id* and *ratio* were left in place re-paired id-to-name
  correctly but left name-to-ratio (and therefore id-to-ratio) wrong for 4 of the 6 rows,
  including transplanting a real tier-2 output-doubling behavior onto the wrong material.
  Re-fetched `wiki.guildwars2.com/index.php?title=Homestead_Refinement%E2%80%94Lumber_Mill&action=raw`
  directly and confirmed the true per-material ratios: Green Wood Log (19723) 20/10/5 -> 1
  (unchanged), Hard Wood Log (19724) 4/2/1 -> 1, Ancient Wood Log (19725) 2/1/1 -> 2 (its
  tier-2 row's raw wikitext carries `quantity=2`, a real doubling), Soft Wood Log (19726)
  12/6/3 -> 1, Elder Wood Log (19722) 4/2/1 -> 1 (unchanged), Seasoned Wood Log (19727)
  4/2/1 -> 1 (no doubling - the previous table's `1->2` cell for Seasoned was fabricated by
  the prior fix, carried over from Ancient's real ratio). The correct remediation was to move
  the *id* column onto the row whose name+ratio were already correct, not the name column
  onto the row whose id+ratio were left in place; applied that way this time. Table corrected
  above. No runtime data was affected (`ref/vendor_offers.json` keys off item id and was never
  derived from this report's table), but this report's own stated purpose as ground truth for
  a future re-seed or test fixture was wrong for Hard/Ancient/Soft/Seasoned Wood Log until this
  correction.

**Independently confirmed, no changes needed:** every other quoted code body
(`cheapestTree.ts`, `dailyCooldowns.ts`, `recipe-nesting`'s `api.d.ts`/`index.ts`) matches
source verbatim; `dailyCooldowns()` is confirmed unreferenced by any pricing/craft-flag path,
supporting the "informational-only" claim; the live bundle's account-scoped
`efficiencyTiers` persistence, `$watchGroup`, and `Account_HomesteadRefinementsController`
wiring match exactly; all 11 item ids resolve to the names/types/rarities stated; the Metal
Forge table, weekly-cap structure, and Black Market price ladder match the wiki exactly with
no corrections needed (the Lumber Mill table required the id/name and, in a later pass, the
ratio-cell correction above); all
repo-side line numbers, counts (236 seeded Homestead offers: 183/28/25; 14,732 recipes;
168-item Exordium BFS closure; 167-item Klobjarne Geirr BFS closure), and the absence of
`MerchantName`/`Tier` usage in `PlanSolver.cs`/`PlanViewModelBuilder.cs`/`Views/` were
reproduced exactly.

**Remaining uncertainty (unchanged from the original report, carried forward as still
open):** the live numeric `daily_purchase_cap`/`weekly_purchase_cap` gw2efficiency actually
assigns to a Homestead recipe (Section 1.1/1.4, Open Question 1); whether
`tools/VendorOfferUpdater`'s wiki ingestion path can already reach the `requirement=`
attribute needed to tag tiers (Open Question 2); whether to seed the Black Market path in the
same change as the tier work (Open Question 3); the `HomesteadUnlocked` master-gate divergence
decision (Open Question 4); whether to fix or defer the `Conflict`-suppression cap-check gap
(Open Question 5); and whether the three station NPCs are unconditionally present in every
homestead plot (Open Question 7). None of these were in scope for this verification pass and
none are affected by the Onion correction above.
