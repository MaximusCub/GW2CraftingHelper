> **Milestone record - 2026-08-24, branch `seed-integrity`.** Moved verbatim out of the append zone in `docs/KNOWN-ISSUES.md` by the 2026-08-25 rotation.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Seed integrity: the reseeder silently deleted hand-authored recipes (seed-integrity)

Found while investigating the in-game report that Gift of Rays showed
UNKNOWN, by running the RecipeSeeder into a scratch directory and diffing
its output against the shipped seed rather than trusting either.

**The defect.** `ref/recipes_seed.json` ships 1,595 negative-id recipes:
1,591 Mystic Forge rows regenerated from `ref/mystic_forge_recipes.json`,
and **four synthetic rows that exist in no source file at all** - ids
-1592..-1595, the Infinite Trebuchet Blueprint chain, carrying
`disciplines: ["Merchant"]` / `["Achievement"]` and per-ingredient
`achievementId`/`achievementBit` metadata. Nothing regenerates them: the
official API serves no negative ids, and the forge source file holds forge
rows only. A reseed therefore wrote 14,962 recipes where the shipped seed
has 14,966, deleting all four without a word.

This is the same defect class as the `expectedOutputCount` overrides that
`MergeMysticForgeRecipes` used to drop on every reseed (see its own comment):
hand-authored data that survives only until someone regenerates.

**A wrong fix, rejected.** The obvious move - backport the four rows into
`mystic_forge_recipes.json` - was implemented, then abandoned after
diffing: that file's merge path forces `disciplines: ["MysticForge"]` and
carries no achievement fields, so it would have silently reclassified a
Merchant row as a forge recipe and dropped the achievement metadata. A
smaller diff that corrupts data is worse than no fix.

**The fix taken.** Step 5a in the seeder reads the seed file it is about to
overwrite and carries forward every negative-id recipe that the regenerated
set does not contain, verbatim, adding its search entry. Preservation is now
structural rather than something a future maintainer has to remember, and it
covers any future hand-authored row, not just these four.

**Verified empirically** (the tool has no test project; this is the honest
check): with the fix, a full reseed against build 205780 reproduces
`recipes_seed.json` byte-identically, and all four rows keep their
disciplines and achievement metadata. The refreshed seed is committed:
search entries 16,022 -> 16,024 and item names 14,762 -> 14,766 (the four
outputs are now locally named, so the Trebuchet chain autocompletes and
renders its icons offline), manifest build 205,505 -> 205,780. Both count
pins in the test suite moved with a comment saying why.

**Note for the UNKNOWN investigation** (branch field-fixes-3 owns the code
rule): this reseed also aligns the seed's build id with the live one, so the
"seed negative entries will fall back to API" condition stops firing today.
That is an alignment, not a fix - the next game build breaks it again, which
is exactly why the durable fix is the fallback rule itself.

Gate: not required - dev-tool and data change; the module's behavior is
covered by the suite (2564 green) and the byte-identical round trip above.
No `.bhm` content changes except the four newly-named items.


