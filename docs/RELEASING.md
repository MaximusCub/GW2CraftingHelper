# Releasing

This document describes the current, actual state of packaging and release
for GW2 Crafting Helper - not an aspirational process.

**What the project actually practices today** (the v0.2.x field-test era):
a release is a CHANGELOG entry, a `manifest.json` version bump, and a
matching `v<version>` git tag on the release commit, deployed by copying
the built `.bhm` into a live Blish HUD install. `CHANGELOG.md` states the
convention in its own header, and `v0.2.0` through `v0.2.3` exist and are
pushed to origin (measured 2026-08-24: `git ls-remote --tags origin`).

**What still does not exist:** a GitHub Releases flow. `gh release list`
returns nothing (measured 2026-08-24), `.github/workflows/tests.yml` builds
and tests but publishes no artifact, and there is no Blish HUD module-repo
listing. So a non-developer still cannot install this module.

Everything below reflects what a contributor can do today with the tools
already in the repo, and is measured against the current build unless
labelled otherwise.

## The release protocol, step by step

1. Land the work on `master`.
2. Re-run the recipe seeder from the repo root and commit the refreshed
   `ref/` seeds:

   ```
   dotnet run --project tools/GW2CraftingHelper.RecipeSeeder/GW2CraftingHelper.RecipeSeeder.csproj -- --output-dir ref --force
   ```

   Roughly 1-2 minutes against the live API (measured 2026-08-25: 1m53s
   cold, 59s on an immediate re-run). `--output-dir ref` is not optional:
   the tool's own default writes into its `bin/` folder, not the repo's.
   Why it is a release step: the seed pins the GW2 build id it was built
   against, and once that id no longer matches the live build every
   negative row in the seed stops counting as a cache hit, putting every
   user on the slow live-API path for their first plan of each session.
3. Bump `manifest.json`'s `version` (the About tab reads it live).
4. Add the matching `CHANGELOG.md` entry - `## <version> - <date>`, in the
   user-facing voice the existing entries use, not commit-message voice.
5. Clear `bin/` and `obj/`, then build Release/x64 (see the clean-build
   rule in the addendum - it is not optional).
6. Tag the release commit `v<version>` and push the tag.
7. Copy `bin/x64/Release/GW2CraftingHelper.bhm` into the live Blish HUD
   install's `modules` directory and reload Blish HUD.

Because every deployed build has a tag, any two shipped builds can be
compared with `git diff v0.2.0..v0.2.1`.

## How a `.bhm` is actually produced

A `.bhm` file comes from the `BlishHUD` NuGet package's own build logic,
imported via:

```
<Import Project="packages\BlishHUD.1.3.0\build\BlishHUD.targets" ... />
```

