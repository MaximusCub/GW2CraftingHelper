> **Frozen record - 2026-08-06, branch `field-test-ux-wave-2-mysticforge-sublabel-drop-fix`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Field-test UX wave 2: MysticForge sublabel drop fix (2026-08-06)

One pre-investigated display-layer fix, following up on field-test wave
finding E above. Display-layer only, no InventoryReducer/PlanSolver/
VendorBatchSolver changes.

**MysticForge silently dropped from a mixed-discipline sublabel.**
`FormatDisciplineSublabel`'s planDiscNames intersection ran "MysticForge"
through the same filter as any other discipline, but planDiscNames can
never contain "MysticForge" in production (`PlanResultBuilder.
NonCraftingDisciplines` strips it out of every option's Disciplines list
before `disciplineMap`/`RequiredDisciplines` is built). So a recipe whose
own Disciplines combined "MysticForge" with a genuine leveled discipline
(not seen in real game data today, but not structurally impossible) had
the forge silently dropped from its sublabel - the intersection kept only
the real discipline, and the sole-MysticForge special case never matched
because "MysticForge" was no longer present in the filtered list. Fixed by
splitting the MysticForge flag out of `recipeDisciplines` before the
planDiscNames intersection runs (so it is never subject to that filter at
all) and always re-prepending "Mystic Forge" to the display text when the
flag is set, so it can no longer be silently dropped regardless of what
planDiscNames does or does not contain. The pre-existing regression test
for this combined case hand-fed a planDiscNames value the real pipeline
could never produce (`{"MysticForge", "Weaponsmith"}`), which let it pass
while validating a codepath no real caller could reach; updated to the
real production shape (`{"Weaponsmith"}` only) and added a companion
end-to-end test that goes through the real `BuildPlanDiscNames` path via
`_builder.Build(result)`, matching the pattern already used by the
adjacent sole-MysticForge end-to-end test.

Validation: `dotnet build -p:Platform=x64` clean (0 errors); both test
suites green - module suite 1115 passed (was 1114; +1 new test,
`PlanViewModelBuilderSublabelTests.
CraftingSteps_Sublabel_MysticForgeWithRealDiscipline_RelabelsButKeepsLevel`),
VendorOfferUpdater.Tests 135 passed (untouched, unaffected). No new Blish
HUD references in tests; the new test exercises real production code
(`PlanViewModelBuilder.Build`/`FormatDisciplineSublabel`) with no
contract-mirror/fake-logic tests.

Live desktop gate: PASS (2026-08-06, same session as the
wave-1 gate above): the corrected sublabel path rendered live - recipes
whose only source is the forge show "Mystic Forge" with no level, and
the mixed-discipline safeguard is additionally covered by the end-to-end
test; no MysticForge row in Required Disciplines (count header (3)
consistent).

### 37. Snapshot refresh failure classification + "GW2 API access is not ready" dialog (2026-08-06)

**Field evidence:** a real user hit the Snapshot tab's Refresh Now while at
the GW2 CHARACTER SELECT screen (not yet in-world) - Blish only resolves the
account's Mumble identity, and therefore its API key, once a character is
loaded into the game world, so every account data source call failed with
an invalid/missing access token. The tab's only feedback was the bare
"Refresh Failed - {time}" label, giving no hint that logging into a
character (not restarting Blish, not re-adding the API key) was the actual
fix - a full hour lost to this before the cause was found.

