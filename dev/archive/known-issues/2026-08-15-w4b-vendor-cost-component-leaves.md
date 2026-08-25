## W4B: Vendor cost-component leaves (2026-08-15)

User-designed after live field observation: an "Amalgamated Rift Essence"
node inside an Endless Summer plan showed a vendor offer costing 3 wallet
currencies PLUS Globs of Ectoplasm (a TP-valued item). `CraftingPlanPipeline.
AugmentWithVendorCostPricesAsync` already TP-values an offer's Item cost
lines and folds them into one effective coin price - correct math, but the
tree row's right side then rendered a very long run (gold-including-
hidden-ectos + 3 currency segments) that collided with the row layout and
hid that part of the "gold" total was actually paid in items, not coins.
Implemented in the isolated `wt-w4b` worktree (stacked on the unmerged
`tree-tooltips` branch, `4095fd2`) on branch `w4b-vendor-cost-leaves`.

**Design.** For any vendor-acquired display-tree node whose winning offer's
price mixes 2+ cost KINDS (coin / non-coin currency / TP-valued item),
`CraftingTreeBuilder.BuildVendorCostComponentLeaves` now synthesizes
DISPLAY-ONLY child leaves - one per non-coin-currency cost line and one
per TP-valued item cost line - instead of the "what it would cost to craft
instead" reference branch that node would otherwise get (component leaves
take precedence; a vendor-only item with no recipe at all, the common
real-world case, was never going to get a reference branch anyway). A
single-kind offer (the overwhelming majority - plain coin, or all-currency,
or all-item-folded-to-coin) is completely unaffected: no leaves, exactly
today's rendering. A raw coin component never gets its own leaf even when
it is one of the 2+ kinds - it stays folded into the parent's compact total
exactly as before, the simplest presentation that still keeps every VISIBLE
leaf's number consistent with the parent. The collapsed parent's cost cell
now shows ONLY that compact gold total (`Views/Rendering/
TreeSectionController.cs` skips the currency-segment run whenever the row's
own children are component leaves) - the collision is gone, and the
breakdown is one click away via the existing expander machinery.

**Data plumbing (all additive, no existing math touched).** The item cost
component's TP-valued gold amount did not previously survive past
`VendorBatchSolver.EvaluateVendorOffers` (an Item cost line was folded
straight into `coinCost` and discarded as a discrete number) - the
merged-ceil batching arithmetic itself (DO-NOT-TOUCH) is unchanged; a new
`VendorItemCostLine` (`ItemId`/`Quantity`/`GoldValue`) is captured at the
EXACT SAME multiplication site that already fed `coinCost`, scaled by the
same `unitsNeeded`, and threaded through `VendorOfferEvaluation` ->
`PlanSolver.Decision.VendorItemCosts` -> `SolverDecision.VendorItemCosts`
(mirroring the existing `VendorCurrencyCosts` passthrough exactly) -> the
display leaf's own `SubtreeCost`. A leaf's gold amount is therefore
literally the same number already folded into the parent's total, never
independently recomputed. `SolverDecision.VendorHasRawCoin` (also additive)
records only whether the winning offer had a genuine raw coin line, so the
"2+ kinds" gate can tell "coin" apart from "item money that became coin"
without re-deriving it from `TotalCost`.

