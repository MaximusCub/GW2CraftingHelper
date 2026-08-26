using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// One node where the automatic comparison would have picked Craft on
    /// cost alone, but no character meets the winning recipe's discipline
    /// requirement - a concrete "train this discipline and save N"
    /// opportunity rather than a silent cost increase.
    /// </summary>
    internal class CompetencyOpportunity
    {
        public int ItemId { get; set; }

        // The real coin cost craft would have committed at (never
        // valuation-inflated - same figure SolverDecision.
        // CraftExcludedRealCost carries).
        public long CraftCost { get; set; }

        // How much cheaper crafting would have been than the plan's actual
        // committed cost for this node - always > 0 (a note is only ever
        // built when craft would genuinely have saved money; see
        // CompetencyOpportunityCalculator.Apply).
        public long DeltaCost { get; set; }

        public IReadOnlyList<string> Disciplines { get; set; }

        public int MinRating { get; set; }
    }
}
