> **Frozen record - 2026-08-15, branch `recipe-ingestion-bug-class-missing-schema-version`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Recipe-ingestion bug class: missing schema-version parameter (2026-08-15)

Root-caused by two independent investigations plus orchestrator-verified
live API probes: every recipe API call in this module omitted the GW2
API's `v=` schema-version query parameter entirely. The GW2 API hides an
entire era of recipes - every recipe whose ingredient list can include a
`Currency` (or other non-`Item`) entry - from UNVERSIONED responses:

- Unversioned `/v2/recipes` (the full id list) returns 13,183 ids;
  versioned `/v2/recipes?v=<date>` returns 13,371 - 188 recipes are
  invisible to any unversioned caller, full stop.
- Unversioned `/v2/recipes/14025` (Amalgamated Rift Essence -> item
  100930) 404s outright - "no such id" - even though the recipe fully
  exists; the versioned request returns it complete.
- Even where a recipe exists in both shapes, ingredient JSON differs:
  versioned ingredients always key their item id as `"id"`; unversioned
  ingredients key it as `"item_id"`. The old parser
  (`Gw2RecipeApiClient.ParseRecipe`, and the offline seeder's
  `ParseRecipeBatch`) read `"item_id"` unconditionally, so even an
  ordinary recipe fetched through a hypothetical versioned call would
  have silently parsed every ingredient id as 0 (Newtonsoft's
  `Value<int>` on a missing key) or, in the seeder's `System.Text.Json`
  path, would have THROWN outright (`GetProperty` has no missing-key
  tolerance) - the seeder could not have completed a versioned run at
  all without this fix.

**The fix (`Services/Gw2RecipeApiClient.cs`,
`tools/GW2CraftingHelper.RecipeSeeder/Program.cs`).** Both `/v2/recipes`
callers (the live runtime client and the offline seeder) now append
`v=<pinned date>` to every recipe URL - search, single-recipe detail, and
the seeder's own id-list and batch-detail endpoints. Pinned to a literal
date (each file keeps its own `SchemaVersion` constant, matching the
existing `BaseUrl` duplication pattern between the two files) rather than
`v=latest`: the module wants "the schema version that exists today,
permanently" - a literal date keeps returning today's shape even after a
future upstream schema revision, where `v=latest` would silently start
returning a new shape with no code change to review. Re-pinning the date
is a deliberate, reviewed action, not automatic. Both ingredient parsers
now read `"id"` first, falling back to `"item_id"` only for defense (an
accidental unversioned call, or a future regression) - verified that no
row currently in `ref/recipes_seed.json` needs the fallback at all (every
existing seed row already stores `RawIngredient`'s own C# property name,
not the raw API key). The seeder's `System.Text.Json` fallback also fixes
a real crash: its old unconditional `GetProperty("item_id")` throws on
any versioned typed ingredient, which - now that every seeder call is
versioned - would otherwise fail the very first batch containing a
Currency ingredient.

**Test:** the seeder's own contract-mirror test
(`Gw2RecipeApiClientParseTests.ParseRecipe_IngredientsWithExplicitType_
PreservesType`) fabricated a "hypothetical" shape keyed on `item_id` for
a typed ingredient - not what the real API ever sends, and exactly the
wrong shape to have caught this bug. Replaced with a test built from the
real, byte-for-byte captured response of
`curl "https://api.guildwars2.com/v2/recipes/14025?v=2026-08-15"`, plus a
second test proving the `item_id` fallback still works for the genuinely
hypothetical legacy shape.

**Re-seeded (`ref/recipes_seed.json`, `ref/recipe_search_seed.json`,
`ref/recipe_seed_manifest.json`, `ref/item_name_seed.json`).**
`tools/GW2CraftingHelper.RecipeSeeder` was re-run against the live API
(build 205505; the previous seed was build 195497, generated
2026-02-20 - about six months stale). Recipe 14025 is now present with
`outputItemId: 100930` and its 3 typed `Currency` ingredients (78/80/79)
plus the 1 `Item` ingredient (Glob of Ectoplasm, 19721, count 50); the
stale negative search entry `"100930": []` is gone, replaced by
`"100930": [14025]`. Net +230 recipes (14736 -> 14966) and +248 search
entries (15774 -> 16022, one entry - `100930` - flipping from negative to
real rather than being newly added). This is LARGER than the isolated
188-recipe schema gap alone: six months of ordinary GW2 content patches
landed in between, independently of this fix (confirmed live - e.g.
recipe 7924's `outputItemId` genuinely moved from 48200 to 107474
upstream, same item name "Wei Qi's Warfists Armor" on both ids, a
real ANet re-ID unrelated to this bug). Every RawRecipe/RawIngredient row
also gained the M37 achievement-dedup schema's nullable fields
(`expectedOutputCount`, `achievementId`, `achievementBit`), added to the
model after this seed was last generated - inflating the raw file diff
far beyond the ~230-recipe content change, but not a value change
for any pre-existing recipe. Spot-checked byte-identical (modulo those
new null fields): the full Zojja's Claymore recipe chain (7836/11539/
11517/11548, already covered by `ZojjasClaymoreValidationTests`).

