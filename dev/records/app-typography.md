> **Milestone record - 2026-08-24, branch `app-typography`.** Moved verbatim out of the append zone in `docs/KNOWN-ISSUES.md` by the 2026-08-25 rotation.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## App-wide UI consistency wave (app-typography)

Branched from v0.2.3, which redesigned the Crafting Plan tab alone. This
wave carries that work to the REST of the module, on five maintainer
directives. It deliberately reuses the 0.2.3 seams rather than growing
new ones: `Services/TypeRampMetrics` for the tier seats,
`Views/Rendering/UiFonts` for the fonts, `PlanContentHeightMath` for the
band heights those tiers need, `PlanRelayoutMath`'s pinned-right-edge
model for every column, and `IconControls` / `TooltipFacility` for icons
and hovers.

### A - the ramp is the whole app's, not the plan tab's

`UiFonts.Title` (18 regular) is **deleted**. It was accepted divergence
5 of the plan-view redesign, kept alive only because restyling Settings
and About was "a second redesign"; this is that redesign. Nothing in the
module resolves 18-regular any more, so the measured defect behind its
retirement - the space glyph advances 4px against 7 at 16-regular and 9
at 18-bold, so multi-word text renders with collapsed word gaps - can no
longer reach the screen through a tier seat. Every promoted role is bold
for that reason, not for a stylistic one.

Placed, each at the band height its tier's measured ink needs:

| Surface | Was | Now |
|---|---|---|
| "Account Snapshot", Settings' five section headers, About's title and its two labelled sections | Title 18 regular / Body 16 | **SectionTitle 24 bold**, 38px band, 2px rule |
| Settings' currency grid header, the Log tab's rows (previously unlabelled), the Snapshot tab's two runs | Body 16 / nothing | **ColumnHeader 20 bold** on `TableHeaderStyle.BandColor`, 32px band |
| Snapshot, Log and Settings status lines | Body 16 | **Status 18 bold** |

Two rows grow because of it: `SnapshotHeaderLayout.StatusRowHeight` and
the Log tab's own, 24 -> 26 (the Status tier's lowest ink is 23 against
Body's 21, drawn at y=2 with the 1px clearance the row has always kept).
A test asserts the clearance against the measured ink rather than
against the literal, so a future tier swap is told which number to write.

**The no-small-grey rule reached one offender outside the plan**: the
Snapshot coin caption was Caption 14 in #828282, smaller AND greyer than
the figure it labels. It keeps the grey and joins the coin run's own size
and y - one channel of de-emphasis, the same fix the plan's Disciplines
line got.

The Log tab's new header names its prefix column **"Time"**. Judgment
call: the column actually holds level, timestamp and tag, and "Time" is
what a reader scans it for; "Level / Time / Tag" is honest and reads as
a legend rather than a header.

### B - one item-icon component

Reported: *"item icon displays are not using a standardized code path
everywhere.. some places give the tooltips, others do not, some places
give colored borders, other places just use the icon image with no
border"*. The inventory, taken at v0.2.3:

| Site | Frame | Hover |
|---|---|---|
| Recipe Tree, Used Materials, Shopping List, Crafting Steps, Required Recipes, plan header, rich tooltip header | rarity | tree/materials/shopping: yes. **Crafting Steps, Required Recipes: none** |
| Snapshot item rows, Snapshot wallet rows, item-search dropdown, Total Cost currency table | **none** | items: rich. **wallet, dropdown: none** |
| Inline coin/currency runs (`CoinCurrencyRenderer`) | none | currency: name. **coin: none** |

`IconControls.CreateItemIcon` IS the framed builder now (it was
`CreateRarityFramedIcon`), it takes the icon's hover text, and it stamps
that text on the frame as well as on the square and the missing-art
placeholder inside it - Blish resolves a tooltip on the deepest control
under the cursor and never bubbles, so each was its own hole.
`CreateUnframedIcon` is the one remaining unframed path, named so it
cannot be picked by accident.