That imported `BlishHUD.targets` file (installed under
`packages/BlishHUD.1.3.0/build/BlishHUD.targets` once NuGet packages are
restored) defines an `AfterTargets="Build"` target named
`BuildBlishHUDModule` that runs automatically **on every build**. As of
M38/WP-29, `GW2CraftingHelper.csproj` redeclares that same-named target
after the import (the one and only hand-written pack/zip logic in this
repo's own csproj) so its own version wins, purely to add a three-file
`Exclude` - see the addendum below. Otherwise it does exactly this,
unconditionally:

1. Copies `manifest.json` into the build output directory (`$(OutDir)`).
2. Copies the **entire** `ref/` folder (recursively, everything under it)
   into `$(OutDir)ref/`.
3. Copies the whole output directory into a temp subfolder and zips it into
   `<OutDir>\<ProjectName>.bhm` (e.g.
   `bin\x64\Release\GW2CraftingHelper.bhm`), overwriting any previous
   `.bhm`.

This was verified directly (not inferred) by running:

```
dotnet build GW2CraftingHelper.csproj -p:Platform=x64 -c Release
```

which produced `bin/x64/Release/GW2CraftingHelper.bhm` (a zip), and then
listing its contents. There is currently **no other step required** to
produce a `.bhm` - building the module in Release is sufficient.

### Measured finding: the packed `.bhm` includes files that never ship via `<Content Include>`

The `BuildBlishHUDModule` target copies `ref/**` wholesale, independent of
which `ref/*.json` files are wired into `GW2CraftingHelper.csproj` via
`<Content Include>`/`CopyToOutputDirectory`. Inspecting the actual `.bhm`
produced by the build above shows it contains, under `ref/`:

- `acquisition_hints_seed.json`, `emblem.png`, `icon.png`,
  `item_name_seed.json`, `mystic_forge_recipes.json`, `recipes_seed.json`,
  `recipe_search_seed.json`, `recipe_seed_manifest.json`,
  `vendor_offers.json` - these are the files also wired into the csproj as
  `<Content Include>`, so their presence is expected.
- **`item_id_cache.json` and `wiki_vendor_cache.json`** - these are *not*
  wired into the csproj as `<Content Include>` items (they exist purely as
  developer-side inputs to `tools/VendorOfferUpdater`), but because the
  BlishHUD packer copies the entire `ref/` directory regardless of csproj
  content wiring, they end up inside the shipped `.bhm` anyway. Measured
  directly from the zip's entry metadata: `wiki_vendor_cache.json` is
  ~19.6 MB uncompressed (~1.1 MB as stored in the zip, since it compresses
  well) and `item_id_cache.json` is ~38 KB. So the *download size* impact
  of the uncontrolled `wiki_vendor_cache.json` inclusion is smaller than
  its raw file size suggests (~1.1 MB, not ~19.6 MB, of actual `.bhm`
  bytes) - but it is still bytes a player downloads and Blish HUD extracts
  for a file with zero runtime consumer in the shipped module. This was a
  real packaging gap at the time this finding was recorded. **Update
  (M38/WP-29): fixed.** `GW2CraftingHelper.csproj` now overrides the
  imported `BuildBlishHUDModule` target with an `Exclude` on its `ref/**`
  copy for `ref/wiki_vendor_cache.json` and `ref/item_id_cache.json`, so
  neither file is ever copied into the output directory or the `.bhm`,
  regardless of whether they exist in the working copy. See the addendum
  below for details. The list is **three** files today: `MysticForgeSeeder`
  later added `ref/mf_item_id_cache.json` to the same `Exclude`
  (`GW2CraftingHelper.csproj:449`, measured).

## `manifest.json` fields

- `name`, `version`, `namespace`, `package` - standard Blish HUD module
  identity fields. `version` is bumped per release under the CHANGELOG +
  tag convention above (`0.2.3` at the time of writing). Nothing *enforces*
  the bump mechanically - no CI check, no analyzer - so it is a step in the
  protocol, not a guarantee.
- `dependencies.bh.blishhud` - minimum Blish HUD host version
  (`>=1.3.0`).
- `url`, `contributors` - metadata shown in Blish HUD's module browser and
  in this module's own "About" tab (`Views/AboutTabContent.cs` reads these
  live from the manifest via reflection).
- `api_permissions` - the GW2 API scopes this module requests, each with an
  `optional` flag and a human-readable `details` string.
- `directories: ["data"]` - **measured to be load-bearing, not vestigial.**
  This is unrelated to the `ref/` content directory described above. It
  declares that Blish HUD should provision a writable, per-module "data"
  directory, which the module resolves at startup via
  `DirectoriesManager.GetFullDirectoryPath("data")` (`Module.cs`,
  `Initialize()`). That path is then used to construct the module's real
  runtime storage: `ModuleLogStore`, `SnapshotStore`, `StatusStore`,
  `VendorOfferStore`, and `OverlayRecipeCacheStore` all write into it, and
  it is displayed to the user in the About tab's "Data directory" row. The
  earlier open question (from the public-repo readiness pass) of whether
  this key was a stale leftover from the original `ModuleTemplate` fork is
  resolved: it is not stale, and it should not be removed. No manifest
  change was made as a result of this verification.

## Installing a built module today

There is no GitHub Release and no in-app Blish HUD module-repository
listing for this module (measured 2026-08-24). The only way to run it is:

1. Clear `bin/` and `obj/`, then build in Release, `x64`:
   `dotnet build GW2CraftingHelper.csproj -p:Platform=x64 -c Release`.
2. Locate the produced `.bhm` (e.g. `bin\x64\Release\GW2CraftingHelper.bhm`).
3. Copy it into your Blish HUD installation's `modules` directory and
   (re)load Blish HUD.

