# M38 Post-Parity Cleanup — Analysis A3: Memory/CPU Efficiency

Scope: current `master` (post-M37, `812d0f0` per KNOWN-ISSUES handoff notes; this
session's `git log` head is `85a738e`, a docs-only merge on top of that — no
runtime code changed since). Read-only analysis. In-flight branches
(`m37-homestead` worktree touching PlanSolver/settings/vendor seed; audit-fix
agents touching marshal/error paths) were NOT built on for line numbers —
findings below cite member/file names and are described so they survive a
rebase.

Every claim is tagged **MEASURED** (I ran something and observed a number) or
**INFERRED** (from reading the code/algorithm). Where I could not measure
cheaply I say so explicitly and specify the measurement to run.

## How the MEASURED numbers were produced

- Built `tools/GW2CraftingHelper.Harness` (`dotnet build ... -p:Platform=x64`,
  green) and ran it directly against the real `ref/*.json` seed files (offline
  mode, no network) — this is the same offline pipeline entry point
  (`CraftingPlanPipeline.GenerateStructuredAsync`) the live module drives from
  `CraftingPlanView`, minus the Blish UI layer.
- `--profile 2` = Exordium (item 90551), the same tree KNOWN-ISSUES already
  uses as its "large real plan" reference point.
- Wall-clock/CPU phase timings come from the harness's own built-in
  `Services/Diagnostics/PlanTimingAnalyzer`, which parses the pipeline's real
  per-phase `Stopwatch` debug log — no code was modified to get these numbers.
- Peak process memory (`PeakWorkingSet64`) was sampled from the *outside* via
  a throwaway PowerShell script (`Start-Process` + polling `Get-Process`),
  since I could not add instrumentation to the module itself. This measures a
  Debug, net48, x64, offline console process — a reasonable proxy for "how
  much heap does loading the seed data + solving one plan cost," **not** a
  measurement of the live Blish HUD process (which shares a CLR/GC with the
  whole overlay and every other loaded module).
- I did not touch any tracked file; the only artifacts created are normal
  build output (`bin/`, `obj/`) and my own scratch files under
  `/tmp/.../scratchpad/`.

Raw numbers (all MEASURED, single dev machine, WSL2 invoking the Windows
`dotnet.exe`/exe directly, Debug build):

| Run | Metric | Value |
|---|---|---|
| `--profile 2 --iterations 6` | Cold total (1st solve) | 116-129ms |
| `--profile 2 --iterations 6` | Cold: Build recipe tree | 43ms (37.1%) |
| `--profile 2 --iterations 6` | Cold: Solve | 40ms (34.5%) |
| `--profile 2 --iterations 6` | Cold: Build result | 29ms (25.0%) |
| `--profile 2 --iterations 6` | Warm median total | 35ms |
| `--profile 2 --iterations 6` | Warm: **Build result** | **18ms (51.4%)** |
| `--profile 2 --iterations 6` | Warm: Solve | 10ms (28.6%) |
| `--profile 2 --iterations 6` | Warm: Build recipe tree | 7ms (20.0%) |
| `--profile 2 --iterations 1` | Peak working set | 187.8 MB |
| `--profile 2 --iterations 5` | Peak working set | 190.5 MB |
| `--profile 2 --iterations 30` | Peak working set | 218.1 MB |
| `--profile 2 --iterations 150` | Peak working set | 219.4 MB |
| `--dump-tree` (profile 2) | Raw+solved tree node lines | ~13,140 (`id=` lines) |
| `--dump-tree` (profile 2) | Solved-tree `decision=Craft` occurrences | 802 |
| `--print-cache-stats` | Recipe cache hits (seed) | Search 168, Recipe 146, 0 misses |

Interpretation of the memory series: **iterations=1 vs 5 costs ~3MB, 5→30
costs ~28MB, 30→150 costs ~1MB (plateau)**. That says the ~188MB floor is
essentially all one-time seed-load + CLR/runtime cost, there is a modest
one-time warm-up growth over the first several solves (JIT, thread pool,
GC generation promotion), and then it is flat — **no observed per-iteration
leak** in repeated single-item re-solves of the same tree. Caveat: this only
exercises repeated identical solves of one item; it does not exercise the
override/re-solve path (`ResolveWithOverrides`) or a multi-item batch, which
are architecturally different call paths (see Finding 1).

---

