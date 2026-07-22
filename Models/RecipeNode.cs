using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class RecipeNode
    {
        public int Id { get; set; }
        public string IngredientType { get; set; }
        public int Quantity { get; set; }
        public int NodeId { get; set; }
        public List<RecipeOption> Recipes { get; set; } = new List<RecipeOption>();
        public bool IsLeaf => Recipes.Count == 0;

        // M37 (KNOWN-ISSUES #26, gw2e parity - achievement-bit ingredient
        // dedup): set once, at tree-build time (RecipeService.BuildNodeAsync),
        // from the matching RawIngredient - see that field's own doc comment.
        // Null for every ordinary ingredient (the vast majority). Preserved
        // across InventoryReducer.CloneNode.
        public int? AchievementId { get; set; }
        public int? AchievementBit { get; set; }

        // True when AchievementBitDedupPrePass zeroed THIS occurrence
        // because the same item id is already being satisfied elsewhere in
        // the tree (another achievement-bit occurrence seen earlier in the
        // same DFS walk, or a plain/normal occurrence of the same id
        // anywhere). Distinct from genuine full ownership (Quantity == 0 via
        // real InventoryReducer consumption): both collapse to
        // CraftingDecision.Have downstream, but only this flag means
        // "nothing here is actually owned - it is just already required
        // elsewhere" (see CraftingTreeBuilder/DecisionPillPlanner's
        // "COUNTED ELSEWHERE" pill). Preserved across InventoryReducer.CloneNode.
        public bool IsAchievementBitDeduped { get; set; }
    }
}
