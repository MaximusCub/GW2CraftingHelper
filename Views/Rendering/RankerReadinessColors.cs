using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// Chrome for the Crafting Ranker's readiness percentages.
    ///
    /// <para>
    /// Three bands, not a gradient. A ranked list is read by scanning, and a
    /// continuous ramp gives a scanning eye nothing to catch on - two rows
    /// four points apart would be indistinguishable while claiming to differ.
    /// Three steps make "nearly there", "under way" and "barely started" legible
    /// at a glance, which is the only judgement the colour is being asked for.
    /// </para>
    ///
    /// <para>
    /// Deliberately NOT an arm of <see cref="PillColors"/>, for the reason
    /// <see cref="ShoppingBadgeColors"/>'s own doc comment gives: that switch is
    /// the recipe tree's decision vocabulary - green means selected, blue owned,
    /// amber ignore-active - and none of those meanings is "you are 70% of the
    /// way through this". Reusing one would dilute a vocabulary the tree depends
    /// on.
    /// </para>
    /// </summary>
    internal static class RankerReadinessColors
    {
        internal const double NearDoneThreshold = 0.90;
        internal const double InProgressThreshold = 0.50;

        /// <summary>Muted enough to sit under body text without reading as a success banner.</summary>
        private static readonly Color NearDone = new Color(126, 186, 126);    // #7EBA7E

        private static readonly Color InProgress = new Color(198, 176, 106);  // #C6B06A

        /// <summary>
        /// Deliberately not the Missing!-red family the Required Recipes status
        /// column uses: "barely started" is not a fault, and red everywhere in
        /// this module means a problem.
        /// </summary>
        private static readonly Color Early = new Color(176, 128, 96);        // #B08060

        /// <summary>The module's standing neutral, for a figure that is not a measurement.</summary>
        internal static readonly Color Neutral = new Color(150, 150, 150);

        internal static Color ForReadiness(double readiness)
        {
            if (readiness >= NearDoneThreshold)
            {
                return NearDone;
            }
            return readiness >= InProgressThreshold ? InProgress : Early;
        }

        /// <summary>
        /// Days are coloured on their own scale rather than the readiness one:
        /// a day count is not a completion fraction, and a long wait is worth
        /// flagging even on a row that is otherwise nearly finished.
        /// </summary>
        internal static Color ForDays(int days)
        {
            if (days <= 0)
            {
                return Neutral;
            }
            if (days <= 7)
            {
                return NearDone;
            }
            return days <= 30 ? InProgress : Early;
        }
    }
}
