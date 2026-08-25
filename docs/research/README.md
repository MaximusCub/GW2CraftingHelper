# Research notes

This directory holds dev-time research reports, not shipped documentation
and not code. Each one records an investigation into how
[gw2efficiency](https://gw2efficiency.com)'s crafting calculator or the
[GW2 Wiki](https://wiki.guildwars2.com/) behaves, so this module's own
logic can deliberately echo that behavior instead of inventing its own.

## Research-only sources policy

This is the same hard rule stated in `CONTRIBUTING.md` and the project's
own coding rules, restated here because it is the entire reason this
directory exists:

- **gw2efficiency is never called by the running module.** Its source
  (`recipe-calculation`, `recipe-nesting`, and related packages) and its
  live calculator frontend are fetched read-only, at dev time, purely to
  document normative behavior. No gw2efficiency API, script, or bundle is
  ever requested at runtime by `GW2CraftingHelper.dll`.
- **The GW2 Wiki is scraped offline only**, by the tools under `tools/`
  (`VendorOfferUpdater`, `MysticForgeSeeder`), never by the running module.
  The wiki-derived data those tools produce (`ref/vendor_offers.json`,
  `ref/mystic_forge_recipes.json`) ships as static, committed seed files.
- No data in these reports is invented. Where a source could not be
  recovered or a value could not be confirmed, the report says so
  explicitly rather than guessing.

## What's here

- `m37-r1-homestead.md` - Homestead Refinement mechanics (gw2e's
  `cheapestTree.ts` tier modeling, wiki conversion tables) - KNOWN-ISSUES #24.
- `m37-r2-batch-economics.md` - multi-item sell-side economics (Cost
  Breakdown rollup, profit/loss banner) - KNOWN-ISSUES #25.
- `m37-r3-achievement-dedup.md` - achievement-bit ingredient deduplication -
  KNOWN-ISSUES #26.
- `m37-r4-vendor-caps.md` - vendor purchase-cap SMW properties and the
  stale-offer-sweep methodology - KNOWN-ISSUES #28.
- `aa-tier-findings.md` - Wizard's Vault (Astral Acclaim) tiered-pricing
  investigation - KNOWN-ISSUES #33.
- `gw2e-convergence-matrix.md` - the five-area, 58-row comparison of this
  module against gw2efficiency, with per-row measured/inferred evidence
  and EQUIVALENT/PRESERVE/ADOPT verdicts. The evidence trail both
  `docs/gw2e-parity-spec.md` and `docs/gw2e-considerations.md` cite by row
  number; an active work queue, not history.
- `gw2e-currency-decision-prices.md` - verbatim extraction of gw2e's
  64-key `CURRENCY_DECISION_PRICES` table from the live bundle and the
  TypeScript source, plus the id-mapping proof. The provenance record
  behind the maintainer's one-time waiver of the no-invented-data rule
  (`Models/CurrencyDecisionDefaults.cs`).
- `minimum-window-width.md` - how wide the window has to be for the
  deepest recipe tree in the game to render untruncated (exact graph
  depths from the committed seeds, text widths measured from the
  installed Menomonia bitmap fonts). Basis for the 1436 -> 1478 -> 1378
  minimum; section 9 carries the depth-14 realistic-worst-case
  re-derivation the shipped 1378 comes from. The one report here that
  studies this module's own layout rather than an external source.

Moved out: `m37-r5-audits.md` (the cross-thread/degradation/thread-safety
audit findings, KNOWN-ISSUES #31) is a closed point-in-time audit and now
lives at [`docs/archive/m37-r5-audits.md`](../archive/m37-r5-audits.md).

The normative gw2efficiency behavior spec these reports feed into lives one
level up, at [`docs/gw2e-parity-spec.md`](../gw2e-parity-spec.md); the
durable "why" these reports produced for the module's own implementation
lives at [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md). For the full
resolution history of each numbered item above, see
[`docs/dev-notes/HISTORY.md`](../dev-notes/HISTORY.md).

## A note on provenance paths

Some of these reports were originally written referencing files fetched
into a session-scoped scratch directory (outside this repo, and long
gone). Where that happened, the report has been updated to cite the
reproducible source instead (a URL, a commit hash) - none of the actual
research findings or quoted code were changed, only the now-meaningless
local path references.
