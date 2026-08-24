using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView's "7. Section builders
    // (continued)" region - the Used Materials row list only. Behavior is
    // unchanged: same row geometry, same PlanContentHeightMath/
    // PlanRelayoutMath calls, same LabelHelpers.CreateRowDivider usage
    // (divider math and its 1px scissor clearance
    // untouched). The only edit inside the moved bodies is
    // _relayoutActions.Add -> the injected ISectionRelayoutSink.AddRelayout
    // and _reellipsisActions.Add -> ISectionRelayoutSink.AddReellipsis, both
    // semantics-preserving pass-throughs (see ISectionRelayoutSink's doc
    // comment).
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
            // Mirrors the constructor-null-guard convention already used
            // for injected dependencies elsewhere in Views/ (ViewAdapter's
            // buildAction, SettingsTabContent's settings, FrameTicker's
            // step) and by DisciplinesSectionRenderer - the
            // sole production call site always passes `this`
            // (CraftingPlanView), but a later section renderer built on
            // this same pattern should fail loud, not with a deferred NRE
            // inside CreateUsedMaterialRow's first AddRelayout call.
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
        /// Moved verbatim from CraftingPlanView.CreateUsedMaterialsBody,
        /// then given the one-pass pre-scan every other plan table already
        /// had (audit batch H): the widest rendered "Nx" string and the
        /// widest UNTRUNCATED name extent, which together let the Amount
        /// column be pulled in beside the names rather than pinned to the
        /// panel edge with a growing empty band between them. Both are
        /// data-derived, so they are measured once here and reused by every
        /// row's relayout closure rather than re-measured per resize tick.
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            var font = UiFonts.Body;

            // Row ORDER only - the pre-scan below sees the same rows either
            // way, so every column edge (and PlanContentHeightMath's row
            // count) is identical sorted or not.
            var rows = PlanTableSorter.Sort(section.Rows, _sortState);

            int maxQtyWidth = 0;
            int widestNameEnd = 0;
            foreach (var row in rows)
            {
                int qtyWidth = (int)System.Math.Ceiling(font.MeasureString($"{row.Quantity}x").Width);
                if (qtyWidth > maxQtyWidth) maxQtyWidth = qtyWidth;

                int nameEnd = NameX + (int)System.Math.Ceiling(font.MeasureString(row.Label ?? "").Width);
                if (nameEnd > widestNameEnd) widestNameEnd = nameEnd;
            }

            // Item/Amount column header. This was the one plan table with
            // a right-hand column and no header naming it (audit batch J,
            // L2) - the reader had to infer that a bare "12x" column was a
            // quantity. Unconditional, like the Shopping List's and the two
            // c-tables', so it can never disagree with
            // PlanContentHeightMath.SectionBodyHeight, which counts it the
            // same way. rightXForWidth because the Amount column is no
            // longer pinned to the panel edge (batch H) - it has to track
            // the same QtyRightEdge its rows do.
            CTableHeaderRenderer.CreateCTableHeaderRow(
                contentFlow, panelWidth,
                SortableHeaderLabel.Decorate("Item", _sortState.IndicatorFor(PlanTableColumn.Item)), NameX,
                SortableHeaderLabel.Decorate("Amount", _sortState.IndicatorFor(PlanTableColumn.Amount)), _sink,
                rightXForWidth: w => QtyRightEdge(w, maxQtyWidth, widestNameEnd),
                onLeftClick: () => SortBy(PlanTableColumn.Item),
                onRightClick: () => SortBy(PlanTableColumn.Amount));

            for (int i = 0; i < rows.Count; i++)
            {
                CreateUsedMaterialRow(
                    rows[i], contentFlow, panelWidth, maxQtyWidth, widestNameEnd,
                    i == rows.Count - 1);
            }
        }

        private void SortBy(PlanTableColumn column)
        {
            _sortState.Cycle(column);
            _onSortChanged();
        }

        /// <summary>
        /// Right edge of the Amount column at a given panel width - the one
        /// formula the build pass and both resize closures share.
        /// </summary>
        private static int QtyRightEdge(int panelWidth, int maxQtyWidth, int widestNameEnd)
        {
            return PlanRelayoutMath.RightBlockRightEdge(panelWidth, maxQtyWidth, widestNameEnd);
        }

        // Moved verbatim from CraftingPlanView.CreateUsedMaterialRow, then
        // refactored onto IconNameRowHelpers/RowRelayoutHelpers (see
        // the class doc comment above) - same geometry, same constants.
        private void CreateUsedMaterialRow(
            PlanRowViewModel row, FlowPanel parent, int panelWidth,
            int maxQtyWidth, int widestNameEnd, bool isLast)
        {
            const int rowHeight = PlanContentHeightMath.UsedMaterialRowHeight;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            int qtyRightEdge = QtyRightEdge(panelWidth, maxQtyWidth, widestNameEnd);
            var font = UiFonts.Body;

            string qtyText = $"{row.Quantity}x";
            int qtyWidth = (int)System.Math.Ceiling(font.MeasureString(qtyText).Width);

            // Icon y=0 (was 1) - the 34px icon frame previously left
            // only 1px of clearance above rowHeight (36), which was exactly
            // enough for the old 1px divider but would overlap the new 2px
            // divider's top pixel by 1 row. Moving the icon up by 1 makes
            // frame height (34) + divider height (2) exactly fill rowHeight
            // with no overlap.
            string fullName = row.Label ?? "";
            var nameHandle = IconNameRowHelpers.CreateIconAndEllipsizedName(
                rowPanel, row.IconUrl, row.Rarity, 8, 0, fullName, font, qtyRightEdge, qtyWidth, NameToQtyGap, NameX, 9);
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
                    Parent = rowPanel
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
                    qtyLabel.Location = new Point(QtyRightEdge(w, maxQtyWidth, widestNameEnd) - qtyWidth, 9);
                },
                w => QtyRightEdge(w, maxQtyWidth, widestNameEnd) + PlanRelayoutMath.TableRightMargin);
            // The re-ellipsis no longer re-stamps anything: the tooltip
            // builder above reads the label's CURRENT text when the box is
            // drawn, so a resize that truncates or untruncates the name is
            // already reflected.
            _sink.AddReellipsis(w => IconNameRowHelpers.ReellipsizeName(
                nameHandle, font, QtyRightEdge(w, maxQtyWidth, widestNameEnd), qtyWidth, NameToQtyGap));
        }
    }
}
