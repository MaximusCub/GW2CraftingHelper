using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class SolverDecision
    {
        public AcquisitionSource Source { get; set; }
        public int RecipeId { get; set; }
        public long? TotalCost { get; set; }
    }
}
