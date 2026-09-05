> **Frozen record - 2026-08-23, branch `kb-focus-release`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Keyboard focus release (kb-focus-release)

Field repro: type into the Crafting Plan search box, do not press Enter,
press Escape. The window closes with the caret still visibly in the box,
and from then on GW2 receives no keyboard input at all until the user
clicks somewhere.

### Diagnosis

Measured by decompiling the vendored `packages/BlishHUD.1.3.0/lib/net472/
Blish HUD.exe` (ilspycmd 10.1.1). Everything below is read off that
source, not inferred:

- `TextInputBase.Focused`'s setter assigns
  `GameService.Input.Keyboard.FocusedControl = this` on EVERY change,
  including a change to **false**. `UnsetFocus()` is the only method that
  nulls the slot (`Focused = false;` then `FocusedControl = null;`), and
  so the only full release.
- Blish soft-unfocuses in two places, both of which therefore leave the
  slot naming a box that is no longer focused: the click-away handler
  (`Focused = _mouseOver && _enabled`) and `DisposeControl`
  (`Focused = false`).
- `Control.Dispose` clears `Parent` BEFORE calling `DisposeControl`, so a
  box disposed while focused leaves the slot holding an orphan whose
  `GetAncestors()` is empty. `KeyboardHandler.Update`'s self-heal only
  walks the named control's ancestors looking for an invisible one, so it
  can never reach that orphan.
- A slot naming one control while another box actually holds focus is
  what the user feels. `KeyboardHandler.ProcessInput`'s Escape branch
  consumes the key clearing the slot and returns, so the first Escape
  does nothing visible; the second finds the slot null and closes the
  window instead. The still-focused box keeps
  `KeyboardHandler._textInputDelegate` (set in `UpdateFocusState(true)`),
  and every keystroke then goes to `_textInputDelegate?.Invoke` and is
  blocked from the game. Clicking anywhere ends it, because the
  click-away handler finally sets `Focused = false`.
- Re-clicking the box does NOT repair it: the setter is guarded by
  `SetProperty`, so with `_focused` already true the assignment to the
  slot is skipped.

The module's own contribution to that desynced state is
`SuggestionPanel.OnFocusChanged`, which re-focused its text box from
inside the `InputFocusChanged` notification whenever the mouse was over
the suggestion list. `UnsetFocus()` raises that notification as its first
step and nulls the slot as its second, so an Escape pressed while
hovering the suggestion list produced exactly the reported state: box
focused, slot empty, listener live. Mouse events are raised from
`MouseHandler.Update` on the main thread (`HandleInput` only stashes the
event), so this is ordinary single-threaded reentrancy, not a race.

### Fix

`Views/FocusRelease.cs` (new) is the module's only full release. It
guards every call - a box may only null the shared slot if it holds
focus or is the control the slot already names - and offers two entry
points:

- `ReleaseOnDispose()`, chained onto a construction site, releases on the
  `Disposed` event, which fires at the top of `Control.Dispose` while the
  control is still whole and ahead of Blish's own soft unfocus. Applied
  at all **11** module text box sites (Snapshot search, Crafting Plan
  search + quantity, Log search, six in Settings, the copyable About
  field).
- `ReleaseWithin(root)` walks a subtree. `ResizableTabbedWindow` calls it
  where the module takes focus away without a click: `Hide()` (the intent,
  ahead of the fade the box would otherwise eat keys through), the
  `Hidden` event (a direct `Visible = false`), `OnTabChanged` BEFORE the
  base implementation swaps and disposes the outgoing view, and
  `DisposeControl`.

`SuggestionPanel` now re-focuses only for the press that is landing on
the panel, observed from the same `LeftMouseButtonPressed` event that
drives the unfocus. The hook is taken in the constructor on purpose:
Blish raises the event in subscription order, and `TextInputBase`
subscribes its own handler when the box first gains focus, so the panel
has to already be ahead of it to classify the release. Keeping that hook
honest needed the panels torn down on unload - they are SpriteScreen
parented, so disposing the window never reached them - which
`CraftingPlanView.DisposeSuggestionPanels()` now does from
`Module.Unload`, alongside the tickers it already had to clean up for the
same reason.

