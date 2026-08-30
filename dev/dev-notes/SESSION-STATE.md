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

The owner's live install runs the **wave-7 build**, deployed 2026-08-30 10:39
from `master` at the merge of PR #238 (`36f21e0`, the wave-7 field-test fixes
W1-W7 + W9), Release x64, md5 `d5f37973050e118aa3a4746647ea6bc7` verified
identical on both sides. Rollbacks in the modules directory:
`TaimisToolbench.bhm.rollback-pre-wave7` (the wave-5/6 build),
`.rollback-pre-wave6`, `.rollback-pre-wave2`.

The owner does not review PRs (stated 2026-08-30): the agent merges and
deploys directly so the owner can test. THE GATE is the owner's review and
acceptance of the functionality - green CI is only the floor that qualifies a
build for that testing, never the acceptance itself. PR #238 was the last
posted-for-review PR. The wave-7 items (W1-W7, W9) are SHIPPED and await in-game confirmation to
become DONE; W3/W10/W11 are queued; W8 (currency-vs-item table mixing) is an
open discussion. Worktrees were cleaned to master only on owner order
2026-08-30 (the merged w6-* branches survive on origin).

**Blish HUD holds a lock on the loaded .bhm**, so a deploy fails with
`Permission denied` while it runs. Blish must be closed first; GW2 itself does
not need to be.


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
- **"Log tab: long lines run off the edge."** The owner never said that. He said
  a long line runs out of space and cannot be read without the tooltip, which is
  dumb, so it should wrap. The paraphrase became a brief, and an agent spent
  effort failing to reproduce an overrun nobody had reported. **The owner's own
  words are the primary source for owner feedback; quote them into the backlog
  rather than summarising them.**

## Hazards worth remembering

- `MysticForgeSeeder`'s `FindRepoRoot` used to probe for a `.git` **directory**.
  A linked worktree's `.git` is a file, so a run inside any of the ~28 worktrees
  walked past the worktree root and would rewrite `ref/` in whichever repo it
  found next. Fixed on `w5-seederfix`; the shape of the bug is worth checking for
  in any other tool that walks up to find the repo root.
- A branch checked out in a worktree cannot be `git checkout`ed in the main
  clone. `git diff master..branch` on a stale-based branch shows master-only
  work as deletions; use the three-dot form to see the real change set.


## The reference was already in the owner's screenshots

An agent reported V4's 24px X button as unverifiable because "no Trading Post
capture exists in this repo", and that was relayed to the owner as needing his
eye. The owner then pointed out the Trading Post was in the top corner of the
ranker screenshot he had already sent. It was.

The images the owner pastes are on disk at
`~/.claude/image-cache/<session-id>/*.png` and can be measured with PIL. When a
UI question is "does ours match the game's", check the owner's existing captures
before declaring it unmeasurable - and prefer a RATIO between two controls in
the SAME image, which needs no UI-scale assumption at all.


## Where this session ended (2026-08-29)

Three PRs open, all CI-green at the time of writing, none merged. Nothing is
deployed: **the owner's live install still runs the wave-2 build.**

| PR | Branch | Contents |
|---|---|---|
| #233 | `legendary-research-docs` | six research documents + the harness. Was red on the ASCII gate from a UTF-8 BOM on three harness sources; fixed. |
| #235 | `wave5-data` | forge recipes + their root cause, five currency valuations, two dead vendors, barter plan totals, the Gaeting removal. 4,240 tests. |
| #236 | `wave6-ui` | all sixteen wave-6 feedback items across five branches. 4,300 tests, exactly the expected sum of the branch deltas. |

`FEEDBACK-BACKLOG.md` rule still stands: an item closes only when a MERGED PR is
named beside it. Everything currently reads `IN PR #235` or `IN PR #236`, so
nothing has closed yet.

### Owed to the owner, none of it startable without him

- **A deploy and a field test.** Nothing in #235 or #236 has been seen in game.
- **F1 stays OPEN**: sticky headers are wired on the Snapshot tab only. The plan
  tab needs a fixed-height spacer per band first; the recipe is in the backlog.
- Three specific in-game checks: the mouse wheel over a pinned header strip, the
  IGNORE toggle's smaller click target (29x24 mark vs a ~70x24 word), and V4's
  24px against the real Trading Post at his UI scale.
- **Git credential**: the default OAuth credential lacks `workflow` scope, so any
  branch touching `.github/workflows/` must be pushed with the `gh` token.

### Open design question, raised by the owner, not yet actioned

