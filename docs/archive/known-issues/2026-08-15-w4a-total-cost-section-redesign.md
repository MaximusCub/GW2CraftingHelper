## W4A: Total Cost section redesign (2026-08-15)

User-designed spec (the user personally iterated on this layout before
handing it off). Implemented in the isolated `wt-cost` worktree off
`origin/master` (`727c90b`) on branch `cost-section-redesign`.

**1. Two formula bands, replacing the old flat cost-tile row
(`Services/PlanViewModelBuilder.cs` `BuildCostFormulaBand`/
`BuildProfitFormulaBand`, `Views/Rendering/SummarySectionRenderer.cs`
`CreateFormulaBand`).** Band 1 reads "Total Materials Value - Your
Materials Used = Actual Cost to Craft"; Band 2 (only when
`CraftingPlanResult.NetSaleValue.HasValue`) reads "Sell Value - Total
Materials Value = Profit/Loss if Sold". Actual Cost to Craft and Sell
Value/Profit are exactly the pre-existing `TotalCoinCost`/`NetSaleValue`/
`CraftingProfit` math, untouched; Total Materials Value is new, computed
for display only. COLLAPSE RULE (user-mandated): Band 1 collapses to a
single "Actual Cost to Craft" tile when `MaterialOpportunityCost` is null
or 0 - the formula is meaningless with no middle term. Both bands render
through the same `CreateFormulaBand` tile-row geometry the pre-W4A
`CreateCostTileRow` already used (`PlanRelayoutMath.
ComputeCostTileGeometry`, unchanged), just called once per band instead
of once over every coin row flattened together - two bands now render as
two stacked tile rows, not one wider one.

**2. Band 2's identity was verified, not assumed - and does NOT
universally hold.** The task's own instruction was to verify
`CraftingProfit == NetSaleValue - TotalCoinCost - MaterialOpportunityCost`
before wiring Band 2's middle tile to `TotalCoinCost + MaterialOpportunityCost`
(Band 1's formula). Reading `Services/SellSideEconomics.cs` end to end:
the identity holds exactly for a single-item plan
(`ApplySellSideEconomics`, `profit = NetSaleValue - solveResult.Plan.
TotalCoinCost - materialOpportunityCost`) but explicitly NOT for a
multi-item batch - `CraftingPlanResult.CraftingProfit`'s own doc comment
states the batch cost subtracted is "NOT Plan.TotalCoinCost, which also
includes every requested root that has no live sell price" (i.e. every
unsellable root in the batch). Using `TotalCoinCost + MaterialOpportunityCost`
for Band 2 would therefore show a middle tile that does not arithmetically
balance the visible Sell Value/Profit numbers for a multi-item batch with
any unsellable root. Fixed by deriving Band 2's Total Materials Value as
`NetSaleValue - CraftingProfit` instead - reusing ONLY the two
already-stored, already-correct fields (never recomputing `CraftingProfit`,
never reading `TotalCoinCost` in this band at all). This is algebraically
IDENTICAL to Band 1's own Total Materials Value for every single-item plan
(proven by `PlanViewModelBuilderSummaryTests.
ProfitBand_TotalMaterialsValueMatchesCostBand_ForSingleItemPlan`), so the
two bands always agree there; for a multi-item batch with a
partially-unsellable root mix the two bands can legitimately show
different numbers under the same "Total Materials Value" label (Band 1
prices the whole batch, Band 2 only its sellable portion, matching what
`CraftingProfit` itself measures) - Band 2's tile carries an extra
tooltip clause for that case rather than silently showing a formula that
would not visually balance.

**3. Mouseover tooltips on every formula-band header (user-mandated).**
`PlanRowViewModel` gained `TooltipText`; `SummarySectionRenderer.
CreateFormulaBand` sets it directly on the caption `Label` control
itself, never on the tile's containing `Panel` - the M32 lesson
(`docs/KNOWN-ISSUES.md`'s "Field-test UX wave", finding D) is that a
label captures the mouse before a container tooltip underneath it would
ever be reached, so the tooltip has to live on the exact control that
receives the hover. Wording matches the spec's exact text for all five
headers; the pre-existing "(buy-order prices)"/"(Nx, ...)"/"(batch
total, ...)"/"(coin costs only)" qualifiers that used to live inline in
row Labels all moved into these tooltips instead, since a formula-band
caption has to stay short to read as a formula.

