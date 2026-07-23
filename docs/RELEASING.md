# Releasing

This document describes the current, actual state of packaging and release
for GW2 Crafting Helper - not an aspirational process. As of this writing
there is **no GitHub Releases flow**: no tags corresponding to real
releases of this module exist, and there is no CI step that publishes a
`.bhm` anywhere. Everything below reflects what a contributor can do today
with the tools already in the repo.

## How a `.bhm` is actually produced

A `.bhm` file is not produced by any custom step in this repo's own
`GW2CraftingHelper.csproj` - there is no hand-written pack/zip target here.
It comes entirely from the `BlishHUD` NuGet package's own build logic,
imported via:

```
<Import Project="packages\BlishHUD.1.3.0\build\BlishHUD.targets" ... />
```

That imported `BlishHUD.targets` file (installed under
`packages/BlishHUD.1.3.0/build/BlishHUD.targets` once NuGet packages are
restored) runs automatically **on every build**, via an
`AfterTargets="Build"` target named `BuildBlishHUDModule`. It does exactly
this, unconditionally:

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
  for a file with zero runtime consumer in the shipped module. This is a
  real packaging gap, not a csproj/manifest problem - fixing it would mean
  moving those two files out of `ref/` (or out of the repo working tree
  entirely) rather than editing `GW2CraftingHelper.csproj` or
  `manifest.json`. No such move was made as part of this change; it is
  called out here as a finding for the
  maintainer to act on separately.

## `manifest.json` fields

- `name`, `version`, `namespace`, `package` - standard Blish HUD module
  identity fields. `version` is currently `0.1.0` and has never been
  bumped; there is no versioning convention enforced anywhere in the repo
  today.
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
listing for this module. The only way to run it today is:

1. Build in Release, `x64`: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64 -c Release`.
2. Locate the produced `.bhm` (e.g. `bin\x64\Release\GW2CraftingHelper.bhm`).
3. Copy it into your Blish HUD installation's `modules` directory and
   (re)load Blish HUD.

There is no documented, supported path for a non-developer to install this
module without building it from source.

## Addendum: build from a clean checkout until the csproj exclusion lands

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

**Recommendation:** until a real fix lands (see the csproj-exclusion gap
in the list below), build any release `.bhm` from a fresh clone or a
working copy you have confirmed does not contain
`ref/wiki_vendor_cache.json`/`ref/item_id_cache.json`, rather than from an
active `VendorOfferUpdater` development workspace. This is a process
recommendation only - **no `GW2CraftingHelper.csproj` change was made as
part of this addendum**; a real fix (e.g. an MSBuild `Exclude` on the
`BuildBlishHUDModule` target's copy, or moving these two files out of
`ref/` entirely) remains the follow-up item already listed below.

## What a real release process would still need

None of the following exist today; they are listed as concrete gaps, not
committed-to future work:

- A tagged release + CI job that builds Release/x64 and attaches the
  resulting `.bhm` as a GitHub Release asset.
- A convention for bumping `manifest.json`'s `version` per release.
- A decision on the `ref/wiki_vendor_cache.json` / `ref/item_id_cache.json`
  packaging gap described above (as of this M38/WP-28 change, both files
  have been untracked and added to `.gitignore` going forward; the
  `.bhm` packaging behavior itself - the `BuildBlishHUDModule` target
  copying all of `ref/` wholesale - is unchanged and still needs a real
  fix, such as moving these files out of `ref/` entirely, if the gap is to
  be closed for good).
- The two stale `v1.0.0`/`v2.0.0` tags inherited from the original
  `blish-hud/ModuleTemplate` fork (both point at the same 2020 template
  commit, unrelated to this module's actual history) have already been
  removed from the GitHub remote (verified via `gh api
  repos/<owner>/<repo>/tags` returning `[]`). Local clones and worktrees
  predating that deletion may still carry the stale local refs; run
  `git tag -d v1.0.0 v2.0.0` in any such clone to prune them before
  starting a real version tag sequence.
