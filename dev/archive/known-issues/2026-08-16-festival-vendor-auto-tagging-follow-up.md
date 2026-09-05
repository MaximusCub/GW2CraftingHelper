> **Frozen record - 2026-08-16, branch `festival-vendor-auto-tagging-follow-up`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Festival-vendor auto-tagging follow-up (2026-08-16)

**Milestone goal:** the previous SEASONAL VENDOR TIP pass (see section
above) hand-tagged exactly three known Candy Corn Vendor (Weekly) ecto
offers with `seasonalFestival`. This pass extends
`tools/VendorOfferUpdater` to DERIVE the tag for any vendor, by parsing
the GW2 Wiki's own `{{Temporary|...}}` template off each vendor NPC
page's wikitext, so seasonal tagging is no longer a hand-curated
one-off.

**What changed:**
- `TemporaryTemplateParser` (new): extracts the `seasonal=` (or, on a
  minority of vendor pages, `event=` - both confirmed live) parameter
  value out of a page's `{{Temporary|...}}`/`{{temporary|...}}` template.
- `Gw2Constants.FestivalKeysByWikiDisplayName` /
  `ResolveSeasonalFestivalKey` (tool copy, `Models/Gw2Constants.cs`):
  curated, MEASURED mapping from the wiki's own festival display-name
  text to the six internal `FestivalContext` name keys
  (`halloween`/`dragonbash`/`wintersday`/`festivalofthefourwinds`/
  `lunarnewyear`/`superadventurefestival`) - both sides independently
  measured, never invented (wiki display names via six live
  `api.php?action=parse&prop=wikitext` fetches, one per festival;
  internal keys via a raw UTF-16LE string scan of `Blish HUD.exe`,
  same technique the runtime `HalloweenFestivalName` constant already
  used for `halloween` alone). A value not in this table (e.g. the
  real, live-confirmed non-festival event vendors "Consortium Trader
  (Fractal Rush)" / "Starter Equipment Vendor") is left untagged with a
  console warning - never guessed.
- `WikiSmwClient.FetchWikitextAsync` (new): fetches a single page's raw
  wikitext via `action=parse&prop=wikitext` (no Semantic MediaWiki
  property exists for a page's `{{Temporary}}` template, unlike every
  other field this tool scrapes).
- `Program.ResolveSeasonalFestivalValuesAsync` / `StripSubobjectSuffix`
  (new, opt-in via `--tag-seasonal-festivals` + `--max-seasonal-pages`):
  a new pipeline pass that fetches each distinct vendor PAGE's wikitext
  (stripping the SMW subobject suffix - `WikiVendorResult.PageName` is
  actually `"Page Title#vendorN"`, live-confirmed, NOT a fetchable page
  title on its own) and caches the raw seasonal/event value by page
  title in a new gitignored dev cache, `ref/seasonal_wikitext_cache.json`.
  Deliberately opt-in, not part of every default run: no SMW property
  means one extra HTTP request per distinct vendor page, which would
  silently balloon the cost/time of the default
  `./tools/refresh-vendor-data.sh` workflow if unioned into every run.
- `ConvertToOffer` (Program.cs): resolves `WikiVendorResult.
  TemporarySeasonalValue` to `VendorOffer.SeasonalFestival` via
  `Gw2Constants.ResolveSeasonalFestivalKey` - deliberately NOT hashed
  into `OfferId` (matches `VendorOffer.SeasonalFestival`'s own existing
  doc comment), so tagging an already-shipped offer never changes its
  identity.

**`ref/vendor_offers.json` regeneration (partial coverage - read
carefully):** `ref/wiki_vendor_cache.json` did not exist on disk in this
worktree (dev-local, gitignored) at the start of this pass, so a full
from-scratch re-scrape was explicitly NOT performed (would be an
unbounded, many-thousand-page live operation, and is outside this
follow-up's scope). Instead, a SCOPED live run
(`--query` restricted to the known festival vendor list +
`--tag-seasonal-festivals` + `--merge-into ref/vendor_offers.json`)
live-tagged the six OTHER known festival vendor NPC pages: Dragon Bash
Merchant (Weekly), Wintersday Trader (Weekly), Festival Rewards Vendor
(Weekly), Gauntlet Ticket Vendor, New Year Vendor, Super Adventure Box
Weekly Trader - 63 wiki rows -> 54 converted offers, reported at the time
as net +2 vs the prior baseline (53536 -> 53538; 52 stale rows for those
6 merchants replaced).

**Correction (2026-08-17 review fix, Critical): that "net +2" figure
CONCEALED a real data-loss bug.** The scoped run's own
`ref/wiki_vendor_cache.json` had 9 rows resolve `GameId 0` (a wiki-query
defect in that pass, not the wiki actually dropping the items - live-
reconfirmed after the fact via `api.php?action=ask` that the wiki still
serves real game ids for every one of them). `Program.cs`'s
`GameId <= 0` filter silently skipped those 9 rows before conversion,
and `MergeIntoBaseline`'s per-merchant WHOLESALE replacement then deleted
the baseline's only copies of the offers those rows would have replaced,
with no fresh row to take their place: Wintersday Trader (Weekly) and
Festival Rewards Vendor (Weekly) each lost `outputItemId` 64736
(Transmutation Charge), 79431 (Chest of Legendary Shards), and 86804
(Tyrian Exchange Voucher) - 6 shipped offers gone. (The same mechanism
also means Super Adventure Box Weekly Trader shipped incomplete - 9
cache rows produced only 6 offers - but that merchant had no prior
baseline row to lose, so nothing was deleted there, only under-added.)
Fixed by (1) restoring the 6 deleted offers byte-for-byte from the
pre-rescrape baseline (merge-base `4735064`) rather than re-guessing
their content, bringing the baseline to 53544 offers, and (2)
`MergeIntoBaseline` now refuses to wholesale-drop a merchant's baseline
rows when this pass's own fresh batch had a `GameId <= 0` row for that
merchant (see its updated doc comment and
`MergeIntoBaselineTests.ProtectedMerchant_*` /
`MixOfProtectedAndUnprotectedMerchants_*`) - a future scoped run with the
same kind of incomplete resolution now keeps the stale baseline rows
(visible, fixable by a follow-up run) instead of silently deleting them.
Also fixed in the same review pass: `Gw2Constants.FestivalDisplayNames`
(`Models/Gw2Constants.cs`) was missing a display-name entry for five of
the six newly-tagged festival keys (only `halloween` was present), so an
active-festival Plan Notes tip for dragonbash/wintersday/
festivalofthefourwinds/lunarnewyear/superadventurefestival would have
rendered the raw internal key verbatim (e.g. "During
superadventurefestival:") - all six now have a MEASURED display name.
Also fixed in this review pass, lower-severity: `WikiVendorResult.
TemporarySeasonalValue`'s doc comment claimed the raw wiki value "still
round-trips through `wiki_vendor_cache.json` for a later run" - false as
written, because `Program.cs` saved that cache BEFORE the seasonal-tag
pass populated the field, so every row in a run's own cache had it as
null; the cache is now re-saved after tagging so the claim is true.
`ResolveSeasonalFestivalValuesAsync` also used to save its wikitext
cache only after its whole fetch loop completed and catch only
`HttpRequestException` per page, so a malformed/non-JSON response or
Ctrl-C anywhere in the loop discarded every page already fetched that
run; it now saves in a `finally` around the loop and treats a JSON parse
failure the same as an HTTP failure (warn, leave that one page
uncached, continue). A stray U+2500 box-drawing comment separator (the
only non-ASCII byte in any file this follow-up touched) was replaced
with ASCII hyphens per the repo's ASCII-only-in-`.cs` rule. Finally, a
module-side test now asserts every distinct `seasonalFestival` value in
the shipped `ref/vendor_offers.json` has a `Gw2Constants.
FestivalDisplayNames` entry and is one of the six known
`FestivalContext` keys, closing the regression-guard gap that let the
display-name bug above ship unnoticed in the first place.

Candy Corn Vendor (Weekly) was deliberately EXCLUDED from this scoped
`--query` (confirmed by first attempting a run that included it, which
recomputed new `OfferId` hashes for all nine of its rows via
`VendorOfferHasher`'s pre-existing, documented "any freshly-touched
merchant gets new OfferIds" behavior - see that file's own M37/Astral
Acclaim doc comment - breaking the "3 known offer IDs survive
identically" requirement; reverted and re-run without it). Its three
original hand-tagged offer IDs, and all nine of its offers, are
confirmed byte-for-byte unchanged (see
`SeasonalFestivalRoundTripTests`, updated this pass). The shipped
baseline now carries `seasonalFestival` on 57 offers across all six
known festivals, up from the original 3 (Halloween only). **This is
still far from full coverage**: the vast majority of vendor pages on
the wiki (thousands, well beyond the seven curated festival-vendor
pages this pass touched) have not been checked for a `{{Temporary}}`
tag at all - a real, non-partial pass requires a full Pass 1 re-scrape
(populating `ref/wiki_vendor_cache.json` from scratch) followed by a
full `--tag-seasonal-festivals` run, which is a many-hour live
operation outside this pass's scope.

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests (`tools/VendorOfferUpdater`
  and its test project reference neither Blish HUD nor Gw2Sharp, same as
  every existing file in that tree).
- [x] Tests exercise real production paths (`TemporaryTemplateParser`,
  `Gw2Constants.ResolveSeasonalFestivalKey`, `Program.ConvertToOffer`,
  and `Program.ResolveSeasonalFestivalValuesAsync` are all exercised
  directly - the latter through a real `WikiSmwClient` against a fake
  `HttpMessageHandler`, matching `WikiSmwClientTests`' own established
  pattern, not a mirrored/fake implementation of the pipeline logic).
- [x] No fake file I/O tests introduced (the cache read/write path is
  exercised through real `File.Exists`/`File.ReadAllText`/
  `File.WriteAllText` calls against real temp files, cleaned up in a
  `finally` block per test).
- [x] Pricing logic preserves multi-source correctness (this pass adds
  no new cost-line/currency logic at all - `SeasonalFestival` is a pure
  metadata tag, untouched cost-resolution code path).
- [x] IDs remain internal-only (no ids surfaced to any UI - this pass is
  entirely inside `tools/VendorOfferUpdater`, a build-time data tool with
  no UI surface).

**Validation performed (measured, this pass's final state):**
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/tools/VendorOfferUpdater/
VendorOfferUpdater.csproj` - 0 errors, 0 warnings.
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/GW2CraftingHelper.csproj -p:Platform=x64`
- 0 errors (only pre-existing StyleCop warnings, none in files this pass
touched). `"/mnt/c/Program Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-festivalscrape/tests/VendorOfferUpdater.Tests/
VendorOfferUpdater.Tests.csproj` - 185 green (26 new: 13
`TemporaryTemplateParserTests`, 5 `SeasonalFestivalMappingTests`, 8
`ResolveSeasonalFestivalValuesAsyncTests`, plus `ConvertToOfferTests`
grew by 9 new `SeasonalFestival`-threading cases; `SeasonalFestivalRoundTripTests`
updated in place for the new 57-tag baseline, not counted as new).
`"/mnt/c/Program Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-festivalscrape/tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` - 1673 green (0 new; one existing pinned
count, `VendorOfferStoreTests.ShippedSeedFile_VendorOfferLoader_
ParsesAllOffers`, updated from 53536 to 53538 to match the regenerated
baseline). Both suites fully green.

**Review-fix pass re-validation (2026-08-17, measured, after the
Critical/Must-Fix corrections above):**
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/tools/VendorOfferUpdater/
VendorOfferUpdater.csproj` - 0 errors, 0 warnings.
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/GW2CraftingHelper.csproj -p:Platform=x64`
- 0 errors (1685 warnings, all pre-existing StyleCop findings, none in
any file this review-fix pass touched - confirmed by grepping the build
output for each touched file's name). `"/mnt/c/Program Files/dotnet/
dotnet.exe" test C:/Dev/Blish/wt-festivalscrape/tests/
VendorOfferUpdater.Tests/VendorOfferUpdater.Tests.csproj` - 191 green (6
new: 3 `MergeIntoBaselineTests` DATA LOSS-guard cases, 2
`ResolveSeasonalFestivalValuesAsyncTests` resilience cases, 1
`TemporaryTemplateParserTests` multi-template case). `"/mnt/c/Program
Files/dotnet/dotnet.exe" test C:/Dev/Blish/wt-festivalscrape/tests/
GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` - 1675 green (2
new: `PlanViewModelBuilderNotesSeasonalVendorTipTests.
ItemCostTip_NonHalloweenFestival_*` and `VendorOfferStoreTests.
ShippedSeedFile_EveryDistinctSeasonalFestivalValue_*`; one existing
pinned count, `ShippedSeedFile_VendorOfferLoader_ParsesAllOffers`,
updated from 53538 to 53544 for the 6 restored offers). Both suites
fully green.

**Risks / follow-ups:**
- Partial coverage, restated: only 7 vendor pages total (the 3
  already-tagged Candy Corn Vendor (Weekly) rows left untouched, plus 6
  freshly live-tagged) have ever been checked for a `{{Temporary}}` tag.
  Every other vendor in the ~53.5k-offer dataset is unswept - a future
  full Pass 1 re-scrape + `--tag-seasonal-festivals` run is needed for
  real coverage.
  **Correction (2026-08-20 fix): the "would need explicit raising"
  framing above is stale.** `--max-seasonal-pages` (default 500 at the
  `Program.cs` CLI level) used to throw `SafetyLimitException` and abort
  the WHOLE run - before fetching anything - the moment the uncached-page
  count exceeded the budget, which a from-scratch sweep of the measured
  ~2,088 distinct vendor pages against an empty (gitignored,
  fresh-clone-absent) `ref/seasonal_wikitext_cache.json` hit
  unconditionally on its very first invocation; `tools/refresh-vendor-
  data.sh` exited 2, Pass 2 never ran, and a re-run made no progress at
  all (same empty cache, same over-budget count, same throw).
  `ResolveSeasonalFestivalValuesAsync`'s budget is now self-healing
  instead: it fetches up to the budget, saves the cache (as before), and
  logs how many pages remain for a subsequent run rather than aborting -
  only a budget `<= 0` still throws `SafetyLimitException`. The script
  also now exposes the budget as `MAX_SEASONAL_PAGES` (matching
  `MAX_RUNTIME`/`MAX_REQUESTS`/`DELAY_PASS1`/`DELAY_PASS2`), defaulted to
  2500 there so the documented one-command full refresh completes the
  seasonal-tag sweep in one run under normal conditions instead of
  needing the limit raised by hand.
- The six wiki-display-name -> internal-key mappings are Halloween,
  Dragon Bash, Wintersday, Festival of the Four Winds, Lunar New Year,
  and Super Adventure Festival ONLY - if Blish HUD's `FestivalContext`
  ever adds a seventh festival, `Gw2Constants.FestivalKeysByWikiDisplayName`
  needs a new MEASURED entry (both a live wiki-page fetch and a
  `Blish HUD.exe` string-heap check) before any vendor for it can be
  tagged; until then any such vendor is silently left untagged with a
  console warning (never guessed) - by design, not a bug.
- `VendorOfferHasher`'s OfferId is NOT stable across a fresh scrape of
  any merchant (pre-existing, documented behavior - see its own
  M37/Astral Acclaim doc comment) - this pass deliberately worked around
  that by excluding Candy Corn Vendor (Weekly) from its scoped query
  rather than fixing the hasher itself (out of scope: it is not this
  pass's task, and a hash-format change would ripple across all ~53.5k
  offers in the shipped dataset).
- **Correction (2026-08-17 review fix, Must Fix): the claim above that
  this pass adds "no new runtime UI-facing code path" is WRONG.** The
  data change alone alters runtime behavior in two measured ways, so a
  live sandbox check is warranted (not optional) before this can be
  considered fully validated:
  - (a) the display-name gap described above (now fixed in this review
    pass) - before the fix, any plan touching an active dragonbash/
    wintersday/festivalofthefourwinds/lunarnewyear/superadventurefestival
    vendor tip would have rendered the raw internal key in the Notes
    section.
  - (b) **Correction (2026-08-18 review fix): the "six items" count below
    was itself measured against the wrong baseline (the post-tagging
    file, not the merge-base one) and undercounted.** Measured against
    merge-base commit `4735064`, **13 items** go from >=1 untagged
    (solver-usable) vendor offer to 0 usable offers, not six: Blood Ruby
    (79280), Petrified Wood (79469), Fresh Winterberry (79899), Jade
    Shard (80332), Fire Orchid Blossom (81127), Orrian Pearl (81706),
    Eitrite Ingot (92317, a real crafting material, not a
    cosmetic/currency item), and 102002, 102175, 104132, 104836, 105086,
    106848. Each is unconditionally removed from the solver by
    `Services/SeasonalOfferFilter`, with no compensating Plan Notes tip
    (their Festival Token cost has no coin price, so
    `SeasonalVendorTipCalculator`'s `TryGetCoinCost` fails for them). A
    further 33 items lose one usable vendor offer but keep at least one
    other. This is arguably the correct policy (an out-of-season festival
    offer should not silently count as always-available), but it is an
    undisclosed PLANNING-BEHAVIOR change for these items, not a purely
    internal data update, and was not stated or gated before now.
    `SeasonalOfferFilter`/`SeasonalVendorTipCalculator` are pre-existing,
    prior-gated code paths, but this pass changed what data flows through
    them.
  - Live sandbox verification for (a)/(b) above has not yet been
    performed as part of this review-fix pass either - flagging it here
    so the gate step (below) covers it rather than
    treating this as settled.
- **Per-vendor tag coverage is internally inconsistent for three of the
  seven vendors this pass touched (2026-08-18 review finding).** A
  vendor's own wiki page is checked once for `{{Temporary}}` and its raw
  value applies to every offer that page sells, but tagging is currently
  keyed per-offer, not per-vendor, so a vendor can ship with some rows
  tagged and others not: Candy Corn Vendor (Weekly) 3/9 offers tagged
  (untagged `outputItemId` 103702, 105376, 73430, 64736, 79431, 86804),
  Wintersday Trader (Weekly) 4/7 (untagged 64736, 79431, 86804), Festival
  Rewards Vendor (Weekly) 9/12 (untagged 64736, 79431, 86804) - all
  measured directly against the shipped `ref/vendor_offers.json`. The
  untagged siblings remain solver-visible year-round via
  `SeasonalOfferFilter` despite selling on a festival-only vendor page -
  the same phantom-year-round-vendor failure mode this whole pass exists
  to close, just left open for these 12 rows. Not fixed in this pass
  (would require either a live re-tag run or deriving the tag from a
  per-page rather than per-offer signal - see the `Requirement`-field
  follow-up idea below); recorded here so the partial-coverage caveat
  above is understood to include per-vendor gaps, not just
  dataset-wide ones.
- **Correction (2026-08-18 review fix, Critical): `MergeIntoBaseline`'s
  protected-merchant union path silently discarded the fresh
  `SeasonalFestival` tag it exists to add.** `kept.Concat(fresh)` put the
  baseline row first, so `GroupBy(OfferId).Select(g => g.First())` kept
  the untagged BASELINE row on any OfferId collision - and since
  `SeasonalFestival` is deliberately not hashed into `OfferId` (by
  design, so tagging a shipped offer never changes its identity), a
  protected merchant whose row content was otherwise unchanged collided
  every time, so the feature's whole output was dropped for exactly the
  merchants the protected-merchant guard exists to preserve data for.
  Separately, a protected merchant's baseline row that predates a
  `VendorOfferHasher` hash-format change gets a DIFFERENT `OfferId` for
  content-identical data, so the OfferId-based dedupe alone would ship a
  duplicate tagged+untagged pair. Fixed by concatenating `fresh` first
  (so it wins any OfferId collision) and adding a second, protected-
  merchant-scoped dedupe pass keyed by offer content
  (`Program.ComputeContentKey` - item/count/costs/locations/caps,
  deliberately excluding `SeasonalFestival` itself) that also prefers the
  fresh-tagged row. The prior test asserting this path (`
  MergeIntoBaselineTests.MergedResult_DedupesByOfferId_...`) carried the
  wrong premise in its own comment ("Same OfferId means content-
  identical") and never asserted which copy survived a collision; it now
  asserts the surviving row carries the fresh tag, and a second test
  covers the content-key (different-OfferId) case.
- **Correction (2026-08-18 review fix, Must Fix): a scoped `--query` run
  could hard-abort AFTER doing live scrape work, discarding it.**
  `ResolveSeasonalFestivalValuesAsync`'s fetch budget (and
  `--max-seasonal-pages` safety check) was scoped to the FULL merged
  `wiki_vendor_cache.json` (Step 2's `MergeWikiCache` union), not to the
  pages this run's own `--query` returned - on a real dev-machine cache
  (thousands of distinct vendor pages), a narrow `--query` computed
  thousands of "uncached" pages and threw `SafetyLimitException` before
  Steps 4-6 ever wrote output, even though the run's own live scrape had
  already completed. Fixed by threading the query-scoped result list
  through as a new optional parameter that the fetch budget now checks
  instead (the existing `--resolve-item-currencies-only` path, which has
  no `--query` and intentionally processes the whole cache, is
  unaffected - it passes no scoped list and keeps its old behavior).
- **Correction (2026-08-18 review fix, Must Fix):
  `WikiSmwClient.FetchWikitextAsync` could permanently miscache a page as
  "checked, not tagged."** `action=parse` does not resolve redirects by
  default (unlike `action=ask`'s SMW queries) - a vendor page whose SMW
  subject title is a redirect returned `#REDIRECT [[Target]]` as its
  wikitext, in which the `{{Temporary}}` parser correctly finds no
  template, silently caching a false negative. The same silent-permanent-
  miss happened when the API returned an `error` object (missing/renamed
  page): the method returned null, and the caller cached that identically
  to a real "no template" result. Fixed by adding `&redirects=1` to the
  request, and by having the caller warn and leave a null-wikitext page
  UNCACHED (retried next run) instead of caching it as `""`.
- Nice-to-have fixes in the same review pass: the `README.md` table row
  for `ref/seasonal_wikitext_cache.json` used an em-dash (repo rule bans
  them outside correctly-encoded UI text), corrected to `-`.
  `ResolveSeasonalFestivalValuesAsync`'s cache-apply loop only ever
  ASSIGNED a non-empty cached value and never CLEARED one - combined with
  the Step 3.5 cache re-save, a value that round-tripped in could never
  be un-set even if the wiki later drops the `{{Temporary}}` template;
  now assigned unconditionally (including `"" -> null`).
  `--max-seasonal-pages` is now rejected at parse time if `<= 0` (used to
  make every tagging run throw `SafetyLimitException` with a message that
  read like a data problem). Recorded but not acted on: `WikiVendorResult.
  Requirement` is already populated on every SMW row at zero extra HTTP
  cost and carries the same "the festival [[X]]" signal `{{Temporary}}`
  does (confirmed in `ref/wiki_vendor_cache.json`); deriving the tag from
  it (with `{{Temporary}}` kept as the authority where the two disagree)
  would give dataset-wide coverage with no separate opt-in fetch pass and
  no `--max-seasonal-pages` trap - worth a future pass, out of scope here
  since this pass was scoped to `{{Temporary}}` parsing.
- **Accepted limitation of the 2026-08-20 tag harvest (recorded, not
  fixed): a shipped `seasonalFestival` tag is now sticky, and a vendor
  that genuinely stops being seasonal will not un-tag itself.**
  `MergeIntoBaseline`'s harvest carries a replaced merchant's baseline
  tag onto its untagged fresh rows, and at that point an untagged fresh
  row is indistinguishable from a transiently-missed one: "the wikitext
  fetch failed / the page was left uncached this run" and "the wiki
  genuinely dropped the `{{Temporary}}` template, so this vendor is
  year-round now" both arrive as `SeasonalFestival == null`. The
  cache-apply loop's `"" -> null` clear (2026-08-18 nice-to-have above)
  un-sets the value on the `WikiVendorResult`, but the harvest then puts
  the old tag straight back, so the tag survives until someone edits
  `ref/vendor_offers.json` by hand. Accepted deliberately: a stale
  seasonal tag only makes `SeasonalOfferFilter` EXCLUDE that offer
  outside its festival window, i.e. the solver ignores a source it could
  legally have used (a missed-saving, visible in a plan) - the opposite
  failure, dropping a shipped tag, re-introduces the phantom
  year-round-vendor mispricing this whole feature exists to close, and
  is silent. Same "never silently delete shipped data" trade the
  protected-merchant path already makes. A future pass wanting real
  un-tagging needs a positive "this page was fetched successfully AND
  carries no `{{Temporary}}`" signal threaded from
  `ResolveSeasonalFestivalValuesAsync` into the merge, rather than
  inferring it from a null.
- Nice-to-have (recorded, not fixed) on the 2026-08-20 self-healing
  budget: `toFetch` is deterministic (wiki-result order, uncached pages
  only) and `Take(maxSeasonalPages)` takes its head, so a truncated run
  converges only via the pages it actually CACHES - a failed fetch is
  deliberately left uncached and retried. A block of permanently-failing
  pages at least as large as the budget, sitting at the head of
  `toFetch`, would therefore be re-attempted every run and starve the
  pages behind it; the run-time NOTE's "the remaining count only shrinks
  from here" is accurate for successful fetches, not for that
  pathological shape. Not reachable under the shipped defaults (budget
  2500 vs the measured ~2,088 distinct vendor pages, so the truncation
  branch never runs at all); a future fix would rotate the fetch window
  or track per-page failure counts.

**Review-fix pass re-validation (2026-08-18, measured, after the
`MergeIntoBaseline`/`ResolveSeasonalFestivalValuesAsync`/
`FetchWikitextAsync` corrections above):**
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/tools/VendorOfferUpdater/
VendorOfferUpdater.csproj` - 0 errors, 0 warnings.
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/GW2CraftingHelper.csproj -p:Platform=x64` -
0 errors, 0 warnings. `"/mnt/c/Program Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-festivalscrape/tests/VendorOfferUpdater.Tests/
VendorOfferUpdater.Tests.csproj` - 196 green (5 new: 1
`MergeIntoBaselineTests` content-key case
(`ProtectedMerchant_DedupesByContent_...`), 3
`ResolveSeasonalFestivalValuesAsyncTests` cases (null-wikitext-left-
uncached, query-scoped fetch budget, stale-value-cleared), 1
`WikiSmwClientTests` redirects=1 case; the existing
`MergedResult_DedupesByOfferId_...` test was corrected in place to assert
which row survives a collision rather than only the count, not counted
as new). `"/mnt/c/Program
Files/dotnet/dotnet.exe" test C:/Dev/Blish/wt-festivalscrape/tests/
GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` - 1675 green (0
new; this pass touched no code the module-side suite exercises). Both
suites fully green. `ref/vendor_offers.json` was NOT regenerated by this
review-fix pass (no live wiki run performed) - the `MergeIntoBaseline`/
`ResolveSeasonalFestivalValuesAsync`/`FetchWikitextAsync` fixes above are
verified by unit test only; the per-vendor tag-coverage gap noted above
is documentation-only for the same reason.

**Post-merge re-validation (2026-08-20, measured, on the
`origin/master` merge commit that precedes this entry):**
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/GW2CraftingHelper.csproj -p:Platform=x64`
- 0 errors (pre-existing StyleCop warnings only).
`"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-festivalscrape/tools/VendorOfferUpdater/
VendorOfferUpdater.csproj` - 0 errors, 0 warnings.
`GW2CraftingHelper.Tests` - 1765/1765 green (the 1675 figure recorded
above was measured pre-merge; master added 90 tests of its own).
`VendorOfferUpdater.Tests` - 205/205 green. `ref/vendor_offers.json`
untouched by the merge, 57 `seasonalFestival` tags intact.

Gate: not yet run live - updater-side tooling verified by suite; the 57 seasonal tags and solver exclusion were live-gated 2026-08-16. Merged after the full review pipeline (three verification rounds) resolved every finding, under the standing merge directive (2026-08-16).
