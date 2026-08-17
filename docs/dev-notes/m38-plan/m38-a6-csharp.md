# M38 Post-Parity Cleanup Analysis - Lens: C#/.NET Practices

Scope: current `master` (commit `be8ebda`, "Merge pull request #56 from
MaximusCub/m37-ach-dedup"). Read-only static analysis; no build was run (the
repo rule for this session was strict read-only, and a build writes to
`bin/`/`obj/` in a checkout shared with concurrent agents, so anything here
that would benefit from compiling was left as a proposed cheap check instead
of being executed). All file/line references are to real source under the
repo root; `.claude/worktrees/*` (agent scratch checkouts of this same repo)
and `packages/`, `bin/`, `obj/`, `.vs/` were excluded from every scan below.

**Headline finding first, because it is the one thing every other section
depends on getting right:** this codebase is already unusually disciplined
for a hobby Blish HUD module - named constants with provenance comments,
zero-allocation pure-math helpers, decompiled-evidence doc comments on the
gnarly machinery, consistent `CancellationToken` threading, and a real
Blish-free unit-test boundary that is respected almost everywhere. The
findings below are mostly small, mechanical, and low-risk *because* the
baseline is already good - do not read the length of this report as "this
codebase is a mess." A few items (nullable reference types, the Models
mutability pattern) are honestly REDESIGN-scale and are labeled as such.

---

## Priority-ordered summary

| # | Finding | Class | Severity |
|---|---|---|---|
| 1 | `Debug.WriteLine` in 4 persistence classes is compiled to a no-op in Release builds, silently swallowing file I/O failures | CLEANUP (needs a small seam) | **Must Fix** |
| 2 | `Module.cs` has 6 structurally-identical "optional resource missing" catch blocks; only 2 log, 4 are silent | CLEANUP | **Must Fix** |
| 3 | No `.editorconfig` / analyzer / ruleset despite CLAUDE.md documenting firm style rules (Allman, ASCII-only, no em-dash) | CLEANUP (additive) | Must Fix |
| 4 | `<LangVersion>` unpinned in the legacy (non-SDK) main `.csproj`; toolchain-drift risk for outside contributors | CLEANUP | Must Fix |
| 5 | Nullable reference types not enabled anywhere in the solution | REDESIGN (but compile-time-only, safe to phase in) | Must Fix (long-term) / quick win available on the two net8.0 tool projects |
| 6 | Identical 13-line currency-metadata await/catch block duplicated 3x in `CraftingPlanPipeline.cs` | CLEANUP | Nice to Have |
| 7 | Repeated `_settings != null && _settings.ScrollDiagnosticsEnabled.Value` guard duplicated ~6x in `CraftingPlanView.cs` | CLEANUP | Nice to Have |
| 8 | Log-level flattening: 84x `Logger.Warn`, 0x `Logger.Error`/`Fatal` across the whole repo | CLEANUP | Nice to Have |
| 9 | `[scrolldiag]`-style gated diagnostics is excellent but bespoke to one subsystem | CLEANUP (extract a tiny shared primitive) | Nice to Have |
| 10 | `CraftingPlanView`'s `FrameTicker`s are parented to the global `SpriteScreen` and are never explicitly stopped from `Module.Unload()` | CLEANUP | Nice to Have (already has a defensive fallback - see caveat) |
| 11 | Five layout-math structs (`PillSpec`, `TreeColumnEdges`, `CostTileGeometry`, `ColumnEdges`, `RowInput`) are mutable, non-`readonly` structs | CLEANUP | Nice to Have |
| 12 | Vestigial csproj cruft: no-op `Prefer32Bit` under x64-only configs, empty `DocumentationFile`, dead legacy `CodeAnalysisRuleSet` | CLEANUP | Nice to Have |
| 13 | Two unused legacy `<Reference>`s (`System.Numerics`, `AsyncClipboardService`) | CLEANUP | Nice to Have |
| 14 | Result/DTO `Models/*.cs` are uniformly mutable (`{ get; set; }`) even though most flow one-way through the pipeline | REDESIGN | Nice to Have / flag only |
| 15 | Hardcoded UI/status string literals (informational - localization is deferred, not proposing a fix) | n/a | Flag only |

Sections below expand on each, plus a section explicitly confirming what
*not* to touch.

---

## 1. Exception handling & logging consistency (the biggest real gap)

### 1a. `Debug.WriteLine` is a Release no-op - Must Fix

`System.Diagnostics.Debug.WriteLine(...)` calls are `[Conditional("DEBUG")]`
and are **erased by the compiler** in any build that doesn't define
`DEBUG`. The shipped Release configuration
(`GW2CraftingHelper.csproj`, `PropertyGroup` for `Release|x64`) sets
`<DefineConstants>TRACE</DefineConstants>` only - no `DEBUG`. That means
every one of these catch blocks is **completely silent** in the binary
real users run:

- `Services/SnapshotStore.cs:27,43,55` (load/save/delete snapshot failures)
- `Services/StatusStore.cs:25,43` (load/save status failures)
- `Services/VendorOfferStore.cs:38,67` (vendor baseline/overlay load failures)
- `Services/Recipes/OverlayRecipeCacheStore.cs:65,97,119,140(?),246,284` (recipe overlay cache read/write failures)

