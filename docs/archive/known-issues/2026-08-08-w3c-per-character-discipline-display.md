## W3C: Per-character discipline display (2026-08-08)

User-directed, field-test feedback (gw2efficiency parity): the Required
Disciplines section of a generated plan listed each required discipline
and its minimum rating, but never said WHO on the account could actually
craft it. Implemented in the isolated `wt-w3c` worktree, STACKED on the
unmerged `w3b-generation-progress` branch (base commit `1ffaa65`) on
branch `w3c-character-disciplines`.

**1. Snapshot capture (`Models/SnapshotCharacterDiscipline.cs`,
`Services/Gw2AccountSnapshotService.cs`).** A new `SnapshotCharacterDiscipline`
model (`CharacterName`, `Discipline`, `Rating`, `Active`) and a new
`AccountSnapshot.CharacterDisciplines` list, captured inside the same
per-character loop that already fetched each character's inventory.
Per-character fetch: the existing narrow `V2.Characters[name].Inventory.
GetAsync` call for items, plus a second, separate `V2.Characters[name].
Crafting.GetAsync` call for the discipline signal - both need only the
already-required `account`/`characters`(/`inventories`) scopes, no new
permission requirement (**review-fix, see item 4**: an initial version
combined both signals into one round trip via the fuller `V2.Characters
[name].GetAsync` record; reverted back to two lean endpoints, since the
full record's extra recipe/equipment/build-tab payload widened this
cosmetic feature's failure surface onto plan-affecting inventory data).
Inventory failures are tolerated per-character exactly like the pre-W3C
code always has (a conservative item under-count, never a false claim);
a crafting-fetch failure for ANY character instead nulls
`CharacterDisciplines` for the WHOLE snapshot, discarding entries already
gathered from other, successfully-fetched characters too - a partial list
would read as an affirmative "not trained on any character" claim for a
discipline the fetch simply never reached, exactly the case this
null-vs-empty distinction exists to prevent (see item 4). The outer
character-LIST fetch failing is the only thing that fails the whole
snapshot, unchanged from pre-W3C. Every learned discipline is captured
regardless of `Active` (GW2 only
allows 2 concurrently active disciplines per character, but a levelled
rating persists on an inactive one), using `CharacterCraftingDiscipline.
Discipline.RawValue` (not the Gw2Sharp enum's `.Value`/`.ToEnumString()`)
so the captured string matches `RequiredDiscipline.Discipline`'s own
plain-string shape (from `Recipe.Disciplines`) byte-for-byte, including
for a discipline value the enum does not recognize.
`AccountSnapshot.CharacterDisciplines` is deliberately NOT defaulted to
an empty list like `Items`/`Wallet` - null ("never captured": a pre-W3C
snapshot.json, or a snapshot from before the character-list fetch even
started) is a distinct, meaningful state from a non-null empty list
("captured, and it came back empty"), preserved end-to-end through
`SnapshotStore`'s existing Newtonsoft (de)serialization with zero store
changes needed.

**2. Display (`Models/CraftingPlanResult.cs`, `Models/PlanSolveContext.cs`,
`Services/CraftingPlanPipeline.cs`, `Services/PlanViewModelBuilder.cs`,
`Views/Rendering/DisciplinesSectionRenderer.cs`).** `CraftingPlanResult.
CharacterDisciplines` is a straight passthrough of the snapshot (same
"cosmetic, never fed into solving" shape as the existing M34-B2a
`OwnedCurrencyAmounts` pattern), threaded through both `GenerateStructuredAsync`
overloads (single- and multi-item) and snapshotted onto `PlanSolveContext`
so a local `ResolveWithOverrides` re-solve keeps showing it without a
network round trip. `PlanViewModelBuilder.BuildDisciplinesSection` adds a
new `PlanRowViewModel.CharacterAvailabilityText` per discipline row via
`BuildCharacterAvailabilityText`: characters that have the discipline are
listed highest-rating-first as `"Anna (500), Bob (400)"`; a character
below the row's required `MinRating` gets the `"Bob (400/450)"` slash-min
suffix instead of being hidden or miscounted as sufficient; a discipline
nobody has yields the plain `"Not trained on any character"` string
(never silently blank); a snapshot with no character-crafting data at all
(old snapshot / degraded fetch - `CharacterDisciplines` null) yields a
null `CharacterAvailabilityText`, and the renderer shows nothing extra
for that row rather than fabricate a claim either way. `DisciplinesSectionRenderer.
CreateDisciplineRow` renders the non-null text as a secondary
(`DefaultFont12`, grey) label between the discipline name and the
right-aligned Level column, ellipsized to whatever room is left via the
same `LabelHelpers.EllipsizeToWidth` + tooltip-on-truncate convention
`UsedMaterialsSectionRenderer` already uses, with the full text on hover
via `BasicTooltipText` when truncated. No new layout machinery: the row
stays the existing fixed `PlanContentHeightMath.DisciplineRowHeight`
(32px, untouched), and the new label's X position is fixed at build time
(it sits after the discipline name, whose text never changes on resize)
so only a settle-time re-ellipsis (`ISectionRelayoutSink.AddReellipsis`),
never a reposition, is needed when the panel is resized. **Review-fix,
see item 4:** `Module.cs`'s wiring used to pass a fully-null
`AccountSnapshot` (not just null owned-materials data) whenever "Use Own
Materials" was unchecked, silently dropping this whole cosmetic feature
along with it; and `PlanResultBuilder`'s pre-existing multi-discipline
greedy-cover tiebreak (unrelated to the passthrough above, but directly
feeding `BuildCharacterAvailabilityText`'s "not trained" claim) picked
alphabetically among equally-covering disciplines with no account
preference, so it could name a discipline the account doesn't have over
one it does. Both fixed - see item 4 for the full findings.

**3. Tests.** `SnapshotStoreTests` gained 2 (a real store, temp-directory
round trip of populated `CharacterDisciplines`; null `CharacterDisciplines`
round-trips as null, not an empty list). `SnapshotSerializationTests`
gained 2 (a legacy snapshot.json missing the field entirely deserializes
cleanly to null - the "no data captured yet" backward-compat case; a
populated list round-trips through `SnapshotHelpers.Serialize`/
`DeserializeSnapshot` byte-for-byte). `PlanViewModelBuilderStepSectionsTests`
gained 4 against the real `PlanViewModelBuilder`/`CraftingPlanResultBuilders.
MakeResult` production path: all matching characters meet the required
rating (ordered highest-first, no slash suffix); one character below the
required rating (slash-min convention); no character has the discipline
("Not trained on any character"); and no snapshot character data at all
(`CharacterAvailabilityText` null, not an empty string). No Blish HUD
references in any new test; no fake file I/O (`SnapshotStoreTests` uses a
real `SnapshotStore` against a real temp directory, matching the
project's existing storage-test convention).

**4. Review-fix pass (this round) - 2 Critical + 3 Must Fix findings from
adversarial review, all fixed.**

- *Critical: a per-character crafting-fetch failure produced a PARTIAL
  `CharacterDisciplines` list instead of the "no data" null state.*
  `Gw2AccountSnapshotService`'s per-character loop only ever counted the
  character-LIST fetch as a failure; any individual character's data
  fetch failing was silently skipped with no flag set, so a real,
  plausible failure mode (list succeeds, some or all per-character
  detail calls then fail/rate-limit) left `CharacterDisciplines` as a
  non-null list missing exactly the failed characters' entries -
  indistinguishable from "captured, and this account genuinely has
  nobody trained in it." `BuildCharacterAvailabilityText` treats any
  non-null list as authoritative, so this fabricated an affirmative "Not
  trained on any character" claim from missing data, violating both the
  repo's "never invent data" invariant and the W3C spec's own item 4
  ("degraded fetch -> show nothing"). Fixed: a new
  `characterDisciplineDataDegraded` flag is set on ANY per-character
  crafting-fetch exception (or an unexpected null response with no
  exception); if set after the loop, `snapshot.CharacterDisciplines` is
  reset to null wholesale, discarding even the entries successfully
  gathered from other characters - a coarse but honest "we don't have
  complete data, so make no claim" behavior, matching the null/empty
  distinction's own binary design.
- *Must Fix: the single-round-trip full-character-record fetch traded a
  tiny payload for one of the heaviest v2 endpoints and widened the
  cosmetic feature's failure blast radius onto plan-affecting inventory
  data.* `V2.Characters[name].GetAsync` pulls in the character's full
  learned-recipe id list plus up to 8 equipment/build tabs whenever the
  (typically granted) `builds` scope is present - none of it used here -
  adding latency (risking the whole-snapshot 60s budget on larger
  accounts) and a new deserialization failure surface that, on a hiccup,
  would drop that character's INVENTORY (which feeds owned-materials
  reduction) rather than just its cosmetic discipline data. Reverted to
  two small, independently-caught endpoints: the pre-W3C
  `V2.Characters[name].Inventory.GetAsync` (unchanged) plus a new
  `V2.Characters[name].Crafting.GetAsync` for the discipline signal -
  both need only the already-required `account`/`characters`(/
  `inventories`) scopes.
- *Must Fix: the "Use Own Materials" checkbox silently hid this whole
  cosmetic feature when unchecked.* `Module.cs`'s `generateAsync` lambda
  passed a fully-null `AccountSnapshot` on the `useOwn: false` branch, so
  `result.CharacterDisciplines` came back null and every discipline row
  quietly lost its character text even though the on-disk snapshot had
  full data - unrelated cosmetic account info should not be gated on the
  solver's owned-materials toggle. Fixed: the lambda is now `async` and,
  on that branch, overlays `_currentSnapshot?.CharacterDisciplines` (and
  the matching `PlanSolveContext.CharacterDisciplines`, which has a
  public setter) onto the already-generated result after the pipeline
  call returns - `snapshot: null` still correctly disables owned-materials
  reduction/the force-buy pre-pass/owned-currency annotation, all
  independently gated on `snapshot != null` inside the pipeline.
- *Must Fix: the multi-discipline greedy-cover tiebreak could name a
  discipline the account doesn't have over one it does.*
  `PlanResultBuilder`'s pre-existing Pass 2 set-cover loop (unrelated to
  W3C's own passthrough code, but directly feeding
  `BuildCharacterAvailabilityText`'s claim) broke coverage-count ties by
  "prefer already-selected, then alphabetical" - for a recipe craftable
  by, say, Armorsmith/Leatherworker/Tailor with no other craft step to
  seed a Pass 1 preference, it always picked "Armorsmith" (alpha-first)
  regardless of the account, so a player with only Tailor read "Armorsmith
  - Not trained on any character" and could conclude they needed a second
  500 discipline they don't. `Build` gained an optional
  `characterDisciplines` parameter (defaults to null, so every
  pre-existing test/caller is unaffected) used ONLY to add a third
  tiebreak tier - "prefer a discipline the account has ANY character
  trained in" - between "prefer already-selected" and alphabetical; this
  can only relabel which equally-good discipline is reported, never
  change which recipes need a discipline, how many are required, or any
  cost/decision.
- *Must Fix: zero test coverage on the pipeline wiring that makes the
  feature appear at all.* Only the leaf builder
  (`PlanResultBuilderTests`) and the store (`SnapshotStoreTests`/
  `SnapshotSerializationTests`) had coverage; the three
  `result.CharacterDisciplines = ...`/`context.CharacterDisciplines`
  assignments inside `CraftingPlanPipeline` (single-item generate,
  multi-item generate, `ResolveWithOverrides` carry-forward) were
  unverified - deleting any one of them still left the full suite green.
  Five new `CraftingPlanPipelineTests` now cover: single- and multi-item
  `GenerateStructuredAsync` carrying a populated `CharacterDisciplines`
  into both `result` and `result.SolveContext`; a null-snapshot
  generation keeping it null in both places; and `ResolveWithOverrides`
  carrying it forward across a local re-solve for both the
  populated and the null case.

New tests: `PlanResultBuilderTests` gained 2 (the account-preference
tiebreak itself; a companion regression guard proving the pre-W3C
alphabetical fallback is unchanged when `characterDisciplines` is null/
omitted). `CraftingPlanPipelineTests` gained 5, listed above. No test
exercises `Gw2AccountSnapshotService` directly (it references
`Blish_HUD`/`Gw2Sharp`, out of scope for the Blish-free-tests invariant,
matching the file's existing zero direct-test-coverage pattern) - the
per-character degradation fix there is covered by build + code review
only, same as every other branch in that file.

Validation: `dotnet build -p:Platform=x64` clean (0 errors); module test
suite green - 1206 passed (was 1199 before this review-fix pass; +7 new
tests, all listed in item 4 above). No new Blish HUD references in
tests; every new test exercises real production code
(`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`,
`PlanResultBuilder.Build`) with no contract-mirror/fake-logic tests.
Item/currency/vendor IDs remain internal-only - only character names and
discipline names (both already user-facing concepts) appear in the
`CharacterAvailabilityText` display strings.

**5. Review-fix pass round 2 (2026-08-08) - 2 further Must Fix findings
from a follow-up adversarial review, both fixed.** Both findings were
newly introduced BY item 4's own fixes interacting with each other -
neither existed before that round.

- *Must Fix: the item-4 "Use Own Materials" overlay fix bypassed the
  account-preference tiebreak on the very branch it was meant to fix, and
  then silently changed the reported discipline on the first local
  override re-solve.* Item 4's fix backfilled `result.CharacterDisciplines`
  onto the ALREADY-BUILT result after `Module.cs`'s useOwn:false pipeline
  call returned - but `PlanResultBuilder.Build()` had already run, inside
  that call, with `characterDisciplines` null (since `snapshot: null` was
  passed to disable reduction, and the pipeline derived the tiebreak list
  solely from `snapshot?.CharacterDisciplines` at the time). So the
  account-preference tiebreak (item 4's OWN third fix) never actually ran
  on this branch: a recipe coverable by, say, Armorsmith/Leatherworker/
  Tailor with an account that only has Tailor still reported "Armorsmith -
  Not trained on any character" with "Use Own Materials" off - the exact
  misleading claim the tiebreak fix was supposed to close. Worse,
  `PlanSolveContext` is mutable and the overlay also patched
  `result.SolveContext.CharacterDisciplines`, so the FIRST local override
  re-solve after that (`ResolveWithOverrides` -> `Build()` again, now with
  a non-null `context.CharacterDisciplines`) silently re-picked "Tailor" -
  the Required Disciplines section rewrote itself with no discipline-
  related user action. Fixed by threading the cosmetic list into the
  pipeline as its own argument instead of patching the result after the
  fact: `GenerateStructuredAsync` (both the single-item and
  `IReadOnlyList<PlanRequestItem>` overloads) and the private
  `GenerateStructuredMultiAsync` all gained an optional
  `characterDisciplines` parameter, used via
  `characterDisciplines ?? snapshot?.CharacterDisciplines` wherever the
  old `snapshot?.CharacterDisciplines`-only computation fed `Build()` and
  `result.CharacterDisciplines` - so it is available to the tiebreak on
  the VERY FIRST `Build()` call, on both branches, regardless of whether
  `snapshot` itself is null. `Module.cs`'s lambda now passes
  `characterDisciplines: _currentSnapshot?.CharacterDisciplines`
  explicitly on BOTH the useOwn:true and useOwn:false branches (alongside
  `snapshot: null` on the latter, still correctly disabling reduction/the
  force-buy pre-pass/owned-currency annotation) and the post-hoc overlay
  is gone entirely - the lambda is a plain (non-`async`) expression again,
  matching its pre-W3C shape. `PlanSolveContext.CharacterDisciplines` is
  populated from this same value at generation time, so a local
  `ResolveWithOverrides` re-solve now carries forward an already-correct
  list instead of "discovering" it partway through a session.
- *Must Fix: doubling this feature's per-character API round trips (1 ->
  2, sequential) doubled its exposure to the exact class of transient
  failure that item 4's own all-or-nothing rule turns into a silent,
  whole-account, every-refresh feature loss.* A 30-character account went
  from 30 to 60 sequential per-character round trips inside the hard 60s
  `SnapshotFetchTimeout` (`Module.cs`'s `CancelAfter`) whose expiry
  discards the whole snapshot; independently, a single transient 429/500
  on just ONE character's `/crafting` call - not implausible for GW2's
  API - permanently wiped `CharacterDisciplines` for every character, on
  every refresh, with only a `Warn` log line and no in-UI signal (item 4's
  all-or-nothing rule is otherwise correct: see item 4's own rationale for
  why a partial list is unacceptable). Fixed with two cheap mitigations
  that do not reopen the reverted full-record endpoint: (1) each
  character's inventory and crafting-discipline fetches now run
  CONCURRENTLY via `Task.WhenAll` (two new private helpers,
  `FetchCharacterInventoryItemsAsync`/`FetchCharacterCraftingAsync`, each
  catching its own failures internally so neither one's failure faults
  the other's `Task`), restoring the wall-clock cost to roughly one round
  trip per character instead of two; and (2)
  `FetchCharacterCraftingAsync` gained one bounded retry (2 attempts, no
  artificial delay - mirroring `ItemMetadataService.GetMetadataAsync`'s
  own first-wave + retry-wave pattern) before a character's crafting data
  counts as failed, so a single transient hit self-heals instead of
  wiping the whole account's discipline data. Concurrency is capped at 2
  in-flight requests at a time (one character's own pair; the `foreach`
  loop still awaits each character before moving to the next), so this
  does not turn into an unbounded request burst against the GW2 API.

New tests: `CraftingPlanPipelineTests` gained 2, proving the item-5 fix
end to end through the exact call shape `Module.cs`'s useOwn:false branch
uses (`snapshot: null`, `characterDisciplines` supplied explicitly) - one
through the list overload's single-item short-circuit, one through the
genuine multi-item path - both asserting the account-owned discipline
(not the alphabetically-first one) is reported, and that a subsequent
`ResolveWithOverrides` no-op re-solve reports the identical discipline
rather than changing it. No test exercises the
`Gw2AccountSnapshotService` concurrency/retry change directly, for the
same reason item 4's per-character degradation fix has none (the file
references `Blish_HUD`/`Gw2Sharp`, out of scope for the Blish-free-tests
invariant) - covered by build + code review only, consistent with the
file's existing zero-direct-test-coverage pattern.

Validation (round 2): `dotnet build -p:Platform=x64` clean (0 errors);
module test suite green - 1208 passed (was 1206 after round 1; +2 new
tests, listed above). No new Blish HUD references in tests; both new
tests exercise real production code
(`CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`)
with no contract-mirror/fake-logic tests. Item/currency/vendor IDs remain
internal-only.

Live desktop gate: PASS (2026-08-08, orchestrator session). Sandbox
Blish (isolated preflight settings, dummy-window mode) with the
synthetic snapshot seeded with 4 `CharacterDisciplines` entries across
2 characters. Verified live across two generated plans:

- "Zojja's Claymore" (Weaponsmith 500): the Required Disciplines row
  rendered "Weaponsmith | Maximus Test (500), Alt Number Two (400/500)
  | Level 500" - sufficient character plain, below-threshold character
  in the slash form, sorted highest rating first; the "Characters"
  column header present and aligned over the text.
- "Zojja's Breastplate" (Armorsmith 500, deliberately absent from the
  seed): the row rendered "Not trained on any character".

No exceptions in the Blish log across the session. Alongside the W3C
checks, the seeded snapshot rendered correctly on the Snapshot tab, the
Required Recipes "(showing N missing of M)" header and Hide Unlocked
default were intact, and craft steps showed "Mystic Forge" as a plain
location tag with no fake level.
