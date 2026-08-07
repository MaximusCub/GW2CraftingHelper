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

        // M37 (KNOWN-ISSUES #22/#27): the ignore toggle (and every other
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

        // ---- ForSnapshotAge (M39 snapshot search) ----

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

        // ---- ForRefreshFailure (field-tested pain, 2026-08-06: the
        // Snapshot tab's Refresh Now used to show only bare
        // "Refresh Failed - {time}" regardless of cause) ----

        [Fact]
        public void ForRefreshFailure_ApiAccessNotReady_ReturnsAccessNotReadyText()
        {
            Assert.Equal(
                "Refresh failed \u2014 GW2 API access not ready",
                StatusText.ForRefreshFailure(SnapshotFailureKind.ApiAccessNotReady, failedSourceCount: 5, totalSourceCount: 5));
        }

        [Fact]
        public void ForRefreshFailure_NetworkOrApiDown_ReturnsCouldNotReachText()
        {
            Assert.Equal(
                "Refresh failed \u2014 could not reach the GW2 API",
                StatusText.ForRefreshFailure(SnapshotFailureKind.NetworkOrApiDown, failedSourceCount: 5, totalSourceCount: 5));
        }

        [Fact]
        public void ForRefreshFailure_PartialFailure_ReturnsCountText()
        {
            Assert.Equal(
                "Refresh partially failed \u2014 2 of 5 sources",
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
    }

}
