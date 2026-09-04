# D2 - Module Logging System & Log Tab Redesign

Status: PROPOSAL (design only, no code changes). 2026-07-22.
Scope: `Views/LogTabContent.cs` (74 lines today), a NEW `Services/ModuleLog.cs` (+ `ModuleLogStore.cs`),
`ModuleSettings.cs` additions, `SettingsTabContent.cs` additions, and migration notes for existing
`Logger.*` call sites. No changes proposed to `CraftingPlanView.cs`'s scroll/relayout machinery itself
(WP-21/23/24/25/26 territory) beyond how its `[scrolldiag]` channel is *routed*.

Evidence labels: **MEASURED** = read directly from the files cited inline (paths/line numbers).
**INFERRED** = a reasonable conclusion not directly proven by a read. **OPEN** = an unresolved
question.

---

## 1. Scope

Four requirements, all addressed below: (1) a real module-wide log system with levels, (2) the Log tab
becomes a search/view pane, (3) a disk-usage policy (do not fill a user's disk with noise), (4) log
rotation plus retention settings (maximum log size on disk, days of retention).

---

## 2. Problem / intent

**MEASURED** - today there are two disjoint, incomplete logging paths, neither of which meets those
requirements:

1. **Blish's own `Logger`** (`Blish_HUD.Logger.GetLogger<T>()`) is called 48 times across 6 files
   (`Module.cs` 11x, `Services/Gw2AccountSnapshotService.cs` 8x, `Views/MainThreadMarshal.cs` 2x,
   `Views/MainView.cs` 1x, `Views/SettingsTabContent.cs` 1x, `Views/CraftingPlanView.cs` ~25x including
   the 12 `[scrolldiag]`-tagged lines). All of it writes to Blish HUD's *own* log file
   (`...\Documents\Guild Wars 2\addons\blishhud\logs`, MEASURED; Blish retains ~6 files, rotation
   not controllable from inside a module) - invisible from any in-module UI, and not something this module should try to duplicate or
   supersede. The module has zero control over Blish's rotation/retention; building a second copy of
   that mechanism would be redundant scope, not the requirement.
2. **The Log tab** (`Views/LogTabContent.cs`, 74 lines, MEASURED) shows *only*
   `CraftingPlanView.LastDebugLog` (wired in `Module.cs:388` as
   `() => _craftingContent.LastDebugLog`) - a `List<string>` of timing lines
   (`Services/Diagnostics/PlanTimingAnalyzer.cs`'s regex expects `"<phase>: <n>ms (<n> items)"`) plus a
   few free-text notes, reset to `null` at the top of every `Generate*Async` call
   (`Services/CraftingPlanPipeline.cs:530/875/959`) and overwritten wholesale in `CraftingPlanView.cs`
   at lines 1923/1984/2015/3696. One plan's worth of lines, no levels, no timestamps, no persistence,
   no search, no filter.

Neither path gives the user (or anyone debugging a bug report) a way to answer "what did
the module actually do in the last session" - snapshot refresh failures, cache-refresh/seed-load
anomalies, API degradations (`Gw2AccountSnapshotService`'s 8 `Logger.Warn` sites, all currently
Blish-log-only and invisible in-module) never reach any UI at all today.

**What a module-level log should add, concretely** (this is the actual scope decision):
structured **module lifecycle/domain events** - not a mirror of Blish's own framework-level log, and not
a raw firehose of every `Logger.Debug` call already in the codebase. Concretely, worth a user's disk:

- Crafting-plan generation lifecycle (start/finish/failure/cancel, item count, elapsed ms - the same
  facts `PlanTimingAnalyzer` already parses, but leveled and durable across sessions).
- Snapshot refresh lifecycle (start/success/failure/backoff-skip - mirrors `Module.cs`'s existing
  `Logger.Info`/`Logger.Warn` calls at lines 455/508/557/561/581/607/611).
- API failures/degradations (the 8 `Gw2AccountSnapshotService.Logger.Warn` sites: wallet/bank/shared
  inventory/material storage/character-list/per-character-inventory/item-metadata/currency-metadata
  fetch failures).
- Cache/seed-load anomalies (`Module.cs` Initialize catches: item-search-provider fallback line 208/214,
  acquisition-hints-unavailable line 233, GW2-build-ID-fetch-failure line 251, plus the currently-silent
  `catch { }` around vendor-baseline/recipe-seed/recipe-manifest loads at lines 164-195 - these swallow
  the exception entirely today with **no** log call; see Section 8 migration list).
- The gated `[scrolldiag]` diagnostic channel (12 sites in `CraftingPlanView.cs`, currently
  `Logger.Debug`-only, invisible in-module) - a natural fit for the new sink's Debug level, still gated
  on the existing `ScrollDiagnosticsEnabled` setting so its cost profile does not change.

**What should NOT move**: routine Blish-plumbing noise that has no module-domain meaning (e.g.
`MainThreadMarshal`'s "queued action threw"/"dropped an action" - these are about the Blish host
integration itself, not module behavior a user would search for; leave them on Blish's `Logger` only -
see Section 8).

---

## 3. Proposed UX - the Log tab as a search/view pane

Replaces `Views/LogTabContent.cs` (74 lines) with a genuine search/view pane, following the existing
**lightweight FlowPanel(CanScroll)** pattern (pattern A) - this tab's content is
label-per-row with a toolbar, not multi-column ellipsized rows that must reflow live during a resize
drag, so it does not need to opt into the M33 PlanContentHeightMath/relayout-registry contract (pattern
B, `CraftingPlanView`-only, DO-NOT-TOUCH per M38).

**Layout** (top to bottom, inside the `ViewAdapter`-provided bordered panel - same chrome every other
tab gets):

1. **Toolbar row** (fixed height, above the scroll panel - mirrors `MainView`'s existing filter-row
   pattern at `Views/MainView.cs:227` which already builds a `Dropdown` the same way):
   - **Level filter**: a `Blish_HUD.Controls.Dropdown` with items `"All"`, `"Error+"`, `"Warn+"`,
     `"Info+"`, `"Debug+"` (each includes its own level and everything more severe - i.e. a minimum-
     severity threshold, the conventional log-viewer semantics). Default `"Info+"` (Debug-level lines,
     including `[scrolldiag]`, are hidden by default even when they exist in the ring - see Section 6).
   - **Text search box**: an `AutocompleteTextBox` (already exists at `Views/AutocompleteTextBox.cs`;
     INFERRED reuse as a plain filter-as-you-type box with no suggestion source wired, i.e. autocomplete
     disabled - if that constructor shape does not support "no suggestions", fall back to a plain
     `Blish_HUD.Controls.TextBox` with a `TextChanged` handler; this is a small INFERRED risk, not a
     blocking one). Case-insensitive substring match against the raw line text.
   - **Follow-tail checkbox** ("Follow"): when checked (default ON), the view auto-scrolls to the newest
     line whenever a new one arrives while this tab is open; unchecking freezes the current scroll
     position (matches the mental model of `tail -f` vs a paused view - useful once a user is reading a
     specific older line and doesn't want the pane to jump under them).
   - **Copy to Clipboard** button: copies the *currently filtered* view (respecting level+text filters)
     as plain text to the OS clipboard, for pasting into a bug report. INFERRED: Blish HUD exposes
     clipboard access (an `AsyncClipboardService` reference appears in the WP-02 cleanup scope as a
     "remove unused `<Reference>`" candidate, meaning a clipboard-capable type is already linked into
     this project today) - confirm the exact API surface at implementation time; if unavailable, ship a
     "Select All" affordance instead (Blish `TextBox`/`Label` selection semantics need checking) - OPEN
     QUESTION for the implementer, not a blocker on the rest of the design.
   - **Clear view** button: clears what's currently displayed in the tab (does NOT delete the on-disk
     log file or the in-memory ring - see Section 9 for exact lifecycle). Useful for "I want to watch
     just what happens from now on" without losing history for a bug report.
2. **Scrollable log panel** (the existing `FlowPanel(CanScroll=true)`, resized on `Container.Resized`
   exactly like today's `LogTabContent.Build` at lines 21-34): one row per log line, `icon(level) +
   timestamp + [tag] + message`, e.g.:
   `[WARN] 14:32:07  Failed to refresh account snapshot: TimeoutException` (icon omitted here; a small
   colored square/text badge per level - reuses the existing `Label.TextColor` idiom `SettingsTabContent`
   already has for Info/Error/Success/Warning colors at lines 36-39 - is the cheapest correct choice,
   no new icon assets needed).
3. **Empty state**: when the filtered view has zero matching lines, show a single grey label - either
   `"No log entries yet."` (ring genuinely empty) or `"No entries match the current filter."` (ring has
   data, filter excludes all of it) - distinguishing these two cases matters so a user with an active
   text-search typo doesn't think logging is broken.

**What is explicitly NOT in scope for this tab**: editing/deleting individual log entries, exporting to
a file picker (clipboard covers the "attach to a bug report" use case more simply), remote log shipping.

---

## 4. Data & architecture

### 4.1 New types

- **`Services/ModuleLogEntry`** (new, small POCO/struct): `DateTime TimestampUtc`, `ModuleLogLevel
  Level`, `string Tag` (nullable - e.g. `"scrolldiag"`, `"snapshot"`, `"plan"`; free-form, not an enum,
  so new call sites never require a schema change), `string Message`. Blish-free (no `Blish_HUD.*`
  `using`), same discipline as every other `Models`/`Services` type - keeps it independently testable
  and keeps the "Tests must never reference Blish HUD" invariant trivially satisfiable.
- **`ModuleLogLevel`** enum: `Debug = 0, Info = 1, Warn = 2, Error = 3` - four levels, matching Blish's
  own `Logger` level names 1:1 (INFERRED: Blish's `Logger.Debug/Info/Warn` calls observed in this
  codebase strongly suggest this is Blish's own level set; no `Fatal`/`Trace` call sites exist anywhere
  in this repo, MEASURED via the grep in Section 2, so four levels covers 100% of current usage without
  inventing unused ones).
- **`Services/ModuleLog`** (new, static or singleton service - see 4.3 for the threading shape): the
  sink every call site writes through. Two responsibilities:
  1. **In-memory ring buffer** (`ModuleLogEntry[]` fixed-capacity circular buffer, default capacity
     2000 entries - see Section 6 for why 2000 and not "unbounded") - always populated, at every level,
     regardless of any setting. This is what backs the Log tab's live view. Cheap: 2000 small structs
     is a few hundred KB at most, held only while the module is loaded, GC'd on `Unload()`.
  2. **File sink** - gated (Section 6 policy), writes to a NEW persisted file under the data dir (NOT
     reusing `snapshot.json`/`status.txt`/the vendor overlay - a dedicated file, `data/module_log.jsonl`,
     newline-delimited JSON, one `ModuleLogEntry` per line via `Newtonsoft.Json.JsonConvert.
     SerializeObject`, append-only). Newline-delimited rather than one big JSON array (the
     `SnapshotStore`/`StatusStore`/`VendorOfferStore` shape - MEASURED) because a log file is
     fundamentally append-only and a crash mid-write to a single JSON array can corrupt the *entire*
     file, whereas a crash mid-append to JSONL loses at most the last unterminated line - the tail
     reader (Section 4.2) already has to tolerate a partial last line for exactly this reason.
- **`Services/ModuleLogStore`** (new): the file-IO half - `AppendLine(ModuleLogEntry)`, `ReadAll()` (for
  the Log tab's "load persisted history on tab open" - see Section 9), `RotateIfNeeded(maxBytes,
  maxAgeDays)`, `DeleteAll()`. Constructed with `dataDirectoryPath` exactly like `SnapshotStore`/
  `StatusStore`/`VendorOfferStore` (MEASURED constructor shape: `new XStore(dataDir[, otherDeps])`).

### 4.2 Persistence format & rotation mechanics

- **File**: `data/module_log.jsonl` (one file, not a directory of dated files - keeps `RotateIfNeeded`
  simple: a single size/age check against one path, no directory scan). Each line:
  `{"t":"2026-07-22T03:14:07Z","lvl":"Warn","tag":"snapshot","msg":"Failed to refresh account snapshot: TimeoutException"}`
  (short property names - `t`/`lvl`/`tag`/`msg` - deliberately, since this file is written far more
  often than `snapshot.json` and every byte compounds across a retention window).
- **Rotation policy** (age- and size-based, both configurable - see Section 5 settings):
  - On each `AppendLine`, cheaply check `new FileInfo(path).Length` (no need to keep a running byte
    counter in memory - `FileInfo.Length` is one syscall, not a big cost at the append rate this sink
    will actually see per the Section 6 policy) against `MaxLogSizeBytes`. If exceeded, truncate from
    the front: read the file, drop the oldest N% (propose 25%) of lines by count (cheap: JSONL means
    "line count" is trivial to reason about; do not attempt byte-exact truncation - a line is the atomic
    unit), rewrite via the SAME atomic `.tmp` + `File.Replace`/`File.Copy` pattern `StatusStore.Save`
    (MEASURED, `Services/StatusStore.cs:36-39`) and `VendorOfferStore.SaveOverlay` (MEASURED,
    `Services/VendorOfferStore.cs:85-95`) already use - **not** `SnapshotStore`'s own `File.WriteAllText`
    (MEASURED, `Services/SnapshotStore.cs:39` - plain, non-atomic; the three stores do NOT all share one
    atomic pattern, so the new store must not copy the wrong sibling).
  - On module `LoadAsync` (once per session, not per-append - age-based pruning does not need per-write
    cost), drop any line whose `t` is older than `RetentionDays` before the in-session ring/file diverge
    further. Same rewrite mechanics as the size trim.
  - Rotation runs on whatever thread called `AppendLine`/`LoadAsync` - file IO is safe off the main
    thread (existing precedent: `SaveStatusThreadSafe` in `Module.cs` already persists from a ThreadPool
    continuation) - it must NOT marshal to the main thread to do file work.
- **`onError` callback** (WP-16 shape, MEASURED from the M38 plan `Services/Diagnostics` WP-16 scope at
  `m38-cleanup-plan.md` lines 263-272): `ModuleLogStore` takes an `Action<string, Exception> onError =
  null` constructor parameter from day one, called instead of a bare `Debug.WriteLine` on any IO
  failure inside the store. This is exactly the shape WP-16 is retrofitting onto the four existing
  stores - building it in now means this is not a fifth store WP-16 has to revisit later. A log
  store's own IO failure obviously must never itself try to log through the same
  sink (unbounded recursion) - `Module.cs` wires this callback to `Logger.Warn` (Blish's own logger),
  the same target WP-16 wires the other four stores' callbacks to.

### 4.3 Threading

- **Write path**: `ModuleLog.Write(level, tag, message)` must be safely callable from ANY thread -
  ThreadPool continuations (`Module.cs`'s snapshot refresh, `CraftingPlanPipeline`'s async generation),
  the main/UI thread (`CraftingPlanView`'s `[scrolldiag]` sites, which run synchronously inside Blish's
  `Update`/`Paint`/input-event callbacks), and background `Task.Run` bodies (the build-ID fetch in
  `Module.cs:241`). The ring buffer write is a `lock`-protected circular-index write (small, bounded,
  contention only ever between a handful of concurrent producers - never a hot loop) - simplest correct
  choice; do not reach for `ConcurrentQueue`/channel machinery the module has no other precedent for and
  does not need at this call volume (Section 6's own policy keeps this to a few lines per user action,
  not a hot path). The file-sink append can run inline on whatever thread called `Write` (file IO is
  not a control mutation - safe off the main thread, same reasoning `SaveStatusThreadSafe` already
  relies on) OR be pushed through the existing `FrameTicker`/dirty-flag idiom if batching writes turns
  out to matter - **not needed at this call volume**; do it inline, keep it simple, revisit only if a
  future measurement shows contention (Efficiency Principle in this repo's own review rules: prefer
  simple over clever until a number says otherwise).
- **CRITICAL invariant, must not be violated by the implementation**: `ModuleLog.Write` must NEVER touch
  a Blish control directly (no dispatching to the Log tab's `FlowPanel` from inside `Write`). The tab
  view reads the ring on its own cadence (Section 4.4), not via a push callback from the sink - this
  is the same "producer never touches the consumer's UI" separation `Module.cs` already uses for
  `_snapshotDirty`/`_statusDirty` (MEASURED, `Module.cs:453-475`).
- **Read/refresh path for the Log tab**: reuse the **same dirty-flag-drain-in-Update() idiom** already
  established in `Module.cs` (`_snapshotDirty`/`_pendingSnapshot`, `_statusDirty` at lines 71-79,
  453-475) rather than inventing a new mechanism or reaching for a `FrameTicker` (that primitive exists
  for *multi-frame* work items like the scroll-verify window - a "did the ring change" poll is a single
  cheap check, not multi-frame work). Concretely: `ModuleLog` exposes a monotonically increasing
  `long Version` (incremented under the same lock on every `Write`); the Log tab's `Update`-adjacent
  refresh (wired the same way `Module.cs` already wires `_mainWindow.TabChanged` to `_logContent.Refresh()`
  at line 415-421, PLUS a poll) compares the last-seen version to the current one each `Module.Update()`
  tick and calls `Refresh()`/an incremental-append method only when it changed - avoids a full
  dispose+rebuild of every row on every frame the tab happens to be open (today's `LogTabContent.Refresh`
  disposes+rebuilds ALL rows every call - MEASURED, lines 43-46 - acceptable for a 74-line stub shown
  only on tab-change, but must not become an every-frame cost once this tab can also update while
  already open and "Follow" is checked). Concretely this means: append-only incremental update (add new
  Blish `Label` controls for only the newly-arrived entries, matching the existing lightweight
  FlowPanel-per-row pattern used by `MainView`/`SettingsTabContent`) rather than the full-rebuild
  `Refresh()` on every version bump - full rebuild stays as the *filter-changed* path (level dropdown,
  text search, or explicit "Clear view") since those genuinely need to redraw every visible row anyway.

### 4.4 What is reused vs new

| Piece | Reused from | New |
|---|---|---|
| Atomic file write | `StatusStore`/`VendorOfferStore` `.tmp`+`Replace` pattern | `ModuleLogStore` itself |
| `onError` callback shape | WP-16's proposed shape (not yet landed) | First store to actually ship it |
| Dirty-flag/Update() drain | `Module.cs` `_snapshotDirty`/`_statusDirty` | `ModuleLog.Version` counter |
| Lightweight FlowPanel(CanScroll) | `MainView`/old `LogTabContent`/`SettingsTabContent` | Toolbar row (Dropdown+search+checkbox+buttons) |
| `Dropdown` control idiom | `MainView`'s filter Dropdown (`Views/MainView.cs:227`) | Level-filter Dropdown instance |
| Settings JSON-string-in-one-entry idiom | `CurrencyValuationsJson` template | N/A - new settings here are plain `int`/`bool`, simpler than that template needs |
| Blish-free test pattern (`IDisposable`, temp dir) | `VendorOfferStoreTests`/`StatusStoreTests` | `ModuleLogStoreTests`, `ModuleLogTests` |

No changes to `AccountSnapshot`, `AccountItemIndex`, `CraftingPlanPipeline`'s public surface, or
`PlanContentHeightMath`/relayout registry. `PlanTimingAnalyzer` is untouched - its regex-based
summarization of timing lines is a separate, narrower concern (parsing `CraftingPlanResult.DebugLog`
for the Crafting Plan tab's own use) and this proposal does not fold it into `ModuleLog`; the Crafting
Plan tab keeps generating its own `DebugLog` exactly as today (out of scope: this document proposes no
changes to the Crafting Plan tab). The only NEW cross-reference: whichever `Generate*Async` call
site already appends to `DebugLog` may ALSO call `ModuleLog.Write(Info, "plan", ...)` for the
lifecycle-level facts (start/finish/failure/elapsed) - see Section 8's migration table - but the two
lists remain independent; nothing is deleted from `CraftingPlanResult.DebugLog`.

---

## 5. Settings introduced (all via `ModuleSettings.cs`, the existing `DefineSetting<T>` primitive-only
template - MEASURED, `Services/ModuleSettings.cs:66-105` - no new settings idiom needed):

| Setting | Type | Default | Rationale |
|---|---|---|---|
| `LogMaxSizeBytes` | `int` | `2 \* 1024 \* 1024` (2 MB) | Requirement: a maximum log size on disk. 2 MB of JSONL at the Section 6 write rate is INFERRED to hold weeks of normal-Info-level history comfortably (a few hundred bytes/line, low write rate - see Section 6) while remaining a trivial disk footprint even for a user who never opens Settings. |
| `LogRetentionDays` | `int` | `14` | Requirement: a days-of-retention setting. Two weeks covers "the bug happened a few days ago, can you send me the log" without becoming a silent multi-month accumulation. |
| `LogDiagnosticsEnabled` | `bool` | `false` | Requirement: log only what is useful. Gates whether Debug-level lines (including the migrated `[scrolldiag]` channel - see Section 8) reach the FILE sink at all; they always still land in the in-memory ring (so a live "turn it on, reproduce, turn it off, read the tab" flow works without a file at all) - see Section 6's exact policy table. This SUBSUMES the existing `ScrollDiagnosticsEnabled` setting (Section 8: migrate, do not duplicate). |
| `LogMinFileLevel` | `int` (stored as the `ModuleLogLevel` ordinal; 0-3) | `1` (Info) | The floor level that reaches the file sink even when `LogDiagnosticsEnabled` is false. Kept a separate setting from the diagnostics toggle so a power user can, in principle, silence even Info-level file writes (set to `2`/Warn) without touching the diagnostics toggle's meaning - OPEN QUESTION 3 below asks whether this is worth exposing in the UI at all vs. hardcoding. |

**UI**: reuses **idiom (b)** from `SettingsTabContent` (TextBox + per-row error Label + shared Save
button, `SettingsInputParser.TryParse*` validation, "invalid rows left unchanged not cleared" contract
- MEASURED, `Views/SettingsTabContent.cs`) for `LogMaxSizeBytes`
(accept a human-friendly value like `"2"` labeled "MB", convert to bytes at parse time - do not make
the user type raw byte counts) and `LogRetentionDays` (plain integer, 1-365 clamp mirroring the
existing Homestead-tier 0-2 clamp pattern at `ModuleSettings.cs:128-133`). Reuses **idiom (a)**
(immediate-apply `Checkbox`, no Save button - MEASURED, `ValueOwnMaterials`'s pattern) for
`LogDiagnosticsEnabled`, replacing (not duplicating) the existing JSON-only `ScrollDiagnosticsEnabled`
flip - see Section 8. `LogMinFileLevel` is proposed as NOT exposed in the UI initially (hardcoded at
Info) unless the extra control surface is wanted - see Open Question 3.

A new "Logging" section header in `SettingsTabContent.Build`, following the existing
`AddSectionHeader`/`AddInfoLine`/row-builder idiom (MEASURED pattern names) - no new
control idiom invented, per this repo's explicit rule ("No Dropdown/stepper control is used anywhere in
Views for settings today... reuse idiom (a) or (b), not invent a third").

---

## 6. Level policy - log only what is useful

Exact ring-buffer-vs-file-sink policy, addressing the disk-usage requirement directly:

| Level | Ring (always) | File sink | When to use |
|---|---|---|---|
| **Error** | Yes | Always (regardless of `LogDiagnosticsEnabled`) | Something the user should know failed and didn't silently degrade - e.g. a snapshot fetch that timed out and left stale data, a plan generation that threw. Rare by construction (repo invariant: prefer graceful degradation - most current `catch` blocks already recover, so genuine Errors should be uncommon). |
| **Warn** | Yes | Always | A degradation that was handled but the user might want to know about - migrates the 8 `Gw2AccountSnapshotService.Warn` sites, `Module.cs`'s refresh-failed sites, `MainView`'s "Refresh Now failed", `SettingsTabContent`'s "Failed to save currency valuations". |
| **Info** | Yes | Always (respects `LogMaxSizeBytes`/`LogRetentionDays` trims, but not gated by the diagnostics toggle) | One line per discrete lifecycle event, NOT progress spam - "Plan generation started (3 items)" / "Plan generation finished in 842ms" / "Snapshot refreshed: 1,204 items, 6 wallet entries" / "Item search fallback to static provider: <reason>". This is the tier the "useful only" requirement is really about: one Info line per user-visible action, not one per internal step. |
| **Debug** | Yes | Only when `LogDiagnosticsEnabled` is true | Everything currently behind `ScrollDiagnosticsEnabled`'s 12 `[scrolldiag]` sites, plus any future fine-grained troubleshooting output. This is exactly Blish's own `Logger.Debug` usage today, migrated. |

**Why this shape bounds disk growth**: Error/Warn/Info together are, by
construction, at most a few dozen lines per typical session (one snapshot refresh cycle every 10 minutes
per `Module.cs`'s `StaleThreshold`, MEASURED at line 42; one plan generation per user Generate click,
a human-paced action) - the disk-growth risk is entirely concentrated in the Debug tier, which is the
one tier this policy gates behind an explicit opt-in toggle that defaults OFF. Combined with the
size/age caps (Section 5), the worst case with diagnostics left on indefinitely is bounded at
`LogMaxSizeBytes` (2 MB default) - the size cap is a hard backstop even if a user forgets diagnostics
is on, not just a soft preference.

**Ring capacity (2000 entries) vs file cap (2 MB) are independent knobs**: the ring is what the Log tab
can show *right now* without reading the file back; the file is the durable history. 2000 entries at the
Error/Warn/Info write rate above is INFERRED to comfortably span "this session plus the last several" -
if the ring wraps mid-session that's fine, the file still has the full history within its own cap.

---

## 7. Log tab lifecycle - "cleared on what"

- **In-memory ring**: populated from module load; survives tab close/reopen (backed by the singleton
  `ModuleLog`, not by the view); cleared only on module `Unload()` (process exit / module disable) -
  never by user action inside the tab (the "Clear view" button, Section 3, only clears the *view's own
  filtered rendering*, not the ring itself, so switching tabs and coming back still shows history).
- **On-disk file**: survives across sessions entirely (that is the point - "send me your log file" for
  a bug report spanning a prior session). Trimmed only by the rotation policy (Section 4.2) - never
  wholesale-deleted except by an explicit user action. Proposed addition: a "Clear log file" button
  (separate from "Clear view") that calls `ModuleLogStore.DeleteAll()` - OPEN QUESTION 4 below asks
  whether this should be exposed at all vs. leaving rotation as the only cleanup path.
- **On tab open** (`Build`): the view loads the CURRENT ring contents (not the on-disk file - the ring
  already holds everything written this session, and reading the file back on every tab-open would be
  wasted IO for the common case) into the FlowPanel, respecting the current filter state (defaults:
  `"Info+"`, empty search, Follow ON). Showing pre-session history from the file on tab open is a
  deliberate, separate feature (read `ModuleLogStore.ReadAll()` once at
  `LoadAsync` and seed the ring with it before the module logs its own first line this session) - this
  proposal recommends doing exactly that (seed the ring from the file's tail at startup, capped at ring
  capacity) since it costs nothing extra architecturally and directly serves the "meaningful log system"
  requirement (a log that only ever shows "since I last opened the tab" is not much better than today's
  per-generation reset).

---

## 8. Existing call sites: MIGRATE vs STAY

**MIGRATE to `ModuleLog` (in addition to, not instead of, the existing Blish `Logger` call - both sinks
fire from the same call site; this is additive, not a rip-and-replace, so Blish's own log still gets
everything it gets today for anyone debugging via Blish's own log viewer)**:

| Site | Current | New `ModuleLog` call | Level |
|---|---|---|---|
| `Module.cs:508` "Refreshing account snapshot..." | `Logger.Info` | `Write(Info, "snapshot", ...)` | Info |
| `Module.cs:561` "Fetched snapshot CapturedAt=... items=..." | `Logger.Info` | `Write(Info, "snapshot", ...)` | Info |
| `Module.cs:611` "Failed to refresh account snapshot" | `Logger.Warn` | `Write(Warn, "snapshot", ...)` | Warn |
| `Module.cs:557` "Discarding snapshot fetch superseded by Clear Cache" | `Logger.Info` | `Write(Info, "snapshot", ...)` | Info |
| `Module.cs:208/214` item-search fallback | `Logger.Info` | `Write(Warn, "startup", ...)` | Warn (a degraded fallback, not routine) |
| `Module.cs:233` acquisition hints unavailable | `Logger.Info` | `Write(Warn, "startup", ...)` | Warn |
| `Module.cs:251` build-ID fetch failure | `Logger.Debug` | `Write(Debug, "startup", ...)` | Debug (genuinely low-stakes - build-ID only affects cache invalidation) |
| `Module.cs:164-195` **currently-silent** `catch { }` around vendor-baseline/recipe-seed/manifest load | *(nothing today - bare catch, no log call at all)* | `Write(Warn, "startup", ...)` | Warn - **this is a real gap the migration closes, not just a routing change** |
| `Gw2AccountSnapshotService.cs` 8x `Logger.Warn` (wallet/bank/shared-inv/mat-storage/char-list/per-char/item-meta/currency-meta) | `Logger.Warn` | `Write(Warn, "snapshot-fetch", ...)` | Warn |
| `MainView.cs:195` "Refresh Now failed" | `Logger.Warn` | `Write(Warn, "snapshot", ...)` | Warn |
| `SettingsTabContent.cs:530` "Failed to save currency valuations" | `Logger.Warn` | `Write(Warn, "settings", ...)` | Warn |
| `CraftingPlanView.cs` 12x `[scrolldiag]` sites | `Logger.Debug` (gated on `ScrollDiagnosticsEnabled`) | `Write(Debug, "scrolldiag", ...)` (gated on the renamed/reused `LogDiagnosticsEnabled`) | Debug |
| Plan generation start/finish/failure (currently only inferred from `DebugLog`/timing lines, not a discrete `Logger` call today) | *(none - new)* | `Write(Info, "plan", "Generation started (N items)")` / `Write(Info, "plan", "Generation finished in Nms")` / `Write(Warn, "plan", "Generation failed: ...")` | Info/Warn - **new lifecycle events, not a migration of an existing call** |

**STAY on Blish's `Logger` only (do NOT migrate)**:

- `MainThreadMarshal.cs`'s two sites (`Logger.Debug` "dropped an action", `Logger.Warn` "queued action
  threw") - these are about the Blish-host integration primitive itself, not module domain behavior;
  a user reading the Log tab gets no actionable signal from "an internal marshal queue dropped
  something," and duplicating it doubles noise for zero benefit. If `MainThreadMarshal` failures ever
  become frequent enough to matter, that is itself a bug to fix, not a log line to surface to users.
- Any `Logger.Debug`/`Info` inside `CraftingPlanView.cs` NOT already tagged `[scrolldiag]` (if any exist
  beyond the 12 counted sites - re-verify exact count at implementation time; the 25x total for that
  file includes the 12 `[scrolldiag]` sites plus whatever remainder are `LastDebugLog`-adjacent, which
  are explicitly out of scope: this document proposes no changes to the Crafting Plan tab).

**Rationale for the additive (not replacing) approach**: Blish's own `Logger` calls remain because (a)
they cost nothing extra to keep, (b) anyone with direct file-system/Blish-log-viewer access loses
nothing, and (c) it avoids a risky "delete and re-route 48 call sites in one PR" - each migrated site is
a one-line addition (`ModuleLog.Write(...)` alongside the existing `Logger.X(...)` call), which is
low-risk, easily reviewed diff-by-diff, and trivially revertible per-site if one turns out noisy.

---

## 9. Invariant / contract impacts

- **Blish-free tests**: `ModuleLogEntry`, `ModuleLogLevel`, `ModuleLog`, `ModuleLogStore` must all avoid
  any `Blish_HUD.*`/`Gw2Sharp.*`/`Microsoft.Xna.*` `using` (the sink's public surface is plain C#
  types/strings; only the Log tab VIEW touches Blish controls). New tests
  `tests/GW2CraftingHelper.Tests/Services/ModuleLogStoreTests.cs` (real temp-dir file IO, `IDisposable`
  teardown, same shape as `VendorOfferStoreTests`/`StatusStoreTests` - MEASURED template) and
  `ModuleLogTests.cs` (ring wraparound, level filtering, thread-safety smoke test via concurrent writes
  from multiple `Task.Run` bodies) satisfy "tests exercise real production code paths" - no
  contract-mirror tests.
- **No raw IDs shown to users**: N/A - log messages are free text about module behavior, not IDs; this
  proposal's migrated call sites already avoid raw item/currency/vendor IDs in their message text
  (MEASURED: existing messages use counts/names/exception text, not IDs) - implementer should keep this
  discipline for any new `Write` call (e.g. do not log raw `itemId` values in plan-lifecycle lines;
  log item counts/names instead, matching existing `Gw2AccountSnapshotService` message style).
- **Coin icon invariant**: N/A - the Log tab shows no coin amounts.
- **No runtime wiki/gw2efficiency calls**: N/A - this feature adds no network calls at all (pure local
  logging/persistence).
- **M33 layout contract (`PlanContentHeightMath`/relayout registry)**: NOT touched, NOT opted into - the
  Log tab uses pattern A (lightweight FlowPanel), explicitly out of the M33 blast radius: a new tab
  only needs to opt into any of that contract if it grows genuinely complex, and otherwise pattern (A)
  is the correct, far-cheaper default.
- **Atomic persistence pattern**: `ModuleLogStore` follows the `StatusStore`/`VendorOfferStore` atomic
  `.tmp`+`Replace` shape (Section 4.2) - not `SnapshotStore`'s non-atomic `WriteAllText` (see Section
  4.2).

---

## 10. Effort class

**M** (Medium). Justification: no changes to solver/pipeline/pricing math or the invariant-heavy
`CraftingPlanView` machinery (which is where L/XL effort in this codebase concentrates); the new surface
is one small sink service + one small file-backed store (both following an already-established template
almost line-for-line) + a tab-view rewrite that reuses an existing lightweight pattern already proven in
three other tabs + ~15 one-line migration edits at existing call sites + 4 new primitive settings using
the existing `DefineSetting<T>` template (no new settings idiom). The size driver is breadth (many small
touch points across `Module.cs`, `Gw2AccountSnapshotService.cs`, `MainView.cs`, `SettingsTabContent.cs`,
`CraftingPlanView.cs`'s 12 `[scrolldiag]` sites) rather than depth/risk in any single file - each touch
point is a low-risk additive one-liner, not a rewrite, so this sits below the L threshold this repo
reserves for CraftingPlanView-scale structural work.

---

## 11. Dependencies & sequencing (incl. M38 packages)

- **No WP package currently targets this area** (confirmed by a full read of `m38-cleanup-plan.md`) - this is genuinely greenfield relative to M38, so it can be sequenced
  independently of the WP waves with one exception below.
- **WP-16** (`m38-cleanup-plan.md` lines 263-272, MEASURED): adds the `onError` callback shape to
  `SnapshotStore`/`StatusStore`/`VendorOfferStore`/`OverlayRecipeCacheStore`. This proposal's
  `ModuleLogStore` should build that shape in from day one (Section 4.2) rather than landing without it
  and needing a follow-up - **no hard ordering dependency either direction** (this feature can land
  before or after WP-16; if it lands first, `ModuleLogStore` is simply the reference implementation
  WP-16's four retrofits can point at).
- **WP-17** (`m38-cleanup-plan.md` lines 274-283, MEASURED): centralizes `Module.cs` catch-consistency
  and `FrameTicker` teardown-on-`Unload`. This feature's design deliberately does NOT start its own
  `FrameTicker` (Section 4.3 - dirty-flag poll instead), so there is nothing for `ModuleLog` to register
  into WP-17's teardown path. If `Module.cs`'s Unload sequence changes shape under WP-17, the only
  touch point this feature has there is disposing `ModuleLog`'s ring (trivial - no unmanaged resources,
  a plain field can just go out of scope) - flag this as a one-line note for whichever PR lands second.
- **WP-27/28** (docs restructure, `KNOWN-ISSUES.md` split): whichever PR implements this proposal should
  add its own "why JSONL, why these caps" rationale to `docs/KNOWN-ISSUES.md` or `docs/ARCHITECTURE.md`
  in whichever shape WP-27 lands in - sequence-agnostic, just don't write to a `KNOWN-ISSUES.md` location
  WP-27 is mid-relocating.
- **Suggested internal sequencing** (within this feature, as separate commits/PRs per this repo's
  "logical git commits" rule): (1) `ModuleLogEntry`/`ModuleLogLevel`/`ModuleLog`/`ModuleLogStore` +
  tests (pure service layer, no UI, no `Module.cs` wiring yet); (2) `ModuleSettings.cs` additions +
  `SettingsTabContent.cs` "Logging" section; (3) `LogTabContent.cs` rewrite (search/filter/follow/copy
  UI) wired to a throwaway in-memory-only `ModuleLog` instance for manual testing; (4) `Module.cs`
  wiring + the ~15 call-site migrations (Section 8), one file at a time, each independently reviewable
  and revertible.

---

## 12. Open questions

1. **Clipboard API availability** (Section 3): the exact Blish HUD clipboard surface needs a one-shot
   verification (an `AsyncClipboardService`-shaped reference already appears linked into this project
   per the WP-02 cleanup notes, but its actual usability for a plain "copy this string" operation is
   unconfirmed) before committing to "Copy to Clipboard" as a real button vs. a "Select All + Ctrl+C"
   fallback.
2. **Seed-the-ring-from-file-at-startup** (Section 7): this proposal recommends it (better matches "a
   meaningful log system," not "log tab resets every launch"), but it is an explicit design choice, not
   free - confirm whether pre-session history should be visible on first tab-open each session, vs.
   a simpler "ring starts empty every launch, file is history-only, read only via Copy/a future export"
   design that is marginally cheaper to reason about.
3. **`LogMinFileLevel` UI exposure** (Section 5): proposed as hardcoded (Info floor, not user-editable)
   to avoid adding a fifth setting for a knob that mostly matters when debugging a report, not to end
   users. Confirm whether it should be a real Settings-tab control or stay code-only.
4. **"Clear log file" button** (Section 7): should the Log tab expose a destructive "delete the on-disk
   history now" action distinct from rotation, or should rotation (age/size caps) be the only cleanup
   path a user has? Leaning toward yes (cheap, matches "Clear Cache" precedent already in the Snapshot
   tab), but explicitly left open.
5. **Plan-generation lifecycle logging ownership** (Section 4.4/Section 8's last row): this proposal adds
   NEW `Write(Info/Warn, "plan", ...)` calls around `CraftingPlanPipeline`'s `Generate*Async` methods -
   technically a (very small, additive-only) touch to a file WP-13 (`m38-cleanup-plan.md` lines 231-238)
   is also going to restructure (extracting shared helpers across the three `Generate*Async` overloads).
   Confirm whether this feature's plan-lifecycle logging lands before or after WP-13 - either order is
   low-risk (additive one-liners at method entry/exit, not touching the extracted-helper bodies WP-13
   is reshaping), but one order may be preferable for diff-cleanliness.
6. **Level-filter default** (`"Info+"`, Section 3): confirm this matches the intended "useful default
   view" vs., say, defaulting to `"All"` and letting users dial down noise themselves.
