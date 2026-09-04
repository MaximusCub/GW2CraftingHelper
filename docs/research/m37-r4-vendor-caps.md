# M37 R4 - Vendor Cap Data Seeding + Stale-Offer Sweep (Research Report)

Research-only, dev-time. No runtime code was changed. All web probing was done
directly against the live `wiki.guildwars2.com` Semantic MediaWiki (SMW) `api.php`
endpoint, read-only. The module itself never calls the wiki (or gw2efficiency) at
runtime; findings below are meant to become static seed JSON + code design for a
follow-up implementation session.

---

## 1. Upstream gw2efficiency mechanism (how a cap is meant to be CONSUMED)

This section is **not fresh research** - it restates what M34's own research already
established and is currently encoded verbatim in `PlanSolver.cs` doc comments
(`EvaluateVendorOffers`'s XML doc, `FinalizeVendorBatches`'s XML doc). I re-read the
code to confirm the comments accurately describe the shipped behavior; I did not
re-fetch `gw2efficiency/recipe-calculation`'s `dailyCooldowns.ts` for this report.
**INFERRED (from M34-era code comments, not independently re-verified against gw2e
source):**

- gw2efficiency's `dailyCooldowns.ts` treats a vendor purchase cap as **purely a
  post-solve UI notice**. A cap never excludes an offer from competing in the
  craft/buy/vendor decision and never re-routes the tree to a different
  acquisition source - it is surfaced only as "this will take you more than one
  day/week" after the cheapest tree is already fixed.
- Our module's M34 implementation (`PlanSolver.FinalizeVendorBatches`,
  `Models/TimegatedItem.cs`) already echoes exactly this: `CraftingPlan.TimegatedItems`
  is populated **after** the winning vendor offer and its aggregate purchase count
  are known, comparing `unitsNeeded` (ceil(aggregate demand / offer.OutputCount),
  merged across every tree occurrence) against the offer's cap. **DailyCap takes
  precedence when positive; WeeklyCap is used only when DailyCap is absent/zero**
  (`PlanSolver.cs` ~line 1205: `cap = DailyCap>0 ? DailyCap : (WeeklyCap>0 ?
  WeeklyCap : null)`). This is MEASURED (direct code read) and is already covered
  by dedicated tests (`PlanSolverTests.cs`: `DailyCapTakesPrecedenceOverWeeklyCap`,
  `WeeklyCapUsed_WhenDailyCapAbsent`, an explicit-zero-cap test, and an
  OutputCount-batching test).
- **Conclusion for this task**: the consumption side (`PlanSolver` + `TimegatedItem`
  + the Crafting Steps informational row in `PlanViewModelBuilder.cs`) is fully
  built, tested, and gw2e-parity-correct. The *only* gap is that zero seeded offers
  ever populate `DailyCap`/`WeeklyCap`, so the machinery is dead code today. This
  task is a **pure data-seeding problem**, not an algorithm problem.

---

## 2. Ground-truth data (wiki-verified, complete)

### 2a. Does SMW expose purchase caps as queryable properties? **YES - MEASURED.**

I probed the live wiki directly (not the static `Special:Properties`/`Special:Browse`
pages, which are AJAX-rendered and don't return real data to a non-JS fetch - see
Section 6 caveats). Using the SMW `action=ask` JSON API directly:

- A guessed property name, `Has purchase limit`, is **not real** - MEASURED:
  `[[Has purchase limit::+]]` returns `"results":[],"meta":{"count":0}}` (SMW
  echoes back any syntactically-valid property name in `printrequests` whether or
  not it is ever used - zero matches for a wildcard query is the actual proof of
  non-existence, not the absence of an error).
- Five **real, populated** numeric (`typeid":"_num"`) SMW properties exist, all set
  on the **same per-offer subobject** (`NpcPage#vendorN`) that `WikiSmwClient.cs`
  already queries for `Has item cost`/`Has item quantity`/`Sells item`/`Has
  vendor`/`Located in`:

  | Property (exact SMW name) | Verified real usage (raw JSON) | Approx. wiki-wide usage |
  |---|---|---|
  | `Has daily purchase cap` | `Adisa#vendor6`->`[5]`, `Aksim#vendor7/8/9/10`->`[5]` | ~433 pages (Property: page's own reported usage count - MEASURED via `wiki.guildwars2.com/wiki/Property:Has_daily_purchase_cap`) |
  | `Has weekly purchase cap` | `Archaeologist Vorri/historical#vendor70`->`[10]`, `#vendor71`->`[5]`, `Arid Esker#vendor20/21`->`[7]`, `Ash Legion Provisioner#vendor1`->`[7]` | ~450 pages |
  | `Has character purchase cap` | `Einar Blackforge#vendor1/2/3`->`[1]`, `Escaped Prisoner Arissea#vendor1/5`->`[1]` | not measured (real, but count not fetched) |
  | `Has total purchase cap` | `"Gravedigger" Fim#vendor7`->`[1]`, `Aetherblade Quartermaster#vendor2..5`->`[1]` | not measured |
  | `Has seasonal purchase cap` | `Wizard's Vault/Historical Astral Rewards#vendor1`->`[3]`, `#vendor2`->`[1]`, `#vendor4`->`[40]`, `#vendor5`->`[30]`, `#vendor21`->`[1]` | not measured |

  All five were confirmed with **raw JSON quoted verbatim** by the fetch tool for at
  least one query each (shown above); the "~433"/"~450" counts came from the
  auto-generated `Property:` page's own usage-count display (MEASURED but not
  independently re-derived from a raw count query - SMW's `format=count` output
  format did not work through this API wrapper in my testing, see Section 6).
