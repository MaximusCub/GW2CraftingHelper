using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class SolveResult
    {
        public CraftingPlan Plan { get; set; }
        public IReadOnlyDictionary<int, SolverDecision> Decisions { get; set; }
    }
}
