## Audit batch F: input flow (audit-f-input-flow)

Four maintainer-approved UX-audit findings on the Crafting Plan tab's
input flow, plus one regression the first of them exposed.

- **H4, stale resolved item (the correctness bug):** a row's item id was
  set only by a suggestion pick and never cleared, and nothing else ever
  assigned it. Editing the search box afterwards therefore left the plan
  generating for the previously picked item while the box read the new
  name - "Mystic Clover" on screen, Deldrimor Steel Ingot in the plan.
  Three parts: (1) a search-box `TextChanged` handler drops the row's
  resolved item once the text diverges from the resolved name, with case
  and surrounding whitespace not counting as divergence; (2) Generate
  first resolves typed-but-unpicked rows against the item search
  provider, adopting an exact case-insensitive name match only - a
  partial name stays unresolved rather than planning for whatever ranked
  first, and the adoption is re-checked on the main thread against what
  the row holds at that moment, so a pick or a further keystroke landing
  during the search cannot be overwritten by a result describing older
  text; (3) with nothing resolved, the status now distinguishes "Select
  at least one item before generating." (every row blank) from "No item
  matched what you typed - pick an item from the suggestion list."
  (text that resolved to nothing), where the old copy told someone
  staring at a filled-in box to select an item.
  A name that belongs to **several items** is a third case, and the one
  with no way to notice it: GW2 reuses item names freely (4136 of the
  14762 seeded names are shared, and three distinct items are called
  "Amethyst Gold Ring"), the provider sorts by name so they all land in
  one result window, and item ids are never displayed - adopting the
  first would have generated a full plan for an arbitrary one of them
  with nothing on screen to say which. Such a name now stays unresolved
  and says so: "More than one item has that name - pick the one you want
  from the suggestion list."
  A Generate where only SOME rows resolved no longer drops the rest in
  silence either: the plan is still generated from the rows that
  resolved, and the strip carries "N row(s) has/have no item selected and
  is/are not in this plan." for as long as that plan is on screen.
  While the resolution pass runs, Generate is disabled and the strip
  reads "Resolving items..." - the pass is awaited, and nothing
  downstream disables the button until a generation actually starts, so
  clicks during it would otherwise be silent and each would start another
  full generation.
  The decisions live in the new Blish-free `Services/ItemRowSelection.cs`
  (staleness rule, exact/ambiguous name match, status copy), covered by
  20 tests. `TriggerGenerate` is now a thin wrapper that owns the
  resolution await, the Generate button for its duration, and the marshal
  back to the main thread before the generate body, which touches
  controls from its first line; the body is `GenerateFromResolvedRows`.
- **Typed text across a row add/remove (regression from H4):** rebuilt
  rows seeded their search box from `ItemName`, which H4 now clears, so
  typing a name and pressing "+" wiped it. Rows keep the text they last
  showed (`ItemRowState.TypedText`) and seed from that.
- **M1, deferred controls honesty:** the Prices dropdown, Value Own
  Materials, and Use Own Materials on the no-plan path now put
  "Settings changed - press Generate Plan to update" on the status strip
  as they change - they look like the instant-apply controls on other
  tabs but only affect the next plan. (Use Own Materials with a plan on
  screen already regenerates behind a confirm, so it is not deferred.)
  The warning is standing state, not a one-shot status write, and is
  appended to whatever the status board says: a generation in flight
  re-renders the strip about seven times a second and would otherwise
  have erased it within 150ms, ending on "Plan generated - &lt;time&gt;"
  for a plan built with the price basis the user had just changed away
  from. It survives a tab switch for the same reason, and is cleared when
  a generation actually starts - which is the run that includes it. The
  "rows not in this plan" notice above rides the same mechanism.
  A generation also dims the plan area to 0.45 opacity, restored in the
  `finally` that already covers success, failure and cancellation alike;
  a superseded generation returns at its `myGen` check and leaves the dim
  to the newer generation that owns it.
