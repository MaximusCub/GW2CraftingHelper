> **Frozen record - 2026-08-17, branch `quality-phase4b-bundling`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Quality-audit phase 4b: pure parameter bundling (B10, quality-phase4b-bundling)

Three private-surface signature refactors, one commit each. Zero
behavior change: contexts are private sealed classes with get-only
properties, constructed once per top-level call from the same
expressions at the same point the old calls evaluated them. No public
surface changed; no test edited; suite 1837/1837 green after each
commit.

| Method | Params before | Params after |
|---|---|---|
| PlanSolver.Evaluate | 15 (node + 14 threaded) | 2 (node + EvaluateContext) |
| PlanSolver.Collect | 10 + ref int | 2 + ref int (node + CollectContext) |
| CraftingTreeBuilder.BuildNode | 10 | 3 (node + BuildContext + insideReferenceBranch) |
| CraftingTreeBuilder.BuildChildren | 10 | 3 (recipe + BuildContext + insideReferenceBranch) |
| CraftingTreeBuilder.BuildVendorCostComponentLeaves | 6 | 3 (parentNodeId + decision + BuildContext) |

Bundled vs kept as parameters:

- **EvaluateContext** (14 fields): prices, vendorOffers, memo,
  priceBasis, overrides, currencyValuation, forceBuyOnlyNodeIds,
  competencyIndependentForceBuyNodeIds, costDiagnostics,
  rawCraftCostDiagnostics, ignoredItemIds, homesteadTiers (normalized
  `?? Default` in the ctor, preserving Evaluate's old defensive
  normalization), bestRatingByDiscipline, ownedQuantityUsedByNode.
  Kept: `node` (varies per recursive call).
- **CollectContext** (8 fields): memo, stepMap, currencyMap,
  craftOrder, vendorBatchTracking, vendorOccurrences,
  craftOccurrences, ignoredItemIds. Kept: `node` (varies per call)
  and `ref int craftCounter` (mutable accumulation stays a ref
  parameter, not context state). AggregateStep's signature unchanged.
- **BuildContext** (8 fields): decisions, metadata, hints,
  ownedQuantityUsedByNodeId, ignoredItemIds, currencyMetadata,
  ownedCurrencyAmounts, ownedVendorItemAmounts. Kept: `node`/`recipe`
  and `decision` (vary per call), `insideReferenceBranch` (flips to
  true inside reference branches). BuildTree's public signature is
  byte-identical.

Call-site claims re-verified at HEAD before acting: Evaluate had
exactly 2 call sites (Solve + self-recursion); Collect had 3 (Solve +
2 self-recursion sites); the CraftingTreeBuilder build methods are
private with BuildTree as the only entry point.

Build 0 errors each commit; suite 1837/1837 green each commit (zero
count change).

Gate: PASS (private-signature refactor with zero behavior change -
review confirmed public surfaces byte-identical, no test edits, and
the classic bundling hazards absent; suite held at exactly 1837; no
rendered surface changed, so no sandbox check applies).
