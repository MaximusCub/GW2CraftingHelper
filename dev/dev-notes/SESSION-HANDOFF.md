# Session handoff (2026-08-28)

Written so a fresh Claude session can pick this project up after the local
folder rename, which changes the project directory name and therefore the
key Claude stores its memory under. `--resume` is not expected to survive
that. Read this file first.

## 1. Do this before anything else

The rename is finished everywhere except the local folder. The full runbook
is [`docs/RENAMING.md`](../../docs/RENAMING.md); the short version:

```
cd /mnt/c/Dev/Blish && mv GW2CraftingHelper TaimisToolbench
```

That has to run from a terminal that is NOT inside the folder, with Claude
Code closed. Measured cause of the earlier failure: the WSL pids holding a
cwd inside the folder were `-bash` and `claude --resume`, i.e. the session
itself.

Then, inside the renamed clone:

- `CLAUDE.md` line 12: `C:/Dev/Blish/GW2CraftingHelper/...` becomes
  `.../TaimisToolbench/...`
- `.github/workflows/tests.yml`, rename-tripwire step: delete the CLAUDE.md
  allowlist entry. It exists only for that path.
- Re-link any worktree junction:
  `cmd.exe /c "mklink /J C:\Dev\Blish\<worktree>\packages C:\Dev\Blish\TaimisToolbench\packages"`
- **Copy the memory directory**, which is the part that silently breaks
  otherwise. It is keyed by the opened folder path, so the rename forks it:

```
cp -r ~/.claude/projects/-mnt-c-Dev-Blish-GW2CraftingHelper/memory \
      ~/.claude/projects/-mnt-c-Dev-Blish-TaimisToolbench/
```

Leave a pointer line in both `MEMORY.md` files so neither looks abandoned.

## 2. Where the project stands

Master is green and everything raised on 2026-08-28 is merged. Zero open
PRs and one worktree at the time of writing. Suite: 3,659 module + 3 seeder
+ 234 updater.

Merged that day, in order: tooltips made mandatory and icon-only (#213),
ranker Status column and readiness bars (#215), settings currency icons
(#214), two `ConfigureAwait` fixes (#217, #218), the comment-length CI gate
(#216), the ranker stat-cache warm path (#219), `OwnMaterialsGate` made
internal (#220), vendor tooling repair (#221), plan-schema backward
compatibility (#222), the readiness track colour (#223), the lazy corpus
rebuild (#224), barter-item valuation (#225), small-screen fixes (#226),
the window size ceiling (#227), and a backlog entry (#228).

## 3. Open, and owned by the maintainer

- **Tag v0.3.0.** Verified safe: no schema bump since v0.2.4, and after
  #222 both saved plans and plan history survive one regardless.
- **NUX**, the in-module first-run experience. Spec exists at
  `/mnt/c/Dev/Blish/nux/spec.md` (928 lines, outside the repo); the repo is
  untouched. Last unbuilt item from the original roadmap.

## 4. Deferred by decision, do not re-propose

- **i18n** until the feature set is locked. The research is done and
  favourable; the blocker is sequencing, because every string added after
  translation starts forces another pass.
- **The missing content-width cap.** Filed in
  [`docs/KNOWN-ISSUES.md`](../../docs/KNOWN-ISSUES.md) under DEFERRED with
  the measurement, the in-repo precedent for the fix shape, and the two
  open questions. Called a distraction; leave it there.

## 5. Verification still owed

- A **barter vendor route rendering in a real plan**, the user-visible half
  of #225. Blish's XNA text box would not take synthetic input in the
  sandbox (neither SendKeys nor scan codes), so no item could be typed to
  generate against. Easiest done in a real session.
- The **snapshot refresh and first-run paths**. The sandbox runs a canned
  snapshot, 38 days old at the time of writing, so the refresh interval,
  epoch guard, commit gate and failure classifier are never exercised. A
  scoped, revocable GW2 API key would close this; the maintainer raised the
  idea and it was neither accepted nor declined.

## 6. Environment facts that cost time to rediscover

- Pushing any `.github/workflows/*` change from WSL fails: `~/.gitconfig`
  wires `/usr/bin/gh` in as the credential helper and that token lacks the
  `workflow` scope. Use the Windows `gh.exe`, or
  `git -c credential.helper= -c credential.helper=<script wrapping gh.exe> push`.
- The desktop gate sandbox is Blish over MS Paint showing a full-screen
  screenshot. Backdrops live in `/mnt/c/Dev/Blish/backdrops/`. Launch the
  module from a space-free path; a path with spaces fails silently with no
  log file written.
- A Blish window reported at `-32000,-32000` has three causes, not one:
  the space-in-path failure (which writes no log, so a fresh log line rules
  it out), the target window not being foreground (activate PAINT, never
  Blish), or the target being the wrong window entirely. `mspaint` with a
  bad path opens a file-not-found dialog that a title match for "Paint"
  will happily find and activate. Always assert the target's rect before
  launching Blish.
- Blish UI scale in the sandbox has been observed at 0.81, so shipped
  geometry constants appear at 0.81x on screen. A 42px row measures 34px.
  Multiply before calling a measurement a mismatch.
- Sweep `keep-awake.ps1` by command line with a `-ne $PID` guard before
  arming and after tearing down. It has leaked and left the screens awake.

## 7. Working agreements worth carrying over

These live in Claude's memory directory and will be lost if section 1's
copy is skipped, so they are summarised here.

- Orchestrate rather than implement: dispatch agents, review their work
  adversarially, do not hand-write what an agent should build.
- Comments are minimal and human. A repo-wide sweep is directed but not
  run; the measured debt is 431 blocks of 13-plus lines across 236 files,
  of which 96 are inline `//` and the rest XML doc. An individual PR must
  not be blocked for matching house style.
- Desktop access is granted per occasion and expires the moment the
  maintainer says they are back. Ask again rather than assume.
- Label claims as measured, inferred or guessed, and do not report a
  regression that cannot be attributed.
