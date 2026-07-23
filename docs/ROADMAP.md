# GW2CraftingHelper - Roadmap

> Supersedes all prior roadmap revisions (the detailed per-milestone
> planning template this document used through M17 is preserved in git
> history; this revision is a short, current-state summary instead).

## Status

- **M37 - gw2efficiency parity: complete.** The crafting-plan solver
  targets full behavioral parity with gw2efficiency's crafting calculator
  for every node (buy/craft/vendor decisions, owned-materials reduction,
  multi-item batches, sell-side economics, vendor purchase caps/timegates,
  Homestead Refinement efficiency tiers, achievement-bit ingredient
  dedup). The normative spec this targets is
  [`docs/gw2e-parity-spec.md`](gw2e-parity-spec.md); the full resolution
  history is [`docs/KNOWN-ISSUES.md`](KNOWN-ISSUES.md) and
  [`docs/dev-notes/HISTORY.md`](dev-notes/HISTORY.md).
- **M38 - cleanup wave: complete.** Structural cleanup across the whole
  codebase: test-infrastructure consolidation, analyzer/style hygiene, the
  `CraftingPlanView` God-class decomposition into per-section renderers
  plus a `TreeSectionController` (see
  [`docs/ARCHITECTURE.md`](ARCHITECTURE.md) section 5), pipeline/solver
  structural splits, coverage gaps closed, and this documentation
  restructure.
- **M39 - core tabs shipped.** Snapshot search/filter, the Log tab (with
  the JSONL log store and rotation), the Settings tab, and the About tab
  all landed and are in normal use - see the README's "Tabs" section for
  what each one does today.

## What's not done

- **Plan History** and **Crafting Ranker** exist only as placeholder tabs
  (they render a "Coming Soon" stub, no functional content).
- Further tab feature proposals (deeper snapshot search, plan history,
  a crafting ranker, and similar ideas) have been discussed and drafted,
  but those proposal documents live outside this repository, not under
  `docs/`. None of them are committed roadmap items yet - treat any tab
  beyond the five listed above as unplanned until a proposal is actually
  merged here.
- Localization (en/de/fr/es) is deliberately deferred - see the DEFERRED
  list in `docs/KNOWN-ISSUES.md`.

## Where the detailed history lives

- Milestone-by-milestone bug fixes and live-verification records:
  [`docs/KNOWN-ISSUES.md`](KNOWN-ISSUES.md) (short/current) and
  [`docs/dev-notes/HISTORY.md`](dev-notes/HISTORY.md) (full diary).
- The original per-phase project plans (Phase A through the M14-M17
  navigation/visual-parity work, each with full scope/acceptance-criteria
  templates) are archived at
  [`docs/archive/plans/2026-02-15/`](archive/plans/2026-02-15/).
- Durable architecture rationale: [`docs/ARCHITECTURE.md`](ARCHITECTURE.md).
