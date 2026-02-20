# GW2CraftingHelper - Project Rules

## Build & Test

- Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64`
- Tests: `dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`
- `.csproj` uses explicit `<Compile Include>` - new `.cs` files must be registered
- Changes must be incremental with logical git commits
- Prefer one commit per logical step (e.g., refactor, behavior change, tests, UI polish)

---

## Code Style

- Use Allman brace style for C#
- Keep edits focused and minimal
- Avoid unrelated refactors or formatting churn
- Follow existing patterns in neighboring files before introducing new structure
- **ASCII-only in source (.cs):** Source files must contain only ASCII characters (U+0000-U+007F). Do not paste raw Unicode into code, comments, or string literals. If Unicode must be shown at runtime (UI glyphs, item names, etc.), represent it using escapes (e.g., `"\u25BC"` / `"\u25B6"`) or data returned by the GW2 API.
- **No em-dashes in source or config:** Never use em-dash (`\u2014`) in source code, comments, string literals, config files, test code, or any non-user-facing text unless specifically required. Use a plain ASCII hyphen (`-`) or double-hyphen (`--`) instead. Em-dashes are only acceptable in correctly-encoded user presentation layers (e.g., UI text rendered via BlishHUD controls).

---

# Repo Invariants (Non-Negotiable)

These rules MUST always be followed. They override any conflicting defaults.

---

## Testing

- Tests must exercise **real production code paths**
- No contract-mirror tests
- No fake logic tests
- No fake file I/O tests
- Use real `SnapshotStore` / `StatusStore` with temporary directories when testing storage
- Tests must NEVER reference:
  - Blish HUD
  - BlishHUD.exe
  - Gw2Sharp
  - Any UI code

Tests must remain completely Blish-free.

---

## UI & Display

- Item, currency, and vendor IDs are internal-only - never display them to users
- Coin icons MUST appear to the RIGHT of the number (matching GW2 in-game style):

  `123[gold icon] 45[silver icon] 67[copper icon]`

- This applies everywhere coin amounts are shown: coin panel, tooltips, item values, vendor prices, etc.
- GW2 coin asset IDs:
  - Gold = 156904
  - Silver = 156907
  - Copper = 156902

---

## Data & APIs

- Prefer official GW2 APIs (`api.guildwars2.com`)
- Do not invent data when APIs are missing
- `gw2efficiency` is research-only - the module must NEVER call it at runtime
- Pricing logic must preserve multiple sources and avoid invalid currency comparisons

---

# Self-Review After Every Edit (Edit -> Review -> Fix Loop)

Goal: Reduce back-and-forth by enforcing a deliberate adversarial review mindset after every runtime-affecting change.

---

## Code Reviewer Mode - REQUIRED MINDSET SHIFT

When entering Code Reviewer Mode, you MUST change perspective:

- You are no longer the author.
- You are a skeptical senior engineer reviewing someone else's pull request.
- Assume the author made subtle mistakes.
- Actively try to break the code mentally.
- Look for edge cases, regressions, and invariant violations.
- Challenge assumptions.
- Look for architectural drift.
- Look for hidden coupling.
- Look for future merge conflicts.
- Do NOT defend the implementation.
- Your job is to find faults.

---

## When to Apply

Apply this loop for any change affecting:

- Code
- Tests
- Config
- Build behavior
- Runtime logic

Docs-only changes may skip the strict adversarial pass but must be checked for duplication, contradictions, and stale guidance.

---

## Per-File Review Process

After modifying ANY runtime-affecting file:

1. Pause.
2. Switch to Code Reviewer Mode.
3. Review ONLY the file you just changed (plus directly impacted call sites/tests if necessary).

During review, explicitly evaluate:

- What happens with null inputs?
- What happens with empty collections?
- What happens with large inputs?
- What happens under cancellation?
- What happens under API failure?
- Could this produce inconsistent state?
- Could this break existing tests?
- Does this violate any Repo Invariants?
- Does this introduce unintended coupling?
- Does this create future merge hotspots?
- Is error handling correct and consistent?
- Are there race conditions?
- Is duplicated logic introduced?
- Are tests proving behavior or merely mirroring implementation?

---

## Reviewer Checklist - Best Practices and Performance (Diff-Scoped)

During Code Reviewer Mode, evaluate the change **relative to the existing codebase**. The goal is to prevent introducing new problems, not to redesign the project.

### Standards and Consistency

- Does this follow existing repo patterns (naming, layout, logging, DI usage)?
- Does this match established structure in neighboring files?
- Did this introduce a new abstraction, helper, or pattern unnecessarily?
  - If yes, can it reuse an existing pattern instead?

### Scope Discipline

- Is the change narrowly scoped to the task?
- Did it sneak in unrelated refactors?
- Did it expand public surface area without necessity?
- Did it increase coupling between modules?

### Performance (Regression Prevention Only)

Focus on the delta, not a whole-project performance audit.

- Does this add new work in hot paths (UI render, plan generation loops)?
- Does it introduce new allocations inside loops?
- Does it add repeated API/network calls?
- Does it introduce blocking or long-running work on the UI thread?
- Does it increase memory retention (unbounded lists, caches, logs)?
- Does it degrade behavior on low-end systems (polling, timers, excessive updates)?

If a likely regression is detected, it is at least **Must Fix** unless clearly justified.

If performance-sensitive code was touched, the review must include at least one explicit note about allocation/work frequency impact.

### Efficiency Principle

Prefer simple, predictable solutions over clever ones.
Avoid adding infrastructure or framework-like patterns unless explicitly required by the milestone.

---

## Issue Classification

Every issue must be classified as exactly one of:

- **Critical**
  - Crashes
  - Broken build/tests
  - Incorrect logic
  - Data corruption
  - Severe regression
  - Violates repo invariants

- **Must Fix**
  - Likely bug
  - Edge case failure
  - Test gap that risks regression
  - Performance trap
  - Leaky abstraction
  - Future merge hazard
  - Misleading API surface

- **Nice to Have**
  - Minor refactor
  - Readability improvement
  - Micro-optimization
  - Non-blocking polish

---

## Mandatory Fix Loop

- Fix ALL **Critical** issues.
- Fix ALL **Must Fix** issues.
- Re-run the review mentally.
- Repeat until:
  - Zero Critical
  - Zero Must Fix

Only then proceed to another file.

---

## End-of-Milestone Adversarial Review

After milestone completion:

1. Review the entire change set as if you are an external reviewer unfamiliar with the code.
2. Evaluate:
   - Cross-file consistency
   - API coherence
   - Architecture alignment
   - Test realism (real behavior vs mirrored logic)
   - Regression risk
   - Repo invariant compliance
3. Again classify findings as Critical / Must Fix / Nice to Have.
4. Automatically fix all Critical and Must Fix.
5. Repeat until clean.

---

## Reviewer Integrity Rule

If you cannot find at least one Nice to Have item during review of a non-trivial change, assume you did not review deeply enough and review again.

The goal is defensive engineering, not perfection.

---

# PR Workflow (STRICT)

All review happens via GitHub Pull Requests.

## Branch

- Create a dedicated milestone branch:
  `git switch -c <milestone-branch>`
- Branch name must reflect the milestone.

## Validation

Run:

`dotnet build GW2CraftingHelper.csproj -p:Platform=x64`  
`dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`

Both must pass before PR creation.

## Commit & Push

- Commit logically grouped changes.
- Push:
  `git push -u origin <milestone-branch>`

## GitHub CLI (`gh`)

`gh` is installed but NOT on the shell PATH. Always invoke it via its absolute path:

```bash
"/c/Program Files/GitHub CLI/gh.exe"
```

Example: `"/c/Program Files/GitHub CLI/gh.exe" pr create ...`

## PR Creation

Use:

`"/c/Program Files/GitHub CLI/gh.exe" pr create --base master --head <milestone-branch> --title "<concise milestone title>" --body-file <tempfile>`

### Required PR Body Template

```
## Milestone Goal
Brief description of what this milestone accomplishes.

