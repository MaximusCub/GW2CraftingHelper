using System;
using System.Collections.Generic;
using System.Linq;
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
            ISet<int> ignoredItemIds = null,
            // W4B (vendor cost-component leaves): all three optional/null-
            // tolerant, same convention as every other cosmetic lookup on
            // this method - a caller that omits them simply gets no
            // currency icon/name or HAVE-pill data on any synthesized
            // component leaf, never a crash or a missing leaf. See
            // BuildVendorCostComponentLeaves for how each is used.
            // Review-fix (recipe-ingestion-fix, Must Fix): currencyMetadata
            // is also now read by the plain Currency-leaf naming below -
            // see BuildNode's Currency leaf naming for why this closes the
            // whole class of Gw2Constants.KnownCurrencyNames drift rather
            // than just the two ids the review caught.
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata = null,
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts = null,
            IReadOnlyDictionary<int, int> ownedVendorItemAmounts = null)
        {
            return BuildNode(node: root, decisions: decisions, metadata: metadata, hints: hints,
                insideReferenceBranch: false, ownedQuantityUsedByNodeId: ownedQuantityUsedByNodeId,
                ignoredItemIds: ignoredItemIds, currencyMetadata: currencyMetadata,
                ownedCurrencyAmounts: ownedCurrencyAmounts, ownedVendorItemAmounts: ownedVendorItemAmounts);
        }

        private static CraftingTreeNode BuildNode(
            RecipeNode node,
            IReadOnlyDictionary<int, SolverDecision> decisions,
            IReadOnlyDictionary<int, ItemMetadata> metadata,
            IReadOnlyDictionary<int, AcquisitionHint> hints,
            bool insideReferenceBranch,
            IReadOnlyDictionary<int, int> ownedQuantityUsedByNodeId,
            ISet<int> ignoredItemIds,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata,
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts,
            IReadOnlyDictionary<int, int> ownedVendorItemAmounts)
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

            // GuildUpgrade nodes are leaf nodes: the GW2 API tags a Guild
            // Decoration recipe's claimed-guild-hall-upgrade requirement
            // with ingredient type "GuildUpgrade" (e.g. recipe 12002 ->
            // item 80471, upgrade id 829) - a distinct id space from both
            // item and wallet-currency ids, with no defined relationship
            // to either (see CraftingDecision's XML doc for the full
            // collision rationale). IconUrl/Rarity are cleared explicitly
            // rather than left at the item-keyed lookup above: `metadata`
            // can carry a colliding entry via routes other than the
            // Item-only CollectTreeItemIds fetch (CraftingPlanPipeline
            // also unions step/target/used-material/vendor-cost-component
            // ids into it). IDs are never displayed (repo invariant), so
            // this uses a generic label plus an AcquisitionHint instead of
            // resolving a name. Full guild-decoration support (real
            // upgrade name, ownership verification) is out of scope - see
            // docs/KNOWN-ISSUES.md.
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

            // Currency nodes are leaf nodes. Name/IconUrl resolve through
            // CurrencyDisplayResolver (live-preferred, static-fallback -
            // matches PlanViewModelBuilder's Summary/shopping-row currency
            // costs, KNOWN-ISSUES #16) rather than the item-keyed lookup
            // above: a wallet currency id is a distinct id space from item
            // ids, with a real seed collision (item/currency id 24 is both
            // a vendor-offer outputItemId and the KnownCurrencyNames key
            // "Pristine Fractal Relics" - see CraftingDecision's XML doc
            // for the full rationale). Rarity is always null - currencies
            // have no rarity concept. Scoped to the literal string
            // "Currency" only; any other non-Item type falls through to
            // its own UnrecognizedIngredient branch below rather than
            // being labeled Currency on the strength of "not an Item".
            if (node.IngredientType == "Currency")
            {
                treeNode.Decision = CraftingDecision.Currency;
                treeNode.Name = CurrencyDisplayResolver.ResolveName(node.Id, currencyMetadata);
                treeNode.IconUrl = CurrencyDisplayResolver.ResolveIconUrl(node.Id, currencyMetadata);
                treeNode.Rarity = null;
                return treeNode;
            }

            // Any ingredient type that is neither "Item", "GuildUpgrade",
            // nor "Currency" lands here, hoisted before the decisions
            // lookup below (matching where the GuildUpgrade/Currency
            // branches sit) so this is correct by this method's own
            // construction, not merely because PlanSolver's Evaluate never
            // memoizes a non-Item node today. Name/IconUrl/Rarity/
            // AcquisitionHint/AcquisitionBadge are all reset or skipped -
            // every one of them would otherwise resolve from the
            // ITEM-domain `metadata`/`hints` dictionaries keyed on this
            // same raw id, the cross-domain collision risk CraftingDecision's
            // XML doc explains for GuildUpgrade/Currency. Gets its own
            // CraftingDecision.UnrecognizedIngredient value rather than
            // sharing Unknown with a genuine no-source "Item" node:
            // DecisionPillPlanner cannot tell the two Unknown cases apart,
            // and a shared value previously routed this node to the
            // no-options branch's live, interactive IGNORE pill - keyed on
            // this node's non-item ItemId, a no-op click that could
            // silently zero an unrelated "Item" node sharing the same
            // numeric id elsewhere in the tree.
            if (node.IngredientType != "Item")
            {
                treeNode.Decision = CraftingDecision.UnrecognizedIngredient;
                treeNode.Name = "Unrecognized ingredient type";
                treeNode.IconUrl = null;
                treeNode.Rarity = null;
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
            // currency-ux-package (Feature 3): decision-only, see
            // CraftingTreeNode.DecisionValue's own doc comment.
            treeNode.DecisionValue = decision.ComparisonValue;
            // currency-ux-package review fix (finding 4, MEASURED): see
            // CraftingTreeNode.VendorComponentCostsUnreliable's own doc
            // comment.
            treeNode.VendorComponentCostsUnreliable = decision.VendorComponentCostsUnreliable;
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

            // AUDIT ROW 20/38: SolverDecision.PriceSideFellBack is already
            // gated to BuyFromTp-only by PlanSolver's Commit (see that
            // field's doc comment), but the guard is repeated here to match
            // this method's own explicit-Source convention (VendorCurrencyCosts
            // just above) rather than lean on the upstream invariant alone.
            treeNode.PriceSideFellBack = decision.Source == AcquisitionSource.BuyFromTp &&
                decision.PriceSideFellBack;

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
                    treeNode.Children = BuildChildren(
                        recipe, decisions, metadata, hints, insideReferenceBranch, ownedQuantityUsedByNodeId,
                        ignoredItemIds, currencyMetadata, ownedCurrencyAmounts, ownedVendorItemAmounts);
                }
            }
            else
            {
                // W4B: a BuyFromVendor node whose winning offer mixed 2+
                // cost KINDS (coin / non-coin currency / TP-valued item)
                // gets DISPLAY-ONLY component-leaf children instead of the
                // reference branch below - see
                // BuildVendorCostComponentLeaves' own doc comment for why
                // this takes precedence (only reachable when node.Recipes
                // is ALSO non-empty - a vendor-only item with no recipe at
                // all, the common real case, was never going to build a
                // reference branch anyway). Null (fewer than 2 kinds, not a
                // BuyFromVendor decision at all, or VendorComponentCostsUnreliable
                // - see below) falls through to the existing reference-branch
                // logic completely unchanged.
                //
                // W4B review-fix (Critical): VendorComponentCostsUnreliable
                // (SolverDecision's own doc comment) is true whenever this
                // occurrence's decision.VendorItemCosts/VendorCurrencyCosts
                // are pre-merge numbers PlanSolver's AllocateVendorNodeCosts
                // pass has since reallocated a DIFFERENT (corrected)
                // TotalCost around - i.e. this item was needed via 2+ tree
                // occurrences that merged into one true batched vendor
                // purchase. Synthesizing a leaf from those stale numbers
                // would show a component cost that no longer sums to (and
                // can even exceed) this node's own corrected SubtreeCost -
                // suppressing leaf synthesis entirely for that case keeps
                // the parent-total-equals-sum-of-visible-parts guarantee
                // exact rather than approximate. The node still shows its
                // own correct (reallocated) SubtreeCost either way.
                List<CraftingTreeNode> componentLeaves = decision.Source == AcquisitionSource.BuyFromVendor &&
                    !decision.VendorComponentCostsUnreliable
                    ? BuildVendorCostComponentLeaves(
                        node.NodeId, decision, metadata, currencyMetadata, ownedCurrencyAmounts, ownedVendorItemAmounts)
                    : null;

                // AUDIT ROW 20/38 review-fix (DISPLAY CAVEAT gap, round 3;
                // widened round 7): a BuyFromVendor node's own coin cost
                // (SubtreeCost/UnitCost) always includes every barter item's
                // value, whether or not that item also got a component leaf
                // - so the parent needs this flag regardless of
                // componentLeaves. Round 3 only set it when componentLeaves
                // was null (the common pure-item-barter offer, kindCount==1:
                // itemCount>0, currencyCount==0, VendorHasRawCoin==false -
                // see BuildVendorCostComponentLeaves' own kindCount gate -
                // and a multi-kind offer suppressed by
                // VendorComponentCostsUnreliable), leaving a 2+-kind offer
                // that DID get leaves with a hard-false parent flag even
                // though the caveat belongs on the coin total either way.
                // That leaf-only carrier is unreachable whenever the node
                // renders collapsed (PlanContentHeightMath.IsNodeExpanded:
                // `!dimmed && depth < 2`, and any non-Craft ancestor forces
                // childDimmed for everything beneath it) - exactly the
                // common case for a vendor node nested a couple of levels
                // down. OR across every VendorItemCosts line rather than
                // reading just one: any one barter item having fallen back
                // is enough to want the caveat on the node whose own coin
                // cost already includes it. The per-line flag stays
                // meaningful even when VendorComponentCostsUnreliable makes
                // the line's Quantity/GoldValue stale (SolverDecision.
                // VendorComponentCostsUnreliable's own doc comment) - which
                // TP side priced that item is independent of the later
                // batch-cost reallocation - so reading it here is safe. The
                // leaf keeps its own flag too (unchanged) - the tooltip gate
                // already tolerates both being true, they render as separate
                // rows, no double-render.
                if (decision.Source == AcquisitionSource.BuyFromVendor &&
                    decision.VendorItemCosts != null)
                {
                    treeNode.PriceSideFellBack = decision.VendorItemCosts.Any(line => line.PriceSideFellBack);
                }

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
                bool wantsReferenceBranch = !insideReferenceBranch &&
                    (decision.Source == AcquisitionSource.BuyFromTp ||
                     decision.Source == AcquisitionSource.BuyFromVendor) &&
                    node.Recipes.Count > 0;

                if (componentLeaves != null)
                {
                    // W4B review-fix (Must Fix): a vendor node whose cost
                    // ALSO has a recipe (so it would otherwise get a
                    // reference branch) must not silently lose that
                    // comparison just because it also got component leaves
                    // - STACK them instead of picking one, component leaves
                    // first so TreeSectionController's
                    // `Children[0].IsCostComponent` cost-cell-suppression
                    // check (unchanged, still correct) keeps working. The
                    // reference-branch ingredients are appended as
                    // additional, ordinary children - IsReferenceBranch is
                    // purely informational today (not read by any renderer;
                    // dimming already comes from node.Decision != Craft
                    // uniformly for every child, component leaf or not), so
                    // mixing the two lists is safe.
                    if (wantsReferenceBranch)
                    {
                        var referenceChildren = BuildChildren(
                            node.Recipes[0], decisions, metadata, hints, insideReferenceBranch: true,
                            ownedQuantityUsedByNodeId: ownedQuantityUsedByNodeId, ignoredItemIds: ignoredItemIds,
                            currencyMetadata: currencyMetadata, ownedCurrencyAmounts: ownedCurrencyAmounts,
                            ownedVendorItemAmounts: ownedVendorItemAmounts);
                        componentLeaves.AddRange(referenceChildren);
                        treeNode.IsReferenceBranch = true;
                    }
                    treeNode.Children = componentLeaves;
                }
                else if (wantsReferenceBranch)
                {
                    treeNode.Children = BuildChildren(
                        node.Recipes[0], decisions, metadata, hints, insideReferenceBranch: true,
                        ownedQuantityUsedByNodeId: ownedQuantityUsedByNodeId, ignoredItemIds: ignoredItemIds,
                        currencyMetadata: currencyMetadata, ownedCurrencyAmounts: ownedCurrencyAmounts,
                        ownedVendorItemAmounts: ownedVendorItemAmounts);
                    treeNode.IsReferenceBranch = true;
                }
            }

            ApplyAcquisitionHint(treeNode, hints);
            return treeNode;
        }

        /// <summary>
        /// W4B (vendor cost-component leaves): synthesizes DISPLAY-ONLY
        /// child leaves for a BuyFromVendor node whose winning offer mixed
        /// 2+ cost KINDS (coin / non-coin currency / TP-valued item) - the
        /// real field case that motivated this (an "Amalgamated Rift
        /// Essence" vendor cost inside an Endless Summer plan: 3 wallet
        /// currencies + Globs of Ectoplasm) was rendering as one very long
        /// segmented cost cell that collided with the row layout AND hid
        /// that part of the "gold" total was actually paid in items, not
        /// coin. Returns null when the winning offer had fewer than 2
        /// kinds, so the caller falls back to the existing reference-branch
        /// behavior completely unchanged (this is also why a pure-coin or
        /// pure-currency or pure-item offer - the overwhelming majority of
        /// vendor offers - never gets any leaves at all: each of those has
        /// exactly one kind).
        ///
        /// A raw coin component (decision.VendorHasRawCoin) is deliberately
        /// NEVER given its own leaf, even when it is one of the 2+ kinds
        /// that triggered synthesis - it simply stays folded into the
        /// parent's own SubtreeCost exactly as it already is today. This is
        /// the simplest presentation that still keeps "parent total = sum
        /// of the parts a leaf can show" true: a currency leaf's own cost
        /// cell is blank by design (the quantity IS the cost - see the
        /// currency-leaf comment below), so the only leaves that need to
        /// visibly sum to anything are the item leaves, and coin needs no
        /// leaf to be accounted for (it is just money, already visible in
        /// the parent's own total).
        ///
        /// Every number placed on a leaf here is READ from
        /// decision.VendorCurrencyCosts/VendorItemCosts - fields
        /// VendorBatchSolver.EvaluateVendorOffers already computed for this
        /// exact tree occurrence and folded into decision.TotalCost/
        /// ComparisonValue - nothing is recomputed, so a leaf's displayed
        /// amount can never drift from what the parent's own SubtreeCost
        /// already shows (see VendorItemCostLine.GoldValue's own doc
        /// comment for exactly where that number was captured).
        /// </summary>
        private static List<CraftingTreeNode> BuildVendorCostComponentLeaves(
            int parentNodeId,
            SolverDecision decision,
            IReadOnlyDictionary<int, ItemMetadata> metadata,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata,
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts,
            IReadOnlyDictionary<int, int> ownedVendorItemAmounts)
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
                        Name = ResolveName(line.ItemId, metadata),
                        IconUrl = ResolveIcon(line.ItemId, metadata),
                        Rarity = ResolveRarity(line.ItemId, metadata),
                        Quantity = line.Quantity,
                        Decision = CraftingDecision.BuyFromVendor,
                        IsCostComponent = true,
                        // The exact gold value already folded into the
                        // parent's own SubtreeCost for this line - see this
                        // method's own doc comment.
                        SubtreeCost = line.GoldValue,
                        UnitCost = line.Quantity > 0 ? line.GoldValue / line.Quantity : (long?)null,
                        ComponentOwnedQuantity = ResolveOwnedQuantity(line.ItemId, ownedVendorItemAmounts),
                        // AUDIT ROW 20/38 review-fix (DISPLAY CAVEAT gap):
                        // this leaf's UnitCost above came from the barter
                        // item's own TP price (VendorItemCostLine.GoldValue -
                        // see VendorBatchSolver.EvaluateVendorOffers), which
                        // can itself have fallen back to the item's NON-
                        // preferred TP side. Threaded through unchanged so
                        // TreeSectionController's existing fell-back-price
                        // tooltip caveat can also catch this leaf, not just
                        // a plain BuyFromTp node.
                        PriceSideFellBack = line.PriceSideFellBack
                    });
                }
            }

            // Currency components: cost cell deliberately left blank (no
            // SubtreeCost) - the quantity itself IS the cost; showing it a
            // second time as an invented currency-to-gold rate would be
            // circular (repo invariant: never invent exchange rates).
            // Name/icon resolved the same way the currency SUMMARY rows
            // and the tree's own "Unit price:" tooltip already do
            // (CurrencyDisplayResolver, live metadata with the offline
            // Gw2Constants fallback) - not the bare offline-only fallback
            // the solver-tree Currency-ingredient branch above uses, so
            // this leaf shows a real icon whenever currency metadata is
            // available.
            if (decision.VendorCurrencyCosts != null)
            {
                foreach (var line in decision.VendorCurrencyCosts)
                {
                    leaves.Add(new CraftingTreeNode
                    {
                        ItemId = line.Id,
                        NodeId = SyntheticComponentNodeId(parentNodeId, componentIndex++),
                        Name = CurrencyDisplayResolver.ResolveName(line.Id, currencyMetadata),
                        IconUrl = CurrencyDisplayResolver.ResolveIconUrl(line.Id, currencyMetadata),
                        Quantity = line.Count,
                        Decision = CraftingDecision.BuyFromVendor,
                        IsCostComponent = true,
                        ComponentOwnedQuantity = ResolveOwnedQuantity(line.Id, ownedCurrencyAmounts)
                    });
                }
            }

            return leaves;
        }

        /// <summary>
        /// Deterministic, stable id for a W4B synthetic component leaf -
        /// cannot collide with a real RecipeNodeIds-assigned id (always a
        /// small non-negative int, 0..N-1 for an N-node tree - see
        /// RecipeNodeIds.Assign) because this is always negative. Stable
        /// across ResolveWithOverrides rebuilds of the SAME tree because
        /// both inputs are: parentNodeId is the real solver node's own
        /// NodeId, which RecipeNodeIds.Assign fixes once per tree and
        /// PlanSolver.Solve's assignNodeIds:false reuses verbatim on every
        /// local re-solve (see RecipeNodeIds' own doc comment); componentIndex
        /// is this leaf's fixed position within decision.VendorItemCosts/
        /// VendorCurrencyCosts, which are themselves built by iterating the
        /// SAME winning VendorOffer's own CostLines list (a plain List,
        /// never re-ordered) on every re-solve of the same context. The
        /// x1000 spacing is far larger than any real vendor offer's cost
        /// line count (single digits in practice), leaving no realistic
        /// risk of two components of the same parent colliding.
        /// </summary>
        private static int SyntheticComponentNodeId(int parentNodeId, int componentIndex)
        {
            return -(parentNodeId * 1000 + componentIndex + 1);
        }

        /// <summary>
        /// Informational "OWN n" badge value for a W4B cost-component
        /// leaf - the RAW holding (never clamped to the line's need), 0
        /// when there is no ownership data for this id at all. The badge
        /// states a wallet/inventory fact ("you own n"), not a coverage
        /// allocation, so clamping to the component quantity would
        /// misstate the holding (gate finding 2026-08-16: a 300-essence
        /// wallet rendered "OWN 250" against a 250-cost line). Never
        /// influences Quantity/SubtreeCost above - purely what
        /// DecisionPillPlanner reads to decide between showing the badge
        /// (owned > 0) or no pill at all (owned == null/0) - see
        /// CraftingTreeNode.ComponentOwnedQuantity's own doc comment.
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
            IReadOnlyDictionary<int, SolverDecision> decisions,
            IReadOnlyDictionary<int, ItemMetadata> metadata,
            IReadOnlyDictionary<int, AcquisitionHint> hints,
            bool insideReferenceBranch,
            IReadOnlyDictionary<int, int> ownedQuantityUsedByNodeId,
            ISet<int> ignoredItemIds,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata,
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts,
            IReadOnlyDictionary<int, int> ownedVendorItemAmounts)
        {
            var children = new List<CraftingTreeNode>(recipe.Ingredients.Count);
            foreach (var ingredient in recipe.Ingredients)
            {
                children.Add(BuildNode(
                    ingredient, decisions, metadata, hints, insideReferenceBranch, ownedQuantityUsedByNodeId,
                    ignoredItemIds, currencyMetadata, ownedCurrencyAmounts, ownedVendorItemAmounts));
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
