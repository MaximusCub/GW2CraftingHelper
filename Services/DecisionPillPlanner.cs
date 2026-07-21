using System.Collections.Generic;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public enum PillKind
    {
        Selected,
        Available,
        Locked,
        Have
    }

    public struct PillSpec
    {
        public string Text;
        public AcquisitionSource? Source; // non-null => clickable
        public PillKind Kind;
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
                specs.Add(new PillSpec { Text = "HAVE", Source = null, Kind = PillKind.Have });
                return specs;
            }
            if (node.Decision == CraftingDecision.Currency)
            {
                specs.Add(new PillSpec { Text = "CURRENCY", Source = null, Kind = PillKind.Locked });
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
                specs.Add(new PillSpec { Text = badgeText, Source = null, Kind = PillKind.Locked });
                return specs;
            }
            if (options.Count == 1)
            {
                specs.Add(new PillSpec { Text = options[0].text, Source = null, Kind = PillKind.Locked });
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
                specs.Add(new PillSpec
                {
                    Text = opt.text,
                    // The selected pill is already the active choice -
                    // clicking it would be a no-op re-solve, so it is
                    // rendered non-interactive rather than wired up.
                    Source = selected ? (AcquisitionSource?)null : opt.src,
                    Kind = selected ? PillKind.Selected : PillKind.Available
                });
            }
            return specs;
        }
    }
}
