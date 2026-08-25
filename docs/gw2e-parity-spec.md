# GW2 Efficiency Parity Spec

## Provenance

Researched 2026-07-20 at dev time from gw2efficiency's open-source
`recipe-calculation` and `recipe-nesting` libraries, plus its live calculator
frontend (Angular templates and JS bundle, fetched read-only for UI-behavior
confirmation). This document is a normative behavioral spec that this
module's crafting logic echoes for parity purposes -- the module itself
never calls gw2efficiency at runtime; gw2efficiency is research-only, per
project rule.

---

# gw2efficiency Crafting Calculator: Normative Behavior Spec (M33 research)

Research-only. Sources are gw2efficiency's open-source libraries (`recipe-calculation`,
`recipe-nesting`) fetched from `raw.githubusercontent.com` at commit `master` on
2026-07-20/21, plus the live calculator's actual Angular template and JS bundle
(`gw2efficiency.com/views/Crafting/calculator.html`,
`gw2efficiency.com/views/_directives/componentTree.html`,
`gw2efficiency.com/scripts/application.js`) fetched read-only for UI-behavior
confirmation (no gw2efficiency API calls are made by the module itself; this is
dev-time research only, per project rule). Local copies used during this
research (`calculator_view.html`, `componentTree.html`, `application.js`, and
the fetched library sources) lived in a session-scoped scratch directory that
is not part of this repo and no longer exists; see the Appendix below for
exactly which files were fetched from where, for re-fetching if needed.

Repo commits fetched:
- `gw2efficiency/recipe-calculation` @ `ea10eb833a89d335eabf834e16040f23fb98d387` (current master)
- `gw2efficiency/recipe-nesting` @ `bd5082dfdca32c22ae520b03032e790728fd4fe7` (current master)
- `gw2efficiency/recipe-calculation` @ `72d90c74` (2022-08-01, last commit before the
  static vendor table was emptied) - used only to recover historical vendor numbers.
- `gw2efficiency/tradingpost-fees`, `gw2efficiency/item-value` masters (context only).

---

## 0. Pipeline overview

Two packages, used together:

1. **`@gw2efficiency/recipe-nesting`** (`nestRecipes`) - takes the flat list of GW2-API-shaped
   recipes (extended with a `merchant` field - see Section 3) and nests them into a tree of
   `NestedRecipe` / `BasicItemComponent` / `BasicCurrencyComponent` nodes
   (`src/index.ts:59-193`). This is a one-time, price-independent structural step.
2. **`@gw2efficiency/recipe-calculation`** consumes that tree and, in order:
   - `calculateTreeQuantity` - resolves `totalQuantity` / `usedQuantity` per node from the
     requested `amount`, recipe `output`, and `availableItems` (owned stock)
     (`src/calculateTreeQuantity.ts`).
   - `calculateTreePrices` - resolves `buyPrice(Each)`, `craftPrice`, `decisionPrice`,
     `craftResultPrice`, `craftDecisionPrice` bottom-up (`src/calculateTreePrices.ts`).
   - `calculateTreeCraftFlags` - sets the boolean `craft` per node from those prices
     (`src/calculateTreeCraftFlags.ts`).
   - `cheapestTree` orchestrates all three **twice** (see Section 1.3) to produce the final
     tree (`src/cheapestTree.ts:11-72`).
   - `updateTree` re-runs only quantity+price (never touches `craft` flags) - this is what
     the frontend calls after a manual pill click or a price/amount change
     (`src/updateTree.ts`).
   - `usedItems`, `craftingSteps`, `dailyCooldowns`, `recipeItems` are read-only tree
     summarizers for the Shopping List / Crafting Steps / cooldown-warning UI sections.

---

## 1. Craft-vs-buy decision per node

### 1.1 The exact comparison

`calculateTreeCraftFlags.ts:8-15`:
```ts
const hasComponents = !!tree.components
const isUsed = tree.usedQuantity !== 0
const isCheaperToCraft =
  typeof tree.craftPrice !== 'undefined' && (!tree.buyPrice || tree.decisionPrice < tree.buyPrice)
const isForceBuy = forceBuyItems.indexOf(tree.id) !== -1

const craft = hasComponents && isUsed && isCheaperToCraft && !isForceBuy
```

So a node is crafted iff **all** of:
- it has a recipe (`components` present - a `BasicItemComponent`/`BasicCurrencyComponent`
  leaf can never be "crafted"),
- the resolved quantity actually needed is non-zero (`usedQuantity !== 0` - an item already
  fully covered by owned/available stock is not "crafted" even if it has a recipe),
- it is cheaper (or has no buy price at all) to craft: `!tree.buyPrice || decisionPrice <
  buyPrice`,
- it is not in the caller-supplied `forceBuyItems` id list.

**Tie behavior:** the comparison is **strict less-than** (`decisionPrice < buyPrice`). On an
exact tie, `isCheaperToCraft` is `false` -> **the node is bought, not crafted.** The same
strict `<` is used one level up when a recipe's own `decisionPrice`/`craftResultPrice` are
folded into its price (`calculateTreePrices.ts:66-68`, quoted in Section 1.2) - so ties favor
buying at every level of the tree, not just the leaf decision.

### 1.2 `decisionPrice` bookkeeping (this is what actually drives the comparison)

