## Audit batch E: tree interaction honesty (audit-de-cost-tree)

Four maintainer-approved UX-audit findings, all one story: a pill's
appearance should tell you whether you can click it, and the tree's
actions should be reachable when you need them.

- **H3, dimmed pills lied.** A reference branch under a bought item ("what
  it would cost to craft instead") drew a full, full-strength-looking pill
  set that no click handler was ever wired to, and nothing explained the
  silence. Three parts: (1) any pill whose click is dead only because the
  row is dimmed now carries "Under a bought item - switch the parent to
  CRAFT to change this", appended rather than assigned over, so a dimmed
  Subdued pill keeps its "why it loses" text and a dimmed committed pill
  keeps its value-detail hover; (2) the predicate for "is this a click
  target" moved to `DecisionPillPlanner.IsInteractive`, because the view
  read it twice - once to wire handlers, once to decide who needs
  explaining - and those two readings drifting apart is how the dimmed set
  ended up silent; (3) a dimmed row's pills dim to 0.6 rather than the
  0.35 its name/quantity/cost still use (at 0.35 every hue crushed to the
  same near-black ring), and the "this whole branch is inactive" job moved
  to a 2px rule down the dimmed row's own indent channel, which joins
  across consecutive rows into one continuous line per depth. The rule
  sits inside the existing `TreeRowHeight`; no height math changed.

- **M3, the toolbar scrolled away.** Best Path / Craft All / Buy All /
  Expand All / Collapse All lived in the Recipe Tree's section header,
  inside the scrolling content area, so a long plan scrolled Collapse All
  off screen at exactly the point it became worth pressing. They now sit
  in a dedicated row of the non-scrolling top strip, right-anchored, under
  a "Recipe Tree:" label naming what they act on; the section title stays
  in the scroll flow with the tree. The three plan-mutating presets are
  separated from the two view-only actions by a wider gap. The state they
  mutate stays with `TreeSectionController`, which publishes a
  `TreeToolbarCommands` instance on every tree render and withdraws it in
  `ResetTreeRenderState`; the buttons read that field at click time, so a
  click between one render dropping a tree and the next publishing one
  does nothing rather than reaching into disposed controls. The strip's Y
  arithmetic moved to the Blish-free `Services/TopRegionLayoutMath`, whose
  guarantee is that a hidden toolbar row costs exactly zero - the strip
  without it is byte-identical to the strip before the row existed.

- **M11, chrome looked clickable and TP looked like nothing.**
  UNKNOWN/UNRECOGNIZED/CURRENCY/GUILD UPGRADE drew the same crisp grey
  ring an Available pill draws; `PillKind.Locked` now recesses its ring to
  45% alpha and drops its label to 78% white. `PillKind.Subdued`
  deliberately does not follow - it looks muted but is a real click
  target, which is exactly what its separate switch arm was kept for.
  Separately, the Shopping List badged VENDOR/CURRENCY/UNKNOWN rows and
  left Trading Post rows bare, so "no badge" silently meant TP; every row
  is badged now, which makes an unbadged row a defect rather than a
  statement. Badging the majority row type promoted a latent overlap - the
  name column's ellipsis budget never reserved room for the tag - so the
  tag width is now subtracted from that budget in both the build and the
  re-ellipsis pass.

- **L6, pills vanished silently.** A tree row's pill column is a fixed
  240px budget and pills past it were simply not rendered, with nothing on
  the row to say they existed; the IGNORE toggle is emitted last, so it
  was usually first to go. `PlanRelayoutMath.ComputePillFit` now escalates:
  draw everything at normal padding; failing that, tighten side padding
  from 6px to 3px and draw everything; failing that, draw as many tightened
  pills as fit alongside a trailing "+N" pill whose tooltip names exactly
  what was left out. Tightening alone resolves the common case, so "+N" is
  the rare tail. Its tooltip deliberately does not say "widen the window":
  `pillColX` and `maxRightEdge` move together, so the budget is the same at
  every panel width and no window size would have shown the hidden pills.
  That same width-invariance is why the fit is resolved once at build time
  while the resize closure only repositions. Per the finding, the "+N" pill
  is not wired to a popup offering the hidden options: the hidden pills are
  almost always the trailing annotation and the IGNORE toggle, and a real
  affordance means a new popup surface with its own dismiss/focus/scroll
  behaviour hanging off a case tightening already resolves - the desktop
  gate decides whether the fact needs an affordance.

Review round (audit-de-cost-tree), two defects found against the batch as
written and fixed on the same branch:

- **H3's dimmed Subdued pill did not actually keep its "why it loses"
  text.** `PillSubduingTooltipBuilder.Build` was called only inside the
  `if (interactive)` arm, and `interactive` is false on a dimmed row, so
  the pill the bullet above (and gate step 3) names as the both-tooltips
  case showed only the "Under a bought item" line. The subduing text is
  now built from the spec before the interactivity branch - it is pure
  text and never depended on the click wiring - and `PillKind.Subdued`
  gained its own tooltip arm for the not-wired case. Gate step 3's
  "must show BOTH" is now the behaviour the code implements.

- **M3 moved the five actions out of the region batch F dims.**
  `SetContentDimmed` (the "this is the plan you are replacing" fade
  applied for the length of a Generate run) writes only
  `_contentPanel.Opacity`, and the toolbar row is parented to the strip,
  so the buttons that mutate the superseded plan sat at full brightness
  above a faded tree - and, since Opacity does not block hit-testing,
  Best Path / Buy All still re-solved it mid-run. `SetContentDimmed` now
  drives the toolbar panel's opacity and disables the five buttons for
  the length of the run, restoring both on every exit path the content
  dim is restored on.

Height-math check at this HEAD: nothing in this batch changes a
renderer-emitted height. The dimmed rule and every pill (including "+N")
live inside the unchanged `TreeRowHeight`; the shopping tag lives inside
the unchanged `ShoppingRowHeight`; the tree's section header is a fixed
30px whether or not it holds buttons. The one new height contract is the
top strip's, and `TopRegionLayoutMathTests` pins it with absolute pixel
literals in both toolbar states.

Validation: build 0 errors, full suite 1968 passed / 0 failed (1906
baseline, 1939 after batch D). No new test references Blish.

What the desktop gate should look at:

1. **Toolbar placement:** generate a plan long enough to scroll, expand
   several branches, scroll to the bottom. All five buttons must still be
   on screen and working. Confirm Collapse All from that scrolled position
   actually collapses, and that Best Path / Craft All / Buy All still
   re-solve. At minimum window width (930) the five buttons must not
   collide with the "Recipe Tree:" label.
2. **Toolbar row appearing:** with no plan on screen, the strip must show
   no toolbar row and no gap where one would be. Press Generate; the row
   must appear once, with everything below it shifting down by exactly one
   row, and the content area must not flash or lose scroll position.
   Generating a second plan must not shift anything.
3. **Dimmed branch:** expand a bought node's reference branch. The pills
   must be readable as pills (not a row of near-black rings), a single
   continuous vertical rule must run down the branch's left indent, and
   hovering a dimmed CRAFT/TP/VENDOR or IGNORE pill must show the "Under a
   bought item" line. Hovering a dimmed pill that already had a tooltip
   (a Subdued pill, or a committed pill with value detail) must show BOTH,
   not one replacing the other.
4. **Non-interactive chrome:** find an UNKNOWN or CURRENCY pill next to a
   clickable Available pill on another row. The two must be
   distinguishable at a glance without hovering.
5. **"+N" pill:** find a row with many pills (a partly-owned item with
   three sources, so CRAFT/TP/VENDOR + "HAVE n/m NEEDED" + IGNORE). Either
   every pill fits at the tightened padding - which is the expected common
   outcome - or a "+N" pill sits at the end whose tooltip names exactly the
   pills that are missing. A row must never simply end early with pills
   silently gone. Confirm the tightened pills still read cleanly.
6. **Shopping badges:** every Shopping List row must carry a badge, TP
   rows included, and a long item name must not push its badge into the
   Amount column.
7. **Toolbar during a re-generate:** with a plan on screen, change a
   setting and press Generate. For the length of the run the toolbar row
   must fade with the plan below it and its five buttons must be
   unclickable, then both must come back at full strength when the new
   plan lands - including on a cancelled or failed run.

Gate: PASS (2026-08-22 evening desktop batch, branch build at the
review-fix HEAD, captures preflight/gDE1-gDE4) - this is the live
desktop verification the maintainer required before this batch may
merge. Verified on the restored Mystic Clover x77 plan: (1) plan
header as one left-aligned Font32 "Mystic Clover x 42 needed" line,
no in-scroll timestamp; (2) promoted Actual Cost to Craft tile with
"+ 3 currencies required" under its caption, three tiles sharing a
bottom line, section headers demoted below the title; (3) the tree's
Item / Source / Cost header row with per-denomination sub-columns -
coin and currency icons forming straight vertical rules down the
cost column; (4) the five tree actions in the fixed strip
("Recipe Tree:" cluster with the wider gap before Expand/Collapse);
Collapse All clicked from the strip WHILE SCROLLED TO THE BOTTOM
collapsed the tree with the viewport staying put - the M3 scenario
end to end; (5) a live "+2" overflow pill on the root row (its cost
cell spans three denominations, narrowing the pill column exactly as
L6 predicted); (6) dimmed subtree pills at the raised dim floor with
the value-detail tooltip still served on the dimmed selected pill
("Only available source / Crafting gold price ... Optimization
price"); (7) every Shopping List row badged - VENDOR rows and the
Glob of Ectoplasm TP badge. Not exercised live: gate item 7's
mid-generate toolbar fade (the restored-plan session did not run a
fresh generate on this build; the dim shares SetContentDimmed's
single restore funnel review-verified in batch F) and the narrow-
width pill-drop escalation beyond the +2 case. Suite 1969/1969 at
HEAD; the height-math contract was re-walked clean by the verify
round.
