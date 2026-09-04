> **Milestone record - 2026-08-24, branch `field-fixes-3`.** Moved verbatim out of the append zone in `docs/KNOWN-ISSUES.md` by the 2026-08-25 rotation.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Zero-band retention, scroll anchoring, click default, MF recipes, first-load snapshot (field-fixes-3)

Five independent field reports from one live 0.2.3 session.

### 1. The zero band keeps its cells even when a term is unmeasured

Reported in the field: the Total Cost section can end up displaying only
the "Actual Cost to Craft" tile when everything in the recipe tree that
requires ingredients or purchases ends up ignored. It should retain the
other cells in the section and show them as 0 rather than making them
disappear.

This overrides the v0.2.2 rule recorded in the
`root-ignore-summary-zero` section above, which kept the collapsed lone
tile whenever `PlanViewModelBuilder.HasUnpricedNode` was true. The
argument for that (a zero nobody measured must not be dressed as a
priced equation) was sound about the NUMBER and wrong about the
MECHANISM: real plans routinely carry UNKNOWN nodes (Globs of Dark
Matter, account-bound gifts), so the collapsed band turned up
constantly, and a section that drops cells reads as broken rather than
as careful.

The honesty moved into text that states it. On an unpriced zero the band
renders all three tiles at 0 and:

- every tile caption takes `PlanViewModelBuilder.UnpricedTileMarker`
  (`*`),