14 call sites across 4 files (grep: `Debug.WriteLine` in real repo tree).
Every one of these guards a `try/catch (Exception ex)` around real local
file I/O (`File.ReadAllText`, `File.WriteAllText`, `File.Replace`, JSON
deserialize) that already degrades gracefully on failure (returns `null`,
falls back to an empty dataset, etc.) - the *behavior* is fine. The problem
is purely diagnostic: a maintainer triaging "my snapshot never saves" or
"my vendor overlay keeps resetting" from a user bug report gets zero trail
today, in a Release build, forever.

**Why this isn't a one-line "just use `Logger`" fix**: none of these four
classes currently reference `Blish_HUD` at all, and all four are exercised
directly by Blish-free unit tests using the real class with a temp
directory (`tests/GW2CraftingHelper.Tests/Services/SnapshotStoreTests.cs`,
`StatusStoreTests.cs`, `VendorOfferStoreTests.cs`,
`RecipeCacheStoreTests.cs`) - exactly the repo-invariant pattern CLAUDE.md
calls out ("Use real `SnapshotStore`/`StatusStore` with temporary
directories"). Across the whole real (non-worktree) source tree, only 6
files ever call `Blish_HUD.Logger.GetLogger` (`Module.cs`,
`Services/Gw2AccountSnapshotService.cs`, and 4 files under `Views/`) - i.e.
there is an existing, apparently deliberate architectural line: the
Blish-free-tested Services layer (`PlanSolver`, `CraftingPlanPipeline`,
`InventoryReducer`, the four stores above, etc.) carries **zero**
`Blish_HUD` dependency, and logging only exists in the Module entry point
and the View layer. Adding `Blish_HUD.Logger` directly to these four store
classes would quietly erode that boundary.

**Recommended fix (mechanical, respects the boundary)**: give each store an
optional injected failure-sink instead of a hard Blish dependency, e.g.
```csharp
public SnapshotStore(string dataDirectoryPath, Action<string, Exception> onError = null)
```
with `onError` defaulting to a no-op, called instead of
`Debug.WriteLine` on every catch path. `Module.cs` wires the real
`Blish_HUD.Logger.Warn` in at construction time (it already constructs all
four of these: `Module.cs:58-59` `SnapshotStore`/`StatusStore`, and the
`VendorOfferStore`/`OverlayRecipeCacheStore` construction sites nearby).
Tests keep using the parameterless constructor and stay exactly as
Blish-free as today; a test can optionally assert on the callback if a
future test wants to prove a failure path actually reported something.
This is behavior-preserving except that Release users now get a log line
where they previously got total silence - which is the point.

### 1b. Inconsistent silent-vs-logged catch blocks within `Module.cs` - Must Fix

`Module.cs` has 6 bare `catch` blocks (no exception type, no variable) for
what is the same conceptual scenario each time - "an optional embedded/game
resource is missing or unready, degrade gracefully":

- `Module.cs:114` (vendor baseline stream) - silent
- `Module.cs:130` (recipe search/recipe seed streams) - silent (comment: "No seed files yet - graceful degradation")
- `Module.cs:142` (recipe seed manifest stream) - silent (comment: "No manifest - staleness detection disabled")
- `Module.cs:223` (module icon texture) - silent
- `Module.cs:232` (emblem texture) - silent
- `Module.cs:270` (`GameService.Gw2Mumble` active-character lookup) - silent (comment: "Gw2Mumble unavailable - graceful fallback")

Compare these to two structurally identical "optional resource missing,
fall back" scenarios a few lines away in the *same method* that **do**
log:

- `Module.cs:162-165` - item-search-provider fallback: `Logger.Info("Item search fallback to static provider: [{0}] {1}", ex.GetType().Name, ex.Message)`
- `Module.cs:181-185` - acquisition-hints unavailable: `Logger.Info("Acquisition hints unavailable: [{0}] {1}", ex.GetType().Name, ex.Message)`

There is no reason the vendor-baseline-missing or recipe-seed-missing paths
should be less observable than the item-search-fallback path right next to
them - they are the same class of "shipped seed file didn't load" failure.
This is a pure logging-consistency gap (fix the class, not the instance):
add the same `Logger.Info("<what fell back>: [{0}] {1}", ex.GetType().Name, ex.Message)`
shape to all 6 sites, using `catch (Exception ex)` instead of bare `catch`
so there's something to log. Zero behavior change to the fallback logic
itself.

The same silent bare-`catch` pattern (single catch-all, no logging, no
variable) also appears, for the same "degrade gracefully" reason, in:
`Services/SnapshotHelpers.cs:108` (bad JSON -> null), `Services/RecipeClientFactory.cs:17`
(missing Mystic Forge data file -> empty dataset), and
`Services/VendorOfferResolver.cs:153` (wiki lookup retry loop, exhausted
retries -> null). These three are lower priority than the `Module.cs` set
(they're each the *only* handler for their scenario, so there's no sibling
inconsistency to point at - just an absence of a log line), but they belong
in the same fix-pass since they're the same root pattern.

### 1c. Triplicated currency-metadata swallow in `CraftingPlanPipeline.cs` - Nice to Have (CLEANUP)

The exact same ~13-line block appears three times, once per public entry
point:

- `CraftingPlanPipeline.cs:178-190` (inside `GenerateAsync`, line 50)
- `CraftingPlanPipeline.cs:429-441` (inside `GenerateStructuredAsync`, line 212)
- `CraftingPlanPipeline.cs:758-770` (inside `GenerateStructuredMultiAsync`, line 585)

```csharp
try
{
    currencyMetadata = await currencyTask;
}
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    throw;
}
catch (Exception)
{
    currencyMetadata = null;
}
```
with an identical doc comment above each copy ("Any failure besides genuine
caller cancellation is swallowed here too, consistent with the service's
own contract of returning an empty result"). The *design* is fine and
already well-documented (currency icon/name display degrades to the
offline `Gw2Constants` text fallback - not a bug), but the triplication
means a future change to this contract (e.g. "actually log this one") has
to be made identically in three places or it silently drifts. Extract a
single private helper, e.g.
`AwaitCurrencyMetadataOrNullAsync(Task<IReadOnlyDictionary<int, CurrencyMetadata>> currencyTask, CancellationToken ct)`,
and call it from all three methods. Purely mechanical, behavior-preserving.
(This is the same trio of methods KNOWN-ISSUES.md #21.1 describes as the
"synthetic wrapper pipeline" from M35 - worth being aware the wrapper
refactor already touches these three methods' shared shape, so this
extraction is a small, complementary follow-up, not a competing change.)

The identical status-message string literals inside these same three
methods (`"Fetching currency details..."`, `"Collecting item IDs..."`,
etc. - each appears twice, once per near-duplicate method) are the same
duplication, one layer up; see the localization note in the last section.

### 1d. Log-level flattening - Nice to Have

Across the real repo tree: `Logger.Warn` is called 84 times, `Logger.Debug`
27 times, `Logger.Info` 31 times, and `Logger.Error`/`Logger.Fatal`
**zero** times (measured via grep over the 6 files that use
`Blish_HUD.Logger` at all). Genuinely unexpected failures (e.g.
`MainThreadMarshal.cs:58` "MainThreadMarshal queued action threw",
`Views/CraftingPlanView.cs:507` FrameTicker step failures) and genuinely
recoverable/expected degradations (e.g. a single character's inventory
fetch failing in `Gw2AccountSnapshotService.cs:168`) are logged at the same
`Warn` level. This isn't wrong, but it flattens severity for anyone
triaging a user's Blish log - consider reserving `Error` for failures that
visibly break a user-facing feature (a whole plan generation failing, a
snapshot never refreshing) versus `Warn` for a single degraded data point
inside an otherwise-successful operation. Zero behavior/perf impact either
way - purely a triage-signal improvement.

### 1e. `[scrolldiag]` gating pattern is excellent but a one-off - Nice to Have

`Views/CraftingPlanView.cs:344` (`ScrollDiagTag`) plus roughly a dozen call
sites is a genuinely good pattern: a settings-gated (`ModuleSettings.ScrollDiagnosticsEnabled`,
default false), zero-cost-when-disabled, tagged diagnostic log used to
live-debug the scroll-restore/verify contest documented at length in
`docs/KNOWN-ISSUES.md` #12/#23. It is correctly implemented (every call site
checks the bool *before* building the format string, per the comment at
`CraftingPlanView.cs:338-343`). The one gap: the exact same
`bool diagEnabled = _settings != null && _settings.ScrollDiagnosticsEnabled.Value;`
expression is repeated verbatim at `CraftingPlanView.cs:567, 647, 655, 977,
1005, 1065, 1638` - a private property (`private bool ScrollDiagEnabled =>
_settings != null && _settings.ScrollDiagnosticsEnabled.Value;`) would
remove the duplication and the (currently theoretical) risk of a new call
site forgetting the null check. Separately: this gated-diagnostics *shape*
(settings-gated bool -> tagged `Logger.Debug` -> monotonic frame counter)
is valuable enough that a small shared helper (e.g. a `DiagLog` static
class taking a tag, an enabled-flag, and a format+args) would let a future
investigation into `PlanContentHeightMath`'s height contract or
`StatusUpdateGuard`'s race handling - both call out as similarly gnarly in
`docs/KNOWN-ISSUES.md` - reuse the same low-noise pattern instead of
reinventing the gating boilerplate. This is additive, not a rewrite of the
existing scroll-diagnostics code.

---

## 2. Async hygiene

This area is in genuinely good shape; the two things worth flagging are a
gap and a warning against a "fix" that would actually be wrong here.

### 2a. `async void` - already handled correctly, one under-commented sibling pattern

The only `async void` in the real codebase is
`Views/SuggestionPanel.cs:57` (`OnTextChanged`). It is already exemplary:
every exception path is caught (`OperationCanceledException` then a
catch-all), the continuation is marshaled back onto the main thread via
`MainThreadMarshal.Run` before touching any Blish control, and there's a
long comment (`SuggestionPanel.cs:88-103`) explaining exactly why (Blish's
XNA host installs no `SynchronizationContext`, so a future async search
provider could resume off-thread). Nothing to fix here - flagging only so
this doesn't get "corrected" into something worse by a well-meaning future
PR that doesn't have this context.

### 2b. `ObserveFault` is correct but has no comment explaining why it exists

`Services/CraftingPlanPipeline.cs:1435-1440`:
```csharp
private static void ObserveFault(Task task)
{
    task?.ContinueWith(
        t => { var _ = t.Exception; },
        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
}
```
called at `CraftingPlanPipeline.cs:160, 411, 747` immediately after starting
`_currencyMetadataService?.GetAllAsync(ct)` as a task that isn't `await`ed
until later. This is the right defensive pattern: between task creation and
the later `await currencyTask`, another `await` in between (item metadata
fetch) can throw and exit the method before `currencyTask` is ever awaited,
which would otherwise leave its fault unobserved. It's correct, but there
is **zero comment** at any of the 3 call sites explaining this - it reads
like a no-op/dead call to anyone unfamiliar with the reasoning, which makes
it a good candidate for accidental deletion in a future "why is this here"
cleanup pass. This is exactly the "essential complexity under-commented in
code" gap called out for this analysis: the fix is a one-line comment, not
a change to the mechanism.

### 2c. `ConfigureAwait(false)` - correctly absent; do not add it

Zero occurrences of `ConfigureAwait` in the real repo (measured). This is
**correct for this host**, not a gap: `MainThreadMarshal.cs`'s own doc
comment already establishes that Blish HUD's XNA host installs no
`SynchronizationContext`, meaning `SynchronizationContext.Current` is
already `null` on every `await` in this process - there is no captured
context for `ConfigureAwait(false)` to skip, so it would add visual noise
for zero behavioral benefit here. This matters because it's exactly the
kind of "best practice" that a generic C# linter/analyzer (or a
well-meaning contributor coming from ASP.NET/WPF) would flag as missing.
**Recommendation**: if any Roslyn analyzer package is adopted (see the
StyleCop section below), explicitly leave `CA2007`
("ConfigureAwaitAnalyzer" / do-not-forget-ConfigureAwait) disabled, and
add a one-line note next to `MainThreadMarshal`'s existing doc comment
(or in `CLAUDE.md`) saying so, so this doesn't get "fixed" by someone
running a generic analyzer later.

