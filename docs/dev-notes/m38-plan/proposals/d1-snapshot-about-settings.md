# D1 - Snapshot Search, About, Settings Audit

Design proposal only. No production code was changed to produce this document
(read-only session against `/mnt/c/Dev/Blish/GW2CraftingHelper`, no build/test
run, no worktrees under `/mnt/c/Dev/Blish/wt-m38-*` touched or referenced as
authoritative). Covers three of the seven module tabs: **Snapshot**
(redesign), **About** (new), **Settings** (extend + audit). Log, Plan History,
and Crafting Ranker are explicitly out of scope for this document (assumed to
be separate proposals in this same wave) and are referenced only where they
create a sequencing or naming collision worth flagging.

## Epistemic legend

- **MEASURED** - read directly from this repo's source, or independently
  re-verified this session against a real Blish HUD artifact (source file,
  shipped assembly).
- **INFERRED** - a reasoned conclusion from measured evidence, not itself
  directly observed (e.g. "this property probably lives on that type").
- **GUESS** - no supporting evidence gathered; flagged as such wherever used
  (rare in this document; open questions are used instead of guessing).

No time/memory/duration estimates appear anywhere below, per instruction.
Effort is classified S/M/L/XL with a one-line reason only.

---

## Cross-cutting findings (apply to more than one feature below)

These came out of this session's own reading and are load-bearing for more
than one section, so they are stated once here rather than repeated.

### Tab views are rebuilt from scratch on every visit, not cached (MEASURED)

I pulled `TabbedWindow2.cs` from the `blish-hud/Blish-HUD` `dev` branch (MIT
licensed, public repo, via `gh api repos/blish-hud/Blish-HUD/...`) to settle
this rather than assume it:

```csharp
protected virtual void OnTabChanged(ValueChangedEventArgs<Tab> e) {
    ShowView(e.NewValue?.View());   // <-- Func<IView> invoked fresh, every tab click
    ...
}
```

`Tab.View` is a `Func<IView>` and it is re-invoked **every time the tab
becomes selected**, not just the first time. `Module.cs` registers each tab
with an inline lambda that allocates a brand-new `ViewAdapter` on every call
(e.g. `() => new ViewAdapter("Snapshot", c => _snapshotContent.Build(c))`),
and `ViewAdapter.Build()` itself opens with "Defensive: clear any existing
children before rebuilding" - confirming the design already expects to run
repeatedly over a tab's lifetime, not once.

**Implication for every tab designed below:** a view's own `Build()` method
re-runs (and every `Control` it creates gets disposed and recreated) on every
single tab click, including re-clicking a tab the user just navigated away
from. Any per-session UI state that should feel "sticky" (a typed search
string, a checked filter) will NOT survive that by itself - it must be cached
in the long-lived view **instance**'s own fields (the instance itself
persists across tab switches; only its rendered `Control`s get torn down) and
re-applied when `Build()` runs again. This is already true of MainView's
existing `Dropdown`/`Checkbox` today (they silently reset to defaults on
every tab revisit) - not a regression I'm introducing, just a fact the
Snapshot design below accounts for deliberately instead of inheriting by
accident.

It also means a `TabChanged`-driven refresh (like `LogTabContent.Refresh()`)
is refreshing a view that is about to be rebuilt anyway by the same
`OnTabChanged` call - I could not fully resolve why `LogTabContent` needs
both without reading Blish's `ViewContainer`/presenter internals (out of
scope here; flagged as an open question for whoever owns the Log tab
proposal). It does **not** affect anything proposed in this document: none
of Snapshot/About/Settings need a `TabChanged` subscription, because
`Build()` already reruns with fresh data on every visit.

### `FrameTicker` is private to `CraftingPlanView`, not a shared primitive (MEASURED correction)

The scout summary's "FrameTicker for multi-frame work" reads as a shared
module primitive. Reading `Views/CraftingPlanView.cs` directly shows
`private sealed class FrameTicker : Control` is a **nested private class
inside `CraftingPlanView` itself** (line 457), instantiated three times for
its own scroll/resize machinery. There is no reusable `FrameTicker` service
elsewhere in `Services/` or `Views/`. None of the three features below need
one - flagged so nobody reaches for a nonexistent shared ticker instead of
Module's existing per-tick polling (`Update()`'s dirty-flag drain) or a
view's own lazy recompute at `Build()` time.

### M38 collisions relevant to this document (read from `m38-cleanup-plan.md`)