**Fix:** `Services/SnapshotFailureKind`/`SnapshotFailureClassification`/
`SnapshotFailureClassifier` (Blish-free, unit-tested) classify a failed
refresh into `ApiAccessNotReady` (invalid/missing/under-scoped token -
matched by exception TYPE NAME string, not a Gw2Sharp `is` check, so the
classifier itself never needs a Gw2Sharp reference), `NetworkOrApiDown` (a
total failure with a known transport/5xx cause), `PartialFailure` (some
sources succeeded), or `Unknown`. `Gw2AccountSnapshotService` (the one file
allowed to reference Gw2Sharp) now threads each failed source's exception
type name through `SnapshotFetchFailedException`'s new
`FailedSourceExceptionTypeNames`. `Views/MainView.cs`'s Refresh Now handler
(refactored into `RefreshNowAsync()` so both the button and the new
dialog's Retry button share one code path) shows a new
`Views/ApiAccessDialog` - the three checks (in-world, API key added, module
has permission) plus Retry/Close - on `ApiAccessNotReady`, and otherwise
appends a classified cause to the status label (e.g. "Refresh failed -
could not reach the GW2 API", "Refresh partially failed - 2 of 5 sources")
instead of the bare message. `ApiAccessDialog` follows the existing
`ModalDialog` StandardWindow construction technique but is a separate class
(different title/buttons/multi-line wrapped content - via
`DrawUtil.WrapText`, the `AboutTabContent.AddInfoLine` fix pattern - rather
than a generalization of `ModalDialog`'s single-sentence Confirm/Cancel
shape) and skips its settings-backed position persistence (a rare
error-path dialog, always re-centered on `Show()`).

**Validation:** `dotnet build -p:Platform=x64` - 0 errors. Module test
suite (`tests/GW2CraftingHelper.Tests`) - 1130/1130 passing (up from the
1101 floor; +29 new tests: 22 in the new `SnapshotFailureClassifierTests`,
+3 `SnapshotFetchFailedExceptionTests`, +4 `StatusTextTests`).
VendorOfferUpdater suite (`tests/VendorOfferUpdater.Tests`) - 135/135
passing, unchanged (this branch does not touch that tool). The
classification decision logic is real-unit-tested end to end (priority
ordering, all four kinds, both the raw-type-name and `Classify(Exception)`
entry points); `ApiAccessDialog`/`MainView` are Blish HUD UI code with no
test net (repo invariant: tests must stay Blish-free) - not yet visually
verified in a live Blish session.

**Live gate:** PASS (2026-08-06, live branch-build sandbox
session, captures pop_01-03 in preflight/captures): Refresh Now with no
API key raised the classified ApiAccessNotReady dialog - title "GW2 API
access is not ready", the three checks rendered
verbatim (in-world character with the Mumble explanation, key registered
in Blish, module permission path), Retry re-fired the refresh (status
timestamp advanced behind the still-open dialog on repeat failure),
Close dismissed cleanly; the status line shows the classified
"Refresh failed - GW2 API access not ready - <time>" in place of the
bare "Refresh Failed". Zero FATAL lines in the session log.

---

### 38. Review-pass fixes on item 37: ApiAccessDialog self-defense + background-refresh status parity (2026-08-06)

**Context:** a review of item 37's dialog/classification landing (before
its own live gate above was exercised) found three gaps in the diff, all
fixed in this follow-up pass without touching the pure classification
logic itself.

**Fix 1 (`Views/ApiAccessDialog.cs`):** `Show()` had no defensive check of
its own for having already been `Dispose()`'d - it relied entirely on
`Views/MainView.cs`'s `_headerPanel == null || _headerPanel.Parent == null`
guard, a proxy check over an unrelated, Module-owned object's lifecycle
that only happened to also protect `ApiAccessDialog` today because
`Module.Unload()` disposes both back-to-back in one synchronous method -
coincidence of ordering, not design. `Show()` and `Hide()` now check a new
private `_disposed` flag (set at the top of `Dispose()`) and return early;
`Dispose()` itself is now idempotent (a second call is a no-op) rather
than risking a double-dispose of the underlying `StandardWindow`.

**Fix 2 (`Views/ApiAccessDialog.cs`):** the dialog never reset
`_isShowing` when dismissed via the window's own built-in title-bar X
button or Escape key (`CanClose`/`CanCloseWithEscape` both default true
and were never overridden) - both bypass the Retry/Close `StandardButton`
click handlers entirely (confirmed by decompiling the shipped Blish HUD
assembly: `WindowBase2.OnLeftMouseButtonPressed` calls `Hide()` directly
on an X-button click), so `_isShowing` stayed stuck true and every later
`Show()` call silently no-op'd for the rest of the session - reproducing
the exact "no explanation, dead end" pain item 37 exists to fix. The
constructor now subscribes `_window.Hidden += OnWindowHidden` (a named
handler, matching `ModalDialog`'s own `OnWindowMoved` idiom; unsubscribed
in `Dispose()`), which fires whenever the window's Visible=false
transition completes regardless of which path triggered it.

**Fix 3 (`Module.cs`):** `RefreshSnapshotInBackgroundAsync`'s catch block
(the auto-refresh path fired on module load, every stale-snapshot
`Update()` tick, and `OnSubtokenUpdated`) still wrote only the bare
"Refresh failed - {time}" status, even though it can hit the exact same
`InvalidAccessTokenException` root cause as the manual Refresh Now
button - a returning user who alt-tabs to character select while a
cached snapshot goes stale would silently get the old, uninformative
message through a path they never clicked anything for. It now calls the
same `SnapshotFailureClassifier.Classify(ex)` +
`StatusText.ForRefreshFailure(...)` pair `Views/MainView.cs`'s
`RefreshNowAsync` already uses, for status-text parity. Deliberately does
NOT pop `ApiAccessDialog` from this background path: showing a top-level
window unprompted while the user is off doing something else in-game
(rather than having just clicked Refresh Now themselves) is a separate,
more debatable UX call, left open rather than folded into this fix pass.

**Validation:** `dotnet build -p:Platform=x64` - 0 errors. No new
StyleCop warning categories introduced; the two new early-return guards
(`if (_disposed) return;`) add two more instances of the file's own
pre-existing braceless-if style (matching the sibling `if (_isShowing)
return;` line right next to each), already covered by the SA1503 debt
item 32 tracks - not a new class of finding. Module test suite
(`tests/GW2CraftingHelper.Tests`) - 1130/1130 passing, unchanged from
item 37's count: no pure/Blish-free logic changed in this pass, only its
call sites (`Module.cs`) and view-layer plumbing (`ApiAccessDialog.cs`),
both Blish HUD code with no test net under the repo's own Blish-free
testing invariant - `SnapshotFailureClassifier`/`StatusText` themselves
remain fully covered by their existing real unit tests, unmodified.
VendorOfferUpdater suite (`tests/VendorOfferUpdater.Tests`) - 135/135
passing, unchanged (this pass does not touch that tool). Both floors
(1101+/135) cleared. `ApiAccessDialog`/`Module.cs` remain untested-by-
design UI/host code - not yet visually verified in a live Blish session.

**Live gate:** PASS (2026-08-06, live branch-build sandbox
session, captures pop_01-03 in preflight/captures): Refresh Now with no
API key raised the classified ApiAccessNotReady dialog - title "GW2 API
access is not ready", the three checks rendered
verbatim (in-world character with the Mumble explanation, key registered
in Blish, module permission path), Retry re-fired the refresh (status
timestamp advanced behind the still-open dialog on repeat failure),
Close dismissed cleanly; the status line shows the classified
"Refresh failed - GW2 API access not ready - <time>" in place of the
bare "Refresh Failed". Zero FATAL lines in the session log.

---
