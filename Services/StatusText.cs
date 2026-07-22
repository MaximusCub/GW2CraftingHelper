using System;

namespace GW2CraftingHelper.Services
{
    public static class StatusText
    {
        public static string Normalize(string status) => status ?? "";

        /// <summary>
        /// M37 (KNOWN-ISSUES #22/#27): the re-solve status line for
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
        /// M39 (snapshot search, d1-snapshot-about-settings.md Feature 1):
        /// formats a snapshot's age for the Snapshot tab's staleness suffix,
        /// e.g. "Updated - 3:41 PM (2m ago)". A negative age (CapturedAt
        /// momentarily ahead of the local clock - e.g. minor clock skew
        /// right after a fetch) is treated as zero rather than shown as a
        /// negative duration.
        /// </summary>
        public static string ForSnapshotAge(TimeSpan age)
        {
            if (age < TimeSpan.Zero)
            {
                age = TimeSpan.Zero;
            }

            if (age.TotalMinutes < 1)
            {
                return "just now";
            }

            if (age.TotalHours < 1)
            {
                return $"{(int)age.TotalMinutes}m ago";
            }

            if (age.TotalDays < 1)
            {
                return $"{(int)age.TotalHours}h {age.Minutes}m ago";
            }

            return $"{(int)age.TotalDays}d ago";
        }
    }
}
