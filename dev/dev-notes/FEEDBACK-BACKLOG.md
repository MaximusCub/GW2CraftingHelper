# Field-test feedback backlog

Every item the owner has asked for, with the evidence that closed it. This
file exists because two agreed items (sticky table headers, sort indicators)
were deferred into a scratchpad and then never dispatched. A scratchpad does
not survive a session and nothing forced a re-read.

## The rule

An item becomes DONE only when the OWNER has explicitly confirmed it working.
A merged PR makes an item SHIPPED (PR #n) - a distinct state, not DONE.
Before reporting a wave complete, diff this file against what shipped and
state any OPEN/SHIPPED-unconfirmed item explicitly rather than silently
carrying it. An owner report of breakage reopens a shipped item.

Status: OPEN | IN PROGRESS (branch) | SHIPPED (PR #n) - owner confirmation
pending | DONE (owner-confirmed, PR #n) | RULED OUT (reason)

---

## Carried over - agreed but never dispatched (this is the failure this file prevents)

| id | Item | Status |
|----|------|--------|
| F1 | **Sticky table header rows.** When scrolling a long table the header row pins to the top of the viewport while any of that table's rows are visible, then scrolls away past the table's end. Wanted as a MODULE-WIDE standard for all full-width tables. | PARTIAL - Snapshot half SHIPPED (PR #236), mechanism verified working in the 2026-08-30 sandbox drive (pin, wheel-through, unpin all correct); owner validated the mechanism 2026-08-30 ("sticky headers in snapshot looks right except for the overdraw"); final confirmation after W7 lands. Recipe Tree half is now an explicit owner request: see W3 below. Adoption recipe in "Plan tab adoption" below. |
| F2 | **Sort indicators, option 3.** Persistent dim indicator on every sortable column at rest, solid + directional when active. Dim to solid is an OPACITY change so header width never moves. Reuse `UiGlyphs.SortAscending`. Owner ruled: **NO sorting in the Crafting Ranker** (its row order is already an answer). Snapshot table's Amount column must widen to accommodate. | DONE (owner-confirmed 2026-08-30: "F2 is good", PR #236); owner refinement -> W2 below. Done on Shopping List, Used Materials, both Snapshot runs, and Plan History (newly sortable). |

## Bugs

| id | Item | Status |
|----|------|--------|
| B1 | Toggling a node between CRAFT and VENDOR repopulates the Total Cost currency table, moving the scroll position; sometimes it grows enough to push the whole Recipe Tree section off the viewport. Repro: Obsidian Heavy Breastplate, collapse the breastplate node, toggle back and forth. | DONE (owner-confirmed 2026-08-30: "craft/vendor toggling is no longer causing the user to get thrown around in the scroll positioning disorienting them", PR #236; was w6-viewport) - cause was not the table growing: Blish's `Scrollbar` zeroes `ScrollDistance` inside the restore's own assignment when its cached percent is stale. |
| B2 | **REGRESSION - still broken.** Clicking IGNORE repeatedly without moving the mouse eventually expands/collapses the node instead of toggling IGNORE. Two causes were fixed in PR #232 (pill membership, cost-column re-derivation); a third remains. | SHIPPED (PR #236) - DONE (owner-confirmed 2026-08-30: "B2 is good. will retest with new buttons later", PR #236) (was w6-tree) - third cause found: the row's guard read Blish's hover flag, which HoverChainResync cannot fix on a full rebuild because the new rows have not been laid out when it runs. Guard is now geometric. SWEEP (wave6-ui): the identical pattern was live at `Views/CraftingPlanView.cs` on the Required Recipes header - both the press-feedback predicate and the press-time flag read `hideUnlockedCheckbox.MouseOver`, on a path whose own CheckedChanged rebuilds the whole plan, so the checkbox is a new instance on every click. Both now hit-test geometrically. The only other live `.MouseOver` reads are `Views/SuggestionPanel.cs` 76 and 440, deliberately left: that panel is created once, parented to SpriteScreen and positioned explicitly, so nothing there is rebuilt under the cursor. |
| B3 | Tree content still overdraws the pinned header when scrolled. Owner's question, which must be answered not deflected: "why aren't we just positioning the viewport lower so this can never happen?" | DONE (owner-confirmed 2026-08-30: "i see no draw issues in recipe tree at UI size large", PR #236) - FIXED - hard cutoff re-asserted at every container in the viewport. The residual the branch could not reach, the per-depth containers in `Views/Rendering/TreeSectionController.cs`, was swept during wave-6 integration: 6 sites, all 6 that file has. The reach is now `cutoff - SlipBudget` at depth 64 exactly as at depth 1. `TopStripZIndex` stays as defence in depth - see docs/ARCHITECTURE.md V.26.1. |

## Layout and dynamic sizing

| id | Item | Status |
|----|------|--------|
| L1 | "+N" no-room chips appear on Obsidian Heavy Breastplate rows where the real available width WOULD fit the full pill run, the ignore control and the currency cell without cramming. The dynamic layout logic is giving up too early. | SHIPPED (PR #236) - REOPENED BY W5 (owner 2026-08-30: the +1 chip still shows on 1x Obsidian Shard while width remains) (was w6-tree) - pill column is data-derived now, floor 256 plus half the surplus past the minimum window. |
| L2 | Consider replacing the IGNORE text pill with an X control - no language to translate, fewer i18n problems later. | SHIPPED (PR #236) - owner confirmation pending (was w6-tree) - remove mark in a raised (off) or pressed (on) key; state is the fill, not the mark. |
| V5 | Recipe Tree "Cost" header still not visually centred over its content. | SHIPPED (PR #236) - DONE (owner-confirmed 2026-08-30: "V5 is good", PR #236) (was w6-tree) - it centred over the widest SINGLE row, and one mixed coin+currency row spans a band every coin row collapses. |
| V6 | Recipe Tree "Item" column header should anchor further left. | SHIPPED (PR #236) - refined by W10: owner wants the label left-aligned over the FULL column including the icon gutter, on all tables (was w6-tree) - moved onto the plan's shared left-header rule, 8px left. |

## Visual polish

| id | Item | Status |
|----|------|--------|
| V1 | Currency icon is not vertically centred against the numeric digits of the amount text beside it. | SHIPPED (PR #236) - owner will re-check once W1 removes inline borders; refinement -> W11 (was `w6-icons`) - the seat was 0 at nearly every inline site, not a line-box-vs-digits mismatch. |
| V2 | **Regression from PR #232.** Currency icons were given a grey BACKGROUND. Only a gentle grey BORDER was asked for; the fill was never requested and the background was previously transparent. | SHIPPED (PR #236) - DONE (owner-confirmed 2026-08-30: "V2 is good", PR #236) (was `w6-icons`) - the frame builder always drew a filled plate; #232 merely routed currency art onto it, and transparent art let it show. |
| V3 | Settings: "Copper per unit" header not centred over the content below it. | SHIPPED (PR #236) - DONE (owner-confirmed 2026-08-30: "V3 good", PR #236) (was `w6-polish`) |
| V4 | Crafting Ranker X buttons should match the size scale of the X control in the top corner of the GW2 Trading Post window. | SHIPPED (PR #236) - owner: "too large, likely because we are no longer using the real blish button"; W4 rework in flight, recheck after (was `w6-icons`) - 28 -> 24px. **MEASURED, not eyeballed.** The reference was in the owner's own ranker screenshot all along (GW2 close plate and our buttons in the same image, so a pure ratio needs no UI-scale assumption): GW2 plate 15px wide, our 28px button's plate 19px wide, ratio 1.27x. `FeedbackButton` insets its plate by `(3, 3, Width-6, Height-5)`, so 28 -> 24 shrinks the plate 22 -> 18 logical, predicting 15.5px against the reference 15px. |
| V7 | Clear Overrides dialog: "Ignore marks are kept" should be centred under the text above it. | SHIPPED (PR #236) - owner confirmation pending (was `w6-polish`) - `ModalDialog` centred the block, not the lines. |
| V8 | Log tab: a long line runs out of space and cannot be read without the tooltip, which is dumb. It should WRAP instead. (Owner's words. An earlier version of this row paraphrased it as "runs off the edge", which is a different claim and sent an agent looking for an overrun that was never reported.) | SHIPPED (PR #236) - DONE (owner-confirmed 2026-08-30: "V8 is good", PR #236) (was `w6-polish`) - wrapping shipped with variable row heights; the tooltip is now a supplement, not the only way to read a line. |

## Research

| id | Item | Status |
|----|------|--------|
| R1 | Two currency ids (39 and 77) are both named "Gaeting Crystal". Establish whether one is deprecated, or produce a thesis for why both exist. Answered: id 39 was retired in-game 2022-07-19 and force-converted to Magnetite Shards; id 77 is the live one and carries the role forward across expansions rather than being replaced. Currency 39 and its item form 86094 removed from the module. Evidence: `dev/records/gaeting-crystal-duplicate-ids.md`. | DONE (owner-confirmed 2026-08-30: "R1/R2 good"; answered and shipped, PR #235) |
| R2 | Currency 77 is absent from `Gw2Constants` while 82 cost lines in `ref/vendor_offers.json` are priced in it. Found while answering R1. | DONE (owner-confirmed 2026-08-30: "R1/R2 good"; PR #235, commit 9c5c9ac names 77 at `Models/Gw2Constants.cs:152`; the 28-equality pin is PR #237) |

## Field test wave 1 findings (2026-08-30, owner's words verbatim)

| id | Item | Status |
|----|------|--------|
| W1 | "for currency icons - i would like for them to have a border in the config/settings screen like they do, but not have a border when the icon is used in the smaller scale next to numeric digits -- this is in cases where its used similarly to the gold/silver/copper icons as a currency symbol, versus an actual icon." | SHIPPED (PR #238) - owner confirmation pending |
| W2 | "give a bit more padding -- maybe double it -- between the column heading icons and the glyphs that indicate sort status on all tables with sorting enabled" (Image #1: Shopping List header, glyphs tight against labels; measured gap ~5px at owner's scale) | SHIPPED (PR #238) - owner confirmation pending |
| W3 | "the sticky column headers feature we discussed does not appear to be working.. see before scrolling down the recipe tree: [Image #2] and after: [Image #3] note that the table header row does not stick to the top as you scroll down so that you can always see what column label each row belongs to as you scroll" - Explanation: the Recipe Tree is the Crafting Plan tab, whose sticky-header adoption was DELIBERATELY not built (owner ruling parked F1 at the Snapshot tab pending validation). Owner's 2026-08-30 follow-up on the Snapshot tab: "only half work" - see W7. Owner validated the mechanism 2026-08-30 modulo the overdraw ("looks right except for the overdraw"). The Recipe Tree half builds AFTER W7 lands and the in-flight wave-7 agents release the shared files. | QUEUED - build after W7 lands |
| W7 | "ok sticky header in snapshot tab only half work -- [Image #6] the content from the window scroll is painting over the top of them versus the viewport getting a cutoff just at the bottom of the pinned table header instead." - Owner's screenshot (815 items, live account) and the 2026-08-30 sandbox drive (capture smoke2/39) independently show scrolled row text ghosting through the pinned header band. Root cause CORRECTED during the fix: the vendor's default ZIndex is 5, so the ZIndex-1 sticky clip painted FIRST and rows overdrawn it at full brightness - the missing cutoff was secondary. The viewport now publishes a hard cutoff at the lowest pinned band's live bottom (owner's directive); ClipZIndex stays 1 because the hit test walks ZIndex descending, which is what makes wheel-over-pinned-header scroll (sandbox-verified PASS). | SHIPPED (PR #238) - owner confirmation pending |
| W4 | "the X glyph needs to be black and the button needs the same shadowing and mouseover animation as the traditional blish buttons like \"Best Path\", etc all have. it looks like we just created our own instance of something that looks like a button kinda. can we actually fully reproduce the same behavior with the same nice animations and features that the core blish buttons give, but just with more of the customizability that we need? I thought we were going to subclass it and keep a lot of the cool behaviors?" - Measured from Image #2: current X is white glyph on light plate; the standard buttons render black-on-grey with the hover animation. CLAUDE.md's StandardButton note (black text over light art, untinted icon blit) aligns with the ask. Owner addendum mid-wave: "too large", match the top-corner close button scale. Discovery: FeedbackButton already subclassed StandardButton - the real offender was the hand-drawn toggle pill in RenderDecisionPills, now a real 24px FeedbackButton (inherited hover/atlas/shadow, black ink, amber tint while ignored). | SHIPPED (PR #238) - owner confirmation pending |
| W5 | "the 1x obsidian shard row is still showing the +1 instead of fitting the full content: [Image #4] yet the Skywatch Archipelago: Commander's Choice Chest is the longest row and there is plenty more room for that status column to be wider both in the direction of the item tree and towards the currency column on the right. The auto-size and fit algorithm that is adjusting the display layout needs more work." - Image #4 also shows the "+N" hover tooltip ("No room to show: HAVE 19/30 NEEDED") on the Mystic Clover row. This is L1's problem class recurring in the wave-6 build: the data-derived pill column (floor 256 + half surplus) still collapses pills while width exists. | SHIPPED (PR #238) - owner confirmation pending; also re-closes L1 when confirmed |
| W6 | "confirm dialog for clearing overrides needs the Confirm title to be centered: [Image #5]" - Image #5 shows the Confirm dialog with body text and buttons centred (V7's fix) but the "Confirm" title left-aligned in the title bar. | SHIPPED (PR #238) - owner confirmation pending |
| W8 | "i notice the currency table now shows a mix of currencies and items.. we should make a todo to discuss whether its right to mix that in that table or if we should do it.. as well as why that choice was made in the first place." - DISCUSSION TODO, no code attached. CONTEXT (archaeology for the discussion, primary sources): the mixing is deliberate, from wave-5's w5-noncoin work (commits 57fa0a8, cfb2dd1, 28d4a7c; docs/ARCHITECTURE.md 7.5). The plan reports THREE costs in three units - TotalCoinCost, CurrencyCosts (wallet currencies), BarterItemCosts (untradeable vendor tokens, e.g. Mystic Coin) - and "PlanViewModel.NonCoinCostTotals and the Total Cost section's table are the display side, and they are one list projected from one set of rows so the plan-level figure and the table a reader checks it against cannot drift" - that one-list-no-drift rule is WHY items sit in the currency table. The motivation was measured: "Legendary Rune (91536) buys 6 of its 7 vendor steps for no coin at all; a plan of that shape presenting one gold figure presents a fraction of its own price." TENSION for the discussion: the owner's earlier ruling says "Total Cost owns everything priced (gold AND currencies); Prerequisites owns what is not priced at all" - barter items are coin-unpriced, yet they render in the Total Cost table. Options to weigh: keep the mixed one-list table (figures cannot drift); split barter items into their own table or a Prerequisites-adjacent section (matches the earlier ruling, costs the no-drift property); or visual separation within one table. | OPEN - awaiting owner discussion |
| W9 | "i would like to see if we can make the viewport cutoff that lives under the horizontal line that is under 'Plan Updated' a little tighter and closer to the actual horiztonal line.. you can see the gap between them and it looks 'off' i think we want to see no gap. [Image #7]" - Plan tab at UI Large, B3 confirmed clean there; this is the residual gap between the section separator line and the first clipped content row. Measured source: 3px layout dead strip + 2px slip headroom + 0-2px scale loss. Fix: SeparatorToContentGap 5 -> 0, viewport top on the rule, cutoff at the rule's bottom edge. | SHIPPED (PR #238) - owner confirmation pending |
| W10 | "V6 - i realize all tables have this issue - the icon \"column\" is being treated as its own column.. so when i said left align the Item column heading .. you left aligned it above the text portion of the content, rather than left aligned on the whole title bar. this issue occurs for all tables in crafting plan with an icon on the left side, the item columns in the snapshot tab, the crafting rankers \"item\" column - though it has the rank column to the left which is a slight variance but that same \"icon is part of the item column content\" rule applies there for the column label alignment. the vendor cost valuations table in the settings tab is impacted too." - Rule: the header label left-aligns to the FULL column including the icon gutter; the icon is part of the column content, not a separate column. Scope: plan-tab tables with icons, Snapshot item columns, Ranker Item column (rank gutter included in the label span), Settings vendor cost valuations grid. | QUEUED (wave7-fieldtest) - starts when the in-flight agents release MainView/TreeSectionController |
| W11 | "V1 i want to come back to once the borders are removed but overall the alignment of text to currency icons should duplicate this exactly: [Image #8] we do not need the vertical bar separators as we likely dont have space for them. but notice how gold silver and copper icons are slightly lower compared to the text in game compared to our app placement here: [Image #9] which is more aligned with the top of the text." - Spec: duplicate Image #8's text-to-icon alignment for non-coin currency icons (once W1 removes inline borders) WITHOUT the vertical bar separators; icons seat slightly lower than the text like in-game - ours rides top-aligned (Image #9 is our app). | QUEUED (wave7-fieldtest) - blocked on W1 |

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
