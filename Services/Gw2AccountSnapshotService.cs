using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Modules.Managers;
using GW2CraftingHelper.Models;
using Gw2Sharp.WebApi.V2.Models;

namespace GW2CraftingHelper.Services
{
    public class Gw2AccountSnapshotService
    {
        private static readonly Logger Logger = Logger.GetLogger<Gw2AccountSnapshotService>();

        private static readonly TokenPermission[] RequiredPermissions =
        {
            TokenPermission.Account,
            TokenPermission.Characters,
            TokenPermission.Inventories,
            TokenPermission.Wallet
        };

        private const int ItemBulkLimit = 200;

        private readonly Gw2ApiManager _apiManager;
        private readonly Dictionary<int, (string Name, string IconUrl)> _itemCache = new Dictionary<int, (string, string)>();
        private readonly Dictionary<int, (string Name, string IconUrl)> _currencyCache = new Dictionary<int, (string, string)>();
        private readonly object _cacheLock = new object();

        public Gw2AccountSnapshotService(Gw2ApiManager apiManager)
        {
            _apiManager = apiManager;
        }

        public bool HasRequiredPermissions()
        {
            return _apiManager.HasPermissions(RequiredPermissions);
        }

        // The 5 independent top-level account-data sources tallied below for
        // success/failure (KNOWN-ISSUES 31/api-degradation F1). Per-character
        // inventory failures are NOT counted individually here - they are
        // already tolerated as a partial-Characters-source degradation by
        // the inner try/catch around each character's own inventory fetch.
        private const int SourceCount = 5;

