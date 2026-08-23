using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;

namespace GW2CraftingHelper.Views.Rendering
{
    // Factors the "icon +
    // ellipsized name label, re-ellipsized on drag-settle" shape that
    // UsedMaterialsSectionRenderer.CreateUsedMaterialRow and
    // ShoppingListSectionRenderer.CreateShoppingRow both build byte-for-byte
    // identically: IconControls.CreateRarityFramedIcon at a fixed (x, y),
    // then PlanRelayoutMath.NameMaxWidthBeforeColumn -> LabelHelpers.
    // EllipsizeToWidth -> a rarity-colored, drop-shadowed name Label at
    // (nameX, nameY) - confirmed identical at every one of those call sites
    // (same nameX 50, nameY 9, icon (8, 0), NameMaxWidthBeforeColumn
    // gap 12 - confirmed by constant-by-constant comparison).
    //
    // Deliberately NOT adopted by CraftStepsSectionRenderer.CreateCraftStepRow,
    // DisciplinesSectionRenderer.CreateDisciplineRow, or
    // RecipesSectionRenderer.CreateRecipeRow: none of those three rows call
    // EllipsizeToWidth/NameMaxWidthBeforeColumn at all (CraftStepRow builds
    // its name via cumulative cursor-x label concatenation with no width
    // cap; DisciplineRow has no icon and no name-column at all, just two
    // plain labels; RecipeRow's name label has no width cap either - it
    // relies on the row's sublabel/status-tag layout instead). Forcing any
    // of those three through this helper would either drop a feature
    // (RecipeRow's optional sublabel-below-the-name line) or change pixel
    // geometry outright - per this package's own brief, that is worse than
    // leaving the duplication in place, so they stay hand-rolled.
    //
    // Split into a build-time method (CreateIconAndEllipsizedName) and a
    // separate settle-time method (ReellipsizeName) rather than a single
    // method that also registers the AddReellipsis closure itself: both
    // pre-extraction callers do meaningfully different follow-up work after
    // the label text actually changes (UsedMaterialRow only ever touches
    // its own tooltip; ShoppingRow rebuilds a multi-line tooltip AND
    // repositions a source-tag Panel that sits to the name's right) - that
    // follow-up is genuinely per-row, so it stays in the caller's own
    // AddReellipsis closure, mirroring the existing CoinCurrencyRenderer.
    // RenderValueCellRightAligned/RepositionValueCellRightAligned handle
    // pattern already used elsewhere in this codebase rather than inventing
    // a new callback-based shape.
    internal static class IconNameRowHelpers
    {
        /// <summary>
        /// The subset of a row's own state a later ReellipsizeName call
        /// needs: the live Label (so its .Text can be read/written) and the
        /// two build-time values that don't change on resize (the
        /// untruncated name and the fixed nameX the width cap is measured
        /// against).
        /// </summary>
        internal sealed class IconNameHandle
        {
            internal Label NameLabel;
            internal string FullName;
            internal int NameX;
        }

        /// <summary>
        /// Builds the rarity-framed icon and the ellipsized, rarity-colored,
        /// drop-shadowed name label immediately to its right. rightEdge/
        /// qtyWidth/nameGap are threaded straight into
        /// PlanRelayoutMath.NameMaxWidthBeforeColumn exactly as each
        /// pre-extraction caller computed them inline (untouched - this
        /// helper does not change that arithmetic, only where it is
        /// called from).
        /// </summary>
        internal static IconNameHandle CreateIconAndEllipsizedName(
            Panel rowPanel, string iconUrl, string rarity, int iconX, int iconY,
            string fullName, BitmapFont font, int rightEdge, int qtyWidth, int nameGap, int nameX, int nameY,
            int iconSize = 32, int borderThickness = 1)
        {
            IconControls.CreateRarityFramedIcon(rowPanel, iconUrl, rarity, iconX, iconY, iconSize, borderThickness);

            int nameMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(rightEdge, qtyWidth, nameGap, nameX);
            string displayName = LabelHelpers.EllipsizeToWidth(font, fullName, nameMaxWidth);
            var nameLabel = LabelHelpers.WithDescenderClearance(
                new Label()
                {
                    Text = displayName,
                    Font = font,
                    TextColor = RarityColors.GetRarityNameColor(rarity),
                    ShowShadow = true,
                    ShadowColor = Color.Black * 0.8f,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(nameX, nameY),
                    Parent = rowPanel
                });

            return new IconNameHandle { NameLabel = nameLabel, FullName = fullName, NameX = nameX };
        }

        /// <summary>
        /// Re-ellipsizes handle.NameLabel for a new rightEdge/qtyWidth -
        /// RunReellipsis-time only (ISectionRelayoutSink.AddReellipsis's own
        /// contract: settle, not every drag tick), never MeasureString-heavy
        /// per-frame work. Returns true only when the displayed text
        /// actually changed, mirroring both pre-extraction callers' own
        /// "if (nameLabel.Text != newDisplayName)" gate - the caller's own
        /// truncation-dependent follow-up (tooltip text, tag position)
        /// should run only then, exactly as it did before extraction.
        /// </summary>
        internal static bool ReellipsizeName(IconNameHandle handle, BitmapFont font, int rightEdge, int qtyWidth, int nameGap)
        {
            int newMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(rightEdge, qtyWidth, nameGap, handle.NameX);
            string newDisplayName = LabelHelpers.EllipsizeToWidth(font, handle.FullName, newMaxWidth);
            if (handle.NameLabel.Text == newDisplayName)
            {
                return false;
            }

            handle.NameLabel.Text = newDisplayName;
            return true;
        }
    }
}
