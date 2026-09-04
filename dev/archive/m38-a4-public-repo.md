> **Frozen record - 2026-08-17.** M38 cleanup analysis, public-repo-readiness lens, closed and kept as evidence.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

# M38 Analysis A4: Public GitHub Community Readiness

**Scope**: What a stranger encounters landing on `github.com/MaximusCub/GW2CraftingHelper` at current `master` (`be8ebda`). This is a documentation/packaging/hygiene audit, not a code-behavior audit. All findings are read-only observations; nothing was modified.

**Epistemic labeling**: Every factual claim below was directly observed (file read, `git` command run, or `gh` query run) — marked inline only where a size/measurement is load-bearing. No runtime performance claims are made in this report (out of lens); where I note something CPU/memory-adjacent (e.g. clone size) it is MEASURED via `du`/`git ls-files`, not inferred.

---

## 0. Overall verdict

The engineering underneath is unusually rigorous for a hobby Blish HUD module - a real parity spec reverse-engineered from gw2efficiency's actual source (`docs/gw2e-parity-spec.md`), a 1300-line evidence-backed engineering log (`docs/KNOWN-ISSUES.md`), decompile-evidenced bug workarounds (`WheelDeltaSanitizer`, `FrameTicker`/`MainThreadMarshal`), and 800+ passing tests. None of that is visible from the repo's front door. The public-facing surface (README, LICENSE-adjacent metadata, CI, issue process) describes a different, much smaller module than what actually ships, and several pieces of internal working process (AI-agent session handoff notes, personal dev-machine paths, a repo cache-commit rule the tree itself contradicts) are sitting in the default branch where any stranger will see them.

This is fixable with almost entirely additive/organizational work (new docs, restructuring existing docs, a `.gitattributes`/history rewrite for the cache blobs) rather than code changes — good news for a "PR-by-PR" execution plan since most of this can proceed in parallel with the code-cleanup PRs.

---

## 1. README.md — severely out of date (Critical-for-public-readiness)

**File**: `README.md` (36 lines)

The README describes a module that:
- "caches and displays Guild Wars 2 account data including inventory, wallet, and material storage"
- Lists features: bank/shared-inv/material-storage/character-bag collection, wallet display, Items/Wallet/All filter, dedup, auto-refresh.

It says **nothing** about the module's actual current core feature: a full crafting-plan solver with buy-vs-craft decisions, a collapsible recipe tree, Trading Post + vendor + Mystic Forge pricing, a gw2efficiency-parity shopping list, currency valuation, settings tab, or item search/autocomplete. Compare against what `docs/ROADMAP.md` and `docs/KNOWN-ISSUES.md` describe as shipped (M1–M37): this is the majority of the module's functionality and its main value proposition, entirely absent from the README.

Also stale/misleading:
- No mention of the crafting UI at all, no screenshots.
- API permissions list omits `unlocks` (present in `manifest.json`, marked optional) — a contributor reading only the README doesn't know recipe-unlock checking exists.
- No install instructions for an end user (only `dotnet build`/`dotnet test` — i.e. *contributor* instructions, not *player* instructions). See §6.
- No CI badge despite a working GitHub Actions workflow (`.github/workflows/tests.yml`).
- The GitHub repo description (`gh repo view`, MEASURED) says "A BlishHUD module to help Guild Wars 2 players with crafting efforts" — closer to the truth than the README body, but still generic and undersells the parity work.

**Recommendation** (cleanup, docs-only): Rewrite README to describe the crafting-plan solver as the headline feature, add 2-3 screenshots/GIFs of the actual UI (recipe tree, shopping list, settings tab), add a CI badge, keep build/test section, add an end-user install section (see §6), and link out to `docs/ROADMAP.md`/an architecture doc for depth rather than trying to enumerate every feature inline.

---

## 2. `docs/` organization — internal engineering journal mixed with (absent) public docs

**Files**: `docs/KNOWN-ISSUES.md` (1796 lines / 116 KB), `docs/ROADMAP.md` (609 lines / 40 KB), `docs/gw2e-parity-spec.md` (730 lines / 44 KB), `docs/research/*.md` (3 files, 40-64 KB each), `docs/archive/plans/2026-02-15/*`, `docs/icon_candidates.html`.

