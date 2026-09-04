> **Frozen record - 2026-08-16, branch `currency-ux-package-defaults-plan-scope-pills-value`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Currency UX package: defaults, plan-scope pills, value-detail hover (2026-08-16)

Three approved currency-UX features shipped as one coherent
package on branch `currency-ux-package`, in three feature-sized commits.

**Feature 1 - default currency decision values.** Ships gw2efficiency's
`CURRENCY_DECISION_PRICES` table (MIT-licensed, attributed - see
`Models/CurrencyDecisionDefaults.cs`'s header comment and
`docs/research/gw2e-currency-decision-prices.md`'s full provenance writeup
from the extraction run) as curated default decision-only currency
valuations - a deliberate, one-time waiver of the "no invented
data" rule for this specific table only. `CurrencyValuation` gained a
three-state precedence (user-set value, else explicit clear, else
curated default, else none) via `TryGetEffectiveCopperValue` and a
persisted `ClearedCurrencyIds` set, so a user can suppress a default and
have it stay suppressed rather than resurrecting on next load.
`CurrencyValuationSerializer`'s persisted JSON shape grew a `"Cleared"`
array alongside `"Values"`, with full backward compatibility for the old
flat-dict format (verified via round-trip tests reading the pre-existing
shape).

Design decision, found only after the first implementation attempt broke
8 pre-existing tests: `PlanSolver.Solve`'s top-level entry already
coalesces a `null` `currencyValuation` argument to `CurrencyValuation.None`
internally (line ~204) before threading it through `Evaluate`/
`EvaluateVendorOffers` - so wiring `TryGetEffectiveCopperValue` directly
into the solver's currency-lookup call sites would have applied curated
defaults to *every* solver/pipeline test in the suite, not just real
settings-driven callers, since `CurrencyValuation.None` is the exact same
static singleton both a bare unit test and an unconfigured real user's
settings produce. Reverted that approach; defaults are instead applied at
exactly one seam - the new `ModuleSettings.GetEffectiveCurrencyValuation`,
whose sole caller is `Module.cs`'s Generate button handler - so every
existing solver/pipeline test that constructs its own `CurrencyValuation`
(or omits one, or reads `ModuleSettings.GetCurrencyValuation`'s raw form)
is completely unaffected. This is why Feature 1's own commit left the
1410-test baseline byte-for-byte unmodified (+25 new tests only, no
existing test needed a currency-id swap to avoid an unintended default) -
scoped to Feature 1 alone; see the correction below for the package as a
whole, since Feature 2 did intentionally rewrite one pre-existing test.

**Feature 2 - plan-scope currency pills.**
Every currency leaf row in the Recipe Tree (ordinary `Currency`-decision
nodes and W4B vendor cost-component currency leaves alike) now gets a
`HAVE {have}/{planTotal} TOTAL` pill alongside its existing `CURRENCY`
tag - `have` = whole-plan wallet holding, `planTotal` = the whole plan's
need for that currency (`plan.CurrencyCosts`), both plan-scope facts
(deliberately no per-row allocation), so the identical pill text is
truthful at every tree occurrence of the same currency id. Full coverage
collapses to the plain blue `HAVE` pill (matching item-pill vocabulary);
the pill is omitted entirely (not shown as `HAVE 0`) when no wallet
snapshot is available. A currency-type cost-component leaf's row-scope
`OWN n` badge is replaced by this same plan-scope pill; an item-type
cost-component leaf's `OWN n` badge is unchanged, out of this feature's
scope. `PlanViewModel` gained `CurrencyPlanTotals`/`OwnedCurrencyAmounts`
passthroughs; `DecisionPillPlanner.BuildPillSpecs` gained two optional
parameters (default `null`, so every pre-existing call site/test is
unaffected).

**Feature 3 - value-detail hover on CRAFT/VENDOR pills.** Duplicated
verbatim from gw2efficiency's own crafting-pill tooltip template: when a
node's decision-only comparison figure diverges from its displayed real
gold cost, its committed CRAFT/VENDOR pill gains a hover block
("Crafting gold price" / "Currencies" delta with its opportunity-cost
caption / "Optimization price", plus a "Vendor cap: N per day/week" line
when the winning vendor offer carries one - per the #21
resolution). Plumbing: `SolverDecision`/`CraftingTreeNode` each gained a
`ComparisonValue`/`DecisionValue` passthrough of an internal figure
`PlanSolver.Evaluate` already computed and discarded (`Decision.
ComparisonValue`, private) - no new solver math, purely a public-surface
passthrough of an already-correct, already-recursive rollup. Both new
fields are explicitly documented DECISION-ONLY at every hop. The actual
tooltip text/divergence decision lives in the new Blish-free
`ValueDetailTooltipBuilder`, matching this repo's established pattern of
keeping tree-rendering logic unit-testable outside `TreeSectionController`.
Audited (not assumed) that "currency names available wherever an icon/
amount appears" was already satisfied by prior work (field-test finding
B's tooltip sweep, `IconControls.CreateItemIcon`'s `tooltipText`
parameter) - no changes needed for that sub-requirement.

