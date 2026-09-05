> **Frozen record - 2026-08-22, branch `audit-j-consistency`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Audit batch J: consistency sweep (audit-j-consistency)

The last audit batch, deliberately: every item here is a vocabulary or
chrome decision, and running it last let it adopt the words batches
A-K had already settled rather than inventing a tenth spelling that
would then have to be swept again. Each finding was re-located against
this HEAD before it was touched - the audit's own line numbers are
stale and three of its claims no longer held.

### Audit findings

- **M9, quantity notation - DONE.** The Snapshot tab spelled a quantity
  three ways: the recipe tree's prefix ("47x Mystic Clover"), the item
  row's suffix ("Mystic Clover x30") and the wallet row's colon
  ("Spirit Shards: 50"). All three are the tree's prefix form now.
  Two exemptions, both because the number is not counting the thing
  the label names: a tabular Amount column, whose header already
  labels its bare numbers, and the per-source breakdown line under an
  item, whose labels are LOCATIONS - "20x Bank" parses as twenty banks
  and "10x Character: Maximus Test" collides the multiplier with the
  label's own colon, so that line keeps "Bank 20   Character: Maximus
  Test 10" (fix round 1; the sweep had reached it). The wallet row
  keeps its thousands separator, since balances run to seven figures
  where an item count does not.
- **M10, status lines - DONE.** `StatusText.Stamp(verb, when)` is now
  the only place a "&lt;verb&gt; &lt;separator&gt; &lt;timestamp&gt;"
  line is composed; MainView's cache-cleared/updated/failed lines,
  SettingsTabContent's "Saved", CraftingPlanView's "Plan generated" and
  the restored-plan seed all call it. Separator is the em-dash (the
  majority spelling; the hyphen was already in use as the module's
  WITHIN-clause separator, so reusing it gave one line two identical
  separators at two grammatical levels), and `ForRefreshFailure`'s cause
  clause moved to a colon for the same reason.
  The Snapshot line's two-times-read-as-one confusion is fixed by
  `ForSnapshotAgeSuffix`: "(snapshot 29d old)" instead of a bare
  "(29d ago)" straight after an absolute timestamp. It is now the
  module's ONLY age wording: the older `ForSnapshotAge` was left
  standing with no caller and nine tests holding it up, one of them a
  Theory that only mirrored the two formatters against each other -
  the contract-mirror shape the repo invariant forbids. Deleted in fix
  round 1, with the bucket coverage retargeted onto
  `ForSnapshotAgeSuffix` as literal boundary assertions. Placement was
  NOT touched: which row a status lives in was settled by batches
  F/I/G.
- **M12, label vocabulary - DONE**, all four parts. Placeholders are one
  "Search {scope}..." shape (the Log tab's bare "Search..." names its
  scope; Settings' "Filter currencies..." was the lone "Filter"
  spelling). The Log toolbar is textbox-then-dropdown, matching the
  Snapshot search row it used to mirror; nothing else on the row moved,
  since the Follow checkbox's offset is the same sum. "Clear view" ->
  "Clear View", the only sentence-case button label in a module of Title
  Case ones.
  The per-currency "Clear" checkbox named an ACTION it does not perform
  - it is a persistent flag that suppresses the curated default
  estimate, not a button that empties the box beside it. It reads
  "Ignore" now, with the tag slot showing "ignored" and the tooltip and
  section info line reworded. NOT the longer "Ignore default": the
  cell's total extent is what decides whether the currency grid gets two
  columns at the 930px window minimum (2 * MinColumnWidth = 848 against
  an 864px settings panel), so the four extra pixels came out of the
  input-to-checkbox gap instead, leaving MinColumnWidth untouched - now
  pinned by a test asserting two columns at that minimum.
