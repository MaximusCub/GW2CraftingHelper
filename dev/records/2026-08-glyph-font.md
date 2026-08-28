> **Milestone record - 2026-08-27, branch `module-button-glyphfont`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## A module-owned button and a shipped glyph font (module-button-glyphfont)

Two deliverables that are really one: the module could not put a glyph on a
button, and it had no glyph to put there. The research behind both is
`/mnt/c/Dev/Blish/glyphs/spec.md` (out of repo, 817 lines, measured against the
installed Blish HUD 1.3.0 binaries and assets).

### Why the button subclasses StandardButton rather than Control

Blish's `StandardButton` has four limits, all of them measured out of the
decompiled 1.3.0 binary:

1. It exposes no `Font`, and draws in `DefaultFont14`. So no button could sit
   on this module's own type ramp, and none could carry a glyph from a font
   the module ships.
2. `Paint` assigns `_textColor` on **every frame**, so a text colour written
   from outside is overwritten before it is ever drawn.
3. It blits `Icon` with no tint, onto button art whose face samples about
   (200,193,175). Blish's own white affordance textures - 733269/733270, the
   matched X pair - are therefore invisible on a button. This is the measured
   reason `PlanHistoryTabContent` reached for a `Checkbox` instead.
4. With no text, it seats the icon at `Width / 2 + 8 - iconWidth - 4`. The `+8`
   is a text gap being paid for when there is no text, so an icon-only button
   is 4px right of centre at every width, by construction.

All four live in `Paint` and `RecalculateLayout`, and **both are virtual**.
Everything above them - the hover tween through the `common/button-states`
atlas, the click event and its `Enabled` gate, the tooltip plumbing every one
of this module's 22 buttons relies on, focus, opacity, the Container/Control
lifecycle - is inherited free, and is exactly the part that would have to be
rebuilt, and kept rebuilt against future Blish releases, by a control derived
from `Control`. The button art is Blish's own and both textures are reachable
through the public `GameService.Content.GetTexture`, so repainting the face
ourselves costs two texture handles and no fidelity.

So: override two methods, keep the class name and the file
(`Views/Rendering/FeedbackButton.cs`), and give the new properties defaults
that reproduce StandardButton's rendering exactly. Every existing call site is
a drop-in with a zero-line diff, and no new type reaches the public surface.

The one default that is *not* a no-op is `Font = UiFonts.Caption`. That is the
same face `DefaultFont14` resolves to, so nothing moves on screen; what changes
is that `UiFonts`' own claim - "anything measuring one of those controls
measures in Caption" - is now true by construction rather than by coincidence.

### The glyph font

`ContentsManager.GetBitmapFont` throws `NotImplementedException` in 1.3.0 and
Blish rasterizes no TTFs at runtime, so the font is a BMFont we author and
package: `ref/glyphs.fnt` plus `ref/glyphs_0.png`, assembled at load through
MonoGame.Extended 3.8.0's public `BitmapFont` / `BitmapFontRegion` /
`TextureRegion2D` constructors - the same shape `BitmapFontReader` builds when
it inflates one of Blish's own XNB faces.

**The merge is the load-bearing decision.** A sort indicator is part of the
header's own `Label.Text` (`SortableHeaderLabel.Decorate`), which is what lets
nine call sites' worth of right-alignment and column arithmetic keep working
without knowing an indicator exists - they right-align off a width that already
includes it. A `Label` has exactly one `Font`. So `UiFonts.ColumnHeader` is now
Menomonia Bold 20 **merged with** our glyphs in one font, rather than either
font alone. Every existing `MeasureString` keeps measuring the whole string
correctly and no call site learns anything new.

MonoGame exposes no way to enumerate a `BitmapFont`'s character map -
`GetCharacterRegion` is the entire public surface - so the merge probes the
whole BMP once per merged face at load. 65,536 dictionary lookups, cached.

