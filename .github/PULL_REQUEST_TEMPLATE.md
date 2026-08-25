## Summary

Brief description of what this change accomplishes.

## What Changed

High-level summary grouped logically (not per-file noise).

## Validation Performed

- Build command run and result (`dotnet build GW2CraftingHelper.sln -p:Platform=x64`)
- Test command run and result (`dotnet test GW2CraftingHelper.sln` - all three test projects, matching CI)
- Manual validation steps (if applicable)

## Repo Invariants Checklist

ASCII-only source, no em-dashes, Blish-free tests, `<Compile Include>`
registration and live-doc path resolution are enforced by the `invariants` CI
job. A rule CI checks does not need a checkbox; these two do.

- [ ] Item/currency/vendor IDs remain internal-only (not displayed to users)
- [ ] Pricing logic preserves multi-source correctness and avoids invalid currency comparisons

## Risks / Follow-ups

Known tradeoffs, edge cases, or future improvements.
