namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The module's two currency-icon sizes, matched to the game's own two
    /// wallet tiers (owner ruling, 2026-08-27) - the currency counterpart of
    /// <see cref="ItemIconTiers"/>, and Blish-free for the same reason: the
    /// layout math that reserves room for an icon and the view that draws it
    /// must read the same number.
    ///
    /// <para>
    /// MEASURED from the staged references
    /// gate-ranker/currency-wallet-list-reference.png and
    /// gate-ranker/currency-summary-bar-reference.png. Calibration follows
    /// the tooltip fidelity audit's method - a capture counts as native only
    /// once one of its metrics is shown to match a known native one - but
    /// lands a far tighter anchor than that audit's text pitch can:
    /// the live gold-coin currency texture (asset 156904) is 32x32, and
    /// template-matching it against the wallet LIST row is pixel-exact at
    /// scale 1.000 (mean squared error 2.0 over the texture's opaque pixels,
    /// against 887 and 1176 one pixel either side). A capture cannot match an
    /// unresampled source that closely unless it is native 1:1, so game
    /// pixels read 1:1 as module logical pixels - the same conclusion
    /// ItemIconTiers reached, independently corroborated here by the bag
    /// sidebar measuring the same 44px pitch in this capture as in
    /// bag-sidebar-icon-size-reference.png.
    /// </para>
    ///
    /// <para>
    /// The same coin triple appears at BOTH tiers in the game, which is why
    /// two constants exist: the LIST's Coins row draws gold/silver/copper at
    /// 32, and the summary bar redraws that identical run at 16.
    /// </para>
    ///
    /// <para>
    /// WHERE THE ICON SITS, MEASURED at both tiers: always to the RIGHT of
    /// the number (the repo's coin invariant, confirmed by the game itself),
    /// with its box centred on the number's INK rather than sitting on the
    /// baseline - list tier, icon box y178..209 (centre 193.5) against the
    /// "841" glyph ink y188..198 (centre 193.0); bar tier, box y114..129
    /// against ink y115..126, within a pixel at half the size. That is the
    /// rule the renderers already spell as
    /// <c>iconYOffset = (textHeight - iconSize) / 2</c>; recorded here so it
    /// need not be re-derived from a screenshot. The measured gap between the
    /// last glyph pixel and the icon box is 5px at list tier and 3px at bar
    /// tier, which is what CoinSegmentMath.CoinLabelIconGap approximates.
    /// </para>
    /// </summary>
    internal static class CurrencyIconTiers
    {
        /// <summary>
        /// TIER 1 - the in-game wallet LIST size (Inventory &gt; All
        /// Currencies, one row per currency): a currency TABLE ROW, where the
        /// icon is the row's subject rather than a unit marker on a number.
        /// The Summary section's currency table and the Snapshot tab's wallet
        /// rows are that table.
        ///
        /// <para>
        /// MEASURED 32: the gold coin's 32x32 texture matched at box
        /// (x329, y178) with error 2.0, sharply minimal. Row geometry from
        /// the same capture: the Coins row band is y173..214 (42px, the list's
        /// uniform row pitch) and the icon box is y178..209, so the icon is
        /// centred with 5px of clearance above and below - the derivation
        /// behind PlanContentHeightMath.CurrencyRowIconPad.
        /// </para>
        /// </summary>
        public const int WalletListIconSize = 32;

        /// <summary>
        /// TIER 2 - the in-game wallet SUMMARY BAR size (the compact inline
        /// runs pinned to the bottom edge of the Inventory window): an inline
        /// gold/silver/copper (or single-currency) run sitting beside a
        /// number inside a cell or a sentence. Every coin run the plan tables,
        /// Ranker, Snapshot header and tooltips draw is this tier - see
        /// CoinSegmentMath.CoinIconSize, which is defined as this constant.
        ///
        /// <para>
        /// MEASURED 16: the same 32x32 gold-coin texture matched at box
        /// (x414, y114) at size 16 with error 23.9, against 830 and 1412 one
        /// pixel either side, and identically in both captures (the list
        /// capture includes the bar). The tier ratio is therefore exactly
        /// 0.5, mirroring how the game halves its own texture.
        /// </para>
        /// </summary>
        public const int WalletBarIconSize = 16;

        /// <summary>
        /// Where the icon sits relative to the number it marks. MEASURED at
        /// both tiers: the icon is always to the RIGHT of the number (the
        /// repo's coin invariant, confirmed by the game itself), and its box
        /// is centred on the number's ink rather than sitting on its
        /// baseline - list tier, icon box y178..209 (centre 193.5) against
        /// the "841" glyph ink y188..198 (centre 193.0); bar tier, box
        /// y114..129 against ink y115..126, within a pixel at half the size.
        /// This is the rule the renderers already spell as
        /// <c>iconYOffset = (textHeight - iconSize) / 2</c>; it is recorded
        /// here so the next reader does not have to re-derive it from a
        /// screenshot.
        /// </summary>
        public const string VerticalAlignmentRule =
            "icon box centred on the number's ink, icon to the right of the number";
    }
}
