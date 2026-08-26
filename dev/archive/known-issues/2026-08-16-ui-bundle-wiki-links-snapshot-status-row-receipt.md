> **Frozen record - 2026-08-16, branch `ui-bundle-wiki-links-snapshot-status-row-receipt`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## UI bundle: wiki links, snapshot status row, receipt/what-if captions (2026-08-16)

Three maintainer-approved UI features, developed together in one
worktree (`ui-bundle` branch) since they touch disjoint files except
where noted.

**Feature A (wiki links)**: two placements, both maintainer-authorized
context actions rather than visible icons.

1. Recipe Tree rows (`TreeSectionController.RenderTreeNode`): every row
   (item leaf, internal node, cost-component leaf, reference-branch
   child alike) gets a right-click handler that opens the item's GW2
   wiki page (`WikiLinkBuilder.BuildItemPageUrl(node.Name)`) in the
   default browser, plus a "Right-click: Open wiki page" tooltip line.
   This is the module's first external-URL launch
   (`WikiLinkLauncher.Open`, a thin try/caught `Process.Start` wrapper
   logging failures to `ModuleLog` rather than crashing the row click).
   Right-click was chosen over a visible icon because the row's fixed
   column grid (caret/icon/name/pill column/cost column, all
   right-anchored per `PlanRelayoutMath.ComputeTreeColumnEdges`) has no
   spare pixels at typical window widths, and right-click cannot
   collide with the row's existing left-click expand/collapse toggle.
2. Required Recipes rows (`RecipesSectionRenderer`): same right-click
   pattern, scoped to `StatusTag == "Missing!"` rows only (a row the
   user has nothing left to unlock for gets no wiki affordance at all)
   - per the spec's own "Required Recipes Missing! rows" wording, a
   narrower scope than the tree row affordance's "each item row".
   Target is flag-based (`RequiredRecipe.IsLearnedFromItem`, set from
   `RecipeOption.Flags.Contains("LearnedFromItem")` in
   `PlanResultBuilder`, mirroring the existing `IsAutoLearned` flag
   exactly): a LearnedFromItem recipe links to its own
   "Recipe: &lt;output item name&gt;" wiki sheet page
   (`WikiLinkBuilder.BuildRecipeSheetUrl`); every other recipe links to
   the output item's own page with a "#Acquisition" anchor
   (`BuildItemAcquisitionUrl`) - anchors degrade gracefully to page-top
   on a wiki page with no such section, and page titles match item
   names via wiki redirects, as the spec assumes.

