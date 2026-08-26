> **Frozen record - 2026-08-16, branch `gate-investigation-receipt-what-if-captions-value`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Gate investigation: receipt/what-if captions + value-detail hover (2026-08-16)

Two live gate findings from tonight's batched desktop session against the
`ui-bundle`/`currency-ux-package` features (both entries earlier in this
file). Both were investigated to the deepest reachable, Blish-free seam via
new real-production-path tests; neither investigation found a code defect.

**Item 1 (GATE FAIL as reported): receipt/what-if captions not rendering on
an override-re-solve.** Live repro: Amalgamated Rift Essence plan, root
manually overridden to VENDOR (the "Decisions updated (1 override(s))"
re-solve via `CraftingPlanPipeline.ResolveWithOverrides`), root expanded
shows 4 synthesized cost-component leaves then 4 dimmed reference-branch
children, with no "Vendor price:"/"If crafted instead:" caption or caption
tooltip on either group's first row.

Traced the full chain the live path exercises: `PlanSolver.Solve` ->
`CraftingPlanPipeline.ResolveWithOverrides` ->
`CraftingPlanPipeline.BuildCraftingTreeResult` ->
`CraftingTreeBuilder.BuildTree` (sets `IsReferenceBranch`/`IsCostComponent`
per node) -> `Services/ReceiptCaptionHelper.cs`'s
`ComputeCaptionSplitIndex`/`CaptionForChildIndex` ->
`Views/Rendering/TreeSectionController.cs`'s three render call sites
(the initial default-expanded build inside `RenderTreeNode`, the lazy
expand/collapse toggle handler, and the Expand All button's lazy-build
loop - the report's own "TWO call sites" undercounts; there are three, and
all three correctly compute `captionSplitIndex` from the parent node once
and thread the right child's caption into `RenderTreeNode`'s `captionText`
parameter) -> `UpdateTreeRowTooltip` -> `rowPanel.BasicTooltipText`. Every
step reads correctly on inspection: `CraftingPlanPipeline.
BuildCraftingTreeResult` passes `currencyMetadata`/owned-amount dictionaries
through to `CraftingTreeBuilder.BuildTree` unchanged on the override path
exactly as the initial generation does; `PlanViewModelBuilder.Build` assigns
`vm.TreeRoot = result.CraftingTree` verbatim (no reordering/cloning of
`Children`); `TreeSectionController.CreateTreeSection` receives that same
list unmodified; `ResetContentPanelToEmpty` fully disposes the previous
render's controls before every rebuild (no stale-panel reuse).

Wrote a new real-path test,
`CraftingPlanPipelineTests.MixedVendorOffer_NotBaselineWinner_ResolveWithOverrides_ProducesReferenceBranchWithValidCaptionSplit`
(`tests/GW2CraftingHelper.Tests/Services/CraftingPlanPipelineTests.cs`),
reproducing the exact live shape: a Craft-baseline item whose recipe is
non-empty, manually overridden to `BuyFromVendor` via
`ResolveWithOverrides` against a 2-kind vendor offer (item + currency).
It asserts `resolved.CraftingTree.IsReferenceBranch`, the 2-leaves-then-
1-reference-child `Children` shape (the same stacking
`CraftingTreeBuilder`'s own
`MixedOfferNode_AlsoHasRecipe_StacksComponentLeavesThenReferenceBranch`
test already locks down for the non-override case), and that
`ReceiptCaptionHelper.ComputeCaptionSplitIndex`/`CaptionForChildIndex`
return the expected non-null split and both caption strings on the
resulting node. **This test passes** - the data and helper layer that
ultimately feeds the tooltip is correct for this exact live scenario.

**Item 2 (investigate, fix if real): value-detail hover not firing on a
CRAFT pill above a currency-valued vendor child.** Live repro: Deldrimor
Steel Ingot x5 root CRAFT pill, subtree contains a Philosopher's Stone
`BuyFromVendor` child priced in spirit shards (curated default 3600
copper/unit, `Models/CurrencyDecisionDefaults.cs`) - so the root's
`DecisionValue` was expected to exceed its `SubtreeCost` and
`ValueDetailTooltipBuilder.TryBuild` was expected to fire, but did not.

