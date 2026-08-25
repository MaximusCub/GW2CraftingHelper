## Summary

Brief description of what this change accomplishes.

## What Changed

High-level summary grouped logically (not per-file noise).

## Validation Performed

- Build command run and result (`dotnet build GW2CraftingHelper.csproj -p:Platform=x64`)
- Test command run and result (`dotnet test GW2CraftingHelper.sln` - all three test projects, matching CI)
- Manual validation steps (if applicable)

## Repo Invariants Checklist

- [ ] No Blish HUD / `Gw2Sharp` references added to tests
- [ ] Tests exercise real production code paths (no contract-mirror or fake I/O tests)
- [ ] Item/currency/vendor IDs remain internal-only (not displayed to users)
- [ ] Pricing logic preserves multi-source correctness and avoids invalid currency comparisons
- [ ] `.cs` source stays ASCII-only, no em-dashes introduced

## Risks / Follow-ups

Known tradeoffs, edge cases, or future improvements.
