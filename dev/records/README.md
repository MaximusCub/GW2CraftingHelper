# Milestone records

One file per branch, named for the branch slug. A branch that finishes a
milestone writes its record here and nowhere else, which is the whole point
of the directory: two branches never touch the same file, so two records
never conflict, and no one has to remember a merge ritual.

Each record is what the branch actually did - the defect, the root cause,
the measurements, the review rounds, and the live desktop gate at the end.
It describes the code as of its own date and is not maintained afterwards.
Corrections are a new record, never an edit to an old one.

Older records, rotated out of `docs/KNOWN-ISSUES.md` in one 2026-08-24 pass
before this directory existed, are in
[`../archive/known-issues/`](../archive/known-issues/) under a
`YYYY-MM-DD-<slug>.md` scheme. Nothing new goes there.

Every record here and there has a one-line stub in the ledger in
[`docs/KNOWN-ISSUES.md`](../../docs/KNOWN-ISSUES.md), carrying the exact
quoted names that `.cs`, test and `ref/` files cite it by, so a grep of the
tracker still lands one hop from the full record.