| WP | Scope (quoted) | Relevance here |
|---|---|---|
| **WP-21** | "move the ~500-700 lines of `static`, instance-state-free rendering primitives into `Views/Rendering/`: `CoinCurrencyRenderer` (coin+currency segment build/layout/reposition ...)" | Snapshot tab's coin panel should call this once it exists. |
| **WP-22** | "`Views/MainView.cs` - delete its private coin color/segment code, call the WP-21 `CoinCurrencyRenderer` (left-anchored mode) ... Removes the second independent encoding of the coin invariant." | This is literally MainView's coin code - direct collision, see Feature 1 sequencing. |
| **WP-16** | "add `Action<string, Exception> onError = null` (no-op default) to `SnapshotStore`, `StatusStore`, `VendorOfferStore`, `OverlayRecipeCacheStore` ... a NEW store ... should be built with that same onError-callback shape from day one" | No new store is proposed in this document (About is derived/read-only, Settings additions are primitive `SettingEntry<T>`), so WP-16 is **not** touched or owed anything here. Recorded so a future Plan History/Ranker/Log store proposal doesn't have to re-discover this. |
| **WP-17** | "convert the 6 bare `catch` in `Module.cs` ... add `StopLiveTickers()` ... call it from `Module.Unload()`" | Nothing proposed here starts a new ticker, so no new `Unload()` teardown obligation. The one `Module.cs` edit this document does propose (Feature 1's `LoadAsync` snapshot-dirty fix) is a two-line addition next to code WP-17 will also be touching - flagged as a shared-file sequencing note in Feature 1. |
| **WP-27** | "rewrite `README.md` ... new `docs/ARCHITECTURE.md` ... Split `docs/KNOWN-ISSUES.md` ... **remove** the AI-session-handoff tail" | About tab's "source location" content should point at whatever WP-27 lands, not duplicate it. |
| **WP-28** | "Reconcile `ref/wiki_vendor_cache.json` ... Verify `manifest.json` `\"directories\":[\"data\"]` ... Reconcile 'Lachlan Mulcahy' vs 'MaximusCub' branding." | About tab's author/contributor line is explicitly WP-28's decision to make, not this proposal's - see Feature 2. |
| WP-04/23/24/25/26 | `CraftingPlanView` decomposition waves | Confirms `CraftingPlanView.cs` is out of scope; several rejected Settings candidates in Feature 3 name this explicitly as the reason. |

---

## Feature 1: Snapshot tab - account inventory search

### Problem/intent

User directive (verbatim): *"a way to search your bag, banks, etc."*

Today (MEASURED, `Views/MainView.cs`, 449 lines): the Snapshot tab has a
header row, a 3-item content-type `Dropdown` (All/Items/Wallet), an
`Aggregate` checkbox, a coin panel, and one scrollable `FlowPanel` of plain
icon+text rows built by `RebuildItems`/`RebuildWallet`. **There is no text
search anywhere in this tab.** Rows are built directly from
`AccountSnapshot.Items` (one `SnapshotItemEntry` per storage source per
item), so the same item already appears as multiple flat rows today (one per
source) unless "Aggregate" collapses them into a single unlabeled total -
there is no view that shows "this item, broken down by where it is."

A backing structure for exactly that already exists and is completely unused
by this tab: `Services/AccountItemIndex.cs` (itemId -> source -> count) plus
its `GetPrioritizedSources` static helper (ranks `MaterialStorage` > active
character > `SharedInventory` > `Bank` > other characters, alphabetically).
It already has a real, Blish-free test file
(`tests/.../Services/AccountItemIndexTests.cs`).

I also traced a concrete, confirmed bug that the brief asked this design to
"subsume a fix for" - see the dedicated subsection below.

### Proposed UX

Row-by-row (keeps every existing row's Y-position semantics; this is an
in-place redesign of `MainView.cs`, not a new file):

```
+----------------------------------------------------------------------+
| Account Snapshot        Updated - 3:41 PM (2m ago)   [Clear Cache] [Refresh Now] |
+----------------------------------------------------------------------+
| [ Search items and currencies...            ]  Show: [All v]        |
| [x]Bank [x]Material Storage [x]Shared Inv [x]Characters              |
+----------------------------------------------------------------------+
| 123[gold] 45[silver] 67[copper]                                      |
+----------------------------------------------------------------------+
| [icon] Copper Ore                                    x250            |
|          Material Storage 150 - Bank 100                             |
| [icon] Iron Ore                                       x40             |
|          Character: Zaeed 40                                         |
| ...                                                                   |
+----------------------------------------------------------------------+
```

- **Header row** - unchanged buttons/position. Status label gains a
  staleness suffix, e.g. `Updated - 3:41 PM (2m ago)`, recolored (existing
  `InfoTextColor`-style treatment, no new asset) once the snapshot is older
  than the refresh interval (see Feature 3's new
  `SnapshotRefreshIntervalMinutes` setting - reusing the *same* threshold
  value here means the staleness label and the auto-refresh trigger can
  never visually disagree about what "stale" means).
- **Search row** - a plain `Blish_HUD.Controls.TextBox` (not the
  `AutocompleteTextBox` subclass - see "why not reuse SuggestionPanel"
  below), `TextChanged`-driven, substring-filters by item name *and* wallet
  currency name, case-insensitive. Existing content-type `Dropdown`
  (All/Items/Wallet) is kept alongside it (unchanged position/options)
  because Wallet entries have no per-source breakdown at all - filtering by
  source structurally does not apply to currencies.
- **Source-filter row** - a new row of plain `Checkbox` controls, one each
  for Bank / Material Storage / Shared Inventory / Characters (all checked
  by default = show everything, matching today's implicit "no filter"
  behavior). Unchecking one hides that source's contribution from every
  item's total and breakdown; an item with zero quantity across the checked
  sources drops out of the list entirely. This row is only meaningful when
  the content-type dropdown includes Items (`All`/`Items`); it can simply be
  left visible-but-inert when `Wallet` is selected rather than adding show/
  hide logic that itself needs testing - a deliberate simplicity choice.
- **The existing `Aggregate` checkbox is retired**, not kept. Its job (one
  row per item instead of one row per item-per-source) becomes the *only*
  behavior of the redesigned list - every item row is now always grouped via
  `AccountItemIndex`, with a bold total and a gray sub-line breaking the
  total down by source (ordered via the existing, unmodified
  `AccountItemIndex.GetPrioritizedSources`). This directly satisfies "result
  rows with counts per source" as the default, not an opt-in.
- Per-source labels strip the internal `"Character:"` encoding prefix before
  display (e.g. show `Character: Zaeed`, or just `Zaeed`, never the raw
  `Character:Zaeed` token) - a small polish fix, not a raw-ID violation
  (`Source` strings are already display names, not numeric ids), but showing
  an internal encoding token verbatim would look like a bug.
- **Coin panel** - unchanged position; see WP-21/22 sequencing note below
  for what renders it.
- **Empty states** (two, both currently missing or under-specified):
  - No snapshot has ever loaded (never fetched, disk file absent) - keep
    today's exact message: `"No snapshot available. Click Refresh Now."`
  - A snapshot exists but the current search text + source filters match
    nothing - **new** message, e.g. `No items match "linen" in the selected
    sources.` Today this case renders a silently blank list (confirmed by
    reading `RebuildItems`/`RebuildWallet` - there is no "zero results"
    label anywhere in the existing code), which looks broken. Adding this
    label is a small, in-scope bug fix riding along with the redesign.

#### Why not reuse `SuggestionPanel`/`AutocompleteTextBox`/`IItemSearchProvider` wholesale

The brief asks to reuse the Crafting Plan tab's suggestion/search UI "where
sensible." I read `Views/SuggestionPanel.cs` (383 lines),
`Views/AutocompleteTextBox.cs` (44 lines), and
`Contracts/IItemSearchProvider.cs` in full before deciding. That machinery
is shaped for a **different** interaction: type a few characters, get a
floating dropdown of candidate items, arrow/click to pick exactly **one**,
which then drives a brand-new plan generation. It exists specifically
because `IItemSearchProvider`'s own contract requires every result to be "a
confirmed plan target" (`ItemSearchResult.IsPlanTarget`) - it is deliberately
*not* a general item-name search, it is a plan-target search, and reusing it
here would either violate that contract or require weakening it for
everyone.

What the Snapshot tab actually needs is closer to MainView's own existing
idiom one row up: a control changes -> `RebuildContent()` reruns
(`_filterDropdown.ValueChanged`/`_aggregateCheckbox.CheckedChanged` already
do exactly this). The search box is a third input to that same, already
-established pattern - a plain `TextBox` + `TextChanged`, no floating panel,
no cancellation token, no `MainThreadMarshal` involvement, because unlike
`CraftableItemSearchProvider`'s scan over the module's item-name seed
catalog, this "search" is a synchronous, in-memory substring scan over data
that is *already fully loaded* (`AccountSnapshot.Items`) - there is nothing
to await and nothing that can arrive out of order. Forcing the
async/cancel/marshal ceremony `SuggestionPanel` needs (because it *can*
be handed a slower provider in the future, per its own code comment) onto a
search that structurally never needs it would be new complexity with no
payoff - exactly what the repo's "prefer simple, predictable solutions"
guidance argues against.

### Data & architecture

- **Reused as-is (zero changes):** `AccountItemIndex` (+ its already-passing
  tests), `AccountItemIndex.GetPrioritizedSources`, `AccountSnapshot` /
  `SnapshotItemEntry` / `SnapshotWalletEntry`, `SnapshotStore`,
  `Gw2AccountSnapshotService`, Module's existing `_snapshotDirty` /
  `SetSnapshot` / `SetStatus` drain mechanism (unchanged shape - the fix
  below adds one more call into it, not a new mechanism).
- **New, inside `MainView.cs`:**
  - A `TextBox` (search) + a 4-`Checkbox` row (source filter). Both wired
    through the exact same `RebuildContent()`-on-change idiom the file
    already uses for its `Dropdown`/`Checkbox`.
  - A private `AccountItemIndex _accountItemIndex` field, rebuilt every
    place `_snapshot` is assigned (the constructor *and* `SetSnapshot` -
    the constructor currently sets `_snapshot` directly, bypassing
    `SetSnapshot`, so the index needs its own build call there too, not
    just inside `SetSnapshot`). `AccountItemIndex`'s constructor already
    tolerates a `null` items list, so no extra null-guarding is needed at
    the call site.
  - Private instance fields `_lastSearchText` / `_checkedSources` (a small
    `HashSet<string>` or four bools), written from the new controls'
    change handlers and **read back when `Build()` reconstructs them**, so
    the search/filter state feels sticky across tab switches despite every
    control being disposed and recreated on each visit (see the
    cross-cutting finding above - this is the concrete application of it).
  - Row rendering for Items switches from "one row per `SnapshotItemEntry`"
    to "one row per matching itemId, built by intersecting
    `_accountItemIndex.GetSources(itemId)` with the currently-checked
    source set" - a cheap `HashSet` intersection per matching item, not a
    new indexing structure.
- **Staleness label:** computed inline wherever the status text is already
  set (`Build()`'s initial render, `SetSnapshot`, `SetStatus`) from
  `_snapshot.CapturedAt` vs `DateTime.Now` - no timer, no `FrameTicker`
  (see cross-cutting note), because `Build()` already reruns with fresh
  data on every tab visit and `SetStatus`/`SetSnapshot` already run on
  every genuine data change. The exact "stale" threshold should read
  Feature 3's new setting rather than a second hardcoded literal.
- **WP-21/22 sequencing:** `MainView`'s `AddCoinSegment`/`GetCoinColor`
  (lines ~406-447 today) are, per WP-22's own text, a byte-identical
  duplicate of code WP-21 is extracting into `Views/Rendering/
  CoinCurrencyRenderer`. This redesign should call the post-WP-21
  `CoinCurrencyRenderer` (left-anchored mode, exactly as WP-22 specifies)
  instead of re-touching or re-duplicating the private methods. If this
  proposal's implementation lands **before** WP-21/22 merge, keep today's
  private methods untouched with a `// TODO(WP-22): repoint to
  CoinCurrencyRenderer` comment rather than blocking on unrelated M38
  scheduling - see Dependencies below.
- **Threading:** no change to any cross-thread surface. `SetSnapshot`/
  `SetStatus` remain the only entry points touched off the dirty-flag
  drain, exactly as today; the new controls are pure main-thread UI with no
  awaits of their own.

#### The disk-restored-snapshot bug (confirmed; fix required as a companion change)

The brief asked this design to "subsume" a known gap: a disk-restored
snapshot not reaching the tab display until a refresh. I traced this in
`Module.cs` rather than take it on faith:

```csharp
protected override async Task LoadAsync()
{
    _currentSnapshot = _snapshotStore.LoadLatest();   // <-- loads from disk
    Gw2ApiManager.SubtokenUpdated += OnSubtokenUpdated;
    if (_snapshotService.HasRequiredPermissions())
    {
        await RefreshSnapshotInBackgroundAsync();      // <-- only path that ever sets _snapshotDirty
    }
}
```

`_snapshotContent` (the `MainView` instance) was already constructed back in
`Initialize()`, with whatever `_currentSnapshot` was *at that point* (always
`null` - `Initialize()` runs before `LoadAsync()`). The only two things that
ever call `_snapshotContent.SetSnapshot(...)` are `Update()`'s
`_snapshotDirty` drain, and `MainView`'s own `SetSnapshot(null)` inside its
Clear Cache handler. `LoadAsync`'s disk-load line **never sets
`_pendingSnapshot`/`_snapshotDirty`**, so the disk-loaded value sits in the
`_currentSnapshot` field, fully valid, and is simply never pushed to the
view - the tab shows the "No snapshot available" empty state - until a real
network refresh completes (either the one this same `LoadAsync` may await
if `HasRequiredPermissions()` is already true, or a later background/manual
one). If permissions are not ready yet at `LoadAsync` time, there is no
guaranteed bound on how long that empty state persists, even though good
(possibly slightly stale) data already exists on disk.

**Proposed fix** (minimal, reuses the existing mechanism verbatim, no new
one): immediately after the `LoadLatest()` call, when the result is
non-null, set `_pendingSnapshot = _currentSnapshot; _snapshotDirty = true;`
- the exact same two fields `FetchAndSaveSnapshotAsync`'s commit closure
already sets on a successful network fetch. `Update()`'s existing drain
picks it up on the very next tick, completely decoupled from whether a
network refresh ever succeeds. This is a two-line addition to `Module.cs`,
not a new mechanism, and directly makes "show possibly-stale-but-real data
immediately, refresh in the background" the actual behavior - which is
exactly what the new staleness indicator above is designed to communicate
honestly to the user.

This fix sits inside `LoadAsync`, a few lines above the epoch-guard-adjacent
`FetchAndSaveSnapshotAsync`/`SnapshotCommitGate` machinery that KNOWN-ISSUES
#31 (M37) hardened against a Clear-Cache race. The fix itself cannot race
Clear Cache (the window is not yet interactive at `LoadAsync` time), but
whoever implements this should re-read `SnapshotCommitGate`'s own doc
comment before touching this method, since it is adjacent to a documented
race-sensitive area, not because the two-line fix itself needs the gate.

### Settings introduced

None owned directly by this feature. The staleness threshold reuses Feature
3's proposed `SnapshotRefreshIntervalMinutes` (cross-referenced there, not
duplicated as a second setting here).

### Invariant/contract impacts

- **Coin icons right of numbers:** unaffected - unchanged today, and
  `CoinCurrencyRenderer` (post-WP-21/22) is documented as preserving the
  same invariant in one shared place.
- **No raw IDs displayed:** confirmed safe. `AccountItemIndex` is keyed
  internally by `itemId`, but every rendered row still reads `entry.Name`/
  source display strings, never the id itself, matching the file's own
  existing `// Never display raw item IDs (repo invariant)` comments.
- **Tests must stay Blish-free:** `AccountItemIndex` is already exercised by
  a real, Blish-free test file; nothing in this feature adds a new
  service/store that would need its own new test file. `MainView.cs` itself
  remains untestable by the repo's own convention (Blish-coupled View
  class, same as today - no regression, no new gap).