`calculateTreePrices.ts:57-74`:
```ts
const craftDecisionPrice = components.map((c) => c.decisionPrice || 0).reduce((a, b) => a + b, 0)
const craftPrice = components.map((c) => c.craftResultPrice || 0).reduce((a, b) => a + b, 0)

// Update the decision price of this tree segment to the craft price,
// used to determine the craft price of the higher-up recipe
if (
  !('craft' in tree && tree.craft === false) &&
  (('craft' in tree && tree.craft === true) ||
    !decisionPrice ||
    craftDecisionPrice < decisionPrice)
) {
  decisionPrice = craftDecisionPrice
  craftResultPrice = craftPrice
}
craftResultPrice = craftResultPrice || craftPrice
decisionPrice = decisionPrice || craftDecisionPrice
```
Three regimes, all inside the *same* function (it is reused pre- and post-craft-flags, see
Section 1.3):
- **No `craft` flag on the node yet** (first pass, inside `cheapestTree`): pick whichever of
  buy-decisionPrice vs craft-decisionPrice is cheaper (`!decisionPrice || craftDecisionPrice <
  decisionPrice`) - this is literally the "cheapest tree" search.
- **`craft === true`** (root, or a user/force override): *always* use the craft price,
  regardless of whether buying would be cheaper. This is how "force craft anyway" is
  implemented - not a second array, just setting this one field.
- **`craft === false`** (user override, or `forceBuyItems`): the buy-side `decisionPrice`
  is left untouched (the `if` guard is `false`), so this node's contribution to its own
  parent's `craftDecisionPrice` is its **buy** price, not a (possibly cheaper) craft price.

`decisionPrice` (used to *decide*) and `craftResultPrice`/`craftPrice` (used to *display the
real gold total*) diverge whenever `Currency`-type components are involved - see Section 4.

### 1.3 Two-pass structure inside `cheapestTree` (`cheapestTree.ts:46-71`)

```ts
const treeWithQuantity = calculateTreeQuantity(amount, tree, availableItems, ignoredBitItemIds)
const treeWithPrices = calculateTreePrices(treeWithQuantity, itemPrices, customCurrencyPrices)
let treeWithCraftFlags = calculateTreeCraftFlags(treeWithPrices, forceBuyItems)
treeWithCraftFlags = { ...treeWithCraftFlags, craft: true }        // root is ALWAYS "crafted"
const treeWithQuantityPostFlags = calculateTreeQuantity(amount, treeWithCraftFlags, availableItems, ignoredBitItemIds)
return calculateTreePrices(treeWithQuantityPostFlags, itemPrices, customCurrencyPrices)
```
Pass 1 computes prices with no `craft` flags (pure cheapest-path search) -> derives
`craft` flags from that -> forces the root's `craft = true` (you always want to *make*
the top-level item, that is the point of the calculator) -> **recomputes quantities**
(so a node whose ancestor is *not* crafted stops propagating demand to its own children -
`calculateTreeQuantity.ts:70-73`, `ignoreAvailable = ('craft' in tree && tree.craft ===
false) || usedQuantity === 0 || ignoreAvailable`) -> recomputes prices one final time
with the now-fixed `craft` flags, which is what actually gets rendered.

If `valueOwnItems` is enabled (default `true`, see Section 5.3 below - actually the UI's own
term, see Section 1.4), there is a **pre-pass before all of this**
(`cheapestTree.ts:26-44`): compute quantity+prices *ignoring* owned stock, find every item id
where `buyPrice < craftDecisionPrice * 0.85` (i.e. selling the item's components on the TP,
net of the ~15% trading-post cut, would out-earn using them - `getCheaperToBuyItemIds`,
`cheapestTree.ts:74-94`), and force `craft: false` on exactly those ids
(`disableCraftForItemIds`) before the real two-pass calculation runs. This is what makes
"Value Own Materials" (see Section 1.4) buy fresh materials instead of spending stock that is
worth more sold than used.

### 1.4 How the user's manual craft/buy toggle actually integrates

This part is **not** in `recipe-calculation` at all - it lives in the (closed-source)
Angular app, but the mechanism is fully visible in the fetched template
(`views/_directives/componentTree.html:126-184`):
```html
<span class="price" ng-hide="usedQuantity===0 || type==='Currency' || buyPrice===false"
      ng-class="{selected: !component.craft}" ng-click="component.craft = false; emitChange()">
  <input type="radio" ng-checked="!component.craft" /><strong>TP</strong>
  <small><gold amount="component.buyPrice"></gold></small>
</span>
<span class="price" ng-hide="usedQuantity===0 || !components"
      ng-class="{selected: component.craft}" ng-click="component.craft = true; emitChange()">
  <input type="radio" ng-checked="component.craft" />
  <strong ng-show="!disciplines.includes('Merchant')">Crafting</strong>
  <strong ng-show="disciplines.includes('Merchant')">Merchant</strong>
  <small><gold amount="component.craftPrice"></gold></small>
  ...
</span>
```
A pill click **directly mutates `component.craft` to a literal `true`/`false` on that tree
node** (not an id added to some external list) and calls `emitChange()`, which the
controller wires to `updateTree` (`recipe-calculation`'s `updateTree`, confirmed via the
bundle: `n.recalculateTree = I` where `I` calls `a.updateTree(...)` - `application.js`
minified, module boundary near the `cheapestTree`/`updateTree` imports). Per the package's
own README: *"This method does not change any craft flags... If you want to recalculate the
cheapest tree, just use cheapestTree again!"* - so a manual override sticks until the user
explicitly re-runs the optimizer.

