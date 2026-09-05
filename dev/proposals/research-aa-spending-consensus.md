# Research: Community Consensus on Wizard's Vault (Astral Acclaim) Spending for Legendary Crafters

Research date: 2026-07-22. All web sources fetched/searched on this date unless a
different publish date is noted; all `api.guildwars2.com` calls were made live on
2026-07-22 and prices are snapshots, not fixed values.

## Methodology and limitations (read first)

- Primary source for prices/tiers/caps: `wiki.guildwars2.com`, fetched both as
  rendered pages and as raw wikitext (`action=raw`) to get exact template
  parameters rather than a paraphrase.
- Community-consensus sources: general web search plus direct fetch of guide
  sites (GuildJen, Snow Crows, Medium, eathealthy365, and a cluster of SEO
  aggregator sites). **Reddit (r/Guildwars2) could not be sourced in this
  environment**: `WebSearch` rejects `reddit.com` as a domain filter ("not
  accessible to our user agent"), and `WebFetch` on both `www.reddit.com` and
  `old.reddit.com` search URLs returned "unable to fetch." The official GW2
  forums thread that surfaced in search (`en-forum.guildwars2.com/topic/158611-`)
  also 403'd on fetch. This is a real gap: the report leans on guide-site
  commentary as the community-facing evidence, not raw Reddit/forum threads.
  Treat "community consensus" below as "guide-site and wiki-talk-page
  consensus," which is a narrower claim than the research question ideally
  wanted.
- Several SEO-style guide sites (ssegold.com, boostroom.com, vortexgaming.io,
  mmokb.com) repeat near-identical talking points (Mystic Coins good, Bag of
  Gold good, "don't hoard Acclaim across reset"). They read as
  derivative/templated rather than independently reported, so they are
  counted as **one weak corroborating cluster**, not several independent
  votes, in the CONSENSUS calls below.
- Mystic Forge success-rate figures for Mystic Clover vary slightly by source
  (~31% vs ~33%); ArenaNet has never published an exact rate, so all EV math
  below uses a 31-33% range, not a single precise number.

## 1. Current Wizard's Vault stock relevant to legendary crafting (wiki-sourced)

Fetched from the live `wiki.guildwars2.com/wiki/Wizard's_Vault` page, cross-checked
against its raw wikitext (`action=raw`) to get exact `{{vendor table row}}`
parameters. [Wizard's Vault - GW2 Wiki](https://wiki.guildwars2.com/wiki/Wizard%27s_Vault)

| Item | AA cost | Seasonal cap | Notes |
|---|---|---|---|
| Mystic Coin | 9 AA each | 60/season (540 AA to cap) | Flat single-tier price; no purchase beyond the cap. |
| Mystic Clover | 60 AA each | 20/season (1,200 AA to cap) | Flat single-tier price; no purchase beyond the cap. Account-bound, **not tradable on the Trading Post** (confirmed live: `GET /v2/commerce/prices/19675` -> `"text": "no such id"`). |
| Obsidian Shard | 30 AA each | 20/season (600 AA to cap) | Flat single-tier price. Also account-bound/no-sell (confirmed via `/v2/items/19925` flags `AccountBound, NoSalvage, NoSell`). |
| Vision Crystal | 150 AA each | 4/season (600 AA to cap) | Ascended-tier trophy; tangential to weapon legendaries, more relevant to legendary armor/ascended progression. |
| Bag of Coins (1 Gold) | 8 AA each (limited tier), **then 35 AA each (unlimited tier)** | 100/season at 8 AA, unlimited after that at 35 AA | **This is the confirmed two-tier item** - see section 2. |
| Legendary Weapon Starter Key - Set 1 | 1,000 AA | Limit 1 per account, ever (not per-season) | Requires *Guild Wars 2: Visions of Eternity*. One-time unlock of a starter kit (precursor + legendary-specific gift + choice of Gift of Magic/Might) for one of several Gen-1 legendaries (Bifrost, Quip, Bolt, Meteorlogicus in Set 1). Later "Set 2..10" variants exist as new content ships, each independently capped at 1/account. [Legendary Weapon Starter Key-Set 1](https://wiki.guildwars2.com/wiki/Legendary_Weapon_Starter_Key%E2%80%94Set_1) |
| Legendary Essence of Luck | 20 AA | Cap not captured in the raw-wikitext extraction (unspecified) | Minor luck-consumable; not central to crafting. Flagged UNVERIFIED for cap. |

**Not currently in the store** (correcting an earlier assumption):
Amalgamated Gemstone does not appear in the Wizard's Vault at all, now or
historically as far as searches could establish - it is purely a Mystic Forge
product (T6 material + doubloon/crest + ectoplasm), unrelated to Astral
Acclaim. [Search results](https://www.resetera.com/threads/guild-wars-2-end-of-dragons-ot-cantha-finally.558073/page-21)
confirm only the Forge-recipe origin.

**What stays vs rotates:** Mystic Coin, Mystic Clover, Obsidian Shard, and
Bag of Coins read as permanent fixtures of the "Astral Rewards" table across
seasons (present in an August 2023 guide and still present in the July 2026
wiki snapshot, at the *same* AA prices/caps both times - see section 4,
source 2 vs the live wiki). Cosmetics and one-off unlocks (starter keys,
season-specific skins) are what actually rotates; old cosmetics move to a
"Legacy Rewards" section, reportedly at a markup - one secondary source
described a "+20%" bump, but the dedicated Legacy Rewards wiki page 404'd on
fetch, so **this specific figure is UNVERIFIED** and is not load-bearing for
anything legendary-crafting-relevant (it applies to cosmetics).

Astral Acclaim itself: earned via daily (5 AA login + up to 4x10 AA daily
objectives), weekly (up to 8x50 AA), and expansion-gated special/seasonal
objectives; **cannot be bought with gems or real money**; soft wallet cap of
1,300 AA (claims can push you over if you were under the cap when you
claimed). Season length is quarterly (~112 days / 16 weeks), and the wiki
states up to ~22,195 AA is earnable across a full 16-week cycle. [Astral
Acclaim - GW2 Wiki](https://wiki.guildwars2.com/wiki/Astral_Acclaim),
[Talk:Wizard's Vault](https://wiki.guildwars2.com/wiki/Talk:Wizard%27s_Vault)

## 2. Tiered pricing: confirmed pattern, and where it does/doesn't apply

The premise that many items have tiered pricing (N at a discount, then a
higher price) is **confirmed for Bag of Coins (1 Gold)** and
**NOT confirmed for any of Mystic Coin / Mystic Clover / Obsidian Shard /
Vision Crystal**, per the raw wikitext of the main Astral Rewards table (no
second row, no "unlimited"/"additional" language for any of those four - the
row simply disappears once the season cap is hit, i.e. a hard stop, not a
price increase).

Bag of Coins (1 Gold) itself has an explicit two-tier structure per its own
wiki page:
- Tier 1 ("limited"): 8 AA each, up to 100/season -> 800 AA caps out 100 gold.
- Tier 2 ("unlimited"): 35 AA each, no cap, available after Tier 1 is
  exhausted.
[Bag of Coins (1 Gold) - GW2 Wiki](https://wiki.guildwars2.com/wiki/Bag_of_Coins_(1_Gold))

Arithmetic (section 5) shows Tier 1 is ~4.4x more copper-efficient than Tier
2 (1,250 vs 286 copper/AA), which is a clean, well-defined case of "buy the
discounted batch, stop before the second tier" - the exact tiered pattern in
question. No 3+-source community discussion of this *specific*
item's tiering was found (it's a minor gold-conversion sink, not a
legendary-crafting headline), so the "stop at tier 1" advice here is
**UNVERIFIED as explicit community guidance** but is a direct, unambiguous
consequence of the wiki's own posted prices - CONSENSUS-by-arithmetic even
without a named source recommending it.

## 3. Live economics sanity check (api.guildwars2.com, fetched 2026-07-22)

```
GET /v2/commerce/prices/19976   (Mystic Coin)
  buys.unit_price  = 19,999 copper   (highest current buy order)
  sells.unit_price = 20,991 copper   (lowest current sell listing / "instant buy" cost)

GET /v2/commerce/prices/19721   (Glob of Ectoplasm)
  sells.unit_price = 2,680 copper

GET /v2/commerce/prices/19675   (Mystic Clover)  -> {"text": "no such id"}  (untradable, confirmed)
GET /v2/commerce/prices/19925   (Obsidian Shard) -> {"text": "no such id"}  (untradable, confirmed)
```

Mystic Coin's ~20,991c ("roughly 2 gold each") matches every guide's
back-of-envelope figure, which is a good sign the live snapshot is
representative and not an outlier.

### Copper-per-AA, headline items

| Item / tier | AA cost | Copper value avoided | Copper per AA |
|---|---|---|---|
| Mystic Coin (single) | 9 AA | 20,991c (TP instant-buy) | **~2,332 c/AA** (23s 32c) |
| Bag of Coins, Tier 1 | 8 AA | 10,000c (flat 1 gold) | **~1,250 c/AA** (12s 50c) |
| Bag of Coins, Tier 2 | 35 AA | 10,000c (flat 1 gold) | **~286 c/AA** (2s 86c) |
| Mystic Clover (via Vault) | 60 AA | see below - not directly TP-priceable | **~1,233 c/AA implied**, see caveat |

Mystic Clover has no Trading Post price (account-bound), so its "value avoided"
has to come from the cheapest *comparable* alternative acquisition route
rather than a market quote. Using the single-attempt Mystic Forge gamble
recipe (1 Obsidian Shard + 1 Mystic Coin + 1 Glob of Ectoplasm + 6
Philosopher's Stone, ~31-33% success, [Mystic Clover -
GW2 Wiki](https://wiki.guildwars2.com/wiki/Mystic_Clover)) and pricing **only
the two TP-tradable ingredients** (Mystic Coin 20,991c + Glob of Ectoplasm
2,680c = 23,671c per attempt; Obsidian Shard and Philosopher's Stone are
excluded because they are untradable and gated behind Laurels/Spirit Shards
respectively, not gold):

```
EV cost per successful clover (TP-priceable mats only)
  = 23,671c / success_rate
  = 23,671c / 0.33  ≈ 71,730c  (~7g 17s)   [optimistic end]
  = 23,671c / 0.31  ≈ 76,358c  (~7g 64s)   [conservative end]
  midpoint ≈ 74,044c (~7g 40s)

Copper-per-AA if the Vault clover displaces this route:
  74,044c / 60 AA ≈ 1,234 c/AA  (12s 34c)
```

### Does the arithmetic match the community's stated ranking? Partially, and the mismatch is informative.

On this narrow slice of arithmetic, **Mystic Coin (~2,332 c/AA) actually beats
Mystic Clover (~1,234 c/AA implied) on raw copper-per-AA** - the opposite of
most guides' stated #1-pick ordering (which puts Clovers ahead of or level
with Coins). Three things explain the gap, and all three are the *actual*
reasons guides give for prioritizing clovers, not a copper-efficiency claim:

1. **The clover EV number is incomplete.** It excludes Obsidian Shard and
   Philosopher's Stone costs (both untradable) and excludes the value of the
   "weighted random item" consolation prize on a failed attempt - real costs
   and real (partial) refunds this calculation can't price from TP data
   alone. The true EV cost is bounded but not precisely known.
2. **The clover route is high-variance; the Vault route is not.** A ~31-33%
   success rate means the *actual* number of attempts (and coins/ecto spent)
   to get N clovers has real variance around the EV midpoint - a legendary
   crafter with a fixed deadline is not indifferent between a guaranteed 60
   AA and a coin-flip-ish gamble with the same expected cost.
3. **Clovers are the actual bottleneck resource, not the cheapest one.** A
   single Gen-1/2/3 legendary weapon needs 77 Mystic Clovers
   ([gw2bltc](https://www.gw2bltc.com/en/item/19675-Mystic-Clover) via search
   snippet), and every guaranteed clover source (Vault: 20/season; Miyani:
   10/week; Fractal vendor: 10/week; Raid vendor: 15/week) is quantity-capped.
   The Vault's 20/season isn't prized because it's the cheapest 20 clovers
   available - it's prized because it's 20 *additional*, RNG-free clovers on
   top of the weekly-capped guaranteed sources, which is what actually gates
   how fast a legendary can be finished. [How to Get Mystic Clovers - Snow
   Crows](https://snowcrows.com/guides/open-world/how-to-get-mystic-clovers),
   [eathealthy365 2026 guide](https://eathealthy365.com/your-ultimate-guide-to-farming-mystic-clovers-in-2026/)

This is the key nuance for any eventual ranking feature: a pure
"copper saved per AA" metric would under-rank clovers relative to how
legendary crafters actually value them, because the real driver is
*guaranteed supply against a hard per-legendary requirement*, not marginal
cost. A ranking feature that only computes copper-per-AA (as sketched in the
addendum's "honest auto-valuation" note) would reproduce this mismatch unless
it also accounts for capped/guaranteed-supply value - which is exactly why
the addendum's revised "ranked deal table, no global rate" direction (already
on file) is the right shape: it should show clover EV-cost-avoided *and* flag
that clovers are demand-capped-by-recipe in a way coins aren't.

### Obsidian Shard: consensus is contested, arithmetic resolves it

- GuildJen (dated 2026-07-10): explicitly says **do not buy Obsidian Shards
  from the Vault**, buy them from a cheaper vendor instead.
- The 2023 Medium "Top 5" list ranks Obsidian Shards #4, worth buying.
- Wiki-sourced arithmetic settles this: the Laurel Merchant sells Obsidian
  Shard for **1 Laurel each, with no purchase limit**
  ([Obsidian Shard - GW2 Wiki](https://wiki.guildwars2.com/wiki/Obsidian_Shard)),
  versus the Vault's 30 AA each capped at 20/season. Laurels are earned
  passively (daily login + achievements) and most active accounts are not
  laurel-starved, so 30 AA for something obtainable for 1 low-friction Laurel
  is a bad trade for almost any player who isn't already laurel-poor. **This
  is CONTESTED in the guide literature but effectively resolved by the
  wiki's own alternate-price data**, and GuildJen's specific advice is the
  one the numbers support.

## 4. Community/guide-site survey (priority order, reasoning, dates)

1. **GuildJen, "The Best Items to Get from the Wizard's Vault"** - dated
   2026-07-10. Tier-1 picks for legendary crafters: Mystic Coins ("notorious
   bottleneck... sell for roughly 2 gold each"), Mystic Clovers ("vital...
   hoard cheaply before season reset"), Legendary Weapon Starter Kit ("~85%
   of the way to a Gen-1 legendary... resell for hundreds of gold even if you
   don't want it"). Explicitly excludes Obsidian Shards from the "buy here"
   list. [guildjen.com](https://guildjen.com/the-best-items-to-get-from-the-wizards-vault/)
2. **Medium, "Top 5 Best Items in Wizard's Vault"** - dated 2023-08-28 (SotO
   launch era). Ranks: (1) Mystic Clovers, (2) Mystic Coins, (3) Bag of
   Laurels, (4) Obsidian Shards, (5) Bag of Coins. Reasoning centers on
   legendary weapons being end-game gear and clovers being the hard-to-get
   bottleneck. Useful as a "typical across seasons" check: the #1/#2 picks
   (clovers, coins) match 2026 guides almost exactly, three years later, at
   the *same* AA prices - strong evidence this part of the store has not
   changed. [medium.com](https://medium.com/@jaesurmanker99/guild-wars-2-top-5-best-items-in-wizards-vault-secrets-of-the-obscure-967c50d2a5b5)
3. **eathealthy365, "Your Ultimate Guide to Farming Mystic Clovers in 2026"**
   - dated 2026-03-02. Frames the Vault as a "game-changer" *supplementary*
   guaranteed source, not the primary one: recommended order is (1) daily
   login/Vault ("essentially free"), (2) PvP/WvW vendors if you play those
   modes, (3) Fractal vendor ("near-free"), (4) Mystic Forge 10-clover
   recipe for the remainder, calling forge-crafting "almost always cheaper"
   once bonus-material returns are counted. Explicit "blended strategy"
   framing, not a single best answer. [eathealthy365.com](https://eathealthy365.com/your-ultimate-guide-to-farming-mystic-clovers-in-2026/)
4. **Snow Crows, "How to Get Mystic Clovers"** - dated 2026-02-09, updated
   2026-03-06. Presents the Vault as one "reliable but seasonally
   constrained" option among several (Forge gamble, Miyani vendor "best
   value for most players," expansion vendors "unlimited supply"), and
   explicitly declines to name one universal best method - it frames the
   choice as playstyle/resource-dependent. [snowcrows.com](https://snowcrows.com/guides/open-world/how-to-get-mystic-clovers)
5. **SEO-aggregator cluster** (ssegold.com, boostroom.com, vortexgaming.io,
   mmokb.com; no reliable independent dates, largely evergreen/generic
   copy) - all converge on the same short list: Mystic Coins, Bag of
   Gold/Coins, "spend before season resets, don't hoard AA." Treated as one
   weak corroborating vote, not four, per the methodology note above.
6. **GW2 official forum thread ("Wizards vault rewards")** and **Reddit** -
   both inaccessible in this environment (403 / fetch-blocked respectively).
   Not represented in this survey; see Methodology.

## 5. Synthesis: CONSENSUS / CONTESTED / UNVERIFIED

**CONSENSUS** (independently stated by 3+ non-derivative sources, or
directly confirmed by wiki arithmetic):
- Mystic Coins are a top-tier Vault buy for anyone doing endgame
  crafting - named favorably by GuildJen, the 2023 Medium list, and the
  entire SEO cluster, and it is the single item every source that ranks
  anything at all puts in its top 2-3. Arithmetic agrees: 2,332 c/AA is the
  best clean, fully-TP-priced rate found in this survey.
- Mystic Clovers are treated as a top-priority legendary-crafting buy by
  every source that discusses legendary crafting specifically (GuildJen,
  Medium 2023, eathealthy365, Snow Crows-as-one-of-several-guaranteed-legs).
  This holds across nearly 3 years of guides at an unchanged price/cap,
  which is reasonably strong evidence of durability, not a one-season fad.
  As shown in section 3, this consensus is really about **guaranteed,
  RNG-free supply against a hard 77-per-weapon requirement**, not raw
  copper efficiency - the arithmetic alone does not reproduce the ranking,
  but the "capped guaranteed supply is scarce and valuable" reasoning
  behind it is well supported.
- Don't let Astral Acclaim expire unspent at season rollover; it does not
  carry a good exchange rate forward and cannot be cashed out for gems/real
  money. Repeated verbatim or near-verbatim across GuildJen, the SEO
  cluster, and the wiki's own conversion note.
- Tiered items should be bought through the cheap tier and stopped before
  the expensive tier - directly demonstrated for Bag of Coins (1,250 vs 286
  c/AA, a >4x drop), even though no guide explicitly discussed this specific
  item's tiering (arithmetic-derived consensus, not source-stated).

**CONTESTED**:
- Obsidian Shards: worth buying (Medium 2023, ranked #4) vs. skip it
  (GuildJen 2026, "cheaper elsewhere"). Wiki data on the Laurel Merchant
  alternative (1 Laurel each, uncapped) sides clearly with "skip it" for
  anyone not laurel-starved, but no source directly cited that comparison -
  this report is the first place the two numbers were put side by side.
- Legendary Weapon Starter Key (1,000 AA, 1/account): favorably described
  by GuildJen and a general search-summary ("generally considered a solid
  investment"), but this is really only 2 substantive voices, both
  asserting rather than arguing the case, and neither compares it against
  *not* buying it (e.g., saving the 1,000 AA for ~111 Mystic Coins/~2.5
  gold-bags-worth of Tier-1 value instead). Labeled contested/thin rather
  than consensus.
- Whether Mystic Forge gambling or guaranteed-vendor/Vault purchases are the
  "real" best value for clovers: eathealthy365 says guaranteed sources come
  first and forge-crafting fills the remainder; other older commentary (and
  gw2bltc/gw2efficiency-style calculators, per search snippets) frame
  forge-crafting as generally the cheapest per-clover method once bonus
  material returns are counted. Both can be true simultaneously (forge is
  cheaper in expectation, Vault/vendors are safer/guaranteed), which is
  likely why no source actually contradicts another head-on - it's a
  risk-preference difference, not a factual disagreement.

**UNVERIFIED** (assertions found but not corroborated or checkable here):
- The "+20%" Legacy Rewards markup for rotated cosmetics (one paraphrased
  source, dedicated wiki subpage 404'd).
- Legendary Essence of Luck's seasonal cap (not captured in the raw wikitext
  extraction obtained).
- Any Reddit-specific consensus, since Reddit was unreachable in this
  environment - the "community consensus" in this report is guide-site
  consensus, which usually mirrors Reddit sentiment for this game but is not
  the same evidence.
- Whether gw2efficiency's or gw2bltc's live crafting calculators currently
  quote a lower per-clover EV than the ~7.2-7.6g midpoint computed above -
  both sites are JavaScript-rendered and did not return usable data through
  the fetch tool available here (only static HTML/meta content came back).

## 6. Final summary table

| Item | AA price / tier | Community verdict | Arithmetic check |
|---|---|---|---|
| Mystic Coin | 9 AA each, cap 60/season | CONSENSUS top-tier buy (GuildJen, Medium 2023, SEO cluster) | **2,332 c/AA** (TP instant-buy 20,991c/coin) - best clean, fully-priced rate in this survey; confirms the consensus. |
| Mystic Clover | 60 AA each, cap 20/season | CONSENSUS top-priority buy for legendary crafters (GuildJen, Medium 2023, eathealthy365, Snow Crows) | **~1,234 c/AA implied** (EV cost avoided via TP-priceable Forge-gamble mats only, ~31-33% success) - *lower* raw rate than Mystic Coin; consensus is driven by guaranteed-supply-against-a-77-per-weapon-cap, not copper efficiency (see section 3). |
| Obsidian Shard | 30 AA each, cap 20/season | CONTESTED (worth it per Medium 2023; skip it per GuildJen 2026) | **Arithmetic sides with "skip it"**: Laurel Merchant sells the same item for 1 uncapped Laurel vs. 30 capped AA. |
| Bag of Coins, Tier 1 | 8 AA each, cap 100/season | CONSENSUS-by-arithmetic "buy the discount tier" (no source named this item specifically, but the tiering itself is the pattern in question) | **1,250 c/AA** (flat 1 gold = 10,000c). |
| Bag of Coins, Tier 2 | 35 AA each, uncapped, after Tier 1 | CONSENSUS-by-arithmetic "stop before this tier" | **286 c/AA** - a >4x drop from Tier 1, the clearest tiered-pricing cliff found. |
| Legendary Weapon Starter Key (Set 1) | 1,000 AA, 1/account ever | CONTESTED/thin (2 favorable voices, no direct opposing case, but no rigorous cost-benefit found either) | Not computed - one-time unlock of account-bound crafting components feeding into a tradable legendary; no clean per-AA comparison exists since it's not a repeatable/marginal purchase. |
| Vision Crystal | 150 AA each, cap 4/season | UNVERIFIED (no source discussed it in a legendary-crafting context) | Not computed - tangential to weapon legendaries. |
| Amalgamated Gemstone | N/A - not in the store | N/A | Confirms the speculative inclusion of this item was incorrect; it is Mystic-Forge-only. |

## Sources

- [Wizard's Vault - GW2 Wiki](https://wiki.guildwars2.com/wiki/Wizard%27s_Vault) (rendered + raw wikitext)
- [Astral Acclaim - GW2 Wiki](https://wiki.guildwars2.com/wiki/Astral_Acclaim)
- [Mystic Clover - GW2 Wiki](https://wiki.guildwars2.com/wiki/Mystic_Clover)
- [Mystic Coin - GW2 Wiki](https://wiki.guildwars2.com/wiki/Mystic_Coin)
- [Obsidian Shard - GW2 Wiki](https://wiki.guildwars2.com/wiki/Obsidian_Shard)
- [Philosopher's Stone - GW2 Wiki](https://wiki.guildwars2.com/wiki/Philosopher%27s_Stone)
- [Bag of Coins (1 Gold) - GW2 Wiki](https://wiki.guildwars2.com/wiki/Bag_of_Coins_(1_Gold))
- [Legendary Weapon Starter Key-Set 1 - GW2 Wiki](https://wiki.guildwars2.com/wiki/Legendary_Weapon_Starter_Key%E2%80%94Set_1)
- [Talk:Wizard's Vault - GW2 Wiki](https://wiki.guildwars2.com/wiki/Talk:Wizard%27s_Vault)
- [GuildJen - The Best Items to Get from the Wizard's Vault](https://guildjen.com/the-best-items-to-get-from-the-wizards-vault/) (2026-07-10)
- [Medium - Top 5 Best Items in Wizard's Vault](https://medium.com/@jaesurmanker99/guild-wars-2-top-5-best-items-in-wizards-vault-secrets-of-the-obscure-967c50d2a5b5) (2023-08-28)
- [eathealthy365 - Your Ultimate Guide to Farming Mystic Clovers in 2026](https://eathealthy365.com/your-ultimate-guide-to-farming-mystic-clovers-in-2026/) (2026-03-02)
- [Snow Crows - How to Get Mystic Clovers](https://snowcrows.com/guides/open-world/how-to-get-mystic-clovers) (2026-02-09, updated 2026-03-06)
- [gw2bltc - Mystic Clover item page](https://www.gw2bltc.com/en/item/19675-Mystic-Clover) (search-snippet only; page did not render crafting-cost data via fetch)
- `api.guildwars2.com/v2/commerce/prices/{19976,19721,19675,19925}` and `v2/items/{19675,19976,19721,19925}` - live, fetched 2026-07-22
- Attempted but inaccessible: `en-forum.guildwars2.com` (403), `reddit.com` / `old.reddit.com` (fetch blocked; domain rejected by WebSearch filter), `gw2efficiency.com` crafting calculator (JS-rendered, no usable static content)
