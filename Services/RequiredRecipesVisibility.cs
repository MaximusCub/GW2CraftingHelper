using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure "Hide Unlocked Recipes" filter/header-text logic for the plan
    /// view's Required Recipes section (wave-3 quick win #3, 2026-08-06
    /// field testing). Blish-free by design (works only on
    /// PlanRowViewModel/List/bool) so it can be exercised by a real test
    /// without any Blish HUD dependency - CraftingPlanView calls this from
    /// its RequiredRecipes render branch instead of embedding the filter
    /// predicate inline where it could not be unit-tested.
    ///
    /// A row is "unlocked" here iff its StatusTag is exactly "Learned" or
    /// "Auto-learned" (PlanViewModelBuilder.BuildRecipesSection's own two
    /// unlocked-status strings). A row with StatusTag == "Missing!" is kept
    /// (that is the whole point of the filter); a row with an EMPTY
    /// StatusTag ("" - recipe permission was not available, so unlock
    /// status could not be determined at all, see
    /// PlanViewModelBuilderStepSectionsTests.RequiredRecipes_NullMissing_EmptyStatusTag)
    /// is also kept, deliberately - hiding a row whose status is unknown
    /// would silently claim "you have nothing to do here" for a recipe this
    /// module simply could not check, which is worse than a harmless extra
    /// row.
    /// </summary>
    public static class RequiredRecipesVisibility
    {
        private const string LearnedStatusTag = "Learned";
        private const string AutoLearnedStatusTag = "Auto-learned";

        public static bool IsUnlocked(string statusTag)
        {
            return statusTag == LearnedStatusTag || statusTag == AutoLearnedStatusTag;
        }

        /// <summary>
        /// Returns the rows that should render given the current filter
        /// state: every row when hideUnlocked is false, otherwise every row
        /// that is NOT Learned/Auto-learned. Never mutates the input list -
        /// the section's own Rows list stays the permanent, unfiltered
        /// source of truth across toggles (see CraftingPlanView's
        /// _hideUnlockedRecipes field doc comment).
        /// </summary>
        public static List<PlanRowViewModel> ApplyFilter(
            IReadOnlyList<PlanRowViewModel> rows, bool hideUnlocked)
        {
            if (rows == null)
            {
                return new List<PlanRowViewModel>();
            }

            if (!hideUnlocked)
            {
                return new List<PlanRowViewModel>(rows);
            }

            var visible = new List<PlanRowViewModel>(rows.Count);
            foreach (var row in rows)
            {
                if (!IsUnlocked(row?.StatusTag))
                {
                    visible.Add(row);
                }
            }
            return visible;
        }

        /// <summary>
        /// Section header title. Always states the TOTAL recipe count (post
        /// wave-3 #2 Mystic Forge filter, applied upstream by
        /// PlanViewModelBuilder.BuildRecipesSection) so the header is never
        /// dishonest about how many recipes the plan actually needs -
        /// "(showing K missing of N)" only replaces the bare "(N)" when the
        /// filter is actually active and there is at least one recipe to
        /// count.
        /// </summary>
        public static string BuildHeaderTitle(int totalCount, int visibleCount, bool hideUnlocked)
        {
            if (hideUnlocked && totalCount > 0)
            {
                return $"Required Recipes (showing {visibleCount} missing of {totalCount})";
            }
            return $"Required Recipes ({totalCount})";
        }

        /// <summary>
        /// Friendly single-line replacement for an empty filtered row list -
        /// shown instead of a section that would otherwise render a header
        /// with zero rows beneath it. totalCount is the section's real
        /// (unfiltered) recipe count, matching BuildHeaderTitle's own N.
        /// </summary>
        public static string AllUnlockedMessage(int totalCount)
        {
            return $"All {totalCount} recipes already unlocked.";
        }
    }
}
