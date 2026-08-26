> **Frozen record - 2026-08-22, branch `audit-k-notes`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Audit batch K: Plan Notes wrapping (audit-k-notes)

UX audit finding M14. `Views/Rendering/NotesSectionRenderer` locked each
note to one `PlanContentHeightMath.FallbackTextRowHeight` (28px) row and
ellipsized it with `LabelHelpers.EllipsizeToWidth`, with a DEBUG assert
forbidding any child from exceeding that height. At ~830px usable that
capped a note near 100 characters before the rest became hover-only
tooltip text - while the maintainer's UI law routes every opportunity and
every complex consideration into exactly this section.

### Reconciliation: M14's one-row claim vs. the live capture's three lines

The 2026-08-22 desktop captures (`preflight/ph11-scroll2.png`,
`preflight/ph12-scroll3.png`, Mystic Clover x77) show the Notes header
reading "Notes (1)" above THREE stacked text lines. That is not a note
containing line breaks and is not evidence against M14:

- `PlanViewModelBuilder.BuildNotesSection` block 5 emitted the forge-scope
  caveat as **three separate `PlanRowType.NoteLine` rows**, one complete
  sentence each, with its own comment saying why ("a single ~243-char row
  would edge-clip exactly the caveat the note exists to deliver, and the
  split preserves the fixed height-per-row contract"). It was a
  hand-authored workaround for M14, written at the builder, one plan-
  content-specific note at a time. **This branch retires it**: the caveat
  is now one row carrying all three sentences, and the renderer wraps it.
  The builder no longer hand-splits notes to keep text on screen, and the
  comment that told the next author to do so is gone.
- The header's "(N)" counts logical note ENTRIES (`noteEntryCount`), not
  physical rows, which is why three rows read as "Notes (1)".
- No note Label in the builder contains a `\n` today. M14's claim -
  every note row renders as exactly one ellipsized 28px line - was and is
  accurate at the renderer.

So the gap to close was greedy WIDTH-wrapping of a long unbroken line,
which is what still ellipsized at ~100 characters. Explicit line breaks
are supported anyway (they compose with width wrapping, each hard line
wrapping on its own), so a note can carry its own breaks without the
builder having to split it into rows.

### What changed

- **`Services/TextWrapMath`** (new, Blish-free): greedy word wrap with a
  separate first-line budget, explicit-line-break splitting, hard-split
  of a token wider than a whole line, and the
  single-line ellipsis moved verbatim out of
  `LabelHelpers.EllipsizeToWidth`. Measurement is a `Func<string,int>`
  seam rather than a `BitmapFont` - the same shape
  `SummarySectionLayoutMath` uses when it takes an already-measured
  `widestNumberWidth`. `LabelHelpers.EllipsizeToWidth` is now the font
  adapter over it, so the two truncation paths cannot drift.
- **`Services/NotesSectionLayoutMath`** (new, Blish-free): the note text
  budget (via the shared `PlanRelayoutMath.NameMaxWidthBeforeColumn`, not
  a second copy), the per-note wrap, and the body height.
- **`NotesSectionRenderer`**: one 28px row Panel per wrapped LINE. The
  fixed row-height contract and the DEBUG child-extent assert are
  unchanged - only the row COUNT became width-dependent.
- **Height math**: the Notes arm now counts wrapped lines. `rows.Count`
  is no longer the row count on screen, so `Render` returns the height it
  built and `CreateCollapsibleSection` uses that (the same special-casing
  Summary already has, with the stronger property that the number cannot
  drift from what was built because it IS what was built).
  `PlanContentHeightMath.SectionBodyHeight` is untouched; its class doc's
  "no text wrapping anywhere in the file" sentence and
  `PlanViewModel`'s matching comment were corrected.
- **`PlanViewModelBuilder`**: the forge-scope caveat collapses from three
  hand-split sentence rows to one row, retiring the M14 workaround.
- **`ISectionRelayoutSink.RequestRerenderAfterSettle`** (new): the seam a
  re-ellipsis closure uses to ask for one deferred rebuild when the
  settled width changes a note's line count - see the resize decision
  below.

### Design decisions (recorded, not incidental)

- **Over-long single word hard-splits, it does not ellipsize.** A token
  wider than a whole line is broken across lines at the widest prefix
  that fits, so nothing is lost - ellipsis is exactly the text loss this
  work removes. Ellipsis survives only as the tail of a note past
  `TextWrapMath.MaxWrappedLines` (24).
- **Coin cell on the first line only**, so only the first line's budget
  is reduced by it and every later line gets the full width.
- **Continuation lines carry the same two-space indent** as the first, so
  a wrapped note reads as one block rather than as several notes.
- **Resize re-wraps at settle; a changed line count defers to a rebuild.**
  `RunReellipsis`/`ReplayRelayout` must never change a row's height (see
  `CraftingPlanView`'s `_relayoutActions` field comment; that property is
  what lets the settle pass skip scroll preservation entirely), and this
  section spends one row per line - so a width that changes a note's line
  count changes the section's height. The closure writes the new text
  back in place while the count is unchanged; when it moves, it calls
  `ISectionRelayoutSink.RequestRerenderAfterSettle` and `ResizeSettleStep`
  runs one `PreserveScrollAcross(() => RenderPlan(_currentPlan))` after
  the pass finishes - deferred because `RenderPlan` clears the registry
  `RunReellipsis` is iterating. At most one rebuild per settled drag, only
  when a line count actually moved, and through the same scroll-preserving
  path every other rebuild (Generate, pill re-solve, hide-unlocked toggle)
  uses. The earlier slot-pinned variant of this pass was rejected in
  review: padding a shortened note to its old slot count leaves permanent
  blank 28px rows INSIDE the section, mid-note, for the rest of the
  session's plan.
