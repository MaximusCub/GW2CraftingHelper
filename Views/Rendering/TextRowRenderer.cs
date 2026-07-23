using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-23c (m38-a1-architecture.md S3b-T2, continuing the WP-23/WP-23b
    // extractions): moved verbatim out of CraftingPlanView.CreateTextRow -
    // private static -> internal static, no logic changes.
    //
    // Unlike ShoppingSourceTag (exactly one call site, moved directly into
    // ShoppingListSectionRenderer) this is the CreateTextRow analogue of the
    // GetPillColors fork WP-23b resolved with PillColors.cs: grepped every
    // call site in CraftingPlanView before moving anything and found THREE,
    // not one - the CraftingSteps section's TimegatedNotice branch (moving,
    // now inside CraftStepsSectionRenderer), the default fallback case in
    // CreateCollapsibleSection (staying in the view - a section type with no
    // dedicated body builder), and CreateSummarySectionBody's noteRows loop
    // (staying in the view - Summary is not part of this package's scope).
    // Because two non-extracted callers remain, CreateTextRow could not
    // simply move into CraftStepsSectionRenderer the way a single-call-site
    // helper would; extracted here instead, exactly mirroring the
    // GetPillColors -> PillColors.cs resolution: CraftingPlanView's two
    // remaining call sites now call TextRowRenderer.CreateTextRow directly
    // (forward Views/Rendering call, never the reverse edge the WP-21
    // findings fix, commit 5c56b2a, already reverted once).
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
