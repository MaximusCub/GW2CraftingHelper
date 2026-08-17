#!/usr/bin/env bash
# refresh-vendor-data.sh - Refreshes the vendor offers baseline data.
#
# Usage:
#   ./tools/refresh-vendor-data.sh              # Full refresh (pass 1 + pass 2)
#   ./tools/refresh-vendor-data.sh --pass2-only # Currency resolution only (uses cached wiki data)
#   ./tools/refresh-vendor-data.sh --help       # Print usage
#
# Environment overrides (optional):
#   MAX_RUNTIME   Max wiki scrape time in minutes  (default: 20)
#   MAX_REQUESTS  Max HTTP requests for wiki scrape (default: 2000)
#   DELAY_PASS1   Delay between wiki requests in ms (default: 250)
#   DELAY_PASS2   Delay between resolution requests (default: 1500)
#
# Requires: .NET 8 SDK, Git Bash on Windows, internet access.
# jq is optional - used for offer count in the summary if available.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

PROJ="tools/VendorOfferUpdater/VendorOfferUpdater.csproj"
CACHE="ref/wiki_vendor_cache.json"
OUTPUT="ref/vendor_offers.json"

# --merge-into requires the target file to already exist (Program.cs
# errors out otherwise) - normally true, since $OUTPUT is a baseline file
# committed to the repo, but guard against a from-scratch bootstrap (no
# prior baseline at all) falling back to a plain first-time generate
# instead of hard-failing. Computed fresh (not once up front) so that if
# Pass 1 itself bootstraps $OUTPUT into existence, Pass 2 still merges
# into it rather than repeating the no-baseline case.
merge_flags() {
    if [[ -f "$OUTPUT" ]]; then
        printf '%s\n%s\n' --merge-into "$OUTPUT"
    fi
}

# --- Argument parsing ---

PASS2_ONLY=false

for arg in "$@"; do
    case "$arg" in
        --pass2-only)
            PASS2_ONLY=true
            ;;
        --help|-h)
            echo "Usage: $0 [--pass2-only] [--help]"
            echo ""
            echo "Refreshes ref/vendor_offers.json by scraping the GW2 Wiki."
            echo ""
            echo "  (no flags)     Full refresh: wiki scrape + currency resolution (~15 min)"
            echo "  --pass2-only   Skip wiki scrape; resolve currencies from cached wiki data (~3 min)"
            echo "  --help         Print this message and exit"
            echo ""
            echo "Environment overrides:"
            echo "  MAX_RUNTIME=${MAX_RUNTIME:-20}    Max wiki scrape time in minutes"
            echo "  MAX_REQUESTS=${MAX_REQUESTS:-2000}  Max HTTP requests for wiki scrape"
            echo "  DELAY_PASS1=${DELAY_PASS1:-250}   Delay between wiki requests (ms)"
            echo "  DELAY_PASS2=${DELAY_PASS2:-1500}  Delay between resolution requests (ms)"
            exit 0
            ;;
        *)
            echo "ERROR: Unknown argument: $arg" >&2
            echo "Run '$0 --help' for usage." >&2
            exit 1
            ;;
    esac
done

# --- Prerequisites ---

if ! command -v dotnet &>/dev/null; then
    echo "ERROR: dotnet not found. Install the .NET 8 SDK." >&2
    exit 1
fi

if [[ ! -f "$PROJ" ]]; then
    echo "ERROR: Project file not found: $PROJ" >&2
    echo "Run this script from the repository root or via tools/refresh-vendor-data.sh" >&2
    exit 1
fi

# --- Build ---

echo "=== Building VendorOfferUpdater (Release) ==="
dotnet build "$PROJ" -c Release
echo ""

# --- Pass 1: Wiki scrape ---

if [[ "$PASS2_ONLY" == false ]]; then
    # Data-safety fix (2026-08-19): --tag-seasonal-festivals re-derives
    # seasonal tags from the live wiki, and --merge-into "$OUTPUT" (self-
    # merge against the file this same run is about to overwrite) applies
    # Program.MergeIntoBaseline's tag-preserving collision rules - without
    # both, this pass wholesale-replaced ref/vendor_offers.json with an
    # untagged fresh scrape, silently dropping every previously-tagged
    # festival-vendor row. See tools/VendorOfferUpdater/README.md's
    # "Seasonal Tag Preservation" section for the full explanation.
    echo "=== Pass 1: Wiki scrape (--skip-item-resolution --tag-seasonal-festivals) ==="
    mapfile -t MERGE_FLAGS < <(merge_flags)
    dotnet run --project "$PROJ" -c Release --no-build -- \
        --skip-item-resolution \
        --tag-seasonal-festivals \
        "${MERGE_FLAGS[@]+"${MERGE_FLAGS[@]}"}" \
        --max-runtime "${MAX_RUNTIME:-20}" \
        --max-requests "${MAX_REQUESTS:-2000}" \
        --delay "${DELAY_PASS1:-250}" \
        "$OUTPUT"
    echo ""
else
    echo "=== Skipping Pass 1 (--pass2-only) ==="
    if [[ ! -f "$CACHE" ]]; then
        echo "ERROR: Wiki cache not found: $CACHE" >&2
        echo "Run a full refresh first (without --pass2-only) to generate it." >&2
        exit 1
    fi
    echo "Using existing cache: $CACHE"
    echo ""
fi

# --- Pass 2: Currency resolution ---

# Data-safety fix (2026-08-19): --merge-into "$OUTPUT" here for the same
# reason as Pass 1 above - Pass 2 recomputes offers from the wiki cache
# (which already carries Pass 1's resolved seasonal tags) and would
# otherwise wholesale-overwrite $OUTPUT with only this run's fresh set,
# with no protection for a merchant this run's own currency/game-id
# resolution happened to miss.
echo "=== Pass 2: Currency resolution (--resolve-item-currencies-only) ==="
mapfile -t MERGE_FLAGS < <(merge_flags)
dotnet run --project "$PROJ" -c Release --no-build -- \
    --resolve-item-currencies-only \
    "${MERGE_FLAGS[@]+"${MERGE_FLAGS[@]}"}" \
    --delay "${DELAY_PASS2:-1500}" \
    "$OUTPUT"
echo ""

# --- Summary ---

echo "=== Summary ==="

if [[ -f "$OUTPUT" ]]; then
    FULL_PATH="$(cd "$(dirname "$OUTPUT")" && pwd)/$(basename "$OUTPUT")"
    FILE_SIZE=$(wc -c < "$OUTPUT" | tr -d '[:space:]')
    echo "Output:     $FULL_PATH"
    echo "File size:  $FILE_SIZE bytes"

    if command -v jq &>/dev/null; then
        OFFER_COUNT=$(jq '.offers | length' "$OUTPUT")
        echo "Offers:     $OFFER_COUNT"
    else
        echo "jq not found; skipping offer count."
    fi
else
    echo "WARNING: Output file not found: $OUTPUT" >&2
fi

echo ""
echo "Done."
