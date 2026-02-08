using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Modules.Managers;
using GW2CraftingHelper.Models;
using Gw2Sharp.WebApi.V2.Models;

namespace GW2CraftingHelper.Services {

    public class Gw2AccountSnapshotService {

        private static readonly Logger Logger = Logger.GetLogger<Gw2AccountSnapshotService>();

        private static readonly TokenPermission[] RequiredPermissions = {
            TokenPermission.Account,
            TokenPermission.Characters,
            TokenPermission.Inventories,
            TokenPermission.Wallet
        };

        private readonly Gw2ApiManager _apiManager;

        public Gw2AccountSnapshotService(Gw2ApiManager apiManager) {
            _apiManager = apiManager;
        }

        public bool HasRequiredPermissions() {
            return _apiManager.HasPermissions(RequiredPermissions);
        }

        public async Task<AccountSnapshot> FetchSnapshotAsync(CancellationToken ct) {
            var snapshot = new AccountSnapshot { CapturedAt = DateTime.UtcNow };

            // Wallet (also extracts coins as currency ID 1)
            try {
                var wallet = await _apiManager.Gw2ApiClient.V2.Account.Wallet.GetAsync(ct);
                foreach (var entry in wallet) {
                    if (entry.Id == 1) {
                        snapshot.CoinCopper = entry.Value;
                    } else {
                        snapshot.Wallet.Add(new SnapshotWalletEntry {
                            CurrencyId   = entry.Id,
                            CurrencyName = "",
                            Value        = entry.Value
                        });
                    }
                }
            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                Logger.Warn(ex, "Failed to fetch wallet");
            }

            ct.ThrowIfCancellationRequested();

            // Bank
            try {
                var bank = await _apiManager.Gw2ApiClient.V2.Account.Bank.GetAsync(ct);
                foreach (var item in bank) {
                    if (item == null) continue;
                    snapshot.Items.Add(new SnapshotItemEntry {
                        ItemId = item.Id,
                        Count  = item.Count,
                        Source = "Bank"
                    });
                }
            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                Logger.Warn(ex, "Failed to fetch bank");
            }

            ct.ThrowIfCancellationRequested();

            // Shared inventory
            try {
                var shared = await _apiManager.Gw2ApiClient.V2.Account.Inventory.GetAsync(ct);
                foreach (var item in shared) {
                    if (item == null) continue;
                    snapshot.Items.Add(new SnapshotItemEntry {
                        ItemId = item.Id,
                        Count  = item.Count,
                        Source = "SharedInventory"
                    });
                }
            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                Logger.Warn(ex, "Failed to fetch shared inventory");
            }

            ct.ThrowIfCancellationRequested();

            // Material storage
            try {
                var materials = await _apiManager.Gw2ApiClient.V2.Account.Materials.GetAsync(ct);
                foreach (var mat in materials) {
                    if (mat.Count <= 0) continue;
                    snapshot.Items.Add(new SnapshotItemEntry {
                        ItemId = mat.Id,
                        Count  = mat.Count,
                        Source = "MaterialStorage"
                    });
                }
            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                Logger.Warn(ex, "Failed to fetch material storage");
            }

            ct.ThrowIfCancellationRequested();

            // Character inventories
            try {
                var characterNames = await _apiManager.Gw2ApiClient.V2.Characters.IdsAsync(ct);
                foreach (var name in characterNames) {
                    ct.ThrowIfCancellationRequested();
                    try {
                        var inv = await _apiManager.Gw2ApiClient.V2.Characters[name].Inventory.GetAsync(ct);
                        if (inv?.Bags == null) continue;
                        foreach (var bag in inv.Bags) {
                            if (bag?.Inventory == null) continue;
                            foreach (var item in bag.Inventory) {
                                if (item == null) continue;
                                snapshot.Items.Add(new SnapshotItemEntry {
                                    ItemId = item.Id,
                                    Count  = item.Count,
                                    Source = "Character:" + name
                                });
                            }
                        }
                    } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                        Logger.Warn(ex, "Failed to fetch inventory for character {CharacterName}", name);
                    }
                }
            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                Logger.Warn(ex, "Failed to fetch character list");
            }

            return snapshot;
        }
    }

}
