using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-23b (m38-a1-architecture.md S3b-T2, continuing the WP-23 pilot):
    // moved verbatim out of CraftingPlanView's "7. Section builders
    // (continued)" region - the Used Materials row list only. Behavior is
    // unchanged: same row geometry, same PlanContentHeightMath/
    // PlanRelayoutMath calls, same LabelHelpers.CreateRowDivider usage
    // (DO-NOT-TOUCH #6 - divider math and its M36b 1px scissor clearance
    // untouched). The only edit inside the moved bodies is
    // _relayoutActions.Add -> the injected ISectionRelayoutSink.AddRelayout
    // and _reellipsisActions.Add -> ISectionRelayoutSink.AddReellipsis, both
    // semantics-preserving pass-throughs (see ISectionRelayoutSink's doc
    // comment). Per the WP-23 pilot's FORWARD NOTE, Used Materials depends
    // only on the already-extracted IconControls/RarityColors/LabelHelpers/
    // PlanRelayoutMath statics the pilot also used, so this section needed
    // no further dependency resolution.
    //
    // M38 WP-24 (m38-a2-simplify.md finding #3): CreateUsedMaterialRow's
    // icon+ellipsized-name construction and its divider+relayout tail now
    // go through the two shared row-shape helpers - IconNameRowHelpers
    // (build via CreateIconAndEllipsizedName, re-ellipsize via
    // ReellipsizeName) and RowRelayoutHelpers.FinishRow - both extracted
    // from this row and ShoppingListSectionRenderer.CreateShoppingRow, the
    // only two rows across the extracted renderers that actually share the
    // ellipsis shape (see IconNameRowHelpers' own doc comment for why
    // Crafting Steps/Disciplines/Recipes rows do not). Geometry unchanged -
    // see the WP-24 constant-by-constant table in the PR/commit body.
    internal sealed class UsedMaterialsSectionRenderer
    {
        private readonly ISectionRelayoutSink _sink;

        internal UsedMaterialsSectionRenderer(ISectionRelayoutSink sink)
        {
            // Mirrors the constructor-null-guard convention already used
            // for injected dependencies elsewhere in Views/ (ViewAdapter's
            // buildAction, SettingsTabContent's settings, FrameTicker's
            // step) and by DisciplinesSectionRenderer, the WP-23 pilot - the
            // sole production call site always passes `this`
            // (CraftingPlanView), but a later section renderer built on
            // this same pattern should fail loud, not with a deferred NRE
            // inside CreateUsedMaterialRow's first AddRelayout call.
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>
        /// Moved verbatim from CraftingPlanView.CreateUsedMaterialsBody.
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateUsedMaterialRow(section.Rows[i], contentFlow, panelWidth, i == section.Rows.Count - 1);
            }
        }

        // Moved verbatim from CraftingPlanView.CreateUsedMaterialRow, then
        // WP-24-refactored onto IconNameRowHelpers/RowRelayoutHelpers (see
        // the class doc comment above) - same geometry, same constants.
        private void CreateUsedMaterialRow(PlanRowViewModel row, FlowPanel parent, int panelWidth, bool isLast)
        {
            const int rowHeight = PlanContentHeightMath.UsedMaterialRowHeight;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            const int nameX = 50;
            int qtyRightEdge = panelWidth - 8;
            var font = GameService.Content.DefaultFont14;

            string qtyText = $"{row.Quantity}x";
            int qtyWidth = (int)System.Math.Ceiling(font.MeasureString(qtyText).Width);

            // M36: icon y=0 (was 1) - the 34px icon frame previously left
            // only 1px of clearance above rowHeight (36), which was exactly
            // enough for the old 1px divider but would overlap the new 2px
            // divider's top pixel by 1 row. Moving the icon up by 1 makes
            // frame height (34) + divider height (2) exactly fill rowHeight
            // with no overlap.
            string fullName = row.Label ?? "";
            var nameHandle = IconNameRowHelpers.CreateIconAndEllipsizedName(
                rowPanel, row.IconUrl, row.Rarity, 8, 0, fullName, font, qtyRightEdge, qtyWidth, 12, nameX, 9);
            if (nameHandle.NameLabel.Text != fullName)
            {
                rowPanel.BasicTooltipText = fullName;
            }

            var qtyLabel = new Label()
            {
                Text = qtyText,
                Font = font,
                TextColor = new Color(200, 200, 200),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(qtyRightEdge - qtyWidth, 9),
                Parent = rowPanel
            };

            // M33 C2b: qty label position is a pure reposition (qtyWidth is
            // font-only); the name is left untouched during drag ticks and
            // only re-ellipsized at settle (RunReellipsis) to avoid a
            // MeasureString call per row per tick.
            //
            // M36b: bottomClearance 0 - UsedMaterialRowHeight (36) is
            // immune to the Container.Paint round-trip defect (see
            // LabelHelpers.CreateRowDivider's doc comment) and its icon frame is
            // flush-fit with zero slack; giving it clearance it doesn't
            // need would reintroduce the icon/divider overlap M36 fixed.
            RowRelayoutHelpers.FinishRow(rowPanel, panelWidth, rowHeight, isLast, 0, _sink, w =>
            {
                qtyLabel.Location = new Point(w - 8 - qtyWidth, 9);
            });
            _sink.AddReellipsis(w =>
            {
                if (IconNameRowHelpers.ReellipsizeName(nameHandle, font, w - 8, qtyWidth, 12))
                {
                    rowPanel.BasicTooltipText = nameHandle.NameLabel.Text != fullName ? fullName : null;
                }
            });
        }
    }
}