This is also exactly what a release deploy does - the tag and CHANGELOG
entry are what make a given copy of those three steps identifiable after
the fact. There is still no documented, supported path for a
non-developer to install this module without building it from source;
`README.md`'s "Installing" section says the same thing and should stay
consistent with this one.

## Addendum: the ref/ cache-file packaging gap (fixed, M38/WP-29)

The "Measured finding" above establishes that the `BuildBlishHUDModule`
target copies all of `ref/` into the `.bhm` regardless of `<Content
Include>` wiring, and that `ref/wiki_vendor_cache.json`/
`ref/item_id_cache.json` were untracked and gitignored (PR #92) precisely
because of this. Untracking them only closes the gap for a **clean
checkout** - a `git clone` never has them, so its `.bhm` never contains
them. It does **not** retroactively remove them from a developer's
existing working copy: anyone who ran `tools/VendorOfferUpdater` before
PR #92 (or has run it since, since the tool still writes those files
locally as its own working cache) still has both files sitting in `ref/`
on disk, gitignored or not, and a build from that same working copy still
picks them up via the wholesale `ref/` copy and ships them - gitignore
only controls what `git` tracks, not what MSBuild's `CopyToOutputDirectory`
-equivalent packaging step reads off disk.

This was directly measured on the M38 WP-25 branch (recorded in
`docs/dev-notes/HISTORY.md` under the WP-25 entry, 2026-07-23): a worktree
created **after** PR #92 (so never had the caches materialize at all)
produced a `.bhm` of 6.0 MB, versus 7.2 MB from an equivalent build on a
working copy that still had them - and the smaller, cache-free build
loaded and ran the module's full interaction surface (recipe tree render,
decision-pill overrides, Ignore toggle, presets) with no missing-file
errors or behavior difference, confirming these two files have no runtime
consumer in the shipped module at all.

**Fixed (M38/WP-29):** the csproj exclusion described as a follow-up item
above has landed. `GW2CraftingHelper.csproj` now redeclares the imported
`BuildBlishHUDModule` target (same name, declared after the `BlishHUD.targets`
import, so it wins) with an `Exclude` added to the `ref/**` copy for
`ref/wiki_vendor_cache.json` and `ref/item_id_cache.json` - and, since
`MysticForgeSeeder` landed, `ref/mf_item_id_cache.json`, making it three
files. None of them is
copied into `$(OutDir)ref` or zipped into the `.bhm` any more, regardless of
whether a developer's working copy has them sitting on disk from running
`tools/VendorOfferUpdater`. Building from an active `VendorOfferUpdater`
development workspace is no longer a concern for this specific gap; a
clean checkout is no longer required to get a cache-free `.bhm`. However,
because the pack target's staging-dir copy zips whatever is already sitting
in `$(OutDir)` rather than only the files this target itself just copied,
a release `.bhm` must still be built after clearing `bin/` and `obj/` -
otherwise a stale, pre-exclusion copy of a cache file left over from an
earlier incremental build can be re-zipped into the output regardless of
the `Exclude` list above.

## What a real release process would still need

These are listed as concrete gaps, not committed-to future work:

- A CI job that builds Release/x64 on a tag and attaches the resulting
  `.bhm` as a GitHub Release asset. The tags exist; nothing consumes them.
  This is the one gap that actually blocks a non-developer install.
- ~~A convention for bumping `manifest.json`'s `version` per release~~ -
  done: the CHANGELOG + `v<version>` tag protocol at the top of this file,
  practiced across v0.2.0 through v0.2.3.
- ~~A decision on the `ref/wiki_vendor_cache.json` / `ref/item_id_cache.json`
  packaging gap~~ - resolved as of M38/WP-29; see the addendum above.
- The two stale `v1.0.0`/`v2.0.0` tags inherited from the original
  `blish-hud/ModuleTemplate` fork (both point at the same 2020 template
  commit, unrelated to this module's actual history) have already been
  removed from the GitHub remote (verified via `gh api
  repos/<owner>/<repo>/tags` returning `[]`). Local clones and worktrees
  predating that deletion may still carry the stale local refs; run
  `git tag -d v1.0.0 v2.0.0` in any such clone to prune them before
  starting a real version tag sequence.
