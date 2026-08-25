> **Frozen record - 2026-08-23, branch `cost-band-restyle`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Cost band restyle (cost-band-restyle)

Revises audit-D's promotion of the Total Cost section's result tile,
from the maintainer's live field test: "the currency table under total
cost in craftin plan needs to be centered. the size of the
gold/silver/copper text for total materials value and your materials
used is not the same as for Actual cost to craft - they should be the
same. if you want to visually highlight actual cost to craft, draw a box
around it and give it a colored tint and semi transparency so the
background texture still peeks through.. this will draw the eye to focus
there while keeping overall visual balance."

### What audit-D did, and why it is being undone

Audit-D promoted "Actual Cost to Craft" to `DefaultFont32` and paid for
the extra leading with `PlanContentHeightMath.PromotedCostTileRowHeight`
(76). The promotion worked - the eye did land on the figure - but it
broke the thing the band exists to say: three tiles that read as one
formula, `Total Materials Value - Your Materials Used = Actual Cost to
Craft`, cannot read as one formula when the right-hand side is drawn at
twice the size of the left. The field test is the first look at it in
the live game, and it says so directly.

### The three changes

**One amount font.** All three tiles render their coin runs at
`DefaultFont16` - the SMALL tiles' existing size, not one size up. Two
reasons for taking the smaller of the two options the directive allowed:
each tile's coin run is centred inside its own `(panelWidth - 40) / 3`
slice, and a three-denomination run (`123g 45s 67c`, six controls) is
already close to that slice at Font16, so growing every tile's font
would push all three toward overlapping their neighbours at ordinary
window widths rather than only the one that overflows today. And the
emphasis is now carried by the box, which is exactly what the directive
asked for ("No font-size-based emphasis") - a larger shared font would
be re-introducing a weaker version of the thing being removed.