Three bulk actions exist above the tree (`calculator.html:599-608`,
`"Expand all" / "Collapse all" -- "Best path" / "Craft all" / "Buy all"`), and their
implementations (found in `application.js`) are:
```js
e.setTreeToBestPath = function(){ _(!0) }                                     // re-run cheapestTree
e.setTreeToCrafting = function(){ t = B(deepClone(tree), 'craft', !0, e=>e.components); t.craft=!0; ... }
e.setTreeToBuying   = function(){ t = B(deepClone(tree), 'craft', !1, e=>e.buyPrice !== false); t.craft=!0; ... }
```
"Craft all" force-sets `craft=true` on every node that *has* a recipe (raw items/currencies
are unaffected, they can't be crafted anyway). "Buy all" force-sets `craft=false` on every
node that *has* a buy price (unpriceable/currency nodes are left alone - there is nothing
else they could do), but **the root itself is always forced back to `craft=true`** even
by "Buy all" - you cannot ask the calculator to just show you a raw TP price without a
recipe tree beneath it, *except* by manually clicking the root's own "TP" pill after the
fact (the root supports the same TP/Crafting pill pair as everything else once rendered).
The two per-item toggles that indirectly feed `forceBuyItems` (not `craft` directly) are
**Daily cooldowns = Buy** (adds `staticItems.buyableDailyCooldowns` to the force-buy list)
and **Mystic Forge Promotions = Disallow** (default; adds precursor-promotion recipe ids to
the same list) - both resolved once at calculation time, not via per-node clicks.

---

## 2. Price basis: buy-order vs instant-buy

### 2.1 The default, straight from the live app's default state

`application.js` (deobfuscated variable names kept as found), the calculator controller's
initial `$scope` assignment:
```js
e.expandedSections = {"cost-breakdown":true,"recipe-tree":true,"used-owned-materials":true,
                       "shopping-list":true,"required-disciplines":true,"required-recipes":true,
                       "crafting-steps":true}
e.price = "buy"                          // <-- DEFAULT price basis
e.useOwnItems = "false"                  // <-- DEFAULT: do not consume owned/bank/character stock
e.dailyCooldowns = "craft"               // <-- DEFAULT: allow crafting daily-cooldown items
e.updateOwnMaterialsAutomatically = false
e.updateBestPathAutomatically = false
e.shoppingListOrder = "-sortBuyPrice"
e.hideUnlockedRecipes = true
e.showAllRecipes = false
e.withApiKey = false
e.valueOwnItems = "true"                 // <-- DEFAULT: value owned stock at its sell value
e.allowMysticForgePromotions = "false"   // <-- DEFAULT: disallow (force-buy precursor promos)
```
And the actual UI control, from `views/Crafting/calculator.html:73-77`:
```html
<strong>Material price</strong>
<select ng-model="price">
  <option value="buy" translate>buy price ("order")</option>
  <option value="sell" translate>sell price ("instant buy")</option>
</select>
```
So gw2efficiency's own labeling is explicit: **"buy price" = buy order** (patient, usually
cheaper), **"sell price" = "instant buy"** (the current lowest listed sell offer, usually
more expensive, but immediate). **The calculator's default is "buy price" (buy orders).**
This is corroborated by a i18n string used elsewhere in the app,
`'sell price ("instant buy")': 'Verkaufs-Preis "Sofortkauf"'` (German), confirming the
"sell price = instant-buy" equivalence app-wide, not just on this page.

### 2.2 How the mode resolves to GW2 API fields, and the automatic per-item fallback

Also found in `application.js` (the price-map builder that feeds `cheapestTree`'s
`itemPrices` argument):
```js
function u(itemIds, mode) {           // mode = "buy" | "sell"
  let r = {}
  itemIds.map(id => {
    let i = TradingpostService.get(id)     // cached {buy:{price}, sell:{price}}
    if (!i) return
    if (i[mode] && i[mode].price) { r[id] = i[mode].price; return }
    const other = mode === "sell" ? "buy" : "sell"
    if (i[other] && i[other].price) r[id] = i[other].price   // per-item fallback
  })
  r = a.useVendorPrices(r)   // legacy no-op passthrough, see Section 3
  return r
}
```
And the trading-post cache itself is populated straight from the official commerce API:
```js
this.refreshFromListings = function (e) {
  var t = n.getItemFromCache(this.item_id)
  t.buy.price  = e.buys[0]  !== undefined ? e.buys[0].unit_price  : 0   // highest buy order
  t.sell.price = e.sells[0] !== undefined ? e.sells[0].unit_price : 0   // lowest sell listing
  ...
}
```
So: **`price=="buy"`** -> `buys[0].unit_price` (GW2 API's `buys` array, i.e. the highest
standing **buy order**). **`price=="sell"`** -> `sells[0].unit_price` (GW2 API's `sells`
array, i.e. the lowest **sell listing** = pay-now/"instant buy" price). **If the selected
side has no price for a given item** (thin market), the calculator silently falls back to
the *other* side for that one item, per-item, rather than leaving it unpriced. This fallback
is purely a UI/data-fetch concern - `recipe-calculation` itself has no opinion here, it just
receives whatever `itemPrices` map it's handed.

### 2.3 Where sell-side (trading-post) fees enter

