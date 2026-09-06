> **Milestone record - 2026-09-02, branch `m40-review-findings`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Defects in the sticky headers, the viewport cutoff, the tree columns and the dialog title

Pull request 239 fixes a set of unrelated defects left in the module after
pull request 238. Most of them are in the two mechanisms that milestone
built - the pinned sticky header and the scrolling viewport's clip cutoff -
plus the recipe tree's column arithmetic, the modal dialog's title, and the
vendor-offer tool. Several were code that had gone dead or duplicated, and
one was arithmetic that was right for one screen size only.

### A click on a pinned sticky header landed on the row behind it

`StickyHeaderHost` held its clip at `ClipZIndex` 1, below the scrolling
panel's vendor default 5, on the stated grounds that the low value let the
mouse wheel fall through to the scroll panel.

Read against Blish HUD's own source that could not work.
`Container.TriggerMouseInput` walks children by ZIndex DESCENDING and breaks
on the first that answers, and `Container.CapturesInput()` returns
`Mouse | MouseWheel` for every container. So the ZIndex-5 scroll panel won
every event, and a click or a hover over a pinned band landed on the
scrolled row hidden behind it. The class doc promised a pinned band sorts
like the header it is; it could not.

`Control.TriggerMouseInput` does discriminate by event type and returns null
for `MouseWheelScrolled` when the control lacks the `MouseWheel` flag. The
fix is `WheelTransparentClippedPanel` in `Views/Rendering/ClipCutoff.cs`,
which captures `Mouse` only. `ClipZIndex` becomes 10, so the clip wins the
click; the clip, the band (`HeaderBands.Band`) and `SortableHeaderCells`'
hover washes are all that type, so the parent's loop steps past all three and
the content panel raises the wheel event its own scrollbar is subscribed to.
Every container between the host's clip and the cursor has to answer the same
way or the walk breaks inside it.

The derivation is `docs/ARCHITECTURE.md` section V.26.2, which also records
that no Blish-free test can cover input dispatch.

### The top strip's ZIndex protected nothing

`CraftingPlanView.TopStripZIndex` 1 was documented as a cover that paints
over whatever leaks past the viewport's top edge, and
`Services/TopRegionLayoutMath` cited it as what protects the separator
rule's 2px.

`Container.PaintChildren` sorts `OrderBy(ZIndex)` - ascending - so a ZIndex
of 1 against the content panel's default 5 paints FIRST and covered nothing.
The defence in depth never existed.

The one control that overlaps the viewport is the separator rule, and it now
carries its own `SeparatorZIndex` 10, above the content panel, so it paints
last. Because that walk's opposite is the hit test, the rule also had to
become a `WheelTransparentClippedPanel` or its 2px would swallow the wheel
where it overlaps the first scrolled row. Both comments now state what is
true.

### The clip cutoff spent the worst UI scale's slip at every UI scale

`ClipCutoffMath.CutoffTopFor` added the constant `SlipBudget` 2 - the worst
round-trip loss over the four GW2 UI Sizes - whatever scale was live. At UI
Size Large the scissor round trip loses nothing, so that cut the first 2px
off every scrolled row in every viewport and off every row under a pinned
band. The gap between the reserve and a given edge's actual loss varies with
the edge's phase, which is why the pinned band's seam flickered while
scrolling.

`SlipBudgetFor(scale)` measures the live scale's own worst case over a
`PhaseSweep` of 4096 absolute y values and caches one slot: 0 at 1.0, 1 at
1.103, 2 at 0.81 and 0.897. Measured rather than tabulated, so an unlisted
scale gets its own true answer instead of the four-value worst; a scale that
is not positive, NaN included, falls back to the constant.

Nothing is obliged to paint the strip between the protected edge and the
cutoff, so `StickyHeaderHost` now paints it: while a band is whole, its clip
is `SlipBudgetFor` pixels taller than the band and carries
`HeaderBands.BandColor`, which became internal for that. Only while the band
is whole - during the push-out its bottom is the table's last row, and
padding past that would draw band colour below the table.

### A part-placed frame published a cutoff for bands that had not moved

`StickyHeaderHost.Update` reset `_pinnedBottom` before placing, so a throw
part-way through published a cutoff describing bands that were never moved.
The value is absolute while the clips are parent-relative, so a frozen
placement drifts from its band as soon as the window is dragged, and
scrolled rows then overdraw exactly the band the cutoff was protecting.

`Update` now accumulates into a local and publishes only on completion. The
failure path calls a new `StandDown`, which returns every band to its own
container and withdraws the cutoff, leaving the tables scrolling with no
sticky headers at all. Nothing is disposed - a control must not leave the
tree from inside the update walk over it.

### The slip budget was published as two static fields

`SlipBudgetFor` first cached a scale and its measured budget in two static
fields, which a concurrent caller could read torn: a budget belonging to one
scale returned against another scale's key. The comment argued the hazard
away with "both on the paint thread", which is true of today's callers only.
The pair is now one immutable object published by a single reference write,
so a stale read is a whole stale pair.

