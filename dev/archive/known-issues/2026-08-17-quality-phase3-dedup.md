> **Frozen record - 2026-08-17, branch `quality-phase3-dedup`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Quality-audit phase 3: structural dedup (quality-phase3-dedup)

Two structural deduplications, no behavior change intended; module
suite 1827/1827 after each commit (zero count change).

**B5+B12+B7 - CraftingPlanPipeline shared pipeline body (commit 1):**
GenerateStructuredAsync (single-item) and GenerateStructuredMultiAsync
each duplicated Steps 2-through-return (~230 lines). Both now keep only
Step 1 (tree build - PlanPhaseTimingSummary keys on the "Build recipe
tree"/"Build recipe trees" phase-label literals, so those stay in the
callers) and delegate to one private RunPipelineAsync. Pure code
motion: the single-item body was absorbed in place; a mechanical diff
of the shared body against each old body shows only the parameterized
divergence hunks. Divergence hunks eliminated (now parameterized once):
Step 8 metadata-id add (targetItemId vs foreach items),
result.RequestedItems, the crafting-tree build (B12: the single-item
path's inlined tree build now goes through the existing
BuildCraftingTreeResult helper; its else-branch was
argument-for-argument the inlined code and its MultiItemRoots = null
write is a no-op on a fresh result), the SellSideEconomics dispatch
(per-path calls preserved exactly; the single/multi asymmetry remains
a known hazard, out of scope here), and the 27-field PlanSolveContext
initializer + five-call annotation block (B7 - previously two copies
differing only in TargetItemId/Quantity/RequestedItems). Phase-event
ordering, the ObserveFault/currencyTask concurrency shape, and the
assignNodeIds:!useForceBuyPrePass coupling moved together unchanged.

**B6 option (a) - InventoryReducer flat overload deleted (commit 2):**
the flat Reduce(RecipeNode, Dictionary<int,int>, guide) overload +
ReduceNode were a complete second reduction implementation with no
production caller (both CraftingPlanPipeline sites call the
AccountItemIndex "sourced" overload), including a ~46-line
zero-owned-guide/recipe-rescale tail duplicated from
ReduceNodeSourced. Deleted; canonical VOM doc comments (including the
KNOWN RESIDUAL note) moved onto the surviving sourced members. Port
stats: 28 flat call sites, all in InventoryReducerTests.cs (repo-wide
grep found no others), ported with intent preserved; 6 ports that
would have exactly duplicated an existing Sourced_ twin split the same
owned total across two sources instead. Re-pinned flat-specific
assertions: two trivially-true external-pool checks (flat Reduce
copied the caller's dictionary) replaced with UsedMaterials pins
(CraftableFullyOwned, CurrencyNodes_NeverConsumed); Assert.Null(
Sources) pins re-pinned to production sourced behavior (single-source
allocation listing; same-source-across-branches allocation merging);
the flat-vs-sourced equivalence test became
Sourced_SourceSplitInvariance_SameQuantityResults with an absolute
quantity anchor. [Fact] count 51 -> 51; suite total 1827 unchanged.
Follow-up (review finding): the deletion sweep covered call sites
only; 6 comment references to the deleted ReduceNode symbol (in
AchievementBitDedupPrePass.cs, AchievementBitDedupPrePassTests.cs,
InventoryReducerTests.cs, MultiItemPlanTests.cs) plus one live
KNOWN-ISSUES pointer were retargeted to ReduceNodeSourced, where the
cited behavior now lives. Historical records (HISTORY.md, the gw2e
convergence matrix, and this section's own deletion narrative) keep
the old name deliberately.

Gate: PASS (structural dedup with measured move-purity - the review
diffed the shared body against both original paths and found only the
four parameterization hunks, and three targeted reducer mutations were
killed by the ported tests; no rendered surface changed, so no desktop
gate applies; suite 1827/1827 and build clean at the verification
pass).