Gaeting Crystal valuation. There is exactly one live id at a time. The table
currently holds `{ 28, 3600 }` (Magnetite Shard) and `{ 77, 3600 }` as two
independent literals, while 77's own comment says it must equal what 28 charges.
Proposal on the table: peg 77 to 28 rather than pin a number or match by name -
the name is the ambiguous part, and the only discriminator (the description) is
documented on the wiki as stale. **Verify the 1:1 vendor claim before building
anything on it.** A CI assertion that exactly one live currency is named "Gaeting
Crystal" would turn the next transition from silent staleness into a loud failure
with no runtime behaviour at all.

## Wiki rate limiting: we tripped it, and how to not do it again

On 2026-08-29 the GW2 wiki began injecting "An automated filter has identified
this page view as potentially automated" into rendered page views for this
household's IP. Measured: the block page is served with **HTTP 200** and is
within one byte of the real article's size (151,933 vs 151,934), because the
warning is injected INTO the article rather than replacing it. Checking the
status code and `Content-Length` therefore says nothing; three wrong conclusions
were drawn in a row from exactly that. **Grep the body for "potentially
automated" before declaring wiki access healthy.**

Scope, measured: it affects `/wiki/<Page>` rendered views. `api.php` was still
serving real content (`action=parse` returned the correct wikitext) while the
rendered path was blocked. The wiki's own headers show nginx + Varnish with
`vary: Accept-Encoding, Cookie`; a cookie-less request is a cache hit and a
cookie-bearing one passes through, but BOTH carried the warning, so the cache is
a red herring.

The cause was almost certainly this session: `tools/VendorOfferUpdater`,
`tools/MysticForgeSeeder`, the dead-vendor sweep, the Gaeting research and a
number of ad-hoc `curl` calls all hit the wiki within a few hours, and the
ad-hoc ones sent curl's default User-Agent.

**Rules for future wiki work:**
- Use the tools' own throttles (`--delay`, `--max-requests`) and never raise them
  to go faster. `MysticForgeSeeder`'s 200-request default exists for this reason.
- Every ad-hoc request gets a real User-Agent identifying the project, the same
  way `WikiRecipeClient` sets `TaimisToolbench-MysticForgeSeeder/1.0`.
- Prefer `api.php` over scraping rendered pages, and prefer one batched SMW query
  over many page fetches.
- Never run two wiki-touching agents concurrently. Several ran in parallel here.
- The "report this error" link in the warning points at **English Wikipedia** and
  is useless: it is Wikipedia's boilerplate left in, the projects share no
  administrators, and its preloaded title makes the reporter declare themselves a
  long-term abuser. The real venue is the GW2 wiki's own admin noticeboard.


## Field test issued 2026-08-29

A 20-item list covering all sixteen wave-6 items plus the four user-visible
wave-5 data fixes was handed to the owner. Items nobody can settle without the
game, called out as highest value:

- **B2** clicking IGNORE repeatedly on a node WITH CHILDREN (leaves always worked,
  which is why PR #232 looked fixed).
- **B3** at UI Size **Large** - integer scale means zero clip slip, so a leak
  there falsifies the diagnosis rather than just failing.
- **The mouse wheel over a pinned Snapshot header strip** - the clip is a sibling
  of the scroll panel and no Blish-free test can cover input dispatch.
- **V4** the 24px X buttons, though these were measured off the owner's own
  screenshot rather than guessed.
- **#17** the Obsidian vendor route, which should now be craft cost plus the
  10-ectoplasm fee rather than ~2g95s.

Owner rulings taken this session: **F1 parks at the Snapshot tab** for validation
rather than being extended to the plan tab now; the Gaeting 1:1 question is to be
settled by research without him.


# START HERE - state at 2026-08-30, end of the long wave-5/6 session

Read this section first. Everything above is history and evidence.

## What is true right now

- **`master` is green** and carries PRs #233 through #238. Suite:
  4109 + 238 + 3 = **4350**. Build is 0 warnings. **PR #238 (wave-7
  field-test fixes, W1-W7 + W9) is MERGED AND DEPLOYED** (2026-08-30 10:39,
  md5 `d5f37973050e118aa3a4746647ea6bc7` verified; the deploy section above
  has details). Rollbacks beside the live file: `.rollback-pre-wave7` (the
  wave-5/6 build), `.rollback-pre-wave6`, `.rollback-pre-wave2`.
- **The owner does not review PRs and does not want them posted**: green CI
  qualifies a build, the agent merges and deploys so the owner can test.
  THE GATE is the owner's review and acceptance of the functionality
  (his correction 2026-08-30; see also dev/dev-notes/FEEDBACK-BACKLOG.md's
  rule section - SHIPPED vs DONE).