### The modal dialog's title sat 14px above the built-in one

`DialogLayoutMath.TitleLineY` was the constant -11, which is the title bar's
draw-RECTANGLE origin. `DrawStringOnCtrl` defaults to
`VerticalAlignment.Middle` and offsets by `rect.Height / 2 - textSize.Y / 2`,
so the line box starts lower than the rectangle does.

Both inputs are measured from the vendor's own tree rather than inferred:
`ref/titlebar-inactive.png` is 1024x64, and the face
`ContentService.DefaultFont32` resolves to,
`Content/fonts/menomonia/menomonia-32-regular.fnt`, has `lineHeight` 36. The
built-in title therefore seats at -11 + 32 - 18 = 3, and `ModalDialog`'s
self-drawn title was rendering 14px above the one `ApiAccessDialog` shows.

`TitleLineY` becomes a function of the title's own measured height and
reproduces the vendor's expression term for term, each half truncated
separately, which differs by a pixel from `(Height - textSize) / 2` on an odd
face. `ModalDialog` passes `title.Height`, already written synchronously by
`Label.RecalculateLayout` from the AutoSize setters in the same initialiser.

### The tree's pill column ratcheted its granted width instead of its ink

`ScannedPillColumnWidth` applied the plan-lifetime floor to the granted
`width`, then derived `costClaim` from the CURRENT surplus. Two consequences.
A floor-held width was re-attributed after any resize, so
`EffectiveCostColumnWidth` changed at a constant pill width and `PillColX`
moved. And the ratchet froze the share a wide window had afforded, so
widen-then-narrow left the name column without the minimum-window budget
`TreePillColumnMath.Affordable`'s own doc promises it keeps.

Both are the same mistake: the one-way floor was on the granted width. What
an ignore click shrinks - and therefore what the no-narrowing invariant has
to hold - is the widest run the tree REQUIRES. The ratchet moves onto that
required run; `_planPillColumnFloor` becomes `_planPillRequiredFloor`,
starting at 0 rather than at the column's fixed floor.

At a constant panel width the two orders are provably identical, because
clamping is monotonic and `max(clamp(a), clamp(b))` equals `clamp(max(a, b))`.
They part only across a resize, which is the case that was wrong.

The whole width-and-claim rule moves into `TreePillColumnMath.Resolve`, which
returns the width, the claim and the ink to carry forward as one
`ColumnResolution`, so the two can never be attributed to different window
widths, and a Blish-free test can pin what the view could not expose.

### An in-place tree refresh could keep a stale cost-column claim

`TryRefreshInPlace` gated on the pill column's width alone. The claim is
netted out of the cost column by `EffectiveCostColumnWidth`, so a refresh
that kept a stale claim placed its preserved rows where a full render at the
same window size would not have. It now gates on the width and the claim
together, which is what `Resolve` returning both makes possible.

### The vendor-offer tool walked past a worktree root

`tools/VendorOfferUpdater/Program.cs`'s `FindRepoRoot` probed for a ".git"
DIRECTORY only. A linked worktree marks its root with a ".git" FILE holding a
gitdir pointer, so every run inside a worktree walked past the worktree root
and resolved `ref/vendor_offers.json` against whichever repo it met next, or
against the process's working directory. It is the same bug already fixed in
`tools/MysticForgeSeeder/Program.cs` on branch `w5-seederfix`.

The probe now tests both. The tree was swept for a third site: those two
`FindRepoRoot` implementations are the only upward directory walks in
`tools/` and `tests/`, so there is none. `FindRepoRoot` takes an optional
start directory so the walk is testable; the production call still starts at
`AppContext.BaseDirectory`.

### The inverted-ink rule was production-dead

`PillColors.GlyphColor`, which inverted the IGNORE toggle's mark against its
own plate, had been dead since a `FeedbackButton` replaced the hand-drawn
toggle. Its only remaining citation was its own neighbour's
comment. Both are deleted.

Its doc guarded a real 3:1 non-text contrast floor, so the argument is
measured and kept rather than deleted with the code. Blish multiplies
`AbsoluteOpacity` into every `DrawOnCtrl` colour, so a dimmed ignored toggle
composites its black ink AND its amber plate toward the backdrop together:
on the shipped `#9C7327`, by sRGB relative luminance, 4.90:1 at full
strength, 1.87:1 over black and 2.04:1 over the row's own backdrop. White ink
in that state alone would read 4.41:1 and 4.04:1. The colour is not changed -
this glyph is ruled black, and inverting it in one state is a design call
rather than a correctness fix. The figures are filed under DEFERRED in
`docs/KNOWN-ISSUES.md`, which the call site cites.

### A tinted disabled button drew a dim icon over a lit plate

`FeedbackButton.Paint` dimmed its icon on `Enabled` alone, while the plate
and the text ink both keyed on `Enabled || _plateTint.HasValue`. A tinted
button that was disabled therefore drew a dimmed icon over a fully lit plate.
All three layers now read one `showsState` local.

### The ignore toggle was square only by coincidence

