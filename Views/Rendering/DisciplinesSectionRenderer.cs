using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-23 (m38-a1-architecture.md S3b-T2 pilot): moved verbatim out of
    // CraftingPlanView's "7. Section builders (continued)" region - the
    // Required Disciplines row list only. Behavior is unchanged: same row
    // geometry, same PlanContentHeightMath/PlanRelayoutMath calls, same
    // LabelHelpers.CreateRowDivider usage (DO-NOT-TOUCH #6 - divider math
    // and its M36b 1px scissor clearance untouched). The only edit inside
    // the moved bodies is _relayoutActions.Add -> the injected
    // ISectionRelayoutSink.AddRelayout, which is a semantics-preserving
    // pass-through (see ISectionRelayoutSink's doc comment).
    //
    // The "Discipline"/"Level" column header (CreateCTableHeaderRow) stays
    // in CraftingPlanView for this pilot - it is shared chrome with the
    // not-yet-extracted Required Recipes section (CreateRecipesBody calls
    // the same method for its "Recipe"/"Status" header), and moving it here
    // would either widen this PR's scope to Recipes too or leave Recipes
    // calling into a class named for Disciplines. CraftingPlanView calls
    // CreateCTableHeaderRow immediately before constructing this renderer;
    // see the RequiredDisciplines case in CreateCollapsibleSection.
    internal sealed class DisciplinesSectionRenderer
    {
        private readonly ISectionRelayoutSink _sink;

        internal DisciplinesSectionRenderer(ISectionRelayoutSink sink)
        {
            // Mirrors the constructor-null-guard convention already used
            // for injected dependencies elsewhere in Views/ (ViewAdapter's
            // buildAction, SettingsTabContent's settings, FrameTicker's
            // step) - the sole production call site always passes `this`
            // (CraftingPlanView), but a later section renderer built on
            // this same pattern should fail loud, not with a deferred NRE
            // inside CreateDisciplineRow's first AddRelayout call.
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>
        /// Moved verbatim from CraftingPlanView.CreateDisciplinesBody's row
        /// loop (the header-row call that used to precede it stays in the
        /// view - see the class doc comment above).
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateDisciplineRow(section.Rows[i], contentFlow, panelWidth, i == section.Rows.Count - 1);
            }
        }

        // Moved verbatim from CraftingPlanView.CreateDisciplineRow. Only
        // change: _relayoutActions.Add(...) -> _sink.AddRelayout(...).
        private void CreateDisciplineRow(PlanRowViewModel row, FlowPanel parent, int panelWidth, bool isLast)
        {
            const int rowHeight = PlanContentHeightMath.DisciplineRowHeight;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };
            var font = GameService.Content.DefaultFont14;

            new Label()
            {
                Text = row.Label ?? "", Font = font,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(8, 7), Parent = rowPanel
            };
            var levelLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, row.Sublabel, font, Color.White, panelWidth - 8, 7);

            // M36b: bottomClearance 1 - DisciplineRowHeight (32) is
            // VULNERABLE to the Container.Paint round-trip defect (see
            // LabelHelpers.CreateRowDivider's doc comment), same ~10.2% vanish rate as
            // the 44px rows; the M36b investigation confirmed this via
            // simulation but omitted it from its fix list by oversight.
            // No icon in this row (two DefaultFont14 labels at y=7 only),
            // so there is no icon-clearance side effect to worry about -
            // the new divider top (rowHeight - 3 = 29) sits well clear of
            // the text baseline.
            Panel divider = isLast ? null : LabelHelpers.CreateRowDivider(rowPanel, panelWidth, rowHeight, 1);

            _sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, rowHeight);
                levelLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(w - 8, levelLabel.Width), 7);
                if (divider != null) divider.Size = new Point(w, 2);
            });
        }
    }
}
