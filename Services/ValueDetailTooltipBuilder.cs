using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Builds the "value-detail" hover
    /// text for a Recipe Tree CRAFT/VENDOR pill whose committed decision's
    /// DECISION-ONLY comparison value (CraftingTreeNode.DecisionValue)
    /// diverges from its displayed real gold cost (SubtreeCost) - i.e. a
    /// valued currency (or an unpriceable descendant's own divergence,
    /// rolled up recursively - see DecisionValue's own doc comment)
    /// contributed to why this decision won. Spec duplicated verbatim from
    /// gw2efficiency's own crafting-pill hover template. Kept Blish-free
    /// (unlike TreeSectionController, which only calls this and assigns
    /// the result to BasicTooltipText) so the actual text-building logic is
    /// directly unit-testable, matching this repo's established pattern for
    /// tree-rendering logic (DecisionPillPlanner, CoinSegmentMath, ...).
    ///
    /// DECISION-ONLY (repo invariant, restated here since this class's
    /// whole purpose is to surface a decision-only figure): every number
    /// this class formats is for a HOVER TOOLTIP only - it must never be
    /// copied into a displayed total anywhere else in the app.
    ///
    /// This hover can never fire for an unpriceable component, only for a
    /// valued currency. PlanSolver.RecomputeComparisonValues
    /// sets ComparisonValue = TotalCost for every fallback-tier decision,
    /// and fallback tier propagates transitively up through every Craft
    /// ancestor, so an unvalued currency or GuildUpgrade ingredient
    /// anywhere in a chosen subtree forces delta = 0 for that node AND
    /// every ancestor above it - TryBuildContent's delta &lt;= 0 guard then
    /// suppresses the hover for the whole chain. Documented here, not
    /// fixed, so a future reader treats this as a known scope limit of the
    /// current solver rollup rather than rediscovering it as a bug.
    /// </summary>
    internal static class ValueDetailTooltipBuilder
    {
        /// <summary>
        /// Attempts to build the value-detail tooltip for <paramref name="node"/>.
        /// Returns false (and a null <paramref name="content"/>) when this
        /// node's decision is not CRAFT/BuyFromVendor, either cost figure is
        /// unavailable, or the two figures do not diverge - the caller must
        /// show nothing in all of those cases, not an empty or misleading
        /// tooltip.
        /// <para>
        /// Each gold figure stays a coin span, which is what lets the rich
        /// tooltip surface draw it with real coin icons instead of spelling
        /// it "1g 23s 45c".
        /// </para>
        /// <para>
        /// Returned UNWRAPPED. The surface that renders this measures and
        /// wraps against a real font at a real pixel width, so pre-breaking
        /// the text here would only fight it. (There was once a second,
        /// plain-string entry point that applied TooltipTextFormat's
        /// character budget; nothing called it and it is gone.)
        /// </para>
        /// </summary>
        public static bool TryBuildContent(
            CraftingTreeNode node,
            IReadOnlyDictionary<int, TimegatedItem> vendorCapsByItemId,
            out TooltipContent content)
        {
            content = null;

            if (node == null)
            {
                return false;
            }

            if (node.Decision != CraftingDecision.Craft && node.Decision != CraftingDecision.BuyFromVendor)
            {
                return false;
            }

            // a currency-type
            // vendor cost-component leaf also has Decision == BuyFromVendor
            // and is kept out today only because BuildVendorCostComponentLeaves
            // happens not to set DecisionValue on that construction path -
            // an incidental, not structural, guarantee. Explicit guard so
            // this can never silently attach to a CURRENCY badge (which
            // has no cost cell at all - see TreeSectionController's own
            // "Paid in a non-coin currency" tooltip for that pill) if that
            // construction path ever changes.
            if (node.IsCostComponent)
            {
                return false;
            }

            // a
            // merged vendor decision's VendorComponentCostsUnreliable is
            // already the documented signal (see that field's own doc
            // comment, and CraftingTreeBuilder's identical guard on
            // synthesizing cost-component leaves) that this node's
            // per-occurrence cost breakdown could not be proven to still
            // sum correctly after AllocateVendorNodeCosts merged 2+ tree
            // occurrences - the same conservative posture applied here so
            // this hover never presents a currency figure it cannot vouch
            // for on that node.
            if (node.VendorComponentCostsUnreliable)
            {
                return false;
            }

            if (!node.DecisionValue.HasValue || !node.SubtreeCost.HasValue)
            {
                return false;
            }

            long realGold = node.SubtreeCost.Value;
            long decisionTotal = node.DecisionValue.Value;
            long delta = decisionTotal - realGold;

            // Shown only when the two numbers diverge (spec). DecisionValue
            // is never less than SubtreeCost by construction (a currency
            // valuation only ever ADDS to the comparison figure - see
            // SolverDecision.ComparisonValue's own doc comment), but the
            // <= guard (not ==) is defensive: a future change that let
            // DecisionValue dip below SubtreeCost should suppress this
            // tooltip rather than print a negative "Currencies:" line.
            if (delta <= 0)
            {
                return false;
            }

            var builder = new TooltipContentBuilder();
            builder.Text("Crafting gold price: ").Coin(realGold, CoinSegmentMath.GameStyleText(realGold)).EndLine();
            builder.Text("Currencies: ").Coin(delta, CoinSegmentMath.GameStyleText(delta)).EndLine();
            builder.Text("This is an estimated opportunity cost for the used currencies in the recipe.").EndLine();
            builder.EndLine();
            builder.Text("Optimization price: ").Coin(decisionTotal, CoinSegmentMath.GameStyleText(decisionTotal));

            // Maintainer-ratified #21 resolution: append the winning
            // vendor offer's purchase cap, when this node's item has one -
            // informational only, matching TimegatedItem's own doc comment
            // (never gates or reroutes the decision).
            if (node.Decision == CraftingDecision.BuyFromVendor &&
                vendorCapsByItemId != null &&
                vendorCapsByItemId.TryGetValue(node.ItemId, out var cap))
            {
                builder.EndLine();
                builder.Text($"Vendor cap: {cap.CapValue} per {CapPeriodText(cap.CapType)}");
            }

            content = builder.Build();
            return true;
        }

        private static string CapPeriodText(TimegatedCapType capType)
        {
            switch (capType)
            {
                case TimegatedCapType.Daily: return "day";
                case TimegatedCapType.Weekly: return "week";
                case TimegatedCapType.Seasonal: return "season";
                default: return "period";
            }
        }
    }
}
