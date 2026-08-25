# GW2CraftingHelper - Roadmap

> Supersedes all prior roadmap revisions (the detailed per-milestone
> planning template this document used through M17 is preserved in git
> history; this revision is a short, current-state summary instead).

## Status

- **M37 - gw2efficiency parity: complete as specified by the July spec,
  with a ratified convergence queue in flight.** The crafting-plan solver
  targets full behavioral parity with gw2efficiency's crafting calculator
  for every node (buy/craft/vendor decisions, owned-materials reduction,
  multi-item batches, sell-side economics, vendor purchase caps/timegates,
  Homestead Refinement efficiency tiers, achievement-bit ingredient
  dedup). The normative spec this targets is
  [`docs/gw2e-parity-spec.md`](gw2e-parity-spec.md). The August 2026
  convergence audit
  ([`docs/research/gw2e-convergence-matrix.md`](research/gw2e-convergence-matrix.md)
  plus the maintainer's rulings in
  [`docs/gw2e-considerations.md`](gw2e-considerations.md)) then found a
  queue of genuine ADOPT gaps against the live calculator; those are what
  the current wave of branches is implementing.
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
- **Current phase - field-test releases + the convergence-adoption wave.**
  Since 2026-08 the project ships stamped builds to a live Blish HUD
  install: `v0.2.0` (2026-08-23) through `v0.2.3` (2026-08-24, the
  plan-view redesign), each with a `CHANGELOG.md` entry and a matching git
  tag - see [`CHANGELOG.md`](../CHANGELOG.md) and
  [`docs/RELEASING.md`](RELEASING.md). Work arrives as one branch per
  milestone, each ending in a live desktop gate; the feedback loop is the
  maintainer field-testing the deployed build. The queue is driven by the
  convergence matrix's ADOPT rows and by field-test findings, not by a
  fixed milestone plan.

## What's not done

- **Plan History** and **Crafting Ranker** exist only as placeholder tabs
  (they render a "Coming Soon" stub, no functional content).
- Further tab feature proposals (deeper snapshot search, plan history,
  a crafting ranker, and similar ideas) live in-repo at
  [`docs/dev-notes/m38-plan/proposals/`](dev-notes/m38-plan/proposals/).
  Being written down does not make them committed roadmap items: `d1`
  (snapshot/about/settings) is partly implemented, while `d3`
  (plan history) and `d4` (crafting ranker) back the two placeholder tabs
  above and are still proposals. Treat any tab beyond the five listed
  above as unplanned until it actually ships.
- Localization (en/de/fr/es) is deliberately deferred - see the DEFERRED
  list in `docs/KNOWN-ISSUES.md`.

## Where the detailed history lives

- Milestone-by-milestone bug fixes and live-verification records, in three
  tiers: [`docs/KNOWN-ISSUES.md`](KNOWN-ISSUES.md) is the current-state
  tracker (numbered catalog, the open list, and a ledger of every rotated
  milestone record); the full milestone records live one file each under
  [`docs/archive/known-issues/`](archive/known-issues/); and the pre-M38
  fix-pass diary is [`docs/dev-notes/HISTORY.md`](dev-notes/HISTORY.md).
- The original per-phase project plans (Phase A through the M14-M17
  navigation/visual-parity work, each with full scope/acceptance-criteria
  templates) are archived at
  [`docs/archive/plans/2026-02-15/`](archive/plans/2026-02-15/).
- Durable architecture rationale: [`docs/ARCHITECTURE.md`](ARCHITECTURE.md).

## Backlog

- **First-run experience (NUX / welcome).** Requested 2026-08-24. A new
  user opens the module to empty tabs and no explanation of what an API
  key buys them; the first-load snapshot now fills the data, but nothing
  yet introduces the tabs, the plan flow, or what to do without a key.
