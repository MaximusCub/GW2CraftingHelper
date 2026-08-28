# MysticForgeSeeder

Offline tool that scrapes Mystic Forge recipes from the
[GW2 Wiki](https://wiki.guildwars2.com/) API and resolves the item names it
finds to GW2 item IDs, producing `ref/mystic_forge_recipes.json` - the
Mystic Forge recipe data the module (and `tools/TaimisToolbench.RecipeSeeder`,
which merges it into the main recipe search index) reads at build/seed time.
The official GW2 API does not expose Mystic Forge recipes, which is why this
tool goes to the wiki instead.

This is a standalone .NET 8 console project with no dependency on the main
`TaimisToolbench.csproj` - it does not need the module's `packages/`
restored to build.

## Quick Start

```
dotnet run --project tools/MysticForgeSeeder/MysticForgeSeeder.csproj
```

Run from anywhere inside the repo - it walks up from the current directory
looking for a `.git` folder to find the repo root, then reads/writes under
that root's `ref/` directory.

## CLI Reference

| Flag | Default | Description |
|------|---------|-------------|
| `--dry-run` | off | Query the wiki but skip writing output (used to preview counts) |
| `--force-resolve` | off | Re-resolve every item name to an ID instead of skipping names already in the cache |
| `--delay <ms>` | 250 | Delay between wiki API requests |
| `--max-requests <n>` | 200 | Safety limit on total HTTP requests to the wiki |

## What It Does

1. Queries the wiki for all Mystic Forge recipes (`WikiRecipeClient.QueryMysticForgeRecipesAsync`).
2. Collects every unique output/ingredient name across those recipes
   (case-insensitive dedup).
3. Resolves each new name to a GW2 item ID via the wiki, skipping names
   already present in `ref/mf_item_id_cache.json` unless `--force-resolve`
   is set. Unresolved names are cached as a miss sentinel and printed (up
   to 50) so they can be investigated.
4. Builds recipe objects, skipping any recipe with an ingredient count
   `<= 0` or an ingredient/output name that never resolved to an ID
   (skips are printed).
5. Writes `ref/mystic_forge_recipes.json`.

## Data Files

| File | Role |
|------|------|
| `ref/mystic_forge_recipes.json` | Resolved Mystic Forge recipes. Committed and consumed by the module and by `tools/TaimisToolbench.RecipeSeeder`. |
| `ref/mf_item_id_cache.json` | Name -> item ID cache (including unresolved-name sentinels). Gitignored - dev-only, avoids re-resolving known names on every run. |

## When to Re-run

- After a game update adds, removes, or changes Mystic Forge recipes.
- After the wiki's Mystic Forge recipe pages are corrected/updated.
- Followed by re-running `tools/TaimisToolbench.RecipeSeeder`, since that
  tool merges `ref/mystic_forge_recipes.json` into the search index it
  produces - a stale seed here means stale Mystic Forge search results in
  the module.
