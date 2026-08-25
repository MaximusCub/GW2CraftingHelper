## Value-detail hover investigation, pipeline-level follow-up (value-detail-pipeline, 2026-08-17)

Follow-up to "Gate investigation: receipt/what-if captions + value-detail
hover (2026-08-16)" above: that entry's Item 2 traced the value-detail
hover only as far as `PlanSolver.Solve -> CraftingTreeBuilder.BuildTree ->
ValueDetailTooltipBuilder.TryBuild` (the seam test) and found no defect,
but the live miss reproduced again on two separate desktop builds after
that entry was merged - a stronger signal than "stale build", so this
pass went one layer further down: the full `CraftingPlanPipeline.
GenerateStructuredAsync` path the seam test does not model at all (VOM
force-buy pre-pass, `InventoryReducer`, real vendor-offer-store lookups,
`ModuleSettings.GetEffectiveCurrencyValuation()`'s actual return value on
a fresh settings state).

Reproduced a simplified analogue of the live shape end to end (2 levels,
single vendor occurrence - NOT the live tree's actual depth, which
matters for the untested factors listed at the bottom of this entry): a
craft root (Deldrimor Steel Ingot-style, quantity 5) whose recipe has a
vendor-only child priced
purely in spirit shards (currency 23, curated default 3600 copper/unit,
`Models/CurrencyDecisionDefaults.cs` line 109) plus an ordinary TP-priced
sibling, `OwnMaterialsMode.Valued` with a real `AccountSnapshot` owning
some of the SIBLING (not the vendor child - reduction never touches the
node the divergence comes from), through `CraftingPlanPipeline.
GenerateStructuredAsync` itself (fake HTTP fixtures, real
`VendorOfferStore`/`InventoryReducer`, matching this file's established
pattern). New test:
`CraftingPlanPipelineTests.GenerateStructuredAsync_
CraftRootWithVendorChildValuedInCuratedCurrency_VomOn_
ValueDetailTooltipFires`. **Passes on the first run**: `root.SubtreeCost
== 140` (real coin only, the sibling's un-owned 7 units at the InstantBuy
basis' sell price of 20), `root.DecisionValue == 360140` (the same 140
plus the vendor child's 100 shards x 3600 copper/unit), and `TryBuild`
returns true with all three expected lines carrying those exact figures.

Checked the four live factors the prior entry's seam test could not
exercise, all confirmed not to be the gap:
- (a) `ModuleSettings.GetEffectiveCurrencyValuation()` is exactly
  `CurrencyValuation.WithDefaults(GetCurrencyValuation())`
  (`Services/ModuleSettings.cs` line 329); on a fresh/empty persisted
  state `GetCurrencyValuation()` deserializes to `CurrencyValuation.None`
  - byte-for-byte the same valuation the test constructs.
- (b) VOM's force-buy pre-pass, zero-owned guide solve, and
  `InventoryReducer` all ran (`useForceBuyPrePass` requires
  `OwnMaterialsMode.Valued` + a non-null snapshot + a non-null reducer -
  all three supplied).
- (c) `PlanSolver.RecomputeComparisonValues` and the vendor-currency
  reallocation pass both ran as part of the real `Solve()` call inside
  the pipeline (not bypassed) and produced the correct rolled-up
  `DecisionValue`.
- (d) the snapshot's owned quantity sits on the sibling, confirmed not to
  touch the vendor child's own reduction.

Went one step further than the prior entry: also reproduced the case
where the root's pill is genuinely `PillKind.Selected` (2+ options,
craft beating an intentionally-uncompetitive TP price) rather than
`PillKind.Locked` (single option) - the prior entry's seam test left the
root single-option, whose own base tooltip would actually read "Only
available source", not the "Current source: CRAFT" wording the live
report quoted (`Views/Rendering/TreeSectionController.cs`'s
`spec.Kind == PillKind.Selected` branch, line ~1381, is the only site
that produces that exact wording). New test:
`CraftingPlanPipelineTests.GenerateStructuredAsync_
CraftRootSelectedAmongMultipleOptions_ValueDetailTooltipFires` - asserts
`DecisionPillPlanner.BuildPillSpecs` returns a `CRAFT` pill with
`PillKind.Selected` (the same Blish-free data `TreeSectionController`
consumes to pick a render branch) AND that `TryBuild` fires. **Also
passes.** Since the append gate at
`TreeSectionController.RenderDecisionPills` (line ~1490) calls
`ValueDetailTooltipBuilder.TryBuild(node, ...)` on the SAME `node`/`spec`
already established as Selected/Craft in that same loop iteration where
the base "Current source: CRAFT" tooltip was just set two branches
above, and `TryBuild` is a pure function of that node's own fields, a
live miss on this exact wording requires the node reaching line 1490 live
to carry different `SubtreeCost`/`DecisionValue`/
`VendorComponentCostsUnreliable` values than the ones
`GenerateStructuredAsync` produces for THIS shape - which the tests above
rule out only for this shape, not for the live tree.

