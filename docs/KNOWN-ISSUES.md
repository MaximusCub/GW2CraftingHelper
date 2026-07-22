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

## 12. Fast wheel-up scroll: net-downward stutter (REOPENED 2026-07-21; root-caused and FIXED in M36, live-verified)
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
than intended but never backwards. SUPERSEDED 2026-07-21: that
"never backwards" characterization held for the specific burst captured
in this 2026-07-20 session but is not the general case - see the
REOPENED record below for a real vendor bug that does make specific
coalesced multi-notch up-bursts scroll backwards, not just shorter.)
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

REOPENED 2026-07-21 (live user report, after the M33 machinery-side fixes
above and the intervening M34/M35 milestones): the user reported the
original symptom again - fast wheel-up flicks still net downward. An
instrumented [scrolldiag] capture of the user's own physical mouse over
the live Blish-on-Paint session EXONERATED the M33 restore/verify
machinery itself: zero writer interference (no SyncRestore/Verify/
ResizePreserve writes) anywhere near the wheel events in question - the
machinery this milestone's fixes targeted was never the cause of the
reopened report. The trace instead revealed a real bug in the vendored
library: fast multi-notch wheel-UP flicks arrive at the content panel's
MouseWheelScrolled with corrupted raw deltas, exactly (N*120) - 65536 for
the coalesced up-notch count N=2..8 (measured histogram: N=2 -> -65296,
N=3 -> -65176, N=4 -> -65056, N=5 -> -64936, N=6 -> -64816, N=7 -> -64696,
N=8 -> -64576; 47 occurrences), and Blish's own Scrollbar scrolls ONE STEP
DOWN for each such event (it looks only at Math.Sign of the corrupted,
negative-looking delta, never the magnitude). Fast multi-notch DOWN
flicks coalesce cleanly (-240..-840, no corruption); single notches both
directions are clean. Net effect: every fast pair of up-notches produced
a down-step, reproducing the original report exactly.

ROOT CAUSE (confirmed by decompiling the shipped BlishHUD v1.3.0 "Blish
HUD.exe" with ilspycmd, cross-checked against a clone of the matching
public source): Blish_HUD.Input.MouseEventArgs.WheelDelta extracts a
Win32 low-level mouse hook's signed 16-bit wheel delta as unsigned, then
tries to recover the sign by subtracting 65536 whenever the unsigned
value exceeds SystemInformation.MouseWheelScrollDelta (120) - a threshold
that only correctly discriminates a SINGLE notch's two directions. A
coalesced 2+ up-notch event (unsigned 240, 360, ...) is well above 120
and gets "corrected" anyway even though it was never actually a wrapped
negative value, turning e.g. a legitimate +240 into 240 - 65536 = -65296.
See Services/WheelDeltaSanitizer.cs's class doc comment for the full
derivation, including why single notches and coalesced down-flicks are
unaffected.
VERDICT - NOT DebugHelper-only: both Blish_HUD.Input.WinApiMouseHookManager
(the normal path used when directly attached to a running, focused GW2
client) and DebugHelperMouseHookManager (used under this dev machine's
dummy-window mode) construct MouseEventArgs from the identical raw
mouseData field and feed it through the SAME buggy WheelDelta getter -
InputService's ApplicationSettings.Instance.DebugEnabled only chooses
which hook manager supplies that field, not how it gets interpreted. A
real GW2-attached player fast-flicking the wheel upward is exposed to the
identical corruption; this is not a dummy-window/dev-tooling artifact.

Fixed module-side (M36) via a new Services/WheelDeltaSanitizer.cs (pure,
Blish-free, exhaustively unit-tested against the full histogram above
plus boundary values, including lattice-edge tests at the documented
N=46/N=47 threshold boundary) that classifies a raw delta as
wrapped-positive whenever raw <= -60000 (a threshold with a wide safety
margin from every plausible genuine delta - see the class doc comment
for the derivation) and recovers the intended positive delta.
CraftingPlanView's OnContentWheelObserved (already unconditionally
subscribed to _contentPanel.MouseWheelScrolled since M33 C2a) now, on a
wrapped event, cancels Blish's own not-yet-applied single-step-down
Glide tween (GameService.Animation.Tweener.TargetCancel) before it can
land, then writes the position N clean up-notches would have produced
instead, computed in the same pixel space Blish's own per-notch step
operates in (new ScrollMath.ApplyPixelDelta) so a corrected fast flick
composes exactly like N clean single notches rather than a differently-
scaled jump. [scrolldiag] gated logging (writer=WheelWrapFix) records
every correction (rawIn, intendedDelta, before, after); the sanitizer
classification itself is unconditional and zero-allocation.

