using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    internal class ReducedTreeResult
    {
        public RecipeNode ReducedTree { get; set; }

        public List<UsedMaterial> UsedMaterials { get; set; } = new List<UsedMaterial>();

        /// <summary>
        /// Per-node owned-quantity attribution: how many units
        /// EACH tree node (not aggregated by item id, unlike UsedMaterials)
        /// consumed from the owned pool during reduction. Keyed by the
        /// RecipeNode object reference INSIDE ReducedTree (reference
        /// equality - RecipeNode has no Equals/GetHashCode override), NOT by
        /// NodeId: at reduction time NodeId has not been assigned yet
        /// (PlanSolver.Solve assigns it fresh, later, when the reduced tree
        /// is actually solved) - callers convert this to a NodeId-keyed
        /// lookup themselves once Solve() has run against ReducedTree and
        /// populated real NodeIds on these same node objects (see
        /// CraftingPlanPipeline). Only entries with a positive consumed
        /// amount are present.
        /// </summary>
        public Dictionary<RecipeNode, int> OwnedQuantityUsedByNode { get; set; } = new Dictionary<RecipeNode, int>();
    }
}
