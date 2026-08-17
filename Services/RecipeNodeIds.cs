using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Deterministic pre-order DFS NodeId assignment (root, then each
    /// node's recipes' ingredients, in list order) - the same algorithm
    /// PlanSolver.Solve has always used internally, extracted here
    /// so it can also be called BEFORE InventoryReducer.Reduce runs.
    ///
    /// Why this matters: InventoryReducer.CloneNode preserves whatever
    /// NodeId a node already has when it clones/prunes a tree. Pre-assigning
    /// ids to the UNREDUCED tree here, before reduction clones it, lets
    /// those ids survive unchanged onto the corresponding SURVIVING nodes
    /// of the reduced clone - which is exactly what
    /// OwnedMaterialsForceBuyPrePass needs: it computes gw2e's force-buy
    /// rule against a genuine zero-owned baseline (the pre-reduction tree),
    /// and CraftingPlanPipeline's real solve (on the POST-reduction tree)
    /// must be able to key that pre-pass's forceBuyOnlyNodeIds set against
    /// the SAME ids - see PlanSolver.Solve's `assignNodeIds` parameter,
    /// which is set to false for that real solve so it reuses these
    /// pre-assigned (non-contiguous, but still tree-unique) ids instead of
    /// renumbering from scratch over the pruned tree's smaller shape.
    /// </summary>
    public static class RecipeNodeIds
    {
        public static void Assign(RecipeNode root)
        {
            int nextNodeId = 0;
            AssignRecursive(root, ref nextNodeId);
        }

        private static void AssignRecursive(RecipeNode node, ref int nextNodeId)
        {
            node.NodeId = nextNodeId++;
            foreach (var recipe in node.Recipes)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    AssignRecursive(ingredient, ref nextNodeId);
                }
            }
        }
    }
}