MECHANISM (M36 fix-pass, re-verified against decompiled Glide rather
than assumed): a review of this fix theorized TargetCancel was a no-op
here, on the premise that Glide defers a new Tween's by-target
dictionary registration to the NEXT Tweener.Update() call - decompiling
Glide.Tween.TweenerImpl.Tween<T>() from the shipped Blish HUD.exe
disproved that premise: Tween<T>() enqueues the new tween to its
private toAdd queue AND calls its own AddAndRemove() synchronously,
before returning - registering the tween in the by-target
ConcurrentDictionary in the SAME call, not deferred. So by the time this
handler runs (subscribed after Blish's own Scrollbar, confirmed by
construction - _contentPanel's Scrollbar is created via its CanScroll
property setter inside the same object-initializer statement that
constructs _contentPanel, strictly before this view's own
MouseWheelScrolled subscription line runs), Blish's wrong tween is
already registered, and TargetCancel finds and neutralizes it
synchronously (Tween.Cancel nulls its var/lerper slot, so even an
Update() before its removal from the list skips writing ScrollDistance
entirely) - not "canceled a frame late". The cancel-then-direct-write
shape is therefore kept as-is rather than replaced with a counter-tween
or a one-frame-deferred correction (both considered and rejected - see
CraftingPlanView.ApplyWheelWrapCorrection's own doc comment for the full
decompiled-evidence walkthrough). A bounded, one-shot defensive
re-assert (StartWheelWrapVerify: at most 2 real frames, re-asserts once
if ScrollDistance has drifted from the corrected target and no newer
wheel event has landed, [scrolldiag] writer=WheelWrapFix/reassert) was
added regardless, as insurance against a future Blish/Glide vendor
change - not because this mechanism is expected to fail.
Also fixed in the same pass: MouseWheelScrollLines == -1 (Windows' "one
screen at a time" setting) would otherwise flip this correction's sign;
WheelDeltaSanitizer.SanitizeScrollLines substitutes Windows' documented
default of 3 lines whenever the OS-reported value is not a usable
positive count (Blish's own HandleWheelScroll has the identical defect
under that setting and cannot be fixed here, so direction-correctness is
chosen over exact step-size parity for that one OS setting value).

VERIFICATION STATE: root cause confirmed against the live 2026-07-21
instrumented user trace and independently cross-verified by decompiling
the shipped BlishHUD v1.3.0 binary (Blish_HUD.Input.MouseEventArgs.
WheelDelta, Blish_HUD.Controls.Scrollbar.HandleWheelScroll/ScrollAnimated,
Blish_HUD.InputService's hook-manager selection, and - this fix-pass -
Glide.Tween.TweenerImpl.Tween<T>()/AddAndRemove() for the TargetCancel
timing question above). Fix confirmed by a green build and the full
Blish-free unit test suite (WheelDeltaSanitizerTests covers the entire
measured histogram, boundary values, the N=46/N=47 lattice edge, and
SanitizeScrollLines; ScrollMathTests covers the pixel-delta arithmetic,
including that a single multi-notch correction composes identically to N
single-notch steps). LIVE-VERIFIED 2026-07-21 (instrumented desktop
capture of THIS fix on the merged build): synthetic single-event
multi-notch up-deltas (+240 and +360, which Blish's WheelDelta getter
wraps to -65296/-65176 exactly as in the user's original trace) each
produced a [scrolldiag] writer=WheelWrapFix correction with the right
intendedDelta and moved the bar UP by exactly N clean notches
(0.1261->0.1031 for 2 notches, 0.1031->0.0688 for 3) - no down-step,
no snap-back on subsequent frames, and the defensive re-assert never
needed to fire (empirically confirming the synchronous-registration
TargetCancel mechanism the fix-pass decompilation predicted).

## 13. Resize UX rework: live reflow, no settle stutter (FIXED in M33, live-verified end-state; drag-tick perf live-verified 2026-07-21)
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
restored at settle. In particular, ReplayRelayout replays the full
relayout closure registry synchronously on every real drag frame
(previously: once, 150ms after the drag settled) - a genuine change in
perf character versus the pre-M33 debounce-only approach. The
SuspendLayout/ResumeLayout batching is a real, reasoned mitigation (see
ReplayRelayout's doc comment).
LIVE-VERIFIED 2026-07-21: the user confirmed the live drag feel is
smooth ("genuinely works well") across a drag session on a ~9,400px
multi-item plan (Exordium + Gift of Fortune) that replayed the relayout
registry 435 ticks over the course of the drag (M36 fix-pass, NICETOHAVE
a: this is the exact same drag-session capture #19's "435
writer=ResizePreserve writes" count below comes from - one drag, one
relayout-registry-replay-per-tick mechanism, counted from two angles),
with no felt lag or stutter on real hardware. This closes the drag-tick
perf caveat by direct user experience rather than by profiling - no
profiler numbers were captured, so a future report of stutter on a much
larger plan or a lower-end machine should still be taken seriously and
re-measured properly rather than dismissed against this note.

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
plus a green build and full test suite. LIVE-VERIFIED 2026-07-21 by the
user's own manual resize drags with ScrollDiagnosticsEnabled: 435
writer=ResizePreserve writes captured; on every height-changing tick the
vendor's own reset zeroed the bar (before=0.0000) and ResizePreserve
synchronously re-applied the constant absolute pixel offset
(savedOffset=1529 held across newHeight 678->794, with the ratio
adapting 0.1758->0.1782 as the viewport height changed) - drag-settle
verify exited reason=stable at realFrame=1.
(2026-07-20 desktop session note: six synthetic grip-drag attempts
across two fresh launches failed to re-catch the TabbedWindow2 resize
grip after this fix landed - the one successful synthetic catch of the
session predates the fix, which is why the live check above needed a
human drag rather than the screenshot-loop's synthetic input. The
primitives the fix reuses - pixel-offset capture, ScrollMath.
RatioForOffset, synchronous write, StartScrollVerify - are each
live-verified under item #14's captures.)

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
  FIXED in M37 - see item #25 below for the full mechanism and
  divergence record; the per-root "sell excess crafted components for
  profit" pill (gw2e's `showprofit` per-node display) remains out of
  scope, and the `MultiItemNote` text now describes the real batch
  rollup instead of the placeholder wording described above (NOT
  gw2e's own banner text verbatim - see #25's divergence record for
  why).

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
LIVE-VERIFIED 2026-07-21 (desktop screenshot loop on merged master,
Exordium + Gift of Fortune batch): multi-row input strip (add/remove
rows, dynamic top-strip reflow), batch generation, "Exordium and 1
other" title, merged Total Cost (Laurels 264 = both items' merged
demand), the sum-of-all-crafted-recipes note, both root trees stacked
above one merged 77/78-item Shopping List, and multi-source pill
highlighting all render correctly. The M34 additions were verified in
the same session: status line completes ("Plan generated - <time>",
never stuck), Exordium's Laurel total shows the merged-ceil 180 (the
old 186 overcount is gone on live data), and the IGNORE pill round-trips
correctly with proper tree-wide economics (ignoring Pile of Crystalline
Dust flipped every T6 promotion to craft and re-priced the batch;
un-ignoring restored the original plan exactly). Scroll restore stayed
zero-flash under the multi-root tree during ignore re-solves
([scrolldiag]: SyncRestore + single same-frame contest + stable at
realFrame 2). Still unverified live: USING N OWNED pill and
owned-currency annotations (need an account snapshot with relevant
stock - covered by unit tests), KNOWN-ISSUES #19's resize-preserve
(needs a human drag), drag-tick relayout perf on a fully-expanded tree.

## 22. Ignore-pill click sets status to "Best path restored" (cosmetic) (FIXED in M37)
Observed live 2026-07-21: clicking an IGNORE/IGNORED pill re-solves
correctly but writes the status line "Best path restored" - the label
belongs to the Best Path preset, not the ignore toggle. Pick a neutral
re-solve status ("Decisions updated" family) for ignore clicks.
FIXED in M37: root cause was CraftingPlanView.ApplyOverridesAndResolve
choosing the status text from `_nodeOverrides.Count == 0` as a proxy for
"the Best Path preset fired", instead of from which control actually
fired the re-solve. Every trigger that funnels through that one shared
method (Best Path, Craft All, Buy All, per-node craft/tp/vendor pill
cycling, and the ignore toggle) inherited the same ternary, so the
ignore toggle - which never touches `_nodeOverrides` at all - hit the
`== 0` branch and printed the Best Path preset's own label. Fixed by
threading an explicit `isBestPathPreset` flag from the one call site
that is actually the Best Path preset (bestPathButton.Click) through to
a new pure `StatusText.ForOverrideResolve(isBestPathPreset,
overrideCount)` helper (Services layer, Blish-free, unit-tested); every
other call site keeps its implicit `false` default and gets the neutral
"Decisions updated (N override(s))" text regardless of count. See #27
for the full trigger sweep this also covers (Craft All/Buy All could hit
the identical mislabel in the edge case where their preset legitimately
resolves to zero overrides).

## 23. Horizontal dividers appear/disappear with scroll position (FIXED in M36; see M36b follow-up below for 44px/32px rows)
User report: the same rows' divider lines are present at one scroll
offset and gone at another - not a contrast problem (that was #7, fixed
in M30), a presence/absence flicker as the list scrolls.
ROOT CAUSE: Blish applies the GW2 UI-size setting as a real GPU scale
matrix (GraphicsService.UIScaleTransform = Matrix.CreateScale(
UIScaleMultiplier)), not an integer-pixel-snapped one - confirmed by
decompiling the shipped BlishHUD v1.3.0 binary. The default "Normal" GW2
UI size scales by 0.897 (GetScaleRatio case 1), so a divider Panel
declared 1px tall in logical UI space rasterizes to 0.897 PHYSICAL
pixels. Guaranteed physical coverage is floor(0.897) = 0: depending on
where the divider's scaled top/bottom edges land relative to the
physical scanline grid, it can cover zero physical rows and vanish
entirely, or one and render fine - a function of the divider's absolute
screen Y (row position + scroll offset), which changes continuously as
the user scrolls. Scroll offset itself is an integer in logical space;
the non-integer scale is what turns a 1px integer height into
fractional physical coverage. #7's fix (higher-contrast divider color)
addressed a real but different symptom of this SAME underlying
mechanism - a low-alpha divider composited against a varying texture is
also inconsistent contrast, but #7 never widened the divider's height,
so the zero-coverage vanishing case it did not address remained latent
and is what this user report caught.
FIX (M36): every divider in CraftingPlanView widened from 1px to 2px -
CreateRowDivider (the shared helper behind Used Materials, Shopping
List, Crafting Steps, Required Disciplines, and Required Recipes row
dividers) and the per-section headerDivider under each collapsible
section's title row. floor(2 * 0.897) = floor(1.794) = 1 guarantees at
least one covered physical scanline at ANY scroll offset, eliminating
the zero-coverage case. Both are bottom-anchored inside their existing
bounds (row divider: Location.Y = rowHeight - 2; header divider:
Location.Y = 28 inside the 30px header panel, was 29) rather than
grown past them, so PlanContentHeightMath's row-count-based section
height arithmetic (verified against this change) needed no adjustment -
the divider has always lived inside rowHeight, not on top of it. Two
row types (Used Materials, Shopping List; both rowHeight 36 with a 34px
icon frame starting at y=1) had only 1px of clearance below their icon,
exactly enough for the old 1px divider but not the new 2px one; their
icon's y was nudged from 1 to 0 so icon height (34) + divider height (2)
exactly fill rowHeight with no overlap. NOT touched: the two existing
2px separators (title-row and section-header-row separators, already
wide enough), the transparent multi-root rootDivider (M35, a 12px gap
panel, not a hairline), and the Recipe Tree (uses indent guidelines
instead of row dividers by design - no change applicable).

CORRECTION (M36 fix-pass): the verification note below originally
claimed "the rest had several pixels of existing headroom" for every row
type not listed above. That was wrong for the Required Recipes section's
no-sublabel row: RecipeRowHeightNoSublabel (32) plus its 34px icon frame
at y=1 already overflowed the row by 3px BEFORE this divider change
(icon bottom = 35 against a 32px row), and the divider width increase
made the shortfall 1px worse (34 + 2 = 36 needed, only 32 available) - a
real, pre-existing overflow this fix's own verification pass missed, not
a case with headroom to spare. Fixed as its own item this same
fix-pass: RecipeRowHeightNoSublabel raised to 36 (icon y nudged to 0,
exact fit, mirroring the Used Materials/Shopping List pattern above) -
see PlanContentHeightMath.RecipeRowHeightNoSublabel and
CraftingPlanView.CreateRecipeRow.

VERIFICATION STATE: code-verified - the 0.897 scale factor and the GPU
matrix mechanism were confirmed against a decompile of the shipped
BlishHUD v1.3.0 binary (GraphicsService.GetScaleRatio /
UIScaleTransform); the row/header geometry changes were verified by
reading every affected call site to confirm rowHeight/headerPanel
containment and check for icon/label crowding (the Required Recipes
no-sublabel row needed a coherent adjustment per the correction above,
in addition to the two rows originally noted; every other row type has
several pixels of genuine existing headroom, re-checked this pass). The
header divider's own clearance (Location.Y = 28 inside the 30px
headerPanel, under a DefaultFont18 section title at y=4) was reasoned
from bounding-box containment only - actual glyph ascent/descent metrics
for DefaultFont18 were not measured, so whether 28 (down from 29) reads
as visually tighter under real glyphs is unverified; treat a future
title/divider crowding report as expected-until-checked rather than a
surprise regression. Build and full test suite green. LIVE-VERIFIED
2026-07-21 (desktop screenshot loop on the merged build): the Shopping
List captured at three successive one-notch scroll offsets shows a
visible divider between EVERY adjacent row pair at every offset - the
same rows that previously lost their divider at unlucky positions keep
it across all sampled offsets. Header divider and section-title glyph
clearance looked normal in the same captures (no crowding observed),
though no glyph-metric measurement was taken.
**CORRECTION (M36b, see follow-up below):** the "LIVE-VERIFIED" claim
above is downgraded from unqualified to **36px sections verified;
44px/32px rows were still vulnerable** - the capture above only ever
sampled the Shopping List, whose rowHeight (36) is mathematically
immune to the defect regardless of this fix. It never exercised the
Required Recipes/Disciplines/Crafting Steps row heights (44px/32px),
which a follow-up investigation proved were still broken. Recorded
here as an honest correction rather than silently edited away.

## Follow-up to #23 (M36b): 44px/32px rows still vulnerable after M36
User re-reported divider misses in Required Recipes on the merged M36
build. Live pixel-scans (idle desktop, two successive scroll captures
one wheel notch apart) proved the misses are position-dependent
single-boundary vanishes that move with scroll phase, not a fixed
row/content correlation: one capture showed exactly one missing
divider boundary; the next capture (content shifted ~1 logical pixel)
showed that boundary restored but two DIFFERENT boundaries missing.
Measured environment UI scale for this test setup was 0.81 (Small),
correcting the earlier 0.897 (Normal) assumption used throughout #23's
original analysis and fix.
ROOT CAUSE: a second, independent scissor round trip that #23's M36
analysis did not model. `rowPanel` is itself a Blish `Container`, and
every `Container.Paint()` unscales the physical scissor it was just
given back to logical space before re-intersecting and re-scaling it
for its own children (`Container.cs:377-381`, `Control.cs:1176-1177`
in the decompiled Blish HUD v1.3.0 binary) - a SECOND floor/ceil step
on top of the one #23 modeled for the divider's own quad-vs-scissor
math. This second round trip can shrink the clip rectangle reaching
the divider by exactly 1 logical pixel, always at the row's bottom
edge, which for `rowHeight` = 44 (Crafting Steps, Recipe rows with a
sublabel) and 32 (Required Disciplines) reliably deletes the divider
entirely (0 physical scanlines) at ~10.2% of scroll phases at the
default scale - matching the "one boundary here, different boundaries
there, one notch later" live evidence exactly. `rowHeight` = 36 (Used
Materials, Shopping List, Recipe rows with no sublabel) is immune by
construction at every GW2 UI Size scale tested, which is why M36's own
Shopping List verification pass came back clean - it happened to
sample the one row height that was never broken.
FIX (M36b): `CreateRowDivider` gained a `bottomClearance` parameter -
1 extra logical pixel of gap between the divider and `rowHeight` for
the vulnerable 44px/32px row types (`CreateCraftStepRow`,
`CreateRecipeRow`'s sublabel branch, `CreateDisciplineRow`), 0
(unchanged) for the immune 36px row types. Proven immune by
simulation across every `rowHeight` value in the file and all four GW2
UI Size scale factors (0.81/0.897/1.0/1.103), not just the default -
see `CreateRowDivider`'s doc comment in Views/CraftingPlanView.cs for
the full derivation. Required Disciplines was not part of the user's
report but was confirmed mathematically identical exposure (~10.2%
vanish rate) and free of any icon-clearance side effect (that row has
no icon, just two `DefaultFont14` labels well clear of the new divider
position) - fixed proactively rather than left latent.
The section headerDivider (H=30, built inline in CreateSectionHeader,
not via CreateRowDivider) shares the same Container.Paint exposure -
simulation shows it is immune at the default 0.897 scale but vulnerable
(~16-17%) at the "Small" 0.81 scale, which is the scale actually
measured in this session's live pixel-scans - so it received the same
1px bottom clearance (y 28 -> 27) in the same wave.
VERIFICATION STATE: LIVE-VERIFIED 2026-07-21 (post-fix desktop
pixel-scans on the m36b build, automated scanner measuring divider
scanline positions against expected row boundaries): Required Recipes
scanned at FIVE distinct scroll offsets - including the exact viewport
from the user's bug report (the Gift of Dust/Claws/Scales/Bones/Fangs
block) - with uniform 35-36px physical pitches and ZERO missing
boundaries (pre-fix scans of the same section showed 1-2 missing
boundaries per viewport that moved with scroll phase); Crafting Steps
scanned clean at two offsets; Shopping List (immune 36px class)
previously verified at three offsets. Required Disciplines (32px,
3 text-only rows, same shared-helper fix, simulation-proven) was not
individually pixel-scanned.

