# Known Issues Backlog (logged 2026-07-20, user hands-on session post-M29; updated post-M30)

User-reported issues from exploring the merged M23-M29 build, addressed in
milestone M30. Items 1-7 plus three additional issues found during the M30
wave (9-11) are fixed below with resolution notes. Item 8 remains open as a
data-work item. Original hypotheses are kept for context; they were not all
correct - see the resolution note on each item for what actually shipped.

## 1. Pill toggle STILL resets scroll position (FIXED in M30)
Clicking a decision pill resets the viewport to top despite M23's
PreserveScrollAcross wrapping ApplyOverridesAndResolve.
Hypothesis: the M24 rewrite changed content composition (cost tiles,
reordered sections) so the multi-frame restore's height-convergence
window (10 frames) may be too short, or a new rerender path bypasses the
wrapper, or the generation-guard supersedes the restore before it
settles. Reproduce with a tall Exordium plan; instrument
RestoreScrollOffset attempts vs convergence.
Fixed in M30: the M23 restore loop never actually spanned multiple real
frames - GameService.Overlay.QueueMainThreadUpdate, when re-queued from
inside its own callback, drains in the same frame instead of waiting for
the next Update() tick. Restore, guard, and resize debounce are now
driven by a per-frame FrameTicker control; the restore contests
Blish's reset-to-zero while yielding to genuine nonzero user scrolls.

## 2. Resize-drag flicker + transient tree collapse (FIXED in M30)
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
Fixed in M30: resize was doing a full dispose+rebuild on every drag
tick; replaced with a 150ms trailing debounce (FrameTicker-driven) with
live width sizing during the drag and a single settle rebuild after.

## 3. Total Cost section: currency icons missing (FIXED in M30)
Currency cost rows are text-only ("50x Spirit Shards"). Add currency
icons (GW2 API /v2/currencies provides icon URLs; render-service
pipeline already exists for items). Respect coin invariant conventions.
Fixed in M30: currency rows now render icons sourced from /v2/currencies
through the existing item render pipeline.

## 4. Insufficient padding between major sections (FIXED in M30)
Total Cost / Recipe Tree / Shopping List etc. need more vertical
breathing room between sections.
Fixed in M30: increased vertical spacing between major sections.

## 5. Recipe Tree container does not contract after collapse (FIXED in M30)
Collapsing the Recipe Tree section (or large subtrees) leaves a large
whitespace gap before Shopping List. The M9 collapse-reflow class of bug
resurfacing in the M24 layout - likely the tree's outer FlowPanel
height not shrinking (AutoSize only grows? stale Height on collapsed
child containers?).
Fixed in M30: collapsed tree containers now contract correctly; no
stale whitespace gap remains before Shopping List.

## 6. Shopping list Amount/Each/Total columns too tight (FIXED in M30)
Increase inter-column whitespace (the M24 review fixed overlap;
readability spacing still insufficient).
Fixed in M30: added inter-column whitespace to the Amount/Each/Total
columns.

## 7. Horizontal dividers render at inconsistent brightness (FIXED in M30)
Row divider lines vary in visible contrast row-to-row; some vanish into
the background texture. Hypothesis: low-alpha divider color composites
against the varying parchment texture. Fix: higher-contrast consistent
divider (opaque dark line, or two-tone 1px light-over-dark), verify over
both light and dark texture regions via captures.
Fixed in M30: dividers now use a consistent higher-contrast color,
verified legible over both light and dark parchment texture regions.

## 8. Ball of Dark Energy (71994) - unpriceable, needs acquisition hint (FIXED in M32)
Fixed in M32: curated wiki-verified seed (ref/acquisition_hints_seed.json,
5 entries: Ball of Dark Energy plus the four HoT map-completion Gifts
feeding Gift of Maguuma) rendered as tooltips on unknown-source tree
pills and shopping rows; the misleading "Only available source" tooltip
on true-unknown pills now falls back to "No known acquisition source".
No pricing invented. Live-verified via tooltip capture. Follow-up: a
dev-time seeder tool (wiki fetch -> seed JSON) when the list grows past
hand-curation; same tooling family as the collections/achievements
pillar. Note: hovering must target row whitespace - name labels capture
the mouse and suppress the row tooltip (pre-existing Blish behavior).

## 9. Rarity text unreadable over parchment texture (FIXED in M30)
Legendary rarity text measured luminance ~52 against the parchment
background, effectively unreadable.
Fixed in M30: rarity colors brightened with a dim floor lift, and label
shadows added for contrast over the texture.

## 10. Window content region overshot the opaque background texture (FIXED in M30)
contentRegion extended 11px past the texture's opaque area, and the
texture itself fades over its last ~15 rows, risking bleed-through at
the window edge.
Fixed in M30: window is now 684 high with 26px total margin; verified
no bleed-through against the texture.