- **Source of these properties**: they are set by `Template:Vendor_table_row` (the
  wikitext macro every vendor-item table row is built from) from its `per day`,
  `per week`, `per season`, `per character`, `total` parameters respectively.
  **This is INFERRED/reported by the fetch tool's summary of the raw template
  source and its documentation page**, not a byte-for-byte quote I personally
  re-verified character by character - but it is strongly cross-corroborated (see
  2b) by three independent methods all agreeing on the same values for the same
  rows: raw wikitext template calls, the rendered HTML table's "Limit" column, and
  live SMW `ask` query results.
- Our `Models/TimegatedItem.cs`/`TimegatedCapType` enum only has `Daily`/`Weekly`
  - `Has character purchase cap`, `Has total purchase cap`, `Has seasonal purchase
  cap` have **no corresponding model field anywhere** in the module. Seeding those
  three would currently have nowhere to go (see Section 4/6).

**Practical consequence**: `tools/VendorOfferUpdater/WikiSmwClient.cs`'s
`PrintoutSuffix` constant needs two more `|?...` printout requests
(`Has daily purchase cap`, `Has weekly purchase cap`) appended to its **existing**
query - no new query condition, no new pagination/partitioning logic, no new
NPC/page traversal. The subobjects being fetched are already the right ones.

### 2b. The concrete named case: "Candy Corn Vendor (Weekly)" Ecto offers

MEASURED (triangulated via three independent methods that all agree):

1. **Raw wikitext** (`action=raw` on the page) contains, verbatim as reported by
   the fetch tool:
   ```
   {{vendor table row|item=Glob of Ectoplasm|quantity=5|cost=1 Gibbering Skull|per week=1}}
   {{vendor table row|item=Glob of Ectoplasm|quantity=5|cost=1 Tyria's Best Nougat Center|per week=1}}
   {{vendor table row|item=Glob of Ectoplasm|quantity=5|cost=1 High-Quality Plastic Fangs|per week=1}}
   ```
2. **Rendered HTML table** shows a "Limit" column value of `"1 per week"` for each
   of those three rows (plus other rows on the same vendor page: Trader's Candy
   Core Chest = 1/wk, Trader's Grim Machine Chest = 1/wk, Transmutation Charge =
   3/wk, Tattered Bat Wing = 1/wk, Chest of Legendary Shards = 3/wk, Tyrian
   Exchange Voucher = 1/wk - 9 capped rows total on this one vendor page, all
   weekly, none daily).
3. **Live SMW `ask` query** against `[[Has vendor::Candy Corn Vendor (Weekly)]]`
   with `Has daily purchase cap`/`Has weekly purchase cap` printouts returns
   `weekly cap = 1` for the three Ecto rows (subobjects `#vendor4`, `#vendor5`,
   `#vendor6`), `weekly cap = 3` for Transmutation Charge and Chest of Legendary
   Shards, `weekly cap = 1` for the remaining four, and **empty daily cap for all
   nine** - matching the rendered table exactly.

Cross-check against our own repo data: `ref/vendor_offers.json` currently has
**exactly 3** offers for `outputItemId 19721` (Glob of Ectoplasm) from
`merchantName: "Candy Corn Vendor (Weekly)"`:

```
offerId 125d48dd... cost=[Item 48807 x1] (Gibbering Skull)          -> should be WeeklyCap=1
offerId ea03f152... cost=[Item 48805 x1] (High-Quality Plastic Fangs) -> should be WeeklyCap=1
offerId ecf886df... cost=[Item 48806 x1] (Tyria's Best Nougat Center) -> should be WeeklyCap=1
```
All three currently have `DailyCap`/`WeeklyCap` omitted (null) - **exactly the gap
KNOWN-ISSUES #28 names**. Each should carry `WeeklyCap = 1` (not `DailyCap`).

I also checked whether the *plain* `"Candy Corn Vendor"` page (a **different** NPC,
using `Piece of Candy Corn` as its currency - 43 subobjects checked via a single
`ask` query, raw JSON quoted in full during research) carries any caps: **no** -
every one of its 43 rows returns `"Has daily purchase cap":[]`,
`"Has weekly purchase cap":[]`. This matches real game knowledge (the base Trick-
or-Treat vendor is uncapped; only the reward-track-style `"(Weekly)"` vendor resets
weekly) and confirms our seed's current null-cap state is **already correct** for
those 42 non-"(Weekly)" Candy Corn offers (exact count from a direct query over
`ref/vendor_offers.json` for `merchantName == "Candy Corn Vendor"`) - the gap is
specifically the `"(Weekly)"`-suffixed vendor's rows.

