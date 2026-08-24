# Changelog

Every version deployed to a live Blish HUD install gets an entry here and a
matching `v<version>` git tag on the release commit, so any two shipped
builds can be compared with `git diff v0.2.0..v0.2.1`. The About tab shows
the running version.

## 0.2.3 - 2026-08-24

The plan-view redesign (PR #173): the crafting planner stops wasting
your screen.

### Changed
- Every plan section now justifies to the full window width: item names
  flex, and pills, costs, amounts, levels and statuses anchor to the
  right edge at every window size - no more columns crammed left with
  stranded dead space. The minimum window width drops from 1478px to a
  measured 1378px.
- A real type hierarchy: section titles at 24 bold, column headers at
  20 bold, an 18-bold status line with a larger spinner - each tier
  visibly a step above the next. Character and discipline names render
  at full body size everywhere.
- The Total Cost section is a full-width formula band; the currency
  table's numbers right-align under proper headers.
- Required Recipes is one line per row with its own Discipline column;
  the Shopping List gains a Source column with aligned, color-coded
  badges (vendor teal, unknown red) and a sortable header.

### Added
- Overrides and Ignored counts as persistent chips with guarded Clear
  buttons: clearing confirms with plain-language consequences, and
  actions that would change nothing skip the dialog and say why.
  The status line reports events only.
- Generate Plan explains itself on hover (it clears manual decisions
  and ignore marks - now it says so before you click).

### Fixed
- Rapidly toggling a pill without moving the mouse no longer drops
  clicks (re-solves now update the existing controls in place instead
  of rebuilding them under your cursor).

## 0.2.2 - 2026-08-24

Second field-test round plus the pixel-authenticity wave (PRs #163-#170).

### Added
- Item tooltips now duplicate the in-game visual style, measured
  pixel-by-pixel from real game captures: the framed item icon header,
  white identity block with the name alone carrying rarity color, teal
  flavour text, light-blue rune/sigil bonus lines, the game's own line
  order, a translucent black canvas, and the vendor value as an
  unlabelled final coin line (absent on unsellable items).
- Those rich tooltips appear on every item surface: recipe tree rows,
  Used Materials, the Shopping List, and Snapshot results - and restored
  plans fetch their stat data in the background so hovers work right
  after a restart without regenerating.
- Item stat blocks in tooltips: attributes, defense/weapon strength,
  upgrade bonuses, binding, level - computed from the same API data the
  plan already fetches (zero extra requests).
- The Snapshot tab lays items out in multiple columns when the window is
  wide enough.
- A Click Volume slider in Settings (with live readout and a Test
  button): the module's click sound was nearly inaudible at Blish's
  volume ceiling; it now plays at your chosen level, default well above
  the old cap, and 0 turns it off.
- Searching the Snapshot with a single letter that only matches a
  character name now explains why nothing shows yet.

### Fixed
- Dismissing the module window no longer strands your keyboard: closing
  with Escape, switching tabs, or pressing Enter in a box previously
  could leave GW2 ignoring all keys until you clicked. Root-caused to a
  Blish focus-slot quirk (plus one bug of ours) and fixed at every text
  box.
- The plan's own root item no longer offers an IGNORE pill, and a plan
  whose cost is genuinely zero renders the full cost formula at 0
  instead of collapsing the Total Cost section; zeros caused by
  unpriceable items stay honestly unexplained instead of being dressed
  up as profit.
- Rapid pill toggling and table re-sorts no longer lose your sort state
  on re-solve; sorting resets only when a new plan arrives.

### Changed
- All row and body text moved up a size (Menomonia 16) with every layout
  constant re-derived from measured font metrics; the minimum window
  width follows the same measurements.
- The corner icon is re-padded to match the size of GW2's own top-row
  icons (it rendered noticeably larger than its neighbors).

## 0.2.1 - 2026-08-24

First round of live field-test feedback, fixed and shipped (PRs #156-#161).

### Fixed
- The Clear Cache confirmation dialog now fits its whole message and its
  title bar renders cleanly; a second layout bug that clipped the last
  wrapped line mid-glyph was caught on the desktop gate and fixed too.
- The Settings currency valuation boxes now read as inputs: "Currency /
  Copper per unit" column headers, each box hinting its own default, and an
  instruction line. (The override mechanics were verified working all
  along - type a number, press Save, the tag shows "was N".)
- The item search's suggestion list drops directly under the box instead of
  floating off to the right.
- Recipe Tree cost values align under the "Cost" column header for coin and
  currency rows alike.
- Letters with descenders ("y", "g") no longer render with their tails
  clipped; swept across all row labels.
- Deep crafting plans (for example +24 Agony Infusion, the deepest chain in
  the game) now restore correctly after a restart - they previously saved
  but silently failed to load.

### Changed
- Total Cost band: all three tiles use the same coin text size; "Actual
  Cost to Craft" is highlighted with a translucent gold box instead of a
  larger font; the currency table is centered.
- The minimum window width is now 1436px, sized by traversing every recipe
  in the game so the deepest tree renders without truncation (research
  in docs/research/minimum-window-width.md). Narrow screens get a
  screen-fitted floor instead of an off-screen window. The decision-pill
  column widened so the standard four-pill run always fits.
- The plan-strip and Snapshot-refresh spinners are Blish HUD's own circular
  painterly spinner instead of rotating ASCII characters.

### Added
- Used Materials and Shopping List are sortable by clicking column headers,
  with ^ / v indicators; a third click restores plan order. Mixed
  coin/currency columns sort coherently without inventing exchange rates.
- Leaving the Settings tab with unsaved edits prompts to Save or Discard
  (window close included); reverting a field to its original value counts
  as clean.
- Buttons, pills, sort headers and suggestion rows dim while pressed and
  restore on release, and clicks play Blish's click sound. (Blish 1.3.0's
  own StandardButton is silent due to an upstream asset-path bug; the
  module plays the correctly-resolved sound and documents the one-line
  removal if Blish ever fixes theirs.)

## 0.2.0 - 2026-08-23

First stamped field-test release. Highlights relative to the unversioned
development era:

- Full 30-finding UX/visual audit implemented (visible tree carets, cost
  readability, interaction honesty on pills, table density, Settings
  restructure, log readability, Plan Notes wrapping, consistency sweep).
- Central tooltip facility: opaque tooltips with real coin icons, wrapped
  text, four-edge screen clamping, one shared surface (Blish never disposes
  tooltips; its own path leaks per control).
- Per-character source checkboxes and character-name search on the
  Snapshot tab (2+ letters to match names); sticky filters; setting-driven
  snapshot staleness; Delete Log File; typed item names generate without a
  suggestion pick, with ambiguous names called out.
- Truly modal confirmation dialogs, ellipsized log lines with full-text
  tooltips, and a large body of gate-verified fixes recorded in
  docs/KNOWN-ISSUES.md.

## Before 0.2.0

Unversioned development (manifest said 0.1.0 throughout). History lives in
git and docs/KNOWN-ISSUES.md.
