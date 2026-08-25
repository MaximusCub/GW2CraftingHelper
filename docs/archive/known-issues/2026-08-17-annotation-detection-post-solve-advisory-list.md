## Annotation-detection: post-solve advisory-list characterization tests + B8 shape fixes (2026-08-17)

**Milestone goal:** quorum verdict D-3 (TARGETED_FIX_ONLY). A mutation
deleting all four post-solve annotation-pass calls
(`CompetencyOpportunityCalculator.Apply`/`ExcessCraftOutputCalculator.
Apply`/`RecipeSheetSavingsCalculator.Apply`/`SeasonalVendorTipCalculator.
Apply`) at the multi-item generation site and inside `ResolveWithOverrides`
left the suite green at 1765 - none of the existing coverage asserted on
those four `CraftingPlanResult` properties from either of those two call
shapes, only from the single-item `GenerateStructuredAsync` path. Close
that gap with characterization tests, apply the accompanying B8 shape
fixes, and document the whole annotation-pass group in
`docs/ARCHITECTURE.md`.

**What changed:**
1. **Annotation-detection characterization tests
   (`tests/.../CraftingPlanPipelineTests.cs`).** Six new tests, real
   pipeline paths throughout (no mocked calculators): three pin the exact
   mutation described above -
   `GenerateStructuredAsync_ListOverload_MultiItem_
   PopulatesAllFourAdvisoryLists` (via the public list overload at
   `CraftingPlanPipeline`), `ResolveWithOverrides_SingleItemContext_
   PopulatesAllFourAdvisoryLists`, and `ResolveWithOverrides_
   MultiItemContext_PopulatesAllFourAdvisoryLists` - each asserting
   `NotNull` on all four advisory lists (every one of the four
   calculators unconditionally assigns its own property, empty list or
   not, once called - a plain `NotNull` is therefore a precise, minimal
   wiring proof; calculator CONTENT correctness stays with the dedicated
   `*CalculatorTests` classes, not duplicated here). A fourth test,
   `GenerateStructuredAsync_ListOverload_SingleItem_
   RoutesToSingleItemPath_NotMultiItemWrapper`, pins the list overload's
   own dispatcher invariant (items.Count == 1 routes to the untouched
   single-item overload, never the multi-item wrapper path). A fifth,
   `NullOffersForItemDelegate_NoOffersSource_EmitsNothing_NoCrash`
   (`RecipeSheetSavingsCalculatorTests.cs`), pins the B8 null-delegate
   guard below. **Verified against the actual mutation**: temporarily
   commented out the four `Apply` calls at both sites, rebuilt (0
   errors), ran the full suite - exactly the three targeted tests failed
   (`Assert.NotNull() Failure: Value is null`), the other 1770 stayed
   green - then reverted and confirmed 1773/1773 green again. A sixth
   test, `GenerateStructuredAsync_RecipeSheetSavings_EndToEnd_
   PopulatesOpportunity`, was added later (gate finding 1, 2026-08-17
   correction commit) - see item 3 below.
2. **B8: `SellSideEconomics.ApplyForPlanShape` self-dispatch
   (`Services/SellSideEconomics.cs`).** `ResolveWithOverrides` used to
   branch on `context.Tree.Id != Gw2Constants.MultiItemWrapperItemId`
   itself to pick `ApplySellSideEconomics` vs
   `ApplyBatchSellSideEconomics`. That if/else now lives inside
   `SellSideEconomics` as `ApplyForPlanShape`, using the SAME
   `Gw2Constants.MultiItemWrapperItemId` constant - `ResolveWithOverrides`
   calls the one new method instead of duplicating the shape check.
   **Correction (gate 2026-08-17): not a pure move.** The constant is
   unchanged but the OPERAND is not: the deleted if/else read
   `context.Tree.Id` (the frozen, generation-time tree); the new call
   passes `solveTree`, which is `reduced.ReducedTree` - a fresh
   `InventoryReducer` clone - whenever `context.UnreducedTree != null &&
   _reducer != null`. Equivalent today only because
   `InventoryReducer.CloneNode` copies `Id` onto the clone and the
   wrapper root is never pruned - an invariant nothing asserts. The new
   `tree != null &&` guard does NOT change null-tree behavior: a null
   tree used to throw NRE at `context.Tree.Id`; it still NREs, one frame
   deeper, at `itemRoot.NodeId` inside `ComputePerItemEconomics` (MEASURED
   via a temporary probe test, since reverted). The guard is defensive
   only, and the state is unreachable in production anyway -
   `ResolveWithOverrides` passes the same `solveTree` to `_solver.Solve`
   and `BuildCraftingTreeResult` before this call. See
   `SellSideEconomics.ApplyForPlanShape`'s own doc comment for the
   corrected description.
