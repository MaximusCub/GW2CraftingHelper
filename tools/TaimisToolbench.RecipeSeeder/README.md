# TaimisToolbench.RecipeSeeder

Offline tool that queries the official [GW2 API](https://api.guildwars2.com/)
(`/v2/recipes`, `/v2/items`) to build the recipe seed files the module reads
at startup instead of fetching every recipe live.

## Quick Start

From the repo root:

```
dotnet run --project tools/TaimisToolbench.RecipeSeeder/TaimisToolbench.RecipeSeeder.csproj -- --output-dir ref --force
```

Pass `--output-dir` explicitly: with no flag the tool writes into the
`ref/` folder beside its own built executable (`bin/...`), not the repo's.
If seed files already exist in the target directory, it refuses to
overwrite them unless you pass `--force`.

## CLI Reference

| Flag | Default | Description |
|------|---------|-------------|
| `--output-dir <path>` | `ref/` (relative to the built tool's own directory) | Where to write the seed files |
| `--force` | off | Overwrite existing seed files instead of refusing to run |

## What It Does

1. Fetches the current GW2 build ID (recorded in the manifest for
   provenance; a fetch failure here is a warning, not fatal).
2. Fetches every recipe ID from `/v2/recipes`, then batch-fetches full
   recipe details (batches of 200, concurrency 4).
3. Builds a search index mapping each output item ID to the recipe IDs
   that produce it.
4. Merges in Mystic Forge recipes from `ref/mystic_forge_recipes.json`
   (produced separately by `tools/MysticForgeSeeder`) so Mystic Forge
   outputs are searchable the same way as normal recipes.
5. Adds negative/leaf search entries for ingredient items that are never
   the output of any recipe, so a search miss can be distinguished from
   "not yet checked".
6. Fetches display names/icons for every craftable item and writes an item
   name seed for the search/autocomplete UI.

## Data Files

| File | Role |
|------|------|
| `ref/recipes_seed.json` | Full recipe details, keyed by recipe ID. Loaded by the module at startup. |
| `ref/recipe_search_seed.json` | Output item ID -> recipe ID index (including negative/leaf entries and merged Mystic Forge recipes). |
| `ref/recipe_seed_manifest.json` | Seed schema version, source GW2 build ID, and creation timestamp. |
| `ref/item_name_seed.json` | Item ID -> display name/icon for craftable items, used by search/autocomplete. |

## When to Re-run

- Before every release - it is step 2 of `docs/RELEASING.md`'s protocol,
  because a seed stale against the live build puts every user on the slow
  live-API path.
- After a game update that adds, removes, or changes recipes.
- After re-running `tools/MysticForgeSeeder` (its output is merged in at
  step 4 above - a stale `ref/mystic_forge_recipes.json` will seed stale
  Mystic Forge search entries).
- If the exact-count seed-pin tests in the main test suite start failing
  because the seed legitimately changed size (see `docs/KNOWN-ISSUES.md`
  for why those tests pin exact counts rather than just "> 0").
