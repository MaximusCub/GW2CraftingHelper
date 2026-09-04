# D5 - Feasibility Study: "Tell me what to do next"

**Status:** Design proposal / feasibility study only. No code changes. Backlog candidate (same tier as localization: interesting, deferrable, greenlight-gated).
**Scope target tab:** Crafting Ranker (tab #5, currently a `Module.BuildPlaceholder` "Coming Soon" stub). This study treats "what to do next" as the Ranker tab's core charter, NOT a change to the Crafting Plan tab.
**Author's stance up front:** Tier 1 is real, cheap, and worth building. Tier 2 is real but data-quality-bounded. Tier 3 (farming/earning guidance) is a research project that even gw2efficiency has not attempted; it should stay out of scope, possibly forever, unless a specific narrow slice is greenlit.

Evidence tags used throughout: **MEASURED** = read from this repo's code/data; **INFERRED** = reasoned from measured facts, not directly proven; **GUESS** = judgement call, flagged as such.

---

## 0. The question this study answers

Once the module knows what a player wants to craft - possibly more than one item, in a priority order -
can it tell them what to do next? The module can generate plans, it knows the account's currencies and
inventory, and it holds vendor daily purchase limits and other in-game time-gates. The wanted output is
a short, concrete "go do this next": a purchase, an item to farm, or a currency to earn. Legendary
crafting and the game systems behind it are deep enough that this may not be tractable at all, so the
study answers honestly rather than optimistically.

---

## 1. DATA WE HAVE vs DATA WE LACK

### 1.1 What the solver already emits (MEASURED)

Every generated plan produces a `CraftingPlanResult` (`Models/CraftingPlanResult.cs`) whose `Plan` (`Models/CraftingPlan.cs`) already carries, per step, almost everything a "buy this / craft this now" recommender needs:

| Data | Source (MEASURED) | Directly usable for "next step"? |
|---|---|---|
| Per-step acquisition source | `PlanStep.Source` = `BuyFromTp` / `Craft` / `Currency` / `BuyFromVendor` / `UnknownSource` (`Models/AcquisitionSource.cs`) | Yes - this IS the "what kind of action" bucket |
| Coin cost of each step | `PlanStep.UnitCost` / `TotalCost` (long, copper) | Yes |
| Non-coin currency cost of a vendor step | `PlanStep.VendorCurrencyCosts` (`List<CostLine>{Type,Id,Count}`), already scaled to the step quantity | Yes |
| Per-purchase vendor batch shape | `PlanStep.VendorOfferOutputCount` + `VendorOfferCurrencyCostLinesPerBatch` (unscaled, one purchase) | Yes - lets us express "each click gives N, costs C" |
| Total plan coin cost | `CraftingPlan.TotalCoinCost` | Yes |
| Total plan currency cost | `CraftingPlan.CurrencyCosts` (`List<CurrencyCost>{CurrencyId,Amount}`) | Yes |
| Owned wallet balances (per currency) | `CraftingPlanResult.OwnedCurrencyAmounts` + `Services/AccountCurrencyIndex.cs` over `AccountSnapshot.Wallet` | Yes - enables "you can afford this currency cost now" |
| Owned material used per node | `ReducedTreeResult.OwnedQuantityUsedByNode` -> snapshotted as `PlanSolveContext.OwnedQuantityUsedByNodeId` | Yes - enables "you already own the inputs for this craft" |
| Vendor purchase caps | `VendorOffer.DailyCap` / `WeeklyCap` (`Models/VendorOffer.cs`), surfaced post-solve as `CraftingPlan.TimegatedItems` (`Models/TimegatedItem.cs`: `ItemId`, `CapType` Daily/Weekly, `CapValue`, `NeededCount`) | Partially - see the cap-consumption gap in 1.3 |
| Account inventory index (item -> source -> count) | `Services/AccountItemIndex.cs`, `GetPrioritizedSources(itemId, activeCharacter)` ranks MaterialStorage > active char > SharedInventory > Bank > others | Yes - "you have 40/60, the other 20 are on <char>" |
| Acquisition hints for unpriceable items | `CraftingPlanResult.AcquisitionHints` (`Models/AcquisitionHint.cs`: `Hint` text + `Badge` e.g. "SALVAGE"/"EXPLORE", wiki-seeded, `ref/acquisition_hints_seed.json`) | Yes as *text*, no as *actionable data* |

**Critical correction to a stale code comment.** The doc comment in `Models/CraftingPlan.cs` says `TimegatedItems` is *"currently always empty, since no seeded vendor offer carries a cap yet (M34 R3)."* **That is now false.** MEASURED from `ref/vendor_offers.json`:

- 53,529 total vendor offers.
- **373 offers carry a non-null `DailyCap`**; daily-cap value distribution: `1`x174, `5`x85, `10`x46, `20`x22, `6`x13, `4`x13, `3`x7, `25`x4, `30`x3, `21`x3, `8`x2, `2`x1.
- **319 offers carry a non-null `WeeklyCap`**; weekly-cap value distribution: `100`x177, `250`x39, `1`x39, `3`x12, `10`x12, `5`x19, `7`x10, `2`x3, `20`x3, `4`x2, `50`x2, `8`x1.
- Total capped offers: **692**. That is ~1.3% of the dataset.

And the solver DOES consume them now: `Services/PlanSolver.cs` carries `DailyCap`/`WeeklyCap` through `VendorOfferBatch` (lines ~828-859) and emits `TimegatedItems` in `FinalizeVendorBatches` (lines ~1321-1329). So the M34 machinery is **live**, not dormant. The stale comment in `CraftingPlan.cs`/`TimegatedItem.cs` should be corrected (flag for a docs-consistency sweep; it is NOT in any current M38 WP - INFERRED from reading the WP list).

**Takeaway for Tier 1:** the plan object is roughly 90% of the way to an actionable-now feed. What is missing is not solver data - it is a small, pure *classifier* that joins plan steps to the wallet/coin/inventory snapshot and buckets them.

### 1.2 The account snapshot we already fetch (MEASURED)

`Services/Gw2AccountSnapshotService.cs` requests token scopes `Account`, `Characters`, `Inventories`, `Wallet` and produces `AccountSnapshot { CapturedAt, CoinCopper, List<SnapshotItemEntry> Items, List<SnapshotWalletEntry> Wallet }`. Items merge Bank + SharedInventory + MaterialStorage + `Character:<name>` bags (equipped gear NOT captured - MEASURED). Refresh is all-or-nothing per source (`SnapshotFetchFailedException`, KNOWN-ISSUES 31). Persisted as `data/snapshot.json` (`Services/SnapshotStore.cs`). This gives us, for free at "next step" time: current coin, current wallet, current item inventory with per-source location.

### 1.3 What we genuinely LACK, and which API could close each gap

| Gap | Why it matters for "do this next" | Closable? Endpoint + scope | Verdict |
|---|---|---|---|
| **Today's/this-week's *consumption* of a vendor purchase cap** | To say "you can still buy 3 of your 5 daily", we must know how many you already bought today. The plan knows the cap (5) and the demand (`NeededCount`), NOT how many you have spent. | **No general endpoint.** GW2 exposes no per-vendor purchase counter. Only *specific* tracked lists exist (below). | **Not closable** for arbitrary vendor caps. Honest UI can show cap + demand, not "remaining today". |
| **Time-gated daily *crafting* already done today** | Charged Quartz, Lump of Mithrillium, Glob of Elder Spirit Residue, Spool of Silk Weaving Thread, Spool of Thick Elonian Cord etc. are daily craft-once items. If the user already made today's, "craft this next" is wrong. | **`/v2/account/dailycrafting`** returns an array of time-gated recipe *names* already crafted since reset. Master list at `/v2/dailycrafting`. **Scope: `account` + `progression`** (MEASURED via GW2 wiki). | **Closable**, but needs the **new `progression` scope** (see 1.4). This is the single highest-value gap-closer for Tier 2. |
| **Daily/weekly PvE completion state** (world bosses, map chests, raids) | Some crafting inputs come as rewards from once-per-day/week content (e.g. world-boss chests dropping pre-legendary mats). | `/v2/account/worldbosses`, `/mapchests`, `/raids` - arrays of completed ids since reset. **Scope: `account` + `progression`** (MEASURED). | **Closable** but low ROI: mapping "world boss X drops material Y I need" is a content-to-drop-table problem we do NOT have data for (see next row). |
| **Wizard's Vault (modern dailies) progress + Astral Acclaim** | The current daily/weekly reward system; Astral Acclaim buys some crafting-relevant items. | `/v2/account/wizardsvault/daily` + `/weekly` (objectives + progress). **Scope: `account` + `progression`** (INFERRED same family; wiki lists them under account). | Closable, niche. Only relevant if we model Astral-Acclaim vendor offers, which the dataset may or may not include (not verified). |
| **Achievement / collection progress** (legendary collections are achievements) | Legendary crafting is the motivating case. "Legendary X is 12/15 collection items" lives in the achievements API. | **`/v2/account/achievements`** (per-achievement current/max/done/repeated). **Scope: `progression`** (MEASURED). Resolve names via `/v2/achievements`. | **Closable** but **heavy**: legendary collections are dozens of interlocking achievements; mapping "which achievement objective corresponds to which craftable/acquirable item" is a large curation problem with no existing repo scaffolding. |
| **Currency *earn rates*** ("do meta X to get 50 currency Y / hour") | The "earning a particular currency" requirement. | **No API exists.** There is no endpoint that says "activity A yields currency B at rate R." | **Not closable via API.** Only via hand-curated wiki data + assumptions = invented-data territory. |
| **Item drop / farm sources** ("farm map Z for material M") | The "farming items" requirement. | **No structured API.** Drop tables are not in the official API. Wiki has semantic (SMW) "dropped by" data of variable quality. | **Not closable honestly at scale.** Same class of problem the vendor-offer wiki-seeder solved for *one* narrow domain, at real curation cost. |

### 1.4 The permission cost (MEASURED + flag)

Everything in the "daily/weekly progress" and "achievements" rows requires the **`progression`** token scope, which the module does **not** currently request (`RequiredPermissions` = Account/Characters/Inventories/Wallet only, MEASURED). Adding `progression` is a user-visible permission escalation: the user must regenerate/re-grant their API key, and `HasRequiredPermissions()` gating must degrade gracefully (Tier 1 must keep working with the *current* four scopes; Tier 2 daily-crafting awareness must be an *optional enhancement* gated on the extra scope, never a hard requirement). **Design rule: never make "what to do next" hard-depend on a scope the user hasn't granted.**

### 1.5 The wiki-seeder precedent, honestly scoped (MEASURED + INFERRED)

The vendor-offer pipeline proves the repo *can* bake curated wiki data into a shipped JSON seed (`ref/vendor_offers.json`, `ref/wiki_vendor_cache.json`, `ref/acquisition_hints_seed.json`) that the runtime reads locally and **never** calls the wiki live (invariant: no runtime wiki/gw2efficiency calls - MEASURED via repo rules). That precedent maps cleanly onto: (a) the daily-crafting master list (tiny, stable, `/v2/dailycrafting` is even a live official endpoint we could seed once); (b) a curated "currency -> primary sources" hint table (small, high-churn-risk). It maps **poorly** onto full drop tables and earn-rate models: those are large, volatile, and the moment we ship a number like "~40 Blue Ice/hour in Bjora" we own a maintenance liability and a trust liability (see Risks). The precedent enables Tier 2 hints; it does not rescue Tier 3.

### 1.6 REVISED - The binding-gate principle (2026-07-22)

**The observation.** Ectos can be salvaged, bought on the TP, or bought from vendors - but vendors often charge unreasonable prices in a currency of no current use to the focused goal, prices only a desperate player would pay. A cap can LOOK like a use-or-lose pathway when it is nothing of the sort. ALL acquisition routes must be understood before a time-gated vendor entry is treated as a real gate; the mere coexistence of "I need ectos" and "a time-gated ecto vendor exists" does not make the gate binding.

**The principle this forces (a hard design requirement, not a nicety).** A timegate is advice-worthy only when it is BINDING on the chosen path. Cap-data coverage (Section 1.1) is necessary but nowhere near sufficient: a cap the plan is not actually forced through is noise, and surfacing it is *worse* than saying nothing, because it teaches the user the tool cannot tell a real gate from an irrelevant one (Risk 1, the dominant risk). Three tests decide bindingness; ALL three must hold:

- **(a) Route dominance.** The capped route is genuinely the cheapest way to obtain at least the capped units. If a cheaper (or equal-and-un-gated) route exists for those units, the cap gates nothing.
- **(b) Currency opportunity cost.** The currency the capped route burns has low opportunity cost measured against the watched-set's AGGREGATE currency demand, valued through the existing per-currency valuation seam (the M25 machinery is the cited precedent; whichever seam ships, it must yield a genuine coin-equivalent or a same-currency comparison, or test (b) *itself* becomes the invalid-currency comparison the repo invariants forbid). This cuts both ways: candy corn is junk to a legendary crafter today (cheap to spend now), but if a future watched item wants it, spending it now is contested. The test is against the whole priority set, not one item in isolation.
- **(c) No un-gated escape hatch.** Un-gated routes (TP, uncapped vendors, already-owned stock) cannot cover the remaining need at acceptable cost. If they can, the cap is not on the critical path for that item.

**First-order filter (the cheap structural proxy for the three tests).** The module already knows tradability structurally: an item with a `BuyFromTp` route has a TP price; one without does not. So:

- **TP-liquid items -> caps are almost always ignorable noise.** The un-gated TP route (test c) usually dominates, and the cap gates a rounding error of the demand.
- **Account-bound / no-TP items (Mystic Clovers, homestead materials) -> caps are real gates.** There is no un-gated escape hatch by construction, so test (c) tends to hold and the cap can genuinely bind.

The word "almost" in the first bullet is load-bearing; see route splitting below and Section 5.5 for where a TP-liquid item's cap can still bind on a sub-quantity.

**Cap-aware route splitting (new solver-adjacent logic, not a lookup).** Cap semantics are warn-only (M34-B1 #3, gw2e parity): the solver does NOT re-route around a cap, so when a capped offer's per-unit price wins it can route an ENTIRE quantity through that offer regardless of the cap. Honest next-step advice therefore cannot just echo the solver's route; it must SPLIT - the capped units at the gated price, the remainder at the next-cheapest route. This is real per-item arithmetic reaching into the solver's per-offer cost model (`PlanStep.VendorOfferOutputCount` / `VendorOfferCurrencyCostLinesPerBatch` + the item's TP `ItemPrice`), not a table lookup, and it is *coupled* to test (b): you only take the capped units for currencies that pass the opportunity-cost test, otherwise the capped route is treated as unavailable and the whole quantity routes un-gated. Effort implications are folded into the tier effort classes (Section 5 and the Effort class summary).

**Salvage stays out of the model.** The TP price of a liquid commodity already embeds its salvage economics (arbitrage keeps salvage-derived supply priced into the sell/buy quotes), so modeling salvage as a separate route double-counts. The one caveat - bound items whose only un-gated alternative IS salvage - is in Section 5.5.

**The live specimen (docs/KNOWN-ISSUES.md, M37 desktop-wave observation (d), 2026-07-22).** The merged build currently renders:

> "Glob of Ectoplasm is timegated - Weekly limit: 1 (plan needs 86)"

Technically true, economically irrelevant. The weekly cap of 1 comes from the seeded "Candy Corn Vendor (Weekly)" offer (cost 1 Gibbering Skull / 1 Tyria's Best Nougat Center / 1 High-Quality Plastic Fangs, `WeeklyCap=1`, KNOWN-ISSUES item 28). Ecto is among the most TP-liquid items in the game; 85+ of the 86 come from the TP regardless. The cap fails test (a) for all but 1 unit, fails test (c) outright (the TP is a fat un-gated escape hatch), and only conditionally passes test (b). A next-step engine that turned this into "buy your weekly ecto from the Candy Corn Vendor" would be exactly the trust-destroying noise this principle guards against. Every tier below is measured against this specimen.

**Trace across tiers.** Tier 1's classifier must apply this filter before surfacing any cap as an action (2, Tier 1). Tier 2's projector must apply it *before* the ceil arithmetic, or it projects phantom poles (2, Tier 2). Tier 3 is only reinforced: the ultimate "understand all routes" failure is guessing a farm route you cannot even price, which is exactly why Tier 3 stays out.

---

## 2. DECOMPOSITION INTO CAPABILITY TIERS

### Tier 1 - "Actionable now" (bookkeeping over data the solver already emits)

**What it is:** Given one already-generated plan (or several, ranked), classify each `PlanStep` against the current snapshot into buckets the user can act on *right now*, with zero new game-knowledge:

- **Buy on TP now** - `Source == BuyFromTp` and `TotalCost <= AccountSnapshot.CoinCopper`. Show item, qty, coin cost, and a running "coin remaining after this" so the user sees how far their gold stretches.
- **Buy from vendor now** - `Source == BuyFromVendor` and every line in `VendorCurrencyCosts` is `<= AccountCurrencyIndex.GetQuantity(currencyId)` (and coin portion affordable). Show merchant name + location (`VendorOffer.MerchantName`/`Locations`) so it is literally "go here, buy this".
- **Craft now** - `Source == Craft` and the node's inputs are already owned (derivable from `OwnedQuantityUsedByNodeId` vs required). Show discipline needed (`RequiredDisciplines`).
- **Blocked (short by X)** - same step but the wallet/coin/inventory is short; show the shortfall ("need 1,240 more Karma", "need 3 more Mithril Ore").
- **Time-gated (REVISED 2026-07-22)** - an item in `Plan.TimegatedItems` is a CANDIDATE gate, never automatically an action. Before Tier 1 surfaces it as something to act on, it must pass the binding-gate tests (Section 1.6). **Conservative v1 rule:** surface a capped-vendor action ONLY for items with NO TP route - the trivially-safe subset where `Source` carries no `BuyFromTp` option (account-bound / no-TP items, where escape-hatch test (c) holds by construction). A TP-liquid item's capped-vendor offer is NEVER surfaced as a gate in v1, even when it is marginally cheaper than the TP; the full three-test binding analysis (and the route splitting it enables) is a named refinement, not v1. A suppressed gate does not vanish - the item still appears in its normal Buy-on-TP bucket at full quantity, the cap simply goes unmentioned. **The ecto specimen under this rule:** ecto has a `BuyFromTp` route, so v1 suppresses the Candy Corn weekly cap outright and produces "Buy 86 Glob of Ectoplasm on the Trading Post" (affordability permitting) with NO mention of any weekly vendor - the correct, honest output (contrast the live render in Section 1.6).

**Honest assessment of proximity (MEASURED):** the current model supports this *almost entirely as pure bookkeeping*. `PlanStep.Source` is the bucket key; `TotalCost`/`VendorCurrencyCosts` are the costs; `OwnedCurrencyAmounts` + `AccountCurrencyIndex` + `AccountItemIndex` + `CoinCopper` are the "do I have it" side; `MerchantName`/`Locations` are the "where". The ONE thing Tier 1 cannot do honestly is "you can still buy this within *today's* cap" for vendor caps, because no API exposes today's per-vendor consumption (1.3). Tier 1 must therefore phrase cap info as *"this exceeds the N/<period> cap; expect multiple <period>s"* (a property of the plan, always true) rather than *"you have K left today"* (unknowable). That is a phrasing constraint, not a blocker.

**The only genuinely new logic Tier 1 needs:** a single Blish-free classifier service (proposed `NextActionClassifier`) that takes a `CraftingPlanResult` + `AccountSnapshot` (or the already-built `AccountCurrencyIndex`/`AccountItemIndex`) and returns a list of typed, bucketed action rows. No network, no new fetch, fully unit-testable with real temp-dir fixtures.

**REVISED - Tier 1 is no longer pure bookkeeping in the time-gate bucket (2026-07-22).** Two pieces are genuinely new logic, not lookups over emitted data:

1. **The binding-gate classifier (Section 1.6).** v1 is the cheap NO-TP-route rule above - a structural check that `Source` carries no `BuyFromTp` option, still trivial and still pure. The refinement (full three-test analysis for TP-liquid items) is real work and is deliberately deferred (and, per Section 5.5, blocked on TP order-book depth the module does not currently fetch).
2. **Cap-aware route splitting.** Even in v1, once a NO-TP item is surfaced as a gate, presenting "buy 1 this week from vendor V, the other N-1 from <next-cheapest route>" requires splitting the quantity across the capped and un-gated prices rather than echoing the solver's single warn-only route. This reaches into `PlanStep`/`VendorOfferBatch` per-offer cost data (Section 1.1) and is coupled to the currency valuation of test (b); it is solver-adjacent arithmetic. It nudges the Tier-1 effort class up (Section 5 / Effort class).

Neither piece needs a new API scope or new game-knowledge; both are self-consistent by construction. What changes versus the original framing is that "restate the plan against your wallet" is NOT automatically safe in the time-gate bucket - the ecto specimen proves a naive restatement is precisely the trust-destroying failure. The binding rule is what keeps Tier 1's "cannot be wrong in a trust-destroying way" claim true.

### Tier 2 - "Time-gate scheduler" (project capped purchases forward)

**What it is:** For every `TimegatedItem` in a plan (or across a priority set), compute a forward projection: "Item X needs `NeededCount` purchases at `CapValue`/<period> -> earliest completion is ceil(NeededCount / CapValue) <period>s from a clean start." Aggregate into a simple calendar-ish list ("This week: buy 10 of A, 5 of B. Next week: ...") sorted by longest pole (the item that gates overall completion).

**Data-quality assessment (MEASURED, and this is the crux):** only **692 of 53,529 offers (~1.3%)** carry any cap. The cap *values* are clean and game-plausible (daily 1/5/10/20; weekly 100/250), which is a good sign the M37 seeding was careful. BUT:

- The projection is only as complete as cap coverage. If a legendary path routes through a capped item the seed *missed*, the scheduler silently under-projects (says "done in 1 week" when reality is 3). **INFERRED risk:** cap coverage is a curated subset, not exhaustive; we cannot prove completeness from the data.
- Tier 2 is materially better *if* it can subtract already-consumed time-gates. The **only** honest source for that is `/v2/account/dailycrafting` (daily craft-once items) behind the new `progression` scope. For pure *vendor* caps there is no consumption feed, so Tier 2's vendor projections are always "from a clean slate" - accurate as a *minimum* number of periods, never as a "you're 2 days in" live countdown.
- Weekly/daily *reset timing* (server reset is 00:00 UTC daily / Monday 07:30 UTC weekly - GUESS on exact times, must be verified) is needed to phrase "this week vs next week". That is a constant, not a data feed, but it is a correctness detail.

**REVISED - the projector must filter for bindingness FIRST (2026-07-22).** Coverage completeness (the 1.3% concern above) is only the SECOND-worst failure mode. The worst is projecting a gate that is not binding. A projector that faithfully computes "ceil(86/1) = 86 weeks" for the ecto specimen is precisely, confidently, loudly wrong - worse than the coverage gap, because the coverage gap silently under-reports a real pole while this OVER-reports a phantom one and shouts it. A projector that projects a non-binding gate is wrong *by construction*, independent of how complete the seed is. So the projector's FIRST pass is not the ceil arithmetic; it is the Section 1.6 binding filter applied per `TimegatedItem`:

- **Drop any gate that fails the liquid/bound filter.** TP-liquid -> drop (unless the route-splitter proves the vendor dominates a sub-quantity, a refinement, not v1); account-bound / no-TP -> keep.
- **Project only the bound units.** For a kept gate, project the units that actually route through the capped offer (the route-split's capped portion), never the full `NeededCount` when the un-gated remainder covers the rest.
- **Rank surviving gates by longest pole across the priority set** (Section 3), applying test (b) against AGGREGATE currency demand so a currency that is cheap for one item but contested across the set is not spent blindly.

A projector that skips this pass and projects `TimegatedItems` verbatim reproduces the ecto specimen at scale.

**Verdict (REVISED 2026-07-22):** Tier 2 is buildable as a **binding-gate lower-bound projector**: it filters `TimegatedItems` through the Section 1.6 tests, projects only the bound units of the surviving gates, and frames the result as "at least N periods" (never a live day-by-day countdown for vendor caps - unbuildable honestly). Its accuracy is bounded by TWO things that must *both* hold: (i) seed cap coverage (unknown, so the projection is a lower bound at best) AND (ii) correct bindingness classification (a projector that projects non-binding gates is wrong regardless of coverage). The second bound is the harder one and the one the ecto specimen exposes; it did not appear in the original framing at all.

### Tier 3 - "Acquisition guidance" (farming / earning suggestions)

**What it is:** The most ambitious of the requirements - "go farm map Z", "earn currency Y by doing meta X". For an unpriceable/unvendorable node (`Source == UnknownSource`, or a currency with no vendor path), tell the user *where in the game* to get it.

**Honest assessment:** This needs data the repo does not have and cannot get from official APIs (1.3): drop tables, farm-efficiency, currency earn-rates, content-to-reward mappings. The ONLY existing hook is `AcquisitionHint` (wiki-seeded text like "SALVAGE"/"EXPLORE" + a sentence), which is *presentational* - it is honest precisely because it does not pretend to be a quantified recommendation. Turning that into "go do X next" at that level means:

1. Curating a currency-source table (medium wiki effort, moderate churn).
2. Curating drop/farm tables (large effort, high churn, low half-life).
3. Modeling earn-rate (impossible without invented numbers - violates the repo's no-invented-data invariant in full).

**Prior-art check (MEASURED via web research):** gw2efficiency - the most mature community tool, research-only reference here - ships a **Farming Tracker**, a **Legendary tracker**, and a **Dailies** page. Crucially, **none of these is a "do this next" recommender.** The Farming Tracker *measures* wallet/item deltas over a session you manually start/stop (a stopwatch, not an advisor). The Legendary tracker *shows collection progress* (read from the achievements API). The Dailies page *lists today's dailies* (not "which daily advances your goal"). **The single most capable tool in the ecosystem does not attempt Tier 3.** That is the strongest possible evidence that Tier 3 as imagined is not a "we just haven't built it" gap - it is a genuinely hard, arguably-unsolved problem, and any attempt risks inventing data. **Recommendation: Tier 3 stays out of scope, possibly forever.** The most we should honestly offer in that direction is surfacing the *existing* `AcquisitionHint` text next to blocked items ("this item is typically obtained by: <wiki hint>") - which is Tier 1 polish, not a new engine.

---

## 3. PRIORITY-ORDER SEMANTICS (>1 wanted item sharing materials and caps)

This is the genuinely interesting scheduling question and the part that is *not* just bookkeeping. Two sub-problems: **shared materials** and **shared caps**.

### 3.1 The model today (MEASURED)

The pipeline already solves multiple requested items together: `GenerateStructuredAsync(List<PlanRequestItem>)` wraps 2+ items in a synthetic multi-item root (`Gw2Constants.MultiItemWrapperItemId`), and the solver's per-stepKey aggregation **already merges shared demand** - if two legendaries each need 38 Mystic Clovers, the merged plan's clover step is 76, and `TimegatedItem.NeededCount` reflects the merged 76, not two separate 38s (MEASURED from `PlanStep`/`TimegatedItem` semantics + the merge doc comments). So *within a single generated batch*, shared-material accounting is already correct and global.

**What is NOT modeled today:** a user-defined **priority order** over the requested items, and any notion of "which item to finish *first*". The batch solve produces one merged shopping list with no per-item attribution of the capped rows. A Ranker priority list is genuinely new state (a stored ordered list of target items), and "which item to do first" is a genuinely new computation.

### 3.2 Greedy-per-priority vs global - worked example

Take the motivating example: **two legendaries, both needing Mystic Clovers**, under a weekly cap. (Clovers are Mystic-Forge-crafted rather than a single vendor row, so the "cap" in practice sits on upstream weekly-capped ingredients/currency conversions - GUESS on exact routing; the principle holds for any weekly-capped shared input, and the dataset does contain real `WeeklyCap` rows of 10/100/250 to anchor it.) Say the shared capped input is **item C, WeeklyCap = 10**, and:

- Legendary **A** (priority 1) needs 30 of C.
- Legendary **B** (priority 2) needs 20 of C.
- Combined demand = 50 of C, cap 10/week -> **5 weeks minimum regardless of strategy** (this is the invariant: the weekly cap gates the *sum*, and no ordering beats ceil(50/10) = 5).

The interesting part is not total time (fixed by the cap) but **what the user should do each week**, and here greedy-vs-global diverge in *user experience*, not in total duration:

- **Greedy per priority:** "Weeks 1-3: spend all 10/week toward A (finish A's 30 in week 3). Weeks 4-5: 10/week toward B." Result: **A is a usable legendary at end of week 3**, B at end of week 5. The user gets a finished item sooner.
- **Global / balanced:** "Each week: 6 toward A, 4 toward B (proportional)." Result: neither is finished until week 5; both cross the line together.

**Design position (GUESS, but a defensible one):** for legendary crafting, **greedy-by-priority is almost always what the user wants** - a finished legendary at week 3 is strictly more valuable than two half-legendaries at week 3, because a legendary is only useful when complete. The priority order the user set *is* the tie-break, and it should be honored greedily: pour capped throughput into priority-1 until it is satisfied, then priority-2, etc. Global balancing only wins in the rare case where the user explicitly wants "everything progresses evenly" (a psychological preference, not an efficiency one). **Recommendation: greedy-by-priority as the default and only mode for v1; do not build the global optimizer.** It adds real complexity (it is a bin-packing / scheduling problem once multiple caps with different periods interact) for a worse default outcome.

### 3.3 Where the hard part actually is (INFERRED)

The scheduling math for a *single* shared cap is trivial (division + greedy fill). The complexity explodes only if we try to co-schedule **many** caps with **different periods** (daily + weekly interacting), plus **shared non-capped materials** whose purchase order affects coin flow, plus **currency budgets** shared across items. That is a genuine constraint-scheduling problem. **v1 should not attempt it.** v1 should: (a) show the merged demand per capped item (already computed), (b) show greedy per-priority "do X toward your #1 goal first" ordering, (c) show the resulting minimum period count. That captures 90% of the user value at 10% of the complexity.

---

## 4. RISKS

1. **A wrong "go do X" is worse than no advice (trust).** This is the dominant risk and it shapes every tier decision. If the tool says "buy 10 of C from vendor V" and V doesn't sell C (stale seed), or "craft this now" when the user already hit the daily cap, the user stops trusting the *whole module*, including the plan tab that is otherwise correct. **Mitigation:** Tier 1 only ever asserts things derived from live snapshot + solver output (self-consistent by construction). Tier 2 must hedge ("at least N weeks", never "done Tuesday"). Tier 3's absence is itself a mitigation.
2. **Data staleness (snapshot + seed).** The wallet/inventory snapshot is up to 10 min stale (staleness threshold, MEASURED) and vendor caps/offers are a shipped seed frozen at build time. A "buy now, you can afford it" that was true 9 minutes ago may be false. **Mitigation:** show snapshot `CapturedAt` prominently; offer a Refresh; never present affordability as a guarantee, only as "as of <time>".
3. **Game-update churn.** ArenaNet reworks vendors, caps, and currencies regularly (e.g. the entire Wizard's Vault daily system replaced the old daily system). Any curated cap/source data decays. The 692 capped offers are a snapshot of *today's* game. **Mitigation:** Tier 1 rides on the plan (auto-correct when the seed is re-generated); Tier 2 inherits the seed's decay; Tier 3's curation burden is exactly why it stays out.
4. **Permission-escalation friction.** Adding `progression` scope for daily-crafting awareness asks the user to re-issue their API key. If done clumsily (hard dependency), it breaks the Ranker for everyone who doesn't. **Mitigation:** strict graceful degradation (1.4).
5. **Over-promising in UI copy.** A tab literally titled around "what to do next" invites the user to expect Tier 3. **Mitigation:** frame the tab honestly ("Next actions from your plan"), and make the empty/blocked states say *why* something can't be advised ("no known vendor/TP source - see acquisition hint").
6. **Coupling to the plan pipeline / M38 blast radius.** Running `GenerateStructuredAsync` per tracked item in the Ranker couples the Ranker to a pipeline that M38 WP-11/12/13/15 are actively refactoring. **Mitigation:** consume only the stable public `CraftingPlanResult` surface, never internals; sequence Ranker work *after* the WP-11..15 pipeline dedupe settles (see 8).

---

## 5. VERDICT

### 5.1 Feasibility rating per tier

| Tier | Feasibility | One-line justification |
|---|---|---|
| **Tier 1 - actionable now** | **HIGH / build it, with the binding-gate rule** | ~90% of the data is already emitted; the classifier is mostly bookkeeping BUT its time-gate bucket MUST apply the Section 1.6 binding rule (v1: surface caps only for NO-TP items) plus cap-aware route splitting, or it reproduces the ecto specimen. Still no new API, no new game-knowledge; self-consistent by construction *once the binding rule is in*. |
| **Tier 2 - time-gate scheduler** | **MEDIUM / binding-gate lower-bound projector only** | Cap data is live but only ~1.3% coverage AND must be filtered for bindingness (Section 1.6) BEFORE projection - a projector that projects a non-binding gate (the ecto specimen) is wrong by construction, not merely incomplete. Honest as "at least N periods", never a live countdown. Daily-crafting consumption still needs the new `progression` scope. |
| **Tier 3 - acquisition guidance** | **LOW / out of scope, possibly forever** | Requires drop/earn-rate data no API provides; even gw2efficiency doesn't attempt it; any quantified answer risks inventing data (invariant violation). Ship existing `AcquisitionHint` text at most. |

### 5.2 Recommended minimum viable slice

A **"Do Next" section in the Crafting Ranker tab**, Tier 1 only, over the *currently open / most-recently generated* plan first, then optionally over a small stored priority list:

- A `RankerStore` (new, JSON file under the data dir, atomic `.tmp`+`File.Replace`, `onError` callback from day one to match M38 WP-16 shape) holding an ordered `List<PlanRequestItem>` priority list plus per-entry display metadata. Real-file-IO Blish-free test alongside `SnapshotStoreTests`/`VendorOfferStoreTests`.
- A `NextActionClassifier` (new, pure, Blish-free) that takes `CraftingPlanResult` + `AccountSnapshot` and returns bucketed rows (Buy-TP-now / Buy-vendor-now / Craft-now / Blocked / Time-gated). Fully unit-tested with real fixtures.
- A Ranker view using **scroll pattern (A)** (single `FlowPanel(CanScroll=true)`, resize-only, like `MainView`/`LogTabContent`) - explicitly NOT the M33 heavy contract, keeping the Ranker out of the WP-21..26 blast radius. Coin cells call the **post-WP-21/22 `CoinCurrencyRenderer`**, never a third copy of `AddCoinSegment`.

### 5.3 What to build FIRST if greenlit

**Tier 1 "Do Next" over a single plan, rendered as a Ranker section - not a separate system.** Rationale: it reuses the entire existing pipeline output, needs zero new API scope, and delivers the user's core "go buy/craft this now" ask immediately. **REVISED (2026-07-22):** the earlier claim that Tier 1 "cannot be wrong in a trust-destroying way because it only restates the plan against your wallet" is now qualified - it holds for the four affordability buckets, but the fifth (time-gated) bucket CAN be trust-destroying if it restates `TimegatedItems` naively (the ecto specimen). So the binding-gate rule (Section 1.6, v1 = surface caps only for NO-TP items) and cap-aware route splitting are part of the FIRST cut, not deferrable polish - without them the first cut ships the ecto noise. The priority-list store and greedy multi-item ordering (Section 3) remain a fast follow. Tier 2's binding-gate lower-bound projector is a third increment, gated on a decision about the `progression` scope AND the binding-filter/route-splitter shared with Tier 1.

### 5.4 What stays out of scope (possibly forever)

- Any farming/drop/earn-rate recommendation (Tier 3 core).
- A live "you have K purchases left today" countdown for vendor caps (no API).
- A global multi-cap multi-period optimizer (Section 3.3) - greedy-by-priority is a better default anyway.
- Legendary *collection* progress via the achievements API - large curation, separate feature, not "what to do next".

### 5.5 REVISED - Open refinements: where the binding-gate principle is incomplete or can misfire (2026-07-22)

I adopt the three binding tests, the liquid/bound filter, cap-aware route splitting, and the salvage exclusion - they are correct and load-bearing. Four places where they are incomplete or can misfire, recorded rather than silently absorbed:

1. **TP liquidity is a spectrum, not a bit - and the module cannot currently SEE the spectrum (MEASURED).** The filter reads "has a `BuyFromTp` route" as "liquid, so escape hatch (c) holds." But `Services/Gw2PriceApiClient.cs` prices the TP from `/v2/commerce/prices`, which returns only the single best-buy/best-sell UNIT quote (`BuyUnitPrice` / `SellUnitPrice`) - no order-book depth. So "TP-liquid" structurally means "has a quoted price," and test (c) is evaluated as if infinite quantity were available at that one quote. For a thick commodity (ecto: millions/day) this is fine. For a thin book (a plan needing 500 of an item with 30 sell orders before the price triples), the un-gated remainder's true marginal cost rises steeply and a capped-but-cheap vendor route CAN dominate the cheapest path for MORE than its cap - i.e. the cap binds even though the item is nominally "TP-liquid." v1's NO-TP-route rule dodges this entirely (it only surfaces items with no TP route at all), but the liquid-item REFINEMENT cannot be done honestly without depth data the module does not fetch today. **Adding `/v2/commerce/listings` (depth) is the real prerequisite for "full three-test analysis on liquid items"** and is out of scope here - flag it as the gating dependency, not a free follow-on.

2. **The liquid/bound filter is binary, but bindingness is per-sub-quantity.** A TP-liquid item whose capped vendor is CHEAPER than the TP has a cap that genuinely binds on the cheapest path for the vendor-dominant sub-quantity - exactly the route-splitting case. The v1 rule suppresses it (accepts a foregone cheap purchase to never mislead). That is the right v1 trade, but its cost is real, not zero: where the capped currency is near-free to the user (candy corn), suppressing the cheap route foregoes real savings. Route splitting is what recovers it; until it ships, the honest framing is "v1 chooses never-mislead over always-cheapest," not "the liquid filter is free."

3. **Route splitting is coupled to test (b), not sequential.** "Capped units at the gated price, remainder at next-cheapest" is only correct for currencies that pass the opportunity-cost test against the AGGREGATE watched-set demand. A currency contested by a higher-priority watched item should NOT be spent on the capped units at all - the split collapses to "route everything un-gated." So the splitter takes the per-currency valuation as an INPUT; it is not a pure per-item function. Section 1.6 states the split mechanically; in practice it is valuation-driven, which is also why test (b)'s valuation seam (Section 1.6) is a hard dependency of the splitter, not just of the classifier.

4. **The salvage exclusion is safe for liquid items but can produce FALSE-POSITIVE gates on bound items (the mirror of the ecto under-warn).** "TP price embeds salvage economics" holds only where a TP price exists. For an account-bound item whose only un-gated alternative to a capped vendor is salvage, dropping salvage from the model removes the very escape hatch test (c) needs, so the projector can conclude "no escape hatch -> cap binds" when a salvage route actually defeats it. Because v1 surfaces gates ONLY for NO-TP items - exactly the bound items where this bites - the mitigation must ship WITH v1: alongside any surfaced capped-vendor gate, render the existing wiki `AcquisitionHint` (SALVAGE / EXPLORE, Section 1.1) for that item, so the user sees the alternative route and the cap is never presented as the sole path. This reuses Tier 1's already-proposed hint surfacing; no new data.

**One structural boundary (not a misfire).** The three tests decide whether a gate is REAL (Tier 1 classification). Whether acting on a real gate THIS period is worth surfacing NOW is a separate, fourth consideration - critical-path / longest-pole relevance - and it belongs to Tier 2 scheduling (Section 3's greedy-by-priority), not to the binding classifier. A binding gate on an item that will finish long before the priority-1 longest pole is real but low-value to surface this week. I keep this OUT of the three BINDING tests (they are about economic reality) and locate it in the SURFACING/ranking layer, to avoid conflating "is this gate real" with "should I mention it now." I considered promoting it to a fourth binding test and rejected that: it is a ranking concern, not a bindingness one.

---

## PROPOSAL FORMAT SUMMARY (per brief)

**Problem / intent:** Turn generated plans + live account snapshot into a concrete "go do this next" list, honestly bounded to what the data supports (scope stated in Section 0).

**Proposed UX:** Crafting Ranker tab gains a "Do Next" list. Each row: item icon + name, action verb ("Buy on Trading Post", "Buy from <merchant> at <location>", "Craft (<discipline>)"), quantity, cost (coin cell via shared `CoinCurrencyRenderer`; currency cells right-of-number per invariant), and an affordability state chip (Ready / Short by X / Time-gated: N weeks). Sections grouped by bucket, "Ready now" first. Empty state: "Generate a plan (or add items to your priority list) to see what to do next." Blocked-with-no-source state shows the wiki `AcquisitionHint` text if present, else "No known vendor or Trading Post source." A snapshot-freshness line ("as of <CapturedAt>") with a Refresh affordance.

**Data & architecture:** Reuses `CraftingPlanPipeline.GenerateStructuredAsync`/`ResolveWithOverrides`, `CraftingPlanResult`/`CraftingPlan`/`PlanStep`, `AccountSnapshot`, `AccountCurrencyIndex`, `AccountItemIndex`, `AcquisitionHint`, `VendorOffer.MerchantName`/`Locations`, `Plan.TimegatedItems`. NEW: `NextActionClassifier` (pure, Blish-free), `RankerStore` (JSON file under data dir, atomic write + `onError` callback per WP-16, tested with real temp-dir IO). Threading: classification is synchronous/pure and runs off the marshaled plan result on the main thread like existing view refreshes; per-item plan generation reuses the pipeline's existing async + `MainThreadMarshal.Run` drain (no new threading primitive). Persistence format/location: `data/ranker.json` (INFERRED name), Newtonsoft indented JSON, same shape as `snapshot.json`/`vendor offer` stores.

**Settings introduced:** Optionally a `RankerPriorityMaxEntries` int (trivial `DefineSetting<int>`); a bool "Use daily-crafting API (requires progression permission)" gating any Tier 2 daily-crafting enhancement (default off). Follow SettingsTabContent idiom (a) immediate-apply checkbox / (b) TextBox+Save; no new control idiom. A large curated priority list, if it ever outgrows a single setting, uses the file-backed `RankerStore` rather than the JSON-in-one-SettingEntry trick.

**Invariant / contract impacts:** No IDs shown to users (action rows show names/icons only). Coin icons right of number via shared renderer. Tests stay Blish-free (classifier + store are pure). No runtime wiki/gw2efficiency calls (hints are the existing local seed). ASCII-only source. Scroll pattern (A) keeps the Ranker out of the M33 `PlanContentHeightMath`/relayout-registry contract (WP-21..26). Adding `progression` scope (Tier 2 only) is a user-visible permission change - must degrade gracefully.

**Effort class (REVISED 2026-07-22):**
- Tier 1 "Do Next" over a single plan (classifier + Ranker view + tests): **M -> M/L** - the base classifier is still one pure service + one pattern-(A) view + store on existing pipeline output, but the Section 1.6 binding-gate rule and cap-aware route splitting add real per-item, solver-adjacent arithmetic (splitting a quantity across a capped price and the next-cheapest route, coupled to currency valuation) that is not present in the raw plan output. The v1 NO-TP-route rule keeps the binding CLASSIFIER trivial; the route SPLITTER is the piece that lifts this above pure bookkeeping and justifies the bump.
- Priority-list store + greedy multi-item ordering: **+S-M** - new store + ordered-list UI + greedy fill math (Section 3.2). (Unchanged.)
- Tier 2 binding-gate lower-bound projector (+ optional daily-crafting scope): **+M** (class unchanged; scope refined) - forward projection stays simple arithmetic, but it now MUST run the Section 1.6 binding filter and reuse the Tier-1 route-splitter *before* projecting (shared logic, so no separate bump), on top of the `progression` scope escalation, graceful degradation, and reset-timing correctness.
- Tier 3: **XL and not recommended** - blocked on non-existent data; would require a curation program, not a coding task. (Unchanged; the binding principle only reinforces the exclusion.)

**Dependencies & sequencing:**
- **After** M38 WP-21/22 (so the Ranker calls the shared `CoinCurrencyRenderer`, not a 3rd coin-code copy).
- **After** M38 WP-11/12/13/15 pipeline dedupe settles (Ranker consumes `GenerateStructuredAsync`; building against a moving pipeline invites churn).
- **New store follows** WP-16's `onError`-callback shape from day one (avoid being a 5th store WP-16 must retrofit).
- **If** any Tier 2 feature spawns a `FrameTicker` (e.g. a countdown), its teardown registers in the WP-17 Unload path.
- **Independent of / synergistic with** the Plan History proposal (both want a small stored "plan request" record - `PlanRequestItem` list + summary; a shared persisted request shape could serve both - flag for cross-proposal reconciliation).
- Correct the stale "TimegatedItems always empty" comments in `CraftingPlan.cs`/`TimegatedItem.cs` (docs sweep; not currently in any WP).

**Open questions:** see below.
