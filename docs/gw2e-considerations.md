# gw2efficiency Convergence: Considerations Log

Date: 2026-08-15

This is a living document, not a to-do list. Every entry below is a place
where GW2CraftingHelper does something differently from gw2efficiency (gw2e)
on purpose, or where matching gw2e exactly is blocked by one of this repo's
hard rules, and where reasonable people could land in different places. None
of these are bugs - the full list of concrete fixes (things gw2e does better
and we should change) lives in
`docs/research/gw2e-convergence-matrix.md` and gets implemented separately.

Work through these at your own pace. Each entry explains: the question, what
gw2e does, what we do, why the difference exists (taste, or a case where ours
is arguably better), and what it would take to change if you ever want to.
Source references point into `docs/research/gw2e-convergence-matrix.md` (the
full evidence trail lives there) by matrix row number.

*Updated by a correction pass after initial publication: entries 20-23 are
new, entries 7 and 11 had wording corrected in place. See the Summary at the
bottom for what changed and why.*

---

## 1. Should the module ship a curated default currency-value table? [constraint-blocked]

**The question**: gw2e ships a hardcoded table of ~46 currencies (Karma=1,
Spirit Shard=3600, Badge of Honor=23, etc, all in copper-equivalent terms) so
that currencies immediately affect craft-vs-buy decisions with zero setup.
GW2CraftingHelper ships with none - every currency is worth nothing to the
solver until you type a value in yourself.

- **gw2e**: `static/currencyDecisionPrices.ts`, a 46-entry table applied
  automatically whenever you haven't overridden a currency. These are the
  maintainers' own unlabeled "what is this worth for optimization purposes"
  guesses, not real Trading Post prices - most of the currencies listed
  aren't even tradable.
- **Ours**: `CurrencyValuation.None` is the default; the code's own comment
  says the solver "never invents one (repo invariant): only currencies the
  user explicitly priced here are usable for cost comparison." The Settings
  tab's currency list (Karma, Laurels, Spirit Shards, Rift Essences, Astral
  Acclaim) ships every row blank on purpose.
- **Why this is a real tradeoff and not a bug**: porting gw2e's table would
  mean inventing numbers this repo's own rules forbid ("Do not invent data
  when APIs are missing"). That rule exists for good reason - gw2e's numbers
  are opinions, not facts. But the practical cost is real too: a fresh
  install gets zero currency-aware craft/buy comparisons until you manually
  configure something, which most users won't do on day one.
- **What adopting gw2e's approach would look like**: not a straight port
  (that would violate the no-invented-data rule as written). A middle path
  would be an independently wiki/community-sourced starter table, clearly
  labeled as an estimate you can override or clear, shipped opt-out rather
  than opt-in. That's a product decision about how far "no invented data"
  should stretch, not a mechanical change.
- Matrix reference: row 16 (merged with row 29).

---

## 2. Cycle protection: our general algorithm vs gw2e's hardcoded item list

**The question**: nothing to decide here, this is recorded for awareness -
you may want to know how much better our approach already is.

- **gw2e**: mostly detects only direct self-reference cycles generically; for
  one real n-hop cycle it could not handle cleanly (Condensed Ley-Line
  Essence, item ids 91224/91137/91222/91171), it hardcodes those four ids and
  flattens ALL pairwise references among them, including two ids (91222,
  91171) that aren't actually part of the cycle.
- **Ours**: a per-build ancestor-path check (`HashSet<int> visiting`) that
  detects cycles of any depth without needing a curated list. Verified by
  hand-tracing against the exact same real GW2 items gw2e had to hardcode: our
  approach only intervenes on the genuine cycle edge, and lets 91222/91171's
  own non-cyclic references fully expand where gw2e's blanket 4-item patch
  would flatten them regardless.
- **Why preserve**: this is a marked improvement, not taste. Nothing to
  change; noted so you're aware the comparison was made and came out clearly
  in our favor.
- Matrix reference: row 2.

---

## 3. Mystic Clover-style probabilistic output: one fractional number vs two named fields

**The question**: awareness only, no action needed.

- **gw2e**: encodes both "true yield on a successful craft" and
  "probability-weighted average yield" in a single fractional
  `output_item_count` field (0.31 for Mystic Clover). A reader of gw2e's raw
  data cannot tell from that number alone that a successful craft actually
  produces exactly 1 clover.
- **Ours**: two distinct fields, `OutputItemCount` (the true per-success
  yield, an int) and `ExpectedOutputCount` (the probability-adjusted average,
  a nullable double). More expressive, avoids the ambiguity gw2e's single
  number carries.
