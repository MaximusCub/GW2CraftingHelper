> **Frozen record - 2026-08-17.** Ground-work checklist for the M37 item-29 live verification, closed and kept as evidence.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

# KNOWN-ISSUES item 29 - owned-materials UI live verification (M37)

Ground-work deliverables for the main session to execute. Read-only research
was performed against the real production code paths listed below; nothing
in the repo working tree was modified.

## 1. Where the file goes

Copy the fixture `docs/dev-notes/m38-plan/m37-item29-snapshot.json` over:

```
C:\Dev\Blish\blish-preflight-settings\data\snapshot.json
```

(WSL path: `/mnt/c/Dev/Blish/blish-preflight-settings/data/snapshot.json`)

This overwrites the existing placeholder (`CoinCopper: 0, Items: [],
Wallet: []`). The format is the real `SnapshotStore`/`AccountSnapshot`
Newtonsoft.Json envelope (verified against
`GW2CraftingHelper/Services/SnapshotHelpers.cs`,
`GW2CraftingHelper/Services/SnapshotStore.cs`, and
`GW2CraftingHelper/Models/AccountSnapshot.cs` /
`SnapshotItemEntry.cs` / `SnapshotWalletEntry.cs`):

- `AccountSnapshot`: `CapturedAt` (DateTime), `CoinCopper` (int), `Items`
  (`List<SnapshotItemEntry>`), `Wallet` (`List<SnapshotWalletEntry>`).
  Plain `JsonConvert.SerializeObject(snapshot, Formatting.Indented)` -
  default PascalCase property names, no custom naming strategy or envelope
  wrapper.
- `SnapshotItemEntry`: `ItemId` (int), `Name` (string), `IconUrl` (string),
  `Count` (int), `Source` (string).
- `SnapshotWalletEntry`: `CurrencyId` (int), `CurrencyName` (string),
  `IconUrl` (string), `Value` (int).
- `SnapshotStore.LoadLatest()` reads this file once, in
  `Module.LoadAsync()` (`Module.cs:386`) - **the file must exist before
  the module loads** (i.e. before Blish HUD starts, or before the module is
  (re)enabled) for `_currentSnapshot` to pick it up. A hot-swap of the file
  while the module is already running will NOT be picked up without a
  module reload/Blish restart.

`Source` was deliberately set to `"MaterialStorage"` on every item
(`AccountItemIndex.SourceMaterialStorage`) - confirmed via
`Services/AccountItemIndex.cs`: `MaterialStorage` is **priority 1** in
`GetPrioritizedSources`, consumed before the active character's bag, shared
inventory, or bank, and unlike the character-priority tier it does **not**
depend on `Gw2Mumble`/an active character being detected. This makes the
demo robust even if the preflight Blish HUD instance has no live GW2
character context.

## 2. Settings entries to inject

None are strictly required for the core owned-materials behavior - see the
caveat below - but two are worth setting explicitly for a clean,
reproducible run. Edit
`C:\Dev\Blish\blish-preflight-settings\settings.json`, inside the
`GW2CraftingHelper` module's `Settings.Entries` array (the same array that
already holds `ModalDialogX`/`CurrencyValuationsJson`/etc. - see
`ModuleConfiguration -> ModuleStates -> GW2CraftingHelper -> Settings ->
Entries`):

```json
{
  "T": "System.Boolean, mscorlib",
  "Key": "ValueOwnMaterials",
  "Value": true
}
```

This entry **already exists** in the current `settings.json` but is
currently set to `false` (line ~147). Flip it to `true` (or delete the
entry entirely so the code default of `true` applies -
`ModuleSettings.cs:66`). This is gw2efficiency's "value own materials"
toggle (force-buy pre-pass + sell-opportunity-cost profit adjustment); it
is **not** what makes the reduction/pills appear (see caveat below), but
M34's owned-currency/force-buy behavior is written to assume it is on, and
leaving it `false` would be an inconsistent state to screenshot from.