Every site above is routed through it except two, both deliberate and
both stated here rather than left to be re-discovered:

- **the inline coin/currency runs stay UNFRAMED** (they still go through
  the component - see the review fixes below). A frame adds 2px to every
  segment's advance, and that advance is a term in the module's own
  minimum-window-width derivation; it would also draw a rarity border
  around a denomination that has no rarity.
- **the About tab's module icon.** It is the module's logo, not an item.

Each converted site passes its art size INSET by the border rather than
growing its box, so no layout arithmetic moved anywhere. Crafting Steps
and Required Recipes gained `ApplyPlainToIconTree`, so their row note now
covers the biggest target on the row.

**Snapshot rarity comes from the session stat cache**, which is the only
source that tab has: `AccountSnapshot` carries no rarity and is
schema-guarded against gaining fields (see `ItemStatBlock`'s own note on
why stats are a session side channel). A row whose block has not been
fetched frames NEUTRAL rather than guessing, and picks up its colour on
the next rebuild. Consequence, stated plainly: on a fresh session most
Snapshot frames are the unknown-rarity grey, and the uniform treatment -
not the colour - is what this directive buys there.

### C - the Snapshot tab's layout

Reported: *"the snapshot tab for sure needs the layout overhaul because
it has no tooltips and the same icon issue"*.

The results were a grid of unlabelled two-line cards. They are two
sortable tables now, each with a SectionTitle band and rule ("Items",
"Currencies") and a ColumnHeader band under it. Both bands span the full
grid and track the panel; a run with no rows is ABSENT, not an empty
heading over nothing.

- **The header band carries one label pair per grid column**, on the same
  x's as the cells beneath it. The Settings currency grid already labels
  a multi-column grid this way; the alternative labels columns two and
  three with nothing.
- **The count is a column, not a prefix.** "30x Mystic Clover" became
  `Mystic Clover ... 30x` with the amount right-pinned, because a
  quantity a reader can SORT by has to line up down the column rather
  than move with each name's length. The name is the only part of a cell
  that flexes, its budget stops at the Amount band, and the band is
  `max(widest amount, its own header label)` - the header-floored rule
  the plan tables needed once headers went to 20 bold.
- **`MinColumnWidth` is re-derived term by term** for the new cell:
  40 icon column + 45 chars x 9px + 12 gap + 79 amount floor + 8 pad =
  **544** (was 516 for a 52-char name-plus-prefix run). Two columns still
  fit the 1252px grid the 1378px window minimum leaves; a third now needs
  a 1758px window rather than 1674.
- **Sorting** goes through the existing `TableSortState` cycle
  (asc -> desc -> back to the search's own order), one state per run,
  session-sticky across a tab switch, in a Blish-free
  `SnapshotTableSorter` shaped like `PlanTableSorter`. A click re-PLACES
  the rows it already has rather than rebuilding them - see the review
  fixes below.
- **Hovers.** Wallet rows had none at all and now carry the currency name
  on the panel, the name label and the icon. Item rows had the deferred
  rich path already but showed NOTHING for the common case - see the
  "Follow-up: snapshot rows without plan-cached stats" note above - and
  now always head with the item's name and always carry the full source
  breakdown. **The on-hover metadata fetch that note offers as the other
  candidate fix was NOT taken**: it is a network call on a hover path,
  and snapshot fetch triggering belongs to a sibling branch.

Blish-free and tested: `SnapshotResultLayout` stacks the two sections
(the view writes every y itself rather than betting on a FlowPanel
re-flowing a later sibling - the reason the two runs already shared one
panel), `SnapshotTableSorter` holds the comparators, and
`SnapshotItemGridLayout` grew the cell's own column edges.

### D - sortable headers are cells, not text

Reported: *"the header rows of columns that you can click to sort should
highlight lightly when you mouse over them to show that an action can be
triggered from them. also the tooltip should probably and click action
should probably trigger for mouseover of the entire column header cell,
not just the text"*.

`Views/Rendering/SortableHeaderCells` owns both halves for every sortable
table in the module. The mechanism is measured against decompiled Blish
HUD 1.3.0 rather than assumed, and it is the same finding
`PressFeedback` already records: `Container.TriggerMouseInput` raises the
CONTAINER's own mouse events first and only then walks its children, and
`Control.CheckMouseLeft` clears `MouseOver` only when the cursor leaves
that control's own bounds. The header row panel therefore sees every
move, press and click inside the band INCLUDING those over its labels,
and the cell under the cursor follows from `RelativeMousePosition`. The
wash panels are passive scenery; every handler lives on the row.

Two things do not follow from that:

- the "click to sort" note is stamped on the label AND on the cell's
  wash, because a tooltip resolves on the deepest control under the
  cursor and never bubbles - whichever of the two the cursor is over is
  the only one that can answer;
- the label's own `Click` handler is GONE (`MakeClickable` is now
  `MarkSortable`). A second handler on the label would fire alongside
  the row's for one press and cycle the sort TWICE - the exact bug the
  container-first dispatch order creates, and the reason it is called out
  here rather than left as a comment.

