# gw2efficiency Convergence Matrix

Date: 2026-08-15

> DO-NOT-TOUCH references below predate the 2026-08-17 high-evidence-zone
> policy - see docs/KNOWN-ISSUES.md's policy note. A DO-NOT-TOUCH citation
> is no longer a blocking verdict; it is a proof requirement (see that
> note for what proof looks like). Individual ADOPT/PRESERVE calls below
> have not been re-litigated under the new rule.

## Method

Five area readers independently compared GW2CraftingHelper's crafting-planner
logic against gw2efficiency's (gw2e) publicly available source and live
behavior, across five areas:

1. Tree construction (gw2e `recipe-nesting` vs `Services/CraftingTreeBuilder.cs`,
   `Services/RecipeService.cs`, `Services/RecipeNodeIds.cs`, `Models/RecipeNode.cs`)
2. Costing and decisions (craft-vs-buy rule, forceBuy semantics, vendor price
   table usage, TP pricing basis, fee math, overproduction/sellable quantity,
   rounding)
3. Valuations and account integration (currency decision-pricing, item-value
   pipeline, own-materials reduce/value/annotate semantics)
4. Tree/plan display conventions (gw2e `componentTree.html` + `calculator.html`
   vs `TreeSectionController.cs` / `DecisionPillPlanner.cs` / `PlanViewModelBuilder.cs`)
5. Edge-case handling sweep (account-bound/no-TP items, no-recipe-no-price
   items, timegated items, vendor purchase caps, recipe-unlock gating,
   precursor/legendary mechanics, decorations/guild upgrades)

This document is the synthesis pass over those five reads: 62 raw findings
were deduplicated to 57 distinct matrix rows (5 pairs of rows independently
reported the same mechanism from two areas; each pair is merged into one row
below, noted where it happened). A subsequent correction pass fixed four
verdicts, annotated several evidence gaps in place, and added row 63
(Homestead Refinement efficiency tiers - a mechanism the five-area sweep
missed entirely) - see the "Updated by a correction pass" note at the top of
the Summary section for the full list of what changed.

## Evidence standard

Every claim is labeled:

- **measured** - fetched or read directly, with a source path or URL and an
  excerpt or line reference. On the gw2e side this means the real MIT-licensed
  source (`recipe-nesting`, `recipe-calculation`, `tradingpost-fees` on
  `raw.githubusercontent.com/gw2efficiency/...`), the live app templates
  (`gw2efficiency.com/views/...`), or the query APIs
  (`api.gw2efficiency.com/recipes|items`, `api.guildwars2.com`). On our side
  this means a file:line citation into this repository (read-only for this
  audit).
- **inferred** - a reasonable conclusion from the measured evidence but not
  itself independently confirmed; called out explicitly wherever it appears.

## Decision rules applied

- **ADOPT**: gw2e's behavior is better for core functionality/problem-handling,
  or the comparison revealed a bug in ours. Implemented by follow-up packages,
  not by this audit.
- **PRESERVE**: the difference is taste, or our approach is a marked
  improvement. Ours stays; the open question goes to
  `docs/gw2e-considerations.md`.
- **EQUIVALENT**: behavior matches in substance (implementation details may
  differ).
- **INVESTIGATE**: evidence is genuinely insufficient; noted with what would
  resolve it.

No ADOPT below violates a repo invariant or touches a DO-NOT-TOUCH item
(ModuleLog locking, PlanContentHeightMath, PlanRelayoutMath, scroll machinery,
merged-ceil vendor batching). Where gw2e's behavior would require violating a
hard constraint (chiefly "do not invent data when APIs are missing"), the row
is PRESERVE with a constraint-blocked considerations entry, never ADOPT.

Rows already known/in-flight per existing project tracking (KNOWN-ISSUES.md,
gw2e-parity-spec.md, or named in-flight work) are marked **[already-known]**
or **[in-flight]** and are not re-litigated here; they are included for
completeness of area coverage and because several add corroborating fresh
evidence.

---

## Area 1: Tree construction

*gw2e `recipe-nesting` vs `Services/CraftingTreeBuilder.cs`,
`Services/RecipeService.cs`, `Services/RecipeNodeIds.cs`, `Models/RecipeNode.cs`*

### 1. Nesting keyed by output item id (one recipe resolved per output) [already-known, ARE case]

- gw2e (measured): `recipe-nesting/src/index.ts`, `nestRecipes()`:
  `const recipesMap = toMap(recipes, 'id')` where `TransformedRecipe.id =
  recipe.output_item_id`. Multiple raw recipes sharing an output_item_id
  collide in one map slot; exactly one recipe survives nesting per output.
- Ours (measured): `Services/RecipeService.cs:308-380` `BuildNodeAsync`
  iterates ALL recipe ids for an output and appends every resulting
  `RecipeOption` to `node.Recipes` (a list, not a map) - all alternate
  recipes are preserved as sibling options.
- Verdict: **PRESERVE**
- Rationale: documented 2026-08-15 (ARE case): our per-node
  `CanCraft`/`CanBuyTp`/`CanBuyVendor` is better than gw2e's one-recipe
  ceiling. This row only re-confirms the mechanism at the source; not
  re-litigated, no new considerations entry.

### 2. Cycle protection during tree build

- gw2e (measured): `recipe-nesting/src/index.ts` `nestRecipe()` uses a
  batch-scoped `recipe.nested` flag plus a direct self-reference guard
  (`if (recipe.id === id) return component`), plus a hardcoded 4-item special
  case for Condensed Ley-Line Essence ids `[91224, 91137, 91222, 91171]` that
  flattens ALL pairwise references among the four, even non-cyclic ones.
- Ours (measured): `Services/RecipeService.cs` `BuildNodeAsync` lines
  276-302, 384-386 - a per-build `HashSet<int> visiting` tracks the current
  ancestor path; a collision collapses only that occurrence to a leaf.
  Verified against `ref/recipes_seed.json`: recipe -1354 (output 91224) lists
  ingredient 91137, recipe -1344 (output 91137) lists ingredient 91224 - the
  exact real 2-cycle gw2e hardcodes for. Hand-traced: our path-based check
  only intervenes on the genuine cycle edge; the other two ids in gw2e's
  hardcoded list (91222, 91171) are not on any real cycle and fully expand
  under our algorithm, unlike gw2e's blanket 4-item flattening.
- Verdict: **PRESERVE**
- Rationale: a general, sound, ancestor-path algorithm that handles cycles of
  any depth without a curated id list, and (verified against the exact items
  gw2e needed to hardcode) produces a strictly more complete tree for the
  non-cyclic legs. Marked improvement, not just taste. Considerations entry
  added for awareness (no open question).

### 3. Node identity / dedup key for downstream consumers

- gw2e (measured): `recipe-nesting` has no per-occurrence id; identity is the
  GW2 item id, with quantity resolution deferred to a later
  `calculateTreeQuantity` pass operating on a per-edge ratio.
- Ours (measured): `Services/RecipeNodeIds.cs` assigns every node a distinct
  deterministic pre-order-DFS `NodeId`, even for repeated item ids.
  `Services/PlanSolver.cs` memoizes by `NodeId`, not item id.
- Verdict: **EQUIVALENT**
- Rationale: different mechanisms for the same purpose, a direct consequence
  of an already-documented architecture choice (`docs/research/m37-r3-achievement-dedup.md`
  Section 3.3: we bake absolute quantity at build time instead of deferring).
  No correctness gap either way.

### 4. Achievement-bit ingredient dedup (KNOWN-ISSUES #26) [already implemented]

- gw2e (measured via `docs/research/m37-r3-achievement-dedup.md`, quoting
  `recipe-calculation@master` verbatim): classifies bit/normal item ids
  tree-wide, pre-seeds `ignoredBitItemIds`, zeroes duplicate achievement-bit
  occurrences during the quantity pass.
- Ours (measured): `Services/AchievementBitDedupPrePass.cs` (196 lines) ports
  this 1:1, with two evidence-backed, documented deviations: also clears
  `node.Recipes` on a zeroed occurrence (matches `InventoryReducer`'s
  zero-quantity convention); walks only `node.Recipes[0]` since our tree can
  carry multiple alternate recipes per node (a direct consequence of row 1).
  Wired in `Services/CraftingPlanPipeline.cs:147/587`; covered by
  `AchievementBitDedupPrePassTests.cs`.
- Verdict: **EQUIVALENT**
- Rationale: already implemented and tested; the port is faithful and both
  deviations are justified. No action.

### 5. GuildUpgrade ingredient type (Guild Hall / Scribe decoration components) [reachable once in-flight versioned-recipe ingestion lands]

- gw2e (measured): `recipe-nesting/src/api.d.ts` declares
  `type: 'Item' | 'GuildUpgrade' | 'Currency'`. `nestRecipe()` resolves a
  GuildUpgrade component via `recipeUpgradesMap` into a nested recipe node,
  falls back to `decorationsMap`, and drops it cleanly (`return false`,
  filtered by `compact()`) if neither resolves.
- Ours (measured): `Services/IRecipeApiClient.cs` `RawIngredient.Type` is an
  unconstrained string with no GuildUpgrade concept anywhere in the codebase
  (`grep -ril GuildUpgrade --include=*.cs .` = zero hits).
  `Services/RecipeService.cs:294-297` treats any non-"Item" type as a leaf;
  `Services/CraftingTreeBuilder.cs:80-86` then labels it
  `CraftingDecision.Currency` and calls `Gw2Constants.ResolveCurrencyName`,
  which falls back to the literal string "Currency" for unrecognized ids.
  Severity is worse than a mis-rendered pill: `Services/PlanSolver.cs:428`
  special-cases only `ingredient.IngredientType == "Currency"` - a
  GuildUpgrade leaf does not match that check, falls through to the same
  method's `Evaluate()` call, and gets priced as an ordinary ITEM against
  whatever Trading Post item happens to share that numeric id, while
  `CraftingTreeBuilder.cs:80-86` simultaneously labels the very same node
  "Currency" in the tree display. That is a wrong cost and a wrong
  shopping-list line, not only a labeling defect.
- Verdict: **ADOPT** (size: S-M)
- Rationale: corrected from an earlier pass, which understated both
  reachability and severity. Reachability: the earlier claim that
  `ref/recipes_seed.json`'s ingredient `type` values are `{"Item"}` only
  today is false - measured this pass (`python` scan of the same seed file,
  14,736 recipe entries): 47,554 `Item` + 3 `Currency` ingredients, zero
  `GuildUpgrade`, i.e. exactly `{Item, Currency}`, matching row 62's own
  correct citation of the same file (the two rows previously contradicted
  each other on this point; row 62 was right, this row was wrong). But the
  seed's absence of `GuildUpgrade` is an artifact of how it was ingested,
  not of GW2's real recipe data: measured this pass via
  `api.guildwars2.com/v2/recipes/12002?v=2022-03-23`, which returns
  `{"type":"GuildUpgrade","id":829,"count":5}` as a real ingredient (a
  600-recipe versioned sample carries 70 `GuildUpgrade` + 41 `Currency`
  occurrences) - the unversioned endpoint this seed was evidently built from
  returns no `type` field at all. That gap is exactly what the in-flight
  versioned-recipe ingestion fix closes, so this path goes from latent to
  live the moment that branch merges; it is not a hypothetical future case.
  Fix: recognize "GuildUpgrade" explicitly in both
  `Services/PlanSolver.cs:428`'s Currency special-case (so it is not silently
  costed as an item) and `Services/CraftingTreeBuilder.cs:80-86`'s display
  branch (so it does not render as "Currency"), routing it to
  Unknown/hint-driven display instead until real resolution exists (full
  recipe/decoration resolution is a larger, separate consideration - see
  Edge-case row 62, "Guild Hall decorations / permanent guild upgrades").

