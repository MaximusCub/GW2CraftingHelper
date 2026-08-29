using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TaimisToolbench.Models
{
    /// <summary>
    /// The solver's own acquisition vocabulary - what
    /// <see cref="Services.PlanSolver"/> decided for a node
    /// (<see cref="Services.SolverDecision.Source"/>) and what a
    /// <see cref="Models.PlanStep"/> was aggregated under. Deliberately a
    /// separate enum from the display-layer
    /// <see cref="Models.CraftingDecision"/>: the solver never needs an
    /// owned/ignored state and the tree builder never needs a distinct
    /// currency-leaf source.
    /// <para>
    /// The single bridge between the two vocabularies is
    /// <see cref="Services.CraftingTreeBuilder.MapSource"/> - keep any future
    /// member added here mirrored there. The per-member mapping table is in
    /// docs/ARCHITECTURE.md section S1.5.
    /// </para>
    /// </summary>
    // Serialized as its enum NAME, not Newtonsoft's bare-int default:
    // this type is persisted into plan.json, and a future member reorder
    // must not silently remap an already-persisted plan's decisions.
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum AcquisitionSource
    {
        BuyFromTp,
        Craft,
        Currency,
        BuyFromVendor,
        UnknownSource,
    }
}
