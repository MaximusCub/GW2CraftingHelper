> **Milestone record - 2026-08-25, branch `vendor-refresh`.** Moved verbatim out of the append zone in `docs/KNOWN-ISSUES.md` by the 2026-08-25 rotation.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Vendor data refresh, and the stale row it tried to ship (vendor-refresh)

The maintainer authorised running the vendor updater unattended - "i have
never run it by hand or supervised it before so i will not be planning to
start now. if you have concerns about it.. do work to mitigate your
concerns first" - so the concerns were mitigated first, and one of them
turned out to be real.

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
items the maintainer reported as UNKNOWN, and the one this data could
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