### 2d. No blocking `.Result`/`.Wait()` in production code

Confirmed via grep: the only `.GetAwaiter().GetResult()` calls in the whole
repo are in the two CLI tool `Main` methods
(`tools/GW2CraftingHelper.Harness/Program.cs:67`,
`tools/GW2CraftingHelper.RecipeSeeder/Program.cs:25`), which is the
standard, correct shape for a synchronous `Main` bootstrapping an async
body. No `.Result`/`.Wait()` anywhere else. Nothing to fix.

---

## 3. IDisposable & event unsubscription

### 3a. `SuggestionPanel` and `ModalDialog` - exemplary, no changes needed

Both (`Views/SuggestionPanel.cs`, `Views/ModalDialog.cs`) implement
`IDisposable` correctly: every `+=` in the constructor has a matching `-=`
in `Dispose()`, `CancellationTokenSource`s are canceled-then-disposed, and
child controls are disposed before being dropped. Verified line-by-line;
no findings.

### 3b. `CraftingPlanView` has no `IDisposable` surface, and its `FrameTicker`s escape the normal Blish disposal cascade - Nice to Have

`Views/CraftingPlanView.cs` (4,802 lines) does not implement `IDisposable`
and is not itself a Blish `Control` (it's a plain composition class driven
via `Build(Container)` from a `ViewAdapter`, wired at
`Module.cs:328`: `() => new ViewAdapter("Crafting Plan", c => _craftingContent.Build(c))`).
Its actual visual tree (panels, labels, rows) is parented under the
container Blish gives it, so ordinary Blish control disposal handles that
part fine.