- **M33 layout contract (`PlanContentHeightMath`/relayout registry):** not
  engaged at all. This tab stays on the lightweight Pattern A
  (`FlowPanel(CanScroll)`, dispose+rebuild on every filter/search change) -
  exactly what it already does today, just triggered by one more control.
  No relayout-closure registry, no `PlanRelayoutMath`, no
  `WheelDeltaSanitizer` involvement - zero M33/WP-21..26 blast radius.

**Explicit perf note** (required by the review checklist for anything
touching a rebuild-on-every-keystroke path): rebuilding rows on every
keystroke is the *existing* dispose+rebuild idiom (Pattern A), just
triggered more often than a `Dropdown`/`Checkbox` change would be. The
total row count is bounded by the account's own item-entry count, which is
itself bounded by GW2's own fixed storage-slot limits (bank/material
storage/shared inventory/character bag slots are all capped by the game
itself - **INFERRED**, I did not verify the exact numeric caps against a
live API doc this session). A per-keystroke `O(n)` substring scan plus an
`O(k)` row rebuild (`k` = the filtered subset, typically far smaller than
`n`) is well inside a single frame's budget - the same order of work
`CraftableItemSearchProvider` already performs synchronously and instantly
today, over a data set (the whole GW2 item-name catalog) an order of
magnitude larger than any one account's snapshot. No debounce is proposed;
it is called out as a tunable open question only if a live test on a
maxed-out multi-character account later shows visible jank.