### Fix, second pass (adversarial review)

Review hardened the first pass in two places. Neither is a reproduced
field failure; both are stated as what the code now guarantees.

- `FocusRelease.Release()` called `UnsetFocus()` and returned without
  checking its own post-condition. `UnsetFocus()` is not atomic: it is
  `Focused = false;` - which raises `InputFocusChanged` synchronously,
  through `OnInputFocusChanged`, before the slot is touched - and only
  then `FocusedControl = null;`. The module ships exactly one handler on
  that event, `SuggestionPanel.OnFocusChanged`, and it re-focuses. A
  re-focus landing inside a `FocusRelease` call would end it with
  `Focused == true`, `FocusedControl == null` and
  `UpdateFocusState(true)` having re-armed `SetTextInputListner` - the
  exact swallowed-keyboard state, now with no slot for
  `KeyboardHandler`'s heal sweep to name; through `ReleaseOnDispose()` it
  would re-subscribe a control mid-teardown into Blish's static input
  handler, leaving `_textInputDelegate` pointing at a disposed box for
  the rest of the session. That handler re-focuses only while
  `_pressOverPanel` is set, which the next bullet shows cannot be true
  outside the press dispatch that set it, so this is a hazard closed
  rather than a bug observed. `Release()` walks boxes shared with
  whatever handlers the module adds later and must not depend on that
  analysis holding, so it now verifies: after `UnsetFocus()`, while the
  box still reports focus, it forces `Focused = false` (bounded to 3
  attempts - a handler that re-focuses on every notification cannot be
  out-waited and a spin is worse than a stale slot), then nulls the slot
  only if the box is genuinely unfocused and the slot still names it.
  The invariant it holds: the slot names the box that holds focus, or
  nothing.
- `SuggestionPanel._pressOverPanel`, the press-landed-on-the-panel
  discriminator, is now cleared in `ShowPanel()`, `HidePanel()` and
  `Dispose()` as well as on the global `LeftMouseButtonReleased` and
  where `OnFocusChanged` consumes it. This is hardening, not a repair.
  Re-checked against the decompile, the flag cannot outlive the press
  dispatch that sets it: `Control.Input` is `GameService.Input`, so this
  panel's constructor hook and `TextInputBase`'s own
  `OnGlobalMouseLeftMouseButtonPressed` are two handlers on one event,
  this panel's first. The flag only goes true while the panel is visible;
  the panel is only visible while the box is focused (`ShowPanel()` is
  reachable only under a `_textBox.Focused` guard, and `HidePanel()` runs
  on every unfocus); and a focused box always has `TextInputBase`'s
  handler attached, because `UpdateFocusState(true)` adds it. That
  handler therefore runs later in the same dispatch, sets
  `Focused = _mouseOver && _enabled` - false for a press that landed on
  the panel rather than the box - and `OnFocusChanged` consumes the flag
  synchronously. A dropped `LeftMouseButtonReleased`, which Blish is free
  to do (`MouseHandler.HandleInput` returns without stashing the event
  when a foreground `Form`'s client rectangle contains the point, while
  `CameraDragging`, and while the cursor is hidden, and
  `MouseHandler.Update` skips dispatch entirely when GW2 does not have
  focus), therefore cannot latch it. The clears cost nothing and state
  the lifetime bound at each site, so it survives any of those
  preconditions moving.

No test was added. Every step of this is Blish-bound: which release API
is called, the order Blish raises two of its own events in, and a walk
over `Container.Children`. The testable-looking residue is a three-bool
predicate that would only mirror the implementation, which this repo does
not accept. It stands on the sandbox check. Neither second-pass change has
a scripted repro - the reported dead keyboard is exercised by
step 1, and step 6 is a regression check on the discriminator itself,
which is the only part of this pass with behaviour a gate operator can
observe.

`Views/Rendering/TreeSectionController.cs`, the tooltip composers and
`RichTooltipSurface` were not touched.

### Sandbox check

