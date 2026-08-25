## Tree row tooltip composer extraction + architecture doc corrections (2026-08-17)

**Milestone goal:** apply quorum verdict D-2 (TARGETED_FIX_ONLY): correct
and extend `docs/ARCHITECTURE.md`'s TreeSectionController state/render
split entry (record the split as rejected by decision, not deferred;
correct the coupling figure), add a STANDING RULE line for future
tree-row/pill features, extract the Recipe Tree row's extra-tooltip-line
construction into a pure, Blish-free `Services/` composer with real-path
tests, and fold in quorum verdict D-1's targeted `FrameTicker`/line-count
doc corrections plus the `CraftingPlanView.FormatPhaseText` -> `Services/`
move. No `TreeSectionController` state/render split and no
`PlanStripController` were implemented - both are explicitly rejected by
this verdict, not merely out of scope.

**What changed:**
1. **`docs/ARCHITECTURE.md` section 5 (TreeSectionController entry).**
   Added a new "TreeSectionController state/render split: rejected by
   decision, not deferred by oversight" paragraph beside the existing
   TreeSectionController bullet: states the one-owner/one-lifetime
   invariant, the constructed-once-in-`CraftingPlanView`'s-constructor
   fact (`Views/CraftingPlanView.cs` ~614), the preferred future shape
   (Blish-free pure composers under `Services/`, never a class bisection),
   and the measured coupling figure - `TreeSectionController` is
   mentioned by name in 14 production `.cs` files (`Module.cs` plus 9
   `Services/` files and 4 `Views/` files, not counting `Models/`
   shape-mirroring comments or test files), of which 13 are comment-only;
   the actual compile-time coupling is 2 references, both in
   `Views/CraftingPlanView.cs` (the field declaration and the constructor
   call site) - plus, as measured pre-change at `ce64423`, 3 mentions in
   `docs/ARCHITECTURE.md` and `docs/ROADMAP.md` (`docs/KNOWN-ISSUES.md`
   carried 42 more as historical narrative) - not 18. (That doc-mention count is a snapshot, not a live figure: every doc entry that names the class (including this one) adds mentions on landing, so no post-change total is stated here - reproduce the current count with `git grep -c TreeSectionController -- '*.md'`.) (The file count moved
   from a pre-change 13 to 14 because this same milestone's own new
   `TreeRowTooltipComposer.cs` documents its provenance with a
   "TreeSectionController.RenderTreeNode" reference in its own doc
   comment - a real, verified `grep` count taken after the change, not
   the pre-change snapshot.)
2. **`CONTRIBUTING.md` Code Style section.** Added the STANDING RULE: every
   new tree-row/pill feature extracts its pure text/decision computation
   into a tested, Blish-free `Services/` composer BEFORE wiring it into
   `TreeSectionController` - the 8-for-8 proven pattern (now including
   `TreeRowTooltipComposer`), with an explicit note that this is not a
   staged step toward eventually splitting `TreeSectionController` -
   that split is rejected by decision, cross-referenced to the
   `docs/ARCHITECTURE.md` entry above.
3. **`Services/TreeRowTooltipComposer.cs` (new) + real-path tests
   (`TreeRowTooltipComposerTests.cs`, 24 test methods / 28 test cases).**
   Extracted the `extraTooltipLines` build (`Views/Rendering/
   TreeSectionController.cs`, formerly ~726-940) verbatim into a static,
   Blish-free `BuildExtraTooltipLines(node, captionText, currentPlan)`
   method: the qty>1 unit-price line(s) (including the Field-test finding
   B zero-coin-with-currency-cost suppression), the AUDIT ROW 20/38
   TP-price-side-fallback caveat (including the b18fb03 null-plan/
   PriceBasis hazard class - a null `currentPlan` gets a basis-agnostic
   sentence rather than silently reading `null?.PriceBasis` as `false`),
   the Unknown/GuildUpgrade acquisition hint, the receipt/what-if caption
   insert-at-front, and the wiki-link tooltip line. The actual Blish-bound
   right-click event wiring (`RightMouseButtonPressed`/`MouseLeft`/
   `RightMouseButtonReleased`) stays in `TreeSectionController.
   RenderTreeNode`, gated by the same `WikiLinkBuilder.HasWikiPage`
   predicate the composer also calls - calling that cheap pure predicate
   twice per row is deliberate (keeps the composer Blish-free rather than
   threading a bool back out for one call site). `FormatCoin` deliberately
   duplicates `CoinCurrencyRenderer.FormatCoinText`'s format rather than
   referencing it, matching `ValueDetailTooltipBuilder`'s own precedent
   and stated rationale (that class lives in `Views.Rendering` and is
   Blish-coupled). The pills cascade (`RenderDecisionPills`, ~1252-1499
   pre-change) was explicitly NOT touched - out of scope per the verdict
   (interleaved click wiring).
