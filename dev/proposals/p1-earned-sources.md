# P1 - Earned acquisition as a first-class source

Status: DESIGN PROPOSAL. No production code, `ref/` data, test or workflow
was changed to produce it. Written 2026-08-29 against branch `w4-p1scope`
at commit `60a7e19`.

This is the scoping document for **P1** of
[`legendary-gap-analysis.md`](legendary-gap-analysis.md) - the proposal that
document ranks first: *"Make non-transactional acquisition a first-class
source."* The gap analysis states the bar it has to clear:

> The bar is deliberately low to begin with: **even a typed source with a
> sentence beats `UnknownSource` plus a curated hint table with ten rows in
> it.** It does not need to cost these routes. It needs to stop pretending
> it does not know what they are.

**A note on the two source documents.** `legendary-gap-analysis.md` and
`module-capability-audit.md` are not on `master` and are not on this branch;
both live on `legendary-research-docs` (commits `30e6ec2` and `7716c2e`).
Their repo-relative citations above resolve only against that branch today.
This matters for merge order, not for the design: nothing proposed here
loads or reads those files. Every claim about *current module behaviour*
below is re-verified against the working tree and cited to
`file:line` here, not quoted from either document.

Claims are tagged **MEASURED** (read directly out of a file cited by path
and line, or - where marked - queried live against
`api.guildwars2.com`), **INFERRED** (reasoned from something measured, not
itself printed anywhere) or **PROPOSED** (a design decision, with no code
behind it). Where a decision has real alternatives, they are listed with a
recommendation and the reason.

---

## 0. The scope line, in one paragraph

The module is a project planner WITH a price optimizer, and the goal is to
help with all aspects of crafting.
Under the previous framing an unpriceable route was out of scope by
definition. It is now in scope. P1 does not make earned routes cheaper,
faster or costed - **it makes them nameable, and gives them a home**. The
unit of work is: the solver commits a typed `Earned` decision instead of
`UnknownSource`; a new **Prerequisites** section lists what the player has
to go and do; and the plan total stops silently presenting itself as
complete when part of the project is effort rather than gold.

---

## 1. What the module does today

MEASURED, all of it.

### 1.1 The vocabulary

`Models/AcquisitionSource.cs:26-33` is the entire solver vocabulary:
`BuyFromTp`, `Craft`, `Currency`, `BuyFromVendor`, `UnknownSource`.
`Models/CraftingDecision.cs:23-33` is the display twin and adds `Have`,
`GuildUpgrade`, `UnrecognizedIngredient`. The single bridge between them is
`Services/CraftingTreeBuilder.cs:536` (`MapSource`), whose `default` arm
**throws** so a new source member without a display arm fails loudly.

### 1.2 Where an earned item lands

`Services/PlanSolver.cs:1294` is the terminal fallback:

```
return Commit(AcquisitionSource.UnknownSource, null, null, 0, null);
```

reached only when there is no TP price, no comparable or fallback recipe,
and no vendor offer of either tier. It commits `TotalCost = null`,
`ComparisonValue = null`, and - because `Commit`'s `hasUnvaluedCurrency`
parameter defaults to `false` (`Services/PlanSolver.cs:1139-1147`) -
`HasUnvaluedCurrency = false`.

There is a second `UnknownSource` commit at `Services/PlanSolver.cs:903-908`,
the "Ignore"-d item branch, which commits `TotalCost = 0L`. It is a
different thing entirely (the user asserted they hold the item) and nothing
in this proposal touches it.

### 1.3 The fallback data

`ref/acquisition_hints_seed.json` has **10 rows** (MEASURED by reading the
file). Each carries `itemId`, `hint` (a sentence), `badge` (a short label),
`sourceUrl` and `lastVerified`. The runtime model is
`Models/AcquisitionHint.cs:10-25`; the loader is
`Services/AcquisitionHintService.cs:43-73`, which degrades to an empty
dictionary on any malformed input and never throws. The file is packaged
because it lives in `ref/` (`TaimisToolbench.csproj:568`) and loaded once at
startup (`Module.cs:527-531`).

Applied at `Services/CraftingTreeBuilder.cs:486-509`, **guarded on
`Decision == Unknown`** so a hint never bleeds onto a node with a real
priced source.

### 1.4 What the user actually sees today

More than "an UNKNOWN pill", which matters for sizing this work:

- **Tree pill.** `Services/DecisionPillPlanner.cs:243-260`: when no source
  is feasible, the pill text is the seeded badge (`SALVAGE`, `EXPLORE`,
  `WVW`) when one exists and does not collide with a source badge
  (`IsReservedSourceBadgeText`, `Services/DecisionPillPlanner.cs:99-105`),
  and `"UNKNOWN"` otherwise. So 10 items already render a specific pill.
- **Shopping List badge.** `Services/ShoppingSourceBadge.cs:33-46` makes the
  identical choice; `:81-85` puts the hint sentence in the badge tooltip,
  falling back to *"No known acquisition source - check the item's wiki
  page"*.
- **Row type.** `Services/PlanViewModelBuilder.cs:1439-1447`
  (`MapShoppingRowType`) has a permissive `default: ShoppingUnknown` - the
  opposite posture to `MapSource`'s throw.

**INFERRED consequence for scoping:** the *rendering* surface for a named
earned route mostly exists at the node level. What does not exist is (a) a
**type** - the badge is an uncurated free string, the decision is `Unknown`,
and no consumer can ask "is this node earned rather than merely
unrecognised?" - and (b) any **whole-plan view** of the earned work.

### 1.5 The three behaviours that decide the design

These are the load-bearing measurements. Everything in sections 3 and 4
follows from them.

**(a) An unpriceable ingredient contributes zero and does NOT demote its
parent recipe.** `Services/PlanSolver.cs:1498-1509`:

```
long? ingredientCost = Evaluate(ingredient, ctx);
craftCost += ingredientCost ?? 0L;
var ingredientDecision = ctx.Memo[ingredient.NodeId];
craftRealCost += ingredientDecision.TotalCost ?? 0L;
...
if (ingredientDecision.HasUnvaluedCurrency) { hasUnvaluedCurrency = true; }
```

