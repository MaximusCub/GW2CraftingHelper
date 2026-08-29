# The engineering record

Nothing in this directory is current documentation. It is the dated,
mostly-frozen record of how the module got to be the way it is: what was
measured, what was tried and rejected, what a live desktop session actually
showed. It lives here rather than under `docs/` so that a reader looking for
documentation finds documentation, and a reader looking for evidence finds
evidence, without either one having to sift the other.

Current documentation is [`docs/`](../docs/README.md). If a page here and a
page there disagree, `docs/` wins and this directory is stale by design.

## What is in here

| Path | What it holds |
| --- | --- |
| [`records/`](records/) | One file per branch: the milestone record a branch wrote when it finished. This is where new records land. |
| [`archive/known-issues/`](archive/known-issues/) | 69 older milestone records, rotated out of `docs/KNOWN-ISSUES.md` in one pass on 2026-08-24, before per-branch files existed. Same content, older filing scheme. |
| [`archive/`](archive/) | Closed point-in-time audits (`m37-*`, `m38-a4/a5/a6`) and the original per-phase project plans under `archive/plans/`. |
| [`dev-notes/HISTORY.md`](dev-notes/HISTORY.md) | The pre-M38 fix-pass diary: hypotheses, instrumentation, root-cause traces and dated gate records, filed under the same item numbers `docs/KNOWN-ISSUES.md` uses. |
| [`dev-notes/SESSION-HANDOFF.md`](dev-notes/SESSION-HANDOFF.md) | State of play at the 2026-08-28 handoff, written so a fresh session can recover after the local folder rename forks Claude's memory directory. Read it before resuming work. |
| [`dev-notes/m38-plan/`](dev-notes/m38-plan/) | The M38 cleanup wave's analysis and work-package plan. Executed; kept for the reasoning. |
| [`proposals/`](proposals/) | Written-up designs for features that were never built. Not a roadmap. |

## Why records are kept and not summarised

Several shipped constants are the way they are because somebody measured
them once, at 3x scaling, against the running game. The measurement is the
whole value of the record - a summary of it is worth nothing, because the
next person who wants to change the constant needs the number and the
method, not the conclusion. So these files are kept whole, and kept
greppable in-repo rather than moved to PR bodies.

The corollary is that they go stale: they describe the code as it was on
their date. `VendorOfferResolver`, for one, was deleted in M38/WP-10 and is
still described in the present tense in dozens of files here. Every file
under `archive/` carries a two-line banner saying so; files under
`records/` carry their branch and date in the same place.

Two consequences of that, both deliberate. A record still names the
throwaway worktree a command was run in, because that is what the record
said and rewriting it would be an edit. And where a record is known-wrong
today - the M38 plan's "test-count floor: 854 green", against 2,803 now -
the correction is in that file's banner, not in its text.

## Editing rules

- **Records are evidence, not documentation.** A correction to a record is a
  new record, not a rewrite of the old one.
- Two things are exempt from that, because neither touches what the record
  says: the provenance banner at the top of each archived file, and a
  relative link repaired after a directory move.
- **Do not append to `archive/known-issues/`.** New records go to
  `records/<branch-slug>.md`.
