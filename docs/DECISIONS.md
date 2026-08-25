# Decisions: designs considered and rejected

[`ARCHITECTURE.md`](ARCHITECTURE.md) describes what the module *is*, in the
present tense. This file holds the other half: refactors and designs that
were proposed, evaluated, and deliberately not done. They live here so the
architecture doc can stay a map instead of turning into a rebuttal brief,
and so a reader who arrives at one of these ideas independently can find
out whether it was rejected on merit or simply never considered.

Each entry names the reasoning and, where one exists, links the dated
record. An entry is a decision, not a law: bring new evidence and it can be
reopened.

---

## Splitting `TreeSectionController` into a stateful pair

**Proposal:** bisect `Views/Rendering/TreeSectionController.cs` into a
stateful collaborator (owning `_nodeOverrides`, `_ignoredItemIds`,
`_nodeExpansion`, `_treeNodeStates`) and a separate stateless renderer,
mirroring the six per-render section renderers extracted alongside it in
M38.

**Rejected.** The invariant this class exists to hold is one owner, one
lifetime. It is constructed once in `CraftingPlanView`'s own constructor
rather than freshly per render precisely because its override state must
survive a local pill-click re-solve, and a two-class split would either
duplicate that lifetime management across both halves or reintroduce a
second implicit owner - the same class of bug the single-owner primitives
in [`ARCHITECTURE.md`](ARCHITECTURE.md) section 1 exist to prevent, at the
object-graph level instead of the thread level.

The coupling argument does not favour the split either. Measured
pre-change at `ce64423`, the class is named in 14 production `.cs` files
(`Module.cs`, 9 under `Services/`, 4 under `Views/`), of which 13 are
comment-only; the actual compile-time coupling is 2 references, both in
`Views/CraftingPlanView.cs` (the field declaration and the constructor call
site). A state/render split would relocate half of that across a new seam
rather than shrink it. Doc mentions are a moving target for the same reason
- every entry naming the class, including this one, adds to the count - so
reproduce it with `git grep -c TreeSectionController -- '*.md'` rather than
citing a figure.

**The accepted alternative** is not a class bisection: per the STANDING
RULE in [`../CONTRIBUTING.md`](../CONTRIBUTING.md), each new tree-row or
decision-pill feature extracts its pure text/decision computation into a
Blish-free, unit-tested composer under `Services/` first -
`TreeRowTooltipComposer` is the latest instance of the pattern already
behind `DecisionPillPlanner`, `ValueDetailTooltipBuilder`,
`PillSubduingEvaluator`/`PillSubduingTooltipBuilder` and
`ReceiptCaptionHelper` - and keeps wiring it into `TreeSectionController`'s
existing single-owner shape.

**Record:**
[`archive/known-issues/2026-08-17-tree-row-tooltip-composer-extraction-architecture.md`](archive/known-issues/2026-08-17-tree-row-tooltip-composer-extraction-architecture.md).

---

## An `ApplyAll` seam over the post-solve annotation passes

**Proposal:** collapse the three producer call sites that invoke the four
post-solve annotation calculators
([`ARCHITECTURE.md`](ARCHITECTURE.md) section 10) into a single `ApplyAll`
entry point, so a fifth pass would be one edit instead of four.

**Rejected as premature.** The four calculators do not share a signature -
they take different inputs (`learnedRecipeIds`, `vendorOffers`,
`characterDisciplines`) - so a shared seam would need its own parameter
object carrying the union of them, which no caller needs today. The cost
being avoided is one extra edit site on a change that happens rarely; the
cost being added is a permanent abstraction that every future pass has to
fit. Revisit if a fifth pass arrives and its inputs are already covered.

**Record:**
[`archive/known-issues/2026-08-17-annotation-detection-post-solve-advisory-list.md`](archive/known-issues/2026-08-17-annotation-detection-post-solve-advisory-list.md).

---

## WP-26: extracting the scroll/resize/wheel controller

**Cut on 2026-07-23**, after five sibling extractions in the same wave
landed. Full reasoning in [`ARCHITECTURE.md`](ARCHITECTURE.md) section 5,
where it stays because it explains why that machinery is still in
`Views/CraftingPlanView.cs` today - it is present-tense mechanism, not only
history.

---

## M38's `Services/` foldering target

**Proposal:** split the flat `Services/` directory into `Services/Pricing/`,
`Services/Planning/`, `Services/Persistence/`, `Services/Vendor/`,
`Services/Layout/` and `Services/Api/`, as set out in
[`dev-notes/m38-plan/m38-a1-architecture.md`](dev-notes/m38-plan/m38-a1-architecture.md)
section 5.

**Not executed.** None of the six directories exists; `Services/` is 141
flat files with two subdirectories (`Recipes/`, `Diagnostics/`) that arrived
for other reasons. Because `GW2CraftingHelper.csproj` lists every file
explicitly, each move is also a csproj path edit, and the plan's own
sequencing note flagged high merge-conflict potential against the branches
in flight at the time. The branches kept arriving, the move never had a
quiet window, and the benefit is navigational rather than behavioral - which
[`README.md`](README.md)'s code-layout table now delivers at zero merge
cost. Recorded in the DEFERRED list in
[`KNOWN-ISSUES.md`](KNOWN-ISSUES.md); still available to a future wave that
wants it.
