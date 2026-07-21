namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// One row of a (possibly multi-item) plan request: an item id and the
    /// quantity requested for it. M35 (gw2efficiency parity - multi-item
    /// plans): gw2e's own Calculator models a batch as exactly this shape,
    /// N of these rows (`e.recipes = [{ id, amount }, ...]` - see
    /// docs/gw2e-parity-spec.md / the M34 r1 multi-item research report),
    /// with each row's Quantity becoming the ingredient quantity its own
    /// item tree carries under the synthetic wrapper root (see
    /// RecipeService.BuildMultiItemTreeAsync and
    /// Gw2Constants.MultiItemWrapperItemId).
    /// </summary>
    public class PlanRequestItem
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
    }
}
