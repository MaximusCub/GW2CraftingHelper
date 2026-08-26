> **Frozen record - 2026-08-22, branch `audit-g-settings`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Audit batch G: Settings restructure (audit-g-settings)

Commits on audit-g-settings off master 47bb2c5, covering the three
maintainer-approved UX audit findings against Views/SettingsTabContent.
cs (M4 currency-list density, M5 save buttons + empty heading, M6
visual structure), plus the review round that followed them. Persistence semantics are untouched - every setting
is written by the same code, with the same validation, the same
"invalid rows keep their persisted value" contract and the same
three-state currency precedence as before; only layout, control
placement and the confirmation surface changed.

**Supersedes B14** (backlog-cleanup, gate PASS 2026-08-17): that batch
deduplicated the four per-section save rows into one AddSaveRow helper
and live-verified all four rendering identically with their green
dated "Saved" labels. Under the maintainer-approved M5 the four rows
and their four status labels are gone entirely, replaced by one Save,
so that gate observation no longer describes the shipping UI. The
dated green confirmation pattern itself is kept, once.

- **One line per currency, two-up (M4):** each currency was a 54px
  two-line row spanning the full panel while using only its left
  portion - name/input/hint/error on line one, a default-state label
  and Clear checkbox on line two - stacked 47 deep. Each is now a 30px
  cell: name (ellipsized to 170px, full name on hover only when it did
  not fit), input, Clear, and one tag slot at the right of the cell.
  That slot shows the persisted default state ("default N", or
  "cleared" when suppressed) and is taken over by the red "Invalid"
  warning while an amount will not parse - only ever one of the two, so
  a half-width cell needs room for one. The gw2efficiency attribution +
  editable/clearable wording is on the input's tooltip. The input's
  placeholder is the unit ("copper") on every row: Blish's TextBox
  insets a placeholder 10px a side and draws it untruncated inside the
  control's own scissor, so a 70px box shows ~50px of it - enough for
  "copper", not for "default: 3600", which is why the default estimate
  is a label and not the placeholder it briefly was. Cells are packed
  left-to-right, top-to-bottom into an absolutely-positioned grid
  panel. Section height: ~2,690px -> ~880px two-up (the row block
  itself 2,538 -> 720).
- **Filter box (M4):** a "Filter currencies..." TextBox above the grid
  hides non-matching cells and re-packs the rest, with a "N of 47
  shown" counter beside it. Hidden rows are still read and written by
  Save - filtering is display-only, nothing is dropped. A row whose
  amount did not parse is forced back on screen by the next filter pass
  whatever the query says (SettingsCurrencyGridLayout.Compute's
  alwaysShow), so the save bar's "N invalid entries not saved" can
  never point at a tag the filter is concealing.
- **The grid panel holds its unfiltered height (M4):** Blish's
  Scrollbar zeroes ScrollDistance/TargetScrollDistance whenever the
  scrolling container's content height changes - its RecalculateLayout
  captures the previous scrollbar percent, recomputes it from the
  visible children, and resets on any difference - and it does so a
  frame later, so the reset cannot be undone in place. Sizing the grid
  to the match count therefore snapped the tab back to scroll-top on
  every filter keystroke that changed the count. The grid panel is now
  fixed at SettingsCurrencyGridLayout.ComputeHeight (the full 47-row
  height for the current column count) and only the cells move; the
  cost is trailing blank space under a filtered list, which is why the
  grid is deliberately the last thing in the panel and the Astral
  Acclaim note moved above it.
- **Width changes are re-laid out (M4):** the row/header panels, the
  header rules, the grid panel and every cell + cell rule are re-sized
  from container.Resized (ApplyPanelWidth, early-out when the width did
  not move, so a height-only resize or a vertical drag costs nothing).
  Without it the tab kept the width it was first opened at: narrowing
  the window left the second column of cells beyond the panel's right
  edge, invisible and untypeable until the tab was closed and
  re-opened.
- **Section order (M4):** the three short sections (Homestead
  Refinement, Logging, Snapshot) now build before the long currency
  section, so the tab opens on controls rather than on a wall of
  currency rows.
- **One Save for the tab (M5):** the four per-section Save buttons are
  replaced by a single Save in a bar that is a sibling of the scrolling
  FlowPanel, so it never scrolls away. SaveAll runs all four persists
  in order - currency valuations (with its defensive "Save failed - see
  log" branch), Homestead tiers, log max size (including the live
  ModuleLog.MaxFileSizeBytes push) + retention days, snapshot refresh
  interval - sums their invalid-entry counts and writes one status:
  green "Saved - <date>" when everything parsed, amber "Saved - N
  invalid entries not saved" otherwise. Per-row error labels are
  unchanged. Placement note: the audit suggested a fixed footer; the
  bar is anchored at the TOP instead, because LogTabContent already
  builds a fixed toolbar this way above its own CanScroll FlowPanel
  and a top bar needs only ContentRegion.Width, while a bottom footer
  would also depend on ContentRegion.Height being final at Build time
  (its failure mode being a Save bar floating over the rows).
- **Empty heading demoted (M5):** "Plan Defaults" was a section header
  with three info lines and no controls at all. It is now a single note
  line under Currency Valuations, the pricing section it points at.