## Priority 1 — `PlanResultBuilder.FindRecipeOption` re-walks the whole tree per unique recipe, on the synchronous UI-thread pill-click path

**File**: `Services/PlanResultBuilder.cs`, method `FindRecipeOption` (private,
~line 296), called from `Build` twice — once per unique `RecipeId` while
building `stepOptions` for Required Disciplines (dedup via `seenOptionIds`),
and again per unique `RecipeId` while building `requiredRecipes` (dedup via
`seenRecipeIds`).

**What it does**: unbounded DFS from the tree root (`node.Recipes`, then
recurse into `option.Ingredients`) to find the one `RecipeOption` matching a
given `recipeId`, with no memoization and no early result cache shared
between the two call sites. Called once per *unique* craft-step recipe id —
confirmed via `!seenOptionIds.Add(...)`/`!seenRecipeIds.Add(...)` guards — but
each call independently re-scans from the root.

**Why it matters**: `PlanResultBuilder.Build` is called from:
1. `CraftingPlanPipeline.GenerateStructuredAsync` (both single- and
   multi-item overloads) — off the UI thread (result of an `await`), so a
   slow `Build` here does not block input, only delays the "Plan generated"
   status.
2. `CraftingPlanPipeline.ResolveWithOverrides` — called **synchronously,
   directly from `CraftingPlanView.ApplyOverridesAndResolve`**, which is
   itself called directly from a pill's `Click` handler
   (`Views/CraftingPlanView.cs`), a Best Path/Craft All/Buy All button click,
   or an Ignore-pill toggle. There is no `await`, no `Task.Run`, no
   `MainThreadMarshal` hop anywhere in that call chain — `ResolveWithOverrides
   -> PlanSolver.Solve -> PlanResultBuilder.Build -> FindRecipeOption` all run
   on the main/render thread, in the same frame as the click, immediately
   followed by `RenderPlan`'s full dispose+rebuild (Priority 3).

**MEASURED**: for the Exordium tree (~13,140 combined raw+solved tree node
lines in the `--dump-tree` output, i.e. several thousand nodes; 146 distinct
recipes fetched per `--print-cache-stats`), "Build result" is the single
largest phase on a *warm* run — 18ms of a 35ms median total (51.4%),
overtaking both "Solve" (10ms) and "Build recipe tree" (7ms). This is
consistent with an O(uniqueRecipes × treeSize) algorithm dominating once the
recipe/vendor caches are warm and the cheaper phases (network-shaped, already
memoized) shrink to near zero.

**INFERRED**: the two dedup loops each independently re-walk from the root,
so the real cost is closer to 2× (uniqueRecipes × treeSize) tree-node
visits — for Exordium that's on the order of 10^5-10^6 node visits per
`Build()` call, which is what actually shows up as the 18ms. This scales with
both the number of distinct recipes crafted (grows with plan complexity) and
tree size (grows with multi-item batches — KNOWN-ISSUES already logs a
~9,400px two-item batch as a real user scenario), so a larger/multi-item plan
than Exordium would make this materially worse, not better.

**User-facing consequence (INFERRED)**: every pill click, Best Path click,
Craft All/Buy All, or Ignore toggle pays this cost synchronously before the
screen updates. At Exordium's scale that's ~18ms just for `Build result`,
plus ~10ms for `Solve`, plus whatever `PlanViewModelBuilder.Build` and
`RenderPlan`'s dispose+rebuild add (unmeasured — see Priority 3) — all inside
one input-handler call, all before the next frame can present. 18ms alone
already exceeds a single 60Hz frame budget (16.7ms); the combined chain is
the more meaningful number and is currently unmeasured end-to-end (Blish
dependency — see "Recommended measurements" below).

**Classification**: **CLEANUP** (mechanical, behavior-preserving). Build one
`Dictionary<int, RecipeOption>` (recipeId → option) with a single tree walk
at the top of `Build`, and have both dedup loops do an O(1) lookup into it
instead of calling `FindRecipeOption` per recipe id. This does not change any
observable output — same options are found, same disciplines/recipes are
derived — it only removes the redundant re-walks. No essential complexity is
at risk here: `FindRecipeOption`'s job (map a `RecipeId` back to the
`RecipeOption` that produced a craft step, since `RecipeNode`/`RecipeOption`
don't carry a reverse index) is legitimate; only the "re-scan from the root
every time, twice" execution strategy is accidental.

