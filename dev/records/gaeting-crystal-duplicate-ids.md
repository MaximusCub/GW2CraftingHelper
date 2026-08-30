> **Milestone record - 2026-08-29, branch `w6-gaeting`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Two "Gaeting Crystal" currency ids: one is retired (gaeting-crystal-duplicate-ids)

Answers FEEDBACK-BACKLOG item R1: two wallet currency ids, 39 and 77, both
carry the name "Gaeting Crystal", and the Settings valuation grid drew them
as two identically-labelled rows.

**Answer: currency 39 is retired. It was withdrawn from the game on
2022-07-19 and no account can hold it. Currency 77 is the live one. They
are not two forms of one good; they are two different goods that ArenaNet
gave the same name eight years apart.**

---

### 1. The two API responses, verbatim

`https://api.guildwars2.com/v2/currencies/39`, fetched 2026-08-29:

```json
{
  "id": 39,
  "name": "Gaeting Crystal",
  "description": "Earned from bosses and events inside Path of Fire raids.",
  "order": 317,
  "icon": "https://render.guildwars2.com/file/B31002F9772C0DDF7428A7A16BD14945C34AB4A8/1896039.png"
}
```

`https://api.guildwars2.com/v2/currencies/77`, fetched 2026-08-29:

```json
{
  "id": 77,
  "name": "Gaeting Crystal",
  "description": "Earned from bosses and events inside Janthir Wilds raids. Used at specific raid vendors in the Janthir region.",
  "order": 116,
  "icon": "https://render.guildwars2.com/file/0C05B2E6F012B209C3095CDD6AF5F84B0BA9CC3A/3442797.png"
}
```

`name` collides. `description`, `order` and `icon` all differ, and the
description is the discriminator: Path of Fire raids versus Janthir Wilds
raids. Neither response carries any "deprecated" or "historical" flag - the
API has no such field for currencies, which is why the id alone cannot
settle this and the wiki had to.

### 2. Currency 39 was retired by a patch, and balances were force-converted

The 2022-07-19 patch notes
(<https://wiki.guildwars2.com/wiki/Game_updates/2022-07-19>) say, verbatim:

> Gaeting Crystals have been retired and players will automatically have any
> Gaeting Crystals in their possession exchanged for an equal amount of
> Magnetite Shards.

> Raids and achievement rewards that previously awarded Gaeting Crystals now
> award Magnetite Shards.

> Merchants who previously traded items for Gaeting Crystals now accept
> Magnetite Shards in their place.

The wiki gives currency 39 its own page,
<https://wiki.guildwars2.com/wiki/Gaeting_Crystal_(historical)>, whose
infobox carries `status = historical` and `status notes = Replaced by
Magnetite Shards with the July 19, 2022 game update`, and whose prose is
entirely past tense ("were a currency earned from killing bosses ... in the
Hall of Chains, Mythwright Gambit, and The Key of Ahdashim raid wings").

The conversion was automatic, account-wide and 1:1, so **no live account
holds currency 39 in any amount**. The brief's caution about not removing a
valuation while held balances might still exist does not apply here; there
are none by construction.

### 3. Currency 77 is live, and is a rolling per-expansion currency

<https://wiki.guildwars2.com/wiki/Gaeting_Crystal> (no disambiguator - the
current one owns the plain title) describes it as the raid currency of "the
most recent expansion", introduced with the Janthir Wilds raid (Mount
Balrior). Its own mechanic repeats what killed id 39:

> Upon the release of a new expansion, all collected Gaeting Crystals are
> converted to Magnetite Shards, and the vendors are changed to accept the
> converted currency.

