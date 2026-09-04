> **Frozen record - 2026-08-17.** M38 cleanup analysis, test-suite lens, closed and kept as evidence.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

# M38 Cleanup Analysis — Lens: Test Suite Quality

Scope: `tests/GW2CraftingHelper.Tests` (854 tests measured against an
expected 848 - small drift, not investigated) and `tests/VendorOfferUpdater.Tests` (67
tests). Read-only analysis against current `master`
(`85a738e`). No files modified, no git state changed.

All performance statements below are labeled **MEASURED** (I ran the suites
and/or counted) or **INFERRED** (reasoned from code, not executed). No time
estimates are given beyond the measured numbers themselves.

---

## 0. Headline numbers (MEASURED)

Ran both suites via `dotnet test` (net48 project through `dotnet.exe`, net8.0
tool-test project natively):

```
GW2CraftingHelper.Tests:  854 passed, 0 failed, 0 skipped, ~5.2-5.3s wall
VendorOfferUpdater.Tests:  67 passed, 0 failed, 0 skipped, ~1.2-1.5s wall
```

No `[Fact(Skip=...)]` anywhere in either project — good hygiene, nothing is
silently disabled.

Per-test timings were captured from `--logger "console;verbosity=normal"`
output and aggregated by test class (MEASURED, two runs, consistent):

| Class | total ms | # tests | share of suite |
|---|---|---|---|
| `VendorOfferResolverTests` | ~3141 | 8 | **32%** |
| `RecipeServiceConcurrencyTests` | ~718 | 3 | 7% |
| `Recipes.RecipeCacheSerializerTests` | ~413 | 2 | 4% |
| `AcquisitionHintServiceTests` | ~319 | 7 | 3% |
| `CurrencyMetadataServiceTests` | ~316 | 10 | 3% |
| everything else (≈824 tests) | ~4862 | 824 | ~51% |