### 6. Currency ingredient passthrough

- gw2e (measured): `nestRecipe()`: `if (component.type === 'Currency') return
  component` - untouched, never recursed. Currency nodes are also explicitly
  excluded from achievement_bit dedup classification.
- Ours (measured): `Services/RecipeService.cs:294-297` returns a bare leaf
  for a non-"Item" ingredient without recursion.
  `Services/AchievementBitDedupPrePass.cs:122/173` both gate on
  `node.IngredientType == "Item"`, excluding Currency exactly as gw2e does.
- Verdict: **EQUIVALENT**
- Rationale: both treat Currency as a terminal, unresolved leaf and exclude
  it from dedup. No divergence.

### 7. Per-craft ingredient quantity scaling (quantity multiplication)

- gw2e (measured, `recipe-calculation`'s `calculateTreeQuantityInner`):
  `componentAmount = Math.ceil(usedQuantity / output)`, derived generically
  at every node in a single downstream pass that runs after nesting.
- Ours (measured): `Services/RecipeService.cs:346-373` computes
  `craftsNeeded = Math.Ceiling(quantity / expectedOutputCount)` once per
  `RecipeOption` at tree-build time, baked into the child node's absolute
  Quantity rather than resolved later.
- Verdict: **EQUIVALENT**
- Rationale: same ceil-based math, computed at a different pipeline stage per
  the already-documented build-time-vs-deferred architecture split (row 3).

### 8. Fractional / expected-value output modeling (Mystic Clover-style probabilistic recipes)

- gw2e (measured): a single fractional `output_item_count` (0.31 for Mystic
  Clover) is reused generically by `calculateTreeQuantityInner`'s ceil/round
  math for every recipe, ordinary or probabilistic alike.
- Ours (measured): `Services/IRecipeApiClient.cs` splits this into
  `RawRecipe.OutputItemCount` (int, true per-success yield = 1) and
  `RawRecipe.ExpectedOutputCount` (nullable double, probability-adjusted
  average = 0.31); `Services/RecipeService.cs:329-331` defaults the latter to
  the former for ordinary recipes and only the fractional value drives
  `craftsNeeded`. Confirmed in `ref/recipes_seed.json`'s Mystic Clover entry
  (recipe -1591): `"outputItemCount": 1, "expectedOutputCount": 0.31`.
- Verdict: **PRESERVE**
- Rationale: gw2e conflates "true yield on success" and "probability-weighted
  average yield" into one number; ours keeps them distinct and correctly
  named, avoiding a real ambiguity in gw2e's raw data. Marked improvement.
  Considerations entry added for awareness.

### 9. Whether batch-rounding overproduction is reflected in a node's own displayed quantity / buy price

- gw2e (measured): `calculateTreeQuantityInner` rounds a node's own
  totalQuantity up to a whole multiple of its own recipe output BEFORE
  computing usedQuantity, so `buyPrice = tree.usedQuantity * buyPriceEach`
  reflects the rounded-up quantity even on the TP buy-price pill for a
  craftable-but-bought item.
- Ours (measured): `Services/RecipeService.cs` sets `node.Quantity =
  quantity` (the exact parent-requested value) with no rounding to a
  multiple of this node's own recipe output; the craftsNeeded-driven
  rounding only scales this node's OWN children's ingredient quantities.
  No field anywhere surfaces CraftsNeeded/OutputCount for display (no
  references in `Views/` or `Contracts/`).
- Verdict: **PRESERVE**
- Rationale: if bought via TP, ours charges for exactly the raw amount
  needed - economically correct (you would not buy 10 on the TP just because
  a hypothetical craft path yields in whole batches). gw2e's uniform
  inflation of the buy-price basis is an artifact of reusing one formula for
  both paths. However ours also never surfaces that Craft would yield more
  than requested - a minor display gap. Net: arguably more correct for the
  buy side but a genuine display nuance; considerations entry added.

### 10. Recipe `flags` retention (e.g. AutoLearned)

- gw2e (measured): `recipe-nesting/src/api.d.ts`'s raw entry carries `flags`,
  but `transformRecipe()` does NOT copy `flags` onto `TransformedRecipe` at
  all - silently dropped at the nesting stage, structurally unavailable
  downstream.
- Ours (measured): `Services/IRecipeApiClient.cs` `RawRecipe.Flags` is
  carried onto `RecipeOption.Flags`, preserved across
  `InventoryReducer` clones, and actively read:
  `Services/PlanResultBuilder.cs:335` `option.Flags.Contains("AutoLearned")`
  drives the Required Recipes section's auto-learned distinction.
- Verdict: **PRESERVE**
- Rationale: ours retains and uses real recipe metadata gw2e's own nesting
  layer structurally cannot pass through at all. Marked improvement.
  Considerations entry added for awareness.

### 11. Recipe-level achievement_id (recipe itself achievement-gated, distinct from ingredient-level achievement_bit)

- gw2e (measured): `transformRecipe()` carries `achievement_id` as pure
  passthrough metadata; confirmed neither `calculateTreePrices.ts` nor
  `calculateTreeCraftFlags.ts` reference it - informational only.
- Ours (measured): `Services/IRecipeApiClient.cs` `RawRecipe.AchievementId`
  is documented "informational only: NOT read by AchievementBitDedupPrePass"
  - reserved for a future display feature, not wired into any decision.
- Verdict: **EQUIVALENT**
- Rationale: both sides carry this as inert metadata not touching any
  pricing/craft/dedup decision. No divergence.

### 12. Quantity resolution timing: baked-at-build-time (absolute) vs deferred-to-a-separate-pass (per-edge ratio) [already documented]

- gw2e (measured): `NestedRecipe` stores only a per-edge quantity ratio; all
  absolute quantity resolution is deferred to `calculateTreeQuantity`,
  re-run fresh and immutably on every `cheapestTree`/`updateTree` call.
- Ours (measured): `Services/RecipeService.cs` `BuildNodeAsync` bakes each
  node's absolute integer Quantity once at build time; no separate
  downstream resolution pass; PlanSolver/CraftingTreeBuilder treat Quantity
  as final and re-solve the same tree object.
- Verdict: **PRESERVE**
- Rationale: this is the same foundational difference already documented in
  `docs/research/m37-r3-achievement-dedup.md` Section 3.3/4.2, which
  concluded our approach is strictly safer than gw2e's own equivalent
  interactive-update path (gw2e's `updateTree.ts` does not re-run its
  classification pre-pass and can silently un-dedupe a node after a manual
  pill click). Re-confirmed from the nesting-layer side; no new
  considerations entry needed, already tracked.

### 13. Vendor-purchase-cap fields on the nested tree (daily_purchase_cap / weekly_purchase_cap)

- gw2e (measured): `transformRecipe()` carries `daily_purchase_cap`/
  `weekly_purchase_cap` directly on every nested recipe node (vendor
  purchases are modeled as Merchant-discipline recipes).
- Ours (measured): `Models/RecipeNode.cs` and `Models/RecipeOption.cs` have
  no purchase-cap fields; vendor offers and caps live entirely outside the
  recipe tree in `ref/vendor_offers.json`, consumed by
  `VendorOffer`/`VendorBatchSolver`.
- Verdict: **PRESERVE**
- Rationale: already-known, already-documented architectural divergence
  (`gw2e-parity-spec.md` Section 3), and the merged-ceil vendor batching this
  feeds is a DO-NOT-TOUCH item. Not actionable in this area; not
  re-litigated, no new considerations entry.

---

## Area 2: Costing and decisions

*craft-vs-buy decision rule, forceBuy semantics, vendor price table usage, TP
pricing basis, fee math, overproduction/sellable quantity, craftResultPrice,
tree re-flagging, rounding*

### 14. Core craft-vs-buy/vendor decision rule and decision-price bookkeeping

- gw2e (measured, `calculateTreeCraftFlags.ts:8-15`, commit ea10eb8):
  `craft = hasComponents && isUsed && isCheaperToCraft && !isForceBuy`,
  `isCheaperToCraft = craftPrice !== undefined && (!buyPrice || decisionPrice
  < buyPrice)` - strict less-than, tie favors buy, missing buyPrice
  force-crafts. `calculateTreePrices.ts:63-74` has three tri-state regimes
  (unset/true/false).
- Ours (measured, `Services/PlanSolver.cs:606-637` `PickCheapest` +
  353-588 `Evaluate`): `craftBeatsBuy = craftCost.HasValue &&
  (!buyCost.HasValue || craftCost.Value < buyCost.Value)` - identical
  strict-less-than and missing-buy-price force-craft. All three gw2e regimes
  map 1:1 to our unset/forced-craft-override/forced-buy-exclusion branches.
- Verdict: **EQUIVALENT**
- Rationale: all three regimes reproduced structurally, not just in outcome.
  Highest-confidence match in this area.

### 15. Currency ingredient cost: contributes to the craft/buy decision only, zero to the displayed real coin total

*(Independently reported by both the Costing and the Valuations readers; merged here.)*

- gw2e (measured, `gw2e-parity-spec.md` Section 4.3 quoting
  `calculateTreePrices.ts:21-38`): a non-Coin Currency leaf's
  `craftResultPrice`/`buyPrice` is always `false`, coerced to 0 when summed;
  its `decisionPrice` uses `CURRENCY_DECISION_PRICES`/`customCurrencyPrices`
  and DOES flow into the parent's `craftDecisionPrice`.
- Ours (measured, `Services/PlanSolver.cs:395-464`): a Currency-type
  ingredient never recurses into `Evaluate`; its contribution is added ONLY
  to the local `craftCost` (comparison value) accumulator when
  `currencyValuation.TryGetCopperValue` succeeds - `craftRealCost` (which
  becomes the displayed `Decision.TotalCost`) never sees it.
- Verdict: **EQUIVALENT**
- Rationale: same two-track split (decision value vs real coin total), same
  "unvalued currency contributes zero to both" fallback. Matches
  `gw2e-parity-spec.md` normative directive #5 exactly. Already correctly
  implemented on our side, consistent with the currency-comparability fix
  in flight.

### 16. Default currency decision-value source (used when the user hasn't priced a currency)

*(Independently reported by both the Costing and the Valuations readers; merged here.)*

- gw2e (measured, `static/currencyDecisionPrices.ts`, commit ea10eb8): ships
  a hardcoded 46-entry `CURRENCY_DECISION_PRICES` table (Karma=1, Spirit
  Shard=3600, Badge of Honor=23, ...) applied automatically as fallback -
  the maintainers' own unlabeled optimization-only guesses, not real TP
  prices (most aren't even tradable). Overridable per-call via
  `customCurrencyPrices`.
- Ours (measured, `Models/CurrencyValuation.cs`): `CurrencyValuation.None`
  is the default (empty dictionary); the class's own doc comment states the
  solver never invents one - only user-priced currencies are usable for cost
  comparison. No built-in default table exists anywhere in the codebase.
- Verdict: **PRESERVE** (constraint-blocked)
- Rationale: porting gw2e's hardcoded table would directly violate the repo
  HARD CONSTRAINT "do not invent data when APIs are missing" - no ADOPT is
  possible. The gap is real (a fresh install gets zero currency-aware
  craft/buy comparisons until the user configures something) but the fix
  space is a product decision, not a mechanical port. Considerations entry
  added.

### 17. Vendor offer with an unvalued non-coin currency line: comparability with coin/craft options [in-flight]

- gw2e (measured): vendor purchases are Merchant-discipline recipes, so a
  Merchant recipe's currency ingredients get identical treatment to any
  other recipe's currency ingredients (row 15): an unvalued currency
  contributes 0 and the recipe fully participates in the normal
  cheapest-recipe comparison. No separate "incomparable, fallback only" tier
  for vendor offers exists in gw2e's model.
- Ours (measured, `Services/VendorBatchSolver.cs:274-320, 328-370`): an
  offer with even one non-coin currency line lacking a
  `currencyValuation` entry sets `allValued=false`, routing the WHOLE offer
  to a fallback tier ranked only against other fallback offers, never
  against TP/craft in `PickCheapest`.
- Verdict: **ADOPT** (the asymmetry finding only, in-flight - named in task
  context as "craft/vendor comparability asymmetry fix"); the RESOLUTION
  DIRECTION is corrected below and is not part of this ADOPT.
- Rationale: this row documents the mechanism independently but is not a new
  discovery, and the asymmetry itself is real and belongs here: our own
  craft-ingredient handling (`Services/PlanSolver.cs:428-456`) treats an
  unvalued currency as zero-contribution and lets the recipe compete
  normally (row 15), while vendor-offer handling
  (`Services/VendorBatchSolver.cs:274-320`) disqualifies the whole offer for
  the identical condition - an internal inconsistency in our own logic
  worth fixing regardless of which direction the fix takes.

  Corrected from an earlier pass: that pass additionally prescribed gw2e's
  specific resolution - uniform treatment where an unvalued currency
  contributes 0 and the offer competes normally in `PickCheapest` - as the
  fix direction. That prescription does not survive scrutiny against the
  repo's own hard constraint of "no invalid currency comparisons": a vendor
  offer priced entirely in an unvalued currency (e.g. 500 Karma, no user
  valuation configured) would evaluate to 0 copper under gw2e's model and
  win outright against every priced coin/craft alternative, producing a
  zero-cost plan for something that in reality costs real currency the user
  hasn't told the solver how to weigh. The craft-side precedent this row
  leaned on for "gw2e's model doesn't violate the invariant any more than
  our own craft-side handling already accepts" is not equivalent: on the
  craft side, an unvalued currency is one ingredient among others that ARE
  priced, so the resulting understatement is bounded by the rest of the
  recipe's real cost; a vendor offer can be 100% currency, with nothing
  bounding the understatement. Symmetry could equally be restored in the
  opposite direction instead - making an unvalued currency ingredient
  disqualify its recipe from comparison too, the same way an unvalued
  vendor offer is disqualified today. Per this audit's own decision rules,
  a resolution direction that conflicts with a hard constraint goes to
  `docs/gw2e-considerations.md` to be worked through, rather
  than being asserted as the ADOPT; the asymmetry finding itself remains an
  ADOPT action item independent of that open question.

### 18. Vendor-purchasability modeling: separate acquisition-source arm with its own tie-break vs gw2e's "vendor is just a recipe"

- gw2e (measured, `gw2e-parity-spec.md` Section 3.2 + normative directive
  #4): a vendor purchase IS a `NestedRecipe` (disciplines=['Merchant']); it
  competes for "cheapest recipe at this node" the same way any recipe does,
  then that winner competes against buyPrice via the single
  `isCheaperToCraft` rule. No independent three-way comparison exists.
- Ours (measured, `Services/PlanSolver.cs:606-637` `PickCheapest`): an
  explicit three-way comparison (buy/craft/vendor) with its own tie-break -
  "when both craft and vendor beat buy, the numerically cheaper wins; an
  exact craft/vendor tie keeps vendor." Vendor offers are evaluated by a
  structurally separate engine (`VendorBatchSolver.EvaluateVendorOffers`)
  from `node.Recipes`.
- Verdict: **PRESERVE**
- Rationale: the same structural divergence already flagged by
  `gw2e-parity-spec.md` normative directive #4 and closely related to the
  already-documented one-recipe-per-output ARE finding (row 1). Our separate
  VendorOffer architecture is what makes the DO-NOT-TOUCH merged-ceil vendor
  batching, purchase-cap notices, and W4B vendor cost-component leaves
  possible - collapsing vendor into the recipe-candidate list would conflict
  with those hard constraints. Genuine marked improvement; considerations
  entry logs the craft/vendor tie-break rule itself, since gw2e has no
  equivalent rule to compare against.

### 19. TP price-basis field mapping (buy order vs instant-buy) and its default

- gw2e (measured, `gw2e-parity-spec.md` Section 2.1-2.2): default `price =
  "buy"`; `"buy"` maps to `buys[0].unit_price` (highest standing buy order),
  `"sell"` maps to `sells[0].unit_price` (lowest sell listing).
- Ours (measured, `Models/ItemPrice.cs`, `Models/PriceBasis.cs`,
  `Services/PlanSolver.cs:1061-1066` `GetUnitPrice`): field-for-field
  identical mapping, named from the opposite semantic angle. App-level
  default confirmed `PriceBasis.BuyOrder` at
  `Views/CraftingPlanView.cs:140` and `Services/CraftingPlanPipeline.cs:72/405`,
  matching gw2e's buy-order default. (`PlanSolver.Solve`'s own low-level
  default parameter is `InstantBuy`, but no production call site relies on
  it - all three real call sites pass `context.PriceBasis` explicitly.)
- Verdict: **EQUIVALENT**
- Rationale: exact field-level and default-level match, confirmed by reading
  both the model doc comments and the UI default assignment.

### 20. Per-item automatic fallback to the other TP price side when the selected side is thin/missing

*(Independently reported by both the Costing and the Valuations readers; merged here - the Valuations reader explicitly flagged the duplication risk.)*

- gw2e (**inferred/unverified**, not independently re-fetched this pass -
  cited second-hand from `gw2e-parity-spec.md` Section 2.2, which itself
  quotes `application.js`'s price-map builder `u(itemIds, mode)`): when the
  user's selected side has no price for a specific item, that one item
  silently falls back to the other side rather than being left unpriced.
  Evidence-gap note: `application.js` is the live minified app bundle, and
  this same audit elsewhere (row 58) states directly that a bundle grep
  against it was inconclusive - it is the one artifact this audit admits it
  could not grep reliably. For the sole ADOPT resting on this source, the
  claim should be treated as inferred, not measured, until it is re-quoted
  first-hand.
- Ours (measured, `Services/PlanSolver.cs:1041-1055` `GetBuyCost` and
  `Services/VendorBatchSolver.cs:241-256`, both via the shared
  `GetUnitPrice`): returns exactly one side with no fallback; treats
  `unitPrice <= 0` as fully unpriceable. No fallback exists anywhere upstream
  either (`Services/TradingPostService.cs:253-254` is a straight passthrough
  of raw sells/buys, 0 meaning "no listings").
- Verdict: **ADOPT** (size: S)
- Rationale: a real gap - under the default BuyOrder basis, any item with an
  empty buy-order book but a live sell listing (a common thin-market pattern,
  and common for the ascended/legendary-tier materials this module targets)
  renders fully unpriceable where gw2e would silently price it via the sell
  side. Fix: add a same-item other-side fallback inside `GetUnitPrice`/
  `GetBuyCost` (the single call site both TP and vendor pricing already
  share) when the primary side is 0, matching gw2e's per-item fallback
  semantics exactly. Does not touch DO-NOT-TOUCH vendor batching.

### 21. Trading Post fee math precision (listing fee + exchange tax) used in sell-side/profit metrics

- gw2e (measured, commit 25ea04a, `gw2efficiency/tradingpost-fees@master`,
  `src/index.js`): `listingFee(price) = max(round(price*0.05), 1)`,
  `tax(price) = max(round(price*0.1), 1)`, both 0 for price<=0;
  `subFees = floor(price - listingFee - tax)`.
- Ours (measured, `Services/TradingPostMath.cs`, 55 lines):
  `ListingFee`/`ExchangeFee` = `max(1, RoundHalfUp(totalValue, pct))` where
  `RoundHalfUp(v,p) = (v*p+50)/100` - algebraically identical round-half-up
  semantics, identical floor of 1, identical 0-for-non-positive guard.
- Verdict: **EQUIVALENT**
- Rationale: bit-for-bit equivalent to gw2e's own published
  tradingpost-fees package. Notably makes our sell-side economics MORE
  precise than gw2e's own live app, which uses a flat 0.85 approximation in
  its actual Cost Breakdown UI rather than this package - exactly what
  `gw2e-parity-spec.md` normative directive #3 recommended. Already the
  better of gw2e's two implementations.

### 22. Scope of TP fee application: material cost totals must never include a sell-side fee, only profit/savings metrics may

- gw2e (measured, `gw2e-parity-spec.md` Section 2.3): fee math is not in the
  raw material cost at all, only in profit convenience metrics; craft/buy
  cost totals never subtract a fee anywhere in `calculateTreePrices.ts`.
- Ours (measured): `TradingPostMath` is referenced only from
  `Services/SellSideEconomics.cs` (lines 146, 189); `Services/PlanSolver.cs`
  (all cost aggregation) never references it at all.
- Verdict: **EQUIVALENT**
- Rationale: fee math is architecturally isolated to the sell-side/profit
  module and cannot leak into cost totals even by accident, matching gw2e.

### 23. Overproduction / sellable-quantity rounding to whole multiples of a recipe's output

- gw2e (measured, `calculateTreeQuantity.ts:48-50, 68`): every node's
  demand is rounded UP to a whole multiple of its own recipe output;
  `componentAmount = ceil(usedQuantity/output)` propagates to children.
- Ours (measured, `Services/RecipeService.cs:329-357`,
  `Services/InventoryReducer.cs:112-114/266-268`,
  `Services/SellSideEconomics.cs:117-139`): same ceiling rule via
  `craftsNeeded`; when the batch overproduces past requested quantity, the
  surplus is folded into `SellableQuantity` and priced as sellable revenue
  rather than discarded.
- Verdict: **EQUIVALENT**
- Rationale: same rounding rule and the same "overproduction is real and
  sellable" consequence, computed at a different pipeline stage. Bonus: ours
  uses the fractional `ExpectedOutputCount` (row 8) rather than gw2e's plain
  integer output, an improvement already covered by prior EV-pricing work.

### 24. Unpriceable items: "no recipe + no TP price" vs "has a recipe + no TP price"

- gw2e (measured, `gw2e-parity-spec.md` Section 5.1): item with no recipe and
  no price -> unconditionally bought-with-unknown-price, never force-crafted.
  Item with a recipe and no price -> unconditionally force-crafted.
- Ours (measured, `Services/PlanSolver.cs:478-588`): `canCraft` is true
  whenever `node.Recipes` is non-empty (an unpriceable ingredient contributes
  0 rather than disqualifying its recipe). "Has recipe, no buy price" always
  force-crafts; "no recipe, no buy price" falls through to `UnknownSource`
  with no invented number.
- Verdict: **EQUIVALENT**
- Rationale: both binary outcomes match exactly, including the mechanism (a
  defined-but-unbeaten craft cost auto-wins whenever there is no buy price).

### 25. "Value Own Materials" force-buy pre-pass (zero-owned baseline, 85% margin rule)

*(Independently reported by both the Costing and the Valuations readers; merged here.)*

- gw2e (measured, `cheapestTree.ts:26-44,74-94`): computes quantity/prices
  ignoring all owned stock (zero-owned baseline), flags every item where
  `buyPrice < craftDecisionPrice * 0.85`, force-sets `craft:false` on those
  ids before the real two-pass solve runs.
- Ours (measured, `Services/OwnedMaterialsForceBuyPrePass.cs`): runs a
  throwaway `PlanSolver.Solve` against the pipeline's original unreduced
  tree with the identical `ForceBuyDiscountFactor = 0.85` constant, feeding
  the real solve's `forceBuyOnlyNodeIds`.
- Verdict: **EQUIVALENT**
- Rationale: same threshold, formula, and zero-owned-baseline requirement.
  Already implemented and closely matches gw2e; included for area
  completeness, not a new finding.

### 26. Manual per-node craft/buy/vendor override persistence and feasibility enforcement

- gw2e (measured, `gw2e-parity-spec.md` Section 1.4): a pill click directly
  mutates `component.craft`; the override persists until re-optimized.
  Feasibility is enforced client-side by never rendering an infeasible pill
  (`ng-hide` conditions) - an infeasible override is structurally impossible
  to create.
- Ours (measured, `Services/PlanSolver.cs:530-549`): overrides are checked
  first, before `PickCheapest`, so a fresh solve still honors a prior
  override - but each branch is additionally gated on the corresponding
  `canCraft`/`canBuyTp`/`canBuyVendor` flag; an override matching a now-
  infeasible source falls through silently to the normal 3-way auto-pick.
  `SolverDecision.CanCraft/CanBuyTp/CanBuyVendor` are exposed publicly for a
  UI layer to gate pill visibility the same way gw2e does.
- Verdict: **EQUIVALENT**
- Rationale: different enforcement layer (gw2e: UI never offers an
  infeasible choice; ours: solver defensively re-validates and gracefully
  degrades) but the same end guarantee - a truly infeasible source is never
  forced. Closed caveat (stitched in from row 39, which this row originally
  left as "outside this area's file set"): row 39 measures
  `Services/DecisionPillPlanner.cs:119-165` `BuildPillSpecs` reading
  `CraftingTreeNode.CanCraft`/`CanBuyTp`/`CanBuyVendor`
  (`Models/CraftingTreeNode.cs:61-63`) to decide which of up to three
  concurrent pills to render per node - confirming the display layer does
  consume the exposed `Can*` flags to gate pill visibility, the same way
  gw2e's `ng-hide` conditions do. The two rows should have been read
  together rather than shipped with one EQUIVALENT carrying a self-declared
  unverified half.

### 27. Policy-driven bulk force-buy list (gw2e's `forceBuyItems` - "Daily cooldowns = Buy" and "Mystic Forge Promotions = Disallow" default settings)

- gw2e (measured, `gw2e-parity-spec.md` Section 1.4): `cheapestTree`'s
  `forceBuyItems: Array<number>` is populated from two UI toggles resolved
  once at calculation time and unconditionally sets `craft=false` on any
  matching node regardless of whether a valid buy price exists.
- Ours: grep across `Services/*.cs` and `Models/*.cs` finds no
  DailyCooldown or MysticForgePromotion concept anywhere. The only
  "force buy" machinery is `forceBuyOnlyNodeIds` (row 25) and per-node
  manual overrides (row 26), neither of which is a caller-supplied,
  policy-driven, item-id-keyed bulk list independent of price comparison.
- Verdict: **PRESERVE** (corrected from an earlier ADOPT pass - see below)
- Rationale: fails the ADOPT bar on this audit's own decision rule - both
  toggle halves are a user-preference policy choice (which side of a
  price-independent default you want applied), not gw2e handling a case
  better than ours, and nothing in our current logic is incorrect today. It
  also directly contradicts row 61, which classifies the identical "Mystic
  Forge Promotions = Disallow" half of this same mechanism as **PRESERVE**
  because "building the toggle before the underlying data exists would be
  speculative scope creep" - that reasoning applies verbatim to this row's
  promotions half, and its cooldown half is equally not-yet-buildable: it
  depends on row 56's curated daily-cooldown id list, which does not exist
  in the codebase yet and is itself only a row 56 ADOPT proposal, not
  shipped data. Two rows must not give opposite verdicts to the same
  mechanism. Considerations entry added; at most this is a deferred
  follow-on to row 56, once that curated id list exists.

---

## Area 3: Valuations and account integration

*currency decision-pricing, item-value pipeline, own-materials reduce/value/annotate semantics*

### 28. Currency cost decision-vs-display separation

See row 15 (merged - independently confirmed by this area's reader with the
same conclusion: **EQUIVALENT**, already correctly implemented, consistent
with the currency-comparability fix in flight).

### 29. Default per-unit decision-price table for non-coin currencies

See row 16 (merged - independently confirmed by this area's reader with the
same conclusion: **PRESERVE**, constraint-blocked by the no-invented-data
hard constraint).

### 30. Core own-materials mechanic: reduce (not value, not merely annotate) quantity demand

- gw2e (measured, fetched `raw.githubusercontent.com/gw2efficiency/recipe-calculation/master/src/calculateTreeQuantity.ts`):
  walks the tree against a single shared, mutated `availableItems` map -
  `availableQuantity = min(availableItems[id], totalQuantity);
  availableItems[id] -= availableQuantity; usedQuantity = totalQuantity -
  availableQuantity`. Owned stock is consumed first-come-first-served,
  always at zero cost.
- Ours (measured, `Services/InventoryReducer.cs:78-95` `ReduceNode`): the
  identical mechanic against a shared `Dictionary<int,int> pool`, DFS-ordered
  exactly like gw2e's recursive walk.
- Verdict: **EQUIVALENT**
- Rationale: both engines REDUCE (never invent a cost for owned stock) and
  both separately ANNOTATE an opportunity-cost figure elsewhere (rows 34,
  35) rather than folding a "value" into the reduced total. Faithful match.

### 31. Sequencing: WHEN reduction happens relative to the craft-vs-buy decision

- gw2e (measured, `cheapestTree.ts:46-71` and `calculateTreeQuantity.ts:70-73`,
  fetched directly): computes prices/craft-flags FIRST (a
  price-only pass), then re-runs `calculateTreeQuantity` a SECOND time using
  those now-fixed craft flags: `ignoreAvailable = (tree.craft === false) ||
  usedQuantity === 0 || ignoreAvailable`, propagating to every descendant.
  Owned stock is only ever reserved against a branch that will actually be
  crafted.
- Ours (measured): `InventoryReducer.Reduce`/`ReduceNode` runs at
  `CraftingPlanPipeline` Step 6 (`Services/CraftingPlanPipeline.cs:198-213`),
  entirely BEFORE `PlanSolver.Solve` runs at Step 7 (line 240-245).
  `Reduce`'s signature takes no prices/vendor offers at all - it is
  structurally price-blind. `ReduceNode` unconditionally recurses into
  `node.Recipes[0]`'s ingredients with `consumeFromPool=true` whenever the
  node has any recipe, with no check for whether PlanSolver will end up
  buying this node instead.
- Verdict: **ADOPT** (size: L)
- Rationale: a genuine bug relative to gw2e's model, not a taste difference.
  Concrete failure scenario: item A has both a recipe (needing raw material
  B) and a cheaper TP buy price, so PlanSolver correctly decides to BUY A.
  The player owns some B. Because InventoryReducer runs before any pricing
  exists, it still walks A's recipe and consumes owned B against a craft
  branch PlanSolver never actually takes (`Collect()` only recurses into a
  node's Recipes when `decision.Source==Craft`, so the reservation is
  invisible downstream but B is already gone from `pool`). Confirmed
  consequences, with no compensating step found anywhere downstream
  (`OwnedMaterialsForceBuyPrePass` runs AFTER reduction and only adds nodes
  to a force-buy set - it never rolls back pool consumption): (1)
  `CraftingPlanResult.UsedMaterials` lists B as consumed even though the
  real plan never crafts anything requiring B - a phantom entry the user can
  directly observe; (2) if B is also needed by a sibling branch (or another
  root in a multi-item plan) that IS actually crafted, that branch sees less
  `pool[B]` available than it should, silently understating real savings or
  forcing an unnecessary extra TP purchase; (3)
  `SellSideEconomics.ComputeMaterialOpportunityCost` sums over the same
  `UsedMaterials` list, so `CraftingProfit` is understated by B's forgone-
  sale value even though B was never actually spent. gw2e's two-pass
  sequencing exists specifically to prevent exactly this. No repo invariant
  or DO-NOT-TOUCH item blocks fixing it, but the fix is nontrivial:
  InventoryReducer needs to become decision-aware (reuse a throwaway
  PlanSolver pass, the same pattern `OwnedMaterialsForceBuyPrePass` already
  uses, to learn per-node Source before consuming pool down a branch; or
  restructure reduction to run after/interleaved with the decision pass) -
  touches `CraftingPlanPipeline`'s step ordering, `InventoryReducer`'s two
  `Reduce` overloads, and likely the force-buy pre-pass's tree-preparation
  contract.

### 32. "Value Own Materials" force-buy pre-pass: threshold and zero-owned baseline

See row 25 (merged - independently confirmed by this area's reader with the
same conclusion: **EQUIVALENT**, already implemented).

### 33. Force-buy pre-pass gating: unconditional (gw2e) vs snapshot-conditional (ours)

- gw2e (measured): the `if (valueOwnItems)` gate runs the pre-pass whenever
  that toggle is on (default true), independent of whether `useOwnItems`
  (account/stock connection) is also on.
- Ours (measured): `useForceBuyPrePass = ownMaterialsMode ==
  OwnMaterialsMode.Valued && snapshot != null && _reducer != null`
  (`Services/CraftingPlanPipeline.cs:181-182, 617`), explicitly commented as
  narrower than gw2e's unconditional gate.
- Verdict: **EQUIVALENT**
- Rationale: verified by direct derivation, not just trusting the comment -
  the pre-pass condition `buyCost < craftCost * 0.85` can only fire when
  `buyCost < craftCost`, which is exactly the region where the ordinary
  `PickCheapest` comparison (strict `buyCost<craftCost`) already
  independently picks Buy with no help from the pre-pass. With no snapshot,
  the diagnostic pass is computed on the same zero-owned data the real solve
  would also use, so the pre-pass is provably a no-op in that case - a
  correctly-reasoned optimization, not a behavioral gap.

### 34. "Ignore" pill availability and cascade depth

*(Merged with row 43 in Area 4, "Ignore-pill visibility gating"; both readers independently surfaced the same underlying divergence from different angles. Corrected cross-reference: an earlier pass of this row pointed to row 47 instead - row 47 is "Tree-node default expansion depth," an unrelated mechanism. Row 43 is the correct match; considerations entry 7 already cited rows 34/43 correctly.)*

- gw2e (measured, `componentTree.html:221-230`): the Ignore pill's own
  `ng-show` is `useOwnItems === 'true' && showprofit == false && (...)` - it
  only renders when "Value Own Materials" is on (gw2e's own default for
  `useOwnItems` is "false"), and clicking it feeds back into
  `calculateTreeQuantity`'s full `usedQuantity`/`ignoreAvailable`
  re-derivation for the WHOLE subtree below it.
- Ours (measured, `Services/DecisionPillPlanner.cs:167-208`
  `AppendOwnershipPills`): appends an IGNORE pill unconditionally to every
  eligible node "regardless of ownership" (doc comment explicit), with no
  gate on whether an account snapshot is even loaded, and no gate on
  `OwnMaterialsMode`. `PlanSolver`'s `ignoredItemIds` handling zeroes only
  that node's own cost/quantity by item id tree-wide rather than cascading
  gw2e's full quantity re-derivation to descendants - already self-
  documented in `docs/KNOWN-ISSUES.md` #20.4 as "an explicitly recorded,
  narrower substitute."
- Verdict: **PRESERVE**
- Rationale: two divergences bundled together. The cascade-depth narrowing
  is already known and self-documented (KNOWN-ISSUES #20.4) - nothing new
  there. The always-available-regardless-of-mode-or-snapshot gating: a user
  with no account connection at all can still click Ignore and get a
  free-cost node, a strictly more general (not incorrect) affordance than
  gw2e's gated version. Corrected novelty claim: an earlier pass described
  this as "a genuinely new observation" - that overclaims. `docs/KNOWN-
  ISSUES.md`'s own DEFERRED list already records this exact question
  verbatim: "Ignore-pill cascade semantics + own-materials gating
  divergences (#20.4): revisit only on user feedback." The user has already
  been shown this divergence and has already chosen to defer it, not left
  it unraised - this row re-surfaces it with fresh supporting evidence (the
  live `ng-show` gate and gw2e's `useOwnItems` default) rather than
  discovering it. Considerations entry added as a follow-up to the existing
  #20.4 record, explicitly noting it restates an already-deferred item
  rather than new ground - consistent with this log's own preamble promise
  not to re-raise settled ground.

### 35. Own-materials cost annotation: framing and presentation

- gw2e (measured, `gw2e-parity-spec.md` Section 6.3): the Cost Breakdown
  section computes two full parallel totals (tree priced with
  `availableItems={}` vs the real tree) and derives "Cost of own
  materials"/"Cost of own currencies" as the REPLACEMENT cost (what it would
  have cost to buy the owned-satisfied components fresh).
- Ours (measured, `Services/SellSideEconomics.cs:172-193`
  `ComputeMaterialOpportunityCost`): computes the net instant-sell value
  (opportunity cost of not selling, via `TradingPostMath.NetSaleRevenue`) of
  consumed owned materials, surfaced as a single row "Own materials (sell
  value forgone)" in the Total Cost section.
- Verdict: **PRESERVE**
- Rationale: both reduce for free and separately annotate a non-total-
  affecting figure, but the economic framing differs - gw2e answers "what
  did having this in stock save you" (replacement/buy-cost basis); ours
  answers "what did using this instead of selling it cost you" (sell-side
  opportunity-cost basis), arguably the more rigorous framing for a
  "should I use or sell this" decision, but a genuinely different number a
  user comparing against gw2e would not expect to match. Taste/architecture
  difference; considerations entry added.

### 36. Default state: consume owned stock at all, by default?

- gw2e (measured): `useOwnItems = "false"` is the default - a fresh page
  load does not consume owned/bank/character stock until the user opts in.
- Ours (measured, `Views/CraftingPlanView.cs:134`): `_useOwnMaterials = true`
  is the field default - reduction is ON by default whenever a snapshot
  exists.
- Verdict: **PRESERVE**
- Rationale: product-context justified - gw2efficiency is a public website
  most visitors reach without an API key entered, so defaulting stock-
  consumption off makes sense for them; this module is a Blish HUD overlay
  only useful once account-connected, where defaulting to "use what I
  actually own" is the more helpful out-of-the-box behavior. Reasonable
  divergence; considerations entry added in case literal default-parity is
  wanted.

### 37. gw2efficiency's item-value pipeline (`/items` fields: value, valueIsVendor, crafting.buy/sell)

- gw2e (measured, fetched `api.gw2efficiency.com/items?ids=...` and
  `raw.githubusercontent.com/gw2efficiency/item-value/master/src/itemValue.js`):
  a separate `item-value` package computes a general-purpose "best guess at
  this item's worth" via an 8-step priority fallback, used for the site's
  Wealth/Account-Value pages. Confirmed this is NOT what the crafting
  calculator itself uses - the calculator's own price-map builder reads raw
  `buys[0]`/`sells[0]` directly, bypassing this pipeline entirely.
- Ours: no analogous "account wealth / item worth estimator" feature exists
  (grep for AccountValue/Wealth/BankValue: zero matches). `Models/ItemPrice.cs`
  carries only raw BuyInstant/SellInstant, matching how gw2e's OWN
  calculator prices things.
- Verdict: **EQUIVALENT**
- Rationale: out of scope on both sides in the same way - this belongs to a
  different site feature gw2efficiency's own crafting calculator does not
  consult either, and this module has no wealth-estimation feature to
  compare it against.

### 38. Per-item automatic fallback to the other TP side when the chosen price basis has no listing

See row 20 (merged - independently confirmed by this area's reader; the
reader itself flagged the duplication risk explicitly). **ADOPT** (size: S).

---

## Area 4: Tree/plan display conventions

*gw2e `componentTree.html` + `calculator.html` vs `TreeSectionController.cs` /
`DecisionPillPlanner.cs` / `PlanViewModelBuilder.cs`*

### 39. Concurrent source-pill count per node (recipe-nesting ceiling) [already-known, ARE case]

- gw2e (measured, re-fetched 2026-08-15, byte-identical to the prior
  2026-07-20/21 quote): `componentTree.html:126-144` gates at most two
  concurrent radio pills (TP xor Crafting/Merchant) per node, since gw2e's
  nesting picks exactly one recipe per output.
- Ours (measured, `Services/DecisionPillPlanner.cs:119-165`
  `BuildPillSpecs`): `CraftingTreeNode.CanCraft/CanBuyTp/CanBuyVendor`
  (`Models/CraftingTreeNode.cs:61-63`) are independently tracked, so up to
  three concurrent pills (CRAFT/TP/VENDOR) can render on one node.
- Verdict: **PRESERVE**
- Rationale: already documented (2026-08-15 ARE-case finding, ours better).
  This row reconfirms the live evidence is unchanged; not re-litigated.

### 40. Non-coin currency cost: zero contribution to gold total, separate opportunity-cost breakdown

- gw2e (measured, `componentTree.html:149-201`): the wallet-exclamation
  tooltip shows Crafting gold price / Currencies (= craftDecisionPrice -
  craftPrice) / Optimization Price, captioned "estimated opportunity cost";
  a Currency leaf shows no number, just the label "Currency".
- Ours (measured): `TreeSectionController.cs:707-733` adds a per-node
  "Unit price: N Currency" tooltip line via
  `CurrencyDisplayResolver.ResolveTreeNodeUnitAmounts` when a BuyFromVendor
  node's cost is wholly/partly non-coin; a plain Currency child renders a
  locked "CURRENCY" pill with no price, matching gw2e.
- Verdict: **EQUIVALENT**
- Rationale: behavior matches in substance; extends `gw2e-parity-spec.md`
  Section 4.4 forward to record the 2026-08-06 field-test addition as the
  parity-maintaining continuation, not a new gap.

### 41. Owned-materials per-node annotation wording

- gw2e (measured, `componentTree.html:203-211`): "Using {{ totalQuantity -
  usedQuantity }} owned materials" - a single delta number.
- Ours (measured, `Services/DecisionPillPlanner.cs:167-208`,
  `Views/Rendering/TreeSectionController.cs:1098-1113`): "HAVE {used}/{total}
  NEEDED" plus a tooltip spelling out total demand, covered count, and
  remaining count together - a 2026-08-06 field-test fix for a
  field-reported paradox where gw2e-style single-number wording read as
  contradictory next to the row's own remaining-need prefix.
- Verdict: **PRESERVE**
- Rationale: marked improvement (shows total/covered/remaining together,
  closing a real field-verified confusion gw2e's single-delta number
  invites). Considerations entry added for awareness, not because it needs
  revisiting.

### 42. Owned-currency display granularity: per-node tree pill vs. aggregate-only summary

- gw2e (measured, `componentTree.html:213-219`): "Using {{ ownedQuantity }}
  owned currency" renders directly on the Currency leaf's own row.
- Ours (measured, `Services/AccountCurrencyIndex.cs:6-19`,
  `Services/PlanViewModelBuilder.cs:228-229`): owned-currency coverage
  surfaces only in the Total Cost/summary section's currency rows;
  `TreeSectionController` never renders an owned-currency annotation on the
  Currency leaf itself.
- Verdict: **PRESERVE**
- Rationale: `AccountCurrencyIndex`'s own doc comment cites an M34-era
  report claiming gw2e "only ever nets owned currency out at the Shopping
  List/summary layer" - the live 2026-08-15 fetch contradicts that specific
  claim (a per-node pill clearly exists). Rather than treat this as a bug,
  note that per-node display risks visually implying the same wallet
  balance is independently available at every occurrence of that currency
  across the tree (a currency is pooled, not per-branch-consumable like an
  item) - our aggregate-only summary avoids that ambiguity. **Resolved**
  (audit row 56 PART B #3, 2026-08-16): the stale internal citation in
  `AccountCurrencyIndex.cs` was corrected in place (Considerations Section
  11 updated to match). No behavior change.

  Correction to this row's own original "what would resolve it" pointer:
  it named `calculateTreeQuantity.ts`'s per-node `ownedQuantity` assignment
  rule as "not fetched in this pass" and the thing that would give full
  confidence. That file has now been fetched (`raw.githubusercontent.com/
  gw2efficiency/recipe-calculation/master/src/calculateTreeQuantity.ts`)
  and it contains no per-node `ownedQuantity` assignment at all; it
  explicitly EXCLUDES Currency-type nodes from availability consumption
  altogether (`if (nesting > 0 && tree.type !== 'Currency' && !ignoreAvailable
  && availableItems[tree.id])`). This strengthens the PRESERVE - gw2e's own
  quantity-calculation engine never nets owned currency against any node,
  tree or summary - but it invalidates the original pointer: whatever
  populates `componentTree.html`'s "Using N owned currency" pill is computed
  somewhere in `application.js` (the live app bundle), not in the published
  `recipe-calculation` package, so a future confirm attempt should not aim
  back at `calculateTreeQuantity.ts` a second time - it would find the same
  answer again.

### 43. Ignore-pill visibility gating (vs. always-offered)

See row 34 in Area 3 (merged - both readers independently surfaced the same
divergence: gw2e gates the Ignore pill behind `useOwnItems`, ours shows it
unconditionally). **PRESERVE**, considerations entry added as a follow-up to
KNOWN-ISSUES #20.4.

### 44. Vendor purchase-cap indicator: inline tree-row badge vs. section-only notice

- gw2e (measured, `componentTree.html:47-87`): an hourglass icon renders on
  the item-name row of every non-root, non-coin component whenever
  `matchedItem.weekly_purchase_cap || daily_purchase_cap` is truthy, tooltip
  "This recipe has a weekly limit of N and a daily limit of M." Shown
  independent of which acquisition source is currently committed. (The exact
  population rule for `matchedItem` is inferred from context, not
  independently re-confirmed against `application.js` in this pass.)
- Ours (measured, `Models/TimegatedItem.cs`,
  `Services/VendorBatchSolver.cs:540-590` `FinalizeVendorBatches`): a
  plain-text notice ONLY inside the Crafting Steps section, and only for an
  item whose FINAL solved decision is vendor-purchase with merged
  NeededCount actually exceeding the seeded cap. A capped item the solver
  ultimately routes to TP/craft instead shows no cap information anywhere.
- Verdict: **PRESERVE** (corrected from an earlier ADOPT pass - see below)
- Rationale: no correctness or decision impact exists on either side - row 55
  (**EQUIVALENT**, re-verified this pass: `dailyCooldowns.ts` is imported by
  none of `cheapestTree`/`calculateTreeCraftFlags`/`calculateTreePrices`/
  `calculateTreeQuantity`) already establishes that purchase caps are
  informational-only in both engines. The delta here is purely WHERE and
  WHEN an advisory renders, not whether it affects the plan. Ours fires only
  when the merged `NeededCount` actually exceeds a cap on the committed
  vendor path (higher-signal, decision-relevant); gw2e badges every capped
  component unconditionally regardless of which source wins (more noise -
  a cap on an item the solver ultimately routes to TP/craft never
  constrains the plan at all). This is the same class of divergence as row
  48 (wiki-link icon), which the audit itself scored **PRESERVE** as "a UI
  convenience, not core craft-vs-buy correctness" - row 44 was scored
  inconsistently against that sibling row. Considerations entry added.
  Evidence-gap note: the earlier ADOPT pass also asserted, without a
  measured check, that an inline tree-row badge would need "no
  PlanContentHeightMath/PlanRelayoutMath change ... if implemented as a
  tooltip-only badge" - adding content to a tree row is precisely the class
  of change that perturbs row-height/relayout inputs, both of which are
  DO-NOT-TOUCH; that assurance was unverified and should not be treated as
  settled if this is ever revisited.

### 45. "Recipe has N variants" badge (compensating for gw2e's one-recipe-per-output nesting ceiling) [already-known, ARE case]

- gw2e (measured, `componentTree.html:89-120`): a shuffle icon renders when
  `component.multipleRecipeCount > 1`, tooltip "This recipe has {{N}}
  variants." gw2e's own UI-level acknowledgment that its nesting already
  collapsed multiple real recipe options down to one before render.
- Ours (measured, `Models/CraftingTreeNode.cs:61-63`,
  `Services/DecisionPillPlanner.cs:119-165`): every real option is exposed
  directly as an independently-selectable pill, not just an FYI count on
  top of one pre-picked recipe.
- Verdict: **PRESERVE**
- Rationale: already known/in-flight (the 2026-08-15 ARE case, ours better).
  This row adds corroborating live evidence that gw2e's own UI implicitly
  concedes the gap by badging it rather than resolving it - reinforces, does
  not change, the existing verdict.

### 46. Dimmed "what it would cost to craft instead" reference branch on a bought node

- gw2e (measured, `componentTree.html:250-252`): a bought node's
  subcomponent tree `ng-if` requires `component.craft` to be true; when
  false, children do not render at all. Grepped `componentTree.html` and
  `calculator.html` for a literal `.not-crafted` class: zero matches in
  either file.
- Ours (measured, `Models/CraftingTreeNode.cs:77-81` `IsReferenceBranch`,
  `Views/Rendering/TreeSectionController.cs:767-813`): always builds a
  bought node's children, dimmed and collapsed by default, so a user can
  expand to see what it would have cost to craft instead.
- Verdict: **PRESERVE**
- Rationale: a genuinely useful feature gw2e's live UI does not have at all
  - an outright addition, not a port. Note: the in-repo comment at
  `TreeSectionController.cs:767-770` attributes it to "gw2e's '.not-crafted'
  informational reference branch" - that specific provenance claim does not
  hold up against the current live template (no such gated-but-rendered
  concept, no matching CSS class, anywhere). Behavior preserved as-is.
  **Resolved** (audit row 56 PART B #3, 2026-08-16): the comment's
  provenance claim was corrected in place (module-original enhancement,
  not a gw2e port) - the considerations log entry (Section 12) is updated
  to match.

### 47. Tree-node default expansion depth

- gw2e (measured/documented, `gw2e-parity-spec.md` Section 6.2):
  `application.js`'s `tree.expanded=true` is set ONLY on the root after
  `cheapestTree` returns - every deeper level starts collapsed, one manual
  toggle at a time.
- Ours (measured, `Services/PlanContentHeightMath.cs:150-158`
  `IsNodeExpanded`): `!dimmed && depth < 2` - auto-expands both the root
  (depth 0) and its direct children (depth 1) by default.
- Verdict: **PRESERVE** (constraint-blocked)
- Rationale: a genuinely new divergence (the existing spec directive #8's
  "already how this module behaves" clause refers only to SECTION-level
  expansion, not tree-NODE expansion depth - re-verified by re-reading
  Section 6.2's own text). Whether the shallower gw2e default is preferable
  is a taste question, but `PlanContentHeightMath` is on the DO-NOT-TOUCH
  list - this cannot become an ADOPT regardless of preference. Goes to
  considerations only, and any future change would need a path that does
  not touch `PlanContentHeightMath`'s core arithmetic.

### 48. Per-item wiki-link affordance on tree rows

- gw2e (measured, `componentTree.html:38-45`): every tree row carries a
  wiki-link icon with `ng-href="{{ component.name | wikiLink }}"
  target="_blank"`, opening the GW2 wiki page for that item.
- Ours: no equivalent anywhere - grepped `Views/*.cs` and
  `Views/Rendering/*.cs` for `wiki.guildwars2.com`, `Process.Start`,
  `OpenUrl`: zero hits. The module never launches an external URL from any
  control today.
- Verdict: **PRESERVE**
- Rationale: a UI convenience, not core craft-vs-buy correctness, so it does
  not meet the ADOPT bar. Adding it would be the module's first external-
  URL-launch affordance - a small but real scope expansion worth a
  deliberate decision (feasible in Blish HUD via Process.Start,
  low risk) rather than an automatic follow-up package. Considerations entry
  added.

### 49. "No known source" labeling: plain text vs. seeded acquisition badges [already-known]

- gw2e (measured, `componentTree.html:186-192`, unchanged from the prior
  2026-07-20/21 quote): a grey, non-interactive "Not sold or crafted" pill,
  no attempt to classify why.
- Ours (measured, `Services/DecisionPillPlanner.cs:124-135`): prefers a
  seeded wiki-verified badge (e.g. SALVAGE, EXPLORE) over the plain UNKNOWN
  fallback, backed by curated `Models/AcquisitionHint.cs` hints.
- Verdict: **PRESERVE**
- Rationale: already known - `gw2e-parity-spec.md` Section 5.3/directive #6
  already records this comparison and concludes ours is strictly more
  informative. This row reconfirms the live template is unchanged; not
  re-litigated.

### 50. Section-level default expansion (Cost Breakdown / Recipe Tree / Shopping List headers) [already-known]

- gw2e (measured, `calculator.html:377-758`): every top-level section's
  `expandedSections[...]` starts true, confirmed unchanged in the 2026-08-15
  re-fetch.
- Ours (measured): `PlanSectionViewModel.IsDefaultExpanded = true` set on
  every section builder in `Services/PlanViewModelBuilder.cs`.
- Verdict: **EQUIVALENT**
- Rationale: already known (directive #8's second clause says this already
  matches); reconfirmed unchanged live. Not a new finding.

### 51. Bulk tree actions (Expand All / Collapse All / Best Path / Craft All / Buy All) [already-known/shipped]

- gw2e (measured, `calculator.html:599-607`): `expandTree()`/
  `collapseTree()`/`setTreeToBestPath()`/`setTreeToCrafting()`/
  `setTreeToBuying()`, unchanged from the prior quote.
- Ours (measured, `Views/Rendering/TreeSectionController.cs:292-448`):
  Collapse All / Expand All / Buy All / Craft All / Best Path buttons with
  equivalent semantics.
- Verdict: **EQUIVALENT**
- Rationale: already known/shipped; reconfirmed unchanged live 2026-08-15.
  Not a new finding.

### 52. Merchant/Mystic Forge pill and sublabel relabeling [already shipped, field-verified]

- gw2e (documented, `gw2e-parity-spec.md` Section 3.2): the Crafting pill
  relabels to "Merchant" when disciplines includes Merchant. No separate
  discussion found of a Mystic-Forge-specific exclusion in this pass
  (application.js's exact discipline-collection logic was not re-fetched).
- Ours: KNOWN-ISSUES.md field-test wave finding E + wave-2 fix
  (2026-08-06): Mystic Forge is excluded from Required Disciplines
  alongside the pre-existing Achievement/Merchant filter, and its sublabel
  renders "Mystic Forge" with no level number instead of the raw
  "MysticForge 0".
- Verdict: **EQUIVALENT**
- Rationale: already shipped and field-verified, consistent with gw2e's
  general "Merchant is a pill-label variant, not a real discipline" pattern
  documented elsewhere in Section 3.2. Recorded here to extend coverage to
  this post-research-date fix; the Mystic-Forge-specific sub-claim on gw2e's
  side could not be independently re-verified in this pass (evidence
  insufficient for that one detail, though the general pattern it's
  consistent with is confirmed).

---

## Area 5: Edge-case handling sweep

*account-bound/no-TP items, no-recipe-no-price items, timegated items, vendor
purchase caps/limited stock, recipe-unlock gating, precursor/legendary
mechanics, decorations/guild upgrades*

### 53. Tree-node display of an item with no TP price at all (account-bound legendary-crafting trophies, gem-store-locked, etc.)

- gw2e (measured, `gw2e-parity-spec.md` Section 5.1/5.3; fresh corroboration
  via `api.guildwars2.com/v2/items/19678` and
  `api.gw2efficiency.com/items?ids=19678`): Gift of Battle has flags
  AccountBound/NoSalvage/etc and no buy/sell price fields at all; gw2e's
  live calculator would show it as a bare, unexplained unpriceable leaf -
  "gw2efficiency does not attempt to classify why an item is unpriced."
- Ours (measured, `Services/CraftingTreeBuilder.cs:88-93`, `:174-194`
  `ApplyAcquisitionHint`): the same item id has a curated entry in
  `ref/acquisition_hints_seed.json` ("Obtained from the Gift of Battle Item
  Reward Track (WvW)... Account bound; not tradable; no recipe.", badge
  "WVW") via `Services/AcquisitionHintService.cs` - our tree explains WHY
  the item is unpriceable.
- Verdict: **PRESERVE**
- Rationale: same core algorithm (no invented price, no forced craft),
  already normative per `gw2e-parity-spec.md` Section 7 #6. Our
  `AcquisitionHintService` is a marked improvement gw2e's own source
  explicitly does not attempt. Considerations entry added. Note: this is
  the tree-node level mechanism only - the aggregate Total Cost rollup
  treatment of an account-bound root item is the separate, already-in-flight
  W4A/W4B redesign and is not re-litigated here.

### 54. Item with literally no recipe AND no price anywhere (bare leaf, no explanation available on either side)

- gw2e (measured, `gw2e-parity-spec.md` Section 5.1; fresh probe this
  session via `api.gw2efficiency.com/items?ids=103801`, Proof of Siege
  Expertise): `{price:{gems:false}, vendor_price:null, value:false}` -
  fully unpriced, renders as a bare unclassified leaf.
- Ours (measured, `Services/CraftingTreeBuilder.cs:88-93`): also
  `CraftingDecision.Unknown` - `ref/acquisition_hints_seed.json` (6 total
  entries) has none for this item id, so it also renders bare, matching
  gw2e's treatment for this specific item.
- Verdict: **EQUIVALENT**
- Rationale: both systems correctly leave a genuinely source-less item
  unclassified with no invented price and no forced craft, matching
  `gw2e-parity-spec.md` Section 7 #6 exactly. Not a new finding - this fresh,
  independently-chosen probe confirms the existing normative directive still
  holds with no drift.

### 55. Vendor daily/weekly/seasonal purchase caps: do they gate the craft/buy/vendor decision, or are they purely a post-solve notice? [already implemented]

- gw2e (measured, full re-fetch of
  `raw.githubusercontent.com/gw2efficiency/recipe-calculation/master/src/helpers/dailyCooldowns.ts`,
  byte-identical to the prior fetch): the `dailyCooldowns()` function only
  writes into a plain breakdown map, is never imported by any of
  `cheapestTree.ts`/`calculateTreeCraftFlags.ts`/`calculateTreePrices.ts`/
  `calculateTreeQuantity.ts`, and checks the purchase-cap fields
  independently of craft/buy outcome.
- Ours (measured, `Services/VendorBatchSolver.cs`
  `FinalizeVendorBatches`): builds `TimegatedItem` notices purely post-solve
  from the winning offer's caps; doc comment states caps never exclude an
  offer or change Source/TotalCost - purely informational. Shipped and
  seeded per KNOWN-ISSUES #20.2/#28/#33.
- Verdict: **EQUIVALENT**
- Rationale: already fully implemented and gw2e-parity-correct,
  independently re-confirmed against fresh upstream source
  with zero drift. No action needed.

### 56. Intrinsic recipe-level daily crafting cooldowns (e.g. Deldrimor Steel Ingot / Spool of Silk Weaving Thread-style ascended materials limited to ~1 craft per account per day, enforced server-side, no vendor offer involved)

- gw2e (measured, same `dailyCooldowns.ts` fetch as row 55): `const
  dailyCooldownIds = DAILY_COOLDOWNS.filter(x => x.craftInterval ===
  'daily').map(x => x.id)` - a separate, hardcoded static id list feeds the
  identical breakdown/notice mechanism, entirely independent of the
  purchase-cap fields checked two lines later. This list exists because
  GW2's server enforces this cooldown with no official API exposure at all
  - gw2e has to hand-curate it, the same way it hand-curates
  `currencyDecisionPrices.ts`.
- Ours: grepped the repo for CraftCooldown/DailyCraftLimit/OncePerDay/
  craft-cooldown concepts - zero hits. `VendorBatchSolver.FinalizeVendorBatches`
  only inspects steps where `step.Source == AcquisitionSource.BuyFromVendor`
  - a Craft-source step for e.g. Deldrimor Steel Ingot is never inspected
  for any cooldown, and `TimegatedCapType` has no craft-cooldown member. A
  plan needing 5x Deldrimor Steel Ingot renders a normal, unremarked Craft
  step today.
- Verdict: **ADOPT** (size: S-M)
- Rationale: a real, previously undocumented gap hitting a core real-world
  case for a crafting-planner module - ascended-material timegating is
  routine endgame GW2 crafting. Fix shape: a small wiki-curated static
  daily-cooldown item id list (mirroring the
  `acquisition_hints_seed.json`/`mystic_forge_recipes.json` precedent - no
  invented data, wiki-verified) plus a light, additive-only pre/post-pass
  keyed on Craft-source steps that reuses the existing `TimegatedItem`
  notice shape. Must not modify `VendorBatchSolver`'s own merged-ceil
  vendor-batch cost math (DO-NOT-TOUCH) - this is a parallel, independent
  code path over Craft steps, not a change to vendor batching. Related: row
  27 (policy-driven bulk force-buy list) would consume the same curated id
  list for its "Daily cooldowns = Buy" half.

### 57. Character-level and total/lifetime (non-recurring) vendor purchase caps [already-known, deferred]

- gw2e (measured): the same `dailyCooldowns.ts` mechanism (informational-
  only) conceptually extends, but the fetched function itself only reads
  daily/weekly purchase-cap fields - no character/total field referenced in
  this specific code path. `docs/research/m37-r4-vendor-caps.md` Section 2a
  independently confirmed via live wiki SMW query that "Has character
  purchase cap" and "Has total purchase cap" are real, populated wiki
  properties.
- Ours: `TimegatedCapType` enum = {Daily, Weekly, Seasonal} only.
  `docs/KNOWN-ISSUES.md`'s DEFERRED list states explicitly that character/
  total purchase caps remain deliberately unseeded - the module has no
  account/character concept at all.
- Verdict: **PRESERVE**
- Rationale: already known and already recorded as a deliberate, explained
  deferral (KNOWN-ISSUES #28 DEFERRED list) - not re-litigated. This sweep
  confirms coverage and finds no new information beyond the existing record.

### 58. Recipe-unlock ("is this recipe learned on the real account") cross-reference for the Required Recipes section

- gw2e (measured, live fetch of
  `gw2efficiency.com/views/Crafting/calculator.html`): Required Recipes
  markup is `{{ recipe.unlocked ? 'Unlocked' : 'Missing' }}` - a binary
  ternary with no third branch visible for an unknown/no-API-key state.
  `withApiKey` defaults to false and the calculator is a public website
  requiring a manually pasted API key. INFERRED (bundle grep was
  inconclusive against the ~4.2MB minified bundle) that with no key entered,
  every recipe shows "Missing," even for recipes the account may already
  know - not confirmed to the same measured standard as the markup itself.
- Ours (measured, `Services/Gw2AccountRecipeClient.cs:19-28`): calls the
  real, in-client `Gw2ApiManager` (Blish HUD's own scoped API access, no
  manual key paste) against `V2.Account.Recipes`, gated on
  `TokenPermission.Unlocks`. `Models/RequiredRecipe.cs:12` `IsMissing` is a
  genuine three-state `bool?`. `Services/RequiredRecipesVisibility.cs`
  explicitly keeps a row with an unknown status visible rather than hiding
  it. `Views/CraftingPlanView.cs:245` defaults `_hideUnlockedRecipes = true`,
  matching gw2e's own default.
- Verdict: **PRESERVE**
- Rationale: same core mechanism, but ours never requires a manual API-key
  paste and has an explicit three-state design that never falsely claims
  "Missing" when unlock status genuinely could not be determined - a marked
  improvement over what the fetched gw2e markup appears to offer.
  Considerations entry added (the inferred no-key-default claim on gw2e's
  side is worth a follow-up confirm if full certainty is wanted).

### 59. Legendary/precursor achievement-collection recipes (multi-ingredient recipes gated behind a collection achievement rather than a crafting discipline - e.g. Gift of the Catalyst, The North Wind, Ydalir, Glint's Bastion)

- gw2e (measured, `docs/research/m37-r3-achievement-dedup.md` Section
  2.3/2.4, cross-checked against the same recovered custom-recipes.json
  snapshot with no drift): 283 of 8,962 recipes carry a recipe-level
  `achievement_id`; confirmed named legendary-precursor examples include The
  North Wind (output 73037, achievement_id 2418), Ydalir (69817, 2452),
  Glint's Bastion (75482, 2621), each an ordinary 13-ingredient flat Item
  recipe.
- Ours (measured, python scan of `ref/recipes_seed.json`,
  14,736 entries): exactly ONE Achievement-discipline recipe exists in the
  entire seed - the Infinite Trebuchet Blueprint (#26's verification
  target). Zero legendary/precursor achievement-collection recipes anywhere
  in the seed; `ref/acquisition_hints_seed.json` has none for these item ids
  either. A user targeting e.g. Ydalir today gets a chain of bare UNKNOWN
  nodes for every collection-gated ingredient, with no explanation.
- Verdict: **ADOPT** (size: M)
- Rationale: the consuming mechanism already exists and is tested
  (Achievement discipline recognized as inherently-available in
  `Services/PlanResultBuilder.cs:337-347`; `AchievementBitDedupPrePass`
  already shipped per KNOWN-ISSUES #26) - only the recipe DATA is missing.
  Directly mirrors the #26 precedent: wiki/official-API-cross-verified
  curated recipe data (not gw2efficiency-sourced at runtime, satisfying the
  hard constraint), same seed-file shape already in production. Fills a
  real, high-value gap given legendary crafting is a headline use case for
  a crafting-planner module.
- Evidence-gap note: the gw2e-side measurement above establishes only that
  gw2e HAS this data (283 achievement-gated recipes, via a recovered
  `custom-recipes.json` snapshot cited in `m37-r3-achievement-dedup.md`) -
  and that snapshot is itself gw2e's research-only data, the exact boundary
  this repo's invariants forbid using as a runtime or seed source, so it
  cannot be where the curated replacement comes from. Nothing measured in
  this pass establishes that all ~283 collection-gated recipes' full
  ingredient lists can actually be re-curated from the wiki or the official
  API at that volume - the #26 precedent this row leans on covered a single
  recipe (Infinite Trebuchet Blueprint), not 283. Both the "wiki/official-
  API-cross-verified" fix shape and the size-M estimate above are therefore
  unsupported by direct evidence and should be treated as inferred, not
  measured, until a real feasibility check (e.g. a sample pull of a handful
  of these recipes' ingredient lists from the wiki) is done.

### 60. Random-output ("gambling") Mystic Forge precursor recipes (4 ingredients -> a probabilistic single output drawn from a small pool)

- gw2e (measured, `recipe-nesting/src/api.d.ts`): `API_Recipes_Entry` has a
  single `output_item_id: number` field with no probability/weight field
  anywhere in the schema. INFERRED from the schema's shape (not a positive
  statement found in prose) that gw2efficiency's own recipe model cannot
  represent probabilistic output either, structurally excluding these combos
  from both the official-API-derived and custom-recipes-derived data the
  live calculator consumes.
- Ours (measured, python scan of
  `ref/mystic_forge_recipes.json`, 1,591 entries): zero entries with
  "precursor" or "random" anywhere in the wiki-sourced `comment` field.
  `Services/MysticForgeRecipeData.cs`'s schema has no probability concept
  either.
- Verdict: **PRESERVE**
- Rationale: a mutual structural limitation, not a parity gap - neither a
  deterministic recipe-tree solver nor gw2e's own single-output schema can
  meaningfully price a gamble. No implementation action; considerations
  entry added as a documentation-only note (an explicit "gambling Mystic
  Forge recipes are out of scope" statement somewhere user-facing may be
  worth adding).

### 61. Mystic Forge Promotions setting (gw2e's `allowMysticForgePromotions`, default false, force-buys time-limited guaranteed-precursor promotional recipe ids)

- gw2e (measured, `gw2e-parity-spec.md` Section 1.4): one of exactly two
  settings that feed the solver's `forceBuyItems` list.
- Ours: grepped the repo for promotion/precursor-adjacent setting names -
  zero hits, no analog setting anywhere. Moot today since (per rows 27, 59)
  the module seeds neither precursor-collection recipes nor promotional-
  precursor recipes at all.
- Verdict: **PRESERVE**
- Rationale: recorded as a considerations-log item only, not an action -
  this setting becomes relevant only once (and if) row 59's ADOPT work also
  seeds a time-limited promotional precursor recipe; building the toggle
  before the underlying data exists would be speculative scope creep against
  the repo's scope-discipline rule.

### 62. Guild Hall decorations / permanent guild upgrades (a GuildUpgrade-typed ingredient whose "recipe" output is an account/guild-wide unlock, not a craftable item)

- gw2e (measured, `recipe-nesting/src/index.ts`): a
  `BasicGuildUpgradeComponent` type exists; `nestRecipe` resolves a
  GuildUpgrade ingredient via `recipeUpgradesMap` first, falls back to an
  external `decorationMap`, and if both fail, silently DROPS the component
  from the tree via `compact()` - the source itself carries the
  maintainer's own unresolved TODO: "Return `component` (type=
  'GuildUpgrade'), and handle that in the frontend."
- Ours (measured, python scan of `ref/recipes_seed.json`):
  ingredient `type` values present are exactly {Item, Currency} - no
  GuildUpgrade concept anywhere. Structurally, the official GW2 API's
  `/v2/recipes` endpoint cannot expose guild-upgrade "recipes" at all -
  guild-hall unlocks live under the separate, guild-permission-scoped
  `/v2/guild/:id/upgrades` surface, which this module's official-API-only
  recipe pipeline never touches.
- Verdict: **PRESERVE**
- Rationale: not a clear "gw2e is better" case - their own handling is
  self-documented as incomplete (explicit TODO, silent-drop fallback), and
  closing this gap on our side would require a genuinely new wiki-scrape
  data-sourcing tool (no analog of gw2e's unpublished decorationMap exists
  to reference) - a standalone milestone-sized decision, not an audit-driven
  ADOPT. Considerations entry added as a candidate future milestone; current
  absence does not misrepresent anything (no invalid data shown, the area is
  simply unaddressed). Related: row 5 (GuildUpgrade ingredient type
  mis-rendering) is the narrower, immediately-actionable fix that should
  land regardless of whether this broader feature is ever pursued.

---

## Coverage-gap addendum

The row below was not produced by any of the five area reads - it is a
mechanism the synthesis pass missed entirely (zero hits for
`competenc|homestead|efficiencyTier|userEfficiency` in either this matrix or
`docs/gw2e-considerations.md` before this correction), even though the task
brief's own already-known list flagged it as a divergence that "belongs in
considerations." Added here during the correction pass to close that
coverage gap. Numbered 63 (after the original 62-row sweep) rather than
inserted into the Area 2/3 sequence, to avoid renumbering rows already
cross-referenced elsewhere in this document and in
`docs/gw2e-considerations.md`.

### 63. Homestead Refinement efficiency-tier mechanism: continuous per-recipe scaling vs discrete per-tier offer rows [already implemented, M37/KNOWN-ISSUES #24 - newly added to this audit's coverage, not a new discovery]

- gw2e (measured, `raw.githubusercontent.com/gw2efficiency/recipe-calculation/master/src/cheapestTree.ts`,
  cross-checked in `docs/research/m37-r1-homestead.md` Section 1.2): `cheapestTree`
  takes a `userEfficiencyTiers` parameter defaulting to `{'102306':'0',
  '102205':'0','103049':'0'}` (Fiber/Metal/Wood Homestead Refinement
  stations, tier 0 = no upgrade). `applyEfficiencyTiersToTree()` runs before
  any pricing pass and, for a matched Homestead Refinement recipe node,
  continuously scales the single component's quantity
  (`component.quantity = component.quantity / (efficiencyTier * 2)`,
  doubling output when input drops below 1), plus three hardcoded per-item
  quirks layered on top of that general formula: onion (12142), potato
  (12135), and iron ore (19699) each get a special-cased discount at tier 1
  that the general halving formula does not produce on its own.
- Ours (measured, `Models/HomesteadEfficiencyTiers.cs`,
  `Services/CraftingPlanPipeline.cs:95/245`, `Services/PlanSolver.cs:139-142`,
  `Services/VendorBatchSolver.cs:171-220`): `HomesteadEfficiencyTiers`
  (default tier 0 for every material, matching gw2e's own default) is wired
  through the pipeline into both the solver and vendor-offer evaluation.
  Mechanically different from gw2e's continuous per-recipe scaling: our
  wiki-seeded data carries separate discrete vendor-offer rows pre-tagged
  with a `HomesteadTier`, and `VendorBatchSolver.EvaluateVendorOffers`
  simply excludes any offer whose tagged tier exceeds the user's configured
  tier for that output material - a coarser, offer-selection-based
  mechanism rather than gw2e's continuous quantity-formula scaling with
  hardcoded per-item exceptions. Prior art: `docs/research/m37-r1-homestead.md`
  (936-line research report backing the M37 implementation);
  `docs/KNOWN-ISSUES.md` #24 ("FIXED in M37").
- Verdict: **PRESERVE**
- Rationale: already implemented and already deliberate, not a gap this
  audit is surfacing for the first time - M37's own research report already
  weighed gw2e's mechanism and chose a different implementation strategy
  (discrete pre-tagged offer rows over a continuous per-recipe scaling
  formula) while preserving the same user-facing default (tier 0, matching
  gw2e) and the same absence of a master "do you own Homestead" gate
  (gw2e has none either, per `m37-r1-homestead.md` Section 1.5). This is
  exactly the kind of taste/architecture divergence this audit's own
  decision rules route to PRESERVE-plus-considerations rather than ADOPT,
  and it was already a known divergence before this audit began.
  Recorded here only because the mechanism was otherwise entirely
  absent from this matrix's coverage, which risked implying it had never
  been compared. Considerations entry added, cross-referencing the existing
  M37/`m37-r1-homestead.md`/KNOWN-ISSUES #24 record rather than re-litigating
  it.

---

## Summary

*(Updated by a correction pass after initial publication - see the inline
"corrected from an earlier pass" notes on rows 5, 17, 20, 27, 34, 42, 44, 59,
and the new row 63. Counts below reflect the corrected state.)*

- 58 distinct mechanisms compared (63 raw findings - the original 62 from the
  five-area sweep plus row 63, added during the correction pass to close a
  coverage gap the sweep missed entirely - 5 duplicate pairs merged).
- **EQUIVALENT**: 23 rows (behavior matches in substance).
- **PRESERVE**: 29 rows (taste or marked improvement; open questions in
  `docs/gw2e-considerations.md`). Rows 27 and 44 moved here from ADOPT in the
  correction pass (both failed the ADOPT bar - policy/taste preference and a
  where/when-to-render UI question respectively, neither a case of gw2e
  handling something better or a bug in ours); row 63 (Homestead Refinement
  efficiency tiers) is new.
- **ADOPT**: 6 distinct action items. Row 17 is in-flight (named in project
  tracking as the "craft/vendor comparability asymmetry fix") but its
  resolution DIRECTION was corrected out to a considerations entry rather
  than asserted, since gw2e's specific model can breach the
  no-invalid-currency-comparisons hard constraint. Row 5 becomes reachable
  once the in-flight versioned-recipe-ingestion fix lands (previously
  mis-assessed as latent against production data that doesn't reflect that
  fix). Rows 20, 31, 56, 59 are net-new. See
  `docs/gw2e-considerations.md`'s companion summary and existing project
  tracking for follow-up package scoping.
- **INVESTIGATE**: 0 rows (no row required this verdict). Evidence-standard
  caveats worth tracking on rows that keep their verdict regardless: rows 58
  and 60 carry an explicitly labeled INFERRED sub-claim; row 20's sole
  gw2e-side citation was corrected from measured to inferred/unverified
  (never independently re-fetched, and sourced from the one artifact this
  audit elsewhere admits it could not grep reliably - row 58's bundle
  caveat); row 59's "wiki/official-API-cross-verified, size M" fix framing
  was corrected to flagged-as-inferred (evidence establishes gw2e HAS the
  data, not that ~283 recipes' ingredients can be curated at that volume).
  None of these block their row's own verdict.