The toggle took its width from `GlyphButtonMetrics.RowActionSize`, through
`ReservedIgnorePillWidth`, and its height from `PillHeight`. It was square
only while two independent constants both happened to be 24. Both axes now
come from `RowActionSize`, as the Ranker and Plan History rows already did.

### An unreachable arm in the pill wiring

Inside `else if (ignoreInteractive)`, `TreeSectionController` guarded a
hand-rolled hover-and-press wiring with `if (!isToggle)`. `ignoreInteractive`
implies `PillKind.Ignore`, which is exactly what `isToggle` tests, so the arm
could never run. The code is gone; its comment survives to say why nothing is
wired there - the button's press comes from `FeedbackButton`'s own
`PressFeedback` wiring and its hover is Blish's `OnMouseEntered` tween.

### One centring rule written three times

`DialogLayoutMath.TitleX`, `LineX` and the button row's `confirmX` were three
copies of "half the difference, never negative". They are one private
`CentredX` now, which is also what keeps a centred title agreeing with the
lines under it. `TitleX`'s input clamps become `LineX`'s too; the difference
is visible only for a negative measured width, which is not reachable.

### The vendor's title-bar constants re-typed in three places

`WindowBase2`'s `STANDARD_TITLEBAR_HEIGHT` 40 and
`STANDARD_TITLEBAR_VERTICAL_OFFSET` 11 were re-typed in `Views/DialogWindow`
and in the dialog title's seat while `Services/WindowSizing` already held
them privately. They are internal there now and both sites read them, so a
re-typed 40 in one place cannot go unreconciled.

### A copied Draw, and comments that had gone stale

`StickyClipAuthorityFlowPanel` was a verbatim copy of the sealed
`ClipAuthorityFlowPanel.Draw` with one term changed. That term becomes a
`protected virtual ProtectedEdge`, the `Draw` is inherited and sealed, and
the subclass is one expression. Its paint-order paragraph, stale after the
ZIndex change above, is corrected with it.

`EffectiveCostColumnWidth`'s floor cannot fire while the claim comes from
`ScannedPillColumn`, because `RightClaim` caps it at exactly the slack that
subtraction leaves. It is kept as defence for a claim written elsewhere and
now says so, rather than reading as a live clamp.

### Milestone codes and change history left in comments

Milestone codes and change-history narration were dropped from four
comments: two W5 codes in `TreePillColumnMathTests`,
`SortIndicatorLayout.Gap`'s account of how it came to be 8, and the `(V4)`
and `B1 #2` siblings in `RankerRowLayout` and `VendorBatchSolver`. The
measurements those comments carried are kept - `SortIndicatorLayout` still
records that at a space's advance the mark measured about 5px from the label
and read as attached to the word.

### Regression coverage

- `ClipCutoffMathTests`: the live budget equals that scale's own worst
  single-container slip and is 0 at 1.0; the one-slot cache answers each
  scale for itself across a change sequence; four unmeasured scales (0.75,
  1.25, 1.5, 2.0) each get a budget that bounds their own round trip at every
  y in the sweep; 0, -1 and NaN fall back to the constant 2.
- `TreePillColumnMathTests`: widen-then-narrow gives the name column its
  budget back and does not re-attribute the claim; a shrinking required run
  at a constant panel width holds the column; a Theory pins the
  clamp-then-ratchet equivalence across four panel/slack pairs (0/0, 200/0,
  400/45, 900/45); a panel width of 0 takes the floor and pins nothing.
- `DialogLayoutMathTests`: the title seat's arithmetic, that each half is
  truncated separately, and that -11 is correct for exactly one face height -
  one as tall as the whole title bar, which none is. The old case pinned the
  rectangle origin.
- `tests/VendorOfferUpdater.Tests/FindRepoRootTests.cs` is new: ".git" as a
  file finds the worktree root, as a directory finds the clone root, a
  worktree nested below an enclosing clone stops at the worktree, and no
  marker anywhere falls back to the working directory.

Input dispatch has no coverage and cannot have any here, because the repo
invariants bar a test from referencing Blish HUD.

### Validation

- Build 0 warnings.
- Suite 4123 + 242 + 3 at the end of the branch, from 4109 at the branch
  point.
- All 19 invariant gates green.
- Budgets raised in `docs/file-budgets.txt`: `StickyHeaderHost` 334 to 404,
  `DialogLayoutMath` 435 to 472 and its tests 461 to 484, `ModalDialog` 421
  to 425, `DialogWindow` 106 to 108, `FeedbackButton` 260 to 265,
  `TopRegionLayoutMath` 111 to 116, `MainView` 2830 to 2831, and
  `VendorOfferUpdater/Program.cs` 1844 to 1858.

Gate: NOT RUN - no live game session is recorded in the branch's commits, and
the two largest changes are exactly the ones no test can reach. To confirm in
game: click a pinned sticky header's column cell and check it sorts rather
than hitting the row behind it; scroll with the wheel over a pinned band and
check the table still scrolls; and check that at UI Size Large the first
scrolled row is not clipped below the separator rule and no hairline flickers
under a pinned band while scrolling.