4. **The pills-cascade sibling (`PlanStripTickDecision.cs`) gained
   `FormatPhaseText`.** `CraftingPlanView.FormatPhaseText` (private
   static, ~2976-2982 pre-change, pure - no Blish/instance-state
   dependency) moved verbatim into the existing pure, Blish-free,
   already-unit-tested `Services/PlanStripTickDecision.cs`, alongside its
   sibling status-strip decision `Decide`. No `PlanStripController` class
   was created - rejected by verdict D-1. `CraftingPlanView`'s sole call
   site (`_statusBoard.UpdatePhase(myGen, (int)pe.Phase,
   FormatPhaseText(pe))`) now reads
   `PlanStripTickDecision.FormatPhaseText(pe)`. Six new tests added to
   `PlanStripTickDecisionTests.cs` cover null event, empty display name,
   the Total-present branch, the documented Detail-fallback regression
   case (the first-run "Building recipe tree" hint - the ONLY surviving
   path for that hint once `CraftingPlanView` started passing
   `progress: null` to the old `IProgress<PlanStatus>` channel, per
   `CraftingPlanPipeline.FirstRunTreeHint`'s own doc comment), the
   Total-takes-priority-over-Detail ordering, and the plain-ellipsis
   fallback. Every stale `CraftingPlanView.FormatPhaseText` cross-reference
   left behind by the move (`Services/CraftingPlanPipeline.cs` x2,
   `Services/PlanPhaseEvent.cs`, `tests/.../CraftingPlanPipelineTests.cs`)
   was corrected to `PlanStripTickDecision.FormatPhaseText` (fix the
   class, not the instance) - `docs/KNOWN-ISSUES.md`'s own historical W3B
   narrative entry was deliberately left as-is (an accurate record of
   that milestone's state at the time, not a live cross-reference).
5. **Quorum verdict D-1 fold-in, `docs/ARCHITECTURE.md` section 1
   (`FrameTicker`).** Corrected "three live instances" to "FOUR live
   instances (measured)": `_spinnerTicker` (the W3B status-strip spinner
   ticker, added between the section's original writing and now) is a
   fourth `FrameTicker` field on `CraftingPlanView`, canceled/nulled by
   `StopLiveTickers()` alongside the other three. Added a matching
   follow-up sentence to `docs/KNOWN-ISSUES.md`'s `CraftingPlanView`
   tab-switch hazard-class row (the table entry originally recorded via a
   2026-08-06 review correcting a first sweep pass's "no live race"
   miss) noting the row's own three-ticker count was itself stale by
   the time of this pass, and that `_spinnerTicker` sits in the identical
   hazard class as the other three (not independently verified live -
   same OUT OF SCOPE deferral as the rest of that row).
6. **Quorum verdict D-1 fold-in, `docs/ARCHITECTURE.md` section 5
   (stale line-count figure).** The "~2,802 lines" post-WP-26 figure is
   now explicitly scoped as "at the time WP-26 was cut" rather than
   read as still-current, with a new "Measured current" sentence stating
   `Views/CraftingPlanView.cs`'s real current line count (3,674 lines,
   2026-08-17) and explaining the growth as expected feature/fix landing
   (W3B status strip, currency-ux-package, gate-round fixes, this
   milestone's own extraction) routed through the STANDING RULE on the
   way in, not a regression of the WP-21 through WP-25 decomposition.

**Validation performed:**
- Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
  C:/Dev/Blish/wt-qtooltip/GW2CraftingHelper.csproj -p:Platform=x64
  -t:Rebuild` - 0 errors, 1781 StyleCop warnings, none newly introduced in
  any touched file (`Services/TreeRowTooltipComposer.cs`, `Views/Rendering/
  TreeSectionController.cs`, `Services/PlanStripTickDecision.cs`,
  `Views/CraftingPlanView.cs`, `Services/CraftingPlanPipeline.cs`,
  `Services/PlanPhaseEvent.cs`) - one transient SA1512 warning
  (single-line comment followed by blank line) introduced by the
  `FormatPhaseText` removal was caught and fixed before the final build.
  (This entry previously claimed 1782 pre-existing warnings with none
  newly introduced; an independent `-t:Rebuild` found that claim false in
  two ways. First, the underlying `c1c52e3` commit's "0 warnings, 0
  errors" was an incremental-build artifact - `dotnet build` without
  `-t:Rebuild` reports "Nothing to do" when nothing changed since the
  last build, not a true warning count. Second, a real `-t:Rebuild`
  surfaced 6 warning sites in the two changed pure files:
  `Services/PlanStripTickDecision.cs` (52,65)/(86,69)/(87,36)/(88,51)
  SA1503 and `Services/TreeRowTooltipComposer.cs` (67,18)/(225,14)
  SA1513. Five are the same warnings moved verbatim from their
  pre-extraction locations (the four `FormatPhaseText` single-line `if`s
  and the (67,18) construct, character-identical to `ce64423`'s
  `TreeSectionController.cs:740-744`), so the total warning count was
  unaffected by their relocation. The sixth, (225,14) in the new private
  `FormatCoin`, was on genuinely new code - a multi-line
  `if (copper < 0) { copper = 0; }` missing the blank line after its
  closing brace that `CoinCurrencyRenderer.FormatCoinText`'s single-line
  form never needed - and has been fixed by adding that blank line,
  dropping the true total from 1782 to 1781.)
- Tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test
  C:/Dev/Blish/wt-qtooltip/tests/GW2CraftingHelper.Tests/
  GW2CraftingHelper.Tests.csproj` - 1802/1802 green (baseline 1768 + 34
  new: 28 `TreeRowTooltipComposerTests` cases, 6
  `PlanStripTickDecisionTests` cases; independently re-run and confirmed
  at HEAD). Two tests failed on first run (the two null-caption/null-hint
  composer tests did not account for the default test-fixture item name
  also triggering the wiki-link line) and were corrected before the final
  green run. (This figure previously read 1799/1799 with 25
  `TreeRowTooltipComposerTests` cases; the c1c52e3 follow-up commit added
  3 more branch-coverage cases to that file without updating this record
  - the same stale-validation defect class the ARCHITECTURE.md coupling
  figure above was corrected for.)
- Manual: `git status --short` confirmed no intermediate cache files
  (`ref/wiki_vendor_cache.json`/`ref/item_id_cache.json`) were touched; a
  full-diff ASCII scan (`grep -P '[^\x00-\x7F]'`) over every file this
  pass touched confirmed no non-ASCII bytes (and therefore no em-dashes).

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests (`TreeRowTooltipComposerTests`/
  `PlanStripTickDecisionTests` use only `Models`/`Services` types)
- [x] Tests exercise real production paths (`TreeRowTooltipComposer.
  BuildExtraTooltipLines`/`PlanStripTickDecision.FormatPhaseText` are the
  exact methods `TreeSectionController`/`CraftingPlanView` now call)
- [x] No fake file I/O tests introduced
- [x] Pricing logic preserves multi-source correctness (no solver/pricing
  logic touched - pure text-composition code motion plus documentation)
- [x] IDs remain internal-only (not displayed) - the composer's tooltip
  lines carry only names/amounts, matching the pre-move code verbatim

**Risks / follow-ups:** the `TreeSectionController` pills cascade
(`RenderDecisionPills`) remains un-extracted, unchanged from before this
milestone - explicitly out of scope per the verdict (interleaved click
wiring), a candidate for a future STANDING-RULE-following pass of its
own. `docs/ROADMAP.md`'s own `TreeSectionController` mention was not
independently re-verified against the new coupling figure beyond the
`grep` count already cited above.

Gate: not run live this pass - pure extraction of tooltip composition
with the emitted line content pinned by the suite (1802 green at the
stream's verification, re-run post-merge below); the visual surface is
unchanged by construction, and the next desktop gate batch covers
tooltips incidentally through its deferred value-detail hover check.
Merged under the maintainer's standing merge directive (2026-08-16).
