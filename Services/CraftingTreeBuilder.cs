using System;
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
            IReadOnlyDictionary<int, AcquisitionHint> hints = null,
            IReadOnlyDictionary<int, int> ownedQuantityUsedByNodeId = null,
            ISet<int> ignoredItemIds = null)
        {
            return BuildNode(node: root, decisions: decisions, metadata: metadata, hints: hints,
                insideReferenceBranch: false, ownedQuantityUsedByNodeId: ownedQuantityUsedByNodeId,
                ignoredItemIds: ignoredItemIds);
        }

        private static CraftingTreeNode BuildNode(
            RecipeNode node,
            IReadOnlyDictionary<int, SolverDecision> decisions,
            IReadOnlyDictionary<int, ItemMetadata> metadata,
            IReadOnlyDictionary<int, AcquisitionHint> hints,
            bool insideReferenceBranch,
            IReadOnlyDictionary<int, int> ownedQuantityUsedByNodeId,
            ISet<int> ignoredItemIds)
        {
            var treeNode = new CraftingTreeNode
            {
                ItemId = node.Id,
                NodeId = node.NodeId,
                Name = ResolveName(node.Id, metadata),
                IconUrl = ResolveIcon(node.Id, metadata),
                Rarity = ResolveRarity(node.Id, metadata),
                Quantity = node.Quantity,
                // M34-B2a #1: set uniformly for every node (including the
                // Have/Currency/Unknown early returns below), from
                // whichever NodeId this node was assigned by the Solve()
                // call that produced `decisions` - see CraftingTreeNode's
                // doc comment.
                OwnedQuantityUsed = ownedQuantityUsedByNodeId != null &&
                    ownedQuantityUsedByNodeId.TryGetValue(node.NodeId, out int ownedUsed)
                        ? ownedUsed
                        : 0
            };

            // Quantity-zero nodes are already owned - OR, M37 (KNOWN-ISSUES
            // #26), zeroed by AchievementBitDedupPrePass because this exact
            // item id is already being counted elsewhere in the tree. Both
            // collapse to the same Have display; IsAchievementBitDeduped is
            // the only thing that distinguishes the two for the pill layer
            // (see DecisionPillPlanner), matching the IsIgnored precedent
            // just below.
            if (node.Quantity == 0)
            {
                treeNode.Decision = CraftingDecision.Have;
                treeNode.IsAchievementBitDeduped = node.IsAchievementBitDeduped;
                return treeNode;
            }

            // M34-B2b: a manually "Ignore"-d item id (per PlanSolver's own
            // matching short-circuit in Evaluate/Collect, which already
            // zeroed this node's cost and generated no step) collapses to
            // the same Have display a genuinely-owned node gets - IsIgnored
            // is the only thing that distinguishes the two for the pill
            // layer (see DecisionPillPlanner). Item-only, matching the
            // solver's own scope decision.
            if (node.IngredientType == "Item" &&
                ignoredItemIds != null && ignoredItemIds.Contains(node.Id))
            {
                treeNode.Decision = CraftingDecision.Have;
                treeNode.IsIgnored = true;
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
                    treeNode.Children = BuildChildren(recipe, decisions, metadata, hints, insideReferenceBranch, ownedQuantityUsedByNodeId, ignoredItemIds);
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
                treeNode.Children = BuildChildren(node.Recipes[0], decisions, metadata, hints, insideReferenceBranch: true, ownedQuantityUsedByNodeId: ownedQuantityUsedByNodeId, ignoredItemIds: ignoredItemIds);
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
            bool insideReferenceBranch,
            IReadOnlyDictionary<int, int> ownedQuantityUsedByNodeId,
            ISet<int> ignoredItemIds)
        {
            var children = new List<CraftingTreeNode>(recipe.Ingredients.Count);
            foreach (var ingredient in recipe.Ingredients)
            {
                children.Add(BuildNode(ingredient, decisions, metadata, hints, insideReferenceBranch, ownedQuantityUsedByNodeId, ignoredItemIds));
            }
            return children;
        }

        /// <summary>
        /// The single bridge between the solver's <see cref="AcquisitionSource"/> vocabulary
        /// and the display-layer <see cref="CraftingDecision"/> vocabulary (M38 DO-NOT-TOUCH
        /// #15 - see both enums' own doc comments for the full per-member mapping and why the
        /// two vocabularies are deliberately kept separate).
        ///
        /// <see cref="AcquisitionSource.UnknownSource"/> has its own explicit arm rather than
        /// falling into <c>default</c> - it is a genuinely reachable production value
        /// (gw2efficiency's "Not sold or crafted": no recipe, no TP price, no vendor offer; see
        /// PlanSolverTests.NoRecipeAndNoPrice_IsUnknownSource_WithAllFlagsFalse), so its mapping
        /// to <see cref="CraftingDecision.Unknown"/> must be preserved verbatim.
        /// <see cref="AcquisitionSource.Currency"/> is deliberately NOT given an arm: it cannot
        /// reach this method today (the caller sets
        /// <see cref="CraftingTreeNode.Decision"/> = <see cref="CraftingDecision.Currency"/>
        /// directly for a non-"Item" node, before any decision lookup happens - see
        /// <see cref="BuildNode"/>), so any call with it would mean that invariant broke.
        /// Falling through to <c>default</c> - which now throws instead of silently returning
        /// <see cref="CraftingDecision.Unknown"/> - is the one intentional behavior change this
        /// method makes (M38 WP-05): it also fails loudly for any future
        /// <see cref="AcquisitionSource"/> member added without a matching arm here, rather
        /// than quietly mis-displaying it as Unknown.
        /// </summary>
        private static CraftingDecision MapSource(AcquisitionSource source)
        {
            switch (source)
            {
                case AcquisitionSource.Craft: return CraftingDecision.Craft;
                case AcquisitionSource.BuyFromTp: return CraftingDecision.BuyFromTp;
                case AcquisitionSource.BuyFromVendor: return CraftingDecision.BuyFromVendor;
                case AcquisitionSource.UnknownSource: return CraftingDecision.Unknown;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(source), source, "Unmapped AcquisitionSource - add a CraftingDecision case above.");
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
            // M35: never collect the synthetic multi-item wrapper's own
            // sentinel id (see Gw2Constants.MultiItemWrapperItemId) - it is
            // not a real item and must never trigger a metadata fetch. The
            // recursion below still walks past it into its recipe's
            // Ingredients (the N real item roots) unaffected.
            if (node.IngredientType == "Item" && node.Id != Gw2Constants.MultiItemWrapperItemId)
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
