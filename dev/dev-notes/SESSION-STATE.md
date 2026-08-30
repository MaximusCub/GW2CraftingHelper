# Live session state - 2026-08-29

Written so a compacted or fresh session can resume without re-deriving
anything. Durable truth lives in git, GitHub and
`dev/dev-notes/FEEDBACK-BACKLOG.md`; this file records only what is in flight.

## Ground truth, in preference order

1. `dev/dev-notes/FEEDBACK-BACKLOG.md` - every owner request, 16 OPEN. An item
   closes ONLY when a merged PR is named beside it. Diff this before ever
   reporting a wave complete.
2. `gh pr list` - what is shipped and what is proposed.
3. `git branch` - unmerged work.
4. `dev/proposals/*.md` - six research documents (PR #233).

## Open PRs

- **#235** wave5-data: 90 legendary armour recipes recovered, 5 currency
  valuations added and 16 refused, a dead vendor retired. CI was green.
- **#233** legendary-research-docs: six research documents, no code.

## Branches with unmerged work

`w5-noncoin` (barter plan totals + the floor disclosure; DONE, unmerged),
`w5-seederfix` (in progress), `w6-*` (in progress, see below), plus the
already-merged-into-wave5-data sources `w5-currvals`, `w5-forgerecipes`,
`w5-deadvendors`, and the research sources `w4-*` folded into #233.

## Agents in flight

| branch | backlog items | brief |
|---|---|---|
| w5-seederfix | - | MERGED into `wave5-data` (PR #235). The variant-anchor diagnosis was DISPROVEN; the real defect was output-side name resolution. See "Claims retracted" below. |
| w6-tables | F1, F2 | sticky table headers; persistent sort indicators (opacity-only, never width). NO sorting in the Ranker (owner ruling). |
| w6-tree | B2, L1, L2, V5, V6 | third cause of the IGNORE repeated-click bug; "+N no room" firing when space exists; IGNORE text pill to a language-free control; Cost header centring; Item header anchor. |
| w6-viewport | B1, B3 | currency table reflow moving the scroll; the header overdraw. |
| w6-icons | V1, V2, V4 | currency icon vertical centring; the grey-background REGRESSION we caused; Ranker X button scale. |
| w6-polish | V3, V7, V8 | Copper-per-unit header centring; dialog body centring; Log line wrapping with VARIABLE row heights. |
| w6-gaeting | R1, R2 | R1 answered: currency 39 / item 86094 is the Path of Fire era Gaeting Crystal, historical since the 2022-07-19 update; currency 77 / item 104026 is the live Janthir Wilds one. Owner ruled the retired id be removed outright rather than relabelled. R2: currency 77 is missing from `Gw2Constants` while 82 corpus cost lines are priced in it. |

## Owner rulings that shape the work

- The module is **a project planner WITH a price optimizer**, not a price
  optimizer alone. Non-transactional routes are the product, not out of scope.
- **No sorting in the Crafting Ranker** - its row order is already an answer.
- **Do not engineer below `WindowSizing.MinWindowWidth`.**
- The viewport overdraw must be fixed by a **hard horizontal cutoff**, not by a
  gap sized against clip slip. No correctness argument may mention tree depth.
- Gold/silver/copper coin icons take **no** frame; other currency icons take a
  border and NO background fill.
- **A currency retired from the game is removed outright, not relabelled** - no
  valuation row, no Settings row, no name entry. A currency no account can hold
  and nothing in the corpus is priced in cannot affect a solve, so carrying it
  only invites the reader to think it can.
- Prerequisites belong between Total Cost and the Recipe Tree. Total Cost owns
  everything priced (gold AND currencies); Prerequisites owns what is not
  priced at all. A "prereq steps" list mirroring Crafting Steps is wanted.

## Process rule adopted this session

A factual claim entering a durable artefact - a proposal, an agent brief, or a
PR body - must cite the PRIMARY source (file:line, a command, an API response),
never another agent's summary. If it is checkable offline in under a minute,
check it before writing it. Five claims were retracted this session; every one
was an agent's report repeated without checking the primary.

## Environment facts that cost time to rediscover

- dotnet: `"/mnt/c/Program Files/dotnet/dotnet.exe"`, Windows-style paths only.
- gh: `"/mnt/c/Program Files/GitHub CLI/gh.exe"`. A push touching
  `.github/workflows/*` needs it via
  `git -c credential.helper= -c credential.helper=/tmp/ghcred.sh push`.
  A `--body-file` for the Windows gh must be a Windows path.
- Worktrees need a packages junction:
  `cmd.exe /c "mklink /J C:\Dev\Blish\<wt>\packages C:\Dev\Blish\TaimisToolbench\packages"`
- Sandbox: `preflight/launch-sandbox.ps1` (blank Paint + Blish, isolated
  settings). Corner icon is a FIXED (320,0) offset from the Blish window rect;
  captures are taken from screen y-89, clicks take screen coords. Activation
  and every input burst must be SEPARATE bash calls.
- `Gw2BuildApiClientTests` had a wall-clock race; fixed in PR #234, but the
  suite has other timing-sensitive tests documented there as deliberate.

## Deployed

The owner's live install runs the wave-2 build (2026-08-29 18:34). None of
wave 5 or 6 is deployed. Rollback copy: `TaimisToolbench.bhm.rollback-pre-wave2`.


## Wave 5 integration, as of 2026-08-29

`wave5-data` (PR #235) now carries `w5-forgerecipes`, `w5-currvals`,
`w5-deadvendors`, `w5-noncoin` and `w5-seederfix`. Pushed; suite **4,239 green**
(3,998 + 238 + 3). Measured in the merged tree: vendor offers 59,244, recipes
seed 15,109, forge recipes 1,734, hints 17, and the negative id partition holds
at 4 hand-authored rows in `[-99999, -1]` against 1,734 generated at or below
-100000.

`PersistedPlan.CurrentSchemaVersion` is still **3**. `w5-noncoin` moved only
`SchemaShapeHash`; its three new members are additive and every consumer
null-guards the list an older plan deserializes as null. No saved plan is lost.

## Claims retracted, and what replaced them

Kept because the process rule exists to stop these recurring: a factual claim
entering a proposal, an agent brief or a PR body must cite a primary source,
never another agent's summary.

- **"The seeder dropped 90 legendary armour recipes because anchored ingredient
  names resolve to no item id."** False, and it had reached PR #235's body.
  `[[Ardent Glorious Armguards#item1]]|?Has game id` resolves fine through the
  tool's existing path, and the dev cache already held 377 anchored names with
  zero misses. The real defect is output-side: a recipe's `Has canonical name`
  is a disambiguated display name that is no wiki page, and multi-variant pages
  carry no page-level `Has game id`.
- **"~338 further `Recipe: Box/Satchel` recipes are dropped by the same bug."**
  False. 286 already ship in master; 4 were actually dropped.
- **"Scholar Glenna (Gaeting Crystal) is 110 offers over 10 items."** It is 121
  rows over 112 output items; the 110 counted only the rows that charge the
  crystal.
- **"Currency 39 should stay valued so its Settings row remains clearable."**
  Overruled by the owner and by measurement: zero cost lines in the corpus are
  priced in currency 39, and no wallet has held it since 2022-07-19.
- **"The currency table growing is what moves the scroll."** The table growing
  is only the trigger. Blish's `Scrollbar` zeroes `ScrollDistance` inside the
  restore's own assignment statement when its cached percent is stale.
- **"The icon sits off-centre because the line box and the digit ink disagree."**
  For Menomonia they agree within 1px at every face in the ramp. The seat was
  simply 0.

## Hazards worth remembering

- `MysticForgeSeeder`'s `FindRepoRoot` used to probe for a `.git` **directory**.
  A linked worktree's `.git` is a file, so a run inside any of the ~28 worktrees
  walked past the worktree root and would rewrite `ref/` in whichever repo it
  found next. Fixed on `w5-seederfix`; the shape of the bug is worth checking for
  in any other tool that walks up to find the repo root.
- A branch checked out in a worktree cannot be `git checkout`ed in the main
  clone. `git diff master..branch` on a stale-based branch shows master-only
  work as deletions; use the three-dot form to see the real change set.
