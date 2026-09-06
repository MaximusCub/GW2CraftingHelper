> **Milestone record - 2026-09-04, branch `cleanup-public-surfaces`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Development-process residue removed from tracked prose and comments

A repository hygiene pass, in pull request 244. Tracked documentation,
source comments and CI comments had accumulated text that belongs to how the
work was produced rather than to the project itself. This repository is
public, so that text is published. The pass removed it and left the
technical content it was wrapped around.

235 files, 1,509 insertions and 2,201 deletions. Two commits: a wide
sanitization pass over 193 files, then a smaller correctness pass over
indexes, counts and three dead documents.

### What was removed, and from where

The categories, as the branch's own commits define them:

- Quoted speech, and attribution of a decision to an individual.
- Narration of how the work proceeded: working status, tallies of review
  passes, and per-change test counts recorded as progress rather than as
  evidence.
- Private tracker row codes (`W#`, `L#`, `F#`, `B#`, `H#`, `M40-N` and
  similar) that resolve to files not in this repository.
- Citations of untracked paths, and personal capture directories.
- Terms coined during development and then used as if defined. The
  substitutions are literal: "desktop gate" now reads "sandbox check", and a
  "field report" is named as the defect it describes.
- The third-person narrator, on the grounds that these documents are
  published under their author's name and no separate observer exists.

The surfaces touched were `.cs` doc and inline comments across `Services/`
and `Views/`, the test projects, `.github/workflows/tests.yml`, and the
tracked prose in `docs/`, `dev/proposals/`, `dev/records/`,
`dev/archive/`, plus `README.md`, `CONTRIBUTING.md` and `CLAUDE.md`.

Two `.github/workflows/tests.yml` comments quoted a bug report verbatim.
Each now states the defect plainly instead.

No behaviour changed in the first commit. Its own message records comments
and prose only, with the single `.cs` line whose text moved sitting beside
byte-identical code.

### What was kept

The defect, the cause, the approach taken, every measurement and derivation,
file and line references, `KNOWN-ISSUES #N` citations, and repo-relative
documentation paths. Where a fact originated with a person, the fact
survives and the person does not.

That is the rule the pass replaced the removed text with, and it is written
down in [`../archive/known-issues/README.md`](../archive/known-issues/README.md)
rather than left implicit. That file previously promised that the archived
milestone records differ from their originals in exactly two listed ways.
The pass made that false, so it gains a third carve-out naming this
sanitization, and stating that no measurement, derivation, date, file or
line reference, test count, commit hash, finding, heading or link was
altered.

Three test identifiers were renamed. They attributed a capture or a report
to an individual, and printed that attribution into CI log output. Each now
names the in-game observation instead.

### Stale index text corrected

Three index files described `dev/proposals/` as designs for features that
were never built. Four of those proposals had shipped in 0.3.0, and 16
source files cite them by section as the reasoning behind shipped code, with
a CI check that the citations resolve. `dev/proposals/README.md`,
`dev/README.md` and `docs/README.md` now separate the shipped write-ups from
the unbuilt ones and name both sets.

Six directory counts in `docs/README.md` were refreshed against the tree:
`Models/` 53 to 57, `Services/` 165 to 209, `Services/Recipes/` 8 to 10,
`Views/` 17 to 65, `Views/Rendering/` 37 to 46, and
`Views/CraftingPlanView.cs` 4,987 lines to 5,185.

`docs/ARCHITECTURE.md` section 5 restated plan-view line figures inline.
It now points at `docs/file-budgets.txt`, which CI enforces, so the number
cannot go stale when the file grows.

References to a local worktree path under `.claude/` were removed from five
documents. Local tooling has no reason to name itself in published
documentation.

### Three documents deleted

- `dev/archive/m38-a4-public-repo.md`, 214 lines. It audited what a stranger
  finds on arriving at the repository and recommended removing exactly this
  class of text, while its own body preserved that text quoted and intact.
  Its recommendations had all been carried out except adding a
  `SECURITY.md`. No inbound references.
- `dev/archive/m37-item29-checklist.md`, 240 lines. A verification checklist
  written in the imperative for a procedure that had already run;
  KNOWN-ISSUES #29 is marked VERIFIED. Its one fixture path did not resolve.
  No inbound references.
- `docs/RENAMING.md`, 182 lines. It was the runbook for the repository
  rename. Its entry was also removed from the old-name
  grep allowlist in `.github/workflows/tests.yml`, where it had been the
  only exception outside `dev/`, `docs/research/` and `CHANGELOG.md`. The
  commits do not state why it was dropped, so this record does not either.

### Validation

The commits record: build 0 warnings, suite unchanged at 4186 + 242 + 3
across all three test projects, and all invariant gates green, including the
markdown link check, the KNOWN-ISSUES anchor check and the doc-citation
check.

Gate: not required - no runtime surface. The pass changed comments, tracked
prose and three test method names. No production code path, player-visible
string, or data file was modified, and the suite count is unchanged either
side of the branch.