The sibling test
`PlanSolverCurrencyValuationTests.ComparisonValue_RollsUpThroughAncestorCraft_MatchesDecisionOnlyExpectation`
already proved the raw `SolverDecision.ComparisonValue` rolls up correctly
through `PlanSolver` for this exact shape (a Craft ancestor over a
currency-valued `BuyFromVendor` child). Added a new test one layer
further down the real chain,
`PlanSolverCurrencyValuationTests.CraftRoot_VendorChildValuedInCuratedCurrency_ValueDetailTooltipFires`,
that walks `PlanSolver.Solve` -> `CraftingTreeBuilder.BuildTree` ->
`ValueDetailTooltipBuilder.TryBuild` for a Deldrimor-shaped tree (craft
root, vendor-only child priced purely in spirit shards, valuation supplied
via `CurrencyValuation.WithDefaults(CurrencyValuation.None)` so the
curated default - not a hand-picked test value - is what is exercised,
matching the live report's own wording). **This test passes on the first
attempt**: `root.SubtreeCost == 0`, `root.DecisionValue == 360000` (100
shards at 3600 copper/unit), and `TryBuild` returns true with all three
expected lines ("Crafting gold price:", "Currencies:", "Optimization
price:"). `CraftingTreeBuilder.BuildNode` does copy
`decision.ComparisonValue`/`decision.TotalCost` onto
`CraftingTreeNode.DecisionValue`/`SubtreeCost` unconditionally for every
decision (`Services/CraftingTreeBuilder.cs` lines 185/188), so the
DecisionValue genuinely folds up vendor-child currency valuations all the
way to a Craft root - this is not the gap.

Also read `TreeSectionController.RenderDecisionPills`' own value-detail
wiring (`Views/Rendering/TreeSectionController.cs` ~1462-1483): it gates
on `spec.Kind == Selected || Locked` and
`node.Decision == Craft || BuyFromVendor`, calls
`ValueDetailTooltipBuilder.TryBuild(node, plan?.VendorCapsByItemId, out
valueDetailText)`, and appends the result onto the pill's own
`BasicTooltipText` - structurally correct on inspection, no defect found.

**Conclusion for both items**: no code defect was found in the reachable
chain (solver -> pipeline -> tree builder -> caption/tooltip helper). Both
new tests are real production-path regression coverage for the exact
reported live shapes and pass cleanly. The residual, un-fixed possibility
for both is either (a) tonight's live session ran against a Blish HUD
build that predated the commits under test in this same session (this
file's own `ui-bundle`/`currency-ux-package` entries both note their gate
was "not yet run live" as of the point they were merged, and several
`fill gate line` merge commits landed the same day), or (b) a genuine
Blish-only rendering/tooltip-binding gap in
`Views/Rendering/TreeSectionController.cs` that is outside this repo's
test-runnable boundary (`Blish_HUD.Controls.Panel.BasicTooltipText` and
the mouse-hover binding that reads it cannot be exercised from an xunit
test per this repo's Blish-free test invariant) - the same constraint
every other UI-adjacent entry in this file already notes. If a future live
session reproduces either miss against a confirmed-current build, the next
step is temporary Blish-side instrumentation (a log line in
`TreeSectionController.RenderTreeNode`/`RenderDecisionPills` recording the
computed `captionText`/`valueDetailText` at build time) rather than further
static tracing, since every reachable real-path test now confirms the data
layer is correct.

Tests: 1673 -> 1675 (2 new: `MixedVendorOffer_NotBaselineWinner_
ResolveWithOverrides_ProducesReferenceBranchWithValidCaptionSplit`,
`CraftRoot_VendorChildValuedInCuratedCurrency_ValueDetailTooltipFires`),
via `dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`.
Both new tests exercise real production entry points (`PlanSolver`,
`CraftingPlanPipeline.ResolveWithOverrides`, `CraftingTreeBuilder`,
`ReceiptCaptionHelper`, `ValueDetailTooltipBuilder`) with real
`VendorOfferStore`/`InventoryReducer` where applicable - no Blish HUD
reference, no fake logic, no fake file I/O. Build:
`dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - clean, 0 errors.
No files on the DO-NOT-TOUCH list (`ModuleLog`, `PlanContentHeightMath`,
`PlanRelayoutMath`, scroll machinery, `VendorBatchSolver` merged-ceil
batching) were edited.

Gate: investigation outcome recorded 2026-08-16 - no code defect found; both live-gate anomalies (captions on override-re-solve, value-detail on a valued-vendor-child craft root) reproduce as PASSING real-path tests at every Blish-free seam (2 new tests); residual is Blish-side render binding or an observation artifact - both visuals re-verify at the next desktop session. Merged under the maintainer's standing merge directive (2026-08-16).
