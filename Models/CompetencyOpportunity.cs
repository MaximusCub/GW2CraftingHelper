using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Adversarial-review fix (#7, source-selection-simplification design-
    /// law gap): one node where the automatic buy-vs-craft-vs-vendor
    /// comparison would have picked Craft on cost alone, but no character
    /// meets the winning recipe's discipline requirement (see
    /// SolverDecision.CraftExcludedByCompetency) - a concrete, actionable
    /// opportunity ("train this discipline and save N") rather than a
    /// silent cost increase the user has no way to discover. Matches the
    /// maintainer's own design law: structured sections show the BEST-NOW
    /// option; opportunities/considerations go to Plan Notes with concrete
    /// numbers.
    /// </summary>
    public class CompetencyOpportunity
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