3. **B8: `RecipeSheetSavingsCalculator.Apply` narrowed to
   `Func<int, IReadOnlyList<VendorOffer>>`
   (`Services/RecipeSheetSavingsCalculator.cs`).** The `vendorOfferStore`
   parameter (a full `VendorOfferStore`) is replaced with `offersForItem`
   - the one method (`GetOffersForItem`) this calculator ever called on
   it. The old `vendorOfferStore != null` gate is now a null-delegate
   guard with the identical meaning ("no offer source available -> emit
   nothing"). `CraftingPlanPipeline` computes the narrowed delegate once
   in its constructor (`_offersForRecipeSheetItem`, null when
   `vendorOfferStore` is null) and threads it to all three call sites.
   Added a defensive `?? Array.Empty<VendorOffer>()` on the delegate's
   return in `TryEmit`: `VendorOfferStore.GetOffersForItem` itself never
   returns null, but the parameter is now caller-suppliable rather than
   bound to that one class's own contract. `RecipeSheetSavingsCalculatorTests.
   cs` keeps its real, temp-directory-backed `VendorOfferStore` fixture
   (repo invariant: real stores with temp dirs, never a fake) - only the
   17 call sites' argument changed, from `vendorOfferStore: store` to
   `offersForItem: store.GetOffersForItem` (a plain delegate over the
   same real store).
   **Correction (gate finding 1, 2026-08-17):** this narrowing moved the
   offer source onto a previously-unpinned `CraftingPlanPipeline` field,
   `_offersForRecipeSheetItem`, computed once in the constructor. Nothing
   in the suite asserted on its content: replacing the whole assignment
   with `_offersForRecipeSheetItem = null;` left all 1773 pre-existing
   tests green (verified), silently disabling every recipe-sheet-savings
   note in production with no crash. Closed with an end-to-end pipeline
   test, `GenerateStructuredAsync_RecipeSheetSavings_EndToEnd_
   PopulatesOpportunity` (real temp-directory `VendorOfferStore` plus a
   non-empty `recipeSheetItemIdByRecipeId`, no fakes), asserting
   `RecipeSheetSavingsOpportunities` is non-empty with correct content,
   not merely `NotNull`. Verified against the mutation: reintroducing
   `_offersForRecipeSheetItem = null;` fails this test; reverting
   restores 1774/1774 green.
4. **`docs/ARCHITECTURE.md` Section 10, "Post-solve annotation passes."**
   New section naming the four calculators and their one-collection-each
   contract, why `SellSideEconomics` is adjacent but not a member (it
   writes displayed totals, not an advisory Notes list), the three
   producer wiring sites plus the fourth (consumer) edit site in
   `PlanViewModelBuilder.BuildNotesSection`, and that the SellSide-first/
   Competency-last call order is convention (kept identical for
   readability), not a data dependency - every pass reads only the
   already-built display tree, never another pass's output. Explicitly
   notes there is no `ApplyAll` seam collapsing the three producer calls
   - rejected on review as premature (quorum verdict D-3): the four
   calculators do not share a signature.

**Validation performed:**
- Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
  C:/Dev/Blish/wt-qannot/GW2CraftingHelper.csproj -p:Platform=x64` - 0
  errors (1782 pre-existing StyleCop warnings, none new).
- Tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test
  C:/Dev/Blish/wt-qannot/tests/GW2CraftingHelper.Tests/
  GW2CraftingHelper.Tests.csproj` - 1774/1774 green (baseline 1768 + 6
  new tests listed above).
- Mutation verification (see item 1 above): the exact judge's mutation
  (deleting all four `Apply` calls at the multi-item and
  override-resolve sites) reproduced by hand, rebuilt, and run - exactly
  the three targeted characterization tests failed, no others; reverted
  and re-confirmed 1774/1774 green.

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests
- [x] Tests exercise real production paths (real `CraftingPlanPipeline`/
  `PlanSolver`/`RecipeService`/`VendorOfferStore` throughout, no mocked
  calculators)
- [x] No fake file I/O tests introduced (`RecipeSheetSavingsCalculatorTests`
  keeps its real, temp-directory-backed `VendorOfferStore`)
- [x] Pricing logic preserves multi-source correctness (no solver/pricing
  logic touched - annotation-pass wiring, a parameter-type narrowing, and
  a dispatch move only)
- [x] IDs remain internal-only (not displayed)

**Risks / follow-ups:** none identified beyond what is already itemized
above; the B8 narrowing's defensive `?? Array.Empty<VendorOffer>()` guard
is currently unreachable from any production caller (only
`VendorOfferStore.GetOffersForItem`, which never returns null, is ever
wired in) - kept as cheap, correct defense-in-depth now that the
parameter type accepts an arbitrary delegate, not dead code removal
material.

Gate: not run live this pass - annotation-calculator wiring and B8
shape fixes carry no new rendered surface; the previously-unpinned
call shapes are now suite-pinned by the characterization tests this
branch adds (the mutation that silently deleted all four annotation
passes now fails the suite). Merged under the maintainer's standing
merge directive (2026-08-16).

---
