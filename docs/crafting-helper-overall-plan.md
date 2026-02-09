# GW2 Crafting Helper -- Overall Plan

> **Status**: Approved and frozen.
> This is the approved top-down product plan for the GW2 Crafting Helper module.
> It is frozen unless explicitly revised. Future implementation work must conform to it.

---

# Section 0: Existing Functionality Inventory

| File | What it does | Classification | Notes |
|---|---|---|---|
| **Models/AccountSnapshot.cs** | Root snapshot: coins, items, wallet | **Reusable with adaptation** | Good structure. Crafting plan needs to query "do I have X of item Y?" -- current flat list works but needs a lookup method or an adapter. |
| **Models/SnapshotItemEntry.cs** | Item entry with source tracking | **Reusable as-is** | `ItemId`, `Count`, `Source` are exactly what account-awareness needs. `Name`/`IconUrl` useful for display. |
| **Models/SnapshotWalletEntry.cs** | Wallet currency entry | **Reusable as-is** | `CurrencyId`, `Value` needed for vendor-currency checks. |
| **Services/Gw2AccountSnapshotService.cs** | Fetches full account state from GW2 API | **Reusable with adaptation** | Already fetches bank, materials, shared inventory, all characters, wallet. Resolves names/icons. Needs: recipe fetching, TP price fetching (new service or extension). |
| **Services/SnapshotStore.cs** | JSON file persistence for snapshots | **Reusable as-is** | |
| **Services/StatusStore.cs** | Text file persistence for status | **Reusable as-is** | |
| **Services/SnapshotHelpers.cs** | Aggregation, formatting, serialization | **Reusable as-is** | `AggregateItems` directly useful for "how many total of item X do I own?" |
| **Services/StatusText.cs** | Null normalization | **Reusable as-is** | Trivial utility. |
| **Views/MainView.cs** | Current snapshot-viewer UI | **Not aligned** | This is the account snapshot viewer. The crafting helper needs a fundamentally different view (plan steps, tree, acquisition actions). MainView stays for its current purpose; crafting gets a new view. |
| **Module.cs** | Lifecycle, window, corner icon | **Reusable with adaptation** | Window creation, corner icon, dirty-flag threading pattern, refresh orchestration -- all reusable. Will need a second view or a view-switching mechanism for the crafting helper. |
| **manifest.json** | Permissions: account, characters, inventories, wallet | **Reusable with adaptation** | May need `tradingpost` permission added if we use authenticated TP endpoints. Public `/v2/commerce/prices` needs no auth though. |

**Summary**: The account data pipeline (fetch -> snapshot -> persist) is solid and reusable. The UI layer is purpose-built for snapshot viewing and won't carry over. Module lifecycle patterns are reusable.

---

# Section 1: UX + Interaction Spec

## Window: "Crafting Helper"

Separate view shown in the existing `StandardWindow` (or a second window -- TBD). The crafting helper has three states:

### State 1: No Plan

The initial state. Shows:
- **Target item display**: icon + name of "Orrax Manifested" (hardcoded for now, future: typeahead selector)
- **Checkbox**: "Use my account materials" (checked by default)
- **Button**: "Generate Plan"
- Clicking "Generate Plan" fetches needed data (recipes, TP prices, account snapshot if checkbox is on) and produces the plan. A spinner/status label shows progress.

### State 2: Plan Active

The main working state. Shows:

**Header area:**
- Target item icon + name + quantity (1)
- Status: "Plan generated -- {time}" or "Syncing..."
- Checkbox: "Use my account materials" (toggle triggers confirmation if plan exists)
- Button: "Sync / Refresh" (triggers confirmation if plan exists)

**Plan body** (scrollable):
An ordered list of **acquisition steps**. Each step is one of:
- **Craft**: "Craft 1x Orrax Manifested at Weaponsmith station" -- shows ingredients needed for that specific craft, with sub-items resolved
- **Buy from TP**: "Buy 3x Orichalcum Ingot from Trading Post -- 1g 23s each"
- **Buy from Vendor**: "Buy 1x Thermocatalytic Reagent from Vendor -- 10c each"
- **Already Owned**: "Have 5x Mithril Ore in Material Storage" (only when account-awareness is on)
- **Unknown Source**: "Acquire 1x [item] -- no known automated source" (for items with no TP listing and no vendor data)

Each step shows: icon, item name, quantity needed, source, unit cost (if known), total cost.

**Summary footer:**
- Estimated total cost in gold (TP + vendor coin costs summed)
- Non-coin costs listed separately (e.g., "50 Spirit Shards") -- not converted to gold
- Items already owned (count, if account-aware)

### State 3: Confirmation Dialog

When user clicks "Sync / Refresh" or toggles the account-awareness checkbox while a plan exists:
- Modal or inline prompt: "This will regenerate the crafting plan and discard the current details."
- Two buttons: "Regenerate" / "Cancel"

### Interactions

