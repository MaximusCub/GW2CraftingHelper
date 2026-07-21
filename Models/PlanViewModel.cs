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
        RecipeRow,

        // Plain informational line in the Crafting Steps section (M34-B1
        // #3) - a vendor-capped item whose merged demand exceeds its
        // offer's daily/weekly purchase cap. Never numbered/badged like a
        // CraftStep row; rendered via the same plain-text row pattern as
        // any other fallback text row.
        TimegatedNotice,

        // M35 (gw2efficiency parity - multi-item plans): a single plain
        // informational line appended to the Summary/Total Cost section
        // ONLY for a genuine multi-item batch (2+ requested items) -
        // echoes gw2e's own Cost Breakdown banner verbatim ("Profit
        // numbers are the sum of all crafted recipes." -
        // docs/gw2e-parity-spec.md, the M34 r1 multi-item research
        // report). M37 (KNOWN-ISSUES #25) added a real batch-level Sell
        // value/Profit rollup driven by the same craft===true filter
        // gw2e's own rollup uses - see
        // CraftingPlanPipeline.ApplyBatchSellSideEconomics and
        // PlanViewModelBuilder.BuildSummarySection. Rendered via the same
        // plain-text row pattern as TimegatedNotice.
        MultiItemNote
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

        // M35 (gw2efficiency parity - multi-item plans): populated INSTEAD
        // of TreeRoot for a genuine multi-item batch (2+ requested items) -
        // one full CraftingTreeNode per requested item, in request order,
        // mirrors CraftingPlanResult.MultiItemRoots' own doc comment
        // exactly (the synthetic wrapper root never surfaces here either).
        // Null for a single-item plan, which continues to populate
        // TreeRoot as before - CraftingPlanView.RenderPlan branches on
        // whichever of the two is non-null.
        public List<CraftingTreeNode> MultiItemRoots { get; set; }

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

        // Non-null only for a fractional-per-unit "Each" amount (M34-B1
        // #2): when a vendor offer's true per-unit rate does not divide
        // evenly (e.g. "2 for 3"), the renderer displays this literal
        // bundle text instead of Amount, rather than inventing a rounded
        // number. Null for every whole-number amount and for every Total
        // (non-"Each") amount - see CurrencyDisplayResolver.ResolveUnitAmounts.
        public string BundleLabel { get; set; }

        // Owned/needed split for a shopping-row currency Total amount
        // (M34-B2b, gw2e parity - mirrors PlanRowViewModel.
        // CurrencyOwnedQuantity's doc comment): min(Amount, wallet amount)
        // of this currency the account already holds. Null (not 0) when no
        // wallet snapshot was available, or this amount is a per-unit
        // "Each" figure (ownership is a total-quantity concept - see
        // CurrencyDisplayResolver.ResolveAmounts/ResolveUnitAmounts).
        public int? OwnedQuantity { get; set; }
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

        // Owned/needed split for a CurrencyCost row (M34-B2a #4, gw2e
        // parity - see AccountCurrencyIndex): min(Quantity, wallet amount)
        // of this currency the account already holds; the renderer derives
        // "still needed" as Quantity - CurrencyOwnedQuantity. Null (not 0)
        // when no wallet snapshot was available at all, distinct from "0
        // owned" - only ever set on CurrencyCost rows.
        public int? CurrencyOwnedQuantity { get; set; }
    }
}
