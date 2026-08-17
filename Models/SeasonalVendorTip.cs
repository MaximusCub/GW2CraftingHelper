using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// opportunity-notes (SEASONAL VENDOR TIP): one plan item that a
    /// currently-active festival vendor sells (or barters) more cheaply
    /// than this plan's own chosen price - see
    /// Services/SeasonalVendorTipCalculator, the sole producer. Never
    /// affects solving (seasonal offers are unconditionally excluded from
    /// the solver's own offer set - see Services/SeasonalOfferFilter);
    /// cosmetic display data only, same "advisory" contract as
    /// RecipeSheetSavingsOpportunity's own doc comment.
    /// </summary>
    public class SeasonalVendorTip
    {
        /// <summary>The plan item this tip applies to.</summary>
        public int ItemId { get; set; }

        /// <summary>
        /// The festival's internal name key (Blish_HUD.Contexts.
        /// FestivalContext.Festival.Name, e.g. "halloween" - lowercase,
        /// MEASURED, NOT the capitalized DisplayName). Consumers that show
        /// this to the user must resolve a display string via
        /// Gw2Constants.ResolveFestivalDisplayName - see that method's own
        /// doc comment.
        /// </summary>
        public string Festival { get; set; }

        public string MerchantName { get; set; }

        /// <summary>The offer's own cost lines, unscaled (one purchase).</summary>
        public List<CostLine> CostLines { get; set; }

        /// <summary>Units produced per purchase (VendorOffer.OutputCount).</summary>
        public int OutputCount { get; set; }

        /// <summary>Coin-equivalent cost per unit of output.</summary>
        public long OfferUnitCost { get; set; }

        /// <summary>This plan's own chosen unit price for the same item.</summary>
        public long PlanUnitPrice { get; set; }

        public int? DailyCap { get; set; }
        public int? WeeklyCap { get; set; }
    }
}