- **L1, plan empty state - DONE.** A dim centered "No plan yet. Search
  for an item above, then click Generate Plan." replaces the blank
  parchment. It is an ordinary child of the content panel, so the first
  real render sweeps it through `ResetContentPanelToEmpty` rather than a
  second disposal path that could drift; the gap above it is a spacer
  Panel because the content panel is a SingleTopToBottom FlowPanel that
  positions its own children. `ShowEmptyPlanState` resets the content
  panel first, which is load-bearing rather than defensive: it registers
  a relayout closure and `_relayoutActions` is cleared only there, so a
  no-plan tab visit would otherwise leave the previous visit's closures
  writing `Size` into controls that visit had already disposed. Also
  shown after a rolled-back plan render, which used to leave the tab
  blank.
  Two fix-round-1 corrections. It is shown only when the status board
  reports nothing in flight: a solver started before a tab switch is
  still running on the way back, and "No plan yet... click Generate
  Plan." beside a "Generating..." strip told the user to do the thing
  already happening. And its own relayout closure was dead - both
  `ReplayRelayout` call sites were gated on `_currentPlan != null`,
  which is exactly when the empty state does NOT exist, so a no-plan
  tab dragged narrower kept the label centered on the build-time width
  and overflowed the panel. The per-tick replay dropped that gate
  (`ReplayRelayout` already no-ops on an empty registry); the settle
  ticker keeps it, since every job in that pass is about rendered plan
  content.
- **L2, counts - DONE; L2, Used Materials header - DONE (the audit's
  "batch H already did this" was wrong).** Rule adopted: ALL-COUNTABLE.
  Every section whose body is a list of like rows names how many,
  because that count is what a COLLAPSED header owes the reader (three
  rows or ninety?), and because the alternative - dropping the five
  counts that already existed - throws information away to buy
  tidiness. The Recipe Tree gains "(N)". Total Cost keeps none under
  the same rule, deliberately: its body is a fixed formula band plus an
  optional currency table, not a list, so any number in its header
  would be counting one of its parts.
  The tree's N is every node at every depth - what Expand All reveals -
  not the rows currently on screen, which would change under the reader
  on every caret click. It rides the existing whole-tree pre-scan
  (`TreeCostColumnMath.ScanColumns` gained a `NodeCount`, five new
  tests) rather than a second walk; the scan is simply hoisted above the
  header call it now feeds, and reads nothing the header produces.
  Used Materials was verified at this HEAD and had NO header row - batch
  H pulled its Amount column in beside the names but never named it. It
  has an Item/Amount header now, on the shared c-table renderer.
