namespace GW2CraftingHelper.Models
{
    public class PlanStep
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public AcquisitionSource Source { get; set; }
        public long UnitCost { get; set; }
        public long TotalCost { get; set; }
        public int RecipeId { get; set; }
    }
}