**4. Currency table replaces the plain-text currency rows
(`SummarySectionRenderer.CreateCurrencyTable`/
`CreateCurrencyTableHeaderRow`/`CreateCurrencyTableRow`).** Columns:
Currency (icon + name) | Required | Have | Needed. The 4-column shape
does not fit `CTableHeaderRenderer`'s left/middle/right (3-slot)
signature, so the header is hand-rolled - the same precedent
`ShoppingListSectionRenderer.CreateShoppingListHeaderRow` already set for
its own 4-column (Item/Amount/Each/Total) header, rather than stretching
`CTableHeaderRenderer` to fit a shape it was not designed for. Column
geometry is new pure arithmetic in `Services/SummarySectionLayoutMath.
ComputeCurrencyColumnEdges` (fixed-width right-to-left columns - Required/
Have/Needed are always short plain integers, no coin icons, so no
per-render widest-value pre-scan is needed the way the Shopping List's
Each/Total columns need one). Rows sort alphabetically by resolved
currency name (user-mandated) via a stable `OrderBy`, not `List.Sort`
(unstable) - two different unknown currency ids both fall back to the
same generic "Currency" name, and an unstable sort could reorder that
tied pair nondeterministically run to run. `Have` is now the RAW,
UNCLAMPED wallet holding (`PlanRowViewModel.CurrencyOwnedQuantity`'s
contract changed from `Math.Min(owned, Required)` pre-W4A to the real
holding - user-mandated); `Needed` (`CurrencyNeededQuantity`, new field)
is `max(0, Required - Have)`; both are null (never a fabricated 0) when
no wallet snapshot exists. Rows where `Have >= Required` get
`CurrencyFullyCovered = true` (new field), rendered as a green "OK" badge
at the row's right edge.

**5. Glyph verification: check-mark vs "OK" badge fallback.** The spec
asked for a green check-mark glyph, with an explicit authorization to
fall back to a green "OK" text badge if the glyph could not be verified
to render in the Blish font, and an explicit ban on color-emoji
codepoints. No live Blish HUD session was available in this environment
to render-test the glyph directly. This module's own prior investigation
(`docs/dev-notes/HISTORY.md`, "Carried follow-up resolved: caret glyphs")
already found that a technically-representable Unicode glyph (a triangle
expand/collapse indicator) was NOT the reliable choice for this exact
font once live-tested across multiple desktop sessions/machines - ASCII
carets were kept instead. Given that precedent and no way to independently
verify a different, also-unverified glyph here, this package takes the
pre-authorized safe fallback: a small green "OK" pill via the existing
`LabelHelpers.CreateSmallTag` helper (same one the tree's Locked/Available
pills and the shopping source tag already use), colored to match
`PillColors.PillKind.Selected`'s green (#1F8F0C) rather than adding a new
`PillKind` for this single non-tree use. **A live desktop check of this
one glyph decision remains open** - if a future session confirms the
check-mark glyph (the escaped form, backslash-u-2713 - see the
ASCII-only-source rule) renders cleanly in this font, swapping it in is
a one-line change in `SummarySectionRenderer.FullCoverageMarkerText`.

**6. Footnote row (user-mandated).** A new `PlanRowType.SummaryFootnote`
row, always exactly one, always last (after the pre-existing multi-item
`MultiItemNote` banner when both are present) - subdued styling
(`DefaultFont12`, dim grey `(130,130,130)`, via a new `CreateFootnoteRow`)
distinct from `MultiItemNote`'s plain `TextRowRenderer.CreateTextRow`
styling, so it reads as fine print rather than plan-specific information.
Text: "Prices are Trading Post data - actual purchase and sale prices are
likely to vary."

