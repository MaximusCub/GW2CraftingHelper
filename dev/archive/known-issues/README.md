# Archived milestone records

One file per milestone record, moved **verbatim** out of
[`docs/KNOWN-ISSUES.md`](../../../docs/KNOWN-ISSUES.md) by the 2026-08-24
docs rotation pass. Nothing was edited during the move itself: each file is
its section's original bytes, `## ` heading and all.

Naming is `YYYY-MM-DD-<slug>.md` - the date in the section title, or the
date of the commit that introduced the section when the title carries none;
the slug is the branch slug already in the title's parentheses, else a kebab
of the title.

**Do not append here.** This directory is closed. New milestone records go
to [`../../records/<branch-slug>.md`](../../records/), one file per branch;
this directory is the older scheme, kept as it was rather than renamed.
Corrections to a record are written as a new record, never as an edit to an
old one: these are evidence, not documentation.

Each file has a one-line stub in the milestone record ledger in
`docs/KNOWN-ISSUES.md`, carrying the quoted names `.cs`, test, and `ref/`
files cite the record by, so a `grep` in that file still lands one hop from
the full record.

## What has been changed since the move, and why it is not an edit

Four carve-outs from "never edit an archived record". None of them changes
what a record found; the first two exist because a grep or a click has to
keep working, and the third because this directory is public. A reviewer
diffing this directory against
`git show 3caf935:docs/KNOWN-ISSUES.md` should expect exactly these
differences and no others.

1. **The provenance banner.** Every file here opens with two blockquote
   lines giving its date, its branch and the fact that it is a point-in-time
   record which may not describe current code. This is additive metadata
   above the record, not a rewrite of it. The motivation is concrete:
   `VendorOfferResolver` was deleted in M38/WP-10, and a grep for it returns
   zero code hits and dozens of confident present-tense hits in this
   directory. Nothing in that hit list said "historical" until the banners
   did.
2. **Relative links repaired after a directory move.** Two so far. Commit
   `16d8a25` rewrote a dangling link in
   [`2026-08-23-min-width-1436.md`](2026-08-23-min-width-1436.md)
   (`research/minimum-window-width.md` -> a `../`-prefixed path) that the
   deeper directory broke; the `docs/archive` -> `dev/archive` move
   re-pointed the `docs/KNOWN-ISSUES.md` and `docs/research/` links in the
   files that carried them. Link *text* is untouched in every case.
3. **Private-conversation material removed.** These records are published;
   the working conversation behind them is not. A sanitization pass replaced
   quoted personal speech, attribution of a decision to a named individual,
   and agent-process residue with the impersonal technical fact each
   carried, and deleted the sentence where it carried none. Only prose of
   that kind changed. No measurement, derivation, date, file or line
   reference, test count, commit SHA, finding, heading or link was altered,
   so every claim a record makes about the code still reads as it did.
4. Nothing else. Backticked paths in the prose of a record still name
   `docs/archive/...` where the record said so at the time - that is what
   the record said, and the banner is what marks it historical.
