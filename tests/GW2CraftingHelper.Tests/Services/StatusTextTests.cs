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

        // Count: the module's one spelling of a counted noun. "(s)" is
        // banned from user-facing text, and every count the plan view is
        // about to show (overrides, ignored items, copied lines) goes
        // through here.

        [Theory]
        [InlineData(0, "0 overrides")]
        [InlineData(1, "1 override")]
        [InlineData(2, "2 overrides")]
        [InlineData(147, "147 overrides")]
        public void Count_PluralizesOnOneAndNothingElse(int n, string expected)
        {
            Assert.Equal(expected, StatusText.Count(n, "override"));
        }

        [Fact]
        public void Count_IrregularPlural_IsPassedExplicitly()
        {
            Assert.Equal("1 entry", StatusText.Count(1, "entry", "entries"));
            Assert.Equal("2 entries", StatusText.Count(2, "entry", "entries"));
        }

        [Fact]
        public void Count_NegativeCount_StillReadsAsAPlural()
        {
            // Not reachable from any current caller, but a count that went
            // negative must not read "-1 override" as though it were one.
            Assert.Equal("-1 overrides", StatusText.Count(-1, "override"));
        }

        // The ignore toggle (and every other
        // non-Best-Path re-solve trigger) must never produce the Best
        // Path preset's own label - this is exactly the "Best path
        // restored" mislabel bug.
        [Fact]
        public void ForOverrideResolve_NotBestPathPreset_ReportsTheEventOnly()
        {
            Assert.Equal("Plan updated", StatusText.ForOverrideResolve(isBestPathPreset: false));
        }

        [Fact]
        public void ForOverrideResolve_BestPathPreset_ReturnsBestPathRestored()
        {
            Assert.Equal("Best path restored", StatusText.ForOverrideResolve(isBestPathPreset: true));
        }

        /// <summary>
        /// The events/state split: the status line reports what HAPPENED
        /// and carries no standing count. How many decisions are overridden
        /// is state, and lives in its own chip.
        /// </summary>
        [Fact]
        public void ForOverrideResolve_CarriesNoCount()
        {
            string line = StatusText.ForOverrideResolve(isBestPathPreset: false);

            Assert.DoesNotContain("override", line, StringComparison.OrdinalIgnoreCase);
            foreach (char c in line)
            {
                Assert.False(char.IsDigit(c), "the status line must carry no count: " + line);
            }
        }

        [Theory]
        [InlineData(0, "Overrides: 0")]
        [InlineData(1, "Overrides: 1")]
        [InlineData(12, "Overrides: 12")]
        public void ForOverridesChip_IsALabelledCount(int n, string expected)
        {
            Assert.Equal(expected, StatusText.ForOverridesChip(n));
        }

        [Theory]
        [InlineData(1, "Ignored: 1")]
        [InlineData(7, "Ignored: 7")]
        public void ForIgnoredChip_IsALabelledCount(int n, string expected)
        {
            Assert.Equal(expected, StatusText.ForIgnoredChip(n));
        }

        /// <summary>
        /// The two failure verbs must stay distinct: a failed GENERATION
        /// leaves the tab with the plan it had, a failed local re-solve
        /// leaves the plan on screen intact with only the change
        /// unapplied. "Error:" said neither.
        /// </summary>
        [Fact]
        public void FailureVerbs_NameWhatFailed_AndDiffer()
        {
            Assert.Equal("Generation failed: no route to host", StatusText.ForGenerationFailure("no route to host"));
            Assert.Equal("Update failed: no route to host", StatusText.ForUpdateFailure("no route to host"));
            Assert.NotEqual(
                StatusText.ForGenerationFailure("x"), StatusText.ForUpdateFailure("x"));
        }

        [Fact]
        public void NoOpLines_SayWhyTheClickDidNothing()
        {
            // Each pairs with one guard in the confirm matrix. They are
            // sentence-case event lines like every other status write, and
            // none of them reaches for "(s)".
            foreach (string line in new[]
            {
                StatusText.NoOverridesToClear,
                StatusText.AlreadyCraftingEverything,
                StatusText.AlreadyBuyingEverything,
                StatusText.ReSolveUnavailable
            })
            {
                Assert.False(string.IsNullOrWhiteSpace(line));
                Assert.DoesNotContain("(s)", line);
                Assert.Equal(char.ToUpperInvariant(line[0]), line[0]);
            }
        }

        [Fact]
        public void UnavailableIsNotUnnecessary_SoItsLineClaimsNothingAboutThePlan()
        {
            // A plan restored without its solve context can be rendered and
            // not re-solved. The three lines above assert what the plan
            // ALREADY contains, which is exactly what nothing has read in
            // that state - so this one must not be any of them, and must
            // name the action that gets out of it.
            Assert.NotEqual(StatusText.AlreadyCraftingEverything, StatusText.ReSolveUnavailable);
            Assert.NotEqual(StatusText.AlreadyBuyingEverything, StatusText.ReSolveUnavailable);
            Assert.NotEqual(StatusText.NoOverridesToClear, StatusText.ReSolveUnavailable);

            Assert.DoesNotContain("Already", StatusText.ReSolveUnavailable);
            Assert.Contains("Generate Plan", StatusText.ReSolveUnavailable);
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
        public void ForSnapshotAgeSuffix_Zero_ReadsAsCaptured()
        {
            Assert.Equal("snapshot just captured", StatusText.ForSnapshotAgeSuffix(TimeSpan.Zero));
        }

        [Fact]
        public void ForSnapshotAgeSuffix_Negative_ClampedToZero()
        {
            // CapturedAt momentarily ahead of the local clock (minor clock
            // skew) must never render as a negative duration.
            Assert.Equal("snapshot just captured", StatusText.ForSnapshotAgeSuffix(TimeSpan.FromSeconds(-5)));
        }

        // The bucket ladder, boundary by boundary - the coverage that used
        // to sit on a second, caller-less age wording.
        [Theory]
        [InlineData(0.5, "snapshot just captured")]
        [InlineData(1, "snapshot 1m old")]
        [InlineData(59, "snapshot 59m old")]
        [InlineData(60, "snapshot 1h 0m old")]
        [InlineData(65, "snapshot 1h 5m old")]
        [InlineData(1439, "snapshot 23h 59m old")]
        [InlineData(1440, "snapshot 1d old")]
        [InlineData(2880, "snapshot 2d old")]
        public void ForSnapshotAgeSuffix_BucketLadder(double minutes, string expected)
        {
            Assert.Equal(expected, StatusText.ForSnapshotAgeSuffix(TimeSpan.FromMinutes(minutes)));
        }
    }

}