**Two glyphs ship, not fourteen.** The shortlist this font was scoped against
was carets up/down/left/right, a sortable-but-unsorted chevron pair, check, X,
filled and hollow dots, plus/minus, reorder arrows and a warning triangle.
Rendering them cut it to two, on a measurement worth recording: Bootstrap draws
on a 16px grid with roughly 1-unit strokes, and this UI has room for 6-10px of
ink. A stroked icon at that size lands at well under one pixel of coverage -
`x-lg` at 8px measures a 0.66px diagonal, **paler than Menomonia's own solid
U+00D7**, and `check-lg`, both chevrons, `plus-lg` and `dash-lg` all fail the
same way. Only flattened fills survive the trip, which is what the two carets
are. `tools/build-glyph-font.py --preview` prints the coverage as ASCII art and
is how to re-run that judgement before adding a glyph.

The disqualified entries are not a backlog. Any of them wanting a seat should
be reconsidered as a texture (spec section 6 catalogues Blish's own affordance
art, already reachable by asset id) rather than as a glyph.

### What moved, and what deliberately did not

Moved: the sort indicators, at all nine call sites, via the two constants in
`Services/UiGlyphs.cs`. They were `"^"` and `"v"` - a circumflex accent (10x7
ink, 3px down the line box, 8px advance) beside a lowercase letter (11x11 ink,
6px down, 9px advance). Mismatched in height, in advance and in seat, because
Menomonia has no symmetric up/down pair anywhere in its 226 codepoints. That is
the case that justifies the font existing, and it is now a matched pair pinned
by assertion.

Not moved, deliberately:

- **The five controls fixed on `glyph-fixes`.** The Ranker's reorder and remove
  seats are `Image` controls wearing the game's own art (155953, 733269), and
  Plan History's pin is a `Checkbox`; both are better answers than a glyph, and
  both were verified on screen. Plan History's delete carries U+00D7, and
  the measurement above says `x-lg` at that size would be a *downgrade*, not an
  upgrade. Nothing here is a clean win, so nothing here moved.
- **The tree carets** (`TreeRowShapePlanner`), which are ASCII `v` and `>` in
  their own fixed-width column. They render, they are standalone rather than
  inline, and they are pinned by a 40KB golden sweep. A texture is the better
  answer there anyway - Blish's house pattern for every expand/collapse
  affordance is one texture rotated 90 degrees.
- **The `+`/`-` row buttons**, for the stroke-weight reason above.
- **The sortable-but-unsorted indicator.** It would fix a real documented gap -
  `SortableHeaderCells` notes that its hover wash alone is insufficient for a
  column showing no mark - but it puts an indicator in every header of every
  table at all times, which moves the Amount and Source band geometry of four
  tables. That is a separate change with its own screen gate.

### The metric seam

`Services/GlyphFontDescriptor` is a Blish-free parser and the arithmetic that
places a glyph: `BaselineAlignedYOffset` for a glyph merged into a line of
Menomonia, `BoxCentredYOffset` for a control whose whole label is one glyph,
plus `AdvanceOf` and `MeasureRun`. `Views/Rendering/GlyphFont` is the only file
that touches both those numbers and a `Texture2D`.

`GlyphFontDescriptorTests` reads the **shipped** `ref/glyphs.fnt` - linked into
the test output by the test csproj, not copied - and pins: the atlas carries
exactly the codepoints `UiGlyphs` names; the pair is symmetric in width, height,
advance, x-offset and y-offset; the two directions advance and measure
identically, which is what stops a table's columns jumping on the second click
of a sort; and, merged into Bold 20, the ink lands inside the cap band and
above the descender floor, so no header band grows.

Parsing is strict: a glyph rectangle reaching past its own page, a zero-area
glyph, a duplicate codepoint, a missing `common` record or a missing page all
throw rather than yielding a partial font. A half-loaded glyph font is the
exact failure this whole exercise exists to stop.

### Degraded path

A corrupt install whose `ref/` files are missing gets a Warn line and the ASCII
pair back, through `UiGlyphs.AsciiFallback` at the one seam that knows both the
indicator and whether the font exists. Worse typography, no lost information -
as against a header that silently loses its only sort mark, because a codepoint
with no region draws nothing **and** advances zero pixels.

### The CI tripwire

The `UI glyph escapes exist in the shipped font` step now guards two fonts. It
cannot tell which one a string is drawn in - a codepoint in a `.cs` file says
nothing about the `Font` its `Label` was given - so the rule is **scoped rather
than widened**: our PUA codepoints are legal in `Services/UiGlyphs.cs` and
nowhere else, and are checked against `ref/glyphs.fnt` itself rather than
against a list beside it. Allowing them anywhere else would let one be pasted
into a string a Menomonia `Label` draws, and it would vanish exactly the way
the original five did.

