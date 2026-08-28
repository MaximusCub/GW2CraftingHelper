using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    internal class SolveResult
    {
        public CraftingPlan Plan { get; internal set; }

        public IReadOnlyDictionary<int, SolverDecision> Decisions { get; internal set; }
    }
}
