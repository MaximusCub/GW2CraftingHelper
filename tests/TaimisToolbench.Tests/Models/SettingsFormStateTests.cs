using System;
using System.Collections.Generic;
using TaimisToolbench.Models;
using Xunit;

namespace TaimisToolbench.Tests.Models
{
    public class SettingsFormStateTests
    {
        // The curated currency list the Settings tab renders is 47 rows
        // wide; the shape of the capture, not the exact membership, is
        // what these tests pin.
        private const int CurrencyRowCount = 47;

        private static readonly int[] HomesteadItemIds = { 97102, 96979, 97169 };

        /// <summary>
        /// Every save-gated field the Settings tab owns, in the shape
        /// SettingsTabContent.CaptureFormState builds it: one amount and
        /// one Ignore flag per currency row, one tier per Homestead row,
        /// two logging fields and the snapshot interval.
        /// </summary>
        private static SettingsFormState BuildFullState(
            Func<int, string> currencyAmount = null,
            Func<int, bool> currencyIgnored = null,
            Func<int, string> homesteadTier = null,
            string logMaxSizeMb = "2",
            string logRetentionDays = "30",
            string snapshotInterval = "15")
        {
            var state = new SettingsFormState();

            for (int i = 0; i < CurrencyRowCount; i++)
            {
                int currencyId = i + 1;
                state.AddText(
                    SettingsFormState.CurrencyAmountKey(currencyId),
                    currencyAmount == null ? "" : currencyAmount(currencyId));
                state.AddFlag(
                    SettingsFormState.CurrencyIgnoreKey(currencyId),
                    currencyIgnored != null && currencyIgnored(currencyId));
            }

            foreach (int itemId in HomesteadItemIds)
            {
                state.AddText(
                    SettingsFormState.HomesteadTierKey(itemId),
                    homesteadTier == null ? "0" : homesteadTier(itemId));
            }

            state.AddText(SettingsFormState.LogMaxSizeMbKey, logMaxSizeMb);
            state.AddText(SettingsFormState.LogRetentionDaysKey, logRetentionDays);
            state.AddText(SettingsFormState.SnapshotRefreshIntervalMinutesKey, snapshotInterval);

            return state;
        }

        [Fact]
        public void BarterItemKeys_NeverCollideWithCurrencyKeysOnTheSameNumber()
        {
            // An item id and a currency id are different id spaces that
            // collide numerically. Sharing a key would collapse the two
            // rows into one comparison and silently stop reporting edits to
            // whichever lost - AddField rejects a duplicate rather than
            // overwriting, so this would be a hard failure at capture time.
            var state = new SettingsFormState();

            state.AddText(SettingsFormState.CurrencyAmountKey(39), "3600");
            state.AddFlag(SettingsFormState.CurrencyIgnoreKey(39), false);
            state.AddText(SettingsFormState.BarterItemAmountKey(39), "7");
            state.AddFlag(SettingsFormState.BarterItemIgnoreKey(39), true);

            Assert.Equal(4, state.FieldCount);
        }

        [Fact]
        public void BarterItemAmount_EditIsReportedAsAChange()
        {
            var baseline = new SettingsFormState();
            baseline.AddText(SettingsFormState.BarterItemAmountKey(19925), "667");

            var edited = new SettingsFormState();
            edited.AddText(SettingsFormState.BarterItemAmountKey(19925), "900");

            Assert.Contains(SettingsFormState.BarterItemAmountKey(19925), edited.ChangedKeys(baseline));
        }

        [Fact]
        public void FullState_CoversEveryCurrencyRowPlusIgnoreFlagAndTheOtherSections()
        {
            var state = BuildFullState();

            Assert.Equal((CurrencyRowCount * 2) + HomesteadItemIds.Length + 3, state.FieldCount);
        }

        [Fact]
        public void IdenticalCapture_IsNotDirty()
        {
            var baseline = BuildFullState();
            var current = BuildFullState();

            Assert.False(current.IsDirtyAgainst(baseline));
            Assert.Empty(current.ChangedKeys(baseline));
        }

