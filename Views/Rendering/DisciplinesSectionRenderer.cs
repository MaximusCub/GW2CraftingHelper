using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of
    // CraftingPlanView's "7. Section builders (continued)" region - the
    // Required Disciplines row list only. Behavior is unchanged: same row
    // geometry, same PlanContentHeightMath/PlanRelayoutMath calls, same
    // LabelHelpers.CreateRowDivider usage (DO-NOT-TOUCH #6 - divider math
    // and its M36b 1px scissor clearance untouched). The only edit inside
    // the moved bodies is _relayoutActions.Add -> the injected
    // ISectionRelayoutSink.AddRelayout, which is a semantics-preserving
    // pass-through (see ISectionRelayoutSink's doc comment).
    //
    // The "Discipline"/"Level" column header (CreateCTableHeaderRow) stayed
    // in CraftingPlanView for this pilot - it was shared chrome with the
    // not-yet-extracted Required Recipes section (CreateRecipesBody called
    // the same method for its "Recipe"/"Status" header), and moving it then
    // would have either widened this pilot's scope to Recipes too or left
    // Recipes calling into a class named for Disciplines. M38 WP-23c
    // extracted Required Recipes too, so both callers are now extracted
    // section renderers - the stay-in-the-view rationale no longer applies.
    // The header call moved into this class's Render() below (see
    // Views/Rendering/CTableHeaderRenderer's doc comment for the full
    // resolution); CraftingPlanView.CreateCollapsibleSection no longer
    // references the c-table header for either section.
    //
    // CreateDisciplineRow's
    // divider+relayout tail now goes through RowRelayoutHelpers.FinishRow -
    // the shared "row panel resize + extra reposition + divider resize"
    // shape confirmed identical across all five extracted renderers' row
    // builders (see that class's doc comment). This row has no icon and no
    // name column at all (just two plain DefaultFont14 labels), so it does
    // not match IconNameRowHelpers (the other WP-24 helper) and stays
    // hand-rolled there - see IconNameRowHelpers' own doc comment. Geometry
    // unchanged - see the WP-24 constant-by-constant table in the PR/commit
    // body.
    internal sealed class DisciplinesSectionRenderer
    {
        private readonly ISectionRelayoutSink _sink;

        internal DisciplinesSectionRenderer(ISectionRelayoutSink sink)
        {
            // Mirrors the constructor-null-guard convention already used
            // for injected dependencies elsewhere in Views/ (ViewAdapter's
            // buildAction, SettingsTabContent's settings, FrameTicker's
            // step) - the sole production call site always passes `this`
            // (CraftingPlanView), but a later section renderer built on
            // this same pattern should fail loud, not with a deferred NRE
            // inside CreateDisciplineRow's first AddRelayout call.
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>
        /// Moved verbatim from CraftingPlanView.CreateDisciplinesBody's row
        /// loop, plus the CreateCTableHeaderRow call this renderer now owns
        /// directly.
        ///
        /// The per-character-availability column gets a real header: a
        /// per-row X (varying with each discipline name's width) could
        /// never line up with a single header position, fixed here by
        /// computing ONE column X for the whole section - 8 + the widest
        /// discipline name actually in this section's rows + charGap - and
        /// passing it into CreateDisciplineRow instead of letting each row
        /// measure its own nameLabel. Every row's charX is now <= that
        /// fixed X by construction (it IS the max), so this can never make
        /// a charLabel overlap its own nameLabel; the "Characters" header
        /// is only added when at least one row actually has availability
        /// text to show under it (never both null and non-null in the same
        /// section in practice - see BuildCharacterAvailabilityText's doc
        /// comment - but this checks all rows rather than assuming that).
        /// No change to rowHeight, PlanContentHeightMath, or
        /// PlanRelayoutMath - NameMaxWidthBeforeColumn's existing 20px
        /// floor still clamps the ellipsis width on narrow panels exactly
        /// as it did before.
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            var font = GameService.Content.DefaultFont14;
            int maxNameWidth = 0;
            bool anyCharacterText = false;
            for (int i = 0; i < section.Rows.Count; i++)
            {
                int nameWidth = (int)Math.Ceiling(font.MeasureString(section.Rows[i].Label ?? "").Width);
                if (nameWidth > maxNameWidth)
                {
                    maxNameWidth = nameWidth;
                }

                if (!string.IsNullOrEmpty(section.Rows[i].CharacterAvailabilityText))
                {
                    anyCharacterText = true;
                }
            }

            int charX = 8 + maxNameWidth + CharGap;

            if (anyCharacterText)
            {
                CTableHeaderRenderer.CreateCTableHeaderRow(contentFlow, panelWidth, "Discipline", 8, "Level", _sink, "Characters", charX);
            }
            else
            {
                CTableHeaderRenderer.CreateCTableHeaderRow(contentFlow, panelWidth, "Discipline", 8, "Level", _sink);
            }

            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateDisciplineRow(section.Rows[i], contentFlow, panelWidth, i == section.Rows.Count - 1, charX);
            }
        }

        // Shared between Render() (header column X) and
        // CreateDisciplineRow so both always agree on the same gap.
        private const int CharGap = 12;

        // Moved verbatim from CraftingPlanView.CreateDisciplineRow. Only
        // change: _relayoutActions.Add(...) -> _sink.AddRelayout(...).
        //
        // The character-availability label sits between the discipline
        // name and the Level column. charX is passed in by Render() as
        // one fixed column X for the whole section (8 + the widest
        // discipline name present + CharGap) - a per-row X could never
        // line up with a single header label - which Render()'s own doc
        // comment covers in
        // full. Guaranteed >= 8 + nameLabel.Width + CharGap for every row
        // in this call (charX's max-of-all-rows construction), so charLabel
        // can never overlap nameLabel here.
        private void CreateDisciplineRow(PlanRowViewModel row, FlowPanel parent, int panelWidth, bool isLast, int charX)
        {
            const int rowHeight = PlanContentHeightMath.DisciplineRowHeight;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };
            var font = GameService.Content.DefaultFont14;

            new Label()
            {
                Text = row.Label ?? "", Font = font,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(8, 7), Parent = rowPanel
            };
            var levelLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, row.Sublabel, font, Color.White, panelWidth - 8, 7);

            // "Anna (500), Bob (400/450)" - secondary text sitting
            // between the discipline name and the right-aligned Level
            // column, ellipsized to whatever room is left (same
            // EllipsizeToWidth + tooltip-on-truncate convention as
            // UsedMaterialsSectionRenderer.CreateUsedMaterialRow's name
            // column). charX is fixed at build time (the section's set of
            // discipline names never changes on resize, so the column X
            // Render() derived from them does not either) - only the
            // AVAILABLE width changes as levelLabel's position shifts with
            // panelWidth, so only a re-ellipsis (text truncation), never a
            // reposition, is needed on resize. Entirely skipped when
            // row.CharacterAvailabilityText is null (the snapshot never
            // captured this data - see that field's own doc comment): no
            // label, no tooltip, no claim either way.
            var charFont = GameService.Content.DefaultFont12;
            var charColor = new Color(170, 170, 170);
            string fullCharText = row.CharacterAvailabilityText;
            Label charLabel = null;
            if (!string.IsNullOrEmpty(fullCharText))
            {
                int charMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(panelWidth - 8, levelLabel.Width, CharGap, charX);
                string charDisplayText = LabelHelpers.EllipsizeToWidth(charFont, fullCharText, charMaxWidth);
                charLabel = new Label()
                {
                    Text = charDisplayText, Font = charFont, TextColor = charColor,
                    AutoSizeWidth = true, AutoSizeHeight = true,
                    Location = new Point(charX, 9), Parent = rowPanel
                };
                if (charLabel.Text != fullCharText)
                {
                    rowPanel.BasicTooltipText = fullCharText;
                }
            }

            // M36b: bottomClearance 1 - DisciplineRowHeight (32) is
            // VULNERABLE to the Container.Paint round-trip defect (see
            // LabelHelpers.CreateRowDivider's doc comment), same ~10.2% vanish rate as
            // the 44px rows; the M36b investigation confirmed this via
            // simulation but omitted it from its fix list by oversight.
            // No icon in this row (two DefaultFont14 labels at y=7 only),
            // so there is no icon-clearance side effect to worry about -
            // the new divider top (rowHeight - 3 = 29) sits well clear of
            // the text baseline.
            RowRelayoutHelpers.FinishRow(rowPanel, panelWidth, rowHeight, isLast, 1, _sink, w =>
            {
                levelLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(w - 8, levelLabel.Width), 7);
            });

            if (charLabel != null)
            {
                _sink.AddReellipsis(w =>
                {
                    int newMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(w - 8, levelLabel.Width, CharGap, charX);
                    string newDisplayText = LabelHelpers.EllipsizeToWidth(charFont, fullCharText, newMaxWidth);
                    if (charLabel.Text != newDisplayText)
                    {
                        charLabel.Text = newDisplayText;
                        rowPanel.BasicTooltipText = newDisplayText != fullCharText ? fullCharText : null;
                    }
                });
            }
        }
    }
}