The exception is its nested `FrameTicker : Control` helper
(`CraftingPlanView.cs:457-538`), used for the scroll-verify,
resize-debounce, and wheel-wrap-verify sequences documented at length in
`docs/KNOWN-ISSUES.md` #12/#13/#19/#23. Each `FrameTicker` parents itself
directly to `GameService.Graphics.SpriteScreen` (`CraftingPlanView.cs:479-482`)
- the global overlay root - specifically so it keeps receiving `DoUpdate`
even if the plan's own content panel gets torn down mid-sequence. Every
ticker is bounded and self-disposing on completion (`Cancel()` calls
`Dispose()`, `CraftingPlanView.cs:532-537`), and `Build()` proactively
cancels any leftover ticker from a prior build cycle
(`CraftingPlanView.cs:1308-1313`) - both of those are correct, intentional,
and already well-commented.

The gap is specifically **module unload**: `Module.Unload()`
(`Module.cs:432-443`) disposes `_modalDialog`, `_cornerIcon`, and
`_mainWindow`, but never touches `_craftingContent` or anything it owns.
If the user disables the module while a bounded verify sequence happens to
be mid-flight (per the docs, "at most 2 real frames" for scroll verify,
"at most 30 frames" for the old restore path), that `FrameTicker` keeps
running against a panel tree that `_mainWindow.Dispose()` just tore down.
**This is not a crash risk today** - `FrameTicker.DoUpdate`
(`CraftingPlanView.cs:499-508`) already wraps the step callback in a
`try/catch (Exception ex) { Logger.Warn(ex, "FrameTicker step failed; stopping"); }`
fallback that self-cancels on any failure - but it means correctness here
depends on a generic catch-and-log fallback rather than a deliberate
teardown, and a module disable timed unluckily could emit a spurious
"FrameTicker step failed" warning that looks like a real bug report to a
maintainer. Recommendation: give `CraftingPlanView` a small
`StopLiveTickers()` method (cancel `_scrollVerifyTicker`,
`_resizeDebounceTicker`, `_wheelWrapVerifyTicker` if non-null - the same
three lines `Build()` already runs at `CraftingPlanView.cs:1308-1313`) and
call it from `Module.Unload()`. Mechanical, additive, does not touch any
of the documented scroll/resize machinery itself.

