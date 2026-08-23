using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;

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
    // by the caller before this row is built.
    //
    // middleXForWidth exists for the ONE caller whose middle column is not
    // fixed: the Recipe Tree's "Source" header sits over the decision-pill
    // column, whose x is derived from the panel width
    // (PlanRelayoutMath.ComputeTreeColumnEdges), so a build-time constant
    // would strand it the moment the window is dragged. Supplying it opts
    // that label into the AddRelayout closure below; omitting it keeps the
    // previous fixed-x behaviour for every other caller. Still
    // position-only, per the interface's "position/width-only" contract -
    // see ISectionRelayoutSink.AddRelayout's doc comment.
    //
    // rightXForWidth is the same escape hatch for the right label: the
    // Recipe Tree's "Cost" header sits over a cost column that is no longer
    // pinned to the panel edge (audit batch H pulls the whole pill+cost
    // block in beside the names), so it has to track the same
    // PlanRelayoutMath.ComputeTreeColumnEdges arithmetic its rows do.
    // Omitting it keeps the previous panelWidth-8 anchor for every other
    // caller.
    internal static class CTableHeaderRenderer
    {
        internal static void CreateCTableHeaderRow(
            FlowPanel parent, int panelWidth, string leftLabel, int leftX, string rightLabel, ISectionRelayoutSink sink,
            string middleLabel = null, int middleX = 0, Func<int, int> middleXForWidth = null,
            Func<int, int> rightXForWidth = null)
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
            Label middleLabelControl = null;
            if (!string.IsNullOrEmpty(middleLabel))
            {
                middleLabelControl = new Label()
                {
                    Text = middleLabel, Font = font, TextColor = Color.White,
                    AutoSizeWidth = true, AutoSizeHeight = true,
                    Location = new Point(middleXForWidth != null ? middleXForWidth(panelWidth) : middleX, 5),
                    Parent = rowPanel
                };
            }
            var rightLabelControl = LabelHelpers.CreateRightAlignedLabel(
                rowPanel, rightLabel, font, Color.White,
                rightXForWidth != null ? rightXForWidth(panelWidth) : panelWidth - 8, 5);

            sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, PlanContentHeightMath.CTableHeaderRowHeight);
                int rightEdge = rightXForWidth != null ? rightXForWidth(w) : w - 8;
                rightLabelControl.Location = new Point(PlanRelayoutMath.RightAlignedX(rightEdge, rightLabelControl.Width), 5);
                if (middleLabelControl != null && middleXForWidth != null)
                {
                    middleLabelControl.Location = new Point(middleXForWidth(w), 5);
                }
            });
        }
    }
}
