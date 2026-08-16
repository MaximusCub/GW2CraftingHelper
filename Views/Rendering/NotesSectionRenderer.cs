using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    // design-plan-notes.md (Notes section, Option 1): renders every row of
    // PlanSectionType.Notes - all PlanRowType.NoteLine - as a plain text
    // row with an optional right-aligned coin cell. Mirrors TextRowRenderer's
    // shape exactly, plus the coin cell CoinCurrencyRenderer.
    // RenderValueCellRightAligned/RepositionValueCellRightAligned already
    // give every shopping/tree value cell, drawn ONLY when row.CoinValue >
    // 0 (the CreateCollapsibleSection default fallback would call plain
    // TextRowRenderer.CreateTextRow for every row instead, which never
    // renders a coin value at all - silently dropping every reclaim amount
    // this section shows - so this section needs its own case in that
    // switch rather than falling through to the default).
    //
    // Row-height discipline is load-bearing (design-plan-notes.md section
    // 4): PlanSectionType.Notes gets no case in PlanContentHeightMath.
    // SectionBodyHeight (DO-NOT-TOUCH) on purpose - it falls through to
    // that method's existing default arm (rows.Count * FallbackTextRowHeight),
    // which is only correct as long as EVERY NoteLine row - with or without
    // a coin cell - renders at exactly that height. Both branches below use
    // the same fixed rowHeight; the DEBUG assert at the end of CreateNoteRow
    // is the load-bearing check this class's own correctness depends on.
    //
    // Review fix (findings 2/3): the label is now ellipsized against the
    // available width (rightEdge minus the coin cell, when present) rather
    // than a raw AutoSizeWidth label with no cap - matching every other
    // label+right-value row shape in this codebase (UsedMaterialsSectionRenderer
    // -> IconNameRowHelpers.CreateIconAndEllipsizedName / LabelHelpers.
    // EllipsizeToWidth). Unlike that icon+name helper (which is
    // icon-column-specific), this row has no icon, so it calls
    // LabelHelpers.EllipsizeToWidth/PlanRelayoutMath.NameMaxWidthBeforeColumn
    // directly rather than going through IconNameRowHelpers. A truncated
    // row also gets a BasicTooltipText with the full, untruncated text -
    // same "ellipsize + tooltip" contract as every other truncatable row.
    // Re-ellipsized at settle via AddReellipsis, mirroring
    // IconNameRowHelpers.ReellipsizeName's own "only touch Text/tooltip
    // when the displayed string actually changed" gate.
    internal sealed class NotesSectionRenderer
    {
        private readonly ISectionRelayoutSink _sink;

        internal NotesSectionRenderer(ISectionRelayoutSink sink)
        {
            // Mirrors the constructor-null-guard convention every other
            // section renderer uses (see DisciplinesSectionRenderer/
            // UsedMaterialsSectionRenderer's own doc comments) - the sole
            // production call site always passes `this`
            // (CraftingPlanView), but a fail-loud guard here beats a
            // deferred NRE inside the first AddRelayout call.
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            foreach (var row in section.Rows)
            {
                CreateNoteRow(row, contentFlow, panelWidth);
            }
        }

        private void CreateNoteRow(PlanRowViewModel row, FlowPanel parent, int panelWidth)
        {
            const int rowHeight = PlanContentHeightMath.FallbackTextRowHeight;
            const int labelX = 8;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };
            var font = GameService.Content.DefaultFont14;

            string fullText = "  " + (row.Label ?? "");
            bool hasCoin = row.CoinValue > 0;

            // Coin cell only when CoinValue > 0 - mirrors
            // CoinCurrencyRenderer's own "hasCoin = copper > 0" convention,
            // but unlike RenderValueCellRightAligned's own dash fallback, a
            // plain-text NoteLine (competency/forge-scope) renders NO value
            // cell at all rather than an unpriced dash - there is no price
            // concept for those lines to begin with.
            //
            // MeasureValueWidth is called BEFORE the cell itself is built
            // so the label's own max width can reserve room for it -
            // mirrors MeasureValueWidth's own documented shopping-list
            // pre-scan use, same "measure-then-build" ordering.
            int coinCellWidth = hasCoin ? CoinCurrencyRenderer.MeasureValueWidth(row.CoinValue, null, font) : 0;
            int gapBeforeCoin = hasCoin ? 12 : 0;

            int maxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                panelWidth - 8, coinCellWidth, gapBeforeCoin, labelX);
            string displayText = LabelHelpers.EllipsizeToWidth(font, fullText, maxWidth);

            var label = new Label()
            {
                Text = displayText,
                Font = font,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(labelX, 4),
                Parent = rowPanel
            };
            if (displayText != fullText)
            {
                rowPanel.BasicTooltipText = row.Label ?? "";
            }

            if (hasCoin)
            {
                int rightEdge = panelWidth - 8;
                var coinHandle = CoinCurrencyRenderer.RenderValueCellRightAligned(
                    rowPanel, row.CoinValue, null, rightEdge, 4, font);

                _sink.AddRelayout(w =>
                {
                    rowPanel.Size = new Point(w, rowHeight);
                    CoinCurrencyRenderer.RepositionValueCellRightAligned(coinHandle, w - 8, 4);
                });
            }
            else
            {
                _sink.AddRelayout(w => rowPanel.Size = new Point(w, rowHeight));
            }

            _sink.AddReellipsis(w =>
            {
                int newCoinCellWidth = hasCoin
                    ? CoinCurrencyRenderer.MeasureValueWidth(row.CoinValue, null, font)
                    : 0;
                int newMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                    w - 8, newCoinCellWidth, gapBeforeCoin, labelX);
                string newDisplayText = LabelHelpers.EllipsizeToWidth(font, fullText, newMaxWidth);
                if (label.Text != newDisplayText)
                {
                    label.Text = newDisplayText;
                    rowPanel.BasicTooltipText = newDisplayText != fullText ? (row.Label ?? "") : null;
                }
            });

#if DEBUG
            // Load-bearing per this class's own doc comment: PlanSectionType.
            // Notes relies on PlanContentHeightMath's default arm (rows.Count
            // * FallbackTextRowHeight, DO-NOT-TOUCH), which is only correct
            // when every row renders at exactly that height. Fail loud in
            // DEBUG rather than silently drifting the section's real height
            // out of sync with what CraftingPlanView computed for it.
            //
            // Review fix (finding 3): re-reading rowPanel.Height here (set
            // from the same const rowHeight two statements above the
            // original version of this assert) can never fail - it guards
            // nothing. The real ways a NoteLine could break the 28px
            // contract are a child control growing taller than the row (a
            // future WrapText/larger-font change to the label, or a coin
            // cell taller than rowHeight - 4), so assert on the CHILDREN's
            // own extents instead.
            foreach (var child in rowPanel.Children)
            {
                System.Diagnostics.Debug.Assert(
                    child.Bottom <= rowHeight,
                    "NotesSectionRenderer: every NoteLine row's child controls must fit within " +
                    "PlanContentHeightMath.FallbackTextRowHeight - see this class's own doc comment.");
            }
#endif
        }
    }
}
