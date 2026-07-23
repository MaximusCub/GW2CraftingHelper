# M37 Item 25 Research: Multi-Item Sell-Side Economics (gw2efficiency parity)

Dev-time research only. gw2efficiency is never called at runtime by the
module (project HARD RULE); this report documents upstream behavior so the
module's own logic can echo it. Fetched live 2026-07-21 via direct `curl`
(network egress confirmed available in this environment) into a
session-scoped scratch directory (not part of this repo, and no longer
available):

- `application.js` (4,220,941 bytes) - live minified webpack bundle, from
  `https://gw2efficiency.com/scripts/application.js?cb=1783715316`.
- `calculator_view.html` (48,052 bytes) - the Angular calculator template,
  from `https://gw2efficiency.com/views/Crafting/calculator.html`.
- `componentTree.html` (9,901 bytes) - the recursive per-node tree
  directive template, from
  `https://gw2efficiency.com/views/_directives/componentTree.html`.
- `rn/index.ts`, `rn/api.d.ts` - `gw2efficiency/recipe-nesting@master`
  source, from `raw.githubusercontent.com`.

All quoted code below is verbatim from these files (byte-for-byte, not
paraphrased) unless explicitly marked otherwise. This supersedes the lost
`m34-r1-gw2e-multiitem.md` report referenced by KNOWN-ISSUES #21/#25 - that
report is gone (transient scratchpad), so everything below was
re-derived from scratch against the *current* live app, not recovered from
the old report. `docs/gw2e-parity-spec.md` (the committed M33 spec) already
covers the **single-item** Cost Breakdown formula (`n(e)`, Sections 2.3 and
6.3) from an earlier fetch; I independently re-fetched and re-verified that
same function today (see 1.1) and found it byte-identical in logic, so the
existing spec's single-item claims stand confirmed. Everything about
**multi-item** batching (Sections 1.2-1.8 below) is new: it was not in
`gw2e-parity-spec.md` at all.

---

## 1. Upstream gw2e mechanism

### 1.1 Per-item Cost Breakdown formula `n(e)` - MEASURED, re-confirmed today

Found in `application.js` at the module that also defines the multi-item
`o()` function (Section 1.2). Deminified with consistent names below the
verbatim quote:

```js
function n(e){let t={cost:e.craftPrice,cost_each:e.craftPrice/e.totalQuantity,currencies_cost:e.decisionPrice-e.craftPrice,currencies_cost_each:(e.decisionPrice-e.craftPrice)/e.totalQuantity};return e.buy?i(i({},t),{saving:e.buyPrice-e.craftPrice,saving_each:(e.buyPrice-e.craftPrice)/e.totalQuantity,profit_buy:e.buy.price*e.totalQuantity*.85-e.craftPrice,profit_buy_each:(e.buy.price*e.totalQuantity*.85-e.craftPrice)/e.totalQuantity,profit_sell:e.sell.price*e.totalQuantity*.85-e.craftPrice,profit_sell_each:(e.sell.price*e.totalQuantity*.85-e.craftPrice)/e.totalQuantity}):t}
```

Deminified:
```js
function n(node) {
  let base = {
    cost: node.craftPrice,
    cost_each: node.craftPrice / node.totalQuantity,
    currencies_cost: node.decisionPrice - node.craftPrice,
    currencies_cost_each: (node.decisionPrice - node.craftPrice) / node.totalQuantity
  };
  return node.buy ? { ...base,
    saving: node.buyPrice - node.craftPrice,
    saving_each: (node.buyPrice - node.craftPrice) / node.totalQuantity,
    profit_buy:  node.buy.price  * node.totalQuantity * 0.85 - node.craftPrice,
    profit_buy_each:  (node.buy.price  * node.totalQuantity * 0.85 - node.craftPrice) / node.totalQuantity,
    profit_sell: node.sell.price * node.totalQuantity * 0.85 - node.craftPrice,
    profit_sell_each: (node.sell.price * node.totalQuantity * 0.85 - node.craftPrice) / node.totalQuantity
  } : base;
}
```
MEASURED facts:
- `node.buy`/`node.sell` are the node's *own* raw TP price objects
  (`buy.price` = `buys[0].unit_price`, highest standing buy order;
  `sell.price` = `sells[0].unit_price`, lowest sell listing - confirmed via
  `refreshFromListings`, quoted unchanged from the M33 spec and
  re-verified today at the same call site). `node.buy` is only truthy when
  the item actually has TP data, i.e. the entire `saving`/`profit_*` block
  is *absent* (not zero - the key is missing) whenever the node has no buy
  price at all (account-bound, MF-only, etc.).
- **No `Math.floor` anywhere in `n()`.** All six extra fields are raw
  (possibly fractional) JS numbers. The 0.85 factor is applied to the
  *whole* `price * totalQuantity` product in one multiplication, not
  per-unit.
- `profit_buy` uses the **buy-order** side (`node.buy.price`, revenue if
  you instant-sell into the current top buy order right now); `profit_sell`
  uses the **sell-listing** side (`node.sell.price`, revenue if you list at
  the current ask and it eventually sells). Both are always computed and
  shown side by side, completely independent of the separate `price`
  scope variable (buy/sell) that only controls *material cost* basis
  (`gw2e-parity-spec.md` Section 2.1-2.2).

### 1.2 The multi-item rollup `o()` (the "sell-excess-crafted-components" mechanism) - MEASURED, new today

Same module, immediately following `n()`:

```js
function o(e,t,r){const a={};e.components.forEach((e,i)=>{!function e(t,r){const i=n(r),a=i&&"number"==typeof i.cost?i.cost:0,o=Math.floor(.85*(r.buy&&r.buy.price||0)*t.totalQuantity)-a,s=Math.floor(.85*(r.sell&&r.sell.price||0)*t.totalQuantity)-a;t.craftedComponentsBreakdown={totalCost:a,totalSellingToBuyOrderProfit:o,totalSellingToSellOrderProfit:s},t.components&&r.components&&t.components.forEach((t,n)=>{e(t,r.components[n])})}(e,t.components[i]),function e(t){if("Currency"===t.type&&r){const e=r.find(e=>e.id===t.id);if(e){a[t.id]||(a[t.id]=0);const n=Math.max(0,e.value-a[t.id]);t.ownedQuantity=Math.min(n,t.totalQuantity),a[t.id]+=t.ownedQuantity}}t.components&&t.components.forEach(t=>{e(t)})}(e)});const o=e.components.filter(e=>e.craft);return i(i({},e),{},{craftedComponentsBreakdown:{totalCost:o.reduce((e,t)=>e+(t.craftedComponentsBreakdown?t.craftedComponentsBreakdown.totalCost:0),0),totalSellingToBuyOrderProfit:o.reduce((e,t)=>e+(t.craftedComponentsBreakdown?t.craftedComponentsBreakdown.totalSellingToBuyOrderProfit:0),0),totalSellingToSellOrderProfit:o.reduce((e,t)=>e+(t.craftedComponentsBreakdown?t.craftedComponentsBreakdown.totalSellingToSellOrderProfit:0),0)}})}
```

