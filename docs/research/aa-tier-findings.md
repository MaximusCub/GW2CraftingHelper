# Astral Acclaim package - tier investigation (report-only)

Research-only, dev-time. No solver/model code was changed based on this
note; it documents what the wiki/scrape actually encode for Wizard's
Vault-style tiered pricing, per the task's instruction to report rather than
force data. All findings measured live against `wiki.guildwars2.com`'s SMW
`action=ask` API and raw wikitext (`action=raw`) on 2026-07-22.

## Question

The research report `research-aa-spending-consensus.md` establishes that
only "Bag of Coins (1 Gold)" among Wizard's Vault stock is genuinely tiered
(N units at a discount price, then unlimited units at a higher price - a
>4x cliff, 1,250 vs 286 copper/AA). This note asks: how does the wiki/our
scrape actually *represent* that tiering, and is the tier-1 quantity limit
queryable the same way a normal seasonal cap is?

## Finding: two tiers = two separate wiki item pages/game IDs, not one row

The tiering is **not** encoded as a single vendor-table row with two prices.
It is two entirely separate GW2 items with distinct wiki pages and distinct
game IDs:

| Wiki item page | Game ID | AA cost | Seasonal cap | Source page (`Has vendor`) |
|---|---|---|---|---|
| `Bag of Coins (1 Gold) (limited)` | 100878 | 8 AA | `[100]` (queryable via `Has seasonal purchase cap`) | `Wizard's Vault` (current live page) |
| `Bag of Coins (1 Gold) (unlimited)` | 100595 | 35 AA | `[]` (uncapped - confirmed empty array, not a missing key) | `Wizard's Vault/Historical Astral Rewards` |

Each is its own independent `{{vendor table row}}` template call, each with
its own `Sells item` subject, so `WikiSmwClient`'s existing per-subobject
scrape captures both as two ordinary, unrelated-looking `VendorOffer` rows
(same as any other pair of vendor offers) - there is no "tier" relationship
visible in the data model at all, and none needs to be added: `PlanSolver`
already treats these as two independent purchase options for the same
output item (whichever is cheaper wins under the existing vendor-offer
comparison), which happens to reproduce "buy the discount tier first" for
free once both rows are seeded, with no tier-aware code required.

## Finding: the tier-1 quantity limit IS queryable - via the normal seasonal-cap property

Tier 1's "N units at the discount price" limit is exactly the same
`Has seasonal purchase cap` SMW property used for every other Wizard's Vault
cap (Mystic Coin, Mystic Clover, Obsidian Shard, etc.) - `100` for the
`(limited)` row. No separate "tier quantity" property exists or is needed;
the existing daily/weekly/seasonal-cap parsing added by this package already
threads it through unchanged.

## Finding (documentation gap, not a data-modeling gap): the two rows currently live on different pages

As of 2026-07-22, only the `(limited)` tier-1 row appears in the live
`Wizard's Vault` page's raw wikitext (`{{vendor table row | item = Bag of
Coins (1 Gold) (limited)| per season=100 | cost = 8 Astral Acclaim}}`,
confirmed via `action=raw`). The `(unlimited)` tier-2 continuation row is
*not* present on that page at all today - it exists only as an archived
entry on `Wizard's Vault/Historical Astral Rewards#vendor3`, even though the
tier-2 deal is still live and purchasable in-game (per the spending-consensus
report's own confirmation that both tiers are current). This looks like a
wiki editorial gap (the community wiki may simply not have re-added the
"and after that, 35 AA unlimited" row to the current page's table), not
something our scraper is failing to find - both rows exist and are captured;
they just come from two different `merchantName` values in the seed
(`Wizard's Vault` for the discount tier, `Wizard's Vault/Historical Astral
Rewards` for the continuation tier). No code should special-case this: it is
exactly the kind of thing a scoped `--query "[[Has vendor::~Wizard's
Vault*]]"` re-scrape will pick up automatically (from whichever page(s) the
wiki has it on) the next time either page changes.

## Finding: no other Wizard's Vault item is genuinely tiered

Every other capped Wizard's Vault row (Mystic Coin, Mystic Clover, Obsidian
Shard, Vision Crystal, Bag of Laurels, Lucent Crystal Vault Bag, Tome of
Knowledge, and the one-time `Has total purchase cap` items like the
Legendary Weapon Starter Keys) appears exactly once in the live page's raw
wikitext, each with a single `per season=N` or `per character=` parameter -
no second row, no "unlimited continuation" language for any of them. This
independently confirms the spending-consensus report's claim that Bag of
Coins is the only genuinely two-tier item in the current store.

## Conclusion for future work

No model or code change is needed to represent tiering: `VendorOffer` +
`SeasonalCap` (this package) already captures both Bag of Coins rows
correctly as independent offers with their own real caps/costs, and
`PlanSolver`'s existing cheapest-offer selection already produces
"prefer the discount tier" behavior without any tier-aware logic, the same
way it already handles ordinary multi-vendor price competition for any
other item. A future Astral Acclaim budget-allocation feature (per the
addendum's "ranked deal table" design) can read both rows directly; it does
not need a new "tier" concept in the seed data.