**7. Height agreement lives in a new class, not
`PlanContentHeightMath.cs` (DO-NOT-TOUCH for this package).** The
redesigned section's shape (two independently-present tile rows, a
currency table header + N rows, a note row, a footnote row) cannot be
expressed by `PlanContentHeightMath.SummaryBodyHeight`'s pre-W4A formula
(a single boolean "has a coin row" flag good for exactly one
`CostTileRowHeight`, not two independently-gated bands) without editing
that method - and `Services/PlanContentHeightMath.cs`/`PlanRelayoutMath.cs`
were both explicitly DO-NOT-TOUCH for this package (shared infrastructure
several other sections' row builders depend on, plus other in-flight
work touching the same files). Resolution: a new `Services/
SummarySectionLayoutMath.cs` (`BodyHeight`, `ComputeCurrencyColumnEdges`) -
Blish-free, unit-tested, reusing `PlanContentHeightMath`'s existing public
row-height CONSTANTS directly rather than redefining them, only owning
the Summary-specific COUNTING logic. `Views/CraftingPlanView.cs`'s one
real call site (`CreateCollapsibleSection`) now special-cases
`PlanSectionType.Summary` to call `SummarySectionLayoutMath.BodyHeight`
instead of `PlanContentHeightMath.SectionBodyHeight`.
`PlanContentHeightMath.cs` itself has ZERO diff from this package -
its own private `SummaryBodyHeight` method and its existing
`PlanContentHeightMathTests.cs` coverage still compile and still pass
exactly as before, they are simply no longer reached for a real Summary
section. `PlanRowType.CoinTotal` (the enum member that dead method still
references by name) is likewise kept, unused by new code, purely so that
file keeps compiling unmodified - see that enum member's own doc comment.

**8. No per-row divider on the new currency table rows (review
self-catch).** `RowRelayoutHelpers.FinishRow` (the divider-plus-relayout
helper every other c-table row in this file uses) was tried first and
then deliberately backed out: `CurrencyRowHeight` (28px) was never part
of the M36b `Container.Paint` round-trip simulation sweep that
`LabelHelpers.CreateRowDivider`'s own doc comment documents (only 44px/
32px rows are proven vulnerable to the vanishing-divider defect and only
36px rows are proven immune - 28px is neither), and the pre-W4A Summary
section deliberately had no per-row dividers at all by its own original
doc comment. Adding one at an unproven row height, for a visual element
the spec never actually asked for, would have risked resurrecting
exactly the defect DO-NOT-TOUCH #6 (divider math) exists to stay clear
of. Currency rows resize via a plain `AddRelayout` closure instead, with
no divider - the header row's dark background alone delineates the
table, matching gw2e's own header-only table styling.

**9. Review self-catch, then a second self-catch on the first
(adversarial-review fix round): a raw Unicode check-mark character was
never shipped in any `.cs` source file - `Views/Rendering/
SummarySectionRenderer.cs`'s own glyph-decision comment (see item 5)
has always used the properly-escaped textual form (backslash-u-2713),
verified by a non-ASCII grep of every touched `.cs` file before commit
(zero hits). The raw character instead leaked TWICE into this very
markdown file's own prose while drafting items 5 and 9 above - a
record about catching a Unicode paste that itself contained a Unicode
paste, which was also factually wrong about what the `.cs` file
contains (it does not, and never did, carry a checkmark glyph in any
form - `SummarySectionRenderer.FullCoverageMarkerText` ships the ASCII
`"OK"` text badge per item 5). Caught in a later adversarial-review
pass and replaced with plain ASCII description; this file itself now
carries zero non-ASCII bytes, matching its pre-W4A state.

**10. Tests (Blish-free, real `PlanViewModelBuilder.Build` production
path).** `PlanViewModelBuilderSummaryTests.cs`,
`PlanViewModelBuilderSellEconomicsTests.cs`, and
`PlanViewModelBuilderMultiItemTests.cs` were extended/rewritten in place
(same files, same focus, new row shape) rather than duplicated: cost-band
collapse rule (both the null AND the exactly-zero
`MaterialOpportunityCost` case), cost-band arithmetic (`Total Materials
Value == Actual Cost to Craft + Your Materials Used`), profit-band
presence/absence, profit-band arithmetic including the single-item/
multi-item identity divergence (item 2 above), loss sign, tooltip
qualifier placement (buy-order basis, overproduction, batch/coin-costs-
only, all now asserted via `Contains` on `TooltipText` rather than the
old `Label`-suffix assertions), currency-row alphabetical ordering,
unclamped Have plus the derived Needed/FullyCovered fields across
covered/gap/no-wallet-data/wrong-currency-id cases, and the always-present
footnote row. A new `SummarySectionLayoutMathTests.cs` covers `BodyHeight`
(null/empty, collapsed vs. expanded cost band collapsing to the SAME one
tile-row height, both bands stacking to two, currency header+rows,
note+footnote rows, and a full-section combination) and
`ComputeCurrencyColumnEdges` (right-to-left ordering, panel-width
scaling). No Blish HUD/`Gw2Sharp` references in any new/changed test; no
fake file I/O; every assertion drives the real `PlanViewModelBuilder.
Build(CraftingPlanResult)` entry point.