1. **The repro.** Crafting Plan tab, click the item search box, type a
   few letters, do NOT press Enter. Press Escape. Whatever the design
   does - the box unfocuses, or the window closes - the caret must be
   GONE from the box, and typing must reach the game immediately, with
   no click needed first. Run it twice: once with the mouse resting over
   the suggestion list that dropped under the box, once with the mouse
   over the box itself. The hover-the-list run is the one that used to
   fail.
2. **Escape is not eaten.** From that same state, count the Escapes. The
   first releases the box, the next closes the window. Neither one
   should be silently swallowed with nothing happening.
3. **Tab switch while focused.** Type into the Crafting Plan search box
   and, without pressing Enter or Escape, click straight onto another
   tab. Keyboard reaches the game right away. Then press Escape once and
   confirm it closes the window rather than being consumed by a slot the
   old, disposed box left behind. Repeat with the Snapshot search box and
   with a Settings number field.
4. **Suggestion picking still works.** Type enough to raise the
   suggestion list and pick a row with the mouse. The row is selected,
   the name lands in the box, the list dismisses. Do it slowly and with a
   fast click.
5. **Window close by other means.** With a text box focused, close the
   window with its X and with the corner icon toggle. Keyboard reaches
   the game in both cases.
6. **The suggestion-panel discriminator survives an interrupted press.**
   Type into the Crafting Plan search box until the suggestion list
   drops. Press and HOLD the left mouse button over a suggestion row, and
   while still holding it Alt-Tab out of GW2 and release the button
   outside the game client - this is the case where Blish never delivers
   the release. Return to GW2 with the box still focused and check both
   halves of the discriminator from that state, without clicking
   anywhere else first: clicking a suggestion row still selects it (name
   in the box, list dismissed), and a single Escape still hard-releases
   (caret gone, typing reaches the game with no click needed). Then
   repeat the interrupted press and, instead of Escape, click straight
   onto another tab: keyboard must again reach the game immediately.
   Note for the operator: this is a regression check, not a repro. The
   flag is consumed inside the press that sets it, so a dropped release
   is not expected to change any behaviour here - and note that clicking
   anywhere off the suggestion panel, the search box included, reassigns
   the flag and ends the interrupted-press state, which is why the steps
   above go straight from the Alt-Tab to the check.

7. **Enter does not strand the slot.** Click the Qty box, type a
   number, press Enter (caret gone), then press Escape ONCE: the window
   closes immediately - no swallowed first Escape. Measured basis:
   Blish's TextBox.OnEnterPressed is a soft unfocus (Focused = false
   before EnterPressed is raised), leaving the shared FocusedControl
   slot naming the box; every module text box now chains
   ReleaseOnEnter(), whose handler runs ahead of any site handler on
   the same event and clears the stale slot. Repeat on the Crafting
   Plan search box with NO suggestions showing (a query with no
   matches), where Enter falls through AutocompleteTextBox to the same
   base path.

Gate: PASS (2026-08-23 night sandbox session, branch build, captures
preflight/gKB1-gKB8). (1)+(2) The exact user repro with the mouse over
the suggestion list: first Escape released the box (dropdown gone,
caret gone, window OPEN - eaten by design, not by a stale slot),
second Escape closed the window. (3) Tab-switch with a focused,
typed-in search box: a single Escape afterwards closed the window
immediately - the disposed box left no slot behind. (7) Qty box +
Enter, then a single Escape: window closed immediately - the
ReleaseOnEnter chain healed Blish's Enter soft-unfocus. (4) Suggestion
picking still works (name lands, list dismisses; one eaten
first-click-after-activation, a known sandbox artifact, resolved on
the repeat). (5) Corner-icon toggle with prior box focus produced no
stranding across subsequent interactions. (6) The interrupted-press
Alt-Tab case is not safely synthesizable in the sandbox; it remains
the regression-check the section describes, pinned by the
discriminator's press-consumed design. Keyboard reach was verified
through Escape semantics rather than typed-into-Paint checks: every
single-Escape-closes result above requires the slot and listener to be
clean, which is precisely the reported failure's negation.
