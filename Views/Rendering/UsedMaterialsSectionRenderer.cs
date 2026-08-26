using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    // The Used Materials row list.
    //
    // CreateUsedMaterialRow's
    // icon+ellipsized-name construction and its divider+relayout tail
    // go through the two shared row-shape helpers - IconNameRowHelpers
    // (build via CreateIconAndEllipsizedName, re-ellipsize via
    // ReellipsizeName) and RowRelayoutHelpers.FinishRow - both extracted
    // from this row and ShoppingListSectionRenderer.CreateShoppingRow, the
    // only two rows across the extracted renderers that actually share the
    // ellipsis shape (see IconNameRowHelpers' own doc comment for why
    // Crafting Steps/Disciplines/Recipes rows do not).
    //
    // sortState/onSortChanged carry the maintainer-requested clickable
    // column headers: this renderer only reads the state (to order its
    // rows and mark the active header) and asks the view to re-render when
    // a header is clicked - the state itself outlives every render, so a
    // regenerate keeps the sort the user chose.
    internal sealed class UsedMaterialsSectionRenderer
    {
        private readonly ISectionRelayoutSink _sink;
        private readonly TableSortState<PlanTableColumn> _sortState;
        private readonly Action _onSortChanged;

        // Session item-stat lookup (ItemMetadataService's own cache), so a
        // Used Materials row hovers the same rich item tooltip a tree row
        // does. Optional: a null lookup, or a null answer from it, degrades
        // to the full-name-when-truncated tooltip this row always had.
        private readonly Func<int, ItemStatBlock> _getItemStatBlock;

        internal UsedMaterialsSectionRenderer(
            ISectionRelayoutSink sink, TableSortState<PlanTableColumn> sortState, Action onSortChanged,
            Func<int, ItemStatBlock> getItemStatBlock = null)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _sortState = sortState ?? throw new ArgumentNullException(nameof(sortState));
            _onSortChanged = onSortChanged ?? throw new ArgumentNullException(nameof(onSortChanged));
            _getItemStatBlock = getItemStatBlock;
        }

        // Left x of the name column (past the row's 32px icon at x=8), and
        // the gap the ellipsis budget keeps between the name and the
        // Amount column.
        private const int NameX = 50;
        private const int NameToQtyGap = 12;

        /// <summary>
        /// One-pass pre-scan, as every other plan table has: the widest
        /// rendered "Nx" string, which is the Amount
        /// column's reserved band and therefore the Item column's ellipsis
        /// budget. Data-derived, so it is measured once here and reused by
        /// every row's relayout closure rather than re-measured per resize
        /// tick.
        /// <para>
        /// The band is max(widest data, header label): the "Amount" header
        /// right-aligns onto the same edge as the rows, and at the
        /// ColumnHeader tier it is routinely wider than a short "12x", so
        /// scanning data alone would let a name run under its own header.
        /// </para>
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            var font = UiFonts.Body;

            // Row ORDER only - the pre-scan below sees the same rows either
            // way, so every column edge (and PlanContentHeightMath's row
            // count) is identical sorted or not.
            var rows = PlanTableSorter.Sort(section.Rows, _sortState);

            string amountHeaderText =
                SortableHeaderLabel.Decorate("Amount", _sortState.IndicatorFor(PlanTableColumn.Amount));
            int maxQtyWidth =
                (int)System.Math.Ceiling(TableHeaderStyle.Font.MeasureString(amountHeaderText).Width);
            foreach (var row in rows)
            {
                int qtyWidth = (int)System.Math.Ceiling(font.MeasureString($"{row.Quantity}x").Width);
                if (qtyWidth > maxQtyWidth)
                {
                    maxQtyWidth = qtyWidth;
                }
            }

            // Item/Amount column header. Without it this is the one plan
            // table with a right-hand column nothing names, leaving the
            // reader to infer that a bare "12x" column is a quantity.
            // Unconditional, like the Shopping List's and the two
            // column-header tables', so it can never disagree with
            // PlanContentHeightMath.SectionBodyHeight, which counts it the
            // same way. No rightXForWidth: the Amount column is pinned to
            // the panel edge, which is ColumnHeaderRowRenderer's own default.
            ColumnHeaderRowRenderer.CreateColumnHeaderRow(
                contentFlow, panelWidth,
                SortableHeaderLabel.Decorate("Item", _sortState.IndicatorFor(PlanTableColumn.Item)), NameX,
                amountHeaderText, _sink,
                onLeftClick: () => SortBy(PlanTableColumn.Item),
                onRightClick: () => SortBy(PlanTableColumn.Amount),
                // The Item column is everything left of the Amount band -
                // the name's own ellipsis terms, with the gap split.
                leftColumnEndForWidth: w => PlanRelayoutMath.HeaderSplitBeforeColumn(
                    PlanRelayoutMath.PinnedRightEdge(w), maxQtyWidth, NameToQtyGap));

            for (int i = 0; i < rows.Count; i++)
            {
                CreateUsedMaterialRow(
                    rows[i], contentFlow, panelWidth, maxQtyWidth,
                    i == rows.Count - 1);
            }
        }

        private void SortBy(PlanTableColumn column)
        {
            _sortState.Cycle(column);
            _onSortChanged();
        }

        private void CreateUsedMaterialRow(
            PlanRowViewModel row, FlowPanel parent, int panelWidth,
            int maxQtyWidth, bool isLast)
        {
            const int rowHeight = PlanContentHeightMath.UsedMaterialRowHeight;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            int qtyRightEdge = PlanRelayoutMath.PinnedRightEdge(panelWidth);
            var font = UiFonts.Body;

            string qtyText = $"{row.Quantity}x";
            int qtyWidth = (int)System.Math.Ceiling(font.MeasureString(qtyText).Width);

            // Icon y=0 (was 1) - the 34px icon frame previously left
            // only 1px of clearance above rowHeight (36), which was exactly
            // enough for the old 1px divider but would overlap the new 2px
            // divider's top pixel by 1 row. Moving the icon up by 1 makes
            // frame height (34) + divider height (2) exactly fill rowHeight
            // with no overlap.
            // maxQtyWidth, not this row's own qtyWidth: the Amount column
            // is a reserved band right-aligned on the pinned edge, so the
            // name's budget stops at the band's LEFT edge. Budgeting
            // against one row's short "1x" would let its name run under the
            // column's widest value.
            string fullName = row.Label ?? "";
            var nameHandle = IconNameRowHelpers.CreateIconAndEllipsizedName(
                rowPanel, row.IconUrl, row.Rarity, 8, 0, fullName, font,
                qtyRightEdge, maxQtyWidth, NameToQtyGap, NameX, 9);
            // Composed at HOVER time, not here: a plan restored from disk
            // fills its stat cache in the background (Q13), and a snapshot
            // taken now could never show what lands after it. It also
            // keeps the compose work off the render path.
            Func<TooltipContent> buildTooltip = () => ItemRowTooltipComposer.BuildRowContent(
                _getItemStatBlock == null || row.ItemId <= 0 ? null : _getItemStatBlock(row.ItemId),
                fullName,
                nameHandle.NameLabel.Text != fullName,
                null);
            TooltipFacility.ApplyRichDeferred(rowPanel, buildTooltip);
            TooltipFacility.ApplyRichDeferred(nameHandle.NameLabel, buildTooltip);
            if (row.ItemId > 0)
            {
                IconControls.ApplyRichDeferredToIconTree(nameHandle.IconFrame, buildTooltip);
            }

            var qtyLabel = LabelHelpers.WithDescenderClearance(
                new Label()
                {
                    Text = qtyText,
                    Font = font,
                    TextColor = new Color(200, 200, 200),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(qtyRightEdge - qtyWidth, 9),
                    Parent = rowPanel,
                });
            TooltipFacility.ApplyRichDeferred(qtyLabel, buildTooltip);

            // Qty label position is a pure reposition (qtyWidth is
            // font-only); the name is left untouched during drag ticks and
            // only re-ellipsized at settle (RunReellipsis) to avoid a
            // MeasureString call per row per tick.
            //
            // bottomClearance 0 - UsedMaterialRowHeight (36) is
            // immune to the Container.Paint round-trip defect (see
            // LabelHelpers.CreateRowDivider's doc comment) and its icon frame is
            // flush-fit with zero slack; giving it clearance it doesn't
            // need would reintroduce the icon/divider overlap.
            RowRelayoutHelpers.FinishRow(
                rowPanel, panelWidth, rowHeight, isLast, 0, _sink,
                w =>
                {
                    qtyLabel.Location = new Point(
                        PlanRelayoutMath.PinnedRightEdge(w) - qtyWidth, 9);
                });
            // The re-ellipsis no longer re-stamps anything: the tooltip
            // builder above reads the label's CURRENT text when the box is
            // drawn, so a resize that truncates or untruncates the name is
            // already reflected.
            _sink.AddReellipsis(w => IconNameRowHelpers.ReellipsizeName(
                nameHandle, font, PlanRelayoutMath.PinnedRightEdge(w), maxQtyWidth, NameToQtyGap));
        }
    }
}
