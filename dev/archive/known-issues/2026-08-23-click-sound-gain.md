> **Frozen record - 2026-08-23, branch `click-sound-gain`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Click volume slider (click-sound-gain)

Field feedback, verbatim: the module's click sound is "VERY quiet. I can
barely hear it over my own mouse physical click sound." This section
records what the playback path actually was, the mapping that replaced
it, and the two judgment calls taken along the way.

### The measured playback path

All of the following is decompiled from the vendored binaries with
`ilspycmd` - Blish HUD 1.3.0
(`packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe`) and MonoGame
3.8.0.1641 (`packages/MonoGame.Framework.WindowsDX.3.8.0.1641`).

1. `PressFeedback.PlayClick` called
   `ContentService.PlaySoundEffectByName("button-click")`.
2. That method's body, in full: skip if `_playRemainingAttempts <= 0` or
   `GameService.GameIntegration.Audio.AudioDevice == null`, then
   `SoundEffect.FromStream(_audioDataReader.GetFileStream(name + ".wav"))
   .Play(GameService.GameIntegration.Audio.Volume, 0f, 0f)`.
3. `AudioIntegration.Volume` is `GetVolume()`, which is:
   `MathHelper.Clamp(mean(last 20 samples of the GW2 audio session's
   MasterPeakValue), 0f, 0.4f)` when "use game volume" is on (the
   default); Blish's own `Volume` setting otherwise, which is itself
   `SetRange(0f, 0.4f)` with a **0.2 default**; and a hard `0f` when
   "mute if no game audio" is on (also the default) and that mean is
   below 0.0001.
4. `SoundEffect.Play(volume, pitch, pan)` assigns straight to
   `SoundEffectInstance.Volume`, whose setter **throws
   ArgumentOutOfRangeException outside [0,1]** - it does not clamp - and
   then multiplies by the static `SoundEffect.MasterVolume`, which is
   MonoGame's untouched `1f` default (`grep MasterVolume` over the whole
   decompiled Blish assembly: no hits).

So the one input that controls loudness is that first argument, its
ceiling is **0.4**, and its everyday value is whatever the game happened
to be peaking at.

The asset itself explains the rest. `audio/button-click.wav` inside
`ref.dat` is 44.1 kHz stereo, 128 ms, 22,616 bytes, and peaks at
**0.357 of full scale** (-8.9 dBFS) with an RMS of 0.024. At Blish's
0.2 fixed default that peak lands at 0.071 (-23 dBFS); at a realistic
game-audio mean it is lower still. A -23 dBFS transient under a physical
mouse click is exactly the reported symptom.

### The mapping, and where the default came from

The module now plays the effect itself: same asset, read once from
`ref.dat`'s `audio` subpath - the same archive and subpath
`ContentService.Load` reads - decoded once into a cached `SoundEffect`,
played at a volume the user sets.

`Services/ClickSoundVolume` is the whole mapping, Blish-free and
therefore unit-tested (39 cases):

- `ToVolume(percent) = Clamp(percent, 0, 100) / 100f`, linear in
  amplitude, handed to `Play` as-is.
- `0` is not "volume 0" - `IsSilent` short-circuits before the asset
  load and before any pooled voice is taken.
- `100` is exactly `1f`, the loudest the asset can be played.
- The clamp is load-bearing, not decoration: see the throwing setter
  above.

Percent that reproduces today's loudness: **40** is the loudest today
could ever be (the 0.4 clamp), and **20** is Blish's fixed-volume
default. The shipped default is **`ClickSoundVolume.DefaultPercent =
75`** - 1.875x the absolute old ceiling (+5.5 dB) and 3.75x the old
fixed default (+11.5 dB), with headroom left above it. It sits at
-11.4 dBFS peak. That constant is the single line to edit when the
maintainer's field test returns a number; nothing else encodes a
default.

### Deliberate divergence 1: the slider is not save-gated

