# gw2efficiency CURRENCY_DECISION_PRICES - Research Extraction

Research-only. No repository files were modified by the extraction run this document
describes. Originally written to a per-session scratchpad directory; committed here
under `docs/research/` (currency-ux-package review fix, finding 8) so the provenance
citations in `Models/CurrencyDecisionDefaults.cs` and `docs/KNOWN-ISSUES.md` resolve for
anyone reading the code, not only within the authoring session.

Every claim below is labeled **CONFIRMED** (directly observed in fetched/read bytes),
**INFERRED** (reasoned from confirmed facts but not directly observed), or **NOT FOUND**.

---

## 1. The complete table, verbatim

### 1a. As compiled into the live production bundle (first-hand, this run)

**CONFIRMED.** Extracted from `application.js`, a copy of which was already present at
`scratchpad/gw2e/application.js` from an earlier fetch. Freshness was verified first-hand this
run: `curl -sI "https://gw2efficiency.com/scripts/application.js?cb=1"` returned
`etag: W/"6a7c925c-407446"` and `last-modified: Wed, 12 Aug 2026 15:33:48 GMT`. The etag's hex
suffix `407446` = **4224070 bytes**, which is byte-for-byte the size of the cached local copy
(`wc -c` = 4224070). The cached file is therefore confirmed identical to the file currently
served in production - no re-download was needed and no stale-cache risk exists.

The table is a single minified object literal, found at three reference sites and one
definition site (see section 4 for full byte offsets and surrounding code):

```js
t.CURRENCY_DECISION_PRICES={1:1,2:1,3:3500,4:3e3,5:32,6:32,7:80,9:32,10:32,11:32,12:32,13:32,
14:32,15:23,16:3600,18:void 0,19:70,20:70,22:70,23:3600,24:1200,25:100,26:800,27:45,28:3600,
29:3600,30:void 0,31:50,32:25,33:1600,34:9,35:720,36:135,37:void 0,38:void 0,39:3600,40:void 0,
41:void 0,42:void 0,43:void 0,44:void 0,45:50,46:void 0,47:void 0,49:void 0,50:25,51:void 0,
52:void 0,53:3500,54:void 0,55:void 0,56:void 0,57:300,58:void 0,59:void 0,60:310,61:200,
62:100,64:35,65:135,67:35,68:320,69:32,70:void 0}
```

(`void 0` is minified JS for `undefined`; `4:3e3` is minified `4:3000`; `24:1200` is the
folded result of the TypeScript source's `24: 15 * 80` - i.e. the minifier evaluated the
constant expression rather than dropping it.)

This is a plain JS object: no gaps in the byte stream, no truncation, 64 keys total. It is
keyed by **gw2efficiency's own currency id**, which - confirmed in section 2 below - is
numerically identical to the official GW2 API's `id` field for every one of these 64 entries.

### 1b. As authored in TypeScript source (independently cross-checked, live, this run)

**CONFIRMED.** `curl`'d live (this run) from
`https://raw.githubusercontent.com/gw2efficiency/recipe-calculation/master/src/static/currencyDecisionPrices.ts`
(HTTP 200). Byte-for-byte identical in content to the pre-existing cached copy at
`scratchpad/gw2e/static_currencyDecisionPrices.ts`, and semantically identical to 1a (same 64
keys, same values, `24: 15 * 80` unevaluated here since this is un-minified source):

