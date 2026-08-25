# Archived milestone records

One file per milestone record, moved **verbatim** out of
[`docs/KNOWN-ISSUES.md`](../../KNOWN-ISSUES.md) by the 2026-08-24 docs
rotation pass. Nothing here was edited during the move: each file is its
section's original bytes, `## ` heading and all.

Naming is `YYYY-MM-DD-<slug>.md` - the date in the section title, or the
date of the commit that introduced the section when the title carries none;
the slug is the branch slug already in the title's parentheses, else a kebab
of the title.

**Do not append here.** New milestone records go to the append zone at the
end of `docs/KNOWN-ISSUES.md` and rotate into this directory once they are
gated and swept - see "How this file works" in that file. Corrections to an
archived record are written as a new record, never as an edit to an old one:
these are evidence, not documentation.

Each file has a one-line stub in that file's milestone record ledger,
carrying the quoted names `.cs`, test, and `ref/` files cite the record by,
so a `grep` in `docs/KNOWN-ISSUES.md` still lands one hop from the full
record.
