## Wave-3 quick wins (2026-08-06)

Four user-directed changes from the same 2026-08-06 field-testing session,
implemented in the isolated `wt-wave3a` worktree off master (4ac5461,
includes PR #102) on branch `wave3a-quick-wins`. One commit per item.

**1. Use Own Materials defaults to checked.** The Crafting Plan strip's
"Use Own Materials" checkbox (`CraftingPlanView._useOwnMaterials`) now
starts `true` for a fresh session, per explicit maintainer direction - a
deliberate divergence from gw2efficiency's own unchecked default. The
field is purely in-memory session state, never read from or written to
`ModuleSettings`, so only the fresh-session starting point changes; there
is no persisted user choice to override.

**2. Mystic Forge recipes excluded from Required Recipes.** A Mystic
Forge combination has nothing to "learn" - it just exists - so listing it
in the Required Recipes section read as an unlock task that does not
exist. `PlanViewModelBuilder.BuildRecipesSection` now skips any recipe
whose ENTIRE `Disciplines` list is `MysticForge`; a recipe combining the
forge with a real leveled discipline still has something to learn and
stays. Builds on PR #102's `NonCraftingDisciplines`/sublabel work - only
this section's own row list is filtered, not the raw `RequiredRecipes`
list or the Crafting Steps section's per-step "Mystic Forge" sublabel
lookup (both read from the same unfiltered list and are unaffected). The
header count reflects the post-filter total, and the whole section is
omitted (not left present with a "(0)" header) when nothing survives the
filter.

**3. "Hide Unlocked Recipes" checkbox, default checked.** Added to the
Required Recipes section header: hides Learned/Auto-learned rows so only
Missing! recipes show by default; rows with unknown status (recipe
permission unavailable) stay visible rather than being silently treated
as "nothing to do here." The header always states the real total,
switching to "(showing K missing of N)" while the filter is active; when
every recipe is unlocked and the filter hides them all, a single friendly
line ("All N recipes already unlocked.") replaces the empty section body.
The filter predicate and header-text formatting live in a new Blish-free
`Services/RequiredRecipesVisibility` class; toggling the checkbox
re-renders through the existing `RenderPlan(_currentPlan)` rebuild path
(mirroring `TreeSectionController`'s own pill-click/preset re-render), not
a new parallel relayout mechanism. State is session-only (not persisted in
`ModuleSettings` - no per-plan-view boolean setting precedent exists there
today), matching every other sticky toggle already on `CraftingPlanView`.

**4. Log tab "Clear view" now survives tab rebuilds.** The "Clear view"
floor (a ring version watermark) used to live on `LogTabContent` itself,
which Blish reconstructs fresh every time the Log tab is selected (the
tab's own view-factory in `Module.cs` calls `new LogTabContent(...)` on
every build) - so a cleared view resurrected the moment a user switched
tabs and back. The watermark now lives on `Module` itself
(`_logViewClearedBeforeVersion`), injected into `LogTabContent` via a
constructor getter/setter delegate pair (mirroring
`TreeSectionController`'s own pattern for view state that outlives a
single render), so it persists for the whole module session instead.
`ModuleLog`'s own locking design is untouched; the watermark stays a
plain, main-thread-only `long` per the PR #101 threading rules - written
only from the Clear-view button's `Click` handler, read only from
`LogTabContent`'s already main-thread-only rebuild paths
(`GetFilteredEntries`/`AppendNewRows`, both gated by the existing
`_buildComplete` discipline). The floor comparison itself moved into a new
Blish-free `Services/LogViewFloor.IsVisible`. Also added
`BasicTooltipText` to the Clear view button: "Hide current entries from
this view. New entries still appear; the log file keeps everything."

Validation: `dotnet build -p:Platform=x64` clean (0 errors); both test
suites green - module suite 1140 passed (was 1115; +25 new tests: 17 in
`RequiredRecipesVisibilityTests`, 5 in `LogViewFloorTests`, 3 added to
`PlanViewModelBuilderStepSectionsTests` for the Mystic-Forge-exclusion
behavior), `VendorOfferUpdater.Tests` 135 passed (untouched, unaffected).
No new Blish HUD references in tests; every new test exercises real
production code (`PlanViewModelBuilder.Build`, `RequiredRecipesVisibility`,
`LogViewFloor`) with no contract-mirror/fake-logic tests. Pure-Blish view
code (`CraftingPlanView`'s checkbox wiring, `LogTabContent`'s
constructor-injected delegates, `Module`'s tab-factory wiring) has no new
tests per the Blish-free-tests invariant.

Live desktop gate: PASS (orchestrator, 2026-08-06, live branch-build
sandbox session, captures w3a_01-06 in preflight/captures):
- Use Own Materials starts CHECKED with zero interaction (fresh module
  session, capture w3a_01).
- Required Recipes: header reads "(showing 14 missing of 34)" with Hide
  Unlocked Recipes CHECKED by default; unfiltering restores all 34 with
  Auto-learned tags; the pre-wave total for the same plan was 47 - the
  13 Mystic Forge combos are gone from the section entirely in both
  filter states; unverifiable-status rows correctly stay visible under
  the filter (sandbox has no API key, so unlock status is unknowable).
- Clear view: after clearing, tab-away to Snapshot and back shows the
  view still cleared ("No entries match the current filter." empty
  state); a subsequent refresh-failure burst appears ABOVE the floor
  while cleared entries stay hidden - the floor survives tab rebuilds
  and does not block new entries.
- Zero FATAL lines in the session log.
