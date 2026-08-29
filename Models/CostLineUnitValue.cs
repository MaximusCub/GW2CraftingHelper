namespace TaimisToolbench.Models
{
    /// <summary>
    /// What one unit of a vendor offer's Item cost line costs to acquire,
    /// once the solver has run the same craft/TP/vendor comparison over it
    /// that a recipe ingredient already gets - docs/ARCHITECTURE.md 7.4.
    /// <para>
    /// A NULL reference is the unresolved answer, and the caller then treats
    /// the line exactly as it did before cost lines were solved: a barter
    /// line worth no coin.
    /// </para>
    /// <para>
    /// A mutable class with settable properties because it round-trips
    /// through PersistedPlan - see PlanSolveContext.VendorCostLineValues.
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