```ts
export const CURRENCY_DECISION_PRICES: Record<number, number | undefined> = {
  1: 1, // Gold
  2: 1, // Karma
  3: 3500, // Laurel
  4: 3000, // Gem
  5: 32, // Ascalonian Tear
  6: 32, // Shard of Zhaitan
  7: 80, // Fractal Relic
  9: 32, // Seal of Beetletun
  10: 32, // Manifesto of the Moletariate
  11: 32, // Deadly Bloom
  12: 32, // Symbol of Koda
  13: 32, // Flame Legion Charr Carving
  14: 32, // Knowledge Crystal
  15: 23, // Badge of Honor
  16: 3600, // Guild Commendation
  18: undefined, // Transmutation Charge
  19: 70, // Airship Part
  20: 70, // Ley Line Crystal
  22: 70, // Lump of Aurillium
  23: 3600, // Spirit Shard
  24: 15 * 80, // Pristine Fractal Relic
  25: 100, // Geode
  26: 800, // WvW Skirmish Claim Ticket
  27: 45, // Bandit Crest
  28: 3600, // Magnetite Shard
  29: 3600, // Provisioner Token
  30: undefined, // PvP League Ticket
  31: 50, // Proof of Heroics
  32: 25, // Unbound Magic
  33: 1600, // Ascended Shards of Glory
  34: 9, // Trade Contract
  35: 720, // Elegy Mosaic
  36: 135, // Testimony of Desert Heroics
  37: undefined, // Exalted Key
  38: undefined, // Machete
  39: 3600, // Gaeting Crystal
  40: undefined, // Bandit Skeleton Key
  41: undefined, // Pact Crowbar
  42: undefined, // Vial of Chak Acid
  43: undefined, // Zephyrite Lockpick
  44: undefined, // Trader's Key
  45: 50, // Volatile Magic
  46: undefined, // PvP Tournament Voucher
  47: undefined, // Racing Medallion
  49: undefined, // Mistborn Key
  50: 25, // Festival Token
  51: undefined, // Cache Key
  52: undefined, // Red Prophet Shard
  53: 3500, // Green Prophet Shard
  54: undefined, // Blue Prophet Crystal
  55: undefined, // Green Prophet Crystal
  56: undefined, // Red Prophet Crystal
  57: 300, // Blue Prophet Shard
  58: undefined, // War Supplies
  59: undefined, // Unstable Fractal Essence
  60: 310, // Tyrian Defense Seal
  61: 200, // Research Note
  62: 100, // Unusual Coin
  64: 35, // Jade Sliver
  65: 135, // Testimony of Jade Heroics
  67: 35, // Canach Coins
  68: 320, // Imperial Favor
  69: 32, // Tales of Dungeon Delving
  70: undefined, // Legendary Insight
}
```

**Units:** all non-`undefined` values are **copper**. Confirmed by usage site
`src/calculateTreePrices.ts` (`decisionPriceEach = ... CURRENCY_DECISION_PRICES[tree.id]`,
then multiplied straight into `decisionPrice`/`buyPrice`, which are copper amounts elsewhere in
the same file/package). `undefined` means "gw2efficiency assigns this currency no decision
value" - it is excluded from cost comparisons entirely, not valued at 0.

Compiled 1a and hand-authored 1b are the **same table** (1a is what actually ships to users;
1b is its un-minified origin). Both were independently confirmed live this run.

---

## 2. Mapping to the official GW2 API currency ids

**CONFIRMED.** Fetched live this run:
`curl -s "https://api.guildwars2.com/v2/currencies?ids=all&v=2022-03-23"` -> HTTP 200, 79
currency objects, saved to `scratchpad/gw2e-currencies-api.json`.

gw2efficiency's `CURRENCY_DECISION_PRICES` is keyed directly by the **official GW2 wallet
currency id** - not a gw2efficiency-internal id. Every one of the 64 keys in the table is a
valid, present `id` in the live API response. Matching was done purely by numeric id (not by
name), then cross-checked against gw2e's own inline `// comment` name for that id vs. the
live API's `name` field for the same id, as a sanity check on the id/name pairing itself.

