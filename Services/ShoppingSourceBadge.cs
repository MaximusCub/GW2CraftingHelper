using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The short source badge a Shopping List row carries next to its name
    /// (Blish-free so the mapping is directly unit testable; the renderer
    /// only turns the returned text into a tag).
    /// <para>
    /// Every shopping row type returns a badge. A plain Trading Post
    /// purchase used to return null - "no badge" was the only thing saying
    /// "buy this from the TP", which is a meaning the reader had to already
    /// know. With TP badged, an unbadged shopping row is a defect rather
    /// than a silent statement, which is what
    /// <see cref="ForRow"/> returning null now means: a row type the
    /// Shopping List does not emit.
    /// </para>
    /// </summary>
    public static class ShoppingSourceBadge
    {
        public static string ForRow(PlanRowViewModel row)
        {
            if (row == null)
            {
                return null;
            }

            switch (row.RowType)
            {
                case PlanRowType.ShoppingBuy: return "TP";
                case PlanRowType.ShoppingVendor: return "VENDOR";
                case PlanRowType.ShoppingCurrency: return "CURRENCY";
                case PlanRowType.ShoppingUnknown:
                    // Prefer the seeded wiki hint's badge (e.g. "SALVAGE",
                    // "EXPLORE") when one exists - "UNKNOWN" remains the
                    // fallback for no-source items with no seeded hint, and
                    // for a badge equal to one of the three source badges
                    // above, which would be indistinguishable from a row
                    // that really does have that source (see
                    // DecisionPillPlanner.IsReservedSourceBadgeText, which
                    // guards the recipe tree's copy of this decision). The
                    // hint TEXT still reaches TooltipForRow either way.
                    return !string.IsNullOrEmpty(row.BadgeText) &&
                            !DecisionPillPlanner.IsReservedSourceBadgeText(row.BadgeText)
                        ? row.BadgeText
                        : "UNKNOWN";
                default: return null;
            }
        }

        /// <summary>
        /// What the badge means, in prose - the hover on the pill itself.
        /// The badge is four to seven capital letters, which says WHICH
        /// source only to a reader who already knows the vocabulary; this
        /// says what to actually do about it.
        /// <para>
        /// A seeded acquisition hint always wins for the sources that can
        /// carry one: the hint is specific to this item ("Salvage from
        /// level 80 rares"), and generic prose would be strictly less
        /// useful sitting on top of it. Null for a row type the Shopping
        /// List does not emit, matching <see cref="ForRow"/>.
        /// </para>
        /// </summary>
        public static string TooltipForRow(PlanRowViewModel row)
        {
            if (row == null)
            {
                return null;
            }

            switch (row.RowType)
            {
                case PlanRowType.ShoppingBuy:
                    return "Buy on the Trading Post";
                case PlanRowType.ShoppingVendor:
                    return string.IsNullOrEmpty(row.HintText)
                        ? "Buy from a vendor"
                        : "Buy from a vendor - " + row.HintText;
                case PlanRowType.ShoppingCurrency:
                    return "Paid from your wallet";
                case PlanRowType.ShoppingUnknown:
                    return !string.IsNullOrEmpty(row.HintText)
                        ? row.HintText
                        : "No known acquisition source - check the item's wiki page";
                default: return null;
            }
        }
    }
}