Not in the raw material cost at all - **only in the "profit" convenience metrics**, which
compare the finished output's *sale* value to its craft cost. Found in `application.js`,
the function building the "Cost Breakdown" section data (`n(e)` operating on the *root*
tree node, where `e.buy`/`e.sell` are the *root item's own* TP price objects):
```js
function n(e) {
  let t = {
    cost: e.craftPrice,
    cost_each: e.craftPrice / e.totalQuantity,
    currencies_cost: e.decisionPrice - e.craftPrice,
    currencies_cost_each: (e.decisionPrice - e.craftPrice) / e.totalQuantity,
  }
  return e.buy ? {
    ...t,
    saving: e.buyPrice - e.craftPrice,
    saving_each: (e.buyPrice - e.craftPrice) / e.totalQuantity,
    profit_buy:  e.buy.price  * e.totalQuantity * 0.85 - e.craftPrice,
    profit_sell: e.sell.price * e.totalQuantity * 0.85 - e.craftPrice,
    ...
  } : t
}
```
`* 0.85` approximates "sell this crafted item and net the post-fee amount" (GW2's combined
5% listing + 10% exchange cut). This app-level `0.85` is a **flat approximation**, distinct
from the precise `gw2e-tradingpost-fees` module (`subFees`/`subListing`/`subTax`,
5%/min-1c and 10%/min-1c with rounding - `tradingpost-fees/src/index.js:1-49`) which exists
as a separate published package but is **not** what either the profit display or
`recipe-calculation`'s own internal `0.85` (see Section 1.3, `cheapestTree.ts:82`) actually
use - both reimplement the flat 15%-off shortcut instead. **"Cost" and "Savings compared to
buying" apply NO fee at all** - they are raw material costs, because you are *buying*
materials, not selling them. **The "Profit (buy price)"/"Profit (sell price)" rows, and the
whole Cost Breakdown block, are only rendered `ng-show="cost_breakdown...saving !==
undefined"`, which requires `e.buy` truthy on the root** - i.e. **for an account-bound /
non-tradable output (legendaries, including Exordium, are account-bound) these Savings/
Profit rows do not render at all.** Only "Cost" and "Cost of Currencies" show.

---

## 3. Vendor-purchasable items

### 3.1 The hardcoded table is dead code

`src/static/vendorItems.ts` (current master, in full):
```ts
// (Legacy) This is no longer used, we now manage vendor items via custom recipes:
// https://github.com/gw2efficiency/custom-recipes/blob/schema-update/merchants.js
export const VENDOR_ITEMS = {}
```
and `src/helpers/useVendorPrices.ts`:
```ts
// (Legacy) Return the price map as-is
export function useVendorPrices(priceMap) { return priceMap }
```
Both are now no-ops. **The `custom-recipes` repo the comment points to is not public** - I
confirmed via the GitHub API that `gw2efficiency/custom-recipes` 404s (and it is absent from
the full `gw2efficiency` org repo listing, which has 12 public repos, none named
`custom-recipes`). I cannot fetch its `merchants.js`. Git history shows the static table was
progressively emptied: `a0bae1ca` (2022-03, TS rewrite, table still populated) ->
`72d90c74` (2022-08, "remove vendorItems whose new vendor recipes are available") ->
`7e1c2ba5` (2022-12, "Remove static vendor items", final emptying). **Do not port
`VENDOR_ITEMS` into our seed data - it is intentionally dead in the upstream project.**

### 3.2 The actual (current) mechanism: vendor purchases are modeled as recipes, not a lookup table

`recipe-nesting`'s recipe shape (`src/api.d.ts:1-22`, `src/index.ts:31-47`) carries an
optional `merchant?: { name: string; locations: Array<string> }` field alongside the normal
`disciplines`/`ingredients`. A vendor-purchasable item is encoded as a synthetic **Recipe**
whose `disciplines === ['Merchant']`, whose `ingredients` are the currency (and/or coin)
cost, and whose `merchant` field names the NPC and location(s). This recipe gets nested by
`nestRecipes` exactly like a crafting recipe, and **all of `recipe-calculation`'s normal
craft-vs-buy math applies to it unmodified** - a vendor "recipe" competes with the item's TP
price via the exact same `decisionPrice < buyPrice` comparison as any other recipe (Section 1).
Evidence this is really how it's modeled:
- `craftingSteps.ts:111-117`: `isMerchantWithNoDependencies` checks
  `disciplines.length===1 && disciplines[0]==='Merchant' && !hasCraftedComponents`, and such
  steps are sorted to the very top of the crafting-steps list, alphabetically by merchant
  name (`craftingSteps.ts:22-24`) - this only makes sense if "buy from a merchant" is a
  *step type*, i.e. a recipe.
- `views/_directives/componentTree.html:138-146`: the same pill that says "Crafting" for a
  normal recipe says **"Merchant"** instead when `disciplines.includes('Merchant')` - it is
  the *same* craft/buy radio pill, just relabeled; there is no separate third "Vendor" pill
  type in the UI.
- `views/Crafting/calculator.html:1091-1102`: the Crafting Steps section's expanded-step
  view literally prints `Merchant` / `{{ step.merchant.name }}` with a `Locations:` tooltip
  built from `step.merchant.locations`.
- `cheapestTree.ts:174-222` (`applyEfficiencyTiersToTree`): homestead-refinement "vendor"
  recipes are matched by checking `tree.merchant && tree.merchant.name.includes('Homestead
  Refinement')` - again, they are ordinary tree nodes with a `merchant` field, not a
  separate concept.

**Practical consequence for our module:** "vendor competes with TP price" is not a special
rule - it *is* the same recipe-vs-buy rule. A vendor-purchasable Exordium-tree item should
be seeded as a Mystic-Forge/Merchant-style recipe entry (ingredients = the vendor's
currency/coin cost, discipline tag = Merchant, a merchant name/location for display) in
`ref/recipes_seed.json`, **not** as a new "vendor price" table - this directly matches
KNOWN-ISSUES #17's ask ("Mystic Runestone: vendor-purchased (Miyani, spirit shards) -
missing vendor offer" -> the fix is a recipe-seed entry, not a vendor-seed entry).

### 3.3 Historical vendor numbers recovered (for wiki cross-check only, NOT to be used as-is)

> **Since shipped (2026-08):** the seeding purpose this section served is
> fulfilled - `ref/vendor_offers.json` is now produced by
> `tools/VendorOfferUpdater`'s wiki scrape, so these 2022 figures are a
> historical cross-check only, exactly as the heading says. Kept because
> `docs/research/m37-r3-achievement-dedup.md` cites them.

I checked the last pre-emptying commit (`72d90c74`, Aug 2022) against the specific items the
brief named. Item ids resolved via the GW2 wiki API:

| Item (Exordium-relevant) | Item ID | In `72d90c74` static table? | Value found |
|---|---|---|---|
| Spool of Gossamer Thread | 19790 | **Yes** | `{type:'gold', quantity:10, cost:640}` (i.e. 64c each) from "Master Craftsmen / Crafting Station" |
| Obsidian Shard | 19925 | **Yes** | `{type:'karma', quantity:1, cost:2100}` from "Tactician Deathstrider, Cathedral of Glorious Victory, Straits of Devastation [SW]" |
| Philosopher's Stone (20796) | 20796 | No | not present even in 2022 - already modeled as a Merchant-recipe (Miyani) by then |
| Mystic Runestone (79418) | 79418 | No | not present - wiki confirms it's Mystic-Forge-vendor-only (requires the "Scholar of Secrets" Mastery to buy from a Mystic Forge vendor), consistent with Merchant-recipe modeling |
| Glob of Ectoplasm (19721) | 19721 | No | correct - it's salvage-only, not vendor-sold |
| Vision Crystal (46746) | 46746 | No | correct - crafted from Ascended mats, not vendor-bought |
| Siege Master's Guide (79683) | 79683 | No | wiki marks this item `status = discontinued`; likely irrelevant to a live Exordium plan |

These 2022 numbers are **stale and must be independently wiki/API-verified** by whatever
dev-time seeder does the actual data work (per repo rule: no invented data, prefer the
official API, gw2efficiency is research-only) - they are cited here only to confirm *which*
items were vendor-modeled historically and to give the seeder a starting point.

---

## 4. Non-coin currency costs (spirit shards, karma, etc.)

### 4.1 Encoding

A currency cost is a tree node with `type: 'Currency'` and a GW2-API currency id (Karma=2,
Spirit Shard=23, etc. - `/v2/currencies`). `nestRecipes` passes these through unchanged
(`recipe-nesting/src/index.ts:136-138`, "Just give back the component for currencies").

### 4.2 Valuation: a hardcoded "decision price" table, used ONLY for the craft/buy decision

`src/static/currencyDecisionPrices.ts` (full table, 46 entries) assigns each currency a
copper-equivalent "decision price" **per unit**, e.g.:
```ts
export const CURRENCY_DECISION_PRICES: Record<number, number | undefined> = {
  1: 1,        // Gold (coin) - 1 copper each, trivially
  2: 1,        // Karma
  23: 3600,    // Spirit Shard  (= 36 silver each)
  15: 23,      // Badge of Honor
  61: 200,     // Research Note
  70: undefined, // Legendary Insight - no assigned value at all
  18: undefined, // Transmutation Charge
  ... // full 46-entry table in src/static/currencyDecisionPrices.ts
}
```
These are **not real trading-post prices** (most of these currencies aren't tradable at
all) - they're the maintainers' own "what is one of these worth for optimization purposes"
estimates, overridable per-call via `cheapestTree`'s `customCurrencyPrices` parameter
(`calculateTreePrices.ts:31-37`).

### 4.3 Currencies contribute to the DECISION price but ZERO to the displayed gold total

`calculateTreePrices.ts:21-38`:
```ts
let buyPriceEach = itemPrices[tree.id] || false
if (tree.type === 'Currency') {
  buyPriceEach = tree.id === 1 ? 1 : false   // coin=1c/ea; every other currency has NO buy price
}
const buyPrice = buyPriceEach ? tree.usedQuantity * buyPriceEach : false
let craftResultPrice = buyPrice     // <-- starts equal to buyPrice, i.e. FALSE for non-coin currencies

let decisionPriceEach = buyPriceEach || undefined
if (tree.type === 'Currency') {
  decisionPriceEach = customCurrencyPrices[tree.id] ?? CURRENCY_DECISION_PRICES[tree.id]
}
let decisionPrice = decisionPriceEach ? tree.usedQuantity * decisionPriceEach : false
```
And at the parent, `craftPrice = components.map(c => c.craftResultPrice || 0).reduce(...)`
(line 59) - **a non-coin currency leaf's `craftResultPrice` is `false`, which the `|| 0`
coerces to zero.** So a currency cost (spirit shards, karma, etc.) **influences whether a
parent recipe gets flagged `craft: true`** (via its contribution to `craftDecisionPrice`,
which the parent's own `decisionPrice` is compared against, Section 1.2) **but contributes
literally nothing to the parent's displayed `craftPrice`/`craftResultPrice` gold total.**
This is the *exact* mechanism behind KNOWN-ISSUES #16 ("Vendor-source items show no price")
- a Merchant-recipe priced mostly/entirely in a non-coin currency will show a near-zero or
blank gold price on its own pill, by upstream design, not by omission.

### 4.4 How this is actually surfaced in the UI (so it isn't just silently lost)

`views/_directives/componentTree.html:138-184`, on the "Crafting"/"Merchant" pill:
```html
<small><gold amount="component.craftPrice"></gold></small>
<span ng-show="(component.craftPrice != component.craftDecisionPrice) && !component.multipleRecipeTree">
  | <div class="decision-tooltip-wrapper">
      <span class="decision-tooltip-trigger"><i class="sprite-account-wallet-small-exclamation"></i></span>
      <div class="decision-tooltip-content">
        <small>Crafting gold price: <gold amount="component.craftPrice"></gold></small>
        <small>Currencies: <gold amount="component.craftDecisionPrice - component.craftPrice"></gold></small>
        <small class="smallest">This is an estimated opportunity cost
                                 for the used currencies in the recipe.</small>
        <hr/>
        <small>Optimization Price: <gold amount="component.craftDecisionPrice"></gold></small>
      </div>
    </div>
</span>
```
So whenever a node's real gold price differs from its decision price (i.e. it has a
meaningful non-coin currency component), a small "!" wallet icon appears next to the price;
hovering shows a 3-line breakdown: **"Crafting gold price"** (the real coin total),
**"Currencies"** (= `craftDecisionPrice - craftPrice`, explicitly captioned **"This is an
estimated opportunity cost for the used currencies in the recipe"**), and **"Optimization
Price"** (= `craftDecisionPrice`, the full decision-basis total). The same delta
(`decisionPrice - craftPrice`) is also what feeds the top-level "Cost Breakdown" section's
**"Cost of Currencies"** row (Section 2.3's `n(e)` function; the HTML labels it
`(estimated opportunity cost)` too, `calculator.html:412-424`/`507-519`). A bare `Currency`
leaf itself shows *no number at all* on its own pill - just a plain grey, non-interactive
label `Currency` (`componentTree.html:194-201`). This confirms the module's existing M30 fix
(#3: "currency rows render icons... in the Total Cost section") is the right *place* to put
currency costs (a separate, clearly-labeled row/total, not folded into the coin total) -
gw2efficiency's own UI goes one step further and explicitly calls it an **"estimated
opportunity cost,"** which is worth echoing in our label text.

---

## 5. Items with NO TP price (account-bound, MF-only, etc.)

### 5.1 What the algorithm does

If an item id is simply absent from the `itemPrices` map handed to `cheapestTree`/
`calculateTreePrices` (confirmed by `tests/calculateTreePrices.spec.ts`'s "missing buy
prices" case, id `4` never appears in the test's `prices` object), then per
`calculateTreePrices.ts:21`, `buyPriceEach = itemPrices[tree.id] || false` -> `false`,
so `buyPrice = false` and (for a non-Currency, non-recipe leaf) `decisionPrice = false` too.
Back in `calculateTreeCraftFlags.ts:9-15`: if this leaf has **no** `components` (no known
recipe either), `hasComponents` is `false`, so `craft` is unconditionally `false` regardless
of price - **it is never "forced to craft."** It is simply flagged as bought, with a `false`
buy price. If it *does* have a recipe (just no TP listing), `isCheaperToCraft` reduces to
`typeof craftPrice !== 'undefined' && !tree.buyPrice` -> effectively always `true` (since
`!false` is `true`) -> **it IS force-crafted**, because "no buy price and has a recipe"
short-circuits the `!tree.buyPrice` branch. So: **truly unpriceable + no recipe ->
bought-with-unknown-price; unpriceable + has a recipe -> always crafted** (there is no
other option). This is a clean, important distinction for the module's own "false UNKNOWN"
sweep (KNOWN-ISSUES #17): an item rendering UNKNOWN should be checked for whether it *should*
have a recipe/vendor-recipe seed (then it'd auto-force-craft) vs whether it is genuinely
craft-and-buy-less (then it's expected to render as unpriceable, by design, not a bug).

### 5.2 What downstream consumers do with a `false` buy price

`usedItems.ts:24-32`: an unpriced, uncraftable leaf still gets bucketed into the `buy` map
(`breakdown.buy[tree.id] += tree.usedQuantity`) exactly like a normally-priced item - **there
is no separate "unpriceable" bucket** in the data layer; only the *display* layer
distinguishes it (by checking `buyPrice === false`).

### 5.3 Exact UI treatment

`views/_directives/componentTree.html:186-192`:
```html
<span class="price desaturated"
      ng-show="usedQuantity !== 0 && buyPrice === false && !components && type !== 'Currency'">
  <strong translate>Not sold or crafted</strong>
</span>
```
A grey, non-interactive (no `ng-click`, no radio input) pill reading exactly **"Not sold or
crafted."** No price number is shown at all - not "0", not a dash, just the label. In the
Shopping List (`calculator.html:806-819`), the same condition renders as an em-dash instead:
```html
<span class="single-price" ng-show="shopping_item.buyPriceEach">...</span>
<span class="single-price desaturated" ng-show="!shopping_item.buyPrice">&mdash;</span>
<span class="price desaturated" ng-show="!shopping_item.buyPrice">&mdash;</span>
```
i.e. Shopping List rows for unpriceable items show a plain em dash character (the literal glyph, rendered via the &mdash; HTML entity) in both the "each" and
"total" columns, still listed by name/quantity like every other shopping-list row (again:
same data bucket, only the price cells differ). **Neither surface invents a badge like
"account-bound" or "achievement-only"** - gw2efficiency does not attempt to classify *why*
an item is unpriced, it just shows the absence of a price plainly. Our module's M32
acquisition-hint tooltips (curated wiki-verified hints on top of the plain "no known
source" fallback) are strictly *more* informative than upstream here, and there is nothing
in gw2efficiency's own behavior that argues against keeping that enhancement.

---

## 6. Tree/display semantics

### 6.1 Source-pill exposure (which sources a node shows)

Confirmed line-by-line from `componentTree.html:124-246` - up to **six** pills can coexist
on one node, each independently gated:

| Pill | Shown when | Interactive? | Sets |
|---|---|---|---|
| `TP` | `usedQuantity!==0 && type!=='Currency' && buyPrice!==false` | yes, click | `craft=false` |
| `Crafting` / `Merchant` | `usedQuantity!==0 && components present` (label swaps to "Merchant" if `disciplines.includes('Merchant')`) | yes, click | `craft=true` |
| `Not sold or crafted` | `usedQuantity!==0 && buyPrice===false && !components && type!=='Currency'` | no | (nothing - dead end) |
| `Currency` | `type==='Currency'` | no | (nothing; grey `#eee` bg) |
| `Using N owned materials` | `usedQuantity < totalQuantity` | no | (info only) |
| `Using N owned currency` | `type==='Currency' && ownedQuantity>0` | no | (info only) |

**When both `TP` and `Crafting`/`Merchant` are eligible, both render side-by-side as a radio
pair**, with the currently-selected one highlighted (`ng-class="{selected: ...}"`) and an
actual `<input type="radio">` reflecting the chosen source - this is precisely the "expose
all sources with the selected one highlighted" behavior KNOWN-ISSUES #18 asks for, and it
directly implies the **displayed pill must always agree with `component.craft`** (i.e. with
whichever price basis the solver actually used for that node) - there is no independent
"decision label" concept upstream, the pill *is* the `craft` flag, rendered.

### 6.2 Default expansion

Confirmed from `application.js`: after the initial calculation, only
`tree.expanded = true` is set on the **root** node
(`o.tree.expanded=!0` right after `cheapestTree` returns). Nothing else auto-expands -
`componentTree.html:250-276`'s subtree only renders
`ng-if="component.expanded && component.components && component.craft && usedQuantity>0"`,
and a fresh subcomponent object has no `.expanded` property, which the `+`/`-` toggle reads
as `!== true` -> shows the collapsed `+` affordance. So: **default = root expanded, every
descendant collapsed, one level at a time**, plus a manual `+`/`-` per node and the bulk
`expandTree()`/`collapseTree()` links (the latter re-forces the root back to `expanded=true`
even after a full collapse, so the top level of the tree is never fully hidden). All of the
page's top-level *sections* (Cost Breakdown, Recipe Tree, Used Owned Materials, Shopping
List, Required Disciplines, Required Recipes, Crafting Steps), by contrast, **default to
fully expanded** (`expandedSections: {...: true, ...}` for all seven keys) - this is a
different axis (section visibility) from tree-node expansion, and it matches this module's
already-shipped-by-default fully-expanded layout for these same sections.

### 6.3 Totals aggregation (Cost Breakdown columns)

From Section 2.3's `n(e)` function, applied twice - once to the "excluding owned materials"
tree (`calculateTreePrices` run with `availableItems={}`) and once to the real tree - giving
the "Using own materials" vs "Without using own materials" comparison blocks seen in
`calculator.html:384-581`:
- **Cost** = `craftPrice` (real gold total, currencies excluded, Section 4.3).
- **Cost of Currencies** = `decisionPrice - craftPrice` (the non-coin "shadow" total,
  labeled *estimated opportunity cost*).
- **Savings compared to buying** = `buyPrice - craftPrice` of the **root** (only shown if the
  root itself has a TP price - i.e. never for an account-bound legendary like Exordium).
- **Profit (buy price)** / **Profit (sell price)** = root's `buy.price`/`sell.price` *
  quantity * 0.85, minus `craftPrice` (again, only if root is tradable).
- **Cost of own materials** / **Cost of own currencies** = the same `craftPrice`/
  `decisionPrice - craftPrice` split, computed on the *excluding-owned* tree, restricted to
  the components actually satisfied from `availableItems` (this is the "using own
  materials" block, and it explicitly labels the currency-portion `(estimated opportunity
  cost)` again).

### 6.4 Shopping List

Sourced from `usedItems()`'s `buy` + `currency` maps merged with owned-but-partially-used
items; default sort is **`-sortBuyPrice`** (highest total buy price first - the `<select>`
options are `Total Price` / `Price each` / `Quantity`, `calculator.html:768-772`). Rows with
`buyPrice===false` render an em-dash in both price columns (Section 5.3); rows are otherwise
plain name+quantity+price, each individually checkbox-able (a shopping-checklist UX, not
modeled in `recipe-calculation` at all - purely a frontend-local `checked` flag per row).

### 6.5 Crafting Steps ordering (worth echoing verbatim - `craftingSteps.ts:6-35`)

```ts
export function craftingSteps(tree) {
  let steps = craftingStepsInner(tree).reverse()
  steps = steps.filter((step) => step.quantity > 0)
  // Mystic Clovers (and their components, already ordered by the recursion) go first
  const mysticCloverSteps = steps.filter((step) => step.id === MYSTIC_CLOVER_ID)   // 19675
  steps = steps.filter((step) => step.id !== MYSTIC_CLOVER_ID)
  steps = [...mysticCloverSteps, ...steps]
  // Then no-dependency Merchant purchases, alphabetical by merchant name
  const merchantSteps = steps.filter(isMerchantWithNoDependencies)
    .sort((a, b) => a.merchant?.name.localeCompare(b.merchant?.name || '') || 0)
  steps = steps.filter((step) => !isMerchantWithNoDependencies(step))
  steps = [...merchantSteps, ...steps]
  return steps.map((step) => ({ ...step, crafts: Math.ceil(step.quantity / step.output) }))
}
```
Rationale in the source comment: Mystic Clovers (and their own inputs, Obsidian Shards /
Philosopher's Stones, which get naturally ordered above the Clover step by the recursion)
are surfaced first "since they generate items that are always useful for crafting the other
steps" - i.e. a UX nudge to knock out the RNG/time-gated step early. Then dependency-free
merchant buys are pulled to the very top (alphabetically), so the "go buy these first"
shopping trip is grouped together before any crafting begins. `step.crafts =
ceil(quantity/output)` is the literal "how many times you click Craft" count, computed only
after all same-id steps across the whole tree have been merged (so quantities aren't
double-counted per-branch).

---

## 7. Normative directives for our module (translating the above)

1. **Decision rule**: `craft = hasRecipeOrVendorRecipe && usedQuantity!=0 && (noBuyPrice ||
   decisionPrice < buyPrice) && !manuallyForcedBuy`. Ties (`decisionPrice == buyPrice`) must
   resolve to **buy**, not craft.
2. **Manual override** = a literal per-node craft/buy flag the user sets directly (not a
   separate override list at the app layer, though a `forceBuyItems`-style id list is fine
   internally for cooldown/MF-promotion-style bulk policies); once set, quantity/price
   recompute must **not** revert it until the user explicitly re-optimizes.
3. **Price basis default = buy order** (GW2 API `buys[0].unit_price`), with automatic
   per-item fallback to the sell/instant-buy price when the buy side has no price. Trading
   post fees (5%+10%) belong only in "would-you-profit-from-selling-the-output" style
   metrics, never in material cost totals, and (per repo invariant) real per-copper-rounded
   fee math should be used rather than a flat 0.85 if we implement a profit metric at all.
4. **Vendor items = recipes**, tagged with a vendor/merchant identity, not a separate lookup
   table; they compete with TP price through the exact same decision rule as any recipe.
   The current module's pill vocabulary (TP / Craft / Vendor / Currency / Unknown, per
   KNOWN-ISSUES) can keep "Vendor" as a distinct label for clarity (gw2efficiency just
   reuses "Merchant" as a Crafting-pill variant), but the underlying decision math should be
   unified, not vendor-vs-craft-vs-buy as three independent branches.
5. **Non-coin currencies**: give them a per-unit "decision value" (own or gw2efficiency's
   table as a reference, wiki-adjusted) used *only* to decide craft vs buy, and make sure
   they contribute **zero** to the displayed gold total of any node that contains them -
   surface the delta as a clearly-labeled separate line/tooltip ("estimated opportunity
   cost"), exactly matching the M30 currency-icon fix's spirit.
6. **Unpriceable items**: no recipe + no TP price -> shown bought, price cell blank/dash,
   no invented number, no "forced craft." Has a recipe + no TP price -> always crafted
   (nothing else it could be). Do not conflate "unpriceable" with "account-bound" or
   "achievement-gated" in the algorithm - those are just why a *seed* is missing a price,
   not a distinct algorithmic state.
7. **Multi-source display**: any node eligible for more than one source must render all
   eligible pills simultaneously with the active one highlighted, and the highlighted pill
   must always match the price basis the solver actually used - directly actionable for
   KNOWN-ISSUES #18.
8. **Tree default expansion**: root expanded, everything else collapsed one level at a time,
   plus bulk expand-all/collapse-all. Section-level expansion (Cost Breakdown, Recipe Tree,
   Shopping List, etc.) defaults to fully open - already how this module behaves.
9. **Crafting Steps ordering**: Mystic Clover (and its own inputs) first, then dependency-
   free vendor/merchant buys (alphabetical), then everything else in tree order, with
   per-id quantities merged before computing "how many crafts."

---

## Appendix: fetched artifacts (for re-fetching if needed)

None of the files below are committed to this repo (the fetches were
dev-time research, not build inputs); this list records exactly what was
fetched and from where so the research is reproducible.

- Full `src/` + relevant `tests/` of `gw2efficiency/recipe-calculation@master`
  (`ea10eb8`), fetched from `raw.githubusercontent.com`.
- `src/index.ts`, `src/api.d.ts`, and the README of
  `gw2efficiency/recipe-nesting@master` (`bd5082d`), fetched from
  `raw.githubusercontent.com`.
- `gw2efficiency/tradingpost-fees` and `gw2efficiency/item-value` (fee/value
  helper packages), fetched for context only.
- `vendorItems.ts` @ commit `72d90c74` (last populated version before
  removal), fetched from `raw.githubusercontent.com`.
- The live `views/Crafting/calculator.html` Angular template, fetched from
  `gw2efficiency.com`.
- The live `views/_directives/componentTree.html` template (the per-node
  pill markup quoted throughout Section 1/Section 4/Section 5/Section 6),
  fetched from `gw2efficiency.com`.
- The live, minified `application.js` app bundle (~4.2MB), fetched from
  `gw2efficiency.com` and searched via regex for the default-state block,
  price-map builder, cost-breakdown formula, and tree bulk-action functions
  quoted above.

## Known gaps / caveats

- `gw2efficiency/custom-recipes` (the actual current vendor-recipe data source) is not a
  public repo - I could not fetch real current vendor currency amounts for Mystic Runestone,
  Philosopher's Stone, etc. Treat the historical `72d90c74` numbers (Section 3.3) as a
  starting point only; the seeder must wiki/API-verify actual current values.
  I did not attempt to recover it as fetching a private/inaccessible repo did not seem
  worth pursuing further given the behavioral mechanism (Section 3.2) is fully confirmed
  without it.
- **Partly superseded (2026-08):** `calculateTreeQuantity.ts` has since been
  fetched. It assigns no per-node `ownedQuantity` at all and excludes
  Currency-type nodes from availability consumption entirely, so whatever
  populates gw2e's tree pill number lives in their live `application.js`,
  not in the published `recipe-calculation` package. Full record: entry 11
  of `docs/gw2e-considerations.md` (matrix row 42).
- No dedicated gw2efficiency FAQ/help page documenting calculator behavior was found via
  WebSearch; all behavioral confirmation instead came directly from the shipped Angular
  template and JS bundle, which I consider a stronger (ground-truth) source than any FAQ
  prose would have been.
- I did not verify `application.js`'s `recalculateTree`/`I()` wiring keystroke-by-keystroke
  (webpack module numbering makes exact call-graph tracing slow); the README-documented
  contract of `updateTree` (never touches `craft` flags) plus the observed default-state and
  pill-click markup together are sufficient to be confident in Section 1.4's description, but
  a future pass could pin the exact function bodies if more precision is ever needed.