| Action | Result |
|---|---|
| Click "Generate Plan" | Fetch data, produce plan, transition to State 2 |
| Click "Sync / Refresh" (plan exists) | Show confirmation -> regenerate or cancel |
| Toggle "Use my account materials" (plan exists) | Show confirmation -> regenerate or cancel |
| Toggle "Use my account materials" (no plan) | Just toggles the flag for next generation |
| Plan generation fails (API error, no permissions) | Show error in status label, remain in current state |

---

# Section 2: What Is a "Crafting Plan"?

A **crafting plan** is a resolved, ordered description of everything a player needs to do to craft a target item, starting from their current state (or from nothing).

Conceptually it contains:

1. **Target**: The item to craft, and how many.

2. **Recipe tree (resolved)**: The full dependency graph of the target item -- what it's made of, what those are made of, recursively -- with a decision at each node: craft it, buy it, or already have it.

3. **Acquisition list**: A flat, actionable list derived from the resolved tree. Each entry says: "You need N of item X. Get it by [crafting / buying from TP / buying from vendor / you already have it]." This is what the user sees.

4. **Cost summary**: Total coin cost of all TP and vendor-coin purchases. Non-coin currency costs listed separately without conversion.

**Key decisions baked into a plan:**
- At each node: is it cheaper to buy the item outright or craft it from sub-components?
- When the user owns materials: those are subtracted from requirements before costing.
- When multiple sources exist (TP and vendor), the cheapest known coin source wins. Non-coin vendor sources are shown as alternatives, not auto-selected over coin sources.

**What a plan is NOT:**
- It is not a live-updating dashboard. It's a snapshot-in-time calculation.
- It does not track progress ("you've crafted step 3 of 7"). It's a reference sheet.
- It does not auto-purchase anything.

---

# Section 3: Major Subsystems

| # | Subsystem | Responsibility | Exists? |
|---|---|---|---|
| **S1** | Account Data | Fetch and cache player inventory, wallet, materials, bank | **Yes** -- `Gw2AccountSnapshotService` + `SnapshotStore` |
| **S2** | Recipe Resolution | Given a target item ID, fetch its recipe and recursively resolve all sub-recipes into a dependency tree | **No** -- needs `/v2/recipes/search` + `/v2/recipes` calls |
| **S3** | Price Provider | For any item, provide available prices: TP buy/sell, vendor coin price, vendor currency price | **No** -- needs `/v2/commerce/prices` calls + a static vendor dataset |
| **S4** | Plan Solver | Given a recipe tree + prices + (optional) account inventory, produce a resolved plan with buy/craft decisions and cost calculations | **No** -- core new logic |
| **S5** | Plan Model | Data types representing the plan: steps, sources, costs | **No** -- new models |
| **S6** | Crafting View | UI to display plan state (no plan / generating / active plan / confirmation) | **No** -- new view |
| **S7** | Item Metadata | Resolve item IDs to names, icons | **Partial** -- `Gw2AccountSnapshotService` already does this for snapshot items; needs generalization for recipe ingredients |

---

# Section 4: High-Level Incremental Roadmap

Each milestone is a shippable, testable increment.

### Milestone 1: Recipe Tree
- Fetch the recipe for "Orrax Manifested" from `/v2/recipes/search?output=ID`
- Recursively resolve sub-recipes into a tree
- Pure data, fully testable without Blish HUD
- **Deliverable**: `RecipeTree` model + `RecipeService` that builds it

### Milestone 2: TP Price Lookup
- Fetch prices from `/v2/commerce/prices` for all leaf items in the tree
- **Deliverable**: `TradingPostPriceProvider` returning buy-instant / sell-instant prices per item

### Milestone 3: Plan Solver (no account, no vendors)
- Walk the recipe tree with TP prices
- At each node: decide buy vs craft (cheapest coin cost wins)
- Produce a flat acquisition list + cost summary
- **Deliverable**: `PlanSolver` + `CraftingPlan` model, fully testable

### Milestone 4: Crafting View (basic)
- New Blish HUD view showing the plan
- Generate Plan button -> calls recipe + price + solver -> renders steps
- Status labels, scrollable list, cost summary
- **Deliverable**: Working end-to-end for "Orrax Manifested" with TP-only pricing

### Milestone 5: Account Awareness
- Wire existing `AccountSnapshot` into the solver
- "Use my account materials" checkbox subtracts owned items
- Confirmation dialog on toggle/refresh
- **Deliverable**: Account-aware plans

### Milestone 6: Vendor Pricing
- Static vendor dataset (small, checked into repo)
- Vendor coin prices compete with TP prices in the solver
- Vendor currency prices shown as separate line items
- **Deliverable**: Vendor-aware cost optimization

### Milestone 7: Polish
- Error handling for missing recipes, API failures, no permissions
- Sync/Refresh flow with confirmation
- Cost summary footer with coin + non-coin breakdown
- Item metadata resolution for all plan items (names, icons)
