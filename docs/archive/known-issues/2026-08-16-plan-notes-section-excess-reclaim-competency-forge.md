## Plan Notes section: excess/reclaim, competency, forge scope (2026-08-16)

Implemented `design-plan-notes.md` (Option 1 - a single flat "Notes"
section, last in the plan, one shared `NoteLine` row shape) against a
worktree branched from current `master` (post currency-ux-package and
cooldowns merges) - the design doc was written against a slightly older
`master`, so every file/line reference was re-located against the real
current code before editing; the design's structure held unchanged.

**What changed, in commit order:**

1. **Data model** (`aa3d21b`) - `PlanSectionType.Notes`/`PlanRowType.
   NoteLine`; `CraftingTreeNode.CraftsNeeded`/`RecipeOutputCount`
   (nullable, Craft-decision-only); the new `ExcessCraftOutput` model;
   `CraftingPlanResult.ExcessCraftOutputs`/`ProbabilisticForgeOutputItemIds`;
   `ItemMetadata.IsAccountBound`; `RawItem.Flags`.
2. **Item-flags plumbing** (`58dc043`) - `Gw2ItemApiClient` now parses
   `/v2/items`' `flags` array (previously silently discarded);
   `ItemMetadataService` derives `IsAccountBound` from `"AccountBound"`
   membership. This was the design's single largest scope item (section
   9's risk note) - implemented per the design's **primary path**, no
   fallback substitution needed: it stayed within the documented
   three-file estimate exactly (`IItemApiClient.cs`, `Gw2ItemApiClient.cs`,
   `ItemMetadataService.cs`).
3. **`CraftingTreeBuilder`** (`0476d63`) - copies the chosen recipe's
   `CraftsNeeded`/`OutputCount` onto the display tree node, closing
   `docs/gw2e-considerations.md` #4 (computed but never surfaced).