It also runs the check backwards: a glyph in the atlas that `UiGlyphs` does not
name fails the build. Every glyph is licence surface and a vocabulary nothing
re-measures when it drifts.

### Licence

Bootstrap Icons, MIT, sourced from `icons/*.svg` rather than from the built
webfont so the provenance of the rasterized artwork stays a plain MIT copy
question with no OFL "changing formats" argument to have. `ref/THIRD-PARTY-
NOTICES.txt` carries the notice, and the packaging claim was verified rather
than assumed: `BuildBlishHUDModule`'s `ref\**` glob puts all three files in the
`.bhm` with no csproj change, confirmed by unzipping the Release artifact.

---

## Amendment - 2026-08-28 (branch `ranker-columns`): the reading-size caret trio

The maintainer asked whether the atlas could retire the remaining ASCII
affordances: *"now that we have some glyph things to work with are we able to
use those instead of v ^ and > in other places?"*

**Verdict: yes, measured.** Three rows added to `tools/build-glyph-font.py`'s
`GLYPHS` table, regenerated with `--fetch --preview`:

| Codepoint | Bootstrap icon | ink | advance | coverage |
|---|---|---|---|---|
| U+E102 `CaretUp` | `caret-up-fill` | 12x8 | 13 | solid, peak alpha 255, no sub-pixel edge |
| U+E103 `CaretDown` | `caret-down-fill` | 12x8 | 13 | solid |
| U+E104 `CaretRight` | `caret-right-fill` | 8x12 | 9 | solid |

They pass for the same reason the sort pair passed and `x-lg` failed: Bootstrap's
carets are **flattened fills**, so scaling down loses area rather than coverage.
The `--preview` ramp shows a solid `@` interior at every row for all three; the
stroke-based icons this font was originally scoped against (`x-lg`, `check-lg`,
the chevrons, `plus-lg`, `dash-lg`) still measure well under one pixel of
coverage at these sizes and are still absent.

**A separate SIZE, therefore separate codepoints.** The sort pair is 9x6 of ink,
authored to sit inside a Menomonia Bold 20 column header's own `Label.Text`
beside a 17px cap. The trio above is authored for body-size seats - a 28px
button and the recipe tree's 18px caret column - where 6px of ink is a speck.
Their `rise` differs for the same reason: `rise` is the ink centre's height above
the BASELINE of whichever face the glyph is merged into, and Bold 20 puts its cap
centre 8px up while Regular 16 puts its own 7px up (`Services/TypeRampMetrics`).

`U+E104` is authored 8x12 rather than 12x8 so it carries the **same ink area** as
its expanded partner. Sized by height it came out 5x8, which read as the lighter
of the two states on a toggle where both states have to feel equal.

### Seats replaced

| Seat | Was | Now |
|---|---|---|
| Recipe tree expand/collapse column | `"v"` / `">"` | `UiGlyphs.ExpandCaret` |
| Crafting Plan section headers | `"v"` / `">"` | `UiGlyphs.ExpandCaret` |
| Crafting Ranker reorder buttons | 155953 art on a bare `Image` | `UiGlyphs.CaretUp` / `CaretDown` on a `FeedbackButton` |

The first two draw in a THIRD font: `UiFonts.BodyGlyphs`, Menomonia Regular 16
merged with the atlas. Merged rather than standalone because those two seats sit
on layouts measured against Body - the merge inherits Body's line height, letter
spacing and baseline exactly, so the caret labels kept every y and every band
height they already had and no golden moved. `TreeRowShapePlanner.CaretGlyph`
therefore stays ASCII: it is the token the seat degrades to, not the string that
is drawn, which is what keeps `tree-row-shape-sweep.txt` byte-identical.

The Ranker's buttons draw in `UiFonts.Glyphs` standalone, which centres ink in
the line box rather than seating it on a baseline - what a button with no
neighbouring text wants. That seat is only possible at all because
`FeedbackButton` (PR #210) gave a `StandardButton` a `Font`: an up/down pair
needs two symmetric triangles and the one face Blish ships has none.
