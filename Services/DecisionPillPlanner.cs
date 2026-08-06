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

        // Non-interactive "USING N OF M OWNED" annotation (M34-B2b, gw2e's
        // "Using N owned materials" pill; field-test finding A widened the
        // text to show the original total demand alongside the owned
        // count, not just the remaining-need count alone - see
        // AppendOwnershipPills' doc comment) - informational only, never
        // clickable, coexists alongside whichever source pill(s) this node
        // already has (see BuildPillSpecs).
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
        /// count itself is the affordance. HAVE/CURRENCY/UNKNOWN are always
        /// single, non-interactive pills (no AcquisitionSource value
        /// represents "force use owned materials", so there is nothing to
        /// override to).
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
        public static List<PillSpec> BuildPillSpecs(CraftingTreeNode node)
        {
            var specs = new List<PillSpec>(3);

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
        /// Appends the two owned-materials pills (M34-B2b) shared by every
        /// non-Have/non-Currency return path in BuildPillSpecs: the
        /// non-interactive "USING N OF M OWNED" annotation (only when this
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
        /// demand" contract) removes the ambiguity without changing what
        /// either number means.
        /// </summary>
        private static void AppendOwnershipPills(List<PillSpec> specs, CraftingTreeNode node)
        {
            if (node.OwnedQuantityUsed > 0)
            {
                int totalDemand = node.OwnedQuantityUsed + node.Quantity;
                specs.Add(new PillSpec(
                    $"USING {node.OwnedQuantityUsed} OF {totalDemand} OWNED",
                    null,
                    PillKind.OwnedInfo));
            }
            specs.Add(new PillSpec("IGNORE", null, PillKind.Ignore));
        }
    }
}
