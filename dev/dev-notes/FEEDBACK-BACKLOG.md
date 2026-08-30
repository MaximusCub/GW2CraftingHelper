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
| F1 | **Sticky table header rows.** When scrolling a long table the header row pins to the top of the viewport while any of that table's rows are visible, then scrolls away past the table's end. Wanted as a MODULE-WIDE standard for all full-width tables. | IN PROGRESS (w6-tables) - mechanism built (`Services/StickyHeaderLayout`, `Views/Rendering/StickyHeaderHost`) and wired on the Snapshot tab, whose two runs share one scroll. The Crafting Plan tab is NOT wired: its bands flow in a `FlowPanel` and the adoption needs a change in `Views/CraftingPlanView.cs` / `Views/Rendering/TreeSectionController.cs`, which another agent owned this wave. See "Plan tab adoption" below. |
| F2 | **Sort indicators, option 3.** Persistent dim indicator on every sortable column at rest, solid + directional when active. Dim to solid is an OPACITY change so header width never moves. Reuse `UiGlyphs.SortAscending`. Owner ruled: **NO sorting in the Crafting Ranker** (its row order is already an answer). Snapshot table's Amount column must widen to accommodate. | IN PROGRESS (w6-tables) - done on Shopping List, Used Materials, both Snapshot runs, and Plan History (newly sortable). Awaiting a merged PR number. |

## Bugs

| id | Item | Status |
|----|------|--------|
| B1 | Toggling a node between CRAFT and VENDOR repopulates the Total Cost currency table, moving the scroll position; sometimes it grows enough to push the whole Recipe Tree section off the viewport. Repro: Obsidian Heavy Breastplate, collapse the breastplate node, toggle back and forth. | OPEN |
| B2 | **REGRESSION - still broken.** Clicking IGNORE repeatedly without moving the mouse eventually expands/collapses the node instead of toggling IGNORE. Two causes were fixed in PR #232 (pill membership, cost-column re-derivation); a third remains. | OPEN |
| B3 | Tree content still overdraws the pinned header when scrolled. Owner's question, which must be answered not deflected: "why aren't we just positioning the viewport lower so this can never happen?" | OPEN |

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
| V1 | Currency icon is not vertically centred against the numeric digits of the amount text beside it. | OPEN |
| V2 | **Regression from PR #232.** Currency icons were given a grey BACKGROUND. Only a gentle grey BORDER was asked for; the fill was never requested and the background was previously transparent. | OPEN |
| V3 | Settings: "Copper per unit" header not centred over the content below it. | OPEN |
| V4 | Crafting Ranker X buttons should match the size scale of the X control in the top corner of the GW2 Trading Post window. | OPEN |
| V7 | Clear Overrides dialog: "Ignore marks are kept" should be centred under the text above it. | OPEN |
| V8 | Log tab: long lines run off the edge. They should wrap, with the full detail available on hover. | OPEN |

## Research

| id | Item | Status |
|----|------|--------|
| R1 | Two currency ids (39 and 77) are both named "Gaeting Crystal". Establish whether one is deprecated, or produce a thesis for why both exist. | OPEN |

## Confirmed working in the wave-2 build (owner-verified)

- Generate Plan empty-request error dialog
- Crafting Ranker Analyze / Analyzing...
- Clear History dialog sizing
- Dimmed-subtree grey vertical lines are gone

---

## Plan tab adoption of sticky headers (F1's remaining half)

`StickyHeaderHost` moves the REAL band rather than drawing a copy, so a
pinned header still sorts. That works on the Snapshot tab because its
bands sit in an absolutely-placed grid panel: taking one out leaves no
hole. The Crafting Plan tab's bands are children of a `FlowPanel`, and
removing one reflows every section below it by the band's height.

The change, when `Views/CraftingPlanView.cs` is free:

1. Add a fixed-height SPACER to `Views/Rendering/HeaderBands.cs` - a
   `Panel` of `HeaderBands.RowHeight` that stays in the flow, with the
   band as its only child at (0,0). The flow then measures the spacer,
   not the band, and pinning the band out of it moves nothing.
2. `CraftingPlanView` builds one `StickyHeaderHost(<the container that
   holds the scrolling content panel>, <the scrolling content panel>)`
   per `Build`, and calls `Clear()` before every teardown of its section
   controls - a pinned band is not a child of the content panel, so the
   dispose walk would otherwise miss it.
3. Each section renderer that owns a band hands it over with the spacer
   as its `home`, and a geometry of
   `(present, 0, 0, panelWidth, HeaderBands.RowHeight, HeaderBands.RowHeight + <that section's rows' height>)`.
   The rows' height is already synchronous in
   `Services/PlanContentHeightMath`, and the spacer's own live position
   supplies both the flow offset and the scroll, so nothing new has to be
   tracked per frame.
4. The Recipe Tree is the same call, with its subtree height as the rows'
   height (`TreeSectionController` already computes it). Its pinned band
   lands at the top of the content panel's own viewport, which is BELOW
   the pinned top strip - so this does not touch B3 either way.
