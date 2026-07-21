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
- Remaining parity pillars: Mystic Clover EV (blocked on probability
  data - seeder first), vendor cap data scraping, phase-2
  owned-materials-as-competing-source, localization. Multi-item plans
  landed in M35 (see that section below) - last item removed from this
  line.
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

## 20. M34: gw2efficiency owned-materials parity + correctness fixes

Follow-on milestone after M33's KNOWN-ISSUES 12-18 closure (master
`e486f86`, 636 tests). Two research reports
(`m34-r2-gw2e-owned-materials.md`, `m34-r3-gw2e-caps-and-misc.md`) and two
live-oddity root-cause investigations (`m34-m1-owned-materials-map.md`,
`m34-m2-live-oddities.md`) preceded implementation. 723 tests green at
milestone end.

### 20.1 Correctness fix: Obsidian Shard 179x showed Total 186, not 180 (M34-B1 #1)

Real bug, reproduced bit-for-bit via the offline Harness (`--profile 2
--dump-tree`). Obsidian Shard (item 19925) is needed via five separate tree
occurrences (a self-referential Mystic Forge recipe re-expands it once per
branch - three duplicated Vision Crystal branches at qty 4 each, plus two
direct Mystic Clover-chain branches at qty 83 and 84). The winning vendor
offer is a 3-for-3-Laurels bulk purchase. `PlanSolver` computed
`unitsNeeded = ceil(node.Quantity / offer.OutputCount)` **once per
occurrence** and summed the already-rounded results:
`ceil(4/3)+ceil(4/3)+ceil(4/3)+ceil(83/3)+ceil(84/3) = 2+2+2+28+28 = 62`
purchases x 3 = **186 Laurels** - a real 6-Laurel (+3.3%) overcount versus
the correct aggregate-first answer, `ceil(179/3)*3 = 180`. This directly
contradicted gw2efficiency's own documented convention
(`craftingSteps.ts`: batch counts are ceil'd only after all same-id steps
across the whole tree are merged). Fixed by tracking each item's winning
vendor offer batch shape across every occurrence and re-deriving the merged
step's true cost from AGGREGATE demand with a single ceil
(`PlanSolver.FinalizeVendorBatches`); occurrences that genuinely picked
different offers are left as the sum of their own already-correct
per-occurrence costs rather than forced through one offer's batch shape.
The identical per-node `unitsNeeded` scaling also applies to a bulk offer's
coin cost, so any item needed via 2+ tree occurrences and priced via a
same-offer bulk vendor purchase was equally exposed, not just this one
Obsidian Shard repro.

Secondary, independent issue noted but only partially addressed: even at
the correct 180 total, a truncating-average "Each" price for a
batch-purchased currency row (`180/179` still truncates to a
non-representative "1") is inherently a poor fit - gw2e's own shopping list
never shows a per-unit currency price at all. M34-B1 #2 changed the Each
cell to resolve the WINNING OFFER's own true per-batch rate
(`PlanStep.VendorOfferOutputCount`/`VendorOfferCurrencyCostLinesPerBatch`)
instead of the old truncated total/quantity average, rendering a literal
"N for M" bundle label when that rate is fractional, and omitting the Each
cell entirely (never a guessed rate) when a merged row's occurrences used
more than one distinct offer.

### 20.2 Cap-parity change: vendor purchase caps no longer hard-exclude an offer (M34-B1 #3)

