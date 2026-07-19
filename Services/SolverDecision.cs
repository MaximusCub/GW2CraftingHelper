using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class SolverDecision
    {
        public AcquisitionSource Source { get; internal set; }
        public int RecipeId { get; internal set; }
        public long? TotalCost { get; internal set; }

        // Which acquisition paths were feasible for this node, independent
        // of which one was chosen. Drives the per-node override UI.
        public bool CanCraft { get; internal set; }
        public bool CanBuyTp { get; internal set; }
        public bool CanBuyVendor { get; internal set; }
    }
}
