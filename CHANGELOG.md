# Changelog

Every version deployed to a live Blish HUD install gets an entry here and a
matching `v<version>` git tag on the release commit, so any two shipped
builds can be compared with `git diff v0.2.0..v0.2.1`. The About tab shows
the running version.

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