- **Why preserve**: marked improvement. Noted for awareness.
- Matrix reference: row 8.

---

## 4. Should a TP buy-price pill reflect craft-batch overproduction?

**The question**: if a recipe crafts in batches of, say, 5, and you only need
3, should the *buy* price shown for that item reflect 3 (what you'd actually
pay on the Trading Post) or 5 (rounded up to match what a craft would
produce)?

- **gw2e**: rounds a node's own quantity up to a whole multiple of its recipe
  output BEFORE computing either the craft or the buy price, so the TP
  buy-price pill for a craftable-but-bought item reflects the inflated
  quantity, not the raw amount you asked for.
- **Ours**: bills the buy side for exactly the raw amount needed. The craft
  side does correctly account for the whole batch internally, but we never
  surface anywhere (no field, no tooltip) that choosing Craft would actually
  yield more than you asked for.
- **Why this is a real tradeoff**: our buy-side number is arguably more
  economically honest (you'd never buy 10 on the Trading Post just because a
  hypothetical craft path would yield a batch of 10). But we lose the
  visibility gw2e gives you into "craft yields more than you need" - a user
  might not realize picking Craft here nets them a surplus.
- **What adopting gw2e's approach would look like**: either match gw2e's
  buy-price inflation (probably not worth it, ours is more correct there),
  or - the more appealing option - surface the existing `CraftsNeeded`/
  output-count data that's already computed but never displayed, as a small
  "this yields N, you need M" note on craftable nodes. That's a display-only
  addition, not a costing change.
- Matrix reference: row 9.

---

## 5. Recipe metadata gw2e's own pipeline structurally cannot pass through

**The question**: awareness only.

- **gw2e**: its `transformRecipe()` nesting step does not copy the raw
  API's `flags` array (which includes things like AutoLearned) onto its
  internal recipe representation at all - the data is silently dropped and
  unavailable to anything downstream.
- **Ours**: carries `Flags` through the whole pipeline and uses it - the
  Required Recipes section's "auto-learned" distinction is driven by it.
- **Why preserve**: marked improvement, not a gap to close. Noted for
  awareness.
- Matrix reference: row 10.

---

## 6. Vendor-vs-craft-vs-buy: our explicit three-way comparison vs gw2e's "vendor is just a recipe"

**The question**: gw2e never has a genuine three-way choice - a vendor
purchase is modeled as an ordinary recipe (with a Merchant discipline) that
competes against other recipes first, and only the single winner then
competes against the TP buy price. We instead run an explicit three-way
buy/craft/vendor comparison with our own tie-break rule (vendor wins an exact
craft/vendor tie). Is our tie-break rule the right one?

- **gw2e**: no equivalent rule exists to compare against - it never faces a
  genuine three-way tie because vendor and craft compete against each other
  first, before buy is even considered.
- **Ours**: `PlanSolver.PickCheapest` - "when both craft and vendor beat buy,
  the numerically cheaper of the two wins; an exact craft/vendor tie keeps
  vendor."
- **Why this is worth logging rather than treating as settled**: our separate
  VendorOffer architecture (distinct from the recipe list, with its own
  purchase-cap and batch-merging model) is what makes several DO-NOT-TOUCH
  features possible - merged-ceil vendor batching, purchase-cap notices, and
  the in-flight vendor cost-component leaves work. Folding vendor into the
  recipe list the way gw2e does would conflict with those. So the
  architecture stays; the open question is narrowly whether "vendor wins
  ties" is the tie-break you'd actually want, since it was never validated
  against an external reference.
- **What changing it would look like**: a one-line change to the tie-break
  comparison in `PickCheapest`, if you decide craft (or an explicit
  user-configurable preference) should win ties instead.
- Matrix reference: row 18.

---

## 7. "Ignore" pill: always available vs gated behind an active own-materials mode [already deferred - #20.4]

