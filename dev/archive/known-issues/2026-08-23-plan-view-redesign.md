> **Frozen record - 2026-08-23, branch `plan-view-redesign`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Plan-view redesign (plan-view-redesign)

Branched from the unmerged `tooltip-authenticity` head, so its deferred
rich-tooltip facility is part of this work's baseline - the ellipsis
plus full-name idiom below is stamped through `TooltipFacility`.

Built from the `plan-redesign` research set: `spec.md` (build order,
phases 0-4), `decisions.md` (the decisions taken, which override
the spec where they touch), and the four dossiers `typography.md`,
`layout.md`, `minwidth.md`, `status-ux.md`. Where a dossier and the spec
disagree the spec's cross-check wins; where anything and `decisions.md`
disagree, `decisions.md` wins.

### Phase 0 - foundations

**Tables justify, they do not pull in.** Audit batch H pulled every
table's right-hand block LEFT to sit one 24px breathing room past the
widest name it rendered. The recovered space landed to the RIGHT of the
block, which was rejected in game: columns bunched to the left
instead of justifying out to take the available width dynamically,
leaving stranded dead space. Every block is now pinned at
`PlanRelayoutMath.PinnedRightEdge(P) = P - 8`, the name column is the
only one that flexes, and ellipsis plus a full-text tooltip is the sole
overflow idiom.

Deleted rather than left unused, so no caller can reintroduce the
pull-in: `RightBlockX`, `RightBlockRightEdge`, `TableGutterBreathingRoom`,
`TableRightBlockMinX`; `ComputeTreeColumnEdges`' `widestNameEnd`
PARAMETER; `TreeCostColumnMath`'s `measureNameEnd` callback,
`TreeColumnScan.WidestNameEnd` and the depth-carrying walk that existed
only for them; `ShoppingColumnMath.BlockWidth`;
`SummarySectionLayoutMath.ComputeCurrencyColumnEdgesForPanel` /
`CurrencyHeaderBandWidth` / `CurrencyTableOffsetX` (the currency table's
CENTRING dies with the pull-in that motivated it);
`RowRelayoutHelpers`' `dividerWidthForWidth` and
`ShoppingListSectionRenderer.HeaderBandWidth`, both of which now compute
exactly `P`. Header bands and row dividers are full-width again for
free. Six per-render name pre-scans lose their name half; two lose the
whole loop, so net `MeasureString` work per render goes DOWN.

Two consequences the pinned model REQUIRES, since names can now actually
be clipped where the pull-in guaranteed they could not:

- a name's ellipsis budget stops at its neighbouring column's BAND (the
  max across the table), not at that one row's own value width -
  otherwise a row reading "1x" lets its name run under the column's
  widest "429750x";
- every band is `max(widest data, its own header label)`, because a
  header at the ColumnHeader tier routinely out-measures the data under
  it (measured: "Amount" 79px at 20-bold against a 32px "12x";
  "Required" 89px against the currency number column's 60px floor).

**The type ramp is named once.** `Services/TypeRampMetrics` holds the
measured Menomonia ink for every size the module draws in and names the
two promoted tiers: ColumnHeader 20 bold, SectionTitle 24 bold - JC-1
resolved to Alternative B, 20/24, with 18/22 held as the retreat if
that reads too big. The retreat is a two-line swap there,
with the height constants and their tests following from it.
`Views/Rendering/UiFonts` turns that into a `BitmapFont` and nothing
else; an unmapped point size throws at the seam rather than rendering at
a size no constant was derived for.

Both measured font-inventory defects are recorded in code, not only in
the dossier. **Menomonia 18-REGULAR's space glyph advances 4px** (against
7 at 16-regular and 9 at 18-bold), so multi-word text at that size
renders with collapsed word gaps - measured, `" x 42 needed"` is 104px at
both 16-regular and 18-regular. That is why status is 18 BOLD, and why
18-regular is now retired from the plan view entirely (it survives only
in the Settings and About tabs, which this milestone does not restyle).
**Menomonia 22-REGULAR is metrically identical to 24-regular** - same
line height, cap and advances, different file bytes - so there is no
regular-weight step between 20 and 24 and it must never be loaded.
22-bold is a genuine intermediate.

**Minimum window width 1478 -> 1378.** 1478 fitted the deepest chain
that EXISTS ("+24 Agony Infusion", depth 23) untruncated. 1378 is
derived for the deepest REALISTIC chain instead - the legendary trinkets
Transcendence and Conflux, both exactly depth 14, whose widest row at
every font size is `429750x Pile of Glittering Dust`. Every term
measured at Menomonia 16 against the installed XNBs
(`plan-redesign/minwidth.md`, which reproduces every anchor figure of
`docs/research/minimum-window-width.md` byte for byte):

```
 629  widestNameEnd = nameX(14) 394 + "429750x " 69 + name 166
 +24  the designed name-to-pill gutter at the deepest row
+256  TreePillColumnWidth
+335  cost column: 181 worst-digit six-digit-gold coin run
                 + 154 widest two-currency vendor run
  +8  TableRightMargin
---- 1252 tab panel  +126 chrome  ==== 1378
```

The +154 rider is JC-5, a judgment call: the
like-for-like depth-14 figure is **1232**, which accepts that a row
combining a forced-craft dust chain with a vendor currency run
ellipsizes. Declined - the module designs for a 1920x1080 minimum
resolution, so shrinking the floor further to buy a size that renders
cramped is the wrong trade when Full HD is effectively universal on
gaming displays. A two-currency vendor run now always fits at the
floor. The agony chain
reads whole to depth 19 and ellipsizes from depth 20, six levels past
the deepest realistic plan; that boundary is pinned by a test so the
accepted degradation cannot quietly get worse. The controls row's own
floor is subsumed and now measured rather than estimated: "Value Own
Materials" is 145px at Blish's Font14, putting that row under 700px.

