# Addendum: Astral Acclaim / Wizard's Vault awareness (maintainer question, 2026-07-22)

## What the module knows today (MEASURED against master)

1. Seed data: ref/vendor_offers.json contains 1,682 "Wizard's Vault" merchant offers
   (plus "Wizard's Gobbler/historical"-style seasonal variants). Mystic Clover (19675)
   has 17 vendor offers total, including the Wizard's Vault route at 60x currency 63.
2. Currency identity: Gw2Constants maps 63 -> "Astral Acclaim" (offline fallback);
   live /v2/currencies provides name/icon; the account wallet snapshot captures ALL
   wallet currencies, so owned Astral Acclaim IS captured and owned/needed
   annotations would render for it.
3. Solver reality: the route is economically INERT today, by two stacked designs:
   a) PlanSolver's invalid-currency-comparison invariant: offers with any UNVALUED
      currency line are not coin-comparable and survive only as a last-resort
      fallback when nothing priceable exists. Clover has a priceable EV craft
      route, so the WV offer never wins.
   b) The Settings valuation UI's CuratedCurrencyIds is a hardcoded list
      {2 Karma, 3 Laurels, 23 Spirit Shards, 78/79/80 Rift Essences} - Astral
      Acclaim (63) is NOT listed, so a user cannot value it at all. The only lever
      that could activate the route is unreachable.
4. Caps: TimegatedCapType = {Daily, Weekly} only. Wizard's Vault limits are
   SEASONAL; the wiki exposes "Has seasonal purchase cap" as a real SMW property
   (M37 r4 research), deliberately scoped out in M37 (no consuming model). The
   seed's WV clover row carries no cap fields at all.
5. Rotation/staleness: WV stock rotates per season; the scrape accumulates
   "/historical" pages. The M37 stale sweep did not specially handle WV rotation.

## Design-doc status

d4 (ranker) and d5 (next-step) encode the general binding-gate framework but do
not name Astral Acclaim. The framework fits WV exactly: Mystic Clover is a no-TP
item, so under the v1 conservative rule a WV route with a modeled cap would
surface as a GENUINE gate - the archetype of use-or-lose value for legendary
crafters. Three prerequisites make it real:

- P1 (S): add 63 to CuratedCurrencyIds so users can value Astral Acclaim.
  Wrinkle: AA is untradable and earned via capped play; its "copper value" is
  inherently subjective. Note the honest auto-suggestion idea below.
- P2 (M): seasonal-cap ingestion end to end - scrape "Has seasonal purchase cap"
  (updater printout + WikiVendorResult + ConvertToOffer + hasher), add
  TimegatedCapType.Seasonal, thread through the warn-only notice. The r4 report
  already located the data; M37's out-of-scope note is the anchor to revisit.
- P3 (S/M): WV rotation handling in the stale sweep (treat "/historical" WV
  merchants distinctly; verify current-season stock at refresh time).

## Honest auto-valuation idea (design note, not commitment)

AA's opportunity cost is computable WITHOUT inventing data: several WV stock
items are TP-tradable (e.g. Mystic Coins at a fixed AA price), so an implied
copper-per-AA rate = best (TP sell value / AA cost) across current-season
tradable stock. Surfacing that as a SUGGESTED (user-confirmable, never silent)
valuation would ground the WV route in live prices while keeping the
no-invented-data invariant: the rate derives from real TP quotes, and the user
opts in. Interacts with binding test (b): AA spent on clovers competes with AA
spent on coins/gold - the implied rate IS that competition, priced.

## REVISED (maintainer critique, 2026-07-22): no single-anchor valuation

The auto-valuation idea above is WITHDRAWN as originally framed. Maintainer
critique: a single implied copper/AA rate from one item assumes AA is equally
valuable across the store, which is false - deal quality varies per item, and
tiered pricing (limited quantity at a discount, higher price tier after)
makes the implied rate a supply curve, not a point. Deal-weighing is
partly subjective.

Refined direction - RANKED DEAL TABLE / BUDGET ALLOCATION, no global rate:
the module never needs a global AA exchange rate to be useful. For the
legendary-relevant subset of current-season WV stock it can compute a
per-item copper-equivalent-per-AA figure from live data only:
- TP-tradable stock (Mystic Coins etc.): TP sell value / AA cost, per tier
  (each price tier is its own row; "first N at X AA" and "thereafter at Y AA"
  rank separately).
- Untradable-but-plan-needed stock (Mystic Clovers): the alternative-route
  cost the solver already computes (clover craft EV at live prices) / AA
  cost - i.e. copper SAVED per AA versus the route you would otherwise take.
Then AA becomes a BUDGET to allocate down a sorted list, not a currency to
value: "your N AA returns most as: clovers (tier 1) > coins (tier 1) >
coins (tier 2) > ...". This matches how the community actually reasons
("best-value vault purchases"), keeps every number live-data-derived,
respects tiers natively, and leaves genuinely subjective uses (cosmetics)
outside the table for user judgment. The next-step engine's binding tests
then treat AA-capped routes via allocation rank, not exchange rate.

Data prerequisite: per-tier quantities/prices for WV offers - check whether
the wiki vendor tables encode the tiers as separate rows (likely, given the
1,682 scraped offers) and whether seasonal caps distinguish the discount
tier; fold into P2/P3.

## Research outcome (2026-07-22, full cited report: research-aa-spending-consensus.md)

Guide-site consensus exists (Reddit/forums unreachable from this environment -
flagged honestly in the report): Mystic Coins and Mystic Clovers are the
unanimous legendary-crafter priorities; spend AA before season reset. KEY
NUANCE: raw copper-per-AA UNDER-RANKS clovers (~1,234 c/AA implied via the
forge-EV alternative) vs coins (~2,332 c/AA live), yet the community puts
clovers first - the driver is GUARANTEED CAPPED SUPPLY against a hard
77-per-weapon requirement (every RNG-free clover source is quantity-capped),
not copper efficiency. Design consequence: the deal table needs TWO signals,
not one - (a) copper-per-AA arithmetic, (b) a structural scarcity flag
("no-TP item whose guaranteed sources are ALL quantity-capped" - derivable
from our own seed data + tradability, no opinion needed). Default sort for
legendary crafters: scarcity-flagged hard-requirement items first, then by
copper-per-AA; arithmetic always shown. Tiering reality-check: only Bag of
Coins is genuinely tiered (8 AA x100 then 35 AA uncapped, a >4x cliff);
the legendary mats are hard seasonal caps with no continuation tier.
Contested items (Obsidian Shards - arithmetic settles it against, 1 uncapped
Laurel beats 30 AA; the 1,000-AA starter key - thin evidence) belong in the
table with their numbers, unranked by default. Correction absorbed:
Amalgamated Gemstones were never Vault stock.

## Open questions

- Does gw2efficiency model the Wizard's Vault as a clover source in its
  calculator? (Unresearched; parity-relevant if we pursue this.)
- WV weekly AA income modeling (for Tier-2 projection of "seasons to afford"):
  earnable AA per season is boundable from wiki data but rotates; defer with P2.
