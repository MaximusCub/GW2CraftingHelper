> **Frozen record - 2026-08-16, branch `source-selection-simplification-competency-aware`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Source selection simplification: competency-aware default + subdued losing pills (2026-08-16)

Approved redesign (docs/gw2e-considerations.md context, the
merged Plan Notes/competency machinery). Two independent rules on branch
`source-selection-simplification`:

**Rule 1 - competency-aware default.** A Craft source now only wins the
AUTOMATIC buy-vs-craft-vs-vendor comparison when some character actually
has one of the winning recipe's Disciplines at its MinRating
(`CraftCompetencyEvaluator`, a pure/testable Services class). Snapshot
data absent (`characterDisciplines == null`) is competency UNKNOWN and
never penalizes craft - byte-identical to pre-existing behavior for every
caller that doesn't pass the new parameter.

*Seam decision:* studied how `PickCheapest` (pure economics) and
`DecisionPillPlanner` (a pure mirror of `node.Decision`, no independent
logic of its own) divide responsibility, then implemented the check
INSIDE `PlanSolver.Evaluate`, folded into the SAME
`craftExcludedFromAutoPick` flag the M34-B2a #3 force-buy pre-pass
already uses (excludes Craft from the automatic `PickCheapest`/terminal-
fallback race; `canCraft` and the manual-override branch both read the
UNMODIFIED `bestComparable`/`bestFallback` values, so `CanCraft`/the
CRAFT pill and a manual override are both unaffected). This is cheaper
and more surgical than a separate throwaway-solve prepass (mirroring
`OwnedMaterialsForceBuyPrePass`'s own mechanism) would have been: the
winning recipe's Disciplines/MinRating are already in scope at the exact
point `craftExcludedFromAutoPick` is read, with zero extra solves.
Additionally gated on `(canBuyTp || canBuyVendor)` - a node whose ONLY
feasible source is Craft still auto-crafts regardless of competency,
since excluding it there would drop real, priced cost data out of the
plan entirely (UnknownSource) rather than merely changing a default.
`CraftingPlanPipeline` threads `characterDisciplines` into every
`Solve()` call for a generation, including the zero-owned guide solve
(so `InventoryReducer` never discounts ingredients for a Craft path
competency will end up overriding) and into `ResolveWithOverrides`' local
re-solves via the already-existing `PlanSolveContext.CharacterDisciplines`
field.

**Rule 2 - subdued losing pills.** A non-selected, multi-option pill that
decisively loses to the selected pill renders subdued (reuses
`PillKind.Locked`'s exact muted-gray color via a new `PillKind.Subdued`
case - no new colors) with a tooltip
explaining why, under two independently-checked rules:
- **StrictDomination** (checked first - a stronger, valuation-free
  claim): the losing option's raw coin and every currency/item cost-line
  kind are each >= the selected option's (missing kind on either side
  reads as 0), with at least one strictly greater. Needs NO valuation at
  all - covers the canonical Amalgamated Rift Essence
  shape (vendor needs the same coin, 10 more raw Globs of Ectoplasm than
  crafting does) with a real end-to-end test through the actual solver.