**11. Adversarial-review fix round (2026-08-15) - 7 findings fixed from
an independent code review of this package (5 file-scoped, 2 process-
level).** All fixed in the same `wt-cost` worktree/branch, small logical
commits, before any push/PR:

- **Footnote height desync (Critical-adjacent Must Fix,
  `Views/Rendering/SummarySectionRenderer.cs` `Render`).** The renderer
  kept only the LAST `SummaryFootnote` row (`footnoteRow = row`,
  overwriting) while `SummarySectionLayoutMath.BodyHeight` sums
  `FallbackTextRowHeight` per footnote row it counts - the two agreed by
  coincidence only because exactly one footnote row is ever emitted
  today. Fixed by collecting into a `footnoteRows` List (mirrors the
  pre-existing `noteRows` pattern) and rendering every entry, so the
  renderer and the height math can never desync regardless of how many
  footnote rows a future change emits.
- **Ellipsized currency-name tooltip swallowed (Must Fix,
  `CreateCurrencyTableRow`, both the build path and its `AddReellipsis`
  closure).** The M32 lesson (this file's own "Field-test UX wave"
  finding D) is that a label captures the mouse before a tooltip on a
  control underneath it is ever reached; the currency table's `nameLabel`
  sat directly on top of its own truncated text with the tooltip stamped
  only on the containing `rowPanel`, so hovering the visibly-truncated
  name showed nothing. Fixed by stamping `BasicTooltipText` on
  `nameLabel` AND `rowPanel` in both places. This is a repo-wide pattern
  (confirmed by grep: `Views/Rendering/DisciplinesSectionRenderer.cs`:193/
  220, `UsedMaterialsSectionRenderer.cs`:89/121, and
  `ShoppingListSectionRenderer.cs`:227/284 all stamp the tooltip on the
  row panel only, never on the name label sitting over the truncated
  text) - per the "fix the class, not the instance" directive, the sweep
  is reported here, but those three files are pre-existing, untouched by
  this branch, and outside this package's scope (only the
  `SummarySectionRenderer.cs` instance introduced by W4A is fixed here);
  the same one-line fix applies to each and is left as a follow-up.
- **Formula-band operators never drawn (Must Fix,
  `CreateFormulaBand`).** The band read as three same-shaped tiles with
  no visible "-"/"=" relationship between them - exactly the "two-tile
  split-column band" ambiguity the redesign exists to remove, arguably
  worse (now two such unlabelled bands instead of one). Fixed by drawing
  a small dim `Label` centered on each tile boundary (no tooltip, so it
  never steals hover from a neighboring caption) - `"-"` between every
  pair but the last, `"="` for the last, matching the spec's formula
  text exactly. Never drawn for a collapsed 1-tile band.
- **Band 2's middle tile shared Band 1's caption despite legitimately
  differing (Must Fix, `Services/PlanViewModelBuilder.cs`
  `BuildProfitFormulaBand`).** For a multi-item batch with an unsellable
  root, Band 1's and Band 2's "Total Materials Value" tiles can hold
  DIFFERENT numbers (see item 2 above) with only a tooltip to
  disambiguate - two identically-labeled tiles showing different numbers
  reads as a bug, not a scoping nuance, especially in a section whose
  whole point is now to read as a balancing formula. Fixed by giving the
  multi-item case its own caption, `"Materials Value (sellable)"` (new
  `MaterialsValueSellableLabel` const); single-item plans are unaffected
  (still `"Total Materials Value"`, matching Band 1 exactly, per the
  proven identity).
- **The multi-item divergence itself was asserted only in
  prose/comments, never by a running test (Must Fix,
  `tests/.../PlanViewModelBuilderMultiItemTests.cs`).** New
  `MultiItemRequest_UnsellableRootPresent_ProfitBandMiddleTileDivergesFromCostBand`
  models a batch where `CraftingProfit` is set the way
  `ApplyBatchSellSideEconomics` would actually produce it for a
  partially-unsellable root mix (sellable-root-only cost, never the
  whole-batch `TotalCoinCost`) and asserts Band 1's and Band 2's middle
  tiles both hold their own correct-but-different values, plus the new
  distinct caption above.
