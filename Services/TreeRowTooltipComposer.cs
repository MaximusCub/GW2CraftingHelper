using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Builds a Recipe Tree row's extra tooltip lines (unit-price line(s),
    /// the AUDIT ROW 20/38 TP price-side-fallback caveat, the Unknown/
    /// GuildUpgrade acquisition hint, the receipt/what-if caption, and the
    /// "Right-click: Open wiki page" affordance line) - moved verbatim out
    /// of <c>TreeSectionController.RenderTreeNode</c> (tree-tooltip-composer
    /// milestone; docs/ARCHITECTURE.md section 5's STANDING RULE). Pure,
    /// Blish-free string shaping so this row-tooltip logic is directly
    /// unit-testable without a live <c>Panel</c>/<c>BasicTooltipText</c>,
    /// matching this repo's established pattern for tree-rendering text
    /// (DecisionPillPlanner, ValueDetailTooltipBuilder,
    /// ShoppingRowTooltipFormatter, ...).
    ///
    /// Deliberately excludes the wiki-link RIGHT-CLICK WIRING itself
    /// (<c>rowPanel.RightMouseButtonPressed</c>/<c>MouseLeft</c>/
    /// <c>RightMouseButtonReleased</c>) - that is Blish-bound event wiring
    /// and stays in <c>TreeSectionController.RenderTreeNode</c>, gated by
    /// the identical <see cref="WikiLinkBuilder.HasWikiPage"/> predicate
    /// this class also calls to decide whether to append the tooltip line.
    /// Calling that cheap pure predicate twice per row (once here, once in
    /// the caller) is intentional - it keeps this class free of any Blish
    /// dependency rather than threading a bool back out for one call site.
    /// </summary>
    public static class TreeRowTooltipComposer
    {
        /// <summary>
        /// Returns a fresh, never-null list (empty when nothing applies) so
        /// the caller can hand it straight to <c>UpdateTreeRowTooltip</c>
        /// and capture the SAME list in its settle re-ellipsis closure,
        /// matching <c>RenderTreeNode</c>'s own "computed once, reused
        /// verbatim" comment on <c>extraTooltipLines</c> - only the "is the
        /// name actually truncated" line is reconsidered on resize, never
        /// this list's contents.
        /// </summary>
        public static List<string> BuildExtraTooltipLines(
            CraftingTreeNode node,
            string captionText,
            PlanViewModel currentPlan)
        {
            var extraTooltipLines = new List<string>();
            if (node == null)
            {
                return extraTooltipLines;
            }

            if (node.Quantity > 1 &&
                (node.Decision == CraftingDecision.BuyFromTp ||
                 node.Decision == CraftingDecision.BuyFromVendor))
            {
                // Field-test finding B: a pure-currency vendor offer
                // (spirit shards, karma, ...) has UnitCost == 0 (not null -
                // see CraftingTreeBuilder.BuildNode), which used to render a
                // misleading "0g 0s 0c" instead of the real per-unit
                // currency cost; a mixed coin+currency offer still shows
                // both lines below. The coin line is suppressed only when
                // it is genuinely zero AND a currency cost exists to show
                // instead of it.
                bool hasCurrencyCosts = node.VendorCurrencyCosts != null && node.VendorCurrencyCosts.Count > 0;
                if (node.UnitCost.HasValue && !(node.UnitCost.Value == 0 && hasCurrencyCosts))
                {
                    extraTooltipLines.Add("Unit price: " + FormatCoin(node.UnitCost.Value));
                }
                if (node.Decision == CraftingDecision.BuyFromVendor && hasCurrencyCosts)
                {
                    var unitCurrencyAmounts = CurrencyDisplayResolver.ResolveTreeNodeUnitAmounts(
                        node.VendorCurrencyCosts, node.Quantity, currentPlan?.CurrencyMetadata);
                    if (unitCurrencyAmounts != null)
                    {
                        foreach (var amount in unitCurrencyAmounts)
                        {
                            string amountText = amount.BundleLabel ?? amount.Amount.ToString();
                            extraTooltipLines.Add($"Unit price: {amountText} {amount.Name}");
                        }
                    }
                }
            }

            // AUDIT ROW 20/38 (gw2e price-side fallback parity, DISPLAY
            // CAVEAT): this node's TP unit price came from the item's
            // NON-preferred side because the preferred side had no
            // listings (CraftingTreeBuilder.BuildNode/
            // SolverDecision.PriceSideFellBack) - flag it so the number
            // shown doesn't read as an ordinary preferred-side price.
            // Deliberately outside the node.Quantity > 1 gate above: this
            // caveat is about WHICH TP side priced the node, not about a
            // qty=1 row already showing its own total as the unit price.
            //
            // also covers a BuyFromVendor
            // cost-component leaf (node.IsCostComponent) whose own TP-
            // valued barter price fell back the same way - see
            // VendorItemCostLine.PriceSideFellBack and
            // CraftingTreeBuilder.BuildVendorCostComponentLeaves.
            //
            // Also: also covers a plain
            // BuyFromVendor node with no cost-component leaves at all (a
            // pure item-barter offer, kindCount==1 - the common case - or
            // any offer VendorComponentCostsUnreliable suppressed leaf
            // synthesis for) - CraftingTreeBuilder.BuildNode sets that
            // node's own PriceSideFellBack (OR across VendorItemCosts) in
            // that case, so `node.Decision ==
            // CraftingDecision.BuyFromVendor` on its own already covers
            // both this case and the cost-component leaf above (a leaf's
            // own Decision is always BuyFromVendor too - see
            // BuildVendorCostComponentLeaves) - IsCostComponent is kept as
            // an explicit disjunct anyway to document both producers by
            // name rather than rely on that overlap implicitly.
            //
            // Also:
            // BuildNode's parent-flag check widened further to also cover a
            // BuyFromVendor node that DID get component leaves (2+ cost
            // kinds) - its own coin total still includes the fallen-back
            // item's value, and that parent node is exactly what renders
            // collapsed by default a couple of levels deep
            // (PlanContentHeightMath.IsNodeExpanded caps expansion at
            // depth < 2), hiding the leaf-only caveat. `node.Decision ==
            // CraftingDecision.BuyFromVendor` reads the widened parent flag
            // with no further gate changes; the leaf below it (if any)
            // keeps carrying its own flag too, so both rows can show a
            // caveat with no double-render (they are separate tooltip
            // lines on separate rows).
            //
            // (Misattributed caveat text on vendor
            // rows): a BuyFromVendor PARENT's PriceSideFellBack is never
            // about ITS OWN item's TP price - that node was not bought on
            // the TP at all - it is an aggregate ("did any barter cost
            // line fall back") folded up from VendorItemCosts so the
            // caveat is reachable even when the offending leaf renders
            // collapsed (see the BuildNode comments this mirrors). Reusing
            // the BuyFromTp/cost-component-leaf sentence here asserted THIS
            // row's item has no buy orders on the preferred side, which is
            // false in general: the row's own item may have a perfectly
            // healthy TP presence, or none at all - only one of its vendor
            // cost items fell back. A BuyFromTp node and an IsCostComponent
            // leaf both keep the original sentence unchanged - for those
            // two, the flag genuinely describes the row's own price. A
            // plain BuyFromVendor parent (not itself a cost-component leaf
            // - a leaf's Decision is always BuyFromVendor too, so this is
            // an explicit "not a leaf" carve-out, checked first) gets a
            // distinct sentence naming the component instead of the row.
            //
            // currentPlan is a
            // real nullable parameter here (moved verbatim from
            // TreeSectionController's own hoisted _getCurrentPlan()
            // local), so when it IS null neither ternary below can know
            // the actual PriceBasis - reading that as false and picking
            // the InstantBuy-unavailable wording would be an unearned
            // claim about which side fell back. A null plan instead gets a
            // basis-agnostic sentence that states only the fact this code
            // does know (the node's price came from the other TP side).
            if (node.PriceSideFellBack &&
                (node.Decision == CraftingDecision.BuyFromTp || node.IsCostComponent))
            {
                extraTooltipLines.Add(currentPlan == null
                    ? "Other trading post price side shown"
                    : currentPlan.PriceBasis == PriceBasis.BuyOrder
                        ? "Buy-order price unavailable - instant-buy price shown"
                        : "Instant-buy price unavailable - buy-order price shown");
            }
            else if (node.PriceSideFellBack && node.Decision == CraftingDecision.BuyFromVendor)
            {
                extraTooltipLines.Add(currentPlan == null
                    ? "A vendor cost item's other trading post price side shown"
                    : currentPlan.PriceBasis == PriceBasis.BuyOrder
                        ? "A vendor cost item's buy-order price is unavailable - its instant-buy price is used"
                        : "A vendor cost item's instant-buy price is unavailable - its buy-order price is used");
            }

            // guildupgrade-ingredients fix: a GuildUpgrade node's
            // acquisition-hint-style explanation (see CraftingTreeBuilder's
            // "GuildUpgrade" branch) shares this same tooltip line as the
            // Unknown case - both are "no priceable source, here is why"
            // text, just for a different reason.
            if ((node.Decision == CraftingDecision.Unknown || node.Decision == CraftingDecision.GuildUpgrade) &&
                !string.IsNullOrEmpty(node.AcquisitionHint))
            {
                extraTooltipLines.Add(node.AcquisitionHint);
            }

            // UI-bundle milestone, Feature C (receipt/what-if captions):
            // sanctioned tooltip fallback. Inserted at the front, ahead of
            // any unit-price/caveat lines already in extraTooltipLines -
            // but on a row whose label got ellipsized, UpdateTreeRowTooltip
            // itself prepends the full item name ahead of everything
            // already in extraTooltipLines, so on those rows the caption
            // reads second, after the name line, not first.
            if (!string.IsNullOrEmpty(captionText))
            {
                extraTooltipLines.Insert(0, captionText);
            }

            // The tooltip-line
            // half of the affordance only - see this class's own doc
            // comment for why the actual right-click wiring stays in
            // TreeSectionController. WikiLinkBuilder.HasWikiPage/
            // BuildItemPageUrl additionally suppress the affordance
            // entirely for the known placeholder names (see
            // WikiLinkBuilder's SentinelNames), which never resolve to a
            // real page at all.
            if (WikiLinkBuilder.HasWikiPage(node.Name))
            {
                extraTooltipLines.Add("Right-click: Open wiki page");
            }

            return extraTooltipLines;
        }

        // Deliberately duplicates CoinCurrencyRenderer.FormatCoinText's
        // plain "Xg Ys Zc" FORMAT rather than referencing it - that class
        // lives in Views.Rendering and is Blish-coupled, while this class
        // must stay Blish-free to remain unit-testable (repo invariant,
        // same precedent as ValueDetailTooltipBuilder.FormatCoin). The
        // split itself is shared via CoinSegmentMath.Split; only the
        // trivial format string is kept in lockstep in spirit.
        private static string FormatCoin(long copper)
        {
            var (gold, silver, cop) = CoinSegmentMath.Split(copper);
            return $"{gold}g {silver}s {cop}c";
        }
    }
}
