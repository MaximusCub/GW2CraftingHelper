> **Frozen record - 2026-08-16, branch `opportunity-notes-recipe-sheet-savings-seasonal`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Opportunity notes: recipe-sheet savings + seasonal vendor tips (2026-08-16)

Extended the Plan Notes section (previous entry, immediately above) with
two OPPORTUNITY note kinds, both carrying concrete
numbers, per the design law: structured sections show the
BEST-NOW option; opportunities/considerations go to Plan Notes.

**1. RECIPE-SHEET SAVINGS.** For a bought (not crafted) item whose
reference branch (`CraftingTreeBuilder`'s "what it would cost to craft
instead" hypothetical subtree) is blocked on a missing, `LearnedFromItem`
recipe that a curated map says is purchasable, and where crafting would
actually be cheaper: "Buy the `<output>` recipe (`<sheet cost>`) to craft
it instead - saves `<delta>` per unit", or the training variant when no
character meets the recipe's own discipline/rating.

**Design decision worth flagging explicitly (data-availability gap, not
a scope cut):** the intended join (VendorOfferStore offers whose
output is the sheet item) requires a recipe-id -> unlocking-sheet-item-id
mapping. Neither the real GW2 `/v2/recipes` API nor this repo's existing
data (recipes seed, vendor offers, item metadata) captures that
linkage anywhere - the GW2 API only exposes it from the OTHER direction
(an item's own `details.recipe_id`, on `/v2/items`, for `Unlock`/
`CraftingRecipe` consumables), and building a live index over that
(fetch-and-cross-reference every vendor offer's own item metadata to
find which are recipe sheets) is exactly the "reverse-sheet-index
plumbing" the task explicitly ruled out. Rather than fabricate a
recipe/sheet pairing that could not be verified against a real wiki
source (repo
invariant: never invent data), `RecipeSheetSavingsCalculator.Apply`
takes `recipeSheetItemIdByRecipeId` as an injectable, optional
dictionary - `CraftingPlanPipeline`'s own constructor default is empty.
**Since fixed (review-fix round):** `Module.cs`'s `Initialize()` now
loads a small, wiki-verified `ref/recipe_sheet_items.json` seed via the
new `Services/RecipeSheetItemSeedService.Load` (same try/catch,
Blish-`ContentsManager`-stream loading shape as the neighboring
`acquisition_hints_seed.json`/`daily_cooldown_items.json` reads
immediately above it) and passes the result as
`recipeSheetItemIdByRecipeId:` on the real `CraftingPlanPipeline`
construction - **this note now fires in production** whenever a plan's
reference branch matches a seeded recipe. Every other piece of the
feature (missing+LearnedFromItem detection, craft-vs-chosen-cost delta
math, "not comparable" skip rules, discipline-training-blocked
detection, sheet-price lookup via the ordinary
`VendorOfferStore.GetOffersForItem`, and the two-row Notes rendering) is
fully implemented and covered by real, injected fixture data in
`RecipeSheetSavingsCalculatorTests`/
`PlanViewModelBuilderNotesRecipeSheetSavingsTests`, plus the now-real
`ref/recipe_sheet_items.json` seed wiring exercised via
`RecipeSheetItemSeedService`.

**2. SEASONAL VENDOR TIP.** Blish's `FestivalContext` is read via
`Module.cs`'s `ReadActiveFestivalNames()` and projected to plain
`Festival.Name` strings (e.g. `"halloween"`) before crossing into the
Blish-free `Services`/`Models` layers. **Since fixed (review-fix round
#3):** the read is no longer a one-shot `Initialize()`-time call - it is
now a `Func<IReadOnlyList<string>>` (`CraftingPlanPipeline`'s
`activeFestivalNames` constructor parameter) that `Module.cs` passes as
`ReadActiveFestivalNames`, invoked LAZILY at plan-generation time
instead. A one-shot `Initialize()`-time read could observe `NotReady`
(the context loads asynchronously) and silently disable the feature for
the whole session; the lazy read re-checks on every plan instead. Every
failure state (context not registered, `NotReady`/`Unavailable`/`Failed`,
or any exception) still collapses to an empty list, now logged at Info
(an expected, common, benign state) so "seasonal tips disabled by
<availability>" is distinguishable in the module log from "no festival
active" (Available with an empty list, which logs nothing). Only the
exception path still logs at Warn. **MEASURED, not guessed:** `Festival.Name` and
`Festival.DisplayName` were read via `System.Reflection` directly against
the shipped `packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe` (no live
game client needed - `Festival` instances are plain static fields) -
`Name` is lowercase (`"halloween"`), NOT the capitalized `DisplayName`
(`"Halloween"`) an unverified guess would likely have used, which would
have silently broken every string match. `Gw2Constants.
FestivalDisplayNames` is a small curated Name->DisplayName table (same
measurement), not a capitalizer, since Blish's own DisplayName is not a
simple capitalization of Name for every festival (`"superadventurefestival"`
-> `"Super Adventure Festival"`).

`VendorOffer.SeasonalFestival` seeds exactly the three real
Candy Corn Vendor (Weekly) ecto (Glob of Ectoplasm, item 19721) offers
already present in `ref/vendor_offers.json`'s wiki-scraped baseline -
every other offer in that 53,536-row file is untouched. By
explicit decision, seasonal offers are excluded from the
solver's own candidate set UNCONDITIONALLY (`SeasonalOfferFilter.
ExcludeSeasonal`, applied only at the actual `_solver.Solve`/
`OwnedMaterialsForceBuyPrePass` call sites in `CraftingPlanPipeline` -
every other consumer of the vendor-offers dictionary, e.g. metadata
widening, keeps seeing the raw/unfiltered data unchanged) - a plan
always assumes the regular market, active festival or not.
`SeasonalVendorTipCalculator` is the separate, purely-informational pass
that surfaces an active, cheaper festival offer as a Notes row; its cost
description is built ONLY from Item-type cost lines (the only kind the
three seeded offers have) - a coin cost line would have no safe way to
render inline as text without violating the "coin icons right of the
number" invariant with the row's one `CoinValue` slot already spent on
the plan's own price, so that case is skipped entirely rather than
rendered incorrectly (currently unreachable with the seeded data, but a
real restriction, not a hypothetical one).

The wiki-scrape-updater-side automation to detect/tag FUTURE seasonal
offers from the wiki's Temporary template (so new festival vendors don't
need a hand edit like this one) is a recorded follow-up, not this pass.

**What changed:** `Models/CraftingTreeNode.cs` (`ReferenceRecipeId`/
`Disciplines`/`MinRating`/`IsLearnedFromItem`, reference-branch-only),
`Models/RecipeSheetSavingsOpportunity.cs`, `Models/SeasonalVendorTip.cs`,
`Models/CraftingPlanResult.cs` (+2 fields), `Models/VendorOffer.cs`
(+`SeasonalFestival`), `Models/Gw2Constants.cs` (+`HalloweenFestivalName`,
`FestivalDisplayNames`), `Services/CraftingTreeBuilder.cs`
(`ApplyReferenceRecipeInfo`), `Services/CostLineValuation.cs` (new,
shared coin-valuation helper - never touches `VendorBatchSolver`, one of
the frozen files), `Services/SeasonalOfferFilter.cs` (new),
`Services/RecipeSheetSavingsCalculator.cs` (new),
`Services/SeasonalVendorTipCalculator.cs` (new),
`Services/CraftingPlanPipeline.cs` (two new optional constructor
parameters, both default-empty; wired at all three result-building call
sites), `Services/PlanViewModelBuilder.cs` (`BuildNotesSection` gains the
two new note kinds), `Module.cs` (`FestivalContext` read, now lazy;
loads and wires the `ref/recipe_sheet_items.json` seed),
`ref/vendor_offers.json` (3 rows tagged),
`ref/recipe_sheet_items.json` (new, curated recipe-id ->
unlocking-sheet-item-id seed), `Services/RecipeSheetItemSeedService.cs`
(new, loads the seed file), `tools/VendorOfferUpdater/Models/VendorOffer.cs`
(seasonal-festival tagging support for the updater side),
`tests/VendorOfferUpdater.Tests/SeasonalFestivalRoundTripTests.cs` (new),
`.github/workflows/tests.yml` (updated to run the updater/seeder test
projects alongside the main suite).

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests (both new calculators and
  `SeasonalOfferFilter`/`CostLineValuation` are plain `internal static`
  classes over `Models` types only; `Module.cs`'s own `FestivalContext`
  read is the ONLY place in this whole package that touches Blish, and
  it is the one file this repo does not unit-test).
- [x] Tests exercise real production paths (real `VendorOfferStore`
  backed by a temp-directory baseline in
  `RecipeSheetSavingsCalculatorTests`, not a fake/mirrored store -
  matches `VendorOfferStoreTests`' own precedent).
- [x] No fake file I/O tests introduced.
- [x] Pricing logic preserves multi-source correctness (`CostLineValuation`
  refuses - never guesses - on a non-coin currency line, an unpriced Item
  line, or any unrecognized `CostLine.Type`, mirroring `VendorBatchSolver.
  EvaluateVendorOffers`' own posture without touching that frozen
  file).
- [x] IDs remain internal-only (every note resolves item/recipe/discipline
  **names**, never raw ids).

**Validation performed:** `dotnet build GW2CraftingHelper.csproj
-p:Platform=x64` - clean, 0 errors (only pre-existing StyleCop warnings,
none in new/edited files). `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` (measured, at this pass's own commit):
1554 (baseline) -> 1601 (+47): `CostLineValuationTests` (7),
`SeasonalOfferFilterTests` (5), `RecipeSheetSavingsCalculatorTests` (12),
`SeasonalVendorTipCalculatorTests` (10 - includes a review-fix-round
addition, see below), `PlanViewModelBuilderNotesRecipeSheetSavingsTests`
(4), `PlanViewModelBuilderNotesSeasonalVendorTipTests` (5), plus 3 new
cases appended to the existing `CraftingTreeBuilderTests`, plus one
existing test file gained two new constructor parameters
(`CraftingPlanResultBuilders`, not itself a test). All 1601 green at
that point.

**Updated (later review-fix rounds, measured 2026-08-16):** further
review-fix rounds (activating the recipe-sheet seed, seasonal tag
round-trip/lazy festival read/tip wrap/craft-cost math fixes, the
recursive vendor-currency guard, and the updater CI gap) added more
tests and a new CI-wired test project. Current totals: `dotnet test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` - 1616
green. `dotnet test tests/VendorOfferUpdater.Tests/
VendorOfferUpdater.Tests.csproj` - 136 green (includes the new
`SeasonalFestivalRoundTripTests`). `dotnet test
tests/GW2CraftingHelper.RecipeSeeder.Tests/
GW2CraftingHelper.RecipeSeeder.Tests.csproj -p:Platform=x64` - 3 green.
All three suites green as of this pass.

**Review-fix round (self-review, before handoff):**
`SeasonalVendorTipCalculator` was picking the FIRST qualifying seasonal
offer per item rather than the cheapest - the three real seeded ecto
offers are exactly this case (three Halloween candy colors, each its
own TP price, all trading for the same 5x ecto), so a plan could have
surfaced a real but non-optimal deal. Fixed to scan every qualifying
offer and keep the cheapest, mirroring `RecipeSheetSavingsCalculator`'s
own identical "cheapest priceable offer wins" precedent - new test
`MultipleQualifyingOffers_PicksCheapest`.

**Risks / follow-ups:**
- `recipeSheetItemIdByRecipeId` now ships from `ref/recipe_sheet_items.json`
  (see the RECIPE-SHEET SAVINGS section above) - the seed is small and
  curated by hand; growing its coverage (more wiki-verified recipe/sheet
  pairs) is the natural next step to widen when the feature can fire,
  not to activate it for the first time.
- Seasonal-offer detection is a one-time hand tag of three known rows,
  not an automated wiki-scrape pass - see the SEASONAL VENDOR TIP section
  above.
- No live sandbox verification was performed - `Views/CraftingPlanView.cs`
  and `Views/Rendering/NotesSectionRenderer.cs` are Blish-bound and
  outside this repo's test-runnable surface, same constraint every
  UI-adjacent entry in this file notes (including the immediately
  preceding Plan Notes entry, which this one extends without touching
  `NotesSectionRenderer.cs`/`PlanContentHeightMath.cs`/
  `PlanRelayoutMath.cs`/scroll machinery at all - every new row is a
  plain `NoteLine`, already covered by that renderer's existing 28px
  contract). The two new note kinds' real on-screen wording/wrapping have
  not been visually confirmed in a running Blish HUD client - the
  RECIPE-SHEET SAVINGS row shape has been verified via injected test
  fixtures and is now wired to a real, non-empty
  `recipeSheetItemIdByRecipeId` seed in production (see above), but has
  not yet been confirmed against a real generated plan on-screen.

Gate: PASS (negative checks) 2026-08-16 (live sandbox session). Seasonal exclusion verified as the headline: the ARE craft path now prices ectos at the real TP rate (~26s vs the old ~4s26 phantom Halloween vendor), the ecto row's vendor source is gone entirely, and the Candy Corn tip correctly does NOT render out of season; sheet-savings positive render suite-covered.
Gate: not yet run live - queued for the next batched sandbox session. Merged after the full review pipeline resolved every finding (2 adversarial rounds, verification zero-blocking, 1536/1536 pre-merge), under the standing merge directive (2026-08-16).
