> **Frozen record - 2026-08-15, branch `craft-vendor-comparability-parity-fix-adversarial`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Craft/vendor comparability parity fix - adversarial review follow-up (2026-08-15)

A second, adversarial pass over the fix above (Code Reviewer Mode, per
this repo's mandatory Edit -> Review -> Fix loop) found five defects, all
in `Services/PlanSolver.cs`'s recipe loop and terminal fallback branch.
Four were fixed; one is a flagged, deliberately-unfixed heuristic
limitation (documented below rather than fixed: a genuinely debatable
large fix, flagged instead of expanding scope).

**Fixed (critical): cross-tier scale mismatch at the terminal fallback
comparison.** The terminal tie-break compared `bestFallbackCraftCost` (a
ComparisonValue that could include valued-currency valuation copper)
against `fallbackVendorCoinCost` (real coin only, by the donor's own
"discard valuation once not allValued" design) - two different scales.
Fixed by ranking the craft fallback tier itself on `craftRealCost`
(never the valuation-tainted `craftCost`) and comparing that against
`fallbackVendorCoinCost` at the terminal branch - both sides are now
real coin only, exactly mirroring `EvaluateVendorOffers`' own
fallback-vs-fallback ranking. `bestFallbackCraftCost` and
`bestFallbackCraftRealCost` are now always assigned the same value for
a fallback-tier recipe, so a fallback decision's returned
`ComparisonValue` can never smuggle hidden valuation to a parent either
(closes the same class of leak as the propagation fix below).

**Fixed (mustFix): mixed valued/unvalued currency on one recipe no
longer partially contaminates the fallback ranking.** A recipe's
valuation contribution (`valuationCopper`) is now accumulated separately
from `craftCost` and only folded in when the recipe stays comparable
(`!hasUnvaluedCurrency`) - mirrors `EvaluateVendorOffers`' identical
`valuationCopper`/`allValued` split byte-for-byte. Previously a valued
line's copper was added directly into `craftCost` inline, so a LATER
unvalued line on the same recipe demoted it to fallback without ever
retracting the earlier contribution. Covered by
`RecipeWithBothValuedAndUnvaluedCurrency_DiscardsValuation_RanksOnRealCostOnly`
(also exercises the cross-tier fix above - the two defects compounded on
the same code path).

**Fixed (mustFix): fallback-tier taint now propagates transitively
through ancestor Craft decisions.** `Decision` gained an internal
`HasUnvaluedCurrency` bool (never surfaced on the public
`SolverDecision` - purely a tier-tracking aid, same scope as
`ComparisonValue`), set true at every fallback-tier `Commit` call site.
The recipe loop now ORs a chosen ingredient's own
`HasUnvaluedCurrency` into `hasUnvaluedCurrency` after evaluating it, so
a recipe with NO currency ingredient of its own but that consumes an
ingredient whose OWN decision is fallback-tier is itself demoted to
fallback too. Without this, a currency cost hidden one Craft level down
would "launder" back into a fully-comparable-looking ancestor - the
transitive shape of the exact asymmetry this whole fix exists to close.
Covered by
`FallbackTaintPropagatesThroughAncestorCraft_NeverLaundersHiddenCurrencyCost`.
This also uncovered two pre-existing `PlanSolverVendorOfferTests` (
`VendorCurrencyCosts_MergedAcrossDeduplicatedOccurrences`,
`VendorCurrencyCosts_MergeOverflow_ClampsRatherThanWraps`) that
inadvertently relied on the OLD (buggy) non-propagating behavior to
force an intermediate item to craft via a fallback-vendor-sourced
ingredient despite having its own real TP price - updated in place
(remove that intermediate item's TP price so the fallback craft is still
exercised as the last resort) using the same established pattern already
applied to the 3 pre-existing tests fixed in the base pass above; their
real subject (VendorCurrencyCosts merging across tree occurrences) is
unaffected.

**Fixed (mustFix): a `Currency`-type ingredient tagged with the coin
currency id is now treated as real copper, not an unvaluable currency.**
`Models/CurrencyValuation.cs` hard-throws if ever keyed on
`Gw2Constants.CoinCurrencyId`, so without a dedicated branch a
coin-typed ingredient could never be valued and would unconditionally
demote its recipe to the fallback tier - turning a data quirk (GW2's
v2/recipes ingredients can carry `IngredientType: "Currency"` tagged
with the coin id itself) into a wrong decision. Mirrors
`EvaluateVendorOffers`' identical coin-vs-currency routing
(`VendorBatchSolver.cs` ~230-240): the ingredient's `Quantity` is added
directly to both `craftCost` and `craftRealCost`, no valuation lookup
involved. This fix has two sibling sites that needed the identical
carve-out and were found only because the first regression test written
against the primary fix failed (`RecomputeCraftCosts`, which
re-derives every Craft decision's `TotalCost` bottom-up AFTER
`Evaluate`'s initial commit and previously skipped ALL Currency-type
ingredients unconditionally, silently stripping the coin contribution
back out; and `Collect`, whose top-of-method Currency-node handling
previously folded a coin-typed ingredient into `currencyMap` -
`plan.CurrencyCosts` - alongside genuine non-coin currencies, which
would have mis-tagged real copper as currency id 1 and double-reported
it against the coin already counted in `TotalCost`). All three sites
now agree. Covered by
`CoinTypedCurrencyIngredient_IsRealCoin_NeverDemotesRecipeToFallback`.

**Fixed (adversarial-review self-catch, not in the original finding
list): the new `craftCost = checked(craftCost + valuationCopper)` fold
could throw an uncaught `OverflowException`.** `craftCost` (from
non-currency ingredients) and `valuationCopper` could each individually
stay within `long` range while their sum overflows - the original
inline `checked` add (pre-existing code, one accumulator) caught this at
the point of addition; splitting the accumulation into two variables
(the finding-5 fix above) moved the final combine outside any
try/catch. Wrapped in the same try/catch-and-demote-to-fallback pattern
used everywhere else in this loop for absurd valuation input, rather
than letting a crafting-tree with an extreme currency valuation crash
the whole `Solve()` call.

**Flagged limitation (finding 4, deliberately not fixed - flagged rather
than expanded into a large, debatable fix): the
terminal fallback tie-break can let a vendor offer with a near-zero
coin part beat a craft fallback with a materially higher real coin
cost, even though the vendor's true total cost (its own large unvalued
currency line) is unknown and could be higher.** Concretely: a craft
fallback costing 500 real copper loses to a vendor fallback offer
costing 0 coin + 500,000 units of an unvalued currency, purely because
0 <= 500. This is NOT a scale mismatch (both sides are real coin, after
the finding-1 fix above) and not new unsoundness introduced by this
milestone: it is the identical heuristic `EvaluateVendorOffers`' own
frozen fallback-vs-fallback ranking already uses today (rank by
coin part alone, since currency is unknowable/incomparable across
offers - see that method's doc comment), now visible in a new pairing
(craft vs. vendor) that never existed before this milestone added a
craft fallback tier at all. Rejecting a low-coin-part vendor offer in
favor of craft would require inventing some notion of "this coin part
isn't a meaningful proxy for total cost," which is exactly the kind of
currency-exchange-rate judgment the repo invariant (avoid inventing
currency comparisons) forbids, and the vendor-side ranking rule is
explicitly frozen pattern-donor code this milestone mirrors
rather than redesigns. The deliberate choice is documented here (not
just left silent)
and locked by a dedicated regression test,
`AllFallback_VendorZeroCoinPart_BeatsHigherRealCraftCost_DocumentedLimitation`,
so a future change to this heuristic is a conscious decision rather than
an untested drift.

**Tests**: 4 new regression tests added to
`PlanSolverCraftVendorComparabilityTests.cs` (one per fixed finding,
plus the flagged-limitation lock), for 15 new tests total in that file
since the base pass; 2 additional pre-existing `PlanSolverVendorOfferTests`
updated in place (see the transitive-propagation entry above) for 5
pre-existing tests updated in place across this whole milestone.

Validation: `dotnet build -p:Platform=x64` clean (0 errors, no new
warnings from any touched file). Module test suite green - 1288 passed
(was 1273 baseline; 1284 after the base pass; +4 new
`PlanSolverCraftVendorComparabilityTests` in this follow-up, 2 more
pre-existing tests updated in place, net test count change +4 from the
base pass's 1284). No new Blish HUD references in tests; every
new/updated test exercises real production code (`PlanSolver.Solve`
end-to-end, real `RecipeNode`/`RecipeOption`/`VendorOffer` fixtures), no
contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only. Pricing logic continues to preserve multiple sources and
avoid inventing currency exchange rates.
