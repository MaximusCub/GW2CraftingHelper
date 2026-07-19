using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Contracts
{
    public class CraftingTreeNode
    {
        private IReadOnlyList<CraftingTreeNode> _children = Array.Empty<CraftingTreeNode>();

        public int ItemId { get; set; }

        // Structural solver node id (internal plumbing for override maps;
        // never displayed). Stable for a given tree shape.
        public int NodeId { get; set; }

        public string Name { get; set; }
        public string IconUrl { get; set; }

        // GW2 API rarity string (e.g. "Fine", "Exotic"); null/empty = unknown.
        public string Rarity { get; set; }

        public int Quantity { get; set; }
        public CraftingDecision Decision { get; set; }

        // Feasible acquisition paths for this node (drives override cycling).
        public bool CanCraft { get; set; }
        public bool CanBuyTp { get; set; }
        public bool CanBuyVendor { get; set; }

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
