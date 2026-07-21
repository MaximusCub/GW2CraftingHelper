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
- Scroll verify's reset-vs-user discriminator (M33 C2a superseded the
  M30-era guard): the verify window is now only 2-3 real frames and
  suppresses its own zero-reassert whenever a wheel event landed in the
  last 250ms, so a user who just wheeled to exactly the top is not
  bounced at all. A non-wheel-driven arrival at exactly zero (e.g. a
  script/automation setting VerticalScrollOffset directly) within that
  short window could still be bounced up to 4 times before the cap gives
  up contesting it (accepted tradeoff; revisit only if reported).
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

## 12. Fast wheel-up scroll: net-downward stutter (FIXED in M33, live-verified)
Rapid successive wheel-up events make the viewport scroll up then jump
back down further than it went up - net downward movement with an
upward stutter. Hypothesis: scroll guard/restore machinery (or some
per-frame interaction with Blish's Panel wheel handling) contesting
rapid user input. Reproduce with the proven instrument-first loop
(synthetic rapid wheel-up while a guard window could be live and while
idle) before designing a fix.
Fixed in M33 (C2a): a live instrumented capture (c12-baseline-analysis,
2026-07-20) confirmed the root cause - the old restore Tick's
divergence check required contentHeight to be unchanged frame-to-frame
before it could trust the scrollbar's live value over its own target,
and nested AutoSize convergence kept contentHeight fluctuating for
several real frames after every rebuild, so a wheel notch landing in
that window was silently overwritten. Container heights (section
bodies, recipe-tree child containers) are now finalized synchronously
at build time via PlanContentHeightMath, removing the fluctuating-height
window entirely. On top of that, the post-restore verify window now
yields immediately on any observed wheel event (no heightUnchanged
precondition), so a user's wheel input during a live restore/verify
window is never contested.
VERIFICATION STATE: the above is confirmed against the c12-baseline-
analysis (2026-07-20) live instrumented capture of the OLD code, plus
Blish-free unit tests of the pure math helpers (PlanContentHeightMath,
ScrollMath) and a green build against the vendored Blish HUD v1.3.0
source. LIVE-VERIFIED 2026-07-20 (instrumented desktop capture of THIS
code): a wheel-up event landing INSIDE a live verify window (verifyLive
=True in the trace) triggered an immediate "verify exit
reason=wheel-observed" with no contested write, and a fast wheel-up
burst then descended monotonically to exactly 0.0000 and stayed there
- no zero-reassert bounce at top. Idle fast bursts remain clean.
(Note: Blish's own per-frame wheel coalescing still drops ~40% of
notches in very fast bursts - stock library behavior, scrolls shorter
than intended but never backwards.)
(M33 fix-pass note: an earlier revision of this fix also suppressed the
zero-reassert contest whenever a wheel event had landed within the last
250ms of wall clock, intending to protect a user who "just wheeled to
exactly the top." That suppression was removed - it could only ever
trigger for a wheel that predated the restore window arming, in which
case the saved scroll offset reflects the user's real, non-top
position, and suppressing the reassert would abandon restoring it,
reintroducing the #14 top-jump. The genuine "wheeled to exactly top"
case never reaches the verify window at all: PreserveScrollAcross skips
the restore/verify entirely when the saved offset is already 0.)

## 13. Resize UX rework: live reflow, no settle stutter (FIXED in M33, live-verified end-state; drag-tick perf unmeasured)
The 150ms debounce-only approach is REJECTED by user feedback: content
must reflow smoothly WHILE dragging, not lag until the mouse holds
still. Additionally the settle rebuild itself is visibly ugly: stray
horizontal divider lines flash and the view visibly reconstructs from a
collapsed state (nested AutoSize convergence made visible). Direction:
in-place width relayout of EXISTING controls during drag (update
widths/x-positions; no dispose+rebuild), making the settle rebuild
unnecessary or invisible. This is the previously-rejected "option b" -
now required; design it to avoid drift between build and relayout paths.
Fixed in M33 (C2b): the 150ms dispose+rebuild (ResizeDebounceStep calling
PreserveScrollAcross(() => RenderPlan(...))) is gone entirely. Every
CreateX... builder now also registers a small closure (a per-render
List<Action<int>> relayout registry, lifecycle mirrors the tree's own
per-render node-state list) that repositions/resizes its EXISTING
controls for a new panelWidth; OnPanelResized replays the full registry
synchronously on every real drag tick - live reflow while dragging, no
debounce wait. This was provable safe specifically because of the M33
C2a height work already on this branch: since every row/container height
is finalized synchronously at build time (PlanContentHeightMath) rather
than left to Blish's multi-frame AutoSize convergence, a pure width/X
write on a fixed-height row cannot re-trigger that convergence window -
the exact "blind-overwrite window" and paint-at-scroll-0 mechanism the
#12/#14 baseline capture (c12-baseline-analysis, 2026-07-20) measured
only ever opens when a rebuild changes content height. A relayout that
never touches Height therefore cannot open it, so the settle rebuild's
flash/divider-glitch/scroll-contest triad simply has no trigger left -
confirmed by construction, not raced against. The one remaining
text-measurement cost (the 3 EllipsizeToWidth call sites - Used
Materials, Shopping List, and Tree row names) is deferred to a short
trailing settle pass (same FrameTicker debounce mechanism, repurposed)
that only reassigns Label.Text/tooltip on existing labels - still no
rebuild, no height change, no scroll disturbance. Width-dependent
arithmetic (tree column anchors, cost-tile geometry, header centering,
name-column budgeting) was extracted into a new Blish-free
Services/PlanRelayoutMath.cs (unit tested), mirroring ShoppingColumnMath,
so the build path and every relayout/re-ellipsis closure share one
source of truth and cannot drift apart. A DEBUG-only check asserts no
relayout closure ever moves the scrollbar, and a DEBUG-only log fires if
a section renders rows but registers no relayout closure, so a future
section type that forgets to wire this up fails loud instead of
silently freezing at build-time width on later resizes.
VERIFICATION STATE: the visible-glitch elimination above (no dispose+
rebuild, no scroll disturbance) is confirmed by construction against
the M33 C2a height work plus Blish-free unit tests of
Services/PlanRelayoutMath.cs, and a green build. LIVE-VERIFIED
2026-07-20 (desktop screenshot loop, real Exordium plan ~8.6k px
content): a synthetic grip drag resized the window and the end-state
layout was fully correct at both the narrower and re-widened widths
(header centering, right-anchored cost columns, pills, buttons), with
zero scrollbar writes during width reflow and label ellipsis correctly
restored at settle. NOT yet measured live: per-tick smoothness/cost on
a fully-EXPANDED tree. In particular, ReplayRelayout now replays the full relayout
closure registry synchronously on every real drag frame (previously:
once, 150ms after the drag settled) - a genuine change in perf
character. The SuspendLayout/ResumeLayout batching is a real,
reasoned mitigation (see ReplayRelayout's doc comment), but its
wall-clock cost per drag tick on a large plan has not been measured
against a live running Blish instance. If a user reports stutter/lag
while dragging the window edge on a large plan, treat it as this
still-open measurement gap, not a regression to re-diagnose from
scratch.

## 14. Pill-click viewport flash (jump to top and back) (FIXED in M33, live-verified)
Clicking a TP/VENDOR override pill visibly flashes the view to the top
for an instant before the scroll restore re-asserts. The restore
converges but applies a frame+ late. Direction: apply the saved scroll
ratio synchronously inside PreserveScrollAcross immediately after the
rebuild (before first paint), with the FrameTicker loop only defending
convergence afterward - target: zero visible movement.
Fixed in M33 (C2a): PreserveScrollAcross now writes the restore ratio to
the scrollbar synchronously immediately after the rebuild returns, using
container heights that are already finalized (not still collapsed at
Height=0 awaiting AutoSize convergence) - nothing paints between the
rebuild and the correct restore write landing, so there is no wrong
position left for the user to see. A short (2-3 real frame) FrameTicker
verify still runs afterward, but only to contest Blish's own single
expected post-rebuild scrollbar reset, not to converge toward a still-
moving target.
VERIFICATION STATE: confirmed by construction against the M33 C2a
height work (the c12-baseline-analysis trace was captured against the
OLD code) plus Blish-free unit tests of the pure math helpers and a
green build. LIVE-VERIFIED 2026-07-20 (two independent instrumented
pill-click captures of THIS code): [scrolldiag] shows writer=SyncRestore
writing the correct ratio synchronously with content height already
final (no convergence drift), exactly one same-frame Verify/zeroReassert
contest of Blish's single expected reset, and "verify exit reason=stable"
at realFrame 2; pre/post screenshots are pixel-identical in viewport
position, including across an override that changed content height
(absolute pixel offset correctly preserved). Baseline for comparison:
the OLD code showed 4+ frames of contested writes with drifting ratio.

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

## 19. Resize-drag scroll reset on height change (FIXED in M33 C2c)
Live-verified regression from M33 C2b's live in-place relayout: any window
resize drag that changes the content panel's VIEWPORT HEIGHT (dragging the
bottom edge or a corner) reset scroll to the top, with no [scrolldiag]
writes during the drag at all. Mechanism: the settle rebuild C2b removed
used to run inside PreserveScrollAcross, so scroll was restored as a side
effect of that wrapper; the new live-reflow path never rebuilds, so
nothing contested Blish's own Scrollbar.RecalculateLayout, which zeroes
ScrollDistance whenever _scrollbarPercent (viewportHeight/contentHeight)
changes - which a height-changing drag does on every tick. Width-only
reflow was and remains unaffected (verified live: no scroll disturbance).
Confirmed by decompiling packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe
(Blish_HUD.Controls.Scrollbar/Panel): the reset is NOT synchronous inside
the Height setter - the nested scrollbar Height write that Panel's own
"Height" PropertyChanged handler triggers runs before Panel.RecalculateLayout
has refreshed ContentRegion, so it reads a still-stale ratio and sees no
change; the real reset comes from Scrollbar.DoUpdate's unconditional
per-frame Invalidate(), which by then sees the refreshed ContentRegion and
lands the reset a real frame (or two) after the resize tick - the same
delayed-reset window StartScrollVerify already exists to contest for
rebuilds (item #14).
Fixed in M33 (C2c): OnPanelResized now captures the content panel's
absolute scroll offset in pixels before its Height changes on every
resize tick, and - only when the tick actually changed content-panel
height - re-applies it synchronously afterward via the new
PreserveScrollAcrossResize (same ScrollMath.RatioForOffset conversion
ApplySavedScrollSynchronously already uses for rebuilds), logged as
[scrolldiag] writer=ResizePreserve. A second, less obvious fix was needed
for the write itself to actually stick: on a pure height-only tick
(dragging just the bottom edge, no width change) nothing else touches the
scrollbar first, so Scrollbar's own cached percent is still stale when
PreserveScrollAcrossResize runs; writing ScrollDistance directly would
itself trigger the vendor's RecalculateLayout for the first time against
the now-fresh ContentRegion, which would detect the change and reset back
to 0 synchronously, undoing the write within the same statement. The fix
calls scrollbar.RecalculateLayout() directly first (bypassing Control's
once-per-LayoutState UpdateLayout guard) to force that stale-to-fresh
transition - and its harmless, invisible-since-synchronous reset - to
happen under this method's control, so the restore write immediately
after is the one that lands. A rebuild does not need this extra step:
PreserveScrollAcross's mutate() already churns through many of the
content panel's own direct children, each reaching
Panel.UpdateContentRegionBounds and forcing the same transition
organically before ApplySavedScrollSynchronously ever writes.
This keeps every tick visually correct without a per-tick verify ticker
(which would spam a new FrameTicker on every drag frame); a single
bounded verify window is
instead armed once, at drag settle (reusing the existing
ResizeDebounceStep/StartScrollVerify machinery unmodified), to contest
the one trailing later-frame reset the vendor source shows can still land
after the drag's last tick. The settle ticker itself - previously
scheduled only when width changed - is now also scheduled on a
height-only change (e.g. dragging just the bottom edge): that drag shape
previously got no settle handling of any kind, which would have starved
even the per-tick write of its trailing verify. A rebuild
(PreserveScrollAcross) clears any pending resize-verify up front, since
it disposes and recreates the very content the pending verify would
otherwise be measured against.
VERIFICATION STATE: confirmed by construction against the decompiled
vendor Scrollbar/Panel source and the existing ScrollMath unit coverage,
plus a green build and full test suite. Not re-confirmed by a live
in-game drag-resize check (screenshot loop) after this specific fix -
treat a fresh scroll-reset-on-height-drag report as reopening this item
rather than assuming it is the same M33 C2b regression recurring.
(2026-07-20 desktop session note: six synthetic grip-drag attempts
across two fresh launches failed to re-catch the TabbedWindow2 resize
grip after this fix landed - the one successful synthetic catch of the
session predates the fix - so the pending live check needs a human
drag. The primitives the fix reuses - pixel-offset capture,
ScrollMath.RatioForOffset, synchronous write, StartScrollVerify - are
each live-verified under item #14's captures.)

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
