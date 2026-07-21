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
            IReadOnlyDictionary<int, ItemMetadata> metadata,
            IReadOnlyDictionary<int, AcquisitionHint> hints = null)
        {
            return BuildNode(root, decisions, metadata, hints, insideReferenceBranch: false);
        }

        private static CraftingTreeNode BuildNode(
            RecipeNode node,
            IReadOnlyDictionary<int, SolverDecision> decisions,
            IReadOnlyDictionary<int, ItemMetadata> metadata,
            IReadOnlyDictionary<int, AcquisitionHint> hints,
            bool insideReferenceBranch)
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
                ApplyAcquisitionHint(treeNode, hints);
                return treeNode;
            }

            treeNode.Decision = MapSource(decision.Source);
            treeNode.SubtreeCost = decision.TotalCost;
            treeNode.CanCraft = decision.CanCraft;
            treeNode.CanBuyTp = decision.CanBuyTp;
            treeNode.CanBuyVendor = decision.CanBuyVendor;
            treeNode.VendorCurrencyCosts = decision.Source == AcquisitionSource.BuyFromVendor
                ? decision.VendorCurrencyCosts
                : null;

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
                    // Propagate insideReferenceBranch as-is (not reset to
                    // false): a Craft decision reached WHILE already inside
                    // a reference branch is still hypothetical content, and
                    // must keep suppressing further reference branches
                    // below it - see the cap comment below for why.
                    treeNode.Children = BuildChildren(recipe, decisions, metadata, hints, insideReferenceBranch);
                }
            }
            else if (!insideReferenceBranch &&
                     (decision.Source == AcquisitionSource.BuyFromTp ||
                      decision.Source == AcquisitionSource.BuyFromVendor) &&
                     node.Recipes.Count > 0)
            {
                // Reference branch: gw2e's "what it would cost to craft
                // instead" - informational, not an actual crafting step, so
                // it is built from recipe[0] (the deterministic first
                // option) rather than a "chosen" recipe, since nothing was
                // crafted here. PlanSolver.Evaluate always walks every
                // recipe's ingredients to get a comparison value, even for a
                // node it ultimately decides to buy, so those ingredients'
                // decisions already exist in the dict - safe to recurse into
                // here.
                //
                // Capped to AT MOST ONE reference branch per root-to-leaf
                // path: everything built below here passes
                // insideReferenceBranch=true, which blocks starting another
                // one no matter how many further Craft/Buy-with-recipe
                // decisions alternate beneath it. A naive "reset to
                // not-inside on every Craft step" cap (tried first) does NOT
                // bound this: GW2 crafting data very commonly alternates
                // buyable-with-a-recipe <-> craft <-> buyable-with-a-recipe
                // down a chain, and node.Recipes here is the FULL
                // alternate-recipe graph the upstream RecipeService already
                // expanded for every option (not just the solver's chosen
                // path, which is all this builder walked before) - letting
                // reference branches restart at every such alternation
                // measured as an effectively unbounded hang on a real deep
                // item (Deldrimor Steel Ingot) during manual verification.
                treeNode.Children = BuildChildren(node.Recipes[0], decisions, metadata, hints, insideReferenceBranch: true);
                treeNode.IsReferenceBranch = true;
            }

            ApplyAcquisitionHint(treeNode, hints);
            return treeNode;
        }

        /// <summary>
        /// Sets AcquisitionHint/AcquisitionBadge from the seeded hint
        /// dictionary, but only for Decision == Unknown nodes - hints must
        /// never bleed onto a node that has a real (even if unappealing)
        /// priced source, since the hint text describes how to acquire an
        /// item with NO known source at all. Hint and Badge are set
        /// independently (each only when its own value is non-empty) so a
        /// seed entry can supply one without the other.
        /// </summary>
        private static void ApplyAcquisitionHint(
            CraftingTreeNode treeNode,
            IReadOnlyDictionary<int, AcquisitionHint> hints)
        {
            if (treeNode.Decision != CraftingDecision.Unknown || hints == null)
            {
                return;
            }
            if (!hints.TryGetValue(treeNode.ItemId, out var hint) || hint == null)
            {
                return;
            }
            if (!string.IsNullOrEmpty(hint.Hint))
            {
                treeNode.AcquisitionHint = hint.Hint;
            }
            if (!string.IsNullOrEmpty(hint.Badge))
            {
                treeNode.AcquisitionBadge = hint.Badge;
            }
        }

        private static List<CraftingTreeNode> BuildChildren(
            RecipeOption recipe,
            IReadOnlyDictionary<int, SolverDecision> decisions,
            IReadOnlyDictionary<int, ItemMetadata> metadata,
            IReadOnlyDictionary<int, AcquisitionHint> hints,
            bool insideReferenceBranch)
        {
            var children = new List<CraftingTreeNode>(recipe.Ingredients.Count);
            foreach (var ingredient in recipe.Ingredients)
            {
                children.Add(BuildNode(ingredient, decisions, metadata, hints, insideReferenceBranch));
            }
            return children;
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