## 11. Decision pill labels unreadable against same-hue backdrop (FIXED in M30)
Pill labels were drawn in the pill's own border color over a same-hue
backdrop, making the text nearly invisible.
Fixed in M30: pill labels now render in white.

## Carried follow-ups (from M24-M29 PRs)
- Caret glyphs: settle ASCII v/> headers vs unicode tree triangles after
  an in-game check of which renders reliably
- Remaining parity pillars: multi-item plans, Mystic Clover EV (blocked
  on probability data - seeder first), vendor cap data scraping,
  phase-2 owned-materials-as-competing-source, localization
- Blish HUD has no SynchronizationContext: async continuations in
  TriggerGenerate resume off the main thread and mutate UI controls -
  latent cross-thread hazard, audit all await points that touch controls.
- InvalidateUpToContentPanel bubbles a full content-panel re-measure on
  every tree toggle - fine at current sizes, revisit if large-tree
  toggle jank appears.
- Resize debounce settle-path verified by code review + the FrameTicker
  mechanism proven live for scroll restore; a positive in-game
  observation of the single settle rebuild is still pending (synthetic
  input could not catch the TabbedWindow2 resize grip).
- Scroll guard's reset-vs-user discriminator: a user dragging to exactly
  top within ~0.3s of a layout mutation may be bounced up to 4 times
  before winning (accepted tradeoff; revisit only if reported).
- Currency rows now use API names from /v2/currencies (may differ
  slightly from the old hardcoded fallbacks, e.g. singular forms);
  Gw2Constants remains the offline fallback.

---

# M33 Backlog: Exordium Full-Parity Wave (logged 2026-07-20, post-M32 hands-on)

User-reported issues from live testing of the merged M30-M32 build, plus a
milestone directive. GOAL: full behavioral parity with the gw2efficiency
crafting calculator for an Exordium plan, every node. Hypotheses below are
from the orchestrating session - instrument/verify before trusting.

## THE METHOD (user directive - governs all items below)
Research how gw2efficiency.com/crafting/calculator handles Exordium
ITEM BY ITEM (research-only at dev time; the module must NEVER call
gw2efficiency at runtime). For each node document: which sources gw2e
shows, which price basis it uses, craft/buy defaults, how it displays
vendor/currency costs, and how it handles unpriceable items. Make the
module ECHO that behavior rather than inventing an approach. New
dev-time seeders (vendor pricing, Mystic Forge recipes) are welcome;
they must write static seed JSON, never scrape at runtime.

