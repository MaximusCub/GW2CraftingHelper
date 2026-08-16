using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models
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

        // How many units of this node's OWN demand were covered by owned
        // inventory during reduction (M34-B2a #1, gw2e parity groundwork -
        // see InventoryReducer.ReducedTreeResult.OwnedQuantityUsedByNode).
        // 0 when reduction never ran (no snapshot) or nothing owned was
        // consumed for this node. Quantity + OwnedQuantityUsed recovers the
        // node's original pre-reduction demand. This makes a PARTIALLY-owned
        // node representable (Quantity > 0 but OwnedQuantityUsed > 0) -
        // previously only fully-owned nodes (Quantity reduced to 0 ->
        // Decision.Have) were visible at all.
        public int OwnedQuantityUsed { get; set; }

        // True when the user manually marked this item's id "Ignore" (M34-
        // B2b, gw2e parity - see PlanSolver's ignoredItemIds parameter and
        // the "IGNORE"/"IGNORED" pill). Distinct from genuine full ownership
        // (Quantity == 0 via real inventory reduction): CraftingTreeBuilder
        // sets Decision = Have for an ignored node too (its cost is zero and
        // it generates no crafting step, same as a truly-owned node), but
        // this flag lets the pill layer still show an active, clickable
        // "IGNORED" toggle alongside HAVE instead of the plain single HAVE
        // pill a naturally-owned node gets.
        public bool IsIgnored { get; set; }

        // True when this exact tree occurrence was zeroed by
        // AchievementBitDedupPrePass (M37, KNOWN-ISSUES #26) because the
        // same item id is already being counted elsewhere in the tree (an
        // earlier achievement-bit occurrence of itself, or a plain/normal
        // occurrence of the same id anywhere). Like IsIgnored, this
        // coexists with Decision == Have (Quantity == 0) but means
        // something different: nothing here is actually owned - the item
        // still needs to be obtained once, just not counted twice. The
        // pill layer renders a distinct, non-interactive "COUNTED
        // ELSEWHERE" annotation instead of the plain HAVE a genuinely-owned
        // node gets (see DecisionPillPlanner).
        public bool IsAchievementBitDeduped { get; set; }

        // Feasible acquisition paths for this node (drives override cycling).
        public bool CanCraft { get; set; }
        public bool CanBuyTp { get; set; }
        public bool CanBuyVendor { get; set; }

        public int? RecipeId { get; set; }
        public long? UnitCost { get; set; }
        public long? SubtreeCost { get; set; }

        // currency-ux-package (Feature 3): passthrough of
        // SolverDecision.ComparisonValue for this node's committed
        // decision - see that property's own doc comment. DECISION-ONLY
        // (repo invariant, restated here since this is the field the tree
        // renderer's value-detail tooltip reads directly): SubtreeCost
        // above remains the sole real/displayed cost for this node;
        // DecisionValue exists only to explain, on hover, why a CRAFT/
        // BuyFromVendor decision won when it diverges from SubtreeCost -
        // never to be shown as, or folded into, a displayed total. Equal
        // to SubtreeCost whenever no currency valuation contributed
        // anywhere in this node's own subtree (the common case).
        public long? DecisionValue { get; set; }

        // AUDIT ROW 20/38 (gw2e price-side fallback parity): true when this
        // node's UnitCost above came from an item's NON-preferred TP side
        // because the preferred side (per the plan's PriceBasis) had no
        // listings - see PlanSolver.GetUnitPrice's fallback overload. Three
        // distinct producers, all gated the same way at their own source:
        // (1) a plain BuyFromTp node, from SolverDecision.PriceSideFellBack
        // (CraftingTreeBuilder.BuildNode gates this to Decision ==
        // BuyFromTp only); (2) a BuyFromVendor cost-component leaf
        // (IsCostComponent) representing a TP-valued Item barter line, from
        // VendorItemCostLine.PriceSideFellBack (review-fix,
        // BuildVendorCostComponentLeaves) - the leaf's OWN price, not the
        // parent vendor node's; (3) review-fix round 3 (DISPLAY CAVEAT gap),
        // widened round 7 (multi-kind offers): a BuyFromVendor node - the OR
        // of every VendorItemCosts line's own PriceSideFellBack, set
        // regardless of whether that node also got cost-component leaves
        // (CraftingTreeBuilder.BuildNode, right after componentLeaves is
        // computed), so both the parent AND a leaf beneath it can carry the
        // flag at once with no double-counting (they are separate nodes).
        // Always false for every other node, including a currency
        // cost-component leaf (never TP-priced). The recipe-tree renderer
        // reads this to add a caveat to the unit-price tooltip - a DIFFERENT
        // sentence for case (3) than for cases (1)/(2) (review-fix round 8):
        // a BuyFromVendor parent's flag describes one of ITS cost items
        // falling back, not the row's own item, so the wording names the
        // component rather than asserting the row's own price fell back -
        // see TreeSectionController.RenderTreeNode's tooltip-gate comment.
        public bool PriceSideFellBack { get; set; }

        // Non-coin currency cost of a BuyFromVendor decision (see
        // SolverDecision.VendorCurrencyCosts). Null for every other
        // Decision, and also null for a BuyFromVendor decision whose offer
        // was purely coin-priced (nothing to report). Internal ids only -
        // a later display task is responsible for resolving these to
        // names/icons before render.
        public IReadOnlyList<CostLine> VendorCurrencyCosts { get; set; }

        // True when this node was bought (TP/vendor) but ALSO has a known
        // recipe, so Children holds gw2e's "what it would cost to craft
        // instead" reference branch rather than an actual crafting step.
        // The view renders these dimmed and collapsed by default.
        //
        // W4B review-fix: for a BuyFromVendor node that ALSO synthesized
        // cost-component leaves (IsCostComponent children), Children is a
        // STACK of both - the component leaves first, then the reference
        // branch's own recipe ingredients appended after (see
        // CraftingTreeBuilder.BuildNode) - not one or the other. This flag
        // still means exactly "Children includes the reference-branch
        // ingredients", just no longer implies Children is EXCLUSIVELY the
        // reference branch in that mixed case.
        public bool IsReferenceBranch { get; set; }

        // W4B (vendor cost-component leaves): true only for a DISPLAY-ONLY
        // synthetic leaf CraftingTreeBuilder.BuildVendorCostComponentLeaves
        // creates under a BuyFromVendor node whose winning offer mixed 2+
        // cost kinds (coin / non-coin currency / TP-valued item) - never
        // set on a real solver-backed node. Default false, additive, so an
        // old plan.json simply deserializes every existing node with this
        // false (renders exactly as before W4B) until the plan is
        // regenerated. Downstream effects of this flag:
        // - DecisionPillPlanner.BuildPillSpecs gives it ONLY the
        //   informational "OWN n" badge (from ComponentOwnedQuantity
        //   below, shown only when n > 0) and/or a "CURRENCY" badge (when
        //   this leaf's cost cell is blank), or no pill at all - never a
        //   decision pill, never Ignore, never override-clickable. (W4B
        //   2026-08-15: "OWN n" replaced the earlier HAVE/"HAVE N/M
        //   NEEDED" vocabulary - see DecisionPillPlanner.BuildPillSpecs'
        //   own doc comment for why.)
        // - It corresponds to no RecipeNode at all (it is never fed back
        //   into PlanSolver.Evaluate/CollectPresetOverrides, which walk the
        //   SOLVER tree exclusively - see CraftingPlanPipeline.
        //   CollectPresetOverrides), so it cannot affect a solver decision.
        public bool IsCostComponent { get; set; }

        // W4B: informational-only "how much of this cost component's own
        // Quantity do you already own" count, populated ONLY when
        // IsCostComponent is true (0 otherwise). Unlike OwnedQuantityUsed
        // above, this NEVER reduces Quantity or any displayed cost - a
        // cost component is a fact about what the winning vendor offer
        // charges, not a shopping demand InventoryReducer ever sees (see
        // AccountCurrencyIndex/AccountItemIndex's own "cosmetic
        // reconciliation, never consulted by the solver" precedent, which
        // this reuses for both currency and item components). Deliberately
        // a separate field from OwnedQuantityUsed (whose own doc comment's
        // "Quantity + OwnedQuantityUsed recovers the original pre-
        // reduction demand" contract does not hold here - Quantity on a
        // cost-component leaf is never reduced at all).
        public int ComponentOwnedQuantity { get; set; }

        // Wiki-derived acquisition guidance (see AcquisitionHintService),
        // set for Decision == Unknown nodes with a seeded hint. Also set
        // (guildupgrade-ingredients fix) for Decision == GuildUpgrade
        // nodes with a fixed, non-seeded explanation - see
        // CraftingTreeBuilder's "GuildUpgrade" branch - since that
        // decision has the same "no priceable source, here is why" shape
        // as Unknown but no AcquisitionHintService entry to draw from.
        // Tooltip-only text, never an id.
        public string AcquisitionHint { get; set; }

        // Short pill label (e.g. "SALVAGE", "EXPLORE") for the same seeded
        // hint entry as AcquisitionHint, set only under the same
        // Decision == Unknown guard. Null/empty when the hint has no badge
        // (or no hint at all) - the view falls back to "UNKNOWN".
        public string AcquisitionBadge { get; set; }

        public IReadOnlyList<CraftingTreeNode> Children
        {
            get => _children;
            set => _children = value ?? Array.Empty<CraftingTreeNode>();
        }
    }
}