Deminified (renamed to avoid the minifier's scope-shadowed single letters):
```js
function o(usingOwnRoot, withoutOwnRoot, ownedCurrencies) {
  const ownedSoFar = {};

  usingOwnRoot.components.forEach((childUsingOwn, i) => {
    const childWithoutOwn = withoutOwnRoot.components[i];

    // (A) recursively stamp EVERY node in the subtree with its own
    // "what if I sold this instead" figures, using ONLY that node's own
    // price/quantity - never its children's.
    (function walk(nodeUsingOwn, nodeWithoutOwn) {
      const breakdown = n(nodeWithoutOwn);
      const cost = breakdown && typeof breakdown.cost === 'number' ? breakdown.cost : 0;
      const buyOrderProfit  = Math.floor(0.85 * (nodeWithoutOwn.buy  && nodeWithoutOwn.buy.price  || 0) * nodeUsingOwn.totalQuantity) - cost;
      const sellOrderProfit = Math.floor(0.85 * (nodeWithoutOwn.sell && nodeWithoutOwn.sell.price || 0) * nodeUsingOwn.totalQuantity) - cost;
      nodeUsingOwn.craftedComponentsBreakdown = {
        totalCost: cost,
        totalSellingToBuyOrderProfit: buyOrderProfit,
        totalSellingToSellOrderProfit: sellOrderProfit
      };
      if (nodeUsingOwn.components && nodeWithoutOwn.components) {
        nodeUsingOwn.components.forEach((c, idx) => walk(c, nodeWithoutOwn.components[idx]));
      }
    })(childUsingOwn, childWithoutOwn);

    // (B) unrelated side effect: annotates Currency nodes with ownedQuantity
    // from the wallet, draining a running per-currency-id pool. Cosmetic,
    // not part of the profit math.
    (function annotateOwnedCurrency(node) {
      if (node.type === 'Currency' && ownedCurrencies) {
        const match = ownedCurrencies.find(c => c.id === node.id);
        if (match) {
          ownedSoFar[node.id] = ownedSoFar[node.id] || 0;
          const remaining = Math.max(0, match.value - ownedSoFar[node.id]);
          node.ownedQuantity = Math.min(remaining, node.totalQuantity);
          ownedSoFar[node.id] += node.ownedQuantity;
        }
      }
      if (node.components) node.components.forEach(annotateOwnedCurrency);
    })(childUsingOwn);
  });

  // (C) the AGGREGATE shown at the top of Cost Breakdown: sum ONLY the
  // DIRECT children of the passed-in root that are flagged craft===true,
  // using ONLY their own (not recursively summed) per-node figures from (A).
  const craftedChildren = usingOwnRoot.components.filter(c => c.craft);
  return { ...usingOwnRoot,
    craftedComponentsBreakdown: {
      totalCost: craftedChildren.reduce((sum, c) => sum + (c.craftedComponentsBreakdown ? c.craftedComponentsBreakdown.totalCost : 0), 0),
      totalSellingToBuyOrderProfit: craftedChildren.reduce((sum, c) => sum + (c.craftedComponentsBreakdown ? c.craftedComponentsBreakdown.totalSellingToBuyOrderProfit : 0), 0),
      totalSellingToSellOrderProfit: craftedChildren.reduce((sum, c) => sum + (c.craftedComponentsBreakdown ? c.craftedComponentsBreakdown.totalSellingToSellOrderProfit : 0), 0)
    }
  };
}
```

Call site (same module, the function that builds the whole per-generation
result object consumed by the Angular `$scope`):
```js
{costBreakdown:{
  excluding_own: i(i({}, n(e)), {}, {own_materials_cost:k, own_materials_cost_each:Math.ceil(k/e.totalQuantity), own_currencies_cost:S, own_currencies_cost_each:Math.ceil(S/e.totalQuantity)}),
  total: n(t)
},
tree: o(e,t,p),
ownedItems:b, shoppingItems:w, craftingSteps:E, dailyCooldowns:{...}
}
```
where `e` = the real, owned-materials-applied tree root; `t` = the same
tree recomputed with `availableItems={}` (ignoring owned stock); `p` =
owned-currency amounts. **`tree: o(e,t,p)` replaces the Angular scope's
own `tree` object** - this is where `tree.craftedComponentsBreakdown`
(read by the templates below) and `tree.multipleRecipeTree` (passed
through unchanged by the object spread) come from.

**MEASURED, key semantics** (all from the exact code above):
1. `o()`'s first argument's own `.components` are its **direct children
   only**. In multi-item mode the argument passed is the *root* (the
   synthetic wrapper with `multipleRecipeTree: true`), so its
   `.components` are exactly the N requested items' own root nodes. In
   single-item mode the argument is that one item's own root, so
   `.components` are its top-level recipe ingredients.
2. The recursive walk (A) computes a `craftedComponentsBreakdown` on
   **every node in the whole tree**, not just the direct children -
   but the aggregate rollup (C) only ever sums the **direct children**,
   filtered to `craft === true`. Deeper descendants' own
   `craftedComponentsBreakdown` values are computed but, per Section 1.3
   below, only ever read by the UI when that specific node happens to be
   a direct child of a `multipleRecipeTree` node (i.e., in practice: only
   the N item roots in a batch - see 1.3).
3. **The filter is `craft === true` only - never `tradable`.** An item
   that the solver decided to *buy* rather than craft is excluded from the
   sum entirely (its `craftedComponentsBreakdown` may still exist, just
   unused). An item that IS being crafted but has **no TP price at all**
   (account-bound, MF-only) is **not excluded** - see 1.4.
4. Two different fee formulas coexist for the "same" concept: `n()` (1.1,
   single-item Cost-Breakdown Profit rows) is **unfloored**; `o()`'s inner
   walker (multi-item rollup, and the per-node "Crafting Profit" pill,
   1.3) **floors** `0.85 * price * totalQuantity` to a whole number before
   subtracting cost. Neither implements GW2's actual 5%-listing +
   10%-exchange, min-1-copper-each fee structure - both are a flat 15%-off
   shortcut, confirmed identical to what `gw2e-parity-spec.md` Section 2.3
   already documented for `n()` from an earlier fetch, and now confirmed
   to extend to `o()` too.

### 1.3 Exact UI wiring (multipleRecipeTree gating) - MEASURED, from `calculator_view.html` + `componentTree.html`

**(a) Per-node "Crafting Profit" pill** (`componentTree.html:232-245`):
```html
<!-- Owned -->
<span class="price" ng-show="showprofit && component.craft && component.tradable">
  <strong translate>Crafting Profit</strong>
  <small>
    <gold amount="component.craftedComponentsBreakdown.totalSellingToSellOrderProfit"> </gold>
  </small>
  <strong>TOTAL |</strong>
  <small>
    <gold
      amount="component.craftedComponentsBreakdown.totalSellingToSellOrderProfit / component.totalQuantity"
    ></gold>
  </small>
  <strong translate>per item</strong>
</span>
```
Shows only `totalSellingToSellOrderProfit` (the sell-listing/ask-price
figure, NOT `totalSellingToBuyOrderProfit`) - unlike the root rollup
(below), which shows both. Gated on THREE conditions: `showprofit`,
`component.craft`, `component.tradable`.

`showprofit` is not a persistent scope flag - it is re-derived **fresh at
every recursion level** when the directive recurses into subcomponents
(`componentTree.html:268-273`):
```html
<component-tree
  component="subcomponent"
  showprofit="!!component.multipleRecipeTree"
  use-own-items="{{useOwnItems}}"
  daily-cooldowns="dailyCooldowns"
></component-tree>
```
`component` here is the *parent* node one level up from `subcomponent`.
Since only the synthetic wrapper root ever has `multipleRecipeTree: true`,
`showprofit` is `true` **only** for the wrapper's direct children (the N
item roots) - it is `false` for every deeper node, because their own
parent (some ordinary item root or ingredient, never the wrapper) never
has `multipleRecipeTree` set. **Conclusion: the per-node "Crafting Profit"
pill can only ever appear on the N top-level item roots of a multi-item
batch, never on any nested ingredient, and never in single-item mode at
all** (there, the tree's own root is passed `showprofit` from the
top-level controller binding, not through this recursive prop - not
found set to `true` anywhere else in either template).