### Effort class

**M (Medium).** No new service, store, or persisted setting of its own; it
reuses `AccountItemIndex`/`GetPrioritizedSources` verbatim and stays on the
existing lightweight scroll pattern. It is not S because it touches a
confirmed cross-cutting bug in `Module.cs` (adjacent to documented
race-sensitive code, even though the fix itself is small) and changes a
keystroke-frequency rebuild path that the review checklist requires an
explicit perf note for.

### Dependencies & sequencing

- Independent of the Log / Plan History / Crafting Ranker proposals.
- Loosely coupled to **WP-21/22**: can ship before them (temporary
  `// TODO(WP-22)` marker on the untouched private coin methods) or after
  (clean call-through to `CoinCurrencyRenderer`) - does not need to block on
  M38's own Wave F scheduling.
- The `Module.LoadAsync` fix touches the same file/area WP-17 is also
  touching (`Module.cs` catch-consistency + `Unload` teardown) - not a
  logical conflict (different lines, different concern), but both should be
  reviewed together if they land in overlapping timeframes to avoid a
  needless merge conflict in a small file.
- Should land together with (or before) Feature 3's
  `SnapshotRefreshIntervalMinutes` setting, since the staleness label ideally
  reads that same value rather than a second hardcoded literal.

### Open questions

