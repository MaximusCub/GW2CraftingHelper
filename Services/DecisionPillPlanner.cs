using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public enum PillKind
    {
        Selected,
        Available,
        Locked,
        Have,

        // Non-interactive "HAVE N/M NEEDED" annotation (M34-B2b, gw2e's
        // "Using N owned materials" pill; field-test finding A widened the
        // text to show the original total demand alongside the owned
        // count, not just the remaining-need count alone, and the
        // maintainer's final wording pass (2026-08-06) moved OWNED away
        // from sitting next to the total - see AppendOwnershipPills' doc
        // comment) - informational only, never clickable, coexists
        // alongside whichever source pill(s) this node already has (see
        // BuildPillSpecs).
        OwnedInfo,

        // Interactive per-item "IGNORE"/"IGNORED" toggle (M34-B2b, gw2e's
        // "Ignore" pill) - clicking marks (or unmarks) this item id as fully
        // in-hand tree-wide for this session's re-solves. Text alone
        // ("IGNORE" vs "IGNORED") carries the current state; Kind stays the
        // same for both so the view can style the two states from one
        // switch arm using CraftingTreeNode.IsIgnored.
        Ignore,

        // Non-interactive "COUNTED ELSEWHERE" annotation (M37, KNOWN-ISSUES
        // #26, gw2e's achievement-bit ingredient dedup) - the sole pill on a
        // node AchievementBitDedupPrePass zeroed. Unlike Ignore, there is
        // nothing for the user to toggle/undo here, so it replaces the
        // plain HAVE pill entirely rather than appending alongside it (a
        // genuinely-owned node's HAVE display must never be confused with
        // "this still needs to be obtained once, just not counted twice").
        AchievementBitDeduped
    }

    public readonly struct PillSpec
    {
        public readonly string Text;
        public readonly AcquisitionSource? Source; // non-null => clickable
        public readonly PillKind Kind;

        public PillSpec(string text, AcquisitionSource? source, PillKind kind)
        {
            Text = text;
            Source = source;
            Kind = kind;
        }
    }

    /// <summary>
    /// Pure decision-to-pill mapping for the recipe tree (gw2e's multi-pill
    /// model, KNOWN-ISSUES #18) - Blish-free so the mapping for every
    /// CanCraft/CanBuyTp/CanBuyVendor combination is directly unit-testable
    /// (see DecisionPillPlannerTests); CraftingPlanView.RenderDecisionPills
    /// is responsible only for turning these specs into actual Panel/Label
    /// controls, never for deciding which pills exist or which is selected.
    /// </summary>
    public static class DecisionPillPlanner
    {
        /// <summary>
        /// One pill per feasible acquisition source: 2-3 pills means a real
        /// choice, exactly 1 pill means the source is locked - the pill
        /// count itself is the affordance. HAVE/CURRENCY/GUILD UPGRADE/
        /// UNRECOGNIZED are always single, non-interactive pills (no
        /// AcquisitionSource value represents "force use owned materials",
        /// "resolve this guild upgrade", or "acquire this unrecognized
        /// ingredient type", so there is nothing to override to). UNKNOWN
        /// (a genuine no-source "Item" node) is the sole exception among the
        /// "locked" group - see AppendOwnershipPills for why it alone also
        /// gets the interactive IGNORE toggle.
        ///
        /// The selected pill (Kind == Selected, or the sole Locked pill
        /// when there is only one option) always matches node.Decision -
        /// the solver's actual committed Source - never an independently
        /// guessed "cheapest looking" option; PlanSolver.Evaluate guarantees
        /// Decision is always one of the true CanCraft/CanBuyTp/CanBuyVendor
        /// flags whenever any of them is true (see PlanSolver.PickCheapest),
        /// so the switch below can never legitimately miss - the default
        /// arm exists purely as a non-crashing safety net for a future
        /// regression, not a real code path today.
        /// </summary>
        /// <param name="node">The tree node to build pills for.</param>
        /// <param name="currencyPlanTotals">
        /// currency-ux-package (Feature 2): PlanViewModel.CurrencyPlanTotals
        /// - the WHOLE plan's real currency need, keyed by currency id.
        /// Null (the default) reproduces this method's pre-Feature-2
        /// behavior exactly - every existing call site/test that omits this
        /// parameter is unaffected.
        /// </param>
        /// <param name="ownedCurrencyAmounts">
        /// PlanViewModel.OwnedCurrencyAmounts - raw wallet holding, keyed by
        /// currency id. Null (the default, same as when no snapshot is
        /// available) suppresses the new HAVE/TOTAL pill entirely rather
        /// than implying 0 owned - see AppendCurrencyOwnershipPill.
        /// </param>
        public static List<PillSpec> BuildPillSpecs(
            CraftingTreeNode node,
            IReadOnlyDictionary<int, long> currencyPlanTotals = null,
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts = null)
        {
            var specs = new List<PillSpec>(3);

            // W4B (vendor cost-component leaves): checked BEFORE every
            // other branch below - a component leaf is a fact about a
            // price, not an acquisition decision, so it must NEVER get a
            // decision pill (CRAFT/TP/VENDOR/UNKNOWN) or the Ignore toggle,
            // and it must not fall into the ordinary Have/OwnedInfo path
            // below (which reads OwnedQuantityUsed - a field this leaf
            // never populates, since its Quantity is never reduced for
            // ownership - see CraftingTreeNode.ComponentOwnedQuantity's own
            // doc comment).
            //
            // Maintainer's field-test finding (2026-08-15): the ordinary
            // blue HAVE/"HAVE x/y NEEDED" vocabulary means "your stock
            // covers this need and reduced the plan cost" everywhere else
            // in the tree; a component leaf's ownership never reduces this
            // line's cost (it is purely informational - see
            // ComponentOwnedQuantity's own doc comment), so reusing HAVE
            // here misled testers. Replaced with a subdued "OWN n" badge -
            // PillKind.OwnedInfo, the SAME muted-gold kind the ordinary
            // partial-ownership annotation already uses (no new color) -
            // showing the raw ComponentOwnedQuantity holding, with no
            // full-vs-partial distinction (coverage never changes this
            // line's cost either way, unlike the ordinary Have/OwnedInfo
            // split). Shown only when there is something to report
            // (holding > 0) - no "OWN 0" clutter.
            //
            // A currency-type component (deliberately blank cost cell -
            // SubtreeCost never set, see BuildVendorCostComponentLeaves'
            // currency-line branch) also gets a "CURRENCY" badge -
            // PillKind.Locked, the SAME kind/text the ordinary currency-
            // ingredient leaf's own pill uses a few lines below - so a
            // glance explains why no gold value is shown. The two badges
            // are independent and may both appear on one leaf.
            if (node.IsCostComponent)
            {
                if (!node.SubtreeCost.HasValue)
                {
                    // currency-ux-package (Feature 2): a currency-type
                    // component's own row-scope OWN badge is REPLACED by
                    // the same plan-scope HAVE/TOTAL pill every ordinary
                    // currency leaf gets below - see
                    // AppendCurrencyOwnershipPill's own doc comment for why
                    // row-scope ComponentOwnedQuantity is deliberately not
                    // used here. An item-type component (the else branch
                    // below, SubtreeCost.HasValue) keeps its OWN badge
                    // unchanged - out of this feature's scope.
                    specs.Add(new PillSpec("CURRENCY", null, PillKind.Locked));
                    AppendCurrencyOwnershipPill(specs, node.ItemId, currencyPlanTotals, ownedCurrencyAmounts);
                }
                else if (node.ComponentOwnedQuantity > 0)
                {
                    specs.Add(new PillSpec(
                        $"OWN {node.ComponentOwnedQuantity}",
                        null,
                        PillKind.OwnedInfo));
                }
                return specs;
            }

            if (node.Decision == CraftingDecision.Have)
            {
                // M37 (KNOWN-ISSUES #26): a dedup-zeroed node gets ONLY the
                // "COUNTED ELSEWHERE" pill, never the plain HAVE a
                // genuinely-owned node gets - nothing here is actually
                // owned, so showing HAVE alongside would be misleading
                // (see PillKind.AchievementBitDeduped's own doc comment).
                if (node.IsAchievementBitDeduped)
                {
                    specs.Add(new PillSpec("COUNTED ELSEWHERE", null, PillKind.AchievementBitDeduped));
                    return specs;
                }
                specs.Add(new PillSpec("HAVE", null, PillKind.Have));
                // A node collapses to Have both for genuine full ownership
                // (Quantity == 0 via real reduction) and for a manually
                // "Ignore"-d item (M34-B2b) - only the latter gets the extra
                // toggle pill, so the user can still un-ignore it. A
                // naturally-owned node has nothing to un-ignore and keeps
                // the single plain HAVE pill unchanged.
                if (node.IsIgnored)
                {
                    specs.Add(new PillSpec("IGNORED", null, PillKind.Ignore));
                }
                return specs;
            }
            if (node.Decision == CraftingDecision.Currency)
            {
                specs.Add(new PillSpec("CURRENCY", null, PillKind.Locked));
                // currency-ux-package (Feature 2): every ordinary currency
                // leaf gets the same plan-scope HAVE/TOTAL pill the
                // cost-component branch above adds.
                AppendCurrencyOwnershipPill(specs, node.ItemId, currencyPlanTotals, ownedCurrencyAmounts);
                return specs;
            }
            // guildupgrade-ingredients fix: a distinct locked pill from
            // CURRENCY (see CraftingDecision.GuildUpgrade's own doc
            // comment for why the two must never share vocabulary) - no
            // AcquisitionSource represents it, so it is single and
            // non-interactive, same as CURRENCY/HAVE.
            if (node.Decision == CraftingDecision.GuildUpgrade)
            {
                specs.Add(new PillSpec("GUILD UPGRADE", null, PillKind.Locked));
                return specs;
            }
            // Adversarial-review fix (guildupgrade-ingredients, second
            // pass): a distinct locked pill from UNKNOWN below - see
            // CraftingDecision.UnrecognizedIngredient's own doc comment for
            // why the two must not share a Decision value. Before this fix,
            // an unrecognized-ingredient-type leaf shared CraftingDecision.
            // Unknown with a genuine no-source "Item" node, fell into the
            // options.Count == 0 branch below, and picked up an interactive
            // IGNORE pill via AppendOwnershipPills - clickable, but keyed by
            // TreeSectionController on this node's raw non-item ItemId, so
            // it either did nothing (no matching "Item" node shares that id)
            // or silently zeroed an unrelated "Item" node's cost tree-wide
            // (a matching id does exist elsewhere). Short-circuiting here,
            // same as GuildUpgrade/Currency above, means this leaf can never
            // reach that branch and never gets the IGNORE pill at all.
            if (node.Decision == CraftingDecision.UnrecognizedIngredient)
            {
                specs.Add(new PillSpec("UNRECOGNIZED", null, PillKind.Locked));
                return specs;
            }

            var options = new List<(AcquisitionSource src, string text)>(3);
            if (node.CanCraft) options.Add((AcquisitionSource.Craft, "CRAFT"));
            if (node.CanBuyTp) options.Add((AcquisitionSource.BuyFromTp, "TP"));
            if (node.CanBuyVendor) options.Add((AcquisitionSource.BuyFromVendor, "VENDOR"));

            if (options.Count == 0)
            {
                // Prefer the seeded wiki hint's badge (e.g. "SALVAGE",
                // "EXPLORE") when one exists - "UNKNOWN" remains the
                // fallback for no-source items with no seeded hint at all.
                string badgeText = !string.IsNullOrEmpty(node.AcquisitionBadge)
                    ? node.AcquisitionBadge
                    : "UNKNOWN";
                specs.Add(new PillSpec(badgeText, null, PillKind.Locked));
                AppendOwnershipPills(specs, node);
                return specs;
            }
            if (options.Count == 1)
            {
                specs.Add(new PillSpec(options[0].text, null, PillKind.Locked));
                AppendOwnershipPills(specs, node);
                return specs;
            }

            AcquisitionSource current;
            switch (node.Decision)
            {
                case CraftingDecision.Craft: current = AcquisitionSource.Craft; break;
                case CraftingDecision.BuyFromTp: current = AcquisitionSource.BuyFromTp; break;
                case CraftingDecision.BuyFromVendor: current = AcquisitionSource.BuyFromVendor; break;
                default: current = options[0].src; break; // defensive; solver always matches one of the options
            }

            foreach (var opt in options)
            {
                bool selected = opt.src == current;
                specs.Add(new PillSpec(
                    opt.text,
                    // The selected pill is already the active choice -
                    // clicking it would be a no-op re-solve, so it is
                    // rendered non-interactive rather than wired up.
                    selected ? (AcquisitionSource?)null : opt.src,
                    selected ? PillKind.Selected : PillKind.Available));
            }
            AppendOwnershipPills(specs, node);
            return specs;
        }

        /// <summary>
        /// currency-ux-package (Feature 2, maintainer's own design):
        /// appends the plan-scope "HAVE {have}/{planTotal} TOTAL" pill
        /// shared by every currency leaf (ordinary and cost-component
        /// alike) - have = whole-plan wallet holding,
        /// planTotal = the WHOLE plan's need for this currency id
        /// (PlanViewModel.CurrencyPlanTotals, itself
        /// CraftingPlanResult.Plan.CurrencyCosts). BOTH numbers are
        /// plan-scope facts, deliberately NOT this row's own need
        /// (node.Quantity) - the identical pill text is therefore truthful
        /// at every tree occurrence of the same currency id, unlike an
        /// item pill's row-scope "NEEDED" wording. Omitted entirely when
        /// <paramref name="ownedCurrencyAmounts"/> is null (no wallet
        /// snapshot at all - "have" is genuinely unknown, not zero) or does
        /// not contain this currency id, mirroring how the ordinary item
        /// OwnedInfo pill is only ever added when there is real ownership
        /// data to report.
        ///
        /// FULL COVERAGE (have &gt;= planTotal) collapses to the plain blue
        /// PillKind.Have pill with just "HAVE" - the same kind/text an
        /// ordinary fully-owned item gets - matching item-pill vocabulary
        /// per the maintainer's design; the counts move into the tooltip
        /// (see TreeSectionController.RenderDecisionPills). Partial or zero
        /// coverage keeps the "HAVE {have}/{planTotal} TOTAL" text on a
        /// PillKind.OwnedInfo pill - the "TOTAL" suffix (vs. the ordinary
        /// item pill's "NEEDED" suffix) is deliberate, distinguishing this
        /// plan-scope fact from the item pills' row-scope NEEDED.
        /// </summary>
        private static void AppendCurrencyOwnershipPill(
            List<PillSpec> specs,
            int currencyId,
            IReadOnlyDictionary<int, long> currencyPlanTotals,
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts)
        {
            if (ownedCurrencyAmounts == null || !ownedCurrencyAmounts.TryGetValue(currencyId, out int have))
            {
                return;
            }

            // currency-ux-package review fix (finding 3, MEASURED): a real,
            // positive plan total is required before either pill variant is
            // emitted - `currencyPlanTotals` (plan.CurrencyCosts) and
            // `ownedCurrencyAmounts` (CraftingPlanPipeline.BuildOwnedCurrencyAmounts,
            // deliberately widened beyond plan.CurrencyCosts to every vendor
            // offer in the tree - see that method's own doc comment) have
            // different key sets by construction: a currency leaf reachable
            // only through a non-chosen reference branch (never walked by
            // Collect, so never in plan.CurrencyCosts) can have an entry in
            // ownedCurrencyAmounts with no corresponding plan total at all.
            // The old `long planTotal = 0; TryGetValue(...)` default made
            // that case indistinguishable from "plan genuinely needs zero of
            // this currency" - with have=0 too, `0 &gt;= 0` rendered a plain
            // blue "HAVE" (full coverage) pill and a "fully covered" tooltip
            // for a currency the plan never asked for and the wallet may not
            // even hold, on any currency id vendor-offer-widening surfaced.
            if (currencyPlanTotals == null ||
                !currencyPlanTotals.TryGetValue(currencyId, out long planTotal) ||
                planTotal <= 0)
            {
                return;
            }

            if (have >= planTotal)
            {
                specs.Add(new PillSpec("HAVE", null, PillKind.Have));
            }
            else
            {
                specs.Add(new PillSpec($"HAVE {have}/{planTotal} TOTAL", null, PillKind.OwnedInfo));
            }
        }

        /// <summary>
        /// Appends the two owned-materials pills (M34-B2b) shared by every
        /// non-Have/non-Currency return path in BuildPillSpecs: the
        /// non-interactive "HAVE N/M NEEDED" annotation (only when this
        /// node's own demand was actually partly covered by real inventory -
        /// see CraftingTreeNode.OwnedQuantityUsed's doc comment) and the
        /// interactive "IGNORE" toggle (offered on every real item node
        /// regardless of ownership, matching gw2e's own always-offered
        /// "Ignore" pill - Section 3.2 of the r2 report). A node this method
        /// is called for is, by construction, never already ignored (an
        /// ignored node's Decision is Have, handled separately above), so
        /// the toggle always starts from its "IGNORE" (not yet active) text.
        ///
        /// Field-test finding A: the pill used to read "USING N OWNED"
        /// showing only the covered count, while node.Quantity (the tree
        /// row's own "Nx" prefix) already shows the REMAINING need after
        /// that coverage was subtracted - e.g. "120x ... USING 130 OWNED"
        /// read as a paradox (130 owned covering only 120 needed?) when the
        /// true original demand was actually 250. Spelling out the total
        /// (OwnedQuantityUsed + Quantity, per CraftingTreeNode's own "Quantity
        /// + OwnedQuantityUsed recovers the node's original pre-reduction
        /// demand" contract) removed the ambiguity without changing what
        /// either number means - but the fixed text ("USING {used} OF
        /// {total} OWNED") still put OWNED immediately beside the total, and
        /// the maintainer's final wording pass (2026-08-06) found testers
        /// misreading {total} itself as an owned count. "HAVE {used}/{total}
        /// NEEDED" keeps both numbers but drops OWNED from beside the total
        /// and reuses the vocabulary of the existing full-coverage HAVE
        /// pill instead of inventing a third one.
        /// </summary>
        private static void AppendOwnershipPills(List<PillSpec> specs, CraftingTreeNode node)
        {
            if (node.OwnedQuantityUsed > 0)
            {
                int totalDemand = node.OwnedQuantityUsed + node.Quantity;
                specs.Add(new PillSpec(
                    $"HAVE {node.OwnedQuantityUsed}/{totalDemand} NEEDED",
                    null,
                    PillKind.OwnedInfo));
            }
            specs.Add(new PillSpec("IGNORE", null, PillKind.Ignore));
        }
    }
}
