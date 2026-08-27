namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The module's two item-icon sizes, matched to the game's own two
    /// inventory tiers (owner ruling, 2026-08-26). Blish-free so the
    /// layout math that reserves room for an icon and the view that draws
    /// it read the same number.
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
    /// Art sizes below exclude the module's own 1-2px rarity frame, so the
    /// frame lands inside the measured window: 52+2 = 54 against the
    /// game's 54-56, 40+2 = 42 against 39-40 plus its border.
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
        /// Shopping List). Not yet applied there: those renderers' 36px
        /// row heights are load-bearing in the divider-vanishing immunity
        /// proof (see LabelHelpers.CreateRowDivider), so their resize is a
        /// follow-up that re-runs that proof per new row height.
        /// </summary>
        public const int BagSidebarIconSize = 40;
    }
}
