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
```

The tool auto-detects the repository root by walking up the directory tree looking for a `.git` folder, then writes to `ref/vendor_offers.json` relative to that root. Pass an explicit path as the first argument to override.

## What it queries

1. **GW2 API** `/v2/currencies` — loads all currency IDs and names so wiki currency strings (e.g. "Laurel", "Gold Coin") can be mapped to numeric IDs.
2. **GW2 Wiki SMW API** `action=ask` — queries for items that have both a game ID and at least one vendor, pulling:
   - `Has game id` — item ID
   - `Sold by` — merchant NPC name(s)
   - `Has vendor cost` — numeric price
   - `Has vendor currency` — currency name
   - `Has vendor quantity` — output count (defaults to 1)

   Wiki query: `[[Has game id::+]][[Sold by::+]]`, paginated in batches of 500.

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