---

## 4. Nullable reference types

**Not enabled anywhere in the solution** - measured by grep: zero
`#nullable` directives, zero `<Nullable>` project properties, across the
main module, both net48 tool/test projects, and both net8.0 tool/test
projects. This is the single largest "modern C# practices" gap for a
project aiming at "exemplary... ready for public consumption."

This is legitimately **REDESIGN-scale** for the main module - `Nullable`
touches every file that declares a reference-typed member, and there are
1,138 class declarations across the real source tree (measured). It is
**not** a redesign in the risky sense, though: enabling NRT is a
compile-time-only annotation exercise with zero runtime behavior change
(it can only ever add *warnings*, never change what the compiled IL does),
so unlike most items in this report it's one of the safer big changes
available, provided it's done as an opt-in, incremental rollout (per-file
`#nullable enable` pragmas, or `<Nullable>annotations</Nullable>` restricted
to newly-touched files) rather than a flag day that produces thousands of
warnings at once.

Two concrete, much smaller, genuinely-quick wins that don't require
touching the legacy net48 project at all:

- `tools/MysticForgeSeeder/MysticForgeSeeder.csproj` and
  `tools/VendorOfferUpdater/VendorOfferUpdater.csproj` are both already
  modern SDK-style **net8.0** projects (confirmed) with no cost or
  compatibility concern to enabling `<Nullable>enable</Nullable>` - the
  .NET 8 SDK template normally turns this on by default and it was
  explicitly left off here. This is a genuinely free improvement for two
  self-contained console tools with a much smaller surface than the main
  module.
- The existing manual null-defense style already present everywhere (506
  raw `== null` checks measured across the repo, `?.`/`??`/`??=` used
  throughout) means the team is already thinking in these terms by hand;
  NRT would formalize an existing habit rather than impose a new one.

Do not attempt this on the main module in one PR - stage it (e.g. one
`Services/*Math.cs` pure-helper file at a time, since those have no Blish
dependency and the fewest incoming/outgoing null-contract ambiguities).

---

## 5. Magic numbers vs named constants - mostly a strength, not a gap

Spot-checked `Views/CraftingPlanView.cs`, `Views/SettingsTabContent.cs`,
`Services/WheelDeltaSanitizer.cs`, `Services/PlanRelayoutMath.cs`. This
codebase is unusually disciplined here: layout constants are named,
commented, and frequently cross-referenced with provenance
(`RarityFramedIconOuterSize = 34` at `CraftingPlanView.cs:47` explicitly
ties itself to `CreateRarityFramedIcon`'s own defaults so the two can't
drift silently; `BlishScrollWheelStepPixels = 30` at `CraftingPlanView.cs:336`
documents it's hardcoded because the underlying Blish field has no public
accessor, with an explicit "re-verify on any BlishHUD upgrade" note). The
`WheelDeltaSanitizer.WrapThreshold`/`WrapCorrection` constants
(`Services/WheelDeltaSanitizer.cs:90,97`) are backed by a decompiled-source
derivation in the class doc comment - this is exactly the kind of
essential-complexity documentation this analysis was asked to look for, and
it's already there. No findings to raise; this is worth preserving as the
house style when reviewing new code, not something to "clean up."

---

## 6. readonly/immutability discipline

Mixed, but most of what looks like a violation on a naive grep isn't one.
A heuristic scan found roughly 100 `private readonly` fields vs ~109
non-readonly private fields (measured, regex heuristic - not a semantic
analysis). The large majority of the non-readonly set is in `Module.cs`
(`_cornerIcon`, `_mainWindow`, `_modalDialog`, `_craftingPipeline`, etc.,
`Module.cs:48-80`) and is **not** a violation: Blish HUD modules initialize
via the `OnModuleLoaded` lifecycle callback, not the constructor, so these
fields genuinely cannot be `readonly` (C# only allows readonly assignment
in a constructor or field initializer). Flagging this as a "fix" would be
wrong - it's a framework constraint, not sloppiness.

