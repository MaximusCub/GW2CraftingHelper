using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView.CreateTextRow -
    // private static -> internal static, no logic changes.
    //
    // Lives in its own shared file (like PillColors) because it has three
    // call sites: CreateCollapsibleSection's default fallback case (the
    // one still inside CraftingPlanView), CraftStepsSectionRenderer's
    // TimegatedNotice branch, and SummarySectionRenderer's noteRows loop.
    // All three call TextRowRenderer.CreateTextRow directly - a forward
    // Views/Rendering call, never the reverse edge already reverted once
    // (commit 5c56b2a).
    internal static class TextRowRenderer
    {
        internal static void CreateTextRow(string text, FlowPanel parent, int panelWidth, ISectionRelayoutSink sink)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, PlanContentHeightMath.FallbackTextRowHeight),
                Parent = parent
            };
            new Label()
            {
                Text = "  " + text,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(8, 4),
                Parent = rowPanel
            };

            // Not width-dependent beyond the row's own cosmetic width (fixed
            // left-anchored text, m2 3.6's "no relayout needed" case).
            sink.AddRelayout(w => rowPanel.Size = new Point(w, PlanContentHeightMath.FallbackTextRowHeight));
        }
    }
}