`m34-r3-gw2e-caps-and-misc.md` verdict: gw2efficiency's daily/weekly
purchase caps are informational display only (a post-solve "this'll take
you N days" banner) - they never re-route the solver, exclude an offer, or
change a craft/buy decision. Our solver's pre-M34 `EvaluateVendorOffers`
did the opposite: once a node's own occurrence-local quantity exceeded a
cap, it silently excluded that vendor offer from evaluation entirely, which
would (the moment any cap data is ever seeded - today 0 of 53,530 wiki
offers carry cap data, so this was previously inert) make the solver pick a
different, possibly worse, path than gw2e for the same node. Fixed to match
upstream: caps no longer gate offer evaluation; cap-exceeding merged demand
now surfaces as a `CraftingPlan.TimegatedItems` entry, rendered as a plain
informational row in the Crafting Steps section instead of silently
re-routing the plan.

### 20.3 Status race: stale "Building final result..." status line (M34-B1 #4)

Root-caused via `m34-m2-live-oddities.md`: a generation's own trailing
progress tick (routed through `Progress<T>`'s default
`SynchronizationContext`, two ThreadPool hops) and that same generation's
completion write (an inlined task continuation, effectively one hop) race
for Blish's `QueueMainThreadUpdate` queue with no FIFO guarantee between
them. In practice the completion write ("Plan generated...") reliably
drained first, so the late-arriving trailing tick overwrote it right back
with the stale "Building final result..." text - and nothing wrote to the
status label again for that generation. The pre-existing `myGen ==
_generateSequence` guard could not catch this: both callbacks belong to the
SAME generation, so the guard (designed to reject a superseded generation's
stale callback) passed for both. Fixed with a per-generation
`_statusClosedForCurrentGeneration` flag, set the instant a generation
writes its own completion/error status; the progress-tick callback now
checks this flag too (via the new, pure `StatusUpdateGuard.ShouldApply`)
before every `SetStatus` call, closing the race at drain time regardless of
which callback happens to actually drain first.

### 20.4 Owned-materials parity scope (M34-B2a + M34-B2b)

Two research reports (`m34-r2-gw2e-owned-materials.md`,
`m34-m1-owned-materials-map.md`) found gw2efficiency's owned-materials
model diverges from ours in several ways; M34-B2a/B2b closed the ones in
scope:

- **Per-node owned attribution** (B2a #1): a new `CraftingTreeNode.
  OwnedQuantityUsed` field (threaded from a new `ReducedTreeResult.
  OwnedQuantityUsedByNode` side channel, keyed by stable `NodeId`) makes a
  PARTIALLY-owned node representable for the first time - previously only
  fully-owned nodes (reduced to `Decision.Have`) were visible at all, and a
  node whose item id recurred elsewhere in the tree had no way to attribute
  "how much did THIS node use" (the old `UsedMaterials` list is aggregated
  by item id only).
- **Primary-option-only pool consumption** (B2a #2): `InventoryReducer`
  previously walked EVERY `RecipeOption` on a node when consuming owned
  stock, letting an alternate recipe the solver would never choose drain
  the shared pool meant for a real branch. Now only the primary
  (first-listed) option recurses with consumption; every option's
  ingredient quantities are still rescaled so the solver's cost comparison
  across options stays correct.
- **"Value Own Materials" force-buy pre-pass** (B2a #3): gw2e's
  `valueOwnItems` setting is a genuine pre-pass that force-excludes craft
  from nodes where buying beats a 15%-discounted craft cost
  (`buyPrice < craftDecisionPrice * 0.85`) - our prior `OwnMaterialsMode.
  Valued` only adjusted a downstream profit number and had ZERO effect on
  which items got crafted (a real parity gap, since the target item in
  this project's Exordium-precursor use case is always account-bound and
  therefore never has the sell price the old profit adjustment needed
  anyway). `OwnedMaterialsForceBuyPrePass` now applies gw2e's exact rule
  against a genuine zero-owned baseline solve, and `ModuleSettings.
  ValueOwnMaterials` defaults to `true` (matching gw2e) with its first
  Settings-tab checkbox. Deliberately narrower than gw2e's always-on gate:
  it only activates when a real snapshot is actually driving reduction, so
  the new default doesn't surprise a user who has never enabled "Use Own
  Materials" with newly forced-buy decisions.
- **Owned currency is display-only, never fed back into the tree** (B2a
  #4 + B2b): matching gw2e's own two-tier design (item ownership is a real
  structural input to the algorithm; currency ownership is a cosmetic
  annotation plus a downstream summary-only netting, Section 4 of the r2
  report), `AccountCurrencyIndex` wraps the wallet snapshot the same way
  `AccountItemIndex` wraps owned items, but is consulted ONLY after solving
  - `CraftingPlanResult`/`PlanSolveContext.OwnedCurrencyAmounts` are
  populated strictly from the plan's already-final currency totals, never
  read by `InventoryReducer` or `PlanSolver`. A regression test proves
  decisions/costs are identical with and without wallet data. B2b then
  surfaced this previously-plumbed-but-unrendered data: the Total Cost
  section's currency rows and the Shopping List's vendor currency cells now
  show an "(X owned, Y needed)" annotation when wallet data is present
  (Total Cost inline; Shopping List as a tooltip, to avoid new row-height
  layout math for a cosmetic-only addition) - byte-identical to before when
  no wallet snapshot exists.
- **"Using N owned materials" pill** (B2b): `DecisionPillPlanner` now
  emits a non-interactive `PillKind.OwnedInfo` pill ("USING N OWNED")
  alongside a node's normal CRAFT/TP/VENDOR/UNKNOWN pill whenever
  `OwnedQuantityUsed > 0`, matching gw2e's own
  `usedQuantity < totalQuantity` condition. A fully-owned node (collapsed
  to the single `HAVE` pill) deliberately keeps that existing plain
  treatment rather than also showing the annotation - a scope decision, not
  an oversight (gw2e's own live tree does not collapse a fully-owned node
  to a single pill the way this module's M28 UI simplification does, so
  there is no exact upstream precedent to follow for this specific
  combination).
- **"Ignore" pill** (B2b): gw2e's per-item "treat this as fully in-hand
  tree-wide" override is implemented as a NEW per-solve `ignoredItemIds`
  parameter threaded through `PlanSolver.Solve`/`Evaluate`/`Collect`
  (keyed by ItemId, not NodeId, matching gw2e's "every occurrence of that
  item id" semantics) and `CraftingPlanPipeline.ResolveWithOverrides` -
  reusing the same local-resolve machinery M21's craft/buy pill clicks use,
  with the ignored-id set held as view-session state
  (`CraftingPlanView._ignoredItemIds`) alongside, but independent of,
  `_nodeOverrides` (neither "Best Path" nor "Craft All"/"Buy All" clears
  it, matching gw2e's own documented "bulk actions are unrelated to
  ownership" behavior). An ignored item contributes zero cost, generates no
  crafting step or shopping row, and its own recipe's ingredients are never
  evaluated (matching gw2e's "an un-crafted branch never asks for its
  ingredients" rule) - `CraftingTreeBuilder` collapses it to the same
  `Decision.Have` display a genuinely-owned node gets, but sets a new
  `CraftingTreeNode.IsIgnored` flag so the pill layer keeps showing an
  active, clickable "IGNORED" toggle (distinct from a naturally-owned
  node's plain, non-interactive `HAVE` pill) for un-ignoring.
  **Conservative reading recorded per the milestone brief**: gw2e's own
  Ignore mechanism works INDIRECTLY, by injecting a synthetic
  `source: "Ignored"` entry into the owned-materials accounting layer and
  letting the existing `calculateTreeQuantity`/`calculateTreePrices` re-run
  naturally re-derive zero cost and zero-quantity cascading through
  descendants; this module's `InventoryReducer`-based reduction already ran
  once, before `PlanSolveContext.Tree` was even captured, and re-running it
  locally (no network calls, per the existing local-resolve contract) is
  out of reach of the current architecture (`m34-m1-owned-materials-map.md`
  Section 5 explicitly flags this as needing "a new mechanism"). The
  chosen, narrower-but-correct substitute: zero the ignored node's OWN cost
  contribution directly at solve time (matching gw2e's END STATE for that
  node - Section 2.1's "owned units are free, full stop" - and Section
  5.2's "no crafting step for zero demand") without attempting to cascade
  zero-quantity scaling down through an ignored node's own descendants the
  way gw2e's real quantity-computation pass does (out of scope; those
  descendants simply are never visited at all here, rather than being
  visited-but-showing-zero, since the ignored node's own recipe traversal
  is skipped entirely). Scoped to Item nodes only (no Currency-Ignore
  support, since gw2e's Currency-Ignore path funnels through the wallet
  accounting layer this module doesn't have wired into the tree at all -
  see the B2a #4 note above).
- **Known, deliberately out-of-scope gaps** (flagged, not fixed this
  milestone): the multi-recipe-option pool-consumption risk (m1 report
  Section 6.2 point 5 in the r2 report / Section 1 point 5 of the m1 map)
  is explicitly NOT the same bug as B2a #2 above (that fix was
  primary-option-ONLY consumption in `InventoryReducer`; `PlanSolver.
  Evaluate` itself still walks every recipe option for cost-comparison
  purposes, which is safe there since it is read-only); achievement-bit
  ingredient de-duplication (r2 report Section 1.5) has no equivalent in
  this module at all - niche (only matters for the small set of GW2
  recipes with a one-time achievement-reward ingredient reused across
  multiple tree branches) and out of scope for this pass.

**VERIFICATION STATE**: all of the above is confirmed by a green build and
the full 723-test suite (real production code paths - `PlanSolver`,
`CraftingTreeBuilder`, `CraftingPlanPipeline`, `DecisionPillPlanner`,
`CurrencyDisplayResolver`, `PlanViewModelBuilder`), plus the Obsidian Shard
180-vs-186 numbers reproduced bit-for-bit against the real Exordium tree
via the offline Harness (`m34-m2-live-oddities.md`). **Not yet re-confirmed
by a live in-game desktop check** (screenshot loop) for any of the M34-B1/
B2a/B2b visual changes - the pill layout in particular (a node can now show
up to 5 pills: one of CRAFT/TP/VENDOR/HAVE/UNKNOWN, an optional
"USING N OWNED", and an "IGNORE"/"IGNORED" toggle) has an acknowledged,
unverified overflow risk on a deeply-nested, narrow-panel tree row (the
M33 m1 map's own "up to six pills" ceiling note) - treat a fresh
pill-overflow or clipped-pill report as expected-until-checked rather than
a regression.

## 21. M35: gw2efficiency parity - multi-item plans

Final major gw2efficiency parity pillar (see the "Remaining parity
pillars" line above). Preceded by a research report
(`m34-r1-gw2e-multiitem.md`) documenting gw2e's exact mechanism: the
Calculator's `e.recipes` array (N `{id, amount}` rows, add/remove/reorder,
one shared settings panel for the whole batch) is wrapped, at Generate
time, under a single synthetic fake parent node (`id: false`, name
`"Multiple recipes"`, `multipleRecipeTree: true`, `quantity: 1`,
`output: 1`, `components` = the N real item trees each carrying its own
requested amount as its own `quantity`) and fed through the SAME
single-root `cheapestTree` solver unmodified; the fake node is never
rendered (`componentTree.html`'s own `ng-if`), so the Recipe Tree section
shows what looks like N independent top-level trees; Shopping List/
Crafting Steps/Required Disciplines/Required Recipes are generic per-id
tree walkers with no multi-item-specific code at all, so they merge
automatically; Cost Breakdown drops its per-item "(per item)" sub-lines
and adds a "Profit numbers are the sum of all crafted recipes" banner.

### 21.1 B1: synthetic wrapper pipeline (Services layer)

`RecipeService.BuildMultiItemTreeAsync` builds each requested item's own
tree via the existing single-item `BuildTreeAsync` path, then wraps 2+ of
them under a synthetic root `RecipeNode` using new
`Gw2Constants.MultiItemWrapperItemId`/`MultiItemWrapperRecipeId` sentinels
(`int.MinValue` - real GW2 ids are always positive, so these can never
collide with a genuine tree item/recipe). A single-entry request returns
that item's own tree UNWRAPPED - gw2e's own `if (r.length===1) return
r[0]` short-circuit, verbatim. `PlanSolver.Collect`/`CraftingTreeBuilder`
hide the wrapper's own throwaway "craft" decision and sentinel id
everywhere (no step, no craft-order entry, no vendor-batch entry, no
metadata fetch ever targets it) - mirrors `componentTree.html`'s own
`ng-if="!component.multipleRecipeTree"`. `CraftingPlanPipeline` gains a
`GenerateStructuredAsync(IReadOnlyList<PlanRequestItem>, ...)` overload
that delegates straight to the existing single-item method for exactly one
item (byte-identical output, confirmed by a regression test asserting the
two paths produce identical `CraftingTree`/steps/disciplines/recipes down
to every field), and to a new `GenerateStructuredMultiAsync` for 2+ -
which mirrors the single-item pipeline step for step (force-buy pre-pass,
inventory reduction, solve, vendor-batch finalization) with the wrapper
tree standing in for a single item's tree throughout, so M34's merge-then-
ceil correctness fix (`FinalizeVendorBatches`) and the force-buy pre-pass/
Ignore-pill overrides apply across ALL requested items' shared materials
for free, not just within one item's own tree (regression-tested: two
items each needing 2 of a bulk-vendor-only shared material, `ceil(2/5)+
ceil(2/5) = 2` purchases solved independently vs. `ceil(4/5) = 1` purchase
solved as a merged batch - the merged answer is what the pipeline
produces). Sell-side economics (profit/net-sale-value) are deliberately
left unset for a multi-item batch - see 21.3's divergence note.

### 21.2 B2: multi-row UI (Views layer)

- **Input strip**: the single search-box+qty strip becomes a vertical list
  of item rows (`CraftingPlanView._itemRows`, one `ItemRowState` per row -
  search box, qty box, Remove button), echoing gw2e's own `e.recipes`
  ng-repeat. A Remove button only renders once 2+ rows exist
  (`ItemRowRequestBuilder.CanRemoveRow` - gw2e's own
  `ng-if="recipes.length > 1"`), and an Add button sits on the trailing
  edge of the LAST row only (rather than gw2e's own separate "Add another
  item" link row) - a deliberate simplification that keeps the single-row
  case's row height/position byte-identical to pre-M35
  (`ComputeTopRegionLayout`'s own doc comment proves the N==1 formula
  reproduces the old fixed Y-offset constants exactly). Reordering rows
  (gw2e's `moveRecipe` up/down arrows) is NOT implemented - out of scope
  for this milestone (see the divergences below).
- **Tree render**: N top-level trees stacked in the Recipe Tree section's
  single shared content FlowPanel, wrapper hidden - falls out almost for
  free, since each requested item's own root `CraftingTreeNode` already
  IS a full icon/name/quantity/pill/cost row (the same shape a single-item
  plan's tree root always was), so `CreateTreeSection` simply loops
  `RenderTreeNode` once per root instead of once total, with a thin visual
  divider (`PlanContentHeightMath.MultiRootDividerHeight`) between
  consecutive roots only (never for a single root). Total Cost/Cost
  Breakdown adds a plain-text "Totals above are the sum of all crafted
  recipes in this batch." row (`PlanRowType.MultiItemNote`) only in multi
  mode - reworded from gw2e's own "Profit numbers are..." banner since this
  module does not yet compute multi-item sell-side profit at all (see
  21.3). Section machinery itself (Total Cost, Shopping List, Crafting
  Steps, Required Disciplines/Recipes) needed ZERO section-builder changes
  beyond the note row - they already operate on `CraftingPlanResult`'s
  already-merged `Plan.Steps`/`UsedMaterials`/`RequiredDisciplines`/
  `RequiredRecipes`, which the B1 pipeline populates correctly for a batch
  the same way it always has for one item.
- **M33/M34 contracts preserved**: `PlanContentHeightMath` gained
  `MultiRootTreeFlowHeight`/`MultiRootDividerHeight` (a one-root list is
  proven byte-identical to the pre-M35 single-tree height via a dedicated
  test) and a `MultiItemNote` branch in `SummaryBodyHeight`; the new
  divider Panel registers a width-only relayout closure like every other
  chrome element in the file; `DecisionPillPlanner`/pill click handling is
  completely untouched (each root node is walked by the same
  `RenderTreeNode` recursion as before, so USING N OWNED/IGNORE keep
  working per-node exactly as before); `PreserveScrollAcross`/
  `PreserveScrollAcrossResize` wrap the batch render and the row Add/Remove
  reflow respectively (the latter also arms the settle-time scroll-verify
  directly, since a discrete one-shot row-count change - unlike a
  continuous resize drag - never generates the further ticks
  `ResizeSettleStep`'s own debounce relies on); the status pipeline is
  unchanged (`TriggerGenerate`'s existing per-generation `myGen`/
  `_statusClosedForCurrentGeneration` guard already covers a batch
  generation the same way it covered one item, since nothing about the
  guard is item-count-specific).

### 21.3 Known divergences from gw2e's own multi-item UX

- **No row reordering** (gw2e's `moveRecipe` up/down arrows): not
  implemented. B2's own task scope named only "search box, qty, remove
  button" for each row; reordering was judged non-essential polish and
  left out to keep the milestone bounded.
- **No URL/file persistence of the row list**: gw2e's own multi-item state
  lives entirely in a shareable URL (`?item=...` / `/crafting/calculator/
  <encoded>`), which has no analog in a Blish HUD module (no address bar).
  The row list instead persists as in-memory session state
  (`CraftingPlanView._itemRows`) across tab switches within the same
  Blish HUD session, exactly like `_nodeOverrides`/`_ignoredItemIds`
  already did - lost on module reload/game restart, matching how every
  other piece of this view's session state already behaves.
- **No multi-item sell-side economics**: gw2e's own multi-item Cost
  Breakdown drops the per-item view and sums profit across every selected
  item, plus exposes a multi-item-only "sell excess crafted components for
  profit" rollup. Neither is implemented - `CraftingPlanResult.
  SellableQuantity`/`NetSaleValue`/`CraftingProfit`/
  `MaterialOpportunityCost` stay at their type defaults for a multi-item
  result (`GenerateStructuredMultiAsync`'s own doc comment), since "what
  would selling N independently-selected items net" has no obvious
  single-number generalization the way it does for one target item. The
  new Cost Breakdown note is worded around this (see 21.2) rather than
  echoing gw2e's "Profit numbers..." text verbatim, since this module
  currently shows no profit figure at all in multi mode to be "the sum
  of." A future milestone could add a batch-level profit rollup.

**VERIFICATION STATE**: build green, full test suite green (Blish-free
production-path tests: `ItemRowRequestBuilderTests` for the row-list pure
logic, `PlanViewModelBuilderTests` for the per-root viewmodel mapping/
title/note-row gating, `PlanContentHeightMathTests` for the multi-root
height arithmetic including the byte-identical-at-N==1 proof, plus the
existing B1 `MultiItemPlanTests`/`PlanSolverTests`/`RecipeServiceTests`
coverage of the wrapper pipeline itself). **Not yet verified by a live
in-game desktop check** (screenshot loop) - the multi-row input strip's
visual layout (row spacing, Add/Remove button placement, the tree
section's inter-root divider) and the dynamic top-strip reflow when adding
a second/third row are reasoned from the same explicit-height math this
file already relies on elsewhere, but have not been visually confirmed
against a running Blish HUD instance. Treat a fresh multi-row layout
report as expected-until-checked rather than an automatic regression.

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
