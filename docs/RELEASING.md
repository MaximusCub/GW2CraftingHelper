# Releasing

This document describes the current, actual state of packaging and release
for GW2 Crafting Helper - not an aspirational process.

**What the project actually practices today** (the v0.2.x field-test era):
a release is a CHANGELOG entry, a `manifest.json` version bump, and a
matching `v<version>` git tag on the release commit, deployed by copying
the built `.bhm` into a live Blish HUD install. `CHANGELOG.md` states the
convention in its own header, and `v0.2.0` through `v0.2.4` exist and are
pushed to origin (measured 2026-08-25: `git ls-remote --tags origin`).

**What changed since:** `.github/workflows/release.yml` now builds
Release/x64 on any pushed `v*` tag and publishes
`bin/x64/Release/GW2CraftingHelper.bhm` as a GitHub Release asset, with the
matching `CHANGELOG.md` section as the body. It refuses to publish if the
tag does not match `manifest.json`'s version, or if `CHANGELOG.md` has no
section for it. Pushing a tag is therefore the whole release action.

**What still does not exist:** a Blish HUD module-repository listing, so
the download is still manual. And `gh release list` returned nothing as of
2026-08-25 - the workflow has not yet been exercised by a pushed tag, so
the first release will be the one that proves it end to end.

Everything below reflects what a contributor can do today with the tools
already in the repo, and is measured against the current build unless
labelled otherwise.

## The release protocol, step by step

1. Land the work on `master`.
2. Bump `manifest.json`'s `version` (the About tab reads it live). Check
   that `manifest.json`'s `description` still matches the GitHub repo
   description - it is the sidebar text, the search-result snippet, and the
   Open Graph card used every time the link is pasted into Discord or
   Reddit, and it is the only sentence most people will ever read.
3. Add the matching `CHANGELOG.md` entry - `## <version> - <date>`, in the
   user-facing voice the existing entries use, not commit-message voice.
   The release workflow uses this section verbatim as the release body and
   fails the build if it is missing.
4. **Sweep the prose that names a version.** `manifest.json` is not the
   only place a version number is written down, and the others drift
   silently because nothing reads them: this file (the v0.2.x paragraph at
   the top, the `manifest.json` fields section, the "what a real release
   process would still need" list) and `docs/ROADMAP.md`'s current-phase
   bullet all state which release is newest. `grep -rn "0\.2\." docs/
   *.md` finds them; update each to the version being shipped, and stamp
   any claim you re-checked with `(measured YYYY-MM-DD)` rather than "at
   the time of writing". This step exists because ROADMAP.md and
   RELEASING.md both still said v0.2.3 was newest after v0.2.4 shipped.
5. **If the plan view changed, refresh `docs/images/`.** The README's
   screenshots are the only proof a visitor has that the product works.
   They went stale once already: the shots taken 2026-07-23 showed columns
   packed hard left with a wide empty band, which is the exact layout the
   0.2.3 entry in `CHANGELOG.md` describes as removed. Retake against the
   current build at full window width, cropped to whole rows.
6. Clear `bin/` and `obj/`, then build Release/x64 (see the clean-build
   rule in the addendum - it is not optional).
7. Tag the release commit `v<version>` and push the tag. That triggers
   `.github/workflows/release.yml`, which rebuilds Release/x64 on CI and
   publishes the `.bhm` to GitHub Releases. Check the run succeeded and the
   asset is attached.
8. Copy `bin/x64/Release/GW2CraftingHelper.bhm` into the live Blish HUD
   install's `modules` directory and reload Blish HUD.

Because every deployed build has a tag, any two shipped builds can be
compared with `git diff v0.2.0..v0.2.1`.

## Required: an offer diff on every `data(vendor):` pull request

`ref/vendor_offers.json` is 14.8MB on a single line, so `git diff` on a
vendor refresh reports `1 insertion(+), 1 deletion(-)` - the whole file
that prices every vendor in the game, replaced as one indivisible hunk.
A reviewer facing that has two options: rubber-stamp it, or hand-write a
JSON differ. This repo already has the scar tissue from the first option
(`ref/vendor_offer_exclusions.json` exists because a stale row shipped).

**A pull request containing a `data(vendor):` commit must carry the
`--diff-summary` output in its body.** `tools/refresh-vendor-data.sh`
snapshots the baseline before overwriting it and prints the summary at the
end of the run, so it is already on screen when the PR is written. To
produce it by hand from any two dataset copies:

```
dotnet run --project tools/VendorOfferUpdater/VendorOfferUpdater.csproj -- \
    --diff-summary <old vendor_offers.json> <new vendor_offers.json>
```

It reports offers added, removed, repriced and retagged, keyed by merchant
and item rather than by the content hash - see
`tools/VendorOfferUpdater/README.md` for why the raw `offerId` set is not
usable for this. A refresh that changed nothing prints "No offer changed",
and `ref/vendor_offers.json` will be byte-for-byte unmodified: only
`ref/vendor_offers_manifest.json` moves. That is the intended no-op signal.

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
  tag convention above (`0.2.4`, measured 2026-08-25). Nothing *enforces*
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

The supported path is the `.bhm` attached to a GitHub Release, dropped
into Blish HUD's `modules` folder - that is what `README.md`'s "Installing"
section documents, and the two should stay consistent. There is still no
in-app Blish HUD module-repository listing, so the download is manual.

Building it locally instead produces the identical artifact:

1. Clear `bin/` and `obj/`, then build in Release, `x64`:
   `dotnet build GW2CraftingHelper.csproj -p:Platform=x64 -c Release`.
2. Locate the produced `.bhm` (e.g. `bin\x64\Release\GW2CraftingHelper.bhm`).
3. Copy it into your Blish HUD installation's `modules` directory and
   (re)load Blish HUD.

This is also exactly what the release workflow does on CI - the tag and
CHANGELOG entry are what make a given copy of those three steps
identifiable after the fact.

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

- ~~A CI job that builds Release/x64 on a tag and attaches the resulting
  `.bhm` as a GitHub Release asset~~ - done:
  `.github/workflows/release.yml`. Untested against a real tag push at the
  time of writing; the first release exercises it.
- A Blish HUD module-repository listing, so the module is installable from
  inside Blish HUD rather than by downloading a file. This is now the only
  remaining friction in a non-developer install.
- ~~A convention for bumping `manifest.json`'s `version` per release~~ -
  done: the CHANGELOG + `v<version>` tag protocol at the top of this file,
  practiced across v0.2.0 through v0.2.4.
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