**Adversarial-review catch during re-seeding:** a from-scratch seeder run
is a full regeneration, not a merge - it silently DROPPED the 4
hand-authored M37 achievement-bit recipes (`-1592` through `-1595`,
Infinite Trebuchet Blueprint and its 3 Merchant sub-recipes), since those
were manually spliced into the previously-shipped seed files and are not
derivable from either the live `/v2/recipes` list or
`ref/mystic_forge_recipes.json` - the seeder has no code path that
produces them. Caught only because `RecipeCacheSerializerTests` already
pins their presence. Restored via a throwaway console tool (deleted
before this fix's commits - same "disposable scratch project" precedent
as the W3D KNOWN-ISSUES entry) that loaded both the old and newly
regenerated seed files through the real `RecipeCacheSerializer`
production path and re-serialized the union, so the on-disk shape is
byte-identical to what a seeder that also knew about achievement recipes
would have produced. `RecipeCacheSerializerTests`' pinned counts (recipes
14736 -> 14966, searches 15774 -> 16022) and `ItemNameSeedDataTests`'
pinned count (14587 -> 14762) were updated to match.

**Runtime discovery caveat (documented in code at
`Gw2RecipeApiClient.SearchByOutputAsync` too):** versioning the search URL
only fixes recipes this client can otherwise SEE via search. The GW2
API's own `/v2/recipes/search` index has a SEPARATE gap, independent of
this bug: `/v2/recipes/search?output=100930&v=latest` returns an EMPTY
array live, even though recipe 14025 fully exists and is fetchable by id.
Live search-by-output cannot discover these recipes at all, versioned or
not - only the seeded search index (built by the offline seeder walking
the full `/v2/recipes` id list, never the search endpoint) can. A cache
miss on one of these output items during a live module session will
therefore still come back empty from `SearchByOutputAsync`'s own API
fallback; only the shipped seed protects against that.

**Out-of-scope finding, deferred (not fixed by this pass):** re-running
the seeder with schema versioning incidentally changed how GUILD-gated
recipes are shaped too - an unversioned response puts a guild-upgrade
requirement in a separate top-level `guild_ingredients` array (which this
module has never read, on either side of this fix); the versioned
response folds it directly into `ingredients` as a new, previously-unseen
`"GuildUpgrade"` ingredient type (678 such ingredient rows across
Guild Decoration/Scribe recipes in the current seed, e.g. recipe 9917 ->
item 75375, guild upgrade id 279). Neither `PlanSolver` nor
`CraftingTreeBuilder` has a `"GuildUpgrade"` arm: `PlanSolver.Evaluate`
only short-circuits `"Currency"` to a free leaf, and
`CraftingTreeBuilder.BuildNode` buckets ANY non-`"Item"` type as a
display `Currency` leaf - so a guild-upgrade ingredient renders today as
a generically-named "Currency" leaf (`Gw2Constants.ResolveCurrencyName`
has no entry for a guild upgrade id and falls back to the literal string
"Currency") that costs nothing. Verified NOT a crash risk (empirically,
via `AmalgamatedRiftEssenceIngestionTests.
GuildUpgradeIngredient_DiscoveredByTheSameSchemaFix_DoesNotThrow`, and by
code inspection: `CollectItemIds` already excludes non-`"Item"` node ids
from the TP price fetch, so a guild-upgrade id is never looked up as if
it were a tradeable item). Cosmetic-only, bounded to Guild Decoration
recipes, and needs real design work (a new ingredient-type concept, not
a one-line fix) - left for a future milestone.

**PARTIALLY RESOLVED (2026-08-16):** this finding under-described the real
severity, though not quite the way first written here either (see the
correction below) - a live-API audit found a GuildUpgrade ingredient does
NOT reach the item-pricing path described above (that observation held up:
`CollectItemIds`' `"Item"`-only gate means it never is), but it DOES reach
`PlanSolver`'s vendor-offer evaluation, which prices offers by raw
ingredient id with no `"Item"`-type gate at all - a genuine mis-costing
bug (latent-but-reachable-via-vendor-offers in the current seed, not
merely cosmetic), not just a display gap. See "GuildUpgrade ingredient
costing/display fix" below for the corrected mechanism and the fix (the
renamed test above is now
`GuildUpgradeIngredient_NeverPricedAsItemOrCurrency_DisplaysAsUnresolvedGuildUpgrade`).
**Scope of "resolved" (adversarial review correction, 2026-08-16):** only
the mis-costing bug and the wrong-domain "Currency" mislabel are fixed -
the ORIGINAL deferred item's other half (resolving a GuildUpgrade
ingredient's real upgrade name, and verifying the active character's
guild actually owns/has-unlocked it) remains unimplemented; the leaf
still renders the generic, ID-free "Guild upgrade (unresolved)" label.
See the "GuildUpgrade ingredient costing/display fix" section's own
"Remaining / deferred" note below - this marker previously read simply
"RESOLVED" with no such scoping, which left the deferred remainder
recorded nowhere even though three production comments point readers at
this document for exactly that remainder.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - PASS (0
errors). Tests: `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` - 1276 total (1273 baseline + 3 net new:
the replaced contract-mirror test became 2 real tests, plus the 2 new
`AmalgamatedRiftEssenceIngestionTests`) - PASS.

Gate: PASS 2026-08-16 (orchestrator live sandbox session, combined wave-4 staging build). Verified: Amalgamated Rift Essence is searchable via the regenerated name seed, recipe 14025 resolves from the regenerated recipe seed with its three currency ingredients plus 50 Globs of Ectoplasm across all nine disciplines, currency leaf names resolve correctly via live metadata (Fine/Masterwork/Rare Rift Essence), full plan generates in ~2s. The orchestrator independently verified the committed seed files contain 14025 and the cleared "100930" negative search entry before the gate.

---
