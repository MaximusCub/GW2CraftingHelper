> **Milestone record - 2026-08-25, branch `tab-design-pass`.** Moved verbatim out of the append zone in `docs/KNOWN-ISSUES.md` by the 2026-08-25 rotation.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Remaining-tabs design pass (tab-design-pass)

The closing note of the app-wide UI consistency wave named exactly this
milestone: those tabs took the wave's TYPE ramp but not its LAYOUT -
Settings, Log and About stayed left-packed, with the panel's right half
empty at every window width, because that wave scoped the ramp plus the
Snapshot grid rather than a per-tab redesign. Reported in game, on
the Settings capture: the type hierarchy there does not follow the
Crafting Plan pane's consistently, and every tab needs the same
treatment.

Measured before the change, at the 1378 window minimum (a 1232px panel):
the widest Settings control row ended at x=604, leaving 628px empty on
every row at the NARROWEST window the module supports; the currency grid
ran two columns of 616 holding 454 of content, and its column count was
hard-capped at two, so a wide window left hundreds of pixels dead inside
each of 47 cells; the Log tab's prefix gutter was sized from a worst-case
template (widest level + widest-digit stamp + fourteen 'w' glyphs) on
every row whatever the rows contained; About never re-read its width
after Build at all, so widening the window left the prose wrapped at the
width the tab was opened with.

Nothing new was invented. Every edge here is PlanRelayoutMath's
(PinnedRightEdge, RightAlignedX, NameMaxWidthBeforeColumn, CenterX),
every grid is SnapshotItemGridLayout's min-column-width law, every band
is max(widest data, own header label), and every overflow is ellipsis
plus a full-text tooltip stamped on the label AND its row panel.

### The cross-cutting frame

One content frame per tab: left inset 16, right edge
PlanRelayoutMath.PinnedRightEdge(contentWidth), contentWidth = the
container less WindowSizing.ScrollbarAllowance. That constant is new only
as a NAME - it replaces three private copies (LogTabContent's,
SnapshotItemGridLayout's, MainView's source-filter one), and it lives in
WindowSizing because WindowToTabPanelChrome already accounts for the same
20px there. WindowSizing.RightEdgePadding becomes the same number seen
from the padding side rather than a second definition of it, since the
chrome derivation and CraftingPlanView are written in those terms.
SettingsTabContent.RightEdgePadding and AboutTabContent's copy are
deleted; they subtracted 20 for a scrollbar and then placed everything
with no right-hand relationship at all, which IS the left-pack.

Accepted asymmetry, stated rather than hidden: 16 left, 8 right. The
right margin is PlanRelayoutMath.TableRightMargin, the module's one table
margin, reused rather than forked; the left inset is the section-title
inset these tabs already used. A maintainer who wants 16/16 moves one
constant.

### The resize split, and a regression it caught

Justifying a tab means its text now depends on the width, and the first
draft paid for that on the wrong clock: Settings' resize handler re-ran
every paragraph wrap and every name ellipsize on every resize EVENT, and
About's did the same for three paragraphs and six fact values. That is
hundreds of MeasureString calls plus fifty Label.Text writes inside a
scrolling FlowPanel, synchronously, at drag-event frequency. Before this
milestone neither handler measured a single string.