The washes carry `ZIndex = -1`, because Blish draws children in ZIndex
order and a wash created after its label would otherwise paint over the
text.

`Services/HeaderCellMath` does the split, Blish-free and tested: a
PARTITION rather than padded boxes, so no click lands in a dead strip
between two columns. Each boundary is the caller's own COLUMN edge where
it has one, and the midpoint between two labels only where it does not -
see the review fixes below, where the label midpoint turned out to be
the wrong rule for the columns that matter. Its degenerate cases -
labels that touch, a right-aligned label that has slid left of its
neighbour in a narrow window, a supplied boundary outside the band -
collapse rather than inverting. On the Snapshot tab every cell ends at
its own grid column's edge, and the last column absorbs the remainder
integer division leaves.

### E - tooltip translucency

From the maintainer's own in-game inventory capture. A real GW2 tooltip's
interior is NOT flat: background medians shift about 20 levels per
channel across one box - (34,38,40) at one end to (43,55,55) at the
other - because the scene behind shows through, which puts the game's
alpha nearer 0.75-0.85. `RichTooltipSurface` painted a flat
`Color(0,0,0) * 0.92f`, which reads as an opaque card beside it.

Two changes, and nothing else. **The alpha constant goes 0.92 -> 0.82**,
the UPPER end of the measured band deliberately: audit finding H6 is that
content behind a tooltip must never bleed through LEGIBLY, and 0.82
leaves 18% of the scene where the bottom of the band would leave 25%.
And **a 1px light bevel immediately inside the dark border**, which the
capture shows as a pair rather than a single edge - the chrome grey this
file already carries for the header icon's frame, at 0.22 alpha, a
highlight on the canvas rather than a second border. Both are cheaply
reversible: one constant and one call.

### Judgment calls, all cheap to reverse

1. **Log column header reads "Time"** over a level+timestamp+tag prefix.
2. **Snapshot sections read "Items" / "Currencies"**, matching the empty
   state's own wording rather than the filter dropdown's "Wallet".
3. **Snapshot item names take their rarity colour only when there IS
   one**, and keep white otherwise (revised - see the review fixes).
4. **Snapshot icons draw the plan's own 32px art in a 34px frame**
   (revised); the item-search dropdown and the Total Cost currency table
   still inset their art by the border, because on those two rows the
   box size is itself a layout term.
5. **The wash is white at 0.07 (0.14 held)** and the label keeps its
   amber hover tint, because an unsorted column shows no sort indicator
   and the wash alone is deliberately faint.