- **Weighted**: both options' fully-valued decision-value figures are
  non-null and the losing one is strictly greater ("more expensive at
  your current currency values"). Any strictly-positive margin counts -
  a pill only reaches this comparison at all when it is one of 2-3 real
  offered choices, so an objectively (if narrowly) worse valued option is
  still worth flagging rather than under-reporting it behind an invented
  percentage threshold (no such threshold was specified).
- Unvalued AND non-dominated (a genuine tradeoff, e.g. less of one kind,
  more of another) leaves both pills normal, per spec.
- The selected pill is never evaluated as a "losing" candidate at all -
  structurally impossible for it to be subdued.

*Seam decision:* `PlanSolver.Evaluate` now computes a raw
`PillSourceCostBreakdown` (RawCoin + non-coin currency/item cost lines,
raw quantities, never gold-valued for the raw-comparison fields) for
EVERY feasible source at a node - not just the winner - mirroring
`costDiagnostics`' own "always computed, never filtered by decision"
precedent, attached to `PlanSolver.Decision`/`SolverDecision`/
`CraftingTreeNode` via the SAME winner-agnostic passthrough chain
`CanCraft`/`CanBuyTp`/`CanBuyVendor` already use. Vendor's breakdown
reuses `VendorBatchSolver`'s own already-evaluated
`VendorCurrencyCosts`/`VendorItemCosts`/coin-cost output verbatim
(`VendorBatchSolver.cs` itself - the merged-ceil math - was never
touched, per the DO-NOT-TOUCH list); Craft's breakdown decomposes the
candidate recipe's DIRECT (non-recursive) ingredient list, which needs
no pricing/recursion since domination compares raw ingredient quantities
by id, the same granularity `VendorItemCostLine.Quantity` already uses.
Detection itself (`PillSubduingEvaluator`) is a pure, Blish-free,
directly-testable Services class operating only on two
`PillSourceCostBreakdown` values - never reads a `CraftingTreeNode`,
never resolves a name, never decides which pill is selected
(`DecisionPillPlanner`'s own job). Tooltip TEXT is built by a second
pure class (`PillSubduingTooltipBuilder`, mirroring the pre-existing
`ValueDetailTooltipBuilder` "Blish-free builder, the View only assigns
the string" split) so raw currency/item ids never cross into
`DecisionPillPlanner`/`PillSubduingEvaluator` at all (repo invariant:
IDs internal-only) - name resolution happens only at the View layer,
which already had `CurrencyMetadata` for this purpose and gained a new
`PlanViewModel.ItemMetadata` passthrough (mirroring `CurrencyMetadata`'s
own precedent exactly) for the item-kind case.

*Adversarial-review fix (self-caught):* a merged
multi-occurrence vendor step's per-occurrence `VendorCurrencyCosts`/
`VendorItemCosts` (and therefore this node's own
`BuyFromVendorCostBreakdown`, built from those same local numbers) can
disagree with the corrected `TotalCost` once `AllocateVendorNodeCosts`
reallocates it - the exact signal `CraftingTreeNode.
VendorComponentCostsUnreliable` already exists for, and
`ValueDetailTooltipBuilder`/`CraftingTreeBuilder.
BuildVendorCostComponentLeaves` already gate on. `DecisionPillPlanner`
now takes the same conservative posture: subduing detection is
suppressed entirely (every pill stays plain `Available`) whenever that
flag is set on the node, rather than risk a wrong verdict off stale
numbers when a merged Vendor decision is the SELECTED baseline every
other pill gets compared against.

**Repo invariants checklist:**
- ASCII-only / no em-dashes in every new/edited `.cs` file (verified via
  a non-ASCII grep sweep across the full changed-file list - zero hits).
- Allman brace style throughout.
- Tests exercise real production code paths - `PlanSolver.Solve`,
  `CraftingTreeBuilder.BuildTree`, and `DecisionPillPlanner.
  BuildPillSpecs` are called directly and unmocked in every new test
  file; no Blish HUD reference anywhere in tests.
- IDs remain internal-only - `PillCostDelta.Id` is a raw currency/item
  id but is a Services-layer, id-only DTO never displayed directly;
  `PillSubduingTooltipBuilder` is the sole place it gets resolved to a
  name before ever reaching a tooltip string.
- Coin amounts in the new tooltip text use the same "Xg Ys Zc" plain-text
  convention `ValueDetailTooltipBuilder` already established for its own
  hover (not a coin-icon rendering context, so the icon-right-of-number
  rule for the coin PANEL/shopping rows does not apply here).
- `Services/VendorBatchSolver.cs` (merged-ceil math), `Services/
  ModuleLog.cs`, `PlanContentHeightMath`, `PlanRelayoutMath`, and scroll
  machinery were never touched.

**New files:** `Services/CraftCompetencyEvaluator.cs`,
`Services/PillSubduingEvaluator.cs`, `Services/
PillSubduingTooltipBuilder.cs`, `Models/PillSourceCostBreakdown.cs`.

**Test plan (real path per rule, as specified):** competency flips the
default (`PlanSolverCraftCompetencyTests.
NonCompetentAccount_CraftCheapestButNotCraftable_DefaultsToNextBestSource`);
unknown competency preserves prior behavior
(`NoCharacterDisciplines_CompetencyUnknown_CraftStillAutoWins`); domination
detected on the real Amalgamated Rift Essence shape through the actual
solver (`PlanSolverPillSubduingTests.
AmalgamatedRiftEssenceShape_VendorNeedsMoreRawEcto_StrictlyDominated`);
weighted subduing through a real `CurrencyValuation`
(`WeightedValuation_VendorCheaperInRawCoinButPricierWhenValued_Subdued`);
unvalued+non-dominated left untouched, both at the pure-evaluator level
(`PillSubduingEvaluatorTests.UnvaluedAndNonDominated_BothPillsStayNormal`)
and through the real solver
(`PlanSolverPillSubduingTests.UnvaluedNonDominatedAlternative_
StaysAvailable_NotSubdued`) - plus the self-caught
`VendorComponentCostsUnreliable` suppression fix, and the "no alternative
source exists" guard for Rule 1 that stops competency from silently
dropping a node's cost out of the plan.

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
GW2CraftingHelper.csproj -p:Platform=x64` - clean, 0 errors (only
pre-existing StyleCop warnings scattered across the file set, none of
them new patterns this package introduced beyond one cosmetic SA1204
"static members before non-static" in the brand-new
`PillSubduingEvaluator.cs`, left as-is - matches this repo's own
already-extensive, pre-existing, unaddressed StyleCop backlog rather
than a regression).

Tests (measured, `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj`): 1554 (baseline) -> 1575 after Rule 1
(+21) -> 1605 after Rule 2 (+30, including the VendorComponentCostsUnreliable
suppression fix's own test). All green at every commit checkpoint.

**Nice-to-have (not fixed, noted for a future pass):**
- `PlanResultBuilder` already carries two independent, byte-identical
  copies of the "MysticForge/Achievement/Merchant are inherently
  available" tag set (`InherentlyAvailableDisciplines`/
  `NonCraftingDisciplines`); `CraftCompetencyEvaluator` now adds a THIRD
  independent copy (`NonLevelableDisciplineTags`) rather than couple a
  solver-path pure class to `PlanResultBuilder`'s display-adjacent
  internals - a future pass could extract one shared canonical set.
- The cosmetic SA1204 StyleCop warning noted above.
- StrictDomination's ITEM-kind comparison only decomposes a candidate
  craft recipe's DIRECT ingredients, not a full recursive expansion - a
  domination that only becomes visible several craft levels deep (rather
  than at the immediate ingredient list) is not detected. Not believed to
  affect the canonical Amalgamated-Rift-Essence-shaped cases
  (which are direct-ingredient-level by construction), and no case
  requiring deeper recursion was specified.

No live desktop verification was performed - `Views/Rendering/
TreeSectionController.cs` and `Views/Rendering/PillColors.cs` are
Blish-bound and outside this repo's test-runnable surface, same
constraint every UI-adjacent entry in this file notes. The Subdued
pill's actual on-screen color/tooltip rendering has not been visually
confirmed in a running Blish HUD client.

Gate: not yet run live - queued for the next desktop session (subdued-pill + competency-default visuals). Merged after the deepest pipeline of the wave: implementation, two adversarial rounds, three verification passes (the second MEASURED an overcorrection suppressing a real 70c opportunity; the third revert-tested both direction pins on the final design), under the standing merge directive (2026-08-16).
