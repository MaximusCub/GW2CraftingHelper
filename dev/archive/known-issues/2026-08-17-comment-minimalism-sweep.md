> **Frozen record - 2026-08-17, branch `comment-minimalism-sweep`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Comment-minimalism sweep (repo-wide, comment-minimalism-sweep branch)

Repo-wide pass applying the repo's comment bar: a comment survives
only if it states a constraint the code cannot express, once, in 1-2
lines. Review-round narration, gate/round markers, dates, milestone
tags, and superseded-design retellings deleted; genuine invariants kept
trimmed at one canonical site. Comments and blank lines only - verified
code-identical (comment-stripped before/after diff clean per commit).

**Per-area line deltas (files touched, comment lines +added/-removed;
measured via `git diff --numstat` against the master merge-base,
including the findings-fix and verification passes below):**

- Services (incl. Diagnostics/Recipes): 75 files, +1877 -4658
- Models: 31 files, +369 -853
- Views (incl. Rendering): 25 files, +855 -1682
- Module.cs: 1 file, +149 -426
- Contracts: 1 file, +4 -4
- tools (VendorOfferUpdater/RecipeSeeder/Harness/MysticForgeSeeder):
  9 files, +99 -175
- tests (module + updater suites): 118 files, +569 -617
- Total: 260 files, +3,922 -8,415, net -4,493 lines. 12 touched files
  had non-ASCII bytes (em-dashes/arrows/box glyphs) converted to ASCII,
  including Contracts/IItemSearchProvider.cs (missed by the original
  sweep, fixed in the findings-fix pass); a repo-wide
  `grep -P '[^\x00-\x7F]'` over every `.cs` file now returns zero
  hits. The flagged InMemoryRecipeCacheStore em-dash was already fixed
  on origin/master - the remaining instances of that class were in
  Services/Recipes and tests, all fixed here.

**Invariants relocated / canonicalized (from -> to):**

- CompetencyOpportunityCalculator.Apply rationale: three byte-identical
  4-line blocks in CraftingPlanPipeline reduced to one-line
  "annotation-only" notes; canonical contract stays on the calculator's
  own class doc.
- CraftingPlanPipeline Solve()-overload twin blocks (Step 5.5/5.6,
  seasonal filter, force-buy pre-pass, metadata widening): stated once
  at the single-item overload; multi-item overload reduced to "see the
  single-item overload's matching X" pointers.
- PlanSolver comparability-tier rationale: canonical at
  Decision.ComparisonValue/HasUnvaluedCurrency and the Evaluate
  vendor/recipe tier blocks; restatement sites (SolverDecision,
  CraftingTreeNode, CraftingPlanResult, CompetencyOpportunity,
  CompetencyOpportunityCalculator) trimmed to short passthrough notes.
- AccountCurrencyIndex: gw2efficiency provenance litigation deleted;
  the display-only/never-affects-decisions invariant kept as the class
  doc.
- DecisionPillPlanner: three successive pill-wording narrations reduced
  to the current wording only.
- No invariant was orphaned: every deletion either had a surviving
  canonical statement (owning class doc, ARCHITECTURE.md sections 4/5/7/8,
  or this file) or was process metadata with no invariant content.

**Kept intact (verified):** PlanContentHeightMath's one-level-per-frame
AutoSize convergence class doc; WheelDeltaSanitizer's root-cause/
threshold contract doc; ISectionRelayoutSink's seam contract;
CurrencyDecisionDefaults' MIT permission notice; scroll
preserve-restore-verify invariants in CraftingPlanView (comment edits
only, no code touched in the files pinned by expensive evidence).

**Flagged suspect claims (not rewritten; verify separately):** none
found - no surviving comment's technical claim was determined false
during the sweep. Two former false claims were already self-documented
as corrected upstream (Gw2Constants festival-table scoping,
VendorOfferUpdater round-trip save ordering) and were compressed to
their corrected form.

**Findings-fix pass (post-review):** the review of the sweep found the
original commits had (a) missed Contracts/ entirely (em-dashes in
IItemSearchProvider.cs), (b) reported three mutually inconsistent
line-delta totals (the numbers above are now measured, not summed by
hand), (c) left review-narration labels ("gate finding", "fix-pass",
round markers, dates) and M##/WP-## process tags in both touched and
untouched files, and (d) in a few places dropped a KNOWN-ISSUES pointer
while keeping the milestone tag. All four were corrected in one
follow-up commit: milestone/process tags and review-round narration
removed repo-wide from `.cs` comments (doc-section pointers such as
"KNOWN-ISSUES #26" or "docs/KNOWN-ISSUES.md's W4A entry" kept), dates
removed from comments, and the dropped pointers restored. The pass was
comment-only (every changed line is a comment or a `#region` label).

**Validation:** build 0 errors after every batch; module suite
1827/1827 after every batch (and after the findings-fix pass); updater
suite 207/207 after the tools and tests batches (and after the
findings-fix pass).

Gate: PASS (comments and whitespace only - the review pass verified
the diff contains no code-token change, so no sandbox check applies;
evidence is the no-semantic-change check plus both suites green at the
verification pass - module 1827/1827, updater 207/207, build 0
errors).