6. **Theme B's tests reach only as far as the Blish-free rule allows.**
   Its surface is Views-layer control construction; what could be pinned
   was pinned (the coin denominations' hover text), and the gate below
   is the evidence for the rest.

### Review fixes

An adversarial pass over the five themes above found six things worth
fixing. All six are in this branch; each is stated here with what it was
and why the new behaviour is the right one.

**The Snapshot's sort click re-ran the account search.** `SortBy` called
`RebuildContent`, which re-ran `SnapshotSearchResultBuilder.BuildItemRows`
over the whole account index and then disposed and recreated every
control - on a full snapshot, thousands of rows of synchronous
main-thread work to change nothing but the order. It also dropped the
scroll position (Blish's `Scrollbar` zeroes itself when the content
height changes) and replaced the header panel under a stationary cursor,
which leaves `MouseOver` false until the mouse moves - the exact
stale-hover class `HoverChainResync` exists for, made easier to hit by
theme D's whole-cell target. The plan view had already been fixed for
this class (`RerenderForSortChange`).

The fix is not to copy the plan's scroll-preserve machinery but to
remove the need for it: the cells are held in the SEARCH's order and a
click derives a placement ORDER over them
(`SnapshotTableSorter.ItemOrder` / `WalletOrder`, the same comparators
`SortItems` applies). Nothing is re-queried, nothing is disposed, the
row count and grid height are identical across a click - so the scroll
offset and the hover chain are untouched by construction and no resync
call is needed. Cycling back to None restores the search's own order
without the view keeping a second copy of it.

**Two re-layout paths still measured strings.** Commit 6201777 set the
contract for the plan's headers (position-and-width work per tick,
measuring at build and settle only) and the Snapshot had not been held
to it: `PlaceAmountLabel` measured a FIXED string per cell, and
`LayoutSectionChrome` allocated a column list, two arrays and two
closures per grid column and measured both header labels. The amount
width is captured at build, and the chrome now owns a `HeaderCellPlan`
rebuilt only when the column count or a header's width changes.

*Correction to how often those ran.* This was first recorded as a
per-frame path on both counts, and it is not one. The Snapshot's
re-layout is trailing-debounced: `ResizeSettleDebounce.Schedule` stamps a
tick and returns, a ThreadPool wait loops until the settle window of quiet
and then
marshals `RefitResultRows` ONCE, so `LayoutResultGrid` runs once per drag
and once per sort click, never per pixel. `HeaderCellPlan`'s OTHER
callers - `CTableHeaderRenderer` and `ShoppingListSectionRenderer`, which
register through `ISectionRelayoutSink` - DO run per frame, because
`CraftingPlanView.ReplayRelayout` replays those closures straight off
Blish's `Resized` event. One class, two rates. The fixes stand either way
(a repeated path that allocates and measures for nothing is still worth
removing); only the stated frequency was wrong, and the dual rate is now
stated once, at `HeaderCellPlan`.

**The repack re-stamped every tooltip on the row.** Follow-on from the
same pass, and the cheap thing was removed while the expensive one
stayed: the Snapshot re-fit closure called back into the row's whole
tooltip stamp - a fresh builder closure, four `TooltipFacility.Register`
calls (each a `TooltipContentSource` allocation plus a
`ConditionalWeakTable` Remove+Add) and a recursive walk of the icon's
child tree - per row, for content no part of which is a function of the
column width. Only the two text lines were ever invalidated, and only
because `FitRowTextLabel` wrote a plain tooltip: a non-null
`BasicTooltipText` write nulls `Control._tooltip` and so drops the rich
surface stamped over it. `FitRowTextLabel` no longer writes tooltips at
all - the row owns them, as it already did for the strip, the amount and
the icon - so the repack now fits text and moves the amount, and nothing
else. The plain note those labels carried was overwritten by the rich
stamp on the same line, but it was NOT dead: `Register` captures a
control's `BasicTooltipText` as the source's `FallbackText`, and
`ResolveContent` returns exactly that when a deferred builder throws -
and the item builder calls into the session stat cache from inside
Blish's mouse-moved handler. Round 1 dropped it and called the change
behaviour-neutral on the strength of a fallback nothing could reach,
which was wrong. `CreateItemRow` now stamps the line's own text as a
plain note once, at build, before the rich stamp takes the label over:
unconditional rather than shorten-conditional as before, so the fallback
does not depend on the column width and the repack still owns no
tooltip.

