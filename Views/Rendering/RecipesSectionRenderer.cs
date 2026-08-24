using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    // The Required Recipes row list, with both row heights
    // (RecipeRowHeightWithSublabel 44px, RecipeRowHeightNoSublabel 36px)
    // and the Auto-learned/Learned/Missing! status tags - same row
    // geometry, PlanContentHeightMath/PlanRelayoutMath calls, and
    // LabelHelpers.CreateRowDivider usage (divider
    // math, its per-branch bottomClearance selection, and the 1px
    // scissor clearance untouched). The only edit inside the moved row body
    // is _relayoutActions.Add -> the injected ISectionRelayoutSink.AddRelayout
    // (a semantics-preserving pass-through - see ISectionRelayoutSink's doc
    // comment).
    //
    // Render() calls CTableHeaderRenderer (the shared "Recipe"/"Status"
    // column header, also used by Required Disciplines) directly, exactly
    // as DisciplinesSectionRenderer does - see that class's doc comment.
    //
    // CreateRecipeRow's
    // divider+relayout tail goes through RowRelayoutHelpers.FinishRow -
    // the shared "row panel resize + extra reposition + divider resize"
    // shape identical across all five extracted renderers' row
    // builders (see that class's doc comment). This row's name label is
    // NOT run through IconNameRowHelpers: it has
    // no width cap or ellipsis at all (row.Label renders in full,
    // regardless of length), an optional sublabel line BELOW the name
    // rather than a same-line secondary label, and an icon y that varies
    // with hasSublabel - a genuinely different shape from the two
    // ellipsized-name rows IconNameRowHelpers actually covers; forcing it
    // through that helper would mean either inventing ellipsis this row
    // never had or dropping its sublabel line, both real behavior changes,
    // so it stays hand-rolled - see IconNameRowHelpers' own doc comment.
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

        // Left x of the name column (past the row's 34px framed icon at
        // x=8), shared by the header and every row.
        private const int NameX = 50;
        private const string StatusHeaderText = "Status";

        /// <summary>
        /// Moved verbatim from CraftingPlanView.CreateRecipesBody, plus the
        /// CreateCTableHeaderRow call this renderer now owns directly (see
        /// the class doc comment above). The Status column is pinned to the
        /// panel edge and this row's name has no ellipsis at all (see the
        /// class doc comment), so this section measures nothing per render.
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            // No pre-scan: the Status column right-aligns onto the pinned
            // panel edge and the recipe name is uncapped (its ellipsis
            // budget arrives with the Discipline column), so nothing in
            // this section is derived from a measured column width.
            CTableHeaderRenderer.CreateCTableHeaderRow(
                contentFlow, panelWidth, "Recipe", NameX, StatusHeaderText, _sink);
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateRecipeRow(
                    section.Rows[i], contentFlow, panelWidth, i == section.Rows.Count - 1);
            }
        }

        private static int MeasureWidth(BitmapFont font, string text)
        {
            return (int)Math.Ceiling(font.MeasureString(text ?? "").Width);
        }

        // The no-sublabel branch's rowHeight (32)
        // left the 34px CreateRarityFramedIcon default frame at y=1
        // overflowing rowHeight by 3px even BEFORE the divider-width
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
        private void CreateRecipeRow(
            PlanRowViewModel row, FlowPanel parent, int panelWidth,
            bool isLast)
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

            var font = UiFonts.Body;
            int nameY = hasSublabel ? 4 : 8;
            LabelHelpers.WithDescenderClearance(
                new Label()
                {
                    Text = row.Label ?? "",
                    Font = font,
                    TextColor = RarityColors.GetRarityNameColor(row.Rarity),
                    ShowShadow = true,
                    ShadowColor = Color.Black * 0.8f,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(NameX, nameY),
                    Parent = rowPanel
                });

            if (hasSublabel)
            {
                LabelHelpers.WithDescenderClearance(
                    new Label()
                    {
                        Text = row.Sublabel,
                        Font = UiFonts.Caption,
                        TextColor = new Color(170, 170, 170),
                        AutoSizeWidth = true,
                        AutoSizeHeight = true,
                        Location = new Point(NameX, 24),
                        Parent = rowPanel
                    });
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
                statusLabel = LabelHelpers.CreateRightAlignedLabel(
                    rowPanel, row.StatusTag, font, statusColor,
                    PlanRelayoutMath.PinnedRightEdge(panelWidth), hasSublabel ? 10 : 8);
            }

            // M36b: bottomClearance depends on which rowHeight this branch
            // used. hasSublabel is now 48px
            // (RecipeRowHeightWithSublabel, raised from the 44px M36b
            // simulated as VULNERABLE to the Container.Paint round-trip
            // defect - see LabelHelpers.CreateRowDivider's doc comment).
            // 48 is a height that simulation never covered, so the 1px is
            // carried forward from 44 rather than proven for 48; it stays
            // because it costs nothing here - the divider top (rowHeight -
            // 3 = 45) clears both the icon frame bottom (1 + 34 = 35) and
            // the sublabel's lowest ink (y=43). The no-sublabel branch
            // (36px, RecipeRowHeightNoSublabel) is on the proven-immune
            // list and flush-fit with zero slack; giving it clearance it
            // doesn't need would reintroduce that overlap.
            RowRelayoutHelpers.FinishRow(
                rowPanel, panelWidth, rowHeight, isLast, hasSublabel ? 1 : 0, _sink,
                w =>
                {
                    if (statusLabel != null)
                    {
                        statusLabel.Location = new Point(
                            PlanRelayoutMath.RightAlignedX(
                                PlanRelayoutMath.PinnedRightEdge(w), statusLabel.Width),
                            hasSublabel ? 10 : 8);
                    }
                });
        }
    }
}
