> **Milestone record - 2026-08-29, branch `gaeting-equality-pin`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Currency 77 is pinned to currency 28, and two false claims about it are corrected

Branch `w6-gaeting` established that the two "Gaeting Crystal" currency ids
are different goods, removed the retired id 39, and gave the live id 77 a
default value of 3600 - the same figure currency 28, Magnetite Shard, carries,
because the only live offer priced in crystals is a 1-for-1 exchange into
shards. That work is recorded in
[`gaeting-crystal-duplicate-ids.md`](gaeting-crystal-duplicate-ids.md). This
branch, merged as pull request 237, is the follow-up: it makes the derivation
enforceable, and it corrects two statements about id 77 that the evidence does
not support. Five files, no behaviour change.

### Id 77's value was derived from id 28 but nothing held them together

`Models/CurrencyDecisionDefaults.cs` carried `{ 28, 3600 }` and `{ 77, 3600 }`
as two independent literals, while id 77's own comment said it must equal what
id 28 is worth. Moving 28 on its own broke no test, so the derivation was a
claim in a comment and nothing more.

`tests/TaimisToolbench.Tests/Models/CurrencyDecisionDefaultsTests.cs` adds
`GaetingCrystal_MatchesTheMagnetiteShardItBuys`, which reads both defaults
through `TryGetDefault` and asserts they are equal. It follows the shape the
file already uses for `CastoranHeroics_MatchesItsDesertAndJadeSiblings`.

Two decisions are recorded at the new test rather than in this record, because
a reader of the file needs them. It is deliberately not a shared constant: id
28 sits in the vendored gw2efficiency block and id 77 in the derived-here
block, which the source file keeps apart and never interleaves, so a shared
constant would make the licensed import a compile-time dependency of the
module's own derivation. The existing value pin on id 77 stays as well:
`docs/ARCHITECTURE.md` section 8.3 makes id 77 a snapshot of one expansion
that is due re-derivation when the next ships, so a hard peg would carry a
future change to 28 into 77 with nobody re-deriving it. The value pin forces
that re-derivation to be a deliberate edit; the new assertion catches silent
drift between the two.

`docs/file-budgets.txt` raises that test file from 135 to 155 lines for the
assertion and the note that has to sit with it.

### "The Path of Fire raid vendor" was the wrong vendor

Section 8.3's row for currency 77 said its offers are priced at the count and
coin "the Path of Fire raid vendor charges in Magnetite Shards". No Path of
Fire vendor is involved. Measured against `ref/vendor_offers.json`: each of
the 80 remaining crystal-priced cost lines has an exact twin - same output
item, same output count, same complete cost line set with currency 28
substituted for 77 - among the Mount Balrior shard-priced offers.
`Raid Expert's Portable Magnetite Shard Exchange/Mount Balrior` carries all
80 and `Scholar Glenna (Mount Balrior)` carries 8 of them. 40 output items are
priced in both currencies at identical counts, with zero divergences. Those
are the same Janthir Wilds raid vendors after the conversion moved them from
crystals to shards, not a Path of Fire vendor. The row is rewritten to say so.

### The table comment had the same defect in the present tense

`Models/CurrencyDecisionDefaults.cs`'s comment on id 77 claimed its vendors
"charge what currency 28 charges", present tense, when the only thing buyable
with currency 77 today is the 1-for-1 Magnetite Shard exchange itself. The
comment now reads that its only live exchange sells 1 Magnetite Shard for 1,
and that its historical tables charged what 28 now charges. The price identity
is between the historical crystal-priced tables and the current shard-priced
tables of the same goods, which is still the evidence for the 1-for-1.

### The earlier record's offer counts were re-measured

Section 5 of `dev/records/gaeting-crystal-duplicate-ids.md` was measured over
a 59,414-offer corpus and reported 110 live cost lines charging item 86094,
all from `Scholar Glenna (Gaeting Crystal)`, flagged there as a data defect to
fix later. Branch `w5-deadvendors` added that merchant to
`ref/vendor_offer_exclusions.json`, so the corpus is now 59,244 offers and
that row is zero. The section was re-measured against `ref/vendor_offers.json`
rather than edited from the old figures; currency 39 (0), currency 77 (82
total, 80 historical, 2 live) and item 104026 (0) all still hold. The section
now states that its numbers are as of a date and a commit, because every one
of them moves the next time the corpus is rebuilt.

Gate: not required - the only module source change is a comment, so no runtime
behaviour moves. What the branch adds is a test assertion, a corrected
architecture row and a re-measured record. Nothing to look at in game.