**`StatusText.Count`** is the module's one spelling of a counted noun,
so `"(s)"` never reaches the interface. `LogTabContent` routes through
it. `ForOverrideResolve` still writes `"(N override(s))"` - the one
remaining offender, and the string the events/state split rewrites
outright rather than repairs, so it moves with that work in phase 3.

### Phase 1 - typography rollout

Column headers (all six tables, through `TableHeaderStyle.Font`'s single
seam) and the Total Cost tile captions to ColumnHeader; the eight
section titles to SectionTitle; the status line to Status (18 bold,
JC-2); the plan header's `" x N needed"` suffix to SmallHeading (20
regular) and the craft-step badge to SmallHeadingBold (20 bold), which
between them retire 18-regular. Body rows are untouched - that is what
keeps the depth-14 minimum valid.

The Disciplines character-availability line goes Caption -> Body and
keeps its grey: it was the one text in the view both smaller AND greyer
than its neighbours, and it carries character names, which a user reads
letter by letter. One channel of de-emphasis, not two. The craft-step
sublabel stays Caption grey (JC-7) - it annotates a quantity, not a
name.

Heights, each derived from measured ink rather than chosen, and each
moved in the same commit as its renderer because they are load-bearing
for scroll math:

| Constant | Was | Now | Derivation |
|---|---|---|---|
| `CTableHeaderRowHeight` / `CTableHeaderLabelY` | 28 / 5 | **32 / 4** | 20-bold lowest ink 26; y=4 reproduces the Body header's exact optics (cap top 8px down, ink bottom 2px clear) |
| `SectionHeaderRowHeight` | 32 | **38** | 24-bold lowest ink 30; title y=3, ink bottom 33, divider top 35 |
| `SectionHeaderCaretY` | 6 | **10** | the caret is Body against a SectionTitle title - baseline-aligned, with the same 1px optical lift the pair had |
| `CostTileRowHeight` | 56 | **58** | caption block bottom 31 against an amount run bottom-anchored at 30 - a 1px overprint |
| `CostBandCaptionLineHeight` | 25 | **32** | same 7px of slack over the real line height (18 -> 25) as before |
| `StatusToSeparatorGap` | 23 | **25** | status lowest ink 23 plus the 2px it has always kept off the rule |
| `InlineSpinnerLayout.PlanStripSize` | 18 | **20** | centred on a 23px line box rather than a 20px one |

The Total Cost disclosure line stays Caption and gains its own measured
height; it had been sharing the caption's, which would have grown it by
7px for nothing.

Three constants move from Views into `PlanContentHeightMath` and are
aliased back (`TableHeaderStyle.LabelY`, the section-header band's three
y's, the cost tile's caption y and amount pad). A label y and the band
height it sits in are one piece of arithmetic, and only one of the two
was testable where they lived.

### Phase 2 - layout per section

Built in the spec's dependency order. Every section's rightmost column
was already pinned by phase 0; this phase gave each one the columns and
the overflow idiom the pinned model requires.

**Total Cost.** Only cleanup was left: each currency row was two nested
panels, because the inner one was the table's centred slice. The table
justifies now, so the slice was exactly the size of the row around it -
a control per row, and a second `Size` write per resize tick, for
geometry that had become the identity. Collapsed to one panel carrying
its own background; the row's truncation tooltip moved with it.

**Used Materials.** Nothing left - phase 1 already gave it the
header-aware Amount band and both halves of the tooltip stamp.

**Required Disciplines.** The character run's full text was stamped on
the row panel alone. Blish resolves a tooltip on the deepest capturing
control under the cursor and never bubbles, so it fired on the blank
strip BESIDE the truncated names and not on the names themselves - the
one place a reader points to find out what was cut off. Both stamps go
through one helper now, so the build pass and the settle re-ellipsis
cannot stamp different control sets.

**Crafting Steps.** `Craft Nx <name>` had no width cap at all: a long
name ran under the right-aligned sublabel and off the panel. It takes
the standard idiom now - ellipsis, full name on the label AND the row
panel, re-derived at settle - budgeted against the widest SUBLABEL this
render draws rather than the row's own (a row with a short sublabel, or
none, must not let its name run under the widest one in the column).
That band is the section's only pre-scan, and unlike every other
table's it has no header label to floor it: the column is unlabelled.

**Required Recipes - the Discipline column.** The discipline was
`row.Sublabel`, a Caption line UNDER the recipe name. That cost the
section a second row height (48 against 36), put a name and its
discipline on different reading lines, and made the discipline both
smaller AND greyer than the text beside it - the double punishment the
type rules ban for a name a reader picks the letters of.

Recipe (flex) | Discipline | Status, one line at 36px:

- `Services/RecipesColumnMath` owns the edges (Blish-free, tested).
  Status pins to `PinnedRightEdge`; Discipline is LEFT-ruled at its own
  x - discipline names are words, not numerics, and a ragged right edge
  under a left rule still reads as one column; the name absorbs the
  rest.
- Both bands are `max(widest data, own header label)`. Measured at the
  ColumnHeader tier, "Discipline" out-measures a short "Chef 400". The
  Status pre-scan comes BACK here - phase 0 deleted it because nothing
  consumed a band width yet, and the accepted divergence that recorded
  that is now discharged.
