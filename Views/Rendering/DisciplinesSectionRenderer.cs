using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    // The Required Disciplines row list, plus the column header row this
    // renderer owns directly.
    //
    // The "Discipline"/"Level" column header call (ColumnHeaderRowRenderer,
    // shared with Required Recipes) lives in this class's Render() below;
    // CraftingPlanView.CreateCollapsibleSection no longer references the
    // column header row for either section.
    //
    // CreateDisciplineRow's
    // divider+relayout tail goes through RowRelayoutHelpers.FinishRow -
    // the shared "row panel resize + extra reposition + divider resize"
    // shape identical across all five extracted renderers' row
    // builders (see that class's doc comment). This row has no icon and no
    // name column at all (just plain Body labels - name, level and the
    // character run), so it does
    // not match IconNameRowHelpers and stays
    // hand-rolled - see IconNameRowHelpers' own doc comment.
    internal sealed class DisciplinesSectionRenderer
    {
        private readonly ISectionRelayoutSink _sink;

        internal DisciplinesSectionRenderer(ISectionRelayoutSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>
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
        ///
        /// The Level column is pinned to the panel edge
        /// (PlanRelayoutMath.PinnedRightEdge) and the Characters column is
        /// the one that flexes into whatever the Discipline column and the
        /// Level band leave it.
        /// </summary>
        internal void Render(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            var font = UiFonts.Body;

            // Body, not Caption: character names were the one text in this
            // row that was both smaller AND greyer than its neighbours, and
            // a name a user reads letter by letter is the worst thing to
            // shrink. The grey stays - one channel of de-emphasis, not two.
            var charFont = UiFonts.Body;
            // Header labels are part of their own columns' widths: at the
            // ColumnHeader tier a header is routinely wider than the data
            // under it, and a column narrower than its own header lets the
            // neighbouring column run under that header.
            int maxNameWidth = MeasureWidth(TableHeaderStyle.Font, DisciplineHeaderText);
            int maxCharWidth = 0;
            int maxLevelWidth = 0;
            bool anyCharacterText = false;
            for (int i = 0; i < section.Rows.Count; i++)
            {
                int nameWidth = MeasureWidth(font, section.Rows[i].Label);
                if (nameWidth > maxNameWidth)
                {
                    maxNameWidth = nameWidth;
                }

                int levelWidth = MeasureWidth(font, section.Rows[i].Sublabel);
                if (levelWidth > maxLevelWidth)
                {
                    maxLevelWidth = levelWidth;
                }

                if (!string.IsNullOrEmpty(section.Rows[i].CharacterAvailabilityText))
                {
                    if (!anyCharacterText)
                    {
                        // The column's own header is part of its extent:
                        // it starts at the same charX and can be wider than
                        // a short availability string.
                        anyCharacterText = true;
                        maxCharWidth = MeasureWidth(TableHeaderStyle.Font, CharactersHeaderText);
                    }

                    int charWidth = MeasureWidth(charFont, section.Rows[i].CharacterAvailabilityText);
                    if (charWidth > maxCharWidth)
                    {
                        maxCharWidth = charWidth;
                    }
                }
            }

            int charX = 8 + maxNameWidth + CharGap;

            // The Level band is reserved even when no row carries a level:
            // its header still right-aligns onto the pinned edge, and the
            // Characters column's ellipsis budget has to stop short of it.
            int levelHeaderWidth = MeasureWidth(TableHeaderStyle.Font, LevelHeaderText);
            int levelColumnWidth = maxLevelWidth > levelHeaderWidth ? maxLevelWidth : levelHeaderWidth;

            if (anyCharacterText)
            {
                ColumnHeaderRowRenderer.CreateColumnHeaderRow(
                    contentFlow, panelWidth, DisciplineHeaderText, 8, LevelHeaderText, _sink,
                    CharactersHeaderText, charX);
            }
            else
            {
                ColumnHeaderRowRenderer.CreateColumnHeaderRow(
                    contentFlow, panelWidth, DisciplineHeaderText, 8, LevelHeaderText, _sink);
            }

            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateDisciplineRow(
                    section.Rows[i], contentFlow, panelWidth, i == section.Rows.Count - 1,
                    charX, levelColumnWidth);
            }
        }

        // Shared between Render() (header column X) and
        // CreateDisciplineRow so both always agree on the same gap.
        private const int CharGap = 12;
        private const string LevelHeaderText = "Level";
        private const string CharactersHeaderText = "Characters";
        private const string DisciplineHeaderText = "Discipline";

        private static int MeasureWidth(BitmapFont font, string text)
        {
            return (int)Math.Ceiling(font.MeasureString(text ?? "").Width);
        }

        // The character-availability label sits between the discipline
        // name and the Level column. charX is passed in by Render() as
        // one fixed column X for the whole section (8 + the widest
        // discipline name present + CharGap) - a per-row X could never
        // line up with a single header label - which Render()'s own doc
        // comment covers in
        // full. Guaranteed >= 8 + nameLabel.Width + CharGap for every row
        // in this call (charX's max-of-all-rows construction), so charLabel
        // can never overlap nameLabel here.
        private void CreateDisciplineRow(
            PlanRowViewModel row, FlowPanel parent, int panelWidth, bool isLast,
            int charX, int levelColumnWidth)
        {
            const int rowHeight = PlanContentHeightMath.DisciplineRowHeight;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };
            var font = UiFonts.Body;

            LabelHelpers.WithDescenderClearance(
                new Label()
                {
                    Text = row.Label ?? "", Font = font,
                    AutoSizeWidth = true, AutoSizeHeight = true,
                    Location = new Point(8, 7), Parent = rowPanel,
                });
            int levelRightEdge = PlanRelayoutMath.PinnedRightEdge(panelWidth);
            var levelLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, row.Sublabel, font, Color.White, levelRightEdge, 7);

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
            var charFont = UiFonts.Body;
            var charColor = new Color(170, 170, 170);
            string fullCharText = row.CharacterAvailabilityText;
            Label charLabel = null;
            if (!string.IsNullOrEmpty(fullCharText))
            {
                // levelColumnWidth, not this row's own level text: Level
                // is a reserved band right-aligned on the pinned edge, so
                // the character run's budget stops at the band's left edge.
                int charMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                    levelRightEdge, levelColumnWidth, CharGap, charX);
                string charDisplayText = LabelHelpers.EllipsizeToWidth(charFont, fullCharText, charMaxWidth);
                // The reported site: "Anna (500), Bobby (400/450)" - the one
                // label in this row carrying character names, which are the
                // only text here a user picks the letters of.
                charLabel = LabelHelpers.WithDescenderClearance(
                    new Label()
                    {
                        Text = charDisplayText, Font = charFont, TextColor = charColor,
                        AutoSizeWidth = true, AutoSizeHeight = true,
                        Location = new Point(charX, 9), Parent = rowPanel,
                    });
                if (charLabel.Text != fullCharText)
                {
                    // Both controls: the label captures the hover before
                    // the row panel under it is ever reached, so a tooltip
                    // on the panel alone only fires on the blank strip
                    // beside the very text it exists to expand.
                    StampCharacterTooltip(rowPanel, charLabel, fullCharText);
                }
            }

            // M36b: bottomClearance 1 - DisciplineRowHeight was 32, which
            // that investigation's simulation found VULNERABLE to the
            // Container.Paint round-trip defect (see
            // LabelHelpers.CreateRowDivider's doc comment) at the same
            // ~10.2% vanish rate as the 44px rows, then omitted from its
            // fix list by oversight. The +2pt body bump raised the row to
            // 36, which the same simulation proves immune, so the clearance
            // is now belt-and-braces rather than the fix. It stays because
            // it costs nothing: no icon in this row (a Body name and level
            // at y=7, a Body character line at y=9, lowest ink y=30), so
            // the divider top (rowHeight - 3 = 33) sits well clear either
            // way.
            RowRelayoutHelpers.FinishRow(
                rowPanel, panelWidth, rowHeight, isLast, 1, _sink,
                w =>
                {
                    levelLabel.Location = new Point(
                        PlanRelayoutMath.RightAlignedX(
                            PlanRelayoutMath.PinnedRightEdge(w), levelLabel.Width),
                        7);
                });

            if (charLabel != null)
            {
                _sink.AddReellipsis(w =>
                {
                    int newMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                        PlanRelayoutMath.PinnedRightEdge(w), levelColumnWidth, CharGap, charX);
                    string newDisplayText = LabelHelpers.EllipsizeToWidth(charFont, fullCharText, newMaxWidth);
                    if (charLabel.Text != newDisplayText)
                    {
                        charLabel.Text = newDisplayText;
                        StampCharacterTooltip(
                            rowPanel, charLabel, newDisplayText != fullCharText ? fullCharText : null);
                    }
                });
            }
        }

        /// <summary>
        /// The truncated character run's full text, on the label AND the
        /// row panel under it. Null is a deliberate clear (see
        /// TooltipFacility.ApplyPlain), which is what an untruncated run
        /// after a widening drag needs.
        /// </summary>
        private static void StampCharacterTooltip(Panel rowPanel, Label charLabel, string fullCharText)
        {
            TooltipFacility.ApplyPlain(rowPanel, fullCharText);
            TooltipFacility.ApplyPlain(charLabel, fullCharText);
        }
    }
}
