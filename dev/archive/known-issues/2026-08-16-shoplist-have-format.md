> **Frozen record - 2026-08-16, branch `shoplist-have-format`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Shopping-list row tooltip: scope collision + swallowed hover (shoplist-have-format, 2026-08-16)

Follow-up review of the shoplist-have-format branch (three own commits:
`7d4cb2a` add raw unclamped currency holding, `fa36cb7` reword the
shopping-row tooltip off the banned "N owned, M needed" phrasing,
`fa1829c` fix the tooltip-currency-lines-dropped-on-resize divergence)
found two Critical/Must-Fix issues in the new wording and its wiring,
fixed in this pass.

**SCOPE COLLISION (Critical).** The reworded wording rendered bare
`HAVE {cc.Amount}/{cc.Amount}` - a per-row (PlanStep) total, never the
whole plan's need for that currency id - with no scope marker, in the
exact vocabulary `DecisionPillPlanner.AppendCurrencyOwnershipPill`
reserves for PLAN-scope facts (`HAVE {have}/{planTotal} TOTAL`). Two
shopping rows drawing on the same wallet currency (e.g. a 700-Karma need
split into 300 + 400 across two vendor rows, wallet holds 500) could
each independently render as fully covered - "HAVE 300/300" and
"HAVE 400/400" - double-counting the one wallet balance, with the new
"(you hold 500)" aside actively reinforcing the false reading by
splicing a wallet-wide figure into a row-scope coverage fraction.
`Services/ShoppingRowTooltipFormatter.cs` now appends a `THIS ROW`
scope marker to both the shortfall and covered lines (mirroring
`AppendCurrencyOwnershipPill`'s own `TOTAL` marker on the plan-scope
pill), and the surplus aside now reads `(wallet N)` instead of "(you
hold N)" - the same "wallet" term the Summary c-table's Have column and
the tree's `HAVE x/y TOTAL` pill already use, so no third phrasing for
the same concept survives. New/updated wording:
`Karma: HAVE 200/500 THIS ROW, NEED 300` (shortfall),
`Spirit Shards: HAVE 500/500 THIS ROW` (covered, no surplus),
`Spirit Shards: HAVE 500/500 THIS ROW (wallet 999999)` (covered,
surplus). A plan-scope fix (threading `PlanViewModel.CurrencyPlanTotals`/
`OwnedCurrencyAmounts` into `ShoppingListSectionRenderer.Render` and
building the line exactly like `AppendCurrencyOwnershipPill` does) was
considered and rejected for this pass: `CurrencyAmountViewModel`
deliberately carries no currency id field at all (`CurrencyDisplayResolver`'s
own doc comment: "the no-displayed-IDs invariant is enforced by
construction here... so a caller cannot accidentally surface a raw
currency id"), so a plan-scope lookup would require adding one - a
larger, cross-file change out of scope for this pass. The row-scope
`THIS ROW` marker fully resolves the misreading (both halves now
honestly say "this row", never implying plan-wide coverage) without
touching that invariant.

**TOOLTIP SWALLOWED BY CHILD CONTROLS (Must Fix).** `BuildTooltip()`
stamped the new HAVE/NEED text on `rowPanel` only; `nameLabel` and the
Total cell's segment controls (`CoinCurrencyRenderer.RenderValueCellRightAligned`'s
labels/icons) have no `BasicTooltipText` of their own and silently
captured the mouse first over most of the row - including over the
Total cell's own currency amount, the one place a user hovering "do I
have enough?" would look. This repo has already root-caused and fixed
this exact class twice (the "Field-test UX wave" finding D and the
ellipsized-currency-name tooltip fix in `SummarySectionRenderer.cs`).
Fixed in `Views/Rendering/ShoppingListSectionRenderer.cs`:
`BuildTooltip()` now also assigns the same string to
`nameLabel.BasicTooltipText` and, via a new `SetValueCellTooltip`
helper, to every control in the Total cell's `ValueCellHandle` (coin
segments, currency segments, or the dash label for an unpriceable row).
`BuildTooltip()`'s definition and initial call were moved to after
`nameLabel`/`totalCell` are constructed so both are in scope for every
rebuild, including the existing `AddReellipsis` resize/settle path -
that rebuild already ran through the same `BuildTooltip()` closure, so
no new divergence risk is introduced.

**Verification split.** The wording change (`ShoppingRowTooltipFormatter.
BuildCurrencyLines`) is Blish-free and fully suite-covered - see
`ShoppingRowTooltipFormatterTests.cs`. The wiring fix
(`ShoppingListSectionRenderer.BuildTooltip`/`SetValueCellTooltip`,
`AddReellipsis` reassigning `BasicTooltipText` on resize/settle, and the
on-screen hover behavior itself - does hovering the name or the Total
cell now actually show the tooltip) is Blish-bound and outside this
repo's test-runnable surface, same constraint every UI-adjacent entry in
this file notes. Not yet confirmed live.

**Sweep note.** Re-ran the required sweep for other `"(N owned, M
needed)"`-style sites (repo rule: fix the class, not the instance):
zero remaining production `.cs` sites - the only `OwnedQuantity`
consumers are `CurrencyDisplayResolver`, `ShoppingRowTooltipFormatter`,
and the model itself; `SummarySectionRenderer` uses a Have/Needed column
table and `DecisionPillPlanner` already uses `HAVE`/`NEEDED`. The four
hits in `docs/dev-notes/HISTORY.md` (lines 745, 1496, 1765, 2615) are
historical field-test transcripts and are deliberately left verbatim,
not updated - recorded here so a future reviewer does not need to
re-derive that exclusion.

**Tests:** `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` - 1682 green (measured, this pass's own
commit), 0 failed. `ShoppingRowTooltipFormatterTests.cs` updated for the
new `THIS ROW`/`wallet` wording; its tautological
`BuildCurrencyLines_NeverMentionsPlanRequires` case (asserted a string
that appears nowhere in the formatter, so it could never fail) was
removed - `BuildCurrencyLines_MultipleCurrencies_OneLinePerCurrencyInOrder`
already pins the full multi-currency line set with exact-string
assertions.

**Build:** `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` -
PASS, 0 errors (pre-existing StyleCop warnings only, none in
edited files).

Gate: not yet run live - queued for the next desktop session. Merged after the full review pipeline resolved every finding, under the standing merge directive (2026-08-16).
