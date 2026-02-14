# GW2CraftingHelper — Project Rules

## Build & Test

- Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64`
- Tests: `dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`
- .csproj uses explicit `<Compile Include>` — new .cs files must be registered
- Changes must be incremental with logical git commits

## Code Style

- Use Allman brace style for C#

## Repo Invariants

These rules MUST always be followed. They override any conflicting defaults.

### Testing

- Tests must exercise real production code paths — no contract-mirror or fake logic tests
- Tests must NEVER reference Blish HUD, Blish HUD.exe, Gw2Sharp, or any UI code; test non-UI logic only
- No fake file I/O tests — use real SnapshotStore / StatusStore with temp directories

### UI & Display

- Item, currency, and vendor IDs are internal-only — never display them to users
- Coin icons MUST appear to the RIGHT of the number (matching GW2 in-game style):
  `123[gold icon] 45[silver icon] 67[copper icon]`
  This applies everywhere coin amounts are shown: coin panel, tooltips, item values, vendor prices, etc.
- GW2 coin asset IDs: Gold = 156904, Silver = 156907, Copper = 156902

### Data & APIs

- Prefer official GW2 APIs (api.guildwars2.com); do not invent data when APIs are missing
- gw2efficiency is research-only — the module must NEVER call it at runtime
- Pricing must preserve multiple sources and avoid invalid currency comparisons

## Self-Review After Every Edit (Edit → Review → Fix Loop)

Goal: Reduce back-and-forth by enforcing an automatic reviewer pass after every file change, plus an end-of-milestone holistic review.

### When to apply

- Apply this rule for any milestone, feature, refactor, or bugfix task that involves code changes.

### Operating loop (per file)

Whenever you modify ANY file (code, tests, configs, docs), you MUST immediately switch into **Code Reviewer Mode** before editing any other file.

**Code Reviewer Mode requirements**

1. Review ONLY the changes you just made in that file (plus directly impacted call sites/tests if necessary).
2. Identify issues and classify each as exactly one of:
   - **Critical**: correctness, crashes, data loss, security, broken build/tests, wrong behavior, severe regressions.
   - **Must Fix**: likely bugs, edge-case failures, bad API usage, test gaps that risk regressions, performance traps, maintainability problems that will cause churn soon.
   - **Nice to Have**: style polish, minor refactors, readability, small optimizations, non-blocking test additions.

**Fix/check loop**

- You MUST fix all **Critical** and **Must Fix** issues without pausing to ask the user.
- After fixing, re-run the reviewer pass for that same file.
- Repeat until there are **zero Critical** and **zero Must Fix** issues remaining for that file.
- Only then proceed to the next file.

### End-of-milestone validation (whole change set)

After you believe the milestone is complete:

1. Switch into Code Reviewer Mode for the entire milestone.
2. Validate the set of changed files as a group:
   - cross-file consistency
   - architecture/structure coherence
   - public API changes and usage
   - error handling and logging
   - unit/integration tests coverage and realism
   - build/test pass likelihood
   - risk of regressions
3. Again classify findings as Critical / Must Fix / Nice to Have.
4. Automatically fix all Critical and Must Fix items.
5. Repeat the full-change-set review until satisfied that **no Critical** and **no Must Fix** remain.

### Output requirements at the end (before asking for commit approval)

When the milestone is fully validated:

- Provide a consolidated summary:
  - What changed (high level)
  - Validation performed (what you checked)
  - Remaining **Nice to Have** items (bullet list)
- Provide diffs for all changed and new files, following the rules below.
- Do NOT commit automatically.
- Ask for explicit approval to commit ("Reply OK to commit").
- Only after the user replies OK may you proceed with git commit.

#### Diff rules — code and small files (STRICT)

For `.cs`, `.csproj`, `.md`, `.json` files under 200 KB, and any other human-authored file:

- **FULL UNIFIED DIFF REQUIRED — NO EXCEPTIONS.** Run `git diff` (tracked) or `git diff --no-index /dev/null <file>` (new untracked). Print the COMPLETE unified diff output inline — every added, removed, and context line, for every file. Do NOT summarize, abbreviate, paraphrase, elide with "...", or replace diff content with descriptions. If the diff is large, split it per-file across multiple message sections, but still print every line of every file's diff. **Truncating or omitting lines from a code diff is a Critical violation.**

#### Diff rules — large generated artifacts (PRACTICAL)

For generated data files over 200 KB (e.g. `vendor_offers.json`, large caches):

- Do NOT dump the full inline diff (it is unhelpful multi-MB JSON noise).
- Instead, provide ALL of the following:
  - `git diff --stat` line for the file
  - File size in bytes (`wc -c` or `FileInfo.Length`)
  - SHA256 hash of the file (`sha256sum` or equivalent)
  - Whether the file was fully regenerated or partially appended
  - Whether the output is deterministic (same inputs → same output)
  - Number of top-level records (e.g. "53,531 offers")

#### Intermediate / cache files

- Intermediate caches (e.g. `wiki_vendor_cache.json`, build artifacts) must NOT be committed unless the user explicitly requests it.
- If such files exist in the working tree, note them in the summary and confirm they are excluded from the commit.

### Notes

- Never skip the immediate review after a file edit.
- If tests exist for the affected behavior, update/add tests as part of "Must Fix" when needed to prevent regressions.
- Prefer small, safe edits per loop. Keep changes focused and consistent with existing project style.
- Do not introduce any Blish HUD/BlishHUD.exe references into tests; keep tests Blish-free and exercise real production code paths (no contract-mirror tests, no fake file I/O tests).