**Severity**: Must Fix (real algorithmic complexity issue on a hot,
UI-thread-blocking, click-driven path; not a crash, but a directly measured
and explained input-latency contributor that gets worse as plans grow).

---

## Priority 2 — Seed loading: `ReadToEnd()` + `Deserialize<string>` doubles transient memory, repeated in 4 places, and runs synchronously in `Module.Initialize`

**Files** (identical pattern in all four):
- `Services/VendorOfferLoader.Load` (13.3MB `ref/vendor_offers.json`)
- `Services/Recipes/RecipeCacheSerializer.LoadRecipeSeed` (8.15MB
  `ref/recipes_seed.json`)
- `Services/Recipes/RecipeCacheSerializer.LoadSearchSeed` (551KB
  `ref/recipe_search_seed.json`)
- `Services/Recipes/ItemNameSeedData.Load` (2.1MB `ref/item_name_seed.json`)

**MEASURED** (file sizes, `ls -la ref/*.json` on this checkout):
`vendor_offers.json` 13,306,985 bytes, `recipes_seed.json` 8,149,147 bytes,
`item_name_seed.json` 2,100,068 bytes, `mystic_forge_recipes.json` 868,224
bytes (loaded by a different, streaming-friendly path —
`FileMysticForgeRecipeSource`/`MysticForgeRecipeData`, not this pattern),
`recipe_search_seed.json` 551,516 bytes. Combined ≈ 24.9MB of JSON text
loaded at module start. (`ref/wiki_vendor_cache.json`, 18.1MB, is confirmed
**dev-tool-only** — grepped every `.cs` reference; the only consumer is
`tools/VendorOfferUpdater/Program.cs`. It is not part of the runtime load
path — good, matches the "research-only" invariant in spirit even though
that invariant is worded about gw2efficiency specifically.)

**INFERRED** (standard, well-known .NET behavior): each loader does
`new StreamReader(stream).ReadToEnd()` to build a full in-memory UTF-16
`string`, THEN calls `JsonSerializer.Deserialize<T>(string, options)`. For a
13MB mostly-ASCII UTF-8 file, the UTF-16 string is ~2× the byte count (~26MB
of `char[]`) — an intermediate allocation that exists purely because of the
loader's own code shape, not because `System.Text.Json` requires it. Every
loader here has a same-package (`System.Text.Json` 5.0.0) `Deserialize<T>`
overload that takes the `Stream` directly, which parses UTF-8 bytes without
ever materializing a UTF-16 copy of the whole document. Swapping to that
overload removes the doubling for all four files (~25MB less peak transient
allocation) with **zero behavior change** — same options, same output type,
same exceptions on malformed JSON.

**MEASURED** (peak working set numbers above): ~188MB peak RSS for a
Debug/net48/x64 offline process whose only substantial job before the first
solve is loading these five files plus constructing the in-memory
dictionaries (`SeededRecipeCacheStore._recipes`: 14,736 `RawRecipe`;
`VendorOfferStore._mergedById`/`_mergedByOutput`: 53,530 `VendorOffer`,
indexed twice — once by string `OfferId`, once by `int OutputItemId` with a
per-list `Sort()` on every rebuild). ~188MB for ~25MB of source JSON is a
roughly 7-8× expansion, which is in the normal range for "JSON text → object
graph with per-object/per-list/per-Dictionary-entry overhead" in .NET, not
evidence of a specific bug beyond the string-doubling above — but it is the
number a future "why does this module use 200MB" report would start from.

**INFERRED, not verified by decompile (would need real effort to confirm
precisely)**: `Module.Initialize()` is a synchronous `void` method in the
Blish HUD module contract, distinct from `protected override async Task
LoadAsync()`. The framework's own API shape (sync `Initialize`, async
`LoadAsync`) is a strong signal that `Initialize` is meant to be fast/UI-safe
and `LoadAsync` is where slower work belongs — and this module already
follows that convention itself elsewhere (`Task.Run(async () => ... await
FetchGw2BuildIdAsync() ...)` inside `Initialize`, `await
RefreshSnapshotInBackgroundAsync()` inside `LoadAsync`). Today, all five seed
files are parsed synchronously inside `Initialize` instead. If `Initialize`
runs on Blish's main/UI thread during module enable (the framework
convention strongly implies this but I have not decompiled Blish's own
module loader to confirm it against *this* version), then every module
(re)enable pays the full ~25MB parse cost as one synchronous UI-thread
stall.

