using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// The solver's own acquisition vocabulary - what <see cref="Services.PlanSolver"/>
    /// decided for a node (<see cref="Services.SolverDecision.Source"/>) and what a
    /// <see cref="Models.PlanStep"/> was aggregated under. This is a deliberately separate
    /// enum from the display-layer <see cref="Models.CraftingDecision"/> -
    /// the two vocabularies diverge because the solver never needs an "owned/ignored"
    /// state (that is display-only, see <see cref="Models.CraftingDecision.Have"/>) while
    /// the tree builder never needs a distinct currency-leaf source (currency nodes are
    /// intercepted before any <see cref="AcquisitionSource"/> lookup - see below). The single
    /// bridge between the two is <see cref="Services.CraftingTreeBuilder.MapSource"/>; keep any
    /// future member added here mirrored there.
    ///
    /// Per-member mapping to <see cref="Models.CraftingDecision"/>:
    /// <list type="bullet">
    /// <item><see cref="Craft"/> -&gt; <see cref="Models.CraftingDecision.Craft"/></item>
    /// <item><see cref="BuyFromTp"/> -&gt; <see cref="Models.CraftingDecision.BuyFromTp"/></item>
    /// <item><see cref="BuyFromVendor"/> -&gt; <see cref="Models.CraftingDecision.BuyFromVendor"/></item>
    /// <item><see cref="Currency"/> -&gt; <see cref="Models.CraftingDecision.Currency"/> in
    /// principle, but in practice <see cref="Services.CraftingTreeBuilder"/> never routes a
    /// currency leaf through <see cref="Services.CraftingTreeBuilder.MapSource"/> at all - it
    /// sets <see cref="Models.CraftingDecision.Currency"/> directly as soon as it sees a
    /// non-"Item" ingredient type, before any decision lookup. This member exists because
    /// <see cref="Models.PlanStep.Source"/> shares this same enum for aggregation bookkeeping.
    /// </item>
    /// <item><see cref="UnknownSource"/> -&gt; <see cref="Models.CraftingDecision.Unknown"/> -
    /// a genuinely reachable production path (gw2efficiency's "Not sold or crafted": no recipe,
    /// no TP price, no vendor offer).</item>
    /// </list>
    /// <see cref="Models.CraftingDecision.Have"/> has no counterpart here - it is set
    /// directly by <see cref="Services.CraftingTreeBuilder"/> for zero-quantity/owned and
    /// manually-ignored nodes, never derived from an <see cref="AcquisitionSource"/> value.
    /// </summary>
    // Serialized as its enum NAME, not Newtonsoft's bare-int default:
    // this type is persisted into plan.json, and a future member reorder
    // must not silently remap an already-persisted plan's decisions.
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AcquisitionSource
    {
        BuyFromTp,
        Craft,
        Currency,
        BuyFromVendor,
        UnknownSource,
    }
}