The module already had the answer twice over and this milestone now uses
it once: positions and widths track the drag live, and the half that
MEASURES text runs at drag settle. Correction to the first pass of this
work, which swept for the wrong shape and found no third instance:
it looked for width-GUARDED relayouts and so found only the two tabs it
had just written, when the predicate that matters is TEXT MEASUREMENT ON
A RESIZE PATH. By that predicate the module's heaviest instance was
already there and had been made 50% heavier by this very branch -
LogTabContent's Resized handler calls RefitRows undebounced, and the new
Tag column took it from two EllipsizeToWidth calls per row to three, so
narrowing the window over a full 2000-entry ring cost up to 6000
MeasureString binary searches per drag EVENT against 4000 on master. The
Log tab now takes the same split, which is what makes the sweep true.
Services/ResizeSettleDebounce is
MainView's own ScheduleRowRefit/RunRowRefitAfterSettleAsync pair lifted
out verbatim - one stamped clock, one in-flight waiter that re-arms
against the stamp, no cancel-and-replace timer (which costs a
CancellationTokenSource and a thrown cancellation per drag frame on the
UI thread's own event path). MainView now goes through it too, so the
module has one such debounce rather than a copy per view;
CraftingPlanView's stays separate because it steps across frames off
DoUpdate and cannot use Task.Delay. The class is Blish-free - the caller
hands it the marshal - which is what makes its behaviour testable.

All three settle callbacks are gated, because the callback is marshalled
onto the MAIN thread while Blish runs Build on a ThreadPool thread: Log
tests IsLive, Settings and About test a volatile _buildComplete cleared
at the top of Build and set at its end. About needs the flag rather than
a liveness test for a reason the other two do not share - Module keeps
ONE AboutTabContent for the module's lifetime and Blish re-runs Build on
it at every open, so a settle armed by a drag can land inside the next
open's Build, on blocks that Build has already nulled. Settings and
About also Cancel their debounce from Teardown at Module.Unload; Log
does not need to, since Blish builds a fresh LogTabContent per visit.

The lever in the views is one bool, measureText, threaded through the
layout functions so build and both halves of a resize stay one code path
rather than three. On Settings and About the Label's own
explicitly-written Height is the cache; at measureText false a paragraph
keeps the wrap it has and only its box moves. VISIBLE COST, stated: for
up to 150ms after a drag stops, a paragraph is wrapped to the previous
width and a name that no longer fits still shows its "...". That is the
same trade CraftingPlanView's re-ellipsis registry already makes.

On the Log tab the cache could not be the control, because a row's
columns MOVE on the live half while their strings stay behind: the old
skip-the-search test compared the new band against the Label's own
current width, which the live half has already updated, so a narrowing
drag would have skipped exactly the re-fit it needs. Each row now records
the width each of its three strings was fitted at, and
LogRowLayout.KeepsFitting - Blish-free and tested, since it is the thing
that decides whether text overflows - reads that instead. It holds the
NARROWEST width a whole string is known to fit in, so a drag out and back
skips both ways. Log's visible cost is its own: for up to 150ms a
narrowing column clips its text against the row panel rather than showing
"...".

Deliberately NOT added: a per-label memo that skips a re-wrap when the
budget is unchanged. Both tabs cap prose at a reading measure, so at
every width above roughly a 620px column the budget IS unchanged and the
memo would eliminate most of the settle pass's remaining work. It is
half a dozen lines and reversible; it was left out because the settle
window already bounds the work to once per drag, and the memo would put
cache-invalidation state on the views for a pass that runs once.

Services/ColumnBoardLayout packs variable-height blocks into N min-width
columns, row-major, each board row as tall as its tallest block.
Row-major rather than shortest-column masonry on purpose: masonry
balances better but re-sorts blocks as the width changes, and a settings
section that jumps columns mid-drag is worse than a ragged bottom.

### Settings

The tab's real structure is a TABLE of settings, not a form: the
setting's NAME flexes and its CONTROL pins to the column's right edge, so
every input in a column lands on one vertical line at every width. Two
columns of >= 570 fit at the floor, three at a 1710 panel, four at 2280 -
one section per column. The 570 is derived term by term (16 pad + a
22-char name floor at MaxCharWidthPx + 12 + the widest cluster on the
tab, which is the click-volume row's slider/readout/Test run, + 8), not
chosen, and the test reproduces the sum rather than asserting the total.

The unit hint and the error message shared no column with anything; they
sat at two hard-coded x's 130px apart. They are one right-pinned tag slot
now, banded at max(widest unit, widest error) across the section so the
column does not MOVE when a row fails validation - the header-floored
band rule, applied to a form. The Diagnostics row's 92-character
explanation, which used to sit at a fourth left column x=186 matching
nothing, becomes that row's own wrapped description line.

The currency grid's column cap is gone (it now uses the same
gridWidth / MinColumnWidth law its Snapshot sibling already used) and its
cell justifies: the [amount][Ignore][tag] block pins to the cell's own
PinnedRightEdge and the name is the only part that flexes. CORRECTION to
the design phase's arithmetic, since the derivation is now the test: the
re-derived MinColumnWidth is 490, not the 504 the spec computed (that sum
double-counted a term). Two columns therefore need a 980px panel, against
the floor's 1232 - nothing regresses at the minimum, and the two-column
threshold still sits far below it.

Six grey info lines above that section became two. Each dropped line was
checked against the tooltips already on the controls it describes:
"some currencies show a default estimate" is carried by the default tag's
own hover, "leave a currency unset" by the amount box's, and the
price-basis pointer moved to the section title's hover. The two that
remain are the one that says what the number means and the line that
says an amount can be typed over a default at all. This is
copy, not layout - cheap to revert.

CORRECTION, since the first draft of this section got it wrong and said
so in a code comment as well: those hovers do not repeat the dropped
lines verbatim, and one of them did not carry its line at all. The
default tag exists only on rows that HAVE a default, so on the four
curated currencies that have none (Astral Acclaim and the three Rift
Essence tiers) nothing said that leaving the box blank is a supported
state rather than an unfinished one - which is the exact confusion the
in-game note says this section already had once. The amount box's own
hover now states it on both of its branches. A comment asserting a false
invariant is worse than no comment, so both it and this paragraph now
say what is actually true.

State that only a dialog could see is now on the surface that owns it:
the save bar carries a dirty chip on the left (hidden entirely at zero,
the tree chips' own rule) and [Discard] [Save] pinned right, against the
same content width the scrolling panel below uses so Save lands on the
line the content's right edge holds. UnsavedChangeCount and
DiscardChanges both already existed and were reachable only from the
tab-switch prompt. Discard goes through the confirm matrix because the
click destroys manual edits; Save stays always-enabled, because a
disabled primary invites "why is this disabled?".

Cost of the chip, stated: it recomputes a whole-form SettingsFormState
(about 100 keys) on every keystroke in a settings box. Not a hot path,
and LoadAll suspends the refresh so one load does not run fifty of them.

The Sound section's "applies immediately - no Save needed" line became a
right-pinned tag in its own title band, in neutral Locked chrome. The
four save-gated sections carry no counterpart: a standing "Save needed"
on four of five sections is a colour that says nothing.

That rule has exactly one exception, and leaving it out was a bug rather
than restraint. The Diagnostics checkbox writes its setting straight
through on CheckedChanged and is deliberately absent from
CaptureFormState, so it applies immediately - but it sits INSIDE the
save-gated Logging section, directly above two boxes that do wait for
Save. Untagged, it inherited a promise the tab had just started making:
tick it, see no dirty chip, conclude nothing was recorded, hit Discard to
revert - and Discard cannot, because the value is already live. The tag
therefore also exists at ROW level, same word, same chrome, same pinned
right edge, budgeted into the row's name column through ClusterWidth the
way an input row is budgeted against its box. One word, two scopes; a
control tagged nowhere waits for Save, and that is now true.

The section
titles moved to the same 16px inset their own rules start at, instead of
floating 16px inside them, and each rule now spans its own column.

### Log

The prefix gutter was one worst-case string. It is two aligned bands now
- a Time band that is a genuine constant (max over the level names of
"[LEVEL] " plus the widest-digit stamp; the level names are a closed set,
so no row can widen it) and a Tag band at max(widest tag actually
rendered, its own "Tag" header label). The template existed for a real
reason, recorded in its own doc comment: the incremental append path sees
only new entries, so a content-derived width would drift from a full
rebuild's. That is answered rather than reverted - the widest rendered
tag is a monotonic high-water mark per render generation, reset by
RebuildRows (which every filter change, tab switch and Clear View already
goes through) and only ever raised by an append, where a raise re-runs
the existing suspended refit walk after the eviction trim.

Cost, stated: one more Label per rendered row (four controls against
three) and one more ellipsize per row per refit, both bounded by the ring
cap and both inside the existing SuspendLayout - and, on a resize, run
once per drag rather than once per drag event (see "The resize split"
above; a bound is not a mitigation, and this is the path where the
difference is largest). The rebuild path's new
per-row tag measuring is memoised per distinct tag string - the module
writes about a dozen tags in its whole lifetime - so a filter keystroke
walking 2000 rows measures a dozen strings, not 2000.

Accepted divergence: timestamps still do not align pixel-for-pixel
between an [INFO] row and a [DEBUG] one, because the level word and the
stamp share one Label. Fixing it costs a further label per row on the
module's heaviest render path. The tag and message columns - the two a
reader scans - do align.

The header stays inert: three plain ColumnHeader labels, no
SortableHeaderCells. A log is chronological, "sort by tag" is a filter,
and the level dropdown is where filtering lives; that class already
supports inert columns through a null OnClick, so the option stays open
at no cost, but wiring it for zero sortable columns would be
infrastructure with no caller.

The toolbar's three left controls are 26, 30 and 25px tall and shared no
optical centre; UiMetrics.ButtonHeight's own doc comment names that as a
separate, unmade decision. This is that decision - and it is
PlanRelayoutMath.CenterX turned on its side rather than a second rule, so
the Snapshot tab's own search row centres through the same arithmetic
against its own row height. The three right-hand buttons keep their
spots, now expressed through RightAlignedX off PinnedRightEdge rather
than three literals, and the search box is the one control that flexes
into free width, capped at 400. SearchMinWidth is deliberately a tested
PROPERTY of the layout rather than a clamp: it holds at every width the
module supports, and below the narrow-screen floor the box keeps
shrinking instead, because overlapping clusters are worse than a small
box.

The gap between the toolbar's two clusters is not stranded space - it is
the plan tab's own controls-row shape. Stated so the gate does not flag
it.

### About

The tab never re-read its width after Build - the resize handler resized
the root panel and nothing else, and the width was captured once - so
widening the window left the prose wrapped at whatever width the tab was
opened with, permanently. That is fixed independently of any redesign:
Build and Resized both go through one placement pass.

They do NOT go through the same entry point, and that distinction is
load-bearing. The stored width is what the resize path compares against
to skip a height-only event, and it OUTLIVES Build, because Module holds
one AboutTabContent for the module's lifetime and Blish re-runs Build on
every tab selection. Build therefore calls the unguarded ApplyLayout;
only Resized calls the guarded Relayout. Routing Build through the guard
- which the first draft of this section did - leaves the second and every
later open of the tab blank: eleven freshly-built blocks stacked at
(0, 0) inside a panel still at height 0. SettingsTabContent has the same
one-instance lifecycle and was already written this way.

Two columns now: an identity card on the left (a "Module" band, the
description, and six facts rows whose label band is MEASURED across the
six strings and whose value flexes to the column's pinned edge) and the
two prose blocks on the right, each with the 2px rule every other
SectionTitle band in the module draws - About's two were the only ones
drawing none. Below the two-column threshold they stack. The module name
is the plan header's own idiom reused verbatim: Display 32 with the
version at SmallHeading 20 regular beside it, baseline-aligned through
TypeRampMetrics - which is the pair SmallHeading exists for.

Copy: the six fact labels lose their trailing colons (inside a table with
a rule, a colon on every label is punctuation doing a column's job),
"Disclaimer:" becomes "Disclaimer", "Credits: gw2efficiency" becomes
"gw2efficiency". The two approved literal strings (the
ArenaNet disclaimer, the gw2efficiency credit) ship verbatim.

ACCEPTED DIVERGENCE, still open: past roughly 1100px
of panel, About stops using its width. Text is capped at a 560px measure
- 66 characters at the module's own measured 8.4px Body-16 average -
because a 280-character line at a 2560 window is a worse artefact than
white space, and the plan tab's tooltip work already respects the same
rule. The copyable value boxes are capped the same way: a 2300px box
holding a URL is the same defect as a 2300px paragraph. Every other
surface in this milestone uses all of its width. The lever is one
constant, AboutLayoutMath.ProseMeasure.

A second, smaller instance of the same call: the full-width Currency
Valuations section's two remaining notes are capped at
SettingsFormLayout.ProseMeasure, which is one section column's own
content width (546) rather than About's 560. Two tabs, two derivations,
each stated at its own site - deliberately not forked into a third
shared constant, because each is derived from the surface it sits on.

### Snapshot

The grid, its headers, its sortable cells and its tooltips were already
right. Two things were not.

The tab had two right edges: the header buttons pinned to
containerWidth - 10 while the grid's rightmost column ended at
containerWidth - 28, eighteen pixels apart on the same tab at every
width. SnapshotHeaderLayout.ChromeRightEdge derives the chrome's edge
from the SCROLLING grid's own width, so the buttons, the coin block, the
status line's cap and the header rule now land on one line. The
inter-button gap drops from 20 to the module's 8.

The coin row was a caption and ~150px of coin run left-packed at x=0,
with the rest of the band empty. It is a justified row now: a result line
on the left ("Showing N items - K currencies", through StatusText.Count
and the "N of M shown" shape the currency filter already uses) ellipsized
against the coin block, and the coin block right-pinned as a unit in its
own child panel so the dispose-and-rebuild refresh cannot destroy the
result line beside it. CoinCurrencyRenderer is untouched, so the icons
stay to the RIGHT of their numbers; only the block's origin moved. Counts
and names only - no ids reach the line.

The header band adopts the 38px section-header band with its rule at 35
and 1px of clearance, like every other heading in the module, instead of
a 40px band with a flush rule; every left edge on the tab moves to the
16px inset; and the search row's TextBox and Dropdown get the same
centring rule the Log toolbar does.

Deliberately untouched: the source-filter run's flow, cap and
scroll-on-overflow are correct, and it is still handed the CONTAINER's
width rather than the grid's, so a wrapped run can still reach ~20px
right of the chrome edge. Re-deriving it would move a wrap threshold in a
component this milestone has no complaint about.

### Sandbox check checklist

Take each tab at 1378 (the enforced minimum), 1638, 1836 and 2406, and
one very wide (2560+). At EVERY width, on every tab: no band of empty
space to the right of the content, and no text running under a
neighbouring column.

EVERY TAB, THE DRAG ITSELF
- Grab the window's resize grip on Settings, on About and on Snapshot in
  turn and drag it fast from the minimum to the widest and back, several
  times, WITHOUT letting go. The drag must stay smooth: boxes, buttons
  and grid columns follow the edge frame by frame. Paragraph wrapping and
  the "..." on shortened names deliberately lag - watch for them to catch
  up within about a fifth of a second of releasing, once, not repeatedly.
- Now the heavy one. On the Log tab, set the level filter to Debug+ with
  an EMPTY search so the ring is as full as the session allows (leave the
  module running a while first, or run a couple of plans and a snapshot
  refresh, to fill it), then drag the window edge NARROWER, fast, without
  letting go. This is the worst case in the module - three text
  measurements for every row on screen. The drag must stay smooth and
  the three columns must track the edge. Rows whose message no longer
  fits will be CLIPPED mid-word during the drag, with no "..."; that is
  the declared cost. On release, every one of them must gain its "..."
  within about a fifth of a second, in one pass. Then widen again and
  confirm the "..." disappear from rows that now fit.
- Release the drag and immediately switch tabs. Nothing throws and
  nothing appears in the Log tab at Debug+ about a re-fit wait (the
  warning strings to watch for are "text re-fit wait failed" and "row
  re-fit wait failed").

SETTINGS
- 1378: two section columns; the four sections pack Sound|Homestead over
  Logging|Snapshot; every input box in a column shares one right edge
  with every other; the currency grid runs two columns whose cells'
  Ignore checkboxes and tag slots line up with the inputs above them.
- 1638: the currency grid goes to THREE columns while the section board
  stays at two. Confirm this is legible rather than accidental - the two
  grids have different cell minimums by design.
- 1836: the section board goes to three columns. 2406: four columns, one
  section each, nothing stretched.
- 2560: nothing on the tab ends before the panel does, except the two
  grey notes under Currency Valuations, which are capped at a reading
  measure on purpose. This is the width the old two-column cap failed at.
- Type a letter into a currency amount, press Save: the "Invalid" tag
  appears IN the tag slot and the column does not move a pixel. Same for
  a Homestead tier ("Must be 0, 1, or 2" is the widest string that slot
  ever holds, so it is what the band is sized to).
- Type into any box: the dirty chip appears on the save bar; Discard
  appears beside Save; Discard raises the confirm dialog; discarding
  clears the chip and the status line.
- Drag the click-volume slider: no dirty chip (immediate-apply), and the
  "Applies immediately" tag sits at the Sound band's right edge.
- In Logging, confirm the Diagnostics row carries the SAME tag on its own
  row, pinned to the same right edge as the two input boxes below it, and
  that the two input rows do NOT. Tick the box: no dirty chip appears
  (correct - it is already saved), and its hover says so. Then type into
  Log max size, click Discard, and confirm the box stays ticked while the
  box below reverts. That difference is what the tag is there to declare.
- Filter the currency list to one match, then clear it: the tab does not
  jump to the top (the fixed grid height still holds).
- Filter to "Astral", hover its empty amount box, and read the whole
  hover: it must say that leaving the box blank keeps the currency out of
  price comparisons. Repeat on a Rift Essence row. These four have no
  default tag, so this hover is the only place that fact lives. Hover a
  row that DOES have a default (Karma) and confirm its box says the
  blank case keeps the default instead.

LOG
- 1378: the Tag column starts at one fixed x on every row; run the eye
  down it. The Message column starts at one fixed x. All three headers
  sit on their own columns.
- Switch the level filter from Info+ to Debug+ and back: the Tag band
  RESIZES to the widest tag actually on screen and every row re-fits
  together. Switch to a filter that admits only [plan] rows and confirm
  the message column visibly gains width against the old template.
- Leave Follow checked while a snapshot refresh writes [snapshot-fetch]
  rows: the band widens once, every row re-fits, and no row is left at
  the old x.
- Drag the window from 1378 to 2560 and back: rows re-fit once the drag
  stops, with no compounded "..." after several out-and-back drags (the
  re-fit always starts from the stored whole string, never from what the
  label currently shows). The toolbar's search box grows to its 400 cap
  and no further, and the three buttons stay pinned, both DURING the drag.
- At 1378 confirm the search box, level dropdown, Follow checkbox and the
  three buttons share one optical centre line.

ABOUT
- Open at 1378, then widen to 2560 WITHOUT closing the tab: the layout
  reflows. This is the defect that did not reflow at all before.
- Open About, switch to Snapshot, switch BACK to About without touching
  the window edge. The tab renders exactly as it did the first time. The
  first draft of this milestone had Build going through the guarded
  Relayout, and Module reuses one AboutTabContent instance across every
  open, so the second open's fresh blocks were measured against the
  first open's stored width, the guard short-circuited, and the tab came
  back blank. Repeat the switch three or four times, and once more after
  a resize, since the guard only fires when the width matches.
- The companion sequence, which the bullet above deliberately avoids:
  with About open, DRAG the window edge and then - within the 150ms
  settle window, so immediately - click Settings and click straight back
  to About. Blish re-runs Build on the one reused AboutTabContent off the
  UI thread while the settle callback lands on the main thread, so this
  is the one sequence that puts the two on the same blocks. About must
  come back fully drawn, not blank and not truncated, and the log must
  carry no "MainThreadMarshal queued action threw". Repeat three or four
  times, narrowing and widening.
- 1378: two columns; facts left, Disclaimer over gw2efficiency right;
  both prose headings draw a rule.
- Read the gw2efficiency paragraph and count roughly 60-70 characters per
  line at every width from 1378 up. At 2560 the columns are wide and the
  TEXT is not - that is the declared divergence, not a bug.
- Below ~950 panel (a narrow-screen client) the two columns stack and
  nothing clips.
- The module name renders at Display 32 with the version at 20 regular on
  the same baseline; the six facts labels have no trailing colons and
  their values flex to one right edge; the copyable boxes are capped, not
  panel-wide.

SNAPSHOT
- 1378: put a straightedge (or a screenshot ruler) down the right side.
  Clear Cache/Refresh Now, the coin block, the header rule's end, and the
  last grid column's amounts all end on ONE line. This is the fix to look
  for.
- The coin row reads "Showing N items - K currencies" on the left and the
  coin run on the right, with each icon to the RIGHT of its number.
- Search until one item matches: the result line updates to "N of M" and
  never runs under the coin block; a long line ellipsizes with the full
  text on hover.
- 1632: the grid goes to three columns; the coin block and header buttons
  do not move relative to the last column.
- Uncheck enough character filters to wrap the source-filter run: it
  drops to its own full-width row and everything below shifts, unchanged
  from before this milestone. The run may still reach slightly right of
  the chrome edge - recorded above as deliberately untouched.
- The header title sits at the 16px inset with its rule 1px clear beneath
  the buttons.

Gate: PASS (2026-08-25 sandbox session, branch build at the fix HEAD,
captures preflight/gTB0-gTB5).

SETTINGS - the tab that prompted the milestone. The left-packed run of
rows is gone: a genuine two-column board (Sound | Homestead Refinement,
Logging | Snapshot, then Currency Valuations) filling the panel, inputs
aligned on their column's own edge with the unit hint after them, each
description wrapped to a reading measure under the control it belongs
to, "Applies immediately" chips right-anchored on their section rows,
and Save right-anchored in the bar. The right half of the panel is doing
work at last.
LOG - a three-band Time / Tag / Message gutter with Message owning the
remaining width, toolbar controls left and the three buttons pinned
right.
ABOUT - a two-column document: the Module facts card (label + value
rows) left, Disclaimer and the gw2efficiency credit as prose right at a
sane measure rather than one 1300px line.
SNAPSHOT - the audit found and fixed the last left-packed strip: the
result line now sits opposite a right-anchored coin block on one
justified row, and the source-filter run starts at the same gutter as
the search box above it (it began at x=0) and ends on the tab's shared
chrome right edge.
NARROW - at a 1024 client the board keeps two columns with no collision
and no overlap; content runs under the screen edge exactly as the
plan-view gate already recorded for a sub-minimum client, which is the
window's effective-minimum behavior and not this milestone's doing.

Two round-capped Must Fixes were applied before this
gate rather than shipped: About cleared its built flag only inside the
off-thread Build, leaving a settle callback free to dereference the
blocks Build was about to replace (now cleared on the main thread at the
factory, mirroring Settings); and the Snapshot source-filter run - the
one content-driven width on that tab - was still escaping the frame in
both modes. The two tests that encoded the old filter-run geometry moved
to the new contract.

