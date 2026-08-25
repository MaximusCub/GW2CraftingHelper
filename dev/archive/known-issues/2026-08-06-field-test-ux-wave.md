> **Frozen record - 2026-08-06, branch `field-test-ux-wave`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Field-test UX wave (six S-sized display fixes, 2026-08-06)

Six pre-investigated display-layer fixes from a live field-test pass over
the recipe tree; every finding was root-caused against master (e2e0936)
before implementation. All six commits: display-layer only, no
InventoryReducer/PlanSolver/VendorBatchSolver changes.

**A. Owned/needed pill clarity.** The tree row's "Nx" prefix shows the
post-reduction REMAINING need, so the "USING N OWNED" pill alongside it
read as a paradox in the field (a large owned count next to a smaller
remaining-need number looked contradictory). `DecisionPillPlanner.
AppendOwnershipPills` now shows "USING {used} OF {total} OWNED"
(total = OwnedQuantityUsed + Quantity, per CraftingTreeNode's own
documented contract) plus an explanatory hover tooltip.

**B. Non-coin vendor unit price + currency name tooltips.** A pure-
currency vendor offer (spirit shards, karma, ...) has UnitCost == 0 (not
null), so the tree row's "Unit price:" tooltip rendered a misleading
"0g 0s 0c" and silently dropped the real currency cost. Added
`CurrencyDisplayResolver.ResolveTreeNodeUnitAmounts` (a total/quantity
approximation with the same "N for M" bundle fallback as
ResolveUnitAmounts - CraftingTreeNode carries no per-offer batch data, so
this is not the shopping list's true per-batch rate). Separately, swept
the currency-icon-only cost cells (tree cost column + shopping list
Each/Total, both routed through `CoinCurrencyRenderer.
LayoutCurrencySegments`) to add a hover tooltip naming the currency,
per the field-test principle "anywhere a currency icon shows, the name
must be available."

**C. Pill border contrast.** Three pill border colors measured below the
3:1 WCAG non-text contrast minimum against white (Selected 2.31:1,
OwnedInfo 2.42:1, active-Ignore 2.10:1). Darkened to 4.21:1/4.90:1/4.29:1
respectively, same hue, same fill/text treatment otherwise.

**D. Pill tooltip coverage.** Interactive pills already set
`outer.BasicTooltipText`, but the inner fill panel and its label cover
almost the entire pill (outer is only a 1px border ring), so the tooltip
was swallowed whenever the cursor was over inner/label instead of the
thin outer ring - the user never saw it live (labels capture mouse, the
same M32 lesson). Tooltip text is now stamped onto outer/inner/label
together. Also extended tooltip coverage to four previously tooltip-less
pill kinds: Selected, Have, OwnedInfo (finding A's text), and
AchievementBitDeduped (explains the COUNTED ELSEWHERE semantics per
#26).

**E. Mystic Forge is a facility, not a discipline (user-approved).** The
forge has no rating requirement and nothing to unlock, so it no longer
appears in the Required Disciplines section (joins the existing
Achievement/Merchant filter in `PlanResultBuilder.
NonCraftingDisciplines`) - supersedes the M37-era comment/tests that
deliberately kept it in that list. Its step/recipe sublabel used to
render "MysticForge 0" (the internal id string, plus a meaningless
rating); `FormatDisciplineSublabel` now shows "Mystic Forge" with no
level number for the sole-facility case.

Validation: `dotnet build -p:Platform=x64` clean (0 errors); both test
suites green - module suite 1114 passed (was 1101; +13 new tests across
DecisionPillPlannerTests/CurrencyDisplayResolverTests/
PlanViewModelBuilderSublabelTests), VendorOfferUpdater.Tests 135 passed
(untouched, unaffected). No new Blish HUD references in tests; all new
tests exercise real production code (DecisionPillPlanner,
CurrencyDisplayResolver, PlanResultBuilder, PlanViewModelBuilder) with no
contract-mirror/fake-logic tests. Pure-Blish view code (PillColors,
TreeSectionController's pill rendering, IconControls, CoinCurrencyRenderer)
has no new tests per the Blish-free-tests invariant - covered by the live
desktop gate instead.

Live desktop gate: PASS (orchestrator, 2026-08-06, live branch-build
sandbox session under the hardened desktop protocol, captures uxg_01-11
in preflight/captures; Exordium plan with the synthetic snapshot, Use Own
Materials on, tree fully expanded):
- Finding A + final wording: the partial-coverage pill renders
  "HAVE 30/75 NEEDED" live (45x Stabilizing Matrix row: needs 75, owns 30)
  directly above a fully-covered row's plain blue "HAVE" (Mystic
  Runestone) - the vocabulary alignment reads exactly as the maintainer
  intended; hover shows the approved tooltip verbatim: "Needs 75 total -
  30 covered by your materials, 45 left to acquire".
- Finding B1: hovering a non-coin vendor row's price area shows
  "Unit price: 92 for 912 Spirit Shard" (Philosopher's Stone; the
  N-for-M bundle fallback for a non-divisible rate, currency NAME
  included) - the misleading "0g 0s 0c" is gone. (B2 icon-name tooltips
  verified by review; the same name-threading resolver is proven live
  by B1.)
- Finding C: pill border colors pixel-sampled from captures - selected
  green renders (30,143,11) vs target #1F8F0C, owned-info gold matches
  the darkened #8A6D1F range (269 px in-range), ignore-active amber
  renders (156,115,38) vs target #9C7327. All three darkened borders
  live; fill technique and white text untouched.
- Finding D: hovering an UNSELECTED VENDOR pill shows "Switch to VENDOR"
  with a hover highlight - the previously-swallowed label hover now
  fires; the OwnedInfo tooltip above proves the previously tooltip-less
  kinds are covered.
- Finding E: "Required Disciplines (3)" lists Armorsmith 450 / Jeweler
  225 / Weaponsmith 500 - MysticForge row GONE, count consistent; recipe
  sublabels render "Mystic Forge" (space, no level) while real
  disciplines keep levels ("Gift of Condensed Magic / Mystic Forge" seen
  alongside "Weaponsmith 400" rows).
- Ignore round-trip on the new pill layout: IGNORE on the partial-HAVE
  row -> HAVE + IGNORED (amber), ancestor costs updated; un-ignore
  restores "TP | HAVE 30/75 NEEDED | IGNORE" and all costs exactly.
- Session sweep: zero FATAL, zero relayout warnings.

---