        [Fact]
        public void EditedCurrencyAmount_IsDirtyOnThatRowOnly()
        {
            var baseline = BuildFullState();
            var current = BuildFullState(currencyAmount: id => id == 23 ? "1200" : "");

            Assert.True(current.IsDirtyAgainst(baseline));
            Assert.Equal(
                new[] { SettingsFormState.CurrencyAmountKey(23) },
                current.ChangedKeys(baseline));
        }

        [Fact]
        public void TypedThenRevertedToOriginalValue_IsNotDirty()
        {
            var baseline = BuildFullState(currencyAmount: id => id == 23 ? "1200" : "");

            var typed = BuildFullState(currencyAmount: id => id == 23 ? "12005" : "");
            Assert.True(typed.IsDirtyAgainst(baseline));

            var reverted = BuildFullState(currencyAmount: id => id == 23 ? "1200" : "");
            Assert.False(reverted.IsDirtyAgainst(baseline));
        }

        [Fact]
        public void BlankedThenRetypedOriginalValue_IsNotDirty()
        {
            var baseline = BuildFullState(currencyAmount: id => id == 5 ? "8" : "");

            var blanked = BuildFullState(currencyAmount: id => id == 5 ? "" : "");
            Assert.True(blanked.IsDirtyAgainst(baseline));

            var retyped = BuildFullState(currencyAmount: id => id == 5 ? "8" : "");
            Assert.False(retyped.IsDirtyAgainst(baseline));
        }

        [Fact]
        public void ToggledIgnoreCheckbox_IsDirty()
        {
            var baseline = BuildFullState();
            var current = BuildFullState(currencyIgnored: id => id == 32);

            Assert.Equal(
                new[] { SettingsFormState.CurrencyIgnoreKey(32) },
                current.ChangedKeys(baseline));
        }

        [Fact]
        public void ToggledIgnoreCheckboxBackAgain_IsNotDirty()
        {
            var baseline = BuildFullState(currencyIgnored: id => id == 32);
            var current = BuildFullState(currencyIgnored: id => id == 32);

            Assert.False(current.IsDirtyAgainst(baseline));
        }

        [Fact]
        public void EditedHomesteadTier_IsDirty()
        {
            var baseline = BuildFullState();
            var current = BuildFullState(homesteadTier: id => id == HomesteadItemIds[1] ? "2" : "0");

            Assert.Equal(
                new[] { SettingsFormState.HomesteadTierKey(HomesteadItemIds[1]) },
                current.ChangedKeys(baseline));
        }

        [Fact]
        public void EditedLoggingFields_AreDirty()
        {
            var baseline = BuildFullState();

            Assert.Equal(
                new[] { SettingsFormState.LogMaxSizeMbKey },
                BuildFullState(logMaxSizeMb: "8").ChangedKeys(baseline));
            Assert.Equal(
                new[] { SettingsFormState.LogRetentionDaysKey },
                BuildFullState(logRetentionDays: "7").ChangedKeys(baseline));
        }

        [Fact]
        public void EditedSnapshotInterval_IsDirty()
        {
            var baseline = BuildFullState();
            var current = BuildFullState(snapshotInterval: "45");

            Assert.Equal(
                new[] { SettingsFormState.SnapshotRefreshIntervalMinutesKey },
                current.ChangedKeys(baseline));
        }

        [Fact]
        public void SeveralSectionsEditedAtOnce_AllReportedSorted()
        {
            var baseline = BuildFullState();
            var current = BuildFullState(
                currencyAmount: id => id == 2 ? "5" : "",
                homesteadTier: id => id == HomesteadItemIds[0] ? "1" : "0",
                snapshotInterval: "45");

            var changed = current.ChangedKeys(baseline);

            Assert.Equal(3, changed.Count);
            Assert.Contains(SettingsFormState.CurrencyAmountKey(2), changed);
            Assert.Contains(SettingsFormState.HomesteadTierKey(HomesteadItemIds[0]), changed);
            Assert.Contains(SettingsFormState.SnapshotRefreshIntervalMinutesKey, changed);

            var sorted = new List<string>(changed);
            sorted.Sort(StringComparer.Ordinal);
            Assert.Equal(sorted, changed);
        }

