namespace TaimisToolbench.Models
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

        /// <summary>
        /// The item's display name at request time. DISPLAY ONLY - nothing
        /// solves, prices, dedups or matches on it, and it may be null (a
        /// plan written before this member existed, or a row whose search
        /// never resolved a name). It exists for the one screen that has
        /// no other source: a request-only restore, where the result whose
        /// ItemMetadata normally names these rows is precisely what could
        /// not be read. See docs/ARCHITECTURE.md section 12.
        /// </summary>
        public string Name { get; set; }
    }
}