### 2c. Gift of Battle / "Battle Master" precedent for the stale-offer sweep

MEASURED from repo history (`git log`, `git show 7e73172`, `docs/KNOWN-ISSUES.md`
item 17's follow-up note): M33 Wave B removed a stale `ref/vendor_offers.json`
entry for Gift of Battle (item 19678), merchant "Battle Master", cost "500 Badges
of Honor". Per the commit message and KNOWN-ISSUES note, **two independent wiki
re-checks** confirmed this purchase path was removed in the **Spring 2016
Quarterly Update** and Gift of Battle has been WvW-reward-track-only since. The
fix was a **manual** single-line JSON edit (`git show 7e73172` diff: 2 lines
changed in `ref/vendor_offers.json`) after manual wiki verification - there is no
existing automated stale-offer detector in the codebase. Today, `ref/vendor_offers.json`
has **zero** "Battle Master" or Gift-of-Battle-outputting offers (verified by
direct query over the current file), confirming the fix stuck.

---

## 3. Current module state (read directly from source)

### 3a. Data flow, dev-time (the seeder)

- `tools/VendorOfferUpdater/Program.cs` (`RunAsync`): Step 1 loads `/v2/currencies`
  from the official API; Step 2 runs `WikiSmwClient.QueryVendorItemsAsync`
  against `[[Sells item::+]]` (or an override `--query`), paginating/partitioning
  by vendor-name prefix when the SMW ~5500-row offset limit is hit, writing a raw
  cache to `ref/wiki_vendor_cache.json` (61,355 rows today, PascalCase
  `WikiVendorResult` shape - `System.Text.Json` default naming, NOT camelCase);
  Step 3 resolves item-based currency names to item IDs via
  `ResolveItemGameIdsAsync`, cached in `ref/item_id_cache.json` (40 KB, ~this
  session confirmed `Gibbering Skull`->48807, `Tyria's Best Nougat Center`->48806,
  `High-Quality Plastic Fangs`->48805, `Piece of Candy Corn`->36041); Step 4
  converts every `WikiVendorResult` to a `VendorOffer` via `Program.ConvertToOffer`
  (skips rows with no resolved game id or an unresolvable cost currency); Step 5
  dedupes by `OfferId` and writes `ref/vendor_offers.json` (camelCase, nulls
  omitted, 53,530 offers, 13 MB today).
- `WikiSmwClient.cs`'s `PrintoutSuffix` constant (hardcoded string, 6 printouts:
  `Sells item.Has game id`, `Sells item`, `Has item quantity`, `Has item cost`,
  `Has vendor`, `Located in`) is the **single choke point** for what gets scraped
  per subobject - this is exactly where the 2 new cap printouts need to be added.
  `WikiVendorResult`/`ParseResult` (same file) have **no** cap fields/parsing at
  all today - both need 2 new nullable-int fields plus 2 new `TryGetProperty`
  blocks mirroring the existing `Has item quantity` block.
- `Program.ConvertToOffer` (~line 456) already **calls**
  `VendorOfferHasher.ComputeOfferId(..., null, null)` with hardcoded `null, null`
  for the cap parameters - the plumbing to pass real values through exists, it
  is simply never fed anything but `null`.

### 3b. `VendorOfferHasher` - EXACT scope, MEASURED

Two near-identical copies exist (`tools/VendorOfferUpdater/VendorOfferHasher.cs`
for the seeder, `Services/VendorOfferHasher.cs` for the runtime module - same
algorithm, SHA256 vs. `SHA256.HashData`/manual hex, kept in sync manually, no
shared assembly reference between the tool and the module). Both compute:

```
sb: "output={id}/{count};costs={sorted "Type:Id:Count" csv};merchant={name};
     locations={sorted csv};dailyCap={value|"null"};weeklyCap={value|"null"}"
offerId = SHA256(sb).ToHexString()
```

**`dailyCap`/`weeklyCap` ARE part of the hashed string** - this is not inferred,
it is read directly from the source (`sb.Append(";dailyCap=")` /
`sb.Append(";weeklyCap=")`, unconditionally, before hashing) and is additionally
proven by an *already-passing* test,
`VendorOfferHasherTests.DifferentCaps_ProduceDifferentHashes`, which asserts
`hashNoCap != hashDailyCap != hashWeeklyCap != hashNoCap` for otherwise-identical
inputs. **So: yes, adding a real cap value to any offer that is currently seeded
with `null,null` baked into its hash WILL change that offer's `OfferId`.** An
offer that has (and always had) no wiki-recorded cap gets byte-identical hash
input before and after this change (still `dailyCap=null;weeklyCap=null`) - its
`OfferId` is unaffected. **Only offers that actually gain a real cap value churn
their ID.** Given ~433-450 wiki subobjects wiki-wide carry a cap
(Section 2a), and our seed converts a subset of all wiki subobjects into
53,530 offers, the number of `OfferId`s that will actually change is expected to
be on the order of a few hundred, not all 53,530 - **exact count is only knowable
after re-running the scraper with the new printouts** (INFERRED order-of-magnitude,
not measured).

