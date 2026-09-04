> **Frozen record - 2026-08-16, branch `audit-row-56-daily-craft-cooldown-notices-three`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## AUDIT ROW 56: daily craft-cooldown notices + three small fixes (2026-08-16)

### PART A: daily craft-cooldown notices

Server-enforced daily crafting cooldowns (as distinct from the existing
M34-B1 #3 vendor PURCHASE cap notices - see `TimegatedItem`/
`TimegatedCapType`) were entirely unmodeled: a plan telling the user to
craft e.g. 30 Lump of Mithrillium via `AcquisitionSource.Craft` omitted
the ~30 real-world days that recipe's own daily reset actually requires.

Fix, purely additive/informational, no solver or pricing change,
`VendorBatchSolver` untouched:

- `ref/daily_cooldown_items.json` (new, mirrors
  `ref/acquisition_hints_seed.json`'s precedent): 15 wiki-verified
  entries, each with an item id, a per-day cap, and a
  `wiki.guildwars2.com` citation. Curated by fetching each candidate
  item's RAW wikitext (`index.php?title=...&action=raw`) via
  `api.php`/`index.php`, not by trusting a suggested item list at face
  value - that research turned up a real correction: the suggested set
  (Deldrimor Steel Ingot, Spiritwood Plank, Elonian
  Leather Square, Bolt of Damask, Xunlai Electrum Ingot) are the
  ascended-refinement STEP-2 outputs, and the wiki confirms those five
  are explicitly NOT recipe-capped ("The step 2 materials are not
  time-gated and can be traded on the Trading Post" -
  `wiki.guildwars2.com/wiki/Crafting_material#Ascended_crafting_materials`).
  The real daily cap lives one tier earlier, on the STEP-1 precursor
  each of those five (four of them; Xunlai Electrum Ingot shares
  Deldrimor's own precursor) consumes: Lump of Mithrillium, Glob of
  Elder Spirit Residue, Spool of Silk Weaving Thread, Spool of Thick
  Elonian Cord - each confirmed via its own item page's raw wikitext
  ("This item can only be acquired once per day per account...",
  `timegate = y`, `[[Category:Time gated recipes]]`). The suggested
  "obsidian refinement" example did NOT verify - the wiki's own Obsidian
  Refinement subsection explicitly has no time-gating note ("unlike the
  Ectoplasm Refinement section above it") and Vision Crystal's own
  recipe carries no `timegate` flag - so no obsidian-refinement entry
  was added: an entry that cannot be verified is not included. The
  remaining eleven entries (Heat Stone, Clay Pot, Vial of Maize Balm,
  Gossamer Stuffing, Grow Lamp, Plate of Meaty
  Plant Food, Plate of Piquant Plant Food, plus the four Dragon Hatchling
  Doll parts below) came from the wiki's own
  `Category:Time gated recipes` listing.
  - **Review fix (audit row 56 PART C, finding 1):** the first cut of
    this seed excluded four `Category:Time gated recipes` members - the
    Dragon Hatchling Doll parts (Adornments 79795, Eye 79726, Frame
    79817, Hide 79790) - on the stated grounds that they "carry
    `timegate = y` but no explicit prose sentence on their own pages,"
    while keeping Gossamer Stuffing (79763), a fifth Dragon Hatchling
    Doll component from the same category. Re-checking each item's raw
    wikitext (`index.php?action=raw`) shows Gossamer Stuffing's own page
    has exactly the same evidence shape as the four excluded parts -
    only `| timegate = y` plus `[[Category:Time gated recipes]]`, no
    separate prose sentence either - so the exclusion line was not a
    real distinction, it just never re-checked the one entry it had
    already decided to keep. All four omitted items are real and
    reachable (live API recipes 11885/11878/11888/11889, confirmed
    outputs in `ref/recipes_seed.json`), and a Gift of Aurene plan crafts
    all five Dragon Hatchling Doll parts together - warning on 1 of 5 and
    staying silent on the other 4 read as "the other 4 are
    unconstrained," which is worse than warning on none. Fixed by adding
    all four at `perDayCap 1` (the cap `timegate = y` itself signals, same
    as every other entry in this seed) rather than dropping Gossamer
    Stuffing for consistency, since all five are genuinely capped.
  - **Review fix (audit row 56 PART C, finding 2):** the first cut also
    seeded Charged Quartz Crystal (43772, the task's other named
    example) at `perDayCap 1` - real per the wiki, but dead data in this
    module: `AppendDailyCooldownNotices` only ever inspects
    `AcquisitionSource.Craft` steps, and Charged Quartz Crystal is made
    at a Place of Power, not via any recipe this module resolves - it is
    not a recipe OUTPUT anywhere in `ref/recipes_seed.json` or
    `ref/mystic_forge_recipes.json` (`GET /v2/recipes/search?output=43772`
    also returns `[]`), and it has no `ref/acquisition_hints_seed.json`
    entry either. A plan needing 30 of them surfaces as a
    shopping/unknown leaf with no cooldown warning at all, while the
    seed entry made it look covered. Removed the entry;
    `DailyCooldownItemServiceTests` now pins its absence as a regression
    guard. **General limitation this exposes, not fully fixed here:**
    the notice pass only ever covers items reachable via a Craft step -
    any gated item whose recipe the account has not learned, or that is
    produced by a non-recipe mechanic (Place of Power, achievement
    reward, etc.), resolves to a non-Craft row today and gets no
    cooldown notice regardless of whether it is in this seed. Extending
    the pass to also cover `ShoppingUnknown`/non-craft rows is a real
    follow-up, out of scope for this fix.
  - **`itemName`/`note` fields are internal-only documentation.**
    `DailyCooldownItemService.Load` never reads either field (see its
    own `DailyCooldownEntry` shape) and no test pins them - they exist
    purely to make the JSON file human-readable during curation/review
    and can drift from the live API silently if an item is ever renamed.
    Spot-checked against `GET /v2/items` for all entries as of this
    review; not otherwise enforced.
- `Models/DailyCooldownItem.cs` / `Services/DailyCooldownItemService.cs`
  (new): loader, byte-for-byte the same shape/never-throws contract as
  `AcquisitionHintService.Load`.
- `CraftingPlanResult.DailyCooldownItems` / `PlanSolveContext.
  DailyCooldownItems` (new fields) wired through `CraftingPlanPipeline`
  at every site `AcquisitionHints` already flows through (both
  `GenerateStructured*Async` result-builds + their `PlanSolveContext`
  snapshots, plus `ResolveWithOverrides`) - loaded once in `Module.cs`
  with the same try/catch-degrades-to-null seed-load convention as the
  acquisition hints seed immediately above it.
- `PlanViewModelBuilder.AppendDailyCooldownNotices` (new, called from
  `BuildCraftingStepsSection`): an additive pass over the section's
  already-filtered Craft-source steps. A step whose aggregate `Quantity`
  exceeds the seed's `PerDayCap` for that item id gets one
  `PlanRowType.TimegatedNotice` row appended - reusing that row's exact
  plain-`Label`-text shape (the same generic `TextRowRenderer` branch
  the pre-existing vendor-cap notices already render through, see
  `CraftStepsSectionRenderer.Render`), never the `TimegatedItem`
  model/`Plan.TimegatedItems` list itself, so a recipe-level cooldown
  can never be confused with (or accidentally validated by
  `PlanStructuralValidator` as) a vendor purchase cap. Wording: `"{item}
  is timegated - {cap} per day per account - crafting {qty} will take
  about {days} day(s)"`, `days = Ceiling(qty / cap)`.

Tests (new): `DailyCooldownItemServiceTests` (7 cases, mirrors
`AcquisitionHintServiceTests` including a shipped-seed-file pin) and
`PlanViewModelBuilderDailyCooldownTests` (7 cases: exceeds-cap,
at-cap/no-notice, not-in-seed, null-seed-no-throw, non-Craft-step
never triggers, non-divisible-quantity rounds up, and a vendor-cap +
craft-cooldown notice coexisting in one section).

### PART B: three small fixes

1. **Magenta missing-texture icons.** `Views/MainView.cs`'s
   `CreateItemRow`/`CreateWalletRow` (Snapshot tab - the reported case
   was the Spirit Shards wallet row) and `Views/SuggestionPanel.cs`'s
   search-suggestion rows all fell back to `ContentService.Textures.
   Error` - Blish's alarming magenta missing-texture placeholder -
   whenever `IconUrl` was empty, conflating an ordinary data gap with a
   genuine texture-load failure. All three now call the existing
   `Views/Rendering/IconControls.CreateItemIcon` helper (which already
   degrades an empty `iconUrl` to a neutral dark-grey empty-slot square,
   used everywhere else in the crafting-plan tree/rows), removing the
   duplicated inline icon-loading logic entirely rather than patching it
   three times. `Module.cs`'s own `ContentService.Textures.Error`
   fallback (module icon texture failed to *load*, a real failure) is a
   different case and was left untouched.
2. **`Gw2Constants.KnownCurrencyNames` audit.** Verified every existing
   entry against a live `GET /v2/currencies?ids=all&v=2022-03-23` fetch
   (2026-08-16). The six ids the task flagged as pre-ingestion
   mispairs (36, 49, 50, 58, 59, 60) are already correctly paired on the
   current master - the `recipe-ingestion-fix` PR (#113) already fixed
   them. The one real remaining gap: id 68 (Imperial Favor, a Cantha
   vendor currency) was missing from the dict entirely, so any plan
   costing it fell back to the generic "Currency" label via
   `ResolveCurrencyName` - added as `{ 68, "Imperial Favors" }`,
   matching the dict's own established singular-API-name -> pluralized-
   display-name convention (confirmed exceptionless across all ~44
   pre-existing entries before extending it). New test:
   `Gw2ConstantsCurrencyNamesTests` (5 cases) pins 19 ids' exact display
   strings against a real, verbatim-captured `/v2/currencies` snapshot
   (not invented), including an explicit "id 60 is Tyrian Defense Seal,
   not Imperial Favor - real Imperial Favor is id 68" regression guard
   for the exact bug class this audit found.
   - **Not fixed, flagged for a follow-up pass:** `ref/vendor_offers.json`
     references ~19 further currency ids with no `KnownCurrencyNames`
     entry at all (31, 35, 46, 52-54, 57, 64, 66, 69, 70, 72, 73, 75-77,
     81-83 - Legendary Insight and Ancient Coin among them). Each would
     need its own real-vs-mass-noun pluralization judgment call the task
     did not ask for and this pass did not verify community-standard
     wording for; left as a known completeness gap rather than guessed.
3. **Two stale/incorrect gw2efficiency-provenance doc comments.**
   `Views/Rendering/TreeSectionController.cs`'s dimmed-reference-branch
   comment claimed the branch was "gw2e's `.not-crafted` informational
   reference branch" - gw2efficiency has no such concept; it is a module
   original. `Services/AccountCurrencyIndex.cs` claimed gw2efficiency
   "only ever nets owned currency out at the summary layer" - gw2e
   also has a per-node "owned" pill on the tree itself. Both comments
   corrected in place; no behavior change.

### PART C: code-review fixes (post-merge review round)

1. **`ref/daily_cooldown_items.json` coverage/consistency fixes** - see
   the corrections inline in PART A above: added the four Dragon
   Hatchling Doll parts (finding 1), removed the dead Charged Quartz
   Crystal entry (finding 2), documented the Craft-step-only limitation
   this exposes, and noted `itemName`/`note` are internal-only
   documentation fields. Seed count: 15 (was 12: +4 Dragon Hatchling
   Doll parts, -1 Charged Quartz Crystal). `DailyCooldownItemServiceTests`
   extended (same `[Fact]` methods, more assertions - no test count
   change) to pin the four new ids at `perDayCap 1` and pin 43772's
   absence as a regression guard.
2. **`Gw2ConstantsCurrencyNamesTests` was a contract-mirror test.** Its
   sole non-trivial assertion compared `Gw2Constants.KnownCurrencyNames`
   against `ExpectedDictName`, a hand-copied duplicate of that same
   production dictionary - `LiveApiNameById` (the "real snapshot" the
   file's doc comment sells) was only ever interpolated into a failure
   message, never asserted against, so the test would have passed
   unchanged even if every `LiveApiNameById` value were wrong. Fixed by
   asserting `ExpectedDictName[id]` against `LiveApiNameById[id]` for
   every pinned id (equal, or the dict's established pluralization of
   it) alongside the existing equality check, so a future entry added
   with a mispaired id now fails instead of sailing through. No
   production code changed; the underlying data was independently
   re-verified against a live fetch and found correct.
3. **Dead singular-day branch removed.** `PlanViewModelBuilder.
   AppendDailyCooldownNotices`'s `day{(days == 1 ? "" : "s")}` was
   unreachable - the loop `continue`s whenever `step.Quantity <=
   cooldown.PerDayCap`, so every emitted notice already has `days =
   Ceiling(qty / cap) >= 2`. Simplified to the always-true plural form;
   no behavior change (existing tests already only assert the plural
   wording).
4. **`Services/AccountCurrencyIndex.cs` doc comment precision.** The
   PART B #3 correction above (gw2e nets owned currency out via a
   per-node pill, not summary-layer-only) is now explicit that the pill
   is *display* only - gw2e's own quantity engine never nets owned
   currency into a decision either (matching this class and
   `docs/research/gw2e-convergence-matrix.md`'s `calculateTreeQuantity.
   ts` finding), so the correction cannot be misread as gw2e netting
   currency into decision math.
5. **`CraftingPlanResultBuilders.MakeResult`'s `dailyCooldownItems`
   parameter moved to the end of the parameter list**, matching
   `CraftingPlanPipeline`'s own constructor convention (which appends it
   after `moduleLog` for exactly this reason) - it previously sat
   between `acquisitionHints` and `timegatedItems`, a positional hazard
   for any future caller not using named arguments. All 119 existing
   `MakeResult(...)` call sites use named arguments, so this is a
   no-op for current callers.
6. **`docs/gw2e-considerations.md` Section 12 / `docs/research/gw2e-
   convergence-matrix.md` row 46 marked resolved**, pointing at the
   PART B #3 fix - both previously still described the
   `TreeSectionController` provenance comment as an open
   recommendation after it had already been corrected.
7. **Dangling `FindRepoFile` comment in `DailyCooldownItemServiceTests.cs`
   moved** to sit with the `using static` it documents, rather than
   floating disconnected at the end of the class body.

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-cooldowns/GW2CraftingHelper.csproj -p:Platform=x64` -
0 errors, warning count/content unchanged from baseline (all new
warnings, if any, are the project's pre-existing StyleCop noise
pattern, not introduced by this change; confirmed no new warning in
any file this round touched). Tests: `"/mnt/c/Program
Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-cooldowns/tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` -
1429 passed, 0 failed (1410 baseline + 14 PART A + 5 PART B; PART C
added assertions to existing `[Fact]`s rather than new ones, so the
total is unchanged). No Blish HUD/BlishHUD.exe reference in any test
file; every new/changed assertion exercises a real production entry
point (`DailyCooldownItemService.Load`, `PlanViewModelBuilder.Build`,
`Gw2Constants.KnownCurrencyNames`/`ResolveCurrencyName` directly), no
contract mirrors, no fake file I/O (the shipped-seed-file test reads
the real `ref/daily_cooldown_items.json` from disk via the existing
`RepoFileLocator` helper). IDs remain internal-only - the new craft-
cooldown notice text never surfaces an item id, only its resolved name.
No live desktop verification was performed - `Views/MainView.cs`,
`Views/SuggestionPanel.cs`, and `Views/Rendering/TreeSectionController.cs`
are all Blish-bound and outside this repo's test-runnable surface; the
icon-placeholder fix in particular (a Snapshot-tab render change) has
not been visually confirmed in a live client.

### PART D: post-PART-C follow-up review fixes

1. **Coverage gap against a named target: Charged Quartz Crystal
   (43772).** PART C finding 2 correctly removed 43772 from
   `ref/daily_cooldown_items.json` (dead data - the notice pass only ever
   inspects `AcquisitionSource.Craft` steps, and 43772 is never a recipe
   output). But 43772 is one of this work's named motivating examples,
   is `AccountBound`/no-TP/no-vendor-offer, and had no
   `ref/acquisition_hints_seed.json` entry either - so it resolved to a
   `ShoppingUnknown` leaf with zero timegate signal at all. Concrete
   consequence: a plan for one Grow Lamp (66993, seeded) needs 10x Charged
   Quartz Crystal and emitted no notice anywhere. Fixed with the minimal,
   fully-additive remedy: added a `ref/acquisition_hints_seed.json` entry
   for 43772 (hint text names the Place of Power source, the 1-per-day
   cap, and account-bound/no-TP status; badge `DAILY`), reusing the
   existing `ShoppingUnknown` hint/badge path
   (`PlanViewModelBuilder.ResolveHintText`/`ResolveBadgeText`) with no new
   code. `AcquisitionHintServiceTests.Load_ShippedSeedFile_*` updated
   (6 -> 7 entries) to pin the new entry. The general limitation PART C
   already documented (the notice pass only ever covers `Craft`-source
   steps) still stands and is unchanged by this fix.
2. **Nice-to-haves taken alongside (all cheap, same-file as their own
   finding):**
   - `Models/DailyCooldownItem.cs`: `PerDayCap`'s doc comment now states
     it is output-UNITS per day (matching how
     `AppendDailyCooldownNotices` actually compares it against
     `PlanStep.Quantity`) and flags that every seeded recipe today has
     `output_item_count == 1`, so a future multi-output entry would need
     the comparison divided by the recipe's own output count, not
     `PerDayCap` reinterpreted. No behavior change (still latent, no
     seeded recipe triggers it).
   - `Services/PlanViewModelBuilder.cs`: craft-cooldown notice wording
     appends "(runs in parallel with other daily-gated items)" - each row
     was already individually accurate, but nothing said multiple rows'
     day-estimates are independent maxima, not a sum (the flagship
     Gift of Aurene / multi-Dragon-Hatchling-Doll-component case).
     `PlanViewModelBuilderDailyCooldownTests`' existing substring
     assertions (`"30 days"`, `"3 days"`) still pass unchanged.
   - `tests/.../Services/CraftingPlanPipelineTests.cs`: new
     `DailyCooldownItems_SurvivesGenerateStructuredAsync_
     AndResolveWithOverridesRoundTrip` pins the seed dictionary through a
     `GenerateStructuredAsync` -> `ResolveWithOverrides` round trip
     (mirrors the file's own `ResolveWithOverrides_
     CarriesCharacterDisciplinesForward` shape) - closes the previously
     untested 5-site hand-copied wiring in `CraftingPlanPipeline.cs`.
   - `docs/gw2e-considerations.md` Section 11 / `docs/research/
     gw2e-convergence-matrix.md` row 42 marked **Resolved**, matching the
     sibling Section 12 / row 46 resolution PART C already recorded for
     the same PART B #3 fix - both had been left describing the
     `AccountCurrencyIndex.cs` comment fix as still-open.
   - `Services/AccountCurrencyIndex.cs`: the PART B #3 comment correction
     is refined - it previously asserted gw2efficiency nets owned
     currency out "at BOTH the Shopping List/summary display layer AND
     via a per-node pill," but only the per-node pill is measured
     evidence (a live `componentTree.html` fetch); the summary-layer half
     was the original unverified M34-era claim carried forward unchanged.
     Now reads "at least via a per-node display pill," with an explicit
     note that the summary-layer half is unconfirmed. No behavior change.
   - `tests/.../Models/Gw2ConstantsCurrencyNamesTests.cs`: the bare
     `LiveApiNameById[id]` indexer inside the `foreach` is now preceded by
     an `Assert.True(...ContainsKey(id)...)` check with a legible failure
     message, so a future id added to `ExpectedDictName` without a
     matching `LiveApiNameById` entry fails cleanly instead of throwing
     an undiagnostic `KeyNotFoundException`.
   - `Services/DailyCooldownItemService.cs`: `Load` now also skips an
     entry with `ItemId <= 0` (previously only `PerDayCap` was
     validated) - no `PlanStep` ever carries one, matching the existing
     malformed-seed-data guard shape. New test
     `Load_ZeroOrNegativeItemId_EntrySkipped_NoThrow`.
   - Not taken: the split-source under-reporting and parallel-vs-additive
     *aggregation* (as opposed to wording) nice-to-haves remain latent
     only (no seeded item currently has a vendor offer, per
     `ref/vendor_offers.json`) and would need solver-adjacent design
     work, out of scope for a same-file cheap fix.

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-cooldowns/GW2CraftingHelper.csproj -p:Platform=x64` -
0 errors, warning count/content unchanged from baseline (all warnings
are the project's pre-existing StyleCop noise pattern). Tests:
`"/mnt/c/Program Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-cooldowns/tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` -
1431 passed, 0 failed (1429 baseline + 2 new: `Load_
ZeroOrNegativeItemId_EntrySkipped_NoThrow` and `DailyCooldownItems_
SurvivesGenerateStructuredAsync_AndResolveWithOverridesRoundTrip`). No
Blish HUD/BlishHUD.exe reference in any test file; every new/changed
assertion exercises a real production entry point
(`AcquisitionHintService.Load`, `DailyCooldownItemService.Load`,
`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`,
`Gw2ConstantsCurrencyNamesTests`), no contract mirrors, no fake file
I/O. IDs remain internal-only - the new/changed hint text never
surfaces an item id. No live desktop verification was performed (same
Blish-bound surface as PART A/B/C).

Gate: PASS 2026-08-16 (live desktop session). Deldrimor Steel Ingot x5 rendered the timegate notice verbatim ('Lump of Mithrillium is timegated - 1 per day per account - crafting 5 will take about 5 days'); the empty-IconUrl magenta fix verified on the Snapshot tab (Spirit Shards row degrades to no icon); currency-name corrections suite-covered.
