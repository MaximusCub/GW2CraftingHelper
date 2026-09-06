> **Milestone record - 2026-09-05, branch `w20-total-cost-groups`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Total Cost: the inventory rows get a real Have and Needed, and the group labels read as headings

Pull request 245. The Total Cost section's non-coin table was already
split into a "From your wallet" group and a "From your inventory" group
by branch `w17-coin-seat`. Two problems were left in that table. Every
inventory row showed a Required figure and a "-" under both Have and
Needed, so two of the table's four columns carried nothing for half of
it. And the two group labels drew in the caption face at the tile-caption
grey, which put them under the column-header band as fine print rather
than over the rows they name.

### Have and Needed on the inventory rows

The inventory rows are barter items: untradeable vendor tokens whose
units are the price. They were built with a null owned quantity, on the
stated grounds that the module reads a wallet and not an item count.

That reason was wrong. The account snapshot indexes bank, shared
inventory, material storage and every character's bags, and
`Services/CraftingPlanPipeline.cs` already built an owned-count map for
exactly these vendor item cost lines - `BuildOwnedVendorItemComponentAmounts`,
whose result the Recipe Tree's cost-component leaf already draws its own
OWN badge from. The map simply never reached the result object.

**The fix.** `Models/CraftingPlanResult.cs` gains
`OwnedVendorItemAmounts`, an owned amount per item id. The pipeline sets
it in two places, on generation and on the local re-solve path, so a
re-solve keeps showing it.

In `Services/PlanViewModelBuilder.cs`, both halves of the table now go
through two new helpers instead of doing their own arithmetic.
`LookupOwned` returns the raw holding of one id or null when the map has
no answer. `ApplyOwnedSplit` seats `CurrencyOwnedQuantity`,
`CurrencyNeededQuantity` and `CurrencyFullyCovered` from that holding.
The wallet rows call it with `OwnedCurrencyAmounts`, the barter rows with
`OwnedVendorItemAmounts`. One derivation for both, so a wallet currency
and a barter item cannot answer the same coverage question differently.

Because the barter rows read the same map as the tree's leaf, the table
and the tree cannot state different holdings for one token.

The null contract is unchanged and still load-bearing. With no account
snapshot the map is null and both columns still read "-". A snapshot that
shows none of a token now reads 0, which is a different fact. The
coverage marker lights for a fully owned barter item on the same terms a
wallet currency gets it, the holding being known and meeting the
requirement, because a row reading "Have 13 / Needed 0" beside a blank
Status would contradict its own columns.

The holding is cosmetic, on the same never-fed-back contract as the
currency holding beside it: the plan still costs what it costs.

**Persistence.** Adding a property to the persisted graph moved
`PersistedPlan.SchemaShapeHash` and added one line to
`tests/shared/persisted_plan_schema.txt` and to each of the ten
plan-solver goldens. `CurrentSchemaVersion` is unchanged: an addition is
not a rename, a removal or a retype, so no saved plan becomes unreadable.

### The group labels read as headings

`Views/Rendering/SummarySectionRenderer.cs` drew "From your wallet" and
"From your inventory" in `UiFonts.Caption` at the formula band's tile
caption grey. They now draw in `HeaderBands.Font` and
`HeaderBands.LabelColor`, the face and colour every column header in the
module already uses, on no band of their own. The band is what keeps the
column header row above them ranked higher.

`Services/SummarySectionLayoutMath.cs` changes both constants to match.
`NonCoinGroupHeadingHeight` was `PlanContentHeightMath.FallbackTextRowHeight`
(28) and is now `PlanContentHeightMath.ColumnHeaderRowHeight` (32).
`NonCoinGroupHeadingTextY` was the caption line box centred in the
heading row and is now `PlanContentHeightMath.ColumnHeaderLabelY` (4).
That pair is the one already derived to clear this face's descenders
inside the row, so a heading and the band it matches move together.

The renderer and the section's `BodyHeight` both size the table from the
one grouping function, so a taller heading cannot desync the sticky
band's unpin point from the flow it was measured against.

### Regression coverage

`tests/TaimisToolbench.Tests/Services/PlanNonCoinCostTotalsTests.cs`
gains four cases, all driven through the real pipeline via
`GenerateStructuredAsync` with a real `AccountSnapshot`:

- a partial holding fills both columns - the plan needs 6, the account
  holds 4, and Needed is 2 with the coverage marker off;
- a full holding closes the gap and lights the marker, reports the
  holding raw at 10 rather than capping it at 6, and the plan still costs
  6 of the token;
- a snapshot that holds none of the token reads 0 and not null, which is
  the distinction the whole null contract exists for;
- the table row and the tree's cost-component leaf state the same
  holding, which is what pins against a second count derived somewhere
  else.

The existing no-snapshot case is kept and still asserts three nulls and a
false.

`tests/TaimisToolbench.Tests/Services/PlanContentHeightMathTests.cs`
gains an ink-clearance case: the group heading row holds the column
header face's descenders, which a row sized for the shorter caption line
would clip.

### Validation

The branch's commits record the `docs/file-budgets.txt` adjustments for
each change but state no build result and no test totals, so this record
has none to quote.

Gate: NOT RUN - no live in-game check is recorded on either commit. Two
things to confirm in game. On a plan with vendor item costs and a
connected account, that the "From your inventory" rows now show real Have
and Needed figures and that a fully held token shows the coverage marker;
with no account snapshot both columns should still read "-". And that the
two group labels now read as headings over their rows, ranked below the
column header band but above the data, at the taller 32px row.