- **A truncated note puts the full text on the tooltip of every one of
  its rows**, not just the last one, so a hover anywhere on the note
  reads the whole thing.

### Tests (+39, 1890 -> 1929)

`TextWrapMathTests` (24): ellipsis parity cases (fits whole, truncates,
budget narrower than "...", non-positive budget, null measure); short
text stays one line; empty and null text still produce one line; long
text breaks at word boundaries with every word kept; no line exceeds its
budget; wrapping consults the measurement rather than counting characters
(a proportional measure where "i" is narrow); over-long word hard-splits
losslessly, both alone and after normal words; explicit `\n`, `\r\n` and
`\r` breaks compose with width wrapping and force a break even when the
text would fit; blank source line keeps its own row; leading indent is
content; the first-line budget is honored separately; past the line cap
the tail ellipsizes and reports truncation; a zero budget terminates with
the text intact; the wrap is deterministic at a fixed width (what the
renderer's line-count comparison rests on) and a wider budget genuinely
needs fewer lines (the widen case behind the deferred rebuild).

`NotesSectionLayoutMathTests` (15): the text budget reserves the coin
cell plus its gap and matches the shared `NameMaxWidthBeforeColumn`
formula; short note is one indented line; empty and null notes still
occupy one row; the real forge-scope note wraps with no ellipsis and no
lost words; every line of a wrapped note is indented; a valued note's
first line is shorter than the rest and both respect their own budgets;
explicit breaks compose with width wrapping; narrowing then widening back
recovers exactly the original lines, with no blank line and no leftover
ellipsis; a very
narrow panel does not degenerate; null measure throws; body height counts
wrapped LINES (and would have been undercounted by the old per-row arm),
zero/negative lines are zero, and one line is exactly
`FallbackTextRowHeight`.

`PlanViewModelBuilderNotesForgeScopeTests` now pins the caveat to ONE
`NoteLine` row carrying all three sentences (it asserted three rows
before), which is the regression guard on the retired hand-split.

Validation: module build 0 errors; suite 1929/1929.

Desktop gate should look at:
1. Generate the Mystic Clover x77 plan and open Notes. The forge-scope
   caveat must render in full with no "..." anywhere in the section, and
   the section body must end flush against its last line of text - no
   clipped final row and no blank rows, which is the check that the
   renderer's returned height matches what it built.
2. The forge-scope caveat is now ONE ~243-char note row, so item 1 is
   itself the wrap check: it must read as two or more indented lines that
   hang together as one block, with no "...". Also worth confirming on a
   second long note if the preflight account can produce one - a seasonal
   vendor tip ("During <festival>: <merchant> trades <cost> for Nx <item>
   (limit N purchases/week)") or a recipe-sheet savings lead-in.
3. Valued notes (any Excess/reclaim line, or the "Total reclaimable
   value" rollup): the coin amount must sit at the right of the note's
   FIRST line with the icons still to the RIGHT of each number, and the
   text must not run under it.
4. Narrow the window until a note re-wraps, then widen it back. Mid-drag
   the section must not jump or lose scroll position; at settle the note
   must re-flow to its correct line count at the new width, with NO blank
   28px rows anywhere inside the section and no "..." at a width the text
   fits. Scroll position must survive the settle-time rebuild that the
   changed line count triggers - this is the item that exercises
   `RequestRerenderAfterSettle`, so also confirm repeated narrow/widen
   cycles do not accumulate drift or flicker.
Gate: PASS (2026-08-22 evening desktop batch, branch build 35b83f9,
captures preflight/gK1-gK3). Generated Mystic Clover x77 live on
the branch build: the Notes section read "Notes (1)" and the forge
caveat rendered as ONE note wrapping naturally across two width-fit
lines (breaking mid-sentence at "e.g. / precursor forging"),
replacing the three hand-split single-sentence rows the 2026-08-22
photography captures show on master. Section heights composed
correctly below Crafting Steps with the timegate line intact. The
resize-across-a-line-count-change rebuild (RequestRerenderAfterSettle)
was not exercised live - synthetic resize-grip drags are documented
unreliable - and stands on the TextWrapMath/NotesSectionLayoutMath
tests plus the verify pass's height-contract walk.

---
