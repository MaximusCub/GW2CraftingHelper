> **Frozen record - 2026-08-23, branch `settings-dirty-prompt`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Settings dirty prompt (settings-dirty-prompt)

Reported in game: if settings have been edited before tabbing
away, the module must prompt the user to save or discard those changes
before proceeding. The
Settings tab has one Save button covering four sections, and nothing
told a user who typed a currency override and then clicked another tab
that the override was about to evaporate - the tab is rebuilt from
persisted settings on every re-entry, so the edit was silently gone.

**Dirty detection.** `Models/SettingsFormState` is a flat key/value bag
of the tab's save-gated control values - one amount and one Ignore flag
per currency row (47 of each), one tier per Homestead row, the two
logging fields and the snapshot interval. Dirty is "this capture differs
from the baseline taken at the last load or successful save", so typing
a value and reverting it reads as clean, as does typing whitespace and
deleting it (every value is trimmed on capture, matching what every
`SettingsInputParser` entry point does before parsing). A key present in
only one of the two captures counts as a change in both directions, and
duplicate keys are rejected outright rather than overwritten - two
controls sharing a key would collapse into one comparison and silently
stop reporting edits to whichever lost. The type has no Blish reference,
so the whole comparison is covered by real tests; only the thin
`CaptureFormState` reader in `SettingsTabContent` touches controls.

The Diagnostics checkbox is deliberately NOT part of the dirty model.
Its `CheckedChanged` handler writes straight through to `ModuleSettings`
and to the live `ModuleLog`, so it is never an unsaved change; listing
it would raise a save prompt for a value already on disk.

**Hook mechanics, measured from the vendored Blish HUD 1.3.0 binary**
(`packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe`, decompiled with
`ilspycmd`). There is no cancellable before-change hook, and the reason
is structural rather than an omission:

- `TabbedWindow2.SelectedTab`'s setter is a plain (non-virtual) property.
  It calls `SetProperty(ref _selectedTab, value, invalidateLayout: true)`
  - which assigns the backing field - and only then calls
  `OnTabChanged(new ValueChangedEventArgs<Tab>(previous, value))`.
  By the time anything else runs, the tab has already changed.
- `OnTabChanged` is `protected virtual`, but its FIRST act is
  `ShowView(e.NewValue?.View())`, and it raises the public `TabChanged`
  event LAST. So the outgoing view is already torn down and the incoming
  one already requested before any module handler is reached. There is
  no `TabChanging`, no `Cancel` flag on the event args, and no return
  value the handler can use.
- Two earlier interception points were measured and rejected.
  (a) Overriding `OnTabChanged` in `ResizableTabbedWindow` and deferring
  the `base` call until the user answers: `_selectedTab` is private and
  has already moved, so the sidebar would highlight the new tab while the
  old view is still on screen for the dialog's lifetime - a visibly
  inconsistent state, and the deferred `base` call re-enters `ShowView`
  from a dialog callback. (b) Overriding `OnClick` and swallowing the
  click before `SelectedTab = HoveredTab` runs: `HoveredTab` is private,
  so this means re-deriving the hit test from `SidebarActiveBounds`
  (protected), `RelativeMousePosition` (public) and `Tabs.FromIndex`
  (public) against TAB_VERTICALOFFSET=40 / TAB_HEIGHT=50, which are
  private consts. That is a hand-copy of Blish's private layout geometry
  whose drift mode is a missed or spurious prompt, and the click it reads
  can differ from the cached `HoveredTab` the base would have used if the
  mouse moved between the last `UpdateTabStates` and the click.

So the prompt is raised AFTER the switch, from the public `TabChanged`
event, keyed on `e.PreviousValue == _settingsTab`. This is safe because
of a second measurement: `WindowBase2.ShowView` -> `ClearView` ->
`Container.ClearChildren`, and `ClearChildren` only unparents
(`while (_children.Count > 0) _children[0].Parent = null;`) - it does not
dispose. The Settings TextBoxes and Checkboxes are therefore still alive
and still holding the user's typed text when the handler runs, so both
Save (persists exactly what was on screen) and the dirty comparison
itself are reading real values, not a torn-down form.

**Window close is deliberately NOT hooked.** `TabChanged` never fires
when the window is closed, and hanging the same prompt off
`_mainWindow.Hidden` was tried and rejected on two measurements against
the vendored binary:

- `Hidden` is not "the user closed the window". Every `WindowBase2`
  subscribes in its constructor to
  `Gw2Mumble.PlayerCharacter.IsInCombatChanged ->
  UpdateWindowBaseDynamicHUDCombatState` and to
  `Gw2Instance.IsInGameChanged -> UpdateWindowBaseDynamicHUDLoadingState`,
  and both statics call `wb.Hide()`. A user running Blish's overlay
  options "hide windows in combat" (`DynamicHUDWindows = ShowPeaceful`)
  or "hide during loading screens" (`DynamicHUDLoading = NeverShow`)
  would get a modal save prompt over gameplay for pulling a mob or
  zoning, with the auto-restored module window behind it. Both options
  default to `AlwaysShow`, so it was configuration-dependent - which
  makes it worse to diagnose, not better.
