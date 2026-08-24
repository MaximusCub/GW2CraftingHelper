using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView's "7. Section
    // builders (continued)" region - the Crafting Steps row list (including
    // its TimegatedNotice informational rows) and the step-number
    // rendering. Behavior is unchanged: same row geometry, same
    // PlanContentHeightMath/PlanRelayoutMath calls, same
    // LabelHelpers.CreateRowDivider usage (divider math
    // and its 1px scissor clearance untouched). The only edits inside
    // the moved bodies are _relayoutActions.Add -> the injected
    // ISectionRelayoutSink.AddRelayout (a semantics-preserving pass-through -
    // see ISectionRelayoutSink's doc comment) and CreateTextRow(...) ->
    // TextRowRenderer.CreateTextRow(..., _sink) (see that class's doc
    // comment for why it is its own shared file rather than part of this
    // one - it has two other call sites still living in CraftingPlanView).
    //
    // CreateCraftStepRow's
    // divider+relayout tail goes through RowRelayoutHelpers.FinishRow -
    // the shared "row panel resize + extra reposition + divider resize"
    // shape identical across all five extracted renderers' row
    // builders (see that class's doc comment). This row's name/qty labels
    // are NOT run through IconNameRowHelpers: they
    // are built via cumulative cursor-x concatenation ("Craft " + "{n}x " +
    // name) with no width cap or ellipsis at all, a genuinely different
    // shape from the ellipsized-name-at-a-fixed-column rows - see
    // IconNameRowHelpers' own doc comment.
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
        /// Moved verbatim from CraftingPlanView.CreateCraftingStepsBody,
        /// then given the one-pass pre-scan the other plan tables carry
        /// (audit batch H): the widest sublabel - this table's whole
        /// right-hand block - and the widest UNTRUNCATED extent of the
        /// "Craft Nx Name" run, so the sublabel column can be pulled in
        /// beside that run rather than pinned to the panel edge. The run is
        /// built by cumulative cursor-x concatenation and never ellipsized
        /// (see the class doc comment), so the scan reproduces exactly that
        /// concatenation; pulling the column in past it is what stops a long
        /// name running under the sublabel.
        /// <para>
        /// TimegatedNotice rows are plain full-width text rows with no
        /// columns of their own, so they take no part in the scan. A section
        /// where no row carries a sublabel has no right-hand block to pull
        /// in and stays pinned exactly as before.
        /// </para>
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            // No pre-scan: the sublabel right-aligns onto the pinned panel
            // edge and the step's own "Craft Nx Name" run is uncapped, so
            // nothing here is derived from a measured column width.
            //
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

        private static string QtyPrefix(int quantity)
        {
            return $"{quantity}x ";
        }

        // Moved verbatim from CraftingPlanView.CreateCraftStepRow. Only
        // change: _relayoutActions.Add(...) -> _sink.AddRelayout(...).
        // Left x of the row's text run, and its fixed leading word - both
        // shared with Render()'s pre-scan so the measured extent is exactly
        // what the row lays out.
        private const int TextX = 94; // iconX(52) + frame(34) + gap(8)
        private const string CraftPrefix = "Craft ";

        private void CreateCraftStepRow(
            PlanRowViewModel row, int stepNumber, FlowPanel parent, int panelWidth,
            bool isLast)
        {
            const int rowHeight = PlanContentHeightMath.CraftStepRowHeight;
            const int badgeSize = 36;
            const int badgeX = 8;
            const int badgeY = 4;
            const int iconX = 52;

            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            new Panel()
            {
                Size = new Point(badgeSize, badgeSize),
                Location = new Point(badgeX, badgeY),
                BackgroundColor = Color.White * 0.08f,
                Parent = rowPanel
            };
            string numberText = stepNumber.ToString();
            var numberFont = UiFonts.Title;
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

            var textFont = UiFonts.Body;
            var greyColor = new Color(170, 170, 170);
            int x = TextX;

            // "Craft ", "12x " and the item name are one sentence on one
            // baseline: every label on it gets the same box treatment, so
            // the clearance can never make the three disagree.
            var craftLabel = LabelHelpers.WithDescenderClearance(
                new Label()
                {
                    Text = CraftPrefix, Font = textFont, TextColor = greyColor,
                    AutoSizeWidth = true, AutoSizeHeight = true,
                    Location = new Point(x, 13), Parent = rowPanel
                });
            x += craftLabel.Width;

            var qtyLabel = LabelHelpers.WithDescenderClearance(
                new Label()
                {
                    Text = QtyPrefix(row.Quantity), Font = textFont, TextColor = greyColor,
                    AutoSizeWidth = true, AutoSizeHeight = true,
                    Location = new Point(x, 13), Parent = rowPanel
                });
            x += qtyLabel.Width;

            LabelHelpers.WithDescenderClearance(
                new Label()
                {
                    Text = row.Label ?? "", Font = textFont, TextColor = RarityColors.GetRarityNameColor(row.Rarity),
                    ShowShadow = true, ShadowColor = Color.Black * 0.8f,
                    AutoSizeWidth = true, AutoSizeHeight = true,
                    Location = new Point(x, 13), Parent = rowPanel
                });

            Label sublabelLabel = null;
            if (!string.IsNullOrEmpty(row.Sublabel))
            {
                sublabelLabel = LabelHelpers.CreateRightAlignedLabel(
                    rowPanel, row.Sublabel, UiFonts.Caption,
                    new Color(153, 153, 153),
                    PlanRelayoutMath.PinnedRightEdge(panelWidth), 16);
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
            RowRelayoutHelpers.FinishRow(
                rowPanel, panelWidth, rowHeight, isLast, 1, _sink,
                w =>
                {
                    if (sublabelLabel != null)
                    {
                        sublabelLabel.Location = new Point(
                            PlanRelayoutMath.RightAlignedX(
                                PlanRelayoutMath.PinnedRightEdge(w), sublabelLabel.Width),
                            16);
                    }
                });
        }
    }
}