The Settings tab has one Save button and an unsaved-changes prompt driven
by `CaptureFormState`/`UnsavedChangeCount`. The click volume row is
**deliberately outside** that model: it writes through to
`ModuleSettings` on every `ValueChanged` - and from there to the live
player, see the two-sliders section below - exactly like the Diagnostics
checkbox (idiom (a)), and it is **absent from
`CaptureFormState`**. Auditioning a volume through a Save button - and
through a save prompt on the next tab switch - is hostile for the one
setting a user tunes by ear, and listing it in the form state would
report every drag as an unsaved change to a value already on disk. This
is the same reasoning `SettingsFormState`'s own doc comment already gives
for the Diagnostics checkbox.

Cost of writing on every change, measured: `SettingEntry.Value` ignores
an unchanged value, the TrackBar snaps to whole numbers (`SmallStep` is
off), and `SettingsService.Save()` only sets a dirty flag - the JSON
write is debounced until 4 seconds past the last change.

### Deliberate divergence 2: no game-volume coupling

Playing the effect ourselves gives up Blish's game-derived volume,
including its "mute if the game makes no sound" rule. Kept anyway, on
purpose: that rule is the cause of the complaint, and its zero case is
not only a muted game - the peak buffer also reads zero when GW2 is not
running or simply is not making noise at that moment, which would leave
the Settings tab's own Test button dead exactly when someone is setting
the volume with the game quiet. The user-facing mute is the slider's own
0.

The no-audio-device guard is kept, reading a different signal.
Blish tests `Audio.AudioDevice == null`, whose type is NAudio's
`MMDevice`; referencing it here pulls the whole `NAudio.Wasapi` assembly
into the module for one null check (measured: it is a `CS0012` without
it). MonoGame answers the same question in a type the module already
references - `SoundEffect`'s stream constructor throws
`NoAudioHardwareException` when the sound system failed to initialize -
and the loader turns that into a permanent, quiet give-up.

### Two Blish behaviors this row had to work around

- **Nothing in a Blish teardown disposes this tab's controls.**
  `ViewContainer.DisposeControl` runs `Clear()` - and so
  `Container.ClearChildren`, which only sets each child's `Parent` to
  `null` - *before* `base.DisposeControl()`. `Container.GetDescendants`
  is a lazy iterator that enqueues a container's children only after the
  caller has already disposed it, so the walk that disposes the
  `ViewContainer` then finds it empty: disposing the host window
  disposes nothing underneath this tab. `TrackBar` is the one control
  type used here that subscribes to a **static** event in its
  constructor (`Control.Input.Mouse.LeftMouseButtonReleased`, released
  only in its `DisposeControl` override), so the slider is disposed
  explicitly on **both** exits - the previous cycle's at the top of
  `Build`, and the last one from `SettingsTabContent.Teardown`, called
  by `Module.Unload`. Disposing only on rebuild would have left the
  final slider on Blish's mouse handler - and, through its
  `ValueChanged` closure, the entire `SettingsTabContent` graph - for the
  rest of the Blish process, accumulating one graph per module
  disable/re-enable. Swept the other control types used here:
  `TextInputBase` takes its global hooks on focus and releases them on
  unfocus; `Checkbox`, `StandardButton`, `Panel` and `Label` take none.
- `TrackBar.MinValue`/`MaxValue` are assigned even though 0 and 100 are
  already the defaults. Their setters are the only callers of the
  private `MinMaxChanged`, which fills the ten-increment table that
  Ctrl+drag snaps against with `Enumerable.Aggregate`; on a TrackBar
  that never had either assigned that table is empty and the first
  Ctrl+drag throws.

### There are TWO sliders for this setting, and the wiring assumes it

This module never overrides `Module.GetSettingsView`. Blish's default
returns `new SettingsView(ModuleParameters.SettingsManager.ModuleSettings)`,
which renders **every** `SettingEntry` the module defines - and a
`SettingEntry<int>` renders as `IntSettingView : NumericSettingView<int>`,
whose `BuildSetting` builds its own 277x16 `TrackBar`. So Blish's Manage
Modules pane already shows a second, fully draggable 0-100 click-volume
slider, and it is the one the maintainer may well reach first.

Everything therefore hangs off the **setting**, not off either control:

- `SettingEntry.ValueChanged` -> `Module.OnClickSoundVolumeChanged` ->
  `ClickSound.VolumePercent`. Without this the Blish-side slider would
  persist a value that did not take effect until the next relaunch.