**Conclusion: correct-by-design for the shape modelled here; no code
defect found in it.** Both tests pass, so the data layer (solver through
`CraftingTreeNode`) is correct for a shallow craft-over-valued-vendor-
child tree at every depth an xunit test can reach.

This does NOT clear the whole pipeline, and the live behaviour has NOT
been verified either way - no live capture was taken during this pass.
Two suppression paths inside `ValueDetailTooltipBuilder.TryBuild` itself
remain untested for a Craft ROOT, and both would produce exactly the
reported symptom:

- **Fallback-tier propagation (the strongest untested candidate).**
  `PlanSolver.RecomputeComparisonValues` (line 2443) sets
  `ComparisonValue = TotalCost` whenever `decision.HasUnvaluedCurrency`,
  and that flag propagates transitively up through every Craft ancestor
  (line 1061). One unvalued currency or `GuildUpgrade` ingredient
  ANYWHERE in the chosen subtree therefore forces `delta == 0` on the
  root and suppresses this hover - the scope limit already documented in
  `ValueDetailTooltipBuilder.cs` lines 26-36. A real Deldrimor Steel
  Ingot tree is far deeper than the 2-level fixture used here and can
  easily contain one. The only existing test on this path
  (`PlanSolverCurrencyValuationTests.
  MixedCoinValuedUnvaluedFallbackOffer_ComparisonValueMatchesTotalCost_
  NoTooltip`) covers a FLAT vendor leaf, never the ancestor rollup.
- **`VendorComponentCostsUnreliable`.** Set by
  `FlagUnreliableVendorComponentCosts` on every occurrence of a vendor
  step merged across 2+ tree occurrences. No test anywhere passes a node
  with this flag true to `TryBuild`. It lands on vendor nodes rather than
  Craft ancestors, so it is the weaker candidate for a root-pill miss,
  but it is untested.

Next step if a third live repro occurs: rule out fallback-tier
propagation FIRST (a test with an unvalued-currency ingredient buried
under the craft root, asserting whether the root hover survives), since
that is a cheap Blish-free test and a genuine code-level explanation.
Only if that comes back clean is Blish-side instrumentation warranted - a
log line in `TreeSectionController.RenderDecisionPills` at line ~1490
recording `node.ItemId`, `node.Decision`, `node.SubtreeCost`, `node.
DecisionValue`, `node.VendorComponentCostsUnreliable`, and the `TryBuild`
return value at the moment of the live render.

Tests: 1768 -> 1770 (2 new:
`GenerateStructuredAsync_CraftRootWithVendorChildValuedInCuratedCurrency_
VomOn_ValueDetailTooltipFires`,
`GenerateStructuredAsync_CraftRootSelectedAmongMultipleOptions_
ValueDetailTooltipFires`), via `"/mnt/c/Program Files/dotnet/dotnet.exe"
test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`. Both
new tests exercise real production entry points
(`CraftingPlanPipeline.GenerateStructuredAsync`, real `VendorOfferStore`/
`InventoryReducer`, `DecisionPillPlanner`, `ValueDetailTooltipBuilder`) -
no Blish HUD reference, no fake logic, no fake file I/O. Build:
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-valuedetail/GW2CraftingHelper.csproj -p:Platform=x64` -
clean, 0 errors (pre-existing StyleCop warnings only, none in either
touched file). No files on the DO-NOT-TOUCH list (`ModuleLog`,
`PlanContentHeightMath`, `PlanRelayoutMath`, scroll machinery,
`VendorBatchSolver` merged-ceil batching) were edited - only a test file
and this doc.

Gate: not run live this pass - test-and-docs change with no runtime
code touched; pipeline-level behaviour is suite-pinned (mutation-checked
per the review record above) and the live hover re-check stays on the
next desktop gate batch, where fallback-tier propagation is the first
thing to rule out. Merged under the maintainer's standing merge
directive (2026-08-16).
