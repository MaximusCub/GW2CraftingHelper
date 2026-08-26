using System;
using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    internal class CraftingTreeBuilder
    {
        /// <summary>
        /// Build-invariant lookup state threaded through every BuildNode/
        /// BuildChildren recursion, constructed once per BuildTree() call.
        /// The node/recipe under construction and insideReferenceBranch
        /// vary per call and stay plain parameters. Field semantics match
        /// the same-named BuildTree() parameters.
        /// </summary>
        private sealed class BuildContext
        {
            public IReadOnlyDictionary<int, SolverDecision> Decisions { get; }

            public IReadOnlyDictionary<int, ItemMetadata> Metadata { get; }

            public IReadOnlyDictionary<int, AcquisitionHint> Hints { get; }

            public IReadOnlyDictionary<int, int> OwnedQuantityUsedByNodeId { get; }

            public ISet<int> IgnoredItemIds { get; }

            public IReadOnlyDictionary<int, CurrencyMetadata> CurrencyMetadata { get; }

            public IReadOnlyDictionary<int, int> OwnedCurrencyAmounts { get; }

            public IReadOnlyDictionary<int, int> OwnedVendorItemAmounts { get; }

            public BuildContext(
                IReadOnlyDictionary<int, SolverDecision> decisions,
                IReadOnlyDictionary<int, ItemMetadata> metadata,
                IReadOnlyDictionary<int, AcquisitionHint> hints,
                IReadOnlyDictionary<int, int> ownedQuantityUsedByNodeId,
                ISet<int> ignoredItemIds,
                IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata,
                IReadOnlyDictionary<int, int> ownedCurrencyAmounts,
                IReadOnlyDictionary<int, int> ownedVendorItemAmounts)
            {
                Decisions = decisions;
                Metadata = metadata;
                Hints = hints;
                OwnedQuantityUsedByNodeId = ownedQuantityUsedByNodeId;
                IgnoredItemIds = ignoredItemIds;
                CurrencyMetadata = currencyMetadata;
                OwnedCurrencyAmounts = ownedCurrencyAmounts;
                OwnedVendorItemAmounts = ownedVendorItemAmounts;
            }
        }

        public CraftingTreeNode BuildTree(
            RecipeNode root,
            IReadOnlyDictionary<int, SolverDecision> decisions,
            IReadOnlyDictionary<int, ItemMetadata> metadata,
            IReadOnlyDictionary<int, AcquisitionHint> hints = null,
            IReadOnlyDictionary<int, int> ownedQuantityUsedByNodeId = null,
            ISet<int> ignoredItemIds = null,
            // All three optional/null-tolerant: a caller that omits them
            // gets no currency icon/name or HAVE-pill data on synthesized
            // component leaves, never a crash or a missing leaf.
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata = null,
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts = null,
            IReadOnlyDictionary<int, int> ownedVendorItemAmounts = null)
        {
            var ctx = new BuildContext(
                decisions, metadata, hints, ownedQuantityUsedByNodeId,
                ignoredItemIds, currencyMetadata, ownedCurrencyAmounts, ownedVendorItemAmounts);
            var rootNode = BuildNode(root, ctx, insideReferenceBranch: false);

            // Marked here rather than inside BuildNode: this method is the
            // only place that knows which node the caller asked for a tree
            // OF - a multi-item batch calls it once per requested root, so
            // each of those N nodes is a plan root too.
            rootNode.IsPlanRoot = true;
            return rootNode;
        }

        private static CraftingTreeNode BuildNode(
            RecipeNode node,
            BuildContext ctx,
            bool insideReferenceBranch)
        {
            var treeNode = new CraftingTreeNode
            {
                ItemId = node.Id,
                NodeId = node.NodeId,
                Name = ResolveName(node.Id, ctx.Metadata),
                IconUrl = ResolveIcon(node.Id, ctx.Metadata),
                Rarity = ResolveRarity(node.Id, ctx.Metadata),
                Quantity = node.Quantity,
                // Set uniformly for every node (including the early
                // returns below), from the NodeId assigned by the Solve()
                // call that produced the context's decisions.
                OwnedQuantityUsed = ctx.OwnedQuantityUsedByNodeId != null &&
                    ctx.OwnedQuantityUsedByNodeId.TryGetValue(node.NodeId, out int ownedUsed)
                        ? ownedUsed
                        : 0,
            };

            // Quantity-zero nodes are already owned - or zeroed by
            // AchievementBitDedupPrePass because the id is counted
            // elsewhere in the tree. Both collapse to the same Have
            // display; IsAchievementBitDeduped alone distinguishes them
            // for the pill layer.
            if (node.Quantity == 0)
            {
                treeNode.Decision = CraftingDecision.Have;
                treeNode.IsAchievementBitDeduped = node.IsAchievementBitDeduped;
                return treeNode;
            }

            // A manually "Ignore"-d item id collapses to the same Have
            // display a genuinely-owned node gets; IsIgnored alone
            // distinguishes the two for the pill layer.
            if (node.IngredientType == "Item" &&
                ctx.IgnoredItemIds != null && ctx.IgnoredItemIds.Contains(node.Id))
            {
                treeNode.Decision = CraftingDecision.Have;
                treeNode.IsIgnored = true;
                return treeNode;
            }

            // GuildUpgrade nodes are leaves: a distinct id space from item
            // and currency ids (see CraftingDecision). IconUrl/Rarity are
            // cleared explicitly - `metadata` can carry a colliding
            // item-keyed entry via the widened metadata fetch. IDs are
            // never displayed (repo invariant), so a generic label plus
            // AcquisitionHint stands in for a name. Full guild-decoration
            // support is out of scope - see KNOWN-ISSUES #54.
            if (node.IngredientType == "GuildUpgrade")
            {
                treeNode.Decision = CraftingDecision.GuildUpgrade;
                treeNode.Name = "Guild upgrade (unresolved)";
                treeNode.IconUrl = null;
                treeNode.Rarity = null;
                treeNode.AcquisitionHint =
                    "Requires a claimed Guild Hall upgrade. This module does not " +
                    "yet resolve guild upgrade names or verify ownership.";
                return treeNode;
            }

            // Currency nodes are leaves. Name/IconUrl resolve through
            // CurrencyDisplayResolver, never the item-keyed lookup: a
            // wallet currency id is a distinct id space with a real seed
            // collision (id 24 - see CraftingDecision). Rarity is always
            // null. Scoped to the literal "Currency" only; any other
            // non-Item type falls through to UnrecognizedIngredient.
            if (node.IngredientType == "Currency")
            {
                treeNode.Decision = CraftingDecision.Currency;
                treeNode.Name = CurrencyDisplayResolver.ResolveName(node.Id, ctx.CurrencyMetadata);
                treeNode.IconUrl = CurrencyDisplayResolver.ResolveIconUrl(node.Id, ctx.CurrencyMetadata);
                treeNode.Rarity = null;
                return treeNode;
            }

            // Any type that is neither "Item", "GuildUpgrade", nor
            // "Currency" lands here, hoisted before the decisions lookup
            // so it never resolves from the item-domain metadata/hints
            // dictionaries (cross-domain id collision - see
            // CraftingDecision). Gets its own UnrecognizedIngredient value
            // rather than sharing Unknown: a shared value once routed this
            // node to the interactive IGNORE pill, whose click could
            // silently zero an unrelated "Item" node with the same
            // numeric id.
            if (node.IngredientType != "Item")
            {
                treeNode.Decision = CraftingDecision.UnrecognizedIngredient;
                treeNode.Name = "Unrecognized ingredient type";
                treeNode.IconUrl = null;
                treeNode.Rarity = null;
                return treeNode;
            }

            // Look up solver decision by NodeId
            if (!ctx.Decisions.TryGetValue(node.NodeId, out var decision))
            {
                treeNode.Decision = CraftingDecision.Unknown;
                ApplyAcquisitionHint(treeNode, ctx.Hints);
                return treeNode;
            }

            treeNode.Decision = MapSource(decision.Source);
            treeNode.SubtreeCost = decision.TotalCost;
            // Decision-only, see CraftingTreeNode.DecisionValue.
            treeNode.DecisionValue = decision.ComparisonValue;
            treeNode.VendorComponentCostsUnreliable = decision.VendorComponentCostsUnreliable;
            treeNode.CanCraft = decision.CanCraft;
            treeNode.CanBuyTp = decision.CanBuyTp;
            treeNode.CanBuyVendor = decision.CanBuyVendor;
            treeNode.CraftCostBreakdown = decision.CraftCostBreakdown;
            treeNode.BuyFromTpCostBreakdown = decision.BuyFromTpCostBreakdown;
            treeNode.BuyFromVendorCostBreakdown = decision.BuyFromVendorCostBreakdown;
            // Straight passthrough, consumed by CompetencyOpportunityCalculator.
            treeNode.CraftExcludedByCompetency = decision.CraftExcludedByCompetency;
            treeNode.CraftExcludedRealCost = decision.CraftExcludedRealCost;
            treeNode.CraftExcludedDisciplines = decision.CraftExcludedDisciplines;
            treeNode.CraftExcludedMinRating = decision.CraftExcludedMinRating;
            treeNode.CheapestCraftUntrained = decision.CheapestCraftUntrained;
            treeNode.CheapestCraftRealCost = decision.CheapestCraftRealCost;
            treeNode.CheapestCraftDisciplines = decision.CheapestCraftDisciplines;
            treeNode.CheapestCraftMinRating = decision.CheapestCraftMinRating;
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

            // PriceSideFellBack is already gated to BuyFromTp by Commit;
            // the guard is repeated here to match this method's own
            // explicit-Source convention.
            treeNode.PriceSideFellBack = decision.Source == AcquisitionSource.BuyFromTp &&
                decision.PriceSideFellBack;

            if (decision.Source == AcquisitionSource.Craft)
            {
                var recipe = node.Recipes.FirstOrDefault(r => r.RecipeId == decision.RecipeId);
                if (recipe != null)
                {
                    treeNode.RecipeId = recipe.RecipeId;
                    // Batch shape of this occurrence's chosen recipe - see
                    // CraftingTreeNode.CraftsNeeded for why this can
                    // exceed treeNode.Quantity.
                    treeNode.CraftsNeeded = recipe.CraftsNeeded;
                    treeNode.RecipeOutputCount = recipe.OutputCount;
                    // The same basis CraftsNeeded above was derived from.
                    treeNode.RecipeExpectedOutputCount = recipe.ExpectedOutputCount;
                    // Propagate insideReferenceBranch as-is: a Craft
                    // decision reached inside a reference branch is still
                    // hypothetical and must keep suppressing further
                    // reference branches below it.
                    treeNode.Children = BuildChildren(recipe, ctx, insideReferenceBranch);
                }
            }
            else
            {
                // A BuyFromVendor node whose winning offer mixed 2+ cost
                // kinds gets display-only component-leaf children instead
                // of the reference branch (see
                // BuildVendorCostComponentLeaves); null falls through to
                // the reference-branch logic unchanged.
                //
                // VendorComponentCostsUnreliable suppresses leaf synthesis
                // entirely: the pre-merge numbers no longer sum to the
                // node's corrected SubtreeCost, and a leaf built from them
                // would break the parent-total-equals-sum-of-visible-parts
                // guarantee. The node still shows its correct SubtreeCost.
                List<CraftingTreeNode> componentLeaves = decision.Source == AcquisitionSource.BuyFromVendor &&
                    !decision.VendorComponentCostsUnreliable
                    ? BuildVendorCostComponentLeaves(node.NodeId, decision, ctx)
                    : null;

                // A BuyFromVendor node's own coin cost always includes
                // every barter item's value, whether or not that item got
                // a component leaf - so the parent needs this flag
                // regardless of componentLeaves (a leaf-only carrier is
                // unreachable when the node renders collapsed). OR across
                // every VendorItemCosts line: any one barter item having
                // fallen back warrants the caveat. The per-line flag stays
                // meaningful even when VendorComponentCostsUnreliable
                // makes Quantity/GoldValue stale - which TP side priced
                // the item is independent of the batch-cost reallocation.
                if (decision.Source == AcquisitionSource.BuyFromVendor &&
                    decision.VendorItemCosts != null)
                {
                    treeNode.PriceSideFellBack = decision.VendorItemCosts.Any(line => line.PriceSideFellBack);
                }

                // Reference branch: gw2e's "what it would cost to craft
                // instead" - informational, built from recipe[0] since
                // nothing was crafted here. Evaluate always walks every
                // recipe's ingredients, so their decisions already exist.
                //
                // Capped to AT MOST ONE reference branch per root-to-leaf
                // path: everything below passes insideReferenceBranch=true.
                // Do not relax this to reset on Craft steps - GW2 data
                // commonly alternates buyable-with-a-recipe <-> craft down
                // a chain, and letting reference branches restart at every
                // alternation hangs effectively unboundedly on real deep
                // items (Deldrimor Steel Ingot).
                bool wantsReferenceBranch = !insideReferenceBranch &&
                    (decision.Source == AcquisitionSource.BuyFromTp ||
                     decision.Source == AcquisitionSource.BuyFromVendor) &&
                    node.Recipes.Count > 0;

                if (componentLeaves != null)
                {
                    // A vendor node that also has a recipe must not lose
                    // its reference-branch comparison just because it got
                    // component leaves - stack them, component leaves
                    // first so TreeSectionController's
                    // `Children[0].IsCostComponent` check keeps working.
                    if (wantsReferenceBranch)
                    {
                        var referenceChildren = BuildChildren(
                            node.Recipes[0], ctx, insideReferenceBranch: true);
                        componentLeaves.AddRange(referenceChildren);
                        treeNode.IsReferenceBranch = true;
                        ApplyReferenceRecipeInfo(treeNode, node.Recipes[0]);
                    }

                    treeNode.Children = componentLeaves;
                }
                else if (wantsReferenceBranch)
                {
                    treeNode.Children = BuildChildren(
                        node.Recipes[0], ctx, insideReferenceBranch: true);
                    treeNode.IsReferenceBranch = true;
                    ApplyReferenceRecipeInfo(treeNode, node.Recipes[0]);
                }
            }

            ApplyAcquisitionHint(treeNode, ctx.Hints);
            return treeNode;
        }

        /// <summary>
        /// Surfaces the reference branch's own recipe (node.Recipes[0])
        /// onto the tree node, so RecipeSheetSavingsCalculator can check
        /// its Disciplines/MinRating/Flags without re-walking the solver's
        /// RecipeNode tree.
        /// </summary>
        private static void ApplyReferenceRecipeInfo(CraftingTreeNode treeNode, RecipeOption referenceRecipe)
        {
            treeNode.ReferenceRecipeId = referenceRecipe.RecipeId;
            treeNode.ReferenceRecipeDisciplines = referenceRecipe.Disciplines;
            treeNode.ReferenceRecipeMinRating = referenceRecipe.MinRating;
            treeNode.ReferenceRecipeIsLearnedFromItem =
                referenceRecipe.Flags != null && referenceRecipe.Flags.Contains("LearnedFromItem");
        }

        /// <summary>
        /// Synthesizes display-only child leaves for a BuyFromVendor node
        /// whose winning offer mixed 2+ cost kinds (coin / non-coin
        /// currency / TP-valued item); a single-kind offer (the vast
        /// majority) returns null and the caller falls back to the
        /// reference branch.
        ///
        /// A raw coin component never gets its own leaf - it stays folded
        /// into the parent's SubtreeCost, which keeps "parent total = sum
        /// of the parts a leaf can show" true (currency leaves have blank
        /// cost cells by design; only item leaves must visibly sum).
        ///
        /// Every number on a leaf is read from
        /// decision.VendorCurrencyCosts/VendorItemCosts - nothing is
        /// recomputed, so a leaf's displayed amount can never drift from
        /// the parent's SubtreeCost.
        /// </summary>
        private static List<CraftingTreeNode> BuildVendorCostComponentLeaves(
            int parentNodeId,
            SolverDecision decision,
            BuildContext ctx)
        {
            int currencyCount = decision.VendorCurrencyCosts?.Count ?? 0;
            int itemCount = decision.VendorItemCosts?.Count ?? 0;
            int kindCount = (currencyCount > 0 ? 1 : 0) + (itemCount > 0 ? 1 : 0) + (decision.VendorHasRawCoin ? 1 : 0);
            if (kindCount < 2)
            {
                return null;
            }

            var leaves = new List<CraftingTreeNode>(itemCount + currencyCount);
            int componentIndex = 0;

            // Item components first (the piece the collapsed parent's coin
            // total was hiding) - see SyntheticComponentNodeId for the
            // collision-safety argument behind the id each leaf gets.
            if (decision.VendorItemCosts != null)
            {
                foreach (var line in decision.VendorItemCosts)
                {
                    leaves.Add(new CraftingTreeNode
                    {
                        ItemId = line.ItemId,
                        NodeId = SyntheticComponentNodeId(parentNodeId, componentIndex++),
                        Name = ResolveName(line.ItemId, ctx.Metadata),
                        IconUrl = ResolveIcon(line.ItemId, ctx.Metadata),
                        Rarity = ResolveRarity(line.ItemId, ctx.Metadata),
                        Quantity = line.Quantity,
                        Decision = CraftingDecision.BuyFromVendor,
                        IsCostComponent = true,
                        // The exact gold value already folded into the
                        // parent's own SubtreeCost for this line - see this
                        // method's own doc comment.
                        SubtreeCost = line.GoldValue,
                        UnitCost = line.Quantity > 0 ? line.GoldValue / line.Quantity : (long?)null,
                        ComponentOwnedQuantity = ResolveOwnedQuantity(line.ItemId, ctx.OwnedVendorItemAmounts),
                        // This leaf's UnitCost came from the barter item's
                        // TP price, which can itself have fallen back to
                        // the non-preferred side; threaded through so the
                        // fell-back-price tooltip caveat catches this leaf
                        // too.
                        PriceSideFellBack = line.PriceSideFellBack,
                    });
                }
            }

            // Currency components: cost cell deliberately blank - the
            // quantity itself IS the cost, and an invented
            // currency-to-gold rate would violate the repo's
            // never-invent-exchange-rates invariant. Name/icon resolve via
            // CurrencyDisplayResolver, same as the currency summary rows.
            if (decision.VendorCurrencyCosts != null)
            {
                foreach (var line in decision.VendorCurrencyCosts)
                {
                    leaves.Add(new CraftingTreeNode
                    {
                        ItemId = line.Id,
                        NodeId = SyntheticComponentNodeId(parentNodeId, componentIndex++),
                        Name = CurrencyDisplayResolver.ResolveName(line.Id, ctx.CurrencyMetadata),
                        IconUrl = CurrencyDisplayResolver.ResolveIconUrl(line.Id, ctx.CurrencyMetadata),
                        Quantity = line.Count,
                        Decision = CraftingDecision.BuyFromVendor,
                        IsCostComponent = true,
                        ComponentOwnedQuantity = ResolveOwnedQuantity(line.Id, ctx.OwnedCurrencyAmounts),
                    });
                }
            }

            return leaves;
        }

        /// <summary>
        /// Deterministic, stable id for a synthetic component leaf -
        /// always negative, so it cannot collide with a real
        /// RecipeNodeIds-assigned id (always non-negative). Stable across
        /// re-solves because both inputs are: the parent's NodeId is
        /// reused verbatim, and componentIndex is the leaf's fixed
        /// position within the offer's never-re-ordered CostLines. The
        /// x1000 spacing dwarfs any real cost-line count.
        /// </summary>
        private static int SyntheticComponentNodeId(int parentNodeId, int componentIndex)
        {
            return -(parentNodeId * 1000 + componentIndex + 1);
        }

        /// <summary>
        /// Informational "OWN n" badge value for a cost-component leaf -
        /// the RAW holding, never clamped to the line's need: the badge
        /// states a wallet/inventory fact, and clamping would misstate the
        /// holding (a 300-essence wallet once rendered "OWN 250" against a
        /// 250-cost line). Never influences Quantity/SubtreeCost.
        /// </summary>
        private static int ResolveOwnedQuantity(
            int id, IReadOnlyDictionary<int, int> ownedAmounts)
        {
            if (ownedAmounts == null || !ownedAmounts.TryGetValue(id, out int owned))
            {
                return 0;
            }

            return owned;
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
            BuildContext ctx,
            bool insideReferenceBranch)
        {
            var children = new List<CraftingTreeNode>(recipe.Ingredients.Count);
            foreach (var ingredient in recipe.Ingredients)
            {
                children.Add(BuildNode(ingredient, ctx, insideReferenceBranch));
            }

            return children;
        }

        /// <summary>
        /// The single bridge between the solver's <see cref="AcquisitionSource"/> vocabulary
        /// and the display-layer <see cref="CraftingDecision"/> vocabulary.
        /// <see cref="AcquisitionSource.UnknownSource"/> has its own explicit arm: it is a
        /// genuinely reachable production value ("Not sold or crafted") and must keep mapping
        /// to <see cref="CraftingDecision.Unknown"/>. <see cref="AcquisitionSource.Currency"/>
        /// deliberately has no arm - the caller sets the Currency decision directly for a
        /// non-"Item" node before any decision lookup, so reaching this method with it means
        /// that invariant broke. <c>default</c> throws so a future member added without a
        /// matching arm fails loudly instead of quietly displaying as Unknown.
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
            // The synthetic wrapper's sentinel id is not a real item and
            // must never trigger a metadata fetch; the recursion still
            // walks past it into the N real item roots.
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