## Carried follow-up resolved: caret glyphs (settled 2026-07-21)
ASCII carets ("v" / ">" section headers) rendered reliably in every
capture across three desktop sessions and two machines' font stacks
this milestone cycle; the unicode-triangle alternative is unnecessary.
Settled: keep ASCII carets.

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

---

# M37 Backlog: Post-Parity Wave (logged 2026-07-21, user directives after M33-M36)

User directives from the 2026-07-21 session, issued after full Exordium
behavioral parity (M33-M36, master 812d0f0, 812 tests, PRs #44-#51).
Localization is explicitly DEFERRED to the long-term backlog (user: "not
core functionality"); upstream Blish HUD issue posts are explicitly
SKIPPED (the v1.3.0 wheel-delta bug is already fixed on their unreleased
dev branch; our module-side sanitizer stays until a fixed release ships,
then can be retired at leisure).

## THE METHOD (still governs items 24-26)
Research how gw2efficiency handles each behavior FIRST (dev-time only -
the module never calls gw2e or the wiki at runtime), document it, then
echo it. NOTE FOR THE IMPLEMENTING SESSION: the prior session's research
reports lived in a transient scratchpad and are GONE. Committed
references that survive: docs/gw2e-parity-spec.md (normative algorithm
spec), the resolution records in items 12-23 above, and project memory.
gw2e sources remain publicly fetchable (recipe-calculation/recipe-nesting
on GitHub; the live app bundle at gw2efficiency.com; the custom-recipes
repo is GONE from GitHub - recover via Wayback Machine if needed).

## 24. Homestead refinement handling (parity gap)
gw2e's solver models Janthir Wilds homestead refinements - its
cheapestTree has homestead-refinement merchant-name matching (observed
in the M34 research of cheapestTree.ts). We model nothing. Refinement
tiers are NOT in the official API, so a manual user setting is required
(old critique item, pre-M18). Research first: exact gw2e mechanism
(how refinement conversions are modeled as merchant recipes, the user
toggle and its default, daily caps, which material families) plus wiki
ground truth for the conversions (rates, daily limits, unlock state).
Then: wiki-verified static seed for the conversions, a Settings toggle,
solver participation mirroring gw2e, display. No invented data.

## 25. Multi-item sell-side economics (parity gap, deliberate M35 gap) (FIXED in M37)
M35 left SellableQuantity/NetSaleValue/CraftingProfit unset for batches
(documented in GenerateStructuredMultiAsync). gw2e's multi mode shows a
Cost Breakdown that sums Cost/Savings/Profit across items, a "Profit
numbers are the sum of all crafted recipes" banner, and a
sell-excess-crafted-components-for-profit rollup exposed ONLY when
multipleRecipeTree is true (observed in the M34 research of the app
bundle). Research the exact semantics from the live bundle (what counts
as excess, the 0.85 fee basis, tradability gating per item, display
layout), then implement for MultiItemRoots batches. Single-item
economics (M20) must be byte-identical after.

FIXED in M37 (research: docs/research/m37-r2-batch-economics.md,
re-fetched live from the app bundle 2026-07-21 since the earlier M34
report referenced above was lost). Mechanism:
- Extracted the M20 single-item per-item arithmetic (over-production
  bump, sell-price lookup, own-materials opportunity cost) out of
  `CraftingPlanPipeline.ApplySellSideEconomics` into two pure helpers -
  `ComputePerItemEconomics` (one requested root's own
  SellableQuantity/NetSaleValue/TargetUnitSellPrice/ItemCraftCost/IsCraft)
  and `ComputeMaterialOpportunityCost` (the batch-merged UsedMaterials
  sum). `ApplySellSideEconomics` itself is a pure extraction - same
  fields, same order, same arithmetic - proved byte-identical by the
  full pre-existing single-item economics test suite
  (`CraftingPlanPipelineTests`' `Structured_*`/`ResolveWithOverrides_*`
  hand-computed-value tests) passing unmodified, plus a new
  `MultiItemPlanTests.GenerateStructuredAsync_SingleEntryList_MatchesLegacySingleItemCall`
  assertion comparing every economics field between the direct
  single-item call and the list-of-one entry point.
- New `CraftingPlanPipeline.ApplyBatchSellSideEconomics`: calls
  `ComputePerItemEconomics` once per requested root (paired by index
  with the wrapper recipe's own `Ingredients`, both built in request
  order by `RecipeService.BuildMultiItemTreeAsync`), then sums the
  qualifying roots' SellableQuantity/NetSaleValue into the batch
  totals and `NetSaleValue - ItemCraftCost` (each root's own
  post-correction `SolverDecision.TotalCost` - see the isolated-root-
  cost note below) into CraftingProfit, minus the batch's single
  MaterialOpportunityCost. Wired into both
  `GenerateStructuredMultiAsync` (fresh generation) and
  `ResolveWithOverrides`'s wrapper-context branch (so an override/
  Ignore re-solve of a batch keeps the rollup live, mirroring how every
  other part of a re-solve already behaves).
- Bonus fix found in review: `ApplyBatchSellSideEconomics` also now sets
  `result.PriceBasis` unconditionally (mirroring the single-item
  method), which fixes a latent M35 gap - NOTHING ever set
  `CraftingPlanResult.PriceBasis` for a multi-item batch before this
  change (`PlanResultBuilder.Build` never touches it, and
  `GenerateStructuredMultiAsync` never called `ApplySellSideEconomics`,
  the only other place that did), so it silently stayed at the enum
  default (`PriceBasis.InstantBuy`) regardless of the actual basis used
  to solve the plan - a batch generated with the module's own default
  (`BuyOrder`) never showed the "Total (buy-order prices)" label
  suffix. Regression-tested:
  `GenerateStructuredAsync_MultiItem_PriceBasisIsSetEvenWithNoQualifyingRoots`.
- MaterialOpportunityCost is unaffected by the per-root filter below -
  it stays a single sum over the batch's already-merged UsedMaterials
  list (unchanged from M35), set whenever Valued mode produced any
  usedMaterials at all, even if zero roots qualify for the sell/profit
  rollup.

Divergences from gw2e (recorded M34-style, per this file's convention):
- **DIVERGED** (review fix - see "Review fix" note below): gw2e's
  rollup sums only roots whose committed decision is `craft === true`,
  filtering out any requested item the solver decided to buy. This
  module's requested roots CAN resolve to a buy decision
  (`PlanSolver.Evaluate` has no root-only special case - proven live by
  `GenerateStructuredAsync_MultiItem_PerRootDecision_MatchesStandaloneSingleItemSolve`,
  M35), but the batch rollup deliberately does NOT add gw2e's
  craft-only filter, per the research report's own explicit
  recommendation (Section 4.1.1): a bought-but-tradable root still
  contributes its own SellableQuantity/NetSaleValue/CraftingProfit,
  matching this module's own already-shipped single-item
  `ApplySellSideEconomics` semantics (which has never filtered by
  craft-vs-buy - a flip/arbitrage number is still meaningful) and what
  a user would see running each item through the module one at a time
  and adding the numbers up by hand - see
  `GenerateStructuredAsync_MultiItem_OneRootBoughtButTradable_IncludedInSum`.
- **DIVERGED**: gw2e's rollup still includes an untradable CRAFTED
  root as a hidden `-cost` drag (Section 1.4 of the research report -
  the per-node "Crafting Profit" pill would never show this item at
  all, since it is gated on `tradable`, but the top rollup silently
  absorbs its full craft cost as a loss anyway - an upstream quirk,
  not a design). This module excludes such a root entirely instead
  (contributes 0, not a penalty) - both its revenue AND its own craft
  cost drop out together, matching this module's own single-item
  `NetSaleValue` convention (null/absent rather than a hidden
  negative) - see
  `GenerateStructuredAsync_MultiItem_OneRootUntradable_ExcludedFromSumNotNegative`.
- **DIVERGED**: single profit basis (SellInstant/buy-order, the M20
  module convention) instead of gw2e's `profit_buy`/`profit_sell` dual
  buy-order/sell-listing variants - this module has never shown a
  second sell-listing figure, and the batch rollup stays consistent
  with the single-item row rather than doubling the Total Cost
  section's row count.
- gw2e's per-node "Crafting Profit" pill (shown only on the N
  top-level item roots of a batch, gated on `showprofit`/`craft`/
  `tradable` together - research report Section 1.3a) is explicitly
  OUT OF SCOPE for this fix, as directed - only the aggregate rollup
  rows are added. A future milestone could add per-root pills.
- gw2e's unrelated `excessiveComponents`/`step.excessAmount` feature
  ("sell excess crafted components for profit" - the bulk-crafting-
  granularity warning, not `craftedComponentsBreakdown`) remains
  unimplemented; the research report explicitly found no code path
  connecting the two upstream features despite similar-sounding names.

Documented nuance (review fix): `MaterialOpportunityCost` is a single
sum over the batch's whole merged `UsedMaterials` list, computed
independently of the per-root filter above - inventory reduction walks
the ENTIRE unreduced wrapper tree before `PlanSolver` ever decides Buy
vs Craft per root, so a root that ends up bought can still have owned
ingredient stock recorded as "used" against its own never-crafted
subtree, and that forgone value is deducted from the batch's
`CraftingProfit` regardless of whether that root's own economics are
otherwise included in the sum. This matches the single-item path's own
pre-existing behavior exactly (`ApplySellSideEconomics`' own
`MaterialOpportunityCost` is likewise never gated on the target's own
craft/buy decision) - see
`GenerateStructuredAsync_MultiItem_ValuedMode_MixedBuyCraftBatch_MaterialOpportunityCostIsWholeTreeSum`.

Isolated per-root craft cost: `Services/PlanSolver.cs`'s
`SolverDecision.TotalCost`, read via
`solveResult.Decisions[itemRoot.NodeId].TotalCost` - the same
post-correction (after `AllocateVendorNodeCosts`/`RecomputeCraftCosts`)
real-coin figure `CraftingTreeBuilder` already copies onto
`CraftingTreeNode.SubtreeCost` for that node's own pill display. Using
it (rather than `Plan.TotalCoinCost`, the whole batch's cost) is what
lets a shared-material batch attribute cost proportionally to each
root instead of double-counting or dropping the shared portion -
regression-tested (review fix) by
`GenerateStructuredAsync_TwoItems_SharedBulkVendorMaterial_BothTradable_CraftingProfitUsesRealNonDuplicatedSharedCost`,
which exercises this attribution across two roots that BOTH qualify
for the sell/profit rollup and share a merged vendor-bulk purchase (the
earlier `..._SingleCeilAcrossBoth` test above has no TP price on either
finished item, so it never reached this summing code at all).

Display: the Total Cost/Cost Breakdown section's existing `CoinTotal`
row machinery (`CreateCostTileRow`) already handles an arbitrary
simultaneous tile count generically (M33), so no View changes were
needed - only `PlanViewModelBuilder.BuildSummarySection`'s row
wording, gated on `isMultiItem`: "Sell value (batch total, after 15%
TP fees)" and "Profit if sold"/"Loss if sold" with a "(batch total)"
qualifier (concatenated before any existing "(coin costs only)"
qualifier), dropping the single-item "Nx overproduction" quantity
qualifier (no single requested quantity to compare a batch sum
against). The `MultiItemNote` row is gated on the SAME
`result.NetSaleValue.HasValue` condition as the Sell value/Profit rows
above it (mirroring gw2e's own shared `ng-show` condition, research
report Section 1.3b) - review fix: the first version of this milestone
gated the note on `isMultiItem` alone, so it could render next to zero
profit numbers whenever a batch had no qualifying root. Its wording is
"Sell value and profit are the sum across every requested item that
has a live Trading Post sell price." - NOT gw2e's own verbatim banner
text ("...sum of all crafted recipes"), because (review fix) the batch
rollup has no craft-vs-buy filter at all (divergence item 1, above), so
"crafted recipes" would be inaccurate. Single-item mode's summary
rows/labels are unchanged (verified by the full pre-existing
`PlanViewModelBuilderTests` single-item economics suite passing
unmodified).

Review fix (post-merge adversarial review): the version of this
milestone first merged had three defects since corrected: (1) the
craft-only filter above was a real, unauthorized divergence from the
research report's explicit 4.1.1 recommendation, mislabeled as an
"echo"; (2) the `MultiItemNote` row's gating and wording (previous
paragraph); (3) two of the milestone's own regression tests
(`GenerateStructuredAsync_MultiItem_PriceBasisIsSetEvenWithNoQualifyingRoots`,
which reused a fixture where both roots actually qualified, and the
tests renamed above) did not exercise the branches their names/doc
comments claimed to cover. All three are fixed as described in this
section; the divergences list and test names above reflect the
corrected, current state.

