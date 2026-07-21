using System.Collections.Generic;
using GW2CraftingHelper.Contracts;

namespace GW2CraftingHelper.Models
{
    public enum PlanSectionType
    {
        Summary,
        UsedMaterials,
        ShoppingList,
        CraftingSteps,
        RequiredDisciplines,
        RequiredRecipes,

        // Not a member of PlanViewModel.Sections (the tree renders from
        // PlanViewModel.TreeRoot, not a row list) - used only as a
        // dictionary key so its header expansion persists like every
        // other section's.
        RecipeTree
    }

    public enum PlanRowType
    {
        CoinTotal,
        CurrencyCost,
        UsedMaterial,
        ShoppingBuy,
        ShoppingVendor,
        ShoppingCurrency,
        ShoppingUnknown,
        CraftStep,
        DisciplineRow,
        RecipeRow
    }

    public class PlanViewModel
    {
        public string TargetItemName { get; set; }
        public string TargetIconUrl { get; set; }

        // GW2 API rarity string; null/empty = unknown (neutral color/border).
        public string TargetRarity { get; set; }
        public int TargetQuantity { get; set; }
        public List<PlanSectionViewModel> Sections { get; set; } = new List<PlanSectionViewModel>();
        public CraftingTreeNode TreeRoot { get; set; }

        // Passthrough of CraftingPlanResult.CurrencyMetadata (see that
        // field's doc comment) so the recipe-tree renderer can resolve a
        // node's VendorCurrencyCosts (raw CostLine ids) to display-ready
        // name/icon via CurrencyDisplayResolver at render time, the same
        // way BuildShoppingListSection already resolves it for shopping
        // rows. Null under the same conditions as the source field - the
        // resolver's own null-safe fallbacks handle that case.
        public IReadOnlyDictionary<int, CurrencyMetadata> CurrencyMetadata { get; set; }
    }

    /// <summary>
    /// A single non-coin currency amount, already resolved to display-ready
    /// name/icon (never a raw currency id - see CurrencyDisplayResolver).
    /// Used for BuyFromVendor rows/nodes priced wholly or partly in a
    /// non-coin currency (spirit shards, karma, etc.) - KNOWN-ISSUES #16.
    /// </summary>
    public class CurrencyAmountViewModel
    {
        public long Amount { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }
    }

    public class PlanSectionViewModel
    {
        public PlanSectionType SectionType { get; set; }
        public string Title { get; set; }
        public List<PlanRowViewModel> Rows { get; set; } = new List<PlanRowViewModel>();
        public bool IsDefaultExpanded { get; set; }
    }

    public class PlanRowViewModel
    {
        public PlanRowType RowType { get; set; }
        public string Label { get; set; }
        public string Sublabel { get; set; }
        public string IconUrl { get; set; }

        // GW2 API rarity string; null/empty = unknown (neutral border).
        public string Rarity { get; set; }
        public int Quantity { get; set; }
        public long CoinValue { get; set; }

        // Per-unit price (CoinValue is the row's total for Quantity units).
        // Only populated for shopping rows, which show both a unit-price and
        // a total-price table column.
        public long UnitCoinValue { get; set; }
        public string StatusTag { get; set; }

        // Wiki-derived acquisition guidance for unknown-source rows,
        // tooltip-only. Deliberately separate from Sublabel, which renders
        // inline in the row itself - HintText never renders inline.
        public string HintText { get; set; }

        // Short pill/tag label (e.g. "SALVAGE", "EXPLORE") for
        // ShoppingUnknown rows, from the same seeded hint entry as
        // HintText. Null when the hint has no badge (or no hint at all) -
        // the view falls back to "UNKNOWN".
        public string BadgeText { get; set; }

        // Non-coin currency cost(s) of this row (ShoppingVendor rows only -
        // see PlanStep.VendorCurrencyCosts), already resolved to
        // display-ready name/icon. Total-quantity amounts, mirroring
        // CoinValue. Null/empty when this row has no non-coin currency
        // cost - KNOWN-ISSUES #16.
        public List<CurrencyAmountViewModel> CurrencyCosts { get; set; }

        // Per-unit ("Each" column) counterpart of CurrencyCosts - integer-
        // divided by Quantity the same way UnitCoinValue divides CoinValue.
        // Null/empty under the same condition as CurrencyCosts.
        public List<CurrencyAmountViewModel> UnitCurrencyCosts { get; set; }
    }
}