Also note (`componentTree.html:222-230`), the "Ignore" pill is
**mutually exclusive** with profit mode:
```html
<span class="price" ng-show="useOwnItems === 'true' && showprofit == false && (component.usedQuantity != 0 || component.ignored)" ...>
```

**(b) Cost Breakdown section, "Without using own materials" block**
(`calculator_view.html:476-581`, full, unelided):
```html
<div ng-show="expandedSections['cost-breakdown']" ng-class="{'desaturated': owned_items.length > 0}">
  <h3 class="tier-3 center" ng-show="owned_items.length > 0" translate>Without using own materials</h3>

  <h3 class="tier-3 center"
      ng-show="tree.multipleRecipeTree && !(tree.craftedComponentsBreakdown.totalSellingToBuyOrderProfit === 0 && tree.craftedComponentsBreakdown.totalSellingToSellOrderProfit === 0)"
      translate>
    Profit numbers are the sum of all crafted recipes
  </h3>

  <ul class="c-filter stacking calculator-cost" ng-class="{'margin-bottom': owned_items.length == 0}">
    <li> <!-- Cost --> ... cost_breakdown.total.cost ... </li>
    <li> <!-- Cost of Currencies --> ... cost_breakdown.total.currencies_cost ... </li>
    <li ng-show="cost_breakdown.total.saving !== undefined"> <!-- Savings compared to buying --> ... </li>
    <li ng-show="cost_breakdown.total.saving !== undefined"> <!-- Profit (buy price) --> ... cost_breakdown.total.profit_buy ... </li>
    <li ng-show="cost_breakdown.total.saving !== undefined"> <!-- Profit (sell price) --> ... cost_breakdown.total.profit_sell ... </li>

    <li ng-show="tree.multipleRecipeTree && !(tree.craftedComponentsBreakdown.totalSellingToBuyOrderProfit === 0 && tree.craftedComponentsBreakdown.totalSellingToSellOrderProfit === 0)">
      <span class="label stack" translate> Profit (buy price) </span>
      <span class="stack"><span gold amount="tree.craftedComponentsBreakdown.totalSellingToBuyOrderProfit"></span></span>
    </li>
    <li ng-show="tree.multipleRecipeTree && !(tree.craftedComponentsBreakdown.totalSellingToBuyOrderProfit === 0 && tree.craftedComponentsBreakdown.totalSellingToSellOrderProfit === 0)">
      <span class="label stack" translate> Profit (sell price) </span>
      <span class="stack"><span gold amount="tree.craftedComponentsBreakdown.totalSellingToSellOrderProfit"></span></span>
    </li>
  </ul>
</div>
```
**MEASURED, and important**: `cost_breakdown.total` comes from `n(t)`
where `t` is the root passed to the whole computation. In multi-item mode
that root is the synthetic wrapper, which is a fake node with `id: false`
and therefore **never has a `buy`/`sell` TP price of its own**. So
`n(wrapperRoot).buy` is always falsy, meaning **`cost_breakdown.total.saving`
is always `undefined` for a multi-item batch, unconditionally** -
the ordinary "Savings compared to buying" / "Profit (buy price)" /
"Profit (sell price)" `<li>`s (the ones gated on `saving !== undefined`)
are **never shown for ANY multi-item batch**, regardless of any
individual requested item's own tradability. Only "Cost" and "Cost of
Currencies" survive from that row set (and they ARE meaningful sums,
because `craftPrice`/`decisionPrice` accumulate bottom-up through the tree
math for free - no explicit per-item summing code needed for those two).