**Cross-feature interaction checked:** a currency-type cost-component
leaf's `Locked` (`CURRENCY`) pill has `node.Decision == BuyFromVendor`,
which matches Feature 3's pill-kind/decision gate - but that leaf's
`DecisionValue` is never set (`CraftingTreeBuilder.BuildVendorCostComponentLeaves`
constructs those nodes directly, bypassing the `BuildNode` path that
sets it), so `ValueDetailTooltipBuilder.TryBuild`'s null guard silently
declines rather than attaching an incorrect tooltip - verified by
tracing the construction path, not merely asserted.

Repo invariants: ASCII-only .cs (checked via `perl -ne 'print if
/[^\x00-\x7F]/'` over every new/edited file across all three features -
zero hits); no em dashes (same sweep); Allman braces; new `.cs` files
registered in `GW2CraftingHelper.csproj`'s explicit `<Compile Include>`
list (`CurrencyDecisionDefaults.cs`, `ValueDetailTooltipBuilder.cs` -
the test project is SDK-style/globbed, so its two new test files needed
no registration); no Blish HUD/BlishHUD.exe/Gw2Sharp references in any
test file (grepped); tests exercise real production paths, no contract
mirrors; IDs remain internal-only (pill/tooltip text shows names/counts,
never raw currency ids); coin icons unaffected (no coin-rendering code
touched); currency valuations stay decision-only and are restated as
such in a comment at every place a decision-only figure surfaces near
display code (`CurrencyDecisionDefaults`, `CurrencyValuation`,
`ModuleSettings`, `Module.cs`, `SolverDecision`, `CraftingTreeNode`,
`ValueDetailTooltipBuilder`); `Services/ModuleLog.cs`,
`PlanContentHeightMath`, `PlanRelayoutMath`, scroll machinery, and
`VendorBatchSolver`'s merged-ceil batching math were never touched -
`VendorBatchSolver.cs` itself has zero diff across all three commits
(Feature 3's vendor-cap line only ever *reads* `CraftingPlan.
TimegatedItems`, an output that solver already computes).

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - clean,
0 errors, no new warnings in any new/edited file. Tests: 1410 (baseline)
-> 1435 (Feature 1, +25) -> 1446 (Feature 2, +11) -> 1470 (Feature 3,
+24), all green at every step (`dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj`). No live desktop verification was
performed - `Views/SettingsTabContent.cs` and
`Views/Rendering/TreeSectionController.cs` are Blish-bound and outside
this repo's test-runnable surface, same constraint every UI-adjacent
entry in this file notes; the Settings tab's new default/Clear-checkbox
layout in particular (new pixel columns past the existing `ErrorX`) has
not been visually confirmed to fit within a real Settings window at
typical widths.

**Correction (currency-ux-package review fix, finding 6, MEASURED):** two
claims above are false as written for the package as a whole. (1) The
"byte-for-byte unmodified" wording above is scoped to Feature 1's own
commit only and is accurate there, but Feature 2's commit (`c7bac28`)
intentionally rewrote one pre-existing test - `DecisionPillPlannerTests.cs`
gained a `+172/-3` diff, and
`CostComponent_CurrencyType_WithOwnership_ShowsBothBadgesTogether_CurrencyFirst`
was renamed to `..._WithRowScopeOwnershipOnly_ShowsOnlyCurrencyBadge` with
its assertions rewritten, because Feature 2 replaces the currency
component's own row-scope `OWN n` badge with the new plan-scope pill (see
Feature 2's own paragraph above) - a behavior change absorbed into an
existing test, not preserved unmodified. (2) The final test count stated
above (1470) is stale: the review-fix commit `a49ba19` added tests
without updating this entry (bringing the count to 1478), and a further
review-fix round (finding 5, adding real-production-path coverage for the
merged-vendor valued-currency ComparisonValue shape) brings the measured
count on this branch to 1479. Both corrections per this repo's measured/
inferred/guess discipline - an entry that overstates baseline preservation
is worse than none.

**Nice-to-have note (currency-ux-package review fix):** a persisted plan
snapshot's `PlanSolveContext.CurrencyValuation` stores the POST-`WithDefaults`
materialized valuation (curated defaults baked in at solve time, not
re-resolved on load - round-trip verified working). A user who later
clears a currency default in Settings will still see the OLD default
applied when that already-saved plan is re-solved locally, until the plan
is regenerated fresh. This is consistent with how a user-set override
value already behaves in a saved snapshot (both are frozen at solve time),
so it is arguably correct snapshot semantics rather than a bug - noted
here so it is not rediscovered as one.

Gate: PASS with one deferred slice 2026-08-16 (live desktop session). Plan-scope currency pills verified live in collapsed full-coverage form (plain HAVE on all covered leaves incl. W4B component leaves - the unified vocabulary); Settings defaults suite-covered; value-detail hover: all three suppression paths verified live correct (unvalued ARE, component leaves, non-diverging rows) but no positive render occurred in the tested shapes - a propagation question (vendor-child currency valuations folding into a craft root's DecisionValue) is under investigation on branch gate-fixes.
