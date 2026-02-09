# Phase A – gw2efficiency Research (Reference Only)

> **Purpose**: Verified observations, known gaps, and labeled inferences from gw2efficiency discovery research. Ground truth for feasibility checks and validation. NOT a roadmap or implementation guide.
>
> **Rules for future use**:
> - Treat as a reference appendix — consult only when validating assumptions or confirming data availability.
> - Do NOT treat as a roadmap or instruction to build bottom-up.
> - Future work proceeds top-down from desired UX and product goals, consulting this document only when needed.
> - When referencing, explicitly state which fact or constraint is being pulled.

---

## Observed Endpoints

| Endpoint | Classification | Auth | What It Provides |
|---|---|---|---|
| `api.guildwars2.com/v2/commerce/prices?ids=...` | **Official GW2 API** | None | Aggregated TP: highest buy order + lowest sell listing per item (copper) |
| `api.guildwars2.com/v2/commerce/listings?ids=...` | **Official GW2 API** | None | Full order book: all buy/sell listings at each price point |
| `api.guildwars2.com/v2/items?ids=...` | **Official GW2 API** | None | Item metadata incl. `vendor_value` (sell-TO-NPC price, NOT buy-from-NPC) |
| `api.guildwars2.com/v2/recipes?ids=...` | **Official GW2 API** | None | Recipe definitions: ingredients, output, disciplines |
| `api.guildwars2.com/v2/recipes/search?output=ID` | **Official GW2 API** | None | Find recipes producing a given item |
| gw2-api.com/* | **gw2efficiency internal** (archived) | — | Proxy/enriched endpoints. NOT usable at runtime. |
| `staticItems.vendorItems` in `@gw2efficiency/recipe-calculation` | **gw2efficiency open-source** (MIT) | — | Hand-curated vendor buy prices. Readable source code, not a runtime API. |

## TP Price Structure (`/v2/commerce/prices`)

```json
{
  "id": 19684,
  "buys":  { "quantity": 145975, "unit_price": 7018 },
  "sells": { "quantity": 126,    "unit_price": 7019 }
}
```

- `sells.unit_price` = what you pay to buy instantly (lowest listing)
- `buys.unit_price` = what you get selling instantly (highest order)
- All values in copper. 15% TP tax on sales (5% listing + 10% exchange).

## TP Full Order Book (`/v2/commerce/listings`)

```json
{
  "id": 19684,
  "buys": [
    { "listings": 1, "unit_price": 7018, "quantity": 250 },
    { "listings": 3, "unit_price": 7017, "quantity": 1500 }
  ],
  "sells": [
    { "listings": 1, "unit_price": 7019, "quantity": 50 },
    { "listings": 2, "unit_price": 7020, "quantity": 100 }
  ]
}
```

## Vendor Item Structure (gw2efficiency static dataset)

```javascript
{
  20798: {
    type: 'spirit-shard',    // 'gold' | 'spirit-shard' | 'karma' | 'dungeon-currency'
    quantity: 1,              // Items received per purchase
    cost: 1,                  // Cost per purchase in the given currency
    npcs: [
      { name: 'Miyani / Mystic Forge Attendant', position: 'Mystic Forge' }
    ]
  }
}
```

## Pricing Decision Rules (gw2efficiency algorithm)

1. Start with TP prices from `/v2/commerce/prices` (user picks buy-order or sell-listing mode)
2. `useVendorPrices()` merges vendor prices — overwrites TP price when vendor is cheaper
3. Per recipe-tree node: compare buy price vs sum-of-sub-component craft costs — pick cheaper
4. Owned inventory items subtracted first (free, or with opportunity cost per user toggle)
5. Multiple recipes per item: all evaluated, cheapest selected

## Critical Gap: Vendor Buy Prices

**Vendor buy prices (what vendors charge YOU) are NOT in any official GW2 API.** ArenaNet acknowledged this (developer issue #235) but deprioritized it because:
- Test vendors are indistinguishable from real ones in their data
- Prices vary per vendor, not per item

The `vendor_value` field on `/v2/items` is the sell-to-NPC price (what the NPC pays you), which is unrelated to buy-from-NPC pricing.

gw2efficiency solves this with a static hand-maintained dataset in their MIT-licensed npm package.

## API Rate Limits

- Max 200 IDs per bulk request
- 300 request burst capacity, refills at 5 requests/second
- HTTP 429 returned when exceeded

## Unknowns and Inferences

### Confirmed Facts
- Official GW2 API provides TP prices, recipes, and item metadata
- Vendor buy prices come from a hardcoded static dataset, not from any API
- The `cheapestTree()` algorithm compares buy vs craft per tree node
- Multiple recipes per item are handled by evaluating all and selecting cheapest

### Inferred (High Confidence)
- Currency conversion rates (karma→gold, etc.) are computed from tradable item prices, not from any official rate
- `useVendorPrices()` overwrites TP prices when vendor is cheaper

### Inferred (Medium Confidence)
- Vendor data is maintained manually and updated when game patches change vendor inventories
- The "8x multiplier" for vendor buy prices from vendor_value is a rough community heuristic, not used by gw2efficiency

### Unknown
- Exact completeness of the vendorItems dataset
- How gw2efficiency handles items requiring mixed currencies
- Whether their live backend diverges from open-source packages
- How frequently the static vendor data is updated

## Reference Links

### Official GW2 API
- [API:2/commerce/prices](https://wiki.guildwars2.com/wiki/API:2/commerce/prices)
- [API:2/commerce/listings](https://wiki.guildwars2.com/wiki/API:2/commerce/listings)
- [API:2/items](https://wiki.guildwars2.com/wiki/API:2/items)
- [API:2/recipes](https://wiki.guildwars2.com/wiki/API:2/recipes)

### gw2efficiency Open Source (MIT)
- [recipe-calculation](https://github.com/gw2efficiency/recipe-calculation) — core pricing algorithm + vendor data
- [recipe-nesting](https://github.com/gw2efficiency/recipe-nesting) — recipe tree builder

### Community Discussion
- [Request: Add Vendor buy prices to API](https://gw2developers.github.io/forum-backup/Request-Add-Vendor-buy-prices-to-API-call/) — ArenaNet response on the vendor pricing gap