### 2a. `KNOWN-ISSUES.md` is not a public-facing "known issues" document, and its name will mislead
A stranger sees a file called "known issues" and expects a short, current list of open bugs/limitations. What's actually there (read the head and tail, MEASURED) is a chronological engineering diary: per-milestone bug hypotheses, root-cause writeups, fix-pass notes, and — critically — an **AI-agent session handoff section** at the tail (lines ~1770-1796) containing:
- Hardcoded personal dev-machine paths: `C:\Dev\Blish\preflight\`, `C:\Dev\Blish\blish-preflight-settings\data`.
- Explicit references to the AI development process: "Sonnet subagents; opus only for hardest verify", "orchestrate-dont-implement", "Blish-over-Paint automation protocol", "activate-verify", environment UI scale settings, etc.
- Operational notes clearly written for the *next Claude session*, not for a human contributor or user ("Project memory holds everything...").

This is exactly the kind of content that must be preserved (it is the evidence trail for essential complexity) but it should **not** be the thing a stranger's browser lands on when they click "docs" looking for known limitations. Two different audiences are being served by one file.

**Recommendation** (structural docs split — mostly mechanical extraction, low risk):
- Keep a short, current, human-readable `KNOWN-ISSUES.md` (or fold into GitHub Issues) listing genuinely open/deferred items only — the "DEFERRED" section at the tail (localization, ignore-pill cascade semantics, Skirmish Merchant page split, etc.) is exactly this and is already well-written for a public audience.
- Move the historical fix-pass narrative into `docs/dev-notes/` or `docs/CHANGELOG-detailed.md`, explicitly marked as an internal engineering log.
- Move or delete the AI-session-handoff / automation-protocol tail entirely - it references local machine paths and an AI-agent orchestration workflow that has no relevance to a public contributor and reads oddly (or reveals process) to an outside audience. If it must be kept for continuity, it belongs in something clearly out-of-repo-scope (e.g. a private gist, or a file excluded from the public remote), not in the file most likely to be read first by a curious visitor.
- **Do not** touch the technical rationale content (root causes, primitives list: `FrameTicker`, `MainThreadMarshal`, `PlanContentHeightMath`, `PlanRelayoutMath`, `DecisionPillPlanner`, `WheelDeltaSanitizer`) - that is the essential-complexity evidence trail this cleanup protects. It should be *relocated and re-titled*, not deleted or reworded.

### 2b. No public architecture doc — the "why is this hairy" knowledge has no discoverable home
There is no `ARCHITECTURE.md`. The genuinely impressive, hard-won rationale for the module's unusual machinery (why `MainThreadMarshal`/`FrameTicker` exist — Blish has no `SynchronizationContext`; why `WheelDeltaSanitizer` exists — a shipped-Blish-binary bug; the scroll restore/verify contest; merged-ceil vendor batching) currently lives only inside the internal engineering diary described in 2a, and only in prose form, not linked from any code file (see §4 for the corresponding in-code gap). A public contributor has no way to discover *why* the code looks the way it does without reading a 1300-line diary end to end.

**Recommendation** (docs, additive): Extract a `docs/ARCHITECTURE.md` (or `docs/design/`) containing just the durable "why" for each piece of essential complexity, written for a contributor audience, with pointers to the relevant `.cs` files and (optionally) back-references to the detailed historical log for provenance. This is the single highest-value new document for this repo's public credibility — it's what turns "why is this so complicated" from a red flag into a demonstration of care.

### 2c. `docs/research/*.md` and `docs/gw2e-parity-spec.md` — genuinely impressive, framed as internal-only, should be surfaced
`docs/gw2e-parity-spec.md` is a normative behavioral spec reverse-engineered from gw2efficiency's actual open-source libraries and live frontend, with exact source citations (commit hashes, file:line references) — this is a standout artifact that most hobby projects never produce. Likewise `docs/research/m37-r*.md` (achievement-bit dedup, vendor caps, batch economics) show real investigative rigor with explicit MEASURED/OBSERVED/INFERRED/UNVERIFIED labeling.

Problems for public consumption:
- These research docs reference ephemeral, session-specific scratch paths as their source-of-truth for re-inspection, e.g. `docs/gw2e-parity-spec.md` line 25-27: `/tmp/claude-1000/.../scratchpad/src/...` — a path that will not exist for any other reader, ever. This is a dangling reference that undermines the doc's credibility for anyone except the original session.
- They're filed under `docs/research/` with milestone-jargon names (`m37-r2-batch-economics.md`) that mean nothing to an outside reader; there's no `docs/research/README.md` index explaining what "research-only, dev-time" means or why gw2efficiency is referenced at all (a newcomer might reasonably wonder if the module calls gw2efficiency at runtime — it explicitly does not, per repo invariant, but that's stated deep in the doc body, not up front in a folder-level index).

**Recommendation**: Keep these — they're an asset — but add a `docs/research/README.md` framing them ("dev-time investigation notes justifying parity decisions; the module itself never calls gw2efficiency at runtime — see CLAUDE.md"), and scrub/replace the dangling scratchpad path references with either "no longer available" or removal of that provenance-pointer sentence (behavior-preserving; it's dead prose, not load-bearing to the spec content itself).

### 2d. `docs/ROADMAP.md` is stale and could mislead about project status
Header states `**Status date**: 2026-02-19`. Current work (per git log, MEASURED) is milestone M37+ dated 2026-07-21 — five months of shipped milestones (M17-M37, roughly 20+ milestones) postdate the roadmap's stated status line, though the file's "Completed Work" table does appear to have been extended past M15 in places (inconsistent — the status date wasn't bumped even as content was appended). A public reader has no reliable signal for "is this still maintained / what's actually done."

**Recommendation**: Either keep ROADMAP.md current on every milestone merge (mechanical) or replace it with a lighter `CHANGELOG.md` (Keep a Changelog style) updated per-release, and let closed milestones live in `docs/archive/`. Given the pace of this repo, a maintained CHANGELOG is more sustainable than a hand-updated roadmap.

### 2e. `docs/icon_candidates.html` — stray dev-scratch artifact in the public docs tree
A milestone-17 icon-picker scratch page (dark-themed HTML mockup for choosing tab icons) sits directly in `docs/` alongside the real documentation, titled "M17 Icon Candidates - GW2 Crafting Helper". Harmless content-wise, but it's exactly the kind of one-off internal artifact that clutters a public docs folder and signals "nobody tidies this."

**Recommendation**: Move to `docs/archive/` or delete (cleanup, trivial, no behavior impact).

---

## 3. Repo hygiene: committed data caches inflate clone size and contradict the project's own rule

**Files**: `ref/wiki_vendor_cache.json` (18 MB, MEASURED via `du`), `ref/vendor_offers.json` (13 MB), `ref/recipes_seed.json` (7.8 MB), `ref/item_name_seed.json` (2.1 MB), `ref/mystic_forge_recipes.json` (848 KB), `ref/recipe_search_seed.json` (540 KB), `ref/item_id_cache.json` (40 KB).

MEASURED: `git ls-files | xargs du -ch` totals **44 MB** of tracked content, of which the `ref/*.json` data files alone account for ~42 MB — i.e. essentially the entire repo's clone weight is these seven data files, not source code. `.git` itself is 38 MB (MEASURED), and `ref/` has been touched across 23 separate commits (MEASURED via `git log --oneline -- ref/`), meaning multiple historical revisions of these large blobs are also retained in pack history, not just the working tree.

This directly contradicts **CLAUDE.md's own stated rule**: *"Intermediate caches (e.g., `wiki_vendor_cache.json`, build artifacts) must NOT be committed unless explicitly requested."* Yet `ref/wiki_vendor_cache.json` is not only committed, it's explicitly documented as intentional in `tools/VendorOfferUpdater/README.md` ("Committed to repo for developer convenience"). This is a live policy contradiction inside the repo's own rules, independent of any public-readiness concern — worth resolving one way or the other regardless of audience.

Separately from the policy contradiction, note (MEASURED via csproj inspection) that of the seven `ref/*.json` files, only `vendor_offers.json`, `mystic_forge_recipes.json`, `recipe_search_seed.json`, `recipes_seed.json`, `recipe_seed_manifest.json`, `item_name_seed.json`, and `acquisition_hints_seed.json` are wired into `<Content Include>` with `CopyToOutputDirectory` in `GW2CraftingHelper.csproj` (i.e. actually ship in the built module). `ref/wiki_vendor_cache.json` (the largest file, 18 MB) and `ref/item_id_cache.json` are **pure dev-tool scratch files** — never shipped in the module output, only consumed by the offline `VendorOfferUpdater` tool. Committing an 18 MB scraper cache that never ships is the single biggest, most mechanical win available here.

**Recommendation** (mixed - most of this is cleanup, one part is a judgment call that should be made explicitly):
- `ref/wiki_vendor_cache.json`: move out of the tracked tree (gitignore it going forward) — it's a regenerable scraper cache with no runtime consumer. If "developer convenience" (avoiding a 15-minute wiki re-scrape) is worth preserving, host it as a release asset or Git LFS object instead of a plain tracked blob re-committed on every refresh.
- `ref/vendor_offers.json`, `recipes_seed.json`, etc.: these *do* ship and *are* load-bearing (the module reads them at runtime per `manifest.json`/csproj) — keep committed, but consider Git LFS for the multi-MB ones to keep a plain `git clone` fast; this is a judgment call, not a correctness issue.
- Regardless of the LFS decision, update CLAUDE.md's cache-commit rule to either (a) actually match reality (name `wiki_vendor_cache.json` as the one exception, with rationale) or (b) enforce the rule and remove the file — right now the rule and the file disagree, which is confusing for future contributors trying to figure out what's actually allowed.
- If removing from history entirely is desired (not just going-forward), that's a history rewrite - flag it explicitly as a deliberate, disruptive action (force-push, breaks existing clones/forks) rather than folding it into a routine cleanup PR.

---

## 4. Essential complexity is well-documented in `docs/` but under-documented in code (the gap this audit flags)

Where essential complexity's rationale lives only in docs and not in code, that gap itself is the finding.

Spot-checked (MEASURED via `grep -c` doc-comment density):
- `Services/PlanSolver.cs` (80 KB): 3 public-signature matches, 244 `///` lines — reasonably documented in-file.
- `Services/CraftingPlanPipeline.cs` (72 KB): 6 signatures, 195 doc lines — reasonable.
- `Services/IRecipeApiClient.cs`: 3 signatures, **0** doc-comment lines — an interface with zero XML docs.
- `Services/RecipeService.cs`: 3 signatures, 48 doc lines — partial.

Did not exhaustively audit every file (out of lens depth for this pass), but the pattern across the named machinery is worth checking directly by a follow-up pass: `Views/MainThreadMarshal.cs`, `Views/ResizableTabbedWindow.cs` / `FrameTicker`-adjacent code, `Services/WheelDeltaSanitizer.cs`, `Services/StatusUpdateGuard.cs`, `Services/PlanContentHeightMath.cs`, `Services/PlanRelayoutMath.cs` - these are precisely the files where KNOWN-ISSUES.md carries paragraphs of hard-won rationale (shipped-binary bug workarounds, synchronous height contracts, scroll-restore contests) that a contributor reading only the `.cs` file would have no way to discover. A one-line class-doc-comment pointing to "see docs/ARCHITECTURE.md#wheel-delta-sanitizer" (once that doc exists per §2b) on each of these classes would close the discoverability gap cheaply and is exactly the kind of "flag, don't fix the machinery" work to surface rather than resolve here.

**Recommendation**: Not "add doc comments everywhere" (that's generic advice) — specifically, ensure each of the named hairy-machinery classes carries a class-level `///` summary that states *why* it exists (one sentence) and cross-references the architecture doc, once §2b exists. This is additive/mechanical, zero behavior risk.

---

## 5. Branding/identity inconsistency and dead version tags

- `LICENSE` copyright: "Lachlan Mulcahy". `manifest.json` `contributors`: `[{"name": "MaximusCub"}]`. Git commit history (MEASURED, `git log --author`) shows 324 commits as "Lachlan Mulcahy <lachlan@me.com>" and 54 as "MaximusCub <MaximusCub@users.noreply.github.com>" — same person, two identities, inconsistently applied across files a stranger will read side-by-side (LICENSE vs manifest vs GitHub org `MaximusCub/GW2CraftingHelper`). Not a defect, but worth a single deliberate decision (pick one name for all public-facing metadata) rather than leaving it split by accident.
- The repo carries two annotated-looking tags, `v1.0.0` and `v2.0.0` (MEASURED via `git for-each-ref`/`git log`), both pointing at the **same commit**, `8b04c08`, dated **2020-07-20** — a leftover from when this repo was forked from `blish-hud/ModuleTemplate` (confirmed: the repo's earliest commits are authored by Dade Lamkins, the Blish HUD template's actual maintainer, e.g. "Create README.md", "Create LICENSE", "Merge branch 'master' of https://github.com/blish-hud/ModuleTemplate"). MEASURED: `v2.0.0` is 381 commits behind current `master`. These tags will show up in GitHub's tag/release UI and falsely suggest the *real* module (this crafting helper) has had two stable releases, when in fact it has never been versioned or released at all — `manifest.json` still reads `"version": "0.1.0"`, and `gh release list` (MEASURED) returns **zero** releases.

**Recommendation**: Delete (or clearly document as template-inheritance artifacts) the `v1.0.0`/`v2.0.0` tags before treating this as a "ready for community" repo — a visitor sorting by tags will otherwise see two-year-old-looking releases that don't correspond to anything real. Separately, establish an actual versioning/release process (bump `manifest.json` version per release, tag, optionally attach the built `.bhm` as a GitHub Release asset — see §6) since none currently exists.

---

## 6. Module packaging story — no visible path from source to an installed module

The README's "Building"/"Testing" sections are the *only* documented path from clone to running code, and they stop at `dotnet build`. There is no documented answer to either question a stranger will actually ask:

- **As a player**: "How do I install this in Blish HUD?" No `.bhm` packaging step is documented anywhere public. There's no GitHub Release with a downloadable `.bhm` (confirmed empty via `gh release list`). Most Blish HUD modules are distributed either through the in-app Blish HUD module repository or a Releases-page `.bhm` download; this project has neither a public listing nor a Release asset. A player cannot currently install this module without building it themselves from source.
- **As a contributor**: how is a `.bhm` even produced from this csproj? Nothing in the csproj shows a pack/zip target (checked — the only "pack"-adjacent hits are NuGet package-restore boilerplate). The one place packaging mechanics are described at all is buried in `docs/KNOWN-ISSUES.md`'s internal handoff notes (§2a): "launch from a COPY of the .bhm so builds stay unlocked" and a reference to `start_blish.ps1` hardcoding "the repo bin .bhm path" — i.e. the packaging/deployment knowledge that exists is entirely informal, tied to one contributor's local automation scripts (`C:\Dev\Blish\preflight\`), and not written down anywhere a new contributor could find or run.
- `manifest.json` declares `"directories": ["data"]` — but the module's actual bundled data lives under `ref/` (per the `<Content Include="ref\...">` entries in the csproj, §3) and there is no `data/` directory anywhere in the repo. This looks like either a leftover from the original module template (Blish HUD's scaffold commonly includes a `data/` directories declaration) that was never updated to match this project's actual `ref/` convention, or a real packaging gap. Flagging as INFERRED / worth a direct verification (attempt an actual `.bhm` pack-and-load) — if `"directories"` genuinely controls what Blish HUD's loader includes from a packaged module, and `ref/` isn't listed, the shipped `.bhm` might not include its own seed data. This is squarely a "should a stranger be able to trust the packaging" finding even though it may turn out to be inert.

**Recommendation**:
1. Verify (cheap, one build) whether `manifest.json`'s `"directories": ["data"]` actually needs to say `"ref"` for a packaged `.bhm` to include the seed files, or whether Blish HUD's packager only respects `<Content>` `CopyToOutputDirectory` regardless of the manifest `directories` field (if so, the manifest key is vestigial and should be removed to stop misleading readers, not fixed).
2. Document an actual pack step (even a simple `zip` of the build output + manifest into a `.bhm`) in a `docs/RELEASING.md` or in the tools/ tree, and wire it into CI so tagged pushes produce a downloadable `.bhm` GitHub Release asset — this is the single most "are you ready for the public" gap: right now there is no way for a non-developer to install this module at all.
3. Once a real release process exists, retire the stale `v1.0.0`/`v2.0.0` tags (§5) and start a real version sequence from `manifest.json`'s current `0.1.0` (or bump to `1.0.0` to mark the "public-ready" milestone - a judgment call).

---

## 7. Missing standard community artifacts

None of the following exist (checked via `find`, MEASURED — all absent): `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `CHANGELOG.md`, `.github/ISSUE_TEMPLATE/*`, `.github/PULL_REQUEST_TEMPLATE.md`, `CODEOWNERS`.

For a project aiming to be an exemplary, high-quality codebase ready for public consumption as a GitHub community project, these are the standard, low-cost signals GitHub itself surfaces (the repo "Insights → Community Standards" checklist will show most of these as red). Given the PR-heavy workflow already documented in CLAUDE.md (branch-per-milestone, strict PR body template, adversarial review gate), a `CONTRIBUTING.md` is nearly free to write - most of the content already exists in CLAUDE.md, just needs to be split into a public-appropriate contributor guide (build/test commands, PR expectations, code style) versus what should stay in CLAUDE.md as agent-specific tooling instructions (exact `dotnet.exe`/`gh.exe` WSL path-probing, for instance, is irrelevant to a contributor on a Mac or native Linux).

**Recommendation** (all additive, no behavior risk):
- `CONTRIBUTING.md`: build/test commands (platform-neutral — CLAUDE.md's WSL-specific `dotnet.exe` path probing is an *agent* convenience, not something to put in front of a human contributor on macOS/Linux), code style summary (Allman, ASCII-only, explicit csproj includes), PR expectations.
- `.github/PULL_REQUEST_TEMPLATE.md`: could reuse the existing "Milestone Goal / What Changed / Validation Performed / Risks" template from CLAUDE.md's PR workflow section almost verbatim.
- `.github/ISSUE_TEMPLATE/bug_report.md` and `feature_request.md`: standard, cheap.
- `CODE_OF_CONDUCT.md`: standard `contributor_covenant` template is the path of least effort.
- `SECURITY.md`: worth having given this module handles GW2 API keys (even though it never persists them beyond what Blish HUD itself manages) — a one-paragraph "how to report a vulnerability" plus a note that the module requests `account`/`characters`/`inventories`/`wallet`/`unlocks` scopes and what it does with them addresses the most likely security question a cautious installer will have (this content already exists informally in the README's "API Permissions" section and can be lightly extended).

CI observation: `.github/workflows/tests.yml` (MEASURED, read in full) is a single clean job — checkout, setup .NET 8, `nuget restore`, `dotnet test -c Release`. It's fine as far as it goes, but: it doesn't build/validate the main `GW2CraftingHelper.csproj` module itself (`-p:Platform=x64`, per CLAUDE.md's own build command) — only the test project. A contributor's PR could pass CI while the actual module fails to build. Also worth a `dotnet build` step added before test, and a status badge in the README (currently absent) so CI health is visible without clicking into Actions.

---

## 8. `.gitignore` — generally solid, one notable soft spot

`.gitignore` (MEASURED, read in full, 348 lines) is the standard GitHub Visual Studio template plus repo-specific additions at the tail (`.claude/settings.local.json`, `bin/`, `obj/`, `.vs/`, `packages/`, root-level `*.dll`/`*.exe`/`*.pdb` artifacts, and one specific exception: `ref/mf_item_id_cache.json`). Verified via `git ls-files`: no `bin/`, `obj/`, `.vs/`, `packages/`, or stray `.user`/`.pdb` files are actually tracked — the ignore rules are working as intended for build output. `.claude/` itself is currently untracked (confirmed via `git status` and `git log --all --diff-filter=A -- '.claude/*'`, which shows only `.claude/settings.local.json` was ever added-then-presumably-removed historically) — no live leak there.

The one gap: the `ref/*.json` cache-vs-shipped-data question (§3) is really a `.gitignore` policy decision that hasn't been made explicitly — right now everything in `ref/` is tracked by default (no `ref/`-specific ignore rules beyond the one `mf_item_id_cache.json` exception), which is how the 18 MB `wiki_vendor_cache.json` got in. Once §3's disposition is decided, `.gitignore` should gain an explicit `ref/wiki_vendor_cache.json` (or `ref/*cache*.json`) line so it can't silently recur on the next scrape-and-commit cycle.

---

## 9. Secrets / personal-data hygiene — clean, no action needed

Actively searched (MEASURED, `grep -riE` across `.cs`/`.json`/`.config` for `api[_-]?key|access[_-]?token|apikey|bearer |authorization:|secret`, plus manual inspection of `App.config`, the vendor/recipe seed files, and `.gitattributes`): **no credentials, tokens, or API keys found**. The handful of "secret" string matches are all legitimate GW2 item names ("Book of Secrets", "Anton's Secret", "Order of Whispers Secret Code") - false positives, verified by direct inspection. `App.config` contains only standard .NET assembly binding redirects, nothing sensitive. No real names/emails beyond the already-public LICENSE copyright line and git commit authorship (§5) were found in tracked content. The only personal-data-adjacent items are local dev-machine file paths (`C:\Dev\Blish\preflight\...`) inside `docs/KNOWN-ISSUES.md` and the `docs/research/*.md` scratchpad references (§2a, §2c) - not secrets, but worth scrubbing as part of the docs restructuring since they're meaningless (and mildly identifying of a local setup) to any other reader.

---

## 10. Repo layout legibility — one structural flag for contributor first impressions

Out of primary lens (this borders the general cleanup/redesign report), but relevant to "what does a stranger find": `Views/CraftingPlanView.cs` is a single 4802-line, single-class file (MEASURED via `wc -l` / class-declaration grep) with no `#region` markers or partial-class split. It is very likely the first file a new contributor opens (it's the main UI for the module's headline feature) and a 4800-line monolith with no internal navigation aids is a legibility barrier independent of whether its logic is correct. Flagging for the synthesis to route to whichever PR handles Views/ restructuring — not proposing a specific split here, since that's a structural/REDESIGN call this report's lens shouldn't pre-empt.

Also noticed: `tools/MysticForgeSeeder/MysticForgeSeeder.csproj` exists and builds standalone but is **not referenced in `GW2CraftingHelper.sln`** at all (verified via `grep -c "MysticForgeSeeder" GW2CraftingHelper.sln` → 0), unlike the sibling tools `VendorOfferUpdater`, `GW2CraftingHelper.Harness`, and `GW2CraftingHelper.RecipeSeeder`, which are all wired into the solution. A contributor opening the `.sln` in Visual Studio will never see this tool exists. It also has no `README.md` (unlike `VendorOfferUpdater`, which has a thorough one) — nor do `GW2CraftingHelper.Harness` or `GW2CraftingHelper.RecipeSeeder`. Of four tool projects under `tools/`, only one (`VendorOfferUpdater`) is documented.

**Recommendation**: Add `MysticForgeSeeder.csproj` to the `.sln` (mechanical, one-line fix), and add short READMEs to `GW2CraftingHelper.Harness`, `GW2CraftingHelper.RecipeSeeder`, and `MysticForgeSeeder` following `VendorOfferUpdater/README.md`'s existing pattern (it's a good template: quick-start, data files it touches, when to re-run).

---

## Prioritized gap list (concrete artifacts to create/restructure)

**High priority — first impression / trust:**
1. Rewrite `README.md` to describe the actual product (crafting solver, not just inventory cache), add screenshots, add CI badge, add end-user install instructions once §6 exists. (§1)
2. Resolve the `ref/wiki_vendor_cache.json` 18 MB dev-only-cache-vs-committed-rule contradiction — either gitignore it going forward or update CLAUDE.md's rule to say why it's the deliberate exception. (§3)
3. Delete or clearly annotate the stale, template-inherited `v1.0.0`/`v2.0.0` tags before any public "releases" story starts. (§5)
4. Establish and document an actual `.bhm` packaging/release process (currently: none exists publicly; a player cannot install this module without building from source). Verify the `manifest.json` `"directories": ["data"]` vs actual `ref/` mismatch while doing so. (§6)

**Medium priority — docs restructuring (mechanical extraction, not rewriting technical content):**
5. Split `docs/KNOWN-ISSUES.md`: public-facing current-issues list stays; historical fix-pass narrative moves to an internal/dev-notes location; AI-session-handoff/automation-protocol tail (personal machine paths, agent-orchestration notes) removed from the public-facing doc entirely. (§2a)
6. Write `docs/ARCHITECTURE.md` extracting the durable "why" for the named essential-complexity machinery (FrameTicker, MainThreadMarshal, WheelDeltaSanitizer, PlanContentHeightMath, etc.), cross-referenced from class-level doc comments in the corresponding `.cs` files. (§2b, §4)
7. Add `docs/research/README.md` framing the research docs, and scrub the dangling ephemeral-scratchpad path references inside them. (§2c)
8. Refresh or replace `docs/ROADMAP.md`'s stale status date with a maintained `CHANGELOG.md`. (§2d)
9. Move `docs/icon_candidates.html` to `docs/archive/`. (§2e)

**Medium priority — standard community scaffolding (additive, cheap):**
10. `CONTRIBUTING.md`, `.github/PULL_REQUEST_TEMPLATE.md`, `.github/ISSUE_TEMPLATE/*`, `CODE_OF_CONDUCT.md`, `SECURITY.md`. (§7)
11. Extend CI to build the main module (`-p:Platform=x64`), not just the test project; add a status badge to README. (§7)

**Low priority — polish:**
12. Reconcile "Lachlan Mulcahy" vs "MaximusCub" branding across LICENSE/manifest.json/GitHub org. (§5)
13. Add `MysticForgeSeeder.csproj` to the `.sln`; add READMEs to `GW2CraftingHelper.Harness`, `GW2CraftingHelper.RecipeSeeder`, `MysticForgeSeeder` following `VendorOfferUpdater/README.md`'s pattern. (§10)
14. Fill the `Services/IRecipeApiClient.cs`-style zero-doc-comment gaps on public interfaces once §6/§2b give a documentation convention to follow. (§4)

---

## What's genuinely impressive and should be surfaced, not hidden

- `docs/gw2e-parity-spec.md` — a normative behavioral spec reverse-engineered from gw2efficiency's actual open-source `recipe-calculation`/`recipe-nesting` packages plus its live frontend, with exact commit-hash/file:line provenance. This is portfolio-quality reverse-engineering work; it deserves a prominent link from the README ("this module's crafting math is spec'd against gw2efficiency's actual source — see docs/gw2e-parity-spec.md") rather than being buried three directories deep.
- The decompile-evidenced bug workarounds (`WheelDeltaSanitizer` for a shipped-Blish-binary scroll bug; `MainThreadMarshal`/`FrameTicker` for Blish's missing `SynchronizationContext`) are the kind of "we found and worked around an upstream bug with evidence" story that makes a project look mature, not messy — once the rationale is extracted into `docs/ARCHITECTURE.md` (§2b) it becomes a selling point instead of an undocumented reader-confusion risk.
- 800+ tests (per KNOWN-ISSUES.md's own handoff note: "Tests: 812 green on master") with an explicit Blish-free testing discipline (real production code paths, no Blish HUD references in tests) is a genuinely strong engineering practice worth stating plainly in a CONTRIBUTING.md as a project value, not just enforced silently via CLAUDE.md rules a public contributor never sees.
