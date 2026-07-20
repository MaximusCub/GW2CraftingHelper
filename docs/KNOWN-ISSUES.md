# Known Issues Backlog (logged 2026-07-20, user hands-on session post-M29)

User-reported issues from exploring the merged M23-M29 build. Address as
milestone M30 (bugs 1-7) + data work (item 8). Root-cause hypotheses are
from the orchestrating session's architecture knowledge - verify before
trusting.

## 1. Pill toggle STILL resets scroll position
Clicking a decision pill resets the viewport to top despite M23's
PreserveScrollAcross wrapping ApplyOverridesAndResolve.
Hypothesis: the M24 rewrite changed content composition (cost tiles,
reordered sections) so the multi-frame restore's height-convergence
window (10 frames) may be too short, or a new rerender path bypasses the
wrapper, or the generation-guard supersedes the restore before it
settles. Reproduce with a tall Exordium plan; instrument
RestoreScrollOffset attempts vs convergence.

## 2. Resize-drag flicker + transient tree collapse
Dragging the bottom-right resize control redraws content every drag tick;
during the drag the recipe tree's INTERNAL expansion state renders
collapsed, then correctly reverts to the persisted expansion state once
the mouse settles (before mouseup). Top-level section open/closed state
is unaffected.
Hypothesis: OnPanelResized full-rerenders on every width change during
the drag (no debounce); intermediate renders read default depth<2
expansion before _nodeExpansion is applied... but _nodeExpansion IS
consulted at render - more likely lazily-built deep nodes render
collapsed until rebuilt. Fix direction: debounce re-render to drag
settle (e.g. 150ms after last resize event), and/or stop full
dispose+rebuild on resize (relayout in place).

## 3. Total Cost section: currency icons missing
Currency cost rows are text-only ("50x Spirit Shards"). Add currency
icons (GW2 API /v2/currencies provides icon URLs; render-service
pipeline already exists for items). Respect coin invariant conventions.

## 4. Insufficient padding between major sections
Total Cost / Recipe Tree / Shopping List etc. need more vertical
breathing room between sections.

## 5. Recipe Tree container does not contract after collapse
Collapsing the Recipe Tree section (or large subtrees) leaves a large
whitespace gap before Shopping List. The M9 collapse-reflow class of bug
resurfacing in the M24 layout - likely the tree's outer FlowPanel
height not shrinking (AutoSize only grows? stale Height on collapsed
child containers?).

## 6. Shopping list Amount/Each/Total columns too tight
Increase inter-column whitespace (the M24 review fixed overlap;
readability spacing still insufficient).

## 7. Horizontal dividers render at inconsistent brightness
Row divider lines vary in visible contrast row-to-row; some vanish into
the background texture. Hypothesis: low-alpha divider color composites
against the varying parchment texture. Fix: higher-contrast consistent
divider (opaque dark line, or two-tone 1px light-over-dark), verify over
both light and dark texture regions via captures.

## 8. Ball of Dark Energy (71994) - unpriceable, needs acquisition hint
Wiki-verified: salvaged from ascended gear with ascended salvage kits
(guaranteed from weapons/armor, very low chance from trinkets); account
bound; no producing recipe; not TP-tradable. Current behavior (honest
"Acquire (no known source)") is correct but unhelpful. Fix direction:
acquisition-hints data source (wiki-derived, seeder work - same family
as the collections/achievements pillar). NEVER invent pricing for it.

## Carried follow-ups (from M24-M29 PRs)
- Caret glyphs: settle ASCII v/> headers vs unicode tree triangles after
  an in-game check of which renders reliably
- Remaining parity pillars: multi-item plans, Mystic Clover EV (blocked
  on probability data - seeder first), vendor cap data scraping,
  phase-2 owned-materials-as-competing-source, localization