VERIFICATION STATE: build green, full test suite green (830 tests -
Blish-free, real production-path tests throughout:
`MultiItemPlanTests` for the pipeline aggregation/re-solve-recompute
behavior including the no-craft-filter and untradable-exclusion
divergences plus the shared-vendor-cost and Valued-mode-mixed-batch
interactions, `PlanViewModelBuilderTests` for the batch row
wording/note text and its gating, `PlanContentHeightMathTests` for the
first-time-at-4-tiles multi-item height case). Not yet verified by a
live in-game desktop check - a fresh batch generation with a mix of
crafted/bought and tradable/untradable requested items should be
screenshot-loop verified before this is treated as visually confirmed,
matching this file's existing convention for other M35/M37 UI changes.

## 26. Achievement-bit ingredient dedup (parity micro-gap)
gw2e ships ~274 achievement-discipline custom recipes (achievement_id,
ingredients mirroring collection requirements) and de-duplicates
achievement-bit ingredients across the tree (flagged in M34 research as
a known absent behavior in our module; zero Exordium impact - pick a
real affected item for verification, e.g. a legendary with an
achievement-gated collection component). Research exact dedup semantics
from gw2e sources first; echo. Small.

## 27. Ignore-pill click status label (FIXED in M37, closes #22)
Item #22 above: clicking IGNORE/IGNORED re-solves correctly but writes
"Best path restored" - a preset label, not an ignore label. Use the
neutral "Decisions updated" status family. Trivial; close #22 when done.
FIXED in M37: see #22's resolution note for the mechanism. Full sweep of
every user-triggered re-solve entry point that shares
ApplyOverridesAndResolve, and the status text each now writes:
  - Best Path preset (bestPathButton.Click) -> "Best path restored"
    (the only trigger that legitimately gets this label; passes
    isBestPathPreset: true explicitly).
  - Craft All preset (craftAllButton.Click -> ApplyPreset) ->
    "Decisions updated (N override(s))".
  - Buy All preset (buyAllButton.Click -> ApplyPreset) ->
    "Decisions updated (N override(s))".
  - Per-node craft/tp/vendor pill cycling (the `interactive` pill
    branch) -> "Decisions updated (N override(s))" (unchanged; this
    site was never mislabeled since a pill click always adds at least
    one override).
  - Ignore/un-ignore toggle (the `ignoreInteractive` pill branch) ->
    "Decisions updated (N override(s))" (the reported defect; N reflects
    unrelated per-node overrides, not the ignore set, since ignore state
    lives in a separate `_ignoredItemIds` set).
