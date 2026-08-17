# Contributing to GW2 Crafting Helper

Thanks for your interest in contributing. This document covers what you
actually need to build, test, and submit a change to this repository. It is
intentionally short; the deeper "why" behind some of the module's less
obvious code lives in `docs/` (see `docs/KNOWN-ISSUES.md` and
`docs/gw2e-parity-spec.md`).

## Prerequisites

- .NET SDK 8.0 (the main module targets .NET Framework 4.8 / MonoGame via
  Blish HUD, but the SDK used to drive `dotnet build`/`dotnet test` is 8.0 -
  see `.github/workflows/tests.yml`)
- On Windows, if `dotnet` is not already on your `PATH`, the SDK installer
  normally places it at `C:\Program Files\dotnet\dotnet.exe`.

## Building

```
dotnet build GW2CraftingHelper.csproj -p:Platform=x64
```

The `Platform=x64` argument is required - the project only defines output
paths for the `x64` platform (see `GW2CraftingHelper.csproj`'s
`Debug|x64`/`Release|x64` property groups). Building without it fails
outright with "The BaseOutputPath/OutputPath property is not set", not a
silently-working-but-wrong build.

A successful build also produces a `.bhm` file next to the built DLL (e.g.
`bin\x64\Release\GW2CraftingHelper.bhm`) - see `docs/RELEASING.md` for how
that packaging step works and what it currently does and does not cover.

## Testing

```
dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj
```

At the time of writing this suite is 1,101 tests, all green. A few rules
the test suite enforces and that any new test must follow:

- **Tests exercise real production code paths.** No contract-mirror tests,
  no fake logic tests, no fake file I/O tests. Storage-backed behavior (e.g.
  `SnapshotStore`, `StatusStore`) is tested against the real store classes
  pointed at a temporary directory, not a hand-rolled substitute.
- **Tests are Blish-free.** Nothing under `tests/` may reference Blish HUD,
  `BlishHUD.exe`, `Gw2Sharp`, or any UI code. The module's business logic
  (`Services/`, `Models/`, `Contracts/`) is deliberately factored so it can
  be exercised without a running Blish HUD host; keep it that way when
  adding new logic.

CI (`.github/workflows/tests.yml`) restores, builds the main module with
`-p:Platform=x64`, and runs this same test project on every push and pull
request.

## Project/Solution Structure

- `GW2CraftingHelper.csproj` is a **non-SDK-style** (classic) project and
  lists every source file explicitly via `<Compile Include="...">`. If you
  add a new `.cs` file, you must add a matching `<Compile Include>` entry
  yourself - the build will simply not see a file that isn't listed. This
  is unusual compared to modern SDK-style projects (which glob `**/*.cs`
  automatically); it is a consequence of Blish HUD's module template, not
  a stylistic choice, so please don't try to "fix" it as part of an
  unrelated change.
- `tools/` contains standalone offline utilities (see below) that are
  separate console projects, most of them SDK-style, each with its own
  README where one exists.
- `GW2CraftingHelper.sln` lists every project, including the tools - if you
  add a new tool project, add it to the `.sln` too (a project that only
  exists on disk but isn't in the `.sln` is easy to miss entirely, which
  happened to `MysticForgeSeeder` before this was fixed).

## Code Style

- Allman brace style (opening brace on its own line) for C#.
- **ASCII-only in `.cs` source.** Do not paste raw Unicode into code,
  comments, or string literals. If Unicode must be shown at runtime (UI
  glyphs, item names, etc.), use an escape (e.g. `"\u25BC"`) or data
  returned by the GW2 API. CI runs an advisory (non-blocking) ASCII check
  over `.cs` files on every push.
- No em-dashes in source, comments, or config - use a plain hyphen (`-`) or
  double-hyphen (`--`) instead.
- Follow the patterns already established in neighboring files rather than
  introducing new structure for a small change.
- Keep edits focused; avoid unrelated refactors or formatting churn in the
  same change as a behavior fix.
- **STANDING RULE (Recipe Tree row/pill features):** every new tree-row or
  decision-pill feature extracts its pure text/decision computation into a
  tested, Blish-free `Services/` composer BEFORE wiring it into
  `Views/Rendering/TreeSectionController.cs` - the 8-for-8 proven pattern
  (`DecisionPillPlanner`, `ValueDetailTooltipBuilder`,
  `PillSubduingEvaluator`/`PillSubduingTooltipBuilder`,
  `ReceiptCaptionHelper`, `CurrencyDisplayResolver`,
  `TreeRowTooltipComposer`, ...). Only the actual Blish-bound
  Panel/Label/event-wiring belongs in `TreeSectionController` itself. This
  is not a suggestion to eventually split `TreeSectionController` into a
  stateful/stateless pair - that split was evaluated and rejected by
  decision (see `docs/ARCHITECTURE.md` section 5); the class stays a
  single owner with a single lifetime, and every new feature's logic grows
  the `Services/` side of that boundary, never a second stateful class.

## Where seed/reference data comes from

The module ships several JSON files under `ref/` (recipes, item names,
vendor offers, Mystic Forge recipes) that it reads at runtime. **None of
these are fetched by the module itself at runtime** - `gw2efficiency` is
research-only (see `docs/gw2e-parity-spec.md`/`docs/research/`) and is
never called from module code, and the GW2 Wiki is never queried by the
running module either. All of it is produced ahead of time by the offline
tools under `tools/` and committed to the repo:

- `tools/GW2CraftingHelper.RecipeSeeder` - queries the official GW2 API
  (`api.guildwars2.com`) to build `ref/recipes_seed.json`,
  `ref/recipe_search_seed.json`, and related files.
- `tools/VendorOfferUpdater` - scrapes the GW2 Wiki (Semantic MediaWiki API)
  plus the official GW2 API to build `ref/vendor_offers.json`. See
  `tools/VendorOfferUpdater/README.md` for the full two-pass workflow.
- `tools/MysticForgeSeeder` - scrapes the GW2 Wiki to build
  `ref/mystic_forge_recipes.json`.

If you're changing anything that touches pricing, recipes, or vendor data,
check whether the relevant tool under `tools/` needs to be re-run rather
than hand-editing a `ref/*.json` file.

## Pull Requests

- A pull request template exists at `.github/PULL_REQUEST_TEMPLATE.md` -
  please fill it in rather than deleting the sections.
- Describe what changed and why, and how you validated it (build/test
  commands run, and any manual verification for UI-affecting changes).
- Keep commits logically grouped (e.g. a refactor, a behavior change, and
  its tests as separate commits) rather than one large mixed commit, where
  practical.
- If your change touches pricing, currency comparisons, or item/vendor IDs
  shown anywhere in the UI, note that this repo has a couple of
  non-negotiable invariants: IDs are internal-only and must never be
  displayed to users, and pricing logic must preserve multiple sources and
  avoid comparing incompatible currencies. A reviewer will flag a PR that
  violates either.

## Reporting Bugs

Please use the bug report template under `.github/ISSUE_TEMPLATE/` when
opening an issue. If you can attach a log tail, the module's in-app "Log"
tab has a "Copy" button that copies recent log lines to the clipboard -
that's the quickest way to get diagnostic output into a bug report.
