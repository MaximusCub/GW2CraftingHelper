> **Milestone record - 2026-08-27, branch `firstpaint-truncation`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## First-paint viewport truncation (KNOWN-ISSUES #64)

The report, in the maintainer's words: "there was a bug where the bottom of
the viewport was cut off until the window got a slight resize jiggle to
trigger full redraw flow". Three gate captures taken on 2026-08-27
(`gate-master/verify-costband.png`, `fs-open2.png`, `shot-plan-raw.png`)
show the same Exordium plan on a fresh window open: the plan header, the
Total Cost band and the four currency rows are drawn, and then nothing -
no Recipe Tree, no Used Materials, no Shopping List, no Crafting Steps,
just empty panel down to the bottom of the window. The plan behind those
captures has a 406-node tree, so the missing sections are not absent, they
are undrawn.

### What the capture actually measured

`verify-costband.png` is a 0.81-scaled crop (the currency rows measure 34px
against `PlanContentHeightMath.CurrencyRowHeight` 42; the window's persisted
width, 1378, lands on the module's own minimum). Reading the scroll chrome
off it is what turned "sections are missing" into a geometry statement:

- Blish parents a `Panel`'s scrollbar as a SIBLING and sizes it
  `ContentRegion.Height - 20`, positioned at `panel.Top + ContentRegion.Top
  + 10` (vendor `Panel.UpdateScrollbar`). The groove in the capture runs
  from the top of the content area to a hard stop level with the last drawn
  row, and nothing below it.
- So the scroll container - `CraftingPlanView`'s `_contentPanel` - ENDED
  where the drawing ended. Its own thumb is a small fraction of the groove,
  so it knew about the full content height; the viewport it had to draw it
  into was short. The sections below the fold were laid out, scrollable and
  clipped away.

That rules out the whole "content height computed too early" family
(`PlanContentHeightMath` and the section renderers were never implicated):
the defect is in the height of the VIEWPORT, which is
`buildPanel.ContentRegion.Height - TopRegionHeight`, and therefore in the
container `ViewAdapter` hands the view.

### Root cause, from the vendor assembly

Decompiled `packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe`
(`Blish_HUD.Controls.Control`, `Container`, `Panel`, `WindowBase2`):

1. `Container.ContentRegion`'s backing field is nullable, and the getter
   falls back to `new Rectangle(Point.Zero, Size)` - the panel's full size -
   until something assigns it.
2. `Panel` assigns it in exactly one place: `RecalculateLayout`, as
   `(left, top, size.X - left - right, size.Y - top - bottom)`, where a
   title reserves `HEADER_HEIGHT` 36 and a border adds the 4/7/4/7
   paddings. For the module's titled, bordered tab panel that is 43px of
   vertical chrome.
3. `Control.Size`'s setter reaches `RecalculateLayout` SYNCHRONOUSLY, via
   `OnPropertyChanged("Height", invalidateLayout: true) -> Invalidate() ->
   UpdateLayout()`, and only then raises `Resized`. In the ordinary case a
   `Resized` subscriber therefore sees a fresh `ContentRegion` - which is
   why this survived so long.
4. `UpdateLayout` has a guard: `if (Interlocked.Increment(ref
   _layoutSuspendCount) == 1 && Parent?.IsLayoutSuspended != true &&
   LayoutState != LayoutState.Ready)`. A control whose PARENT is
   layout-suspended does not recalculate at all. And a parent IS
   layout-suspended for the whole of its own `RecalculateLayout`, because
   the same `UpdateLayout` increments that counter around the call.

Put those together with this repo's own window. `Views/ResizableTabbedWindow`
calls `ClampToMinimum()` from `RecalculateLayout()`, and `ClampToMinimum`
writes `Size`. That write is re-entrant: it happens INSIDE the window's
layout pass, so for the duration of the nested `Resized` the window reports
`IsLayoutSuspended`. Every panel resized by a subscriber during that window
skips its own `RecalculateLayout` and keeps the previous size's
`ContentRegion`.

`Views/ViewAdapter` was reading exactly that:

```
borderedPanel.Size = <from the window's fresh ContentRegion>;   // correct
contentPanel.Size  = borderedPanel.ContentRegion - 2 * INNER;   // one resize behind
```

The bordered panel ends up the right size (its size is written directly),
which is why the panel chrome looks correct in the captures; the inner
content panel keeps a height derived from the PRE-clamp window, and
`CraftingPlanView` subtracts its top strip from that. Nothing reads the
region again, so the short viewport is permanent. A drag - the jiggle -
writes `Size` from the mouse handler, outside any layout pass, so the skip
does not apply and every panel converges.

The skip itself is not permanent, which is the detail that made this hard
to see: `UpdateLayout` leaves `LayoutState` at `Invalidated` when it skips,
and `Control.Update` calls `UpdateLayout` every frame, so the bordered
panel's own region is correct again on the next frame and its chrome draws
at the right size. What is permanent is the SIZE that was computed from the
stale region and written onto the child, because nothing recomputes it.

The window's own `ContentRegion` is not part of the problem and is still
read directly: `WindowBase2.OnResized` assigns it from `Width`/`Height`
synchronously before raising `Resized`, so it cannot be a pass behind.

### The fix

`Services/PanelChromeMath.cs` (new, Blish-free, 132 lines) mirrors
`Panel.RecalculateLayout`'s inset derivation and is fed Blish's own public
`Panel` constants by `ViewAdapter`, so the vendor numbers are not
duplicated. `ViewAdapter` computes the bordered panel's size from the
window's content region and the inner panel's size from the size it just
assigned - it no longer reads any `Panel.ContentRegion` back. The trigger
is fixed rather than papered over: no synthetic resize, no deferred
"re-measure next frame" pass, and the first paint is correct because the
arithmetic never depended on a layout pass having run.

Both flow paths use the same two helpers, so `Build` and the resize handler
cannot drift.

Every tab shares the container and therefore shared the defect - Crafting
Plan, Snapshot, Ranker, Plan History, Log, Settings, About all size
themselves from `container.ContentRegion` in their own `Build`. All are
fixed by the one change, and none of their files are touched: the container
they are handed is a bare `Panel` (no title, no border), whose content
region legitimately IS its size, so their reads were never the problem.

### Regression coverage

`tests/GW2CraftingHelper.Tests/Services/PanelChromeMathTests.cs` pins the
property the bug violated: a titled, bordered panel's content height is
derived from its real chrome (600 -> 557), NOT the panel-sized default
(600) that a not-yet-laid-out Blish panel reports; that a bare panel is the
one shape where the default is the truth (which is what makes the tab
contents' own reads safe); and that the arithmetic floors at 0, since
`Control.Size` ignores a negative component outright and would leave a
child at its previous size - the same stale-size failure reached from the
other end.

### Validation

- `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - 0 warnings, 0 errors.
- Same, `-c Release` - 0 warnings, 0 errors.
- `dotnet test tests/GW2CraftingHelper.Tests` - all green, +12 cases over the
  branch point.
- CI invariant scripts run locally: ASCII-only, no em-dash, tests
  Blish-free, csproj Compile list matches disk, budgets, public-surface pin,
  markdown/KNOWN-ISSUES citation resolution.

Gate: NOT RUN - no live game session was available while this branch was
written (neither Gw2-64 nor Blish HUD was running). The failing state is
recorded in the three captures named above; the confirming capture is the
same window opened fresh, with the sections below Total Cost drawn without
a resize.