**What depends on `OfferId` stability (full enumeration, grepped across the whole
repo, worktrees excluded):**
- `tools/VendorOfferUpdater/Program.cs`: build-time dedup
  (`GroupBy(o => o.OfferId).Select(g => g.First())`) - within a single build run
  only, unaffected by cross-run ID drift.
- `Services/VendorOfferStore.cs`: merges a shipped baseline (`ref/vendor_offers.json`)
  with a runtime user-local overlay file (`vendor_offers_overlay.json`), keyed
  **purely by `OfferId` string equality** (`_mergedById[offer.OfferId] = offer`,
  overlay wins on exact-ID collision). It has **no real-world-identity concept**
  (no "same vendor + same item + same cost, different cap" matching) - if a
  user's local overlay ever contained an old-hash copy of a real offer that the
  shipped baseline now re-hashes differently, both would survive as two distinct
  entries under `GetOffersForItem`, and `PlanSolver.EvaluateVendorOffers` would
  just pick whichever is cheaper (functionally harmless for pricing, but a latent
  duplicate). **This is currently impossible in production**: `IWikiVendorClient`
  (the interface `VendorOfferResolver` -> `VendorOfferStore.AddOffersToOverlay`
  depends on to *populate* that overlay at runtime) has **zero production
  implementations** - the only class implementing it repo-wide is
  `tests/GW2CraftingHelper.Tests/Helpers/InMemoryWikiVendorClient.cs`, a test
  double. `VendorOfferResolver` is referenced only by its own tests and by
  `CraftingPlanPipeline.cs`; I did not find it wired to any concrete wiki client
  in DI/`Module.cs`. So today, no user's overlay file can contain a real vendor
  offer at all - this stale-duplicate risk is theoretical/dormant, not live.
- **Nothing else.** `Models/PlanStep.cs`, `Models/CraftingPlan.cs`, and the
  Blish HUD `SnapshotStore`/`StatusStore` persistence layers were grepped and
  contain **no** `OfferId` field or reference anywhere - a plan is never
  persisted with a vendor offer's identity baked in, so churned IDs cannot
  corrupt saved user data.
- Every hardcoded `OfferId = "v1"` / `"test-vendor"` / `"test-karma-offer"` string
  across `PlanSolverTests.cs`, `CraftingTreeBuilderTests.cs`,
  `DecisionPillPlannerTests.cs`, `CraftingPlanPipelineTests.cs`,
  `MultiItemPlanTests.cs`, `VendorOfferStoreTests.cs`, `VendorOfferResolverTests.cs`
  is an **arbitrary literal**, never derived from `VendorOfferHasher` - these are
  completely unaffected by any hasher/cap change.
- The only tests that exercise the *real* hasher are `VendorOfferHasherTests.cs`
  (both the `Services/` and `tools/VendorOfferUpdater` copies, byte-identical
  test files) and `tools/VendorOfferUpdater/tests/.../ConvertToOfferTests.cs`
  (asserts `offer.OfferId` matches `^[0-9a-f]{64}$`, i.e., shape only, not a
  specific literal value) - **no existing test hardcodes an expected hash
  literal**, so none will need updating for this change; they already exercise
  the cap-bearing code paths defensively.

**Net verdict on reviewability**: because only offers that *gain* a real cap
value churn their ID, and because nothing durable is keyed by `OfferId` in
production, the change is safe. The one real cost is a `git diff` on
`ref/vendor_offers.json` for the (small, expected few-hundred) affected offers
will show as a full line delete+insert (new hash) rather than an in-place `+2
fields` edit, since JSON array elements are whole-line replacements when the
sort key (`OfferId`) itself changes. This is a **diff-noise/reviewability**
concern, not a correctness one - addressed in Section 4's rollout plan.

### 3c. `PlanSolver`/`TimegatedItem` consumption (already correct, per Section 1)

`PlanSolver.FinalizeVendorBatches` (Section 1) and `Models/TimegatedItem.cs`
(`TimegatedCapType { Daily, Weekly }` only - no Seasonal/Character/Total) are
fully implemented and unit-tested against synthetic offers with
`DailyCap`/`WeeklyCap` set. `PlanViewModelBuilder.cs` (line ~390-403) renders
`result.Plan.TimegatedItems` into a `PlanRowType.TimegatedNotice` row in the
Crafting Steps section, labeled `"Daily"`/`"Weekly"` - confirmed by direct read.
**Nothing here needs to change** for a Daily/Weekly-only seed; it needs new
model/UI surface only if a future milestone chooses to seed
Seasonal/Character/Total caps too (see Section 6).