        // A user who selects a box, types spaces and deletes them, or
        // pastes a value with a trailing newline, has not changed what
        // Save would persist - every parser in SettingsInputParser trims
        // before parsing.
        [Theory]
        [InlineData("1200", "  1200  ")]
        [InlineData("", "   ")]
        [InlineData("", null)]
        [InlineData("30", "30\t")]
        public void WhitespaceOnlyDifference_IsNotDirty(string baselineText, string currentText)
        {
            var baseline = new SettingsFormState();
            baseline.AddText(SettingsFormState.LogRetentionDaysKey, baselineText);

            var current = new SettingsFormState();
            current.AddText(SettingsFormState.LogRetentionDaysKey, currentText);

            Assert.False(current.IsDirtyAgainst(baseline));
        }

        [Fact]
        public void CaptureOrder_DoesNotAffectComparison()
        {
            var baseline = new SettingsFormState();
            baseline.AddText(SettingsFormState.LogMaxSizeMbKey, "2");
            baseline.AddText(SettingsFormState.LogRetentionDaysKey, "30");

            var current = new SettingsFormState();
            current.AddText(SettingsFormState.LogRetentionDaysKey, "30");
            current.AddText(SettingsFormState.LogMaxSizeMbKey, "2");

            Assert.False(current.IsDirtyAgainst(baseline));
        }

        // A field present in only one of the two captures is a change, in
        // both directions - a row that appeared or disappeared between
        // baseline and capture is exactly the case a value-only compare
        // would miss.
        [Fact]
        public void FieldPresentInOnlyOneCapture_IsDirtyEitherWay()
        {
            var withField = new SettingsFormState();
            withField.AddText(SettingsFormState.LogMaxSizeMbKey, "2");
            withField.AddText(SettingsFormState.LogRetentionDaysKey, "30");

            var withoutField = new SettingsFormState();
            withoutField.AddText(SettingsFormState.LogMaxSizeMbKey, "2");

            Assert.Equal(
                new[] { SettingsFormState.LogRetentionDaysKey },
                withoutField.ChangedKeys(withField));
            Assert.Equal(
                new[] { SettingsFormState.LogRetentionDaysKey },
                withField.ChangedKeys(withoutField));
        }

        [Fact]
        public void EmptyCaptureAgainstEmptyBaseline_IsNotDirty()
        {
            Assert.False(new SettingsFormState().IsDirtyAgainst(new SettingsFormState()));
        }

        // The tab has no baseline until it has been built once; a null
        // baseline must not be read as "everything changed", or the very
        // first tab switch after startup would prompt.
        [Fact]
        public void NullBaseline_IsNotDirty()
        {
            var current = BuildFullState(currencyAmount: id => "999");

            Assert.False(current.IsDirtyAgainst(null));
            Assert.Empty(current.ChangedKeys(null));
        }

        [Fact]
        public void DuplicateKey_IsRejected()
        {
            var state = new SettingsFormState();
            state.AddText(SettingsFormState.LogMaxSizeMbKey, "2");

            Assert.Throws<ArgumentException>(
                () => state.AddText(SettingsFormState.LogMaxSizeMbKey, "3"));
            Assert.Throws<ArgumentException>(
                () => state.AddFlag(SettingsFormState.LogMaxSizeMbKey, true));
        }

        [Fact]
        public void MissingKey_IsRejected()
        {
            var state = new SettingsFormState();

            Assert.Throws<ArgumentException>(() => state.AddText(null, "2"));
            Assert.Throws<ArgumentException>(() => state.AddText("", "2"));
        }

        [Fact]
        public void CurrencyAmountAndIgnoreKeys_NeverCollide()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int currencyId = 1; currencyId <= 200; currencyId++)
            {
                Assert.True(seen.Add(SettingsFormState.CurrencyAmountKey(currencyId)));
                Assert.True(seen.Add(SettingsFormState.CurrencyIgnoreKey(currencyId)));
                Assert.True(seen.Add(SettingsFormState.HomesteadTierKey(currencyId)));
            }
        }
    }
}