**Coin icons answered no hover.** `CoinCurrencyRenderer` built its coin
icon as a raw `Panel` with a `BackgroundTexture`, entirely outside
`IconControls` - so in a Total Cost row the spirit-shard icon named
itself and the gold coin beside it said nothing. That is the module's
most numerous icon draw and the site directive B's report literally
describes. `IconControls.CreateAssetIcon` is the asset-id twin of the
unframed path (no missing-art branch: an asset id is a constant, so
there is no data gap to degrade), and `CoinSegmentMath.DenominationName`
names the three denominations beside the ids it already owns. The
inventory table above understated this and is corrected.

**Header cells stopped at the midpoint between two WORDS.** The split
took `gapStart + (gapEnd - gapStart) / 2` over the label extents, and a
header's text is a fraction of the column it names: on the Shopping List
the boundary landed roughly halfway between "Item" and "Source", so a
click above the right-hand end of the item NAMES sorted by Source.
`HeaderCellMath.LabelExtent` now carries an optional explicit boundary
(the midpoint remains the fallback), `ShoppingColumnMath` derives its
four from the same pre-scan its columns come from,
`SnapshotItemGridLayout.CellHeaderSplitX` does the same for a grid cell,
and Used Materials hands `CTableHeaderRenderer` the three terms its name
column already budgets against. The inert headers keep the fallback -
their cells answer no click and paint no wash, so a boundary between
them decides nothing.

**Snapshot names got dimmer for the common case.** The rows took
`GetRarityNameColor(RarityFor(...))` unconditionally, and `RarityFor`
answers null for any item no plan has fetched - so on a fresh session
every name dropped from white to the palette's 200-grey unknown entry,
while the frame that colour was paying for stayed neutral anyway.
Directive A's own no-small-grey rule argues the other way: the rarity
colour is taken when there IS one, white otherwise. The art also goes
back to the 32px the rows drew before the frame arrived (the box grows
to 34, which the 40px text column clears), so the visible delta on this
tab is not "smaller icons, dimmer names".

The order is always derived from the rows as the SEARCH produced them,
never from what is currently on screen, so ties still break in the
search's own order exactly as the rebuild path made them - a click is
not a compounding sort. `SortItems`/`SortWallet` are gone with the
rebuild that used them; the comparators they wrapped are what the order
is built from, and what the tests drive.

**The wave's comment ratio was over the bar.** The brief set "well under
25%" for new code and the wave measured 32% (896 comment lines of 2719
added `.cs` lines against v0.2.3). The heaviest offenders were prose
re-narrating a decision this file already records at length - the sorter's
24-line preamble on returning an order rather than a copy, restated a
third time on `MainView.SortSection`. Compressed to the invariant plus a
pointer at each canonical site: the wave is now 22% (522 of 2342), and
every file the wave ADDED is under 25% on its own
(`SnapshotResultLayout` 16%, `SortableHeaderCells` 19%,
`SnapshotTableSorter` 20%, `HeaderCellMath` 23%, `HeaderCellPlan` 24%).
`MainView`'s share of the wave is 26%, against that file's own 43% at
v0.2.3. What survived is measured numbers (the 79px header floor, the
544px column, Blish's container-first dispatch order and its
`BasicTooltipText` setter) stated once where they are used.

### Out of scope, untouched

The sibling `field-fixes-3` branch owns the Total Cost zero-band
retention rule, scroll anchoring across re-solves, the click-sound
default, the UNKNOWN Mystic-Forge recipe fallback and first-load snapshot
triggering. None is touched here. `Services/PlanViewModelBuilder.cs` and
`Views/CraftingPlanView.cs` are untouched apart from one mechanical
rename of the icon component in the latter.

### Desktop gate

