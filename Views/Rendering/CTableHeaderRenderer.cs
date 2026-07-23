using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-23c (m38-a1-architecture.md S3b-T2, continuing the WP-23/WP-23b
    // extractions): moved verbatim out of CraftingPlanView.CreateCTableHeaderRow -
    // private static -> internal static, no logic changes.
    //
    // The WP-23 pilot deliberately left this one in CraftingPlanView,
    // calling it directly immediately before constructing
    // DisciplinesSectionRenderer (see that class's original doc comment):
    // moving it then would have either widened the pilot's scope to
    // Required Recipes (its only other caller, not yet extracted) or left
    // Required Recipes calling into a class named for Disciplines. This
    // package extracts Required Recipes too, so BOTH callers are now
    // extracted section renderers - the pilot's stay-in-the-view rationale
    // no longer applies, and each renderer now owns its own header call
    // directly inside Render(), mirroring how ShoppingListSectionRenderer
    // already owns CreateShoppingListHeaderRow (WP-23b) rather than having
    // CraftingPlanView call it on the renderer's behalf. CraftingPlanView's
    // CreateCollapsibleSection no longer references the c-table header at
    // all for either section (see the RequiredDisciplines/RequiredRecipes
    // cases).
    internal static class CTableHeaderRenderer
    {
        internal static void CreateCTableHeaderRow(
            FlowPanel parent, int panelWidth, string leftLabel, int leftX, string rightLabel, ISectionRelayoutSink sink)
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
            var rightLabelControl = LabelHelpers.CreateRightAlignedLabel(rowPanel, rightLabel, font, Color.White, panelWidth - 8, 5);

            sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, PlanContentHeightMath.CTableHeaderRowHeight);
                rightLabelControl.Location = new Point(PlanRelayoutMath.RightAlignedX(w - 8, rightLabelControl.Width), 5);
            });
        }
    }
}