Where it's worth a look: the `Models/*.cs` result/DTO types
(`PlanStep`, `CraftingPlan`, `ItemPrice`, `PlanViewModel`, etc.) are
**uniformly mutable** - every property is `{ get; set; }`, even on types
that represent a one-way pipeline output (a solved plan, a priced item)
that nothing downstream is supposed to mutate. E.g. `Models/PlanStep.cs`
(all 8 properties `{ get; set; }`) flows from `PlanSolver` through
`PlanResultBuilder`/`PlanViewModelBuilder` into the View layer with nothing
stopping a View-layer bug from silently mutating a step after the fact.
This is consistent throughout the codebase (so at least it isn't an
inconsistency), and there's no evidence of an actual bug from it today -
it's a latent-risk observation, not a live defect. Converting these to
constructor-initialized `{ get; }`-only properties is **REDESIGN**, not
CLEANUP (it touches every construction call site across
`Services/PlanSolver.cs`, `Services/PlanResultBuilder.cs`,
`Services/PlanViewModelBuilder.cs`, and the View layer that reads them) -
worth doing eventually for the "exemplary codebase" goal, but should be its
own reviewed PR per type, not folded into a mechanical cleanup pass.

---

## 7. struct vs class choices

Five small structs exist purely as layout-math return values:
`PillSpec` (`Services/DecisionPillPlanner.cs:38`), `TreeColumnEdges` and
`CostTileGeometry` (`Services/PlanRelayoutMath.cs:56,139`), `ColumnEdges`
(`Services/ShoppingColumnMath.cs:22`), `RowInput`
(`Services/ItemRowRequestBuilder.cs:24`). All five use mutable public
fields (e.g. `PillSpec.Text`/`Source`/`Kind`) rather than `readonly struct`
with `readonly` fields or get-only properties. In every call site checked,
these are constructed once via an object initializer and never mutated
afterward, so the mutability is latent risk rather than a live bug (a
mutable struct becomes a real footgun only when stored in a mutable
collection and indexed-and-mutated in place, which doesn't happen here).
Marking them `readonly struct` with `readonly` fields is a mechanical,
behavior-preserving change that documents the actual usage pattern and
closes off that footgun for good. These are all layout/relayout helpers
invoked on window resize/rebuild, not per-frame in the render loop, so any
perf benefit from avoiding defensive struct copies is **INFERRED and
negligible** - this is a clarity/immutability-discipline improvement, not
a performance one.

---

## 8. String comparison / culture

This is in good shape and worth calling out as a strength given it's the
exact area that will matter most if localization is ever revisited: 17
explicit `StringComparison` usages, all `Ordinal` (8) or `OrdinalIgnoreCase`
(9); **zero** uses of `CurrentCulture`/`InvariantCulture` variants
anywhere (measured). 18 uses of `.ToLowerInvariant()` for case-insensitive
comparisons - correct and culture-safe, though marginally less efficient
than an `OrdinalIgnoreCase` comparison directly (allocates a new string to
lower first). This is a genuinely minor, `Nice to Have`-tier, INFERRED
(not measured) efficiency note - not worth a dedicated pass, just something
to prefer going forward when touching call sites that already do this.

---

## 9. Editorconfig / analyzers / StyleCop - absent; proposed ruleset

There is no `.editorconfig` anywhere in the repo, and no analyzer/StyleCop
package referenced by any project (`packages.config` for the main module
has none; neither `.Tests.csproj` nor either net8.0 tool `.csproj`
references one). The only static-analysis artifact in the tree is
`<CodeAnalysisRuleSet>MinimumRecommendedRules.ruleset</CodeAnalysisRuleSet>`
under the `Release|x64` `PropertyGroup` in `GW2CraftingHelper.csproj` - this
is the legacy VS "Code Analysis" (FxCop-era) mechanism, effectively inert
today (nobody runs "Analyze > Run Code Analysis" in a CI pipeline, and no
Roslyn analyzer package is wired to it) and safe to remove or replace.

CLAUDE.md already documents firm, specific conventions (Allman braces,
ASCII-only source, no em-dash, explicit `<Compile Include>`, coin-icon
ordering, ID-non-display). None of these are currently machine-enforced -
they rely entirely on a reviewer (human or Claude) remembering to check.
Recommended, low-risk, **additive-only** step:

1. Add a root `.editorconfig` that codifies what's *already* the house
   style rather than introducing a new one: `csharp_new_line_before_open_brace = all`
   (Allman), `csharp_style_var_for_built_in_types`/`var_when_type_is_apparent`
   matching current usage, `dotnet_sort_system_directives_first = true`
   (already the de facto pattern in every file spot-checked), and naming
   conventions for `_camelCase` private fields (already 100% consistent -
   verified across every file read during this analysis).
2. Add `StyleCop.Analyzers` via `packages.config` (it ships analyzer-only
   DLLs and NuGet's legacy installer wires the `<Analyzer Include=...>`
   items into old-style `.csproj` automatically - no migration to
   SDK-style/PackageReference required) with a **custom ruleset that
   suppresses the rules that fight this codebase's established,
   deliberate style** rather than fighting it:
   - Suppress `SA1600`/`SA1602` (mandatory XML doc on every member) - this
     codebase already documents intent generously via prose `//` comments
     at decision points rather than `<summary>` on every method; forcing
     XML doc everywhere would produce thousands of new warnings for zero
     value and actively encourage empty "boilerplate" doc comments.
   - Suppress `SA1027`/file-header rules that assume a license banner.
   - Keep brace-placement (`SA1500`), one-statement-per-line, and
     `using`-ordering rules **on** - they're already 100% followed and
     will catch drift for free.
   - As covered in 2c above: if any Microsoft.CodeAnalysis.NetAnalyzers
     rules are added later, explicitly disable `CA2007` (ConfigureAwait) -
     it's actively wrong advice for a host with no `SynchronizationContext`.
3. Set the new analyzer set to emit **warnings, not errors**, at least for
   the first pass - there is no CI gate today (no `.github/workflows`
   found), so nothing will silently break; treat it as a to-do surfaced in
   the IDE, not a build gate, until the maintainer has triaged the initial
   warning set once.

---

## 10. Target framework & language-version currency

The solution mixes two toolchains (measured from each `.csproj`):

| Project | TFM | Style |
|---|---|---|
| `GW2CraftingHelper.csproj` (main module) | `net48` | legacy, non-SDK |
| `tests/GW2CraftingHelper.Tests` | `net48` | SDK-style, PackageReference |
| `tools/GW2CraftingHelper.Harness` | `net48` | legacy |
| `tools/GW2CraftingHelper.RecipeSeeder` | `net48` | legacy |
| `tools/MysticForgeSeeder` | **net8.0** | SDK-style |
| `tools/VendorOfferUpdater` | **net8.0** | SDK-style |
| `tests/VendorOfferUpdater.Tests` | **net8.0** | SDK-style |

The `net48` split is required (Blish HUD 1.3.0 and MonoGame.Extended target
`net472`/`net48` - not a choice, a platform constraint, do not change it).
The net8.0 tools are correctly free of that constraint since they're
build-time helper scripts, not part of the shipped module.

**`<LangVersion>` is unset everywhere in the legacy (`net48`, non-SDK)
projects** - `GW2CraftingHelper.csproj`, the two `net48` tool `.csproj`s.
For an SDK-style project, an unset `LangVersion` resolves deterministically
from the TFM via the SDK's own build logic. For these **old-style**
projects (importing `Microsoft.CSharp.targets` directly, not
`Microsoft.NET.Sdk`), there is no such TFM-based capping - an unset
`LangVersion` resolves to whatever "latest non-preview" the installed
`csc.exe` supports, which depends on the Visual Studio / Build Tools
version on whatever machine runs the build. For a project explicitly being
prepared "ready for public consumption as a GitHub community project,"
this is a real (if currently invisible) contributor-onboarding risk: two
contributors on different VS versions building the identical `.csproj`
could resolve to different effective C# language versions, so a PR that
happens to use a newer syntax could compile for its author and fail for a
reviewer on an older toolchain, or vice versa.

Evidence this already matters in practice: the codebase already uses named
tuple returns (`Services/WheelDeltaSanitizer.cs:115`,
`(bool IsWrapped, int IntendedDelta)`, requiring C# 7+ and the
`System.ValueTuple` reference already present in the `.csproj`), `is X x`
pattern matching (`is TimeSpan d403` in the net8.0 tool tree, and the shape
is present in spirit elsewhere), and switch statements throughout - none of
this is exotic, but it confirms the toolchain already resolves to at least
C# 7.x today, informally, per-machine.

**Recommendation**: pin `<LangVersion>` explicitly in the three legacy
`net48` `.csproj`s to whatever the currently-resolved version actually is,
so this becomes a committed fact instead of an environment-dependent one.
This report intentionally does **not** guess a number - the cheap way to
get it right is a single one-time local build with diagnostic verbosity
(e.g. `dotnet build GW2CraftingHelper.csproj -p:Platform=x64 -v:diag | grep -i langversion`,
or just check the `csc` command line MSBuild echoes) and pin exactly that
value; guessing and picking something lower would silently disable
whatever syntax is already relied upon, and picking something higher
without verifying could newly break on a contributor's older toolchain.
This was not run in this analysis pass to stay strictly read-only in a
checkout with concurrent agents active.

---

## 11. csproj hygiene

Beyond the explicit-`<Compile Include>` requirement (already followed -
verified the `ItemGroup` lists every `.cs` file individually, no wildcard
globs, consistent with the repo rule), a few small, mechanical items in
`GW2CraftingHelper.csproj`:

- `<Prefer32Bit>true</Prefer32Bit>` appears in both the `Debug|x64` and
  `Release|x64` `PropertyGroup`s. `Prefer32Bit` only has any effect for
  `AnyCPU` builds; with `PlatformTarget` pinned to `x64` in both
  configurations (which is the only platform CLAUDE.md's build command
  ever uses), this setting is a no-op left over from the original VS
  project template. Safe to remove from both `x64` `PropertyGroup`s.
- `<DocumentationFile></DocumentationFile>` (empty) under `Release|x64` -
  generates no XML doc file either way; vestigial, safe to remove.
- `<CodeAnalysisRuleSet>MinimumRecommendedRules.ruleset</CodeAnalysisRuleSet>`
  under `Release|x64` - see section 9; either remove or repoint at a real
  ruleset once one exists.
- Two `<Reference>`s appear unused (measured - zero source files reference
  either namespace/type anywhere in the real tree): `System.Numerics`
  (0 usages) and `AsyncClipboardService` (0 usages - already marked
  `<Private>False</Private>`, i.e. not even copied to the output
  directory, reinforcing that it's dead weight rather than a soft
  runtime-only dependency). Both look like leftovers from the original
  Blish HUD module template. Recommend removing both, verified with a
  build afterward (not done here to stay read-only) since a Blish HUD
  module template reference occasionally turns out to be required by the
  packaged `.targets`/`.props` import chain even when nothing in source
  calls into it directly - confirm before deleting, don't just trust the
  grep.

None of the above are behavior-affecting; all are pure dead-weight removal.

---

## 12. Localization-hardcoding flag (informational only - not proposing a fix)

Per instructions, localization is deferred and this is flagged, not
recommended for action. Hardcoded, UI-facing string literals are pervasive
and will need extraction if a future localization milestone happens:

- Inline `PlanStatus { Message = "..." }` progress strings, duplicated
  across `CraftingPlanPipeline.cs`'s near-parallel `GenerateAsync`/
  `GenerateStructuredAsync`/`GenerateStructuredMultiAsync` methods (e.g.
  `"Collecting item IDs..."`, `"Resolving vendor offers..."`,
  `"Solving crafting plan..."` each appear 2-3 times verbatim - 24+ literal
  occurrences measured across `Services/*.cs` and `Views/*.cs` combined).
- Inline `Text = "..."` on Blish `Label`/`StandardButton` controls
  throughout `Views/*.cs` (24 occurrences measured in `Views/*.cs` alone),
  e.g. `ModalDialog.cs`'s `"Regenerate"`/`"Cancel"`/`"Confirm"`.
- Decision-pill text built inline in `Services/DecisionPillPlanner.cs`
  (`"IGNORE"`/`"IGNORED"`, etc., per the `PillKind` doc comments).

No action proposed here per the task's instructions - flagging only so a
future localization-scoping pass has a starting inventory instead of
starting from zero.

---

## Explicitly confirmed sound - do not restructure

To avoid a synthesis pass mis-reading grep noise as findings, these were
specifically checked against `docs/KNOWN-ISSUES.md` and read in full or in
large part, and are correct, already well-documented, and out of scope for
any "simplify this" recommendation:

- `Services/WheelDeltaSanitizer.cs` - the entire class doc comment is a
  decompiled-evidence root-cause writeup (Blish HUD's own
  `MouseEventArgs.WheelDelta` getter bug) with a derived, justified
  threshold constant. This is the canonical example of essential
  complexity done right; it should be the template other gnarly fixes are
  measured against, not a target for reduction.
- `Views/MainThreadMarshal.cs` - correctly documents why
  `GameService.Overlay.QueueMainThreadUpdate` cannot be used for
  multi-frame work (empirically measured "400 same-frame re-queues" per
  the comment), and why `FrameTicker` exists as the alternative.
  `Views/CraftingPlanView.cs`'s `FrameTicker` class itself
  (`CraftingPlanView.cs:457-538`) is correctly self-cancelling and
  defensively wraps its step callback - see section 3b for the one narrow
  gap (module-unload teardown), which does not require touching this
  mechanism itself.
- `[scrolldiag]` gating (`CraftingPlanView.cs:338-372` and its ~12 call
  sites) - correctly zero-cost-when-disabled, correctly never fed back
  into any actual scroll/restore decision (diagnostics-only, as the
  comment insists). The only change proposed anywhere in this report is
  deduplicating the repeated enabled-check expression (section 1e) and
  extracting the *pattern* for reuse elsewhere - not touching this
  instance of it.
- `Services/CraftingPlanPipeline.cs`'s triplicated currency-await/catch
  (section 1c) is a real duplication finding, but the *swallow* itself
  (falling back to `null` -> text-only currency display) is a deliberate,
  documented contract, not a bug - only the code duplication is flagged,
  not the behavior.

---

## What was not covered (out of lens / left to other passes per the task)

- Architecture/God-object concerns about `Views/CraftingPlanView.cs`
  (4,802 lines) and `Services/PlanSolver.cs`/`CraftingPlanPipeline.cs`
  (1,582/1,463 lines) - file size and single-responsibility questions are
  a structural/architecture lens, not a C#-practices one; only the
  specific IDisposable/duplication angles on those files are covered
  above.
- Anything under `docs/research/m37-*.md` or the in-flight
  `m37-homestead` worktree / audit-fix branches - per instructions, no
  recommendation here is anchored to exact line numbers in areas those
  branches are actively touching (PlanSolver settings/vendor-seed logic,
  marshal/error-path audit fixes); the findings above that touch
  `PlanSolver`-adjacent files are scoped to logging/duplication only, not
  the solver logic itself.
