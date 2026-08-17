using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView.CreateCTableHeaderRow -
    // private static -> internal static, no logic changes. Each c-table
    // section renderer (Disciplines/Recipes) owns its own header call
    // directly inside Render(), mirroring how ShoppingListSectionRenderer
    // owns CreateShoppingListHeaderRow; CraftingPlanView itself no longer
    // references the c-table header at all.
    //
    // Optional middleLabel/middleX so Required Disciplines
    // can honestly label its per-character availability text with a
    // "Characters" header once that text lines up on a fixed column (see
    // DisciplinesSectionRenderer's own comment on Render() for why the
    // column has to be fixed, not per-row). Defaulted to null/0 so the
    // Required Recipes call site (still a plain left/right header) needs no
    // change. Like leftLabel, middleLabel sits at a fixed X computed once
    // by the caller before this row is built - it is never repositioned by
    // the AddRelayout closure below (only rowPanel.Size and the
    // right-aligned column move on resize, per the interface's "position/
    // width-only" contract - see ISectionRelayoutSink.AddRelayout's doc
    // comment).
    internal static class CTableHeaderRenderer
    {
        internal static void CreateCTableHeaderRow(
            FlowPanel parent, int panelWidth, string leftLabel, int leftX, string rightLabel, ISectionRelayoutSink sink,
            string middleLabel = null, int middleX = 0)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, PlanContentHeightMath.CTableHeaderRowHeight),
                BackgroundColor = new Color(35, 35, 35),
                Parent = parent
            };
            var font = GameService.Content.DefaultFont14;
            new Label()
            {
                Text = leftLabel, Font = font, TextColor = Color.White,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(leftX, 5), Parent = rowPanel
            };
            if (!string.IsNullOrEmpty(middleLabel))
            {
                new Label()
                {
                    Text = middleLabel, Font = font, TextColor = Color.White,
                    AutoSizeWidth = true, AutoSizeHeight = true,
                    Location = new Point(middleX, 5), Parent = rowPanel
                };
            }
            var rightLabelControl = LabelHelpers.CreateRightAlignedLabel(rowPanel, rightLabel, font, Color.White, panelWidth - 8, 5);

            sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, PlanContentHeightMath.CTableHeaderRowHeight);
                rightLabelControl.Location = new Point(PlanRelayoutMath.RightAlignedX(w - 8, rightLabelControl.Width), 5);
            });
        }
    }
}
