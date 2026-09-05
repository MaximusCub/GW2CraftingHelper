> **Frozen record - 2026-08-23, branch `spinner-feedback`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Spinner and button feedback (spinner-feedback)

Two requirements reported in game:

- Replace the module's ASCII-text loading spinner with Blish's own
  circular animated spinner, the one drawn under the module icons in the
  top-of-screen overlay while they load.
- Give buttons click feedback, visual and audio: shade or depress the
  button on mousedown, unshade on release, and play a sound, so a click
  reads as registered.

Everything below marked "measured" was read out of the vendored Blish
HUD 1.3.0 binary (`packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe`,
decompiled with `ilspycmd`) and out of Blish's asset archive
(`C:\Blish.HUD\ref.dat`, a plain zip).

**The spinner, measured.** `Blish_HUD.Controls.LoadingSpinner` is a
public `Control` with a parameterless constructor whose entire body is
`Size = new Point(64, 64)`. Its `Paint` does one thing: hand its own
bounds to `Blish_HUD.LoadingSpinnerUtil.DrawLoadingSpinner`, which draws

    spriteBatch.DrawOnCtrl(control, _loadingSpinnerTexture, bounds,
        new Rectangle((int)(GameService.Overlay.CurrentGameTime
            .TotalGameTime.TotalSeconds * 21.3333) % 64 * 64, 0, 64, 64));

so:

- The texture is `GameService.Content.GetTexture("spinner-atlas")`.
  `spinner-atlas.png` in ref.dat measures 4096x64 - 64 frames of 64x64,
  laid out horizontally.
- The frame index is derived from GLOBAL game time at 21.33 frames/sec
  (a 3.0s loop), not from per-control state. The animation therefore
  costs us no ticker, cannot be paused or restarted, and starts at
  whatever phase the clock is at when the control becomes visible.
- The source rect is a fixed 64x64 but the destination is `bounds`, so
  the control renders correctly at ANY size. Nothing needed cropping;
  only the 64x64 default had to change, since neither status row here is
  anywhere near 64px tall.
- It can simply be constructed and parented - no service registration,
  no atlas handling, no disposal of ours (the texture is a static field
  on LoadingSpinnerUtil, shared by every instance).

**Where ours went.** The rotating ASCII glyph was
`CraftingPlanView.SpinnerFrames` (`'|' '/' '-' '\'`), appended to the
plan strip's status text by `RenderFromBoard` and advanced once per
150ms by `SpinnerTick`. It is gone; `SpinnerTickInterval` and
`_lastSpinnerTickUtc` stay, now rate-limiting only the strip's text
re-render (writing an `AutoSizeWidth` Label's `Text` re-triggers a text
measure, so the ticker must not do it 60x/sec for a whole generation).
The Snapshot tab had no spinner at all - "Refreshing..." sat there
static - so it gained one for the life of a Refresh Now.

**Row fit, measured.** The plan strip's status row is exactly
`TopRegionLayoutMath.StatusToSeparatorGap` = 21 logical px (status label
Y to separator Y); the spinner is 18. The Snapshot tab's status row is
its own 24px `_statusPanel`; the spinner is 20. Both sizes and the
label-trailing placement arithmetic live in the Blish-free
`Services/InlineSpinnerLayout`, with tests pinning both fits and the
"tracks an AutoSizeWidth label's right edge" property.

The spinner trails the status text rather than leading it, for the same
reason the ASCII glyph did: the phase text then always lays out from the
label's fixed x=0 origin and only the spinner moves.

**Button feedback, measured.** The report is accurate, and
it is not only about our custom controls - Blish's own `StandardButton`
is worse than it looks:

- Hover works. `OnMouseEntered`/`OnMouseLeft` tween `AnimationState`
  between 0 and 8 over 0.25s, stepping through the
  "common/button-states" atlas.
- Press does nothing. There is no `OnLeftMouseButtonPressed` override
  and no pressed frame in that atlas walk. A held button is pixel-
  identical to a hovered one.
- The click sound is dead code. `OnClick` calls
  `Control.Content.PlaySoundEffectByName("audio\\button-click")`, but
  `ContentService.Load` builds its audio reader as
  `zipArchiveReader.GetSubPath("audio")` and `PlaySoundEffectByName`
  looks up `Path.Combine(_subPath, soundName + ".wav")`. The name
  therefore resolves to `audio/audio/button-click.wav`, which is not in
  ref.dat (`audio/button-click.wav` is), the `FileExists` guard rejects
  it, and the method returns without playing or logging. Blish's
  `Checkbox`, `GlowButton` and `CornerIcon` pass the unprefixed
  `"button-click"` and DO play - which is why checkboxes on the Settings
  tab click and buttons next to them do not.

So the gap is press shading on everything, plus sound on every
StandardButton. `Views/Rendering/PressFeedback` is the single helper:
on `LeftMouseButtonPressed` it captures the control's own `Opacity`,
multiplies it by 0.8, and plays `"button-click"` (unprefixed, at
`GameService.GameIntegration.Audio.Volume`, which `PlaySoundEffectByName`
applies itself); on `LeftMouseButtonReleased` AND on `MouseLeft` it
restores the captured value.

Two decisions worth keeping:

- It writes `Opacity`, not `BackgroundColor` or `TextColor`. Every
  click target here already owns a different hover vocabulary on a
  different property (a decision pill swaps BackgroundColor to white, a
  sortable header swaps TextColor, a tree row and a section header each
  swap to a different translucent wash). A helper writing those same
  properties would have to restore a resting value the site's own
  MouseLeft handler is also writing, making correctness depend on which
  handler was subscribed first. Nothing else touches Opacity on any of
  these controls, so this composes with all four schemes, and measured
  `Control.AbsoluteOpacity()` walks the parent chain into every
  `DrawOnCtrl`/`DrawStringOnCtrl` call - which is what makes a 20% dim
  legible on a target whose own background is transparent, since its
  labels and icons dim with it.