1. **The ramp, on each of the four tabs.** Snapshot: "Account Snapshot"
   at 24 bold over a full-width rule, the status line at 18 bold,
   "Items"/"Currencies" at 24 bold, "Item"/"Currency"/"Amount" at 20 bold
   on the dark band. Settings: five section headings at 24 bold, the
   currency grid's "Currency"/"Copper per unit" on a banded 20-bold
   header, the save-bar status at 18 bold. Log: status at 18 bold,
   "Time"/"Message" on a banded 20-bold header. About: the module title
   and both "Disclaimer:"/"Credits:" headings at 24 bold. Nothing
   anywhere renders multi-word text at 18 REGULAR (the collapsed-word-gap
   defect); nothing renders a name at Caption grey.
2. **One icon treatment everywhere.** Three formerly-inconsistent sites,
   side by side with a plan row's icon: (a) a Snapshot item row, (b) a
   Snapshot wallet row, (c) the item-search dropdown under the Crafting
   Plan tab's search box. All three must show the same 1px frame, and all
   three must answer a hover. Then hover a Crafting Steps icon and a
   Required Recipes icon - both must now show the row's own note, which
   they never did. Finally hover the GOLD COIN icon in a Total Cost row
   and then the spirit-shard icon beside it: the coin must name its
   denomination where it used to say nothing. Snapshot item names must
   read WHITE on a fresh session (no plan generated), not grey.
3. **Snapshot full-width tracking, at several widths.** At the 1378
   floor, at ~1600, and maximised: both section rules and both header
   bands must run the full width of the result panel at every width, the
   Amount column must stay pinned the same distance from each cell's
   right edge, and the header pair over EVERY grid column must sit on the
   x's of the cells beneath it (check the rightmost column especially -
   it absorbs the division remainder). Drag slowly across the 2 -> 3
   column threshold (~1758px window) and confirm the third column's
   header pair appears with it.
4. **Snapshot item tooltips.** Hover an item row for something never
   planned this session: a rich tooltip must appear, headed with the
   item's name and carrying the full source breakdown. Hover a wallet
   row: the currency's name. Hover a wallet row's ICON: the same. Then
   generate a plan containing one of those items, return, and hover it
   again - the full stat block must now head the box instead.
5. **Sortable-header hover wash and whole-cell click, on a plan table AND
   the snapshot.** On Used Materials: move the cursor into the header
   band well AWAY from the word "Item" - the whole left cell must wash
   and show the "click to sort" note, and a click there must sort. Same
   for "Amount" on the right. Confirm ONE click cycles the sort ONE step
   (a double cycle is the regression this design has to avoid). Repeat on
   the Snapshot items header. Confirm the Recipe Tree's inert "Source"
   header does NOT wash.
   **Then the column boundary specifically**, on the Shopping List: hover
   the header directly above the right-hand end of the longest item NAME.
   The Item cell must be the one that washes and the one that answers the
   click - it used to be Source from about halfway across the names.
6. **The Snapshot sort click keeps its place.** Take a full snapshot,
   filter All, empty search, and scroll down to the Currencies run. Click
   its Amount header: the run must re-order with no perceptible stall,
   the list must NOT jump to the top, and the header under the
   still-stationary cursor must stay washed (no mouse jiggle needed).
   Click twice more - descending, then back to the search's own order.
   Do the same on the Items header with the list scrolled deep, and
   confirm the Currencies run's order is untouched by it.
7. **Tooltip translucency against the capture.** Open the maintainer's
   own inventory screenshot beside a module tooltip over the same kind of
   bright scene: the interior must no longer read as an opaque card, the
   dark border must have a light line immediately inside it, and NO text
   behind the tooltip may be legible through it (audit H6).

Gate: PASS (2026-08-25 desktop session, branch build, captures
preflight/gTY1-gTY15; display-sleep inhibitor held the session).

