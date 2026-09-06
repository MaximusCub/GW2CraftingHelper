> **Milestone record - 2026-09-04, branch `w17-coin-seat`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Coin icons seat on the game's baseline, and Total Cost groups its non-coin rows

Two unrelated changes to the Crafting Plan's cost display, in pull request
243. Gold, silver and copper icons drew too high against the numbers beside
them. Separately, the Total Cost section's non-coin table listed wallet
currencies and barter items in one alphabetical run.

### The coin seat

Every inline coin run took the seat `CoinSegmentMath.InlineIconY` computes.
That seat centres the icon BOX on the digits' declared ink box. Neither box
matches the pixels it contains.

Two paddings sit between the boxes and the ink, both measured on shipped
assets:

- A Menomonia glyph box runs one row past its ink at each edge. The faces
  are built with `outline="1" spacing="1,1"`. On the 14-regular, 16-regular,
  20-bold and 32-regular pages, every `0` inks rows 1 to height-2 of its own
  box.
- The three 32x32 coin textures do not fill their box. Gold (156904) and
  silver (156907) ink rows 5..26. Copper (156902) inks rows 4..26. The last
  inked row is 26 on all three.

Centring the two boxes therefore left the visible art high.

The reference is the wallet summary bar capture already recorded in
`Services/CurrencyIconTiers.cs`: icon box y114..129 against digit ink
y115..126. The art's last inked row in a 16px box lands at y127, one row
below the digits' ink bottom.

### The fix for the seat

`Services/CoinSegmentMath.cs` gains `CoinIconY`, which works in ink terms on
both sides. It takes the digits' ink bottom, subtracts `CoinArtInkBottom`
for the icon size, and adds `CoinInkBelowBaseline`, which is 1. Two named
constants carry the measurements: `GlyphBoxInkPad` 1, and the art's last
inked row 26 of 32.

`CoinArtInkBottom` scales with the icon box rather than assuming the 16px
answer, since the same run draws at 32 in the wallet list tier.

`Views/Rendering/CoinCurrencyRenderer.cs` gains `CoinDigitSeat` beside the
existing `DigitSeat`, and its two coin layout paths call the new one. At the
shipped body face and the 16px bar tier the seat moves from 1 to 3.

Only gold, silver and copper move. Other inline currency icons keep the
centred seat, because they already measured centred to within half a pixel.
The two seats now differ deliberately, and `CoinIconY`, `CoinDigitSeat`,
`CurrencyIconTiers.VerticalAlignmentRule` and `docs/ARCHITECTURE.md` section
S1.3 each say so.

A face that reports no `0` region falls back to the centred seat. A bottom
seat is a claim about the baseline, and a bare line box does not have one.

### Regression coverage for the seat

`tests/TaimisToolbench.Tests/Services/CoinSegmentMathTests.cs` adds cases
that pin, per denomination, that the art's last inked row lands one row
under the digits' ink bottom. Copper's extra top row must not shift the
seat. Other cases pin that the coin seat sits strictly below the currency
seat, so folding the two back together fails; that the currency seat's value
is unchanged; that `CoinArtInkBottom` scales with icon size; that an icon
taller than the digits' ink stays inside the line box; and that the seat is
read off whichever face it is handed, using menomonia-32-regular.

### Grouping the non-coin cost table

The Total Cost section's non-coin table holds one row per wallet currency
and one per barter item the plan spends. They were sorted into a single
alphabetical list. A reader working out what to look up in the wallet had to
pick those rows out from ones checked in inventory instead.

`Services/SummarySectionLayoutMath.cs` gains `GroupNonCoinRows`, which
splits `CurrencyCost` rows on the `IsBarterItemCost` flag they already
carried. Wallet currencies come first, then barter items. The headings are
`WalletGroupHeading` "From your wallet" and `InventoryGroupHeading` "From
your inventory". Relative order inside a group is the caller's, so one
alphabetical sort still leaves each group alphabetical. An empty group
produces no entry.

The grouping is derived, not modelled. No heading row is added to
`section.Rows`. A heading row would be one missed `RowType` check away from
counting as a cost in the plan-level totals projection, in the row-kind
count behind the disclosure line and name header, in the currency column
scan, and in two height calculations.

`Services/PlanViewModelBuilder.cs` emits the rows already grouped, so
`BuildNonCoinCostTotals` is unchanged. It still projects the plan-level
figure from the same rows the table draws, in row order, so the figure and
the table cannot drift. Only the order changed. Its doc now names the change
that would break that: aggregating per group.

`Views/Rendering/SummarySectionRenderer.cs` draws a heading row per group
with `CreateNonCoinGroupHeadingRow`, in the caption face and the grey the
formula band's tile captions use, left-anchored on the table's icon gutter.
`SummarySectionLayoutMath.NonCoinTableRowsHeight` prices the whole table
below the column-header band, and both the renderer and `BodyHeight` size
from that one function over the same groups. The sticky column-header band's
trailing height closure now captures that value instead of multiplying a row
count.

### Regression coverage for the table

`SummarySectionLayoutMathTests.cs` adds cases for the grouping and for
`BodyHeight`: one heading reserved per group, order inside a group
preserved, a single-kind plan yielding one group, an identical name on
either side still landing in its own group, and every other row kind
ignored. `PlanNonCoinCostTotalsTests.cs` runs the real pipeline and pins
that a wallet currency leads a barter item whose name sorts ahead of it, and
that `NonCoinCostTotals` still matches the table's row order.

`docs/file-budgets.txt` moves with the seven files whose line counts changed.

Gate: NOT RUN - the branch's commits record no live game or sandbox check.
A reviewer should open a plan whose cost includes coin and both a wallet
currency and a barter item. Confirm the coin icons sit level with the
digits rather than above them, that other currency icons look unchanged,
and that the non-coin table shows two headings with the wallet group first.
