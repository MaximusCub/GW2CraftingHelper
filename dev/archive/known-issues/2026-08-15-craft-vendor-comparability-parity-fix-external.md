> **Frozen record - 2026-08-15, branch `craft-vendor-comparability-parity-fix-external`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Craft/vendor comparability parity fix - external review, fourth-site finding (2026-08-15)

An external review of the two passes above found one more defect in
`Services/PlanSolver.cs`, in the coin-typed-currency-ingredient carve-out
added by the adversarial-review follow-up pass (the "all three sites now
agree" fix documented above).

**The asymmetry.** That earlier fix made a `Currency`-type ingredient
tagged with `Gw2Constants.CoinCurrencyId` (real copper paid directly as
part of a recipe, not a currency needing a user valuation) contribute to
`decision.TotalCost` via `Evaluate`'s recipe loop and
`RecomputeCraftCosts`, and confirmed it reached the Recipe Tree
(`CraftingTreeNode.SubtreeCost`, sourced from `memo`/`Decisions`) and the
Crafting Steps shopping-list row (`RefreshCraftStepCosts`, which sums
`decision.TotalCost` per craft-step occurrence). It did not reach
`plan.TotalCoinCost` - the Total Cost summary band. That total is built
by summing only `BuyFromTp`/`BuyFromVendor` step costs (deliberately,
to avoid double-counting a Craft step's already-recursive total against
its own Buy-step children) - a coin-typed currency ingredient has no
Buy step of its own, so its copper fell through that sum entirely. The
same reproduction the earlier fix's own test used demonstrates it: a
recipe costing 10 copper (a TP-bought sub-ingredient) plus 50 copper
(a coin-typed currency ingredient) reported `decision.TotalCost == 60`
(Recipe Tree, Crafting Steps row) but `plan.TotalCoinCost == 10` (Total
Cost summary band) - the same "two sections of the same page disagree"
defect class the M34 fix (fcbb277) eliminated for the vendor-batch
correction passes, now reintroduced for this one ingredient shape.
Confirmed latent rather than live on this branch: no seeded recipe in
`ref/recipes_seed.json` currently carries a `Currency` ingredient tagged
with the coin id, but the whole premise of the fix this defect was found
in is that the pending ~188-recipe ingest may bring that shape in, so it
would have shipped armed.

**Fixed (mustFix): `plan.TotalCoinCost` now includes coin-typed currency
ingredients.** `Collect`'s Currency-node handling no longer special-cases
the coin id with an early return; it now folds into `currencyMap` via
the exact same per-tree-occurrence accumulation every other currency
already uses (visited once per occurrence, matching how
`Evaluate`/`RecomputeCraftCosts` already count it exactly once per
occurrence - no double count introduced). `Solve`'s plan-building step
then routes that one `currencyMap` key into `totalCoinCost` (instead of
the ordinary `BuyFromTp`/`BuyFromVendor`-step sum, which it has no step
to be caught by) and excludes it from `plan.CurrencyCosts`, so it still
never double-displays as a "currency 1" line - preserving the original
fix's own display intent, just reaching all four sites instead of three.
No other `currencyMap` consumer is affected:
`VendorBatchSolver.FinalizeVendorBatches` only ever writes non-coin
vendor currency lines into `currencyMap` (vendor's own coin cost is
already routed straight into a Buy step's coin cost, never into
`currencyMap`, at `VendorBatchSolver.cs` ~230-240), so it can never
collide with or double-count the new coin key.

This also corrects `SellSideEconomics`' profit calculation
(`NetSaleValue - Plan.TotalCoinCost`), which previously would have
overstated profit by exactly the hidden coin-ingredient amount for any
plan carrying one - not a separate fix, a direct consequence of
`TotalCoinCost` now being correct.

**Tests**: extended the existing
`CoinTypedCurrencyIngredient_IsRealCoin_NeverDemotesRecipeToFallback`
test (same fixture, no new test method - the finding was that this
exact test's own scenario proved the bug once `plan.TotalCoinCost` was
inspected) with an assertion that `plan.TotalCoinCost == 60` (previously
would have been `10`) and that `plan.CurrencyCosts` never carries a
`CoinCurrencyId` entry. Net test count unchanged (1288) from the prior
pass.

Validation: `dotnet build -p:Platform=x64` clean (0 errors, no new
warnings from any touched file). Module test suite green - 1288 passed
(unchanged from the prior pass's count; no new test methods, one
existing test extended with additional assertions). No new Blish HUD
references in tests; the extended test exercises real production code
(`PlanSolver.Solve` end-to-end). Item/currency/vendor IDs remain
internal-only. Pricing logic continues to preserve multiple sources and
avoid inventing currency exchange rates - this fix corrects a real-coin
total, not a valuation-derived one, so it does not touch the
decision-only valuation principle.

Gate: PASS 2026-08-16 (live sandbox session, combined wave-4 staging build). Verified: Amalgamated Rift Essence offers CRAFT and VENDOR side by side with CRAFT winning honestly on the real priced portion (50 vs 60 ectos, identical currency lines washing out); manual VENDOR override honored and re-solved; fallback-tier vendor behavior observed live on a shard-priced vendor path in the Zojja plan. Coin-typed currency TotalCoinCost routing covered by the suite (no live coin-ingredient recipe in the gate scenarios).