1. Single combined "Characters" checkbox (this proposal's choice) versus a
   per-character checkbox row (more granular, more UI, more state to keep
   sticky) - a real UX tradeoff, not resolved here.
2. Should the search box also match against source/character labels (e.g.
   typing a character's name surfaces everything on that character), or
   stay scoped to item/currency names only (this proposal's choice)?
3. Exact GW2 structural slot-count caps (flagged INFERRED above) - worth an
   implementer double-checking if the "no debounce needed" perf conclusion
   is to be trusted on the largest realistic accounts.
4. Does the maintainer actually want session-sticky search/filter state, or
   would a clean reset to "show everything" on every tab visit be
   preferred? This proposal defaults to sticky because it is nearly free
   given the cross-cutting finding above, but it is a real preference call.

---

## Feature 2: About tab

### Problem/intent

User directive (verbatim): *"information on the app itself - version,
project source location, author details."* Brief additionally asks for
licenses/attributions (Blish HUD, GW2 API terms), a "built against Blish
1.3.0" note, and diagnostics info (module data directory).

Today (MEASURED, `Module.cs` line ~409): the About tab renders
`Module.BuildPlaceholder` - a single gray "Coming Soon" label. No view class
exists.

### Proposed UX

A new, short, static-content tab (no controls to interact with beyond
plain selectable text) - closer in spirit and size to `LogTabContent.cs`
(74 lines) than to `SettingsTabContent.cs`:

```
+----------------------------------------------------------------------+
| [icon] GW2 Crafting Helper                            v0.1.0         |
| Caches and displays GW2 account inventory, wallet, and material      |
| storage data.                                                        |
|                                                                       |
| Source:      https://github.com/<org>/GW2CraftingHelper  (copyable)  |
| Author:      <from manifest contributors>                            |
| Built with:  Blish HUD 1.3.0 - https://blish-hud.com                 |
|                                                                       |
| --- Licenses & Attributions ---                                      |
| Built on Blish HUD (MIT License) - github.com/blish-hud/Blish-HUD    |
| Not affiliated with, endorsed, sponsored, or approved by ArenaNet or |
| NCSOFT. Guild Wars 2 content and materials are trademarks and        |
| copyrights of ArenaNet, NCSOFT, or their licensors.  [see note below]|
|                                                                       |
| --- Diagnostics ---                                                  |
| Module data directory: <DirectoriesManager path>          (copyable) |
+----------------------------------------------------------------------+
```

- Header: module icon (`Module._moduleIconTexture`, already loaded from
  `icon.png` in `Initialize()` - just needs passing to the new view),
  name, version.
- Description: manifest description text, shown as-is.
- Source / data-directory rows render as **plain, selectable/copyable
  text** (e.g. a read-only `TextBox`, matching the pattern this codebase
  already uses wherever a value needs to be copy-pasteable), **not** a
  click-to-launch-browser button. See the explicit reasoning and open
  question below - I found no precedent anywhere in this codebase, and no
  confirmed Blish HUD API, for launching an external process from inside
  the overlay, and I am not willing to invent that behavior without
  evidence it is safe inside GW2's overlay sandbox.
- Author/contributor row: whatever the manifest's `contributors` list says,
  read live, not hardcoded - see the branding note below.
- "Built with Blish HUD 1.3.0" row: a static string for v1 (see Data &
  architecture - the dynamic alternative is possible but unverified).
- Licenses & Attributions: two short lines (Blish HUD's MIT credit, and a
  GW2-API/ArenaNet fan-content disclaimer) - **both need the maintainer's
  sign-off on exact wording before shipping**, flagged explicitly below,
  not silently assumed correct.
- Diagnostics: the module's own data directory path
  (`DirectoriesManager.GetFullDirectoryPath("data")`), useful for a user
  filing a bug report who needs to attach `snapshot.json`/`status.json`/etc.

### Data & architecture

- **New:** `Views/AboutTabContent.cs`. Same shape as `LogTabContent.cs`:
  one `FlowPanel(CanScroll = true)`, `Build(Container)` populates it once,
  no relayout registry, no M33 involvement. `Module.cs` wires it exactly
  like `SettingsTabContent` (`_aboutContent = new AboutTabContent(...)`;
  tab registration swaps `BuildPlaceholder` for
  `c => _aboutContent.Build(c)`).
- **Manifest reachability - MEASURED this session, going one step further
  than the scout notes:** I could not find a real `Blish HUD.exe` inside
  this repo's own NuGet package (`packages/BlishHUD.1.3.0/lib/net472/` ships
  only the XML doc and satellite resource DLLs, not the main assembly), but
  I located a real installed copy of the same 1.3.0 assembly elsewhere on
  this machine
  (`~/.nuget/packages/blishhud/1.3.0/lib/net472/Blish HUD.exe`) and ran
  `strings` against it directly:

  ```
  get_Author  get_Contributors  get_Description  get_Manifest
  get_Name    get_Package       get_Url           get_Version
  get_Dependencies  get_Directories
  ```

  All nine compiled property accessors exist somewhere in the real shipped
  1.3.0 assembly - this **independently reconfirms** the scout's claim
  rather than just repeating it. What I could **not** confirm without full
  IL disassembly is that they hang together as
  `ModuleParameters.Manifest.{Name,Version,Url,Description,Author,
  Contributors,Package,Dependencies}` in exactly that shape (**INFERRED**,
  strong but not proven). Recommend the same one-line `Logger.Info` smoke
  test the scout already flagged, as the literal first step of
  implementation, before writing the rest of the tab around it.
- **Fallback (removes the risk of the above being wrong):** if any Manifest
  read throws, returns null, or is empty, fall back to hand-parsing the
  module's own `manifest.json` via
  `ContentsManager.GetFileStream("manifest.json")` + a tiny Newtonsoft DTO -
  this is the exact try/catch-with-graceful-fallback shape already used
  four times in `Module.Initialize()` for seed files
  (`ItemNameSeedData.Load`, `AcquisitionHintService.Load`, recipe seed/
  manifest loads). This makes the "verify Manifest first" step a confidence
  booster, not a hard blocker - the tab can never end up blank or crash
  either way.
- **"Built against Blish HUD 1.3.0":** two options.
  - (a) Static string literal, manually updated when `manifest.json`'s own
    `dependencies.bh.blishhud` bumps. Zero runtime risk, and that manifest
    field changes rarely enough that keeping a doc-only line in sync by
    hand next to it is a trivial maintenance cost. **Recommended for v1.**
  - (b) Read `Manifest.Dependencies` live and format it (confirmed
    `get_Dependencies` exists in the real assembly, per above) - the exact
    return shape is unverified without IL inspection; a stretch goal, not
    a blocker.
- **Data directory:** `Module.Initialize()` already computes
  `DirectoriesManager.GetFullDirectoryPath("data")` into a local `dataDir`
  variable - it just needs promoting to a field (or passed straight into
  `AboutTabContent`'s constructor at the point it's already in scope).
  Trivial plumbing, one line.
- **Branding (author/contributor):** read the manifest's `contributors`
  list live, at runtime, every time the tab builds - **never** hardcode
  either "MaximusCub" or "Lachlan Mulcahy" in this new file. WP-28 owns
  reconciling which name is authoritative; a live read means this tab
  reflects that decision automatically, with zero further code change,
  the moment `manifest.json` is updated. This proposal takes no position
  on which name is correct.
- **Licenses & Attributions text - both lines need explicit sign-off,
  not silent assumption:**
  - *Blish HUD:* confirmed **MEASURED via WebSearch** this session that
    `blish-hud/Blish-HUD` is MIT licensed (its own GitHub `LICENSE` file).
    A short credit line + link ("Built on Blish HUD (MIT License) -
    github.com/blish-hud/Blish-HUD") is the common pattern for MIT
    attribution in an About screen; I did not attempt to reproduce the
    full MIT license text inline, and whether a short credit line
    satisfies the maintainer's own bar (as opposed to bundling the full
    license text) is a judgment call for them, not something I resolved.
  - *GW2 API / ArenaNet content disclaimer:* I attempted to fetch
    ArenaNet's own Content Terms of Use
    (`arena.net/en/legal/content-terms-of-use`) and the GW2 wiki's API
    Terms of Use page directly this session; neither fetch returned
    usable rendered text (both appear to be JS-rendered pages that did not
    convert to readable content through automated fetch). The line shown
    in the wireframe above is the **INFERRED**, widely-used community-
    convention phrasing seen across the GW2 fan-tool ecosystem (paraphrase,
    not a verified quote of ArenaNet's current required wording) - it is
    explicitly a draft, not something to ship as-is. This repo's own "do
    not invent data" posture argues for treating exact current wording as
    a required verification step (a maintainer or a follow-up research
    session with better access to those legal pages), not something this
    proposal resolves.
- **Threading:** none. Pure static/derived text, read once at `Build()`
  time, no awaits, no cross-thread surface.

### Settings introduced

None. Everything About shows is either derived (manifest fields, data-dir
path) or static text - nothing here is a user preference worth persisting.

### Invariant/contract impacts

- **No raw IDs:** all manifest fields are already human-facing strings
  (name/version/url/description/author) - no risk.
- **ASCII-only source:** the new file itself will be plain ASCII; the one
  thing to watch is if `manifest.json`'s own `description` field ever grows
  a non-ASCII character (it is plain ASCII today, MEASURED) - a defensive
  note, not a current problem.
- **Tests must stay Blish-free:** `AboutTabContent` is Blish-coupled
  (reads `ModuleParameters`, builds `Control`s) exactly like every other
  View class in this repo - no unit test is possible or expected, matching
  existing precedent (`MainView`/`CraftingPlanView`/`SettingsTabContent`/
  `LogTabContent` have none either).

### Effort class

**S (Small).** No new service, store, setting, or threading surface; a
single new View class the size of `LogTabContent.cs`, built from data the
module either already loads or can read from an already-idiomatic
fallback path. The one real risk (the Manifest-property assumption) is
cheap to de-risk with a smoke test and has a safe fallback either way, so
it does not push this into Medium.

### Dependencies & sequencing

- Best sequenced after (or coordinated with) **WP-27/28**: if About ships
  first using the hand-parsed-`manifest.json` fallback path and WP-27/28
  later restructures docs/branding, only static fallback strings need
  touching up - the live Manifest-reading happy path is unaffected by
  either WP.
- No collision with `CraftingPlanView.cs` or the M33 contract at all -
  fully independent new file.

### Open questions

1. Confirm `ModuleParameters.Manifest`'s exact reachable shape via a live
   smoke test before building the rest of the tab around it (the specific,
   concrete first step this proposal recommends).
2. If the dynamic "Built against Blish X.Y.Z" stretch goal is wanted,
   confirm `Manifest.Dependencies`'s actual return shape.
3. **Get the exact, current ArenaNet-required disclaimer wording verified**
   against ArenaNet's own legal pages - this proposal's draft text is
   explicitly a starting point, not a verified quote.
4. Does the maintainer want a clickable "open in browser" affordance for
   the source URL at all, contingent on confirming `Process.Start` (or a
   Blish-provided equivalent, if one exists and I simply didn't find it) is
   safe and functional from inside the GW2 overlay?
5. Which branding (MaximusCub vs. Lachlan Mulcahy) is authoritative - this
   is explicitly WP-28's call, not this proposal's; the About tab is
   designed to reflect whatever that decision ends up being with zero
   further code change.

---

## Feature 3: Settings tab - audit and additions

### Problem/intent

User directive (verbatim): *"any useful settings that a user might need."*
This is framed by the brief as an audit task first, additions second, with
an explicit warning against settings soup.

### Audit of the existing settings surface (MEASURED, `ModuleSettings.cs` + `SettingsTabContent.cs`)

| Setting | Type | Surfaced in Settings tab UI? | Control idiom used | Verdict |
|---|---|---|---|---|
| `ModalDialogX`/`ModalDialogY` | `int` x2 | No | - | Correctly not exposed - internal window-position bookkeeping for `ModalDialog`, not a user preference. No action. |
| `CurrencyValuationsJson` | `string` (JSON) | Yes | TextBox + shared Save button + per-row error label | Working as designed (6 curated currencies). |
| `ValueOwnMaterials` | `bool` | Yes | Checkbox, immediate-apply | Working as designed. |
| `HomesteadFiberTier`/`MetalTier`/`WoodTier` | `int` (0-2) x3 | Yes | TextBox + shared Save button + per-row error label | M37, working as designed. |
| `ScrollDiagnosticsEnabled` | `bool` | **No** - flip-only via hand-edited settings JSON, by its own doc comment | none | **Audit finding** - candidate to surface, see below. |
| Price Basis (Instant Buy / Buy Orders) | *(lives per-plan inside `CraftingPlanView`, not `ModuleSettings` at all)* | N/A | N/A | Confirmed deliberate - `SettingsTabContent`'s own "Plan Defaults" section already says so via an info line. A *module-level default* is a different, separate candidate - see rejected list below. |
| Own-Materials master toggle (the per-plan checkbox, distinct from `ValueOwnMaterials`) | *(`CraftingPlanView`-local)* | N/A | N/A | Same as above. |

### Proposed additions (accepted)

1. **`SnapshotRefreshIntervalMinutes`** (`int`, default `10`, clamped
   `1`-`120`) - replaces `Module.cs`'s hardcoded
   `private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(10);`
   constant. Same TextBox+Save+error-label idiom as the Homestead tier
   rows; a new `SettingsInputParser.TryParseRefreshMinutes`-style validator
   (mirrors `TryParseTier`'s exact shape, just a wider range). This is the
   one setting Feature 1 explicitly wants to exist, so its staleness label
   and Module's own auto-refresh trigger read the *same* number and can
   never silently disagree about what "stale" means.
2. **Surface `ScrollDiagnosticsEnabled`** as a real Checkbox (immediate-
   apply, the exact `ValueOwnMaterials` idiom, copy-paste shape). The
   setting already exists and is documented as instrumentation-only
   ("never changes scroll/guard/restore behavior") - the only gap is
   discoverability. A user asked by the maintainer to help diagnose a
   scroll-jank report currently has to be walked through hand-editing the
   persisted settings JSON; that is a real, avoidable support-friction
   problem given the setting is otherwise safe to expose.

### Considered and explicitly REJECTED

1. **Default price basis / default target quantity / tree default
   expansion depth** (the brief's own named examples). **Rejected for this
   proposal** - not because the ideas are bad, but because consuming any of
   them requires a read inside `CraftingPlanView.cs`, which the brief
   itself and the M38 plan both mark out of scope (WP-25's tree-controller
   extraction is explicitly classified high-risk in the M38 plan; even a
   small new default-quantity/default-expansion-depth read is still an
   edit to a 4812-line file under active decomposition). Deferred to a
   follow-up proposal scoped explicitly against `CraftingPlanView`, ideally
   after WP-21/23/25 land and the file is smaller and safer to touch.
2. **A "Clear Cache" button inside the Settings tab.** **Rejected.** The
   action already lives exactly where it belongs - the Snapshot tab
   header, next to the data it clears. Duplicating it into Settings would
   add a second button with identical behavior and zero new value: textbook
   settings-soup.
3. **A "search scope" toggle for the new Snapshot search box** (e.g. "names
   only" vs. some second mode). **Rejected.** There is no second
   searchable field in this proposal's scope (source/character labels are
   deliberately not searched, per Feature 1's open question 2) - a toggle
   with only one meaningful position is not a setting, it's a bug waiting
   to happen.
4. **Persisting the Snapshot source-filter checkboxes across module
   restarts** (as opposed to just across tab switches within one session,
   which Feature 1 already covers for free via instance fields).
   **Rejected for v1.** In-session stickiness already covers the common
   case at zero persisted-setting cost; persisting across restarts would
   add 4-5 new `SettingEntry<bool>` entries for a preference most users
   likely never touch (the safe, useful default - "show everything" - is
   already what a fresh module load gives you). Revisit only on actual
   user feedback asking for it.
5. **A Log tab retention/level/size-cap setting.** **Explicitly not
   proposed here** - that setting, if any, belongs to whichever proposal
   designs the Log tab redesign (out of this document's assigned scope).
   Flagged only as a sequencing note: `SettingsTabContent.Build()` is a
   flat sequence of `BuildXSection(panelWidth)` calls, so a future
   `BuildLoggingSection()` slots in with zero risk of colliding with
   anything proposed here, provided its section title doesn't collide with
   the new "Snapshot" section this proposal adds.
6. **A visible "Reset all settings to defaults" button.** **Considered,
   rejected.** `ModuleSettings.ResetToDefaults()` already exists in the
   codebase (MEASURED) but is called from **no View and no test anywhere**
   in the main tree today - it is dead code as of this session's read.
   Wiring a button to it is straightforward, but Blish HUD's own generic
   per-module settings screen already exposes a module-level reset/
   uninstall path at the platform level; duplicating that inside this
   module's own Settings tab is redundant scope, not a "useful setting a
   user might need." (The dead-`ResetToDefaults()` observation itself is
   noted here for completeness, not as something this proposal asks
   anyone to fix.)

### Data & architecture (for the two accepted additions)

- `SnapshotRefreshIntervalMinutes`: one new `SettingEntry<int>` in
  `ModuleSettings.cs`, same `DefineSetting` call shape as
  `HomesteadFiberTier`. Consumed by `Module.cs`'s `Update()` staleness
  check (replacing the `StaleThreshold` constant read) *and* by Feature
  1's staleness label - both reading the same value is the entire point.
  New validator in `SettingsInputParser` (same shape as `TryParseTier`,
  wider range), one new TextBox+Save row in `SettingsTabContent` - proposed
  as its own small new "Snapshot" section (matching the file's existing
  one-section-per-feature layout), rather than folding it into the
  existing "Plan Defaults" section, which is about per-plan choices, a
  different concern.
- `ScrollDiagnosticsEnabled` surfacing: zero new fields - the setting
  already exists. Just one new Checkbox row wired directly to
  `_settings.ScrollDiagnosticsEnabled`, an exact copy of
  `AddValueOwnMaterialsRow`'s shape.
- **Threading:** both are plain main-thread settings changes.
  `SnapshotRefreshIntervalMinutes` is only ever *read* from `Update()`
  (already main-thread) - no new cross-thread surface at all.

### Settings introduced

- `SnapshotRefreshIntervalMinutes` (`int`, default `10`, valid range
  `1`-`120`) - **new**.
- `ScrollDiagnosticsEnabled` already exists; this proposal adds only its
  UI checkbox, not a new `SettingEntry`.

### Invariant/contract impacts

- **No raw IDs involved.**
- **Persistence pattern:** both ride the existing plain `SettingEntry<T>` /
  `SettingCollection.DefineSetting` mechanism - zero new persistence
  surface, so **WP-16's onError-callback work is entirely unaffected**
  (that work targets the four file-backed *stores*; Blish's own
  `SettingCollection` handles its own persistence and failure path outside
  this module's control).
- **Clamping precedent:** `ModuleSettings.ClampTier`'s existing contract -
  "a corrupt/out-of-range persisted value is clamped, never thrown, so a
  hand-edited settings file can never crash `Update()`" - must apply
  identically to the new interval setting. A hand-edited `0` or `99999`
  must clamp to `1`/`120` respectively *before* it ever reaches the
  staleness comparison in `Update()`, not merely be rejected by the
  Settings tab's own input validation (which only guards the UI path, not
  a hand-edited settings file on disk).

### Effort class

**S (Small).** One new `int` setting plus its validator and one new
Settings-tab row, one new UI row for an already-existing setting, and a
small constant-to-setting swap in `Module.cs`. No new service, no new
threading, no new persistence mechanism.

### Dependencies & sequencing

- `SnapshotRefreshIntervalMinutes` should land together with (or ahead of)
  Feature 1's staleness indicator, for the reason stated repeatedly above
  (one shared threshold, not two that can drift).
- No M38 collisions - neither `SettingsTabContent.cs` nor
  `ModuleSettings.cs` appears in any WP package's scope in the plan I read.

### Open questions

1. Section grouping/title for the new setting - a new standalone
   "Snapshot" section (this proposal's lean) vs. folding it elsewhere - a
   naming/taste call for the maintainer.
2. Should the newly-visible `ScrollDiagnosticsEnabled` checkbox get its own
   "Diagnostics" section (potentially shared later with whatever the Log
   tab proposal adds), or stay a single standalone row for now? A
   coordination point with whoever designs the Log tab, not something this
   document can resolve alone.
3. Is `1`-`120` minutes the right clamp range for the refresh interval?
   Chosen as a reasonable-looking bound with no real usage data behind it
   - flagged rather than asserted as correct.

---

## Appendix: files read this session (grounding for MEASURED claims above)

`Module.cs` (full), `Views/MainView.cs` (full), `Views/SettingsTabContent.cs`
(full), `Views/SuggestionPanel.cs` (full), `Views/AutocompleteTextBox.cs`
(full), `Views/ViewAdapter.cs` (full), `Services/ModuleSettings.cs` (full),
`Services/AccountItemIndex.cs` (full), `Services/AccountCurrencyIndex.cs`
(full), `Services/SnapshotStore.cs` (full), `Services/SettingsInputParser.cs`
(full), `Services/CraftableItemSearchProvider.cs` (full),
`Services/ItemSearchProviderFactory.cs` (full),
`Contracts/IItemSearchProvider.cs` (full), `manifest.json` (full),
`docs/KNOWN-ISSUES.md` item 31 (M37 concurrency/degradation audits),
`tests/GW2CraftingHelper.Tests/Services/AccountItemIndexTests.cs` (excerpt),
`/mnt/c/Dev/Blish/m38-plan/m38-cleanup-plan.md` WP-16/17/21/22/27/28
sections, `Views/CraftingPlanView.cs` (targeted greps for `FrameTicker`),
plus a repo-wide grep for `ResetToDefaults` (main tree only - worktrees
under `.claude/worktrees` excluded from consideration per this session's
constraints).

External, dev-time-only research (never called at runtime by the module):
`blish-hud/Blish-HUD` GitHub repo (`TabbedWindow2.cs` source, `dev` branch,
fetched via `gh api`, MIT licensed) to settle the tab-view-caching question;
`strings` run directly against a real installed BlishHUD 1.3.0 assembly
(`~/.nuget/packages/blishhud/1.3.0/lib/net472/Blish HUD.exe`) to independently
reconfirm the Manifest-property claim; WebSearch for Blish HUD's license and
for GW2 API/ArenaNet disclaimer conventions (inconclusive on exact required
wording - see Feature 2's explicit open question).
