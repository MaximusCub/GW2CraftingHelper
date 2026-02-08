# GW2 Crafting Helper

A [Blish HUD](https://blishhud.com/) module that caches and displays Guild Wars 2 account data including inventory, wallet, and material storage.

## Features

- Collects items from bank, shared inventory, material storage, and all character bags
- Displays wallet currencies and coin balance
- Filter view by Items, Wallet, or All
- Aggregate duplicate items across sources
- Auto-refreshes when data becomes stale

## API Permissions

This module requires the following GW2 API permissions:

- **account** — Account info and coin balance
- **characters** — Character list and inventory access
- **inventories** — Bank, shared inventory, and material storage
- **wallet** — Wallet currency data

## Building

```
dotnet build GW2CraftingHelper.csproj -p:Platform=x64
```

## Testing

```
dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj
```

## License

[MIT](LICENSE)