- **L3, chrome drift - DONE**, with the inventory re-taken at this HEAD
  rather than trusted from the audit. Three header styles across six
  tables: banded/Font14/white/26px on Required Recipes, Required
  Disciplines, the Recipe Tree (banded by batch D) and the Total Cost
  currency table; unbanded/Font12/#999999/22px on the Shopping List; and
  nothing at all on Used Materials.
  **The band wins**, and `Views/Rendering/TableHeaderStyle` owns the
  tokens all three builders read. Three grounds: it is what four of the
  five existing headers already do, so unifying the other way would
  rewrite the majority to match the minority; it is the more recent
  deliberate decision (batch D chose it AFTER the lighter treatment
  existed, which is why the audit's own suggestion of the Shopping
  List's style is superseded); and every data row in this module already
  carries a 2px divider and usually an icon, so an unbanded grey header
  reads as a faint first data row - which is the complaint. The cost is
  stated rather than hidden: the Shopping List's header grows four
  pixels and Used Materials gains one, and both are paid for in
  `PlanContentHeightMath.SectionBodyHeight` in the same commit, counted
  unconditionally exactly as the two c-tables already were. The new
  Shopping band is bounded by its own last column, the rule batch H's
  fix round established for every other band, and resolves to exactly
  the panel width whenever the columns are still pinned.
  Buttons: 30 (Snapshot's two), 28 (the Log tab's three, Save, Generate
  Plan) and 24 (the five tree actions - re-checked, they did move to the
  strip in batch E but kept their 24 - and the item row's +/- pair)
  become one `UiMetrics.ButtonHeight = 28` applied at the construction
  sites. 28 wins on button count, and is the height of the one input row
  a button already shares - the plan's item row, whose search and
  quantity boxes are both 28 beside its +/- pair. It is NOT the module's
  input height, and fix round 1 corrected the constant's doc comment
  which claimed it was: TextBoxes are 26 at nine of eleven sites and the
  two Dropdowns outside the plan tab are 30, so the Log toolbar still
  runs three input heights and a button on it does not share a baseline
  with the search box beside it. Bringing the inputs to 28 is a separate,
  unmade decision, recorded here rather than implied by the constant.
  The Snapshot pair's y is derived from the header height rather than
  rewritten; the tree toolbar's y already derived from its row height.
  Scope, corrected in verification round 1: the constant covers the ten
  buttons that live on a TAB. The four dialog footer buttons
  (`ModalDialog`'s confirm/cancel, `ApiAccessDialog`'s retry/close) are
  still 25 and were missing from the inventory above, so the constant's
  summary line - "every StandardButton in the module" - was false as
  written. They are left at 25 deliberately: each is hand-placed against
  a fixed window size, so changing its height moves it relative to a
  window edge rather than to a row of neighbours, which is a separate
  unmade decision and not verifiable without the live gate.
- **L5, missing wallet icon - DONE**, root cause first. `IconUrl` is
  empty for that entry. Live it comes from
  `Gw2AccountSnapshotService.ResolveCurrencyDetailsAsync`, which
  resolves the v2/currencies icon for every wallet row, so the captured
  hole is the seeded fixture
  `docs/dev-notes/m38-plan/m37-item29-snapshot.json`, which carries
  `"IconUrl": ""` for currency 23. The fixture is left as-is (inventing
  an icon URL would be inventing data), because the state is reachable
  live anyway whenever that currencies fetch fails or a currency is
  absent from its cache - so the fix is the general no-icon case, not
  one row. `IconControls` already degraded a missing icon to a neutral
  square instead of Blish's magenta missing-texture; the square just
  read as a HOLE. It now carries a dim centered ASCII mark and, when the
  caller supplied no tooltip of its own, "No icon available for this
  entry." - stamped on the mark as well as the square, since Blish
  resolves a tooltip on the deepest control under the cursor. Marking
  rather than collapsing the column: an un-iconed row whose text starts
  32px left of every other row's is the worse artifact, and the plan's
  tables anchor their name column to a fixed x a per-row collapse would
  break. Built only on the missing path.
- **L7, About wording - DONE.** "unknown" (version), "Not set in
  manifest.json" (source URL) and "Not listed in manifest.json" (author)
  all become the single `NotAvailableText` the data-directory row
  already used, "Not available". Two of the three named an
  implementation detail the reader cannot act on.

### Photography findings

- **P1, ModalDialog did not block background input - DONE.**
  `Views/ModalBackdrop` is a bare capturing `Control` raised beneath the
  dialog for its lifetime. Measured against BlishHUD 1.3.0, not assumed:
  `Container.TriggerMouseInput` walks children by ZIndex descending then
  sibling index descending and BREAKS on the first whose bounds hold the
  cursor and whose own `TriggerMouseInput` returns non-null - which
  `Control.TriggerMouseInput` does for anything carrying the Mouse or
  MouseWheel capture flag. `CaptureType.Filter` is the one flag that
  loop steps past, so the backdrop must not carry it. That is the entire
  mechanism; it paints nothing.
  **It covers the module window, not the screen.** A capturing control
  also stops Guild Wars 2 itself from seeing the click, and a confirm
  left open swallowing every click in the game is not a trade a HUD
  overlay should make for a two-button dialog. Other modules' windows
  and the game stay live.
  Z-order is not a constant - a window's ZIndex is
  `5 + Screen.WINDOW_BASEZINDEX + its rank among windows ordered by
  (TopMost, LastInteraction)` - so the backdrop tracks
  `dialog.ZIndex - 1` on every frame it is visible, and is constructed
  on the FIRST `Show()` rather than in the constructor so that on the
  tie that arithmetic can produce with a non-TopMost module window it is
  the later SpriteScreen child and wins the sibling-index tiebreak.
  Module hands the blocked surface over as a lambda because the module
  window is built after the dialog. Dropped on every exit path the
  dialog has - both buttons, `Hide()`, and the title-bar X / Escape
  route through `Dismiss` - before the callbacks run. ApiAccessDialog is
  deliberately NOT given one: it is an error dialog with Retry/Close,
  not a confirm gating destructive state.
- **P2, Snapshot breakdown hard-clip - DONE.** Both lines of an item row
  (and the wallet row) run through `LabelHelpers.EllipsizeToWidth`, and
  a shortened line carries the full text through the tooltip facility's
  plain path - stamped on the Label itself as well as the row Panel,
  because Blish resolves a tooltip on the deepest control under the
  cursor and does not bubble. A width change re-fits the rows in place -
  each row Panel takes the new width and each line is re-ellipsized
  against it, tooltip re-decided - so a widened window stops showing
  "..." on text that now fits; a height-only drag arms nothing.
  Fix round 1 replaced the first attempt, which routed the resize
  through the EXISTING search debounce and claimed "a drag costs nothing
  per frame". It cost a CancellationTokenSource allocated, cancelled and
  disposed, plus a thrown-and-caught cancellation exception, on EVERY
  drag frame, on the UI thread's own event path - and its callback then
  disposed and recreated every row inside a scrolling FlowPanel,
  re-running the whole search and risking the scroll position, to change
  nothing but text. The trailing wait is now armed once per drag (later
  events only stamp the last-event time and the single pending waiter
  re-arms itself, the bounded shape the plan tab's settle ticker uses)
  and is gated on the width the rows were actually laid out at, so a
  drag ending where it started re-fits nothing. Build-time fit and
  resize re-fit share one rule, `FitRowTextLabel`.
- **P3, doubled log tag - DONE, at the root.** Two sinks with different
  shapes: `ModuleLogEntry` carries the tag as a FIELD, which
  `LogLineFormat` renders in the row's own prefix column, while Blish's
  `Logger` has no tag column and needs it inside the message. All
  fourteen call sites prepended the bracketed form to the message AND
  handed the same tag to ModuleLog. `LogScrollDiag` - the single method
  writing to both - now adds the bracketed form for Blish's Logger only.
  Class sweep over every `ModuleLog.Shared.Write` in the tree: no other
  site embeds its own tag in its message (the "[TypeName]" runs in
  Module.cs and RecipeClientFactory are exception type names, not tags),
  so this was the sole instance.
- **P4, ApiAccessDialog title/close-X collision - DONE.** Two changes,
  because either alone leaves no margin: the window is 560 wide (was
  480) and the title drops the word carrying none ("GW2 API access not
  ready"). Measured rather than guessed: `WindowBase2` draws the title
  in DefaultFont32 - the largest font in the toolkit, not the one a
  title this long was sized against - at a fixed 80px offset into the
  left title-bar texture, clipped to that texture's bounds, which stop
  2px short of the right section; the exit button sits 32px plus its own
  width inside that section's right edge. The title's budget therefore
  scales 1:1 with window width. Everything inside the dialog derives
  from `ContentWidth`, so the checklist simply wraps to fewer lines and
  the buttons re-center.
- **P5 - SKIPPED, already resolved by batch D.** Verified at this HEAD:
  `CreatePlanHeader` emits `" x {vm.TargetQuantity} needed"`, with a
  comment recording why it is "needed" rather than a bare count. No work
  done.

### Validation

Build 0 errors and the full suite green per commit. Suite 2168 baseline
-> 2192 after the batch, then -> 2186 after fix round 1 deleted the
caller-less age formatter's nine tests and retargeted its bucket
coverage onto `ForSnapshotAgeSuffix` (net +18 Blish-free tests over the
baseline: `StatusText`'s stamp and age suffix, 5 on the tree scan's node
count, 1 pinning two currency columns at the window minimum, and the
rest folded into the reworked `PlanContentHeightMath` header
assertions). No new test references Blish.

Height-math check: two renderer-emitted heights DO change in this batch,
and both are paid for in `PlanContentHeightMath.SectionBodyHeight` in
the same commit as the renderer - the Shopping List's header (22 -> the
shared 26) and Used Materials' new header (0 -> 26). Nothing else in the
batch moves a height: the empty-state label lives outside every
section's math, the button-height change is bounded by rows whose
heights are fixed constants, and the tree's node count is text.

### What the sandbox check should look at

1. **Modal really blocks:** open the Snapshot tab's Clear Cache confirm
   and click the Crafting Plan tab's "+" add-row button behind it, the
   tab strip, and the module window's own title bar. None may respond.
   Then click OUTSIDE the module window - the game and any other
   module's window must still respond. Cancel, and confirm the module
   window is live again. Repeat for the Log tab's Delete Log File and
   the plan's regenerate confirm, and dismiss one with Escape and one
   with the title-bar X - both must release the block.
2. **Snapshot breakdown ellipsizes:** find (or filter to) an item held
   by several characters so the breakdown line is long, at the 930px
   minimum width. The line must end in "..." rather than a clipped
   word, and hovering it - and the row's own name line, and the bare
   strip beside them - must show the full text. Then drag the window
   wider: about a fifth of a second after the drag settles the rows must
   re-fit and the "..." disappear on lines that now fit. Scroll the
   result list part-way down FIRST and confirm the drag does not move
   the scroll position, and that the drag itself stays smooth (the
   re-fit is in place now - no row is rebuilt and the search does not
   re-run).
3. **Log tags single:** turn diagnostics on, scroll the Crafting Plan
   tab, then read the Log tab at Debug+. Every scrolldiag line must show
   "[scrolldiag]" exactly once, in the dim prefix column. Copy a few
   lines and confirm the clipboard text matches.
4. **About wording:** the About tab's Source, Author, Version and Data
   directory rows must each read either a real value or "Not available"
   - no "unknown", no "Not set in manifest.json".
5. **Snapshot quantity notation:** item rows read "30x Mystic Clover"
   and wallet rows "50x Spirit Shards" - no suffix "x30" and no
   "Name: value" colon anywhere on the tab. The breakdown line beneath
   an item is the exemption and must read "Bank 20   Character: Maximus
   Test 10", counting the item at each location, NOT "20x Bank". The
   Spirit Shards row's icon slot must show the dim placeholder mark with
   its "No icon available" tooltip rather than an empty hole.
6. **Empty plan state:** open the Crafting Plan tab with no plan (a
   fresh profile, or after a plan fails to restore). The dim "No plan
   yet..." line must be centered in the content area, and must vanish
   the instant the first plan renders. Generate, then switch tabs away
   and back - the plan must still be there and the empty state must NOT
   reappear. Two more: with no plan, drag the window narrower and wider
   and confirm the line stays centered and never overflows the panel.
   Then click Generate Plan, switch to the Snapshot tab while it is
   still solving, and switch back - the content area must show the
   spinner's status only, never "No plan yet..." beside "Generating".
7. **Chrome, the two visible costs:** the Shopping List's header is now
   a dark band with white Font14 labels like every other table, and Used
   Materials has an Item/Amount header it did not have. Confirm both
   bands stop just past their own last column rather than running to the
   panel edge, at 930px and at 1400px+, and that the rows below them did
   not shift out of their section (nothing overlapping the next section
   header, no gap). Also confirm the Recipe Tree header reads "Recipe
   Tree (N)" and that N does not change when branches are expanded or
   collapsed.
8. **Button heights:** on the Snapshot header, the Log toolbar, the plan
   controls row and the Recipe Tree strip, every BUTTON must be the same
   height. The item row's "+"/"-" pair must line up with the quantity
   box, not sit short of it. Buttons are NOT expected to share a
   baseline with the textboxes and dropdowns beside them - those are
   still 26 and 30 outside the plan tab. Record how bad the Log
   toolbar's three-height run actually looks; that is the evidence for
   whether the inputs should follow to 28.
9. **Settings "Ignore":** the per-currency checkbox reads "Ignore",
   fits without touching the tag beside it, and ticking it still shows
   "ignored" in that tag and still suppresses the default on save. At
   the 930px window minimum the currency grid must still be TWO columns.
10. **API-access dialog title:** force the ApiAccessNotReady path (press
    Refresh Now at character select). The title must read "GW2 API
    access not ready" in full with clear space before the close X, and
    the checklist must wrap inside the wider window with the buttons
    centered.

### Fix round 1 (review findings)

Six Must Fix findings, all re-located against this HEAD first and all
fixed; the affected item bullets above are rewritten rather than
appended to, so they describe what the code does now.

1. The Snapshot tab's resize path armed the search debounce per drag
   frame (CTS churn plus a thrown cancellation exception per frame) and
   rebuilt every row. Now a bounded once-per-drag wait and an in-place
   re-fit. See P2 above.
2. M9's prefix sweep had reached the location breakdown, where "20x
   Bank" reads as twenty banks. Exempted, alongside tabular Amount
   columns. See M9 above.
3. `UiMetrics.ButtonHeight`'s doc claimed 28 was the module's TextBox
   and Dropdown height. It is not; the comment now records the real
   reason and names the input-height decision as unmade. See L3 above.
4. The plan's empty state contradicted an in-flight generation.
5. The plan's empty state registered a relayout closure that could
   never run. Both in L1 above.
6. `StatusText.ForSnapshotAge` had no production caller and nine tests,
   one of them a pure contract mirror. Deleted, coverage retargeted.
   See M10 above.

Nice to Have items from the same review are not addressed here and stay
open: the duplicated header-band rule
(`ShoppingListSectionRenderer.HeaderBandWidth` vs
`CTableHeaderRenderer.BandWidth`), `IconControls`' run-on comment block,
`ModalBackdrop`'s over-broad "other modules' windows stay live" claim,
`ApiAccessDialog` having no backdrop, the redundant
`ResetContentPanelToEmpty` on the rollback path, `SettingsTabContent`'s
stale "Clear checkbox" doc wording, the 2px input-to-checkbox gap, the
plan header's "x N needed" suffix versus M9's prefix rule, the
single-fetch currency cache behind L5's placeholder, and `UiMetrics`
living in `Views.Rendering`.

### Verification round 1

All six fix-round-1 findings re-read at HEAD and confirmed fixed: the
Snapshot resize path no longer touches the search debounce (one bounded
waiter per drag, in-place `RefitResultRows`, gated on
`_lastRowLayoutWidth`); the breakdown reads `{Label} {Count}`; the empty
state is `else if (!boardSnapshot.InFlight)` and its relayout closure is
reachable (the per-tick `ReplayRelayout` gate is `widthChanged` alone);
`StatusText.ForSnapshotAge` is gone from the tree, tests included.
Inventories re-taken independently and they hold: TextBoxes 26 at nine
of eleven sites, Dropdowns 30 outside the plan tab, the plan item row's
quantity box and +/- pair all 28 at y=3 in a 35px row.

One residual defect of the same class as finding 3, fixed here:
`UiMetrics.ButtonHeight`'s summary line still claimed "every
StandardButton in the module" while four dialog footer buttons are 25.
Comment scoped to tabs, the exclusion and its reason recorded, and the
L3 bullet above corrected. Doc-only - no control height changed.

Build 0 errors (2082 pre-existing StyleCop warnings), suite 2186 passed
/ 0 failed, tree clean, nothing pushed.

Gate: PASS (2026-08-23 sandbox session, branch build at the
review-fix HEAD, captures preflight/gJ1-gJ6). (L1) With plan.json
moved aside, the Crafting Plan tab rendered the centered dim empty
state. (M9) Snapshot rows read "75x Green Wood Log", the wallet
"50x Spirit Shards" - prefix notation throughout. (P2) The
multi-character breakdown line ellipsized and its hover showed the
FULL breakdown wrapped in the opaque facility tooltip. (M10) The
status line read "... Aug 23, 2026 12:23 AM (snapshot 32d old)" -
the failure timestamp and the snapshot age no longer read as one
moment; the fix-round breakdown format kept "Character: Maximus
Test 10" label-first (no "20x Bank"). (L5) Spirit Shards rendered a
placeholder icon instead of a column hole. (P4) The widened
ApiAccessDialog title sits clear of its close button. (P1) With the
Clear Cache confirm open, a click on the Bank checkbox behind it
was eaten by the backdrop - Bank stayed checked, where the
2026-08-22 photography session proved such clicks used to land.
(P3) Zero doubled tags in module_log.jsonl and fresh entries write
single tags - fixed at the write site. Observed, tolerable, noted:
ModalDialog can stack on top of an open ApiAccessDialog (different
dialog classes; backdrop still gates the module beneath both). Not
staged live: L3's unified header bands and button heights across
every tab (spot-checked on the surfaces above; pinned by the
re-baselined height tests), M12's Log row-order swap (code-reviewed).
