# Field-test feedback backlog

Every item the owner has asked for, with the evidence that closed it. This
file exists because two agreed items (sticky table headers, sort indicators)
were deferred into a scratchpad and then never dispatched. A scratchpad does
not survive a session and nothing forced a re-read.

## The rule

An item leaves this file ONLY when a merged PR is named beside it. "Done" with
no PR number is not done. Before reporting a wave complete, diff this file
against what shipped and state any OPEN item explicitly rather than silently
carrying it.

Status: OPEN | IN PROGRESS (branch) | DONE (PR #n) | RULED OUT (reason)

---

## Carried over - agreed but never dispatched (this is the failure this file prevents)

| id | Item | Status |
|----|------|--------|
| F1 | **Sticky table header rows.** When scrolling a long table the header row pins to the top of the viewport while any of that table's rows are visible, then scrolls away past the table's end. Wanted as a MODULE-WIDE standard for all full-width tables. | OPEN |
| F2 | **Sort indicators, option 3.** Persistent dim indicator on every sortable column at rest, solid + directional when active. Dim to solid is an OPACITY change so header width never moves. Reuse `UiGlyphs.SortAscending`. Owner ruled: **NO sorting in the Crafting Ranker** (its row order is already an answer). Snapshot table's Amount column must widen to accommodate. | OPEN |

## Bugs

| id | Item | Status |
|----|------|--------|
| B1 | Toggling a node between CRAFT and VENDOR repopulates the Total Cost currency table, moving the scroll position; sometimes it grows enough to push the whole Recipe Tree section off the viewport. Repro: Obsidian Heavy Breastplate, collapse the breastplate node, toggle back and forth. | IN PROGRESS (`w6-viewport`) - cause was not the table growing: Blish's `Scrollbar` zeroes `ScrollDistance` inside the restore's own assignment when its cached percent is stale. |
| B2 | **REGRESSION - still broken.** Clicking IGNORE repeatedly without moving the mouse eventually expands/collapses the node instead of toggling IGNORE. Two causes were fixed in PR #232 (pill membership, cost-column re-derivation); a third remains. | OPEN |
| B3 | Tree content still overdraws the pinned header when scrolled. Owner's question, which must be answered not deflected: "why aren't we just positioning the viewport lower so this can never happen?" | IN PROGRESS (`w6-viewport`), **PARTIAL** - hard cutoff built and every container outside the tree swept. RESIDUAL: the per-depth containers in `Views/Rendering/TreeSectionController.cs` still need the mechanical `Panel`/`FlowPanel` -> `ClippedPanel`/`ClippedFlowPanel` swap. Blocked on `w6-tree` owning that file. B3 is NOT closed until that lands. |

## Layout and dynamic sizing

| id | Item | Status |
|----|------|--------|
| L1 | "+N" no-room chips appear on Obsidian Heavy Breastplate rows where the real available width WOULD fit the full pill run, the ignore control and the currency cell without cramming. The dynamic layout logic is giving up too early. | OPEN |
| L2 | Consider replacing the IGNORE text pill with an X control - no language to translate, fewer i18n problems later. | OPEN |
| V5 | Recipe Tree "Cost" header still not visually centred over its content. | OPEN |
| V6 | Recipe Tree "Item" column header should anchor further left. | OPEN |

## Visual polish

| id | Item | Status |
|----|------|--------|
| V1 | Currency icon is not vertically centred against the numeric digits of the amount text beside it. | IN PROGRESS (`w6-icons`) - the seat was 0 at nearly every inline site, not a line-box-vs-digits mismatch. |
| V2 | **Regression from PR #232.** Currency icons were given a grey BACKGROUND. Only a gentle grey BORDER was asked for; the fill was never requested and the background was previously transparent. | IN PROGRESS (`w6-icons`) - the frame builder always drew a filled plate; #232 merely routed currency art onto it, and transparent art let it show. |
| V3 | Settings: "Copper per unit" header not centred over the content below it. | IN PROGRESS (`w6-polish`) |
| V4 | Crafting Ranker X buttons should match the size scale of the X control in the top corner of the GW2 Trading Post window. | IN PROGRESS (`w6-icons`) - 28 -> 24px, the floor the 16px glyph allows. Owner eyeball needed; no TP capture exists to measure against. |
| V7 | Clear Overrides dialog: "Ignore marks are kept" should be centred under the text above it. | IN PROGRESS (`w6-polish`) - `ModalDialog` centred the block, not the lines. |
| V8 | Log tab: long lines run off the edge. They should wrap, with the full detail available on hover. | IN PROGRESS (`w6-polish`) - wrapping shipped with variable row heights. NOTE: the agent could not reproduce the overrun itself (the column already ellipsized). If it persists after the next build, the cause is upstream of the message fit. |

## Research

| id | Item | Status |
|----|------|--------|
| R1 | Two currency ids (39 and 77) are both named "Gaeting Crystal". Establish whether one is deprecated, or produce a thesis for why both exist. Answered: id 39 was retired in-game 2022-07-19 and force-converted to Magnetite Shards; id 77 is the live one and carries the role forward across expansions rather than being replaced. Currency 39 and its item form 86094 removed from the module. Evidence: `dev/records/gaeting-crystal-duplicate-ids.md`. | IN PROGRESS (PR #235, not yet merged) |
| R2 | Currency 77 is absent from `Gw2Constants` while 82 cost lines in `ref/vendor_offers.json` are priced in it. Found while answering R1. | IN PROGRESS (PR #235, not yet merged) |

## Confirmed working in the wave-2 build (owner-verified)

- Generate Plan empty-request error dialog
- Crafting Ranker Analyze / Analyzing...
- Clear History dialog sizing
- Dimmed-subtree grey vertical lines are gone
