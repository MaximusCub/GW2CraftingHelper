using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Contracts
{
    public class CraftingTreeNode
    {
        private IReadOnlyList<CraftingTreeNode> _children = Array.Empty<CraftingTreeNode>();

        public int ItemId { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public int Quantity { get; set; }
        public CraftingDecision Decision { get; set; }
        public int? RecipeId { get; set; }
        public long? UnitCost { get; set; }
        public long? SubtreeCost { get; set; }

        public IReadOnlyList<CraftingTreeNode> Children
        {
            get => _children;
            set => _children = value ?? Array.Empty<CraftingTreeNode>();
        }
    }
}
