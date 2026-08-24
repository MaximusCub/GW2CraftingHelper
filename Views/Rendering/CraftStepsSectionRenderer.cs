using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
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
    // are NOT run through IconNameRowHelpers: this row has no icon column
    // of the shape that helper builds, and its name sits at a cursor x
    // accumulated from the two fixed words before it ("Craft " + "{n}x ")
    // rather than at a fixed column - see IconNameRowHelpers' own doc
    // comment. Only the ellipsis idiom itself is shared.
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
        /// The sublabel column is pinned to the panel edge and the
        /// "Craft Nx Name" run flexes into whatever is left of the row,
        /// ellipsizing with its full name on a tooltip.
        /// <para>
        /// The one pre-scan is the sublabel BAND - the widest sublabel this
        /// render draws, which is where the step name's budget has to stop.
        /// Budgeting against a row's own (possibly absent) sublabel instead
        /// would let a short-sublabel row's name run under the widest one.
        /// TimegatedNotice rows are plain full-width text rows with no
        /// columns of their own, so they take no part in the scan; a
        /// section where no row carries a sublabel gives its names the
        /// whole row. The column has no header, so unlike every other table
        /// here the band has no header label to floor it.
        /// </para>
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            var sublabelFont = UiFonts.Caption;
            int maxSublabelWidth = 0;
            for (int i = 0; i < section.Rows.Count; i++)
            {
                var row = section.Rows[i];
                if (row.RowType == PlanRowType.TimegatedNotice) continue;
                if (string.IsNullOrEmpty(row.Sublabel)) continue;

                int width = (int)System.Math.Ceiling(sublabelFont.MeasureString(row.Sublabel).Width);
                if (width > maxSublabelWidth) maxSublabelWidth = width;
            }

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
                    CreateCraftStepRow(row, stepNumber++, contentFlow, panelWidth, maxSublabelWidth, isLast);
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

        // Gap the step name's ellipsis budget keeps between itself and the
        // sublabel band, matching the name-to-column gap every other table
        // in the plan reserves.
        private const int NameToSublabelGap = 12;

        private void CreateCraftStepRow(
            PlanRowViewModel row, int stepNumber, FlowPanel parent, int panelWidth,
            int maxSublabelWidth, bool isLast)
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
            // Digits only, so the space-glyph defect that retired
            // 18-regular elsewhere is not the reason this moved - the badge
            // is chrome, and chrome above body is bold. 20-bold's cap fills
            // the badge square better than 18-regular's did.
            string numberText = stepNumber.ToString();
            var numberFont = UiFonts.SmallHeadingBold;
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

            // The name is the row's flexing run: "Craft " and "Nx " are
            // fixed words at a font-only cursor x, so the whole of the
            // row's slack lands here. nameX is that cursor - invariant to
            // panelWidth, which is why the settle closure recaptures it
            // rather than re-measuring the two labels before it.
            int nameX = x;
            string fullName = row.Label ?? "";
            int nameMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                PlanRelayoutMath.PinnedRightEdge(panelWidth), maxSublabelWidth, NameToSublabelGap, nameX);
            var nameLabel = LabelHelpers.WithDescenderClearance(
                new Label()
                {
                    Text = LabelHelpers.EllipsizeToWidth(textFont, fullName, nameMaxWidth),
                    Font = textFont, TextColor = RarityColors.GetRarityNameColor(row.Rarity),
                    ShowShadow = true, ShadowColor = Color.Black * 0.8f,
                    AutoSizeWidth = true, AutoSizeHeight = true,
                    Location = new Point(nameX, 13), Parent = rowPanel
                });
            StampNameTooltip(rowPanel, nameLabel, fullName);

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
            _sink.AddReellipsis(w =>
            {
                string newDisplayName = LabelHelpers.EllipsizeToWidth(
                    textFont, fullName,
                    PlanRelayoutMath.NameMaxWidthBeforeColumn(
                        PlanRelayoutMath.PinnedRightEdge(w), maxSublabelWidth, NameToSublabelGap, nameX));
                if (nameLabel.Text != newDisplayName)
                {
                    nameLabel.Text = newDisplayName;
                    StampNameTooltip(rowPanel, nameLabel, fullName);
                }
            });
        }

        /// <summary>
        /// The step's full item name on the label AND the row panel when
        /// the name is truncated, and a deliberate clear of both when a
        /// widening drag untruncates it (see TooltipFacility.ApplyPlain).
        /// The label is stamped because it captures the hover before the
        /// row panel beneath it.
        /// </summary>
        private static void StampNameTooltip(Panel rowPanel, Label nameLabel, string fullName)
        {
            string tooltip = nameLabel.Text != fullName ? fullName : null;
            TooltipFacility.ApplyPlain(rowPanel, tooltip);
            TooltipFacility.ApplyPlain(nameLabel, tooltip);
        }
    }
}
