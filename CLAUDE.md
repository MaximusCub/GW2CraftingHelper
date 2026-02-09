# GW2CraftingHelper — Project Rules

## Build & Test

- Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64`
- Tests: `dotnet test tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`
- .csproj uses explicit `<Compile Include>` — new .cs files must be registered
- Changes must be incremental with logical git commits

## Code Style

- Use Allman brace style for C#

## Repo Invariants

These rules MUST always be followed. They override any conflicting defaults.

### Testing

- Tests must exercise real production code paths — no contract-mirror or fake logic tests
- Tests must NEVER reference Blish HUD, Blish HUD.exe, Gw2Sharp, or any UI code; test non-UI logic only
- No fake file I/O tests — use real SnapshotStore / StatusStore with temp directories

### UI & Display

- Item, currency, and vendor IDs are internal-only — never display them to users
- Coin icons MUST appear to the RIGHT of the number (matching GW2 in-game style):
  `123[gold icon] 45[silver icon] 67[copper icon]`
  This applies everywhere coin amounts are shown: coin panel, tooltips, item values, vendor prices, etc.
- GW2 coin asset IDs: Gold = 156904, Silver = 156907, Copper = 156902

### Data & APIs

- Prefer official GW2 APIs (api.guildwars2.com); do not invent data when APIs are missing
- gw2efficiency is research-only — the module must NEVER call it at runtime
- Pricing must preserve multiple sources and avoid invalid currency comparisons