        public async Task<AccountSnapshot> FetchSnapshotAsync(CancellationToken ct)
        {
            var snapshot = new AccountSnapshot { CapturedAt = DateTime.UtcNow };
            int failedSources = 0;

            // Per-source failure type names for SnapshotFailureClassifier
            // (KNOWN-ISSUES api-degradation F1 follow-up, field-tested
            // 2026-08-06): captured here, where real Gw2Sharp exception
            // types are in scope, as plain type-name strings so the
            // Blish-free classifier and SnapshotFetchFailedException never
            // need a Gw2Sharp reference of their own - see
            // SnapshotFailureClassifier's class doc comment.
            var failedSourceExceptionTypeNames = new List<string>();

            // Wallet (also extracts coins as currency ID 1)
            try
            {
                var wallet = await _apiManager.Gw2ApiClient.V2.Account.Wallet.GetAsync(ct);
                foreach (var entry in wallet)
                {
                    if (entry.Id == 1)
                    {
                        snapshot.CoinCopper = entry.Value;
                    }
                    else
                    {
                        snapshot.Wallet.Add(new SnapshotWalletEntry
                        {
                            CurrencyId   = entry.Id,
                            CurrencyName = "",
                            Value        = entry.Value
                        });
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Warn(ex, "Failed to fetch wallet");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot-fetch", $"Failed to fetch wallet: {ex.GetType().Name} - {ex.Message}");
                failedSources++;
                failedSourceExceptionTypeNames.Add(ex.GetType().Name);
            }

            ct.ThrowIfCancellationRequested();

            // Bank
            try
            {
                var bank = await _apiManager.Gw2ApiClient.V2.Account.Bank.GetAsync(ct);
                foreach (var item in bank)
                {
                    if (item == null) continue;
                    snapshot.Items.Add(new SnapshotItemEntry
                    {
                        ItemId = item.Id,
                        Count  = item.Count,
                        Source = "Bank"
                    });
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Warn(ex, "Failed to fetch bank");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot-fetch", $"Failed to fetch bank: {ex.GetType().Name} - {ex.Message}");
                failedSources++;
                failedSourceExceptionTypeNames.Add(ex.GetType().Name);
            }

            ct.ThrowIfCancellationRequested();

            // Shared inventory
            try
            {
                var shared = await _apiManager.Gw2ApiClient.V2.Account.Inventory.GetAsync(ct);
                foreach (var item in shared)
                {
                    if (item == null) continue;
                    snapshot.Items.Add(new SnapshotItemEntry
                    {
                        ItemId = item.Id,
                        Count  = item.Count,
                        Source = "SharedInventory"
                    });
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Warn(ex, "Failed to fetch shared inventory");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot-fetch", $"Failed to fetch shared inventory: {ex.GetType().Name} - {ex.Message}");
                failedSources++;
                failedSourceExceptionTypeNames.Add(ex.GetType().Name);
            }

            ct.ThrowIfCancellationRequested();

            // Material storage
            try
            {
                var materials = await _apiManager.Gw2ApiClient.V2.Account.Materials.GetAsync(ct);
                foreach (var mat in materials)
                {
                    if (mat.Count <= 0) continue;
                    snapshot.Items.Add(new SnapshotItemEntry
                    {
                        ItemId = mat.Id,
                        Count  = mat.Count,
                        Source = "MaterialStorage"
                    });
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Warn(ex, "Failed to fetch material storage");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot-fetch", $"Failed to fetch material storage: {ex.GetType().Name} - {ex.Message}");
                failedSources++;
                failedSourceExceptionTypeNames.Add(ex.GetType().Name);
            }

            ct.ThrowIfCancellationRequested();

            // Character inventories
            try
            {
                var characterNames = await _apiManager.Gw2ApiClient.V2.Characters.IdsAsync(ct);

                // W3C (per-character discipline display): non-null as soon
                // as the character list itself is obtained, even if it
                // turns out empty or every character below fails - see
                // AccountSnapshot.CharacterDisciplines' own doc comment for
                // why null vs. empty is a meaningful distinction here.
                snapshot.CharacterDisciplines = new List<SnapshotCharacterDiscipline>();

                foreach (var name in characterNames)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        // W3C: fetches the character's FULL record
                        // (V2.Characters[name].GetAsync) rather than the
                        // narrower .Inventory sub-endpoint this call used
                        // pre-W3C. The full record's .Bags is byte-
                        // identical in shape to the narrower endpoint's own
                        // .Bags (both IReadOnlyList<CharacterInventoryBag>,
                        // confirmed via Gw2Sharp 1.7.4 reflection), and it
                        // additionally carries .Crafting - the per-character
                        // discipline data this package adds (see
                        // AccountSnapshot.CharacterDisciplines) - so one
                        // round trip now captures what previously took two.
                        // Same scopes as before (account, characters, plus
                        // inventories for a non-null Bags) - no new
                        // permission requirement. Inventory and crafting
                        // share this one try/catch: a failure here degrades
                        // BOTH signals for this one character, same as the
                        // pre-W3C behavior degraded inventory alone - it
                        // still never fails the whole snapshot.
                        var character = await _apiManager.Gw2ApiClient.V2.Characters[name].GetAsync(ct);

                        if (character?.Bags != null)
                        {
                            foreach (var bag in character.Bags)
                            {
                                if (bag?.Inventory == null) continue;
                                foreach (var item in bag.Inventory)
                                {
                                    if (item == null) continue;
                                    snapshot.Items.Add(new SnapshotItemEntry
                                    {
                                        ItemId = item.Id,
                                        Count  = item.Count,
                                        Source = AccountItemIndex.CharacterSourcePrefix + name
                                    });
                                }
                            }
                        }

                        if (character?.Crafting != null)
                        {
                            foreach (var cd in character.Crafting)
                            {
                                if (cd == null) continue;
                                snapshot.CharacterDisciplines.Add(new SnapshotCharacterDiscipline
                                {
                                    CharacterName = name,
                                    // RawValue (not ToEnumString()/Value):
                                    // preserves the literal string the API
                                    // returned even for a discipline
                                    // Gw2Sharp's enum does not recognize,
                                    // and matches the plain-string shape
                                    // RequiredDiscipline.Discipline already
                                    // uses (from Recipe.Disciplines) so the
                                    // two can be compared directly.
                                    Discipline = cd.Discipline?.RawValue ?? "",
                                    Rating = cd.Rating,
                                    Active = cd.Active
                                });
                            }
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        Logger.Warn(ex, "Failed to fetch data for character {CharacterName}", name);
                        ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot-fetch", $"Failed to fetch data for character {name}: {ex.GetType().Name} - {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Warn(ex, "Failed to fetch character list");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot-fetch", $"Failed to fetch character list: {ex.GetType().Name} - {ex.Message}");
                failedSources++;
                failedSourceExceptionTypeNames.Add(ex.GetType().Name);
            }

            // A partial or total failure must never silently masquerade as a
            // full snapshot (KNOWN-ISSUES 31/api-degradation F1): throw
            // instead of returning a snapshot with holes relative to what a
            // prior good fetch may already have on disk/in memory. See
            // SnapshotFetchFailedException's doc comment for the full
            // conservative-persistence-rule rationale.
            if (failedSources > 0)
            {
                throw new SnapshotFetchFailedException(failedSources, SourceCount, failedSourceExceptionTypeNames);
            }

            // Resolve display names and icon URLs
            await ResolveItemDetailsAsync(snapshot.Items, ct);
            await ResolveCurrencyDetailsAsync(snapshot.Wallet, ct);

            return snapshot;
        }

        private async Task ResolveItemDetailsAsync(List<SnapshotItemEntry> items, CancellationToken ct)
        {
            try
            {
                List<int> uncachedIds;
                lock (_cacheLock)
                {
                    uncachedIds = items
                        .Select(i => i.ItemId)
                        .Distinct()
                        .Where(id => !_itemCache.ContainsKey(id))
                        .ToList();
                }

                for (int i = 0; i < uncachedIds.Count; i += ItemBulkLimit)
                {
                    ct.ThrowIfCancellationRequested();
                    var chunk = uncachedIds.Skip(i).Take(ItemBulkLimit);
                    var fetched = await _apiManager.Gw2ApiClient.V2.Items.ManyAsync(chunk, ct);
                    lock (_cacheLock)
                    {
                        foreach (var item in fetched)
                        {
                            var url = item.Icon.Url;
                            _itemCache[item.Id] = (item.Name ?? "", url != null ? url.AbsoluteUri : "");
                        }
                    }
                }

                lock (_cacheLock)
                {
                    foreach (var entry in items)
                    {
                        if (_itemCache.TryGetValue(entry.ItemId, out var cached))
                        {
                            entry.Name = cached.Name;
                            entry.IconUrl = cached.IconUrl;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Warn(ex, "Failed to resolve item names/icons");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot-fetch", $"Failed to resolve item names/icons: {ex.GetType().Name} - {ex.Message}");
            }
        }

        private async Task ResolveCurrencyDetailsAsync(List<SnapshotWalletEntry> wallet, CancellationToken ct)
        {
            try
            {
                bool needsFetch;
                lock (_cacheLock)
                {
                    needsFetch = _currencyCache.Count == 0;
                }

                if (needsFetch)
                {
                    ct.ThrowIfCancellationRequested();
                    var currencies = await _apiManager.Gw2ApiClient.V2.Currencies.AllAsync(ct);
                    lock (_cacheLock)
                    {
                        foreach (var c in currencies)
                        {
                            var url = c.Icon.Url;
                            _currencyCache[c.Id] = (c.Name ?? "", url != null ? url.AbsoluteUri : "");
                        }
                    }
                }

                lock (_cacheLock)
                {
                    foreach (var entry in wallet)
                    {
                        if (_currencyCache.TryGetValue(entry.CurrencyId, out var cached))
                        {
                            entry.CurrencyName = cached.Name;
                            entry.IconUrl = cached.IconUrl;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Warn(ex, "Failed to resolve currency names/icons");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot-fetch", $"Failed to resolve currency names/icons: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
