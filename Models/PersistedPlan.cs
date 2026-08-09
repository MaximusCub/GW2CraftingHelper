using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// On-disk shape for W3D (plan persistence across module restarts) -
    /// everything Module needs to restore the Crafting Plan tab instantly
    /// on module load, with no network call and no re-solve. See
    /// Services/PlanStore.cs for the store that reads/writes this.
    /// </summary>
    public class PersistedPlan
    {
        /// <summary>
        /// When this plan was originally generated (the same value the
        /// Crafting Plan tab's own "Generated: ..." header shows). Reused
        /// as-is by a later local override re-solve (see
        /// CraftingPlanPipeline.ResolveWithOverrides) - an override click
        /// re-solves locally with the SAME prices, it does not re-generate,
        /// so this timestamp (and the staleness banner it drives on
        /// restore) must not silently advance just because the user
        /// clicked a decision pill.
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// The original request (item ids + quantities) this plan was
        /// generated for, in request order - mirrors
        /// CraftingPlanResult.RequestedItems' own shape, but populated for
        /// every plan (single- or multi-item), not only a genuine
        /// multi-item batch. Not used to reconstruct the search box/
        /// quantity inputs on restore (W3D spec item 5 - those stay at
        /// their defaults); persisted for round-trip fidelity of the
        /// original request only.
        /// </summary>
        public IReadOnlyList<PlanRequestItem> RequestItems { get; set; }

        /// <summary>"Use Own Materials" checkbox state at generation time.</summary>
        public bool UseOwnMaterials { get; set; }

        /// <summary>Price basis (instant-buy vs. buy orders) at generation time.</summary>
        public PriceBasis PriceBasis { get; set; }

        /// <summary>
        /// The full displayed result, including its SolveContext - the
        /// prices/offers/metadata/tree a local
        /// CraftingPlanPipeline.ResolveWithOverrides re-solve needs to keep
        /// working with no network call after a restart (W3D spec item 3's
        /// correctness bar).
        /// </summary>
        public CraftingPlanResult Result { get; set; }
    }
}
