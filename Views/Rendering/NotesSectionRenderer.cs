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
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };
            var font = GameService.Content.DefaultFont14;

            new Label()
            {
                Text = "  " + (row.Label ?? ""),
                Font = font,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(8, 4),
                Parent = rowPanel
            };

            // Coin cell only when CoinValue > 0 - mirrors
            // CoinCurrencyRenderer's own "hasCoin = copper > 0" convention,
            // but unlike RenderValueCellRightAligned's own dash fallback, a
            // plain-text NoteLine (competency/forge-scope) renders NO value
            // cell at all rather than an unpriced dash - there is no price
            // concept for those lines to begin with.
            if (row.CoinValue > 0)
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

#if DEBUG
            // Load-bearing per this class's own doc comment: PlanSectionType.
            // Notes relies on PlanContentHeightMath's default arm (rows.Count
            // * FallbackTextRowHeight, DO-NOT-TOUCH), which is only correct
            // when every row renders at exactly that height. Fail loud in
            // DEBUG rather than silently drifting the section's real height
            // out of sync with what CraftingPlanView computed for it.
            System.Diagnostics.Debug.Assert(
                rowPanel.Height == rowHeight,
                "NotesSectionRenderer: every NoteLine row must render at exactly " +
                "PlanContentHeightMath.FallbackTextRowHeight - see this class's own doc comment.");
#endif
        }
    }
}
