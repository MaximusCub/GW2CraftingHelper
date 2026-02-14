# VendorOfferUpdater

Offline tool that scrapes vendor-sold items from the [GW2 Wiki](https://wiki.guildwars2.com/) Semantic MediaWiki API, resolves currency names via the official [GW2 API](https://api.guildwars2.com/), and writes a `vendor_offers.json` baseline file consumed by the Blish HUD module.

## Prerequisites

- .NET 8 SDK
- Internet access (no API key needed — both endpoints are public)

## Build & Run

```bash
# Default output → ref/vendor_offers.json (auto-detected via repo root)
dotnet run --project tools/VendorOfferUpdater/VendorOfferUpdater.csproj

# Custom output path
dotnet run --project tools/VendorOfferUpdater/VendorOfferUpdater.csproj /tmp/vendor_offers.json

# Query a specific vendor for validation
dotnet run --project tools/VendorOfferUpdater/VendorOfferUpdater.csproj -- --query '[[Has vendor::"Miyani"]]'
```

The tool auto-detects the repository root by walking up the directory tree looking for a `.git` folder, then writes to `ref/vendor_offers.json` relative to that root. Pass an explicit path as the first positional argument to override.

### `--query <condition>`

Overrides the default SMW query condition (`[[Sells item::+]]`) with a custom one. Useful for validating specific vendors without crawling the entire wiki:

```bash
dotnet run -- --query '[[Has vendor::"Assassin"]]'
```

## What it queries

1. **GW2 API** `/v2/currencies` — loads all currency IDs and names so wiki currency strings (e.g. "Coin", "Volatile Magic") can be mapped to numeric IDs.
2. **GW2 Wiki SMW API** `action=ask` — queries vendor subobject pages (`[[Sells item::+]]`) and pulls:
   - `Sells item.Has game id` — item's GW2 game ID (property chain)
   - `Sells item` — item page name
   - `Has item quantity` — output count (defaults to 1)
   - `Has item cost` — record type with `Has item value` (amount) and `Has item currency` (name)
   - `Has vendor` — NPC vendor page
   - `Located in` — location pages

   Paginated in batches of 500.

## Rate limiting

- **1 second** delay between wiki requests.
- **Exponential backoff** on HTTP 429 or 5xx: 1 s → 2 s → 4 s (up to 3 retries).
- Respects the `Retry-After` header when present (uses the longer of backoff or header value).

## Output schema

```jsonc
{
  "schemaVersion": 1,
  "generatedAt": "2026-02-13T12:34:56.0000000Z",  // ISO 8601 UTC
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

Offers are deduplicated by `offerId` and sorted alphabetically. Null fields (`locations`, `dailyCap`, `weeklyCap`) are omitted from the output.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success — offers written |
| 1 | Error (network failure, unexpected exception) |
| 130 | Cancelled (Ctrl+C) |

## When to re-run

Re-run periodically when vendor data on the wiki changes. The Blish HUD module loads `ref/vendor_offers.json` as its baseline vendor dataset at startup; committing an updated file keeps the module's offline data current.