Same-class sibling bug also fixed by this change: Craft All/Buy All
could hit the identical "Best path restored" mislabel in the edge case
where CraftingPlanPipeline.BuildPresetOverrides legitimately returns an
empty override map (e.g. a tree with no craftable nodes for Craft All,
or none priced on the TP for Buy All) - previously indistinguishable
from the Best Path preset's own zero-overrides state. Now decoupled
entirely: only an explicit isBestPathPreset: true reaches the Best Path
text, regardless of resulting count.
Not a Blish-free seam originally (the ternary lived inline in the
CraftingPlanView method), but the module already has an established
Services-layer pure-helper pattern for status strings (StatusText.
Normalize, used by MainView); StatusText.ForOverrideResolve extends
that existing seam rather than inventing a new one, and is covered by
StatusTextTests (Blish-free, per repo invariants).

## 28. Vendor cap data seeding + stale-offer sweep (PARTIAL - core FIXED in M37; gaps deferred, see DEFERRED below)
M34 shipped gw2e-parity warn-only cap machinery (TimegatedItems +
Crafting Steps notice) but 0 of ~53,530 seeded offers carry
DailyCap/WeeklyCap values, so it is inert. Extend tools/VendorOfferUpdater
to fetch purchase-cap data from the wiki (research whether SMW exposes
caps as queryable properties; else targeted parsing), seed caps
(mind VendorOfferHasher: content-derived offerIds change when the offer
payload changes - check hasher scope + tests), and run a stale-offer
detection pass (wiki-verify current availability; precedent: the Gift
of Battle offer removed in M33 Wave B was wiki-confirmed discontinued
in 2016). Scope guard: prioritize offers actually reachable from seeded
recipe trees before attempting all 53k. Known concrete case to verify:
the "Candy Corn Vendor (Weekly)" Ecto offers carry no caps despite the
name (M34 research).

