using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class CraftingTreeBuilder
    {
        public CraftingTreeNode BuildTree(
            RecipeNode root,
            IReadOnlyDictionary<int, SolverDecision> decisions,
            IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            return BuildNode(root, decisions, metadata);
        }

        private static CraftingTreeNode BuildNode(
            RecipeNode node,
            IReadOnlyDictionary<int, SolverDecision> decisions,
            IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            var treeNode = new CraftingTreeNode
            {
                ItemId = node.Id,
                NodeId = node.NodeId,
                Name = ResolveName(node.Id, metadata),
                IconUrl = ResolveIcon(node.Id, metadata),
                Rarity = ResolveRarity(node.Id, metadata),
                Quantity = node.Quantity
            };

            // Quantity-zero nodes are already owned
            if (node.Quantity == 0)
            {
                treeNode.Decision = CraftingDecision.Have;
                return treeNode;
            }

            // Currency nodes are leaf nodes
            if (node.IngredientType != "Item")
            {
                treeNode.Decision = CraftingDecision.Currency;
                treeNode.Name = Gw2Constants.ResolveCurrencyName(node.Id);
                return treeNode;
            }

            // Look up solver decision by NodeId
            if (!decisions.TryGetValue(node.NodeId, out var decision))
            {
                treeNode.Decision = CraftingDecision.Unknown;
                return treeNode;
            }

            treeNode.Decision = MapSource(decision.Source);
            treeNode.SubtreeCost = decision.TotalCost;
            treeNode.CanCraft = decision.CanCraft;
            treeNode.CanBuyTp = decision.CanBuyTp;
            treeNode.CanBuyVendor = decision.CanBuyVendor;

            if (decision.Source == AcquisitionSource.BuyFromTp ||
                decision.Source == AcquisitionSource.BuyFromVendor)
            {
                treeNode.UnitCost = (decision.TotalCost.HasValue && node.Quantity > 0)
                    ? decision.TotalCost.Value / node.Quantity
                    : (long?)null;
            }

            if (decision.Source == AcquisitionSource.Craft)
            {
                var recipe = node.Recipes.FirstOrDefault(r => r.RecipeId == decision.RecipeId);
                if (recipe != null)
                {
                    treeNode.RecipeId = recipe.RecipeId;
                    var children = new List<CraftingTreeNode>(recipe.Ingredients.Count);
                    foreach (var ingredient in recipe.Ingredients)
                    {
                        children.Add(BuildNode(ingredient, decisions, metadata));
                    }
                    treeNode.Children = children;
                }
            }

            return treeNode;
        }

        private static CraftingDecision MapSource(AcquisitionSource source)
        {
            switch (source)
            {
                case AcquisitionSource.Craft: return CraftingDecision.Craft;
                case AcquisitionSource.BuyFromTp: return CraftingDecision.BuyFromTp;
                case AcquisitionSource.BuyFromVendor: return CraftingDecision.BuyFromVendor;
                default: return CraftingDecision.Unknown;
            }
        }

        private static string ResolveName(
            int id, IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            if (metadata != null &&
                metadata.TryGetValue(id, out var meta) &&
                !string.IsNullOrEmpty(meta.Name))
            {
                return meta.Name;
            }
            return "Unknown Item";
        }

        private static string ResolveIcon(
            int id, IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            if (metadata != null &&
                metadata.TryGetValue(id, out var meta))
            {
                return meta.IconUrl;
            }
            return null;
        }

        private static string ResolveRarity(
            int id, IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            if (metadata != null &&
                metadata.TryGetValue(id, out var meta))
            {
                return meta.Rarity;
            }
            return null;
        }

        public static void CollectTreeItemIds(
            RecipeNode node,
            IReadOnlyDictionary<int, SolverDecision> decisions,
            HashSet<int> ids)
        {
            if (node.IngredientType == "Item")
            {
                ids.Add(node.Id);
            }

            if (!decisions.TryGetValue(node.NodeId, out var d))
            {
                return;
            }

            if (d.Source != AcquisitionSource.Craft)
            {
                return;
            }

            var recipe = node.Recipes.FirstOrDefault(r => r.RecipeId == d.RecipeId);
            if (recipe == null)
            {
                return;
            }

            foreach (var ing in recipe.Ingredients)
            {
                CollectTreeItemIds(ing, decisions, ids);
            }
        }
    }
}
