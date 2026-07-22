namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// The solver's own acquisition vocabulary - what <see cref="Services.PlanSolver"/>
    /// decided for a node (<see cref="Services.SolverDecision.Source"/>) and what a
    /// <see cref="Models.PlanStep"/> was aggregated under. This is a deliberately separate
    /// enum from the display-layer <see cref="Contracts.CraftingDecision"/> (M38 DO-NOT-TOUCH
    /// #15) - the two vocabularies diverge because the solver never needs an "owned/ignored"
    /// state (that is display-only, see <see cref="Contracts.CraftingDecision.Have"/>) while
    /// the tree builder never needs a distinct currency-leaf source (currency nodes are
    /// intercepted before any <see cref="AcquisitionSource"/> lookup - see below). The single
    /// bridge between the two is <see cref="Services.CraftingTreeBuilder.MapSource"/>; keep any
    /// future member added here mirrored there.
    ///
    /// Per-member mapping to <see cref="Contracts.CraftingDecision"/>:
    /// <list type="bullet">
    /// <item><see cref="Craft"/> -&gt; <see cref="Contracts.CraftingDecision.Craft"/></item>
    /// <item><see cref="BuyFromTp"/> -&gt; <see cref="Contracts.CraftingDecision.BuyFromTp"/></item>
    /// <item><see cref="BuyFromVendor"/> -&gt; <see cref="Contracts.CraftingDecision.BuyFromVendor"/></item>
    /// <item><see cref="Currency"/> -&gt; <see cref="Contracts.CraftingDecision.Currency"/> in
    /// principle, but in practice <see cref="Services.CraftingTreeBuilder"/> never routes a
    /// currency leaf through <see cref="Services.CraftingTreeBuilder.MapSource"/> at all - it
    /// sets <see cref="Contracts.CraftingDecision.Currency"/> directly as soon as it sees a
    /// non-"Item" ingredient type, before any decision lookup. This member exists because
    /// <see cref="Models.PlanStep.Source"/> shares this same enum for aggregation bookkeeping.
    /// </item>
    /// <item><see cref="UnknownSource"/> -&gt; <see cref="Contracts.CraftingDecision.Unknown"/> -
    /// a genuinely reachable production path (gw2efficiency's "Not sold or crafted": no recipe,
    /// no TP price, no vendor offer).</item>
    /// </list>
    /// <see cref="Contracts.CraftingDecision.Have"/> has no counterpart here - it is set
    /// directly by <see cref="Services.CraftingTreeBuilder"/> for zero-quantity/owned and
    /// manually-ignored nodes, never derived from an <see cref="AcquisitionSource"/> value.
    /// </summary>
    public enum AcquisitionSource
    {
        BuyFromTp,
        Craft,
        Currency,
        BuyFromVendor,
        UnknownSource
    }
}