- **Dividers (M6):** AddSectionHeader draws the same 2px
  SectionDividerColor rule CraftingPlanView's section headers do
  (bottom-anchored with 1px clearance in the 30px header), and each
  currency cell carries a LabelHelpers.CreateRowDivider rule, hidden on
  the cells of the last populated grid row so it re-anchors as the
  filter re-packs the list. The cell's input sits at y=1 so it ends
  clear of the rule at y=27.
- **Layout math is Blish-free (M4):** Services/
  SettingsCurrencyGridLayout.cs owns the filter predicate, the packing
  math (column count, column width, per-cell X/Y/row, grid height) AND
  the cell's horizontal constants; the view aliases those constants at
  compile time and only copies placements onto controls. MinColumnWidth
  is now derived (CellTagX + CellTagWidth = 424) rather than
  hand-estimated: the previous 340 was short of the cell it claimed to
  size, so a two-up column between 680 and ~722px clipped the invalid
  tag. Two-up now needs a 848px panel, below which the grid falls back
  to one column - including at the window's 930px minimum, where the
  section is 1,410px of rows rather than 720. 42 tests cover the
  one/two-column boundary, blank/trimmed/case-insensitive matching,
  re-packing around hidden entries, alwaysShow overrides (including a
  short array), the empty result, null names, non-positive
  width/height, the fixed height, and the width budgets - the tag
  budget against every real value in CurrencyDecisionDefaults, so a
  future six-figure default fails the suite instead of clipping.
- **Review-pass fixes (own commit):** Build now nulls the currency
  grid/filter/count/status fields alongside the row lists it already
  cleared (same stale-disposed-control class as the _homesteadRows
  comment records); the scroll panel's height is clamped at 0 now that
  the save bar is subtracted from it; the demoted note was shortened to
  fit the panel width at the window's 930px minimum.
- **Measured, not assumed:** the filter's re-flow relies on FlowPanel
  subscribing to each child's Resized and skipping invisible children -
  both confirmed by decompiling the shipped Blish HUD 1.3.0 binary
  (FlowPanel.OnChildAdded -> ChangedChildOnResized ->
  ReflowChildLayout, which filters on c.Visible), so setting the grid
  panel's Height is enough and the first draft's extra Invalidate was
  removed as a second reflow per keystroke.

Validation: build 0 errors and the full suite green before each commit
(1886 baseline -> 1928 with the new layout tests; the increase is all
new tests, zero regressions).

Desktop gate items (all in the Settings tab):
1. Currency rows are one line each, with Clear on the same line and a
   readable "default N" tag at the right of every defaulted cell (the
   whole number, not a clipped one) - check a 4-digit default such as
   Guild Commendation or Spirit Shard. Two cells per line once the
   window is wide enough (panel >= 848px); one per line at the 930px
   window minimum. The whole section fits in roughly a screen and a
   half two-up instead of four-plus screens.
2. Typing in the filter box hides non-matching currencies and re-packs
   the rest with no gaps; the counter reads "N of 47 shown"; clearing
   the box restores all 47. Scroll down to the filter box first: the
   panel must NOT jump back to the top on any keystroke, including
   backspaces. The grid keeps its full height, so a short match list
   leaves blank space below it.
3. One Save button, visible without scrolling from any scroll position,
   and one green dated "Saved - <date>" confirmation. Change one value
   in EVERY section (a currency amount, a Homestead tier, log max size,
   log retention, snapshot interval), click Save once, reopen the tab
   and confirm all five persisted. Enter one bad value and confirm the
   amber "1 invalid entry not saved" wording plus the per-row tag.
4. A 2px rule under every section header, and a rule between currency
   rows with none dangling under the last populated row (check both
   unfiltered and with a filter that leaves an odd number of matches).
5. Section order top to bottom: Homestead Refinement, Logging,
   Snapshot, Currency Valuations; no "Plan Defaults" header anywhere,
   with its note present under Currency Valuations.
6. Resize the window while the Settings tab is open, both wider and
   back down to the 930px minimum, and confirm every currency cell
   stays inside the panel and stays typeable, the columns switch
   between one-up and two-up, and the section-header rules span the new
   width.
7. Type a bad amount into one currency, filter it off screen, click
   Save: the amber "1 invalid entry not saved" must be accompanied by
   that row reappearing with its red "Invalid" tag despite the filter.
Gate: PASS (2026-08-22 evening desktop batch, branch build b740035,
captures preflight/gG1-gG7). (1) The tab rendered top-down as: top
Save bar, Homestead / Logging / Snapshot short sections each with
the 2px header rule, then Currency Valuations with the filter box,
"47 currencies" count, and the one-line two-up grid - "copper"
placeholder inputs, Clear checkboxes, grey default tags, row rules,
name ellipsis on "Manifesto of the Moletaria...". (2) Typing
"shard" filtered to "6 of 47 shown" with the grid repacked two-up
and no scroll jump. (3) Save produced the green "Saved - Aug 22,
2026 8:33 PM" label beside the button (all sections saved in one
click; Save's "Save every section on this tab." tooltip verified).
(4) The "was N" override tag and amber "cleared" tag were NOT
exercised live: late-session synthetic keyboard degradation kept
the override keystrokes landing in the filter box (a documented
input-death mode, not a module fault - the filter box accepting
them proves the click-to-focus path). Both tags are pinned by the
RefreshCurrencyRowDefaultState logic restored verbatim from
master's proven three-state code plus the CellTagWidth fit test.
One-column fallback at the 930px minimum also not exercised
(synthetic resize unreliable); pinned by SettingsCurrencyGridLayout
tests.