That has already happened once to id 77: the page now lists Visions of
Eternity encounters (Guardian's Glade, 12 crystals) as the source, and flags
a live game bug that the id-77 description still names Janthir Wilds even
though the Janthir vendors already moved to Magnetite Shards. Id 77 was NOT
retired by that transition - it was re-pointed at the new expansion's raids,
which is precisely why ArenaNet needed a second id in 2024 rather than
reusing 39: 39 had already been drained and its vendors converted.

**Thesis for why two ids exist, and it is the strongest reading of the
evidence above, not a certainty:** "Gaeting Crystal" is a *role*, not a
good - the current expansion's raid token, deliberately drained to Magnetite
Shards each time a new expansion lands. Id 39 held that role for Path of
Fire and was retired outright in 2022 when raids consolidated on Magnetite
Shards. Id 77 was allocated fresh in 2024 when Janthir Wilds brought raids
back, and now carries the role forward. Reusing id 39 would have resurrected
a currency the client had already told every player was gone.

### 4. The item forms: 86094 pairs with 39, not with 77

`https://api.guildwars2.com/v2/items?ids=86094,104026`, fetched 2026-08-29:

| item | name | icon | pairs with |
|---|---|---|---|
| 86094 | Gaeting Crystal | `.../B31002F9772C0DDF7428A7A16BD14945C34AB4A8/1896039.png` | currency **39** - byte-identical icon URL |
| 104026 | Gaeting Crystal | `.../C8933D93BBFEF7A1102A6FCC285D140923ABD651/3442796.png` | currency **77** |

Both are `"details": {"type": "Currency"}` consumables (the wallet-deposit
item form). The wiki infoboxes agree: the historical page carries
`id = 86094`, the current page carries `id = 104026, requires = jw`.

This corrects a claim that was in the source before this branch.
`Models/BarterItemDecisionDefaults.cs` stated that item 86094 and currencies
39 and 77 were "the same in-game good in item and wallet form" and therefore
"cannot disagree". Two of the three are the same good (86094 and 39); the
third is a different good whose item form, 104026, this module does not
carry at all.

### 5. Vendor-offer counts in `ref/vendor_offers.json` (59,414 offers)

Measured 2026-08-29 over the shipped corpus:

| cost line | offers | of which on a `/historical` merchant | live |
|---|---|---|---|
| currency 39 | **0** | - | **0** |
| currency 77 | 82 | 80 | **2** |
| item 86094 | 110 | 0 | 110 (but see below) |
| item 104026 | 0 | - | 0 |

- **Currency 39 is charged by zero offers of any kind.** Independent
  confirmation of the retirement: the wiki scrape found nothing anywhere
  that still takes it.
- **Currency 77's 80 historical offers** come from
  `Scholar Glenna (Mount Balrior)/historical` (40) and
  `Titan Specialist Tante/historical` (40) - the Janthir vendors the wiki
  says already switched to Magnetite Shards.
- **Currency 77's 2 live offers** are the same offer from both merchants:
  1 Gaeting Crystal buys 1 Magnetite Shard (item 77378). That 1:1 exchange
  is the derivation behind valuing id 77 at 3600, the same figure currency
  28 (Magnetite Shard) carries. The 2022 conversion of id 39 was also 1:1
  into Magnetite Shards, so 3600 is the correct figure for id 39 too - the
  two values agreeing is a coincidence of both being pegged to the same
  shard, not evidence that the ids are the same thing.

**Separate data defect found while counting, not fixed here.** All 110
offers charging item 86094 come from one merchant, `Scholar Glenna (Gaeting
Crystal)`, which the corpus carries with no `/historical` suffix and which
the module therefore treats as live. Its wiki page
(<https://wiki.guildwars2.com/wiki/Scholar_Glenna_(Gaeting_Crystal)>) opens
"This page lists the items formerly available from Scholar Glenna ... The
following offers were removed and replaced with equivalent offers", and its
infobox carries `status = historical`. The SMW scrape misses it because the
page marks itself historical in the infobox rather than in the page title,
which is exactly the gap `ref/vendor_offer_exclusions.json` exists to cover
(`tools/VendorOfferUpdater/Program.cs`, step 5c). 110 dead offers over 10
output items are being ranked as purchasable. Recommend a follow-up on the
vendor-data branch; out of scope for a research task.

### 6. `/v2/account/wallet` semantics

**Not established, and it does not change the conclusion.** The endpoint
requires an API key (<https://wiki.guildwars2.com/wiki/API:2/account/wallet>)
and none was available here. The documented response is an array of
`{id, value}` and the documented example is sparse (ids 1 and 5, skipping
2, 3 and 4), which *suggests* the wallet omits currencies the account has no
record of - **inferred from the example, not confirmed**.

It does not matter for this question for two reasons. First, the 2022 patch
converted every id-39 balance to Magnetite Shards, so whether the wallet
omits id 39 or reports it as 0, no user has a nonzero amount. Second, and
decisively, **the Settings valuation grid is not built from the wallet at
all**: `Views/SettingsTabContent.BuildCuratedCurrencyIds` enumerates
`CurrencyDecisionDefaults.DefaultCopperPerUnit.Keys` plus four hard-coded
ids. A row appears because the module carries a default for that id, not
because the player holds any. That is why the duplicate row was visible to
an owner who cannot possibly hold currency 39.

### 7. Sweep: is "Gaeting Crystal" the only duplicated row label?

Yes. Every label the grid can draw was enumerated - `Gw2Constants.
KnownCurrencyNames` resolved over the currency row ids, plus every
`BarterItemDecisionDefaults` name - on this branch (73 rows) and on the
unmerged `w5-currvals` branch that adds id 77 (78 rows). "Gaeting Crystal"
is the **only** duplicated string on either. On `w5-currvals` it is a
three-way collision: currency 39, currency 77, and item 86094.

`BarterItemDecisionDefaultsTests` already has a tripwire for exactly this
failure ("Two curated rows share a display name, which would render as two
indistinguishable Settings rows"), but it only checks the barter table
against itself, so it could never have caught a currency-vs-currency or
currency-vs-item collision. **Proposed, not built** (a new test is outside a
research task's remit): widen that assertion to the full row set the grid
actually draws - currency ids resolved through `Gw2Constants` plus the
barter names - so the next same-name currency pair fails a build instead of
reaching a player. The sweep above measured that such a test passes today
with the renames in this branch applied.

---

### What changed on this branch

Label only, plus comment corrections. No valuation was added or removed:
both 3600 figures are correct (section 5) and removing id 39's entry would
also remove its Settings row, taking away the user's ability to see and
clear a default that is still applied to every solve.

| file | change |
|---|---|
| `Models/Gw2Constants.cs` | id 39 renamed to `"Gaeting Crystal (historical)"` |
| `Models/BarterItemDecisionDefaults.cs` | item 86094 renamed to `"Gaeting Crystal (historical)"`; the false "three forms cannot disagree" comment replaced with the 86094/39 pairing |
| `Models/CurrencyDecisionDefaults.cs` | comment at `{ 39, 3600 }` records the retirement and points here |
| `tests/.../BarterItemDecisionDefaultsTests.cs` | the same false claim, restated in a test comment, corrected |
| `Services/CurrencyTooltipFacts.cs`, `Views/Rendering/ItemIconTooltip.cs` | the same false claim, restated in two doc comments, corrected |

"Gaeting Crystal (historical)" is not invented: it is the exact title of the
wiki page for both id 39 and item 86094. Anywhere a resolved name is turned
into a wiki URL through `WikiLinkBuilder`, that now lands on the retired
currency's page rather than the current one's; the Settings grid itself
builds no wiki links, so this is a side benefit, not the reason for the
rename.

The rename intentionally leaves currency 39 and item 86094 sharing a label.
They ARE one good in two id spaces (section 4), they render in two different
Settings sections, and that pairing predates this branch. The collision the
owner reported - two *different* goods under one name - is gone.

### Merge note for whoever lands `w5-currvals`

Currency 77's valuation entry lives on the unmerged branch `w5-currvals`
(commit 6ce6fe3), which this branch cannot see. That commit also rewrites
the comment on the `{ 39, 3600 }` line in `CurrencyDecisionDefaults.cs`, and
so does this branch, so **that one line will conflict**. Take this branch's
text. `w5-currvals` also adds `{ 77, "Gaeting Crystal" }` to
`Gw2Constants.KnownCurrencyNames`; leave that name alone - id 77 is the live
currency and owns the plain name. Its inline comment there reads "Gaeting
Crystal (Janthir Wilds raids)", which section 3 shows is already stale
in-game; "current expansion's raid currency" is the accurate description.
