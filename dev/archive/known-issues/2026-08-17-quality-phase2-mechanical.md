> **Frozen record - 2026-08-17, branch `quality-phase2-mechanical`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Quality-audit phase 2: safe-mechanical batch (quality-phase2-mechanical)

Triage source: quality-audit-triage.md sections A1/A3/A4/C/E, verified at
master e21a280; every site re-located and re-verified against d1092f5
before editing (10 PRs of drift). Six commits, build + module suite
green after each; the sixth fixes review findings against the first
five (two stale method-name repoints, a dangling "Cap data" anchor
retargeted to dev-notes/HISTORY.md, the m37-r1..r4 duplicates dropped).

**A1 factual comment/doc fixes (8 of 9 applied):**
- A1.1 SnapshotFailureClassifier "only place" claim corrected (its own
  Classify(Exception) overload also derives a type name)
- A1.2 CraftingPlanResult + CompetencyOpportunity repointed from the
  never-existent CraftingPlanPipeline.BuildCompetencyOpportunities to
  CompetencyOpportunityCalculator.Apply
- A1.3 ShoppingColumnMath repointed to
  ShoppingListSectionRenderer.Render (the pre-scan's actual home;
  CreateShoppingListBody survives only as the CraftingPlanView method
  the Render body was moved from)
- A1.4 ARCHITECTURE.md: literal CraftingPlanView line count (3 lines
  stale again at re-check) replaced with non-rotting wording
- A1.5 SKIPPED - already fixed on master (doc says FOUR instances)
- A1.6 AccountIndex -> AccountItems at all three comment sites
- A1.7 PillSubduingTooltipBuilder comment now says the two FormatCoins
  share the split, not the format
- A1.8 VendorBatchState reverted-ratchet history rewritten as a
  do-not-re-add warning (15 lines -> 8)
- A1.9 PlanSolver reallocation-guard comment extended: skipped
  fallback-tier decisions keep pre-correction ComparisonValue
- C4 trapped symbols and C6(a): SKIPPED - already fixed by merged PRs

**A3 dead-symbol deletions (measured, repo-wide grep re-verified):**
ModuleSettings.ResetToDefaults, RecipeService.CacheStats,
InMemoryRecipeCacheStore.GetAllSearches/GetAllRecipes,
CraftingPlanView.RarityFramedIconOuterSize, Harness Percentile,
MysticForgeSeeder WikiRecipeClient.RequestCount, the 2-arg
ItemSearchProviderFactory.Create (5 test call sites ported to 3-arg),
and 3 stale System.Linq usings. SKIPPED: PlanStoreHelpers' System.IO
using - triage claim wrong, the file throws InvalidDataException
(caught by the build gate, restored before commit). Kept per triage:
ValueOwnMaterials, ScrollDiagnosticsEnabled, TierByMaterialId.

**A4 test fixes (6 items):** absolute-value pins replacing the two
identical-IL default-argument comparisons; Homestead Exordium-shape test
now walks a real non-Homestead offer so the tier gate is actually
reached; the four "v=" substring asserts upgraded to a date-shape regex
(v=latest now fails); WikiLinkBuilder agreement test made a
discriminating Theory; ZojjasClaymore fixture renumbered to a fake
9001+/9101+ range and marked synthetic (real-ID collision sites for
46742 verified untouched). Module suite 1823 -> 1827 (Theory expansion).

**D-4 + C2:** /mnt/c/Dev/Blish/m38-plan committed at
docs/dev-notes/m38-plan/ (9 top-level docs + 1 json snapshot +
9 proposals); the four dangling anchors in ModuleLog.cs and
CraftingPlanView.cs retargeted to the committed paths. The m37-r1..r4
research records were dropped from the copy: no anchor points at them,
and docs/research/ already holds the canonical versions (corrected
Lumber Mill tables, machine-local paths scrubbed) which the stale
copies contradicted.

**C1 + C3 (former-frozen files, comment-only):** PlanRelayoutMath mirror
comment retargeted to SummarySectionRenderer.CreateFormulaBand (which
absorbed the old CreateCostTileRow's geometry); the
21-line EvaluateVendorOffers reviewer exchange compressed to 4 lines
(three load-bearing facts kept); nine review-round prefixes stripped;
FinalizeVendorBatches reverted-branch history compressed to a pointer at
VendorBatchState; the rotting PlanSolver.cs:1062 citation dropped.

**Final counts (measured):** build 0 errors; module suite 1827/1827;
updater suite 207/207; RecipeSeeder suite 3/3.

Gate: PASS (comment/doc/test-only batch plus dead-symbol deletions
with re-verified zero references; no rendered surface changed, so no
sandbox check applies; evidence is the per-commit build/suite record
above and the verification pass at 9022c9b - module 1827/1827, updater
207/207, build 0 errors).

---
