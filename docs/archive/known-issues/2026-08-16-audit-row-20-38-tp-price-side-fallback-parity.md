## AUDIT ROW 20/38: TP price-side fallback parity (2026-08-16)

**Bug**: `PlanSolver.GetUnitPrice` returned 0 whenever the selected
price basis's preferred TP side (buy orders / `SellInstant`, or
instant-buy / `BuyInstant`) had no listings for an item, even when that
SAME item's OTHER side had a real, usable price. `GetBuyCost`'s
`unitPrice > 0` check then treated the item as fully unpriceable,
dropping the BuyFromTp option entirely - an item with an empty
preferred side became unpurchasable in the plan, forcing it to Craft
(if a recipe existed) or Unknown. Confirmed against gw2efficiency's own
live bundle (verified twice first-hand): preferred side first, cross-
side fallback to the same item's other side when the preferred side is
missing/zero, unpriced only when BOTH sides are empty.

**Fix**: `PlanSolver.GetUnitPrice` gained a 3-arg overload
`(ItemPrice price, PriceBasis priceBasis, out bool priceSideFellBack)`
that tries the basis-preferred side first and falls back to the item's
other side only when the preferred side is 0; the existing 2-arg
overload now delegates to it (`out _`), so its remaining external
caller (`CraftingPlanPipeline.CollectPresetOverrides`'s Buy-All
feasibility check, which only ever needs the `> 0` priceable check, not
the fell-back fact) gains the fallback automatically with no call-site
change. `GetBuyCost` (the only caller inside `PlanSolver.Evaluate`)
captures the out param into a local `buyPriceSideFellBack`, which
`Commit` folds into a new `Decision.PriceSideFellBack` field gated to
`src == AcquisitionSource.BuyFromTp` (always false for Craft/
BuyFromVendor/UnknownSource commits). `VendorBatchSolver.
EvaluateVendorOffers`'s Item cost-line pricing calls the same 3-arg
overload directly and carries the fact into `VendorItemCostLine.
PriceSideFellBack`. The merged-ceil vendor batching math itself
(`VendorBatchSolver`'s `FinalizeVendorBatches`/`AllocateVendorNodeCosts`)
was not touched anywhere in this history - only the per-item unit price
it already multiplies by can now be a fallback-side number.

**Display**: `CraftingTreeNode.PriceSideFellBack` (read by the
recipe-tree row tooltip in `Views/Rendering/TreeSectionController.cs`)
has three producers, all set by `CraftingTreeBuilder.BuildNode`:

1. A plain `BuyFromTp` node - copied straight from `SolverDecision.
   PriceSideFellBack`. The flag describes THIS node's own TP price.
2. A `BuyFromVendor` cost-component leaf (`IsCostComponent`,
   `BuildVendorCostComponentLeaves`) representing a TP-valued Item
   barter line - copied from that line's own `VendorItemCostLine.
   PriceSideFellBack`. The flag describes THIS leaf's own price, not
   the parent vendor node's.
3. A `BuyFromVendor` node itself - the OR across every one of its
   `VendorItemCosts` lines' own `PriceSideFellBack`, set whenever the
   node's `Source` is `BuyFromVendor` and `VendorItemCosts` is
   non-null, regardless of whether that same offer also produced
   cost-component leaves (a 2+-cost-kind offer gets both a flagged
   parent and a flagged leaf with no double-counting - they are
   separate tree nodes rendering separate tooltip lines). This flag
   describes one of the node's VENDOR COST ITEMS falling back, not the
   node's own item - a `BuyFromVendor` node was never priced on the TP
   at all.

The tooltip renders one of TWO DIFFERENT sentences depending on which
case set the flag, chosen by `_getCurrentPlan()?.PriceBasis` (threaded
through via `CraftingPlanResult.PriceBasis` -> `PlanViewModel.
PriceBasis`, `PlanViewModelBuilder.Build`):

- Cases 1 and 2 (`node.Decision == CraftingDecision.BuyFromTp ||
  node.IsCostComponent`) get "Buy-order price unavailable - instant-buy
  price shown" (or the InstantBuy-basis reverse) - accurate here,
  because the flag genuinely describes this row's own price.
- Case 3 (a plain `BuyFromVendor` node, i.e. not itself a
  cost-component leaf - checked as an explicit "not case 1/2" carve-out
  since a leaf's own `Decision` is always `BuyFromVendor` too) gets a
  distinct sentence naming the component instead of the row: "A vendor
  cost item's buy-order price is unavailable - its instant-buy price is
  used" (or the reverse). Reusing the case-1/2 sentence here would
  assert THIS row's item has no buy orders, which is false in general -
  the row's own item may have a perfectly healthy TP presence, or no TP
  presence at all; only one of its vendor cost items fell back.

Shown regardless of the existing `Quantity > 1` unit-price-line gate,
since the caveat concerns which TP side priced a value rather than
whether a separate per-unit line is useful. The shopping list row
tooltip (`PlanViewModelBuilder.BuildShoppingListSection`) does **not**
carry either caveat - it would need `PriceSideFellBack` threaded through
`PlanStep` and `PlanSolver.Collect`'s per-step-key merge across
possibly-multiple tree occurrences; recorded as an open follow-up, not
implemented, out of scope for this change.

`docs/ARCHITECTURE.md` section 8 ("Solver decision rules") was reworded
to match: the basis is *preferred per item*, with a same-item other-side
fallback when the preferred side has no listings, cross-referenced to
this section by name. A single item is never priced on a mixed basis -
but a TOTAL summed across several items (e.g. a craft cost built from
multiple ingredients) CAN combine sides when a fallback fires on one of
them; the earlier absolute claim that "the solver never compares one
item's buy-order price against a different item's sell-listing price"
was false once totals are considered and has been removed.

**Tests** (all exercise real `PlanSolver.Solve` / `CraftingTreeBuilder.
BuildTree` / `PlanViewModelBuilder.Build` production code paths via each
test file's existing real-solver helpers - no contract-mirror or
fake-logic tests anywhere in this change):

- `PlanSolverPriceBasisAndOverrideTests.cs`:
  `BuyOrderBasis_NoBuyOrders_FallsBackToInstantBuyPrice` (fallback
  chosen, replaces the old bug-asserting
  `BuyOrderBasis_NoBuyOrders_ItemNotPriceable`),
  `BuyOrderBasis_BothSidesEmpty_ItemNotPriceable` (both sides empty
  stays unpriceable), `BuyOrderBasis_UsesBuyOrderPrice`
  (`PriceSideFellBack == false` when no fallback is needed),
  `BuyOrderBasis_VendorItemBarter_BarterItemFallsBackToOtherSide`
  (fallback reaches `VendorBatchSolver`'s per-item pricing),
  `BuyOrderBasis_CraftWinsOverFallbackPricedBuy_DecisionFlagStaysFalse`
  and
  `BuyOrderBasis_VendorWinsOverFallbackPricedBuy_DecisionFlagStaysFalse`
  (the `src == BuyFromTp` gate stays closed when Craft/BuyFromVendor
  wins instead), and
  `BuyOrderBasis_FallbackPricedBuyWinsOverCraft_SourceIsBuyFromTp`
  (pins that a fallback-priced buy must WIN a real three-way comparison
  on cost, not merely be available when nothing else competes).
- `CraftingTreeBuilderTests.cs`:
  `LeafBuyNode_PriceSideFellBack_ReachesCraftingTreeNode` (flag reaches
  the tree node), `CraftNode_WinsOverFallbackPricedBuy_
  PriceSideFellBackStaysFalseOnNode` and `VendorNode_
  WinsOverFallbackPricedBuy_PriceSideFellBackStaysFalseOnNode` (gate
  stays closed on the winning node when Craft/BuyFromVendor beats a
  fallback-priced buy), `MixedOffer_ItemCostPreferredSideEmpty_
  LeafFlagsPriceSideFellBack` and its negative sibling `MixedOffer_
  ItemCostPreferredSidePresent_LeafPriceSideFellBackFalse` (both assert
  the leaf AND the parent, case 2 and case 3 together for a 2-kind
  offer), `SingleKindVendorOffer_ItemOnly_FallsBackToOtherSide_
  ParentFlagsPriceSideFellBack` and its negative sibling
  `SingleKindVendorOffer_ItemOnly_NoLeaves` (case 3, `kindCount==1`,
  no leaves at all), and `MultiOccurrence_MergedVendorOffer_
  ItemFallsBackToOtherSide_ParentFlagsPriceSideFellBack` (case 3 under
  `VendorComponentCostsUnreliable` batch reallocation).
- `PlanViewModelBuilderSummaryTests.cs`:
  `Build_PriceBasisBuyOrder_PassedThroughToViewModel` and
  `Build_PriceBasisInstantBuy_PassedThroughToViewModel` (the
  `PriceBasis` passthrough feeding the tooltip's sentence choice is
  actually asserted, not just assumed).

`Views/Rendering/TreeSectionController.cs` itself remains untested -
Blish-bound UI code, outside this repo's Blish-free test-runnable
surface (same constraint every other UI-adjacent entry in this file
notes). The exact tooltip sentence text - both the original pair and
the case-3 vendor-component pair - is verified by code inspection only,
not by an automated assertion; the trigger CONDITION under which some
line is added (as opposed to its exact wording) is fully covered by the
tree-node-level tests above, since `CraftingTreeNode.PriceSideFellBack`
is the only input the renderer's `if`/`else if` branches read.

**Self-review findings** (consolidated across eight adversarial review
rounds over this change; the findings below are the ones that produced
a real fix - many further confirmations of no-regression were also
recorded across those rounds and are not repeated individually here):
confirmed `PlanStructuralValidator.cs`'s NRE-semantics comment for
`GetUnitPrice`'s unchecked field access stays accurate under the new
overload; confirmed `PlanViewModelBuilder.Build` is the sole production
`PlanViewModel` construction site, so the `PriceBasis` passthrough is
never left at its `InstantBuy` default by a different code path;
confirmed `RenderTreeNode` is the sole recursive tree-row renderer, so
the tooltip logic automatically covers nested/reference-branch rows;
confirmed no existing test fixture silently depended on the old
"empty side = fully unpriceable" behavior; confirmed the widened
case-3 parent flag (OR across `VendorItemCosts`, unconditional on
whether leaves were also built) can never regress cases 1/2 - it is a
pure OR-widening, never removing a previously-reachable `true`; confirmed
`decision.VendorItemCosts != null` is checked before `.Any(...)` in
every case-3 site, so a coin/currency-only `BuyFromVendor` decision
(no item cost lines at all) cannot NRE; confirmed the round-8 tooltip
wording split covers the exact same trigger SET as the single-sentence
version it replaced (case 1 OR case 2 OR case 3, unchanged) - only
which of two sentences fires per case changed, not whether any line
appears at all; confirmed `VendorBatchSolver`'s merged-ceil batching
arithmetic (`FinalizeVendorBatches`/`AllocateVendorNodeCosts`/the
`unitsNeeded` scaling) was never touched anywhere across all eight
rounds - every change here only ever read or carried an
already-computed boolean alongside it; grepped every file this change
touched for non-ASCII bytes and for an em dash character - none found;
grepped every added/edited test file for "Blish"/"BlishHUD"/"Gw2Sharp"
- none found, all test files stay Blish-free and exercise real
production entry points; confirmed exactly one `Gate: [PENDING`
occurrence remains in this file (this section's own, at the true EOF).

Nice-to-have (recorded, not applied - out of scope for this change):
the shopping-list row tooltip still does not carry either caveat (see
Display above); a `SolverDecision`-level precomputed
`VendorItemPriceSideFellBack` field could replace the inline `.Any()`
scan `CraftingTreeBuilder.BuildNode` re-runs per node, but `VendorItemCosts`
lists are always small (single digits of cost lines per real vendor
offer) and adding a new `SolverDecision` field would be an unrequested
new abstraction; a small script wrapping `dotnet build -t:Rebuild` with
an automated before/after warning-count diff over `git diff
--name-only` would make "no new StyleCop warnings" a mechanically
verified claim instead of a manually-run one each round.

- Follow-up (shopping-list caveat asymmetry): the missing-caveat gap
  above is not symmetric between the two bases. Under
  `PriceBasis.InstantBuy`, if an item's `BuyInstant` side has zero
  listings, the same per-item fallback swaps in its `SellInstant` (buy-
  order) price instead - a price no seller is currently offering, only
  what buyers are bidding. The shopping list still renders that item as
  a flat `Buy` row at that coin figure with no caveat, which reads as an
  instantly-fillable price when it is not one - a buy order posted at
  that figure still has to wait for a seller to fill it, and the true
  instant-fill cost (if any seller exists at all) could be higher. The
  reverse direction (`PriceBasis.BuyOrder` falling back to `BuyInstant`)
  does not have this problem the same way: the fallback number there IS
  an instantly-fillable price, just not the preferred one. Recorded as
  an explicit follow-up alongside the general shopping-list gap above,
  not implemented, out of scope for this change.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64 -t:Rebuild`
- clean, 0 errors. StyleCop warning-code histogram for both files this
final round touched (`Views/Rendering/TreeSectionController.cs`,
`Models/CraftingTreeNode.cs`) is byte-for-byte identical before and
after (verified via `git stash`/rebuild/`git stash pop`, comparing
warning-code counts, not just line-by-line text, since every added
comment line shifts subsequent line numbers) - zero new warnings
anywhere in either file. Tests: 1383 passed, 0 failed (`dotnet test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`) - this
final round changed tooltip wording and doc comments only, no new
`[Fact]` added or removed, so the total is unchanged from the prior
round. No Blish HUD/BlishHUD.exe references in any test file; every
test exercises a real production entry point, no contract mirrors. IDs
remain internal-only; coin icons unaffected throughout this entire
history (pricing/tooltip logic only, no coin-rendering code touched).
No live desktop verification was performed at any point in this
history - `TreeSectionController.cs` is Blish-bound and outside this
repo's test-runnable surface, same constraint every UI-adjacent entry
in this file notes.

Gate: PARTIAL PASS 2026-08-16 (orchestrator live desktop session). Fallback pricing exercised implicitly throughout live plans; the caveat tooltip's specific fallen-back shape did not occur in the tested plans - suite-covered, visual slice deferred.
