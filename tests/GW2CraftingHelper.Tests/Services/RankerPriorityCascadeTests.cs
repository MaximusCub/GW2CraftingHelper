using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class RankerPriorityCascadeTests
    {
        private const string Storage = AccountItemIndex.SourceMaterialStorage;
        private const string Bank = AccountItemIndex.SourceBank;
        private const int MithrilliumId = 46742;

        private static AccountSnapshot Snapshot(
            int coin = 0,
            List<SnapshotItemEntry> items = null,
            List<SnapshotWalletEntry> wallet = null)
        {
            return new AccountSnapshot
            {
                CoinCopper = coin,
                Items = items ?? new List<SnapshotItemEntry>(),
                Wallet = wallet ?? new List<SnapshotWalletEntry>(),
            };
        }

        private static SnapshotItemEntry Item(int itemId, int count, string source = Storage)
        {
            return new SnapshotItemEntry { ItemId = itemId, Count = count, Source = source, Name = "Item " + itemId };
        }

        private static CraftingPlanResult Consumed(
            long coin = 0,
            List<UsedMaterial> used = null,
            List<CurrencyCost> currencies = null,
            List<PlanStep> steps = null,
            Dictionary<int, DailyCooldownItem> cooldowns = null)
        {
            var result = CraftingPlanResultBuilders.MakeResult(
                totalCoinCost: coin,
                currencyCosts: currencies,
                steps: steps,
                usedMaterials: used,
                dailyCooldownItems: cooldowns);
            return result;
        }

        private static int ResidualCount(RankerSlotAvailability availability, int itemId)
        {
            return availability.Snapshot.Items.Where(i => i.ItemId == itemId).Sum(i => i.Count);
        }

        [Fact]
        public void WithNoSnapshot_TheCascadeIsInertAndEverySlotSolvesUnreduced()
        {
            var cascade = new RankerPriorityCascade(null);

            Assert.False(cascade.HasSnapshot);
            var availability = cascade.CurrentAvailability;
            Assert.Null(availability.Snapshot);
            Assert.Null(availability.CoinCopper);
            Assert.Empty(availability.Currency);

            cascade.Consume(Consumed(coin: 9999, used: new List<UsedMaterial>
            {
                new UsedMaterial { ItemId = 5, QuantityUsed = 3 },
            }));

            Assert.Null(cascade.CurrentAvailability.Snapshot);
            Assert.Null(cascade.CurrentAvailability.CoinCopper);
        }

        [Fact]
        public void TheFirstSlotSeesTheUntouchedAccount()
        {
            var cascade = new RankerPriorityCascade(Snapshot(
                coin: 5000,
                items: new List<SnapshotItemEntry> { Item(2, 40) },
                wallet: new List<SnapshotWalletEntry> { new SnapshotWalletEntry { CurrencyId = 29, Value = 300 } }));

            var availability = cascade.CurrentAvailability;

            Assert.Equal(5000, availability.CoinCopper);
            Assert.Equal(40, ResidualCount(availability, 2));
            Assert.Equal(300, availability.Currency[29]);
            Assert.Empty(availability.ClaimedItemIds);
            Assert.Empty(availability.ClaimedGatedUnits);
        }

        [Fact]
        public void ConsumptionComesFromUsedMaterials_NotTheShoppingList()
        {
            // UsedMaterials is the solver's own post-solve record, so a plan
            // that BOUGHT an intermediate never shows its ingredients here -
            // which is the whole reason the leaf shopping list is the wrong
            // consumption record.
            var cascade = new RankerPriorityCascade(Snapshot(
                items: new List<SnapshotItemEntry> { Item(2, 40), Item(3, 10) }));

            cascade.Consume(Consumed(
                used: new List<UsedMaterial> { new UsedMaterial { ItemId = 2, QuantityUsed = 25 } },
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 3, Quantity = 10, Source = AcquisitionSource.BuyFromTp },
                }));

            var availability = cascade.CurrentAvailability;
            Assert.Equal(15, ResidualCount(availability, 2));
            Assert.Equal(10, ResidualCount(availability, 3));
            Assert.Contains(2, availability.ClaimedItemIds);
            Assert.DoesNotContain(3, availability.ClaimedItemIds);
        }

        [Fact]
        public void MaterialsAreTakenFromTheStorageLocationTheReducerNamed()
        {
            var cascade = new RankerPriorityCascade(Snapshot(items: new List<SnapshotItemEntry>
            {
                Item(2, 10, Storage),
                Item(2, 10, Bank),
            }));

            cascade.Consume(Consumed(used: new List<UsedMaterial>
            {
                new UsedMaterial
                {
                    ItemId = 2,
                    QuantityUsed = 6,
                    Sources = new List<MaterialSourceAllocation>
                    {
                        new MaterialSourceAllocation { Source = Bank, Quantity = 6 },
                    },
                },
            }));

            var items = cascade.CurrentAvailability.Snapshot.Items;
            Assert.Equal(10, items.Single(i => i.Source == Storage).Count);
            Assert.Equal(4, items.Single(i => i.Source == Bank).Count);
        }

        [Fact]
        public void AUsedMaterialWithNoSourceBreakdown_StillComesOffTheResidual()
        {
            var cascade = new RankerPriorityCascade(Snapshot(items: new List<SnapshotItemEntry>
            {
                Item(2, 10, Storage),
                Item(2, 10, Bank),
            }));

            cascade.Consume(Consumed(used: new List<UsedMaterial>
            {
                new UsedMaterial { ItemId = 2, QuantityUsed = 14, Sources = null },
            }));

            Assert.Equal(6, ResidualCount(cascade.CurrentAvailability, 2));
        }

        [Fact]
        public void AnAllocationNamingASourceTheSnapshotNoLongerHolds_SpillsOntoWhatIsLeft()
        {
            var cascade = new RankerPriorityCascade(Snapshot(items: new List<SnapshotItemEntry>
            {
                Item(2, 10, Storage),
            }));

            cascade.Consume(Consumed(used: new List<UsedMaterial>
            {
                new UsedMaterial
                {
                    ItemId = 2,
                    QuantityUsed = 4,
                    Sources = new List<MaterialSourceAllocation>
                    {
                        new MaterialSourceAllocation { Source = "Character:Ghost", Quantity = 4 },
                    },
                },
            }));

            Assert.Equal(6, ResidualCount(cascade.CurrentAvailability, 2));
        }

        [Fact]
        public void AnExhaustedStackIsDroppedFromTheResidualSnapshotEntirely()
        {
            var cascade = new RankerPriorityCascade(Snapshot(items: new List<SnapshotItemEntry>
            {
                Item(2, 5),
            }));

            cascade.Consume(Consumed(used: new List<UsedMaterial>
            {
                new UsedMaterial { ItemId = 2, QuantityUsed = 5 },
            }));

            Assert.Empty(cascade.CurrentAvailability.Snapshot.Items);
        }

        [Fact]
        public void TheCascadeNeverMutatesTheCallersSnapshot()
        {
            var original = Snapshot(coin: 100, items: new List<SnapshotItemEntry> { Item(2, 40) });
            var cascade = new RankerPriorityCascade(original);

            cascade.Consume(Consumed(coin: 60, used: new List<UsedMaterial>
            {
                new UsedMaterial { ItemId = 2, QuantityUsed = 30 },
            }));

            Assert.Equal(40, original.Items.Single().Count);
            Assert.Equal(100, original.CoinCopper);
        }

        [Fact]
        public void CurrenciesAreNettedByTheCascadeBecauseTheSolverNeverConsultsTheWallet()
        {
            var cascade = new RankerPriorityCascade(Snapshot(wallet: new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 29, Value = 300 },
            }));

            cascade.Consume(Consumed(currencies: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 29, Amount = 120 },
            }));

            var availability = cascade.CurrentAvailability;
            Assert.Equal(180, availability.Currency[29]);
            Assert.Contains(29, availability.ClaimedCurrencyIds);
            Assert.Equal(180, availability.Snapshot.Wallet.Single(w => w.CurrencyId == 29).Value);
        }

        [Fact]
        public void YouCannotSpendCurrencyYouDoNotHave_TheShortfallIsNotCarriedForward()
        {
            var cascade = new RankerPriorityCascade(Snapshot(wallet: new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 29, Value = 50 },
            }));

            cascade.Consume(Consumed(currencies: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 29, Amount = 500 },
            }));
            cascade.Consume(Consumed(currencies: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 29, Amount = 500 },
            }));

            Assert.Equal(0, cascade.CurrentAvailability.Currency[29]);
        }

        [Fact]
        public void ACurrencySplitAcrossWalletRowsIsEmittedOnceAtItsResidualAmount()
        {
            var cascade = new RankerPriorityCascade(Snapshot(wallet: new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 29, Value = 100 },
                new SnapshotWalletEntry { CurrencyId = 29, Value = 200 },
            }));

            cascade.Consume(Consumed(currencies: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 29, Amount = 50 },
            }));

            var wallet = cascade.CurrentAvailability.Snapshot.Wallet;
            Assert.Single(wallet);
            Assert.Equal(250, wallet[0].Value);
        }

        [Fact]
        public void CoinIsDrainedBySlotsAboveAndFloorsAtZero()
        {
            var cascade = new RankerPriorityCascade(Snapshot(coin: 1000));

            cascade.Consume(Consumed(coin: 400));
            Assert.Equal(600, cascade.CurrentAvailability.CoinCopper);

            cascade.Consume(Consumed(coin: 900));
            Assert.Equal(0, cascade.CurrentAvailability.CoinCopper);
        }

        [Fact]
        public void DailyGatedCraftsAccumulateAcrossSlotsBecauseTheCapIsPerAccount()
        {
            var cooldowns = new Dictionary<int, DailyCooldownItem>
            {
                { MithrilliumId, new DailyCooldownItem { ItemId = MithrilliumId, PerDayCap = 1 } },
            };
            var cascade = new RankerPriorityCascade(Snapshot());

            cascade.Consume(Consumed(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = MithrilliumId, Quantity = 30, Source = AcquisitionSource.Craft },
            }, cooldowns: cooldowns));

            cascade.Consume(Consumed(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = MithrilliumId, Quantity = 20, Source = AcquisitionSource.Craft },
            }, cooldowns: cooldowns));

            Assert.Equal(50, cascade.CurrentAvailability.ClaimedGatedUnits[MithrilliumId]);
        }

        [Fact]
        public void OnlyCraftStepsClaimADailyGate()
        {
            var cooldowns = new Dictionary<int, DailyCooldownItem>
            {
                { MithrilliumId, new DailyCooldownItem { ItemId = MithrilliumId, PerDayCap = 1 } },
            };
            var cascade = new RankerPriorityCascade(Snapshot());

            cascade.Consume(Consumed(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = MithrilliumId, Quantity = 30, Source = AcquisitionSource.BuyFromTp },
            }, cooldowns: cooldowns));

            Assert.Empty(cascade.CurrentAvailability.ClaimedGatedUnits);
        }

        [Fact]
        public void ANullOrEmptyResultIsIgnoredRatherThanConsumingTheAccount()
        {
            var cascade = new RankerPriorityCascade(Snapshot(
                coin: 500, items: new List<SnapshotItemEntry> { Item(2, 40) }));

            cascade.Consume(null);
            cascade.Consume(new CraftingPlanResult());

            Assert.Equal(500, cascade.CurrentAvailability.CoinCopper);
            Assert.Equal(40, ResidualCount(cascade.CurrentAvailability, 2));
        }

        [Fact]
        public void CharacterDisciplinesArePassedThroughUnchangedToEverySlot()
        {
            var snapshot = Snapshot(items: new List<SnapshotItemEntry> { Item(2, 5) });
            snapshot.CharacterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Kara", Discipline = "Huntsman", Rating = 500 },
            };
            var cascade = new RankerPriorityCascade(snapshot);

            cascade.Consume(Consumed(used: new List<UsedMaterial>
            {
                new UsedMaterial { ItemId = 2, QuantityUsed = 5 },
            }));

            var disciplines = cascade.CurrentAvailability.Snapshot.CharacterDisciplines;
            Assert.Single(disciplines);
            Assert.Equal("Kara", disciplines[0].CharacterName);
        }

        [Fact]
        public void AvailabilitySnapshotsAreIndependentOfLaterConsumption()
        {
            // The refresh loop reads CurrentAvailability, then awaits two
            // solves before consuming. A later Consume must not retroactively
            // change the availability an in-flight solve was handed.
            var cascade = new RankerPriorityCascade(Snapshot(
                coin: 1000, items: new List<SnapshotItemEntry> { Item(2, 40) }));

            var first = cascade.CurrentAvailability;
            cascade.Consume(Consumed(coin: 300, used: new List<UsedMaterial>
            {
                new UsedMaterial { ItemId = 2, QuantityUsed = 30 },
            }));

            Assert.Equal(1000, first.CoinCopper);
            Assert.Equal(700, cascade.CurrentAvailability.CoinCopper);
            Assert.Equal(10, ResidualCount(cascade.CurrentAvailability, 2));
        }
    }
}