| id | gw2e value (copper) | gw2e comment | Live API `name` | Match |
|---|---|---|---|---|
| 1 | 1 | Gold | **Coin** | Same currency, different label (see note below) |
| 2 | 1 | Karma | Karma | match |
| 3 | 3500 | Laurel | Laurel | match |
| 4 | 3000 | Gem | Gem | match |
| 5 | 32 | Ascalonian Tear | Ascalonian Tear | match |
| 6 | 32 | Shard of Zhaitan | Shard of Zhaitan | match |
| 7 | 80 | Fractal Relic | Fractal Relic | match |
| 9 | 32 | Seal of Beetletun | Seal of Beetletun | match |
| 10 | 32 | Manifesto of the Moletariate | Manifesto of the Moletariate | match |
| 11 | 32 | Deadly Bloom | Deadly Bloom | match |
| 12 | 32 | Symbol of Koda | Symbol of Koda | match |
| 13 | 32 | Flame Legion Charr Carving | Flame Legion Charr Carving | match |
| 14 | 32 | Knowledge Crystal | Knowledge Crystal | match |
| 15 | 23 | Badge of Honor | Badge of Honor | match |
| 16 | 3600 | Guild Commendation | Guild Commendation | match |
| 18 | undefined | Transmutation Charge | Transmutation Charge | match (no value either way) |
| 19 | 70 | Airship Part | Airship Part | match |
| 20 | 70 | Ley Line Crystal | Ley Line Crystal | match |
| 22 | 70 | Lump of Aurillium | Lump of Aurillium | match |
| 23 | 3600 | Spirit Shard | Spirit Shard | match |
| 24 | 1200 (15*80) | Pristine Fractal Relic | Pristine Fractal Relic | match |
| 25 | 100 | Geode | Geode | match |
| 26 | 800 | WvW Skirmish Claim Ticket | WvW Skirmish Claim Ticket | match |
| 27 | 45 | Bandit Crest | Bandit Crest | match |
| 28 | 3600 | Magnetite Shard | Magnetite Shard | match |
| 29 | 3600 | Provisioner Token | Provisioner Token | match |
| 30 | undefined | PvP League Ticket | PvP League Ticket | match |
| 31 | 50 | Proof of Heroics | Proof of Heroics | match |
| 32 | 25 | Unbound Magic | Unbound Magic | match |
| 33 | 1600 | Ascended Shards of Glory | Ascended Shards of Glory | match |
| 34 | 9 | Trade Contract | Trade Contract | match |
| 35 | 720 | Elegy Mosaic | Elegy Mosaic | match |
| 36 | 135 | Testimony of Desert Heroics | Testimony of Desert Heroics | match |
| 37 | undefined | Exalted Key | Exalted Key | match |
| 38 | undefined | Machete | Machete | match |
| 39 | 3600 | Gaeting Crystal | Gaeting Crystal | match, but **this row is deliberately not imported** - see 3c |
| 40 | undefined | Bandit Skeleton Key | Bandit Skeleton Key | match |
| 41 | undefined | Pact Crowbar | Pact Crowbar | match |
| 42 | undefined | Vial of Chak Acid | Vial of Chak Acid | match |
| 43 | undefined | Zephyrite Lockpick | Zephyrite Lockpick | match |
| 44 | undefined | Trader's Key | Trader's Key | match |
| 45 | 50 | Volatile Magic | Volatile Magic | match |
| 46 | undefined | PvP Tournament Voucher | PvP Tournament Voucher | match |
| 47 | undefined | Racing Medallion | Racing Medallion | match |
| 49 | undefined | Mistborn Key | Mistborn Key | match |
| 50 | 25 | Festival Token | Festival Token | match |
| 51 | undefined | Cache Key | Cache Key | match |
| 52 | undefined | Red Prophet Shard | Red Prophet Shard | match |
| 53 | 3500 | Green Prophet Shard | Green Prophet Shard | match |
| 54 | undefined | Blue Prophet Crystal | Blue Prophet Crystal | match |
| 55 | undefined | Green Prophet Crystal | Green Prophet Crystal | match |
| 56 | undefined | Red Prophet Crystal | Red Prophet Crystal | match |
| 57 | 300 | Blue Prophet Shard | Blue Prophet Shard | match |
| 58 | undefined | War Supplies | War Supplies | match |
| 59 | undefined | Unstable Fractal Essence | Unstable Fractal Essence | match |
| 60 | 310 | Tyrian Defense Seal | Tyrian Defense Seal | match |
| 61 | 200 | Research Note | Research Note | match |
| 62 | 100 | Unusual Coin | Unusual Coin | match |
| 64 | 35 | Jade Sliver | Jade Sliver | match |
| 65 | 135 | Testimony of Jade Heroics | Testimony of Jade Heroics | match |
| 67 | 35 | Canach Coins | Canach Coins | match |
| 68 | 320 | Imperial Favor | Imperial Favor | match |
| 69 | 32 | Tales of Dungeon Delving | Tales of Dungeon Delving | match |
| 70 | undefined | Legendary Insight | Legendary Insight | match |

