namespace TaimisToolbench.Models
{
    /// <summary>
    /// What one unit of a vendor offer's Item cost line costs to acquire,
    /// once the solver has run the same craft/TP/vendor comparison over it
    /// that a recipe ingredient already gets.
    /// <para>
    /// A NULL reference is the unresolved answer - no acquisition subtree, a
    /// cut recursion, or no priceable route under the line - and the caller
    /// then treats the line exactly as it did before cost lines were solved:
    /// a barter line worth no coin.
    /// </para>
    /// <para>
    /// The two figures are the solver's own pair and must not be conflated:
    /// <see cref="RealCoin"/> is coin actually spent and is what reaches a
    /// plan total, while <see cref="ComparisonExtra"/> is the decision-only
    /// remainder (a valued wallet currency somewhere under the line) that may
    /// move a comparison and must never be reported as gold.
    /// </para>
    /// <para>
    /// A mutable class with settable properties because it round-trips
    /// through PersistedPlan: PlanSolveContext snapshots the resolved values
    /// rather than the subtrees they came from, which is a few dozen small
    /// rows instead of several thousand RecipeNodes on a path that
    /// re-serializes on every override click.
    /// </para>
    /// </summary>
    internal class CostLineUnitValue
    {
        /// <summary>Coin per unit. Never includes a notional valuation.</summary>
        public long RealCoin { get; set; }

        /// <summary>
        /// Per-unit decision-only value on top of <see cref="RealCoin"/>,
        /// i.e. the subtree decision's ComparisonValue minus its TotalCost.
        /// Zero for a subtree that bottoms out purely in coin.
        /// </summary>
        public long ComparisonExtra { get; set; }

        /// <summary>
        /// The subtree carries a cost that has no honest coin equivalent (an
        /// unvalued currency, a barter line of its own). <see cref="RealCoin"/>
        /// is still real, but it is not the whole story, so an offer carrying
        /// this line is fallback-tier - the same treatment an unvalued line
        /// has always had.
        /// </summary>
        public bool HasUnvaluedCost { get; set; }
    }
}
