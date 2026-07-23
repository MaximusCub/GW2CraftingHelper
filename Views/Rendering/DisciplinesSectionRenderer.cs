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
    // The "Discipline"/"Level" column header (CreateCTableHeaderRow) stayed
    // in CraftingPlanView for this pilot - it was shared chrome with the
    // not-yet-extracted Required Recipes section (CreateRecipesBody called
    // the same method for its "Recipe"/"Status" header), and moving it then
    // would have either widened this pilot's scope to Recipes too or left
    // Recipes calling into a class named for Disciplines. M38 WP-23c
    // extracted Required Recipes too, so both callers are now extracted
    // section renderers - the stay-in-the-view rationale no longer applies.
    // The header call moved into this class's Render() below (see
    // Views/Rendering/CTableHeaderRenderer's doc comment for the full
    // resolution); CraftingPlanView.CreateCollapsibleSection no longer
    // references the c-table header for either section.
    //
    // M38 WP-24 (m38-a2-simplify.md finding #3): CreateDisciplineRow's
    // divider+relayout tail now goes through RowRelayoutHelpers.FinishRow -
    // the shared "row panel resize + extra reposition + divider resize"
    // shape confirmed identical across all five extracted renderers' row
    // builders (see that class's doc comment). This row has no icon and no
    // name column at all (just two plain DefaultFont14 labels), so it does
    // not match IconNameRowHelpers (the other WP-24 helper) and stays
    // hand-rolled there - see IconNameRowHelpers' own doc comment. Geometry
    // unchanged - see the WP-24 constant-by-constant table in the PR/commit
    // body.
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
        /// loop, plus the CreateCTableHeaderRow call this renderer now owns
        /// directly (M38 WP-23c - see the class doc comment above).
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            CTableHeaderRenderer.CreateCTableHeaderRow(contentFlow, panelWidth, "Discipline", 8, "Level", _sink);
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
            RowRelayoutHelpers.FinishRow(rowPanel, panelWidth, rowHeight, isLast, 1, _sink, w =>
            {
                levelLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(w - 8, levelLabel.Width), 7);
            });
        }
    }
}
