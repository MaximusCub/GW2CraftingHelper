> **Frozen record - 2026-07-21.** M37 R5 read-only concurrency and degradation audit, closed and kept as evidence.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

# M37 R5 - Concurrency and Degradation Audits (KNOWN-ISSUES item 31)

Read-only audit phase for KNOWN-ISSUES item 31 (three never-formally-swept
reviews recorded there as verification debt). Three independent audit
passes each swept one area of the codebase for classified findings; every
blocking (Critical/MustFix) finding was then independently re-checked by a
separate adversarial refutation pass before being handed to the fix pass
recorded in KNOWN-ISSUES.md item 31's "FIXED in M37" note. No files were
modified during this phase - inventory only. All three summaries below are
reproduced verbatim from each audit's own report.

Totals across all three audits: **9 confirmed, 0 refuted, 2 nice-to-have.**
Every confirmed finding was adversarially re-walked against the real code
by a second pass and held up (zero findings were refuted on review).

---

## Audit: await-marshal (KNOWN-ISSUES 31a)

Cross-thread await sweep of Views/, Module.cs, and pipeline/service
callbacks that mutate Blish controls or render-read view state.

**Findings: 2 confirmed (both MustFix), 0 refuted, 1 nice-to-have.**

> SCOPE: KNOWN-ISSUES 31a cross-thread await sweep of Views/, Module.cs, and
> pipeline/service callbacks that mutate Blish controls or render-read view
> state. m37-homestead worktree is OUT of scope and was not examined.
> Adjacent audits 31b (offline/API-down) and 31c (price/recipe-cache
> thread-safety) NOT covered here. INVENTORY (await/continuation -> resume
> thread -> touches -> verdict): [Module.Initialize Task.Run@191 ->
> ThreadPool -> recipeOverlay.InvalidateIfStale/recipeSeed.SetCurrentBuildId
> (cache, not controls) -> OK for 31a; note concurrent read by an early
> Generate is a 31c cache-race, out of scope]. [Module.LoadAsync await
> RefreshSnapshotInBackground@392 -> ThreadPool -> snapshot fields via
> Update() dirty-flag -> OK]. [Module.FetchAndSaveSnapshotAsync await@457 ->
> ThreadPool continuation writes _currentSnapshot/_pendingSnapshot/
> _snapshotDirty + _snapshotStore.Save with NO epoch guard -> FINDING F1].
> [Module RefreshSnapshotInBackground/UserRefresh finally
> _refreshInProgress=false off-thread@498/517 -> ThreadPool, read
> main-thread w/o barrier -> FINDING F3 (NiceToHave)]. [Module.Update
> dirty-flag drain + SaveStatusThreadSafe -> main thread -> OK].
> [Module.FetchGw2BuildIdAsync awaits -> ThreadPool, no controls -> OK].
> [CraftingPlanView Generate Click async@1393 -> main entry -> OK].
> [TriggerGenerate await _generateAsync@1944 -> ThreadPool; vm build
> off-thread is pure over fetched data; all state+control writes inside
> MainThreadMarshal.Run w/ myGen guard -> marshaling OK, BUT
> _generateSequence bumped before empty-request early-return -> FINDING
> F2]. [statusProgress Progress<T>@1924 -> ThreadPool -> marshaled +
> StatusUpdateGuard(myGen,_statusClosedForCurrentGeneration) -> OK].
> [OnContentWheelObserved/ApplyWheelWrapCorrection/StartWheelWrapVerify@803/
> 910/1001 -> main thread (MouseWheelScrolled event + FrameTicker.DoUpdate)
> -> OK]. [OnPanelResized/PreserveScrollAcrossResize/ResizeSettleStep@1445/
> 1583/1774 -> main thread (Resized + FrameTicker) -> OK].
> [ApplyOverridesAndResolve@3675 + pipeline.ResolveWithOverrides (returns
> CraftingPlanResult, fully synchronous incl. M37 batch economics
> recompute) -> main-thread click, no continuation -> OK].
> [Achievement-dedup wiring: AchievementBitDedupPrePass.Apply is
> pure/static, called synchronously inside pipeline generate/resolve, no
> continuation, touches no controls -> OK]. [pill/preset/craft-all/buy-all/
> best-path/toggle Click + row toggle handlers -> main thread -> OK].
> [CreateItemIcon GetRenderServiceTexture@4797: currency/item icon arrival
> is Blish-internal AsyncTexture2D swap, no module-side continuation -> OK].
> [MainView Refresh Now Click async@138 -> ThreadPool continuation ->
> SaveStatusThreadSafe (file I/O safe) + MainThreadMarshal.Run for control
> mutations w/ disposed-parent guard -> OK; but its sibling Clear Cache
> button feeds Module.ClearCache -> F1]. [MainView Clear Cache Click@129 ->
> main thread sync -> OK locally]. [SuggestionPanel.OnTextChanged async
> void@57 -> providers complete synchronously today; marshaled w/
> stale-query+focus+disposed guards -> OK]. [SuggestionPanel global mouse
> hook OnGlobalMouseClick@326, AutocompleteTextBox keyboard,
> ModalDialog/SettingsTabContent/LogTabContent/ViewAdapter handlers -> all
> main-thread input/layout events, no awaits -> OK]. PlanViewModelBuilder
> confirmed stateless (no instance fields) so concurrent Build() from a
> generate continuation and a main-thread ApplyOverridesAndResolve is safe.
> VERDICT: control/view-state marshaling is broadly correct and the
> M31/M34 primitives are applied consistently; two MustFix defects are
> guard-shaped (a missing epoch guard on the snapshot-refresh completion,
> and a generation-counter bump that leaks past a validation early-return),
> plus one NiceToHave memory-visibility note.