**Result: 63 of 64 entries map cleanly and unambiguously by id.** The one non-identical label
is id 1: gw2e's source comment calls it "Gold" while the API's formal `name` is "Coin" - this
is the same currency (the base gold/silver/copper wallet currency); the comment is a
colloquialism, not a mismatch. No entry in the table failed to resolve to a live API id, and
no entry pointed at the wrong currency.

**No entries needed name-based matching** - every gw2e key is already the correct numeric API
id, so there was no ambiguity to resolve by name.

---

## 3. Entries needing care

### 3a. Currencies our module already surfaces in Settings

Read from `Models/Gw2Constants.cs` (`KnownCurrencyNames`, ids used by `CurrencyValuation`),
**CONFIRMED** by direct file read (no edits made):

| id | Module's label | gw2e decision value | Notes |
|---|---|---|---|
| 2 | Karma | 1 copper | present |
| 3 | Laurels | 3500 copper | present |
| 4 | Gems | 3000 copper | present |
| 5 | Ascalonian Tears | 32 copper | present |
| 6 | Shards of Zhaitan | 32 copper | present |
| 7 | Fractal Relics | 80 copper | present |
| 9 | Seals of Beetletun | 32 copper | present |
| 10 | Manifesto of the Moletariate | 32 copper | present |
| 11 | Deadly Blooms | 32 copper | present |
| 12 | Symbols of Koda | 32 copper | present |
| 13 | Flame Legion Charr Carvings | 32 copper | present |
| 14 | Knowledge Crystals | 32 copper | present |
| 15 | Badges of Honor | 23 copper | present |
| 16 | Guild Commendations | 3600 copper | present |
| 18 | Transmutation Charges | **undefined** | gw2e assigns no value |
| 19 | Airship Parts | 70 copper | present |
| 20 | Ley Line Crystals | 70 copper | present |
| 22 | Lumps of Aurillium | 70 copper | present |
| 23 | Spirit Shards | 3600 copper | present |
| 24 | Pristine Fractal Relics | 1200 copper | present |
| 25 | Geodes | 100 copper | present |
| 26 | WvW Skirmish Claim Tickets | 800 copper | present |
| 27 | Bandit Crests | 45 copper | present |
| 28 | Magnetite Shards | 3600 copper | present |
| 29 | Provisioner Tokens | 3600 copper | present |
| 30 | PvP League Tickets | **undefined** | gw2e assigns no value |
| 32 | Unbound Magic | 25 copper | present |
| 33 | Ascended Shards of Glory | 1600 copper | present |
| 34 | Trade Contracts | 9 copper | present |
| 36 | **"Elegy Mosaics"** (module's label) | 135 copper | **id/name mismatch in our module - see 3b** |
| 45 | Volatile Magic | 50 copper | present |
| 47 | Racing Medallions | **undefined** | gw2e assigns no value |
| 49 | **"Festival Tokens"** (module's label) | **undefined** | **id/name mismatch in our module - see 3b** |
| 50 | **"Mistborn Motes"** (module's label) | 25 copper | **id/name mismatch in our module - see 3b** |
| 58 | **"Jade Slivers"** (module's label) | **undefined** | **id/name mismatch in our module - see 3b** |
| 59 | **"Research Notes"** (module's label) | **undefined** | **id/name mismatch in our module - see 3b** |
| 60 | **"Imperial Favors"** (module's label) | 310 copper | **id/name mismatch in our module - see 3b** |
| 62 | Unusual Coins | 100 copper | present |
| 63 | Astral Acclaim | **not in gw2e's table at all** (no key, not even `undefined`) | gw2e's table has no row for this id - it postdates or was never added |
| 78 | Fine Rift Essence | **not in gw2e's table at all** | gw2e's table stops at id 70; ids 78/79/80 have no row |
| 79 | Rare Rift Essence | **not in gw2e's table at all** | " |
| 80 | Masterwork Rift Essence | **not in gw2e's table at all** | " |

**Straightforward answer for the decision-table import:** of the module's
currently-surfaced ids, gw2e supplies a usable (non-`undefined`) decision price for
2, 3, 4, 5, 6, 7, 9, 10, 11, 12, 13, 14, 15, 16, 19, 20, 22, 23, 24, 25, 26, 27, 28, 29, 32, 33,
34, 45, 62, plus the id-36/49/50/58/59/60 slots once correctly relabeled (see 3b - the gw2e
*values* at those ids are fine, only our module's *display names* are wrong).
gw2e has **no value** for 18, 30, 47, and (correctly, at their real ids) 49/58/59.
gw2e has **no row at all** for 63 (Astral Acclaim) and 78/79/80 (Fine/Rare/Masterwork Rift
Essence) - these currencies are simply absent from their table, not zero-valued.

### 3b. Independent finding, out of scope to fix here: our module's `KnownCurrencyNames` has wrong id-to-name pairings

**CONFIRMED**, found incidentally while cross-checking 3a against the live API - **this is a
bug in our own module's data, unrelated to gw2e, and was NOT fixed (research-only task, no
repo edits)**. `Models/Gw2Constants.cs` pairs several ids with the wrong currency name relative
to the live GW2 API:

| id | Module says | Live API says this id actually is | The name the module used actually belongs to id |
|---|---|---|---|
| 36 | Elegy Mosaics | Testimony of Desert Heroics | 35 (missing from module entirely) |
| 49 | Festival Tokens | Mistborn Key | 50 |
| 50 | Mistborn Motes | Festival Token | 49 (and "Mistborn Motes" isn't the API's name anywhere - the real item is "Mistborn Key") |
| 58 | Jade Slivers | War Supplies | 64 (missing from module entirely) |
| 59 | Research Notes | Unstable Fractal Essence | 61 (missing from module entirely) |
| 60 | Imperial Favors | Tyrian Defense Seal | 68 (missing from module entirely) |

Pluralization-only differences (e.g. module's "Shards of Zhaitan" vs API's singular "Shard of
Zhaitan") are cosmetic and not flagged here - only cases where the module's id points at a
*different currency entirely* are listed. Recommend tracking this as a separate
follow-up; it affects `ResolveCurrencyName` display text and would also affect which currency
a default decision value gets seeded for at those ids if this table is
imported id-for-id without noticing the mislabeling.

### 3c. Duplicate currency name in the live API (not a gw2e or module issue - a GW2 API quirk)

**CONFIRMED.** The live API has *two different currency ids* both named "Gaeting Crystal":
id 39 (`"Earned from bosses and events inside Path of Fire raids."`) and id 77
(`"Earned from bosses and events inside Janthir Wilds raids... "`). gw2e's table only has a
row for id 39 (3600 copper); id 77 has no row (postdates their table). If the import
logic ever resolves gw2e prices by *name* instead of id, id 39 and id 77 would collide -
another reason id-keyed import (as gw2e itself does) is the only safe approach.

**Resolved 2026-08-29: id 39 is deliberately NOT imported.** It is the only row in gw2e's
table that this module drops on purpose, so the gap is a divergence and not drift. The
currency was retired in-game on 2022-07-19 and every held balance force-converted into
Magnetite Shards (id 28), so no account can hold one; and no offer in `ref/vendor_offers.json`
charges it, so it cannot reach a solve even if one could. Its item form, item 86094, is
dropped from `Models/BarterItemDecisionDefaults.cs` for the same reason. Id 77, the live
Gaeting Crystal, is a rolling currency whose worth resets each expansion - what that means for
any valuation of it is stated once in `docs/ARCHITECTURE.md` section 8.3, with the measured
evidence in `dev/records/gaeting-crystal-duplicate-ids.md`.

### 3d. `id` 74 is a malformed/placeholder entry in the live API

**CONFIRMED.** `id: 74` in the live API response has `"name": ""` and `"description": ""`
(both empty strings) but reuses the exact same icon URL as id 63 (Astral Acclaim):
`.../1856A01E331452E4C14E4C9CF4F818E3FAEF9B79/3124964.png`. This is almost certainly a stray/
placeholder id on ArenaNet's side, not a real currency - **Astral Acclaim's real, correctly-
named id is 63**, which is what the module already uses (confirmed correct - the "74?" guess
that prompted this check does not match the live API and should be disregarded).

### 3e. Removed/legacy currencies in gw2e's table absent from the live API

**CONFIRMED: none.** Every one of the 64 ids in `CURRENCY_DECISION_PRICES` (section 1) is
present in the current live API response. The live API is also missing ids 8, 17, 21, and 48
entirely (never allocated, or removed before this snapshot) - and gw2e's table independently
skips exactly those same four ids too, with no explanation needed: the table simply never had
rows for ids that don't exist. There is no evidence of gw2e carrying a stale/removed currency.

### 3f. Currencies added to the live API that postdate gw2e's table (informational)

**CONFIRMED** present in the live API but with **no row at all** in gw2e's table (ids 71-83,
plus 63 and 66 noted above): 63 Astral Acclaim, 66 Ancient Coin, 71 Jade Miner's Keycard,
72 Static Charge, 73 Pinch of Stardust, 75 Calcified Gasp, 76 Ursus Oblige, 77 Gaeting Crystal
(second one, see 3c), 78 Fine Rift Essence, 79 Rare Rift Essence, 80 Masterwork Rift Essence,
81 Antiquated Ducat, 82 Testimony of Castoran Heroics, 83 Aether-Rich Sap. For full
current-content coverage, these ids will need locally-chosen defaults or will
simply remain unvalued (consistent with the repo invariant "do not invent data when APIs are
missing" - gw2e itself doesn't invent values for these either, it just doesn't mention them).

---

## 4. Provenance

### 4a. Byte-offset locations in `application.js` (4,224,070 bytes)

**CONFIRMED**, found via `grep -bo` byte-offset search this run:

- **Offset 627082** (and again, overlapping, at 627116 from a wider grep window): the
  **definition site** -
  `t.CURRENCY_DECISION_PRICES=void 0,t.CURRENCY_DECISION_PRICES={1:1,2:1,3:3500,...70:void 0}`
  - immediately preceded by `Object.defineProperty(t,"__esModule",{value:!0})` (a standard
  webpack/TypeScript-compiled ES-module interop stub), and immediately followed by the start of
  a sibling module beginning `t.DAILY_COOL...` (i.e. `DAILY_COOLDOWNS`, a neighboring static
  table from the same `@gw2efficiency/recipe-calculation` package's `src/static/` directory).
  This module is webpack module index-numbered `n(187)` at its call sites.
- **Offset 626393**: a **usage site**, inside the compiled `calculateTreePrices` function:
  `l=i&&"number"==typeof i[t.id]?i[t.id]:r.CURRENCY_DECISION_PRICES[t.id]` - this is the
  compiled form of `src/calculateTreePrices.ts`'s `customCurrencyPrices` override logic (a
  caller-supplied override wins per-id; otherwise `CURRENCY_DECISION_PRICES` is the fallback).
  `r` is the module reference to `n(187)` (the same module defined at offset 627082).
- **Offset 2072318**: a second **usage site**, inside a different bundled entry point
  (`tradingpost-fees`-adjacent code, judging by the surrounding `profit_buy`/`profit_sell`/
  trading-post-fee-percentage `.85` multiplier logic): `const d=n(187).CURRENCY_DECISION_PRICES`.

All three usage/definition sites resolve to the same webpack module id (`187`), confirming a
single canonical table, not several independently-drifted copies.

### 4b. Cross-check against the live GitHub source (this run)

**CONFIRMED.** `curl`'d live this run:
`https://raw.githubusercontent.com/gw2efficiency/recipe-calculation/master/src/static/currencyDecisionPrices.ts`
returned HTTP 200 with content identical to section 1b, which is in turn semantically identical
(same 64 keys/values, modulo minification) to the compiled bundle table in section 1a. Three
independent artifacts (locally-cached compiled bundle, live-refetched compiled bundle via HEAD
etag/last-modified match, live-refetched TS source) all agree.

### 4c. License

**CONFIRMED, for the specific package that is the source of this table (not the whole site):**

- `curl`'d live this run: `https://raw.githubusercontent.com/gw2efficiency/recipe-calculation/master/package.json`
  -> `"license": "MIT"`.
- `curl`'d live this run: GitHub API `contents` endpoint for the repo root ->
  a file named **`LICENCE`** (British spelling, root of repo) exists, decoded content is the
  standard MIT License text, copyright line: `Copyright (c) 2016 queicherius (David Reeß)`.
- GitHub's own repository-license API classification (returned inline with the `contents`
  response) independently tags it `"license": {"key": "mit", "name": "MIT License", ...}`.
- `curl`'d live this run: `https://api.github.com/orgs/gw2efficiency/repos` -> the
  `gw2efficiency` GitHub org publishes several small calculation/data packages, **all tagged
  MIT**: `scraping`, `recipe-nesting`, `item-attributes`, `recipe-calculation`, `item-value`,
  `tradingpost-fees`, `account-value`, `playerbase-statistics`, `static-data`, `api-status`.
  Two repos (`issues`, `game-data`) show no license.

**What this does NOT establish (explicitly checked, not assumed):**

- **NOT FOUND**: any license notice for the `application.js` bundle itself, or for
  gw2efficiency's main site/frontend source. Searched `application.js` for
  copyright/license banners (`grep -bo`); every hit found is a **third-party** dependency's own
  bundled license header (classnames - MIT, Jed Watson 2018; object-assign - MIT, Sindre
  Sorhus; lodash - MIT, OpenJS Foundation; React 16.14.0 - MIT, Facebook; AngularJS 1.4.14 -
  MIT, Google). None of these banners are first-party gw2efficiency copyright notices, and no
  first-party banner was found anywhere in the 4.2 MB file.
- **NOT FOUND**: a public GitHub repo for the gw2efficiency.com frontend itself. The org's
  public repo list (fetched live, listed above) contains only calculation/data-library repos,
  no site/frontend repo - consistent with the site's own frontend source being closed/private.
- **Conclusion (INFERRED from the above two NOT-FOUND results):** the MIT license covers the
  `@gw2efficiency/recipe-calculation` **npm package** (and therefore the specific
  `CURRENCY_DECISION_PRICES` data structure within it, which is what this document extracts)
  as published to GitHub/npm. It does **not** follow that the gw2efficiency.com web
  application as a whole, or `application.js` as a compiled artifact, is under any open
  license - that is a separate, unconfirmed question this research did not find evidence to
  answer either way, and the warning not to assume main-site-source = MIT is correct:
  no such assumption is made here.

---

## Summary

- The table is genuinely complete: 64 currency ids, each keyed by the real GW2 API currency
  id, values in copper, `undefined` meaning "gw2e assigns no decision value" (not zero).
- It maps essentially perfectly to the live API (63/64 identical name match; the 64th, id 1,
  is the same currency under a colloquial vs. formal label - "Gold" vs. "Coin").
- Our module's Karma/Laurels/Spirit Shards/Gems/etc. (most of `KnownCurrencyNames`) get usable
  gw2e values directly. Astral Acclaim (63) and all three Rift Essences (78/79/80) have **no**
  gw2e value at all (table doesn't reach that far) and will need a locally-chosen default
  or remain unvalued.
- Independently discovered and flagged (not fixed): our own `Gw2Constants.KnownCurrencyNames`
  has 5-6 ids paired with the wrong currency name (36, 49, 50, 58, 59, 60) - worth a follow-up
  ticket separate from this import.
