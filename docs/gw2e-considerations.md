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

## 7. "Ignore" pill: always available vs gated behind an active own-materials mode

**The question**: should the Ignore pill (which zeroes a node's cost/quantity
tree-wide) be available all the time, or only when you're actively valuing
owned materials / connected to an account?

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
- Matrix reference: rows 34/43 (merged).

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
  should get a doc-only correction. Full confidence on gw2e's actual
  per-node assignment RULE (not just that the pill exists) would need
  reading `calculateTreeQuantity.ts`'s per-node `ownedQuantity` logic, which
  was not fetched in this pass.
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
- **What to do**: a small comment fix at
  `TreeSectionController.cs:767-770` to say this is a module-original
  enhancement rather than a port. No behavior change.
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

## Summary

19 entries above. Each maps to a PRESERVE (or constraint-blocked) row in
`docs/research/gw2e-convergence-matrix.md`; several PRESERVE rows in that
matrix are not repeated here because they were already fully settled/tracked
elsewhere (KNOWN-ISSUES.md, gw2e-parity-spec.md) with no open question left
for you to weigh - the matrix marks those explicitly as
"[already-known]"/"[in-flight]" so you can see the full picture without this
log re-raising settled ground.
