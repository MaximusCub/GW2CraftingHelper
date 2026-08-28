namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The closed vocabulary of item-icon sizes. Every framed icon in the
    /// module names one of these; none passes a bare number. The two
    /// governed tiers are the ruling; the rest are the surfaces the ruling
    /// exempts, named here rather than left as literals so "what size is
    /// that icon" has exactly one answer per surface and a new surface has
    /// to pick from this list instead of inventing a number.
    /// </summary>
    internal enum ItemIconTier
    {
        /// <summary>
        /// TIER 1 - in-game bag-slot art: the Snapshot tab's item grid, the
        /// Crafting Plan heading item, the Crafting Ranker's rows.
        /// </summary>
        BagSlot,

        /// <summary>
        /// TIER 2 - in-game bag-SIDEBAR art: every row-level icon in the
        /// Crafting Plan tab.
        /// </summary>
        BagSidebar,

        /// <summary>
        /// EXEMPT - the Snapshot tab's wallet rows. A currency has no
        /// rarity and its row is half the height of an item row.
        /// </summary>
        WalletRow,

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

        /// <summary>
        /// EXEMPT - a currency icon inline in a table cell (the plan
        /// summary's currency table, the Ranker's shortfall cells).
        /// </summary>
        InlineCurrency,

        /// <summary>
        /// EXEMPT - the Plan History tab's expanded per-item detail lines,
        /// which are caption-height.
        /// </summary>
        PlanHistoryDetail,
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
                case ItemIconTier.WalletRow: return 32;
                case ItemIconTier.SearchSuggestion: return 22;
                case ItemIconTier.TooltipHeader: return 32;
                case ItemIconTier.InlineCurrency: return 16;
                case ItemIconTier.PlanHistoryDetail: return 20;

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
