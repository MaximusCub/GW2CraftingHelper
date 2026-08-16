namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// opportunity-notes (RECIPE-SHEET SAVINGS): one item whose plan-chosen
    /// source is NOT craft, but crafting it instead would be cheaper once a
    /// purchasable recipe sheet is bought (and, possibly, a discipline
    /// trained) - see Services/RecipeSheetSavingsCalculator, the sole
    /// producer. Cosmetic display data only (same "advisory, never fed
    /// back into a decision or total" contract as ExcessCraftOutput's own
    /// doc comment) - PlanViewModelBuilder.BuildNotesSection is the only
    /// consumer.
    /// </summary>
    public class RecipeSheetSavingsOpportunity
    {
        /// <summary>The bought item this opportunity applies to.</summary>
        public int ItemId { get; set; }

        /// <summary>The missing, LearnedFromItem recipe that would craft it.</summary>
        public int RecipeId { get; set; }

        /// <summary>The purchasable recipe-sheet item that unlocks RecipeId.</summary>
        public int SheetItemId { get; set; }

        /// <summary>Cheapest coin cost found for the sheet itself.</summary>
        public long SheetCost { get; set; }

        /// <summary>
        /// Chosen-source unit cost minus craft-if-crafted unit cost, using
        /// RecipeId. Always &gt; 0 - the calculator never emits a
        /// non-positive or unpriceable delta.
        /// </summary>
        public long SavingsPerUnit { get; set; }

        /// <summary>
        /// True when no character on the account meets RecipeId's own
        /// discipline/rating requirement - drives the "train X to N and"
        /// wording variant. False (including when no account snapshot was
        /// available at all - see RecipeSheetSavingsCalculator's own doc
        /// comment) means the plain wording applies.
        /// </summary>
        public bool DisciplineBlocked { get; set; }

        /// <summary>
        /// The recipe's own (real, player-levelable) discipline - null when
        /// the recipe needs no such discipline (e.g. Mystic Forge/
        /// Achievement/Merchant-only) or DisciplineBlocked is false.
        /// </summary>
        public string Discipline { get; set; }

        /// <summary>Minimum rating required in Discipline. Meaningless when Discipline is null.</summary>
        public int RequiredRating { get; set; }
    }
}
