> **Frozen record - 2026-08-23, branch `field-fixes-1`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Field-test fixes wave 1 (field-fixes-1)

The first feedback from outside the build loop: v0.2.0 was run in game
as a player would, and five defects came back from the field. Every
one was reproduced from the code before it was touched, and three of them
are visible in the existing gate captures - which is the useful lesson of
this wave. The captures had them all along; nobody was looking for them,
because each gate was reading for the item it was staged to prove.

- **Bug 1, the confirm dialog did not fit its own sentence - DONE.**
  `ModalDialog`'s message Label was 380px wide, centered, and never
  wrapped, so Clear Cache's ~640px sentence was centered on the label's
  midpoint and clipped at BOTH ends: `preflight/gB2-confirm-dialog`
  shows "ched account snapshot? It can only be rebuilt when the GW2 A".
  The 400px window additionally squeezed WindowBase2's left title-bar
  texture into ~200px, which rasterizes as coloured streaks behind the
  title - the misaligned-title half of the report. Both
  are fixed by adopting `ApiAccessDialog`'s proven geometry and text
  handling: 560x170 and a wrap against the content width. Blish draws
  the title itself at a fixed 80px indent in DefaultFont32 with no
  alignment control, so window width is the only lever either dialog has
  over its title bar.
  Review fix (round 1): the button line is now FIXED and the message is
  capped to the lines that fit above it, rather than the message pushing
  the buttons down. The window cannot grow to absorb a longer sentence -
  `WindowBase2` derives its content region from the region handed to its
  protected `ConstructWindow`, and `Container.ContentRegion` has no
  public setter - so pushing the buttons walked them out of the content
  region: at four wrapped lines their bottom 2px fell outside it, at five
  the pair was effectively unclickable and the title-bar X was the only
  exit. All three current callers wrap to two lines, so this was latent,
  but the old `Math.Max` guard read as protection it could not provide.
  The wrap now goes through `TextWrapMath.Wrap` with a caller-supplied
  line cap (new overload; the four-argument one still uses
  `MaxWrappedLines`), which ellipsizes the tail into the last line and
  reports it, and the dialog puts the full text on the label's tooltip
  when anything was dropped.
- **Bug 2, the currency valuation box did not read as an input - DONE.**
  The mechanic was never broken - typed digits go through
  `SettingsInputParser.TryParseCopperValue` into `CurrencyValuation` and
  out to the settings file on Save, and come back into the box on the
  next open - but nothing in the cell said so. "copper" as the
  placeholder named the unit the box holds, which reads as a label on a
  read-only field, and the grey "default 3600" beside it states a fact
  without offering an action. The unit moved out of the box into a
  "Currency"/"Copper per unit" column header over the grid (stated once
  per column), the box now hints with the currency's own default value,
  and one info line names the interaction outright. The three-state tag,
  the Ignore checkbox and every cell X are unchanged.
  Review fix (round 1): the placeholder was set once at build and never
  revisited, so an ignored currency kept a greyed default in its box
  while the tag beside it read "ignored" - the box would have stated the
  opposite of the tag. `RefreshCurrencyRowDefaultState` now owns the
  placeholder alongside the tag and the checkbox: cleared while the
  currency is ignored, restored to the default digits when it is not, so
  all three always describe one state.
- **Bug 3, the typeahead list floated far right - DONE.** The offset was
  deliberate (M15): the list was anchored past the Qty stepper so it
  would not cover the row's own quantity field or the rows below it. In
  use it reads as a detached panel with no visible tie to the box being
  typed into, so it is back to the classic dropdown position - the text
  box's own left edge, immediately below it. Transient occlusion of the
  controls underneath is what a dropdown does; the list closes on pick,
  on focus loss and on any outside click. Screen clamp and the
  flip-above-the-box branch kept; `anchorOffsetX` and
  `SuggestionAnchorGap` removed.
- **Bug 4, tree cost values sat left of the Cost header - DONE.** The
  sub-column layout (batch D) meeting the pulled-in column edge (batch
  H): the cost column reserves a trailing band as wide as the widest
  currency run any row in the tree draws, and every row's copper
  sub-column ends one gap left of it, while the "Cost" header
  right-aligns on the far side of that band. So in any tree containing a
  vendor-currency cost, coin-only rows ended a whole band short of the
  header and currency rows landed under it - `preflight/gDE1-top` shows
  the two ~80px apart. `TreeCostColumnMath.ComputeRowEdges` now
  collapses the band for a row that does not fill it, so every row's
  rightmost segment (coin run, currency run, or the unpriceable dash)
  ends on the header's own edge; rows that do draw currency keep the
  shared band and stay aligned with each other. Reserved column width
  is unchanged, so no row reaches further right than the column already
  owned.