**Finding T1 (moderate, quick win): real wall-clock delays dominate suite
runtime.** `VendorOfferResolverTests` alone is a third of total measured
test time despite being 8 of 854 tests (1%). The cause is directly visible
in the test file
(`tests/GW2CraftingHelper.Tests/Services/VendorOfferResolverTests.cs`):
`AllRetriesFail_ItemInFailedList` (1000ms), `CancellationStopsRequests`
(`cts.CancelAfter(500)`, line 192), `TransientFailure_RetriesAndSucceeds`,
`ConcurrencyNeverExceedsMax` all drive real `Task.Delay`-based latency via
`InMemoryWikiVendorClient.LatencyMs` and real retry backoff rather than a
virtual/fake clock. `RecipeServiceConcurrencyTests.CancellationStopsPreWarm`
(line 219, `cts.CancelAfter(150)`) and `ConcurrencyDoesNotExceedMaxDegreeOfParallelism`
(line 155, `LatencyMs = 50`) do the same for `RecipeService`. This is a
legitimate way to test concurrency-limiting and cancellation behavior
end-to-end (there's no fake scheduler in this codebase to swap in), so it is
not a correctness bug — but it is the single biggest lever on suite runtime,
and real-wall-clock waits are a source of CI flakiness on loaded/slow
runners (a `CancelAfter(150)` racing a 100ms-latency fake client has a much
smaller safety margin than `CancelAfter(500)` vs 200ms). **Quick win**: no
code change needed to "fix" this (it's inherent to testing real retry/backoff
timing), but if the milestone wants a faster inner dev loop, these are the
concrete, named tests to either (a) parameterize down to smaller
sleep/backoff constants, or (b) introduce a single shared fake-delay
abstraction the resolver/service take a delegate for, so tests can compress
elapsed time without weakening what they assert. Flagging, not proposing a
redesign — this is squarely in "measure once, decide later" territory.

---

## 1. Realism — adherence to "real production code paths, no contract
mirrors"

**This is the suite's strongest area.** I went looking for contract-mirror
tests (tests that re-implement the algorithm under test and compare
implementation-to-implementation rather than calling production code) and
found none in the pure-math/service layer. Specifically good examples,
confirmed by reading the test file against its target class:

- `WheelDeltaSanitizerTests` (183 lines, 30 cases incl. the full measured
  N=2..8 histogram, the `N=46`/`N=47` lattice-edge boundary, and
  `SanitizeScrollLines`) calls the real
  `Services/WheelDeltaSanitizer.Classify`/`SanitizeScrollLines` statics and
  matches `docs/KNOWN-ISSUES.md` item #12's derivation almost line-for-line.
  This is exactly the "essential complexity pinned by a loud test" pattern
  the milestone asks for - nothing to change here.
- `StatusUpdateGuardTests` (33 lines, 3 cases) directly pins the M34-B1 #4
  race fix (`StatusUpdateGuard.ShouldApply`) with the three states that
  matter (current/not-closed, stale/either, current/closed). Small and
  correct.
- `PlanContentHeightMathTests` (392 lines, 34 cases), `ScrollMathTests` (104
  lines, 12 cases), `PlanRelayoutMathTests` (240 lines, 20 cases) all call
  the real `Services/PlanContentHeightMath` / `ScrollMath` /
  `PlanRelayoutMath` statics that back the scroll-restore and resize-drag
  machinery documented in KNOWN-ISSUES #12-#19. The practice of
  extracting Blish-free pure-math classes specifically so they're testable
  is working as intended - this is the right structural pattern and the
  cleanup pass should preserve it, not "simplify" it away.
- `AchievementBitDedupPrePassTests`, `OwnedMaterialsForceBuyPrePassTests`,
  `PlanSolverTests`'s aggregate-before-ceil section (below) all build a real
  `RecipeNode`/`RecipeOption` tree and run it through the actual
  `PlanSolver`/`InventoryReducer`, asserting on the real output — not on a
  hand-computed shadow value.
- The merged-ceil invariant (KNOWN-ISSUES 20.1, the Obsidian Shard
  179→180-not-186 fix) is excellently pinned:
  `PlanSolverTests.MultiOccurrenceBulkVendorOffer_CurrencyCost_AggregatesBeforeCeiling`
  and `..._CoinCost_AggregatesBeforeCeiling` (lines ~1291, ~1328) reproduce
  the exact 179/180/186 arithmetic from the known-issues doc, calling real
  `PlanSolver.Evaluate`/`FinalizeVendorBatches`, not a re-derivation.

No violations of "no fake file I/O tests" either: every `SnapshotStore`,
`StatusStore`, `RecipeCacheStore`, `VendorOfferStore`,
`VendorOfferResolverTests` test that touches disk uses a real temp directory
and the real store class (see hygiene notes in §4, which is about
duplication of *how* they do this, not about faking it).

I did not find any test asserting against a hand-copied expectation that
duplicates a formula from the production code (the classic contract-mirror
smell) — expectations are literal numbers/booleans/strings, not
re-computations. This is a healthy sign for a codebase of this age and this
much milestone churn.

---

## 2. Fixture duplication — concrete shared-builder candidates

**Finding T2 (moderate, quick win, mechanical/behavior-preserving): the
`RecipeNode`/`RecipeOption` test-tree builders are copy-pasted verbatim
across at least four files.**

`Leaf(int id, int quantity, string type = "Item")`, `Craftable(int id, int
quantity, params RecipeOption[] recipes)`, and `Option(int recipeId, int
outputCount, int craftsNeeded, params RecipeNode[] ingredients)` are
**byte-for-byte identical** private static methods in:

- `tests/GW2CraftingHelper.Tests/Services/PlanSolverTests.cs:11-49`
- `tests/GW2CraftingHelper.Tests/Services/CraftingTreeBuilderTests.cs:12-53`

confirmed via direct diff of the two blocks — same field assignments, same
null-guard on `params`, same parameter defaults. A third file,
`InventoryReducerTests.cs:17-55`, has a `Leaf` with the same shape but a
divergent `Craftable` (it auto-computes `craftsNeeded = ceil(qty/outputCount)`
inline instead of taking it as a parameter — a real semantic difference, not
just a rename). A fourth, `DecisionPillPlannerTests.cs:352-355`, has a
narrower 2-parameter `Leaf`. `CraftingTreeBuilderTests.cs` additionally has
its own `Meta(...)` dictionary-of-`ItemMetadata` builder
(`CraftingTreeBuilderTests.cs:55`) that is functionally the same shape as
`PlanViewModelBuilderTests.cs`'s `MetaFor(...)` (`PlanViewModelBuilderTests.cs:55`).

**Quick win**: extract `Leaf`/`Craftable`/`Option` into one shared
`tests/GW2CraftingHelper.Tests/Helpers/RecipeNodeBuilders.cs` (matching the
existing `Helpers/` convention already used for the `InMemory*Client`
fixtures) and have `PlanSolverTests` and `CraftingTreeBuilderTests` use it
directly (mechanical, behavior-preserving — these two copies are provably
identical today). `InventoryReducerTests`'s divergent `Craftable` should
either be reconciled into the shared builder as an optional parameter or
consciously kept local with a comment explaining why it differs; leaving it
silently different is what makes future drift invisible.

**Finding T3 (minor, quick win): duplicated `FindRepoFile` helper.**
`tests/GW2CraftingHelper.Tests/Services/AcquisitionHintServiceTests.cs:172-185`
and
`tests/GW2CraftingHelper.Tests/Services/Recipes/RecipeCacheSerializerTests.cs:124-137`
both define an identical private `FindRepoFile(string relativePath)` (walks
up from `AppContext.BaseDirectory` up to 12 levels looking for a file — used
to locate the real `ref/*.json` seed files regardless of build
configuration). `RecipeCacheSerializerTests.cs`'s own doc comment even says
"Mirrors AcquisitionHintServiceTests' FindRepoFile pattern" — the
duplication is self-acknowledged in code, not accidental. Extract to
`Helpers/RepoFileLocator.cs` (or similar) so a third seed-file test (see
Finding T6 below) doesn't triple it.

**Finding T4 (minor, quick win): repeated inline temp-directory
boilerplate instead of the project's own established pattern.** The project
already has a clean idiom for this — a constructor that creates a
`Path.Combine(Path.GetTempPath(), "GW2CraftingHelper_..._" + Guid.NewGuid())`
directory and an `IDisposable.Dispose()` that deletes it
(`SnapshotStoreTests.cs:17-26`, `StatusStoreTests.cs`,
`VendorOfferResolverTests.cs:16-34`, `VendorOfferStoreTests.cs`). But
`CraftingPlanPipelineTests.cs` re-derives the same 5-line
create/try/finally/delete block **inline, per test method, seven separate
times** (lines ~163-203, ~705-746, ~971-1032, ~1699-1737, ~1745-1771,
~1779-1827, ~1835-1853), and `MultiItemPlanTests.cs` does it twice more
(~171-246, ~1294-1358), and `RecipeCacheStoreTests.cs` twice more (~85-150,
~159-200) — roughly a dozen copies of the same boilerplate in a codebase
that already has the right abstraction sitting in a sibling file. All of
them are correct (proper try/finally, no leak on exception), so this is pure
duplication, not a bug — but it is exactly the kind of drift-prone
repetition the milestone's "simpler, cleaner" goal targets. **Quick win**:
a small `Helpers/TempDirectory : IDisposable` (constructor creates+returns
the path, `Dispose` best-effort-deletes) would let every one of these call
sites become a one-line `using (var tmp = new TempDirectory())` and would
also unify the two current styles (ad-hoc inline vs. class-level
constructor/Dispose) into one.

**Finding T5 (nice-to-have): style-only — `CraftingPlanPipelineTests.cs`**
and `MultiItemPlanTests.cs` fully-qualify `System.IO.Path`/`System.IO.Directory`
32+ times each instead of adding `using System.IO;` (every other file in the
project that touches the filesystem uses the `using`). Cosmetic, but a
5-second fix if anyone is already touching these files for T4.

---

## 3. Coverage gaps on documented invariants

The task asks specifically whether each load-bearing invariant from
`docs/KNOWN-ISSUES.md` is "pinned by a test that would fail loudly." Status,
invariant by invariant:

| Invariant (KNOWN-ISSUES ref) | Pinned? | Where |
|---|---|---|
| Merged-ceil vendor batching (20.1) | **Yes, well** | `PlanSolverTests.cs` "Aggregate-before-ceil" section (~1282-1577), reproduces the exact 179/180/186 numbers |
| `PlanContentHeightMath` synchronous height contract (#12/#19) | **Yes, well** | `PlanContentHeightMathTests.cs`, 34 cases |
| Relayout closure registry / `PlanRelayoutMath` (#13) | **Yes** | `PlanRelayoutMathTests.cs`, 20 cases (the closure-registry *wiring* itself — `CraftingPlanView`'s per-render `List<Action<int>>` — is Blish-bound and can't be unit tested; only the pure width/geometry math can be, and is) |
| `WheelDeltaSanitizer` thresholds (#12 reopened) | **Yes, excellently** | `WheelDeltaSanitizerTests.cs`, full histogram + boundaries + lattice edges |
| `StatusUpdateGuard` race (20.3) | **Yes** | `StatusUpdateGuardTests.cs`, all 3 states |
| Owned-materials per-node attribution (20.4 B2a #1) | **Yes** | `InventoryReducerTests.OwnedQuantityUsedByNode_*` (4 tests, ~602-664) |
| Primary-option-only pool consumption (20.4 B2a #2) | Present | `InventoryReducerTests` (not individually located by name search but the file's 41 cases cover multi-recipe-option scenarios) |
| Force-buy pre-pass (20.4 B2a #3) | **Yes** | `OwnedMaterialsForceBuyPrePassTests.cs` + `CraftingPlanPipelineTests`'s "force-buy" section |
| Owned currency display-only / never fed back (20.4 B2a #4) | **Yes** | `CraftingPlanPipelineTests.OwnedCurrency_DoesNotAffectDecisionsOrTotals` — a real "same decisions/costs with and without wallet data" regression test, exactly the shape the invariant needs |
| Ignore pill (20.4 B2b) | **Partially — see Finding T6** | see below |

**Finding T6 (moderate — the most concrete gap this lens found): the
Ignore↔Owned-materials interaction is untested at every layer.** Per
KNOWN-ISSUES 20.4's own "Conservative reading" paragraph, the Ignore
mechanism and the owned-materials/InventoryReducer mechanism are two
independently-evolved systems that both want to zero out a node's cost, and
the doc explicitly discusses their different scopes (Ignore zeroes at solve
time via `ignoredItemIds`; ownership zeroes via `InventoryReducer` before
the tree is even built). I checked every test that exercises `IsIgnored` /
`ignoredItemIds`:

- `CraftingPlanPipelineTests.ResolveWithOverrides_IgnoredItemIds_ZeroesIngredientCost`
  (~1281) explicitly comments "No snapshot: nothing owned via real
  reduction" — i.e. it deliberately avoids combining the two mechanisms.
- `CraftingPlanPipelineTests.ResolveWithOverrides_IgnoredItemIds_ManualOverrideOnSameNodeStillApplies`
  and `..._NullIgnoredItemIds_BehavesExactlyAsBefore` test Ignore vs. the
  manual craft/buy override, not vs. ownership.
- `MultiItemPlanTests.ResolveWithOverrides_MultiItem_IgnoreRootItemId_ZeroesCostButKeepsItInRollup`
  tests Ignore in a multi-item context, again with no snapshot present.
- `DecisionPillPlannerTests.cs`'s own `Node(...)` builder
  (line 21) takes both `ownedQuantityUsed` and `isIgnored` as independent
  parameters — the display layer clearly anticipated the combination could
  occur — but no test in the file passes both non-default at once (checked
  via grep for the co-occurring parameters; none found).

Given `PlanSolver.Evaluate`'s ignored-check runs *before* looking at the
node's existing `Decision` (`Services/PlanSolver.cs:319`, before whatever
decision ownership already assigned it), and given `InventoryReducer` runs
in a separate earlier pass, there is a real, plausible ordering question —
"what does the pill/cost show for an item that is both partially-owned AND
ignored, or fully-owned AND ignored?" — that no test currently answers. If
that combination ever regresses, nothing in the suite would fail loudly. Not
proposing what the *correct* behavior should be (that's a product decision,
out of scope for this lens) - flagging that it's currently unobserved by any
test, which is exactly the "would fail loudly" bar the milestone set.

**Finding T7 (moderate): three of the four real shipped `ref/*.json` data
files have zero "does the real file still parse" test; the fourth (largest,
highest-churn) file has none either.** `RecipeCacheSerializerTests.cs` pins
`ref/recipes_seed.json` (14,736 rows) and `ref/recipe_search_seed.json`
against the real shipped files via `FindRepoFile`.
`AcquisitionHintServiceTests.Load_ShippedSeedFile_ParsesSixEntriesWithHintAndBadge`
does the same for `ref/acquisition_hints_seed.json`. But grep across the
entire `GW2CraftingHelper.Tests` project for `mystic_forge_recipes.json`,
`item_name_seed.json`, or `vendor_offers.json` (the actual filenames)
returns **nothing** — no test loads any of these three real shipped files
through the real production loader (`VendorOfferStore`/`VendorOfferLoader`
for `vendor_offers.json`, `MysticForgeRecipeData`/whatever loads
`mystic_forge_recipes.json`, `ItemNameSeedData` for `item_name_seed.json`).
`VendorOfferStoreTests.cs` (10 tests) exercises `LoadBaseline` exclusively
against small synthetic in-memory JSON strings/streams, never the real file.
`ref/vendor_offers.json` is, per KNOWN-ISSUES item 17, a ~53,530-row file
actively hand-edited (the Battle Master stale-offer removal) and tool-edited
(`VendorOfferUpdater`) across milestones — it is exactly the kind of file
where a bad merge, encoding issue, or manual JSON edit could silently break
parsing with zero test signal until someone notices missing vendor prices
live in-game. `mystic_forge_recipes.json` similarly carries the hand-added
Mystic Clover recipe -1591 (KNOWN-ISSUES item 17) with no regression pin.
**Recommend** (mechanical, same shape as the three existing shipped-file
tests): one `Load_ShippedVendorOffersFile_ParsesWithoutThrowing`-style test
per remaining file, asserting a sane lower-bound count and a couple of
known-stable rows — not a full snapshot (too brittle, see Finding T8) but
enough to fail loudly on a corrupt file.

**Finding T8 (integration-level negative-path gap): `CraftingPlanPipelineTests`
(43 tests, the top-level orchestrator covering `RecipeService` +
`TradingPostService` + `VendorOfferResolver`/`Store` + `CurrencyMetadataService`
+ `PlanSolver` end to end) has zero cancellation tests and zero
"a dependency throws mid-generation" tests.** Grepped the file for
`ThrowsAsync`/`ThrowsAnyAsync`/`Assert.Throws` — none. Every one of its 43
tests calls `GenerateAsync`/`GenerateStructuredAsync` with
`CancellationToken.None` and a fully-successful set of in-memory API
fixtures. Contrast with the individual dependencies, which *do* test their
own failure modes in isolation (`Gw2ApiClient404Tests` for the raw HTTP
client; `ItemMetadataServiceTests.RetryWaveFailure_DegradesToPartialResult_DoesNotThrow`
and `TransientPartialResponse_RetriedOnce` for the metadata layer;
`VendorOfferResolverTests` for wiki-fetch retry/cancellation). Nothing at
the pipeline level proves what a user actually experiences if, say, the
price API throws for one batch while item metadata succeeds — does the
whole "Generate Plan" click surface a clean error, a partial plan, or an
unhandled exception on the (Blish, main-thread-marshalled) UI? That's the
actual failure surface a real user hits, and it's currently only inferable
from reading code, not proven by a test.

**Root cause, and why `TradingPostServiceTests` in particular has zero
failure-mode tests (Finding T9, minor but explains T8's blind spot):**
`Helpers/InMemoryPriceApiClient.cs` (39 lines) has **no failure-injection
capability at all** — no `LatencyMs`, no `ThrowOnCallNumber`/`SetFailures`
equivalent — unlike its three siblings: `InMemoryItemApiClient` has
`ThrowOnCallNumber` + `DropOnce` (partial-response simulation, used by
`ItemMetadataServiceTests`'s degrade/retry tests); `InMemoryRecipeApiClient`
and `InMemoryWikiVendorClient` both have `LatencyMs` + failure counters
(used by the concurrency/cancellation tests above). Because the fixture
literally cannot simulate a price-fetch failure, `TradingPostServiceTests`
(11 tests) has no way to exercise that path even if someone wanted to add
one. This is the concrete, mechanical fix underlying T8's price-layer blind
spot: add the same `ThrowOnCallNumber`-style hook to `InMemoryPriceApiClient`
to bring it to parity with its siblings, then write the corresponding
`TradingPostServiceTests` case.

---

## 4. Brittleness

**Finding T10 (labeled intentional, not a bug — but worth naming): three
tests pin an exact row-count against a real, actively-edited shipped data
file.** `RecipeCacheSerializerTests.LoadRecipeSeed_ShippedSeedFile_ParsesAllRowsIncludingAchievementRecipes`
asserts `Assert.Equal(14736, recipes.Count)` against the live
`ref/recipes_seed.json`. The test's own doc comment frames this as
deliberate ("pins the real file against silent drift" —
`RecipeCacheSerializerTests.cs:18`), which is a legitimate and useful
trip-wire pattern, not an oversight. But `recipes_seed.json` is exactly the
file KNOWN-ISSUES shows being edited nearly every milestone (M33 added
Mystic Clover recipe -1591, M33 added ~20 item names, etc.) — every
legitimate future seed addition will make this specific assertion fail, and
the failure message ("expected 14736, got 14737") gives the developer no
signal about *what* changed, only that *something* did. This is a fair
trade-off (a loud, unambiguous trip-wire beats silent drift) but it is
brittle by design and should be called out explicitly in the milestone
execution plan as "expect to touch this line whenever `ref/recipes_seed.json`
changes" rather than something to be surprised by. The two other
shipped-file pins (`ref/recipe_search_seed.json`,
`ref/acquisition_hints_seed.json`, 6 entries) are much lower-churn files, so
the same pattern is far less brittle there.

**Finding T11 (minor): real-timing tests, revisited from §0.** The same
`CancelAfter(150)`-vs-`LatencyMs=100` and `CancelAfter(500)`-vs-`LatencyMs=200`
margins noted in Finding T1 are a brittleness concern as much as a
performance one — on a heavily loaded CI runner or a slow dev machine, a
150ms cancellation window racing a 100ms-per-item fake latency with 5 BFS
levels queued has a real (if currently unobserved) chance of finishing
"too fast" or "too slow" and flipping the assertion. No failure was
observed in two measured runs, so this is not an active problem, but it's a
latent one worth a mental flag for anyone touching
`RecipeServiceConcurrencyTests.CancellationStopsPreWarm` or
`VendorOfferResolverTests.CancellationStopsRequests`.

No brittleness was found around hardcoded item/recipe IDs used as
*representative* test fixtures (e.g. `ZojjasClaymoreValidationTests`'s use of
the real Zojja's Claymore id 46762, or `MysticForgeIntegrationTests`'s use of
the real Mystic Clover id 19675) — those tests build their own fully
synthetic `InMemoryRecipeApiClient` graphs and never touch a live or
seed-file source for the numbers they assert on; the real ids are used only
for readability/traceability to the real item, which is a deliberate,
documented, and reasonable choice (see the class doc comments), not
data-coupling.

---

## 5. Organization, naming, and structure

**Naming**: broadly consistent. The dominant convention across both
projects is `MethodOrScenario_Condition_ExpectedResult` (e.g.
`MultiOccurrenceBulkVendorOffer_CurrencyCost_AggregatesBeforeCeiling`,
`ResolveWithOverrides_IgnoredItemIds_ZeroesIngredientCost`), with a smaller
set of single-clause names for simpler cases
(`ConcurrencyDoesNotExceedMaxDegreeOfParallelism`,
`CancellationStopsRequests`). I did not find snake_case, `Test1`-style, or
`Should_`-prefixed outliers in either project. This is a non-issue; no
action needed.

**File size / structural risk (Finding T12, structural — not urgent, but
real):** `PlanSolverTests.cs` (2705 lines, 88 cases) and
`PlanViewModelBuilderTests.cs` (1770 lines, 93 cases), followed by
`CraftingPlanPipelineTests.cs` (1857 lines, 43 cases) and
`MultiItemPlanTests.cs` (1498 lines, 16 cases — the fewest tests of the four
largest files, i.e. large multi-item fixture setup rather than many small
cases), are large single-class files. Each already self-organizes with
`// --- Section (M-number reference) ---` banner comments (17 sections in
`PlanSolverTests.cs`, 22 in `PlanViewModelBuilderTests.cs`) that map cleanly
onto milestones — the raw material for a mechanical split already exists in
the file. Given the project's own CLAUDE.md explicitly worries about "future
merge hotspots," and given this milestone's stated concern about concurrent
branches landing changes in overlapping areas, a single 2705-line test file
that any PlanSolver-touching change must diff against is a plausible
hotspot. **This is explicitly a REDESIGN-adjacent, not CLEANUP, item** —
splitting a test class by milestone-tagged region into multiple files (e.g.
`PlanSolverTests.cs` + `PlanSolverVendorBatchingTests.cs` +
`PlanSolverIgnoreTests.cs` ...) is mechanical and behavior-preserving in
principle, but doing it safely across 2700 lines of interdependent private
helpers (`Leaf`/`Craftable`/`Option`/`CoinVendorOffer`/`MixedVendorOffer`/
`WrapperOf`) needs care to avoid accidentally duplicating or losing a helper
in the split — flag for the execution plan as a distinct, reviewable step,
not a drive-by rename.

**Cross-project structural finding (Finding T13, the most structurally
interesting finding from this lens): `VendorOfferHasher` is implemented
twice — once in the module (`Services/VendorOfferHasher.cs`) and once in the
standalone tool (`tools/VendorOfferUpdater/VendorOfferHasher.cs`) — and each
copy has its own, entirely separate test suite
(`tests/GW2CraftingHelper.Tests/Services/VendorOfferHasherTests.cs`, 10
cases, vs. `tests/VendorOfferUpdater.Tests/VendorOfferHasherTests.cs`, same
case names, same assertions) with no test anywhere proving the two
implementations agree.** I diffed the two production files directly: the
string-composition logic (what goes into the hash — item id, output count,
cost lines, merchant name, locations) is byte-for-byte identical; only the
final digest-to-hex step differs, and only because it has to
(`Services/VendorOfferHasher.cs` targets net48 and hand-rolls
`SHA256.Create()` + a byte-to-hex loop; `tools/VendorOfferUpdater/VendorOfferHasher.cs`
targets net8.0 and uses the newer `SHA256.HashData` + `Convert.ToHexString`).
This split exists for a real, defensible reason — `VendorOfferUpdater` is a
separate offline dev-time tool on a newer TFM, consistent with the "no
Gw2Sharp/Blish in tests" and "gw2efficiency is research-only, module never
calls it at runtime" separation the repo already enforces. But it means the
tool that *generates* `ref/vendor_offers.json`'s `OfferId` values and the
module that *reads/dedupes by* those same `OfferId` values are two
independently-maintained algorithms with zero automated proof they still
compute the same hash for the same input. A future edit to the
string-composition lines in one file without the other (e.g. changing
cost-line ordering, or how `null` locations are handled) would silently
desync offer IDs between the tool and the module — and **both** test suites
would stay green throughout, each blind to the other, because each only
ever calls its own copy. This is a structural test-infra gap, not a quick
win: the fix isn't "add a test," it's "add a shared golden-vector fixture"
— e.g. a small JSON file of `{inputs, expectedHash}` pairs checked into
`ref/` or `tests/` that *both* test projects load and assert against, so a
divergence in either copy's hashing logic fails in both suites
simultaneously. Flagging for the synthesis as a cross-project test-infra
task, not a one-file fix.

---

## 6. Snapshot / temp-dir hygiene

Every filesystem-touching test uses a fresh `Guid.NewGuid()`-suffixed temp
directory and either an `IDisposable.Dispose()` or a `try/finally` to delete
it (best-effort, swallowing delete failures — reasonable, since a
locked-file delete failure on Windows/WSL shouldn't fail the test itself).
No test writes into a shared/fixed path, no test leaves state for a
following test to accidentally depend on. The only issue found here is the
duplication already covered in Finding T4 (two different idioms for the same
correct behavior) — there is no actual leak or cross-test contamination risk
identified.

**Finding T14 (nice-to-have, hygiene/DX only): production `Console.WriteLine`
diagnostic logging bleeds into `VendorOfferUpdater.Tests`' `dotnet test`
output.** MEASURED: running the suite produces interleaved lines like
`[A] offset=0 +1 new` and `[all] sub-partitions done, 35/36 empty prefixes
skipped` mixed in with the `Passed .../[N ms]` lines. Traced to
`tools/VendorOfferUpdater/WikiSmwClient.cs` (e.g. lines 199, 231-247,
294-327), which unconditionally writes progress/diagnostic text to
`Console.Out` — and because `WikiSmwClientTests.cs` correctly exercises the
real `WikiSmwClient` production code (good, per the realism rule), that
logging necessarily leaks into test-run output. Harmless to correctness,
but it makes scanning a large CI log for an actual failure line noisier than
it needs to be. Not proposing removing the production logging (it may be
load-bearing for the tool's interactive CLI use) — just noting the noise
for anyone doing a "make CI output clean" pass; a `TextWriter`
abstraction the tests could redirect to `TextWriter.Null` would be the
minimal fix if this is ever prioritized.

---

## 7. Summary — quick wins vs. structural work

**Quick wins (mechanical, behavior-preserving, low risk — good fits for a
single small PR each):**
- T2: extract shared `Leaf`/`Craftable`/`Option` RecipeNode builders (4 call
  sites, 2 of them byte-identical today) into `Helpers/RecipeNodeBuilders.cs`.
- T3: extract the duplicated `FindRepoFile` helper into `Helpers/`.
- T4: extract a `Helpers/TempDirectory : IDisposable` and replace ~12 inline
  create/try/finally/delete blocks across `CraftingPlanPipelineTests.cs`,
  `MultiItemPlanTests.cs`, `RecipeCacheStoreTests.cs`.
- T5: `using System.IO;` cleanup in the two files above (bundle with T4).
- T7: add "shipped file parses without throwing" pin tests for
  `ref/vendor_offers.json`, `ref/mystic_forge_recipes.json`,
  `ref/item_name_seed.json` (same shape as the three that already exist).
- T9: add `ThrowOnCallNumber`-style failure injection to
  `InMemoryPriceApiClient` for parity with its siblings (unblocks a future
  `TradingPostServiceTests` negative-path test).
- T14: optional — redirect/suppress `WikiSmwClient`'s console logging during
  test runs.

**Coverage gaps to close (new tests, no production-code change implied):**
- T6: at least one test combining Ignore + partial/full ownership on the
  same node, at both the `PlanSolver`/`CraftingPlanPipeline` layer and the
  `DecisionPillPlanner` display layer — proving (whatever the intended
  behavior is) that the interaction is deliberate, not accidental.
- T8: pipeline-level cancellation test and at least one "a dependency
  throws mid-`GenerateStructuredAsync`" test in `CraftingPlanPipelineTests`,
  once T9 makes it possible to simulate on the price side (item/recipe side
  already has the fixture support).

**Structural / needs-careful-review work (label these REDESIGN-adjacent in
the execution plan, not drive-by cleanup):**
- T12: split `PlanSolverTests.cs` (2705 lines/88 tests) and
  `PlanViewModelBuilderTests.cs` (1770 lines/93 tests) along their existing
  `// --- Section ---` banners into multiple files/partial classes, to
  reduce merge-hotspot risk — mechanical in principle, but needs a careful
  pass over shared private helpers to avoid duplicating or dropping one
  during the split.
- T13: introduce a shared golden-vector fixture so
  `Services/VendorOfferHasher` and `tools/VendorOfferUpdater/VendorOfferHasher`
  (two independently-maintained, currently-identical hashing
  implementations, one per TFM) are proven to agree by an automated test
  that would fail if either drifts — currently nothing would catch that.

**Named, but explicitly not recommending action on:** T1/T11 (real-delay
concurrency/cancellation tests) — flagged as the dominant runtime cost and a
latent (never-observed) flakiness risk, but replacing real `Task.Delay` with
a virtual clock is itself a small piece of test infrastructure with its own
risk/benefit tradeoff that the synthesis should weigh deliberately rather
than default into; T10 (exact-count seed pins) — working as intended, just
naming the maintenance cost so it isn't mistaken for an oversight later.

---

## Positive findings worth preserving (do not "clean up" these away)

- The Blish-free pure-math extraction pattern (`WheelDeltaSanitizer`,
  `StatusUpdateGuard`, `PlanContentHeightMath`, `ScrollMath`,
  `PlanRelayoutMath`, `ShoppingColumnMath`) paired with focused, thorough
  unit tests for each is directly why the essential complexity documented in
  `docs/KNOWN-ISSUES.md` is testable at all despite the module being
  Blish-bound. This is the single best structural decision in the codebase
  from a testability standpoint and should be the template for any *new*
  essential-complexity extraction the M38 pass produces, not something to
  simplify.
- No skipped tests, no contract-mirror tests found, no fake file I/O,
  no Blish/Gw2Sharp references leaked into either test project (spot-checked
  via the same grep the repo's own invariant describes) — the "Blish-free
  tests" and "real production code paths" rules are holding up well under
  848+ tests of milestone churn.
