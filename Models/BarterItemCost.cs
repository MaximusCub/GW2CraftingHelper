namespace TaimisToolbench.Models
{
    /// <summary>
    /// One untradeable barter item's whole-plan cost: the account-bound
    /// tokens a vendor takes in place of coin, whose units ARE the price.
    /// Nothing of this is in <see cref="CraftingPlan.TotalCoinCost"/> -
    /// a barter line has no Trading Post price to fold in (see
    /// <see cref="VendorItemCostLine.GoldValue"/>), so a coin total that
    /// omitted this list would report a plan as costing less than it does.
    /// <para>
    /// The Item-id twin of <see cref="CurrencyCost"/>, and a separate type
    /// rather than a shared one because a GW2 item id and a GW2 currency id
    /// are different id spaces that collide numerically - the same reason
    /// Models/BarterItemDecisionDefaults.cs is separate from
    /// Models/CurrencyDecisionDefaults.cs. ItemId is internal-only (repo
    /// invariant); only the resolved name/icon ever reach the UI.
    /// </para>
    /// </summary>
    internal class BarterItemCost
    {
        public int ItemId { get; set; }

        public long Amount { get; set; }
    }
}
