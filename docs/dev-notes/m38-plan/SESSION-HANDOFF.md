# Session handoff - M38/M39 execution (parked 2026-07-23)

Written by the orchestrating session before /clear. Read project memory FIRST
(MEMORY.md + linked files - especially m38-cleanup-directive, tab-roadmap-directive,
blish-automation-environment with the 2026-07-22 HARDENED protocol, gw2e-parity-goal).
This file is the operational runbook the memory files point at.

## Repo state at parking

- master: 49e3d30 (PR #91) or later. PR ledger this cycle: #53-#91 all merged
  (M37 complete; M38 waves A-E complete; F/G through WP-23c; M39 core tabs; AA complete).
- Suite floors at parking: 1101 module tests / 115+ updater tests (counts grow; re-measure).
- Plan + all reports/proposals: this directory (m38-cleanup-plan.md, m38-a1..a6, proposals/).

## In-flight at parking (verify state before anything else)

1. wt-m38-wp23d, branch m38-wp23d-summary-helpers (WP-23d summary-section extraction
   + WP-24 row-helper factoring). A workflow was mid implement+review; it STOPS
   UNPUSHED by design with a "[PENDING - the orchestrator fills in PASS/FAIL]"
   marker in docs/KNOWN-ISSUES.md. ON PICKUP: git -C /mnt/c/Dev/Blish/wt-m38-wp23d
   log --oneline origin/master..HEAD. If commits + the PENDING marker + a
   diff-evidence commit exist, the review completed - run the ORCHESTRATOR GATE
   (below) then release. If the worktree is missing or has no commits, re-run the
   package: same scope (extract summary/Total Cost section incl. economics tiles,
   currency annotation rows, MultiItemNote banner into Views/Rendering/
   SummarySectionRenderer via ISectionRelayoutSink, MOVE-ONLY; plus factor the two
   row-builder shapes across the extracted renderers with constant-by-constant
   pixel-identity proof), same gate protocol.
2. WP-28: DONE before parking - PR #92 MERGED (LICENSE MIT/MaximusCub, CoC
   v2.1, CONTRIBUTING, RELEASING, templates, gitignore + the two ref/*.json
   caches untracked with CLAUDE.md reconciled, tags v1.0.0/v2.0.0 gone remote
   AND local, NO SECURITY.md). Its worktree wt-m38-wp28 was removed by its
   release agent. WP-27 INPUT from its pack check: the .bhm build blanket-copies
   ref/** so users ship the ~19.6MB wiki_vendor_cache.json + item_id_cache.json
   inside the module package - a real size/packaging finding for WP-27/WP-29-era
   cleanup (fix belongs with packaging docs, likely excluding caches from the
   pack); manifest "directories":["data"] is load-bearing, NOT vestigial.

## The orchestrator gate (established protocol, used for WP-21/22/23a/23b/23c)

1. Stage: cp <worktree>/bin/x64/Debug/GW2CraftingHelper.bhm /mnt/c/Dev/Blish/wp21-check/
2. DESKTOP SAFETY (hardened 2026-07-22 after a real incident - see
   blish-automation-environment memory): >=300s user idle (idlegate.ps1) OR the
   user explicitly says the desktop is free; activate.ps1 output READ (never
   piped to /dev/null) before EVERY input batch AND EVERY capture; short
   commands; never bridge waits >15s inside one compound command; abort the
   whole session if foreground is ever not Blish/Paint.
3. Launch: Start-Process mspaint; then 'C:\Blish.HUD\Blish HUD.exe' with
   @('-g','0','--debug','--module','C:\Dev\Blish\wp21-check\GW2CraftingHelper.bhm',
   '--pid',<paintPid>,'--window','MSPaintApp','--settings','C:\Dev\Blish\blish-preflight-settings')
4. Window: corner icon click at screen (344,136) [window persists at 8,120
   1064x845]; plan tab (91,280); search box (213,267); activate Blish before
   typing; suggestion (188,291); Generate (922,298).
5. Generation wait: until-loop on data/module_log.jsonl containing
   "Generation finished" (the M39 log system emits it; DO NOT blind-sleep).
6. Checks per package: section renders vs reference captures in
   C:\Dev\Blish\preflight\captures (m37_* and wp2*_*); scan_dividers.py at 2+
   offsets (uniform 29/30 for 36px rows, 35/36 for 44px, 26 for 32px at UI
   scale 0.81; last-row + column-header have NO divider by design - isLast);
   grep Blish log for relayout DEBUG warnings (expect zero); wheel via
   wheelburst.ps1 (Paint focused), drags unreliable.
7. Record: replace the PENDING marker in the WORKTREE's docs/KNOWN-ISSUES.md
   with a dated PASS record naming captures; commit with the standard trailer:
   Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
   Claude-Session: https://claude.ai/code/session_01X328KFrx3w7bkzFucaisE2
   (a new session should use ITS OWN session link for new commits).
8. Release: sonnet agent - push, PR per the repo 5-checkbox template quoting the
   gate PASS verbatim in Validation Performed, checks --watch, merge
   --merge --delete-branch, worktree remove+prune. gh NOTE: gh.exe cannot
   resolve WSL worktree gitdirs - run it from the main repo dir with explicit
   --repo/--base/--head, or use /usr/bin/gh.

## Remaining roadmap after WP-23d+24

1. WP-25: extract the tree section controller (tree renderer + interactive
   override loop + _treeNodeStates/_nodeOverrides/_ignoredItemIds/_lastResult
   state into a TreeSectionController taking the ResolveWithOverrides delegate).
   HARDEST remaining piece - it owns interaction state. Same workflow shape
   (implement + 2-3 review lenses incl. an interaction-state lens, orchestrator
   gate incl. LIVE pill-click + ignore round-trip checks, release).
2. WP-26 (scroll/resize/wheel controller move): plan says CUT-FIRST and the
   parked session was LEANING CUT (highest risk, purely organizational payoff,
   machinery already region-mapped with KNOWN-ISSUES anchors). Decide; if cut,
   record the decision + rationale in docs/KNOWN-ISSUES.md and the plan copy here.
3. WP-27 (docs restructure): rewrite README (solver headline; author byline
   STILL UNPROVIDED by user - use the GitHub handle MaximusCub and flag);
   docs/ARCHITECTURE.md; split KNOWN-ISSUES (public current-issues vs dev-notes
   diary, REMOVE the AI-handoff/personal-path tail); research-doc framing.
   Run ONLY after F/G settles (every F/G branch appends gate records to
   KNOWN-ISSUES). Reconcile the stale "Disciplines never individually
   pixel-scanned" line (it WAS scanned - M37 item 30 + WP-23 pilot).
4. FINAL SMOKE TEST (desktop, hardened protocol): fresh master build ->
   C:\Dev\Blish\m38-final\; verify all tabs (Snapshot search incl. the
   disk-restored synthetic snapshot + staleness label; About; Log tab
   search/filter/follow; Settings incl. Homestead + Astral Acclaim valuation
   rows), single-item plan + owned-materials pass, a 2-item batch with a
   tradable root (economics tiles + banner), ignore round-trip status, the
   ecto seasonal/weekly timegated notice, divider scans on every section class.
   Then LEAVE the session running in test mode for the user.
5. Wrap: update memory (m38-cleanup-directive + gw2e-parity-goal) with the
   final state; summarize per the repo terminal-output rules (PR URLs, results,
   remaining Nice-to-Haves).

## Standing user decisions (do not re-ask)

MIT license; CoC yes; SECURITY.md NO; 2020 tags delete; KNOWN-ISSUES split yes;
upstream Blish posts NEVER; localization deferred; Ranker/Do-Next Tier 1/Plan
History remain unscheduled proposals (proposals/ dir); AA deal table parked with
ranker; Tier-2 progression-scope question deferred until ranker exists.
Open item: author/byline for README/About (ask only when WP-27 lands or user volunteers).

## Environment facts (rediscovery is wasteful)

- Worktrees: fetch + worktree add from origin/master; copy packages/ (~203MB)
  read-only from the main tree before building. Main tree stays on master.
- Build: "/mnt/c/Program Files/dotnet/dotnet.exe" build C:/Dev/.../GW2CraftingHelper.csproj
  -p:Platform=x64 (Windows paths). Suites: tests/GW2CraftingHelper.Tests +
  tests/VendorOfferUpdater.Tests.
- Pushes prompt once per branch (expected). Workflow-file pushes are REFUSED
  by git-over-HTTPS regardless of token scope - use the GitHub contents API.
- PowerShell $vars get eaten by bash interpolation - use script files.
- The preflight synthetic snapshot (item-29 design) is installed in
  blish-preflight-settings/data; ValueOwnMaterials=true; ScrollDiagnosticsEnabled=true.

## COMPLETED 2026-07-23 (closing session)
Every item in this runbook was executed: WP-23d+24 gated+merged (PR #93),
WP-25 gated+merged (PR #94), WP-26 CUT (recorded), WP-27 docs merged (PR #95),
WP-29/29b pack exclusions merged (PRs #96/#97), final smoke test PASSED on
master c64e171 (m38f_* captures), desktop left running in test mode, memory
wrapped. This file is retained as a historical record only - project memory
(m38-cleanup-directive) is the authority on final state.