The banner ("Profit numbers are the sum of all crafted recipes") and the
two extra `craftedComponentsBreakdown`-based rows share the exact same
`ng-show` condition, and that condition is what actually carries the
"profit" concept in multi-item mode - **the banner text is describing
those two extra rows specifically, not the (always-hidden-in-multi-mode)
ordinary Profit rows above them.** The extra rows have no `(per item)`
sub-line at all (unlike every ordinary row, which hides its sub-line via
`ng-show="!tree.multipleRecipeTree"` but still has the markup).

**This is NOT the same feature as `excessiveComponents`/`step.excessAmount`**
(a completely unrelated warning about recipes whose output granularity
forces crafting more than strictly needed - `calculator_view.html:288-301`
and `:1123-1133`, e.g. "This item is sold/crafted in bulk and you will
craft {{ step.excessAmount }} extra... It is also available on the TP for
{{ step[price].price }} each."). KNOWN-ISSUES' own paraphrase
("sell-excess-crafted-components-for-profit") risks conflating the two;
I explicitly did not find any code path connecting `excessAmount`/
`excessiveComponents` to `craftedComponentsBreakdown` - they are
independent features that happen to both involve "extra" quantities.

### 1.4 Tradability / account-bound gating - MEASURED (aggregate), INFERRED (origin of the flag)

- The **per-node pill** (1.3a) requires `component.tradable` to render at
  all.
- The **aggregate sum** (1.2, step C) does **not** check `tradable` at
  all - only `craft`. An account-bound (or otherwise unpriced) requested
  root item that the solver crafted (which, per `gw2e-parity-spec.md`
  Section 5.1, is forced whenever an item has a recipe but no buy price)
  still gets summed in. Its own walk-step (A) computes
  `buyOrderProfit = Math.floor(0.85 * 0 * totalQuantity) - cost = -cost`
  (no buy/sell price -> 0 revenue), so it contributes a **negative**
  amount equal to its own full craft cost to the displayed aggregate,
  even though the per-node pill for that same item would never render
  (gated on `tradable`, false). **Verified from the code above; this
  looks like a genuine upstream inconsistency/quirk** (the visible
  per-item pills for a batch would never show a negative number for an
  account-bound item, but the top rollup silently absorbs its full cost as
  a loss) rather than an intentional design. Flagging as a candidate
  point of deliberate divergence rather than blind replication - see
  Section 4.
- I could **not** locate the literal line that sets `component.tradable`
  itself (searched `application.js` broadly - Section 5, "Sources" -
  found only *consumers* of a `tradable` boolean in unrelated app
  sections - wallet/investment tracking, achievement-of-gemstore-price
  helpers - none of which visibly feed the crafting tree's node
  hydration). The crafting-calculator's own tree-hydration step (where
  `disciplines`, `name`, `image`, etc. get attached to raw nesting-library
  nodes) is closed-source and I did not find it in the bundle within the
  time available. **UNVERIFIED**: exact derivation formula for
  `component.tradable` / `step.tradable`. Reasonably confident
  (INFERRED, not measured) it means "this item has GW2-API commerce
  listings at all", based solely on how it gates "is there a TP price to
  show" in both `componentTree.html` (1.3a) and the Crafting Steps
  `step.tradable` (`calculator_view.html:1127-1130`, gating "It is also
  available on the TP for X each").

### 1.5 Price-basis field-naming: gw2e vs. this module - MEASURED both sides

gw2e's own `buy.price`/`sell.price` (raw TP fields on any tree node) map:
- `buy.price` = `buys[0].unit_price` = revenue if selling instantly right
  now.
- `sell.price` = `sells[0].unit_price` = revenue if listing at today's ask
  and waiting for a sale.

This module's `ItemPrice` (`Models/ItemPrice.cs`) uses the **opposite
naming direction** on purpose (own field docs, MEASURED from source):
- `BuyInstant` = `sells.unit_price` ("cost to buy instantly" - i.e. gw2e's
  `sell.price`).
- `SellInstant` = `buys.unit_price` ("revenue from selling instantly" -
  i.e. gw2e's `buy.price`).

So gw2e's `profit_buy`/`totalSellingToBuyOrderProfit` (using `buy.price`)
corresponds to **our `SellInstant`**, not `BuyInstant` - the names are
inverted between the two codebases. This module's existing single-item
`ApplySellSideEconomics` (Section 3) already and only uses `SellInstant`
(the instant-sell/buy-order basis - the "realistic, sell it right now"
number) - i.e. it already mirrors gw2e's `profit_buy`, not `profit_sell`,
and never shows a second "list-and-wait" profit figure the way gw2e's UI
does. This is an existing, already-shipped simplification (one profit
number, not two), not something introduced by this research.

### 1.6 Ground truth for our own module's fee model (for comparison) - MEASURED

`Services/TradingPostMath.cs` already implements the real GW2 Trading
Post fee structure precisely: 5% listing fee + 10% exchange fee, each
independently rounded half-up with a 1-copper minimum, applied to the
**total** sale value (not per-unit):
```csharp
public static long ListingFee(long totalValue) => totalValue <= 0 ? 0 : Math.Max(1L, RoundHalfUp(totalValue, 5));
public static long ExchangeFee(long totalValue) => totalValue <= 0 ? 0 : Math.Max(1L, RoundHalfUp(totalValue, 10));
public static long NetSaleRevenue(long unitPrice, int quantity) { /* totalValue - ListingFee - ExchangeFee, floored at 0 */ }
```
This is **already more precise than gw2e's own flat-0.85 shortcut**
(Section 1.1/1.2/1.6) - a deliberate, already-shipped design decision
(matches `gw2e-parity-spec.md` directive #3: "real per-copper-rounded fee
math should be used rather than a flat 0.85"). The recommended design
(Section 4) keeps using this existing helper unchanged for any new batch
math - it must NOT be replaced with gw2e's flat 0.85 for "authenticity".

---

## 2. Ground-truth data

This item is a pure algorithm/UI-mechanism question, not a data-seeding
one (unlike KNOWN-ISSUES #24/#28's wiki-rate tables) - there is no numeric
ground-truth table to reproduce here. The relevant "ground truth" is:

- The upstream source code itself (Section 1 above, all MEASURED/quoted).
- This module's own, already-correct GW2 Trading Post fee formula
  (Section 1.6, MEASURED) - already wiki-accurate (5%/10%, min 1c,
  rounded on the total), so no new fee research was needed or performed.
- No GW2 API endpoint provides "batch profit" data directly; every number
  in this feature is derived entirely from `/v2/commerce/prices` unit
  prices (already fetched by the existing pipeline) plus the solver's own
  craft-cost totals. No new API calls are required for the recommended
  design in Section 4.

---

## 3. Current module state

All read directly from the working tree at
`/mnt/c/Dev/Blish/GW2CraftingHelper` (not the `.claude/worktrees/*` copies,
which are other agents' sandboxes and were not inspected).

### 3.1 `Models/CraftingPlanResult.cs`
Four fields exist for sell-side economics, all documented as "stay at
type default for a multi-item batch":
- `int SellableQuantity` (default 0)
- `long? NetSaleValue` (default null)
- `long? CraftingProfit` (default null)
- `long? MaterialOpportunityCost` (default null)
- Plus `PriceBasis PriceBasis`, `long? TargetUnitSellPrice`.
- `IReadOnlyList<PlanRequestItem> RequestedItems` (2+ entries only for a
  genuine batch) and `List<CraftingTreeNode> MultiItemRoots` (populated
  instead of `CraftingTree` for a batch) are the batch-mode equivalents
  already in place for the tree/shopping-list side of M35.

### 3.2 `Services/CraftingPlanPipeline.cs`
- `ApplySellSideEconomics(...)` (private static, ~line 1034-1107): the
  single-item (M20) implementation. Computes, in order:
  1. `SellableQuantity` = `max(requestedQuantity, actualProducedQuantity)`
     when the root was crafted and its chosen recipe over-produces
     (`chosenRecipe.CraftsNeeded * chosenRecipe.OutputCount`).
  2. `MaterialOpportunityCost` = sum over `UsedMaterials` of
     `TradingPostMath.NetSaleRevenue(matPrice.SellInstant, used.QuantityUsed)`,
     only in `OwnMaterialsMode.Valued`; a material with `SellInstant == 0`
     contributes 0, not exclusion.
  3. `NetSaleValue`/`CraftingProfit`, only when `prices[targetItemId].SellInstant > 0`:
     `NetSaleValue = TradingPostMath.NetSaleRevenue(SellInstant, sellableQuantity)`;
     `CraftingProfit = NetSaleValue - Plan.TotalCoinCost - MaterialOpportunityCost`.
  - Called from the single-item `GenerateStructuredAsync(int,...)` overload
    and from `ResolveWithOverrides`, guarded there by
    `if (context.Tree.Id != Gw2Constants.MultiItemWrapperItemId)` -
    i.e. **already explicitly skipped** for a multi-item context on every
    local re-solve, not just initial generation.
- `GenerateStructuredAsync(IReadOnlyList<PlanRequestItem>,...)`
  (~line 518-548): single-entry list delegates straight to the untouched
  single-item overload (byte-identical, regression-tested - see 3.4);
  2+ entries go to `GenerateStructuredMultiAsync`.
- `GenerateStructuredMultiAsync` (private, ~line 573-813): builds the
  synthetic wrapper tree (`RecipeService.BuildMultiItemTreeAsync`), runs
  the identical fetch/reduce/force-buy-prepass/solve/metadata pipeline a
  single item uses, then calls `BuildCraftingTreeResult` to populate
  `MultiItemRoots`. **Never calls `ApplySellSideEconomics`** - its own doc
  comment (verbatim, lines 561-571) states this is deliberate pending
  "a future milestone".
- `BuildCraftingTreeResult` (~line 997-1032): shared tree-building helper
  that branches on `tree.Id == Gw2Constants.MultiItemWrapperItemId` to
  populate either `CraftingTree` (single) or `MultiItemRoots` (batch, one
  `CraftingTreeNode` per `wrapperRecipe.Ingredients` entry - i.e. each
  requested item's own root, exactly the nodes needed for the new
  per-item economics loop in Section 4).
- `Gw2Constants.MultiItemWrapperItemId`/`MultiItemWrapperRecipeId` =
  `int.MinValue` sentinels (`Models/Gw2Constants.cs`).

### 3.3 `Models/PlanSolveContext.cs`
Snapshots everything `ResolveWithOverrides` needs for a local re-solve,
including `RequestedItems` (already carried through for a batch) and
`PriceBasis`/`OwnMaterialsMode`/`CurrencyValuation`. Does **not** currently
carry `TargetItemId`/`Quantity` in any batch-meaningful way (they hold the
wrapper's own placeholder `Gw2Constants.MultiItemWrapperItemId`/`1`, per
the multi-solve-context construction) - a batch-economics helper must get
each real item's own id/quantity from `RequestedItems`, not from
`TargetItemId`/`Quantity`.

### 3.4 Tests
- `tests/GW2CraftingHelper.Tests/Services/MultiItemPlanTests.cs`:
  `GenerateStructuredAsync_SingleEntryList_MatchesLegacySingleItemCall`
  is the existing byte-identical regression precedent to extend/keep
  green; `GenerateStructuredAsync_MultiItem_PerRootDecision_MatchesStandaloneSingleItemSolve`
  is a strong existing precedent pattern (per-root comparison against a
  standalone single-item solve) to mirror for a new economics test.
- `tests/GW2CraftingHelper.Tests/Services/PlanViewModelBuilderTests.cs`:
  single-item economics view-model tests already exist
  (`SellValuePresent_AddsSellAndProfitRows`,
  `NegativeProfit_RendersAsLossWithAbsoluteValue`,
  `CurrencyCostsPresent_ProfitRowGetsCoinOnlyQualifier`,
  `OverproducedBatch_SellRowShowsActualQuantity`,
  `MaterialOpportunityCostPositive_NoSellPrice_StillAddsRow`); multi-item
  tests exist for title/row-suppression
  (`MultiItemRequest_AppendsMultiItemNoteRowToSummarySection`,
  `SingleItemRequest_NoMultiItemNoteRow`) but none yet for economics rows
  in multi mode - matches the M35 doc comment's "deliberately unset".

### 3.5 `Models/PlanViewModel.cs` + `Services/PlanViewModelBuilder.cs` (the "Cost Breakdown" builder)
This module does **not** replicate gw2e's multi-block Cost Breakdown UI
(no "Using own materials" vs "Without using own materials" split, no
separate "Savings compared to buying" row) - that was already condensed,
in M20, into a single "Total Cost" section (`PlanSectionType.Summary`)
with a flat list of `PlanRowViewModel` rows. `BuildSummarySection`
(`Services/PlanViewModelBuilder.cs:128-230`) currently emits, in order:
1. Always: one `CoinTotal` row, "Total" (+ " (buy-order prices)" suffix
   when `PriceBasis.BuyOrder`), `CoinValue = Plan.TotalCoinCost`.
2. If `MaterialOpportunityCost > 0`: one `CoinTotal` row, "Own materials
   (sell value forgone)".
3. If `NetSaleValue.HasValue` (single-item only today): one `CoinTotal`
   row "Sell value (after 15% TP fees)" (or "Sell value (Nx, after 15%
   TP fees)" when `SellableQuantity > TargetQuantity`), then one more
   `CoinTotal` row "Profit if sold" / "Loss if sold" (+ " (coin costs
   only)" qualifier when the plan has non-coin currency costs),
   `CoinValue = Math.Abs(CraftingProfit ?? 0)`.
4. One `CurrencyCost` row per `Plan.CurrencyCosts` entry.
5. If `isMultiItem`: one `MultiItemNote` row, literal text "Totals above
   are the sum of all crafted recipes in this batch." - this is the exact
   row this task's item (f) asks to be redesigned once real batch
   sell/profit numbers exist.

### 3.6 `Views/CraftingPlanView.cs` (the actual Cost Breakdown / "Total Cost" renderer)
`CreateSummarySectionBody` (~line 3321) partitions `section.Rows` into
`coinRows` (all `PlanRowType.CoinTotal`), `otherRows` (`CurrencyCost`), and
`noteRows` (`MultiItemNote`/plain text), then:
- **All** `coinRows` render together via `CreateCostTileRow` as a single
  horizontal band of N equal-width tiles (one call, one row, regardless
  of N) - `PlanContentHeightMath.SummaryBodyHeight` treats the whole coin
  band as **one fixed `CostTileRowHeight`, independent of tile count**
  (`hasCoinRow ? CostTileRowHeight : 0`, a boolean not a count). Tile
  width floors at 80px (`PlanRelayoutMath.ComputeCostTileGeometry`) and
  the row centers its total content width, same behavior already
  exercised today at up to 4 simultaneous tiles (Total + Own materials +
  Sell value + Profit). **Practical consequence: adding new batch-level
  coin tiles requires zero View/height-math changes** as long as the
  total simultaneous tile count for any one plan stays at or below what
  single-item mode already reaches (4) - see Section 4.
- `CurrencyCost` rows render individually via `CreateCurrencyRow`
  (fixed-height, one per currency).
- `MultiItemNote`/plain-text rows render via `CreateTextRow`
  (`FallbackTextRowHeight`).

---

## 4. Recommended echo design

### 4.0 Seed schema
**N/A.** This is a pure computation/display feature - no new wiki/vendor
data, no new `ref/*.json` seed file. (Unlike KNOWN-ISSUES #24/#28, which
do need new seed data.)

### 4.1 Explicit divergence decisions (documented, not blind mirroring)

gw2e's own multi-item rollup (Section 1.2/1.4) has two properties I
recommend **not** replicating literally, with rationale:

1. **Craft-only filtering.** gw2e sums only requested items whose own
   root resolved to `craft === true`, silently dropping any item the
   solver chose to buy outright. This module's *single-item*
   `ApplySellSideEconomics` has **no** such filter today - it computes
   `NetSaleValue`/`CraftingProfit` for the target item whenever it has a
   sell price, whether the plan crafted or bought it (a flip/arbitrage
   number is still meaningful information). Recommendation: for
   consistency with our own already-shipped single-item semantics (which
   this task requires stay byte-identical anyway), the batch aggregate
   should **sum the same per-item {SellableQuantity, NetSaleValue,
   CraftingProfit} triple regardless of craft-vs-buy**, i.e. do NOT add
   gw2e's craft-only filter. This is simpler, requires no new "was this
   root crafted" branch, and matches what a user would see if they ran
   each item through this module one at a time and added the numbers up
   by hand.
2. **Untradable-crafted-item silent negative drag** (Section 1.4). gw2e's
   aggregate includes an account-bound crafted item as a `-cost`
   contribution with no visible per-item explanation. Recommendation:
   **do not replicate this.** Exactly like our own single-item path
   already does (`NetSaleValue` stays `null` when the target has no sell
   price - no profit number is shown at all, not a hidden negative), a
   requested item with no sell price should be **excluded from the batch
   sum entirely** (contribute nothing, not a penalty) - i.e. reuse
   `ApplySellSideEconomics`'s own per-item gating (`prices[itemId].SellInstant > 0`)
   unchanged, just once per requested item instead of once for a single
   target.
3. **Two profit bases (buy-order vs sell-listing).** gw2e always shows
   both `profit_buy` and `profit_sell`. This module's single-item design
   already only shows one (`SellInstant`/buy-order basis - Section 1.5).
   Recommendation: keep it that way for the batch total too, for
   consistency with the existing single-item row and to avoid doubling
   the Total Cost section's row count.

### 4.2 Algorithm

Add one new private static helper to `CraftingPlanPipeline.cs`,
e.g. `ComputePerItemEconomics(RecipeNode itemRoot, SolveResult solveResult, IReadOnlyDictionary<int,ItemPrice> prices, int requestedQuantity, OwnMaterialsMode ownMaterialsMode)` that factors out **exactly** the
`SellableQuantity`/`NetSaleValue`/`CraftingProfit` computation currently
inline in `ApplySellSideEconomics` (the over-production bump via
`solveResult.Decisions[itemRoot.NodeId]`/chosen-recipe lookup, then the
sell-price/fee lookup) but returns a small tuple/struct instead of writing
onto `CraftingPlanResult` directly. Then:

- **Refactor `ApplySellSideEconomics`** to call this new helper for its
  own single-item case and write the result onto `result.*` exactly as
  today - this is a pure extraction, the single-item code path and its
  output are unchanged (same arithmetic, same order of operations,
  same rounding via the untouched `TradingPostMath` calls) - verify with
  the existing single-item tests, which must all still pass unmodified.
- **`MaterialOpportunityCost` needs no per-item split at all** - it is
  already computed once over the batch's *merged* `UsedMaterials` list
  (Section 3.2, step 2), which `GenerateStructuredMultiAsync` already
  populates correctly for a batch via the shared `InventoryReducer`. Pull
  this block out into its own tiny helper
  (`ComputeMaterialOpportunityCost(usedMaterials, prices, ownMaterialsMode)`)
  called identically by both the single-item and the new multi-item path.
- **New: `ApplyBatchSellSideEconomics`** (multi-item analog), called from
  `GenerateStructuredMultiAsync` right where `ApplySellSideEconomics` is
  called in the single-item path, and from `ResolveWithOverrides`'s
  currently-empty `else` branch (the `if (context.Tree.Id !=
  Gw2Constants.MultiItemWrapperItemId)` guard - add the multi-item call in
  the `else`, so a local Ignore/override re-solve of a batch keeps the
  batch profit figures live, matching how every other part of a re-solve
  already behaves). Logic:
  1. Locate the wrapper's recipe (`tree.Recipes.First(r => r.RecipeId ==
     Gw2Constants.MultiItemWrapperRecipeId)`) and its `.Ingredients` -
     these are the N item roots, in request order (same list
     `BuildCraftingTreeResult` already walks for `MultiItemRoots`).
  2. For each item root, paired with its corresponding `RequestedItems[i]`
     entry (same order - both come from the same wrapper-build step, see
     `RecipeService.BuildMultiItemTreeAsync`), call
     `ComputePerItemEconomics(itemRoot, solveResult, prices,
     items[i].Quantity, ownMaterialsMode)`. Skip (contribute nothing) any
     item whose own price lookup fails (`SellInstant <= 0`) - per 4.1.2.
  3. Sum the surviving items' `SellableQuantity` into a batch total (an
     int sum is fine - no per-item unit-price averaging needed since we
     never display an aggregate "per unit" figure), sum `NetSaleValue`
     into `result.NetSaleValue`, sum `(NetSaleValue - itemCraftCost)` per
     item into `result.CraftingProfit`. Each item's own craft cost is
     **not** `Plan.TotalCoinCost` (that is the whole batch's shared-material
     total) - it must be each item's own `craftPrice`-equivalent, i.e. the
     coin cost attributable to that one root. The existing solver/tree
     already exposes this per-node (the same `n(e)`-equivalent value our
     `PlanSolver`/`CraftingTreeBuilder` must already compute per node to
     show a price on that node's own pill - reuse whatever field already
     backs the per-node coin price display, do not introduce a new
     costing pass).
  4. Call `ComputeMaterialOpportunityCost` once over the batch's merged
     `usedMaterials` (unchanged from what `GenerateStructuredMultiAsync`
     already has) and subtract it from the summed profit, exactly
     mirroring the single-item formula's own structure
     (`CraftingProfit = NetSaleValue - CoinCost - MaterialOpportunityCost`).
  5. `result.SellableQuantity` for a batch has no single natural
     "requested quantity" to compare against (unlike single-item's
     `Plan.TargetQuantity`) - store the summed produced quantity as-is;
     the view layer should not attempt an "overproduced" qualifier for
     the batch row (see 4.3) since there is no single target quantity to
     compare it to.
  - Leave `TargetUnitSellPrice` `null` for a batch (it is inherently a
    single-item concept - a batch has N unit prices, one per item; do not
    invent a meaningless "average").

### 4.3 Display (`PlanViewModelBuilder.BuildSummarySection` + `Views/CraftingPlanView.cs`)

No new `PlanRowType` is needed - reuse `CoinTotal` exactly as today.
Because `NetSaleValue.HasValue`/`MaterialOpportunityCost.HasValue` will
now also be populated for a multi-item result once 4.2 ships, the
**existing** `BuildSummarySection` code (Section 3.5, steps 2-3) already
fires unchanged for a batch with zero new branching - only the row
**labels** need a small multi-item-aware tweak (batch wording, since
"Sell value (Nx, after 15% TP fees)" reads oddly when N is a summed
across-items quantity rather than one item's own production count):
- Single-item: keep every existing label verbatim (byte-identical - see
  4.4).
- Multi-item: `"Sell value (batch total, after 15% TP fees)"` and
  `"Profit if sold" / "Loss if sold"` with a `" (batch total)"` qualifier
  appended before any existing `" (coin costs only)"` qualifier -
  concatenate, do not replace, so a batch with non-coin currency costs
  reads `"Profit if sold (batch total, coin costs only)"`.
- Drop the "overproduced quantity" qualifier for multi-item (no single
  target quantity - 4.2 step 5); always use the plain `"Sell value..."`
  label there.
- **Update the `MultiItemNote` text** (Section 3.5 step 5): today it says
  "Totals above are the sum of all crafted recipes in this batch." - once
  Sell value/Profit rows exist for a batch, this note is exactly the
  right place to echo gw2e's actual banner concept accurately: something
  like *"Sell value and profit are the sum across every requested item
  that has a live Trading Post sell price."* (mirroring gw2e's "Profit
  numbers are the sum of all crafted recipes" banner, Section 1.3b, but
  worded around this module's actual craft-agnostic, tradable-only
  semantics from 4.1, not gw2e's craft-only one - do not just copy gw2e's
  sentence verbatim, since our filter is deliberately different).
- `Views/CraftingPlanView.cs`: **no changes required.** `CreateCostTileRow`/
  `CreateCurrencyRow`/`CreateTextRow`/`SummaryBodyHeight` already handle
  an arbitrary number of `CoinTotal` rows generically (Section 3.6); the
  maximum simultaneous tile count for a batch (Total + Own materials +
  Sell value + Profit = 4) does not exceed what single-item mode already
  exercises today, so no new geometry/height edge case is introduced.

### 4.4 Byte-identical single-item regression plan

- The refactor in 4.2 (extracting `ComputePerItemEconomics`/
  `ComputeMaterialOpportunityCost` out of `ApplySellSideEconomics`) touches
  the single-item code path's *source layout* but must not touch its
  *arithmetic* - every existing call site
  (`ApplySellSideEconomics(result, treeUsedForSolve, solveResult, prices,
  targetItemId, quantity, priceBasis, usedMaterials, ownMaterialsMode)`)
  keeps the exact same signature and result-field assignments.
- Run the full existing suite unmodified first (baseline green), then
  after the refactor + new multi-item code, confirm:
  - Every existing test in `PlanViewModelBuilderTests.cs` /
    `CraftingPlanPipelineTests.cs` tagged single-item economics
    (Section 3.4 list) still passes with **no changes to their
    assertions** - if any assertion needs editing, the refactor
    introduced a behavior change and must be fixed, not the test.
  - `MultiItemPlanTests.GenerateStructuredAsync_SingleEntryList_MatchesLegacySingleItemCall`
    still passes unmodified (it never touches the new batch code path at
    all, by construction - single-entry lists short-circuit before
    reaching `GenerateStructuredMultiAsync`).
- New tests to add (mirroring existing naming conventions):
  - `MultiItemPlanTests`: `GenerateStructuredAsync_MultiItem_TwoTradableCraftedItems_SumsSellValueAndProfit`
    (compare against running each item alone through the single-item path
    and adding NetSaleValue/CraftingProfit by hand - same comparison
    pattern as the existing `..._MatchesStandaloneSingleItemSolve` test);
    `..._OneItemUntradable_ExcludedFromSumNotNegative` (asserts the
    untradable item contributes 0, not a negative drag - directly tests
    the 4.1.2 divergence decision); `..._OneItemBoughtNotCrafted_StillContributes`
    (tests the 4.1.1 divergence decision); `ResolveWithOverrides_MultiItem_...`
    variant proving the batch economics recompute after an Ignore/override
    re-solve (mirrors the existing `ResolveWithOverrides_MultiItem_*`
    tests already in the file).
  - `PlanViewModelBuilderTests`: `MultiItemRequest_SellValuePresent_AddsAggregateSellAndProfitRows`,
    `MultiItemRequest_NoteRowTextDescribesSellValueSum` (asserts the
    updated `MultiItemNote` wording from 4.3), plus a negative-profit
    variant mirroring `NegativeProfit_RendersAsLossWithAbsoluteValue`.
  - A geometry/height regression is very unlikely given 4.3's "reuses
    existing generic machinery" analysis, but add one
    `PlanContentHeightMathTests` case confirming `SummaryBodyHeight` for a
    multi-item row set with 4 simultaneous `CoinTotal` rows still returns
    exactly `CostTileRowHeight` (already implied by the existing
    boolean-not-count logic, Section 3.6, but worth a named test since
    this is the first time 4 tiles will occur in *multi*-item mode).
- Full build + full test suite must stay green per repo invariant
  (`<dotnet> build ... && <dotnet> test ...`).

---

## 5. Sources

- `docs/gw2e-parity-spec.md` (committed, M33 research) - single-item Cost
  Breakdown baseline (Sections 2.1-2.3, 6.3), re-confirmed independently
  today for `n(e)` (Section 1.1).
- `docs/KNOWN-ISSUES.md` sections 21 ("M35: gw2efficiency parity -
  multi-item plans", especially 21.3) and 25 ("Multi-item sell-side
  economics") - task framing and the M35 doc-comment pointer.
- Live fetch, 2026-07-21 (this session):
  - `https://gw2efficiency.com/scripts/application.js?cb=1783715316`
    (4,220,941 bytes) - `n()`/`o()` functions, `craftedComponentsBreakdown`,
    the cost-breakdown call site, `refreshFromListings`, `DAILY_COOLDOWNS.tradable`.
  - `https://gw2efficiency.com/views/Crafting/calculator.html` (48,052
    bytes) - Cost Breakdown section markup (both blocks), `excessiveComponents`/
    `step.excessAmount` (the unrelated feature, Section 1.3).
  - `https://gw2efficiency.com/views/_directives/componentTree.html`
    (9,901 bytes) - per-node pill markup, `showprofit` recursive wiring.
  - `https://raw.githubusercontent.com/gw2efficiency/recipe-nesting/master/src/index.ts`
    and `api.d.ts` - checked for a `tradable` field definition (not
    found there - Section 1.4).
- Local module source read directly (not fetched):
  `Models/CraftingPlanResult.cs`, `Models/PlanSolveContext.cs`,
  `Models/PlanViewModel.cs`, `Models/ItemPrice.cs`, `Models/PriceBasis.cs`,
  `Models/Gw2Constants.cs`, `Services/CraftingPlanPipeline.cs`,
  `Services/TradingPostMath.cs`, `Services/PlanViewModelBuilder.cs`,
  `Services/PlanContentHeightMath.cs`, `Services/PlanRelayoutMath.cs`,
  `Views/CraftingPlanView.cs`,
  `tests/GW2CraftingHelper.Tests/Services/MultiItemPlanTests.cs`,
  `tests/GW2CraftingHelper.Tests/Services/PlanViewModelBuilderTests.cs`.

---

## 6. Open questions

1. **`component.tradable` / `step.tradable` exact derivation** -
   UNVERIFIED (Section 1.4). I found only consumers, not the definition,
   within the time budget. If a future session needs to be byte-exact
   about which items this module should treat as "tradable" for gating
   purposes, it should rely on this module's own existing tradability
   signal (GW2 API commerce-listing presence, i.e. `ItemPrice.SellInstant
   > 0` / `BuyInstant > 0`, already used throughout the single-item path)
   rather than trying to reverse-engineer gw2e's closed-source hydration
   step further.
2. **Per-item "own craft cost" plumbing** (Section 4.2 step 3) - I did
   not trace exactly which existing field/method already exposes a
   single item-root's own coin cost in isolation from the batch's shared
   `Plan.TotalCoinCost` (the tree nodes clearly carry *a* per-node price
   for their own pill display, per `CraftingTreeBuilder`, but I did not
   open that file to confirm the exact field name/type to reuse). The
   implementing session must open `Services/CraftingTreeBuilder.cs` /
   `Models/CraftingTreeNode.cs` first and use whatever field the tree
   renderer already reads for a node's own price pill - do not compute a
   parallel/duplicate costing pass.
3. Whether to literally replicate gw2e's second profit basis
   (`profit_sell`/"list and wait") for the batch, given the single-item
   path already deliberately shows only one. Recommendation in Section
   4.1.3 is "no, stay consistent with the existing single-item design",
   but this is a product call, not a mechanically-forced one - flag for
   the implementing session/user if they want the second number anyway.
4. Whether "which items count" (4.1) should be a Settings toggle
   (mirroring gw2e's literal craft-only behavior for users who want exact
   upstream parity) rather than a fixed module default. Not recommended
   given this module already has no analogous single-item toggle, but
   noted as a possible follow-up if user feedback wants it.
5. No live in-game verification was performed for this report (research
   task only, per the task framing) - once implemented, this feature
   should go through the same screenshot-loop live-verification the M35
   multi-item UI already received (KNOWN-ISSUES #21.3's "LIVE-VERIFIED
   2026-07-21" note) before being marked resolved.