- `SettingEntry.ValueChanged` -> `SettingsTabContent.
  OnClickVolumeSettingChanged` -> this tab's slider and "NN%" readout, so
  a drag in the other pane does not leave this tab displaying a value
  that is no longer true.
- This tab's own `ValueChanged` writes the setting and **nothing else**;
  the two hops above do the rest. One path, whichever slider moved.

The loop terminates: the setting-side handler skips the slider write when
the slider already rounds to that percent, and when it does write, the
`ValueChanged` it raises writes back an unchanged setting, which
`SettingEntry` does not re-announce.

Both subscriptions are dropped on unload (`Module.Unload`,
`SettingsTabContent.Teardown`) and that is not optional:
`SettingsManager` hands out `module.State.Settings`, and
`SettingCollection.DefineSetting` returns the **existing** entry for a
key it already holds - so a disable/re-enable cycle re-defines onto the
same objects, and an unsubscribed handler would root each dead `Module`
in turn.

**Swept the sibling settings for the same defect.** The shape that breaks
is "pushed once at `Initialize` into a live object, and otherwise only by
this tab" - every setting Blish also renders has that second UI, so any
setting with that shape was already silently ignoring it.

- `LogDiagnosticsEnabled` - **same defect, fixed the same way.** It is
  pushed into `ModuleLog.Shared.DiagnosticsEnabled`, so toggling Blish's
  own checkbox for it persisted without taking effect until relaunch.
  Now `Module.OnLogDiagnosticsEnabledChanged` carries it, and this tab's
  checkbox writes the setting only.
- `SnapshotRefreshIntervalMinutes` - not affected. The stale-check tick
  re-reads it from the setting on every `Update`.
- `ScrollDiagnosticsEnabled` - not affected. `CraftingPlanView` reads it
  from the setting at each use.
- `LogMaxSizeBytes` - **same defect, and the first sweep got this one
  wrong.** It is not once-per-session: `ModuleLog.Configure` only seeds
  it, `ModuleLog.MaxFileSizeBytes` is a live-settable property that every
  file write re-reads for its self-trim check, and the Settings tab was
  already pushing it there on Save with a comment saying exactly why. So
  the tab's own Save applied immediately while a drag of the TrackBar
  Blish renders for the same entry persisted a value the running file
  sink ignored for the rest of the session. Now
  `Module.OnLogMaxSizeBytesChanged` carries it and the tab's Save writes
  the setting only, same shape as the two above. The handler reads
  `GetClampedLogMaxSizeBytes()` rather than the raw new value, which is
  load-bearing here and not just symmetry: `IntSettingView.RefreshValue`
  widens the bar to `Math.Max(MaxValue, value)` and leaves `MinValue` at
  0, so Blish's bar for this entry spans 0 to the persisted byte count
  and can hand over a few hundred bytes - far under the 1 MB floor the
  tab's own parser enforces.
  Residual, deliberately not fixed: the tab's size TextBox does not
  follow a Blish-side drag, so a Save there afterwards writes back what
  the box still shows. Live-refreshing it would clobber a half-typed
  entry, and the Save is an explicit user action; last writer wins, as
  it did before.
- `LogRetentionDays` - excluded, and this one holds. Age-based pruning
  runs exactly once, in `Module.Initialize`, so a change applies next
  session regardless of which UI made it. Nothing holds a live copy to
  keep current.

### The setting does not reach checkboxes, and cannot cheaply

Swept every Blish control the module instantiates for one that plays a
sound of its own. Measured from the 1.3.0 decompile - the controls that
call `PlaySoundEffectByName` are `Checkbox`, `ColorBox`, `CornerIcon`,
`GlowButton`, `MenuItem`, `StandardButton`, `TabbedWindow` and
`WindowBase`; of those the module uses `Checkbox` (7 sites:
`SettingsTabContent` x2, `CraftingPlanView` x3, `MainView`,
`LogTabContent`), `CornerIcon` (1), `StandardButton` (every
`FeedbackButton`) and `TabbedWindow2`/`WindowBase` as its window chrome.

- `StandardButton` is already mute - it passes `"audio\\button-click"`
  to a reader already rooted at `audio`, so the `FileExists` check fails
  and it returns silently. That bug is why `FeedbackButton` exists, and
  it is what makes the module's buttons fully controlled by the slider.