Confirmed findings: **31a-F1** (Module.cs:459, snapshot refresh vs Clear
Cache race, MustFix), **31a-F2** (Views/CraftingPlanView.cs:1881,
generation-sequence bump before early-return, MustFix). Nice-to-have:
**31a-F3** (Module.cs:498, `_refreshInProgress` memory-visibility).

---

## Audit: api-degradation (KNOWN-ISSUES 31b)

Offline/API-down degradation: behavior when each `/v2` endpoint (prices,
items, currencies, recipes, account) fails or times out.

**Findings: 6 confirmed (1 Critical, 5 MustFix), 0 refuted, 0 nice-to-have.**

> Scope: master branch only (m37-homestead worktree and the stray
> scratch checkouts of this same repo were excluded). Swept
> every consumed /v2 endpoint family end-to-end for offline/API-down
> degradation: /v2/commerce/prices (Gw2PriceApiClient -> TradingPostService,
> 4 call sites in CraftingPlanPipeline), /v2/items (Gw2ItemApiClient ->
> ItemMetadataService), /v2/currencies (CurrencyMetadataService - confirmed
> solid: internal 5s timeout, correctly distinguishes caller-cancellation
> vs internal timeout, never permanently negative-caches a transient
> failure), /v2/recipes (Gw2RecipeApiClient -> CompositeRecipeApiClient ->
> RecipeService, confirmed genuinely used at runtime, not just
> research/seed), and /v2/account* (Gw2AccountSnapshotService for the
> ownership snapshot, Gw2AccountRecipeClient for learned recipes). Traced
> status surfacing end-to-end via CraftingPlanView.TriggerGenerate
> (M31/M34-B1#4 primitives - StatusUpdateGuard, MainThreadMarshal,
> generation-sequence guard - all functioning correctly; every Generate
> path DOES reach a terminal status, just sometimes only after a very long
> uncommunicative wait) and MainView's Refresh Now handler (also solid:
> try/catch/finally, disposed-control checks, button re-enable on every
> path). VendorOfferResolver's wiki-fetch retry/backoff logic is well-built
> but confirmed DEAD CODE at runtime (Module.cs passes resolver:null) so it
> introduces no live-degradation risk. Verified via decompiling
> Gw2Sharp.dll's XML docs that both the module's own HttpClient and
> Gw2Sharp's default HttpClient share the same ~100s default timeout,
> ruling out a literal infinite hang from that specific angle, but not the
> several-hundred-second cumulative-sequential-timeout risk documented in
> F6. Re-swept after the first pass turned up only the account-snapshot
> issue, specifically to check for siblings of the 'unguarded await aborts
> everything' pattern per the fix-the-class-not-the-instance rule - found
> it repeated in TradingPostService, ItemMetadataService's first wave, and
> Gw2AccountRecipeClient (F2-F4), plus a distinct ct-plumbing bug in
> Gw2RecipeApiClient (F5). Verdict: one Critical (silent account-snapshot
> cache corruption on refresh failure, F1) plus five MustFix findings, all
> with concrete evidence in the current source; no Critical/MustFix
> findings for the currency-metadata path, the wiki/vendor-offer path
> (unused), or the UI-thread status-surfacing primitives, which are all in
> good shape as-is.

