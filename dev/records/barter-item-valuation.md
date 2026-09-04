> **Milestone record - 2026-08-28, branch `barter-item-valuation`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Barter-item valuation: the vendor offers the solver was throwing away (barter-item-valuation)

`VendorBatchSolver.EvaluateVendorOffers` set `priceable = false` and broke
out of its cost-line loop the moment an `Item` cost line had no Trading
Post price, discarding the whole offer. An account-bound item has no TP
price by definition, and account-bound tokens are exactly what barter
vendors take, so the module reported "no vendor route" for items that are
genuinely purchasable - just not with gold.

**Measured, not assumed.** Over the shipped `ref/vendor_offers.json`
(59,414 offers) against `/v2/commerce/prices` and `/v2/items`, 2026-08-28:

| | |
|---|---|
| offers carrying at least one `Item` cost line | 19,763 |
| distinct item ids appearing as costs | 1,032 |
| of those, tradeable and priced fine | 378 |
| of those, no TP price at all | 654 |
| item cost-line usages covered by the 654 | 10,551 of 21,489 (49%) |
| concentration | top 5 = 41.6%, top 20 = 54.3%, top 50 = 65.7%, top 100 = 77.1% |

One figure recorded in advance did not survive checking. The claim that
every one of the 654 is flagged `AccountBound` is 628 of 654. Of the
remaining 26, 23 are `SoulbindOnAcquire` rather than `AccountBound`, and
3 (25720, 84356,
102962) no longer resolve on `/v2/items` at all. None is tradeable, so the
conclusion holds; the wording did not.

**Characterize first.** `PlanSolverBarterItemValuationTests` landed as a
separate commit ahead of any production change, pinning that such an offer
was dropped from BOTH tiers, that its coin part went with it, and that a
second, fully-priced offer for the same item still won. Green on the
unchanged solver, then converted in the behaviour commit - so the diff of
that file IS the statement of what moved.

**What moved.** An unpriced `Item` line is now a BARTER line and obeys the
rule a non-coin currency line already obeyed: valued, it folds into the
offer's comparison value (never into the committed coin cost); unvalued, it
routes the offer into the existing fallback lane. `CurrencyValuation` grew
an item-keyed table beside its currency-keyed one - two tables and not one
tagged key, because a GW2 currency id and a GW2 item id are different id
spaces that collide numerically (currency 39 and item 39 are unrelated
things), and a tagged key would have rewritten the persisted JSON and every
call site for no behavioural gain.

**The invariant, argued rather than assumed.** "Pricing logic must preserve
multiple sources and avoid invalid currency comparisons" forbids inventing
a comparison and forbids dropping one line of a multi-line cost. This
change does the opposite of both: an unvalued barter line makes the whole
offer non-comparable via the fallback lane rather than being silently
dropped (the old code dropped the offer, which threw away its coin line
too), and a valuation is explicit and user-editable - precisely what
`CurrencyValuation` already is for currencies. The DECISION-ONLY boundary
is unmoved: no valuation ever reaches a displayed gold total.
`Services/CostLineValuation.cs` keeps the strict "skip rather than guess"
posture, deliberately - its two callers have no valuation, no
comparable/fallback split and nowhere to report an incomparable cost, so
the strict rule is right there and the two must not be made to converge.

**The sweep.** Three consumers read "empty `VendorCurrencyCosts`" as "this
coin figure is the whole cost", which a barter node breaks:
`RecipeSheetSavingsCalculator` (direct and recursive), then
`SeasonalVendorTipCalculator`, then `TreeRowTooltipComposer`, which would
have printed "Unit price: 0c" for an item that really costs tokens - the
same field-test finding a pure-currency offer already had to suppress.
All three now check `VendorHasBarterItemCost`, and each guard has a test
that was confirmed red with the guard stubbed out before being restored.

**Defaults, and the two deliberate exclusions.** `BarterItemDecisionDefaults`
carries 26 entries, each derived under one stated rule: the cheapest
repeatable exchange in `ref/vendor_offers.json` whose entire cost is coin
or a currency that already has a `CurrencyDecisionDefaults` value. The rule
is conservative by construction - it can only name a route we can see, so
each value is an upper bound on the real acquisition cost, and an
over-valued token makes its offer look dearer, never cheaper. The Black
Lion family (2,365 + 1,359 + five smaller rows, ~35% of affected usages) is
left unvalued on purpose: gem-store RNG-chest currency whose gold worth is
personal, the posture Astral Acclaim already gets. So are the Grandmaster
Marks, which were expected to be valuable: their chain bottoms out in
`Glob of Dark Matter` plus daily time-gated crafts, so no route satisfies
the rule, and inventing one would have been exactly the thing the invariant
forbids.

**Known gap, recorded not fixed.** A pure-barter shopping row renders the
gw2e-style unpriceable dash rather than naming the tokens it needs. That is
honest (the row genuinely has no coin price) and the tree's synthesized
component leaf does name the token and its quantity, but the shopping list
has no barter equivalent of `VendorCurrencyCosts` and giving it one is its
own display task.
