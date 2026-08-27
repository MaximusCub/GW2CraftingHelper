namespace TaimisToolbench.Services
{
    /// <summary>
    /// Resolves the "Use Own Materials" toggle against the one fact that
    /// decides whether it can do anything: whether an account snapshot
    /// exists to subtract from.
    /// <para>
    /// Invariant: with no snapshot there is nothing to subtract, so the
    /// plan is solved as if the account owns nothing. The toggle must not
    /// render as an active, satisfied setting while that is true - which is
    /// why <see cref="OwnMaterialsControlState.Checked"/> is BOTH what the
    /// checkbox displays and the value handed to the solver. The two cannot
    /// drift, so the box can never claim an input the plan did not get.
    /// </para>
    /// <para>
    /// The user's standing intent is held by the caller and passed through
    /// untouched, so a snapshot arriving later restores the setting with no
    /// restart and no second source of truth.
    /// </para>
    /// <para>
    /// Blish-free and unit-testable, for the same reason as
    /// <see cref="PlanStripTickDecision"/>: the view that consumes it is
    /// not.
    /// </para>
    /// </summary>
    public static class OwnMaterialsGate
    {
        /// <summary>
        /// Applied only while the toggle is gated off, so the disabled
        /// state says why rather than reading as an arbitrary refusal.
        /// </summary>
        public const string NoAccountDataTooltip =
            "Needs account data. Without it there is nothing to subtract, so every ingredient is priced as if you own none.";

        public static OwnMaterialsControlState Resolve(bool userIntent, bool accountDataAvailable)
        {
            if (!accountDataAvailable)
            {
                return new OwnMaterialsControlState(false, false, NoAccountDataTooltip);
            }

            return new OwnMaterialsControlState(true, userIntent, null);
        }
    }

    public readonly struct OwnMaterialsControlState
    {
        public readonly bool Enabled;

        /// <summary>
        /// What the checkbox shows AND what the next Generate solves with -
        /// see <see cref="OwnMaterialsGate"/>'s invariant.
        /// </summary>
        public readonly bool Checked;

        /// <summary>
        /// Null while the toggle is live. Callers apply it unconditionally
        /// so the explanation is cleared on the way back rather than left
        /// standing over a working control.
        /// </summary>
        public readonly string Tooltip;

        public OwnMaterialsControlState(bool enabled, bool isChecked, string tooltip)
        {
            Enabled = enabled;
            Checked = isChecked;
            Tooltip = tooltip;
        }
    }
}
