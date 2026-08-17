using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-23c (m38-a1-architecture.md S3b-T2, continuing the WP-23/WP-23b
    // extractions): moved verbatim out of CraftingPlanView's "7. Section
    // builders (continued)" region - the Required Recipes row list (with
    // BOTH row heights: RecipeRowHeightWithSublabel (44px) and
    // RecipeRowHeightNoSublabel (36px, per the M36 fix-pass correction) and
    // the Auto-learned/Learned/Missing! status tags. Behavior is unchanged:
    // same row geometry, same PlanContentHeightMath/PlanRelayoutMath calls,
    // same LabelHelpers.CreateRowDivider usage (DO-NOT-TOUCH #6 - divider
    // math, its per-branch bottomClearance selection, and the M36b 1px
    // scissor clearance untouched). The only edit inside the moved row body
    // is _relayoutActions.Add -> the injected ISectionRelayoutSink.AddRelayout
    // (a semantics-preserving pass-through - see ISectionRelayoutSink's doc
    // comment).
    //
    // CreateCTableHeaderRow (the shared "Recipe"/"Status" column header,
    // also used by Required Disciplines' "Discipline"/"Level" header) moves
    // with this package too, into its own Views/Rendering/CTableHeaderRenderer -
    // see that class's doc comment for why: the WP-23 pilot deliberately
    // left it in CraftingPlanView because Required Recipes (this section)
    // was not yet extracted; now that both callers are extracted section
    // renderers, this renderer's Render() calls CTableHeaderRenderer
    // directly, exactly as DisciplinesSectionRenderer now does, rather than
    // relying on CraftingPlanView to call it first.
    //
    // CreateRecipeRow's
    // divider+relayout tail now goes through RowRelayoutHelpers.FinishRow -
    // the shared "row panel resize + extra reposition + divider resize"
    // shape confirmed identical across all five extracted renderers' row
    // builders (see that class's doc comment). This row's name label is
    // NOT run through IconNameRowHelpers (the other WP-24 helper): it has
    // no width cap or ellipsis at all (row.Label renders in full,
    // regardless of length), an optional sublabel line BELOW the name
    // rather than a same-line secondary label, and an icon y that varies
    // with hasSublabel - a genuinely different shape from the two
    // ellipsized-name rows IconNameRowHelpers actually covers; forcing it
    // through that helper would mean either inventing ellipsis this row
    // never had or dropping its sublabel line, both real behavior changes,
    // so it stays hand-rolled - see IconNameRowHelpers' own doc comment.
    // Geometry unchanged - see the WP-24 constant-by-constant table in the
    // PR/commit body.
    internal sealed class RecipesSectionRenderer
    {
        private readonly ISectionRelayoutSink _sink;

        internal RecipesSectionRenderer(ISectionRelayoutSink sink)
        {
            // Mirrors the constructor-null-guard convention already used
            // for injected dependencies elsewhere in Views/ (ViewAdapter's
            // buildAction, SettingsTabContent's settings, FrameTicker's
            // step) and by every other section renderer on this pattern -
            // the sole production call site always passes `this`
            // (CraftingPlanView), but a later section renderer built on
            // this same pattern should fail loud, not with a deferred NRE
            // inside CreateRecipeRow's first AddRelayout call.
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>
        /// Moved verbatim from CraftingPlanView.CreateRecipesBody, plus the
        /// CreateCTableHeaderRow call this renderer now owns directly (see
        /// the class doc comment above).
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            CTableHeaderRenderer.CreateCTableHeaderRow(contentFlow, panelWidth, "Recipe", 50, "Status", _sink);
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateRecipeRow(section.Rows[i], contentFlow, panelWidth, i == section.Rows.Count - 1);
            }
        }

        // The no-sublabel branch's rowHeight (32)
        // left the 34px CreateRarityFramedIcon default frame at y=1
        // overflowing rowHeight by 3px even BEFORE the M36 divider-width
        // change (icon bottom = 1 + 34 = 35, rowHeight = 32) - pre-existing
        // negative headroom, not "several pixels of headroom" as
        // KNOWN-ISSUES #23 previously (incorrectly) claimed for this row,
        // and made 1px worse once that row's divider grew from 1px to 2px
        // (needed 34 + 2 = 36 to sit flush, still only had 32). Fixed
        // coherently, mirroring the Used Materials/Shopping List pattern
        // already on this branch: RecipeRowHeightNoSublabel raised to 36
        // (icon at y=0, 34 tall, + the 2px divider = exact fit, zero
        // overlap) and this branch's icon y nudged from 1 to 0 to match.
        // The WithSublabel branch (44) already had ample headroom and is
        // unchanged.
        //
        // Moved verbatim from CraftingPlanView.CreateRecipeRow. Only
        // change: _relayoutActions.Add(...) -> _sink.AddRelayout(...).
        private void CreateRecipeRow(PlanRowViewModel row, FlowPanel parent, int panelWidth, bool isLast)
        {
            bool hasSublabel = !string.IsNullOrEmpty(row.Sublabel);
            int rowHeight = hasSublabel
                ? PlanContentHeightMath.RecipeRowHeightWithSublabel
                : PlanContentHeightMath.RecipeRowHeightNoSublabel;

            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            // A context action
            // (right-click), not a visible icon - the row already packs an
            // icon/name/optional sublabel/right-aligned status tag into a
            // fixed height with no spare column.
            // Right-click also cannot collide with the row's existing
            // interactions (this row has none - unlike the Recipe Tree,
            // Required Recipes rows are not expand/collapse toggles), and
            // is naturally low-accidental-click-risk for a click that
            // steals focus into the default browser.
            //
            // Fix-pass (right-click-as-camera-drag): mirrors
            // TreeSectionController's identical fix - GW2's own right-drag
            // is the camera-rotate gesture, so firing on button-DOWN alone
            // opened the browser and yanked focus out of a fullscreen game
            // the instant a drag begun over this row went down, with no
            // way to abort. A bare switch to RightMouseButtonReleased is
            // not sufficient either: Blish routes the release event to
            // whichever row is under the cursor at release time, so a drag
            // that started on a DIFFERENT row would open THIS row's page.
            // Pairing press+release on this SAME rowPanel closes that:
            // press arms a per-row flag, and only this row's own Released
            // handler (which only fires when the release also lands on
            // this row) can consume it; MouseLeft disarms the flag as soon
            // as the cursor leaves this row after a press, so a stale arm
            // from an earlier aborted drag can't be replayed by an
            // unrelated release later landing back on this row.
            if (!string.IsNullOrEmpty(row.WikiUrl))
            {
                string wikiUrl = row.WikiUrl;
                bool wikiLinkArmed = false;
                rowPanel.RightMouseButtonPressed += (_, __) => wikiLinkArmed = true;
                rowPanel.MouseLeft += (_, __) => wikiLinkArmed = false;
                rowPanel.RightMouseButtonReleased += (_, __) =>
                {
                    if (wikiLinkArmed)
                    {
                        wikiLinkArmed = false;
                        WikiLinkLauncher.Open(wikiUrl);
                    }
                };
                rowPanel.BasicTooltipText = "Right-click: Open wiki page";
            }

            IconControls.CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 8, hasSublabel ? 1 : 0);

            var font = GameService.Content.DefaultFont14;
            int nameY = hasSublabel ? 4 : 8;
            new Label()
            {
                Text = row.Label ?? "",
                Font = font,
                TextColor = RarityColors.GetRarityNameColor(row.Rarity),
                ShowShadow = true,
                ShadowColor = Color.Black * 0.8f,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(50, nameY),
                Parent = rowPanel
            };

            if (hasSublabel)
            {
                new Label()
                {
                    Text = row.Sublabel,
                    Font = GameService.Content.DefaultFont12,
                    TextColor = new Color(170, 170, 170),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(50, 22),
                    Parent = rowPanel
                };
            }

            Label statusLabel = null;
            if (!string.IsNullOrEmpty(row.StatusTag))
            {
                Color statusColor = Color.White;
                if (row.StatusTag == "Missing!")
                {
                    statusColor = new Color(255, 100, 100);
                }
                else if (row.StatusTag == "Auto-learned")
                {
                    statusColor = new Color(150, 200, 150);
                }
                statusLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, row.StatusTag, font, statusColor, panelWidth - 8, hasSublabel ? 10 : 8);
            }

            // M36b: bottomClearance depends on which rowHeight this branch
            // used. hasSublabel (44px, RecipeRowHeightWithSublabel) is
            // VULNERABLE to the Container.Paint round-trip defect (see
            // LabelHelpers.CreateRowDivider's doc comment) - icon frame bottom (1 + 34 =
            // 35) leaves ample headroom below rowHeight-3 (41). The
            // no-sublabel branch (36px, RecipeRowHeightNoSublabel) is
            // immune and flush-fit with zero slack; giving it
            // clearance it doesn't need would reintroduce that overlap.
            RowRelayoutHelpers.FinishRow(rowPanel, panelWidth, rowHeight, isLast, hasSublabel ? 1 : 0, _sink, w =>
            {
                if (statusLabel != null)
                {
                    statusLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(w - 8, statusLabel.Width), hasSublabel ? 10 : 8);
                }
            });
        }
    }
}
