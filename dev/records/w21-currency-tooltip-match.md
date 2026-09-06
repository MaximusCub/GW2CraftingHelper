> **Milestone record - 2026-09-05, branch `w21-currency-tooltip-match`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## The module's currency tooltip is matched to the game's own

The module draws its own rich tooltips rather than using the game's. Held
beside a real wallet-currency tooltip, three things did not match: the
currency's name read neutral grey where the game's reads a warm tan, the
header icon painted three pixels small on each axis, and the frame around
that icon was the wrong grey. All three are fixed here, measured against a
lossless capture of the game's currency tooltip. Merged as pull request
246.

### The name took the rarity colour a currency does not have

`TooltipContent.HeaderLine` sent every header name through
`TooltipSpan.RarityText`. A currency has no rarity, so its name landed on
the unknown-rarity neutral fallback.

The game colours it (255,204,119), which is Blish's
`ContentService.Colors.Chardonnay` to the unit. A new
`TooltipSpanRole.CurrencyName` carries it, and `HeaderLine` hands it out
off the header subject the composers already state:
`subject.IsCurrency` takes the new role, everything else keeps
`RarityText`. `RichTooltipSurface` resolves the role to Chardonnay.

The branch on `IsCurrency` rather than on a missing rarity is deliberate.
An item's name is measured at its rarity colour, white included for
Basic, so an item whose rarity nobody resolved still wants the rarity role
and its neutral fallback. Only a subject with no rarity to have reaches
the new role.

### The header icon's box was a physical measurement used as a logical one

A screen capture is native 1:1 only against the GW2 UI size it was taken
at. Blish paints module coordinates through that size's own scale - 0.897
at "Normal", 1.0 at "Large" - so a pixel count lifted off a capture is a
logical number only when the capture was taken at "Large".

The item and currency icon tiers were measured at "Large". The tooltip
header icon's 34x34 was not, so the module reserved 34 logical units and
painted 30.5, three pixels short on each axis beside the game's own
tooltip. The same capture pins the conversion: it shows the game's header
icon at 34 physical pixels beside the module's 34-unit frame painted at
31.

`ItemIconTiers` now holds 36 of art for `ItemIconTier.TooltipHeader`,
which with its 1px frame is a 38-unit box: 34 / 0.897 = 37.9. That paints
the game's 34 at "Normal" and its 38 at "Large".

`Views/Rendering/RichTooltipSurface.cs` had a second copy of the same
numbers - `HeaderIconSize` 32, `HeaderIconBorder` 1 and a
`HeaderIconFrameSize` summed from them - which it used to reserve the
header row's height and the name's indent. Those three constants are gone
and the surface reads `ItemIconTiers.FrameSize(ItemIconTier.TooltipHeader)`
instead, so the row cannot grow apart from the icon drawn into it.

### The frame colour was a single edge sample

`HeaderIconFrameColor` was (166,175,174), taken from one edge of an item
capture and recorded at the time as a judgment call. The currency capture
paints an exact (229,229,229) on all four edges, and the constant now
holds that. One frame serves both the item and the currency header, so
both moved. The two shapes stay as they were: a plate for an item, a ring
for a currency, because a plate behind a currency's transparent art shows
through as a background.

### Regression coverage

- `ItemIconTiersTests` pins the tier at 38 and asserts that 38 rounds to
  the game's 34 through the 0.897 scale, which is the arithmetic the
  defect got wrong.
- `TooltipHeaderSubjectTests` pins that a currency name carries the
  `CurrencyName` role and that an item with no resolved rarity still
  carries `Rarity`, which is the distinction the branch turns on.

`docs/ARCHITECTURE.md` section S1.3 gained the capture-scale rule and why
this one tier had to be converted while the tiers above it did not.

### Validation

The commits record no build or test output, so this record cannot quote
either. Both commits record the `docs/file-budgets.txt` raises they
needed.

Gate: NOT RUN - no live in-game confirmation is recorded on this branch,
and the fix is a comparison against the game's own rendering, which only
an in-game look can settle. A reviewer should hover a wallet currency
until the module's tooltip opens, put the game's tooltip for the same
currency beside it, and check the name colour, the icon box size and the
frame at both the "Normal" and "Large" GW2 UI sizes.
