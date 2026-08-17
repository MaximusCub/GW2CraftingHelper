# VendorOfferUpdater

Offline tool that scrapes vendor-sold items from the [GW2 Wiki](https://wiki.guildwars2.com/) Semantic MediaWiki API, resolves currency names via the official [GW2 API](https://api.guildwars2.com/), and writes a `vendor_offers.json` baseline file consumed by the Blish HUD module.

## Quick Start

The easiest way to refresh vendor data is the wrapper script. Requires **Git Bash on Windows** and the **.NET 8 SDK**.

```bash
# Full refresh — wiki scrape + currency resolution (~15 min)
./tools/refresh-vendor-data.sh

# Currency resolution only — uses cached wiki data (~3 min)
./tools/refresh-vendor-data.sh --pass2-only
```

The script builds the tool, runs the appropriate passes, and prints a summary with file size and offer count.

## Two-Pass Architecture

A full refresh takes ~15 minutes because the GW2 Wiki rate-limits API requests. To keep individual runs manageable and allow recovery from interruptions, the tool splits work into two passes:

**Pass 1 — Wiki scrape** (`--skip-item-resolution`):
Queries all vendor items from the wiki via Semantic MediaWiki `action=ask`. Saves raw results to `ref/wiki_vendor_cache.json` (merges with any existing cache). Generates a partial `ref/vendor_offers.json` without item-based currency resolution.

**Pass 2 — Currency resolution** (`--resolve-item-currencies-only`):
Loads the cached wiki results from `ref/wiki_vendor_cache.json`. Resolves item-based currency names (e.g. "Mystic Coin", "Glob of Ectoplasm") to GW2 game IDs by querying the wiki. Generates the final `ref/vendor_offers.json`.

If Pass 1 is interrupted (safety limit, rate-limit block, timeout), the wiki cache preserves all partial results. Re-running Pass 1 merges new results into the existing cache. Once the cache is complete, Pass 2 can be run independently.

## Prerequisites

- .NET 8 SDK
- Internet access (no API key needed — both endpoints are public)
- Git Bash on Windows (for the wrapper script)

## CLI Reference

```bash
dotnet run --project tools/VendorOfferUpdater/VendorOfferUpdater.csproj -- [options] [output-path]
```

The tool auto-detects the repository root by walking up the directory tree looking for a `.git` folder, then writes to `ref/vendor_offers.json` relative to that root. Pass an explicit path as the first positional argument to override.

### Options

| Flag | Default | Description |
|------|---------|-------------|
| `--skip-item-resolution` | off | Skip item-based currency resolution; generate partial output and save wiki cache |
| `--resolve-item-currencies-only` | off | Load wiki cache instead of scraping; resolve currencies and generate final output |
| `--query <condition>` | `[[Sells item::+]]` | Override the SMW query condition (e.g. `[[Has vendor::"Miyani"]]`) |
| `--max-depth <n>` | 2 | Max prefix partition depth for SMW queries |
| `--max-requests <n>` | 2000 | Safety limit on total HTTP requests |
| `--max-runtime <minutes>` | 30 | Safety limit on total execution time |
| `--delay <ms>` | 250 | Delay between wiki API requests (minimum enforced: 200 ms) |
| `--dry-run` | off | Print query plan only, no HTTP calls to wiki |
| `--tag-seasonal-festivals` | off | Fetch each distinct vendor page's wikitext and tag offers whose page carries a `{{Temporary\|...\|seasonal=}}`/`{{Temporary\|...\|event=}}` value matching one of the six known GW2 festivals. Opt-in: adds one extra HTTP request per distinct, not-yet-cached vendor page (see `--max-seasonal-pages`) |
| `--max-seasonal-pages <n>` | 500 | Safety limit on how many new (uncached) vendor pages `--tag-seasonal-festivals` will fetch in one run |

### Environment Overrides (wrapper script)

The `refresh-vendor-data.sh` script accepts these environment variables:

| Variable | Default | Used in |
|----------|---------|---------|
| `MAX_RUNTIME` | 20 | Pass 1 `--max-runtime` |
| `MAX_REQUESTS` | 2000 | Pass 1 `--max-requests` |
| `DELAY_PASS1` | 250 | Pass 1 `--delay` |
| `DELAY_PASS2` | 1500 | Pass 2 `--delay` |

Example:

```bash
DELAY_PASS1=500 MAX_RUNTIME=30 ./tools/refresh-vendor-data.sh
```

## Data Files

| File | Size | Role |
|------|------|------|
| `ref/vendor_offers.json` | ~13 MB | **Baseline vendor offers** — loaded by the Blish HUD module at runtime. Contains deduplicated, ID-resolved vendor offers. Committed to repo and embedded in the `.bhm` package. |
| `ref/wiki_vendor_cache.json` | ~16 MB | **Wiki query cache** — raw SMW results from Pass 1. Used by Pass 2 for currency resolution. Supports incremental merging across multiple scrape runs. Committed to repo for developer convenience. |
| `ref/item_id_cache.json` | ~40 KB | **Item ID cache** — maps item currency names to GW2 game IDs. Avoids re-resolving known items on subsequent runs. Committed to repo. |
| `ref/seasonal_wikitext_cache.json` | small | **Seasonal festival tag cache** — maps vendor page name to its raw wiki `{{Temporary\|...}}` seasonal/event value (or `""` for "checked, not tagged"). Only populated by `--tag-seasonal-festivals`. Gitignored (dev-local, like `wiki_vendor_cache.json`/`item_id_cache.json`). |

## What It Queries

1. **GW2 API** `/v2/currencies` — loads all currency IDs and names so wiki currency strings (e.g. "Coin", "Volatile Magic") can be mapped to numeric IDs.
2. **GW2 Wiki SMW API** `action=ask` — queries vendor subobject pages (`[[Sells item::+]]`) and pulls:
   - `Sells item.Has game id` — item's GW2 game ID
   - `Sells item` — item page name
   - `Has item quantity` — output count (defaults to 1)
   - `Has item cost` — record type with `Has item value` (amount) and `Has item currency` (name)
   - `Has vendor` — NPC vendor page
   - `Located in` — location pages
   - `Has daily purchase cap` - daily purchase limit (absent = uncapped)
   - `Has weekly purchase cap` - weekly purchase limit (absent = uncapped)
   - `Has seasonal purchase cap` - Wizard's Vault seasonal purchase limit (absent = uncapped or not a Vault offer)

## Rate Limiting

- Configurable delay between wiki requests (default 250 ms, minimum 200 ms).
- **HTTP 403** (wiki rate-limit block): 30-second base cooldown with exponential backoff and jitter, up to 3 retries.
- **HTTP 429 / 5xx**: exponential backoff (1 s / 2 s / 4 s), up to 3 retries. Respects `Retry-After` header.
- Both query and currency resolution methods return partial results on failure rather than losing work.

## Output Schema

```jsonc
{
  "schemaVersion": 1,
  "generatedAt": "2026-02-13T12:34:56.0000000Z",
  "source": "gw2wiki-smw",
  "offers": [
    {
      "offerId": "a1b2c3...",       // SHA-256 hash (deterministic dedup key)
      "outputItemId": 12345,        // GW2 item ID
      "outputCount": 1,             // quantity produced
      "costLines": [                // one or more costs
        { "type": "Currency", "id": 1, "count": 100 }
      ],
      "merchantName": "Miyani"
      // "locations", "dailyCap", "weeklyCap", "seasonalCap" omitted when null
    }
  ]
}
```

Offers are deduplicated by `offerId` and sorted alphabetically. Null fields are omitted from the output.

## Exit Codes

| Code | Meaning | Action |
|------|---------|--------|
| 0 | Success — offers written | Commit updated `ref/vendor_offers.json` |
| 1 | Error (network failure, unexpected exception) | Check error message; retry if transient |
| 2 | Safety limit exceeded (max requests or max runtime) | Partial results saved to wiki cache. Increase `--max-runtime` or `--max-requests` and re-run |
| 130 | Cancelled (Ctrl+C) | Partial results may be saved to wiki cache |

## When to Re-run

- **Game patches** that add new vendors, items, or currencies
- **Wiki updates** when the community documents new or corrected vendor data
- **Periodically** (e.g. quarterly) to pick up gradual wiki improvements
- After modifying the VendorOfferUpdater tool itself, to verify output correctness
- **Each Wizard's Vault season** - the live "Wizard's Vault" page's current-season
  stock/prices/caps can change at a season boundary. See "Wizard's Vault Rotation"
  below for the scoped refresh command.
- **After adding new printouts/fields to `WikiSmwClient`** - Re-running Pass 2 alone
  (`--resolve-item-currencies-only`) against `ref/wiki_vendor_cache.json` reuses the
  old cached `WikiVendorResult` shape and will silently omit the new fields forever;
  a full Pass 1 re-scrape is required to fetch them. Pass 1's cache merge overwrites
  any existing entry for a re-queried `PageName` with the freshly-fetched result (see
  `Program.cs`), so a normal full re-scrape backfills new fields for every page without
  needing to delete `ref/wiki_vendor_cache.json` first. Deleting the cache first is only
  needed if a page must be dropped from the cache entirely (e.g. it no longer resolves
  on the wiki) rather than refreshed.

## Wizard's Vault Rotation

The Wizard's Vault (Astral Acclaim) reward store spans three distinct wiki
pages, all captured by our scrape under three distinct `merchantName` values.
The naming itself already tells you which "kind" of page an offer came from -
no separate field is needed to distinguish them:

| `merchantName` | What it is | Rotation behavior |
|---|---|---|
| `Wizard's Vault` | The **current season's** live store. | Overwritten each season - prices/caps/stock for THIS page can change at a season boundary. This is the page that carries `seasonalCap` for capped rows (e.g. Mystic Coin 60/season, Mystic Clover 20/season). |
| `Wizard's Vault/Historical Astral Rewards` | Wiki-maintained archive of **past seasons'** rotated rewards (mostly one-off cosmetics/unlocks that have since left the live page). | Grows over time; a row appearing here does not mean it's currently purchasable. |
| `Wizard's Vault/Legacy Rewards` | Cosmetics that rotated out of the live store into a permanent "Legacy" tab. | Grows over time; separate from the seasonal-cap system entirely (no `seasonalCap` values observed on this page). |

**Confirmed live (2026-07-22):** `Has seasonal purchase cap` is used
*exclusively* by pages under this `merchantName` prefix wiki-wide - but
only TWO of the three pages actually carry it: a `[[Has seasonal
purchase cap::+]]` probe with no vendor filter returned 29 rows total,
split between `Wizard's Vault` and `.../Historical Astral Rewards` only
(zero on `.../Legacy Rewards`, consistent with that page's "separate
from the seasonal-cap system entirely" note in the table above). No
other vendor on the wiki uses this property, so those 29 rows are the
complete set that exists to seed.

**Known wiki-documentation quirk (Bag of Coins tiering):** the two-tier
"Bag of Coins (1 Gold)" item is represented as two *separate* wiki item
pages/game-IDs, not one row with two prices. As of this writing, only the
discount tier ("Bag of Coins (1 Gold) (limited)", 8 AA, seasonal cap 100)
appears as a `{{vendor table row}}` on the live `Wizard's Vault` page; the
continuation tier ("Bag of Coins (1 Gold) (unlimited)", 35 AA, uncapped) is
currently only machine-readable via the `Wizard's Vault/Historical Astral
Rewards` page's subobjects, even though it is still a live, purchasable deal
in-game. Both rows are seeded (from their respective pages) so this doesn't
lose data, but a from-scratch reader expecting the "unlimited" tier to be
tagged with the live merchant name will not find it there - this is a wiki
editorial gap, not a scraper bug. See `docs/research/aa-tier-findings.md` for
the full investigation.

**To refresh Wizard's Vault data for a new season** (scoped, keeps the diff
reviewable - mirrors the `--query`/`--merge-into` pattern from KNOWN-ISSUES.md
item 24's Homestead Refinement seeding):

```bash
dotnet run --project tools/VendorOfferUpdater/VendorOfferUpdater.csproj -- \
  --query "[[Has vendor::~Wizard's Vault*]]" \
  --merge-into ref/vendor_offers.json \
  ref/vendor_offers.json
```

`~Wizard's Vault*` is a prefix wildcard match on `Has vendor` that covers all
three page names above and nothing else (verified: `Wizard's
Gobbler`/`Portable Wizard's Tower Exchange`, which share the "Wizard's" word
but not the "Wizard's Vault" prefix, are not matched), and leaves every
other merchant's offers byte-for-byte unchanged - but matching the wildcard
pattern is not the same as coming back with rows to merge. The 2026-07-22
seeding pass's run of this exact command only actually returned/replaced
offers for `Wizard's Vault` and `.../Historical Astral Rewards`;
`.../Legacy Rewards` came back with no rows that run and its existing
offers passed through untouched (see KNOWN-ISSUES.md item 33). Do not
assume this command refreshes all three merchants every time it is run -
check which merchant names actually changed in the resulting diff.

**Stale-offer sweep status:** no automated stale-offer detector exists for
Wizard's Vault specifically (the general sweep is manual - see
KNOWN-ISSUES.md item 28). If/when that tooling is built, the `merchantName`
convention above ("Wizard's Vault" = current, `.../Historical Astral Rewards`
= archived, `.../Legacy Rewards` = rotated-out cosmetics) is sufficient to
tell current-season offers apart from historical ones without any new code -
a sweep should only ever treat `Wizard's Vault` (current) rows as
"unexpectedly missing => investigate," never the historical/legacy pages,
which are expected to retain old rows indefinitely.
