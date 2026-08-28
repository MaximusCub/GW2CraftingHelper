using System.Collections.Generic;
using Newtonsoft.Json;

namespace TaimisToolbench.Models
{
    internal class RecipeNode
    {
        public int Id { get; set; }

        public string IngredientType { get; set; }

        public int Quantity { get; set; }

        public int NodeId { get; set; }

        public List<RecipeOption> Recipes { get; set; } = new List<RecipeOption>();

        // Computed, not stored - Newtonsoft would
        // otherwise write this into every persisted plan.json even though
        // it can never be assigned back on load (no setter). [JsonIgnore]
        // keeps the on-disk schema to genuine state only; behavior is
        // unchanged either way (a read-only computed property was always
        // silently skipped on deserialize, this just also skips it on
        // serialize).
        [JsonIgnore]
        public bool IsLeaf => Recipes.Count == 0;

        // Achievement-bit ingredient dedup (KNOWN-ISSUES #26, gw2e
        // parity): set once, at tree-build time (RecipeService.BuildNodeAsync),
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