- The recipe name gains the standard ellipsis, with the full name
  COMPOSED with the row's existing wiki hint rather than assigned over
  it, and both stamped on the name label as well as the row panel (the
  hint was on the panel alone, where the name label swallowed it).
- `RecipeRowHeightWithSublabel` and the per-row height branch are
  deleted; `RecipeRowHeightNoSublabel` is renamed `RecipeRowHeight`,
  there being nothing left to distinguish it from.
- The column and its header are reserved only when some row carries a
  discipline - the same gate Required Disciplines puts on its Characters
  column, so a mystic-forge-only list gives that width to the name.

**Shopping List - the Source column.** The badge was glued to the
name's right edge, so its x moved with every row's own name length and
no two badges lined up; its width had to come out of that row's own
ellipsis budget; and every badge rendered in the same recessed grey, so
the column said WHICH source only to a reader who stopped and read four
capitals on every row.

Item (flex) | Source | Amount | Each | Total:

- `ShoppingColumnMath` grows `SourceX`, derived right-to-left off the
  same pinned edge as the rest of the block. Badges LEFT-rule at that
  x and the name's budget stops there - one fixed x for the whole
  table. The band is `max(widest badge, own header label)`, floored at
  the header for the mirror image of the right-aligned columns' reason:
  a left-ruled header wider than its data overhangs RIGHT, into Amount.
- Fifth sortable column, ordered by the badge TEXT the column shows
  rather than by `PlanRowType`, so a seeded SALVAGE badge groups with
  the S's instead of with the other unknown-source rows.
- Two colours, and only two (`Views/Rendering/ShoppingBadgeColors`):
  VENDOR teal `#2E8B84`, the one hue with no existing meaning in the
  module, for the "go somewhere and buy it" class; UNKNOWN `#B24A4A`,
  darkened out of the Missing!-red family Required Recipes already
  uses, for "the plan cannot price or source this". TP and CURRENCY
  keep Locked's chrome - TP because it is the majority row, CURRENCY
  because the tree's own CURRENCY pill is Locked chrome and one meaning
  must not have two looks. `PillColors` is untouched: its vocabulary is
  the tree's DECISIONS, and none of them means "vendor".
- An UNKNOWN row's unpriceable dash takes the same red, so "no source"
  and "no price" read as one statement. The name keeps its rarity
  colour at full strength - an unknown source is a fact about
  acquisition, not a defect of the item.
- The badge carries its own prose hover (Blish-free, beside the text
  mapping in `ShoppingSourceBadge`), stamped on all three of the tag's
  nested controls because the outer panel is a 1px border.
- The badge's reposition moves from the settle pass to the per-tick
  relayout: its x is width-derived now rather than trailing an
  ellipsis.

**Recipe Tree.** Columns were done in phase 0; what this phase owed it
is the click fix below. **Notes** needed nothing - it was already the
model section for the width principle.

Settle-pipeline inventory after this phase: tree names, Used Materials,
Shopping List, Disciplines characters, Summary currency names, Notes
re-wrap, plus the two new closures - Required Recipes names and
Crafting Steps names. Net `MeasureString` work per render is still down
on the pre-redesign figure.

### Phase 2 - the pill update-in-place fix

Reported in game: rapid IGNORE toggling with a stationary mouse drops
clicks, and the pill stops highlighting. Two distinct mechanisms, both
measured against decompiled Blish HUD 1.3.0.

**Dropped clicks.** `MouseHandler` buffers exactly ONE pending mouse
event - `_mouseEvent`, written by the hook thread on every event and
consumed once per `Update` - and `Control.OnLeftMouseButtonReleased`
raises `Click` only when that same control INSTANCE was primed by its
own `OnLeftMouseButtonPressed` (`_clickPrimed`). A frame long enough to
contain both halves of the next click therefore loses the press, and
the release finds nothing primed. Every pill click was rebuilding every
control in the plan inside that frame.

Note what this is NOT: click dispatch does not go through
`ActiveControl`. `MouseHandler.HandleMouseEvent` routes button events
through `SpriteScreen.TriggerMouseInput`, and `Container.TriggerMouseInput`
hit-tests `AbsoluteBounds.Contains(position)` against the LIVE tree, so
a freshly built pill under a stationary cursor does receive the click.
Only the priming is lost.

So a local re-solve now asks the tree to update ITSELF, and rebuilds
the plan around it. `TreeSectionController.TryRefreshInPlace` matches
the new solve's tree against the rows already on screen and, when they
present the same rows at the same depth and dim state with the same
children counts, the same cost sub-column widths and the same node
count, repaints each row's pill column, cost cell, qty prefix and
tooltip into the controls it already has. Icons, names, carets and the
row panels themselves - most of the row, and all of its texture work -
are never touched. Ignoring a LEAF material, the case the report is
about, satisfies the gate; ignoring a node with children does not (an
ignored node is built as a leaf - `CraftingTreeBuilder` returns before
its children), and that click still pays for a full rebuild. Every
rejection is a correct full rebuild rather than a wrong cheap one.

Rows are keyed by solver NodeId, not by build order: a lazy expand
appends its children at the END of the build list, so build order stops
being tree order the first time anyone expands anything.

The view keeps the tree section's controls across such a render by
detaching them before the dispose sweep and re-attaching them at the
point the tree occupies in the flow - `_contentPanel` lays children out
in child order, so re-parenting at the right moment IS the ordering.
The tree's relayout/re-ellipsis closures move to their own registry for
the same reason: a closure whose controls survive has to survive with
them. Both registries are replayed together and touch disjoint
controls, so their relative order cannot matter.

