namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// One row of a (possibly multi-item) plan request: an item id and
    /// the quantity requested for it. Each row's Quantity becomes the
    /// ingredient quantity its item tree carries under the synthetic
    /// wrapper root.
    /// </summary>
    internal class PlanRequestItem
    {
        public int ItemId { get; set; }

        public int Quantity { get; set; }
    }
}
