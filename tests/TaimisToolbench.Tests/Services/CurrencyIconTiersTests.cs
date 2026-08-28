using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    // The currency counterpart of the item-icon tier pins: the two sizes
    // themselves, which module surface is on which tier, and the height
    // arithmetic that has to keep holding what those tiers draw.
    public class CurrencyIconTiersTests
    {
        // --- The measured pair ---
        //
        // Pinned absolutely, the same way the other geometry proofs in this
        // suite are: these came from template-matching the live 32x32 gold
        // coin texture against the staged wallet captures (see
        // Services/CurrencyIconTiers for the calibration), so a change here
        // is a claim about the GAME, not about the module, and has to be
        // re-measured rather than re-derived.
        [Fact]
        public void WalletTiers_ArePinnedToTheMeasuredCaptures()
        {
            Assert.Equal(32, CurrencyIconTiers.WalletListIconSize);
            Assert.Equal(16, CurrencyIconTiers.WalletBarIconSize);

            // The game halves its own texture between the two tiers; the
            // exact 2:1 is a measured fact, not a convenience.
            Assert.Equal(
                CurrencyIconTiers.WalletListIconSize,
                CurrencyIconTiers.WalletBarIconSize * 2);
        }

        // --- Which surface sits on which tier ---
        [Fact]
        public void InlineCoinRuns_DrawAtTheBarTier()
        {
            // Every "number then unit icon" run - shopping Each/Total, the
            // recipe tree's cost sub-columns, the Ranker's remaining cost,
            // the Snapshot header's wallet total, plan history's cost
            // column - measures and draws through CoinSegmentMath, so this
            // one equality is what puts all of them on the bar tier.
            Assert.Equal(CurrencyIconTiers.WalletBarIconSize, CoinSegmentMath.CoinIconSize);
        }

        [Fact]
        public void CurrencyTableRows_DrawAtTheListTier()
        {
            // The Summary's currency table is a wallet list - one row per
            // currency, icon as the row's subject - so it takes the larger
            // tier, unlike the inline runs directly above it in the same
            // section.
            Assert.Equal(
                CurrencyIconTiers.WalletListIconSize,
                SummarySectionLayoutMath.CurrencyIconSize);

            // The renderer insets the art by the module's 1px frame either
            // side (CreateItemIcon with CurrencyIconSize - 2 and border 1),
            // so the FRAMED box occupies exactly the measured window rather
            // than overflowing it - the same rule ItemIconTiers states.
            const int borderThickness = 1;
            int artSize = SummarySectionLayoutMath.CurrencyIconSize - (2 * borderThickness);
            Assert.Equal(
                CurrencyIconTiers.WalletListIconSize,
                artSize + (2 * borderThickness));
        }

        [Fact]
        public void RankerCurrencyShortfallLines_DrawAtTheListTier()
        {
            // Owner ruling, 2026-08-27, superseding this line's original
            // bar-tier seat: the Ranker's breakdown is a currency LIST - a
            // grid of named entries with their own amounts - and it reads at
            // the size the game's wallet list uses. The line carries its own
            // taller pitch to hold it.
            Assert.Equal(CurrencyIconTiers.WalletListIconSize, RankerRowLayout.CurrencyIconSize);
            Assert.True(RankerRowLayout.CurrencyLineHeight >= RankerRowLayout.CurrencyIconSize);
        }

        // --- The heights that carry them ---
        [Fact]
        public void CurrencyRowHeight_HoldsItsListTierIcon_CentredAsTheGameCentresIt()
        {
            Assert.True(
                PlanContentHeightMath.CurrencyRowHeight >= CurrencyIconTiers.WalletListIconSize,
                "a currency row cannot be shorter than the icon it draws");

            // Even difference, so the centring the renderer computes as
            // (rowHeight - iconSize) / 2 is exact rather than rounded, and
            // lands on the 5px the game's own 42px wallet row leaves above
            // and below its 32px icon.
            int slack = PlanContentHeightMath.CurrencyRowHeight - CurrencyIconTiers.WalletListIconSize;
            Assert.Equal(0, slack % 2);
            Assert.Equal(PlanContentHeightMath.CurrencyRowIconPad, slack / 2);
            Assert.Equal(5, PlanContentHeightMath.CurrencyRowIconPad);
        }

        [Fact]
        public void AmountRunHeight_IsNeverShorterThanEitherThingTheRunHolds()
        {
            // The regression this exists for: the formula bands used to
            // reserve their amount run as "the coin icon size", which was
            // only ever right because an inline coin happened to draw at
            // the amount text's own line height. Moving the coins onto the
            // 16px bar tier broke that coincidence, and a reserve that
            // followed the icon down would have stopped the band
            // bottom-anchoring its amount.
            Assert.True(PlanContentHeightMath.AmountRunHeight >= CoinSegmentMath.CoinIconSize);
            Assert.True(PlanContentHeightMath.AmountRunHeight >= PlanContentHeightMath.AmountTextLineHeight);
            Assert.Equal(
                PlanContentHeightMath.AmountTextLineHeight,
                TypeRampMetrics.Regular16.LineHeight);
        }
    }
}
