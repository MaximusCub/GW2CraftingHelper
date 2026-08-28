namespace TaimisToolbench.Services
{
    /// <summary>
    /// The closed vocabulary of framed-icon sizes: what a call site names
    /// instead of passing a number. Nothing is measured here - every tier
    /// resolves to a constant on <see cref="ItemIconTiers"/> or
    /// <see cref="CurrencyIconTiers"/>, which own the measurements. This is
    /// the vocabulary; those two are the ruler.
    ///
    /// <para>
    /// Four of the six are governed by an owner ruling (two item tiers, two
    /// currency tiers). The last two are the surfaces no ruling covers, named
    /// here rather than left as literals so every icon in the module traces
    /// to a name and a new surface has to pick from this list instead of
    /// inventing a number.
    /// </para>
    /// </summary>
    internal enum ItemIconTier
    {
        /// <summary>
        /// ITEM TIER 1 - in-game bag-slot art: the Snapshot tab's item grid,
        /// the Crafting Plan heading item, the Crafting Ranker's rows, the
        /// Plan History tab's collapsed rows.
        /// </summary>
        BagSlot,

        /// <summary>
        /// ITEM TIER 2 - in-game bag-SIDEBAR art: every row-level icon in
        /// the Crafting Plan tab, and the Plan History tab's expanded
        /// per-item detail lines.
        /// </summary>
        BagSidebar,

        /// <summary>
        /// CURRENCY TIER 1 - in-game wallet LIST art, where the icon is a
        /// table row's subject rather than a unit marker on a number: the
        /// Snapshot tab's wallet rows and the plan Summary's currency table.
        /// </summary>
        CurrencyListRow,

        /// <summary>
        /// CURRENCY TIER 2 - in-game wallet SUMMARY BAR art, for a currency
        /// icon inline beside a number inside a cell (the Ranker's shortfall
        /// cells). The coin runs themselves are unframed and go through
        /// CoinCurrencyRenderer, not here.
        /// </summary>
        CurrencyBarRun,

        /// <summary>
        /// EXEMPT - the item-search suggestion list, whose row height is
        /// fixed by the dropdown it drops out of.
        /// </summary>
        SearchSuggestion,

        /// <summary>
        /// EXEMPT - the rich tooltip's header icon, sized to the game's own
        /// tooltip rather than to the game's bags.
        /// </summary>
        TooltipHeader,
    }

    /// <summary>
    /// The module's two item-icon sizes, matched to the game's own two
    /// inventory tiers (owner ruling, 2026-08-26), plus the pixel table
    /// behind <see cref="ItemIconTier"/>. Blish-free so the layout math
    /// that reserves room for an icon and the view that draws it read the
    /// same number.
    ///
    /// <para>
    /// MEASURED from the staged references against the in-game tooltip
    /// text (~14px, the same class as UiFonts.Body, so game pixels at
    /// default UI scale read 1:1 as module logical pixels):
    /// bag-icon-size-reference.png shows a main bag grid at ~59-60px slot
    /// pitch with ~54-56px of slot art; bag-sidebar-icon-size-reference.png
    /// shows the bag side bar at ~44px pitch with ~39-40px of art - a
    /// sidebar:slot art ratio of ~0.72.
    /// </para>
    ///
    /// <para>
    /// Art sizes below exclude the module's own rarity frame, so the frame
    /// lands inside the measured window: 52+2 = 54 against the game's
    /// 54-56, 40+2 = 42 against 39-40 plus its border. That derivation is
    /// why the frame is 1px at every tier - a 2px frame would put tier 1 at
    /// 56, the top of the measured window rather than inside it - and why
    /// the thickness is a property of the tier here rather than a number
    /// each call site chooses.
    /// </para>
    /// </summary>
    internal static class ItemIconTiers
    {
        /// <summary>
        /// TIER 1 - in-game bag-slot size: the Snapshot tab's item grid,
        /// the Crafting Plan heading item, the Crafting Ranker's rows.
        /// </summary>
        public const int BagSlotIconSize = 52;

        /// <summary>
        /// TIER 2 - in-game bag-SIDEBAR size: the Crafting Plan tab's
        /// row-level icons (recipe tree, Used Materials, Required Recipes,
        /// Shopping List, Crafting Steps). The row heights that carry these
        /// icons are derived from this constant in
        /// PlanContentHeightMath (RowIconFrameSize and the row-height sums
        /// built on it), and the divider-vanishing immunity proof was
        /// re-run at those heights - see LabelHelpers.CreateRowDivider and
        /// the executable re-derivation in
        /// tests/.../RowDividerScissorSimulationTests.cs.
        /// </summary>
        public const int BagSidebarIconSize = 40;

        /// <summary>
        /// The rarity frame, at every tier. See the derivation above: the
        /// measured art windows already account for one pixel of module
        /// frame on each side.
        /// </summary>
        public const int FrameBorder = 1;

        /// <summary>Art size, in logical pixels, of one tier's icon.</summary>
        public static int ArtSize(ItemIconTier tier)
        {
            switch (tier)
            {
                case ItemIconTier.BagSlot: return BagSlotIconSize;
                case ItemIconTier.BagSidebar: return BagSidebarIconSize;

                // The two currency tiers read their measurement off
                // CurrencyIconTiers rather than restating it, so the module
                // has ONE source of truth per measured window. They differ
                // from the item tiers in where the frame sits: an item tier's
                // measured window is the ART (the game draws no frame of its
                // own around a bag slot, so the module's 1px sits outside
                // it), while a currency tier's measured window is the whole
                // BOX - the wallet list's 32px is the icon's footprint in the
                // row. So the art is inset by the frame and the framed box
                // lands exactly on the measurement instead of overflowing it
                // by 2. Both currency call sites already did this arithmetic
                // inline; here it happens once.
                case ItemIconTier.CurrencyListRow:
                    return CurrencyIconTiers.WalletListIconSize - (2 * FrameBorder);
                case ItemIconTier.CurrencyBarRun:
                    return CurrencyIconTiers.WalletBarIconSize - (2 * FrameBorder);

                // The two the rulings do not cover. Their numbers ARE the
                // measurement, because the surface each belongs to is the
                // only thing that sizes them: the suggestion dropdown's 24px
                // row box, and the game tooltip's own 32px header icon.
                case ItemIconTier.SearchSuggestion: return 22;
                case ItemIconTier.TooltipHeader: return 32;

                // Not a fallback that guesses a size: an unnamed tier is a
                // programming error, and a silent default is how the module
                // grew its icon-size variations in the first place.
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(tier), tier, "No art size is defined for this icon tier.");
            }
        }

        /// <summary>Rarity-frame thickness of one tier's icon.</summary>
        public static int BorderThickness(ItemIconTier tier)
        {
            // Uniform across tiers today, and asked for by tier rather than
            // read as a constant so a future tier-specific frame becomes a
            // switch here instead of a number at every call site. Routed
            // through ArtSize for its validity check, so an unnamed tier
            // throws rather than quietly answering 1.
            ArtSize(tier);
            return FrameBorder;
        }

        /// <summary>
        /// Overall edge of one tier's framed icon - art plus the frame on
        /// both sides. This is the number layout math reserves, never the
        /// art size alone.
        /// </summary>
        public static int FrameSize(ItemIconTier tier)
        {
            return ArtSize(tier) + (2 * BorderThickness(tier));
        }
    }
}
