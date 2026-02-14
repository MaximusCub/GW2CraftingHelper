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

## What It Queries

1. **GW2 API** `/v2/currencies` — loads all currency IDs and names so wiki currency strings (e.g. "Coin", "Volatile Magic") can be mapped to numeric IDs.
2. **GW2 Wiki SMW API** `action=ask` — queries vendor subobject pages (`[[Sells item::+]]`) and pulls:
   - `Sells item.Has game id` — item's GW2 game ID
   - `Sells item` — item page name
   - `Has item quantity` — output count (defaults to 1)
   - `Has item cost` — record type with `Has item value` (amount) and `Has item currency` (name)
   - `Has vendor` — NPC vendor page
   - `Located in` — location pages

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
      // "locations", "dailyCap", "weeklyCap" omitted when null
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
