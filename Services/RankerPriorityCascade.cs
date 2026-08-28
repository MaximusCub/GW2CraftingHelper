using System;
using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Walks the Crafting Ranker's priority list top to bottom, handing each
    /// slot only what the slots above it left behind.
    ///
    /// The contract is: call <see cref="CurrentAvailability"/> before solving
    /// slot n, pass its Snapshot to the pipeline's owned solve, then call
    /// <see cref="Consume"/> with that solve's result before moving to slot
    /// n+1. Nothing is re-solved: an earlier slot's plan is decided without
    /// reference to any later one, which is what the user's ordering means.
    ///
    /// The ledger tracks four kinds of claim, because "what a plan takes from
    /// you" is four different things:
    ///  - materials, from CraftingPlanResult.UsedMaterials (the solver's own
    ///    post-solve consumption record, which already reflects every
    ///    buy-vs-craft decision, including decisions caused by what was owned)
    ///  - currencies and coin, netted by this class, because the solver never
    ///    consults the wallet (see AccountCurrencyIndex)
    ///  - daily-cooldown crafting actions, which are capped per ACCOUNT, so
    ///    two items needing the same gated ingredient queue rather than run
    ///    in parallel
    ///
    /// Blish-free by construction.
    /// </summary>
    internal sealed class RankerPriorityCascade
    {
        private readonly AccountSnapshot _original;
        private readonly List<SnapshotItemEntry> _residualItems;
        private readonly Dictionary<int, List<SnapshotItemEntry>> _itemsById;
        private readonly Dictionary<int, int> _currency;
        private readonly Dictionary<int, int> _claimedGatedUnits = new Dictionary<int, int>();
        private readonly HashSet<int> _claimedItemIds = new HashSet<int>();
        private readonly HashSet<int> _claimedCurrencyIds = new HashSet<int>();

        private long _coinCopper;
        private bool _dirty = true;
        private AccountSnapshot _cachedSnapshot;

        /// <summary>A null snapshot leaves the cascade inert - every slot solves unreduced.</summary>
        public RankerPriorityCascade(AccountSnapshot snapshot)
        {
            _original = snapshot;

            if (snapshot == null)
            {
                _residualItems = new List<SnapshotItemEntry>();
                _itemsById = new Dictionary<int, List<SnapshotItemEntry>>();
                _currency = new Dictionary<int, int>();
                return;
            }

            _coinCopper = snapshot.CoinCopper;

            _residualItems = new List<SnapshotItemEntry>();
            _itemsById = new Dictionary<int, List<SnapshotItemEntry>>();
            if (snapshot.Items != null)
            {
                foreach (var entry in snapshot.Items)
                {
                    if (entry == null || entry.Count <= 0)
                    {
                        continue;
                    }

                    // Cloned: the cascade decrements Counts as it walks, and
                    // the caller's snapshot is shared with the Snapshot tab.
                    var clone = new SnapshotItemEntry
                    {
                        ItemId = entry.ItemId,
                        Name = entry.Name,
                        IconUrl = entry.IconUrl,
                        Count = entry.Count,
                        Source = entry.Source,
                    };
                    _residualItems.Add(clone);

                    if (!_itemsById.TryGetValue(clone.ItemId, out var bucket))
                    {
                        bucket = new List<SnapshotItemEntry>();
                        _itemsById[clone.ItemId] = bucket;
                    }

                    bucket.Add(clone);
                }
            }

            _currency = new Dictionary<int, int>();
            if (snapshot.Wallet != null)
            {
                foreach (var entry in snapshot.Wallet)
                {
                    if (entry == null || entry.Value <= 0)
                    {
                        continue;
                    }

                    _currency[entry.CurrencyId] = _currency.TryGetValue(entry.CurrencyId, out int existing)
                        ? existing + entry.Value
                        : entry.Value;
                }
            }
        }

        /// <summary>True when there was no snapshot to cascade at all.</summary>
        public bool HasSnapshot => _original != null;

        /// <summary>What the next unsolved slot may draw on. Never null.</summary>
        public RankerSlotAvailability CurrentAvailability
        {
            get
            {
                return new RankerSlotAvailability
                {
                    Snapshot = _original == null ? null : MaterializeSnapshot(),
                    CoinCopper = _original == null ? (int?)null : ClampToInt(_coinCopper),
                    Currency = new Dictionary<int, int>(_currency),
                    ClaimedGatedUnits = new Dictionary<int, int>(_claimedGatedUnits),
                    ClaimedItemIds = new HashSet<int>(_claimedItemIds),
                    ClaimedCurrencyIds = new HashSet<int>(_claimedCurrencyIds),
                };
            }
        }

        /// <summary>
        /// Folds one solved slot into the ledger. A null result is ignored
        /// rather than throwing: a slot whose solve failed must not silently
        /// consume the account, and must not abort the rest of the run.
        /// </summary>
        public void Consume(CraftingPlanResult owned)
        {
            if (owned == null || _original == null)
            {
                return;
            }

            ConsumeMaterials(owned.UsedMaterials);
            ConsumeCurrencies(owned.Plan?.CurrencyCosts);
            ConsumeCoin(owned.Plan?.TotalCoinCost ?? 0);
            ClaimGatedCrafts(owned);
            _dirty = true;
        }

        private void ConsumeMaterials(IReadOnlyList<UsedMaterial> usedMaterials)
        {
            if (usedMaterials == null)
            {
                return;
            }

            foreach (var used in usedMaterials)
            {
                if (used == null || used.QuantityUsed <= 0)
                {
                    continue;
                }

                if (!_itemsById.TryGetValue(used.ItemId, out var bucket))
                {
                    continue;
                }

                int remaining = used.QuantityUsed;

                // Source-accurate first: InventoryReducer records which
                // storage location it drew from, and the reducer is itself
                // source-aware (an account-bound stack on the wrong character
                // is not interchangeable with one in the bank).
                if (used.Sources != null)
                {
                    foreach (var allocation in used.Sources)
                    {
                        if (allocation == null || allocation.Quantity <= 0)
                        {
                            continue;
                        }

                        remaining -= TakeFrom(bucket, allocation.Source, allocation.Quantity);
                    }
                }

                // Whatever the per-source pass could not place (a legacy
                // result with no Sources list, or a source the snapshot no
                // longer holds) comes off any remaining stack, in list order.
                if (remaining > 0)
                {
                    TakeFrom(bucket, null, remaining);
                }

                _claimedItemIds.Add(used.ItemId);
            }
        }

        /// <summary>
        /// Decrements up to <paramref name="quantity"/> units from the bucket,
        /// restricted to <paramref name="source"/> when it is non-null.
        /// Returns how much was actually taken.
        /// </summary>
        private int TakeFrom(List<SnapshotItemEntry> bucket, string source, int quantity)
        {
            int taken = 0;
            foreach (var entry in bucket)
            {
                if (quantity <= 0)
                {
                    break;
                }

                if (entry.Count <= 0)
                {
                    continue;
                }

                if (source != null && !string.Equals(entry.Source, source, StringComparison.Ordinal))
                {
                    continue;
                }

                int take = Math.Min(entry.Count, quantity);
                entry.Count -= take;
                quantity -= take;
                taken += take;
            }

            return taken;
        }

        private void ConsumeCurrencies(IReadOnlyList<CurrencyCost> currencyCosts)
        {
            if (currencyCosts == null)
            {
                return;
            }

            foreach (var cost in currencyCosts)
            {
                if (cost == null || cost.Amount <= 0)
                {
                    continue;
                }

                if (!_currency.TryGetValue(cost.CurrencyId, out int held) || held <= 0)
                {
                    continue;
                }

                // You cannot spend what you do not have; the shortfall is
                // this slot's problem, not a debt carried to the next one.
                int spent = (int)Math.Min(held, cost.Amount);
                _currency[cost.CurrencyId] = held - spent;
                _claimedCurrencyIds.Add(cost.CurrencyId);
            }
        }

        private void ConsumeCoin(long coinCost)
        {
            if (coinCost <= 0)
            {
                return;
            }

            _coinCopper = Math.Max(0, _coinCopper - coinCost);
        }

        private void ClaimGatedCrafts(CraftingPlanResult owned)
        {
            var cooldowns = owned.DailyCooldownItems;
            var steps = owned.Plan?.Steps;
            if (cooldowns == null || cooldowns.Count == 0 || steps == null)
            {
                return;
            }

            foreach (var step in steps)
            {
                if (step == null || step.Source != AcquisitionSource.Craft || step.Quantity <= 0)
                {
                    continue;
                }

                if (!cooldowns.TryGetValue(step.ItemId, out var cooldown) || cooldown == null || cooldown.PerDayCap <= 0)
                {
                    continue;
                }

                _claimedGatedUnits[step.ItemId] = _claimedGatedUnits.TryGetValue(step.ItemId, out int existing)
                    ? existing + step.Quantity
                    : step.Quantity;
            }
        }

        private static int ClampToInt(long value)
        {
            if (value <= 0)
            {
                return 0;
            }

            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private AccountSnapshot MaterializeSnapshot()
        {
            if (!_dirty && _cachedSnapshot != null)
            {
                return _cachedSnapshot;
            }

            var items = new List<SnapshotItemEntry>(_residualItems.Count);
            foreach (var entry in _residualItems)
            {
                if (entry.Count > 0)
                {
                    items.Add(entry);
                }
            }

            // One row per currency id, carrying the whole residual amount.
            // The source snapshot may split a currency across rows (the
            // ledger already summed them), so emitting per source row would
            // multiply the residual.
            var wallet = new List<SnapshotWalletEntry>();
            var emitted = new HashSet<int>();
            if (_original.Wallet != null)
            {
                foreach (var entry in _original.Wallet)
                {
                    if (entry == null || !emitted.Add(entry.CurrencyId))
                    {
                        continue;
                    }

                    if (!_currency.TryGetValue(entry.CurrencyId, out int remaining) || remaining <= 0)
                    {
                        continue;
                    }

                    wallet.Add(new SnapshotWalletEntry
                    {
                        CurrencyId = entry.CurrencyId,
                        CurrencyName = entry.CurrencyName,
                        IconUrl = entry.IconUrl,
                        Value = remaining,
                    });
                }
            }

            _cachedSnapshot = new AccountSnapshot
            {
                CapturedAt = _original.CapturedAt,
                CoinCopper = ClampToInt(_coinCopper),
                Items = items,
                Wallet = wallet,
                CharacterDisciplines = _original.CharacterDisciplines,
            };
            _dirty = false;
            return _cachedSnapshot;
        }
    }
}
