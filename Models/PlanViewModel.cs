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
        public string StatusTag { get; set; }
    }
}
