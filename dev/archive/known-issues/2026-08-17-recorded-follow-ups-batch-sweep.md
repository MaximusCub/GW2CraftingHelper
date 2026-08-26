> **Frozen record - 2026-08-17, branch `recorded-follow-ups-batch-sweep`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Recorded follow-ups batch sweep (2026-08-17)

**Milestone goal:** four small, explicitly-logged non-blocking follow-ups
from recent review rounds, taken now as a batch, plus a sweep of
docs/KNOWN-ISSUES.md's last two days of sections for any other purely
cosmetic (comment/doc/test-only) follow-up bullets cheap enough to take
alongside them.

**What changed:**
1. **Cooldown notice wording (`Services/PlanViewModelBuilder.cs`,
   `AppendDailyCooldownNotices`).** The "(runs in parallel with other
   daily-gated items)" clause rendered unconditionally on every daily
   craft-cooldown notice, even a lone one with no sibling notice to run
   in parallel with. The method now collects qualifying notices first
   and appends the clause only when the plan has 2+ of them.
   Wording corrected on a later follow-up sweep (recorded-followups-
   sweep verification finding): the 2+ count (`pending`) only ever
   counts daily craft-cooldown notices from this loop, never the
   separate Daily-cap vendor notices this same section also emits from
   `Plan.TimegatedItems` - so a plan with exactly one craft-cooldown
   notice running alongside a Daily-cap vendor notice IS genuinely
   running in parallel with another daily-gated item, yet the old
   "daily-gated" wording implied that broader population was what the
   gate measured. The clause text is now "(runs in parallel with other
   daily-crafted items)", naming only the population the count actually
   covers.
   `PlanViewModelBuilderDailyCooldownTests` updated: the existing single-
   notice test now asserts the clause is absent, and a new
   `TwoCraftCooldownNotices_BothAppendParallelClause` test pins it
   present on both rows of a 2-notice plan.
2. **`AcquisitionHintServiceTests.cs` header comment.** Corrected the
   claim that the in-file fixture "mirrors" the real
   `ref/acquisition_hints_seed.json` content - the fixture holds 6 of
   the now-7 seed entries. Reworded to describe the fixture as an
   isolated parsing-shape exercise, pointing drift coverage at the
   separate `Load_ShippedSeedFile_*` test that pins the real file.
3. **`PlanSolveContext.CompetencyIndependentForceBuyNodeIds` persistence
   gap (srcsel verification finding).** `ForceBuyOnlyNodeIds` had a
   dedicated `PlanStoreTests` round-trip test; its sibling
   `CompetencyIndependentForceBuyNodeIds` had none. Added two real
   `PlanStore`/`PlanStoreHelpers` round-trip tests: a populated set
   surviving save+load, and both sets round-tripping correctly as an
   explicit JSON `null` (`OwnMaterialsMode.Free`, pre-pass never ran)
   without `PlanStructuralValidator` rejecting the reload. (The new
   test's own comment initially assumed Newtonsoft omits a null
   property from the written JSON entirely - measured false, the
   project uses no custom `JsonSerializerSettings` so the default
   `NullValueHandling.Include` writes an explicit `null`; the test and
   its comment were corrected to assert that instead before this was
   committed.)
4. **`ForceBuyPrePassResult` doc nuance
   (`Services/OwnedMaterialsForceBuyPrePass.cs`) - direction corrected
   (recorded-followups-sweep verification finding).** The doc comment
   read as if the "competency-blind" raw evaluation was training-
   independent top to bottom. In reality it is competency-blind only at
   the node's OWN recipe choice (picks the cheapest recipe among
   `node.Recipes` regardless of training); each child ingredient's
   contribution to that raw figure still comes from
   `PlanSolver.Evaluate`'s normal competency-RESOLVED recursive call
   (`bestRatingByDiscipline` threaded through), which makes the raw
   craft cost look pricier than a truly training-blind figure would.
   An earlier version of this entry (and the doc comment it described)
   drew the wrong conclusion from that correct premise: membership is
   `buyCost < rawCraftCost * 0.85`, so an INFLATED rawCraftCost only
   makes that test EASIER to satisfy - it can only ADD nodes to
   `CompetencyIndependentForceBuyNodeIds`, never cause a miss. The real
   risk is the opposite of what was originally written: a parent node
   whose own untrained recipe would genuinely survive a true blind
   evaluation can still be pulled into the set by a resolved child's
   inflated cost, suppressing that PARENT's own
   `Decision.CheapestCraftUntrained` - i.e. this can falsely EXCLUDE a
   real training opportunity, not miss an independent one. Corrected the
   doc comment and this entry to state that direction plainly. No
   runtime behavior changed; the code on this branch is unchanged from
   master.

**Sweep of docs/KNOWN-ISSUES.md's last two days of sections (2026-08-15/
16, plus the Festival-vendor entry's own later-dated review-fix notes)
for other pure comment/doc/test "follow-up:"/"nice-to-have" bullets:**
every other open item found is either (a) a real feature/behavior change
deferred out of scope (shopping-list caveat threading, wiki-scrape
auto-detection of new seasonal vendors, per-vendor tag-coverage
incoherence, currency-id pluralization judgment calls, extracting a
shared discipline-tag constant set, `ExcessCraftOutputCalculator`'s
recursion depth, the caret-tooltip sweep across three untouched
renderer files), (b) already resolved/taken in an earlier pass (PART D's
own nice-to-haves, the currency-name-index guard test, the
`DailyCooldownItemService.Load` `ItemId <= 0` guard), (c) an accepted
design tradeoff explicitly not a bug (the currency-valuation snapshot
staleness note, the sticky seasonal-tag limitation), or (d) touches a
DO-NOT-TOUCH file (`PlanContentHeightMath`'s tree-arm caption-row
widening). None qualified as both pure comment/doc/test AND genuinely
cheap beyond the four items already taken above - nothing further was
taken.

**Validation performed:**
- Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
  C:/Dev/Blish/wt-followups/GW2CraftingHelper.csproj -p:Platform=x64` -
  0 errors (1782 pre-existing StyleCop warnings, none in any file this
  pass touched).
- Tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test
  C:/Dev/Blish/wt-followups/tests/GW2CraftingHelper.Tests/
  GW2CraftingHelper.Tests.csproj` - 1768/1768 green (baseline 1765 + 3
  new: `TwoCraftCooldownNotices_BothAppendParallelClause`,
  `Save_Load_CompetencyIndependentForceBuyNodeIds_
  PopulatedSetRoundTrips`, `Save_Load_ForceBuyNodeIdSets_NullInJson_
  DeserializeToNullWithoutValidatorRejection`). One test failed on first
  run (the "absent-in-JSON" assumption above) and was corrected before
  the final green run.
- Manual: `git status --short` confirmed no intermediate cache files
  (`ref/wiki_vendor_cache.json`/`ref/item_id_cache.json`) were touched;
  a full-diff ASCII scan (`grep -P '[^\x00-\x7F]'`) confirmed no non-ASCII
  bytes (and therefore no em-dashes) in any touched file.

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests
- [x] Tests exercise real production paths
- [x] No fake file I/O tests introduced
- [x] Pricing logic preserves multi-source correctness (no cost/pricing
  logic touched at all - wording, a doc comment, and two new persistence
  tests only)
- [x] IDs remain internal-only (not displayed)

**Risks / follow-ups:** none new; the sweep's own "not taken" list above
restates why each remaining candidate stays open.

Gate: not applicable - comment/test/wording cleanup with no visual surface beyond a conditional notice clause (suite-pinned). Merged under the maintainer's standing merge directive (2026-08-16).