Since the terminal `UnknownSource` commit leaves `HasUnvaluedCurrency`
false, an item with no source at all is counted at 0 and its parent recipe
stays **comparable-tier**. This is deliberate and documented as gw2e parity
at `Services/PlanSolver.cs:1395-1399` ("gw2e's `craftPrice =
sum(component.craftResultPrice || 0)`... Coin totals are then deliberately
partial").

**(b) But an unvalued CURRENCY, or any non-`Item` ingredient, does demote
it.** `Services/PlanSolver.cs:1480-1496` sets `hasUnvaluedCurrency = true`
for both. So the module is **stricter about a currency it cannot value than
about an item it cannot source at all.** That asymmetry is the centre of
gravity of section 3.2.

**(c) The "this total is a floor" disclosure only fires on an exactly-zero
total.** `Services/PlanViewModelBuilder.cs:240`:

```
bool unpricedZero = result.Plan.TotalCoinCost == 0 && HasUnpricedNode(result);
```

`HasUnpricedNode` walks for `Decision == CraftingDecision.Unknown`
(`Services/PlanViewModelBuilder.cs:418`). The footnote it gates
(`:209-210`) reads *"Some items in this plan have no recipe and no Trading
Post price, so they count as 0 here. These totals are a floor, not a
measured cost."*

**INFERRED:** for a legendary this never fires. The plan's coin total is
thousands of gold, so `TotalCoinCost == 0` is false, and the caveat is
suppressed for exactly the plan that most needs it. The module already owns
the right sentence and shows it in the one case where it matters least.

### 1.6 The Gift of Battle exclusion, read rather than assumed

The gap analysis and the capability audit both describe
`ref/vendor_offer_exclusions.json` as deleting Gift of Battle's vendor row
*because the earned route is inexpressible*. **MEASURED: that is not what
the file says.** Its `reason` field is:

> "The Battle Master's 500 Badges of Honor sale was removed in the Spring
> 2016 Quarterly Update; Gift of Battle has since come only from the WvW
> Gift of Battle Item Reward Track. Shipping this row would price a
> legendary component from a vendor path that has not existed for a decade"

The exclusion is a claim that **the wiki is wrong about the game**, hand
verified 2026-08-25 with a source URL, and the file's own header comment
says each entry is exactly that. It is correct on its own merits and stays
correct after P1 ships. See 6.1 - this is the one place where the premise
does not survive contact with the file.

---

## 2. The model

### 2.1 What the API can and cannot tell us

**MEASURED by live query against `api.guildwars2.com`** (queried live
2026-08-29; not recalled). This unevenness is the single
most important input to the model, so it comes before the types.

| Tier | What the API gives | Endpoints | Example |
|---|---|---|---|
| **1. Live progress** | Per-step progress toward the route itself | `/v2/account/achievements` (`current`, `max`, `done`, `repeated`, **`bits`**), `/v2/achievements` (declares `rewards`), `/v2/account/raids`, `/v2/account/wizardsvault/{daily,weekly,special,listings}`, `/v2/account/legendaryarmory`, `/v2/account/masteries`, `/v2/account/dungeons`, `/worldbosses`, `/mapchests`, `/dailycrafting` | Achievement 2258 "Mistward Legguards" declares `rewards: [{type: Item, id: 70598, count: 1}]` and has 7 bits. "4 of 7 lanterns" is expressible end to end. |
| **2. Holding only** | What the player *has*, against a requirement - not progress toward earning more | Wallet and materials, already fetched today (`Services/Gw2AccountSnapshotService.cs:63,94,123,152`) | "You have 129 of 3,255 Fractal Relics." |
| **3. Described only, permanently** | Nothing. Not progress, not even the definition | `/v2/wvw/rewardtracks` returns `{"text": "API not active"}`. So does `/v2/pvp/rewardtracks`. There is **no** map- or world-completion endpoint (`/v2/account/mapchests` is daily chests, not completion). | **Gift of Battle** and **Gift of Exploration**: no recipe, no usable vendor row, no track definition, no progress. |

Two conclusions follow, and both are structural:

- **Tier 3 is small, enumerable, and contains the most important item in the
  game.** Gift of Battle is required by Gen 1, Gen 2 and Gen 3 weapons, and by
  Aurora, Conflux and Warbringer - 7 of 18 trees measured, not all of them. A model that assumes progress is fetchable, and treats a null
  as "not fetched yet", will be silently wrong about it forever.
- **Therefore the tier must be an explicit, curated field on the route, not
  an inference from whether a lookup returned null.** The two states the UI
  must never confuse:
  - *"No progress shown because we could not get it"* - no API key, missing
    scope, fetch failed, or the account genuinely has zero. Recoverable; the
    UI should say what is missing.
  - *"No progress shown because none exists, ever"* - tier 3. Not
    recoverable; the UI should say the sentence and stop, with no implied
    promise that connecting an API key would help.

### 2.2 Where the variety lives: one member, or many?

| Option | Shape | Cost |
|---|---|---|
| **A (recommended)** | One new `AcquisitionSource.Earned` + one `CraftingDecision.Earned`; the *kind* of activity and the *evidence tier* are data on the route, not enum members | One arm in `MapSource`, one branch in the pill planner. Every future activity is a data row. |
| B | No enum change at all; enrich `AcquisitionHint` and keep committing `UnknownSource` | Zero solver risk, but no consumer can distinguish "earned, described" from "unrecognised" without re-reading the hint dictionary at every site. Leaves `HasUnpricedNode` unable to tell the two apart, and gives the Prerequisites section no predicate to select on. |
| C | One member per activity: `WvwRewardTrack`, `WorldCompletion`, `RaidEncounter`, `Salvage`, `Drop`, `Collection`, ... | Multiplies every `switch` in the module (`MapSource` throws on an unhandled one, `MapShoppingRowType`, `DecisionPillPlanner`, `PlanStep.Source` aggregation). Each new activity becomes a code change plus a persisted-enum-ordinal hazard. And none of these is a *decision the solver makes* - the solver never picks between "raid" and "world completion". |

**Recommendation: A.** The solver's job is to choose among routes it can
compare; an earned route is a single terminal outcome regardless of which
activity produces it. The activity is a *description*, and descriptions
belong in data. PROPOSED.

### 2.3 The types

PROPOSED.

```
// Models/AcquisitionSource.cs - appended
Earned,

// Models/CraftingDecision.cs - appended LAST (see 6.2)
Earned,
```

**The tier, explicit and curated** (2.1):

```
internal enum EarnedEvidenceTier
{
    // The route's progress is NOT observable by any API, now or ever.
    // WvW and PvP reward tracks, world/map completion. A null progress
    // reading here is the answer, not a failure. DEFAULT, deliberately:
    // an un-annotated row must claim nothing.
    Described,

    // The account API reports a HOLDING that this route accumulates
    // toward (a wallet currency, a material). Progress toward earning
    // more is still unobservable; what the player has is not.
    Held,

    // The account API reports progress toward the route itself:
    // achievement bits, weekly raid clears, Wizard's Vault objectives.
    Live,
}
```

`Described` is value 0 and the default on purpose. A row that nobody has
annotated must degrade to "we can only describe this", never to "we will
show you progress" that then never arrives.

**The activity kind:**

```
internal enum EarnedActivity
{
    Unspecified,    // a described route that fits no bucket; renders EARNED
    RewardTrack,    // WvW / PvP reward track            - always Described
    MapCompletion,  // world completion, map exploration - always Described
    Encounter,      // raid / strike / fractal / boss kill
    Salvage,
    Drop,           // container, chest, RNG
    Collection,
    Achievement,    // content -> achievement -> item; can be Live
    Conversion,     // XP overflow (Spirit Shards), currency exchange
    Barter,         // a real vendor whose entire cost the module cannot price
}
```

`Barter` earns its place because three of the ten existing seed rows are
exactly that (items 106712, 105804, 106986 - *"Paid for in account-bound
items, so this module cannot price it"*), and calling those "earned" would
be wrong. See 6.3.

**The route**, extending today's hint rather than replacing it:

```
internal class AcquisitionHint          // name kept; see 6.4
{
    public int ItemId { get; set; }
    public string Hint { get; set; }        // existing - the sentence
    public string Badge { get; set; }       // existing - the pill label
    public string SourceUrl { get; set; }   // existing - maintainer only
    public string LastVerified { get; set; }// existing - maintainer only

    // NEW, all optional:
    public EarnedActivity Activity { get; set; }      // default Unspecified
    public EarnedEvidenceTier Tier { get; set; }      // default Described
    public string Rate { get; set; }        // PROSE, not a number. See 2.4.
    public string Cap { get; set; }         // "1 per week per account"
    public string Prerequisite { get; set; }// "requires Astral Heartbeat"
    public bool OncePerCharacter { get; set; }

    // Tier == Live only. The achievement whose completion PAYS OUT this
    // item, so /v2/account/achievements can be read for bits progress.
    // 0 when unknown or not applicable.
    public int AchievementId { get; set; }

    // Tier == Held only. The wallet currency id this route accumulates.
    // 0 when not applicable.
    public int HeldCurrencyId { get; set; }
}
```

`AchievementId` and `HeldCurrencyId` are the *only* fields that make a
tier's promise machine-checkable, which is why they are separate from the
prose. An implementing agent should treat `Tier == Live && AchievementId ==
0` as a seed defect, not a runtime case to handle gracefully.

### 2.4 Why `Rate` and `Cap` are prose and not numbers

This is a real decision with a real alternative.

| Option | Reason |
|---|---|
| **Prose strings (recommended)** | The gap analysis's own confidence section records that the taxonomy research is *"reliable on structure... and explicitly unreliable on numbers"*, with the wiki contradicting itself three ways on WvW ticket totals. A typed `int PerWeek` invites arithmetic - a "this takes 14 weeks" claim - on data nobody has verified. A string cannot be summed by accident. |
| Typed numeric rate/cap | Would let P3 compute a calendar floor without a second migration. But P3 needs a *verified* number to be worth anything, and P1 shipping an unverified one under a typed field is how a guess becomes a fact. |

**Recommendation: prose in P1.** When P3 (time-to-complete) is scoped, it
adds its own typed fields with its own verification standard, and the prose
stays as the human explanation beside them. Stated plainly: **this design
deliberately makes it impossible for P1 to compute a duration.** That is the
seam with P3 (section 8).

Note the tier interacts here: a *Live* route's cap is often already
observable (`/v2/account/raids` weekly clears, Wizard's Vault caps), so P3
will get some numbers for free from tier 1 and none at all from tier 3. That
asymmetry is P3's problem, but the tier field is what will let it see the
asymmetry rather than guess at it.

### 2.5 Where the data comes from

Honest split, because this is the part that decides whether P1 is a week or
a quarter.

**Machine-derivable, and this is the good news:**

- **The item-to-achievement index.** `/v2/achievements` declares each
  achievement's `rewards`, including `{type: Item, id, count}` (MEASURED for
  achievement 2258 -> item 70598). The endpoint enumerates all ids and is
  bulk-expandable. So the reverse map *item id -> achievement that pays it
  out* is generatable, not curated. This fits the repo's existing pattern
  exactly: a maintainer-side tool under `tools/` emits a `ref/*.json` seed,
  as `tools/VendorOfferUpdater` already does for vendor offers. That single
  tool supplies `AchievementId` and `Tier = Live` for the entire achievement
  class - including the SotO lantern-collection case - with no hand
  curation at all.
- **The work list.** Run the existing solver over a target and collect every
  node committing the terminal decision. The capability audit's section 8
  nominates `tools/TaimisToolbench.Harness` for precisely this and notes the
  harness already exists. This turns "which items do we owe a sentence?"
  from research into a command.
- **Untradeability.** `Models/ItemMetadata.cs:21` (`IsAccountBound`), already
  captured from `/v2/items`. It corroborates that no TP route exists; it
  never names the route that does.

**Curated by hand, with no derivation available:**

- The activity kind, the tier for everything except the achievement class,
  the sentence, the rate, the cap, the prerequisite. There is no GW2 API
  field for "this comes from a WvW reward track" - the endpoint that would
  say so returns `"API not active"` (2.1). This is wiki research, one row at
  a time, with `sourceUrl` and `lastVerified` filled in - the discipline the
  existing ten rows already follow, and the reason the file's schema already
  has those two columns.

**Scale, stated so nobody is surprised:** ten rows exist. INFERRED, from the
component list in the gap analysis (Gift of Battle, Gift of Exploration,
Legendary Insights/Divinations, Spirit Shards, Provisioner Tokens, the
per-map exploration gifts, the mode currencies): the hand-curated remainder
is on the order of **30 to 60 rows** - materially fewer if the achievement
index lands first and absorbs the collection/achievement class wholesale.
Bounded and reviewable, and it is the majority of P1's effort. The code is
the small half.

**Repo-invariant check:** "Do not invent data when APIs are missing" is
satisfied the same way the existing seed satisfies it - every hand-written
row cites a wiki URL and a verification date, and a row with no source is
not written. Generated rows cite the API. This is transcription with
provenance, not invention.

---

## 3. How it flows through the solver

### 3.1 The commit site

PROPOSED. One site changes: `Services/PlanSolver.cs:1294`.

```
// today
return Commit(AcquisitionSource.UnknownSource, null, null, 0, null);

// proposed
return Commit(
    ctx.EarnedRouteItemIds != null && ctx.EarnedRouteItemIds.Contains(node.Id)
        ? AcquisitionSource.Earned
        : AcquisitionSource.UnknownSource,
    null, null, 0, null);
```

`TotalCost` and `ComparisonValue` stay **null**, unchanged. That is the
whole arithmetic story, and it is deliberate:

- **Never zero.** `Commit` is not passed `0L`. The node contributes nothing
  because there is nothing to contribute, and it is the *parent's* summation
  (`Services/PlanSolver.cs:1499`, `?? 0L`) that turns null into zero, exactly
  as it does today for every unpriceable node.
- **Never dropped.** The node keeps its memo entry, its tree row, its pill,
  its Shopping List row and its `PlanStep`. Nothing about its visibility
  decreases, and section 4 adds a second, more prominent surface.
- **No re-ranking.** An `Earned` decision is reached only when every other
  route has already failed. It never competes with anything, so it cannot
  win or lose a comparison that an `UnknownSource` would not have.

The set is a `HashSet<int>` of item ids with a curated route, threaded
through `EvaluateContext` and `PlanSolveContext` beside the existing
`AcquisitionHints` dictionary.

**The solver never touches the tier.** Tier is a presentation contract, read
only by the view-model builder. Keeping it out of the solver is what makes
adding tier 1 later a UI change rather than a re-solve.

### 3.2 The two-tier comparability system, and what to do about it

This is **the hardest decision in the proposal.**

The two-tier comparability rule (`docs/ARCHITECTURE.md` sections 7.1 and 8)
exists so that a route whose cost cannot honestly be compared with coin
never wins a coin comparison. An unvalued currency line demotes its offer; an
unvalued Currency *ingredient* demotes its recipe
(`Services/PlanSolver.cs:1480-1486`); a non-`Item` ingredient demotes it
(`:1490-1496`). But an `Item` ingredient with **no source at all** does not
(`:1498-1509`, since `HasUnvaluedCurrency` stays false through the terminal
commit).

Concretely: a recipe consuming Gift of Battle is comparable-tier today, and
its comparison value omits Gift of Battle entirely. That is the same shape
of partial accounting the barter guard at `Services/PlanSolver.cs:1267-1284`
was added to refuse - *"a PARTIAL accounting while a craft route's real cost
is a complete one"*.

| Option | Effect | Verdict |
|---|---|---|
| **A. Demote the parent recipe to fallback tier when a chosen ingredient is `Earned`** (set `hasUnvaluedCurrency` on the transitive propagation at `:1508`) | Logically consistent with the barter guard and with the currency rule. | **Rejected for P1.** It reverses documented gw2e parity (`Services/PlanSolver.cs:1395-1399`), moves whole-result goldens in `tests/TaimisToolbench.Tests/Goldens/plan-solver/` which the repo treats as findings and not baselines, and cascades: for a legendary the earned components sit near the root, so transitive propagation would make essentially the *entire tree* fallback-tier and switch off comparison in the many places where it is still meaningful and correct. A change of that blast radius is not a stage-one change and is not what P1 is for. |
| **B (recommended). Leave the tier alone; add a separate, non-ranking flag** | `Decision.HasEarnedComponent`, propagated transitively the same way (`:1505-1509`) but read by **no** ranking code - not `PickCheapest`, not the tier split, not `PillSubduingEvaluator`. Its only consumers are the disclosure surfaces in section 4. | Recommended. It buys the honesty without touching a single decision. |
| C. Do nothing; rely on the per-node pill | Leaves the plan total presenting itself as complete. Fails the requirement that a partial total be communicated rather than hidden. | Rejected. |

**Recommendation: B, and file A as an open question rather than silently
declining it.** Option A is arguably the *correct* long-run answer and P1
should not pretend otherwise; it is rejected here on blast radius and
staging, not on principle. Section 9 records it as unsettled.

**Interaction with the barter guard.** None, and this is worth stating
because it looks like it should collide. The barter guard
(`Services/PlanSolver.cs:1267-1284`) fires inside the `if
(fallbackCraftCost.HasValue || vendorEvaluation.FallbackCoinCost.HasValue)`
branch at `:1256`. The `Earned` commit is at `:1294`, **after** that branch
has been declined - reached only when no fallback craft and no fallback
vendor offer exist. The two are mutually exclusive by control flow. An item
with a barter-priced vendor offer is a `BuyFromVendor` fallback and stays
one; it never becomes `Earned`, which is correct - it *is* purchasable, just
not with gold. That is what the `Barter` activity kind in 2.3 is for: it
describes such an item's *hint*, not its decision.

### 3.3 What a plan total must mean

PROPOSED, and this is the second hard decision.

The rule: **`TotalCoinCost` keeps meaning exactly what it means today - the
sum of coin the plan can actually account for - and the module stops
allowing that number to be read as the project's cost when it is not.**

Rejected alternative: give `Earned` nodes a synthetic cost (a valuation, a
"gold equivalent" of a reward track). This would make the total complete and
comparable and is what a price optimiser would do. It is rejected because it
is the exact thing `Models/CurrencyDecisionDefaults.cs` and the "never invent
an exchange rate" invariant refuse, and because there is no honest exchange
rate between gold and six weeks of WvW. The repo already made this call for
currencies; P1 does not get to make the opposite call for effort.

Rejected alternative: suppress the total entirely when a plan contains an
earned node. Rejected because the coin half is real, useful, and often the
part the user came for. Suppression trades a partial truth for no truth.

So the total stands, the Prerequisites section carries the other half, and
the summary footnote points from one to the other (4.4).

---

## 4. What the user sees: the Prerequisites section

### 4.1 Why a section beats scattering pills

The shape: a Prerequisites section, listing achievements, currencies, WvW
rewards and the like. That is how a player thinks about a legendary project:
**here is the gold part, and here is the list of things you must go and do.**

An earned component scattered as a pill three levels deep in a collapsed
tree is technically present and practically invisible. The Prerequisites
section is the answer to a question the tree cannot answer at all: *what
does this project require of me, in total?*

**This is not new chrome; it is the third member of an existing family.**
`PlanSectionType` already has `RequiredDisciplines` and `RequiredRecipes`
(`Models/PlanViewModel.cs:11-12`) - two sections whose entire job is "things
you must have that are not purchases". `Prerequisites` is the third, and it
is the one that covers activity rather than capability.

**The pills stay.** The tree pill answers *what is this node*; the section
answers *what must I do*. Removing the pill would leave a node in the tree
with no explanation of why it has no cost.

### 4.2 The section

PROPOSED. `PlanSectionType.Prerequisites`, one new `PlanRowType`, placed
**immediately after `Summary`**.

Placement reasoning: Plan Notes is last because *"every note kind is a caveat
ABOUT facts shown in an earlier section"* (`Models/PlanViewModel.cs:16-19`).
A prerequisite is not a caveat about the total - it is the other half of the
answer, and it is peer to the total, not subordinate to it. Putting it
directly under the money makes the two halves read as one answer.

**Row shape.** Name, quantity, and a status cell whose content is determined
by the tier - the tier's whole purpose is that this cell has three distinct
contracts:

| Tier | Status cell | Example row |
|---|---|---|
| `Live` | Real progress from the account API | `Mistward Legguards - 1 - 4 of 7 lanterns` |
| `Held` | Holding against requirement, from the wallet the module already fetches | `Fractal Relics - 3,255 - you have 129` |
| `Described` | The route sentence only. **No progress affordance of any kind** - no bar, no "0 of", no "connect an API key to see progress" | `Gift of Battle - 1 - WvW Gift of Battle reward track` |

The `Described` row deliberately does **not** render a zeroed progress
control. A "0 of 1" next to Gift of Battle is a promise the API can never
keep (2.1), and an empty progress bar reads as "you have made no progress"
rather than "this cannot be measured".

**No coin cell.** `CoinValue = 0`, following the convention
`Models/PlanViewModel.cs:93-98` states for `NoteLine`, where
`NotesSectionRenderer` draws a coin cell only when `CoinValue > 0`. A
prerequisite has no price by definition; rendering `0` would be the exact
lie section 3.3 refuses.

**Grouping.** Rows sorted by tier descending (`Live`, then `Held`, then
`Described`), then by name ordinal - the same "sort by resolved name, not by
composed label" rule the excess block already uses
(`Services/PlanViewModelBuilder.cs:1102-1104`). Tier-first because the
actionable and the measurable belong at the top, and the "just go do this"
items read fine as a tail.

**Empty state.** The section is omitted entirely when the plan has no earned
node - the same posture every other optional section takes.

### 4.3 The achievement chain, end to end

The motivating case - an item obtained from map completion, or from lighting
lanterns for a SotO achievement reward - is the tier-1 case and it is fully
expressible (2.1, MEASURED):

1. The seed (generated, 2.5) says item 70598 is paid out by achievement 2258.
2. `/v2/achievements/2258` declares the payout and the 7 bits.
3. `/v2/account/achievements` returns `current`, `max`, `done` and which
   `bits` are ticked for that account.
4. The row renders `4 of 7`.

Two rendering notes, both PROPOSED:

- **Bit names, not bit numbers.** `/v2/achievements` names each bit. The row
  should say "4 of 7 lanterns" using the achievement's own noun where the
  data supplies one, not "4 of 7 bits". A raw bit index is an internal id and
  the repo invariant forbids showing ids to users.
- **`done` short-circuits the row.** An achievement already complete means
  the item is already earned or already claimable; the row should say so
  rather than showing `7 of 7`. This is the natural place the
  `/v2/account/legendaryarmory` idea would eventually plug in (8, seams).

### 4.4 The summary footnote, widened and pointed

Two changes to `Services/PlanViewModelBuilder.cs`.

**(i) Widen the disclosure gate.** Today (`:240`) the "these totals are a
floor" footnote requires `TotalCoinCost == 0`. PROPOSED: it also fires when
the plan contains an earned node at any total, driven by the section-3.2(B)
flag, so the marker `*` (`:208`) and a footnote appear on a 3,000-gold
legendary plan. The footnote's wording changes to point at the new section
rather than restating the mechanism:

```
* This total covers what can be bought or crafted. N prerequisites must be
  earned - see Prerequisites above.
```

The existing zero-total wording stays for the case it was written for (a
plan with genuinely unrecognised items and no earned route).

**INFERRED severity: Must Fix within its stage.** Naming the earned nodes in
a dedicated section while the summary still presents a complete-looking total
would make the module *less* honest at the summary level than before, because
the reader now knows those components exist and the total still does not
mention them.

**(ii) Plan Notes is NOT used for this.** An earlier draft of this proposal
put earned components in Plan Notes to avoid new chrome. That instinct was
wrong: Plan Notes is explicitly the home for *caveats about facts shown
elsewhere*
(`Models/PlanViewModel.cs:14-24`), and a prerequisite is a fact in its own
right. Plan Notes keeps exactly the note kinds it has.

### 4.5 The currency table

No change to the table. It reports what the plan will spend from the wallet,
which is correct and unaffected.

What changes is that a mode currency with a curated route now *also* gets a
`Held`-tier Prerequisites row saying how it is earned. That is the answer to
the capability audit's line - *"a currency is a cost the plan reports; it is
never a thing the plan tells you how to get"* - and it is answered by adding
a row in the new section, not by restructuring the table. The two surfaces
have different jobs: the table says what it costs, the section says how you
get it.

### 4.6 Node-level surfaces

Unchanged in shape, improved in content:

- **Pill.** `Services/DecisionPillPlanner.cs:243-260` already prefers the
  seeded badge over `"UNKNOWN"`. The fallback for an `Earned` node with no
  badge becomes **`EARNED`** rather than `UNKNOWN`; a seeded badge still wins
  (`WVW`, `EXPLORE`, `SALVAGE`, `RAID`).
  `IsReservedSourceBadgeText` (`:99-105`) gains `EARNED`, for the reason its
  own comment gives: a hand-written badge reading `EARNED` beside a
  module-owned `EARNED` pill is indistinguishable while meaning something
  narrower. The interactive `IGNORE` toggle stays - it is the module's
  admission that the user may already own the item, which is *more* true for
  an earned item, not less.
- **Row tooltip.** `Services/ShoppingSourceBadge.cs:81-85` composes from the
  route instead of falling back to *"No known acquisition source"*:
  `Earned, not bought. <Hint>` then `Rate:` / `Limit:` / `Requires:` lines
  when present. No new field on `PlanRowViewModel` is needed - `HintText`
  (`Models/PlanViewModel.cs:326`) already carries composed prose and
  `Services/PlanViewModelBuilder.cs:766-784` already resolves it.
- **Shopping List row type.** `MapShoppingRowType`'s permissive `default`
  (`:1439-1447`) puts an `Earned` step on `ShoppingUnknown` with no change,
  and `ResolveHintText`/`ResolveBadgeText` (`:771`, `:796`) keep working
  because they key on that row type. Leave it alone rather than "tidying" it
  into an explicit arm - the arm buys nothing and a new shopping row type
  would ripple into every renderer.

---

## 5. UI plumbing a new section costs

MEASURED, so the estimate is not a guess.

| Site | What is needed |
|---|---|
| `Models/PlanViewModel.cs:5-31` | `PlanSectionType.Prerequisites` member |
| `Models/PlanViewModel.cs:33-101` | One `PlanRowType.PrerequisiteRow` member |
| `Services/PlanViewModelBuilder.cs` | A `BuildPrerequisitesSection`, and its call in `Build()` after the summary |
| `Views/CraftingPlanView.cs:4708-4742` | One `case` in the section render dispatch |
| `Services/PlanContentHeightMath.cs:287-313` | Either a `case`, or a deliberate reliance on the `default` arm (`rows.Count * FallbackTextRowHeight`). The `Notes` section deliberately takes the latter because its rows wrap to a variable number of lines (`Models/PlanViewModel.cs:19-23`). A Prerequisites row is one fixed-height line, so it should take an explicit `case` and be counted like the other column-header tables. |
| A new renderer under `Views/Rendering/` | Follows `DisciplinesSectionRenderer` / the existing table renderers |
| `TaimisToolbench.csproj` | Explicit `<Compile Include>` for any new `.cs` |

**Persistence cost of the new section: zero.** MEASURED: `PlanViewModel`,
`PlanSectionType` and `PlanRowType` do not appear anywhere in
`tests/shared/persisted_plan_schema.txt` (248 lines, grepped). The view model
is rebuilt from `CraftingPlanResult` on every render and is not part of the
persisted graph. Section expansion state is keyed by `PlanSectionType` but
that is a runtime dictionary, not a plan file.

---

## 6. Migration and blast radius

### 6.1 The Gift of Battle exclusion is NOT reverted

Per 1.6: `ref/vendor_offer_exclusions.json`'s stated reason is that the
Battle Master's sale **was removed from the game in 2016**, hand-verified
with a source. That is a factual claim about GW2, not a workaround for a
modelling limit. P1 changes what the module can express; it does not change
what the game sells. Reverting the exclusion would reintroduce a vendor row
for a purchase that does not exist, and the module would then confidently
price a legendary component from a decade-dead vendor - strictly worse than
today.

What P1 *does* change: item 19678 already has a seed row with badge `WVW`, so
it becomes an `Earned` decision, `Activity = RewardTrack`, `Tier = Described`,
and gets a Prerequisites row reading `Gift of Battle - 1 - WvW Gift of Battle
reward track`, instead of an `UnknownSource` counted at 0 with no summary
mention. Given 2.1 - no recipe, no usable vendor row, no track definition, no
progress endpoint - **a curated sentence is the maximum the game will ever
allow the module to say about the single most universally required legendary
component.** That is the strongest possible argument for P1 existing at all.

**This corrects a premise in two upstream documents.** Both
`legendary-gap-analysis.md` (section 4, P1's closing note) and
`module-capability-audit.md` (section 3) describe the exclusion as motivated
by inexpressibility. An implementing agent should not go looking for an
exclusion to revert, and whichever document is merged should be corrected in
the same wave.

### 6.2 The two enums

- **`AcquisitionSource`** carries `[JsonConverter(typeof(StringEnumConverter))]`
  (`Models/AcquisitionSource.cs:22-25`) precisely so a member reorder cannot
  remap a persisted plan. Appending `Earned` is safe: no existing file
  contains the name, and every existing name still binds.
- **`CraftingDecision` has NO converter** and round-trips as a **raw ordinal
  int** (`Models/CraftingDecision.cs:16-21`). `Earned` must be appended
  **last**, after `UnrecognizedIngredient`. Inserting it anywhere else
  silently reassigns every later member's on-disk integer.
- **`MapSource`'s `default` throws** (`Services/CraftingTreeBuilder.cs:536-547`).
  A new `AcquisitionSource` member without a matching arm is a runtime throw,
  by design. Add the arm in the same commit as the member.
- **`MapShoppingRowType`'s `default` does not throw** (`:1439-1447`) and needs
  no edit (4.6).

### 6.3 The ten curated hints: seed, not superseded

They are the seed of the new data, not a thing to replace. All ten keep
working unchanged if `Activity` defaults to `Unspecified` and `Tier` to
`Described`. Backfilling is a data edit with no code coupling:

| Item | Existing badge | Activity | Tier |
|---|---|---|---|
| 71994 Ball of Dark Energy | `SALVAGE` | `Salvage` | `Described` |
| 70698 / 70797 / 71943 / 74528 exploration gifts | `EXPLORE` | `MapCompletion` | `Described` (no completion endpoint exists) |
| 19678 Gift of Battle | `WVW` | `RewardTrack` | `Described` (endpoint returns "API not active") |
| 43772 Charged Quartz Crystal | `DAILY` | `Conversion` | `Held` (materials are already fetched) |
| 106712 / 105804 Gifts of the Survivors / People | `MERCHANT` | `Barter` | `Described` |
| 106986 Gift of the Hylek | `ACHIEVEMENT` | `Achievement` | `Live` once the index exists; `Described` until then |

The `Barter` rows are why the activity enum must exist rather than being
derived from "a hint row exists" - see 7.1.

### 6.4 Persistence: what moves and what does not

This is the constraint to check, and the answer is favourable.

MEASURED from `Models/PersistedPlan.cs:41,60-61`: `CurrentSchemaVersion` is
**3** and `SchemaShapeHash` is a SHA-256 over the persisted graph's public
member *signatures*, snapshotted at `tests/shared/persisted_plan_schema.txt`
(248 lines). `AcquisitionHint`'s five members are lines 1-5 of that file;
`CraftingPlanResult.AcquisitionHints` is line 23;
`CraftingTreeNode.AcquisitionHint` is line 50.

Three facts follow:

1. **Adding an enum MEMBER does not move the shape hash.** The snapshot
   records member signatures (`CraftingTreeNode.Decision:CraftingDecision`),
   not enum contents. So `AcquisitionSource.Earned` and
   `CraftingDecision.Earned` are invisible to the guard - which is exactly
   why `CraftingDecision.cs:16-21`'s APPEND-LAST rule must be honoured by
   hand. Nothing will catch a violation.
2. **The whole view-model layer is outside the graph**, so the Prerequisites
   section costs nothing (section 5).
3. **Adding a PROPERTY to `AcquisitionHint` moves the hash but need NOT bump
   `CurrentSchemaVersion`.** The precedent is `docs/ARCHITECTURE.md` section
   12.4: `CurrencyMetadata.Description` was *"purely additive: one string,
   absent from an older file and left null by Newtonsoft... A plan written
   before it still deserializes and `CurrentSchemaVersion` stays at 3."* The
   new optional members in 2.3 are the identical shape. The two new **enum**
   properties (`Activity`, `Tier`) are the one wrinkle: an absent int
   deserializes to 0, which is `Unspecified` and `Described` respectively -
   chosen as value 0 for exactly this reason (2.3).

**So the staging follows the persistence, not the other way round:**

- **Stage one touches the persisted graph not at all.** Enum members and a
  view-model section only. Shape hash unchanged, `persisted_plan_schema.txt`
  unchanged, version 3 unchanged, every saved plan and plan-history blob
  survives untouched.
- **Stage two pays the shape-hash move once**, additively, with
  `CurrentSchemaVersion` staying at 3: edit `SchemaShapeHash`, regenerate
  `tests/shared/persisted_plan_schema.txt`, confirm
  `PlanCompatibilityFixtureTests` still loads every fixture in
  `tests/shared/plan_fixtures/`.

**A schema-version bump is never required by anything in this proposal.** If
an implementing agent finds themselves editing `CurrentSchemaVersion`, the
design has drifted - stop and re-read this section.

### 6.5 Everything else that changes behaviour

| Site | Change | Risk |
|---|---|---|
| `Services/CraftingTreeBuilder.cs:486-509` `ApplyAcquisitionHint` | The `Decision != Unknown` guard must also admit `Earned`, or the hint stops being applied to precisely the nodes P1 exists for | **Critical if missed** - silent, and it removes text that works today |
| `Services/PlanViewModelBuilder.cs:418` `HasUnpricedNode` | Must count `Earned` as unpriced as well as `Unknown` | **Critical if missed** - promoting the decision without this makes the floor disclosure *stop* firing on zero-total plans where it fires today: a strict regression |
| `Services/DecisionPillPlanner.cs:99-105`, `243-260` | `EARNED` fallback text, reserved-word list | Low |
| `Services/ShoppingSourceBadge.cs:81-85` | Composed tooltip | Low |
| `Services/PlanViewModelBuilder.cs:240,209-213` | Widen the floor gate, reword the footnote (4.4) | Medium - changes what a *priced* plan displays, so summary-row assertions move. That movement is the feature. |
| `Models/PlanSolveContext.cs`, `EvaluateContext` | Thread the earned-id set | Low; mirrors `AcquisitionHints`, already threaded |
| New section plumbing | Section 5 | Medium, but mechanical and pattern-matched to two existing sections |

**Tests.** Existing coverage that will move, all under
`tests/TaimisToolbench.Tests/`:
`Services/PlanViewModelBuilderAcquisitionHintsTests.cs` (asserts hint/badge
resolution against `UnknownSource` steps),
`Services/PlanSolverCoreDecisionTests.cs` (four assertions on
`AcquisitionSource.UnknownSource`, at `:48`, `:281`, `:286`, `:365`),
`Services/DecisionPillPlannerTests.cs:612`, and the plan-solver goldens if a
golden target has a seeded hint. New tests must exercise the real solver over
a real seed - the repo forbids contract-mirror tests - and the two that
matter most are: *a priced plan containing an earned node renders the floor
footnote and a Prerequisites section*, and *a `Described`-tier row renders no
progress affordance*.

---

## 7. Staging

Each stage ships and is useful alone. The stages run **tier 3 -> tier 2 ->
tier 1**, i.e. cheapest evidence first, because that ordering also runs
"no new API" -> "no new scope" -> "new scope".

### 7.1 Stage one: the typed source and the section (no new API calls)

**Ships:** `AcquisitionSource.Earned` + `CraftingDecision.Earned`; the
`MapSource` arm; the commit-site change at `Services/PlanSolver.cs:1294`;
the two guard widenings in 6.5 (`ApplyAcquisitionHint`, `HasUnpricedNode`);
the `EARNED` pill fallback and reserved-word entry; the composed row tooltip;
**the Prerequisites section rendering `Described`-tier rows only**; and the
seed expansion, using the existing five columns.

**Derives `Earned` from the presence of a curated route.** Any item id with a
seed row commits `Earned`; any without stays `UnknownSource`. That is the
existing ten-row semantic promoted to a type, and it costs **zero persisted
schema movement** (6.4).

**No new API call and no new scope.** Every row is `Described`. This is
argued rather than assumed: `/v2/account/achievements` needs the
`progression` scope, which is not among the module's current required
permissions (`Services/Gw2AccountSnapshotService.cs:17-23`: `Account`,
`Characters`, `Inventories`, `Wallet`). Adding a scope changes what the
module asks users for and what it does when the key lacks it - a whole
concern of its own, and not one stage one should carry.

**Deliberately does NOT do:**

- No `Tier`, `Activity`, `Rate`, `Cap`, `Prerequisite`, `AchievementId`
  fields - no schema-hash move. Every row renders as `Described` because
  that is the only tier stage one can honour.
- No summary-gate widening. Stage one's honesty gain is the section's
  existence; the summary still behaves exactly as today. Sequenced this way
  because the gate widening moves summary assertions across the suite and
  deserves its own reviewable commit.
- No progress of any kind, no duration, no availability gate, no solver tier
  change, no ranking change, no `HasEarnedComponent` flag.

**Why this is a real ship:** a user opening a legendary plan gets a
Prerequisites section listing Gift of Battle, Gift of Exploration and the
rest with a real sentence each, instead of those items being invisible
`UNKNOWN` leaves three levels down a collapsed tree. That is the gap
analysis's stated bar, met with the Prerequisites shape.

**Known wart it leaves:** three of the ten rows are barter purchases and will
read as earned (6.3). Acceptable for one stage - "you must go get this, we
cannot price it" is still true - and resolved by stage two.

### 7.2 Stage two: structure, tiers, and the wallet (still no new scope)

**Ships:** `EarnedActivity` and `EarnedEvidenceTier` and the route fields
(2.3); the seed backfill including the `Barter` correction; the non-ranking
`HasEarnedComponent` flag (3.2 option B); the widened, pointed floor footnote
(4.4); **`Held`-tier rows** rendering "you have 129 of 3,255" from the wallet
and materials the snapshot service **already fetches under existing
permissions**; the shape-hash regeneration (6.4).

**Deliberately does NOT do:** change any tier of the *comparability* system,
any comparison, or any total. Adds no API scope.

**Why it ships alone:** this is the stage where a plan stops claiming a
complete cost it does not have, and where the currency prerequisites become
actionable against what the player already holds. The `Held` tier is
unusually cheap - the data is already in the snapshot - which is why it is
sequenced ahead of the more valuable but more expensive `Live` tier.

### 7.3 Stage three: the achievement index and live progress

**Ships:** a maintainer-side tool under `tools/` that walks `/v2/achievements`
and emits `ref/achievement_rewards.json` (item id -> achievement id + bit
names), following the `tools/VendorOfferUpdater` pattern; the `progression`
scope; an `/v2/account/achievements` fetch alongside the existing snapshot
calls; and `Live`-tier rows with real bit progress (4.3).

**Why last:** it is the only stage that changes what the module asks users to
grant, and it needs the tier contract from stage two to have somewhere to
land. It is also the stage that delivers the lantern-collection case
literally.

**Degradation, specified now so it is not improvised later:** a `Live` row
with no key, no `progression` scope, or a failed fetch renders as a
`Described` row plus an explicit "connect an API key with progression access
to see progress" affordance. It must **never** render as `0 of 7`, and a
`Described` row must never render that affordance - that distinction is the
entire reason the tier is a curated field (2.1).

### 7.4 Stage four: coverage

Pure data. Run the harness over every legendary output, enumerate the
terminals, curate rows until the earned surface of the legendary chain is
described. Optionally add a maintainer-side check that every terminal
decision in a named target's tree has a route row, so coverage regression is
visible. Sized only after stage one's harness pass produces the actual list.

---

## 8. What this does NOT solve

Stated plainly, so the pieces do not collide.

- **Duration. "This takes N weeks" is P3, not P1.** P1 records a rate as a
  human sentence and structurally *cannot* compute a floor from it (2.4). The
  seam: P3 adds typed numeric fields beside P1's prose, with its own
  verification standard, and owns anything that multiplies, sums or
  schedules. P1 owns the description; P3 owns the arithmetic. P1's
  `EarnedEvidenceTier` is a gift to P3 - it tells P3 exactly which routes
  have observable rates (`Live`) and which will never have one
  (`Described`) - but P1 never uses it that way. If an implementing agent
  finds themselves writing `weeks = quantity / rate` in P1, they have crossed
  the seam.
- **Availability. "Can this account do this at all?" is P2/P4.** Achievement
  and collection *gates*, masteries, account unlocks, the vendor-side
  required-item field, expansion ownership. Note the overlap risk:
  P1 stage three reads `/v2/account/achievements` for **progress toward an
  item this plan needs**; P2/P4 would read the same endpoint for **whether a
  vendor exchange or recipe is unlocked**. Same endpoint, same scope,
  different question. The seam is: P1 never suppresses, re-routes or
  disqualifies anything on the basis of an achievement; it only reports.
  Whichever ships first should own the fetch and expose it, and the second
  should reuse it rather than adding a parallel one.
- **Ownership. "Do I already have this legendary?"** `/v2/account/
  legendaryarmory` is readable and directly relevant, and 4.3's `done`
  short-circuit is the natural hook. Out of scope for P1: it changes what a
  plan is *for*, not how a component is described.
- **Game-mode labelling and filtering (P4).** P1 will describe a WvW reward
  track in prose because the seed row says so. It adds no mode concept, does
  not read the 1,407 offers already carrying "World vs. World" in
  `locations`, and gives no way to exclude a mode. If P4 introduces a mode
  tag, `EarnedActivity.RewardTrack` is *not* it - one is a route kind, the
  other a content classification, and an item can be both or neither.
- **Pricing an earned route.** Never, in any stage. No effort-to-gold rate,
  no synthetic valuation, no "equivalent cost". 3.3.
- **Solver tier correctness for sourceless items.** Section 3.2's option A is
  not done, and P1 does not claim the comparability system is now consistent.
  It is not. See 9.1.
- **Time gates and caps as re-routing.** `Models/TimegatedItem.cs` and the
  daily-cooldown seed stay warn-only. A `Cap` string on a route is prose in a
  tooltip, not a constraint.
- **The multi-output, repeat-cost and RNG rows** (gap analysis rows 6, 7, 8).
  Untouched.

---

## 9. What this proposal could not settle

1. **Whether the comparability asymmetry in 3.2 should eventually be fixed.**
   An unvalued currency demotes its recipe; an item with no source at all does
   not. The second is documented gw2e parity
   (`Services/PlanSolver.cs:1395-1399`); the first is a deliberate divergence
   the repo chose. They cannot both be right on principle, and P1 defers
   rather than resolves. *To settle: run the harness over a legendary with
   option A applied and measure how much of the tree goes fallback-tier, then
   decide whether comparison survives it. That measurement was not made here
   and the number is not guessed.*
2. **How much of the achievement class the generated index actually covers.**
   2.5 rests on one verified example (achievement 2258 -> item 70598). Whether
   most collection and achievement rewards declare a machine-readable
   `rewards` entry, or whether that example is unrepresentative, is unmeasured.
   *To settle: one full walk of `/v2/achievements` counting achievements with
   an `Item`-typed reward, run before stage three is scheduled.* If coverage
   is poor, stage three shrinks to hand curation and the tool is not worth
   building.
3. **Whether `Earned` should be derived from the presence of a route row
   (stage one) or from an explicit activity (stage two), permanently.** Stage
   one's derive-from-presence is chosen for its zero schema cost and stage two
   replaces it. If stage two never ships, the three `Barter` rows stay
   mislabelled - an acceptable one-stage wart and an unacceptable permanent
   state.
4. **How many hand-curated rows the legendary chain actually needs.** 30-60 is
   INFERRED from the component list, not counted, and the achievement index
   could cut it substantially. The harness pass in 7.4 turns it into a number;
   no schedule should be built on the inference.
5. **Whether Prerequisites should sit above or below the Shopping List.**
   4.2 argues for immediately after Summary on the grounds that it is the
   other half of the answer. The competing view - that the Shopping List is
   what the user acts on first, so nothing should push it down - is
   reasonable, and this is a layout question best settled by looking at a
   real legendary plan rather than by argument.
