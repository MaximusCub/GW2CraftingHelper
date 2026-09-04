> **Frozen record - 2026-08-23, branch `root-ignore-summary-zero`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Root ignore suppression and the zero-cost band (root-ignore-summary-zero)

Two findings from the same field-test round, both about what
the plan shows once nothing is left to buy.

### 1. The root row must not offer IGNORE

The top-level item in the recipe tree should not be IGNORE-able. The
Ignore pill means "treat this item as already in hand
tree-wide"; on the plan's own target that is a request to plan nothing,
and gw2e offers it only because gw2e's tree has no separate target row.

`CraftingTreeNode.IsPlanRoot` is set in `CraftingTreeBuilder.BuildTree` -
the one method that knows which node the caller asked for a tree OF, and
the method a multi-item batch calls once per requested root, so all N
roots carry it. `DecisionPillPlanner.AppendOwnershipPills` then skips the
"IGNORE" spec for a root, which removes the affordance everywhere at
once: `TreeSectionController` wires the click handler FROM the spec list,
so a suppressed spec is a suppressed handler. Nothing under `Views/` was
edited.

The un-ignore half of the toggle is deliberately NOT suppressed, but not
for the reason first written here. Ignores do NOT outlive the plan they
were set in: `TreeSectionController.ResetForNewPlan` clears
`_ignoredItemIds` on every Generate, and `GenerateStructuredAsync` takes
no ignore set at all, so a freshly generated plan's root can never arrive
already ignored. The genuinely reachable route is a **multi-item batch**:
ignores are keyed by item id and apply tree-wide within one solve, so
ignoring an item where it appears as an *ingredient* under one requested
root also flips a *sibling requested root* of that same item to ignored.
That root offers no "IGNORE" pill, so the "IGNORED" pill in the
`Decision == Have` branch is the only way back out
(`PlanRootIgnoreTests.MultiItemBatch_IgnoringAnIngredient...`).

`PersistedPlan.CurrentSchemaVersion` stays at **3**. `IsPlanRoot` is
`internal`, and Newtonsoft's default contract serializes public
properties only, so the flag never enters the persisted graph and no bump
is needed - a bump would have discarded every existing user's saved plan
(overrides and ignores included) to avoid a pill that self-heals on the
next re-solve anyway. Restore is the one path that builds a
`CraftingPlanResult` without `CraftingTreeBuilder`, so
`PlanStoreHelpers.DeserializePersistedPlan` re-derives the flag on the
roots it already knows - `CraftingTree`, or every `MultiItemRoots` entry
(`PlanStoreTests.LoadLatest_*_ReMarks*AsPlanRoot`).

### 2. A zero-cost plan must still render the whole band

"If you do ignore it the display layout in Total Cost section gets all
messed up and reverts to just showing the Actual Cost to Craft section
with 0c while the rest of the layout disappears when it should revert to
0s."

The cause is `PlanViewModelBuilder.BuildCostFormulaBand`'s collapse rule,
not the renderer: with no owned-materials term the band emits ONE tile,
and `SummarySectionRenderer` left-aligns a lone tile in a full-width
band. That reads fine next to a real number and reads as a broken
section next to `0c`. The rule now collapses only when there is a cost to
show - a plan whose coin cost AND owned-materials term are both zero
renders the full "Total Materials Value - Your Materials Used = Actual
Cost to Craft" formula at zero, tooltips and footnote included. Both of
those zeros have to be zeros somebody computed; the two paragraphs below
are the qualification, not a footnote to it.

The zero middle term must be a **known** zero, not merely an absent one.
`MaterialOpportunityCost` is null by contract outside
`OwnMaterialsMode.Valued` (`SellSideEconomics.ComputeMaterialOpportunityCost`),
so "Use Own Materials" on with "Value Own Materials" off and an inventory
covering the whole plan produces coin cost 0, real `UsedMaterials`, and no
valuation at all. Printing the band there would assert "Your Materials
Used 0c" directly above a Used Materials section listing the materials
actually consumed - a number nobody computed. That case keeps the
collapsed single tile
(`PlanViewModelBuilderSummaryTests.CostBand_ZeroCostButMaterialsConsumedUnvalued_StaysCollapsed`);
Valued mode that genuinely computed 0 still gets the full band.

The same measured-vs-unmeasured test governs the **cost** term. A plan
also totals 0 when nothing in it could be priced: an item with no recipe
and no TP listing generates normally (nothing gates it), and
`CraftingTreeBuilder.BuildNode` maps that node to `Decision = Unknown`
while an ignored node maps to `Have` + `IsIgnored` - so the two are
cleanly separable. `PlanViewModelBuilder.HasUnpricedNode` walks the
display tree (`CraftingTree`, or every `MultiItemRoots` entry, skipping
reference branches) from the zero-cost gate only, never on the priced
path, and an unpriced node keeps the collapsed tile: "Total Materials
Value 0c" would state the full market value of a craft the pipeline never
valued, under a root row reading UNKNOWN.

So the class fixed here is every **known** zero - ignoring every child, a
currency-only plan, and Valued mode that priced the consumed materials at
0 all get the same band. Two zero-cost states stay collapsed on purpose,
because a term is unmeasured rather than zero: Free mode with owned
materials actually consumed (the paragraph above), and a plan zeroed by
unpriced items. Free mode with owned materials therefore still shows the
lone 0c tile - that is the deliberate shape, not a residual of the
reported bug; see the known-vs-absent-zero paragraph above before
"fixing" it. Band height is unaffected either way -
`SummarySectionLayoutMath.BodyHeight` counts one cost band whether it
holds 1 or 3 tiles.