- Closing is not destructive in the first place. Hiding the window does
  not call `ShowView`/`ClearView`, so the Settings TextBoxes keep the
  user's typed text and still have it when the window is reopened.
  Prompting there put an Escape/X keypress (which routes to cancel, i.e.
  Discard) between the user and edits that were never at risk.

So closing the window is left exactly as it behaved before this branch:
edits stay in the controls, and the prompt appears only when the user
actually leaves the tab.

**Off-thread build.** `WindowBase2.ShowView` runs the view's build as
`view.DoLoad(progress).ContinueWith(BuildView)` with no scheduler, so
`SettingsTabContent.Build` executes on a ThreadPool thread while
`UnsavedChangeCount` is called from the main thread's `TabChanged`
handler. `Build` clears and refills `_rows` (47 entries) and
`_homesteadRows`, which `CaptureFormState` enumerates - a tab switch
landing mid-build would throw "Collection was modified" out of Blish's
own input dispatch, where the module has no try/catch. A `volatile bool
_buildComplete`, cleared as the first statement of `Build` and set as the
last of `LoadAll`, makes `UnsavedChangeCount` return 0 for that window.
The cost is a missed prompt for a switch within a frame or two of the tab
building, which is the same benign outcome the null baseline already had.

Clearing it in `Build` is not early enough on its own. `OnTabChanged`
commits to the rebuild on the MAIN thread - it evaluates the tab's view
factory and calls `ShowView`, which queues `BuildView` off an
already-completed `DoLoad` - and only afterwards raises `TabChanged`. In
the ThreadPool scheduling gap between those two, `Build` has not run its
first statement yet and the flag still reads true from the PREVIOUS
build, so a second tab click landing there would enumerate the row lists
the queued `Build` is about to clear. `SettingsTabContent.BeginRebuild`
therefore clears the flag from the Settings tab's view factory, which is
evaluated on the main thread inside `OnTabChanged` before either the
queued build or the event. Only the tab-entry path calls it, so it
cannot suppress the leave-the-tab prompt (leaving Settings evaluates the
INCOMING tab's factory, not this one).

**Prompt shape.** The existing `ModalDialog` has exactly two buttons, and
no third was added. Confirm = Save, cancel = Discard, and `cancelText` is
now an optional `Show` parameter (defaulting to the "Cancel" every
existing caller already got) so the second button says "Discard" rather
than "Cancel" - a button labelled Cancel would promise to put the user
back on the Settings tab, which is exactly what Blish gives no way to do.
Both button widths floor at their historical values (100 confirm, 70
cancel) and grow to fit a longer label rather than being clipped by
StandardButton's centered, unpadded text region - every existing dialog
is pixel-identical, and the second prompt's "Open Settings" fits. Discard restores the last loaded/saved values into the
controls and clears the save bar's status line.

**Saving from the prompt reports its own failures.** The tab's save bar
is unparented the moment the view is torn down, so a `SaveAll` driven
from the prompt has nowhere on screen to say "3 entries were rejected" -
and `SaveAll` rebases its baseline on the controls, so nothing
re-prompts either. The user asked to save and half of it silently would
not. `SaveAll` therefore returns a `SaveOutcome` (invalid-entry count
plus a failed-write flag; the in-tab Save button still ignores it and
uses the status label), and the prompt raises a second dialog when the
outcome is not `AllSaved`, offering "Open Settings" / "Dismiss". Note
that reopening rebuilds the form from persisted settings, so the message
says "re-enter" rather than promising the rejected text back.

**Re-raising the shared dialog needed a fix in `ModalDialog` itself.**
The second prompt is raised from inside the first one's confirm callback,
and `Dismiss` used to hide the window BEFORE running that callback. That
does not work, for reasons measured in the vendored binary:
`WindowBase2.Hide()` does not set `Visible = false` - it resumes the
0.2s reflecting fade tween built in the constructor, and only that
tween's `OnComplete` sets `Visible = false` and raises `Hidden`. Meanwhile
`WindowBase2.Show()` begins `BringWindowToFront(); if (Visible) return;`.
So the re-raised dialog's `Show()` early-returned into a window that was
already fading out: it painted the second message for ~0.2s, faded to
nothing, and its own `Hidden` event ran `Dismiss(confirmed: false)` -
a flash, then the same silent partial save the second dialog exists to
prevent. `Dismiss` now runs the callback first and skips its own
`Hide()` when that callback re-armed the dialog (`_isShowing` true
again), inside a `try`/`finally` so a throwing callback still closes it.
The early return then does the right thing: the still-visible window
carries the replacement's content. Nothing else changes for the four
single-shot callers - their callbacks do not re-arm, so the window is
hidden the moment the callback returns, one main-thread statement later
than before and with no frame drawn in between.

**Accepted limits.**

- The switch itself cannot be vetoed, so the tab has already changed when
  the prompt appears. On the tab path this is benign - returning to
  Settings rebuilds the form from persisted settings either way - but the
  prompt cannot offer "stay here".
- The dialog's title-bar X and the Escape key both route to cancel, which
  here means Discard. Benign now that the prompt is raised only on the
  tab path: returning to Settings rebuilds the form from persisted
  settings either way, so Discard is what leaving the tab already meant.
- A tab switch that lands while the Settings tab is still building on
  Blish's worker thread does not prompt (see "Off-thread build").
- A module unload with dirty settings (Blish shutting down, module
  disabled) tears the window down without prompting; `Unload` has no
  user-interaction budget.
- If another module dialog is already on screen `ModalDialog.Show`
  returns false and the prompt is skipped for that leave. Not reachable
  in practice - `ModalBackdrop` blocks the module window while any dialog
  is up, so the tab click that would trigger it cannot land.

Sandbox check:

1. Settings tab, edit one currency amount (type a number into an empty
   box), then click another tab. The prompt appears, headed "Confirm",
   reading "You have 1 unsaved change on the Settings tab. Save now, or
   discard and keep the last saved values?", with Save and Discard
   buttons - both fully labelled, neither clipped.
2. Choose Save on that prompt, return to the Settings tab: the typed
   value is in the box and its tag reads "was N". Restore the fixture
   afterwards (clear the box, Save).
3. Repeat the edit, click another tab, choose Discard, return to the
   Settings tab: the box is back to its pre-edit value and the tag reads
   "default N" again.
4. Clean tab switch: open the Settings tab, touch nothing, click another
   tab. No prompt. Then scroll the tab, use the currency search box, and
   switch away - still no prompt (neither is a save-gated field).
5. Revert-to-original: type over a value, then retype the original text
   exactly (or blank a box and retype what was in it), and switch away.
   No prompt.
6. Multi-section count: edit a Homestead tier AND the snapshot interval
   AND one currency Ignore checkbox, then switch away. The prompt reads
   "3 unsaved changes" (plural).
7. Window close: edit a field on the Settings tab and click the window's
   title-bar X. NO prompt appears. Reopen the window (corner icon) - the
   Settings tab is still selected and the typed text is still in the box,
   untouched.
8. Diagnostics checkbox alone: toggle it, switch away. No prompt (it
   applies immediately). Toggle it back afterwards to restore the
   fixture.
9. Invalid entry, saved in the tab: type "abc" into a currency box, press
   Save (status reads "Saved - 1 invalid entry not saved", the row tag
   reads "Invalid"), then switch away. No prompt - the user has already
   been told, and re-prompting would loop on a value that can never be
   saved.
10. Invalid entry, saved from the prompt: type "abc" into a currency box
    AND a valid number into a second one, switch away, choose Save. A
    second dialog appears reading "1 Settings entry could not be saved -
    the value was not a valid number. Everything else was saved. Open the
    Settings tab to re-enter it?" with Open Settings / Dismiss. It must
    STAY on screen until a button is pressed - watch it for a few seconds
    and confirm it neither fades out nor closes itself. Choose
    Open Settings: the tab is selected, the valid edit is persisted, and
    the "abc" box is back to its persisted value. Restore the fixture
    (clear the second box, Save).
11. Dismiss on that second dialog closes it and leaves the module window
    interactive (the backdrop is gone), with the tab the user switched to
    still selected.
12. Rapid switching: click Settings and immediately another tab, back and
    forth several times without editing anything. No crash, no prompt, and
    the Settings tab still renders correctly when it settles.
13. The other three confirm dialogs still behave (the Dismiss reordering
    is shared chrome): Clear Cache on the main tab, Delete log file on the
    Log tab, and the own-materials Regenerate confirm - each closes on
    Confirm and on Cancel, and Escape/X still cancels, with the module
    window interactive again afterwards.

Gate: PASS (2026-08-23 sandbox session, branch build at the
review-round-2 HEAD, captures preflight/gF2a-gF2d). Typed 7 into
Karma's box and tabbed to Snapshot: the prompt appeared with the
exact dirty count ("You have 1 unsaved change on the Settings tab.
Save now, or discard and keep the last saved values?") and
Save/Discard verbs, body fully wrapped. Discard returned the tab to
the persisted state (Karma back to the greyed default-1 placeholder,
"default 1" tag). A clean tab-away raised no prompt
(dialog-region luma probe). Not staged live: the Save-from-prompt
path (mechanically the same SaveAll the Settings button runs,
live-proven in the field-fixes-1 gate), the window-close prompt, and
the invalid-entry rejection message; all pinned by
SettingsFormStateTests plus the review rounds' binary-verified
teardown ordering.
