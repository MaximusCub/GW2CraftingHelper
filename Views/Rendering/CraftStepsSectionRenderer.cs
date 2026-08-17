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
    // builders (continued)" region - the Crafting Steps row list (including
    // its TimegatedNotice informational rows - the M34 warn-only vendor-cap
    // notices with the M37 Seasonal wording from PR #81) and the step-number
    // rendering. Behavior is unchanged: same row geometry, same
    // PlanContentHeightMath/PlanRelayoutMath calls, same
    // LabelHelpers.CreateRowDivider usage (DO-NOT-TOUCH #6 - divider math
    // and its M36b 1px scissor clearance untouched). The only edits inside
    // the moved bodies are _relayoutActions.Add -> the injected
    // ISectionRelayoutSink.AddRelayout (a semantics-preserving pass-through -
    // see ISectionRelayoutSink's doc comment) and CreateTextRow(...) ->
    // TextRowRenderer.CreateTextRow(..., _sink) (see that class's doc
    // comment for why it moved to its own shared file rather than into this
    // one - it has two other call sites still living in CraftingPlanView).
    //
    // Per the WP-23 pilot's FORWARD NOTE, CreateCraftStepRow itself depends
    // only on the already-extracted IconControls/RarityColors/LabelHelpers/
    // PlanRelayoutMath statics the pilot also used - confirmed true. The one
    // dependency the pilot's per-row check did not cover was body-level:
    // CreateCraftingStepsBody's TimegatedNotice branch called
    // CraftingPlanView's private CreateTextRow, resolved via TextRowRenderer
    // as described above.
    //
    // CreateCraftStepRow's
    // divider+relayout tail now goes through RowRelayoutHelpers.FinishRow -
    // the shared "row panel resize + extra reposition + divider resize"
    // shape confirmed identical across all five extracted renderers' row
    // builders (see that class's doc comment). This row's name/qty labels
    // are NOT run through IconNameRowHelpers (the other WP-24 helper): they
    // are built via cumulative cursor-x concatenation ("Craft " + "{n}x " +
    // name) with no width cap or ellipsis at all, a genuinely different
    // shape from the ellipsized-name-at-a-fixed-column rows - see
    // IconNameRowHelpers' own doc comment. Geometry unchanged - see the
    // WP-24 constant-by-constant table in the PR/commit body.
    internal sealed class CraftStepsSectionRenderer
    {
        private readonly ISectionRelayoutSink _sink;

        internal CraftStepsSectionRenderer(ISectionRelayoutSink sink)
        {
            // Mirrors the constructor-null-guard convention already used
            // for injected dependencies elsewhere in Views/ (ViewAdapter's
            // buildAction, SettingsTabContent's settings, FrameTicker's
            // step) and by every other section renderer on this pattern -
            // the sole production call site always passes `this`
            // (CraftingPlanView), but a later section renderer built on
            // this same pattern should fail loud, not with a deferred NRE
            // inside CreateCraftStepRow's first AddRelayout call.
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>
        /// Moved verbatim from CraftingPlanView.CreateCraftingStepsBody.
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            // A TimegatedNotice row (vendor-cap informational
            // line) is a plain text row, not a numbered craft step - render
            // it via the same generic TextRowRenderer pattern every other
            // section's fallback rows use, and don't consume a step number
            // for it (stepNumber only advances for real CraftStep rows).
            int stepNumber = 1;
            for (int i = 0; i < section.Rows.Count; i++)
            {
                var row = section.Rows[i];
                bool isLast = i == section.Rows.Count - 1;
                if (row.RowType == PlanRowType.TimegatedNotice)
                {
                    TextRowRenderer.CreateTextRow(row.Label, contentFlow, panelWidth, _sink);
                }
                else
                {
                    CreateCraftStepRow(row, stepNumber++, contentFlow, panelWidth, isLast);
                }
            }
        }

        // Moved verbatim from CraftingPlanView.CreateCraftStepRow. Only
        // change: _relayoutActions.Add(...) -> _sink.AddRelayout(...).
        private void CreateCraftStepRow(
            PlanRowViewModel row, int stepNumber, FlowPanel parent, int panelWidth, bool isLast)
        {
            const int rowHeight = PlanContentHeightMath.CraftStepRowHeight;
            const int badgeSize = 36;
            const int badgeX = 8;
            const int badgeY = 4;
            const int iconX = 52;
            const int textX = 94; // iconX(52) + frame(34) + gap(8)

            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            new Panel()
            {
                Size = new Point(badgeSize, badgeSize),
                Location = new Point(badgeX, badgeY),
                BackgroundColor = Color.White * 0.08f,
                Parent = rowPanel
            };
            string numberText = stepNumber.ToString();
            var numberFont = GameService.Content.DefaultFont18;
            var numberMeasure = numberFont.MeasureString(numberText);
            int numberWidth = (int)System.Math.Ceiling(numberMeasure.Width);
            int numberHeight = (int)System.Math.Ceiling(numberMeasure.Height);
            new Label()
            {
                Text = numberText,
                Font = numberFont,
                TextColor = Color.White,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(badgeX + (badgeSize - numberWidth) / 2, badgeY + (badgeSize - numberHeight) / 2),
                Parent = rowPanel
            };

            IconControls.CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, iconX, 5);

            var textFont = GameService.Content.DefaultFont16;
            var greyColor = new Color(170, 170, 170);
            int x = textX;

            var craftLabel = new Label()
            {
                Text = "Craft ", Font = textFont, TextColor = greyColor,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(x, 13), Parent = rowPanel
            };
            x += craftLabel.Width;

            var qtyLabel = new Label()
            {
                Text = $"{row.Quantity}x ", Font = textFont, TextColor = greyColor,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(x, 13), Parent = rowPanel
            };
            x += qtyLabel.Width;

            new Label()
            {
                Text = row.Label ?? "", Font = textFont, TextColor = RarityColors.GetRarityNameColor(row.Rarity),
                ShowShadow = true, ShadowColor = Color.Black * 0.8f,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(x, 13), Parent = rowPanel
            };

            Label sublabelLabel = null;
            if (!string.IsNullOrEmpty(row.Sublabel))
            {
                sublabelLabel = LabelHelpers.CreateRightAlignedLabel(
                    rowPanel, row.Sublabel, GameService.Content.DefaultFont12,
                    new Color(153, 153, 153), panelWidth - 8, 16);
            }

            // M36b: bottomClearance 1 - CraftStepRowHeight (44) is
            // VULNERABLE to the Container.Paint round-trip defect (see
            // LabelHelpers.CreateRowDivider's doc comment): its icon frame bottom
            // (iconY 5 + 34 = 39) sits 2px clear of the new divider top
            // (rowHeight-3 = 41), so the 1px shift is free of
            // icon-clearance side effects.
            //
            // Name/qty labels sit at a fixed x (font-only, not
            // width-dependent - textX never depended on panelWidth); only
            // the row width, its divider, and the right-aligned sublabel
            // need to move.
            RowRelayoutHelpers.FinishRow(rowPanel, panelWidth, rowHeight, isLast, 1, _sink, w =>
            {
                if (sublabelLabel != null)
                {
                    sublabelLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(w - 8, sublabelLabel.Width), 16);
                }
            });
        }
    }
}