Coverage: `PlanRootIgnoreTests` (pill suppression across every
`BuildPillSpecs` return path, the flag's single write site, a real
multi-item batch, the reachable ignored-sibling-root case, the end-to-end
"ignore every ingredient" plan through
`CraftingPlanPipeline.ResolveWithOverrides` into `PlanViewModelBuilder`,
and the end-to-end unpriced-ingredient counterexample that must NOT get
the band), nine zero-band cases in `PlanViewModelBuilderSummaryTests`
(including the unvalued-materials collapse, the unpriced single-item and
batch collapses, and the reference-branch exemption), and two restore
cases in `PlanStoreTests`.
Two pre-existing `DecisionPillPlannerTests` end-to-end cases asserted an
IGNORE pill on a `BuildTree` root and were updated to the new
expectation.

### Desktop gate

1. Generate any plan. The **root row shows no IGNORE pill** - source
   pills and any HAVE annotation are unchanged, and every child row still
   offers IGNORE. Check a multi-item batch too: all N top-level rows.
2. Force the known-zero branch first: turn **"Use Own Materials" OFF**
   (or pick a target whose ingredients you own none of), then ignore
   **every child** of the root until the plan costs nothing. The Total
   Cost section still shows the **full band** - "Total Materials Value -
   Your Materials Used = Actual Cost to Craft" - with 0 amounts, the
   "-"/"=" operators between the tiles, the result tile's highlight box,
   and the footnote line. It does not collapse to a lone 0c tile.
   The precondition matters: with "Use Own Materials" on and stock in
   hand, ignoring a child does NOT stop `InventoryReducer` consuming it
   (the reducer is guided by the solve, not by the ignore set), so the
   plan is no longer a zero-material one and step 2 cannot be read.
3. Repeat step 2 with **"Use Own Materials" ON** on an item you DO own
   ingredients for. Both outcomes below are correct - neither is a
   failure of this change:
   - "Value Own Materials" ON (the default): the full band renders with
     **non-zero** amounts. Total Materials Value and Your Materials Used
     both carry the valuation of what your inventory covered; only Actual
     Cost to Craft reads 0c.
   - "Value Own Materials" OFF: the band collapses to the **lone 0c
     tile**, by design - nothing computed the middle term, so printing it
     as 0 would contradict the Used Materials section right below.
4. Back in the step 2 state, un-ignore one of those children: the band
   returns to its ordinary shape and the numbers come back.
5. Plan an item with **no recipe and no Trading Post price** (root row
   reads UNKNOWN). The plan totals 0c but the band stays a **lone 0c
   tile** - a zero nobody measured must not be dressed up as a priced
   equation. The profit band below obeys the same rule: on such a plan
   **no "Sell Value / Total Materials Value / Profit if Sold" tiles
   render at all** (an unpriced-zero cost would otherwise read as
   "profit = the entire sale price"). Also try a variant whose TARGET
   has a sell price but whose ingredient is unpriced - same
   expectation: lone 0c tile, no profit band.
6. Generate a **multi-item batch** where one requested item is also an
   ingredient of another requested item (e.g. request a weapon and one of
   its components). Ignore that component where it appears as a child row
   under the other root. Its own top-level row flips to HAVE + IGNORED
   (not IGNORE), and clicking IGNORED restores it. This is the only
   reachable ignored-root state - a fresh Generate always clears prior
   ignores, so ignoring an ingredient and then planning that item alone
   yields an ordinary un-ignored root.

Gate: PASS with recorded partials (2026-08-23 night desktop session,
branch build, captures preflight/gRZ1-gRZ6). LIVE-VERIFIED: the plan
root offers no IGNORE pill - and on the strongest possible variant:
a schema-3 plan RESTORED from disk into this build (PlanStoreHelpers
re-derives IsPlanRoot on restore, so suppression holds on restored
plans; children keep their IGNORE pills). The regenerate-confirm
backdrop also demonstrably ate mispositioned clicks (batch-J modality
working). PARTIALS: the all-ignored zero band, un-ignore restoration,
the pre-ignored root's IGNORED escape hatch, and the unpriced-zero
profit-band suppression were not reachable live in that gate - the
sandbox's known synthetic-input decay (M33/M38 class) killed keyboard
then clicks before the multi-step flows completed. All four stand on
real-production-path tests (PlanRootIgnoreTests end-to-end through
CraftingPlanPipeline + the three zero-band viewmodel cases + the
profit-band assertion) and are one-hover checks on a live install.
Morning re-run (2026-08-24, screens awake, captures preflight/gM1-gM8):
those partials are now LIVE-VERIFIED - a fresh-generated root also
offers no IGNORE pill; ignoring the sole child rendered the FULL
three-tile band at 0 ("Total Materials Value 0c - Your Materials Used
0c = Actual Cost to Craft 0c", profit band legitimately present since
the plan is priced); un-ignoring restored every number. The pre-ignored
root's escape hatch is live-UNREACHABLE by design (TriggerGenerate
clears the ignore set, so a root cannot arrive ignored through the UI);
its unit pin in PlanRootIgnoreTests is the correct and complete
coverage. Bonus: the dimmed-pill dead-click tooltip fired correctly.
