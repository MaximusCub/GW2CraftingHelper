using System;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{

    public class StatusTextTests
    {

        [Fact]
        public void Normalize_NonNull_ReturnsSameString()
        {
            Assert.Equal("Updated \u2014 1:00 PM", StatusText.Normalize("Updated \u2014 1:00 PM"));
        }

        [Fact]
        public void Normalize_Null_ReturnsEmpty()
        {
            Assert.Equal("", StatusText.Normalize(null));
        }

        [Fact]
        public void Normalize_Empty_ReturnsEmpty()
        {
            Assert.Equal("", StatusText.Normalize(""));
        }

        // The ignore toggle (and every other
        // non-Best-Path re-solve trigger) must never produce the Best
        // Path preset's own label, regardless of the current override
        // count - this is exactly the "Best path restored" mislabel bug.
        [Fact]
        public void ForOverrideResolve_NotBestPathPreset_ZeroOverrides_ReturnsDecisionsUpdated()
        {
            Assert.Equal("Decisions updated (0 override(s))", StatusText.ForOverrideResolve(isBestPathPreset: false, overrideCount: 0));
        }

        [Fact]
        public void ForOverrideResolve_NotBestPathPreset_WithOverrides_ReturnsDecisionsUpdatedWithCount()
        {
            Assert.Equal("Decisions updated (3 override(s))", StatusText.ForOverrideResolve(isBestPathPreset: false, overrideCount: 3));
        }

        [Fact]
        public void ForOverrideResolve_BestPathPreset_ReturnsBestPathRestored()
        {
            Assert.Equal("Best path restored", StatusText.ForOverrideResolve(isBestPathPreset: true, overrideCount: 0));
        }

        // ---- ForSnapshotAge ----

        [Fact]
        public void ForSnapshotAge_LessThanOneMinute_ReturnsJustNow()
        {
            Assert.Equal("just now", StatusText.ForSnapshotAge(TimeSpan.FromSeconds(30)));
        }

        [Fact]
        public void ForSnapshotAge_Zero_ReturnsJustNow()
        {
            Assert.Equal("just now", StatusText.ForSnapshotAge(TimeSpan.Zero));
        }

        [Fact]
        public void ForSnapshotAge_Negative_ClampedToZero_ReturnsJustNow()
        {
            // CapturedAt momentarily ahead of the local clock (minor clock
            // skew) must never render as a negative duration.
            Assert.Equal("just now", StatusText.ForSnapshotAge(TimeSpan.FromSeconds(-5)));
        }

        [Fact]
        public void ForSnapshotAge_UnderOneHour_ReturnsMinutesAgo()
        {
            Assert.Equal("2m ago", StatusText.ForSnapshotAge(TimeSpan.FromMinutes(2)));
        }

        [Fact]
        public void ForSnapshotAge_JustUnderOneHour_ReturnsMinutesAgo()
        {
            Assert.Equal("59m ago", StatusText.ForSnapshotAge(TimeSpan.FromMinutes(59)));
        }

        [Fact]
        public void ForSnapshotAge_UnderOneDay_ReturnsHoursAndMinutesAgo()
        {
            Assert.Equal("1h 5m ago", StatusText.ForSnapshotAge(TimeSpan.FromMinutes(65)));
        }

        [Fact]
        public void ForSnapshotAge_ExactlyOneHour_ReturnsHoursAndMinutesAgo()
        {
            Assert.Equal("1h 0m ago", StatusText.ForSnapshotAge(TimeSpan.FromHours(1)));
        }

        [Fact]
        public void ForSnapshotAge_OneDayOrMore_ReturnsDaysAgo()
        {
            Assert.Equal("2d ago", StatusText.ForSnapshotAge(TimeSpan.FromDays(2)));
        }

        [Fact]
        public void ForSnapshotAge_ExactlyOneDay_ReturnsDaysAgo()
        {
            Assert.Equal("1d ago", StatusText.ForSnapshotAge(TimeSpan.FromDays(1)));
        }

        // ---- IsStale (the staleness label and Module.Update()'s
        // auto-refresh gate share ONE threshold, sourced from
        // SnapshotRefreshIntervalMinutes - the threshold is a parameter
        // here precisely so the caller's setting value flows through
        // rather than being hardcoded on either side) ----

        [Fact]
        public void IsStale_AgeBelowThreshold_ReturnsFalse()
        {
            Assert.False(StatusText.IsStale(TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(10)));
        }

        [Fact]
        public void IsStale_AgeEqualToThreshold_ReturnsTrue()
        {
            Assert.True(StatusText.IsStale(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)));
        }

        [Fact]
        public void IsStale_AgeAboveThreshold_ReturnsTrue()
        {
            Assert.True(StatusText.IsStale(TimeSpan.FromMinutes(11), TimeSpan.FromMinutes(10)));
        }

        [Fact]
        public void IsStale_SameAge_DifferentThresholds_ThresholdDecides()
        {
            // The same 6-minute-old snapshot is stale under a 5-minute
            // setting and fresh under a 10-minute one - pins that the
            // verdict tracks the supplied threshold, not a constant.
            TimeSpan age = TimeSpan.FromMinutes(6);
            Assert.True(StatusText.IsStale(age, TimeSpan.FromMinutes(5)));
            Assert.False(StatusText.IsStale(age, TimeSpan.FromMinutes(10)));
        }

        // ---- ForRefreshFailure (field-tested pain: the
        // Snapshot tab's Refresh Now used to show only bare
        // "Refresh Failed - {time}" regardless of cause) ----

        [Fact]
        public void ForRefreshFailure_ApiAccessNotReady_ReturnsAccessNotReadyText()
        {
            Assert.Equal(
                "Refresh failed: GW2 API access not ready",
                StatusText.ForRefreshFailure(SnapshotFailureKind.ApiAccessNotReady, failedSourceCount: 5, totalSourceCount: 5));
        }

        [Fact]
        public void ForRefreshFailure_NetworkOrApiDown_ReturnsCouldNotReachText()
        {
            Assert.Equal(
                "Refresh failed: could not reach the GW2 API",
                StatusText.ForRefreshFailure(SnapshotFailureKind.NetworkOrApiDown, failedSourceCount: 5, totalSourceCount: 5));
        }

        [Fact]
        public void ForRefreshFailure_PartialFailure_ReturnsCountText()
        {
            Assert.Equal(
                "Refresh partially failed: 2 of 5 sources",
                StatusText.ForRefreshFailure(SnapshotFailureKind.PartialFailure, failedSourceCount: 2, totalSourceCount: 5));
        }

        [Fact]
        public void ForRefreshFailure_Unknown_ReturnsBareFailedText()
        {
            // Matches the pre-existing "Refresh failed - {time}" shape
            // exactly - callers append the time suffix themselves.
            Assert.Equal(
                "Refresh failed",
                StatusText.ForRefreshFailure(SnapshotFailureKind.Unknown, failedSourceCount: 0, totalSourceCount: 0));
        }

        // ---- Stamp (audit batch J, M10): the ONE shape every timestamped
        // status line in the module uses. Four sites wrote it by hand with
        // two different separators before this. ----

        [Fact]
        public void Stamp_VerbAndTime_UsesTheSingleSeparatorAndFormat()
        {
            Assert.Equal(
                "Plan generated \u2014 Aug 8, 2026 3:00 PM",
                StatusText.Stamp("Plan generated", new DateTime(2026, 8, 8, 15, 0, 0)));
        }

        [Fact]
        public void Stamp_FailureCause_ReadsAsOneClausePerSeparator()
        {
            // The cause clause is colon-introduced and the timestamp
            // dash-introduced, so the composed line never repeats one
            // separator at two grammatical levels.
            string cause = StatusText.ForRefreshFailure(
                SnapshotFailureKind.NetworkOrApiDown, failedSourceCount: 5, totalSourceCount: 5);
            Assert.Equal(
                "Refresh failed: could not reach the GW2 API \u2014 Aug 15, 2026 3:41 PM",
                StatusText.Stamp(cause, new DateTime(2026, 8, 15, 15, 41, 0)));
        }

        [Fact]
        public void Stamp_BlankVerb_ReturnsBareTimestampNotADanglingSeparator()
        {
            string expected = "Aug 8, 2026 3:00 PM";
            Assert.Equal(expected, StatusText.Stamp(null, new DateTime(2026, 8, 8, 15, 0, 0)));
            Assert.Equal(expected, StatusText.Stamp("   ", new DateTime(2026, 8, 8, 15, 0, 0)));
        }

        // ---- ForSnapshotAgeSuffix (audit batch J, M10): the age suffix
        // names its subject, so a failure timestamp followed by a snapshot
        // age can no longer read as one moment. ----

        [Fact]
        public void ForSnapshotAgeSuffix_Days_NamesItsSubject()
        {
            Assert.Equal("snapshot 29d old", StatusText.ForSnapshotAgeSuffix(TimeSpan.FromDays(29)));
        }

        [Fact]
        public void ForSnapshotAgeSuffix_Minutes_NamesItsSubject()
        {
            Assert.Equal("snapshot 2m old", StatusText.ForSnapshotAgeSuffix(TimeSpan.FromMinutes(2)));
        }

        [Fact]
        public void ForSnapshotAgeSuffix_Hours_KeepsTheMinutesComponent()
        {
            Assert.Equal(
                "snapshot 3h 20m old",
                StatusText.ForSnapshotAgeSuffix(TimeSpan.FromMinutes(200)));
        }

        [Fact]
        public void ForSnapshotAgeSuffix_SubMinute_ReadsAsCapturedNotZeroOld()
        {
            Assert.Equal("snapshot just captured", StatusText.ForSnapshotAgeSuffix(TimeSpan.FromSeconds(30)));
        }

        [Fact]
        public void ForSnapshotAgeSuffix_Negative_ClampedToZero()
        {
            // Same clock-skew clamp ForSnapshotAge applies - never a
            // negative duration on screen.
            Assert.Equal("snapshot just captured", StatusText.ForSnapshotAgeSuffix(TimeSpan.FromSeconds(-5)));
        }

        // The two age wordings read the same bucket ladder, so they can
        // never disagree about when a snapshot turns from minutes into
        // hours - the drift this extraction exists to prevent.
        [Theory]
        [InlineData(2)]
        [InlineData(59)]
        [InlineData(60)]
        [InlineData(200)]
        [InlineData(1440)]
        [InlineData(41760)]
        public void ForSnapshotAgeSuffix_AgreesWithForSnapshotAgeBuckets(int minutes)
        {
            var age = TimeSpan.FromMinutes(minutes);
            string magnitude = StatusText.ForSnapshotAge(age).Replace(" ago", "");
            Assert.Equal($"snapshot {magnitude} old", StatusText.ForSnapshotAgeSuffix(age));
        }
    }

}