Confirmed findings: **F1** (Services/Gw2AccountSnapshotService.cs:42,
silent empty-snapshot on total failure, Critical), **F2**
(Services/TradingPostService.cs:54, price batch abort, MustFix), **F3**
(Services/ItemMetadataService.cs:57, metadata first-wave abort, MustFix),
**F4** (Services/CraftingPlanPipeline.cs:451, unguarded learned-recipes
fetch, MustFix), **F5** (Services/Gw2RecipeApiClient.cs:25, `ct` silently
a no-op + missing 404 handling, MustFix), **F6** (Module.cs:471, unbounded
sequential snapshot fetch with no overall timeout, MustFix). No
nice-to-have findings from this audit.

---

## Audit: price-cache (KNOWN-ISSUES 31c)

Price-cache thread-safety: the M26 TTL cache + locks under concurrent
generate/re-solve/refresh (`Services/TradingPostService.cs`).

**Findings: 1 confirmed (MustFix), 0 refuted, 1 nice-to-have.**

> SCOPE: audited only the main tree at the repo root
> (master, PRs #53-#56 merged). A homestead worktree was not read or
> touched - explicitly out of scope per instructions. No files were modified; this is a
> read-only sweep. Re-swept once after the first pass (initial pass found
> the cache internals individually lock-safe; re-sweep against the actual
> concurrent CALLERS in Module.cs/CraftingPlanView.cs/CraftingPlanPipeline.cs
> is what surfaced the real, reachable race below - so the "completely
> clean" first impression did not survive the re-sweep).
>
> TARGET PRIMITIVE: Services/TradingPostService.cs - the M26 TTL price
> cache. Single instance for the module's process lifetime (Module.cs:209,
> held by the one long-lived CraftingPlanPipeline created in Module init).
>
> LOCK/FIELD ACCESS MATRIX:
> - `_cache` (Dictionary<int,(ItemPrice,DateTime)>): every read
>   (TryGetValue, twice) and every write (indexer assignment) happens
>   inside `lock (_cacheLock)`; no unguarded access found anywhere in the
>   repo (grep-confirmed _cache is private and touched only inside
>   TradingPostService.cs). The single `await _api.GetPricesAsync(batch,
>   ct)` per batch is the ONLY await in the method and sits strictly
>   OUTSIDE the lock - correct for not blocking the UI/other threads during
>   I/O, but this is also exactly the gap that produces finding 31c-1 (no
>   in-flight de-dup covering that window).
> - `_cacheLock`: plain `object`, held only across pure synchronous
>   dictionary ops (never across an await) - confirmed no re-entrancy-
>   while-holding-lock hazard from ThreadPool continuations.
> - `ItemPrice` instances: mutable properties, but repo-wide grep for
>   `.BuyInstant =` / `.SellInstant =` shows they are set ONLY at
>   construction inside TradingPostService.GetPricesAsync's batch-insert
>   loop, never mutated afterward anywhere else (PlanSolver,
>   CraftingPlanPipeline, ResolveWithOverrides, PlanViewModelBuilder, etc.
>   all treat ItemPrice as read-only). A fully-constructed instance is what
>   gets published into `_cache`/the returned dictionary - no partially-
>   built instance is ever visible cross-thread, so no field-tearing is
>   possible despite the class itself lacking `readonly`/immutability
>   enforcement.
> - `PlanSolveContext.Prices`: a fresh, independent `Dictionary<int,
>   ItemPrice>` built once per generation by CraftingPlanPipeline (never a
>   live view over `_cache`); ResolveWithOverrides (line 845) and its M37
>   batch-economics companion (ApplyBatchSellSideEconomics) read ONLY this
>   snapshot and never call back into TradingPostService/_cache at all -
>   confirmed by reading both methods end to end. This means the local
>   re-solve / batch-economics-recompute path under audit here is,
>   by construction, decoupled from the cache and cannot race it; the real
>   overlap risk is strictly between concurrent Generate-family calls
>   (GenerateAsync L102, GenerateStructuredAsync L270,
>   GenerateStructuredMultiAsync L644, and the nested
>   AugmentWithVendorCostPricesAsync L959 which is sequential-within-one-
>   generation, not concurrent with itself).
>
> HUNT-LIST RESULTS (KNOWN-ISSUES 31c items, each explicitly checked):
> - Check-then-act race (TTL check -> non-atomic fetch+insert): CONFIRMED,
>   real, reachable interleaving via the Generate button + "Use Own
>   Materials" toggle-modal (neither TriggerGenerate nor
>   OnOwnMaterialsToggled mutually excludes the other, and _generateAsync
>   always runs with CancellationToken.None so a superseded generation's
>   fetch is never cancelled) -> filed as 31c-1 (MustFix).
> - Lock granularity (fetch under lock vs. outside): fetch is correctly
>   outside the lock (no UI-blocking stampede-via-lock-holding), but that
>   same choice is what enables 31c-1's duplicate-fetch stampede - no
>   request coalescing exists to close the window.
> - Dictionary mutation during enumeration: checked, NOT PRESENT - no
>   caller ever enumerates `_cache` directly; every access is TryGetValue
>   under lock, and every returned dictionary is an independent copy, not
>   a live view.
> - Torn/tearing reads of multi-field ItemPrice entries: checked, NOT
>   PRESENT - see ItemPrice access-matrix note above.
> - TTL expiry mid-solve (mixed old/new prices within one solve): checked,
>   ACCEPTABLE / not a violation - each generation captures its own Prices
>   dictionary once (Step 3) before solving and never re-reads the cache;
>   concurrent cache churn elsewhere can only rebind a dictionary slot to a
>   NEW ItemPrice instance, never mutate an instance a prior generation
>   already holds, so a given solve's snapshot stays internally
>   self-consistent for the merged-ceil/economics invariants (M34
>   FinalizeVendorBatches, M37 batch economics) regardless of what other
>   threads do to `_cache` meanwhile.
> - Re-entrancy from ThreadPool continuations while a lock is held:
>   checked, NOT PRESENT - no await occurs inside a lock block.
> - Disposal/shutdown races (module unload mid-fetch): checked -
>   Module.Unload (Module.cs:432) disposes `_httpClient` with no
>   cancellation of in-flight fetches; a resulting
>   ObjectDisposedException/HttpRequestException from Gw2PriceApiClient
>   propagates uncaught through TradingPostService.GetPricesAsync into
>   CraftingPlanPipeline's Generate* methods into TriggerGenerate's own
>   catch-all (Views/CraftingPlanView.cs:1995), which is defended by
>   MainThreadMarshal.Run's null-Overlay guard (drops and logs rather than
>   throwing) and the view's own `_contentPanel == null` disposal check -
>   verified this does not crash and cannot leave a torn cache write (the
>   exception fires before that batch's insert-under-lock statement runs).
>   No finding filed for this angle.
> - Background/TTL refresh + prefetch: checked, DOES NOT EXIST as a code
>   path - grepped the whole repo for Timer/Prefetch/Preload/RefreshPrices/
>   WarmCache; the only "PreWarmCache" hit is RecipeService's unrelated
>   M10-era recipe cache (with its own dedicated
>   RecipeServiceConcurrencyTests.cs), not prices. TTL expiry for prices is
>   enforced purely lazily, inside GetPricesAsync itself, on whatever call
>   happens to land after expiry - there is no separate background
>   refresher or prefetcher to audit for this primitive; noted as N/A
>   rather than assumed-and-skipped.
>
> VERDICT: 1 MustFix (concurrent-generation cache stampede / no in-flight
> de-dup, reachable via a concrete two-widget UI interaction, no data
> corruption but real wasted API load and an unguarded test gap) and 1
> NiceToHave (unbounded `_cache` growth, no eviction, benign at current
> scale). No Critical findings - the lock discipline around the dictionary
> itself is sound and every other specifically-requested hazard (tearing,
> enumeration-during-mutation, re-entrancy-while-locked, mid-solve
> inconsistency, disposal crash) was checked and found not present.

Confirmed finding: **31c-1** (Services/TradingPostService.cs:39, price
fetch stampede / no in-flight de-dup, MustFix). Nice-to-have: **31c-2**
(Services/TradingPostService.cs:23, unbounded `_cache` growth, no
eviction policy).

---

## Disposition

All 9 confirmed findings and nice-to-have 31a-F3 were fixed in the
same M37 work that produced this report - see KNOWN-ISSUES.md item 31's
"FIXED in M37" record for the per-finding fix summary, tests added, and
the accepted M38 candidate (31c-2, deferred, not implemented).
