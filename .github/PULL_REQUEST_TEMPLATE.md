## Summary

Brief description of what this change accomplishes.

## What Changed

High-level summary grouped logically (not per-file noise).

## Validation Performed

- Build command run and result (`dotnet build GW2CraftingHelper.sln -p:Platform=x64`)
- Test command run and result (`dotnet test GW2CraftingHelper.sln` - all three test projects, matching CI)
- Manual validation steps (if applicable)

## Repo Invariants Checklist

ASCII-only source, no em-dashes, `<Compile Include>` registration, live-doc
path resolution, and the absence of Blish HUD and Gw2Sharp references under
`tests/` are all checked by the `invariants` CI job, so they need no checkbox
here. What CI cannot check is whether a test exercises anything real: a
contract mirror and a fake-I/O stub compile and pass exactly like the genuine
article. That one is a reading, which is why it leads the list.

- [ ] Tests exercise real production code paths (no contract-mirror or fake I/O tests)
- [ ] Item/currency/vendor IDs remain internal-only (not displayed to users)
- [ ] Pricing logic preserves multi-source correctness and avoids invalid currency comparisons

## Risks / Follow-ups

Known tradeoffs, edge cases, or future improvements.
