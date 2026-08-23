using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView's "7. Section builders
    // (continued)" region - the Shopping List row list, its header row, and
    // its ShoppingSourceTag helper. Behavior is unchanged: same row
    // geometry, same PlanContentHeightMath/PlanRelayoutMath/
    // ShoppingColumnMath calls, same LabelHelpers.CreateRowDivider usage
    // (divider math and its 1px scissor clearance
    // untouched), same CoinCurrencyRenderer usage for the Each/Total cells.
    // The only edits inside the moved bodies are _relayoutActions.Add ->
    // the injected ISectionRelayoutSink.AddRelayout, _reellipsisActions.Add
    // -> ISectionRelayoutSink.AddReellipsis (both semantics-preserving
    // pass-throughs - see ISectionRelayoutSink's doc comment), and
    // GetPillColors(...) -> PillColors.GetPillColors(...) (see PillColors'
    // doc comment for why that helper lives in its own file).
    //
    // CreateShoppingRow's
    // icon+ellipsized-name construction and its divider+relayout tail
    // go through the two shared row-shape helpers - IconNameRowHelpers
    // (build via CreateIconAndEllipsizedName, re-ellipsize via
    // ReellipsizeName) and RowRelayoutHelpers.FinishRow - both extracted
    // from this row and UsedMaterialsSectionRenderer.CreateUsedMaterialRow,
    // the only two rows across the extracted renderers that actually share
    // the ellipsis shape (see IconNameRowHelpers' own doc comment for why).
    // Everything this row does AFTER the name label - the tooltip-parts
    // build, the source-tag Panel, the qty label, the Each/Total coin cells -
    // is unchanged, still hand-rolled here (it does not match either shared
    // shape).
    internal sealed class ShoppingListSectionRenderer
    {
        // Gap between the name label and its source tag, and between the
        // name column and the Amount column - both were bare literals at
        // three call sites each before the tag started eating into the
        // name's ellipsis budget.
        private const int TagGap = 8;
        private const int NameToQtyGap = 12;

        // Left x of the name column (past the row's 32px icon at x=8).
        private const int NameX = 50;

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
            // The same pass also measures the widest "Nx" amount string and
            // the widest UNTRUNCATED name+tag extent, which is what lets the
            // Amount/Each/Total block be pulled in beside the names instead
            // of pinned to the panel edge (audit batch H) - see
            // ShoppingColumnMath.ComputeEdgesForPanel. The tag rides in the
            // name extent because it sits between the name and the Amount
            // column, exactly as the ellipsis budget already treats it.
            int maxEachWidth = 0;
            int maxTotalWidth = 0;
            int maxQtyWidth = 0;
            int widestNameEnd = 0;
            foreach (var row in section.Rows)
            {
                int eachW = CoinCurrencyRenderer.MeasureValueWidth(row.UnitCoinValue, row.UnitCurrencyCosts, coinFont);
                if (eachW > maxEachWidth) maxEachWidth = eachW;

                int totalW = CoinCurrencyRenderer.MeasureValueWidth(row.CoinValue, row.CurrencyCosts, coinFont);
                if (totalW > maxTotalWidth) maxTotalWidth = totalW;

                int qtyW = (int)System.Math.Ceiling(coinFont.MeasureString($"{row.Quantity}x").Width);
                if (qtyW > maxQtyWidth) maxQtyWidth = qtyW;

                int nameEnd = NameX
                    + (int)System.Math.Ceiling(coinFont.MeasureString(row.Label ?? "").Width)
                    + TagReserve(row);
                if (nameEnd > widestNameEnd) widestNameEnd = nameEnd;
            }

            // The header and every data row derive their build-time edges
            // from this SAME scan, and their relayout closures re-derive
            // them from it too - the pre-scan depends only on row data,
            // never on panelWidth, so it does not need to re-run on resize
            // at all and no two rows can anchor the table differently.
            var scan = new ColumnScan(maxEachWidth, maxTotalWidth, maxQtyWidth, widestNameEnd);
            CreateShoppingListHeaderRow(contentFlow, panelWidth, scan);
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateShoppingRow(section.Rows[i], contentFlow, panelWidth, scan, i == section.Rows.Count - 1);
            }
        }

        // The four data-derived (panelWidth-invariant) measurements every
        // row and header closure needs to recompute its column edges -
        // grouped so a fifth cannot be added to one call site and forgotten
        // at another.
        private readonly struct ColumnScan
        {
            internal readonly int MaxEachWidth;
            internal readonly int MaxTotalWidth;
            internal readonly int MaxQtyWidth;
            internal readonly int WidestNameEnd;

            internal ColumnScan(int maxEachWidth, int maxTotalWidth, int maxQtyWidth, int widestNameEnd)
            {
                MaxEachWidth = maxEachWidth;
                MaxTotalWidth = maxTotalWidth;
                MaxQtyWidth = maxQtyWidth;
                WidestNameEnd = widestNameEnd;
            }

            internal ShoppingColumnMath.ColumnEdges EdgesFor(int panelWidth)
            {
                return ShoppingColumnMath.ComputeEdgesForPanel(
                    panelWidth, MaxEachWidth, MaxTotalWidth, MaxQtyWidth, WidestNameEnd);
            }
        }

        /// <summary>
        /// Width the row's source tag takes out of the name column, or 0
        /// when it carries none - resolved identically by the pre-scan and
        /// by the row builder itself.
        /// </summary>
        private static int TagReserve(PlanRowViewModel row)
        {
            string sourceTag = ShoppingSourceBadge.ForRow(row);
            return string.IsNullOrEmpty(sourceTag)
                ? 0
                : LabelHelpers.MeasureSmallTagWidth(sourceTag) + TagGap;
        }

        // Moved verbatim from CraftingPlanView.CreateShoppingListHeaderRow.
        // Changes since: _relayoutActions.Add(...) -> _sink.AddRelayout(...),
        // and the column edges come from the shared pre-scan.
        private void CreateShoppingListHeaderRow(
            FlowPanel parent, int panelWidth, ColumnScan scan)
        {
            var edges = scan.EdgesFor(panelWidth);
            var rowPanel = new Panel()
            {
                Size = new Point(HeaderBandWidth(panelWidth, edges), TableHeaderStyle.RowHeight),
                BackgroundColor = TableHeaderStyle.BandColor,
                Parent = parent
            };
            var font = TableHeaderStyle.Font;
            var color = TableHeaderStyle.LabelColor;

            // This section builds its own header row rather than going
            // through CTableHeaderRenderer, so "Item" has to opt into the
            // same box treatment its Amount/Each/Total siblings get for
            // free from CreateRightAlignedLabel.
            LabelHelpers.WithDescenderClearance(new Label()
            {
                Text = "Item", Font = font, TextColor = color,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(NameX, TableHeaderStyle.LabelY), Parent = rowPanel
            });
            var amountLabel = LabelHelpers.CreateRightAlignedLabel(
                rowPanel, "Amount", font, color, edges.QtyRightEdge, TableHeaderStyle.LabelY);
            var eachLabel = LabelHelpers.CreateRightAlignedLabel(
                rowPanel, "Each", font, color, edges.EachRightEdge, TableHeaderStyle.LabelY);
            var totalLabel = LabelHelpers.CreateRightAlignedLabel(
                rowPanel, "Total", font, color, edges.TotalRightEdge, TableHeaderStyle.LabelY);

            // Header column labels are font-only (fixed text) -
            // pure reposition on every drag tick, recomputing edges from
            // the SAME cached pre-scan ComputeEdgesForPanel was built with
            // (ShoppingColumnMath is the single source of truth both paths
            // call).
            _sink.AddRelayout(w =>
            {
                var e = scan.EdgesFor(w);
                rowPanel.Size = new Point(HeaderBandWidth(w, e), TableHeaderStyle.RowHeight);
                amountLabel.Location = new Point(
                    PlanRelayoutMath.RightAlignedX(e.QtyRightEdge, amountLabel.Width), TableHeaderStyle.LabelY);
                eachLabel.Location = new Point(
                    PlanRelayoutMath.RightAlignedX(e.EachRightEdge, eachLabel.Width), TableHeaderStyle.LabelY);
                totalLabel.Location = new Point(
                    PlanRelayoutMath.RightAlignedX(e.TotalRightEdge, totalLabel.Width), TableHeaderStyle.LabelY);
            });
        }

        /// <summary>
        /// Width of the header's band: up to the Total column plus the
        /// margin every plan table keeps past its block, never wider than
        /// the panel. Same rule CTableHeaderRenderer.BandWidth applies -
        /// a band that runs past its own last column stopped bounding the
        /// table it belongs to once batch H pulled the columns in.
        /// </summary>
        private static int HeaderBandWidth(int panelWidth, ShoppingColumnMath.ColumnEdges edges)
        {
            int width = edges.TotalRightEdge + PlanRelayoutMath.TableRightMargin;
            if (width > panelWidth) width = panelWidth;
            return width > 0 ? width : 0;
        }

        // A ValueCellHandle's own
        // controls (the coin/currency icon+label segments, or the single
        // DashLabel for an unpriceable row) have no BasicTooltipText of
        // their own, so they silently swallow the row's tooltip exactly
        // like nameLabel does - see CreateShoppingRow's BuildTooltip doc
        // comment. Segment counts are always small (one row's worth of
        // coin/currency denominations), so this is cheap even called on
        // every BuildTooltip rebuild.
        private static void SetValueCellTooltip(CoinCurrencyRenderer.ValueCellHandle cell, string tooltipText)
        {
            if (cell.DashLabel != null)
            {
                TooltipFacility.ApplyPlain(cell.DashLabel, tooltipText);
                return;
            }
            foreach (var (label, icon) in cell.CoinSegments.Controls)
            {
                TooltipFacility.ApplyPlain(label, tooltipText);
                TooltipFacility.ApplyPlain(icon, tooltipText);
            }
            foreach (var (label, icon) in cell.CurrencySegments.Controls)
            {
                TooltipFacility.ApplyPlain(label, tooltipText);
                TooltipFacility.ApplyPlain(icon, tooltipText);
            }
        }

        // Moved verbatim from CraftingPlanView.CreateShoppingRow, then
        // refactored onto IconNameRowHelpers/RowRelayoutHelpers (see
        // the class doc comment above) - same geometry, same constants.
        private void CreateShoppingRow(
            PlanRowViewModel row, FlowPanel parent, int panelWidth, ColumnScan scan, bool isLast)
        {
            var edges = scan.EdgesFor(panelWidth);
            const int rowHeight = PlanContentHeightMath.ShoppingRowHeight;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            var font = GameService.Content.DefaultFont14;

            string qtyText = $"{row.Quantity}x";
            int qtyWidth = (int)System.Math.Ceiling(font.MeasureString(qtyText).Width);

            // The source tag sits immediately right of the name, so its
            // width has to come out of the name's ellipsis budget - it is
            // resolved before the name is built, not after. Previously only
            // the minority VENDOR/CURRENCY/UNKNOWN rows carried a tag and
            // the budget ignored it, so a long name pushed its own tag into
            // the Amount column; now that every row is badged that would be
            // the common case, not the rare one.
            string sourceTag = ShoppingSourceBadge.ForRow(row);
            int tagReserve = TagReserve(row);

            // Icon y=0 (was 1) - see the identical note in
            // CreateUsedMaterialRow; same 36px rowHeight / 34px icon frame
            // shape, same 1px shortfall against the new 2px divider.
            string fullName = row.Label ?? "";
            string hintText = row.HintText;
            var nameHandle = IconNameRowHelpers.CreateIconAndEllipsizedName(
                rowPanel, row.IconUrl, row.Rarity, 8, 0, fullName, font,
                edges.QtyRightEdge, qtyWidth, NameToQtyGap + tagReserve, NameX, 9);
            var nameLabel = nameHandle.NameLabel;

            // Owned/needed split for this row's currency cost(s),
            // cosmetic-only tooltip (avoids new inline layout math for a
            // fixed-height shopping row - see PlanContentHeightMath).
            // shoplist-have-format: line text now built by the Blish-free
            // ShoppingRowTooltipFormatter (its own doc comment covers the
            // HAVE/NEED wording and why "plan requires" was dropped -
            // cc.Amount is this row's own total, never the whole plan's
            // requirement for that currency id, which this renderer is
            // never handed). currencyLines is captured once and reused by
            // both BuildTooltip below and the AddReellipsis rebuild, so
            // the two can never diverge on a resize.
            var currencyLines = ShoppingRowTooltipFormatter.BuildCurrencyLines(row.CurrencyCosts);

            Panel tagPanel = null;
            if (!string.IsNullOrEmpty(sourceTag))
            {
                PillColors.GetPillColors(PillKind.Locked, false, out Color tagBorder, out Color tagFill);
                tagPanel = LabelHelpers.CreateSmallTag(
                    rowPanel, sourceTag, NameX + nameLabel.Width + TagGap, 9, tagBorder, tagFill);
            }

            var qtyLabel = LabelHelpers.WithDescenderClearance(
                new Label()
                {
                    Text = qtyText,
                    Font = font,
                    TextColor = new Color(200, 200, 200),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(edges.QtyRightEdge - qtyWidth, 9),
                    Parent = rowPanel
                });

            // Each/Total cells: coin-only rows render exactly as before;
            // a row priced wholly or partly in a non-coin currency (e.g. a
            // vendor offer paid in spirit shards) renders currency segments
            // alongside/instead of coin; a row with neither (genuinely
            // unpriceable - gw2e: "Not sold or crafted") renders a dash,
            // never a blank cell.
            var eachCell = CoinCurrencyRenderer.RenderValueCellRightAligned(rowPanel, row.UnitCoinValue, row.UnitCurrencyCosts, edges.EachRightEdge, 9, font);
            var totalCell = CoinCurrencyRenderer.RenderValueCellRightAligned(rowPanel, row.CoinValue, row.CurrencyCosts, edges.TotalRightEdge, 9, font);

            // TOOLTIP SWALLOWED BY
            // CHILD CONTROLS: a container's BasicTooltipText never fires
            // when a child control with no tooltip of its own covers the
            // hover point - the row's children (nameLabel, tagPanel,
            // qtyLabel, the Each/Total cells) all capture the mouse before
            // rowPanel's own tooltip is ever reached. The HAVE/NEED text is
            // therefore stamped on nameLabel and the Total cell's own
            // controls too - the two places a user hovering "do I have
            // enough?" actually looks - not just the row's blank gaps.
            // BuildTooltip/SetValueCellTooltip below and the initial call
            // are deliberately placed AFTER nameLabel/totalCell exist so
            // both are in scope for every (re)build, including the
            // AddReellipsis rebuild further down.
            void BuildTooltip()
            {
                var tooltipParts = new List<string>();
                if (nameLabel.Text != fullName)
                {
                    tooltipParts.Add(fullName);
                }
                if (!string.IsNullOrEmpty(hintText))
                {
                    tooltipParts.Add(hintText);
                }
                tooltipParts.AddRange(currencyLines);
                string tooltipText = tooltipParts.Count > 0 ? string.Join("\n", tooltipParts) : null;
                TooltipFacility.ApplyPlain(rowPanel, tooltipText);
                TooltipFacility.ApplyPlain(nameLabel, tooltipText);
                SetValueCellTooltip(totalCell, tooltipText);
            }
            BuildTooltip();

            // Qty + Each/Total cells reposition every drag tick
            // (no MeasureString - CoinCurrencyRenderer.RepositionValueCellRightAligned uses only
            // cached segment text widths). The name label and its source
            // tag are untouched here; both depend on ellipsis truncation
            // and only update at settle (RunReellipsis) below.
            //
            // M36b: bottomClearance 0 - ShoppingRowHeight (36) is immune to
            // the Container.Paint round-trip defect (see LabelHelpers.CreateRowDivider's
            // doc comment) and its icon frame is flush-fit with zero
            // slack; see the identical note in CreateUsedMaterialRow.
            RowRelayoutHelpers.FinishRow(
                rowPanel, panelWidth, rowHeight, isLast, 0, _sink,
                w =>
                {
                    var e = scan.EdgesFor(w);
                    qtyLabel.Location = new Point(e.QtyRightEdge - qtyWidth, 9);
                    CoinCurrencyRenderer.RepositionValueCellRightAligned(eachCell, e.EachRightEdge, 9);
                    CoinCurrencyRenderer.RepositionValueCellRightAligned(totalCell, e.TotalRightEdge, 9);
                },
                w => scan.EdgesFor(w).TotalRightEdge + PlanRelayoutMath.TableRightMargin);
            _sink.AddReellipsis(w =>
            {
                var e = scan.EdgesFor(w);
                if (IconNameRowHelpers.ReellipsizeName(
                    nameHandle, font, e.QtyRightEdge, qtyWidth, NameToQtyGap + tagReserve))
                {
                    BuildTooltip();
                }
                if (tagPanel != null)
                {
                    tagPanel.Location = new Point(NameX + nameLabel.Width + TagGap, 9);
                }
            });
        }
    }
}