A. RAMP: Settings, Log and About now carry 24-bold section titles with
   rules where they previously had one 18-regular size and, on Log, no
   headings at all. Settings reads as a hierarchy ("Sound", "Homestead
   Refinement", "Logging", "Snapshot"); About's title and both labelled
   sections match; Log gained "Time"/"Message" column headers over
   columns that were unlabelled before.
B. ONE ICON PATH: verified on three formerly-unframed surfaces - the
   Snapshot item rows (Augur's Stone renders its ascended frame,
   Zojja's Claymore its own), the neutral placeholder on an art-less row
   (Mithril Ore), and the framed icons in the search dropdown.
C. SNAPSHOT: the overhaul is the visible one. Two-column grid with a
   24-bold "Items" title, per-column "Item"/"Amount" headers, amounts
   right-pinned to their column edge, and holdings as a sublabel under
   each name. Hovering a row now opens the rich item tooltip (account
   binding, value, holdings) - the recorded "snapshot rows have no rich
   tooltip" gap, closed without adding a network call on the hover path.
D. SORTABLE HEADER CELLS: proven on the strongest case rather than the
   easy one. Hovering EMPTY space at x=400, far from the "Item" text,
   lit the whole cell's wash and tinted the label; CLICKING that empty
   space sorted the table ("Item ^", Augur's Stone first). The Amount
   cell behaves the same (1x, 5x, 30x, 42x ascending with its own
   indicator). No dead strip between the two columns.
E. TOOLTIP TRANSLUCENCY: 0.82 alpha plus the 1px inner bevel ships; the
   rich tooltip over the Snapshot list reads as a translucent panel
   rather than the old flat slab, and text behind it stays illegible
   (audit H6 holds).

Recorded, NOT a defect in this wave: these tabs now have the plan tab's
TYPE hierarchy but not its LAYOUT - Settings, Log and About are still
left-packed, with the panel's right half empty, because this wave scoped
the ramp plus Snapshot's grid rather than a per-tab redesign. The
maintainer saw the same thing on the Settings capture and asked for the
full treatment on every tab; that is its own milestone, not a fix to
this one.



---

## Amendment - 2026-08-28: a promoted tier may seat on ROW CONTENT when the row's own art sets its height

The placement table above seats every promoted tier on CHROME - section
titles, column headers, status lines - and nothing on the content of a
row. That reading was applied literally once since: a request to promote
the Plan History tab's row name was declined, citing this record.

The maintainer has now ruled the other way for the Crafting Ranker
(2026-08-28, branch `ranker-columns`):

> "i feel like the font size on crafting ranker for 'materials,
> currencies, etc' could be a touch larger for better legibility - maybe
> the item name goes up a size class and these labels go to the same size
> as item name - given the icon size the item name font size has room for
> it vertically to be larger"

The rationale in that last clause is the rule, not the exception. A
Ranker row is 60px tall because it carries a tier-1 bag-slot item icon
(`RankerRowLayout.RowHeight` = `ItemIconTiers.BagSlotIconSize` + 3px
clearance each side), and a Body 16 line box is 20 of those 60. The
height was already paid for; refusing to use it is not restraint, it is
waste. So:

| Ranker surface | Was | Now |
|---|---|---|
| Item name | Body 16 | **Status 18 bold** |
| Gate strip labels and percentages (Materials / Currencies / Time gates / Disciplines / Recipes) | Caption 14 | **Body 16** |
| Readiness percentage | Status 18 bold | unchanged - it now draws white inside its own bar |

**The rule, stated so the next reader does not have to infer it:** a
promoted tier may seat on row content when the row's own ART, not its
text, sets the row height - and only up to the height that art already
bought. Where a row's height is set by its text (the Log tab's rows, the
plan's currency table), the original reading stands and a promotion there
would grow the row.

The rhythm is derived rather than re-picked: `RankerRowLayout.MainLineY`
centres any line box in `RowHeight`, and every seat on the Ranker's main
line - rank caption, name, days, coin run, readiness bar, buttons, chip -
resolves its y through it. A further tier change moves one expression.

NOT extended to the other tabs in that branch. Plan History, Snapshot and
Log rows keep Body 16; whether the same argument applies to them is a
separate call, and their row heights are not all set by art.