FIXED in M37 (cap seeding): WikiSmwClient's PrintoutSuffix extended with
two new SMW printouts (Has daily purchase cap, Has weekly purchase cap);
ConvertToOffer now threads the parsed values into both VendorOfferHasher
and the emitted VendorOffer instead of a hardcoded null,null. A full
fresh two-pass re-scrape (63,055-row ref/wiki_vendor_cache.json, 819 rows
carry a real cap) seeded 689 of the 53,530 baseline offers with a real
DailyCap/WeeklyCap (0 lost a cap, 0 changed between two different real
values). VendorOfferHasher.OfferId churn was verified confined exactly to
those 689 offers in both directions (no offer changed its id without a
cap change, and vice versa), so the merge-by-OfferId contract in
VendorOfferStore is unaffected. Verified concrete case: the three "Candy
Corn Vendor (Weekly)" Glob of Ectoplasm offers (cost 1 Gibbering Skull /
1 Tyria's Best Nougat Center / 1 High-Quality Plastic Fangs) now carry
WeeklyCap=1, cross-checked three independent ways (SMW ask query, raw
wikitext `{{vendor table row|...|per week=1}}`, and the rendered page's
"Limit" column) - see docs/research/m37-r4-vendor-caps.md Section 2b for
the full triangulation. Has character purchase cap, Has total purchase
cap, and Has seasonal purchase cap are real, populated SMW properties
(confirmed) but were deliberately NOT seeded this milestone - the module
has no model field or consuming logic for them (TimegatedCapType is
Daily/Weekly only) and no account/character concept at all, so seeded
values would have nowhere to go; left for a future milestone's own
design pass.