**Synthetic node ids.** Each leaf gets a deterministic, stable, always-
NEGATIVE `NodeId` (`-(parentNodeId * 1000 + componentIndex + 1)`) -
`RecipeNodeIds.Assign` only ever produces small non-negative ids, so
collision is structurally impossible, and the id is stable across a
`ResolveWithOverrides` re-solve of the same tree (the parent's own NodeId
is preserved verbatim, and a leaf's `componentIndex` is a fixed position in
a list built from the same winning offer's own `CostLines`), so expansion
state survives a decision-pill click elsewhere in the tree.

**Decision pills stay decision-free.** Component leaves are facts about a
price, not an acquisition choice: `DecisionPillPlanner.BuildPillSpecs`
checks `CraftingTreeNode.IsCostComponent` FIRST and returns only an
informational HAVE / "HAVE x/y NEEDED" pill (or none) - never a CRAFT/TP/
VENDOR pill, never Ignore, never `Source`-bearing (so it can never be
clicked into an override). The HAVE data itself comes from a NEW,
deliberately separate field, `ComponentOwnedQuantity` - unlike the existing
`OwnedQuantityUsed` (which means "already subtracted from Quantity"), a
component leaf's `Quantity`/cost are NEVER reduced for ownership; owning
some of a cost component is purely informational, read from the account
snapshot's wallet (`AccountCurrencyIndex`) and inventory
(`AccountItemIndex`) via two new pipeline-computed, cosmetic-only
dictionaries (`ownedCurrencyAmounts`, already existed; the new
`OwnedVendorItemAmounts`) that - like their existing `OwnedCurrencyAmounts`
sibling - are never consulted by `InventoryReducer` or `PlanSolver`, so
they cannot affect a decision or a total. `CraftingPlanPipeline` also
widens its one bulk item-metadata fetch to include every vendor Item cost
component id (`AddVendorItemComponentIds`) - those ids are never real tree
ingredients, so without this an item leaf would show "Unknown Item"
instead of "Glob of Ectoplasm".

**Verified separation from the solver.** `CraftingPlanPipeline.
CollectPresetOverrides`/`BuildPresetOverrides` and `PlanSolver.Evaluate`'s
override lookup both walk `RecipeNode`/`RecipeOption` (the SOLVER tree)
exclusively - a component leaf corresponds to no `RecipeNode` at all and is
never fed back into either, so Craft All/Buy All/Best Path and a plain
per-node override click are provably unaffected (a dedicated pipeline test
builds a preset override map and re-solves with component leaves present,
asserting the leaves survive untouched and every produced override key is
`>= 0`, i.e. a real solver id, never a synthetic negative one).

`Models/CraftingTreeNode.cs` gained `IsCostComponent`/`ComponentOwnedQuantity`
- both additive with `false`/`0` defaults, so an old `plan.json` simply
deserializes every existing node with no component leaves (renders exactly
as it did before this milestone) until the plan is regenerated;
`PlanStructuralValidator.IsValidCraftingTreeNode`'s existing recursive
`Children` walk already covers a component leaf with zero changes needed.

New tests (all Blish-free, real production paths - no Blish/BlishHUD/
Gw2Sharp references, no fake file I/O): `VendorBatchSolver`/`PlanSolver`
level tests proving `VendorItemCosts`/`VendorHasRawCoin` populate correctly
for mixed offers and stay null/false for single-kind ones
(`PlanSolverVendorOfferTests`); `CraftingTreeBuilder` tests covering leaf
labels/amounts/flags, parent-total-equals-leaf-value consistency, blank
currency-leaf cost, no leaves for every single-kind offer shape (coin-only,
currency-only, item-only, coin+currency-with-currency-leaf-only), HAVE-pill
coverage (full/partial/none) from owned-amount dictionaries, currency-leaf
name/icon from live `CurrencyMetadata` with the offline fallback, and
stable/collision-free synthetic ids across two builds of the same tree
(`CraftingTreeBuilderTests`); pill-vocabulary tests proving a component
leaf never gets a decision/Ignore pill and only ever the informational
HAVE/OwnedInfo pair (`DecisionPillPlannerTests`); end-to-end pipeline tests
through a real `VendorOfferStore` + `AccountSnapshot` proving the metadata
widening, the owned-amount wiring, a `ResolveWithOverrides` round-trip
preserving leaf NodeIds/values, and the preset-override separation
(`CraftingPlanPipelineTests`); and a real `PlanStore` save/load round trip
proving component leaves survive gzip-compressed persistence and pass
`PlanStructuralValidator` unchanged (`PlanStoreTests`). Full module suite:
1273 baseline + 30 new W4B tests, all green.

**Review-fix round (2026-08-15) - 7 findings from an adversarial review (2
Critical, 4 Must Fix, 1 Must Fix flagged for explicit justification), all
addressed.** The two Critical findings were the same defect surfacing twice:
`VendorItemCosts`/`VendorCurrencyCosts` are captured PRE-merge, per tree
occurrence, by `VendorBatchSolver.EvaluateVendorOffers` - but when the same
vendor item is needed via 2+ tree occurrences that merge into one true
batched purchase (the exact shape the merge-then-ceil machinery exists for),
`AllocateVendorNodeCosts` reallocates each occurrence's corrected
`TotalCost` share WITHOUT re-deriving those raw component numbers the same
way. A component leaf built from them could show a value that no longer
summed to (and could even exceed) its own parent's corrected total - a
reproduced, concrete regression of the exact "two sections of the same page
disagree" defect class the batching correction passes exist to prevent, one
level lower.

- *Fix (Critical x2, `Services/VendorBatchSolver.cs` lines 767/743,
  `Services/CraftingTreeBuilder.cs` line 283).* Added
  `SolverDecision.VendorComponentCostsUnreliable` (additive bool, default
  false) and a new `PlanSolver.FlagUnreliableVendorComponentCosts` pass,
  run immediately after `AllocateVendorNodeCosts` in `PlanSolver.Solve`: it
  marks every occurrence of a step that genuinely merged 2+ tree occurrences
  (`vendorOccurrences[stepKey].Count > 1` AND
  `step.VendorOfferOutputCount > 0`, the same gate `AllocateVendorNodeCosts`
  itself uses to decide whether a step was actually corrected).
  `CraftingTreeBuilder.BuildVendorCostComponentLeaves` now short-circuits to
  "no leaves" whenever this flag is set, regardless of kind count - the
  node still shows its own correctly reallocated `SubtreeCost`, just without
  an unprovable item/currency breakdown. Deliberately kept OUT of
  `VendorBatchSolver.cs` (DO-NOT-TOUCH: merged-ceil batching math) - the new
  pass lives in `PlanSolver.cs`, reads `AllocateVendorNodeCosts`'s own
  already-public inputs/outputs (`vendorOccurrences`, `stepMap`) strictly
  after it returns, and writes only the new auxiliary flag; `VendorBatchSolver.cs`'s
  `AllocateVendorNodeCosts` method body is byte-for-byte unchanged (only its
  doc comment gained a cross-reference). A single-occurrence vendor buy is
  unaffected (nothing was actually reallocated there, so the original
  numbers stay accurate) - every pre-existing W4B leaf test keeps passing
  unmodified. New test: `CraftingTreeBuilderTests.
  MultiOccurrence_MergedMixedVendorOffer_SuppressesComponentLeaves_ParentStaysConsistent`
  reproduces the exact two-occurrence bulk-offer shape from the finding
  (batch size 15, two 6-unit occurrences merging to one true batch) and
  asserts both occurrences get no leaves while their reallocated
  `SubtreeCost` values still sum exactly to the real merged `PlanStep`
  total.

- *Fix (Must Fix, `Services/CraftingTreeBuilder.cs` line 155): reference
  branch silently dropped when a vendor node also got component leaves.*
  The two no longer compete for the same `Children` slot - a vendor node
  whose offer both mixed 2+ cost kinds AND has a known recipe now STACKS
  them: component leaves first (so `TreeSectionController`'s
  `Children[0].IsCostComponent` cost-cell-suppression check keeps working
  unmodified), then the reference branch's own recipe ingredients appended
  as additional, ordinary children. Verified safe to mix: `IsReferenceBranch`
  is purely informational today (not read by any renderer - grepped), and
  per-child dimming already comes from the PARENT's `Decision != Craft`
  uniformly, not from any per-child reference-branch flag, so a mixed
  `Children` list renders exactly as consistently dimmed as either kind
  alone. `CraftingTreeNode.IsReferenceBranch`'s doc comment updated to
  record the now-possible mixed case. New test: `CraftingTreeBuilderTests.
  MixedOfferNode_AlsoHasRecipe_StacksComponentLeavesThenReferenceBranch`.

- *Fix (Must Fix, `Services/VendorBatchSolver.cs` line 300): missing
  `Count > 0` guard on the Item cost-line capture.* A zero/negative-count
  Item cost line (malformed wiki-scraped seed data) could invent a phantom
  "item" cost KIND, flipping an otherwise single-kind offer into
  leaf-synthesis mode with a 0-quantity/0-gold ghost leaf. Guarded the raw
  `itemCostRaw` capture with `cost.Count > 0`, mirroring the raw-coin
  branch's own identical guard a few lines above it; `coinCost` itself is
  untouched (a Count of 0 already contributed nothing to it). New test:
  `PlanSolverVendorOfferTests.ZeroCountItemCostLine_DoesNotPopulateVendorItemCosts`.

- *Fix (Must Fix, `Services/CraftingPlanPipeline.cs` line 859):
  `ResolveWithOverrides` metadata gap for a non-baseline-winning vendor
  offer.* `ResolveWithOverrides` never re-fetches metadata (by design - it
  is purely local, no network calls); the pre-existing
  `AddVendorItemComponentIds` only scanned the BASELINE winning decisions'
  `VendorItemCosts`, so a node whose original decision was Craft - later
  manually overridden to `BuyFromVendor`, an ordinary and commonly-used
  interaction - could surface an item component leaf whose id was never
  widened into `PlanSolveContext.Metadata`, rendering "Unknown Item" with
  no icon until the whole plan was regenerated. Added
  `AddAllVendorOfferItemComponentIds`, called at both generation entry
  points (`GenerateStructuredAsync`/`GenerateStructuredMultiAsync`) right
  alongside the existing decisions-only widening: it scans every `Item`
  cost line on EVERY vendor offer already fetched for the tree (not just
  the winning one), using data already resident in memory from the
  existing `vendorOffers` fetch - no extra network round trip, and
  `ResolveWithOverrides` itself needed no change. New test:
  `CraftingPlanPipelineTests.
  MixedVendorOffer_NotBaselineWinner_ResolveWithOverrides_StillResolvesRealItemMetadata`.

- *Fix (Must Fix, `tests/.../CraftingTreeBuilderTests.cs` line 1160):
  missing 3-distinct-currency single-kind coverage.* Added
  `SingleKindVendorOffer_ThreeDistinctCurrencies_NoItemNoCoin_CountsAsSingleKind_NoLeaves`,
  locking down that `kindCount` counts by
  `decision.VendorCurrencyCosts.Count > 0` (a boolean per KIND) rather than
  per distinct currency id, so an offer spanning 3 different non-coin
  currencies still gets no leaves - a deliberate design choice that was
  previously unverified by any test.

- *Justified, not changed (`Services/VendorBatchSolver.cs` line 333):
  the `itemsScalable`/`continue` overflow guard added inside
  `EvaluateVendorOffers`.* Flagged as new control flow inside a DO-NOT-TOUCH
  method. Kept as-is: it is structurally identical to - and only extends to
  a second cost dimension - the pre-existing `scalable`/`continue` guard a
  few lines below it for currency lines (same file, same loop, same
  overflow-safety shape, predates this feature), so it introduces no new
  KIND of control flow. It can only fire when a single occurrence's scaled
  Item-cost quantity exceeds `int.MaxValue` - unreachable with real GW2
  data. Rewriting it as a clamp instead (silently truncating the
  represented cost) would be the actual behavior change, and a strictly
  worse one - a clamped value is silently wrong, while skipping the offer
  fails safe exactly like its currency sibling already does. Documented
  inline with this reasoning for future reviewers.

Validation: `dotnet build -p:Platform=x64` clean (0 errors); full module
test suite green - 1308 passed (1273 baseline + 30 original W4B + 5 new
review-fix tests: 3 in `CraftingTreeBuilderTests`, 1 in
`CraftingPlanPipelineTests`, 1 in `PlanSolverVendorOfferTests`). No new
Blish HUD references in tests; every new test exercises real production
code (`PlanSolver.Solve`, `CraftingTreeBuilder.BuildTree`,
`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`) with
no contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only. `VendorBatchSolver.cs`'s DO-NOT-TOUCH merged-ceil methods
(`EvaluateVendorOffers`, `FinalizeVendorBatches`, `AllocateVendorNodeCosts`,
`MergeVendorCurrencyCosts`, `VendorBatchesEqual`, `ScaleCostLines`) had
their dollar-amount arithmetic (coin costs, ceil/batch selection,
allocation shares) left byte-for-byte unchanged throughout this round - the
only edits inside that file are a `Count > 0` capture guard and doc
comments, both confirmed by diff review to touch no cost computation.
Performance note: the new `AddAllVendorOfferItemComponentIds` scan runs
once per plan generation (not a render/UI hot path) over `vendorOffers`
data already resident in memory from the pre-existing fetch; its only
allocation is additional entries in the `metadataIds` HashSet that was
already being built, no new collection type.

**Review-fix round 2 (2026-08-15) - 2 Must Fix findings from a further
adversarial review, both the same shape as round 1's fixes one field/map
over, both addressed.**

- *Fix (Must Fix, `Services/VendorBatchSolver.cs` line 292): missing
  `Count > 0` guard on the non-coin Currency cost-line capture.* Round 1
  guarded the Item cost-line capture (`itemCostRaw`) against a
  zero/negative-count line inventing a phantom "item" cost KIND, but left
  the identical `currencyCosts.Add(cost)` five lines above it unguarded -
  a zero/negative-count non-coin Currency cost line could still invent a
  phantom "currency" cost KIND the same way, flipping an otherwise
  single-kind offer into leaf-synthesis mode with a 0-quantity (or, for a
  negative Count, negative-quantity) ghost leaf: blank cost, no pill. Now
  guarded with `if (cost.Count > 0)`, mirroring both the raw-coin branch's
  own guard immediately above it and the Item-line guard below it;
  `coinCost` is untouched either way. New test:
  `PlanSolverVendorOfferTests.ZeroCountCurrencyCostLine_DoesNotPopulateVendorCurrencyCosts`
  (sibling to round 1's `ZeroCountItemCostLine_DoesNotPopulateVendorItemCosts`).

- *Fix (Must Fix, `Services/CraftingPlanPipeline.cs`
  `BuildOwnedVendorItemComponentAmounts`): the ownership map was never
  widened the way metadata was.* Round 1 widened metadata fetching
  (`AddAllVendorOfferItemComponentIds`) so a vendor offer NOT chosen at
  baseline still resolves a real name/icon after a manual
  `BuyFromVendor` override via `ResolveWithOverrides`. The parallel
  ownership computation, `BuildOwnedVendorItemComponentAmounts`, was left
  scoped to only the BASELINE winning decisions'
  `VendorItemCosts` (via `AddVendorItemComponentIds` alone) - so the same
  override scenario surfaced a correctly-named item component leaf with
  permanently NO have pill, even with the item sitting in the account,
  until the whole plan was regenerated. `PlanSolveContext.
  OwnedVendorItemAmounts` is, like `Metadata`, captured once at generation
  time and passed to `ResolveWithOverrides` verbatim - never recomputed.
  Widened `BuildOwnedVendorItemComponentAmounts` to also call
  `AddAllVendorOfferItemComponentIds` over `vendorOffers` (reusing the
  round 1 method rather than duplicating its scan), at both call sites
  (single-item and multi-item generation entry points). Extended test:
  `CraftingPlanPipelineTests.
  MixedVendorOffer_NotBaselineWinner_ResolveWithOverrides_StillResolvesRealItemMetadataAndOwnership`
  (renamed from round 1's `...StillResolvesRealItemMetadata`, now also
  attaching a snapshot with partial ownership of the item component and
  asserting `ComponentOwnedQuantity` survives the override re-solve).

Validation: `dotnet build -p:Platform=x64` clean (0 errors); full module
test suite green - 1309 passed (1308 from round 1 + 1 new round-2 test;
the round-2 ownership fix extended an existing test rather than adding a
new one). No new Blish HUD references in tests; both new/extended tests
exercise real production code (`PlanSolver.Solve`,
`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`)
with no contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only. `VendorBatchSolver.cs`'s DO-NOT-TOUCH merged-ceil methods
had their dollar-amount arithmetic left byte-for-byte unchanged - the only
edit inside that file this round is the one `Count > 0` capture guard
(plus its doc comment). `ResolveWithOverrides`/`BuildPresetOverrides`
themselves needed no change in either fix - both walk the SOLVER tree via
`solveResult.Decisions`, not the display tree, and neither fix touches
decision-making, only the cosmetic metadata/ownership maps consulted
afterward when building display leaves.

**Review-fix round 3 (2026-08-15) - 1 Must Fix finding: the round-2
ownership widening covered the ITEM component map only and missed its
exact currency-side sibling.**

- *Fix (Must Fix, `Services/CraftingPlanPipeline.cs`
  `BuildOwnedCurrencyAmounts`): the currency ownership map was never
  widened the way the item one was.* Round 2 widened
  `BuildOwnedVendorItemComponentAmounts` to scan every vendor offer's Item
  cost lines (`AddAllVendorOfferItemComponentIds`), not just the baseline
  winning decisions - but `BuildOwnedCurrencyAmounts` still keyed its
  dictionary strictly off `plan.CurrencyCosts`, the baseline plan's
  aggregated currency totals. Same failure shape as round 2, currency
  side: a node whose baseline decision is Craft, manually overridden to
  `BuyFromVendor` via `ResolveWithOverrides`, surfaces a currency
  cost-component leaf with a correct name/icon/quantity but permanently NO
  have pill, even with a full wallet, because that currency id was never
  in `plan.CurrencyCosts` and `PlanSolveContext.OwnedCurrencyAmounts` is
  captured once at generation time and reused verbatim (never
  recomputed) by `ResolveWithOverrides` - exactly the reuse-verbatim
  argument that justified the round-2 item-side fix. Added
  `AddAllVendorOfferCurrencyComponentIds`, the currency-side twin of
  `AddAllVendorOfferItemComponentIds` (same non-coin-Currency /
  `Count > 0` filter `VendorBatchSolver.EvaluateVendorOffers` itself
  uses), and widened `BuildOwnedCurrencyAmounts` to scan `vendorOffers`
  through it in addition to `plan.CurrencyCosts`, at both call sites
  (single-item and multi-item generation entry points). Harmless for the
  pre-existing currency summary rows (`PlanViewModelBuilder`), which only
  ever look up the ids they themselves iterate from `plan.CurrencyCosts` -
  extra keys in the returned map are simply never read by that caller.
  Extended test: `CraftingPlanPipelineTests.
  MixedVendorOffer_NotBaselineWinner_ResolveWithOverrides_StillResolvesRealItemMetadataAndOwnership`
  now also attaches a wallet entry for the offer's non-coin currency
  component and asserts `ComponentOwnedQuantity` on the currency leaf
  survives the override re-solve, the currency-side sibling of the
  existing item-leaf assertion in the same test.

Validation: `dotnet build -p:Platform=x64` clean (0 errors); full module
test suite green - 1309 passed (unchanged count - the round-3 fix
extended the same existing test the round-2 fix had already extended,
rather than adding a new one). No new Blish HUD references in tests; the
extended test exercises real production code (`PlanSolver.Solve`,
`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`)
with no contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only. `VendorBatchSolver.cs`'s DO-NOT-TOUCH merged-ceil methods
were not touched at all this round (the fix is entirely inside
`CraftingPlanPipeline.cs`, a cosmetic ownership-annotation map consulted
strictly after solving). `ResolveWithOverrides`/`BuildPresetOverrides`
themselves needed no change: `BuildPresetOverrides` walks `context.Tree`
(the solver's `RecipeNode` tree) and never references
`OwnedCurrencyAmounts` at all - confirmed by reading it. `ResolveWithOverrides`'s
own decision-making call, `_solver.Solve(context.Tree, ...)`, is likewise
never given `OwnedCurrencyAmounts` and cannot see it; that map is only
passed, read-only, into `BuildCraftingTreeResult` afterward, which
annotates the DISPLAY tree's already-decided leaves with a HAVE pill - it
cannot feed back into `solveResult.Decisions`, so this fix cannot affect a
solver decision, only the cosmetic HAVE pill a display leaf reads
afterward.

**Pre-gate addendum (2026-08-15): "OWN n"/"CURRENCY" badge wording pass.**
Maintainer field-testing found the component leaves' informational HAVE/
"HAVE x/y NEEDED" pill (Section "Decision pills stay decision-free" above)
misleading: that vocabulary means "your stock covers this need and reduced
the plan cost" everywhere else in the tree, but a component leaf's
ownership never reduces this line's cost (see `ComponentOwnedQuantity`'s
own doc comment) - it is purely informational.
`DecisionPillPlanner.BuildPillSpecs`'s `IsCostComponent` branch now shows a
subdued "OWN n" badge instead (n = the raw `ComponentOwnedQuantity` holding, no full-vs-partial
split since coverage never changes the cost either way), rendered only
when n > 0 (no "OWN 0" clutter) - reusing `PillKind.OwnedInfo` (the same
muted-gold kind the ordinary partial-ownership annotation already uses, no
new color). Also added: a "CURRENCY" badge (`PillKind.Locked`, the SAME
kind/text the plain currency-ingredient leaf's own pill already uses a few
lines above in `BuildPillSpecs`) on the currency-type component shape
(`SubtreeCost` never set - the "deliberately blank cost cell"
`BuildVendorCostComponentLeaves`' currency-line branch produces),
explaining at a glance why no gold value is shown - gw2efficiency's own grey Currency-
badge pattern. The two badges are independent and may both appear on one
leaf; `TreeSectionController.RenderDecisionPills` needed no layout change
- both badge strings are short ("CURRENCY" plus "OWN n") and comfortably
fit the existing 240px pill-column budget together (the pill column
already fits the old, longer "HAVE x/y NEEDED" text alongside a source
pill - see the `RealSolver_*`/`PartialOwnership_*` tests above), so
`PlanRelayoutMath.ComputeVisiblePillCount` (untouched) never needs to drop
either one in practice.

Regular (non-component) currency-ingredient leaves already carried the
identical "CURRENCY" / `PillKind.Locked` badge before this pass (the
`CraftingDecision.Currency` short-circuit in `BuildPillSpecs`) - the
vocabulary-consistency extension this addendum considered for those leaves
was therefore already in place with zero additional diff.

Tooltips follow the existing pattern exactly (`BasicTooltipText` stamped on
all three of `outer`/`inner`/`label` together in
`TreeSectionController.RenderDecisionPills`, per that method's own "tooltip resolved once... then
stamped onto outer/inner/label together" comment - a tooltip on `outer`
alone is swallowed by `inner`/`label`, which cover almost the entire pill).
"OWN n"'s tooltip: "You own {n} - informational only, does not change the
plan cost". "CURRENCY"'s tooltip (component-leaf case only; the ordinary
currency-ingredient leaf's ambient "Only available source" default is
unchanged): "Paid in a non-coin currency - no gold value to show here".
The now-dead `IsCostComponent` branch of the `PillKind.Have` tooltip (a
component leaf can no longer reach that `Kind` at all) was simplified back
to the plain-node wording only.

Updated: `Models/CraftingTreeNode.cs` (`IsCostComponent`'s and
`ComponentOwnedQuantity`'s doc comments), `Services/CraftingTreeBuilder.cs`
(`ResolveOwnedQuantity`'s doc comment), `Services/DecisionPillPlanner.cs`,
`Views/Rendering/TreeSectionController.cs`. `DO-NOT-TOUCH` files (`Services/
ModuleLog.cs`, `Services/PlanContentHeightMath.cs`, `Services/
PlanRelayoutMath.cs`, scroll machinery, `VendorBatchSolver`'s merged-ceil
vendor batching math) were not touched. `DecisionPillPlannerTests` updated:
3 existing cost-component tests renamed/adjusted for the new "OWN n"
wording (no more full/partial HAVE split), plus 3 new tests covering the
CURRENCY-badge threshold (blank vs. non-null `SubtreeCost`) and the two
badges coexisting in emission order (CURRENCY first, then OWN). Full module
suite: 1312 passed (1309 baseline + 3 net new).
`dotnet build -p:Platform=x64` clean (0 errors). No new Blish HUD
references in tests; the extended/added tests exercise the real
`DecisionPillPlanner.BuildPillSpecs` production code, no contract-mirror/
fake-logic tests. Item/currency/vendor IDs remain internal-only (badge text
is `"OWN n"`/`"CURRENCY"` only, never an id).

Live desktop gate: Gate: PASS 2026-08-16 (orchestrator live sandbox session, combined wave-4 staging build). Verified on the real Amalgamated Rift Essence vendor path: component leaves render under the vendor-selected root (ecto leaf's gold share exactly equals the parent's collapsed total; three currency leaves blank-cost with CURRENCY badges), OWN badges show the RAW wallet holding (300/150/100 against 250/100/50 needs) after the gate-found clamp fix in this branch, no OWN badge at zero holding, manual override to VENDOR re-solves and the overridden state survives module restart via the persisted plan, tree-button tooltips render live (Best Path text verified verbatim). Known composition note: component leaves and the dimmed what-crafting-would-cost reference branch both render under a vendor root with a recipe - as designed; a visual separator is a queued UX question.
