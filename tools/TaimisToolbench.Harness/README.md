# TaimisToolbench.Harness

A console harness for exercising the crafting-plan pipeline
(`Services/CraftingPlanPipeline.cs`, `Services/PlanSolver.cs`) directly,
without running inside Blish HUD. Useful for checking plan output, timing,
and cache behavior after a solver/pipeline change without a manual
in-game/in-Blish test pass for every iteration.

It references `TaimisToolbench.csproj` directly (`ProjectReference` with
`SetPlatform=x64`), so building it also builds the main module.

## Quick Start

```
dotnet run --project tools/TaimisToolbench.Harness/TaimisToolbench.Harness.csproj -- --profile 1
```

## CLI Reference

| Flag | Default | Description |
|------|---------|-------------|
| `--profile <n>` | required | Which built-in item profile to plan for (see below) |
| `--iterations <n>` | 1 | Re-run the plan this many times (for warm-cache timing) |
| `--live` | off | Use the real GW2 API clients instead of null/offline stubs |
| `--raw` | off | Print raw plan output |
| `--print-cache-stats` | off | Print recipe cache hit/miss counters after planning |
| `--clear-overlay-cache` | off | Clear the on-disk overlay recipe cache before running |
| `--dump-tree` | off | Dump the full recipe tree, not just the top-level decision |
| `--homestead-tier <0\|1\|2>` | pipeline default (tier 0) | Homestead Refinement efficiency tier, applied uniformly to Fiber/Metal/Wood (the live module exposes these three independently via the Settings tab; this harness applies one tier to all three for simplicity) |

## Profiles

Built-in profiles are small, fixed item lists defined in `Program.cs`
(`GetProfileItems`) - useful because they give a reproducible before/after
comparison point for a solver change:

| Profile | Item(s) |
|---|---|
| 1 | Gift of Fortune (plus Zojja's Claymore when `--live` is set) |
| 2 | Exordium |
| 3 | Klobjarne Geirr - reaches Homestead Refinement via Gift of the Homesteader; pair with `--homestead-tier` to compare decisions/quantities across tiers |

## Data Files

Without `--live`, the harness loads the same `ref/*.json` seed files the
shipped module uses (vendor offers, recipe search/recipe seeds) if they're
present next to the built harness executable, and writes its own working
cache under a local `harness_data/` folder (created next to the built
executable, not under `ref/` or the repo root).

## When to Re-run

- After any change to `PlanSolver`, `CraftingPlanPipeline`, or the recipe
  cache stores, to sanity-check plan output/timing before writing or
  updating a formal test.
- With `--live` when validating against current, real GW2 API prices
  rather than the offline seed data.
