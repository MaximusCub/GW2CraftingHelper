## Font bump and decision-round polish (font-and-polish)

Four maintainer decisions from the same field-test round, taken as four
commits. The first is the module-wide type change the
minimum-window-width research had been holding open; the other three are
small, independent fixes to things the field test tripped over.

### 1. The +2pt bump ("do it")

Body text moves **Menomonia 14 -> 16** and small/caption/pill text
**12 -> 14**. Title (18) and the plan's display title (32) are unchanged.

`Views/Rendering/UiFonts` names the four sizes by ROLE - `Body`,
`Caption`, `Title`, `Display` - and is now the only place
`GameService.Content.DefaultFontNN` is read from anywhere under `Views/`.
That is the point: the previous size decision was spread over ~60 call
sites plus every Label that silently took Blish's own default, so
"is the module consistent?" was not a question anyone could answer by
looking. It is now `grep -rn DefaultFont Views/` returning comments only,
and `grep -rn "new Label" --include=*.cs .` outside `Views/` returning a
single site (`Module.BuildPlaceholder`) that names a font too.

- **50 Labels were taking Blish's DefaultFont14 default** rather than
  setting a font. Under the old scheme that was invisibly correct; under
  the bump it would have left a third of the module one size behind.
  Every one of them now names a font. The 50th is
  `Module.BuildPlaceholder`'s "Coming Soon", the body of the Plan History
  and Crafting Ranker tabs - live UI that a `Views/`-scoped grep does not
  reach.
- **Four control types are deliberately excluded and stay at Blish's
  own DefaultFont14**: `Checkbox` and `StandardButton` (which
  `FeedbackButton` derives from) expose no `Font` property at all, and
  `TextBox`/`Dropdown` have internal padding Blish authors against its
  default while holding typed values rather than module prose. Anything
  MEASURING one of those four measures in `Caption`, which is the size
  they actually paint - `MainView.MeasureCheckboxWidth`,
  `SettingsCurrencyGridLayout.CellClearWidth` and `ModalDialog`'s button
  sizing all say so at the point of use, and
  `SettingsCurrencyGridLayoutTests` carries a second char-width bound for
  exactly those controls. The same reasoning covers text the module never
  builds a control for at all: `TooltipTextFormat`'s line budget sizes
  Blish's own `BasicTooltipView`, so it is measured at Font14 too.

#### Measured font metrics behind every re-derived constant

Taken by parsing the installed
`C:\Blish.HUD\Content\fonts\menomonia\menomonia-{12,14,16,18}-regular.xnb`
(MonoGame.Extended `BitmapFontReader` XNB, uncompressed) and measuring
with MG.Extended's own advance / `XOffset+Width` rule - the same method
`docs/research/minimum-window-width.md` used, re-run and cross-checked
against that report's published figures before being trusted (it
reproduces the report's pill-run table exactly: 222/198, 242/218,
436/406, 482/452, and its 174px `Thermocatalytic Reagent`).

| size | line height | lowest ASCII ink, past the line box | `M` | `w` | `0` |
|---|---|---|---|---|---|
| 12 | 13 | +3 | 11 | 11 | 8 |
| 14 | 18 | +1 | 13 | 13 | 9 |
| 16 | 20 | +1 | 15 | 14 | 10 |
| 18 | 20 | +3 | 16 | 16 | 11 |

Real strings measure **1.10-1.11x** wider at 16 than at 14 (730 -> 810,
174 -> 192, 263 -> 292), not the naive 16/14 = 1.143.

#### Constants re-derived (old -> new, and on what basis)

