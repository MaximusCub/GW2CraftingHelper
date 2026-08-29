namespace TaimisToolbench.Services
{
    /// <summary>
    /// What one unit of a vendor offer's Item cost line costs to acquire,
    /// once the solver has run the same craft/TP/vendor comparison over it
    /// that a recipe ingredient already gets.
    /// <para>
    /// The two figures are the solver's own pair and must not be conflated:
    /// <see cref="RealCoin"/> is coin actually spent and is what reaches a
    /// plan total, while <see cref="ComparisonExtra"/> is the decision-only
    /// remainder (a valued wallet currency somewhere under the line) that may
    /// move a comparison and must never be reported as gold.
    /// </para>
    /// </summary>
    internal readonly struct CostLineUnitValue
    {
        /// <summary>
        /// Unresolved - no subtree, a cycle cut, an exhausted budget, or a
        /// subtree with no priceable route at all. The caller then treats the
        /// line exactly as it did before: a barter line worth no coin.
        /// </summary>
        public static readonly CostLineUnitValue Unresolved = default(CostLineUnitValue);

        public CostLineUnitValue(long realCoin, long comparisonExtra, bool hasUnvaluedCost)
        {
            RealCoin = realCoin;
            ComparisonExtra = comparisonExtra;
            HasUnvaluedCost = hasUnvaluedCost;
            IsResolved = true;
        }

        public bool IsResolved { get; }

        /// <summary>Coin per unit. Never includes a notional valuation.</summary>
        public long RealCoin { get; }

        /// <summary>
        /// Per-unit decision-only value on top of <see cref="RealCoin"/>,
        /// i.e. the subtree decision's ComparisonValue minus its TotalCost.
        /// Zero for a subtree that bottoms out purely in coin.
        /// </summary>
        public long ComparisonExtra { get; }

        /// <summary>
        /// The subtree carries a cost that has no honest coin equivalent (an
        /// unvalued currency, a barter line of its own). <see cref="RealCoin"/>
        /// is still real, but it is not the whole story, so an offer carrying
        /// this line is fallback-tier - the same treatment an unvalued line
        /// has always had.
        /// </summary>
        public bool HasUnvaluedCost { get; }
    }
}
