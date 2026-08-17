# M37 Research Report - Item #26: Achievement-bit ingredient dedup (parity micro-gap)

Scope: KNOWN-ISSUES.md item 26. Research-only (dev-time); the module never
calls gw2efficiency or the wiki at runtime. All findings below are labeled
MEASURED (I ran code/fetched a file/queried an API and observed a concrete
result), OBSERVED (I read source code directly, quoted verbatim, and traced
the logic by hand - not executed), INFERRED (a reasonable conclusion from
MEASURED/OBSERVED facts, not itself directly checked), or UNVERIFIED
(could not be confirmed with a primary source - stated explicitly, never
invented).

---

## 1. Upstream gw2e mechanism (exact behavior, quoted code)

### 1.0 Where it lives in the pipeline

Two packages are involved, exactly as documented in
`docs/gw2e-parity-spec.md` Section 0:

- **`@gw2efficiency/recipe-nesting`** (`nestRecipes`) carries the
  `achievement_id`/`achievement_bit` fields from the flat API-shaped
  recipe's `ingredients[]` entries onto the nested tree node it builds for
  that ingredient, unchanged. OBSERVED, `recipe-nesting@master`
  (`src/index.ts`, fetched `raw.githubusercontent.com`, current master at
  research time):
  ```ts
  // src/api.d.ts (recipe-nesting)
  export interface API_Recipes_Entry {
    ...
    achievement_id?: number
    ...
    ingredients: Array<{
      type: 'Item' | 'GuildUpgrade' | 'Currency'
      id: number
      count: number
      achievement_id?: number
      achievement_bit?: number
    }>
    ...
  }
  ```
  ```ts
  // src/index.ts, nestRecipe(), building a nested Recipe-type component
  return {
    ...omit(componentRecipe, ['nested']),
    quantity: component.quantity,
    ...(typeof component.achievement_id === 'number' && {
      achievement_id: component.achievement_id,
    }),
    ...(typeof component.achievement_bit === 'number' && {
      achievement_bit: component.achievement_bit,
    }),
  }
  ```
  A `Currency`-type ingredient is passed through untouched ("Just give back
  the component for currencies") - `achievement_bit` dedup below explicitly
  excludes `Currency`-type nodes for the same reason. A plain
  (non-recipe-having) `Item`-type ingredient also just carries its own
  `achievement_id`/`achievement_bit` fields as set on the ingredient object
  itself (`BasicItemComponent` type explicitly includes both fields).
  recipe-nesting itself does **no** deduplication - it is a pure,
  price/session-independent structural transform. This confirms the dedup
  key downstream is the plain **item id** (`tree.id`), never the
  `achievement_id` (which only identifies *which* GW2 achievement the
  ingredient's edge belongs to) and never the numeric `achievement_bit`
  value itself (which is only *evidence that this edge is achievement-
  gated at all* - see 1.2, the check is `typeof tree.achievement_bit ===
  'number'`, a boolean gate, not a value comparison).

- **`@gw2efficiency/recipe-calculation`**'s `cheapestTree` orchestrates a
  two-part mechanism that is the *entire* dedup implementation. Nothing in
  `calculateTreePrices.ts` or `calculateTreeCraftFlags.ts` mentions
  `achievement_bit` at all (confirmed by reading both files in full,
  quoted in Section 1.3 below) - the effect on price/craft-flags is a pure
  side effect of the quantity becoming zero, via the exact same code path
  every already-owned (`usedQuantity===0`) node goes through.

### 1.1 Part 1: `initialTreeChecks` - a full-tree pre-seed pass

OBSERVED, `recipe-calculation@master`, `src/cheapestTree.ts` (fetched
directly, quoted in full for the relevant functions):

```ts
export function initialTreeChecks(
  tree: NestedRecipe,
  userEfficiencyTiers: Record<string, string>,
  ignoredBitItemIds: Array<number>,
  bitItemIds = new Set<number>(),
  normalItemIds = new Set<number>(),
  isRootNode = true
): NestedRecipe {
  collectItemDataForIgnoringBits(tree, bitItemIds, normalItemIds)
  tree = applyEfficiencyTiersToTree(tree, userEfficiencyTiers)

  if ('components' in tree && Array.isArray(tree.components)) {
    tree = {
      ...tree,
      components: tree.components.map((component) =>
        initialTreeChecks(component as NestedRecipe, userEfficiencyTiers,
          ignoredBitItemIds, bitItemIds, normalItemIds, false)
      ),
    }
  }

  if (isRootNode) {
    bitItemIds.forEach((id) => {
      if (normalItemIds.has(id)) {
        ignoredBitItemIds.push(id)
      }
    })
  }

  return tree
}

function collectItemDataForIgnoringBits(
  tree: NestedRecipe, bitItemIds: Set<number>, normalItemIds: Set<number>
) {
  if (!tree.id) return
  if ((tree.type as 'Recipe' | 'Currency' | 'Item') === 'Currency') return

  if (typeof tree.achievement_bit === 'number') {
    bitItemIds.add(tree.id)
  } else {
    normalItemIds.add(tree.id)
  }
}
```

This walks the **entire, unpruned nested tree once**, before any quantity
math runs, classifying every non-Currency node's `id` into exactly one of
two sets *per occurrence* (`bitItemIds` if that occurrence's own
`achievement_bit` is a number, else `normalItemIds` - the SAME id can and
does end up in both sets if it occurs both ways somewhere in the tree).
After the full walk, `ignoredBitItemIds` (an output parameter, mutated by
reference) is pre-seeded with **every id present in both sets** - i.e. an
id that appears *only* via achievement-bit occurrences never gets
pre-seeded; an id that appears via achievement-bit *and* at least one
plain/normal occurrence gets pre-seeded before any node is ever visited by
the quantity pass.

`cheapestTree.ts` calls this once at the very top of the whole calculation
and threads the SAME `ignoredBitItemIds` array (by reference, so
`initialTreeChecks`'s pushes are visible to every later call) into every
one of its 2-3 `calculateTreeQuantity` calls (the `valueOwnItems` pre-pass,
the main pass, and the post-craft-flags pass):

```ts
const ignoredBitItemIds: Array<number> = []
tree = initialTreeChecks(tree, userEfficiencyTiers, ignoredBitItemIds)
if (valueOwnItems) {
  const treeWithQuantityWithoutAvailableItems =
    calculateTreeQuantity(amount, tree as RecipeTree, {}, ignoredBitItemIds)
  ...
}
const treeWithQuantity =
  calculateTreeQuantity(amount, tree as RecipeTree, availableItems, ignoredBitItemIds)
...
const treeWithQuantityPostFlags =
  calculateTreeQuantity(amount, treeWithCraftFlags, availableItems, ignoredBitItemIds)
```

### 1.2 Part 2: `calculateTreeQuantityInner` - the actual zeroing

OBSERVED, `src/calculateTreeQuantity.ts`, quoted in full:

```ts
export function calculateTreeQuantity(
  amount: number, tree: RecipeTree | RecipeTreeWithCraftFlags,
  availableItems: Record<string, number> = {},
  ignoredBitItemIds: Array<number> = []
) {
  // Make sure that we don't modify the passed-in object
  return calculateTreeQuantityInner(amount, tree, { ...availableItems },
    false, 0, [...ignoredBitItemIds])
}

function calculateTreeQuantityInner(
  amount: number, tree: RecipeTree | RecipeTreeWithCraftFlags,
  availableItems: Record<string, number>, ignoreAvailable = false,
  nesting = 0, ignoredBitItemIds: Array<number>
) {
  const output = tree.output || 1
  let treeQuantity = amount * tree.quantity

  if (typeof tree.achievement_bit === 'number') {
    ignoredBitItemIds.includes(tree.id) ? (treeQuantity = 0) : ignoredBitItemIds.push(tree.id)
  }

  treeQuantity = Math.ceil(treeQuantity / output) * output
  const totalQuantity = Math.round(treeQuantity)

  let availableQuantity = 0
  if (nesting > 0 && tree.type !== 'Currency' && !ignoreAvailable && availableItems[tree.id]) {
    availableQuantity = Math.min(availableItems[tree.id], totalQuantity)
    availableItems[tree.id] -= availableQuantity
  }
  const usedQuantity = totalQuantity - availableQuantity

  if (!tree.components) {
    return { ...tree, components: undefined, output, totalQuantity, usedQuantity }
  }

  const componentAmount = Math.ceil(usedQuantity / output)
  ignoreAvailable = ('craft' in tree && tree.craft === false) || usedQuantity === 0 || ignoreAvailable

  const components = tree.components.map((component) =>
    calculateTreeQuantityInner(componentAmount, component, availableItems,
      ignoreAvailable, ++nesting, ignoredBitItemIds)
  )

  return { ...tree, components, output, totalQuantity, usedQuantity }
}
```

**Exact rule, in order, for a node whose own `achievement_bit` is a
number** (checked BEFORE this node's own quantity is finalized):
- If this node's `id` is **already** in `ignoredBitItemIds` (either
  pre-seeded by `initialTreeChecks`, or pushed there by an EARLIER
  achievement-bit occurrence of the same id encountered earlier in this
  SAME `calculateTreeQuantity` call's depth-first walk) -> force
  `treeQuantity = 0` for this occurrence, right before the
  output-rounding step. This cascades: `totalQuantity=0`,
  `usedQuantity=0`, and (since `componentAmount =
  Math.ceil(0/output) = 0` and `ignoreAvailable` becomes `true` via
  `usedQuantity === 0`) **every descendant of this occurrence also
  collapses to zero quantity**, all the way down - the whole duplicate
  subtree, not just its root, goes to zero.
- Else, this id has not been seen yet in this call -> push its id onto
  `ignoredBitItemIds` and let this occurrence's `treeQuantity` compute
  normally (non-zero, chargeable). This id will now zero every LATER
  achievement-bit occurrence of itself within the same
  `calculateTreeQuantity` call (but a plain/normal occurrence of the same
  id, if one exists elsewhere in the tree, is never touched by this
  check at all - see 1.4).

Each top-level `calculateTreeQuantity()` call makes its own private copy
of `ignoredBitItemIds` (`[...ignoredBitItemIds]`) before recursing, so the
mutations from one call (e.g. the `valueOwnItems` pre-pass) never leak
into the next call's array - **only the `initialTreeChecks` pre-seed is
shared across all of `cheapestTree`'s calls; the "first occurrence wins"
bookkeeping restarts fresh every call.** Because the pre-seed is
unaffected by tree order and DFS order is deterministic given a fixed tree
shape, the SAME occurrence wins "first" in every one of `cheapestTree`'s
own calls (tree component order does not change between its 2-3 internal
`calculateTreeQuantity` calls).

### 1.3 Effect on price and craft-flags: purely a consequence of quantity=0, not a separate rule

OBSERVED. `calculateTreePrices.ts` and `calculateTreeCraftFlags.ts` do
**not** reference `achievement_bit` anywhere (confirmed by reading both
files in full). The zeroing therefore reaches price/craft state only
through the ordinary `usedQuantity` field, exactly the same path any
already-owned node goes through:
```ts
// calculateTreePrices.ts
const buyPrice = buyPriceEach ? tree.usedQuantity * buyPriceEach : false   // 0 when usedQuantity=0
let craftResultPrice = buyPrice
...
// (a Recipe-type deduped node's own children are ALSO usedQuantity=0,
//  so craftDecisionPrice/craftPrice - sums over components - are 0 too)
```
```ts
// calculateTreeCraftFlags.ts
const isUsed = tree.usedQuantity !== 0            // false for a deduped node
const craft = hasComponents && isUsed && isCheaperToCraft && !isForceBuy   // craft=false
```
So a deduped achievement-bit node ends up structurally identical to a
genuinely-already-owned (`usedQuantity===0`) node: zero displayed price,
`craft=false`, and (per `docs/gw2e-parity-spec.md` Section 6.1's pill
table) **no source pill renders on it at all** (every pill's `ng-show`
guard requires `usedQuantity!==0`) - not "HAVE", not "0 gold", just no
interactive pill. This is a UI-level distinction our own module's
"Have" pill (which DOES render, non-interactively) does not have to
replicate; see Section 4.

**No interaction with the craft-vs-buy comparison beyond this**: dedup
never changes `PickCheapest`/`isCheaperToCraft`'s own math, never touches
`forceBuyItems`, and is evaluated strictly before craft flags exist (Part
1 of `cheapestTree`'s two-pass structure, `docs/gw2e-parity-spec.md`
Section 1.3) - it is a quantity-layer effect only, full stop.

### 1.4 Ground-truth test (authoritative - pins exact expected values)

MEASURED: `recipe-calculation@master`,
`tests/calculateTreeQuantity.spec.ts`, `it('handles achievement bit items
correctly', ...)` (fetched verbatim via `raw.githubusercontent.com`,
introduced by commit `b9e0346b` "Put quantity 0 for bits that are
fulfilled elsewhere" and refined by `d9586270` "Better clarity with
namings, added a quick test with some scenarios", both **2026-02**,
i.e. this whole mechanism is recent upstream work, not a
long-standing/battle-tested one). The test tree has, among its root's
direct components:
- id `55`, `achievement_bit: 0` (top-level occurrence)
- id `200` (a nested Recipe) whose own single component is id `55`,
  `achievement_bit: 0` again (deeper nesting)
- id `55`, quantity `2`, **no** `achievement_bit` (a normal occurrence)
- id `56`, `achievement_bit: 1` (first occurrence)
- id `56`, `achievement_bit: 1` again (second occurrence, no normal
  version anywhere)
- id `999`, quantity `1`, no achievement_bit
- id `999`, quantity `3`, no achievement_bit (a second, unrelated normal
  occurrence)

Asserted results (`initialTreeChecks` run first, then
`calculateTreeQuantity(1, recipeTree, {}, ignoredBitItemIds)`):

| Node | totalQuantity | Comment (verbatim from the test) |
|---|---|---|
| id 55, top-level bit | **0** | "Bit exists as real item elsewhere, zeroed" |
| id 55, nested-inside-200 bit | **0** | "Bit exists as real item elsewhere, zeroed in deeper nesting" |
| id 55, normal occurrence | **2** (unchanged) | "Real item is not zeroed when bit version exists" |
| id 56, first bit occurrence | **1** (unchanged) | "Duplicate bit items, first one is kept" |
| id 56, second bit occurrence | **0** | "Duplicate bit items, second one is zeroed" |
| id 999, first normal occurrence | **1** (unchanged) | "Real duplicate items are unaffected" |
| id 999, second normal occurrence | **3** (unchanged) | "Real duplicate items are unaffected" |

This single test fully confirms every claim in 1.1-1.2 with concrete
numbers: (a) id 55 - which has BOTH a bit and a normal occurrence - gets
**both** its bit occurrences zeroed (not just "all but the first"; the
pre-seed means even the very first bit occurrence encountered is zeroed),
while its normal occurrence is completely untouched; (b) id 56 - bit-only,
no normal occurrence anywhere - follows plain "first survives, rest
zeroed" among its own bit occurrences; (c) id 999 - an ordinary repeated
item with no achievement_bit at all - is **not deduplicated in any way**
by this mechanism (each occurrence keeps its own independently-computed
quantity; the parity spec's Section 6.5 note that identical ids get
summed applies only at the later `craftingSteps`/`usedItems`
*display-summarization* layer, never here).

### 1.5 A real fragility in the upstream implementation itself (OBSERVED, worth flagging for the design decision)

`src/updateTree.ts` (called by the live frontend after every manual
craft/buy pill click or amount edit, per `docs/gw2e-parity-spec.md`
Section 1.4) is:
```ts
export function updateTree(amount, tree, itemPrices, availableItems = {}, customCurrencyPrices = {}) {
  const treeWithQuantity = calculateTreeQuantity(amount, tree, availableItems)
  return calculateTreePrices(treeWithQuantity, itemPrices, customCurrencyPrices)
}
```
It calls `calculateTreeQuantity` **without ever passing
`ignoredBitItemIds`**, and never re-runs `initialTreeChecks`. Since the
parameter defaults to `[]`, every post-interaction `updateTree` call
restarts the "first bit occurrence wins" bookkeeping from a **completely
empty, un-pre-seeded** array. Consequence (traced by hand from the code,
not executed - INFERRED from OBSERVED code): the id-56-style case (dedup
among achievement-bit-only occurrences, no normal counterpart) is
self-contained per call and keeps working correctly after `updateTree`.
The id-55-style case (an achievement-bit occurrence that should stay
zeroed *because* a normal occurrence exists elsewhere) is **not**
self-contained - it depends on the pre-seed that only `initialTreeChecks`
computes - so a manual pill click or amount change appears likely to make
that specific zeroed node's quantity reappear (non-zero) until the user
re-runs "Best path" (which calls `cheapestTree` fresh, re-running
`initialTreeChecks`). I did not reproduce this live in the running
calculator (out of scope/no benefit over reading the two functions
directly), so this specific downstream consequence is **INFERRED**, not
MEASURED - flagged as an open question in Section 6, and as a design
choice point in Section 4 (echo the fragility literally, or fix it by
always recomputing the pre-seed).

---

## 2. Ground-truth data (recovered custom-recipes.json)

### 2.1 Repo status and recovery method

MEASURED, today (2026-07-21): `https://github.com/gw2efficiency/custom-recipes`,
`https://raw.githubusercontent.com/gw2efficiency/custom-recipes/master/recipes.json`,
and `https://api.github.com/repos/gw2efficiency/custom-recipes` all return
HTTP 404 right now - the repo is genuinely gone from GitHub, confirming
the task's premise and `docs/gw2e-parity-spec.md`'s own July-2026 finding
(Section 3.1/"Known gaps").

Recovered via the Wayback Machine CDX API
(`web.archive.org/cdx/search/cdx?url=raw.githubusercontent.com/gw2efficiency/custom-recipes*`),
which lists snapshots keyed to several historical commit hashes. The most
complete/recent snapshot found is commit `38f18679ebec2900f6704029f58cac1c1d565f49`,
crawled **2026-02-20** (timestamp `20260220031318` for `recipes.json`,
fetched via
`https://web.archive.org/web/20260220031318if_/https://raw.githubusercontent.com/gw2efficiency/custom-recipes/38f18679ebec2900f6704029f58cac1c1d565f49/recipes.json`).
This is a genuine historical snapshot, not a live/current file - **it may
be stale relative to whatever was in the repo the moment before deletion**
(UNVERIFIED how close). The repo's root tree (also recovered via Wayback,
snapshot of the GitHub HTML page itself) additionally lists
`merchants.js`, `decorationRecipes.json`, `ignored-items.json`,
`ignored.json`, `generateDailyWeeklyCaps.js`, `package.json`,
`official-id-overwrite.txt` as sibling files - `merchants.js` was not
fetched (out of scope for this task; `docs/gw2e-parity-spec.md` Section
3.2 already independently confirmed the vendor-recipe mechanism from
`recipe-nesting`'s type definitions and the live app bundle, without
needing this file).

### 2.2 Overall shape

MEASURED (parsed the recovered `recipes.json` directly with Python):
- **8,962** total custom recipe entries in this snapshot.
- Every entry's shape is a flat `{name, output_item_id, output_item_count,
  ingredients: [{count, type, id, achievement_id?, achievement_bit?}, ...],
  disciplines: [...], achievement_id?}` object - i.e. exactly the
  `API_Recipes_Entry` shape `recipe-nesting`'s type definitions declare
  (Section 1.0), confirming these are meant to be concatenated with the
  official `/v2/recipes` output and fed to `nestRecipes` as one combined
  list. There is a `name` field (a human label, not part of the official
  API schema) that the real official-API recipe objects do not carry -
  this is custom-recipes-only metadata, presumably for the maintainers'
  own tooling.
- Distinct `disciplines` values seen across all 8,962 entries: `Mystic
  Forge`, `Growing`, `Salvage`, `Scribe`, `Achievement`, `Double Click`,
  `Handiworker`, `Charge`, `Huntsman`, `Weaponsmith`, `Merchant`,
  `Artificer`.

### 2.3 The "Achievement" discipline population

MEASURED: **283** entries have `disciplines: ["Achievement"]`, and every
one of those 283 also carries a recipe-level `achievement_id` (282 distinct
`achievement_id` values across the 283 recipes - one achievement_id is
reused by two recipes in this snapshot; not investigated further, out of
scope). This is the **broad** population the KNOWN-ISSUES text's "~274
achievement-discipline custom recipes" describes - my measured count (283,
snapshot dated 2026-02-20) is close to but not identical to the M34
session's cited figure (~274, as of ~2026-07-20); both are point-in-time
snapshots of a data source that was actively maintained (and has since
been deleted), so some drift between the two counts is expected and not a
discrepancy worth chasing further.

**Critical distinction the KNOWN-ISSUES phrasing blurs, and which this
report corrects**: recipe-level `achievement_id` (283 recipes) is a much
broader, different thing than ingredient-level `achievement_bit` (the
field the dedup mechanism in Section 1 actually keys on). Measured
directly:
- Recipes with a **recipe-level** `achievement_id` (marks the recipe
  itself as achievement-gated - e.g. "this Gift is only obtainable via a
  specific collection achievement"): **283**.
- Recipes with an **ingredient-level** `achievement_bit` on at least one
  ingredient (the only field the dedup mechanism actually reads): **7**.

Only those 7 recipes can ever exercise the dedup mechanism from Section 1
at all - the other 276 "Achievement"-discipline recipes behave as
completely ordinary multi-ingredient recipes as far as
`calculateTreeQuantity` is concerned; their component items are plain
`Item` nodes with no special dedup treatment, subject only to the
ordinary owned-stock/quantity math every other item gets.

**This means the KNOWN-ISSUES backlog's own example guess ("e.g. a
legendary with an achievement-gated collection component") does not
actually describe an item that exercises this specific dedup mechanism**,
per the currently-recoverable data - see Section 2.4/2.5 for what does,
and Section 4 for the corrected verification target.

### 2.4 The 276 recipe-level-only examples (illustrative, not dedup-relevant)

MEASURED, complete list of names sampled (first 40 of 283, for
orientation): `Gift of the Catalyst`, `Gift of the Raven Spirit`,
`Machined Torch`, `Machined Sword`, `Machined Rifle`, `Machined Shield`,
`Machined Axe`, `Machined Longbow`, `Machined Hammer`, `Machined Staff`,
`Machined Greatsword`, `Machined Warhorn`, `Machined Pistol`, `Machined
Dagger`, `The North Wind`, `Ydalir`, `Glint's Bastion`, `Fix-r-Upper`,
`Dark Harvest`, `Bo`, `Horologicus`, `Wild Abandon`, `Metabolic Primer`,
`Enchanted Treasure Chest`, `Ogre Sharpening Kit`, `Hylek Maintenance
Kit`, `Uncanny Jar`, `Metabolic Primer`, `Utility Primer`, `Big Bag of
Junk`, `Jormag Defender's Kit`, `Grawl Supply Sack`, `Glob of Ectoplasm`,
`Spirit Smith`, `Bag of Mentor's Supplies`, `Mini Candy Corn Ghoulemental`,
`Luminescent Boots Skin`, `Bandit Coin Purse`, `Luminescent Shoulders
Skin`, `Luminescent Gloves Skin`. `The North Wind` (output item 73037,
achievement_id 2418), `Ydalir` (69817, 2452), and `Glint's Bastion`
(75482, 2621) ARE genuine legendary-weapon achievement/collection recipes
(HoT/PoF precursor collections), each with 13 plain-`Item` ingredients and
**no** `achievement_bit` on any of them - confirming these do not touch
the dedup mechanism.

One concrete cross-recipe repeat WAS found among these: item id **46746**
("Vision Crystal", per `docs/gw2e-parity-spec.md` Section 3.3 - crafted
from Ascended materials, not vendor-sold) appears as the LAST ingredient
of both `Gift of the Catalyst` (achievement_id 2250, 60 ingredients) and
`Gift of the Raven Spirit` (achievement_id 2550, 60 ingredients) - but
neither occurrence carries `achievement_bit`, so this is an entirely
**ordinary** shared-ingredient case (handled by plain owned-stock/quantity
math, or by our own module's existing per-stepKey aggregation - see
Section 3), not an instance of the mechanism this task is about.

### 2.5 The 7 recipes that actually carry `achievement_bit` (complete, verbatim)

MEASURED - this is the **complete** set; there are no others in the
recovered snapshot. All seven are the WvW "Infinite [siege weapon]
Blueprint" achievement rewards, each requiring exactly 4 of the parent
achievement's "bits" as item ingredients:

```json
{
  "name": "Infinite Trebuchet Blueprint",
  "output_item_id": 103980,
  "output_item_count": 1,
  "ingredients": [
    { "count": 1, "type": "Item", "id": 103886, "achievement_id": 8493, "achievement_bit": 0 },
    { "count": 1, "type": "Item", "id": 103834, "achievement_id": 8493, "achievement_bit": 1 },
    { "count": 1, "type": "Item", "id": 103801, "achievement_id": 8493, "achievement_bit": 2 },
    { "count": 1, "type": "Item", "id": 103974, "achievement_id": 8493, "achievement_bit": 3 }
  ],
  "disciplines": ["Achievement"],
  "achievement_id": 8493
}
```
```json
{
  "name": "Infinite Siege Golem Blueprint",
  "output_item_id": 103911,
  "output_item_count": 1,
  "ingredients": [
    { "count": 1, "type": "Item", "id": 103864, "achievement_id": 8497, "achievement_bit": 0 },
    { "count": 1, "type": "Item", "id": 104012, "achievement_id": 8497, "achievement_bit": 1 },
    { "count": 1, "type": "Item", "id": 103809, "achievement_id": 8497, "achievement_bit": 3 },
    { "count": 1, "type": "Item", "id": 104059, "achievement_id": 8497, "achievement_bit": 4 }
  ],
  "disciplines": ["Achievement"],
  "achievement_id": 8497
}
```
```json
{
  "name": "Infinite Ballista Blueprint",
  "output_item_id": 103993,
  "output_item_count": 1,
  "ingredients": [
    { "count": 1, "type": "Item", "id": 103758, "achievement_id": 8512, "achievement_bit": 0 },
    { "count": 1, "type": "Item", "id": 103796, "achievement_id": 8512, "achievement_bit": 1 },
    { "count": 1, "type": "Item", "id": 103949, "achievement_id": 8512, "achievement_bit": 2 },
    { "count": 1, "type": "Item", "id": 103854, "achievement_id": 8512, "achievement_bit": 3 }
  ],
  "disciplines": ["Achievement"],
  "achievement_id": 8512
}
```
The remaining four (same shape, listed for completeness, not reproduced
verbatim to save space): `Infinite Shield Generator Blueprint`
(output 103915, achievement_id 8498, bit items 103826/103935/103762/103957,
bits 0/1/4/2), `Infinite Flame Ram Blueprint` (output 103878,
achievement_id 8481, bit items 103937/103845/103755/103919, bits 0/1/3/4),
`Infinite Catapult Blueprint` (output 103963, achievement_id 8441, bit
items 104036/103959/103860/103920, bits 0/1/3/4), `Infinite Arrow Cart
Blueprint` (output 103995, achievement_id 8465, bit items
103851/103894/103757/103788, bits 0/1/3/2).

**No bit-item id repeats across any of these 7 recipes** (all 28
achievement_bit ingredient occurrences reference 28 distinct item ids -
checked programmatically), and **none of these 28 ids appear as a plain
(non-`achievement_bit`) ingredient anywhere else** in the full 8,962-entry
custom-recipes snapshot (checked programmatically) - so, on this data
alone, no single one of these 7 achievement recipes, solved by itself,
ever triggers the zeroing rule (nothing repeats). Section 4 explains what
minimal, still real/verified addition WOULD trigger it.

Each of the 4 bit items per blueprint DOES have its own separate
acquisition recipe within the same custom-recipes snapshot - a
`disciplines: ["Merchant"]` (WvW vendor purchase) recipe, e.g.:
```json
{
  "name": "Pile of Recycled Trebuchets", "output_item_id": 103886, "output_item_count": 1,
  "ingredients": [{ "count": 100, "type": "Item", "id": 103913 }],
  "disciplines": ["Merchant"]
}
{
  "name": "Trebuchet Mechanism", "output_item_id": 103834, "output_item_count": 1,
  "ingredients": [
    { "count": 25, "type": "Item", "id": 87557 },
    { "count": 1000, "type": "Currency", "id": 65 },
    { "count": 3000, "type": "Currency", "id": 26 },
    { "count": 5000, "type": "Currency", "id": 15 }
  ],
  "disciplines": ["Merchant"]
}
{
  "name": "Box of Scavenged Siege Parts (trebuchet)", "output_item_id": 103974, "output_item_count": 1,
  "ingredients": [
    { "count": 3, "type": "Item", "id": 93146 },
    { "count": 5, "type": "Item", "id": 19678 },
    { "count": 100, "type": "Item", "id": 93075 }
  ],
  "disciplines": ["Merchant"]
}
```
(the 3rd bit, item 103801 "Proof of Siege Expertise", has **no** recipe of
any kind in the recovered custom-recipes snapshot at all - see 2.6.)

### 2.6 Official-API cross-check (MEASURED, 2026-07-21, `api.guildwars2.com/v2`)

All items and the parent achievement for the Trebuchet example verified
directly against the live official API (individual `/v2/items/{id}`
calls; a single batched call for all 5 ids returned `{"text":"no such
id"}` for unclear reasons and was abandoned in favor of individual calls,
which all succeeded):

| Id | Type | Name (official API, `lang=en`) |
|---|---|---|
| 8493 | Achievement | "Infinite Trebuchet" (reward: 1x item 103980) |
| 103980 | Item | Infinite Trebuchet Blueprint |
| 103886 | Item | Pile of Recycled Trebuchets |
| 103834 | Item | Trebuchet Mechanism |
| 103801 | Item | Proof of Siege Expertise |
| 103974 | Item | Box of Scavenged Trebuchet Parts |

`GET /v2/achievements/8493` returns a `bits` array of 6 text-only entries
(no per-bit item ids in the official schema itself - the official API
never exposes "this bit corresponds to this item", which is exactly why
gw2efficiency has to hand-curate that mapping in custom-recipes):
```
0: "Purchase a trebuchet token from the skirmish supervisor."
1: "Purchase a trebuchet mechanism from the skirmish supervisor."
2: "Complete the associated siege expertise achievement."
3: "Purchase trebuchet parts from the blue catmander in the Alpine Borderlands."
4: "Salvage parts from the broken down trebuchet on the third floor of Stonemist Castle."
5: "Visit Canis in Alpine Borderlands and bow to him."
```
and `rewards: [{"type": "Item", "id": 103980, "count": 1}]` - matching the
custom recipe's `output_item_id`/`achievement_id` exactly. The custom
recipe's `achievement_bit` values (0, 1, 2, 3) line up with bit indices
0, 1, 2, 3 above (bits 4 and 5 have no corresponding purchasable/craftable
item at all and are correctly absent from the recipe's ingredient list).
Currency names (`/v2/currencies/{id}`, individual calls, `lang=en`):
`65` = Testimony of Jade Heroics, `26` = WvW Skirmish Claim Ticket, `15` =
Badge of Honor. Item names: `87557` = Grandmaster Mark Shard, `93146` =
Emblem of the Conqueror, `19678` = Gift of Battle, `93075` = Emblem of the
Avenger, `103913` = Pile of Recycled Siege Equipment.

This item/achievement data is real, self-consistent, and independently
confirmed against the live official API - it is safe to use as a seed
source if the module ever adds achievement-recipe support (Section 4).

---

## 3. Current module state (actually read)

### 3.1 Seed data - zero achievement analog anywhere

MEASURED (`ref/` directory, `/mnt/c/Dev/Blish/GW2CraftingHelper/ref/`):
`acquisition_hints_seed.json`, `item_id_cache.json`, `item_name_seed.json`,
`mf_item_id_cache.json`, `mystic_forge_recipes.json`,
`recipe_search_seed.json`, `recipe_seed_manifest.json`,
`recipes_seed.json` (7.4 MB, **14,732** recipe entries, `schemaVersion: 1`),
`vendor_offers.json`, `wiki_vendor_cache.json`.

`grep -ril "achievement" --include="*.cs" --include="*.json" .` across the
entire repo (excluding `obj`/`bin`) returns **zero hits**. Parsed every
`recipes_seed.json` entry's `ingredients[].type` and `disciplines[]`
programmatically:
- Ingredient `type` values present: `{"Item"}` only - **no `Currency`
  ingredient type exists anywhere in our seed data at all** (a
  pre-existing gap noted in passing; not this task's scope, but relevant
  context since `achievement_bit` dedup explicitly special-cases
  `Currency` nodes upstream and we have none to special-case).
- `disciplines` values present: `Weaponsmith`, `Scribe`, `Leatherworker`,
  `Tailor`, `Huntsman`, `Armorsmith`, `Artificer`, `Homesteader`,
  `Jeweler`, `Chef`, `MysticForge` - **no `Achievement` or `Merchant`
  discipline value** (vendor purchases are modeled separately, via
  `ref/vendor_offers.json` + `VendorOffer`/`PlanSolver.EvaluateVendorOffers`,
  not as `disciplines: ["Merchant"]` recipes the way gw2e does it - see
  `docs/gw2e-parity-spec.md` Section 3, an already-documented, pre-existing
  architectural divergence, not something this task needs to change).
- Directly checked: none of the 7 achievement-bit item ids, none of the 7
  Blueprint output ids, and no achievement id from Section 2.5 appears
  anywhere in `recipes_seed.json` as either an `outputItemId` or an
  ingredient `id` - **completely absent**, confirming there is genuinely
  nothing in our current data that could exercise this behavior even by
  accident.

### 3.2 In-memory model shapes - no field exists to carry `achievement_bit` at all

MEASURED (read in full):
- `Services/IRecipeApiClient.cs` - `RawIngredient { Type, Id, Count }` and
  `RawRecipe { Id, OutputItemId, OutputItemCount, ExpectedOutputCount?,
  Ingredients, Disciplines, MinRating, Flags }`. No achievement field of
  any kind.
- `Models/RecipeNode.cs` - `{ Id, IngredientType, Quantity, NodeId, Recipes
  }`. No achievement field.
- `Models/RecipeOption.cs` - `{ RecipeId, OutputCount, CraftsNeeded,
  ExpectedOutputCount, Ingredients, Disciplines, MinRating, Flags }`. No
  achievement field.
- `Contracts/CraftingDecision.cs` - enum `{ Craft, BuyFromTp, BuyFromVendor,
  Have, Currency, Unknown }`. No achievement-related value.
- `Contracts/CraftingTreeNode.cs` - has an `IsIgnored` bool (M34-B2b, the
  "Ignore pill" feature) that is the closest existing precedent for what
  an achievement-bit-dedup flag would look like (see Section 4), but no
  achievement field itself.

### 3.3 Tree-build path (`Services/RecipeService.cs`) - the recipe-nesting analog

MEASURED (read `BuildNodeAsync`, lines 276-369, in full).
`RecipeService.BuildNodeAsync` is our module's direct structural analog of
gw2e's `nestRecipes`: for each `raw.Ingredients` entry it computes
`ingredientQuantity = craftsNeeded * ingredient.Count` and recurses into
`BuildNodeAsync(ingredient.Id, ingredient.Type, ingredientQuantity, ...)`,
producing a `RecipeNode` whose `Quantity` is **already the final, absolute
integer demand for that specific tree occurrence** - baked in once, at
build time. This is an important architectural difference from gw2e's own
design: gw2e's nested tree stores a small per-edge `quantity` (the
recipe's own per-craft ratio) and defers ALL absolute-quantity resolution
(`totalQuantity`/`usedQuantity`) to the separate `calculateTreeQuantity`
pass, which is *itself* where the achievement_bit dedup lives. Our module
has no equivalent separate "resolve quantities" pass at all - by the time
`PlanSolver.Solve` ever sees the tree, every node's absolute `Quantity` is
already fixed, and `PlanSolver.Evaluate`/`Collect` work forward from that
fixed number rather than deriving it from a smaller per-edge ratio. This
does not block echoing the behavior (Section 4 below still works), but it
means the natural insertion point in OUR architecture is a
**pre-Solve tree pass that zeroes `RecipeNode.Quantity` directly** (the
value PlanSolver already treats as authoritative), not a
"calculateTreeQuantity"-style function that recomputes quantities from
smaller ratios the way gw2e's does.

### 3.4 `PlanSolver.cs`, `CraftingTreeBuilder.cs` - existing (correct) ordinary dedup, and the exact precedent pattern to reuse

MEASURED (read both files in full/near-full - `PlanSolver.cs` is 1,544
lines; read to line 1236 plus the `Collect`/`AggregateStep` sections
covering the relevant logic).

- **Ordinary cross-branch aggregation already exists and is correct**:
  `PlanSolver.Collect`/`AggregateStep` key every step by `(ItemId, Source,
  RecipeId)` and **sum `Quantity` across every tree occurrence** that
  resolves to the same key (`existing.Quantity += node.Quantity`), then
  (via `FinalizeVendorBatches`) compute bulk-offer costs once against that
  aggregate, ceiling exactly once - explicitly gw2e-parity-motivated (the
  M34-B1 code comments cite `docs/gw2e-parity-spec.md` Section 6.5's
  "quantities are merged across the whole tree before `Math.ceil` ever
  runs"). This is functionally analogous to gw2e's own `craftingSteps.ts`
  merge-by-id step (Section 1 above, "Real duplicate items are
  unaffected" in the ground-truth test) - **our module already gets the
  ordinary (non-achievement) case right.** What's entirely missing is the
  achievement-bit-specific *exception* to this: for an achievement-bit
  item, occurrences must NOT sum - only one occurrence may ever be
  "real," the rest must be zero, tree-wide.
- **`CraftingTreeBuilder.BuildNode`** already has the exact display
  behavior a deduped node needs, for free: `if (node.Quantity == 0) {
  treeNode.Decision = CraftingDecision.Have; return treeNode; }` runs
  before any decision lookup, currency check, or recipe recursion. Zeroing
  `RecipeNode.Quantity` on a duplicate achievement-bit occurrence, before
  `PlanSolver.Solve` ever runs, would make it collapse to `Have` (free,
  no crafting step, no recursion into its own ingredients) with **zero**
  additional changes to `PlanSolver` or `CraftingTreeBuilder` at all.
- **The exact precedent pattern for "a pre-pass computes a NodeId set,
  fed into the pipeline right after `RecipeNodeIds.Assign`, right before
  `Solve`" already exists**: `Services/OwnedMaterialsForceBuyPrePass.cs`
  (`ComputeForceBuyOnlyNodeIds`), wired in
  `Services/CraftingPlanPipeline.cs` immediately after
  `RecipeNodeIds.Assign(tree)` and passed into `_solver.Solve(...,
  forceBuyOnlyNodeIds: ...)`. This is the natural, already-established
  seam for a new `AchievementBitDedupPrePass`-style class (Section 4) -
  it requires no change to `PlanSolver`'s signature at all if the pre-pass
  mutates `RecipeNode.Quantity` directly (per the point above) rather than
  threading a new set through `Solve`.
- **`IsIgnored`** (`CraftingTreeNode.IsIgnored`, wired through
  `DecisionPillPlanner.BuildPillSpecs` and `Views/CraftingPlanView.cs`) is
  the exact precedent for how to give a zeroed-for-a-different-reason node
  its own distinguishing pill instead of the plain silent HAVE a
  genuinely-owned node gets - see `CraftingTreeBuilder.cs` lines 58-71 and
  `DecisionPillPlanner.cs` lines 68-82 (quoted/discussed in Section 4).

---

## 4. Recommended echo design (Small, per the backlog)

Sized to the backlog's explicit "Small" scope: this is a self-contained,
low-risk addition (new nullable/optional fields defaulting to absent, one
new pure pre-pass class, one new boolean display flag) that touches zero
existing recipe data (all 14,732 current seed entries are unaffected) and
does not change `PlanSolver`'s public signature.

### 4.1 Seed schema (additive only)

Add two nullable ingredient-level fields, mirroring gw2e's own
`achievement_id`/`achievement_bit` naming exactly (for anyone
cross-referencing this report or upstream source later) to
`Services/IRecipeApiClient.cs`:
```csharp
public class RawIngredient
{
    public string Type { get; set; }
    public int Id { get; set; }
    public int Count { get; set; }

    // New, optional (JSON-absent = null = ordinary ingredient, matching
    // every existing seed entry unchanged).
    public int? AchievementId { get; set; }
    public int? AchievementBit { get; set; }
}
```
`RawRecipe` itself does not need a recipe-level `AchievementId` for THIS
feature (that field only marks the recipe as achievement-gated for
display/informational purposes upstream - Section 2.3 - and is not read
by the dedup mechanism at all); adding it is optional future scope, not
required here. JSON shape in `ref/recipes_seed.json` stays 100% backward
compatible - a seed entry simply omits `achievementId`/`achievementBit`
unless it needs them.

Propagate the same two nullable fields onto `Models/RecipeNode.cs` (set
once, in `RecipeService.BuildNodeAsync`, from the matching
`RawIngredient`, alongside `IngredientType`/`Quantity`):
```csharp
public int? AchievementId { get; set; }
public int? AchievementBit { get; set; }
```

### 4.2 A new, pure, Blish-free pre-pass class (mirrors `OwnedMaterialsForceBuyPrePass`'s seam exactly)

`Services/AchievementBitDedupPrePass.cs` (name illustrative), a static
pure-tree-transform, unit-testable in total isolation:
```csharp
public static class AchievementBitDedupPrePass
{
    // Walks the WHOLE tree once (mirrors initialTreeChecks +
    // collectItemDataForIgnoringBits): classify each non-Currency node's
    // Id into bitIds (AchievementBit.HasValue) or normalIds (else).
    // Then walk again in the SAME DFS order PlanSolver.Evaluate will use
    // (node.Recipes[i].Ingredients, in order) and, for every node whose
    // own AchievementBit.HasValue, zero its Quantity in place (mutating
    // the RecipeNode directly - the same field CraftingTreeBuilder
    // already treats as authoritative for the Have collapse) unless this
    // is the FIRST time its Id has been seen as a bit occurrence AND its
    // Id was not pre-seeded (i.e. it has no normal occurrence anywhere in
    // the tree). Zeroing also does not recurse into the zeroed node's own
    // Recipes - exactly like the existing ignoredItemIds short-circuit in
    // PlanSolver.Evaluate/Collect (M34-B2b) already does for a different
    // reason - a zeroed subtree draws no demand from its own children.
    public static void Apply(RecipeNode tree) { ... }
}
```
Wired in `Services/CraftingPlanPipeline.cs` immediately after
`RecipeNodeIds.Assign(tree)` and before `_solver.Solve(...)` (both call
sites currently at lines ~305/~657) - a single new line, no `Solve`
signature change. Because it mutates `Quantity` directly on the same tree
`PlanSolver`/`CraftingTreeBuilder` already consume, it needs **zero**
changes to `PlanSolver.cs`, `CraftingTreeBuilder.cs`'s cost/decision logic,
or the multi-item wrapper path (`Gw2Constants.MultiItemWrapperItemId`) -
the wrapper's own synthetic root is never itself achievement-bit-tagged,
and the pre-pass naturally walks straight through it into the real item
roots, exactly like `CollectTreeItemIds` already does today.

**Open design choice** (Section 1.5): should the pre-pass also re-run on
every local re-solve (override/ignore-pill clicks,
`CraftingPlanPipeline`'s `ReSolve`-style path), or only once at tree-build
time? gw2e's own `updateTree` (used for exactly those interactions) does
NOT re-run the equivalent of this pre-pass, and (per Section 1.5) this
looks like an upstream fragility rather than an intentional design. Given
our module's re-solve path already re-uses the SAME tree object
(`RecipeNode.Quantity` mutations from this pre-pass persist on the tree
across re-solves, unlike gw2e's immutable-tree-rebuild-per-call style), the
simplest and most ROBUST choice for our architecture is to run the
pre-pass exactly ONCE, right after the tree is built and NodeIds are
assigned, and never again - the zeroed `Quantity` values simply stay
zeroed for the rest of that tree's lifetime, which is strictly SAFER than
gw2e's own behavior (never silently un-dedupes), not a parity gap. Flagging
this explicitly as a deliberate, justified departure from literal
bug-for-bug upstream parity, not an oversight.

### 4.3 Display: a new boolean flag mirroring `IsIgnored` exactly

`Contracts/CraftingTreeNode.cs`:
```csharp
// True when this node's own achievement_bit-style ingredient is already
// being satisfied by an earlier occurrence of the same item id elsewhere
// in this tree (see AchievementBitDedupPrePass) - Decision collapses to
// Have, same as IsIgnored, but the pill layer shows a distinct,
// non-clickable annotation instead of the plain HAVE pill a genuinely
// owned node gets, since nothing is actually owned here.
public bool IsAchievementBitDeduped { get; set; }
```
`CraftingTreeBuilder.BuildNode`'s existing `Quantity == 0` early return
already sets `Decision = Have`; add one more `IsAchievementBitDeduped =
true` assignment guarded on `node.AchievementBit.HasValue`, in the exact
same place the existing `IsIgnored` assignment lives (lines 58-71) - same
shape, new boolean, no new Decision enum value needed.

`Services/DecisionPillPlanner.BuildPillSpecs`: add one more `if
(node.IsAchievementBitDeduped)` arm parallel to the existing
`if (node.IsIgnored)` arm (lines 77-80), producing a single non-interactive
pill (no `Ignore`-style toggle - there is nothing for the user to
undo/override here, unlike the Ignore pill) with text along the lines of
`"COUNTED ELSEWHERE"` or `"ALREADY REQUIRED"` (exact copy is a UI-wording
decision, not a data/algorithm one - out of this report's scope; whatever
is chosen must not be confused with the plain "HAVE" a genuinely-owned
node gets, since the acquisition story here is completely different: this
item still needs to be obtained once, just not twice).

### 4.4 No new Settings toggle needed

Unlike item #24 (homestead refinement tiers, which needs a manual setting
because the API can't tell us the user's unlock state) or item #25
(multi-item sell-side economics), this mechanism is **not** user-facing
policy - it is a pure correctness fix for how demand is counted, with a
single unambiguous correct behavior (matching upstream exactly, modulo the
4.2 robustness note). No settings surface is warranted, matching the
backlog's "Small" sizing.

### 4.5 Test plan

1. **Unit tests for `AchievementBitDedupPrePass` in isolation** (pure
   `RecipeNode` trees, no Blish, no I/O, no `RecipeService`) - port the
   exact gw2e ground-truth scenarios from Section 1.4 1:1, since they are
   now independently-confirmed, authoritative expected values:
   - An id with BOTH an achievement-bit occurrence and a normal occurrence
     elsewhere in the tree -> the achievement-bit occurrence(s) become
     `Quantity == 0`; the normal occurrence is untouched.
   - The same achievement-bit id occurring twice with NO normal occurrence
     anywhere -> the first occurrence (DFS order) keeps its quantity, the
     second becomes `Quantity == 0`.
   - An ordinary (no achievement fields at all) id occurring twice ->
     neither occurrence is touched by this pre-pass (still each node's own
     independent quantity; ordinary aggregation is `PlanSolver`'s job, not
     this pre-pass's).
   - A zeroed achievement-bit node that itself has sub-ingredients (a
     `Recipe`-shaped node, not a bare leaf) -> its own children also end
     up `Quantity == 0` (cascades down, mirroring 1.2's "the whole
     duplicate subtree collapses").
2. **`CraftingTreeBuilder` test**: a deduped `RecipeNode` (Quantity=0,
   AchievementBit.HasValue) builds a `CraftingTreeNode` with `Decision ==
   Have` and `IsAchievementBitDeduped == true` (and, for symmetry, a
   genuinely-owned zero-quantity node with no AchievementBit still gets
   `IsAchievementBitDeduped == false`).
3. **`DecisionPillPlanner` test**: the new pill spec appears exactly when
   `IsAchievementBitDeduped` is true, is non-interactive (`Source ==
   null`), and does not appear for the ordinary `IsIgnored`/plain-`Have`
   cases.
4. **One end-to-end `PlanSolver`/`CraftingTreeBuilder` integration test**
   using the concrete, wiki/API-verified seed data from Section 4.6 below
   (real production code paths throughout, per repo invariant - no
   fake/mirrored logic) - construct exactly the multi-item scenario in
   4.6, solve it, and assert the shared bit item's cost/quantity is
   counted once, not twice.

### 4.6 Concrete verification target (real, verified data - not invented)

Per Section 2.5, **no single existing achievement recipe, on its own,
exercises this mechanism** - none of the 7 real `achievement_bit` recipes
share a bit id with each other or with any plain ingredient elsewhere in
the currently-recoverable gw2efficiency data. The KNOWN-ISSUES backlog's
own example guess (a legendary precursor collection) is not correct for
this specific mechanism per the recovered data (Section 2.3/2.4) - those
recipes use recipe-level `achievement_id` only, never ingredient-level
`achievement_bit`.

The smallest **real, wiki/API-verified** addition that WOULD exercise the
mechanism, using only data already confirmed in Section 2.5/2.6 (nothing
invented):
1. Seed the `Infinite Trebuchet Blueprint` achievement recipe (output item
   103980, 4 ingredients as quoted in Section 2.5) plus its 3
   Merchant-discipline sub-recipes for items 103886/103834/103974 (Section
   2.5's quoted JSON) - item 103801 ("Proof of Siege Expertise") correctly
   gets no recipe at all (Section 2.6 confirms no acquisition path exists
   for it beyond the achievement itself - it should render `Unknown`/"Not
   sold or crafted", which is CORRECT behavior, not a gap).
2. Build a **multi-item plan** (our module's existing
   `BuildMultiItemTreeAsync`/`Gw2Constants.MultiItemWrapperItemId`
   mechanism, M35) selecting BOTH `Infinite Trebuchet Blueprint` (x1) AND
   `Pile of Recycled Trebuchets` (item 103886, x1) directly as a second
   target - i.e. the user separately wants 1 more "Pile of Recycled
   Trebuchets" for some other reason, on top of the one already needed as
   bit 0 of the Blueprint.
3. **Expected before this fix (current module behavior, if this data were
   seeded today without the fix)**: the plan would show TWO separate
   demands for item 103886 - one via the achievement-bit ingredient edge,
   one via the direct multi-item root - and (per `PlanSolver.AggregateStep`'s
   existing ordinary per-stepKey summing, Section 3.4) they would very
   likely be SUMMED into a single merged step demanding **2** total (or,
   if the achievement-bit occurrence happened to resolve to a different
   NodeId/decision path, shown as two separate line items) - either way,
   the plan would ask the user to buy/vendor-purchase 2 units and spend 2x
   the Badges of Honor/coin cost, when only 1 additional unit is actually
   needed (the Blueprint's own bit-0 requirement is satisfied for free by
   the direct purchase elsewhere in the same plan - or vice versa,
   depending on which occurrence "wins" the aggregation).
4. **Expected after this fix**: the achievement-bit occurrence (bit 0 of
   the Blueprint) is zeroed (Decision=Have, IsAchievementBitDeduped=true,
   no crafting step, no shopping-list row, no coin cost) because item
   103886 also has a normal (non-bit) occurrence elsewhere in the same
   tree (the directly-selected second target); the direct-purchase root
   keeps its own full, un-deduped demand of 1. Total plan cost for item
   103886 = exactly 1 unit's worth, not 2.

This is a real, wiki/API-verified scenario constructed entirely from
Section 2.5/2.6's confirmed data - it is NOT a fabricated example, but it
IS a constructed test scenario (the "second target" half is a deliberate
choice to make the bug reproducible, not itself independently notable
content). If a genuinely single-item, no-multi-item-plan-needed repro is
required instead, none currently exists in the recoverable gw2efficiency
data - stating this explicitly rather than inventing one, per the task's
hard rule against invented data.

---

## 5. Sources

- `docs/gw2e-parity-spec.md` (this repo, read in full) - normative parity
  spec; no existing mention of achievement_bit/ignoredBitItemIds.
- `docs/KNOWN-ISSUES.md` lines 1127-1269 (M37 backlog, item 26's exact
  wording) and lines 140-1126 (M33 backlog context), this repo.
- `github.com/gw2efficiency/recipe-calculation` @ `master` (fetched via
  `raw.githubusercontent.com`, 2026-07-21): `src/calculateTreeQuantity.ts`,
  `src/cheapestTree.ts`, `src/calculateTreePrices.ts`,
  `src/calculateTreeCraftFlags.ts`, `src/updateTree.ts`, `src/types.ts`,
  `src/craftingSteps.ts`, `tests/calculateTreeQuantity.spec.ts`,
  `README.md`.
- `github.com/gw2efficiency/recipe-nesting` @ `master` (fetched via
  `raw.githubusercontent.com`, 2026-07-21): `src/index.ts`, `src/api.d.ts`.
- `api.github.com/repos/gw2efficiency/recipe-calculation/commits?path=src/calculateTreeQuantity.ts`
  and `.../commits/{sha}` for commits `b9e0346b` ("Put quantity 0 for bits
  that are fulfilled elsewhere") and `d9586270` ("Better clarity with
  namings, added a quick test with some scenarios") - both 2026-02.
- `github.com/gw2efficiency/custom-recipes` - confirmed 404/deleted,
  2026-07-21 (direct `curl` to `github.com`, `raw.githubusercontent.com`,
  and `api.github.com`).
- Wayback Machine (`web.archive.org`): CDX API search for
  `raw.githubusercontent.com/gw2efficiency/custom-recipes*`; recovered
  `recipes.json` @ commit `38f18679ebec2900f6704029f58cac1c1d565f49`,
  crawled 2026-02-20 (`web.archive.org/web/20260220031318if_/...`); repo
  root tree HTML @ `web.archive.org/web/20260301081601/...`.
- `api.guildwars2.com/v2/items/{id}`, `/v2/achievements/8493`,
  `/v2/currencies/{id}` (official API, `lang=en`, fetched 2026-07-21) -
  cross-verification for Section 2.6.
- WebSearch: "gw2efficiency recipe-calculation ignoredBitItemIds
  achievement" surfaced `github.com/gw2efficiency/issues/issues/2099`
  ("Streamline adding achievements related items into recipes and improve
  recipes", closed not-planned, body inaccessible via WebFetch - GitHub
  rendered an error page for that specific issue) and
  `github.com/gw2efficiency/issues/issues/104` ("Include achievement
  progress in the crafting calculator") - neither added information beyond
  what the source code itself already confirmed; not relied upon for any
  claim in this report.
- This repo's own source, read directly: `Services/RecipeService.cs`,
  `Services/PlanSolver.cs` (lines 1-1236 read in full; `Collect`/
  `AggregateStep` sections covering the relevant logic),
  `Services/CraftingTreeBuilder.cs`, `Services/CraftingPlanPipeline.cs`
  (grepped for `BuildTreeAsync`/`RecipeNodeIds.Assign`/`Solve(`/
  `ignoredItemIds`/`forceBuyOnlyNodeIds`), `Services/DecisionPillPlanner.cs`
  (lines 1-95), `Services/IRecipeApiClient.cs`, `Models/RecipeNode.cs`,
  `Models/RecipeOption.cs`, `Contracts/CraftingDecision.cs`,
  `Contracts/CraftingTreeNode.cs`, `ref/recipes_seed.json` (parsed
  programmatically, all 14,732 entries).

---

## 6. Open questions

1. **How stale is the recovered `custom-recipes.json` snapshot (2026-02-20)
   relative to the live repo's state right before its deletion (some point
   before 2026-07-21)?** UNVERIFIED - no way to check without a snapshot
   closer to the actual deletion date, which the Wayback CDX search did
   not surface (the latest snapshot found for `recipes.json` was
   2026-02-20; no later one exists in the CDX index at the time of this
   research). Practical impact: the 7 achievement_bit recipes and their
   ids are almost certainly still accurate (WvW siege blueprints are old,
   stable content), but a seeder should re-verify item/achievement ids
   against the live official API before shipping (which Section 2.6
   already partly did for the Trebuchet example).
2. **Does the live gw2efficiency calculator actually exhibit the
   Section 1.5 fragility (a manual pill click/amount change un-doing a
   "shared with a normal occurrence" dedup) in practice?** INFERRED from
   reading `updateTree.ts`, not reproduced live in the running app (would
   require driving the actual calculator UI with a crafted multi-item
   scenario - out of scope for a source-code-level research task, and
   the module never calls gw2efficiency at runtime regardless).
3. **Are there other achievement-bit-bearing custom recipes in the
   `merchants.js` or `decorationRecipes.json` sibling files** (not
   fetched - only `recipes.json` was pulled)? UNVERIFIED. Given
   `recipe-nesting`'s type definitions put `achievement_bit` only on
   `ingredients[]` (any recipe type), and `merchants.js`/
   `decorationRecipes.json` are almost certainly narrower, specialized
   data files (vendor identities and Homestead-decoration recipes
   respectively, per their names and `docs/gw2e-parity-spec.md` Section
   3's independent findings), this is INFERRED to be unlikely to matter,
   but not directly checked.
4. **Exact wording for the new pill label** (Section 4.3) - a UI-copy
   decision the report deliberately leaves open rather than picking
   arbitrary text under the banner of "verified data only."
5. **Should `RawRecipe.AchievementId` (recipe-level) also be added now**,
   even though the dedup mechanism doesn't need it, purely so a future,
   separate task (surfacing "this recipe/collection is achievement-gated"
   informationally, matching gw2e's broader 283-recipe population from
   Section 2.3) doesn't need a second schema migration? Left as a judgment
   call for the implementing session - Section 4.1 intentionally scoped
   the seed-schema change to the minimum this specific task needs.

---

## Verification

An independent verifier re-checked this report against primary sources
(re-fetching upstream source files, the ground-truth test, the recovered
Wayback snapshot via a direct `curl` of the exact same URL cited in
Section 2.1, the live official GW2 API, and this repo's own source) and
confirmed it holds up with one isolated correction.

### Correction applied

- **Section 2.5, `Infinite Shield Generator Blueprint`**: the bit-item/
  bit-value pairing for the last two ingredients was transposed in the
  original text (it read `bit items 103826/103935/103957/103762, bits
  0/1/4/2`, which pairs `103957->4` and `103762->2`). The verifier
  independently re-fetched the exact same Wayback Machine URL cited in
  Section 2.1 via `curl` and parsed the entry programmatically; the
  actual ingredients array is `[{103826, bit 0}, {103935, bit 1},
  {103762, bit 4}, {103957, bit 2}]`. The line has been corrected in
  place above to `bit items 103826/103935/103762/103957, bits 0/1/4/2`,
  which now correctly pairs `103762->4` and `103957->2`. This was an
  isolated transcription slip in the "for completeness, not reproduced
  verbatim" summary list (Section 2.5) - the three fully-reproduced
  verbatim JSON blocks earlier in that same section (Trebuchet, Siege
  Golem, Ballista) were independently re-checked and are byte-for-byte
  correct, and the other three "remaining four" entries (Flame Ram,
  Catapult, Arrow Cart) were also independently re-verified and are
  correct as originally stated - only the Shield Generator pairing was
  wrong.

### Independently cross-checked and confirmed accurate (no changes needed)

- **Section 1 (upstream mechanism)**: `calculateTreeQuantity.ts`,
  `cheapestTree.ts` (`initialTreeChecks`/
  `collectItemDataForIgnoringBits`), `calculateTreePrices.ts` and
  `calculateTreeCraftFlags.ts` (confirmed to have zero `achievement_bit`
  references), `updateTree.ts` (confirmed to omit `ignoredBitItemIds`,
  defaulting to `[]`), and `recipe-nesting`'s `src/index.ts`/`api.d.ts`
  were all re-fetched directly from `raw.githubusercontent.com/master`
  and match this report's quotes byte-for-byte (modulo this report's own
  `...` elisions). The ground-truth test
  `tests/calculateTreeQuantity.spec.ts` ("handles achievement bit items
  correctly") was fetched in full and every asserted value in the
  Section 1.4 table (ids 55/56/999, `totalQuantity` 0/0/2/1/0/1/3)
  matches exactly. Commit hashes `b9e0346b` and `d9586270` were
  confirmed via the GitHub commits API against the exact SHA prefixes,
  messages, and 2026-02 dates cited.
- **Section 2 (recovered custom-recipes data)**: the exact Wayback
  snapshot URL was independently re-fetched via `curl` (WebFetch itself
  refused `web.archive.org`, but `curl` succeeded) and every quantitative
  figure was reproduced exactly: 8,962 total entries, 283 entries with
  "Achievement" in `disciplines` (with the same caveat this report
  already notes - 9 of the 283 pair Achievement with another discipline
  like "Double Click"), 282 distinct `achievement_id` values among those
  283 with exactly one reuse (`achievement_id` 1750, "Glob of
  Ectoplasm" x2), exactly 7 recipes carrying ingredient-level
  `achievement_bit` (all 7 names/achievement_ids/output_item_ids
  confirmed), all 28 bit-ingredient occurrences confirmed distinct with
  zero appearing as a plain ingredient elsewhere in the 8,962-entry
  snapshot, the three quoted Merchant sub-recipe JSON blocks (items
  103886/103834/103974, byte-for-byte match), and zero recipes for item
  103801. The North Wind/Ydalir/Glint's Bastion and Vision Crystal
  (46746) shared-ingredient claims in Section 2.4 were also confirmed.
- **Official GW2 API cross-checks (Section 2.6)**: `/v2/achievements/8493`
  and the item names for 103980/103886/103834/103801/103974 were
  re-fetched live and match exactly.
  `api.github.com/repos/gw2efficiency/custom-recipes` correctly 404s.
- **Local repo claims (Section 3)**: every cited file was read directly
  and matches this report's description exactly, including the
  `RawIngredient`/`RawRecipe`, `RecipeNode`, `RecipeOption`,
  `CraftingDecision`, and `CraftingTreeNode.IsIgnored` shapes, the
  `BuildNodeAsync` quantity-baking behavior, the cited line ranges for
  the `IsIgnored` precedent, the `CraftingPlanPipeline` wiring line
  numbers, the `AggregateStep` summing behavior, and
  `Gw2Constants.MultiItemWrapperItemId`. The repo-wide `achievement` grep
  (zero hits) and the full `ref/recipes_seed.json` parse (14,732 entries,
  `schemaVersion` 1, `Item`-only ingredient types, the 11-discipline set,
  zero `achievement` substrings, zero matches for any of the 7 verified
  bit items) were both reproduced.
- **Internal consistency**: Sections 2, 3, and 4 correctly build on the
  Section 1 mechanism, and the MEASURED/OBSERVED/INFERRED/UNVERIFIED
  labels throughout are used correctly and match what was actually
  checkable - notably the Section 1.5 `updateTree.ts` fragility claim is
  correctly labeled INFERRED rather than overclaimed as MEASURED.

### Remaining uncertainty (unchanged from Section 6)

The correction above does not affect any of the open questions already
disclosed in Section 6, which stand as originally written: the staleness
of the 2026-02-20 Wayback snapshot relative to the repo's actual deletion
date remains UNVERIFIED; whether the live gw2efficiency calculator
actually exhibits the Section 1.5 `updateTree.ts` fragility in practice
remains INFERRED, not reproduced live; whether `merchants.js`/
`decorationRecipes.json` contain additional achievement-bit recipes
remains UNVERIFIED (not fetched); the exact pill-label wording (Section
4.3) remains an open UI-copy decision; and whether to add
`RawRecipe.AchievementId` now (Section 4.1) remains a judgment call for
the implementing session. None of these bear on the correction applied
above, and none of the report's five core claims are affected by it.
