using System;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using TaimisToolbench.Models;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// The plan's page title: the rarity-framed item icon, the item name,
    /// the "x N needed" annotation, and the stat tooltip all three carry.
    /// Freshly constructed per render like every other renderer here, and
    /// stateless for the same reason.
    /// </summary>
    internal sealed class PlanHeaderRenderer
    {
        private readonly ISectionRelayoutSink _sink;
        private readonly Func<int, ItemStatBlock> _getItemStatBlock;

        internal PlanHeaderRenderer(ISectionRelayoutSink sink, Func<int, ItemStatBlock> getItemStatBlock)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _getItemStatBlock = getItemStatBlock;
        }

        /// <summary>
        /// Plan header: rarity-framed item icon + the item's own name in
        /// its rarity colour + a grey quantity, left-aligned at the
        /// content gutter every section below it also starts at.
        ///
        /// Three separate things used to compete here. The block was
        /// CENTRED while everything under it was left-aligned, so the plan
        /// had no single left edge. It carried a right-aligned "Generated:
        /// ..." panel duplicating - to the minute - the timestamp the
        /// fixed status strip 70px above already shows, so a plan opened
        /// with the same text twice. And its title shared DefaultFont18
        /// with every collapsible section header, leaving the page with no
        /// typographic top level at all.
        ///
        /// So: the in-scroll timestamp is gone (the strip keeps it, and it
        /// never scrolls away); the title is left-aligned and rendered at
        /// DefaultFont32, and CreateSectionHeader drops to DefaultFont16,
        /// so Font18-and-up now belongs to the page title alone. The
        /// "Crafting Plan for " prefix is gone with it - the tab is
        /// already titled "Crafting Plan" and the strip already says "Plan
        /// generated", so the prefix cost half the title's width to repeat
        /// what two other elements say.
        /// </summary>
        internal void Render(PlanViewModel vm, FlowPanel contentPanel, int panelWidth)
        {
            // Tier 1 of the two-tier icon system (owner ruling): the plan's
            // heading item carries in-game bag-slot-sized art, like the
            // Snapshot grid and the Ranker rows. The frame thickness comes
            // from the tier now, not from a local constant - this header
            // was the one tier-1 site drawing a 2px frame, so its icon was
            // 56px where every other tier-1 icon was 54. Header height is
            // unchanged at 64, which now clears the 54px frame by 5px a
            // side instead of 4.
            const int headerHeight = 64;
            const int iconPad = 10;

            // Same 8px content gutter the Summary section's tiles, the
            // currency table's icon column and the footnote all start at.
            const int headerX = 8;

            int frameSize = ItemIconTiers.FrameSize(ItemIconTier.BagSlot);

            var titleFont = UiFonts.Display;

            // Regular weight, one tier down from the title it annotates -
            // and not the 18-regular it used to be, whose 4px space glyph
            // rendered " x 42 needed" no wider than Body did.
            var qtyFont = UiFonts.SmallHeading;

            string nameText = vm.TargetItemName ?? "Unknown Item";

            // "needed", not a bare count: the quantity here is what the
            // plan still has to obtain after owned materials were
            // subtracted, which is routinely smaller than the number in
            // the Qty box the user typed (live capture ph13: box 77,
            // header 42, 35 already owned). A bare "x 42" beside a box
            // reading 77 reads as a bug. Deliberately not "to craft" -
            // a root the solver decided to BUY is just as legitimate.
            string qtyText = vm.TargetQuantity > 1 ? $" x {vm.TargetQuantity} needed" : "";

            var nameMeasure = titleFont.MeasureString(nameText);
            int nameWidth = (int)System.Math.Ceiling(nameMeasure.Width);
            int textHeight = (int)System.Math.Ceiling(nameMeasure.Height);

            int qtyHeight = 0;
            if (qtyText.Length > 0)
            {
                qtyHeight = (int)System.Math.Ceiling(qtyFont.MeasureString(qtyText).Height);
            }

            int iconY = (headerHeight - frameSize) / 2;
            int textY = iconY + (frameSize - textHeight) / 2;
            // Bottom-aligned against the much taller name rather than
            // top-aligned, with a small optical lift off the descender
            // line, so the two sit on one reading line.
            int qtyY = textY + textHeight - qtyHeight - 4;

            var titlePanel = new Panel()
            {
                Size = new Point(panelWidth, headerHeight),
                Parent = contentPanel,
            };

            // PlanViewModel carries no target item id of its own, so the
            // tree root - the very item this header names - is the id. A
            // multi-item batch has no single target (TreeRoot is null there
            // by design) and no stat block either; the header row is
            // composed from what the header already draws, so the hover
            // opens on the same icon and name whether or not a stat block
            // has landed.
            //
            // Composed at hover time, so a plan restored from disk shows
            // its stats as soon as the background top-up lands (Q13).
            var treeRoot = vm.TreeRoot;
            var identity = ItemTooltipIdentity.ForItem(
                nameText, vm.TargetIconUrl, vm.TargetRarity);
            var hover = ItemIconTooltip.Composed(
                identity,
                () => ItemRowTooltipComposer.BuildRowContent(
                    TreeRowTooltipComposer.BuildStatTooltipContent(treeRoot, _getItemStatBlock),
                    identity,
                    (TooltipContent)null));

            IconControls.CreateItemIcon(
                titlePanel, vm.TargetIconUrl, ItemIconFrame.ForRarity(vm.TargetRarity),
                headerX, iconY, ItemIconTier.BagSlot, hover);

            int textX = headerX + frameSize + iconPad;
            var nameLabel = new Label()
            {
                Text = nameText,
                Font = titleFont,
                TextColor = RarityColors.GetRarityNameColor(vm.TargetRarity),
                ShowShadow = true,
                ShadowColor = Color.Black * 0.8f,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(textX, textY),
                Parent = titlePanel,
            };

            if (qtyText.Length > 0)
            {
                var qtyLabel = new Label()
                {
                    Text = qtyText,
                    Font = qtyFont,
                    TextColor = new Color(170, 170, 170),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(textX + nameWidth, qtyY),
                    Parent = titlePanel,
                };
            }

            // Every x here is now a constant or a font-only measurement,
            // so nothing in the title moves with the panel width - only
            // the panel's own cosmetic width, same as TextRowRenderer's
            // rows. The centring anchor (and the right-aligned timestamp
            // that needed one) is gone.
            _sink.AddRelayout(w => titlePanel.Size = new Point(w, headerHeight));
        }
    }
}