No other settings entries are needed. `CurrencyValuationsJson` should stay
at its current empty-string default - leaving Spirit Shards unvalued is
what forces the Bloodstone Shard node to a deterministic Vendor decision
(see item 29's currency section below); assigning it a valuation would
change that decision to a price comparison.

**Important caveat - this is a UI checkbox, not a persisted setting:**
`CraftingPlanView`'s own "Use Own Materials" checkbox
(`Views/CraftingPlanView.cs:1350-1357`, backed by the private field
`_useOwnMaterials`) defaults to **unchecked every time the view is built**
- it is plain session state, not read from `ModuleSettings` and not
persisted anywhere in `settings.json`. There is no settings-injection
shortcut for it. **The verifier must manually tick the "Use Own Materials"
checkbox in the Crafting Plan tab** before generating the plan (or after
generating once, ticking it and confirming the "This will regenerate the
plan" modal). Only when this checkbox is checked does
`Module.cs`'s generate lambda (`Module.cs:257-286`) pass `_currentSnapshot`
into `CraftingPlanPipeline.GenerateStructuredAsync` at all; with it
unchecked, `null` is passed regardless of the settings.json content or the
presence of a snapshot file, and none of the M34 owned-materials UI can
appear.

## 3. What to generate

1. Launch Blish HUD against the preflight profile (existing process - out
   of scope for this note).
2. Open the GW2 Crafting Helper window -> **Crafting Plan** tab.
3. Search/select item id 90551 ("Exordium") - the same canonical item the
   offline Harness uses for `--profile exordium`
   (`tools/GW2CraftingHelper.Harness/Program.cs:336-339`). Quantity `1`.
4. **Baseline pass (do this first):** leave "Use Own Materials" unchecked,
   click Generate Plan, and note the Total Cost coin figure. This is the
   "no-snapshot baseline" for requirement (d).
5. Tick "Use Own Materials" (confirm the regenerate modal). This is the
   live-verification pass.
6. Screenshot: the full tree (scrolled to each node in the table below),
   the Total Cost / Summary section, and the Shopping List row for
   Bloodstone Shard (hover for its tooltip).

## 4. Snapshot design summary

Every chosen item was traced against the real seed data
(`ref/recipes_seed.json`, `ref/item_name_seed.json`, `ref/vendor_offers.json`)
using the exact same math the production code uses for tree-building
(`RecipeService.BuildNodeAsync`, `Services/RecipeService.cs:276-369`:
`craftsNeeded = ceil(quantity / ExpectedOutputCount-or-OutputItemCount)`,
`ingredientQuantity = craftsNeeded * per-craft ingredient count`) and for
reduction (`InventoryReducer.ReduceNodeSourced`,
`Services/InventoryReducer.cs`). All five items resolve through a **single,
unambiguous recipe option** at every step of their path from Exordium (no
multi-recipe tie-break uncertainty), and a full-tree occurrence scan
confirmed each item id appears **exactly once** anywhere in Exordium's
primary-recipe-option tree - so there is no shared-pool ambiguity between
two occurrences of the same item competing for the owned stock.

| Item (id) | Path from Exordium (90551) | Depth | Needed qty | Recipe ambiguity |
|---|---|---|---|---|
| Mystic Clover (19675) | Exordium -> Mystic Tribute (71820) -> Mystic Clover | 2 | 77 | none (1 recipe) |
| Stabilizing Matrix (73248) | Exordium -> Gift of Exordium (90893) -> Gift of the Mists (76427) -> Cube of Stabilized Dark Energy (73137) -> Stabilizing Matrix | 4 | 75 | none (1 recipe) |
| Mystic Runestone (79418) | Exordium -> Gift of Exordium (90893) -> Mystic Runestone | 2 | 100 | none (0 recipes, vendor-only leaf) |
| Augur's Stone (46752) | Exordium -> Exitare (90883) -> Visionary Inscription (80503) -> Vision Crystal (46746) -> Augur's Stone | 4 | 1 | none (0 recipes, vendor-only leaf) |
| Bloodstone Shard (20797) | Exordium -> Gift of Maguuma Mastery (73239) -> Bloodstone Shard | 2 | 1 | none (0 recipes, vendor-only leaf) |

Currency: **Spirit Shards** (currency id 23, confirmed against
`Models/Gw2Constants.cs:63` and every `CurrencyDisplayResolverTests`/
`CurrencyMetadataServiceTests` fixture - `CurrencyDisplayResolver.ResolveName(23,
null)` hard-falls-back to `"Spirit Shards"` even with no live
`CurrencyMetadata`, so the display name does not depend on a successful
live `/v2/currencies` call). Bloodstone Shard's only vendor offer
(`ref/vendor_offers.json`, Miyani / Ward Crafter Lucirae, both identical)
costs `200x Spirit Shards` per unit, `outputCount: 1` - confirmed via the
raw offer JSON, so 1 needed Bloodstone Shard = 200 Spirit Shards total.

## 5. Verification table (expected visual outcomes)

| # | UI surface | Item / row | Expected text | Price-independent? |
|---|---|---|---|---|
| 1 | Tree, depth 2 | Mystic Clover node | `USING 30 OWNED` pill next to a CRAFT/TP/VENDOR decision pill (remaining need 47) | **Yes** - `OwnedQuantityUsed`/pill text is set purely by `InventoryReducer` before any pricing runs (`DecisionPillPlanner.AppendOwnershipPills`, `Services/DecisionPillPlanner.cs:152-164`). Which decision pill is *selected* may vary with live TP prices; the `USING 30 OWNED` annotation itself will not. |
| 2 | Tree, depth 4 | Stabilizing Matrix node | `USING 30 OWNED` pill next to a decision pill (remaining need 45) | **Yes**, same reasoning as #1. |
| 3 | Tree, depth 2 | Mystic Runestone node | Single `HAVE` pill, node collapsed/no cost (needed 100, owned 150) | **Yes** - Mystic Runestone has zero TP listing and zero recipe (confirmed: `ref/recipes_seed.json` has no recipe producing item 79418); reduction alone zeroes `Quantity`, forcing `Decision.Have` regardless of any price data (`CraftingTreeNode.cs` doc comment: "Quantity reduced to 0 -> Decision.Have"). |
| 4 | Tree, depth 4 | Augur's Stone node | Single `HAVE` pill (needed 1, owned 5) | **Yes**, same reasoning as #3. This item exists in the snapshot specifically to *neutralize* its own 20-Spirit-Shard vendor cost so it does not add noise to row #6/#7's total - see caveat below. |
| 5 | Tree, depth 2 | Bloodstone Shard node | Single `VENDOR` pill (only one acquisition source - no recipe, no TP listing), **no** `USING N OWNED` pill (0 owned) | **Yes** - Bloodstone Shard is account-bound/non-tradeable (no TP listing possible) and has no recipe, so `CanCraft=false`, `CanBuyTp=false` structurally, leaving Vendor as the only feasible source regardless of price (`PlanSolver.cs:565-568`, the `fallbackVendorCoinCost.HasValue` path taken when nothing else is comparable). |
| 6 | Total Cost / Summary section | Currency cost row | `200x Spirit Shards` with a `(50 owned, 150 needed)` annotation directly below/beside it | **Yes**, conditioned on caveat below. Row label format: `PlanViewModelBuilder.cs:207` (`"{cc.Amount}x {currencyName}"`); annotation format: `CraftingPlanView.cs:3425` (`"({owned} owned, {needed} needed)"`). |
| 7 | Shopping List section | Bloodstone Shard row tooltip (hover) | Tooltip includes the line `Spirit Shards: 50 owned, 150 needed` | **Yes**, conditioned on caveat below. Format: `CraftingPlanView.cs:2860` (`"{cc.Name}: {cc.OwnedQuantity.Value} owned, {needed} needed"`). Confirmed this and row #6 read from the *same* `OwnedCurrencyAmounts[23]=50` value (`CraftingPlanPipeline.BuildOwnedCurrencyAmounts`), so the two numbers must always agree. |
| 8 | Total Cost / Summary section | Overall coin total | Visibly **smaller** than the no-snapshot baseline (step 4 in section 3) | **Partially** - the dominant, price-independent contributor is Mystic Runestone's elimination: 100 units x a fixed 1-gold vendor price (KNOWN-ISSUES.md line 443-444: "Mystic Runestone... 1-gold coin purchase from Miyani") = a fixed 100g reduction that does not depend on any live TP price. The Mystic Clover (30/77 owned) and Stabilizing Matrix (30/75 owned) partial reductions add *further* coin savings on top of that, but their exact size depends on live TP/craft prices for their subtrees - expect *some* additional reduction, don't hard-check an exact number for those two. |

## 6. Caveats / things not fully validated

- **Row #6/#7's exact "150 needed" depends on Spirit Shards having no other
  consumer in the plan.** Two other leaves in the Exordium tree also sell
  for Spirit Shards from Miyani: Philosopher's Stone (1 Spirit Shard each,
  appears many times deep in the tree at large quantities) and Augur's
  Stone (20 Spirit Shards, single occurrence). Philosopher's Stone is a
  common bulk TP commodity - as long as it has a live TP listing (near
  certain), `PlanSolver.cs:340-352`'s rule that an *unvalued* vendor
  currency line is excluded from cost comparison means Philosopher's Stone
  will resolve to `TP`, not `Vendor`, and contribute nothing to the Spirit
  Shards total. This was **not** verified against a live TP price feed
  (out of scope for this read-only pass) - if the live plan's Total Cost
  row shows a Spirit Shards total noticeably above 200 (e.g. because
  Philosopher's Stone's TP listing is genuinely absent), that is the
  reason; the `owned`/`needed` split will still be internally consistent
  (`owned` will just be `min(50, actual total)`).
  Augur's Stone is neutralized by owning 5 of the 1 needed (row #4) so it
  contributes 0 regardless.
- **`ValueOwnMaterials` (settings.json) does not gate rows #1-#5 or #6-#7.**
  It only feeds `OwnMaterialsMode` (force-buy pre-pass + profit-display
  opportunity cost, `ModuleSettings.GetOwnMaterialsMode`) - a value it
  already had `true` in code before M34-B2a; confirmed by reading
  `ModuleSettings.cs`, `CraftingPlanPipeline.cs`, and `Module.cs`'s wiring
  directly. The reduction/pill/currency-annotation behavior in the table
  above is driven entirely by (a) the snapshot file existing at module load
  and (b) the CraftingPlanView "Use Own Materials" checkbox being checked
  at generate time. Section 2's settings change is a hygiene step, not a
  functional prerequisite for rows #1-#7 - do it anyway, since M34's other
  behaviors (force-buy pre-pass, profit figure) assume it is on and a
  screenshot pass is a reasonable time to also confirm those aren't broken.
- **Could not run the offline Harness to cross-check these quantities**
  against the actual solved tree (would require building
  `tools/GW2CraftingHelper.Harness` via `dotnet build`, which was avoidable
  given the seed-data derivation above was sufficient and other agents are
  concurrently using the working tree - skipped per the task's own
  allowance). All quantities above were instead derived by hand-walking
  `ref/recipes_seed.json` with a script that reproduces
  `RecipeService.BuildNodeAsync`'s exact `craftsNeeded`/ingredient-scaling
  formula, and cross-checked against `InventoryReducer`'s reduction math
  and `PlanSolver`'s vendor/TP/craft comparison rules by reading the
  production source directly (not by executing it). If any live number in
  the table disagrees with what actually renders, trust the live render
  over this table and note the discrepancy - the *mechanism* claims (which
  pill kind appears, which fields drive it, whether it's price-independent)
  are the parts backed by reading the actual decision code
  (`DecisionPillPlanner.cs`, `PlanSolver.cs`, `InventoryReducer.cs`) and
  should be treated as reliable; the specific *quantities* (77, 75, 100, 1,
  200, etc.) are the parts backed by hand-replaying the seed JSON and
  should be treated as "expected, verify against actual."
- **`CoinCopper: 1500000` (150g) and the Spirit Shards `Value: 50`** in the
  snapshot are arbitrary but deliberately chosen so `owned (50) < needed
  (200)` for row #6/#7 and so the account "looks real" rather than
  all-zero. Neither number is derived from anything in the repo - if a
  more specific coin/Spirit-Shard balance is wanted, only the `Value: 50`
  line matters functionally (it must stay below whatever the live Spirit
  Shards total turns out to be, per the caveat above, or the "needed"
  annotation will not appear at all - `CurrencyOwnedQuantity` is only set
  when `OwnedCurrencyAmounts` contains the currency id, which it always
  will here, but if `owned >= needed` the annotation would show `(200
  owned, 0 needed)` instead of demonstrating a shortfall - keep it below
  200 if you change it).
- **Blish must be restarted (or the module reloaded) after copying the
  snapshot file**, since `SnapshotStore.LoadLatest()` only runs once, in
  `Module.LoadAsync()`. Editing the file while Blish HUD is already
  running and the module already loaded will have no effect until the next
  load.

## 7. Files

- Snapshot to copy: `docs/dev-notes/m38-plan/m37-item29-snapshot.json`
  (left in place by the 2026-08-24 docs rotation; only this checklist moved)
  -> `C:\Dev\Blish\blish-preflight-settings\data\snapshot.json`
- This checklist: `docs/archive/m37-item29-checklist.md`