## 12. Fast wheel-up scroll: net-downward stutter
Rapid successive wheel-up events make the viewport scroll up then jump
back down further than it went up - net downward movement with an
upward stutter. Hypothesis: scroll guard/restore machinery (or some
per-frame interaction with Blish's Panel wheel handling) contesting
rapid user input. Reproduce with the proven instrument-first loop
(synthetic rapid wheel-up while a guard window could be live and while
idle) before designing a fix.

## 13. Resize UX rework: live reflow, no settle stutter
The 150ms debounce-only approach is REJECTED by user feedback: content
must reflow smoothly WHILE dragging, not lag until the mouse holds
still. Additionally the settle rebuild itself is visibly ugly: stray
horizontal divider lines flash and the view visibly reconstructs from a
collapsed state (nested AutoSize convergence made visible). Direction:
in-place width relayout of EXISTING controls during drag (update
widths/x-positions; no dispose+rebuild), making the settle rebuild
unnecessary or invisible. This is the previously-rejected "option b" -
now required; design it to avoid drift between build and relayout paths.

## 14. Pill-click viewport flash (jump to top and back)
Clicking a TP/VENDOR override pill visibly flashes the view to the top
for an instant before the scroll restore re-asserts. The restore
converges but applies a frame+ late. Direction: apply the saved scroll
ratio synchronously inside PreserveScrollAcross immediately after the
rebuild (before first paint), with the FrameTicker loop only defending
convergence afterward - target: zero visible movement.

## 15. Shopping tag text contrast (VENDOR / SALVAGE / UNKNOWN)
The grey shopping-list source tags have poor text-vs-fill contrast.
Tag text should be near-white / light grey (match the M30 #11 pill-label
fix, which only covered tree pills, not shopping tags).

## 16. Vendor-source items show no price
Vendor-decision rows (e.g. Vision Crystal, Philosopher's Stone, Mystic
Clover) render empty Each/Total cells. Show the actual vendor cost,
including non-coin currency costs with currency icons (pipeline for
icons exists since M30 #3). Echo gw2e's display for the same nodes.

## 17. Seed data gaps: false UNKNOWNs in the Exordium tree (FIXED in M33)
Original hypothesis was wrong on 3 of its 4 points, confirmed by wiki
research and an offline Harness dump. Gift of Exordium, Gift of Metal, and
Gift of the Mists were already correctly seeded (recipes -1337, 6074,
-1005) before this milestone - they rendered UNKNOWN because
PlanSolver.Evaluate stopped evaluating a recipe's ingredients on the first
unpriceable sibling, so every later sibling never got a decision at all
(fixed in the M33 solver-parity rewrite, not a seed gap). Mystic Runestone
was never priced in Spirit Shards; it is a 1-gold coin purchase from
Miyani, already correctly seeded. The self-referential Obsidian Shard
recipe (id -496: 1 Obsidian Shard + 1 Mystic Coin + 1 Pile of Putrid
Essence + 1 Mini Risen Priest of Balthazar -> 3 Obsidian Shards) is
genuine wiki-documented game data (a real, obscure festival-junk-to-shard
Mystic Forge combo), not a scraper artifact - the solver's per-path
visiting-set cycle guard already handles it safely, and the same is true
of the ~98 similar self-referential salvage-trophy tier-up recipes found
elsewhere in the seed (their large propagated quantities are correct,
wiki-scale arithmetic, not a bug). The one real gap: Mystic Clover
(19675) had no Mystic Forge recipe seeded at all despite being needed 77x
by Mystic Tribute - added this milestone (recipe -1591: 1 Obsidian Shard +
1 Mystic Coin + 1 Glob of Ectoplasm + 6 Philosopher's Stone, EV-priced at
the wiki-documented 0.31 success rate from a 40k-sample community study).
Also added: 20 missing item names (Mystic Runestone, Tribute to the
Exitare, Mystic Clover, and 17 others an offline Harness dump showed
rendering "Unknown Item") and one acquisition hint - Gift of Battle, whose
only vendor-purchase path (Battle Master, 500 Badges of Honor) was removed
in the Spring 2016 Quarterly Update per a fresh wiki check; it is WvW
reward-track only now. Gift of Glory and Gift of War were also checked
against this same "believed no-source" pattern and are NOT genuine gaps -
both have a real, already-seeded Miyani vendor purchase priced in
currently-tradable materials (Shard of Glory / Memory of Battle), just
unresolvable in the offline Harness (no live TP data there); no hint was
added for either, since one would be actively misleading. Verified via the
Harness's new --dump-tree flag: Exordium, Gift of Exordium, Mystic Tribute,
Gift of Maguuma Mastery, Gift of Condensed Magic/Might, and Mystic Clover
all now resolve to a real Craft/Vendor decision instead of Unknown.
Follow-up: ref/vendor_offers.json still carries a stale "Battle Master,
500 Badges of Honor" offer for Gift of Battle scraped from the wiki's
historical (removed) vendor section, which currently masks the new hint
above (item 8's precedent shows the acquisition-hint system only applies
to genuinely Unknown nodes) - worth a future VendorOfferUpdater cleanup
pass, out of scope for this seed-data milestone.

**Follow-up resolved (M33 Wave B polish, 2026-07-20):** the stale
Battle Master / 500 Badges of Honor offer was removed from
ref/vendor_offers.json after two independent wiki re-checks confirmed
the purchase path was removed in the Spring 2016 Quarterly Update. Gift
of Battle now correctly resolves Unknown with its WVW acquisition-hint
badge visible instead of a bogus BuyFromVendor decision (confirmed via
a rebuilt offline Harness --dump-tree run). The same pass also named the
remaining ~26 base-tier materials (ores, dust, leather, venom sacs, Tiny
Claw, etc.) that still rendered "Unknown Item" in the Harness dump.

## 18. Multi-source decision display is inconsistent
Glob of Ectoplasm shows a VENDOR pill while its pricing uses TP. Items
available from multiple sources should expose all sources with the
selected one highlighted (gw2e-style), and the displayed decision MUST
match the price basis actually used by the solver. Audit the
decision-to-pill mapping for every source combination.

## Handoff notes for the implementing session
- Project memory holds the environment + working rules: the
  Blish-over-Paint screenshot loop (input routing: Paint focused for
  wheel/drag, Blish focused for typing; corner icon at +320,0; idle
  gate + activate-verify + cursor-drift protocol), orchestrate-dont-
  implement (sonnet subagents), and the QueueMainThreadUpdate same-frame
  drain / FrameTicker / MainThreadMarshal primitives.
- Every runtime change: adversarial review gate, then PR -> CI ->
  self-merge. Visual fixes verified in the screenshot loop.
- Tests: 512 green on master (799a4c5). Build/test commands in CLAUDE.md.