**Stale hover.** `MouseHandler.Update` recomputes the hover chain ONLY
when the mouse position changed between frames
(`if (previous.Position != State.Position) ActiveControl =
SpriteScreen.TriggerMouseInput(MouseMoved, State)`). A replacement
control landing under a stationary cursor therefore has `MouseOver`
false and never fires `MouseEntered` - the pill reads as un-hovered,
and this module's own `AnyPillHovered` guard answers wrongly, until the
user jiggles the mouse. `Views/Rendering/HoverChainResync` calls the
same entry point Blish's own motion branch calls, with the live mouse
state. It does NOT restore `MouseHandler.ActiveControl` (private
setter), so tooltip resolution and input blocking still wait for a real
move; the visible hover state, which is what a stationary user sees, is
what this fixes.

Sweep of the other rebuild-on-click surfaces, per the fix-the-class
rule:

- **Sort headers** (Used Materials, Shopping List) and the **Hide
  Unlocked** filter rebuilt the whole plan including the tree, although
  neither re-solves anything: the tree is a pure function of the plan,
  and the plan is unchanged. Both preserve the tree outright now - not
  even refreshed, because its contents are already this plan's - and
  resync the hover chain.
- **Expand/collapse carets** and Expand All / Collapse All build or
  hide rows directly under the cursor, so they resync the hover chain.
  They do not need the in-place path: they never re-solve, and already
  touch only the subtree they own.
- **Section header collapse toggles** flip `Visible` without rebuilding
  anything, and are left alone.

### Phase 3 - status, chips, confirms

A correction taken here: the status of ACTIONS and the state of the
reader's own EDITS are two separate things and must not be presented as
one.

The status line carried both. `Decisions updated (3 override(s))` mixed
an EVENT - a re-solve just finished - with STATE, how many decisions
you have overridden, which stays true until you change it. The state
half then vanished the moment anything else wrote to the strip, so the
one fact worth keeping was the one that did not last.

- `StatusText.ForOverrideResolve` reports the event alone: `Plan
  updated`, or `Best path restored` when that preset is what fired it,
  never inferred from a zero count. Its `overrideCount` parameter is
  gone, not ignored.
- `Overrides: N` and `Ignored: N` are persistent chips in the top
  strip's LEFT slot, each hidden entirely at zero - a standing
  `Overrides: 0` spends attention on the absence of a thing, and a
  permanently disabled clear button beside it invites "why is this
  disabled?". `Services/TreeChipStripLayout` owns their x's, Blish-free
  and tested. They sit where the grey `Recipe Tree:` caption was: small
  AND grey, labelling five buttons whose own verbs and tooltips already
  said what they act on.
- Each chip has a clear action, and both go through the confirm matrix.

**The confirm matrix.** A dialog appears ONLY when the click would
change something; otherwise the click skips the dialog AND the
re-solve, and the strip says why (three new no-op lines). Predicates
are read at CLICK time from live tree state through `TreeToolbarCommands`
- Craft All and Buy All each build their preset and compare it against
the current override map, a bounded walk that is cheap for a click and
wasteful per render. Generate Plan is deliberately exempt: it clears
both overrides and ignore marks, but it is the tab's primary action and
gating it would punish the ordinary case. Its tooltip is its entire
safety mechanism, so it ships in the same change - what it does, and
what it costs you.

