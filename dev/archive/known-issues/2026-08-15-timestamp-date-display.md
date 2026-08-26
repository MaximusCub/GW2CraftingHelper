> **Frozen record - 2026-08-15, branch `timestamp-date-display`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Timestamp date display (all user-facing timestamps gain dates)

User-directed, field-test feedback: "a session could wrap over midnight" -
plain HH:mm/h:mm tt time-only displays are ambiguous across a midnight
boundary, so every user-facing timestamp in the module gains its date. No
transient-event exclusions - the directive covered every one of them, not
just the long-lived ones.

**Sites converted (10 in scope, plus 3 out-of-scope sites pulled in as a
tracked scope expansion).** All ten formerly time-only sites now render with
a date, using `CultureInfo.InvariantCulture`:
- `Module.cs`: the `SaveStatusThreadSafe` "Updated" snapshot status and the
  refresh-failure "{cause}" status (~line 1049 and ~line 1079).
- `Views/LogTabContent.cs`: `FormatLine`'s log-row timestamp, now
  `yyyy-MM-dd HH:mm:ss` (was `HH:mm:ss`).
- `Views/MainView.cs`: the `_clearButton.Click` handler's "Cache Cleared"
  status (~line 247), `RefreshNowAsync`'s "Updated" status (~line 526), and
  its own refresh-failure "{cause}" status (~line 581).
- `Views/SettingsTabContent.cs`: the four "Saved" labels (homestead tiers,
  log retention, snapshot refresh interval, currency valuations).

Scope expansion (needs PR-body ratification, not silently absorbed): three
additional `Views/CraftingPlanView.cs` sites that were already
date-formatted (`SeedRestored`'s "Generated ..." line 724,
`_statusBoard.Finish`'s "Plan generated - ..." line 2618, the W3D banner's
"Generated: ..." line 3083) were converted from ambient `CurrentCulture`
interpolation to `CultureInfo.InvariantCulture` too, to keep all thirteen
timestamp sites in the module agreeing on one culture policy. This was
outside this feature's original brief ("already date-formatted, leave
untouched") and needs the user's explicit sign-off in the PR body, not
silent inclusion.

**InvariantCulture policy.** Every one of the thirteen sites above formats
with `CultureInfo.InvariantCulture` rather than the ambient
`CurrentCulture`: the module's UI strings are English-only, Invariant keeps
month abbreviations and the AM/PM designator stable (under de-DE, `h:mm tt`
yields an EMPTY AM/PM designator - "2:14" would be ambiguous with "14:14"),
and it stops ':' from being culture-substituted inside `HH:mm:ss`.
Documented in-repo at `Views/LogTabContent.cs`'s `FormatLine` (the log
format is the strongest case) and at `Views/CraftingPlanView.cs`'s
`SeedRestored` call (the three out-of-scope sites' own anchor).

**OPEN USER DECISION (layout risk, not silently patched around).** At the
default 930x710 window, the header status label's free run before
`_clearButton`'s left edge is ~524px; the worst-case failure composite
("Refresh partially failed - 3 of 5 sources - Aug 15, 2026 3:41 PM (2h 5m
ago)") is inferred at roughly 490-530px. Overflow slides UNDER the Clear
Cache button rather than being clipped, because the buttons draw on top of
the status label. Options: widen the label's free run, give status its own
row, or accept the risk as-is - awaiting the user's call.

**Recorded facts (not bugs, not fixed).**
- Log search matches the FORMATTED line (post-`FormatLine`), so dates are
  now searchable text - a short numeric query (e.g. "15" or "20") now
  matches nearly every row via the date, not just a rare timestamp
  coincidence.
- Log rows clip ~11 chars more on the right than before (the `yyyy-MM-dd `
  prefix is 11 characters). Copy still exports the full, unclipped line via
  `CopyToClipboard`; a horizontal scroll on the Log tab's content panel is
  the real fix if that tab is revisited.

Gate: PASS 2026-08-16 (orchestrator live sandbox session, combined wave-4 staging build). Verified: Log tab rows render "[INFO] 2026-08-15 21:14:10" ISO-dated; a freshly-produced failure status renders "Refresh failed - GW2 API access not ready - Aug 15, 2026 9:32 PM (25d ago)" with no clipping at the default window width; plan strip and W3D banner dated. Note: a status string persisted by a PRE-fix build renders in the old time-only format until the next status write - expected, not a defect.
---