- `Checkbox` (`OnLeftMouseButtonReleased`) and `CornerIcon` (`OnClick`)
  are **not** covered. They play `"button-click"` at
  `GameIntegration.Audio.Volume` - the same quiet, game-derived path
  this branch replaced everywhere else.
- Window/tab chrome (`window-close`, `tab-swap-N`) is Blish's own and
  out of scope for a module setting.

So at 0 the module is silent except for a checkbox tick and the corner
icon, and at 100 a checkbox is audibly quieter than a button in the same
window. That is a real seam and the field test will meet it.

Why it is not fixed here: the sound sits **inside** those overrides,
ahead of the base call. `Checkbox.OnLeftMouseButtonReleased` is
`if (Enabled) { Content.PlaySoundEffectByName("button-click"); }
base.OnLeftMouseButtonReleased(e);`, and `Control`'s base method is what
raises `Click` (via `_clickPrimed`/`_lastClickTime`, both private). A
subclass that skipped the base to skip the sound would break clicking;
there is no hook, no volume argument and no static to swap. The only
full fix is a module-owned checkbox: `LabelBase` is public, `DrawText`
and `LabelRegion` are protected, and `Checkable.TextureRegionsCheckbox`
is public, so Blish's own 25-line `Checkbox` can be reimplemented
verbatim minus the sound - but that is a new control plus 7 call-site
changes across 4 view files, which does not belong in a branch whose
subject is a volume slider. Deferred, with the recipe recorded here.

### Incidental: this is now cheaper per click, not dearer

`PlaySoundEffectByName` re-read and re-decoded the 22 KB wav into a
brand-new `SoundEffect` on **every** press and never disposed any of
them. The module now decodes once and reuses one cached effect, disposed
on module unload (statics outlive a module instance inside one Blish
session, same as `TooltipFacility`).

### Gate items

1. The Settings tab's new **Sound** section renders at the top: label,
   slider, "NN%" readout, Test button, none overlapping, and the slider
   drags with the readout tracking it live.
2. The **Test** button audibly plays the click, and its loudness follows
   the slider - clearly louder at 100 than at 25.
3. **0 is silent for the clicks this module plays**: drag to 0 and
   neither the Test button nor any other button, row or pill in the
   module makes a sound. Checkboxes and the corner icon are knowingly
   NOT covered - they still tick at Blish's own volume (see the section
   above); hearing those at 0 is expected, not a gate failure.
4. The value **survives a relaunch**: set something distinctive (say 40),
   close Blish, reopen, and the slider, the readout and the actual
   loudness all come back at 40.
5. The **other** slider works too: with this tab open, drag the click
   volume slider Blish renders under Manage Modules and confirm the next
   click is immediately at the new loudness (no relaunch) and that this
   tab's slider and readout follow it.
6. **Log size cap, same wiring, no regression**: with the Settings tab's
   Logging section on 1 MB, Save, and confirm `data/module_log.jsonl`
   still trims at the saved cap this session (the tab no longer pushes
   it directly - `Module` does).
7. Report the number that feels right - it replaces `DefaultPercent`.

Gate: PASS on the render half (2026-08-23 night desktop session,
captures preflight/gSND1-gSND2): the Sound section renders first on
the Settings tab with the Click volume label, the TrackBar at the 75
default, the live "75%" readout, the Test button beside it, and the
instant-apply/zero-off/checkbox-exception prose. The audible half -
how loud 75 actually feels, the Test button's playback at the dragged
value, silence at 0, persistence across a relaunch - is the
maintainer's field check by nature (the sandbox cannot hear); the
percent-to-volume mapping and clamps are pinned by
ClickSoundVolumeTests. The maintainer's number becomes the new
DefaultPercent in a one-line change.
Morning re-run (2026-08-24, captures preflight/gM9-gM14): thumb DRAG
moves the value with the readout live-updating (75 -> 21), the value
SURVIVED a full Blish relaunch (restored at 21), and the slider was
returned to ~the default afterwards. Notes: click-on-track does not
jump and the wheel scrolls the panel, not the slider - both stock
Blish TrackBar behavior. Only audibility remains with the maintainer.