- **Bug 5, descenders clipped - DONE.** `AutoSizeHeight` sizes a Label
  to exactly its font's measured text height, and Blish clips a control
  to its own bounds - so a descender lands in the last row of the clip
  window, which the `Container.Paint` scissor round trip recorded under
  #23 can shave off by a logical pixel. Scroll-phase and UI-scale
  dependent, hence intermittent. Reported for character names in
  Required Disciplines; also visible in the wallet/item rows of
  `preflight/ph01-snapshot-multichar`, which clips the tail off
  "Green Wood Log".
  `LabelHelpers.WithDescenderClearance` pins a label to its measured
  height plus two pixels - the clearance the Log tab's row metrics have
  carried since they were written - and is applied to the class: the
  shared label factories (right-aligned, c-table header, icon+name row,
  snapshot row text) and the hand-rolled row labels in the tree,
  disciplines, recipes, craft steps, notes, summary and fallback-text
  renderers. Not applied to pills and small tags (fixed-height chrome,
  uppercase text) or the Log tab (already clear). Row heights unchanged.
  Review fix (round 1): the helper now also pins
  `VerticalAlignment.Top`, and that is what makes a partial sweep safe.
  `Blish_HUD.Controls.Label.VerticalAlignment` is a public settable
  property whose default this module does not control; if it were
  `Middle`, growing a box by two would push its glyphs down by one while
  an unswept sibling on the same row stayed put - "Craft " and "12x " on
  one baseline, the item name a pixel below, which is worse than the clip
  it fixes. `Top` puts both pixels below the glyphs, so a swept label
  renders at exactly the y it did before: additive clearance, never
  motion. The prefix labels that share a baseline with a swept one are
  swept too, so every label on one row now has the same box shape -
  the craft-step row's "Craft "/"12x ", the tree row's quantity prefix,
  the shopping list's own "Item" header (it builds its header by hand
  rather than through `CTableHeaderRenderer`, so it did not get the
  treatment its Amount/Each/Total siblings got for free) and the
  shopping-list and used-materials quantity cells. Rich tooltip text
  spans were swept as the same class - they carry item and character
  names too.

Build 0 errors, 2102 StyleCop warnings (2082 before this wave; the 20
added sit in the same rule families the codebase trips throughout -
trailing commas in multi-line initializers, comment spacing). Suite 2197
passed / 0 failed (2186 baseline, +11: six on the new per-row cost edges, one on
the currency column header, four on the caller-supplied wrap line cap; the
stale "copper" placeholder width test was rewritten against the real defaults
table), tree clean, nothing pushed.

Desktop gate items, one per bug:

1. Open Snapshot, press Clear Cache: the confirm reads its whole
   sentence with margin on both sides, the title bar draws clean, and
   Discard/Cancel sit on one line. Repeat for the Crafting Plan tab's
   regenerate confirm and the Log tab's Delete Log File - both still fit
   on one line. Confirm/Cancel sit at the SAME height in all three (the
   button line no longer moves with the message length). With the confirm
   up, a click on a checkbox behind it is still eaten by the backdrop, and
   Escape still cancels.
2. Settings > Currency Valuations: each grid column carries a
   "Currency"/"Copper per unit" header, a currency with a default shows
   that number greyed in its box, typing a number and pressing Save
   persists it (tag flips to "was N"), and clearing the box and saving
   restores "default N". Tick Ignore and Save: the tag reads "ignored"
   and the box is EMPTY (no greyed default contradicting it); untick and
   Save and the greyed default comes back. Narrow the window to one
   column and the header follows.
3. Crafting Plan: type in an item search box - the suggestion list opens
   directly under that box, left edges flush, and closes on pick and
   on a click outside it. Drag the window to the right screen edge
   and repeat: the list stays fully on screen.
4. Generate a plan with a vendor-currency cost in it (Mystic Clover
   does): in the Recipe Tree, gold-only rows, mixed coin+currency rows
   and any unpriceable dash all end on the same x as the "Cost" header's
   right edge. Drag-resize the window and they still do.
5. A plan whose Required Disciplines rows carry character names with
   descenders ('y', 'g', 'p'): the tails render whole, at more than one
   scroll position. Same check on the Snapshot tab's item rows ("Green
   Wood Log") and the plan's shopping list. Then read the baselines
   WITHIN a row, which is what the clearance could have broken: a craft
   step's "Craft 12x <name>" is one unbroken line of text, a tree row's
   "12x <name>" likewise, and the shopping list's "Item" header sits on
   the same line as its Amount/Each/Total headers.

Gate: PASS after one gate-found fix (2026-08-23 desktop sessions,
captures preflight/gA1w-gA7w). (1) Modal: the FIRST gate run showed
the second wrapped line clipped mid-glyph - AutoSizeHeight with a
fixed Width takes Blish's stale-layout-pass measure; fixed in
cf193ea by adopting ApiAccessDialog's auto-size-both-and-parent-last
shape, re-gated: both lines fully visible ("...when the GW2 API is /
reachable."), buttons anchored, title-bar chrome clean at 560px.
(2) Settings: the typed-override path was LIVE-PROVEN end to end
for the first time - typed 5 into Karma's box (placeholder shows
the row's default digits under the new Currency / Copper per unit
headers), Save produced the green dated label and the tag flipped
to "was 1"; override then reverted to keep the fixture canonical.
(3) Typeahead: eight results dropped directly under the search box,
left-aligned with it. (4) The root row's coin run ends under the
Cost header's right edge (previously ~80px short); currency rows
unchanged. (5) Zoomed crop confirms full descenders on "Log",
"Augur's" and "Mystic" where the ph01 capture shows clipping.
Blish's fixed 80px title indent (title cannot be centered without
reimplementing window chrome) is recorded as the accepted limit;
the Emblem option noted for a future decision.
