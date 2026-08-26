using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    // Three call sites: CreateCollapsibleSection's default fallback case
    // (still inside CraftingPlanView), CraftStepsSectionRenderer's
    // TimegatedNotice branch, and SummarySectionRenderer's noteRows loop -
    // which is why it is a shared file rather than a private helper on any
    // one of them.
    internal static class TextRowRenderer
    {
        internal static void CreateTextRow(string text, FlowPanel parent, int panelWidth, ISectionRelayoutSink sink)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, PlanContentHeightMath.FallbackTextRowHeight),
                Parent = parent,
            };
            LabelHelpers.WithDescenderClearance(new Label()
            {
                Font = UiFonts.Body,
                Text = "  " + text,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(8, 4),
                Parent = rowPanel,
            });

            // Not width-dependent beyond the row's own cosmetic width (fixed
            // left-anchored text, m2 3.6's "no relayout needed" case).
            sink.AddRelayout(w => rowPanel.Size = new Point(w, PlanContentHeightMath.FallbackTextRowHeight));
        }
    }
}