**The highlight box.** The result tile's caption, its `+ N currencies
required` disclosure line and its coin run are wrapped in a box: a warm
gold tint (`214, 176, 96`) at alpha 0.14 for the fill and 0.5 for the
1px border, both scaled from one tint with the same premultiplied
`Color * f` idiom `FullCoverageFill` already uses. Blish composites a
Panel's `BackgroundColor` over what is behind it, so the window's
parchment texture reads through the fill - that is the "semi
transparency so the background texture still peeks through" the
directive asked for, and it is the reason the fill is not simply a solid
dark swatch.

Structurally the box is a real `Panel` and the result tile's controls
are its CHILDREN, not its siblings. That buys two things: the fill is
painted behind them by the container's own paint order, so nothing
depends on sibling z-order; and because the box's width is font-derived
(width-invariant), a resize repositions one control instead of
re-centring three runs. The box is never clamped to the tile width -
Blish clips a container's children, so a box narrower than its content
would CUT the amount off, where an unboxed run merely overlaps its
neighbour.

The box panel IS the fill, and the 1px frame is four edge panels drawn
ON it. The first draft instead copied `LabelHelpers.CreateSmallTag` - a
border-coloured OUTER panel with the fill inset inside it - which is
wrong here: that idiom under-paints the whole interior with the border
colour, and every existing caller only gets away with it because its
border is OPAQUE, so the under-paint is invisible by construction. This
is the first caller with a translucent border, and the under-paint made
the interior composite at `1 - 0.5 * 0.86 ~= 0.57` instead of the
documented `0.14`: a near-solid gold slab with no discernible ring,
i.e. the exact opposite of what the directive asked for. With nothing
beneath the fill, the interior is 0.14 (parchment reads through at 86%)
and an edge - frame over fill - lands at ~0.57, four times the
interior's density, which is what makes a 1px ring read as an edge. The
edges are siblings of the tile's labels but can never overlap them:
content is inset by `CostBandBoxPadX`/`PadY`, both larger than the
border width.

The box's geometry (`CostBandBoxTop`, `CostBandBoxHeight`,
`CostBandBoxWidth`) and the amount's bottom-anchoring clamp
(`BandAmountY`) live in `SummarySectionLayoutMath` beside the constants
they are built from, not inline in the Blish-bound renderer, so the
tests below call the production expressions rather than restating them.

**The centred currency table.** Batch H pulled the Required/Have/Needed
block in beside the currency names, which closed the dead gutter but
left the finished table pinned against the section's left edge with all
the recovered space dead to its right. `CurrencyTableOffsetX` now
centres it. Centring moves ONE control per row - a content panel holding
the whole row, the header band included - rather than shifting every
column's x, so the columns keep the panel-relative geometry
`SummarySectionLayoutMath` already computes for them, the header cannot
centre differently from the rows under it, and a table still spanning
the panel gets offset 0, i.e. byte-identically the old layout.

### Height math

`CostBandHeight` is re-derived from the new geometry instead of a
promoted font's leading: `6` box margin + `6` box pad + `20` caption
line + `4` gap + `20` coin run + `6` box pad + `6` box margin = **68**,
and **86** with the disclosure line (`+18`, unchanged). It was 76/94.
`PlanContentHeightMath.PromotedCostTileRowHeight`, whose only reader was
`CostBandHeight` (compiler-verified - `PlanContentHeightMath` is a
high-evidence zone, and this is a deletion of a constant nothing reads,
not a change to one that is read), is gone.

The 20px caption-line reserve is deliberately larger than the ~17 the
font measures: the renderer places the caption from real font metrics
and clamps the amount below it, so the reserve has to cover the tallest
plausible metric or the band clips its own amount. The DEBUG assert is
kept honest by asserting on the BOX's bottom edge for a highlighted band
(the box extends one pad below the amount, so it, not the amount, is the
band's lowest ink).

Not touched, by scope: `TreeCostColumnMath` and every other tree file
(concurrent branch), and `CoinCurrencyRenderer.SegmentLayoutHandle.
IconYOffset`, which is still used - `RichTooltipSurface` centres its own
coin icons with it - so only its doc comment's now-wrong example (the
promoted tile) was corrected.

Sweep for the same class of defect (two font sizes in one row of
comparable stats): none found. The remaining `DefaultFont32` use is the
plan's TITLE and `DefaultFont18` is a craft-step NUMBER - both are a
different kind of thing from the stat beside them, not the same kind at
a different size.

### Validation

Build 0 errors, suite 2192 passed / 0 failed (2186 baseline, +6: the two
re-baselined `CostBandHeight` literals rewritten, four new
`CurrencyTableOffsetX` tests (pinned/centred/narrow-panel/geometry-
preserved), one test driving `BandAmountY` + `CostBandBoxHeight` to pin
the box inside the reserved band across the whole plausible range of
measured caption heights, and one pinning `CostBandBoxWidth` against its
tile slice at the narrowest panel the module can present). Tree clean,
nothing pushed.

### What the desktop gate should look at

1. **Equal sizes.** Total Materials Value, Your Materials Used and
   Actual Cost to Craft draw their gold/silver/copper numbers at the
   SAME size, and all three sit on one baseline with the `-` and `=`
   operators between them.
2. **The box.** A tinted, semi-transparent box surrounds the Actual Cost
   tile's caption + disclosure line + amount, with breathing room on all
   four sides - and the parchment texture is visible THROUGH the fill,
   not painted over by it. The eye should land there first without the
   band looking lopsided.
3. **The centred table.** The Currency table under the band sits in the
   middle of the section, with roughly equal margin either side, and its
   header band tracks its columns (Required/Have/Needed still right-
   aligned over their own numbers, the OK marker still at the right end).
4. **Resize.** Drag the window narrower and wider: the box stays centred
   on its tile, the table stays centred, and at narrow widths the table
   degrades to the left-pinned layout rather than overrunning the panel.
5. **A plan with no currency costs.** The disclosure line is absent, the
   band is shorter by exactly one line, and the box still fits inside it
   with no clipping at the top or bottom.

Gate: PASS (2026-08-23 desktop session, branch build at the
review-fix HEAD, capture preflight/gWB1-cost-band.png, restored
Mystic Clover x77 plan). All three tiles' coin runs render at one
size; the Actual Cost to Craft tile sits in the gold-tinted
translucent box with the parchment texture visibly reading through
its interior and a discernible ring (the review's recomposited
edge-strip construction working as intended); the "+ 3 currencies
required" note renders inside the box; the Currency table is
centered in the section with its internal column alignment and
header band intact. Narrow-width box-vs-operator overlap and the
resize relayout of the boxed tile were not staged live; both are
pinned by the new SummarySectionLayoutMath box-geometry tests.