- `MouseLeft` restores as well as release, because Blish routes mouse
  events to the control under the cursor: a press dragged off the target
  is delivered `MouseLeft` and never a release.

The restore runs before the click fires - measured,
`Control.OnLeftMouseButtonReleased` raises `LeftMouseButtonReleased`
first and only then calls `OnClick` - so a button that disables itself
in its own handler (Generate) cannot be left stuck dim.

**Coverage.** All 14 of the module's buttons, via `FeedbackButton`, a
`StandardButton` subclass that wires itself in its constructor;
decision pills in both interactive arms (never the dead-click ones);
expandable tree rows (leaf rows are not clickable and get nothing);
plan section headers; sortable table headers; suggestion rows. Blish's
checkboxes, dropdowns, text boxes and tab strip already have working
sound and were left alone.

If a later Blish release fixes the double-prefixed path in
`StandardButton.OnClick`, `FeedbackButton` will play the sound twice on
a completed click, and the `PlayClick()` call in `PressFeedback.Wire` is
what to drop.

**Nested click targets (review round 1).** Measured:
`Container.TriggerMouseInput` calls `base.TriggerMouseInput` - which
raises the container's OWN mouse events - BEFORE it walks its children,
and the deepest child only wins the RETURN value (ActiveControl) and the
suppression of its siblings. So a press inside a wired child reaches the
wired parent as well. Exactly one such nesting exists in the module: a
decision pill inside an expandable tree row, which would otherwise have
played two click sounds and dimmed two controls for one press. The row's
click handler already had a "bail if a pill is hovered" guard for the
same reason; `PressFeedback.Wire` takes an optional suppression
predicate and the row now passes that same guard, extracted to
`TreeSectionController.AnyPillHovered` so the two cannot drift. No other
wired control is a descendant of another - the labels inside a tree row
and inside a section header do receive the events, but they carry
tooltips, not press wiring.

### Sandbox check

1. Plan tab, press Generate Plan on a real multi-item request: a
   circular painterly spinner turns to the right of the status text for
   the whole run, at roughly one revolution every 3 seconds, and
   disappears the instant the final "Plan generated..." text lands. The
   phase text itself must not jitter horizontally as it changes.
2. Switch to another tab mid-generation and back: the spinner is still
   turning next to the live phase text (the strip re-arms from the
   status board on rebuild).
3. A generation whose status carries a standing notice (leave one item
   row with unresolvable typed text, then Generate): confirm the longer
   composed status text has not pushed the spinner off the right edge of
   the window at the minimum window width. If it has, that is a real
   finding - the spinner is anchored to the label's right edge with no
   clamp.
4. Snapshot tab, Refresh Now: a spinner turns beside "Refreshing..."
   and stops when the timestamp lands. Switching tabs mid-refresh and
   back leaves it still turning.
5. Press-and-hold a decision pill in the Recipe Tree: the pill visibly
   dims while held and returns to its hover white on release. Press it,
   drag off it without releasing, and confirm it returns to its resting
   border color rather than staying dim or staying white.
6. Press-and-hold a sortable column header (e.g. Shopping List's Item):
   the header text visibly dims while held, returns to the hover tint on
   release, and the sort still applies once.
7. Press-and-hold a plan section header and an expandable tree row: the
   whole row dims, including its labels and icons, and restores on
   release.
8. Press-and-hold Generate Plan, then any Settings tab button: the
   button dims while held and restores on release. Disabled buttons
   (Generate during a run) must NOT dim or click.
9. Press a decision pill on a row that also expands: ONLY the pill
   dims, not the whole row behind it, and the press produces one click
   sound rather than two.
10. Sound is NOT verifiable in the muted dummy session used for these
   captures - the screenshot harness runs Blish with no audio device, in
   which case `PlaySoundEffectByName` returns at its first guard. The
   click sound needs a live audio check; what IS
   verified here is only that the asset exists (ref.dat contains
   `audio/button-click.wav`, 22,616 bytes uncompressed) and that the
   name passed is the unprefixed one Blish's own working callers use.

Gate: PASS (2026-08-24 sandbox sessions, captures
preflight/gC1-gC14). (1) SPINNER: "Building recipe tree..." rendered
with Blish's golden circular spinner inline in the plan strip, and
two captures ~350ms apart show DIFFERENT rotation frames - the
atlas animation live. (2) PRESS FEEDBACK, measured numerically on
the Expand All FeedbackButton: hover luma 165.6 -> held 143.3
(~13% press-dim while the mouse is down) -> released 165.6, an
exact restore. A pill press was not stageable (the session's plan
was all-owned - its only pill is the HAVE chrome, correctly
non-interactive); pill wiring shares the same PressFeedback helper
verified on the button. (3) Sound not verifiable in the muted dummy
session (PlaySoundEffectByName no-ops without an audio device) -
the corrected unprefixed asset name is measured fact; the
first real click in game is the audio gate. BONUS gate-found
fix folded into this branch: the persisted +24 Agony plan exceeded
Newtonsoft's default read MaxDepth of 64 and silently failed to
restore - raised to 512 with a mutation-checked 30-level real
pipeline round-trip test, and the deep plan then RESTORED live
("Recipe Tree (47)", 147,639g total) in this gate's own session.