- **`CurrencyNumberColumnWidth`'s fixed 60px floor had no widest-value
  pre-scan, unlike the sibling `ShoppingColumnMath` (Must Fix,
  `Services/SummarySectionLayoutMath.cs` +
  `Views/Rendering/SummarySectionRenderer.cs`).** The class doc comment's
  claim that Required/Have/Needed have "no realistic risk of a value
  needing more than a handful of digits" stopped being true the moment
  the Have column was unclamped to the real wallet holding (item 4
  above) - Karma routinely reaches 6-7 digits, which can plausibly
  exceed 60px, and `CreateRightAlignedLabel` grows leftward from the
  column's own right edge, so an unreserved overlong value would
  visually intrude into its left neighbor rather than clip. Fixed the
  same way `ShoppingColumnMath.ComputeEdges` already solves this: a new
  `EffectiveCurrencyNumberColumnWidth(widestNumberWidth)` widens the
  reserved band past the 60px floor when needed, and
  `ComputeCurrencyColumnEdges` gained an optional `widestNumberWidth`
  parameter (defaults to 0, reproducing the exact prior fixed-60px
  geometry for every existing caller/test) that the renderer now feeds
  from a per-render pre-scan of the section's own Required/Have/Needed
  strings - mirrors `ShoppingListSectionRenderer.Render`'s own
  `maxEachWidth`/`maxTotalWidth` pre-scan shape exactly. Five new
  `SummarySectionLayoutMathTests.cs` cases cover the floor/widen
  boundary and prove the default-parameter path is byte-identical to
  the pre-fix geometry.
- **`docs/KNOWN-ISSUES.md` (this file) contained two raw Unicode
  check-mark characters despite claiming, in the very sentence
  containing one of them, that the check-mark had been reduced to its
  escaped form (process-level Must Fix, not a `.cs` file).** Neither
  ever shipped in source - `SummarySectionRenderer.cs` has always used
  the properly-escaped textual form in its own comment, verified again
  by a fresh non-ASCII grep of every touched `.cs` file (zero hits) as
  part of this round. The raw characters existed only in this markdown
  file's own prose (items 5 and 9 above) - a record about catching a
  Unicode paste that itself contained a Unicode paste, and factually
  wrong about what the `.cs` file contains. Both instances rewritten in
  plain ASCII; item 9's text above corrected to describe the reality
  precisely. This file now contains zero non-ASCII bytes again, matching
  its pre-W4A state.