---

## 4. Recommended echo design

### 4a. Seed schema (no breaking change)

`VendorOffer.DailyCap`/`WeeklyCap` (`Models/VendorOffer.cs`,
`tools/.../Models/VendorOffer.cs`) already exist as `int?` and are already
`[JsonIgnore when null]`-equivalent (`DefaultIgnoreCondition.WhenWritingNull` in
`Program.cs`'s serializer options) - **no model/schema change needed** for
Daily+Weekly. Do **not** add Seasonal/Character/Total fields in this pass (see
Section 6 - they have no consuming model/UI yet; adding unused fields would be
scope creep against the "narrowly scoped" review rule).

### 4b. Updater extension (targeted, ~4 small edits)

1. `WikiSmwClient.cs`: extend `PrintoutSuffix` with
   `"|?Has daily purchase cap|?Has weekly purchase cap"`.
2. `WikiSmwClient.cs`: add `DailyCap`/`WeeklyCap` (`int?`) to `WikiVendorResult`;
   in `ParseResult`, add two blocks mirroring the existing `Has item quantity`
   block (`printouts.TryGetProperty("Has daily purchase cap", ...) &&
   ....GetArrayLength() > 0 -> result.DailyCap = ....GetInt32()`, same for
   weekly). Empty array (no cap) must map to `null`, not `0`.
3. `Program.ConvertToOffer`: pass `result.DailyCap, result.WeeklyCap` (instead of
   literal `null, null`) into both `VendorOfferHasher.ComputeOfferId(...)` and the
   returned `VendorOffer { DailyCap = ..., WeeklyCap = ... }`.
4. `ref/wiki_vendor_cache.json` is a **cache** keyed by the old `WikiVendorResult`
   shape without cap fields - it must be **regenerated**, not reused via
   `--resolve-item-currencies-only`, or the cached rows will silently lack caps
   forever. Document this explicitly in the updater README's "When to Re-run"
   section (a new bullet: "After adding new printouts/fields to WikiSmwClient -
   the wiki cache does not retroactively backfill new fields").

### 4c. Reachability scoping (cheap, already computed)

Ran directly against the repo's own seed files (no wiki calls, seconds of
runtime): build the set of every item id that appears as a recipe **output** or
**ingredient** anywhere in `ref/recipes_seed.json` (14,732 recipes -> 15,769
distinct item ids total: 14,570 outputs, 4,173 ingredients). Intersect
`vendor_offers.json`'s 53,530 offers against that set:

| Filter | Count | % of 53,530 |
|---|---|---|
| Offer's output item appears anywhere in the recipe-seed universe (output or ingredient) | 5,707 | 10.7% |
| Offer's output item appears specifically as an **ingredient** of some seeded recipe (the decision-relevant subset - this is exactly what `PlanSolver.Evaluate` looks up `vendorOffers[node.Id]` for) | 5,487 | 10.2% |
| Distinct items (of the recipe universe's 15,769) that have >=1 vendor offer at all | 796 | - |

**Recommendation**: use the "ingredient-of-some-seeded-recipe" set (5,487 offers,
~10%) as the priority queue for both cap-seeding verification and the stale-offer
sweep, not all 53,530. This is a coarse over-approximation (it does not filter to
recipes reachable specifically from a named root like Exordium, and it treats
every seeded recipe as equally "real" even though some may themselves be
unreachable/vestigial) - a tighter BFS from specific named targets is possible as
a follow-up refinement but was not necessary to get an order-of-magnitude-useful
scope cut. The script is ~15 lines of Python/C# over two already-committed JSON
files and reproducible in seconds.

### 4d. Cap seeding for the concrete case (ready to apply)

Set `WeeklyCap = 1` on the three `"Candy Corn Vendor (Weekly)"` Glob-of-Ectoplasm
offers currently in `ref/vendor_offers.json` (offerIds `125d48dd...`,
`ea03f152...`, `ecf886df...` - see Section 2b for the exact cost lines
distinguishing them). This can either be done by hand (2-line-per-offer JSON
edits, matching the Gift-of-Battle precedent's manual style, accepting that the
`OfferId` will need to be recomputed via `VendorOfferHasher.ComputeOfferId` with
the new cap value or the entry will silently mismatch its own dedup key) **or**,
better, by running the extended updater end-to-end once 4b lands, which will
regenerate this (and every other capped offer) correctly and consistently.
Prefer the latter - hand-editing a SHA256-derived ID is exactly the kind of
"invented data" the repo rules warn against.

### 4e. Stale-offer sweep design

No automated tool exists today (Section 2c: the Gift of Battle fix was fully
manual). Recommended minimal automation, scoped to the ~5,487 reachable offers
(4c), not all 53,530:

1. Group reachable offers by `merchantName` (many items share a vendor page, so
   this collapses 5,487 offers to a much smaller number of wiki pages to
   actually re-fetch - e.g. all Candy Corn Vendor items are one page fetch).
2. For each distinct merchant name among reachable offers, re-run the *same*
   `[[Has vendor::"<name>"]]` SMW query used to build the seed and diff the
   returned subobject set against what's currently seeded for that merchant:
   - A seeded `(outputItemId, costLines, merchantName)` combination with **no**
     matching subobject in a fresh query result is a stale-offer candidate (the
     Gift-of-Battle pattern) - flag for manual wiki confirmation before removal,
     exactly as the precedent did (two independent checks), since a transient
     wiki edit-war or a page rename could produce a false negative.
   - A subobject present on the wiki with **no** matching seeded offer is a
     missing-offer candidate (the inverse gap, also worth surfacing even though
     it's a different KNOWN-ISSUES concern).
3. Do **not** auto-delete on a single pass - the precedent explicitly used two
   independent wiki re-checks before removing Gift of Battle; a scripted first
   pass should produce a reviewable candidate list (few-hundred-item scope, per
   4c), not a silent mass deletion.

### 4f. Test plan

- `WikiSmwClientTests.cs` (dev-tool tests, already has a `FakeHttpHandler`/
  `WikiJsonBuilder` harness per Section 3's file listing): add a case asserting
  `ParseResult` maps a populated `Has daily purchase cap`/`Has weekly purchase
  cap` printout array to `WikiVendorResult.DailyCap`/`WeeklyCap`, and a case
  asserting an **empty** printout array (the wiki's own representation of "no
  cap") maps to `null`, not `0` (the real repo-invariant-relevant edge case -
  `PlanSolver.FinalizeVendorBatches` treats `WeeklyCap.HasValue && Value > 0` as
  "has a real cap," so a wrongly-mapped `0` would be silently ignored as
  "no cap," while a wrongly-mapped `null` who should be `1` would silently mask
  a real cap - both are wrong in opposite directions, worth a test each).
- `ConvertToOfferTests.cs`: extend `MakeResult`'s helper to accept
  `dailyCap`/`weeklyCap` and add a case asserting `ConvertToOffer` threads them
  into both the returned `VendorOffer.DailyCap`/`WeeklyCap` and (indirectly,
  via `OfferIdIsPopulated`'s existing `^[0-9a-f]{64}$` shape assertion, or a new
  explicit case) that two calls differing only in cap value produce different
  `OfferId`s - this directly exercises the hasher-stability finding from
  Section 3b as a regression guard, not just a hand-verified claim.
- No `PlanSolver`/`TimegatedItem` test changes needed (Section 3c) - that layer
  is already defensively covered against synthetic capped offers.
- After regenerating `ref/vendor_offers.json`: a targeted assertion (script or a
  new tiny xunit fact reading the checked-in JSON directly, matching the repo's
  "tests exercise real production paths" invariant if it reads the real file
  from `ref/`) that the three named Candy-Corn-Ecto `OfferId`s now carry
  `WeeklyCap == 1` would give a permanent regression guard for the exact
  KNOWN-ISSUES #28 named case.

### 4g. Rollout / reviewability

1. Land 4b (updater code + tests) as one commit - no `ref/` data change yet, so
   the diff is small and purely additive (new printouts, new fields, no behavior
   change to existing offers since `null`/`null` still round-trips identically).
2. Regenerate `ref/wiki_vendor_cache.json` and `ref/vendor_offers.json` via the
   full two-pass refresh (`./tools/refresh-vendor-data.sh`, ~15 min per the
   README) as a **separate** commit, so the (small, few-hundred-row) `OfferId`
   churn is isolated and easy to spot-check in review (grep the diff for
   `"dailyCap"`/`"weeklyCap"` additions vs. unrelated hash-only churn).
3. Run the stale-offer sweep (4e) as a **third**, clearly-labeled commit/PR
   section, since it removes rather than adds data and deserves its own
   reviewer attention per the Gift-of-Battle precedent's "two independent
   checks" bar - do not fold it into the same commit as the cap seeding.
4. Full `dotnet build`/`dotnet test` per `CLAUDE.md` before PR, as always.

---

## 5. Sources

- Local, read directly (all under the repo root,
  main worktree only - `.claude/worktrees/*` copies ignored as stale):
  `docs/gw2e-parity-spec.md`, `docs/KNOWN-ISSUES.md` (items 17, 28), `Models/VendorOffer.cs`,
  `Models/CraftingPlan.cs`, `Models/TimegatedItem.cs`, `Services/VendorOfferHasher.cs`,
  `Services/VendorOfferStore.cs`, `Services/VendorOfferResolver.cs`,
  `Services/IWikiVendorClient.cs`, `Services/PlanSolver.cs` (partial - Evaluate,
  EvaluateVendorOffers, FinalizeVendorBatches, PickCheapest), `Services/PlanViewModelBuilder.cs`
  (grep + targeted read), `tools/VendorOfferUpdater/{Program.cs,WikiSmwClient.cs,
  VendorOfferHasher.cs,README.md,Models/*.cs}`, `tests/GW2CraftingHelper.Tests/Services/
  VendorOfferHasherTests.cs`, `tests/VendorOfferUpdater.Tests/{ConvertToOfferTests.cs,
  VendorOfferHasherTests.cs}`, `ref/vendor_offers.json`, `ref/wiki_vendor_cache.json`,
  `ref/item_id_cache.json`, `ref/recipes_seed.json` (all queried via one-off Python scripts,
  not modified), `git log`/`git show 7e73172` (Gift of Battle removal).
- Live wiki, fetched read-only via `WebFetch` against
  `wiki.guildwars2.com/api.php` (`action=ask`, SMW JSON API) and
  `wiki.guildwars2.com/index.php?...action=raw` / `wiki.guildwars2.com/wiki/...`:
  - `[[Has purchase limit::+]]` (proved non-existence of a guessed property name)
  - `[[Has daily purchase cap::+]]`, `[[Has weekly purchase cap::+]]`,
    `[[Has character purchase cap::+]]`, `[[Has total purchase cap::+]]`,
    `[[Has seasonal purchase cap::+]]` (proved existence + real usage of all five)
  - `[[Has vendor::Candy Corn Vendor (Weekly)]]` and `[[Has vendor::Candy Corn Vendor]]`
    with cap printouts (the concrete named case + its uncapped sibling vendor)
  - `wiki.guildwars2.com/wiki/Property:Has_weekly_purchase_cap` and
    `.../Property:Has_daily_purchase_cap` (usage-count self-report)
  - `index.php?title=Candy_Corn_Vendor_(Weekly)&action=raw` (raw wikitext template
    calls) and `wiki.guildwars2.com/wiki/Candy_Corn_Vendor_(Weekly)` (rendered table)
  - `wiki.guildwars2.com/wiki/Template:Vendor_table_row` (parameter documentation)

---

## 6. Open questions / caveats

- **`Special:Browse`/`Special:Properties` are AJAX-rendered** and returned no
  usable data to a plain `WebFetch` (no JS execution) - I worked around this
  entirely via direct `action=ask` JSON calls and the auto-generated `Property:`
  pages instead, which are static and did return real data. If a future session
  needs the *complete* SMW property list (not just the 5 cap-related ones I
  already confirmed matter here), it will need the same `action=ask`-based
  workaround, not `Special:Properties` directly.
- **SMW's `format=count`** did not behave as expected through this API wrapper
  (returned `count:0`/empty for queries I had just proven return real rows via
  the normal list format) - I did not get a reliable *total* match count for
  `Has daily purchase cap`/`Has weekly purchase cap` beyond the `Property:` page's
  own self-reported "~433"/"~450 pages" figures. UNVERIFIED: the exact total
  count of wiki subobjects carrying each cap type; only order-of-magnitude is
  established.
- **`Has character purchase cap`/`Has total purchase cap`/`Has seasonal purchase
  cap` have zero model/consumption support** in this codebase (`TimegatedCapType`
  is Daily/Weekly only). I deliberately scoped 4a/4b to Daily+Weekly only, since
  seeding the other three now would produce data with nowhere to go and no
  algorithm/UI to interpret it. If a future milestone wants to model
  `per character` caps specifically, note that the solver has **no
  account/character concept at all** today (single-character assumption
  implicit everywhere) - that would need its own design pass, not a data-only
  extension. Flagging this as an open question for the next planning session
  rather than answering it here, since it's out of this task's stated scope.
- **Exact churn count for `OfferId`s** after re-running the full 53,530-offer
  scrape is INFERRED ("a few hundred," Section 3b) - not measured, since that
  requires actually re-running the ~15-minute two-pass scraper, which this
  research-only task did not do (no runtime/build changes were made in this
  session; the updater tool itself was read but not executed).
- The reachability set (4c) is a coarse over-approximation (whole recipe-seed
  universe, not BFS from specific named roots like Exordium) - a tighter
  root-specific reachability count was not computed and would need the actual
  set of "user-facing target items" defined first (that set does not obviously
  exist as a single seed file today - `ref/recipe_search_seed.json` may be the
  right starting point but its schema was not inspected here).

---

## Verification

An independent verifier re-checked this report's claims against primary sources
(live wiki SMW API re-queries, direct repo file reads, and `gh api` fetches of
upstream gw2efficiency source) in a separate pass. Summary of outcome:

**Correction applied (this pass):**
- Section 2b's plain-`"Candy Corn Vendor"`-page check understated the row count:
  the live page actually has **43** subobjects (`#vendor1`-`#vendor43`,
  `meta.count=43`), not 30 - all 43 confirmed uncapped (empty daily/weekly cap
  arrays), so the qualitative conclusion ("plain Candy Corn Vendor is correctly
  uncapped") was already right, only the specific count was off (~30% undercount).
  Also corrected the derived "~49 non-\"(Weekly)\" Candy Corn offers" figure to the
  exact **42**, per a direct query over `ref/vendor_offers.json` for
  `merchantName == "Candy Corn Vendor"`. Both instances fixed in place in Section
  2b above. **This correction does not change any recommendation** (the
  `WeeklyCap=1` fix for the 3 named Ecto offerIds on the *Weekly* vendor, Section
  4d, is unaffected), the hasher-safety analysis (Section 3b), or the
  reachability scoping (Section 4c).

**Independently re-confirmed exactly as originally stated (no correction needed):**
- All 5 SMW cap properties (daily/weekly/character/total/seasonal purchase cap)
  are real and populated; `[[Has purchase limit::+]]` returns `count:0`,
  confirming the guessed property name does not exist.
- Every specific example subobject/value pair quoted in Section 2a's table and
  the narrative (Adisa, Aksim, Archaeologist Vorri/historical, Arid Esker, Ash
  Legion Provisioner, Einar Blackforge, Escaped Prisoner Arissea, "Gravedigger"
  Fim, Aetherblade Quartermaster, Wizard's Vault/Historical Astral Rewards)
  reproduced exactly on live re-query.
- `Property:Has_daily_purchase_cap` / `Property:Has_weekly_purchase_cap` usage
  self-reports (433 / 450) match exactly.
- `Template:Vendor_table_row`'s 5 parameters as documented.
- The full "Candy Corn Vendor (Weekly)" case (Section 2b): all 9 capped rows and
  their exact cap values, including the 3 Ecto rows at `WeeklyCap=1`, reproduced
  exactly, with raw wikitext `action=raw` reproducing the 3 `{{vendor table
  row|...|per week=1}}` calls verbatim. The 3 named offerIds
  (`125d48dd.../ea03f152.../ecf886df...`) in `ref/vendor_offers.json` exist
  exactly as described (outputItemId 19721, matching cost lines, currently
  `dailyCap=None/weeklyCap=None`).
- All repo-source claims in Sections 1, 3a, 3b, 3c: `Services/VendorOfferHasher.cs`
  hash-string format; `Models/TimegatedItem.cs`'s Daily/Weekly-only enum;
  `Services/IWikiVendorClient.cs` having zero production implementations outside
  the test double; `PlanSolver.cs`'s DailyCap-precedence ternary (~line 1205);
  existing `PlanSolverTests.cs` cases; `tools/VendorOfferUpdater/WikiSmwClient.cs`'s
  6-printout `PrintoutSuffix`; `Program.ConvertToOffer`'s hardcoded
  `ComputeOfferId(..., null, null)` call (~line 456); `VendorOfferHasherTests`
  10/10 passing including `DifferentCaps_ProduceDifferentHashes`;
  `ConvertToOfferTests.cs`'s `^[0-9a-f]{64}$` regex assertion; the Gift-of-Battle
  commit (`git show 7e73172`) and KNOWN-ISSUES items 17/28.
- Section 4c's reachability table (14,732 recipes, 14,570 distinct outputs,
  4,173 distinct ingredients, 15,769 union, 53,530 total offers, 5,707
  reachable/10.7%, 5,487 ingredient-reachable/10.2%, 796 distinct items with
  >=1 offer) was independently re-derived from `ref/recipes_seed.json` +
  `ref/vendor_offers.json` in the verification pass and matched exactly,
  including both decimal percentages.
- Section 1's INFERRED upstream-gw2efficiency-semantics claim (caps are a
  post-solve UI notice only, never excluding an offer from the craft/buy/vendor
  decision) was upgraded from INFERRED-and-unreverified to independently
  confirmed: the verifier fetched the live `gw2efficiency/recipe-calculation`
  source (`src/helpers/dailyCooldowns.ts`, `src/cheapestTree.ts`,
  `src/calculateTreeCraftFlags.ts`) and confirmed `dailyCooldowns.ts` only
  accumulates a usage breakdown from an already-computed tree, with zero
  purchase-cap references in the tree-building/decision logic. This report's
  Section 1 text itself is left unchanged (it accurately flagged its own claim
  as inferred/unreverified at the time it was written); this note records that
  a later pass closed that gap in the report's favor.

**Remaining uncertainty (unchanged from Section 6, not resolved by this pass):**
- Exact total wiki-wide counts for `Has character purchase cap`,
  `Has total purchase cap`, and `Has seasonal purchase cap` usage were not
  measured (only the daily/weekly counts have a self-reported total).
- SMW's `format=count` output was still not exercised successfully; the
  ~433/~450 figures remain sourced from the `Property:` page's own self-report,
  not an independently-run count query.
- Exact `OfferId` churn count after a real scraper re-run remains unmeasured
  (order-of-magnitude "a few hundred" only, per Section 3b/6) - would require
  actually executing the ~15-minute two-pass scraper, which no verification
  pass has done.
- The reachability set's coarseness (whole recipe-seed universe vs. BFS from
  named roots) and `ref/recipe_search_seed.json`'s schema remain unexamined.