- **M15, suggestion list occlusion:** the list opened directly under the
  search box, over this row's quantity field and every row below it.
  `SuggestionPanel` takes an anchor offset and opens that far right of
  the text box - right of the Qty stepper - clamped so a window at the
  right screen edge cannot push it off. It still overlaps part of the
  persistent controls row (the Prices dropdown's right half and Value Own
  Materials) and this row's own +/- buttons while open; anchoring cannot
  clear a full-width controls row, and this is the position the finding
  approved.
- **M16, the "+" button:** moved right of the quantity field so it no
  longer abuts it and reads as a Qty stepper, and given the tooltip "Add
  another item to this plan". The "-" button beside it got the same
  treatment ("Remove this item from the plan") rather than leaving the
  sibling half-fixed.

Validation: build 0 errors and the full suite green per commit (1906,
up from 1886 with the 20 new ItemRowSelection tests). No new test
references Blish.

What the desktop gate should look at:

1. **Stale-pick invalidation, live:** pick an item from the suggestion
   list, then edit the box to a different item's name and press Generate.
   The plan must be for what the box says (or, for a partial name, the
   "pick an item from the suggestion list" status) - never for the
   earlier pick. Also: type a full item name without ever opening the
   list and press Generate; it should plan that item.
2. **Shared name:** type "Amethyst Gold Ring" in full, do not open the
   suggestion list, press Generate. No plan may be generated - the status
   must read "More than one item has that name - pick the one you want
   from the suggestion list." Picking one of the three from the list and
   pressing Generate must then plan normally.
3. **Partly resolved Generate:** row 1 picked from the suggestion list,
   row 2 typed with a name that matches nothing ("Mystic Clove"), press
   Generate. The plan must be generated for row 1 AND the strip must
   carry "1 row has no item selected and is not in this plan." for as
   long as that plan is on screen - the old behavior planned row 1 and
   said nothing at all about row 2.
4. **Settings-changed status:** change Prices or Value Own Materials with
   a plan on screen; the status line under the toolbar must switch to
   "Settings changed - press Generate Plan to update" instead of leaving
   the "Plan generated - <time>" line up. Then the harder case: press
   Generate and change Prices WHILE it runs. The warning must appear
   immediately, survive the whole run (the spinner re-renders the strip
   about seven times a second), and still be there beside
   "Plan generated - <time>" when the run finishes - the plan on screen
   was built with the old basis. It must also survive a tab switch away
   and back, and disappear the moment the next Generate starts.
5. **Dimmed stale plan:** press Generate with a plan already on screen -
   the plan area should visibly dim for the run and return to full
   opacity when it finishes, on a successful run and on a failing one
   (an offline/error run is the one worth checking).
6. **Suggestion list position:** open the list on the first of two rows
   and confirm the second row's search box and quantity field stay
   visible and clickable, and that the list stays inside the window at
   minimum window width (930).
7. **Button tooltips:** hover "+" and "-" and confirm the tooltips read
   plainly and do not clip.

Gate: PASS (2026-08-22 evening desktop batch, branch build 454681b,
captures preflight/gF0-gF5). (1) Typed "mystic clover" lowercase,
never opened the suggestion list, pressed Generate: the module log
recorded "Plan for Mystic Clover x1" - unique-exact-name adoption
working end to end. (2) The suggestion list opened to the right of
the qty stepper, no longer covering Use Own Materials or the Prices
label (the documented partial-overlap tradeoff visible and
acceptable). (3) Toggling Value Own Materials appended the standing
notice "Settings changed - press Generate Plan to update" after the
board status with a separator; it survived subsequent renders and
was still standing alongside a later honest-status line. (4) The
"+" button showed "Add another item to this plan" on hover at its
separated position. (5) Appending "xx" to the resolved name and
pressing Generate produced "No item matched what you typed - pick
an item from the suggestion list." with the previous plan untouched
- stale-pick invalidation plus the honest empty status, no wrong
plan. Ambiguous-name and multi-row partial-resolution statuses were
not staged live (no duplicate-named craftable in the fixture path);
both are pinned by the ItemRowSelection tests. Bonus: the x1
all-owned plan rendered the HAVE pill and a 0c cost tile - the
zero-cost plan state previously uncaptured.