FIXED in M37 (stale-offer sweep, reachable-only): computed the reachable
offer set as every vendor offer whose output item appears as an
ingredient of some ref/recipes_seed.json recipe (5,487 of the 53,530
baseline offers, ~10.2%, matching M34's projection) and swept only that
set, per this item's scope guard. An initial automated pass (identity
diff of the reachable set against stage 2's fresh 63,055-row wiki cache)
produced 394 raw candidates, but investigation showed that signal alone
is unreliable: ~278 were SMW GameId-resolution misses (the item is still
listed, e.g. Tycho's Leather Bag/Crude Salvage Kit, but that pass's fetch
failed to resolve a game id for the row) and most of the remainder were
large-vendor-page row-capture gaps in the same fresh scrape (Tycho, Brass
Nettlemoor, Chef Kaga, and Traveling Elonian Trader were all directly
re-verified live and are still selling the "missing" items at the same
cost). After filtering those out and re-checking every remaining
candidate against a fresh, independent full per-merchant SMW query, only
23 of 394 stayed unmatched. Two-route wiki verification (a live SMW ask
re-query plus the vendor's dedicated /Historical wiki subpage, which
records the exact game-update patch note for each removed row) confirmed
exactly 2 offers as genuinely discontinued and removed them:
  - Memory of Battle (outputItemId 71581) from Dugan, cost 75 Badge of
    Honor + 100 WvW Skirmish Claim Ticket + 1 Emblem of the Avenger -
    removed per the April 16, 2024 game update (Dugan/Historical), and
    independently confirmed as the only [[Sells item::Memory of
    Battle]] SMW match anywhere on the current wiki (i.e. the wiki
    itself has no live replacement source). Safe to remove: Memory of
    Battle is Trading-Post-tradable (checked via /v2/commerce/prices),
    so no recipe needing it regresses to a false Unknown node (the
    item 17 precedent's exact regression check).
  - Shield Generator Blueprint (outputItemId 76483) from Dugan, cost 1
    Emblem of the Avenger + 10 Shield Generator Blueprint + 10 WvW
    Skirmish Claim Ticket - removed per the November 19, 2024 game
    update (Dugan/Historical). Safe to remove: the item retains 18
    other live vendor offers in the seed.
ref/vendor_offers.json now carries 53,528 offers (5,485 reachable,
~10.2% - down from 5,487 pre-sweep since both removed offers were
themselves reachable). The remaining 21 of the 23 unmatched candidates
were investigated and deferred (documented here rather than removed,
per this item's two-independent-checks bar):
  - 18 offers naming "Skirmish Supervisor", "Lionguard (Skirmish
    Merchant)", or "Mercenary (Skirmish Merchant)" as the merchant
    (Mist Pendant, Mist Band (Infused), Obsidian Shard, Ascended
    Salvage Kit, Mists-Charged Jade Band (Infused), Pile of Soybeans):
    all three wiki pages were restructured into /Armor, /Weapons,
    /Others subpages; the items are still sold in-game under the split
    pages (confirmed live). This is a missing-offer/rename gap for a
    future re-scrape to follow, not a stale offer - NOT removed.
  - 2 Brass Nettlemoor offers (Healing Signet, Plague Signet): the
    wiki's own item pages gained a "(ring)" disambiguation suffix
    (avoiding a name clash with same-named skills); the items, ids, and
    costs are unchanged and still correctly seeded - NOT removed, no
    action needed.
  - 1 "Merchant (Untamed Crags)" offer (Hydrocatalytic Reagent, 50
    Research Note): that exact vendor page no longer resolves on the
    wiki (no page, no redirect), while the underlying item+cost is
    still valid via dozens of other crafting-material vendors - deferred
    pending further research into whether the page was renamed or the
    original scrape mislabeled the vendor.
A --detect-stale updater mode (per the design sketched in
docs/research/m37-r4-vendor-caps.md Section 4e) was evaluated and
deliberately skipped: the investigation above showed a naive automated
diff is dominated by false positives (394 raw candidates, 2 survived
two-route verification) driven by scraper coverage gaps and wiki page
renames that need human judgment to tell apart from genuine removals.
Shipping that as unattended tooling now would risk encouraging
unverified mass removals later; the manual method is documented in the
research report instead. Also out of scope for this pass and left
uncommitted/discarded: the incidental ~5,400-offer wiki-drift superset
(new Homestead recipes, unrelated vendor page changes) that a full
from-scratch re-scrape also picked up alongside the cap data - adopting
it wholesale was out of this item's stale-offer-sweep scope and is
recorded as a candidate for a future "missing offers" pass instead.

## 29. Owned-materials UI live verification (verification debt)
USING-N-OWNED pills, owned-currency annotations, and the owned/needed
shopping splits (M34 B2b) are unit-tested but have never been SEEN
rendering. Method (no real API key needed): write a synthetic
AccountSnapshot into the ISOLATED preflight settings data dir
(C:\Dev\Blish\blish-preflight-settings\data - real SnapshotStore
format; never touch the user's real Blish config) with Exordium-relevant
stock (e.g. Mystic Coins, Elder Wood Logs, T6 mats, wallet Spirit
Shards), enable Use Own Materials, generate Exordium, verify via the
screenshot loop: partial-owned pills, full-owned HAVE, currency
owned/needed annotations, and that totals shrink accordingly.

## 30. Required Disciplines divider pixel-scan (verification debt)
The M36b clearance fix covered 32px discipline rows via the shared
helper (simulation-proven) but that section was never individually
pixel-scanned - it is short and sits somewhere above Required Recipes
(the M36b session never located it on screen). Locate it, scan at 2+
scroll offsets with the committed scanner
(C:\Dev\Blish\preflight\scan_dividers.py - methodology and verdict
reading documented in the script header; environment UI scale is 0.81
so 32px rows pitch at ~25.9px).

## 31. Concurrency and degradation audits (verification debt)
Three never-formally-swept reviews, each producing classified findings
(fix Critical/MustFix per the repo review loop):
(a) Cross-thread await audit: every await continuation that touches
Blish controls must marshal via the M31 primitives
(MainThreadMarshal/generation guards). New async paths were added in
M33-M36 (currency icon arrival, wheel correction, resize preserve,
multi-item generate) - audit ALL await points in Views/ + Module.cs +
pipeline callbacks that mutate controls.
(b) Offline/API-down degradation: behavior when each /v2 endpoint
(prices, items, currencies, recipes, account) fails or times out -
status surfacing, partial renders, retry paths, no crashes/hangs.
(c) Price-cache thread-safety: the M26 TTL cache + locks under
concurrent generate/re-solve/refresh.

## DEFERRED (recorded, not M37 scope)
- Localization (en/de/fr/es via API lang param): user-deferred backlog,
  "not core functionality". Full-milestone scale when picked up.
- Upstream Blish HUD issue/PR for the wheel-delta wrap: skipped by user
  decision; fix already exists on Blish dev; our sanitizer is
  forward-compatible and retirable after their next release.
- Ignore-pill cascade semantics + own-materials gating divergences
  (#20.4): revisit only on user feedback.
- Multi-item row reordering (gw2e moveRecipe): out of scope per M35.
- Skirmish Merchant-family wiki page split (#28, 18 offers): Skirmish
  Supervisor / Lionguard (Skirmish Merchant) / Mercenary (Skirmish
  Merchant) wiki pages were restructured into /Armor, /Weapons, /Others
  subpages; the items are still sold in-game under the split pages, but
  the seed's merchant-page linkage is now stale-shaped. Missing-offer/
  rename gap for a future re-scrape to follow up; not removed.
- "Merchant (Untamed Crags)" vendor-page-name mismatch (#28, 1 offer):
  the Hydrocatalytic Reagent / 50 Research Note offer's exact vendor
  page no longer resolves on the wiki (no page, no redirect), while the
  item and cost remain valid via other crafting-material vendors.
  Deferred pending research into whether the page was renamed or the
  original scrape mislabeled the vendor.
- Wiki-drift missing-offers superset (#28, ~5,400 offers): M37's full
  from-scratch re-scrape (for cap seeding) incidentally picked up new
  Homestead recipes and unrelated vendor page changes beyond the
  stale-offer-sweep scope. Discarded uncommitted; recorded here as a
  candidate for a future dedicated "missing offers" pass.
- Character/total/seasonal purchase caps (#28): the wiki's "Has
  character purchase cap", "Has total purchase cap", and "Has seasonal
  purchase cap" SMW properties are real and populated (confirmed in
  M37) but were deliberately not seeded - the module has no model field
  or consuming logic for them (TimegatedCapType is Daily/Weekly only)
  and no account/character concept at all. Left for a future
  milestone's own design pass.

## Handoff notes for the implementing session
- Project memory holds everything: parity goal + full M33-M36 record
  (root causes, primitives: FrameTicker, MainThreadMarshal,
  PlanContentHeightMath, PlanRelayoutMath, DecisionPillPlanner,
  WheelDeltaSanitizer), the Blish-over-Paint automation protocol
  (input routing: Paint focused for wheel/drag, Blish for typing;
  corner icon +320,0 offset with window at 8,120; idle gate >=120s +
  activate-verify; launch from a COPY of the .bhm so builds stay
  unlocked; settings-injection for module settings; input-death
  diagnosis via silent [scrolldiag]; window position PERSISTS across
  sessions - re-derive click coords from fresh captures every time;
  environment UI scale 0.81), and orchestrate-dont-implement (sonnet
  subagents; opus only for hardest verify; desktop automation is
  main-thread-only - subagents decline it).
- Helper scripts: C:\Dev\Blish\preflight\ (wins/shot/click/activate/fg/
  type/scroll/drag/wheelburst/repro_b/scan_dividers.py/start_paint/
  start_blish - note start_blish.ps1 hardcodes the repo bin .bhm path;
  use an inline Start-Process with the copy path instead).
- Every runtime change: adversarial review gate, then PR -> CI ->
  self-merge (git and gh allowlisted; git push prompts once per branch
  due to a global ask rule - expected).
- Tests: 812 green on master (812d0f0). Build/test commands in CLAUDE.md.
- ScrollDiagnosticsEnabled=true persists in the preflight settings.json.
