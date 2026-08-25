## Quality-audit cleanup, phase 1: four bug fixes (B1-B4, 2026-08-17)

Cross-dimension quality-audit triage (comment hygiene / dead code /
duplication / correctness / test hygiene / architecture drift)
identified four behavior-affecting bugs as the highest-priority phase
of a larger cleanup plan - small, independent, each landed as its own
commit on `quality-phase1-bugs`, in the triage's own recommended
order, gates green after every commit:

- **B1** (`Models/PersistedPlan.cs`): `CurrentSchemaVersion` was stale
  at 2 while the persisted graph grew ~275 lines of new fields
  (`CraftingTreeNode`'s `CraftCostBreakdown`/`BuyFromTpCostBreakdown`/
  `BuyFromVendorCostBreakdown`, `PlanSolveContext`'s
  `CompetencyIndependentForceBuyNodeIds`/`UnreducedTree`/
  `AccountItems`/`ActiveCharacterName`, `CraftingPlanResult`'s
  `ExcessCraftOutputs`/`RecipeSheetSavingsOpportunities`/
  `SeasonalVendorTips`, among others) across `CraftingPlanResult.cs`/
  `CraftingTreeNode.cs`/`PlanSolveContext.cs` after the 1 -> 2 bump
  with no matching version bump - the exact silent-default failure the
  schema-version gate exists to reject. Bumped to 3, plus a new
  reflection-based member-set guard test
  (`PersistedPlanSchemaMemberSetTests`) that fails independent of
  whether a future change remembers to bump `CurrentSchemaVersion`,
  and a `LoadLatest_QualityAuditSchemaVersion2File_...` test mirroring
  the existing SchemaVersion-1 rejection test. **User-visible effect
  of this fix, one time only:** the very first module load after this
  change, any plan.json a pre-fix build wrote (SchemaVersion 2) is
  rejected by the existing tolerance gate exactly like every other
  schema mismatch already handles it - one Warn log line, the
  Crafting Plan tab comes up empty instead of restoring, the user
  generates a fresh plan. This is the same known, already-exercised,
  safe fresh-start path the 1 -> 2 bump itself used (see that bump's
  own doc comment in `PersistedPlan.cs`), not a new failure mode; it
  fires once per installation, not on every load.
- **B2** (`Services/PlanStructuralValidator.cs`): four restored lists
  (`CompetencyOpportunities`/`ExcessCraftOutputs`/
  `RecipeSheetSavingsOpportunities`/`SeasonalVendorTips`) were missing
  the per-entry null check every other restored list already has,
  reachable from `PlanViewModelBuilder.BuildNotesSection`'s unguarded
  per-entry dereference. Added the same `NoNullEntries` call already
  used for the other ten lists, plus one corruption test per list.
- **B3** (`Services/RecipeClientFactory.cs`): `MysticForgeRecipeData.
  LoadWarnings` was collected on every seed load and never read; the
  load-failure `catch` swallowed the exception wholesale too. Wired
  both to `ModuleLog.Shared.Write(Warn, "startup", ...)` via an
  optional `ModuleLog` injection parameter (mirroring
  `CraftingPlanPipeline`'s existing pattern), logging a warning COUNT
  only - not the raw warning text, since one `LoadWarnings` category
  embeds a raw item id and a Warn-level `ModuleLog` line is a
  Log-tab-visible surface the item/currency/vendor-id-internal-only
  invariant covers (per `PlanStructuralValidator.NoNullValues`'s own
  precedent for the same tension). `RecipeCount` folded into the same
  line instead of staying unreferenced.
- **B4** (`tools/VendorOfferUpdater/Program.cs`): `MergeWikiCache`'s
  `Unchanged` counter (`existing.Count - refreshed`) could under-report
  or go negative because `refreshed` was incremented against the
  `merged` dictionary the same loop was mutating, so a duplicate
  PageName within one fresh batch double-counted as a refresh of the
  existing cache. Fixed by counting against sets built from the
  original `existing`/`fresh` inputs rather than the mutating
  dictionary; `Merged` output is byte-identical for every non-
  duplicate input. Console-only counter, dev tool, no shipped-plan
  impact.

**Validation (2026-08-17, measured):**
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-qp1/GW2CraftingHelper.csproj -p:Platform=x64` (clean
rebuild) - 0 errors, 1788 warnings, all pre-existing StyleCop findings
unrelated to this change (confirmed no new warnings in any of the four
touched files individually). `"/mnt/c/Program Files/dotnet/dotnet.exe"
test C:/Dev/Blish/wt-qp1/tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` - 1776/1776 green (11 new: B1 adds 3 -
2 `PersistedPlanSchemaMemberSetTests` cases + 1
`LoadLatest_QualityAuditSchemaVersion2File_...` in `PlanStoreTests`;
B2 adds 4 null-entry corruption tests in `PlanStoreTests`; B3 adds 4
`RecipeClientFactoryTests` cases; B4 touches only the updater suite.
Measured after each commit: 1770 after B1, 1774 after B2, 1778 after
B3, consistent with a pre-B1 count of 1765; B1's own commit shipped 5
new tests, and the later follow-up commit b5fe6e6 consolidated its 4
member-set [Fact]s into 2, taking the branch total to its final 1776 -
the "B1 adds 3" breakdown above describes HEAD's tree, not B1's own
commit). `"/mnt/c/Program Files/
dotnet/dotnet.exe" build C:/Dev/Blish/wt-qp1/tools/VendorOfferUpdater/
VendorOfferUpdater.csproj` - 0 errors, 0 warnings. `"/mnt/c/Program
Files/dotnet/dotnet.exe" test C:/Dev/Blish/wt-qp1/tests/
VendorOfferUpdater.Tests/VendorOfferUpdater.Tests.csproj` - 207/207
green (2 new `MergeWikiCacheTests` cases for B4). Both suites fully
green after every one of the four commits, not just at the end.

Gate: PASS (ratified by the orchestrator, 2026-08-17; a subagent had
filled this line and the orchestrator re-judged it rather than letting
the self-fill stand). No live desktop check: B1-B4 touch no rendered
UI surface beyond B3's Log-tab warning line, which flows through the
already-live-gated ModuleLog pipeline, only fires on a corrupted or
incomplete Mystic Forge seed, and so cannot be exercised by a live
sandbox session running on healthy data; the wiring is suite-pinned by
`RecipeClientFactoryTests` against a real `ModuleLog` instance. Module build 0 errors/1788 warnings (clean
rebuild, unchanged), module suite 1776/1776 green, updater build 0
errors/0 warnings, updater suite 207/207 green - re-measured after
fixing this block's own wrong counts above and the retype blind spot
in `PersistedPlanSchemaMemberSetTests` (see that file and
`PersistedPlan.cs` for the fix).

---
