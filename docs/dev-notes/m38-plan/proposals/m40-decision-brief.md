# M40 Decision Brief - what to build next

One-pager distilled from the 2026-07-22 proposal set (d3/d4/d5 + synthesis + AA addendum),
corrected for everything that has shipped since those docs were written. Sources remain the
authority on design detail; this is the comparison layer.

## The three candidates

| | (a) Plan History tab | (b) Crafting Ranker tab | (c) Do-Next Tier 1 |
|---|---|---|---|
| What v1 ships | Auto-captured history of plan REQUESTS + generation-time summary (never full results). View (frozen numbers, dated) + Reuse (fresh re-solve inline). Pin/delete/dedup/retention cap. | Persisted ordered watchlist (list order IS priority, up/down buttons); closeness % per item = 1 - remaining/from-scratch coin cost, via two real solves per item; explicit Refresh only. | NextActionClassifier bucketing plan steps vs live snapshot: buy-TP-now / buy-vendor-now / craft-now / blocked / time-gated - with the binding-gate rule from your ecto directive. |
| Size (docs' own rating) | L; "solid M" if sparkline capture + V2 cross-tab handoff are cut | L; toward "solid M" with polish cuts | M -> M/L (binding-gate + cap-aware route splitting are real solver-adjacent arithmetic) |
| Depends on | Nothing unbuilt. Shares only a model shape with the Ranker - separate stores. | Nothing unbuilt. Establishes the shared RankerStore. | The Ranker's watchlist/store - synthesis: "D4 IS the priority-list foundation D5 needs." |
| Biggest risk | NodeId drift means per-node overrides are NOT replayed in v1 (only ignores, item-id keyed). Honest but a UX limit. | v1 solves watchlist items independently - shared materials double-counted (disclosed in UI caption, not solved). 2N-solve refresh cost unmeasured on deep legendaries. | Trust: "a wrong 'go do X' is worse than no advice." v1 mitigates by NEVER surfacing caps on TP-liquid items (the ecto rule) - conservative by design. |
| Your stated conviction | "Needs thinking thru a bit more" (lowest) | The detailed two-fold ask (highest) | Flagship "last gravy" - wanted the honest feasibility answer |

## What changed since the docs were written (all VERIFIED shipped)

- **Phase 1 "substrate" is DONE.** The synthesis wanted log-core + Settings/About + snapshot
  search built first (store conventions, onError shape, diagnostics toggle). All shipped in M39.
- **Every sequencing gate the docs worried about has cleared.** D5 wanted Wave C
  (WP-11/12/13/15) settled; D3/D4 wanted WP-21/22 coin rendering landed. M38 is fully merged.
- **Plan History's V2 (cross-tab reopen) is less blocked than written.** The doc deferred it
  behind "WP-04/21/23/24/25/26 actively carving up a 4812-line file" - that carving is done
  (CraftingPlanView is 2804 lines, tree interaction isolated in TreeSectionController).
  V2 is still new surface, but no longer racing a refactor.
- **The Ranker's refresh cost got cheaper.** WP-07 memoization cut warm re-solve cost 51.4%
  (measured); the doc's 2N-solve concern was anchored to pre-M38 numbers. The doc's ask to
  measure one deep legendary before shipping still stands.
- **The AA/Wizard's Vault instance of the binding-gate framework has its data.** The addendum
  listed P1 (AA valuation row) / P2 (seasonal-cap ingestion) / P3 (WV rotation) as unbuilt
  prerequisites - all three shipped (M39 settings + the M38-era AA data/notice PRs; the
  seasonal notice branch exists in the timegate wording). Do-Next's hardest concrete test
  case (Mystic Clover via WV, a genuine no-TP gate) is now modelable.

## Recommendation

**M40 = Ranker foundation (D4), M41 = Do-Next Tier 1 (D5) on top of it, M42 = Plan History V1.**

Reasoning: it follows the synthesis's own sequencing AND your conviction ordering; the Ranker
fills one of the two placeholder tabs you flagged as blocking any release thought; and it is
the only option that unlocks another (D5 shares its store). Plan History last also buys time
for its V2 (the genuinely wanted form, now unblocked) to be scoped into v1 rather than
retrofitted. The docs' flagged alternative - Plan History first as a "lower-risk warm-up" -
made sense before M38 proved the store+view pattern six times; a warm-up is no longer needed.

If you want one milestone, not three: D4 alone is self-contained and useful (watchlist +
closeness + priority order); D5 can wait indefinitely without wasting anything.

## Kickoff questions per option (the docs' open items needing your call)

- **D4 Ranker:** confirm coin-denominated closeness metric (docs reject blended currency
  valuation as an invalid-comparison risk); duplicate-add = update quantity in place?;
  sequential refresh acceptable?; watchlist size cap wanted?; store filename data/ranker.json
  (synthesis's resolution of a d4/d5 naming collision).
- **D5 Do-Next:** sign off the conservative v1 gate rule (TP-liquid caps NEVER surfaced -
  suppresses the misleading ecto warning, also forgoes rare real savings on thin-book items);
  AcquisitionHint must render beside any surfaced gate (ships with v1, not after).
- **D3 Plan History:** V1 (inline read-only reuse) vs V2 (reopen into the interactive plan
  tab) as the v1 bar; retention default (docs guess 50, no anchor); auto-capture every
  generate vs explicit save.

Implementation note carried from all three docs: new stores follow the StatusStore /
VendorOfferStore atomic tmp+replace pattern - SnapshotStore.Save is NOT atomic despite an
older scout note claiming otherwise.
