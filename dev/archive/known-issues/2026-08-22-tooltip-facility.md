> **Frozen record - 2026-08-22, branch `tooltip-facility`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Tooltip facility (tooltip-facility)

Audit item C tier 2. Tier 1 fixed the CONTENT of two tooltips (wrapping
composed lines through `Services/TooltipTextFormat`) and explicitly
deferred the container. This branch does the container - once, centrally
- because sizing, wrapping, placement and opacity are a UI-toolkit job,
not something each of the module's ~40 tooltip sites should re-derive.

`Views/Rendering/TooltipFacility` is the single choke point, with two
paths chosen by CONTENT rather than by caller:

- **PLAIN** - composed or long prose. Wrapped through
  `TooltipTextFormat` (tier 1's seam, kept as the text core) and handed
  to Blish's `BasicTooltipText`. Five short one-line literals stay bare
  assignments; a button label needs no facility.
- **RICH** - anything carrying a coin amount, which a string tooltip can
  only spell out as "1g 23s 45c". Drawn by
  `Views/Rendering/RichTooltipSurface` with real gold/silver/copper icons
  (icons RIGHT of their numbers, repo invariant) via
  `CoinCurrencyRenderer`, on an opaque background, clamped to all four
  screen edges.

The tree ROW tooltip and the decision PILL tooltip - audit H6's two
worst surfaces - are on the rich path. Everything else that composes or
runs long routes through the plain path.

### Blish-free split

`Services/TooltipContent` models a tooltip as lines of spans where a
coin span still knows its copper value; every span also carries plain
text, so `ToPlainText`/`ToPlainLines` reproduce byte-for-byte what the
three composers (`ValueDetailTooltipBuilder`,
`PillSubduingTooltipBuilder`, `TreeRowTooltipComposer`) returned before.
Each composer gained a `*Content` entry point and its old string entry
point became a plain-text view of it - which is why every pre-existing
composer test passes untouched.

`Services/TooltipLayoutMath` holds the row breaking (reusing
`TextWrapMath`, with the current row's remaining width as the first-line
budget so a span after a coin run wraps against what is left of the row)
and the placement. Both are unit-tested against synthetic measure
functions; the Blish-coupled shell is the ~180-line surface class.

**One wrap policy per path, never two on one string.** The structured
content is UNWRAPPED. The plain path wraps it at the composer's return
seam with `TooltipTextFormat`'s 75-character budget; the rich path wraps
the same content against a real font at a real pixel width. Applying
both would break every long line twice.

### THE LIFECYCLE DECISION: one shared surface, never one per control

**Decision.** There is exactly ONE `RichTooltipSurface` for the whole
module, repointed on hover. Content is held in a
`ConditionalWeakTable<Control, TooltipContent>` keyed by the hovered
control, so the facility never holds a control alive and a disposed
row's content is collected with it. `Module.Unload` disposes the single
surface. The facility deliberately offers NO API for a per-control
instance.

**Measured basis** (decompiled from
`packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe` with `ilspycmd`, repo
precedent - these are readings of BlishHUD 1.3.0, not inference):

- `Control.Dispose(bool)` clears effects, nulls every event, cancels
  tweens, sets `Parent = null` and calls `DisposeControl()`. **It never
  touches the control's `_tooltip` field.**
- A Tooltip is **not its owner's child**: `Tooltip.Show()` sets
  `Parent = Control.Graphics.SpriteScreen` and `Hide()` sets
  `Parent = null`. So `Container.DisposeControl`'s `GetDescendants()`
  sweep never reaches it either.
- Therefore **nothing in Blish ever disposes a Tooltip.** One instance
  per tree row and per pill - controls this module rebuilds wholesale on
  every render - would leak one undisposed container plus its whole
  child tree (Labels, coin icon Panels holding `AsyncTexture2D`
  references) per row per render.
- Correction to a common assumption: `BasicTooltipText` does **not**
  share one basic view. `Control.Tooltip`'s getter lazily creates
  `new Tooltip(new BasicTooltipView(_basicTooltipText))` **per control**,
  and only on hover, because `Tooltip.HandleMouseMoved` is the only
  reader (`Control.ActiveControl?.Tooltip`). Blish's own basic path
  therefore leaks too - one small Tooltip+Label per hovered control, plus
  another each time the text changes, since the `BasicTooltipText` setter
  nulls `_tooltip` without disposing it. It is small enough to have gone
  unnoticed; at this module's control counts it would not be.
- The one thing Blish does do on owner disposal:
  `Tooltip.ControlOnActiveControlChanged` subscribes `Disposed` on the
  ACTIVE control and answers with `SetCurrent(null)` -> `Hide()`. That
  prevents a visible orphan tooltip on screen; it does not prevent the
  leak.
- The shared instance is safe against use-after-dispose by Blish's own
  guard: `Control.Tooltip`'s getter checks `_tooltip._disposedValue` and
  returns null (or the basic fallback) rather than returning a disposed
  container. So a control that outlived `Shutdown()` simply shows nothing.

**Ordering hazard the facility encodes.** `Control`'s `BasicTooltipText`
setter nulls `_tooltip` whenever the text changes. `ApplyRich` therefore
clears `BasicTooltipText` BEFORE assigning the surface; the reverse order
would silently drop it.

**Content replaced under a stationary cursor.** `ApplyRich` finishes by
calling `RichTooltipSurface.RefreshShowing`, which redraws the box when
the surface is already visible for that same control (and hides it when
the new content is empty). This is parity with the plain path, not a
nicety: the `BasicTooltipText` setter either writes the new text into the
live `BasicTooltipView` (`ActiveControl == this`) or drops `_tooltip` so
the next hover rebuilds - both branches refresh. The rich path has no
such setter, and `Tooltip.HandleMouseMoved` calls `Show` only while the
tooltip is HIDDEN, so a re-applied content would otherwise stay stale
until the pointer left. The reachable path is the tree's settle
re-ellipsis (`UpdateTreeRowTooltip` from the re-ellipsis closure) firing
after a window resize while the cursor rests on the row: the full-name
line appears or disappears, and the visible box has to follow.

### What the surface adds over Blish's own tooltip

**(a) Four-edge placement.** Measured (and already recorded under "Audit
batches A+B+C tier 1"): `Tooltip.UpdateTooltipPosition` flips
above/below the cursor to protect the TOP edge and shifts left by the
tooltip's full width at the RIGHT edge, and clamps neither result - a
tall tooltip placed below the cursor runs off the BOTTOM of the screen,
and the left shift can land at a NEGATIVE X.
`TooltipLayoutMath.Place` keeps Blish's above-when-it-fits preference
and its 36px cursor gap, then clamps all four edges. When neither side
can hold the box with its gap it takes the roomier side and clamps in -
the only case where it may reach across the cursor, and it needs a
tooltip taller than the screen minus the gap to happen at all. Re-run
every tick while visible, because `Tooltip.UpdateContainer` re-runs
Blish's unclamped positioning every tick.

**(b) Mouse transparency.** `RichTooltipSurface.TriggerMouseInput`
returns null unconditionally. Blish's tooltips avoid becoming the active
control only by never being placed under the cursor - exactly the
constraint the clamp relaxes. Without the opt-out, a clamped tooltip
would win the hit test (Container and Label both default to
`CaptureType.Mouse`), fire `ActiveControlChanged`, and be hidden by
Blish's own handler on the next frame: a show/hide flicker loop.

**(c) Opacity.** Blish draws its tooltip art at `Color.White * 0.98f`
over whatever is behind it, which is the audit H6 bleed-through
complaint. The surface's content sits on an opaque panel inside the
content edge buffer, so the frame still reads as a Blish tooltip and
nothing shows through the middle.

**(d) A width that knows about the screen.**
`BasicTooltipView.MAX_WIDTH` is a hard 500 that knows nothing about the
display. `TooltipLayoutMath.MaxContentWidth` keeps 500 as the preferred
width - so a rich tooltip reads like every plain one - and narrows it on
a screen that cannot afford it, down to a 120px floor.

**(e) A wrapper that cannot overflow.** `DrawUtil.WrapText` splits on
spaces only and never splits an over-long token, so an unbroken run
wider than the cap overflows it outright. `TextWrapMath` hard-splits.

### Feeder lines fixed

Both lines the audit named as reaching a tooltip unwrapped now go
through the facility:

- the full item name prepended by `TreeSectionController.
  UpdateTreeRowTooltip` on an ellipsized row (item names are unbounded);
- the pill tooltip's head prose - the acquisition hints and the
  HAVE/OWN/"needs N total" sentences - which the pill loop used to
  assign raw and concatenate with `"\n\n"`.

Both are on the rich path, so they are wrapped against the real font at
the real pixel width rather than at the 75-character approximation of
it.

### Swallowed hover on the tree row (found migrating it)

The tree row tooltip was assigned to the row Panel only. Tooltip lookup
reads exactly ONE control - `Tooltip.HandleMouseMoved` uses
`Control.ActiveControl`, the deepest capturing control under the cursor -
so the row's own name and quantity Labels swallowed it. The tooltip fired
only over the bare strip of Panel beside them, which is the one place a
reader is not pointing. That is the same swallowed-hover class already
fixed in `ShoppingListSectionRenderer`, in `LogTabContent`'s rows, and in
this same file's pill outer/inner/label stamping; the tree row was the
remaining instance, and it hid exactly the full-item-name line the
tooltip exists to show. Both Labels now carry the content alongside the
Panel.

**Tooltips only - do NOT copy the row's handlers onto the Labels.** Mouse
EVENTS, unlike tooltip lookup, already reach the parent:
`Container.TriggerMouseInput` calls `base.TriggerMouseInput` on ITSELF -
which fires that container's own `Click` / `RightMouseButtonPressed` /
`MouseMoved` handlers - before it walks its children, and the deepest
child only wins the RETURN value (`control2 ?? control`, the value that
becomes `ActiveControl`) and `break`s out of its SIBLINGS. So a
left-click or right-drag over the row's name text already reaches
`rowPanel`, which is also why the row's hover wash lights up over the
name today. Stamping `toggleHandler` onto `nameLabel` would fire the
toggle twice per click and leave the row visually unchanged.

### Deliberately out of scope

The confirm-dialog-across-tab-switch callback hazard the ABC review
flagged is untouched here. Two upstream Blish candidates remain noted
and unattempted: clamping `UpdateTooltipPosition` on all four edges, and
making `BasicTooltipView`'s max width relative to the screen.

### Desktop gate (live, required)

1. **Opaque value tooltip with coin icons:** generate a currency-bearing
   plan (a Mystic Clover / spirit-shard chain - anything whose committed
   CRAFT or VENDOR pill diverges) and hover that pill. "Crafting gold
   price", "Currencies" and "Optimization price" must each show real
   gold/silver/copper ICONS, each icon to the RIGHT of its number - not
   the letters "g"/"s"/"c". The opportunity-cost sentence must be
   wrapped across lines with nothing cut off. Nothing behind the tooltip
   may show through it: hover a pill over a busy part of the tree and
   confirm no row text, divider or icon is visible through the tooltip's
   middle.
2. **Tree row tooltip, coin icons and the name line:** hover a
   quantity>1 TP row - its "Unit price" line must show coin icons. Hover
   a row whose NAME is ellipsized: the tooltip must open with the full
   item name, wrapped if long, above the other lines. Hover directly over
   the row's NAME TEXT and over its quantity prefix, not just the empty
   strip beside them - the tooltip must appear over all three (before
   this branch it appeared over none of the text). With the cursor on the
   NAME text: left-click must expand/collapse the row exactly ONCE per
   click (not toggle-and-toggle-back), and right-click must open the wiki
   page - both handlers still live on the row Panel only.
   Then park the cursor on an ellipsized row's name and resize the module
   window until that name FITS: the open tooltip must drop its full-name
   line while the cursor sits still, and gain it again when the window is
   narrowed back.
3. **Stays inside the screen on all four edges:** drag the module window
   so a tree row sits near the BOTTOM of the screen and hover a pill
   with a tall tooltip - the tooltip must stay fully on screen (this is
   the edge Blish never clamps). Repeat near the RIGHT edge, near the
   LEFT edge with a wide tooltip, and near the TOP. In every case the
   whole box must be visible, and the tooltip must not flicker
   on and off as the cursor sits still (the hit-test opt-out).
4. **Pill tooltip composition:** hover a subdued (decisively-losing)
   pill that is still clickable - the tooltip must read "Switch to X", a
   blank line, then the "more expensive" explanation, with its gold
   margin drawn as coin icons. On a dimmed reference branch, the same
   pill must show its explanation plus the dead-click line, with no
   blank line at the very top.
5. **Plain tooltips still fine:** hover Clear Cache, Delete Log File,
   the Use Own Materials checkbox, Hide Unlocked Recipes, a settings
   currency row's input, a shopping list row, a truncated log row, and a
   Summary cost tile. Each must show its usual text, wrapped, with no
   line running past the window edge and nothing missing.
6. **No leak across renders:** regenerate a plan a dozen times, hovering
   pills and rows between regenerations, then keep using the module.
   There must be no growing stutter on hover and no second tooltip ever
   visible at once.

Gate: PASS after one gate-found Critical was fixed and re-gated
(2026-08-23 desktop sessions, captures preflight/gT1-gT6). The
FIRST gate run crashed Blish fatally on the second hover:
RichTooltipSurface.DisposeContent built a List directly from
Children, and ControlCollection.CopyTo throws by design - the empty
first build survived, the first content REPOINT died
(InvalidOperationException through Tooltip.HandleMouseMoved,
process-fatal). Fixed in b57be54 with the repo's established
Children.ToArray() idiom (ten existing sites; this file was the
sole deviation) and re-gated: four hover repoints across different
controls survived. Verified live on the fixed build: the rich
value-detail tooltip renders OPAQUE (zero bleed-through of the
table behind - the H6 translucency defect is dead), with coin
amounts as real icon runs (gold/silver/copper icons right of the
numbers, per the invariant) for Currencies and Optimization price,
the opportunity-cost sentence wrapped inside the box, and the box
inside the window; a 3-second held hover showed no flicker (the
mouse opt-out working). The bottom/right edge-clamp cases and the
wiki right-click-on-name behavior were not staged live; both are
pinned by TooltipLayoutMath tests and the review round's
label-handler verification respectively.
