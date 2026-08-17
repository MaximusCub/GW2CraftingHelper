using System.Collections.Generic;

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

        // Plan Notes (single flat advisory section, Option 1 of
        // design-plan-notes.md): excess/reclaim, competency gaps, and the
        // Mystic-Clover-yield forge-scope caveat, in that fixed order - see
        // PlanViewModelBuilder.BuildNotesSection. Always last (Build()'s
        // section 7) since every note kind is a caveat ABOUT facts shown in
        // an earlier section. No PlanContentHeightMath case is added for
        // this type on purpose - it falls through to that method's existing
        // default arm (rows.Count * FallbackTextRowHeight), which is
        // already correct as long as every NoteLine row renders at exactly
        // that height (see NotesSectionRenderer's own doc comment).
        Notes,

        // Not a member of PlanViewModel.Sections (the tree renders from
        // PlanViewModel.TreeRoot, not a row list) - used only as a
        // dictionary key so its header expansion persists like every
        // other section's.
        RecipeTree
    }

    public enum PlanRowType
    {
        // W4A (Total Cost section redesign): CoinTotal itself is no longer
        // emitted by PlanViewModelBuilder.BuildSummarySection (superseded
        // by CostFormulaTile/ProfitFormulaTile below) - kept as an enum
        // member ONLY because Services/PlanContentHeightMath.cs (DO-NOT-
        // TOUCH for this package) still references it by name in its own
        // private SummaryBodyHeight method. That method is itself now
        // unreachable for a real Summary section (Views/CraftingPlanView.cs
        // routes PlanSectionType.Summary through
        // Services/SummarySectionLayoutMath.BodyHeight instead - see that
        // class's doc comment) but is left byte-for-byte unmodified per the
        // W4A task brief, so removing this member would break its
        // compilation. Do not resurrect this as a real row type without
        // first re-reading that class's doc comment.
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
        // ONLY for a genuine multi-item batch (2+ requested items). M37
        // (KNOWN-ISSUES #25) added a real batch-level Sell value/Profit
        // rollup - see SellSideEconomics.ApplyBatchSellSideEconomics
        // and PlanViewModelBuilder.BuildSummarySection - and reworded
        // this note's Label text to describe it. The rollup has NO
        // craft-vs-buy filter at all
        // (SellSideEconomics.ApplyBatchSellSideEconomics' own doc comment,
        // divergence item 1): a bought-but-tradable root
        // with a live sell price still contributes to the sum. The
        // Label text is therefore deliberately NOT gw2e's own verbatim
        // Cost Breakdown banner ("Profit numbers are the sum of all
        // crafted recipes." - docs/gw2e-parity-spec.md, the M34 r1
        // multi-item research report) - "crafted recipes" would be
        // inaccurate for a craft-agnostic, tradable-only rollup; see
        // docs/KNOWN-ISSUES.md #25's divergence record. Rendered via
        // the same plain-text row pattern as TimegatedNotice.
        MultiItemNote,

        // W4A (Total Cost section redesign): one tile of the Total Cost
        // section's first formula band - "Total Materials Value - Your
        // Materials Used = Actual Cost to Craft" - collapsing to a single
        // "Actual Cost to Craft" tile (one row of this type) when there is
        // no materials-used middle term to subtract (PlanViewModelBuilder.
        // BuildSummarySection's collapse rule). Rendered as an equal-width
        // stat tile, same shape the pre-W4A CoinTotal band used - see
        // SummarySectionRenderer.
        CostFormulaTile,

        // W4A: one tile of the Total Cost section's second formula band -
        // "Sell Value - Total Materials Value = Profit/Loss if Sold" -
        // present only when the plan has a live sell price
        // (CraftingPlanResult.NetSaleValue.HasValue). Always exactly 3 rows
        // of this type when present - no collapse rule, the profit formula
        // is meaningless with fewer than 3 terms.
        ProfitFormulaTile,

        // W4A: the Total Cost section's single subdued trading-post
        // pricing-basis footnote row, always present exactly once at the
        // bottom of the section.
        SummaryFootnote,

        // design-plan-notes.md (Notes section, Option 1): the one shared
        // row shape for every line in PlanSectionType.Notes - excess/
        // reclaim, competency, and forge-scope lines all use this single
        // member rather than one row type per note kind. Label carries the
        // full self-describing sentence; CoinValue is 0 for a plain-text
        // line (competency, forge-scope) and > 0 for an excess-item or
        // total line - NotesSectionRenderer draws a right-aligned coin cell
        // only when CoinValue > 0, mirroring CoinCurrencyRenderer's own
        // "hasCoin = copper > 0" convention. Never carries StatusTag/
        // BadgeText (no pills in this section, per the brief).
        NoteLine
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

        // source-selection-simplification (maintainer-approved redesign,
        // docs/gw2e-considerations.md): passthrough of CraftingPlanResult.
        // ItemMetadata, mirroring CurrencyMetadata's own precedent exactly
        // - lets the recipe-tree renderer resolve a Subdued pill's
        // StrictDomination item-kind deltas (raw item ids, e.g. Globs of
        // Ectoplasm) to a display-ready name via PlanViewModelBuilder.
        // ResolveName at render time, the same "id-only in the pure
        // layers, resolved only at render" split CurrencyMetadata already
        // establishes. Null under the same conditions as the source field.
        public IReadOnlyDictionary<int, ItemMetadata> ItemMetadata { get; set; }

        // AUDIT ROW 20/38 (gw2e price-side fallback parity): passthrough of
        // CraftingPlanResult.PriceBasis so the recipe-tree renderer can word
        // a fallen-back node's unit-price tooltip caveat with the correct
        // side names ("buy-order price unavailable" vs. "instant-buy price
        // unavailable") instead of a basis-agnostic message.
        public PriceBasis PriceBasis { get; set; }

        // currency-ux-package (Feature 2): plan-scope currency facts for
        // the Recipe Tree's per-leaf "HAVE {have}/{planTotal} TOTAL" pill
        // (DecisionPillPlanner.BuildPillSpecs/TreeSectionController.
        // RenderDecisionPills) - deliberately whole-PLAN totals, not any
        // per-row allocation, so the identical pill text is truthful at
        // every tree occurrence of the same currency id (see
        // DecisionPillPlanner's own doc comment on why no allocation is
        // computed). Both are plain passthroughs of already-computed plan
        // facts, keyed by currency id:
        //
        // CurrencyPlanTotals: CraftingPlanResult.Plan.CurrencyCosts (the
        // whole plan's real currency need), converted to a dictionary.
        // Null when the plan needs no currency at all.
        //
        // OwnedCurrencyAmounts: passthrough of CraftingPlanResult.
        // OwnedCurrencyAmounts (raw wallet holding, never clamped to need -
        // see that field's own doc comment). Null when no wallet snapshot
        // was available - distinct from "0 owned", and the tree renderer
        // must treat it that way (omit the pill entirely, not show HAVE 0).
        public IReadOnlyDictionary<int, long> CurrencyPlanTotals { get; set; }
        public IReadOnlyDictionary<int, int> OwnedCurrencyAmounts { get; set; }

        // currency-ux-package (Feature 3, maintainer-ratified #21
        // resolution): passthrough of CraftingPlan.TimegatedItems
        // (informational-only vendor purchase caps - see that class's own
        // doc comment), re-indexed by ItemId so the Recipe Tree's
        // value-detail tooltip can look up a BuyFromVendor node's winning
        // offer cap in O(1) instead of scanning the list per row. Null
        // when the plan has no timegated items at all.
        public IReadOnlyDictionary<int, TimegatedItem> VendorCapsByItemId { get; set; }
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
        // CurrencyDisplayResolver.ResolveAmounts/ResolveUnitAmounts). This
        // value is DELIBERATELY clamped so the HAVE/Amount pair the row
        // tooltip renders always reads as a coverage fraction (e.g.
        // "HAVE 500/500") rather than overshooting the total - see
        // RawOwnedQuantity below for the real, unclamped holding.
        public int? OwnedQuantity { get; set; }

        // Raw, UNCLAMPED wallet holding backing OwnedQuantity above
        // (shoplist-have-format): unlike OwnedQuantity, this is never
        // capped at Amount, so it can exceed the row's Total when the
        // account holds more of this currency than this row needs. Null
        // under the exact same conditions as OwnedQuantity (no wallet
        // snapshot / per-unit "Each" figure). Tooltip-only - lets the
        // shopping row's tooltip spell out the real holding even when the
        // clamped OwnedQuantity/Amount pair alone would hide it.
        public int? RawOwnedQuantity { get; set; }
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

        // UI-bundle milestone, Feature A (wiki links): the GW2 wiki page
        // this row's row-level wiki affordance should open (see
        // WikiLinkBuilder). Currently populated only for RecipeRow rows
        // (RequiredRecipes section - see PlanViewModelBuilder.
        // BuildRecipesSection); null for every other row type, which
        // suppresses the affordance entirely rather than guessing a URL.
        public string WikiUrl { get; set; }

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

        // Owned split for a CurrencyCost row (M34-B2a #4, gw2e parity - see
        // AccountCurrencyIndex): the account's wallet holding of this
        // currency. Null (not 0) when no wallet snapshot was available at
        // all, distinct from "0 owned" - only ever set on CurrencyCost
        // rows. W4A (Total Cost section redesign, user-mandated): this is
        // now the RAW, UNCLAMPED wallet amount (was min(Quantity, wallet
        // amount) pre-W4A) - the redesigned currency table's "Have" column
        // shows the real holding even when it exceeds what the plan needs,
        // rather than silently capping it at Quantity. CurrencyNeededQuantity
        // below is the (still-clamped-to-zero) gap derived from this value.
        public int? CurrencyOwnedQuantity { get; set; }

        // W4A: still-to-acquire gap for a CurrencyCost row in the
        // redesigned currency table's "Needed" column - max(0, Quantity -
        // CurrencyOwnedQuantity). Null (not 0) whenever CurrencyOwnedQuantity
        // is null (no wallet snapshot) - mirrors that field's own null
        // contract, never a fabricated gap. Only ever set on CurrencyCost
        // rows.
        public int? CurrencyNeededQuantity { get; set; }

        // W4A: true when CurrencyOwnedQuantity is present AND covers the
        // full Required amount (CurrencyOwnedQuantity >= Quantity) - drives
        // the currency table's green full-coverage marker. Always false
        // when no wallet snapshot exists (CurrencyOwnedQuantity null) -
        // "unknown" must never render as "covered". Only ever set on
        // CurrencyCost rows.
        public bool CurrencyFullyCovered { get; set; }

        // W4A (Total Cost section redesign, user-mandated mouseover
        // tooltips): the exact-meaning tooltip text for a CostFormulaTile/
        // ProfitFormulaTile row's header caption. Set directly on the
        // caption Label control itself, never on the tile's containing
        // Panel (M32 lesson: a label captures the mouse before a container
        // tooltip underneath it would ever be reached - see
        // SummarySectionRenderer.CreateFormulaBand). Null/unused for every
        // other row type.
        public string TooltipText { get; set; }

        // W4A review-fix-round-2: true (default) when the formula band's
        // "=" operator drawn immediately to this tile's left is an honest
        // statement (left tile - middle tile literally equals this tile's
        // displayed CoinValue). SummarySectionRenderer.CreateFormulaBand
        // only ever draws "=" before the LAST tile in a band, and only
        // reads this field on that tile - it being false on every other
        // tile is harmless (unread there). The one case this is ever
        // false: the profit band's loss tile, whose Label/CoinValue
        // deliberately show "Loss if Sold" / Math.Abs(profit) (the
        // pre-existing sign convention - PlanViewModelBuilder.
        // BuildProfitFormulaBand). With that convention the drawn
        // equation ("Sell Value - Total Materials Value = <abs loss>")
        // is arithmetically FALSE for a negative profit, since the true
        // right-hand side is negative. When false, the renderer
        // substitutes a neutral, non-equality punctuation mark for that
        // one boundary instead of "=".
        public bool FormulaResultIsExact { get; set; } = true;

        // W3C (per-character discipline display, gw2efficiency parity):
        // which of the account's characters have this DisciplineRow's
        // discipline, and at what rating - e.g. "Anna (500), Bob
        // (400/450)" (the "/450" suffix marks a character below the row's
        // required MinRating - see PlanViewModelBuilder.
        // BuildCharacterAvailabilityText). "Not trained on any character"
        // when the snapshot has discipline data but no character has it.
        // Null (not empty) when the snapshot has no character-crafting
        // data at all (old snapshot / degraded fetch) - the renderer must
        // show nothing extra for that case, never a fabricated claim
        // either way. Only ever set on DisciplineRow rows.
        public string CharacterAvailabilityText { get; set; }
    }
}