- every tile tooltip takes `UnpricedTooltipSuffix` ("... could not be
  priced and count as 0, so this figure is a floor rather than a
  measured total"),
- the section grows a second `SummaryFootnote` row,
  `UnpricedFootnoteText`, above the standing Trading Post line. The
  renderer already drew every footnote row it was handed and
  `SummarySectionLayoutMath.BodyHeight` already counted every one, so
  neither needed a change.

A measured zero therefore still reads differently from an unmeasured
one - no marker, one footnote. `HasUnpricedNode` is now walked ONCE per
section build, in `BuildSummarySection`, and still only from the
zero-cost gate.

Two things deliberately did NOT change. The **profit band** still
suppresses entirely on an unpriced zero: its tiles would not be zeros at
all - an unmeasured 0 materials value makes "Profit if Sold" the entire
sale price, a large invented number no footnote makes safe. And **Free
mode with owned materials consumed** still collapses to the lone tile
(the middle term is absent, not zero - see the older section's
known-vs-absent-zero paragraph).

The profit band's own absence is nonetheless the same complaint one band
lower - the reported state, only the Actual Cost to Craft section,
is a Total Cost section with the cost band collapsed AND no Sell
Value / Profit tiles - so it is now accounted for in text rather than
left as two missing cells: a third `SummaryFootnote` row,
`ProfitSuppressedFootnoteText`, added on exactly the condition
`BuildProfitFormulaBand` returns on (`NetSaleValue.HasValue`), so it
never claims a band was hidden on a plan that had no sell price to show
one for. The multi-item `MultiItemNote` row ("sell value and profit are
the sum across every requested item ...") is gated on the same
condition for the same reason - it used to scope two tiles that were not
on the page.

### 2. Scroll anchoring across a re-solve

Reported in the field: toggling IGNORE on a row in the recipe tree can
adjust which currencies show up in the Total section at the top, which
makes the content under the cursor "move" as the view reflows.

`CraftingPlanView.PreserveScrollAcross` preserved the scroll OFFSET,
which holds the view still only while the content ABOVE the viewport
keeps its height. A re-solve changes it - the Total Cost currency table
gains or loses rows - so the same offset now points at different
content.

`Services/ScrollAnchorMath` (Blish-free) is the whole decision:
`AnchorLine` (the cursor's content-space line when the cursor is over
the panel, else the viewport top), `TryCapture` (the lowest-starting
candidate at or above that line, ties to the shortest - so a tree row
wins over the section containing it, with no nesting description
needed), `FindTop`, and `RestoredOffset` (old offset plus how far the
anchored element moved, clamped; the cursor cancels out of the
arithmetic and only decides WHICH element is anchored).

The view stays a thin shim: a `_scrollAnchors` key -> control registry
(sections keyed by `PlanSectionType` in `CreateSectionHeader`, tree rows
keyed by solver `NodeId` through a new optional
`TreeSectionController` delegate - the same identity that class's own
in-place row pairing already trusts across a re-solve), a parent walk to
content space that skips anything invisible or no longer under the
content panel, and `GameService.Input.Mouse` for the cursor. An anchor
that no longer exists after the rebuild (its subtree was ignored away)
falls back to the previous offset-only restore rather than jumping to
where a missing row "would" be.

### 3. Click sound default 75 -> 35

The field test that the click-volume section above was waiting on
returned 35% or so as a reasonable click default volume.
`ClickSoundVolume.DefaultPercent` is now 35 - 1.75x Blish's 0.2 fixed
volume (+4.9 dB), 0.875x its 0.4 absolute ceiling (-1.2 dB), putting the
asset's own 0.357 peak at -18.1 dBFS. The setting is persisted, so a
user who already moved the slider keeps their value; only the default
changed. That section's dB derivation and the test that pinned "louder
than the old ceiling" (true of 75, not of 35) were both restated.

### 4. UNKNOWN Mystic Forge gifts - measured: not the build bump, and mostly not a defect

Reported in the field: Gift of Rays, Gift of the Survivors, Gift of the
People and Gift of the Hylek all show UNKNOWN in the recipe tree,
alongside the per-generation log line "Recipe seed built for build 205505; current
build 205780; seed negative entries will fall back to API."

**That log line is not about negative recipe ids.** "Negative entries"
there means negative CACHE rows - `SeededRecipeCacheStore.TryGetSearch`
invalidates an entry only when it is an EMPTY list AND the seed build
differs. Synthetic Mystic Forge ids were never touched by it.
`MysticForgeSeedStalenessTests` proves this several ways, including one
test that plans item 107040 (Gift of Rays) through the REAL shipped
`ref/` seed files with the seed build bumped and an API client that
404s the MF id: it still crafts.

Measured data state, audited directly against `ref/`: all **1591**
recipes in `ref/mystic_forge_recipes.json` are already present in
`ref/recipes_seed.json`, and every one of their output items already has
a non-empty `ref/recipe_search_seed.json` row that already lists that MF
id (0 missing on each count). 107040 -> `[-1587]` is one of them.

**The parent plan is Endless Summer (107022)**, named by the field
session's own `module_log.jsonl` and `plan.json`, not Orrax: 107040
is an ingredient of -1586, and the other three gifts are its siblings.
`MysticForgeSeedStalenessTests.RealShippedSeed_EndlessSummerGifts_...`
plans that exact parent through the REAL shipped seed under a build
bump: **Gift of Rays resolves to Craft on recipe -1587**. The reported
UNKNOWN for it was not reproduced by any route. The persisted
`plan.json` from that session carries
`IgnoredItemIds: [107040]` and renders the row `IsIgnored` - the IGNORE
pill is offered on every non-root item row, so an ignored row is not
evidence of an UNKNOWN one, and an ignored row reads IGNORED, not
UNKNOWN.

The other three are not a recipe-lookup failure either - each has an
EMPTY search row in the seed (the seeder's "the API knows no recipe for
this item") and the wiki confirms none exists - but calling them
"correct UNKNOWNs" was wrong. **The module ships a vendor offer for all
three and silently discards it.** Audited directly against
`ref/vendor_offers.json` (and against the unpacked shipped 0.2.3 `.bhm`,
which carries the same rows): 106712 -> Castaway Agnes (Hullgarden),
paid in items 105848 + 106467 + 106370 and 500 of currency 83; 105804 ->
Canach (Breezy Cay), items 105933 + 106672 + 106627 and 500 of currency
81; 106986 -> Palak (Hullgarden), 250x item 19717, 300,000 karma and 200
copper. None carries a seasonal tag, so `SeasonalOfferFilter` is not
involved.

The mechanism is `VendorBatchSolver.EvaluateVendorOffers`: an `Item`
cost line whose item has no Trading Post price sets `priceable = false`
and discards the WHOLE offer. These are barter offers paid in
account-bound tokens, which can never have a TP price, so every offer
for the node is discarded, nothing comparable and no fallback survives,
and `PlanSolver` commits `UnknownSource` - exactly what that
`plan.json` records (Source `UnknownSource`, RecipeId 0, for all three).
`AcquisitionHintSeedVendorAgreementTests.ShippedBarterOffer_...` proves
it on the real solver path with the real shipped offer.

**Class size, counted from `ref/vendor_offers.json`:** 17,802 of 53,544
offers carry an `Item` cost line, and 4,664 of the 14,965 distinct
output items have NO offer that is free of one. Every one of those
renders a bare UNKNOWN while the module holds the merchant and location
on disk. **Follow-up (not done in this wave):** when every offer for a
node is discarded as unpriceable, surface that offer's merchant and
location on the node itself, instead of hand-typing them into a second
file. That would cover the class; the three hints below cover only the
three items the report named.

So the fix for these three is the pre-existing acquisition-hint
mechanism (`CraftingTreeBuilder.ApplyAcquisitionHint`, unchanged), with
two corrections from review. **They break the precedent the other seven
hints set**: all seven pre-existing entries (71994, 70698, 70797, 71943,
74528, 19678, 43772) are for items with ZERO vendor offers, so the
mechanism's implicit contract was "no source anywhere in our data".
These three each have exactly one offer, and 106986's first draft
contradicted it (it said "Shaman Palak in Shipwreck Strand"; the record
says Palak, Hullgarden). The vendor record is now the authority: each
hint names only the merchant and location that record carries, makes no
cost claim at all, and adds only what the record has no field for (the
achievement, "not craftable", and why the row still counts as 0).
`AcquisitionHintSeedVendorAgreementTests` pins that agreement against
both real files and trip-wires on a fourth hinted item gaining an offer,
so a re-scrape that relocates a merchant now fails a test instead of
showing two answers. Their names come from the item API, so their
absence from `ref/item_name_seed.json` never mattered.

The badges are `MERCHANT` / `MERCHANT` / `ACHIEVEMENT`, not `VENDOR`: a
`VENDOR` badge was byte-identical to the single-source VENDOR pill
(`DecisionPillPlanner`), which means the opposite thing - a priced
purchase inside `Plan.TotalCoinCost` rather than an Unknown node
contributing 0 - and on this very plan would have sat directly above
item 1's new "no recipe and no Trading Post price ... count as 0"
footnote, arguing with it. `DecisionPillPlanner` now drops any badge
equal to a module-owned source badge back to `UNKNOWN`, so the seed
cannot reintroduce the collision. The sweep found one sibling with the
same defect - `ShoppingSourceBadge.ForRow`, which renders the same
seeded badge on a Shopping List `ShoppingUnknown` row next to real
`TP`/`VENDOR`/`CURRENCY` rows - and it now shares the one predicate. The
hint TEXT still reaches the tooltip in both places, so the guard costs
nothing but the misleading badge.

`SeededRecipeCacheStore.MergeMysticForgeRecipes` is kept but is a
**measured no-op on today's data** - every iteration hits
`existing.Contains(recipe.Id)` and re-stores an equal recipe - and must
not be read as the explanation for any of the four reports. What it buys
is future-proofing in the direction the seed is weak: a seed row saying
"the API knows no recipe" is an EMPTY list served as a cache HIT, so
nothing consults `MysticForgeRecipeData`, and a recipe added to the
wiki-sourced forge file without re-running the seeder would be invisible
(and, via the stale-seed fallback that turns an empty row into an API
call `CompositeRecipeApiClient` rescues, its visibility would depend on
the live game build id - nonsense for wiki data). With the merge, adding
a forge recipe to `ref/mystic_forge_recipes.json` alone is enough.
Genuine API-sourced staleness still falls back exactly as before.

### 5. First-load snapshot

"When you load for the first time we should trigger a snapshot
immediately to try to fetch your API inventory stuff otherwise
everything is empty and requires either I assume waiting 10mins OR
clicking manually to pull a snapshot."

An install with nothing cached had no automatic route to its first
snapshot at all - not a slow one. `Module.LoadAsync` fetches only when
Blish has already granted the subtoken (usually it has not at that
point), and the `SubtokenUpdated` handler that would cover the late
grant is attached on the very next line, so an event that fired first is
missed. `Update()`'s interval refresh then returns on
`_currentSnapshot == null` every tick forever.

`Update()` now runs a one-shot first-load fetch through the existing
`RefreshSnapshotInBackgroundAsync` (spinner, status text and failure
classification unchanged). `Services/FirstLoadSnapshotGate` holds the
rule Blish-free: fire once, only with the API ready and no refresh or
post-failure backoff in the way - and a blocked tick does NOT spend the
one shot, so a module whose key arrives late still gets its fetch.
`Module.IsInRefreshFailureBackoff` was extracted so the gate and the
refresh itself read the same window. `Views/MainView.cs` is untouched.

Two follow-ups from review, both in that branch of `Update()`.
**Clear Cache re-arms the shot** (`_firstLoadRefreshAttempted = false`
inside the `SnapshotCommitGate.Clear` callback, alongside the other
field resets): an in-session clear recreates exactly the nothing-cached
state the shot exists for, and without the reset it left the tab with no
automatic route to a snapshot until Blish restarted. And **that branch
no longer probes the API manager per frame**. Both of the gate's inputs
are live readings evaluated as arguments -
`_snapshotService.HasRequiredPermissions()` (`Gw2ApiManager.HasPermissions`
takes an `IEnumerable`, so an enumerator allocation) and
`IsInRefreshFailureBackoff()` (a `UtcNow` read) - and the branch itself
is reached every frame for as long as nothing is cached, which with no
API key configured is the whole session. Checking the spent flag at the
call site only silences it AFTER the shot is used, and the shot is never
used in exactly that case, so the flag alone left the probe running at
frame rate on the UI thread. `FirstLoadSnapshotGate.ShouldCheckNow` is
the second guard: a Blish-free frame-delta accumulator that lets the
gate be re-evaluated once every two seconds instead. It is seeded full
(and reset the same way by Clear Cache) so an armed shot is still
checked on the very next tick, and the coarse interval costs no first-
load latency - a granted subtoken reaches the fetch through
`OnSubtokenUpdated`; this poll is only the backstop for the grant that
fired before the handler was attached.

### Desktop gate

1. **Zero band, unmeasured**: plan an item with no recipe and no
   Trading Post price (root row reads UNKNOWN), or any plan carrying an
   UNKNOWN node whose total comes to 0. The Total Cost band shows all
   THREE tiles at 0 with the "-"/"=" operators and the result
   highlight, each caption ending in `*`, a footnote line above the
   Trading Post line explaining the `*`, and the `*` explanation again
   in each tile's hover text. No profit band renders on such a plan -
   and when the target itself has a Trading Post sell price, a SECOND
   footnote line says so ("Sell Value and Profit if Sold are hidden
   here ..."), so no cells go missing without an explanation on screen.
2. **Zero band, measured**: with "Use Own Materials" OFF, ignore every
   child until the plan costs nothing. Same three tiles at 0, but NO
   `*` on any caption and only the one Trading Post footnote - a
   measured zero must still look different from an unmeasured one.
3. **Scroll anchoring**: generate a deep plan (a legendary), scroll
   into the middle of the recipe tree, put the cursor ON a row, and
   click IGNORE on a row at or below it - the gesture that used to jar.
   The row under the cursor stays under the cursor while the Total Cost
   section above gains or loses currency rows. Repeat with the cursor
   off the panel (click IGNORE via a row further down, then move the
   mouse away first): the topmost visible row holds instead. Also
   collapse/expand a section header and a tree caret - neither should
   jump.
4. **Click default**: on a profile that has never touched the slider
   (or after Clear Cache / a fresh install), Settings shows **35%** and
   the click is audible but unobtrusive. On a profile that HAS moved
   the slider, the old value is still there - the default change must
   not overwrite it.
5. **Mystic Forge**: search for and plan **Endless Summer** (the exact
   item to search - it is the parent the report came from). First clear
   the carried-over ignore: **Gift of Rays** will show the IGNORED pill
   from the 0.2.3 session, so click it to un-ignore. Its row must then
   read CRAFT, not UNKNOWN, and expand into Gift of the Sun, Gift of
   the Beach, Gift of Infused Gems and Purified Rift Essence. Gift of
   the Survivors, Gift of the People and Gift of the Hylek are NOT
   craftable (barter purchase / achievement - verified on the wiki and
   against the shipped vendor offer), so they correctly stay
   uncraftable, but their pill must now read **MERCHANT**, **MERCHANT**
   and **ACHIEVEMENT** instead of UNKNOWN, with the merchant name and
   location in each row's hover text - and NOT "VENDOR", which is the
   priced single-source pill and must not appear on a row the Total Cost
   footnote simultaneously calls unpriced.
6. **First-load snapshot**: with a valid API key set, clear the cached
   snapshot (Clear Cache) and restart Blish. The Snapshot tab starts
   fetching by itself within a few seconds - spinner visible, then real
   inventory - with no click and no 10-minute wait. Then, WITHOUT
   restarting, press Clear Cache again: a fresh fetch must start by
   itself the same way (the one shot is re-armed by the clear). Then
   repeat with the API key removed: the tab stays empty with its normal
   status, the log shows no repeating fetch attempts, and adding the key
   mid-session starts exactly one fetch.

Gate: PASS with two recorded partials (2026-08-25 desktop session,
branch build merged with master, captures preflight/gFX1-gFX8,
gAN1-gAN3, gFL1-gFL2, gMF1; display-sleep inhibitor held the session).

1. ZERO BAND: live PASS on the known-zero case - crafting Green Wood
   Dowel and ignoring its only child rendered the full three tiles
   ("Total Materials Value 0c - Your Materials Used 0c = Actual Cost to
   Craft 0c") with the profit band beside it, where the old build showed
   a lone tile. PARTIAL: the unpriced-zero variant (marker + footnote)
   could not be reached live - it needs a zero total produced by an
   unpriceable node, and an unpriceable item has no recipe, so it is
   absent from the craftable search index and cannot be planned
   directly. Pinned by the round's discriminator tests instead.
2. SCROLL ANCHORING: live PASS, measured rather than eyeballed. Scrolled
   deep into an expanded 154-node Wupwup tree (Total Cost off-screen
   above), clicked IGNORE on "3x Deldrimor Steel Ingot" with the cursor
   resting on it: that row and every one of the eight rows above it held
   their EXACT pixel y across the re-solve, while the row's own subtree
   collapsed and new content flowed in below. This is the reported
   gesture, and the jar is gone.
3. CLICK DEFAULT: ClickSoundVolume.DefaultPercent is 35. Live check
   confirmed the other half of the contract - a persisted 78 from an
   earlier session survived the change, so only new installs take 35.
4. MYSTIC FORGE UNKNOWN: live PASS on the field report -
   "Gift of Rays" now generates a complete plan (legendary-purple
   header, 892g 88s 38c, eight currency requirements listed) where it
   previously rendered UNKNOWN. Note the stale-build lead
   was DISPROVEN by this round's tests; the defect actually fixed was
   the empty-seed-row cache hit, and this is its live proof.
5. FIRST-LOAD SNAPSHOT: PARTIAL by environment. With the cached snapshot
   deleted and a fresh launch, the module correctly did NOT fetch - the
   sandbox has no API key and the status read "GW2 API access not
   ready". The half that IS verifiable passed and was the actual risk:
   the module log stayed EMPTY across the whole session, so the blocked
   gate neither spends its one shot nor re-probes per frame. Firing on a
   real key belongs to an install that has one.

FOLLOW-UP within the same session, from the Gift of Rays capture: Actual
Cost to Craft was all that screen showed, where it should carry three
fields. The band still collapsed whenever the middle term
was zero - the ORIGINAL "no middle term to subtract" rule, older than
this round's zero-plan work, and the ordinary case for any plan whose
materials you do not already own. Rule deleted: all three tiles now
always render. The one genuinely unmeasured case (Free mode with
materials consumed, where MaterialOpportunityCost is null by contract)
keeps its 0 and moves the "nobody priced these" fact into the tile's own
tooltip, with a test asserting the two zeros do not read identically.
Eight tests pinned the old contract and moved with it. Live re-verified
on the same plan: "Total Materials Value 895g 13s 39c - Your Materials
Used 0 = Actual Cost to Craft 895g 13s 39c".

Bonus verified in passing: the seed-integrity fix from PR #176 works end
to end - "Pile of Recycled Trebuchets" now autocompletes with its icon
and resolves through VENDOR at 1940g, where its chain was previously
absent from the local name seed.


