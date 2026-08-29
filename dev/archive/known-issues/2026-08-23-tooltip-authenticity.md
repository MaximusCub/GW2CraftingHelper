> **Frozen record - 2026-08-23, branch `tooltip-authenticity`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).
>
> **Superseded in part - 2026-08-26, branch `tooltip-fidelity`.** Lossless
> live captures (2026-08-25, `/mnt/c/Dev/Blish/tooltips/live/`, audited in
> `/mnt/c/Dev/Blish/tooltips/fidelity-audit.md`) overturned several claims
> below: the rarity WORD is drawn in the rarity colour, not white (the
> 2012-2016 captures behind G5's white reading predate a game change); the
> upgrade blue is measured at (85,153,255), not the spec's recommended
> value; `MaxContentWidth` moved 350 -> 392; the inner bevel ring is not in
> the game (it darkens inward); and the canvas carries a blue-green cast,
> not pure black. Desktop-gate step 3's "so are the rarity word" clause and
> the line-order map's `<Rarity> white` row are the superseded statements.
> The white ATTRIBUTE lines, line order, and blank rules were all
> re-confirmed by the same captures.
>
> **Superseded further - 2026-08-26, branch `tooltip-round2`.** Thirteen
> owner captures (`/mnt/c/Dev/Blish/tooltips/live3/`, audited in the
> fidelity audit's live3 addendum) overturned more of the below: the
> whole consumable effect block is grey #AAA, not white-first-line (the
> round-2 correction #1 and F7 rested on JPEG-era captures); the game
> folds the duration into a "Nourishment (45 m):" lead-in with an inline
> effect icon rather than a "Duration:" line; the binding line is TWO
> independent dimensions (account + soul) and AccountBound reads
> "Account Bound on Acquire", not the most-specific "on Use"; crafting
> materials and upgrade components have NO type line and no rarity word
> (nor do trophies and consumables, which say "Consumable", never
> "Food"); materials/trophies/consumables lead with their description
> under the header; the vendor value is contiguous for EVERY shape (the
> Generic blank and the `ValueSitsAfterABlank` table are refuted -
> vials measures a material's coin row one pitch under the line above);
> coin segments are bare digits ("2g 0s 0c", never "00"); the sigil
> cooldown DOES exist in the API (inside buff.description markup); and
> the owner's field ruling retired the Menomonia-16 divergence - the
> tooltip renders at 14 with `MaxContentWidth` 350. The line-order map
> and desktop-gate steps 3/6/15 below describe the superseded shapes.
>
> **Superseded further - 2026-08-28, branch `wave1-tooltips`.** The
> owner's field ruling that retired the Menomonia-16 divergence is itself
> retired: a side-by-side against the game's own tooltip reads the 14pt
> box a size too small, and the surface now draws at `UiFonts.Body`
> (Menomonia 16). `ItemTooltipMaxContentWidth` is not a transferable
> number - it is the midpoint of the bracket the game's own break
> decisions leave when the corpus is measured THROUGH the face the
> surface draws in - so it was re-derived against menomonia-16-regular
> and moved 332 -> 376, reproducing the same wraps. Every statement below
> about a 14pt face or a 350/332 cap is superseded; the line order,
> colours, canvas and blank rules are not.
>
> Also settled here, because it has been mis-diagnosed more than once:
> the game's own tooltip is SEMI-TRANSPARENT and so is this one. Scene
> showing through the box in a side-by-side is the match, not a defect.

## Tooltip authenticity (tooltip-authenticity)

The maintainer's mandate, verbatim: *"tool tips should try as hard as
possible to duplicate the in-game visual style pixel perfectly.
divergences where necessary because of the item representations being
slightly different are ok.. but lets try to make it match."*

Everything below is built from `/mnt/c/Dev/Blish/tooltip-research/spec.md`,
the read-only research artefact that measured the game's own tooltip off
five wiki captures, two third-party replicas, and the live `/v2/items`
responses. Its gap map numbers the deltas G1-G25; this section says what
each one became. **Where the spec labels a claim `inferred` (chiefly
FWDekker's per-item-type replica of the LINE ORDER - no modern in-game
equipment tooltip capture exists on the wiki) the implementation follows
the replica and this section says so.**

### One correction to the record

`ItemStatTooltipComposer` carried the standing comment *"No item icon:
GW2's own item tooltips have none"*. That is **false** - all three wiki
captures open with a ~34x34 framed item icon (spec section 1.5). It was
the largest single visual divergence in the feature and is now the header
row.

### What changed, in the spec's own build order

**1. Colours and roles** (G3, G4, G5, G6, G7, G9). `TooltipSpanRole`
gained `Flavor`, `AbilityType`, `Warning` and a reserved `BonusInactive`;
`Default` (white) took over the whole identity block, and `Muted` narrowed
to genuine secondary annotations. `Bonus` went from green to the measured
light blue - the old comment asserting *"the game's own green for granted
bonuses"* was wrong; `Rune_effects_full.jpg` and
`Rune_effects_partial.jpg` both show blue, and FWDekker uses `#5599ff`.
Flavour went from grey to the measured pale teal `#B1D7D2`. Legendary
moved to the wiki dark skin's `#974EFF`; the rest of `RarityColors` was
already an exact match and is untouched.

`ItemDescriptionSanitizer` stopped discarding the API's `<c=@...>` runs
and now emits role-carrying spans, which is the only way "A gift bag!"
(white) can be told from the quoted flavour after it (teal) inside one
description string. `Sanitize()` is still exactly the concatenation of
those spans, and a nested run restores its outer role on close.
`ItemStatBlock` carries the description verbatim; the split happens at
compose time. `Services/` stays XNA-free - only
`RichTooltipSurface.ResolveColor` maps a role to a `Color`.

**2. Canvas** (G1, G2, G8, G23). `RichTooltipSurface` overrides
`PaintBeforeChildren`, so Blish's own tooltip texture (decompiled: drawn
at `Color.White * 0.98f`, plus four dark inner edge bands) is never drawn
at all. The box is ONE translucent layer - `Color(0,0,0) * 0.92f` - plus a
1px `rgb(6,10,12)` border on all four edges. That is the G1 collision's
recommended resolution: the content covers Blish's chrome entirely, so
audit finding H6 (content bleeding through the box) stays fixed while the
fill is the game's own translucency rather than an opaque `(14,14,14)`.
**Never reduce the alpha while Blish's own art is still drawn underneath**
- that stacks two translucent layers and matches neither.

G23 needed no code: Blish's content edge buffer is `Thickness(4 top, 4
right, 3 bottom, 6 left)` and `RecalculateLayout` turns it into the
ContentRegion every child is positioned inside, which already IS the
game's measured 6px left padding.

**3. Line order and blanks** (G12, G13, G14, G15, G20) and **6. content
completeness** (G16, G17, G18, G19), taken in one commit because both are
lines in the same rewritten identity block.

```
[icon] <Name>                     rarity colour
Weapon Strength / Defense         white   (thousands-separated, G19)
+<N> <Attribute>                  white
<buff> / (N): <bonus>              upgrade-bonus blue
<nourishment> / Duration          white   (measured, steak.png)
(blank)
Infusion Slot                     white   (one line per slot, G16)
(blank)
<Rarity>                          white   (omitted when Basic, G20)
<Weight class> / <Type> Armor     white
(<Hand>)                          white   (G17)
Damage Type / Required Level      white
Double-click to select stats.     white   (G12)
<description / flavour>           white / teal   (G13)
Unique                            white   (G18)
<binding> / <restrictions>        white
(blank)                           ONLY on the Generic shape - see below
<value>                           per-denomination coin run, unlabelled (G14)
```

Whether a blank sits under the header depends on what the body OPENS
with, not on combat facts alone. A body opening with the combat facts or
with the nourishment block runs straight on; one opening with the
identity block, or with an upgrade component's bonus run, takes a blank
first. See "Post-review corrections" below for the measurements and for
the one capture this still diverges from.

The blank ABOVE the value splits the same way, on the item's own shape:
measured absent on `steak.png` (body bands 39, 57, 75-blank, 93, 111,
129 - one 18px pitch from `Required Level: 10` to the coin row, row 128
empty). FWDekker has FOURTEEN builders, only ELEVEN of which emit a
value at all, and it puts a break before `getValue()` in just two of
those eleven - `Generic`, its fallback, and an `UpgradeComponent` of
type Gem. So a weapon, armour piece, consumable, trinket, bag or rune
runs its value straight on under the line above, while a crafting
material, a trait, a key, a Gem or any type the API adds later takes a
blank first. The Generic blank is INFERRED; only its absence is
measured. The three builders that emit no value line - `Gathering`,
`MiniPet`, `Tool` - are a GUESS and the only one in the table; see
"Accepted divergences" below. The vendor value is omitted entirely on a `NoSell`
item - there is no last line and no blank in front of one - and its
plain rendering now drops leading zero units through
`CoinSegmentMath.FormatSegmentTexts`, so "7c", never "0g 0s 7c".

**4. Header icon** (G11). A new HEADER line KIND on `TooltipContent`
carrying an icon URL. `TooltipLayoutMath` gives every row its own `Y` and
`Height`, indents a header line's name column past the icon (continuation
rows included, with the wrap budget narrowed to match), and hands the icon
to the first row of the line only. The icon rides `ItemStatBlock` out of
the same `/v2/items` response the name does - no new request.

**5. Metrics** (G21, G22, G24). Row heights are per-row: a prose row is
one line pitch, only a coin row takes icon clearance, only a header row is
icon-tall. Coin icons in a tooltip are drawn AND measured at ~0.8x the
line height, and the tooltip wraps at 350px. Both of those are
TOOLTIP-LOCAL by design - `CoinSegmentMath.CoinIconSize` and
`TooltipLayoutMath.PreferredMaxContentWidth` keep their values for every
plan table and every plain tooltip; the tooltip passes its own numbers in.

### Beyond the gap map: the maintainer's own scope

**Q1, all surfaces.** The rich item tooltip is now on Used Materials rows,
Shopping List rows and the Snapshot result list as well as the recipe tree
and the plan header. There is no per-surface fork: all five compose
through `Services/ItemRowTooltipComposer`, whose one rule is that the stat
block OPENS the box (it already carries the full name in its rarity
colour, so the ellipsis fallback line would be a duplicate) and the
surface's own extra lines follow after a blank.
`ShoppingRowTooltipFormatter` grew a `BuildRowContent` returning
`TooltipContent`; its HAVE/NEED wording and `BuildCurrencyLines` are
untouched.

`PlanRowViewModel` carries the row's ITEM id, **0 on a currency row** -
the same cross-domain collision `TreeRowTooltipComposer.RowIdIsAnItemId`
guards on the tree side (id 24 is both a real item and the currency
"Pristine Fractal Relics"), gated in `PlanViewModelBuilder` where the
row's source is known.

**Q13, restored plans.** `TooltipFacility.ApplyRichDeferred` composes a
row's content when the box is about to be drawn rather than at render
time. `ApplyRestoredPlan` then fills the session stat cache in the
background via `ItemMetadataService.WarmStatBlocksAsync`, and the next
hover picks the blocks up with no re-render at all; a cursor already
resting on a row when the fetch lands is redrawn by
`TooltipFacility.RefreshCurrent`, marshalled to the main thread.

`WarmStatBlocksAsync` deliberately is NOT `GetMetadataAsync`: that method
writes the unlocked `_cache` and `_knownMissing`, which only the
plan-generation thread touches, and a restore-time top-up racing a
Generate would be two threads writing one Dictionary. The warm path
writes only the locked stat side table, which was already designed for a
background writer and a UI-thread reader. **The stat cache stays OFF the
`PersistedPlan` graph** - nothing here touches the schema, and
`PersistedPlanSchemaMemberSetTests` is unchanged.

Deferring also moved the compose work off the render path, where it ran
once per row per render for rows nobody points at, and let the settle
re-ellipsis closures stop re-stamping tooltips entirely.

### Accepted divergences (G16-style: representation differences, not bugs)

- **Infusion slots say "Infusion Slot", not "Unused Infusion Slot"** (the
  game's exact string, captured on
  `Applying_Infusion_To_Ascended_Staff.jpg`). What is socketed in the
  player's own copy is instance state `/v2/items` does not carry, so the
  module will not claim the slot is empty. It is white and one line per
  slot, so the BLOCK HEIGHT matches the game even though the wording does
  not. Two slots therefore read "Infusion Slot / Infusion Slot".
- **A gathering tool, a salvage kit and a miniature run their vendor
  value straight on under the line above - GUESSED, not measured.** These
  are the only three entries in `ValueSitsAfterABlank` with no evidence
  behind them in either direction: FWDekker's `Gathering`, `MiniPet` and
  `Tool` builders emit no `getValue()` at all (they end on
  `getLevel() + getFlags()`, `"Miniature" + getFlags()` and
  `getDescription() + getFlags()`), and no capture of one exists. This
  module does show their value, because a mining pick and a salvage kit
  both sell, so a shape had to be picked. Picked contiguous by nearest
  body shape - Gathering's description/level/flags body matches Gizmo's
  and Trophy's, Tool's matches Container's and Consumable's, MiniPet's
  matches Trophy's, and all three of those neighbours are contiguous.
  Desktop gate step 6 settles it in one hover.
  **Judgment call - flagged for the maintainer.**
- **Armour's slot line is the API's own noun plus "Armor"** - "Gloves
  Armor" where the game says "Hand Armor". The game's slot vocabulary
  (Head/Hand/Chest/Leg/Foot/Shoulder) is a different word set from
  `details.type` (Helm/Gloves/Coat/Leggings/Boots/Shoulders), and a
  mapping table is exactly the kind of invented data this module refuses.
  The ORDER is the game's (weight class first, measured on
  `warhelm.jpg`); only the noun differs. **Judgment call - flagged for the
  maintainer.**
- **"Damage Type: <x>" has no counterpart in the game's weapon block.**
  Kept because it is a real API fact the tooltip already showed, placed
  after the hand line. **Judgment call - flagged.**
- **A stat-selectable item shows "Double-click to select stats." AND its
  own description, if it has one.** The game's description for exactly
  that item state IS that string, so for the common case the two are the
  same line; emitting both rather than choosing loses nothing. No
  `/v2/itemstats` request is made and `StatChoiceCount` remains the seam
  for a future numbered representation. **Judgment call - flagged.**
- **No `(x/6)` counter and no greyed inactive rune tiers.** Both need the
  character's equipped set - instance state the plan surfaces have no
  business asking for. `TooltipSpanRole.BonusInactive` exists, reserved
  and unused, so an equipped-aware surface does not have to re-plumb the
  role through every composer (spec section 3.2).
- **`<c=@reminder>` has its own `Reminder` role at (175,175,175)**, the
  spec's section-1.4 constant for it (gw2efficiency `.desc-reminder`
  `#afafaf`, inferred). It is deliberately NOT folded into `Muted`
  (150,150,150), which is the measured `#939496` annotation grey off
  `xyaren.png` - two sources, 25 levels per channel apart.
- **The header icon is framed in the measured light grey (166,175,174)**,
  not in the rarity colour the module frames its ROW icons with. The name
  beside it already carries the rarity, and the grey is what the capture
  shows. **Judgment call - flagged.**
- **The upgrade-bonus blue `Color(120,170,235)` is a recommendation, not a
  measurement.** The direction (blue, not green) is measured and
  unambiguous; the exact triple comes from a heavily-compressed JPEG over
  a dark-blue scene plus one replica.
- **Coin number tints are unchanged** (G10). The only measurement is a
  2012 capture; the module's constants are brighter. Not chased without a
  modern sample.
- **A consumable's use description stays inside the identity block, so
  `warhelm.jpg` keeps its blank under the header where the game has
  none.** The game (and FWDekker's `Consumable` / `Container` / `Generic`
  builders) put a consumable's description FIRST, above the identity
  block; the module puts every description in the identity position,
  which is what G13 measured off `xyaren.png` and what keeps one line
  order for every item type. Given that placement the identity block is
  what opens the body, and a blank before it is correct. Reversing it
  means per-item-type builders. **Judgment call - flagged.**
- **No blank before the binding-flags block**, which gap G15 lists as one
  of four required blanks. The primary evidence does not support it:
  `warhelm.jpg` renders `Consumable` and `Account Bound` on consecutive
  16px pitches with no gap (measured, bands y=160-168 and y=176-184), and
  FWDekker's `getFlags()` carries no leading break in any of its fifteen
  builders. `xyaren.png` does show a blank above `Unique`, but the line
  above that blank is the `0/500 in Material Storage` annotation - its
  own block, blank-separated on both sides - which this module does not
  have. Emitting the blank would make `warhelm` measurably worse.

### Reviewer-scrutiny list

- **`PaintBeforeChildren` replaces Blish's art outright.** If a future
  Blish version moves the tooltip's painting, the box would lose its
  background rather than gain a second one. Measured against 1.3.0 by
  decompilation (`ilspycmd`, repo precedent).
- **Deferred builders run inside Blish's mouse-moved handler.** An
  exception in one would be a crash on hover, so resolution is wrapped -
  the box shows nothing and the failure is logged. Every builder should
  stay a pure read of already-built state.
- **A deferred builder retains what it closes over** for as long as its
  control lives: the row view model, the name Label, the renderer's stat
  lookup. Value-references-key cycles in a `ConditionalWeakTable` are the
  documented ephemeron case and still collect, but the retained graph per
  row is larger than the finished `TooltipContent` it replaced.
- **Compose-per-hover cost is unmeasured.** It is ~20 builder lines plus a
  short `StringBuilder` walk of the description, once per hover, against
  the per-render composition it replaced. Inferred to be a net win; nobody
  has profiled it.
- **Icon-note clobbering.** `ApplyRichDeferredToIconTree` cannot skip an
  empty payload the way the eager version does (nothing is composed yet),
  so it would overwrite an icon's own "no icon available for this entry"
  note with silence. `TooltipFacility.Register` now captures each
  control's plain text as the source's fallback
  (`TooltipContent.OrText`), which covers every call site including the
  recipe tree, and a builder that throws degrades to it too. The capture
  lives in the facility rather than at the icon-tree call site precisely
  because a re-stamp - `MainView.ApplyItemRowTooltip` runs again on every
  column resize - reads a `BasicTooltipText` the FIRST stamp already
  nulled; the fallback is carried forward from the previous source unless
  the control has since been given real plain text, or has since had it
  deliberately cleared (`ApplyPlain` records both - see "Post-review
  corrections, round 2"). The `row.ItemId > 0`
  gates that remain are about a currency icon naming its own currency,
  NOT about emptiness - they never prevented it (see "Post-review
  corrections").
- **The header row's wrap budget** is `maxWidth - indent` for continuation
  rows. A very long item name in a narrow box is the case to look at.
- **`RefreshCurrent` is main-thread only.** It is reached from a
  background continuation exclusively through `MainThreadMarshal.Run`.
- **`ItemStatTooltipComposer.SpaceCamelCase`** is unchanged and would
  still mangle an acronym-bearing type token ("PvP" -> "Pv P"); no such
  token exists in the type vocabulary today.

### Post-review corrections

An adversarial review of the milestone raised six Must Fix findings.
Each was verified before being acted on; one was refuted with
measurements and is recorded as a divergence instead of a fix.

**1. The header's blank contradicted `warhelm.jpg` - FIXED, and the rule
re-derived.** The rule was "no combat facts -> blank", and its comment
cited `warhelm.jpg` and `steak.png` as captures of items that HAVE
combat facts. Neither does. Re-measured (PIL, per-row glyph-band profile
of each capture):

| Capture | icon bottom | first body band | blank? |
|---|---|---|---|
| `xyaren.png` (Exotic back item) | y=34 | y=53 | yes |
| `warhelm.jpg` (Fine transmutation consumable) | y=37 | y=38 | no |
| `steak.png` (Fine food) | y=37 | y=39 | no |

That also settles a contradiction inside the spec itself: section 1.2
measures `steak.png` as `37 -> 39` while section 1.6's ASCII
transcription of the same image draws a blank under the name. The
measurement wins. FWDekker's per-type builders agree and explain the
split - `Armor` / `Weapon` / `Consumable` / `Container` / `Generic` emit
their leading block with no break, while `Back` / `Bag` /
`UpgradeComponent` emit `getHeader() + "<br />"`. So the blank is owed
when the body opens with the identity block or with a bonus run, not
when combat facts are merely absent. Food now runs its nourishment block
straight on under the header. The residual `warhelm` divergence is in
the divergence list above.

**2. The missing fourth blank of G15 (before the flags block) -
REFUTED.** See the divergence list: `warhelm.jpg` measures the flags
line contiguous with the line above it, and FWDekker emits no break
before `getFlags()`. The finding cited `warhelm.jpg` as showing the
blank; it does not. Recorded rather than implemented.

**3. A header line with a null icon url - FIXED.** `LayoutContent`
reserved the 39px name indent for every header line, while `RenderRow`
drew only when `IconUrl != null`, so an item whose `/v2/items` response
carries no `icon` rendered its name floating over an empty reserved
column with the body below it at x=0. `TooltipLine.IconUrl`'s own
contract promised the opposite. `HeaderLine` now normalises null to
empty, so a header row always has an icon to draw and null keeps one
meaning: this row draws no icon.

**4. The `row.ItemId > 0` gate did not prevent icon-note clobbering -
FIXED.** A real item id does not make the builder non-empty:
`ItemRowTooltipComposer.BuildRowContent(null stats, short name, no
extras)` returns `Empty`, which is exactly the state of a plan restored
from disk before the Q13 top-up lands. See the reviewer-scrutiny entry
above for the fix.

**5. `ShoppingRowTooltipFormatter` doc-comment hijack - FIXED.** The
`BuildCurrencyLines` summary had been left in place above the newly
inserted `BuildRowContent`, leaving the new method with two `<summary>`
elements and `BuildCurrencyLines` bare of the "THIS ROW" scope-collision
rationale that keeps its suffix from reading as noise. Moved back.

**6. The reminder-grey justification was wrong by an order of magnitude
- FIXED.** `#afafaf` is 175, not "within two levels" of `Muted`'s 150.
`Reminder` is now its own role at the spec's `Color(175,175,175)`.

### Post-review corrections, round 2

A second adversarial review raised three Must Fix findings. All three
were re-verified against the captures before being acted on, and all
three held.

**1. A food's nourishment line was painted upgrade-bonus BLUE - FIXED.**
Re-measured on `steak.png` (PIL, median RGB of pixels at or above 88% of
each band's peak): the two nourishment bands read (252,254,253) and
(252,255,255), against "Food" (251,255,252) and "Required Level: 10"
(254,254,251) on the same capture and the Fine-blue name (106,150,209)
above them. That line IS `details.description` - the field
`ItemStatBlock.NourishmentDescription` carries
(`Gw2ItemApiClient.ParseDetail`) - so the measurement is of this line,
not of a neighbour. The blue is measured on RUNE and SIGIL bonuses only
(`Rune_effects_*.jpg`, FWDekker `#5599ff`), and step 1's re-colouring
swept the food line up with them. `TooltipSpanRole.Bonus`'s own doc
comment asserted the same wrong thing and no longer does.

*Judgment call, flagged.* The 2012 capture's nourishment line is a prose
sentence ("Meal: Double-click to gain +10 power. Lasts 30 minutes.").
The modern API returns an EFFECT LIST in the same field (measured on
12452, 12457, 12345-12348, 9440, 50082 - e.g. "30% Magic Find\n40% Gold
from Monsters\n+10% Experience from Kills", with no `<c=@...>` markup in
any of the eight samples, which is why the factory's flat `Sanitize`
still serves this field). No modern capture of a food tooltip exists on
the wiki, so nobody has measured what the game paints an effect LIST.
White is the only measurement that exists for the field, and it is what
ships; one live sandbox capture of any food item would settle it.

**2. An unconditional blank above the vendor value - FIXED.** Asserted
here as the game's shape and contradicted by the only capture of a value
line. `steak.png` re-measured: body bands at 39, 57, 75 (blank), 93
("Food"), 111 ("Required Level: 10"), 129 (the coin row - number band
y=130-139 at x4-23, coin icon x23-41). Row 128 carries no glyph and rows
122-127 only the `q` descender of "Required": 111 -> 129 is ONE 18px
pitch, contiguous. FWDekker agrees for nine of the eleven builders that
emit a value. The blank now splits on the item's shape - see the
line-order block above - and desktop gate step 6 was corrected, since it
had told the tester to confirm the divergence.

**3. A tooltip CLEAR could be undone by the next re-stamp - FIXED.**
`TooltipFacility.Register` inferred a source's fallback text from
`control.BasicTooltipText`, a field the facility itself nulls on the
first stamp, so a null could not be told from a deliberate clear and the
previous source's note was carried forward over it.
`MainView.FitRowTextLabel` clears a row line's tooltip the moment the
line fits and then re-stamps the row's deferred builder, so widening the
window past a Snapshot row's truncation boundary resurrected the full
item name - and with no stat block for that item yet (a plan restored
from disk before the Q13 top-up) the builder returns empty content, so
the row showed its own full name as a tooltip over the name it was
already showing in full. `ApplyPlain` now records the caller's intent -
clears included - on any source the control already carries. It costs
one `ConditionalWeakTable` lookup per plain tooltip assignment, on the
render path. Not unit-testable: `TooltipFacility` takes a Blish
`Control` and tests stay Blish-free, so desktop gate step 12 carries the
check and now names the no-stat-block precondition it needs.

### Post-review corrections, round 3

**1. The round-2 blank-above-the-value table cited evidence that does
not exist, and miscounted its source twice - FIXED.** Re-read
`tooltip-research/fwd-ItemTooltip.js` line by line. `tooltipString` has
FOURTEEN builders, not thirteen (Armor 433, Back 457, Bag 477,
Consumable 494, Container 513, Gathering 531, Gizmo 548, MiniPet 563,
Tool 580, Trinket 596, Trophy 618, UpgradeComponent 635, Weapon 653,
Generic 680), and only ELEVEN of them call `getValue()` at all (448,
468, 485, 504, 522, 554, 609, 626, 644, 670, 687). So "twelve of its
thirteen builders agree" was wrong in both numbers: the true figure is
nine of the eleven that emit a value. Worse, `Gathering`, `MiniPet` and
`Tool` emit NO value line whatsoever, so they cannot agree with either
shape - yet round 2 had listed all three in the no-blank arm on the
strength of that same sentence, while the arm's own doc comment said an
unknown shape falls to Generic. They are now their own labelled arm,
still contiguous but on a stated nearest-body-shape inference rather
than a borrowed measurement, recorded under "Accepted divergences" as
the one guess in the table, pinned by a Theory test that says so, and
handed to desktop gate step 6. The counts are corrected here, in
`ItemStatTooltipComposer.ValueSitsAfterABlank`'s doc comment and in
`ItemStatTooltipComposerTests`.

### Desktop gate (live, required)

1. Hover a crafting material row in the recipe tree. The box opens with a
   ~34x34 FRAMED ICON and the item name beside it, vertically centred on
   the icon - not a name-only first line.
2. The box itself: square corners, a 1px near-black border on all four
   edges, a faintly translucent black fill with the scene visible through
   it, and NO Blish tooltip art (no rounded/lit texture, no gradient edge
   bands). Every glyph carries a dark halo.
3. Hover an item with attributes (any ascended armour piece): the
   attribute lines are WHITE, and so are the rarity word, the type, the
   weight class and the binding line. Nothing in that block is grey.
   Only the NAME carries the rarity colour.
4. Hover a Superior Rune: all six `(N): <bonus>` lines are LIGHT BLUE,
   not green, none greyed, and there is no `(x/6)` counter.
5. Hover an item whose description has a flavour run (Zojja's anything):
   the flavour is pale TEAL and any unmarked description prose beside it
   stays white.
6. The vendor value is the LAST line, with no "Vendor value:" label,
   per-denomination tints, and coin ICONS to the RIGHT of their numbers.
   On a sellable weapon, armour piece, food or rune it sits directly
   under the line above it with NO blank row (measured on steak.png); on
   a crafting material there IS one blank above it (inferred, FWDekker's
   Generic builder). On a NoSell item (Bolt, a Rebreather) there is no
   value line and no trailing blank at all. THEN settle the one guess in
   the table: hover a Copper Mining Pick (Gathering), a Master's Salvage
   Kit (Tool) and any sellable miniature (MiniPet) and record whether a
   blank sits above the coin row. Nothing measures these three - if the
   game shows a blank, move them to the default arm in
   `ValueSitsAfterABlank` and flip
   `ATypeTheReplicaGivesNoValueLineIsGuessedContiguous`.
7. A Basic item (Mithril Ore) shows NO "Basic" line; an Exotic one shows
   "Exotic" in white.
8. Coin icons in the tooltip are noticeably smaller than the plan
   tables' - about 0.8x the line height - and the plan tables' own coin
   icons are UNCHANGED. Prose rows are one line pitch apart with no extra
   padding.
9. Flavour text wraps at roughly 350px, not 500. Plain tooltips
   elsewhere in the module are unchanged.
10. Hover a row in Used Materials, a row in the Shopping List (name,
    quantity, source tag, Each cell, Total cell, and the icon), and a row
    in the Snapshot result list. All show the same rich item tooltip. A
    Shopping List row with a currency cost still shows its HAVE/NEED
    lines, after the stat block and one blank.
11. Id-space gate: hover a CURRENCY shopping row and a currency
    cost-component leaf in the tree. Neither may open with an unrelated
    ITEM's name, rarity, type or vendor value.
12. Resize the window across a row's truncation boundary with the cursor
    held still on that row: the tooltip's name must appear exactly once,
    never twice, on either side of the boundary. Do it once on a
    Snapshot row whose stat block has NOT arrived (restart so the plan
    restores from disk, and hover before the Q13 top-up lands): once the
    row is wide enough to show its name in full, hovering it must show
    NOTHING rather than that same name repeated as a tooltip.
13. Q13: restart Blish so the plan restores from disk WITHOUT
    regenerating. Within a few seconds of the tab opening, hovering a
    tree row shows the full stat block - no Generate needed. Confirm the
    first hover before the fetch lands is the plain fallback and never an
    empty or flickering box.
14. Confirm no tooltip anywhere shows a raw item id, currency id or
    vendor id.
15. Hover a FOOD item (a Cup of Lotus Fries, any feast) and a UTILITY
    consumable (a sharpening stone). Its nourishment lines start on the
    line immediately under the header - no blank between the name and
    the first effect - and they are WHITE, the same white as the type
    and level lines below them, NOT the runes' light blue. An item with
    no combat facts and no nourishment (a back item, a crafting
    material) still opens with one blank.
16. Hover an item whose icon never loads or whose entry has no icon at
    all. The header shows the neutral dark empty-slot square with its
    "-" mark, the name sits beside it in the same column every other
    tooltip's name sits in, and the body lines below start at the left
    padding - the name must not float over an empty gap.
17. Restart Blish so the plan restores from disk, and BEFORE the stat
    top-up lands hover the ICON of a Used Materials / Shopping List /
    Snapshot row whose icon is missing. It must still say "No icon
    available for this entry." - never an empty box. After the top-up
    lands the same hover shows the full stat block.

Gate: PASS (2026-08-23 night desktop session, branch build at a291fb1,
captures preflight/gTT1-gTT28). Verified live: the full ascended block
(Wupwup Claymore - icon header with framed 32px icon and pink name,
thousands-separated strength, nine white attribute lines, one line per
infusion slot, white identity block with hand line and damage type,
teal flavour inside the block, unlabelled trailing 1g 00s 00c with
icons right, dark 0.92 canvas with no Blish art, shadowed glyphs);
rune (six light-blue (N): tiers, no counter, white Exotic word, the
Element: abilitytype span in pale yellow, white apply-description);
food (nourishment white and flush under the header, NoSell = no value
line, no trailing blank); Basic ore (no rarity line, white prose,
blank-then-value for a material, API icon in the header even though
the ROW icon is the neutral dash placeholder); Fine transmutable and
plank and dowel and mini (Exotic mini: account-bound, no value, no
ids anywhere). All four Q1 surfaces live-hovered with the same rich
box: tree rows, Snapshot list, Shopping List, Used Materials. Q13:
after a restart with no Generate, the restored plan's root hover
showed the full stat block within seconds. Recorded partials: the
Gathering/Tool/MiniPet blank-above-value guess stays unsettled (no
sellable specimen reachable in the sandbox fixture); step 11 stands on
the Blish-free id-space tests plus live UNKNOWN/mini hovers (no
currency-item row renders in the current plans); steps 12 and 17's
timing races (truncation-boundary double-name, pre-top-up icon note)
are pinned by TooltipLayoutMath/deferred-builder tests - the top-up
lands faster than synthetic input can race it. Snapshot rows without
cached stat blocks show no tooltip on an untruncated name - correct
per the no-stats fallback rule.