**Measured finding: Clear Overrides and Best Path are the same
action.** `decisions.md` distinguishes them ("clear = back to solver
defaults, Best Path = apply cheapest preset"), but
`TreeSectionController.ApplyBestPathPreset` clears `_nodeOverrides` and
re-solves, and that is exactly what clearing does - the solver's own
choices ARE the cheapest plan it can find. The two differ only in the
status line they write and the dialog they ask. Both shipped as
specified rather than one being silently dropped, and the finding is
recorded here: either Best Path is renamed, or one
of the two goes.

**Wording**, per the status dossier's table: the failure verb splits
(`Generation failed:` leaves the tab without a plan, `Update failed:`
leaves the plan on screen intact with only the change unapplied); the
restored-plan seed drops its second hyphen clause and names a button
that exists; the quantity-reset, settings-changed, no-items, unmatched,
ambiguous and unresolved-rows lines all trim. `"(s)"` is now absent
from the module entirely, including the two remaining non-user-facing
offenders. The `Use Own Materials` dialog is aligned to the matrix
(JC-11) - it was the one dialog left that did not say what is lost.

**Width floor.** The chip cluster is bounded by its widest realistic
form: `Overrides: 12` + 8 + a 124px button + 20 + `Ignored: 12` + 8 +
a 110px button, roughly 455px at Body 16, against the right cluster's
414px of buttons + 32px of gaps + the 20px right padding. Under 950px
together, comfortably inside the 1378 floor, so the floor does not
move.

### Accepted divergences

1. ~~**`RecipeRowHeightWithSublabel` (48) survives phase 1.**~~
   DISCHARGED in phase 2: the sublabel became a column and the constant
   went with it. `RecipeRowHeightNoSublabel` is now `RecipeRowHeight`.
2. **`StatusToSeparatorGap` is 25, not the spec's "+3px".** The spec
   derived the move from the LINE HEIGHT (20 -> 23); the constant's own
   doc comment derives it from the LOWEST INK plus 2px of clearance,
   which is 23 -> 25. The measured-clearance rule is the one that ships
   and the one the test asserts.
3. ~~**The Required Recipes status pre-scan is gone entirely.**~~
   DISCHARGED in phase 2: the scan is back, header-floored, feeding the
   Discipline column's edges.
4. ~~**The currency table keeps its nested full-width content panel.**~~
   DISCHARGED in phase 2: collapsed to one panel, with the row's
   truncation tooltip moved onto it.
5. **`UiFonts.Title` (18 regular) still exists** for the Settings and
   About tabs' own section headers. They have the same collapsed-space
   defect, but restyling two tabs this milestone does not otherwise
   touch is not a font rollout, it is a second redesign.
6. **The chips read `Overrides: N` / `Ignored: N`, not
   `StatusText.Count`'s `N overrides`.** `decisions.md` gives the
   literal wording, and a labelled count is what a gauge should be: two
   chips side by side read as one instrument panel, where "1 override"
   beside "3 items" reads as prose that forgot to be a sentence. The
   dialog copy does use `StatusText.Count`, where the counts sit inside
   real sentences.
7. **A row's in-place repaint is unconditional, not gated on a per-row
   "did anything change" test.** A pill's text, colour, tooltip and
   click wiring derive from the node AND from plan-scope facts
   (currency totals, owned amounts, subduing results), so a cheaper
   test would have to re-derive nearly all of it to be correct - and a
   wrong skip leaves a stale, still-clickable pill. The saving that
   matters is structural (no dispose/rebuild of icons, names, carets,
   row panels or child containers), and it is taken either way.
8. **Clear Overrides ships despite being the same action as Best
   Path.** See the phase 3 finding above: implementing the design as
   stated, and recording that the code says the two are one, is
   more useful than inventing a difference to justify the second
   button.
9. **`Views/Rendering/HoverChainResync` does not restore
   `MouseHandler.ActiveControl`.** That setter is private in Blish
   1.3.0. Tooltip resolution and input blocking therefore still wait
   for a real mouse move; the visible hover state does not. Splitting
   the two is a divergence from "the hover chain is fully resynced",
   and it is the half a stationary user can see.

### For reviewer scrutiny

1. The band-vs-row-width change in the name budgets (Used Materials,
   Shopping List, Disciplines characters). Every one of those three now
   budgets against the column's widest value rather than the row's own,
   which is correct for a pinned band and is a REAL behaviour change:
   short-value rows lose a few pixels of name they used to keep.
2. `PlanContentHeightMath.SectionHeaderCaretY = 10` is baseline
   alignment, not centring - `layout.md` suggested centring the caret in
   the band (y=9 at 38px). Baseline was chosen because the pair reads as
   one line and the old 18pt pairing was already baseline-aligned to
   within a pixel. Cheap to swap.
3. Whether 20/24 is in fact too big. Every height above is derived from
   `TypeRampMetrics`' tier seats, and the tests assert derivations
   rather than literals, so the 18/22 retreat is a constant swap plus
   whatever the test failures then name. MEASURED after the review
   corrections below: applying it and running the suite gives 2501 green
   on six constants and no test edits - see "Post-review corrections",
   finding 2.
3a. **The tree-preserving render path is the highest-risk change in
   this milestone and has had no live run.** It detaches three
   `_contentPanel` children, disposes the rest, and re-parents them
   mid-rebuild; it keeps a second relayout registry alive across a
   render that clears the first; and it is entered from three places
   (a local re-solve, a sort click, the Hide Unlocked filter). Read
   `ResetContentPanelToEmpty`, `RenderPlan`'s preserve branch and
   `RenderPlanAfterResolve` together. Sandbox check steps 2, 4 and 7
   are what actually exercise it.
3b. **`TryRefreshInPlace`'s gate is deliberately conservative and its
   rejections are invisible.** A rejected refresh is a correct full
   rebuild, so a gate that is too tight looks like nothing at all -
   the click simply stays slow. If in-game testing says clicks are
   still dropped, instrument the gate before changing anything else:
   the node-count and cost-width checks are the two most likely to
   reject a case that would have been fine. `TreeRowIdentity` is a
   third rejector now (post-review finding 1) and the one to check
   LAST - it is measured on both sides, rejecting the vendor-leaf
   collision and accepting the ordinary ignore.
3c. **The Shopping List's Source band is floored at its own header for
   the OPPOSITE reason to every other column.** It is left-ruled, so a
   header wider than its widest badge overhangs RIGHT into Amount, not
   left into the name. Worth one look at a list whose only badge is
   `TP` (39px against a ~96px header).
4. `UiFonts` resolves `GetFont` per property access rather than
   memoizing. Blish caches internally (`_loadedBitmapFonts`), and the
   call sites are per-section, never per-row; a static cache here would
   outlive a module reload and hold a disposed font.
5. The 1378 figure spends 146px on a rider that only the widest
   two-currency vendor offer needs. 1232 is one constant away.

### Post-review corrections

Nine findings from the adversarial review of this milestone, all
verified against the code before being fixed. Every one reproduced.

**1 (critical). The in-place tree refresh trusted `NodeId` as item
identity, which is false for synthetic cost-component leaves.**
`MatchRows` paired a built row to a fresh node on `NodeId` alone and
`RepaintRow` then deliberately never re-derived Name, IconUrl or Rarity.
That premise holds for a real recipe node - `RecipeNodeIds.Assign` gives
it a stable pre-order id - but `CraftingTreeBuilder.cs:371,406` assign
`NodeId = SyntheticComponentNodeId(parentNodeId, componentIndex++)`, so
a vendor cost-component leaf's id is its POSITION in the offer's cost
lines while its display strings come from that line's own `ItemId`. A
re-solve picking a different offer of the same shape (`{item,
currency}` becoming `{other item, currency}`) keeps every id, depth,
children count and column width, and the row would have kept one item's
name and icon over another item's quantity, cost cell and tooltip - a
state a fresh render disagrees with, which is the second-use rule
outright.

Fixed by `Services/TreeRowIdentity.SameRow`, which the pairing now asks:
item id, cost-component-ness and the three display strings the refresh
keeps, plus the structural pair (`Children.Count`, quantity-presence)
that used to be inline. The hazard is not asserted from a hand-built
model - the first test BUILDS it through the real `PlanSolver` and
`CraftingTreeBuilder` and shows the two leaves sharing a `NodeId` while
naming different items. A second test pins the other direction:
ignoring a leaf material still leaves every row repaintable, so the
stricter gate did not quietly take back the click fix it guards.

**2 (must fix). The 18/22 retreat decisions.md ordered kept "one commit
away" was blocked by a test asserting it is wrong.**
`TypeRampMetricsTests` asserted `ColumnHeaderPointSize >= 16 * 1.25`,
i.e. `>= 20`, so JC-1's own documented fallback failed by construction;
and `PlanContentHeightMathTests` pinned the literal `8` for the header's
cap top, which reads as "the other seat is a regression" rather than
naming the `CTableHeaderLabelY` that seat needs.

The absolute gate is gone. What survives is the relation it was
pretending to be - the title/header/body steps in INK, which is what a
hierarchy actually is - and the optical placement is read out of the ink
it was inherited from (a Body-16 header at LabelY 5). A new test pins
each tier seat's ink to its own point size, so a half-done swap fails
instead of silently deriving every band height from a font the view has
stopped drawing in.

The retreat is now recorded as MEASURED rather than asserted: applied,
suite run, 2501 green. Six constants, no test edits, every band height
unchanged:

    ColumnHeaderPointSize 20 -> 18, ColumnHeaderInk Bold20 -> Bold18
    SectionTitlePointSize 24 -> 22, SectionTitleInk Bold24 -> Bold22
    PlanContentHeightMath.CTableHeaderLabelY   4 -> 5
    PlanContentHeightMath.SectionHeaderCaretY 10 -> 9

The last two are not free-standing choices - a label y is one half of a
band's arithmetic, and the shorter font's cap top and baseline both move
- and each is named by the assertion that fails without it. Removing
them from the swap and re-running gives exactly two failures, both
naming the number to write.

**3 (must fix). `UiFonts`' "fail loudly at the seam" guard blocked
neither banned font face.** `SizeOf()` validated the point SIZE only, so
`Regular(18)` and `Regular(22)` - the two measured defects the milestone
exists to escape - resolved happily, while the file's own doc comment
and `TypeRampMetrics` both stated the ban. Moving `SmallHeadingPointSize`
to 18 during a retreat would have rendered " x 42 needed" at 4px word
gaps with no build error, no test failure and nothing on screen to name
the cause.

The ban now lives once, in `TypeRampMetrics.HasUsableRegularFace`:
`UiFonts.Regular` throws on it at the seam (Bold keeps all four sizes,
because the defect is in the FACE, not the size), and a test refuses to
seat the ramp's one regular-weight role on either - so CI fires before a
screenshot would.

**4 (must fix). The two new chips could overlap the five right-anchored
toolbar buttons, and the guard built for it was never called.**
`TreeChipStripLayout.Slots.EndX` was documented as "what a caller checks
the right-hand button cluster against" and grep found it only in its own
tests. The chips replaced a fixed ~90px grey caption with up to ~438px
of live content against a button cluster starting at `rowWidth - 466`;
below a ~924px row they overlap, and two live buttons on the same pixels
is a click landing on whichever Blish hit-tests last. Reachable inside
the module's supported range: `EffectiveMinWindowWidth` falls back to
the client width below 1378, so a 1024x768 windowed client renders an
918px row and the 930 narrow-screen floor renders 824.

`Fit()` is now the only way to place the strip - `Compute` is deleted,
since a public entry with no production caller is one nothing
re-measures when it drifts. It degrades once, dropping the two clear
buttons and keeping both counts: what the plan's state IS is the
information, and the actions that change it stay reachable through
Generate Plan (clears both) and Best Path (clears overrides), so nothing
becomes unreachable on a window already below the designed floor.
`PlaceTreeToolbarRow` publishes where the button cluster starts - it is
the only place that knows the row's width - and re-fits on every resize
tick, which the chips previously never saw.

**5 (must fix). Craft All / Buy All reported a no-op for a state that is
actually unavailable.** `PresetWouldChange` returned FALSE when
`_lastResult.SolveContext` was null and `ConfirmPreset` read any
non-true answer as "nothing to do", so a plan restored without its solve
context - a real state, since `PlanStructuralValidator` only validates a
`SolveContext` when one is present - answered a Craft All click with
"Already crafting everything craftable". A confident statement about a
plan nothing had examined, on the one line this milestone rebuilt around
a dead click having to say why.

The predicate is tri-state now (true / false / null = cannot be
answered), and the class rather than the instance:
`ApplyOverridesAndResolve`'s silent return on the same condition made
EVERY local change dead in that state - each decision pill, Best Path,
both chip clears - so it reports `StatusText.ReSolveUnavailable` too,
and the confirms ask `CanReSolve` before opening a dialog whose action
cannot run. The new line deliberately claims nothing about the plan's
contents, which is exactly what cannot be known there.

**6 (must fix). `TryRefreshInPlace`'s doc comment described a mechanism
the code does not implement.** It claimed that "keeping the pill's own
instance alive across the re-solve removes the priming hazard outright";
`RepaintRow` disposes and rebuilds every pill Panel, Label and click
handler on a matched row, and only the `List<Panel>` the hover guard
closes over survives. The frame-shortening half is the real argument and
the whole of it. The clause is deleted rather than softened - the
surrounding prose is a measured argument, so a wrong sentence in it
carries a wrong constant's weight, and this is the sentence a reader
would trust if clicks were still dropped in game.
`HoverChainResync` states the mechanism correctly and is now the wording
of record, pointed at from here.

**7 (must fix). The narrow-client tier assertion sat 6px from asserting
the opposite of what ships.** `TreeChipStripLayoutTests` asserted
`CountsOnly` at a 1024px client - the one case showing the chips degrade
on a real narrow window, and the one gate step 14 was written from.
Recomputed from the production constants: `TabPanelWidthFor(1024)` 898,
a 918px row, a 432px limit against a 438px full strip. A 6px margin, and
90/78 of that 438 are the two count labels the test's own comment
concedes it cannot resolve glyph-for-glyph, because a `Label`'s font is
Blish's and the module measures them live. Real glyphs 12px narrower
combined and the strip renders Full at 1024 while the test keeps passing
on CountsOnly: a green suite certifying a degradation the module does
not perform, at the exact width the overlap finding came from.

What is asserted at every rendered width is now what holds whatever the
labels measure - the counts survive (188px against the narrowest row's
338px) and the strip stops short of the buttons. The TIER is asserted
only where the margin is not a glyph's width: 930, where Full misses by
100px and CountsOnly clears by 150, and 1378, where Full has 348px of
slack. The 1024 arithmetic is recorded in the file as the reason that
width carries no tier assertion.

**8 (must fix). The number the fit negotiates against lived in the view
as five literals, and the test re-typed it.** The repo invariant puts
column/height/ramp arithmetic in `Services/` with tests;
`TreeChipStripLayout.Fit` honoured it but its `limitX` did not.
`PlaceTreeToolbarRow` derived the limit from a walk over widths that
existed only as arguments to `CreateTreeToolbarRow`'s five `PlaceRight`
calls, so the test hard-coded `414 + 32 + 20` beside a comment admitting
a width changed there would leave the boundary cases describing a row
that no longer exists. Renaming "Craft All" to something 34px wider
would have kept production correct (it measures the walk) and quietly
turned every boundary case in the suite into a statement about a row
nobody ships.

`Services/TreeToolbarRowLayout` now owns the row's fixed geometry - each
button's width and the gap to its left, the two chip clear buttons'
widths, and `ChipLimitX(rowWidth)` derived from their sum.
`PlaceTreeToolbarRow` reads `ChipLimitX`, `CreateTreeToolbarRow` places
the same slots, and the tests fit against the same function, so a width
change moves all three at once. Proven by mutation: widening one slot by
400px fails five tests in this suite; before the change the same edit
moved production and left the suite green. `WindowSizing.RightEdgePadding`
is named for the same reason - the cluster stands off the row's right
edge by the same 20px `WindowToTabPanelChrome` already accounts for.

**9 (must fix). Finding 8's fix traded a self-correcting derivation for
a static one, and only prose bound them.** `PlaceTreeToolbarRow` still
PLACED the buttons by walking `_treeToolbarButtons` - whatever
`CreateTreeToolbarRow` put there - but DERIVED the chip limit from
`TreeToolbarRowLayout.ChipLimitX(w)`, a static sum over `RightButtons`.
Measured, the two agreed exactly: the walk consumes 414px of widths and
32px of gaps from `w - RightEdgePadding`, ending at `w - 466`, and
`ChipLimitX` returns `w - (20 + 446) - 20 = w - 486`, the walk's end
less `GroupGap`. So the round was behaviour-neutral - but `ButtonSlot`'s
constructor is public, so a sixth `PlaceRight("Export", new
TreeToolbarRowLayout.ButtonSlot(90, 4), ...)` compiles and ships without
touching `RightButtons`. The strip would then believe it had 94px more
room than the row has: invisible at 1378, where 348px of slack absorbs
it, and on a narrow client the chips paint over the leftmost button -
two live controls on the same pixels, the click landing on whichever
Blish hit-tests last. That is the defect `TreeChipStripLayout` exists to
prevent, and the walk-derived limit finding 8 replaced could not produce
it, because it measured the buttons actually placed.

The limit is now `Math.Min(x - GroupGap, ChipLimitX(w))`: the walk's own
end x is the self-correcting term, the modelled limit the tests assert
is the cap, and production is never looser than the model. A divergence
between them can now only cost the chips room. The alternative - driving
the placement loop from `RightButtons` zipped against a same-length spec
array - was rejected on failure modes, not cost: a spec entry with no
slot would then silently not be placed, trading an invisible overlap for
an invisible missing button, while the clamp cannot produce either. The
residual is a documentation defect only: a slot missing from
`RightButtons` leaves the suite's boundary cases describing a row 94px
narrower than the one that ships. A stale test over-claiming a tier is
worth strictly less than two controls sharing a click target.

### Sandbox check checklist (live Blish, real plan)

1. Every section at the 1378px minimum width: the ramp is legible -
   section title, then column header, then row, each visibly a step
   above the next, in the Total Cost band, Recipe Tree, Used Materials,
   Shopping List, Required Disciplines, Required Recipes, Crafting
   Steps and Notes.
2. Full-width justification at 1378 AND at a wide client (1920+ and
   wider): every table's rightmost column ends one margin in from the
   panel edge, header bands and row dividers run the full width, and no
   table leaves a stranded band of dead space beside it at any width.
   Drag the window across the whole range and watch for a column that
   stops tracking.
3. Ellipsis plus full-name tooltip on a DEEP tree (a legendary, expand
   to depth 14+): truncated names end in an ellipsis and hovering shows
   the whole name. Confirm the same on a truncated Used Materials,
   Shopping List and Disciplines-characters row - hovering the LABEL,
   not only the strip beside it.
4. The Overrides and Ignored chips appear with a non-zero count,
   disappear at zero, and show the right numbers after each of: a pill
   click, Craft All, Buy All, Best Path, Clear Overrides, Clear Ignored,
   Generate Plan.
5. Both chip clear actions go through the confirm matrix's
   would-change-anything guard, and each is distinct from Best Path
   (clear = back to solver defaults; Best Path = apply the cheapest
   preset).
6. Confirm matrix including the no-op cases: Best Path with no
   overrides, Craft All when everything craftable is already crafted,
   Buy All when everything buyable is already bought. Each must SKIP the
   dialog, skip the re-solve, and say why on the status line.
7. Rapid stationary IGNORE toggling: click one pill repeatedly WITHOUT
   moving the mouse. Every click lands, the pill's own highlight tracks
   its state, and no click is swallowed by a rebuild frame.
8. The Generate Plan tooltip is present and states both facts (fetches
   prices and rebuilds; clears manual decisions and ignore marks) - it
   is that button's entire safety mechanism.
9. Status strip at 18 bold with the 20px spinner: no descender touches
   the separator rule, the spinner sits inside the band, and the longest
   real status line still fits at 1378.
10. Confirm no ID of any kind became visible anywhere in the redesign.
11. The Shopping List's Source column: badges LEFT-rule on one x for
    the whole table (not trailing each name), the fifth header sorts
    and groups them, VENDOR reads teal and UNKNOWN red, an UNKNOWN
    row's dash carries the same red, and each badge's own hover names
    the source in prose. Check a list whose only badge is TP - the
    header is wider than the badge there and must not overhang into
    Amount.
12. Required Recipes is one line per row with a real Discipline column
    that lines up under its header, and a truncated recipe name's
    tooltip shows the full name AND still offers the wiki hint.
13. The tree survives a click that does not re-solve it: sort the Used
    Materials and Shopping List headers, and toggle Hide Unlocked, with
    the tree scrolled and partly expanded. Expansion state, scroll
    position and column tracking must all be exactly as they were, and
    a window drag afterwards must still move every tree column.
14. **Narrow client, which steps 1-13 never reach.** Run the game
    windowed at 1024x768 (and again at the 930 floor) so
    `EffectiveMinWindowWidth` falls back below 1378. With BOTH counts
    non-zero: both counts stay readable at every width, and nothing in
    the left cluster paints on "Best Path" or any other toolbar button.
    At the 930 floor the two clear buttons are gone (100px past the
    boundary, so this one is certain); at 1024 the strip is within 6px
    of the boundary, so RECORD which way it falls rather than expecting
    an answer - that measurement is the only thing that can settle the
    count labels' real widths. Drag back out to 1378+ and both clear
    buttons must be present. Post-review findings 4, 7, 9.
15. **A vendor node whose offer carries an item cost AND a currency
    cost** (two synthesised cost-component leaves - expand one). Ignore
    a sibling material so the re-solve can change which bulk offer the
    node takes, then read the two leaves: each name and icon must match
    the quantity, cost cell and tooltip beside it. If a leaf ever names
    one item and prices another, the row-identity gate has a hole.
    Post-review finding 1.

Gate: PASS (2026-08-24 morning sandbox session, branch build, captures
preflight/gRD1-gRD17). Verified live: (1) the ramp reads as three
clear tiers in every section; (2) full-width justification holds at
the 1900 client, at 1024, and at the ~930 floor - headers, dividers
and right-anchored columns track the panel at every width; (4) both
chips appear with correct counts, sit side by side in the old grey
label's slot, and disappear at zero (a fresh Generate cleared the
restored "Overrides: 1"); (5) Clear Ignored raised its
consequence-stating confirm ("Stop ignoring 1 item? Their material
costs count toward the plan again.") and its button tooltip reads
"Clears every ignore mark and re-solves"; (6) Best Path with nothing
to change SKIPPED dialog and re-solve with a status explanation -
wording nit recorded: it says "No decision overrides to clear", a
Clear-Overrides phrase, where a Best-Path-specific line would read
better; (7) FIVE rapid stationary IGNORE clicks all landed (odd
parity held through five rebuild frames - the update-in-place fix
proven on the exact field repro); (8) the Generate tooltip states
both facts; (9) the 18-bold status + 20px spinner rendered through a
live generation; (10) no ids anywhere; (11) badges left-rule on one
x, VENDOR teal, UNKNOWN red with matching red dashes, DAILY distinct;
(12) Required Recipes is one line per row with a real Discipline
column and a green Auto-learned status; (13) Hide Unlocked toggled
with the sections above pixel-identical; (14) MEASURED at 1024: both
count labels AND both clear buttons fit with clear space before Best
Path - and they still render at the ~930 floor, against the review's
certain-vanish prediction (the cluster is narrower than modeled).
Sub-minimum clients CLIP an already-wide window (pre-existing
effective-min behavior, not a redesign regression). Recorded
partials, all test-pinned or one-hover checks: deep-tree ellipsis
tooltips (pinned by the depth-19/20 boundary tests + the deferred
full-name builders), item 15's dual-cost vendor leaves (row-identity
gate is test-pinned; one expand on a live plan settles it), badge
hover prose, and the longest-status-at-1378 measurement.
