using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    internal class SolveResult
    {
        public CraftingPlan Plan { get; internal set; }

        public IReadOnlyDictionary<int, SolverDecision> Decisions { get; internal set; }

        /// <summary>
        /// Every vendor cost-line item this solve costed, and what one unit
        /// of it costs. Null when the solve was given no cost-line inputs. A
        /// null VALUE is a line nothing could cost, which stays a barter
        /// line. Snapshotted into PlanSolveContext so an override re-solve
        /// reuses these answers rather than re-deriving them.
        /// </summary>
        public IReadOnlyDictionary<int, CostLineUnitValue> VendorCostLineValues { get; internal set; }
    }
}
