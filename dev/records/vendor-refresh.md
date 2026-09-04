> **Milestone record - 2026-08-25, branch `vendor-refresh`.** Moved verbatim out of the append zone in `docs/KNOWN-ISSUES.md` by the 2026-08-25 rotation.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Vendor data refresh, and the stale row it tried to ship (vendor-refresh)

The vendor updater is run unattended, so any concern about running it that
way is mitigated before the run rather than watched for during it. The
concerns were mitigated first, and one of them turned out to be real.

**Method.** Dedicated branch; the 13MB baseline copied outside the repo
before anything ran; the WRAPPER script rather than a manual invocation,
because tools/VendorOfferUpdater/README.md is explicit that a manual pass
without `--tag-seasonal-festivals` and `--merge-into` wholesale-replaces
the dataset. The local wiki cache was gone, so both passes re-scraped from
scratch (~15 min, rate-limited).

**Result, measured against the backup rather than trusted.** 53,544 ->
59,414 offers. By CONTENT (item, count, merchant, cost lines) only 254 of
51,679 distinct offers disappeared, and every single one had all-null cost
lines - rows the previous pass could not resolve and which no consumer
could price. 581 items became purchasable that were not before, including
**Gift of the Survivors (106712) from Castaway Agnes** - one of the four
items reported in the field as UNKNOWN, and the one this data could
actually fix (it is a Visions of Eternity vendor item, not a recipe).
Seasonal tags went 57 -> 597.

**The defect the refresh introduced, and the repo's own trip-wire that
caught it.** `AcquisitionHintSeedVendorAgreementTests` pins WHICH items
carry both a hand-verified acquisition hint and a shipped vendor offer,
with the comment "a fourth arriving unnoticed is worth a manual look". A
fourth arrived: item 19678, **Gift of Battle**, newly scraped as sold by
`Battle Master` in Eternal Battlegrounds for 500 Badges of Honor. This
repo's own hint for that item, hand-verified 2026-07-20, says that vendor
path "was removed in the Spring 2016 Quarterly Update". The wiki still
describes the sale on a page that is not marked historical, so the scrape
ingested it. Shipping it would have priced a legendary component from a
decade-dead vendor and quietly changed every legendary plan that uses it.

**Fix, as data rather than heuristics.** New `ref/vendor_offer_exclusions.json`
refuses rows by (merchant, item) with a reason, a source URL and a
verification date; the tool applies it after the merge, so neither a fresh
scrape nor a surviving baseline row can reintroduce one. Three tests in
VendorOfferUpdater.Tests pin that a refused row is dropped, that the
refusal is keyed on BOTH merchant and item (the genuine Gift of Battle
path must never be caught by it), and that a missing or corrupt list is
survivable rather than fatal. With the row excluded the agreement
trip-wire passes unchanged - the population is back to the three recorded
items.

**A second finding, recorded because it invalidates a stated premise.**
OfferId is NOT stable across a full re-scrape. A pinned row
(Drojkor, Spirit Squall / item 84618) came back with byte-identical item,
count, cost lines, merchant and location under a different hash, because
`VendorOfferHasher`'s own doc comment says a recompute appends hash
segments (homesteadTier, seasonalCap) that the committed baseline predates
- ids only stayed stable while `--merge-into` copied untouched baseline
objects through. Two tests pinned literal hashes on the premise that only
a content change could move them; both now key on content, which is what
they meant.

Gate: not required - dev-tool and data change. Both suites green (module
2650, tool 210), and the loss analysis above is the verification a desktop
gate could not add.



## Addendum, 2026-08-28: a re-reported "missing vendor data" gap that is not one

A follow-up asked for the refresh again, on the premise that Gift of the
Survivors (106712), Gift of the People (105804) and Gift of the Hylek
(106986) are absent from every shipped seed and that a
`tools/VendorOfferUpdater` + wiki-cache refresh was the outstanding fix.
Both halves of that premise are wrong, and no wiki traffic was spent on
re-testing them.

**The rows were never missing.** All three are present, with fully
resolved cost lines, in every committed `ref/vendor_offers.json` back to
the first vendor seeding commit (1956466). Compared field by field against
the pre-refresh baseline at `a39ec81^`, the three rows differ from today's
only in `offerId` - the hash migration recorded above. So the 2026-08-25
refresh did not add them either, and this record's own claim that 106712
was among the "581 items [that] became purchasable" is an artifact of that
migration rather than a data change.

**A refresh cannot fix them, because the gap is not in the data.** The
mechanism is the one already written up in `dev/records/field-fixes-3.md`:
`VendorBatchSolver.EvaluateVendorOffers` sets `priceable = false` and
discards the whole offer when any `Item` cost line has no Trading Post
price, and these are barter offers paid in account-bound tokens that can
never have one. The open follow-up named there - surface the discarded
offer's merchant and location on the node instead of hand-typing them into
`ref/acquisition_hints_seed.json` - remains the only thing that closes
this class. Re-counted against the current 59,414-offer dataset: 19,763
offers carry an `Item` cost line, and 4,885 of 15,544 distinct output
items have no offer free of one (an upper bound on the class - a barter
item that is tradable still prices).

**What the re-verification did turn up** was a defect in the review
artifact itself, since fixed: see `tools/VendorOfferUpdater/README.md` on
the `rehashed` classification. Run against this record's own refresh, the
old summary called 48,750 of 53,544 rows repriced with identical before
and after, and reported `retagged: 0` for the run that took seasonal tags
from 57 to 597. The mandated diff was at its least trustworthy on exactly
the run it was written to review.
