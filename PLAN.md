# Account Snapshot Cache - Implementation Plan

## Overview
Implement a vertical slice that pulls player account data from the GW2 API, caches it locally as JSON, and displays it in the module's StandardWindow with filtering.

---

## Step 1: Update manifest.json

Add `api_permissions` and `directories`:

```json
{
  "api_permissions": {
    "account":     { "optional": false, "details": "Required for account info and coin balance" },
    "characters":  { "optional": false, "details": "Required to list characters" },
    "inventories": { "optional": false, "details": "Required for bank, shared inventory, materials, character bags" },
    "wallet":      { "optional": false, "details": "Required for wallet/currency data" }
  },
  "directories": ["data"]
}
```

---

## Step 2: Create Models (Models/ folder)

### `Models/SnapshotItemEntry.cs`
Simple DTO for one item row in the snapshot:
- `int ItemId`
- `string Name` (empty string initially, name resolution is a follow-up)
- `int Count`
- `string Source` (e.g. "Bank", "MaterialStorage", "SharedInventory", "Character:Zojja")

### `Models/SnapshotWalletEntry.cs`
- `int CurrencyId`
- `string CurrencyName`
- `int Value`

### `Models/AccountSnapshot.cs`
Top-level snapshot:
- `DateTime CapturedAt`
- `int CoinCopper` (from /v2/account `coins` field, NOT from wallet)
- `List<SnapshotItemEntry> Items`
- `List<SnapshotWalletEntry> Wallet` (from /v2/account/wallet, does NOT include coins)

All classes are plain POCOs with public properties for Newtonsoft.Json serialization.

---

## Step 3: Create Services (Services/ folder)

### `Services/SnapshotStore.cs`
Handles JSON read/write to the module's data directory.
- Constructor takes the data directory path (string)
- `AccountSnapshot LoadLatest()` - reads `snapshot.json`, returns null if missing/corrupt
- `void Save(AccountSnapshot snapshot)` - writes `snapshot.json`
- Uses `Newtonsoft.Json` (already referenced, available at runtime via Blish HUD)

### `Services/Gw2AccountSnapshotService.cs`
Fetches data from the GW2 API and assembles an `AccountSnapshot`.
- Constructor takes `Gw2ApiManager`
- `async Task<AccountSnapshot> FetchSnapshotAsync(CancellationToken ct)`
- Internally calls (with try/catch per endpoint for partial failure resilience):
  1. `Gw2ApiManager.Gw2ApiClient.V2.Account.GetAsync()` -> extract `coins` field for CoinCopper
  2. `Gw2ApiManager.Gw2ApiClient.V2.Account.Wallet.GetAsync()` -> wallet currency entries
  3. `Gw2ApiManager.Gw2ApiClient.V2.Account.Bank.GetAsync()` -> bank items (nullable entries = empty slots)
  4. `Gw2ApiManager.Gw2ApiClient.V2.Account.Inventory.GetAsync()` -> shared inventory
  5. `Gw2ApiManager.Gw2ApiClient.V2.Account.Materials.GetAsync()` -> material storage
  6. `Gw2ApiManager.Gw2ApiClient.V2.Characters.AllAsync()` to get character names, then for each: `Gw2ApiManager.Gw2ApiClient.V2.Characters[name].Inventory.GetAsync()` -> bags -> items
- Maps API DTOs to our model types
- Sets `Source` field appropriately for each item

---

## Step 4: Update Module.cs

- Add fields: `SnapshotStore _snapshotStore`, `Gw2AccountSnapshotService _snapshotService`, `AccountSnapshot _currentSnapshot`, `CancellationTokenSource _refreshCts`
- In `Initialize()`: create `_snapshotStore` using `DirectoriesManager.GetFullDirectoryPath("data")`
- In `LoadAsync()`:
  1. Load last cached snapshot via `_snapshotStore.LoadLatest()`
  2. Create `_snapshotService`
  3. Check permissions via `Gw2ApiManager.HasPermissions()`, if available fetch fresh snapshot
  4. Hook `Gw2ApiManager.SubtokenUpdated` to retry if token wasn't ready
- In `Update()`: check staleness (10min default), trigger auto-refresh if needed
- In `Unload()`: cancel any pending refresh, dispose resources
- Pass `_currentSnapshot` (and a refresh callback) to MainView

---

## Step 5: Update Views/MainView.cs

Rebuild MainView to display the snapshot data:
- Constructor takes `AccountSnapshot` and an `Action refreshCallback`
- `Build(Container buildPanel)`:
  1. **Header row**: Title label + "Refresh Now" `StandardButton`
  2. **Filter row**: `Dropdown` for Items/Wallet/All + `Checkbox` for "Aggregate" toggle
  3. **Coin display**: Shows CoinCopper formatted as `Xg Ys Zc`
  4. **Scrollable content**: `FlowPanel` with `CanScroll = true` containing:
     - Item rows: Label showing `[ItemId] Name x Count (Source)`
     - Wallet rows: Label showing `CurrencyName: Value`
  5. Refresh button calls the refresh callback
  6. Filter/aggregate changes rebuild the content panel

Coin formatting helper: `value / 10000` gold, `(value % 10000) / 100` silver, `value % 100` copper.

---

## Step 6: Register new files in .csproj

Add `<Compile Include>` entries for:
- `Models\AccountSnapshot.cs`
- `Models\SnapshotItemEntry.cs`
- `Models\SnapshotWalletEntry.cs`
- `Services\SnapshotStore.cs`
- `Services\Gw2AccountSnapshotService.cs`

---

## File Summary

| File | Action |
|------|--------|
| `manifest.json` | Add api_permissions + directories |
| `Models/AccountSnapshot.cs` | Create |
| `Models/SnapshotItemEntry.cs` | Create |
| `Models/SnapshotWalletEntry.cs` | Create |
| `Services/SnapshotStore.cs` | Create |
| `Services/Gw2AccountSnapshotService.cs` | Create |
| `Module.cs` | Modify - wire services, lifecycle |
| `Views/MainView.cs` | Modify - full UI rebuild |
| `ModuleTemplate.csproj` | Add Compile includes |

---

## Key Design Decisions
- **Newtonsoft.Json** for serialization (already a dependency, available at runtime)
- **Partial failure**: each API endpoint wrapped in try/catch, snapshot can be partial
- **No item name resolution in v1**: store ItemId, show ID in UI. Name resolution is a follow-up
- **Coins from /v2/account**: CoinCopper stored separately on AccountSnapshot, NOT as a wallet entry
- **Simple staleness check**: compare `_currentSnapshot.CapturedAt` against 10min threshold in `Update()`
- **View recreation**: ToggleWindow creates a new MainView each time with current snapshot data