## What Changed
High-level summary grouped logically (not per-file noise).

## Validation Performed
- Build command run and result
- Test command run and result
- Manual validation steps (if applicable)

## Repo Invariants Checklist
- [ ] No Blish HUD references added to tests
- [ ] Tests exercise real production paths
- [ ] No fake file I/O tests introduced
- [ ] Pricing logic preserves multi-source correctness
- [ ] IDs remain internal-only (not displayed)

## Risks / Follow-ups
Known tradeoffs, edge cases, or future improvements.
```

If a PR already exists:
- Push additional commits to the same branch.
- Update the PR body to reflect the current state.

---

# Terminal Output Rules (End of Milestone)

At milestone completion, output ONLY:

- PR URL
- Short consolidated summary:
  - What changed (high level)
  - Build/test results
  - Remaining Nice to Have items
- Any special reviewer notes

Do NOT include inline diffs, file dumps, or large pasted code blocks.

---

## Intermediate / Cache Files

- Intermediate caches (e.g., `wiki_vendor_cache.json`, build artifacts) must NOT be committed unless explicitly requested.
- If such files exist in the working tree, exclude them and mention them in the summary.

---

## Final Notes

- Never skip the immediate review after editing a runtime-affecting file.
- Update/add tests as part of Must Fix when needed to prevent regressions.
- Keep changes small and focused.
- Do not introduce any Blish HUD/BlishHUD.exe references into tests.
- Always preserve real production code path coverage.