| constant | old | new | basis |
|---|---|---|---|
| `WindowSizing.MinWindowWidth` | 1436 | **1478** | the research's +2pt variant (measured at Menomonia 16, not scaled), plus one `TreeIndentPer` of vendor-leaf headroom, plus the widest-digit rather than example-digit cost column (see below) |
| `TooltipTextFormat.LineBudgetChars` | 75 | **71** | NOT a body-bump consequence: this budget sizes text Blish renders itself, in its own `BasicTooltipView` at DefaultFont14, which the module has no seam to re-font. Re-measured at **Font14** over every >=55-character prose string the module builds (73 of them): 7.03px/char average, so Blish's 500px cap is 71 characters, not the 76 the shipped 6.5px/char estimate assumed |
| `SnapshotItemGridLayout.MaxCharWidthPx` | 8 | **9** | item names measure ~8.4px/char at Font16 (192px over 23 characters), rounded up |
| `SnapshotItemGridLayout.MinColumnWidth` | 464 | **516** | derived: `40 + 52*9 + 8`. Two columns at 1158px, three at 1674px |
| `SettingsCurrencyGridLayout.CellNameWidth` | 170 | **190** | 170 x 1.11, so the same currency names still fit before ellipsis |
| `SettingsCurrencyGridLayout.CellTagWidth` | 100 | **110** | "default 3600" measures 98px at Font16; keeps the ~11% slack the 100px slot gave its 89px at Font14 |
| `SettingsCurrencyGridLayout.CellClearWidth` | 74 | **74** | unchanged - it sizes a `Checkbox` label Blish keeps at Font14 |
| `SettingsCurrencyGridLayout.MinColumnWidth` | 424 | **454** | derived from the three above. Two columns need a 908px panel (a 1034px window), clearing the 1478 minimum by ~444px |
| `PlanContentHeightMath.CTableHeaderRowHeight` | 26 | **28** | header label at `LabelY` 5, lowest Font16 ink y=26 - exactly the old band |
| `PlanContentHeightMath.DisciplineRowHeight` | 32 | **36** | two labels at y=7/y=9, ink y=28, divider top was y=29. 36 is what every other single-line table row uses and is on `CreateRowDivider`'s proven-immune list |
| `PlanContentHeightMath.RecipeRowHeightWithSublabel` | 44 | **48** | name line box 18 -> 20 pushed the sublabel y=22 -> 24, and the sublabel's own font grew: ink y=43 against a divider at y=41 |
| `RecipesSectionRenderer` sublabel y | 22 | **24** | sits directly under the name's new 20px line box |
| `TopRegionLayoutMath.StatusToSeparatorGap` | 21 | **23** | the plan status label's Font16 ink landed exactly on the separator |
| `SummarySectionLayoutMath.CostBandCaptionLineHeight` | 20 | **25** | caption font 12 -> 14, measured line height 13 -> 18; keeps the same slack over the real metric |
| `SummarySectionLayoutMath.CostBandCurrencyNoteHeight` | 18 | **23** | same +5 |
| `SummarySectionLayoutMath.CostBandHeight(false/true)` | 68 / 86 | **73 / 96** | falls out of the two above |
| `TreeSectionController.PillHeight` | 20 | **24** | the pill label sits at y=2 in an inset panel of `PillHeight - 2`; its Font14 ink is y=21 against an 18px interior |
| `LabelHelpers.SmallTagHeight` | 18 (literal) | **22** (named) | same shape one level out; promoted to a constant because two call sites centred a tag with a hand-repeated `- 18` |
| `MainView.ItemRowHeight` | 52 | **56** | name line box ends y=24, so the breakdown moved y=24 -> 26 and its ink y=43 -> 47; keeps the old 9px bottom slack |
| `MainView` breakdown line y | 24 | **26** | as above |
| `SettingsTabContent.CurrencyRowHeight` | 30 | **32** | cell labels at y=6, Font16 ink y=27, divider top y=27 |
| `SettingsTabContent.CurrencyHeaderRowHeight` | 24 | **26** | header labels' Font16 ink y=25 |
| `SettingsTabContent.InfoRowHeight` | 20 | **22** | info line at y=2, Font16 ink y=23 - same 1px overhang 20 gave Font14's y=21 |
| `AboutTabContent.InfoRowHeight` | 20 | **22** | same site shape (`AddLabeledInfoSection`'s fixed-height heading panel) |
| `CraftingPlanView` section header font | Font16 | **Font18 (`Title`)** | Body moved onto the 16 this header sat at, flattening the page to one level. Font18 no longer collides with the plan title (Font32) and matches what Settings/About already use |
| `CraftingPlanView.SectionHeaderRowHeight` | 30 (literal) | **32** (named) | the promoted Font18 title's ink is y=28 against a divider at y=27 |
| `ModalDialog.WindowHeight` | 170 | **190** | the message is capped to whole lines of the body font; +20 is exactly one Font16 line, taking the cap from three lines back to four to pay for ~11% wider text |

**Deliberately unchanged, each for a stated reason** (recorded beside the
constant, not just here):

- `PlanRelayoutMath.TreePillColumnWidth` stays **256**. The research's
  four-pill `CRAFT/TP/VENDOR/IGNORE` run measures **242px** at Font14
  against the **252px** budget a 256px column leaves, so it still fits at
  normal padding. The `CURRENCY` + `HAVE n/m TOTAL` run does now need the
  tightened-padding pass (263 normal / 251 tight), which
  `ComputePillFit` already applies unprompted; the `HAVE n/m NEEDED`
  annotation overflows to `+N` as it always did.
- `UsedMaterialRowHeight` / `ShoppingRowHeight` /
  `RecipeRowHeightNoSublabel` (36) and `TreeRowHeight` (40) are
  **icon-driven**: a 34px rarity frame plus a 2px divider already exceeds
  the tallest text run in them.
- `CraftStepRowHeight` (44): its body text was **already Font16** before
  the bump, so only its Font12 -> Font14 sublabel moved, and that
  sublabel's new ink (y=35) still clears its divider at y=41.
- `CurrencyRowHeight` / `FallbackTextRowHeight` (28): a single line at
  y=4 (ink 25) or y=7 (ink 26), with no divider beneath either.
- `CostTileRowHeight` (56): its amount is bottom-anchored by
  `BandAmountY` and its caption block bottom, at the grown caption
  metric, still lands above it (24 vs 30).
- `SnapshotHeaderLayout.StatusRowHeight` / `LogTabContent.StatusRowHeight`
  (24): status labels sit at y=2, Font16 ink y=23.
- `MainView.WalletRowHeight` (36): icon-driven (32px icon at y=2, plus
  2), and its single Font16 line's ink (y=27) sits well inside.
- `SuggestionPanel.RowHeight` (28): its one line is now centred on the
  font's OWN `LineHeight` rather than on a hand-tuned 16, so the offset
  moves with any future size change instead of stranding a descender on
  the next row's top edge (these rows stack flush and opaque).
- `LogTabContent`'s row metrics are **measured from the font at runtime**
  (`Measure(font, "Ag").Height + 2`), so they moved on their own.
  `NotesSectionLayoutMath` likewise takes a measure function.
- `ApiAccessDialog.WindowHeight` (300): **inferred, not measured** - by
  line count its three checks wrap to six lines at both sizes, putting
  the button line near y=193 inside a ~255px content region, i.e. ~60px
  of headroom. Gate item 1 is what actually confirms it.

#### Modelling honesty on the 1478 figure

The window minimum is **measured for the fonts and inferred for the
chrome**, exactly as it was at 1436: the 126px window-to-panel chain has
one ~8px term (Blish's `Panel` border) taken from this repo's own comment
rather than a decompile, so the whole figure carries +/-2px there. That
is the **only** uncertainty in the figure.

All three deepest-row constants in `PlanRelayoutMathTests` are direct
measurements in the one convention the production code uses -
MonoGame.Extended's advance / `XOffset+Width` rule, which is what
`TreeSectionController`'s `nameFont.MeasureString` and
`TreeCostColumnMath`'s pre-scan compute, and the rule that reproduces the
research's `65` for `4194304x ` and `174` for `Thermocatalytic Reagent`:
`DeepestRowQtyPrefixWidth` 65 -> **73** and `DeepestRowNameWidth`
174 -> **192**. So the depth-23 row's designed **24px gutter** and the
depth-24 vendor leaf's exact zero-gutter fit are measured facts, not
approximations.

`DeepestPlanCostColumnWidth` 165 -> **181** is the constant that moved
the window minimum past the research's own +2pt prediction, and it is
worth stating why. Menomonia's digits are **not one width**: at Font16
`0` advances 10px and inks 12, `2` and `7` advance 10 and ink 11, `1`
advances 6, and every other digit advances 9 and inks 11. A run's
measured rect is the leading digits' advances plus the last digit's ink,
so the widest run is drawn from `0`/`2`/`7` and ends in `0` - all-twos
is 3px short of it, all-nines 10px short.
The cost column is the three digit runs' measured widths plus 78px of
fixed chrome (`TreeCostColumnMath.SegmentWidth` = text +
`CoinLabelIconGap` 2 + `CoinIconSize` 20, three segments, two
`CoinSegmentGap` 6 between them), so a six-digit gold total plus two
two-digit units measures:

| gold digits | Font14 | Font16 |
|---|---|---|
| all nines (or 3/4/5/6/8 - one advance class) | 161 | 171 |
| all twos (or sevens) | 168 | 178 |
| widest run: `0`/`2`/`7` ending in `0` | 171 | **181** |
| the research's live example, ~174,000 gold | 166 | 176 |

The constant is now the **worst case**, 181, and the minimum is derived
from it - 1472 -> **1478**. A figure taken at any one example total is
light for a plan whose gold happens to run wider: the withdrawn 175 (an
all-nines figure) by 6px, the live example's 176 by 5px. That is exactly
the kind of digit-choice artifact this section already withdrew once; at
1472 such a plan would have spent
the depth-24 vendor leaf's headroom and cut the depth-23 gutter to 18px.
There is no residual term here to trade against the +/-2px chrome
uncertainty: the constant covers every total the module can price.

An earlier draft of this section recorded two other caveats - a Font16
quantity prefix of 76-77 and a "3px cost-column convention gap". Both
were artifacts of summing `xAdvance` instead of measuring the inked rect,
which is not what either call site does. They are withdrawn rather than
left to send a future maintainer chasing a gap that is not there.

`docs/research/minimum-window-width.md` derived the original Font14 cost
column as `76+6+40+6+40 = 165`, whose components in fact sum to 168 (and
to 171 measured on the inked rect the renderer uses). The report's
arithmetic is corrected in place; the shipped 1436 minimum was derived
from the slipped 165 and is superseded by this section either way.

### 2. The one-letter empty-state hint ("add a hint")

`SnapshotSearchResultBuilder` holds character-name matching back below
`MinCharacterSearchLength` (2) for a good reason - one letter surfaces
everything a character whose name contains it holds, so the opening
keystroke of an item search would widen the list instead of narrowing it
- but the hold-back is invisible: the list comes back empty and reads as
a broken search.

`ShortQueryCharacterHint` returns one extra line for the "No items
match ..." message on **exactly** the case the rule caused: a query
shorter than the minimum whose next keystroke really would match a roster
name. It is silent on a two-letter query (that already searches character
names, so an empty list there is a genuine no-result and the hint would
be a lie), on a one-letter query no roster name carries, on a blank query,
and with no roster at all. It takes the unchecked-character set so a
character the source filter has excluded never triggers a promise the
filter cannot keep. It names no character and no id. MainView appends it
on the items branch only - the Wallet filter has no character matching at
all. Seven Blish-free cases, one of which drives the real `BuildItemRows`
path at one and then two letters to show the hint's premise is true.

### 3. The background-refresh spinner ("use spinner")

Only a clicked Refresh Now turned the inline spinner; the timer-driven
auto-refresh (`Update()`'s staleness gate, `OnSubtokenUpdated`, module
load) ran silently.

`Module` carries a `volatile _backgroundRefreshInFlight` around
`RefreshSnapshotInBackgroundAsync`'s body, set **past** both early
returns so a tick that declines to refresh (already running, or inside
the failure backoff) never spins over nothing. `Update()` drains it to
the view on change - the same dirty-flag shape `SaveStatusThreadSafe`
already uses for status text, because the `finally` may resume on a
ThreadPool thread. The drain sits ABOVE `Update()`'s
`if (_refreshInProgress) return;`, or it could only ever switch the
spinner on after the refresh it belongs to had finished, and it only
marks a value applied once a view existed to receive it, so the
module-load refresh is not lost against a null `_snapshotContent`.

`MainView` keeps **two** flags and shows the OR of them. One shared flag
would let a Refresh Now clicked DURING an auto-refresh switch the running
refresh's spinner off: `UserRefreshAsync`'s own `_refreshInProgress` gate
returns null immediately in that case and MainView's `finally` runs at
once. `_refreshInProgress` remains the gate on whether a refresh may
START and is deliberately not reused for this.

Spinner only, not the status text: the user did not ask for this refresh,
so replacing the timestamp they are reading with "Refreshing..." is a
surprise rather than feedback - and the background path's cancellation
arm writes no status that would restore the label afterwards.

### 4. Sort reset on a new plan ("reset to defaults when you gen a new plan")

`ResetPerPlanSortState` clears both sortable tables to `None` at
`TriggerGenerate`'s commit point, beside the existing
`_sectionExpansion.Clear()`. That point is precisely what distinguishes a
new plan from a re-render of the same one: a re-sort, a tree pill
override and a re-solve all re-render through `RenderPlan` without ever
reaching it, so they keep the sort exactly as before - which is the
behaviour the sortable-tables round deliberately built and which stays.

One method rather than two calls at the site, so a future third sortable
table cannot be reset in one place and forgotten in another - and it is
called from **all three** sites that clear `_sectionExpansion`
(`TriggerGenerate`'s commit point, `ApplyRestoredPlan`,
`RollBackFailedPlanRender`), so "arriving at a different plan" is one
pairing rather than three independent ones.

Only the first of those three can carry a stale sort today:
`ApplyRestoredPlan` cannot run after a Generate in the same session
(`Module`'s `_generateCompletedThisSession` guards it) and
`RollBackFailedPlanRender` leaves no plan and therefore no sortable table
rendered at all. Both calls are no-ops as the code stands. They are there
because the alternative is a local invariant resting on a guard in
another file: relax or reorder that guard, or add a second restore path
(plan history is on the roadmap), and a restored plan would inherit the
previous plan's sort column and header indicator - precisely the
behaviour this commit removes.

`TableSortState`'s class doc claimed the superseded lifetime and is
corrected. The struck-through claim in the sortable-tables section above
points here.

### Reviewer-scrutiny list

Things a reviewer should look at hardest, stated rather than buried:

1. **`UiFonts` is a new abstraction.** It is justified by the 60+ call
   sites it replaces and by `UiMetrics`' existing precedent in the same
   namespace, but it IS new surface.
2. **43 of the 49 Label font insertions were mechanical.** Each was
   verified to be body prose, not a caption; the ones inside fixed-height
   rows were then checked against that row's ink budget, which is where
   the eight row-height growths came from.
3. **`ModalDialog.WindowHeight` 170 -> 190 is the one growth not forced
   by a clipping calculation** - three lines still fit at 170. It buys
   back the line ~11% wider text can now need.
4. **The 1478 minimum's +/-2px chrome term** is the figure's one soft
   spot, described above rather than papered over. Everything else in the
   chain is measured in the convention the renderer itself measures in,
   and the cost column is taken at its widest digits rather than at an
   example total - which is the 6px that separates 1478 from 1472.
5. **The spinner wiring has no automated coverage.** `Module` and
   `MainView` are Blish-bound; the two-flag OR and the `Update()` drain
   are argued from source and pinned only by desktop gate item 4.
6. **`Checkbox` staying at Font14** is a visible inconsistency in the
   Settings tab and the Snapshot source filters - Blish gives no seam.
   Worth a maintainer look at the gate.
7. **The `CURRENCY` + `HAVE n/m TOTAL` pill run now needs the tightened
   padding pass** where it did not before. Not a regression (the pass
   exists for this), but it is a visible density change on those rows.

### Desktop gate

1. At the **1478** minimum, read a plan and a snapshot end to end on
   every tab. Row text is legibly larger than before and nothing is
   clipped: check the Required Disciplines character line (the reported
   descender site), Required Recipes rows WITH a sublabel, the Summary
   cost band's caption and disclosure line, the tree's decision pills,
   and the Shopping List's source tags.
2. Resize from the minimum outward and back. No row, divider, tag or pill
   overlaps its neighbour at any width, the Snapshot grid still gives two
   columns at the minimum and a third past ~1674px, and the Settings
   currency grid stays two-up.
3. Snapshot tab: type ONE letter that a character's name contains but no
   item's does - the empty-state message carries "Type another letter to
   match character names." Type a letter no character carries, and a
   two-letter query, and confirm the line does NOT appear.
4. Leave the module past the snapshot refresh interval with the Snapshot
   tab open: the spinner appears beside the status label for the whole of
   the automatic refresh and stops when it lands. Click Refresh Now while
   that automatic refresh is running and confirm the spinner keeps
   turning rather than stopping early.
5. Sort Used Materials by Amount, then Generate a new plan: the header
   indicator is gone and the rows are in the plan's own order. Re-sort,
   then click a tree decision pill (re-solve) and confirm the sort and
   its indicator SURVIVE that.

Gate: PASS (2026-08-23 desktop session, branch build at the fix HEAD,
captures preflight/gFP5-gFP36), with two sub-cases left to the live
install. (1) READING PASS at the effective minimum: every tab read end
to end - snapshot rows and source tags, tree pills including the "+2"
overflow run, the Required Disciplines character line, Required Recipes
rows WITH sublabel (zoomed: name descenders and sublabel both clear of
the 48px row's divider), the cost band caption, Shopping List tags,
Settings currency grid two-up, About prose, Log rows - nothing clipped
or overlapping. (3) HINT: fixture gained "Quiet Quinn" (only q-carrying
name); "q" produced the empty state WITH "Type another letter to match
character names.", "j" produced it WITHOUT the hint, "qu" listed Green
Wood Log via the character match with no hint. (5) SORT RESET: Shopping
List sorted by Amount showed the indicator; a fresh Generate (Mystic
Tribute, live prices) cleared it and returned plan order; Used
Materials re-sorted ascending (Coin 25x before Clover 35x), and a
VENDOR pill re-solve ("Decisions updated (1 override(s))", list 36->33)
kept the sort, its indicator, and the scroll position. (4) SPINNER:
partially machine-verified - Refresh Now showed "Refreshing..." with
the inline spinner turning and both buttons disabled 300ms in, and the
failure landed with a fresh status timestamp, spinner stopped, buttons
re-enabled. The AUTOMATIC path never starts a refresh in the keyless
sandbox (the API-ready guard skips the timer attempt), so
spinner-during-auto and the overlap click stand on the shared two-flag
wiring plus the manual-path evidence; confirm on a live install. (2)
RESIZE: the client-growth path re-clamped live (window grew from the
1064-client floor to the full minimum when the client widened; the
sandbox renders at Blish UI scale 0.81, so 1478 logical = ~1197
physical, measured against the search-row constants); grip drag-resize
remains synthetically uncatchable (longstanding), so
outward-and-back dragging and the 3-column snapshot threshold are the
user's live checks. Bonus: the Clear Cache ModalDialog message wraps
un-clipped with Caption-measured button padding, the ApiAccessDialog
stacking case renders as recorded, and both plan generations logged
single [plan] tags.

---
