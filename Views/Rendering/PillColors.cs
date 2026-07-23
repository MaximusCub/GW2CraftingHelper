using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-23b (m38-a1-architecture.md S3b-T2, continuing the WP-23 pilot):
    // moved verbatim out of CraftingPlanView's "9. Recipe tree rendering"
    // region - private static -> internal static, no logic changes.
    //
    // GetPillColors could not move alongside either of its two call sites:
    // it is used both by the Shopping List section's source-tag panel (see
    // ShoppingListSectionRenderer, extracted alongside this file in the same
    // package) and by CraftingPlanView.RenderDecisionPills (the recipe
    // tree's decision pills, not yet extracted). The WP-23 pilot's FORWARD
    // NOTE (docs/KNOWN-ISSUES.md) flagged exactly this fork and named the
    // resolution: extract the shared piece to Views/Rendering and have
    // CraftingPlanView forward to it, rather than bump GetPillColors
    // private -> internal on CraftingPlanView again (which would reintroduce
    // the reverse Views/Rendering -> CraftingPlanView dependency edge the
    // WP-21 findings fix, commit 5c56b2a, already reverted once for exactly
    // this reason). CraftingPlanView.RenderDecisionPills now calls
    // PillColors.GetPillColors exactly as it already calls
    // RarityColors.GetRarityBorderColor - a forward call into
    // Views/Rendering, never the other way around.
    internal static class PillColors
    {
        /// <summary>
        /// isIgnoreActive is only meaningful for PillKind.Ignore (whether
        /// THIS specific Ignore pill is the active/"IGNORED" state, i.e.
        /// node.IsIgnored) - ignored for every other kind.
        /// </summary>
        internal static void GetPillColors(PillKind kind, bool isIgnoreActive, out Color border, out Color fill)
        {
            switch (kind)
            {
                case PillKind.Selected:
                    border = new Color(45, 197, 14); // #2DC50E
                    fill = border * 0.15f;
                    break;
                case PillKind.Have:
                    border = new Color(113, 113, 255); // #7171FF
                    fill = border * 0.15f;
                    break;
                case PillKind.Available:
                    border = new Color(138, 138, 138); // #8A8A8A
                    fill = Color.Transparent;
                    break;
                case PillKind.OwnedInfo:
                    // Muted gold, distinct from every other pill hue -
                    // informational only, never confused with a selectable
                    // source (M34-B2b).
                    border = new Color(201, 162, 39); // #C9A227
                    fill = border * 0.15f;
                    break;
                case PillKind.Ignore:
                    // Amber when active ("IGNORED", currently toggled on);
                    // plain clickable grey (matching Available) otherwise -
                    // never Selected's green, to avoid reading as "the
                    // chosen acquisition source" (M34-B2b).
                    border = isIgnoreActive ? new Color(229, 168, 60) : new Color(138, 138, 138); // #E5A83C / #8A8A8A
                    fill = isIgnoreActive ? border * 0.15f : Color.Transparent;
                    break;
                case PillKind.AchievementBitDeduped:
                    // Muted violet - distinct from Have's blue and
                    // OwnedInfo's gold: nothing here is actually owned, just
                    // already required elsewhere (M37, KNOWN-ISSUES #26).
                    border = new Color(155, 118, 219); // #9B76DB
                    fill = border * 0.15f;
                    break;
                case PillKind.Locked:
                default:
                    border = new Color(107, 107, 107); // #6B6B6B
                    fill = Color.Black * 0.3f;
                    break;
            }
        }
    }
}
