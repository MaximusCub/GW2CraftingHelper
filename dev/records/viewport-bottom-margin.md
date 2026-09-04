> **Milestone record - 2026-08-28, branch `viewport-height`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Content viewport falls short of the window bottom (KNOWN-ISSUES #66)

Reported in the field: the bottom of the viewable port sits far above the
actual bottom of the overall textured window. The
capture (`gate-master/viewport-short-of-window.png`, a crop of the lower
window on a 3440x1440 client) shows three tree rows, a fourth clipped
mid-height at the viewport's bottom edge, the scrollbar's down-arrow ending
ten pixels above that same line, and then a wide band of empty window
backdrop before the resize grip.

This is NOT KNOWN-ISSUES #65 returning, and the fix for #65 was neither
wrong nor incomplete. That defect was a size read back off a
`Panel.ContentRegion` that a skipped layout pass had left stale; it was real,
it is fixed, and `Services/PanelChromeMath.cs` still derives every size in
the chain rather than reading one back. What is left is not a stale number.
It is the correct number, and the correct number is too small - by a
constant, at every window size.

### The arithmetic

Blish sizes a window's content region in `WindowBase2.OnResized` as
`Height - ContentRegion.Y - _contentMargin.Y`, and both terms are fixed at
construction from the two texture-space rectangles the window is built with
(`ConstructWindow`):

```
_contentMargin.Y = windowRegion.Bottom - contentRegion.Bottom
ContentRegion.Y  = contentRegion.Y + TITLEBAR(40) - Padding.Top,
                   Padding.Top = max(windowRegion.Top - 40, 11)
```

Both rectangles are read as ABSOLUTE coordinates in the background texture.
`Module.cs` passed `windowRegion (35, 26, 930, 710)` and
`contentRegion (81, 11, 884, 684)`, and its comment shows the vertical pair
was reasoned window-region-RELATIVE: "flush would be contentRegion.Y +
contentRegion.Height == windowRegion.Height (11 + 699 == 710) ... so an
extra 15px margin keeps every row on opaque backdrop". Under Blish's reading
those numbers give

```
_contentMargin.Y = 736 - (11 + 684) = 41     intended 15
ContentRegion.Y  = 11 + 40 - max(-14, 11) = 40 = flush under the title bar
```

so the intended 15px of clearance became 15 + `windowRegion.Top` 26 = 41,
and the same mix-up spent nothing at all at the top. Everything below the
viewport, at window height H:

```
 41  Blish window content margin        (Module.cs, the defect)
 16  ViewAdapter.OUTER_PADDING
  7  Blish Panel.BOTTOM_PADDING         (the bordered panel's own inset)
 10  ViewAdapter.INNER_PADDING
 ---
 74  viewport bottom = H - 74
```

against 102 above it, of which only 26 is empty space (40 title bar and 36
`Panel.HEADER_HEIGHT` are drawn chrome). The measured capture agrees: tree
rows are 43px apart against `PlanContentHeightMath.TreeRowHeight` 48, so the
crop is at 0.896 scale, and the 64 crop px from the viewport's clip line to
the resize grip's lower edge is 71 real px.

The shortfall is CONSTANT, not proportional - `_contentMargin.Y` is a
control-space constant and the whole chain below it is additive, so the
viewport grows one-for-one with the window and the band never closes. It was
invisible to the #65 gate because that gate ran in a ~750px-tall sandbox
window where 74px of backdrop reads as ordinary padding, and because nothing
ever measured the gap.

### The premise behind the margin was also wrong

The 15px was for the texture fading "over roughly its last 15 rows".
Measured off asset 502049 itself (alpha channel, columns x=300/500/700):
alpha is 223-241 of 255 at row 736, which is `windowRegion.Bottom`, holds
above 200 until row 744, and only reaches 0 around row 765. The window
region already ends seven rows inside the opaque area. Nothing was ever
bleeding through there.

Scale check, since the texture is stretched with the window while the margin
is not: `windowRegion.Bottom` lands at `40 + 0.9793 * (H - 40)`, so the
opaque edge sits `~0.011 * H` above the control's bottom - 8px at H=750,
16px at H=1440, 23px at H=2160. With the bottom gap now 48px the panel's own
border stays inside the opaque area up to H=2850, and the drawn content
inside the viewport up to well past any real client.

### The fix

`contentRegion` is now `(81, 11, 884, 710)`: bottom margin 15, the number
the original comment intended, reclaiming 26px of viewport at every size.
The texture drawing is untouched by construction -
`_windowToTextureHeightRatio` is `(ContentRegion.Height + _contentMargin.Y +
ContentRegion.Y - 40) / 1024`, and `ContentRegion.Height + _contentMargin.Y`
is `windowRegion.Bottom - contentRegion.Y` = 725 whichever way the bottom
moves, so `BackgroundDestinationBounds` is bit-identical before and after.

The vertical terms of both rectangles moved into `Services/WindowSizing.cs`,
which already owned "the chrome between that window and a tab's content
panel" for the horizontal axis; `Module.cs` builds the two rectangles from
them, `Views/ViewAdapter.cs`'s two paddings are named there too, and
`WindowToTabPanelTopChrome` / `WindowToTabPanelBottomChrome` /
`TabPanelHeightFor` are the vertical twins of `WindowToTabPanelChrome` /
`TabPanelWidthFor`.

Every tab shared the defect and every tab is fixed by the one change: all
seven are hosted by the same `ViewAdapter` container, which is sized from
the window's content region. No tab file is touched.

### Regression coverage

`tests/TaimisToolbench.Tests/Services/PanelChromeMathTests.cs` gains a sweep
that walks the whole chain - `WindowSizing`'s shipped constants plus the
production `PanelChromeMath` helper, exactly as `ViewAdapter` composes them
- at window heights 710, 750, 900, 1080, 1200, 1440 and 2160, and asserts
the gap below the viewport equals a budget written as literals rather than
summed back off the constant under test. Reverting the one constant to 684
fails nine cases, every swept height among them; a ratio error would fail
the one-for-one growth test beside them.

### Validation

- `dotnet build TaimisToolbench.csproj -p:Platform=x64`, Debug and Release - 0 warnings, 0 errors.
- `dotnet test TaimisToolbench.sln` - 3411 / 3 / 230, all green (module suite +18 over the branch point's 3393).
- CI invariant scripts run locally.

Gate: NOT RUN - no live game session was available while this branch was
written. The failing state is the capture named above; the confirming
capture is the same window with the plan's last row ending a panel inset
above the window's bottom edge rather than 74px above it.