4. **`ExcessCraftOutputCalculator`** (`6662925`, new, `internal static`,
   mirrors `SellSideEconomics`' shape) - walks `CraftingTree`/
   `MultiItemRoots`, aggregates every `Decision == Craft` occurrence's
   positive `(CraftsNeeded * RecipeExpectedOutputCount - Quantity)`
   surplus (the EV basis, not the nominal `RecipeOutputCount` - using the
   nominal basis fabricates a large fake surplus for a fractional-EV
   recipe like Mystic Clover) by `ItemId`, resolves `ReclaimValue` (null
   when unpriced OR account-bound) / `IsAccountBound`. Advisory-only by
   construction: it writes nothing but `CraftingPlanResult.
   ExcessCraftOutputs`. Excludes two categories of node from the walk:
   any node beneath an `IsReferenceBranch` subtree (hypothetical "what it
   would cost to craft instead" content, never actually crafted), and
   every requested root item id (single-item `CraftingTree`, or each
   `MultiItemRoots` entry) - the root's own over-production is already
   advertised via `SellSideEconomics.ComputePerItemEconomics`'
   `sellableQuantity` bump, so including it here would double-advertise
   the same coins under a different label.
5. **`PlanResultBuilder`** (`4fd0a1e`) - extends the existing
   `craftSteps`/`recipeOptionIndex` loop with the Mystic-Clover-style
   signal (`MysticForge` discipline + `ExpectedOutputCount <
   OutputCount`), populating `ProbabilisticForgeOutputItemIds`.
   Deliberately does **not** attempt true multi-outcome gambles
   (precursor forging etc.) - per KNOWN-ISSUES #17 those never reach the
   solved tree at all, so there is nothing in a plan to detect them from.
6. **`CraftingPlanPipeline`** (`bf37c95`) - `ExcessCraftOutputCalculator.
   Apply` wired into all three `SellSideEconomics.Apply(Batch)
   SellSideEconomics` call sites (single-item generate, multi-item
   generate, `ResolveWithOverrides`'s single-vs-batch branch), grepped
   1:1 against the existing precedent.
7. **`PlanViewModelBuilder`** (`2b2ebd3`) - extracted `MatchingCharacterDisciplines`/
   `BestCharacterRating` out of `BuildCharacterAvailabilityText` (pure
   refactor, byte-identical output for that method) so the Required
   Disciplines column and the new competency notes can never drift on
   what counts as "blocked". Added `BuildNotesSection`: excess/reclaim
   lines (alphabetical by item name via `StringComparer.Ordinal`, plus a
   total line only when 2+ entries exist), competency lines (a
   `RequiredDiscipline` whose account-best rating is missing or below
   `MinRating` - gated on `CharacterDisciplines != null` so no snapshot
   never produces a false "blocked" claim), and the forge-scope line (0
   or 1). Wired into `Build()` as section 7 (last), appended only when
   non-empty. Forge-scope wording explicitly distinguishes the two
   mechanics design section 9 flags as easy to conflate: this plan's own
   Mystic-Clover-style yield IS probability-adjusted (EV already priced
   in); true multi-outcome gambles are a different mechanic never
   modeled and never shown.
8. **`NotesSectionRenderer`** (`d50ba53`, new) + `CraftingPlanView.
   CreateCollapsibleSection`'s new `PlanSectionType.Notes` case - label
   left, a right-aligned coin cell via `CoinCurrencyRenderer` only when
   `CoinValue > 0` (no cell, not an unpriced dash, for a plain
   competency/forge-scope line - those have no price concept at all).
   The default fallback case was NOT sufficient here: it calls plain
   `TextRowRenderer.CreateTextRow` for every row, which never draws a
   coin value, silently dropping every reclaim amount.

**Repo invariants checked:**

- Reclaim value is advisory-only, never folded into `Plan.TotalCoinCost`/
  `CraftingProfit`/any Summary tile - `ExcessCraftOutputCalculator`
  writes only `CraftingPlanResult.ExcessCraftOutputs`, verified by
  reading every write site in the calculator and grepping for any other
  reference to it outside `PlanViewModelBuilder.BuildNotesSection`
  (display-only reader).
- `Services/ModuleLog.cs`, `Services/PlanContentHeightMath.cs`,
  `Services/PlanRelayoutMath.cs`, scroll machinery, and
  `VendorBatchSolver`'s merged-ceil math were never touched (confirmed
  via `git diff --stat` across all eight commits). `PlanSectionType.Notes`
  gets no case in `PlanContentHeightMath.SectionBodyHeight`'s switch on
  purpose - it falls through to that method's pre-existing `default` arm
  (`rows.Count * FallbackTextRowHeight`), which is only correct because
  every `NoteLine` row renders at exactly that height; a DEBUG assert in
  `NotesSectionRenderer` guards this invariant going forward.
- ASCII-only `.cs` (swept every new/edited file with
  `grep -rlP '[^\x00-\x7F]'` - zero hits); no em dashes (same sweep, plain
  hyphens throughout); Allman braces; `GW2CraftingHelper.csproj`'s
  explicit `<Compile Include>` list updated for all three new production
  files (`Models/ExcessCraftOutput.cs`, `Services/
  ExcessCraftOutputCalculator.cs`, `Views/Rendering/
  NotesSectionRenderer.cs`) - the test project is SDK-style/globbed, so
  its four new test files needed no registration.
- No Blish HUD/`BlishHUD.exe`/`Gw2Sharp` references in any test file
  (grepped); every new test exercises a real production code path -
  `PlanViewModelBuilder.Build()` for the three Notes-section test files,
  `ExcessCraftOutputCalculator.Apply` directly (plain `CraftingTreeNode`
  fixtures, no Blish) for the calculator tests, and `PlanResultBuilder.
  Build()` directly for the three new forge-scope-detection tests added
  to `PlanResultBuilderTests.cs` (not just asserted against a stub
  `CraftingPlanResult` - the render-layer forge-scope tests use a stub,
  but the detection logic itself is covered against the real builder).
- IDs remain internal-only (excess/competency/forge-scope lines all
  resolve item/discipline **names**, never raw item ids, for display).
- Coin icons render right of the number wherever a Notes row shows one -
  unchanged shared `CoinCurrencyRenderer` path, no new coin-rendering
  code.

**Test plan, matching the design's spec:** four new test files
(`PlanViewModelBuilderNotesExcessTests.cs`,
`PlanViewModelBuilderNotesCompetencyTests.cs`,
`PlanViewModelBuilderNotesForgeScopeTests.cs`,
`ExcessCraftOutputCalculatorTests.cs`) plus three new tests appended to
the existing `PlanResultBuilderTests.cs` for the forge-scope detection
logic itself (not spelled out as a separate file in the design's test
plan, added here for real-production-path coverage of `PlanResultBuilder.
Build`'s new branch, per this repo's "tests must exercise real
production code paths" invariant).

**No fallback substitutions were needed anywhere in this package** - the
account-bound item-flags plumbing (the design's only flagged risk of
exceeding its own estimate) was implemented in full per the primary path.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - clean, 0
errors (only pre-existing StyleCop warnings, none in new/edited files).
Tests (measured, `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj`): 1501 (baseline) -> 1533 after the
initial implementation, -> 1536 (+35 total) after the review-fix round
(findings 6/7/8 each added real-production-path test coverage:
`ItemMetadataServiceTests.FetchBatchIntoCache_DerivesIsAccountBound_
FromRawItemFlags`, `Gw2ApiClient404Tests.ItemClient_ParsesFlags_
MissingFieldYieldsEmptyList`, `SellSideEconomicsTests.
ComputePerItemEconomics_FractionalEvRoot_UsesExpectedOutputCountNotNominalOutputCount`,
plus two new assertions appended to the existing
`CraftingTreeBuilderTests.CraftNode_ChildrenAreIngredients`). All green
at every one of the eight commit checkpoints (verified individually via
`git stash push --keep-index` before each commit, not just at the end).
One unrelated flaky failure was observed once
(`TradingPostServiceTests.ConcurrentCalls_OverlappingIds_
SharedIdsCoalesce_UniqueIdFetchedSeparately`, a pre-existing concurrency
test) and confirmed non-reproducing on immediate re-run, both isolated
and in the full suite.

**Nice-to-have (not fixed, noted for a future pass):**
`ExcessCraftOutputCalculator.Walk` is a plain recursive pre-order walk,
consistent with every other tree walk in this module
(`CraftingTreeBuilder.BuildChildren`, `PlanResultBuilder.
IndexRecipeOptions`) but therefore shares their same unbounded recursion
depth on a pathologically deep crafting tree; not a regression, not
addressed here.

No live desktop verification was performed - `Views/CraftingPlanView.cs`
and `Views/Rendering/NotesSectionRenderer.cs` are Blish-bound and outside
this repo's test-runnable surface, same constraint every UI-adjacent
entry in this file notes. The Notes section's real on-screen layout
(row spacing, coin-cell alignment at 28px row height, section
collapse/expand) has not been visually confirmed in a running Blish HUD
client.

Gate: PASS (hidden-when-empty) 2026-08-16 (orchestrator live desktop session). Verified hidden on two plans that generate zero notes - correct per design; positive note rendering is suite-covered (cooldown notice, a sibling feature, rendered live); a notes-generating live fixture rides the next natural field test.