**The question**: should the Ignore pill (which zeroes a node's cost/quantity
tree-wide) be available all the time, or only when you're actively valuing
owned materials / connected to an account? You've already seen this question
once and deferred it (`docs/KNOWN-ISSUES.md`'s DEFERRED list: "Ignore-pill
cascade semantics + own-materials gating divergences (#20.4): revisit only on
user feedback") - this entry restates it with fresher supporting evidence, it
is not a new discovery. An earlier pass of this log described the gating half
below as "a genuinely new observation," which overclaimed; corrected here.

- **gw2e**: the Ignore pill only renders when "Value Own Materials" is
  toggled on (and that toggle itself defaults off) - a user who hasn't
  connected an account or turned that setting on never sees it.
- **Ours**: the Ignore pill shows on every eligible node "regardless of
  ownership" (the code's own comment), with no gate on account connection or
  on `OwnMaterialsMode` at all.
- **Why this is a real tradeoff**: ours is strictly more flexible - a user
  with no account connection can still say "I don't care about this
  ingredient's cost, pretend I have it." gw2e's gating is more guided (it
  only offers the affordance when it's contextually meaningful) but also
  more restrictive.
- **Related, already-tracked separately**: this sits alongside a related,
  already-recorded divergence in `docs/KNOWN-ISSUES.md` #20.4 - when you
  click Ignore, gw2e cascades a full quantity re-derivation to every
  descendant of that node, while we only zero that specific node's own
  cost/quantity by item id. That part is already documented there as a
  deliberate, narrower substitute and doesn't need a fresh decision here.
- **What matching gw2e would look like**: gate `AppendOwnershipPills`'s
  Ignore pill on `OwnMaterialsMode != Free` (and optionally on an active
  snapshot), a small conditional in `DecisionPillPlanner.cs`.
- Matrix reference: rows 34/43 (merged; row 34's own header previously
  mis-cited its merge partner as row 47 - corrected to row 43).

---

## 8. Own-materials cost annotation: replacement cost vs sell-side opportunity cost

**The question**: when you use owned materials instead of buying them fresh,
what number should the plan show you as the "value" of having had them on
hand?

- **gw2e**: computes a full parallel tree priced as if you owned nothing, and
  shows the difference as "Cost of own materials" / "Cost of own
  currencies" - i.e. what it would have cost to buy those components fresh
  (a replacement-cost framing).
- **Ours**: computes the net instant-sell value of the consumed owned
  materials (via `TradingPostMath.NetSaleRevenue`) and shows it as a single
  "Own materials (sell value forgone)" row - an opportunity-cost framing
  (what did using this instead of selling it cost you).
- **Why this is a real tradeoff**: these are genuinely different questions
  with genuinely different numbers. Replacement cost answers "how much did
  owning this save you against buying it." Opportunity cost answers "how
  much did you give up by not selling it instead." Ours is arguably the more
  rigorous framing for a "should I use or sell this" decision, but it also
  means a user comparing our numbers against gw2e's site should not expect
  them to match, and might be confused if they don't know why.
- **What adopting gw2e's framing would look like**: not a replacement - more
  likely an additional row showing the replacement-cost figure alongside the
  existing opportunity-cost one, since both answer legitimately different
  questions a user might want.
- Matrix reference: row 35.

---

## 9. Own materials: consume by default, or require opt-in?

**The question**: should the plan use your owned materials by default, or
should you have to turn that on explicitly like gw2e requires?

- **gw2e**: `useOwnItems` defaults to false - a fresh page load ignores your
  bank/character inventory until you opt in.
- **Ours**: `_useOwnMaterials` defaults to true whenever an account snapshot
  exists.
- **Why this is a reasonable divergence, not a gap**: gw2efficiency is a
  public website most visitors reach without ever entering an API key, so
  defaulting stock-consumption off makes sense there. This module is a Blish
  HUD overlay that only does anything useful once you're account-connected -
  defaulting to "use what I actually own" is the more helpful out-of-the-box
  behavior for this product specifically.
- **What matching gw2e's default would look like**: flip the field default
  in `Views/CraftingPlanView.cs`, a one-line change, if literal default-parity
  with gw2e ever matters more than the out-of-the-box convenience.
- Matrix reference: row 36.

---

## 10. Owned-materials tree annotation wording: single delta vs total/covered/remaining

**The question**: awareness only - this was already changed based on a real
field-test finding, not a fresh question.

- **gw2e**: "Using {{ totalQuantity - usedQuantity }} owned materials" - a
  single delta number, with the row's own quantity label separately showing
  only what's still needed.
- **Ours**: "HAVE {used}/{total} NEEDED" plus a tooltip spelling out total
  demand, covered count, and remaining count together - a fix (2026-08-06
  field-test finding) for a real reported case where gw2e-style single-number
  wording read as contradictory next to the row's own remaining-need prefix.
- **Why preserve**: marked improvement, already shipped in response to real
  user confusion. Noted for awareness, not because it needs revisiting.
- Matrix reference: row 41.

---

## 11. Owned-currency display: per-node pill vs aggregate-only summary

**The question**: should owned-currency coverage show on every tree node
where that currency appears, or only once in the summary?

- **gw2e**: shows "Using {{ ownedQuantity }} owned currency" directly on each
  Currency leaf's own row, independently at every occurrence in the tree.
- **Ours**: surfaces owned-currency coverage only in the Total Cost/summary
  section's currency rows - never per-node in the tree.
- **Why this is worth a second look**: a currency is a single pooled wallet
  balance, not a per-branch-consumable resource like an item. Showing it on
  every occurrence in the tree risks visually implying the same balance is
  independently available at each spot, which isn't true. Our aggregate-only
  approach avoids that specific ambiguity, which is why this is recorded as
  PRESERVE rather than ADOPT - but see the note below before treating that as
  fully settled.
- **A loose end worth flagging**: the code comment in `AccountCurrencyIndex.cs`
  cites an older internal report claiming gw2e "only ever nets owned
  currency out at the summary layer" - a live fetch this session shows that
  claim is not accurate (gw2e does have a per-node pill). That comment
  should get a doc-only correction.
- **Corrected follow-up pointer**: this entry previously said full confidence
  on gw2e's actual per-node assignment rule would need reading
  `calculateTreeQuantity.ts`'s per-node `ownedQuantity` logic, "not fetched
  in this pass." That file has since been fetched, and it settles the
  opposite of what the pointer implied: it assigns no per-node
  `ownedQuantity` at all, and it explicitly excludes Currency-type nodes
  from availability consumption entirely. Whatever populates the tree pill's
  number is computed in gw2e's live `application.js` bundle, not in the
  published `recipe-calculation` package - a future confirm attempt should
  aim there, not back at `calculateTreeQuantity.ts`.
- Matrix reference: row 42.

---

## 12. The dimmed "what it would cost to craft instead" branch: ours is an original feature, not a gw2e port

**The question**: no decision needed - this is a documentation correction.

- **What we have**: when a node is bought rather than crafted, we still
  build and show its children, dimmed and collapsed, so you can expand to
  see what crafting would have cost. gw2e's live UI has no equivalent at
  all - a bought node's children simply don't render.
- **The loose end**: the code comment describing this feature attributes it
  to "gw2e's '.not-crafted' informational reference branch" - that specific
  provenance claim doesn't hold up. No such concept, and no matching CSS
  class, exists anywhere in gw2e's current live templates. This looks like
  either a stale claim or a reference to something gw2e has since removed.
- **Resolved** (audit row 56 PART B #3, 2026-08-16): the comment at
  `TreeSectionController.cs` (now near line 895) was corrected in place to
  say this is a module-original enhancement rather than a gw2e port. No
  behavior change.
- Matrix reference: row 46.

---

## 13. Tree-node default expansion depth: root-only vs root-plus-children [constraint-blocked]

**The question**: how much of the tree should be expanded when a plan first
renders?

- **gw2e**: only the root node auto-expands; everything below starts
  collapsed, one manual click at a time.
- **Ours**: both the root and its direct children auto-expand
  (`depth < 2` in `PlanContentHeightMath.IsNodeExpanded`).
- **Why this is genuinely open**: this is a taste call, not a correctness
  question, and I don't have a strong argument either way. But
  `PlanContentHeightMath` is on the DO-NOT-TOUCH list for this repo, so even
  if you decide gw2e's shallower default is preferable, it cannot be
  auto-adopted - any change here needs a path that doesn't touch that file's
  core arithmetic.
- Matrix reference: row 47.

---

## 14. Per-item wiki-link affordance on tree rows

**The question**: should each tree row link out to the GW2 wiki for that
item, the way gw2e's does?

- **gw2e**: every tree row carries a wiki-link icon that opens the item's
  wiki page in a new tab.
- **Ours**: no equivalent - the module has never launched an external URL
  from any control.
- **Why this needs a deliberate decision rather than an automatic add**: it's
  a real convenience but a genuine first for this module (its first
  external-URL-launch affordance). Technically simple in Blish HUD (a
  `Process.Start` call), low risk, but worth you deciding on purpose rather
  than it sneaking in as a side effect of this audit.
- Matrix reference: row 48.

---

## 15. Tree-node display of account-bound/unpriceable items: our acquisition hints go beyond what gw2e attempts

**The question**: awareness only.

- **gw2e**: for an item with neither a recipe nor a TP price (e.g. an
  account-bound legendary-crafting trophy like Gift of Battle), shows a bare
  "Not sold or crafted" pill with no explanation of why.
- **Ours**: the same item, when covered by our curated
  `ref/acquisition_hints_seed.json`, shows a specific wiki-verified badge and
  explanation (e.g. "Obtained from the Gift of Battle Item Reward Track
  (WvW)... Account bound; not tradable; no recipe."). Coverage is real but
  narrow (6 entries today) - most unpriceable items still render bare on
  both sides.
- **Why preserve**: marked improvement where hint coverage exists; gw2e's own
  source explicitly doesn't attempt this. Noted for awareness. (Growing hint
  coverage further is a separate, ongoing content task, not a design
  question.)
- Matrix reference: row 53.

---

## 16. Recipe-unlock cross-reference: no-manual-key design plus a genuine three-state field

**The question**: awareness only.

- **gw2e**: Required Recipes shows a binary "Unlocked"/"Missing," and (as a
  public website) requires you to manually paste a GW2 API key before
  `recipe.unlocked` can reflect your real account at all.
- **Ours**: uses Blish HUD's own in-client, scoped API access directly - no
  manual key paste - and `IsMissing` is a genuine three-state field (`bool?`)
  so a row whose status genuinely couldn't be determined stays visibly
  "unknown" rather than being mislabeled "Missing."
- **Why preserve**: marked improvement on both counts. Noted for awareness.
  One loose end: whether gw2e's no-key default truly shows "Missing" for
  every recipe (rather than some other unknown-state handling) was inferred,
  not independently confirmed against the live minified bundle - worth a
  follow-up fetch only if you want full certainty on that specific detail.
- Matrix reference: row 58.

---

## 17. Gambling Mystic Forge recipes: out of scope on both sides

**The question**: not really a question - a documentation suggestion.

- **Both engines**: a deterministic recipe-tree solver (ours) and gw2e's own
  single-output recipe schema are structurally unable to represent a
  probabilistic multi-outcome Mystic Forge combine (4 ingredients in, one of
  several possible precursors out). Neither side has any data or mechanism
  for this.
- **Suggestion**: consider adding an explicit, user-facing note somewhere
  that "gambling" Mystic Forge recipes (as opposed to deterministic ones,
  which are supported) are intentionally out of scope, so a user doesn't
  wonder why a precursor gamble never shows up in a plan.
- Matrix reference: row 60.

---

## 18. Mystic Forge Promotions setting: contingent on future precursor-recipe data

**The question**: not actionable yet - recorded so it isn't forgotten.

- **gw2e**: has a "Mystic Forge Promotions = Disallow" toggle (default on)
  that force-buys promotional precursor items rather than letting the solver
  treat a temporary promotion as always-craftable.
- **Ours**: no equivalent setting, and none is needed yet - we don't seed any
  promotional precursor recipe data at all (see the related ADOPT item for
  legendary/precursor achievement-collection recipes in the matrix).
- **When this becomes relevant**: only if/when that recipe data gets added.
  Building the toggle before the underlying data exists would be building
  ahead of need.
- Matrix reference: row 61.

---

## 19. Guild Hall decorations / permanent guild upgrades: a genuine future-milestone candidate

**The question**: is full Guild Hall decoration crafting support (recipes
whose "output" is an account/guild-wide unlock rather than a craftable item)
worth a future milestone?

- **gw2e**: attempts to resolve these via an internal, unpublished
  `decorationMap`, but its own source carries an explicit unresolved TODO for
  when that resolution fails, and silently drops the component from the tree
  in that case. Not a clean success story to copy wholesale.
- **Ours**: no concept of GuildUpgrade-typed ingredients exists at all. This
  is structurally harder for us than it is for gw2e - the official GW2 API's
  `/v2/recipes` endpoint doesn't expose guild-upgrade recipes; that data
  lives under the separate, guild-permission-scoped
  `/v2/guild/:id/upgrades` surface, which this module's official-API-only
  recipe pipeline doesn't touch today.
- **Why this is a real open question rather than a clear-cut adopt**: gw2e's
  own handling is incomplete by its own admission, so there's no clean target
  to port. Closing this gap properly would need a new wiki-scrape
  data-sourcing tool (there's no published equivalent of gw2e's
  `decorationMap` to lean on) - a standalone milestone-sized decision.
  Meanwhile, the narrower and more urgent half of this problem - making sure
  a GuildUpgrade-typed ingredient never mis-renders as a generic "Currency"
  pill if one shows up in seed data - is already an ADOPT item in the matrix
  (row 5) and doesn't need to wait on this larger decision.
- **What full support would look like**: a new data-sourcing effort (likely
  wiki-based, matching the `acquisition_hints_seed.json`/
  `mystic_forge_recipes.json` precedent of curated, verifiable, non-invented
  data) plus recipe/decoration resolution logic in `RecipeService`/
  `CraftingTreeBuilder` mirroring gw2e's `recipeUpgradesMap`/`decorationsMap`
  approach, but with a cleaner "drop cleanly if unresolvable" fallback than
  gw2e's own admittedly-incomplete handling.
- Matrix reference: row 62 (related: row 5).

---

## 20. Policy-driven bulk force-buy list: "Daily cooldowns = Buy" and "Mystic Forge Promotions = Disallow"

**The question**: gw2e ships two settings that unconditionally force a
matching item to Buy regardless of price comparison - one for items with an
intrinsic daily crafting cooldown, one for time-limited promotional
precursor recipes. Do you want either as a settings toggle?

- **gw2e**: `cheapestTree`'s `forceBuyItems` list is populated from these two
  UI toggles, resolved once per calculation and applied independent of
  whether a valid buy price even exists.
- **Ours**: no equivalent concept - grepped `Services/*.cs` and
  `Models/*.cs` for DailyCooldown/MysticForgePromotion, zero hits. The only
  force-buy machinery that exists (`forceBuyOnlyNodeIds`, entry 25's
  pre-pass; per-node manual overrides) is price-comparison-driven, not a
  caller-supplied policy list.
- **Why this landed here and not as an automatic fix**: this was initially
  scored as an ADOPT ("a genuine feature gap"), which does not survive
  scrutiny against this log's own decision rule - both toggle halves are a
  user-preference policy choice (which side of a price-independent default
  you want applied), not gw2e handling a case better than ours, and nothing
  in our current logic is wrong today. It also would have directly
  contradicted entry 18 below (Mystic Forge Promotions), which correctly
  treats the identical promotions toggle as not-yet-buildable because the
  underlying recipe data doesn't exist yet - the same reasoning applies to
  this row's promotions half, and its cooldown half depends on data that is
  itself only a proposal (the matrix's row 56, "intrinsic recipe-level daily
  crafting cooldowns" - a curated id list that doesn't exist in the codebase
  yet).
- **What building this would look like, if you want it**: at most a deferred
  follow-on to row 56 once its curated daily-cooldown id list exists - the
  cooldown half of this toggle would consume that same list; the promotions
  half additionally needs the legendary/precursor recipe data entry 18
  below is waiting on.
- Matrix reference: row 27 (related: rows 56, 61/entry 18).

---

## 21. Vendor purchase-cap indicator: inline tree-row badge vs. Crafting-Steps-only notice

**The question**: should a vendor purchase cap show up as a badge on the tree
row itself (so you see exposure before committing to a source), or only in
the Crafting Steps section once the plan has already been solved and a
vendor path has won?

- **gw2e**: an hourglass icon renders on every non-root, non-coin component's
  row whenever its matched vendor data carries a daily or weekly purchase
  cap - shown unconditionally, independent of which acquisition source
  ultimately wins for that node.
- **Ours**: a plain-text notice appears only inside the Crafting Steps
  section, and only for an item whose FINAL solved decision is
  vendor-purchase with merged demand actually exceeding the seeded cap. A
  capped item the solver routes to TP or craft instead shows no cap
  information anywhere.
- **Why this is a real tradeoff, not a clear-cut adopt**: matrix row 55
  already establishes purchase caps are informational-only in both engines
  (already implemented on our side, per KNOWN-ISSUES #20.2/#28/#33) -
  neither side lets a cap change the craft/buy/vendor decision. So the only
  thing in question is where and when
  an advisory renders, not whether it affects your plan. Ours fires only
  when a cap actually binds on the path you're committed to (higher signal);
  gw2e badges every capped component regardless of which source wins (more
  noise - a cap on an item routed to TP/craft never constrains anything).
  This was initially scored ADOPT, inconsistently with how this same log
  treats entry 14 below (the wiki-link icon), a UI-convenience row scored
  PRESERVE on the same "not core craft-vs-buy correctness" reasoning that
  applies here too.
- **A build note if you do want it**: an earlier pass asserted that an
  inline tooltip-only badge would need "no PlanContentHeightMath/
  PlanRelayoutMath change" - that assurance was never actually checked.
  Adding content to a tree row is exactly the class of change that perturbs
  row-height/relayout inputs, and both of those are on this repo's
  DO-NOT-TOUCH list. Verify that concretely before assuming a tooltip-only
  badge is free.
- **What matching gw2e would look like**: an inline tree-row badge/tooltip
  (display-only, reusing already-seeded `VendorOffer.DailyCap`/`WeeklyCap`
  joined by item id in `TreeSectionController`, no solver change) - but see
  the build note above before assuming that's actually height-neutral.
- Matrix reference: row 44.

---

## 22. Vendor offer priced entirely in an unvalued currency: which side should give way? [hard-constraint conflict]

**The question**: our craft-ingredient handling and our vendor-offer handling
disagree about what an unvalued (un-priced) non-coin currency should do to
comparability. Craft-side, an unvalued currency ingredient contributes zero
and the recipe still competes normally. Vendor-side, an offer with even one
unvalued currency line gets pulled out of the normal comparison entirely and
only competes against other similarly-disqualified offers. That asymmetry is
real and a fix is already in flight - but which direction should the fix
resolve it?

- **gw2e's model** (uniform treatment: an unvalued currency contributes zero,
  the offer/recipe competes normally either way) is NOT safe to port as-is.
  Porting it would let a vendor offer priced 100% in an unvalued currency
  (say, 500 Karma with no valuation configured) evaluate to 0 copper and win
  outright against every priced coin/craft alternative - a zero-cost plan
  for something that actually costs real currency. That breaches this
  repo's own "no invalid currency comparisons" invariant.
- **Why the craft-side precedent doesn't settle it**: on the craft side, an
  unvalued currency is one ingredient among others that ARE priced, so the
  understatement is bounded by the rest of the recipe's real cost. A vendor
  offer can be entirely currency, with nothing bounding the understatement -
  the two situations aren't equivalent even though they look symmetric on
  the surface.
- **The other direction is equally available**: instead of loosening the
  vendor side to match the craft side's leniency, the craft side could be
  tightened to match the vendor side's caution - make an unvalued currency
  ingredient disqualify its recipe from comparison too, the same way an
  unvalued vendor offer is disqualified today. Both directions restore
  symmetry; only one of them risks a zero-cost plan.
- **What to decide**: whether comparability should lean permissive (gw2e's
  way, currency-blind and hoping the amounts are usually small enough not to
  matter) or leans conservative (disqualify anything with an un-priceable
  cost component, on either side, until the user prices it) - a genuine
  product/risk call, not a mechanical fix.
- Matrix reference: row 17 (the asymmetry finding itself is an ADOPT action
  item in the matrix; this entry is only the contested resolution
  direction).

---

## 23. Homestead Refinement efficiency tiers: continuous formula vs discrete tagged offers [already implemented - awareness only]

**The question**: no fresh decision needed - this entry exists because the
mechanism was missing from this log's coverage entirely, not because
anything about it is unresolved. Recorded for completeness.

- **gw2e**: `cheapestTree`'s `userEfficiencyTiers` parameter feeds
  `applyEfficiencyTiersToTree()`, which continuously scales a matched
  Homestead Refinement recipe's component quantity by formula
  (halving input per tier, doubling output once input drops below 1),
  layered with three hardcoded per-item quirks (onion, potato, iron ore)
  that the general formula doesn't produce on its own.
- **Ours**: `HomesteadEfficiencyTiers` (default tier 0 for every material,
  matching gw2e's own default) is wired through the pipeline into both the
  solver and vendor-offer evaluation, but mechanically differently -
  wiki-seeded data carries discrete vendor-offer rows pre-tagged with a
  tier, and any offer above the user's configured tier is simply excluded
  from consideration, rather than continuously scaling a formula.
- **Why this is settled, not open**: this was already researched and
  deliberately implemented in M37 (`docs/research/m37-r1-homestead.md`,
  936 lines; `docs/KNOWN-ISSUES.md` #24, "FIXED in M37") - a different
  implementation strategy chosen on purpose while preserving gw2e's
  user-facing default (tier 0) and its absence of a master "do you own
  Homestead" gate (gw2e has none either). Nothing here needs your attention
  unless you want to revisit that M37 decision on its own merits.
- Matrix reference: row 63.

---

## Summary

23 entries above (19 from the original pass, plus 20-23 added in a
correction pass). Each maps to a PRESERVE (or constraint-blocked) row in
`docs/research/gw2e-convergence-matrix.md`; several PRESERVE rows in that
matrix are not repeated here because they were already fully settled/tracked
elsewhere (KNOWN-ISSUES.md, gw2e-parity-spec.md) with no open question left
for you to weigh - the matrix marks those explicitly as
"[already-known]"/"[in-flight]" so you can see the full picture without this
log re-raising settled ground.

**Correction pass notes**: entries 20 and 21 are new because matrix rows 27
and 44 moved here from ADOPT (both failed the ADOPT bar on review - policy
preference and a where/when-to-render question, not gw2e handling something
better or a bug in ours). Entry 22 is new because matrix row 17's asymmetry
finding stays an ADOPT action item, but its previously-asserted resolution
direction turned out to conflict with the "no invalid currency comparisons"
hard constraint and was moved here instead, per this log's own routing rule
for constraint conflicts. Entry 23 is new because the underlying mechanism
(Homestead Refinement efficiency tiers) was absent from both this log and
the matrix entirely, despite already being implemented and documented
elsewhere (M37/KNOWN-ISSUES #24) - added to close that coverage gap, not
because anything about it is unresolved. Entries 7 and 11 had wording
corrected in place (a novelty overclaim and an invalidated follow-up
pointer, respectively) without changing their underlying verdicts.

---

## Orchestrator checksum verdicts and decisions (2026-08-16)

1. ADOPT-row checksum outcomes: Row 5 GuildUpgrade CONFIRMED by the
   orchestrator's own live API fetch (recipe 12002 returns a
   GuildUpgrade-typed ingredient) and repo reads - queued for
   implementation. Row 17 resolved by the shipped comparability fix
   (conservative direction; entry 22 ratified conservative by the
   maintainer). Row 20/38 TP other-side fallback CONFIRMED twice
   first-hand (research agent + the orchestrator's own fetch of
   gw2efficiency's live application.js: preferred side first, same-item
   cross-side fallback when missing/zero, unpriced only when both sides
   empty) - queued. Row 31 InventoryReducer price-blind ordering
   CONFIRMED (Reduce before Solve in CraftingPlanPipeline) - queued with
   a design round; the design will also deliver a "Value own materials"
   toggle matching gw2e's valueOwnItems (default true) semantics, per
   the maintainer. Row 56 craft-cooldowns gap CONFIRMED - queued. Row 59
   achievement-gated recipes DEMOTED: three of the audit's four named
   examples (The North Wind 73037, Ydalir 69817, Glint's Bastion 75482)
   are achievement REWARDS auto-granted on collection completion, not
   recipes; the ~283 count is unverifiable from primary sources;
   Zap-class LearnedFromItem precursor recipes are ordinary /v2/recipes
   members already covered by the seed regeneration; the genuine
   residue is Mystic-Forge-only legendary FINAL assemblies (e.g. Bolt
   30699), re-scoped as a possible expansion of the existing
   mystic_forge_recipes.json curated seed (wiki {{Recipe}} templates are
   machine-parseable) - awaiting an explicit maintainer go.

2. Maintainer decisions on this log's entries (2026-08-15/16
   walkthrough): #1 ADOPT gw2e's currency decision-value table as
   shipped defaults (decision-only, overridable and clearable, labeled
   estimates with attribution; the no-invented-data rule is explicitly
   waived for this table by the maintainer); a derived-from-real-data
   valuation engine is backlogged. #4 keep exact-amount buy billing;
   surface craft-batch excess and its net reclaim value in the planned
   Crafting Plan Notes section. #6 keep vendor-wins-ties (vendor
   guarantees exact quantity at fixed price; craft-then-sell reclaim
   carries market risk and effort). #8 keep the opportunity-cost
   display; gw2e has no display-framing option (their Value Own
   Materials toggle is decision-behavior, not display - see verdict on
   row 31 above). #11 REPLACED by the maintainer's own design: currency
   leaves get a plan-scope "HAVE {have}/{planTotal} TOTAL" pill (both
   numbers plan-level facts, truthful at every occurrence, no per-row
   allocation), collapsing to plain HAVE at full coverage - supersedes
   both gw2e's per-node pill and our aggregate-only status quo. #13 keep
   root-plus-children expansion. #14 ADOPT a per-row wiki link
   (placement at the implementer's discretion first pass). #19
   guild-decoration support backlogged. #20 ratified demoted (cooldown
   toggle is a follow-on once row 56's data exists). #21 ratified
   preserve, with cap info folded into the planned price-detail
   tooltip. #22 ratified conservative. New maintainer-requested
   convergence item: duplicate gw2e's crafting-pill value-detail
   tooltip (rows: crafting gold price / currencies estimated
   opportunity cost with explanatory note / optimization price; shown
   only when the two diverge) - spec extracted verbatim from their
   componentTree.html template.