URL construction (`Services/WikiLinkBuilder.cs`) is a pure, unit-tested
helper: spaces become underscores, then the whole title is percent-
encoded via `Uri.EscapeDataString` (RFC 3986 unreserved characters -
letters/digits/`-`/`.`/`_`/`~` - survive unescaped, so the underscores
just inserted are untouched) - except the recipe sheet page's literal
`Recipe:_` namespace prefix, built separately so its colon is never
percent-encoded (`%3A` would not match the site's real URLs). Tested
against both spec-named examples: `"Zojja's Claymore"` ->
`Zojja%27s_Claymore` (and `Recipe:_Zojja%27s_Claymore` for the sheet
page), `"Bolt of Damask"` -> `Bolt_of_Damask`. No item/vendor id is
ever part of a built URL or displayed anywhere - IDs stay internal-only
throughout, unchanged.

**Feature B (status own row)**: `Views/MainView.cs`'s Snapshot tab
status label (`_statusLabel`, the "Updated - &lt;date&gt; (age)"/
failure-status text) moved out of `_headerPanel` - where it shared a
fixed x=140 slot with the Clear Cache/Refresh Now buttons, a layout the
method's own long-standing "Layout risk" doc comment already flagged as
capable of running out of room at the window's clamped 930x710 minimum
size - into its own new full-width `_statusPanel` row directly beneath
the header. Every row below (search box, source-filter checkboxes,
coin panel, scrollable content) shifts down by `StatusRowHeight` (24px)
plus the same 5px gap the header already used before the search row;
every other existing gap in the chain is preserved exactly via the same
additive `const` pattern the file already used for its row Y positions.
Verified before touching layout, per the milestone's own caution: the
Snapshot tab's rows are plain fixed-Y `Panel`s sized from local
`private const int` fields in `MainView.cs` itself, not
`PlanContentHeightMath`/`PlanRelayoutMath` - those two frozen files
size only the Crafting Plan tab's section/tree rows and are untouched
by this change. Both duplicated status-string build sites
(`MainView.ApplyStatusDisplay` and `Module.cs`'s Updated/Cache
Cleared/failure-status string composition) are unedited and keep
producing the same text; only the label's layout home moved.

**Feature C (receipt/what-if captions)**: under a `BuyFromVendor` node
whose `Children` stack BOTH W4B's synthesized cost-component leaves
(`IsCostComponent == true`) AND the dimmed "what it would cost to craft
instead" reference branch appended after them
(`CraftingTreeBuilder.BuildNode`'s `componentLeaves != null &&
wantsReferenceBranch` case - `node.IsReferenceBranch` true with
`Children[0].IsCostComponent` true is exactly this case, detectable
from the node alone with no new model field), two subdued captions mark
the group boundary: "Vendor price:" ahead of the component leaves,
"If crafted instead:" ahead of the reference-branch children.

**Substitution from the spec's own sanctioned fallback**: implemented
as extra tooltip lines on each group's first child row, NOT as real
inserted tree rows. Reason, per the spec's own caution: tree row
heights flow through `PlanContentHeightMath`'s tree arm
(`ChildrenHeight`/`TreeChildFlowHeight`/`TreeNodeHeight`, all frozen,
untouched), which sums `TreeRowHeight` over exactly
`node.Children.Count` - a caption is not one of this node's existing
children, so inserting a real row would desync the height math
(computed from `Children.Count`) from the actually-rendered row count,
reproducing the exact multi-frame flash/stutter class M33 C2a's
explicit-height rework was written to eliminate (see that section's own
entry above). `Services/ReceiptCaptionHelper.cs` (pure, Blish-free,
12 unit tests) computes `ComputeCaptionSplitIndex` (the child index
where the component-leaf run ends and the reference-branch run begins,
or -1 when a node is not this stacked case) once per node, and
`CaptionForChildIndex` maps a child index to caption text or null.
`TreeSectionController.RenderTreeNode` gained an optional
`captionText` parameter (default null, so every pre-existing call site
compiles unchanged) threaded through all three places children are
rendered - the initial default-expanded pass, the lazy-build
expand/collapse toggle handler, and the Expand All button's lazy-build
loop - each computing the split index once and passing the right
child's caption text down. The row itself already carries a tooltip
(`UpdateTreeRowTooltip`/`extraTooltipLines`); the caption is inserted
at the front of that same list so it reads first, ahead of any
unit-price/caveat lines a component leaf or reference-branch child
already shows.

**Risk/follow-up (reported, not fixed)**: because the caption is
tooltip-only, a user who never hovers the first row of either group
will not see it - the row-insertion approach the spec preferred would
be strictly more discoverable, but requires either widening
`PlanContentHeightMath`'s tree arm to understand a new "caption pseudo-
row" concept or synthesizing captions as real (Blish-free-serializable)
`CraftingTreeNode` children the existing math would count for free;
both are real design changes to a frozen file's contract or the
solver-adjacent tree-node model, out of scope for this milestone's
"tooltip fallback, report the substitution" instruction.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - clean,
0 errors (StyleCop warning count unchanged from before this milestone;
no new warning codes introduced by any edited/added file's own lines).
Tests: 1576 passed, 0 failed after two review-fix commits (84538e6, cc99f3f; initially 1549 = baseline 1519 + 30 new: 15
`WikiLinkBuilderTests` + 12 `ReceiptCaptionHelperTests` + 2
`PlanResultBuilderTests` [`RequiredRecipes_LearnedFromItemFlag`/
`_NoLearnedFromItemFlag_DefaultsFalse`] + 4
`PlanViewModelBuilderStepSectionsTests` [wiki-URL scoping/flag-target
cases] = 1549) via `dotnet test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`. No
Blish HUD/`BlishHUD.exe`/`Gw2Sharp` reference in any test file; every
new test exercises a real production entry point (`WikiLinkBuilder`'s
public static methods, `ReceiptCaptionHelper`'s public static methods,
`PlanResultBuilder.Build`, `PlanViewModelBuilder.Build`), no contract
mirrors, no fake file I/O. `WikiLinkLauncher`/the `RightMouseButtonPressed`
handlers and Feature B's layout are Blish-bound UI surface outside this
repo's test-runnable boundary, same constraint every UI-adjacent entry
in this file notes - not live-verified in a running Blish HUD/GW2
client this session (browser automation requires asking the user
first, per this repo's own rule, and was not requested this session).
No `PersistedPlan` schema bump: `RequiredRecipe.IsLearnedFromItem` and
`PlanRowViewModel.WikiUrl` are both additive with safe defaults
(`false`/`null`) - the same precedent `IsCostComponent`'s own doc
comment already established - and `PlanRowViewModel` itself is never
persisted at all (only `CraftingPlanResult`, which
`PlanViewModelBuilder.Build` rebuilds fresh on every render/restore).
IDs remain internal-only throughout; coin icons unaffected (no
coin-rendering code touched by any of the three features).

Gate: MIXED 2026-08-16 (orchestrator live desktop session). Status own-row PASS (full-width, dated failure status, no button collision); wiki-link affordance PASS-visual ('Right-click: Open wiki page' tooltip renders on rows; the actual browser launch deliberately untested with guests present); receipt/what-if captions FAIL - they do not render in the reachable override-re-solve state (ARE vendor flip showed both child groups uncaptioned); root-cause + fix in flight on branch gate-fixes.
