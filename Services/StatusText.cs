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
    }
}