- **Worktrees are CLEANED to master only** on owner order 2026-08-30; the
  merged `w6-*`/`wave6-ui` branches survive on origin if a surgical look
  back is ever needed. A stray agent-harness worktree (`vivid-fjord`) and
  its `.gitignore` entries were removed the same day.

## Owner away 2026-08-30 for a few days - validations deferred

The owner left before validating wave-7 in game. When he returns, hand him
this checklist (all SHIPPED in PR #238, awaiting his verdict to become DONE;
verbatim findings and evidence live in dev/dev-notes/FEEDBACK-BACKLOG.md
W1-W9):

1. **Snapshot tab (W7 + F1)**: scroll Items and Currencies - no row text
   ghosting through the pinned header; header pins/unpins correctly; wheel
   over a pinned header still scrolls. Known trade: a row under the band can
   lose ~2px of top at some scroll phases - should read invisible.
2. **Plan tab (W9, W4, W5, B2 retest)**: no gap between the "Plan updated"
   rule and the first scrolled row; X buttons black-glyph on the standard
   face with Best Path's hover/press animation, sized near the close button;
   ignored state = amber plate, dimmed rows inert; rapid IGNORE clicks on a
   node WITH children do not expand/collapse or drop clicks; Obsidian Heavy
   Breastplate "1x Obsidian Shard" shows full pills, no "+1" with room to
   spare; "+N" still appears at true minimum width.
3. **W1**: Settings valuation grid keeps currency borders; icons beside
   digits (plan totals, Shopping List Each/Total, Snapshot header, Ranker)
   are frame-less, coin-style.
4. **W2**: sort glyphs clearly detached from labels on all sorted tables.
5. **W6**: Clear Overrides confirm title centered.
6. **Regression sweep**: CRAFT/VENDOR toggle does not throw the scroll (B1,
   owner-confirmed); overrides survive load; nothing paints above separator
   lines.

Queued behind his return: **W3** (Recipe Tree sticky headers - the owner
validated the Snapshot mechanism modulo the overdraw, which W7 fixed; build
after he re-validates Snapshot), **W10** (header labels left-align over the
full column incl. icon gutters, all tables) and **W11** (icon seat per his
in-game references, blocked on W1 which this deploy carries) - both are
unblocked file-wise and dispatch on his word. **W8** (currency-vs-item
mixing in the Total Cost table) is an open DISCUSSION, archaeology recorded
in its backlog row (ARCHITECTURE 7.5, the one-list-no-drift rule, the
Legendary Rune measurement, the tension with the Prerequisites ruling).
**V7** has never been explicitly confirmed. Nothing is startable without
him: his standing rule is that his findings decide the next wave.

## The one thing blocking everything

**The owner is away; in-game validation of wave-7 is deferred** (stated
2026-08-30, back in a few days). Until his verdicts land: W1/W2/W4/W5/W6/W7/W9
stay SHIPPED-pending, W3/W10/W11 stay queued, and no new wave starts.

## Known-open, in priority order, for when the owner returns

1. His wave-7 validation checklist (see "Owner away" above). Verdicts convert
   W1/W2/W4/W5/W6/W7/W9 to DONE or reopen items.
2. **W3, W10, W11** - queued, dispatch on his word (W3 after he re-validates
   Snapshot post-W7; the recipe for the plan-tab adoption is in
   `dev/dev-notes/FEEDBACK-BACKLOG.md`). The old F1 park ruling was
   conditionally lifted 2026-08-30: he validated the Snapshot mechanism
   modulo the overdraw, which W7 fixes.
3. **W8 discussion** - whether the Total Cost table should mix currencies and
   barter items; archaeology recorded in its backlog row.
4. **v0.3.0 tagging.** Deferred by the owner pending in-game validation.
5. `TreeRowPillHitTest` now also answers a checkbox question, so its pill-specific
   name is misleading. Renaming churns a 248-line test suite; the owner has not
   reviewed it yet.
6. NUX, the in-module first-run experience. Spec at `/mnt/c/Dev/Blish/nux/spec.md`,
   outside the repo. Last unbuilt item from the original roadmap.
7. Sweep candidates from wave-7: `PillColors.GlyphColor` is production-unused;
   `TreeRowPillHitTestTests` mirrors the view's pill assembly and should
   share one source of truth.

Deferred by decision, **do not re-propose**: i18n until the feature set is locked;
the missing content-width cap (filed in `docs/KNOWN-ISSUES.md` under DEFERRED).
