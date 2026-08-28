using System;

namespace TaimisToolbench.Models
{
    /// <summary>
    /// The outcome of reading a plan.json document: the request layer
    /// always, the result layer when this build could read it. See
    /// PersistedPlan's own doc comment for the two-layer shape and
    /// docs/ARCHITECTURE.md section 12 for the contract.
    /// <para>
    /// <see cref="Plan"/> is never null. When <see cref="HasResult"/> is
    /// false its Result and NodeOverrides are null and nothing else on it
    /// was read from the result layer, so a caller that renders a plan
    /// must branch on <see cref="HasResult"/> rather than null-checking
    /// Result - the flag also carries WHY, which the user is told.
    /// </para>
    /// </summary>
    internal sealed class PersistedPlanLoad
    {
        private PersistedPlanLoad(PersistedPlan plan, Exception resultDiscardCause)
        {
            Plan = plan;
            ResultDiscardCause = resultDiscardCause;
        }

        public PersistedPlan Plan { get; }

        /// <summary>
        /// Null exactly when <see cref="HasResult"/> is true. Carries the
        /// exception the result layer failed on so the store can keep the
        /// two verdicts apart: a PlanSchemaVersionMismatchException is
        /// routine drift and is reported at Info, anything else is damage
        /// and goes to the error channel (see Services/PlanStore.cs).
        /// </summary>
        public Exception ResultDiscardCause { get; }

        public bool HasResult => ResultDiscardCause == null;

        internal static PersistedPlanLoad Full(PersistedPlan plan)
        {
            return new PersistedPlanLoad(plan, null);
        }

        internal static PersistedPlanLoad RequestOnly(PersistedPlan request, Exception cause)
        {
            return new PersistedPlanLoad(request, cause);
        }
    }
}
