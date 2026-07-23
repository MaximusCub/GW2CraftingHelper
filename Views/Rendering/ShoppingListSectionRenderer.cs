using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-23b (m38-a1-architecture.md S3b-T2, continuing the WP-23 pilot):
    // moved verbatim out of CraftingPlanView's "7. Section builders
    // (continued)" region - the Shopping List row list, its header row, and
    // its ShoppingSourceTag helper. Behavior is unchanged: same row
    // geometry, same PlanContentHeightMath/PlanRelayoutMath/
    // ShoppingColumnMath calls (all three stay put in Services, per plan -
    // DO-NOT-TOUCH), same LabelHelpers.CreateRowDivider usage
    // (DO-NOT-TOUCH #6 - divider math and its M36b 1px scissor clearance
    // untouched), same CoinCurrencyRenderer usage for the Each/Total cells.
    // The only edits inside the moved bodies are _relayoutActions.Add ->
    // the injected ISectionRelayoutSink.AddRelayout, _reellipsisActions.Add
    // -> ISectionRelayoutSink.AddReellipsis (both semantics-preserving
    // pass-throughs - see ISectionRelayoutSink's doc comment), and
    // GetPillColors(...) -> PillColors.GetPillColors(...).
    //
    // The WP-23 pilot's FORWARD NOTE flagged this section by name:
    // CreateShoppingRow also called CraftingPlanView's private static
    // GetPillColors(PillKind, bool, out Color, out Color) for its
    // source-tag panel colors. GetPillColors is ALSO called by
    // CraftingPlanView.RenderDecisionPills (the recipe tree's decision
    // pills, not yet extracted) - grepped before this move - so it could
    // not simply move into this class the way ShoppingSourceTag did
    // (ShoppingSourceTag has exactly one call site, inside CreateShoppingRow
    // below, and moved here directly). Resolution: GetPillColors was
    // extracted to its own Views/Rendering/PillColors.cs (analogous to the
    // WP-21 Tier-1 extraction's RarityColors.cs), and CraftingPlanView's
    // RenderDecisionPills now forwards to it - a forward
    // CraftingPlanView -> Views/Rendering call, never the reverse edge the
    // WP-21 findings fix (commit 5c56b2a) already reverted once.
    internal sealed class ShoppingListSectionRenderer
    {
        private readonly ISectionRelayoutSink _sink;

        internal ShoppingListSectionRenderer(ISectionRelayoutSink sink)
        {
            // Mirrors the constructor-null-guard convention already used
            // for injected dependencies elsewhere in Views/ (ViewAdapter's
            // buildAction, SettingsTabContent's settings, FrameTicker's
            // step) and by DisciplinesSectionRenderer/
            // UsedMaterialsSectionRenderer - the sole production call site
            // always passes `this` (CraftingPlanView), but a later section
            // renderer built on this same pattern should fail loud, not
            // with a deferred NRE inside CreateShoppingRow's first
            // AddRelayout call.
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        // Right-aligned price columns for the shopping list's Each and
        // Total prices: both anchor to a fixed right edge and grow
        // LEFTWARD, so a gold-value amount in either column can never grow
        // into the other's space. Previously each column reserved a fixed
        // width (150/90) regardless of content; a 3+ digit gold value in
        // Each or Total could still exceed its fixed band and bleed into
        // the Amount column to its left. Column widths are now derived from
        // the actual widest rendered value per column, clamped to those
        // same fixed minimums so short/low-value lists don't look cramped -
        // see ShoppingColumnMath (Blish-free, unit-tested arithmetic).
        //
        // Moved verbatim from CraftingPlanView.CreateShoppingListBody.
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            var coinFont = GameService.Content.DefaultFont14;

            // Pre-scan: widest actual coin+currency value width per column
            // this render (CoinCurrencyRenderer.MeasureValueWidth accounts for a currency-only
            // or mixed row's icon(s) too, not just coin - KNOWN-ISSUES
            // #16). One pass over the section's rows (shopping lists run to
            // maybe 50-60 rows in practice) - negligible next to the
            // per-row control creation this method already does.
            int maxEachWidth = 0;
            int maxTotalWidth = 0;
            foreach (var row in section.Rows)
            {
                int eachW = CoinCurrencyRenderer.MeasureValueWidth(row.UnitCoinValue, row.UnitCurrencyCosts, coinFont);
                if (eachW > maxEachWidth) maxEachWidth = eachW;

                int totalW = CoinCurrencyRenderer.MeasureValueWidth(row.CoinValue, row.CurrencyCosts, coinFont);
                if (totalW > maxTotalWidth) maxTotalWidth = totalW;
            }

            int totalRightEdge = panelWidth - 8;
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge, maxEachWidth, maxTotalWidth);

            // Both the header and every data row are handed this SAME
            // ColumnEdges instance (for the build), and the same cached
            // maxEachWidth/maxTotalWidth (for their relayout closures) - a
            // relayout tick re-invokes ShoppingColumnMath.ComputeEdges with
            // the new panelWidth but these SAME data-derived maxima (M33
            // C2b: the pre-scan above depends only on row data, never on
            // panelWidth, so it does not need to re-run on resize at all).
            CreateShoppingListHeaderRow(contentFlow, panelWidth, edges, maxEachWidth, maxTotalWidth);
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateShoppingRow(section.Rows[i], contentFlow, panelWidth, edges, maxEachWidth, maxTotalWidth, i == section.Rows.Count - 1);
            }
        }

        // Moved verbatim from CraftingPlanView.CreateShoppingListHeaderRow.
        // Only change: _relayoutActions.Add(...) -> _sink.AddRelayout(...).
        private void CreateShoppingListHeaderRow(
            FlowPanel parent, int panelWidth, ShoppingColumnMath.ColumnEdges edges, int maxEachWidth, int maxTotalWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, PlanContentHeightMath.ShoppingHeaderRowHeight),
                Parent = parent
            };
            var font = GameService.Content.DefaultFont12;
            var color = new Color(153, 153, 153);

            new Label()
            {
                Text = "Item", Font = font, TextColor = color,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(50, 4), Parent = rowPanel
            };
            var amountLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Amount", font, color, edges.QtyRightEdge, 4);
            var eachLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Each", font, color, edges.EachRightEdge, 4);
            var totalLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Total", font, color, edges.TotalRightEdge, 4);

            // M33 C2b: header column labels are font-only (fixed text) -
            // pure reposition on every drag tick, recomputing edges from
            // the SAME cached maxEachWidth/maxTotalWidth ComputeEdges was
            // built with (ShoppingColumnMath is the single source of truth
            // both paths call).
            _sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, PlanContentHeightMath.ShoppingHeaderRowHeight);
                var e = ShoppingColumnMath.ComputeEdges(w - 8, maxEachWidth, maxTotalWidth);
                amountLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.QtyRightEdge, amountLabel.Width), 4);
                eachLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.EachRightEdge, eachLabel.Width), 4);
                totalLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.TotalRightEdge, totalLabel.Width), 4);
            });
        }

        // Moved verbatim from CraftingPlanView.ShoppingSourceTag - only one
        // call site (CreateShoppingRow below), unlike GetPillColors, so no
        // shared-class extraction was needed for this helper.
        private static string ShoppingSourceTag(PlanRowViewModel row)
        {
            switch (row.RowType)
            {
                case PlanRowType.ShoppingVendor: return "VENDOR";
                case PlanRowType.ShoppingCurrency: return "CURRENCY";
                case PlanRowType.ShoppingUnknown:
                    // Prefer the seeded wiki hint's badge (e.g. "SALVAGE",
                    // "EXPLORE") when one exists - "UNKNOWN" remains the
                    // fallback for no-source items with no seeded hint.
                    return !string.IsNullOrEmpty(row.BadgeText) ? row.BadgeText : "UNKNOWN";
                default: return null; // ShoppingBuy: plain TP purchase, no tag needed
            }
        }

        // Moved verbatim from CraftingPlanView.CreateShoppingRow. Changes:
        // _relayoutActions.Add(...) -> _sink.AddRelayout(...);
        // _reellipsisActions.Add(...) -> _sink.AddReellipsis(...);
        // GetPillColors(...) -> PillColors.GetPillColors(...).
        private void CreateShoppingRow(
            PlanRowViewModel row, FlowPanel parent, int panelWidth, ShoppingColumnMath.ColumnEdges edges,
            int maxEachWidth, int maxTotalWidth, bool isLast)
        {
            const int rowHeight = PlanContentHeightMath.ShoppingRowHeight;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            // M36: y=0 (was 1) - see the identical note in
            // CreateUsedMaterialRow; same 36px rowHeight / 34px icon frame
            // shape, same 1px shortfall against the new 2px divider.
            IconControls.CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 8, 0);

            const int nameX = 50;
            var font = GameService.Content.DefaultFont14;

            string qtyText = $"{row.Quantity}x";
            int qtyWidth = (int)System.Math.Ceiling(font.MeasureString(qtyText).Width);
            int nameMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(edges.QtyRightEdge, qtyWidth, 12, nameX);

            string fullName = row.Label ?? "";
            string hintText = row.HintText;
            string displayName = LabelHelpers.EllipsizeToWidth(font, fullName, nameMaxWidth);
            var nameLabel = new Label()
            {
                Text = displayName,
                Font = font,
                TextColor = RarityColors.GetRarityNameColor(row.Rarity),
                ShowShadow = true,
                ShadowColor = Color.Black * 0.8f,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(nameX, 9),
                Parent = rowPanel
            };
            var tooltipParts = new List<string>();
            if (displayName != fullName)
            {
                tooltipParts.Add(fullName);
            }
            if (!string.IsNullOrEmpty(hintText))
            {
                tooltipParts.Add(hintText);
            }
            // M34-B2b: owned/needed split for this row's currency cost(s),
            // cosmetic-only tooltip (avoids new inline layout math for a
            // fixed-height shopping row - see PlanContentHeightMath).
            if (row.CurrencyCosts != null)
            {
                foreach (var cc in row.CurrencyCosts)
                {
                    if (cc.OwnedQuantity.HasValue)
                    {
                        long needed = cc.Amount - cc.OwnedQuantity.Value;
                        tooltipParts.Add($"{cc.Name}: {cc.OwnedQuantity.Value} owned, {needed} needed");
                    }
                }
            }
            if (tooltipParts.Count > 0)
            {
                rowPanel.BasicTooltipText = string.Join("\n", tooltipParts);
            }

            string sourceTag = ShoppingSourceTag(row);
            Panel tagPanel = null;
            if (!string.IsNullOrEmpty(sourceTag))
            {
                PillColors.GetPillColors(PillKind.Locked, false, out Color tagBorder, out Color tagFill);
                tagPanel = LabelHelpers.CreateSmallTag(
                    rowPanel, sourceTag, nameX + nameLabel.Width + 8, 9, tagBorder, tagFill);
            }

            var qtyLabel = new Label()
            {
                Text = qtyText,
                Font = font,
                TextColor = new Color(200, 200, 200),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(edges.QtyRightEdge - qtyWidth, 9),
                Parent = rowPanel
            };

            // Each/Total cells: coin-only rows render exactly as before;
            // a row priced wholly or partly in a non-coin currency (e.g. a
            // vendor offer paid in spirit shards) renders currency segments
            // alongside/instead of coin; a row with neither (genuinely
            // unpriceable - gw2e: "Not sold or crafted") renders a dash,
            // never a blank cell (KNOWN-ISSUES #16).
            var eachCell = CoinCurrencyRenderer.RenderValueCellRightAligned(rowPanel, row.UnitCoinValue, row.UnitCurrencyCosts, edges.EachRightEdge, 9, font);
            var totalCell = CoinCurrencyRenderer.RenderValueCellRightAligned(rowPanel, row.CoinValue, row.CurrencyCosts, edges.TotalRightEdge, 9, font);

            // M36b: bottomClearance 0 - ShoppingRowHeight (36) is immune to
            // the Container.Paint round-trip defect (see LabelHelpers.CreateRowDivider's
            // doc comment) and its icon frame is flush-fit with zero
            // slack; see the identical note in CreateUsedMaterialRow.
            Panel divider = isLast ? null : LabelHelpers.CreateRowDivider(rowPanel, panelWidth, rowHeight, 0);

            // M33 C2b: qty + Each/Total cells reposition every drag tick
            // (no MeasureString - CoinCurrencyRenderer.RepositionValueCellRightAligned uses only
            // cached segment text widths). The name label and its source
            // tag are untouched here; both depend on ellipsis truncation
            // and only update at settle (RunReellipsis) below.
            _sink.AddRelayout(w =>
            {
                var e = ShoppingColumnMath.ComputeEdges(w - 8, maxEachWidth, maxTotalWidth);
                rowPanel.Size = new Point(w, rowHeight);
                qtyLabel.Location = new Point(e.QtyRightEdge - qtyWidth, 9);
                CoinCurrencyRenderer.RepositionValueCellRightAligned(eachCell, e.EachRightEdge, 9);
                CoinCurrencyRenderer.RepositionValueCellRightAligned(totalCell, e.TotalRightEdge, 9);
                if (divider != null) divider.Size = new Point(w, 2);
            });
            _sink.AddReellipsis(w =>
            {
                var e = ShoppingColumnMath.ComputeEdges(w - 8, maxEachWidth, maxTotalWidth);
                int newMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(e.QtyRightEdge, qtyWidth, 12, nameX);
                string newDisplayName = LabelHelpers.EllipsizeToWidth(font, fullName, newMaxWidth);
                if (nameLabel.Text != newDisplayName)
                {
                    nameLabel.Text = newDisplayName;
                    var parts = new List<string>();
                    if (newDisplayName != fullName) parts.Add(fullName);
                    if (!string.IsNullOrEmpty(hintText)) parts.Add(hintText);
                    rowPanel.BasicTooltipText = parts.Count > 0 ? string.Join("\n", parts) : null;
                }
                if (tagPanel != null)
                {
                    tagPanel.Location = new Point(nameX + nameLabel.Width + 8, 9);
                }
            });
        }
    }
}
