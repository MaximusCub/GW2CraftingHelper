using System;
using System.Globalization;

namespace GW2CraftingHelper.Services
{
    public static class StatusText
    {
        public static string Normalize(string status) => status ?? "";

        /// <summary>
        /// The one timestamp format every user-facing status line uses.
        /// Invariant culture for the reason LogLineFormat records: the
        /// module's strings are English-only, and "h:mm tt" yields an EMPTY
        /// AM/PM designator under some cultures.
        /// </summary>
        public const string TimestampFormat = "MMM d, yyyy h:mm tt";

        /// <summary>
        /// The one separator between a status verb and its timestamp. An
        /// em-dash rather than the hyphen two of the four call sites used:
        /// a hyphen is already this module's WITHIN-clause separator
        /// ("Copy failed - clipboard unavailable", "Refresh failed:
        /// could not reach the GW2 API"), so reusing it here left a line
        /// with two identical separators at two different grammatical
        /// levels. Em-dash is permitted here under the repo's ASCII rule
        /// (rendered UI text, written as an escape, never a raw glyph).
        /// </summary>
        public const string StampSeparator = " \u2014 ";

        /// <summary>
        /// The shape of every timestamped status line in the module:
        /// "&lt;Sentence case verb&gt; - &lt;timestamp&gt;", one separator,
        /// one timestamp format. Four sites wrote this by hand with two
        /// different separators (Views/MainView's three snapshot lines,
        /// SettingsTabContent's "Saved", CraftingPlanView's "Plan
        /// generated"); they now all call here, so no future site can
        /// invent a fifth spelling.
        /// <para>
        /// The verb is written by the caller and is expected to be sentence
        /// case; a blank verb yields the bare timestamp rather than a
        /// dangling separator.
        /// </para>
        /// </summary>
        public static string Stamp(string verb, DateTime when)
        {
            string trimmedVerb = verb == null ? "" : verb.Trim();
            string timestamp = when.ToString(TimestampFormat, CultureInfo.InvariantCulture);
            return trimmedVerb.Length == 0 ? timestamp : trimmedVerb + StampSeparator + timestamp;
        }

        /// <summary>
        /// The re-solve status line for
        /// CraftingPlanView.ApplyOverridesAndResolve. "Best path restored"
        /// is the Best Path preset's own label and must only be written
        /// when that preset is the trigger - every other re-solve trigger
        /// (Craft All, Buy All, per-node craft/tp/vendor pill cycling, the
        /// ignore toggle) gets the neutral "Decisions updated" family,
        /// regardless of how many overrides happen to remain afterward.
        /// </summary>
        public static string ForOverrideResolve(bool isBestPathPreset, int overrideCount)
        {
            return isBestPathPreset
                ? "Best path restored"
                : $"Decisions updated ({overrideCount} override(s))";
        }

        /// <summary>
        /// Formats a snapshot's age for the Snapshot tab's staleness
        /// suffix (d1-snapshot-about-settings.md Feature 1),
        /// e.g. "Updated - Aug 15, 2026 3:41 PM (2m ago)". A negative age
        /// (CapturedAt momentarily ahead of the local clock - e.g. minor
        /// clock skew right after a fetch) is treated as zero rather than
        /// shown as a negative duration.
        /// </summary>
        public static string ForSnapshotAge(TimeSpan age)
        {
            if (age < TimeSpan.Zero)
            {
                age = TimeSpan.Zero;
            }

            return age.TotalMinutes < 1 ? "just now" : $"{AgeMagnitude(age)} ago";
        }

        /// <summary>
        /// How much time an age is, with no "ago"/"old" framing - the one
        /// bucket ladder <see cref="ForSnapshotAge"/> and
        /// <see cref="ForSnapshotAgeSuffix"/> both read, so the two
        /// wordings can never disagree about when a snapshot turns from
        /// minutes into hours. Callers handle the sub-minute case
        /// themselves; below a minute this reports "0m".
        /// </summary>
        private static string AgeMagnitude(TimeSpan age)
        {
            if (age.TotalHours < 1)
            {
                return $"{(int)age.TotalMinutes}m";
            }

            if (age.TotalDays < 1)
            {
                return $"{(int)age.TotalHours}h {age.Minutes}m";
            }

            return $"{(int)age.TotalDays}d";
        }

        /// <summary>
        /// The Snapshot header's age suffix, worded so it cannot be read as
        /// part of the timestamp beside it. The line pairs two different
        /// moments - when the last refresh ATTEMPT happened and how old the
        /// snapshot on screen is - and the old
        /// "Refresh failed: ... - Aug 15, 2026 3:41 PM (29d ago)" put a
        /// bare relative time immediately after an absolute one, which
        /// reads as a restatement of that same instant rather than a
        /// second fact about a different one. "(snapshot 29d old)" names
        /// its subject, so the two can no longer collapse into one.
        /// <para>
        /// Built on <see cref="ForSnapshotAge"/>'s buckets rather than
        /// beside them, so the two can never disagree about when a
        /// snapshot turns from minutes into hours.
        /// </para>
        /// </summary>
        public static string ForSnapshotAgeSuffix(TimeSpan age)
        {
            if (age < TimeSpan.Zero)
            {
                age = TimeSpan.Zero;
            }

            if (age.TotalMinutes < 1)
            {
                return "snapshot just captured";
            }

            return $"snapshot {AgeMagnitude(age)} old";
        }

        /// <summary>
        /// Whether a snapshot of the given age counts as stale against the
        /// caller-supplied threshold. The Snapshot tab's staleness recolor
        /// (Views/MainView.cs) and Module.Update()'s auto-refresh gate both
        /// derive their threshold from
        /// ModuleSettings.GetClampedSnapshotRefreshIntervalMinutes, so the
        /// warning color and the auto-refresh can never disagree about
        /// which snapshots are stale.
        /// </summary>
        public static bool IsStale(TimeSpan age, TimeSpan staleThreshold)
        {
            return age >= staleThreshold;
        }

        /// <summary>
        /// Cause text for a failed Refresh Now (Views/MainView.cs), keyed
        /// by SnapshotFailureClassifier's classification - the field-tested
        /// fix for the "Refresh Failed" dead end (at CHARACTER
        /// SELECT every account data source throws an invalid-token
        /// exception, and the bare status line gave no hint why). Callers
        /// pass the result to <see cref="Stamp"/> as the verb, so the
        /// Unknown case still reads exactly like every other status line
        /// (nothing more specific to say once no known pattern matched).
        /// ApiAccessNotReady also drives Views/MainView.cs's walkthrough
        /// dialog, but still gets its own status text here so the header
        /// label reads correctly once that dialog is closed.
        /// <para>
        /// The cause clause is introduced by a COLON, not the dash it used
        /// to use: <see cref="StampSeparator"/> now owns the dash, and a
        /// line carrying both ("Refresh failed - could not reach the GW2
        /// API - Aug 15, 2026 3:41 PM") gave two unrelated clauses the same
        /// separator and no way to tell which was which.
        /// </para>
        /// </summary>
        public static string ForRefreshFailure(SnapshotFailureKind kind, int failedSourceCount, int totalSourceCount)
        {
            switch (kind)
            {
                case SnapshotFailureKind.ApiAccessNotReady:
                    return "Refresh failed: GW2 API access not ready";
                case SnapshotFailureKind.NetworkOrApiDown:
                    return "Refresh failed: could not reach the GW2 API";
                case SnapshotFailureKind.PartialFailure:
                    return $"Refresh partially failed: {failedSourceCount} of {totalSourceCount} sources";
                default:
                    return "Refresh failed";
            }
        }
    }
}