**Recommended measurement** (cheap, should run before deciding this is worth
fixing): wrap the existing seed-load block in `Module.Initialize` with a
`Stopwatch` (a 2-line, temporary, easily-reverted change) and log the elapsed
ms plus which Blish thread it ran on (`Thread.CurrentThread.ManagedThreadId`
compared against the id `Update()`/`DoUpdate` run on) on a real desktop
session. This turns "INFERRED: probably blocks UI at module enable" into a
real number with no architecture change required to find out.

**Classification**:
- The `ReadToEnd()`→string→`Deserialize<string>` → `Deserialize<Stream>`
  swap is **CLEANUP** — mechanical, four call sites, no output change,
  low risk.
- Moving the seed load off `Initialize` and into the existing async
  `LoadAsync`/`Task.Run` pattern is **REDESIGN-adjacent**: low architectural
  risk (the module already has the exact background-then-marshal pattern
  established for the build-id fetch and the snapshot refresh), but it does
  need a real decision about what `CraftingPlanView`/`Generate` should do if
  a user clicks Generate before the seed load finishes (today that can't
  happen, since it's synchronous-before-anything-else runs). Flag for the
  execution phase to design a small "not ready yet" guard, not to be done as
  a blind mechanical change.

**Severity**: Must Fix for the ReadToEnd doubling (safe, mechanical, real
memory win); Nice to Have / needs-a-decision for the Initialize→LoadAsync
move pending the measurement above.

---

## Priority 3 — `RenderPlan`'s full dispose+rebuild runs on every pill click, not just fresh Generate (documented for resize, not for this path)

**File**: `Views/CraftingPlanView.cs`, `RenderPlan` (~line 2055).

**What happens**: `RenderPlan` unconditionally does
`foreach (var child in _contentPanel.Children.ToArray()) { child.Dispose(); }`
then rebuilds every section from scratch. This is called from:
- `TriggerGenerate`'s completion callback (fresh plan — expected, one-time).
- `ApplyOverridesAndResolve` (**every** pill click / Best Path / Craft All /
  Buy All / Ignore toggle), via `PreserveScrollAcross(() => RenderPlan(vm))`.
- Expand All / Collapse All buttons, via the same wrapper.

**Context that matters (essential complexity, correctly scoped already)**:
M33 C2b explicitly solved this exact class of problem for the **resize drag**
path — that one now does live in-place relayout via `_relayoutActions`
instead of a rebuild, specifically because resize is a *continuous,
unbounded-frequency* interaction (435 ticks in one logged drag). Pill clicks,
Best Path, and Ignore toggles are *discrete, user-paced* interactions — the
M33 team's own scoping (resize only) reads as a deliberate, reasonable
tradeoff, not an oversight. I am **not** recommending the pill-click path
be rewritten to use the relayout registry — that would be a nontrivial
REDESIGN (the relayout registry is purely position/width, not built/child-set
aware) for a path that has not been shown to be a problem.

**INFERRED control-count estimate**: KNOWN-ISSUES' own M36/M37 records give a
concrete real-world content size — a two-item batch (Exordium + Gift of
Fortune) rendering ~9,400px of content. At the row heights this file uses
(32/36/44px per KNOWN-ISSUES #23/#36b), that is roughly 210-290 visible rows
at once (not the full ~6,500-node raw tree — collapsed/unexpanded tree depth
is not materialized, per the existing depth<2 default and lazy child
construction noted in `RenderPlan`'s own comments). Each row appears to
create several `Control`s (a row `Panel`, a framed icon — itself 2-3 nested
controls per `CreateRarityFramedIcon`, 1-3 `Label`s, 0-5 pills per
KNOWN-ISSUES #20.4's "up to five pills," a divider `Panel`). That puts a
full rebuild in the neighborhood of 1,000-2,000 `Control` object
alloc+dispose pairs per click on a plan that size — plausible, not measured
(no Blish host available in this read-only, headless environment to
instrument it directly).

**Already self-flagged in code, worth elevating**: `ReplayRelayout`'s own doc
comment (the resize-drag-tick path, not this one) contains an explicit "PERF
CAVEAT" acknowledging its own SuspendLayout/ResumeLayout mitigation "is
reasoned, not measured: no live drag-resize check on a large, fully-expanded
plan (deep tree + long shopping list) has been performed against a running
Blish instance." That is exactly the kind of gap this audit is asked to
surface — it is already correctly identified by the team, just not yet
closed. I'm restating it here because a cleanup pass that touches
`CraftingPlanView.cs` broadly (very likely, given its size — see "Other
observations") risks silently regressing something that has never actually
been measured, and the file itself says so.

**Recommended measurement** (per the existing project screenshot-loop
protocol already in project memory, no new tooling needed): a live
drag-resize capture on the same ~9,400px two-item batch already used for the
435-tick capture, with a simple counter/log of `_relayoutActions.Count` and
per-tick wall time (a temporary `Stopwatch` around `ReplayRelayout`'s body).
Separately, a live pill-click capture on the same plan with a temporary
`Stopwatch` around `ApplyOverridesAndResolve`'s body would turn this
section's INFERRED control-count estimate into a real end-to-end
click-to-paint number, and would also capture `PlanViewModelBuilder.Build`'s
cost, which Priority 1's harness numbers do not include (it runs inside
`CraftingPlanView`, not `CraftingPlanPipeline`).

**Classification**: **MEASURE FIRST.** Do not restructure this path in the
same pass as Priority 1 — if the future measurement shows it's fine (likely,
given it is a discrete, human-paced interaction, not a per-frame one), no
change is needed here at all.

**Severity**: Nice to Have (verification debt), not a fix — matches the
"treat a fresh report as expected-until-checked" posture this project's own
KNOWN-ISSUES already uses for unmeasured UI paths.

---

## Priority 4 — Unbounded, TTL-less in-memory caches (inconsistent with the project's own TTL precedent)

**File**: `Services/ItemMetadataService.cs` — `_cache`
(`Dictionary<int, ItemMetadata>`) and `_knownMissing` (`HashSet<int>`).

**INFERRED** (code reading — the growth pattern is deterministic from the
code, not something I needed to run): both fields are instance fields on a
single `ItemMetadataService` constructed once in `Module.Initialize` and
reused for the module's entire lifetime. Every distinct item id ever
requested across every plan generated in a play session accumulates in
`_cache` permanently — no TTL, no LRU, no size cap, no clear. `_knownMissing`
is even described in its own comment as caching "for the service's lifetime
(module session)."

**Contrast with the project's own established pattern**: `TradingPostService`
(`Services/TradingPostService.cs`) caches the same shape of data (per-item
lookups) but with an explicit `CacheTtl = TimeSpan.FromMinutes(15)` — a
deliberate, documented policy. `ItemMetadataService` has no equivalent
policy at all; item metadata (name/icon/rarity) does change far less often
than prices, so an unbounded cache is a much more defensible choice than it
would be for prices — but the asymmetry (one service has a TTL, the sibling
service with the same shape has none, with no comment explaining why the
difference is intentional) reads as drift rather than a considered decision.

**Severity, calibrated honestly**: practically low. Growth is bounded by
"how many distinct item ids has this player looked up in this session,"
which is naturally rate-limited by how fast a human can type/click; each
`ItemMetadata` entry is a handful of small strings + an int, so even a
session touching 10,000 distinct items is a few MB, not a leak that will
OOM a client. This is a **Nice to Have / consistency** finding, not a
Must Fix — flagging it because "ready for public consumption, exemplary"
was named as a goal, and an unexplained inconsistency between two
near-identical caching services is exactly the kind of thing a new
contributor reading this codebase would trip over.

**Also checked and found correctly bounded (no finding needed)**:
- `CraftingPlanView._nodeExpansion`/`_treeNodeStates`/`_relayoutActions`/
  `_reellipsisActions`: all explicitly `.Clear()`'d at the start of every
  fresh Generate (`_nodeExpansion.Clear()` in `TriggerGenerate`'s completion
  callback) or every `RenderPlan` call — correctly scoped to "current plan's
  lifetime," not session-unbounded. This was one of the audit brief's named
  suspects and it checks out clean.
- `InventoryReducer.Reduce`'s `CloneNode(tree)` (full tree clone) is
  correctly gated behind `if (snapshot != null && _reducer != null)` in
  `CraftingPlanPipeline` — when "Use Own Materials" is off (the common case,
  `Module.cs` passes `snapshot: null`), the clone/reduce pass is skipped
  entirely rather than running over an empty pool. No finding here.
- `CurrencyMetadataService._cache`: also unbounded, but the service's own
  doc comment states the full currency list is small and "effectively
  static for a session" — a stated rationale, unlike `ItemMetadataService`.
  No finding.

**Classification**: CLEANUP if addressed (add a size cap or session-scoped
TTL mirroring `TradingPostService`'s existing pattern) — small, isolated,
does not touch any hot path.

---

## Priority 5 — String/LINQ churn inside `PlanResultBuilder`'s dedup loops (secondary to Priority 1, same method)

**File**: `Services/PlanResultBuilder.cs`, inside the per-craft-step loop
building `stepOptions` (~line 109): `option.Disciplines.Where(d =>
!NonCraftingDisciplines.Contains(d)).ToList()` allocates a new `List<string>`
per craft step, every `Build()` call (fresh generate AND every pill-click
re-solve). Similarly the debug-log construction at the top of `Build`
(`usedMaterials.Select(u => $"...").ToList()` + `string.Join`) allocates a
list of formatted strings purely to build one debug line, unconditionally,
even when nothing will ever read `debugLog` (e.g. a background-generate path
whose log isn't surfaced anywhere the user can see, or the Log tab is never
opened this session).

**INFERRED**: individually cheap (few-hundred-item lists, not the thousands
`FindRecipeOption` touches), so this is not going to show up as its own
Timing Summary phase the way Priority 1 does — it is bundled inside the same
measured 18ms warm "Build result" figure, as incidental allocation pressure
on top of the real algorithmic cost. Worth fixing in the same pass as
Priority 1 (same method, same review), not worth a separate investigation.

**Classification**: CLEANUP, Nice to Have on its own, but "free" to fix
alongside Priority 1 since you're already rewriting this method.

---

## Priority 6 — Things I looked at because they *sound* like they'd be perf problems, and are not (essential complexity, correctly handled — do not touch)

Listed explicitly because the essential-complexity warning cuts both ways:
finding nothing wrong in a suspicious-looking area is itself a useful
result for the synthesis pass, so it doesn't get "rediscovered" as a target.

- **`FrameTicker`/scroll-restore/verify machinery**
  (`Views/CraftingPlanView.cs`): per-frame `DoUpdate` on a 1×1 invisible
  control, self-canceling, with an explicit duplicate-frame guard. This is
  the documented fix for a real, decompile-confirmed Blish HUD library bug
  (`QueueMainThreadUpdate` same-frame re-drain) and is bounded to a handful
  of frames per event (2-3 for scroll verify, at most 2 for wheel-wrap
  defensive re-assert) — not a persistent per-frame cost once idle. No
  finding.
- **`ReplayRelayout`'s per-drag-tick closure replay** (435 ticks observed
  live, per KNOWN-ISSUES #13/#19): the closures themselves do not allocate
  (`foreach` over an already-built `List<Action<int>>`, struct enumerator,
  each closure just writes `.Size`/`.Location` on an existing control) — the
  *count* of closures scales with visible row count, which is the
  already-self-flagged PERF CAVEAT covered under Priority 3, not a new
  allocation finding.
- **`WheelDeltaSanitizer`**: documented and verified as "unconditional and
  zero-allocation" in its own class doc comment (KNOWN-ISSUES #12); read the
  implementation, this claim holds — pure integer arithmetic, no
  allocations, no LINQ.
- **`CraftableItemSearchProvider`/autocomplete search**: named in the audit
  brief as a "string/LINQ churn in loops" suspect. MEASURED the actual seed
  size: `ref/item_name_seed.json` holds 14,587 entries (`grep -c '"id"'`),
  not tens of thousands. The search does a linear scan with a precomputed
  `NameLower` per entry (computed once at load, not per keystroke) and an
  early-break heuristic once enough prefix matches are found. 14.5K simple
  string comparisons per keystroke is not a meaningful cost on any modern
  machine, and it is properly `CancellationTokenSource`-guarded against
  overlapping searches in `SuggestionPanel.OnTextChanged`. No finding.
- **`RecipeService.BuildNodeAsync`/`BuildMultiItemTreeAsync`**: already uses
  `BoundedConcurrency.ForEachAsync` for parallel recipe fetches rather than
  naive sequential awaits or unbounded `Task.WhenAll`. No finding.
- **`PlanSolver.Evaluate`**: memoized via a `Dictionary<int, Decision>
  memo` keyed by `NodeId`, called once per node during a solve (confirmed by
  reading the recursion shape) — O(N), not O(N²). The 10ms warm "Solve"
  figure for a several-thousand-node tree is unremarkable for the amount of
  correctness logic this method carries (vendor batching, merged-ceil,
  ignore/achievement-bit interactions — all named ESSENTIAL in
  KNOWN-ISSUES). I did not find an allocation or complexity problem here
  worth reporting; if the synthesis wants deeper PlanSolver profiling, it
  needs a dedicated pass with more budget than this analysis had, not a
  guess from a first read of an 80KB file.
- **Icon/texture loading** (`AsyncTexture2D.FromAssetId`,
  `GameService.Content.GetRenderServiceTexture`): both are Blish HUD
  framework APIs with their own internal caching (by asset id / URL) that
  this module has no control over and, per the framework's documented
  contract, should make repeated calls on every `RenderPlan` rebuild cheap.
  I could not verify this via decompile within this analysis's budget —
  flagged here as INFERRED-and-unverified rather than either a finding or a
  clean bill of health, in case a future pass wants to confirm it.

---

## Recommended measurements for the execution phase (in priority order)

1. **Stopwatch around `ApplyOverridesAndResolve`'s body**, live desktop, on
   the largest available real plan (the existing ~9,400px two-item batch is
   a good baseline) — turns Priority 1 and Priority 3's combined
   click-to-render cost from "measured backend phases + inferred control
   count" into one real number that includes `PlanViewModelBuilder.Build`
   and `RenderPlan`, which the offline harness cannot see at all (no Blish
   host).
2. **Stopwatch (or existing `[scrolldiag]`-style gated logging) around
   `ReplayRelayout`'s body**, live desktop, drag-resize on the same large
   plan — closes the PERF CAVEAT `ReplayRelayout`'s own doc comment already
   asks for, before any cleanup pass edits the file it lives in.
3. **Stopwatch around the seed-load block in `Module.Initialize`**, live
   desktop, plus which thread it ran on — turns Priority 2's "INFERRED,
   probably blocks module enable" into a real number and a real thread id,
   which should decide whether the `LoadAsync` migration is worth doing at
   all.
4. Re-run this session's harness commands
   (`GW2CraftingHelper.Harness.exe --profile 2 --iterations 6
   --print-cache-stats`, offline mode, no code changes needed) **after**
   Priority 1's fix lands, as a regression check — "Build result" should
   drop from the dominant warm-run phase to near-zero, with "Solve"
   becoming the largest phase instead. This is a cheap, already-available
   before/after check the execution phase should not skip.

## Explicit CLEANUP vs REDESIGN summary

| # | Finding | Classification |
|---|---|---|
| 1 | `PlanResultBuilder.FindRecipeOption` O(N×M) re-walk | **CLEANUP** — memoize with one dict, no behavior change |
| 2a | `ReadToEnd()`+string `Deserialize` in 4 loaders | **CLEANUP** — swap to `Deserialize(Stream)` overload |
| 2b | Seed load moved out of `Initialize` into async | **REDESIGN-adjacent** — needs a "not ready yet" UX decision; measure first |
| 3 | `RenderPlan` full rebuild on pill click | **NO CHANGE RECOMMENDED** — measure first; likely fine given discrete/human-paced interaction |
| 4 | `ItemMetadataService` unbounded cache | **CLEANUP** — small, isolated, mirror `TradingPostService`'s TTL pattern |
| 5 | Minor LINQ churn in `PlanResultBuilder` | **CLEANUP** — fix alongside #1, same method |

## Other observations (outside strict memory/CPU lens, noted in passing)

- `Views/CraftingPlanView.cs` is 4,802 lines / 239KB — by far the largest
  file in the codebase (next largest, `Services/PlanSolver.cs`, is 1,582
  lines). This is a maintainability/structure concern for a different lens
  of this audit, not a memory/CPU one — a large file does not, by itself,
  cost anything at runtime. Not re-litigated here.
- `Services/VendorOfferResolver.cs`/`IWikiVendorClient` (live-wiki
  vendor-offer resolution code) is fully wired into
  `CraftingPlanPipeline` but is **dead at runtime**: `Module.cs`
  constructs the pipeline with `resolver: null` explicitly, and every
  `_resolver != null` guard in the pipeline is therefore always false in
  production. Zero runtime cost today (confirmed by the null check), so no
  memory/CPU finding — but it's retained, tested, unreachable production
  code, which is squarely in scope for whatever lens of this audit covers
  dead code/maintainability.
