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

            // Character inventories + crafting disciplines
            try
            {
                var characterNames = await _apiManager.Gw2ApiClient.V2.Characters.IdsAsync(ct);

                // W3C (per-character discipline display): non-null as soon
                // as the character list itself is obtained - see
                // AccountSnapshot.CharacterDisciplines' own doc comment for
                // why null vs. empty is a meaningful distinction here. May
                // still be reset to null below (see
                // characterDisciplineDataDegraded) if any single
                // character's crafting fetch fails - a partial list would
                // read as an affirmative "not trained on any character"
                // claim for a discipline this fetch simply never reached
                // (W3C review-fix, critical: violates "never invent data"
                // and the W3C spec's own "degraded fetch -> show nothing"
                // requirement).
                snapshot.CharacterDisciplines = new List<SnapshotCharacterDiscipline>();
                bool characterDisciplineDataDegraded = false;

                foreach (var name in characterNames)
                {
                    ct.ThrowIfCancellationRequested();

                    // W3C review-fix (mustFix): inventory and crafting-
                    // discipline are two independent round trips per
                    // character (previously sequential - one full await
                    // after the other), which doubled this feature's
                    // exposure to the hard SnapshotFetchTimeout budget
                    // (Module.cs's CancelAfter) for large accounts. Firing
                    // them concurrently restores the wall-clock cost to
                    // roughly one round trip per character. Each awaited
                    // task catches its own failures internally (see
                    // FetchCharacterInventoryItemsAsync/
                    // FetchCharacterCraftingAsync below), so Task.WhenAll
                    // never faults on a per-character failure - only a
                    // genuine cancellation propagates out of it.
                    var inventoryTask = FetchCharacterInventoryItemsAsync(name, ct);
                    var craftingTask = FetchCharacterCraftingAsync(name, ct);
                    await Task.WhenAll(inventoryTask, craftingTask);

                    // Inventory: a failure here is tolerated exactly like
                    // every other per-character inventory failure always
                    // has been - this character's items are simply missing
                    // from Items, a conservative under-count (inflates buy
                    // cost, never fabricates a claim). It does NOT set
                    // characterDisciplineDataDegraded, since inventory and
                    // discipline data are independent signals.
                    snapshot.Items.AddRange(inventoryTask.Result);

                    // Crafting disciplines (W3C): its own small, lean
                    // endpoint - needs only account+characters scopes (a
                    // strict subset of Inventory's account+characters+
                    // inventories above), both already covered by
                    // RequiredPermissions, so no new permission requirement.
                    // Unlike Inventory, ANY failure here (exception after
                    // the bounded retry inside FetchCharacterCraftingAsync,
                    // or a null response with no failure exception - both
                    // defensive, since IBlobClient<T>.GetAsync should throw
                    // rather than return null on failure) flips
                    // characterDisciplineDataDegraded so the WHOLE
                    // snapshot's CharacterDisciplines is discarded below,
                    // even the entries already gathered from other,
                    // successfully-fetched characters - see that flag's
                    // doc comment above for why a partial list is
                    // unacceptable here even though it is fine for
                    // Inventory.
                    var craftingOutcome = craftingTask.Result;
                    if (craftingOutcome.Degraded)
                    {
                        characterDisciplineDataDegraded = true;
                    }
                    else
                    {
                        snapshot.CharacterDisciplines.AddRange(craftingOutcome.Disciplines);
                    }
                }

                if (characterDisciplineDataDegraded)
                {
                    snapshot.CharacterDisciplines = null;
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Warn(ex, "Failed to fetch character list");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot-fetch", $"Failed to fetch character list: {ex.GetType().Name} - {ex.Message}");
                failedSources++;
                failedSourceExceptionTypeNames.Add(ex.GetType().Name);
                // Adversarial-review fix (Critical #8): if anything escapes
                // the per-character loop above (Task.WhenAll faulting,
                // inventoryTask.Result/craftingTask.Result rethrowing - the
                // loop's own doc comment that WhenAll "never faults on a
                // per-character failure" is not a guarantee this catch can
                // rely on), snapshot.CharacterDisciplines can already be a
                // PARTIALLY populated, non-null list at this point (some
                // characters' entries added before the failure). Before
                // this fix that partial list survived into the returned
                // snapshot and read as an affirmative "no character has
                // this discipline" for every character the loop never
                // reached - exactly the "never invent data" violation the
                // characterDisciplineDataDegraded machinery above exists to
                // prevent for a single character's failure. Null it here
                // too, matching that same "degraded fetch -> show nothing"
                // contract for a failure that escapes the loop entirely.
                snapshot.CharacterDisciplines = null;
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

        // Pre-W3C narrow per-character endpoint, unchanged behavior
        // (reverted from an earlier W3C-introduced full-record
        // V2.Characters[name].GetAsync, which pulled in learned recipes/
        // equipment/build-tab payloads never used here and widened this
        // cheap cosmetic feature's failure blast radius onto plan-affecting
        // owned-materials data - see docs/KNOWN-ISSUES.md's W3C section).
        // Never throws - a failure is tolerated exactly like every other
        // per-character inventory failure always has been, so the caller
        // can await this concurrently with FetchCharacterCraftingAsync via
        // Task.WhenAll without either one's failure short-circuiting the
        // other (W3C review-fix, mustFix: extracted from an inline
        // sequential await so the two per-character round trips run
        // concurrently instead).
        private async Task<List<SnapshotItemEntry>> FetchCharacterInventoryItemsAsync(string characterName, CancellationToken ct)
        {
            var items = new List<SnapshotItemEntry>();
            try
            {
                var inventory = await _apiManager.Gw2ApiClient.V2.Characters[characterName].Inventory.GetAsync(ct);
                if (inventory?.Bags != null)
                {
                    foreach (var bag in inventory.Bags)
                    {
                        if (bag?.Inventory == null) continue;
                        foreach (var item in bag.Inventory)
                        {
                            if (item == null) continue;
                            items.Add(new SnapshotItemEntry
                            {
                                ItemId = item.Id,
                                Count  = item.Count,
                                Source = AccountItemIndex.CharacterSourcePrefix + characterName
                            });
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Warn(ex, "Failed to fetch inventory for character {CharacterName}", characterName);
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot-fetch", $"Failed to fetch inventory for character {characterName}: {ex.GetType().Name} - {ex.Message}");
            }
            return items;
        }

        // W3C review-fix (mustFix): one bounded retry on top of the
        // existing single attempt - the all-or-nothing rule in
        // FetchSnapshotAsync (a single failed character's crafting fetch
        // discards CharacterDisciplines for the WHOLE account, see that
        // call site's own comment for why a partial list is unacceptable)
        // means a single transient 429/500 on just one character used to
        // silently wipe this feature for every character on every refresh.
        // Mirrors ItemMetadataService.GetMetadataAsync's own first-wave +
        // retry-wave pattern, including that pattern's lack of an
        // artificial delay between attempts. Never throws (except a
        // genuine cancellation) - the Degraded flag on the returned tuple
        // is how failure (including a defensive null payload with no
        // exception - IBlobClient<T>.GetAsync should throw rather than
        // return null on failure) is reported back to the caller.
        private async Task<(bool Degraded, List<SnapshotCharacterDiscipline> Disciplines)> FetchCharacterCraftingAsync(string characterName, CancellationToken ct)
        {
            var disciplines = new List<SnapshotCharacterDiscipline>();
            const int maxAttempts = 2;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var crafting = await _apiManager.Gw2ApiClient.V2.Characters[characterName].Crafting.GetAsync(ct);
                    if (crafting?.Crafting == null)
                    {
                        if (attempt < maxAttempts)
                        {
                            continue;
                        }
                        return (true, disciplines);
                    }

                    foreach (var cd in crafting.Crafting)
                    {
                        if (cd == null) continue;
                        disciplines.Add(new SnapshotCharacterDiscipline
                        {
                            CharacterName = characterName,
                            // RawValue (not ToEnumString()/Value): preserves
                            // the literal string the API returned even for
                            // a discipline Gw2Sharp's enum does not
                            // recognize, and matches the plain-string shape
                            // RequiredDiscipline.Discipline already uses
                            // (from Recipe.Disciplines) so the two can be
                            // compared directly.
                            Discipline = cd.Discipline?.RawValue ?? "",
                            Rating = cd.Rating,
                            Active = cd.Active
                        });
                    }
                    return (false, disciplines);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    if (attempt < maxAttempts)
                    {
                        continue;
                    }
                    Logger.Warn(ex, "Failed to fetch crafting disciplines for character {CharacterName}", characterName);
                    ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot-fetch", $"Failed to fetch crafting disciplines for character {characterName}: {ex.GetType().Name} - {ex.Message}");
                    return (true, disciplines);
                }
            }
            return (true, disciplines);
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