Re-validated after all seven fixes above: `dotnet build -p:Platform=x64`
clean, 0 errors - a fresh warning check against the touched files found
none from `SummarySectionRenderer.cs`/`SummarySectionLayoutMath.cs`, and
the pre-existing StyleCop warnings elsewhere in `PlanViewModelBuilder.cs`
all sit on lines outside this round's diff. `Services/
PlanContentHeightMath.cs`/`PlanRelayoutMath.cs`/`Services/ModuleLog.cs`
remain zero diff; `Views/Rendering/TreeSectionController.cs` was not
touched. Full validation numbers below.

Validation: `dotnet build -p:Platform=x64` clean (0 errors; zero new
warnings from any touched/new file, across both the original pass and
the review-fix round). Module test suite green - 1303 passed, 0 failed
(was 1273 before this whole package; +30 net new tests: +24 from the
original pass, +6 from the review-fix round above - 1 multi-item
divergence test plus 5 `SummarySectionLayoutMathTests.cs` cases for the
widened-column geometry). No new Blish HUD references in tests; every
new/changed test exercises real production code paths with no
contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only (the currency table's Label is now the resolved NAME only,
never the id). Coin amounts still render icon-right-of-number throughout
(both formula bands reuse `CoinCurrencyRenderer.BuildCoinSegments`/
`LayoutCoinSegments` unchanged). Not regressed: `PlanContentHeightMath.cs`/
`PlanRelayoutMath.cs` have zero diff; `Views/Rendering/
TreeSectionController.cs` was not touched; every other section renderer
(Used Materials, Shopping List, Crafting Steps, Required Disciplines,
Required Recipes) is untouched.

No live desktop verification was performed for this package (browser/game
automation was out of scope for this session) - item 5's glyph choice and
the overall visual layout (including the review-fix round's new formula-
band operators and widened currency columns) are unverified live and
should get a look in a real Blish HUD session before this is considered
fully done.

**12. Adversarial-review fix round 2 (2026-08-15) - 1 blocking finding
fixed from a second independent code review, this one specifically
targeting round 1's own fixes.**

- **Loss-band `"="` operator asserts a false equation (Must Fix,
  `Views/Rendering/SummarySectionRenderer.cs` `CreateFormulaBand`,
  introduced by round 1's "formula-band operators never drawn" fix
  above).** Round 1 started literally drawing `"-"`/`"="` between tiles,
  but the profit band's loss tile has always shown `Math.Abs(profit)`
  under a `"Loss if Sold"` caption (the pre-existing sign convention,
  predating both review rounds - coin amounts render via
  `CoinCurrencyRenderer.BuildCoinSegments`, which clamps negative input to
  0, so there was never a way to show a signed coin value without
  touching that shared, reused-not-modified machinery). Once round 1 made
  the band's final boundary a literal `"="`, that pre-existing convention
  became actively wrong on screen: `PlanViewModelBuilderSummaryTests.
  ProfitBand_NegativeProfit_LabeledLossWithAbsoluteValue`'s own numbers
  (`NetSaleValue = 340`, `TotalMaterialsValue = 500`, `profit = -160`)
  render as `"340 - 500 = 160"`, which is false (the true right-hand side
  is -160). This is the common case, not an edge case - most GW2 recipes
  craft at a loss. Fixed by giving `PlanRowViewModel` a new
  `FormulaResultIsExact` field (default `true`, read only on a band's
  LAST tile): `PlanViewModelBuilder.BuildProfitFormulaBand` sets it to
  `profit >= 0` on the Profit/Loss tile (the only row either band ever
  sets it false on - Band 1's three non-negative terms always balance
  exactly, so every other tile keeps the true default);
  `CreateFormulaBand` reads it to choose the final boundary's symbol -
  `"="` when true, a new neutral `":"` separator (`NeutralResultSeparator`
  - deliberately not `"-"`, which would misread as a second subtraction,
  and not `"="`, the exact claim being removed) when false. The
  non-final boundary (Band size is always 1 or 3 tiles; only a 3-tile
  band has a non-final boundary at all) is untouched - the left two
  tiles' own subtraction was never in question, only whether the FINAL
  tile's displayed value is the true right-hand side. `Math.Abs(profit)`
  and the `"Loss if Sold"` caption are both UNCHANGED (grep-swept: the
  only `Math.Abs` call in `Services/`/`Views/`/`Models/` outside test
  code is this one, so there is no sibling instance of this pattern to
  fix elsewhere) - this fix only changes which punctuation mark is drawn
  at one boundary, never the coin math, never the caption text, and never
  touches `CoinSegmentMath`/`CoinCurrencyRenderer` (reused as-is, per
  task instruction). Covered by three new/extended
  `PlanViewModelBuilderSummaryTests.cs` cases:
  `ProfitBand_NegativeProfit_LabeledLossWithAbsoluteValue` now also
  asserts `FormulaResultIsExact == false`; a new
  `ProfitBand_ZeroProfit_FormulaResultIsExactTrue` covers the `profit ==
  0` boundary of the `>= 0` check (identity holds exactly there too, not
  just for strictly positive profit); and both
  `ProfitBand_SellPricePresent_ThreeTilesWithIdentityArithmetic` and
  `CostBand_MaterialsUsedPositive_ExpandsToThreeTilesWithCorrectArithmetic`
  gained an explicit `FormulaResultIsExact == true` assertion on their
  respective bands' last tile. `SummarySectionRenderer.cs` itself stays
  Blish-bound and untestable directly per the repo's test invariants, so
  the operator-selection LOGIC is asserted at the data-flag level
  (`FormulaResultIsExact`) rather than the rendered glyph - the same
  boundary the class's own existing tests already draw for tooltip text
  and coin values, which are likewise never rendered in a test.

Re-validated after this round-2 fix: `dotnet build -p:Platform=x64`
clean, 0 errors (only pre-existing StyleCop warnings, none on lines this
round touched). Module test suite green - 1304 passed, 0 failed (was
1303 after round 1, before this whole W4A package's baseline of 1273;
+31 net new tests overall, +1 from this round: three cases extended/
added, but `ProfitBand_ZeroProfit_FormulaResultIsExactTrue` is the only
wholly new `[Fact]`). No new Blish HUD references in tests; the new/
changed assertions all still drive the real `PlanViewModelBuilder.
Build(CraftingPlanResult)` entry point. Not regressed: `Services/
ModuleLog.cs`/`PlanContentHeightMath.cs`/`PlanRelayoutMath.cs`/scroll
machinery/merged-ceil vendor batching all remain zero diff across both
review rounds; `Views/Rendering/TreeSectionController.cs` was not
touched; `CoinSegmentMath`/`CoinCurrencyRenderer` were read but not
modified - the fix lives entirely in `PlanRowViewModel` (new field),
`PlanViewModelBuilder.BuildProfitFormulaBand` (sets it), and
`SummarySectionRenderer.CreateFormulaBand` (reads it). Item/currency/
vendor IDs remain internal-only; coin amounts still render icon-right-
of-number throughout (unchanged). No live desktop verification was
performed for this round either, same caveat as item 11's own closing
paragraph above.

Gate: PASS 2026-08-16 (orchestrator live sandbox session, combined wave-4 staging build). Verified: cost-band collapse rule (single Actual Cost to Craft tile when opportunity cost is zero - ARE and Zojja plans), currency table alphabetical with icons and correct Required/Have/Needed math, green OK badges on all fully-covered rows, TP-variance footnote, band-caption tooltip renders on hover, coin icons right of numbers. Checkmark-glyph experiment FAILED live (U+2713 renders as an empty tag in the Blish font) - the OK badge is the permanent marker choice.

Follow-ups (recorded during a later polish pass, not yet implemented):

- Follow-up: delete `PlanContentHeightMath.SummaryBodyHeight`, its tests
  (`PlanContentHeightMathTests.cs` ~348-390), and `PlanRowType.CoinTotal`
  once the DO-NOT-TOUCH freeze on `PlanContentHeightMath` lifts - all
  three are dead for production since `CraftingPlanView` routes Summary
  to `SummarySectionLayoutMath`. **DONE (2026-08-17, high-evidence-zones
  branch).** Characterized first per the new policy (see this file's
  policy note above): confirmed by grep that no production call site
  ever passes `PlanSectionType.Summary` into `SectionBodyHeight`
  (`CraftingPlanView` always special-cases it to
  `SummarySectionLayoutMath.BodyHeight` first) and that
  `PlanViewModelBuilder` never emits `PlanRowType.CoinTotal`. Deleted:
  `SummaryBodyHeight`, its `SectionBodyHeight` switch case,
  `PlanRowType.CoinTotal`, and 4 tests that referenced `CoinTotal`
  directly and would not otherwise compile (`Summary_CoinRowPlusCurrencyRows`
  plus the three originally estimated at ~348-390) - one more than this
  bullet's own estimate, found during characterization. **Follow-up
  correction (2026-08-17, same branch, code review):** the fifth
  Summary-shape test, `Summary_NoCoinRow_OmitsTileRow`, does not reference
  `CoinTotal` and still compiled/passed, so it was initially left as-is -
  but review found it had gone vacuous: with `SummaryBodyHeight`'s switch
  case gone, it exercises `SectionBodyHeight`'s `default` arm and only
  passes because `CurrencyRowHeight`/`FallbackTextRowHeight` are both 28,
  a coincidence unrelated to Summary. It duplicated
  `UnknownSectionType_FallsBackToTextRowHeightPerRow` under a name
  claiming Summary-specific semantics that no longer exist, and would
  false-fail the moment either constant is retuned independently. Deleted.
  Full suite: 1765 before the first deletion pass, 1761 after it, 1760
  after this correction (5 dead/vacuous tests removed total).
- Follow-up (user decision pending): the Summary currency table now
  shows the RAW wallet holding in Have, while the shopping list still
  clamps its per-currency owned amount to the required amount
  (`CurrencyDisplayResolver.ResolveAmounts`) - the same currency can
  show two different owned numbers in one window; decide whether to
  unclamp the shopping list to match.

---
